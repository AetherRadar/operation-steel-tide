using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int SquadTraversalConnectorComponentBatchSize = 4;
    private const ulong SquadTraversalFailureBaseMilliseconds = 6000;
    private const ulong SquadTraversalFailureMaximumMilliseconds = 30000;

    private readonly List<SquadTraversalLink> _squadTraversalLinks = new();
    private readonly Dictionary<(ulong MateId, int DirectedEdgeId), (ulong ExpiresMsec, int Failures)>
        _squadTraversalFailures = new();
    private readonly Dictionary<(ulong MateId, int DirectedEdgeId), int> _squadTraversalRecoveryAttempts = new();

    private readonly record struct SquadPortalNode(Vector3 Position, int Bucket);

    private void ResetSquadTraversalLinks()
    {
        _squadTraversalLinks.Clear();
        _squadTraversalFailures.Clear();
        _squadTraversalRecoveryAttempts.Clear();
        ResetSquadPortalWalkConnectorCache();
    }

    private int RegisterSquadTraversalLink(
        string source,
        SquadTraversalKind kind,
        bool bidirectional,
        IReadOnlyList<Vector3> forwardPoints,
        float costMultiplier = 1.0f)
    {
        if (forwardPoints.Count < 2)
        {
            return -1;
        }

        var points = new Vector3[forwardPoints.Count];
        var cost = 0.0f;
        for (var index = 0; index < forwardPoints.Count; index++)
        {
            points[index] = forwardPoints[index];
            if (index > 0)
            {
                cost += forwardPoints[index - 1].DistanceTo(forwardPoints[index]);
            }
        }
        var id = _squadTraversalLinks.Count;
        var forwardDirectives = BuildSquadLinkDirectives(id, kind, points, forward: true);
        var reverseDirectives = bidirectional
            ? BuildSquadLinkDirectives(id, kind, points, forward: false)
            : Array.Empty<SquadNavigationDirective>();
        _squadTraversalLinks.Add(new SquadTraversalLink(
            id,
            source,
            kind,
            bidirectional,
            points,
            Mathf.Max(0.1f, cost * Mathf.Max(0.1f, costMultiplier)),
            forwardDirectives,
            reverseDirectives));
        return id;
    }

    private void RegisterSquadHescoTraversalLinks(Vector3 basePosition, float yaw, string source)
    {
        const float barrierHeight = 1.18f;
        const float approachDistance = 1.15f;
        var across = Vector3.Forward.Rotated(Vector3.Up, yaw).Normalized();
        var firstGround = basePosition - across * approachDistance + Vector3.Up * 0.12f;
        var secondGround = basePosition + across * approachDistance + Vector3.Up * 0.12f;
        var top = basePosition + Vector3.Up * (barrierHeight + 0.075f);

        RegisterSquadTraversalLink(
            source + ":vault_a",
            SquadTraversalKind.Vault,
            bidirectional: false,
            new[] { firstGround, top },
            costMultiplier: 1.08f);
        RegisterSquadTraversalLink(
            source + ":drop_b",
            SquadTraversalKind.Drop,
            bidirectional: false,
            new[] { top, secondGround },
            costMultiplier: 1.04f);
        RegisterSquadTraversalLink(
            source + ":vault_b",
            SquadTraversalKind.Vault,
            bidirectional: false,
            new[] { secondGround, top },
            costMultiplier: 1.08f);
        RegisterSquadTraversalLink(
            source + ":drop_a",
            SquadTraversalKind.Drop,
            bidirectional: false,
            new[] { top, firstGround },
            costMultiplier: 1.04f);
    }

    private bool TryPlanSquadLayeredRoute(
        CharacterBody3D navigator,
        Vector3 destination,
        int expansionCap,
        out SquadNavigationDirective[] directives,
        out float cost)
        => TryPlanSquadLayeredRoute(
            navigator,
            destination,
            new SquadNavSearchBudget(expansionCap, SquadNavDiagnosticPlanBudgetMilliseconds),
            out directives,
            out cost);

    private bool TryPlanSquadLayeredRoute(
        CharacterBody3D navigator,
        Vector3 destination,
        SquadNavSearchBudget budget,
        out SquadNavigationDirective[] directives,
        out float cost)
        => TryPlanSquadLayeredRoute(
            navigator,
            destination,
            budget,
            SquadTraversalCapabilities.All,
            out directives,
            out cost);

    private bool TryPlanSquadLayeredRoute(
        CharacterBody3D navigator,
        Vector3 destination,
        SquadNavSearchBudget budget,
        SquadTraversalCapabilities capabilities,
        out SquadNavigationDirective[] directives,
        out float cost)
    {
        directives = Array.Empty<SquadNavigationDirective>();
        cost = float.PositiveInfinity;
        if (_squadTraversalLinks.Count == 0 || !IsInstanceValid(navigator) || budget.IsExhausted)
        {
            return false;
        }

        var nodes = new List<SquadPortalNode>();
        var nodeLookup = new Dictionary<(int X, int Z, int Bucket), int>();
        var graphEdges = new List<SquadNavigationGraphEdge>();
        foreach (var link in _squadTraversalLinks)
        {
            var from = GetOrAddSquadPortalNode(link.ForwardPoints[0], nodes, nodeLookup);
            var to = GetOrAddSquadPortalNode(link.ForwardPoints[^1], nodes, nodeLookup);
            if (!SupportsSquadTraversal(capabilities, link.Kind))
            {
                continue;
            }
            var forwardId = link.Id * 2;
            if (!IsSquadTraversalEdgeDisabled(navigator, forwardId))
            {
                graphEdges.Add(new SquadNavigationGraphEdge(
                    from,
                    to,
                    link.Cost,
                    link.ForwardDirectives));
            }
            if (link.Bidirectional)
            {
                var reverseId = forwardId + 1;
                if (!IsSquadTraversalEdgeDisabled(navigator, reverseId))
                {
                    graphEdges.Add(new SquadNavigationGraphEdge(
                        to,
                        from,
                        link.Cost,
                        link.ReverseDirectives));
                }
            }
        }

        var portalNodeCount = nodes.Count;
        var startNode = nodes.Count;
        nodes.Add(new SquadPortalNode(
            navigator.GlobalPosition,
            SquadTraversalBucket(navigator.GlobalPosition)));
        var goalNode = nodes.Count;
        nodes.Add(new SquadPortalNode(destination, SquadTraversalBucket(destination)));
        var exclude = BuildSquadNavExclusions();
        using var excludeBacking = exclude.AsDisposable();

        AddSquadPortalWalkConnectors(
            portalNodeCount,
            nodes,
            graphEdges);
        if (budget.IsExhausted)
        {
            return false;
        }

        var exactStartPortal = FindExactSquadPortalNode(
            navigator.GlobalPosition,
            portalNodeCount,
            nodes);
        var exactGoalPortal = FindExactSquadPortalNode(
            destination,
            portalNodeCount,
            nodes);
        if (exactStartPortal >= 0 && exactGoalPortal >= 0)
        {
            graphEdges.Add(new SquadNavigationGraphEdge(
                startNode,
                exactStartPortal,
                0.0f,
                Array.Empty<SquadNavigationDirective>()));
            graphEdges.Add(new SquadNavigationGraphEdge(
                exactGoalPortal,
                goalNode,
                0.0f,
                Array.Empty<SquadNavigationDirective>()));
            var exactRoute = SquadNavigationGraph.FindShortestPath(
                nodes.Count,
                startNode,
                goalNode,
                graphEdges,
                out cost);
            if (exactRoute is { Length: > 0 })
            {
                directives = exactRoute;
                return true;
            }
        }

        var portalComponents = BuildSquadPortalComponentMap(portalNodeCount, graphEdges);
        var startComponents = FindSquadPortalEndpointComponents(
            navigator.GlobalPosition,
            portalNodeCount,
            nodes,
            portalComponents);
        var goalComponents = FindSquadPortalEndpointComponents(
            destination,
            portalNodeCount,
            nodes,
            portalComponents);
        var componentsReachableFromStart = BuildSquadPortalComponentReachability(
            portalNodeCount,
            graphEdges,
            portalComponents,
            startComponents,
            reverse: false);
        var componentsThatReachGoal = BuildSquadPortalComponentReachability(
            portalNodeCount,
            graphEdges,
            portalComponents,
            goalComponents,
            reverse: true);
        var attemptedStartComponents = new HashSet<int>();
        var attemptedGoalComponents = new HashSet<int>();
        // Keep the common case bounded, but continue through distinct strongly
        // connected portal components when nearby Hesco links do not reach the goal.
        while (true)
        {
            var attemptedBefore = attemptedStartComponents.Count + attemptedGoalComponents.Count;
            AddSquadPortalConnectors(
                navigator.GlobalPosition,
                startNode,
                connectFromVirtual: true,
                portalNodeCount,
                nodes,
                portalComponents,
                attemptedStartComponents,
                graphEdges,
                budget,
                exclude,
                componentsThatReachGoal);
            if (budget.IsExhausted)
            {
                return false;
            }
            AddSquadPortalConnectors(
                destination,
                goalNode,
                connectFromVirtual: false,
                portalNodeCount,
                nodes,
                portalComponents,
                attemptedGoalComponents,
                graphEdges,
                budget,
                exclude,
                componentsReachableFromStart);
            if (budget.IsExhausted)
            {
                return false;
            }

            var route = SquadNavigationGraph.FindShortestPath(
                nodes.Count,
                startNode,
                goalNode,
                graphEdges,
                out cost);
            if (route is { Length: > 0 })
            {
                directives = route;
                return true;
            }
            if (attemptedStartComponents.Count + attemptedGoalComponents.Count == attemptedBefore)
            {
                return false;
            }
        }
    }

    private int GetOrAddSquadPortalNode(
        Vector3 position,
        List<SquadPortalNode> nodes,
        Dictionary<(int X, int Z, int Bucket), int> lookup)
    {
        // Only authored endpoints that are effectively identical may share a graph
        // node. Nav-cell bucketing used to merge opposite sides of a thin wall into
        // one zero-cost portal.
        var key = (
            Mathf.RoundToInt(position.X * 10.0f),
            Mathf.RoundToInt(position.Z * 10.0f),
            Mathf.RoundToInt(position.Y * 10.0f));
        if (lookup.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var index = nodes.Count;
        nodes.Add(new SquadPortalNode(position, SquadTraversalBucket(position)));
        lookup[key] = index;
        return index;
    }

    private void AddSquadPortalConnectors(
        Vector3 endpoint,
        int virtualNode,
        bool connectFromVirtual,
        int portalNodeCount,
        IReadOnlyList<SquadPortalNode> nodes,
        IReadOnlyList<int> portalComponents,
        ISet<int> attemptedComponents,
        List<SquadNavigationGraphEdge> graphEdges,
        SquadNavSearchBudget budget,
        Godot.Collections.Array<Rid> exclude,
        ISet<int>? eligibleComponents)
    {
        var bucket = SquadTraversalBucket(endpoint);
        var ranked = new PriorityQueue<
            (int Node, float DistanceSquared),
            (float DistanceSquared, int Node)>();
        for (var node = 0; node < portalNodeCount; node++)
        {
            if (budget.IsExhausted)
            {
                return;
            }
            if (nodes[node].Bucket != bucket)
            {
                continue;
            }
            var offset = nodes[node].Position - endpoint;
            offset.Y = 0.0f;
            var distanceSquared = offset.LengthSquared();
            ranked.Enqueue((node, distanceSquared), (distanceSquared, node));
        }

        var candidatesByComponent = new Dictionary<int, List<(int Node, float DistanceSquared)>>();
        var componentOrder = new List<int>();
        while (ranked.TryDequeue(out var candidate, out _))
        {
            if (budget.IsExhausted)
            {
                return;
            }
            var component = portalComponents[candidate.Node];
            if (attemptedComponents.Contains(component)
                || eligibleComponents is not null && !eligibleComponents.Contains(component))
            {
                continue;
            }
            if (!candidatesByComponent.TryGetValue(component, out var componentCandidates))
            {
                componentCandidates = new List<(int Node, float DistanceSquared)>();
                candidatesByComponent[component] = componentCandidates;
                componentOrder.Add(component);
            }
            componentCandidates.Add(candidate);
        }

        var componentsAttemptedThisPass = 0;
        foreach (var component in componentOrder)
        {
            if (budget.IsExhausted)
            {
                return;
            }
            attemptedComponents.Add(component);
            componentsAttemptedThisPass++;
            foreach (var candidate in candidatesByComponent[component])
            {
                if (budget.IsExhausted)
                {
                    return;
                }
                var from = connectFromVirtual ? endpoint : nodes[candidate.Node].Position;
                var to = connectFromVirtual ? nodes[candidate.Node].Position : endpoint;
                if (!TryBuildSquadGridSegment(
                        from,
                        to,
                        bucket,
                        budget,
                        exclude,
                        out var waypoints,
                        out var segmentCost))
                {
                    continue;
                }
                graphEdges.Add(new SquadNavigationGraphEdge(
                    connectFromVirtual ? virtualNode : candidate.Node,
                    connectFromVirtual ? candidate.Node : virtualNode,
                    segmentCost,
                    BuildSquadWalkDirectives(waypoints)));
                // An exact endpoint hit is already the authored portal. Once its
                // component is eligible for the opposite endpoint, scanning more
                // same-floor portals only burns the bounded search budget.
                if (candidate.DistanceSquared <= 0.0001f)
                {
                    return;
                }
                break;
            }
            if (componentsAttemptedThisPass >= SquadTraversalConnectorComponentBatchSize)
            {
                break;
            }
        }
    }

    private static HashSet<int> FindSquadPortalEndpointComponents(
        Vector3 endpoint,
        int portalNodeCount,
        IReadOnlyList<SquadPortalNode> nodes,
        IReadOnlyList<int> portalComponents)
    {
        var components = new HashSet<int>();
        var bucket = SquadTraversalBucket(endpoint);
        for (var node = 0; node < portalNodeCount; node++)
        {
            if (nodes[node].Bucket == bucket)
            {
                components.Add(portalComponents[node]);
            }
        }
        return components;
    }

    private static HashSet<int> BuildSquadPortalComponentReachability(
        int portalNodeCount,
        IReadOnlyList<SquadNavigationGraphEdge> graphEdges,
        IReadOnlyList<int> portalComponents,
        IReadOnlySet<int> seedComponents,
        bool reverse)
    {
        var componentCount = 0;
        for (var node = 0; node < portalNodeCount; node++)
        {
            componentCount = Math.Max(componentCount, portalComponents[node] + 1);
        }
        var adjacency = new List<int>[componentCount];
        for (var component = 0; component < componentCount; component++)
        {
            adjacency[component] = new List<int>();
        }
        foreach (var edge in graphEdges)
        {
            if (edge.From < 0 || edge.From >= portalNodeCount
                || edge.To < 0 || edge.To >= portalNodeCount)
            {
                continue;
            }
            var from = portalComponents[edge.From];
            var to = portalComponents[edge.To];
            if (from == to)
            {
                continue;
            }
            if (reverse)
            {
                (from, to) = (to, from);
            }
            if (!adjacency[from].Contains(to))
            {
                adjacency[from].Add(to);
            }
        }

        var reachable = new HashSet<int>();
        var pending = new Stack<int>();
        foreach (var seed in seedComponents)
        {
            if (seed >= 0 && seed < componentCount && reachable.Add(seed))
            {
                pending.Push(seed);
            }
        }
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var next in adjacency[current])
            {
                if (reachable.Add(next))
                {
                    pending.Push(next);
                }
            }
        }
        return reachable;
    }

    private static SquadNavigationDirective[] BuildSquadWalkDirectives(IReadOnlyList<Vector3> points)
    {
        var directives = new SquadNavigationDirective[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            directives[index] = SquadNavigationDirective.Walk(points[index]);
        }
        return directives;
    }

    private static SquadNavigationDirective[] BuildSquadLinkDirectives(
        int linkId,
        SquadTraversalKind linkKind,
        IReadOnlyList<Vector3> points,
        bool forward)
    {
        var count = points.Count;
        var directedEdgeId = linkId * 2 + (forward ? 0 : 1);
        var directives = new SquadNavigationDirective[count];
        for (var index = 0; index < count; index++)
        {
            var pointIndex = forward ? index : count - index - 1;
            var kind = index == 0 && linkKind is SquadTraversalKind.Vault or SquadTraversalKind.Drop
                ? SquadTraversalKind.Walk
                : linkKind;
            directives[index] = new SquadNavigationDirective(
                points[pointIndex],
                kind,
                directedEdgeId,
                true);
        }
        return directives;
    }

    private static int FindExactSquadPortalNode(
        Vector3 endpoint,
        int portalNodeCount,
        IReadOnlyList<SquadPortalNode> nodes)
    {
        for (var node = 0; node < portalNodeCount; node++)
        {
            if (nodes[node].Position.DistanceSquaredTo(endpoint) <= 0.0001f)
            {
                return node;
            }
        }
        return -1;
    }

    private static int SquadTraversalBucket(Vector3 position)
        => Mathf.RoundToInt(position.Y / SquadNavCellSize);

    private static bool SupportsSquadTraversal(
        SquadTraversalCapabilities capabilities,
        SquadTraversalKind kind)
        => (capabilities & (SquadTraversalCapabilities)(1 << (int)kind)) != 0;

    private bool IsSquadTraversalEdgeDisabled(GodotObject navigator, int directedEdgeId)
    {
        var key = (navigator.GetInstanceId(), directedEdgeId);
        if (!_squadTraversalFailures.TryGetValue(key, out var failure))
        {
            return false;
        }
        if (Time.GetTicksMsec() < failure.ExpiresMsec)
        {
            return true;
        }
        _squadTraversalFailures.Remove(key);
        return false;
    }

    private bool PreserveSquadTraversalAfterStall(SquadMate mate, int directedEdgeId)
    {
        var key = (mate.GetInstanceId(), directedEdgeId);
        var attempts = _squadTraversalRecoveryAttempts.TryGetValue(key, out var previous)
            ? previous + 1
            : 1;
        _squadTraversalRecoveryAttempts[key] = attempts;
        return attempts <= 1;
    }

    private void ClearSquadTraversalRecoveryAttempt(SquadMate mate, int directedEdgeId)
    {
        if (directedEdgeId >= 0)
        {
            _squadTraversalRecoveryAttempts.Remove((mate.GetInstanceId(), directedEdgeId));
        }
    }

    internal void ReportSquadTraversalFailure(SquadMate mate, int directedEdgeId)
    {
        if (!IsInstanceValid(mate) || directedEdgeId < 0)
        {
            return;
        }
        var key = (mate.GetInstanceId(), directedEdgeId);
        _squadTraversalRecoveryAttempts.Remove(key);
        var failures = _squadTraversalFailures.TryGetValue(key, out var previous)
            ? previous.Failures + 1
            : 1;
        var shift = Mathf.Clamp(failures - 1, 0, 3);
        var delay = Math.Min(
            SquadTraversalFailureBaseMilliseconds << shift,
            SquadTraversalFailureMaximumMilliseconds);
        _squadTraversalFailures[key] = (Time.GetTicksMsec() + delay, failures);
        _squadTrailPaths.Remove(mate.GetInstanceId());
        _squadGridPaths.Remove(mate.GetInstanceId());
    }
}
