using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public event Action<long, string>? RemoteMeleeLoadoutReceived;
    public event Action<long, string, int, int, long, int>? RemoteMeleeSwingStarted;
    public event Action<long, Vector3, Vector3, int, string, int, int, long, int>?
        RemoteMeleeHitRequested;
    public event Action<long, Vector3, int, float, string, int, int, bool, bool, int>?
        RemoteMeleeHitConfirmed;

    public long LocalPeerId => Multiplayer.GetUniqueId();

    public void PublishMeleeLoadout(string meleeDefinitionId)
    {
        if (!CanSendMeleeMessage()
            || string.IsNullOrWhiteSpace(meleeDefinitionId)
            || meleeDefinitionId.Length > 64)
        {
            return;
        }
        if (IsHost)
        {
            return;
        }
        RpcId(1, MethodName.SubmitClientMeleeLoadout, meleeDefinitionId);
    }

    public void RequestMeleeHit(
        Vector3 origin,
        Vector3 hitPoint,
        int targetId,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientHitAtMsec,
        int combatEpoch)
    {
        if (IsHost
            || !CanSendMeleeMessage()
            || !MeleePayloadValid(
                origin,
                hitPoint,
                meleeDefinitionId,
                attackIndex,
                swingSequence,
                clientHitAtMsec,
                combatEpoch))
        {
            return;
        }
        RpcId(
            1,
            MethodName.SubmitClientMeleeHit,
            origin,
            hitPoint,
            targetId,
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            clientHitAtMsec,
            combatEpoch);
    }

    public void PublishMeleeSwingStart(
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientStartedAtMsec,
        int combatEpoch)
    {
        if (IsHost
            || !CanSendMeleeMessage()
            || string.IsNullOrWhiteSpace(meleeDefinitionId)
            || meleeDefinitionId.Length > 64
            || attackIndex < 0
            || swingSequence <= 0
            || clientStartedAtMsec <= 0
            || combatEpoch < 0)
        {
            return;
        }
        RpcId(
            1,
            MethodName.SubmitClientMeleeSwingStart,
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            clientStartedAtMsec,
            combatEpoch);
    }

    public void BroadcastMeleeHitConfirmation(
        long sourcePeerId,
        Vector3 hitPoint,
        int targetId,
        float damage,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        bool killed,
        bool armorHit,
        int combatEpoch)
    {
        if (!IsHost
            || !CanSendMeleeMessage()
            || !IsFinite(hitPoint)
            || !float.IsFinite(damage)
            || damage < 0.0f
            || string.IsNullOrWhiteSpace(meleeDefinitionId)
            || meleeDefinitionId.Length > 64
            || attackIndex < 0
            || swingSequence <= 0
            || combatEpoch < 0)
        {
            return;
        }
        if (IsDemolitionSession)
        {
            foreach (var peerId in RegisteredDemolitionPeerIds())
            {
                RpcId(
                    peerId,
                    MethodName.ReceiveMeleeHitConfirmation,
                    sourcePeerId,
                    hitPoint,
                    targetId,
                    damage,
                    meleeDefinitionId,
                    attackIndex,
                    swingSequence,
                    killed,
                    armorHit,
                    combatEpoch);
            }
            return;
        }
        Rpc(
            MethodName.ReceiveMeleeHitConfirmation,
            sourcePeerId,
            hitPoint,
            targetId,
            damage,
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            killed,
            armorHit,
            combatEpoch);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitClientMeleeLoadout(string meleeDefinitionId)
    {
        if (!IsHost
            || string.IsNullOrWhiteSpace(meleeDefinitionId)
            || meleeDefinitionId.Length > 64)
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (!MeleeSenderRegistered(sender))
        {
            return;
        }
        RemoteMeleeLoadoutReceived?.Invoke(sender, meleeDefinitionId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitClientMeleeSwingStart(
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientStartedAtMsec,
        int combatEpoch)
    {
        if (!IsHost
            || string.IsNullOrWhiteSpace(meleeDefinitionId)
            || meleeDefinitionId.Length > 64
            || attackIndex < 0
            || swingSequence <= 0
            || clientStartedAtMsec <= 0
            || combatEpoch < 0)
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (MeleeSenderRegistered(sender))
        {
            RemoteMeleeSwingStarted?.Invoke(
                sender,
                meleeDefinitionId,
                attackIndex,
                swingSequence,
                clientStartedAtMsec,
                combatEpoch);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitClientMeleeHit(
        Vector3 origin,
        Vector3 hitPoint,
        int targetId,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientHitAtMsec,
        int combatEpoch)
    {
        if (!IsHost
            || !MeleePayloadValid(
                origin,
                hitPoint,
                meleeDefinitionId,
                attackIndex,
                swingSequence,
                clientHitAtMsec,
                combatEpoch))
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (!MeleeSenderRegistered(sender))
        {
            return;
        }
        RemoteMeleeHitRequested?.Invoke(
            sender,
            origin,
            hitPoint,
            targetId,
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            clientHitAtMsec,
            combatEpoch);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveMeleeHitConfirmation(
        long sourcePeerId,
        Vector3 hitPoint,
        int targetId,
        float damage,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        bool killed,
        bool armorHit,
        int combatEpoch)
    {
        if (!IsFinite(hitPoint)
            || !float.IsFinite(damage)
            || damage < 0.0f
            || string.IsNullOrWhiteSpace(meleeDefinitionId)
            || meleeDefinitionId.Length > 64
            || attackIndex < 0
            || swingSequence <= 0
            || combatEpoch < 0)
        {
            return;
        }
        RemoteMeleeHitConfirmed?.Invoke(
            sourcePeerId,
            hitPoint,
            targetId,
            damage,
            meleeDefinitionId,
            attackIndex,
            swingSequence,
            killed,
            armorHit,
            combatEpoch);
    }

    private bool CanSendMeleeMessage()
        => IsOnline
            && (!IsExtractionSession
                || !ExtractionMatchStarted
                || ExtractionWorldLaunchStarted)
            && (!IsDemolitionSession || DemolitionMatchStarted);

    private bool MeleeSenderRegistered(long sender)
    {
        if (IsExtractionSession && ExtractionMatchStarted)
        {
            return ExtractionWorldLaunchStarted && ExtractionSlotForPeer(sender) > 0;
        }
        return !IsDemolitionSession
            || !DemolitionMatchStarted
            || TryGetDemolitionAssignment(sender, out _, out _, out _);
    }

    private static bool MeleePayloadValid(
        Vector3 origin,
        Vector3 hitPoint,
        string meleeDefinitionId,
        int attackIndex,
        int swingSequence,
        long clientHitAtMsec,
        int combatEpoch)
        => IsFinite(origin)
            && IsFinite(hitPoint)
            && !string.IsNullOrWhiteSpace(meleeDefinitionId)
            && meleeDefinitionId.Length <= 64
            && attackIndex >= 0
            && swingSequence > 0
            && clientHitAtMsec > 0
            && combatEpoch >= 0;
}
