using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    internal void RequestNavigationRecovery(bool forceEscape = false)
    {
        var forward = _combatDesiredDirection.LengthSquared() > 0.01f
            ? _combatDesiredDirection.Normalized()
            : -GlobalBasis.Z;
        ResetMovementProgress();
        _combatStrafeSign *= -1.0f;
        var velocity = Velocity;
        velocity.X *= 0.25f;
        velocity.Z *= 0.25f;
        Velocity = velocity;
        if (!forceEscape)
        {
            return;
        }

        var left = new Vector3(-forward.Z, 0.0f, forward.X);
        var leftClearance = MeasureMovementClearance(left, 2.2f);
        var rightClearance = MeasureMovementClearance(-left, 2.2f);
        _combatStrafeSign = leftClearance >= rightClearance ? 1.0f : -1.0f;
        var side = left * _combatStrafeSign;
        _combatRecoveryDirection = (side - forward * 0.2f).Normalized();
        _combatRecoveryTimer = 1.15f;
    }
}
