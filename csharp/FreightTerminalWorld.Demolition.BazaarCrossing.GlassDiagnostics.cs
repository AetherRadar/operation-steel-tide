using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarPortalGlassDiagnostic(
        bool Ready,
        int PortalCount,
        int ShotClearedCount,
        bool PlayerReady,
        bool SquadReady,
        bool EnemyReady,
        bool ResetReady,
        string Failures,
        string ActorSummary);

    private async Task<BazaarPortalGlassDiagnostic> BazaarPortalGlassReady(
        DemolitionArenaRuntime arena,
        DemolitionArenaLayout layout)
    {
        var failed = new List<string>();
        if (arena.BazaarGlassFields.Count != 1)
        {
            return new BazaarPortalGlassDiagnostic(
                false,
                layout.BazaarGlassPortals.Count,
                0,
                false,
                false,
                false,
                false,
                $"field-count-{arena.BazaarGlassFields.Count}/1",
                "not-run");
        }

        var field = arena.BazaarGlassFields[0];
        var portals = layout.BazaarGlassPortals;
        var snapshot = field.CaptureStateForDiagnostics();
        var shotClearedCount = 0;
        var playerReady = false;
        var squadReady = false;
        var enemyReady = false;
        var resetReady = false;
        var actorSummary = "not-run";
        try
        {
            field.ResetAllPanes();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (portals.Count != 28
                || portals.Select(portal => portal.Name).Distinct(StringComparer.Ordinal).Count() != 28
                || field.PaneCount != portals.Count
                || field.MovementBlockerCount != portals.Count
                || field.FrameInstanceCount != 0
                || field.BuildsFrames
                || !field.BlocksMovementUntilShattered
                || !field.IsFieldActive)
            {
                failed.Add(
                    $"contract-{portals.Count}:{field.PaneCount}:{field.MovementBlockerCount}:"
                    + $"{field.FrameInstanceCount}:{field.BuildsFrames}:"
                    + $"{field.BlocksMovementUntilShattered}:{field.IsFieldActive}");
            }

            for (var index = 0; index < portals.Count; index++)
            {
                // Keep every portal check independent. A Mid threshold is represented
                // by two panes one metre apart, and shattering either pane correctly
                // pre-shatters its linked partner.
                field.ResetAllPanes();
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var portal = portals[index];
                var normal = new Vector3(portal.Normal.X, 0.0f, portal.Normal.Z).Normalized();
                // Some Mid rooms have paired glass doors only one metre apart.
                // Probe from the named wall's outward side and stay inside the
                // 0.62 m capsule-clear gap so this check cannot hit its neighbour.
                var rayFrom = portal.WorldCenter + normal * 0.35f;
                var rayTo = portal.WorldCenter - normal * 0.35f;
                var feetY = layout.Origin.Y + 0.20f;
                var route = new[]
                {
                    new Vector3(
                        portal.WorldCenter.X + normal.X * 0.45f,
                        feetY,
                        portal.WorldCenter.Z + normal.Z * 0.45f),
                    new Vector3(
                        portal.WorldCenter.X - normal.X * 0.45f,
                        feetY,
                        portal.WorldCenter.Z - normal.Z * 0.45f)
                };

                var layoutClear = layout.HasCapsuleClearance(route, out var layoutBlocker);
                var worldClear = BazaarGlassCapsuleRouteClear(
                    GetWorld3D(), route, 1u, out var worldBlocker);
                var queryBlocked = PhysicsRaycast.TryHit(
                        GetWorld3D(),
                        rayFrom,
                        rayTo,
                        BreakableGlassField.GlassCollisionLayer,
                        out var queryHit,
                        collideWithAreas: true,
                        collideWithBodies: false)
                    && queryHit.Collider == field;
                var movementRayBlocked = PhysicsRaycast.TryHit(
                        GetWorld3D(),
                        rayFrom,
                        rayTo,
                        BreakableGlassField.MovementCollisionLayer,
                        out var movementHit)
                    && movementHit.Collider is StaticBody3D movementBody
                    && movementBody.GetParent() == field;
                var movementCapsuleBlocked = !BazaarGlassCapsuleRouteClear(
                    GetWorld3D(),
                    route,
                    1u | BreakableGlassField.MovementCollisionLayer,
                    out _);
                var expectsLintel = portal.WallName.StartsWith("Wall", StringComparison.Ordinal);
                var lintelFrom = rayFrom;
                var lintelTo = rayTo;
                lintelFrom.Y = layout.Origin.Y + 3.35f;
                lintelTo.Y = layout.Origin.Y + 3.35f;
                var lintelReady = !expectsLintel || PhysicsRaycast.HasHit(
                    GetWorld3D(), lintelFrom, lintelTo, 1u);
                var lowDamageRejected = index != 0
                    || !BreakableGlassField.TryShatterAlongRay(
                        GetWorld3D(),
                        rayFrom,
                        rayTo,
                        1.0f,
                        normal,
                        out _,
                        spawnEffects: false)
                    && !field.IsPaneShattered(index);
                var shotShattered = BreakableGlassField.TryShatterAlongRay(
                        GetWorld3D(),
                        rayFrom,
                        rayTo,
                        100.0f,
                        normal,
                        out _,
                        spawnEffects: false)
                    && field.IsPaneShattered(index)
                    && field.IsPaneCollisionDisabled(index)
                    && field.IsPaneMovementCollisionDisabled(index);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var queryCleared = !PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    rayFrom,
                    rayTo,
                    BreakableGlassField.GlassCollisionLayer,
                    collideWithAreas: true,
                    collideWithBodies: false);
                var movementRayCleared = !PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    rayFrom,
                    rayTo,
                    BreakableGlassField.MovementCollisionLayer);
                var movementCapsuleCleared = BazaarGlassCapsuleRouteClear(
                    GetWorld3D(),
                    route,
                    1u | BreakableGlassField.MovementCollisionLayer,
                    out var clearedBlocker);

                var ready = layoutClear
                    && worldClear
                    && queryBlocked
                    && movementRayBlocked
                    && movementCapsuleBlocked
                    && lintelReady
                    && lowDamageRejected
                    && shotShattered
                    && queryCleared
                    && movementRayCleared
                    && movementCapsuleCleared;
                if (ready)
                {
                    shotClearedCount++;
                }
                else
                {
                    failed.Add(
                        $"{portal.Name}-{layoutClear}:{layoutBlocker}-{worldClear}:{worldBlocker}-"
                        + $"intact{queryBlocked}/{movementRayBlocked}/{movementCapsuleBlocked}-"
                        + $"lintel{lintelReady}-"
                        + $"low{lowDamageRejected}-shot{shotShattered}-"
                        + $"clear{queryCleared}/{movementRayCleared}/{movementCapsuleCleared}:{clearedBlocker}");
                }
            }

            field.ResetAllPanes();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            resetReady = field.ShatteredCount == 0
                && Enumerable.Range(0, field.PaneCount).All(index =>
                    !field.IsPaneShattered(index)
                    && !field.IsPaneCollisionDisabled(index)
                    && !field.IsPaneMovementCollisionDisabled(index));
            if (!resetReady)
            {
                failed.Add("round-reset");
            }

            var linkedGroups = await BazaarMidLinkedGlassGroupsReady(field, layout);
            if (!linkedGroups.Ready)
            {
                failed.Add($"mid-linked-groups-{linkedGroups.Summary}");
            }

            var melee = await BazaarPlayerMeleeGlassReady(field, layout);
            if (!melee.Ready)
            {
                failed.Add($"player-melee-{melee.Summary}");
            }

            var actorResult = await BazaarPortalGlassActorTraversalReady(field, layout);
            playerReady = actorResult.PlayerReady;
            squadReady = actorResult.SquadReady;
            enemyReady = actorResult.EnemyReady;
            actorSummary = $"{actorResult.Summary};melee={melee.Summary};"
                + $"mid-groups={linkedGroups.Summary}";
            if (!playerReady || !squadReady || !enemyReady)
            {
                failed.Add($"actors-{actorSummary}");
            }
        }
        finally
        {
            field.RestoreStateForDiagnostics(snapshot);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (!field.MatchesStateForDiagnostics(snapshot))
            {
                failed.Add("snapshot-restore");
            }
        }

        return new BazaarPortalGlassDiagnostic(
            failed.Count == 0,
            portals.Count,
            shotClearedCount,
            playerReady,
            squadReady,
            enemyReady,
            resetReady,
            string.Join('|', failed.Take(24)),
            actorSummary);
    }

    private async Task<(bool Ready, string Summary)> BazaarMidLinkedGlassGroupsReady(
        BreakableGlassField field,
        DemolitionArenaLayout layout)
    {
        var groupNames = new[]
        {
            (
                First: "Bazaar_Mid_NorthConnector_South_Portal00",
                Second: "Bazaar_Mid_NorthTeaHall_North_Portal00"),
            (
                First: "Bazaar_Mid_NorthTeaHall_South_Portal00",
                Second: "Bazaar_Mid_CenterProduceHall_North_Portal00"),
            (
                First: "Bazaar_Mid_CenterProduceHall_South_Portal00",
                Second: "Bazaar_Mid_SouthCarpetHall_North_Portal01")
        };
        var portalIndexByName = layout.BazaarGlassPortals
            .Select((portal, index) => (portal.Name, Index: index))
            .ToDictionary(entry => entry.Name, entry => entry.Index, StringComparer.Ordinal);
        var results = new List<string>(groupNames.Length);
        var allReady = true;
        foreach (var groupNamesEntry in groupNames)
        {
            var group = (
                First: portalIndexByName[groupNamesEntry.First],
                Second: portalIndexByName[groupNamesEntry.Second]);
            field.ResetAllPanes();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var first = layout.BazaarGlassPortals[group.First];
            var second = layout.BazaarGlassPortals[group.Second];
            var normal = new Vector3(first.Normal.X, 0.0f, first.Normal.Z).Normalized();
            var shotFrom = first.WorldCenter + normal * 0.35f;
            var shotTo = first.WorldCenter - normal * 0.35f;
            var initiallyIntact = !field.IsPaneShattered(group.First)
                && !field.IsPaneShattered(group.Second)
                && !field.IsPaneCollisionDisabled(group.First)
                && !field.IsPaneCollisionDisabled(group.Second)
                && !field.IsPaneMovementCollisionDisabled(group.First)
                && !field.IsPaneMovementCollisionDisabled(group.Second);
            var eventCount = 0;
            var eventPaneIndex = -1;
            var eventMask = 0u;
            void CaptureShatterEvent(BreakableGlassField _, int paneIndex, uint mask)
            {
                eventCount++;
                eventPaneIndex = paneIndex;
                eventMask = mask;
            }
            field.PaneShattered += CaptureShatterEvent;
            bool oneShot;
            try
            {
                oneShot = BreakableGlassField.TryShatterAlongRay(
                    GetWorld3D(),
                    shotFrom,
                    shotTo,
                    100.0f,
                    normal,
                    out _,
                    spawnEffects: false);
            }
            finally
            {
                field.PaneShattered -= CaptureShatterEvent;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            var expectedMask = (1u << group.First) | (1u << group.Second);
            var exactTwoBits = field.ShatteredPaneMask == expectedMask
                && field.ShatteredCount == 2;
            var oneFinalEvent = eventCount == 1
                && eventPaneIndex == group.First
                && eventMask == expectedMask;
            var primaryImpactPreserved = field.LastShatterPosition
                .DistanceSquaredTo(first.WorldCenter) <= 0.08f * 0.08f;
            var bothPaneStatesClear = new[] { group.First, group.Second }.All(index =>
                field.IsPaneShattered(index)
                && field.IsPaneCollisionDisabled(index)
                && field.IsPaneMovementCollisionDisabled(index));
            var bothPhysicsClear = true;
            foreach (var paneIndex in new[] { group.First, group.Second })
            {
                var pane = layout.BazaarGlassPortals[paneIndex];
                var paneNormal = new Vector3(pane.Normal.X, 0.0f, pane.Normal.Z).Normalized();
                var paneFrom = pane.WorldCenter + paneNormal * 0.35f;
                var paneTo = pane.WorldCenter - paneNormal * 0.35f;
                bothPhysicsClear &= !PhysicsRaycast.HasHit(
                        GetWorld3D(),
                        paneFrom,
                        paneTo,
                        BreakableGlassField.GlassCollisionLayer,
                        collideWithAreas: true,
                        collideWithBodies: false)
                    && !PhysicsRaycast.HasHit(
                        GetWorld3D(),
                        paneFrom,
                        paneTo,
                        BreakableGlassField.MovementCollisionLayer);
            }

            var feetY = layout.Origin.Y + 0.20f;
            var laneX = (first.WorldCenter.X + second.WorldCenter.X) * 0.5f;
            var northernZ = Mathf.Min(first.WorldCenter.Z, second.WorldCenter.Z) - 1.65f;
            var southernZ = Mathf.Max(first.WorldCenter.Z, second.WorldCenter.Z) + 1.65f;
            var start = new Vector3(laneX, feetY, northernZ);
            var target = new Vector3(laneX, feetY, southernZ);
            var routeClear = BazaarGlassCapsuleRouteClear(
                GetWorld3D(),
                new[] { start, target },
                1u | BreakableGlassField.MovementCollisionLayer,
                out var routeBlocker);
            var walk = await BazaarWalkPlayer(start, target, ascending: false);
            var ready = initiallyIntact
                && oneShot
                && exactTwoBits
                && oneFinalEvent
                && primaryImpactPreserved
                && bothPaneStatesClear
                && bothPhysicsClear
                && routeClear
                && walk.Ready;
            allReady &= ready;
            results.Add(
                $"{group.First}+{group.Second}:{ready}:"
                + $"shot{oneShot}:bits{exactTwoBits}:"
                + $"event{oneFinalEvent}:{eventCount}/{eventPaneIndex}/{eventMask:X8}:"
                + $"impact{primaryImpactPreserved}:coll{bothPaneStatesClear}/{bothPhysicsClear}:"
                + $"route{routeClear}:{routeBlocker}:walk{walk.Ready}/{walk.Frames}");
        }

        field.ResetAllPanes();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        return (allReady, string.Join(',', results));
    }

    private async Task<(bool Ready, string Summary)> BazaarPlayerMeleeGlassReady(
        BreakableGlassField field,
        DemolitionArenaLayout layout)
    {
        var paneIndex = layout.BazaarGlassPortals
            .Select((portal, index) => (Portal: portal, Index: index))
            .Single(entry => entry.Portal.Name == "Bazaar_A_RearWarehouse_CenterPortal")
            .Index;
        var portal = layout.BazaarGlassPortals[paneIndex];
        var normal = new Vector3(portal.Normal.X, 0.0f, portal.Normal.Z).Normalized();
        var feetY = layout.Origin.Y + 0.20f;
        var start = portal.WorldCenter + normal * 0.85f;
        var target = portal.WorldCenter - normal * 1.65f;
        start.Y = feetY;
        target.Y = feetY;

        field.ResetAllPanes();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease("move_forward");
        Input.ActionRelease("sprint");
        _player.UiLocked = false;
        _player.IsDead = false;
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.RestoreMovementInput();
        var knifeSelected = _player.SelectQuickSlot(PlayerQuickSlot.Melee, notify: false);
        _player.GlobalPosition = start;
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(portal.WorldCenter);
        await WaitFrames(10);

        _player.PrepareMeleeCombatFixtureForDiagnostics();
        _player.SetPhysicsProcess(true);
        _player.StartMeleeAttackForDiagnostics();
        var attackStarted = false;
        var attackFinished = false;
        var bladeSweepResolved = false;
        for (var frame = 0; frame < 120; frame++)
        {
            _player.GlobalPosition = start;
            _player.Velocity = Vector3.Zero;
            _player.FaceWorldPointForDiagnostics(portal.WorldCenter);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            attackStarted |= _player.MeleeAttackActiveForDiagnostics;
            bladeSweepResolved |= _player.MeleeBladeSweepResolvedForDiagnostics;
            if (attackStarted && !_player.MeleeAttackActiveForDiagnostics)
            {
                attackFinished = true;
                break;
            }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var paneCleared = field.IsPaneShattered(paneIndex)
            && field.IsPaneCollisionDisabled(paneIndex)
            && field.IsPaneMovementCollisionDisabled(paneIndex);
        // Keep the actor itself out of the capsule overlap query; layer 1 contains
        // characters as well as authored static geometry.
        _player.GlobalPosition = start + Vector3.Right * 8.0f;
        _player.Velocity = Vector3.Zero;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var route = new[]
        {
            portal.WorldCenter + normal * 0.45f,
            portal.WorldCenter - normal * 0.45f
        };
        route[0].Y = feetY;
        route[1].Y = feetY;
        var passageClear = BazaarGlassCapsuleRouteClear(
            GetWorld3D(),
            route,
            1u | BreakableGlassField.MovementCollisionLayer,
            out var blocker);
        var walk = await BazaarWalkPlayer(start, target, ascending: false);
        var ready = knifeSelected
            && attackStarted
            && attackFinished
            && bladeSweepResolved
            && paneCleared
            && passageClear
            && walk.Ready;
        return (
            ready,
            $"{ready}:knife{knifeSelected}:attack{attackStarted}/{attackFinished}:"
                + $"sweep{bladeSweepResolved}:pane{paneCleared}:"
                + $"passage{passageClear}:{blocker}:walk{walk.Ready}/{walk.Frames}");
    }

    private async Task<(bool PlayerReady, bool SquadReady, bool EnemyReady, string Summary)>
        BazaarPortalGlassActorTraversalReady(
            BreakableGlassField field,
            DemolitionArenaLayout layout)
    {
        var portalIndex = layout.BazaarGlassPortals
            .Select((portal, index) => (Portal: portal, Index: index))
            .Single(entry => entry.Portal.Name == "Bazaar_A_RearWarehouse_CenterPortal")
            .Index;
        var portal = layout.BazaarGlassPortals[portalIndex];
        var normal = new Vector3(portal.Normal.X, 0.0f, portal.Normal.Z).Normalized();
        var feetY = layout.Origin.Y + 0.20f;
        var start = portal.WorldCenter - normal * 1.65f;
        var target = portal.WorldCenter + normal * 1.65f;
        start.Y = feetY;
        target.Y = feetY;

        var player = await BazaarPlayerGlassTraversalReady(
            field, portalIndex, portal.WorldCenter, normal, start, target);
        var squad = await BazaarSquadGlassTraversalReady(
            field, portalIndex, start, target);
        var enemy = await BazaarEnemyGlassTraversalReady(
            field, portalIndex, start, target);
        field.ResetAllPanes();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        return (
            player.Ready,
            squad.Ready,
            enemy.Ready,
            $"player{player.Ready}:{player.Blocked}/{player.Shot}/{player.Walk.Frames};"
                + $"squad{squad.Ready}:{squad.Shattered}/{squad.Walk.Frames};"
                + $"enemy{enemy.Ready}:{enemy.Shattered}/{enemy.Walk.Frames}");
    }

    private async Task<(
        bool Ready,
        bool Blocked,
        bool Shot,
        (bool Ready, int Frames, float HeightDelta) Walk)> BazaarPlayerGlassTraversalReady(
            BreakableGlassField field,
            int paneIndex,
            Vector3 portalCenter,
            Vector3 normal,
            Vector3 start,
            Vector3 target)
    {
        field.ResetAllPanes();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Input.ActionRelease("move_forward");
        _player.ProcessMode = ProcessModeEnum.Inherit;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        _player.GlobalPosition = start;
        _player.Velocity = Vector3.Zero;
        await WaitFrames(6);
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 75; frame++)
        {
            _player.FaceWorldPointForDiagnostics(target);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward");
        var signedProgress = (_player.GlobalPosition - portalCenter).Dot(normal);
        var blocked = signedProgress <= -0.25f
            && !field.IsPaneShattered(paneIndex)
            && (_player.CollisionMask & BreakableGlassField.MovementCollisionLayer) != 0;
        // The helper intentionally has no shooter-exclusion parameter. Move this
        // diagnostic actor out of its test ray before exercising the pane query.
        _player.GlobalPosition = start + Vector3.Right * 8.0f;
        _player.Velocity = Vector3.Zero;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var rayFrom = portalCenter - normal * 1.2f;
        var rayTo = portalCenter + normal * 1.2f;
        var shot = BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            rayFrom,
            rayTo,
            100.0f,
            normal,
            out _,
            spawnEffects: false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var walk = await BazaarWalkPlayer(start, target, ascending: false);
        return (blocked && shot && walk.Ready, blocked, shot, walk);
    }

    private async Task<(bool Ready, bool Shattered, BazaarActorLegResult Walk)>
        BazaarSquadGlassTraversalReady(
            BreakableGlassField field,
            int paneIndex,
            Vector3 start,
            Vector3 target)
    {
        const float delta = 1.0f / 60.0f;
        field.ResetAllPanes();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = start + Vector3.Right * 8.0f;
        var mate = new SquadMate { Name = "BazaarPortalGlassSquadProbe" };
        mate.Configure(this, _player, 1, OperatorRole.Assault, "GLASS-PROBE", false, 0);
        AddChild(mate);
        mate.SetPhysicsProcess(false);
        mate.SetProcess(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        ClearDemolitionSquadRoute(mate);
        ClearDemolitionSquadRouteFallback(mate);
        mate.GlobalPosition = start;
        mate.Velocity = Vector3.Zero;
        mate.ResetCombatTacticsForDiagnostics();
        await BazaarSettleSquadMate(mate, delta);
        var stepsBefore = mate.BazaarRoutePhysicsStepsForDiagnostics;
        var previous = mate.GlobalPosition;
        var longestStall = 0;
        var stall = 0;
        var reached = false;
        var frames = 0;
        for (; frames < 180; frames++)
        {
            mate.StepBazaarRoutePhysicsForDiagnostics(
                SquadNavigationDirective.Walk(target, preciseTrail: true),
                delta);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            BazaarTrackActorStall(
                mate.GlobalPosition,
                previous,
                target,
                ref stall,
                ref longestStall);
            previous = mate.GlobalPosition;
            if (BazaarActorReached(mate.GlobalPosition, target, ascending: false))
            {
                reached = true;
                break;
            }
        }
        var walk = new BazaarActorLegResult(
            reached,
            true,
            true,
            mate.BazaarRoutePhysicsStepsForDiagnostics > stepsBefore,
            frames,
            longestStall,
            mate.GlobalPosition.Y - start.Y);
        var shattered = field.IsPaneShattered(paneIndex);
        var maskReady = mate.CollisionMask
            == (1u | BreakableGlassField.MovementCollisionLayer);
        ClearDemolitionSquadRoute(mate);
        ClearDemolitionSquadRouteFallback(mate);
        mate.QueueFree();
        await WaitFrames(3);
        return (walk.Ready && shattered && maskReady, shattered, walk);
    }

    private async Task<(bool Ready, bool Shattered, BazaarActorLegResult Walk)>
        BazaarEnemyGlassTraversalReady(
            BreakableGlassField field,
            int paneIndex,
            Vector3 start,
            Vector3 target)
    {
        const float delta = 1.0f / 60.0f;
        field.ResetAllPanes();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var enemy = new EnemyOperator
        {
            Name = "BazaarPortalGlassEnemyProbe",
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            NetworkId = int.MaxValue - 8,
            SimulationSeed = 4706
        };
        AddChild(enemy);
        enemy.SetPhysicsProcess(false);
        enemy.SetProcess(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _demolitionOpponentRoutes.Remove(enemy);
        enemy.GlobalPosition = start;
        enemy.Velocity = Vector3.Zero;
        await BazaarSettleEnemy(enemy, delta);
        var walk = await BazaarWalkEnemyLeg(
            enemy,
            target,
            ascending: false,
            "bazaar-enemy-portal-glass",
            delta);
        var shattered = field.IsPaneShattered(paneIndex);
        var maskReady = enemy.CollisionMask
            == (1u | BreakableGlassField.MovementCollisionLayer);
        _demolitionOpponentRoutes.Remove(enemy);
        enemy.QueueFree();
        await WaitFrames(3);
        return (walk.Ready && shattered && maskReady, shattered, walk);
    }

    private static bool BazaarGlassCapsuleRouteClear(
        World3D world,
        IReadOnlyList<Vector3> points,
        uint collisionMask,
        out string blocker)
    {
        blocker = "none";
        const float capsuleHeight = 1.75f;
        using var shape = new CapsuleShape3D { Radius = 0.38f, Height = capsuleHeight };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            CollisionMask = collisionMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f
        };
        for (var segment = 0; segment < points.Count - 1; segment++)
        {
            var from = points[segment];
            var to = points[segment + 1];
            var samples = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 0.45f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var feet = from.Lerp(to, sample / (float)samples);
                query.Transform = new Transform3D(
                    Basis.Identity,
                    feet + Vector3.Up * (capsuleHeight * 0.5f + 0.04f));
                var hits = world.DirectSpaceState.IntersectShape(query, 8);
                using var hitsBacking = hits.AsDisposable();
                if (hits.Count == 0)
                {
                    continue;
                }
                using var hit = hits[0];
                using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                blocker = colliderValue.AsGodotObject() is Node collider
                    ? collider.Name.ToString()
                    : "unknown";
                return false;
            }
        }
        return true;
    }
}
