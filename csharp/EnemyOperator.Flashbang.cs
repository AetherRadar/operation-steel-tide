using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private const float FlashVisionSuppressionThreshold = 0.16f;
    private const float FlashFireSuppressionThreshold = 0.68f;

    private float _flashbangRemaining;
    private float _flashbangFadeSeconds;
    private float _flashbangPeakIntensity;
    private Vector3 _flashbangEscapeDirection;

    public bool IsFlashbanged => FlashbangIntensity > 0.02f;
    public float FlashbangRemaining => _flashbangRemaining;
    public Vector3 FlashbangViewOrigin => GlobalPosition + Vector3.Up * CombatEyeHeight;
    public Vector3 FlashbangViewForward => -GlobalBasis.Z;
    public bool CanReceiveFlashbang => !IsDead && IsInsideTree();
    public float FlashbangIntensity
    {
        get
        {
            if (_flashbangRemaining <= 0.0f || _flashbangPeakIntensity <= 0.0f)
            {
                return 0.0f;
            }
            var fade = Mathf.Clamp(
                _flashbangRemaining / Mathf.Max(0.2f, _flashbangFadeSeconds),
                0.0f,
                1.0f);
            return Mathf.Clamp(_flashbangPeakIntensity * fade, 0.0f, 1.0f);
        }
    }

    public void ApplyFlashbang(FlashbangExposure exposure)
        => ApplyFlashbang(
            exposure.Intensity,
            exposure.DurationSeconds,
            GlobalPosition - exposure.SourcePosition);

    /// <summary>
    /// Applies a directional flash response. Direction is expected to point from the
    /// detonation toward this operator, so it can keep moving away while vision recovers.
    /// Intensity is normalized to 0..1; duration is clamped to a bounded gameplay window.
    /// </summary>
    public void ApplyFlashbang(float intensity, float duration, Vector3 direction)
    {
        if (IsDead)
        {
            return;
        }
        intensity = Mathf.Clamp(intensity, 0.0f, 1.0f);
        duration = Mathf.Clamp(duration, 0.0f, 8.0f);
        if (intensity <= 0.01f || duration <= 0.01f)
        {
            return;
        }

        direction.Y = 0.0f;
        if (direction.LengthSquared() <= 0.001f)
        {
            direction = -GlobalBasis.Z;
            direction.Y = 0.0f;
        }
        var currentIntensity = FlashbangIntensity;
        if (intensity >= currentIntensity || _flashbangEscapeDirection.LengthSquared() <= 0.001f)
        {
            _flashbangEscapeDirection = direction.Normalized();
        }

        _flashbangRemaining = Mathf.Max(_flashbangRemaining, duration);
        _flashbangFadeSeconds = Mathf.Max(_flashbangFadeSeconds, duration * 0.68f);
        var fadeFactor = Mathf.Clamp(
            _flashbangRemaining / Mathf.Max(0.2f, _flashbangFadeSeconds),
            0.001f,
            1.0f);
        _flashbangPeakIntensity = Mathf.Max(currentIntensity, intensity) / fadeFactor;
        _fireTimer = Mathf.Max(_fireTimer, 0.24f + intensity * 0.92f);
        RegisterCombatPressure(1.5f + intensity * 1.25f);
        InvalidateLineOfSightCache();

        if (intensity >= 0.72f)
        {
            _ = TryStandForCombatMovement();
        }
    }

    private void UpdateFlashbangTimers(float delta)
    {
        _flashbangRemaining = Mathf.Max(0.0f, _flashbangRemaining - delta);
        if (_flashbangRemaining <= 0.0f)
        {
            ResetFlashbangState();
        }
    }

    private void ApplyFlashbangMovement(float delta)
    {
        var intensity = FlashbangIntensity;
        if (intensity <= 0.02f || IsDead)
        {
            return;
        }
        var escape = _flashbangEscapeDirection;
        escape.Y = 0.0f;
        if (escape.LengthSquared() <= 0.001f)
        {
            escape = -GlobalBasis.Z;
        }
        escape = ApplyPursuitObstacleAvoidance(escape.Normalized());

        if (IsProne || IsCrouched)
        {
            _ = TryStandForCombatMovement();
        }
        var speed = IsProne
            ? 1.1f
            : IsCrouched ? 1.85f : Mathf.Lerp(3.2f, 5.8f, intensity);

        var current = new Vector3(Velocity.X, 0.0f, Velocity.Z);
        if (current.Length() > speed)
        {
            current = current.Normalized() * speed;
        }
        var currentDirection = current.LengthSquared() > 0.04f ? current.Normalized() : escape;
        var blendedDirection = currentDirection.Lerp(escape, 0.45f + intensity * 0.4f);
        var desiredDirection = blendedDirection.LengthSquared() > 0.001f
            ? blendedDirection.Normalized()
            : escape;
        var velocity = Velocity;
        velocity.X = current.X;
        velocity.Z = current.Z;
        velocity.X = Mathf.MoveToward(velocity.X, desiredDirection.X * speed, delta * 16.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, desiredDirection.Z * speed, delta * 16.0f);
        Velocity = velocity;
        _stationaryMoveTimer = 0.0f;
    }

    private bool FlashbangSuppressesVision
        => FlashbangIntensity >= FlashVisionSuppressionThreshold;

    private bool CanFireDuringFlashbang
        => FlashbangIntensity < FlashFireSuppressionThreshold;

    private float FlashbangFireCadenceMultiplier
        => Mathf.Lerp(1.0f, 3.4f, FlashbangIntensity);

    private float FlashbangAccuracyMultiplier
        => Mathf.Lerp(1.0f, 0.08f, FlashbangIntensity);

    private void ResetFlashbangState()
    {
        _flashbangRemaining = 0.0f;
        _flashbangFadeSeconds = 0.0f;
        _flashbangPeakIntensity = 0.0f;
        _flashbangEscapeDirection = Vector3.Zero;
    }
}
