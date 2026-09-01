using Godot;

namespace OperationSteelTide;

internal readonly record struct EnemyIncendiaryAvoidanceDiagnosticState(
    bool Active,
    bool HazardDetected,
    float HoldRemaining,
    Vector3 EscapeDirection,
    float HorizontalSpeed,
    int EscapeMovementFrames);

public partial class EnemyOperator
{
    private const float IncendiaryEscapeHoldSeconds = 1.15f;
    private const float IncendiaryEscapeAcceleration = 28.0f;
    private const float IncendiaryImmediateResponse = 0.72f;

    private bool _incendiaryAvoidanceActive;
    private bool _incendiaryHazardDetected;
    private float _incendiaryAvoidanceHoldRemaining;
    private Vector3 _incendiaryEscapeDirection;
    private int _incendiaryEscapeMovementFrames;

    internal bool IsAvoidingIncendiaryFireForDiagnostics
        => _incendiaryAvoidanceActive;

    internal EnemyIncendiaryAvoidanceDiagnosticState CaptureIncendiaryAvoidanceForDiagnostics()
        => new(
            _incendiaryAvoidanceActive,
            _incendiaryHazardDetected,
            _incendiaryAvoidanceHoldRemaining,
            _incendiaryEscapeDirection,
            new Vector2(Velocity.X, Velocity.Z).Length(),
            _incendiaryEscapeMovementFrames);

    internal bool ApplyIncendiaryAvoidanceForDiagnostics(float delta)
        => ApplyIncendiaryAvoidance(Mathf.Max(0.0f, delta));

    internal bool ApplyIncendiaryAvoidanceDecisionForDiagnostics(
        float delta,
        bool hazardDetected,
        Vector3 escapeDirection)
        => ApplyIncendiaryAvoidanceDecision(
            Mathf.Max(0.0f, delta),
            hazardDetected,
            escapeDirection,
            applyObstacleAvoidance: false);

    private bool ApplyIncendiaryAvoidance(float delta)
    {
        if (IsDead || Main is null || !IsInstanceValid(Main))
        {
            ResetIncendiaryAvoidance();
            return false;
        }

        var fallbackDirection = ResolveIncendiaryEscapeFallback();
        var hazardDetected = Main.TryGetIncendiaryEscapeDirection(
            GlobalPosition + Vector3.Up * 0.12f,
            fallbackDirection,
            out var escapeDirection);
        return ApplyIncendiaryAvoidanceDecision(
            delta,
            hazardDetected,
            escapeDirection,
            applyObstacleAvoidance: true);
    }

    private bool ApplyIncendiaryAvoidanceDecision(
        float delta,
        bool hazardDetected,
        Vector3 escapeDirection,
        bool applyObstacleAvoidance)
    {
        _incendiaryHazardDetected = hazardDetected;
        escapeDirection.Y = 0.0f;
        if (hazardDetected)
        {
            if (escapeDirection.LengthSquared() <= 0.001f)
            {
                escapeDirection = ResolveIncendiaryEscapeFallback();
            }
            _incendiaryEscapeDirection = escapeDirection.Normalized();
            _incendiaryAvoidanceHoldRemaining = IncendiaryEscapeHoldSeconds;
            _incendiaryAvoidanceActive = true;
        }
        else if (_incendiaryAvoidanceActive)
        {
            _incendiaryAvoidanceHoldRemaining = Mathf.Max(
                0.0f,
                _incendiaryAvoidanceHoldRemaining - delta);
            if (_incendiaryAvoidanceHoldRemaining <= 0.0f)
            {
                _incendiaryAvoidanceActive = false;
                _incendiaryEscapeDirection = Vector3.Zero;
            }
        }

        if (!_incendiaryAvoidanceActive)
        {
            return false;
        }

        var direction = _incendiaryEscapeDirection;
        if (applyObstacleAvoidance)
        {
            var obstacleAdjusted = ApplyPursuitObstacleAvoidance(direction);
            if (obstacleAdjusted.LengthSquared() > 0.001f)
            {
                // Keep most of the world-resolved fire-safe direction so steering around
                // a wall cannot turn the operator straight into an overlapping fire pool.
                direction = direction.Lerp(obstacleAdjusted.Normalized(), 0.42f).Normalized();
            }
        }
        _incendiaryEscapeDirection = direction;

        // Survival movement owns only velocity and stance. Aiming and weapon cadence
        // remain untouched, so an operator can continue returning fire while escaping.
        _ = TryStandForCombatMovement();
        _combatStanceHoldRemaining = 0.0f;
        _combatStanceCooldown = Mathf.Max(_combatStanceCooldown, 0.85f);
        _proneTimer = 0.0f;

        var speed = IsWorldBoss
            ? 7.2f * WorldBossMoveMultiplier
            : IsProne ? 3.8f : IsCrouched ? 4.8f : 6.8f;
        var current = new Vector3(Velocity.X, 0.0f, Velocity.Z);
        var desired = direction * speed;
        if (hazardDetected)
        {
            // Fire contact must produce visible displacement on the first physics frame.
            current = current.Lerp(desired, IncendiaryImmediateResponse);
        }
        else
        {
            current.X = Mathf.MoveToward(
                current.X,
                desired.X,
                delta * IncendiaryEscapeAcceleration);
            current.Z = Mathf.MoveToward(
                current.Z,
                desired.Z,
                delta * IncendiaryEscapeAcceleration);
        }

        var velocity = Velocity;
        velocity.X = current.X;
        velocity.Z = current.Z;
        Velocity = velocity;
        _stationaryMoveTimer = 0.0f;
        _incendiaryEscapeMovementFrames++;
        return true;
    }

    private Vector3 ResolveIncendiaryEscapeFallback()
    {
        if (_incendiaryAvoidanceActive
            && _incendiaryEscapeDirection.LengthSquared() > 0.001f)
        {
            return _incendiaryEscapeDirection;
        }

        var horizontalVelocity = new Vector3(Velocity.X, 0.0f, Velocity.Z);
        if (horizontalVelocity.LengthSquared() > 0.16f)
        {
            return horizontalVelocity.Normalized();
        }

        var target = EngageTargetNode;
        if (target is not null && IsInstanceValid(target))
        {
            var awayFromTarget = GlobalPosition - target.GlobalPosition;
            awayFromTarget.Y = 0.0f;
            if (awayFromTarget.LengthSquared() > 0.001f)
            {
                return awayFromTarget.Normalized();
            }
        }

        var facingFallback = -GlobalBasis.Z;
        facingFallback.Y = 0.0f;
        return facingFallback.LengthSquared() > 0.001f
            ? facingFallback.Normalized()
            : Vector3.Forward;
    }

    private void ResetIncendiaryAvoidance()
    {
        _incendiaryAvoidanceActive = false;
        _incendiaryHazardDetected = false;
        _incendiaryAvoidanceHoldRemaining = 0.0f;
        _incendiaryEscapeDirection = Vector3.Zero;
        _incendiaryEscapeMovementFrames = 0;
    }
}
