using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    private int AssignDemolitionSlot(DemolitionNetworkTeam team)
    {
        var occupied = new HashSet<int>();
        if (team == DemolitionNetworkTeam.Alpha)
        {
            occupied.Add(0);
        }
        foreach (var assignment in _demolitionAssignments.Values)
        {
            if (assignment.Team == team)
            {
                occupied.Add(assignment.Slot);
            }
        }
        for (var slot = 0; slot < 5; slot++)
        {
            if (!occupied.Contains(slot))
            {
                return slot;
            }
        }
        return -1;
    }

    private void SendDemolitionLobbyRoster(long peerId)
    {
        RpcId(peerId, MethodName.ReceiveDemolitionLobbyMember,
            1,
            (int)DemolitionNetworkTeam.Alpha,
            0,
            (int)_demolitionHostRole,
            true);
        foreach (var pair in _demolitionAssignments)
        {
            RpcId(peerId, MethodName.ReceiveDemolitionLobbyMember,
                pair.Key,
                (int)pair.Value.Team,
                pair.Value.Slot,
                (int)pair.Value.Role,
                false);
        }
    }

    private void PublishDemolitionLobbyMember(DemolitionLobbyMember member)
        => DemolitionLobbyMemberReceived?.Invoke(member);

    private void PublishDemolitionLobbyState()
    {
        var alphaPlayers = 1;
        var bravoPlayers = 0;
        foreach (var assignment in _demolitionAssignments.Values)
        {
            if (assignment.Team == DemolitionNetworkTeam.Alpha)
            {
                alphaPlayers++;
            }
            else
            {
                bravoPlayers++;
            }
        }
        var state = new DemolitionLobbyState(
            DemolitionMapId,
            RegisteredDemolitionPlayerCount,
            alphaPlayers,
            bravoPlayers,
            DemolitionCapacity,
            DemolitionMatchStarted);
        DemolitionLobbyStateReceived?.Invoke(state);
        if (IsOnline && IsHost)
        {
            foreach (var peerId in RegisteredDemolitionPeerIds())
            {
                RpcId(peerId, MethodName.ReceiveDemolitionLobbyState,
                    state.MapId,
                    state.PlayerCount,
                    state.AlphaPlayers,
                    state.BravoPlayers,
                    state.Capacity,
                    state.MatchStarted);
            }
        }
    }

    private void ForgetDemolitionPeer(long peerId)
    {
        _demolitionPurchaseV2Peers.Remove(peerId);
        if (!_demolitionAssignments.Remove(peerId))
        {
            return;
        }
        if (IsOnline && IsHost)
        {
            foreach (var remainingPeerId in RegisteredDemolitionPeerIds())
            {
                RpcId(remainingPeerId, MethodName.ReceiveDemolitionLobbyMemberLeft, peerId);
            }
        }
        PublishDemolitionLobbyState();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionLobbyMemberLeft(long peerId)
    {
        if (!IsHost && IsDemolitionSession && _demolitionAssignments.Remove(peerId))
        {
            RemotePeerLeft?.Invoke(peerId);
        }
    }

    private void ResetDemolitionNetworkState()
    {
        IsDemolitionSession = false;
        DemolitionMapId = string.Empty;
        RequestedDemolitionTeam = DemolitionNetworkTeam.Alpha;
        LocalDemolitionSlot = 0;
        DemolitionMatchStarted = false;
        _demolitionHostRole = OperatorRole.Assault;
        _demolitionAssignments.Clear();
        _demolitionPurchaseV2Peers.Clear();
    }

    private List<long> RegisteredDemolitionPeerIds()
        => new(_demolitionAssignments.Keys);

    internal IReadOnlyList<DemolitionLobbyMember> DemolitionLobbyMembers()
    {
        var members = new List<DemolitionLobbyMember>
        {
            new(1, DemolitionNetworkTeam.Alpha, 0, _demolitionHostRole, true)
        };
        foreach (var pair in _demolitionAssignments)
        {
            members.Add(new DemolitionLobbyMember(
                pair.Key,
                pair.Value.Team,
                pair.Value.Slot,
                pair.Value.Role,
                false));
        }
        return members;
    }
}
