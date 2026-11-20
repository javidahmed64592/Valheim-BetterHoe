using BetterHoe.Equip;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Lets EquipItem accept the phantom hoe although it isn't in the inventory.
/// </summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsItem))]
internal static class Inventory_ContainsItem_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemDrop.ItemData item, ref bool __result)
    {
        if (PhantomHoe.Item != null && item == PhantomHoe.Item)
        {
            __result = true;
            return false;
        }

        return true;
    }
}
