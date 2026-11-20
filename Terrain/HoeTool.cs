using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// What a hoe piece does to the heights in the area.
/// </summary>
internal enum ShapeMode { None = 0, Flatten = 1, Raise = 2, Lower = 3, Reset = 4, Ramp = 5 }

/// <summary>
/// What a hoe piece does inside the area: a height change, a paint, or both. Vanilla's level, raise and dig
/// operations all paint dirt as well, so the shaping tools do too. Reset height leaves the paint alone.
/// </summary>
internal sealed class HoeTool
{
    public static readonly HoeTool Flatten = new(ShapeMode.Flatten, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_flatten", "$betterhoe_hint_flatten");
    public static readonly HoeTool Raise = new(ShapeMode.Raise, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_raise", "$betterhoe_hint_raise");
    public static readonly HoeTool Lower = new(ShapeMode.Lower, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_lower", "$betterhoe_hint_lower");
    public static readonly HoeTool Ramp = new(ShapeMode.Ramp, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_ramp", "$betterhoe_hint_ramp_end");
    public static readonly HoeTool ResetHeight = new(ShapeMode.Reset, null, "$betterhoe_tool_reset_height", "$betterhoe_hint_reset_height");
    public static readonly HoeTool PaintDirt = new(ShapeMode.None, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_path", "$betterhoe_hint_path");
    public static readonly HoeTool PaintPaved = new(ShapeMode.None, TerrainModifier.PaintType.Paved, "$betterhoe_tool_paved", "$betterhoe_hint_paved");
    public static readonly HoeTool PaintReset = new(ShapeMode.None, TerrainModifier.PaintType.Reset, "$betterhoe_tool_reset_paint", "$betterhoe_hint_reset_paint");

    public ShapeMode Shape { get; }

    /// <summary>
    /// The paint the tool puts on the area, or null if it leaves the paint alone.
    /// </summary>
    public TerrainModifier.PaintType? Paint { get; }
    public string NameToken { get; }

    /// <summary>
    /// What the place key does with this tool, for the key hints.
    /// </summary>
    public string ActionToken { get; }

    private HoeTool(ShapeMode shape, TerrainModifier.PaintType? paint, string nameToken, string actionToken)
    {
        Shape = shape;
        Paint = paint;
        NameToken = nameToken;
        ActionToken = actionToken;
    }

    /// <summary>
    /// The paint mask colour vanilla's TerrainComp.PaintCleared blends towards for this paint type.
    /// </summary>
    public Color PaintColor => Paint switch
    {
        TerrainModifier.PaintType.Paved => Heightmap.m_paintMaskPaved,
        TerrainModifier.PaintType.Reset => Heightmap.m_paintMaskNothing,
        TerrainModifier.PaintType.Cultivate => Heightmap.m_paintMaskCultivated,
        _ => Heightmap.m_paintMaskDirt,
    };
}
