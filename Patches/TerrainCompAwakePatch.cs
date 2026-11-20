using BetterHoe.Terrain;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Registers the area edit RPC on every terrain compiler.
/// </summary>
[HarmonyPatch(typeof(TerrainComp), "Awake")]
internal static class TerrainComp_Awake_Patch
{
    [HarmonyPostfix]
    private static void Postfix(TerrainComp __instance)
    {
        TerrainRpc.Register(__instance);
    }
}
