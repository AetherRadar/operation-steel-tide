using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureGlassBreak()
    {
        await WaitFrames(6);
        var fieldNodes = GetTree().GetNodesInGroup(BreakableGlassField.GroupName);
        using var fieldNodesBacking = fieldNodes.AsDisposable();
        var field = fieldNodes
            .OfType<BreakableGlassField>()
            .FirstOrDefault(candidate => candidate.Name.ToString().StartsWith(
                "SkybridgeBreakableGlass",
                System.StringComparison.Ordinal));
        if (field is null || !field.TryGetIntactPaneRay(out var rayFrom, out var rayTo, out _))
        {
            GD.PushError("Glass capture could not find an intact pane.");
            GetTree().Quit(2);
            return;
        }

        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var center = (rayFrom + rayTo) * 0.5f;
        var outward = center.DirectionTo(rayFrom);
        rayFrom = center + outward * 1.2f;
        rayTo = center - outward * 1.2f;
        var standPosition = center + outward * 4.2f;
        standPosition.Y = Mathf.Max(0.12f, center.Y - 1.57f);
        _player.GlobalPosition = standPosition;
        _player.FaceWorldPointForDiagnostics(center);
        _player.GrantFireablePrimaryForDiagnostics();
        await WaitFrames(3);

        var shatteredBefore = field.ShatteredCount;
        _player.Fire();
        await WaitFrames(2);
        var shattered = field.ShatteredCount > shatteredBefore;
        var audioPlaying = _player.IsGlassBreakAudioPlaying;
        SaveViewportImage("res://glass_break_validation.png");
        GD.Print($"GLASS_CAPTURE shattered={shattered} player_audio={audioPlaying} path=glass_break_validation.png");
        GetTree().Quit(shattered && audioPlaying ? 0 : 2);
    }

    private async void ValidateBreakableGlass()
    {
        await WaitFrames(6);
        var fieldNodes = GetTree().GetNodesInGroup(BreakableGlassField.GroupName);
        using var fieldNodesBacking = fieldNodes.AsDisposable();
        var fields = fieldNodes
            .OfType<BreakableGlassField>()
            .Where(IsInstanceValid)
            .ToArray();
        var paneCount = fields.Sum(field => field.PaneCount);
        var frameInstances = fields.Sum(field => field.FrameInstanceCount);
        var field = fields.FirstOrDefault(candidate =>
            candidate.PaneCount >= 2
            && !candidate.Name.ToString().StartsWith("SkybridgeBreakableGlass", System.StringComparison.Ordinal));
        var rayFrom = Vector3.Zero;
        var rayTo = Vector3.Zero;
        var paneIndex = -1;
        var rayReady = field is not null
            && field.TryGetIntactPaneRay(out rayFrom, out rayTo, out paneIndex);
        var firstShotBlocked = false;
        var collisionDisabled = false;
        var secondShotCleared = false;
        var audioTriggered = false;
        var audioPlaying = false;
        var closeAudioPlaying = false;
        var skybridgeShot = false;
        var playerAudioPlaying = false;
        if (rayReady && field is not null)
        {
            firstShotBlocked = BreakableGlassField.TryShatterAlongRay(
                GetWorld3D(),
                rayFrom,
                rayTo,
                30.0f,
                rayFrom.DirectionTo(rayTo),
                out _,
                false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            collisionDisabled = field.IsPaneShattered(paneIndex)
                && field.IsPaneCollisionDisabled(paneIndex);
            secondShotCleared = !BreakableGlassField.TryShatterAlongRay(
                GetWorld3D(),
                rayFrom,
                rayTo,
                30.0f,
                rayFrom.DirectionTo(rayTo),
                out _,
                false);
            if (field.TryGetIntactPaneRay(out var audioFrom, out var audioTo, out _))
            {
                var audioCenter = (audioFrom + audioTo) * 0.5f;
                var listenerDirection = audioCenter.DirectionTo(audioFrom);
                var listenerPosition = audioCenter + listenerDirection * 3.0f;
                listenerPosition.Y = audioCenter.Y - 1.45f;
                _player.GlobalPosition = listenerPosition;
                _player.FaceWorldPointForDiagnostics(audioCenter);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                audioTriggered = BreakableGlassField.TryShatterAlongRay(
                    GetWorld3D(),
                    audioFrom,
                    audioTo,
                    30.0f,
                    audioFrom.DirectionTo(audioTo),
                    out _,
                    true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var audioNodes = GetTree().GetNodesInGroup(BreakableGlassField.AudioGroupName);
                using var audioNodesBacking = audioNodes.AsDisposable();
                audioPlaying = audioNodes
                    .Any(node => node switch
                    {
                        AudioStreamPlayer3D spatial => spatial.Playing && spatial.Stream?.GetLength() >= 0.6,
                        AudioStreamPlayer close => close.Playing && close.Stream?.GetLength() >= 0.6,
                        _ => false
                    });
                var closeAudioNodes = GetTree().GetNodesInGroup(BreakableGlassField.AudioGroupName);
                using var closeAudioNodesBacking = closeAudioNodes.AsDisposable();
                closeAudioPlaying = closeAudioNodes
                    .OfType<AudioStreamPlayer>()
                    .Any(audio => audio.Playing && audio.Stream?.GetLength() >= 0.6);
            }
        }

        var skybridgeField = fields.FirstOrDefault(candidate =>
            candidate.Name.ToString().StartsWith("SkybridgeBreakableGlass", System.StringComparison.Ordinal)
            && candidate.TryGetIntactPaneRay(out _, out _, out _));
        var skybridgeReady = skybridgeField is not null;
        var skybridgeFireAccepted = false;
        var skybridgeCrosshair = false;
        var skybridgeAlignment = -1.0f;
        if (skybridgeField is not null
            && skybridgeField.TryGetIntactPaneRay(out var skybridgeFrom, out var skybridgeTo, out _))
        {
            var skybridgeCenter = (skybridgeFrom + skybridgeTo) * 0.5f;
            var skybridgeNormal = skybridgeCenter.DirectionTo(skybridgeFrom);
            var playerPosition = skybridgeCenter + skybridgeNormal * 4.2f;
            playerPosition.Y = skybridgeCenter.Y - 1.57f;
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = playerPosition;
            _player.AimCameraAtWorldPointForDiagnostics(skybridgeCenter);
            _player.GrantFireablePrimaryForDiagnostics();
            skybridgeAlignment = _player.DiagnosticCameraForward.Dot(
                _player.DiagnosticCameraPosition.DirectionTo(skybridgeCenter));
            skybridgeCrosshair = _player.HasGlassInCrosshairForDiagnostics();
            var shatteredBefore = skybridgeField.ShatteredCount;
            skybridgeFireAccepted = _player.FireForDiagnostics();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            skybridgeShot = skybridgeField.ShatteredCount > shatteredBefore;
            playerAudioPlaying = _player.IsGlassBreakAudioPlaying;
        }

        var fieldsBatched = fields.Length > 0
            && fields.Length <= ResidentialTowerSpecs.Length + ResidentialSkyLinks.Sum(link => link.Floors.Length) + 2;
        var panesDense = paneCount >= 1200;
        var framesComplete = frameInstances == paneCount * 5;
        var singleSurfaceVisuals = fields.Length > 0 && fields.All(field => field.UsesSingleSurfaceVisual);
        var residentialTracked = ResidentialGlassPaneCount == paneCount;
        var valid = rayReady
            && firstShotBlocked
            && collisionDisabled
            && secondShotCleared
            && audioTriggered
            && audioPlaying
            && closeAudioPlaying
            && skybridgeShot
            && playerAudioPlaying
            && fieldsBatched
            && panesDense
            && framesComplete
            && singleSurfaceVisuals
            && residentialTracked;
        GD.Print($"GLASS_CHECK valid={valid} fields={fields.Length} panes={paneCount} frames={frameInstances} single_surface={singleSurfaceVisuals} ray_ready={rayReady} first_shot_blocked={firstShotBlocked} collision_disabled={collisionDisabled} second_shot_clear={secondShotCleared} audio_triggered={audioTriggered} audio_playing={audioPlaying} close_audio={closeAudioPlaying} skybridge_ready={skybridgeReady} skybridge_crosshair={skybridgeCrosshair} skybridge_alignment={skybridgeAlignment:0.000} skybridge_fire={skybridgeFireAccepted} skybridge_shot={skybridgeShot} player_audio={playerAudioPlaying} batched={fieldsBatched} dense={panesDense} tracked={residentialTracked}");
        GD.Print($"GLASS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
