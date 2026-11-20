using UnityEngine;

namespace BetterHoe.Terrain;

/// <summary>
/// What a hoe piece does to the heights in the area.
/// </summary>
internal enum ShapeMode { None = 0, Flatten = 1, Raise = 2, Lower = 3 }

/// <summary>
/// What a hoe piece does inside the area: a height change, a paint, or both. Vanilla's level, raise and dig
/// operations all paint dirt as well, so the shaping tools do too.
/// </summary>
internal sealed class HoeTool
{
    public static readonly HoeTool Flatten = new(ShapeMode.Flatten, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_flatten");
    public static readonly HoeTool Raise = new(ShapeMode.Raise, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_raise");
    public static readonly HoeTool Lower = new(ShapeMode.Lower, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_lower");
    public static readonly HoeTool PaintDirt = new(ShapeMode.None, TerrainModifier.PaintType.Dirt, "$betterhoe_tool_path");
    public static readonly HoeTool PaintPaved = new(ShapeMode.None, TerrainModifier.PaintType.Paved, "$betterhoe_tool_paved");
    public static readonly HoeTool PaintReset = new(ShapeMode.None, TerrainModifier.PaintType.Reset, "$betterhoe_tool_reset");

    public ShapeMode Shape { get; }
    public TerrainModifier.PaintType Paint { get; }
    public string NameToken { get; }

    private HoeTool(ShapeMode shape, TerrainModifier.PaintType paint, string nameToken)
    {
        Shape = shape;
        Paint = paint;
        NameToken = nameToken;
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
