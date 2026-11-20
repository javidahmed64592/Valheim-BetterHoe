using System.Collections.Generic;

using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

using UnityEngine;

namespace BetterHoe.Pieces;

/// <summary>
/// Adds the hoe's new tools: Lower ground and Reset ground. They are copies of vanilla pieces (for the ghost,
/// sounds and dust) that the area tool handles itself, so their vanilla terrain operation never runs.
/// </summary>
internal static class HoePieces
{
    public const string LowerName = "BetterHoe_Lower";
    public const string ResetName = "BetterHoe_Reset";

    private const string HoeTable = "_HoePieceTable";
    private const string RaisePrefab = "raise_v2";
    private const string PavedPrefab = "paved_road_v2";
    private const string ReplantPrefab = "replant_v2";

    public static void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
        PieceManager.OnPiecesRegistered += OrderPieces;
    }

    private static void AddPieces()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;

        Sprite raiseIcon = PrefabManager.Instance.GetPrefab(RaisePrefab)?.GetComponent<Piece>()?.m_icon;
        AddPiece(LowerName, RaisePrefab, "$betterhoe_piece_lower", "$betterhoe_piece_lower_description",
            raiseIcon != null ? IconMaker.FlipVertically(raiseIcon) : null);
        AddPiece(ResetName, ReplantPrefab, "$betterhoe_piece_reset", "$betterhoe_piece_reset_description", null);
    }

    private static void AddPiece(string name, string baseName, string displayName, string description, Sprite icon)
    {
        CustomPiece piece = new(name, baseName, new PieceConfig
        {
            Name = displayName,
            Description = description,
            PieceTable = HoeTable,
            Icon = icon,
        });

        // Free like the vanilla Level ground and Pathen tools, wherever there's terrain.
        piece.Piece.m_resources = [];
        piece.Piece.m_groundPiece = true;
        piece.Piece.m_cultivatedGroundOnly = false;
        _ = PieceManager.Instance.AddPiece(piece);
    }

    /// <summary>
    /// Puts Lower ground after Raise ground and Reset ground after Paved road, instead of after the stones.
    /// </summary>
    private static void OrderPieces()
    {
        PieceTable table = PieceManager.Instance.GetPieceTable(HoeTable);
        if (table == null)
        {
            return;
        }

        MoveAfter(table.m_pieces, LowerName, RaisePrefab);
        MoveAfter(table.m_pieces, ResetName, PavedPrefab);
    }

    private static void MoveAfter(List<GameObject> pieces, string name, string anchorName)
    {
        int index = pieces.FindIndex(p => p != null && p.name == name);
        if (index < 0)
        {
            return;
        }

        GameObject piece = pieces[index];
        pieces.RemoveAt(index);
        int anchor = pieces.FindIndex(p => p != null && p.name == anchorName);
        pieces.Insert(anchor < 0 ? pieces.Count : anchor + 1, piece);
    }
}
