using System.Collections.Generic;

using BepInEx.Configuration;

using BetterHoe.Terrain;

using UnityEngine;
using UnityEngine.Rendering;

namespace BetterHoe.Grid;

/// <summary>
/// Draws the terrain grid around the aim point and the area the hoe will change. The grid matches
/// BetterHammer's build grid: thin 1 m lines (the terrain's vertex spacing, so nothing finer is possible),
/// thicker lines every 2 m, red chunk borders and green chunk centre lines. Grid lines follow the terrain
/// exactly because they run along the edges of its mesh. On top of it the area gets a gold outline on the
/// ground, and a tinted preview of the result: the flat surface Level ground makes, the surface Raise or
/// Lower ground makes, or the paint colour.
/// </summary>
internal sealed class GridOverlay
{
    private static ConfigEntry<int> _radius;
    private static ConfigEntry<float> _fadeStart;
    private static ConfigEntry<float> _lift;
    private static ConfigEntry<float> _lineWidth;
    private static ConfigEntry<float> _minorLineScale;
    private static ConfigEntry<Color> _lineColor;
    private static ConfigEntry<float> _chunkLineWidth;
    private static ConfigEntry<Color> _chunkLineColor;
    private static ConfigEntry<float> _chunkCentreLineWidth;
    private static ConfigEntry<Color> _chunkCentreLineColor;
    private static ConfigEntry<float> _areaLineWidth;
    private static ConfigEntry<Color> _areaColor;
    private static ConfigEntry<Color> _previewColor;
    private static ConfigEntry<float> _fillAlpha;
    private static ConfigEntry<Color> _blendColor;
    private static ConfigEntry<Color> _regionColor;

    private static int Radius => _radius.Value;
    private static float FadeStart => Mathf.Min(_fadeStart.Value, Radius - 0.01f);
    private static float Lift => _lift.Value;

    /// <summary>
    /// Lines on a multiple of this many metres get the full width, the ones between are thinner.
    /// </summary>
    private const int MajorInterval = 2;

    /// <summary>
    /// How far the aim can move before the grid is redrawn around it.
    /// </summary>
    private const float RebuildDistance = 2f;

    /// <summary>
    /// Frames between redraws while nothing moves, so terrain changes show up.
    /// </summary>
    private const int RefreshFrames = 10;

    /// <summary>
    /// Frames between redraws of a marked region while nothing changes. It can be much bigger than the area, so
    /// it's redrawn less often.
    /// </summary>
    private const int RegionRefreshFrames = 30;

    public static void BindConfig(ConfigFile config)
    {
        const string section = "Grid Appearance";

        _radius = config.Bind(section, "Radius", 10,
            new ConfigDescription("Half-extent in metres of the grid drawn around the aim point.",
                new AcceptableValueRange<int>(2, 32)));
        _fadeStart = config.Bind(section, "Fade Start", 5f,
            new ConfigDescription("Distance in metres from the aim point where grid lines start to fade out (kept below Radius).",
                new AcceptableValueRange<float>(0f, 32f)));
        _lift = config.Bind(section, "Lift", 0.08f,
            new ConfigDescription("Height in metres above the ground, keeps lines clear of grass and z-fighting.",
                new AcceptableValueRange<float>(0f, 1f)));
        _lineWidth = config.Bind(section, "Line Width", 0.06f,
            new ConfigDescription("Width in metres of the grid lines at 2 m intervals.",
                new AcceptableValueRange<float>(0.01f, 1f)));
        _minorLineScale = config.Bind(section, "Minor Line Width Scale", 0.5f,
            new ConfigDescription("Width of the 1 m lines between the 2 m lines, as a fraction of Line Width.",
                new AcceptableValueRange<float>(0.1f, 1f)));
        _lineColor = config.Bind(section, "Line Color", new Color(1f, 1f, 1f, 0.3f),
            "Colour (including alpha) of the grid lines.");
        _chunkLineWidth = config.Bind(section, "Chunk Line Width", 0.2f,
            new ConfigDescription("Width in metres of the lines on world chunk (zone) borders.",
                new AcceptableValueRange<float>(0.01f, 2f)));
        _chunkLineColor = config.Bind(section, "Chunk Line Color", new Color(1f, 0.1f, 0.1f, 0.75f),
            "Colour (including alpha) of the world chunk border lines.");
        _chunkCentreLineWidth = config.Bind(section, "Chunk Centre Line Width", 0.12f,
            new ConfigDescription("Width in metres of the lines through the centre of each world chunk (zone).",
                new AcceptableValueRange<float>(0.01f, 2f)));
        _chunkCentreLineColor = config.Bind(section, "Chunk Centre Line Color", new Color(0.1f, 1f, 0.4f, 0.75f),
            "Colour (including alpha) of the lines through the centre of each world chunk.");
        _areaLineWidth = config.Bind(section, "Area Line Width", 0.12f,
            new ConfigDescription("Width in metres of the outline around the area the hoe changes.",
                new AcceptableValueRange<float>(0.01f, 1f)));
        _areaColor = config.Bind(section, "Area Color", new Color(1f, 0.78f, 0.25f, 0.9f),
            "Colour (including alpha) of the outline around the area on the ground.");
        _previewColor = config.Bind(section, "Preview Color", new Color(0.35f, 0.85f, 1f, 0.9f),
            "Colour (including alpha) of the outline showing the height the area will have.");
        _fillAlpha = config.Bind(section, "Preview Fill Alpha", 0.18f,
            new ConfigDescription("Opacity of the tinted surface showing the result inside the area.",
                new AcceptableValueRange<float>(0f, 1f)));
        _blendColor = config.Bind(section, "Ramp Blend Color", new Color(0.8f, 0.45f, 1f, 0.9f),
            "Colour (including alpha) of a ramp's edge blend: the band beside the ramp where the ground eases into it.");
        _regionColor = config.Bind(section, "Marked Region Color", new Color(0.25f, 0.9f, 0.75f, 0.9f),
            "Colour (including alpha) of the outline around ground marked with Alt+Middle-Click.");
    }

    private readonly Layer _grid = new("BetterHoe_Grid");
    private readonly Layer _area = new("BetterHoe_Area");

    private Vector2Int _gridCenter;
    private int _gridFrame = -100;
    private bool _hasGrid;

    /// <summary>
    /// What the area layer was last built for; it's rebuilt when this changes.
    /// </summary>
    private object _areaKey;
    private int _areaFrame = -100;
    private bool _hasArea;

    public void Show(Vector3 aim, Area area, HoeTool tool, float value)
    {
        ShowGrid(aim);
        ShowArea((area, tool, value), () => BuildArea(area, tool, value, aim.y));
    }

    /// <summary>
    /// Before a ramp's start is set: a marker at the vertex it would go on.
    /// </summary>
    public void ShowRampStart(Vector3 aim, Vector3 point)
    {
        ShowGrid(aim);
        ShowArea(point, () => BuildRampStart(point));
    }

    public void ShowRamp(Vector3 aim, RampShape ramp)
    {
        ShowGrid(aim);
        ShowArea(ramp, () => BuildRamp(ramp, aim.y));
    }

    private void ShowGrid(Vector3 aim)
    {
        Vector2Int center = new(Mathf.RoundToInt(aim.x), Mathf.RoundToInt(aim.z));
        bool gridStale = !_hasGrid
            || Vector2Int.Distance(center, _gridCenter) > RebuildDistance
            || Time.frameCount - _gridFrame >= RefreshFrames;
        if (gridStale && _grid.Ensure())
        {
            BuildGrid(center, aim.y);
            _gridCenter = center;
            _gridFrame = Time.frameCount;
            _hasGrid = true;
        }

        _grid.SetActive(_hasGrid);
    }

    /// <summary>
    /// The marked region with the tool's result previewed over all of it, and the area under the aim (the brush
    /// that marks and unmarks) on the ground: only the part of it that can be marked, or with none of it, the
    /// whole brush outlined in red.
    /// </summary>
    public void ShowRegion(Vector3 aim, Area brush, Area? allowed, HoeTool tool, float value)
    {
        ShowGrid(aim);
        ShowArea((MarkedRegion.Version, brush, allowed, tool, value), () => BuildRegion(brush, allowed, tool, value, aim.y), RegionRefreshFrames);
    }

    private void ShowArea(object key, System.Action build, int refreshFrames = RefreshFrames)
    {
        bool areaStale = !_hasArea || !key.Equals(_areaKey) || Time.frameCount - _areaFrame >= refreshFrames;
        if (areaStale && _area.Ensure())
        {
            build();
            _areaKey = key;
            _areaFrame = Time.frameCount;
            _hasArea = true;
        }

        _area.SetActive(_hasArea);
    }

    public void Hide()
    {
        _grid.SetActive(false);
        _area.SetActive(false);
    }

    public void Destroy()
    {
        _grid.Destroy();
        _area.Destroy();
        _hasGrid = false;
        _hasArea = false;
    }

    // ----- Grid -----

    private void BuildGrid(Vector2Int center, float fallbackHeight)
    {
        MeshBuilder lines = new();
        MeshBuilder chunks = new();
        int radius = Radius;
        float minorWidth = _lineWidth.Value * _minorLineScale.Value;

        for (int offset = -radius; offset <= radius; offset++)
        {
            int x = center.x + offset;
            int z = center.y + offset;
            float widthX = Mod(x, MajorInterval) == 0 ? _lineWidth.Value : minorWidth;
            float widthZ = Mod(z, MajorInterval) == 0 ? _lineWidth.Value : minorWidth;
            AddGridLine(lines, alongZ: true, x, center, radius, widthX, _lineColor.Value, Lift, fallbackHeight, fade: true);
            AddGridLine(lines, alongZ: false, z, center, radius, widthZ, _lineColor.Value, Lift, fallbackHeight, fade: true);
        }

        // Zones are 64 m wide and centred on multiples of 64, so borders are at 32 + 64k.
        const int zone = (int)ZoneSystem.c_ZoneSize;
        for (int offset = -radius; offset <= radius; offset++)
        {
            int x = center.x + offset;
            int z = center.y + offset;
            AddChunkLine(chunks, alongZ: true, x, center, radius, zone, fallbackHeight);
            AddChunkLine(chunks, alongZ: false, z, center, radius, zone, fallbackHeight);
        }

        _grid.Apply(lines, chunks);
    }

    private static void AddChunkLine(MeshBuilder mesh, bool alongZ, int line, Vector2Int center, int radius, int zone, float fallbackHeight)
    {
        int fromCentre = Mod(line, zone);
        if (fromCentre == zone / 2)
        {
            AddGridLine(mesh, alongZ, line, center, radius, _chunkLineWidth.Value, _chunkLineColor.Value, Lift + 0.01f, fallbackHeight, fade: true);
        }
        else if (fromCentre == 0)
        {
            AddGridLine(mesh, alongZ, line, center, radius, _chunkCentreLineWidth.Value, _chunkCentreLineColor.Value, Lift + 0.01f, fallbackHeight, fade: true);
        }
    }

    /// <summary>
    /// A ribbon on the ground along x = line (alongZ) or z = line, sampled at every vertex.
    /// </summary>
    private static void AddGridLine(MeshBuilder mesh, bool alongZ, int line, Vector2Int center, int radius, float width, Color color,
        float lift, float fallbackHeight, bool fade)
    {
        List<Vector3> points = [];
        List<Color> colors = [];
        int across = alongZ ? center.y : center.x;
        float lineOffset = line - (alongZ ? center.x : center.y);

        for (int t = -radius; t <= radius; t++)
        {
            int x = alongZ ? line : across + t;
            int z = alongZ ? across + t : line;
            points.Add(new Vector3(x, TerrainHeights.Get(x, z, fallbackHeight) + lift, z));

            float alpha = color.a;
            if (fade)
            {
                float dist = Mathf.Sqrt((lineOffset * lineOffset) + (t * t));
                alpha *= Mathf.Clamp01(1f - ((dist - FadeStart) / (Radius - FadeStart)));
            }

            colors.Add(new Color(color.r, color.g, color.b, alpha));
        }

        mesh.AddRibbon(points, colors, width);
    }

    // ----- Area -----

    private void BuildArea(Area area, HoeTool tool, float value, float fallbackHeight)
    {
        MeshBuilder fill = new();
        MeshBuilder lines = new();
        float lift = Lift + 0.02f;

        // Current and natural (unedited) heights of the area's vertices.
        int countX = area.SizeX + 1;
        int countZ = area.SizeZ + 1;
        float[,] ground = new float[countX, countZ];
        float[,] natural = new float[countX, countZ];
        for (int i = 0; i < countX; i++)
        {
            for (int j = 0; j < countZ; j++)
            {
                if (TerrainHeights.TryGet(area.MinX + i, area.MinZ + j, out float height, out float naturalHeight))
                {
                    ground[i, j] = height;
                    natural[i, j] = naturalHeight;
                }
                else
                {
                    ground[i, j] = natural[i, j] = fallbackHeight;
                }
            }
        }

        float Preview(int i, int j)
        {
            return tool.Shape switch
            {
                ShapeMode.Flatten => value,
                ShapeMode.Raise or ShapeMode.Lower => ground[i, j] + value,
                ShapeMode.Reset => natural[i, j],
                _ => ground[i, j],
            };
        }

        Color previewColor = _previewColor.Value;
        Color fillColor = tool.Shape == ShapeMode.None ? PaintTint(tool) : previewColor;
        fillColor.a = _fillAlpha.Value;

        // Preview surface: one quad per cell.
        for (int i = 0; i < area.SizeX; i++)
        {
            for (int j = 0; j < area.SizeZ; j++)
            {
                int x = area.MinX + i;
                int z = area.MinZ + j;
                fill.AddQuad(
                    new Vector3(x, Preview(i, j) + lift, z),
                    new Vector3(x, Preview(i, j + 1) + lift, z + 1),
                    new Vector3(x + 1, Preview(i + 1, j + 1) + lift, z + 1),
                    new Vector3(x + 1, Preview(i + 1, j) + lift, z),
                    fillColor);
            }
        }

        // Outline on the ground, and for height changes an outline at the new height with posts between.
        float width = _areaLineWidth.Value;
        AddOutline(lines, area, (i, j) => ground[i, j] + lift + 0.01f, width, _areaColor.Value);
        if (tool.Shape != ShapeMode.None)
        {
            AddOutline(lines, area, (i, j) => Preview(i, j) + lift + 0.01f, width, previewColor);
            foreach ((int i, int j) in new[] { (0, 0), (area.SizeX, 0), (0, area.SizeZ), (area.SizeX, area.SizeZ) })
            {
                Vector3 bottom = new(area.MinX + i, ground[i, j], area.MinZ + j);
                Vector3 top = new(bottom.x, Preview(i, j), bottom.z);
                if (Mathf.Abs(top.y - bottom.y) > 0.05f)
                {
                    lines.AddPost(bottom, top, width, previewColor);
                }
            }
        }

        _area.Apply(fill, lines);
    }

    // ----- Marked region -----

    private void BuildRegion(Area fullBrush, Area? allowed, HoeTool tool, float value, float fallbackHeight)
    {
        MeshBuilder fill = new();
        MeshBuilder lines = new();
        float lift = Lift + 0.02f;

        Dictionary<(int x, int z), (float ground, float natural)> heights = [];
        foreach ((int x, int z) vertex in MarkedRegion.Vertices())
        {
            heights[vertex] = TerrainHeights.TryGet(vertex.x, vertex.z, out float height, out float natural)
                ? (height, natural)
                : (fallbackHeight, fallbackHeight);
        }

        float Ground(int x, int z)
        {
            return heights.TryGetValue((x, z), out (float ground, float natural) h) ? h.ground : fallbackHeight;
        }

        float Preview(int x, int z)
        {
            (float ground, float natural) = heights.TryGetValue((x, z), out (float, float) found) ? found : (fallbackHeight, fallbackHeight);
            return tool.Shape switch
            {
                ShapeMode.Flatten => value,
                ShapeMode.Raise or ShapeMode.Lower => ground + value,
                ShapeMode.Reset => natural,
                _ => ground,
            };
        }

        Color previewColor = _previewColor.Value;
        Color fillColor = tool.Shape == ShapeMode.None ? PaintTint(tool) : previewColor;
        fillColor.a = _fillAlpha.Value;
        Color regionColor = _regionColor.Value;
        Color regionFill = regionColor;
        regionFill.a = _fillAlpha.Value;
        float width = _areaLineWidth.Value;
        bool shapes = tool.Shape != ShapeMode.None;

        Area bounds = MarkedRegion.Bounds;
        for (int x = bounds.MinX; x < bounds.MaxX; x++)
        {
            for (int z = bounds.MinZ; z < bounds.MaxZ; z++)
            {
                if (!MarkedRegion.Contains(x, z))
                {
                    continue;
                }

                // Teal on the ground, so the marked cells show whatever the tool does; for height changes also
                // the new surface, which can be under the ground (Lower ground) and then only shows where it's not.
                fill.AddQuad(
                    new Vector3(x, Ground(x, z) + lift, z),
                    new Vector3(x, Ground(x, z + 1) + lift, z + 1),
                    new Vector3(x + 1, Ground(x + 1, z + 1) + lift, z + 1),
                    new Vector3(x + 1, Ground(x + 1, z) + lift, z),
                    regionFill);
                if (shapes)
                {
                    fill.AddQuad(
                        new Vector3(x, Preview(x, z) + lift, z),
                        new Vector3(x, Preview(x, z + 1) + lift, z + 1),
                        new Vector3(x + 1, Preview(x + 1, z + 1) + lift, z + 1),
                        new Vector3(x + 1, Preview(x + 1, z) + lift, z),
                        fillColor);
                }

                // The outline runs along every cell edge with no marked cell on its other side.
                foreach ((int dx, int dz, int ax, int az, int bx, int bz) in Edges)
                {
                    if (MarkedRegion.Contains(x + dx, z + dz))
                    {
                        continue;
                    }

                    (int x, int z) a = (x + ax, z + az);
                    (int x, int z) b = (x + bx, z + bz);
                    AddSegment(lines, a, b, Ground(a.x, a.z) + lift + 0.01f, Ground(b.x, b.z) + lift + 0.01f, width, regionColor);
                    if (shapes)
                    {
                        AddSegment(lines, a, b, Preview(a.x, a.z) + lift + 0.01f, Preview(b.x, b.z) + lift + 0.01f, width, previewColor);
                    }
                }
            }
        }

        // The brush: a faint gold fill and outline on the ground, a little above the region's fill. Past the size
        // limit only the part that can be marked, or in red with none of it.
        Area brush = allowed ?? fullBrush;
        Color brushColor = allowed.HasValue ? _areaColor.Value : OutOfRangeColor;
        float[,] brushGround = new float[brush.SizeX + 1, brush.SizeZ + 1];
        for (int i = 0; i <= brush.SizeX; i++)
        {
            for (int j = 0; j <= brush.SizeZ; j++)
            {
                brushGround[i, j] = TerrainHeights.Get(brush.MinX + i, brush.MinZ + j, fallbackHeight) + lift + 0.015f;
            }
        }

        Color brushFill = brushColor;
        brushFill.a = allowed.HasValue ? _fillAlpha.Value : 0f;
        for (int i = 0; allowed.HasValue && i < brush.SizeX; i++)
        {
            for (int j = 0; j < brush.SizeZ; j++)
            {
                int x = brush.MinX + i;
                int z = brush.MinZ + j;
                fill.AddQuad(
                    new Vector3(x, brushGround[i, j], z),
                    new Vector3(x, brushGround[i, j + 1], z + 1),
                    new Vector3(x + 1, brushGround[i + 1, j + 1], z + 1),
                    new Vector3(x + 1, brushGround[i + 1, j], z),
                    brushFill);
            }
        }

        AddOutline(lines, brush, (i, j) => brushGround[i, j], width, brushColor);

        _area.Apply(fill, lines);
    }

    /// <summary>
    /// The brush outline when none of it can be marked because the region would be too big.
    /// </summary>
    private static readonly Color OutOfRangeColor = new(1f, 0.38f, 0.3f, 0.9f);

    /// <summary>
    /// A cell's four edges: the neighbour across it, and the edge's two ends, as offsets from the cell's
    /// south-west vertex.
    /// </summary>
    private static readonly (int dx, int dz, int ax, int az, int bx, int bz)[] Edges =
    [
        (0, -1, 0, 0, 1, 0),
        (0, 1, 0, 1, 1, 1),
        (-1, 0, 0, 0, 0, 1),
        (1, 0, 1, 0, 1, 1),
    ];

    /// <summary>
    /// A straight 1 m ribbon between two vertices, stretched by half its width at each end so corners close.
    /// </summary>
    private static void AddSegment(MeshBuilder mesh, (int x, int z) a, (int x, int z) b, float heightA, float heightB, float width, Color color)
    {
        Vector3 start = new(a.x, heightA, a.z);
        Vector3 end = new(b.x, heightB, b.z);
        Vector3 along = new Vector3(b.x - a.x, 0f, b.z - a.z) * (width / 2f);
        mesh.AddRibbon([start - along, end + along], [color, color], width);
    }

    // ----- Ramp -----

    /// <summary>
    /// A gold cross on the ground at the vertex, and a post up or down to the height the ramp would start at.
    /// </summary>
    private void BuildRampStart(Vector3 point)
    {
        MeshBuilder fill = new();
        MeshBuilder lines = new();
        float lift = Lift + 0.03f;
        float width = _areaLineWidth.Value;
        Color color = _areaColor.Value;

        AddGroundLine(lines, point + new Vector3(-0.75f, 0f, 0f), point + new Vector3(0.75f, 0f, 0f), lift, width, color, point.y);
        AddGroundLine(lines, point + new Vector3(0f, 0f, -0.75f), point + new Vector3(0f, 0f, 0.75f), lift, width, color, point.y);

        float ground = TerrainHeights.GroundAt(point.x, point.z, point.y);
        Vector3 bottom = new(point.x, Mathf.Min(ground, point.y), point.z);
        Vector3 top = new(point.x, Mathf.Max(ground, point.y) + 1f, point.z);
        lines.AddPost(bottom, top, width, _previewColor.Value);

        _area.Apply(fill, lines);
    }

    /// <summary>
    /// The ramp's surface (it's flat, so one quad), its edges at the new heights with posts at the corners, its
    /// outline on the ground in gold, and the outer edges of the blend bands on the ground.
    /// </summary>
    private void BuildRamp(RampShape ramp, float fallbackHeight)
    {
        MeshBuilder fill = new();
        MeshBuilder lines = new();
        float lift = Lift + 0.03f;
        float width = _areaLineWidth.Value;
        Color preview = _previewColor.Value;
        Color fillColor = preview;
        fillColor.a = _fillAlpha.Value;

        Vector3[] core = ramp.Corners(0f);
        if (ramp.Length > 0.01f)
        {
            Vector3 up = Vector3.up * lift;
            fill.AddQuad(core[0] + up, core[1] + up, core[2] + up, core[3] + up, fillColor);
        }

        for (int i = 0; i < 4; i++)
        {
            Vector3 a = core[i];
            Vector3 b = core[(i + 1) % 4];
            lines.AddRibbon([a + (Vector3.up * lift), b + (Vector3.up * lift)], [preview, preview], width);
            AddGroundLine(lines, a, b, lift, width, _areaColor.Value, fallbackHeight);

            float ground = TerrainHeights.GroundAt(a.x, a.z, a.y);
            if (Mathf.Abs(ground - a.y) > 0.05f)
            {
                lines.AddPost(new Vector3(a.x, ground, a.z), a, width, preview);
            }
        }

        if (ramp.Blend > 0f)
        {
            Color blend = _blendColor.Value;
            AddBlendBand(fill, ramp, 1f, lift, blend, fallbackHeight);
            AddBlendBand(fill, ramp, -1f, lift, blend, fallbackHeight);
            Vector3[] outer = ramp.Corners(ramp.Blend);
            AddGroundLine(lines, outer[3], outer[0], lift, width * 0.7f, blend, fallbackHeight);
            AddGroundLine(lines, outer[1], outer[2], lift, width * 0.7f, blend, fallbackHeight);
        }

        _area.Apply(fill, lines);
    }

    /// <summary>
    /// The blend band on one side of the ramp (`side` +1 = left, -1 = right), drawn at the heights it will have:
    /// eased from the ramp's edge down or up to the ground. It fades out towards the ground, as the blend does.
    /// </summary>
    private static void AddBlendBand(MeshBuilder mesh, RampShape ramp, float side, float lift, Color color, float fallbackHeight)
    {
        const int across = 4;
        int along = Mathf.Max(1, Mathf.CeilToInt(ramp.Length));
        float half = ramp.Width / 2f;
        float maxAlpha = Mathf.Clamp01(_fillAlpha.Value * 2f);
        Vector3[,] points = new Vector3[along + 1, across + 1];
        Color[,] colors = new Color[along + 1, across + 1];

        for (int i = 0; i <= along; i++)
        {
            float t = i / (float)along;
            Vector3 centre = Vector3.Lerp(ramp.Start, ramp.End, t);
            for (int j = 0; j <= across; j++)
            {
                float distance = half + (ramp.Blend * j / across);
                Vector3 point = centre + (ramp.Across * side * distance);
                float weight = ramp.WeightAt(point.x, point.z, out float height);
                float ground = TerrainHeights.GroundAt(point.x, point.z, fallbackHeight);
                point.y = Mathf.Lerp(ground, height, weight) + lift;
                points[i, j] = point;
                colors[i, j] = new Color(color.r, color.g, color.b, maxAlpha * weight);
            }
        }

        for (int i = 0; i < along; i++)
        {
            for (int j = 0; j < across; j++)
            {
                mesh.AddQuad(points[i, j], points[i + 1, j], points[i + 1, j + 1], points[i, j + 1],
                    colors[i, j], colors[i + 1, j], colors[i + 1, j + 1], colors[i, j + 1]);
            }
        }
    }

    /// <summary>
    /// A ribbon on the ground from a to b, sampled every half metre so it follows the terrain.
    /// </summary>
    private static void AddGroundLine(MeshBuilder mesh, Vector3 a, Vector3 b, float lift, float width, Color color, float fallbackHeight)
    {
        Vector3 flat = b - a;
        flat.y = 0f;
        int segments = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude / 0.5f));
        List<Vector3> points = [];
        List<Color> colors = [];
        for (int k = 0; k <= segments; k++)
        {
            Vector3 point = Vector3.Lerp(a, b, k / (float)segments);
            point.y = TerrainHeights.GroundAt(point.x, point.z, fallbackHeight) + lift;
            points.Add(point);
            colors.Add(color);
        }

        mesh.AddRibbon(points, colors, width);
    }

    private static void AddOutline(MeshBuilder mesh, Area area, System.Func<int, int, float> height, float width, Color color)
    {
        // Each side is extended by half the width so the corners close.
        float extend = width / 2f;
        AddEdge(mesh, area, height, width, color, alongZ: false, fixedIndex: 0, extend);
        AddEdge(mesh, area, height, width, color, alongZ: false, fixedIndex: area.SizeZ, extend);
        AddEdge(mesh, area, height, width, color, alongZ: true, fixedIndex: 0, extend);
        AddEdge(mesh, area, height, width, color, alongZ: true, fixedIndex: area.SizeX, extend);
    }

    private static void AddEdge(MeshBuilder mesh, Area area, System.Func<int, int, float> height, float width, Color color,
        bool alongZ, int fixedIndex, float extend)
    {
        int count = (alongZ ? area.SizeZ : area.SizeX) + 1;
        List<Vector3> points = [];
        List<Color> colors = [];
        for (int k = 0; k < count; k++)
        {
            int i = alongZ ? fixedIndex : k;
            int j = alongZ ? k : fixedIndex;
            Vector3 point = new(area.MinX + i, height(i, j), area.MinZ + j);
            if (k == 0)
            {
                point += (alongZ ? Vector3.back : Vector3.left) * extend;
            }
            else if (k == count - 1)
            {
                point += (alongZ ? Vector3.forward : Vector3.right) * extend;
            }

            points.Add(point);
            colors.Add(color);
        }

        mesh.AddRibbon(points, colors, width);
    }

    /// <summary>
    /// A colour that reads as the paint type: earthy for dirt, grey for paving, green for natural ground.
    /// </summary>
    private static Color PaintTint(HoeTool tool)
    {
        return tool.Paint switch
        {
            TerrainModifier.PaintType.Paved => new Color(0.75f, 0.75f, 0.78f),
            TerrainModifier.PaintType.Reset => new Color(0.4f, 0.85f, 0.3f),
            _ => new Color(0.7f, 0.5f, 0.25f),
        };
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    // ----- Mesh plumbing -----

    /// <summary>
    /// Collects vertices, colours and triangles for one submesh.
    /// </summary>
    private sealed class MeshBuilder
    {
        public readonly List<Vector3> Vertices = [];
        public readonly List<Color> Colors = [];
        public readonly List<int> Indices = [];

        /// <summary>
        /// A flat ribbon of the given width along a polyline, facing up.
        /// </summary>
        public void AddRibbon(List<Vector3> points, List<Color> colors, float width)
        {
            if (points.Count < 2)
            {
                return;
            }

            float half = width / 2f;
            int start = Vertices.Count;
            for (int k = 0; k < points.Count; k++)
            {
                Vector3 previous = points[Mathf.Max(k - 1, 0)];
                Vector3 next = points[Mathf.Min(k + 1, points.Count - 1)];
                Vector3 direction = next - previous;
                direction.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * half;

                Vertices.Add(points[k] - side);
                Vertices.Add(points[k] + side);
                Colors.Add(colors[k]);
                Colors.Add(colors[k]);

                if (k > 0)
                {
                    int i = start + ((k - 1) * 2);
                    AddTriangles(i, i + 1, i + 3, i + 2);
                }
            }
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            AddQuad(a, b, c, d, color, color, color, color);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colorA, Color colorB, Color colorC, Color colorD)
        {
            int start = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            Colors.Add(colorA);
            Colors.Add(colorB);
            Colors.Add(colorC);
            Colors.Add(colorD);
            AddTriangles(start, start + 1, start + 2, start + 3);
        }

        /// <summary>
        /// A vertical post between two points, made of two crossed quads so it shows from every side.
        /// </summary>
        public void AddPost(Vector3 bottom, Vector3 top, float width, Color color)
        {
            float half = width / 2f;
            foreach (Vector3 side in new[] { Vector3.right * half, Vector3.forward * half })
            {
                AddQuad(bottom - side, top - side, top + side, bottom + side, color);
            }
        }

        /// <summary>
        /// Two triangles for the quad a-b-c-d. The sprite shader doesn't cull, so they show from both sides.
        /// </summary>
        private void AddTriangles(int a, int b, int c, int d)
        {
            Indices.AddRange([a, b, c, a, c, d]);
        }
    }

    /// <summary>
    /// One overlay object with two submeshes sharing the same transparent material.
    /// </summary>
    private sealed class Layer(string name)
    {
        private readonly string _name = name;
        private GameObject _object;
        private Mesh _mesh;
        private bool _warned;

        public bool Ensure()
        {
            if (_object != null)
            {
                return true;
            }

            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                if (!_warned)
                {
                    Jotunn.Logger.LogWarning("No usable shader found for the terrain grid overlay.");
                }

                _warned = true;
                return false;
            }

            _mesh = new Mesh { name = _name, indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();

            _object = new GameObject(_name);
            Object.DontDestroyOnLoad(_object);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

            MeshRenderer renderer = _object.AddComponent<MeshRenderer>();
            Material material = new(shader);
            renderer.sharedMaterials = [material, material];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _object.SetActive(false);
            return true;
        }

        public void Apply(MeshBuilder first, MeshBuilder second)
        {
            int offset = first.Vertices.Count;
            List<Vector3> vertices = [.. first.Vertices, .. second.Vertices];
            List<Color> colors = [.. first.Colors, .. second.Colors];
            List<int> secondIndices = [];
            foreach (int index in second.Indices)
            {
                secondIndices.Add(index + offset);
            }

            _mesh.Clear();
            _mesh.subMeshCount = 2;
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetIndices(first.Indices, MeshTopology.Triangles, 0);
            _mesh.SetIndices(secondIndices, MeshTopology.Triangles, 1);
            _mesh.RecalculateBounds();
        }

        public void SetActive(bool active)
        {
            if (_object != null && _object.activeSelf != active)
            {
                _object.SetActive(active);
            }
        }

        public void Destroy()
        {
            if (_object != null)
            {
                Object.Destroy(_object);
            }

            if (_mesh != null)
            {
                Object.Destroy(_mesh);
            }

            _object = null;
            _mesh = null;
        }
    }
}
