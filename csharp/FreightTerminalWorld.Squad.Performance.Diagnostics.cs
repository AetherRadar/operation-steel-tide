using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const ulong SquadPerformanceMaximumPlanMicroseconds = 75_000;
    private const ulong SquadPerformanceMaximumTotalMicroseconds = 110_000;
    private const ulong SquadPerformanceMaximumReuseMicroseconds = 12_000;
    private const ulong SquadPerformanceMaximumReuseTotalMicroseconds = 60_000;
    private const ulong SquadPerformanceMaximumQueryProbeMicroseconds = 750_000;
    private const ulong SquadPerformanceMaximumFinalizerMicroseconds = 350_000;
    private const ulong SquadPerformanceMaximumConnectorWarmPassMicroseconds = 25_000;
    private const ulong SquadPerformanceMaximumProximityFrameMicroseconds = 300_000;
    private const ulong SquadPerformanceMaximumRepeatedProximityFrameMicroseconds = 75_000;
    private const ulong SquadPerformanceMaximumProximityAverageMicroseconds = 25_000;
    private const ulong SquadPerformanceMaximumProximityFinalizerMicroseconds = 350_000;
    private const int SquadPerformanceReuseSamples = 12;
    private const int SquadPerformanceQueryProbeSamples = 1536;
    private const int SquadPerformanceProximityFrames = 720;

    private async void ValidateSquadPerformance()
    {
        var mates = _squadMates
            .Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy && !mate.IsDowned)
            .Take(2)
            .ToArray();
        _player.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var mate in mates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }

        await WaitFrames(3);
        if (mates.Length < 2)
        {
            GD.Print("SQUAD_PERFORMANCE_CHECK valid=False reason=missing_ai_mates");
            GD.Print("SQUAD_PERFORMANCE_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        DrainPendingFinalizersForDiagnostics();
        var queryProbeFrom = mates[0].GlobalPosition + Vector3.Up * 1.1f;
        var queryProbeStarted = Time.GetTicksUsec();
        for (var sample = 0; sample < SquadPerformanceQueryProbeSamples; sample++)
        {
            var direction = sample % 2 == 0 ? Vector3.Down * 3.0f : Vector3.Forward * 0.75f;
            _ = PhysicsRaycast.HasHit(
                GetWorld3D(),
                queryProbeFrom,
                queryProbeFrom + direction,
                mates[0].GetRid(),
                1);
        }
        var queryProbeMicroseconds = Time.GetTicksUsec() - queryProbeStarted;
        var finalizerStarted = Time.GetTicksUsec();
        DrainPendingFinalizersForDiagnostics();
        var finalizerMicroseconds = Time.GetTicksUsec() - finalizerStarted;

        var fixture = BuildSquadRescueMazeForDiagnostics(
            out var destination,
            out var start,
            out _);
        await WaitFrames(3);
        SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());
        _squadNavNextNormalPlanMilliseconds = 0;

        ulong totalMicroseconds = 0;
        ulong maximumMicroseconds = 0;
        var deferredPlans = 0;
        var plannedRoutes = 0;
        SquadMate? plannedMate = null;
        SquadGridPathState? plannedState = null;
        for (var index = 0; index < mates.Length; index++)
        {
            var mate = mates[index];
            mate.GlobalPosition = start + new Vector3(index * 0.18f, 0.0f, 0.0f);
            mate.Velocity = Vector3.Zero;
            ClearSquadNavigation(mate);

            var started = Time.GetTicksUsec();
            _ = ResolveSquadNavigationDestination(mate, destination, emergency: false);
            var elapsed = Time.GetTicksUsec() - started;
            totalMicroseconds += elapsed;
            maximumMicroseconds = Math.Max(maximumMicroseconds, elapsed);

            if (_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var state))
            {
                if (state.Directives.Length > 0)
                {
                    plannedRoutes++;
                    plannedMate ??= mate;
                    plannedState ??= state;
                }
                else if (state.NextPlanMilliseconds > Time.GetTicksMsec())
                {
                    deferredPlans++;
                }
            }
        }

        ulong reuseTotalMicroseconds = 0;
        ulong reuseMaximumMicroseconds = 0;
        var reuseSamples = 0;
        if (plannedMate is not null && plannedState is not null)
        {
            _player.GlobalPosition = plannedMate.GlobalPosition + new Vector3(0.8f, 0.0f, 0.8f);
            var id = plannedMate.GetInstanceId();
            for (var sample = 0; sample < SquadPerformanceReuseSamples; sample++)
            {
                plannedState.Cursor = 0;
                plannedState.NextShortcutCheckMilliseconds = 0;
                var started = Time.GetTicksUsec();
                _ = ResolveSquadNavigationDestination(plannedMate, destination, emergency: false);
                var elapsed = Time.GetTicksUsec() - started;
                reuseTotalMicroseconds += elapsed;
                reuseMaximumMicroseconds = Math.Max(reuseMaximumMicroseconds, elapsed);
                reuseSamples++;
                _squadGridPaths[id] = plannedState;
            }
        }

        var proximityAnchor = start + new Vector3(0.0f, 0.0f, 2.4f);
        _player.GlobalPosition = proximityAnchor;
        _player.Velocity = Vector3.Zero;
        _player.Rotation = Vector3.Zero;
        SetSquadLeaderTrailForDiagnostics(new[] { proximityAnchor });
        IssueSquadOrder(SquadOrder.Follow);
        for (var index = 0; index < mates.Length; index++)
        {
            var mate = mates[index];
            mate.GlobalPosition = proximityAnchor + new Vector3(index * 0.32f - 0.16f, 0.0f, -0.55f);
            mate.Velocity = Vector3.Zero;
            mate.GrantFireablePrimaryForDiagnostics();
            mate.ResetCombatTacticsForDiagnostics();
            ClearSquadNavigation(mate);
            mate.ProcessMode = ProcessModeEnum.Inherit;
        }

        mates[1].GlobalPosition = proximityAnchor + Vector3.Right * 4.0f;
        mates[0].GlobalPosition = proximityAnchor + Vector3.Back * 1.2f;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var leaderCollisionExcluded = mates.All(mate => mate.LeaderCollisionExcludedForDiagnostics);
        var leaderProbeClear = mates[0].MeasureMovementClearanceForDiagnostics(
            Vector3.Forward,
            2.0f) >= 1.95f;
        var leaderMotionClear = !mates[0].WouldNavigationMotionCollideForDiagnostics(
            Vector3.Forward * 2.0f);
        for (var index = 0; index < mates.Length; index++)
        {
            var mate = mates[index];
            mate.GlobalPosition = proximityAnchor + new Vector3(index * 0.32f - 0.16f, 0.0f, -0.55f);
            mate.Velocity = Vector3.Zero;
            mate.ResetCombatTacticsForDiagnostics();
            ClearSquadNavigation(mate);
        }

        DrainPendingFinalizersForDiagnostics();
        ulong proximityTotalMicroseconds = 0;
        ulong proximityMaximumMicroseconds = 0;
        ulong proximitySecondMaximumMicroseconds = 0;
        var proximityMaximumFrame = -1;
        var proximityFramesOverBudget = 0;
        for (var frame = 0; frame < SquadPerformanceProximityFrames; frame++)
        {
            _player.GlobalPosition = proximityAnchor + new Vector3(
                Mathf.Sin(frame * 0.11f) * 1.6f,
                0.0f,
                Mathf.Cos(frame * 0.07f) * 0.35f);
            var frameStarted = Time.GetTicksUsec();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var frameMicroseconds = Time.GetTicksUsec() - frameStarted;
            proximityTotalMicroseconds += frameMicroseconds;
            if (frameMicroseconds > proximityMaximumMicroseconds)
            {
                proximitySecondMaximumMicroseconds = proximityMaximumMicroseconds;
                proximityMaximumMicroseconds = frameMicroseconds;
                proximityMaximumFrame = frame;
            }
            else if (frameMicroseconds > proximitySecondMaximumMicroseconds)
            {
                proximitySecondMaximumMicroseconds = frameMicroseconds;
            }
            if (frameMicroseconds > SquadPerformanceMaximumRepeatedProximityFrameMicroseconds)
            {
                proximityFramesOverBudget++;
            }
        }
        foreach (var mate in mates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        var proximityFinalizerStarted = Time.GetTicksUsec();
        DrainPendingFinalizersForDiagnostics();
        var proximityFinalizerMicroseconds = Time.GetTicksUsec() - proximityFinalizerStarted;
        var proximityAverageMicroseconds = proximityTotalMicroseconds
            / (ulong)SquadPerformanceProximityFrames;

        var valid = plannedRoutes >= 1
            && deferredPlans >= 1
            && reuseSamples == SquadPerformanceReuseSamples
            && _squadPortalWalkCorridorCacheReady
            && maximumMicroseconds <= SquadPerformanceMaximumPlanMicroseconds
            && totalMicroseconds <= SquadPerformanceMaximumTotalMicroseconds
            && reuseMaximumMicroseconds <= SquadPerformanceMaximumReuseMicroseconds
            && reuseTotalMicroseconds <= SquadPerformanceMaximumReuseTotalMicroseconds
            && queryProbeMicroseconds <= SquadPerformanceMaximumQueryProbeMicroseconds
            && finalizerMicroseconds <= SquadPerformanceMaximumFinalizerMicroseconds
            && SquadPortalWalkWarmPassesForDiagnostics >= 1
            && SquadPortalWalkWarmMaximumMicrosecondsForDiagnostics
                <= SquadPerformanceMaximumConnectorWarmPassMicroseconds
            && leaderCollisionExcluded
            && leaderProbeClear
            && leaderMotionClear
            && proximityMaximumMicroseconds <= SquadPerformanceMaximumProximityFrameMicroseconds
            && proximitySecondMaximumMicroseconds
                <= SquadPerformanceMaximumRepeatedProximityFrameMicroseconds
            && proximityFramesOverBudget <= 1
            && proximityAverageMicroseconds <= SquadPerformanceMaximumProximityAverageMicroseconds
            && proximityFinalizerMicroseconds <= SquadPerformanceMaximumProximityFinalizerMicroseconds;
        GD.Print(
            $"SQUAD_PERFORMANCE_CHECK valid={valid} mates={mates.Length} planned={plannedRoutes} deferred={deferredPlans} "
            + $"connector_cache={_squadPortalWalkCorridorCacheReady} "
            + $"max_usec={maximumMicroseconds} max_budget={SquadPerformanceMaximumPlanMicroseconds} "
            + $"total_usec={totalMicroseconds} total_budget={SquadPerformanceMaximumTotalMicroseconds} "
            + $"reuse_samples={reuseSamples} reuse_max_usec={reuseMaximumMicroseconds} "
            + $"reuse_max_budget={SquadPerformanceMaximumReuseMicroseconds} "
            + $"reuse_total_usec={reuseTotalMicroseconds} "
            + $"reuse_total_budget={SquadPerformanceMaximumReuseTotalMicroseconds} "
            + $"query_samples={SquadPerformanceQueryProbeSamples} "
            + $"query_usec={queryProbeMicroseconds} "
            + $"query_budget={SquadPerformanceMaximumQueryProbeMicroseconds} "
            + $"finalizer_usec={finalizerMicroseconds} "
            + $"finalizer_budget={SquadPerformanceMaximumFinalizerMicroseconds} "
            + $"connector_warm_passes={SquadPortalWalkWarmPassesForDiagnostics} "
            + $"connector_warm_max_usec={SquadPortalWalkWarmMaximumMicrosecondsForDiagnostics} "
            + $"connector_warm_budget={SquadPerformanceMaximumConnectorWarmPassMicroseconds} "
            + $"leader_collision_excluded={leaderCollisionExcluded} "
            + $"leader_probe_clear={leaderProbeClear} "
            + $"leader_motion_clear={leaderMotionClear} "
            + $"proximity_frames={SquadPerformanceProximityFrames} "
            + $"proximity_max_usec={proximityMaximumMicroseconds} "
            + $"proximity_max_frame={proximityMaximumFrame} "
            + $"proximity_max_budget={SquadPerformanceMaximumProximityFrameMicroseconds} "
            + $"proximity_second_max_usec={proximitySecondMaximumMicroseconds} "
            + $"proximity_repeat_budget={SquadPerformanceMaximumRepeatedProximityFrameMicroseconds} "
            + $"proximity_avg_usec={proximityAverageMicroseconds} "
            + $"proximity_avg_budget={SquadPerformanceMaximumProximityAverageMicroseconds} "
            + $"proximity_over_budget={proximityFramesOverBudget} "
            + $"proximity_finalizer_usec={proximityFinalizerMicroseconds} "
            + $"proximity_finalizer_budget={SquadPerformanceMaximumProximityFinalizerMicroseconds}");
        GD.Print($"SQUAD_PERFORMANCE_PASS valid={valid}");
        fixture.QueueFree();
        GetTree().Quit(valid ? 0 : 2);
    }

    private static void DrainPendingFinalizersForDiagnostics()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}
