using System;

namespace OperationSteelTide;

internal sealed class WeaponCycleInputGate
{
    internal const float DebounceSeconds = 0.13f;

    private float _remainingDebounce;

    internal void Advance(float delta)
        => _remainingDebounce = MathF.Max(0.0f, _remainingDebounce - MathF.Max(0.0f, delta));

    internal bool TryConsume(bool firePressed)
    {
        if (firePressed)
        {
            // A wheel pulse can arrive on the same frame as a shot or while the
            // trigger is held. Reject it and absorb the tail of a high-resolution
            // wheel gesture so releasing fire cannot unexpectedly draw melee.
            _remainingDebounce = DebounceSeconds;
            return false;
        }
        if (_remainingDebounce > 0.0f)
        {
            return false;
        }

        _remainingDebounce = DebounceSeconds;
        return true;
    }

    internal void Reset()
        => _remainingDebounce = 0.0f;
}

public partial class TacticalPlayer
{
    private readonly WeaponCycleInputGate _weaponCycleInputGate = new();

    private void AdvanceWeaponCycleInput(float delta)
        => _weaponCycleInputGate.Advance(delta);

    private bool TryAcceptWeaponCycleInput(bool firePressed)
        => _weaponCycleInputGate.TryConsume(firePressed);

    internal float WeaponCycleDebounceSecondsForDiagnostics
        => WeaponCycleInputGate.DebounceSeconds;

    internal void ResetWeaponCycleInputForDiagnostics()
        => _weaponCycleInputGate.Reset();

    internal bool TryCycleWeaponFromInputForDiagnostics(bool firePressed, float elapsed)
    {
        AdvanceWeaponCycleInput(elapsed);
        if (!TryAcceptWeaponCycleInput(firePressed))
        {
            return false;
        }

        CycleWeaponSlots();
        return true;
    }
}
