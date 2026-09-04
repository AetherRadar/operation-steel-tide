using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateTrainingRange()
    {
        await WaitFrames(18);
        var started = _trainingRangeActive
            && _hud.IsGameplayHudVisible
            && !_hud.IsOperationsOfficeVisible
            && TrainingRangeBotCount == 6
            && _player.HasActiveFirearm;
        var initialWeapon = _player.TrainingRangeWeaponPlatform;
        var initialAmmo = _player.Ammo;
        var infiniteAmmo = _player.ReserveAmmo >= 9999 && initialAmmo == _player.CurrentWeaponStats.MagazineSize;

        _player.CycleTrainingRangeWeapon();
        var weaponCycle = _player.TrainingRangeWeaponPlatform != initialWeapon
            && _player.Ammo == _player.CurrentWeaponStats.MagazineSize;

        var target = _trainingRangeBotSlots[0].Bot;
        var targetHit = target is not null
            && target.TakeDamage(10000.0f, target.GlobalPosition + Vector3.Up * 1.4f, _player);
        await WaitFrames(120);
        var respawned = targetHit && TrainingRangeBotCount == 6 && TrainingRangeKills == 1;
        var valid = started && infiniteAmmo && weaponCycle && respawned;
        GD.Print($"TRAINING_RANGE_CHECK valid={valid} started={started} weapons={_player.TrainingRangeWeaponCount} infinite_ammo={infiniteAmmo} weapon_cycle={weaponCycle} target_hit={targetHit} respawned={respawned} bots={TrainingRangeBotCount} kills={TrainingRangeKills}");
        GD.Print($"TRAINING_RANGE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
