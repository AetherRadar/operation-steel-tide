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

        if (!TrySelectGroundedNavigationRecoveryDirection(forward, 3.2f, out var recovery))
        {
            return;
        }
        var left = new Vector3(-forward.Z, 0.0f, forward.X);
        _combatStrafeSign = recovery.Dot(left) >= 0.0f ? 1.0f : -1.0f;
        _combatRecoveryDirection = recovery;
        _combatRecoveryTimer = 1.15f;
    }

    private bool TrySelectGroundedNavigationRecoveryDirection(
        Vector3 forward,
        float travelDistance,
        out Vector3 recovery)
    {
        recovery = Vector3.Zero;
        forward.Y = 0.0f;
        if (forward.LengthSquared() <= 0.01f)
        {
            return false;
        }
        forward = forward.Normalized();
        var left = new Vector3(-forward.Z, 0.0f, forward.X);
        var candidates = new[]
        {
            (left * 0.82f - forward * 0.36f).Normalized(),
            (-left * 0.82f - forward * 0.36f).Normalized(),
            -forward
        };
        var bestScore = float.NegativeInfinity;
        foreach (var candidate in candidates)
        {
            var clearance = MeasureMovementClearance(candidate, travelDistance + 0.25f);
            var supportedDistance = Mathf.Min(travelDistance, Mathf.Max(0.0f, clearance - 0.12f));
            if (supportedDistance < Mathf.Min(0.7f, travelDistance * 0.65f)
                || !HasNavigationRecoveryGroundPath(candidate, supportedDistance))
            {
                continue;
            }
            if (clearance > bestScore)
            {
                bestScore = clearance;
                recovery = candidate;
            }
        }
        return recovery.LengthSquared() > 0.01f;
    }

    private bool HasNavigationRecoveryGroundPath(Vector3 direction, float distance)
    {
        var exclude = BuildNavigationStepExclusions();
        using var excludeBacking = exclude.AsDisposable();
        var samples = Mathf.Max(2, Mathf.CeilToInt(distance / 0.55f));
        for (var sample = 1; sample <= samples; sample++)
        {
            var point = GlobalPosition + direction * (distance * sample / samples);
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    point + Vector3.Up * 0.65f,
                    point + Vector3.Down * 0.9f,
                    exclude,
                    1,
                    out var hit)
                || hit.Normal.Dot(Vector3.Up) < 0.72f
                || Mathf.Abs(hit.Position.Y - GlobalPosition.Y) > 0.68f)
            {
                return false;
            }
        }
        return true;
    }

    internal void RequestRequiredStepRecovery()
    {
        var forward = _combatPathDirection.LengthSquared() > 0.01f
            ? _combatPathDirection
            : _lastStairNavigationDirection.LengthSquared() > 0.01f
                ? _lastStairNavigationDirection
                : _combatDesiredDirection.LengthSquared() > 0.01f
                    ? _combatDesiredDirection
                    : -GlobalBasis.Z;
        forward.Y = 0.0f;
        if (forward.LengthSquared() <= 0.01f)
        {
            forward = Vector3.Forward;
        }
        forward = forward.Normalized();

        ResetMovementProgress();
        if (!TrySelectGroundedNavigationRecoveryDirection(forward, 1.15f, out var escape))
        {
            return;
        }
        var left = new Vector3(-forward.Z, 0.0f, forward.X);
        _combatStrafeSign = escape.Dot(left) >= 0.0f ? 1.0f : -1.0f;
        _combatRecoveryDirection = escape;
        _combatRecoveryTimer = RequiredStepRecoveryDuration;
        _requiredStepRecoveryActive = true;
        var velocity = Velocity;
        velocity.X *= 0.15f;
        velocity.Z *= 0.15f;
        Velocity = velocity;
        RequiredStepRecoveriesForDiagnostics++;
    }
}
