using Godot;

namespace OperationSteelTide;

internal static class OceanBackdropFactory
{
    private const float BackdropSizeMeters = 1600.0f;

    public static MeshInstance3D Create(Vector3 position)
    {
        return new MeshInstance3D
        {
            Name = "OceanBackdrop",
            Position = position,
            Mesh = new PlaneMesh
            {
                Size = new Vector2(BackdropSizeMeters, BackdropSizeMeters),
                SubdivideWidth = 64,
                SubdivideDepth = 64
            },
            MaterialOverride = BuildMaterial(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 128.0f
        };
    }

    private static ShaderMaterial BuildMaterial()
    {
        return new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode cull_disabled, depth_draw_opaque;

uniform vec4 deep_color : source_color = vec4(0.012, 0.075, 0.105, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.055, 0.24, 0.28, 1.0);
uniform vec4 foam_color : source_color = vec4(0.28, 0.58, 0.61, 1.0);

float wave_field(vec2 point) {
    float first = sin(point.x * 2.1 + point.y * 1.7);
    float second = cos(point.x * 3.7 - point.y * 2.6);
    float third = sin(point.x * 7.4 + point.y * 5.2);
    return first * 0.48 + second * 0.34 + third * 0.18;
}

void fragment() {
    vec2 flow = UV * 58.0 + vec2(TIME * 0.018, -TIME * 0.011);
    float broad = wave_field(flow * 0.17) * 0.5 + 0.5;
    float detail = wave_field(flow * 0.74 + vec2(4.0, -2.5)) * 0.5 + 0.5;
    float ripples = clamp(broad * 0.66 + detail * 0.34, 0.0, 1.0);
    float foam = smoothstep(0.76, 0.96, detail) * smoothstep(0.42, 0.72, broad);
    float fresnel = pow(1.0 - max(dot(normalize(NORMAL), normalize(VIEW)), 0.0), 3.0);
    vec3 water = mix(deep_color.rgb, shallow_color.rgb, ripples * 0.58 + fresnel * 0.22);
    water = mix(water, foam_color.rgb, foam * 0.22);
    ALBEDO = water;
    METALLIC = 0.68;
    ROUGHNESS = 0.14;
    SPECULAR = 0.88;
    EMISSION = shallow_color.rgb * (0.035 + ripples * 0.025);
}"
            }
        };
    }
}
