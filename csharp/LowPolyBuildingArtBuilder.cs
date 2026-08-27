using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct LowPolyBuildingArtResult(int BatchCount, int DetailPartCount);

/// <summary>Builds a batched, collision-neutral low-poly art layer over existing gameplay shells.</summary>
internal sealed class LowPolyBuildingArtBuilder
{
    public const string StyleId = "faceted_lowpoly_v1";

    private const string ShaderCode = @"shader_type spatial;
render_mode depth_draw_opaque;

uniform vec4 base_color : source_color;
varying vec3 world_normal;

void vertex() {
    world_normal = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
    float facing = dot(normalize(world_normal), normalize(vec3(-0.48, 0.78, 0.39)));
    float facet_step = floor(clamp(facing * 0.5 + 0.5, 0.0, 0.999) * 4.0) / 3.0;
    float roof_lift = step(0.64, world_normal.y) * 0.06;
    ALBEDO = base_color.rgb * (mix(0.78, 1.04, facet_step) + roof_lift);
    METALLIC = 0.0;
    ROUGHNESS = 0.96;
    SPECULAR = 0.18;
}";

    private static readonly Color[] IndustrialShellColors =
    {
        new(0.37f, 0.42f, 0.39f),
        new(0.43f, 0.36f, 0.27f),
        new(0.29f, 0.40f, 0.37f),
        new(0.40f, 0.31f, 0.29f)
    };

    private readonly Dictionary<string, ShaderMaterial> _materials = new();
    private readonly BoxMesh _unitBox = new() { Size = Vector3.One };
    private readonly Shader _shader = new() { Code = ShaderCode };

    public ShaderMaterial IndustrialFacadeMaterial(string identity)
    {
        var variant = StableVariant(identity, IndustrialShellColors.Length);
        return Material($"industrial_shell_{variant}", IndustrialShellColors[variant]);
    }

    public ShaderMaterial ResidentialFacadeMaterial(int towerIndex, Color accent)
    {
        var neutral = (towerIndex % 3) switch
        {
            0 => new Color(0.40f, 0.40f, 0.36f),
            1 => new Color(0.35f, 0.40f, 0.38f),
            _ => new Color(0.40f, 0.35f, 0.33f)
        };
        return Material($"residential_shell_{towerIndex}", neutral.Lerp(accent.Darkened(0.24f), 0.12f));
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

        var width = footprint.X;
        var depth = footprint.Y;
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;
        var structure = new List<Transform3D>(24);
        AddEntranceBaseBand(
            structure,
            width,
            depth,
            0.38f,
            0.72f,
            0.34f,
            Mathf.Min(9.2f, width * 0.60f));
        foreach (var x in new[] { -halfWidth - 0.22f, halfWidth + 0.22f })
        {
            foreach (var z in new[] { -halfDepth - 0.22f, halfDepth + 0.22f })
            {
                structure.Add(Part(
                    new Vector3(x, roofY * 0.5f, z),
                    new Vector3(0.58f, roofY, 0.58f)));
            }
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

        var accents = new List<Transform3D>(10);
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

        var roof = new List<Transform3D>(8);
        BuildIndustrialRoofProfile(
            roof,
            StableVariant(identity, IndustrialShellColors.Length),
            width,
            depth,
            roofY);

        var shellColor = IndustrialShellColors[StableVariant(identity, IndustrialShellColors.Length)];
        var batchCount = 0;
        batchCount += AddBatch(root, "StructuralRelief", Material($"{identity}_structure", shellColor.Darkened(0.24f)), structure, 340.0f);
        batchCount += AddBatch(root, "AccentRelief", Material($"{identity}_accent", accent.Darkened(0.16f)), accents, 300.0f);
        batchCount += AddBatch(root, "RoofProfile", Material($"{identity}_roof", shellColor.Lightened(0.08f)), roof, 360.0f);
        var roofCollisionCount = AddRoofCollision(root, "IndustrialRoofCollision", roof);
        var detailCount = structure.Count + accents.Count + roof.Count;
        SetMetadata(parent, "industrial", identity, detailCount);
        parent.SetMeta("low_poly_roof_collision_count", roofCollisionCount);
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

        var width = footprint.X;
        var depth = footprint.Y;
        var halfWidth = width * 0.5f;
        var halfDepth = depth * 0.5f;
        var structure = new List<Transform3D>(30);
        AddEntranceBaseBand(
            structure,
            width,
            depth,
            0.42f,
            0.78f,
            0.36f,
            Mathf.Min(5.2f, width * 0.42f));
        foreach (var x in new[] { -halfWidth - 0.24f, halfWidth + 0.24f })
        {
            foreach (var z in new[] { -halfDepth - 0.24f, halfDepth + 0.24f })
            {
                structure.Add(Part(
                    new Vector3(x, roofY * 0.5f, z),
                    new Vector3(0.62f, roofY, 0.62f)));
            }
        }
        AddPerimeterBand(structure, width, depth, roofY - 0.28f, 0.42f, 0.52f);
        AddPerimeterBand(structure, width, depth, roofY + 0.18f, 0.20f, 0.68f);

        var accents = new List<Transform3D>(18);
        BuildResidentialFacadeProfile(accents, profile.Facade, width, depth, roofY);
        var roof = new List<Transform3D>(16);
        BuildResidentialRoofProfile(roof, profile.Roof, width, depth, roofY, profile.TowerIndex);

        var shell = new Color(0.32f, 0.35f, 0.34f).Lerp(accent.Darkened(0.48f), 0.28f);
        var batchCount = 0;
        batchCount += AddBatch(root, "ResidentialMassing", Material($"residential_structure_{profile.TowerIndex}", shell), structure, 330.0f);
        var mutedAccent = accent.Darkened(0.18f).Lerp(shell, 0.34f);
        batchCount += AddBatch(root, "ResidentialColorBlocks", Material($"residential_accent_{profile.TowerIndex}", mutedAccent), accents, 280.0f);
        batchCount += AddBatch(root, "ResidentialRoofSilhouette", Material($"residential_roof_{profile.TowerIndex}", shell.Lightened(0.12f)), roof, 360.0f);
        var roofCollisionCount = AddRoofCollision(root, "ResidentialRoofCollision", roof);
        var detailCount = structure.Count + accents.Count + roof.Count;
        SetMetadata(parent, "residential", profile.Signature, detailCount);
        parent.SetMeta("low_poly_roof_style", profile.Roof.ToString());
        parent.SetMeta("low_poly_roof_collision_count", roofCollisionCount);
        return new LowPolyBuildingArtResult(batchCount, detailCount);
    }

    private static void BuildIndustrialRoofProfile(
        List<Transform3D> parts,
        int variant,
        float width,
        float depth,
        float roofY)
    {
        switch (variant)
        {
            case 0:
                for (var index = -1; index <= 1; index++)
                {
                    parts.Add(Part(
                        new Vector3(index * width * 0.22f, roofY + 0.7f, -depth * 0.16f),
                        new Vector3(width * 0.14f, 1.05f + (index == 0 ? 0.30f : 0), depth * 0.34f)));
                }
                break;
            case 1:
                parts.Add(Part(new Vector3(0, roofY + 0.34f, -depth * 0.13f), new Vector3(width * 0.44f, 0.62f, depth * 0.38f)));
                parts.Add(Part(new Vector3(0, roofY + 0.78f, -depth * 0.13f), new Vector3(width * 0.28f, 0.42f, depth * 0.24f)));
                break;
            case 2:
                for (var index = -2; index <= 2; index++)
                {
                    parts.Add(Part(
                        new Vector3(index * width * 0.17f, roofY + 0.52f, -depth * 0.12f),
                        new Vector3(width * 0.16f, 0.20f, depth * 0.44f),
                        new Vector3(0, 0, index % 2 == 0 ? 0.16f : -0.16f)));
                }
                break;
            default:
                parts.Add(Part(new Vector3(-width * 0.12f, roofY + 0.56f, -depth * 0.16f), new Vector3(width * 0.35f, 0.92f, depth * 0.30f)));
                parts.Add(Part(new Vector3(width * 0.27f, roofY + 0.84f, -depth * 0.20f), new Vector3(width * 0.10f, 1.46f, depth * 0.14f)));
                break;
        }
    }

    private static void BuildResidentialFacadeProfile(
        List<Transform3D> parts,
        ResidentialFacadeStyle style,
        float width,
        float depth,
        float roofY)
    {
        var front = depth * 0.5f + 0.24f;
        var rear = -depth * 0.5f - 0.24f;
        switch (style)
        {
            case ResidentialFacadeStyle.RibbonGlass:
                foreach (var y in new[] { roofY * 0.34f, roofY * 0.67f })
                {
                    parts.Add(Part(new Vector3(0, y, front), new Vector3(width * 0.72f, 0.48f, 0.28f)));
                    parts.Add(Part(new Vector3(0, y, rear), new Vector3(width * 0.72f, 0.48f, 0.28f)));
                }
                break;
            case ResidentialFacadeStyle.VerticalBays:
                foreach (var x in new[] { -width * 0.29f, width * 0.29f })
                {
                    parts.Add(Part(new Vector3(x, roofY * 0.5f, front), new Vector3(0.54f, roofY * 0.84f, 0.28f)));
                    parts.Add(Part(new Vector3(x, roofY * 0.5f, rear), new Vector3(0.54f, roofY * 0.84f, 0.28f)));
                }
                break;
            case ResidentialFacadeStyle.StaggeredGrid:
                parts.Add(Part(new Vector3(-width * 0.28f, roofY * 0.34f, front), new Vector3(width * 0.24f, roofY * 0.22f, 0.28f)));
                parts.Add(Part(new Vector3(width * 0.28f, roofY * 0.66f, front), new Vector3(width * 0.24f, roofY * 0.22f, 0.28f)));
                parts.Add(Part(new Vector3(0, roofY * 0.5f, rear), new Vector3(width * 0.16f, roofY * 0.54f, 0.28f)));
                break;
            case ResidentialFacadeStyle.ServiceBands:
                for (var index = 1; index <= 3; index++)
                {
                    var y = roofY * index * 0.25f;
                    parts.Add(Part(new Vector3(0, y, front), new Vector3(width * 0.58f, 0.7f, 0.3f)));
                }
                break;
            case ResidentialFacadeStyle.TerracedWindows:
                for (var level = 0; level < 3; level++)
                {
                    var side = level % 2 == 0 ? -1.0f : 1.0f;
                    parts.Add(Part(
                        new Vector3(side * width * 0.27f, roofY * (0.28f + level * 0.23f), front),
                        new Vector3(width * (0.18f + level * 0.035f), 0.62f, 0.34f)));
                }
                break;
            default:
                foreach (var x in new[] { -width * 0.34f, 0.0f, width * 0.34f })
                {
                    parts.Add(Part(new Vector3(x, roofY * 0.54f, front), new Vector3(0.46f, roofY * 0.62f, 0.28f)));
                }
                break;
        }
    }

    private static void BuildResidentialRoofProfile(
        List<Transform3D> parts,
        ResidentialRoofStyle style,
        float width,
        float depth,
        float roofY,
        int towerIndex)
    {
        var mirror = towerIndex % 2 == 0 ? -1.0f : 1.0f;
        switch (style)
        {
            case ResidentialRoofStyle.GardenServices:
                foreach (var x in new[] { -width * 0.32f, width * 0.32f })
                {
                    parts.Add(Part(new Vector3(x, roofY + 0.82f, depth * 0.5f + 0.26f), new Vector3(width * 0.18f, 1.25f, 0.62f)));
                }
                parts.Add(Part(new Vector3(0, roofY + 1.42f, depth * 0.5f + 0.35f), new Vector3(width * 0.78f, 0.18f, 0.8f)));
                break;
            case ResidentialRoofStyle.ClinicMechanical:
                for (var index = -2; index <= 2; index++)
                {
                    parts.Add(Part(new Vector3(index * width * 0.14f, roofY + 0.9f, -depth * 0.5f - 0.32f), new Vector3(width * 0.08f, 1.45f, 0.74f)));
                }
                break;
            case ResidentialRoofStyle.MarketCanopy:
                parts.Add(Part(new Vector3(-width * 0.2f, roofY + 0.94f, depth * 0.5f + 1.1f), new Vector3(width * 0.42f, 0.18f, 2.3f), new Vector3(0, 0, 0.13f)));
                parts.Add(Part(new Vector3(width * 0.2f, roofY + 0.94f, depth * 0.5f + 1.1f), new Vector3(width * 0.42f, 0.18f, 2.3f), new Vector3(0, 0, -0.13f)));
                break;
            case ResidentialRoofStyle.WorkshopPlant:
                for (var index = -2; index <= 2; index++)
                {
                    parts.Add(Part(
                        new Vector3(index * width * 0.16f, roofY + 0.66f, -depth * 0.62f + 0.05f),
                        new Vector3(width * 0.17f, 0.22f, depth * 0.24f),
                        new Vector3(0, 0, index % 2 == 0 ? 0.18f : -0.18f)));
                }
                break;
            case ResidentialRoofStyle.ShelterCrown:
                parts.Add(Part(new Vector3(0, roofY + 0.48f, -depth * 0.5f - 0.35f), new Vector3(width * 0.72f, 0.72f, 0.8f)));
                parts.Add(Part(new Vector3(0, roofY + 0.98f, -depth * 0.5f - 0.28f), new Vector3(width * 0.46f, 0.34f, 0.66f)));
                break;
            default:
                parts.Add(Part(new Vector3(mirror * (width * 0.5f + 0.21f), roofY + 1.2f, -depth * 0.18f), new Vector3(0.52f, 2.1f, depth * 0.28f)));
                parts.Add(Part(new Vector3(mirror * (width * 0.58f - 0.05f), roofY + 2.1f, -depth * 0.18f), new Vector3(width * 0.16f, 0.22f, depth * 0.28f)));
                break;
        }
    }

    private static void AddPerimeterBand(
        List<Transform3D> parts,
        float width,
        float depth,
        float y,
        float height,
        float projection)
    {
        parts.Add(Part(new Vector3(0, y, -depth * 0.5f - projection * 0.5f), new Vector3(width + projection * 2.0f, height, projection)));
        parts.Add(Part(new Vector3(0, y, depth * 0.5f + projection * 0.5f), new Vector3(width + projection * 2.0f, height, projection)));
        parts.Add(Part(new Vector3(-width * 0.5f - projection * 0.5f, y, 0), new Vector3(projection, height, depth)));
        parts.Add(Part(new Vector3(width * 0.5f + projection * 0.5f, y, 0), new Vector3(projection, height, depth)));
    }

    private static void AddEntranceBaseBand(
        List<Transform3D> parts,
        float width,
        float depth,
        float y,
        float height,
        float projection,
        float openingWidth)
    {
        var halfDepth = depth * 0.5f;
        var totalWidth = width + projection * 2.0f;
        var clampedOpening = Mathf.Clamp(openingWidth, 0.0f, totalWidth - projection * 2.0f);
        var sideWidth = (totalWidth - clampedOpening) * 0.5f;
        var sideOffset = clampedOpening * 0.5f + sideWidth * 0.5f;
        parts.Add(Part(
            new Vector3(0, y, -halfDepth - projection * 0.5f),
            new Vector3(totalWidth, height, projection)));
        parts.Add(Part(
            new Vector3(-sideOffset, y, halfDepth + projection * 0.5f),
            new Vector3(sideWidth, height, projection)));
        parts.Add(Part(
            new Vector3(sideOffset, y, halfDepth + projection * 0.5f),
            new Vector3(sideWidth, height, projection)));
        parts.Add(Part(
            new Vector3(-width * 0.5f - projection * 0.5f, y, 0),
            new Vector3(projection, height, depth)));
        parts.Add(Part(
            new Vector3(width * 0.5f + projection * 0.5f, y, 0),
            new Vector3(projection, height, depth)));
    }

    private ShaderMaterial Material(string id, Color color)
    {
        if (_materials.TryGetValue(id, out var cached))
        {
            return cached;
        }
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("base_color", color);
        _materials[id] = material;
        return material;
    }

    private int AddBatch(
        Node3D parent,
        string name,
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
            Mesh = _unitBox
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

    private static int AddRoofCollision(
        Node3D parent,
        string name,
        IReadOnlyList<Transform3D> transforms)
    {
        var body = new StaticBody3D
        {
            Name = name,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup("low_poly_roof_collision");
        parent.AddChild(body);
        for (var index = 0; index < transforms.Count; index++)
        {
            var transform = transforms[index];
            body.AddChild(new CollisionShape3D
            {
                Name = $"RoofShape{index:00}",
                Transform = new Transform3D(transform.Basis.Orthonormalized(), transform.Origin),
                Shape = new BoxShape3D { Size = transform.Basis.Scale.Abs() }
            });
        }
        return transforms.Count;
    }

    private static Transform3D Part(Vector3 position, Vector3 size, Vector3 rotation = default)
        => new(Basis.FromEuler(rotation).ScaledLocal(size), position);

    private static void SetMetadata(Node3D parent, string kind, string profile, int detailCount)
    {
        parent.SetMeta("low_poly_style", StyleId);
        parent.SetMeta("low_poly_kind", kind);
        parent.SetMeta("low_poly_profile", profile);
        parent.SetMeta("low_poly_detail_count", detailCount);
    }

    private static int StableVariant(string identity, int count)
    {
        var hash = 2166136261u;
        foreach (var character in identity)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return (int)(hash % (uint)count);
    }
}
