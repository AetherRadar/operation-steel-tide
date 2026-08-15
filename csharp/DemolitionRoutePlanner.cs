using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public readonly record struct DemolitionRouteResult(
    IReadOnlyList<Vector3> Waypoints,
    bool ReachesDestination,
    float Length);

/// <summary>
/// Pure arena route planner. It connects the authored lanes and cover posts into a small
/// visibility graph, then finds a deterministic shortest route for objective movement.
/// </summary>
public sealed class DemolitionRoutePlanner
{
    private const float MaximumVisibilityEdge = 24.0f;
    private const float MaximumEndpointEdge = 34.0f;
    private const float DuplicatePointDistanceSquared = 0.08f * 0.08f;

    private readonly DemolitionArenaLayout _layout;
    private readonly Vector3[] _nodes;
    private readonly List<RouteEdge>[] _baseEdges;

    public DemolitionRoutePlanner(DemolitionArenaLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        var nodes = new List<Vector3>();
        var authoredLinks = new List<(int From, int To)>();
        AddAuthoredPath(nodes, authoredLinks, layout.AttackToAPath);
        AddAuthoredPath(nodes, authoredLinks, layout.AttackToBPath);
        AddAuthoredPath(nodes, authoredLinks, layout.AttackMidPath);
        AddAuthoredPath(nodes, authoredLinks, layout.DefenderToAPath);
        AddAuthoredPath(nodes, authoredLinks, layout.DefenderToBPath);
        AddAuthoredPath(nodes, authoredLinks, layout.SiteRotationPath);
        AddPoints(nodes, layout.AttackSpawns);
        AddPoints(nodes, layout.DefenderSpawns);
        AddPoints(nodes, layout.SitePositions);
        AddPoints(nodes, layout.CoverPoints);
        AddPoint(nodes, layout.Midpoint);

        _nodes = nodes.ToArray();
        _baseEdges = CreateEdgeLists(_nodes.Length);
        foreach (var link in authoredLinks)
        {
            TryConnect(_baseEdges, link.From, link.To, _nodes[link.From], _nodes[link.To]);
        }
        for (var from = 0; from < _nodes.Length; from++)
        {
            for (var to = from + 1; to < _nodes.Length; to++)
            {
                if (HorizontalDistance(_nodes[from], _nodes[to]) <= MaximumVisibilityEdge)
                {
                    TryConnect(_baseEdges, from, to, _nodes[from], _nodes[to]);
                }
            }
        }
    }

    public DemolitionRouteResult Plan(Vector3 start, Vector3 destination)
    {
        destination.Y = start.Y;
        if (CanTraverse(start, destination))
        {
            return new DemolitionRouteResult(
                new[] { destination },
                true,
                HorizontalDistance(start, destination));
        }

        var startIndex = _nodes.Length;
        var destinationIndex = startIndex + 1;
        var edges = CloneEdgesWithEndpoints();
        ConnectEndpoint(edges, startIndex, start, MaximumEndpointEdge);
        ConnectEndpoint(edges, destinationIndex, destination, MaximumEndpointEdge);
        if (edges[startIndex].Count == 0)
        {
            ConnectEndpoint(edges, startIndex, start, float.PositiveInfinity);
        }
        if (edges[destinationIndex].Count == 0)
        {
            ConnectEndpoint(edges, destinationIndex, destination, float.PositiveInfinity);
        }

        var search = FindShortestPath(edges, startIndex, destinationIndex);
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
            frontier.Y = start.Y;
            var frontierRoute = ReconstructRoute(
                search.Previous,
                startIndex,
                frontierIndex,
                start,
                frontier);
            var safeRoute = SimplifyRoute(start, frontierRoute);
            return new DemolitionRouteResult(
                safeRoute,
                false,
                RouteLength(start, safeRoute));
        }

        var route = ReconstructRoute(search.Previous, startIndex, destinationIndex, start, destination);
        var simplified = SimplifyRoute(start, route);
        return new DemolitionRouteResult(simplified, true, RouteLength(start, simplified));
    }

    public bool IsRouteClear(Vector3 start, IReadOnlyList<Vector3> waypoints)
    {
        var from = start;
        for (var index = 0; index < waypoints.Count; index++)
        {
            var to = waypoints[index];
            to.Y = from.Y;
            if (!CanTraverse(from, to))
            {
                return false;
            }
            from = to;
        }
        return true;
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
        for (var nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
        {
            var distance = HorizontalDistance(endpoint, _nodes[nodeIndex]);
            if (distance > maximumDistance || !CanTraverse(endpoint, _nodes[nodeIndex]))
            {
                continue;
            }
            AddBidirectionalEdge(edges, endpointIndex, nodeIndex, distance);
        }
    }

    private RouteSearchResult FindShortestPath(
        List<RouteEdge>[] edges,
        int startIndex,
        int destinationIndex)
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
                var candidateDistance = currentDistance + edge.Cost;
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

            var destinationDistance = HorizontalDistanceSquared(_nodes[index], destination);
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
            point.Y = start.Y;
            reversed.Add(point);
            current = previous[current];
        }
        reversed.Reverse();
        return reversed.ToArray();
    }

    private Vector3[] SimplifyRoute(Vector3 start, IReadOnlyList<Vector3> route)
    {
        var simplified = new List<Vector3>();
        var anchor = start;
        var index = 0;
        while (index < route.Count)
        {
            var furthest = index;
            for (var candidate = route.Count - 1; candidate > index; candidate--)
            {
                if (CanTraverse(anchor, route[candidate]))
                {
                    furthest = candidate;
                    break;
                }
            }
            var waypoint = route[furthest];
            waypoint.Y = start.Y;
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
        Vector3 toPoint)
    {
        if (from == to || edges[from].Exists(edge => edge.To == to) || !CanTraverse(fromPoint, toPoint))
        {
            return;
        }
        AddBidirectionalEdge(edges, from, to, HorizontalDistance(fromPoint, toPoint));
    }

    private bool CanTraverse(Vector3 from, Vector3 to)
        => _layout.HasCapsuleClearance(new[] { from, to }, out _);

    private static void AddAuthoredPath(
        List<Vector3> nodes,
        List<(int From, int To)> links,
        IReadOnlyList<Vector3> path)
    {
        var previous = -1;
        foreach (var point in path)
        {
            var current = AddPoint(nodes, point);
            if (previous >= 0 && previous != current)
            {
                links.Add((previous, current));
            }
            previous = current;
        }
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
            if (HorizontalDistanceSquared(nodes[index], point) <= DuplicatePointDistanceSquared)
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
            length += HorizontalDistance(previous, point);
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
