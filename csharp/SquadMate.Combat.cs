using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const float CombatAcquireRange = 68.0f;
    private const float CombatRetainRange = 78.0f;
    private const float VisibleContactMemory = 8.0f;
    private const float DamageContactMemory = 12.0f;

    private EnemyOperator? _combatTarget;
    private EnemyOperator? _combatThreat;
    private float _combatThreatAge;
    private float _combatMemoryRemaining;
    private float _combatSightTimer;
    private float _combatTargetScanTimer;
    private float _combatManeuverTimer;
    private float _combatCoverCommitment;
    private float _combatAvoidanceTimer;
    private float _combatStrafeSign;
    private Vector3 _combatLastKnownPosition;
    private Vector3 _combatCoverPosition;
    private Vector3 _combatFlankPosition;
    private Vector3 _combatDesiredDirection;
    private Vector3 _combatProgressOrigin;
    private float _combatProgressTimer;
    private bool _combatMoveRequested;
    private bool _combatHasSight;
    private bool _combatHasCoverPosition;
    private int _burstShotsRemaining;

    public int CombatShotsFired { get; private set; }
    public int CombatTargetSwitches { get; private set; }
    public int CombatCoverSelections { get; private set; }
    public int CombatStuckRecoveries { get; private set; }
    internal bool CombatHasSightForDiagnostics => _combatHasSight;

    private void InitializeCombatTactics()
    {
        _combatStrafeSign = SquadSlot % 2 == 0 ? 1.0f : -1.0f;
        _combatProgressOrigin = GlobalPosition;
        _combatLastKnownPosition = GlobalPosition;
        _combatSightTimer = 0.0f;
        _combatTargetScanTimer = 0.0f;
        _combatManeuverTimer = 0.0f;
        _combatHasSight = false;
        _burstShotsRemaining = 0;
        CombatShotsFired = 0;
        CombatTargetSwitches = 0;
        CombatCoverSelections = 0;
        CombatStuckRecoveries = 0;
    }

    private void OnSquadOrderChanged()
    {
        _combatManeuverTimer = 0.0f;
        _combatCoverCommitment = 0.0f;
        _combatHasCoverPosition = false;
        _combatStrafeSign = SquadSlot % 2 == 0 ? 1.0f : -1.0f;
        _burstShotsRemaining = 0;
        ResetMovementProgress();
    }

    private void UpdateCombatTacticalTimers(float delta)
    {
        _combatThreatAge += delta;
        _combatMemoryRemaining = Mathf.Max(0.0f, _combatMemoryRemaining - delta);
        _combatSightTimer = Mathf.Max(0.0f, _combatSightTimer - delta);
        _combatTargetScanTimer = Mathf.Max(0.0f, _combatTargetScanTimer - delta);
        _combatManeuverTimer = Mathf.Max(0.0f, _combatManeuverTimer - delta);
        _combatCoverCommitment = Mathf.Max(0.0f, _combatCoverCommitment - delta);
        _combatAvoidanceTimer = Mathf.Max(0.0f, _combatAvoidanceTimer - delta);

        if (_combatThreatAge > 5.0f)
        {
            _combatThreat = null;
        }
        if (_combatTarget is not null
            && (!IsInstanceValid(_combatTarget)
                || _combatTarget.IsDead
                || GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) > CombatRetainRange))
        {
            ClearCombatTarget();
        }
    }

    private EnemyOperator? UpdateCombatTarget(float delta)
    {
        _ = delta;
        if (Main is null || !IsInstanceValid(Main))
        {
            ClearCombatTarget();
            return null;
        }

        EnemyOperator? candidate = null;
        if (_combatThreat is not null
            && IsInstanceValid(_combatThreat)
            && !_combatThreat.IsDead
            && Main.CanSquadEngage(_combatThreat)
            && GlobalPosition.DistanceTo(_combatThreat.GlobalPosition) < CombatRetainRange)
        {
            candidate = _combatThreat;
        }
        else if (_combatTargetScanTimer <= 0.0f)
        {
            candidate = Main.FindNearestEnemy(GlobalPosition, CombatAcquireRange);
            _combatTargetScanTimer = 0.42f + SquadSlot * 0.04f;
            if (candidate is not null && (!Main.CanSquadEngage(candidate) || candidate.IsDead))
            {
                candidate = null;
            }
        }

        if (candidate is not null && ShouldAdoptCombatTarget(candidate))
        {
            AssignCombatTarget(candidate, VisibleContactMemory);
        }

        if (_combatTarget is null
            || !IsInstanceValid(_combatTarget)
            || _combatTarget.IsDead
            || !Main.CanSquadEngage(_combatTarget))
        {
            ClearCombatTarget();
            return null;
        }

        if (_combatSightTimer <= 0.0f)
        {
            _combatHasSight = HasLineOfSight(_combatTarget);
            _combatSightTimer = 0.1f;
            if (_combatHasSight)
            {
                _combatLastKnownPosition = _combatTarget.GlobalPosition;
                _combatMemoryRemaining = VisibleContactMemory;
            }
        }

        if (!_combatHasSight && _combatMemoryRemaining <= 0.0f)
        {
            ClearCombatTarget();
            return null;
        }
        return _combatTarget;
    }

    private bool ShouldAdoptCombatTarget(EnemyOperator candidate)
    {
        if (_combatTarget is null || !IsInstanceValid(_combatTarget) || _combatTarget.IsDead)
        {
            return true;
        }
        if (candidate == _combatTarget)
        {
            return false;
        }
        if (candidate == _combatThreat)
        {
            return true;
        }
        var currentDistance = GlobalPosition.DistanceSquaredTo(_combatTarget.GlobalPosition);
        var candidateDistance = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
        return candidateDistance < currentDistance * (_combatHasSight ? 0.42f : 0.72f);
    }

    private void AssignCombatTarget(EnemyOperator target, float memorySeconds)
    {
        if (_combatTarget != target)
        {
            CombatTargetSwitches++;
            _burstShotsRemaining = 0;
            _combatHasCoverPosition = false;
            _combatManeuverTimer = 0.0f;
        }
        _combatTarget = target;
        _combatLastKnownPosition = target.GlobalPosition;
        _combatMemoryRemaining = Mathf.Max(_combatMemoryRemaining, memorySeconds);
        _combatSightTimer = 0.0f;
    }

    private void ClearCombatTarget()
    {
        _combatTarget = null;
        _combatHasSight = false;
        _combatMemoryRemaining = 0.0f;
        _combatHasCoverPosition = false;
        _combatCoverCommitment = 0.0f;
        _burstShotsRemaining = 0;
    }

    private Vector3 ResolveFormationDestination()
    {
        var formation = SquadSlot switch
        {
            1 => new Vector3(-2.25f, 0.0f, 3.2f),
            2 => new Vector3(2.25f, 0.0f, 3.25f),
            _ => new Vector3(0.0f, 0.0f, 5.1f)
        };
        return Order == SquadOrder.Follow
            ? Leader.GlobalPosition
                + Leader.GlobalBasis.X * formation.X
                + Leader.GlobalBasis.Z * formation.Z
            : _orderPosition;
    }

    private void UpdateTacticalMovement(
        Vector3 anchorDestination,
        EnemyOperator? hostile,
        bool objectivePriority,
        float delta)
    {
        var destination = anchorDestination;
        var anchorFlat = FlattenToCurrentHeight(anchorDestination);
        if (Order == SquadOrder.Follow
            && GlobalPosition.DistanceTo(Leader.GlobalPosition) > 42.0f)
        {
            GlobalPosition = anchorFlat + Vector3.Up * 0.35f;
            ResetMovementProgress();
        }

        if (hostile is not null && IsInstanceValid(hostile) && !hostile.IsDead && !objectivePriority)
        {
            destination = ResolveCombatDestination(anchorDestination, hostile);
        }

        var flatDestination = FlattenToCurrentHeight(destination);
        var distance = GlobalPosition.DistanceTo(flatDestination);
        var desired = distance > 0.75f
            ? GlobalPosition.DirectionTo(flatDestination)
            : Vector3.Zero;
        desired.Y = 0.0f;
        _combatMoveRequested = desired.LengthSquared() > 0.01f;
        if (_combatMoveRequested)
        {
            desired = AvoidObstacle(desired.Normalized());
        }
        _combatDesiredDirection = desired;

        var spec = OperatorRoles.Spec(Role);
        var boost = Role == OperatorRole.Assault && _overdriveTime > 0.0f ? 1.22f : 1.0f;
        var speed = (distance > 8.0f ? 5.4f : 3.8f) * spec.MovementMultiplier * boost;
        if (_skillActionTime > 0.0f)
        {
            speed *= 0.45f;
        }
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, desired.X * speed, delta * 15.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, desired.Z * speed, delta * 15.0f);
        velocity.Y = IsOnFloor() ? -0.2f : velocity.Y - 22.0f * delta;
        Velocity = velocity;

        var facePoint = hostile is not null && IsInstanceValid(hostile)
            && GlobalPosition.DistanceTo(hostile.GlobalPosition) < CombatAcquireRange
                ? (_combatHasSight ? hostile.GlobalPosition : _combatLastKnownPosition)
                : flatDestination;
        FaceTacticalPoint(facePoint, delta);
    }

    private Vector3 ResolveCombatDestination(Vector3 anchor, EnemyOperator hostile)
    {
        var anchorLeash = Order switch
        {
            SquadOrder.Hold => 1.25f,
            SquadOrder.Move => 8.0f,
            _ => 15.0f
        };
        var enemyDistance = GlobalPosition.DistanceTo(hostile.GlobalPosition);
        if (Order == SquadOrder.Hold)
        {
            return anchor;
        }

        if (_combatManeuverTimer <= 0.0f)
        {
            SelectCombatManeuver(anchor, anchorLeash, hostile, enemyDistance);
            _combatManeuverTimer = 0.85f + SquadSlot * 0.13f;
        }

        if (_combatHasCoverPosition && _combatCoverCommitment > 0.0f)
        {
            return _combatCoverPosition;
        }
        if (!_combatHasSight && _combatMemoryRemaining > 0.0f
            && IsInsideAnchorLeash(_combatLastKnownPosition, anchor, anchorLeash))
        {
            return _combatLastKnownPosition;
        }
        if (enemyDistance < PreferredCombatDistance() * 0.58f)
        {
            var retreat = hostile.GlobalPosition.DirectionTo(GlobalPosition);
            retreat.Y = 0.0f;
            if (retreat.LengthSquared() < 0.01f)
            {
                retreat = GlobalBasis.Z;
            }
            var lateral = new Vector3(-retreat.Z, 0.0f, retreat.X) * _combatStrafeSign;
            var fallback = GlobalPosition + retreat.Normalized() * 3.2f + lateral * 1.8f;
            return IsInsideAnchorLeash(fallback, anchor, anchorLeash) ? fallback : anchor;
        }
        return IsInsideAnchorLeash(_combatFlankPosition, anchor, anchorLeash)
            ? _combatFlankPosition
            : anchor;
    }

    private void SelectCombatManeuver(
        Vector3 anchor,
        float anchorLeash,
        EnemyOperator hostile,
        float enemyDistance)
    {
        var wantsCover = Health / Mathf.Max(1.0f, MaxHealth) < 0.72f
            || (!_combatHasSight && _combatMemoryRemaining > 0.0f);
        if (wantsCover)
        {
            var threatPosition = _combatHasSight ? hostile.GlobalPosition : _combatLastKnownPosition;
            var cover = Main.FindCoverPoint(GlobalPosition, threatPosition);
            if (cover.Y > -500.0f
                && cover.DistanceTo(GlobalPosition) <= 18.0f
                && IsInsideAnchorLeash(cover, anchor, anchorLeash))
            {
                _combatCoverPosition = cover;
                _combatHasCoverPosition = true;
                _combatCoverCommitment = 2.4f;
                CombatCoverSelections++;
                return;
            }
        }

        _combatHasCoverPosition = false;
        var threat = _combatHasSight ? hostile.GlobalPosition : _combatLastKnownPosition;
        var radial = threat.DirectionTo(GlobalPosition);
        radial.Y = 0.0f;
        if (radial.LengthSquared() < 0.01f)
        {
            radial = GlobalBasis.Z;
        }
        radial = radial.Normalized();
        var tangent = new Vector3(-radial.Z, 0.0f, radial.X) * _combatStrafeSign;
        var preferred = PreferredCombatDistance();
        var radialDistance = Mathf.Clamp(enemyDistance - preferred, -3.0f, 3.0f);
        _combatFlankPosition = GlobalPosition
            + tangent * (Role == OperatorRole.Recon ? 4.2f : 3.2f)
            - radial * radialDistance;
    }

    private float PreferredCombatDistance() => Role switch
    {
        OperatorRole.Recon => 20.0f,
        OperatorRole.Medic => 15.0f,
        _ => 10.0f
    };

    private static bool IsInsideAnchorLeash(Vector3 point, Vector3 anchor, float leash)
    {
        var flatPoint = new Vector2(point.X, point.Z);
        var flatAnchor = new Vector2(anchor.X, anchor.Z);
        return flatPoint.DistanceTo(flatAnchor) <= leash;
    }

    private Vector3 FlattenToCurrentHeight(Vector3 point)
        => new(point.X, GlobalPosition.Y, point.Z);

    private void FaceTacticalPoint(Vector3 point, float delta)
    {
        point.Y = GlobalPosition.Y;
        if (GlobalPosition.DistanceSquaredTo(point) <= 0.04f)
        {
            return;
        }
        var desiredYaw = GlobalTransform.LookingAt(point, Vector3.Up).Basis.GetEuler().Y;
        var rotation = Rotation;
        rotation.Y = Mathf.LerpAngle(rotation.Y, desiredYaw, delta * 7.0f);
        Rotation = rotation;
    }

    private Vector3 AvoidObstacle(Vector3 desired)
    {
        var left = new Vector3(-desired.Z, 0.0f, desired.X);
        if (_combatAvoidanceTimer > 0.0f)
        {
            return (desired * 0.22f + left * _combatStrafeSign).Normalized();
        }

        var forwardClearance = MeasureMovementClearance(desired, 1.6f);
        if (forwardClearance >= 1.35f)
        {
            return desired;
        }
        var leftScore = MeasureMovementClearance(left, 2.1f)
            + MeasureMovementClearance((desired * 0.35f + left).Normalized(), 2.4f);
        var rightScore = MeasureMovementClearance(-left, 2.1f)
            + MeasureMovementClearance((desired * 0.35f - left).Normalized(), 2.4f);
        _combatStrafeSign = leftScore >= rightScore ? 1.0f : -1.0f;
        _combatAvoidanceTimer = 0.7f;
        return (desired * 0.22f + left * _combatStrafeSign).Normalized();
    }

    private float MeasureMovementClearance(Vector3 direction, float maxDistance)
    {
        var from = GlobalPosition + Vector3.Up * 0.8f;
        var query = PhysicsRayQueryParameters3D.Create(from, from + direction * maxDistance);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0
            ? maxDistance
            : from.DistanceTo(hit["position"].AsVector3());
    }

    private void TrackTacticalMovement(float delta)
    {
        _combatProgressTimer += delta;
        if (_combatProgressTimer < 0.65f)
        {
            return;
        }
        var progress = GlobalPosition.DistanceTo(_combatProgressOrigin);
        if (_combatMoveRequested && progress < 0.24f)
        {
            _combatStrafeSign *= -1.0f;
            _combatAvoidanceTimer = 1.25f;
            _combatManeuverTimer = 0.0f;
            _combatHasCoverPosition = false;
            CombatStuckRecoveries++;
        }
        _combatProgressOrigin = GlobalPosition;
        _combatProgressTimer = 0.0f;
    }

    private void RegisterCombatThreat(EnemyOperator threat)
    {
        _combatThreat = threat;
        _combatThreatAge = 0.0f;
        AssignCombatTarget(threat, DamageContactMemory);
    }

    private void OnCombatIncapacitated()
    {
        ClearCombatTarget();
        _combatThreat = null;
        _lootHuntSource = null;
        _combatMoveRequested = false;
        _combatDesiredDirection = Vector3.Zero;
        Velocity = Vector3.Zero;
        ResetMovementProgress();
    }

    private void ResetMovementProgress()
    {
        _combatProgressOrigin = GlobalPosition;
        _combatProgressTimer = 0.0f;
        _combatAvoidanceTimer = 0.0f;
        _combatMoveRequested = false;
        _combatDesiredDirection = Vector3.Zero;
    }
}
