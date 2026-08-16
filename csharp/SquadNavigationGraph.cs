using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct SquadNavigationGraphEdge(
    int From,
    int To,
    float Cost,
    SquadNavigationDirective[] Directives);

/// <summary>Pure deterministic shortest-path selection for the sparse portal graph.</summary>
internal static class SquadNavigationGraph
{
    public static SquadNavigationDirective[]? FindShortestPath(
        int nodeCount,
        int start,
        int goal,
        IReadOnlyList<SquadNavigationGraphEdge> edges,
        out float cost)
    {
        cost = float.PositiveInfinity;
        if (nodeCount <= 0 || start < 0 || goal < 0 || start >= nodeCount || goal >= nodeCount)
        {
            return null;
        }
        if (start == goal)
        {
            cost = 0.0f;
            return Array.Empty<SquadNavigationDirective>();
        }

        var distances = new float[nodeCount];
        var previousEdges = new int[nodeCount];
        var visited = new bool[nodeCount];
        Array.Fill(distances, float.PositiveInfinity);
        Array.Fill(previousEdges, -1);
        distances[start] = 0.0f;

        for (var iteration = 0; iteration < nodeCount; iteration++)
        {
            var current = -1;
            var best = float.PositiveInfinity;
            for (var node = 0; node < nodeCount; node++)
            {
                if (visited[node] || distances[node] >= best)
                {
                    continue;
                }
                best = distances[node];
                current = node;
            }
            if (current < 0 || current == goal)
            {
                break;
            }
            visited[current] = true;

            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                var edge = edges[edgeIndex];
                if (edge.From != current || edge.To < 0 || edge.To >= nodeCount || visited[edge.To])
                {
                    continue;
                }
                var candidate = distances[current] + Mathf.Max(0.0f, edge.Cost);
                if (candidate + 0.0001f >= distances[edge.To])
                {
                    continue;
                }
                distances[edge.To] = candidate;
                previousEdges[edge.To] = edgeIndex;
            }
        }

        if (previousEdges[goal] < 0)
        {
            return null;
        }

        var reversed = new List<int>();
        var cursor = goal;
        while (cursor != start)
        {
            var edgeIndex = previousEdges[cursor];
            if (edgeIndex < 0)
            {
                return null;
            }
            reversed.Add(edgeIndex);
            cursor = edges[edgeIndex].From;
        }
        reversed.Reverse();

        var directives = new List<SquadNavigationDirective>();
        foreach (var edgeIndex in reversed)
        {
            directives.AddRange(edges[edgeIndex].Directives);
        }
        cost = distances[goal];
        return directives.ToArray();
    }
}
