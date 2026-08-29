using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // File-size exception (docs/ENGINEERING_STANDARDS.md): this legacy partial still
    // couples refinery spawning with the existing end-to-end map diagnostic helpers.
    // This change moves capture and valley inspection into focused partials; the next
    // refinery-diagnostic edit must extract ValidateRefineryMap and its helper cohort
    // into FreightTerminalWorld.Refinery.Validation.cs before adding another validator.
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

    private void SpawnOldTownInteriorResidents()
    {
        _oldTownInteriorResidentCount = 0;
        var residents = new (Vector3 Position, CivilianRole Role, Vector2 Roam, OperatorVisualId Visual)[]
        {
            (new Vector3(-85.4f, 0.14f, -119.5f), CivilianRole.Resident, new Vector2(0.8f, 1.0f), OperatorVisualId.Magpie),
            (new Vector3(-90.5f, 0.14f, -126.0f), CivilianRole.VolunteerMedic, new Vector2(1.2f, 1.4f), OperatorVisualId.Heron),
            (new Vector3(85.4f, 0.14f, -1.0f), CivilianRole.UtilityWorker, new Vector2(0.8f, 1.0f), OperatorVisualId.Jackal),
            (new Vector3(90.0f, 0.14f, 1.5f), CivilianRole.CommunityGuard, new Vector2(1.2f, 1.3f), OperatorVisualId.Viper)
        };
        for (var index = 0; index < residents.Length; index++)
        {
            var placement = residents[index];
            var civilian = new CivilianNpc
            {
                Name = $"JianghaiInteriorResident_{index + 1:00}"
            };
            civilian.UseAuthoredVisual(placement.Visual);
            civilian.Configure(
                this,
                placement.Role,
                100 + index,
                0,
                Transform3D.Identity,
                placement.Position,
                placement.Roam);
            RegisterResidentialLanguageRefresher(civilian.SetLanguage);
            _levelRoot.AddChild(civilian);
            civilian.AddToGroup("jianghai_interior_resident");
            _civilians.Add(civilian);
            _oldTownInteriorResidentCount++;
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
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var counts = new RefineryRuntimeCounts();
        CountRefineryNodes(_levelRoot, false, false, false, false, ref counts);

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
            && authored.ValleyFoundationMeshCount == 1
            && authored.ValleyMountainMeshCount == 12
            && authored.ValleyExpectedMountainNameCount == 12
            && authored.ValleyUniqueMountainMeshCount == 1
            && authored.ValleyCollisionNodeCount == 0
            && authored.ValleyHierarchyReady
            && authored.ValleyMountainsOutsidePlayableBounds
            && authored.ValleyMountainMaxAngularGapRadians <= 0.55f
            && authored.ValleyInstanceTriangleCount is >= 300_000 and <= 380_000
            && authored.ValleyWorldBounds.Size.X >= 900.0f
            && authored.ValleyWorldBounds.Size.Y >= 60.0f
            && authored.ValleyWorldBounds.Size.Z >= 900.0f
            && authored.ValleyFoundationWorldBounds.Size.X is >= 339.0f and <= 341.0f
            && authored.ValleyFoundationWorldBounds.Size.Y is >= 0.10f and <= 0.14f
            && authored.ValleyFoundationWorldBounds.Size.Z is >= 319.0f and <= 321.0f
            && authored.ValleyFoundationVertexCount == 112
            && authored.ValleyFoundationTriangleCount == 188
            && authored.ValleyFoundationGeometryReady
            && authored.ValleyFoundationMinHeight is >= -0.20f and <= -0.15f
            && authored.ValleyFoundationMaxHeight is >= -0.08f and <= -0.04f
            && authored.ValleyFoundationMaxHeight
                - authored.ValleyFoundationMinHeight is >= 0.10f and <= 0.14f
            && authored.ValleyFoundationMaterialsReady
            && authored.ValleyFoundationUvReady
            && authored.RenderBatchValidation.Valid
            && authored.RenderBatchValidation.BatchCount > 0
            && authored.RenderBatchValidation.SourceCount
                > authored.RenderBatchValidation.BatchCount
            && authored.RenderBatchValidation.NonOriginBatchCount > 0
            && authored.RenderBatchValidation.MaximumCentroidError <= 0.001f
            && authored.RenderBatchValidation.MaximumPositionError <= 0.001f
            && authored.RenderBatchValidation.MaximumBasisError <= 0.001f
            && authored.RenderBatchValidation.MaximumVisibilityShortfall <= 0.001f
            && counts.AuthoredSceneRoots == 1
            && counts.AuthoredSceneMeshes == authored.MeshInstanceCount
            && counts.VisibleAuthoredSceneMeshes == counts.AuthoredSceneMeshes;
        var expectedCollisionProxies = RefineryLayout.Models.Count(
            placement => placement.HasCollision);
        var expectedLandmarkCollisionShapes = _oldTownLandmarks?.CollisionShapeCount ?? 0;
        var gameplayCollisionReady = _jianghaiGameplayCollision is { } gameplayCollision
            && _jianghaiGameplayCollisionError is null
            && IsInstanceValid(gameplayCollision.Body)
            && gameplayCollision.Body.CollisionLayer == 1
            && gameplayCollision.Body.CollisionMask == 0
            && gameplayCollision.SourcePlacementCount == expectedCollisionProxies
            && gameplayCollision.AuthoredSourceMeshCount == 6
            && gameplayCollision.CollisionShapeCount == expectedCollisionProxies + 6
            && gameplayCollision.BoxShapeCount == gameplayCollision.CollisionShapeCount
            && gameplayCollision.ConcaveShapeCount == 0
            && gameplayCollision.DistrictShapeCounts.Count >= 10
            && counts.GameplayCollisionBodies == 2
            && counts.GameplayCollisionShapes
                == gameplayCollision.CollisionShapeCount + expectedLandmarkCollisionShapes
            && counts.GameplayBoxCollisionShapes == counts.GameplayCollisionShapes
            && counts.GameplayNonBoxCollisionShapes == 0;
        var proxiesReady = _refineryCollisionProxyCount
                == _jianghaiGameplayCollision?.CollisionShapeCount
            && gameplayCollisionReady
            && counts.LegacyCollisionShapes == 0
            && counts.LegacyCollisionBodies == 0
            && counts.NonBoxLegacyCollisionShapes == 0;
        var legacyScaffoldsHidden = counts.VisibleLegacyScaffoldGeometry == 0;
        var interactiveDoorsReady = _oldTownLandmarks is { } landmarks
            && _refineryDoors.Count == landmarks.EntryCount
            && _refineryDoors.All(door => IsInstanceValid(door)
                && door.UsesAuthoredVisual
                && door.HasBoxCollision
                && door.MotionStyle == BuildingDoorMotionStyle.Hinged);
        var interiorResidents = _civilians.Where(civilian =>
            IsInstanceValid(civilian)
            && civilian.IsInGroup("jianghai_interior_resident")).ToArray();
        var interiorResidentsReady = _oldTownInteriorResidentCount == 4
            && interiorResidents.Length == 4
            && interiorResidents.All(civilian =>
                civilian.UsesAuthoredVisualForDiagnostics
                && civilian.AuthoredVisualIdForDiagnostics is not null);
        var districtsReady = _oldTownDistricts.Count >= 10
            && _oldTownDistricts.Contains("grand_hotel")
            && _oldTownDistricts.Contains("municipal_treasury")
            && _oldTownDistricts.Contains("founders_plaza");
        var highValueReady = ValidateOldTownHighValueZones(out var zoneSeparation, out var zoneSummary);
        var highValueAccessReady = ValidateHighValueLootAccess(
            out var accessibleHighValueLoot,
            out var highValueAccessBlocker);
        var routeReady = ValidateOldTownRouteProbes(out var routeProbeCount, out var routeBlocker);
        var buildingPhysicsReady = ValidateJianghaiBuildingCollision(
            out var buildingHitCount,
            out var buildingClearCount,
            out var buildingPhysicsSummary);
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
            && _hud.MinimapLandmarkCount >= 10
            && interiorResidentsReady;
        var deploymentForward = -_player.GlobalBasis.Z;
        deploymentForward.Y = 0.0f;
        var expectedDeploymentForward = JianghaiExtractionSpawnLayout.PlayerLookTarget
            - JianghaiExtractionSpawnLayout.PlayerPad;
        expectedDeploymentForward.Y = 0.0f;
        var deploymentGeometryReady = ValidateJianghaiDeploymentGeometry(
            out var deploymentPositionsChecked,
            out var deploymentBlocker);
        var deploymentReady = DeploymentPoint.DistanceTo(ExtractionPoint) > 80.0f
            && DeploymentPoint.DistanceTo(JianghaiExtractionSpawnLayout.PlayerPad) <= 0.05f
            && Mathf.Abs(DeploymentPoint.X) < 150.0f
            && deploymentForward.Normalized().Dot(expectedDeploymentForward.Normalized()) >= 0.98f
            && Mathf.Abs(DeploymentPoint.X + 0.5f) >= 3.0f
            && deploymentGeometryReady;
        var performanceReady = counts.Nodes < 1900
            && counts.StaticBodies < 125
            && counts.MeshInstances < 770
            && counts.Lights <= 32
            && _jianghaiOldCityScene is { } performanceScene
            && performanceScene.InstanceTriangleCount <= 5_000_000
            && performanceScene.ShadowCasterMeshCount > 0
            && performanceScene.ShadowCasterMeshCount < performanceScene.MeshInstanceCount;
        var valid = rootsReady && localizationReady && authoredReady && proxiesReady
            && gameplayCollisionReady && buildingPhysicsReady
            && legacyScaffoldsHidden && interactiveDoorsReady && districtsReady
            && highValueReady && highValueAccessReady && routeReady
            && landmarkReady && authoredQualityReady && gameplayReady && deploymentReady && performanceReady;
        var valleyBoundsSummary = _jianghaiOldCityScene is { } valleyScene
            ? $"{valleyScene.ValleyWorldBounds.Size}:{valleyScene.ValleyFoundationWorldBounds.Size}"
            : "missing";

        GD.Print(
            $"REFINERY_MAP_CHECK valid={valid} "
            + $"map_id={DeploymentMapCatalog.BlackwaterRefineryId} identity=jianghai_old_city "
            + $"root={rootsReady} localization={localizationReady} authored={authoredReady} "
            + $"authored_path={_jianghaiOldCityScene?.ScenePath ?? "missing"} "
            + $"authored_error={(_jianghaiOldCitySceneLoadError is null ? "none" : "load_failed")} "
            + $"authored_roots={counts.AuthoredSceneRoots} "
            + $"authored_meshes={_jianghaiOldCityScene?.MeshInstanceCount ?? 0} "
            + $"authored_surfaces={_jianghaiOldCityScene?.SurfaceCount ?? 0} "
            + $"authored_material_surfaces={_jianghaiOldCityScene?.MaterialSurfaceCount ?? 0} "
            + $"authored_triangles={_jianghaiOldCityScene?.InstanceTriangleCount ?? 0} "
            + $"authored_anchors={_jianghaiOldCityScene?.RequiredAnchorCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.RequiredAnchorTotal ?? 0} "
            + $"authored_terminals={_jianghaiOldCityScene?.AuthoredTerminalCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.VisibleAuthoredTerminalCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.AlignedAuthoredTerminalCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.AuthoredTerminalTotal ?? 0} "
            + $"authored_screens={_jianghaiOldCityScene?.AuthoredStatusScreenCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.AuthoredStatusScreenTotal ?? 0} "
            + $"terminal_statuses={terminalStatusesReady}:{terminalStatusSummary} "
            + $"authored_detail={_jianghaiOldCityScene?.DetailMeshCount ?? 0} "
            + $"valley={_jianghaiOldCityScene?.ValleyFoundationMeshCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.ValleyMountainMeshCount ?? 0}:"
            + $"{_jianghaiOldCityScene?.ValleyInstanceTriangleCount ?? 0} "
            + $"valley_bounds={valleyBoundsSummary} "
            + $"valley_contract={_jianghaiOldCityScene?.ValleyHierarchyReady ?? false}:"
            + $"{_jianghaiOldCityScene?.ValleyExpectedMountainNameCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.ValleyUniqueMountainMeshCount ?? 0}:"
            + $"collision={_jianghaiOldCityScene?.ValleyCollisionNodeCount ?? -1}:"
            + $"outside={_jianghaiOldCityScene?.ValleyMountainsOutsidePlayableBounds ?? false}:"
            + $"gap={_jianghaiOldCityScene?.ValleyMountainMaxAngularGapRadians ?? -1.0f:0.000} "
            + $"authored_quality={_jianghaiOldCityScene?.QualityTier ?? -1} "
            + $"authored_shadows={_jianghaiOldCityScene?.ShadowCasterMeshCount ?? 0} "
            + $"authored_quality_tiers={authoredQualityReady}:{authoredQualitySummary} "
            + $"render_batches={_jianghaiOldCityScene?.RenderBatchValidation.Valid ?? false}:"
            + $"{_jianghaiOldCityScene?.RenderBatchValidation.BatchCount ?? 0}/"
            + $"{_jianghaiOldCityScene?.RenderBatchValidation.SourceCount ?? 0}:"
            + $"non_origin={_jianghaiOldCityScene?.RenderBatchValidation.NonOriginBatchCount ?? 0}:"
            + $"centroid_error={_jianghaiOldCityScene?.RenderBatchValidation.MaximumCentroidError ?? -1.0f:0.000000}:"
            + $"position_error={_jianghaiOldCityScene?.RenderBatchValidation.MaximumPositionError ?? -1.0f:0.000000}:"
            + $"basis_error={_jianghaiOldCityScene?.RenderBatchValidation.MaximumBasisError ?? -1.0f:0.000000}:"
            + $"radius={_jianghaiOldCityScene?.RenderBatchValidation.MaximumBatchRadius ?? -1.0f:0.00}:"
            + $"range_shortfall={_jianghaiOldCityScene?.RenderBatchValidation.MaximumVisibilityShortfall ?? -1.0f:0.000000} "
            + $"terminal_collision={terminalCollisionReady} "
            + $"terminal_collision_aligned={alignedTerminalCollisions}/2 "
            + $"gameplay_collision={gameplayCollisionReady} "
            + $"gameplay_shapes={counts.GameplayCollisionShapes}/"
            + $"{(_jianghaiGameplayCollision?.CollisionShapeCount ?? 0) + expectedLandmarkCollisionShapes} "
            + $"gameplay_boxes={counts.GameplayBoxCollisionShapes}/"
            + $"{(_jianghaiGameplayCollision?.CollisionShapeCount ?? 0) + expectedLandmarkCollisionShapes} "
            + $"gameplay_concave={_jianghaiGameplayCollision?.ConcaveShapeCount ?? -1}/0 "
            + $"authored_gameplay_proxies={_jianghaiGameplayCollision?.AuthoredSourceMeshCount ?? 0}/6 "
            + $"gameplay_error={(_jianghaiGameplayCollisionError is null ? "none" : "build_failed")} "
            + $"building_physics={buildingPhysicsReady}:{buildingHitCount}/11:"
            + $"{buildingClearCount}/3:{buildingPhysicsSummary} "
            + $"legacy_visible={counts.VisibleLegacyScaffoldGeometry} "
            + $"scaffolds_hidden={legacyScaffoldsHidden} "
            + $"proxies={_refineryCollisionProxyCount}/"
            + $"{_jianghaiGameplayCollision?.CollisionShapeCount ?? 0}:"
            + $"{proxiesReady} districts={_oldTownDistricts.Count}:{districtsReady} "
            + $"doors={_refineryDoors.Count}/{_oldTownLandmarks?.EntryCount ?? 0}:"
            + $"{interactiveDoorsReady} interior_residents={_oldTownInteriorResidentCount}/4:"
            + $"{interiorResidentsReady} high_value={highValueReady} "
            + $"high_value_access={highValueAccessReady}:{accessibleHighValueLoot}/12:"
            + $"{highValueAccessBlocker} zone_separation={zoneSeparation:0.0} "
            + $"zone_summary={zoneSummary} routes={routeReady} "
            + $"route_probes={routeProbeCount} route_blocker={routeBlocker} "
            + $"landmarks={landmarkReady} "
            + $"landmark_collision={_oldTownLandmarks?.CollisionShapeCount ?? 0} "
            + $"rooftop_routes={_oldTownLandmarks?.RooftopRouteCount ?? 0} "
            + $"deployment_spawn={deploymentReady}:{DeploymentPoint} "
            + $"deployment_geometry={deploymentGeometryReady}:"
            + $"{deploymentPositionsChecked}/15:{deploymentBlocker} "
            + $"nodes={counts.Nodes} static_bodies={counts.StaticBodies} "
            + $"mesh_instances={counts.MeshInstances} lights={counts.Lights} "
            + $"loot={_lootSources.Count} graded_loot={_buildingLootPickupCount} "
            + $"garrison={_enemies.Count} minimap={_hud.MinimapLandmarkCount} "
            + $"deployment_distance={DeploymentPoint.DistanceTo(ExtractionPoint):0.0} "
            + $"performance={performanceReady}");
        GD.Print($"REFINERY_MAP_PASS valid={valid}");
        await WaitFrames(4);
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
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

    private bool ValidateHighValueLootAccess(
        out int accessibleLoot,
        out string firstBlocker)
    {
        accessibleLoot = 0;
        firstBlocker = "none";
        var exclusions = BuildRefineryLaneExclusions();
        foreach (var lootSource in _lootSources)
        {
            if (lootSource.LootNode is CollisionObject3D collisionObject
                && IsInstanceValid(collisionObject)
                && !exclusions.Contains(collisionObject.GetRid()))
            {
                exclusions.Add(collisionObject.GetRid());
            }
        }
        using var exclusionsBacking = exclusions.AsDisposable();
        using var capsule = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = capsule,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f,
            Exclude = exclusions
        };
        var space = GetWorld3D().DirectSpaceState;
        foreach (var zone in RefineryLayout.HighValueZones)
        {
            var origin = zone.Id == "grand_hotel"
                ? new Vector3(-86.0f, 0.2f, -118.1f)
                : new Vector3(86.0f, 0.2f, -2.0f);
            var positions = RefineryLayout.LootPlacements
                .Where(loot => loot.Grade >= LootGrade.Epic
                    && HorizontalDistance(loot.Position, zone.Center) <= zone.Radius)
                .Select(loot => loot.Position)
                .Concat(RefineryLayout.ValuablePlacements
                    .Where(loot => loot.Grade >= LootGrade.Epic
                        && HorizontalDistance(loot.Position, zone.Center) <= zone.Radius)
                    .Select(loot => loot.Position))
                .ToArray();
            if (positions.Length != 6)
            {
                firstBlocker = $"{zone.Id}:count_{positions.Length}";
                return false;
            }

            foreach (var target in positions)
            {
                var samples = Mathf.Max(1, Mathf.CeilToInt(origin.DistanceTo(target) / 0.45f));
                for (var sample = 0; sample <= samples; sample++)
                {
                    var feet = origin.Lerp(target, sample / (float)samples);
                    query.Transform = new Transform3D(
                        Basis.Identity,
                        feet + Vector3.Up * 0.915f);
                    var hits = space.IntersectShape(query, 8);
                    using var hitsBacking = hits.AsDisposable();
                    if (hits.Count == 0)
                    {
                        continue;
                    }
                    using var hit = hits[0];
                    using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                    firstBlocker = colliderValue.AsGodotObject() is Node collider
                        ? $"{zone.Id}:{target.X:0.0},{target.Z:0.0}:{collider.Name}"
                        : $"{zone.Id}:{target.X:0.0},{target.Z:0.0}:unknown";
                    return false;
                }
                accessibleLoot++;
            }
        }
        return accessibleLoot == 12;
    }

    private bool ValidateJianghaiBuildingCollision(
        out int blockingHits,
        out int clearRoutes,
        out string summary)
    {
        blockingHits = 0;
        clearRoutes = 0;
        summary = "ok";
        var exclusions = BuildRefineryLaneExclusions();
        using var exclusionsBacking = exclusions.AsDisposable();
        var blockingProbes = new[]
        {
            ("west_clock", new Vector3(-8, 1.35f, 26), new Vector3(-24, 1.35f, 26)),
            ("east_founders", new Vector3(25, 1.35f, -40), new Vector3(36, 1.35f, -40)),
            ("pawnshop_wall", new Vector3(-100, 1.0f, -124), new Vector3(-96, 1.0f, -124)),
            ("factory_gate", new Vector3(80.0f, 2.2f, -7.924f), new Vector3(83.0f, 2.2f, -7.924f)),
            ("market_rooftop_deck", new Vector3(0, 7.0f, -126), new Vector3(0, 2.0f, -126)),
            ("edge_west_04", new Vector3(-165, 1.35f, -32), new Vector3(-150, 1.35f, -32)),
            ("edge_east_04", new Vector3(165, 1.35f, -32), new Vector3(150, 1.35f, -32)),
            ("edge_west_05", new Vector3(-138, 1.35f, -60), new Vector3(-123, 1.35f, -60)),
            ("edge_east_05", new Vector3(138, 1.35f, -60), new Vector3(123, 1.35f, -60)),
            ("edge_west_06", new Vector3(-165, 1.35f, -88), new Vector3(-150, 1.35f, -88)),
            ("edge_east_06", new Vector3(165, 1.35f, -88), new Vector3(150, 1.35f, -88))
        };
        foreach (var probe in blockingProbes)
        {
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    probe.Item2,
                    probe.Item3,
                    exclusions,
                    1,
                    out var hit)
                || hit.Collider is not Node collider
                || !collider.IsInGroup(JianghaiGameplayCollisionBuilder.CollisionGroup))
            {
                summary = $"block:{probe.Item1}";
                return false;
            }
            blockingHits++;
        }

        var clearProbes = new[]
        {
            ("truck_low", new Vector3(-2.0f, 0.45f, 88), new Vector3(-2.0f, 0.45f, -212)),
            ("truck_mid", new Vector3(-0.5f, 1.4f, 88), new Vector3(-0.5f, 1.4f, -212)),
            ("truck_high", new Vector3(1.0f, 2.6f, 88), new Vector3(1.0f, 2.6f, -212))
        };
        foreach (var probe in clearProbes)
        {
            if (PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    probe.Item2,
                    probe.Item3,
                    exclusions,
                    1,
                    out var hit))
            {
                summary = hit.Collider is Node blocker
                    ? $"clear:{probe.Item1}:{blocker.Name}"
                    : $"clear:{probe.Item1}:unknown";
                return false;
            }
            clearRoutes++;
        }
        return blockingHits == blockingProbes.Length && clearRoutes == clearProbes.Length;
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
        var pawnshopAirProbes = new[]
        {
            (From: new Vector3(-86, 3.0f, -135.4f), To: new Vector3(-86, 3.0f, -136.6f)),
            (From: new Vector3(-98.0f, 3.0f, -122.1f), To: new Vector3(-98.0f, 3.0f, -122.9f)),
            (From: new Vector3(-94.5f, 3.5f, -111.4f), To: new Vector3(-94.5f, 3.5f, -112.3f))
        };
        var pawnshopAirClear = pawnshopAirProbes.All(probe =>
            !PhysicsRaycast.HasHit(GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var pawnshopVisibleBlocks = new[]
        {
            (From: new Vector3(-86, 1.0f, -135.4f), To: new Vector3(-86, 1.0f, -136.6f)),
            (From: new Vector3(-98.0f, 1.0f, -122.1f), To: new Vector3(-98.0f, 1.0f, -122.9f)),
            (From: new Vector3(-94.5f, 1.0f, -111.4f), To: new Vector3(-94.5f, 1.0f, -112.3f))
        }.Count(probe => PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var factoryAirWallProbes = new[]
        {
            (From: new Vector3(77, 1.2f, -10), To: new Vector3(77, 1.2f, -6)),
            (From: new Vector3(95, 1.2f, -10), To: new Vector3(95, 1.2f, -6)),
            (From: new Vector3(72, 1.2f, -1), To: new Vector3(76, 1.2f, -1)),
            (From: new Vector3(96, 1.2f, -1), To: new Vector3(100, 1.2f, -1)),
            (From: new Vector3(86, 1.2f, 13), To: new Vector3(86, 1.2f, 18))
        };
        var factoryAirWallsClear = factoryAirWallProbes.All(probe =>
            !PhysicsRaycast.HasHit(GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var factoryGateBlocks = new[]
        {
            (From: new Vector3(80.6f, 2.2f, -7.924f), To: new Vector3(83.0f, 2.2f, -7.924f)),
            (From: new Vector3(89.0f, 2.2f, -7.924f), To: new Vector3(91.4f, 2.2f, -7.924f)),
            (From: new Vector3(85.9f, 6.5f, -7.924f), To: new Vector3(85.9f, 4.0f, -7.924f))
        }.Count(probe => PhysicsRaycast.HasHit(GetWorld3D(), probe.From, probe.To, 1));
        var rooftopDeckBlocks = PhysicsRaycast.HasHit(
            GetWorld3D(), new Vector3(0, 7.0f, -126), new Vector3(0, 2.0f, -126), 1);
        var marketRailBlocks = new[]
        {
            (From: new Vector3(1.5f, 4.845f, -123.5f), To: new Vector3(1.5f, 4.845f, -124.3f)),
            (From: new Vector3(1.5f, 5.445f, -123.5f), To: new Vector3(1.5f, 5.445f, -124.3f)),
            (From: new Vector3(1.5f, 4.845f, -127.7f), To: new Vector3(1.5f, 4.845f, -128.4f)),
            (From: new Vector3(1.5f, 5.445f, -127.7f), To: new Vector3(1.5f, 5.445f, -128.4f))
        }.Count(probe => PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var marketRailGapsClear = new[]
        {
            (From: new Vector3(1.5f, 5.15f, -123.5f), To: new Vector3(1.5f, 5.15f, -124.3f)),
            (From: new Vector3(1.5f, 5.15f, -127.7f), To: new Vector3(1.5f, 5.15f, -128.4f))
        }.All(probe => !PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var marketRailPostsBlock = new[]
        {
            (From: new Vector3(0, 5.15f, -123.5f), To: new Vector3(0, 5.15f, -124.3f)),
            (From: new Vector3(0, 5.15f, -127.7f), To: new Vector3(0, 5.15f, -128.4f))
        }.Count(probe => PhysicsRaycast.HasHit(
            GetWorld3D(), probe.From, probe.To, exclusions, 1));
        var rooftopWalkClear = ValidateRooftopCapsuleRoute(
            landmarks.RooftopRoute,
            out var rooftopBlocker);
        var traversalRegistered = _squadTraversalLinks.Any(link =>
            link.Source == "old_town_market_rooftop"
            && link.Bidirectional
            && link.ForwardPoints.Length >= 8);
        var countsReady = landmarks.LandmarkCount == 3
            && landmarks.HighValueZoneCount == 2
            && landmarks.CollisionShapeCount == 20
            && landmarks.EntryCount == 2
            && landmarks.RooftopRouteCount == 1;
        GD.Print($"OLD_TOWN_LANDMARK_CHECK hotel_entry={hotelEntryClear} treasury_entry={treasuryEntryClear} hotel_wall={hotelWallBlocks} pawnshop_air_clear={pawnshopAirClear}:3 pawnshop_visible={pawnshopVisibleBlocks}/3 factory_air_clear={factoryAirWallsClear}:5 factory_gate={factoryGateBlocks}/3 rooftop_deck={rooftopDeckBlocks} rail_blocks={marketRailBlocks}/4 rail_gaps={marketRailGapsClear}:2 rail_posts={marketRailPostsBlock}/2 rooftop_clear={rooftopWalkClear}:{rooftopBlocker} traversal={traversalRegistered} counts={countsReady}:20");
        return hotelEntryClear && treasuryEntryClear && hotelWallBlocks
            && pawnshopAirClear && pawnshopVisibleBlocks == 3
            && factoryAirWallsClear && factoryGateBlocks == 3
            && rooftopDeckBlocks && marketRailBlocks == 4
            && marketRailGapsClear && marketRailPostsBlock == 2
            && rooftopWalkClear && traversalRegistered && countsReady;
    }

    private bool ValidateRooftopCapsuleRoute(
        IReadOnlyList<Vector3> route,
        out string blocker)
    {
        blocker = "none";
        if (route.Count < 8)
        {
            blocker = "route_missing";
            return false;
        }

        const float capsuleHeight = 1.75f;
        using var capsule = new CapsuleShape3D { Radius = 0.50f, Height = capsuleHeight };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = capsule,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f
        };
        var space = GetWorld3D().DirectSpaceState;
        const int firstElevatedDeckPoint = 3;
        var lastElevatedDeckPoint = route.Count - 4;
        for (var segment = firstElevatedDeckPoint; segment < lastElevatedDeckPoint; segment++)
        {
            var from = route[segment];
            var to = route[segment + 1];
            var samples = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 0.50f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var feet = from.Lerp(to, sample / (float)samples);
                query.Transform = new Transform3D(
                    Basis.Identity,
                    feet + Vector3.Up * (capsuleHeight * 0.5f + 0.04f));
                var hits = space.IntersectShape(query, 8);
                using var hitsBacking = hits.AsDisposable();
                if (hits.Count == 0)
                {
                    continue;
                }
                using var hit = hits[0];
                using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                blocker = colliderValue.AsGodotObject() is Node collider
                    ? $"segment_{segment}:{collider.Name}"
                    : $"segment_{segment}:unknown";
                return false;
            }
        }
        return true;
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
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                exclusions.Add(civilian.GetRid());
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
        bool insideGameplayCollision,
        ref RefineryRuntimeCounts counts)
    {
        counts.Nodes++;
        var authoredRoot = node.IsInGroup(JianghaiOldCitySceneLoader.AuthoredSceneGroup);
        var authored = insideAuthoredScene || authoredRoot;
        var legacyVisual = insideLegacyVisualScaffold
            || node.IsInGroup("refinery_legacy_visual_scaffold");
        var legacyCollision = insideLegacyCollisionProxy
            || node.IsInGroup("refinery_legacy_collision_proxy");
        var gameplayCollisionRoot = node.IsInGroup(
            JianghaiGameplayCollisionBuilder.CollisionGroup);
        var gameplayCollision = insideGameplayCollision || gameplayCollisionRoot;
        if (authoredRoot)
        {
            counts.AuthoredSceneRoots++;
        }
        if (node is StaticBody3D)
        {
            counts.StaticBodies++;
            if (gameplayCollisionRoot)
            {
                counts.GameplayCollisionBodies++;
            }
            if (legacyCollision)
            {
                counts.LegacyCollisionBodies++;
            }
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
        if (gameplayCollision && node is CollisionShape3D gameplayCollisionShape)
        {
            counts.GameplayCollisionShapes++;
            if (gameplayCollisionShape.Shape is BoxShape3D)
            {
                counts.GameplayBoxCollisionShapes++;
            }
            else
            {
                counts.GameplayNonBoxCollisionShapes++;
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
                    gameplayCollision,
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
        public int LegacyCollisionBodies;
        public int NonBoxLegacyCollisionShapes;
        public int GameplayCollisionBodies;
        public int GameplayCollisionShapes;
        public int GameplayBoxCollisionShapes;
        public int GameplayNonBoxCollisionShapes;
    }
}
