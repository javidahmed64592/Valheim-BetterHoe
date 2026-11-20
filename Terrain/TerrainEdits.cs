using System.Collections.Generic;

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

    private readonly struct PaintEdit(Rect rect, Color color)
    {
        /// <summary>
        /// World x / z bounds.
        /// </summary>
        public readonly Rect Rect = rect;
        public readonly Color Color = color;
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
        _paints.Add(new PaintEdit(Rect.MinMaxRect(minX, minZ, maxX, maxZ), color));
    }

    /// <summary>
    /// Sends the edits. `center` and `radius` cover every point; the terrain uses them to regrow the grass.
    /// Returns false when no point was on loaded terrain.
    /// </summary>
    public bool Send(Vector3 center, float radius)
    {
        TempMaps.Clear();
        Heightmap.FindHeightmap(center, radius, TempMaps);
        bool sent = false;

        foreach (Heightmap map in TempMaps)
        {
            ZPackage package = Pack(map, center, radius);
            if (package == null)
            {
                continue;
            }

            TerrainComp compiler = map.GetAndCreateTerrainCompiler();
            if (compiler != null && TerrainRpc.Send(compiler, package))
            {
                sent = true;
            }
        }

        return sent;
    }

    private ZPackage Pack(Heightmap map, Vector3 center, float radius)
    {
        int width = map.m_width;
        int pitch = width + 1;

        List<int> heightIndices = [];
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

        List<int> paintIndices = [];
        List<Color> paints = [];
        foreach (PaintEdit edit in _paints)
        {
            TexelRange(edit.Rect.xMin - corner.x, edit.Rect.xMax - corner.x, texel, width, out int x0, out int x1);
            TexelRange(edit.Rect.yMin - corner.z, edit.Rect.yMax - corner.z, texel, width, out int y0, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    paintIndices.Add((y * pitch) + x);
                    paints.Add(edit.Color);
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
