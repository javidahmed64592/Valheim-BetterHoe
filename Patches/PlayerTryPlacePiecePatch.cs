using BetterHoe.Terrain;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Applies the area tool instead of placing the hoe piece. Returning true keeps vanilla's costs: the caller
/// charges stamina, durability, materials and skill when TryPlacePiece succeeds.
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
internal static class Player_TryPlacePiece_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(Player __instance, Piece piece, ref bool __result)
    {
        if (!AreaTool.TryPlace(__instance, piece, out bool placed))
        {
            return true;
        }

        __result = placed;
        return false;
    }
}
