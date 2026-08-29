using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private CivilianNpc? FindNearestAssistableCivilian(
        Vector3 origin,
        float range,
        out float distance)
    {
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
        distance = nearest is null
            ? float.PositiveInfinity
            : Mathf.Sqrt(nearestDistanceSquared);
        return nearest;
    }

    private ILootSource? FindNearestInteractiveLoot(
        Vector3 origin,
        float range,
        out float distance)
    {
        ILootSource? nearest = null;
        var nearestDistanceSquared = range * range;
        foreach (var source in _lootSources)
        {
            if (!source.IsSearchable || !IsInstanceValid(source.LootNode))
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
        distance = nearest is null
            ? float.PositiveInfinity
            : Mathf.Sqrt(nearestDistanceSquared);
        return nearest;
    }

    private bool HasClearPlayerCivilianInteractionLineOfSight(CivilianNpc civilian)
    {
        if (!IsInstanceValid(civilian) || !IsInstanceValid(_player))
        {
            return false;
        }

        var exclude = new Godot.Collections.Array<Rid>
        {
            _player.GetRid(),
            civilian.GetRid()
        };
        using var excludeBacking = exclude.AsDisposable();
        var from = _player.GlobalPosition + Vector3.Up * 1.25f;
        foreach (var targetHeight in new[] { 0.72f, 1.16f })
        {
            if (!PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    from,
                    civilian.GlobalPosition + Vector3.Up * targetHeight,
                    exclude,
                    1))
            {
                return true;
            }
        }
        return false;
    }
}
