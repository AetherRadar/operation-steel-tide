using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct SquadLadderPortal(
        int From,
        int To,
        bool Bidirectional,
        Vector3 Outward);

    private static HashSet<int> BuildSquadTerminalLadderComponents(
        IReadOnlyList<int> portalComponents,
        IReadOnlySet<int> ordinaryPortalNodes,
        IReadOnlySet<int> ladderPortalNodes)
    {
        var ordinaryComponents = new HashSet<int>();
        foreach (var node in ordinaryPortalNodes)
        {
            ordinaryComponents.Add(portalComponents[node]);
        }

        var terminalComponents = new HashSet<int>();
        foreach (var node in ladderPortalNodes)
        {
            var component = portalComponents[node];
            if (!ordinaryComponents.Contains(component))
            {
                terminalComponents.Add(component);
            }
        }
        return terminalComponents;
    }

    private static HashSet<int> SelectSquadLadderTerminalComponents(
        Vector3 start,
        Vector3 destination,
        IReadOnlyList<SquadPortalNode> nodes,
        IReadOnlyList<int> portalComponents,
        IReadOnlySet<int> terminalLadderComponents,
        IReadOnlyList<SquadLadderPortal> ladderPortals)
    {
        const int bucketTolerance = 1;
        var startBucket = SquadTraversalBucket(start);
        var goalBucket = SquadTraversalBucket(destination);
        var ranked = new PriorityQueue<
            SquadLadderPortal,
            (float Score, int From, int To)>();
        foreach (var portal in ladderPortals)
        {
            var from = nodes[portal.From];
            var to = nodes[portal.To];
            var forward = Math.Abs(from.Bucket - startBucket) <= bucketTolerance
                && Math.Abs(to.Bucket - goalBucket) <= bucketTolerance;
            var reverse = portal.Bidirectional
                && Math.Abs(to.Bucket - startBucket) <= bucketTolerance
                && Math.Abs(from.Bucket - goalBucket) <= bucketTolerance;
            if (!forward && !reverse)
            {
                continue;
            }

            var entry = forward ? from.Position : to.Position;
            var exit = forward ? to.Position : from.Position;
            var elevatedEntry = entry.Y >= exit.Y;
            var constrainedDistance = elevatedEntry
                ? HorizontalDistanceSquared(start, entry)
                : HorizontalDistanceSquared(destination, exit);
            var openFloorDistance = elevatedEntry
                ? HorizontalDistanceSquared(destination, exit)
                : HorizontalDistanceSquared(start, entry);
            var score = constrainedDistance * 16.0f + openFloorDistance * 0.02f;
            ranked.Enqueue(portal, (score, portal.From, portal.To));
        }

        var selected = new HashSet<int>();
        var selectedPortals = 0;
        while (selectedPortals < SquadTraversalConnectorComponentBatchSize
            && ranked.TryDequeue(out var portal, out _))
        {
            var fromComponent = portalComponents[portal.From];
            var toComponent = portalComponents[portal.To];
            var added = false;
            if (terminalLadderComponents.Contains(fromComponent))
            {
                added |= selected.Add(fromComponent);
            }
            if (terminalLadderComponents.Contains(toComponent))
            {
                added |= selected.Add(toComponent);
            }
            if (added)
            {
                selectedPortals++;
            }
        }
        return selected;
    }

    private static void AddSquadLadderTerminalEndpointConnectors(
        Vector3 start,
        Vector3 destination,
        int startNode,
        int goalNode,
        IReadOnlyList<SquadPortalNode> nodes,
        IReadOnlyList<int> portalComponents,
        IReadOnlySet<int> terminalLadderComponents,
        IReadOnlySet<int> enabledLadderComponents,
        IReadOnlyList<SquadLadderPortal> ladderPortals,
        List<SquadNavigationGraphEdge> graphEdges)
    {
        const int bucketTolerance = 1;
        const float directAttachDistanceSquared = 2.0f * 2.0f;
        var startBucket = SquadTraversalBucket(start);
        var goalBucket = SquadTraversalBucket(destination);
        foreach (var portal in ladderPortals)
        {
            AddSquadLadderTerminalEndpointConnector(
                start,
                startBucket,
                startNode,
                connectFromVirtual: true,
                startAtTop: false,
                portal.From,
                portal.Outward,
                nodes,
                portalComponents,
                terminalLadderComponents,
                enabledLadderComponents,
                graphEdges,
                bucketTolerance,
                directAttachDistanceSquared);
            AddSquadLadderTerminalEndpointConnector(
                destination,
                goalBucket,
                goalNode,
                connectFromVirtual: false,
                startAtTop: true,
                portal.To,
                portal.Outward,
                nodes,
                portalComponents,
                terminalLadderComponents,
                enabledLadderComponents,
                graphEdges,
                bucketTolerance,
                directAttachDistanceSquared);
            if (!portal.Bidirectional)
            {
                continue;
            }
            AddSquadLadderTerminalEndpointConnector(
                start,
                startBucket,
                startNode,
                connectFromVirtual: true,
                startAtTop: true,
                portal.To,
                portal.Outward,
                nodes,
                portalComponents,
                terminalLadderComponents,
                enabledLadderComponents,
                graphEdges,
                bucketTolerance,
                directAttachDistanceSquared);
            AddSquadLadderTerminalEndpointConnector(
                destination,
                goalBucket,
                goalNode,
                connectFromVirtual: false,
                startAtTop: false,
                portal.From,
                portal.Outward,
                nodes,
                portalComponents,
                terminalLadderComponents,
                enabledLadderComponents,
                graphEdges,
                bucketTolerance,
                directAttachDistanceSquared);
        }
    }

    private static void AddSquadLadderTerminalEndpointConnector(
        Vector3 endpoint,
        int endpointBucket,
        int virtualNode,
        bool connectFromVirtual,
        bool startAtTop,
        int portalNode,
        Vector3 outward,
        IReadOnlyList<SquadPortalNode> nodes,
        IReadOnlyList<int> portalComponents,
        IReadOnlySet<int> terminalLadderComponents,
        IReadOnlySet<int> enabledLadderComponents,
        List<SquadNavigationGraphEdge> graphEdges,
        int bucketTolerance,
        float directAttachDistanceSquared)
    {
        var component = portalComponents[portalNode];
        var portal = nodes[portalNode];
        var offset = endpoint - portal.Position;
        offset.Y = 0.0f;
        var approachSide = offset.Dot(outward);
        if (!terminalLadderComponents.Contains(component)
            || !enabledLadderComponents.Contains(component)
            || Math.Abs(portal.Bucket - endpointBucket) > bucketTolerance
            || portal.Position.DistanceSquaredTo(endpoint) > directAttachDistanceSquared
            || !startAtTop && approachSide < -0.65f
            || startAtTop && approachSide > 0.85f)
        {
            return;
        }

        var distance = portal.Position.DistanceTo(endpoint);
        var directives = distance <= 0.01f
            ? Array.Empty<SquadNavigationDirective>()
            : new[]
            {
                SquadNavigationDirective.Walk(
                    connectFromVirtual ? portal.Position : endpoint)
            };
        graphEdges.Add(new SquadNavigationGraphEdge(
            connectFromVirtual ? virtualNode : portalNode,
            connectFromVirtual ? portalNode : virtualNode,
            distance,
            directives));
    }

    private static float HorizontalDistanceSquared(Vector3 first, Vector3 second)
    {
        var offset = second - first;
        offset.Y = 0.0f;
        return offset.LengthSquared();
    }
}
