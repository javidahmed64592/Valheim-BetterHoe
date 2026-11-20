using System.Collections.Generic;

using UnityEngine;

namespace BetterHoe.UI;

/// <summary>
/// The piece menu always shows the Q / E tab keys and the tab bar's border, even for a piece table without
/// tabs like the hoe's, where there is nothing to switch between. This hides them while such a table is open
/// and puts them back for the others.
/// </summary>
internal static class PieceMenuTidy
{
    private static readonly string[] Names = ["InputHelp", "TabBorder"];

    private static readonly List<GameObject> Objects = [];
    private static Hud _hud;
    private static bool _hidden;

    internal static void AfterUpdateBuild(Hud hud, Player player)
    {
        if (player == null || !Hud.IsPieceSelectionVisible())
        {
            return;
        }

        if (_hud != hud)
        {
            Find(hud);
        }

        PieceTable table = player.m_buildPieces;
        bool hide = table != null && table.m_categories.Count == 0;
        if (hide == _hidden)
        {
            return;
        }

        foreach (GameObject item in Objects)
        {
            if (item != null)
            {
                item.SetActive(!hide);
            }
        }

        _hidden = hide;
    }

    private static void Find(Hud hud)
    {
        _hud = hud;
        _hidden = false;
        Objects.Clear();
        foreach (Transform child in hud.m_buildHud.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (System.Array.IndexOf(Names, child.name) >= 0 && child.gameObject.activeSelf)
            {
                Objects.Add(child.gameObject);
            }
        }
    }
}
