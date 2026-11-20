using System.Collections.Generic;

using BetterHoe.History;

using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// How a height edit changes a terrain vertex.
/// </summary>
internal enum HeightMode : byte
{
    /// <summary>
    /// Move the vertex towards an absolute world height, by the edit's weight (1 = all the way).
    /// </summary>
    Set = 0,

    /// <summary>
    /// Move the vertex up or down by the edit's value.
    /// </summary>
    Add = 1,

    /// <summary>
    /// Undo every height change to the vertex, back to its natural height.
    /// </summary>
    Reset = 2,
}

/// <summary>
/// A batch of terrain edits in world coordinates. Heights live on the heightmap's vertices (whole metres).
/// Paint lives in a mask texture of (width + 1) texels that the terrain mesh stretches over its width, so a
/// texel's centre drifts from the 1 m grid by up to a whole metre across a heightmap; paint is therefore given
/// as rectangles, and every texel drawn with its centre inside one is painted. <see cref="Send"/> finds every
/// heightmap the edits fall in, so edits on a border between two heightmaps change both, and sends each
/// heightmap's share to the owner of its terrain compiler (see <see cref="TerrainRpc"/>).
/// </summary>
internal sealed class TerrainEdits
{
    private readonly struct HeightEdit(Vector3 point, HeightMode mode, float value, float weight)
    {
        public readonly Vector3 Point = point;
        public readonly HeightMode Mode = mode;
        public readonly float Value = value;
        public readonly float Weight = weight;
    }

    /// <summary>
    /// A convex quad in world x / z, corners in order around it, and its paint colour.
    /// </summary>
    private readonly struct PaintEdit(Vector2[] corners, Color color)
    {
        public readonly Vector2[] Corners = corners;
        public readonly Color Color = color;

        public Rect Bounds
        {
            get
            {
                Vector2 min = Corners[0];
                Vector2 max = Corners[0];
                foreach (Vector2 corner in Corners)
                {
                    min = Vector2.Min(min, corner);
                    max = Vector2.Max(max, corner);
                }

                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        /// <summary>
        /// True when the point is inside the quad (on the same side of every edge).
        /// </summary>
        public bool Contains(Vector2 point)
        {
            bool negative = false;
            bool positive = false;
            for (int i = 0; i < Corners.Length; i++)
            {
                Vector2 a = Corners[i];
                Vector2 b = Corners[(i + 1) % Corners.Length];
                float cross = ((b.x - a.x) * (point.y - a.y)) - ((b.y - a.y) * (point.x - a.x));
                negative |= cross < -0.0001f;
                positive |= cross > 0.0001f;
            }

            return !(negative && positive);
        }
    }

    private readonly List<HeightEdit> _heights = [];
    private readonly List<PaintEdit> _paints = [];

    private static readonly List<Heightmap> TempMaps = [];

    public bool IsEmpty => _heights.Count == 0 && _paints.Count == 0;

    public void SetHeight(float x, float z, float worldHeight, float weight = 1f)
    {
        _heights.Add(new HeightEdit(new Vector3(x, 0f, z), HeightMode.Set, worldHeight, weight));
    }

    public void AddHeight(float x, float z, float delta)
    {
        _heights.Add(new HeightEdit(new Vector3(x, 0f, z), HeightMode.Add, delta, 1f));
    }

    public void ResetHeight(float x, float z)
    {
        _heights.Add(new HeightEdit(new Vector3(x, 0f, z), HeightMode.Reset, 0f, 1f));
    }

    /// <summary>
    /// Paints the world rectangle from (minX, minZ) to (maxX, maxZ).
    /// </summary>
    public void Paint(float minX, float minZ, float maxX, float maxZ, Color color)
    {
        _paints.Add(new PaintEdit([new(minX, minZ), new(maxX, minZ), new(maxX, maxZ), new(minX, maxZ)], color));
    }

    /// <summary>
    /// Paints a convex quad given by its world corners in order around it (heights are ignored).
    /// </summary>
    public void Paint(Vector3[] corners, Color color)
    {
        Vector2[] flat = new Vector2[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            flat[i] = new Vector2(corners[i].x, corners[i].z);
        }

        _paints.Add(new PaintEdit(flat, color));
    }

    /// <summary>
    /// Sends the edits. `center` and `radius` cover every point; the terrain uses them to regrow the grass.
    /// `before` gets the data the edits are about to change, for undoing them. Returns false when no point was
    /// on loaded terrain.
    /// </summary>
    public bool Send(Vector3 center, float radius, List<TerrainSnapshot> before)
    {
        TempMaps.Clear();
        Heightmap.FindHeightmap(center, radius, TempMaps);
        before.Clear();

        // Pack and capture everything first: an RPC to ourselves runs straight away and would change the data.
        List<(TerrainComp compiler, ZPackage package)> sends = [];
        foreach (Heightmap map in TempMaps)
        {
            ZPackage package = Pack(map, center, radius, out List<int> heightIndices, out List<int> paintIndices);
            if (package == null)
            {
                continue;
            }

            TerrainComp compiler = map.GetAndCreateTerrainCompiler();
            if (compiler == null || !compiler.m_initialized)
            {
                continue;
            }

            before.Add(TerrainSnapshot.Capture(compiler, heightIndices, paintIndices));
            sends.Add((compiler, package));
        }

        bool sent = false;
        foreach ((TerrainComp compiler, ZPackage package) in sends)
        {
            sent |= TerrainRpc.Send(compiler, package);
        }

        return sent;
    }

    private ZPackage Pack(Heightmap map, Vector3 center, float radius, out List<int> heightIndices, out List<int> paintIndices)
    {
        int width = map.m_width;
        int pitch = width + 1;

        heightIndices = [];
        List<HeightEdit> heights = [];
        foreach (HeightEdit edit in _heights)
        {
            map.WorldToVertex(edit.Point, out int x, out int y);
            if (x >= 0 && y >= 0 && x <= width && y <= width)
            {
                heightIndices.Add((y * pitch) + x);
                heights.Add(edit);
            }
        }

        // The mesh's UVs run from 0 to 1 across its width, and the mask has `pitch` texels, so texel t is drawn
        // centred at (t + 0.5) * texel from the heightmap's south-west corner.
        float size = width * map.m_scale;
        float texel = size / pitch;
        Vector3 corner = map.transform.position - new Vector3(size / 2f, 0f, size / 2f);

        paintIndices = [];
        List<Color> paints = [];
        foreach (PaintEdit edit in _paints)
        {
            Rect bounds = edit.Bounds;
            TexelRange(bounds.xMin - corner.x, bounds.xMax - corner.x, texel, width, out int x0, out int x1);
            TexelRange(bounds.yMin - corner.z, bounds.yMax - corner.z, texel, width, out int y0, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 drawn = new(corner.x + ((x + 0.5f) * texel), corner.z + ((y + 0.5f) * texel));
                    if (edit.Contains(drawn))
                    {
                        paintIndices.Add((y * pitch) + x);
                        paints.Add(edit.Color);
                    }
                }
            }
        }

        if (heights.Count == 0 && paints.Count == 0)
        {
            return null;
        }

        ZPackage package = new();
        package.Write(TerrainRpc.Version);
        package.Write(center);
        package.Write(radius);

        package.Write(heights.Count);
        for (int i = 0; i < heights.Count; i++)
        {
            package.Write(heightIndices[i]);
            package.Write((byte)heights[i].Mode);
            package.Write(heights[i].Value);
            package.Write(heights[i].Weight);
        }

        package.Write(paints.Count);
        for (int i = 0; i < paints.Count; i++)
        {
            package.Write(paintIndices[i]);
            package.Write(paints[i].r);
            package.Write(paints[i].g);
            package.Write(paints[i].b);
        }

        return package;
    }

    /// <summary>
    /// The texels whose drawn centres lie in [from, to), measured from the heightmap's corner, clamped to the
    /// mask. The range is empty (first &gt; last) when none do.
    /// </summary>
    private static void TexelRange(float from, float to, float texel, int width, out int first, out int last)
    {
        first = Mathf.Max(0, Mathf.CeilToInt((from / texel) - 0.5f - 0.0001f));
        last = Mathf.Min(width, Mathf.CeilToInt((to / texel) - 0.5f - 0.0001f) - 1);
    }
}
