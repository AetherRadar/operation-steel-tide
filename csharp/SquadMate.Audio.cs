using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private WeaponBuild _audioWeapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
    private AudioStreamPlayer3D _shotAudio = null!;

    internal bool ShotAudioReadyForDiagnostics
        => IsInstanceValid(_shotAudio)
        && _shotAudio.Stream is AudioStreamWav { Data.Length: > 12000 };

    internal bool ShotAudioPlayingForDiagnostics
        => IsInstanceValid(_shotAudio) && _shotAudio.Playing;

    internal int ShotAudioSignatureForDiagnostics
        => ShotAudioReadyForDiagnostics ? SoundLab.WeaponShotSignature(_audioWeapon) : 0;

    internal void PlayShotAudioForDiagnostics() => PlayShotAudio();

    private void BuildShotAudio()
    {
        _shotAudio = new AudioStreamPlayer3D
        {
            Name = "SquadShotAudio",
            Stream = SoundLab.WeaponShot(_audioWeapon),
            VolumeDb = SoundLab.WeaponShotVolumeDb(_audioWeapon),
            MaxDistance = _audioWeapon.Stats().SoundRadius * 1.9f,
            UnitSize = 12.0f
        };
        _muzzle.AddChild(_shotAudio);
    }

    private void RefreshShotAudio()
    {
        if (!IsInstanceValid(_shotAudio))
        {
            return;
        }
        _shotAudio.Stream = SoundLab.WeaponShot(_audioWeapon);
        _shotAudio.VolumeDb = SoundLab.WeaponShotVolumeDb(_audioWeapon);
        _shotAudio.MaxDistance = _audioWeapon.Stats().SoundRadius * 1.9f;
    }

    private void PlayShotAudio()
    {
        if (!IsInstanceValid(_shotAudio))
        {
            return;
        }
        _shotAudio.PitchScale = _rng.RandfRange(0.96f, 1.04f);
        _shotAudio.Play();
    }
}
