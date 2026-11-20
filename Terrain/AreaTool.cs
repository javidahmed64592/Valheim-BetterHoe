using System;
using System.Globalization;

using BepInEx.Configuration;

using BetterHoe.Core;
using BetterHoe.Grid;
using BetterHoe.Pieces;
using BetterHoe.UI;

using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// The hoe's area tools. While a terrain piece is selected on the hoe, the aim point picks a rectangle of
/// terrain vertices sized by Width x Length, and placing the piece flattens, raises, lowers or paints exactly
/// that rectangle instead of vanilla's round brush. Vanilla still charges the stamina, durability and
/// materials of one placement per click.
/// </summary>
internal static class AreaTool
{
    private const string HoeTableName = "_HoePieceTable";

    private static readonly float[] Steps = [0.1f, 0.25f, 0.5f, 1f, 2f];

    private static ConfigEntry<bool> _gridEnabled;
    private static ConfigEntry<int> _width;
    private static ConfigEntry<int> _length;
    private static ConfigEntry<float> _step;
    private static ConfigEntry<int> _maxSize;

    private static readonly GridOverlay Overlay = new();
    private static readonly Readout Readout = new();

    /// <summary>
    /// Latest aim point from Player.PieceRayTest while an area tool is active.
    /// </summary>
    private static int _aimFrame = -10;
    private static Vector3 _aimPoint;

    private static float? _lockedHeight;

    /// <summary>
    /// Cached count of vertices that would hit vanilla's 8 m limit, for the readout.
    /// </summary>
    private static (Area area, HoeTool tool, float value) _limitKey;
    private static int _limitCount;
    private static int _limitFrame = -100;

    /// <summary>
    /// Frames between limit recounts while nothing changes, so edits made since are picked up.
    /// </summary>
    private const int LimitRefreshFrames = 15;

    /// <summary>
    /// True while an area tool was active last frame; stops vanilla rotating the piece on modifier+scroll.
    /// </summary>
    internal static bool Active { get; private set; }

    private static int MaxSize => _maxSize.Value;
    private static int Width => Mathf.Clamp(_width.Value, 1, MaxSize);
    private static int Length => Mathf.Clamp(_length.Value, 1, MaxSize);
    private static float Step => _step.Value;

    public static void BindConfig(ConfigFile config)
    {
        _gridEnabled = config.Bind("Area", "Use With Vanilla Tools", true,
            "Use the area grid with the hoe's vanilla tools (Level ground, Raise ground, Pathen and Paved road). " +
            "Toggle it in game with Ctrl+Right-Click. BetterHoe's own tools always use it.");
        _width = config.Bind("Area", "Width", 4,
            new ConfigDescription("Width in metres of the area from west to east. Change it in game with Ctrl+Scroll.",
                new AcceptableValueRange<int>(1, 64)));
        _length = config.Bind("Area", "Length", 4,
            new ConfigDescription("Length in metres of the area from south to north. Change it in game with Shift+Scroll.",
                new AcceptableValueRange<int>(1, 64)));
        _step = config.Bind("Area", "Height Step", 0.5f,
            new ConfigDescription("Metres that Raise ground and Lower ground move the area per use (0.1, 0.25, 0.5, 1 or 2). " +
                "Change it in game with Alt+Scroll.",
                new AcceptableValueRange<float>(Steps[0], Steps[Steps.Length - 1])));
        _maxSize = config.Bind("Server", "Max Area Size", 16,
            new ConfigDescription("Largest width or length in metres of the area. (Synced with server)",
                new AcceptableValueRange<int>(1, 64),
                new ConfigurationManagerAttributes { IsAdminOnly = true }));

        if (Array.IndexOf(Steps, _step.Value) < 0)
        {
            _step.Value = 0.5f;
        }
    }

    public static void Destroy()
    {
        Overlay.Destroy();
        Readout.Destroy();
    }

    // ----- Tool selection -----

    /// <summary>
    /// The selected piece when the local player holds a hoe in build mode.
    /// </summary>
    private static Piece SelectedHoePiece(Player player)
    {
        if (player == null || !player.InPlaceMode() || player.m_buildPieces == null)
        {
            return null;
        }

        PieceTable table = player.GetRightItem()?.m_shared?.m_buildPieces;
        return table == null || table != player.m_buildPieces
            || (table.name != HoeTableName && table.name.IndexOf("hoe", StringComparison.OrdinalIgnoreCase) < 0)
            ? null
            : table.GetSelectedPiece();
    }

    /// <summary>
    /// The area tool for a hoe piece, or null for pieces that aren't terrain tools (e.g. the placeable stones).
    /// `own` is true for BetterHoe's pieces, which always use the area.
    /// </summary>
    private static HoeTool ToolFor(Piece piece, out bool own)
    {
        own = false;
        if (piece == null)
        {
            return null;
        }

        switch (Utils.GetPrefabName(piece.gameObject))
        {
            case "mud_road_v2":
                return HoeTool.Flatten;
            case "raise_v2":
                return HoeTool.Raise;
            case "path_v2":
                return HoeTool.PaintDirt;
            case "paved_road_v2":
                return HoeTool.PaintPaved;
            case HoePieces.LowerName:
                own = true;
                return HoeTool.Lower;
            case HoePieces.ResetHeightName:
                own = true;
                return HoeTool.ResetHeight;
            case HoePieces.ResetPaintName:
                own = true;
                return HoeTool.PaintReset;
            default:
                return null;
        }
    }

    /// <summary>
    /// The active area tool for the local player, or null when vanilla should handle the hoe.
    /// </summary>
    internal static HoeTool ActiveTool(Player player)
    {
        if (player == null || player != Player.m_localPlayer)
        {
            return null;
        }

        HoeTool tool = ToolFor(SelectedHoePiece(player), out bool own);
        return tool != null && (own || _gridEnabled.Value) ? tool : null;
    }

    /// <summary>
    /// True when the local player has one of the hoe's terrain tools selected, whether or not the grid is on.
    /// </summary>
    internal static bool HoeTerrainToolSelected(Player player)
    {
        return player != null && player == Player.m_localPlayer && ToolFor(SelectedHoePiece(player), out _) != null;
    }

    /// <summary>
    /// The piece the local player has selected on the hoe, or null when not using a hoe.
    /// </summary>
    internal static Piece SelectedPiece(Player player)
    {
        return player != null && player == Player.m_localPlayer ? SelectedHoePiece(player) : null;
    }

    /// <summary>
    /// The terrain tool for the selected hoe piece, whether or not the grid is on, or null.
    /// </summary>
    internal static HoeTool SelectedTerrainTool(Player player)
    {
        return ToolFor(SelectedPiece(player), out _);
    }

    /// <summary>
    /// True when the selected piece is one of the vanilla terrain tools, which Ctrl+Right-Click switches between
    /// the area and vanilla's brush.
    /// </summary>
    internal static bool GridToggleApplies(Player player)
    {
        return ToolFor(SelectedPiece(player), out bool own) != null && !own;
    }

    internal static bool HeightLocked => _lockedHeight.HasValue;

    internal static void SetAim(Vector3 point)
    {
        _aimFrame = Time.frameCount;
        _aimPoint = point;
    }

    private static bool HasAim => Time.frameCount - _aimFrame <= 2;

    // ----- Per frame -----

    public static void Update(Player player)
    {
        Active = false;
        if (player == null)
        {
            _lockedHeight = null;
            Hide();
            return;
        }

        Piece piece = SelectedHoePiece(player);
        if (piece == null || InputBlocked())
        {
            Hide();
            return;
        }

        HoeTool tool = ToolFor(piece, out bool own);
        if (tool != null && !own && Keys.Ctrl && Input.GetMouseButtonDown(1))
        {
            _gridEnabled.Value = !_gridEnabled.Value;
            player.Message(MessageHud.MessageType.TopLeft, _gridEnabled.Value ? "$betterhoe_grid_on" : "$betterhoe_grid_off");
        }

        if (tool == null || (!own && !_gridEnabled.Value))
        {
            Hide();
            return;
        }

        Active = true;
        HandleInput(player, tool);

        if (!HasAim)
        {
            Hide();
            return;
        }

        Area area = CurrentArea();
        float value = ToolValue(tool);
        Overlay.Show(_aimPoint, area, tool, value);
        Readout.Show(ReadoutText(tool, area, value));
    }

    private static void Hide()
    {
        Overlay.Hide();
        Readout.Hide();
    }

    private static bool InputBlocked()
    {
        return Menu.IsVisible()
            || InventoryGui.IsVisible()
            || Hud.IsPieceSelectionVisible()
            || (Chat.instance != null && Chat.instance.HasFocus())
            || Console.IsVisible()
            || TextInput.IsVisible()
            || (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large);
    }

    private static void HandleInput(Player player, HoeTool tool)
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f)
        {
            int direction = scroll > 0f ? 1 : -1;
            if (Keys.Ctrl)
            {
                _width.Value = Mathf.Clamp(Width + direction, 1, MaxSize);
            }
            else if (Keys.Shift)
            {
                _length.Value = Mathf.Clamp(Length + direction, 1, MaxSize);
            }
            else if (Keys.Alt && tool.Shape is ShapeMode.Raise or ShapeMode.Lower)
            {
                int index = Mathf.Clamp(Array.IndexOf(Steps, _step.Value) + direction, 0, Steps.Length - 1);
                _step.Value = Steps[index];
            }
        }

        if (tool.Shape == ShapeMode.Flatten && Input.GetMouseButtonDown(2) && !Keys.AnyModifier && HasAim)
        {
            if (_lockedHeight.HasValue)
            {
                _lockedHeight = null;
                player.Message(MessageHud.MessageType.TopLeft, "$betterhoe_height_unlocked");
            }
            else
            {
                _lockedHeight = _aimPoint.y;
                player.Message(MessageHud.MessageType.TopLeft,
                    Localization.instance.Localize("$betterhoe_height_locked", Metres(_aimPoint.y)));
            }
        }
    }

    /// <summary>
    /// The area around the aim point: width runs west to east (world x) and length south to north (world z),
    /// whichever way the camera faces.
    /// </summary>
    private static Area CurrentArea()
    {
        return Area.Around(_aimPoint, Width, Length);
    }

    /// <summary>
    /// Flatten: the target height. Raise / lower: the signed step. Painting: unused.
    /// </summary>
    private static float ToolValue(HoeTool tool)
    {
        return tool.Shape switch
        {
            ShapeMode.Flatten => _lockedHeight ?? _aimPoint.y,
            ShapeMode.Raise => Step,
            ShapeMode.Lower => -Step,
            _ => 0f,
        };
    }

    // ----- Placing -----

    /// <summary>
    /// Called instead of Player.TryPlacePiece. Returns false when the piece isn't an active area tool, so vanilla
    /// places it as usual. Otherwise `placed` says whether the terrain was changed, which makes vanilla charge
    /// the usual costs.
    /// </summary>
    internal static bool TryPlace(Player player, Piece piece, out bool placed)
    {
        placed = false;
        HoeTool tool = ActiveTool(player);
        if (tool == null || SelectedHoePiece(player) != piece)
        {
            return false;
        }

        if (!HasAim)
        {
            player.Message(MessageHud.MessageType.Center, "$msg_invalidplacement");
            return true;
        }

        Area area = CurrentArea();
        if (!CheckAccess(player, area))
        {
            return true;
        }

        float value = ToolValue(tool);
        TerrainEdits edits = BuildEdits(tool, area, value);
        Vector3 center = new(area.CenterX, _aimPoint.y, area.CenterZ);
        if (!edits.Send(center, area.Radius + 1f))
        {
            player.Message(MessageHud.MessageType.Center, "$msg_invalidplacement");
            return true;
        }

        PlayEffects(player, piece, center);
        placed = true;
        return true;
    }

    private static TerrainEdits BuildEdits(HoeTool tool, Area area, float value)
    {
        TerrainEdits edits = new();

        if (tool.Shape != ShapeMode.None)
        {
            for (int x = area.MinX; x <= area.MaxX; x++)
            {
                for (int z = area.MinZ; z <= area.MaxZ; z++)
                {
                    switch (tool.Shape)
                    {
                        case ShapeMode.Flatten:
                            edits.SetHeight(x, z, value);
                            break;
                        case ShapeMode.Reset:
                            edits.ResetHeight(x, z);
                            break;
                        default:
                            edits.AddHeight(x, z, value);
                            break;
                    }
                }
            }
        }

        if (tool.Paint == null)
        {
            return edits;
        }

        Color paint = tool.PaintColor;
        for (int x = area.MinX; x < area.MaxX; x++)
        {
            for (int z = area.MinZ; z < area.MaxZ; z++)
            {
                edits.Paint(x + 0.5f, z + 0.5f, paint);
            }
        }

        return edits;
    }

    /// <summary>
    /// Vanilla only checks the ghost's position; the area can reach much further, so every vertex is checked
    /// against wards and no-build locations.
    /// </summary>
    private static bool CheckAccess(Player player, Area area)
    {
        bool onTerrain = false;
        for (int x = area.MinX; x <= area.MaxX; x++)
        {
            for (int z = area.MinZ; z <= area.MaxZ; z++)
            {
                if (!TerrainHeights.TryGet(x, z, out float height, out _))
                {
                    continue;
                }

                onTerrain = true;
                Vector3 point = new(x, height, z);
                if (Location.IsInsideNoBuildLocation(point))
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_nobuildzone");
                    return false;
                }

                if (!PrivateArea.CheckAccess(point, 0f, flash: false))
                {
                    _ = PrivateArea.CheckAccess(point, 0f, flash: true);
                    player.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                    return false;
                }
            }
        }

        if (!onTerrain)
        {
            player.Message(MessageHud.MessageType.Center, "$msg_invalidplacement");
        }

        return onTerrain;
    }

    /// <summary>
    /// What Player.PlacePiece and TerrainOp do for a placed terrain piece, minus the piece itself.
    /// </summary>
    private static void PlayEffects(Player player, Piece piece, Vector3 center)
    {
        player.FaceLookDirection();
        ItemDrop.ItemData item = player.GetRightItem();
        if (item != null)
        {
            player.m_zanim.SetTrigger(item.m_shared.m_attack.m_attackAnimation);
        }

        _ = piece.m_placeEffect.Create(center, Quaternion.identity);
        TerrainOp op = piece.GetComponent<TerrainOp>();
        _ = (op?.m_onPlacedEffect.Create(center, Quaternion.identity));
    }

    // ----- Readout -----

    private static string ReadoutText(HoeTool tool, Area area, float value)
    {
        string text = Localization.instance.Localize("$betterhoe_readout_area",
            Localization.instance.Localize(tool.NameToken), area.SizeX.ToString(CultureInfo.InvariantCulture), area.SizeZ.ToString(CultureInfo.InvariantCulture));

        switch (tool.Shape)
        {
            case ShapeMode.Flatten:
                text += "\n" + Localization.instance.Localize(
                    _lockedHeight.HasValue ? "$betterhoe_readout_height_locked" : "$betterhoe_readout_height", Metres(value));
                break;
            case ShapeMode.Raise:
            case ShapeMode.Lower:
                text += "\n" + Localization.instance.Localize("$betterhoe_readout_step",
                    (value >= 0f ? "+" : "") + Metres(value));
                break;
        }

        if ((tool.Shape is ShapeMode.Flatten or ShapeMode.Raise or ShapeMode.Lower) && CountAtLimit(tool, area, value) > 0)
        {
            text += "\n<color=#ff8060>" + Localization.instance.Localize("$betterhoe_readout_limit") + "</color>";
        }

        return text;
    }

    /// <summary>
    /// How many vertices of the area the edit would push past vanilla's 8 m limit, where they stop.
    /// </summary>
    private static int CountAtLimit(HoeTool tool, Area area, float value)
    {
        (Area, HoeTool, float) key = (area, tool, value);
        if (key.Equals(_limitKey) && Time.frameCount - _limitFrame < LimitRefreshFrames)
        {
            return _limitCount;
        }

        int count = 0;
        for (int x = area.MinX; x <= area.MaxX; x++)
        {
            for (int z = area.MinZ; z <= area.MaxZ; z++)
            {
                if (!TerrainHeights.TryGet(x, z, out float height, out float natural))
                {
                    continue;
                }

                float target = tool.Shape == ShapeMode.Flatten ? value : height + value;
                if (Mathf.Abs(target - natural) > TerrainRpc.MaxLevelDelta + 0.01f)
                {
                    count++;
                }
            }
        }

        _limitKey = key;
        _limitCount = count;
        _limitFrame = Time.frameCount;
        return count;
    }

    internal static string Metres(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
