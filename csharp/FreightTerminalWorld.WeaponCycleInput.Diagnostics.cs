using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateWeaponCycleInput()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }

        _player.ApplyColdStartUnarmed();
        _player.GrantFireablePrimaryForDiagnostics();
        _player.SelectQuickSlot(PlayerQuickSlot.Primary, notify: false);
        _player.ResetWeaponCycleInputForDiagnostics();

        var fireHeldBlocked = !_player.TryCycleWeaponFromInputForDiagnostics(
                firePressed: true,
                elapsed: 0.0f)
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary;
        var gestureTailBlocked = !_player.TryCycleWeaponFromInputForDiagnostics(
                firePressed: false,
                elapsed: 0.01f)
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary;
        var cycleAfterRelease = _player.TryCycleWeaponFromInputForDiagnostics(
                firePressed: false,
                elapsed: _player.WeaponCycleDebounceSecondsForDiagnostics + 0.01f)
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Melee;
        var repeatedPulseBlocked = !_player.TryCycleWeaponFromInputForDiagnostics(
                firePressed: false,
                elapsed: 0.01f)
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Melee;

        var keyFourBound = HasQuickSlotKey("weapon_melee", (Key)52);
        var integratedFireCycleBlocked = false;
        var integratedCycleAfterRelease = false;
        var explicitMeleeWorks = false;
        Input.ActionRelease(GameInputActions.Fire);
        Input.ActionRelease(GameInputActions.WeaponCycle);
        Input.ActionRelease(GameInputActions.WeaponMelee);
        await WaitFrames(2);
        try
        {
            _player.SelectQuickSlot(PlayerQuickSlot.Primary, notify: false);
            _player.ResetWeaponCycleInputForDiagnostics();
            Input.ActionPress(GameInputActions.Fire);
            Input.ActionPress(GameInputActions.WeaponCycle);
            await WaitFrames(2);
            integratedFireCycleBlocked = _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary;

            Input.ActionRelease(GameInputActions.WeaponCycle);
            Input.ActionRelease(GameInputActions.Fire);
            await WaitFrames(12);
            Input.ActionPress(GameInputActions.WeaponCycle);
            await WaitFrames(2);
            integratedCycleAfterRelease = _player.ActiveWeaponSlot == PlayerWeaponSlot.Melee;

            Input.ActionRelease(GameInputActions.WeaponCycle);
            await WaitFrames(2);
            _player.SelectQuickSlot(PlayerQuickSlot.Primary, notify: false);
            Input.ActionPress(GameInputActions.Fire);
            Input.ActionPress(GameInputActions.WeaponMelee);
            await WaitFrames(2);
            explicitMeleeWorks = _player.ActiveWeaponSlot == PlayerWeaponSlot.Melee;
        }
        finally
        {
            Input.ActionRelease(GameInputActions.Fire);
            Input.ActionRelease(GameInputActions.WeaponCycle);
            Input.ActionRelease(GameInputActions.WeaponMelee);
        }

        _player.SelectQuickSlot(PlayerQuickSlot.Primary, notify: false);
        _player.SetMagazineAmmoForDiagnostics(0);
        var emptyTriggerStayedOnFirearm = !_player.FireForDiagnostics()
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary
            && _player.ActiveQuickSlot == PlayerQuickSlot.Primary
            && !_player.KnifeEquipped;

        var valid = fireHeldBlocked
            && gestureTailBlocked
            && cycleAfterRelease
            && repeatedPulseBlocked
            && keyFourBound
            && integratedFireCycleBlocked
            && integratedCycleAfterRelease
            && explicitMeleeWorks
            && emptyTriggerStayedOnFirearm;
        GD.Print(
            $"WEAPON_CYCLE_INPUT_CHECK valid={valid} fire_blocked={fireHeldBlocked} "
            + $"gesture_tail={gestureTailBlocked} release_cycle={cycleAfterRelease} "
            + $"repeated_pulse={repeatedPulseBlocked} key4={keyFourBound} "
            + $"integrated_fire_blocked={integratedFireCycleBlocked} "
            + $"integrated_release_cycle={integratedCycleAfterRelease} "
            + $"explicit_melee={explicitMeleeWorks} empty_stable={emptyTriggerStayedOnFirearm} "
            + $"active={_player.ActiveWeaponSlot}");
        GD.Print($"WEAPON_CYCLE_INPUT_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
