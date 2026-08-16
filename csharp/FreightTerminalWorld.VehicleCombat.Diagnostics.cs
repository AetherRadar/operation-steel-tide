using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateVehicleCombat()
    {
        await WaitFrames(6);
        _missionDirector.ExitDeploymentZone();
        _player.EjectFromVehicleIfAny();
        _player.IsDead = false;
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        _player.ProcessMode = ProcessModeEnum.Inherit;

        foreach (var enemy in _enemies.ToArray())
        {
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.GlobalPosition = new Vector3(210.0f, 0.2f, 210.0f);
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            mate.GlobalPosition = new Vector3(220.0f, 0.2f, 220.0f);
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }

        var arena = new Vector3(0.0f, 0.05f, 55.0f);
        var vehicle = new DriveableVehicle
        {
            Name = "VehicleCombatDiagnosticTruck",
            Main = this,
            Position = arena
        };
        vehicle.Configure(
            "DIAGNOSTIC TRUCK",
            new Color(0.18f, 0.34f, 0.24f),
            maxHealth: 260.0f);
        AddChild(vehicle);

        var npc = SpawnEnemy(
            arena + new Vector3(-4.0f, 0.15f, -60.0f),
            alerted: false,
            teamId: 0);
        var rival = SpawnEnemy(
            arena + new Vector3(4.0f, 0.15f, -60.0f),
            alerted: false,
            teamId: 1);
        npc.GrantFireablePrimaryForDiagnostics();
        rival.GrantFireablePrimaryForDiagnostics();
        await WaitFrames(4);

        _player.GlobalPosition = vehicle.GlobalPosition
            + vehicle.GlobalBasis.X * -2.6f
            + Vector3.Up * 0.3f
            - vehicle.GlobalBasis.Z * 0.5f;
        UpdateDeploymentProtection();
        await WaitFrames(3);
        var entered = vehicle.TryEnter(_player);
        await WaitFrames(3);
        var playerHealthBefore = _player.Health;

        async Task<(bool Aware, bool Targeted, bool Sight, bool Fired, bool Damaged)> ExerciseShooter(
            EnemyOperator shooter,
            EnemyOperator parkedShooter,
            float lateralOffset,
            ulong seed)
        {
            parkedShooter.GlobalPosition = new Vector3(205.0f, 0.2f, 205.0f);
            parkedShooter.ProcessMode = ProcessModeEnum.Disabled;
            shooter.ConfigureCombatProbeForDiagnostics(
                seed,
                vehicle.GlobalPosition,
                bypassPlayerProtection: true,
                suppressContactSharing: true);
            shooter.ClearAlertForDiagnostics();
            shooter.GrantFireablePrimaryForDiagnostics();
            shooter.SentryMode = false;
            shooter.ProcessMode = ProcessModeEnum.Inherit;
            shooter.GlobalPosition = vehicle.GlobalPosition
                + new Vector3(lateralOffset, 0.15f, 50.0f);
            shooter.Velocity = Vector3.Zero;
            var away = shooter.GlobalPosition
                + (shooter.GlobalPosition - vehicle.GlobalPosition).Normalized() * 10.0f;
            shooter.LookAt(new Vector3(away.X, shooter.GlobalPosition.Y, away.Z), Vector3.Up);
            var shotsBefore = shooter.AttackShotsFired;
            var vehicleHealthBefore = vehicle.Health;
            var aware = false;
            var targeted = false;
            var sight = false;
            for (var frame = 0; frame < 480 && vehicle.Health >= vehicleHealthBefore - 0.01f; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                aware |= shooter.Alerted
                    && shooter.EngageTargetNode == _player
                    && shooter.TargetsOccupiedVehicleForDiagnostics(vehicle);
                targeted |= shooter.EngageTargetNode == _player
                    && shooter.TargetsOccupiedVehicleForDiagnostics(vehicle);
                sight |= shooter.HasCurrentTargetLineOfSightForDiagnostics();
            }

            return (
                aware,
                targeted,
                sight,
                shooter.AttackShotsFired > shotsBefore,
                vehicle.Health < vehicleHealthBefore - 0.01f);
        }

        var npcResult = await ExerciseShooter(npc, rival, -3.0f, 0x51A0UL);
        npc.ProcessMode = ProcessModeEnum.Disabled;
        vehicle.RestoreHealth(vehicle.MaxHealth);
        var rivalResult = await ExerciseShooter(rival, npc, 3.0f, 0xA170UL);
        rival.ProcessMode = ProcessModeEnum.Disabled;
        vehicle.RestoreHealth(vehicle.MaxHealth);
        var driverProtected = !_player.IsDead
            && _player.Health >= playerHealthBefore - 0.01f;

        var wall = new StaticBody3D
        {
            Name = "VehicleCombatDiagnosticWall",
            Position = vehicle.GlobalPosition + new Vector3(0.0f, 2.0f, -17.0f),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        wall.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(9.0f, 4.0f, 0.6f) }
        });
        AddChild(wall);
        rival.ConfigureCombatProbeForDiagnostics(
            0xB10CUL,
            vehicle.GlobalPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        rival.ClearAlertForDiagnostics();
        rival.GrantFireablePrimaryForDiagnostics();
        rival.SentryMode = true;
        rival.ProcessMode = ProcessModeEnum.Inherit;
        rival.GlobalPosition = vehicle.GlobalPosition + new Vector3(0.0f, 0.15f, -34.0f);
        rival.Velocity = Vector3.Zero;
        var wallHealthBefore = vehicle.Health;
        var wallShotsBefore = rival.AttackShotsFired;
        var wallAway = rival.GlobalPosition
            + (rival.GlobalPosition - vehicle.GlobalPosition).Normalized() * 10.0f;
        rival.LookAt(new Vector3(wallAway.X, rival.GlobalPosition.Y, wallAway.Z), Vector3.Up);
        await WaitFrames(8);
        var wallAware = rival.Alerted
            && rival.EngageTargetNode == _player
            && rival.TargetsOccupiedVehicleForDiagnostics(vehicle);
        rival.LookAt(vehicle.HostileAimPoint(rival.GlobalPosition), Vector3.Up);
        var wallBlocked = false;
        for (var frame = 0; frame < 75; frame++)
        {
            rival.ArmWeaponForDiagnostics();
            rival.LookAt(vehicle.HostileAimPoint(rival.GlobalPosition), Vector3.Up);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            wallBlocked |= rival.TargetsOccupiedVehicleForDiagnostics(vehicle)
                && !rival.HasCurrentTargetLineOfSightForDiagnostics();
        }
        var wallNoShots = rival.AttackShotsFired == wallShotsBefore;
        var wallNoDamage = vehicle.Health >= wallHealthBefore - 0.01f;
        rival.ProcessMode = ProcessModeEnum.Disabled;
        wall.QueueFree();
        await WaitFrames(3);

        _player.EjectFromVehicleIfAny();
        _player.GlobalPosition = new Vector3(190.0f, 0.2f, 190.0f);
        rival.ConfigureCombatProbeForDiagnostics(
            0xE017UL,
            vehicle.GlobalPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        rival.ClearAlertForDiagnostics();
        rival.GrantFireablePrimaryForDiagnostics();
        rival.SentryMode = true;
        rival.ProcessMode = ProcessModeEnum.Inherit;
        rival.GlobalPosition = vehicle.GlobalPosition + new Vector3(0.0f, 0.15f, -30.0f);
        rival.Velocity = Vector3.Zero;
        var emptyHealthBefore = vehicle.Health;
        var emptyShotsBefore = rival.AttackShotsFired;
        for (var frame = 0; frame < 60; frame++)
        {
            rival.ArmWeaponForDiagnostics();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var emptyIgnored = vehicle.Health >= emptyHealthBefore - 0.01f
            && rival.AttackShotsFired == emptyShotsBefore;

        rival.ProcessMode = ProcessModeEnum.Disabled;
        _player.IsDead = false;
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        _player.GlobalPosition = vehicle.GlobalPosition
            + vehicle.GlobalBasis.X * -2.6f
            + Vector3.Up * 0.3f
            - vehicle.GlobalBasis.Z * 0.5f;
        vehicle.Configure(
            "DIAGNOSTIC TRUCK",
            new Color(0.18f, 0.34f, 0.24f),
            maxHealth: 24.0f);
        await WaitFrames(2);
        var enteredForDestruction = vehicle.TryEnter(_player);
        await WaitFrames(2);
        rival.ConfigureCombatProbeForDiagnostics(
            0xD357UL,
            vehicle.GlobalPosition,
            bypassPlayerProtection: true,
            suppressContactSharing: true);
        rival.GrantFireablePrimaryForDiagnostics();
        rival.SentryMode = true;
        rival.ProcessMode = ProcessModeEnum.Inherit;
        rival.GlobalPosition = vehicle.GlobalPosition + new Vector3(0.0f, 0.15f, -22.0f);
        rival.Velocity = Vector3.Zero;
        rival.LookAt(vehicle.HostileAimPoint(rival.GlobalPosition), Vector3.Up);
        var destroyed = false;
        for (var frame = 0; frame < 90; frame++)
        {
            rival.ArmWeaponForDiagnostics();
            if (IsInstanceValid(vehicle))
            {
                rival.LookAt(vehicle.HostileAimPoint(rival.GlobalPosition), Vector3.Up);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            destroyed = !IsInstanceValid(vehicle) || vehicle.IsDestroyed;
            if (destroyed)
            {
                break;
            }
        }
        var driverEjected = !_player.IsInVehicle;

        var valid = entered
            && npcResult.Aware && npcResult.Targeted && npcResult.Sight && npcResult.Fired && npcResult.Damaged
            && rivalResult.Aware && rivalResult.Targeted && rivalResult.Sight && rivalResult.Fired && rivalResult.Damaged
            && driverProtected
            && wallAware && wallBlocked && wallNoShots && wallNoDamage
            && emptyIgnored
            && enteredForDestruction && destroyed && driverEjected;
        GD.Print($"VEHICLE_COMBAT_CHECK valid={valid} entered={entered} npc_aware={npcResult.Aware} npc_target={npcResult.Targeted} npc_sight={npcResult.Sight} npc_fire={npcResult.Fired} npc_damage={npcResult.Damaged} rival_aware={rivalResult.Aware} rival_target={rivalResult.Targeted} rival_sight={rivalResult.Sight} rival_fire={rivalResult.Fired} rival_damage={rivalResult.Damaged} driver_protected={driverProtected} wall_aware={wallAware} wall_blocked={wallBlocked} wall_no_shots={wallNoShots} wall_no_damage={wallNoDamage} empty_ignored={emptyIgnored} destroy_entered={enteredForDestruction} destroyed={destroyed} ejected={driverEjected}");
        GD.Print($"VEHICLE_COMBAT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
