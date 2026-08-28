using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct MeleeCombatDiagnostic(
        bool MultiTarget,
        bool TargetDeduplicated,
        bool PersistentRidSweep,
        bool WallBlocked,
        int HitTargets)
    {
        public bool Valid => MultiTarget
            && TargetDeduplicated
            && PersistentRidSweep
            && WallBlocked;
    }

    private async Task<MeleeCombatDiagnostic> ValidateMeleeCombatSemantics()
    {
        const float fixtureFloorY = 80.2f;
        const float bladeY = 81.08f;
        var fixtures = _enemies
            .Where(enemy => IsInstanceValid(enemy) && !enemy.IsWorldBoss)
            .Take(3)
            .ToArray();
        if (fixtures.Length < 3)
        {
            return default;
        }

        for (var index = 0; index < _enemies.Count; index++)
        {
            var enemy = _enemies[index];
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.SetPhysicsProcess(false);
            if (!fixtures.Contains(enemy))
            {
                enemy.GlobalPosition = new Vector3(220.0f + index, 70.0f, 220.0f);
            }
        }

        _player.PrepareMeleeCombatFixtureForDiagnostics();
        _player.GlobalPosition = new Vector3(0.0f, fixtureFloorY, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, fixtureFloorY, 30.0f));
        PrepareMeleeFixture(fixtures[0], new Vector3(-0.42f, fixtureFloorY, 38.8f));
        PrepareMeleeFixture(fixtures[1], new Vector3(0.42f, fixtureFloorY, 38.8f));
        fixtures[2].GlobalPosition = new Vector3(220.0f, 70.0f, 220.0f);
        await WaitFrames(4);

        var firstHealth = fixtures[0].CurrentHealth;
        var secondHealth = fixtures[1].CurrentHealth;
        var hitTargets = _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-1.0f, bladeY, 39.3f),
            new Vector3(-1.0f, bladeY, 38.3f),
            new Vector3(1.0f, bladeY, 39.3f),
            new Vector3(1.0f, bladeY, 38.3f),
            beginSwing: true);
        var multiTarget = fixtures[0].CurrentHealth < firstHealth
            && fixtures[1].CurrentHealth < secondHealth
            && hitTargets == 2;
        var firstAfter = fixtures[0].CurrentHealth;
        var secondAfter = fixtures[1].CurrentHealth;
        PrepareMeleeFixture(fixtures[2], new Vector3(0.0f, fixtureFloorY, 38.8f));
        await WaitFrames(4);
        var thirdHealth = fixtures[2].CurrentHealth;
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-1.0f, bladeY, 39.3f),
            new Vector3(-1.0f, bladeY, 38.3f),
            new Vector3(1.0f, bladeY, 39.3f),
            new Vector3(1.0f, bladeY, 38.3f),
            beginSwing: false);
        var targetDeduplicated = Mathf.IsEqualApprox(fixtures[0].CurrentHealth, firstAfter)
            && Mathf.IsEqualApprox(fixtures[1].CurrentHealth, secondAfter)
            && Mathf.IsEqualApprox(fixtures[2].CurrentHealth, thirdHealth);

        PrepareMeleeFixture(fixtures[0], new Vector3(-0.55f, fixtureFloorY, 38.8f));
        PrepareMeleeFixture(fixtures[1], new Vector3(0.95f, fixtureFloorY, 38.8f));
        fixtures[2].GlobalPosition = new Vector3(220.0f, 70.0f, 220.0f);
        await WaitFrames(4);
        var frontHealth = fixtures[0].CurrentHealth;
        var rearHealth = fixtures[1].CurrentHealth;
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_zhanma",
            2,
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(-0.12f, bladeY, 38.8f),
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(-0.12f, bladeY, 38.8f),
            beginSwing: true);
        var frontAfter = fixtures[0].CurrentHealth;
        var frontOnly = frontAfter < frontHealth
            && Mathf.IsEqualApprox(fixtures[1].CurrentHealth, rearHealth);
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_zhanma",
            2,
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(1.35f, bladeY, 38.8f),
            new Vector3(-1.0f, bladeY, 38.8f),
            new Vector3(1.35f, bladeY, 38.8f),
            beginSwing: false);
        var persistentRidSweep = frontOnly
            && Mathf.IsEqualApprox(fixtures[0].CurrentHealth, frontAfter)
            && fixtures[1].CurrentHealth < rearHealth;
        fixtures[0].GlobalPosition = new Vector3(221.0f, 70.0f, 220.0f);
        fixtures[1].GlobalPosition = new Vector3(222.0f, 70.0f, 220.0f);
        PrepareMeleeFixture(fixtures[2], new Vector3(0.0f, fixtureFloorY, 37.15f));
        var wall = new StaticBody3D
        {
            Name = "MeleeWallDiagnostic",
            Position = new Vector3(0.0f, fixtureFloorY + 0.95f, 38.0f),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        wall.AddChild(new CollisionShape3D
        {
            Name = "MeleeWallShape",
            Shape = new BoxShape3D { Size = new Vector3(3.0f, 2.3f, 0.22f) }
        });
        AddChild(wall);
        await WaitFrames(4);

        var protectedHealth = fixtures[2].CurrentHealth;
        _player.ResolveMeleeSweepForDiagnostics(
            "knife_tianxuan",
            0,
            new Vector3(-0.9f, bladeY, 37.55f),
            new Vector3(-0.9f, bladeY, 36.75f),
            new Vector3(0.9f, bladeY, 37.55f),
            new Vector3(0.9f, bladeY, 36.75f),
            beginSwing: true);
        var wallBlocked = Mathf.IsEqualApprox(fixtures[2].CurrentHealth, protectedHealth);
        wall.QueueFree();
        return new MeleeCombatDiagnostic(
            multiTarget,
            targetDeduplicated,
            persistentRidSweep,
            wallBlocked,
            hitTargets);
    }

    private static void PrepareMeleeFixture(EnemyOperator enemy, Vector3 position)
    {
        enemy.ResetTacticalStateForDiagnostics();
        enemy.GlobalPosition = position;
        enemy.Rotation = new Vector3(0.0f, Mathf.Pi, 0.0f);
        enemy.Velocity = Vector3.Zero;
        enemy.ProcessMode = ProcessModeEnum.Inherit;
        enemy.SetProcess(false);
        enemy.SetPhysicsProcess(false);
    }

    private static string FormatMeleeCombat(MeleeCombatDiagnostic combat)
        => $"valid:{combat.Valid};multi:{combat.MultiTarget};"
            + $"dedupe:{combat.TargetDeduplicated};persistent:{combat.PersistentRidSweep};"
            + $"wall:{combat.WallBlocked};"
            + $"targets:{combat.HitTargets}";
}
