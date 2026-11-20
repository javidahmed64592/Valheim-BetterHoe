using System;
using System.Collections.Generic;

using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// A straight ramp between two points with a constant slope. The core is `Width` metres wide and set exactly to
/// the sloping surface; on each side a band `Blend` metres wide eases the ground into the slope. Both ends are on
/// heightmap vertices; the heights are free.
/// </summary>
internal readonly struct RampShape(Vector3 start, Vector3 end, float width, float blend) : IEquatable<RampShape>
{
    public readonly Vector3 Start = start;
    public readonly Vector3 End = end;
    public readonly float Width = width;
    public readonly float Blend = blend;

    /// <summary>
    /// Horizontal length from start to end.
    /// </summary>
    public float Length => new Vector2(End.x - Start.x, End.z - Start.z).magnitude;

    public float Rise => End.y - Start.y;

    /// <summary>
    /// The slope in degrees, positive going up from start to end.
    /// </summary>
    public float SlopeDegrees => Length > 0f ? Mathf.Atan2(Rise, Length) * Mathf.Rad2Deg : 0f;

    public bool IsValid => Length >= 1f;

    public Vector3 Center => (Start + End) / 2f;

    /// <summary>
    /// A circle of this radius around the centre covers the ramp and its blend bands.
    /// </summary>
    public float Radius => (Length / 2f) + (Width / 2f) + Blend + 1f;

    /// <summary>
    /// Unit vector from start to end along the ground, and the one across it (pointing left).
    /// </summary>
    public Vector3 Along
    {
        get
        {
            Vector3 along = End - Start;
            along.y = 0f;
            return along.normalized;
        }
    }

    public Vector3 Across => new(-Along.z, 0f, Along.x);

    /// <summary>
    /// The ramp surface height `t` of the way along (0 = start, 1 = end).
    /// </summary>
    public float HeightAt(float t)
    {
        return Mathf.Lerp(Start.y, End.y, t);
    }

    /// <summary>
    /// Where (x, z) projects onto the ramp: `t` along it and the distance from its centre line.
    /// </summary>
    public void Project(float x, float z, out float t, out float distance)
    {
        Vector3 offset = new(x - Start.x, 0f, z - Start.z);
        float length = Length;
        t = length > 0f ? Vector3.Dot(offset, Along) / length : 0f;
        distance = Mathf.Abs(Vector3.Dot(offset, Across));
    }

    /// <summary>
    /// How strongly the ramp sets the vertex at (x, z): 1 in the core, easing to 0 across the blend band, and
    /// 0 beyond it or past either end.
    /// </summary>
    public float WeightAt(float x, float z, out float height)
    {
        Project(x, z, out float t, out float distance);
        height = HeightAt(Mathf.Clamp01(t));
        if (t is < (-0.001f) or > 1.001f)
        {
            return 0f;
        }

        float half = Width / 2f;
        if (distance <= half + 0.001f)
        {
            return 1f;
        }

        if (Blend <= 0f || distance >= half + Blend)
        {
            return 0f;
        }

        // Smoothstep, so the blend meets both the ramp and the ground without a crease.
        float u = 1f - ((distance - half) / Blend);
        return u * u * (3f - (2f * u));
    }

    /// <summary>
    /// Every whole-metre vertex the ramp changes, with its target height and weight.
    /// </summary>
    public void Vertices(List<(int x, int z, float height, float weight)> result)
    {
        result.Clear();
        float reach = (Width / 2f) + Blend + 1f;
        int minX = Mathf.FloorToInt(Mathf.Min(Start.x, End.x) - reach);
        int maxX = Mathf.CeilToInt(Mathf.Max(Start.x, End.x) + reach);
        int minZ = Mathf.FloorToInt(Mathf.Min(Start.z, End.z) - reach);
        int maxZ = Mathf.CeilToInt(Mathf.Max(Start.z, End.z) + reach);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                float weight = WeightAt(x, z, out float height);
                if (weight > 0.001f)
                {
                    result.Add((x, z, height, weight));
                }
            }
        }
    }

    /// <summary>
    /// The corners of the core (start left, start right, end right, end left) at the ramp's heights, widened by
    /// `extra` on each side.
    /// </summary>
    public Vector3[] Corners(float extra)
    {
        Vector3 side = Across * ((Width / 2f) + extra);
        return
        [
            Start + side,
            Start - side,
            End - side,
            End + side,
        ];
    }

    public bool Equals(RampShape other)
    {
        return Start == other.Start && End == other.End && Width == other.Width && Blend == other.Blend;
    }

    public override bool Equals(object obj)
    {
        return obj is RampShape other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (Start.GetHashCode() * 397) ^ End.GetHashCode() ^ Width.GetHashCode() ^ Blend.GetHashCode();
    }
}
