using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateBazaarCrossing()
    {
        try
        {
            await ValidateBazaarCrossingCore();
        }
        catch (Exception exception)
        {
            GD.PushError($"Bazaar Crossing diagnostic failed: {exception}");
            GD.Print(
                $"BAZAAR_CROSSING_CHECK valid=False exception={exception.GetType().Name}");
            GD.Print("BAZAAR_CROSSING_PASS valid=False");
            try
            {
                CleanupBazaarCrossingDiagnostic();
            }
            catch (Exception cleanupException)
            {
                GD.PushError($"Bazaar Crossing diagnostic cleanup failed: {cleanupException}");
            }
            finally
            {
                QuitDiagnosticAfterSceneCleanup(2);
            }
        }
    }

    private async Task ValidateBazaarCrossingCore()
    {
        await WaitFrames(3);
        _demolitionSelectedMapId = DemolitionMapCatalog.BazaarCrossingId;
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.Print("BAZAAR_CROSSING_CHECK valid=False built=False");
            GD.Print("BAZAAR_CROSSING_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
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

        var offer = DemolitionMapCatalog.Resolve(DemolitionMapCatalog.BazaarCrossingId);
        var catalogReady = offer.Id == DemolitionMapCatalog.BazaarCrossingId
            && offer.Code == "MAP 06"
            && offer.Available
            && offer.EnglishName == "BAZAAR CROSSING"
            && offer.EnglishSubtitle == "OLD-CITY MARKET  //  GALLERIES AND BRIDGES"
            && offer.LocalizationKey == "demolition_map_bazaar_crossing"
            && offer.SubtitleLocalizationKey == "demolition_map_bazaar_crossing_subtitle"
            && offer.ProfileLocalizationKey == "demolition_map_bazaar_crossing_profile"
            && DemolitionMapCatalog.IsAvailable(DemolitionMapCatalog.BazaarCrossingId);
        var localizationReady = GameLocalization.Get(
                offer.LocalizationKey, "en", offer.EnglishName) == offer.EnglishName
            && GameLocalization.Get(
                offer.SubtitleLocalizationKey, "en", offer.EnglishSubtitle) == offer.EnglishSubtitle
            && GameLocalization.Get(
                offer.LocalizationKey, "zh", offer.EnglishName) != offer.EnglishName
            && GameLocalization.Get(
                offer.SubtitleLocalizationKey, "zh", offer.EnglishSubtitle) != offer.EnglishSubtitle
            && GameLocalization.Get(
                offer.ProfileLocalizationKey, "zh", offer.EnglishProfile) != offer.EnglishProfile;

        var boundsReady = layout.MapId == DemolitionMapCatalog.BazaarCrossingId
            && arena.Root.Name == "BazaarCrossingArena"
            && Mathf.IsEqualApprox(layout.WorldBounds.Size.X, 136.0f)
            && Mathf.IsEqualApprox(layout.WorldBounds.Size.Y, 112.0f)
            && layout.WorldBounds.Position.IsEqualApprox(
                new Vector2(layout.Origin.X - 68.0f, layout.Origin.Z - 56.0f));
        var spawnAndSiteReady = layout.AttackSpawns.Count == 5
            && layout.DefenderSpawns.Count == 5
            && layout.SitePositions.Count == 2
            && BazaarPointsSeparated(layout.AttackSpawns, 2.0f)
            && BazaarPointsSeparated(layout.DefenderSpawns, 2.0f)
            && layout.AttackSpawns.Concat(layout.DefenderSpawns).All(point => layout.IsInsideArena(point))
            && layout.SitePositions.All(point => layout.IsInsideArena(point))
            && layout.SitePositions.All(site => Mathf.Abs(site.Y - layout.Origin.Y) <= 0.25f)
            && layout.SiteSeparation >= 80.0f;

        var routeTimingReady = layout.HasThreeAttackRoutes
            && layout.HasBalancedSiteTravel
            && layout.SiteTravelDifferenceRatio <= 0.12f
            && layout.AttackToALength >= 95.0f
            && layout.AttackToBLength >= 95.0f;
        var groundRoutes = BazaarGroundRoutes(layout);
        var groundRouteChecks = groundRoutes.Select(route =>
        {
            var layoutClear = layout.HasCapsuleClearance(route.Points, out var layoutBlocker);
            var physicalClear = BazaarPhysicalRouteClear(
                GetWorld3D(), route.Points, out var physicalBlocker);
            return new
            {
                route.Name,
                LayoutClear = layoutClear,
                PhysicalClear = physicalClear,
                LayoutBlocker = layoutBlocker,
                PhysicalBlocker = physicalBlocker
            };
        }).ToArray();
        var groundRoutesReady = groundRouteChecks.Length == 7
            && groundRouteChecks.All(check => check.LayoutClear && check.PhysicalClear);
        var groundRouteFailures = string.Join('|', groundRouteChecks
            .Where(check => !check.LayoutClear || !check.PhysicalClear)
            .Select(check => $"{check.Name}:{check.LayoutBlocker}:{check.PhysicalBlocker}"));
        var retakeChoicesReady = layout.SiteRotationPath.Count >= 10
            && layout.AuxiliaryPaths.Count == 4
            && layout.AuxiliaryPaths[0].All(point => Mathf.Abs(point.Y - layout.Origin.Y) <= 0.3f)
            && layout.AuxiliaryPaths[0].Count >= 10;

        var spawnPairsBlocked = layout.AttackSpawns.SelectMany(attack =>
            layout.DefenderSpawns.Select(defense =>
                BazaarSightlineBlocked(GetWorld3D(), attack, defense))).All(blocked => blocked);
        var attackSpawnToSitesBlocked = layout.AttackSpawns.SelectMany(spawn =>
            layout.SitePositions.Select(site =>
                BazaarSightlineBlocked(GetWorld3D(), spawn, site))).All(blocked => blocked);
        var defenderSpawnToSitesBlocked = layout.DefenderSpawns.SelectMany(spawn =>
            layout.SitePositions.Select(site =>
                BazaarSightlineBlocked(GetWorld3D(), spawn, site))).All(blocked => blocked);
        var sitesMutuallyBlocked = BazaarSightlineBlocked(
            GetWorld3D(), layout.SitePositions[0], layout.SitePositions[1]);
        var sightlinesReady = spawnPairsBlocked
            && attackSpawnToSitesBlocked
            && defenderSpawnToSitesBlocked
            && sitesMutuallyBlocked
            && !layout.HasSpawnSightlineToSite(0)
            && !layout.HasSpawnSightlineToSite(1);

        var traversalReady = BazaarTraversalGeometryReady(layout, out var traversalFailures);
        var planner = new DemolitionRoutePlanner(layout);
        var verticalPlannerReady = BazaarVerticalPlannerReady(
            layout, planner, out var verticalPlannerFailures);
        var strategyRoutes = new[]
        {
            (Start: layout.AttackSpawn, Key: "postplant_guard_a"),
            (Start: layout.DefenderSpawn, Key: "defense_mid"),
            (Start: layout.AttackSpawn, Key: "postplant_guard_b")
        };
        var strategyRouteChecks = strategyRoutes.Select(entry =>
        {
            var target = layout.StrategyTarget(entry.Key);
            var route = planner.Plan(entry.Start, target);
            return new
            {
                entry.Key,
                Target = target,
                Route = route,
                Clear = planner.IsRouteClear(entry.Start, route.Waypoints)
            };
        }).ToArray();
        var elevatedStrategiesReady = strategyRouteChecks.All(check =>
            check.Target.Y >= layout.Origin.Y + 2.6f
            && check.Route.ReachesDestination
            && check.Route.Waypoints.Count >= 3
            && check.Route.Waypoints.Any(point => point.Y >= check.Target.Y - 0.1f)
            && check.Clear);
        var elevatedStrategyFailures = string.Join('|', strategyRouteChecks
            .Where(check => check.Target.Y < layout.Origin.Y + 2.6f
                || !check.Route.ReachesDestination
                || !check.Clear)
            .Select(check => $"{check.Key}:{check.Route.ReachesDestination}:{check.Route.Waypoints.Count}"));
        var allStrategyRouteChecks = DemolitionArenaLayout.StrategyTargetKeys.Select(key =>
        {
            var target = layout.StrategyTarget(key);
            var attackOwned = key.StartsWith("attack_", StringComparison.Ordinal)
                || key.StartsWith("postplant_", StringComparison.Ordinal);
            var defenseOwned = key.StartsWith("defense_", StringComparison.Ordinal)
                || key.StartsWith("retake_", StringComparison.Ordinal)
                || key.StartsWith("site_", StringComparison.Ordinal);
            var start = attackOwned ? layout.AttackSpawn : layout.DefenderSpawn;
            var route = planner.Plan(start, target);
            return new
            {
                Key = key,
                OwnerKnown = attackOwned != defenseOwned,
                Route = route,
                Clear = planner.IsRouteClear(start, route.Waypoints)
            };
        }).ToArray();
        var allStrategiesReady = allStrategyRouteChecks.All(check =>
            check.OwnerKnown && check.Route.ReachesDestination && check.Clear);
        var allStrategyFailures = string.Join('|', allStrategyRouteChecks
            .Where(check => !check.OwnerKnown || !check.Route.ReachesDestination || !check.Clear)
            .Select(check => $"{check.Key}:{check.OwnerKnown}:{check.Route.ReachesDestination}:{check.Clear}"));
        var postPatrolReady = ValidateDemolitionPostPatrolLayout(layout);

        var aiTraversal = await BazaarAiRouteDirectivesReady(layout);
        var aiDirectivesReady = aiTraversal.Ready;
        var aiDirectiveFailures = aiTraversal.Failures;
        var stairWalks = await BazaarWalkAllStairs(layout);
        var playerStairsReady = stairWalks.Count == 6 && stairWalks.All(walk => walk.Ready);
        var stairWalkFailures = string.Join('|', stairWalks
            .Where(walk => !walk.Ready)
            .Select(walk => $"{walk.Name}:{walk.Ascended}:{walk.Descended}:{walk.AscendGain:0.00}:{walk.DescendLoss:0.00}"));

        var dressingRoot = arena.Root.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing");
        var authoredModelCount = dressingRoot?.GetMeta("authored_model_count").AsInt32() ?? 0;
        var missingModelCount = dressingRoot?.GetMeta("missing_model_count").AsInt32() ?? -1;
        var uniqueSceneCount = dressingRoot?.GetMeta("unique_scene_count").AsInt32() ?? 0;
        var authoredPath = "res://assets/models/bazaar_crossing/bazaar_crossing.glb";
        var authoredVisualFailures = "not-checked";
        var visibleMeshCount = 0;
        var authoredVisualsReady = ResourceLoader.Exists(authoredPath)
            && authoredModelCount == 1
            && missingModelCount == 0
            && uniqueSceneCount == 1
            && BazaarAuthoredVisualsReady(
                dressingRoot,
                layout,
                out authoredVisualFailures,
                out visibleMeshCount);
        var siblingNamesReady = BazaarSiblingNamesUnique(arena.Root, out var siblingFailures);
        var runtimeReady = arena.Active
            && arena.Root.Visible
            && arena.Root.ProcessMode != ProcessModeEnum.Disabled
            && arena.CollisionBodyCount >= layout.CollisionBoxes.Count + layout.TraversalBoxes.Count
            && arena.ActiveCollisionBodyCount == arena.CollisionBodyCount
            && arena.AllStaticBodiesUseWorldLayer()
            && arena.VisualPartCount >= authoredModelCount + 10
            && arena.Sites.Count == 2
            && arena.Sites.Select((site, index) =>
                site.Position.IsEqualApprox(layout.SitePositions[index])).All(equal => equal)
            && layout.DetailBoxes.Count == 0
            && layout.Props.Count == 0;

        arena.SetActive(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var deactivatedReady = !arena.Active
            && !arena.Root.Visible
            && arena.Root.ProcessMode == ProcessModeEnum.Disabled
            && arena.ActiveCollisionBodyCount == 0
            && arena.AllStaticBodiesUseWorldLayer();

        var valid = initiallyIsolated
            && catalogReady
            && localizationReady
            && boundsReady
            && spawnAndSiteReady
            && routeTimingReady
            && groundRoutesReady
            && retakeChoicesReady
            && sightlinesReady
            && traversalReady
            && verticalPlannerReady
            && elevatedStrategiesReady
            && allStrategiesReady
            && postPatrolReady
            && aiDirectivesReady
            && playerStairsReady
            && authoredVisualsReady
            && siblingNamesReady
            && runtimeReady
            && deactivatedReady;
        GD.Print(
            $"BAZAAR_CROSSING_CHECK valid={valid} catalog={catalogReady} localization={localizationReady} "
            + $"bounds={boundsReady}:136x112 root={arena.Root.Name} spawns_sites={spawnAndSiteReady}:5v5:2 "
            + $"timing={routeTimingReady}:{layout.AttackToALength:0.00}:{layout.AttackToBLength:0.00}:{layout.SiteTravelDifferenceRatio:0.000} "
            + $"ground_routes={groundRoutesReady}:7 failures={groundRouteFailures} retakes={retakeChoicesReady}:2 "
            + $"sightlines={sightlinesReady} spawn_pair={spawnPairsBlocked} attack_sites={attackSpawnToSitesBlocked} "
            + $"defender_sites={defenderSpawnToSitesBlocked} site_pair={sitesMutuallyBlocked} "
            + $"traversal={traversalReady}:decks3:ramps6 failures={traversalFailures} "
            + $"vertical_planner={verticalPlannerReady} failures={verticalPlannerFailures} "
            + $"elevated_strategies={elevatedStrategiesReady}:3 failures={elevatedStrategyFailures} "
            + $"all_strategies={allStrategiesReady}:{allStrategyRouteChecks.Length} failures={allStrategyFailures} "
            + $"post_patrol={postPatrolReady} "
            + $"ai_directives={aiDirectivesReady} result={aiTraversal.Summary} failures={aiDirectiveFailures} "
            + $"player_stairs={playerStairsReady}:{stairWalks.Count} failures={stairWalkFailures} "
            + $"authored={authoredVisualsReady}:1/{missingModelCount} meshes={visibleMeshCount} failures={authoredVisualFailures} "
            + $"siblings={siblingNamesReady} sibling_failures={siblingFailures} "
            + $"runtime={runtimeReady} lifecycle={initiallyIsolated}/{deactivatedReady} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} sites={arena.Sites.Count}");
        GD.Print($"BAZAAR_CROSSING_PASS valid={valid}");

        CleanupBazaarCrossingDiagnostic();
        dressingRoot = null;
        arena = null!;
        layout = null!;
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private void CleanupBazaarCrossingDiagnostic()
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

    private static bool BazaarSiblingNamesUnique(Node root, out string failures)
    {
        var failed = new List<string>();
        BazaarCollectDuplicateSiblingNames(root, failed);
        failures = string.Join('|', failed.Take(24));
        return failed.Count == 0;
    }

    private static void BazaarCollectDuplicateSiblingNames(Node parent, List<string> failed)
    {
        var childNodes = parent.GetChildren();
        using var childNodesBacking = childNodes.AsDisposable();
        var children = childNodes.OfType<Node>().ToArray();
        foreach (var group in children.GroupBy(child => child.Name.ToString(), StringComparer.Ordinal))
        {
            if (group.Count() > 1 || group.Key.StartsWith('@'))
            {
                failed.Add($"{parent.Name}/{group.Key}");
            }
        }
        foreach (var child in children)
        {
            BazaarCollectDuplicateSiblingNames(child, failed);
        }
    }
}
