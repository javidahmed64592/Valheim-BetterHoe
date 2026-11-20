using UnityEngine;

namespace BetterHoe.Pieces;

/// <summary>
/// Builds piece icons from vanilla ones. Vanilla icon textures aren't readable, so they are copied through a
/// render texture first.
/// </summary>
internal static class IconMaker
{
    /// <summary>
    /// The sprite turned upside down: Raise ground's mound and up arrow become a pit and a down arrow.
    /// </summary>
    public static Sprite FlipVertically(Sprite source)
    {
        return ToSprite(Copy(source, flip: true), source, "_flipped");
    }

    /// <summary>
    /// Raise ground's mound with its up arrow swapped for a circular "reset" arrow in the same colour.
    /// </summary>
    public static Sprite ResetArrow(Sprite source)
    {
        Texture2D texture = Copy(source, flip: false);
        int width = texture.width;
        int height = texture.height;
        Color[] pixels = texture.GetPixels();

        // The up arrow sits above the mound, in the upper part of the icon (texture rows count from the bottom).
        int arrowFrom = Mathf.RoundToInt(height * 0.45f);
        Color arrow = Color.clear;
        float weight = 0f;
        for (int y = arrowFrom; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[(y * width) + x];
                arrow += pixel * pixel.a;
                weight += pixel.a;
                pixels[(y * width) + x] = Color.clear;
            }
        }

        arrow = weight > 0f ? arrow / weight : new Color(0.85f, 0.75f, 0.55f);
        arrow.a = 1f;

        // A ring arc with an arrowhead at its end, drawn with 4x4 supersampling for smooth edges.
        Vector2 centre = new(width * 0.5f, height * 0.73f);
        float radius = width * 0.15f;
        float thickness = width * 0.055f;
        const float arcStart = 60f;
        const float arcEnd = 330f;
        const float endAngle = arcEnd * Mathf.Deg2Rad;
        Vector2 end = centre + (new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * radius);
        Vector2 tangent = new(-Mathf.Sin(endAngle), Mathf.Cos(endAngle));
        Vector2 outward = new(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
        float head = thickness * 1.6f;
        Vector2 tip = end + (tangent * head * 1.2f);
        Vector2 left = end + (outward * head);
        Vector2 right = end - (outward * head);

        const int samples = 4;
        for (int y = arrowFrom; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        Vector2 p = new(x + ((sx + 0.5f) / samples), y + ((sy + 0.5f) / samples));
                        if (InArc(p, centre, radius, thickness, arcStart, arcEnd) || InTriangle(p, tip, left, right))
                        {
                            hits++;
                        }
                    }
                }

                if (hits > 0)
                {
                    Color pixel = arrow;
                    pixel.a = hits / (float)(samples * samples);
                    pixels[(y * width) + x] = pixel;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return ToSprite(texture, source, "_reset");
    }

    /// <summary>
    /// A wedge of Raise ground's soil with its arrow running up the slope, for the Ramp tool.
    /// </summary>
    public static Sprite Ramp(Sprite source)
    {
        Texture2D texture = Copy(source, flip: false);
        int width = texture.width;
        int height = texture.height;
        Color[] pixels = texture.GetPixels();

        // The mound is in the lower part of the icon and the arrow above it (texture rows count from the bottom).
        int split = Mathf.RoundToInt(height * 0.45f);
        Color soil = Average(pixels, width, 0, split, new Color(0.45f, 0.35f, 0.15f));
        Color arrow = Average(pixels, width, split, height, new Color(0.85f, 0.75f, 0.55f));
        Color edge = Color.Lerp(soil, Color.black, 0.35f);
        edge.a = 1f;
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        Vector2 low = new(width * 0.1f, height * 0.18f);
        Vector2 foot = new(width * 0.9f, height * 0.18f);
        Vector2 high = new(width * 0.9f, height * 0.58f);
        float outline = width * 0.03f;
        Fill(pixels, width, height, p => InTriangle(p, low, foot, high), edge);
        Fill(pixels, width, height, p => InTriangle(p, low, foot, high) && Inset(p, low, foot, high, outline), soil);

        // The arrow runs parallel to the slope, above it.
        Vector2 slope = (high - low).normalized;
        Vector2 normal = new(-slope.y, slope.x);
        Vector2 from = low + (slope * width * 0.22f) + (normal * height * 0.14f);
        Vector2 to = low + (slope * width * 0.72f) + (normal * height * 0.14f);
        float shaft = width * 0.045f;
        float head = width * 0.1f;
        Vector2 tip = to + (slope * head);
        Fill(pixels, width, height, p => NearSegment(p, from, to, shaft / 2f)
            || InTriangle(p, tip, to + (normal * head * 0.75f), to - (normal * head * 0.75f)), arrow);

        texture.SetPixels(pixels);
        texture.Apply();
        return ToSprite(texture, source, "_ramp");
    }

    private static Color Average(Color[] pixels, int width, int fromRow, int toRow, Color fallback)
    {
        Color sum = Color.clear;
        float weight = 0f;
        for (int y = fromRow; y < toRow; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[(y * width) + x];
                if (pixel.a > 0.5f)
                {
                    sum += pixel * pixel.a;
                    weight += pixel.a;
                }
            }
        }

        Color result = weight > 0f ? sum / weight : fallback;
        result.a = 1f;
        return result;
    }

    /// <summary>
    /// Paints `color` over the pixels inside the shape, with 4x4 supersampling for smooth edges.
    /// </summary>
    private static void Fill(Color[] pixels, int width, int height, System.Func<Vector2, bool> inside, Color color)
    {
        const int samples = 4;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        if (inside(new Vector2(x + ((sx + 0.5f) / samples), y + ((sy + 0.5f) / samples))))
                        {
                            hits++;
                        }
                    }
                }

                if (hits > 0)
                {
                    float coverage = hits / (float)(samples * samples);
                    Color under = pixels[(y * width) + x];
                    Color over = color;
                    over.a = 1f;
                    Color blended = Color.Lerp(under, over, coverage);
                    blended.a = Mathf.Max(under.a, coverage);
                    pixels[(y * width) + x] = blended;
                }
            }
        }
    }

    /// <summary>
    /// True when p is at least `margin` inside every edge of the triangle.
    /// </summary>
    private static bool Inset(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float margin)
    {
        return DistanceToLine(p, a, b) >= margin && DistanceToLine(p, b, c) >= margin && DistanceToLine(p, c, a) >= margin;
    }

    private static float DistanceToLine(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 offset = p - a;
        return Mathf.Abs((offset.x * direction.y) - (offset.y * direction.x));
    }

    private static bool NearSegment(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + (ab * t))).magnitude <= radius;
    }

    private static bool InArc(Vector2 p, Vector2 centre, float radius, float thickness, float fromDegrees, float toDegrees)
    {
        Vector2 offset = p - centre;
        if (Mathf.Abs(offset.magnitude - radius) > thickness / 2f)
        {
            return false;
        }

        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        if (angle < 0f)
        {
            angle += 360f;
        }

        return angle >= fromDegrees && angle <= toDegrees;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        static float Side(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return ((p1.x - p3.x) * (p2.y - p3.y)) - ((p2.x - p3.x) * (p1.y - p3.y));
        }

        float d1 = Side(p, a, b);
        float d2 = Side(p, b, c);
        float d3 = Side(p, c, a);
        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(negative && positive);
    }

    /// <summary>
    /// A readable copy of the sprite's pixels, optionally upside down.
    /// </summary>
    private static Texture2D Copy(Sprite source, bool flip)
    {
        Texture2D texture = source.texture;
        Rect rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        // Blit reads the source at uv * scale + offset; a negative y scale starting from the top flips it.
        Vector2 scale = new(rect.width / texture.width, (flip ? -1f : 1f) * rect.height / texture.height);
        Vector2 offset = new(rect.x / texture.width, (rect.y + (flip ? rect.height : 0f)) / texture.height);

        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, target, scale, offset);

        RenderTexture.active = target;
        Texture2D copy = new(width, height, TextureFormat.RGBA32, mipChain: false);
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return copy;
    }

    private static Sprite ToSprite(Texture2D texture, Sprite source, string suffix)
    {
        texture.name = source.name + suffix;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        sprite.name = texture.name;
        return sprite;
    }
}
