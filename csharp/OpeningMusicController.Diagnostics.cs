using Godot;

namespace OperationSteelTide;

internal partial class OpeningMusicController
{
    internal static async void RunDiagnostic(SceneTree tree)
    {
        var controller = new OpeningMusicController { Name = "OpeningMusicDiagnostic" };
        tree.Root.CallDeferred(Node.MethodName.AddChild, controller);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var stream = SoundLab.OpeningCombatTheme();
        var duration = stream.GetLength();
        var loaded = (stream.Format == AudioStreamWav.FormatEnum.Format16Bits
                || stream.Format == AudioStreamWav.FormatEnum.Qoa)
            && stream.MixRate == 44_100
            && stream.Stereo
            && stream.Data.Length > 500_000
            && duration is >= 29.9 and <= 30.1;
        var loopReady = stream.LoopMode == AudioStreamWav.LoopModeEnum.Forward
            && stream.LoopBegin == 0
            && stream.LoopEnd == Mathf.RoundToInt((float)duration * stream.MixRate);
        controller.SetMenuActive(true, immediate: true);
        var started = controller.MenuActiveForDiagnostics && controller.PlayingForDiagnostics;
        controller.SetMenuActive(false, immediate: true);
        var stopped = !controller.PlayingForDiagnostics
            && !controller.MenuActiveForDiagnostics
            && controller.GainForDiagnostics == 0.0f;
        controller.SetMenuActive(true, immediate: true);
        var restarted = controller.PlayingForDiagnostics
            && controller.GainForDiagnostics is > 0.0f and <= 0.2f;
        var valid = loaded && loopReady && started && stopped && restarted;
        GD.Print($"OPENING_MUSIC_CHECK loaded={loaded} format={stream.Format} rate={stream.MixRate} bytes={stream.Data.Length} "
            + $"duration={duration:F2} stereo={stream.Stereo} "
            + $"loop={loopReady} started={started} stopped={stopped} restarted={restarted} "
            + $"gain={controller.GainForDiagnostics:F2}");
        GD.Print($"OPENING_MUSIC_PASS valid={valid}");
        controller.QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        tree.Quit(valid ? 0 : 2);
    }
}
