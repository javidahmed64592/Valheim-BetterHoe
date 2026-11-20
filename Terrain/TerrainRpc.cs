using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// The RPC that applies <see cref="TerrainEdits"/> on the owner of a terrain compiler. Vanilla terrain edits
/// only carry a TerrainOp prefab and a position, so they are limited to the vanilla brush shapes; this one
/// carries the change for each vertex. The owner writes the same per-vertex deltas vanilla's LevelTerrain and
/// PaintCleared write, then saves and refreshes the terrain the same way, so the result is ordinary terrain
/// data that vanilla loads, syncs and saves.
/// </summary>
internal static class TerrainRpc
{
    public const int Version = 1;

    private const string EditRpc = "BetterHoe_Edit";

    /// <summary>
    /// Vanilla keeps every vertex within 8 m of its generated height (TerrainComp.LevelTerrain).
    /// </summary>
    public const float MaxLevelDelta = 8f;

    /// <summary>
    /// Called from TerrainComp.Awake on every client, after vanilla registers its own RPC.
    /// </summary>
    public static void Register(TerrainComp compiler)
    {
        if (compiler.m_nview == null || !compiler.m_initialized)
        {
            return;
        }

        compiler.m_nview.Register<ZPackage>(EditRpc, (_, package) => OnEdit(compiler, package));
    }

    public static bool Send(TerrainComp compiler, ZPackage package)
    {
        if (compiler.m_nview == null || !compiler.m_nview.IsValid())
        {
            return false;
        }

        compiler.m_nview.InvokeRPC(EditRpc, package);
        return true;
    }

    private static void OnEdit(TerrainComp compiler, ZPackage package)
    {
        if (compiler == null || !compiler.m_initialized || !compiler.m_nview.IsValid() || !compiler.m_nview.IsOwner())
        {
            return;
        }

        int version = package.ReadInt();
        if (version != Version)
        {
            Jotunn.Logger.LogWarning($"Ignoring a terrain edit with version {version}; this client uses version {Version}.");
            return;
        }

        Vector3 center = package.ReadVector3();
        float radius = package.ReadSingle();
        float baseY = compiler.transform.position.y;
        int pitch = compiler.m_pitch;
        Heightmap map = compiler.m_hmap;

        int heightCount = package.ReadInt();
        for (int i = 0; i < heightCount; i++)
        {
            int index = package.ReadInt();
            HeightMode mode = (HeightMode)package.ReadByte();
            float value = package.ReadSingle();
            float weight = package.ReadSingle();
            if (index < 0 || index >= compiler.m_modifiedHeight.Length)
            {
                continue;
            }

            if (mode == HeightMode.Reset)
            {
                compiler.m_levelDelta[index] = 0f;
                compiler.m_smoothDelta[index] = 0f;
                compiler.m_modifiedHeight[index] = false;
                continue;
            }

            // Same bookkeeping as TerrainComp.LevelTerrain: the current height already includes the smooth
            // delta, so fold it into the level delta and clear it.
            float height = map.GetHeight(index % pitch, index / pitch);
            float target = mode == HeightMode.Add
                ? height + value
                : Mathf.Lerp(height, value - baseY, Mathf.Clamp01(weight));
            float delta = target - height + compiler.m_smoothDelta[index];
            compiler.m_smoothDelta[index] = 0f;
            compiler.m_levelDelta[index] = Mathf.Clamp(compiler.m_levelDelta[index] + delta, -MaxLevelDelta, MaxLevelDelta);
            compiler.m_modifiedHeight[index] = true;
        }

        int paintCount = package.ReadInt();
        for (int i = 0; i < paintCount; i++)
        {
            int index = package.ReadInt();
            float r = package.ReadSingle();
            float g = package.ReadSingle();
            float b = package.ReadSingle();
            if (index < 0 || index >= compiler.m_paintMask.Length)
            {
                continue;
            }

            // The alpha channel isn't part of the paint (vanilla PaintCleared keeps it too).
            Color color = compiler.m_paintMask[index];
            color.r = r;
            color.g = g;
            color.b = b;
            compiler.m_paintMask[index] = color;
            compiler.m_modifiedPaint[index] = true;
        }

        // Same as TerrainComp.DoOperation. Bumping the operation count by one makes the other clients regrow the
        // grass only around the last operation when they load the new data.
        compiler.m_operations++;
        compiler.m_lastOpPoint = center;
        compiler.m_lastOpRadius = radius;

        bool paintOnly = heightCount == 0;
        compiler.Save(paintOnly);
        map.Poke(1, paintOnly);
        if (ClutterSystem.instance != null)
        {
            ClutterSystem.instance.ResetGrass(center, radius);
        }
    }
}
