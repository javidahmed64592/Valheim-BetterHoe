using UnityEngine;

namespace BetterHoe.Pieces;

/// <summary>
/// Builds piece icons from vanilla ones. Vanilla icon textures aren't readable, so they are copied through a
/// render texture.
/// </summary>
internal static class IconMaker
{
    /// <summary>
    /// The sprite turned upside down: Raise ground's mound and up arrow become a pit and a down arrow.
    /// </summary>
    public static Sprite FlipVertically(Sprite source)
    {
        Texture2D texture = source.texture;
        Rect rect = source.textureRect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        // Blit reads the source at uv * scale + offset; a negative y scale starting from the top flips it.
        Vector2 scale = new(rect.width / texture.width, -rect.height / texture.height);
        Vector2 offset = new(rect.x / texture.width, (rect.y + rect.height) / texture.height);

        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, target, scale, offset);

        RenderTexture.active = target;
        Texture2D copy = new(width, height, TextureFormat.RGBA32, mipChain: false) { name = source.name + "_flipped" };
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        Sprite sprite = Sprite.Create(copy, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        sprite.name = copy.name;
        return sprite;
    }
}
