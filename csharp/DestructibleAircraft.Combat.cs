using Godot;

namespace OperationSteelTide;

public partial class DestructibleAircraft
{
    private enum AircraftCombatMode
    {
        Patrol,
        Engaged,
        Cooldown
    }

    public bool IsAttackOrbitActive { get; private set; }
    public float AttackHorizontalDistance { get; private set; } = float.PositiveInfinity;
    public float LastAttackAngleDegrees { get; private set; } = 90.0f;
    public bool LastAttackPathClear { get; private set; }
    public bool IsCombatEngaged => _combatMode == AircraftCombatMode.Engaged;
    public bool IsCombatCooldown => _combatMode == AircraftCombatMode.Cooldown;
    public Node3D? CurrentTargetForDiagnostics => IsValidCombatTarget(_currentTarget) ? _currentTarget : null;
    public float EngagementRemainingForDiagnostics => _engagementRemaining;

    public const float AttackAltitude = 36.0f;
    public const float AttackOrbitRadius = 9.0f;
    public const float MaximumAttackAngleDegrees = 20.0f;
    public const float EngagementDuration = 18.0f;
    public const float TargetLockDuration = 6.0f;
    public const float CombatCooldownDuration = 8.0f;
    public const float VisualConfirmationSeconds = 0.75f;
    private const float EngageRange = 118.0f;
    private const float VisualDetectionRange = 78.0f;
    private const float VisualFieldOfViewDot = 0.42f;
    private const float VisualScanInterval = 0.15f;
    private const float NearbyRetargetRadius = 52.0f;
    private const float AttackCruiseSpeed = 21.0f;
    private const float AttackOrbitAngularSpeed = 0.75f;
    private const float AttackFireRadius = AttackOrbitRadius + 3.0f;
    private const float MinimumAttackVerticalDrop = 24.0f;
    private const float AttackLeadSeconds = 0.16f;
    private const float AttackHorizontalSpread = 0.7f;
    private const float OpenSkyProbeHeight = 52.0f;
    private const float FireCooldown = 3.1f;
    private const float ShellDamage = 48.0f;
    private const float ShellBlastRadius = 12.5f;

    private float _attackOrbitPhase;
    private float _fireCooldown;
    private float _acquireTimer;
    private float _visualConfirmation;
    private float _engagementRemaining;
    private float _targetLockRemaining;
    private float _cooldownRemaining;
    private AircraftCombatMode _combatMode;
    private Node3D? _currentTarget;
    private Node3D? _visualCandidate;
    private Node3D? _queuedTarget;
    private Vector3 _lastTargetPosition;

    internal void SetAttackTargetForDiagnostics(Node3D target, Vector3 horizontalOffset)
    {
        BeginEngagement(target);
        _acquireTimer = 10.0f;
        _fireCooldown = Mathf.Max(_fireCooldown, 10.0f);
        IsAttackOrbitActive = false;
        _rejoiningPatrol = false;
        GlobalPosition = target.GlobalPosition + horizontalOffset + Vector3.Up * AttackAltitude;
        LastPatrolStepDistance = 0.0f;
    }

    internal void SetPatrolStateForDiagnostics(Vector3 position, Vector3 flightDirection)
    {
        GlobalPosition = position;
        var horizontal = new Vector3(flightDirection.X, 0.0f, flightDirection.Z);
        _flightDirection = horizontal.LengthSquared() > 0.0001f
            ? horizontal.Normalized()
            : Vector3.Right;
        _rejoiningPatrol = false;
        ResetCombatForPatrol(initialScanDelay: 0.0f);
        LastPatrolStepDistance = 0.0f;
    }

    internal void AdvanceCombatStateForDiagnostics(float delta)
    {
        UpdateTargeting(Mathf.Max(0.0f, delta));
    }

    internal void AdvancePatrolStateForDiagnostics(float delta)
    {
        UpdatePatrol(Mathf.Max(0.0f, delta));
    }

    internal bool CanVisuallyDetectForDiagnostics(Node3D target)
    {
        return CanVisuallyDetect(target);
    }

    internal void RegisterOperatorAttack(Node3D actor, Vector3 origin, float soundRadius)
    {
        if (!IsValidCombatTarget(actor)
            || _combatMode == AircraftCombatMode.Cooldown
            || GlobalPosition.DistanceTo(origin) > Mathf.Max(18.0f, soundRadius) + 8.0f
            || !HasOpenSkyAbove(actor.GlobalPosition, actor))
        {
            return;
        }

        if (_combatMode == AircraftCombatMode.Engaged)
        {
            QueueTarget(actor);
            return;
        }
        BeginEngagement(actor);
    }

    private void NotifyDamagedByOperator(Node? attacker)
    {
        if (attacker is not Node3D actor || !IsValidCombatTarget(actor))
        {
            return;
        }
        if (_combatMode == AircraftCombatMode.Engaged)
        {
            QueueTarget(actor);
            return;
        }
        // A direct hit always breaks patrol or cooldown and forces retaliation.
        BeginEngagement(actor);
    }

    private void ResetCombatForPatrol(float initialScanDelay)
    {
        _combatMode = AircraftCombatMode.Patrol;
        _currentTarget = null;
        _visualCandidate = null;
        _queuedTarget = null;
        _visualConfirmation = 0.0f;
        _engagementRemaining = 0.0f;
        _targetLockRemaining = 0.0f;
        _cooldownRemaining = 0.0f;
        _acquireTimer = Mathf.Max(0.0f, initialScanDelay);
        IsAttackOrbitActive = false;
        AttackHorizontalDistance = float.PositiveInfinity;
    }

    private void UpdateTargeting(float dt)
    {
        _fireCooldown = Mathf.Max(0.0f, _fireCooldown - dt);
        if (_combatMode == AircraftCombatMode.Cooldown)
        {
            _cooldownRemaining -= dt;
            if (_cooldownRemaining <= 0.0f && !_rejoiningPatrol)
            {
                ResetCombatForPatrol(_rng.RandfRange(0.15f, 0.5f));
            }
            return;
        }
        if (_combatMode == AircraftCombatMode.Engaged)
        {
            UpdateEngagement(dt);
            return;
        }

        if (Main?.CanAircraftPassivelyDetectOperators() != true)
        {
            ResetVisualConfirmation();
            return;
        }

        _acquireTimer -= dt;
        if (_acquireTimer > 0.0f)
        {
            return;
        }
        _acquireTimer = VisualScanInterval;
        var candidate = AcquireVisibleTarget();
        if (!ReferenceEquals(candidate, _visualCandidate))
        {
            _visualCandidate = candidate;
            _visualConfirmation = candidate is null ? 0.0f : VisualScanInterval;
        }
        else if (candidate is not null)
        {
            _visualConfirmation += VisualScanInterval;
        }
        if (candidate is not null && _visualConfirmation >= VisualConfirmationSeconds)
        {
            BeginEngagement(candidate);
        }
    }

    private void UpdateCombat()
    {
        if (_combatMode != AircraftCombatMode.Engaged
            || !IsValidCombatTarget(_currentTarget)
            || !IsAttackOrbitActive
            || AttackHorizontalDistance > AttackFireRadius)
        {
            return;
        }

        if (_fireCooldown <= 0.0f)
        {
            FireAt(_currentTarget!);
        }
    }

    private void UpdateEngagement(float dt)
    {
        _engagementRemaining -= dt;
        _targetLockRemaining -= dt;
        if (_engagementRemaining <= 0.0f)
        {
            BeginCooldown();
            return;
        }

        var currentAttackable = IsAttackableCombatTarget(_currentTarget);
        if (currentAttackable)
        {
            _lastTargetPosition = _currentTarget!.GlobalPosition;
        }
        if (currentAttackable && _targetLockRemaining > 0.0f)
        {
            return;
        }

        var next = SelectNearbyAlternative(_lastTargetPosition, _currentTarget);
        if (next is not null)
        {
            SwitchTarget(next);
            return;
        }
        if (currentAttackable)
        {
            _targetLockRemaining = Mathf.Min(3.0f, _engagementRemaining);
            return;
        }
        BeginCooldown();
    }

    private void BeginEngagement(Node3D target)
    {
        if (!IsValidCombatTarget(target))
        {
            return;
        }
        _combatMode = AircraftCombatMode.Engaged;
        _engagementRemaining = EngagementDuration;
        _queuedTarget = null;
        _visualCandidate = null;
        _visualConfirmation = 0.0f;
        SwitchTarget(target);
    }

    private void SwitchTarget(Node3D target)
    {
        _currentTarget = target;
        _lastTargetPosition = target.GlobalPosition;
        _targetLockRemaining = Mathf.Min(TargetLockDuration, _engagementRemaining);
        if (ReferenceEquals(_queuedTarget, target))
        {
            _queuedTarget = null;
        }
        IsAttackOrbitActive = false;
        AttackHorizontalDistance = float.PositiveInfinity;
        _rejoiningPatrol = false;
    }

    private void QueueTarget(Node3D target)
    {
        if (!ReferenceEquals(target, _currentTarget))
        {
            _queuedTarget = target;
        }
    }

    private void BeginCooldown()
    {
        _combatMode = AircraftCombatMode.Cooldown;
        _currentTarget = null;
        _queuedTarget = null;
        _visualCandidate = null;
        _visualConfirmation = 0.0f;
        _engagementRemaining = 0.0f;
        _targetLockRemaining = 0.0f;
        _cooldownRemaining = CombatCooldownDuration;
        _patrolPhase = ClosestPatrolPhase(Position);
        _rejoiningPatrol = Position.DistanceTo(PatrolPosition(_patrolPhase)) > 0.05f;
        IsAttackOrbitActive = false;
        AttackHorizontalDistance = float.PositiveInfinity;
    }

    private void ResetVisualConfirmation()
    {
        _visualCandidate = null;
        _visualConfirmation = 0.0f;
    }

    private Node3D? AcquireVisibleTarget()
    {
        if (Main is null || !IsHostile)
        {
            return null;
        }
        Node3D? best = null;
        var bestDistance = VisualDetectionRange;
        foreach (var combatant in Main.GetAircraftCombatants())
        {
            if (!CanVisuallyDetect(combatant))
            {
                continue;
            }
            var distance = GlobalPosition.DistanceTo(combatant.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = combatant;
            }
        }
        return best;
    }

    private Node3D? SelectNearbyAlternative(Vector3 anchor, Node3D? excluded)
    {
        if (Main is null)
        {
            return null;
        }
        if (IsValidCombatTarget(_queuedTarget)
            && !ReferenceEquals(_queuedTarget, excluded)
            && _queuedTarget!.GlobalPosition.DistanceTo(anchor) <= NearbyRetargetRadius
            && HasOpenSkyAbove(_queuedTarget.GlobalPosition, _queuedTarget))
        {
            return _queuedTarget;
        }

        Node3D? best = null;
        var bestDistance = NearbyRetargetRadius;
        foreach (var combatant in Main.GetAircraftCombatants())
        {
            if (ReferenceEquals(combatant, excluded)
                || !IsValidCombatTarget(combatant)
                || !HasOpenSkyAbove(combatant.GlobalPosition, combatant))
            {
                continue;
            }
            var distance = combatant.GlobalPosition.DistanceTo(anchor);
            if (distance < bestDistance && GlobalPosition.DistanceTo(combatant.GlobalPosition) <= EngageRange)
            {
                bestDistance = distance;
                best = combatant;
            }
        }
        return best;
    }

    private bool CanVisuallyDetect(Node3D target)
    {
        if (!IsValidCombatTarget(target))
        {
            return false;
        }
        var toTarget = target.GlobalPosition - GlobalPosition;
        if (toTarget.Length() > VisualDetectionRange)
        {
            return false;
        }
        var horizontal = new Vector3(toTarget.X, 0.0f, toTarget.Z);
        if (horizontal.LengthSquared() <= 0.0001f
            || _flightDirection.Dot(horizontal.Normalized()) < VisualFieldOfViewDot)
        {
            return false;
        }
        var aim = target.GlobalPosition + Vector3.Up * 0.9f;
        return HasOpenSkyAbove(aim, target)
            && Main?.IsLineObscuredBySmoke(GlobalPosition, aim) != true
            && HasSensorLineOfSight(GlobalPosition, aim, target);
    }

    private bool HasSensorLineOfSight(Vector3 from, Vector3 to, Node3D target)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1 | 2 | 4;
        query.CollideWithAreas = false;
        query.Exclude = AttackQueryExclusions(target);
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    private bool IsValidCombatTarget(Node3D? target)
    {
        return target is not null
            && GodotObject.IsInstanceValid(target)
            && Main?.IsActiveAircraftCombatant(target) == true;
    }

    private bool IsAttackableCombatTarget(Node3D? target)
    {
        return IsValidCombatTarget(target)
            && HasOpenSkyAbove(target!.GlobalPosition, target);
    }

    private bool FireAt(Node3D target)
    {
        if (Main is null || !TryBuildAttackSolution(target, out var muzzle, out var aim))
        {
            return false;
        }

        var damage = ShellDamage * _rng.RandfRange(0.92f, 1.08f);
        LastAttackDamage = damage;
        AttackSalvosFired++;
        _fireCooldown = FireCooldown;

        // Physical bomb: deliberately slow enough to dodge, but impossible to shoot down.
        Main.SpawnAircraftShell(muzzle, aim, damage, ShellBlastRadius, this);
        Main.SpawnTracer(muzzle, aim, new Color(1.0f, 0.4f, 0.12f));
        return true;
    }

    private bool TryBuildAttackSolution(Node3D target, out Vector3 muzzle, out Vector3 aim)
    {
        muzzle = GlobalPosition + Vector3.Down * 1.9f + _flightDirection * 2.2f;
        aim = target.GlobalPosition + Vector3.Up * 0.4f;
        if (target is CharacterBody3D body)
        {
            aim += body.Velocity * AttackLeadSeconds;
        }
        aim += new Vector3(
            _rng.RandfRange(-AttackHorizontalSpread, AttackHorizontalSpread),
            _rng.RandfRange(-0.05f, 0.18f),
            _rng.RandfRange(-AttackHorizontalSpread, AttackHorizontalSpread));

        var toAim = aim - muzzle;
        var verticalDrop = -toAim.Y;
        var horizontalDistance = new Vector2(toAim.X, toAim.Z).Length();
        LastAttackAngleDegrees = Mathf.RadToDeg(Mathf.Atan2(horizontalDistance, Mathf.Max(0.001f, verticalDrop)));
        LastAttackPathClear = false;
        if (verticalDrop < MinimumAttackVerticalDrop
            || LastAttackAngleDegrees > MaximumAttackAngleDegrees
            || !HasOpenSkyAbove(aim, target)
            || !HasClearAttackPath(muzzle, aim, target))
        {
            return false;
        }

        LastAttackPathClear = true;
        return true;
    }

    private bool HasOpenSkyAbove(Vector3 point, Node3D target)
    {
        if (!IsInsideTree())
        {
            return false;
        }

        var query = PhysicsRayQueryParameters3D.Create(
            point + Vector3.Up * 0.8f,
            point + Vector3.Up * OpenSkyProbeHeight);
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        query.Exclude = AttackQueryExclusions(target);
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }

    private bool HasClearAttackPath(Vector3 from, Vector3 to, Node3D target)
    {
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1 | 4;
        query.CollideWithAreas = false;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return true;
        }
        return hit["collider"].AsGodotObject() == target;
    }

    private Godot.Collections.Array<Rid> AttackQueryExclusions(Node3D target)
    {
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        if (target is CollisionObject3D collisionTarget)
        {
            exclude.Add(collisionTarget.GetRid());
        }
        return exclude;
    }

    private void UpdateAttackFlight(Node3D target, float dt)
    {
        var parent = GetParent() as Node3D;
        var targetPosition = parent?.ToLocal(target.GlobalPosition) ?? target.GlobalPosition;
        if (!IsAttackOrbitActive)
        {
            var relative = Position - targetPosition;
            _attackOrbitPhase = new Vector2(relative.X, relative.Z).LengthSquared() > 0.01f
                ? Mathf.Atan2(relative.Z, relative.X)
                : 0.0f;
            IsAttackOrbitActive = true;
            _rejoiningPatrol = false;
        }

        _attackOrbitPhase = Mathf.PosMod(
            _attackOrbitPhase + AttackOrbitAngularSpeed * dt,
            Mathf.Tau);
        var desiredPosition = new Vector3(
            targetPosition.X + Mathf.Cos(_attackOrbitPhase) * AttackOrbitRadius,
            Mathf.Max(PatrolAltitude, targetPosition.Y + AttackAltitude),
            targetPosition.Z + Mathf.Sin(_attackOrbitPhase) * AttackOrbitRadius);
        ApplyFlightStep(
            Position.MoveToward(desiredPosition, AttackCruiseSpeed * dt),
            countPatrolDistance: false,
            dt);
        AttackHorizontalDistance = new Vector2(
            GlobalPosition.X - target.GlobalPosition.X,
            GlobalPosition.Z - target.GlobalPosition.Z).Length();
    }
}
