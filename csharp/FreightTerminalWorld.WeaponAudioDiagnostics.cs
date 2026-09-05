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
            (Platform: WeaponPlatform.M4A1, MinimumLocalVolumeDb: 1.5f),
            (Platform: WeaponPlatform.AK74, MinimumLocalVolumeDb: 2.0f),
            (Platform: WeaponPlatform.ScarL, MinimumLocalVolumeDb: 2.0f),
            (Platform: WeaponPlatform.M24, MinimumLocalVolumeDb: 2.5f),
            (Platform: WeaponPlatform.MP5A5, MinimumLocalVolumeDb: 0.5f),
            (Platform: WeaponPlatform.M3A1, MinimumLocalVolumeDb: 1.0f),
            (Platform: WeaponPlatform.AXMC, MinimumLocalVolumeDb: 2.5f),
            (Platform: WeaponPlatform.P226, MinimumLocalVolumeDb: 1.5f),
            (Platform: WeaponPlatform.M1911, MinimumLocalVolumeDb: 2.5f),
            (Platform: WeaponPlatform.AWM, MinimumLocalVolumeDb: 2.5f),
            (Platform: WeaponPlatform.VSS, MinimumLocalVolumeDb: -4.5f),
            (Platform: WeaponPlatform.DesertEagle, MinimumLocalVolumeDb: 1.0f),
            (Platform: WeaponPlatform.GSh18, MinimumLocalVolumeDb: 1.5f)
        };
        var signatures = new HashSet<int>();
        var worldSignatures = new HashSet<int>();
        var enemySignatures = new HashSet<int>();
        var streamCount = 0;
        var levelsReady = true;
        var nearFieldDistinct = true;
        var worldDistinct = true;
        var enemyDistinct = true;
        var headroomReady = true;
        var reports = new List<string>(platforms.Length);
        var smgFired = false;
        var smgPlaying = false;
        var smgTailPlaying = false;
        var recordedAk74Ready = false;
        var recordedPlatforms = 0;
        var recordedAllReady = true;
        var recordedLeadReady = true;
        foreach (var sample in platforms)
        {
            var platform = sample.Platform;
            var build = WeaponCatalog.Build(platform, 0);
            _player.GrantFireablePrimaryForDiagnostics(build);
            await WaitFrames(2);
            var ready = _player.PlayerWeaponAudioReadyForDiagnostics;
            var recorded =
                SoundLab.RecordedWeaponShotReadyForDiagnostics(
                    platform,
                    distant: false,
                    nearField: true)
                && SoundLab.RecordedWeaponShotReadyForDiagnostics(
                    platform,
                    distant: false,
                    nearField: false)
                && SoundLab.RecordedWeaponShotReadyForDiagnostics(
                    platform,
                    distant: true,
                    nearField: false);
            var leadingSilence = SoundLab.RecordedWeaponLeadingSilenceSecondsForDiagnostics(platform);
            recordedLeadReady &= leadingSilence <= 0.02f;
            recordedPlatforms += recorded ? 1 : 0;
            recordedAllReady &= recorded;
            var signature = _player.PlayerWeaponAudioSignatureForDiagnostics;
            var worldSignature = SoundLab.WeaponShotSignature(build);
            var enemySignature = SoundLab.WeaponShotSignature(build, distant: true);
            var volume = _player.PlayerWeaponAudioVolumeDbForDiagnostics;
            var singlePeak = SoundLab.PlayerWeaponShotEffectivePeak(build);
            var burstPeak = SoundLab.PlayerWeaponShotBurstPeak(
                build,
                build.Stats().FireInterval * 0.65f,
                _player.WeaponAudioVoiceCountForDiagnostics);
            streamCount += ready ? 1 : 0;
            levelsReady &= float.IsFinite(volume)
                && volume >= sample.MinimumLocalVolumeDb;
            if (signature != 0)
            {
                signatures.Add(signature);
            }
            if (worldSignature != 0)
            {
                worldSignatures.Add(worldSignature);
            }
            if (enemySignature != 0)
            {
                enemySignatures.Add(enemySignature);
            }
            nearFieldDistinct &= signature != 0 && signature != worldSignature;
            worldDistinct &= worldSignature != 0 && worldSignature != enemySignature;
            enemyDistinct &= enemySignature != 0;
            headroomReady &= singlePeak is >= 0.30f and <= 0.90f
                && burstPeak <= 0.98f;
            if (platform == WeaponPlatform.M3A1)
            {
                smgFired = _player.FireForDiagnostics();
                // Validate the actual trigger-to-voice handoff immediately.
                // Two rendered world frames can exceed this short report's
                // wall-clock duration on a cold, asset-heavy diagnostic run,
                // which made a successful playback look like a failure after
                // the stream had already completed normally.
                smgPlaying = _player.PlayerWeaponAudioPlayingForDiagnostics;
                await WaitFrames(2);
                smgTailPlaying = _player.PlayerWeaponAudioPlayingForDiagnostics;
            }
            if (platform == WeaponPlatform.AK74)
            {
                recordedAk74Ready =
                    SoundLab.RecordedWeaponShotReadyForDiagnostics(
                        platform,
                        distant: false,
                        nearField: true)
                    && SoundLab.RecordedWeaponShotReadyForDiagnostics(
                        platform,
                        distant: false,
                        nearField: false)
                    && SoundLab.RecordedWeaponShotReadyForDiagnostics(
                        platform,
                        distant: true,
                        nearField: false);
            }
            reports.Add(
                $"{platform}:{ready}:{volume:0.0}:"
                + $"minimum={sample.MinimumLocalVolumeDb:0.0}:"
                + $"single_peak={singlePeak:F3}:burst_peak={burstPeak:F3}:"
                + $"local={signature}:world={worldSignature}:recorded={recorded}:lead={leadingSilence:0.000}");
        }

        var localPlayback = _player.PlayerWeaponAudioIsLocalForDiagnostics;
        var valid = localPlayback
            && streamCount == platforms.Length
            && signatures.Count == platforms.Length
            && worldSignatures.Count == platforms.Length
            && enemySignatures.Count == platforms.Length
            && levelsReady
            && nearFieldDistinct
            && worldDistinct
            && enemyDistinct
            && headroomReady
            && smgFired
            && smgPlaying
            && recordedAk74Ready
            && recordedAllReady
            && recordedLeadReady;
        GD.Print(
            $"WEAPON_AUDIO_CHECK valid={valid} local={localPlayback} "
            + $"streams={streamCount}/{platforms.Length} "
            + $"signatures={signatures.Count}/{platforms.Length} levels={levelsReady} "
            + $"world_signatures={worldSignatures.Count}/{platforms.Length} "
            + $"enemy_signatures={enemySignatures.Count}/{platforms.Length} "
            + $"near_field_distinct={nearFieldDistinct} world_vs_enemy_distinct={worldDistinct} "
            + $"enemy_distinct={enemyDistinct} "
            + $"headroom={headroomReady} "
            + $"smg_fired={smgFired} smg_playing={smgPlaying} "
            + $"smg_tail_playing={smgTailPlaying} "
            + $"recorded_ak74={recordedAk74Ready} "
            + $"recorded_platforms={recordedPlatforms}/{platforms.Length} "
            + $"recorded_lead={recordedLeadReady} "
            + $"reports={string.Join(',', reports)}");
        GD.Print($"WEAPON_AUDIO_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateWeaponImpact()
    {
        DisableActorsForSurvivalDiagnostics();
        ParkWeaponImpactCollidersForDiagnostics();
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 40.0f);
        _player.Velocity = Vector3.Zero;
        _player.FaceWorldPointForDiagnostics(new Vector3(0.0f, 0.2f, -40.0f));
        await WaitFrames(6);

        var samples = new[]
        {
            (Platform: WeaponPlatform.MP5A5, MinimumKickback: 0.055f, MinimumCameraPitch: 0.009f),
            (Platform: WeaponPlatform.M4A1, MinimumKickback: 0.073f, MinimumCameraPitch: 0.012f),
            (Platform: WeaponPlatform.AK74, MinimumKickback: 0.081f, MinimumCameraPitch: 0.014f),
            (Platform: WeaponPlatform.P226, MinimumKickback: 0.055f, MinimumCameraPitch: 0.009f),
            (Platform: WeaponPlatform.M1911, MinimumKickback: 0.065f, MinimumCameraPitch: 0.011f),
            (Platform: WeaponPlatform.GSh18, MinimumKickback: 0.055f, MinimumCameraPitch: 0.009f),
            (Platform: WeaponPlatform.DesertEagle, MinimumKickback: 0.090f, MinimumCameraPitch: 0.018f),
            (Platform: WeaponPlatform.AWM, MinimumKickback: 0.117f, MinimumCameraPitch: 0.033f)
        };
        var valid = true;
        var reports = new List<string>(samples.Length);
        foreach (var sample in samples)
        {
            _player.GrantFireablePrimaryForDiagnostics(
                WeaponCatalog.Build(sample.Platform, 0));
            await WaitFrames(8);
            _player.ResetWeaponFeedbackForDiagnostics();
            _hud.ResetCrosshairShotFeedbackForDiagnostics();
            var idle = _player.InspectViewmodelShotImpulseForDiagnostics();
            var idleCrosshair = _hud.InspectCrosshairShotFeedbackForDiagnostics();
            _player.SeedWeaponFeedbackForDiagnostics(0x51E321UL);
            var fired = _player.FireForDiagnostics();
            var impact = _player.InspectViewmodelShotImpulseForDiagnostics();
            var crosshairImpact = _hud.InspectCrosshairShotFeedbackForDiagnostics();
            var audioPlaying = _player.PlayerWeaponAudioPlayingForDiagnostics;
            var idleValid = idle.Kickback <= 0.001f
                && idleCrosshair.Available
                && idleCrosshair.Offset.Length() <= 0.01f
                && idleCrosshair.Scale.DistanceTo(Vector2.One) <= 0.01f;
            var sidearmView = WeaponCatalog.IsSidearm(sample.Platform);
            var rightBiasedViewValid = idle.ViewPosition.X >= 0.295f
                && idle.ViewPosition.X <= 0.39f
                && (sidearmView
                    ? idle.ViewPosition.Z is >= -0.59f and <= -0.52f
                    : idle.ViewPosition.Z is >= -0.70f and <= -0.59f)
                && idle.ViewRotation.Y >= 0.04f;
            var weaponImpulseValid = impact.Kickback >= sample.MinimumKickback
                && impact.Pitch <= -0.018f
                && Mathf.Abs(impact.Side) <= 0.0651f
                && Mathf.Abs(impact.Roll) <= 0.0901f;
            var cameraImpulseValid = impact.CameraPitch <= -sample.MinimumCameraPitch
                && Mathf.Abs(impact.CameraSide) <= 0.0951f
                && impact.CameraPitch >= -0.12f;
            var muzzleValid = impact.MuzzleBloomVisible
                && impact.MuzzleLightEnergy >= 9.0f
                && audioPlaying;
            var crosshairValid = crosshairImpact.Available
                && crosshairImpact.Offset.Length() >= 2.3f
                && crosshairImpact.Scale.X >= 1.75f;
            var immediateComponentsValid = fired
                && idleValid
                && rightBiasedViewValid
                && weaponImpulseValid
                && cameraImpulseValid
                && muzzleValid
                && crosshairValid;
            for (var presentationFrame = 0; presentationFrame < 2; presentationFrame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            var presented = _player.InspectViewmodelShotImpulseForDiagnostics();
            var horizontalFeedbackValid = Mathf.Abs(impact.CameraSide) >= 0.006f
                && Mathf.Abs(presented.HeadRotation.Y) >= 0.0015f
                && Mathf.Abs(crosshairImpact.Offset.X) >= 0.7f
                && Mathf.Abs(crosshairImpact.Rotation) >= 0.012f;
            var immediateValid = immediateComponentsValid && horizontalFeedbackValid;
            valid &= immediateValid;

            if (sample.Platform == WeaponPlatform.AK74)
            {
                SaveViewportImage("res://weapon_ak74_fire_validation.png");
            }
            for (var frame = 0; frame < 30; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            var recovered = _player.InspectViewmodelShotImpulseForDiagnostics();
            var recoveredCrosshair = _hud.InspectCrosshairShotFeedbackForDiagnostics();
            var recoveredValid = recovered.Kickback <= impact.Kickback * 0.08f
                && Mathf.Abs(recovered.Pitch) <= Mathf.Abs(impact.Pitch) * 0.08f
                && recoveredCrosshair.Available
                && recoveredCrosshair.Offset.Length() <= 0.15f
                && recoveredCrosshair.Scale.DistanceTo(Vector2.One) <= 0.015f
                && Mathf.Abs(recoveredCrosshair.Rotation) <= 0.005f
                && Mathf.Abs(recovered.CameraPitch) <= Mathf.Abs(impact.CameraPitch) * 0.03f
                && Mathf.Abs(recovered.CameraSide) <= Mathf.Abs(impact.CameraSide) * 0.03f
                && Mathf.Abs(recovered.HeadRotation.Y) <= 0.0005f;
            valid &= recoveredValid;
            reports.Add(
                $"{sample.Platform}:immediate={immediateValid}:recovered={recoveredValid}"
                + $":idle={idleValid}:right_view={rightBiasedViewValid}"
                + $":weapon_impulse={weaponImpulseValid}:camera_impulse={cameraImpulseValid}"
                + $":horizontal={horizontalFeedbackValid}"
                + $":muzzle={muzzleValid}:crosshair={crosshairValid}"
                + $":kick={impact.Kickback:F4}:pitch={impact.Pitch:F4}"
                + $":roll={impact.Roll:F4}:side={impact.Side:F4}"
                + $":camera_pitch={impact.CameraPitch:F4}"
                + $":camera_side={impact.CameraSide:F4}"
                + $":head_yaw={presented.HeadRotation.Y:F4}"
                + $":view=({idle.ViewPosition.X:F3},{idle.ViewPosition.Y:F3},{idle.ViewPosition.Z:F3})"
                + $":yaw={idle.ViewRotation.Y:F4}"
                + $":idle_crosshair_offset={idleCrosshair.Offset.Length():F3}"
                + $":idle_crosshair_scale={idleCrosshair.Scale.X:F3}"
                + $":crosshair_offset={crosshairImpact.Offset.Length():F3}"
                + $":crosshair_x={crosshairImpact.Offset.X:F3}"
                + $":crosshair_rotation={crosshairImpact.Rotation:F4}"
                + $":crosshair_scale={crosshairImpact.Scale.X:F3}"
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

    private void ParkWeaponImpactCollidersForDiagnostics()
    {
        var parkedIndex = 0;
        void Park(Node3D actor)
        {
            if (!IsInstanceValid(actor))
            {
                return;
            }
            actor.ProcessMode = ProcessModeEnum.Disabled;
            actor.GlobalPosition = new Vector3(
                240.0f + parkedIndex * 3.0f,
                80.0f,
                240.0f);
            parkedIndex++;
        }

        foreach (var enemy in _enemies)
        {
            Park(enemy);
        }
        foreach (var mate in _squadMates)
        {
            Park(mate);
        }
        foreach (var civilian in _civilians)
        {
            Park(civilian);
        }
        foreach (var barrel in _barrels)
        {
            Park(barrel);
        }
        foreach (var vehicle in _vehicles)
        {
            Park(vehicle);
        }
        if (IsInstanceValid(_aircraft))
        {
            Park(_aircraft!);
        }
    }
}
