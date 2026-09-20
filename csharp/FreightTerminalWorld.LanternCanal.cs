using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const string LanternCanalScenePath =
        LanternCanalPreloadCache.CityPath;
    private const string LanternCanalBackdropScenePath =
        LanternCanalPreloadCache.MountainsPath;
    private static readonly Vector3 LanternCanalDeploymentPoint = new(-68.0f, 1.32f, 76.0f);
    private static readonly Vector3 LanternCanalExtractionPoint = new(-68.0f, 1.12f, -88.0f);
    private static readonly Vector3[] LanternCanalHostilePads =
    {
        new(68.0f, 1.32f, 76.0f), new(68.0f, 1.32f, -88.0f),
        new(-68.0f, 1.32f, -20.0f), new(68.0f, 1.32f, 12.0f)
    };
    private static readonly Vector3[] LanternCanalWorldBossPatrolRoute =
    {
        new(68.0f, 1.32f, -80.0f), new(68.0f, 1.32f, -96.0f)
    };
    private static readonly Vector3[][] LanternCanalPatrolRoutes =
    {
        new[] { new Vector3(-42.0f, 1.32f, 76.0f), new Vector3(-42.0f, 1.32f, 62.0f) },
        new[] { new Vector3(-12.0f, 1.32f, 76.0f), new Vector3(-12.0f, 1.32f, 60.0f) },
        new[] { new Vector3(42.0f, 1.32f, 42.0f), new Vector3(42.0f, 1.32f, 26.0f) },
        new[] { new Vector3(42.0f, 1.32f, -12.0f), new Vector3(42.0f, 1.32f, -30.0f) },
        new[] { new Vector3(12.0f, 1.32f, -48.0f), new Vector3(12.0f, 1.32f, -34.0f) },
        new[] { new Vector3(-42.0f, 1.32f, -42.0f), new Vector3(-42.0f, 1.32f, -26.0f) }
    };
    private Node3D? _lanternCanalScene;
    private Node3D? _lanternCanalBackdrop;
    private int _lanternCanalCollisionCount;
    private int _lanternCanalLootCount;
    private int _lanternCanalObjectiveCount;
    private int _lanternCanalMountainMeshCount;
    private long _lanternCanalBuildMilliseconds;

    private bool IsLanternCanalMap
        => string.Equals(_activeRuntimeMapId, DeploymentMapCatalog.LanternCanalId, StringComparison.OrdinalIgnoreCase);

    private void BuildLanternCanalLevel()
    {
        var buildClock = System.Diagnostics.Stopwatch.StartNew();
        _levelRoot = new Node3D { Name = "LanternCanalDistrict" };
        AddChild(_levelRoot);
        _lanternCanalBackdrop = LoadLanternCanalMountainBackdrop();
        _lanternCanalScene = LoadLanternCanalScene(LanternCanalScenePath, "LanternCanalCompleteCity");
        LanternCanalLighting.ConfigureCity(_lanternCanalScene);
        BuildLanternCanalCollision();

        BuildObjectiveTerminal("LanternCeramicsRelay", new Vector3(12.8f, 1.12f, 68.0f), -Mathf.Pi * 0.5f, true);
        BuildObjectiveTerminal("LanternSilkManifest", new Vector3(12.8f, 1.12f, -21.0f), -Mathf.Pi * 0.5f, false);
        _lanternCanalObjectiveCount = 2;
        var concrete = GroundMaterial("lantern_canal_quay", new Color(0.52f, 0.55f, 0.52f), 0.86f);
        var iron = Mat("lantern_canal_iron", new Color(0.075f, 0.09f, 0.085f), 0.72f, 0.38f);
        var yellow = Mat("lantern_canal_warning", new Color(0.78f, 0.52f, 0.07f), 0.16f, 0.58f);
        var white = Mat("lantern_canal_marking", new Color(0.76f, 0.77f, 0.72f), 0.02f, 0.8f);
        BuildExtraction(concrete, iron, yellow, white);
        _extractionMarker.Visible = true;
        _lanternCanalBuildMilliseconds = buildClock.ElapsedMilliseconds;
        GD.Print($"LANTERN_CANAL_LOAD preload_ms={LanternCanalPreloadCache.LoadMilliseconds} yielded_frames={LanternCanalPreloadCache.YieldedFrames} build_ms={_lanternCanalBuildMilliseconds}");
    }

    private Node3D LoadLanternCanalScene(string path, string name)
    {
        var packedScene = LanternCanalPreloadCache.Acquire(path);
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

    private Node3D LoadLanternCanalMountainBackdrop()
    {
        var root = LoadLanternCanalScene(LanternCanalBackdropScenePath, "LanternCanalMountainBackdrop");
        _lanternCanalMountainMeshCount = 0;
        ConfigureLanternCanalMountainOnly(root, false);
        return root;
    }

    private void ConfigureLanternCanalMountainOnly(Node node, bool insideMountain)
    {
        var isMountain = insideMountain
            || node.Name.ToString().StartsWith("JianghaiMountainMassif", StringComparison.Ordinal);
        if (node is GeometryInstance3D geometry)
        {
            geometry.Visible = isMountain;
            if (isMountain && geometry is MeshInstance3D)
            {
                _lanternCanalMountainMeshCount++;
            }
        }
        if (node is Light3D light)
        {
            light.Visible = false;
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                ConfigureLanternCanalMountainOnly(childNode, isMountain);
            }
        }
    }

    private void BuildLanternCanalCollision()
    {
        var report = LanternCanalCollisionBuilder.Build(_lanternCanalScene!, _levelRoot);
        _lanternCanalCollisionCount = report.ShapeCount;
        foreach (var point in new[]
        {
            new Vector3(12.0f, 1.32f, 59.0f), new Vector3(12.0f, 1.32f, 75.0f),
            new Vector3(34.0f, 1.32f, 59.0f), new Vector3(34.0f, 1.32f, 75.0f),
            new Vector3(12.0f, 1.32f, -29.0f), new Vector3(12.0f, 1.32f, -13.0f),
            new Vector3(34.0f, 1.32f, -29.0f), new Vector3(34.0f, 1.32f, -13.0f),
            new Vector3(-34.0f, 1.32f, 42.0f), new Vector3(-34.0f, 1.32f, -40.0f)
        })
        {
            RegisterCoverPoint(point);
        }
        RegisterSquadTraversalLink("lantern_canal_quay_route", SquadTraversalKind.Walk, true,
            new[]
            {
                LanternCanalDeploymentPoint, new Vector3(-68.0f, 1.32f, -48.0f),
                new Vector3(-63.0f, 1.32f, -50.0f), new Vector3(-63.0f, 1.32f, -61.0f),
                new Vector3(-68.0f, 1.32f, -64.0f), LanternCanalExtractionPoint
            });
        foreach (var workshop in new[] { (Name: "ceramics", OffsetZ: 0.0f), (Name: "silk", OffsetZ: -88.0f) })
        {
            for (var level = 0; level < 2; level++)
            {
                var offset = new Vector3(0.0f, level * 3.8f, workshop.OffsetZ);
                RegisterSquadTraversalLink($"lantern_canal_{workshop.Name}_stairs_{level + 1}",
                    SquadTraversalKind.Walk, true, new[]
                    {
                        offset + new Vector3(22.8f, 1.36f, 71.5f),
                        offset + new Vector3(26.6f, 3.26f, 71.5f),
                        offset + new Vector3(26.6f, 3.26f, 73.3f),
                        offset + new Vector3(22.7f, 5.16f, 73.3f)
                    }, costMultiplier: 1.08f);
            }
        }
    }
    private void SpawnLanternCanalWeaponCases()
    {
        _lanternCanalLootCount = 0;
        foreach (var definition in new[]
        {
            (Position: new Vector3(20.0f, 1.255f, 66.0f), Name: "Ceramics workshop response case", Weapon: WeaponCatalog.Build(WeaponPlatform.M4A1, 1)),
            (Position: new Vector3(20.0f, 1.255f, -21.0f), Name: "Silk workshop guard case", Weapon: WeaponCatalog.Build(WeaponPlatform.MP5A5, 1)),
            (Position: new Vector3(-60.0f, 1.12f, 80.0f), Name: "South quay starter case", Weapon: WeaponCatalog.Build(WeaponPlatform.GSh18, 0))
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
            (Position: new Vector3(27.0f, 1.255f, 63.0f), Grade: LootGrade.Rare),
            (Position: new Vector3(20.0f, 5.105f, 69.0f), Grade: LootGrade.Rare),
            (Position: new Vector3(27.0f, 8.905f, 69.0f), Grade: LootGrade.Epic),
            (Position: new Vector3(27.0f, 1.255f, -18.0f), Grade: LootGrade.Uncommon),
            (Position: new Vector3(20.0f, 5.105f, -19.0f), Grade: LootGrade.Rare),
            (Position: new Vector3(27.0f, 8.905f, -19.0f), Grade: LootGrade.Epic)
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
        foreach (var resident in LanternCanalPopulation.Spawn(this, _languageSetting))
        {
            _civilians.Add(resident);
            RegisterResidentialLanguageRefresher(resident.SetLanguage);
        }
        foreach (var route in LanternCanalPatrolRoutes)
        {
            var enemy = SpawnEnemy(route[0], false, teamId: 0);
            enemy.AssignPatrolRoute(route);
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
        _hud.ConfigureMinimap(new Rect2(-80.0f, -192.0f, 160.0f, 278.0f), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

}
