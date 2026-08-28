using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const float CombatAcquireRange = 68.0f;
    private const float CombatRetainRange = 78.0f;
    private const float VisibleContactMemory = 8.0f;
    private const float DamageContactMemory = 12.0f;
    private const float NavigationRecoveryMaximumSpeed = 3.0f;
    private const float RequiredStepRecoveryDuration = 0.48f;
    private const float RequiredStepRecoveryMaximumSpeed = 2.4f;
    private const float FollowFormationArrivalDistance = 1.0f;
    private const float FollowFormationResumeDistance = 1.65f;
    private const float FollowFormationMaximumHeightDifference = 0.45f;
    private const float ClearAvoidanceReuseSeconds = 0.1f;
    private const float ClearAvoidanceDirectionDot = 0.97f;

    private EnemyOperator? _combatTarget;
    private EnemyOperator? _combatThreat;
    private float _combatThreatAge;
    private float _combatMemoryRemaining;
    private float _combatSightTimer;
    private float _combatTargetScanTimer;
    private float _combatManeuverTimer;
    private float _combatCoverCommitment;
    private float _combatAvoidanceTimer;
    private float _combatClearanceReuseTimer;
    private float _combatRecoveryTimer;
    private float _combatStrafeSign;
    private float _combatFlankSide;
    private Vector3 _combatLastKnownPosition;
    private Vector3 _combatCoverPosition;
    private Vector3 _combatFlankPosition;
    private Vector3 _combatRecoveryDirection;
    private Vector3 _combatClearanceDirection;
    private Vector3 _combatEngagementAnchor;
    private Vector3 _combatDesiredDirection;
    private Vector3 _combatPathDirection;
    private Vector3 _combatProgressOrigin;
    private float _combatProgressTimer;
    private bool _combatMoveRequested;
    private bool _requiredStepRecoveryActive;
    private bool _combatHasSight;
    private bool _combatHasCoverPosition;
    private bool _combatHasEngagementAnchor;
    private bool _followFormationSettled;
    private int _burstShotsRemaining;
    private int _combatNavigationStallCount;

    public int CombatShotsFired { get; private set; }
    public int CombatTargetSwitches { get; private set; }
    public int CombatCoverSelections { get; private set; }
    public int CombatFlankSelections { get; private set; }
    public int CombatStuckRecoveries { get; private set; }
    internal int RequiredStepRecoveriesForDiagnostics { get; private set; }
    internal bool RequiredStepRecoveryActiveForDiagnostics => _requiredStepRecoveryActive
        && _combatRecoveryTimer > 0.0f;
    internal Vector3 RequiredStepRecoveryDirectionForDiagnostics => _requiredStepRecoveryActive
        ? _combatRecoveryDirection
        : Vector3.Zero;
    internal bool CombatHasSightForDiagnostics => _combatHasSight;
    internal EnemyOperator? CombatTargetForDiagnostics => _combatTarget;
    internal Vector3 CombatFlankPositionForDiagnostics => _combatFlankPosition;
    internal int MovementClearanceProbesForDiagnostics { get; private set; }
    internal int ClearAvoidanceReusesForDiagnostics { get; private set; }
    internal int FollowFormationHoldFramesForDiagnostics { get; private set; }
    internal int MovementRequestTransitionsForDiagnostics { get; private set; }

    private void InitializeCombatTactics()
    {
        _combatStrafeSign = SquadSlot % 2 == 0 ? 1.0f : -1.0f;
        _combatFlankSide = _combatStrafeSign;
        _combatProgressOrigin = GlobalPosition;
        _combatLastKnownPosition = GlobalPosition;
        _combatSightTimer = 0.0f;
        _combatTargetScanTimer = 0.0f;
        _combatManeuverTimer = 0.0f;
        _combatRecoveryTimer = 0.0f;
        _combatClearanceReuseTimer = 0.0f;
        _combatClearanceDirection = Vector3.Zero;
        _combatHasSight = false;
        _combatHasEngagementAnchor = false;
        _followFormationSettled = false;
        _burstShotsRemaining = 0;
        _combatNavigationStallCount = 0;
        CombatShotsFired = 0;
        CombatTargetSwitches = 0;
        CombatCoverSelections = 0;
        CombatFlankSelections = 0;
        CombatStuckRecoveries = 0;
    }

    private void OnSquadOrderChanged()
    {
        _combatManeuverTimer = 0.0f;
        _combatCoverCommitment = 0.0f;
        _combatHasCoverPosition = false;
        _combatStrafeSign = SquadSlot % 2 == 0 ? 1.0f : -1.0f;
        _combatFlankSide = _combatStrafeSign;
        _combatRecoveryTimer = 0.0f;
        _combatClearanceReuseTimer = 0.0f;
        _combatClearanceDirection = Vector3.Zero;
        _followFormationSettled = false;
        _burstShotsRemaining = 0;
        _combatNavigationStallCount = 0;
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
        _combatClearanceReuseTimer = Mathf.Max(0.0f, _combatClearanceReuseTimer - delta);
        _combatRecoveryTimer = Mathf.Max(0.0f, _combatRecoveryTimer - delta);

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
        else
        {
            // Leader's focus mark outranks ordinary scanning, never self-defence.
            var focus = Main.SquadFocusTarget;
            if (focus is not null
                && IsInstanceValid(focus)
                && !focus.IsDead
                && Main.CanSquadEngage(focus)
                && GlobalPosition.DistanceTo(focus.GlobalPosition) < CombatRetainRange)
            {
                candidate = focus;
            }
            else if (_combatTargetScanTimer <= 0.0f)
            {
                candidate = SelectBestCombatCandidate();
                _combatTargetScanTimer = 0.42f + SquadSlot * 0.04f;
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
            else
            {
                if (_combatTarget.IsScanned)
                {
                    _combatLastKnownPosition = _combatTarget.GlobalPosition;
                    _combatMemoryRemaining = Mathf.Max(_combatMemoryRemaining, 3.0f);
                }
            }
        }

        if (!_combatHasSight && _combatMemoryRemaining <= 0.0f)
        {
            ClearCombatTarget();
            return null;
        }
        return _combatTarget;
    }

    private EnemyOperator? SelectBestCombatCandidate()
    {
        EnemyOperator? best = null;
        var bestScore = float.PositiveInfinity;
        foreach (var candidate in Main.EnumerateSquadEnemies())
        {
            var distance = GlobalPosition.DistanceTo(candidate.GlobalPosition);
            if (distance > CombatAcquireRange)
            {
                continue;
            }
            var score = CombatTargetScore(candidate, distance, HasLineOfSight(candidate));
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    private float CombatTargetScore(EnemyOperator candidate, float distance, bool visible)
    {
        var score = distance + Mathf.Abs(distance - PreferredCombatDistance()) * 0.16f;
        if (visible)
        {
            score -= 13.0f;
        }
        else
        {
            score += 4.0f;
        }
        if (candidate == _combatTarget)
        {
            score -= 10.0f;
        }
        if (candidate == _combatThreat)
        {
            score -= 24.0f;
        }
        if (candidate == Main.SquadFocusTarget)
        {
            score -= 30.0f;
        }
        if (candidate.EngageTargetNode == this)
        {
            score -= 16.0f;
        }
        else if (candidate.EngageTargetNode == Leader)
        {
            score -= 8.0f;
        }
        if (candidate.IsScanned)
        {
            score -= 3.0f;
        }
        return score;
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
        var currentDistance = GlobalPosition.DistanceTo(_combatTarget.GlobalPosition);
        var candidateDistance = GlobalPosition.DistanceTo(candidate.GlobalPosition);
        var currentScore = CombatTargetScore(_combatTarget, currentDistance, _combatHasSight);
        var candidateScore = CombatTargetScore(candidate, candidateDistance, HasLineOfSight(candidate));
        return candidateScore + 2.5f < currentScore;
    }

    private void AssignCombatTarget(EnemyOperator target, float memorySeconds)
    {
        if (_combatTarget != target)
        {
            CombatTargetSwitches++;
            _burstShotsRemaining = 0;
            _combatHasCoverPosition = false;
            _combatManeuverTimer = 0.0f;
            _combatFlankSide = SquadSlot % 2 == 0 ? 1.0f : -1.0f;
            _combatEngagementAnchor = Leader.IsDead ? GlobalPosition : ResolveFormationDestination();
            _combatHasEngagementAnchor = true;
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
        _combatHasEngagementAnchor = false;
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
        if (Order != SquadOrder.Follow)
        {
            return _orderPosition;
        }
        if (Leader.IsDead && _combatHasEngagementAnchor)
        {
            return _combatEngagementAnchor;
        }
        return Leader.GlobalPosition
            + Leader.GlobalBasis.X * formation.X
            + Leader.GlobalBasis.Z * formation.Z;
    }

    private Vector3 ResolveTacticalDestination(
        Vector3 anchorDestination,
        EnemyOperator? hostile,
        bool objectivePriority)
    {
        return hostile is not null
            && IsInstanceValid(hostile)
            && !hostile.IsDead
            && !objectivePriority
                ? ResolveCombatDestination(anchorDestination, hostile)
                : anchorDestination;
    }

    private bool ShouldHoldFollowFormation(
        Vector3 destination,
        EnemyOperator? hostile,
        bool objectivePriority)
    {
        if (Order != SquadOrder.Follow
            || hostile is not null
            || objectivePriority
            || Leader.IsDead
            || Mathf.Abs(GlobalPosition.Y - destination.Y)
                > FollowFormationMaximumHeightDifference)
        {
            _followFormationSettled = false;
            return false;
        }

        var threshold = _followFormationSettled
            ? FollowFormationResumeDistance
            : FollowFormationArrivalDistance;
        if (GlobalPosition.DistanceSquaredTo(destination) > threshold * threshold)
        {
            _followFormationSettled = false;
            return false;
        }

        _followFormationSettled = true;
        FollowFormationHoldFramesForDiagnostics++;
        return true;
    }

    private void UpdateTacticalMovement(
        Vector3 anchorDestination,
        EnemyOperator? hostile,
        bool objectivePriority,
        SquadTraversalKind navigationKind,
        bool navigationSteppedDirect,
        bool navigationPreciseTrail,
        float delta)
    {
        var destination = anchorDestination;
        if (Order == SquadOrder.Follow
            && !objectivePriority
            && hostile is null
            && !Leader.IsDead
            && GlobalPosition.DistanceTo(Leader.GlobalPosition) > 42.0f)
        {
            GlobalPosition = Leader.GlobalPosition + Vector3.Up * 0.35f;
            ResetMovementProgress();
        }

        var flatDestination = FlattenToCurrentHeight(destination);
        var distance = GlobalPosition.DistanceTo(flatDestination);
        // Precise and required walk points use a 0.62-0.65 m arrival tolerance.
        // Stop inside that radius so movement cannot deadlock just outside the waypoint.
        var stopDistance = navigationKind == SquadTraversalKind.Step
            ? 0.2f
            : navigationPreciseTrail ? 0.55f : 0.75f;
        var desired = distance > stopDistance
            ? GlobalPosition.DirectionTo(flatDestination)
            : Vector3.Zero;
        desired.Y = 0.0f;
        _combatPathDirection = desired;
        var reviveTargetNode = ActiveReviveTargetNode;
        if (navigationKind == SquadTraversalKind.Step
            && _requiredStepRecoveryActive
            && _combatRecoveryTimer > 0.0f
            && _combatRecoveryDirection.LengthSquared() > 0.01f)
        {
            desired = _combatRecoveryDirection;
        }
        else if (navigationKind == SquadTraversalKind.Step)
        {
            _requiredStepRecoveryActive = false;
            _combatRecoveryTimer = 0.0f;
            _combatRecoveryDirection = Vector3.Zero;
        }
        else
        {
            if (_requiredStepRecoveryActive)
            {
                _requiredStepRecoveryActive = false;
                _combatRecoveryTimer = 0.0f;
                _combatRecoveryDirection = Vector3.Zero;
            }
            else if (_combatRecoveryTimer > 0.0f
                && _combatRecoveryDirection.LengthSquared() > 0.01f)
            {
                desired = _combatRecoveryDirection;
            }
        }
        var moveRequested = desired.LengthSquared() > 0.01f;
        if (moveRequested != _combatMoveRequested)
        {
            MovementRequestTransitionsForDiagnostics++;
        }
        _combatMoveRequested = moveRequested;
        if (_combatMoveRequested)
        {
            desired = desired.Normalized();
            if (navigationKind != SquadTraversalKind.Step
                && !navigationSteppedDirect
                && !navigationPreciseTrail)
            {
                desired = AvoidObstacle(desired);
                desired = ApplySquadSeparation(desired);
            }
        }
        _combatDesiredDirection = desired;

        var spec = OperatorRoles.Spec(Role);
        var boost = Role == OperatorRole.Assault && _overdriveTime > 0.0f ? 1.22f : 1.0f;
        var urgencyDistance = reviveTargetNode is not null
            ? GlobalPosition.DistanceTo(reviveTargetNode.GlobalPosition)
            : distance;
        var speed = (urgencyDistance > 8.0f ? 5.4f : 3.8f) * spec.MovementMultiplier * boost;
        if (_requiredStepRecoveryActive)
        {
            speed = Mathf.Min(speed, RequiredStepRecoveryMaximumSpeed);
        }
        else if (_combatRecoveryTimer > 0.0f)
        {
            speed = Mathf.Min(speed, NavigationRecoveryMaximumSpeed);
        }
        if (_skillActionTime > 0.0f)
        {
            speed *= 0.45f;
        }
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, desired.X * speed, delta * 15.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, desired.Z * speed, delta * 15.0f);
        velocity.Y = IsOnFloor() ? -0.2f : velocity.Y - 22.0f * delta;
        Velocity = velocity;

        var facePoint = hostile is not null && IsInstanceValid(hostile) && _combatHasSight
            ? hostile.GlobalPosition
            : _combatMoveRequested
                ? GlobalPosition + desired
                : flatDestination;
        FaceTacticalPoint(facePoint, delta);
    }

    private Vector3 ResolveCombatDestination(Vector3 anchor, EnemyOperator hostile)
    {
        var anchorLeash = Order switch
        {
            SquadOrder.Hold => 1.25f,
            SquadOrder.Move => 14.0f,
            _ => Leader.IsDead ? 36.0f : 24.0f
        };
        var enemyDistance = GlobalPosition.DistanceTo(hostile.GlobalPosition);
        if (Order == SquadOrder.Hold)
        {
            return anchor;
        }

        if (_combatManeuverTimer <= 0.0f
            || !_combatHasSight && GlobalPosition.DistanceTo(_combatFlankPosition) < 0.85f)
        {
            SelectCombatManeuver(anchor, anchorLeash, hostile, enemyDistance);
            _combatManeuverTimer = _combatHasSight
                ? 0.85f + SquadSlot * 0.13f
                : 2.35f;
        }

        if (_combatHasCoverPosition && _combatCoverCommitment > 0.0f)
        {
            return _combatCoverPosition;
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
        if (!_combatHasSight && _combatMemoryRemaining > 0.0f)
        {
            _combatHasCoverPosition = false;
            _combatCoverCommitment = 0.0f;
            _combatFlankPosition = SelectBlockedSightFlank(anchor, anchorLeash, hostile);
            CombatFlankSelections++;
            return;
        }

        var wantsCover = Health / Mathf.Max(1.0f, MaxHealth) < 0.58f
            || _combatThreat is not null && _combatThreatAge < 1.8f;
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

    private Vector3 SelectBlockedSightFlank(Vector3 anchor, float anchorLeash, EnemyOperator hostile)
    {
        var threat = hostile.IsScanned ? hostile.GlobalPosition : _combatLastKnownPosition;
        var approach = GlobalPosition.DirectionTo(FlattenToCurrentHeight(threat));
        approach.Y = 0.0f;
        if (approach.LengthSquared() < 0.01f)
        {
            approach = -GlobalBasis.Z;
        }
        approach = approach.Normalized();
        var right = new Vector3(-approach.Z, 0.0f, approach.X);
        var preferredSide = _combatFlankSide;
        var best = GlobalPosition + right * preferredSide * 2.8f;
        var bestScore = float.NegativeInfinity;

        foreach (var side in new[] { preferredSide, -preferredSide })
        {
            foreach (var lateralDistance in new[] { 3.2f, 5.0f, 7.0f })
            {
                foreach (var advanceDistance in new[] { 1.4f, 3.2f })
                {
                    var candidate = GlobalPosition
                        + right * side * lateralDistance
                        + approach * advanceDistance;
                    candidate.Y = GlobalPosition.Y;
                    if (!IsInsideAnchorLeash(candidate, anchor, anchorLeash)
                        || !HasGroundSupport(candidate))
                    {
                        continue;
                    }
                    var travelDistance = GlobalPosition.DistanceTo(candidate);
                    var travelDirection = GlobalPosition.DirectionTo(candidate);
                    travelDirection.Y = 0.0f;
                    var clearance = MeasureMovementClearance(
                        travelDirection.Normalized(),
                        Mathf.Min(travelDistance, 4.5f));
                    if (clearance < Mathf.Min(1.15f, travelDistance * 0.52f))
                    {
                        continue;
                    }
                    var predictedSight = HasLineOfSightFrom(candidate, hostile);
                    var score = clearance
                        + (predictedSight ? 20.0f : 0.0f)
                        + (side == preferredSide ? 0.8f : 0.0f)
                        - travelDistance * 0.12f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                        _combatFlankSide = side;
                    }
                }
            }
        }

        if (bestScore > float.NegativeInfinity)
        {
            return best;
        }
        var leftClearance = MeasureMovementClearance(right, 3.0f);
        var rightClearance = MeasureMovementClearance(-right, 3.0f);
        _combatFlankSide = leftClearance >= rightClearance ? 1.0f : -1.0f;
        var fallback = GlobalPosition + right * _combatFlankSide * 2.6f;
        return IsInsideAnchorLeash(fallback, anchor, anchorLeash) ? fallback : anchor;
    }

    private bool HasLineOfSightFrom(Vector3 position, EnemyOperator hostile)
    {
        var from = position + Vector3.Up * 1.35f;
        var to = hostile.GlobalPosition + Vector3.Up * 1.05f;
        if (Main?.IsLineObscuredBySmoke(from, to) == true)
        {
            return false;
        }
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                from,
                to,
                GetRid(),
                uint.MaxValue,
                out var hit))
        {
            return false;
        }
        var collider = hit.Collider;
        return collider == hostile
            || collider is Node node && (hostile.IsAncestorOf(node) || node.IsAncestorOf(hostile));
    }

    private bool HasGroundSupport(Vector3 position)
    {
        var from = position + Vector3.Up * 0.8f;
        return PhysicsRaycast.HasHit(
            GetWorld3D(),
            from,
            position + Vector3.Down * 1.8f,
            NavigationProbeExclusions(),
            1);
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
        var direction = GlobalPosition.DirectionTo(point);
        var desiredYaw = Mathf.Atan2(-direction.X, -direction.Z);
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
        if (_combatClearanceReuseTimer > 0.0f
            && desired.Dot(_combatClearanceDirection) >= ClearAvoidanceDirectionDot)
        {
            ClearAvoidanceReusesForDiagnostics++;
            return desired;
        }

        var forwardClearance = MeasureMovementClearance(desired, 1.6f);
        if (forwardClearance >= 1.35f)
        {
            _combatClearanceDirection = desired;
            _combatClearanceReuseTimer = ClearAvoidanceReuseSeconds;
            return desired;
        }
        _combatClearanceReuseTimer = 0.0f;
        _combatClearanceDirection = Vector3.Zero;
        var leftScore = MeasureMovementClearance(left, 2.1f)
            + MeasureMovementClearance((desired * 0.35f + left).Normalized(), 2.4f);
        var rightScore = MeasureMovementClearance(-left, 2.1f)
            + MeasureMovementClearance((desired * 0.35f - left).Normalized(), 2.4f);
        _combatStrafeSign = leftScore >= rightScore ? 1.0f : -1.0f;
        _combatAvoidanceTimer = 0.7f;
        return (desired * 0.22f + left * _combatStrafeSign).Normalized();
    }

    private Vector3 ApplySquadSeparation(Vector3 desired)
    {
        var separation = Vector3.Zero;
        var mates = Main.SquadMatesForRuntime;
        for (var index = 0; index < mates.Count; index++)
        {
            var mate = mates[index];
            if (mate == this
                || mate.IsDowned
                || mate.IsBodyBag
                || !IsInstanceValid(mate))
            {
                continue;
            }
            var offset = GlobalPosition - mate.GlobalPosition;
            offset.Y = 0.0f;
            var distance = offset.Length();
            if (distance < 0.05f || distance >= 2.4f)
            {
                continue;
            }
            separation += offset.Normalized() * (1.0f - distance / 2.4f);
        }
        if (separation.LengthSquared() < 0.001f)
        {
            return desired;
        }
        return (desired + separation * 0.85f).Normalized();
    }

    private float MeasureMovementClearance(Vector3 direction, float maxDistance)
    {
        MovementClearanceProbesForDiagnostics++;
        var from = GlobalPosition + Vector3.Up * 0.8f;
        return PhysicsRaycast.TryHit(
            GetWorld3D(),
            from,
            from + direction * maxDistance,
            NavigationProbeExclusions(),
            1,
            out var hit)
                ? from.DistanceTo(hit.Position)
                : maxDistance;
    }

    internal float MeasureMovementClearanceForDiagnostics(Vector3 direction, float maxDistance)
        => MeasureMovementClearance(direction, maxDistance);

    internal bool WouldNavigationMotionCollideForDiagnostics(Vector3 motion)
        => TestMove(
            GlobalTransform,
            motion,
            null,
            NavigationTraversalSafeMargin,
            recoveryAsCollision: false,
            maxCollisions: 4);

    private void TrackTacticalMovement(float delta)
    {
        _combatProgressTimer += delta;
        if (_combatProgressTimer < 0.65f)
        {
            return;
        }
        var movement = GlobalPosition - _combatProgressOrigin;
        var progress = movement.Length();
        var pathAdvance = _combatPathDirection.LengthSquared() > 0.01f
            ? movement.Dot(_combatPathDirection.Normalized())
            : 0.0f;
        if (_combatMoveRequested && progress < 0.24f)
        {
            _combatNavigationStallCount++;
            var forward = _combatPathDirection.LengthSquared() > 0.01f
                ? _combatPathDirection.Normalized()
                : _combatDesiredDirection.LengthSquared() > 0.01f
                    ? _combatDesiredDirection.Normalized()
                : -GlobalBasis.Z;
            if (TryBeginTraversalRecovery(forward))
            {
                _combatProgressOrigin = GlobalPosition;
                _combatProgressTimer = 0.0f;
                return;
            }
            if (TrySelectGroundedNavigationRecoveryDirection(forward, 3.0f, out var recovery))
            {
                var left = new Vector3(-forward.Z, 0.0f, forward.X);
                _combatStrafeSign = recovery.Dot(left) >= 0.0f ? 1.0f : -1.0f;
                _combatFlankSide = _combatStrafeSign;
                _combatAvoidanceTimer = 1.25f;
                _combatRecoveryDirection = recovery;
                _combatRecoveryTimer = 1.05f;
                _combatManeuverTimer = 0.0f;
                _combatHasCoverPosition = false;
                CombatStuckRecoveries++;
            }
            if (_combatNavigationStallCount >= 3)
            {
                Main.ReplanSquadNavigationAfterStall(this);
                _combatNavigationStallCount = 0;
            }
        }
        else if (!_combatMoveRequested || pathAdvance > 0.35f)
        {
            _combatNavigationStallCount = 0;
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
        _combatNavigationStallCount = 0;
        _lootHuntSource = null;
        _combatMoveRequested = false;
        _combatDesiredDirection = Vector3.Zero;
        _combatPathDirection = Vector3.Zero;
        Velocity = Vector3.Zero;
        CancelNavigationTraversal();
        ResetMovementProgress();
    }

    private void ResetMovementProgress()
    {
        _combatProgressOrigin = GlobalPosition;
        _combatProgressTimer = 0.0f;
        _combatAvoidanceTimer = 0.0f;
        _combatClearanceReuseTimer = 0.0f;
        _combatClearanceDirection = Vector3.Zero;
        _combatRecoveryTimer = 0.0f;
        _combatRecoveryDirection = Vector3.Zero;
        _requiredStepRecoveryActive = false;
        _combatMoveRequested = false;
        _combatDesiredDirection = Vector3.Zero;
        _combatPathDirection = Vector3.Zero;
        _followFormationSettled = false;
    }

    internal void ResumeFromExtractionDeployment()
    {
        if (IsExtractionPassenger || IsBodyBag)
        {
            return;
        }

        ProcessMode = ProcessModeEnum.Inherit;
        SetPhysicsProcess(true);
        Velocity = Vector3.Zero;
        _reviveTarget = null;
        _revivePoseBlend = 0.0f;
        _lootHuntSource = null;
        _doorWaitTimer = 0.0f;
        _skillActionTime = 0.0f;
        _overdriveTime = 0.0f;
        _combatThreat = null;
        _combatThreatAge = 0.0f;
        _combatTargetScanTimer = 0.0f;
        _combatSightTimer = 0.0f;
        _combatMemoryRemaining = 0.0f;
        ClearCombatTarget();
        ResetMovementProgress();
        if (IsInstanceValid(Main))
        {
            Main.ClearSquadNavigation(this);
        }
        _remotePosition = GlobalPosition;
        _remoteRotation = Rotation;
    }

    internal bool HasCombatLineOfSightForDiagnostics(EnemyOperator hostile)
        => IsInstanceValid(hostile) && !hostile.IsDead && HasLineOfSight(hostile);

    internal void ResetCombatTacticsForDiagnostics()
    {
        ClearCombatTarget();
        _combatThreat = null;
        _combatThreatAge = 100.0f;
        _combatTargetScanTimer = 0.0f;
        _combatManeuverTimer = 0.0f;
        _combatCoverCommitment = 0.0f;
        _combatRecoveryTimer = 0.0f;
        _combatStrafeSign = SquadSlot % 2 == 0 ? 1.0f : -1.0f;
        _combatFlankSide = _combatStrafeSign;
        _burstShotsRemaining = 0;
        _weaponCooldown = 0.0f;
        RefillMagazine();
        _skillActionTime = 0.0f;
        Health = MaxHealth;
        CombatShotsFired = 0;
        CombatTargetSwitches = 0;
        CombatCoverSelections = 0;
        CombatFlankSelections = 0;
        CombatStuckRecoveries = 0;
        _combatNavigationStallCount = 0;
        RequiredStepRecoveriesForDiagnostics = 0;
        ResetMovementProgress();
        UpdateHealthVisual();
    }

    internal void ResetMovementPerformanceCountersForDiagnostics()
    {
        MovementClearanceProbesForDiagnostics = 0;
        ClearAvoidanceReusesForDiagnostics = 0;
        FollowFormationHoldFramesForDiagnostics = 0;
        MovementRequestTransitionsForDiagnostics = 0;
    }
}
