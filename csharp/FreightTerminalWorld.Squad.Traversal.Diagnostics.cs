using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSquadTraversal()
    {
        var sameXzBlocked = false;
        var sameXzRouted = false;
        var stairAssigned = false;
        var stairEntry = false;
        var stairTop = false;
        var stairAccess = false;
        var stairRescued = false;
        var stairMaximumGain = 0.0f;
        var stairFinal = Vector3.Zero;
        var stairRouteState = "none";
        var stairStepUps = 0;
        var stairRouteSeen = false;
        var stairStepDirectiveSeen = false;
        var stairRouteFrame = -1;
        var stairReplans = 0;
        var stairRecoveries = 0;
        var stairRecoveryInjected = false;
        var stairRecoveryPreserved = false;
        var stairFollowStepSeen = false;
        var stairFollowPlanReady = false;
        var stairFollowPlanState = "none";
        var stairFollowDirectiveSeen = false;
        var stairFollowFinalState = "none";
        var stairFollowCanProcess = false;
        var stairRole = "none";
        var stairEmergencyStepPreserved = false;
        var stairLifecycleCleared = false;
        SquadGridPathState? stairFollowRouteState = null;
        var stairFollowRouteSnapshot = Array.Empty<SquadNavigationDirective>();
        var blockedStepRetryPreserved = false;
        var blockedStepFailover = false;
        var productionVaultLink = false;
        var productionDropLink = false;
        var productionComponentRoute = false;
        var productionComponentStep = false;
        var productionComponentTarget = false;
        var vaultBlocked = false;
        var vaultSeen = false;
        var vaultDone = false;
        var dropSeen = false;
        var dropDone = false;
        var unsafeDropRejected = false;
        var overheadCorridorRejected = false;
        var failure = string.Empty;
        Node3D? traversalFixture = null;

        try
        {
            await WaitSquadTraversalPhysicsFrames(4);
            for (var frame = 0;
                 frame < 600 && !_squadPortalWalkCorridorCacheReady;
                 frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            _missionDirector.ExitDeploymentZone();
            productionVaultLink = _squadTraversalLinks.Any(link =>
                link.Kind == SquadTraversalKind.Vault
                && link.Source.StartsWith("hesco:", StringComparison.Ordinal));
            productionDropLink = _squadTraversalLinks.Any(link =>
                link.Kind == SquadTraversalKind.Drop
                && link.Source.StartsWith("hesco:", StringComparison.Ordinal));
            var mate = _squadMates.FirstOrDefault(candidate =>
                    IsInstanceValid(candidate)
                    && !candidate.IsHumanProxy
                    && !candidate.IsDowned
                    && candidate.Role != OperatorRole.Medic)
                ?? _squadMates.FirstOrDefault(candidate =>
                    IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
            if (mate is null || _districtRouteHubs.Count == 0)
            {
                throw new InvalidOperationException("missing squad mate or district route network");
            }
            stairRole = mate.Role.ToString();

            foreach (var other in _squadMates.Where(candidate => IsInstanceValid(candidate) && candidate != mate))
            {
                other.ProcessMode = ProcessModeEnum.Disabled;
                other.GlobalPosition = new Vector3(310.0f + other.SquadSlot * 3.0f, 0.3f, 310.0f);
            }
            foreach (var enemy in _enemies.Where(IsInstanceValid))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.GlobalPosition = new Vector3(340.0f, 0.3f, 340.0f);
            }

            var opsGate = _districtRouteHubs.First(hub => hub.Id == "OpsGate");
            var deckTarget = opsGate.DeckCenter
                + Vector3.Up * (DistrictRouteDeckThickness * 0.5f + 0.05f);
            var groundUnderDeck = new Vector3(opsGate.DeckCenter.X, 0.25f, opsGate.DeckCenter.Z);
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = deckTarget;
            _player.Velocity = Vector3.Zero;
            mate.ProcessMode = ProcessModeEnum.Disabled;

            var crowdedHesco = _squadTraversalLinks
                .Where(link => link.Kind == SquadTraversalKind.Vault
                    && link.Source.StartsWith("hesco:", StringComparison.Ordinal))
                .OrderBy(link => link.ForwardPoints[0].DistanceSquaredTo(new Vector3(25.0f, 0.2f, -32.0f)))
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(crowdedHesco.Source))
            {
                var productionGoal = crowdedHesco.ForwardPoints[0];
                mate.GlobalPosition = deckTarget;
                mate.Velocity = Vector3.Zero;
                await WaitSquadTraversalPhysicsFrames(2);
                productionComponentRoute = TryPlanSquadLayeredRoute(
                    mate,
                    productionGoal,
                    SquadNavGrid.DefaultExpansionCap,
                    out var productionDirectives,
                    out _);
                productionComponentStep = productionDirectives.Any(directive =>
                    directive.Required && directive.Kind == SquadTraversalKind.Step);
                productionComponentTarget = productionDirectives.Length > 0
                    && productionDirectives[^1].Target.DistanceSquaredTo(productionGoal) <= 0.0625f;
            }

            mate.GlobalPosition = groundUnderDeck;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());
            ClearSquadNavigation(mate);
            await WaitSquadTraversalPhysicsFrames(3);

            sameXzBlocked = !IsSquadMovementCorridorClear(groundUnderDeck, deckTarget, mate);
            var firstDirective = ResolveSquadNavigationDestination(mate, deckTarget, emergency: true);
            sameXzRouted = firstDirective.Target.DistanceTo(deckTarget) > 1.0f
                && Mathf.Abs(firstDirective.Target.Y - groundUnderDeck.Y) < 1.2f;

            ClearLeaderReviveAi();
            ResetAiReviveAbandonment();
            SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());
            var stairStart = opsGate.StairStart
                - opsGate.StairDirection * 0.65f
                + Vector3.Up * 0.3f;
            mate.GlobalPosition = stairStart;
            mate.Velocity = Vector3.Zero;
            mate.RestoreHealth(mate.MaxHealth);
            mate.ResetCombatTacticsForDiagnostics();
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetOrder(SquadOrder.Follow, stairStart);
            _player.GlobalPosition = deckTarget;
            _player.Velocity = Vector3.Zero;
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.SetReviveUsedForDiagnostics(false);
            await WaitSquadTraversalPhysicsFrames(4);

            var climbStartY = mate.GlobalPosition.Y;
            var maximumY = climbStartY;
            var stairStepUpsBefore = mate.NavigationStepUpsForDiagnostics;
            var stairFollowEdge = -1;
            var primedFollowRoute = new SquadGridPathState();
            if (TryPlanSquadGridRoute(
                    mate,
                    deckTarget,
                    emergency: false,
                    out primedFollowRoute))
            {
                _squadTrailPaths.Remove(mate.GetInstanceId());
                _squadGridPaths[mate.GetInstanceId()] = primedFollowRoute;
                AdvanceSquadGridCursor(mate, primedFollowRoute);
            }
            if (primedFollowRoute.Cursor >= 0
                && primedFollowRoute.Cursor < primedFollowRoute.Directives.Length)
            {
                var primedDirective = primedFollowRoute.Directives[primedFollowRoute.Cursor];
                stairFollowPlanReady = true;
                stairFollowPlanState = $"{primedFollowRoute.Cursor}/{primedFollowRoute.Directives.Length}:"
                    + $"{primedDirective.Kind}:{primedDirective.DirectedEdgeId}:"
                    + $"({primedDirective.Target.X:0.0},{primedDirective.Target.Y:0.0},{primedDirective.Target.Z:0.0})";
            }
            mate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 720 && !stairFollowStepSeen; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                stairEntry |= new Vector2(
                    mate.GlobalPosition.X - opsGate.StairStart.X,
                    mate.GlobalPosition.Z - opsGate.StairStart.Z).Length() < 2.0f;
                maximumY = Mathf.Max(maximumY, mate.GlobalPosition.Y);
                stairMaximumGain = maximumY - climbStartY;
                stairFinal = mate.GlobalPosition;
                if (_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var followRoute)
                    && followRoute.Cursor >= 0
                    && followRoute.Cursor < followRoute.Directives.Length)
                {
                    var followDirective = followRoute.Directives[followRoute.Cursor];
                    stairFollowDirectiveSeen |= followDirective.Kind == SquadTraversalKind.Step;
                    stairFollowFinalState = $"{followRoute.Cursor}/{followRoute.Directives.Length}:"
                        + $"{followDirective.Kind}:{followDirective.DirectedEdgeId}:"
                        + $"({followDirective.Target.X:0.0},{followDirective.Target.Y:0.0},{followDirective.Target.Z:0.0}):"
                        + $"pos=({mate.GlobalPosition.X:0.0},{mate.GlobalPosition.Y:0.0},{mate.GlobalPosition.Z:0.0})";
                    if (followDirective.Required
                        && followDirective.Kind == SquadTraversalKind.Step
                        && followDirective.DirectedEdgeId >= 0
                        && stairMaximumGain >= 0.5f)
                    {
                        stairFollowStepSeen = true;
                        stairFollowEdge = followDirective.DirectedEdgeId;
                        stairFollowRouteState = followRoute;
                        stairFollowRouteSnapshot = followRoute.Directives
                            .Skip(followRoute.Cursor)
                            .ToArray();
                    }
                }
            }
            stairFollowCanProcess = mate.CanProcess() && mate.IsPhysicsProcessing();

            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }

            for (var frame = 0; frame < 1080 && _player.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                var assignedThisFrame = ReferenceEquals(_leaderReviver, mate) && mate.IsRevivingLeader;
                stairAssigned |= assignedThisFrame;
                stairEntry |= new Vector2(
                    mate.GlobalPosition.X - opsGate.StairStart.X,
                    mate.GlobalPosition.Z - opsGate.StairStart.Z).Length() < 2.0f;
                maximumY = Mathf.Max(maximumY, mate.GlobalPosition.Y);
                stairMaximumGain = maximumY - climbStartY;
                stairFinal = mate.GlobalPosition;
                stairTop |= maximumY - climbStartY > 4.45f;
                stairAccess |= mate.GlobalPosition.DistanceTo(deckTarget) <= 2.3f
                    && Mathf.Abs(mate.GlobalPosition.Y - deckTarget.Y) <= 1.25f;
                if (_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var activeRoute)
                    && activeRoute.Cursor >= 0
                    && activeRoute.Cursor < activeRoute.Directives.Length)
                {
                    var current = activeRoute.Directives[activeRoute.Cursor];
                    stairRouteSeen = true;
                    stairStepDirectiveSeen |= current.Kind == SquadTraversalKind.Step;
                    stairRouteFrame = frame;
                    stairRouteState = $"{activeRoute.Cursor}/{activeRoute.Directives.Length}:{current.Kind}:{current.DirectedEdgeId}:"
                        + $"({current.Target.X:0.0},{current.Target.Y:0.0},{current.Target.Z:0.0}):"
                        + $"d={mate.GlobalPosition.DistanceTo(current.Target):0.00}:floor={mate.IsOnFloor()}";
                    if (assignedThisFrame && !stairEmergencyStepPreserved)
                    {
                        stairEmergencyStepPreserved = stairFollowRouteState is not null
                            && ReferenceEquals(stairFollowRouteState, activeRoute)
                            && activeRoute.Emergency
                            && current.Required
                            && current.Kind == SquadTraversalKind.Step
                            && current.DirectedEdgeId == stairFollowEdge
                            && stairFollowRouteSnapshot.Any(original =>
                                original.Required == current.Required
                                && original.Kind == current.Kind
                                && original.DirectedEdgeId == current.DirectedEdgeId
                                && original.Target.DistanceSquaredTo(current.Target) <= 0.0001f);
                    }
                    if (!stairRecoveryInjected
                        && stairEmergencyStepPreserved
                        && current.Kind == SquadTraversalKind.Step
                        && stairMaximumGain >= 2.5f)
                    {
                        var directiveBeforeRecovery = current;
                        var routeBeforeRecovery = activeRoute;
                        ReplanLeaderRescueNavigation(mate);
                        stairRecoveryInjected = true;
                        stairRecoveryPreserved = _squadGridPaths.TryGetValue(
                                mate.GetInstanceId(),
                                out var recoveredRoute)
                            && ReferenceEquals(routeBeforeRecovery, recoveredRoute)
                            && recoveredRoute.Cursor >= 0
                            && recoveredRoute.Cursor < recoveredRoute.Directives.Length
                            && recoveredRoute.Directives[recoveredRoute.Cursor].Required
                                == directiveBeforeRecovery.Required
                            && recoveredRoute.Directives[recoveredRoute.Cursor].Kind
                                == directiveBeforeRecovery.Kind
                            && recoveredRoute.Directives[recoveredRoute.Cursor].DirectedEdgeId
                                == directiveBeforeRecovery.DirectedEdgeId
                            && recoveredRoute.Directives[recoveredRoute.Cursor].Target.DistanceSquaredTo(
                                directiveBeforeRecovery.Target) <= 0.0001f;
                    }
                }
            }
            if (_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var finalRoute)
                && finalRoute.Cursor >= 0
                && finalRoute.Cursor < finalRoute.Directives.Length)
            {
                var current = finalRoute.Directives[finalRoute.Cursor];
                stairRouteState = $"{finalRoute.Cursor}/{finalRoute.Directives.Length}:{current.Kind}:{current.DirectedEdgeId}:"
                    + $"({current.Target.X:0.0},{current.Target.Y:0.0},{current.Target.Z:0.0})";
            }
            stairStepUps = mate.NavigationStepUpsForDiagnostics - stairStepUpsBefore;
            stairReplans = LeaderRescueReplansForDiagnostics;
            stairRecoveries = mate.CombatStuckRecoveries;
            stairRescued = !_player.IsDead && _player.ReviveUsed && !_localPlayerDowned;
            stairLifecycleCleared = stairRescued
                && _leaderReviver is null
                && _aiReviveTarget is null
                && !mate.IsRevivingLeader
                && !_squadGridPaths.ContainsKey(mate.GetInstanceId())
                && !_squadTrailPaths.ContainsKey(mate.GetInstanceId());
            if (_player.CanBeRevived)
            {
                _player.TryReceiveRevive(60.0f);
            }
            ClearLeaderReviveAi();

            const int blockedStepEdge = 900001;
            var blockedStepRoute = new SquadGridPathState
            {
                Emergency = true,
                Destination = mate.GlobalPosition,
                Directives = new[]
                {
                    new SquadNavigationDirective(
                        mate.GlobalPosition + Vector3.Forward,
                        SquadTraversalKind.Step,
                        blockedStepEdge,
                        true)
                }
            };
            _squadGridPaths[mate.GetInstanceId()] = blockedStepRoute;
            ReplanLeaderRescueNavigation(mate);
            blockedStepRetryPreserved = _squadGridPaths.TryGetValue(
                    mate.GetInstanceId(),
                    out var retriedBlockedStep)
                && ReferenceEquals(blockedStepRoute, retriedBlockedStep)
                && !IsSquadTraversalEdgeDisabled(mate, blockedStepEdge);
            ReplanLeaderRescueNavigation(mate);
            blockedStepFailover = !_squadGridPaths.ContainsKey(mate.GetInstanceId())
                && IsSquadTraversalEdgeDisabled(mate, blockedStepEdge);
            _squadTraversalFailures.Remove((mate.GetInstanceId(), blockedStepEdge));
            _squadTraversalRecoveryAttempts.Remove((mate.GetInstanceId(), blockedStepEdge));

            traversalFixture = BuildSquadTraversalFixture(
                out var vaultStart,
                out var vaultTarget,
                out var dropStart,
                out var dropTarget,
                out var unsafeDropStart,
                out var overheadStart,
                out var overheadEnd);
            _player.GlobalPosition = new Vector3(260.0f, 80.0f, 260.0f);
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = vaultStart;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            mate.SetOrder(SquadOrder.Move, vaultTarget);
            await WaitSquadTraversalPhysicsFrames(5);
            vaultBlocked = !IsSquadMovementCorridorClear(vaultStart, vaultTarget, mate);
            var traversalCount = mate.CompletedNavigationTraversalsForDiagnostics;
            mate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 360 && !vaultDone; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                vaultSeen |= mate.ActiveNavigationTraversalKindForDiagnostics == SquadTraversalKind.Vault;
                vaultDone = mate.CompletedNavigationTraversalsForDiagnostics > traversalCount
                    && mate.LastCompletedNavigationTraversalKindForDiagnostics == SquadTraversalKind.Vault
                    && mate.GlobalPosition.Z > vaultStart.Z + 0.55f;
            }

            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = dropStart;
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            mate.SetOrder(SquadOrder.Move, dropTarget);
            await WaitSquadTraversalPhysicsFrames(5);
            traversalCount = mate.CompletedNavigationTraversalsForDiagnostics;
            mate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 360 && !dropDone; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                dropSeen |= mate.ActiveNavigationTraversalKindForDiagnostics == SquadTraversalKind.Drop;
                dropDone = mate.CompletedNavigationTraversalsForDiagnostics > traversalCount
                    && mate.LastCompletedNavigationTraversalKindForDiagnostics == SquadTraversalKind.Drop
                    && mate.GlobalPosition.Y < dropStart.Y - 1.4f
                    && mate.GlobalPosition.Z > dropStart.Z + 0.55f;
            }

            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = unsafeDropStart;
            mate.Velocity = Vector3.Zero;
            await WaitSquadTraversalPhysicsFrames(5);
            unsafeDropRejected = !mate.CanPlanNavigationDropForDiagnostics(Vector3.Back);
            overheadCorridorRejected = !IsSquadMovementCorridorClear(overheadStart, overheadEnd, mate);
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"SQUAD_TRAVERSAL_EXCEPTION {failure}");
        }

        var valid = sameXzBlocked && sameXzRouted
            && stairAssigned && stairEntry && stairTop && stairAccess && stairRescued
            && stairFollowPlanReady && stairFollowStepSeen && stairEmergencyStepPreserved
            && stairRecoveryInjected && stairRecoveryPreserved && stairLifecycleCleared
            && blockedStepRetryPreserved && blockedStepFailover
            && productionVaultLink && productionDropLink
            && productionComponentRoute && productionComponentStep && productionComponentTarget
            && vaultBlocked && vaultSeen && vaultDone
            && dropSeen && dropDone && unsafeDropRejected && overheadCorridorRejected
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"SQUAD_TRAVERSAL_CHECK same_xz_blocked={sameXzBlocked} same_xz_routed={sameXzRouted} "
            + $"stair_assigned={stairAssigned} stair_entry={stairEntry} stair_top={stairTop} stair_access={stairAccess} stair_rescued={stairRescued} "
            + $"stair_gain={stairMaximumGain:0.00} stair_final=({stairFinal.X:0.0},{stairFinal.Y:0.0},{stairFinal.Z:0.0}) "
            + $"stair_route_seen={stairRouteSeen} stair_step_seen={stairStepDirectiveSeen} stair_route_frame={stairRouteFrame} "
            + $"stair_route={stairRouteState} stair_steps={stairStepUps} stair_replans={stairReplans} stair_recoveries={stairRecoveries} "
            + $"stair_follow_step={stairFollowStepSeen} stair_emergency_preserved={stairEmergencyStepPreserved} "
            + $"stair_follow_plan={stairFollowPlanReady}:{stairFollowPlanState} "
            + $"stair_follow_directive={stairFollowDirectiveSeen}:{stairFollowFinalState} "
            + $"stair_follow_process={stairFollowCanProcess} stair_role={stairRole} "
            + $"stair_recovery_injected={stairRecoveryInjected} stair_recovery_preserved={stairRecoveryPreserved} "
            + $"stair_lifecycle_cleared={stairLifecycleCleared} "
            + $"blocked_step_retry={blockedStepRetryPreserved} blocked_step_failover={blockedStepFailover} "
            + $"production_vault={productionVaultLink} production_drop={productionDropLink} "
            + $"production_component_route={productionComponentRoute} production_component_step={productionComponentStep} "
            + $"production_component_target={productionComponentTarget} "
            + $"vault_blocked={vaultBlocked} vault_seen={vaultSeen} vault_done={vaultDone} "
            + $"drop_seen={dropSeen} drop_done={dropDone} unsafe_drop_rejected={unsafeDropRejected} "
            + $"overhead_rejected={overheadCorridorRejected} failure={failure}");
        GD.Print($"SQUAD_TRAVERSAL_PASS valid={valid}");
        traversalFixture?.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private Node3D BuildSquadTraversalFixture(
        out Vector3 vaultStart,
        out Vector3 vaultTarget,
        out Vector3 dropStart,
        out Vector3 dropTarget,
        out Vector3 unsafeDropStart,
        out Vector3 overheadStart,
        out Vector3 overheadEnd)
    {
        var root = new Node3D
        {
            Name = "SquadTraversalDiagnostic",
            Position = new Vector3(145.0f, 80.0f, 145.0f)
        };
        AddChild(root);

        AddSquadTraversalBox(root, "VaultFloor", new Vector3(0.0f, -0.15f, 0.0f), new Vector3(6.0f, 0.3f, 8.0f));
        AddSquadTraversalBox(root, "VaultObstacle", new Vector3(0.0f, 0.525f, 0.0f), new Vector3(5.0f, 1.05f, 0.55f));
        vaultStart = root.ToGlobal(new Vector3(0.0f, 0.08f, -2.0f));
        vaultTarget = root.ToGlobal(new Vector3(0.0f, 0.08f, 2.0f));

        AddSquadTraversalBox(root, "DropLowerFloor", new Vector3(14.0f, -0.15f, 1.7f), new Vector3(6.0f, 0.3f, 7.0f));
        AddSquadTraversalBox(root, "DropPlatform", new Vector3(14.0f, 1.0f, -0.55f), new Vector3(3.2f, 2.0f, 2.1f));
        dropStart = root.ToGlobal(new Vector3(14.0f, 2.08f, -0.25f));
        dropTarget = root.ToGlobal(new Vector3(14.0f, 0.08f, 2.2f));

        AddSquadTraversalBox(root, "UnsafePlatform", new Vector3(28.0f, 1.0f, -0.55f), new Vector3(3.2f, 2.0f, 2.1f));
        unsafeDropStart = root.ToGlobal(new Vector3(28.0f, 2.08f, -0.25f));

        AddSquadTraversalBox(root, "LowCeilingFloor", new Vector3(42.0f, -0.15f, 0.0f), new Vector3(5.0f, 0.3f, 5.0f));
        AddSquadTraversalBox(root, "LowCeiling", new Vector3(42.0f, 1.18f, 0.0f), new Vector3(5.0f, 0.2f, 5.0f));
        overheadStart = root.ToGlobal(new Vector3(42.0f, 0.08f, -1.4f));
        overheadEnd = root.ToGlobal(new Vector3(42.0f, 0.08f, 1.4f));
        return root;
    }

    private static void AddSquadTraversalBox(
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
        parent.AddChild(body);
        body.AddChild(new CollisionShape3D
        {
            Name = name + "Shape",
            Shape = new BoxShape3D { Size = size }
        });
    }

    private async System.Threading.Tasks.Task WaitSquadTraversalPhysicsFrames(int count)
    {
        for (var frame = 0; frame < count; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }
}
