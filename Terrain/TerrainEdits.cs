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
/// A batch of per-vertex terrain edits in world coordinates. Heights live on the heightmap's vertices (whole
/// metres) and paint on its mask texels (the centres of the 1 m cells between them). <see cref="Send"/> finds
/// every heightmap each point falls in, so points on a border between two heightmaps change both, and sends
/// each heightmap's share to the owner of its terrain compiler (see <see cref="TerrainRpc"/>).
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

    private readonly struct PaintEdit(Vector3 point, Color color)
    {
        public readonly Vector3 Point = point;
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
    /// Paints the 1 m cell whose centre is (x, z).
    /// </summary>
    public void Paint(float x, float z, Color color)
    {
        _paints.Add(new PaintEdit(new Vector3(x, 0f, z), color));
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

        List<int> paintIndices = [];
        List<PaintEdit> paints = [];
        // Like vanilla's TerrainOp m_halfOffset: a paint texel covers the cell whose lower corner is its vertex,
        // so look the texel up half a cell back from the cell's centre.
        Vector3 halfCell = new(0.5f, 0f, 0.5f);
        foreach (PaintEdit edit in _paints)
        {
            map.WorldToVertexMask(edit.Point - halfCell, out int x, out int y);
            if (x >= 0 && y >= 0 && x <= width && y <= width)
            {
                paintIndices.Add((y * pitch) + x);
                paints.Add(edit);
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
            package.Write(paints[i].Color.r);
            package.Write(paints[i].Color.g);
            package.Write(paints[i].Color.b);
        }

        return package;
    }
}
