using Godot;

namespace OperationSteelTide;

internal readonly record struct EnemyCombatJumpContext(
    bool Pressured,
    bool HasSight,
    bool HasTarget,
    bool OnFloor,
    bool Standing,
    bool Channeling,
    bool HasHeadroom,
    float HorizontalSpeed,
    float Distance,
    float CooldownRemaining);

public partial class EnemyOperator
{
    private float _combatJumpCooldown;
    private float _combatAirborneAttackRemaining;
    private bool _combatAirborneAttackShotPending;
    private int _combatVaults;
    private int _combatJumpAttacks;
    private int _combatAirborneAttackShots;

    internal bool IsCombatAirborneAttack
        => _combatAirborneAttackRemaining > 0.0f && !IsOnFloor();

    internal static bool CanStartCombatJumpAttackForDiagnostics(
        EnemyCombatJumpContext context)
        => CanStartCombatJumpAttack(context);

    private void UpdateAirborneCombatTimers(float delta)
    {
        _combatJumpCooldown = Mathf.Max(0.0f, _combatJumpCooldown - delta);
        _combatAirborneAttackRemaining = Mathf.Max(
            0.0f,
            _combatAirborneAttackRemaining - delta);
        if (_combatAirborneAttackRemaining <= 0.0f)
        {
            _combatAirborneAttackShotPending = false;
        }
    }

    private void PrepareCombatMovementBeforeMove(float delta)
    {
        ApplyFlashbangMovement(delta);
        if (Main?.IsDemolitionMode != true
            || IsWorldBoss
            || SentryMode
            || IsProne
            || IsCrouched
            || IsDead
            || !IsOnFloor()
            || _combatJumpCooldown > 0.0f
            || Main.IsDemolitionOpponentChanneling(this))
        {
            return;
        }

        var horizontal = new Vector3(Velocity.X, 0.0f, Velocity.Z);
        if (horizontal.LengthSquared() < 1.25f * 1.25f)
        {
            return;
        }
        var direction = horizontal.Normalized();
        if (TryFindVaultableMovementBlocker(direction))
        {
            StartCombatJump(direction, 5.25f, 1.35f, airborneAttack: false);
            _combatVaults++;
            return;
        }

        var target = EngageTargetNode;
        var hasTarget = target is not null && IsInstanceValid(target);
        var distance = hasTarget
            ? GlobalPosition.DistanceTo(target!.GlobalPosition)
            : float.PositiveInfinity;
        var jumpContext = new EnemyCombatJumpContext(
            _combatPressureRemaining > 0.0f,
            _cachedLineOfSight,
            hasTarget,
            IsOnFloor(),
            !IsProne && !IsCrouched,
            Main.IsDemolitionOpponentChanneling(this),
            HasHeadroom: true,
            horizontal.Length(),
            distance,
            _combatJumpCooldown);
        if (!CanStartCombatJumpAttack(jumpContext)
            || !HasJumpHeadroom())
        {
            return;
        }
        _ = TryStartCombatJumpAttack(jumpContext, direction);
    }

    private static bool CanStartCombatJumpAttack(EnemyCombatJumpContext context)
        => context.Pressured
            && context.HasSight
            && context.HasTarget
            && context.OnFloor
            && context.Standing
            && !context.Channeling
            && context.HasHeadroom
            && context.HorizontalSpeed >= 1.25f
            && context.Distance is >= 4.5f and <= 10.0f
            && context.CooldownRemaining <= 0.0f;

    private bool TryStartCombatJumpAttack(
        EnemyCombatJumpContext context,
        Vector3 direction)
    {
        if (!CanStartCombatJumpAttack(context))
        {
            return false;
        }
        StartCombatJump(direction.Normalized(), 4.25f, 3.6f, airborneAttack: true);
        _combatJumpAttacks++;
        return true;
    }

    private bool TryFindVaultableMovementBlocker(Vector3 direction)
    {
        var lowFrom = GlobalPosition + Vector3.Up * 0.34f;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                lowFrom,
                lowFrom + direction * 0.9f,
                GetRid(),
                1,
                out var lowHit)
            || lowHit.Collider is not StaticBody3D && lowHit.Collider is not AnimatableBody3D
            || lowHit.Normal.Dot(Vector3.Up) > 0.55f
            || lowFrom.DistanceTo(lowHit.Position) < 0.18f)
        {
            return false;
        }
        var highFrom = GlobalPosition + Vector3.Up * 1.2f;
        if (PhysicsRaycast.TryHit(
                GetWorld3D(),
                highFrom,
                highFrom + direction * 1.0f,
                GetRid(),
                1,
                out _))
        {
            return false;
        }

        var landingProbe = GlobalPosition + direction * 1.15f + Vector3.Up * 1.25f;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                landingProbe,
                landingProbe + Vector3.Down * 1.9f,
                GetRid(),
                1,
                out var groundHit)
            || groundHit.Normal.Dot(Vector3.Up) < 0.72f)
        {
            return false;
        }
        var heightDelta = groundHit.Position.Y - GlobalPosition.Y;
        return heightDelta is >= -0.35f and <= 0.72f
            && HasJumpHeadroom();
    }

    private bool HasJumpHeadroom()
        => !TestMove(
            GlobalTransform,
            Vector3.Up * 0.72f,
            null,
            0.01f,
            recoveryAsCollision: false,
            maxCollisions: 4);

    private void StartCombatJump(
        Vector3 direction,
        float upwardSpeed,
        float cooldown,
        bool airborneAttack)
    {
        var velocity = Velocity;
        var horizontalSpeed = Mathf.Max(new Vector2(velocity.X, velocity.Z).Length(), 4.8f);
        velocity.X = direction.X * horizontalSpeed;
        velocity.Y = upwardSpeed;
        velocity.Z = direction.Z * horizontalSpeed;
        Velocity = velocity;
        _combatJumpCooldown = cooldown;
        _combatAirborneAttackRemaining = airborneAttack ? 0.9f : 0.0f;
        _combatAirborneAttackShotPending = airborneAttack;
        if (airborneAttack)
        {
            _fireTimer = Mathf.Min(_fireTimer, 0.02f);
        }
        _stationaryMoveTimer = 0.0f;
    }

    private bool TryFireDuringAirborneAttack(float distance, bool hasSight)
    {
        if (!_combatAirborneAttackShotPending
            || !IsCombatAirborneAttack
            || !hasSight
            || !CanFireDuringFlashbang
            || _fireTimer > 0.0f
            || distance >= CurrentFireRange)
        {
            return false;
        }

        var shotsBefore = AttackShotsFired;
        if (_combatTarget is not null)
        {
            FireAtSquad(distance);
        }
        else if (_rawTarget is EnemyOperator rival && !rival.IsDead)
        {
            FireAtNode(rival, distance);
        }
        if (AttackShotsFired <= shotsBefore)
        {
            return false;
        }
        _combatAirborneAttackShotPending = false;
        _combatAirborneAttackShots++;
        return true;
    }

    private bool TryHandlePendingAirborneAttackShot(float distance, bool hasSight)
    {
        if (!_combatAirborneAttackShotPending)
        {
            return false;
        }

        // Reserve the jump marker's first shot for a genuinely airborne physics tick.
        // This also preserves the launch velocity instead of letting ground combat
        // movement overwrite it on the takeoff frame.
        if (hasSight)
        {
            FaceCombatContact(hasSight: true);
        }
        _ = TryFireDuringAirborneAttack(distance, hasSight);
        return true;
    }

    private void ResetAirborneCombatState()
    {
        _combatJumpCooldown = 0.0f;
        _combatAirborneAttackRemaining = 0.0f;
        _combatAirborneAttackShotPending = false;
        _combatVaults = 0;
        _combatJumpAttacks = 0;
        _combatAirborneAttackShots = 0;
    }

    private void ClearAirborneCombatForRoundFreeze()
    {
        _combatJumpCooldown = 0.0f;
        _combatAirborneAttackRemaining = 0.0f;
        _combatAirborneAttackShotPending = false;
    }
}
