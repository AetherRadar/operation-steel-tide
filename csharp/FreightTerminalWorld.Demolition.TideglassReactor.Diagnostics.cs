using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateTideglassReactor()
    {
        await WaitFrames(3);
        _demolitionSelectedMapId = DemolitionMapCatalog.TideglassReactorId;
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.Print("TIDEGLASS_REACTOR_CHECK valid=False built=False");
            GD.Print("TIDEGLASS_REACTOR_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        var arena = _demolitionArena;
        var layout = arena.Layout;
        var initiallyIsolated = !arena.Active
            && !arena.Root.Visible
            && arena.Root.ProcessMode == ProcessModeEnum.Disabled
            && arena.ActiveCollisionBodyCount == 0
            && arena.AllStaticBodiesUseWorldLayer();
        GetTree().Paused = false;
        DisableActorsForSurvivalDiagnostics();
        arena.SetActive(true);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var offer = DemolitionMapCatalog.Resolve(DemolitionMapCatalog.TideglassReactorId);
        var catalogReady = offer.Id == DemolitionMapCatalog.TideglassReactorId
            && offer.Code == "MAP 04"
            && offer.Available
            && offer.LocalizationKey == "demolition_map_tideglass_reactor"
            && offer.SubtitleLocalizationKey == "demolition_map_tideglass_reactor_subtitle"
            && offer.ProfileLocalizationKey == "demolition_map_tideglass_reactor_profile"
            && DemolitionMapCatalog.IsAvailable(DemolitionMapCatalog.TideglassReactorId);
        var localizationReady = GameLocalization.Get(
                "demolition_map_tideglass_reactor",
                "zh",
                "TIDEGLASS REACTOR") != "TIDEGLASS REACTOR"
            && GameLocalization.Get(
                "demolition_map_tideglass_reactor_subtitle",
                "zh",
                "CONSTRUCTION DISTRICT") != "CONSTRUCTION DISTRICT"
            && GameLocalization.Get(
                "demolition_map_tideglass_reactor_profile",
                "zh",
                "THREE DISTRICTS") != "THREE DISTRICTS";

        var tideforge = new DemolitionArenaLayout(DemolitionMapCatalog.TideforgeId, layout.Origin);
        var harborLocks = new DemolitionArenaLayout(DemolitionMapCatalog.HarborLocksId, layout.Origin);
        var layoutsDistinct = TideglassLayoutsDiffer(layout, tideforge)
            && TideglassLayoutsDiffer(layout, harborLocks)
            && TideglassLayoutsDiffer(tideforge, harborLocks);

        var dressingRoot = arena.Root.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing");
        var importedProps = layout.Props.Count(prop => TideglassPropModelLoaded(arena.Root, prop));
        var boundsFailures = layout.Props
            .Where(prop => !HarborPropInsideBounds(layout, prop))
            .Select(prop => prop.Name)
            .ToArray();
        var propsInsideBounds = boundsFailures.Length == 0;
        var propsSeparated = HarborPropsSeparated(layout.Props, out var overlapPair);
        var sitesClear = layout.SitePositions.All(site => layout.Props.All(prop =>
            !HarborPointOverlapsProp(site, 3.4f, prop)));
        var spawnsClear = layout.AttackSpawns.Concat(layout.DefenderSpawns).All(spawn =>
            layout.Props.All(prop => !HarborPointOverlapsProp(spawn, 0.8f, prop)));
        var collisionCoverageFailures = layout.Props
            .Where(prop => !HarborPropCollisionCoversModel(arena.Root, prop))
            .Select(prop => prop.Name)
            .ToArray();
        var tightCollisionFailures = layout.Props
            .Where(prop => !TideglassPropCollisionTightlyFitsModel(arena.Root, prop))
            .Select(prop => prop.Name)
            .ToArray();
        var collisionCoverage = collisionCoverageFailures.Length == 0
            && tightCollisionFailures.Length == 0;
        var authoredCollisionOnly = layout.CollisionBoxes.All(box => !box.Visible)
            && layout.DetailBoxes.Count == 0;
        var dressingBoundsReady = TideglassDressingModelsInsideBounds(
            dressingRoot,
            layout,
            out var dressingBoundsFailures);
        var perimeterFenceReady = TideglassPerimeterFenceReady(dressingRoot, layout);
        var perimeterGatesReady = TideglassPerimeterGatesReady(
            dressingRoot,
            layout,
            GetWorld3D(),
            out var perimeterGateFailures);
        var solidMaterialsReady = TideglassSolidMaterialsReady(
            arena.Root,
            dressingRoot,
            layout,
            out var solidMaterialFailures);
        var roadSurfacesReady = TideglassRoadSurfacesAligned(
            dressingRoot,
            layout,
            out var roadSurfaceFailures);
        var towerCollisionReady = TideglassTowerCollisionReady(layout);
        var walkwayCollisionReady = TideglassWalkwayCollisionReady(
            arena.Root,
            dressingRoot,
            layout,
            out var walkwayCollisionFailure);
        var gatewayCollisionReady = TideglassGatewayCollisionReady(
            dressingRoot,
            layout,
            out var gatewayCollisionFailure);
        var brickFactoryCollisionReady = TideglassBrickFactoryCollisionReady(
            dressingRoot,
            layout,
            out var brickFactoryCollisionFailure);
        var constructionLandmarkClearanceReady = TideglassConstructionLandmarkClearanceReady(
            dressingRoot,
            out var constructionLandmarkClearance);
        var authoredMeshCollisionReady = TideglassAuthoredMeshCollisionReady(arena.Root);
        var treyAssembliesReady = TideglassTreyAssembliesReady(
            arena.Root,
            out var treyAssemblyFailures);
        var majadroidVariantsReady = TideglassMajadroidVariantsReady(
            arena.Root,
            out var majadroidVariantFailures);
        var noOrphanCover = layout.CollisionBoxes.All(box =>
            !box.Name.StartsWith("MidCover", StringComparison.Ordinal)
            && box.Name != "MidCoverSiteOffice"
            && box.Name != "MidCoverCargo");
        var collisionShellsMapped = TideglassCollisionShellsHaveAuthoredModels(
            dressingRoot,
            layout);
        var broadConstructionShellsRemoved = layout.CollisionBoxes.All(box =>
            box.Name != "CraneBaseShell"
            && !box.Name.StartsWith("ConstructionSoilBank", StringComparison.Ordinal))
            && layout.NavigationBoxes.Count == 13
            && layout.NavigationBoxes[0].Name == "CraneNavigationFootprint"
            && (layout.NavigationBoxes[0].Center - layout.Origin).IsEqualApprox(
                new Vector3(-32.174f, 0.92f, -14.174f))
            && layout.NavigationBoxes[0].Size.IsEqualApprox(new Vector3(6.3f, 1.8f, 6.3f))
            && layout.NavigationBoxes[1].Name == "ConstructionSouthHillNavigation"
            && layout.NavigationBoxes[2].Name == "ConstructionNorthHillNavigation";
        var geometryReady = layout.CollisionBoxes.Count == 15
            && layout.Props.Count == 20
            && importedProps == layout.Props.Count
            && layout.CollisionBoxes.All(box => box.Size.X > 0.1f && box.Size.Y > 0.1f && box.Size.Z > 0.1f)
            && propsInsideBounds
            && propsSeparated
            && sitesClear
            && spawnsClear
            && collisionCoverage
            && authoredCollisionOnly
            && dressingBoundsReady
            && perimeterFenceReady
            && perimeterGatesReady
            && solidMaterialsReady
            && roadSurfacesReady
            && towerCollisionReady
            && walkwayCollisionReady
            && gatewayCollisionReady
            && brickFactoryCollisionReady
            && constructionLandmarkClearanceReady
            && authoredMeshCollisionReady
            && treyAssembliesReady
            && majadroidVariantsReady
            && broadConstructionShellsRemoved
            && noOrphanCover
            && collisionShellsMapped;

        var spawnAndSiteReady = layout.AttackSpawns.Count == 5
            && layout.DefenderSpawns.Count == 5
            && layout.SitePositions.Count == 2
            && TideglassPointsSeparated(layout.AttackSpawns, 2.0f)
            && TideglassPointsSeparated(layout.DefenderSpawns, 2.0f)
            && layout.AttackSpawns.Concat(layout.DefenderSpawns).All(point => layout.IsInsideArena(point))
            && layout.SitePositions.All(point => layout.IsInsideArena(point))
            && layout.SiteSeparation >= 40.0f
            && layout.SitePositions.All(site => HorizontalDistance(layout.AttackSpawn, site) >= 20.0f)
            && layout.SitePositions.All(site => HorizontalDistance(layout.DefenderSpawn, site) >= 20.0f);
        var heuristicSightlinesBlocked = !layout.HasSpawnSightlineToSite(0)
            && !layout.HasSpawnSightlineToSite(1);
        var physicalSightlinesBlocked = layout.SitePositions.All(site => PhysicsRaycast.HasHit(
            GetWorld3D(),
            layout.AttackSpawn + Vector3.Up * 1.57f,
            site + Vector3.Up * 1.57f,
            1u));
        var spawnSightlineChecks = layout.AttackSpawns.SelectMany((attackSpawn, attackIndex) =>
            layout.DefenderSpawns.Select((defenderSpawn, defenderIndex) =>
            {
                var layoutClear = layout.HasCapsuleClearance(
                    new[] { attackSpawn, defenderSpawn },
                    out var blocker);
                var physicallyBlocked = PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    attackSpawn + Vector3.Up * 1.57f,
                    defenderSpawn + Vector3.Up * 1.57f,
                    1u);
                return new
                {
                    Pair = $"{attackIndex}-{defenderIndex}",
                    LayoutClear = layoutClear,
                    PhysicallyBlocked = physicallyBlocked,
                    Blocker = blocker
                };
            })).ToArray();
        var spawnSightlinesBlocked = spawnSightlineChecks.All(check =>
            !check.LayoutClear && check.PhysicallyBlocked);
        var unblockedSpawnSightlines = string.Join('|', spawnSightlineChecks
            .Where(check => check.LayoutClear || !check.PhysicallyBlocked)
            .Select(check => $"{check.Pair}:{check.Blocker}"));
        var siteRotationSightlineBlocked = !layout.HasCapsuleClearance(
                layout.SitePositions,
                out var siteSightlineBlocker)
            && PhysicsRaycast.HasHit(
                GetWorld3D(),
                layout.SitePositions[0] + Vector3.Up * 1.57f,
                layout.SitePositions[1] + Vector3.Up * 1.57f,
                1u);
        var blockedCoverPoints = layout.CoverPoints
            .Select((point, index) =>
            {
                var clear = layout.HasCapsuleClearance(new[] { point, point }, out var blocker);
                return (Index: index, Clear: clear, Blocker: blocker);
            })
            .Where(check => !check.Clear)
            .Select(check => $"{check.Index}:{check.Blocker}")
            .ToArray();
        var coverPointsClear = blockedCoverPoints.Length == 0;
        var topologyReady = layout.MapId == DemolitionMapCatalog.TideglassReactorId
            && layout.HasThreeAttackRoutes
            && layout.HasBalancedSiteTravel
            && layout.HasDenseCentralCover
            && layout.CentralCoverBodyCount == DemolitionArenaLayout.MinimumCentralCoverBodyCount
            && layout.HasPlayerClearance
            && layout.CentralPropsDoNotOverlap
            && heuristicSightlinesBlocked
            && physicalSightlinesBlocked
            && spawnSightlinesBlocked
            && siteRotationSightlineBlocked
            && coverPointsClear
            && TideglassRoutesStayInside(layout);

        var routeAClear = layout.HasCapsuleClearance(layout.AttackToAPath, out var routeABlocker);
        var routeBClear = layout.HasCapsuleClearance(layout.AttackToBPath, out var routeBBlocker);
        var routeMidClear = layout.HasCapsuleClearance(layout.AttackMidPath, out var routeMidBlocker);
        var defenderAClear = layout.HasCapsuleClearance(layout.DefenderToAPath, out var defenderABlocker);
        var defenderBClear = layout.HasCapsuleClearance(layout.DefenderToBPath, out var defenderBBlocker);
        var rotationClear = layout.HasCapsuleClearance(layout.SiteRotationPath, out var rotationBlocker);
        var physicalRouteAClear = TideglassPhysicalRouteClear(GetWorld3D(), layout.AttackToAPath, out var physicalRouteABlocker);
        var physicalRouteBClear = TideglassPhysicalRouteClear(GetWorld3D(), layout.AttackToBPath, out var physicalRouteBBlocker);
        var physicalRouteMidClear = TideglassPhysicalRouteClear(GetWorld3D(), layout.AttackMidPath, out var physicalRouteMidBlocker);
        var physicalDefenderAClear = TideglassPhysicalRouteClear(GetWorld3D(), layout.DefenderToAPath, out var physicalDefenderABlocker);
        var physicalDefenderBClear = TideglassPhysicalRouteClear(GetWorld3D(), layout.DefenderToBPath, out var physicalDefenderBBlocker);
        var physicalRotationClear = TideglassPhysicalRouteClear(GetWorld3D(), layout.SiteRotationPath, out var physicalRotationBlocker);
        var physicalRoutesReady = physicalRouteAClear
            && physicalRouteBClear
            && physicalRouteMidClear
            && physicalDefenderAClear
            && physicalDefenderBClear
            && physicalRotationClear;
        var planner = new DemolitionRoutePlanner(layout);
        var navigationReady = layout.SitePositions.All(site =>
        {
            var attack = planner.Plan(layout.AttackSpawn, site);
            var defense = planner.Plan(layout.DefenderSpawn, site);
            var attackPath = new[] { layout.AttackSpawn }.Concat(attack.Waypoints).ToArray();
            var defensePath = new[] { layout.DefenderSpawn }.Concat(defense.Waypoints).ToArray();
            return attack.ReachesDestination
                && defense.ReachesDestination
                && planner.IsRouteClear(layout.AttackSpawn, attack.Waypoints)
                && planner.IsRouteClear(layout.DefenderSpawn, defense.Waypoints)
                && TideglassPhysicalRouteClear(GetWorld3D(), attackPath, out _)
                && TideglassPhysicalRouteClear(GetWorld3D(), defensePath, out _);
        });
        var strategyNavigationChecks = DemolitionArenaLayout.StrategyTargetKeys.Select(key =>
        {
            var target = layout.StrategyTarget(key);
            var attackOwned = key.StartsWith("attack_", StringComparison.Ordinal)
                || key.StartsWith("postplant_", StringComparison.Ordinal);
            var defenseOwned = key.StartsWith("defense_", StringComparison.Ordinal)
                || key.StartsWith("retake_", StringComparison.Ordinal)
                || key.StartsWith("site_", StringComparison.Ordinal);
            var start = attackOwned ? layout.AttackSpawn : layout.DefenderSpawn;
            var route = planner.Plan(start, target);
            var physicalPath = new[] { start }.Concat(route.Waypoints).ToArray();
            var physicalClear = TideglassPhysicalRouteClear(
                GetWorld3D(),
                physicalPath,
                out var physicalBlocker);
            return new
            {
                Key = key,
                OwnerKnown = attackOwned != defenseOwned,
                Inside = layout.IsInsideArena(target),
                Route = route,
                Clear = planner.IsRouteClear(start, route.Waypoints),
                PhysicalClear = physicalClear,
                PhysicalBlocker = physicalBlocker
            };
        }).ToArray();
        var strategyTargetsReady = strategyNavigationChecks.All(check =>
            check.OwnerKnown && check.Inside && check.Route.ReachesDestination && check.Clear && check.PhysicalClear);
        var blockedStrategyTargets = string.Join('|', strategyNavigationChecks
            .Where(check => !check.OwnerKnown || !check.Inside || !check.Route.ReachesDestination || !check.Clear || !check.PhysicalClear)
            .Select(check => $"{check.Key}:{check.PhysicalBlocker}"));
        var upwardBlock = TideglassWalkwayUpwardBlockReady(GetWorld3D(), layout);
        var stairTraversal = await TideglassWalkPlayerAcrossStairs(layout);

        var authoredNodes = GetTree().GetNodesInGroup("demolition_authored_model");
        using var authoredNodesBacking = authoredNodes.AsDisposable();
        var authoredModels = authoredNodes
            .OfType<Node3D>()
            .Where(node => IsInstanceValid(node)
                && IsInstanceValid(dressingRoot)
                && dressingRoot!.IsAncestorOf(node))
            .ToArray();
        var scenePaths = authoredModels
            .Where(node => node.HasMeta("demolition_scene_path"))
            .Select(node => node.GetMeta("demolition_scene_path").AsString())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        var dressingSourcePacks = scenePaths
            .Select(TideglassSourcePack)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dressingSceneReuse = scenePaths
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count())
            .DefaultIfEmpty(0)
            .Max();
        var authoredModelCount = dressingRoot?.GetMeta("authored_model_count").AsInt32() ?? 0;
        var missingModelCount = dressingRoot?.GetMeta("missing_model_count").AsInt32() ?? -1;
        var uniqueSceneCount = dressingRoot?.GetMeta("unique_scene_count").AsInt32() ?? 0;
        var allAuthoredPaths = scenePaths.Concat(layout.Props.Select(prop => prop.ScenePath)).ToArray();
        var allSourcePacks = allAuthoredPaths
            .Select(TideglassSourcePack)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sceneReuse = allAuthoredPaths
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count())
            .DefaultIfEmpty(0)
            .Max();
        var compositionReady = authoredModelCount == 26
            && layout.Props.Count == 20
            && allAuthoredPaths.Length == 46
            && allAuthoredPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 46
            && sceneReuse == 1
            && allSourcePacks.SetEquals(new[]
            {
                "kenney_city_kit_roads",
                "kenney_factory_kit",
                "majadroid_construction_site",
                "concrete_road_barrier",
                "quaternius_buildings_pack",
                "quaternius_downtown_city",
                "trey_modular_industrial"
            });
        var noIndustrialKit = allAuthoredPaths.All(path => !path.Contains(
            "/kenney_city_kit_industrial/",
            StringComparison.OrdinalIgnoreCase));
        var scenesExist = allAuthoredPaths.All(path => ResourceLoader.Exists(path));
        var authoredModelsReady = authoredModels.All(TideglassAuthoredModelHasMesh);
        var authoredDressingReady = IsInstanceValid(dressingRoot)
            && authoredModelCount == 26
            && authoredModels.Length == authoredModelCount
            && scenePaths.Length == authoredModelCount
            && missingModelCount == 0
            && uniqueSceneCount == 26
            && dressingSceneReuse == 1
            && dressingSourcePacks.Count == 4
            && compositionReady
            && noIndustrialKit
            && scenesExist
            && authoredModelsReady;

        var runtimeReady = arena.Active
            && arena.Root.Visible
            && arena.Root.ProcessMode != ProcessModeEnum.Disabled
            && arena.Root.Name == "TideglassReactorArena"
            && arena.CollisionBodyCount >= layout.CollisionBoxes.Count + layout.Props.Count
            && arena.ActiveCollisionBodyCount == arena.CollisionBodyCount
            && arena.AllStaticBodiesUseWorldLayer()
            && arena.VisualPartCount >= authoredModelCount + layout.Props.Count + 6
            && arena.Sites.Count == 2
            && arena.Sites.Select((site, index) => site.Position.IsEqualApprox(layout.SitePositions[index])).All(equal => equal);

        var valid = initiallyIsolated
            && catalogReady
            && localizationReady
            && layoutsDistinct
            && geometryReady
            && spawnAndSiteReady
            && topologyReady
            && routeAClear
            && routeBClear
            && routeMidClear
            && defenderAClear
            && defenderBClear
            && rotationClear
            && physicalRoutesReady
            && navigationReady
            && strategyTargetsReady
            && upwardBlock.Ready
            && stairTraversal.Ready
            && authoredDressingReady
            && runtimeReady;
        GD.Print(
            $"TIDEGLASS_REACTOR_CHECK valid={valid} catalog={catalogReady} localization={localizationReady} "
            + $"layouts_distinct={layoutsDistinct} geometry={geometryReady} imported={importedProps}/{layout.Props.Count} "
            + $"bounds={propsInsideBounds} bounds_failures={string.Join('|', boundsFailures)} separated={propsSeparated} overlap={overlapPair} site_clear={sitesClear} "
            + $"spawn_clear={spawnsClear} collision_coverage={collisionCoverage} "
            + $"collision_failures={string.Join('|', collisionCoverageFailures)} tight_collision_failures={string.Join('|', tightCollisionFailures)} "
            + $"dressing_bounds={dressingBoundsReady} dressing_bounds_failures={dressingBoundsFailures} perimeter_fence={perimeterFenceReady} "
            + $"perimeter_gates={perimeterGatesReady} perimeter_gate_failures={perimeterGateFailures} solid_materials={solidMaterialsReady} solid_material_failures={solidMaterialFailures} "
            + $"road_surfaces={roadSurfacesReady} road_surface_failures={roadSurfaceFailures} tower_collision={towerCollisionReady} authored_mesh_collision={authoredMeshCollisionReady} "
            + $"walkway_collision={walkwayCollisionReady}:{walkwayCollisionFailure} gateway_collision={gatewayCollisionReady}:{gatewayCollisionFailure} brick_factory_collision={brickFactoryCollisionReady}:{brickFactoryCollisionFailure} "
            + $"construction_clearance={constructionLandmarkClearanceReady}:{constructionLandmarkClearance:0.000} "
            + $"trey_assemblies={treyAssembliesReady}:{treyAssemblyFailures} majadroid_variants={majadroidVariantsReady}:{majadroidVariantFailures} broad_shell_free={broadConstructionShellsRemoved} orphan_cover_free={noOrphanCover} shells_mapped={collisionShellsMapped} authored_collision_only={authoredCollisionOnly} "
            + $"spawns_sites={spawnAndSiteReady} topology={topologyReady} sightlines={heuristicSightlinesBlocked}/{physicalSightlinesBlocked} "
            + $"spawn_sightlines={spawnSightlinesBlocked} spawn_sightline_failures={unblockedSpawnSightlines} site_sightline={siteRotationSightlineBlocked}:{siteSightlineBlocker} "
            + $"cover_clear={coverPointsClear} cover_blocked={string.Join('|', blockedCoverPoints)} navigation={navigationReady} "
            + $"strategy_targets={strategyTargetsReady} strategy_blocked={blockedStrategyTargets} routes={routeAClear}/{routeBClear}/{routeMidClear}/{defenderAClear}/{defenderBClear}/{rotationClear} "
            + $"blockers={routeABlocker}|{routeBBlocker}|{routeMidBlocker}|{defenderABlocker}|{defenderBBlocker}|{rotationBlocker} "
            + $"physical_routes={physicalRouteAClear}/{physicalRouteBClear}/{physicalRouteMidClear}/{physicalDefenderAClear}/{physicalDefenderBClear}/{physicalRotationClear} "
            + $"physical_blockers={physicalRouteABlocker}|{physicalRouteBBlocker}|{physicalRouteMidBlocker}|{physicalDefenderABlocker}|{physicalDefenderBBlocker}|{physicalRotationBlocker} "
            + $"walkway_upward_block={upwardBlock.Ready}:{upwardBlock.SafeFraction:0.000}:{upwardBlock.Collider} "
            + $"stair_walk={stairTraversal.Ready} west_stair={stairTraversal.WestReady}:{stairTraversal.WestFrames}:{stairTraversal.WestGain:0.00} "
            + $"east_stair={stairTraversal.EastReady}:{stairTraversal.EastFrames}:{stairTraversal.EastGain:0.00} "
            + $"runtime={runtimeReady} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} sites={arena.Sites.Count} "
            + $"authored={authoredDressingReady} authored_models={authoredModelCount} authored_nodes={authoredModels.Length} "
            + $"dressing_scenes={uniqueSceneCount} dressing_packs={dressingSourcePacks.Count} dressing_reuse={dressingSceneReuse} "
            + $"all_scenes={allAuthoredPaths.Length} all_packs={allSourcePacks.Count}:{string.Join('|', allSourcePacks.OrderBy(pack => pack))} "
            + $"max_scene_reuse={sceneReuse} composition={compositionReady} no_industrial={noIndustrialKit} scenes_exist={scenesExist} missing_models={missingModelCount}");
        GD.Print($"TIDEGLASS_REACTOR_PASS valid={valid}");

        arena.SetActive(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var root = arena.Root;
        _demolitionArena = null;
        _demolitionRoutePlanner = null;
        _demolitionSites.Clear();
        root.QueueFree();
        await WaitFrames(3);
        GetTree().Quit(valid ? 0 : 2);
    }

}
