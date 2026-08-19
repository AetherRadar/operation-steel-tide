using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly Vector3[] RefineryWorldBossPatrolRoute =
    {
        new(-126, 0.2f, 54), new(-72, 0.2f, 31), new(-18, 0.2f, 24),
        new(37, 0.2f, 18), new(118, 0.2f, 31), new(132, 0.2f, -57),
        new(105, 0.2f, -132), new(54, 0.2f, -181), new(-8, 0.2f, -187),
        new(-73, 0.2f, -171), new(-124, 0.2f, -128), new(-136, 0.2f, -48),
        new(-78, 0.2f, -52), new(0, 0.2f, -60), new(81, 0.2f, -68)
    };

    private IReadOnlyList<Vector3> ActiveWorldBossPatrolRoute
        => IsBlackwaterRefineryMap ? RefineryWorldBossPatrolRoute : WorldBossPatrolRoute;

    private void SpawnRefineryWeaponCases()
    {
        var definitions = new[]
        {
            new RefineryWeaponCaseDefinition(
                new Vector3(-40.5f, 0.02f, -2.0f), 0.0f,
                "Manifest intake armory", "\u6e05\u5355\u5165\u5e93\u519b\u68b0\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.M4A1, 1),
                new[] { "optic_holo", "mag_extended" }, new[] { "pack_assault" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(41.5f, 0.02f, -18.0f), Mathf.Pi,
                "Relay maintenance locker", "\u4e2d\u7ee7\u7ef4\u4fee\u67dc",
                WeaponCatalog.Build(WeaponPlatform.MP5A5, 1),
                new[] { "optic_micro", "muzzle_suppressor" }, new[] { "helmet_light" }, "knife_hazard"),
            new RefineryWeaponCaseDefinition(
                new Vector3(-78.0f, 0.02f, -79.0f), Mathf.Pi * 0.5f,
                "West pump response case", "\u897f\u6cf5\u7ad9\u5e94\u6025\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.AK74, 2),
                new[] { "muzzle_brake", "grip_vertical" }, new[] { "armor_carrier" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(79.0f, 0.02f, -87.0f), -Mathf.Pi * 0.5f,
                "East pump security case", "\u4e1c\u6cf5\u7ad9\u5b89\u4fdd\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                new[] { "optic_scope", "stock_precision" }, new[] { "helmet_heavy" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(-48.0f, 0.02f, -151.0f), 0.0f,
                "Cracking yard marksman case", "\u88c2\u89e3\u573a\u5c04\u624b\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.M24, 2),
                new[] { "optic_scope", "muzzle_suppressor" }, new[] { "armor_heavy" }, "knife_arctic"),
            new RefineryWeaponCaseDefinition(
                new Vector3(49.0f, 0.02f, -175.0f), Mathf.Pi,
                "Turbine master locker", "\u6da1\u8f6e\u4e3b\u50a8\u7269\u67dc",
                WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                new[] { "optic_holo", "mag_extended" }, new[] { "armor_heavy", "pack_heavy" }, "knife_crimson")
        };
        foreach (var definition in definitions)
        {
            SpawnRefineryWeaponCase(definition);
        }
    }

    private void SpawnRefineryWeaponCase(RefineryWeaponCaseDefinition definition)
    {
        var weaponCase = new WeaponCase
        {
            Position = definition.Position,
            Rotation = new Vector3(0, definition.Rotation, 0),
            EnglishName = definition.EnglishName,
            ChineseName = definition.ChineseName
        };
        weaponCase.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = definition.Weapon,
            Grade = LootGrades.FromTier(definition.Weapon.Attachments.Count >= 5 ? 2 : 1)
        });
        foreach (var part in definition.Parts)
        {
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Attachment,
                AttachmentId = part,
                Grade = LootGrade.Rare
            });
        }
        foreach (var equipmentId in definition.Equipment)
        {
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(equipmentId),
                Grade = equipmentId.Contains("heavy") ? LootGrade.Epic : LootGrade.Rare
            });
        }
        weaponCase.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = WeaponCatalog.Weapon(definition.Weapon.Platform).Caliber,
            Quantity = definition.Weapon.Platform == WeaponPlatform.M24 ? 24 : 75,
            Grade = LootGrade.Uncommon
        });
        if (!string.IsNullOrEmpty(definition.KnifeSkin))
        {
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.KnifeSkin,
                KnifeSkinId = definition.KnifeSkin,
                Grade = LootGrade.Epic
            });
        }
        weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Grade = LootGrade.Uncommon });
        AddChild(weaponCase);
        _lootSources.Add(weaponCase);
        _lootWorldPoints.Add(definition.Position);
    }

    private void SpawnRefineryGradedLoot()
    {
        var index = 0;
        foreach (var placement in RefineryLayout.LootPlacements)
        {
            var pickup = new GradedLootPickup
            {
                Name = $"RefineryLoot_{++index:000}",
                Position = placement.Position
            };
            pickup.Configure(
                CreateGradedLootItem(placement.Grade),
                placement.EnglishName,
                placement.ChineseName);
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
            _buildingLootPickupCount++;
        }
    }

    private void SpawnRefineryValuableLoot()
    {
        var index = 0;
        foreach (var placement in RefineryLayout.ValuablePlacements)
        {
            var item = new LootItem
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = placement.Kind,
                Grade = placement.Grade
            };
            var pickup = new GradedLootPickup
            {
                Name = $"RefineryValuable_{++index:00}_{placement.Kind}",
                Position = placement.Position
            };
            pickup.Configure(
                item,
                ValuableItems.DisplayName(placement.Kind, "en"),
                ValuableItems.DisplayName(placement.Kind, "zh"));
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
        }
    }

    private void ConfigureRefineryMinimap()
    {
        var landmarks = new List<TacticalMapLandmark>
        {
            new(DeploymentPoint, "minimap_deploy", "DEPLOY", new Color(0.36f, 0.82f, 1.0f)),
            new(ExtractionPoint, "minimap_extract", "EXTRACT", new Color(0.32f, 0.95f, 0.66f)),
            new(new Vector3(-31, 0, -7), "minimap_manifest", "MANIFEST", new Color(0.98f, 0.72f, 0.24f)),
            new(new Vector3(35.5f, 0, -10), "minimap_relay", "RELAY", new Color(1.0f, 0.5f, 0.2f)),
            new(new Vector3(0, 0, 48), "minimap_refinery_intake", "INTAKE", new Color(0.94f, 0.66f, 0.22f)),
            new(new Vector3(-91, 0, -72), "minimap_refinery_west_pump", "WEST PUMPS", new Color(0.38f, 0.78f, 1.0f)),
            new(new Vector3(92, 0, -75), "minimap_refinery_east_pump", "EAST PUMPS", new Color(0.38f, 0.78f, 1.0f)),
            new(new Vector3(0, 0, -132), "minimap_refinery_cracking", "CRACKING", new Color(1.0f, 0.48f, 0.18f)),
            new(new Vector3(39, 0, -165), "minimap_refinery_turbine", "TURBINE", new Color(0.44f, 0.94f, 0.68f)),
            new(new Vector3(-78, 0, -39), "minimap_refinery_cyclone", "CYCLONE", new Color(0.44f, 0.82f, 1.0f)),
            new(new Vector3(78, 0, -41), "minimap_refinery_reactor", "REACTOR", new Color(1.0f, 0.48f, 0.22f)),
            new(new Vector3(0, 0, -67), "minimap_refinery_skybridge", "SKYBRIDGE", new Color(0.88f, 0.72f, 1.0f))
        };
        _hud.ConfigureMinimap(new Rect2(-170, -220, MapWidthMeters, MapDepthMeters), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private async void ValidateRefineryMap()
    {
        await WaitFrames(6);
        var counts = new RefineryRuntimeCounts();
        CountRefineryNodes(_levelRoot, false, false, ref counts);
        var rootsReady = IsBlackwaterRefineryMap
            && _levelRoot.Name == "BlackwaterRefinery"
            && _levelRoot.GetNodeOrNull<Node3D>("ExtractionSite") is not null;
        var authoredReady = _refineryAuthoredModelCount == RefineryLayout.Models.Count
            && _refineryAuthoredModelCount >= 90
            && counts.ImportedMeshes >= _refineryAuthoredModelCount
            && counts.CulledImportedMeshes == counts.ImportedMeshes;
        var sourcesReady = _refineryModelScenes.Count >= 18
            && HasRefinerySource("kenney_city_kit_industrial")
            && HasRefinerySource("old_military_crate")
            && HasRefinerySource("concrete_road_barrier");
        var proxiesReady = _refineryCollisionProxyCount == _refineryAuthoredModelCount
            && counts.ModelCollisionShapes >= _refineryCollisionProxyCount
            && counts.NonBoxModelCollisionShapes == 0;
        var doorwayReady = _refineryAccessibleBuildingCount >= 40
            && counts.DoorwayCollisionShapes >= _refineryAccessibleBuildingCount * 4
            && _refineryEntryBeaconCount == _refineryAccessibleBuildingCount
            && counts.EntryBeacons == _refineryEntryBeaconCount;
        var skylineReady = _refineryTallSceneCount >= 6
            && counts.TallSceneModels == _refineryTallSceneCount;
        var doorwayBallisticsReady = ValidateRefineryDoorwayBallistics();
        var factoryReady = _refineryFactoryDistrict is
        {
            AuthoredModelCount: >= 180,
            CollisionShapeCount: >= 34,
            EntryCount: 2,
            RoofModuleCount: >= 20,
            CatwalkModuleCount: >= 20,
            InteriorPropCount: >= 20,
            AlleyPropCount: >= 10
        };
        var factorySourcesReady = _refineryFactoryDistrict?.ScenePaths.Count >= 15
            && HasFactorySource("structure-doorway-wide.glb")
            && HasFactorySource("catwalk-straight.glb")
            && HasFactorySource("crane.glb")
            && HasFactorySource("machine-fortified.glb");
        var hallEntriesReady = ValidateRefineryFactoryEntries();
        var wondersReady = _refineryWonderLandmarks is
        {
            LandmarkCount: 3,
            AuthoredModelCount: >= 55,
            CollisionShapeCount: >= 20,
            ElevatedBridgeModuleCount: >= 15,
            EnterableLandmarkCount: 2
        };
        var wonderSourcesReady = _refineryWonderLandmarks?.ScenePaths.Count >= 8
            && HasWonderSource("structure-doorway-wide.glb")
            && HasWonderSource("hopper-high-round.glb")
            && HasWonderSource("catwalk-straight.glb")
            && HasWonderSource("crane.glb");
        var wonderTraversalReady = ValidateRefineryWonderLandmarks();
        var gameplayReady = _objectiveTerminals.Count == 2
            && IsInstanceValid(_extractionArea)
            && IsInstanceValid(_extractionAircraft)
            && _buildingLootPickupCount == RefineryLayout.LootPlacements.Count
            && _lootSources.Count >= 30
            && _enemies.Count >= RefineryLayout.GarrisonSpawns.Count
            && _hud.MinimapLandmarkCount >= 12;
        var lanesReady = IsRefineryLaneClear(-8.2f) && IsRefineryLaneClear(8.2f);
        var deploymentReady = DeploymentPoint.DistanceTo(ExtractionPoint) > 80.0f;
        var performanceReady = counts.Nodes < 2500
            && counts.StaticBodies < 140
            && counts.MeshInstances < 900
            && counts.Lights <= 28;
        var valid = rootsReady && authoredReady && sourcesReady && proxiesReady
            && doorwayReady && skylineReady && doorwayBallisticsReady
            && factoryReady && factorySourcesReady && hallEntriesReady
            && wondersReady && wonderSourcesReady && wonderTraversalReady
            && gameplayReady && lanesReady && deploymentReady && performanceReady;
        GD.Print($"REFINERY_MAP_CHECK valid={valid} root={rootsReady} authored={authoredReady} models={_refineryAuthoredModelCount}/{RefineryLayout.Models.Count} unique_scenes={_refineryModelScenes.Count} sources={sourcesReady} imported_meshes={counts.ImportedMeshes} culled={counts.CulledImportedMeshes} proxies={counts.ModelCollisionShapes}/{_refineryCollisionProxyCount} proxy_boxes={proxiesReady} doorways={_refineryAccessibleBuildingCount} doorway_shapes={counts.DoorwayCollisionShapes} entry_beacons={counts.EntryBeacons}/{_refineryEntryBeaconCount} doorway_ready={doorwayReady} doorway_ballistics={doorwayBallisticsReady} tall_scenes={_refineryTallSceneCount} skyline={skylineReady} factory={factoryReady} factory_sources={factorySourcesReady} hall_entries={hallEntriesReady} factory_models={_refineryFactoryDistrict?.AuthoredModelCount ?? 0} factory_collision={_refineryFactoryDistrict?.CollisionShapeCount ?? 0} roof={_refineryFactoryDistrict?.RoofModuleCount ?? 0} catwalk={_refineryFactoryDistrict?.CatwalkModuleCount ?? 0} interior_props={_refineryFactoryDistrict?.InteriorPropCount ?? 0} alley_props={_refineryFactoryDistrict?.AlleyPropCount ?? 0} wonders={wondersReady} wonder_sources={wonderSourcesReady} wonder_traversal={wonderTraversalReady} wonder_models={_refineryWonderLandmarks?.AuthoredModelCount ?? 0} wonder_collision={_refineryWonderLandmarks?.CollisionShapeCount ?? 0} bridge_modules={_refineryWonderLandmarks?.ElevatedBridgeModuleCount ?? 0} nodes={counts.Nodes} static_bodies={counts.StaticBodies} mesh_instances={counts.MeshInstances} lights={counts.Lights} loot={_lootSources.Count} graded_loot={_buildingLootPickupCount} garrison={_enemies.Count} minimap={_hud.MinimapLandmarkCount} lanes={lanesReady} deployment_distance={DeploymentPoint.DistanceTo(ExtractionPoint):0.0} performance={performanceReady}");
        GD.Print($"REFINERY_MAP_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureRefineryMap()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _player.Visible = false;
        _hud.Visible = false;
        var captureLight = new DirectionalLight3D
        {
            Name = "RefineryCaptureLight",
            RotationDegrees = new Vector3(-58, -32, 0),
            LightEnergy = 0.85f,
            ShadowEnabled = false
        };
        AddChild(captureLight);
        var camera = new Camera3D { Name = "RefineryCaptureCamera", Fov = 52.0f, Far = 520.0f };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(126, 96, 58);
        camera.LookAt(new Vector3(0, 3, -78), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(14);
        SaveViewportImage("res://refinery_map_validation.png");
        camera.GlobalPosition = new Vector3(0, 5.8f, 69);
        camera.LookAt(new Vector3(0, 4.2f, -48), Vector3.Up);
        camera.Fov = 62.0f;
        await WaitFrames(8);
        SaveViewportImage("res://refinery_ground_validation.png");
        camera.GlobalPosition = new Vector3(0, 3.8f, -68.0f);
        camera.LookAt(new Vector3(0, 5.0f, -124.0f), Vector3.Up);
        camera.Fov = 70.0f;
        await WaitFrames(10);
        SaveViewportImage("res://refinery_hall_validation.png");
        camera.GlobalPosition = new Vector3(0, 28.0f, 48.0f);
        camera.LookAt(new Vector3(0, 7.0f, -48.0f), Vector3.Up);
        camera.Fov = 72.0f;
        await WaitFrames(10);
        SaveViewportImage("res://refinery_wonders_validation.png");
        GD.Print($"REFINERY_MAP_CAPTURE models={_refineryAuthoredModelCount} scenes={_refineryModelScenes.Count} factory_models={_refineryFactoryDistrict?.AuthoredModelCount ?? 0} wonder_models={_refineryWonderLandmarks?.AuthoredModelCount ?? 0} paths=refinery_map_validation.png,refinery_ground_validation.png,refinery_hall_validation.png,refinery_wonders_validation.png");
        GetTree().Quit();
    }

    private bool HasRefinerySource(string fragment)
    {
        foreach (var path in _refineryModelScenes)
        {
            if (path.Contains(fragment, System.StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsRefineryLaneClear(float x)
    {
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        if (!PhysicsRaycast.TryHit(
                GetWorld3D().DirectSpaceState,
            new Vector3(x, 1.1f, 82),
                new Vector3(x, 1.1f, -190),
                exclusions,
                1,
                out var hit))
        {
            return true;
        }

        var collider = hit.Collider as Node;
        GD.Print($"REFINERY_LANE_BLOCKED x={x:0.0} collider={collider?.Name ?? "unknown"} position={hit.Position}");
        return false;
    }

    private Godot.Collections.Array<Rid> BuildRefineryLaneExclusions()
    {
        var exclusions = new Godot.Collections.Array<Rid> { _player.GetRid() };
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                exclusions.Add(enemy.GetRid());
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                exclusions.Add(mate.GetRid());
            }
        }
        foreach (var vehicle in _vehicles)
        {
            if (IsInstanceValid(vehicle))
            {
                exclusions.Add(vehicle.GetRid());
            }
        }
        return exclusions;
    }

    private bool ValidateRefineryDoorwayBallistics()
    {
        var placement = RefineryLayout.Models.FirstOrDefault(model => model.HasDoorway);
        var building = _levelRoot.GetNodeOrNull<StaticBody3D>(placement.Name);
        if (building is null)
        {
            return false;
        }
        var size = placement.CollisionSize * placement.Scale;
        var probeY = Mathf.Min(1.45f, size.Y * 0.25f);
        var outsideZ = size.Z * 0.5f + 0.8f;
        var doorwayOpen = !PhysicsRaycast.HasHit(
            GetWorld3D(),
            building.ToGlobal(new Vector3(0, probeY, outsideZ)),
            building.ToGlobal(new Vector3(0, probeY, 0)),
            building.GetRid(),
            1);
        var wallX = size.X * 0.36f;
        var wallBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(),
            building.ToGlobal(new Vector3(wallX, probeY, outsideZ)),
            building.ToGlobal(new Vector3(wallX, probeY, 0)),
            1);
        return doorwayOpen && wallBlocks;
    }

    private bool HasFactorySource(string fileName)
        => _refineryFactoryDistrict?.ScenePaths.Any(path =>
            path.EndsWith('/' + fileName, System.StringComparison.OrdinalIgnoreCase)) == true;

    private bool HasWonderSource(string fileName)
        => _refineryWonderLandmarks?.ScenePaths.Any(path =>
            path.EndsWith('/' + fileName, System.StringComparison.OrdinalIgnoreCase)) == true;

    private bool ValidateRefineryWonderLandmarks()
    {
        if (_refineryWonderLandmarks is not { } wonders)
        {
            return false;
        }
        var westEntryClear = !PhysicsRaycast.HasHit(GetWorld3D(), wonders.WestEntry, wonders.WestInterior, 1);
        var eastEntryClear = !PhysicsRaycast.HasHit(GetWorld3D(), wonders.EastEntry, wonders.EastInterior, 1);
        var gateGroundClear = !PhysicsRaycast.HasHit(
            GetWorld3D(),
            new Vector3(0, 2.2f, wonders.GateCenter.Z + 8.0f),
            new Vector3(0, 2.2f, wonders.GateCenter.Z - 8.0f),
            1);
        var bridgeBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(),
            wonders.GateCenter + Vector3.Down * 2.0f,
            wonders.GateCenter + Vector3.Up * 2.0f,
            1);
        var westWallBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(),
            wonders.WestInterior + new Vector3(-10.0f, 13.0f, -4.0f),
            wonders.WestInterior + new Vector3(0, 13.0f, -4.0f),
            1);
        var eastWallBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(),
            wonders.EastInterior + new Vector3(10.0f, 13.0f, -4.0f),
            wonders.EastInterior + new Vector3(0, 13.0f, -4.0f),
            1);
        GD.Print($"REFINERY_WONDER_CHECK west_entry={westEntryClear} east_entry={eastEntryClear} gate_clear={gateGroundClear} bridge={bridgeBlocks} west_wall={westWallBlocks} east_wall={eastWallBlocks}");
        return westEntryClear && eastEntryClear && gateGroundClear && bridgeBlocks
            && westWallBlocks && eastWallBlocks;
    }

    private bool ValidateRefineryFactoryEntries()
    {
        if (_refineryFactoryDistrict is not { } hall)
        {
            return false;
        }
        const float traversalLaneX = 8.2f;
        var southClear = !PhysicsRaycast.HasHit(
            GetWorld3D(),
            hall.SouthEntry + new Vector3(-traversalLaneX, 1.0f, 0),
            hall.NorthEntry + new Vector3(-traversalLaneX, 1.0f, 10.0f),
            1);
        var northClear = !PhysicsRaycast.HasHit(
            GetWorld3D(),
            hall.NorthEntry + new Vector3(traversalLaneX, 1.0f, 0),
            hall.SouthEntry + new Vector3(traversalLaneX, 1.0f, -10.0f),
            1);
        var sideBlocked = PhysicsRaycast.HasHit(
            GetWorld3D(),
            hall.InteriorCenter + new Vector3(-10, 1.2f, 0),
            hall.InteriorCenter + new Vector3(-39, 1.2f, 0),
            1);
        GD.Print($"REFINERY_HALL_ENTRY_CHECK south={southClear} north={northClear} wall={sideBlocked}");
        return southClear && northClear && sideBlocked;
    }

    private static void CountRefineryNodes(
        Node node,
        bool insideAuthoredModel,
        bool insideAccessibleBuilding,
        ref RefineryRuntimeCounts counts)
    {
        counts.Nodes++;
        var authored = insideAuthoredModel || node.IsInGroup("refinery_authored_model");
        var accessible = insideAccessibleBuilding || node.IsInGroup("refinery_accessible_building");
        if (node is StaticBody3D)
        {
            counts.StaticBodies++;
        }
        if (node is Light3D)
        {
            counts.Lights++;
        }
        if (node is MeshInstance3D mesh)
        {
            counts.MeshInstances++;
            if (authored)
            {
                counts.ImportedMeshes++;
                if (mesh.VisibilityRangeEnd > 0.0f)
                {
                    counts.CulledImportedMeshes++;
                }
            }
        }
        if (authored && node is CollisionShape3D collision)
        {
            counts.ModelCollisionShapes++;
            if (collision.Shape is not BoxShape3D)
            {
                counts.NonBoxModelCollisionShapes++;
            }
            if (accessible && collision.Name.ToString().StartsWith("Doorway", System.StringComparison.Ordinal))
            {
                counts.DoorwayCollisionShapes++;
            }
        }
        if (node.IsInGroup("refinery_tall_scene"))
        {
            counts.TallSceneModels++;
        }
        if (node.IsInGroup("refinery_entry_beacon"))
        {
            counts.EntryBeacons++;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CountRefineryNodes(childNode, authored, accessible, ref counts);
            }
        }
    }

    private sealed record RefineryWeaponCaseDefinition(
        Vector3 Position,
        float Rotation,
        string EnglishName,
        string ChineseName,
        WeaponBuild Weapon,
        string[] Parts,
        string[] Equipment,
        string KnifeSkin);

    private struct RefineryRuntimeCounts
    {
        public int Nodes;
        public int StaticBodies;
        public int MeshInstances;
        public int Lights;
        public int ImportedMeshes;
        public int CulledImportedMeshes;
        public int ModelCollisionShapes;
        public int NonBoxModelCollisionShapes;
        public int DoorwayCollisionShapes;
        public int TallSceneModels;
        public int EntryBeacons;
    }
}
