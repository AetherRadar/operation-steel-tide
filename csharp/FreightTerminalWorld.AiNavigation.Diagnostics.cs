using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateAiNavigation()
    {
        Node3D? fixture = null;
        EnemyOperator? pursuer = null;
        var friendlyDoor = false;
        var friendlyStairs = false;
        var friendlyArrived = false;
        var enemyDoor = false;
        var enemyStairs = false;
        var enemyArrived = false;
        var enemyTailSearch = false;
        var sentryHeld = false;
        var enemyAttachments = 0;
        var enemyAdvances = 0;
        var enemySteps = 0;
        var enemyRecoveries = 0;
        var productionRoute = false;
        var productionStep = false;
        var productionCapabilities = false;
        var residentialLinks = false;
        var trailAllocatedBytes = long.MaxValue;
        var plannerAllocatedBytes = long.MaxValue;
        var boundedPlans = false;
        var failure = string.Empty;

        try
        {
            await WaitFrames(8);
            _missionDirector.ExitDeploymentZone();
            var mate = _squadMates.FirstOrDefault(candidate =>
                IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
            pursuer = _enemies.FirstOrDefault(candidate =>
                IsInstanceValid(candidate) && !candidate.IsDead && candidate.IsRivalSquad);
            if (mate is null || pursuer is null || _residentialTowers.Count == 0)
            {
                throw new InvalidOperationException("missing navigation actors or residential tower");
            }

            ParkAiNavigationActors(mate, pursuer);
            fixture = BuildAiNavigationFixture(out var route, out var doorPlaneZ, out var upperFloorY);
            var start = route[0];
            var goal = route[^1];

            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = goal;
            _player.Velocity = Vector3.Zero;
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.IsDead = false;
            _player.SetCombatMovementTrailForDiagnostics(route);
            SetSquadLeaderTrailForDiagnostics(route);

            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = start;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetOrder(SquadOrder.Follow, start);
            await WaitFrames(3);
            mate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 900 && !friendlyArrived; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                friendlyDoor |= mate.GlobalPosition.Z < doorPlaneZ - 0.8f;
                friendlyStairs |= mate.GlobalPosition.Y >= upperFloorY - 0.38f;
                friendlyArrived = friendlyDoor
                    && friendlyStairs
                    && mate.GlobalPosition.DistanceTo(goal) <= 2.0f;
            }
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = start + new Vector3(-18.0f, 0.0f, 0.0f);

            pursuer.ProcessMode = ProcessModeEnum.Disabled;
            pursuer.ResetTacticalStateForDiagnostics();
            pursuer.ApplyColdStartUnarmed();
            pursuer.SentryMode = false;
            pursuer.GlobalPosition = start;
            pursuer.Velocity = Vector3.Zero;
            ResetOperatorPursuitPlanCountsForDiagnostics();
            await WaitFrames(3);
            pursuer.TakeDamage(0.1f, pursuer.GlobalPosition + Vector3.Up, _player);
            pursuer.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 900 && !enemyArrived; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                enemyDoor |= pursuer.GlobalPosition.Z < doorPlaneZ - 0.8f;
                enemyStairs |= pursuer.GlobalPosition.Y >= upperFloorY - 0.38f;
                enemyArrived = enemyDoor
                    && enemyStairs
                    && pursuer.GlobalPosition.DistanceTo(goal) <= 2.2f;
            }
            if (enemyArrived)
            {
                // Put the pursuer exactly on the confirmed trail tail, then move
                // the hidden target without recording a new sample. The next
                // pursuit tick must leave the exhausted trail and perform a
                // bounded local search around the last confirmed position.
                pursuer.GlobalPosition = goal;
                pursuer.Velocity = Vector3.Zero;
                pursuer.TakeDamage(0.1f, pursuer.GlobalPosition + Vector3.Up, _player);
                _player.GlobalPosition = goal + new Vector3(0.0f, 0.0f, 70.0f);
                var searchOrigin = pursuer.GlobalPosition;
                for (var frame = 0; frame < 150; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    var searchOffset = pursuer.GlobalPosition - searchOrigin;
                    searchOffset.Y = 0.0f;
                    enemyTailSearch |= searchOffset.LengthSquared() >= 0.8f * 0.8f;
                }
            }
            enemyAttachments = pursuer.PursuitTrailAttachmentsForDiagnostics;
            enemyAdvances = pursuer.PursuitTrailWaypointAdvancesForDiagnostics;
            enemySteps = pursuer.PursuitNavigationStepUpsForDiagnostics;
            enemyRecoveries = pursuer.PursuitRouteRecoveriesForDiagnostics;
            pursuer.ProcessMode = ProcessModeEnum.Disabled;

            var sentryStart = fixture.ToGlobal(new Vector3(-3.5f, 0.12f, 5.1f));
            var sentryTarget = sentryStart + new Vector3(5.0f, 1.2f, 0.0f);
            pursuer.ResetTacticalStateForDiagnostics();
            pursuer.ApplyColdStartUnarmed();
            pursuer.SentryMode = true;
            pursuer.GlobalPosition = sentryStart;
            pursuer.Velocity = Vector3.Zero;
            _player.GlobalPosition = sentryTarget;
            _player.SetCombatMovementTrailForDiagnostics(new[] { sentryTarget });
            pursuer.LookAt(sentryTarget, Vector3.Up);
            pursuer.TakeDamage(0.1f, pursuer.GlobalPosition + Vector3.Up, _player);
            pursuer.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 120; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            var sentryOffset = pursuer.GlobalPosition - sentryStart;
            sentryOffset.Y = 0.0f;
            sentryHeld = sentryOffset.LengthSquared() <= 0.2f * 0.2f;
            pursuer.ProcessMode = ProcessModeEnum.Disabled;
            pursuer.SentryMode = false;

            var tower = _residentialTowers[0];
            var spec = ResidentialTowerSpecs[0];
            var coreZ = -Mathf.Min(spec.Footprint.Y * 0.18f, 3.6f);
            var residentialRoute = BuildResidentialStairNavigationRoute(
                tower,
                floorY: 0.0f,
                coreZ,
                laneOffset: 0.0f,
                descending: false);
            // The fixture above exercises the real door and pursuit path. Probe
            // the production planner from the authored stair-link endpoint here
            // so this assertion isolates cross-floor traversal registration.
            pursuer.GlobalPosition = residentialRoute[0];
            pursuer.Velocity = Vector3.Zero;
            SquadNavigationDirective[] planned;
            ResetOperatorPursuitPlanCountsForDiagnostics();
            var plannerAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
            productionRoute = TryPlanOperatorPursuitRoute(
                pursuer,
                residentialRoute[^1],
                out planned);
            plannerAllocatedBytes = GC.GetAllocatedBytesForCurrentThread()
                - plannerAllocationBefore;
            productionStep = planned.Any(static directive =>
                directive.Required && directive.Kind == SquadTraversalKind.Step);
            productionCapabilities = planned.All(static directive =>
                directive.Kind is not (SquadTraversalKind.Vault or SquadTraversalKind.Drop));

            // Add a cheaper unsupported shortcut after the production pass. The
            // capability-aware graph must ignore it and still return the authored
            // walk/step route rather than rejecting the whole shortest path.
            var diagnosticVaultId = RegisterSquadTraversalLink(
                "diagnostic:operator_unsupported_vault",
                SquadTraversalKind.Vault,
                bidirectional: false,
                new[] { residentialRoute[0], residentialRoute[^1] },
                costMultiplier: 0.1f);
            ResetSquadPortalWalkConnectorCache();
            var capabilityRoute = TryPlanSquadLayeredRoute(
                pursuer,
                residentialRoute[^1],
                new SquadNavSearchBudget(900, 5.0),
                SquadTraversalCapabilities.Walk | SquadTraversalCapabilities.Step,
                out var capabilityPlan,
                out _);
            productionCapabilities &= capabilityRoute
                && capabilityPlan.Length > 0
                && capabilityPlan.All(static directive =>
                    directive.Kind is not (SquadTraversalKind.Vault or SquadTraversalKind.Drop));
            if (diagnosticVaultId == _squadTraversalLinks.Count - 1)
            {
                _squadTraversalLinks.RemoveAt(diagnosticVaultId);
            }
            ResetSquadPortalWalkConnectorCache();
            var expectedResidentialLinks = ResidentialTowerSpecs.Sum(candidate => candidate.Floors);
            residentialLinks = _squadTraversalLinks.Count(link =>
                link.Source.StartsWith("residential_stair:", StringComparison.Ordinal))
                == expectedResidentialLinks;

            var allocationTrail = new CombatMovementTrail();
            for (var index = 0; index < 128; index++)
            {
                allocationTrail.Record(new Vector3(index, 0.0f, index * 0.25f));
            }
            var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 128; index < 4096; index++)
            {
                allocationTrail.Record(new Vector3(index, 0.0f, index * 0.25f));
            }
            trailAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;

            var planCounts = OperatorPursuitPlanCountsForDiagnostics;
            boundedPlans = planCounts.Attempts is >= 1 and <= 12
                && planCounts.Successes >= 1
                && pursuer.PursuitStaticPlansForDiagnostics <= 4
                && plannerAllocatedBytes <= 256_000;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"AI_NAVIGATION_EXCEPTION {failure}");
        }

        var valid = friendlyDoor && friendlyStairs && friendlyArrived
            && enemyDoor && enemyStairs && enemyArrived
            && enemyTailSearch && sentryHeld
            && productionRoute && productionStep && productionCapabilities && residentialLinks
            && trailAllocatedBytes == 0
            && boundedPlans
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"AI_NAVIGATION_CHECK valid={valid} friendly_door={friendlyDoor} "
            + $"friendly_stairs={friendlyStairs} friendly_arrived={friendlyArrived} "
            + $"enemy_door={enemyDoor} enemy_stairs={enemyStairs} enemy_arrived={enemyArrived} "
            + $"enemy_tail_search={enemyTailSearch} sentry_held={sentryHeld} "
            + $"enemy_attach={enemyAttachments} enemy_advances={enemyAdvances} "
            + $"enemy_steps={enemySteps} enemy_recoveries={enemyRecoveries} "
            + $"production_route={productionRoute} production_step={productionStep} "
            + $"production_capabilities={productionCapabilities} "
            + $"residential_links={residentialLinks} trail_allocated={trailAllocatedBytes} "
            + $"planner_allocated={plannerAllocatedBytes} "
            + $"bounded_plans={boundedPlans} failure={failure}");
        GD.Print($"AI_NAVIGATION_PASS valid={valid}");
        fixture?.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private void ParkAiNavigationActors(SquadMate activeMate, EnemyOperator activeEnemy)
    {
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || mate == activeMate)
            {
                continue;
            }
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = new Vector3(410.0f + mate.SquadSlot * 3.0f, 0.3f, 410.0f);
        }
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy == activeEnemy)
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.GlobalPosition = new Vector3(430.0f, 0.3f, 430.0f);
        }
    }

    private Node3D BuildAiNavigationFixture(
        out List<Vector3> route,
        out float doorPlaneZ,
        out float upperFloorY)
    {
        var root = new Node3D
        {
            Name = "AiNavigationDiagnostic",
            Position = new Vector3(230.0f, 80.0f, 230.0f)
        };
        AddChild(root);

        AddAiNavigationBox(root, "LowerFloor", new Vector3(0.0f, -0.15f, 1.0f), new Vector3(10.0f, 0.3f, 12.0f));
        doorPlaneZ = root.ToGlobal(new Vector3(0.0f, 0.0f, 2.6f)).Z;
        AddAiNavigationBox(root, "DoorWallLeft", new Vector3(-2.9f, 1.6f, 2.6f), new Vector3(4.2f, 3.2f, 0.35f));
        AddAiNavigationBox(root, "DoorWallRight", new Vector3(2.9f, 1.6f, 2.6f), new Vector3(4.2f, 3.2f, 0.35f));
        AddAiNavigationBox(root, "DoorHeader", new Vector3(0.0f, 2.85f, 2.6f), new Vector3(1.7f, 0.7f, 0.35f));

        const int stairSteps = 12;
        const float stepRise = 0.15f;
        const float stepRun = 0.46f;
        const float stairStartZ = 0.4f;
        route = new List<Vector3>
        {
            root.ToGlobal(new Vector3(0.0f, 0.12f, 5.4f)),
            root.ToGlobal(new Vector3(0.0f, 0.12f, 3.45f)),
            root.ToGlobal(new Vector3(0.0f, 0.12f, 2.0f)),
            root.ToGlobal(new Vector3(0.0f, 0.12f, 0.8f))
        };
        for (var step = 0; step < stairSteps; step++)
        {
            var top = stepRise * (step + 1);
            var z = stairStartZ - stepRun * (step + 0.5f);
            AddAiNavigationBox(
                root,
                $"StairStep_{step:00}",
                new Vector3(0.0f, top - 0.07f, z),
                new Vector3(1.8f, 0.14f, stepRun * 1.08f));
            route.Add(root.ToGlobal(new Vector3(0.0f, top + 0.075f, z)));
        }
        upperFloorY = root.GlobalPosition.Y + stairSteps * stepRise;
        AddAiNavigationBox(root, "UpperFloor", new Vector3(0.0f, stairSteps * stepRise - 0.15f, -7.2f), new Vector3(8.0f, 0.3f, 5.0f));
        route.Add(root.ToGlobal(new Vector3(0.0f, stairSteps * stepRise + 0.12f, -6.4f)));
        return root;
    }

    private static void AddAiNavigationBox(
        Node3D parent,
        string name,
        Vector3 position,
        Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D
        {
            Name = name + "Shape",
            Shape = new BoxShape3D { Size = size }
        });
        parent.AddChild(body);
    }
}
