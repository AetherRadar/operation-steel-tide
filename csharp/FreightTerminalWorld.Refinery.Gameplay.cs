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
                "Guangchang Pawnshop security armory", "\u5e7f\u660c\u5f53\u94fa\u5b89\u4fdd\u519b\u68b0\u5e93",
                WeaponCatalog.Build(WeaponPlatform.M4A1, 2),
                new[] { "optic_holo", "mag_extended" }, new[] { "armor_heavy" }, "knife_zhanma"),
            new RefineryWeaponCaseDefinition(
                new Vector3(-77, 0.02f, -116), Mathf.Pi,
                "Pawnshop counter response case", "\u5f53\u94fa\u67dc\u53f0\u5e94\u6025\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.MP5A5, 2),
                new[] { "optic_micro", "muzzle_suppressor" }, new[] { "helmet_light" }, "knife_crimson"),
            new RefineryWeaponCaseDefinition(
                new Vector3(91, 0.02f, 4), Mathf.Pi,
                "Red Star Electronics tactical vault", "\u7ea2\u661f\u7535\u5b50\u5382\u6218\u672f\u67dc",
                WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                new[] { "optic_scope", "stock_precision" }, new[] { "armor_heavy", "pack_heavy" }, "knife_tianxuan"),
            new RefineryWeaponCaseDefinition(
                new Vector3(77, 0.02f, -4), 0.0f,
                "Factory guard locker", "\u7535\u5b50\u5382\u8b66\u536b\u67dc",
                WeaponCatalog.Build(WeaponPlatform.AK74, 2),
                new[] { "muzzle_brake", "grip_vertical" }, new[] { "helmet_heavy" }, "knife_arctic"),
            new RefineryWeaponCaseDefinition(
                new Vector3(-14, 4.45f, -126), Mathf.Pi * 0.5f,
                "Old City footbridge marksman case", "\u65e7\u57ce\u5929\u6865\u5e02\u96c6\u5c04\u624b\u7bb1",
                WeaponCatalog.Build(WeaponPlatform.M24, 2),
                new[] { "optic_scope", "muzzle_suppressor" }, new[] { "pack_assault" }, string.Empty),
            new RefineryWeaponCaseDefinition(
                new Vector3(18, 0.02f, -47), -Mathf.Pi * 0.5f,
                "Jianghai Square response case", "\u6c5f\u6d77\u5e7f\u573a\u5e94\u6025\u7bb1",
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
            new(RefineryExtractionMapBuilder.HotelCenter, "minimap_old_town_hotel", "GUANGCHANG PAWNSHOP", new Color(1.0f, 0.45f, 0.2f)),
            new(RefineryExtractionMapBuilder.TreasuryCenter, "minimap_old_town_treasury", "RED STAR ELECTRONICS", new Color(1.0f, 0.45f, 0.2f)),
            new(new Vector3(0, 0, -60), "minimap_old_town_plaza", "JIANGHAI SQUARE", new Color(0.95f, 0.73f, 0.3f)),
            new(new Vector3(0, 0, -126), "minimap_old_town_rooftop", "MARKET FOOTBRIDGE", new Color(0.45f, 0.85f, 1.0f)),
            new(new Vector3(-43, 0, -92), "minimap_old_town_canal", "WEST ARCADE", new Color(0.4f, 0.74f, 1.0f)),
            new(new Vector3(43, 0, -28), "minimap_old_town_garden", "RED STAR FACTORY ROW", new Color(0.48f, 0.9f, 0.55f)),
            new(new Vector3(0, 0, -184), "minimap_old_town_north_gate", "RIVER WHARF", new Color(0.82f, 0.78f, 0.7f)),
            new(new Vector3(0, 0, 48), "minimap_old_town_south_gate", "SOUTH GATE", new Color(0.82f, 0.78f, 0.7f))
        };
        _hud.ConfigureMinimap(new Rect2(-170, -220, MapWidthMeters, MapDepthMeters), landmarks);
        _hud.SetMinimapPlayer(_player.GlobalPosition, 0.0f);
    }

    private async void ValidateRefineryMap()
    {
        await WaitFrames(8);
        var counts = new RefineryRuntimeCounts();
        CountRefineryNodes(_levelRoot, false, false, false, ref counts);

        var rootsReady = IsBlackwaterRefineryMap
            && _levelRoot.Name == "SaintMaraisOldTown"
            && _levelRoot.GetNodeOrNull<Node3D>("ExtractionSite") is not null
            && _levelRoot.GetNodeOrNull<Node3D>("OldTownLandmarks") is not null
            && _jianghaiOldCityScene is { } authoredRoot
            && GodotObject.IsInstanceValid(authoredRoot.Root)
            && authoredRoot.Root.GetParent() == _levelRoot;
        var mapOffer = DeploymentMapCatalog.Resolve(DeploymentMapCatalog.BlackwaterRefineryId);
        var localizationReady = mapOffer.Id == "blackwater_refinery"
            && mapOffer.EnglishName == "JIANGHAI OLD CITY"
            && mapOffer.EnglishSubtitle == "GUANGCHANG PAWNSHOP  //  RED STAR ELECTRONICS"
            && GameLocalization.Get(mapOffer.LocalizationKey, "zh", mapOffer.EnglishName)
                == "\u6c5f\u6d77\u65e7\u57ce"
            && GameLocalization.Get(mapOffer.SubtitleLocalizationKey, "zh", mapOffer.EnglishSubtitle)
                == "\u5e7f\u660c\u5f53\u94fa  //  \u7ea2\u661f\u7535\u5b50\u5382"
            && new (string Key, string Chinese)[]
            {
                ("minimap_old_town_hotel", "\u5e7f\u660c\u5f53\u94fa"),
                ("minimap_old_town_treasury", "\u7ea2\u661f\u7535\u5b50\u5382"),
                ("minimap_old_town_plaza", "\u6c5f\u6d77\u5e7f\u573a"),
                ("minimap_old_town_rooftop", "\u5929\u6865\u5e02\u96c6"),
                ("minimap_old_town_canal", "\u897f\u5173\u9a91\u697c"),
                ("minimap_old_town_garden", "\u7ea2\u661f\u5382\u8857"),
                ("minimap_old_town_north_gate", "\u4e34\u6c5f\u7801\u5934"),
                ("minimap_old_town_south_gate", "\u5357\u57ce\u724c\u574a")
            }.All(entry => GameLocalization.Get(entry.Key, "zh", entry.Key) == entry.Chinese);
        var authoredReady = _jianghaiOldCityScene is { } authored
            && authored.ScenePath == JianghaiOldCitySceneLoader.DefaultScenePath
            && authored.MeshInstanceCount > 0
            && authored.SurfaceCount >= authored.MeshInstanceCount
            && authored.MaterialSurfaceCount > 0
            && authored.MaterialSurfaceCount <= authored.SurfaceCount
            && authored.InstanceTriangleCount is > 0 and <= 5_000_000
            && authored.RequiredAnchorCount == authored.RequiredAnchorTotal
            && authored.AuthoredTerminalTotal == 2
            && authored.AuthoredTerminalCount == authored.AuthoredTerminalTotal
            && authored.VisibleAuthoredTerminalCount == authored.AuthoredTerminalTotal
            && authored.AlignedAuthoredTerminalCount == authored.AuthoredTerminalTotal
            && authored.AuthoredStatusScreenCount == authored.AuthoredStatusScreenTotal
            && authored.AuthoredStatusScreenTotal == 2
            && authored.DetailMeshCount > 0
            && counts.AuthoredSceneRoots == 1
            && counts.AuthoredSceneMeshes == authored.MeshInstanceCount
            && counts.VisibleAuthoredSceneMeshes == counts.AuthoredSceneMeshes;
        var expectedCollisionProxies = RefineryLayout.Models.Count(placement => placement.HasCollision);
        var proxiesReady = _refineryCollisionProxyCount == expectedCollisionProxies
            && counts.LegacyCollisionShapes == _refineryCollisionProxyCount
            && counts.NonBoxLegacyCollisionShapes == 0;
        var legacyScaffoldsHidden = counts.VisibleLegacyScaffoldGeometry == 0;
        var interactiveDoorsReady = _oldTownLandmarks is { } landmarks
            && _refineryDoors.Count == landmarks.EntryCount
            && _refineryDoors.All(door => IsInstanceValid(door)
                && door.UsesAuthoredVisual
                && door.HasBoxCollision);
        var districtsReady = _oldTownDistricts.Count >= 10
            && _oldTownDistricts.Contains("grand_hotel")
            && _oldTownDistricts.Contains("municipal_treasury")
            && _oldTownDistricts.Contains("founders_plaza");
        var highValueReady = ValidateOldTownHighValueZones(out var zoneSeparation, out var zoneSummary);
        var routeReady = ValidateOldTownRouteProbes(out var routeProbeCount, out var routeBlocker);
        var landmarkReady = ValidateOldTownLandmarks();
        var terminalCollisionReady = ValidateRefineryTerminalCollisions(out var alignedTerminalCollisions);
        var terminalStatusesReady = ValidateAuthoredTerminalStatusTransitions(out var terminalStatusSummary);
        var authoredQualityReady = ValidateAuthoredQualityTiers(out var authoredQualitySummary);
        var gameplayReady = _objectiveTerminals.Count == 2
            && terminalCollisionReady
            && terminalStatusesReady
            && IsInstanceValid(_extractionArea)
            && IsInstanceValid(_extractionAircraft)
            && _buildingLootPickupCount == RefineryLayout.LootPlacements.Count
            && _lootSources.Count >= 32
            && _enemies.Count >= RefineryLayout.GarrisonSpawns.Count
            && _hud.MinimapLandmarkCount >= 10;
        var deploymentReady = DeploymentPoint.DistanceTo(ExtractionPoint) > 80.0f;
        var performanceReady = counts.Nodes < 1900
            && counts.StaticBodies < 125
            && counts.MeshInstances < 770
            && counts.Lights <= 32
            && _jianghaiOldCityScene is { } performanceScene
            && performanceScene.InstanceTriangleCount <= 5_000_000
            && performanceScene.ShadowCasterMeshCount > 0
            && performanceScene.ShadowCasterMeshCount < performanceScene.MeshInstanceCount;
        var valid = rootsReady && localizationReady && authoredReady && proxiesReady
            && legacyScaffoldsHidden && interactiveDoorsReady && districtsReady && highValueReady && routeReady
            && landmarkReady && authoredQualityReady && gameplayReady && deploymentReady && performanceReady;

        GD.Print($"REFINERY_MAP_CHECK valid={valid} map_id={DeploymentMapCatalog.BlackwaterRefineryId} identity=jianghai_old_city root={rootsReady} localization={localizationReady} authored={authoredReady} authored_path={_jianghaiOldCityScene?.ScenePath ?? "missing"} authored_error={(_jianghaiOldCitySceneLoadError is null ? "none" : "load_failed")} authored_roots={counts.AuthoredSceneRoots} authored_meshes={_jianghaiOldCityScene?.MeshInstanceCount ?? 0} authored_surfaces={_jianghaiOldCityScene?.SurfaceCount ?? 0} authored_material_surfaces={_jianghaiOldCityScene?.MaterialSurfaceCount ?? 0} authored_triangles={_jianghaiOldCityScene?.InstanceTriangleCount ?? 0} authored_anchors={_jianghaiOldCityScene?.RequiredAnchorCount ?? 0}/{_jianghaiOldCityScene?.RequiredAnchorTotal ?? 0} authored_terminals={_jianghaiOldCityScene?.AuthoredTerminalCount ?? 0}/{_jianghaiOldCityScene?.VisibleAuthoredTerminalCount ?? 0}/{_jianghaiOldCityScene?.AlignedAuthoredTerminalCount ?? 0}/{_jianghaiOldCityScene?.AuthoredTerminalTotal ?? 0} authored_screens={_jianghaiOldCityScene?.AuthoredStatusScreenCount ?? 0}/{_jianghaiOldCityScene?.AuthoredStatusScreenTotal ?? 0} terminal_statuses={terminalStatusesReady}:{terminalStatusSummary} authored_detail={_jianghaiOldCityScene?.DetailMeshCount ?? 0} authored_quality={_jianghaiOldCityScene?.QualityTier ?? -1} authored_shadows={_jianghaiOldCityScene?.ShadowCasterMeshCount ?? 0} authored_quality_tiers={authoredQualityReady}:{authoredQualitySummary} terminal_collision={terminalCollisionReady} terminal_collision_aligned={alignedTerminalCollisions}/2 legacy_visible={counts.VisibleLegacyScaffoldGeometry} scaffolds_hidden={legacyScaffoldsHidden} proxies={counts.LegacyCollisionShapes}/{_refineryCollisionProxyCount} expected_proxies={expectedCollisionProxies} proxy_boxes={proxiesReady} districts={_oldTownDistricts.Count} district_ready={districtsReady} doors={_refineryDoors.Count}/{_oldTownLandmarks?.EntryCount ?? 0} doors_ready={interactiveDoorsReady} high_value={highValueReady} zone_separation={zoneSeparation:0.0} zone_summary={zoneSummary} routes={routeReady} route_probes={routeProbeCount} route_blocker={routeBlocker} landmarks={landmarkReady} landmark_collision={_oldTownLandmarks?.CollisionShapeCount ?? 0} rooftop_routes={_oldTownLandmarks?.RooftopRouteCount ?? 0} nodes={counts.Nodes} static_bodies={counts.StaticBodies} mesh_instances={counts.MeshInstances} lights={counts.Lights} loot={_lootSources.Count} graded_loot={_buildingLootPickupCount} garrison={_enemies.Count} minimap={_hud.MinimapLandmarkCount} deployment_distance={DeploymentPoint.DistanceTo(ExtractionPoint):0.0} performance={performanceReady}");
        GD.Print($"REFINERY_MAP_PASS valid={valid}");
        await WaitFrames(4);
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private async void CaptureRefineryMap()
    {
        var previousQualitySetting = _qualitySetting;
        var performanceReady = false;
        try
        {
            ApplyQuality(2);
            ApplyTimeOfDay(DeploymentTimeOfDay.Dusk);
            foreach (var enemy in _enemies)
            {
                if (IsInstanceValid(enemy))
                {
                    enemy.ProcessMode = ProcessModeEnum.Disabled;
                    enemy.Visible = false;
                }
            }
            foreach (var squadMate in _squadMates)
            {
                if (IsInstanceValid(squadMate))
                {
                    squadMate.Visible = false;
                }
            }
            foreach (var vehicle in _vehicles)
            {
                if (IsInstanceValid(vehicle))
                {
                    vehicle.Visible = false;
                }
            }
            if (IsInstanceValid(_extractionMarker))
            {
                _extractionMarker.Visible = false;
                HideGeometryRecursive(_extractionMarker);
            }
            if (_extractionAircraft is not null && IsInstanceValid(_extractionAircraft))
            {
                _extractionAircraft.Visible = false;
            }
            _player.Visible = false;
            _hud.Visible = false;
            var camera = new Camera3D { Name = "OldTownCaptureCamera", Fov = 52.0f, Far = 560.0f };
            AddChild(camera);
            camera.GlobalPosition = new Vector3(82.0f, 42.0f, 48.0f);
            camera.LookAt(new Vector3(0, 2.0f, -55.0f), Vector3.Up);
            camera.Fov = 45.0f;
            camera.MakeCurrent();
            await WaitFrames(16);
            performanceReady = PrintRefineryRenderingSnapshot("overview");
            SaveViewportImage("res://refinery_map_validation.png");
            camera.GlobalPosition = new Vector3(0, 1.72f, 64.0f);
            camera.LookAt(new Vector3(0, 1.55f, -68.0f), Vector3.Up);
            camera.Fov = 58.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("victory_street");
            SaveViewportImage("res://refinery_ground_validation.png");
            camera.GlobalPosition = new Vector3(-68.0f, 2.25f, -98.0f);
            camera.LookAt(RefineryExtractionMapBuilder.HotelCenter + Vector3.Up * 2.35f, Vector3.Up);
            camera.Fov = 56.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("guangchang_pawnshop");
            SaveViewportImage("res://refinery_hall_validation.png");
            camera.GlobalPosition = new Vector3(104.0f, 5.2f, -22.0f);
            camera.LookAt(RefineryExtractionMapBuilder.TreasuryCenter + new Vector3(-2.0f, 5.2f, 0), Vector3.Up);
            camera.Fov = 48.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("red_star_factory");
            SaveViewportImage("res://refinery_wonders_validation.png");
            camera.GlobalPosition = new Vector3(20.0f, 6.6f, -110.0f);
            camera.LookAt(new Vector3(0, 5.25f, -126.0f), Vector3.Up);
            camera.Fov = 52.0f;
            await WaitFrames(10);
            performanceReady &= PrintRefineryRenderingSnapshot("market_footbridge");
            SaveViewportImage("res://old_town_rooftop_validation.png");
            var captureDoor = _refineryDoors.FirstOrDefault();
            if (captureDoor is not null)
            {
                var outward = (captureDoor.OutsideProbe - captureDoor.InsideProbe).Normalized();
                camera.GlobalPosition = captureDoor.InteractionPoint + outward * 7.0f + Vector3.Up * 1.7f;
                camera.LookAt(captureDoor.InteractionPoint + Vector3.Up * 0.6f, Vector3.Up);
                camera.Fov = 56.0f;
                await WaitFrames(8);
                SaveViewportImage("res://refinery_door_closed_validation.png");
                captureDoor.TrySetOpen(true, bypassClearance: true);
                for (var frame = 0; frame < 120 && captureDoor.IsAnimating; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                await WaitFrames(4);
                SaveViewportImage("res://refinery_door_open_validation.png");
                captureDoor.SetOpenImmediate(false);
            }
            GD.Print($"REFINERY_MAP_CAPTURE valid={performanceReady} map_id={DeploymentMapCatalog.BlackwaterRefineryId} identity=jianghai_old_city time=dusk authored_meshes={_jianghaiOldCityScene?.MeshInstanceCount ?? 0} authored_surfaces={_jianghaiOldCityScene?.SurfaceCount ?? 0} doors={_refineryDoors.Count} paths=refinery_map_validation.png,refinery_ground_validation.png,refinery_hall_validation.png,refinery_wonders_validation.png,old_town_rooftop_validation.png,refinery_door_closed_validation.png,refinery_door_open_validation.png");
        }
        finally
        {
            ApplyQuality(previousQualitySetting);
        }
        QuitDiagnosticAfterSceneCleanup(performanceReady ? 0 : 2);
    }

    private static bool PrintRefineryRenderingSnapshot(string view)
    {
        var drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        var objects = Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        var primitives = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        var videoMemoryMb = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024.0 * 1024.0);
        var textureMemoryMb = Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024.0 * 1024.0);
        var withinBudget = drawCalls <= 2400
            && objects <= 2200
            && primitives <= 10_500_000
            && videoMemoryMb <= 1536.0
            && textureMemoryMb <= 1152.0;
        GD.Print($"REFINERY_RENDER_CHECK valid={withinBudget} view={view} draw_calls={drawCalls:0}/2400 objects={objects:0}/2200 primitives={primitives:0}/10500000 video_mem_mb={videoMemoryMb:0.0}/1536 texture_mem_mb={textureMemoryMb:0.0}/1152");
        return withinBudget;
    }

    private bool ValidateAuthoredTerminalStatusTransitions(out string summary)
    {
        var loader = _jianghaiOldCitySceneLoader;
        loader.ResetTerminalStatuses();
        var resetBefore = loader.TerminalCompletionStates;
        var resetBeforeReady = resetBefore.Count == 2 && !resetBefore[0] && !resetBefore[1];
        loader.SetTerminalCompleted(0);
        var oneCompleted = loader.TerminalCompletionStates;
        var oneCompletedReady = oneCompleted.Count == 2 && oneCompleted[0] && !oneCompleted[1];
        loader.ApplyTerminalStatuses(2);
        var bothCompleted = loader.TerminalCompletionStates;
        var bothCompletedReady = bothCompleted.Count == 2 && bothCompleted[0] && bothCompleted[1];
        loader.ResetTerminalStatuses();
        var resetAfter = loader.TerminalCompletionStates;
        var resetAfterReady = resetAfter.Count == 2 && !resetAfter[0] && !resetAfter[1];
        summary = $"0={resetBeforeReady},1={oneCompletedReady},2={bothCompletedReady},reset={resetAfterReady}";
        return resetBeforeReady && oneCompletedReady && bothCompletedReady && resetAfterReady;
    }

    private bool ValidateAuthoredQualityTiers(out string summary)
    {
        if (_jianghaiOldCityScene is not { } scene)
        {
            summary = "missing";
            return false;
        }

        var originalTier = scene.QualityTier;
        var shadowCounts = new int[3];
        var tiersReady = true;
        try
        {
            for (var tier = 0; tier < shadowCounts.Length; tier++)
            {
                _jianghaiOldCitySceneLoader.ApplyQuality(tier);
                shadowCounts[tier] = scene.ShadowCasterMeshCount;
                tiersReady &= scene.QualityTier == tier;
            }
        }
        finally
        {
            _jianghaiOldCitySceneLoader.ApplyQuality(originalTier);
        }

        tiersReady &= shadowCounts[0] > 0
            && shadowCounts[0] <= shadowCounts[1]
            && shadowCounts[1] <= shadowCounts[2]
            && shadowCounts[2] < scene.MeshInstanceCount;
        summary = $"0={shadowCounts[0]},1={shadowCounts[1]},2={shadowCounts[2]},restored={scene.QualityTier}";
        return tiersReady && scene.QualityTier == originalTier;
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
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var hotelEntryClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.HotelEntry, landmarks.HotelInterior, exclusions, 1);
        var treasuryEntryClear = !PhysicsRaycast.HasHit(
            GetWorld3D(), landmarks.TreasuryEntry, landmarks.TreasuryInterior, exclusions, 1);
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
            && landmarks.CollisionShapeCount >= 17
            && landmarks.EntryCount == 2
            && landmarks.RooftopRouteCount == 1;
        GD.Print($"OLD_TOWN_LANDMARK_CHECK hotel_entry={hotelEntryClear} treasury_entry={treasuryEntryClear} hotel_wall={hotelWallBlocks} treasury_wall={treasuryWallBlocks} rooftop_deck={rooftopDeckBlocks} rooftop_clear={rooftopWalkClear} traversal={traversalRegistered} counts={countsReady}");
        return hotelEntryClear && treasuryEntryClear && hotelWallBlocks && treasuryWallBlocks
            && rooftopDeckBlocks && rooftopWalkClear && traversalRegistered && countsReady;
    }

    private bool ValidateRefineryTerminalCollisions(out int alignedCollisions)
    {
        alignedCollisions = 0;
        var expectedPositions = new[]
        {
            RefineryLayout.RelayTerminal,
            RefineryLayout.ManifestTerminal
        };
        var expectedVisualNames = new[]
        {
            "GrandHotelSecurityTerminalVisual",
            "MunicipalTreasuryManifestTerminalVisual"
        };
        var expectedSize = new Vector3(0.86f, 2.05f, 0.84f);
        for (var index = 0; index < expectedPositions.Length; index++)
        {
            if (index >= _objectiveTerminals.Count)
            {
                continue;
            }

            var terminal = _objectiveTerminals[index];
            var body = terminal.GetChildren().OfType<StaticBody3D>().FirstOrDefault();
            var collision = body?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
            var authoredBounds = new Aabb();
            var hasAuthoredBounds = _jianghaiOldCityScene is { } authoredScene
                && authoredScene.AuthoredTerminalWorldBounds.TryGetValue(
                    expectedVisualNames[index],
                    out authoredBounds);
            if (body is null
                || collision?.Shape is not BoxShape3D box
                || !hasAuthoredBounds
                || terminal.GlobalPosition.DistanceTo(expectedPositions[index]) > 0.05f
                || body.Position.DistanceTo(Vector3.Up * (expectedSize.Y * 0.5f)) > 0.05f
                || box.Size.DistanceTo(expectedSize) > 0.05f
                || !ColliderContainsAuthoredBounds(collision, box, authoredBounds))
            {
                continue;
            }

            alignedCollisions++;
        }
        return alignedCollisions == expectedPositions.Length;
    }

    private static bool ColliderContainsAuthoredBounds(
        CollisionShape3D collision,
        BoxShape3D box,
        Aabb authoredBounds)
    {
        const float tolerance = 0.005f;
        var colliderBounds = new Aabb(
            collision.GlobalPosition - box.Size * 0.5f,
            box.Size);
        var colliderEnd = colliderBounds.Position + colliderBounds.Size;
        var authoredEnd = authoredBounds.Position + authoredBounds.Size;
        return colliderBounds.Position.X <= authoredBounds.Position.X + tolerance
            && colliderBounds.Position.Y <= authoredBounds.Position.Y + tolerance
            && colliderBounds.Position.Z <= authoredBounds.Position.Z + tolerance
            && colliderEnd.X >= authoredEnd.X - tolerance
            && colliderEnd.Y >= authoredEnd.Y - tolerance
            && colliderEnd.Z >= authoredEnd.Z - tolerance;
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
        foreach (var terminal in _objectiveTerminals)
        {
            if (!IsInstanceValid(terminal))
            {
                continue;
            }
            var terminalChildren = terminal.GetChildren();
            using var terminalChildrenBacking = terminalChildren.AsDisposable();
            foreach (var child in terminalChildren)
            {
                if (child is StaticBody3D body && IsInstanceValid(body))
                {
                    exclusions.Add(body.GetRid());
                }
            }
        }
        foreach (var door in _refineryDoors)
        {
            if (IsInstanceValid(door))
            {
                exclusions.Add(door.GetRid());
            }
        }
        return exclusions;
    }

    private static void CountRefineryNodes(
        Node node,
        bool insideAuthoredScene,
        bool insideLegacyVisualScaffold,
        bool insideLegacyCollisionProxy,
        ref RefineryRuntimeCounts counts)
    {
        counts.Nodes++;
        var authoredRoot = node.IsInGroup(JianghaiOldCitySceneLoader.AuthoredSceneGroup);
        var authored = insideAuthoredScene || authoredRoot;
        var legacyVisual = insideLegacyVisualScaffold
            || node.IsInGroup("refinery_legacy_visual_scaffold");
        var legacyCollision = insideLegacyCollisionProxy
            || node.IsInGroup("refinery_legacy_collision_proxy");
        if (authoredRoot)
        {
            counts.AuthoredSceneRoots++;
        }
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
                counts.AuthoredSceneMeshes++;
                if (mesh.IsVisibleInTree())
                {
                    counts.VisibleAuthoredSceneMeshes++;
                }
            }
        }
        if (legacyVisual && node is GeometryInstance3D legacyGeometry
            && legacyGeometry.IsVisibleInTree())
        {
            counts.VisibleLegacyScaffoldGeometry++;
        }
        if (legacyCollision && node is CollisionShape3D collision)
        {
            counts.LegacyCollisionShapes++;
            if (collision.Shape is not BoxShape3D)
            {
                counts.NonBoxLegacyCollisionShapes++;
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CountRefineryNodes(
                    childNode,
                    authored,
                    legacyVisual,
                    legacyCollision,
                    ref counts);
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
        public int AuthoredSceneRoots;
        public int AuthoredSceneMeshes;
        public int VisibleAuthoredSceneMeshes;
        public int VisibleLegacyScaffoldGeometry;
        public int LegacyCollisionShapes;
        public int NonBoxLegacyCollisionShapes;
    }
}
