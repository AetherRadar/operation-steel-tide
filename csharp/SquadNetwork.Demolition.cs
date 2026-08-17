using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public event Action<DemolitionPlayerNetworkState>? DemolitionPlayerStateReceived;
    public event Action<DemolitionActorNetworkState>? DemolitionActorStateReceived;
    public event Action<DemolitionMatchNetworkState>? DemolitionMatchStateReceived;
    public event Action<DemolitionNetworkTeam, int>? DemolitionAssignmentReceived;
    public event Action<long, DemolitionNetworkAction, int>? DemolitionActionReceived;

    public bool IsDemolitionSession { get; private set; }
    public string DemolitionMapId { get; private set; } = string.Empty;
    public DemolitionNetworkTeam RequestedDemolitionTeam { get; private set; }
    public int LocalDemolitionSlot { get; private set; }

    private readonly Dictionary<long, (DemolitionNetworkTeam Team, int Slot)> _demolitionAssignments = new();

    public void ConfigureDemolitionSession(string mapId, DemolitionNetworkTeam requestedTeam)
    {
        IsDemolitionSession = true;
        DemolitionMapId = mapId;
        RequestedDemolitionTeam = requestedTeam;
        LocalDemolitionSlot = 0;
        _demolitionAssignments.Clear();
    }

    public void ConfigureExtractionSession()
    {
        IsDemolitionSession = false;
        DemolitionMapId = string.Empty;
        RequestedDemolitionTeam = DemolitionNetworkTeam.Alpha;
        LocalDemolitionSlot = 0;
        _demolitionAssignments.Clear();
    }

    private void BroadcastDemolitionPlayerState()
    {
        if (LocalPlayer is null)
        {
            return;
        }
        var peerId = Multiplayer.GetUniqueId();
        if (IsHost)
        {
            Rpc(MethodName.ReceiveDemolitionPlayerState, peerId, (int)DemolitionNetworkTeam.Alpha, 0,
                (int)LocalPlayer.Role, LocalPlayer.GlobalPosition, LocalPlayer.Rotation,
                LocalPlayer.Health, LocalPlayer.IsDead);
            return;
        }
        RpcId(1, MethodName.SubmitDemolitionPlayerState, (int)RequestedDemolitionTeam,
            (int)LocalPlayer.Role, LocalPlayer.GlobalPosition, LocalPlayer.Rotation,
            LocalPlayer.Health, LocalPlayer.IsDead, DemolitionMapId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SubmitDemolitionPlayerState(
        int requestedTeam,
        int role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool dead,
        string mapId)
    {
        if (!IsHost || !IsDemolitionSession || mapId != DemolitionMapId
            || !Enum.IsDefined(typeof(DemolitionNetworkTeam), requestedTeam)
            || !Enum.IsDefined(typeof(OperatorRole), role))
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (!_demolitionAssignments.TryGetValue(sender, out var assignment))
        {
            var team = (DemolitionNetworkTeam)requestedTeam;
            var slot = AssignDemolitionSlot(team);
            if (slot < 0)
            {
                return;
            }
            assignment = (team, slot);
            _demolitionAssignments[sender] = assignment;
            RpcId(sender, MethodName.ReceiveDemolitionAssignment, (int)team, slot, DemolitionMapId);
        }
        var state = new DemolitionPlayerNetworkState(sender, assignment.Team, assignment.Slot,
            (OperatorRole)role, position, rotation, health, dead);
        DemolitionPlayerStateReceived?.Invoke(state);
        Rpc(MethodName.ReceiveDemolitionPlayerState, sender, (int)assignment.Team, assignment.Slot,
            role, position, rotation, health, dead);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveDemolitionPlayerState(
        long peerId,
        int team,
        int slot,
        int role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool dead)
    {
        if (peerId == Multiplayer.GetUniqueId()
            || !Enum.IsDefined(typeof(DemolitionNetworkTeam), team)
            || !Enum.IsDefined(typeof(OperatorRole), role))
        {
            return;
        }
        DemolitionPlayerStateReceived?.Invoke(new DemolitionPlayerNetworkState(
            peerId, (DemolitionNetworkTeam)team, slot, (OperatorRole)role,
            position, rotation, health, dead));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionAssignment(int team, int slot, string mapId)
    {
        if (mapId != DemolitionMapId || !Enum.IsDefined(typeof(DemolitionNetworkTeam), team))
        {
            return;
        }
        RequestedDemolitionTeam = (DemolitionNetworkTeam)team;
        LocalDemolitionSlot = slot;
        DemolitionAssignmentReceived?.Invoke(RequestedDemolitionTeam, slot);
    }

    public void BroadcastDemolitionActorState(DemolitionActorNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession)
        {
            return;
        }
        Rpc(MethodName.ReceiveDemolitionActorState, state.ActorId, (int)state.Role,
            state.Position, state.Rotation, state.Health, state.Dead, state.Human);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveDemolitionActorState(
        int actorId,
        int role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool dead,
        bool human)
    {
        if (!Enum.IsDefined(typeof(OperatorRole), role))
        {
            return;
        }
        DemolitionActorStateReceived?.Invoke(new DemolitionActorNetworkState(
            actorId, (OperatorRole)role, position, rotation, health, dead, human));
    }

    public void BroadcastDemolitionMatchState(DemolitionMatchNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession)
        {
            return;
        }
        Rpc(MethodName.ReceiveDemolitionMatchState, state.CurrentRound, state.AlphaScore,
            state.BravoScore, state.Overtime, state.Complete, state.RoundActive,
            state.BuyActive, state.Remaining, state.DevicePhase, state.ActiveSite,
            state.CarrierActorId, state.DevicePosition);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveDemolitionMatchState(
        int currentRound,
        int alphaScore,
        int bravoScore,
        bool overtime,
        bool complete,
        bool roundActive,
        bool buyActive,
        float remaining,
        int devicePhase,
        int activeSite,
        int carrierActorId,
        Vector3 devicePosition)
    {
        DemolitionMatchStateReceived?.Invoke(new DemolitionMatchNetworkState(
            currentRound, alphaScore, bravoScore, overtime, complete, roundActive,
            buyActive, remaining, devicePhase, activeSite, carrierActorId, devicePosition));
    }

    public void RequestDemolitionAction(DemolitionNetworkAction action, int siteIndex)
    {
        if (!IsOnline || !IsDemolitionSession)
        {
            return;
        }
        if (IsHost)
        {
            DemolitionActionReceived?.Invoke(Multiplayer.GetUniqueId(), action, siteIndex);
            return;
        }
        RpcId(1, MethodName.SubmitDemolitionAction, (int)action, siteIndex);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitDemolitionAction(int action, int siteIndex)
    {
        if (!IsHost || !Enum.IsDefined(typeof(DemolitionNetworkAction), action))
        {
            return;
        }
        DemolitionActionReceived?.Invoke(
            Multiplayer.GetRemoteSenderId(), (DemolitionNetworkAction)action, siteIndex);
    }

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

    private void ForgetDemolitionPeer(long peerId) => _demolitionAssignments.Remove(peerId);

    private void ResetDemolitionNetworkState()
    {
        _demolitionAssignments.Clear();
        LocalDemolitionSlot = 0;
    }
}
