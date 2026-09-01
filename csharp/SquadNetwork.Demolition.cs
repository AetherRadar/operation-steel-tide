using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public const int DemolitionCapacity = 10;

    public event Action<DemolitionLobbyMember>? DemolitionLobbyMemberReceived;
    public event Action<DemolitionLobbyState>? DemolitionLobbyStateReceived;
    public event Action? DemolitionMatchStartReceived;
    public event Action<DemolitionPlayerNetworkState>? DemolitionPlayerStateReceived;
    public event Action<DemolitionActorNetworkState>? DemolitionActorStateReceived;
    public event Action<DemolitionMatchNetworkState>? DemolitionMatchStateReceived;
    public event Action<DemolitionNetworkTeam, int>? DemolitionAssignmentReceived;
    public event Action<long, DemolitionNetworkAction, int>? DemolitionActionReceived;
    public event Action<long, int, DemolitionPurchaseSelection>? DemolitionPurchaseRequested;
    public event Action<DemolitionPurchaseNetworkResult>? DemolitionPurchaseResultReceived;
    public event Action<DemolitionFundsNetworkState>? DemolitionFundsStateReceived;

    public bool IsDemolitionSession { get; private set; }
    public string DemolitionMapId { get; private set; } = string.Empty;
    public DemolitionNetworkTeam RequestedDemolitionTeam { get; private set; }
    public int LocalDemolitionSlot { get; private set; }
    public bool DemolitionMatchStarted { get; private set; }
    public int RegisteredDemolitionPlayerCount => 1 + _demolitionAssignments.Count;

    private readonly Dictionary<long, (DemolitionNetworkTeam Team, int Slot, OperatorRole Role)>
        _demolitionAssignments = new();
    private readonly HashSet<long> _demolitionPurchaseV2Peers = new();
    private OperatorRole _demolitionHostRole = OperatorRole.Assault;

    public void ConfigureDemolitionSession(string mapId, DemolitionNetworkTeam requestedTeam)
    {
        ResetExtractionNetworkState();
        ResetDemolitionNetworkState();
        ConfigureLanRoom(LanRoomKind.Demolition, mapId);
        IsDemolitionSession = true;
        DemolitionMapId = mapId;
        RequestedDemolitionTeam = requestedTeam;
    }

    public void ConfigureExtractionSession(string mapId = DeploymentMapCatalog.FreightTerminalId)
    {
        ResetDemolitionNetworkState();
        ConfigureLanRoom(LanRoomKind.Extraction, mapId);
        if (!IsExtractionSession || !IsOnline)
        {
            ResetExtractionNetworkState();
        }
        IsExtractionSession = true;
        ExtractionMapId = mapId;
    }

    public void SetLocalDemolitionLobbyMember(
        OperatorRole role,
        DemolitionNetworkTeam requestedTeam)
    {
        if (!IsDemolitionSession || DemolitionMatchStarted)
        {
            return;
        }

        if (IsHost)
        {
            _demolitionHostRole = role;
            RequestedDemolitionTeam = DemolitionNetworkTeam.Alpha;
            LocalDemolitionSlot = 0;
            PublishDemolitionLobbyMember(new DemolitionLobbyMember(
                1,
                DemolitionNetworkTeam.Alpha,
                0,
                role,
                true));
            PublishDemolitionLobbyState();
            return;
        }

        RequestedDemolitionTeam = requestedTeam;
        if (IsOnline)
        {
            RpcId(1, MethodName.SubmitDemolitionLobbyMember,
                (int)role, (int)requestedTeam, DemolitionMapId);
        }
    }

    public bool TryStartDemolitionMatch()
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession || DemolitionMatchStarted
            || _demolitionAssignments.Count == 0)
        {
            return false;
        }

        DemolitionMatchStarted = true;
        StopLanRoomAdvertisement();
        PublishDemolitionLobbyState();
        DemolitionMatchStartReceived?.Invoke();
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveDemolitionMatchStart, DemolitionMapId);
        }
        return true;
    }

    public bool TryGetDemolitionAssignment(
        long peerId,
        out DemolitionNetworkTeam team,
        out int slot,
        out OperatorRole role)
    {
        if (peerId == 1)
        {
            team = DemolitionNetworkTeam.Alpha;
            slot = 0;
            role = _demolitionHostRole;
            return IsDemolitionSession;
        }
        if (_demolitionAssignments.TryGetValue(peerId, out var assignment))
        {
            team = assignment.Team;
            slot = assignment.Slot;
            role = assignment.Role;
            return true;
        }
        team = DemolitionNetworkTeam.Alpha;
        slot = -1;
        role = OperatorRole.Assault;
        return false;
    }

    public int DemolitionPlayerCount(DemolitionNetworkTeam team)
    {
        var count = team == DemolitionNetworkTeam.Alpha ? 1 : 0;
        foreach (var assignment in _demolitionAssignments.Values)
        {
            if (assignment.Team == team)
            {
                count++;
            }
        }
        return count;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitDemolitionLobbyMember(int role, int requestedTeam, string mapId)
    {
        if (!IsHost || !IsDemolitionSession
            || !Enum.IsDefined(typeof(OperatorRole), role)
            || !Enum.IsDefined(typeof(DemolitionNetworkTeam), requestedTeam))
        {
            return;
        }

        var sender = Multiplayer.GetRemoteSenderId();
        if (DemolitionMatchStarted)
        {
            RpcId(sender, MethodName.ReceiveDemolitionAssignment,
                requestedTeam, -3, DemolitionMapId);
            return;
        }
        if (!string.Equals(mapId, DemolitionMapId, StringComparison.OrdinalIgnoreCase))
        {
            RpcId(sender, MethodName.ReceiveDemolitionAssignment,
                requestedTeam, -2, DemolitionMapId);
            return;
        }

        var team = (DemolitionNetworkTeam)requestedTeam;
        if (!_demolitionAssignments.TryGetValue(sender, out var assignment))
        {
            var slot = AssignDemolitionSlot(team);
            if (slot < 0)
            {
                RpcId(sender, MethodName.ReceiveDemolitionAssignment,
                    requestedTeam, -1, DemolitionMapId);
                return;
            }
            assignment = (team, slot, (OperatorRole)role);
        }
        else
        {
            assignment = (assignment.Team, assignment.Slot, (OperatorRole)role);
        }
        _demolitionAssignments[sender] = assignment;

        RpcId(sender, MethodName.ReceiveDemolitionAssignment,
            (int)assignment.Team, assignment.Slot, DemolitionMapId);
        SendDemolitionLobbyRoster(sender);
        var member = new DemolitionLobbyMember(
            sender,
            assignment.Team,
            assignment.Slot,
            assignment.Role,
            false);
        PublishDemolitionLobbyMember(member);
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveDemolitionLobbyMember,
                member.PeerId, (int)member.Team, member.Slot, (int)member.Role, member.Host);
        }
        PublishDemolitionLobbyState();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionAssignment(int team, int slot, string mapId)
    {
        if (!IsDemolitionSession || !Enum.IsDefined(typeof(DemolitionNetworkTeam), team))
        {
            return;
        }
        if (slot == -2)
        {
            DemolitionMapId = mapId;
            DemolitionAssignmentReceived?.Invoke((DemolitionNetworkTeam)team, slot);
            return;
        }
        if (!string.Equals(mapId, DemolitionMapId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        RequestedDemolitionTeam = (DemolitionNetworkTeam)team;
        LocalDemolitionSlot = slot;
        DemolitionAssignmentReceived?.Invoke(RequestedDemolitionTeam, slot);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionLobbyMember(
        long peerId,
        int team,
        int slot,
        int role,
        bool host)
    {
        if (!IsDemolitionSession
            || !Enum.IsDefined(typeof(DemolitionNetworkTeam), team)
            || !Enum.IsDefined(typeof(OperatorRole), role)
            || slot is < 0 or >= 5)
        {
            return;
        }
        var member = new DemolitionLobbyMember(
            peerId,
            (DemolitionNetworkTeam)team,
            slot,
            (OperatorRole)role,
            host);
        if (host)
        {
            _demolitionHostRole = member.Role;
        }
        else
        {
            _demolitionAssignments[peerId] = (member.Team, member.Slot, member.Role);
        }
        PublishDemolitionLobbyMember(member);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionLobbyState(
        string mapId,
        int players,
        int alphaPlayers,
        int bravoPlayers,
        int capacity,
        bool started)
    {
        if (!IsDemolitionSession || capacity != DemolitionCapacity)
        {
            return;
        }
        DemolitionMapId = mapId;
        DemolitionMatchStarted = started;
        DemolitionLobbyStateReceived?.Invoke(new DemolitionLobbyState(
            mapId,
            Mathf.Clamp(players, 1, capacity),
            Mathf.Clamp(alphaPlayers, 0, 5),
            Mathf.Clamp(bravoPlayers, 0, 5),
            capacity,
            started));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionMatchStart(string mapId)
    {
        if (!IsDemolitionSession
            || !string.Equals(mapId, DemolitionMapId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        DemolitionMatchStarted = true;
        DemolitionMatchStartReceived?.Invoke();
    }

    private void BroadcastDemolitionPlayerState()
    {
        if (!DemolitionMatchStarted || LocalPlayer is null)
        {
            return;
        }
        var peerId = Multiplayer.GetUniqueId();
        if (IsHost)
        {
            SendDemolitionPlayerStateToRegisteredPeers(
                peerId,
                DemolitionNetworkTeam.Alpha,
                0,
                _demolitionHostRole,
                LocalPlayer.GlobalPosition,
                LocalPlayer.Rotation,
                LocalPlayer.Health,
                LocalPlayer.IsDead);
            return;
        }
        RpcId(1, MethodName.SubmitDemolitionPlayerState,
            LocalPlayer.GlobalPosition,
            LocalPlayer.Rotation,
            LocalPlayer.Health,
            LocalPlayer.IsDead,
            DemolitionMapId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SubmitDemolitionPlayerState(
        Vector3 position,
        Vector3 rotation,
        float health,
        bool dead,
        string mapId)
    {
        if (!IsHost || !IsDemolitionSession || !DemolitionMatchStarted
            || !string.Equals(mapId, DemolitionMapId, StringComparison.OrdinalIgnoreCase)
            || !IsFinite(position) || !IsFinite(rotation) || !float.IsFinite(health))
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (!_demolitionAssignments.TryGetValue(sender, out var assignment))
        {
            return;
        }
        var state = new DemolitionPlayerNetworkState(
            sender,
            assignment.Team,
            assignment.Slot,
            assignment.Role,
            position,
            rotation,
            Mathf.Clamp(health, 0.0f, 200.0f),
            dead);
        DemolitionPlayerStateReceived?.Invoke(state);
    }

    internal void RelayDemolitionPlayerState(DemolitionPlayerNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession || !DemolitionMatchStarted
            || !IsFinite(state.Position) || !IsFinite(state.Rotation)
            || !float.IsFinite(state.Health)
            || !_demolitionAssignments.TryGetValue(state.PeerId, out var assignment)
            || assignment.Team != state.Team
            || assignment.Slot != state.Slot
            || assignment.Role != state.Role)
        {
            return;
        }
        SendDemolitionPlayerStateToRegisteredPeers(
            state.PeerId,
            state.Team,
            state.Slot,
            state.Role,
            state.Position,
            state.Rotation,
            Mathf.Clamp(state.Health, 0.0f, 200.0f),
            state.Dead);
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
        var registered = peerId == 1
            ? (DemolitionNetworkTeam)team == DemolitionNetworkTeam.Alpha
                && slot == 0
                && (OperatorRole)role == _demolitionHostRole
            : _demolitionAssignments.TryGetValue(peerId, out var assignment)
                && assignment.Team == (DemolitionNetworkTeam)team
                && assignment.Slot == slot
                && assignment.Role == (OperatorRole)role;
        if (peerId == Multiplayer.GetUniqueId()
            || !Enum.IsDefined(typeof(DemolitionNetworkTeam), team)
            || !Enum.IsDefined(typeof(OperatorRole), role)
            || slot is < 0 or >= 5
            || !IsFinite(position) || !IsFinite(rotation) || !float.IsFinite(health)
            || !registered)
        {
            return;
        }
        DemolitionPlayerStateReceived?.Invoke(new DemolitionPlayerNetworkState(
            peerId,
            (DemolitionNetworkTeam)team,
            slot,
            (OperatorRole)role,
            position,
            rotation,
            Mathf.Clamp(health, 0.0f, 200.0f),
            dead));
    }

    public void BroadcastDemolitionActorState(DemolitionActorNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession || !DemolitionMatchStarted)
        {
            return;
        }
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveDemolitionActorState,
                state.ActorId,
                (int)state.Role,
                state.Position,
                state.Rotation,
                state.Health,
                state.Dead,
                state.Human);
        }
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
        if (!Enum.IsDefined(typeof(OperatorRole), role)
            || !IsFinite(position) || !IsFinite(rotation) || !float.IsFinite(health))
        {
            return;
        }
        DemolitionActorStateReceived?.Invoke(new DemolitionActorNetworkState(
            actorId,
            (OperatorRole)role,
            position,
            rotation,
            Mathf.Clamp(health, 0.0f, 200.0f),
            dead,
            human));
    }

    public void BroadcastDemolitionMatchState(DemolitionMatchNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession || !DemolitionMatchStarted)
        {
            return;
        }
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveDemolitionMatchState,
                state.CurrentRound,
                state.AlphaScore,
                state.BravoScore,
                state.Overtime,
                state.Complete,
                (int)state.Phase,
                state.PhaseRemaining,
                state.AlphaFunds,
                state.BravoFunds,
                state.DevicePhase,
                state.ActiveSite,
                state.CarrierActorId,
                state.DevicePosition,
                state.AlphaWeaponLoadout,
                state.BravoWeaponLoadout,
                state.BazaarGlassMask);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveDemolitionMatchState(
        int currentRound,
        int alphaScore,
        int bravoScore,
        bool overtime,
        bool complete,
        int phase,
        float phaseRemaining,
        int alphaFunds,
        int bravoFunds,
        int devicePhase,
        int activeSite,
        int carrierActorId,
        Vector3 devicePosition,
        int alphaWeaponLoadout,
        int bravoWeaponLoadout,
        uint bazaarGlassMask)
    {
        if (!Enum.IsDefined(typeof(DemolitionNetworkPhase), phase)
            || !Enum.IsDefined(typeof(DemolitionDevicePhase), devicePhase)
            || currentRound < 1 || alphaScore < 0 || bravoScore < 0
            || !DemolitionBotLoadoutNetworkCodec.IsValid(alphaWeaponLoadout)
            || !DemolitionBotLoadoutNetworkCodec.IsValid(bravoWeaponLoadout)
            || !float.IsFinite(phaseRemaining) || !IsFinite(devicePosition))
        {
            return;
        }
        DemolitionMatchStateReceived?.Invoke(new DemolitionMatchNetworkState(
            currentRound,
            alphaScore,
            bravoScore,
            overtime,
            complete,
            (DemolitionNetworkPhase)phase,
            Mathf.Max(0.0f, phaseRemaining),
            Mathf.Clamp(alphaFunds, 0, DemolitionEconomy.MaximumFunds),
            Mathf.Clamp(bravoFunds, 0, DemolitionEconomy.MaximumFunds),
            devicePhase,
            activeSite,
            carrierActorId,
            devicePosition,
            alphaWeaponLoadout,
            bravoWeaponLoadout,
            bazaarGlassMask));
    }

    public void RequestDemolitionAction(DemolitionNetworkAction action, int siteIndex)
    {
        if (!IsOnline || !IsDemolitionSession || !DemolitionMatchStarted)
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
        var sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost || !DemolitionMatchStarted
            || !_demolitionAssignments.ContainsKey(sender)
            || !Enum.IsDefined(typeof(DemolitionNetworkAction), action))
        {
            return;
        }
        DemolitionActionReceived?.Invoke(sender, (DemolitionNetworkAction)action, siteIndex);
    }

    public void RequestDemolitionPurchase(int round, DemolitionPurchaseSelection selection)
    {
        if (!IsOnline || !IsDemolitionSession || !DemolitionMatchStarted || round < 1)
        {
            return;
        }
        if (IsHost)
        {
            DemolitionPurchaseRequested?.Invoke(1, round, selection);
            return;
        }
        if (!UsesDemolitionPurchaseV2(selection))
        {
            RpcId(1, MethodName.SubmitDemolitionPurchase,
                round,
                selection.SidearmId,
                selection.PrimaryId,
                selection.ArmorSelected,
                selection.GrenadeCount,
                selection.SmokeGrenadeCount,
                selection.IncendiaryGrenadeCount);
            return;
        }
        RpcId(1, MethodName.SubmitDemolitionPurchaseV2,
            round,
            selection.SidearmId,
            selection.PrimaryId,
            selection.ArmorSelected,
            selection.GrenadeCount,
            selection.SmokeGrenadeCount,
            selection.IncendiaryGrenadeCount,
            selection.FlashbangGrenadeCount);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitDemolitionPurchase(
        int round,
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount)
    {
        var sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost || !DemolitionMatchStarted || round < 1
            || !_demolitionAssignments.ContainsKey(sender))
        {
            return;
        }
        _demolitionPurchaseV2Peers.Remove(sender);
        DemolitionPurchaseRequested?.Invoke(
            sender,
            round,
            DecodeLegacyDemolitionPurchase(
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitDemolitionPurchaseV2(
        int round,
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount,
        int flashbangGrenadeCount)
    {
        var sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost || !DemolitionMatchStarted || round < 1
            || !_demolitionAssignments.ContainsKey(sender))
        {
            return;
        }
        _demolitionPurchaseV2Peers.Add(sender);
        DemolitionPurchaseRequested?.Invoke(
            sender,
            round,
            DecodeDemolitionPurchaseV2(
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount,
                flashbangGrenadeCount));
    }

    public void SendDemolitionPurchaseResult(
        long peerId,
        DemolitionPurchaseNetworkResult result)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession || peerId <= 1)
        {
            return;
        }
        if (!_demolitionPurchaseV2Peers.Contains(peerId))
        {
            RpcId(peerId, MethodName.ReceiveDemolitionPurchaseResult,
                result.Round,
                result.Approved,
                result.Selection.SidearmId,
                result.Selection.PrimaryId,
                result.Selection.ArmorSelected,
                result.Selection.GrenadeCount,
                result.Selection.SmokeGrenadeCount,
                result.Selection.IncendiaryGrenadeCount,
                result.TotalCost,
                result.RemainingFunds);
            return;
        }
        RpcId(peerId, MethodName.ReceiveDemolitionPurchaseResultV2,
            result.Round,
            result.Approved,
            result.Selection.SidearmId,
            result.Selection.PrimaryId,
            result.Selection.ArmorSelected,
            result.Selection.GrenadeCount,
            result.Selection.SmokeGrenadeCount,
            result.Selection.IncendiaryGrenadeCount,
            result.Selection.FlashbangGrenadeCount,
            result.TotalCost,
            result.RemainingFunds);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionPurchaseResult(
        int round,
        bool approved,
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount,
        int totalCost,
        int remainingFunds)
    {
        DemolitionPurchaseResultReceived?.Invoke(new DemolitionPurchaseNetworkResult(
            round,
            approved,
            DecodeLegacyDemolitionPurchase(
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount),
            Mathf.Max(0, totalCost),
            Mathf.Clamp(remainingFunds, 0, DemolitionEconomy.MaximumFunds)));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionPurchaseResultV2(
        int round,
        bool approved,
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount,
        int flashbangGrenadeCount,
        int totalCost,
        int remainingFunds)
    {
        DemolitionPurchaseResultReceived?.Invoke(new DemolitionPurchaseNetworkResult(
            round,
            approved,
            DecodeDemolitionPurchaseV2(
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount,
                flashbangGrenadeCount),
            Mathf.Max(0, totalCost),
            Mathf.Clamp(remainingFunds, 0, DemolitionEconomy.MaximumFunds)));
    }

    internal static DemolitionPurchaseSelection DecodeLegacyDemolitionPurchase(
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount)
        => new(
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount,
            incendiaryGrenadeCount);

    internal static bool UsesDemolitionPurchaseV2(DemolitionPurchaseSelection selection)
        => selection.FlashbangGrenadeCount > 0;

    internal static DemolitionPurchaseSelection DecodeDemolitionPurchaseV2(
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount,
        int flashbangGrenadeCount)
        => new(
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount,
            incendiaryGrenadeCount,
            flashbangGrenadeCount);

    public void SendDemolitionFundsState(long peerId, DemolitionFundsNetworkState state)
    {
        if (!IsOnline || !IsHost || !IsDemolitionSession || peerId <= 1 || state.Round < 1)
        {
            return;
        }
        RpcId(peerId, MethodName.ReceiveDemolitionFundsState,
            state.Round,
            Mathf.Clamp(state.Funds, 0, DemolitionEconomy.MaximumFunds));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionFundsState(int round, int funds)
    {
        if (!IsDemolitionSession || round < 1)
        {
            return;
        }
        DemolitionFundsStateReceived?.Invoke(new DemolitionFundsNetworkState(
            round,
            Mathf.Clamp(funds, 0, DemolitionEconomy.MaximumFunds)));
    }

}
