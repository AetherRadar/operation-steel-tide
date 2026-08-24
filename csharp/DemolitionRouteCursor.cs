using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>Mutable progress state for one operator following an immutable route result.</summary>
public sealed class DemolitionRouteCursor
{
    private const float DestinationMatchDistanceSquared = 0.2f * 0.2f;
    private const float ProgressSampleSeconds = 0.8f;
    private const float MinimumProgress = 0.3f;
    private const float UnreachableRetrySeconds = 0.75f;

    private Vector3[] _waypoints = Array.Empty<Vector3>();
    private Vector3 _progressOrigin;
    private float _progressTimer;
    private float _unreachableRetryTimer;

    public string RouteKey { get; private set; } = string.Empty;
    public Vector3 Destination { get; private set; }
    public int WaypointIndex { get; private set; }
    public int ReplanCount { get; private set; }
    public bool ReachesDestination { get; private set; }
    public IReadOnlyList<Vector3> Waypoints => _waypoints;
    public bool Complete => WaypointIndex >= _waypoints.Length;
    public Vector3 CurrentWaypoint => Complete ? Destination : _waypoints[WaypointIndex];

    internal DemolitionRouteCursor CloneForDiagnostics()
    {
        return new DemolitionRouteCursor
        {
            RouteKey = RouteKey,
            Destination = Destination,
            WaypointIndex = WaypointIndex,
            ReplanCount = ReplanCount,
            ReachesDestination = ReachesDestination,
            _waypoints = CopyWaypoints(_waypoints),
            _progressOrigin = _progressOrigin,
            _progressTimer = _progressTimer,
            _unreachableRetryTimer = _unreachableRetryTimer
        };
    }

    public bool Matches(string routeKey, Vector3 destination)
        => string.Equals(RouteKey, routeKey, StringComparison.Ordinal)
            && HorizontalDistanceSquared(Destination, destination) <= DestinationMatchDistanceSquared;

    public bool MatchesWithin(string routeKey, Vector3 destination, float tolerance)
        => string.Equals(RouteKey, routeKey, StringComparison.Ordinal)
            && HorizontalDistanceSquared(Destination, destination) <= tolerance * tolerance;

    public void Reset(
        string routeKey,
        Vector3 start,
        Vector3 destination,
        DemolitionRouteResult route,
        bool countAsReplan)
    {
        RouteKey = routeKey;
        Destination = destination;
        _waypoints = CopyWaypoints(route.Waypoints);
        WaypointIndex = 0;
        ReachesDestination = route.ReachesDestination;
        _progressOrigin = start;
        _progressTimer = 0.0f;
        _unreachableRetryTimer = 0.0f;
        ReplanCount = countAsReplan ? ReplanCount + 1 : 0;
    }

    public bool Advance(Vector3 position, float intermediateTolerance, float finalTolerance)
    {
        while (!Complete)
        {
            var tolerance = WaypointIndex == _waypoints.Length - 1
                ? finalTolerance
                : intermediateTolerance;
            if (HorizontalDistanceSquared(position, CurrentWaypoint) > tolerance * tolerance)
            {
                break;
            }
            WaypointIndex++;
            _progressOrigin = position;
            _progressTimer = 0.0f;
        }
        return Complete;
    }

    public bool TrackMovement(Vector3 position, float delta, bool movementRequested)
    {
        if (!movementRequested)
        {
            _progressOrigin = position;
            _progressTimer = 0.0f;
            return false;
        }

        _progressTimer += delta;
        if (_progressTimer < ProgressSampleSeconds)
        {
            return false;
        }
        var stalled = HorizontalDistanceSquared(_progressOrigin, position) < MinimumProgress * MinimumProgress;
        _progressOrigin = position;
        _progressTimer = 0.0f;
        return stalled;
    }

    public bool ShouldRetryUnreachable(float delta)
    {
        if (ReachesDestination)
        {
            _unreachableRetryTimer = 0.0f;
            return false;
        }

        _unreachableRetryTimer += delta;
        if (_unreachableRetryTimer < UnreachableRetrySeconds)
        {
            return false;
        }
        _unreachableRetryTimer = 0.0f;
        return true;
    }

    private static Vector3[] CopyWaypoints(IReadOnlyList<Vector3> source)
    {
        var copy = new Vector3[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            copy[index] = source[index];
        }
        return copy;
    }

    private static float HorizontalDistanceSquared(Vector3 left, Vector3 right)
    {
        var dx = left.X - right.X;
        var dz = left.Z - right.Z;
        return dx * dx + dz * dz;
    }
}
