using UnityEngine;

namespace SledFishMod;

public static class SpriteLoader
{
    public static Sprite Load(int fishId)
    {
        var def = CustomFishRegistry.Get(fishId);
        if (def == null)
        {
            Plugin.Log.LogWarning($"SpriteLoader: no definition for fish ID {fishId}");
            return null;
        }

        var path = def.ImagePath;
        if (!File.Exists(path))
        {
            Plugin.Log.LogWarning($"No image found for '{def.Name}' at {path}");
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, bytes);
            tex.filterMode = FilterMode.Point;

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            return sprite;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to load sprite for '{def.Name}': {ex.Message}");
            return null;
        }
    }
}
