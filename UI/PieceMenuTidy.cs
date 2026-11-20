using UnityEngine;

namespace BetterHoe.UI;

/// <summary>
/// The piece menu shows the category bar and the Q / E keys for switching tabs even when there is only one tab,
/// as with the hoe (Jotunn gives every piece table the categories its pieces use, so the hoe gets just "Misc").
/// While a menu with fewer than two tabs is open, this hides both. Vanilla turns the category bar back on every
/// frame, so it is hidden every frame too; the Q / E keys are put back when a menu with tabs is opened.
/// </summary>
internal static class PieceMenuTidy
{
    private static GameObject _keys;
    private static bool _keysHidden;

    internal static void AfterUpdateBuild(Hud hud, Player player)
    {
        if (player == null || !Hud.IsPieceSelectionVisible() || hud.m_pieceSelectionWindow == null)
        {
            return;
        }

        if (_keys == null)
        {
            _keys = hud.m_pieceSelectionWindow.transform.Find("InputHelp")?.gameObject;
            _keysHidden = false;
        }

        PieceTable table = player.m_buildPieces;
        bool hide = table != null && table.m_categories.Count < 2;

        if (hide && hud.m_pieceCategoryRoot != null && hud.m_pieceCategoryRoot.activeSelf)
        {
            hud.m_pieceCategoryRoot.SetActive(false);
        }

        if (_keys != null && hide != _keysHidden)
        {
            _keys.SetActive(!hide);
            _keysHidden = hide;
        }
    }
}
