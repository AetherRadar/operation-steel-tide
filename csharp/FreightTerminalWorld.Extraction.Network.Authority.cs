using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void OnExtractionObjectiveRequested(long peerId, int objectiveStage)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || objectiveStage != _objectiveStage
            || objectiveStage < 0 || objectiveStage >= _objectiveTerminals.Count)
        {
            return;
        }
        Node3D? actor = peerId == 1
            ? _player
            : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (!IsInstanceValid(actor)
            || actor!.GlobalPosition.DistanceTo(_objectiveTerminals[objectiveStage].GlobalPosition) >= 2.9f)
        {
            return;
        }
        CompleteCurrentObjective();
    }

    private void OnExtractionReviveRequested(long peerId, int targetSlot)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost)
        {
            return;
        }
        var actor = ResolveExtractionPeerActor(peerId);
        ISquadCombatant? target = targetSlot == 0
            ? _player
            : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate.SquadSlot == targetSlot);
        if (!IsInstanceValid(actor) || target is null || !target.CanBeRevived
            || !IsInstanceValid(target.CombatNode)
            || ReferenceEquals(actor, target.CombatNode)
            || actor!.GlobalPosition.DistanceTo(target.CombatNode.GlobalPosition) > 2.9f
            || Mathf.Abs(actor.GlobalPosition.Y - target.CombatNode.GlobalPosition.Y) > 1.25f
            || !HasExtractionReviveLineOfSight(actor, target.CombatNode))
        {
            return;
        }
        if (target.TryReceiveRevive(62.0f))
        {
            ResetAiReviveAbandonment();
            BroadcastExtractionWorldSnapshot();
        }
    }

    private bool HasExtractionReviveLineOfSight(Node3D actor, Node3D target)
    {
        var exclude = new Godot.Collections.Array<Rid>();
        using var excludeBacking = exclude.AsDisposable();
        if (actor is CollisionObject3D actorCollision)
        {
            exclude.Add(actorCollision.GetRid());
        }
        if (target is CollisionObject3D targetCollision)
        {
            exclude.Add(targetCollision.GetRid());
        }
        return !PhysicsRaycast.HasHit(
            GetWorld3D(),
            actor.GlobalPosition + Vector3.Up * 0.78f,
            target.GlobalPosition + Vector3.Up * 0.62f,
            exclude,
            1);
    }

    private bool IsExtractionRemoteShotClear(
        SquadMate shooter,
        EnemyOperator target,
        Vector3 origin,
        Vector3 end)
    {
        var exclude = new Godot.Collections.Array<Rid> { shooter.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (!PhysicsRaycast.TryHit(GetWorld3D(), origin, end, exclude, 1 | 2, out var hit))
        {
            return end.DistanceTo(target.GlobalPosition + Vector3.Up) <= 1.8f;
        }
        return hit.Collider == target;
    }
}
