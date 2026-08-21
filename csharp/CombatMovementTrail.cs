using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal interface ICombatMovementTrailSource
{
    CombatMovementTrail CombatMovementTrail { get; }
}

/// <summary>
/// Allocation-stable movement history shared by combat pursuit. Samples use a
/// monotonic sequence so cursors cannot follow overwritten or teleported spans.
/// </summary>
internal sealed class CombatMovementTrail
{
    private const float SampleSpacing = 0.65f;
    private const float TeleportResetDistance = 18.0f;
    private const int MaximumInterpolatedSamples = 24;
    private const int DefaultCapacity = 192;

    private readonly Vector3[] _points;
    private long _nextSequence;
    private int _count;
    private Vector3 _lastPosition;

    public CombatMovementTrail(int capacity = DefaultCapacity)
    {
        _points = new Vector3[Mathf.Max(16, capacity)];
    }

    public int Count => _count;
    public int Revision { get; private set; }
    public long OldestSequence => _nextSequence - _count;
    public long LatestSequence => _count == 0 ? -1 : _nextSequence - 1;

    public void Record(Vector3 position)
    {
        if (_count == 0)
        {
            Append(position);
            return;
        }

        var offset = position - _lastPosition;
        var distanceSquared = offset.LengthSquared();
        if (distanceSquared > TeleportResetDistance * TeleportResetDistance)
        {
            Reset(position);
            return;
        }
        if (distanceSquared < SampleSpacing * SampleSpacing)
        {
            return;
        }

        var distance = Mathf.Sqrt(distanceSquared);
        var samples = Mathf.Clamp(
            Mathf.CeilToInt(distance / SampleSpacing),
            1,
            MaximumInterpolatedSamples);
        var origin = _lastPosition;
        for (var sample = 1; sample <= samples; sample++)
        {
            Append(origin.Lerp(position, sample / (float)samples));
        }
    }

    public bool TryGet(long sequence, out Vector3 point)
    {
        if (sequence < OldestSequence || sequence >= _nextSequence)
        {
            point = default;
            return false;
        }
        point = _points[(int)(sequence % _points.Length)];
        return true;
    }

    internal void SetForDiagnostics(IReadOnlyList<Vector3> points)
    {
        _count = 0;
        Revision++;
        for (var index = 0; index < points.Count; index++)
        {
            Append(points[index]);
        }
    }

    private void Reset(Vector3 position)
    {
        _count = 0;
        Revision++;
        Append(position);
    }

    private void Append(Vector3 position)
    {
        _points[(int)(_nextSequence % _points.Length)] = position;
        _nextSequence++;
        _count = Mathf.Min(_count + 1, _points.Length);
        _lastPosition = position;
    }
}
