using System;

using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// A rectangle of terrain whose corners sit on heightmap vertices (whole metres). It covers
/// (SizeX + 1) x (SizeZ + 1) height vertices and SizeX x SizeZ paint cells.
/// </summary>
internal readonly struct Area(int minX, int minZ, int sizeX, int sizeZ) : IEquatable<Area>
{
    public readonly int MinX = minX;
    public readonly int MinZ = minZ;
    public readonly int SizeX = sizeX;
    public readonly int SizeZ = sizeZ;

    public int MaxX => MinX + SizeX;
    public int MaxZ => MinZ + SizeZ;

    public float CenterX => MinX + (SizeX / 2f);
    public float CenterZ => MinZ + (SizeZ / 2f);

    /// <summary>
    /// Half the diagonal: a circle of this radius around the centre covers the whole area.
    /// </summary>
    public float Radius => Mathf.Sqrt((SizeX * SizeX) + (SizeZ * SizeZ)) / 2f;

    /// <summary>
    /// The area of the given size centred as closely as possible on `aim`.
    /// </summary>
    public static Area Around(Vector3 aim, int sizeX, int sizeZ)
    {
        return new Area(Mathf.RoundToInt(aim.x - (sizeX / 2f)), Mathf.RoundToInt(aim.z - (sizeZ / 2f)), sizeX, sizeZ);
    }

    public bool Equals(Area other)
    {
        return MinX == other.MinX && MinZ == other.MinZ && SizeX == other.SizeX && SizeZ == other.SizeZ;
    }

    public override bool Equals(object obj)
    {
        return obj is Area other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (((((MinX * 397) ^ MinZ) * 397) ^ SizeX) * 397) ^ SizeZ;
    }
}
