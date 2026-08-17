using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    /// <summary>All living operators are valid targets for the unaffiliated hostile aircraft.</summary>
    public IEnumerable<Node3D> GetAircraftCombatants()
    {
        if (!IsAircraftCombatAllowed())
        {
            yield break;
        }
        if (IsInstanceValid(_player)
            && _player.IsInsideTree()
            && !_player.IsDead
            && !_player.IsExtractionPassenger)
        {
            yield return _player;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate)
                && mate.IsInsideTree()
                && !mate.IsDowned
                && !mate.IsBodyBag
                && !mate.IsExtractionPassenger)
            {
                yield return mate;
            }
        }
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy) && enemy.IsInsideTree() && !enemy.IsDead)
            {
                yield return enemy;
            }
        }
    }

    public bool IsActiveAircraftCombatant(Node3D actor)
    {
        if (!IsAircraftCombatAllowed()
            || !IsInstanceValid(actor)
            || !actor.IsInsideTree())
        {
            return false;
        }
        if (ReferenceEquals(actor, _player))
        {
            return IsInstanceValid(_player)
                && !_player.IsDead
                && !_player.IsExtractionPassenger;
        }
        if (actor is SquadMate mate)
        {
            return _squadMates.Contains(mate)
                && !mate.IsDowned
                && !mate.IsBodyBag
                && !mate.IsExtractionPassenger;
        }
        return actor is EnemyOperator enemy
            && _enemies.Contains(enemy)
            && !enemy.IsDead;
    }

    public bool CanAircraftPassivelyDetectOperators()
    {
        return IsAircraftCombatAllowed()
            && !_missionDirector.IsDeploymentProtected();
    }

    private bool IsAircraftCombatAllowed()
    {
        return !_missionEnded && !_demolitionMode;
    }

    public void NotifyAircraftOperatorAttack(Node3D actor, Vector3 origin, float soundRadius)
    {
        if (!IsActiveAircraftCombatant(actor)
            || !IsInstanceValid(_aircraft)
            || _aircraft!.IsDestroyed)
        {
            return;
        }
        _aircraft.RegisterOperatorAttack(actor, origin, soundRadius);
    }
}
