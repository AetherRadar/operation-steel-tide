using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Classifies authored sloped path runs so the visibility graph cannot enter a ramp from
/// its side. This is pure route data and owns no scene or physics state.
/// </summary>
internal sealed class DemolitionElevationTransitions
{
    private const float MinimumElevationDelta = 0.08f;
    private const float BoundaryMatchDistanceSquared = 0.12f * 0.12f;
    private const float RampProjectionHorizontalTolerance = 1.25f;
    private const float RampProjectionVerticalTolerance = 0.72f;
    private const float RampProjectionEndpointMargin = 0.04f;
    private const float TraversalBodyRadius = 0.38f;
    private const float TraversalBoxMatchMargin = 0.55f;
    private const float TraversalLengthEndpointMargin = 0.12f;

    private readonly IReadOnlyList<Vector3> _nodes;
    private readonly HashSet<int> _boundaryNodes;
    private readonly HashSet<int> _interiorNodes;
    private readonly DemolitionElevationSegment[] _segments;

    private DemolitionElevationTransitions(
        IReadOnlyList<Vector3> nodes,
        HashSet<int> boundaryNodes,
        HashSet<int> interiorNodes,
        DemolitionElevationSegment[] segments)
    {
        _nodes = nodes;
        _boundaryNodes = boundaryNodes;
        _interiorNodes = interiorNodes;
        _segments = segments;
    }

    public static DemolitionElevationTransitions Create(
        IReadOnlyList<Vector3> nodes,
        IReadOnlyList<IReadOnlyList<int>> auxiliaryPaths,
        IReadOnlyList<DemolitionArenaBox> traversalBoxes)
    {
        var boundaryNodes = new HashSet<int>();
        var interiorNodes = new HashSet<int>();
        var segments = new List<DemolitionElevationSegment>();
        foreach (var path in auxiliaryPaths)
        {
            var edge = 0;
            while (edge + 1 < path.Count)
            {
                var firstDelta = nodes[path[edge + 1]].Y - nodes[path[edge]].Y;
                if (Mathf.Abs(firstDelta) <= MinimumElevationDelta)
                {
                    edge++;
                    continue;
                }

                var runStart = edge;
                var runDirection = Mathf.Sign(firstDelta);
                var runEnd = edge;
                while (runEnd + 2 < path.Count)
                {
                    var nextDelta = nodes[path[runEnd + 2]].Y - nodes[path[runEnd + 1]].Y;
                    if (Mathf.Abs(nextDelta) <= MinimumElevationDelta
                        || Mathf.Sign(nextDelta) != runDirection)
                    {
                        break;
                    }
                    runEnd++;
                }

                boundaryNodes.Add(path[runStart]);
                boundaryNodes.Add(path[runEnd + 1]);
                for (var node = runStart + 1; node <= runEnd; node++)
                {
                    interiorNodes.Add(path[node]);
                }
                for (var segment = runStart; segment <= runEnd; segment++)
                {
                    var fromNode = path[segment];
                    var toNode = path[segment + 1];
                    segments.Add(new DemolitionElevationSegment(
                        fromNode,
                        toNode,
                        nodes[fromNode],
                        nodes[toNode],
                        FindTraversalBox(
                            nodes[path[runStart]],
                            nodes[path[runEnd + 1]],
                            traversalBoxes)));
                }
                edge = runEnd + 1;
            }
        }

        // If authored data ever shares a node between a ramp interior and another ramp
        // boundary, the safer interpretation is authored-only.
        boundaryNodes.ExceptWith(interiorNodes);
        return new DemolitionElevationTransitions(
            nodes,
            boundaryNodes,
            interiorNodes,
            segments.ToArray());
    }

    public bool IsInteriorNode(int nodeIndex) => _interiorNodes.Contains(nodeIndex);

    public bool IsBoundaryPoint(Vector3 point)
    {
        foreach (var nodeIndex in _boundaryNodes)
        {
            if (_nodes[nodeIndex].DistanceSquaredTo(point) <= BoundaryMatchDistanceSquared)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsNearTransition(Vector3 point)
    {
        foreach (var segment in _segments)
        {
            if (TryProjectionDistanceSquared(point, segment, out _))
            {
                return true;
            }
        }
        return false;
    }

    public bool SharesSegment(Vector3 left, Vector3 right)
    {
        foreach (var segment in _segments)
        {
            if (TryProjectionDistanceSquared(left, segment, out _)
                && TryProjectionDistanceSquared(right, segment, out _))
            {
                return true;
            }
        }
        return false;
    }

    public bool TryFindClosestSegment(
        Vector3 point,
        out DemolitionElevationSegment closest)
    {
        closest = default;
        var found = false;
        var closestDistanceSquared = float.PositiveInfinity;
        foreach (var segment in _segments)
        {
            if (!TryProjectionDistanceSquared(point, segment, out var distanceSquared)
                || distanceSquared >= closestDistanceSquared)
            {
                continue;
            }
            closest = segment;
            closestDistanceSquared = distanceSquared;
            found = true;
        }
        return found;
    }

    private static bool TryProjectionDistanceSquared(
        Vector3 point,
        DemolitionElevationSegment segment,
        out float distanceSquared)
    {
        var direction = segment.To - segment.From;
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            distanceSquared = float.PositiveInfinity;
            return false;
        }

        var rawFactor = (point - segment.From).Dot(direction) / lengthSquared;
        if (rawFactor < -RampProjectionEndpointMargin
            || rawFactor > 1.0f + RampProjectionEndpointMargin)
        {
            distanceSquared = float.PositiveInfinity;
            return false;
        }
        var factor = Mathf.Clamp(rawFactor, 0.0f, 1.0f);
        var projection = segment.From + direction * factor;
        if (segment.TraversalBox is DemolitionArenaBox traversalBox
            && !IsInsideTraversalFootprint(point, traversalBox))
        {
            distanceSquared = float.PositiveInfinity;
            return false;
        }
        var horizontal = new Vector2(
            point.X - projection.X,
            point.Z - projection.Z).Length();
        var vertical = Mathf.Abs(point.Y - projection.Y);
        distanceSquared = horizontal * horizontal + vertical * vertical;
        return horizontal <= RampProjectionHorizontalTolerance
            && vertical <= RampProjectionVerticalTolerance;
    }

    private static DemolitionArenaBox? FindTraversalBox(
        Vector3 runStart,
        Vector3 runEnd,
        IReadOnlyList<DemolitionArenaBox> traversalBoxes)
    {
        var midpoint = (runStart + runEnd) * 0.5f;
        DemolitionArenaBox? closest = null;
        var closestDistanceSquared = float.PositiveInfinity;
        foreach (var box in traversalBoxes)
        {
            if (!box.Name.Contains("Ramp", StringComparison.Ordinal))
            {
                continue;
            }
            var inverse = new Basis(Quaternion.FromEuler(box.Rotation)).Inverse();
            var local = inverse * (midpoint - box.Center);
            var half = box.Size * 0.5f;
            if (Mathf.Abs(local.X) > half.X + TraversalBoxMatchMargin
                || Mathf.Abs(local.Y) > half.Y + TraversalBoxMatchMargin
                || Mathf.Abs(local.Z) > half.Z + TraversalBoxMatchMargin)
            {
                continue;
            }
            var distanceSquared = local.LengthSquared();
            if (distanceSquared < closestDistanceSquared)
            {
                closest = box;
                closestDistanceSquared = distanceSquared;
            }
        }
        return closest;
    }

    private static bool IsInsideTraversalFootprint(
        Vector3 point,
        DemolitionArenaBox box)
    {
        var inverse = new Basis(Quaternion.FromEuler(box.Rotation)).Inverse();
        var local = inverse * (point - box.Center);
        var half = box.Size * 0.5f;
        var xLimit = half.X + (box.Size.X <= box.Size.Z
            ? -TraversalBodyRadius
            : TraversalLengthEndpointMargin);
        var zLimit = half.Z + (box.Size.Z < box.Size.X
            ? -TraversalBodyRadius
            : TraversalLengthEndpointMargin);
        return Mathf.Abs(local.X) <= xLimit
            && Mathf.Abs(local.Z) <= zLimit
            && Mathf.Abs(local.Y) <= half.Y + RampProjectionVerticalTolerance;
    }
}

internal readonly record struct DemolitionElevationSegment(
    int FromNode,
    int ToNode,
    Vector3 From,
    Vector3 To,
    DemolitionArenaBox? TraversalBox);
