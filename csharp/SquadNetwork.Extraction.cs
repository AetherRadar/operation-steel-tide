using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public const int ExtractionSquadCapacity = 3;

    public event Action<ExtractionLobbyMember>? ExtractionLobbyMemberReceived;
    public event Action<ExtractionLobbyState>? ExtractionLobbyStateReceived;
    public event Action<int>? ExtractionAssignmentReceived;
    public event Action<string, long>? ExtractionMatchStartReceived;
    public event Action? ExtractionWorldLaunchReceived;

    public bool IsExtractionSession { get; private set; }
    public string ExtractionMapId { get; private set; } = DeploymentMapCatalog.FreightTerminalId;
    public bool ExtractionMatchStarted { get; private set; }
    public bool ExtractionWorldLaunchStarted { get; private set; }
    public bool ExtractionWorldBootstrapReceived { get; private set; }
    public long ExtractionWorldSeed { get; private set; }
    public int LocalExtractionSlot { get; private set; }
    public int RegisteredExtractionPlayerCount => 1 + _extractionAssignments.Count;

    private readonly Dictionary<long, (int Slot, OperatorRole Role)> _extractionAssignments = new();
    private OperatorRole _extractionHostRole = OperatorRole.Assault;
    private bool _extractionWorldReadySent;

    public void SetLocalExtractionLobbyMember(OperatorRole role)
    {
        if (!IsExtractionSession || ExtractionMatchStarted)
        {
            return;
        }

        if (IsHost)
        {
            _extractionHostRole = role;
            LocalExtractionSlot = 0;
            PublishExtractionLobbyMember(new ExtractionLobbyMember(1, 0, role, true));
            PublishExtractionLobbyState();
            return;
        }

        if (IsOnline)
        {
            RpcId(1, MethodName.SubmitExtractionLobbyMember, (int)role, ExtractionMapId);
        }
    }

    public int ExtractionSlotForPeer(long peerId)
    {
        if (peerId == 1)
        {
            return 0;
        }
        return _extractionAssignments.TryGetValue(peerId, out var assignment)
            ? assignment.Slot
            : -1;
    }

    public OperatorRole ExtractionRoleForSlot(int slot)
    {
        if (slot == 0)
        {
            return _extractionHostRole;
        }
        foreach (var assignment in _extractionAssignments.Values)
        {
            if (assignment.Slot == slot)
            {
                return assignment.Role;
            }
        }
        return OperatorRole.Assault;
    }

    public bool IsExtractionHumanSlot(int slot)
    {
        if (slot == 0)
        {
            return true;
        }
        foreach (var assignment in _extractionAssignments.Values)
        {
            if (assignment.Slot == slot)
            {
                return true;
            }
        }
        return false;
    }

    public long ExtractionPeerForSlot(int slot)
    {
        if (slot == 0)
        {
            return 1;
        }
        foreach (var pair in _extractionAssignments)
        {
            if (pair.Value.Slot == slot)
            {
                return pair.Key;
            }
        }
        return 0;
    }

    public bool TryStartExtractionMatch(long worldSeed)
    {
        if (!IsOnline || !IsHost || !IsExtractionSession || ExtractionMatchStarted
            || _extractionAssignments.Count == 0 || worldSeed == 0)
        {
            return false;
        }

        ExtractionMatchStarted = true;
        ExtractionWorldSeed = worldSeed;
        ExtractionWorldLaunchStarted = false;
        ExtractionWorldBootstrapReceived = false;
        _extractionWorldReadySent = false;
        _extractionWorldReadyPeers.Clear();
        StopLanRoomAdvertisement();
        PublishExtractionLobbyState();
        ExtractionMatchStartReceived?.Invoke(ExtractionMapId, worldSeed);
        Rpc(MethodName.ReceiveExtractionMatchStart, ExtractionMapId, worldSeed);
        return true;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitExtractionLobbyMember(int role, string mapId)
    {
        if (!IsHost || !IsExtractionSession || ExtractionMatchStarted
            || !Enum.IsDefined(typeof(OperatorRole), role))
        {
            return;
        }

        var sender = Multiplayer.GetRemoteSenderId();
        if (mapId != ExtractionMapId)
        {
            RpcId(sender, MethodName.ReceiveExtractionAssignment, -2, ExtractionMapId);
            return;
        }
        if (!_extractionAssignments.TryGetValue(sender, out var assignment))
        {
            var slot = AssignExtractionSlot();
            if (slot < 0)
            {
                RpcId(sender, MethodName.ReceiveExtractionAssignment, -1, ExtractionMapId);
                return;
            }
            assignment = (slot, (OperatorRole)role);
            _extractionAssignments[sender] = assignment;
        }
        else
        {
            assignment = (assignment.Slot, (OperatorRole)role);
            _extractionAssignments[sender] = assignment;
        }

        RpcId(sender, MethodName.ReceiveExtractionAssignment, assignment.Slot, ExtractionMapId);
        SendExtractionLobbyRoster(sender);
        var member = new ExtractionLobbyMember(sender, assignment.Slot, assignment.Role, false);
        PublishExtractionLobbyMember(member);
        Rpc(MethodName.ReceiveExtractionLobbyMember,
            member.PeerId, member.Slot, (int)member.Role, member.Host);
        PublishExtractionLobbyState();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionAssignment(int slot, string mapId)
    {
        if (!IsExtractionSession)
        {
            return;
        }
        if (slot == -2)
        {
            ExtractionMapId = mapId;
            ExtractionAssignmentReceived?.Invoke(slot);
            return;
        }
        if (mapId != ExtractionMapId)
        {
            return;
        }
        LocalExtractionSlot = slot;
        ExtractionAssignmentReceived?.Invoke(slot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionLobbyMember(long peerId, int slot, int role, bool host)
    {
        if (!IsExtractionSession || !Enum.IsDefined(typeof(OperatorRole), role))
        {
            return;
        }
        if (host)
        {
            _extractionHostRole = (OperatorRole)role;
        }
        else
        {
            _extractionAssignments[peerId] = (slot, (OperatorRole)role);
        }
        PublishExtractionLobbyMember(new ExtractionLobbyMember(
            peerId, slot, (OperatorRole)role, host));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionLobbyState(string mapId, int players, int capacity, bool started)
    {
        if (!IsExtractionSession)
        {
            return;
        }
        ExtractionMapId = mapId;
        ExtractionMatchStarted = started;
        if (!started)
        {
            ExtractionWorldLaunchStarted = false;
            ExtractionWorldBootstrapReceived = false;
            _extractionWorldReadySent = false;
            _extractionWorldReadyPeers.Clear();
        }
        ExtractionLobbyStateReceived?.Invoke(new ExtractionLobbyState(
            mapId, players, capacity, started));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionMatchStart(string mapId, long worldSeed)
    {
        if (!IsExtractionSession || worldSeed == 0)
        {
            return;
        }
        ExtractionMapId = mapId;
        ExtractionWorldSeed = worldSeed;
        ExtractionMatchStarted = true;
        ExtractionWorldLaunchStarted = false;
        ExtractionWorldBootstrapReceived = false;
        _extractionWorldReadySent = false;
        _extractionWorldReadyPeers.Clear();
        ExtractionMatchStartReceived?.Invoke(mapId, worldSeed);
    }

    private int AssignExtractionSlot()
    {
        for (var slot = 1; slot < ExtractionSquadCapacity; slot++)
        {
            var occupied = false;
            foreach (var assignment in _extractionAssignments.Values)
            {
                if (assignment.Slot == slot)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
            {
                return slot;
            }
        }
        return -1;
    }

    private void SendExtractionLobbyRoster(long peerId)
    {
        RpcId(peerId, MethodName.ReceiveExtractionLobbyMember,
            1, 0, (int)_extractionHostRole, true);
        foreach (var pair in _extractionAssignments)
        {
            RpcId(peerId, MethodName.ReceiveExtractionLobbyMember,
                pair.Key, pair.Value.Slot, (int)pair.Value.Role, false);
        }
    }

    private void PublishExtractionLobbyMember(ExtractionLobbyMember member)
        => ExtractionLobbyMemberReceived?.Invoke(member);

    private void PublishExtractionLobbyState()
    {
        var state = new ExtractionLobbyState(
            ExtractionMapId,
            RegisteredExtractionPlayerCount,
            ExtractionSquadCapacity,
            ExtractionMatchStarted);
        ExtractionLobbyStateReceived?.Invoke(state);
        if (IsOnline && IsHost)
        {
            Rpc(MethodName.ReceiveExtractionLobbyState,
                state.MapId, state.PlayerCount, state.Capacity, state.MatchStarted);
        }
    }

    private void ForgetExtractionPeer(long peerId)
    {
        _extractionWorldReadyPeers.Remove(peerId);
        if (!_extractionAssignments.Remove(peerId))
        {
            return;
        }
        if (IsOnline && IsHost)
        {
            Rpc(MethodName.ReceiveExtractionLobbyMemberLeft, peerId);
        }
        PublishExtractionLobbyState();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveExtractionLobbyMemberLeft(long peerId)
    {
        if (!IsHost && IsExtractionSession)
        {
            _extractionAssignments.Remove(peerId);
            _extractionWorldReadyPeers.Remove(peerId);
        }
    }

    private void ResetExtractionNetworkState()
    {
        IsExtractionSession = false;
        ExtractionMapId = DeploymentMapCatalog.FreightTerminalId;
        ExtractionMatchStarted = false;
        ExtractionWorldLaunchStarted = false;
        ExtractionWorldBootstrapReceived = false;
        ExtractionWorldSeed = 0;
        LocalExtractionSlot = 0;
        _extractionHostRole = OperatorRole.Assault;
        _extractionWorldReadySent = false;
        _extractionWorldReadyPeers.Clear();
        _extractionAssignments.Clear();
        _extractionWorldPacketSequence = 0;
        _incomingExtractionWorldPacketSequence = -1;
        _incomingExtractionWorldChunks = Array.Empty<byte[]>();
        _incomingExtractionWorldChunkCount = 0;
        _incomingExtractionBootstrapPacketSequence = -1;
        _incomingExtractionBootstrapChunks = Array.Empty<byte[]>();
        _incomingExtractionBootstrapChunkCount = 0;
        LastExtractionWorldChunkCount = 0;
    }
}
