using System.Collections.Generic;

using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

using UnityEngine;

namespace BetterHoe.Pieces;

/// <summary>
/// Adds the hoe's new tools (Ramp, Lower ground, Reset height, Cultivate and Reset paint) and sets the costs: shaping the ground is
/// free, painting it costs what vanilla charges, and nothing needs a crafting station. The new tools are copies
/// of vanilla pieces (for the ghost, sounds and dust) that the area tool handles itself, so their vanilla terrain
/// operation never runs.
/// </summary>
internal static class HoePieces
{
    public const string LowerName = "BetterHoe_Lower";
    public const string RampName = "BetterHoe_Ramp";
    public const string ResetHeightName = "BetterHoe_ResetHeight";
    public const string ResetPaintName = "BetterHoe_ResetPaint";
    public const string CultivateName = "BetterHoe_Cultivate";

    private const string HoeTable = "_HoePieceTable";
    private const string LevelPrefab = "mud_road_v2";
    private const string RaisePrefab = "raise_v2";
    private const string PathPrefab = "path_v2";
    private const string PavedPrefab = "paved_road_v2";
    private const string ReplantPrefab = "replant_v2";
    private const string CultivatePrefab = "cultivate_v2";

    public static void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
        PieceManager.OnPiecesRegistered += OrderPieces;
    }

    private static void AddPieces()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;

        // Vanilla Raise ground costs stone, and Paved road needs a stonecutter. Shaping the ground is free here,
        // and painting keeps its materials but no tool needs a station. Done before copying, so copies match.
        foreach (string name in new[] { LevelPrefab, RaisePrefab, PathPrefab, PavedPrefab })
        {
            Piece vanilla = PrefabManager.Instance.GetPrefab(name)?.GetComponent<Piece>();
            if (vanilla == null)
            {
                continue;
            }

            vanilla.m_craftingStation = null;
            if (name is LevelPrefab or RaisePrefab)
            {
                vanilla.m_resources = [];
            }
        }

        Sprite raiseIcon = PrefabManager.Instance.GetPrefab(RaisePrefab)?.GetComponent<Piece>()?.m_icon;
        AddPiece(RampName, LevelPrefab, "$betterhoe_piece_ramp", "$betterhoe_piece_ramp_description",
            raiseIcon != null ? IconMaker.Ramp(raiseIcon) : null);
        AddPiece(LowerName, RaisePrefab, "$betterhoe_piece_lower", "$betterhoe_piece_lower_description",
            raiseIcon != null ? IconMaker.FlipVertically(raiseIcon) : null);
        AddPiece(ResetHeightName, LevelPrefab, "$betterhoe_piece_reset_height", "$betterhoe_piece_reset_height_description",
            raiseIcon != null ? IconMaker.ResetArrow(raiseIcon) : null);
        AddPiece(CultivateName, CultivatePrefab, "$betterhoe_piece_cultivate", "$betterhoe_piece_cultivate_description", null);
        AddPiece(ResetPaintName, ReplantPrefab, "$betterhoe_piece_reset_paint", "$betterhoe_piece_reset_paint_description", null);
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
        piece.Piece.m_craftingStation = null;
        piece.Piece.m_groundPiece = true;
        piece.Piece.m_cultivatedGroundOnly = false;
        _ = PieceManager.Instance.AddPiece(piece);
    }

    /// <summary>
    /// Puts Ramp after Level ground, Lower ground and Reset height after Raise ground, and Cultivate and Reset paint
    /// after Paved road, instead of after the stones.
    /// </summary>
    private static void OrderPieces()
    {
        PieceTable table = PieceManager.Instance.GetPieceTable(HoeTable);
        if (table == null)
        {
            return;
        }

        MoveAfter(table.m_pieces, RampName, LevelPrefab);
        MoveAfter(table.m_pieces, LowerName, RaisePrefab);
        MoveAfter(table.m_pieces, ResetHeightName, LowerName);
        MoveAfter(table.m_pieces, CultivateName, PavedPrefab);
        MoveAfter(table.m_pieces, ResetPaintName, CultivateName);
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
