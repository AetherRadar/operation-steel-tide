using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    internal int BazaarRoutePhysicsStepsForDiagnostics { get; private set; }

    /// <summary>
    /// Applies gravity and the production CharacterBody3D motor after the demolition
    /// objective router has selected velocity for this frame.
    /// </summary>
    internal void StepBazaarRoutePhysicsForDiagnostics(float delta)
    {
        BazaarRoutePhysicsStepsForDiagnostics++;
        var velocity = Velocity;
        velocity.Y = IsOnFloor() ? -0.2f : velocity.Y - 22.0f * delta;
        Velocity = velocity;
        MoveOperator(delta);
    }
}
