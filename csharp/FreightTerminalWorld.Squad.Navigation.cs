using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // Legacy partial retained while trail, grid, and traversal caches share one physics lifecycle.
    // Follow-up: extract the complete squad navigation runtime behind a focused service boundary.
    private const float SquadNavigationActiveDestinationDistance = 1.5f;

    private const float SquadTrailSampleSpacing = 0.85f;
    private const float SquadTrailTeleportResetDistance = 18.0f;
    private const int SquadTrailCapacity = 1024;
    private const ulong SquadSpatialQueryCacheMilliseconds = 180;
    private const float SquadSpatialQueryReuseDistanceSquared = 0.2025f;
    private const ulong SquadNavigationDecisionCacheMilliseconds = 120;
    private const ulong SquadNavigationHoldCacheMilliseconds = 240;
    private const ulong SquadNavigationEmergencyCacheMilliseconds = 80;
    private const float SquadNavigationDecisionReuseDistanceSquared = 0.81f;
    private const float SquadTrailVisibilityProbeRangeSquared = 324.0f;
    private const int SquadTrailVisibilityProbeLimit = 12;

    private sealed class SquadTrailPathState
    {
        public int Cursor;
        public int EndCursor;
        public int Direction = 1;
        public int Revision;
        public bool Emergency;
        public Vector3 Destination;
        public ulong NextDirectCheckMilliseconds;
        public ulong NextShortcutCheckMilliseconds;
    }

    private sealed class SquadCorridorQueryState
    {
        public Vector3 From;
        public Vector3 To;
        public bool Clear;
        public ulong ExpiresMilliseconds;
    }

    private sealed class SquadSupportQueryState
    {
        public Vector3 Destination;
        public bool Supported;
        public ulong ExpiresMilliseconds;
    }

    private sealed class SquadNavigationDecisionState
    {
        public Vector3 Origin;
        public Vector3 Destination;
        public bool Emergency;
        public int TrailRevision;
        public SquadNavigationDirective Directive;
        public ulong ExpiresMilliseconds;
    }

    private readonly List<Vector3> _squadLeaderTrail = new();
    private readonly Dictionary<ulong, SquadTrailPathState> _squadTrailPaths = new();
    private readonly Dictionary<ulong, SquadCorridorQueryState> _squadCorridorQueries = new();
    private readonly Dictionary<ulong, SquadSupportQueryState> _squadSupportQueries = new();
    private readonly Dictionary<ulong, SquadNavigationDecisionState> _squadNavigationDecisions = new();
    private bool _squadLeaderTrailInitialized;
    private Vector3 _squadLeaderTrailLastPosition;
    private int _squadLeaderTrailRevision;
    private int _leaderRescueWaypointAdvances;
    private int _leaderRescueReplans;
    private bool _leaderRescueUsedTrail;
    private int _squadNavigationDecisionComputationsForDiagnostics;
    private int _squadNavigationDecisionReusesForDiagnostics;
    private int _squadSupportQueryComputationsForDiagnostics;
    private int _squadSupportQueryReusesForDiagnostics;
    private int _squadCorridorQueryComputationsForDiagnostics;
    private int _squadCorridorQueryReusesForDiagnostics;

    private int LeaderRescueWaypointAdvancesForDiagnostics => _leaderRescueWaypointAdvances;
    private int LeaderRescueReplansForDiagnostics => _leaderRescueReplans;
    private bool LeaderRescueUsedTrailForDiagnostics => _leaderRescueUsedTrail;

    // The leader's real traversal supplies door, corridor and stair connectivity
    // without baking a second navigation mesh over the procedural level.
    private void UpdateSquadLeaderTrail()
    {
        if (!IsInstanceValid(_player) || _player.IsInVehicle)
        {
            return;
        }

        var position = _player.GlobalPosition;
        if (!_squadLeaderTrailInitialized)
        {
            ResetSquadLeaderTrail(position);
            return;
        }

        var segment = position - _squadLeaderTrailLastPosition;
        var distance = segment.Length();
        if (distance > SquadTrailTeleportResetDistance)
        {
            ResetSquadLeaderTrail(position);
            return;
        }
        if (distance < SquadTrailSampleSpacing)
        {
            return;
        }

        var samples = Mathf.Max(1, Mathf.FloorToInt(distance / SquadTrailSampleSpacing));
        var origin = _squadLeaderTrailLastPosition;
        for (var sample = 1; sample <= samples; sample++)
        {
            var fraction = Mathf.Min(1.0f, sample * SquadTrailSampleSpacing / distance);
            AddSquadLeaderTrailPoint(origin.Lerp(position, fraction));
        }
        if (_squadLeaderTrail[^1].DistanceTo(position) >= SquadTrailSampleSpacing * 0.45f)
        {
            AddSquadLeaderTrailPoint(position);
        }
        _squadLeaderTrailLastPosition = position;
    }

    private void AddSquadLeaderTrailPoint(Vector3 position)
    {
        _squadLeaderTrail.Add(position);
        if (_squadLeaderTrail.Count <= SquadTrailCapacity)
        {
            return;
        }

        _squadLeaderTrail.RemoveRange(0, 128);
        _squadLeaderTrailRevision++;
        _squadTrailPaths.Clear();
        _squadGridPaths.Clear();
        _squadCorridorQueries.Clear();
        _squadSupportQueries.Clear();
        _squadNavigationDecisions.Clear();
    }

    private void ResetSquadLeaderTrail(Vector3 position)
    {
        _squadLeaderTrail.Clear();
        _squadLeaderTrail.Add(position);
        _squadLeaderTrailLastPosition = position;
        _squadLeaderTrailInitialized = true;
        _squadLeaderTrailRevision++;
        _squadTrailPaths.Clear();
        _squadGridPaths.Clear();
        _squadCorridorQueries.Clear();
        _squadSupportQueries.Clear();
        _squadNavigationDecisions.Clear();
    }

    private void SetSquadLeaderTrailForDiagnostics(IReadOnlyList<Vector3> points)
    {
        _squadLeaderTrail.Clear();
        foreach (var point in points)
        {
            _squadLeaderTrail.Add(point);
        }
        _squadLeaderTrailLastPosition = points.Count > 0 ? points[^1] : _player.GlobalPosition;
        _squadLeaderTrailInitialized = true;
        _squadLeaderTrailRevision++;
        _squadTrailPaths.Clear();
        _squadGridPaths.Clear();
        _squadCorridorQueries.Clear();
        _squadSupportQueries.Clear();
        _squadNavigationDecisions.Clear();
    }

    internal SquadNavigationDirective ResolveSquadNavigationDestination(
        SquadMate mate,
        Vector3 destination,
        bool emergency)
    {
        if (!IsInstanceValid(mate))
        {
            return SquadNavigationDirective.Walk(destination);
        }
        if (!emergency && IsInstanceValid(_player) && _player.IsInVehicle)
        {
            ClearSquadNavigation(mate);
            return SquadNavigationDirective.Walk(destination);
        }

        var id = mate.GetInstanceId();
        if (HasPendingRequiredSquadDirective(mate)
            && TryResolveSquadGridNavigation(mate, destination, emergency, out var requiredDirective))
        {
            return requiredDirective;
        }
        var now = Time.GetTicksMsec();
        if (TryReuseSquadNavigationDecision(
                mate,
                destination,
                emergency,
                now,
                out var cachedDirective))
        {
            return cachedDirective;
        }
        _squadNavigationDecisionComputationsForDiagnostics++;
        _squadTrailPaths.TryGetValue(id, out var state);
        if (state is null || now >= state.NextDirectCheckMilliseconds)
        {
            if (IsSquadMovementCorridorClearCached(
                    mate,
                    mate.GlobalPosition,
                    destination,
                    now))
            {
                // Keep an already validated route as a fallback while taking the
                // shortcut. Door-edge capsule probes can fluctuate for a frame as
                // the character clears a portal; discarding the route here leaves
                // the mate with no safe directive if the next probe is blocked.
                return CacheSquadNavigationDecision(
                    mate,
                    destination,
                    emergency,
                    now,
                    SquadNavigationDirective.Walk(destination));
            }
            if (state is not null)
            {
                state.NextDirectCheckMilliseconds = now + 180;
            }
        }
        if (CanUseSquadSteppedDirectRoute(mate, destination))
        {
            return CacheSquadNavigationDecision(
                mate,
                destination,
                emergency,
                now,
                SquadNavigationDirective.Walk(destination, steppedDirect: true));
        }

        if (_squadLeaderTrail.Count > 0)
        {
            if (state is null
                || state.Revision != _squadLeaderTrailRevision
                || state.Emergency != emergency
                || state.Destination.DistanceSquaredTo(destination)
                    > SquadNavigationActiveDestinationDistance * SquadNavigationActiveDestinationDistance
                || state.Cursor < 0
                || state.Cursor >= _squadLeaderTrail.Count)
            {
                state = PlanSquadTrailPath(mate, destination, emergency);
                if (state is null)
                {
                    _squadTrailPaths.Remove(id);
                }
                else
                {
                    _squadTrailPaths[id] = state;
                }
            }
            if (state is not null)
            {
                AdvanceSquadTrailCursor(mate, state, emergency);
                if (SquadTrailCursorActive(state))
                {
                    _squadGridPaths.Remove(id);
                    return CacheSquadNavigationDecision(
                        mate,
                        destination,
                        emergency,
                        now,
                        SquadNavigationDirective.Walk(_squadLeaderTrail[state.Cursor]));
                }
                // Trail route exhausted without corridor access to the destination.
                _squadTrailPaths.Remove(id);
            }
        }

        // Trail unusable (off-trail, stale, or exhausted): fall back to the
        // geometric ground-grid planner. If it cannot plan this frame, hold position
        // instead of deliberately pushing into the blocking wall.
        if (TryResolveSquadGridNavigation(mate, destination, emergency, out var gridDirective))
        {
            return CacheSquadNavigationDecision(
                mate,
                destination,
                emergency,
                now,
                gridDirective);
        }
        if (mate.CanUseLocalNavigationTraversal(destination))
        {
            return CacheSquadNavigationDecision(
                mate,
                destination,
                emergency,
                now,
                SquadNavigationDirective.Walk(destination));
        }
        return CacheSquadNavigationDecision(
            mate,
            destination,
            emergency,
            now,
            SquadNavigationDirective.Walk(mate.GlobalPosition));
    }

    private bool TryReuseSquadNavigationDecision(
        SquadMate mate,
        Vector3 destination,
        bool emergency,
        ulong now,
        out SquadNavigationDirective directive)
    {
        directive = default;
        var id = mate.GetInstanceId();
        if (!_squadNavigationDecisions.TryGetValue(id, out var cached)
            || now >= cached.ExpiresMilliseconds
            || cached.Emergency != emergency
            || cached.TrailRevision != _squadLeaderTrailRevision
            || cached.Origin.DistanceSquaredTo(mate.GlobalPosition)
                > SquadNavigationDecisionReuseDistanceSquared
            || cached.Destination.DistanceSquaredTo(destination)
                > SquadNavigationDecisionReuseDistanceSquared)
        {
            _squadNavigationDecisions.Remove(id);
            return false;
        }

        directive = cached.Directive;
        _squadNavigationDecisionReusesForDiagnostics++;
        return true;
    }

    private SquadNavigationDirective CacheSquadNavigationDecision(
        SquadMate mate,
        Vector3 destination,
        bool emergency,
        ulong now,
        SquadNavigationDirective directive)
    {
        var holding = directive.Kind == SquadTraversalKind.Walk
            && directive.Target.DistanceSquaredTo(mate.GlobalPosition) <= 0.04f;
        _squadNavigationDecisions[mate.GetInstanceId()] = new SquadNavigationDecisionState
        {
            Origin = mate.GlobalPosition,
            Destination = destination,
            Emergency = emergency,
            TrailRevision = _squadLeaderTrailRevision,
            Directive = directive,
            ExpiresMilliseconds = now + (emergency
                ? SquadNavigationEmergencyCacheMilliseconds
                : holding
                    ? SquadNavigationHoldCacheMilliseconds
                    : SquadNavigationDecisionCacheMilliseconds)
        };
        return directive;
    }

    private bool CanUseSquadSteppedDirectRoute(SquadMate mate, Vector3 destination)
        => TryValidateSquadSteppedDirectRoute(mate, destination, out _);

    private bool TryValidateSquadSteppedDirectRoute(
        SquadMate mate,
        Vector3 destination,
        out string reason)
    {
        reason = "geometry";
        var origin = mate.GlobalPosition;
        var horizontal = new Vector2(destination.X - origin.X, destination.Z - origin.Z);
        var horizontalDistance = horizontal.Length();
        var verticalDistance = destination.Y - origin.Y;
        if (horizontalDistance is < 0.45f or > 7.5f
            || Mathf.Abs(verticalDistance) > 2.2f
            || Mathf.Abs(verticalDistance) > horizontalDistance * 0.72f + 0.5f)
        {
            return false;
        }

        var exclude = new Godot.Collections.Array<Rid> { mate.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        var samples = Mathf.Max(3, Mathf.CeilToInt(horizontalDistance / 0.3f));
        var previousSupport = float.NaN;
        var finalSupport = float.NaN;
        for (var sample = 0; sample <= samples; sample++)
        {
            var expected = origin.Lerp(destination, sample / (float)samples);
            var supportY = float.NaN;
            var closestDelta = float.PositiveInfinity;
            foreach (var offset in SquadNavSupportFootprintOffsets)
            {
                var probe = new Vector3(expected.X + offset.X, expected.Y, expected.Z + offset.Y);
                if (!PhysicsRaycast.TryHit(
                        GetWorld3D(),
                        probe + Vector3.Up * 0.7f,
                        probe + Vector3.Down * 0.85f,
                        exclude,
                        1,
                        out var support)
                    || support.Normal.Dot(Vector3.Up) < 0.72f)
                {
                    continue;
                }
                var candidateDelta = Mathf.Abs(support.Position.Y - expected.Y);
                if (candidateDelta < closestDelta)
                {
                    closestDelta = candidateDelta;
                    supportY = support.Position.Y;
                }
            }
            if (float.IsNaN(supportY))
            {
                reason = $"support:{sample}";
                return false;
            }
            var supportDelta = supportY - expected.Y;
            if (supportDelta is > 0.52f or < -0.68f)
            {
                reason = $"support_delta:{sample}:{supportDelta:0.00}";
                return false;
            }
            if (!float.IsNaN(previousSupport))
            {
                var step = supportY - previousSupport;
                if (Mathf.Abs(step) > 0.32f
                    || verticalDistance > 0.0f && step < -0.16f
                    || verticalDistance < 0.0f && step > 0.16f)
                {
                    reason = $"step:{sample}:{step:0.00}";
                    return false;
                }
            }
            previousSupport = supportY;
            finalSupport = supportY;
        }
        if (Mathf.Abs(finalSupport - destination.Y) > 0.75f)
        {
            reason = $"final:{finalSupport - destination.Y:0.00}";
            return false;
        }

        if (PhysicsRaycast.HasHit(
            GetWorld3D(),
            origin + Vector3.Up * 1.35f,
            destination + Vector3.Up * 1.35f,
            exclude,
            1))
        {
            reason = "body_ray";
            return false;
        }
        reason = "clear";
        return true;
    }

    internal Vector3 ResolveSquadFollowDestination(SquadMate mate, Vector3 formationDestination)
    {
        if (!IsInstanceValid(mate) || !IsInstanceValid(_player))
        {
            return formationDestination;
        }
        if (_player.IsInVehicle)
        {
            return formationDestination;
        }

        // Formation offsets can hang beyond a narrow catwalk or hub platform. While
        // changing floors, or whenever that offset has no real support, route to the
        // leader's feet first. Formation spacing resumes after both operators share a
        // walkable height band.
        return Mathf.Abs(mate.GlobalPosition.Y - _player.GlobalPosition.Y) > SquadNavSameBandHeight
            || !IsSquadNavigationDestinationSupported(formationDestination, mate)
                ? _player.GlobalPosition
                : formationDestination;
    }

    private bool IsSquadNavigationDestinationSupported(Vector3 destination, SquadMate mate)
    {
        var now = Time.GetTicksMsec();
        var id = mate.GetInstanceId();
        if (_squadSupportQueries.TryGetValue(id, out var cached)
            && now < cached.ExpiresMilliseconds
            && cached.Destination.DistanceSquaredTo(destination) <= SquadSpatialQueryReuseDistanceSquared)
        {
            _squadSupportQueryReusesForDiagnostics++;
            return cached.Supported;
        }

        _squadSupportQueryComputationsForDiagnostics++;
        var exclude = new Godot.Collections.Array<Rid> { mate.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        var supported = TryProbeSquadNavigationSupport(
            destination,
            SquadNavStepHeight + 0.12f,
            SquadNavBandDrop + 0.12f,
            maximumAbove: 0.2f,
            maximumBelow: SquadNavBandDrop,
            exclude,
            out _);
        _squadSupportQueries[id] = new SquadSupportQueryState
        {
            Destination = destination,
            Supported = supported,
            ExpiresMilliseconds = now + SquadSpatialQueryCacheMilliseconds
        };
        return supported;
    }

    private bool IsSquadMovementCorridorClearCached(
        SquadMate mate,
        Vector3 from,
        Vector3 to,
        ulong now)
    {
        var id = mate.GetInstanceId();
        if (_squadCorridorQueries.TryGetValue(id, out var cached)
            && now < cached.ExpiresMilliseconds
            && cached.From.DistanceSquaredTo(from) <= SquadSpatialQueryReuseDistanceSquared
            && cached.To.DistanceSquaredTo(to) <= SquadSpatialQueryReuseDistanceSquared)
        {
            _squadCorridorQueryReusesForDiagnostics++;
            return cached.Clear;
        }

        _squadCorridorQueryComputationsForDiagnostics++;
        var clear = IsSquadMovementCorridorClear(from, to, mate);
        _squadCorridorQueries[id] = new SquadCorridorQueryState
        {
            From = from,
            To = to,
            Clear = clear,
            ExpiresMilliseconds = now + SquadSpatialQueryCacheMilliseconds
        };
        return clear;
    }

    private SquadTrailPathState? PlanSquadTrailPath(
        SquadMate mate,
        Vector3 destination,
        bool emergency)
    {
        var cursor = FindLatestVisibleTrailIndex(mate);
        if (cursor < 0)
        {
            // Off-trail: the grid planner owns the approach (or hands back to the
            // trail at a reachable entry point). Blind nearest-point beelines
            // walk squad mates straight into walls.
            return null;
        }
        var endCursor = emergency
            ? FindClosestDestinationTrailIndex(destination, mate)
            : _squadLeaderTrail.Count - 1;
        if (endCursor < 0)
        {
            if (emergency)
            {
                // No trail point connects to the destination: falling back to the
                // trail tail would hug walls or spin in place. Hand off to the
                // geometric grid planner instead.
                return null;
            }
            endCursor = _squadLeaderTrail.Count - 1;
        }
        if (!IsSquadTrailSpanTraversable(cursor, endCursor))
        {
            return null;
        }
        if (emergency)
        {
            _leaderRescueUsedTrail = true;
        }
        return new SquadTrailPathState
        {
            Cursor = Mathf.Max(0, cursor),
            EndCursor = endCursor,
            Direction = endCursor >= cursor ? 1 : -1,
            Revision = _squadLeaderTrailRevision,
            Emergency = emergency,
            Destination = destination,
            NextDirectCheckMilliseconds = Time.GetTicksMsec() + 180,
            NextShortcutCheckMilliseconds = Time.GetTicksMsec() + SquadNavShortcutCheckIntervalMilliseconds
        };
    }

    private int FindClosestDestinationTrailIndex(Vector3 destination, SquadMate mate)
    {
        var candidates = new List<(int Index, float DistanceSquared)>();
        for (var index = 0; index < _squadLeaderTrail.Count; index++)
        {
            var point = _squadLeaderTrail[index];
            var distance = point.DistanceSquaredTo(destination);
            if (distance > SquadTrailVisibilityProbeRangeSquared
                || Mathf.Abs(point.Y - destination.Y) > 1.8f)
            {
                continue;
            }
            candidates.Add((index, distance));
        }
        candidates.Sort(static (left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
        var probes = Mathf.Min(candidates.Count, SquadTrailVisibilityProbeLimit);
        for (var candidate = 0; candidate < probes; candidate++)
        {
            var index = candidates[candidate].Index;
            if (!IsSquadMovementCorridorClear(_squadLeaderTrail[index], destination, mate))
            {
                continue;
            }
            return index;
        }
        return -1;
    }

    private int FindLatestVisibleTrailIndex(SquadMate mate)
    {
        var probes = 0;
        for (var index = _squadLeaderTrail.Count - 1; index >= 0; index--)
        {
            var point = _squadLeaderTrail[index];
            if (Mathf.Abs(point.Y - mate.GlobalPosition.Y) > 1.8f)
            {
                continue;
            }
            if (mate.GlobalPosition.DistanceSquaredTo(point) > SquadTrailVisibilityProbeRangeSquared)
            {
                continue;
            }
            if (probes >= SquadTrailVisibilityProbeLimit)
            {
                break;
            }
            probes++;
            if (IsSquadMovementCorridorClear(mate.GlobalPosition, point, mate))
            {
                return index;
            }
        }
        return -1;
    }

    private void AdvanceSquadTrailCursor(
        SquadMate mate,
        SquadTrailPathState state,
        bool emergency)
    {
        var advanced = 0;
        while (SquadTrailCursorActive(state)
            && SquadTrailWaypointReached(mate.GlobalPosition, _squadLeaderTrail[state.Cursor]))
        {
            state.Cursor += state.Direction;
            advanced++;
        }

        var now = Time.GetTicksMsec();
        if (SquadTrailCursorActive(state)
            && now >= state.NextShortcutCheckMilliseconds)
        {
            // Trail points are already sequentially traversable. Keep one optional
            // far shortcut probe off the hot path instead of testing every point.
            var furthest = state.Direction > 0
                ? Mathf.Min(state.EndCursor, state.Cursor + 18)
                : Mathf.Max(state.EndCursor, state.Cursor - 18);
            var shortcut = state.Cursor;
            for (var index = furthest;
                 state.Direction > 0 ? index > state.Cursor : index < state.Cursor;
                 index -= state.Direction)
            {
                var point = _squadLeaderTrail[index];
                if (mate.GlobalPosition.DistanceTo(point) > 16.0f
                    || Mathf.Abs(point.Y - mate.GlobalPosition.Y) > 1.8f)
                {
                    continue;
                }
                shortcut = index;
                break;
            }
            if (shortcut != state.Cursor
                && IsSquadMovementCorridorClear(
                    mate.GlobalPosition,
                    _squadLeaderTrail[shortcut],
                    mate))
            {
                advanced += Mathf.Abs(shortcut - state.Cursor);
                state.Cursor = shortcut;
            }
            state.NextShortcutCheckMilliseconds = now + SquadNavShortcutCheckIntervalMilliseconds;
        }

        if (emergency && advanced > 0)
        {
            _leaderRescueWaypointAdvances += advanced;
        }
    }

    private bool SquadTrailCursorActive(SquadTrailPathState state)
    {
        if (state.Cursor < 0 || state.Cursor >= _squadLeaderTrail.Count)
        {
            return false;
        }
        return state.Direction > 0
            ? state.Cursor <= state.EndCursor
            : state.Cursor >= state.EndCursor;
    }

    private static bool SquadTrailWaypointReached(Vector3 position, Vector3 waypoint)
    {
        var horizontal = new Vector2(position.X - waypoint.X, position.Z - waypoint.Z).Length();
        return horizontal <= 1.05f && Mathf.Abs(position.Y - waypoint.Y) <= 1.25f;
    }

    private bool IsSquadTrailSpanTraversable(int cursor, int endCursor)
    {
        if (cursor < 0 || endCursor < 0 || cursor >= _squadLeaderTrail.Count || endCursor >= _squadLeaderTrail.Count)
        {
            return false;
        }
        var direction = endCursor >= cursor ? 1 : -1;
        for (var index = cursor; index != endCursor; index += direction)
        {
            var next = index + direction;
            var from = _squadLeaderTrail[index];
            var to = _squadLeaderTrail[next];
            var horizontal = new Vector2(to.X - from.X, to.Z - from.Z).Length();
            var vertical = to.Y - from.Y;
            if (horizontal <= 0.4f && Mathf.Abs(vertical) > SquadNavStepHeight
                || vertical > horizontal * 0.82f + SquadNavStepHeight
                || -vertical > horizontal * 1.15f + 0.85f)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsSquadMovementCorridorClear(Vector3 from, Vector3 to, SquadMate mate)
    {
        var exclude = new Godot.Collections.Array<Rid> { mate.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        return IsSquadMovementCorridorClearExcluding(from, to, exclude);
    }

    private bool IsSquadMovementCorridorClearExcluding(
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude,
        SquadNavSearchBudget? budget = null)
    {
        if (budget is not null && !budget.CanProbe)
        {
            return false;
        }
        var horizontal = new Vector2(to.X - from.X, to.Z - from.Z);
        if (horizontal.LengthSquared() <= 0.16f)
        {
            return Mathf.Abs(to.Y - from.Y) <= SquadNavStepHeight;
        }
        if (Mathf.Abs(to.Y - from.Y) > horizontal.Length() * 0.8f + 0.8f)
        {
            return false;
        }
        if (!HasSquadCorridorSupport(from, to, horizontal.Length(), exclude, budget))
        {
            return false;
        }

        var direction = horizontal.Normalized();
        var side = new Vector3(-direction.Y, 0.0f, direction.X) * 0.3f;
        for (var ray = 0; ray < 3; ray++)
        {
            if (budget is not null && !budget.CanProbe)
            {
                return false;
            }
            var offset = ray switch
            {
                1 => side,
                2 => -side,
                _ => Vector3.Zero
            };
            var rayFrom = from + Vector3.Up * 0.82f + offset;
            var rayTo = to + Vector3.Up * 0.82f + offset;
            if (PhysicsRaycast.HasHit(GetWorld3D(), rayFrom, rayTo, exclude, 1))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsSquadPortalCenterRayClear(
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude,
        SquadNavSearchBudget? budget = null)
    {
        return (budget is null || budget.CanProbe)
            && !PhysicsRaycast.HasHit(
            GetWorld3D(),
            from + Vector3.Up * 0.82f,
            to + Vector3.Up * 0.82f,
            exclude,
            1);
    }

    private bool HasSquadCorridorSupport(
        Vector3 from,
        Vector3 to,
        float horizontalLength,
        Godot.Collections.Array<Rid> exclude,
        SquadNavSearchBudget? budget = null)
    {
        var samples = Mathf.Max(1, Mathf.CeilToInt(horizontalLength / SquadNavCorridorSampleSpacing));
        var previousSupport = float.NaN;
        var firstSupport = float.NaN;
        var lastSupport = float.NaN;
        for (var sample = 0; sample <= samples; sample++)
        {
            if (budget is not null && !budget.CanProbe)
            {
                return false;
            }
            var expected = from.Lerp(to, sample / (float)samples);
            var endpoint = sample == 0 || sample == samples;
            var supported = endpoint
                ? TryProbeSquadNavigationSupport(
                    expected,
                    SquadNavStepHeight + 0.12f,
                    SquadNavBandDrop + 0.12f,
                    maximumAbove: 0.22f,
                    maximumBelow: SquadNavBandDrop,
                    exclude,
                    out var supportY)
                : TryProbeSquadCorridorSupport(
                    expected,
                    SquadNavStepHeight + 0.12f,
                    SquadNavBandDrop + 0.12f,
                    maximumAbove: 0.22f,
                    maximumBelow: SquadNavBandDrop,
                    exclude,
                    out supportY);
            if (!supported)
            {
                return false;
            }
            if (sample == 0)
            {
                firstSupport = supportY;
            }
            if (!float.IsNaN(previousSupport)
                    && Mathf.Abs(supportY - previousSupport) > SquadNavStepHeight + 0.12f)
            {
                return false;
            }
            previousSupport = supportY;
            lastSupport = supportY;
        }
        return IsSquadCorridorCapsuleSweepClear(
            new Vector3(from.X, firstSupport, from.Z),
            new Vector3(to.X, lastSupport, to.Z),
            exclude,
            budget);
    }

    private bool IsSquadCorridorCapsuleSweepClear(
        Vector3 fromFeet,
        Vector3 toFeet,
        Godot.Collections.Array<Rid> exclude,
        SquadNavSearchBudget? budget = null)
    {
        if (budget is not null && !budget.CanProbe)
        {
            return false;
        }
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _squadNavClearanceShape,
            Transform = new Transform3D(
                Basis.Identity,
                fromFeet + Vector3.Up * (SquadNavClearanceCenterHeight + SquadNavClearanceFloorLift)),
            Motion = toFeet - fromFeet,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.0f,
            Exclude = exclude
        };
        var fractions = GetWorld3D().DirectSpaceState.CastMotion(query);
        return fractions.Length >= 2 && fractions[0] >= 0.999f;
    }

    private float EstimateSquadNavigationCost(SquadMate mate, Vector3 destination)
    {
        if (IsSquadMovementCorridorClear(mate.GlobalPosition, destination, mate))
        {
            return mate.GlobalPosition.DistanceTo(destination);
        }

        var cursor = FindLatestVisibleTrailIndex(mate);
        var endCursor = cursor < 0 ? -1 : FindClosestDestinationTrailIndex(destination, mate);
        if (cursor < 0 || endCursor < 0 || !IsSquadTrailSpanTraversable(cursor, endCursor))
        {
            // No usable trail route: the geometric grid supplies a real estimate
            // so reviver selection prefers a mate that can actually path there.
            if (TryEstimateSquadGridCost(mate, destination, out var gridCost))
            {
                return gridCost;
            }
            return mate.GlobalPosition.DistanceTo(destination) + 1000.0f;
        }
        var cost = mate.GlobalPosition.DistanceTo(_squadLeaderTrail[cursor]);
        var direction = endCursor >= cursor ? 1 : -1;
        for (var index = cursor + direction; direction > 0 ? index <= endCursor : index >= endCursor; index += direction)
        {
            cost += _squadLeaderTrail[index - direction].DistanceTo(_squadLeaderTrail[index]);
        }
        cost += _squadLeaderTrail[endCursor].DistanceTo(destination);
        return cost;
    }

    private float GetSquadNavigationRemainingCost(SquadMate mate, Vector3 destination)
    {
        if (!_squadTrailPaths.TryGetValue(mate.GetInstanceId(), out var state)
            || state.Cursor < 0
            || state.Cursor >= _squadLeaderTrail.Count)
        {
            var gridCost = GetSquadGridRemainingCost(mate);
            if (!float.IsNaN(gridCost))
            {
                return gridCost;
            }
            return mate.GlobalPosition.DistanceTo(destination);
        }

        var cost = mate.GlobalPosition.DistanceTo(_squadLeaderTrail[state.Cursor]);
        for (var index = state.Cursor + state.Direction;
             state.Direction > 0 ? index <= state.EndCursor : index >= state.EndCursor;
             index += state.Direction)
        {
            cost += _squadLeaderTrail[index - state.Direction].DistanceTo(_squadLeaderTrail[index]);
        }
        cost += _squadLeaderTrail[state.EndCursor].DistanceTo(destination);
        return cost;
    }

    private void BeginLeaderRescueNavigation(SquadMate mate, Vector3 destination)
    {
        _leaderRescueWaypointAdvances = 0;
        _leaderRescueReplans = 0;
        _leaderRescueUsedTrail = false;
        _leaderRescueGridPlans = 0;
        _leaderRescueUsedGrid = false;
        _squadTrailPaths.Remove(mate.GetInstanceId());
        _squadNavigationDecisions.Remove(mate.GetInstanceId());
        if (PreserveActiveSquadTraversalForEmergency(mate, destination))
        {
            _leaderRescueUsedGrid = true;
        }
        else
        {
            _squadGridPaths.Remove(mate.GetInstanceId());
        }
        mate.RequestNavigationRecovery();
    }

    private void ReplanLeaderRescueNavigation(SquadMate mate)
    {
        _leaderRescueReplans++;
        if (TryGetActiveRequiredSquadTraversalEdge(mate, out var directedEdgeId))
        {
            if (PreserveSquadTraversalAfterStall(mate, directedEdgeId))
            {
                mate.RequestRequiredStepRecovery();
                return;
            }
            ReportSquadTraversalFailure(mate, directedEdgeId);
            mate.RequestNavigationRecovery(forceEscape: true);
            return;
        }
        _squadTrailPaths.Remove(mate.GetInstanceId());
        _squadGridPaths.Remove(mate.GetInstanceId());
        _squadNavigationDecisions.Remove(mate.GetInstanceId());
        mate.RequestNavigationRecovery(forceEscape: true);
    }

    internal void ReplanSquadNavigationAfterStall(SquadMate mate)
    {
        if (!IsInstanceValid(mate))
        {
            return;
        }
        if (TryGetActiveRequiredSquadTraversalEdge(mate, out var directedEdgeId))
        {
            if (PreserveSquadTraversalAfterStall(mate, directedEdgeId))
            {
                mate.RequestRequiredStepRecovery();
                return;
            }
            ReportSquadTraversalFailure(mate, directedEdgeId);
        }
        else
        {
            ClearSquadNavigation(mate);
        }
        mate.RequestNavigationRecovery(forceEscape: true);
    }

    internal void ClearSquadNavigation(SquadMate mate)
    {
        if (IsInstanceValid(mate))
        {
            var id = mate.GetInstanceId();
            _squadTrailPaths.Remove(id);
            _squadGridPaths.Remove(id);
            _squadCorridorQueries.Remove(id);
            _squadSupportQueries.Remove(id);
            _squadNavigationDecisions.Remove(id);
        }
    }

    private Node3D BuildSquadRescueMazeForDiagnostics(
        out Vector3 playerPosition,
        out Vector3 reviverPosition,
        out Vector3[] leaderTrail)
    {
        var origin = new Vector3(0.0f, 80.0f, 0.0f);
        var root = new Node3D { Name = "SquadRescueMazeDiagnostic", Position = origin };
        AddChild(root);

        var floor = new StaticBody3D
        {
            Name = "SquadRescueMazeFloor",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(floor);
        floor.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, -0.15f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(24.0f, 0.3f, 22.0f) }
        });

        var wall = new StaticBody3D
        {
            Name = "SquadRescueDoorWall",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(wall);
        wall.AddChild(new CollisionShape3D
        {
            Name = "LeftWall",
            Position = new Vector3(-3.75f, 1.6f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(12.5f, 3.2f, 0.65f) }
        });
        wall.AddChild(new CollisionShape3D
        {
            Name = "RightWall",
            Position = new Vector3(8.0f, 1.6f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(4.0f, 3.2f, 0.65f) }
        });

        playerPosition = origin + new Vector3(0.0f, 0.25f, 7.5f);
        reviverPosition = origin + new Vector3(0.0f, 0.25f, -7.5f);
        leaderTrail = new[]
        {
            reviverPosition,
            origin + new Vector3(2.2f, 0.25f, -5.5f),
            origin + new Vector3(4.25f, 0.25f, -3.2f),
            origin + new Vector3(4.25f, 0.25f, -1.0f),
            origin + new Vector3(4.25f, 0.25f, 1.0f),
            origin + new Vector3(4.25f, 0.25f, 3.2f),
            origin + new Vector3(2.2f, 0.25f, 5.5f),
            playerPosition
        };
        return root;
    }

    private Node3D BuildSquadRecoveryPinchForDiagnostics(
        out Vector3 playerPosition,
        out Vector3 reviverPosition)
    {
        var origin = new Vector3(45.0f, 80.0f, 0.0f);
        var root = new Node3D { Name = "SquadRecoveryPinchDiagnostic", Position = origin };
        AddChild(root);

        var floor = new StaticBody3D
        {
            Name = "SquadRecoveryPinchFloor",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(floor);
        floor.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, -0.15f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(18.0f, 0.3f, 16.0f) }
        });

        var pinch = new StaticBody3D
        {
            Name = "SquadRecoveryPinchPosts",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(pinch);
        for (var index = 0; index < 2; index++)
        {
            pinch.AddChild(new CollisionShape3D
            {
                Name = $"PinchPost_{index + 1}",
                Position = new Vector3(index == 0 ? -0.48f : 0.48f, 1.6f, 0.0f),
                Shape = new BoxShape3D { Size = new Vector3(0.26f, 3.2f, 0.55f) }
            });
        }

        playerPosition = origin + new Vector3(0.0f, 0.25f, 5.5f);
        reviverPosition = origin + new Vector3(0.0f, 0.25f, -5.5f);
        return root;
    }
}
