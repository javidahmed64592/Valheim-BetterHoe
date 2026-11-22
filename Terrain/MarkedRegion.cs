using System.Collections.Generic;

using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// Ground marked for a tool to change all at once: a set of 1 m cells, each named by its south-west vertex.
/// Alt+middle-click adds the area under the aim and Alt+right-click takes it away again, so any shape can be
/// drawn out with the area as a brush. While cells are marked, the area tools change every one of them in one
/// use. The region stays marked after a tool is used, so several tools can be used on it in turn, until it's
/// cleared. After clearing, the clear key marks the same ground again while nothing is marked, like
/// BetterHammer's reselect. It's only kept for this session.
/// </summary>
internal static class MarkedRegion
{
    private static readonly HashSet<(int x, int z)> Cells = [];

    /// <summary>
    /// The cells marked when the region was last cleared, for <see cref="Restore"/>.
    /// </summary>
    private static readonly HashSet<(int x, int z)> Previous = [];

    private static int _minX;
    private static int _minZ;
    private static int _maxX;
    private static int _maxZ;

    /// <summary>
    /// Goes up whenever the cells change, so the overlay knows to redraw.
    /// </summary>
    public static int Version { get; private set; }

    public static bool IsEmpty => Cells.Count == 0;

    /// <summary>
    /// True when nothing is marked and there's a cleared region to mark again.
    /// </summary>
    public static bool CanRestore => Cells.Count == 0 && Previous.Count > 0;

    public static int CellCount => Cells.Count;

    /// <summary>
    /// The smallest rectangle around every marked cell.
    /// </summary>
    public static Area Bounds => new(_minX, _minZ, _maxX - _minX, _maxZ - _minZ);

    public static bool Contains(int x, int z)
    {
        return Cells.Contains((x, z));
    }

    /// <summary>
    /// Marks every cell of the area. Returns false, marking nothing, when the region would then be wider or longer
    /// than `maxSize` metres.
    /// </summary>
    public static bool Mark(Area area, int maxSize)
    {
        int minX = IsEmpty ? area.MinX : Mathf.Min(_minX, area.MinX);
        int minZ = IsEmpty ? area.MinZ : Mathf.Min(_minZ, area.MinZ);
        int maxX = IsEmpty ? area.MaxX : Mathf.Max(_maxX, area.MaxX);
        int maxZ = IsEmpty ? area.MaxZ : Mathf.Max(_maxZ, area.MaxZ);
        if (maxX - minX > maxSize || maxZ - minZ > maxSize)
        {
            return false;
        }

        bool changed = false;
        for (int x = area.MinX; x < area.MaxX; x++)
        {
            for (int z = area.MinZ; z < area.MaxZ; z++)
            {
                changed |= Cells.Add((x, z));
            }
        }

        if (changed)
        {
            (_minX, _minZ, _maxX, _maxZ) = (minX, minZ, maxX, maxZ);
            Version++;
        }

        return true;
    }

    /// <summary>
    /// Unmarks every cell of the area. Returns how many were marked.
    /// </summary>
    public static int Unmark(Area area)
    {
        int removed = 0;
        for (int x = area.MinX; x < area.MaxX; x++)
        {
            for (int z = area.MinZ; z < area.MaxZ; z++)
            {
                if (Cells.Remove((x, z)))
                {
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            RecalculateBounds();
            Version++;
        }

        return removed;
    }

    /// <summary>
    /// Unmarks everything, remembering it so <see cref="Restore"/> can mark it again.
    /// </summary>
    public static void Clear()
    {
        if (Cells.Count > 0)
        {
            Previous.Clear();
            Previous.UnionWith(Cells);
            Cells.Clear();
            Version++;
        }
    }

    /// <summary>
    /// Marks the last cleared region again. Returns how many cells it marked.
    /// </summary>
    public static int Restore()
    {
        if (!CanRestore)
        {
            return 0;
        }

        Cells.UnionWith(Previous);
        RecalculateBounds();
        Version++;
        return Cells.Count;
    }

    /// <summary>
    /// Forgets the region and the cleared one, on logging out.
    /// </summary>
    public static void Reset()
    {
        Previous.Clear();
        if (Cells.Count > 0)
        {
            Cells.Clear();
            Version++;
        }
    }

    /// <summary>
    /// Every vertex on a corner of a marked cell, once each.
    /// </summary>
    public static List<(int x, int z)> Vertices()
    {
        HashSet<(int x, int z)> vertices = [];
        foreach ((int x, int z) in Cells)
        {
            _ = vertices.Add((x, z));
            _ = vertices.Add((x + 1, z));
            _ = vertices.Add((x, z + 1));
            _ = vertices.Add((x + 1, z + 1));
        }

        return [.. vertices];
    }

    /// <summary>
    /// The marked cells as rectangles to paint: each run of marked cells along a row (west to east) is one
    /// rectangle, 1 m long from south to north.
    /// </summary>
    public static List<Area> Rows()
    {
        List<Area> rows = [];
        for (int z = _minZ; z < _maxZ; z++)
        {
            int start = int.MinValue;
            for (int x = _minX; x <= _maxX; x++)
            {
                bool marked = x < _maxX && Cells.Contains((x, z));
                if (marked && start == int.MinValue)
                {
                    start = x;
                }
                else if (!marked && start != int.MinValue)
                {
                    rows.Add(new Area(start, z, x - start, 1));
                    start = int.MinValue;
                }
            }
        }

        return rows;
    }

    private static void RecalculateBounds()
    {
        bool first = true;
        foreach ((int x, int z) in Cells)
        {
            if (first)
            {
                (_minX, _minZ, _maxX, _maxZ) = (x, z, x + 1, z + 1);
                first = false;
                continue;
            }

            _minX = Mathf.Min(_minX, x);
            _minZ = Mathf.Min(_minZ, z);
            _maxX = Mathf.Max(_maxX, x + 1);
            _maxZ = Mathf.Max(_maxZ, z + 1);
        }
    }
}
