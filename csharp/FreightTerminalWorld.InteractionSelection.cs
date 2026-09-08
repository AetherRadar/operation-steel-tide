using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // Selection runs every render frame while the loot field itself is mostly
    // static. Keep the broad scans on a short cadence and reuse a nearby result.
    private const ulong InteractionSelectionCacheLifetimeUsec = 75_000;
    private const float InteractionSelectionCacheMovementSquared = 0.25f;
    private static readonly float[] InteractionTargetHeights = { 0.72f, 1.16f };
    private static readonly float[] LootInteractionTargetHeights = { 0.28f, 0.72f, 1.16f };

    private bool _nearestCivilianCacheReady;
    private ulong _nearestCivilianCacheTimestampUsec;
    private Vector3 _nearestCivilianCacheOrigin;
    private float _nearestCivilianCacheRange;
    private int _nearestCivilianCacheCount;
    private CivilianNpc? _nearestCivilianCacheTarget;

    private bool _nearestLootCacheReady;
    private ulong _nearestLootCacheTimestampUsec;
    private Vector3 _nearestLootCacheOrigin;
    private float _nearestLootCacheRange;
    private int _nearestLootCacheCount;
    private ILootSource? _nearestLootCacheTarget;

    private CivilianNpc? FindNearestAssistableCivilian(
        Vector3 origin,
        float range,
        out float distance,
        bool forceRefresh = false)
    {
        var nowUsec = Time.GetTicksUsec();
        if (!forceRefresh
            && IsInteractionSelectionCacheFresh(
                _nearestCivilianCacheReady,
                _nearestCivilianCacheTimestampUsec,
                _nearestCivilianCacheOrigin,
                _nearestCivilianCacheRange,
                _nearestCivilianCacheCount,
                origin,
                range,
                _civilians.Count,
                nowUsec)
            && (_nearestCivilianCacheTarget is null
                || IsCachedCivilianUsable(_nearestCivilianCacheTarget, origin, range)))
        {
            distance = _nearestCivilianCacheTarget is null
                ? float.PositiveInfinity
                : Mathf.Sqrt(origin.DistanceSquaredTo(_nearestCivilianCacheTarget.GlobalPosition));
            return _nearestCivilianCacheTarget;
        }

        CivilianNpc? nearest = null;
        var nearestDistanceSquared = range * range;
        foreach (var civilian in _civilians)
        {
            if (!IsInstanceValid(civilian) || !civilian.CanOfferAssistance)
            {
                continue;
            }
            var distanceSquared = origin.DistanceSquaredTo(civilian.GlobalPosition);
            if (distanceSquared >= nearestDistanceSquared
                || !HasClearPlayerCivilianInteractionLineOfSight(civilian))
            {
                continue;
            }
            nearest = civilian;
            nearestDistanceSquared = distanceSquared;
        }
        _nearestCivilianCacheReady = true;
        _nearestCivilianCacheTimestampUsec = nowUsec;
        _nearestCivilianCacheOrigin = origin;
        _nearestCivilianCacheRange = range;
        _nearestCivilianCacheCount = _civilians.Count;
        _nearestCivilianCacheTarget = nearest;
        distance = nearest is null
            ? float.PositiveInfinity
            : Mathf.Sqrt(nearestDistanceSquared);
        return nearest;
    }

    private ILootSource? FindNearestInteractiveLoot(
        Vector3 origin,
        float range,
        out float distance,
        bool forceRefresh = false)
    {
        var nowUsec = Time.GetTicksUsec();
        if (!forceRefresh
            && IsInteractionSelectionCacheFresh(
                _nearestLootCacheReady,
                _nearestLootCacheTimestampUsec,
                _nearestLootCacheOrigin,
                _nearestLootCacheRange,
                _nearestLootCacheCount,
                origin,
                range,
                _lootSources.Count,
                nowUsec)
            && (_nearestLootCacheTarget is null
                || IsCachedLootUsable(_nearestLootCacheTarget, origin, range)))
        {
            distance = _nearestLootCacheTarget is null
                ? float.PositiveInfinity
                : Mathf.Sqrt(origin.DistanceSquaredTo(_nearestLootCacheTarget.LootNode.GlobalPosition));
            return _nearestLootCacheTarget;
        }

        ILootSource? nearest = null;
        var nearestDistanceSquared = range * range;
        foreach (var source in _lootSources)
        {
            if (!IsInstanceValid(source.LootNode) || !source.IsSearchable)
            {
                continue;
            }
            var distanceSquared = origin.DistanceSquaredTo(source.LootNode.GlobalPosition);
            if (distanceSquared >= nearestDistanceSquared
                || !HasClearPlayerLootInteractionLineOfSight(source))
            {
                continue;
            }
            nearest = source;
            nearestDistanceSquared = distanceSquared;
        }
        _nearestLootCacheReady = true;
        _nearestLootCacheTimestampUsec = nowUsec;
        _nearestLootCacheOrigin = origin;
        _nearestLootCacheRange = range;
        _nearestLootCacheCount = _lootSources.Count;
        _nearestLootCacheTarget = nearest;
        distance = nearest is null
            ? float.PositiveInfinity
            : Mathf.Sqrt(nearestDistanceSquared);
        return nearest;
    }

    private static bool IsInteractionSelectionCacheFresh(
        bool ready,
        ulong timestampUsec,
        Vector3 cachedOrigin,
        float cachedRange,
        int cachedCount,
        Vector3 origin,
        float range,
        int currentCount,
        ulong nowUsec)
    {
        if (!ready || cachedCount != currentCount
            || !Mathf.IsEqualApprox(cachedRange, range)
            || cachedOrigin.DistanceSquaredTo(origin) > InteractionSelectionCacheMovementSquared)
        {
            return false;
        }

        return nowUsec >= timestampUsec
            && nowUsec - timestampUsec <= InteractionSelectionCacheLifetimeUsec;
    }

    private static bool IsCachedCivilianUsable(
        CivilianNpc civilian,
        Vector3 origin,
        float range)
    {
        return IsInstanceValid(civilian)
            && civilian.CanOfferAssistance
            && origin.DistanceSquaredTo(civilian.GlobalPosition) < range * range;
    }

    private static bool IsCachedLootUsable(
        ILootSource source,
        Vector3 origin,
        float range)
    {
        return IsInstanceValid(source.LootNode)
            && source.IsSearchable
            && origin.DistanceSquaredTo(source.LootNode.GlobalPosition) < range * range;
    }

    private bool IsLootInteractionTargetStillValid(ILootSource source, float range)
    {
        return IsCachedLootUsable(source, _player.GlobalPosition, range)
            && HasClearPlayerLootInteractionLineOfSight(source);
    }

    private void InvalidateInteractionSelectionCaches()
    {
        _nearestCivilianCacheReady = false;
        _nearestLootCacheReady = false;
        _nearestCivilianCacheTarget = null;
        _nearestLootCacheTarget = null;
    }

    private bool HasClearPlayerCivilianInteractionLineOfSight(CivilianNpc civilian)
    {
        if (!IsInstanceValid(civilian) || !IsInstanceValid(_player))
        {
            return false;
        }

        var from = _player.GlobalPosition + Vector3.Up * 1.25f;
        foreach (var targetHeight in InteractionTargetHeights)
        {
            if (!PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    from,
                    civilian.GlobalPosition + Vector3.Up * targetHeight,
                    _player.GetRid(),
                    civilian.GetRid(),
                    1))
            {
                return true;
            }
        }
        return false;
    }
}
