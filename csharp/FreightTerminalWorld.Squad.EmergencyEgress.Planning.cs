using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool TryBuildSquadEmergencyDoorPlan(
        SquadMate mate,
        Vector3 destination,
        ulong now,
        ref int corridorProbeBudget,
        out SquadEmergencyEgressPlan plan)
    {
        plan = null!;
        var actor = mate.GlobalPosition;
        var maximumDistanceSquared = SquadEmergencyDoorSearchRange * SquadEmergencyDoorSearchRange;
        var bestScore = float.PositiveInfinity;
        for (var index = 0; index < _refineryDoors.Count; index++)
        {
            var door = _refineryDoors[index];
            if (!IsInstanceValid(door))
            {
                continue;
            }
            var failureKey = unchecked((long)door.GetInstanceId());
            var center = door.InteractionPoint;
            var centerOffset = center - actor;
            centerOffset.Y = 0.0f;
            if (centerOffset.LengthSquared() > maximumDistanceSquared
                || Mathf.Abs(center.Y - actor.Y) > 2.2f
                || IsSquadEmergencyEgressFailureActive(mate.GetInstanceId(), failureKey, now))
            {
                continue;
            }

            var outside = door.OutsideProbe;
            var inside = door.InsideProbe;
            outside.Y = actor.Y;
            inside.Y = actor.Y;
            var outsideDistance = SquadEmergencyHorizontalDistanceSquared(actor, outside);
            var insideDistance = SquadEmergencyHorizontalDistanceSquared(actor, inside);
            var near = outsideDistance <= insideDistance ? outside : inside;
            var far = outsideDistance <= insideDistance ? inside : outside;
            var across = far - near;
            across.Y = 0.0f;
            if (across.LengthSquared() < 0.25f)
            {
                continue;
            }
            across = across.Normalized();
            var exit = far + across * 0.9f;
            if (corridorProbeBudget < 2)
            {
                break;
            }
            corridorProbeBudget -= 2;
            if (!IsSquadMovementCorridorClear(actor, near, mate)
                || !IsSquadMovementCorridorClear(far, exit, mate))
            {
                continue;
            }

            var score = actor.DistanceTo(near)
                + SquadEmergencyGoalScore(exit, destination) * 0.32f;
            if (score >= bestScore)
            {
                continue;
            }
            bestScore = score;
            plan = new SquadEmergencyEgressPlan
            {
                Source = SquadEmergencyEgressSource.Door,
                Directives = new[]
                {
                    RequiredEmergencyWalk(near),
                    RequiredEmergencyWalk(exit)
                },
                Destination = destination,
                ExpiresMilliseconds = now + SquadEmergencyEgressPlanLifetimeMilliseconds,
                FailureKey = failureKey
            };
        }
        return plan is not null;
    }

    private bool TryBuildSquadEmergencyConnectorPlan(
        SquadMate mate,
        Vector3 destination,
        ulong now,
        ref int corridorProbeBudget,
        out SquadEmergencyEgressPlan plan)
    {
        plan = null!;
        var actor = mate.GlobalPosition;
        var actorGoalScore = SquadEmergencyGoalScore(actor, destination);
        var maximumDistanceSquared = SquadEmergencyConnectorSearchRange
            * SquadEmergencyConnectorSearchRange;
        var bestScore = float.PositiveInfinity;
        foreach (var link in _squadTraversalLinks)
        {
            EvaluateSquadEmergencyConnectorDirection(
                mate,
                destination,
                now,
                link,
                link.ForwardDirectives,
                link.ForwardPoints[0],
                link.ForwardPoints[^1],
                link.Id * 2,
                actorGoalScore,
                maximumDistanceSquared,
                ref corridorProbeBudget,
                ref bestScore,
                ref plan);
            if (!link.Bidirectional)
            {
                continue;
            }
            EvaluateSquadEmergencyConnectorDirection(
                mate,
                destination,
                now,
                link,
                link.ReverseDirectives,
                link.ForwardPoints[^1],
                link.ForwardPoints[0],
                link.Id * 2 + 1,
                actorGoalScore,
                maximumDistanceSquared,
                ref corridorProbeBudget,
                ref bestScore,
                ref plan);
        }
        return plan is not null;
    }

    private void EvaluateSquadEmergencyConnectorDirection(
        SquadMate mate,
        Vector3 destination,
        ulong now,
        SquadTraversalLink link,
        SquadNavigationDirective[] directives,
        Vector3 near,
        Vector3 far,
        int directedEdgeId,
        float actorGoalScore,
        float maximumDistanceSquared,
        ref int corridorProbeBudget,
        ref float bestScore,
        ref SquadEmergencyEgressPlan plan)
    {
        var actor = mate.GlobalPosition;
        var failureKey = long.MinValue / 2 + directedEdgeId;
        if (directives.Length == 0
            || actor.DistanceSquaredTo(near) > maximumDistanceSquared
            || Mathf.Abs(actor.Y - near.Y) > 1.8f
            || SquadEmergencyGoalScore(far, destination) > actorGoalScore + 2.0f
            || IsSquadEmergencyEgressFailureActive(mate.GetInstanceId(), failureKey, now)
            || corridorProbeBudget <= 0)
        {
            return;
        }
        corridorProbeBudget--;
        if (!IsSquadMovementCorridorClear(actor, near, mate))
        {
            return;
        }

        var score = actor.DistanceTo(near)
            + SquadEmergencyGoalScore(far, destination) * 0.38f
            + link.Cost * 0.08f;
        if (score >= bestScore)
        {
            return;
        }
        bestScore = score;
        plan = new SquadEmergencyEgressPlan
        {
            Source = SquadEmergencyEgressSource.AuthoredConnector,
            Directives = directives,
            Destination = destination,
            ExpiresMilliseconds = now + SquadEmergencyEgressPlanLifetimeMilliseconds,
            FailureKey = failureKey
        };
    }

    private bool TryBuildSquadEmergencyBreadcrumbPlan(
        SquadMate mate,
        Vector3 destination,
        ulong now,
        ref int corridorProbeBudget,
        out SquadEmergencyEgressPlan plan)
    {
        plan = null!;
        if (_squadLeaderTrail.Count == 0)
        {
            return false;
        }
        var actor = mate.GlobalPosition;
        var baseDirection = SquadEmergencyEgressBaseDirection(mate, destination);
        for (var yawIndex = 0; yawIndex < SquadEmergencyEgressYawRadians.Length; yawIndex++)
        {
            var failureKey = long.MinValue / 4 + yawIndex;
            if (IsSquadEmergencyEgressFailureActive(mate.GetInstanceId(), failureKey, now))
            {
                continue;
            }
            var direction = baseDirection.Rotated(
                Vector3.Up,
                SquadEmergencyEgressYawRadians[yawIndex]);
            var corner = actor + direction * SquadEmergencyBreadcrumbStepDistance;
            if (corridorProbeBudget <= 0)
            {
                break;
            }
            corridorProbeBudget--;
            if (!IsSquadMovementCorridorClear(actor, corner, mate)
                || !TryFindSquadEmergencyBreadcrumbHandoff(
                    mate,
                    corner,
                    destination,
                    ref corridorProbeBudget,
                    out var handoff))
            {
                continue;
            }
            plan = new SquadEmergencyEgressPlan
            {
                Source = SquadEmergencyEgressSource.BreadcrumbHandoff,
                Directives = new[]
                {
                    RequiredEmergencyWalk(corner),
                    RequiredEmergencyWalk(handoff)
                },
                Destination = destination,
                ExpiresMilliseconds = now + SquadEmergencyEgressPlanLifetimeMilliseconds,
                FailureKey = failureKey
            };
            return true;
        }
        return false;
    }

    private bool TryFindSquadEmergencyBreadcrumbHandoff(
        SquadMate mate,
        Vector3 corner,
        Vector3 destination,
        ref int corridorProbeBudget,
        out Vector3 handoff)
    {
        handoff = default;
        var maximumDistanceSquared = SquadEmergencyBreadcrumbHandoffRange
            * SquadEmergencyBreadcrumbHandoffRange;
        var bestScore = float.PositiveInfinity;
        for (var index = _squadLeaderTrail.Count - 1; index >= 0; index--)
        {
            var point = _squadLeaderTrail[index];
            if (Mathf.Abs(point.Y - corner.Y) > 1.4f
                || corner.DistanceSquaredTo(point) > maximumDistanceSquared)
            {
                continue;
            }
            var score = corner.DistanceTo(point)
                + SquadEmergencyGoalScore(point, destination) * 0.28f;
            if (score >= bestScore)
            {
                continue;
            }
            if (corridorProbeBudget <= 0)
            {
                break;
            }
            corridorProbeBudget--;
            if (!IsSquadMovementCorridorClear(corner, point, mate))
            {
                continue;
            }
            bestScore = score;
            handoff = point;
        }
        return bestScore < float.PositiveInfinity;
    }

    private bool TryBuildSquadEmergencyOpenCorridorPlan(
        SquadMate mate,
        Vector3 destination,
        ulong now,
        ref int corridorProbeBudget,
        out SquadEmergencyEgressPlan plan)
    {
        plan = null!;
        var actor = mate.GlobalPosition;
        var baseDirection = SquadEmergencyEgressBaseDirection(mate, destination);
        ReadOnlySpan<float> distances = stackalloc float[] { 5.2f, 3.1f };
        for (var distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
        {
            for (var yawIndex = 0; yawIndex < SquadEmergencyEgressYawRadians.Length; yawIndex++)
            {
                var failureKey = long.MinValue / 8 + distanceIndex * 32L + yawIndex;
                if (IsSquadEmergencyEgressFailureActive(mate.GetInstanceId(), failureKey, now))
                {
                    continue;
                }
                var direction = baseDirection.Rotated(
                    Vector3.Up,
                    SquadEmergencyEgressYawRadians[yawIndex]);
                var waypoint = actor + direction * distances[distanceIndex];
                if (corridorProbeBudget <= 0)
                {
                    return false;
                }
                corridorProbeBudget--;
                if (!IsSquadMovementCorridorClear(actor, waypoint, mate))
                {
                    continue;
                }
                plan = new SquadEmergencyEgressPlan
                {
                    Source = SquadEmergencyEgressSource.OpenCorridor,
                    Directives = new[] { RequiredEmergencyWalk(waypoint) },
                    Destination = destination,
                    ExpiresMilliseconds = now + SquadEmergencyEgressPlanLifetimeMilliseconds,
                    FailureKey = failureKey
                };
                return true;
            }
        }
        return false;
    }

    private static float SquadEmergencyHorizontalDistanceSquared(Vector3 from, Vector3 to)
    {
        var x = to.X - from.X;
        var z = to.Z - from.Z;
        return x * x + z * z;
    }
}
