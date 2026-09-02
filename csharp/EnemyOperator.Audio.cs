using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    /// <summary>
    /// Enemy reports use the equipped platform recipe instead of a single fallback
    /// M4 sample.  The distant mix keeps the report readable while preserving the
    /// weapon's platform, suppression, and sound-radius identity.
    /// </summary>
    private void RefreshShotAudio()
    {
        if (_shotAudio is null || !IsInstanceValid(_shotAudio))
        {
            return;
        }

        _shotAudio.Stream = SoundLab.EnemyShot(CarriedWeapon);
        _shotAudio.VolumeDb = SoundLab.WeaponShotVolumeDb(CarriedWeapon, distant: true);
        _shotAudio.MaxDistance = Mathf.Max(
            90.0f,
            CarriedWeapon.Stats().SoundRadius * 1.9f);
        _shotAudio.UnitSize = 12.0f;
    }

    internal int ShotAudioSignatureForDiagnostics
        => _shotAudio is not null && IsInstanceValid(_shotAudio)
            ? SoundLab.WeaponShotSignature(CarriedWeapon, distant: true)
            : 0;

    internal float ShotAudioVolumeDbForDiagnostics
        => _shotAudio is not null && IsInstanceValid(_shotAudio)
            ? _shotAudio.VolumeDb
            : float.NegativeInfinity;

    internal bool ShotAudioReadyForDiagnostics
        => _shotAudio is not null
            && IsInstanceValid(_shotAudio)
            && _shotAudio.Stream is AudioStreamWav { Data.Length: > 12000 };
}
