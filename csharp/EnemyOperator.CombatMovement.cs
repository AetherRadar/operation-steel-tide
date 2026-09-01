using Godot;

namespace OperationSteelTide;

internal enum EnemyCombatPosture
{
    Standing,
    Crouched,
    Prone
}

internal readonly record struct EnemyCombatPostureContext(
    EnemyCombatPosture Current,
    bool HasSight,
    bool Pressured,
    bool InCover,
    bool SeekingCover,
    bool OnFloor,
    float Distance,
    float HorizontalSpeed,
    float HoldRemaining,
    float CooldownRemaining,
    float FlashIntensity);

internal readonly record struct EnemyCombatPostureDecision(
    EnemyCombatPosture Posture,
    float HoldSeconds,
    float CooldownSeconds,
    string Reason);

internal readonly record struct EnemyCombatMovementDiagnosticState(
    EnemyCombatPosture Posture,
    bool AirborneAttack,
    bool VisionSuppressed,
    bool CanFire,
    float ColliderHeight,
    float EyeHeight,
    float StanceCooldown,
    float JumpCooldown,
    float PressureRemaining,
    float FlashRemaining,
    float FlashIntensity,
    int CrouchTransitions,
    int ProneTransitions,
    int Vaults,
    int JumpAttacks,
    int AirborneAttackShots);

public partial class EnemyOperator
{
    private const float StandingColliderHeight = 1.78f;
    private const float CrouchedColliderHeight = 1.22f;
    private const float ProneColliderHeight = 0.78f;
    private bool _combatCrouched;
    private float _combatStanceHoldRemaining;
    private float _combatStanceCooldown;
    private float _combatPressureRemaining;
    private float _combatCoverSearchCooldown;
    private int _combatCrouchTransitions;
    private int _combatProneTransitions;

    public bool IsCrouched => !IsProne && !IsDead && (_combatCrouched || _inCover);
    internal float CombatColliderHeight => IsProne || IsDead
        ? ProneColliderHeight
        : IsCrouched ? CrouchedColliderHeight : StandingColliderHeight;
    internal float CombatEyeHeight => IsProne ? 0.55f : IsCrouched ? 1.03f : 1.55f;
    internal float CombatMuzzleHeight => IsProne ? 0.55f : IsCrouched ? 1.0f : 1.45f;
    internal float CombatAimHeight => IsProne ? 0.45f : IsCrouched ? 0.9f : 1.2f;
    internal EnemyCombatMovementDiagnosticState CaptureCombatMovementForDiagnostics()
        => new(
            CurrentCombatPosture(),
            IsCombatAirborneAttack,
            FlashbangSuppressesVision,
            CanFireDuringFlashbang,
            _collider.Shape is CapsuleShape3D capsule ? capsule.Height : -1.0f,
            CombatEyeHeight,
            _combatStanceCooldown,
            _combatJumpCooldown,
            _combatPressureRemaining,
            _flashbangRemaining,
            FlashbangIntensity,
            _combatCrouchTransitions,
            _combatProneTransitions,
            _combatVaults,
            _combatJumpAttacks,
            _combatAirborneAttackShots);

    internal static EnemyCombatPostureDecision PlanCombatPostureForDiagnostics(
        EnemyCombatPostureContext context)
        => PlanCombatPosture(context);

    private static EnemyCombatPostureDecision PlanCombatPosture(
        EnemyCombatPostureContext context)
    {
        if (!context.OnFloor || context.SeekingCover || context.FlashIntensity >= 0.45f)
        {
            return new EnemyCombatPostureDecision(
                EnemyCombatPosture.Standing,
                0.0f,
                Mathf.Max(context.CooldownRemaining, 0.8f),
                context.SeekingCover ? "moving to cover" : "mobility required");
        }

        if (context.Current == EnemyCombatPosture.Prone)
        {
            var shouldStand = context.Distance < 6.0f
                || context.HorizontalSpeed > 2.2f
                || !context.HasSight && !context.Pressured
                || context.HoldRemaining <= 0.0f;
            return shouldStand
                ? new EnemyCombatPostureDecision(
                    EnemyCombatPosture.Standing,
                    0.0f,
                    Mathf.Max(context.CooldownRemaining, 2.4f),
                    "prone firing window complete")
                : new EnemyCombatPostureDecision(
                    EnemyCombatPosture.Prone,
                    context.HoldRemaining,
                    context.CooldownRemaining,
                    "hold prone firing lane");
        }

        if (context.Current == EnemyCombatPosture.Crouched)
        {
            var shouldStand = context.Distance < 4.5f
                || !context.HasSight && !context.InCover
                || context.HoldRemaining <= 0.0f && !context.InCover;
            if (!shouldStand)
            {
                return new EnemyCombatPostureDecision(
                    EnemyCombatPosture.Crouched,
                    context.HoldRemaining,
                    context.CooldownRemaining,
                    context.InCover ? "hold covered firing lane" : "hold pressured crouch");
            }
            return new EnemyCombatPostureDecision(
                EnemyCombatPosture.Standing,
                0.0f,
                Mathf.Max(context.CooldownRemaining, 1.8f),
                "crouch firing window complete");
        }

        if (context.CooldownRemaining > 0.0f || !context.HasSight)
        {
            return new EnemyCombatPostureDecision(
                EnemyCombatPosture.Standing,
                0.0f,
                context.CooldownRemaining,
                context.HasSight ? "stance cooldown" : "no confirmed firing lane");
        }
        if (context.InCover && context.Distance is >= 5.0f and <= 34.0f)
        {
            return new EnemyCombatPostureDecision(
                EnemyCombatPosture.Crouched,
                1.35f,
                2.0f,
                "fire from cover");
        }
        if (context.Pressured
            && context.Distance is >= 17.0f and <= 42.0f
            && context.HorizontalSpeed <= 2.2f)
        {
            return new EnemyCombatPostureDecision(
                EnemyCombatPosture.Prone,
                1.3f,
                2.8f,
                "drop under medium-range pressure");
        }
        if (context.Pressured && context.Distance is >= 6.0f and <= 27.0f)
        {
            return new EnemyCombatPostureDecision(
                EnemyCombatPosture.Crouched,
                1.15f,
                2.2f,
                "crouch under close pressure");
        }
        return new EnemyCombatPostureDecision(
            EnemyCombatPosture.Standing,
            0.0f,
            0.0f,
            "retain mobility");
    }

    private void UpdateCombatMovementTimers(float delta)
    {
        _combatStanceHoldRemaining = Mathf.Max(0.0f, _combatStanceHoldRemaining - delta);
        _combatStanceCooldown = Mathf.Max(0.0f, _combatStanceCooldown - delta);
        _combatPressureRemaining = Mathf.Max(0.0f, _combatPressureRemaining - delta);
        _combatCoverSearchCooldown = Mathf.Max(0.0f, _combatCoverSearchCooldown - delta);
        UpdateAirborneCombatTimers(delta);
        UpdateFlashbangTimers(delta);
    }

    private void RegisterCombatPressure(float seconds = 2.25f)
        => _combatPressureRemaining = Mathf.Max(_combatPressureRemaining, seconds);

    private void UpdateDemolitionCombatStance(float distance, bool hasSight)
    {
        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var decision = PlanCombatPosture(new EnemyCombatPostureContext(
            CurrentCombatPosture(),
            hasSight,
            _combatPressureRemaining > 0.0f,
            _inCover,
            _seekingCover,
            IsOnFloor(),
            distance,
            horizontalSpeed,
            IsProne ? _proneTimer : _combatStanceHoldRemaining,
            _combatStanceCooldown,
            FlashbangIntensity));
        ApplyCombatPostureDecision(decision);

        if (_combatPressureRemaining is > 0.0f and <= 0.72f
            && !_seekingCover
            && !_inCover
            && !IsProne
            && !_combatCrouched
            && hasSight
            && distance > 10.0f
            && _combatCoverSearchCooldown <= 0.0f
            && Main is not null)
        {
            _combatCoverSearchCooldown = 1.0f;
            var cover = Main.FindCoverPoint(GlobalPosition, CurrentThreatPosition(hasSight));
            if (cover.Y > -500.0f && cover.DistanceTo(GlobalPosition) < 22.0f)
            {
                _seekingCover = true;
                _coverTarget = cover;
            }
        }
    }

    private void ApplyCombatPostureDecision(EnemyCombatPostureDecision decision)
    {
        var current = CurrentCombatPosture();
        if (decision.Posture == current)
        {
            return;
        }

        switch (decision.Posture)
        {
            case EnemyCombatPosture.Prone:
                SetProne(true);
                _proneTimer = decision.HoldSeconds;
                break;
            case EnemyCombatPosture.Crouched:
                if (!TrySetPronePosture(false, CrouchedColliderHeight))
                {
                    return;
                }
                SetCombatCrouched(true);
                _combatStanceHoldRemaining = decision.HoldSeconds;
                break;
            default:
                if (!TryStandForCombatMovement())
                {
                    return;
                }
                _combatStanceHoldRemaining = 0.0f;
                break;
        }
        _combatStanceCooldown = Mathf.Max(_combatStanceCooldown, decision.CooldownSeconds);
    }

    private EnemyCombatPosture CurrentCombatPosture()
        => IsProne
            ? EnemyCombatPosture.Prone
            : IsCrouched ? EnemyCombatPosture.Crouched : EnemyCombatPosture.Standing;

    private void SetCombatCrouched(bool crouched)
    {
        crouched = crouched && !IsProne && !IsDead;
        if (!crouched
            && _combatCrouched
            && !IsProne
            && !IsDead
            && !HasCombatPostureClearance(StandingColliderHeight))
        {
            return;
        }
        if (_combatCrouched == crouched)
        {
            return;
        }
        _combatCrouched = crouched;
        _combatCrouchTransitions++;
        UpdateAuthoredStanceCollider();
    }

    private void OnCombatProneStateChanged(bool prone)
    {
        _combatProneTransitions++;
        if (prone)
        {
            SetCombatCrouched(false);
        }
        else
        {
            _proneTimer = 0.0f;
        }
    }

    private HitRegion ResolveIncomingHitRegion(float localHeight)
    {
        if (IsProne)
        {
            return localHeight > 0.58f
                ? HitRegion.Head
                : localHeight > 0.24f ? HitRegion.Torso : HitRegion.Limbs;
        }
        if (IsCrouched)
        {
            return localHeight > 1.02f
                ? HitRegion.Head
                : localHeight > 0.46f ? HitRegion.Torso : HitRegion.Limbs;
        }
        return localHeight > 1.48f
            ? HitRegion.Head
            : localHeight > 0.66f ? HitRegion.Torso : HitRegion.Limbs;
    }

    private void ResetCombatMovementState()
    {
        _combatCrouched = false;
        _combatStanceHoldRemaining = 0.0f;
        _combatStanceCooldown = 0.0f;
        _combatPressureRemaining = 0.0f;
        _combatCoverSearchCooldown = 0.0f;
        ResetAirborneCombatState();
        ResetFlashbangState();
        _combatCrouchTransitions = 0;
        _combatProneTransitions = 0;
        UpdateAuthoredStanceCollider();
    }
}
