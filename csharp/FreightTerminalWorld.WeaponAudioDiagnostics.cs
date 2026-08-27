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
}
