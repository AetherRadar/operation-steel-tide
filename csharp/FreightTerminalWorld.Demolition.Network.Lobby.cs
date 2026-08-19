using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct PendingDemolitionDeployment(
        OperatorRole Role,
        string MapId,
        SquadSessionMode Mode,
        string Address,
        DemolitionNetworkTeam Team);

    private bool _demolitionNetworkDeploymentStarted;
    private PendingDemolitionDeployment? _demolitionLobbyDeployment;
    private bool _demolitionJoinPending;
    private OperatorRole _pendingDemolitionRole;
    private string _pendingDemolitionMapId = string.Empty;
    private string _pendingDemolitionAddress = string.Empty;
    private DemolitionNetworkTeam _pendingDemolitionTeam;
    private int _demolitionJoinRejectionCode;

    private void BeginPendingDemolitionJoin(
        OperatorRole role,
        string mapId,
        string address,
        DemolitionNetworkTeam team)
    {
        if (_demolitionJoinPending)
        {
            return;
        }
        var resolvedMapId = DemolitionMapCatalog.Resolve(mapId).Id;
        _demolitionJoinPending = true;
        _demolitionJoinRejectionCode = 0;
        _pendingDemolitionRole = role;
        _pendingDemolitionMapId = resolvedMapId;
        _pendingDemolitionAddress = address;
        _pendingDemolitionTeam = team;
        _squadNetwork.ConfigureDemolitionSession(resolvedMapId, team);
        _hud.SetDemolitionNetworkConnectionPending(true, $"CONNECTING  //  {address}");
        var error = _squadNetwork.Join(address);
        if (error != Error.Ok)
        {
            CancelPendingNetworkDeployment();
        }
    }

    private void BeginHostedDemolitionLobby(PendingDemolitionDeployment deployment)
    {
        _squadNetwork.ConfigureDemolitionSession(deployment.MapId, DemolitionNetworkTeam.Alpha);
        var error = _squadNetwork.Host(deployment.Address);
        if (error != Error.Ok)
        {
            _hud.SetDemolitionNetworkConnectionPending(false, $"HOST FAILED  //  {error}");
            return;
        }

        _demolitionLobbyDeployment = deployment with
        {
            Mode = SquadSessionMode.Host,
            Team = DemolitionNetworkTeam.Alpha
        };
        _demolitionNetworkClient = false;
        _demolitionLocalNetworkTeam = DemolitionNetworkTeam.Alpha;
        _demolitionLocalNetworkSlot = 0;
        _demolitionNetworkDeploymentStarted = false;
        _squadNetwork.SetLocalDemolitionLobbyMember(
            deployment.Role,
            DemolitionNetworkTeam.Alpha);
        _hud.SetDemolitionNetworkLobbyWaiting(
            host: true,
            players: 1,
            capacity: SquadNetwork.DemolitionCapacity,
            canStart: false,
            status: GameLocalization.Get(
                "demolition_lobby_room_open",
                _languageSetting,
                "ROOM OPEN  //  WAITING FOR ANOTHER PLAYER"));
    }

    private void StartHostedDemolitionMatch()
    {
        if (_demolitionLobbyDeployment is null
            || !_squadNetwork.TryStartDemolitionMatch())
        {
            _hud.SetDemolitionNetworkLobbyWaiting(
                host: true,
                players: _squadNetwork.RegisteredDemolitionPlayerCount,
                capacity: SquadNetwork.DemolitionCapacity,
                canStart: false,
                status: GameLocalization.Get(
                    "demolition_lobby_wait_connected",
                    _languageSetting,
                    "WAITING FOR A CONNECTED PLAYER"));
        }
    }

    private void CompletePendingDemolitionJoin()
    {
        if (!_demolitionJoinPending)
        {
            return;
        }
        var role = _pendingDemolitionRole;
        var mapId = _pendingDemolitionMapId;
        var address = _pendingDemolitionAddress;
        var team = _pendingDemolitionTeam;
        _demolitionJoinPending = false;
        _hud.SetDemolitionNetworkConnectionPending(false, _squadNetwork.Status);
        _demolitionLobbyDeployment = new PendingDemolitionDeployment(
            role,
            mapId,
            SquadSessionMode.Join,
            address,
            team);
        _demolitionNetworkClient = true;
        _demolitionNetworkDeploymentStarted = false;
        _squadNetwork.SetLocalDemolitionLobbyMember(role, team);
        _hud.SetDemolitionNetworkLobbyWaiting(
            host: false,
            players: 1,
            capacity: SquadNetwork.DemolitionCapacity,
            canStart: false,
            status: GameLocalization.Get(
                "demolition_lobby_connected",
                _languageSetting,
                "CONNECTED  //  WAITING FOR HOST TO START"));
    }

    private void CancelPendingDemolitionJoin()
    {
        if (!_demolitionJoinPending)
        {
            return;
        }
        _demolitionJoinPending = false;
        _hud.SetDemolitionNetworkConnectionPending(
            false,
            "CONNECTION FAILED  //  ALLOW LOCAL NETWORK / UDP 28960");
    }

    private void CancelDemolitionNetworkLobby(bool closeNetwork = true)
    {
        _demolitionJoinPending = false;
        _demolitionLobbyDeployment = null;
        _demolitionNetworkDeploymentStarted = false;
        if (!_demolitionMode)
        {
            _demolitionNetworkClient = false;
            _demolitionNetworkPhase = DemolitionNetworkPhase.Lobby;
        }
        _hud.ClearDemolitionNetworkLobbyWaiting();
        _hud.SetDemolitionNetworkConnectionPending(false, _squadNetwork.Status);
        if (closeNetwork)
        {
            _squadNetwork.Close();
        }
        if (!_demolitionMode)
        {
            _squadNetwork.StartLanRoomBrowsing();
        }
    }

    private void OnDemolitionLobbyMember(DemolitionLobbyMember member)
    {
        if (_demolitionLobbyDeployment is null || member.Slot < 0)
        {
            return;
        }
        OnDemolitionLobbyState(new DemolitionLobbyState(
            _squadNetwork.DemolitionMapId,
            _squadNetwork.RegisteredDemolitionPlayerCount,
            0,
            0,
            SquadNetwork.DemolitionCapacity,
            _squadNetwork.DemolitionMatchStarted));
    }

    private void OnDemolitionLobbyState(DemolitionLobbyState state)
    {
        if (_demolitionLobbyDeployment is null || state.MatchStarted)
        {
            return;
        }
        var host = _squadNetwork.IsHost;
        var canStart = host && state.PlayerCount >= 2;
        var teamCounts = state.AlphaPlayers + state.BravoPlayers > 0
            ? $"ALPHA {state.AlphaPlayers}/5  //  BRAVO {state.BravoPlayers}/5"
            : $"{state.PlayerCount}/{state.Capacity}";
        var status = host
            ? canStart
                ? $"PLAYER CONNECTED  //  {teamCounts}  //  READY TO START"
                : $"ROOM OPEN  //  {teamCounts}  //  WAITING"
            : $"CONNECTED  //  {teamCounts}  //  WAITING FOR HOST";
        _hud.SetDemolitionNetworkLobbyWaiting(
            host,
            state.PlayerCount,
            state.Capacity,
            canStart,
            status);
    }

    private void OnDemolitionMatchStart()
    {
        if (_demolitionNetworkDeploymentStarted || _demolitionLobbyDeployment is null)
        {
            return;
        }
        _demolitionNetworkDeploymentStarted = true;
        StartNetworkDemolitionDeployment(_demolitionLobbyDeployment.Value);
    }

    private void StartNetworkDemolitionDeployment(PendingDemolitionDeployment deployment)
    {
        _demolitionPlayerRole = deployment.Role;
        _demolitionSelectedMapId = DemolitionMapCatalog.Resolve(deployment.MapId).Id;
        PrepareDemolitionBattlefield();
        InitializeDemolitionNetworkRuntime(deployment.Mode);
        DeploySquad(deployment.Role, deployment.Mode, deployment.Address);
        _hud.ClearDemolitionNetworkLobbyWaiting();
        _hud.SetDemolitionGameplayPresentation(true);
        if (_squadNetwork.IsHost)
        {
            StartDemolitionRound();
        }
        else
        {
            SetDemolitionActorsFrozen(true);
            _hud.ShowLocalizedMessage(
                "demolition_synchronizing",
                "SYNCHRONIZING ROUND STATE",
                new Color(0.42f, 0.88f, 0.72f));
        }
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _missionPhase = "DEMOLITION";
        _hud.SetSquadStatus(_squadNetwork.Status);
        _hud.ShowLocalizedMessage(
            "demolition_deployed",
            "DEMOLITION 5V5  //  FIRST TO 13  //  SIDES SWAP AFTER ROUND 12",
            new Color(1.0f, 0.58f, 0.2f));
        _demolitionLobbyDeployment = null;
    }
}
