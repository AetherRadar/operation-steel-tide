using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string LanternCanalScenePath =
        "res://assets/models/lantern_canal_world/lantern_canal_workshops_v2_5.glb";
    private const string LanternCanalBackdropScenePath =
        "res://assets/models/jianghai_old_city/jianghai_old_city.glb";
    private static readonly Vector3 LanternCanalDeploymentPoint = new(-68.0f, 0.2f, 92.0f);
    private static readonly Vector3 LanternCanalExtractionPoint = new(-68.0f, 0.08f, -88.0f);
    private static readonly Vector3[] LanternCanalHostilePads =
    {
        new(68.0f, 0.2f, 92.0f), new(68.0f, 0.2f, -88.0f),
        new(-68.0f, 0.2f, -20.0f), new(62.0f, 0.2f, 12.0f)
    };
    private static readonly Vector3[] LanternCanalPatrolRoute =
    {
        new(-48.0f, 0.15f, 76.0f), new(-12.0f, 0.15f, 76.0f),
        new(42.0f, 0.15f, 42.0f), new(48.0f, 0.15f, -12.0f),
        new(12.0f, 0.15f, -48.0f), new(-48.0f, 0.15f, -42.0f)
    };
    private Node3D? _lanternCanalScene;
    private Node3D? _lanternCanalBackdrop;
    private int _lanternCanalCollisionCount;
    private int _lanternCanalLootCount;
    private int _lanternCanalObjectiveCount;

    private bool IsLanternCanalMap
        => string.Equals(_activeRuntimeMapId, DeploymentMapCatalog.LanternCanalId, StringComparison.OrdinalIgnoreCase);

    private void BuildLanternCanalLevel()
    {
        _levelRoot = new Node3D { Name = "LanternCanalDistrict" };
        AddChild(_levelRoot);
        _lanternCanalBackdrop = LoadLanternCanalScene(LanternCanalBackdropScenePath, "LanternCanalJianghaiBackdrop");
        _lanternCanalScene = LoadLanternCanalScene(LanternCanalScenePath, "LanternCanalAuthoredWorkshops");
        BuildLanternCanalCollision();

        BuildObjectiveTerminal("LanternCeramicsRelay", new Vector3(12.8f, 0.0f, 68.0f), -Mathf.Pi * 0.5f, true);
        BuildObjectiveTerminal("LanternSilkManifest", new Vector3(12.8f, 0.0f, -21.0f), -Mathf.Pi * 0.5f, false);
        _lanternCanalObjectiveCount = 2;
        var concrete = GroundMaterial("lantern_canal_quay", new Color(0.52f, 0.55f, 0.52f), 0.86f);
        var iron = Mat("lantern_canal_iron", new Color(0.075f, 0.09f, 0.085f), 0.72f, 0.38f);
        var yellow = Mat("lantern_canal_warning", new Color(0.78f, 0.52f, 0.07f), 0.16f, 0.58f);
        var white = Mat("lantern_canal_marking", new Color(0.76f, 0.77f, 0.72f), 0.02f, 0.8f);
        BuildExtraction(concrete, iron, yellow, white);
        _extractionMarker.Visible = true;
    }

    private Node3D LoadLanternCanalScene(string path, string name)
    {
        var packedScene = GD.Load<PackedScene>(path)
            ?? throw new InvalidOperationException($"Unable to load Lantern Canal asset '{path}'.");
        var instance = packedScene.Instantiate();
        if (instance is not Node3D root)
        {
            instance.Free();
            throw new InvalidOperationException($"Lantern Canal asset '{path}' must have a Node3D root.");
        }
        root.Name = name;
        root.AddToGroup("lantern_canal_authored_scene");
        _levelRoot.AddChild(root);
        return root;
    }

    private void BuildLanternCanalCollision()
    {
        _lanternCanalCollisionCount = 0;
        AddLanternCanalCollision("Ground", new Vector3(0.0f, -0.55f, 10.0f), new Vector3(188.0f, 1.0f, 224.0f));
        AddLanternCanalCollision("WestBoundary", new Vector3(-94.0f, 2.0f, 10.0f), new Vector3(1.0f, 4.4f, 224.0f));
        AddLanternCanalCollision("EastBoundary", new Vector3(94.0f, 2.0f, 10.0f), new Vector3(1.0f, 4.4f, 224.0f));
        AddLanternCanalCollision("NorthBoundary", new Vector3(0.0f, 2.0f, -102.0f), new Vector3(188.0f, 4.4f, 1.0f));
        AddLanternCanalCollision("SouthBoundary", new Vector3(0.0f, 2.0f, 122.0f), new Vector3(188.0f, 4.4f, 1.0f));
        BuildLanternCanalWorkshopCollision("Ceramics", new Vector3(22.5f, 0.0f, 67.0f));
        BuildLanternCanalWorkshopCollision("Silk", new Vector3(22.5f, 0.0f, -21.0f));
        foreach (var point in new[]
        {
            new Vector3(12.0f, 0.0f, 59.0f), new Vector3(12.0f, 0.0f, 75.0f),
            new Vector3(34.0f, 0.0f, 59.0f), new Vector3(34.0f, 0.0f, 75.0f),
            new Vector3(12.0f, 0.0f, -29.0f), new Vector3(12.0f, 0.0f, -13.0f),
            new Vector3(34.0f, 0.0f, -29.0f), new Vector3(34.0f, 0.0f, -13.0f),
            new Vector3(-34.0f, 0.0f, 42.0f), new Vector3(-34.0f, 0.0f, -40.0f)
        })
        {
            RegisterCoverPoint(point);
        }
        RegisterSquadTraversalLink("lantern_canal_quay_route", SquadTraversalKind.Walk, true,
            new[] { LanternCanalDeploymentPoint, new Vector3(-42.0f, 0.2f, 36.0f), new Vector3(-42.0f, 0.2f, -38.0f), LanternCanalExtractionPoint });
    }

    private void BuildLanternCanalWorkshopCollision(string prefix, Vector3 center)
    {
        const float width = 15.6f;
        const float depth = 17.4f;
        const float height = 12.4f;
        const float wall = 0.28f;
        AddLanternCanalCollision(prefix + "EastWall", center + new Vector3(width * 0.5f, height * 0.5f, 0.0f), new Vector3(wall, height, depth));
        AddLanternCanalCollision(prefix + "BackWall", center + new Vector3(0.0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, wall));
        AddLanternCanalCollision(prefix + "FrontWall", center + new Vector3(0.0f, height * 0.5f, -depth * 0.5f), new Vector3(width, height, wall));
        // The west shopfront keeps a broad entry for the FPS capsule and squad rescue route.
        AddLanternCanalCollision(prefix + "WestWallNorth", center + new Vector3(-width * 0.5f, height * 0.5f, -5.9f), new Vector3(wall, height, 5.6f));
        AddLanternCanalCollision(prefix + "WestWallSouth", center + new Vector3(-width * 0.5f, height * 0.5f, 5.9f), new Vector3(wall, height, 5.6f));
        for (var level = 1; level <= 2; level++)
        {
            var y = 1.3f + level * 3.8f;
            AddLanternCanalCollision(prefix + "FloorWest" + level, center + new Vector3(-4.8f, y, 0.0f), new Vector3(5.8f, 0.18f, 16.8f));
            AddLanternCanalCollision(prefix + "FloorEast" + level, center + new Vector3(4.8f, y, 0.0f), new Vector3(5.8f, 0.18f, 16.8f));
            AddLanternCanalCollision(prefix + "FloorRear" + level, center + new Vector3(0.0f, y, 5.8f), new Vector3(3.8f, 0.18f, 5.2f));
            var lower = center + new Vector3(0.0f, 1.35f + (level - 1) * 3.8f, -6.0f);
            var upper = center + new Vector3(0.0f, 1.35f + level * 3.8f, -0.6f);
            var slope = -Mathf.Atan2(upper.Y - lower.Y, upper.Z - lower.Z);
            AddLanternCanalCollision(prefix + "StairRamp" + level, (lower + upper) * 0.5f - Vector3.Up * 0.14f,
                new Vector3(2.6f, 0.28f, lower.DistanceTo(upper)), new Vector3(slope, 0.0f, 0.0f));
            RegisterSquadTraversalLink("lantern_canal_" + prefix.ToLowerInvariant() + "_stairs_" + level,
                SquadTraversalKind.Walk, true, new[] { lower, upper }, costMultiplier: 1.08f);
        }
    }

    private void AddLanternCanalCollision(string name, Vector3 position, Vector3 size, Vector3 rotation = default)
    {
        AddInvisibleCollisionBox("LanternCanal" + name, position, size, rotation);
        _lanternCanalCollisionCount++;
    }

    private void SpawnLanternCanalWeaponCases()
    {
        _lanternCanalLootCount = 0;
        foreach (var definition in new[]
        {
            (Position: new Vector3(20.0f, 1.35f, 66.0f), Name: "Ceramics workshop response case", Weapon: WeaponCatalog.Build(WeaponPlatform.M4A1, 1)),
            (Position: new Vector3(20.0f, 1.35f, -21.0f), Name: "Silk workshop guard case", Weapon: WeaponCatalog.Build(WeaponPlatform.MP5A5, 1)),
            (Position: new Vector3(-60.0f, 0.05f, 84.0f), Name: "South quay starter case", Weapon: WeaponCatalog.Build(WeaponPlatform.GSh18, 0))
        })
        {
            var weaponCase = new WeaponCase
            {
                Name = "LanternCanalWeaponCase" + _lanternCanalLootCount,
                Position = definition.Position,
                EnglishName = definition.Name,
                ChineseName = "\u706f\u5f71\u6c34\u5df7\u5e94\u6025\u6b66\u5668\u7bb1"
            };
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.Weapon, Weapon = definition.Weapon, Grade = LootGrade.Uncommon });
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = WeaponCatalog.Weapon(definition.Weapon.Platform).Caliber, Quantity = 60, Grade = LootGrade.Uncommon });
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Grade = LootGrade.Uncommon });
            AddChild(weaponCase);
            _lootSources.Add(weaponCase);
            _lootWorldPoints.Add(definition.Position);
            _lanternCanalLootCount++;
        }
    }

    private void SpawnLanternCanalGradedLoot()
    {
        foreach (var placement in new[]
        {
            (Position: new Vector3(27.0f, 1.45f, 63.0f), Grade: LootGrade.Rare),
            (Position: new Vector3(20.0f, 5.35f, 69.0f), Grade: LootGrade.Rare),
            (Position: new Vector3(27.0f, 9.15f, 69.0f), Grade: LootGrade.Epic),
            (Position: new Vector3(27.0f, 1.45f, -18.0f), Grade: LootGrade.Uncommon),
            (Position: new Vector3(20.0f, 5.35f, -19.0f), Grade: LootGrade.Rare),
            (Position: new Vector3(27.0f, 9.15f, -19.0f), Grade: LootGrade.Epic)
        })
        {
            var pickup = new GradedLootPickup { Name = "LanternCanalSupply" + _lanternCanalLootCount, Position = placement.Position };
            pickup.Configure(CreateGradedLootItem(placement.Grade), "Workshop supplies", "\u5de5\u574a\u7269\u8d44");
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
            _buildingLootPickupCount++;
            _lanternCanalLootCount++;
        }
    }

    private void SpawnLanternCanalEnemies()
    {
        foreach (var position in LanternCanalPatrolRoute)
        {
            var enemy = SpawnEnemy(position, false, teamId: 0);
            enemy.AssignPatrolRoute(LanternCanalPatrolRoute);
        }
        _enemiesRemaining = _enemies.Count;
    }

    private void ConfigureLanternCanalMinimap()
    {
        var landmarks = new List<TacticalMapLandmark>
        {
            new(DeploymentPoint, "minimap_deploy", "DEPLOY", new Color(0.36f, 0.82f, 1.0f)),
            new(ExtractionPoint, "minimap_extract", "EXTRACT", new Color(0.32f, 0.95f, 0.66f)),
            new(new Vector3(22.5f, 0.0f, 67.0f), "lantern_canal_ceramics", "CERAMICS WORKSHOP", new Color(0.45f, 0.86f, 0.92f)),
            new(new Vector3(22.5f, 0.0f, -21.0f), "lantern_canal_silk", "SILK WORKSHOP", new Color(0.96f, 0.64f, 0.84f)),
            new(new Vector3(-42.0f, 0.0f, 10.0f), "lantern_canal_quay", "WEST QUAY", new Color(0.45f, 0.72f, 1.0f))
        };
        _hud.ConfigureMinimap(new Rect2(-94.0f, -102.0f, 188.0f, 224.0f), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private async void ValidateLanternCanal()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        await WaitFrames(3);
        var workshopReady = IsInstanceValid(_lanternCanalScene)
            && FindLanternCanalNode(_lanternCanalScene, "10_House_E_01") is not null
            && FindLanternCanalNode(_lanternCanalScene, "10_House_E_05") is not null;
        var meshCount = CountLanternCanalMeshes(_lanternCanalScene);
        var lightCount = CountLanternCanalLights(_lanternCanalScene);
        var backdropReady = IsInstanceValid(_lanternCanalBackdrop);
        var collisionReady = _lanternCanalCollisionCount >= 30;
        var gameplayReady = _lanternCanalObjectiveCount == 2 && _lanternCanalLootCount >= 9;
        var spawnReady = DeploymentPoint == LanternCanalDeploymentPoint
            && ExtractionPoint == LanternCanalExtractionPoint
            && DeploymentPoint.DistanceTo(ExtractionPoint) > 140.0f;
        var valid = workshopReady && backdropReady && meshCount >= 12 && lightCount >= 10
            && collisionReady && gameplayReady && spawnReady;
        GD.Print($"LANTERN_CANAL_CHECK valid={valid} workshop={workshopReady} backdrop={backdropReady} meshes={meshCount} lights={lightCount} collision={_lanternCanalCollisionCount} objectives={_lanternCanalObjectiveCount} loot={_lanternCanalLootCount} spawn={spawnReady}");
        GD.Print($"LANTERN_CANAL_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureLanternCanal()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _hud.Visible = false;
        var camera = new Camera3D { Name = "LanternCanalReviewCamera", Fov = 55.0f, Far = 420.0f };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(48.0f, 18.0f, 104.0f);
        camera.LookAt(new Vector3(22.0f, 5.0f, 67.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(30);
        SaveViewportImage("res://lantern_canal_validation.png");
        camera.GlobalPosition = new Vector3(44.0f, 12.0f, -2.0f);
        camera.LookAt(new Vector3(22.0f, 5.0f, -21.0f), Vector3.Up);
        await WaitFrames(24);
        SaveViewportImage("res://lantern_canal_workshop_validation.png");
        camera.GlobalPosition = new Vector3(18.0f, 2.25f, 66.0f);
        camera.LookAt(new Vector3(23.0f, 2.5f, 66.0f), Vector3.Up);
        await WaitFrames(24);
        SaveViewportImage("res://lantern_canal_interior_validation.png");
        GD.Print("LANTERN_CANAL_CAPTURE paths=lantern_canal_validation.png,lantern_canal_workshop_validation.png,lantern_canal_interior_validation.png");
        GetTree().Quit();
    }

    private static int CountLanternCanalMeshes(Node? root)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return 0;
        }
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D)
            {
                count++;
            }
            if (child is Node node)
            {
                count += CountLanternCanalMeshes(node);
            }
        }
        return count;
    }

    private static int CountLanternCanalLights(Node? root)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return 0;
        }
        var count = 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Light3D)
            {
                count++;
            }
            if (child is Node node)
            {
                count += CountLanternCanalLights(node);
            }
        }
        return count;
    }

    private static Node? FindLanternCanalNode(Node? root, string requiredName)
    {
        if (root is null || !GodotObject.IsInstanceValid(root))
        {
            return null;
        }
        foreach (var child in root.GetChildren())
        {
            if (child.Name.ToString().Equals(requiredName, StringComparison.Ordinal))
            {
                return child;
            }
            if (child is Node childNode && FindLanternCanalNode(childNode, requiredName) is Node match)
            {
                return match;
            }
        }
        return null;
    }
}
