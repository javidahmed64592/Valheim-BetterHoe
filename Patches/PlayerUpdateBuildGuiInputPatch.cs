using BetterHoe.Core;
using BetterHoe.Terrain;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Ctrl+Right-Click toggles the grid, so it mustn't also open the build menu.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdateBuildGuiInput")]
internal static class Player_UpdateBuildGuiInput_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(Player __instance)
    {
        return !Keys.Ctrl || !AreaTool.HoeTerrainToolSelected(__instance);
    }
}
