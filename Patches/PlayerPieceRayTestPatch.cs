using BetterHoe.Terrain;

using HarmonyLib;

using UnityEngine;

namespace BetterHoe.Patches;

/// <summary>
/// Records where the local player aims while an area tool is active; the area is centred on it.
/// </summary>
[HarmonyPatch(typeof(Player), "PieceRayTest")]
internal static class Player_PieceRayTest_Patch
{
    [HarmonyPostfix]
    private static void Postfix(Player __instance, bool __result, ref Vector3 point)
    {
        if (__result && AreaTool.ActiveTool(__instance) != null)
        {
            AreaTool.SetAim(point);
        }
    }
}
