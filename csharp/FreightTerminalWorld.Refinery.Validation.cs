using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // The six short-range, shadowless furnished interiors add 49 authored
    // mesh nodes. Preserve the previous structural headroom while the capture
    // diagnostic continues to enforce frame-level draw/object/memory budgets.
    private const int RefineryRuntimeMeshInstanceBudget = 820;

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
        var representedCollisionPlacements = _jianghaiGameplayCollision?.Body.GetChildren()
            .OfType<CollisionShape3D>()
            .Where(shape => shape.HasMeta("gameplay_source_placement"))
            .Select(shape => shape.GetMeta(
                "gameplay_source_placement",
                string.Empty).AsString())
            .Distinct(StringComparer.Ordinal)
            .Count() ?? 0;
        var expectedLandmarkCollisionShapes = _oldTownLandmarks?.CollisionShapeCount ?? 0;
        var gameplayCollisionReady = _jianghaiGameplayCollision is { } gameplayCollision
            && _jianghaiGameplayCollisionError is null
            && IsInstanceValid(gameplayCollision.Body)
            && gameplayCollision.Body.CollisionLayer == 1
            && gameplayCollision.Body.CollisionMask == 0
            && gameplayCollision.SourcePlacementCount == expectedCollisionProxies
            && gameplayCollision.SuppressedPlacementCount > 0
            && gameplayCollision.PlacementShapeCount >= expectedCollisionProxies
            && representedCollisionPlacements == expectedCollisionProxies
            && gameplayCollision.AuthoredSourceMeshCount
                == JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount
            && gameplayCollision.AuthoredShapeCount
                == JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount
            && gameplayCollision.DensitySourceCount
                == JianghaiGameplayCollisionContract.ExpectedDensitySourceCount
            && gameplayCollision.SolidSourceCount
                == JianghaiGameplayCollisionContract.ExpectedSolidSourceCount
            && gameplayCollision.EnterableSourceCount
                == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount
            && gameplayCollision.EnterableShapeCount
                == JianghaiGameplayCollisionContract.ExpectedEnterableShapeCount
            && gameplayCollision.CollisionShapeCount
                == gameplayCollision.PlacementShapeCount
                    + gameplayCollision.AuthoredShapeCount
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
        var expectedInteractiveDoors = (_oldTownLandmarks?.EntryCount ?? 0)
            + JianghaiInteriorPopulationService.ExpectedDoorCount;
        var interactiveDoorsReady = _oldTownLandmarks is not null
            && _jianghaiInteriors is { } interiors
            && interiors.Doors.Count == JianghaiInteriorPopulationService.ExpectedDoorCount
            && _refineryDoors.Count == expectedInteractiveDoors
            && _refineryDoors.All(door => IsInstanceValid(door)
                && door.UsesAuthoredVisual
                && door.HasBoxCollision
                && door.MotionStyle == BuildingDoorMotionStyle.Hinged);
        var interiorResidents = _civilians.Where(civilian =>
            IsInstanceValid(civilian)
            && civilian.IsInGroup("jianghai_interior_resident")).ToArray();
        var expectedInteriorResidents = 4
            + JianghaiInteriorPopulationService.ExpectedResidentCount;
        var interiorResidentsReady = _oldTownInteriorResidentCount == 4
            && _jianghaiInteriors?.Residents.Count
                == JianghaiInteriorPopulationService.ExpectedResidentCount
            && interiorResidents.Length == expectedInteriorResidents
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
            && counts.MeshInstances < RefineryRuntimeMeshInstanceBudget
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
            + $"authored_gameplay_proxies={_jianghaiGameplayCollision?.AuthoredSourceMeshCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount} "
            + $"authored_gameplay_shapes={_jianghaiGameplayCollision?.AuthoredShapeCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount} "
            + $"solid_collision={_jianghaiGameplayCollision?.SolidSourceCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedSolidSourceCount} "
            + $"carved_legacy={_jianghaiGameplayCollision?.SuppressedPlacementCount ?? 0} "
            + $"represented_legacy={representedCollisionPlacements}/{expectedCollisionProxies} "
            + $"enterable_collision={_jianghaiGameplayCollision?.EnterableSourceCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount}:"
            + $"{_jianghaiGameplayCollision?.EnterableShapeCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedEnterableShapeCount} "
            + $"gameplay_error={(_jianghaiGameplayCollisionError is null ? "none" : "build_failed")} "
            + $"building_physics={buildingPhysicsReady}:{buildingHitCount}/"
            + $"{JianghaiBuildingBlockingProbeCount}:"
            + $"{buildingClearCount}/3:{buildingPhysicsSummary} "
            + $"legacy_visible={counts.VisibleLegacyScaffoldGeometry} "
            + $"scaffolds_hidden={legacyScaffoldsHidden} "
            + $"proxies={_refineryCollisionProxyCount}/"
            + $"{_jianghaiGameplayCollision?.CollisionShapeCount ?? 0}:"
            + $"{proxiesReady} districts={_oldTownDistricts.Count}:{districtsReady} "
            + $"doors={_refineryDoors.Count}/{expectedInteractiveDoors}:"
            + $"{interactiveDoorsReady} interior_residents={interiorResidents.Length}/"
            + $"{expectedInteriorResidents}:old={_oldTownInteriorResidentCount}:"
            + $"new={_jianghaiInteriors?.Residents.Count ?? 0}:"
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
            + $"mesh_instances={counts.MeshInstances}/"
            + $"{RefineryRuntimeMeshInstanceBudget} lights={counts.Lights} "
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
}
