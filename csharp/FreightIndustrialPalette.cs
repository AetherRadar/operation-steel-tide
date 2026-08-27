using Godot;

namespace OperationSteelTide;

/// <summary>Regrades Kenney industrial buildings for the freight terminal without changing shared assets.</summary>
internal sealed class FreightIndustrialPalette
{
    public const string PaletteId = "weathered_harbor";

    private const string ColormapPath =
        "res://assets/models/kenney_city_kit_industrial/Textures/colormap.png";

    private readonly ShaderMaterial? _material;

    public FreightIndustrialPalette()
    {
        var colormap = GD.Load<Texture2D>(ColormapPath);
        if (colormap is null)
        {
            GD.PushError($"Freight industrial palette is missing its colormap: {ColormapPath}");
            return;
        }

        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode depth_draw_opaque;

uniform sampler2D albedo_texture : source_color, filter_linear;
uniform vec4 concrete_light : source_color = vec4(0.55, 0.49, 0.40, 1.0);
uniform vec4 concrete_mid : source_color = vec4(0.39, 0.35, 0.29, 1.0);
uniform vec4 iron_trim : source_color = vec4(0.105, 0.11, 0.095, 1.0);
uniform vec4 oxide_light : source_color = vec4(0.46, 0.27, 0.15, 1.0);
uniform vec4 oxide_dark : source_color = vec4(0.21, 0.13, 0.085, 1.0);
uniform vec4 glass_color : source_color = vec4(0.075, 0.16, 0.18, 1.0);

varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec3 source = texture(albedo_texture, UV).rgb;
    float brightest = max(source.r, max(source.g, source.b));
    float darkest = min(source.r, min(source.g, source.b));
    float chroma = brightest - darkest;
    float luminance = dot(source, vec3(0.2126, 0.7152, 0.0722));

    vec3 facade = mix(iron_trim.rgb, concrete_mid.rgb, smoothstep(0.012, 0.095, luminance));
    facade = mix(facade, concrete_light.rgb, smoothstep(0.10, 0.88, luminance));

    float blue_bias = source.b - max(source.r, source.g);
    float window_mask = smoothstep(0.22, 0.38, blue_bias)
        * smoothstep(0.16, 0.48, chroma);
    float warm_bias = source.r - source.b;
    float oxide_mask = smoothstep(0.18, 0.52, warm_bias)
        * smoothstep(0.16, 0.50, chroma)
        * (1.0 - window_mask);

    vec3 oxide = mix(oxide_dark.rgb, oxide_light.rgb, smoothstep(0.05, 0.72, luminance));
    vec3 color = mix(facade, oxide, oxide_mask * 0.82);
    color = mix(color, glass_color.rgb * mix(0.68, 1.18, luminance), window_mask);

    float base_grime = (1.0 - smoothstep(0.08, 1.55, world_position.y))
        * (1.0 - window_mask);
    float weather_variation = 0.5 + 0.5 * sin(
        world_position.x * 2.37 + world_position.z * 1.71 + world_position.y * 0.43);
    color *= 1.0 - base_grime * mix(0.10, 0.19, weather_variation);
    float mottling = sin(
        (world_position.x + world_position.z) * 1.31 + world_position.y * 0.61)
        * sin((world_position.x - world_position.z) * 0.47 - world_position.y * 1.83);
    color *= mix(0.975 + mottling * 0.025, 1.0, window_mask);

    ALBEDO = color;
    METALLIC = mix(0.035, 0.17, window_mask);
    ROUGHNESS = mix(0.82, 0.30, window_mask);
    SPECULAR = mix(0.34, 0.62, window_mask);
}"
        };
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("albedo_texture", colormap);
    }

    public int Apply(Node root)
    {
        if (_material is null)
        {
            return 0;
        }

        var visualCount = ApplyRecursive(root, _material);
        if (visualCount > 0)
        {
            root.SetMeta("freight_palette", PaletteId);
            root.SetMeta("freight_palette_visuals", visualCount);
        }
        return visualCount;
    }

    private static int ApplyRecursive(Node node, ShaderMaterial material)
    {
        var visualCount = 0;
        if (node is GeometryInstance3D visual)
        {
            visual.MaterialOverride = material;
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
}
