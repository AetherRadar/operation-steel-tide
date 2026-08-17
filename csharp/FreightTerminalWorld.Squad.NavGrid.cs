using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float SquadNavCellSize = 0.9f;
    private const float SquadNavBandDrop = 0.62f;
    private const float SquadNavBandRise = 0.62f;
    private const float SquadNavStepHeight = 0.55f;
    private const float SquadNavSameBandHeight = 1.6f;
    private const float SquadNavGoalHeight = 1.25f;
    private const ulong SquadNavEdgeTtlMilliseconds = 3000;
    // Formation following already has the leader trail as its cross-floor route.
    // Keep the geometric fallback deliberately small so a first follow frame cannot
    // monopolize the physics thread with a second full navigation search.
    private const int SquadNavFollowExpansionCap = 192;
    private const float SquadNavCorridorSampleSpacing = 1.8f;
    private const ulong SquadNavNormalPlanIntervalMilliseconds = 90;
    private const int SquadNavEstimateExpansionCap = 2500;
    private const int SquadNavCellCacheResetCapacity = 60000;
    private const int SquadNavTrailHandoffCandidates = 3;
    private const float SquadNavTrailHandoffRange = 26.0f;
    private const float SquadNavRetryDestinationDistance = 3.0f;
    private const ulong SquadNavRetryBaseMilliseconds = 900;
    private const ulong SquadNavRetryMaximumMilliseconds = 8000;
    private const float SquadNavSupportFootprintRadius = 0.26f;
    private const float SquadNavClearanceRadius = 0.37f;
    private const float SquadNavClearanceHeight = 1.76f;
    private const float SquadNavClearanceCenterHeight = 0.88f;
    private const float SquadNavClearanceFloorLift = 0.03f;

    // Covers the full playable ground plane (map 340x320 centered near z=-60).
    private static readonly Vector2 SquadNavGridOrigin = new(-175.0f, -225.0f);
    private static readonly SquadNavGrid SquadNavPlanner = new(400, 367, inflationCells: 44);
    private static readonly Vector2[] SquadNavSupportFootprintOffsets =
    {
        Vector2.Zero,
        new Vector2(SquadNavSupportFootprintRadius, 0.0f),
        new Vector2(-SquadNavSupportFootprintRadius, 0.0f),
        new Vector2(0.0f, SquadNavSupportFootprintRadius),
        new Vector2(0.0f, -SquadNavSupportFootprintRadius)
    };

    private readonly Dictionary<long, float> _squadNavCellSupport = new();
    private readonly Dictionary<(long Edge, int Bucket), (bool Clear, ulong ExpiresMsec)> _squadNavEdgeCache =
        new();
    private readonly Dictionary<ulong, SquadGridPathState> _squadGridPaths = new();
    private readonly CapsuleShape3D _squadNavClearanceShape = new()
    {
        Radius = SquadNavClearanceRadius,
        Height = SquadNavClearanceHeight
    };
    private ulong _squadNavNextNormalPlanMilliseconds;
    private int _leaderRescueGridPlans;
    private bool _leaderRescueUsedGrid;

    private bool LeaderRescueUsedGridForDiagnostics => _leaderRescueUsedGrid;
    private int LeaderRescueGridPlansForDiagnostics => _leaderRescueGridPlans;

    private sealed class SquadGridPathState
    {
        public SquadNavigationDirective[] Directives = Array.Empty<SquadNavigationDirective>();
        public int Cursor;
        public bool Emergency;
        public Vector3 Destination;
        public ulong NextPlanMilliseconds;
        public int FailedPlanAttempts;
        public bool TrailHandoff;
        public Vector3 HandoffPoint;
    }

    /// <summary>
    /// Ground-plane corridor planner used when the leader trail cannot produce a
    /// route (mate off-trail, stale trail, or trail exhausted). Falls back to a
    /// grid route toward the nearest reachable trail entry point for multi-floor
    /// destinations so trail following can resume with real stair connectivity.
    /// </summary>
    private bool TryResolveSquadGridNavigation(
        SquadMate mate,
        Vector3 destination,
        bool emergency,
        out SquadNavigationDirective directive)
    {
        directive = SquadNavigationDirective.Walk(destination);
        if (!IsInstanceValid(mate))
        {
            return false;
        }
        var now = Time.GetTicksMsec();
        var id = mate.GetInstanceId();
        _squadGridPaths.TryGetValue(id, out var state);
        var failedPlanAttempts = 0;

        if (state is not null && state.Directives.Length == 0)
        {
            // A failed full search is stable for static interior geometry. Back off
            // repeated probes unless the requested destination meaningfully changes.
            var retryDistance = emergency ? 1.0f : SquadNavRetryDestinationDistance;
            var requestChanged = state.Emergency != emergency
                || state.Destination.DistanceSquaredTo(destination) > retryDistance * retryDistance;
            if (!requestChanged && now < state.NextPlanMilliseconds)
            {
                return false;
            }
            failedPlanAttempts = requestChanged ? 0 : state.FailedPlanAttempts;
            _squadGridPaths.Remove(id);
            state = null;
        }

        if (state is not null)
        {
            // Every adjacent path edge was physics-validated during A*. Rechecking
            // here invalidates valid corner routes and can cause full replan storms.
            var stale = state.Emergency != emergency
                || emergency && state.Destination.DistanceSquaredTo(destination) > 1.0f
                || state.Cursor >= state.Directives.Length;
            if (stale)
            {
                _squadGridPaths.Remove(id);
                state = null;
            }
        }

        if (state is not null)
        {
            AdvanceSquadGridCursor(mate, state);
            if (state.Cursor < state.Directives.Length)
            {
                directive = state.Directives[state.Cursor];
                return true;
            }
            if (state.TrailHandoff)
            {
                // Standing at the trail entry: hand control back to the trail planner.
                _squadTrailPaths.Remove(id);
            }
            _squadGridPaths.Remove(id);
            return false;
        }

        if (!emergency && now < _squadNavNextNormalPlanMilliseconds)
        {
            _squadGridPaths[id] = new SquadGridPathState
            {
                Emergency = false,
                Destination = destination,
                FailedPlanAttempts = failedPlanAttempts,
                NextPlanMilliseconds = _squadNavNextNormalPlanMilliseconds
            };
            return false;
        }
        if (!emergency)
        {
            _squadNavNextNormalPlanMilliseconds = now + SquadNavNormalPlanIntervalMilliseconds;
        }

        if (!TryPlanSquadGridRoute(mate, destination, emergency, out var route))
        {
            var failures = failedPlanAttempts + 1;
            _squadGridPaths[id] = new SquadGridPathState
            {
                Emergency = emergency,
                Destination = destination,
                FailedPlanAttempts = failures,
                NextPlanMilliseconds = now + SquadNavRetryDelayMilliseconds(failures, id)
            };
            return false;
        }
        if (emergency)
        {
            _leaderRescueGridPlans++;
            _leaderRescueUsedGrid = true;
        }
        _squadTrailPaths.Remove(id);
        _squadGridPaths[id] = route;
        AdvanceSquadGridCursor(mate, route);
        if (route.Cursor >= route.Directives.Length)
        {
            _squadGridPaths.Remove(id);
            return false;
        }
        directive = route.Directives[route.Cursor];
        return true;
    }

    private static ulong SquadNavRetryDelayMilliseconds(int failures, ulong instanceId)
    {
        var shift = Mathf.Clamp(failures - 1, 0, 3);
        var delay = Math.Min(
            SquadNavRetryBaseMilliseconds << shift,
            SquadNavRetryMaximumMilliseconds);
        return delay + instanceId % 251;
    }

    private bool TryPlanSquadGridRoute(
        SquadMate mate,
        Vector3 destination,
        bool emergency,
        out SquadGridPathState state)
    {
        state = new SquadGridPathState { Emergency = emergency, Destination = destination };
        // Normal formation searches are globally staggered and use a small budget;
        // emergency rescue retains the full search budget for correctness.
        var expansionCap = emergency
            ? SquadNavGrid.DefaultExpansionCap
            : SquadNavFollowExpansionCap;
        if (Mathf.Abs(mate.GlobalPosition.Y - destination.Y) <= SquadNavSameBandHeight
            && TryBuildSquadGridWaypoints(mate, destination, expansionCap, out var direct))
        {
            state.Directives = BuildSquadWalkDirectives(direct);
            return true;
        }
        if (TryPlanSquadLayeredRoute(
                mate,
                destination,
                expansionCap,
                out var layered,
                out _))
        {
            state.Directives = layered;
            return true;
        }
        if (_squadLeaderTrail.Count == 0)
        {
            return false;
        }
        foreach (var entryPoint in FindSquadGridTrailEntryCandidates(mate))
        {
            if (!TryBuildSquadGridWaypoints(mate, entryPoint, expansionCap, out var handoff))
            {
                continue;
            }
            state.Directives = BuildSquadWalkDirectives(handoff);
            state.TrailHandoff = true;
            state.HandoffPoint = entryPoint;
            return true;
        }
        return false;
    }

    private List<Vector3> FindSquadGridTrailEntryCandidates(SquadMate mate)
    {
        var ranked = new List<(Vector3 Point, float DistanceSquared)>();
        var position = mate.GlobalPosition;
        for (var index = 0; index < _squadLeaderTrail.Count; index++)
        {
            var point = _squadLeaderTrail[index];
            if (Mathf.Abs(point.Y - position.Y) > 1.8f)
            {
                continue;
            }
            var distanceSquared = position.DistanceSquaredTo(point);
            if (distanceSquared > SquadNavTrailHandoffRange * SquadNavTrailHandoffRange)
            {
                continue;
            }
            InsertSquadGridTrailCandidate(ranked, point, distanceSquared);
        }
        var candidates = new List<Vector3>(ranked.Count);
        foreach (var entry in ranked)
        {
            candidates.Add(entry.Point);
        }
        return candidates;
    }

    private static void InsertSquadGridTrailCandidate(
        List<(Vector3 Point, float DistanceSquared)> candidates,
        Vector3 point,
        float distanceSquared)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (distanceSquared >= candidates[index].DistanceSquared)
            {
                continue;
            }
            candidates.Insert(index, (point, distanceSquared));
            if (candidates.Count > SquadNavTrailHandoffCandidates)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }
            return;
        }
        if (candidates.Count < SquadNavTrailHandoffCandidates)
        {
            candidates.Add((point, distanceSquared));
        }
    }

    private bool TryBuildSquadGridWaypoints(
        SquadMate mate,
        Vector3 goal,
        int expansionCap,
        out Vector3[] waypoints)
    {
        var bucket = SquadTraversalBucket(mate.GlobalPosition);
        var exclude = BuildSquadNavExclusions();
        return TryBuildSquadGridSegment(
            mate.GlobalPosition,
            goal,
            bucket,
            expansionCap,
            exclude,
            out waypoints,
            out _);
    }

    private bool TryBuildSquadGridSegment(
        Vector3 start,
        Vector3 goal,
        int bucket,
        int expansionCap,
        Godot.Collections.Array<Rid> exclude,
        out Vector3[] waypoints,
        out float cost)
    {
        waypoints = Array.Empty<Vector3>();
        cost = float.PositiveInfinity;
        var anchorY = bucket * SquadNavCellSize;
        if (Mathf.Abs(start.Y - goal.Y) <= SquadNavStepHeight
            && IsSquadMovementCorridorClearExcluding(start, goal, exclude))
        {
            waypoints = new[] { goal };
            cost = start.DistanceTo(goal);
            return true;
        }
        var probe = new SquadNavProbe(this, bucket, anchorY, exclude);
        SquadNavWorldToCell(start.X, start.Z, out var startX, out var startZ);
        SquadNavWorldToCell(goal.X, goal.Z, out var goalX, out var goalZ);
        if (!SquadNavPlanner.Contains(startX, startZ) || !SquadNavPlanner.Contains(goalX, goalZ))
        {
            return false;
        }
        var startCell = SquadNavPlanner.SnapToWalkable(probe, startX, startZ, 2);
        if (startCell is null)
        {
            return false;
        }
        foreach (var goalCell in FindSquadGridGoalCells(probe, goal, goalX, goalZ, exclude))
        {
            var path = SquadNavPlanner.FindPath(
                probe,
                startCell.Value.X,
                startCell.Value.Z,
                goalCell.X,
                goalCell.Z,
                expansionCap);
            if (path is not { Count: > 1 })
            {
                continue;
            }
            var points = new List<Vector3>(path.Count + 1);
            foreach (var cell in path)
            {
                if (!TrySampleSquadNavSupport(cell.X, cell.Z, bucket, anchorY, exclude, out var supportY))
                {
                    points.Clear();
                    break;
                }
                points.Add(SquadNavCellToWorld(cell.X, cell.Z, supportY + 0.12f));
            }
            if (points.Count == 0)
            {
                continue;
            }
            points[^1] = new Vector3(goal.X, points[^1].Y, goal.Z);
            waypoints = points.ToArray();
            cost = start.DistanceTo(points[0]);
            for (var index = 1; index < points.Count; index++)
            {
                cost += points[index - 1].DistanceTo(points[index]);
            }
            return true;
        }
        return false;
    }

    private List<(int X, int Z)> FindSquadGridGoalCells(
        SquadNavProbe probe,
        Vector3 goal,
        int goalX,
        int goalZ,
        Godot.Collections.Array<Rid> exclude)
    {
        var candidates = new List<(int X, int Z, float Distance)>();
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dz = -2; dz <= 2; dz++)
            {
                var cellX = goalX + dx;
                var cellZ = goalZ + dz;
                if (!SquadNavPlanner.Contains(cellX, cellZ) || !probe.IsCellWalkable(cellX, cellZ))
                {
                    continue;
                }
                if (!TrySampleSquadNavSupport(cellX, cellZ, probe.Bucket, probe.AnchorY, exclude, out var supportY)
                    || Mathf.Abs(supportY - goal.Y) > SquadNavGoalHeight)
                {
                    continue;
                }
                var cellWorld = SquadNavCellToWorld(cellX, cellZ, supportY);
                if (!IsSquadMovementCorridorClearExcluding(cellWorld, goal, exclude))
                {
                    continue;
                }
                candidates.Add((cellX, cellZ, cellWorld.DistanceSquaredTo(goal)));
            }
        }
        candidates.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        var cells = new List<(int X, int Z)>();
        for (var index = 0; index < candidates.Count && index < 3; index++)
        {
            cells.Add((candidates[index].X, candidates[index].Z));
        }
        return cells;
    }

    private void AdvanceSquadGridCursor(SquadMate mate, SquadGridPathState state)
    {
        while (state.Cursor < state.Directives.Length
            && SquadNavigationDirectiveReached(mate.GlobalPosition, state.Directives, state.Cursor))
        {
            var consumed = state.Directives[state.Cursor];
            var consumedRequired = consumed.Required;
            state.Cursor++;
            if (consumedRequired)
            {
                ClearSquadTraversalRecoveryAttempt(mate, consumed.DirectedEdgeId);
                break;
            }
        }
        if (state.Cursor >= state.Directives.Length
            || state.Directives[state.Cursor].Required)
        {
            return;
        }
        var furthest = Mathf.Min(state.Directives.Length - 1, state.Cursor + 20);
        for (var index = state.Cursor + 1; index <= furthest; index++)
        {
            if (!state.Directives[index].Required)
            {
                continue;
            }
            furthest = index - 1;
            break;
        }
        for (var index = furthest; index > state.Cursor; index--)
        {
            var point = state.Directives[index].Target;
            if (mate.GlobalPosition.DistanceTo(point) > 16.0f
                || Mathf.Abs(point.Y - mate.GlobalPosition.Y) > 1.8f
                || !IsSquadMovementCorridorClear(mate.GlobalPosition, point, mate))
            {
                continue;
            }
            state.Cursor = index;
            break;
        }
    }

    private static bool SquadNavigationDirectiveReached(
        Vector3 position,
        SquadNavigationDirective[] directives,
        int cursor)
    {
        var directive = directives[cursor];
        if (directive.Kind is SquadTraversalKind.Vault or SquadTraversalKind.Drop)
        {
            var actionHorizontal = new Vector2(
                position.X - directive.Target.X,
                position.Z - directive.Target.Z).Length();
            return actionHorizontal <= 0.4f
                && Mathf.Abs(position.Y - directive.Target.Y) <= 0.45f;
        }
        if (directive.Kind != SquadTraversalKind.Step)
        {
            if (!directive.Required)
            {
                return SquadTrailWaypointReached(position, directive.Target);
            }
            var requiredHorizontal = new Vector2(
                position.X - directive.Target.X,
                position.Z - directive.Target.Z).Length();
            return requiredHorizontal <= 0.65f
                && Mathf.Abs(position.Y - directive.Target.Y) <= 0.55f;
        }

        var horizontal = new Vector2(
            position.X - directive.Target.X,
            position.Z - directive.Target.Z).Length();
        var elevationDirection = 0.0f;
        var traversalDirection = Vector2.Zero;
        if (cursor + 1 < directives.Length
            && directives[cursor + 1].Kind == SquadTraversalKind.Step
            && directives[cursor + 1].DirectedEdgeId == directive.DirectedEdgeId)
        {
            elevationDirection = directives[cursor + 1].Target.Y - directive.Target.Y;
            traversalDirection = new Vector2(
                directives[cursor + 1].Target.X - directive.Target.X,
                directives[cursor + 1].Target.Z - directive.Target.Z);
        }
        else if (cursor > 0
            && directives[cursor - 1].Kind == SquadTraversalKind.Step
            && directives[cursor - 1].DirectedEdgeId == directive.DirectedEdgeId)
        {
            elevationDirection = directive.Target.Y - directives[cursor - 1].Target.Y;
            traversalDirection = new Vector2(
                directive.Target.X - directives[cursor - 1].Target.X,
                directive.Target.Z - directives[cursor - 1].Target.Z);
        }
        var elevationReached = elevationDirection < -0.01f
            ? position.Y <= directive.Target.Y + 0.32f
            : position.Y >= directive.Target.Y - 0.32f;
        if (!elevationReached)
        {
            return false;
        }
        if (horizontal <= 0.65f)
        {
            return true;
        }
        if (traversalDirection.LengthSquared() <= 0.0001f)
        {
            return false;
        }
        traversalDirection = traversalDirection.Normalized();
        var offset = new Vector2(
            position.X - directive.Target.X,
            position.Z - directive.Target.Z);
        var along = offset.Dot(traversalDirection);
        var lateral = Mathf.Abs(offset.X * traversalDirection.Y - offset.Y * traversalDirection.X);
        return along >= -0.35f && lateral <= 0.95f;
    }

    private bool TryEstimateSquadGridCost(SquadMate mate, Vector3 destination, out float cost)
    {
        cost = 0.0f;
        if (Mathf.Abs(mate.GlobalPosition.Y - destination.Y) <= SquadNavSameBandHeight
            && TryBuildSquadGridWaypoints(
                mate,
                destination,
                SquadNavEstimateExpansionCap,
                out var waypoints)
            && waypoints.Length > 0)
        {
            cost = mate.GlobalPosition.DistanceTo(waypoints[0]);
            for (var index = 1; index < waypoints.Length; index++)
            {
                cost += waypoints[index - 1].DistanceTo(waypoints[index]);
            }
            return true;
        }
        if (!TryPlanSquadLayeredRoute(
                mate,
                destination,
                SquadNavEstimateExpansionCap,
                out _,
                out cost))
        {
            return false;
        }
        return true;
    }

    private float GetSquadGridRemainingCost(SquadMate mate)
    {
        if (!_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var state)
            || state.Directives.Length == 0
            || state.Cursor >= state.Directives.Length)
        {
            return float.NaN;
        }
        var cost = mate.GlobalPosition.DistanceTo(state.Directives[state.Cursor].Target);
        for (var index = state.Cursor + 1; index < state.Directives.Length; index++)
        {
            cost += state.Directives[index - 1].Target.DistanceTo(state.Directives[index].Target);
        }
        return cost;
    }

    private bool HasPendingRequiredSquadDirective(SquadMate mate)
    {
        if (!_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var state))
        {
            return false;
        }
        for (var index = state.Cursor; index < state.Directives.Length; index++)
        {
            if (state.Directives[index].Required)
            {
                return true;
            }
        }
        return false;
    }

    private bool TryGetActiveRequiredSquadTraversalEdge(SquadMate mate, out int directedEdgeId)
    {
        directedEdgeId = -1;
        if (!_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var state)
            || state.Cursor < 0
            || state.Cursor >= state.Directives.Length)
        {
            return false;
        }
        if (!IsRequiredSquadTraversalEdge(state.Directives, state.Cursor))
        {
            return false;
        }
        directedEdgeId = state.Directives[state.Cursor].DirectedEdgeId;
        return true;
    }

    private bool PreserveActiveSquadTraversalForEmergency(SquadMate mate, Vector3 destination)
    {
        if (!_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var state)
            || state.Cursor < 0
            || state.Cursor >= state.Directives.Length)
        {
            return false;
        }

        // A leader can be downed while a mate is already following a valid grid
        // route toward that same position. Keep the live route object and cursor so
        // emergency takeover does not discard a proven walk corridor and immediately
        // retry the more expensive planner from a less favorable position.
        if (state.Destination.DistanceSquaredTo(destination) <= 1.0f)
        {
            state.Emergency = true;
            state.Destination = destination;
            state.NextPlanMilliseconds = 0;
            state.FailedPlanAttempts = 0;
            return true;
        }

        var current = state.Directives[state.Cursor];
        if (!IsRequiredSquadTraversalEdge(state.Directives, state.Cursor))
        {
            return false;
        }

        var end = state.Cursor;
        while (end + 1 < state.Directives.Length
            && state.Directives[end + 1].Required
            && state.Directives[end + 1].DirectedEdgeId == current.DirectedEdgeId)
        {
            end++;
        }
        var remaining = new SquadNavigationDirective[end - state.Cursor + 1];
        Array.Copy(state.Directives, state.Cursor, remaining, 0, remaining.Length);
        state.Directives = remaining;
        state.Cursor = 0;
        state.Emergency = true;
        state.Destination = destination;
        state.NextPlanMilliseconds = 0;
        state.FailedPlanAttempts = 0;
        state.TrailHandoff = false;
        state.HandoffPoint = Vector3.Zero;
        return true;
    }

    private static bool IsRequiredSquadTraversalEdge(
        SquadNavigationDirective[] directives,
        int cursor)
    {
        if (cursor < 0 || cursor >= directives.Length)
        {
            return false;
        }
        var current = directives[cursor];
        if (!current.Required || current.DirectedEdgeId < 0)
        {
            return false;
        }

        for (var index = cursor; index < directives.Length; index++)
        {
            var directive = directives[index];
            if (!directive.Required || directive.DirectedEdgeId != current.DirectedEdgeId)
            {
                break;
            }
            if (directive.Kind is SquadTraversalKind.Step or SquadTraversalKind.Vault or SquadTraversalKind.Drop)
            {
                return true;
            }
        }
        return false;
    }

    private Godot.Collections.Array<Rid> BuildSquadNavExclusions()
    {
        var exclude = new Godot.Collections.Array<Rid>();
        // The player body (and any downed target pose) must never read as terrain.
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        return exclude;
    }

    private static Vector3 SquadNavCellToWorld(int x, int z, float y)
        => new(SquadNavGridOrigin.X + x * SquadNavCellSize, y, SquadNavGridOrigin.Y + z * SquadNavCellSize);

    private static void SquadNavWorldToCell(float worldX, float worldZ, out int x, out int z)
    {
        x = Mathf.Clamp(
            Mathf.RoundToInt((worldX - SquadNavGridOrigin.X) / SquadNavCellSize),
            0,
            SquadNavPlanner.Width - 1);
        z = Mathf.Clamp(
            Mathf.RoundToInt((worldZ - SquadNavGridOrigin.Y) / SquadNavCellSize),
            0,
            SquadNavPlanner.Height - 1);
    }

    private bool TrySampleSquadNavSupport(
        int x,
        int z,
        int bucket,
        float anchorY,
        Godot.Collections.Array<Rid> exclude,
        out float supportY)
    {
        supportY = float.NaN;
        if (x < 0 || x >= SquadNavPlanner.Width || z < 0 || z >= SquadNavPlanner.Height)
        {
            return false;
        }
        var key = ((long)bucket << 21) | ((long)x * SquadNavPlanner.Height + z);
        if (_squadNavCellSupport.TryGetValue(key, out var cached))
        {
            supportY = cached;
            return !float.IsNaN(supportY);
        }
        var world = SquadNavCellToWorld(x, z, anchorY);
        if (TryProbeSquadNavigationSupport(
                world,
                SquadNavBandRise + 0.12f,
                SquadNavBandDrop + 0.12f,
                SquadNavBandRise,
                SquadNavBandDrop,
                exclude,
                out var sampledSupportY))
        {
            supportY = sampledSupportY;
        }
        if (_squadNavCellSupport.Count >= SquadNavCellCacheResetCapacity)
        {
            _squadNavCellSupport.Clear();
        }
        _squadNavCellSupport[key] = supportY;
        return !float.IsNaN(supportY);
    }

    private bool TryProbeSquadNavigationSupport(
        Vector3 expected,
        float rayAbove,
        float rayBelow,
        float maximumAbove,
        float maximumBelow,
        Godot.Collections.Array<Rid> exclude,
        out float supportY)
    {
        supportY = float.NaN;
        var minimumY = float.PositiveInfinity;
        var maximumY = float.NegativeInfinity;
        var space = GetWorld3D().DirectSpaceState;
        for (var index = 0; index < SquadNavSupportFootprintOffsets.Length; index++)
        {
            var offset = SquadNavSupportFootprintOffsets[index];
            var sample = new Vector3(expected.X + offset.X, expected.Y, expected.Z + offset.Y);
            var query = PhysicsRayQueryParameters3D.Create(
                sample + Vector3.Up * rayAbove,
                sample + Vector3.Down * rayBelow);
            query.CollisionMask = 1;
            query.CollideWithAreas = false;
            query.Exclude = exclude;
            var hit = space.IntersectRay(query);
            if (hit.Count == 0 || hit["normal"].AsVector3().Dot(Vector3.Up) < 0.72f)
            {
                return false;
            }
            var hitY = hit["position"].AsVector3().Y;
            var delta = hitY - expected.Y;
            if (delta > maximumAbove || delta < -maximumBelow)
            {
                return false;
            }
            if (index == 0)
            {
                supportY = hitY;
            }
            minimumY = Mathf.Min(minimumY, hitY);
            maximumY = Mathf.Max(maximumY, hitY);
        }
        if (maximumY - minimumY > SquadNavStepHeight + 0.08f)
        {
            supportY = float.NaN;
            return false;
        }
        if (!HasSquadNavigationBodyClearance(
                new Vector3(expected.X, supportY, expected.Z),
                exclude))
        {
            supportY = float.NaN;
            return false;
        }
        return true;
    }

    private bool HasSquadNavigationBodyClearance(
        Vector3 feet,
        Godot.Collections.Array<Rid> exclude)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _squadNavClearanceShape,
            Transform = new Transform3D(
                Basis.Identity,
                feet + Vector3.Up * (SquadNavClearanceCenterHeight + SquadNavClearanceFloorLift)),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.0f,
            Exclude = exclude
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query, 4).Count == 0;
    }

    private bool TryProbeSquadCorridorSupport(
        Vector3 expected,
        float rayAbove,
        float rayBelow,
        float maximumAbove,
        float maximumBelow,
        Godot.Collections.Array<Rid> exclude,
        out float supportY)
    {
        supportY = float.NaN;
        var sample = expected;
        var query = PhysicsRayQueryParameters3D.Create(
            sample + Vector3.Up * rayAbove,
            sample + Vector3.Down * rayBelow);
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        query.Exclude = exclude;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["normal"].AsVector3().Dot(Vector3.Up) < 0.72f)
        {
            return false;
        }

        var hitY = hit["position"].AsVector3().Y;
        var delta = hitY - expected.Y;
        if (delta > maximumAbove || delta < -maximumBelow)
        {
            return false;
        }
        supportY = hitY;
        return true;
    }

    /// <summary>Deterministic physics-backed probe for one anchor height band.</summary>
    private sealed class SquadNavProbe : ISquadNavProbe
    {
        private readonly FreightTerminalWorld _world;
        private readonly Godot.Collections.Array<Rid> _exclude;

        public SquadNavProbe(
            FreightTerminalWorld world,
            int bucket,
            float anchorY,
            Godot.Collections.Array<Rid> exclude)
        {
            _world = world;
            _exclude = exclude;
            Bucket = bucket;
            AnchorY = anchorY;
        }

        public int Bucket { get; }
        public float AnchorY { get; }

        public bool IsCellWalkable(int x, int z)
            => _world.TrySampleSquadNavSupport(x, z, Bucket, AnchorY, _exclude, out _);

        public bool IsEdgeClear(int x, int z, int neighborX, int neighborZ)
        {
            if (!_world.TrySampleSquadNavSupport(x, z, Bucket, AnchorY, _exclude, out var fromY)
                || !_world.TrySampleSquadNavSupport(neighborX, neighborZ, Bucket, AnchorY, _exclude, out var toY)
                || Mathf.Abs(toY - fromY) > SquadNavStepHeight)
            {
                return false;
            }
            var edge = SquadNavEdgeKey(x, z, neighborX, neighborZ);
            var now = Time.GetTicksMsec();
            if (_world._squadNavEdgeCache.TryGetValue((edge, Bucket), out var cached)
                && now < cached.ExpiresMsec)
            {
                return cached.Clear;
            }
            var clear = _world.IsSquadMovementCorridorClearExcluding(
                SquadNavCellToWorld(x, z, fromY),
                SquadNavCellToWorld(neighborX, neighborZ, toY),
                _exclude);
            _world._squadNavEdgeCache[(edge, Bucket)] = (clear, now + SquadNavEdgeTtlMilliseconds);
            return clear;
        }

        private static long SquadNavEdgeKey(int x, int z, int neighborX, int neighborZ)
        {
            var a = (long)x * SquadNavPlanner.Height + z;
            var b = (long)neighborX * SquadNavPlanner.Height + neighborZ;
            return Math.Min(a, b) << 32 | (uint)Math.Max(a, b);
        }
    }
}
