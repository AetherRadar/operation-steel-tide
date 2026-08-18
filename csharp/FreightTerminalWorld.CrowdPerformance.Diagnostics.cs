using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int CrowdPerformanceMaximumOperators = 40;
    private const int CrowdPerformanceWarmupFrames = 60;
    private const int CrowdPerformanceMeasuredFrames = 720;
    private const ulong CrowdPerformanceSlowFrameMicroseconds = 200_000;
    private const ulong CrowdPerformanceMaximumFrameMicroseconds = 2_000_000;
    private const ulong CrowdPerformanceMaximumAverageMicroseconds = 70_000;

    private async void ValidateCrowdPerformance()
    {
        SetProcess(false);
        var operators = _enemies
            .Where(enemy => IsInstanceValid(enemy) && !enemy.IsWorldBoss)
            .Take(CrowdPerformanceMaximumOperators)
            .ToArray();
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        if (operators.Length < 24)
        {
            GD.Print($"CROWD_PERFORMANCE_CHECK valid=False reason=missing_operators operators={operators.Length}");
            GD.Print("CROWD_PERFORMANCE_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = new Vector3(8.0f, 0.2f, 7.0f);
        _player.Velocity = Vector3.Zero;
        var arena = new Vector3(8.0f, 0.2f, 18.0f);
        const int columns = 8;
        for (var index = 0; index < operators.Length; index++)
        {
            var enemy = operators[index];
            enemy.TeamId = index % 2;
            enemy.SentryMode = false;
            enemy.ApplyColdStartUnarmed();
            enemy.GlobalPosition = arena + new Vector3(
                (index % columns - 3.5f) * 1.35f,
                0.0f,
                (index / columns) * 1.35f);
            enemy.LookAt(
                new Vector3(_player.GlobalPosition.X, enemy.GlobalPosition.Y, _player.GlobalPosition.Z),
                Vector3.Up);
            enemy.ConfigureCombatProbeForDiagnostics(
                (ulong)(0xC0FFEE + index * 7919),
                _player.GlobalPosition,
                bypassPlayerProtection: true,
                suppressContactSharing: false);
            enemy.ProcessMode = ProcessModeEnum.Inherit;
        }
        InvalidateCombatTargetIndex();

        for (var frame = 0; frame < CrowdPerformanceWarmupFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        ResetCrowdCombatDiagnostics();
        foreach (var enemy in operators)
        {
            enemy.ResetCrowdPerformanceCountersForDiagnostics();
        }

        ulong totalMicroseconds = 0;
        ulong maximumMicroseconds = 0;
        var slowFrames = 0;
        for (var frame = 0; frame < CrowdPerformanceMeasuredFrames; frame++)
        {
            var heading = Mathf.Sin(frame * 0.017f) * 12.0f;
            _hud.SetMinimapPlayer(_player.GlobalPosition, heading);
            var started = Time.GetTicksUsec();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var elapsed = Time.GetTicksUsec() - started;
            totalMicroseconds += elapsed;
            maximumMicroseconds = Math.Max(maximumMicroseconds, elapsed);
            if (elapsed > CrowdPerformanceSlowFrameMicroseconds)
            {
                slowFrames++;
            }
        }

        var acquisitions = operators.Sum(enemy => enemy.TargetAcquisitionCountForDiagnostics);
        var candidateEvaluations = operators.Sum(enemy => enemy.TargetCandidateEvaluationCountForDiagnostics);
        var lineOfSightProbes = operators.Sum(enemy => enemy.LineOfSightProbeCountForDiagnostics);
        var shareRequests = operators.Sum(enemy => enemy.ContactShareRequestCountForDiagnostics);
        var lockedTargets = operators.Count(enemy => enemy.EngageTargetNode is not null);
        var averageMicroseconds = totalMicroseconds / CrowdPerformanceMeasuredFrames;
        var acquisitionBudget = operators.Length * 60;
        var candidateBudget = operators.Length * operators.Length * 90;
        var lineOfSightBudget = operators.Length * 155;
        var shareRequestBudget = operators.Length * 28;
        var broadcastBudget = operators.Length * 20;
        var recipientVisitBudget = operators.Length * operators.Length * 20;
        var indexRefreshBudget = CrowdPerformanceMeasuredFrames / 5 + 4;
        var valid = lockedTargets >= operators.Length * 3 / 4
            && acquisitions <= acquisitionBudget
            && candidateEvaluations <= candidateBudget
            && lineOfSightProbes <= lineOfSightBudget
            && shareRequests <= shareRequestBudget
            && CombatContactBroadcastCountForDiagnostics <= broadcastBudget
            && CombatContactRecipientVisitCountForDiagnostics <= recipientVisitBudget
            && CombatTargetIndexRefreshCountForDiagnostics <= indexRefreshBudget
            && maximumMicroseconds <= CrowdPerformanceMaximumFrameMicroseconds
            && averageMicroseconds <= CrowdPerformanceMaximumAverageMicroseconds
            && slowFrames <= 2;
        GD.Print(
            $"CROWD_PERFORMANCE_CHECK valid={valid} operators={operators.Length} locked={lockedTargets} "
            + $"frames={CrowdPerformanceMeasuredFrames} avg_usec={averageMicroseconds} "
            + $"avg_budget={CrowdPerformanceMaximumAverageMicroseconds} max_usec={maximumMicroseconds} "
            + $"max_budget={CrowdPerformanceMaximumFrameMicroseconds} slow_frames={slowFrames} "
            + $"acquisitions={acquisitions}/{acquisitionBudget} "
            + $"candidate_evaluations={candidateEvaluations}/{candidateBudget} "
            + $"los_probes={lineOfSightProbes}/{lineOfSightBudget} "
            + $"share_requests={shareRequests}/{shareRequestBudget} "
            + $"broadcasts={CombatContactBroadcastCountForDiagnostics}/{broadcastBudget} "
            + $"recipient_visits={CombatContactRecipientVisitCountForDiagnostics}/{recipientVisitBudget} "
            + $"index_refreshes={CombatTargetIndexRefreshCountForDiagnostics}/{indexRefreshBudget}");
        GD.Print($"CROWD_PERFORMANCE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
