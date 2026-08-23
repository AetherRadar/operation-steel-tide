using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly CombatTargetSpatialIndex _combatTargetSpatialIndex = new();
    private readonly CombatContactRelay _combatContactRelay = new();

    internal int CombatTargetIndexRefreshCountForDiagnostics => _combatTargetSpatialIndex.RefreshCount;
    internal int CombatContactBroadcastCountForDiagnostics => _combatContactRelay.BroadcastCount;
    internal int CombatContactRecipientVisitCountForDiagnostics => _combatContactRelay.RecipientVisitCount;

    internal void CollectHostileTargetsFor(
        EnemyOperator self,
        float range,
        List<Node3D> results)
    {
        results.Clear();
        if (IsPlayerProtected() && !self.IsRivalSquad && !self.BypassesPlayerProtectionForDiagnostics)
        {
            return;
        }

        if (IsInstanceValid(_player)
            && (!_player.IsDead || _player.CombatDowned && _player.CanBeRevived))
        {
            results.Add(_player);
        }
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || mate.IsBodyBag)
            {
                continue;
            }
            if (!mate.IsDowned || mate.CombatDowned && mate.CanBeRevived)
            {
                results.Add(mate);
            }
        }

        _combatTargetSpatialIndex.CollectHostileOperators(
            _enemies,
            self,
            self.GlobalPosition,
            range,
            results);
    }

    internal void InvalidateCombatTargetIndex()
    {
        _combatTargetSpatialIndex.Invalidate();
    }

    internal bool ShouldUseReducedCivilianSimulation(Vector3 position)
        => IsInstanceValid(_player)
            && position.DistanceSquaredTo(_player.GlobalPosition) > 72.0f * 72.0f;

    internal void RelayOperatorContact(
        EnemyOperator source,
        Node3D target,
        Vector3 position,
        float range)
    {
        var physicsFrame = Engine.GetPhysicsFrames();
        if (!_combatContactRelay.TryBeginBroadcast(
                source.TeamId,
                target.GetInstanceId(),
                physicsFrame))
        {
            return;
        }

        var rangeSquared = range * range;
        foreach (var ally in _enemies)
        {
            _combatContactRelay.RecordRecipientVisit();
            if (!IsInstanceValid(ally)
                || ally == source
                || ally.IsDead
                || ally.TeamId != source.TeamId
                || source.GlobalPosition.DistanceSquaredTo(ally.GlobalPosition) > rangeSquared)
            {
                continue;
            }
            ally.ReceiveSharedPursuitContact(target, position);
        }
    }

    internal void ResetCrowdCombatDiagnostics()
    {
        _combatTargetSpatialIndex.ResetDiagnostics();
        _combatContactRelay.ResetDiagnostics();
    }

    internal void InvalidateCombatContactRelayForDiagnostics(int teamId)
    {
        _combatContactRelay.InvalidateTeam(teamId);
    }
}
