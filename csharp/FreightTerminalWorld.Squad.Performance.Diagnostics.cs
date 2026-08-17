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
    private const int SquadPerformanceReuseSamples = 12;

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

        var valid = plannedRoutes >= 1
            && deferredPlans >= 1
            && reuseSamples == SquadPerformanceReuseSamples
            && _squadPortalWalkCorridorCacheReady
            && maximumMicroseconds <= SquadPerformanceMaximumPlanMicroseconds
            && totalMicroseconds <= SquadPerformanceMaximumTotalMicroseconds
            && reuseMaximumMicroseconds <= SquadPerformanceMaximumReuseMicroseconds
            && reuseTotalMicroseconds <= SquadPerformanceMaximumReuseTotalMicroseconds;
        GD.Print(
            $"SQUAD_PERFORMANCE_CHECK valid={valid} mates={mates.Length} planned={plannedRoutes} deferred={deferredPlans} "
            + $"connector_cache={_squadPortalWalkCorridorCacheReady} "
            + $"max_usec={maximumMicroseconds} max_budget={SquadPerformanceMaximumPlanMicroseconds} "
            + $"total_usec={totalMicroseconds} total_budget={SquadPerformanceMaximumTotalMicroseconds} "
            + $"reuse_samples={reuseSamples} reuse_max_usec={reuseMaximumMicroseconds} "
            + $"reuse_max_budget={SquadPerformanceMaximumReuseMicroseconds} "
            + $"reuse_total_usec={reuseTotalMicroseconds} "
            + $"reuse_total_budget={SquadPerformanceMaximumReuseTotalMicroseconds}");
        GD.Print($"SQUAD_PERFORMANCE_PASS valid={valid}");
        fixture.QueueFree();
        GetTree().Quit(valid ? 0 : 2);
    }
}
