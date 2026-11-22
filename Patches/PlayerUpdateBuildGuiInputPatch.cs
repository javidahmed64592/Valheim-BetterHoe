using BetterHoe.Core;
using BetterHoe.Terrain;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Ctrl+Right-Click toggles the grid, Alt+Right-Click unmarks ground and Right-Click drops a ramp's start, so they
/// mustn't also open the build menu.
/// </summary>
[HarmonyPatch(typeof(Player), "UpdateBuildGuiInput")]
internal static class Player_UpdateBuildGuiInput_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(Player __instance)
    {
        return (__instance != Player.m_localPlayer || !AreaTool.BlocksMenu) && (!(Keys.Ctrl || Keys.Alt) || !AreaTool.HoeTerrainToolSelected(__instance));
    }
}
