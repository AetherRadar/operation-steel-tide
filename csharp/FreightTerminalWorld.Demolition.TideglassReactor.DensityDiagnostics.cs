using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // This diagnostic intentionally keeps its physical sampling contracts together while
    // Tideglass V2 geometry is still converging. Follow-up: extract the reusable grid, cover,
    // and lane probes into a focused DemolitionDensityProbe after the V2 layout is locked.
    private const double TideglassDensityMaximumBuildMilliseconds = 6000.0;
    // Exact visible collision no longer treats roof overhangs and open porches as opaque walls.
    // Real authored cover keeps the honest maximum below roughly half the 136 m map width.
    private const float TideglassDensityMaximumHorizontalSightline = 70.0f;
    private const float TideglassDensityMaximumOpenDiameter = 26.0f;
    private const float TideglassDensityMinimumSiteDirectionalCover = 0.18f;
    private const float TideglassDensityMinimumCoveredSiteSamples = 0.60f;
    private const float TideglassDensitySharedSpawnAllowance = 20.0f;
    private const float TideglassDensityMinimumLaneSeparation = 4.0f;
    private const float TideglassDensityLaneOverlapDistance = 7.0f;
    private const float TideglassDensityMaximumLaneOverlapRatio = 0.25f;

    private readonly record struct TideglassDensitySightlineCheck(
        string Name,
        Vector3 From,
        Vector3 To,
        bool Blocked);

    private readonly record struct TideglassDensitySpatialMetrics(
        int PlayableSampleCount,
        float LongestHorizontalSightline,
        Vector3 LongestSightlineFrom,
        Vector3 LongestSightlineTo,
        float LargestOpenDiameter,
        Vector3 LargestOpenCenter);

    private readonly record struct TideglassDensitySiteCoverMetrics(
        int PlayableSampleCount,
        int RayCount,
        float DirectionalCoverage,
        float CoveredSampleRatio);

    private readonly record struct TideglassDensityLaneMetrics(
        float AttackAToBMinimumDistance,
        float AttackAToMidMinimumDistance,
        float AttackBToMidMinimumDistance,
        float AttackAToBOverlapRatio,
        float AttackAToMidOverlapRatio,
        float AttackBToMidOverlapRatio);

    private static readonly string[] TideglassDensityExpansionBuildings =
    {
        "SouthRegistryHouse",
        "SightBlockEastApproachOffices",
        "MidTelegraphHouse",
        "DefenderServiceBlock",
        "SouthwestWatchHouse",
        "DefenderArchiveBlock",
        "NorthFoundryTenement",
        "ConstructionTurbineWorkshop",
        "ReactorAnnex",
        "EastShiftOffice",
        "EastInspectionOffice",
        "MidCompressorHouse",
        "ReactorBoilerWorkshop",
        "EastSwitchgearHall",
        "MidCrewCanteen",
        "CivicUtilityOffice",
        "CivicPumpHouse",
        "SouthGlassworksOffice",
        "MidControlRoom",
        "EastTransformerWorks",
        "CivicCoolingServiceHall",
        "ReactorMaintenanceDepot",
        "WestFoundryWarehouse",
        "WestFoundryInspectionAnnex",
        "NorthFreightOffice",
        "EastOperationsOffice",
        "SouthWorksOffice",
        "WestGateOffice",
        "SouthTransitOffice",
        "MidDispatchOffice"
    };

    private static readonly string[] TideglassDensityDressingBuildings =
    {
        "ConstructionBuilding",
        "OldBrickReactorHall"
    };

    private async void ValidateTideglassReactorDensity()
    {
        try
        {
            await ValidateTideglassReactorDensityCore();
        }
        catch (Exception exception)
        {
            GD.PushError($"Tideglass density diagnostic failed: {exception}");
            GD.Print(
                $"TIDEGLASS_DENSITY_CHECK valid=False exception={exception.GetType().Name}");
            GD.Print("TIDEGLASS_DENSITY_PASS valid=False");
            CleanupTideglassDensityDiagnostic();
            QuitDiagnosticAfterSceneCleanup(2);
        }
    }

    private async Task ValidateTideglassReactorDensityCore()
    {
        await WaitFrames(3);
        _demolitionSelectedMapId = DemolitionMapCatalog.TideglassReactorId;
        var buildTimer = System.Diagnostics.Stopwatch.StartNew();
        EnsureDemolitionArenaBuilt();
        buildTimer.Stop();
        var buildMilliseconds = buildTimer.Elapsed.TotalMilliseconds;
        if (_demolitionArena is null)
        {
            GD.Print(
                $"TIDEGLASS_DENSITY_CHECK valid=False built=False build_ms={buildMilliseconds:0.00}");
            GD.Print("TIDEGLASS_DENSITY_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        var arena = _demolitionArena;
        var layout = arena.Layout;
        GetTree().Paused = false;
        DisableActorsForSurvivalDiagnostics();
        arena.SetActive(true);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var failures = new List<string>();
        var buildTimingReady = buildMilliseconds <= TideglassDensityMaximumBuildMilliseconds;
        if (!buildTimingReady)
        {
            failures.Add("build-time");
        }
        var boundsReady = layout.MapId == DemolitionMapCatalog.TideglassReactorId
            && arena.Root.Name == "TideglassReactorArena"
            && Mathf.IsEqualApprox(layout.WorldBounds.Size.X, 136.0f)
            && Mathf.IsEqualApprox(layout.WorldBounds.Size.Y, 112.0f)
            && layout.WorldBounds.Position.IsEqualApprox(
                new Vector2(layout.Origin.X - 68.0f, layout.Origin.Z - 56.0f));
        if (!boundsReady)
        {
            failures.Add("bounds");
        }

        var sitesReady = layout.SitePositions.Count == 2
            && layout.SitePositions.All(point => layout.IsInsideArena(point))
            && layout.SiteSeparation >= 80.0f;
        if (!sitesReady)
        {
            failures.Add("sites");
        }

        var dressingRoot = arena.Root.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing");
        var authoredNodeArray = GetTree().GetNodesInGroup("demolition_authored_model");
        using var authoredNodesBacking = authoredNodeArray.AsDisposable();
        var authoredModels = authoredNodeArray
            .OfType<Node3D>()
            .Where(node => IsInstanceValid(node)
                && IsInstanceValid(dressingRoot)
                && dressingRoot!.IsAncestorOf(node))
            .ToArray();
        var dressingPaths = authoredModels
            .Where(node => node.HasMeta("demolition_scene_path"))
            .Select(node => node.GetMeta("demolition_scene_path").AsString())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        var allAuthoredPaths = dressingPaths
            .Concat(layout.Props.Select(prop => prop.ScenePath))
            .ToArray();
        var uniqueAuthoredPathCount = allAuthoredPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var sourcePacks = allAuthoredPaths
            .Select(TideglassSourcePack)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loadedPropCount = layout.Props.Count(prop =>
            TideglassPropModelLoaded(arena.Root, prop));
        var missingModelCount = dressingRoot?.GetMeta("missing_model_count").AsInt32() ?? -1;
        var authoredPathReady = layout.Props.Count == 56
            && allAuthoredPaths.Length == 82
            && uniqueAuthoredPathCount == 70
            && sourcePacks.SetEquals(new[]
            {
                "concrete_road_barrier",
                "kenney_city_kit_roads",
                "kenney_factory_kit",
                "majadroid_construction_site",
                "old_military_crate",
                "quaternius_buildings_pack",
                "quaternius_downtown_city",
                "trey_modular_industrial"
            })
            && allAuthoredPaths.All(path => ResourceLoader.Exists(path))
            && loadedPropCount == layout.Props.Count
            && authoredModels.Length == dressingPaths.Length
            && authoredModels.All(TideglassAuthoredModelHasMesh)
            && missingModelCount == 0;
        if (!authoredPathReady)
        {
            failures.Add("authored-paths");
        }

        var expansionProps = TideglassDensityExpansionBuildings
            .Select(name => layout.Props.SingleOrDefault(prop => prop.Name == name))
            .ToArray();
        var expansionScenePaths = expansionProps
            .Where(prop => !string.IsNullOrWhiteSpace(prop.ScenePath))
            .Select(prop => prop.ScenePath)
            .ToArray();
        var expansionBuildingsReady = expansionProps.Length == TideglassDensityExpansionBuildings.Length
            && expansionProps.All(prop => !string.IsNullOrWhiteSpace(prop.Name))
            && expansionScenePaths.Length == TideglassDensityExpansionBuildings.Length
            && expansionScenePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                == 23
            && expansionProps.All(TideglassDensityHasExpectedExpansionScenePath)
            && expansionProps.All(prop => ResourceLoader.Exists(prop.ScenePath))
            && expansionProps.All(prop => TideglassPropModelLoaded(arena.Root, prop));
        if (!expansionBuildingsReady)
        {
            failures.Add("expansion-buildings");
        }

        var majorProps = layout.Props.Where(TideglassDensityIsMajorBuilding).ToArray();
        var majorDressing = authoredModels.Where(model =>
            TideglassDensityDressingBuildings.Contains(
                model.Name.ToString(),
                StringComparer.Ordinal)).ToArray();
        var majorBuildingPositions = majorProps.Select(prop => prop.Position)
            .Concat(majorDressing.Select(model => model.Position))
            .ToArray();
        var majorBuildingsLoaded = majorProps.All(prop =>
                TideglassPropModelLoaded(arena.Root, prop))
            && majorDressing.Length == TideglassDensityDressingBuildings.Length
            && majorDressing.All(TideglassAuthoredModelHasMesh);
        var regionCounts = TideglassDensityBuildingRegionCounts(
            majorBuildingPositions,
            layout.Origin);
        var buildingDistributionReady = regionCounts.Values.All(count => count > 0);
        var majorBuildingsReady = majorBuildingPositions.Length == 41
            && majorBuildingsLoaded
            && buildingDistributionReady;
        if (!majorBuildingsReady)
        {
            failures.Add("major-buildings");
        }

        var groundRouteDefinitions = new (string Name, IReadOnlyList<Vector3> Points)[]
        {
            ("attack-a", layout.AttackToAPath),
            ("attack-b", layout.AttackToBPath),
            ("attack-mid", layout.AttackMidPath),
            ("defender-a", layout.DefenderToAPath),
            ("defender-b", layout.DefenderToBPath),
            ("rotation", layout.SiteRotationPath)
        };
        var groundRouteChecks = groundRouteDefinitions.Select(route =>
        {
            var layoutClear = layout.HasCapsuleClearance(
                route.Points, out var layoutBlocker);
            var physicalClear = TideglassPhysicalRouteClear(
                GetWorld3D(), route.Points, out var physicalBlocker);
            return new
            {
                route.Name,
                route.Points,
                Inside = route.Points.All(point => layout.IsInsideArena(point)),
                Grounded = route.Points.All(point =>
                    Mathf.Abs(point.Y - (layout.Origin.Y + 0.2f)) <= 0.12f),
                LayoutClear = layoutClear,
                PhysicalClear = physicalClear,
                LayoutBlocker = layoutBlocker,
                PhysicalBlocker = physicalBlocker
            };
        }).ToArray();
        var groundRoutesReady = groundRouteChecks.Length == 6
            && groundRouteChecks.All(check => check.Points.Count >= 2
                && check.Inside
                && check.Grounded
                && check.LayoutClear
                && check.PhysicalClear);
        var groundRouteFailures = string.Join(',', groundRouteChecks
            .Where(check => check.Points.Count < 2
                || !check.Inside
                || !check.Grounded
                || !check.LayoutClear
                || !check.PhysicalClear)
            .Select(check =>
                $"{check.Name}:{check.LayoutBlocker}:{check.PhysicalBlocker}:"
                + $"{check.Inside}/{check.Grounded}"));
        if (!groundRoutesReady)
        {
            failures.Add("ground-routes");
        }

        var laneMetrics = TideglassDensityMeasureLaneIndependence(layout);
        var laneIndependenceReady = TideglassDensityLaneMetricsReady(laneMetrics);
        if (!laneIndependenceReady)
        {
            failures.Add("lane-independence");
        }

        var attackRoutesReady = layout.HasThreeAttackRoutes
            && layout.AttackToALength >= 90.0f
            && layout.AttackToBLength >= 90.0f
            && layout.SiteTravelDifferenceRatio <= 0.12f
            && layout.AttackToAPath.All(point => layout.IsInsideArena(point))
            && layout.AttackToBPath.All(point => layout.IsInsideArena(point))
            && layout.AttackMidPath.All(point => layout.IsInsideArena(point));
        if (!attackRoutesReady)
        {
            failures.Add("attack-routes");
        }

        var attackSiteChecks = layout.AttackSpawns.SelectMany((spawn, spawnIndex) =>
            layout.SitePositions.Select((site, siteIndex) =>
                new TideglassDensitySightlineCheck(
                    $"attack-{spawnIndex}-{(char)('A' + siteIndex)}",
                    spawn,
                    site,
                    TideglassDensitySightlineBlocked(GetWorld3D(), spawn, site))))
            .ToArray();
        var defenderSiteChecks = layout.DefenderSpawns.SelectMany((spawn, spawnIndex) =>
            layout.SitePositions.Select((site, siteIndex) =>
                new TideglassDensitySightlineCheck(
                    $"defense-{spawnIndex}-{(char)('A' + siteIndex)}",
                    spawn,
                    site,
                    TideglassDensitySightlineBlocked(GetWorld3D(), spawn, site))))
            .ToArray();
        var opposingSpawnChecks = layout.AttackSpawns.SelectMany((attack, attackIndex) =>
            layout.DefenderSpawns.Select((defense, defenderIndex) =>
                new TideglassDensitySightlineCheck(
                    $"spawn-{attackIndex}-{defenderIndex}",
                    attack,
                    defense,
                    TideglassDensitySightlineBlocked(GetWorld3D(), attack, defense))))
            .ToArray();
        var attackSitePairCount = attackSiteChecks.Length;
        var attackSiteBlockedCount = attackSiteChecks.Count(check => check.Blocked);
        var defenderSitePairCount = defenderSiteChecks.Length;
        var defenderSiteBlockedCount = defenderSiteChecks.Count(check => check.Blocked);
        var opposingSpawnPairCount = opposingSpawnChecks.Length;
        var opposingSpawnBlockedCount = opposingSpawnChecks.Count(check => check.Blocked);
        var sitePairBlocked = TideglassDensitySightlineBlocked(
            GetWorld3D(), layout.SitePositions[0], layout.SitePositions[1]);
        var unblockedSightlines = attackSiteChecks
            .Concat(defenderSiteChecks)
            .Concat(opposingSpawnChecks)
            .Where(check => !check.Blocked)
            .Select(check => TideglassDensitySightlineFailure(check, layout.Origin))
            .ToArray();
        var physicalSightlinesReady = attackSiteBlockedCount == attackSitePairCount
            && defenderSiteBlockedCount == defenderSitePairCount
            && opposingSpawnBlockedCount == opposingSpawnPairCount
            && sitePairBlocked;
        if (!physicalSightlinesReady)
        {
            failures.Add("physical-sightlines");
        }

        var spatialMetrics = TideglassDensityMeasureSpatialMetrics(
            GetWorld3D(), layout);
        var spatialDensityReady = spatialMetrics.PlayableSampleCount >= 900
            && spatialMetrics.LongestHorizontalSightline
                <= TideglassDensityMaximumHorizontalSightline
            && spatialMetrics.LargestOpenDiameter
                <= TideglassDensityMaximumOpenDiameter;
        if (!spatialDensityReady)
        {
            failures.Add("spatial-density");
        }

        var siteACover = TideglassDensityMeasureSiteCover(
            GetWorld3D(), layout, layout.SitePositions[0]);
        var siteBCover = TideglassDensityMeasureSiteCover(
            GetWorld3D(), layout, layout.SitePositions[1]);
        var siteCoverReady = TideglassDensitySiteCoverReady(siteACover)
            && TideglassDensitySiteCoverReady(siteBCover);
        if (!siteCoverReady)
        {
            failures.Add("site-cover");
        }

        var collisionVisuals = layout.CollisionBoxes.Where(box =>
        {
            var body = arena.Root.GetNodeOrNull<Node3D>(box.Name);
            return body?.GetNodeOrNull<MeshInstance3D>("Visual") is not null;
        }).Select(box => box.Name).ToArray();
        var detachedPartitions = TideglassDensityDetachedVisiblePartitions(layout);
        var invisibleCollisionReady = layout.CollisionBoxes.All(box => !box.Visible)
            && layout.DetailBoxes.Count == 0
            && collisionVisuals.Length == 0
            && detachedPartitions.Count == 0;
        if (!invisibleCollisionReady)
        {
            failures.Add("collision-art");
        }

        var routePlanner = new DemolitionRoutePlanner(layout);
        var coverPointChecks = layout.CoverPoints.Select((point, index) =>
        {
            var pointProbe = new[] { point, point };
            var layoutClear = layout.HasCapsuleClearance(
                pointProbe, out var layoutBlocker);
            var physicalClear = TideglassPhysicalRouteClear(
                GetWorld3D(), pointProbe, out var physicalBlocker);
            var attackRoute = routePlanner.Plan(layout.AttackSpawn, point);
            var defenderRoute = routePlanner.Plan(layout.DefenderSpawn, point);
            var attackReachable = attackRoute.ReachesDestination
                && routePlanner.IsRouteClear(
                    layout.AttackSpawn, attackRoute.Waypoints);
            var defenderReachable = defenderRoute.ReachesDestination
                && routePlanner.IsRouteClear(
                    layout.DefenderSpawn, defenderRoute.Waypoints);
            return new
            {
                Index = index,
                Inside = layout.IsInsideArena(point),
                LayoutClear = layoutClear,
                PhysicalClear = physicalClear,
                LayoutBlocker = layoutBlocker,
                PhysicalBlocker = physicalBlocker,
                AttackReachable = attackReachable,
                DefenderReachable = defenderReachable
            };
        }).ToArray();
        var coverPointsReady = coverPointChecks.Length > 0
            && coverPointChecks.All(check => check.Inside
                && check.LayoutClear
                && check.PhysicalClear
                && check.AttackReachable
                && check.DefenderReachable);
        var coverPointFailures = string.Join(',', coverPointChecks
            .Where(check => !check.Inside
                || !check.LayoutClear
                || !check.PhysicalClear
                || !check.AttackReachable
                || !check.DefenderReachable)
            .Select(check =>
                $"{check.Index}:{check.LayoutBlocker}:{check.PhysicalBlocker}:"
                + $"{check.AttackReachable}/{check.DefenderReachable}"));
        if (!coverPointsReady)
        {
            failures.Add("cover-points");
        }

        var strategyChecks = DemolitionArenaLayout.StrategyTargetKeys.Select(key =>
        {
            var target = layout.StrategyTarget(key);
            var attackOwned = key.StartsWith("attack_", StringComparison.Ordinal)
                || key.StartsWith("postplant_", StringComparison.Ordinal);
            var defenseOwned = key.StartsWith("defense_", StringComparison.Ordinal)
                || key.StartsWith("retake_", StringComparison.Ordinal)
                || key.StartsWith("site_", StringComparison.Ordinal);
            var start = attackOwned ? layout.AttackSpawn : layout.DefenderSpawn;
            var route = routePlanner.Plan(start, target);
            return new
            {
                Key = key,
                OwnerKnown = attackOwned != defenseOwned,
                Inside = layout.IsInsideArena(target),
                Route = route,
                Clear = routePlanner.IsRouteClear(start, route.Waypoints)
            };
        }).ToArray();
        var strategyTargetsReady = strategyChecks.Length == 24
            && DemolitionArenaLayout.StrategyTargetKeys
                .Distinct(StringComparer.Ordinal)
                .Count() == 24
            && strategyChecks.All(check => check.OwnerKnown
                && check.Inside
                && check.Route.ReachesDestination
                && check.Clear);
        var strategyFailures = string.Join(',', strategyChecks
            .Where(check => !check.OwnerKnown
                || !check.Inside
                || !check.Route.ReachesDestination
                || !check.Clear)
            .Select(check => check.Key));
        if (!strategyTargetsReady)
        {
            failures.Add("strategy-targets");
        }

        var valid = buildTimingReady
            && boundsReady
            && sitesReady
            && authoredPathReady
            && expansionBuildingsReady
            && majorBuildingsReady
            && attackRoutesReady
            && groundRoutesReady
            && laneIndependenceReady
            && physicalSightlinesReady
            && spatialDensityReady
            && siteCoverReady
            && invisibleCollisionReady
            && coverPointsReady
            && strategyTargetsReady;
        var checkLine =
            $"TIDEGLASS_DENSITY_CHECK valid={valid} "
            + $"build_ms={buildMilliseconds:0.00}/{TideglassDensityMaximumBuildMilliseconds:0} "
            + $"bounds={boundsReady}:{layout.WorldBounds.Size.X:0.00}x{layout.WorldBounds.Size.Y:0.00} "
            + $"sites={sitesReady}:{layout.SitePositions.Count}:{layout.SiteSeparation:0.00} "
            + $"props={layout.Props.Count}:loaded={loadedPropCount} "
            + $"authored_paths={authoredPathReady}:{uniqueAuthoredPathCount}/{allAuthoredPaths.Length}:"
            + $"packs={sourcePacks.Count} "
            + $"expanded_buildings={expansionBuildingsReady}:{expansionProps.Length} "
            + $"major_buildings={majorBuildingsReady}:{majorBuildingPositions.Length} "
            + $"regions=nw{regionCounts["northwest"]},ne{regionCounts["northeast"]},"
            + $"sw{regionCounts["southwest"]},se{regionCounts["southeast"]},mid{regionCounts["mid"]} "
            + $"attack_routes={attackRoutesReady}:{layout.AttackToALength:0.00}/"
            + $"{layout.AttackToBLength:0.00}/{layout.SiteTravelDifferenceRatio:0.000} "
            + $"ground_routes={groundRoutesReady}:6:failures={groundRouteFailures} "
            + $"lane_independence={laneIndependenceReady}:"
            + $"min={laneMetrics.AttackAToBMinimumDistance:0.00}/"
            + $"{laneMetrics.AttackAToMidMinimumDistance:0.00}/"
            + $"{laneMetrics.AttackBToMidMinimumDistance:0.00}:"
            + $"overlap={laneMetrics.AttackAToBOverlapRatio:0.000}/"
            + $"{laneMetrics.AttackAToMidOverlapRatio:0.000}/"
            + $"{laneMetrics.AttackBToMidOverlapRatio:0.000} "
            + $"sightlines={physicalSightlinesReady}:attack_sites={attackSiteBlockedCount}/{attackSitePairCount}:"
            + $"defender_sites={defenderSiteBlockedCount}/{defenderSitePairCount}:"
            + $"spawns={opposingSpawnBlockedCount}/{opposingSpawnPairCount}:sites={sitePairBlocked} "
            + $"los_failures={string.Join(',', unblockedSightlines)} "
            + $"spatial={spatialDensityReady}:samples={spatialMetrics.PlayableSampleCount}:"
            + $"longest_los={spatialMetrics.LongestHorizontalSightline:0.00}@"
            + $"{TideglassDensityLocalPoint(spatialMetrics.LongestSightlineFrom, layout.Origin)}>"
            + $"{TideglassDensityLocalPoint(spatialMetrics.LongestSightlineTo, layout.Origin)}:"
            + $"open_diameter={spatialMetrics.LargestOpenDiameter:0.00}@"
            + $"{TideglassDensityLocalPoint(spatialMetrics.LargestOpenCenter, layout.Origin)} "
            + $"site_cover={siteCoverReady}:A={siteACover.DirectionalCoverage:0.000}/"
            + $"{siteACover.CoveredSampleRatio:0.000}/{siteACover.PlayableSampleCount}:"
            + $"B={siteBCover.DirectionalCoverage:0.000}/"
            + $"{siteBCover.CoveredSampleRatio:0.000}/{siteBCover.PlayableSampleCount} "
            + $"collision_invisible={invisibleCollisionReady}:visuals={collisionVisuals.Length}:"
            + $"detached_partitions={detachedPartitions.Count} "
            + $"cover_points={coverPointsReady}:{coverPointChecks.Length}:failures={coverPointFailures} "
            + $"strategies={strategyTargetsReady}:{strategyChecks.Length}:failures={strategyFailures} "
            + $"failures={string.Join('|', failures)}";

        CleanupTideglassDensityDiagnostic();
        dressingRoot = null;
        arena = null!;
        layout = null!;
        GD.Print(checkLine);
        GD.Print($"TIDEGLASS_DENSITY_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private void CleanupTideglassDensityDiagnostic()
    {
        if (_demolitionArena is not null
            && GodotObject.IsInstanceValid(_demolitionArena.Root))
        {
            _demolitionArena.SetActive(false);
        }
        _demolitionArena = null;
        _demolitionRoutePlanner = null;
        _demolitionSites.Clear();
    }

    private static bool TideglassDensityIsMajorBuilding(DemolitionArenaProp prop)
        => prop.ScenePath.Contains(
                "/quaternius_buildings_pack/",
                StringComparison.OrdinalIgnoreCase)
            || prop.ScenePath.Contains(
                "/trey_modular_industrial/",
                StringComparison.OrdinalIgnoreCase)
            || (prop.ScenePath.Contains(
                    "/quaternius_downtown_city/",
                    StringComparison.OrdinalIgnoreCase)
                && prop.CollisionSize.Y * prop.Scale >= 10.0f)
            || prop.Name == "SightBlockConstructionSiteOffice";

    private static bool TideglassDensityHasExpectedExpansionScenePath(
        DemolitionArenaProp prop)
    {
        var expectedFile = prop.Name switch
        {
            "NorthFreightOffice" or "WestGateOffice" => "inspection-office.glb",
            "EastOperationsOffice" or "SouthWorksOffice"
                or "SouthTransitOffice" or "MidDispatchOffice" => "shift-office.glb",
            _ => string.Empty
        };
        return string.IsNullOrEmpty(expectedFile)
            || prop.ScenePath.EndsWith($"/{expectedFile}", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, int> TideglassDensityBuildingRegionCounts(
        IEnumerable<Vector3> positions,
        Vector3 origin)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["northwest"] = 0,
            ["northeast"] = 0,
            ["southwest"] = 0,
            ["southeast"] = 0,
            ["mid"] = 0
        };
        foreach (var position in positions)
        {
            var local = position - origin;
            if (Mathf.Abs(local.X) <= 24.0f && Mathf.Abs(local.Z) <= 24.0f)
            {
                counts["mid"]++;
            }
            else if (local.Z < 0.0f)
            {
                counts[local.X < 0.0f ? "northwest" : "northeast"]++;
            }
            else
            {
                counts[local.X < 0.0f ? "southwest" : "southeast"]++;
            }
        }
        return counts;
    }

    private static TideglassDensitySpatialMetrics TideglassDensityMeasureSpatialMetrics(
        World3D world,
        DemolitionArenaLayout layout)
    {
        const float sampleSpacing = 2.5f;
        const float boundaryMargin = 2.0f;
        const float rayLength = 170.0f;
        const int directionCount = 24;
        var playableSampleCount = 0;
        var longestSightline = 0.0f;
        var longestFrom = Vector3.Zero;
        var longestTo = Vector3.Zero;
        var largestOpenDiameter = 0.0f;
        var largestOpenCenter = Vector3.Zero;
        for (var x = layout.WorldBounds.Position.X + boundaryMargin;
             x <= layout.WorldBounds.End.X - boundaryMargin;
             x += sampleSpacing)
        {
            for (var z = layout.WorldBounds.Position.Y + boundaryMargin;
                 z <= layout.WorldBounds.End.Y - boundaryMargin;
                 z += sampleSpacing)
            {
                var feet = new Vector3(x, layout.Origin.Y + 0.2f, z);
                if (!TideglassPhysicalRouteClear(
                        world, new[] { feet, feet }, out _))
                {
                    continue;
                }

                playableSampleCount++;
                var eye = new Vector3(x, layout.Origin.Y + 1.57f, z);
                var nearestObstacle = rayLength;
                for (var directionIndex = 0;
                     directionIndex < directionCount;
                     directionIndex++)
                {
                    var angle = Mathf.Tau * directionIndex / directionCount;
                    var direction = new Vector3(
                        Mathf.Cos(angle),
                        0.0f,
                        Mathf.Sin(angle));
                    var rayEnd = eye + direction * rayLength;
                    var hitPoint = PhysicsRaycast.TryHit(
                        world, eye, rayEnd, 1u, out var hit)
                        ? hit.Position
                        : rayEnd;
                    var distance = TideglassDensityHorizontalDistance(
                        eye, hitPoint);
                    nearestObstacle = Mathf.Min(nearestObstacle, distance);
                    if (distance <= longestSightline)
                    {
                        continue;
                    }

                    longestSightline = distance;
                    longestFrom = eye;
                    longestTo = hitPoint;
                }

                var openDiameter = nearestObstacle * 2.0f;
                if (openDiameter > largestOpenDiameter)
                {
                    largestOpenDiameter = openDiameter;
                    largestOpenCenter = eye;
                }
            }
        }

        return new TideglassDensitySpatialMetrics(
            playableSampleCount,
            longestSightline,
            longestFrom,
            longestTo,
            largestOpenDiameter,
            largestOpenCenter);
    }

    private static TideglassDensitySiteCoverMetrics TideglassDensityMeasureSiteCover(
        World3D world,
        DemolitionArenaLayout layout,
        Vector3 site)
    {
        const int directionCount = 16;
        const float coverProbeDistance = 12.0f;
        const int minimumCoveredDirections = 3;
        var playableSamples = 0;
        var coveredSamples = 0;
        var blockedDirections = 0;
        for (var xIndex = -4; xIndex <= 4; xIndex++)
        {
            for (var zIndex = -4; zIndex <= 4; zIndex++)
            {
                var feet = new Vector3(
                    site.X + xIndex,
                    layout.Origin.Y + 0.2f,
                    site.Z + zIndex);
                if (!layout.IsInsideArena(feet)
                    || !TideglassPhysicalRouteClear(
                        world, new[] { feet, feet }, out _))
                {
                    continue;
                }

                playableSamples++;
                var eye = new Vector3(feet.X, layout.Origin.Y + 1.57f, feet.Z);
                var sampleBlockedDirections = 0;
                for (var directionIndex = 0;
                     directionIndex < directionCount;
                     directionIndex++)
                {
                    var angle = Mathf.Tau * directionIndex / directionCount;
                    var direction = new Vector3(
                        Mathf.Cos(angle),
                        0.0f,
                        Mathf.Sin(angle));
                    if (!PhysicsRaycast.HasHit(
                            world,
                            eye,
                            eye + direction * coverProbeDistance,
                            1u))
                    {
                        continue;
                    }

                    sampleBlockedDirections++;
                    blockedDirections++;
                }

                if (sampleBlockedDirections >= minimumCoveredDirections)
                {
                    coveredSamples++;
                }
            }
        }

        var rayCount = playableSamples * directionCount;
        return new TideglassDensitySiteCoverMetrics(
            playableSamples,
            rayCount,
            blockedDirections / (float)Mathf.Max(rayCount, 1),
            coveredSamples / (float)Mathf.Max(playableSamples, 1));
    }

    private static bool TideglassDensitySiteCoverReady(
        TideglassDensitySiteCoverMetrics metrics)
        => metrics.PlayableSampleCount >= 60
            && metrics.RayCount == metrics.PlayableSampleCount * 16
            && metrics.DirectionalCoverage
                >= TideglassDensityMinimumSiteDirectionalCover
            && metrics.CoveredSampleRatio
                >= TideglassDensityMinimumCoveredSiteSamples;

    private static TideglassDensityLaneMetrics TideglassDensityMeasureLaneIndependence(
        DemolitionArenaLayout layout)
    {
        var attackA = TideglassDensitySampleRouteAfterDistance(
            layout.AttackToAPath, TideglassDensitySharedSpawnAllowance);
        var attackB = TideglassDensitySampleRouteAfterDistance(
            layout.AttackToBPath, TideglassDensitySharedSpawnAllowance);
        var attackMid = TideglassDensitySampleRouteAfterDistance(
            layout.AttackMidPath, TideglassDensitySharedSpawnAllowance);
        return new TideglassDensityLaneMetrics(
            TideglassDensityMinimumRouteDistance(attackA, attackB),
            TideglassDensityMinimumRouteDistance(attackA, attackMid),
            TideglassDensityMinimumRouteDistance(attackB, attackMid),
            TideglassDensityRouteOverlapRatio(attackA, attackB),
            TideglassDensityRouteOverlapRatio(attackA, attackMid),
            TideglassDensityRouteOverlapRatio(attackB, attackMid));
    }

    private static bool TideglassDensityLaneMetricsReady(
        TideglassDensityLaneMetrics metrics)
        => metrics.AttackAToBMinimumDistance >= TideglassDensityMinimumLaneSeparation
            && metrics.AttackAToMidMinimumDistance >= TideglassDensityMinimumLaneSeparation
            && metrics.AttackBToMidMinimumDistance >= TideglassDensityMinimumLaneSeparation
            && metrics.AttackAToBOverlapRatio <= TideglassDensityMaximumLaneOverlapRatio
            && metrics.AttackAToMidOverlapRatio <= TideglassDensityMaximumLaneOverlapRatio
            && metrics.AttackBToMidOverlapRatio <= TideglassDensityMaximumLaneOverlapRatio;

    private static IReadOnlyList<Vector3> TideglassDensitySampleRouteAfterDistance(
        IReadOnlyList<Vector3> route,
        float excludedDistance)
    {
        const float sampleSpacing = 1.0f;
        var routeLength = TideglassDensityRouteLength(route);
        if (routeLength <= excludedDistance)
        {
            return Array.Empty<Vector3>();
        }

        var samples = new List<Vector3>();
        for (var distance = excludedDistance;
             distance < routeLength;
             distance += sampleSpacing)
        {
            samples.Add(TideglassDensityRoutePointAtDistance(route, distance));
        }
        samples.Add(route[^1]);
        return samples;
    }

    private static float TideglassDensityRouteLength(IReadOnlyList<Vector3> route)
    {
        var length = 0.0f;
        for (var index = 1; index < route.Count; index++)
        {
            length += TideglassDensityHorizontalDistance(
                route[index - 1], route[index]);
        }
        return length;
    }

    private static Vector3 TideglassDensityRoutePointAtDistance(
        IReadOnlyList<Vector3> route,
        float distance)
    {
        var remaining = Mathf.Max(distance, 0.0f);
        for (var index = 1; index < route.Count; index++)
        {
            var from = route[index - 1];
            var to = route[index];
            var segmentLength = TideglassDensityHorizontalDistance(from, to);
            if (remaining <= segmentLength)
            {
                return from.Lerp(to, remaining / Mathf.Max(segmentLength, 0.001f));
            }
            remaining -= segmentLength;
        }
        return route[^1];
    }

    private static float TideglassDensityMinimumRouteDistance(
        IReadOnlyList<Vector3> first,
        IReadOnlyList<Vector3> second)
        => first.SelectMany(left => second.Select(right =>
                TideglassDensityHorizontalDistance(left, right)))
            .DefaultIfEmpty(0.0f)
            .Min();

    private static float TideglassDensityRouteOverlapRatio(
        IReadOnlyList<Vector3> first,
        IReadOnlyList<Vector3> second)
    {
        if (first.Count == 0 || second.Count == 0)
        {
            return 1.0f;
        }

        var firstOverlap = first.Count(point => second.Any(other =>
            TideglassDensityHorizontalDistance(point, other)
                < TideglassDensityLaneOverlapDistance)) / (float)first.Count;
        var secondOverlap = second.Count(point => first.Any(other =>
            TideglassDensityHorizontalDistance(point, other)
                < TideglassDensityLaneOverlapDistance)) / (float)second.Count;
        return Mathf.Max(firstOverlap, secondOverlap);
    }

    private static float TideglassDensityHorizontalDistance(Vector3 from, Vector3 to)
        => new Vector2(from.X - to.X, from.Z - to.Z).Length();

    private static string TideglassDensityLocalPoint(Vector3 point, Vector3 origin)
    {
        var local = point - origin;
        return $"{local.X:0.0},{local.Z:0.0}";
    }

    private static bool TideglassDensitySightlineBlocked(
        World3D world,
        Vector3 from,
        Vector3 to)
        => PhysicsRaycast.HasHit(
            world,
            from + Vector3.Up * 1.57f,
            to + Vector3.Up * 1.57f,
            1u);

    private static string TideglassDensitySightlineFailure(
        TideglassDensitySightlineCheck check,
        Vector3 origin)
    {
        var from = check.From - origin;
        var to = check.To - origin;
        return $"{check.Name}@{from.X:0.0},{from.Z:0.0}>{to.X:0.0},{to.Z:0.0}";
    }

    private static IReadOnlyList<string> TideglassDensityDetachedVisiblePartitions(
        DemolitionArenaLayout layout)
    {
        const float attachmentTolerance = 0.15f;
        var fullHeightBoxes = layout.CollisionBoxes.Where(box =>
            box.Size.Y >= 2.4f).ToArray();
        return fullHeightBoxes.Where(box =>
                box.Visible
                && Mathf.Min(box.Size.X, box.Size.Z) <= 1.2f
                && Mathf.Max(box.Size.X, box.Size.Z) >= 3.0f
                && !fullHeightBoxes.Any(anchor =>
                    anchor.Name != box.Name
                    && TideglassDensityFootprintsTouch(
                        box,
                        anchor,
                        attachmentTolerance)))
            .Select(box => box.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TideglassDensityFootprintsTouch(
        DemolitionArenaBox left,
        DemolitionArenaBox right,
        float tolerance)
    {
        var xGap = Mathf.Max(
            Mathf.Abs(left.Center.X - right.Center.X)
                - (left.Size.X + right.Size.X) * 0.5f,
            0.0f);
        var zGap = Mathf.Max(
            Mathf.Abs(left.Center.Z - right.Center.Z)
                - (left.Size.Z + right.Size.Z) * 0.5f,
            0.0f);
        return new Vector2(xGap, zGap).Length() <= tolerance;
    }
}
