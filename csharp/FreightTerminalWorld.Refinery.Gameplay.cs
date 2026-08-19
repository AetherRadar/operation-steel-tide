using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly Vector3[] RefineryWorldBossPatrolRoute =
    {
        new(-132, 0.2f, 40), new(-86, 0.2f, 31), new(-24, 0.2f, 28),
        new(24, 0.2f, 28), new(92, 0.2f, 22), new(132, 0.2f, -28),
        new(118, 0.2f, -92), new(84, 0.2f, -112), new(28, 0.2f, -146),
        new(-28, 0.2f, -146), new(-86, 0.2f, -112), new(-118, 0.2f, -92),
        new(-96, 0.2f, -28), new(-24, 0.2f, -60), new(24, 0.2f, -60)
    };

    private IReadOnlyList<Vector3> ActiveWorldBossPatrolRoute
        => IsBlackwaterRefineryMap ? RefineryWorldBossPatrolRoute : WorldBossPatrolRoute;

    private void SpawnRefineryWeaponCases()
    {
        var definitions = new[]
        {
            new RefineryWeaponCaseDefinition(
                new Vector3(-91, 0.02f, -122), 0.0f,
                "Grand Hotel security armory", "\u5927\u9152\u5e97\u5b89\u4fdd\u519b\u68b0\u5e93",
                WeaponCatalog.Build(WeaponPlatform.M4A1, 2),
                new[] { "optic_holo", "mag_extended" }, new[] { "armor_heavy" }, "knife_crimson"),
            new RefineryWeaponCaseDefinition(
                new Vector3(-77, 0.02f, -116), Mathf.Pi,
                "Hotel concierge response case", "\u9152\u5e97\u793c\u5bbe\u5e94\u6025\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.MP5A5, 2),
                new[] { "optic_micro", "muzzle_suppressor" }, new[] { "helmet_light" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(91, 0.02f, 4), Mathf.Pi,
                "Treasury tactical vault", "\u5e02\u653f\u91d1\u5e93\u6218\u672f\u67dc",
                WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                new[] { "optic_scope", "stock_precision" }, new[] { "armor_heavy", "pack_heavy" }, "knife_arctic"),
            new RefineryWeaponCaseDefinition(
                new Vector3(77, 0.02f, -4), 0.0f,
                "Treasury guard locker", "\u91d1\u5e93\u536b\u961f\u67dc",
                WeaponCatalog.Build(WeaponPlatform.AK74, 2),
                new[] { "muzzle_brake", "grip_vertical" }, new[] { "helmet_heavy" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(-14, 4.45f, -126), Mathf.Pi * 0.5f,
                "Market rooftop marksman case", "\u5e02\u96c6\u5c4b\u9876\u5c04\u624b\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.M24, 2),
                new[] { "optic_scope", "muzzle_suppressor" }, new[] { "pack_assault" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(18, 0.02f, -47), -Mathf.Pi * 0.5f,
                "Founders Plaza response case", "\u5f00\u57ce\u5e7f\u573a\u5e94\u6025\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.GSh18, 1),
                new[] { "optic_micro" }, new[] { "armor_carrier" }, string.Empty)
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
                Name = $"OldTownLoot_{++index:000}",
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
                Name = $"OldTownValuable_{++index:00}_{placement.Kind}",
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
            new(RefineryExtractionMapBuilder.HotelCenter, "minimap_old_town_hotel", "GRAND HOTEL", new Color(1.0f, 0.45f, 0.2f)),
            new(RefineryExtractionMapBuilder.TreasuryCenter, "minimap_old_town_treasury", "TREASURY", new Color(1.0f, 0.45f, 0.2f)),
            new(new Vector3(0, 0, -60), "minimap_old_town_plaza", "FOUNDERS PLAZA", new Color(0.95f, 0.73f, 0.3f)),
            new(new Vector3(0, 0, -126), "minimap_old_town_rooftop", "MARKET ROOFTOP", new Color(0.45f, 0.85f, 1.0f)),
            new(new Vector3(-43, 0, -92), "minimap_old_town_canal", "CANAL ROW", new Color(0.4f, 0.74f, 1.0f)),
            new(new Vector3(43, 0, -28), "minimap_old_town_garden", "GARDEN QUARTER", new Color(0.48f, 0.9f, 0.55f)),
            new(new Vector3(0, 0, -184), "minimap_old_town_north_gate", "NORTH GATE", new Color(0.82f, 0.78f, 0.7f)),
            new(new Vector3(0, 0, 48), "minimap_old_town_south_gate", "SOUTH GATE", new Color(0.82f, 0.78f, 0.7f))
        };
        _hud.ConfigureMinimap(new Rect2(-170, -220, MapWidthMeters, MapDepthMeters), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private async void ValidateRefineryMap()
    {
        await WaitFrames(8);
        var counts = new RefineryRuntimeCounts();
        CountRefineryNodes(_levelRoot, false, ref counts);

        var rootsReady = IsBlackwaterRefineryMap
            && _levelRoot.Name == "SaintMaraisOldTown"
            && _levelRoot.GetNodeOrNull<Node3D>("ExtractionSite") is not null
            && _levelRoot.GetNodeOrNull<Node3D>("OldTownLandmarks") is not null;
        var mapOffer = DeploymentMapCatalog.Resolve(DeploymentMapCatalog.BlackwaterRefineryId);
        var localizationReady = mapOffer.EnglishName == "SAINT MARAIS OLD TOWN"
            && mapOffer.EnglishSubtitle == "GRAND HOTEL  //  MUNICIPAL TREASURY"
            && GameLocalization.Get(mapOffer.LocalizationKey, "zh", mapOffer.EnglishName)
                == "\u5723\u9a6c\u96f7\u65e7\u57ce"
            && new[]
            {
                "minimap_old_town_hotel", "minimap_old_town_treasury",
                "minimap_old_town_plaza", "minimap_old_town_rooftop",
                "minimap_old_town_canal", "minimap_old_town_garden",
                "minimap_old_town_north_gate", "minimap_old_town_south_gate"
            }.All(key => GameLocalization.Get(key, "zh", key) != key);
        var authoredReady = _refineryAuthoredModelCount == RefineryLayout.Models.Count
            && _refineryAuthoredModelCount >= 120
            && counts.ImportedMeshes >= _refineryAuthoredModelCount
            && counts.CulledImportedMeshes == counts.ImportedMeshes;
        var sourcesReady = _refineryModelScenes.Count >= 10
            && HasRefinerySource("quaternius_downtown_city")
            && HasRefinerySource("Building_Large_2.gltf")
            && HasRefinerySource("Street_4WayIntersection.gltf")
            && _oldTownLandmarks?.ScenePaths.Count >= 10;
        var proxiesReady = _refineryCollisionProxyCount >= 45
            && counts.ModelCollisionShapes >= _refineryCollisionProxyCount
            && counts.NonBoxModelCollisionShapes == 0;
        var skylineReady = _refineryTallSceneCount >= 30
            && counts.TallSceneModels == _refineryTallSceneCount;
        var districtsReady = _oldTownDistricts.Count >= 10
            && _oldTownDistricts.Contains("grand_hotel")
            && _oldTownDistricts.Contains("municipal_treasury")
            && _oldTownDistricts.Contains("founders_plaza");
        var highValueReady = ValidateOldTownHighValueZones(out var zoneSeparation, out var zoneSummary);
        var routeReady = ValidateOldTownRouteProbes(out var routeProbeCount, out var routeBlocker);
        var landmarkReady = ValidateOldTownLandmarks();
        var gameplayReady = _objectiveTerminals.Count == 2
            && IsInstanceValid(_extractionArea)
            && IsInstanceValid(_extractionAircraft)
            && _buildingLootPickupCount == RefineryLayout.LootPlacements.Count
            && _lootSources.Count >= 32
            && _enemies.Count >= RefineryLayout.GarrisonSpawns.Count
            && _hud.MinimapLandmarkCount >= 10;
        var deploymentReady = DeploymentPoint.DistanceTo(ExtractionPoint) > 80.0f;
        var performanceReady = counts.Nodes < 1900
            && counts.StaticBodies < 125
            && counts.MeshInstances < 760
            && counts.Lights <= 32;
        var valid = rootsReady && localizationReady && authoredReady && sourcesReady && proxiesReady
            && skylineReady && districtsReady && highValueReady && routeReady
            && landmarkReady && gameplayReady && deploymentReady && performanceReady;

        GD.Print($"REFINERY_MAP_CHECK valid={valid} identity=saint_marais_old_town root={rootsReady} localization={localizationReady} authored={authoredReady} models={_refineryAuthoredModelCount}/{RefineryLayout.Models.Count} unique_scenes={_refineryModelScenes.Count} sources={sourcesReady} districts={_oldTownDistricts.Count} district_ready={districtsReady} imported_meshes={counts.ImportedMeshes} culled={counts.CulledImportedMeshes} proxies={counts.ModelCollisionShapes}/{_refineryCollisionProxyCount} proxy_boxes={proxiesReady} tall_scenes={_refineryTallSceneCount} skyline={skylineReady} high_value={highValueReady} zone_separation={zoneSeparation:0.0} zone_summary={zoneSummary} routes={routeReady} route_probes={routeProbeCount} route_blocker={routeBlocker} landmarks={landmarkReady} landmark_models={_oldTownLandmarks?.AuthoredModelCount ?? 0} landmark_collision={_oldTownLandmarks?.CollisionShapeCount ?? 0} rooftop_routes={_oldTownLandmarks?.RooftopRouteCount ?? 0} nodes={counts.Nodes} static_bodies={counts.StaticBodies} mesh_instances={counts.MeshInstances} lights={counts.Lights} loot={_lootSources.Count} graded_loot={_buildingLootPickupCount} garrison={_enemies.Count} minimap={_hud.MinimapLandmarkCount} deployment_distance={DeploymentPoint.DistanceTo(ExtractionPoint):0.0} performance={performanceReady}");
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
            Name = "OldTownCaptureLight",
            RotationDegrees = new Vector3(-55, -35, 0),
            LightEnergy = 0.9f,
            ShadowEnabled = false
        };
        AddChild(captureLight);
        var camera = new Camera3D { Name = "OldTownCaptureCamera", Fov = 52.0f, Far = 560.0f };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(148, 126, 88);
        camera.LookAt(new Vector3(0, 4, -68), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(16);
        SaveViewportImage("res://refinery_map_validation.png");
        camera.GlobalPosition = new Vector3(0, 5.8f, 70);
        camera.LookAt(new Vector3(0, 5.0f, -82), Vector3.Up);
        camera.Fov = 64.0f;
        await WaitFrames(10);
        SaveViewportImage("res://refinery_ground_validation.png");
        camera.GlobalPosition = new Vector3(-86, 7.0f, -92);
        camera.LookAt(RefineryExtractionMapBuilder.HotelCenter + Vector3.Up * 2.8f, Vector3.Up);
        camera.Fov = 70.0f;
        await WaitFrames(10);
        SaveViewportImage("res://refinery_hall_validation.png");
        camera.GlobalPosition = new Vector3(86, 7.0f, -28);
        camera.LookAt(RefineryExtractionMapBuilder.TreasuryCenter + Vector3.Up * 2.8f, Vector3.Up);
        await WaitFrames(10);
        SaveViewportImage("res://refinery_wonders_validation.png");
        camera.GlobalPosition = new Vector3(0, 11.5f, -96);
        camera.LookAt(new Vector3(0, 4.5f, -126), Vector3.Up);
        camera.Fov = 68.0f;
        await WaitFrames(10);
        SaveViewportImage("res://old_town_rooftop_validation.png");
        GD.Print($"REFINERY_MAP_CAPTURE identity=saint_marais_old_town models={_refineryAuthoredModelCount} scenes={_refineryModelScenes.Count} landmark_models={_oldTownLandmarks?.AuthoredModelCount ?? 0} paths=refinery_map_validation.png,refinery_ground_validation.png,refinery_hall_validation.png,refinery_wonders_validation.png,old_town_rooftop_validation.png");
        GetTree().Quit();
    }

    private bool ValidateOldTownHighValueZones(out float separation, out string summary)
    {
        separation = RefineryLayout.HighValueZones.Count == 2
            ? RefineryLayout.HighValueZones[0].Center.DistanceTo(RefineryLayout.HighValueZones[1].Center)
            : 0.0f;
        var valid = RefineryLayout.HighValueZones.Count == 2 && separation >= 190.0f;
        var parts = new List<string>();
        foreach (var zone in RefineryLayout.HighValueZones)
        {
            var graded = RefineryLayout.LootPlacements.Count(loot =>
                HorizontalDistance(loot.Position, zone.Center) <= zone.Radius
                && loot.Grade >= LootGrade.Epic);
            var valuables = RefineryLayout.ValuablePlacements.Count(loot =>
                HorizontalDistance(loot.Position, zone.Center) <= zone.Radius
                && loot.Grade >= LootGrade.Epic);
            var guards = RefineryLayout.GarrisonSpawns.Count(position =>
                HorizontalDistance(position, zone.Center) <= zone.Radius + 12.0f);
            valid &= graded >= 3 && valuables >= 3 && guards >= 4;
            parts.Add($"{zone.Id}:{graded}/{valuables}/{guards}");
        }
        summary = string.Join(',', parts);
        return valid;
    }

    private bool ValidateOldTownRouteProbes(out int checkedProbes, out string firstBlocker)
    {
        checkedProbes = 0;
        firstBlocker = "none";
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        foreach (var probe in RefineryLayout.RouteProbes)
        {
            checkedProbes++;
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    probe.From,
                    probe.To,
                    exclusions,
                    1,
                    out var hit))
            {
                continue;
            }
            var blocker = hit.Collider as Node3D;
            firstBlocker = blocker is null
                ? $"{probe.Name}:unknown@{hit.Position}"
                : $"{probe.Name}:{blocker.GetPath()}@{blocker.GlobalPosition}:hit={hit.Position}:shape={hit.Shape}";
            return false;
        }
        return checkedProbes == RefineryLayout.RouteProbes.Count;
    }

    private bool ValidateOldTownLandmarks()
    {
        if (_oldTownLandmarks is not { } landmarks)
        {
            return false;
        }
        var hotelEntryClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.HotelEntry, landmarks.HotelInterior, 1);
        var treasuryEntryClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.TreasuryEntry, landmarks.TreasuryInterior, 1);
        var hotelWallBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.HotelCenter + new Vector3(-14, 1.2f, 0), landmarks.HotelCenter, 1);
        var treasuryWallBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.TreasuryCenter + new Vector3(14, 1.2f, 0), landmarks.TreasuryCenter, 1);
        var rooftopDeckBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(), new Vector3(0, 7.0f, -126), new Vector3(0, 2.0f, -126), 1);
        var rooftopWalkClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), new Vector3(-20, 5.7f, -126), new Vector3(20, 5.7f, -126), 1);
        var traversalRegistered = _squadTraversalLinks.Any(link =>
            link.Source == "old_town_market_rooftop"
            && link.Bidirectional
            && link.ForwardPoints.Length >= 8);
        var countsReady = landmarks.LandmarkCount == 3
            && landmarks.HighValueZoneCount == 2
            && landmarks.AuthoredModelCount >= 110
            && landmarks.CollisionShapeCount >= 17
            && landmarks.EntryCount == 2
            && landmarks.RooftopRouteCount == 1;
        GD.Print($"OLD_TOWN_LANDMARK_CHECK hotel_entry={hotelEntryClear} treasury_entry={treasuryEntryClear} hotel_wall={hotelWallBlocks} treasury_wall={treasuryWallBlocks} rooftop_deck={rooftopDeckBlocks} rooftop_clear={rooftopWalkClear} traversal={traversalRegistered} counts={countsReady}");
        return hotelEntryClear && treasuryEntryClear && hotelWallBlocks && treasuryWallBlocks
            && rooftopDeckBlocks && rooftopWalkClear && traversalRegistered && countsReady;
    }

    private bool HasRefinerySource(string fragment)
        => _refineryModelScenes.Any(path =>
            path.Contains(fragment, System.StringComparison.OrdinalIgnoreCase));

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

    private static void CountRefineryNodes(
        Node node,
        bool insideAuthoredModel,
        ref RefineryRuntimeCounts counts)
    {
        counts.Nodes++;
        var authored = insideAuthoredModel || node.IsInGroup("refinery_authored_model");
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
        }
        if (node.IsInGroup("refinery_tall_scene"))
        {
            counts.TallSceneModels++;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CountRefineryNodes(childNode, authored, ref counts);
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
        public int TallSceneModels;
    }
}
