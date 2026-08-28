using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct RemoteMeleeSwingKey(long PeerId, int Sequence);
    private readonly record struct RemoteMeleePeerSwing(
        int Sequence,
        string DefinitionId,
        int AttackIndex,
        long ClientStartedAtMsec,
        int CombatEpoch,
        ulong HostReceivedAt,
        ulong HostEffectiveStartedAt,
        float Duration,
        float HitProgress);

    private readonly record struct PendingRemoteMeleeHit(
        long PeerId,
        Vector3 ReportedOrigin,
        Vector3 ReportedHitPoint,
        int TargetId,
        string DefinitionId,
        int AttackIndex,
        int SwingSequence,
        int CombatEpoch,
        ulong ExecuteAt,
        RemoteMeleeSwingState AuthorizationState);

    private sealed class RemoteMeleeSwingState
    {
        public required string DefinitionId { get; init; }
        public required int AttackIndex { get; init; }
        public required int MaximumTargets { get; init; }
        public required ulong AcceptedAt { get; init; }
        public required ulong ExecuteAt { get; init; }
        public required ulong AdditionalTargetsUntil { get; init; }
        public HashSet<int> TargetIds { get; } = new();
    }

    private readonly Dictionary<long, string> _remoteMeleeDefinitionByPeer = new();
    private readonly Dictionary<RemoteMeleeSwingKey, RemoteMeleeSwingState>
        _remoteMeleeSwings = new();
    private readonly Dictionary<long, RemoteMeleePeerSwing>
        _lastRemoteMeleeSwingByPeer = new();
    private readonly Dictionary<long, ulong> _remoteMeleeNextStartByPeer = new();
    private int _remoteNetworkMeleeRequestCount;
    private int _remoteNetworkMeleeConfirmationCount;
    private bool _remoteNetworkMeleeFeedbackFlagsReceived;

    public bool ShouldApplyLocalMeleeDamage
        => !IsInstanceValid(_squadNetwork)
            || !_squadNetwork.IsOnline
            || _squadNetwork.IsHost;

    private int CurrentMeleeCombatEpoch
        => _demolitionMode ? _demolitionMatch.CurrentRound : 0;

    public void OnLocalPlayerMeleeHit(
        Vector3 origin,
        Vector3 hitPoint,
        int targetId,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientHitAtMsec,
        float authoritativeDamage,
        bool killed,
        bool armorHit)
    {
        if (!IsInstanceValid(_squadNetwork) || !_squadNetwork.IsOnline)
        {
            return;
        }
        if (_squadNetwork.IsHost)
        {
            _squadNetwork.BroadcastMeleeHitConfirmation(
                _squadNetwork.LocalPeerId,
                hitPoint,
                targetId,
                authoritativeDamage,
                meleeDefinitionId,
                attackIndex,
                swingSequence,
                killed,
                armorHit,
                CurrentMeleeCombatEpoch);
            return;
        }
        _squadNetwork.PublishMeleeLoadout(meleeDefinitionId);
        _squadNetwork.RequestMeleeHit(
            origin,
            hitPoint,
            targetId,
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            clientHitAtMsec,
            CurrentMeleeCombatEpoch);
    }

    public void OnLocalMeleeSwingStarted(
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientStartedAtMsec)
    {
        if (!IsInstanceValid(_squadNetwork)
            || !_squadNetwork.IsOnline
            || _squadNetwork.IsHost)
        {
            return;
        }
        _squadNetwork.PublishMeleeLoadout(meleeDefinitionId);
        _squadNetwork.PublishMeleeSwingStart(
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            clientStartedAtMsec,
            CurrentMeleeCombatEpoch);
    }

    private void OnRemoteMeleeLoadout(long peerId, string meleeDefinitionId)
    {
        if (!KnifeSkinCatalog.TryDefinition(meleeDefinitionId, out _))
        {
            return;
        }
        if (_remoteMeleeDefinitionByPeer.TryGetValue(peerId, out var current)
            && string.Equals(current, meleeDefinitionId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _lastRemoteMeleeSwingByPeer.Remove(peerId);
        _remoteMeleeDefinitionByPeer[peerId] = meleeDefinitionId;
    }

    private void OnRemoteMeleeSwingStarted(
        long peerId,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientStartedAtMsec,
        int combatEpoch)
    {
        if (_demolitionMode
                && (!_demolitionRoundActive
                    || _demolitionNetworkPhase != DemolitionNetworkPhase.Live)
            || !_remoteMeleeDefinitionByPeer.TryGetValue(peerId, out var equippedId)
            || !string.Equals(equippedId, meleeDefinitionId, StringComparison.OrdinalIgnoreCase)
            || !KnifeSkinCatalog.TryDefinition(meleeDefinitionId, out var definition)
            || combatEpoch != CurrentMeleeCombatEpoch
            || attackIndex < 0
            || attackIndex >= MeleeAttackCatalog.AttackCount(definition.Style)
            || ResolveRemoteMeleeShooter(peerId) is not { } shooter
            || !RemoteMeleeActorActive(shooter))
        {
            return;
        }
        RecordRemoteMeleeSwingStart(
            peerId,
            definition,
            attackIndex,
            swingSequence,
            clientStartedAtMsec,
            combatEpoch);
    }

    private void OnRemoteMeleeHitRequested(
        long peerId,
        Vector3 reportedOrigin,
        Vector3 reportedHitPoint,
        int targetId,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientHitAtMsec,
        int combatEpoch)
    {
        _remoteNetworkMeleeRequestCount++;
        if (_demolitionMode
                && (!_demolitionRoundActive
                    || _demolitionNetworkPhase != DemolitionNetworkPhase.Live)
            || targetId < 0
            || !_remoteMeleeDefinitionByPeer.TryGetValue(peerId, out var equippedId)
            || !string.Equals(equippedId, meleeDefinitionId, StringComparison.OrdinalIgnoreCase)
            || !KnifeSkinCatalog.TryDefinition(meleeDefinitionId, out var definition)
            || combatEpoch != CurrentMeleeCombatEpoch
            || attackIndex < 0
            || attackIndex >= MeleeAttackCatalog.AttackCount(definition.Style)
            || !TryResolveRemoteMeleeActors(peerId, targetId, out var shooter, out var target))
        {
            return;
        }
        var attack = MeleeAttackCatalog.AttackFor(definition.Style, attackIndex);
        if (!RemoteMeleeGeometryValid(
                peerId,
                swingSequence,
                shooter!,
                target!,
                reportedOrigin,
                reportedHitPoint,
                definition)
            || !TryAcceptRemoteMeleeTarget(
                peerId,
                targetId,
                definition,
                attack,
                attackIndex,
                swingSequence,
                clientHitAtMsec,
                combatEpoch,
                out var executeAt,
                out var authorizationState))
        {
            return;
        }
        var pending = new PendingRemoteMeleeHit(
            peerId,
            reportedOrigin,
            reportedHitPoint,
            targetId,
            definition.Id,
            attackIndex,
            swingSequence,
            combatEpoch,
            executeAt,
            authorizationState);
        if (executeAt > Time.GetTicksMsec())
        {
            ApplyRemoteMeleeHitWhenReady(pending);
            return;
        }
        ApplyAcceptedRemoteMeleeHit(pending);
    }

    private void OnRemoteMeleeHitConfirmed(
        long sourcePeerId,
        Vector3 hitPoint,
        int targetId,
        float damage,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        bool authoritativeKilled,
        bool authoritativeArmorHit,
        int combatEpoch)
    {
        _remoteNetworkMeleeConfirmationCount++;
        _remoteNetworkMeleeFeedbackFlagsReceived |= authoritativeKilled
            && authoritativeArmorHit;
        if (targetId < 0
            || combatEpoch != CurrentMeleeCombatEpoch
            || damage <= 0.0f
            || damage > 240.0f
            || !KnifeSkinCatalog.TryDefinition(meleeDefinitionId, out var definition)
            || attackIndex < 0
            || attackIndex >= MeleeAttackCatalog.AttackCount(definition.Style))
        {
            return;
        }
        var confirmsLocalPlayer = sourcePeerId == _squadNetwork.LocalPeerId;
        if (_demolitionMode || IsExtractionNetworkMatch)
        {
            if (confirmsLocalPlayer)
            {
                _player.ConfirmAuthoritativeMeleeHit(
                    authoritativeKilled,
                    authoritativeArmorHit);
            }
            return;
        }
        var target = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy)
            && enemy.NetworkId == targetId);
        if (target is null)
        {
            if (confirmsLocalPlayer)
            {
                _player.ConfirmAuthoritativeMeleeHit(
                    authoritativeKilled,
                    authoritativeArmorHit);
            }
            return;
        }
        Node? attacker = sourcePeerId == _squadNetwork.LocalPeerId
            ? _player
            : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate.IsHumanProxy
                && mate.NetworkPeerId == sourcePeerId);
        var killed = !target.IsDead && target.TakeDamage(damage, hitPoint, attacker);
        if (confirmsLocalPlayer)
        {
            _player.ConfirmAuthoritativeMeleeHit(killed, target.LastHitWasArmored);
        }
    }

    private bool TryResolveRemoteMeleeActors(
        long peerId,
        int targetId,
        out Node3D? shooter,
        out Node3D? target)
    {
        shooter = ResolveRemoteMeleeShooter(peerId);

        if (_demolitionMode)
        {
            if (!IsDemolitionNetworkHostileShot(peerId, targetId))
            {
                target = null;
                return false;
            }
            var team = DemolitionActorTeam(targetId);
            var slot = DemolitionActorSlot(targetId);
            target = team == _demolitionLocalNetworkTeam
                ? slot == _demolitionLocalNetworkSlot
                    ? _player
                    : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                        && mate.SquadSlot == slot)
                : _demolitionOpponents.FirstOrDefault(enemy => IsInstanceValid(enemy)
                    && enemy.NetworkId == targetId);
        }
        else
        {
            target = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy)
                && enemy.NetworkId == targetId
                && !enemy.IsDead);
        }
        return IsInstanceValid(shooter)
            && IsInstanceValid(target)
            && RemoteMeleeActorActive(shooter!)
            && RemoteMeleeActorActive(target!);
    }

    private Node3D? ResolveRemoteMeleeShooter(long peerId)
    {
        var shooter = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && mate.IsHumanProxy
            && mate.NetworkPeerId == peerId);
        if (_demolitionMode
            && !IsInstanceValid(shooter)
            && _remoteDemolitionOpponents.TryGetValue(peerId, out var opponent)
            && IsInstanceValid(opponent))
        {
            return opponent;
        }
        return shooter;
    }

}
