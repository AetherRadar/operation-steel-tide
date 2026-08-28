using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ApplyRemoteMeleeHitWhenReady(PendingRemoteMeleeHit pending)
    {
        var now = Time.GetTicksMsec();
        if (pending.ExecuteAt > now)
        {
            var delay = (pending.ExecuteAt - now) / 1000.0f;
            await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
        }
        if (IsInstanceValid(this) && IsInsideTree())
        {
            ApplyAcceptedRemoteMeleeHit(pending);
        }
    }

    private void ApplyAcceptedRemoteMeleeHit(PendingRemoteMeleeHit pending)
    {
        if (!PendingRemoteMeleeHitAuthorized(pending)
            || _demolitionMode
                && (!_demolitionRoundActive
                    || _demolitionNetworkPhase != DemolitionNetworkPhase.Live)
            || pending.CombatEpoch != CurrentMeleeCombatEpoch
            || !KnifeSkinCatalog.TryDefinition(pending.DefinitionId, out var definition)
            || !TryResolveRemoteMeleeActors(
                pending.PeerId,
                pending.TargetId,
                out var shooter,
                out var target)
            || !RemoteMeleeGeometryValid(
                pending.PeerId,
                pending.SwingSequence,
                shooter!,
                target!,
                pending.ReportedOrigin,
                pending.ReportedHitPoint,
                definition))
        {
            return;
        }

        var attack = MeleeAttackCatalog.AttackFor(definition.Style, pending.AttackIndex);
        var canonicalHitPoint = MeleeTargetPoint(target!);
        var damage = HostMeleeDamage(
            definition,
            attack,
            shooter!,
            target!,
            allowBackstab: _demolitionMode || IsExtractionNetworkMatch);
        var killed = false;
        var armorHit = false;
        if (_demolitionMode)
        {
            ApplyDemolitionNetworkDamage(
                pending.TargetId,
                damage,
                canonicalHitPoint,
                shooter,
                melee: true);
            killed = target switch
            {
                TacticalPlayer player => player.IsDead,
                SquadMate mate => mate.CombatDead,
                EnemyOperator enemy => enemy.IsDead,
                _ => false
            };
            armorHit = target switch
            {
                TacticalPlayer player => player.LastHitWasArmored,
                EnemyOperator enemy => enemy.LastHitWasArmored,
                _ => false
            };
        }
        else if (target is EnemyOperator enemy)
        {
            killed = enemy.TakeDamage(damage, canonicalHitPoint, shooter);
            armorHit = enemy.LastHitWasArmored;
        }
        else
        {
            return;
        }
        _squadNetwork.BroadcastMeleeHitConfirmation(
            pending.PeerId,
            canonicalHitPoint,
            pending.TargetId,
            damage,
            definition.Id,
            pending.AttackIndex,
            pending.SwingSequence,
            killed,
            armorHit,
            pending.CombatEpoch);
    }

    private bool PendingRemoteMeleeHitAuthorized(PendingRemoteMeleeHit pending)
    {
        var key = new RemoteMeleeSwingKey(pending.PeerId, pending.SwingSequence);
        return _remoteMeleeSwings.TryGetValue(key, out var state)
            && ReferenceEquals(state, pending.AuthorizationState)
            && state.TargetIds.Contains(pending.TargetId)
            && state.AttackIndex == pending.AttackIndex
            && string.Equals(
                state.DefinitionId,
                pending.DefinitionId,
                System.StringComparison.OrdinalIgnoreCase);
    }
}
