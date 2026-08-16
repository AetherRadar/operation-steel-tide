using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float SquadNavCellSize = 0.9f;
    private const float SquadNavBandDrop = 1.05f;
    private const float SquadNavBandRise = 1.35f;
    private const float SquadNavStepHeight = 0.55f;
    private const float SquadNavSameBandHeight = 1.6f;
    private const float SquadNavGoalHeight = 1.25f;
    private const ulong SquadNavEdgeTtlMilliseconds = 3000;
    private const int SquadNavEstimateExpansionCap = 2500;
    private const int SquadNavCellCacheResetCapacity = 60000;
    private const int SquadNavTrailHandoffCandidates = 3;
    private const float SquadNavTrailHandoffRange = 26.0f;

    // Covers the full playable ground plane (map 340x320 centered near z=-60).
    private static readonly Vector2 SquadNavGridOrigin = new(-175.0f, -225.0f);
    private static readonly SquadNavGrid SquadNavPlanner = new(400, 367, inflationCells: 44);

    private readonly Dictionary<long, float> _squadNavCellSupport = new();
    private readonly Dictionary<(long Edge, int Bucket), (bool Clear, ulong ExpiresMsec)> _squadNavEdgeCache =
        new();
    private readonly Dictionary<ulong, SquadGridPathState> _squadGridPaths = new();
    private int _leaderRescueGridPlans;
    private bool _leaderRescueUsedGrid;

    private bool LeaderRescueUsedGridForDiagnostics => _leaderRescueUsedGrid;
    private int LeaderRescueGridPlansForDiagnostics => _leaderRescueGridPlans;

    private sealed class SquadGridPathState
    {
        public Vector3[] Waypoints = Array.Empty<Vector3>();
        public int Cursor;
        public bool Emergency;
        public Vector3 Destination;
        public ulong NextPlanMilliseconds;
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
        out Vector3 waypoint)
    {
        waypoint = destination;
        if (!IsInstanceValid(mate))
        {
            return false;
        }
        var now = Time.GetTicksMsec();
        var id = mate.GetInstanceId();
        _squadGridPaths.TryGetValue(id, out var state);

        if (state is not null && state.Waypoints.Length == 0)
        {
            // Blocked marker: keeps replan cost cadenced while no route exists.
            if (now < state.NextPlanMilliseconds)
            {
                return false;
            }
            _squadGridPaths.Remove(id);
            state = null;
        }

        if (state is not null)
        {
            var stale = state.Emergency != emergency
                || emergency && state.Destination.DistanceSquaredTo(destination) > 1.0f
                || state.Cursor >= state.Waypoints.Length
                || !IsSquadMovementCorridorClear(mate.GlobalPosition, state.Waypoints[state.Cursor], mate);
            if (stale)
            {
                _squadGridPaths.Remove(id);
                state = null;
            }
        }

        if (state is not null)
        {
            AdvanceSquadGridCursor(mate, state);
            if (state.Cursor < state.Waypoints.Length)
            {
                waypoint = state.Waypoints[state.Cursor];
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

        if (!TryPlanSquadGridRoute(mate, destination, emergency, out var route))
        {
            _squadGridPaths[id] = new SquadGridPathState { NextPlanMilliseconds = now + 180 };
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
        if (route.Cursor >= route.Waypoints.Length)
        {
            _squadGridPaths.Remove(id);
            return false;
        }
        waypoint = route.Waypoints[route.Cursor];
        return true;
    }

    private bool TryPlanSquadGridRoute(
        SquadMate mate,
        Vector3 destination,
        bool emergency,
        out SquadGridPathState state)
    {
        state = new SquadGridPathState { Emergency = emergency, Destination = destination };
        if (Mathf.Abs(mate.GlobalPosition.Y - destination.Y) <= SquadNavSameBandHeight
            && TryBuildSquadGridWaypoints(mate, destination, SquadNavGrid.DefaultExpansionCap, out var direct))
        {
            state.Waypoints = direct;
            return true;
        }
        if (_squadLeaderTrail.Count == 0)
        {
            return false;
        }
        foreach (var entryPoint in FindSquadGridTrailEntryCandidates(mate))
        {
            if (!TryBuildSquadGridWaypoints(mate, entryPoint, SquadNavGrid.DefaultExpansionCap, out var handoff))
            {
                continue;
            }
            state.Waypoints = handoff;
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
        waypoints = Array.Empty<Vector3>();
        var anchorY = Mathf.Round(mate.GlobalPosition.Y / SquadNavCellSize) * SquadNavCellSize;
        var bucket = Mathf.RoundToInt(anchorY / SquadNavCellSize);
        var exclude = BuildSquadNavExclusions();
        var probe = new SquadNavProbe(this, bucket, anchorY, exclude);
        SquadNavWorldToCell(mate.GlobalPosition.X, mate.GlobalPosition.Z, out var startX, out var startZ);
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
        while (state.Cursor < state.Waypoints.Length
            && SquadTrailWaypointReached(mate.GlobalPosition, state.Waypoints[state.Cursor]))
        {
            state.Cursor++;
        }
        if (state.Cursor >= state.Waypoints.Length)
        {
            return;
        }
        var furthest = Mathf.Min(state.Waypoints.Length - 1, state.Cursor + 20);
        for (var index = furthest; index > state.Cursor; index--)
        {
            var point = state.Waypoints[index];
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

    private bool TryEstimateSquadGridCost(SquadMate mate, Vector3 destination, out float cost)
    {
        cost = 0.0f;
        if (Mathf.Abs(mate.GlobalPosition.Y - destination.Y) > SquadNavSameBandHeight
            || !TryBuildSquadGridWaypoints(mate, destination, SquadNavEstimateExpansionCap, out var waypoints)
            || waypoints.Length == 0)
        {
            return false;
        }
        cost = mate.GlobalPosition.DistanceTo(waypoints[0]);
        for (var index = 1; index < waypoints.Length; index++)
        {
            cost += waypoints[index - 1].DistanceTo(waypoints[index]);
        }
        return true;
    }

    private float GetSquadGridRemainingCost(SquadMate mate)
    {
        if (!_squadGridPaths.TryGetValue(mate.GetInstanceId(), out var state)
            || state.Waypoints.Length == 0
            || state.Cursor >= state.Waypoints.Length)
        {
            return float.NaN;
        }
        var cost = mate.GlobalPosition.DistanceTo(state.Waypoints[state.Cursor]);
        for (var index = state.Cursor + 1; index < state.Waypoints.Length; index++)
        {
            cost += state.Waypoints[index - 1].DistanceTo(state.Waypoints[index]);
        }
        return cost;
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
        var query = PhysicsRayQueryParameters3D.Create(
            world + Vector3.Up * 2.4f,
            world + Vector3.Down * (SquadNavBandDrop + 0.3f));
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        query.Exclude = exclude;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            var hitY = hit["position"].AsVector3().Y;
            if (hitY >= anchorY - SquadNavBandDrop && hitY <= anchorY + SquadNavBandRise)
            {
                supportY = hitY;
            }
        }
        if (_squadNavCellSupport.Count >= SquadNavCellCacheResetCapacity)
        {
            _squadNavCellSupport.Clear();
        }
        _squadNavCellSupport[key] = supportY;
        return !float.IsNaN(supportY);
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
