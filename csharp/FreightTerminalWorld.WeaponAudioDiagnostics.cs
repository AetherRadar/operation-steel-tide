using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateWeaponAudio()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(4);

        var platforms = new[]
        {
            WeaponPlatform.M3A1,
            WeaponPlatform.M4A1,
            WeaponPlatform.VSS
        };
        var signatures = new HashSet<int>();
        var streamCount = 0;
        var levelsReady = true;
        var reports = new List<string>(platforms.Length);
        var smgFired = false;
        var smgPlaying = false;
        foreach (var platform in platforms)
        {
            _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(platform, 0));
            await WaitFrames(2);
            var ready = _player.PlayerWeaponAudioReadyForDiagnostics;
            var signature = _player.PlayerWeaponAudioSignatureForDiagnostics;
            var volume = _player.PlayerWeaponAudioVolumeDbForDiagnostics;
            streamCount += ready ? 1 : 0;
            levelsReady &= float.IsFinite(volume) && volume >= -6.5f;
            if (signature != 0)
            {
                signatures.Add(signature);
            }
            if (platform == WeaponPlatform.M3A1)
            {
                smgFired = _player.FireForDiagnostics();
                await WaitFrames(2);
                smgPlaying = _player.PlayerWeaponAudioPlayingForDiagnostics;
            }
            reports.Add($"{platform}:{ready}:{volume:0.0}:{signature}");
        }

        var localPlayback = _player.PlayerWeaponAudioIsLocalForDiagnostics;
        var valid = localPlayback
            && streamCount == platforms.Length
            && signatures.Count == platforms.Length
            && levelsReady
            && smgFired
            && smgPlaying;
        GD.Print(
            $"WEAPON_AUDIO_CHECK valid={valid} local={localPlayback} "
            + $"streams={streamCount}/{platforms.Length} "
            + $"signatures={signatures.Count}/{platforms.Length} levels={levelsReady} "
            + $"smg_fired={smgFired} smg_playing={smgPlaying} "
            + $"reports={string.Join(',', reports)}");
        GD.Print($"WEAPON_AUDIO_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateWeaponImpact()
    {
        DisableActorsForSurvivalDiagnostics();
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        await WaitFrames(6);

        var samples = new[]
        {
            (Platform: WeaponPlatform.MP5A5, MinimumKickback: 0.035f),
            (Platform: WeaponPlatform.AK74, MinimumKickback: 0.052f),
            (Platform: WeaponPlatform.AWM, MinimumKickback: 0.09f)
        };
        var valid = true;
        var reports = new List<string>(samples.Length);
        foreach (var sample in samples)
        {
            _player.GrantFireablePrimaryForDiagnostics(
                WeaponCatalog.Build(sample.Platform, 0));
            await WaitFrames(8);
            var idle = _player.InspectViewmodelShotImpulseForDiagnostics();
            var fired = _player.FireForDiagnostics();
            var impact = _player.InspectViewmodelShotImpulseForDiagnostics();
            var audioPlaying = _player.PlayerWeaponAudioPlayingForDiagnostics;
            var immediateValid = fired
                && idle.Kickback <= 0.001f
                && impact.Kickback >= sample.MinimumKickback
                && impact.Pitch <= -0.018f
                && impact.MuzzleBloomVisible
                && impact.MuzzleLightEnergy >= 3.0f
                && audioPlaying;
            valid &= immediateValid;

            await WaitFrames(1);
            if (sample.Platform == WeaponPlatform.AK74)
            {
                SaveViewportImage("res://weapon_ak74_fire_validation.png");
            }
            for (var frame = 0; frame < 30; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            var recovered = _player.InspectViewmodelShotImpulseForDiagnostics();
            var recoveredValid = recovered.Kickback <= impact.Kickback * 0.08f
                && Mathf.Abs(recovered.Pitch) <= Mathf.Abs(impact.Pitch) * 0.08f;
            valid &= recoveredValid;
            reports.Add(
                $"{sample.Platform}:immediate={immediateValid}:recovered={recoveredValid}"
                + $":kick={impact.Kickback:F4}:pitch={impact.Pitch:F4}"
                + $":roll={impact.Roll:F4}:side={impact.Side:F4}"
                + $":light={impact.MuzzleLightEnergy:F2}:audio={audioPlaying}");
        }

        _player.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.MP5A5, 0));
        await WaitFrames(8);
        var firstShot = _player.FireForDiagnostics();
        for (var frame = 0; frame < 6; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var secondShot = _player.FireForDiagnostics();
        var activeVoices = _player.ActiveWeaponAudioVoiceCountForDiagnostics;
        var voiceCount = _player.WeaponAudioVoiceCountForDiagnostics;
        var overlappingTails = firstShot
            && secondShot
            && voiceCount >= 4
            && activeVoices >= 2;
        valid &= overlappingTails;

        GD.Print(
            $"WEAPON_IMPACT_CHECK valid={valid} samples={string.Join(',', reports)} "
            + $"voice_pool={voiceCount} active_voices={activeVoices} "
            + $"first_shot={firstShot} second_shot={secondShot} "
            + $"overlapping_tails={overlappingTails} "
            + "capture=weapon_ak74_fire_validation.png");
        GD.Print($"WEAPON_IMPACT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
