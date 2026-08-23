using System;
using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private const float PursuitTrailAttachRange = 28.0f;
    private const float PursuitTrailAttachHeight = 1.65f;
    private const float PursuitTrailWaypointHorizontal = 0.52f;
    private const float PursuitTrailWaypointVertical = 0.72f;
    private const float PursuitFootstepHearingRange = 24.0f;
    private const float PursuitFootstepMinimumSpeedSquared = 0.36f;
    private const ulong PursuitTrailAttachRetryMilliseconds = 360;
    private const ulong PursuitTrailShortcutMilliseconds = 240;
    private const int PursuitTrailAttachCandidateCount = 6;
    private const int PursuitTrailShortcutSamples = 12;
    private const float PursuitTrailShortcutMaximumHeight = 0.46f;

    private CombatMovementTrail? _pursuitTargetTrail;
    private ulong _pursuitTrailTargetId;
    private int _pursuitTrailRevision = -1;
    private long _pursuitTrailCursor = -1;
    private long _pursuitTrailConfirmedEnd = -1;
    private long _pursuitTrailCompletedEnd = -1;
    private ulong _pursuitNextTrailAttachMilliseconds;
    private ulong _pursuitNextTrailShortcutMilliseconds;
    private SquadNavigationDirective[] _pursuitStaticRoute = Array.Empty<SquadNavigationDirective>();
    private int _pursuitStaticRouteCursor;
    private ulong _pursuitStaticRouteTargetId;
    private Vector3 _pursuitStaticRouteDestination;
    private ulong _pursuitNextStaticPlanMilliseconds;
    private int _pursuitStaticPlanFailures;
    internal int PursuitTrailAttachmentsForDiagnostics { get; private set; }
    internal int PursuitTrailWaypointAdvancesForDiagnostics { get; private set; }
    internal int PursuitStaticPlansForDiagnostics { get; private set; }
    internal int PursuitRouteRecoveriesForDiagnostics { get; private set; }
    internal int PursuitNavigationStepUpsForDiagnostics { get; private set; }

    private readonly record struct PursuitTrailAttachCandidate(
        long Sequence,
        Vector3 Point,
        float Score);

    private bool HasActivePursuitNavigationRoute
        => _pursuitTrailCursor >= 0
            || _pursuitStaticRouteCursor < _pursuitStaticRoute.Length;

    private bool ShouldUseVisiblePursuitNavigation(Node3D? target)
        => !SentryMode
            && target is not null
            && (HasActivePursuitNavigationRoute
                || Mathf.Abs(target.GlobalPosition.Y - GlobalPosition.Y) > 0.85f);

    private bool UpdatePursuitNavigationMovement(
        float delta,
        Node3D? target,
        Vector3 fallback,
        float speed,
        bool requireRoute)
    {
        var routed = TryResolvePursuitNavigationDestination(
            target,
            fallback,
            out var destination,
            out var precise);
        if (requireRoute && !routed)
        {
            return false;
        }

        if (Main is not null
            && Main.TryPrepareAiDoorTraversal(GlobalPosition, destination, out var doorWaiting)
            && doorWaiting)
        {
            var doorVelocity = Velocity;
            doorVelocity.X = Mathf.MoveToward(doorVelocity.X, 0.0f, delta * 18.0f);
            doorVelocity.Z = Mathf.MoveToward(doorVelocity.Z, 0.0f, delta * 18.0f);
            Velocity = doorVelocity;
            _pursuitProgressOrigin = GlobalPosition;
            _pursuitProgressTimer = 0.0f;
            ResetPursuitNavigationMotorFrame();
            return true;
        }

        var targetFlat = new Vector3(destination.X, GlobalPosition.Y, destination.Z);
        var direction = GlobalPosition.DirectionTo(targetFlat);
        direction.Y = 0.0f;
        if (routed)
        {
            direction = MaintainPursuitStairDirection(direction, destination);
        }
        var wantsMove = direction.LengthSquared() > 0.02f;
        TrackPursuitProgress(delta, wantsMove, routed);
        if (wantsMove)
        {
            direction = direction.Normalized();
            if (!precise)
            {
                direction = ApplyPursuitObstacleAvoidance(direction);
            }
            LookAt(GlobalPosition + direction, Vector3.Up);
        }

        var movement = wantsMove ? direction * speed : Vector3.Zero;
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, movement.X, delta * 15.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, movement.Z, delta * 15.0f);
        Velocity = velocity;
        if (routed)
        {
            PreparePursuitNavigationMotor(direction, destination);
        }
        return routed;
    }

    private void ConfirmPursuitNavigationContact(Node3D target)
    {
        if (target is not ICombatMovementTrailSource source)
        {
            ClearPursuitNavigationRoutes();
            return;
        }

        var trail = source.CombatMovementTrail;
        var targetId = target.GetInstanceId();
        if (!ReferenceEquals(_pursuitTargetTrail, trail)
            || _pursuitTrailTargetId != targetId
            || _pursuitTrailRevision != trail.Revision)
        {
            ClearPursuitNavigationRoutes();
            _pursuitTargetTrail = trail;
            _pursuitTrailTargetId = targetId;
            _pursuitTrailRevision = trail.Revision;
        }
        _pursuitTrailConfirmedEnd = trail.LatestSequence;
    }

    private void RefreshAudiblePursuitTrail(Node3D? target)
    {
        if (target is not CharacterBody3D body
            || target is not ICombatMovementTrailSource source
            || !ReferenceEquals(_pursuitTargetTrail, source.CombatMovementTrail)
            || _pursuitTrailRevision != source.CombatMovementTrail.Revision)
        {
            return;
        }

        var horizontalVelocitySquared = body.Velocity.X * body.Velocity.X
            + body.Velocity.Z * body.Velocity.Z;
        if (horizontalVelocitySquared < PursuitFootstepMinimumSpeedSquared
            || GlobalPosition.DistanceSquaredTo(body.GlobalPosition)
                > PursuitFootstepHearingRange * PursuitFootstepHearingRange)
        {
            return;
        }
        _pursuitTrailConfirmedEnd = source.CombatMovementTrail.LatestSequence;
    }

    private bool TryResolvePursuitNavigationDestination(
        Node3D? target,
        Vector3 fallback,
        out Vector3 destination,
        out bool precise)
    {
        destination = fallback;
        precise = false;
        if (target is null || !IsInstanceValid(target))
        {
            return false;
        }

        if (TryResolvePursuitTrailDestination(target, out var routedDestination))
        {
            destination = routedDestination;
            precise = true;
            return true;
        }
        if (TryResolveStaticPursuitDestination(target, fallback, out routedDestination))
        {
            destination = routedDestination;
            precise = true;
            return true;
        }
        return false;
    }

    private bool TryResolvePursuitTrailDestination(Node3D target, out Vector3 destination)
    {
        destination = default;
        if (target is not ICombatMovementTrailSource source)
        {
            InvalidatePursuitTrailRoute();
            return false;
        }

        var trail = source.CombatMovementTrail;
        if (!ReferenceEquals(_pursuitTargetTrail, trail)
            || _pursuitTrailTargetId != target.GetInstanceId()
            || _pursuitTrailRevision != trail.Revision
            || _pursuitTrailConfirmedEnd < trail.OldestSequence)
        {
            InvalidatePursuitTrailRoute();
            return false;
        }

        var routeEnd = Math.Min(_pursuitTrailConfirmedEnd, trail.LatestSequence);
        if (routeEnd <= _pursuitTrailCompletedEnd)
        {
            InvalidatePursuitTrailRoute();
            return false;
        }
        if (_pursuitTrailCursor >= trail.OldestSequence
            && _pursuitTrailCursor <= routeEnd
            && trail.TryGet(_pursuitTrailCursor, out _))
        {
            AdvancePursuitTrailCursor(trail, routeEnd);
        }
        else
        {
            _pursuitTrailCursor = -1;
        }

        if (_pursuitTrailCursor < trail.OldestSequence || _pursuitTrailCursor > routeEnd)
        {
            if (!TryAttachToPursuitTrail(trail, routeEnd))
            {
                return false;
            }
            AdvancePursuitTrailCursor(trail, routeEnd);
        }

        if (_pursuitTrailCursor < trail.OldestSequence || _pursuitTrailCursor > routeEnd)
        {
            return false;
        }

        TryShortcutPursuitTrail(trail, routeEnd);
        return trail.TryGet(_pursuitTrailCursor, out destination);
    }

    private bool TryAttachToPursuitTrail(CombatMovementTrail trail, long routeEnd)
    {
        var now = Time.GetTicksMsec();
        if (now < _pursuitNextTrailAttachMilliseconds || routeEnd < trail.OldestSequence)
        {
            return false;
        }
        _pursuitNextTrailAttachMilliseconds = now + PursuitTrailAttachRetryMilliseconds
            + GetInstanceId() % 83UL;

        Span<PursuitTrailAttachCandidate> candidates =
            stackalloc PursuitTrailAttachCandidate[PursuitTrailAttachCandidateCount];
        var candidateCount = 0;
        for (var sequence = trail.OldestSequence; sequence <= routeEnd; sequence++)
        {
            if (!trail.TryGet(sequence, out var point)
                || Mathf.Abs(point.Y - GlobalPosition.Y) > PursuitTrailAttachHeight)
            {
                continue;
            }
            var distanceSquared = GlobalPosition.DistanceSquaredTo(point);
            if (distanceSquared > PursuitTrailAttachRange * PursuitTrailAttachRange)
            {
                continue;
            }
            var age = routeEnd - sequence;
            var candidate = new PursuitTrailAttachCandidate(
                sequence,
                point,
                distanceSquared + age * 0.015f);
            InsertPursuitTrailCandidate(candidates, ref candidateCount, candidate);
        }

        for (var index = 0; index < candidateCount; index++)
        {
            var candidate = candidates[index];
            if (!IsPursuitCorridorClear(candidate.Point))
            {
                continue;
            }
            _pursuitTrailCursor = candidate.Sequence;
            _pursuitNextTrailShortcutMilliseconds = now + PursuitTrailShortcutMilliseconds;
            _pursuitRouteStallCount = 0;
            PursuitTrailAttachmentsForDiagnostics++;
            return true;
        }
        return false;
    }

    private static void InsertPursuitTrailCandidate(
        Span<PursuitTrailAttachCandidate> candidates,
        ref int count,
        PursuitTrailAttachCandidate candidate)
    {
        var insertAt = count;
        for (var index = 0; index < count; index++)
        {
            if (candidate.Score < candidates[index].Score)
            {
                insertAt = index;
                break;
            }
        }
        if (insertAt >= candidates.Length)
        {
            return;
        }
        var last = Math.Min(count, candidates.Length - 1);
        for (var index = last; index > insertAt; index--)
        {
            candidates[index] = candidates[index - 1];
        }
        candidates[insertAt] = candidate;
        count = Math.Min(count + 1, candidates.Length);
    }

    private void AdvancePursuitTrailCursor(CombatMovementTrail trail, long routeEnd)
    {
        while (_pursuitTrailCursor <= routeEnd
            && trail.TryGet(_pursuitTrailCursor, out var point)
            && IsPursuitWaypointReached(point))
        {
            _pursuitTrailCursor++;
            PursuitTrailWaypointAdvancesForDiagnostics++;
        }
        if (_pursuitTrailCursor > routeEnd)
        {
            _pursuitTrailCompletedEnd = Math.Max(_pursuitTrailCompletedEnd, routeEnd);
            _pursuitTrailCursor = -1;
        }
    }

    private void TryShortcutPursuitTrail(CombatMovementTrail trail, long routeEnd)
    {
        var now = Time.GetTicksMsec();
        if (now < _pursuitNextTrailShortcutMilliseconds
            || _pursuitTrailCursor < trail.OldestSequence)
        {
            return;
        }
        _pursuitNextTrailShortcutMilliseconds = now + PursuitTrailShortcutMilliseconds;
        var furthest = Math.Min(routeEnd, _pursuitTrailCursor + PursuitTrailShortcutSamples);
        for (var sequence = furthest; sequence > _pursuitTrailCursor; sequence--)
        {
            if (!trail.TryGet(sequence, out var point)
                || Mathf.Abs(point.Y - GlobalPosition.Y) > PursuitTrailShortcutMaximumHeight
                || !IsPursuitCorridorClear(point))
            {
                continue;
            }
            PursuitTrailWaypointAdvancesForDiagnostics += (int)(sequence - _pursuitTrailCursor);
            _pursuitTrailCursor = sequence;
            return;
        }
    }

    private bool TryResolveStaticPursuitDestination(
        Node3D target,
        Vector3 fallback,
        out Vector3 destination)
    {
        destination = default;
        var targetId = target.GetInstanceId();
        if (_pursuitStaticRoute.Length > 0
            && _pursuitStaticRouteTargetId == targetId
            && _pursuitStaticRouteDestination.DistanceSquaredTo(fallback) <= 9.0f)
        {
            AdvanceStaticPursuitRoute();
            if (_pursuitStaticRouteCursor < _pursuitStaticRoute.Length)
            {
                destination = _pursuitStaticRoute[_pursuitStaticRouteCursor].Target;
                return true;
            }
            InvalidateStaticPursuitRoute();
        }

        var now = Time.GetTicksMsec();
        if (Main is null || now < _pursuitNextStaticPlanMilliseconds)
        {
            return false;
        }

        if (!Main.TryPlanOperatorPursuitRoute(this, fallback, out var route))
        {
            _pursuitStaticPlanFailures++;
            _pursuitNextStaticPlanMilliseconds = now
                + (ulong)Math.Min(2400, 300 << Math.Min(_pursuitStaticPlanFailures, 3));
            return false;
        }

        _pursuitStaticRoute = route;
        _pursuitStaticRouteCursor = 0;
        _pursuitStaticRouteTargetId = targetId;
        _pursuitStaticRouteDestination = fallback;
        _pursuitStaticPlanFailures = 0;
        _pursuitNextStaticPlanMilliseconds = now + 900;
        PursuitStaticPlansForDiagnostics++;
        AdvanceStaticPursuitRoute();
        if (_pursuitStaticRouteCursor >= _pursuitStaticRoute.Length)
        {
            InvalidateStaticPursuitRoute();
            return false;
        }
        destination = _pursuitStaticRoute[_pursuitStaticRouteCursor].Target;
        return true;
    }

    private void AdvanceStaticPursuitRoute()
    {
        while (_pursuitStaticRouteCursor < _pursuitStaticRoute.Length
            && IsPursuitDirectiveReached(_pursuitStaticRoute, _pursuitStaticRouteCursor))
        {
            _pursuitStaticRouteCursor++;
            PursuitTrailWaypointAdvancesForDiagnostics++;
        }
    }

    private bool IsPursuitDirectiveReached(SquadNavigationDirective[] route, int cursor)
    {
        var directive = route[cursor];
        if (directive.Kind != SquadTraversalKind.Step)
        {
            return IsPursuitWaypointReached(directive.Target);
        }

        var horizontal = new Vector2(
            GlobalPosition.X - directive.Target.X,
            GlobalPosition.Z - directive.Target.Z).Length();
        var descending = cursor + 1 < route.Length
            && route[cursor + 1].DirectedEdgeId == directive.DirectedEdgeId
            && route[cursor + 1].Target.Y < directive.Target.Y;
        var elevationReached = descending
            ? GlobalPosition.Y <= directive.Target.Y + 0.32f
            : GlobalPosition.Y >= directive.Target.Y - 0.32f;
        return elevationReached && horizontal <= 0.68f;
    }

    private bool IsPursuitWaypointReached(Vector3 point)
    {
        var horizontal = new Vector2(
            GlobalPosition.X - point.X,
            GlobalPosition.Z - point.Z).Length();
        return horizontal <= PursuitTrailWaypointHorizontal
            && Mathf.Abs(GlobalPosition.Y - point.Y) <= PursuitTrailWaypointVertical;
    }

    private void ClearPursuitNavigationRoutes()
    {
        _pursuitTargetTrail = null;
        _pursuitTrailTargetId = 0;
        _pursuitTrailRevision = -1;
        _pursuitTrailConfirmedEnd = -1;
        _pursuitTrailCompletedEnd = -1;
        InvalidatePursuitTrailRoute();
        InvalidateStaticPursuitRoute();
        _pursuitRouteStallCount = 0;
        _lastPursuitStairDirection = Vector3.Zero;
    }

    private void InvalidatePursuitTrailRoute()
    {
        _pursuitTrailCursor = -1;
        _pursuitNextTrailShortcutMilliseconds = 0;
    }

    private void InvalidateStaticPursuitRoute()
    {
        _pursuitStaticRoute = Array.Empty<SquadNavigationDirective>();
        _pursuitStaticRouteCursor = 0;
        _pursuitStaticRouteTargetId = 0;
        _pursuitStaticRouteDestination = default;
        if (_pursuitStaticPlanFailures > 0)
        {
            _pursuitNextStaticPlanMilliseconds = Time.GetTicksMsec()
                + (ulong)Math.Min(2400, 300 << Math.Min(_pursuitStaticPlanFailures, 3));
        }
    }

    private void ResetPursuitNavigationForDiagnostics()
    {
        ClearPursuitNavigationRoutes();
        _pursuitNextTrailAttachMilliseconds = 0;
        _pursuitNextStaticPlanMilliseconds = 0;
        _pursuitStaticPlanFailures = 0;
        PursuitTrailAttachmentsForDiagnostics = 0;
        PursuitTrailWaypointAdvancesForDiagnostics = 0;
        PursuitStaticPlansForDiagnostics = 0;
        PursuitRouteRecoveriesForDiagnostics = 0;
        PursuitNavigationStepUpsForDiagnostics = 0;
    }
}
