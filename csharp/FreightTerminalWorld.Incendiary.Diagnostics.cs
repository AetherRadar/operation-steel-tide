using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateIncendiaryAiAvoidance()
    {
        await WaitFrames(30);
        var enemy = _enemies.FirstOrDefault(opponent =>
            IsInstanceValid(opponent)
            && !opponent.IsDead
            && !opponent.IsWorldBoss);
        var mate = _squadMates.FirstOrDefault(candidate =>
            IsInstanceValid(candidate)
            && !candidate.IsDowned
            && !candidate.IsBodyBag
            && !candidate.IsNetworkProxy);
        if (enemy is null || mate is null)
        {
            GD.Print($"INCENDIARY_AI_CHECK valid=False stage=preconditions enemy={enemy is not null} mate={mate is not null}");
            GD.Print("INCENDIARY_AI_PASS valid=False");
            QuitDiagnosticAfterSceneCleanup(2);
            return;
        }

        SetProcess(false);
        SetPhysicsProcess(false);
        var platformCenter = _player.GlobalPosition + Vector3.Up * 80.0f;
        var platform = CreateDemolitionEnemyResponsePlatform(platformCenter);
        platform.Name = "IncendiaryAiPlatform";
        var floorY = platformCenter.Y + 0.32f;
        var enemyStart = new Vector3(platformCenter.X - 7.0f, floorY, platformCenter.Z);
        var mateStart = new Vector3(platformCenter.X + 7.0f, floorY, platformCenter.Z);

        foreach (var opponent in _enemies)
        {
            if (!IsInstanceValid(opponent))
            {
                continue;
            }
            opponent.ProcessMode = ProcessModeEnum.Disabled;
            if (opponent != enemy)
            {
                opponent.GlobalPosition = platformCenter
                    + new Vector3(800.0f + opponent.NetworkId * 4.0f, 0.0f, 800.0f);
            }
        }
        foreach (var candidate in _squadMates)
        {
            if (!IsInstanceValid(candidate))
            {
                continue;
            }
            candidate.ProcessMode = ProcessModeEnum.Disabled;
            if (candidate != mate)
            {
                candidate.GlobalPosition = platformCenter
                    + new Vector3(-800.0f - candidate.SquadSlot * 4.0f, 0.0f, -800.0f);
            }
        }

        _player.SetPhysicsProcess(false);
        _player.GlobalPosition = platformCenter + Vector3.Back * 800.0f;
        _player.Velocity = Vector3.Zero;
        enemy.GlobalPosition = enemyStart;
        enemy.Velocity = Vector3.Zero;
        enemy.SentryMode = true;
        enemy.LookAt(enemyStart + Vector3.Forward, Vector3.Up);
        mate.GlobalPosition = mateStart;
        mate.Velocity = Vector3.Zero;
        mate.SetOrder(SquadOrder.Move, mateStart);
        mate.ResetIncendiaryAvoidanceForDiagnostics();

        var enemyFireA = CreateDiagnosticIncendiary(
            enemyStart + Vector3.Left * 1.5f,
            "EnemyFireA");
        var enemyFireB = CreateDiagnosticIncendiary(
            enemyStart + Vector3.Right * 1.5f,
            "EnemyFireB");
        var mateFire = CreateDiagnosticIncendiary(mateStart, "MateFire");
        var fireReady = enemyFireA.IsBurning
            && enemyFireB.IsBurning
            && mateFire.IsBurning;
        var resolverReady = TryGetIncendiaryEscapeDirection(
            enemyStart,
            Vector3.Forward,
            out var overlapEscape)
            && overlapEscape.LengthSquared() > 0.9f;
        var verticalIsolationReady = !TryGetIncendiaryEscapeDirection(
            enemyStart + Vector3.Up * (IncendiaryAiMaximumVerticalSeparation + 0.5f),
            Vector3.Forward,
            out _);

        enemy.ProcessMode = ProcessModeEnum.Inherit;
        mate.ProcessMode = ProcessModeEnum.Inherit;
        var enemyAvoided = false;
        var mateAvoided = false;
        var enemyMoved = false;
        var mateMoved = false;
        var enemyProgressStreak = 0;
        var mateProgressStreak = 0;
        var enemyLongestProgressStreak = 0;
        var mateLongestProgressStreak = 0;
        var previousEnemyDistance = MinimumIncendiaryDistance(
            enemy.GlobalPosition,
            enemyFireA,
            enemyFireB);
        var previousMateDistance = IncendiaryHorizontalDistance(
            mate.GlobalPosition,
            mateFire.GlobalPosition);
        var exitFrames = 0;
        const int maximumExitFrames = 150;
        while (exitFrames < maximumExitFrames)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var enemyState = enemy.CaptureIncendiaryAvoidanceForDiagnostics();
            enemyAvoided |= enemyState.Active && enemyState.EscapeMovementFrames > 0;
            mateAvoided |= mate.IsAvoidingIncendiaryForDiagnostics
                && mate.IncendiaryAvoidanceFramesForDiagnostics > 0;
            enemyMoved |= IncendiaryHorizontalDistance(enemy.GlobalPosition, enemyStart) > 0.6f;
            mateMoved |= IncendiaryHorizontalDistance(mate.GlobalPosition, mateStart) > 0.6f;
            var enemyDistance = MinimumIncendiaryDistance(
                enemy.GlobalPosition,
                enemyFireA,
                enemyFireB);
            var mateDistance = IncendiaryHorizontalDistance(
                mate.GlobalPosition,
                mateFire.GlobalPosition);
            enemyProgressStreak = enemyDistance > previousEnemyDistance + 0.005f
                ? enemyProgressStreak + 1
                : 0;
            mateProgressStreak = mateDistance > previousMateDistance + 0.005f
                ? mateProgressStreak + 1
                : 0;
            enemyLongestProgressStreak = Mathf.Max(
                enemyLongestProgressStreak,
                enemyProgressStreak);
            mateLongestProgressStreak = Mathf.Max(
                mateLongestProgressStreak,
                mateProgressStreak);
            previousEnemyDistance = enemyDistance;
            previousMateDistance = mateDistance;
            if (enemyDistance > IncendiaryGrenade.FireRadius + 0.85f
                && mateDistance > IncendiaryGrenade.FireRadius + 0.85f)
            {
                break;
            }
            exitFrames++;
        }

        var enemyExitDistance = MinimumIncendiaryDistance(
            enemy.GlobalPosition,
            enemyFireA,
            enemyFireB);
        var mateExitDistance = IncendiaryHorizontalDistance(
            mate.GlobalPosition,
            mateFire.GlobalPosition);
        var enemyExited = enemyExitDistance > IncendiaryGrenade.FireRadius + 0.8f;
        var mateExited = mateExitDistance > IncendiaryGrenade.FireRadius + 0.8f;
        var sustainedProgress = enemyLongestProgressStreak >= 12
            && mateLongestProgressStreak >= 12;

        // Keep the teammate's requested move destination inside the live fire. Its
        // predictive check must prevent normal navigation from walking it back in.
        mate.SetOrder(SquadOrder.Move, mateFire.GlobalPosition);
        // Put a confirmed pursuit target across the overlapping fires. Normal enemy
        // pursuit must resume without being allowed to pull the operator into flames.
        enemy.SentryMode = false;
        var conflictDirection = overlapEscape.LengthSquared() > 0.01f
            ? -overlapEscape.Normalized()
            : Vector3.Back;
        _player.GlobalPosition = enemyStart + conflictDirection * 16.0f;
        enemy.TakeDamage(0.1f, enemy.GlobalPosition + Vector3.Up, _player);
        var enemyPathConflict = enemy.CapturePursuitContactStateForDiagnostics()
            .RecentDamageThreatTargetId == _player.GetInstanceId();
        var enemyMinimumAfterExit = enemyExitDistance;
        var mateMinimumAfterExit = mateExitDistance;
        var enemyPathApproachedFire = false;
        var matePredictedAvoidance = false;
        const int reentryObservationFrames = 240;
        for (var frame = 0; frame < reentryObservationFrames; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var enemyDistance = MinimumIncendiaryDistance(
                enemy.GlobalPosition,
                enemyFireA,
                enemyFireB);
            enemyMinimumAfterExit = Mathf.Min(
                enemyMinimumAfterExit,
                enemyDistance);
            enemyPathApproachedFire |= enemyDistance < enemyExitDistance - 0.1f;
            mateMinimumAfterExit = Mathf.Min(
                mateMinimumAfterExit,
                IncendiaryHorizontalDistance(mate.GlobalPosition, mateFire.GlobalPosition));
            matePredictedAvoidance |= mate.IsAvoidingPredictedIncendiaryForDiagnostics;
        }

        var enemyDidNotReenter = enemyMinimumAfterExit >= IncendiaryGrenade.FireRadius;
        var mateDidNotReenter = mateMinimumAfterExit >= IncendiaryGrenade.FireRadius;
        var valid = fireReady
            && resolverReady
            && verticalIsolationReady
            && enemyAvoided
            && mateAvoided
            && enemyMoved
            && mateMoved
            && enemyExited
            && mateExited
            && sustainedProgress
            && enemyPathConflict
            && enemyPathApproachedFire
            && enemyDidNotReenter
            && matePredictedAvoidance
            && mateDidNotReenter;
        GD.Print($"INCENDIARY_AI_CHECK valid={valid} fire={fireReady} resolver={resolverReady}:{overlapEscape} vertical_isolation={verticalIsolationReady} enemy_avoid={enemyAvoided} enemy_move={enemyMoved} enemy_exit={enemyExited}:{enemyExitDistance:0.00}/{exitFrames} enemy_progress={enemyLongestProgressStreak} enemy_conflict={enemyPathConflict}:{enemyPathApproachedFire} enemy_no_reentry={enemyDidNotReenter}:{enemyMinimumAfterExit:0.00} mate_avoid={mateAvoided} mate_move={mateMoved} mate_exit={mateExited}:{mateExitDistance:0.00}/{exitFrames} mate_progress={mateLongestProgressStreak} mate_predicted={matePredictedAvoidance} mate_no_reentry={mateDidNotReenter}:{mateMinimumAfterExit:0.00}");
        GD.Print($"INCENDIARY_AI_PASS valid={valid}");

        enemy.ProcessMode = ProcessModeEnum.Disabled;
        mate.ProcessMode = ProcessModeEnum.Disabled;
        enemyFireA.QueueFree();
        enemyFireB.QueueFree();
        mateFire.QueueFree();
        platform.QueueFree();
        ClearDemolitionUtilityProjectiles();
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private IncendiaryGrenade CreateDiagnosticIncendiary(Vector3 position, string name)
    {
        var incendiary = new IncendiaryGrenade
        {
            Name = name,
            Position = position,
            OwnerBody = _player,
            DamageEnabled = false
        };
        AddChild(incendiary);
        incendiary.Arm(Vector3.Forward);
        incendiary.BeginGroundFuseForDiagnostics(position, Vector3.Up);
        incendiary._PhysicsProcess(0.5);
        return incendiary;
    }

    private static float MinimumIncendiaryDistance(
        Vector3 point,
        IncendiaryGrenade first,
        IncendiaryGrenade second)
        => Mathf.Min(
            IncendiaryHorizontalDistance(point, first.GlobalPosition),
            IncendiaryHorizontalDistance(point, second.GlobalPosition));

    private static float IncendiaryHorizontalDistance(Vector3 first, Vector3 second)
        => new Vector2(first.X - second.X, first.Z - second.Z).Length();
}
