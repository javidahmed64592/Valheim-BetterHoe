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
    }

    private readonly Layer _grid = new("BetterHoe_Grid");
    private readonly Layer _area = new("BetterHoe_Area");

    private Vector2Int _gridCenter;
    private int _gridFrame = -100;
    private bool _hasGrid;

    private (Area area, HoeTool tool, float value) _areaKey;
    private int _areaFrame = -100;
    private bool _hasArea;

    public void Show(Vector3 aim, Area area, HoeTool tool, float value)
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

        (Area, HoeTool, float) key = (area, tool, value);
        bool areaStale = !_hasArea || !key.Equals(_areaKey) || Time.frameCount - _areaFrame >= RefreshFrames;
        if (areaStale && _area.Ensure())
        {
            BuildArea(area, tool, value, aim.y);
            _areaKey = key;
            _areaFrame = Time.frameCount;
            _hasArea = true;
        }

        _grid.SetActive(_hasGrid);
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

        // Ground heights of the area's vertices.
        int countX = area.SizeX + 1;
        int countZ = area.SizeZ + 1;
        float[,] ground = new float[countX, countZ];
        for (int i = 0; i < countX; i++)
        {
            for (int j = 0; j < countZ; j++)
            {
                ground[i, j] = TerrainHeights.Get(area.MinX + i, area.MinZ + j, fallbackHeight);
            }
        }

        float Preview(int i, int j)
        {
            return tool.Shape switch
            {
                ShapeMode.Flatten => value,
                ShapeMode.Raise or ShapeMode.Lower => ground[i, j] + value,
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
            int start = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            for (int k = 0; k < 4; k++)
            {
                Colors.Add(color);
            }

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
