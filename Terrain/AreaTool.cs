using System;
using System.Collections.Generic;
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
/// that rectangle instead of vanilla's round brush. The ramp tool instead takes two clicks, a start and an end,
/// and builds a constant slope between them, Width metres wide. Vanilla still charges the stamina, durability
/// and materials of one placement per click (not for setting a ramp's start).
/// </summary>
internal static class AreaTool
{
    private const string HoeTableName = "_HoePieceTable";

    private static readonly float[] Steps = [0.1f, 0.25f, 0.5f, 1f, 2f];
    private static readonly float[] Blends = [0f, 1f, 2f, 3f, 4f, 6f];

    private static ConfigEntry<bool> _gridEnabled;
    private static ConfigEntry<int> _width;
    private static ConfigEntry<int> _length;
    private static ConfigEntry<float> _step;
    private static ConfigEntry<int> _maxSize;
    private static ConfigEntry<float> _blend;

    private static readonly GridOverlay Overlay = new();
    private static readonly Readout Readout = new();

    /// <summary>
    /// Latest aim point from Player.PieceRayTest while an area tool is active.
    /// </summary>
    private static int _aimFrame = -10;
    private static Vector3 _aimPoint;

    /// <summary>
    /// True when the aim is on bare terrain rather than on a piece.
    /// </summary>
    private static bool _aimOnTerrain;

    private static float? _lockedHeight;

    /// <summary>
    /// The ramp's first point, once set; cleared when it's built, cancelled or another tool is picked.
    /// </summary>
    private static Vector3? _rampStart;

    private static readonly List<(int x, int z, float height, float weight)> RampVertices = [];

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
    private static float Blend => _blend.Value;

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

        _blend = config.Bind("Ramp", "Edge Blend", 2f,
            new ConfigDescription("Metres on each side of a ramp where the ground eases into the slope (0, 1, 2, 3, 4 or 6). " +
                "Change it in game with Alt+Scroll while the Ramp tool is selected.",
                new AcceptableValueRange<float>(Blends[0], Blends[Blends.Length - 1])));

        if (Array.IndexOf(Blends, _blend.Value) < 0)
        {
            _blend.Value = 2f;
        }

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
            case HoePieces.RampName:
                own = true;
                return HoeTool.Ramp;
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

    internal static bool RampStarted => _rampStart.HasValue;

    internal static void SetAim(Vector3 point, bool onTerrain)
    {
        _aimFrame = Time.frameCount;
        _aimPoint = point;
        _aimOnTerrain = onTerrain;
    }

    private static bool HasAim => Time.frameCount - _aimFrame <= 2;

    // ----- Per frame -----

    public static void Update(Player player)
    {
        Active = false;
        if (player == null)
        {
            _lockedHeight = null;
            _rampStart = null;
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
        if (tool != HoeTool.Ramp)
        {
            _rampStart = null;
        }

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

        if (tool == HoeTool.Ramp)
        {
            ShowRamp();
            return;
        }

        Area area = CurrentArea();
        float value = ToolValue(tool);
        Overlay.Show(_aimPoint, area, tool, value);
        Readout.Show(ReadoutText(tool, area, value));
    }

    // ----- Ramp -----

    /// <summary>
    /// The vertex under the aim, at the height a ramp end placed there gets: the locked height, the vertex's
    /// own height on bare ground (so the ramp meets it without a step), or the aimed height on a piece (the top
    /// of a floor).
    /// </summary>
    private static Vector3 RampPoint()
    {
        int x = Mathf.RoundToInt(_aimPoint.x);
        int z = Mathf.RoundToInt(_aimPoint.z);
        float height = _lockedHeight ?? (_aimOnTerrain ? TerrainHeights.Get(x, z, _aimPoint.y) : _aimPoint.y);
        return new Vector3(x, height, z);
    }

    private static RampShape CurrentRamp()
    {
        return new RampShape(_rampStart.Value, RampPoint(), Width, Blend);
    }

    private static void ShowRamp()
    {
        if (!_rampStart.HasValue)
        {
            Vector3 point = RampPoint();
            Overlay.ShowRampStart(_aimPoint, point);
            Readout.Show(Localization.instance.Localize("$betterhoe_readout_ramp_start",
                Width.ToString(CultureInfo.InvariantCulture), Metres(Blend))
                + "\n" + PointHeightText("$betterhoe_readout_ramp_start_height", point.y));
            return;
        }

        RampShape ramp = CurrentRamp();
        Overlay.ShowRamp(_aimPoint, ramp);
        string text = Localization.instance.Localize("$betterhoe_readout_ramp",
            Width.ToString(CultureInfo.InvariantCulture), Metres(ramp.Length), Metres(Blend));
        text += "\n" + PointHeightText("$betterhoe_readout_ramp_end_height", ramp.End.y);
        text += "\n" + Localization.instance.Localize("$betterhoe_readout_ramp_slope",
            (ramp.Rise >= 0f ? "+" : "") + Metres(ramp.Rise), ramp.SlopeDegrees.ToString("0.#", CultureInfo.InvariantCulture));
        if (!ramp.IsValid)
        {
            text += "\n<color=#ff8060>" + Localization.instance.Localize("$betterhoe_ramp_too_short") + "</color>";
        }
        else if (RampAtLimit(ramp))
        {
            text += "\n<color=#ff8060>" + Localization.instance.Localize("$betterhoe_readout_limit") + "</color>";
        }

        Readout.Show(text);
    }

    /// <summary>
    /// "Start height 31.2 m", with "(locked)" when the height lock sets it.
    /// </summary>
    private static string PointHeightText(string token, float height)
    {
        string text = Localization.instance.Localize(token, Metres(height));
        return _lockedHeight.HasValue ? text + " " + Localization.instance.Localize("$betterhoe_readout_locked") : text;
    }

    private static RampShape _rampLimitKey;
    private static bool _rampLimit;
    private static int _rampLimitFrame = -100;

    private static bool RampAtLimit(RampShape ramp)
    {
        if (ramp.Equals(_rampLimitKey) && Time.frameCount - _rampLimitFrame < LimitRefreshFrames)
        {
            return _rampLimit;
        }

        _rampLimitKey = ramp;
        _rampLimitFrame = Time.frameCount;
        _rampLimit = RampExceedsLimit(ramp);
        return _rampLimit;
    }

    private static bool RampExceedsLimit(RampShape ramp)
    {
        ramp.Vertices(RampVertices);
        foreach ((int x, int z, float height, float weight) in RampVertices)
        {
            if (TerrainHeights.TryGet(x, z, out float current, out float natural)
                && Mathf.Abs(Mathf.Lerp(current, height, weight) - natural) > TerrainRpc.MaxLevelDelta + 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// First click: remember the start (free). Second click: build the ramp.
    /// </summary>
    private static bool TryPlaceRamp(Player player, Piece piece, out bool placed)
    {
        placed = false;
        Vector3 point = RampPoint();
        if (!_rampStart.HasValue)
        {
            if (!TerrainHeights.TryGet((int)point.x, (int)point.z, out _, out _))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_invalidplacement");
                return true;
            }

            _rampStart = point;
            player.Message(MessageHud.MessageType.TopLeft, "$betterhoe_ramp_started");
            return true;
        }

        RampShape ramp = CurrentRamp();
        if (!ramp.IsValid)
        {
            player.Message(MessageHud.MessageType.Center, "$betterhoe_ramp_too_short");
            return true;
        }

        ramp.Vertices(RampVertices);
        List<(int x, int z)> points = [];
        foreach ((int x, int z, float _, float _) in RampVertices)
        {
            points.Add((x, z));
        }

        if (!CheckAccess(player, points))
        {
            return true;
        }

        TerrainEdits edits = new();
        foreach ((int x, int z, float height, float weight) in RampVertices)
        {
            edits.SetHeight(x, z, height, weight);
        }

        edits.Paint(ramp.Corners(0f), HoeTool.Ramp.PaintColor);
        if (!edits.Send(ramp.Center, ramp.Radius))
        {
            player.Message(MessageHud.MessageType.Center, "$msg_invalidplacement");
            return true;
        }

        PlayEffects(player, piece, ramp.Center);
        _rampStart = null;
        placed = true;
        return true;
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
            else if (Keys.Alt && tool.Shape == ShapeMode.Ramp)
            {
                int index = Mathf.Clamp(Array.IndexOf(Blends, _blend.Value) + direction, 0, Blends.Length - 1);
                _blend.Value = Blends[index];
            }
        }

        // Right-click drops a ramp's start (the build menu stays shut, see Player_UpdateBuildGuiInput_Patch).
        if (_rampStart.HasValue && Input.GetMouseButtonDown(1) && !Keys.AnyModifier)
        {
            _rampStart = null;
            player.Message(MessageHud.MessageType.TopLeft, "$betterhoe_ramp_cancelled");
        }

        if ((tool.Shape is ShapeMode.Flatten or ShapeMode.Ramp) && Input.GetMouseButtonDown(2) && !Keys.AnyModifier && HasAim)
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

        if (tool == HoeTool.Ramp)
        {
            return TryPlaceRamp(player, piece, out placed);
        }

        Area area = CurrentArea();
        if (!CheckAccess(player, AreaVertices(area)))
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

        edits.Paint(area.MinX, area.MinZ, area.MaxX, area.MaxZ, tool.PaintColor);
        return edits;
    }

    private static List<(int x, int z)> AreaVertices(Area area)
    {
        List<(int x, int z)> points = [];
        for (int x = area.MinX; x <= area.MaxX; x++)
        {
            for (int z = area.MinZ; z <= area.MaxZ; z++)
            {
                points.Add((x, z));
            }
        }

        return points;
    }

    /// <summary>
    /// Vanilla only checks the ghost's position; the area can reach much further, so every vertex is checked
    /// against wards and no-build locations.
    /// </summary>
    private static bool CheckAccess(Player player, List<(int x, int z)> vertices)
    {
        bool onTerrain = false;
        foreach ((int x, int z) in vertices)
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
