using System;
using System.Collections.Generic;

namespace OperationSteelTide;

/// <summary>
/// Lazy walkability source for the squad ground navigation grid. Implementations
/// sample physics on demand and must stay deterministic for identical world state.
/// </summary>
public interface ISquadNavProbe
{
    bool IsCellWalkable(int x, int z);

    /// <summary>True when an operator-sized corridor connects two adjacent cells.</summary>
    bool IsEdgeClear(int x, int z, int neighborX, int neighborZ);
}

/// <summary>
/// Pure 8-connected A* over a lazily probed walkability grid. Coordinates are
/// cell indices; world conversion stays with the caller so planning carries no
/// engine dependencies and remains deterministic.
/// </summary>
public sealed class SquadNavGrid
{
    private const float DiagonalStepCost = 1.4142136f;
    public const int DefaultExpansionCap = 6000;

    private static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] StepZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

    private readonly int _width;
    private readonly int _height;
    private readonly int _inflationCells;

    public SquadNavGrid(int width, int height, int inflationCells)
    {
        _width = width;
        _height = height;
        _inflationCells = inflationCells;
    }

    public int Width => _width;
    public int Height => _height;

    public bool Contains(int x, int z) => x >= 0 && x < _width && z >= 0 && z < _height;

    /// <summary>
    /// Nearest walkable cell within <paramref name="maxRing"/> Chebyshev rings of the
    /// anchor, or null when the whole neighborhood is unwalkable.
    /// </summary>
    public (int X, int Z)? SnapToWalkable(ISquadNavProbe probe, int x, int z, int maxRing)
    {
        if (Contains(x, z) && probe.IsCellWalkable(x, z))
        {
            return (x, z);
        }
        for (var ring = 1; ring <= maxRing; ring++)
        {
            (int X, int Z)? best = null;
            var bestDistance = float.PositiveInfinity;
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dz = -ring; dz <= ring; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring)
                    {
                        continue;
                    }
                    var candidateX = x + dx;
                    var candidateZ = z + dz;
                    if (!Contains(candidateX, candidateZ) || !probe.IsCellWalkable(candidateX, candidateZ))
                    {
                        continue;
                    }
                    var distance = dx * dx + dz * dz;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = (candidateX, candidateZ);
                    }
                }
            }
            if (best is not null)
            {
                return best;
            }
        }
        return null;
    }

    /// <summary>
    /// Octile A* constrained to a region inflated around start and goal. Returns the
    /// cell path including both endpoints, or null when no route exists within the
    /// expansion budget.
    /// </summary>
    public List<(int X, int Z)>? FindPath(
        ISquadNavProbe probe,
        int startX,
        int startZ,
        int goalX,
        int goalZ,
        int expansionCap = DefaultExpansionCap)
    {
        if (expansionCap <= 0
            || !Contains(startX, startZ)
            || !Contains(goalX, goalZ)
            || (startX == goalX && startZ == goalZ))
        {
            return null;
        }
        var minX = Math.Max(0, Math.Min(startX, goalX) - _inflationCells);
        var maxX = Math.Min(_width - 1, Math.Max(startX, goalX) + _inflationCells);
        var minZ = Math.Max(0, Math.Min(startZ, goalZ) - _inflationCells);
        var maxZ = Math.Min(_height - 1, Math.Max(startZ, goalZ) + _inflationCells);
        var startKey = CellKey(startX, startZ);
        var goalKey = CellKey(goalX, goalZ);
        var gScore = new Dictionary<long, float> { [startKey] = 0.0f };
        var cameFrom = new Dictionary<long, long>();
        var closed = new HashSet<long>();
        var open = new MinHeap();
        open.Push(Heuristic(startX, startZ, goalX, goalZ), startKey);
        var expansions = 0;
        while (open.Count > 0)
        {
            var current = open.Pop();
            if (!closed.Add(current.Cell))
            {
                continue;
            }
            if (current.Cell == goalKey)
            {
                return ReconstructPath(cameFrom, goalKey, startKey);
            }
            if (++expansions > expansionCap)
            {
                return null;
            }
            var currentX = (int)(current.Cell / _height);
            var currentZ = (int)(current.Cell % _height);
            for (var direction = 0; direction < 8; direction++)
            {
                var stepX = StepX[direction];
                var stepZ = StepZ[direction];
                var nextX = currentX + stepX;
                var nextZ = currentZ + stepZ;
                if (nextX < minX || nextX > maxX || nextZ < minZ || nextZ > maxZ)
                {
                    continue;
                }
                var nextKey = CellKey(nextX, nextZ);
                if (closed.Contains(nextKey) || !probe.IsCellWalkable(nextX, nextZ))
                {
                    continue;
                }
                if (stepX != 0 && stepZ != 0)
                {
                    // No corner cutting: both orthogonal cells and edges must be clear.
                    var orthogonalX = currentX + stepX;
                    var orthogonalZ = currentZ + stepZ;
                    if (!Contains(orthogonalX, currentZ)
                        || !Contains(currentX, orthogonalZ)
                        || !probe.IsCellWalkable(orthogonalX, currentZ)
                        || !probe.IsCellWalkable(currentX, orthogonalZ)
                        || !probe.IsEdgeClear(currentX, currentZ, orthogonalX, currentZ)
                        || !probe.IsEdgeClear(currentX, currentZ, currentX, orthogonalZ))
                    {
                        continue;
                    }
                }
                if (!probe.IsEdgeClear(currentX, currentZ, nextX, nextZ))
                {
                    continue;
                }
                var stepCost = stepX != 0 && stepZ != 0 ? DiagonalStepCost : 1.0f;
                var tentative = gScore[current.Cell] + stepCost;
                if (gScore.TryGetValue(nextKey, out var existing) && tentative + 1e-4f >= existing)
                {
                    continue;
                }
                gScore[nextKey] = tentative;
                cameFrom[nextKey] = current.Cell;
                open.Push(tentative + Heuristic(nextX, nextZ, goalX, goalZ), nextKey);
            }
        }
        return null;
    }

    private long CellKey(int x, int z) => (long)x * _height + z;

    private static float Heuristic(int x, int z, int goalX, int goalZ)
    {
        var dx = Math.Abs(x - goalX);
        var dz = Math.Abs(z - goalZ);
        return Math.Min(dx, dz) * DiagonalStepCost + Math.Abs(dx - dz);
    }

    private List<(int X, int Z)> ReconstructPath(
        Dictionary<long, long> cameFrom,
        long goalKey,
        long startKey)
    {
        var path = new List<(int X, int Z)>();
        var current = goalKey;
        while (true)
        {
            path.Add(((int)(current / _height), (int)(current % _height)));
            if (current == startKey)
            {
                break;
            }
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }

    private readonly struct Entry
    {
        public readonly float Priority;
        public readonly long Cell;
        public readonly int Order;

        public Entry(float priority, long cell, int order)
        {
            Priority = priority;
            Cell = cell;
            Order = order;
        }
    }

    /// <summary>Array-backed binary min-heap; insertion order breaks priority ties.</summary>
    private sealed class MinHeap
    {
        private Entry[] _items = new Entry[256];
        private int _order;

        public int Count { get; private set; }

        public void Push(float priority, long cell)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, _items.Length * 2);
            }
            _items[Count] = new Entry(priority, cell, _order++);
            SiftUp(Count);
            Count++;
        }

        public Entry Pop()
        {
            var top = _items[0];
            Count--;
            _items[0] = _items[Count];
            SiftDown(0);
            return top;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (Compare(_items[index], _items[parent]) >= 0)
                {
                    break;
                }
                (_items[index], _items[parent]) = (_items[parent], _items[index]);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                var left = index * 2 + 1;
                var right = left + 1;
                var smallest = index;
                if (left < Count && Compare(_items[left], _items[smallest]) < 0)
                {
                    smallest = left;
                }
                if (right < Count && Compare(_items[right], _items[smallest]) < 0)
                {
                    smallest = right;
                }
                if (smallest == index)
                {
                    return;
                }
                (_items[index], _items[smallest]) = (_items[smallest], _items[index]);
                index = smallest;
            }
        }

        private static int Compare(Entry left, Entry right)
        {
            var byPriority = left.Priority.CompareTo(right.Priority);
            return byPriority != 0 ? byPriority : left.Order.CompareTo(right.Order);
        }
    }
}
