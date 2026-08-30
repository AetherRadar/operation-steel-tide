using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionSquadRouteReuseDistance = 2.4f;
    private const float DemolitionSquadRouteIntermediateTolerance = 0.7f;
    private const float DemolitionSquadRouteFinalTolerance = 1.15f;
    private const float DemolitionSquadRouteTickSeconds = 1.0f / 60.0f;
    private const int DemolitionSquadRouteMaximumAuthoredReplans = 3;
    private const float DemolitionEscortProjectionReuseDistance = 1.2f;
    private const float DemolitionEscortMinimumMoveDistance = 1.0f;
    private const float DemolitionEscortOpeningFanOutEnterRadius = 7.5f;
    private const float DemolitionEscortOpeningFanOutExitRadius = 10.0f;
    private const int DemolitionEscortMaximumProjectionRoutePlans = 3;
    private const int DemolitionEscortMaximumTotalRoutePlansPerRefresh = 5;
    private const ulong DemolitionEscortProjectionCacheMilliseconds = 900;
    private const ulong DemolitionEscortFallbackRetryMilliseconds = 300;
    private const ulong DemolitionEscortForcedRecoveryRetryMilliseconds = 50;

    private static readonly Vector2[] DemolitionEscortProjectionOffsets =
    {
        new(0.0f, 0.0f),
        new(0.0f, 0.9f),
        new(0.0f, -0.9f),
        new(0.9f, 0.0f),
        new(1.8f, 0.0f),
        new(1.273f, 1.273f),
        new(1.273f, -1.273f),
        new(0.0f, 1.8f),
        new(0.0f, -1.8f),
        new(-1.8f, 0.0f),
        new(2.7f, 0.0f),
        new(0.0f, 2.7f),
        new(0.0f, -2.7f),
        new(-2.7f, 0.0f)
    };

    private readonly record struct DemolitionSquadRouteFallback(
        Vector3 Destination,
        int ReplanCount);

    private readonly record struct DemolitionEscortProjection(
        Vector3 Start,
        Vector3 Preferred,
        Vector3 StrategyFallback,
        Vector3 Resolved,
        ulong ExpiresMilliseconds,
        bool FanOut);

    private readonly Dictionary<SquadMate, DemolitionRouteCursor> _demolitionSquadRoutes = new();
    private readonly Dictionary<SquadMate, DemolitionSquadRouteFallback>
        _demolitionSquadRouteFallbacks = new();
    private readonly Dictionary<SquadMate, DemolitionEscortProjection>
        _demolitionEscortProjections = new();
    private readonly Dictionary<SquadMate, ulong> _demolitionEscortOpeningFanOut = new();
    private readonly Dictionary<SquadMate, ulong> _demolitionEscortForcedRecoveryRetry = new();

    internal int DemolitionSquadRoutePlansForDiagnostics { get; private set; }
    internal int DemolitionSquadRouteReusesForDiagnostics { get; private set; }
    internal int DemolitionEscortProjectionRoutePlansForDiagnostics { get; private set; }
    internal int DemolitionEscortProjectionMaximumPlansForDiagnostics { get; private set; }
    internal int DemolitionEscortTotalRoutePlansForDiagnostics { get; private set; }
    internal int DemolitionEscortMaximumRefreshPlansForDiagnostics { get; private set; }
    internal ulong DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics { get; private set; }
    internal int DemolitionEscortForcedRecoveryRequestsForDiagnostics { get; private set; }

    internal static Vector3 ResolveDemolitionEscortPreferredDestination(
        int squadSlot,
        Vector3 leaderPosition,
        Vector3 forward)
    {
        forward.Y = 0.0f;
        forward = forward.LengthSquared() > 0.01f
            ? forward.Normalized()
            : Vector3.Forward;
        var right = new Vector3(-forward.Z, 0.0f, forward.X);
        var escortIndex = squadSlot > 0 ? squadSlot - 1 : 0;
        var escortRow = escortIndex / 2;
        var escortSide = escortIndex % 2 == 0 ? -1.0f : 1.0f;
        var lateralOffset = escortSide * (1.8f + escortRow * 0.65f);
        var rearOffset = 2.2f + escortRow * 1.8f;
        return leaderPosition
            + right * lateralOffset
            - forward * rearOffset;
    }

    internal bool TryResolveDemolitionEscortDestination(
        SquadMate mate,
        Node3D leader,
        Vector3 preferred,
        out Vector3 destination)
    {
        var refreshStarted = Time.GetTicksUsec();
        var totalRoutePlans = 0;
        bool FinishRefresh(bool success, Vector3 result, out Vector3 output)
        {
            DemolitionEscortTotalRoutePlansForDiagnostics += totalRoutePlans;
            DemolitionEscortMaximumRefreshPlansForDiagnostics = Mathf.Max(
                DemolitionEscortMaximumRefreshPlansForDiagnostics,
                totalRoutePlans);
            DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics = System.Math.Max(
                DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics,
                Time.GetTicksUsec() - refreshStarted);
            output = result;
            return success;
        }

        var layout = DemolitionLayout();
        var hasStrategyFallback = _demolitionSquadAssignmentTargets.TryGetValue(
            mate,
            out var strategyKey);
        var strategyFallback = hasStrategyFallback
            ? layout.StrategyTarget(strategyKey!)
            : preferred;
        var now = Time.GetTicksMsec();
        if (_demolitionEscortForcedRecoveryRetry.TryGetValue(mate, out var retryAt)
            && now < retryAt)
        {
            return FinishRefresh(false, mate.GlobalPosition, out destination);
        }
        _demolitionEscortForcedRecoveryRetry.Remove(mate);
        var reuseDistanceSquared = DemolitionEscortProjectionReuseDistance
            * DemolitionEscortProjectionReuseDistance;

        if (hasStrategyFallback
            && ShouldDemolitionEscortFanOut(mate, layout, leader))
        {
            if (_demolitionEscortProjections.TryGetValue(mate, out var fanOutCached)
                && fanOutCached.FanOut
                && now < fanOutCached.ExpiresMilliseconds
                && fanOutCached.StrategyFallback.DistanceSquaredTo(strategyFallback)
                    <= reuseDistanceSquared)
            {
                return FinishRefresh(true, fanOutCached.Resolved, out destination);
            }
            if (!TryResolveValidatedDemolitionEscortFallback(
                    mate,
                    layout,
                    leader.GlobalPosition,
                    strategyFallback,
                    ref totalRoutePlans,
                    out var fanOutResolved))
            {
                _demolitionEscortProjections.Remove(mate);
                return FinishRefresh(false, mate.GlobalPosition, out destination);
            }
            _demolitionEscortProjections[mate] = new DemolitionEscortProjection(
                mate.GlobalPosition,
                preferred,
                strategyFallback,
                fanOutResolved,
                now + DemolitionEscortProjectionCacheMilliseconds,
                FanOut: true);
            return FinishRefresh(true, fanOutResolved, out destination);
        }

        if (_demolitionEscortProjections.TryGetValue(mate, out var cached)
            && !cached.FanOut
            && now < cached.ExpiresMilliseconds
            && cached.Preferred.DistanceSquaredTo(preferred) <= reuseDistanceSquared
            && cached.StrategyFallback.DistanceSquaredTo(strategyFallback) <= reuseDistanceSquared
            && cached.Start.DistanceSquaredTo(mate.GlobalPosition) <= 9.0f)
        {
            return FinishRefresh(true, cached.Resolved, out destination);
        }

        var planner = _demolitionRoutePlanner ??= new DemolitionRoutePlanner(layout);
        var projected = TryProjectRuntimeDemolitionEscortDestination(
            mate,
            layout,
            planner,
            leader.GlobalPosition,
            preferred,
            out var resolved,
            out var routePlans,
            ref totalRoutePlans);
        DemolitionEscortProjectionRoutePlansForDiagnostics += routePlans;
        DemolitionEscortProjectionMaximumPlansForDiagnostics = Mathf.Max(
            DemolitionEscortProjectionMaximumPlansForDiagnostics,
            routePlans);
        if (!projected)
        {
            if (!TryResolveValidatedDemolitionEscortFallback(
                    mate,
                    layout,
                    leader.GlobalPosition,
                    strategyFallback,
                    ref totalRoutePlans,
                    out resolved))
            {
                _demolitionEscortProjections.Remove(mate);
                return FinishRefresh(false, mate.GlobalPosition, out destination);
            }
        }
        _demolitionEscortProjections[mate] = new DemolitionEscortProjection(
            mate.GlobalPosition,
            preferred,
            strategyFallback,
            resolved,
            now + (projected
                ? DemolitionEscortProjectionCacheMilliseconds
                : DemolitionEscortFallbackRetryMilliseconds),
            FanOut: false);
        return FinishRefresh(true, resolved, out destination);
    }

    internal void RequestDemolitionEscortNavigationRecovery(SquadMate mate)
    {
        if (!IsInstanceValid(mate))
        {
            return;
        }
        var now = Time.GetTicksMsec();
        if (_demolitionEscortForcedRecoveryRetry.TryGetValue(mate, out var pendingRetry)
            && now < pendingRetry)
        {
            return;
        }
        _demolitionEscortProjections.Remove(mate);
        ClearDemolitionSquadRoute(mate);
        _demolitionEscortForcedRecoveryRetry[mate] = now
            + DemolitionEscortForcedRecoveryRetryMilliseconds;
        DemolitionEscortForcedRecoveryRequestsForDiagnostics++;
        mate.RequestNavigationRecovery(forceEscape: true);
    }

    private bool TryProjectRuntimeDemolitionEscortDestination(
        SquadMate mate,
        DemolitionArenaLayout layout,
        DemolitionRoutePlanner planner,
        Vector3 leader,
        Vector3 preferred,
        out Vector3 destination,
        out int routePlans,
        ref int totalRoutePlans)
    {
        routePlans = 0;
        var towardLeader = HorizontalDirection(preferred, leader);
        var lateral = new Vector3(-towardLeader.Z, 0.0f, towardLeader.X);
        foreach (var offset in DemolitionEscortProjectionOffsets)
        {
            var candidate = preferred
                + towardLeader * offset.X
                + lateral * offset.Y;
            if (!TryGroundDemolitionEscortPoint(mate, candidate, out candidate)
                || candidate.DistanceSquaredTo(mate.GlobalPosition)
                    < DemolitionEscortMinimumMoveDistance * DemolitionEscortMinimumMoveDistance
                || !layout.HasCapsulePointClearance(candidate, out _))
            {
                continue;
            }
            if (routePlans >= DemolitionEscortMaximumProjectionRoutePlans)
            {
                break;
            }
            routePlans++;
            var route = TryPlanDemolitionEscortRoute(
                planner,
                mate.GlobalPosition,
                candidate,
                LocalDemolitionSide,
                ref totalRoutePlans);
            if (route is null)
            {
                break;
            }
            if (!route.Value.ReachesDestination
                || route.Value.Waypoints.Count == 0
                || !planner.IsRouteClear(mate.GlobalPosition, route.Value.Waypoints)
                || !IsDemolitionEscortRoutePhysicallyTraversable(
                    mate,
                    mate.GlobalPosition,
                    route.Value.Waypoints))
            {
                continue;
            }
            destination = candidate;
            return true;
        }

        destination = preferred;
        return false;
    }

    private DemolitionRouteResult? TryPlanDemolitionEscortRoute(
        DemolitionRoutePlanner planner,
        Vector3 start,
        Vector3 destination,
        DemolitionTeam movingTeam,
        ref int totalRoutePlans)
    {
        if (totalRoutePlans >= DemolitionEscortMaximumTotalRoutePlansPerRefresh)
        {
            return null;
        }
        totalRoutePlans++;
        return planner.Plan(start, destination, movingTeam);
    }

    private bool IsDemolitionEscortRoutePhysicallyTraversable(
        SquadMate mate,
        Vector3 start,
        IReadOnlyList<Vector3> waypoints,
        int finalWaypoint = int.MaxValue)
    {
        if (waypoints.Count == 0)
        {
            return false;
        }
        var exclude = new Godot.Collections.Array<Rid> { mate.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        var from = start;
        var end = Mathf.Min(waypoints.Count - 1, finalWaypoint);
        for (var index = 0; index <= end; index++)
        {
            var to = waypoints[index];
            var horizontalLength = new Vector2(to.X - from.X, to.Z - from.Z).Length();
            if (!HasSquadCorridorSupport(from, to, horizontalLength, exclude))
            {
                return false;
            }
            from = to;
        }
        return true;
    }

    internal static bool TryProjectDemolitionEscortDestination(
        DemolitionArenaLayout layout,
        DemolitionRoutePlanner planner,
        Vector3 start,
        Vector3 leader,
        Vector3 preferred,
        DemolitionTeam movingTeam,
        out Vector3 destination,
        out int routePlans)
    {
        routePlans = 0;
        var towardLeader = HorizontalDirection(preferred, leader);
        var lateral = new Vector3(-towardLeader.Z, 0.0f, towardLeader.X);
        var minimumMoveDistanceSquared = DemolitionEscortMinimumMoveDistance
            * DemolitionEscortMinimumMoveDistance;
        foreach (var offset in DemolitionEscortProjectionOffsets)
        {
            var candidate = preferred
                + towardLeader * offset.X
                + lateral * offset.Y;
            candidate.Y = preferred.Y;
            if (candidate.DistanceSquaredTo(start) < minimumMoveDistanceSquared
                || !layout.HasCapsulePointClearance(candidate, out _))
            {
                continue;
            }
            if (routePlans >= DemolitionEscortMaximumProjectionRoutePlans)
            {
                break;
            }
            routePlans++;
            var route = planner.Plan(start, candidate, movingTeam);
            if (!route.ReachesDestination
                || route.Waypoints.Count == 0
                || !planner.IsRouteClear(start, route.Waypoints))
            {
                continue;
            }
            destination = candidate;
            return true;
        }

        destination = preferred;
        return false;
    }

    private bool ShouldDemolitionEscortFanOut(
        SquadMate mate,
        DemolitionArenaLayout layout,
        Node3D leader)
    {
        var leaderId = leader.GetInstanceId();
        var alreadyActive = _demolitionEscortOpeningFanOut.TryGetValue(
            mate,
            out var latchedLeaderId)
            && latchedLeaderId == leaderId;
        var radius = alreadyActive
            ? DemolitionEscortOpeningFanOutExitRadius
            : DemolitionEscortOpeningFanOutEnterRadius;
        var radiusSquared = radius * radius;
        foreach (var spawn in layout.AttackSpawns)
        {
            var leaderDelta = leader.GlobalPosition - spawn;
            leaderDelta.Y = 0.0f;
            if (leaderDelta.LengthSquared() <= radiusSquared)
            {
                _demolitionEscortOpeningFanOut[mate] = leaderId;
                return true;
            }
        }
        _demolitionEscortOpeningFanOut.Remove(mate);
        return false;
    }

    private static Vector3 HorizontalDirection(Vector3 from, Vector3 to)
    {
        var direction = to - from;
        direction.Y = 0.0f;
        return direction.LengthSquared() > 0.01f
            ? direction.Normalized()
            : Vector3.Forward;
    }

    private bool TryGroundDemolitionEscortPoint(
        SquadMate mate,
        Vector3 expected,
        out Vector3 grounded)
    {
        grounded = expected;
        var exclude = new Godot.Collections.Array<Rid> { mate.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        if (!TryProbeSquadNavigationSupport(
                expected,
                rayAbove: 0.75f,
                rayBelow: 1.25f,
                maximumAbove: 0.55f,
                maximumBelow: 1.05f,
                exclude,
                out var supportY))
        {
            return false;
        }
        grounded.Y = supportY;
        return true;
    }

    private bool TryResolveValidatedDemolitionEscortFallback(
        SquadMate mate,
        DemolitionArenaLayout layout,
        Vector3 leaderPosition,
        Vector3 strategyFallback,
        ref int totalRoutePlans,
        out Vector3 destination)
    {
        var planner = _demolitionRoutePlanner ??= new DemolitionRoutePlanner(layout);
        if (TryGroundDemolitionEscortPoint(mate, strategyFallback, out var groundedFallback)
            && layout.HasCapsulePointClearance(groundedFallback, out _))
        {
            var route = TryPlanDemolitionEscortRoute(
                planner,
                mate.GlobalPosition,
                groundedFallback,
                LocalDemolitionSide,
                ref totalRoutePlans);
            if (route is not null
                && route.Value.ReachesDestination
                && route.Value.Waypoints.Count > 0
                && planner.IsRouteClear(mate.GlobalPosition, route.Value.Waypoints)
                && IsDemolitionEscortRoutePhysicallyTraversable(
                    mate,
                    mate.GlobalPosition,
                    route.Value.Waypoints))
            {
                destination = groundedFallback;
                return true;
            }
            for (var index = (route?.Waypoints.Count ?? 0) - 1; index >= 0; index--)
            {
                var frontier = route!.Value.Waypoints[index];
                if (frontier.DistanceSquaredTo(mate.GlobalPosition)
                        >= DemolitionEscortMinimumMoveDistance * DemolitionEscortMinimumMoveDistance
                    && TryGroundDemolitionEscortPoint(mate, frontier, out frontier)
                    && layout.HasCapsulePointClearance(frontier, out _)
                    && IsDemolitionEscortRoutePhysicallyTraversable(
                        mate,
                        mate.GlobalPosition,
                        route.Value.Waypoints,
                        finalWaypoint: index))
                {
                    destination = frontier;
                    return true;
                }
            }
        }

        if (TryResolveDemolitionForwardFrontier(
                mate,
                strategyFallback,
                leaderPosition,
                out var forwardFrontier))
        {
            destination = forwardFrontier;
            return true;
        }

        // The carrier's supported feet are a final connected anchor. This case is only
        // reached when an authored strategy post was removed or became physically invalid.
        if (TryGroundDemolitionEscortPoint(mate, leaderPosition, out var leaderAnchor)
            && layout.HasCapsulePointClearance(leaderAnchor, out _))
        {
            var leaderRoute = TryPlanDemolitionEscortRoute(
                planner,
                mate.GlobalPosition,
                leaderAnchor,
                LocalDemolitionSide,
                ref totalRoutePlans);
            if (leaderRoute is not null
                && leaderRoute.Value.ReachesDestination
                && leaderRoute.Value.Waypoints.Count > 0
                && planner.IsRouteClear(mate.GlobalPosition, leaderRoute.Value.Waypoints)
                && IsDemolitionEscortRoutePhysicallyTraversable(
                    mate,
                    mate.GlobalPosition,
                    leaderRoute.Value.Waypoints))
            {
                destination = leaderAnchor;
                return true;
            }
        }

        if (_demolitionEscortProjections.TryGetValue(mate, out var previous)
            && previous.Resolved.DistanceSquaredTo(mate.GlobalPosition)
                >= DemolitionEscortMinimumMoveDistance * DemolitionEscortMinimumMoveDistance
            && TryGroundDemolitionEscortPoint(mate, previous.Resolved, out var previousResolved)
            && layout.HasCapsulePointClearance(previousResolved, out _)
            && IsSquadMovementCorridorClear(mate.GlobalPosition, previousResolved, mate))
        {
            destination = previousResolved;
            return true;
        }

        // Airborne, dynamically boxed, and transiently unsupported states are normal
        // runtime conditions. Report an explicit miss so the caller can hold this frame,
        // request an escape recovery, and retry shortly without accepting an unsafe target.
        destination = mate.GlobalPosition;
        return false;
    }

    private bool TryResolveDemolitionForwardFrontier(
        SquadMate mate,
        Vector3 destination,
        Vector3 secondaryDirectionTarget,
        out Vector3 frontier)
    {
        var layout = DemolitionLayout();
        var primary = HorizontalDirection(mate.GlobalPosition, destination);
        var secondary = HorizontalDirection(mate.GlobalPosition, secondaryDirectionTarget);
        var directions = new[]
        {
            primary,
            (primary + new Vector3(-primary.Z, 0.0f, primary.X)).Normalized(),
            (primary + new Vector3(primary.Z, 0.0f, -primary.X)).Normalized(),
            secondary,
            new Vector3(-primary.Z, 0.0f, primary.X),
            new Vector3(primary.Z, 0.0f, -primary.X),
            -primary,
            (-primary + new Vector3(-primary.Z, 0.0f, primary.X)).Normalized(),
            (-primary + new Vector3(primary.Z, 0.0f, -primary.X)).Normalized()
        };
        foreach (var distance in new[] { 2.8f, 1.9f, 1.15f })
        {
            foreach (var direction in directions)
            {
                var candidate = mate.GlobalPosition + direction * distance;
                if (!TryGroundDemolitionEscortPoint(mate, candidate, out candidate)
                    || !layout.HasCapsulePointClearance(candidate, out _)
                    || !IsSquadMovementCorridorClear(mate.GlobalPosition, candidate, mate))
                {
                    continue;
                }
                frontier = candidate;
                return true;
            }
        }

        frontier = default;
        return false;
    }

    private bool TryResolveDemolitionSquadNavigation(
        SquadMate mate,
        Vector3 destination,
        out SquadNavigationDirective directive)
    {
        directive = SquadNavigationDirective.Walk(mate.GlobalPosition);
        if (_demolitionRoutePlanner is null
            || !(_demolitionArena?.Active ?? false)
            || !DemolitionLayout().IsInsideArena(mate.GlobalPosition, margin: 4.0f)
            || !DemolitionLayout().IsInsideArena(destination, margin: 4.0f))
        {
            return false;
        }

        if (_demolitionSquadRouteFallbacks.TryGetValue(mate, out var fallback))
        {
            if (fallback.Destination.DistanceSquaredTo(destination)
                <= DemolitionSquadRouteReuseDistance * DemolitionSquadRouteReuseDistance)
            {
                // This destination exhausted the authored arena graph.  Returning false
                // hands the same request to the shared trail/grid navigator and avoids
                // rebuilding the identical failed route every 0.75 seconds.
                if (TryResolveDemolitionForwardFrontier(
                        mate,
                        destination,
                        destination,
                        out var cachedFallbackFrontier))
                {
                    directive = SquadNavigationDirective.Walk(cachedFallbackFrontier);
                    return true;
                }
                return false;
            }
            _demolitionSquadRouteFallbacks.Remove(mate);
        }

        const string routeKey = "squad_demolition";
        if (!_demolitionSquadRoutes.TryGetValue(mate, out var cursor)
            || !cursor.MatchesWithin(routeKey, destination, DemolitionSquadRouteReuseDistance))
        {
            cursor = new DemolitionRouteCursor();
            _demolitionSquadRoutes[mate] = cursor;
            PlanDemolitionSquadRoute(cursor, mate, destination, routeKey, countAsReplan: false);
        }
        else
        {
            DemolitionSquadRouteReusesForDiagnostics++;
        }

        if (cursor.TrackMovement(
                mate.GlobalPosition,
                DemolitionSquadRouteTickSeconds,
                movementRequested: !cursor.Complete))
        {
            PlanDemolitionSquadRoute(cursor, mate, destination, routeKey, countAsReplan: true);
        }

        if (cursor.ReplanCount >= DemolitionSquadRouteMaximumAuthoredReplans)
        {
            return YieldDemolitionSquadRouteToGenericNavigation(
                mate,
                destination,
                cursor.ReplanCount,
                out directive);
        }

        cursor.Advance(
            mate.GlobalPosition,
            DemolitionSquadRouteIntermediateTolerance,
            DemolitionSquadRouteFinalTolerance);
        if (cursor.Complete)
        {
            if (cursor.ReachesDestination)
            {
                directive = SquadNavigationDirective.Walk(destination);
                return true;
            }

            if (cursor.ShouldRetryUnreachable(DemolitionSquadRouteTickSeconds))
            {
                PlanDemolitionSquadRoute(cursor, mate, destination, routeKey, countAsReplan: true);
                cursor.Advance(
                    mate.GlobalPosition,
                    DemolitionSquadRouteIntermediateTolerance,
                    DemolitionSquadRouteFinalTolerance);
                if (cursor.ReplanCount >= DemolitionSquadRouteMaximumAuthoredReplans)
                {
                    return YieldDemolitionSquadRouteToGenericNavigation(
                        mate,
                        destination,
                        cursor.ReplanCount,
                        out directive);
                }
            }
            if (cursor.Complete)
            {
                if (TryResolveDemolitionForwardFrontier(
                        mate,
                        destination,
                        destination,
                        out var retryFrontier))
                {
                    directive = SquadNavigationDirective.Walk(retryFrontier);
                    return true;
                }
                return YieldDemolitionSquadRouteToGenericNavigation(
                    mate,
                    destination,
                    cursor.ReplanCount,
                    out directive);
            }
        }

        directive = SquadNavigationDirective.Walk(cursor.CurrentWaypoint);
        return true;
    }

    private void PlanDemolitionSquadRoute(
        DemolitionRouteCursor cursor,
        SquadMate mate,
        Vector3 destination,
        string routeKey,
        bool countAsReplan)
    {
        if (_demolitionRoutePlanner is null)
        {
            return;
        }
        var route = _demolitionRoutePlanner.Plan(
            mate.GlobalPosition,
            destination,
            LocalDemolitionSide);
        cursor.Reset(routeKey, mate.GlobalPosition, destination, route, countAsReplan);
        DemolitionSquadRoutePlansForDiagnostics++;
    }

    private bool YieldDemolitionSquadRouteToGenericNavigation(
        SquadMate mate,
        Vector3 destination,
        int replanCount,
        out SquadNavigationDirective directive)
    {
        _demolitionSquadRoutes.Remove(mate);
        _demolitionSquadRouteFallbacks[mate] = new DemolitionSquadRouteFallback(
            destination,
            replanCount);
        if (TryResolveDemolitionForwardFrontier(
                mate,
                destination,
                destination,
                out var frontier))
        {
            directive = SquadNavigationDirective.Walk(frontier);
            return true;
        }
        directive = default;
        return false;
    }

    private void ClearDemolitionSquadRoute(SquadMate mate)
    {
        _demolitionSquadRoutes.Remove(mate);
        _demolitionEscortProjections.Remove(mate);
    }

    private void ClearDemolitionSquadRouteFallback(SquadMate mate)
    {
        _demolitionSquadRouteFallbacks.Remove(mate);
    }

    private void ClearDemolitionEscortLifecycleState(SquadMate mate)
    {
        _demolitionEscortProjections.Remove(mate);
        _demolitionEscortOpeningFanOut.Remove(mate);
        _demolitionEscortForcedRecoveryRetry.Remove(mate);
    }

    private void ClearDemolitionSquadRoutes()
    {
        _demolitionSquadRoutes.Clear();
        _demolitionSquadRouteFallbacks.Clear();
        _demolitionEscortProjections.Clear();
        _demolitionEscortOpeningFanOut.Clear();
        _demolitionEscortForcedRecoveryRetry.Clear();
    }
}
