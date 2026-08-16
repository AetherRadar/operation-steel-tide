using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float SquadTrailSampleSpacing = 0.85f;
    private const float SquadTrailTeleportResetDistance = 18.0f;
    private const int SquadTrailCapacity = 1024;

    private sealed class SquadTrailPathState
    {
        public int Cursor;
        public int EndCursor;
        public int Direction = 1;
        public int Revision;
        public bool Emergency;
        public Vector3 Destination;
        public ulong NextDirectCheckMilliseconds;
    }

    private readonly List<Vector3> _squadLeaderTrail = new();
    private readonly Dictionary<ulong, SquadTrailPathState> _squadTrailPaths = new();
    private bool _squadLeaderTrailInitialized;
    private Vector3 _squadLeaderTrailLastPosition;
    private int _squadLeaderTrailRevision;
    private int _leaderRescueWaypointAdvances;
    private int _leaderRescueReplans;
    private bool _leaderRescueUsedTrail;

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
    }

    private void ResetSquadLeaderTrail(Vector3 position)
    {
        _squadLeaderTrail.Clear();
        _squadLeaderTrail.Add(position);
        _squadLeaderTrailLastPosition = position;
        _squadLeaderTrailInitialized = true;
        _squadLeaderTrailRevision++;
        _squadTrailPaths.Clear();
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
    }

    internal Vector3 ResolveSquadNavigationDestination(
        SquadMate mate,
        Vector3 destination,
        bool emergency)
    {
        if (!IsInstanceValid(mate))
        {
            return destination;
        }

        var id = mate.GetInstanceId();
        _squadTrailPaths.TryGetValue(id, out var state);
        var now = Time.GetTicksMsec();
        if (state is null || now >= state.NextDirectCheckMilliseconds)
        {
            if (IsSquadMovementCorridorClear(mate.GlobalPosition, destination, mate))
            {
                _squadTrailPaths.Remove(id);
                _squadGridPaths.Remove(id);
                return destination;
            }
            if (state is not null)
            {
                state.NextDirectCheckMilliseconds = now + 180;
            }
        }

        if (_squadLeaderTrail.Count > 0)
        {
            if (state is null
                || state.Revision != _squadLeaderTrailRevision
                || state.Emergency != emergency
                || emergency && state.Destination.DistanceSquaredTo(destination) > 1.0f
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
                    return _squadLeaderTrail[state.Cursor];
                }
                // Trail route exhausted without corridor access to the destination.
                _squadTrailPaths.Remove(id);
            }
        }

        // Trail unusable (off-trail, stale, or exhausted): fall back to the
        // geometric ground-grid planner before the legacy direct push.
        if (TryResolveSquadGridNavigation(mate, destination, emergency, out var gridWaypoint))
        {
            return gridWaypoint;
        }
        return destination;
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
            NextDirectCheckMilliseconds = Time.GetTicksMsec() + 180
        };
    }

    private int FindClosestDestinationTrailIndex(Vector3 destination, SquadMate mate)
    {
        var closest = -1;
        var bestDistance = float.PositiveInfinity;
        for (var index = 0; index < _squadLeaderTrail.Count; index++)
        {
            var point = _squadLeaderTrail[index];
            var distance = point.DistanceSquaredTo(destination);
            if (distance >= bestDistance
                || Mathf.Abs(point.Y - destination.Y) > 1.8f
                || !IsSquadMovementCorridorClear(point, destination, mate))
            {
                continue;
            }
            bestDistance = distance;
            closest = index;
        }
        return closest;
    }

    private int FindLatestVisibleTrailIndex(SquadMate mate)
    {
        for (var index = _squadLeaderTrail.Count - 1; index >= 0; index--)
        {
            var point = _squadLeaderTrail[index];
            if (Mathf.Abs(point.Y - mate.GlobalPosition.Y) > 1.8f)
            {
                continue;
            }
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

        if (SquadTrailCursorActive(state))
        {
            var furthest = state.Direction > 0
                ? Mathf.Min(state.EndCursor, state.Cursor + 18)
                : Mathf.Max(state.EndCursor, state.Cursor - 18);
            for (var index = furthest;
                 state.Direction > 0 ? index > state.Cursor : index < state.Cursor;
                 index -= state.Direction)
            {
                var point = _squadLeaderTrail[index];
                if (mate.GlobalPosition.DistanceTo(point) > 16.0f
                    || Mathf.Abs(point.Y - mate.GlobalPosition.Y) > 1.8f
                    || !IsSquadMovementCorridorClear(mate.GlobalPosition, point, mate))
                {
                    continue;
                }
                advanced += Mathf.Abs(index - state.Cursor);
                state.Cursor = index;
                break;
            }
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

    private bool IsSquadMovementCorridorClear(Vector3 from, Vector3 to, SquadMate mate)
    {
        var exclude = new Godot.Collections.Array<Rid> { mate.GetRid() };
        if (IsInstanceValid(_player))
        {
            exclude.Add(_player.GetRid());
        }
        return IsSquadMovementCorridorClearExcluding(from, to, exclude);
    }

    private bool IsSquadMovementCorridorClearExcluding(
        Vector3 from,
        Vector3 to,
        Godot.Collections.Array<Rid> exclude)
    {
        var horizontal = new Vector2(to.X - from.X, to.Z - from.Z);
        if (horizontal.LengthSquared() <= 0.16f)
        {
            return true;
        }
        if (Mathf.Abs(to.Y - from.Y) > horizontal.Length() * 0.8f + 0.8f)
        {
            return false;
        }

        var direction = horizontal.Normalized();
        var side = new Vector3(-direction.Y, 0.0f, direction.X) * 0.3f;
        for (var ray = 0; ray < 3; ray++)
        {
            var offset = ray switch
            {
                1 => side,
                2 => -side,
                _ => Vector3.Zero
            };
            var rayFrom = from + Vector3.Up * 0.82f + offset;
            var rayTo = to + Vector3.Up * 0.82f + offset;
            var query = PhysicsRayQueryParameters3D.Create(rayFrom, rayTo);
            query.CollisionMask = 1;
            query.CollideWithAreas = false;
            query.Exclude = exclude;
            if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0)
            {
                return false;
            }
        }
        return true;
    }

    private float EstimateSquadNavigationCost(SquadMate mate, Vector3 destination)
    {
        if (IsSquadMovementCorridorClear(mate.GlobalPosition, destination, mate))
        {
            return mate.GlobalPosition.DistanceTo(destination);
        }

        var cursor = FindLatestVisibleTrailIndex(mate);
        var endCursor = cursor < 0 ? -1 : FindClosestDestinationTrailIndex(destination, mate);
        if (cursor < 0 || endCursor < 0)
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

    private void BeginLeaderRescueNavigation(SquadMate mate)
    {
        _leaderRescueWaypointAdvances = 0;
        _leaderRescueReplans = 0;
        _leaderRescueUsedTrail = false;
        _leaderRescueGridPlans = 0;
        _leaderRescueUsedGrid = false;
        _squadTrailPaths.Remove(mate.GetInstanceId());
        _squadGridPaths.Remove(mate.GetInstanceId());
        mate.RequestNavigationRecovery();
    }

    private void ReplanLeaderRescueNavigation(SquadMate mate)
    {
        _leaderRescueReplans++;
        _squadTrailPaths.Remove(mate.GetInstanceId());
        _squadGridPaths.Remove(mate.GetInstanceId());
        mate.RequestNavigationRecovery();
    }

    internal void ClearSquadNavigation(SquadMate mate)
    {
        if (IsInstanceValid(mate))
        {
            _squadTrailPaths.Remove(mate.GetInstanceId());
            _squadGridPaths.Remove(mate.GetInstanceId());
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
}
