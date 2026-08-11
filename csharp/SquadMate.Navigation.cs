using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    internal void RequestNavigationRecovery()
    {
        _combatStrafeSign *= -1.0f;
        _combatAvoidanceTimer = 0.0f;
        _combatRecoveryTimer = 0.0f;
        var velocity = Velocity;
        velocity.X *= 0.25f;
        velocity.Z *= 0.25f;
        Velocity = velocity;
        ResetMovementProgress();
    }
}
