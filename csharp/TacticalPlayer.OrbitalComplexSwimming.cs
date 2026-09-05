using Godot;

namespace OperationSteelTide;

/// <summary>Map-specific traversal for the flooded Falltide reactor pit.</summary>
public partial class TacticalPlayer
{
    private const float OrbitalComplexSwimSpeed = 3.45f;
    private const float OrbitalComplexSwimRiseSpeed = 3.6f;
    private const float OrbitalComplexSwimSinkSpeed = 2.8f;
    private const float OrbitalComplexSwimMinRootY = -32.2f;
    private const float OrbitalComplexSwimMaxRootY = -27.65f;
    private bool _orbitalComplexSwimmingDiagnosticSink;

    internal void PrepareOrbitalComplexSwimmingDiagnostics()
    {
        _movementInputArmed = true;
        _movementReleaseTime = 0.25f;
        Velocity = Vector3.Zero;
    }

    internal void SetOrbitalComplexSwimmingDiagnosticSink(bool sinking)
        => _orbitalComplexSwimmingDiagnosticSink = sinking;

    internal bool OrbitalComplexSwimmingDiagnosticSink
        => _orbitalComplexSwimmingDiagnosticSink;


    private bool TryUpdateOrbitalComplexSwimming(float delta)
    {
        if (Main is null
            || !Main.IsOrbitalComplexRuntimeReady
            || !OrbitalComplexMapDefinition.IsInBlackwaterSwimVolume(GlobalPosition)
            || IsDead
            || IsInVehicle
            || _isClimbingLadder
            || _isVaulting)
        {
            return false;
        }

        var input = UiLocked
            ? Vector2.Zero
            : Input.GetVector(
                GameInputActions.MoveLeft,
                GameInputActions.MoveRight,
                GameInputActions.MoveForward,
                GameInputActions.MoveBackward);
        if (!_movementInputArmed)
        {
            if (input.LengthSquared() > 0.001f)
            {
                _movementReleaseTime = 0.0f;
            }
            else
            {
                _movementReleaseTime += delta;
                _movementInputArmed = _movementReleaseTime >= 0.2f;
            }
            input = Vector2.Zero;
        }
        var direction = (Transform.Basis * new Vector3(input.X, 0.0f, input.Y)).Normalized();
        var predicted = GlobalPosition + direction * OrbitalComplexSwimSpeed * delta;
        var atSurface = GlobalPosition.Y > OrbitalComplexMapDefinition.BlackwaterSurfaceY + 0.2f;
        if (!OrbitalComplexMapDefinition.IsInsideBlackwaterFootprint(predicted))
        {
            if (atSurface)
            {
                // Let the normal floor solver take over once the swimmer reaches
                // the dry rim.  Underwater movement is held inside the basin.
                return false;
            }
            direction = Vector3.Zero;
        }
        var horizontal = new Vector2(Velocity.X, Velocity.Z);
        var targetHorizontal = new Vector2(direction.X, direction.Z)
            * (Input.IsActionPressed(GameInputActions.Sprint)
                ? OrbitalComplexSwimSpeed * 1.18f
                : OrbitalComplexSwimSpeed);
        horizontal = horizontal.MoveToward(targetHorizontal, delta * 9.0f);

        var verticalTarget = 0.0f;
        if (_orbitalComplexSwimmingDiagnosticSink
            || Input.IsActionPressed(GameInputActions.Crouch))
        {
            verticalTarget = -OrbitalComplexSwimSinkSpeed;
        }
        else if (Input.IsActionPressed(GameInputActions.Jump))
        {
            verticalTarget = OrbitalComplexSwimRiseSpeed;
        }
        else if (GlobalPosition.Y < OrbitalComplexMapDefinition.BlackwaterSurfaceY - 1.0f)
        {
            verticalTarget = OrbitalComplexSwimRiseSpeed * 0.42f;
        }

        var swimVerticalVelocity = Mathf.MoveToward(
            Velocity.Y, verticalTarget, delta * 8.0f);
        var swimStartY = GlobalPosition.Y;
        Velocity = new Vector3(
            horizontal.X,
            0.0f,
            horizontal.Y);
        var previousFloorSnap = FloorSnapLength;
        FloorSnapLength = 0.0f;
        MoveAndSlide();
        FloorSnapLength = previousFloorSnap;
        // Water has no walkable floor. Apply the buoyancy step after the
        // horizontal slide so imported pool meshes or rim triangles cannot
        // snap the capsule back to a dry surface.
        var clampedY = Mathf.Clamp(
            swimStartY + swimVerticalVelocity * delta,
            OrbitalComplexSwimMinRootY,
            OrbitalComplexSwimMaxRootY);
        if (!Mathf.IsEqualApprox(clampedY, GlobalPosition.Y))
        {
            GlobalPosition = new Vector3(GlobalPosition.X, clampedY, GlobalPosition.Z);
        }
        var finalVelocity = Velocity;
        finalVelocity.Y = swimVerticalVelocity;
        Velocity = finalVelocity;

        HasMovementIntent = _movementInputArmed && input.LengthSquared() > 0.001f;
        _isAiming = false;
        Hud?.SetAiming(false);
        Hud?.SetInteraction(
            GameLocalization.Get(
                "falltide_blackwater_swim",
                Hud?.CurrentLanguage ?? "en",
                "BLACKWATER  //  SPACE RISE  //  CTRL SINK"),
            -1.0f,
            true);
        return true;
    }
}
