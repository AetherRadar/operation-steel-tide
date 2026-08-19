using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private bool HandleDemolitionRemoteAbility(
        long peerId,
        OperatorRole role,
        Vector3 origin,
        Vector3 forward)
    {
        if (!_demolitionMode)
        {
            return false;
        }
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && mate.IsHumanProxy
            && mate.NetworkPeerId == peerId);
        var opponentProxy = _remoteDemolitionOpponents.GetValueOrDefault(peerId);
        var actor = proxy as Node3D;
        if (!IsInstanceValid(actor) && IsInstanceValid(opponentProxy))
        {
            actor = opponentProxy;
        }
        if (!_demolitionRoundActive || !IsInstanceValid(actor)
            || actor!.GlobalPosition.DistanceTo(origin) > 4.5f)
        {
            return true;
        }
        if (proxy is not null)
        {
            proxy.TriggerRemoteRoleAbility(origin, forward, _squadNetwork.IsHost);
            return true;
        }
        if (opponentProxy is null)
        {
            return true;
        }
        SpawnRoleActivationPulse(
            opponentProxy.GlobalPosition + Vector3.Up,
            OperatorRoles.Spec(role).Accent,
            2.6f);
        if (_squadNetwork.IsHost && role == OperatorRole.Medic)
        {
            ApplyDemolitionOpponentMedicSpray(opponentProxy, origin, forward);
        }
        return true;
    }

    private void ApplyDemolitionOpponentMedicSpray(
        EnemyOperator source,
        Vector3 origin,
        Vector3 forward)
    {
        var normalizedForward = forward.Normalized();
        EnemyOperator? target = null;
        var bestScore = float.PositiveInfinity;
        foreach (var friendly in _demolitionOpponents.Where(IsInstanceValid))
        {
            if (friendly.IsDead)
            {
                continue;
            }
            var offset = friendly.GlobalPosition - origin;
            var distance = offset.Length();
            if (friendly == source)
            {
                var selfRatio = friendly.CurrentHealth / Mathf.Max(1.0f, friendly.MaxHealth);
                if (selfRatio < 0.96f)
                {
                    target = friendly;
                    bestScore = selfRatio * 4.0f + 2.0f;
                }
                continue;
            }
            if (distance > 8.0f)
            {
                continue;
            }
            var alignment = distance <= 0.01f ? 1.0f : normalizedForward.Dot(offset / distance);
            if (alignment < 0.3f)
            {
                continue;
            }
            var ratio = friendly.CurrentHealth / Mathf.Max(1.0f, friendly.MaxHealth);
            var score = ratio * 4.0f + distance * 0.08f - alignment;
            if (ratio < 0.99f && score < bestScore)
            {
                target = friendly;
                bestScore = score;
            }
        }
        target ??= source;
        target.RestoreDemolitionNetworkHealth(44.0f);
        if (target != source)
        {
            source.RestoreDemolitionNetworkHealth(18.0f);
        }
        SpawnMedicSprayEffect(origin, target.GlobalPosition + Vector3.Up);
    }
}
