using BetterHoe.Core;
using BetterHoe.UI;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Runs when the piece menu opens or switches tab, after vanilla has shown or hidden the tab row.
/// </summary>
[HarmonyPatch(typeof(BuildUi), "UpdateTagButtons")]
[Feature(Feature)]
internal static class BuildUi_UpdateTagButtons_Patch
{
    /// <summary>Without it, the hoe menu has no "Terrain Tools" title and keeps the line and Q / E hint for tabs it doesn't have, as in vanilla.</summary>
    public const string Feature = "Hoe menu title";

    [HarmonyPostfix]
    private static void Postfix(BuildUi __instance)
    {
        PieceMenuTidy.AfterUpdateTagButtons(__instance);
    }
}
