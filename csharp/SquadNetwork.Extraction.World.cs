using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    private const int ExtractionWorldChunkMaximumBytes = 900;
    private const int ExtractionWorldChunkMaximumCount = 64;

    public event Action<long>? ExtractionWorldReadyReceived;
    public event Action<byte[]>? ExtractionWorldStateReceived;
    public event Action<ExtractionMissionNetworkState>? ExtractionMissionStateReceived;
    public event Action<long, int>? ExtractionObjectiveRequested;
    public event Action<long, int>? ExtractionReviveRequested;

    public int LastExtractionWorldChunkCount { get; private set; }
    public int ExtractionWorldReadyPlayerCount => _extractionWorldReadyPeers.Count;

    private int _extractionWorldPacketSequence;
    private int _incomingExtractionWorldPacketSequence = -1;
    private byte[][] _incomingExtractionWorldChunks = Array.Empty<byte[]>();
    private int _incomingExtractionWorldChunkCount;
    private int _incomingExtractionBootstrapPacketSequence = -1;
    private byte[][] _incomingExtractionBootstrapChunks = Array.Empty<byte[]>();
    private int _incomingExtractionBootstrapChunkCount;
    private readonly HashSet<long> _extractionWorldReadyPeers = new();

    public void NotifyExtractionWorldReady()
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || ExtractionWorldLaunchStarted || _extractionWorldReadySent)
        {
            return;
        }
        _extractionWorldReadySent = true;
        if (IsHost)
        {
            RegisterExtractionWorldReady(1);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionWorldReady, ExtractionMapId, ExtractionWorldSeed);
    }

    public void BroadcastExtractionWorldLaunch()
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted
            || ExtractionWorldLaunchStarted
            || _extractionWorldReadyPeers.Count < RegisteredExtractionPlayerCount)
        {
            return;
        }
        ExtractionWorldLaunchStarted = true;
        ExtractionWorldLaunchReceived?.Invoke();
        Rpc(MethodName.ReceiveExtractionWorldLaunch, ExtractionMapId, ExtractionWorldSeed);
    }

    public void BroadcastExtractionWorldState(byte[] payload)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted
            || payload is null || payload.Length == 0)
        {
            return;
        }
        var chunkCount = (payload.Length + ExtractionWorldChunkMaximumBytes - 1)
            / ExtractionWorldChunkMaximumBytes;
        if (chunkCount > ExtractionWorldChunkMaximumCount)
        {
            return;
        }

        LastExtractionWorldChunkCount = chunkCount;
        var packetSequence = unchecked(++_extractionWorldPacketSequence);
        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var offset = chunkIndex * ExtractionWorldChunkMaximumBytes;
            var length = Math.Min(ExtractionWorldChunkMaximumBytes, payload.Length - offset);
            var chunk = new byte[length];
            Buffer.BlockCopy(payload, offset, chunk, 0, length);
            Rpc(MethodName.ReceiveExtractionWorldStateChunk,
                packetSequence, chunkIndex, chunkCount, chunk);
        }
    }

    public void SendExtractionWorldBootstrapState(long peerId, byte[] payload)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted
            || peerId <= 1 || payload is null || payload.Length == 0)
        {
            return;
        }
        var chunkCount = (payload.Length + ExtractionWorldChunkMaximumBytes - 1)
            / ExtractionWorldChunkMaximumBytes;
        if (chunkCount > ExtractionWorldChunkMaximumCount)
        {
            return;
        }

        LastExtractionWorldChunkCount = chunkCount;
        var packetSequence = unchecked(++_extractionWorldPacketSequence);
        for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            var offset = chunkIndex * ExtractionWorldChunkMaximumBytes;
            var length = Math.Min(ExtractionWorldChunkMaximumBytes, payload.Length - offset);
            var chunk = new byte[length];
            Buffer.BlockCopy(payload, offset, chunk, 0, length);
            RpcId(peerId, MethodName.ReceiveExtractionWorldBootstrapChunk,
                packetSequence, chunkIndex, chunkCount, chunk);
        }
    }

    public void BroadcastExtractionMissionState(ExtractionMissionNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        Rpc(MethodName.ReceiveExtractionMissionState,
            state.Phase, state.Remaining, state.Online, state.ObjectiveStage, state.Objective,
            state.DeploymentProtected, state.ReinforcementPending, state.ReinforcementsDeployed,
            state.ReinforcementCountdown, state.EnemiesRemaining, state.ExtractionActive,
            state.ExtractionRemaining, state.MissionEnded, state.ExtractionDeparturePlaying,
            state.MissionSucceeded, state.WorldBossDefeated);
    }

    public void RequestExtractionObjective(int objectiveStage)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionObjectiveRequested?.Invoke(1, objectiveStage);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionObjective, objectiveStage);
    }

    public void RequestExtractionRevive(int targetSlot)
    {
        if (!IsOnline || !IsExtractionSession || !ExtractionMatchStarted
            || !ExtractionWorldLaunchStarted
            || targetSlot < 0 || targetSlot >= ExtractionSquadCapacity)
        {
            return;
        }
        if (IsHost)
        {
            ExtractionReviveRequested?.Invoke(1, targetSlot);
            return;
        }
        RpcId(1, MethodName.SubmitExtractionRevive, targetSlot);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionWorldReady(string mapId, long worldSeed)
    {
        if (!IsHost || !ExtractionMatchStarted || mapId != ExtractionMapId
            || worldSeed != ExtractionWorldSeed || ExtractionWorldLaunchStarted)
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (ExtractionSlotForPeer(sender) > 0)
        {
            RegisterExtractionWorldReady(sender);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionWorldLaunch(string mapId, long worldSeed)
    {
        if (IsHost || !IsExtractionSession || !ExtractionMatchStarted
            || mapId != ExtractionMapId || worldSeed != ExtractionWorldSeed
            || ExtractionWorldLaunchStarted || !ExtractionWorldBootstrapReceived)
        {
            return;
        }
        ExtractionWorldLaunchStarted = true;
        ExtractionWorldLaunchReceived?.Invoke();
    }

    private void RegisterExtractionWorldReady(long peerId)
    {
        if (_extractionWorldReadyPeers.Add(peerId))
        {
            ExtractionWorldReadyReceived?.Invoke(peerId);
        }
    }

    [Rpc(
        MultiplayerApi.RpcMode.Authority,
        TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered,
        TransferChannel = 1)]
    private void ReceiveExtractionWorldStateChunk(
        int packetSequence,
        int chunkIndex,
        int chunkCount,
        byte[] payload)
        => AcceptExtractionWorldStateChunk(
            packetSequence, chunkIndex, chunkCount, payload, bootstrap: false);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionWorldBootstrapChunk(
        int packetSequence,
        int chunkIndex,
        int chunkCount,
        byte[] payload)
        => AcceptExtractionWorldStateChunk(
            packetSequence, chunkIndex, chunkCount, payload, bootstrap: true);

    private void AcceptExtractionWorldStateChunk(
        int packetSequence,
        int chunkIndex,
        int chunkCount,
        byte[] payload,
        bool bootstrap)
    {
        if (IsHost || !IsExtractionSession || !ExtractionMatchStarted
            || packetSequence < 0
            || chunkCount is <= 0 or > ExtractionWorldChunkMaximumCount
            || chunkIndex < 0 || chunkIndex >= chunkCount
            || payload is null || payload.Length == 0
            || payload.Length > ExtractionWorldChunkMaximumBytes)
        {
            return;
        }
        if (bootstrap)
        {
            AcceptExtractionWorldBootstrapChunk(
                packetSequence, chunkIndex, chunkCount, payload);
            return;
        }
        if (packetSequence < _incomingExtractionWorldPacketSequence)
        {
            return;
        }
        if (packetSequence != _incomingExtractionWorldPacketSequence)
        {
            _incomingExtractionWorldPacketSequence = packetSequence;
            _incomingExtractionWorldChunks = new byte[chunkCount][];
            _incomingExtractionWorldChunkCount = 0;
        }
        if (_incomingExtractionWorldChunks.Length != chunkCount
            || _incomingExtractionWorldChunks[chunkIndex] is not null)
        {
            return;
        }

        _incomingExtractionWorldChunks[chunkIndex] = payload;
        _incomingExtractionWorldChunkCount++;
        if (_incomingExtractionWorldChunkCount != chunkCount)
        {
            return;
        }

        var totalLength = 0;
        foreach (var chunk in _incomingExtractionWorldChunks)
        {
            if (chunk is null)
            {
                return;
            }
            totalLength += chunk.Length;
        }
        var assembled = new byte[totalLength];
        var destinationOffset = 0;
        foreach (var chunk in _incomingExtractionWorldChunks)
        {
            Buffer.BlockCopy(chunk, 0, assembled, destinationOffset, chunk.Length);
            destinationOffset += chunk.Length;
        }
        ExtractionWorldStateReceived?.Invoke(assembled);
    }

    private void AcceptExtractionWorldBootstrapChunk(
        int packetSequence,
        int chunkIndex,
        int chunkCount,
        byte[] payload)
    {
        if (packetSequence < _incomingExtractionBootstrapPacketSequence)
        {
            return;
        }
        if (packetSequence != _incomingExtractionBootstrapPacketSequence)
        {
            _incomingExtractionBootstrapPacketSequence = packetSequence;
            _incomingExtractionBootstrapChunks = new byte[chunkCount][];
            _incomingExtractionBootstrapChunkCount = 0;
        }
        if (_incomingExtractionBootstrapChunks.Length != chunkCount
            || _incomingExtractionBootstrapChunks[chunkIndex] is not null)
        {
            return;
        }

        _incomingExtractionBootstrapChunks[chunkIndex] = payload;
        _incomingExtractionBootstrapChunkCount++;
        if (_incomingExtractionBootstrapChunkCount != chunkCount)
        {
            return;
        }

        var totalLength = 0;
        foreach (var chunk in _incomingExtractionBootstrapChunks)
        {
            if (chunk is null)
            {
                return;
            }
            totalLength += chunk.Length;
        }
        var assembled = new byte[totalLength];
        var destinationOffset = 0;
        foreach (var chunk in _incomingExtractionBootstrapChunks)
        {
            Buffer.BlockCopy(chunk, 0, assembled, destinationOffset, chunk.Length);
            destinationOffset += chunk.Length;
        }
        ExtractionWorldBootstrapReceived = true;
        ExtractionWorldStateReceived?.Invoke(assembled);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveExtractionMissionState(
        string phase,
        float remaining,
        bool online,
        int objectiveStage,
        string objective,
        bool deploymentProtected,
        bool reinforcementPending,
        bool reinforcementsDeployed,
        float reinforcementCountdown,
        int enemiesRemaining,
        bool extractionActive,
        float extractionRemaining,
        bool missionEnded,
        bool extractionDeparturePlaying,
        bool missionSucceeded,
        bool worldBossDefeated)
    {
        if (IsHost || !IsExtractionSession || !ExtractionMatchStarted)
        {
            return;
        }
        ExtractionMissionStateReceived?.Invoke(new ExtractionMissionNetworkState(
            phase, remaining, online, objectiveStage, objective, deploymentProtected,
            reinforcementPending, reinforcementsDeployed, reinforcementCountdown,
            enemiesRemaining, extractionActive, extractionRemaining, missionEnded,
            extractionDeparturePlaying, missionSucceeded, worldBossDefeated));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionObjective(int objectiveStage)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted)
        {
            ExtractionObjectiveRequested?.Invoke(Multiplayer.GetRemoteSenderId(), objectiveStage);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionRevive(int targetSlot)
    {
        if (IsHost && IsExtractionSession && ExtractionMatchStarted
            && ExtractionWorldLaunchStarted
            && targetSlot >= 0 && targetSlot < ExtractionSquadCapacity)
        {
            ExtractionReviveRequested?.Invoke(Multiplayer.GetRemoteSenderId(), targetSlot);
        }
    }
}
