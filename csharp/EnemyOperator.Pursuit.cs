using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private const float RivalPursuitSeconds = 18.0f;
    private const float GarrisonPursuitSeconds = 13.0f;
    private const float SharedPursuitSeconds = 10.0f;
    private const float SquadContactShareRange = 38.0f;

    public bool IsPursuing => !IsDead && _hasLastKnownTarget && _pursuitTimer > 0.0f;
    public Vector3 LastKnownTargetPosition => _lastKnownTargetPosition;
    public float PursuitSecondsRemaining => Mathf.Max(0.0f, _pursuitTimer);
    public int SquadContactsReceived { get; private set; }

    private bool _hasLastKnownTarget;
    private Vector3 _lastKnownTargetPosition;
    private float _pursuitTimer;
    private float _lostSightTimer;
    private float _squadShareCooldown;
    private float _avoidanceHoldTimer;
    private float _avoidanceSide = 1.0f;
    private Vector3 _pursuitProgressOrigin;
    private float _pursuitProgressTimer;
    private bool _scriptedObjectiveAvoiding;
    private float _scriptedObjectiveSide = 1.0f;
    private Vector3 _scriptedObjectiveProgressOrigin;
    private float _scriptedObjectiveProgressTimer;

    private float CurrentPursuitDuration => IsWorldBoss ? 28.0f : IsRivalSquad ? RivalPursuitSeconds : GarrisonPursuitSeconds;

    private void InitializePursuitState()
    {
        _pursuitProgressOrigin = GlobalPosition;
        _avoidanceSide = _strafeSign;
    }

    private void UpdatePursuitTimers(float delta)
    {
        _squadShareCooldown = Mathf.Max(0.0f, _squadShareCooldown - delta);
        _avoidanceHoldTimer = Mathf.Max(0.0f, _avoidanceHoldTimer - delta);
        if (!_hasLastKnownTarget)
        {
            return;
        }

        _pursuitTimer = Mathf.Max(0.0f, _pursuitTimer - delta);
        _lostSightTimer += delta;
        if (_pursuitTimer > 0.0f)
        {
            return;
        }

        var searchEnd = _lastKnownTargetPosition;
        ClearPursuitMemory(clearTarget: true);
        _patrolTarget = searchEnd;
        _patrolTimer = 2.5f;
        Alerted = false;
        Suspicion = Mathf.Min(Suspicion, 30.0f);
    }

    private Node3D? AssignedCombatTargetNode()
    {
        if (_combatTarget is not null)
        {
            return IsAttackableCombatant(_combatTarget) ? _combatTarget.CombatNode : null;
        }
        return _rawTarget is not null && GodotObject.IsInstanceValid(_rawTarget)
            ? _rawTarget
            : null;
    }

    private bool IsValidHostileTarget(Node3D? target)
    {
        if (target is null || !GodotObject.IsInstanceValid(target))
        {
            return false;
        }
        return target switch
        {
            ISquadCombatant combatant => IsAttackableCombatant(combatant),
            EnemyOperator enemy => IsHostileTo(enemy),
            _ => false
        };
    }

    private bool CanRetainPursuitTarget(Node3D? target)
        => IsPursuing
            && IsValidHostileTarget(target)
            && (target is not ISquadCombatant { CombatDowned: true }
                || GlobalPosition.DistanceSquaredTo(target.GlobalPosition)
                    <= DownedFinishAcquireRange * DownedFinishAcquireRange);

    private void AssignCombatTarget(Node3D? target)
    {
        _combatTarget = target as ISquadCombatant;
        _rawTarget = target;
    }

    private void BeginPursuitFromCurrentTarget(bool shareContact)
    {
        var target = AssignedCombatTargetNode();
        if (!IsValidHostileTarget(target))
        {
            return;
        }
        RememberPursuitContact(target!, target!.GlobalPosition, CurrentPursuitDuration, shareContact);
    }

    private void RefreshVisiblePursuitContact()
    {
        var target = AssignedCombatTargetNode();
        if (!IsValidHostileTarget(target))
        {
            return;
        }
        RememberPursuitContact(target!, target!.GlobalPosition, CurrentPursuitDuration, shareContact: true);
    }

    private void RememberInvestigationPoint(Vector3 position, float seconds)
    {
        _hasLastKnownTarget = true;
        _lastKnownTargetPosition = position;
        _pursuitTimer = Mathf.Max(_pursuitTimer, seconds);
        _lostSightTimer = 0.0f;
        _pursuitProgressOrigin = GlobalPosition;
        _pursuitProgressTimer = 0.0f;
    }

    private void RememberPursuitContact(
        Node3D target,
        Vector3 position,
        float seconds,
        bool shareContact)
    {
        AssignCombatTarget(target);
        ConfirmPursuitNavigationContact(target);
        RememberInvestigationPoint(position, seconds);
        Alerted = true;
        Suspicion = 100.0f;
        _searchingLoot = false;
        if (shareContact && _squadShareCooldown <= 0.0f)
        {
            SharePursuitContact(target, position);
            _squadShareCooldown = 0.55f;
        }
    }

    private void SharePursuitContact(Node3D target, Vector3 position)
    {
        ContactShareRequestCountForDiagnostics++;
        if (SuppressesContactSharingForDiagnostics || !IsInsideTree() || Main is null)
        {
            return;
        }
        Main.RelayOperatorContact(this, target, position, SquadContactShareRange);
    }

    internal void ReceiveSharedPursuitContact(Node3D target, Vector3 position)
    {
        if (!IsValidHostileTarget(target))
        {
            return;
        }
        var current = AssignedCombatTargetNode();
        if (IsPursuing && _lostSightTimer < 0.4f && current != target)
        {
            return;
        }
        SquadContactsReceived++;
        AssignCombatTarget(target);
        ConfirmPursuitNavigationContact(target);
        RememberInvestigationPoint(position, SharedPursuitSeconds);
        Alerted = true;
        Suspicion = 100.0f;
        _searchingLoot = false;
    }

    private void RegisterDamageThreat(Node? attacker)
    {
        if (attacker is not Node3D target || !IsValidHostileTarget(target))
        {
            return;
        }
        RememberPursuitContact(target, target.GlobalPosition, CurrentPursuitDuration, shareContact: true);
    }

    private Vector3 CurrentPursuitDestination()
    {
        if (!_hasLastKnownTarget)
        {
            return _patrolTarget;
        }
        var targetFlat = new Vector3(
            _lastKnownTargetPosition.X,
            GlobalPosition.Y,
            _lastKnownTargetPosition.Z);
        if (GlobalPosition.DistanceTo(targetFlat) > 1.65f)
        {
            return _lastKnownTargetPosition;
        }

        var phase = (int)(_lostSightTimer / 1.15f) % 4;
        var offset = phase switch
        {
            0 => new Vector3(2.8f, 0.0f, 0.0f),
            1 => new Vector3(0.0f, 0.0f, 2.8f),
            2 => new Vector3(-2.8f, 0.0f, 0.0f),
            _ => new Vector3(0.0f, 0.0f, -2.8f)
        };
        return _lastKnownTargetPosition + offset;
    }

    private Vector3 CurrentThreatPosition(bool hasSight)
    {
        if (hasSight && AssignedCombatTargetNode() is not null)
        {
            return CurrentTargetPosition();
        }
        return _hasLastKnownTarget ? _lastKnownTargetPosition : _patrolTarget;
    }

    private void UpdateLostContactMovement(float delta)
    {
        if (!IsPursuing || SentryMode)
        {
            HoldSentryPosition(delta);
            return;
        }
        if (IsProne)
        {
            SetProne(false);
        }
        _seekingCover = false;
        _inCover = false;
        _searchingLoot = false;

        var target = AssignedCombatTargetNode();
        RefreshAudiblePursuitTrail(target);
        var speed = HasFireablePrimary ? 5.7f : 6.1f;
        if (IsRivalSquad)
        {
            speed *= 1.08f;
        }
        UpdatePursuitNavigationMovement(
            delta,
            target,
            CurrentPursuitDestination(),
            speed,
            requireRoute: false);
    }

    private Vector3 ApplyPursuitObstacleAvoidance(Vector3 direction)
    {
        if (direction.LengthSquared() < 0.01f)
        {
            return direction;
        }
        direction = direction.Normalized();
        var right = new Vector3(-direction.Z, 0.0f, direction.X);
        var forwardClearance = MeasureStaticClearance(direction, 1.45f);
        if (forwardClearance < 1.25f && _avoidanceHoldTimer <= 0.0f)
        {
            var rightDirection = (direction * 0.45f + right).Normalized();
            var leftDirection = (direction * 0.45f - right).Normalized();
            var rightScore = MeasureStaticClearance(rightDirection, 2.8f)
                + MeasureStaticClearance(right, 1.5f) * 0.35f;
            var leftScore = MeasureStaticClearance(leftDirection, 2.8f)
                + MeasureStaticClearance(-right, 1.5f) * 0.35f;
            _avoidanceSide = rightScore >= leftScore ? 1.0f : -1.0f;
            _avoidanceHoldTimer = 1.2f;
        }

        if (_avoidanceHoldTimer <= 0.0f)
        {
            return direction;
        }
        var side = right * _avoidanceSide;
        if (MeasureStaticClearance(side, 0.9f) < 0.55f)
        {
            _avoidanceSide *= -1.0f;
            side = -side;
        }
        return (direction * 0.28f + side).Normalized();
    }

    internal void ResetScriptedObjectiveNavigation()
    {
        _scriptedObjectiveAvoiding = false;
        _scriptedObjectiveSide = _avoidanceSide;
        _scriptedObjectiveProgressOrigin = GlobalPosition;
        _scriptedObjectiveProgressTimer = 0.0f;
    }

    internal Vector3 ResolveScriptedObjectiveDirection(Vector3 objective, float delta)
    {
        var target = new Vector3(objective.X, GlobalPosition.Y, objective.Z);
        var direction = GlobalPosition.DirectionTo(target);
        direction.Y = 0.0f;
        var distance = GlobalPosition.DistanceTo(target);
        if (direction.LengthSquared() < 0.01f || distance < 0.15f)
        {
            return Vector3.Zero;
        }
        direction = direction.Normalized();

        _scriptedObjectiveProgressTimer += delta;
        if (_scriptedObjectiveProgressTimer >= 0.65f)
        {
            var progress = GlobalPosition.DistanceTo(_scriptedObjectiveProgressOrigin);
            if (_scriptedObjectiveAvoiding && progress < 0.24f)
            {
                _scriptedObjectiveSide *= -1.0f;
            }
            _scriptedObjectiveProgressOrigin = GlobalPosition;
            _scriptedObjectiveProgressTimer = 0.0f;
        }

        var directDistance = Mathf.Min(distance, 18.0f);
        if (MeasureStaticCorridorClearance(direction, directDistance) >= directDistance - 0.08f)
        {
            _scriptedObjectiveAvoiding = false;
            return direction;
        }

        var right = new Vector3(-direction.Z, 0.0f, direction.X);
        if (!_scriptedObjectiveAvoiding)
        {
            var rightDirection = (right + direction * 0.18f).Normalized();
            var leftDirection = (-right + direction * 0.18f).Normalized();
            var rightScore = MeasureStaticCorridorClearance(rightDirection, 4.2f);
            var leftScore = MeasureStaticCorridorClearance(leftDirection, 4.2f);
            _scriptedObjectiveSide = rightScore >= leftScore ? 1.0f : -1.0f;
            _scriptedObjectiveAvoiding = true;
        }

        var side = right * _scriptedObjectiveSide;
        if (MeasureStaticCorridorClearance(side, 1.25f) < 0.72f)
        {
            _scriptedObjectiveSide *= -1.0f;
            side = -side;
        }
        return (side + direction * 0.16f).Normalized();
    }

    internal bool IsScriptedObjectiveCorridorClear(Vector3 objective)
    {
        var target = new Vector3(objective.X, GlobalPosition.Y, objective.Z);
        var offset = target - GlobalPosition;
        offset.Y = 0.0f;
        var distance = offset.Length();
        return distance <= 0.15f
            || MeasureStaticCorridorClearance(offset.Normalized(), distance) >= distance - 0.08f;
    }

    private float MeasureStaticCorridorClearance(Vector3 direction, float maxDistance)
    {
        direction = direction.Normalized();
        var side = new Vector3(-direction.Z, 0.0f, direction.X) * 0.32f;
        var minimum = maxDistance;
        foreach (var offset in new[] { Vector3.Zero, side, -side })
        {
            var from = GlobalPosition + Vector3.Up * 0.78f + offset;
            if (PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    from,
                    from + direction * maxDistance,
                    GetRid(),
                    1,
                    out var hit))
            {
                minimum = Mathf.Min(minimum, from.DistanceTo(hit.Position));
            }
        }
        return minimum;
    }

    private float MeasureStaticClearance(Vector3 direction, float maxDistance)
    {
        var from = GlobalPosition + Vector3.Up * 0.78f;
        return PhysicsRaycast.TryHit(
            GetWorld3D(),
            from,
            from + direction * maxDistance,
            GetRid(),
            1,
            out var hit)
                ? from.DistanceTo(hit.Position)
                : maxDistance;
    }

    private void TrackPursuitProgress(float delta, bool wantsMove, bool followingRoute)
    {
        _pursuitProgressTimer += delta;
        if (_pursuitProgressTimer < 0.65f)
        {
            return;
        }
        var progress = GlobalPosition.DistanceTo(_pursuitProgressOrigin);
        if (wantsMove && progress < 0.28f)
        {
            if (followingRoute)
            {
                RecoverPursuitNavigationRoute();
            }
            else
            {
                _avoidanceSide *= -1.0f;
                _avoidanceHoldTimer = 1.35f;
            }
        }
        else if (!wantsMove || progress >= 0.42f)
        {
            _pursuitRouteStallCount = 0;
        }
        _pursuitProgressOrigin = GlobalPosition;
        _pursuitProgressTimer = 0.0f;
    }

    private void ClearPursuitMemory(bool clearTarget)
    {
        _hasLastKnownTarget = false;
        _pursuitTimer = 0.0f;
        _lostSightTimer = 0.0f;
        _avoidanceHoldTimer = 0.0f;
        _pursuitProgressOrigin = GlobalPosition;
        _pursuitProgressTimer = 0.0f;
        ClearPursuitNavigationRoutes();
        if (clearTarget)
        {
            _combatTarget = null;
            _rawTarget = null;
        }
    }

    private void ResetPursuitStateForDiagnostics()
    {
        ClearPursuitMemory(clearTarget: true);
        SquadContactsReceived = 0;
        _squadShareCooldown = 0.0f;
        _avoidanceSide = _strafeSign;
        ResetPursuitNavigationForDiagnostics();
    }
}
