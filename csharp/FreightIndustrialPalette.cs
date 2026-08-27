using Godot;

namespace OperationSteelTide;

/// <summary>Regrades CC0 Kenney industrial buildings into a deterministic, faceted harbor palette.</summary>
internal sealed class FreightIndustrialPalette
{
    public const string PaletteId = "faceted_harbor_v3";
    public const int VariantCount = 6;

    private const string ColormapPath =
        "res://assets/models/kenney_city_kit_industrial/Textures/colormap.png";

    private const string ShaderCode = @"shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform sampler2D albedo_texture : source_color, filter_nearest_mipmap;
uniform vec4 facade_light : source_color;
uniform vec4 facade_mid : source_color;
uniform vec4 facade_shadow : source_color;
uniform vec4 trim_color : source_color;
uniform vec4 accent_light : source_color;
uniform vec4 accent_dark : source_color;
uniform vec4 glass_color : source_color;
uniform float gradient_origin;
uniform float gradient_height;
uniform float variation_phase;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
    vec3 source = texture(albedo_texture, UV).rgb;
    float brightest = max(source.r, max(source.g, source.b));
    float darkest = min(source.r, min(source.g, source.b));
    float chroma = brightest - darkest;
    float luminance = dot(source, vec3(0.2126, 0.7152, 0.0722));

    float height_t = clamp((world_position.y - gradient_origin) / max(gradient_height, 0.1), 0.0, 1.0);
    vec3 facade = mix(facade_shadow.rgb, facade_mid.rgb, smoothstep(0.02, 0.58, height_t));
    facade = mix(facade, facade_light.rgb, smoothstep(0.44, 0.98, height_t));
    float broad_variation = sin(world_position.x * 0.06 + variation_phase) * 0.5
        + cos(world_position.z * 0.078 - variation_phase * 1.31) * 0.32
        + sin(world_position.y * 0.13 + variation_phase * 0.67) * 0.18;
    float orientation = dot(normalize(world_normal), normalize(vec3(-0.42, 0.2, 0.5)));
    facade *= 1.0 + broad_variation * 0.065 + orientation * 0.035;
    facade = mix(facade, facade_shadow.rgb, pow(1.0 - height_t, 3.2) * 0.16);

    float dark_mask = 1.0 - smoothstep(0.075, 0.19, luminance);
    float blue_bias = source.b - max(source.r, source.g);
    float window_mask = smoothstep(0.10, 0.28, blue_bias)
        * smoothstep(0.10, 0.42, chroma);
    float warm_bias = source.r - source.b;
    float warm_mask = smoothstep(0.08, 0.32, warm_bias)
        * smoothstep(0.10, 0.42, chroma)
        * (1.0 - window_mask);
    float green_bias = source.g - max(source.r, source.b);
    float utility_mask = smoothstep(0.08, 0.26, green_bias)
        * smoothstep(0.10, 0.38, chroma)
        * (1.0 - window_mask);

    vec3 accent = mix(accent_dark.rgb, accent_light.rgb, smoothstep(0.28, 0.68, luminance));
    vec3 color = mix(facade, trim_color.rgb, dark_mask * 0.92);
    color = mix(color, accent, max(warm_mask, utility_mask * 0.7));
    color = mix(color, glass_color.rgb * (1.0 + orientation * 0.08), window_mask);

    ALBEDO = color;
    METALLIC = mix(0.0, 0.08, max(dark_mask, window_mask));
    ROUGHNESS = mix(0.94, 0.68, window_mask);
    SPECULAR = mix(0.22, 0.38, window_mask);
}";

    private static readonly PaletteVariant[] Variants =
    {
        new(
            new Color(0.57f, 0.58f, 0.52f),
            new Color(0.40f, 0.43f, 0.40f),
            new Color(0.25f, 0.29f, 0.29f),
            new Color(0.09f, 0.12f, 0.13f),
            new Color(0.63f, 0.32f, 0.16f),
            new Color(0.29f, 0.14f, 0.09f),
            new Color(0.07f, 0.19f, 0.23f)),
        new(
            new Color(0.64f, 0.55f, 0.40f),
            new Color(0.47f, 0.41f, 0.32f),
            new Color(0.29f, 0.29f, 0.27f),
            new Color(0.11f, 0.12f, 0.12f),
            new Color(0.69f, 0.40f, 0.18f),
            new Color(0.35f, 0.19f, 0.10f),
            new Color(0.075f, 0.17f, 0.19f)),
        new(
            new Color(0.50f, 0.59f, 0.54f),
            new Color(0.35f, 0.45f, 0.42f),
            new Color(0.23f, 0.31f, 0.31f),
            new Color(0.08f, 0.12f, 0.13f),
            new Color(0.63f, 0.43f, 0.22f),
            new Color(0.30f, 0.21f, 0.13f),
            new Color(0.06f, 0.20f, 0.22f)),
        new(
            new Color(0.61f, 0.48f, 0.39f),
            new Color(0.45f, 0.36f, 0.32f),
            new Color(0.29f, 0.25f, 0.25f),
            new Color(0.12f, 0.105f, 0.11f),
            new Color(0.68f, 0.34f, 0.18f),
            new Color(0.34f, 0.15f, 0.09f),
            new Color(0.085f, 0.18f, 0.22f)),
        new(
            new Color(0.67f, 0.63f, 0.45f),
            new Color(0.47f, 0.48f, 0.31f),
            new Color(0.25f, 0.29f, 0.20f),
            new Color(0.10f, 0.12f, 0.10f),
            new Color(0.72f, 0.42f, 0.17f),
            new Color(0.35f, 0.18f, 0.08f),
            new Color(0.07f, 0.19f, 0.21f)),
        new(
            new Color(0.55f, 0.65f, 0.63f),
            new Color(0.34f, 0.47f, 0.49f),
            new Color(0.19f, 0.29f, 0.34f),
            new Color(0.07f, 0.11f, 0.14f),
            new Color(0.62f, 0.31f, 0.25f),
            new Color(0.29f, 0.13f, 0.12f),
            new Color(0.055f, 0.18f, 0.24f))
    };

    private readonly Texture2D? _colormap;
    private readonly Shader _shader = new() { Code = ShaderCode };

    public FreightIndustrialPalette()
    {
        _colormap = GD.Load<Texture2D>(ColormapPath);
        if (_colormap is null)
        {
            GD.PushError($"Freight industrial palette is missing its colormap: {ColormapPath}");
        }
    }

    public int Apply(Node root)
        => Apply(root, root.Name.ToString());

    public int Apply(Node root, string identity)
    {
        if (_colormap is null)
        {
            return 0;
        }

        var variantIndex = StableVariant(identity);
        var bounds = MeasureWorldVerticalBounds(root);
        var material = MaterialFor(variantIndex, identity, bounds);
        var visualCount = ApplyRecursive(root, material);
        if (visualCount > 0)
        {
            root.SetMeta("freight_palette", PaletteId);
            root.SetMeta("freight_palette_variant", variantIndex);
            root.SetMeta("freight_palette_visuals", visualCount);
            root.SetMeta("freight_palette_gradient", true);
            root.SetMeta("freight_palette_gradient_height", bounds.Height);
            root.SetMeta("freight_palette_color_seed", StableUnit(identity));
            root.SetMeta("low_poly_building", true);
        }
        return visualCount;
    }

    private ShaderMaterial MaterialFor(
        int variantIndex,
        string identity,
        VerticalBounds bounds)
    {
        var variant = Variants[variantIndex];
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("albedo_texture", _colormap!);
        material.SetShaderParameter("facade_light", variant.FacadeLight);
        material.SetShaderParameter("facade_mid", variant.FacadeMid);
        material.SetShaderParameter("facade_shadow", variant.FacadeShadow);
        material.SetShaderParameter("trim_color", variant.Trim);
        material.SetShaderParameter("accent_light", variant.AccentLight);
        material.SetShaderParameter("accent_dark", variant.AccentDark);
        material.SetShaderParameter("glass_color", variant.Glass);
        material.SetShaderParameter("gradient_origin", bounds.MinimumY);
        material.SetShaderParameter("gradient_height", bounds.Height);
        material.SetShaderParameter("variation_phase", StableUnit(identity) * Mathf.Tau);
        return material;
    }

    private static int StableVariant(string identity)
        => (int)(StableHash(identity) % VariantCount);

    private static float StableUnit(string identity)
        => (StableHash(identity) & 0x00ffffffu) / 16777215.0f;

    private static uint StableHash(string identity)
    {
        var hash = 2166136261u;
        foreach (var character in identity)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return hash;
    }

    private static VerticalBounds MeasureWorldVerticalBounds(Node root)
    {
        var minimumY = float.PositiveInfinity;
        var maximumY = float.NegativeInfinity;
        AccumulateWorldVerticalBounds(root, ref minimumY, ref maximumY);
        if (float.IsPositiveInfinity(minimumY) || float.IsNegativeInfinity(maximumY))
        {
            var fallbackY = root is Node3D node3D ? node3D.GlobalPosition.Y : 0.0f;
            return new VerticalBounds(fallbackY, 8.0f);
        }
        return new VerticalBounds(minimumY, Mathf.Max(maximumY - minimumY, 1.0f));
    }

    private static void AccumulateWorldVerticalBounds(
        Node node,
        ref float minimumY,
        ref float maximumY)
    {
        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
        {
            var bounds = meshInstance.GetAabb();
            for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                var corner = bounds.Position + new Vector3(
                    (cornerIndex & 1) == 0 ? 0.0f : bounds.Size.X,
                    (cornerIndex & 2) == 0 ? 0.0f : bounds.Size.Y,
                    (cornerIndex & 4) == 0 ? 0.0f : bounds.Size.Z);
                var worldCorner = meshInstance.GlobalTransform * corner;
                minimumY = Mathf.Min(minimumY, worldCorner.Y);
                maximumY = Mathf.Max(maximumY, worldCorner.Y);
            }
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                AccumulateWorldVerticalBounds(childNode, ref minimumY, ref maximumY);
            }
        }
    }

    private static int ApplyRecursive(Node node, ShaderMaterial material)
    {
        var visualCount = 0;
        if (node is GeometryInstance3D visual)
        {
            visual.MaterialOverride = material;
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visualCount++;
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                visualCount += ApplyRecursive(childNode, material);
            }
        }
        return visualCount;
    }

    private readonly record struct PaletteVariant(
        Color FacadeLight,
        Color FacadeMid,
        Color FacadeShadow,
        Color Trim,
        Color AccentLight,
        Color AccentDark,
        Color Glass);

    private readonly record struct VerticalBounds(float MinimumY, float Height);
}
