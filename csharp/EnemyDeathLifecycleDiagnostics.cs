using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

/// <summary>Exercise an actual enemy elimination through its normal physics lifecycle.</summary>
public partial class EnemyDeathLifecycleDiagnostics : Node3D
{
    public override async void _Ready()
    {
        var arguments = OS.GetCmdlineUserArgs();
        if (!arguments.Contains("--validate-enemy-death-lifecycle"))
        {
            GD.PushError("The enemy death fixture requires --validate-enemy-death-lifecycle.");
            GetTree().Quit(2);
            return;
        }
        var valid = false;
        var stillAnimatingBeforeEnd = false;
        var completed = false;
        var physicsStopped = false;
        var duration = 0.0f;
        var elapsed = 0.0f;
        EnemyOperator? enemy = null;
        try
        {
            var selected = arguments.FirstOrDefault(argument =>
                argument.StartsWith("--operator-visual=", StringComparison.Ordinal));
            var visual = selected is null ? OperatorVisualId.Viper
                : Enum.Parse<OperatorVisualId>(selected.Split('=')[1], ignoreCase: true);
            enemy = new EnemyOperator
            {
                Name = "DeathLifecycleEnemy",
                OperatorVisual = visual,
                SimulationSeed = 17,
                Position = Vector3.Zero
            };
            enemy.ConfigureInitialLoadout(WeaponCatalog.Build(WeaponPlatform.M4A1, 0));
            AddChild(enemy);
            duration = enemy.AuthoredDeathDurationForDiagnostics;
            enemy.TakeDamage(10000.0f, enemy.GlobalPosition + Vector3.Up, armorPenetration: 1.0f);
            var probeTime = duration > 2.0f ? 1.95f : duration * 0.5f;
            var probeTaken = false;
            var maximumFrames = Mathf.CeilToInt((duration + 1.0f) * Engine.PhysicsTicksPerSecond) + 5;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                elapsed += (float)GetPhysicsProcessDeltaTime();
                if (!probeTaken && elapsed >= probeTime)
                {
                    probeTaken = true;
                    stillAnimatingBeforeEnd = enemy.IsPhysicsProcessing()
                        && !enemy.AuthoredDeathCompletedForDiagnostics
                        && enemy.AuthoredAnimationForDiagnostics == "death";
                }
                if (enemy.AuthoredDeathCompletedForDiagnostics && !enemy.IsPhysicsProcessing())
                {
                    completed = true;
                    physicsStopped = true;
                    break;
                }
            }
            valid = enemy.IsDead && stillAnimatingBeforeEnd && completed && physicsStopped;
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
        }
        finally
        {
            enemy?.QueueFree();
        }
        GD.Print($"ENEMY_DEATH_LIFECYCLE_CHECK duration={duration:F3} elapsed={elapsed:F3} "
            + $"before_end_running={stillAnimatingBeforeEnd} terminal={completed} physics_stopped={physicsStopped}");
        GD.Print($"ENEMY_DEATH_LIFECYCLE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
