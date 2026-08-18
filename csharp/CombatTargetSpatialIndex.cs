using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed class CombatTargetSpatialIndex
{
    private const float CellSize = 32.0f;
    private const ulong RefreshIntervalPhysicsFrames = 6;

    private readonly Dictionary<Vector2I, List<EnemyOperator>> _cells = new();
    private ulong _refreshAfterPhysicsFrame;

    public int RefreshCount { get; private set; }

    public void Invalidate()
    {
        _refreshAfterPhysicsFrame = 0;
    }

    public void ResetDiagnostics()
    {
        RefreshCount = 0;
    }

    public void CollectHostileOperators(
        IReadOnlyList<EnemyOperator> operators,
        EnemyOperator self,
        Vector3 origin,
        float range,
        List<Node3D> results)
    {
        RefreshIfNeeded(operators, Engine.GetPhysicsFrames());
        var minimum = CellFor(origin - new Vector3(range, 0.0f, range));
        var maximum = CellFor(origin + new Vector3(range, 0.0f, range));
        for (var x = minimum.X; x <= maximum.X; x++)
        {
            for (var z = minimum.Y; z <= maximum.Y; z++)
            {
                if (!_cells.TryGetValue(new Vector2I(x, z), out var occupants))
                {
                    continue;
                }
                foreach (var candidate in occupants)
                {
                    if (candidate != self && self.IsHostileTo(candidate))
                    {
                        results.Add(candidate);
                    }
                }
            }
        }
    }

    private void RefreshIfNeeded(IReadOnlyList<EnemyOperator> operators, ulong physicsFrame)
    {
        if (_refreshAfterPhysicsFrame != 0 && physicsFrame < _refreshAfterPhysicsFrame)
        {
            return;
        }

        foreach (var occupants in _cells.Values)
        {
            occupants.Clear();
        }
        foreach (var candidate in operators)
        {
            if (!GodotObject.IsInstanceValid(candidate) || candidate.IsDead)
            {
                continue;
            }
            var cell = CellFor(candidate.GlobalPosition);
            if (!_cells.TryGetValue(cell, out var occupants))
            {
                occupants = new List<EnemyOperator>();
                _cells[cell] = occupants;
            }
            occupants.Add(candidate);
        }

        RefreshCount++;
        _refreshAfterPhysicsFrame = physicsFrame + RefreshIntervalPhysicsFrames;
    }

    private static Vector2I CellFor(Vector3 position)
        => new(
            Mathf.FloorToInt(position.X / CellSize),
            Mathf.FloorToInt(position.Z / CellSize));
}
