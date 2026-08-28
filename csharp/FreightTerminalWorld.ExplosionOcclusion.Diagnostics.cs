using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateExplosionCover()
    {
        await WaitFrames(8);
        var mate = _squadMates.FirstOrDefault(candidate =>
            IsInstanceValid(candidate) && !candidate.IsHumanProxy && !candidate.IsDowned);
        if (!IsInstanceValid(_player) || mate is null)
        {
            PrintExplosionCoverResult(
                valid: false,
                "reason=missing_fixture");
            GetTree().Quit(2);
            return;
        }

        _missionDirector.ExitDeploymentZone();
        _player.ProcessMode = ProcessModeEnum.Disabled;
        mate.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                enemy.GlobalPosition = new Vector3(620.0f + enemy.NetworkId, 90.0f, 620.0f);
            }
        }
        foreach (var otherMate in _squadMates)
        {
            if (!IsInstanceValid(otherMate) || ReferenceEquals(otherMate, mate))
            {
                continue;
            }
            otherMate.ProcessMode = ProcessModeEnum.Disabled;
            otherMate.GlobalPosition = new Vector3(650.0f + otherMate.SquadSlot * 3.0f, 90.0f, 650.0f);
        }

        var stage = new Vector3(480.0f, 60.0f, 480.0f);
        _player.GlobalPosition = stage;
        _player.Velocity = Vector3.Zero;
        mate.GlobalPosition = stage + Vector3.Right * 1.2f;
        mate.Velocity = Vector3.Zero;
        await WaitExplosionPhysicsFrames();

        var sideBlast = stage + new Vector3(0.6f, 1.02f, -6.0f);
        var overheadBlast = stage + new Vector3(0.6f, 6.0f, 0.0f);
        const float radius = 10.0f;
        const float damage = 60.0f;

        ResetExplosionDiagnosticHealth(mate);
        var openPlayerExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), sideBlast, _player, this);
        var openMateExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), sideBlast, mate, this);
        Explode(sideBlast, radius, damage, this);
        var openPlayerDamage = _player.MaxHealth - _player.Health;
        var openMateDamage = mate.MaxHealth - mate.Health;
        var openDamageApplied = openPlayerExposure.IsExposed
            && openMateExposure.IsExposed
            && openPlayerDamage > 0.01f
            && openMateDamage > 0.01f;

        ResetExplosionDiagnosticHealth(mate);
        var roof = CreateExplosionDiagnosticCover(
            "ExplosionCoverRoof",
            stage + new Vector3(0.6f, 2.7f, 0.0f),
            new Vector3(6.0f, 0.5f, 6.0f));
        await WaitExplosionPhysicsFrames();
        var roofPlayerExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), overheadBlast, _player, this);
        var roofMateExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), overheadBlast, mate, this);
        var roofPlayerHealth = _player.Health;
        var roofMateHealth = mate.Health;
        Explode(overheadBlast, radius, damage, this);
        ApplyAircraftStrike(overheadBlast, radius, damage, this);
        var roofBlocksPlayer = Mathf.IsEqualApprox(_player.Health, roofPlayerHealth);
        var roofBlocksMate = Mathf.IsEqualApprox(mate.Health, roofMateHealth);
        roof.QueueFree();
        await WaitExplosionPhysicsFrames();

        ResetExplosionDiagnosticHealth(mate);
        var wall = CreateExplosionDiagnosticCover(
            "ExplosionCoverWall",
            stage + new Vector3(0.6f, 1.7f, -3.0f),
            new Vector3(6.0f, 3.4f, 0.5f));
        await WaitExplosionPhysicsFrames();
        var wallPlayerExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), sideBlast, _player, this);
        var wallMateExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), sideBlast, mate, this);
        var wallPlayerHealth = _player.Health;
        var wallMateHealth = mate.Health;
        Explode(sideBlast, radius, damage, this);
        var wallBlocksPlayer = Mathf.IsEqualApprox(_player.Health, wallPlayerHealth);
        var wallBlocksMate = Mathf.IsEqualApprox(mate.Health, wallMateHealth);
        var wallSurfaceBlast = stage + new Vector3(0.6f, 1.02f, -3.26f);
        var surfacePlayerExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), wallSurfaceBlast, _player, this);
        var surfaceMateExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), wallSurfaceBlast, mate, this);
        var wallSurfaceBlocks = !surfacePlayerExposure.IsExposed
            && !surfaceMateExposure.IsExposed;
        wall.QueueFree();
        await WaitExplosionPhysicsFrames();

        ResetExplosionDiagnosticHealth(mate);
        var lowerWall = CreateExplosionDiagnosticCover(
            "ExplosionCoverOpeningLower",
            stage + new Vector3(0.6f, 0.425f, -3.0f),
            new Vector3(6.0f, 0.85f, 0.5f));
        var upperWall = CreateExplosionDiagnosticCover(
            "ExplosionCoverOpeningUpper",
            stage + new Vector3(0.6f, 2.1f, -3.0f),
            new Vector3(6.0f, 1.8f, 0.5f));
        await WaitExplosionPhysicsFrames();
        var openingPlayerExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), sideBlast, _player, this);
        var openingMateExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), sideBlast, mate, this);
        Explode(sideBlast, radius, damage, this);
        var openingPlayerDamage = _player.MaxHealth - _player.Health;
        var openingMateDamage = mate.MaxHealth - mate.Health;
        var openingRestoresDamage = openingPlayerExposure.IsExposed
            && openingMateExposure.IsExposed
            && openingPlayerDamage > 0.01f
            && openingMateDamage > 0.01f;
        lowerWall.QueueFree();
        upperWall.QueueFree();
        await WaitExplosionPhysicsFrames();

        ResetExplosionDiagnosticHealth(mate);
        var highImpactHealth = mate.Health;
        var enemyExplosionSource = _enemies.FirstOrDefault(enemy =>
            IsInstanceValid(enemy) && !enemy.IsDead);
        mate.TakeExplosionCombatDamage(
            6.0f,
            mate.GlobalPosition + Vector3.Up * 8.0f,
            (Node?)enemyExplosionSource ?? this);
        var highImpactDamage = highImpactHealth - mate.Health;
        var explosionTorso = enemyExplosionSource is not null
            && mate.LastDamageRegionForDiagnostics == HitRegion.Torso
            && Mathf.IsEqualApprox(highImpactDamage, 6.0f);

        ResetExplosionDiagnosticHealth(mate);
        var barrel = new ExplosiveBarrel
        {
            Name = "ExplosionCoverOpenBarrel",
            Main = this,
            Position = stage + new Vector3(0.6f, 0.0f, -2.0f)
        };
        AddChild(barrel);
        await WaitExplosionPhysicsFrames();
        barrel.TakeDamage(
            100.0f,
            barrel.GlobalPosition + Vector3.Up * 0.5f,
            (Node?)enemyExplosionSource ?? this);
        var barrelPlayerDamage = _player.MaxHealth - _player.Health;
        var barrelMateDamage = mate.MaxHealth - mate.Health;
        var barrelOpenDamage = barrel.Exploded
            && barrelPlayerDamage > 0.01f
            && barrelMateDamage > 0.01f;

        ResetExplosionDiagnosticHealth(mate);
        mate.GlobalPosition = stage + Vector3.Right * 12.0f;
        await WaitExplosionPhysicsFrames();
        var shellFloor = CreateExplosionDiagnosticCover(
            "ExplosionCoverShellFloor",
            stage + new Vector3(0.6f, -0.25f, 0.0f),
            new Vector3(6.0f, 0.5f, 6.0f));
        await WaitExplosionPhysicsFrames();
        const float shellDamage = 8.0f;
        const float shellRadius = 4.0f;
        var shellLanded = false;
        var shellBlastOrigin = Vector3.Zero;
        var shell = new AircraftShell
        {
            Name = "ExplosionCoverSingleDamageShell",
            Main = this,
            OwnerAircraft = this,
            Position = stage + new Vector3(0.6f, 2.0f, 0.0f)
        };
        shell.Detonated += onGround =>
        {
            shellLanded = onGround;
            shellBlastOrigin = shell.GlobalPosition + Vector3.Up * 0.2f;
        };
        AddChild(shell);
        shell.Launch(shell.Position, stage + new Vector3(0.6f, -1.0f, 0.0f), shellDamage, shellRadius);
        for (var frame = 0; frame < 30 && !shellLanded; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var shellExposure = ExplosionExposureResolver.ResolveCombatant(
            GetWorld3D(), shellBlastOrigin, _player, this);
        var expectedShellDamage = shellDamage
            * 0.72f
            * (1.0f - _player.GlobalPosition.DistanceTo(shellBlastOrigin) / shellRadius)
            * shellExposure.Fraction;
        var appliedShellDamage = _player.MaxHealth - _player.Health;
        var shellDamageAppliedOnce = shellLanded
            && shellExposure.IsExposed
            && Mathf.Abs(appliedShellDamage - expectedShellDamage) < 0.06f;
        shellFloor.QueueFree();

        var solidCoverInvisible = !roofPlayerExposure.IsExposed
            && !roofMateExposure.IsExposed
            && !wallPlayerExposure.IsExposed
            && !wallMateExposure.IsExposed;
        var valid = openDamageApplied
            && roofBlocksPlayer
            && roofBlocksMate
            && wallBlocksPlayer
            && wallBlocksMate
            && wallSurfaceBlocks
            && openingRestoresDamage
            && solidCoverInvisible
            && explosionTorso
            && barrelOpenDamage
            && shellDamageAppliedOnce;
        PrintExplosionCoverResult(
            valid,
            $"open_damage={openDamageApplied} open_player={openPlayerDamage:0.00} "
            + $"open_mate={openMateDamage:0.00} open_samples={openPlayerExposure.VisibleSamples}/{openPlayerExposure.TotalSamples},"
            + $"{openMateExposure.VisibleSamples}/{openMateExposure.TotalSamples} "
            + $"roof_player={roofBlocksPlayer} roof_mate={roofBlocksMate} "
            + $"roof_samples={roofPlayerExposure.VisibleSamples}/{roofPlayerExposure.TotalSamples},"
            + $"{roofMateExposure.VisibleSamples}/{roofMateExposure.TotalSamples} "
            + $"wall_player={wallBlocksPlayer} wall_mate={wallBlocksMate} "
            + $"wall_samples={wallPlayerExposure.VisibleSamples}/{wallPlayerExposure.TotalSamples},"
            + $"{wallMateExposure.VisibleSamples}/{wallMateExposure.TotalSamples} "
            + $"wall_surface={wallSurfaceBlocks} "
            + $"wall_surface_samples={surfacePlayerExposure.VisibleSamples}/{surfacePlayerExposure.TotalSamples},"
            + $"{surfaceMateExposure.VisibleSamples}/{surfaceMateExposure.TotalSamples} "
            + $"opening_damage={openingRestoresDamage} opening_player={openingPlayerDamage:0.00} "
            + $"opening_mate={openingMateDamage:0.00} "
            + $"opening_samples={openingPlayerExposure.VisibleSamples}/{openingPlayerExposure.TotalSamples},"
            + $"{openingMateExposure.VisibleSamples}/{openingMateExposure.TotalSamples} "
            + $"explosion_torso={explosionTorso} high_impact_damage={highImpactDamage:0.00} "
            + $"barrel_open={barrelOpenDamage} "
            + $"barrel_damage={barrelPlayerDamage:0.00}/{barrelMateDamage:0.00} "
            + $"shell_once={shellDamageAppliedOnce} shell_landed={shellLanded} "
            + $"shell_damage={appliedShellDamage:0.00}/{expectedShellDamage:0.00}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private void ResetExplosionDiagnosticHealth(SquadMate mate)
    {
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        _player.SetArmorForDiagnostics(0.0f);
        mate.RestoreHealth(mate.MaxHealth);
    }

    private StaticBody3D CreateExplosionDiagnosticCover(string name, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = name,
            Position = position,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
        return body;
    }

    private async System.Threading.Tasks.Task WaitExplosionPhysicsFrames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void PrintExplosionCoverResult(bool valid, string detail)
    {
        GD.Print($"EXPLOSION_COVER_CHECK valid={valid} {detail}");
        GD.Print($"EXPLOSION_COVER_PASS valid={valid}");
    }
}
