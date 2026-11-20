using UnityEngine;

namespace BetterHoe.UI;

/// <summary>
/// Tools whose piece table hides the advanced menu (the hoe, the cultivator) get no tabs: vanilla hides the tab
/// row, but leaves the line under it and the Q / E keys for switching tabs, which then do nothing. This hides
/// those two whenever the tabs are hidden, and shows them again for tools with tabs.
/// </summary>
internal static class PieceMenuTidy
{
    private static readonly string[] Names = ["TabBorder", "InputHelp"];

    internal static void AfterUpdateTagButtons(BuildUi buildUi)
    {
        Transform tabs = buildUi.m_tabContainer;
        PieceTable table = buildUi.m_currentBuildTool;
        if (tabs == null || tabs.parent == null || table == null)
        {
            return;
        }

        bool showTabs = !table.m_hideAdvancedMenu;
        foreach (string name in Names)
        {
            Transform item = tabs.parent.Find(name);
            if (item != null && item.gameObject.activeSelf != showTabs)
            {
                item.gameObject.SetActive(showTabs);
            }
        }
    }
}
