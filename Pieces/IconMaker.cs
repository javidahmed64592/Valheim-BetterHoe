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
