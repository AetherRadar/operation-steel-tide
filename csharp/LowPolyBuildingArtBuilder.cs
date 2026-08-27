using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct LowPolyBuildingArtResult(int BatchCount, int DetailPartCount);

/// <summary>Builds batched architectural massing and material variation over gameplay shells.</summary>
internal sealed partial class LowPolyBuildingArtBuilder
{
    public const string StyleId = "architectural_lowpoly_v2";

    private const string ShaderCode = @"shader_type spatial;
render_mode depth_draw_opaque;

uniform vec4 lower_color : source_color;
uniform vec4 middle_color : source_color;
uniform vec4 upper_color : source_color;
uniform vec4 weather_color : source_color;
uniform float gradient_origin;
uniform float gradient_height;
uniform float variation_phase;
uniform float variation_strength;
varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
    float height_t = clamp((world_position.y - gradient_origin) / max(gradient_height, 0.1), 0.0, 1.0);
    vec3 color = mix(lower_color.rgb, middle_color.rgb, smoothstep(0.02, 0.58, height_t));
    color = mix(color, upper_color.rgb, smoothstep(0.44, 0.98, height_t));

    float broad_variation = sin(world_position.x * 0.055 + variation_phase) * 0.52
        + cos(world_position.z * 0.071 - variation_phase * 1.37) * 0.31
        + sin(world_position.y * 0.12 + variation_phase * 0.63) * 0.17;
    float orientation = dot(normalize(world_normal), normalize(vec3(-0.42, 0.22, 0.51)));
    float ground_weather = pow(1.0 - height_t, 3.2) * (0.18 + 0.05 * broad_variation);
    color = mix(color, weather_color.rgb, clamp(ground_weather, 0.0, 0.24));
    color *= 1.0 + broad_variation * variation_strength;
    color *= 1.0 + orientation * 0.035;
    color *= mix(0.97, 1.04, smoothstep(0.55, 0.9, world_normal.y));

    ALBEDO = color;
    METALLIC = 0.0;
    ROUGHNESS = mix(0.96, 0.82, height_t);
    SPECULAR = 0.2;
}";

    private static readonly BuildingGradient[] IndustrialGradients =
    {
        new(new(0.18f, 0.27f, 0.26f), new(0.35f, 0.45f, 0.41f), new(0.51f, 0.54f, 0.46f), new(0.10f, 0.15f, 0.14f)),
        new(new(0.30f, 0.22f, 0.14f), new(0.50f, 0.40f, 0.27f), new(0.61f, 0.53f, 0.36f), new(0.17f, 0.13f, 0.10f)),
        new(new(0.18f, 0.25f, 0.31f), new(0.34f, 0.43f, 0.48f), new(0.50f, 0.56f, 0.57f), new(0.10f, 0.14f, 0.18f)),
        new(new(0.30f, 0.19f, 0.16f), new(0.51f, 0.34f, 0.28f), new(0.60f, 0.47f, 0.37f), new(0.17f, 0.11f, 0.09f)),
        new(new(0.20f, 0.27f, 0.17f), new(0.39f, 0.46f, 0.29f), new(0.55f, 0.57f, 0.39f), new(0.12f, 0.15f, 0.09f)),
        new(new(0.14f, 0.27f, 0.29f), new(0.29f, 0.45f, 0.46f), new(0.45f, 0.58f, 0.56f), new(0.08f, 0.15f, 0.16f))
    };

    private readonly Dictionary<string, ShaderMaterial> _materials = new();
    private readonly BoxMesh _unitBox = new() { Size = Vector3.One };
    private readonly PrismMesh _unitPrism = new()
    {
        Size = Vector3.One,
        LeftToRight = 0.5f
    };
    private readonly CylinderMesh _unitHex = new()
    {
        TopRadius = 0.5f,
        BottomRadius = 0.5f,
        Height = 1.0f,
        RadialSegments = 6,
        Rings = 1
    };
    private readonly Shader _shader = new() { Code = ShaderCode };

    public ShaderMaterial IndustrialFacadeMaterial(
        string identity,
        float worldBaseY,
        float facadeHeight)
    {
        var profile = IndustrialArchitecture(identity);
        return GradientMaterial(
            $"industrial_shell_{identity}",
            IndustrialGradients[profile.PaletteIndex],
            worldBaseY,
            facadeHeight,
            StableUnit($"{identity}:shell"),
            0.075f);
    }

    public ShaderMaterial ResidentialFacadeMaterial(
        int towerIndex,
        Color accent,
        float worldBaseY,
        float facadeHeight)
    {
        var profile = ResidentialArchitecture(towerIndex);
        var gradient = ResidentialGradient(profile.PaletteIndex, accent);
        return GradientMaterial(
            $"residential_shell_{towerIndex}",
            gradient,
            worldBaseY,
            facadeHeight,
            StableUnit($"residential_{towerIndex}:shell"),
            0.082f);
    }

    public LowPolyBuildingArtResult BuildIndustrialDetails(
        Node3D parent,
        string identity,
        Vector2 footprint,
        float roofY,
        int floors,
        Color accent)
    {
        var root = new Node3D { Name = $"LowPolyArt_{identity}" };
        root.AddToGroup("low_poly_building_art");
        root.AddToGroup("low_poly_industrial_art");
        root.SetMeta("low_poly_style", StyleId);
        parent.AddChild(root);
        parent.AddToGroup("low_poly_building");
        parent.AddToGroup("low_poly_industrial_building");

        var architecture = IndustrialArchitecture(identity);
        var width = footprint.X;
        var depth = footprint.Y;
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;
        var structure = new List<Transform3D>(48);
        var massingCollision = new List<Transform3D>(16);
        var massingFacetCollision = new List<Transform3D>(4);
        AddEntranceBaseBand(
            structure,
            width,
            depth,
            0.38f,
            0.72f,
            0.34f,
            Mathf.Min(9.2f, width * 0.60f));
        var industrialCorners = new[]
        {
            new Vector2(-halfWidth - 0.22f, -halfDepth - 0.22f),
            new Vector2(halfWidth + 0.22f, -halfDepth - 0.22f),
            new Vector2(-halfWidth - 0.22f, halfDepth + 0.22f),
            new Vector2(halfWidth + 0.22f, halfDepth + 0.22f)
        };
        for (var cornerIndex = 0; cornerIndex < industrialCorners.Length; cornerIndex++)
        {
            if ((cornerIndex + architecture.MassingIndex) % industrialCorners.Length == 3)
            {
                continue;
            }
            var corner = industrialCorners[cornerIndex];
            var pierHeight = roofY * (0.62f + 0.13f * ((cornerIndex + architecture.MassingIndex) % 3));
            structure.Add(Part(
                new Vector3(corner.X, pierHeight * 0.5f, corner.Y),
                new Vector3(0.58f, pierHeight, 0.58f)));
        }
        for (var floor = 1; floor < floors; floor++)
        {
            AddPerimeterBand(
                structure,
                width,
                depth,
                roofY * floor / floors,
                0.22f,
                0.38f);
        }
        AddPerimeterBand(structure, width, depth, roofY + 0.02f, 0.34f, 0.48f);

        var accents = new List<Transform3D>(24);
        var facets = new List<Transform3D>(16);
        var frontZ = halfDepth + 0.25f;
        var finX = Mathf.Min(width * 0.28f, halfWidth - 1.2f);
        foreach (var x in new[] { -finX, finX })
        {
            accents.Add(Part(
                new Vector3(x, roofY * 0.5f, frontZ),
                new Vector3(0.48f, Mathf.Max(3.2f, roofY * 0.72f), 0.26f)));
        }
        accents.Add(Part(
            new Vector3(-halfWidth - 0.25f, roofY * 0.67f, -depth * 0.18f),
            new Vector3(0.28f, Mathf.Max(2.2f, roofY * 0.34f), Mathf.Min(5.2f, depth * 0.3f))));
        var massingCount = BuildIndustrialMassing(
            structure,
            massingCollision,
            massingFacetCollision,
            accents,
            facets,
            identity,
            architecture.MassingIndex,
            width,
            depth,
            roofY);

        var roof = new List<Transform3D>(12);
        var roofFacets = new List<Transform3D>(10);
        var utilities = new List<Transform3D>(8);
        BuildIndustrialRoofProfile(
            roof,
            roofFacets,
            utilities,
            architecture.RoofIndex,
            width,
            depth,
            roofY);
        facets.AddRange(roofFacets);

        var gradientOrigin = parent.GlobalPosition.Y;
        var shellGradient = IndustrialGradients[architecture.PaletteIndex];
        var batchCount = 0;
        batchCount += AddBatch(root, "StructuralRelief", _unitBox, GradientMaterial($"{identity}_structure", shellGradient.Darkened(0.16f), gradientOrigin, roofY, StableUnit($"{identity}:structure"), 0.062f), structure, 340.0f);
        batchCount += AddBatch(root, "AccentRelief", _unitBox, GradientMaterial($"{identity}_accent", AccentGradient(shellGradient, accent), gradientOrigin, roofY, StableUnit($"{identity}:accent"), 0.072f), accents, 300.0f);
        batchCount += AddBatch(root, "RoofProfile", _unitBox, GradientMaterial($"{identity}_roof", shellGradient.Lightened(0.035f), gradientOrigin, roofY + 3.0f, StableUnit($"{identity}:roof"), 0.052f), roof, 360.0f);
        batchCount += AddBatch(root, "FacetedVolumes", _unitPrism, GradientMaterial($"{identity}_facets", shellGradient.Lightened(0.025f), gradientOrigin, roofY + 4.0f, StableUnit($"{identity}:facets"), 0.058f), facets, 360.0f);
        batchCount += AddBatch(root, "FacetedUtilities", _unitHex, GradientMaterial($"{identity}_utilities", AccentGradient(shellGradient.Darkened(0.08f), accent.Darkened(0.12f)), gradientOrigin, roofY + 4.0f, StableUnit($"{identity}:utilities"), 0.06f), utilities, 330.0f);
        var roofCollisionCount = AddCollision(
            root,
            "IndustrialRoofCollision",
            "low_poly_roof_collision",
            roof,
            roofFacets,
            utilities);
        var massingCollisionCount = AddCollision(
            root,
            "IndustrialMassingCollision",
            "low_poly_massing_collision",
            massingCollision,
            massingFacetCollision,
            System.Array.Empty<Transform3D>());
        var detailCount = structure.Count + accents.Count + roof.Count + facets.Count + utilities.Count;
        SetMetadata(
            parent,
            "industrial",
            identity,
            architecture.MassingStyle,
            architecture.Signature,
            detailCount,
            massingCount);
        parent.SetMeta("low_poly_roof_collision_count", roofCollisionCount);
        parent.SetMeta("low_poly_massing_collision_count", massingCollisionCount);
        return new LowPolyBuildingArtResult(batchCount, detailCount);
    }

    public LowPolyBuildingArtResult BuildResidentialDetails(
        Node3D parent,
        ResidentialTowerDiversityProfile profile,
        Vector2 footprint,
        float roofY,
        Color accent)
    {
        var root = new Node3D { Name = "LowPolyResidentialArt" };
        root.AddToGroup("low_poly_building_art");
        root.AddToGroup("low_poly_residential_art");
        root.SetMeta("low_poly_style", StyleId);
        parent.AddChild(root);
        parent.AddToGroup("low_poly_building");
        parent.AddToGroup("low_poly_residential_building");

        var architecture = ResidentialArchitecture(profile.TowerIndex);
        var width = footprint.X;
        var depth = footprint.Y;
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;
        var structure = new List<Transform3D>(52);
        var massingCollision = new List<Transform3D>(18);
        var massingFacetCollision = new List<Transform3D>(4);
        AddEntranceBaseBand(
            structure,
            width,
            depth,
            0.42f,
            0.78f,
            0.36f,
            Mathf.Min(5.2f, width * 0.42f));
        var residentialCorners = new[]
        {
            new Vector2(-halfWidth - 0.24f, -halfDepth - 0.24f),
            new Vector2(halfWidth + 0.24f, -halfDepth - 0.24f),
            new Vector2(-halfWidth - 0.24f, halfDepth + 0.24f),
            new Vector2(halfWidth + 0.24f, halfDepth + 0.24f)
        };
        for (var cornerIndex = 0; cornerIndex < residentialCorners.Length; cornerIndex++)
        {
            if ((cornerIndex + architecture.MassingIndex) % 4 == 2)
            {
                continue;
            }
            var corner = residentialCorners[cornerIndex];
            var pierHeight = roofY * (0.58f + 0.12f * ((cornerIndex + profile.TowerIndex) % 3));
            var pierBase = roofY * (((cornerIndex + profile.TowerIndex) % 2) * 0.08f);
            structure.Add(Part(
                new Vector3(corner.X, pierBase + pierHeight * 0.5f, corner.Y),
                new Vector3(0.62f, pierHeight, 0.62f)));
        }
        AddPerimeterBand(structure, width, depth, roofY - 0.28f, 0.42f, 0.52f);
        AddPerimeterBand(structure, width, depth, roofY + 0.18f, 0.20f, 0.68f);

        var accents = new List<Transform3D>(28);
        var facets = new List<Transform3D>(18);
        var massingCount = BuildResidentialMassing(
            structure,
            massingCollision,
            massingFacetCollision,
            accents,
            facets,
            architecture.MassingIndex,
            width,
            depth,
            roofY,
            profile.TowerIndex);
        BuildResidentialFacadeProfile(accents, profile.Facade, width, depth, roofY);
        var roof = new List<Transform3D>(18);
        var roofFacets = new List<Transform3D>(10);
        var utilities = new List<Transform3D>(8);
        BuildResidentialRoofProfile(
            roof,
            roofFacets,
            utilities,
            profile.Roof,
            width,
            depth,
            roofY,
            profile.TowerIndex);
        facets.AddRange(roofFacets);

        var gradientOrigin = parent.GlobalPosition.Y;
        var shellGradient = ResidentialGradient(architecture.PaletteIndex, accent);
        var batchCount = 0;
        batchCount += AddBatch(root, "ResidentialMassing", _unitBox, GradientMaterial($"residential_structure_{profile.TowerIndex}", shellGradient.Darkened(0.12f), gradientOrigin, roofY, StableUnit($"residential_{profile.TowerIndex}:structure"), 0.068f), structure, 330.0f);
        batchCount += AddBatch(root, "ResidentialRecessBands", _unitBox, GradientMaterial($"residential_accent_{profile.TowerIndex}", RecessGradient(shellGradient, accent), gradientOrigin, roofY, StableUnit($"residential_{profile.TowerIndex}:accent"), 0.058f), accents, 300.0f);
        batchCount += AddBatch(root, "ResidentialRoofSilhouette", _unitBox, GradientMaterial($"residential_roof_{profile.TowerIndex}", shellGradient.Lightened(0.045f), gradientOrigin, roofY + 4.0f, StableUnit($"residential_{profile.TowerIndex}:roof"), 0.052f), roof, 360.0f);
        batchCount += AddBatch(root, "ResidentialFacetedVolumes", _unitPrism, GradientMaterial($"residential_facets_{profile.TowerIndex}", shellGradient.Lightened(0.028f), gradientOrigin, roofY + 5.0f, StableUnit($"residential_{profile.TowerIndex}:facets"), 0.058f), facets, 360.0f);
        batchCount += AddBatch(root, "ResidentialFacetedUtilities", _unitHex, GradientMaterial($"residential_utilities_{profile.TowerIndex}", AccentGradient(shellGradient.Darkened(0.06f), accent.Darkened(0.14f)), gradientOrigin, roofY + 5.0f, StableUnit($"residential_{profile.TowerIndex}:utilities"), 0.06f), utilities, 330.0f);
        var roofCollisionCount = AddCollision(
            root,
            "ResidentialRoofCollision",
            "low_poly_roof_collision",
            roof,
            roofFacets,
            utilities);
        var massingCollisionCount = AddCollision(
            root,
            "ResidentialMassingCollision",
            "low_poly_massing_collision",
            massingCollision,
            massingFacetCollision,
            System.Array.Empty<Transform3D>());
        var detailCount = structure.Count + accents.Count + roof.Count + facets.Count + utilities.Count;
        var architectureSignature = $"{architecture.Signature}:{profile.Facade}:{profile.Roof}";
        SetMetadata(
            parent,
            "residential",
            profile.Signature,
            architecture.MassingStyle,
            architectureSignature,
            detailCount,
            massingCount);
        parent.SetMeta("low_poly_roof_style", profile.Roof.ToString());
        parent.SetMeta("low_poly_roof_collision_count", roofCollisionCount);
        parent.SetMeta("low_poly_massing_collision_count", massingCollisionCount);
        return new LowPolyBuildingArtResult(batchCount, detailCount);
    }

    private ShaderMaterial GradientMaterial(
        string id,
        BuildingGradient gradient,
        float gradientOrigin,
        float gradientHeight,
        float phase,
        float variationStrength)
    {
        if (_materials.TryGetValue(id, out var cached))
        {
            return cached;
        }
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("lower_color", gradient.Lower);
        material.SetShaderParameter("middle_color", gradient.Middle);
        material.SetShaderParameter("upper_color", gradient.Upper);
        material.SetShaderParameter("weather_color", gradient.Weather);
        material.SetShaderParameter("gradient_origin", gradientOrigin);
        material.SetShaderParameter("gradient_height", Mathf.Max(gradientHeight, 0.1f));
        material.SetShaderParameter("variation_phase", phase * Mathf.Tau);
        material.SetShaderParameter("variation_strength", variationStrength);
        material.SetMeta("low_poly_gradient", true);
        material.SetMeta("low_poly_gradient_height", gradientHeight);
        _materials[id] = material;
        return material;
    }

    private int AddBatch(
        Node3D parent,
        string name,
        Mesh mesh,
        Godot.Material material,
        IReadOnlyList<Transform3D> transforms,
        float visibilityRange)
    {
        if (transforms.Count == 0)
        {
            return 0;
        }
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = transforms.Count,
            Mesh = mesh
        };
        for (var index = 0; index < transforms.Count; index++)
        {
            multiMesh.SetInstanceTransform(index, transforms[index]);
        }
        var visual = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            VisibilityRangeEnd = visibilityRange,
            VisibilityRangeEndMargin = 20.0f
        };
        visual.AddToGroup("low_poly_building_visuals");
        visual.SetMeta("low_poly_style", StyleId);
        visual.SetMeta("low_poly_max_visibility_range", visibilityRange);
        parent.AddChild(visual);
        return 1;
    }

    private static Transform3D Part(Vector3 position, Vector3 size, Vector3 rotation = default)
        => new(Basis.FromEuler(rotation).ScaledLocal(size), position);

    private static void SetMetadata(
        Node3D parent,
        string kind,
        string profile,
        string massingStyle,
        string architectureSignature,
        int detailCount,
        int massingCount)
    {
        parent.SetMeta("low_poly_style", StyleId);
        parent.SetMeta("low_poly_kind", kind);
        parent.SetMeta("low_poly_profile", profile);
        parent.SetMeta("low_poly_massing_style", massingStyle);
        parent.SetMeta("low_poly_architecture_signature", architectureSignature);
        parent.SetMeta("low_poly_detail_count", detailCount);
        parent.SetMeta("low_poly_massing_count", massingCount);
        parent.SetMeta("low_poly_gradient", true);
    }

    private static int StableVariant(string identity, int count)
        => (int)(StableHash(identity) % (uint)count);

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
}
