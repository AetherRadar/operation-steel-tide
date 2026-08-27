using Godot;

namespace OperationSteelTide;

/// <summary>Regrades CC0 Kenney industrial buildings into a deterministic, faceted harbor palette.</summary>
internal sealed class FreightIndustrialPalette
{
    public const string PaletteId = "faceted_harbor_v2";
    public const int VariantCount = 4;

    private const string ColormapPath =
        "res://assets/models/kenney_city_kit_industrial/Textures/colormap.png";

    private const string ShaderCode = @"shader_type spatial;
render_mode depth_draw_opaque;

uniform sampler2D albedo_texture : source_color, filter_nearest_mipmap;
uniform vec4 facade_light : source_color;
uniform vec4 facade_mid : source_color;
uniform vec4 facade_shadow : source_color;
uniform vec4 trim_color : source_color;
uniform vec4 accent_light : source_color;
uniform vec4 accent_dark : source_color;
uniform vec4 glass_color : source_color;

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

    float facing = dot(normalize(world_normal), normalize(vec3(-0.42, 0.76, 0.49)));
    float facet_step = floor(clamp(facing * 0.5 + 0.5, 0.0, 0.999) * 4.0) / 3.0;
    vec3 facade = mix(facade_shadow.rgb, facade_light.rgb, facet_step);
    facade = mix(facade, facade_mid.rgb, 0.32 + 0.34 * step(0.42, luminance));

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

    float story_index = mod(floor(max(world_position.y, 0.0) * 0.42), 3.0);
    float story_tone = story_index < 0.5 ? 0.94 : (story_index < 1.5 ? 1.0 : 0.97);
    facade *= story_tone;

    vec3 accent = mix(accent_dark.rgb, accent_light.rgb, step(0.46, luminance));
    vec3 color = mix(facade, trim_color.rgb, dark_mask * 0.92);
    color = mix(color, accent, max(warm_mask, utility_mask * 0.7));
    color = mix(color, glass_color.rgb * mix(0.72, 1.08, facet_step), window_mask);

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
            new Color(0.085f, 0.18f, 0.22f))
    };

    private readonly ShaderMaterial?[] _materials = new ShaderMaterial?[VariantCount];
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
        var material = MaterialFor(variantIndex);
        var visualCount = ApplyRecursive(root, material);
        if (visualCount > 0)
        {
            root.SetMeta("freight_palette", PaletteId);
            root.SetMeta("freight_palette_variant", variantIndex);
            root.SetMeta("freight_palette_visuals", visualCount);
            root.SetMeta("low_poly_building", true);
        }
        return visualCount;
    }

    private ShaderMaterial MaterialFor(int variantIndex)
    {
        if (_materials[variantIndex] is { } cached)
        {
            return cached;
        }

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
        _materials[variantIndex] = material;
        return material;
    }

    private static int StableVariant(string identity)
    {
        var hash = 2166136261u;
        foreach (var character in identity)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return (int)(hash % VariantCount);
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
}
