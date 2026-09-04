using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Builds the dedicated live-fire range used by the training mode.
///
/// The arena is a separate authored environment at a remote origin.  It uses the
/// repository's tracked CC0/ MIT GLB compositions for all visible structures and
/// props; primitive boxes below are collision-only gameplay scaffolding.
/// </summary>
public sealed class TrainingRangeArenaBuilder
{
    private const string MajadroidRoot = "res://assets/models/majadroid_construction_site";
    private const string TreyIndustrialRoot = "res://assets/models/trey_modular_industrial";
    private const string RoadsRoot = "res://assets/models/kenney_city_kit_roads";
    private const string DowntownRoot = "res://assets/models/quaternius_downtown_city";
    private const string PolyHavenBarrierRoot = "res://assets/models/concrete_road_barrier";
    private const string FurnitureRoot = "res://assets/models/kenney_furniture_kit";

    /// <summary>
    /// The old extraction map occupies roughly z -220..100.  Keeping the range 520 m
    /// downrange makes it impossible for old map collision or extraction actors to
    /// leak into the training session while retaining the same world lighting.
    /// </summary>
    public static readonly Vector3 DefaultOrigin = new(0.0f, 0.0f, 520.0f);

    private readonly Dictionary<string, PackedScene> _sceneCache = new(StringComparer.Ordinal);
    private readonly List<StaticBody3D> _collisionBodies = new();
    private readonly List<Node3D> _authoredModels = new();
    private StandardMaterial3D? _rangeRoadMaterial;
    private bool _built;

    public TrainingRangeArenaRuntime Build(Node parent, Vector3? origin = null)
    {
        if (_built)
        {
            throw new InvalidOperationException("A TrainingRangeArenaBuilder instance can build only one runtime arena.");
        }
        _built = true;
        _collisionBodies.Clear();
        _authoredModels.Clear();

        var arenaOrigin = origin ?? DefaultOrigin;
        var root = new Node3D
        {
            Name = "TrainingRangeArena",
            Position = arenaOrigin,
            ProcessMode = Node.ProcessModeEnum.Disabled,
            Visible = false
        };
        root.AddToGroup("training_range_arena");
        root.SetMeta("training_range_scene", "dedicated_live_fire_range");
        root.SetMeta("training_range_origin", arenaOrigin);
        parent.AddChild(root);

        BuildGroundAndBoundary(root);
        BuildAuthoredStructures(root);
        BuildTrainingStations(root);
        BuildLaneGuidance(root);
        BuildRangeLighting(root);

        var playerSpawn = arenaOrigin + new Vector3(0.0f, 0.24f, 38.0f);
        var botProfiles = BuildBotProfiles(arenaOrigin);
        var stations = BuildStations(arenaOrigin);
        var runtime = new TrainingRangeArenaRuntime(
            root,
            arenaOrigin,
            playerSpawn,
            botProfiles,
            stations,
            _collisionBodies,
            _authoredModels);
        // Build starts inactive; the world explicitly enables it after teleporting the
        // player.  This prevents a frame of collision overlap during menu transition.
        runtime.SetActive(false);
        return runtime;
    }

    private void BuildGroundAndBoundary(Node3D root)
    {
        // The downtown kit's authored asphalt module is a neutral, PBR street
        // surface with real normal/roughness maps.  Its 9 m square is scaled once
        // to cover the complete 99 m yard; unlike the construction-site palette
        // atlas it cannot turn the entire first-person view into a flat orange card.
        // The model's source bounds run from -9..0 on X/Z, hence the positive offset
        // centers it over the arena origin.
        AddAuthoredModel(
            root,
            "RangeAsphaltSurface",
            $"{DowntownRoot}/Street_Asphalt_9x9.gltf",
            new Vector3(49.5f, 0.16f, 49.5f),
            Vector3.Zero,
            new Vector3(11.0f, 1.0f, 11.0f));
        // The box below is invisible collision only and keeps a player from falling
        // through the authored mesh.
        AddCollisionBox(root, "RangeGroundCollision", new(0.0f, -0.52f, 0.0f), new(99.0f, 1.0f, 99.0f));

        // Four low perimeter walls define the self-contained yard.  They are collision
        // only; the authored fence run below provides the visible boundary.
        AddCollisionBox(root, "RangeBoundaryNorth", new(0.0f, 2.2f, -49.0f), new(99.0f, 4.4f, 0.7f));
        AddCollisionBox(root, "RangeBoundarySouth", new(0.0f, 2.2f, 49.0f), new(99.0f, 4.4f, 0.7f));
        AddCollisionBox(root, "RangeBoundaryWest", new(-49.0f, 2.2f, 0.0f), new(0.7f, 4.4f, 99.0f));
        AddCollisionBox(root, "RangeBoundaryEast", new(49.0f, 2.2f, 0.0f), new(0.7f, 4.4f, 99.0f));
    }

    private void BuildAuthoredStructures(Node3D root)
    {
        // Backdrop buildings frame the firing lanes without closing the player's line
        // of sight.  Each scene is a finished, licensed GLB already used by the game.
        AddAuthoredModel(
            root,
            "RangeLoadingBay",
            $"{TreyIndustrialRoot}/loading-bay.glb",
            new(-42.0f, 0.0f, -39.0f),
            new(0.0f, Mathf.Pi, 0.0f),
            Vector3.One * 2.65f);
        AddCollisionBox(root, "RangeLoadingBayCollision", new(-42.0f, 2.5f, -39.0f), new(18.0f, 5.0f, 16.0f));

        AddAuthoredModel(
            root,
            "RangeFoundryWarehouse",
            $"{TreyIndustrialRoot}/foundry-warehouse.glb",
            new(42.0f, 0.0f, -39.0f),
            Vector3.Zero,
            Vector3.One * 2.55f);
        AddCollisionBox(root, "RangeFoundryWarehouseCollision", new(42.0f, 2.5f, -39.0f), new(24.0f, 5.0f, 18.0f));

        AddAuthoredModel(
            root,
            "RangeUtilityOffice",
            $"{TreyIndustrialRoot}/utility-office.glb",
            new(-42.0f, 0.0f, 22.0f),
            new(0.0f, Mathf.Pi * 0.5f, 0.0f),
            Vector3.One * 2.7f);
        AddCollisionBox(root, "RangeUtilityOfficeCollision", new(-42.0f, 2.2f, 22.0f), new(16.0f, 4.4f, 15.0f));

        AddAuthoredModel(
            root,
            "RangeControlRoom",
            $"{TreyIndustrialRoot}/control-room.glb",
            new(42.0f, 0.0f, 22.0f),
            new(0.0f, -Mathf.Pi * 0.5f, 0.0f),
            Vector3.One * 2.25f);
        AddCollisionBox(root, "RangeControlRoomCollision", new(42.0f, 3.0f, 22.0f), new(17.0f, 6.0f, 17.0f));

        AddAuthoredModel(
            root,
            "RangeEntryArch",
            $"{TreyIndustrialRoot}/arch-gateway.glb",
            new(0.0f, 0.0f, 45.0f),
            Vector3.Zero,
            Vector3.One * 1.35f);

        BuildRangeBackstop(root);

        // A backstop and side barriers make the range read as a dedicated facility from
        // the spawn camera. These are authored road barriers; boundary collision
        // remains the invisible boxes created above.
        var fencePositions = new[]
        {
            (new Vector3(-43.0f, 0.0f, -47.0f), 0.0f),
            (new Vector3(-29.0f, 0.0f, -47.0f), 0.0f),
            (new Vector3(-15.0f, 0.0f, -47.0f), 0.0f),
            (new Vector3(15.0f, 0.0f, -47.0f), 0.0f),
            (new Vector3(29.0f, 0.0f, -47.0f), 0.0f),
            (new Vector3(43.0f, 0.0f, -47.0f), 0.0f),
            (new Vector3(-47.0f, 0.0f, -34.0f), 90.0f),
            (new Vector3(-47.0f, 0.0f, -20.0f), 90.0f),
            (new Vector3(-47.0f, 0.0f, -6.0f), 90.0f),
            (new Vector3(47.0f, 0.0f, -34.0f), 90.0f),
            (new Vector3(47.0f, 0.0f, -20.0f), 90.0f),
            (new Vector3(47.0f, 0.0f, -6.0f), 90.0f)
        };
        var fenceIndex = 0;
        foreach (var (position, yawDegrees) in fencePositions)
        {
            AddAuthoredModel(
                root,
                $"RangeFence_{++fenceIndex:00}",
                $"{RoadsRoot}/construction-barrier.glb",
                position,
                new(0.0f, Mathf.DegToRad(yawDegrees), 0.0f),
                Vector3.One * 12.0f);
        }
    }

    /// <summary>
    /// Build a visible, authored impact wall at the end of the firing lanes. The
    /// concrete barriers are CC0 Poly Haven geometry; the companion collision box is
    /// invisible gameplay scaffolding so projectiles and operators cannot leak through
    /// the seam between adjacent meshes.
    /// </summary>
    private void BuildRangeBackstop(Node3D root)
    {
        var segmentX = new[] { -45.0f, -30.0f, -15.0f, 0.0f, 15.0f, 30.0f, 45.0f };
        for (var index = 0; index < segmentX.Length; index++)
        {
            AddAuthoredModel(
                root,
                $"RangeBackstopBarrier_{index + 1:00}",
                $"{PolyHavenBarrierRoot}/concrete_road_barrier.gltf",
                new(segmentX[index], 0.0f, -35.0f),
                new(0.0f, Mathf.Pi, 0.0f),
                // Stretch vertically into a readable impact wall while preserving the
                // barrier's authored width/depth proportions.
                new(9.4f, 6.5f, 8.0f));
        }

        // Keep a small service gap below the sign while still sealing the whole lane
        // width for physics and projectile raycasts.
        AddCollisionBox(
            root,
            "RangeBackstopCollision",
            new(0.0f, 3.2f, -35.0f),
            new(98.0f, 6.4f, 1.4f));
        AddLabel(
            root,
            "RangeBackstopLabel",
            // Place the sign just in front of the backstop plane so it remains legible
            // between the target silhouettes from the fixed spawn camera.
            new(0.0f, 4.8f, -27.0f),
            "LIVE FIRE  //  BACKSTOP",
            new Color(1.0f, 0.55f, 0.24f),
            noDepthTest: true,
            fontSize: 72,
            pixelSize: 0.015f);
        AddLabel(
            root,
            "RangeTargetHeader",
            new(0.0f, 6.1f, -26.7f),
            "TARGET WALL  //  LANES 01-06",
            new Color(0.78f, 0.86f, 0.82f),
            noDepthTest: true,
            fontSize: 72,
            pixelSize: 0.015f);
    }

    private void BuildTrainingStations(Node3D root)
    {
        // Stations use authored Kenney furniture and props so the interaction points
        // are visible in-world instead of being invisible trigger coordinates.
        AddAuthoredModel(
            root,
            "WeaponSelectionBench",
            $"{FurnitureRoot}/desk.glb",
            new(-8.0f, 0.0f, 31.0f),
            new(0.0f, Mathf.Pi, 0.0f),
            Vector3.One * 1.25f);
        AddAuthoredModel(
            root,
            "WeaponSelectionScreen",
            $"{FurnitureRoot}/computerScreen.glb",
            new(-8.0f, 1.0f, 31.0f),
            Vector3.Zero,
            Vector3.One * 1.15f);

        AddAuthoredModel(
            root,
            "AmmunitionBench",
            $"{FurnitureRoot}/table.glb",
            new(0.0f, 0.0f, 31.0f),
            Vector3.Zero,
            Vector3.One * 1.2f);
        AddAuthoredModel(
            root,
            "AmmunitionCrate",
            $"{FurnitureRoot}/cardboardBoxClosed.glb",
            new(0.0f, 0.85f, 31.0f),
            new(0.0f, 0.28f, 0.0f),
            Vector3.One * 1.1f);

        AddAuthoredModel(
            root,
            "BotControlBench",
            $"{FurnitureRoot}/sideTableDrawers.glb",
            new(8.0f, 0.0f, 31.0f),
            new(0.0f, Mathf.Pi, 0.0f),
            Vector3.One * 1.15f);
        AddAuthoredModel(
            root,
            "BotControlScreen",
            $"{FurnitureRoot}/computerScreen.glb",
            new(8.0f, 0.92f, 31.0f),
            Vector3.Zero,
            Vector3.One * 1.08f);

        AddLabel(root, "WeaponStationLabel", new(-8.0f, 2.6f, 31.0f), "ARMORY  //  WEAPON SELECT", new Color(0.34f, 0.86f, 1.0f));
        AddLabel(root, "AmmoStationLabel", new(0.0f, 2.6f, 31.0f), "AMMO BENCH  //  CALIBER", new Color(1.0f, 0.72f, 0.24f));
        AddLabel(root, "BotStationLabel", new(8.0f, 2.6f, 31.0f), "BOT CONTROL  //  RESET", new Color(0.48f, 1.0f, 0.63f));
        AddLabel(root, "RangeSpawnInstruction", new(0.0f, 4.1f, 36.8f), "F  LOADOUT   //   FIRE LANES AHEAD", new Color(0.86f, 0.92f, 0.82f));
    }

    private void BuildLaneGuidance(Node3D root)
    {
        AddLabel(root, "RangeTitle", new(0.0f, 6.0f, 42.0f), "TRAINING RANGE  //  LIVE FIRE", new Color(0.42f, 0.9f, 1.0f));
        AddLabel(root, "RangeRule", new(0.0f, 4.8f, 38.0f), "SELECT  →  LOAD  →  FIRE  →  RESET", new Color(0.92f, 0.94f, 0.84f));

        var laneZ = new[] { 24.0f, 14.0f, 4.0f, -6.0f, -16.0f, -26.0f };
        for (var index = 0; index < laneZ.Length; index++)
        {
            AddLabel(
                root,
                $"LaneLabel_{index + 1:00}",
                new(-17.0f, 2.8f, laneZ[index]),
                $"LANE {index + 1:00}  //  BOT TARGET",
                new Color(0.74f, 0.8f, 0.78f));

            // Authored warning markers make each firing lane readable from the spawn
            // line even before a target has been selected.
            AddAuthoredModel(
                root,
                $"LaneWarningSign_{index + 1:00}",
                $"{RoadsRoot}/road-sign-warning.glb",
                new(-23.0f, 0.02f, laneZ[index]),
                new(0.0f, Mathf.Pi * 0.5f, 0.0f),
                Vector3.One * 1.15f);
            AddAuthoredModel(
                root,
                $"LaneLight_{index + 1:00}",
                $"{RoadsRoot}/construction-light.glb",
                new(23.0f, 0.02f, laneZ[index]),
                Vector3.Zero,
                Vector3.One * 1.1f);
        }

        AddLabel(
            root,
            "RangeFireLineLabel",
            new(0.0f, 1.45f, 31.7f),
            "FIRE LINE",
            new Color(1.0f, 0.84f, 0.36f));
    }

    private static void BuildRangeLighting(Node3D root)
    {
        var lights = new[]
        {
            (new Vector3(-8.0f, 3.2f, 31.0f), new Color(0.18f, 0.68f, 1.0f)),
            (new Vector3(0.0f, 3.0f, 31.0f), new Color(1.0f, 0.46f, 0.16f)),
            (new Vector3(8.0f, 3.0f, 31.0f), new Color(0.20f, 1.0f, 0.50f)),
            (new Vector3(0.0f, 6.0f, -42.0f), new Color(0.28f, 0.66f, 1.0f))
        };
        var index = 0;
        foreach (var (position, color) in lights)
        {
            root.AddChild(new OmniLight3D
            {
                Name = $"RangeLight_{++index:00}",
                Position = position,
                LightColor = color,
                LightEnergy = 1.4f,
                OmniRange = 18.0f,
                ShadowEnabled = true
            });
        }
    }

    private static IReadOnlyList<TrainingRangeBotProfile> BuildBotProfiles(Vector3 origin)
    {
        // Four targets per lane and six depth lanes provide all setup presets
        // (3/6/12/24) without changing the range footprint.
        var columns = new[] { -18.0f, -6.0f, 6.0f, 18.0f };
        var rows = new[] { 24.0f, 14.0f, 4.0f, -6.0f, -16.0f, -26.0f };
        var local = new List<Vector3>(columns.Length * rows.Length);
        foreach (var z in rows)
        {
            foreach (var x in columns)
            {
                local.Add(new Vector3(x, 0.24f, z));
            }
        }
        var visuals = new[]
        {
            OperatorVisualId.Garrison,
            OperatorVisualId.Heron,
            OperatorVisualId.Lynx,
            OperatorVisualId.Magpie,
            OperatorVisualId.Jackal,
            OperatorVisualId.Viper
        };
        var result = new List<TrainingRangeBotProfile>(local.Count);
        for (var index = 0; index < local.Count; index++)
        {
            result.Add(new TrainingRangeBotProfile(
                index,
                origin + local[index],
                visuals[index % visuals.Length],
                $"BOT {index + 1:00}"));
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<TrainingRangeStation> BuildStations(Vector3 origin)
        => new[]
        {
            new TrainingRangeStation("armory", TrainingRangeStationKind.Weapon, origin + new Vector3(-8.0f, 0.0f, 31.0f), 2.8f, "ARMORY  //  WEAPON SELECT"),
            new TrainingRangeStation("ammo", TrainingRangeStationKind.Ammunition, origin + new Vector3(0.0f, 0.0f, 31.0f), 2.6f, "AMMO BENCH  //  CALIBER"),
            new TrainingRangeStation("bot_control", TrainingRangeStationKind.BotControl, origin + new Vector3(8.0f, 0.0f, 31.0f), 2.8f, "BOT CONTROL  //  RESET")
        };

    private Node3D? AddAuthoredModel(
        Node3D root,
        string name,
        string path,
        Vector3 position,
        Vector3 rotation,
        Vector3 scale)
    {
        if (!_sceneCache.TryGetValue(path, out var scene))
        {
            scene = GD.Load<PackedScene>(path);
            if (scene is null)
            {
                GD.PushError($"Training range authored asset is missing: {path}");
                return null;
            }
            _sceneCache[path] = scene;
        }
        if (scene.Instantiate() is not Node3D model)
        {
            GD.PushError($"Training range authored asset could not instantiate: {path}");
            return null;
        }
        model.Name = name;
        model.Position = position;
        model.Rotation = rotation;
        model.Scale = scale;
        model.ProcessMode = Node.ProcessModeEnum.Disabled;
        RemoveExportPlaceholders(model);
        model.SetMeta("training_range_authored_asset", path);
        model.AddToGroup("training_range_authored_model");
        if (path.EndsWith("/road-square.glb", StringComparison.Ordinal)
            || path.EndsWith("/Street_Asphalt_9x9.gltf", StringComparison.Ordinal))
        {
            ApplyRangeRoadMaterial(model);
        }
        else if (name.Contains("Screen", StringComparison.Ordinal))
        {
            ApplyRangePropFinish(model, new Color(0.18f, 0.34f, 0.31f), screen: true);
        }
        else if (name.Contains("WeaponSelection", StringComparison.Ordinal))
        {
            ApplyRangePropFinish(model, new Color(0.24f, 0.32f, 0.34f));
        }
        else if (name.Contains("Ammunition", StringComparison.Ordinal))
        {
            ApplyRangePropFinish(model, new Color(0.42f, 0.32f, 0.19f));
        }
        else if (name.Contains("BotControl", StringComparison.Ordinal))
        {
            ApplyRangePropFinish(model, new Color(0.22f, 0.36f, 0.28f));
        }
        root.AddChild(model);
        _authoredModels.Add(model);
        return model;
    }

    /// <summary>
    /// The source packages were exported from Blender scenes that retain the default
    /// Cube/Camera/Light objects alongside the authored mesh.  Those helpers are not
    /// part of the asset and, when instanced at range stations, read as bright white
    /// boxes in the player's first-person view.  Remove only those unambiguous export
    /// leftovers; the authored meshes and their materials remain untouched.
    /// </summary>
    private static void RemoveExportPlaceholders(Node root)
    {
        var children = root.GetChildren();
        foreach (Node child in children)
        {
            var isDefaultMesh = child is MeshInstance3D
                && string.Equals(child.Name, "Cube", StringComparison.Ordinal);
            var isDefaultCamera = child is Camera3D
                && string.Equals(child.Name, "Camera", StringComparison.Ordinal);
            var isDefaultLight = child is Light3D
                && string.Equals(child.Name, "Light", StringComparison.Ordinal);
            if (isDefaultMesh || isDefaultCamera || isDefaultLight)
            {
                root.RemoveChild(child);
                child.Free();
                continue;
            }
            RemoveExportPlaceholders(child);
        }
    }

    /// <summary>
    /// The Kenney road mesh is authored geometry, but its shared palette atlas is an
    /// art-reference gradient (the road UVs intentionally sample a saturated orange
    /// swatch).  Keep the mesh and its raised markings while using the repository's
    /// licensed PBR asphalt for a readable firing-lane floor.
    /// </summary>
    private void ApplyRangeRoadMaterial(Node3D model)
    {
        _rangeRoadMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.24f, 0.25f),
            AlbedoTexture = GD.Load<Texture2D>("res://assets/textures/asphalt_03_diff_1k.jpg"),
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>("res://assets/textures/asphalt_03_normal_1k.jpg"),
            NormalScale = 0.72f,
            Roughness = 0.88f,
            RoughnessTexture = GD.Load<Texture2D>("res://assets/textures/asphalt_03_rough_1k.jpg"),
            Metallic = 0.04f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One * 0.16f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
        };
        ApplyMaterialRecursive(model, _rangeRoadMaterial);
    }

    private static void ApplyMaterialRecursive(Node node, Material material)
    {
        if (node is MeshInstance3D mesh)
        {
            mesh.MaterialOverride = material;
        }
        foreach (var child in node.GetChildren())
        {
            ApplyMaterialRecursive(child, material);
        }
    }

    /// <summary>
    /// Kenney's furniture materials are intentionally bright and read as display
    /// pieces under the range's sun.  Preserve their authored textures while applying
    /// a restrained station tint so the three interaction benches read as equipment,
    /// ammunition, and control consoles instead of unlit white boxes.
    /// </summary>
    private static void ApplyRangePropFinish(Node node, Color tint, bool screen = false)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh is { } sourceMesh)
        {
            for (var surface = 0; surface < sourceMesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source
                    || source.Duplicate(true) is not BaseMaterial3D finish)
                {
                    continue;
                }
                finish.AlbedoColor = new Color(
                    source.AlbedoColor.R * tint.R,
                    source.AlbedoColor.G * tint.G,
                    source.AlbedoColor.B * tint.B,
                    source.AlbedoColor.A);
                finish.Roughness = Mathf.Max(source.Roughness, 0.68f);
                if (screen)
                {
                    finish.EmissionEnabled = true;
                    finish.Emission = new Color(tint.R * 0.45f, tint.G * 1.25f, tint.B * 1.05f);
                    finish.EmissionEnergyMultiplier = 0.55f;
                }
                mesh.SetSurfaceOverrideMaterial(surface, finish);
            }
        }
        foreach (Node child in node.GetChildren())
        {
            ApplyRangePropFinish(child, tint, screen);
        }
    }

    private void AddCollisionBox(Node3D root, string name, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 0,
            CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = new BoxShape3D { Size = size }
        });
        root.AddChild(body);
        _collisionBodies.Add(body);
    }

    private static void AddLabel(
        Node3D root,
        string name,
        Vector3 position,
        string text,
        Color color,
        bool noDepthTest = false,
        int fontSize = 42,
        float pixelSize = 0.005f)
    {
        root.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Text = text,
            FontSize = fontSize,
            PixelSize = pixelSize,
            Modulate = color,
            OutlineSize = 8,
            NoDepthTest = noDepthTest
        });
    }
}
