using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool HasSquadReviveLineOfSight(SquadMate reviver, ISquadCombatant target)
    {
        if (!IsInstanceValid(reviver) || !IsInstanceValid(target.CombatNode))
        {
            return false;
        }

        var targetNode = target.CombatNode;
        var exclude = new Godot.Collections.Array<Rid> { reviver.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (targetNode is CollisionObject3D collisionTarget)
        {
            exclude.Add(collisionTarget.GetRid());
        }
        var from = reviver.GlobalPosition + Vector3.Up * 0.78f;
        var to = targetNode.GlobalPosition + Vector3.Up * 0.62f;
        return !PhysicsRaycast.HasHit(GetWorld3D(), from, to, exclude, 1);
    }
}
