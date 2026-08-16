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
        if (targetNode is CollisionObject3D collisionTarget)
        {
            exclude.Add(collisionTarget.GetRid());
        }
        var from = reviver.GlobalPosition + Vector3.Up * 0.78f;
        var to = targetNode.GlobalPosition + Vector3.Up * 0.62f;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1;
        query.CollideWithAreas = false;
        query.Exclude = exclude;
        return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }
}
