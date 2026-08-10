using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public override void _Input(InputEvent @event)
    {
        TryRearmMovementInput(@event);
    }

    private bool TryRearmMovementInput(InputEvent @event)
    {
        if (_movementInputArmed
            || UiLocked
            || IsDead
            || IsInVehicle
            || _isClimbingLadder
            || @event is not InputEventKey { Pressed: true, Echo: false } key
            || !IsMovementKey(key))
        {
            return false;
        }

        RestoreMovementInput();
        return true;
    }

    private static bool IsMovementKey(InputEventKey key)
    {
        return IsMovementKeycode(key.PhysicalKeycode)
            || IsMovementKeycode(key.Keycode);
    }

    private static bool IsMovementKeycode(Key key)
    {
        return key is Key.W
            or Key.A
            or Key.S
            or Key.D
            or Key.Up
            or Key.Down
            or Key.Left
            or Key.Right;
    }

    public bool RearmMovementFromKeyForDiagnostics(Key key, bool uiLocked = false)
    {
        var previousUiLocked = UiLocked;
        UiLocked = uiLocked;
        DisarmMovementInput();
        TryRearmMovementInput(new InputEventKey
        {
            PhysicalKeycode = key,
            Pressed = true
        });
        var rearmed = _movementInputArmed;
        UiLocked = previousUiLocked;
        RestoreMovementInput();
        return rearmed;
    }
}
