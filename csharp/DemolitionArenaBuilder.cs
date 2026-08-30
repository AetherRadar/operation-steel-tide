using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Builds generated demolition worlds from immutable layout data.</summary>
public sealed partial class DemolitionArenaBuilder
{
    private readonly Func<string, Color, float, float, Color, StandardMaterial3D> _material;
    private readonly Func<string, Color, float, StandardMaterial3D> _groundMaterial;
    private readonly Dictionary<Vector3, BoxMesh> _boxMeshes = new();
    private readonly List<StaticBody3D> _staticBodies = new();
    private readonly FreightIndustrialPalette _industrialPalette = new();
    private int _visualPartCount;
    private int _palettedBuildingCount;

    public DemolitionArenaBuilder(
        Func<string, Color, float, float, Color, StandardMaterial3D> material,
        Func<string, Color, float, StandardMaterial3D> groundMaterial)
    {
        _material = material;
        _groundMaterial = groundMaterial;
    }

    public DemolitionArenaRuntime Build(Node parent, DemolitionArenaLayout layout)
    {
        _staticBodies.Clear();
        _visualPartCount = 0;
        _palettedBuildingCount = 0;
        var root = new Node3D
        {
            Name = layout.MapId == DemolitionMapCatalog.HarborLocksId
                ? "HarborLocksArena"
                : layout.MapId == DemolitionMapCatalog.TideglassReactorId
                    ? "TideglassReactorArena"
                    : layout.MapId == DemolitionMapCatalog.BazaarCrossingId
                        ? "BazaarCrossingArena"
                        : "TideforgeArena"
        };
        parent.AddChild(root);
        var materials = BuildMaterials();

        foreach (var definition in layout.CollisionBoxes)
        {
            AddStaticBox(root, definition, MaterialFor(materials, definition.Material));
        }
        foreach (var definition in layout.TraversalBoxes)
        {
            AddStaticBox(root, definition, MaterialFor(materials, definition.Material));
        }
        foreach (var definition in layout.DetailBoxes)
        {
            AddVisualBox(root, definition, MaterialFor(materials, definition.Material));
        }
        foreach (var prop in layout.Props)
        {
            AddProp(root, prop);
        }
        var sites = BuildSites(root, layout, materials);
        BuildLandmarks(root, layout, materials);
        BuildCentralCoverDetails(root, layout, materials);
        BuildRouteGuidance(root, layout);
        BuildLighting(root, layout, materials);
        var dressing = new DemolitionArenaDressingBuilder(_industrialPalette).Build(root, layout);
        _visualPartCount += dressing.AuthoredModelCount;
        _palettedBuildingCount += dressing.PalettedBuildingCount;
        root.SetMeta("low_poly_paletted_building_count", _palettedBuildingCount);
        if (layout.MapId == DemolitionMapCatalog.TideglassReactorId)
        {
            BuildTideglassAuthoredCollision(root);
        }
        var bazaarGlassFields = BuildBazaarCrossingGlass(root, layout);
        return new DemolitionArenaRuntime(
            layout,
            root,
            sites,
            _staticBodies,
            bazaarGlassFields,
            _visualPartCount);
    }

    private Dictionary<string, StandardMaterial3D> BuildMaterials()
    {
        return new Dictionary<string, StandardMaterial3D>
        {
            ["ground"] = _groundMaterial("demolition_arena_ground", new Color(0.44f, 0.45f, 0.42f), 0.9f),
            ["concrete"] = _groundMaterial("demolition_arena_concrete", new Color(0.58f, 0.58f, 0.54f), 0.84f),
            ["concrete_dark"] = _material("demolition_arena_concrete_dark", new Color(0.12f, 0.14f, 0.135f), 0.08f, 0.88f, default),
            ["steel"] = _material("demolition_arena_steel", new Color(0.11f, 0.14f, 0.14f), 0.36f, 0.68f, default),
            ["steel_dark"] = _material("demolition_arena_steel_dark", new Color(0.027f, 0.039f, 0.04f), 0.44f, 0.64f, default),
            ["rust"] = _material("demolition_arena_rust", new Color(0.34f, 0.105f, 0.048f), 0.24f, 0.82f, default),
            ["warning"] = _material("demolition_arena_warning", new Color(0.92f, 0.5f, 0.06f), 0.16f, 0.48f, new Color(0.46f, 0.11f, 0.01f)),
            ["cyan"] = _material("demolition_arena_cyan", new Color(0.08f, 0.52f, 0.63f), 0.3f, 0.4f, new Color(0.01f, 0.14f, 0.18f)),
            ["marking"] = _material("demolition_arena_marking", new Color(0.74f, 0.77f, 0.7f), 0.03f, 0.74f, default),
            ["spawn_floor"] = _groundMaterial("demolition_arena_spawn_floor", new Color(0.19f, 0.26f, 0.25f), 0.82f),
            ["mid_floor"] = _groundMaterial("demolition_arena_mid_floor", new Color(0.28f, 0.29f, 0.27f), 0.88f),
            ["foundry_floor"] = _groundMaterial("demolition_arena_foundry_floor", new Color(0.34f, 0.21f, 0.16f), 0.9f),
            ["assembly_floor"] = _groundMaterial("demolition_arena_assembly_floor", new Color(0.12f, 0.21f, 0.23f), 0.82f),
            ["harbor_floor"] = _groundMaterial("demolition_arena_harbor_floor", new Color(0.23f, 0.29f, 0.31f), 0.9f),
            ["water"] = _material("demolition_arena_water", new Color(0.035f, 0.22f, 0.28f), 0.18f, 0.2f, new Color(0.0f, 0.045f, 0.055f)),
            ["site"] = _material("demolition_arena_site", new Color(0.09f, 0.11f, 0.105f), 0.55f, 0.45f, default),
            ["window"] = _material("demolition_arena_window", new Color(0.025f, 0.12f, 0.15f), 0.68f, 0.18f, new Color(0.01f, 0.055f, 0.07f))
        };
    }

    private static StandardMaterial3D MaterialFor(
        IReadOnlyDictionary<string, StandardMaterial3D> materials,
        string id)
        => materials.TryGetValue(id, out var material) ? material : materials["steel_dark"];

    private void AddStaticBox(Node3D root, DemolitionArenaBox definition, Godot.Material material)
    {
        var body = new StaticBody3D
        {
            Name = definition.Name,
            Position = definition.Center,
            Rotation = definition.Rotation,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        if (definition.Visible)
        {
            body.AddChild(new MeshInstance3D
            {
                Name = "Visual",
                Mesh = SharedBox(definition.Size),
                MaterialOverride = material
            });
            _visualPartCount++;
        }
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = new BoxShape3D { Size = definition.Size }
        });
        root.AddChild(body);
        _staticBodies.Add(body);
    }

    private void AddStaticCylinder(
        Node3D root,
        string name,
        Vector3 position,
        float topRadius,
        float bottomRadius,
        float height,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new MeshInstance3D
        {
            Name = "Visual",
            Mesh = new CylinderMesh
            {
                TopRadius = topRadius,
                BottomRadius = bottomRadius,
                Height = height,
                RadialSegments = 20
            },
            MaterialOverride = material
        });
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = new CylinderShape3D
            {
                Radius = Mathf.Max(topRadius, bottomRadius),
                Height = height
            }
        });
        root.AddChild(body);
        _staticBodies.Add(body);
        _visualPartCount++;
    }

    private void AddVisualBox(Node3D root, DemolitionArenaBox definition, Godot.Material material)
    {
        root.AddChild(new MeshInstance3D
        {
            Name = definition.Name,
            Position = definition.Center,
            Rotation = definition.Rotation,
            Mesh = SharedBox(definition.Size),
            MaterialOverride = material
        });
        _visualPartCount++;
    }

    private void AddProp(Node3D root, DemolitionArenaProp definition)
    {
        var body = new StaticBody3D
        {
            Name = definition.Name,
            Position = definition.Position,
            Rotation = new Vector3(0, definition.Yaw, 0),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(body);
        var scene = GD.Load<PackedScene>(definition.ScenePath);
        if (scene?.Instantiate() is Node3D model)
        {
            model.Name = "Model";
            model.Scale = Vector3.One * definition.Scale;
            body.AddChild(model);
            if (definition.ScenePath.Contains(
                    "/kenney_city_kit_industrial/building-",
                    StringComparison.Ordinal)
                && _industrialPalette.Apply(model, definition.Name) > 0)
            {
                _palettedBuildingCount++;
            }
            _visualPartCount++;
        }
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Position = definition.CollisionOffset * definition.Scale,
            Shape = new BoxShape3D { Size = definition.CollisionSize * definition.Scale }
        });
        _staticBodies.Add(body);
    }

    private IReadOnlyList<Node3D> BuildSites(
        Node3D root,
        DemolitionArenaLayout layout,
        IReadOnlyDictionary<string, StandardMaterial3D> materials)
    {
        var sites = new List<Node3D>();
        for (var index = 0; index < layout.SitePositions.Count; index++)
        {
            var letter = ((char)('A' + index)).ToString();
            var site = new Node3D
            {
                Name = $"DemolitionSite_{letter}",
                Position = layout.SitePositions[index]
            };
            root.AddChild(site);
            var siteAccent = index == 0 ? materials["warning"] : materials["cyan"];
            site.AddChild(new MeshInstance3D
            {
                Name = "SiteRing",
                Mesh = new TorusMesh { InnerRadius = 2.8f, OuterRadius = 3.08f, Rings = 40, RingSegments = 8 },
                MaterialOverride = siteAccent
            });
            site.AddChild(new MeshInstance3D
            {
                Name = "SitePlate",
                Position = new Vector3(0, 0.03f, 0),
                Mesh = SharedBox(new Vector3(4.7f, 0.06f, 4.7f)),
                MaterialOverride = materials["site"]
            });
            site.AddChild(new Label3D
            {
                Name = "SiteLabel",
                Text = letter,
                Position = new Vector3(0, 0.16f, 0),
                RotationDegrees = new Vector3(-90, 0, 0),
                FontSize = 180,
                Modulate = index == 0
                    ? new Color(1.0f, 0.58f, 0.16f)
                    : new Color(0.22f, 0.78f, 0.92f),
                OutlineSize = 8
            });
            sites.Add(site);
            _visualPartCount += 3;
        }
        return sites;
    }

    private void BuildLandmarks(
        Node3D root,
        DemolitionArenaLayout layout,
        IReadOnlyDictionary<string, StandardMaterial3D> materials)
    {
        if (layout.MapId == DemolitionMapCatalog.BazaarCrossingId)
        {
            BuildBazaarCrossingLandmarks(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.TideglassReactorId)
        {
            BuildTideglassReactorLandmarks(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.HarborLocksId)
        {
            BuildHarborLocksLandmarks(root, layout, materials);
            return;
        }
        AddSign(root, "ArenaTitle", layout.Origin + new Vector3(0, 5.2f, -54.6f), "TIDEFORGE  //  TF-07", 0, new Color(1.0f, 0.62f, 0.22f));
        AddSign(root, "FoundrySign", layout.Origin + new Vector3(-27.0f, 4.6f, 10.4f), "A  //  FOUNDRY YARD", Mathf.Pi, new Color(1.0f, 0.5f, 0.16f));
        AddSign(root, "AssemblySign", layout.Origin + new Vector3(25.0f, 4.8f, -3.4f), "B  //  ASSEMBLY HALL", Mathf.Pi, new Color(0.42f, 0.86f, 1.0f));

        for (var index = 0; index < 5; index++)
        {
            var y = 2.0f + index * 1.35f;
            var stack = new MeshInstance3D
            {
                Name = $"FoundryStack_{index + 1:00}",
                Position = layout.Origin + new Vector3(-36.5f + index * 2.6f, y, -3.5f),
                Mesh = new CylinderMesh { TopRadius = 0.48f, BottomRadius = 0.62f, Height = 4.0f + index * 0.7f, RadialSegments = 16 },
                MaterialOverride = materials[index % 2 == 0 ? "rust" : "steel_dark"]
            };
            root.AddChild(stack);
            _visualPartCount++;
        }
        for (var index = 0; index < 4; index++)
        {
            AddVisualBox(
                root,
                new DemolitionArenaBox(
                    $"AssemblyTruss_{index + 1:00}",
                    layout.Origin + new Vector3(24.0f + index * 4.4f, 6.6f, -20.0f),
                    new Vector3(0.22f, 0.35f, 22.0f),
                    "warning"),
                materials["warning"]);
        }
    }

    private void BuildCentralCoverDetails(
        Node3D root,
        DemolitionArenaLayout layout,
        IReadOnlyDictionary<string, StandardMaterial3D> materials)
    {
        if (layout.MapId == DemolitionMapCatalog.BazaarCrossingId)
        {
            BuildBazaarCrossingCoverDetails(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.TideglassReactorId)
        {
            BuildTideglassReactorCoverDetails(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.HarborLocksId)
        {
            BuildHarborLocksCoverDetails(root, layout, materials);
            return;
        }
        var converters = new[]
        {
            (Name: "West", Position: new Vector3(-6.8f, 3.72f, 17.5f), Material: "rust"),
            (Name: "East", Position: new Vector3(6.8f, 3.72f, 12.0f), Material: "steel")
        };
        foreach (var converter in converters)
        {
            AddStaticCylinder(
                root,
                $"MidConverter{converter.Name}Drum",
                layout.Origin + converter.Position,
                1.38f,
                1.55f,
                1.72f,
                materials[converter.Material]);
            AddStaticCylinder(
                root,
                $"MidConverter{converter.Name}Exhaust",
                layout.Origin + converter.Position + new Vector3(0.72f, 1.55f, 0.25f),
                0.24f,
                0.32f,
                2.15f,
                materials[converter.Name == "West" ? "warning" : "cyan"]);
        }

        for (var index = 0; index < 3; index++)
        {
            AddStaticCylinder(
                root,
                $"MidGantryPipe_{index + 1:00}",
                layout.Origin + new Vector3(0, 4.65f + index * 0.27f, 21.64f + index * 0.19f),
                0.12f,
                0.12f,
                26.5f,
                materials[index == 1 ? "warning" : "steel"],
                new Vector3(0, 0, Mathf.Pi * 0.5f));
        }
        AddSign(root, "MidGantrySign", layout.Origin + new Vector3(0, 4.4f, 21.54f), "MID  //  PROCESS LINE 04", 0, new Color(0.95f, 0.72f, 0.28f));
        AddSign(root, "MidRelaySign", layout.Origin + new Vector3(23.0f, 4.2f, 19.18f), "R-12  //  RELAY", 0, new Color(0.3f, 0.88f, 1.0f));
        AddSign(root, "MidMaintenanceSign", layout.Origin + new Vector3(-22.0f, 2.7f, -21.92f), "M-04  //  SERVICE", Mathf.Pi, new Color(1.0f, 0.62f, 0.22f));
    }

    private void AddSign(Node3D root, string name, Vector3 position, string text, float yaw, Color color)
    {
        root.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            Text = text,
            FontSize = 42,
            Modulate = color,
            OutlineSize = 7,
            NoDepthTest = false
        });
        _visualPartCount++;
    }

    private void BuildRouteGuidance(Node3D root, DemolitionArenaLayout layout)
    {
        if (layout.MapId == DemolitionMapCatalog.BazaarCrossingId)
        {
            BuildBazaarCrossingRouteGuidance(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.TideglassReactorId)
        {
            BuildTideglassReactorRouteGuidance(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.HarborLocksId)
        {
            BuildHarborLocksRouteGuidance(root, layout);
            return;
        }
        AddFloorLabel(root, "AttackFloorLabel", layout.Origin + new Vector3(0, 0.09f, 43.0f), "ATTACK", new Color(0.56f, 0.92f, 0.86f), 78);
        AddFloorLabel(root, "RouteALabel", layout.Origin + new Vector3(-6.0f, 0.09f, 36.0f), "<  A", new Color(1.0f, 0.58f, 0.18f), 68);
        AddFloorLabel(root, "RouteMidLabel", layout.Origin + new Vector3(0, 0.09f, 34.0f), "MID", new Color(0.9f, 0.88f, 0.68f), 58);
        AddFloorLabel(root, "RouteBLabel", layout.Origin + new Vector3(6.0f, 0.09f, 36.0f), "B  >", new Color(0.28f, 0.82f, 0.96f), 68);
        AddFloorLabel(root, "DefendFloorLabel", layout.Origin + new Vector3(0, 0.09f, -43.0f), "DEFEND", new Color(0.46f, 0.94f, 0.68f), 72);
    }

    private void AddFloorLabel(
        Node3D root,
        string name,
        Vector3 position,
        string text,
        Color color,
        int fontSize)
    {
        root.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            RotationDegrees = new Vector3(-90, 0, 0),
            Text = text,
            FontSize = fontSize,
            Modulate = color,
            OutlineSize = 6,
            NoDepthTest = false
        });
        _visualPartCount++;
    }

    private void BuildLighting(
        Node3D root,
        DemolitionArenaLayout layout,
        IReadOnlyDictionary<string, StandardMaterial3D> materials)
    {
        if (layout.MapId == DemolitionMapCatalog.BazaarCrossingId)
        {
            BuildBazaarCrossingLighting(root, layout);
            return;
        }
        if (layout.MapId == DemolitionMapCatalog.TideglassReactorId)
        {
            BuildTideglassReactorLighting(root, layout);
            return;
        }
        var positions = new[]
        {
            new Vector3(-31, 8.5f, 21), new Vector3(33, 8.5f, -19),
            new Vector3(0, 8.5f, 8), new Vector3(0, 8.5f, -10),
            new Vector3(0, 8.5f, -31)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            var position = layout.Origin + positions[index];
            AddVisualBox(
                root,
                new DemolitionArenaBox($"LightPole_{index + 1:00}", position - Vector3.Up * 4.25f, new Vector3(0.14f, 8.5f, 0.14f), "steel"),
                materials["steel"]);
            root.AddChild(new SpotLight3D
            {
                Name = $"ArenaFloodlight_{index + 1:00}",
                Position = position,
                RotationDegrees = new Vector3(-90, 0, 0),
                LightColor = index % 2 == 0 ? new Color(1.0f, 0.72f, 0.42f) : new Color(0.62f, 0.82f, 0.86f),
                LightEnergy = 4.4f,
                SpotRange = 25.0f,
                SpotAngle = 50.0f,
                ShadowEnabled = index is 1 or 3
            });
            _visualPartCount++;
        }
    }

    private BoxMesh SharedBox(Vector3 size)
    {
        if (!_boxMeshes.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = size };
            _boxMeshes[size] = mesh;
        }
        return mesh;
    }
}
