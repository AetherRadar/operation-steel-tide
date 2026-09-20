using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateLanternCanalPerformance()
    {
        var valid = false;
        try
        {
            ActivateBattlefieldFromOperationsOffice();
            _player.GlobalPosition = LanternCanalDeploymentPoint;
            _player.Velocity = Vector3.Zero;
            _player.UiLocked = true;
            _player.GetNode<Camera3D>("Head/CombatCamera").MakeCurrent();
            _player.FaceWorldPointForDiagnostics(new Vector3(-68, 2, 20));
            var residents = _civilians.Where(npc => IsInstanceValid(npc)).ToArray();
            var guards = _enemies.Where(enemy => IsInstanceValid(enemy) && enemy.TeamId == 0 && !enemy.IsWorldBoss).ToArray();
            var frames = new double[180];
            for (var frame = -30; frame < frames.Length; frame++)
            {
                var start = Time.GetTicksUsec();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (frame >= 0) frames[frame] = (Time.GetTicksUsec() - start) / 1000.0;
            }
            Array.Sort(frames);
            foreach (var resident in residents)
                GD.Print($"LANTERN_RESIDENT_CHECK name={resident.Name} position={resident.GlobalPosition} dead={resident.IsDead} authored={resident.UsesAuthoredVisualForDiagnostics}");
            foreach (var guard in guards)
                GD.Print($"LANTERN_GUARD_CHECK position={guard.GlobalPosition} dead={guard.IsDead} visual={guard.AuthoredVisualIdForDiagnostics}");
            var populationReady = residents.Length == 8 && residents.All(npc =>
                !npc.IsDead && npc.UsesAuthoredVisualForDiagnostics && npc.GlobalPosition.Y > .9f)
                && guards.Length == 6 && guards.All(enemy => !enemy.IsDead
                    && enemy.AuthoredVisualIdForDiagnostics == OperatorVisualId.Garrison);
            var boundedFrames = frames[171] < 100 && frames[^1] < 1000;
            valid = populationReady && boundedFrames && _lanternCanalBuildMilliseconds < 10000;
            GD.Print($"LANTERN_PERFORMANCE_CHECK valid={valid} residents={residents.Length} guards={guards.Length} population={populationReady} preload_ms={LanternCanalPreloadCache.LoadMilliseconds} yielded_frames={LanternCanalPreloadCache.YieldedFrames} build_ms={_lanternCanalBuildMilliseconds} median_ms={frames[90]:F2} p95_ms={frames[171]:F2} max_ms={frames[^1]:F2} draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} video_mb={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:F1}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"LANTERN_PERFORMANCE_CHECK valid=False error={exception}");
        }
        GD.Print($"LANTERN_PERFORMANCE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
