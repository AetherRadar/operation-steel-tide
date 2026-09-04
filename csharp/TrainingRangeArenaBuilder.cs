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
        // The authored road slab supplies the readable range surface.  The box below
        // is invisible collision only and keeps a player from falling through its mesh.
        AddAuthoredModel(
            root,
            "RangeRoadSurface",
            $"{MajadroidRoot}/road.glb",
            Vector3.Zero,
            Vector3.Zero,
            new Vector3(0.72f, 1.0f, 0.72f));
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
            new(-35.0f, 0.0f, -33.0f),
            new(0.0f, Mathf.Pi, 0.0f),
            Vector3.One * 2.65f);
        AddCollisionBox(root, "RangeLoadingBayCollision", new(-35.0f, 2.5f, -33.0f), new(22.0f, 5.0f, 18.0f));

        AddAuthoredModel(
            root,
            "RangeFoundryWarehouse",
            $"{TreyIndustrialRoot}/foundry-warehouse.glb",
            new(34.0f, 0.0f, -33.0f),
            Vector3.Zero,
            Vector3.One * 2.55f);
        AddCollisionBox(root, "RangeFoundryWarehouseCollision", new(34.0f, 2.5f, -33.0f), new(31.0f, 5.0f, 23.0f));

        AddAuthoredModel(
            root,
            "RangeUtilityOffice",
            $"{TreyIndustrialRoot}/utility-office.glb",
            new(-35.0f, 0.0f, 24.0f),
            new(0.0f, Mathf.Pi * 0.5f, 0.0f),
            Vector3.One * 2.7f);
        AddCollisionBox(root, "RangeUtilityOfficeCollision", new(-35.0f, 2.2f, 24.0f), new(18.0f, 4.4f, 17.0f));

        AddAuthoredModel(
            root,
            "RangeControlRoom",
            $"{TreyIndustrialRoot}/control-room.glb",
            new(34.0f, 0.0f, 24.0f),
            new(0.0f, -Mathf.Pi * 0.5f, 0.0f),
            Vector3.One * 2.25f);
        AddCollisionBox(root, "RangeControlRoomCollision", new(34.0f, 3.0f, 24.0f), new(20.0f, 6.0f, 20.0f));

        AddAuthoredModel(
            root,
            "RangeEntryArch",
            $"{TreyIndustrialRoot}/arch-gateway.glb",
            new(0.0f, 0.0f, 45.0f),
            Vector3.Zero,
            Vector3.One * 1.35f);

        // A backstop and side fencing make the range read as a dedicated facility from
        // the spawn camera.  Fence panels are visual assets; boundary collision remains
        // the invisible boxes created above.
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
                $"{MajadroidRoot}/fence.glb",
                position,
                new(0.0f, Mathf.DegToRad(yawDegrees), 0.0f),
                Vector3.One * 1.15f);
        }
    }

    private void BuildTrainingStations(Node3D root)
    {
        // Stations use authored Kenney furniture and props so the interaction points
        // are visible in-world instead of being invisible trigger coordinates.
        AddAuthoredModel(
            root,
            "WeaponSelectionBench",
            $"{FurnitureRoot}/desk.glb",
            new(-27.0f, 0.0f, 34.0f),
            new(0.0f, Mathf.Pi, 0.0f),
            Vector3.One * 1.25f);
        AddAuthoredModel(
            root,
            "WeaponSelectionScreen",
            $"{FurnitureRoot}/computerScreen.glb",
            new(-27.0f, 1.0f, 34.0f),
            Vector3.Zero,
            Vector3.One * 1.15f);

        AddAuthoredModel(
            root,
            "AmmunitionBench",
            $"{FurnitureRoot}/table.glb",
            new(-12.0f, 0.0f, 34.0f),
            Vector3.Zero,
            Vector3.One * 1.2f);
        AddAuthoredModel(
            root,
            "AmmunitionCrate",
            $"{FurnitureRoot}/cardboardBoxClosed.glb",
            new(-12.0f, 0.85f, 34.0f),
            new(0.0f, 0.28f, 0.0f),
            Vector3.One * 1.1f);

        AddAuthoredModel(
            root,
            "BotControlBench",
            $"{FurnitureRoot}/sideTableDrawers.glb",
            new(15.0f, 0.0f, 34.0f),
            new(0.0f, Mathf.Pi, 0.0f),
            Vector3.One * 1.15f);
        AddAuthoredModel(
            root,
            "BotControlScreen",
            $"{FurnitureRoot}/computerScreen.glb",
            new(15.0f, 0.92f, 34.0f),
            Vector3.Zero,
            Vector3.One * 1.08f);

        AddLabel(root, "WeaponStationLabel", new(-27.0f, 2.6f, 34.0f), "ARMORY  //  WEAPON SELECT", new Color(0.34f, 0.86f, 1.0f));
        AddLabel(root, "AmmoStationLabel", new(-12.0f, 2.6f, 34.0f), "AMMO BENCH  //  CALIBER", new Color(1.0f, 0.72f, 0.24f));
        AddLabel(root, "BotStationLabel", new(15.0f, 2.6f, 34.0f), "BOT CONTROL  //  RESET", new Color(0.48f, 1.0f, 0.63f));
    }

    private void BuildLaneGuidance(Node3D root)
    {
        AddLabel(root, "RangeTitle", new(0.0f, 6.0f, 42.0f), "TRAINING RANGE  //  LIVE FIRE", new Color(0.42f, 0.9f, 1.0f));
        AddLabel(root, "RangeRule", new(0.0f, 4.8f, 38.0f), "SELECT  →  LOAD  →  FIRE  →  RESET", new Color(0.92f, 0.94f, 0.84f));

        var laneZ = new[] { 24.0f, 14.0f, 2.0f, -10.0f, -24.0f, -38.0f };
        for (var index = 0; index < laneZ.Length; index++)
        {
            AddLabel(
                root,
                $"LaneLabel_{index + 1:00}",
                new(-17.0f, 2.8f, laneZ[index]),
                $"LANE {index + 1:00}  //  BOT TARGET",
                new Color(0.74f, 0.8f, 0.78f));
        }
    }

    private static void BuildRangeLighting(Node3D root)
    {
        var lights = new[]
        {
            (new Vector3(-27.0f, 3.2f, 34.0f), new Color(0.18f, 0.68f, 1.0f)),
            (new Vector3(-12.0f, 3.0f, 34.0f), new Color(1.0f, 0.46f, 0.16f)),
            (new Vector3(15.0f, 3.0f, 34.0f), new Color(0.20f, 1.0f, 0.50f)),
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
        var local = new[]
        {
            new Vector3(0.0f, 0.24f, 24.0f),
            new Vector3(-8.0f, 0.24f, 14.0f),
            new Vector3(8.0f, 0.24f, 14.0f),
            new Vector3(-12.0f, 0.24f, 2.0f),
            new Vector3(12.0f, 0.24f, 2.0f),
            new Vector3(0.0f, 0.24f, -10.0f),
            new Vector3(-8.0f, 0.24f, -24.0f),
            new Vector3(8.0f, 0.24f, -24.0f),
            new Vector3(0.0f, 0.24f, -38.0f)
        };
        var visuals = new[]
        {
            OperatorVisualId.Garrison,
            OperatorVisualId.Heron,
            OperatorVisualId.Lynx,
            OperatorVisualId.Magpie,
            OperatorVisualId.Jackal,
            OperatorVisualId.Viper
        };
        var result = new List<TrainingRangeBotProfile>(local.Length);
        for (var index = 0; index < local.Length; index++)
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
            new TrainingRangeStation("armory", TrainingRangeStationKind.Weapon, origin + new Vector3(-27.0f, 0.0f, 34.0f), 2.8f, "ARMORY  //  WEAPON SELECT"),
            new TrainingRangeStation("ammo", TrainingRangeStationKind.Ammunition, origin + new Vector3(-12.0f, 0.0f, 34.0f), 2.6f, "AMMO BENCH  //  CALIBER"),
            new TrainingRangeStation("bot_control", TrainingRangeStationKind.BotControl, origin + new Vector3(15.0f, 0.0f, 34.0f), 2.8f, "BOT CONTROL  //  RESET")
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
        model.SetMeta("training_range_authored_asset", path);
        model.AddToGroup("training_range_authored_model");
        root.AddChild(model);
        _authoredModels.Add(model);
        return model;
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

    private static void AddLabel(Node3D root, string name, Vector3 position, string text, Color color)
    {
        root.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Text = text,
            FontSize = 42,
            Modulate = color,
            OutlineSize = 8,
            NoDepthTest = false
        });
    }
}
