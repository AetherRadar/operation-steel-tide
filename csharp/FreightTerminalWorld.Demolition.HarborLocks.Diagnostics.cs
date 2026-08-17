using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateHarborLocks()
    {
        await WaitFrames(3);
        _demolitionSelectedMapId = DemolitionMapCatalog.HarborLocksId;
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.Print("HARBOR_LOCKS_CHECK valid=False built=False");
            GD.Print("HARBOR_LOCKS_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        var arena = _demolitionArena;
        var layout = arena.Layout;
        var initiallyIsolated = !arena.Active
            && !arena.Root.Visible
            && arena.ActiveCollisionBodyCount == 0;
        GetTree().Paused = false;
        DisableActorsForSurvivalDiagnostics();
        arena.SetActive(true);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var catalogReady = DemolitionMapCatalog.IsAvailable(DemolitionMapCatalog.HarborLocksId)
            && DemolitionMapCatalog.Resolve(DemolitionMapCatalog.HarborLocksId).ProfileLocalizationKey
                == "demolition_map_harbor_locks_profile";
        var localizationReady = GameLocalization.Get(
                "demolition_map_harbor_locks",
                "zh",
                "HARBOR LOCKS") != "HARBOR LOCKS"
            && GameLocalization.Get(
                "demolition_map_harbor_locks_profile",
                "zh",
                "THREE LOCK LANES") != "THREE LOCK LANES";
        var importedModels = layout.Props.Count(prop =>
        {
            var body = arena.Root.GetNodeOrNull<StaticBody3D>(prop.Name);
            var model = body?.GetNodeOrNull<Node3D>("Model");
            return IsInstanceValid(body)
                && IsInstanceValid(model)
                && model!.FindChildren("*", "MeshInstance3D", true, false).Count > 0;
        });
        var assetsReady = layout.Props.Count >= 16
            && importedModels == layout.Props.Count;
        var topologyReady = layout.MapId == DemolitionMapCatalog.HarborLocksId
            && layout.HasThreeAttackRoutes
            && layout.HasBalancedSiteTravel
            && layout.HasDenseCentralCover
            && layout.HasPlayerClearance
            && !layout.HasSpawnSightlineToSite(0)
            && !layout.HasSpawnSightlineToSite(1);
        var routeAClear = layout.HasCapsuleClearance(layout.AttackToAPath, out var routeABlocker);
        var routeBClear = layout.HasCapsuleClearance(layout.AttackToBPath, out var routeBBlocker);
        var routeMidClear = layout.HasCapsuleClearance(layout.AttackMidPath, out var routeMidBlocker);
        var rotationClear = layout.HasCapsuleClearance(layout.SiteRotationPath, out var rotationBlocker);
        var planner = new DemolitionRoutePlanner(layout);
        var navigationReady = layout.SitePositions.All(site =>
        {
            var attack = planner.Plan(layout.AttackSpawn, site);
            var defense = planner.Plan(layout.DefenderSpawn, site);
            return attack.ReachesDestination
                && defense.ReachesDestination
                && planner.IsRouteClear(layout.AttackSpawn, attack.Waypoints)
                && planner.IsRouteClear(layout.DefenderSpawn, defense.Waypoints);
        });
        var runtimeReady = arena.Active
            && arena.Root.Visible
            && arena.Root.Name == "HarborLocksArena"
            && arena.CollisionBodyCount >= 55
            && arena.ActiveCollisionBodyCount == arena.CollisionBodyCount
            && arena.AllStaticBodiesUseWorldLayer()
            && arena.Sites.Count == 2;
        var valid = initiallyIsolated
            && catalogReady
            && localizationReady
            && assetsReady
            && topologyReady
            && routeAClear
            && routeBClear
            && routeMidClear
            && rotationClear
            && navigationReady
            && runtimeReady;
        GD.Print($"HARBOR_LOCKS_CHECK valid={valid} catalog={catalogReady} localization={localizationReady} assets={assetsReady} imported={importedModels}/{layout.Props.Count} topology={topologyReady} navigation={navigationReady} routes={routeAClear}/{routeBClear}/{routeMidClear}/{rotationClear} blockers={routeABlocker}|{routeBBlocker}|{routeMidBlocker}|{rotationBlocker} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} sites={arena.Sites.Count}");
        GD.Print($"HARBOR_LOCKS_PASS valid={valid}");

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

    private void CaptureHarborLocks()
    {
        _demolitionSelectedMapId = DemolitionMapCatalog.HarborLocksId;
        CaptureDemolitionArena();
    }
}
