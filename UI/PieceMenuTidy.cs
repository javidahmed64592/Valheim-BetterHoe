using TMPro;

using UnityEngine;

namespace BetterHoe.UI;

/// <summary>
/// Tools whose piece table hides the advanced menu (the hoe, the cultivator) get no tabs: vanilla hides the tab
/// row, but leaves the line under it and the Q / E keys for switching tabs, which then do nothing. This hides
/// the keys whenever the tabs are hidden. The hoe gets a "Terrain Tools" title where the tabs would be, with the
/// line as its underline; other tools without tabs lose the line too. Everything comes back for tools with tabs.
/// </summary>
internal static class PieceMenuTidy
{
    private const string HoeTableName = "_HoePieceTable";
    private const string TitleName = "BetterHoe_MenuTitle";

    /// <summary>
    /// Vanilla's orange for headings, as in the selected piece's name.
    /// </summary>
    private static readonly Color TitleColor = new(1f, 0.72f, 0.36f);

    private static TextMeshProUGUI _title;

    internal static void AfterUpdateTagButtons(BuildUi buildUi)
    {
        RectTransform tabs = buildUi.m_tabContainer;
        PieceTable table = buildUi.m_currentBuildTool;
        if (tabs == null || tabs.parent == null || table == null)
        {
            return;
        }

        bool showTabs = !table.m_hideAdvancedMenu;
        bool hoe = !showTabs && table.name == HoeTableName;

        SetActive(tabs.parent.Find("InputHelp"), showTabs);
        SetActive(tabs.parent.Find("TabBorder"), showTabs || hoe);

        if (hoe && _title == null)
        {
            _title = CreateTitle(tabs);
        }

        if (_title != null && _title.gameObject.activeSelf != hoe)
        {
            _title.gameObject.SetActive(hoe);
        }
    }

    private static void SetActive(Transform item, bool active)
    {
        if (item != null && item.gameObject.activeSelf != active)
        {
            item.gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// A label covering the tab row, in the tab labels' font.
    /// </summary>
    private static TextMeshProUGUI CreateTitle(RectTransform tabs)
    {
        TextMeshProUGUI template = tabs.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        if (template == null)
        {
            return null;
        }

        RectTransform rect = new GameObject(TitleName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(tabs.parent, false);
        rect.anchorMin = tabs.anchorMin;
        rect.anchorMax = tabs.anchorMax;
        rect.pivot = tabs.pivot;
        rect.anchoredPosition = tabs.anchoredPosition;
        rect.sizeDelta = tabs.sizeDelta;

        TextMeshProUGUI title = rect.gameObject.AddComponent<TextMeshProUGUI>();
        title.font = template.font;
        title.fontSharedMaterial = template.fontSharedMaterial;
        title.fontSize = template.fontSize * 1.25f;
        title.enableAutoSizing = false;
        title.color = TitleColor;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.raycastTarget = false;
        title.text = Localization.instance.Localize("$betterhoe_menu_title");
        return title;
    }
}
