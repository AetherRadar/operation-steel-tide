using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public readonly record struct DemolitionRouteResult(
    IReadOnlyList<Vector3> Waypoints,
    bool ReachesDestination,
    float Length);

/// <summary>
/// Pure arena route planner. It connects authored lanes, elevation transitions, and cover
/// posts into a small 3D visibility graph, then finds a deterministic shortest route for
/// objective movement. Automatic links stay on one walkable grade; only authored paths can
/// bridge a full floor change.
/// </summary>
public sealed class DemolitionRoutePlanner
{
    private const float MaximumVisibilityEdge = 24.0f;
    private const float MaximumEndpointEdge = 34.0f;
    private const float MaximumAutomaticVerticalDelta = 0.65f;
    private const float MaximumAuthoredGrade = 0.325f;
    private const float DuplicatePointDistanceSquared = 0.08f * 0.08f;
    private const float OpponentSpawnAvoidanceRadius = 20.0f;
    private const float OpponentSpawnMaximumPenalty = 90.0f;
    private const float OpeningLanePolicyRadius = 18.0f;
    private const float OpeningLaneReversePenalty = 70.0f;
    private const float ObjectiveDepthTolerance = 4.0f;

    private readonly DemolitionArenaLayout _layout;
    private readonly Vector3[] _nodes;
    private readonly List<RouteEdge>[] _baseEdges;
    private readonly DemolitionElevationTransitions _elevationTransitions;

    public DemolitionRoutePlanner(DemolitionArenaLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        var nodes = new List<Vector3>();
        var authoredLinks = new List<(int From, int To)>();
        var auxiliaryNodePaths = new List<IReadOnlyList<int>>();
        AddAuthoredPath(nodes, authoredLinks, layout.AttackToAPath);
        AddAuthoredPath(nodes, authoredLinks, layout.AttackToBPath);
        AddAuthoredPath(nodes, authoredLinks, layout.AttackApproachToAPath);
        AddAuthoredPath(nodes, authoredLinks, layout.AttackApproachToBPath);
        AddAuthoredPath(nodes, authoredLinks, layout.AttackMidPath);
        AddAuthoredPath(nodes, authoredLinks, layout.DefenderToAPath);
        AddAuthoredPath(nodes, authoredLinks, layout.DefenderToBPath);
        AddAuthoredPath(nodes, authoredLinks, layout.SiteRotationPath);
        foreach (var path in layout.AuxiliaryPaths)
        {
            auxiliaryNodePaths.Add(AddAuthoredPath(nodes, authoredLinks, path));
        }
        AddPoints(nodes, layout.AttackSpawns);
        AddPoints(nodes, layout.DefenderSpawns);
        AddPoints(nodes, layout.SitePositions);
        AddPoints(nodes, layout.CoverPoints);
        AddPoint(nodes, layout.Midpoint);

        _nodes = nodes.ToArray();
        _elevationTransitions = DemolitionElevationTransitions.Create(
            _nodes,
            auxiliaryNodePaths,
            layout.TraversalBoxes);
        _baseEdges = CreateEdgeLists(_nodes.Length);
        foreach (var link in authoredLinks)
        {
            TryConnect(
                _baseEdges,
                link.From,
                link.To,
                _nodes[link.From],
                _nodes[link.To],
                allowElevationChange: true);
        }
        for (var from = 0; from < _nodes.Length; from++)
        {
            for (var to = from + 1; to < _nodes.Length; to++)
            {
                if (!_elevationTransitions.IsInteriorNode(from)
                    && !_elevationTransitions.IsInteriorNode(to)
                    && HorizontalDistance(_nodes[from], _nodes[to]) <= MaximumVisibilityEdge)
                {
                    TryConnect(
                        _baseEdges,
                        from,
                        to,
                        _nodes[from],
                        _nodes[to],
                        allowElevationChange: false);
                }
            }
        }
    }

    public DemolitionRouteResult Plan(
        Vector3 start,
        Vector3 destination,
        DemolitionTeam? movingTeam = null)
    {
        if (CanUseRouteSegment(start, destination)
            && TacticalSegmentPenalty(start, destination, destination, movingTeam) <= 0.001f)
        {
            return new DemolitionRouteResult(
                new[] { destination },
                true,
                start.DistanceTo(destination));
        }

        if (movingTeam == DemolitionTeam.Attackers
            && TryMatchSiteDestination(destination, out var attackSiteIndex)
            && TryPlanAuthoredAttackLane(
                start,
                destination,
                attackSiteIndex,
                out var attackRoute))
        {
            return attackRoute;
        }

        var startIndex = _nodes.Length;
        var destinationIndex = startIndex + 1;
        var edges = CloneEdgesWithEndpoints();
        ConnectEndpoint(edges, startIndex, start, MaximumEndpointEdge);
        ConnectEndpoint(edges, destinationIndex, destination, MaximumEndpointEdge);
        if (CanUseRouteSegment(start, destination))
        {
            AddBidirectionalEdge(
                edges,
                startIndex,
                destinationIndex,
                start.DistanceTo(destination));
        }
        if (edges[startIndex].Count == 0)
        {
            ConnectEndpoint(edges, startIndex, start, float.PositiveInfinity);
        }
        if (edges[destinationIndex].Count == 0)
        {
            ConnectEndpoint(edges, destinationIndex, destination, float.PositiveInfinity);
        }

        var search = FindShortestPath(
            edges,
            startIndex,
            destinationIndex,
            start,
            destination,
            movingTeam);
        if (search.Previous[destinationIndex] < 0)
        {
            var frontierIndex = FindClosestReachableFrontier(search.Distances, destination);
            if (frontierIndex < 0)
            {
                return new DemolitionRouteResult(
                    Array.Empty<Vector3>(),
                    false,
                    0.0f);
            }

            var frontier = _nodes[frontierIndex];
            var frontierRoute = ReconstructRoute(
                search.Previous,
                startIndex,
                frontierIndex,
                start,
                frontier);
            var safeRoute = SimplifyRoute(
                start,
                frontierRoute,
                destination,
                movingTeam);
            return new DemolitionRouteResult(
                safeRoute,
                false,
                RouteLength(start, safeRoute));
        }

        var route = ReconstructRoute(search.Previous, startIndex, destinationIndex, start, destination);
        var simplified = SimplifyRoute(start, route, destination, movingTeam);
        return new DemolitionRouteResult(simplified, true, RouteLength(start, simplified));
    }

    public bool IsRouteClear(Vector3 start, IReadOnlyList<Vector3> waypoints)
    {
        var from = start;
        for (var index = 0; index < waypoints.Count; index++)
        {
            var to = waypoints[index];
            if (!CanUseRouteSegment(from, to))
            {
                return false;
            }
            from = to;
        }
        return true;
    }

    private bool TryPlanAuthoredAttackLane(
        Vector3 start,
        Vector3 destination,
        int siteIndex,
        out DemolitionRouteResult route)
    {
        var authoredPath = siteIndex == 0
            ? _layout.AttackApproachToAPath
            : _layout.AttackApproachToBPath;
        var bestEntry = -1;
        var bestCost = float.PositiveInfinity;
        for (var entry = 0; entry < authoredPath.Count; entry++)
        {
            var waypoint = authoredPath[entry];
            if (!CanUseRouteSegment(start, waypoint))
            {
                continue;
            }
            var tacticalCost = TacticalSegmentPenalty(
                start,
                waypoint,
                destination,
                DemolitionTeam.Attackers);
            var previous = waypoint;
            for (var index = entry + 1;
                index < authoredPath.Count && !float.IsPositiveInfinity(tacticalCost);
                index++)
            {
                tacticalCost += TacticalSegmentPenalty(
                    previous,
                    authoredPath[index],
                    destination,
                    DemolitionTeam.Attackers);
                previous = authoredPath[index];
            }
            if (float.IsPositiveInfinity(tacticalCost))
            {
                continue;
            }
            var cost = start.DistanceTo(waypoint)
                + AuthoredPathRemainingLength(authoredPath, entry)
                + tacticalCost;
            if (cost < bestCost)
            {
                bestEntry = entry;
                bestCost = cost;
            }
        }
        if (bestEntry < 0)
        {
            route = default;
            return false;
        }

        var lane = new List<Vector3>(authoredPath.Count - bestEntry + 1);
        for (var index = bestEntry; index < authoredPath.Count; index++)
        {
            lane.Add(authoredPath[index]);
        }
        if (lane.Count == 0
            || lane[^1].DistanceSquaredTo(destination)
                > DuplicatePointDistanceSquared)
        {
            lane.Add(destination);
        }
        else
        {
            lane[^1] = destination;
        }
        if (!IsRouteClear(start, lane))
        {
            route = default;
            return false;
        }

        var simplified = SimplifyRoute(
            start,
            lane,
            destination,
            DemolitionTeam.Attackers);
        route = new DemolitionRouteResult(
            simplified,
            true,
            RouteLength(start, simplified));
        return true;
    }

    private bool TryMatchSiteDestination(Vector3 destination, out int siteIndex)
    {
        siteIndex = ClosestSiteIndex(destination);
        return HorizontalDistanceSquared(destination, _layout.SitePositions[siteIndex])
            <= 0.75f * 0.75f;
    }

    private static float AuthoredPathRemainingLength(
        IReadOnlyList<Vector3> path,
        int startIndex)
    {
        var length = 0.0f;
        for (var index = startIndex + 1; index < path.Count; index++)
        {
            length += path[index - 1].DistanceTo(path[index]);
        }
        return length;
    }

    private List<RouteEdge>[] CloneEdgesWithEndpoints()
    {
        var edges = CreateEdgeLists(_nodes.Length + 2);
        for (var index = 0; index < _baseEdges.Length; index++)
        {
            edges[index].AddRange(_baseEdges[index]);
        }
        return edges;
    }

    private void ConnectEndpoint(
        List<RouteEdge>[] edges,
        int endpointIndex,
        Vector3 endpoint,
        float maximumDistance)
    {
        if (_elevationTransitions.TryFindClosestSegment(endpoint, out var transition))
        {
            ConnectTransitionEndpoint(
                edges,
                endpointIndex,
                endpoint,
                transition.FromNode,
                maximumDistance);
            ConnectTransitionEndpoint(
                edges,
                endpointIndex,
                endpoint,
                transition.ToNode,
                maximumDistance);
            return;
        }

        for (var nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
        {
            var distance = endpoint.DistanceTo(_nodes[nodeIndex]);
            if (distance > maximumDistance
                || _elevationTransitions.IsInteriorNode(nodeIndex)
                || !CanTraverse(endpoint, _nodes[nodeIndex], allowElevationChange: false))
            {
                continue;
            }
            AddBidirectionalEdge(edges, endpointIndex, nodeIndex, distance);
        }
    }

    private void ConnectTransitionEndpoint(
        List<RouteEdge>[] edges,
        int endpointIndex,
        Vector3 endpoint,
        int nodeIndex,
        float maximumDistance)
    {
        var distance = endpoint.DistanceTo(_nodes[nodeIndex]);
        if (distance <= maximumDistance
            && CanTraverse(endpoint, _nodes[nodeIndex], allowElevationChange: true))
        {
            AddBidirectionalEdge(edges, endpointIndex, nodeIndex, distance);
        }
    }

    private RouteSearchResult FindShortestPath(
        List<RouteEdge>[] edges,
        int startIndex,
        int destinationIndex,
        Vector3 start,
        Vector3 destination,
        DemolitionTeam? movingTeam)
    {
        var distances = new float[edges.Length];
        var previous = new int[edges.Length];
        var visited = new bool[edges.Length];
        Array.Fill(distances, float.PositiveInfinity);
        Array.Fill(previous, -1);
        distances[startIndex] = 0.0f;

        for (var iteration = 0; iteration < edges.Length; iteration++)
        {
            var current = -1;
            var currentDistance = float.PositiveInfinity;
            for (var candidate = 0; candidate < edges.Length; candidate++)
            {
                if (!visited[candidate] && distances[candidate] < currentDistance)
                {
                    current = candidate;
                    currentDistance = distances[candidate];
                }
            }
            if (current < 0 || current == destinationIndex)
            {
                break;
            }

            visited[current] = true;
            foreach (var edge in edges[current])
            {
                var from = RoutePoint(
                    current,
                    startIndex,
                    destinationIndex,
                    start,
                    destination);
                var to = RoutePoint(
                    edge.To,
                    startIndex,
                    destinationIndex,
                    start,
                    destination);
                var candidateDistance = currentDistance + edge.Cost
                    + TacticalSegmentPenalty(from, to, destination, movingTeam);
                if (candidateDistance + 0.001f >= distances[edge.To])
                {
                    continue;
                }
                distances[edge.To] = candidateDistance;
                previous[edge.To] = current;
            }
        }
        return new RouteSearchResult(previous, distances);
    }

    private float TacticalSegmentPenalty(
        Vector3 from,
        Vector3 to,
        Vector3 destination,
        DemolitionTeam? movingTeam)
    {
        if (movingTeam is null)
        {
            return 0.0f;
        }
        var originSpawn = movingTeam == DemolitionTeam.Attackers
            ? _layout.AttackSpawn
            : _layout.DefenderSpawn;
        var opposingSpawn = movingTeam == DemolitionTeam.Attackers
            ? _layout.DefenderSpawn
            : _layout.AttackSpawn;
        var penalty = 0.0f;
        var clearance = HorizontalDistanceToSegment(opposingSpawn, from, to);
        if (clearance < OpponentSpawnAvoidanceRadius)
        {
            var depth = 1.0f - clearance / OpponentSpawnAvoidanceRadius;
            penalty += OpponentSpawnMaximumPenalty * depth * depth;
        }

        var attackSiteIndex = -1;
        var attackerSiteObjective = movingTeam == DemolitionTeam.Attackers
            && TryMatchSiteDestination(destination, out attackSiteIndex);
        var attackAxis = new Vector2(
            opposingSpawn.X - originSpawn.X,
            opposingSpawn.Z - originSpawn.Z);
        if (attackerSiteObjective
            && attackAxis.LengthSquared() > 0.001f)
        {
            attackAxis = attackAxis.Normalized();
            var destinationDepth = attackAxis.Dot(new Vector2(
                destination.X - originSpawn.X,
                destination.Z - originSpawn.Z));
            var fromDepth = attackAxis.Dot(new Vector2(
                from.X - originSpawn.X,
                from.Z - originSpawn.Z));
            var toDepth = attackAxis.Dot(new Vector2(
                to.X - originSpawn.X,
                to.Z - originSpawn.Z));
            var maximumDepth = destinationDepth + ObjectiveDepthTolerance;
            if (toDepth > maximumDepth
                && toDepth > fromDepth + 0.001f)
            {
                return float.PositiveInfinity;
            }
        }

        if (attackerSiteObjective
            && HorizontalDistance(from, _layout.AttackSpawn) <= OpeningLanePolicyRadius)
        {
            var openingPath = attackSiteIndex == 0
                ? _layout.AttackApproachToAPath
                : _layout.AttackApproachToBPath;
            var expectedLateral = openingPath.Count >= 2
                ? openingPath[1].X - openingPath[0].X
                : _layout.SitePositions[attackSiteIndex].X - _layout.AttackSpawn.X;
            var actualLateral = to.X - from.X;
            if (Mathf.Abs(expectedLateral) >= 1.0f
                && expectedLateral * actualLateral < -0.25f)
            {
                penalty += OpeningLaneReversePenalty
                    + Mathf.Abs(actualLateral) * 5.0f;
            }
        }
        return penalty;
    }

    private int ClosestSiteIndex(Vector3 destination)
    {
        var closest = 0;
        var closestDistance = float.PositiveInfinity;
        for (var index = 0; index < _layout.SitePositions.Count; index++)
        {
            var distance = HorizontalDistanceSquared(destination, _layout.SitePositions[index]);
            if (distance < closestDistance)
            {
                closest = index;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private Vector3 RoutePoint(
        int index,
        int startIndex,
        int destinationIndex,
        Vector3 start,
        Vector3 destination)
    {
        if (index == startIndex)
        {
            return start;
        }
        if (index == destinationIndex)
        {
            return destination;
        }
        return _nodes[index];
    }

    private static float HorizontalDistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        var segmentX = end.X - start.X;
        var segmentZ = end.Z - start.Z;
        var segmentLengthSquared = segmentX * segmentX + segmentZ * segmentZ;
        if (segmentLengthSquared <= 0.0001f)
        {
            return HorizontalDistance(point, start);
        }
        var pointX = point.X - start.X;
        var pointZ = point.Z - start.Z;
        var amount = Mathf.Clamp(
            (pointX * segmentX + pointZ * segmentZ) / segmentLengthSquared,
            0.0f,
            1.0f);
        var closest = new Vector3(
            start.X + segmentX * amount,
            point.Y,
            start.Z + segmentZ * amount);
        return HorizontalDistance(point, closest);
    }

    private int FindClosestReachableFrontier(IReadOnlyList<float> distances, Vector3 destination)
    {
        var best = -1;
        var bestDestinationDistance = float.PositiveInfinity;
        var bestRouteLength = float.PositiveInfinity;
        for (var index = 0; index < _nodes.Length; index++)
        {
            var routeLength = distances[index];
            if (float.IsPositiveInfinity(routeLength))
            {
                continue;
            }

            var destinationDistance = _nodes[index].DistanceSquaredTo(destination);
            if (destinationDistance + 0.001f < bestDestinationDistance
                || Mathf.IsEqualApprox(destinationDistance, bestDestinationDistance)
                    && routeLength < bestRouteLength)
            {
                best = index;
                bestDestinationDistance = destinationDistance;
                bestRouteLength = routeLength;
            }
        }
        return best;
    }

    private Vector3[] ReconstructRoute(
        int[] previous,
        int startIndex,
        int destinationIndex,
        Vector3 start,
        Vector3 destination)
    {
        var reversed = new List<Vector3> { destination };
        var current = previous[destinationIndex];
        while (current >= 0 && current != startIndex)
        {
            var point = _nodes[current];
            if (point.DistanceSquaredTo(reversed[^1]) > DuplicatePointDistanceSquared)
            {
                reversed.Add(point);
            }
            current = previous[current];
        }
        reversed.Reverse();
        if (reversed.Count > 0
            && reversed[0].DistanceSquaredTo(start) <= DuplicatePointDistanceSquared)
        {
            reversed.RemoveAt(0);
        }
        return reversed.ToArray();
    }

    private Vector3[] SimplifyRoute(
        Vector3 start,
        IReadOnlyList<Vector3> route,
        Vector3 destination,
        DemolitionTeam? movingTeam)
    {
        var simplified = new List<Vector3>();
        var anchor = start;
        var index = 0;
        while (index < route.Count)
        {
            var furthest = index;
            for (var candidate = route.Count - 1; candidate > index; candidate--)
            {
                if (CanUseRouteSegment(anchor, route[candidate])
                    && TacticalSegmentPenalty(
                        anchor,
                        route[candidate],
                        destination,
                        movingTeam) <= 0.001f)
                {
                    furthest = candidate;
                    break;
                }
            }
            var waypoint = route[furthest];
            simplified.Add(waypoint);
            anchor = waypoint;
            index = furthest + 1;
        }
        return simplified.ToArray();
    }

    private void TryConnect(
        List<RouteEdge>[] edges,
        int from,
        int to,
        Vector3 fromPoint,
        Vector3 toPoint,
        bool allowElevationChange)
    {
        if (from == to
            || edges[from].Exists(edge => edge.To == to)
            || !CanTraverse(fromPoint, toPoint, allowElevationChange))
        {
            return;
        }
        AddBidirectionalEdge(edges, from, to, fromPoint.DistanceTo(toPoint));
    }

    private bool CanUseRouteSegment(Vector3 from, Vector3 to)
    {
        var fromTransition = _elevationTransitions.IsNearTransition(from);
        var toTransition = _elevationTransitions.IsNearTransition(to);
        if (!fromTransition && !toTransition)
        {
            return CanTraverse(from, to, allowElevationChange: false);
        }
        if (_elevationTransitions.SharesSegment(from, to))
        {
            return CanTraverse(from, to, allowElevationChange: true);
        }

        var fromMayUseAutomaticEdge = !fromTransition
            || _elevationTransitions.IsBoundaryPoint(from);
        var toMayUseAutomaticEdge = !toTransition
            || _elevationTransitions.IsBoundaryPoint(to);
        return fromMayUseAutomaticEdge
            && toMayUseAutomaticEdge
            && CanTraverse(from, to, allowElevationChange: false);
    }

    private bool CanTraverse(Vector3 from, Vector3 to, bool allowElevationChange)
    {
        var verticalDelta = Mathf.Abs(to.Y - from.Y);
        var horizontalDistance = HorizontalDistance(from, to);
        if (allowElevationChange)
        {
            if (horizontalDistance <= 0.08f
                ? verticalDelta > MaximumAutomaticVerticalDelta
                : verticalDelta / horizontalDistance > MaximumAuthoredGrade + 0.001f)
            {
                return false;
            }
        }
        else if (verticalDelta > MaximumAutomaticVerticalDelta
            || (horizontalDistance <= 0.08f
                ? verticalDelta > 0.25f
                : verticalDelta / horizontalDistance > MaximumAuthoredGrade + 0.001f))
        {
            return false;
        }
        return _layout.HasCapsuleClearance(new[] { from, to }, out _);
    }

    private static int[] AddAuthoredPath(
        List<Vector3> nodes,
        List<(int From, int To)> links,
        IReadOnlyList<Vector3> path)
    {
        var pathNodes = new int[path.Count];
        var previous = -1;
        for (var index = 0; index < path.Count; index++)
        {
            var current = AddPoint(nodes, path[index]);
            pathNodes[index] = current;
            if (previous >= 0 && previous != current)
            {
                links.Add((previous, current));
            }
            previous = current;
        }
        return pathNodes;
    }

    private static void AddPoints(List<Vector3> nodes, IReadOnlyList<Vector3> points)
    {
        foreach (var point in points)
        {
            AddPoint(nodes, point);
        }
    }

    private static int AddPoint(List<Vector3> nodes, Vector3 point)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].DistanceSquaredTo(point) <= DuplicatePointDistanceSquared)
            {
                return index;
            }
        }
        nodes.Add(point);
        return nodes.Count - 1;
    }

    private static List<RouteEdge>[] CreateEdgeLists(int count)
    {
        var edges = new List<RouteEdge>[count];
        for (var index = 0; index < count; index++)
        {
            edges[index] = new List<RouteEdge>();
        }
        return edges;
    }

    private static void AddBidirectionalEdge(List<RouteEdge>[] edges, int from, int to, float cost)
    {
        edges[from].Add(new RouteEdge(to, cost));
        edges[to].Add(new RouteEdge(from, cost));
    }

    private static float RouteLength(Vector3 start, IReadOnlyList<Vector3> route)
    {
        var length = 0.0f;
        var previous = start;
        foreach (var point in route)
        {
            length += previous.DistanceTo(point);
            previous = point;
        }
        return length;
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
        => Mathf.Sqrt(HorizontalDistanceSquared(left, right));

    private static float HorizontalDistanceSquared(Vector3 left, Vector3 right)
    {
        var dx = left.X - right.X;
        var dz = left.Z - right.Z;
        return dx * dx + dz * dz;
    }

    private readonly record struct RouteEdge(int To, float Cost);
    private readonly record struct RouteSearchResult(int[] Previous, float[] Distances);
}
