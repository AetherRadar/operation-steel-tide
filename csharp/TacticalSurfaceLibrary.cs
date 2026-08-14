using Godot;

namespace OperationSteelTide;

internal static class TacticalSurfaceLibrary
{
    private static Texture2D? _fabricWeaveTexture;
    private static Texture2D? _weaponFinishTexture;

    public static StandardMaterial3D Fabric(Color color, float roughness = 0.94f, float textureScale = 7.0f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            AlbedoTexture = FabricWeaveTexture(),
            Metallic = 0.0f,
            Roughness = roughness,
            Uv1Scale = Vector3.One * textureScale,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
    }

    public static StandardMaterial3D WeaponFinish(
        Color color,
        float metallic,
        float roughness,
        float textureScale = 4.5f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            AlbedoTexture = WeaponFinishTexture(),
            Metallic = metallic,
            Roughness = roughness,
            Uv1Scale = Vector3.One * textureScale,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
    }

    private static Texture2D FabricWeaveTexture()
    {
        if (_fabricWeaveTexture is not null)
        {
            return _fabricWeaveTexture;
        }

        var image = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8);
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var warp = (x / 2) % 4;
                var weft = (y / 2) % 4;
                var overUnder = ((warp + weft) & 1) == 0 ? 1.0f : 0.86f;
                var fiberHighlight = (x % 4 == 1 || y % 4 == 1) ? 1.03f : 0.94f;
                var value = Mathf.Clamp(overUnder * fiberHighlight, 0.78f, 1.0f);
                image.SetPixel(x, y, new Color(value, value, value, 1.0f));
            }
        }

        _fabricWeaveTexture = ImageTexture.CreateFromImage(image);
        return _fabricWeaveTexture;
    }

    private static Texture2D WeaponFinishTexture()
    {
        if (_weaponFinishTexture is not null)
        {
            return _weaponFinishTexture;
        }

        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var grain = ((x * 17 + y * 31 + x * y * 7) % 23) / 22.0f;
                var brushed = (x + y * 3) % 29 == 0 ? 0.77f : 0.9f + grain * 0.1f;
                var edgeWear = (x % 16 == 0 || y % 21 == 0) ? 1.0f : brushed;
                var value = Mathf.Clamp(edgeWear, 0.76f, 1.0f);
                image.SetPixel(x, y, new Color(value, value, value, 1.0f));
            }
        }

        _weaponFinishTexture = ImageTexture.CreateFromImage(image);
        return _weaponFinishTexture;
    }
}
