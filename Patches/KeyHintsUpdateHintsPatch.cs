using BetterHoe.Core;
using BetterHoe.UI;

using HarmonyLib;

namespace BetterHoe.Patches;

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
[Feature(Feature)]
internal static class KeyHints_UpdateHints_Patch
{
    /// <summary>Without it, the game's own hints are shown while a hoe tool is selected.</summary>
    public const string Feature = "Key hints";

    [HarmonyPostfix]
    private static void Postfix(KeyHints __instance)
    {
        HoeKeyHints.AfterUpdateHints(__instance);
    }
}
