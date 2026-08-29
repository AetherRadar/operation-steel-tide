using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateSquadGlassRescue()
    {
        var baselineProbeFree = false;
        var assigned = false;
        var approachPlanned = false;
        var glassHeldUntilCommit = false;
        var cachedPlanReused = false;
        var glassShattered = false;
        var traversalSeen = false;
        var dropCompleted = false;
        var rescued = false;
        var probeComputations = 0;
        var probeThrottles = 0;
        var planReuses = 0;
        var failure = string.Empty;
        var mateFinal = Vector3.Zero;
        Node3D? fixture = null;

        try
        {
            await WaitFrames(8);
            _missionDirector.ExitDeploymentZone();
            _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
            var mate = _squadMates.FirstOrDefault(candidate =>
                IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
            if (mate is null)
            {
                throw new InvalidOperationException("missing squad mate");
            }

            foreach (var other in _squadMates.Where(candidate =>
                         IsInstanceValid(candidate) && candidate != mate))
            {
                other.ProcessMode = ProcessModeEnum.Disabled;
                other.GlobalPosition = new Vector3(
                    560.0f + other.SquadSlot * 3.0f,
                    80.3f,
                    560.0f);
            }
            foreach (var enemy in _enemies.Where(IsInstanceValid))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.GlobalPosition = new Vector3(580.0f, 80.3f, 580.0f);
            }
            foreach (var civilian in _civilians.Where(IsInstanceValid))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
                civilian.GlobalPosition = new Vector3(600.0f, 80.3f, 600.0f);
            }

            fixture = BuildSquadGlassRescueFixture(
                out var mateStart,
                out var playerTarget,
                out var glass);
            ClearLeaderReviveAi();
            ResetAiReviveAbandonment();
            SetSquadLeaderTrailForDiagnostics(Array.Empty<Vector3>());
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = playerTarget;
            _player.Velocity = Vector3.Zero;
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.SetReviveUsedForDiagnostics(false);
            _player.IsDead = false;

            mate.ProcessMode = ProcessModeEnum.Inherit;
            mate.SetProcess(false);
            mate.SetPhysicsProcess(false);
            mate.GlobalPosition = mateStart;
            mate.Velocity = Vector3.Zero;
            mate.RestoreHealth(mate.MaxHealth);
            mate.ResetCombatTacticsForDiagnostics();
            mate.GrantFireablePrimaryForDiagnostics();
            mate.SetOrder(SquadOrder.Follow, mateStart);
            ClearSquadNavigation(mate);
            await WaitFrames(5);

            var baselineComputations = mate.RescueGlassProbeComputationsForDiagnostics;
            for (var sample = 0; sample < 180; sample++)
            {
                _ = mate.TryResolveEmergencyGlassEgress(playerTarget, out _);
            }
            baselineProbeFree = mate.RescueGlassProbeComputationsForDiagnostics
                == baselineComputations;

            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            if (!_player.IsDead || !_localPlayerDowned)
            {
                throw new InvalidOperationException("player did not enter downed state");
            }

            mate.SetProcess(true);
            mate.SetPhysicsProcess(true);
            UpdateSquadReviveAi(1.0f / 60.0f);
            assigned = ReferenceEquals(_leaderReviver, mate) && mate.IsRevivingLeader;
            mate.SetProcess(false);
            mate.SetPhysicsProcess(false);
            var probesBeforePlan = mate.RescueGlassProbeComputationsForDiagnostics;
            approachPlanned = mate.TryResolveEmergencyGlassEgress(
                    playerTarget,
                    out var approachDirective)
                && approachDirective.Target.DistanceSquaredTo(mateStart) > 0.25f
                && approachDirective.PreciseTrail;
            glassHeldUntilCommit = glass.ShatteredCount == 0;
            for (var sample = 0; sample < 48; sample++)
            {
                _ = mate.TryResolveEmergencyGlassEgress(playerTarget, out _);
            }
            probeComputations = mate.RescueGlassProbeComputationsForDiagnostics - probesBeforePlan;
            probeThrottles = mate.RescueGlassProbeThrottlesForDiagnostics;
            planReuses = mate.RescueGlassPlanReusesForDiagnostics;
            cachedPlanReused = probeComputations == 1 && planReuses >= 48;

            _reviverNoProgressTime = SquadRescueEgressNoProgressSeconds;
            var traversalCount = mate.CompletedNavigationTraversalsForDiagnostics;
            mate.SetProcess(true);
            mate.SetPhysicsProcess(true);
            for (var frame = 0; frame < 900 && _player.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                glassShattered |= glass.ShatteredCount == 1
                    && mate.RescueGlassShattersForDiagnostics >= 1;
                traversalSeen |= mate.IsRescueGlassTraversalForDiagnostics;
                dropCompleted |= mate.CompletedNavigationTraversalsForDiagnostics > traversalCount
                    && mate.LastCompletedNavigationTraversalKindForDiagnostics == SquadTraversalKind.Drop
                    && mate.GlobalPosition.Y < mateStart.Y - 2.5f;
            }
            rescued = !_player.IsDead && _player.ReviveUsed && !_localPlayerDowned;
            mateFinal = mate.GlobalPosition;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"SQUAD_GLASS_RESCUE_EXCEPTION {failure}");
        }

        var valid = baselineProbeFree
            && assigned
            && approachPlanned
            && glassHeldUntilCommit
            && cachedPlanReused
            && glassShattered
            && traversalSeen
            && dropCompleted
            && rescued
            && probeComputations <= 1
            && string.IsNullOrEmpty(failure);
        GD.Print(
            $"SQUAD_GLASS_RESCUE_CHECK valid={valid} baseline_probe_free={baselineProbeFree} "
            + $"assigned={assigned} approach={approachPlanned} held={glassHeldUntilCommit} "
            + $"cache={cachedPlanReused} shattered={glassShattered} traversal={traversalSeen} "
            + $"drop={dropCompleted} rescued={rescued} probes={probeComputations} "
            + $"throttles={probeThrottles} reuses={planReuses} "
            + $"mate=({mateFinal.X:0.00},{mateFinal.Y:0.00},{mateFinal.Z:0.00}) failure={failure}");
        GD.Print($"SQUAD_GLASS_RESCUE_PASS valid={valid}");
        fixture?.QueueFree();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private Node3D BuildSquadGlassRescueFixture(
        out Vector3 mateStart,
        out Vector3 playerTarget,
        out BreakableGlassField glass)
    {
        var root = new Node3D
        {
            Name = "SquadGlassRescueDiagnostic",
            Position = new Vector3(520.0f, 80.0f, 520.0f)
        };
        AddChild(root);

        AddSquadTraversalBox(
            root,
            "GlassRescueLowerFloor",
            new Vector3(0.0f, -0.15f, -2.0f),
            new Vector3(8.0f, 0.3f, 7.0f));
        AddSquadTraversalBox(
            root,
            "GlassRescueUpperRoom",
            new Vector3(0.0f, 1.5f, 2.1f),
            new Vector3(7.0f, 3.0f, 4.0f));
        AddSquadTraversalBox(
            root,
            "GlassRescueFacadeWall",
            new Vector3(0.0f, 4.5f, 0.0f),
            new Vector3(7.0f, 3.0f, 0.2f));

        var glassMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.42f, 0.82f, 0.94f, 0.42f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var frameMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.18f, 0.2f)
        };
        glass = new BreakableGlassField { Name = "GlassRescueBreakablePane" };
        root.AddChild(glass);
        glass.Configure(glassMaterial, frameMaterial, visibilityRange: 35.0f);
        glass.AddPane(
            new Vector3(0.0f, 4.72f, -0.115f),
            new Vector3(2.5f, 1.9f, 0.035f),
            new Color(0.55f, 0.9f, 0.98f, 0.78f));
        glass.Commit();

        mateStart = root.ToGlobal(new Vector3(0.0f, 3.08f, 2.2f));
        playerTarget = root.ToGlobal(new Vector3(0.0f, 0.08f, -2.0f));
        return root;
    }
}
