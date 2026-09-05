using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Loads the generated transparent inventory cutouts without depending on Godot's
/// editor import cache. This keeps fresh art usable in headless captures as well.
/// </summary>
internal static class GeneratedInventoryArt
{
    public const string ItemAtlasPath = "res://assets/ui/generated_inventory/inventory_item_atlas_v1.png";
    public const string AttachmentAtlasPath = "res://assets/ui/generated_inventory/inventory_attachment_atlas_v1.png";
    public const string HelmetPath = "res://assets/ui/generated_inventory/helmet_tactical_v1.png";
    public const string OperatorPath = "res://assets/ui/generated_inventory/operator_loadout_v1.png";

    private static readonly Dictionary<string, Vector2I> ItemCells = new()
    {
        ["clock"] = new Vector2I(0, 0),
        ["arctic_knife"] = new Vector2I(1, 0),
        ["ak47"] = new Vector2I(2, 0),
        ["gpu"] = new Vector2I(3, 0),
        ["heavy_armor"] = new Vector2I(0, 1),
        ["expedition_pack"] = new Vector2I(1, 1),
        ["adrenaline"] = new Vector2I(2, 1),
        ["trauma_kit"] = new Vector2I(3, 1),
        ["armor_plate"] = new Vector2I(0, 2),
        ["bandage"] = new Vector2I(1, 2),
        ["patrol_vest"] = new Vector2I(2, 2),
        ["ammo_762"] = new Vector2I(3, 2)
    };

    private static readonly Dictionary<string, Vector2I> AttachmentCells = new()
    {
        ["optic"] = new Vector2I(0, 0),
        ["suppressor"] = new Vector2I(1, 0),
        ["foregrip"] = new Vector2I(0, 1),
        ["magazine"] = new Vector2I(1, 1)
    };

    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Image> ImageCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Rect2> RegionCache = new(StringComparer.Ordinal);

    public static Texture2D? Load(string resourcePath)
    {
        if (TextureCache.TryGetValue(resourcePath, out var cached))
        {
            return cached;
        }

        var imported = ResourceLoader.Exists(resourcePath, "Texture2D")
            ? GD.Load<Texture2D>(resourcePath)
            : null;
        var image = imported?.GetImage() ?? Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image is null || image.IsEmpty())
        {
            GD.PushWarning($"Generated inventory art is unavailable: {resourcePath}");
            return null;
        }

        if (image.IsCompressed())
        {
            image.Decompress();
        }
        var texture = imported ?? ImageTexture.CreateFromImage(image);
        TextureCache[resourcePath] = texture;
        ImageCache[resourcePath] = image;
        return texture;
    }

    public static bool AllRequiredTexturesReadyForDiagnostics()
        => Load(ItemAtlasPath) is not null
        && Load(AttachmentAtlasPath) is not null
        && Load(HelmetPath) is not null
        && Load(OperatorPath) is not null;

    public static bool TryGetTextureRegion(string key, out Texture2D? texture, out Rect2 source)
    {
        texture = null;
        source = default;
        if (string.Equals(key, "helmet", StringComparison.Ordinal))
        {
            texture = Load(HelmetPath);
            source = texture is null
                ? default
                : new Rect2(0, 0, texture.GetWidth(), texture.GetHeight());
            source = TightenRegion(key, HelmetPath, source);
            return texture is not null;
        }

        if (ItemCells.TryGetValue(key, out var itemCell))
        {
            texture = Load(ItemAtlasPath);
            if (texture is null)
            {
                return false;
            }
            source = new Rect2(
                itemCell.X * texture.GetWidth() / 4.0f,
                itemCell.Y * texture.GetHeight() / 3.0f,
                texture.GetWidth() / 4.0f,
                texture.GetHeight() / 3.0f);
            source = TightenRegion(key, ItemAtlasPath, source);
            return true;
        }

        if (AttachmentCells.TryGetValue(key, out var attachmentCell))
        {
            texture = Load(AttachmentAtlasPath);
            if (texture is null)
            {
                return false;
            }
            source = new Rect2(
                attachmentCell.X * texture.GetWidth() / 2.0f,
                attachmentCell.Y * texture.GetHeight() / 2.0f,
                texture.GetWidth() / 2.0f,
                texture.GetHeight() / 2.0f);
            source = TightenRegion(key, AttachmentAtlasPath, source);
            return true;
        }

        return false;
    }

    private static Rect2 TightenRegion(string key, string resourcePath, Rect2 cell)
    {
        if (RegionCache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        if (!ImageCache.TryGetValue(resourcePath, out var image) || cell.Size.X <= 0 || cell.Size.Y <= 0)
        {
            return cell;
        }

        var cellPixels = new Rect2I(
            Mathf.FloorToInt(cell.Position.X),
            Mathf.FloorToInt(cell.Position.Y),
            Mathf.FloorToInt(cell.Size.X),
            Mathf.FloorToInt(cell.Size.Y));
        using var cellImage = image.GetRegion(cellPixels);
        var used = cellImage.GetUsedRect();
        var region = used.Size.X > 0 && used.Size.Y > 0
            ? new Rect2(cellPixels.Position + used.Position, used.Size)
            : cell;
        RegionCache[key] = region;
        return region;
    }
}
