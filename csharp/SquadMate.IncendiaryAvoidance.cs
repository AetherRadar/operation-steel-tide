using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const float IncendiaryApproachPredictionSeconds = 0.75f;
    private const float IncendiaryMinimumPredictionDistance = 1.25f;
    private const float IncendiaryMaximumPredictionDistance = 4.0f;
    private const float IncendiaryEscapeSpeed = 6.4f;
    private const float IncendiaryEscapeAcceleration = 28.0f;
    private const float IncendiaryEscapeReleaseSeconds = 0.22f;

    private float _incendiaryEscapeReleaseTimer;
    private Vector3 _incendiaryEscapeDirection;

    internal bool IsAvoidingIncendiaryForDiagnostics { get; private set; }
    internal bool IsAvoidingPredictedIncendiaryForDiagnostics { get; private set; }
    internal Vector3 IncendiaryEscapeDirectionForDiagnostics => _incendiaryEscapeDirection;
    internal int IncendiaryAvoidanceFramesForDiagnostics { get; private set; }

    private bool TryUpdateIncendiaryAvoidance(float delta)
    {
        if (Main is null || !IsInstanceValid(Main) || IsDowned || IsBodyBag || IsNetworkProxy)
        {
            ResetIncendiaryAvoidance();
            return false;
        }
        if (_navigationTraversalActive && !IsOnFloor())
        {
            // A vault, drop or ladder owns the body transform while airborne.
            // Finish that authored motion instead of cancelling into a fall; the
            // next grounded physics frame re-runs this check before normal AI.
            ResetIncendiaryAvoidance();
            return false;
        }

        var horizontalVelocity = Velocity;
        horizontalVelocity.Y = 0.0f;
        var fallbackDirection = horizontalVelocity.LengthSquared() > 0.04f
            ? horizontalVelocity.Normalized()
            : _combatDesiredDirection.LengthSquared() > 0.01f
                ? _combatDesiredDirection.Normalized()
                : -GlobalBasis.Z;
        fallbackDirection.Y = 0.0f;
        if (fallbackDirection.LengthSquared() <= 0.01f)
        {
            fallbackDirection = Vector3.Forward;
        }
        fallbackDirection = fallbackDirection.Normalized();

        var threatenedHere = Main.TryGetIncendiaryEscapeDirection(
            GlobalPosition,
            fallbackDirection,
            out var escapeDirection);
        var threatenedAhead = false;
        if (!threatenedHere && horizontalVelocity.LengthSquared() > 0.16f)
        {
            var predictionDistance = Mathf.Clamp(
                horizontalVelocity.Length() * IncendiaryApproachPredictionSeconds,
                IncendiaryMinimumPredictionDistance,
                IncendiaryMaximumPredictionDistance);
            threatenedAhead = Main.TryGetIncendiaryEscapeDirection(
                GlobalPosition + fallbackDirection * predictionDistance,
                fallbackDirection,
                out escapeDirection);
        }

        if (threatenedHere || threatenedAhead)
        {
            escapeDirection.Y = 0.0f;
            if (escapeDirection.LengthSquared() > 0.01f)
            {
                _incendiaryEscapeDirection = escapeDirection.Normalized();
            }
            _incendiaryEscapeReleaseTimer = IncendiaryEscapeReleaseSeconds;
            IsAvoidingPredictedIncendiaryForDiagnostics = threatenedAhead && !threatenedHere;
        }
        else
        {
            _incendiaryEscapeReleaseTimer = Mathf.Max(
                0.0f,
                _incendiaryEscapeReleaseTimer - Mathf.Max(0.0f, delta));
            IsAvoidingPredictedIncendiaryForDiagnostics = false;
            if (_incendiaryEscapeReleaseTimer <= 0.0f)
            {
                ResetIncendiaryAvoidance();
                return false;
            }
        }

        if (_incendiaryEscapeDirection.LengthSquared() <= 0.01f)
        {
            ResetIncendiaryAvoidance();
            return false;
        }

        // Fire egress pre-empts only the current locomotion. Revive assignments,
        // squad orders, combat targets and demolition routes remain intact and
        // resume as soon as the mate clears the flames.
        CancelNavigationTraversal();
        ResetMovementProgress();
        _doorWaitTimer = 0.0f;
        _followFormationSettled = false;
        var obstacleDirection = AvoidObstacle(_incendiaryEscapeDirection);
        var movementDirection = (
            _incendiaryEscapeDirection * 0.6f
            + obstacleDirection * 0.4f).Normalized();
        _combatMoveRequested = true;
        _combatDesiredDirection = movementDirection;
        _combatPathDirection = movementDirection;

        var speed = IncendiaryEscapeSpeed * OperatorRoles.Spec(Role).MovementMultiplier;
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(
            velocity.X,
            movementDirection.X * speed,
            delta * IncendiaryEscapeAcceleration);
        velocity.Z = Mathf.MoveToward(
            velocity.Z,
            movementDirection.Z * speed,
            delta * IncendiaryEscapeAcceleration);
        velocity.Y = IsOnFloor() ? -0.2f : velocity.Y - 22.0f * delta;
        Velocity = velocity;

        FaceTacticalPoint(GlobalPosition + movementDirection, delta);
        MoveAndSlide();
        BreakableGlassField.TryShatterMovementBlockerFromCollisions(this);
        TryNavigationStepUp(
            movementDirection,
            GlobalPosition + movementDirection * 2.0f);

        IsAvoidingIncendiaryForDiagnostics = true;
        IncendiaryAvoidanceFramesForDiagnostics++;
        return true;
    }

    internal bool UpdateIncendiaryAvoidanceForDiagnostics(float delta)
        => TryUpdateIncendiaryAvoidance(delta);

    internal void ResetIncendiaryAvoidanceForDiagnostics()
    {
        ResetIncendiaryAvoidance();
        IncendiaryAvoidanceFramesForDiagnostics = 0;
    }

    private void ResetIncendiaryAvoidance()
    {
        _incendiaryEscapeReleaseTimer = 0.0f;
        _incendiaryEscapeDirection = Vector3.Zero;
        IsAvoidingIncendiaryForDiagnostics = false;
        IsAvoidingPredictedIncendiaryForDiagnostics = false;
    }
}
