using BetterHoe.Equip;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// SetupVisEquipment throws for items that didn't come through the inventory, so the phantom hoe is hidden from
/// it and put in the player's hand directly afterwards (as BetterHammer does for its hammer).
/// </summary>
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
internal static class Humanoid_SetupVisEquipment_Patch
{
    [HarmonyPrefix]
    private static void Prefix(Humanoid __instance, out ItemDrop.ItemData __state)
    {
        ItemDrop.ItemData hoe = PhantomHoe.Item;
        __state = null;
        if (hoe != null && __instance.GetRightItem() == hoe)
        {
            __state = hoe;
            __instance.m_rightItem = null;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(Humanoid __instance, ItemDrop.ItemData __state)
    {
        if (__state == null)
        {
            return;
        }

        __instance.m_rightItem = __state;
        if (__instance is Player player)
        {
            player.m_visEquipment?.SetRightItem(__state.m_dropPrefab.name.GetStableHashCode(), __state.m_quality);
        }
    }
}
