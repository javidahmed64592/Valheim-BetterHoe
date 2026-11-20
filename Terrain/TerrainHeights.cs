using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// Reads terrain vertex heights on this client.
/// </summary>
internal static class TerrainHeights
{
    /// <summary>
    /// The world height of the vertex at whole-metre (x, z), and its natural height: the height before any
    /// terrain edits, which vanilla never lets a vertex move more than 8 m away from.
    /// </summary>
    public static bool TryGet(int x, int z, out float height, out float natural)
    {
        Vector3 point = new(x, 0f, z);
        Heightmap map = Heightmap.FindHeightmap(point);
        if (map == null)
        {
            height = 0f;
            natural = 0f;
            return false;
        }

        map.WorldToVertex(point, out int vx, out int vy);
        height = map.GetHeight(vx, vy) + map.transform.position.y;
        natural = height;

        TerrainComp compiler = TerrainComp.FindTerrainCompiler(map.transform.position);
        if (compiler != null && compiler.m_initialized)
        {
            int index = (vy * compiler.m_pitch) + vx;
            if (index >= 0 && index < compiler.m_levelDelta.Length)
            {
                natural -= compiler.m_levelDelta[index] + compiler.m_smoothDelta[index];
            }
        }

        return true;
    }

    /// <summary>
    /// The world height of the vertex at whole-metre (x, z), or `fallback` off the terrain.
    /// </summary>
    public static float Get(int x, int z, float fallback)
    {
        return TryGet(x, z, out float height, out _) ? height : fallback;
    }
}
