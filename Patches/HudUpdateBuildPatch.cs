using BetterHoe.UI;

using HarmonyLib;

namespace BetterHoe.Patches;

[HarmonyPatch(typeof(Hud), "UpdateBuild")]
internal static class Hud_UpdateBuild_Patch
{
    [HarmonyPostfix]
    private static void Postfix(Hud __instance, Player player)
    {
        PieceMenuTidy.AfterUpdateBuild(__instance, player);
    }
}
