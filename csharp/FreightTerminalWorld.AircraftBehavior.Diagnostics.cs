using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateAircraftBehavior()
    {
        await WaitFrames(8);
        var aircraft = _aircraft ?? _levelRoot.GetNodeOrNull<DestructibleAircraft>("DistantTiltRotor");
        var nearbyEnemy = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        if (aircraft is null || nearbyEnemy is null)
        {
            GD.Print("AIRCRAFT_BEHAVIOR_CHECK valid=False reason=missing_fixture");
            GD.Print("AIRCRAFT_BEHAVIOR_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        var randomPatrol = DestructibleAircraft.PatrolVariantCount >= 5
            && aircraft.PatrolVariantIndex >= 0
            && aircraft.PatrolVariantIndex < DestructibleAircraft.PatrolVariantCount
            && aircraft.PatrolPhaseForDiagnostics is >= 0.0f and < Mathf.Tau
            && Mathf.Abs(aircraft.PatrolDirectionForDiagnostics) == 1;
        var openingStart = aircraft.GlobalPosition;
        await WaitFrames(12);
        var openingPatrolOnly = !aircraft.IsCombatEngaged
            && aircraft.CurrentTargetForDiagnostics is null
            && aircraft.GlobalPosition.DistanceTo(openingStart) > 0.25f;

        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.ProcessMode = ProcessModeEnum.Disabled;
            mate.GlobalPosition = new Vector3(280.0f + mate.SquadSlot * 3.0f, 80.3f, 280.0f);
        }
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            if (!ReferenceEquals(enemy, nearbyEnemy))
            {
                enemy.GlobalPosition = new Vector3(300.0f + enemy.NetworkId, 80.3f, 300.0f);
            }
        }

        var probe = new Vector3(150.0f, 0.0f, 82.0f);
        var groundQuery = PhysicsRayQueryParameters3D.Create(
            probe + Vector3.Up * 90.0f,
            probe + Vector3.Down * 6.0f);
        groundQuery.CollisionMask = 1;
        groundQuery.CollideWithAreas = false;
        groundQuery.Exclude = new Godot.Collections.Array<Rid> { aircraft.GetRid() };
        var groundHit = GetWorld3D().DirectSpaceState.IntersectRay(groundQuery);
        var deploymentProtected = false;
        var visualEligible = false;
        var visualConfirmed = false;
        var visualTargetName = "none";
        var playerShotTriggered = false;
        var enemyShotTriggered = false;
        var targetHeld = false;
        var switchedNearby = false;
        var hardWindow = false;
        var cooldownStarted = false;
        var cooldownIgnoredShot = false;
        var damageBrokeCooldown = false;
        var allFactionsTargetable = false;
        var coveredShotIgnored = false;
        var coverBreaksTargetLock = false;
        var missionEndDisengaged = false;
        var cooldownWaitedForRejoin = false;
        var rejoinedPatrol = false;
        if (groundHit.Count > 0)
        {
            var surface = groundHit["position"].AsVector3();
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = surface + Vector3.Up * 0.3f;
            _player.Velocity = Vector3.Zero;
            nearbyEnemy.GlobalPosition = surface + new Vector3(12.0f, 0.3f, 3.0f);
            nearbyEnemy.Velocity = Vector3.Zero;
            var patrolPosition = _player.GlobalPosition + Vector3.Left * 36.0f + Vector3.Up * 40.0f;

            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            await WaitFrames(2);
            for (var scan = 0; scan < 7; scan++)
            {
                aircraft.AdvanceCombatStateForDiagnostics(0.15f);
            }
            deploymentProtected = !aircraft.IsCombatEngaged
                && aircraft.CurrentTargetForDiagnostics is null;

            _missionDirector.ExitDeploymentZone();
            await WaitFrames(2);
            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            visualEligible = aircraft.CanVisuallyDetectForDiagnostics(_player);
            for (var scan = 0; scan < 7; scan++)
            {
                aircraft.AdvanceCombatStateForDiagnostics(0.15f);
            }
            visualTargetName = aircraft.CurrentTargetForDiagnostics?.Name.ToString() ?? "none";
            visualConfirmed = aircraft.IsCombatEngaged
                && ReferenceEquals(aircraft.CurrentTargetForDiagnostics, _player);

            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 140.0f);
            playerShotTriggered = aircraft.IsCombatEngaged
                && ReferenceEquals(aircraft.CurrentTargetForDiagnostics, _player);

            NotifyAircraftOperatorAttack(nearbyEnemy, nearbyEnemy.GlobalPosition, 140.0f);
            targetHeld = ReferenceEquals(aircraft.CurrentTargetForDiagnostics, _player);
            var deadlineBeforeNoise = aircraft.EngagementRemainingForDiagnostics;
            aircraft.AdvanceCombatStateForDiagnostics(DestructibleAircraft.TargetLockDuration + 0.05f);
            switchedNearby = ReferenceEquals(aircraft.CurrentTargetForDiagnostics, nearbyEnemy);
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 140.0f);
            hardWindow = aircraft.EngagementRemainingForDiagnostics <= deadlineBeforeNoise;

            aircraft.AdvanceCombatStateForDiagnostics(
                aircraft.EngagementRemainingForDiagnostics + 0.05f);
            cooldownStarted = aircraft.IsCombatCooldown
                && aircraft.CurrentTargetForDiagnostics is null;
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 140.0f);
            cooldownIgnoredShot = aircraft.IsCombatCooldown
                && aircraft.CurrentTargetForDiagnostics is null;
            aircraft.TakeDamage(1.0f, aircraft.GlobalPosition, nearbyEnemy);
            damageBrokeCooldown = aircraft.IsCombatEngaged
                && ReferenceEquals(aircraft.CurrentTargetForDiagnostics, nearbyEnemy);

            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            NotifyAircraftOperatorAttack(nearbyEnemy, nearbyEnemy.GlobalPosition, 140.0f);
            enemyShotTriggered = aircraft.IsCombatEngaged
                && ReferenceEquals(aircraft.CurrentTargetForDiagnostics, nearbyEnemy);
            allFactionsTargetable = GetAircraftCombatants().Contains(_player)
                && GetAircraftCombatants().Contains(nearbyEnemy);

            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            var cover = CreateAircraftBehaviorDiagnosticRoof(_player.GlobalPosition + Vector3.Up * 3.0f);
            await WaitFrames(2);
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 140.0f);
            coveredShotIgnored = !aircraft.IsCombatEngaged
                && aircraft.CurrentTargetForDiagnostics is null;
            cover.QueueFree();
            await WaitFrames(2);

            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 140.0f);
            cover = CreateAircraftBehaviorDiagnosticRoof(_player.GlobalPosition + Vector3.Up * 3.0f);
            await WaitFrames(2);
            aircraft.AdvanceCombatStateForDiagnostics(0.05f);
            coverBreaksTargetLock = !ReferenceEquals(aircraft.CurrentTargetForDiagnostics, _player)
                && (aircraft.IsCombatCooldown
                    || ReferenceEquals(aircraft.CurrentTargetForDiagnostics, nearbyEnemy));
            cover.QueueFree();
            await WaitFrames(2);

            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 140.0f);
            var missionEndedBeforeProbe = _missionEnded;
            _missionEnded = true;
            aircraft.AdvanceCombatStateForDiagnostics(0.05f);
            missionEndDisengaged = aircraft.IsCombatCooldown
                && aircraft.CurrentTargetForDiagnostics is null
                && !GetAircraftCombatants().Any();
            _missionEnded = missionEndedBeforeProbe;

            var distantPatrolPosition = patrolPosition + new Vector3(420.0f, 0.0f, 420.0f);
            aircraft.SetPatrolStateForDiagnostics(distantPatrolPosition, Vector3.Right);
            NotifyAircraftOperatorAttack(_player, _player.GlobalPosition, 900.0f);
            aircraft.AdvanceCombatStateForDiagnostics(
                aircraft.EngagementRemainingForDiagnostics + 0.05f);
            aircraft.AdvanceCombatStateForDiagnostics(
                DestructibleAircraft.CombatCooldownDuration + 0.05f);
            cooldownWaitedForRejoin = aircraft.IsCombatCooldown;
            for (var step = 0; step < 1600 && aircraft.IsCombatCooldown; step++)
            {
                aircraft.AdvancePatrolStateForDiagnostics(0.05f);
                aircraft.AdvanceCombatStateForDiagnostics(0.05f);
            }
            rejoinedPatrol = !aircraft.IsCombatCooldown
                && !aircraft.IsCombatEngaged
                && aircraft.CurrentTargetForDiagnostics is null;
            aircraft.SetPatrolStateForDiagnostics(patrolPosition, Vector3.Right);
        }

        var valid = randomPatrol
            && openingPatrolOnly
            && deploymentProtected
            && visualConfirmed
            && playerShotTriggered
            && enemyShotTriggered
            && targetHeld
            && switchedNearby
            && hardWindow
            && cooldownStarted
            && cooldownIgnoredShot
            && damageBrokeCooldown
            && allFactionsTargetable
            && coveredShotIgnored
            && coverBreaksTargetLock
            && missionEndDisengaged
            && cooldownWaitedForRejoin
            && rejoinedPatrol;
        GD.Print(
            $"AIRCRAFT_BEHAVIOR_CHECK valid={valid} random_patrol={randomPatrol} variant={aircraft.PatrolVariantIndex}/{DestructibleAircraft.PatrolVariantCount} "
            + $"direction={aircraft.PatrolDirectionForDiagnostics} opening_patrol={openingPatrolOnly} deployment_guard={deploymentProtected} "
            + $"visual_eligible={visualEligible} visual_confirm={visualConfirmed} visual_target={visualTargetName} player_shot={playerShotTriggered} enemy_shot={enemyShotTriggered} "
            + $"target_hold={targetHeld} nearby_switch={switchedNearby} hard_window={hardWindow} cooldown={cooldownStarted} "
            + $"cooldown_ignores_shot={cooldownIgnoredShot} damage_breaks_cooldown={damageBrokeCooldown} all_factions={allFactionsTargetable} "
            + $"covered_shot_ignored={coveredShotIgnored} cover_breaks_lock={coverBreaksTargetLock} mission_end_disengage={missionEndDisengaged} "
            + $"cooldown_waits_rejoin={cooldownWaitedForRejoin} rejoined_patrol={rejoinedPatrol}");
        GD.Print($"AIRCRAFT_BEHAVIOR_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private StaticBody3D CreateAircraftBehaviorDiagnosticRoof(Vector3 center)
    {
        var roof = new StaticBody3D
        {
            Name = $"AircraftBehaviorRoof{Time.GetTicksUsec()}",
            Position = center,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        roof.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(10.0f, 0.4f, 10.0f) }
        });
        AddChild(roof);
        return roof;
    }
}
