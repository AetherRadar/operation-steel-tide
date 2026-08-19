using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public event Action<long, int>? ExtractionLootOpenRequested;
    public event Action<long, int, bool, string>? ExtractionLootMutationReceived;
    public event Action<long, int>? ExtractionLootCloseRequested;
    public event Action<long, Vector3, string>? ExtractionLootDropRequested;
    public event Action<ExtractionLootSourceNetworkState>? ExtractionLootStateReceived;

    public void RequestExtractionLootOpen(int sourceId)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionLootOpenRequested?.Invoke(1, sourceId);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionLootOpen, sourceId);
    }

    public void SendExtractionLootMutation(int sourceId, bool opened, string itemsJson)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionLootMutationReceived?.Invoke(1, sourceId, opened, itemsJson);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionLootMutation, sourceId, opened, itemsJson);
    }

    public void CloseExtractionLoot(int sourceId)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionLootCloseRequested?.Invoke(1, sourceId);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionLootClose, sourceId);
    }

    public void RequestExtractionLootDrop(Vector3 position, string itemJson)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionLootDropRequested?.Invoke(1, position, itemJson);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionLootDrop, position, itemJson);
    }

    public void SendExtractionLootState(long peerId, ExtractionLootSourceNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        if (peerId == 1)
        {
            ExtractionLootStateReceived?.Invoke(state);
            return;
        }
        RpcId(peerId, MethodName.ReceiveExtractionLootState,
            state.SourceId, (int)state.Kind, state.Position, state.Opened,
            state.Granted, state.ItemsJson);
    }

    public void BroadcastExtractionLootState(ExtractionLootSourceNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        Rpc(MethodName.ReceiveExtractionLootState,
            state.SourceId, (int)state.Kind, state.Position, state.Opened,
            state.Granted, state.ItemsJson);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionLootOpen(int sourceId)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted)
        {
            ExtractionLootOpenRequested?.Invoke(Multiplayer.GetRemoteSenderId(), sourceId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionLootMutation(int sourceId, bool opened, string itemsJson)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted)
        {
            ExtractionLootMutationReceived?.Invoke(
                Multiplayer.GetRemoteSenderId(), sourceId, opened, itemsJson);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionLootClose(int sourceId)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted)
        {
            ExtractionLootCloseRequested?.Invoke(Multiplayer.GetRemoteSenderId(), sourceId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionLootDrop(Vector3 position, string itemJson)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted)
        {
            ExtractionLootDropRequested?.Invoke(
                Multiplayer.GetRemoteSenderId(), position, itemJson);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionLootState(
        int sourceId,
        int kind,
        Vector3 position,
        bool opened,
        bool granted,
        string itemsJson)
    {
        if (IsHost || !IsExtractionSession || !ExtractionMatchStarted
            || !Enum.IsDefined(typeof(ExtractionLootSourceKind), kind))
        {
            return;
        }
        ExtractionLootStateReceived?.Invoke(new ExtractionLootSourceNetworkState(
            sourceId,
            (ExtractionLootSourceKind)kind,
            position,
            opened,
            granted,
            itemsJson));
    }
}
