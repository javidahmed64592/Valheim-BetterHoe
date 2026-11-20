using System.Reflection;

using BetterHoe.Core;
using BetterHoe.Terrain;

using HarmonyLib;

namespace BetterHoe.Patches;

/// <summary>
/// Ctrl, Shift and Alt + scroll size the area, so stop vanilla from also rotating the piece.
/// </summary>
[HarmonyPatch]
internal static class ZInput_GetMouseScrollWheel_Patch
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method(typeof(ZInput), "GetMouseScrollWheel");
    }

    private static bool Prepare()
    {
        return TargetMethod() != null;
    }

    [HarmonyPostfix]
    private static void Postfix(ref float __result)
    {
        if (AreaTool.Active && Keys.AnyModifier)
        {
            __result = 0f;
        }
    }
}
