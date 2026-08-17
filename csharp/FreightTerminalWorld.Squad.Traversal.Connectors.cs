using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly HashSet<long> _squadBlockedPortalWalkCorridors = new();
    private readonly HashSet<long> _squadClearPortalWalkCorridors = new();
    private readonly List<(int First, int Second)> _squadPortalWalkConnectorEdges = new();
    private const double SquadPortalWalkWarmBudgetMilliseconds = 8.0;
    private bool _squadPortalWalkCorridorCacheReady;
    private bool _squadPortalWalkCorridorCacheWarming;
    private int _squadPortalWalkCorridorCacheGeneration;
    private int _squadPortalWalkWarmPasses;
    private ulong _squadPortalWalkWarmMaximumMicroseconds;

    private int SquadPortalWalkWarmPassesForDiagnostics => _squadPortalWalkWarmPasses;
    private ulong SquadPortalWalkWarmMaximumMicrosecondsForDiagnostics
        => _squadPortalWalkWarmMaximumMicroseconds;

    private readonly record struct SquadPortalComponentBridge(
        int FirstComponent,
        int SecondComponent,
        float DistanceSquared);

    private readonly record struct SquadPortalPairCandidate(
        int First,
        int Second,
        float DistanceSquared);

    private sealed class SquadPortalWalkWarmState
    {
        public SquadPortalWalkWarmState(
            List<SquadPortalNode> nodes,
            List<SquadNavigationGraphEdge> graphEdges,
            List<List<int>> components)
        {
            Nodes = nodes;
            GraphEdges = graphEdges;
            Components = components;
            ConnectedParent = new int[components.Count];
            for (var component = 0; component < components.Count; component++)
            {
                ConnectedParent[component] = component;
            }
            RankingSecondComponent = components.Count > 1 ? 1 : 0;
        }

        public List<SquadPortalNode> Nodes { get; }
        public List<SquadNavigationGraphEdge> GraphEdges { get; }
        public List<List<int>> Components { get; }
        public int[] ConnectedParent { get; }
        public PriorityQueue<SquadPortalComponentBridge, (float Distance, int First, int Second)>
            Bridges { get; } = new();
        public PriorityQueue<SquadPortalPairCandidate, (float Distance, int First, int Second)>
            ActiveCandidates { get; } = new();
        public int RankingFirstComponent { get; set; }
        public int RankingSecondComponent { get; set; }
        public int RankingFirstNode { get; set; }
        public int RankingSecondNode { get; set; }
        public float RankingBestDistanceSquared { get; set; } = float.PositiveInfinity;
        public bool BridgeRankingComplete { get; set; }
        public SquadPortalComponentBridge? ActiveBridge { get; set; }
        public int CandidateFirstNode { get; set; }
        public int CandidateSecondNode { get; set; }
        public bool CandidateRankingComplete { get; set; }
    }

    private void ResetSquadPortalWalkConnectorCache()
    {
        _squadBlockedPortalWalkCorridors.Clear();
        _squadClearPortalWalkCorridors.Clear();
        _squadPortalWalkConnectorEdges.Clear();
        _squadPortalWalkCorridorCacheReady = false;
        _squadPortalWalkCorridorCacheWarming = false;
        _squadPortalWalkWarmPasses = 0;
        _squadPortalWalkWarmMaximumMicroseconds = 0;
        _squadPortalWalkCorridorCacheGeneration++;
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
        var generation = _squadPortalWalkCorridorCacheGeneration;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!IsInsideTree()
            || _squadTraversalLinks.Count == 0
            || generation != _squadPortalWalkCorridorCacheGeneration)
        {
            if (generation == _squadPortalWalkCorridorCacheGeneration)
            {
                _squadPortalWalkCorridorCacheWarming = false;
            }
            return;
        }

        var nodes = new List<SquadPortalNode>();
        var nodeLookup = new Dictionary<(int X, int Z, int Bucket), int>();
        var authoredEdges = new List<SquadNavigationGraphEdge>();
        foreach (var link in _squadTraversalLinks)
        {
            var from = GetOrAddSquadPortalNode(link.ForwardPoints[0], nodes, nodeLookup);
            var to = GetOrAddSquadPortalNode(link.ForwardPoints[^1], nodes, nodeLookup);
            authoredEdges.Add(new SquadNavigationGraphEdge(
                from,
                to,
                link.Cost,
                Array.Empty<SquadNavigationDirective>()));
            if (link.Bidirectional)
            {
                authoredEdges.Add(new SquadNavigationGraphEdge(
                    to,
                    from,
                    link.Cost,
                    Array.Empty<SquadNavigationDirective>()));
            }
        }

        var warmState = BuildSquadPortalWalkWarmState(nodes, authoredEdges);
        if (warmState.Components.Count <= 1)
        {
            _squadPortalWalkCorridorCacheReady = true;
            _squadPortalWalkCorridorCacheWarming = false;
            return;
        }

        while (IsInsideTree()
            && _squadTraversalLinks.Count > 0
            && generation == _squadPortalWalkCorridorCacheGeneration)
        {
            var exclude = BuildSquadPortalWalkConnectorExclusions();
            using var excludeBacking = exclude.AsDisposable();
            var budget = new SquadNavSearchBudget(
                int.MaxValue,
                SquadPortalWalkWarmBudgetMilliseconds);
            var passStarted = Time.GetTicksUsec();
            var complete = AdvanceSquadPortalWalkConnectorCache(warmState, exclude, budget);
            var passMicroseconds = Time.GetTicksUsec() - passStarted;
            _squadPortalWalkWarmPasses++;
            _squadPortalWalkWarmMaximumMicroseconds = Math.Max(
                _squadPortalWalkWarmMaximumMicroseconds,
                passMicroseconds);
            if (complete)
            {
                _squadPortalWalkCorridorCacheReady = true;
                _squadPortalWalkCorridorCacheWarming = false;
                return;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (generation == _squadPortalWalkCorridorCacheGeneration)
        {
            _squadPortalWalkCorridorCacheWarming = false;
        }
    }

    private static SquadPortalWalkWarmState BuildSquadPortalWalkWarmState(
        List<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges)
    {
        var authoredParent = new int[nodes.Count];
        for (var node = 0; node < nodes.Count; node++)
        {
            authoredParent[node] = node;
        }
        foreach (var edge in graphEdges)
        {
            if (edge.From >= 0 && edge.From < nodes.Count
                && edge.To >= 0 && edge.To < nodes.Count)
            {
                UnionSquadPortalComponents(authoredParent, edge.From, edge.To);
            }
        }

        var componentByRoot = new Dictionary<int, int>();
        var components = new List<List<int>>();
        for (var node = 0; node < nodes.Count; node++)
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
        return new SquadPortalWalkWarmState(nodes, graphEdges, components);
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

    private bool AddSquadPortalWalkConnectors(
        int portalNodeCount,
        IReadOnlyList<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges)
    {
        foreach (var connector in _squadPortalWalkConnectorEdges)
        {
            if (connector.First < 0 || connector.First >= portalNodeCount
                || connector.Second < 0 || connector.Second >= portalNodeCount)
            {
                continue;
            }
            AddSquadPortalWalkConnectorEdges(
                connector.First,
                connector.Second,
                nodes,
                graphEdges);
        }
        return _squadPortalWalkCorridorCacheReady;
    }

    private bool AdvanceSquadPortalWalkConnectorCache(
        SquadPortalWalkWarmState state,
        Godot.Collections.Array<Rid> exclude,
        SquadNavSearchBudget budget)
    {
        if (!state.BridgeRankingComplete)
        {
            if (!RankSquadPortalComponentBridges(state, budget))
            {
                return false;
            }
            state.BridgeRankingComplete = true;
        }

        while (budget.CanProbe)
        {
            if (state.ActiveBridge is null)
            {
                if (!TryBeginNextSquadPortalBridge(state))
                {
                    return true;
                }
            }

            if (!state.CandidateRankingComplete)
            {
                if (!RankActiveSquadPortalBridgeCandidates(state, budget))
                {
                    return false;
                }
                state.CandidateRankingComplete = true;
            }

            var connected = false;
            while (state.ActiveCandidates.TryDequeue(out var candidate, out var priority))
            {
                if (!budget.CanProbe)
                {
                    state.ActiveCandidates.Enqueue(candidate, priority);
                    return false;
                }
                if (TryAddSquadPortalWalkConnector(
                        candidate.First,
                        candidate.Second,
                        state.Nodes,
                        state.GraphEdges,
                        exclude,
                        budget))
                {
                    var bridge = state.ActiveBridge
                        ?? throw new InvalidOperationException("Portal warm bridge state was lost.");
                    UnionSquadPortalComponents(
                        state.ConnectedParent,
                        bridge.FirstComponent,
                        bridge.SecondComponent);
                    connected = true;
                    break;
                }
                if (!budget.CanProbe)
                {
                    state.ActiveCandidates.Enqueue(candidate, priority);
                    return false;
                }
            }

            state.ActiveCandidates.Clear();
            state.ActiveBridge = null;
            state.CandidateRankingComplete = false;
            if (!connected && !budget.CanProbe)
            {
                return false;
            }
        }
        return false;
    }

    private static bool RankSquadPortalComponentBridges(
        SquadPortalWalkWarmState state,
        SquadNavSearchBudget budget)
    {
        while (state.RankingFirstComponent < state.Components.Count - 1)
        {
            if (!budget.CanProbe)
            {
                return false;
            }

            var firstComponent = state.Components[state.RankingFirstComponent];
            var secondComponent = state.Components[state.RankingSecondComponent];
            if (state.RankingFirstNode >= firstComponent.Count)
            {
                CompleteSquadPortalBridgeRanking(state);
                continue;
            }
            if (state.RankingSecondNode >= secondComponent.Count)
            {
                state.RankingFirstNode++;
                state.RankingSecondNode = 0;
                continue;
            }

            var first = firstComponent[state.RankingFirstNode];
            var second = secondComponent[state.RankingSecondNode++];
            if (state.Nodes[first].Bucket != state.Nodes[second].Bucket)
            {
                continue;
            }
            var offset = state.Nodes[second].Position - state.Nodes[first].Position;
            offset.Y = 0.0f;
            state.RankingBestDistanceSquared = Math.Min(
                state.RankingBestDistanceSquared,
                offset.LengthSquared());
        }
        return true;
    }

    private static void CompleteSquadPortalBridgeRanking(SquadPortalWalkWarmState state)
    {
        if (!float.IsPositiveInfinity(state.RankingBestDistanceSquared))
        {
            var bridge = new SquadPortalComponentBridge(
                state.RankingFirstComponent,
                state.RankingSecondComponent,
                state.RankingBestDistanceSquared);
            state.Bridges.Enqueue(
                bridge,
                (bridge.DistanceSquared, bridge.FirstComponent, bridge.SecondComponent));
        }

        state.RankingSecondComponent++;
        if (state.RankingSecondComponent >= state.Components.Count)
        {
            state.RankingFirstComponent++;
            state.RankingSecondComponent = state.RankingFirstComponent + 1;
        }
        state.RankingFirstNode = 0;
        state.RankingSecondNode = 0;
        state.RankingBestDistanceSquared = float.PositiveInfinity;
    }

    private static bool TryBeginNextSquadPortalBridge(SquadPortalWalkWarmState state)
    {
        while (state.Bridges.TryDequeue(out var bridge, out _))
        {
            if (FindSquadPortalComponent(state.ConnectedParent, bridge.FirstComponent)
                == FindSquadPortalComponent(state.ConnectedParent, bridge.SecondComponent))
            {
                continue;
            }
            state.ActiveBridge = bridge;
            state.CandidateFirstNode = 0;
            state.CandidateSecondNode = 0;
            state.CandidateRankingComplete = false;
            return true;
        }
        return false;
    }

    private static bool RankActiveSquadPortalBridgeCandidates(
        SquadPortalWalkWarmState state,
        SquadNavSearchBudget budget)
    {
        var bridge = state.ActiveBridge!.Value;
        var firstComponent = state.Components[bridge.FirstComponent];
        var secondComponent = state.Components[bridge.SecondComponent];
        while (state.CandidateFirstNode < firstComponent.Count)
        {
            if (!budget.CanProbe)
            {
                return false;
            }
            if (state.CandidateSecondNode >= secondComponent.Count)
            {
                state.CandidateFirstNode++;
                state.CandidateSecondNode = 0;
                continue;
            }

            var first = firstComponent[state.CandidateFirstNode];
            var second = secondComponent[state.CandidateSecondNode++];
            if (state.Nodes[first].Bucket != state.Nodes[second].Bucket)
            {
                continue;
            }
            var offset = state.Nodes[second].Position - state.Nodes[first].Position;
            offset.Y = 0.0f;
            var candidate = new SquadPortalPairCandidate(first, second, offset.LengthSquared());
            state.ActiveCandidates.Enqueue(
                candidate,
                (candidate.DistanceSquared, candidate.First, candidate.Second));
        }
        return true;
    }

    private bool TryAddSquadPortalWalkConnector(
        int first,
        int second,
        IReadOnlyList<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges,
        Godot.Collections.Array<Rid> exclude,
        SquadNavSearchBudget? budget)
    {
        if (budget is not null && !budget.CanProbe)
        {
            return false;
        }
        var from = nodes[first].Position;
        var to = nodes[second].Position;
        var key = SquadPortalNodePairKey(first, second);
        if (_squadBlockedPortalWalkCorridors.Contains(key))
        {
            return false;
        }
        if (!_squadClearPortalWalkCorridors.Contains(key))
        {
            var clear = IsSquadPortalCenterRayClear(from, to, exclude, budget)
                && IsSquadMovementCorridorClearExcluding(from, to, exclude, budget);
            if (!clear)
            {
                if (budget is null || budget.CanProbe)
                {
                    _squadBlockedPortalWalkCorridors.Add(key);
                }
                return false;
            }
            _squadClearPortalWalkCorridors.Add(key);
            _squadPortalWalkConnectorEdges.Add((first, second));
        }

        AddSquadPortalWalkConnectorEdges(first, second, nodes, graphEdges);
        return true;
    }

    private static void AddSquadPortalWalkConnectorEdges(
        int first,
        int second,
        IReadOnlyList<SquadPortalNode> nodes,
        List<SquadNavigationGraphEdge> graphEdges)
    {
        var from = nodes[first].Position;
        var to = nodes[second].Position;
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
