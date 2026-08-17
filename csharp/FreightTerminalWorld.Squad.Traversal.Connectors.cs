using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly HashSet<long> _squadBlockedPortalWalkCorridors = new();
    private bool _squadPortalWalkCorridorCacheReady;
    private bool _squadPortalWalkCorridorCacheWarming;

    private readonly record struct SquadPortalComponentBridge(
        int FirstComponent,
        int SecondComponent,
        float DistanceSquared);

    private void ResetSquadPortalWalkConnectorCache()
    {
        _squadBlockedPortalWalkCorridors.Clear();
        _squadPortalWalkCorridorCacheReady = false;
        _squadPortalWalkCorridorCacheWarming = false;
    }

    private async void WarmSquadPortalWalkConnectorCache()
    {
        if (_squadPortalWalkCorridorCacheReady
            || _squadPortalWalkCorridorCacheWarming
            || _squadTraversalLinks.Count == 0)
        {
            return;
        }

        _squadPortalWalkCorridorCacheWarming = true;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!IsInsideTree() || _squadTraversalLinks.Count == 0)
        {
            _squadPortalWalkCorridorCacheWarming = false;
            return;
        }

        var nodes = new List<SquadPortalNode>();
        var nodeLookup = new Dictionary<(int X, int Z, int Bucket), int>();
        var graphEdges = new List<SquadNavigationGraphEdge>();
        foreach (var link in _squadTraversalLinks)
        {
            var from = GetOrAddSquadPortalNode(link.ForwardPoints[0], nodes, nodeLookup);
            var to = GetOrAddSquadPortalNode(link.ForwardPoints[^1], nodes, nodeLookup);
            graphEdges.Add(new SquadNavigationGraphEdge(
                from,
                to,
                link.Cost,
                Array.Empty<SquadNavigationDirective>()));
            if (link.Bidirectional)
            {
                graphEdges.Add(new SquadNavigationGraphEdge(
                    to,
                    from,
                    link.Cost,
                    Array.Empty<SquadNavigationDirective>()));
            }
        }

        AddSquadPortalWalkConnectors(
            nodes.Count,
            nodes,
            graphEdges,
            BuildSquadPortalWalkConnectorExclusions());
        _squadPortalWalkCorridorCacheReady = true;
        _squadPortalWalkCorridorCacheWarming = false;
    }

    private Godot.Collections.Array<Rid> BuildSquadPortalWalkConnectorExclusions()
    {
        var exclude = BuildSquadNavExclusions();
        foreach (var vehicle in _vehicles)
        {
            if (IsInstanceValid(vehicle))
            {
                exclude.Add(vehicle.GetRid());
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                exclude.Add(mate.GetRid());
            }
        }
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                exclude.Add(enemy.GetRid());
            }
        }
        return exclude;
    }

    private void AddSquadPortalWalkConnectors(
        int portalNodeCount,
        IReadOnlyList<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges,
        Godot.Collections.Array<Rid> exclude)
    {
        var authoredParent = new int[portalNodeCount];
        for (var node = 0; node < portalNodeCount; node++)
        {
            authoredParent[node] = node;
        }
        foreach (var edge in graphEdges)
        {
            if (edge.From >= 0 && edge.From < portalNodeCount
                && edge.To >= 0 && edge.To < portalNodeCount)
            {
                UnionSquadPortalComponents(authoredParent, edge.From, edge.To);
            }
        }

        var componentByRoot = new Dictionary<int, int>();
        var components = new List<List<int>>();
        for (var node = 0; node < portalNodeCount; node++)
        {
            var root = FindSquadPortalComponent(authoredParent, node);
            if (!componentByRoot.TryGetValue(root, out var component))
            {
                component = components.Count;
                componentByRoot[root] = component;
                components.Add(new List<int>());
            }
            components[component].Add(node);
        }
        if (components.Count <= 1)
        {
            return;
        }

        var bridges = new List<SquadPortalComponentBridge>();
        for (var first = 0; first < components.Count; first++)
        {
            for (var second = first + 1; second < components.Count; second++)
            {
                var ranked = RankSquadPortalComponentPairs(
                    components[first],
                    components[second],
                    nodes);
                if (ranked.Count == 0)
                {
                    continue;
                }
                bridges.Add(new SquadPortalComponentBridge(
                    first,
                    second,
                    ranked[0].DistanceSquared));
            }
        }
        bridges.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));

        var connectedParent = new int[components.Count];
        for (var component = 0; component < components.Count; component++)
        {
            connectedParent[component] = component;
        }
        foreach (var bridge in bridges)
        {
            if (FindSquadPortalComponent(connectedParent, bridge.FirstComponent)
                == FindSquadPortalComponent(connectedParent, bridge.SecondComponent))
            {
                continue;
            }
            if (!TryAddSquadPortalComponentBridge(
                    components[bridge.FirstComponent],
                    components[bridge.SecondComponent],
                    nodes,
                    graphEdges,
                    exclude))
            {
                continue;
            }
            UnionSquadPortalComponents(
                connectedParent,
                bridge.FirstComponent,
                bridge.SecondComponent);
        }
    }

    private bool TryAddSquadPortalComponentBridge(
        IReadOnlyList<int> firstComponent,
        IReadOnlyList<int> secondComponent,
        IReadOnlyList<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges,
        Godot.Collections.Array<Rid> exclude)
    {
        var ranked = RankSquadPortalComponentPairs(firstComponent, secondComponent, nodes);
        foreach (var candidate in ranked)
        {
            if (TryAddSquadPortalWalkConnector(
                    candidate.First,
                    candidate.Second,
                    nodes,
                    graphEdges,
                    exclude))
            {
                return true;
            }
        }
        return false;
    }

    private static List<(int First, int Second, float DistanceSquared)> RankSquadPortalComponentPairs(
        IReadOnlyList<int> firstComponent,
        IReadOnlyList<int> secondComponent,
        IReadOnlyList<SquadPortalNode> nodes)
    {
        var ranked = new List<(int First, int Second, float DistanceSquared)>();
        foreach (var first in firstComponent)
        {
            foreach (var second in secondComponent)
            {
                if (nodes[first].Bucket != nodes[second].Bucket)
                {
                    continue;
                }
                var offset = nodes[second].Position - nodes[first].Position;
                offset.Y = 0.0f;
                ranked.Add((first, second, offset.LengthSquared()));
            }
        }
        ranked.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
        return ranked;
    }

    private bool TryAddSquadPortalWalkConnector(
        int first,
        int second,
        IReadOnlyList<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges,
        Godot.Collections.Array<Rid> exclude)
    {
        var from = nodes[first].Position;
        var to = nodes[second].Position;
        var key = SquadPortalNodePairKey(first, second);
        if (_squadBlockedPortalWalkCorridors.Contains(key))
        {
            return false;
        }
        var clear = IsSquadPortalCenterRayClear(from, to, exclude)
            && IsSquadMovementCorridorClearExcluding(from, to, exclude);
        if (!clear)
        {
            _squadBlockedPortalWalkCorridors.Add(key);
            return false;
        }

        graphEdges.Add(new SquadNavigationGraphEdge(
            first,
            second,
            from.DistanceTo(to),
            new[] { SquadNavigationDirective.Walk(to) }));
        graphEdges.Add(new SquadNavigationGraphEdge(
            second,
            first,
            from.DistanceTo(to),
            new[] { SquadNavigationDirective.Walk(from) }));
        return true;
    }

    private static long SquadPortalNodePairKey(int first, int second)
    {
        var minimum = Math.Min(first, second);
        var maximum = Math.Max(first, second);
        return (long)minimum << 32 | (uint)maximum;
    }

    private static int[] BuildSquadPortalComponentMap(
        int portalNodeCount,
        IReadOnlyList<SquadNavigationGraphEdge> graphEdges)
    {
        var forward = new List<int>[portalNodeCount];
        var reverse = new List<int>[portalNodeCount];
        for (var node = 0; node < portalNodeCount; node++)
        {
            forward[node] = new List<int>();
            reverse[node] = new List<int>();
        }
        foreach (var edge in graphEdges)
        {
            if (edge.From >= 0 && edge.From < portalNodeCount
                && edge.To >= 0 && edge.To < portalNodeCount)
            {
                forward[edge.From].Add(edge.To);
                reverse[edge.To].Add(edge.From);
            }
        }

        var visited = new bool[portalNodeCount];
        var finishOrder = new List<int>(portalNodeCount);
        for (var node = 0; node < portalNodeCount; node++)
        {
            VisitSquadPortalComponent(node, forward, visited, finishOrder);
        }

        var componentMap = new int[portalNodeCount];
        System.Array.Fill(componentMap, -1);
        var component = 0;
        for (var index = finishOrder.Count - 1; index >= 0; index--)
        {
            var node = finishOrder[index];
            if (componentMap[node] >= 0)
            {
                continue;
            }
            AssignSquadPortalComponent(node, component, reverse, componentMap);
            component++;
        }
        return componentMap;
    }

    private static void VisitSquadPortalComponent(
        int node,
        IReadOnlyList<List<int>> graph,
        bool[] visited,
        List<int> finishOrder)
    {
        if (visited[node])
        {
            return;
        }
        visited[node] = true;
        foreach (var next in graph[node])
        {
            VisitSquadPortalComponent(next, graph, visited, finishOrder);
        }
        finishOrder.Add(node);
    }

    private static void AssignSquadPortalComponent(
        int node,
        int component,
        IReadOnlyList<List<int>> reverseGraph,
        int[] componentMap)
    {
        if (componentMap[node] >= 0)
        {
            return;
        }
        componentMap[node] = component;
        foreach (var next in reverseGraph[node])
        {
            AssignSquadPortalComponent(next, component, reverseGraph, componentMap);
        }
    }

    private static int FindSquadPortalComponent(int[] parent, int node)
    {
        while (parent[node] != node)
        {
            parent[node] = parent[parent[node]];
            node = parent[node];
        }
        return node;
    }

    private static void UnionSquadPortalComponents(int[] parent, int first, int second)
    {
        var firstRoot = FindSquadPortalComponent(parent, first);
        var secondRoot = FindSquadPortalComponent(parent, second);
        if (firstRoot != secondRoot)
        {
            parent[secondRoot] = firstRoot;
        }
    }
}
