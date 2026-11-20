using System.Collections.Generic;

using BetterHoe.Equip;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Includes the phantom hoe's piece table in inventory scans, so UpdateKnownRecipesList finds the hoe's pieces
/// without a real hoe.
/// </summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAllPieceTables))]
internal static class Inventory_GetAllPieceTables_Patch
{
    [HarmonyPostfix]
    private static void Postfix(List<PieceTable> tables)
    {
        PieceTable table = PhantomHoe.Item?.m_shared?.m_buildPieces;
        if (table != null && !tables.Contains(table))
        {
            tables.Add(table);
        }
    }
}
