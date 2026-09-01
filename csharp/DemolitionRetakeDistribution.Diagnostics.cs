using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal readonly record struct DemolitionRetakeDistributionDiagnosticResult(
    bool Valid,
    bool AssignmentsValid,
    bool UrgentAssignmentsValid,
    bool RoutesValid,
    bool DetoursValid,
    bool GeometryValid,
    bool TideforgeBGeometryValid,
    int ScenarioCount,
    int UrgentScenarioCount,
    int RouteCheckCount,
    int DetourCheckCount,
    int GeometryPairCount,
    float MaximumDetourRatio,
    float MinimumMidRouteSeparation,
    float MaximumSharedPrefixRatio,
    float WorstSharedPrefixMidRouteSeparation,
    float TideforgeBMaximumSharedPrefixRatio,
    float TideforgeBMinimumMidRouteSeparation,
    string WorstDetourProfile,
    string WorstSharedPrefixProfile,
    string UrgentProfile,
    string Profile);

/// <summary>
/// Pure deterministic coverage for defender retake distribution. It validates assignment
/// capacity and route geometry without touching the live scene or diagnostic fixtures.
/// </summary>
internal static class DemolitionRetakeDistributionDiagnostics
{
    private const float SharedPathTolerance = 2.0f;
    private const float SharedPrefixSampleStep = 0.5f;
    private const float MaximumSharedPrefixRatio = 0.75f;
    private const float TideforgeBMaximumSharedPrefixRatio = 0.50f;
    private const float MinimumMidRouteSeparation = 1.5f;

    public static DemolitionRetakeDistributionDiagnosticResult Run(
        IReadOnlyList<DemolitionArenaLayout> layouts,
        DemolitionStrategyPlanner strategyPlanner)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        ArgumentNullException.ThrowIfNull(strategyPlanner);

        var assignmentsValid = true;
        var urgentAssignmentsValid = true;
        var routesValid = true;
        var detoursValid = true;
        var geometryValid = true;
        var tideforgeBGeometryValid = true;
        var scenarioCount = 0;
        var urgentScenarioCount = 0;
        var routeCheckCount = 0;
        var detourCheckCount = 0;
        var geometryPairCount = 0;
        var maximumDetourRatio = 0.0f;
        var minimumMidRouteSeparation = float.PositiveInfinity;
        var maximumSharedPrefixRatio = 0.0f;
        var worstSharedPrefixMidRouteSeparation = 0.0f;
        var tideforgeBMaximumSharedPrefixRatio = 0.0f;
        var tideforgeBMinimumMidRouteSeparation = float.PositiveInfinity;
        var worstDetourProfile = "none";
        var worstSharedPrefixProfile = "none";
        var urgentProfiles = new List<string>();
        var profiles = new List<string>();

        foreach (var layout in layouts)
        {
            var routePlanner = new DemolitionRoutePlanner(layout);
            for (var siteIndex = 0; siteIndex < layout.SitePositions.Count; siteIndex++)
            {
                urgentScenarioCount++;
                var urgentMembers = BuildMembers(
                    layout,
                    memberCount: 5,
                    starts: new Dictionary<string, Vector3>(StringComparer.Ordinal));
                var urgentPlan = strategyPlanner.Plan(
                    DemolitionTeam.Defenders,
                    DemolitionStrategyPhase.PostPlant,
                    urgentMembers,
                    plantedSiteIndex: siteIndex,
                    siteCenters: layout.LocalSiteCoordinates,
                    remainingSeconds: 1.0f);
                var urgentDirectCount = urgentPlan.Assignments.Count(assignment =>
                    assignment.RouteIntent == DemolitionRouteIntent.DirectRetake);
                var urgentWideCount = urgentPlan.Assignments.Count(assignment =>
                    assignment.RouteIntent == DemolitionRouteIntent.WideFlank);
                var urgentScenarioValid = urgentPlan.Assignments.Count == 5
                    && urgentDirectCount == 3
                    && urgentWideCount == 2
                    && urgentPlan.Assignments.All(assignment =>
                        assignment.RouteIntent != DemolitionRouteIntent.RearApproach
                        && assignment.RouteIntent != DemolitionRouteIntent.Balanced
                        && !assignment.TargetKey.StartsWith(
                            "retake_rear_",
                            StringComparison.Ordinal));
                urgentAssignmentsValid &= urgentScenarioValid;
                urgentProfiles.Add(
                    $"{layout.MapId}:S{siteIndex}:D{urgentDirectCount}:W{urgentWideCount}:"
                    + $"V{urgentScenarioValid}");

                foreach (var memberCount in new[] { 4, 5 })
                {
                    scenarioCount++;
                    var starts = new Dictionary<string, Vector3>(StringComparer.Ordinal);
                    var members = BuildMembers(layout, memberCount, starts);
                    var plan = strategyPlanner.Plan(
                        DemolitionTeam.Defenders,
                        DemolitionStrategyPhase.PostPlant,
                        members,
                        plantedSiteIndex: siteIndex,
                        siteCenters: layout.LocalSiteCoordinates,
                        remainingSeconds: 40.0f);
                    var intentGroups = plan.Assignments
                        .GroupBy(assignment => assignment.RouteIntent)
                        .ToArray();
                    var requiredIntentCount = memberCount == 4 ? 2 : 3;
                    var maximumIntentLoad = intentGroups.Length == 0
                        ? int.MaxValue
                        : intentGroups.Max(group => group.Count());
                    var scenarioAssignmentsValid = intentGroups.Length >= requiredIntentCount
                        && maximumIntentLoad <= 2
                        && intentGroups.All(group => group.Key != DemolitionRouteIntent.Balanced);
                    assignmentsValid &= scenarioAssignmentsValid;

                    var scenarioRoutesValid = true;
                    var scenarioDetoursValid = true;
                    var scenarioMaximumDetourRatio = 0.0f;
                    foreach (var assignment in plan.Assignments)
                    {
                        routeCheckCount++;
                        if (!starts.TryGetValue(assignment.MemberId, out var start))
                        {
                            scenarioRoutesValid = false;
                            continue;
                        }
                        var destination = layout.StrategyTarget(assignment.TargetKey);
                        var route = routePlanner.Plan(
                            start,
                            destination,
                            DemolitionTeam.Defenders,
                            assignment.RouteIntent);
                        var routeValid = route.ReachesDestination
                            && route.Waypoints.Count > 0
                            && routePlanner.IsRouteClear(start, route.Waypoints);
                        scenarioRoutesValid &= routeValid;
                        var baseline = routePlanner.Plan(
                            start,
                            destination,
                            DemolitionTeam.Defenders,
                            DemolitionRouteIntent.Balanced);
                        var baselineValid = baseline.ReachesDestination
                            && baseline.Waypoints.Count > 0
                            && routePlanner.IsRouteClear(start, baseline.Waypoints);
                        detourCheckCount++;
                        var detourValid = routeValid
                            && baselineValid
                            && DemolitionRoutePlanner.IsRetakeDetourWithinBudget(
                                route.Length,
                                baseline.Length);
                        scenarioDetoursValid &= detourValid;
                        if (routeValid && baselineValid)
                        {
                            var ratio = route.Length / Mathf.Max(0.001f, baseline.Length);
                            scenarioMaximumDetourRatio = Mathf.Max(
                                scenarioMaximumDetourRatio,
                                ratio);
                            if (ratio > maximumDetourRatio)
                            {
                                maximumDetourRatio = ratio;
                                worstDetourProfile = $"{layout.MapId}:S{siteIndex}:N{memberCount}:"
                                    + $"{assignment.RouteIntent}:{ratio:0.00}:"
                                    + $"{route.Length:0.0}/{baseline.Length:0.0}";
                            }
                        }
                    }
                    routesValid &= scenarioRoutesValid;
                    detoursValid &= scenarioDetoursValid;

                    var tacticalAssignments = plan.Assignments
                        .Where(assignment => assignment.Duty is DemolitionDuty.Retake
                            or DemolitionDuty.Flank)
                        .GroupBy(assignment => assignment.RouteIntent)
                        .Select(group => group.First())
                        .ToArray();
                    var geometryRoutes = BuildGeometryRoutes(
                        layout,
                        routePlanner,
                        tacticalAssignments);
                    var scenarioGeometryValid = geometryRoutes.Count >= requiredIntentCount;
                    var scenarioMinimumMidSeparation = float.PositiveInfinity;
                    var scenarioMaximumSharedPrefix = 0.0f;
                    for (var left = 0; left < geometryRoutes.Count; left++)
                    {
                        for (var right = left + 1; right < geometryRoutes.Count; right++)
                        {
                            geometryPairCount++;
                            var midSeparation = MidRouteSeparation(
                                geometryRoutes[left],
                                geometryRoutes[right]);
                            var sharedPrefix = SharedPrefixRatio(
                                geometryRoutes[left],
                                geometryRoutes[right]);
                            scenarioMinimumMidSeparation = Mathf.Min(
                                scenarioMinimumMidSeparation,
                                midSeparation);
                            scenarioMaximumSharedPrefix = Mathf.Max(
                                scenarioMaximumSharedPrefix,
                                sharedPrefix);
                            var prefixValid = sharedPrefix <= MaximumSharedPrefixRatio;
                            var separationValid = midSeparation >= MinimumMidRouteSeparation;
                            var pairValid = prefixValid && separationValid;
                            if (IsTideforgeBDirectWidePair(
                                layout,
                                siteIndex,
                                geometryRoutes[left],
                                geometryRoutes[right]))
                            {
                                tideforgeBMaximumSharedPrefixRatio = Mathf.Max(
                                    tideforgeBMaximumSharedPrefixRatio,
                                    sharedPrefix);
                                tideforgeBMinimumMidRouteSeparation = Mathf.Min(
                                    tideforgeBMinimumMidRouteSeparation,
                                    midSeparation);
                                var strictPairValid = sharedPrefix
                                        <= TideforgeBMaximumSharedPrefixRatio
                                    && midSeparation >= MinimumMidRouteSeparation;
                                tideforgeBGeometryValid &= strictPairValid;
                                pairValid &= strictPairValid;
                            }
                            scenarioGeometryValid &= pairValid;
                            if (sharedPrefix > maximumSharedPrefixRatio)
                            {
                                maximumSharedPrefixRatio = sharedPrefix;
                                worstSharedPrefixMidRouteSeparation = midSeparation;
                                worstSharedPrefixProfile = $"{layout.MapId}:S{siteIndex}:N{memberCount}:"
                                    + $"{geometryRoutes[left].Intent}-{geometryRoutes[right].Intent}:"
                                    + $"P{sharedPrefix:0.00}:M{midSeparation:0.00}";
                            }
                        }
                    }
                    geometryValid &= scenarioGeometryValid;
                    minimumMidRouteSeparation = Mathf.Min(
                        minimumMidRouteSeparation,
                        scenarioMinimumMidSeparation);
                    profiles.Add(
                        $"{layout.MapId}:S{siteIndex}:N{memberCount}:"
                        + $"I{intentGroups.Length}:L{maximumIntentLoad}:"
                        + $"D{scenarioMaximumDetourRatio:0.00}:"
                        + $"M{scenarioMinimumMidSeparation:0.00}:"
                        + $"P{scenarioMaximumSharedPrefix:0.00}:"
                        + $"A{scenarioAssignmentsValid}:R{scenarioRoutesValid}:"
                        + $"T{scenarioDetoursValid}:G{scenarioGeometryValid}");
                }
            }
        }

        if (float.IsPositiveInfinity(minimumMidRouteSeparation))
        {
            minimumMidRouteSeparation = 0.0f;
        }
        if (float.IsPositiveInfinity(tideforgeBMinimumMidRouteSeparation))
        {
            tideforgeBMinimumMidRouteSeparation = 0.0f;
            tideforgeBGeometryValid = false;
        }
        var valid = assignmentsValid
            && urgentAssignmentsValid
            && routesValid
            && detoursValid
            && geometryValid
            && tideforgeBGeometryValid
            && scenarioCount > 0;
        return new DemolitionRetakeDistributionDiagnosticResult(
            valid,
            assignmentsValid,
            urgentAssignmentsValid,
            routesValid,
            detoursValid,
            geometryValid,
            tideforgeBGeometryValid,
            scenarioCount,
            urgentScenarioCount,
            routeCheckCount,
            detourCheckCount,
            geometryPairCount,
            maximumDetourRatio,
            minimumMidRouteSeparation,
            maximumSharedPrefixRatio,
            worstSharedPrefixMidRouteSeparation,
            tideforgeBMaximumSharedPrefixRatio,
            tideforgeBMinimumMidRouteSeparation,
            worstDetourProfile,
            worstSharedPrefixProfile,
            string.Join('/', urgentProfiles),
            string.Join('/', profiles));
    }

    private static IReadOnlyList<DemolitionAgentSnapshot> BuildMembers(
        DemolitionArenaLayout layout,
        int memberCount,
        Dictionary<string, Vector3> starts)
    {
        var members = new List<DemolitionAgentSnapshot>(memberCount);
        for (var index = 0; index < memberCount; index++)
        {
            var memberId = $"RETAKE_{index}";
            var start = layout.DefenderSpawns[index];
            starts[memberId] = start;
            members.Add(new DemolitionAgentSnapshot(
                memberId,
                DemolitionTeam.Defenders,
                index % 3 == 0
                    ? OperatorRole.Assault
                    : index % 3 == 1 ? OperatorRole.Recon : OperatorRole.Medic,
                1.0f - index * 0.04f,
                88.0f + index * 17.0f,
                true,
                false,
                start.X - layout.Origin.X,
                start.Z - layout.Origin.Z));
        }
        return members;
    }

    private static List<RoutePath> BuildGeometryRoutes(
        DemolitionArenaLayout layout,
        DemolitionRoutePlanner routePlanner,
        IReadOnlyList<DemolitionAssignment> assignments)
    {
        var routes = new List<RoutePath>(assignments.Count);
        var commonStart = layout.DefenderSpawns[0];
        foreach (var assignment in assignments)
        {
            var route = routePlanner.Plan(
                commonStart,
                layout.StrategyTarget(assignment.TargetKey),
                DemolitionTeam.Defenders,
                assignment.RouteIntent);
            if (!route.ReachesDestination
                || route.Waypoints.Count == 0
                || !routePlanner.IsRouteClear(commonStart, route.Waypoints))
            {
                continue;
            }
            var points = new Vector3[route.Waypoints.Count + 1];
            points[0] = commonStart;
            for (var index = 0; index < route.Waypoints.Count; index++)
            {
                points[index + 1] = route.Waypoints[index];
            }
            routes.Add(new RoutePath(
                assignment.RouteIntent,
                points,
                PathLength(points)));
        }
        return routes;
    }

    private static float MidRouteSeparation(RoutePath left, RoutePath right)
    {
        // Compare equal travel progress: later convergence is valid, while the prefix
        // gate still rejects two roles that merely occupy different points of one lane.
        var leftMidpoint = PointAtDistance(left, left.Length * 0.5f);
        var rightMidpoint = PointAtDistance(right, right.Length * 0.5f);
        return HorizontalDistance(leftMidpoint, rightMidpoint);
    }

    private static float SharedPrefixRatio(RoutePath left, RoutePath right)
    {
        var sharedLength = Mathf.Min(left.Length, right.Length);
        if (sharedLength <= 0.1f)
        {
            return 1.0f;
        }
        for (var distance = 0.0f;
             distance <= sharedLength;
             distance += SharedPrefixSampleStep)
        {
            if (HorizontalDistance(
                    PointAtDistance(left, distance),
                    PointAtDistance(right, distance)) > SharedPathTolerance)
            {
                return distance / sharedLength;
            }
        }
        return 1.0f;
    }

    private static Vector3 PointAtDistance(RoutePath path, float distance)
    {
        for (var index = 1; index < path.Points.Count; index++)
        {
            var segmentLength = path.Points[index - 1].DistanceTo(path.Points[index]);
            if (distance <= segmentLength)
            {
                return path.Points[index - 1].Lerp(
                    path.Points[index],
                    distance / Mathf.Max(0.001f, segmentLength));
            }
            distance -= segmentLength;
        }
        return path.Points[^1];
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
        => new Vector2(left.X - right.X, left.Z - right.Z).Length();

    private static float PathLength(IReadOnlyList<Vector3> points)
    {
        var length = 0.0f;
        for (var index = 1; index < points.Count; index++)
        {
            length += points[index - 1].DistanceTo(points[index]);
        }
        return length;
    }

    private readonly record struct RoutePath(
        DemolitionRouteIntent Intent,
        IReadOnlyList<Vector3> Points,
        float Length);

    private static bool IsTideforgeBDirectWidePair(
        DemolitionArenaLayout layout,
        int siteIndex,
        RoutePath left,
        RoutePath right)
        => layout.MapId == DemolitionMapCatalog.TideforgeId
            && siteIndex == 1
            && (left.Intent == DemolitionRouteIntent.DirectRetake
                    && right.Intent == DemolitionRouteIntent.WideFlank
                || left.Intent == DemolitionRouteIntent.WideFlank
                    && right.Intent == DemolitionRouteIntent.DirectRetake);
}
