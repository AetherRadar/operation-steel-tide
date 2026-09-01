using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionRoutePlanner
{
    internal const float MaximumAuthoredRetakeDetourRatio = 3.0f;
    internal const float AuthoredRetakeDetourAllowance = 8.0f;

    private bool TryPlanAuthoredRetakeCorridor(
        Vector3 start,
        Vector3 destination,
        DemolitionRouteIntent routeIntent,
        out DemolitionRouteResult route)
    {
        var corridor = _layout.RetakeCorridorWaypoints(
            ClosestSiteIndex(destination),
            routeIntent);
        if (corridor.Count == 0)
        {
            route = default;
            return false;
        }

        var waypoints = new List<Vector3>();
        var segmentStart = start;
        for (var index = 0; index <= corridor.Count; index++)
        {
            var segmentDestination = index < corridor.Count
                ? corridor[index]
                : destination;
            var segment = Plan(
                segmentStart,
                segmentDestination,
                DemolitionTeam.Defenders,
                DemolitionRouteIntent.Balanced);
            if (!segment.ReachesDestination || segment.Waypoints.Count == 0)
            {
                route = default;
                return false;
            }
            AppendDistinct(waypoints, segment.Waypoints);
            segmentStart = segmentDestination;
        }

        if (!IsRouteClear(start, waypoints))
        {
            route = default;
            return false;
        }
        var routeLength = RouteLength(start, waypoints);
        var directRoute = Plan(
            start,
            destination,
            DemolitionTeam.Defenders,
            DemolitionRouteIntent.Balanced);
        // A late replan must not drag an operator back through a now-distant anchor.
        if (directRoute.ReachesDestination
            && !IsRetakeDetourWithinBudget(routeLength, directRoute.Length))
        {
            route = default;
            return false;
        }
        route = new DemolitionRouteResult(
            waypoints,
            true,
            routeLength);
        return true;
    }

    internal static bool IsRetakeDetourWithinBudget(
        float routeLength,
        float baselineLength)
        => routeLength <= baselineLength * MaximumAuthoredRetakeDetourRatio
            + AuthoredRetakeDetourAllowance;

    private static void AppendDistinct(
        List<Vector3> destination,
        IReadOnlyList<Vector3> source)
    {
        foreach (var waypoint in source)
        {
            if (destination.Count == 0
                || destination[^1].DistanceSquaredTo(waypoint)
                    > DuplicatePointDistanceSquared)
            {
                destination.Add(waypoint);
            }
        }
    }
}
