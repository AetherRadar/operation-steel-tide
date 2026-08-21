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
            if (!IsInstanceValid(body) || !IsInstanceValid(model))
            {
                return false;
            }
            var meshes = model!.FindChildren("*", "MeshInstance3D", true, false);
            using var meshesBacking = meshes.AsDisposable();
            return meshes.Count > 0;
        });
        var assetsReady = layout.Props.Count >= 16
            && importedModels == layout.Props.Count;
        var tideforge = new DemolitionArenaLayout(DemolitionMapCatalog.TideforgeId, layout.Origin);
        var independentTopology = layout.WorldBounds.Size.X > layout.WorldBounds.Size.Y
            && tideforge.WorldBounds.Size.X < tideforge.WorldBounds.Size.Y
            && layout.AttackSpawn.DistanceTo(tideforge.AttackSpawn) >= 20.0f
            && layout.DefenderSpawn.DistanceTo(tideforge.DefenderSpawn) >= 20.0f
            && layout.SitePositions.Zip(tideforge.SitePositions)
                .All(pair => pair.First.DistanceTo(pair.Second) >= 20.0f);
        var largeBuildings = layout.Props.Count(prop =>
            prop.CollisionSize.X * prop.Scale * prop.CollisionSize.Z * prop.Scale >= 75.0f);
        var propsInsideBounds = layout.Props.All(prop => HarborPropInsideBounds(layout, prop));
        var propsSeparated = HarborPropsSeparated(layout.Props, out var overlapPair);
        var sitesClear = layout.SitePositions.All(site => layout.Props.All(prop =>
            !HarborPointOverlapsProp(site, 3.4f, prop)));
        var spawnsClear = layout.AttackSpawns.Concat(layout.DefenderSpawns).All(spawn =>
            layout.Props.All(prop => !HarborPointOverlapsProp(spawn, 0.6f, prop)));
        var collisionCoverageFailures = layout.Props
            .Where(prop => !HarborPropCollisionCoversModel(arena.Root, prop))
            .Select(prop => prop.Name)
            .ToArray();
        var collisionCoverage = collisionCoverageFailures.Length == 0;
        var geometryReady = independentTopology
            && largeBuildings >= 7
            && propsInsideBounds
            && propsSeparated
            && sitesClear
            && spawnsClear
            && collisionCoverage;
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
        var defenderAClear = layout.HasCapsuleClearance(layout.DefenderToAPath, out var defenderABlocker);
        var defenderBClear = layout.HasCapsuleClearance(layout.DefenderToBPath, out var defenderBBlocker);
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
            && arena.CollisionBodyCount >= 40
            && arena.ActiveCollisionBodyCount == arena.CollisionBodyCount
            && arena.AllStaticBodiesUseWorldLayer()
            && arena.Sites.Count == 2;
        var valid = initiallyIsolated
            && catalogReady
            && localizationReady
            && assetsReady
            && geometryReady
            && topologyReady
            && routeAClear
            && routeBClear
            && routeMidClear
            && defenderAClear
            && defenderBClear
            && rotationClear
            && navigationReady
            && runtimeReady;
        GD.Print($"HARBOR_LOCKS_CHECK valid={valid} catalog={catalogReady} localization={localizationReady} assets={assetsReady} imported={importedModels}/{layout.Props.Count} independent={independentTopology} large_buildings={largeBuildings} bounds={propsInsideBounds} separated={propsSeparated} overlap={overlapPair} site_clear={sitesClear} spawn_clear={spawnsClear} collision_coverage={collisionCoverage} collision_failures={string.Join('|', collisionCoverageFailures)} topology={topologyReady} navigation={navigationReady} routes={routeAClear}/{routeBClear}/{routeMidClear}/{defenderAClear}/{defenderBClear}/{rotationClear} blockers={routeABlocker}|{routeBBlocker}|{routeMidBlocker}|{defenderABlocker}|{defenderBBlocker}|{rotationBlocker} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} sites={arena.Sites.Count}");
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

    private static bool HarborPropsSeparated(
        System.Collections.Generic.IReadOnlyList<DemolitionArenaProp> props,
        out string overlapPair)
    {
        for (var first = 0; first < props.Count; first++)
        {
            var firstBounds = HarborPropBounds(props[first], 0.25f);
            for (var second = first + 1; second < props.Count; second++)
            {
                if (!firstBounds.Intersects(HarborPropBounds(props[second], 0.25f)))
                {
                    continue;
                }
                overlapPair = $"{props[first].Name}|{props[second].Name}";
                return false;
            }
        }
        overlapPair = "none";
        return true;
    }

    private static bool HarborPropInsideBounds(DemolitionArenaLayout layout, DemolitionArenaProp prop)
    {
        var bounds = HarborPropBounds(prop, 0.25f);
        return bounds.Position.X >= layout.WorldBounds.Position.X
            && bounds.Position.Y >= layout.WorldBounds.Position.Y
            && bounds.End.X <= layout.WorldBounds.End.X
            && bounds.End.Y <= layout.WorldBounds.End.Y;
    }

    private static bool HarborPointOverlapsProp(Vector3 point, float radius, DemolitionArenaProp prop)
        => HarborPropBounds(prop, radius).HasPoint(new Vector2(point.X, point.Z));

    private static Rect2 HarborPropBounds(DemolitionArenaProp prop, float margin)
    {
        var basis = new Basis(Vector3.Up, prop.Yaw);
        var half = prop.CollisionSize * prop.Scale * 0.5f;
        var center = prop.Position + basis * (prop.CollisionOffset * prop.Scale);
        var extentX = Mathf.Abs(basis.X.X) * half.X + Mathf.Abs(basis.Z.X) * half.Z + margin;
        var extentZ = Mathf.Abs(basis.X.Z) * half.X + Mathf.Abs(basis.Z.Z) * half.Z + margin;
        return new Rect2(
            new Vector2(center.X - extentX, center.Z - extentZ),
            new Vector2(extentX * 2.0f, extentZ * 2.0f));
    }

    private static bool HarborPropCollisionCoversModel(Node3D root, DemolitionArenaProp prop)
    {
        var body = root.GetNodeOrNull<StaticBody3D>(prop.Name);
        var model = body?.GetNodeOrNull<Node3D>("Model");
        var collision = body?.GetNodeOrNull<CollisionShape3D>("Collision");
        if (!IsInstanceValid(body)
            || !IsInstanceValid(model)
            || collision?.Shape is not BoxShape3D box)
        {
            return false;
        }

        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshes = model!.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        foreach (var child in meshes)
        {
            if (child is not MeshInstance3D mesh)
            {
                continue;
            }
            var meshBounds = mesh.GetAabb();
            for (var corner = 0; corner < 8; corner++)
            {
                var local = meshBounds.Position + new Vector3(
                    (corner & 1) == 0 ? 0.0f : meshBounds.Size.X,
                    (corner & 2) == 0 ? 0.0f : meshBounds.Size.Y,
                    (corner & 4) == 0 ? 0.0f : meshBounds.Size.Z);
                var bodyPoint = body!.ToLocal(mesh.ToGlobal(local));
                minimum = new Vector3(
                    Mathf.Min(minimum.X, bodyPoint.X),
                    Mathf.Min(minimum.Y, bodyPoint.Y),
                    Mathf.Min(minimum.Z, bodyPoint.Z));
                maximum = new Vector3(
                    Mathf.Max(maximum.X, bodyPoint.X),
                    Mathf.Max(maximum.Y, bodyPoint.Y),
                    Mathf.Max(maximum.Z, bodyPoint.Z));
            }
        }

        var tolerance = Vector3.One * 0.08f;
        var collisionMinimum = collision.Position - box.Size * 0.5f - tolerance;
        var collisionMaximum = collision.Position + box.Size * 0.5f + tolerance;
        var covered = !float.IsInfinity(minimum.X)
            && minimum.X >= collisionMinimum.X
            && minimum.Y >= collisionMinimum.Y
            && minimum.Z >= collisionMinimum.Z
            && maximum.X <= collisionMaximum.X
            && maximum.Y <= collisionMaximum.Y
            && maximum.Z <= collisionMaximum.Z;
        if (!covered)
        {
            GD.Print($"HARBOR_COLLISION_CHECK prop={prop.Name} mesh_min={minimum} mesh_max={maximum} collision_min={collisionMinimum} collision_max={collisionMaximum}");
        }
        return covered;
    }
}
