using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public event Action<long, int>? ExtractionDoorToggleRequested;
    public event Action<int, bool>? ExtractionDoorStateReceived;

    public void RequestExtractionDoorToggle(int doorId)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionDoorToggleRequested?.Invoke(1, doorId);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionDoorToggle, doorId);
    }

    public void SendExtractionDoorState(long peerId, int doorId, bool open)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        if (peerId == 1)
        {
            return;
        }
        RpcId(peerId, MethodName.ReceiveExtractionDoorState, doorId, open);
    }

    public void BroadcastExtractionDoorState(int doorId, bool open)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        Rpc(MethodName.ReceiveExtractionDoorState, doorId, open);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionDoorToggle(int doorId)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted)
        {
            ExtractionDoorToggleRequested?.Invoke(Multiplayer.GetRemoteSenderId(), doorId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionDoorState(int doorId, bool open)
    {
        if (IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        ExtractionDoorStateReceived?.Invoke(doorId, open);
    }
}
