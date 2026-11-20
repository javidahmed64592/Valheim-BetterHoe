using BetterHoe.UI;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Runs when the piece menu opens or switches tab, after vanilla has shown or hidden the tab row.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "UpdateTagButtons")]
internal static class BuildUi_UpdateTagButtons_Patch
{
    [HarmonyPostfix]
    private static void Postfix(BuildUi __instance)
    {
        PieceMenuTidy.AfterUpdateTagButtons(__instance);
    }
}
