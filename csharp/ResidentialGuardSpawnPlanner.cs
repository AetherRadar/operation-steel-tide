using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct ResidentialGuardSpawnLayout(
    Vector3 Target,
    Vector3[] SpawnPositions,
    bool UsesResolvedGeometry);

/// <summary>
/// Resolves one residential cache ambush against an explicitly supplied physics snapshot.
/// Instances are scoped to a single cache plan and retain no scene nodes or subscriptions.
/// </summary>
internal sealed class ResidentialGuardSpawnPlanner : System.IDisposable
{
    private const int CandidateDirectionCount = 16;
    private const int MaximumBacktrackingSteps = 4096;
    private const int MaximumResolvedGuardCount = 4;
    private const float MaximumPreferredTargetDistance = 12.0f;
    private const float MaximumRouteDistance = 18.0f;
    private const float MinimumTargetSeparation = 1.2f;
    private const float MinimumGuardSeparation = 1.15f;

    private static readonly float[] SpawnCandidateDistances =
        { 1.65f, 2.25f, 2.95f, 3.75f, 4.65f, 5.55f };
    private static readonly float[] TargetCandidateDistances =
        { 1.25f, 1.75f, 2.3f, 3.0f, 3.8f, 4.8f };

    private readonly PhysicsDirectSpaceState3D _space;
    private readonly Transform3D _cacheTransform;
    private readonly Godot.Collections.Array<Rid> _groundRayExclude = new();
    private readonly Godot.Collections.Array<Rid> _clearanceExclude = new();
    private readonly CapsuleShape3D _clearanceShape = new()
    {
        Radius = 0.38f,
        Height = 1.78f
    };

    public ResidentialGuardSpawnPlanner(
        PhysicsDirectSpaceState3D space,
        Transform3D cacheTransform,
        Rid cacheRid,
        IEnumerable<Rid> additionalExclude)
    {
        _space = space;
        _cacheTransform = cacheTransform;
        _groundRayExclude.Add(cacheRid);
        foreach (var rid in additionalExclude)
        {
            if (!_groundRayExclude.Contains(rid))
            {
                _groundRayExclude.Add(rid);
            }
            if (!rid.Equals(cacheRid) && !_clearanceExclude.Contains(rid))
            {
                _clearanceExclude.Add(rid);
            }
        }
    }

    public ResidentialGuardSpawnLayout Plan(int count, Vector3 preferredTarget)
    {
        if ((HorizontalDistance(_cacheTransform.Origin, preferredTarget) <= MaximumPreferredTargetDistance
                && TryResolveLayout(count, preferredTarget, out var layout))
            || TryFindLayout(count, out layout))
        {
            return layout;
        }

        var fallbackPositions = new Vector3[Mathf.Max(0, count)];
        for (var index = 0; index < fallbackPositions.Length; index++)
        {
            fallbackPositions[index] = FallbackSpawnPosition(index);
        }
        return new ResidentialGuardSpawnLayout(
            preferredTarget,
            fallbackPositions,
            UsesResolvedGeometry: false);
    }

    public bool TryFindLayout(int count, out ResidentialGuardSpawnLayout layout)
    {
        foreach (var distance in TargetCandidateDistances)
        {
            for (var directionIndex = 0; directionIndex < CandidateDirectionCount; directionIndex++)
            {
                var candidateTarget = ToGlobal(RadialOffset(distance, directionIndex));
                if (TryResolveLayout(count, candidateTarget, out layout))
                {
                    return true;
                }
            }
        }

        layout = default;
        return false;
    }

    public bool TryResolveLayout(
        int count,
        Vector3 requestedTarget,
        out ResidentialGuardSpawnLayout layout)
    {
        if (count <= 0)
        {
            layout = new ResidentialGuardSpawnLayout(
                requestedTarget,
                System.Array.Empty<Vector3>(),
                UsesResolvedGeometry: true);
            return true;
        }
        if (count > MaximumResolvedGuardCount
            || !TryGroundPosition(requestedTarget, out var target))
        {
            layout = default;
            return false;
        }

        var candidates = new List<Vector3>(
            SpawnCandidateDistances.Length * CandidateDirectionCount);
        foreach (var distance in SpawnCandidateDistances)
        {
            for (var directionIndex = 0; directionIndex < CandidateDirectionCount; directionIndex++)
            {
                var probe = ToGlobal(RadialOffset(distance, directionIndex));
                if (TryGroundPosition(probe, out var candidate)
                    && HorizontalDistance(candidate, target) >= MinimumTargetSeparation
                    && HasRoute(candidate, target))
                {
                    candidates.Add(candidate);
                }
            }
        }

        var selected = new List<Vector3>(count);
        var searchBudget = MaximumBacktrackingSteps;
        if (!TrySelectSeparatedCandidates(candidates, count, 0, selected, ref searchBudget))
        {
            layout = default;
            return false;
        }

        layout = new ResidentialGuardSpawnLayout(
            target,
            selected.ToArray(),
            UsesResolvedGeometry: true);
        return true;
    }

    public bool TryGroundPosition(Vector3 position, out Vector3 feet)
    {
        if (!PhysicsRaycast.TryHit(
                _space,
                position + Vector3.Up * 0.75f,
                position + Vector3.Down * 0.9f,
                _groundRayExclude,
                1,
                out var floorHit)
            || floorHit.Normal.Dot(Vector3.Up) < 0.72f)
        {
            feet = Vector3.Zero;
            return false;
        }

        feet = floorHit.Position + Vector3.Up * 0.03f;
        if (Mathf.Abs(feet.Y - position.Y) > 0.42f)
        {
            return false;
        }

        using var clearanceQuery = new PhysicsShapeQueryParameters3D
        {
            Shape = _clearanceShape,
            Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * 0.89f),
            CollisionMask = 1,
            CollideWithBodies = true,
            CollideWithAreas = false,
            Exclude = _clearanceExclude
        };
        return !PhysicsShapeProbe.HasCollision(_space, clearanceQuery, 1);
    }

    public void Dispose()
    {
        _clearanceShape.Dispose();
        _groundRayExclude.AsDisposable().Dispose();
        _clearanceExclude.AsDisposable().Dispose();
    }

    public bool HasRoute(Vector3 start, Vector3 destination)
    {
        var flatDistance = HorizontalDistance(start, destination);
        if (flatDistance > MaximumRouteDistance)
        {
            return false;
        }
        var samples = Mathf.Max(1, Mathf.CeilToInt(flatDistance / 0.55f));
        for (var sample = 0; sample <= samples; sample++)
        {
            var position = start.Lerp(destination, sample / (float)samples);
            if (!TryGroundPosition(position, out _))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TrySelectSeparatedCandidates(
        IReadOnlyList<Vector3> candidates,
        int requiredCount,
        int startIndex,
        List<Vector3> selected,
        ref int searchBudget)
    {
        if (selected.Count == requiredCount)
        {
            return true;
        }
        if (candidates.Count - startIndex < requiredCount - selected.Count)
        {
            return false;
        }

        for (var index = startIndex; index < candidates.Count && searchBudget > 0; index++)
        {
            searchBudget--;
            var candidate = candidates[index];
            var separated = true;
            foreach (var existing in selected)
            {
                if (HorizontalDistance(existing, candidate) < MinimumGuardSeparation)
                {
                    separated = false;
                    break;
                }
            }
            if (!separated)
            {
                continue;
            }

            selected.Add(candidate);
            if (TrySelectSeparatedCandidates(
                    candidates,
                    requiredCount,
                    index + 1,
                    selected,
                    ref searchBudget))
            {
                return true;
            }
            selected.RemoveAt(selected.Count - 1);
        }
        return false;
    }

    private Vector3 ToGlobal(Vector3 localPosition)
        => _cacheTransform * localPosition;

    private Vector3 FallbackSpawnPosition(int index)
        => ToGlobal(new Vector3(
            index == 0 ? -0.75f : 0.75f,
            0.05f,
            -1.85f - index * 0.28f));

    private static Vector3 RadialOffset(float distance, int directionIndex)
    {
        var angle = Mathf.Tau * directionIndex / CandidateDirectionCount;
        return new Vector3(
            Mathf.Sin(angle) * distance,
            0.05f,
            -Mathf.Cos(angle) * distance);
    }

    private static float HorizontalDistance(Vector3 left, Vector3 right)
        => new Vector2(left.X - right.X, left.Z - right.Z).Length();
}
