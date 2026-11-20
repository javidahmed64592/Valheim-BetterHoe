using BetterHoe.UI;

using HarmonyLib;

namespace BetterHoe.Patches;

[HarmonyPatch(typeof(KeyHints), "UpdateHints")]
internal static class KeyHints_UpdateHints_Patch
{
    [HarmonyPostfix]
    private static void Postfix(KeyHints __instance)
    {
        HoeKeyHints.AfterUpdateHints(__instance);
    }
}
