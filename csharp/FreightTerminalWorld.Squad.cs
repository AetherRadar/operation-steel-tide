using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly List<SquadMate> _squadMates = new();
    private SquadNetwork _squadNetwork = null!;
    private SquadOrder _squadOrder = SquadOrder.Follow;
    private Vector3 _squadMovePoint;
    private MeshInstance3D? _squadMoveMarker;
    private bool _squadDeployed;
    private bool _localPlayerDowned;
    private bool _localPlayerEliminated;
    private float _squadHudTimer;
    private float _allDownTimer;
    private float _localPlayerDownedTimer;
    private Camera3D? _squadSpectatorCamera;
    private SquadMate? _spectatedMate;
    private int _remoteNetworkShotCount;
    private int _remoteNetworkAbilityCount;
    private string _activeDeploymentMapId = DeploymentMapCatalog.FreightTerminalId;
    private PendingExtractionDeployment? _pendingNetworkExtractionDeployment;
    private PendingExtractionDeployment? _networkLobbyDeployment;
    private int _extractionLocalSquadSlot;
    private bool _networkMatchReloadQueued;

    public int ActiveSquadCount => 1 + _squadMates.Count(mate => IsInstanceValid(mate));
    public int AiSquadCount => _squadMates.Count(mate => IsInstanceValid(mate) && !mate.IsHumanProxy);
    public string ActiveDeploymentMapId => _activeDeploymentMapId;
    internal IReadOnlyList<SquadMate> SquadMatesForRuntime => _squadMates;

    private void BuildSquadSystem()
    {
        EnsureSquadInputActions();
        _squadNetwork = SquadNetworkRuntime.GetOrCreate(GetTree());
        _squadNetwork.RemoteStateReceived += OnRemoteSquadState;
        _squadNetwork.RemotePeerLeft += OnRemoteSquadPeerLeft;
        _squadNetwork.RemoteAbilityReceived += OnRemoteSquadAbility;
        _squadNetwork.RemoteShotReceived += OnRemoteSquadShot;
        _squadNetwork.DemolitionLobbyMemberReceived += OnDemolitionLobbyMember;
        _squadNetwork.DemolitionLobbyStateReceived += OnDemolitionLobbyState;
        _squadNetwork.DemolitionMatchStartReceived += OnDemolitionMatchStart;
        _squadNetwork.DemolitionPlayerStateReceived += OnDemolitionPlayerState;
        _squadNetwork.DemolitionActorStateReceived += OnDemolitionActorState;
        _squadNetwork.DemolitionMatchStateReceived += OnDemolitionMatchState;
        _squadNetwork.DemolitionAssignmentReceived += OnDemolitionNetworkAssignment;
        _squadNetwork.DemolitionActionReceived += OnDemolitionNetworkAction;
        _squadNetwork.DemolitionPurchaseRequested += OnDemolitionNetworkPurchaseRequested;
        _squadNetwork.DemolitionPurchaseResultReceived += OnDemolitionPurchaseResult;
        _squadNetwork.DemolitionFundsStateReceived += OnDemolitionFundsState;
        _squadNetwork.ExtractionLobbyMemberReceived += OnExtractionLobbyMember;
        _squadNetwork.ExtractionLobbyStateReceived += OnExtractionLobbyState;
        _squadNetwork.ExtractionAssignmentReceived += OnExtractionAssignment;
        _squadNetwork.ExtractionMatchStartReceived += OnExtractionMatchStart;
        _squadNetwork.ExtractionWorldLaunchReceived += OnExtractionWorldLaunch;
        _squadNetwork.ExtractionWorldReadyReceived += OnExtractionWorldReady;
        _squadNetwork.ExtractionWorldStateReceived += OnExtractionWorldState;
        _squadNetwork.ExtractionMissionStateReceived += OnExtractionMissionState;
        _squadNetwork.ExtractionPlayerDamageReceived += OnExtractionPlayerDamage;
        _squadNetwork.ExtractionObjectiveRequested += OnExtractionObjectiveRequested;
        _squadNetwork.ExtractionReviveRequested += OnExtractionReviveRequested;
        _squadNetwork.ExtractionLootOpenRequested += OnExtractionLootOpenRequested;
        _squadNetwork.ExtractionLootMutationReceived += OnExtractionLootMutationReceived;
        _squadNetwork.ExtractionLootCloseRequested += OnExtractionLootCloseRequested;
        _squadNetwork.ExtractionLootDropRequested += OnExtractionLootDropRequested;
        _squadNetwork.ExtractionLootStateReceived += OnExtractionLootState;
        _squadNetwork.StatusChanged += OnSquadNetworkStatusChanged;
        _squadNetwork.LanRoomsChanged += OnLanRoomsChanged;
        _squadNetwork.LanRoomBrowseAvailabilityChanged += OnLanRoomBrowseAvailabilityChanged;
        _squadNetwork.ConnectionEstablished += OnSquadConnectionEstablished;
        _squadNetwork.ConnectionAttemptFailed += CancelPendingNetworkDeployment;
        _squadNetwork.ConnectionLost += OnSquadConnectionLost;
        _hud.SquadDeploymentRequested += OnSquadDeploymentRequested;
        _hud.SquadOrderRequested += value => IssueSquadOrder((SquadOrder)value);

        var args = OS.GetCmdlineUserArgs();
        var lobbyCapture = Array.Exists(args, value => value == "--capture-squad-lobby");
        var networkHostCheck = Array.Exists(args, value => value == "--validate-network-host");
        var networkClientCheck = Array.Exists(args, value => value == "--validate-network-client");
        var networkEndpointCheck = Array.Exists(args, value => value == "--validate-network-endpoint");
        var lanDiscoveryCheck = Array.Exists(args, value => value == "--validate-lan-discovery");
        var extractionNetworkCheck = Array.Exists(args, value =>
            value is "--validate-extraction-network-host"
                or "--validate-extraction-network-client");
        var demolitionNetworkCheck = Array.Exists(args, value =>
            value is "--validate-demolition-network-host"
                or "--validate-demolition-network-client"
                or "--validate-demolition-network-alpha-host"
                or "--validate-demolition-network-alpha-client"
                or "--validate-demolition-network-late-client"
                or "--validate-demolition-network-mismatch-client"
                or "--validate-demolition-network-roster-host"
                or "--validate-demolition-network-roster-alpha-client"
                or "--validate-demolition-network-roster-bravo-client");
        var operationsOfficeCommand = Array.Exists(args, value =>
            value == "--validate-operations-office"
            || value == "--validate-demolition"
            || value == "--validate-demolition-rules"
            || value == "--validate-demolition-arena"
            || value == "--validate-demolition-briefing"
            || value == "--validate-demolition-buy"
            || value == "--validate-demolition-network-host"
            || value == "--validate-demolition-network-client"
            || value == "--validate-demolition-network-alpha-host"
            || value == "--validate-demolition-network-alpha-client"
            || value == "--validate-demolition-network-late-client"
            || value == "--validate-demolition-network-mismatch-client"
            || value == "--validate-demolition-network-roster-host"
            || value == "--validate-demolition-network-roster-alpha-client"
            || value == "--validate-demolition-network-roster-bravo-client"
            || value == "--capture-operations-office"
            || value == "--capture-demolition-briefing"
            || value == "--capture-demolition-buy"
            || value == "--capture-demolition-arena");
        var diagnostic = Array.Exists(args, value =>
            value.StartsWith("--capture", StringComparison.Ordinal)
            || value.StartsWith("--validate", StringComparison.Ordinal))
            && !lobbyCapture
            && !networkEndpointCheck
            && !lanDiscoveryCheck
            && !extractionNetworkCheck
            && !demolitionNetworkCheck
            && !operationsOfficeCommand;
        if (diagnostic)
        {
            var mode = networkHostCheck
                ? SquadSessionMode.Host
                : networkClientCheck ? SquadSessionMode.Join : SquadSessionMode.Local;
            var diagnosticEndpoint = ResolveNetworkDiagnosticEndpoint(args);
            DeploySquad(OperatorRole.Assault, mode, diagnosticEndpoint);
            if (networkHostCheck || networkClientCheck)
            {
                ValidateNetworkSession(networkHostCheck ? "host" : "client");
            }
        }
        else
        {
            _player.UiLocked = true;
            _player.DisarmFireInput();
            _player.DisarmMovementInput();
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        WarmSquadPortalWalkConnectorCache();
    }

    private static string ResolveNetworkDiagnosticEndpoint(string[] args)
    {
        const string prefix = "--network-diagnostic-endpoint=";
        foreach (var argument in args)
        {
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return argument[prefix.Length..].Trim();
            }
        }
        return "127.0.0.1";
    }

    private static void EnsureSquadInputActions()
    {
        EnsureKeyAction(GameInputActions.UseClassSkill, Key.H);
        EnsureKeyAction(GameInputActions.SquadFollow, Key.F1);
        EnsureKeyAction(GameInputActions.SquadHold, Key.F2);
        EnsureKeyAction(GameInputActions.SquadMove, Key.F3);
    }

    private static void EnsureKeyAction(StringName action, Key key)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
        var events = InputMap.ActionGetEvents(action);
        using var eventsBacking = events.AsDisposable();
        if (events.Count > 0)
        {
            return;
        }
        using var inputEvent = new InputEventKey { PhysicalKeycode = key };
        InputMap.ActionAddEvent(action, inputEvent);
    }

    private void OnSquadDeploymentRequested(int role, int mode, string address)
    {
        if (_squadDeployed || _deploymentPurchaseCommitted)
        {
            return;
        }
        if (!_hud.DeploymentMapAvailable)
        {
            return;
        }
        var sessionMode = (SquadSessionMode)mode;
        if (sessionMode == SquadSessionMode.Join
            && (string.IsNullOrWhiteSpace(address)
                || !SquadNetwork.TryParseEndpoint(address, SquadNetwork.DefaultPort, out _, out _)))
        {
            _hud.SetSquadStatus("JOIN FAILED  //  INVALID HOST OR PORT");
            return;
        }
        if (sessionMode == SquadSessionMode.Host
            && !SquadNetwork.TryParseHostEndpoint(address, SquadNetwork.DefaultPort, out _, out _))
        {
            _hud.SetSquadStatus("HOST FAILED  //  INVALID BIND IP OR PORT");
            return;
        }
        var selectedMapId = _hud.SelectedDeploymentMapId;
        var deployment = new PendingExtractionDeployment(
            selectedMapId,
            (OperatorRole)role,
            sessionMode,
            address,
            _hud.SelectedDeploymentLoadout);
        if (sessionMode == SquadSessionMode.Host)
        {
            if (_networkLobbyDeployment is not null)
            {
                StartHostedExtractionMatch();
                return;
            }
            BeginHostedExtractionLobby(deployment);
            return;
        }
        if (sessionMode == SquadSessionMode.Join)
        {
            if (!_squadNetwork.IsOnline)
            {
                BeginPendingExtractionJoin(deployment);
            }
            return;
        }
        if (!string.Equals(selectedMapId, _activeRuntimeMapId, StringComparison.OrdinalIgnoreCase))
        {
            DeploymentMapRuntime.StageDeployment(deployment);
            GetTree().Paused = false;
            GetTree().ReloadCurrentScene();
            return;
        }
        if (!TryCommitSelectedDeployment())
        {
            return;
        }
        _activeDeploymentMapId = selectedMapId;
        DeploySquad((OperatorRole)role, sessionMode, address);
    }

    private void DeploySquad(OperatorRole role, SquadSessionMode mode, string address)
    {
        if (_squadDeployed)
        {
            return;
        }
        ActivateBattlefieldFromOperationsOffice();
        _squadNetwork.LocalPlayer = _player;
        _player.ConfigureRole(role);
        if (!_demolitionMode
            && _squadNetwork.IsOnline
            && _squadNetwork.IsExtractionSession
            && _squadNetwork.ExtractionMatchStarted)
        {
            _player.GlobalPosition = _extractionLocalSquadSlot == 0
                ? DeploymentPoint
                : ExtractionSpawnPads.FriendlyMemberPosition(
                    DeploymentPoint,
                    _player.GlobalBasis,
                    _extractionLocalSquadSlot);
            _player.Velocity = Vector3.Zero;
        }
        _player.UiLocked = false;
        _player.DisarmFireInput();
        _player.RestoreMovementInput();
        _squadDeployed = true;
        EnsureDeploymentBaseline();
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        ResetAiReviveAbandonment();
        _squadOrder = SquadOrder.Follow;
        ResetSquadLeaderTrail(_player.GlobalPosition);

        var reuseDemolitionSession = _demolitionMode
            && _squadNetwork.IsOnline
            && _squadNetwork.IsDemolitionSession
            && _squadNetwork.DemolitionMatchStarted
            && string.Equals(_squadNetwork.DemolitionMapId, _demolitionSelectedMapId,
                StringComparison.OrdinalIgnoreCase);
        if (_demolitionMode && !reuseDemolitionSession)
        {
            _squadNetwork.ConfigureDemolitionSession(_demolitionSelectedMapId, _demolitionLocalNetworkTeam);
        }
        else if (!_demolitionMode && (!_squadNetwork.IsExtractionSession
            || !string.Equals(_squadNetwork.ExtractionMapId, _activeDeploymentMapId,
                StringComparison.OrdinalIgnoreCase)))
        {
            _squadNetwork.ConfigureExtractionSession(_activeDeploymentMapId);
        }
        Error networkError = Error.Ok;
        var reuseExtractionSession = !_demolitionMode
            && _squadNetwork.IsOnline
            && _squadNetwork.IsExtractionSession;
        if (!reuseExtractionSession && !reuseDemolitionSession)
        {
            switch (mode)
            {
                case SquadSessionMode.Host:
                    networkError = _squadNetwork.Host(address);
                    break;
                case SquadSessionMode.Join:
                    networkError = _squadNetwork.Join(address);
                    break;
                default:
                    _squadNetwork.Close();
                    _hud.SetSquadStatus("LOCAL SQUAD  //  1 HUMAN + 2 AI");
                    break;
            }
        }
        if (networkError != Error.Ok)
        {
            _hud.SetSquadStatus($"NETWORK UNAVAILABLE  //  AI SQUAD ACTIVE ({networkError})");
        }

        EnsureAiSquadFill();
        InitializeExtractionNetworkWorld();
        InitializeExtractionLootNetwork();
        CompleteSquadDeploymentPresentation(role);
    }

    private void BeginPendingExtractionJoin(PendingExtractionDeployment deployment)
    {
        if (_pendingNetworkExtractionDeployment is not null)
        {
            return;
        }
        _pendingNetworkExtractionDeployment = deployment;
        _squadNetwork.ConfigureExtractionSession(deployment.MapId);
        _hud.SetSquadConnectionPending(true, $"CONNECTING  //  {deployment.Address}");
        var error = _squadNetwork.Join(deployment.Address);
        if (error != Error.Ok)
        {
            CancelPendingNetworkDeployment();
        }
    }

    private void BeginHostedExtractionLobby(PendingExtractionDeployment deployment)
    {
        _squadNetwork.ConfigureExtractionSession(deployment.MapId);
        var error = _squadNetwork.Host(deployment.Address);
        if (error != Error.Ok)
        {
            _hud.SetSquadStatus($"HOST FAILED  //  {error}");
            return;
        }
        _networkLobbyDeployment = deployment;
        _extractionLocalSquadSlot = 0;
        _squadNetwork.SetLocalExtractionLobbyMember(deployment.Role);
        _hud.SetSquadLobbyWaiting(
            host: true,
            players: 1,
            capacity: SquadNetwork.ExtractionSquadCapacity,
            canStart: false,
            status: GameLocalization.Get(
                "squad_lobby_room_open",
                _languageSetting,
                "ROOM OPEN  //  WAITING FOR ANOTHER PLAYER"));
    }

    private void StartHostedExtractionMatch()
    {
        if (_networkLobbyDeployment is null)
        {
            return;
        }
        var seed = Random.Shared.NextInt64(1, long.MaxValue);
        if (!_squadNetwork.TryStartExtractionMatch(seed))
        {
            _hud.SetSquadStatus(GameLocalization.Get(
                "squad_wait_connected_player",
                _languageSetting,
                "WAITING FOR A CONNECTED PLAYER"));
        }
    }

    private void CompletePendingNetworkDeployment()
    {
        if (_pendingNetworkExtractionDeployment is { } extraction)
        {
            _pendingNetworkExtractionDeployment = null;
            _hud.SetSquadConnectionPending(false, _squadNetwork.Status);
            _networkLobbyDeployment = extraction;
            _squadNetwork.SetLocalExtractionLobbyMember(extraction.Role);
            _hud.SetSquadLobbyWaiting(
                host: false,
                players: 1,
                capacity: SquadNetwork.ExtractionSquadCapacity,
                canStart: false,
                status: GameLocalization.Get(
                    "squad_lobby_connected",
                    _languageSetting,
                    "CONNECTED  //  WAITING FOR HOST TO START"));
            return;
        }
        CompletePendingDemolitionJoin();
    }

    private void OnSquadConnectionEstablished() => CompletePendingNetworkDeployment();

    private void CancelPendingNetworkDeployment()
    {
        _pendingNetworkExtractionDeployment = null;
        _networkLobbyDeployment = null;
        _hud.ClearSquadLobbyWaiting();
        _hud.SetSquadConnectionPending(false, "CONNECTION FAILED  //  ALLOW LOCAL NETWORK / UDP 28960");
        CancelPendingDemolitionJoin();
    }

    private void OnExtractionLobbyMember(ExtractionLobbyMember member)
    {
        if (_networkLobbyDeployment is null || member.Slot < 0)
        {
            return;
        }
        OnExtractionLobbyState(new ExtractionLobbyState(
            _squadNetwork.ExtractionMapId,
            _squadNetwork.RegisteredExtractionPlayerCount,
            SquadNetwork.ExtractionSquadCapacity,
            _squadNetwork.ExtractionMatchStarted));
    }

    private void OnExtractionLobbyState(ExtractionLobbyState state)
    {
        if (_networkLobbyDeployment is null || state.MatchStarted)
        {
            return;
        }
        _hud.SetDeploymentMapSelection(state.MapId);
        var host = _squadNetwork.IsHost;
        var canStart = host && state.PlayerCount >= 2;
        var status = host
            ? canStart
                ? GameLocalization.Format(
                    "squad_lobby_ready",
                    _languageSetting,
                    "PLAYER CONNECTED  //  {0}/{1}  //  READY TO START",
                    state.PlayerCount,
                    state.Capacity)
                : GameLocalization.Format(
                    "squad_lobby_waiting",
                    _languageSetting,
                    "ROOM OPEN  //  {0}/{1}  //  WAITING",
                    state.PlayerCount,
                    state.Capacity)
            : GameLocalization.Format(
                "squad_lobby_client_waiting",
                _languageSetting,
                "CONNECTED  //  {0}/{1}  //  WAITING FOR HOST",
                state.PlayerCount,
                state.Capacity);
        _hud.SetSquadLobbyWaiting(host, state.PlayerCount, state.Capacity, canStart, status);
    }

    private void OnExtractionAssignment(int slot)
    {
        if (slot == -2)
        {
            _squadNetwork.Close();
            CancelPendingNetworkDeployment();
            _hud.SetSquadStatus("JOIN FAILED  //  HOST IS USING A DIFFERENT MAP");
            return;
        }
        if (slot < 0)
        {
            _squadNetwork.Close();
            CancelPendingNetworkDeployment();
            _hud.SetSquadStatus("ROOM FULL  //  EXTRACTION SQUAD SUPPORTS 3 PLAYERS");
            return;
        }
        _extractionLocalSquadSlot = slot;
    }

    private void OnExtractionMatchStart(string mapId, long worldSeed)
    {
        if (_networkMatchReloadQueued || _networkLobbyDeployment is null || worldSeed == 0)
        {
            return;
        }
        _networkMatchReloadQueued = true;
        var deployment = _networkLobbyDeployment with
        {
            MapId = mapId,
            WorldSeed = worldSeed,
            SquadSlot = _squadNetwork.LocalExtractionSlot
        };
        DeploymentMapRuntime.StageDeployment(deployment);
        _hud.SetSquadLobbyWaiting(
            _squadNetwork.IsHost,
            _squadNetwork.RegisteredExtractionPlayerCount,
            SquadNetwork.ExtractionSquadCapacity,
            canStart: false,
            status: GameLocalization.Get(
                "squad_lobby_loading",
                _languageSetting,
                "SYNCHRONIZING OPERATION  //  LOADING SHARED WORLD"));
        GetTree().Paused = false;
        CallDeferred(MethodName.ReloadNetworkMatchScene);
    }

    private void ReloadNetworkMatchScene()
    {
        if (_networkMatchReloadQueued)
        {
            GetTree().ReloadCurrentScene();
        }
    }

    private void OnSquadNetworkStatusChanged(string status) => _hud.SetSquadStatus(status);

    private void OnSquadConnectionLost(bool extractionSession)
    {
        if (!extractionSession)
        {
            var demolitionSession = _demolitionJoinPending
                || _demolitionLobbyDeployment is not null
                || _demolitionNetworkClient
                || _demolitionMode && _squadDeployed;
            if (!demolitionSession)
            {
                return;
            }
            var battlefieldActive = _demolitionMode && _squadDeployed;
            CancelDemolitionNetworkLobby(closeNetwork: false);
            _hud.SetDemolitionNetworkConnectionPending(
                false,
                "HOST LOST  //  RETURNING TO OPERATIONS");
            if (battlefieldActive)
            {
                GetTree().Paused = false;
                CallDeferred(MethodName.ReloadAfterExtractionConnectionLost);
            }
            return;
        }
        var sharedWorldActive = _networkMatchReloadQueued
            || _squadDeployed
            || _extractionWorldLaunchPending
            || DeploymentMapRuntime.CurrentWorldSeed != 0;
        _pendingNetworkExtractionDeployment = null;
        _networkLobbyDeployment = null;
        _networkMatchReloadQueued = false;
        _hud.ClearSquadLobbyWaiting();
        _hud.SetSquadConnectionPending(false, "HOST LOST  //  RETURNING TO OPERATIONS");
        if (!sharedWorldActive)
        {
            return;
        }
        _extractionWorldLaunchPending = false;
        GetTree().Paused = false;
        DeploymentMapRuntime.ClearTransientDeployment();
        CallDeferred(MethodName.ReloadAfterExtractionConnectionLost);
    }

    private void ReloadAfterExtractionConnectionLost() => GetTree().ReloadCurrentScene();

    private void OnLanRoomsChanged(IReadOnlyList<LanRoomInfo> rooms) => _hud.SetLanRooms(rooms);

    private void OnLanRoomBrowseAvailabilityChanged(bool available)
        => _hud.SetLanRoomBrowseAvailable(available);

    private void DetachSquadNetworkEvents()
    {
        if (!IsInstanceValid(_squadNetwork))
        {
            return;
        }
        _squadNetwork.RemoteStateReceived -= OnRemoteSquadState;
        _squadNetwork.RemotePeerLeft -= OnRemoteSquadPeerLeft;
        _squadNetwork.RemoteAbilityReceived -= OnRemoteSquadAbility;
        _squadNetwork.RemoteShotReceived -= OnRemoteSquadShot;
        _squadNetwork.DemolitionLobbyMemberReceived -= OnDemolitionLobbyMember;
        _squadNetwork.DemolitionLobbyStateReceived -= OnDemolitionLobbyState;
        _squadNetwork.DemolitionMatchStartReceived -= OnDemolitionMatchStart;
        _squadNetwork.DemolitionPlayerStateReceived -= OnDemolitionPlayerState;
        _squadNetwork.DemolitionActorStateReceived -= OnDemolitionActorState;
        _squadNetwork.DemolitionMatchStateReceived -= OnDemolitionMatchState;
        _squadNetwork.DemolitionAssignmentReceived -= OnDemolitionNetworkAssignment;
        _squadNetwork.DemolitionActionReceived -= OnDemolitionNetworkAction;
        _squadNetwork.DemolitionPurchaseRequested -= OnDemolitionNetworkPurchaseRequested;
        _squadNetwork.DemolitionPurchaseResultReceived -= OnDemolitionPurchaseResult;
        _squadNetwork.DemolitionFundsStateReceived -= OnDemolitionFundsState;
        _squadNetwork.ExtractionLobbyMemberReceived -= OnExtractionLobbyMember;
        _squadNetwork.ExtractionLobbyStateReceived -= OnExtractionLobbyState;
        _squadNetwork.ExtractionAssignmentReceived -= OnExtractionAssignment;
        _squadNetwork.ExtractionMatchStartReceived -= OnExtractionMatchStart;
        _squadNetwork.ExtractionWorldLaunchReceived -= OnExtractionWorldLaunch;
        _squadNetwork.ExtractionWorldReadyReceived -= OnExtractionWorldReady;
        _squadNetwork.ExtractionWorldStateReceived -= OnExtractionWorldState;
        _squadNetwork.ExtractionMissionStateReceived -= OnExtractionMissionState;
        _squadNetwork.ExtractionPlayerDamageReceived -= OnExtractionPlayerDamage;
        _squadNetwork.ExtractionObjectiveRequested -= OnExtractionObjectiveRequested;
        _squadNetwork.ExtractionReviveRequested -= OnExtractionReviveRequested;
        _squadNetwork.ExtractionLootOpenRequested -= OnExtractionLootOpenRequested;
        _squadNetwork.ExtractionLootMutationReceived -= OnExtractionLootMutationReceived;
        _squadNetwork.ExtractionLootCloseRequested -= OnExtractionLootCloseRequested;
        _squadNetwork.ExtractionLootDropRequested -= OnExtractionLootDropRequested;
        _squadNetwork.ExtractionLootStateReceived -= OnExtractionLootState;
        _squadNetwork.StatusChanged -= OnSquadNetworkStatusChanged;
        _squadNetwork.LanRoomsChanged -= OnLanRoomsChanged;
        _squadNetwork.LanRoomBrowseAvailabilityChanged -= OnLanRoomBrowseAvailabilityChanged;
        _squadNetwork.ConnectionEstablished -= OnSquadConnectionEstablished;
        _squadNetwork.ConnectionAttemptFailed -= CancelPendingNetworkDeployment;
        _squadNetwork.ConnectionLost -= OnSquadConnectionLost;
        if (ReferenceEquals(_squadNetwork.LocalPlayer, _player))
        {
            _squadNetwork.LocalPlayer = null;
        }
    }

    private void ValidateNetworkEndpoint()
    {
        var defaultHost = SquadNetwork.TryParseEndpoint(string.Empty, SquadNetwork.DefaultPort,
            out var loopback, out var loopbackPort)
            && loopback == "127.0.0.1"
            && loopbackPort == SquadNetwork.DefaultPort;
        var hostname = SquadNetwork.TryParseEndpoint("steel-tide.example", SquadNetwork.DefaultPort,
            out var hostnameValue, out var hostnamePort)
            && hostnameValue == "steel-tide.example"
            && hostnamePort == SquadNetwork.DefaultPort;
        var tunnel = SquadNetwork.TryParseEndpoint("steel-tide.example:41237", SquadNetwork.DefaultPort,
            out var tunnelHost, out var tunnelPort)
            && tunnelHost == "steel-tide.example"
            && tunnelPort == 41237;
        var ipv6 = SquadNetwork.TryParseEndpoint("[2001:db8::7]:30001", SquadNetwork.DefaultPort,
            out var ipv6Host, out var ipv6Port)
            && ipv6Host == "2001:db8::7"
            && ipv6Port == 30001;
        var nakedIpv6 = SquadNetwork.TryParseEndpoint("2001:db8::8", SquadNetwork.DefaultPort,
            out var nakedIpv6Host, out var nakedIpv6Port)
            && nakedIpv6Host == "2001:db8::8"
            && nakedIpv6Port == SquadNetwork.DefaultPort;
        var defaultHostBind = SquadNetwork.TryParseHostEndpoint(string.Empty, SquadNetwork.DefaultPort,
            out var defaultBindIp, out var defaultBindPort)
            && defaultBindIp == "*"
            && defaultBindPort == SquadNetwork.DefaultPort;
        var ipv4HostBind = SquadNetwork.TryParseHostEndpoint("192.168.10.5:31000", SquadNetwork.DefaultPort,
            out var ipv4BindIp, out var ipv4BindPort)
            && ipv4BindIp == "192.168.10.5"
            && ipv4BindPort == 31000;
        var ipv6HostBind = SquadNetwork.TryParseHostEndpoint("[2001:db8::9]:31001", SquadNetwork.DefaultPort,
            out var ipv6BindIp, out var ipv6BindPort)
            && ipv6BindIp == "2001:db8::9"
            && ipv6BindPort == 31001;
        var hostnameHostBindRejected = !SquadNetwork.TryParseHostEndpoint(
            "steel-tide.example:31000",
            SquadNetwork.DefaultPort,
            out _,
            out _);
        var invalidRejected = !SquadNetwork.TryParseEndpoint("host:0", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint("host:65536", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint(":28960", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint("[2001:db8::7", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint("host:28960:extra", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint("udp://host:41237", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint(":::", SquadNetwork.DefaultPort, out _, out _)
            && !SquadNetwork.TryParseEndpoint("[not-ipv6]:41237", SquadNetwork.DefaultPort, out _, out _);
        var creditsBefore = _operatorProfileStore.Profile.Credits;
        var deploymentsBefore = _operatorProfileStore.Profile.DeploymentCount;
        _hud.ShowSquadLobby("NETWORK ENDPOINT VALIDATION");
        _hud.SelectSquadSessionForDiagnostics(SquadSessionMode.Local);
        var localAddressLocked = !_hud.IsSquadNetworkAddressEditable;
        _hud.SelectSquadSessionForDiagnostics(SquadSessionMode.Host);
        var hostAddressEditable = _hud.IsSquadNetworkAddressEditable;
        _hud.SetSquadConnectionPending(true, "NETWORK ENDPOINT VALIDATION");
        var pendingAddressLocked = !_hud.IsSquadNetworkAddressEditable;
        _hud.SetSquadConnectionPending(false, "NETWORK ENDPOINT VALIDATION");
        var hostAddressRestored = _hud.IsSquadNetworkAddressEditable;
        _hud.SelectSquadSessionForDiagnostics(SquadSessionMode.Join);
        var joinAddressEditable = _hud.IsSquadNetworkAddressEditable;
        var addressModes = localAddressLocked && hostAddressEditable && pendingAddressLocked
            && hostAddressRestored && joinAddressEditable;
        _hud.SetLanRoomBrowseAvailable(true);
        _hud.SetLanRooms(new[]
        {
            new LanRoomInfo(
                "endpoint-room",
                "ENDPOINT HOST",
                "192.168.10.42",
                30123,
                LanRoomKind.Extraction,
                DeploymentMapCatalog.FreightTerminalId,
                2,
                SquadNetwork.ExtractionSquadCapacity)
        });
        _hud.SelectSquadLanRoomForDiagnostics(0);
        var lanRoomSelection = _hud.SquadLanRoomBrowserUiReady
            && _hud.VisibleExtractionLanRoomCount == 1
            && _hud.SelectedSquadSessionMode == SquadSessionMode.Join
            && _hud.SquadNetworkAddress == "192.168.10.42:30123"
            && _hud.SelectedDeploymentMapId == DeploymentMapCatalog.FreightTerminalId;
        OnSquadDeploymentRequested(
            (int)OperatorRole.Assault,
            (int)SquadSessionMode.Host,
            "steel-tide.example:31000");
        var invalidHostDeploymentPreserved = !_squadDeployed
            && !_deploymentPurchaseCommitted
            && _hud.IsSquadLobbyVisible
            && _operatorProfileStore.Profile.Credits == creditsBefore
            && _operatorProfileStore.Profile.DeploymentCount == deploymentsBefore;
        OnDemolitionDeploymentRequested(
            (int)OperatorRole.Assault,
            (int)WeaponPlatform.M4A1,
            1,
            (int)WeaponPlatform.P226,
            DemolitionMapCatalog.TideforgeId,
            (int)SquadSessionMode.Host,
            "steel-tide.example:31000",
            (int)DemolitionNetworkTeam.Alpha);
        var invalidDemolitionHostPreserved = !_squadDeployed && !_demolitionMode;
        OnSquadDeploymentRequested((int)OperatorRole.Assault, (int)SquadSessionMode.Join, "host:0");
        var invalidDeploymentPreserved = !_squadDeployed
            && !_deploymentPurchaseCommitted
            && _hud.IsSquadLobbyVisible
            && _operatorProfileStore.Profile.Credits == creditsBefore
            && _operatorProfileStore.Profile.DeploymentCount == deploymentsBefore;
        OnSquadDeploymentRequested((int)OperatorRole.Assault, (int)SquadSessionMode.Join, "127.0.0.1:28961");
        var validJoinDeferred = !_squadDeployed
            && !_deploymentPurchaseCommitted
            && _pendingNetworkExtractionDeployment is not null
            && _hud.IsSquadLobbyVisible
            && _operatorProfileStore.Profile.Credits == creditsBefore
            && _operatorProfileStore.Profile.DeploymentCount == deploymentsBefore;
        _squadNetwork.Close();
        CancelPendingNetworkDeployment();
        OnDemolitionDeploymentRequested(
            (int)OperatorRole.Assault,
            (int)WeaponPlatform.M4A1,
            1,
            (int)WeaponPlatform.P226,
            DemolitionMapCatalog.TideforgeId,
            (int)SquadSessionMode.Join,
            "127.0.0.1:28961",
            (int)DemolitionNetworkTeam.Alpha);
        var demolitionJoinDeferred = !_squadDeployed && _demolitionJoinPending;
        _squadNetwork.Close();
        CancelPendingNetworkDeployment();
        var valid = defaultHost && hostname && tunnel && ipv6 && nakedIpv6 && defaultHostBind
            && ipv4HostBind && ipv6HostBind && hostnameHostBindRejected && addressModes && lanRoomSelection
            && invalidRejected && invalidHostDeploymentPreserved && invalidDemolitionHostPreserved
            && invalidDeploymentPreserved
            && validJoinDeferred && demolitionJoinDeferred;
        GD.Print($"NETWORK_ENDPOINT_CHECK valid={valid} default={defaultHost} hostname={hostname} tunnel={tunnel} ipv6={ipv6} naked_ipv6={nakedIpv6} host_default={defaultHostBind} host_ipv4={ipv4HostBind} host_ipv6={ipv6HostBind} host_hostname_rejected={hostnameHostBindRejected} address_modes={addressModes} lan_room={lanRoomSelection} invalid_rejected={invalidRejected} invalid_host_deployment_preserved={invalidHostDeploymentPreserved} invalid_demolition_host_preserved={invalidDemolitionHostPreserved} invalid_deployment_preserved={invalidDeploymentPreserved} join_deferred={validJoinDeferred} demolition_join_deferred={demolitionJoinDeferred} endpoint={tunnelHost}:{tunnelPort}");
        GD.Print($"NETWORK_ENDPOINT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private void EnsureAiSquadFill()
    {
        if (!_squadDeployed)
        {
            return;
        }
        // Demolition fields a 5v5 squad; network extraction uses three host-assigned slots.
        var networkExtraction = !_demolitionMode
            && _squadNetwork.IsOnline
            && _squadNetwork.IsExtractionSession
            && _squadNetwork.ExtractionMatchStarted;
        var firstSlot = _demolitionMode || networkExtraction ? 0 : 1;
        var lastSlot = _demolitionMode ? DemolitionSquadSize - 1 : 2;
        for (var slot = firstSlot; slot <= lastSlot; slot++)
        {
            if (_demolitionMode && slot == _demolitionLocalNetworkSlot)
            {
                continue;
            }
            if (networkExtraction && slot == _extractionLocalSquadSlot)
            {
                continue;
            }
            if (_squadMates.Any(mate => IsInstanceValid(mate) && mate.SquadSlot == slot))
            {
                continue;
            }
            var extractionHuman = networkExtraction && _squadNetwork.IsExtractionHumanSlot(slot);
            var extractionPeer = extractionHuman ? _squadNetwork.ExtractionPeerForSlot(slot) : 0;
            var networkProxy = _demolitionMode && IsDemolitionNetworkClient
                || networkExtraction && !_squadNetwork.IsHost && !extractionHuman;
            var humanProxy = networkExtraction ? extractionHuman : networkProxy;
            var resolvedRole = networkExtraction && extractionHuman
                ? _squadNetwork.ExtractionRoleForSlot(slot)
                : RoleForSlot(slot);
            SpawnSquadMate(
                slot,
                resolvedRole,
                humanProxy,
                networkExtraction
                    ? extractionPeer
                    : networkProxy ? -DemolitionActorId(_demolitionLocalNetworkTeam, slot) : 0,
                networkProxy);
        }
        // Drop any AI beyond the mode's roster.
        for (var i = _squadMates.Count - 1; i >= 0; i--)
        {
            var mate = _squadMates[i];
            if (!IsInstanceValid(mate))
            {
                _squadMates.RemoveAt(i);
                continue;
            }
            if ((mate.SquadSlot < firstSlot || mate.SquadSlot > lastSlot) && !mate.IsHumanProxy)
            {
                mate.QueueFree();
                _squadMates.RemoveAt(i);
            }
        }
    }

    private OperatorRole RoleForSlot(int slot)
    {
        // AI always takes the two roles the player did not pick.
        var remaining = new List<OperatorRole>();
        foreach (OperatorRole role in Enum.GetValues<OperatorRole>())
        {
            if (role != _player.Role)
            {
                remaining.Add(role);
            }
        }
        return remaining[Mathf.Clamp(slot - 1, 0, remaining.Count - 1)];
    }

    private SquadMate SpawnSquadMate(
        int slot,
        OperatorRole role,
        bool human,
        long peerId,
        bool networkProxy = false)
    {
        var callsigns = new[] { "RAVEN", "ECHO", "VIPER" };
        var position = ExtractionSpawnPads.FriendlyMemberPosition(
            _player.GlobalPosition,
            _player.GlobalBasis,
            slot);
        var mate = new SquadMate
        {
            Name = human ? $"NetworkSquadmate_{peerId}" : $"AiSquadmate_{slot}",
            Position = position
        };
        var sign = callsigns[Mathf.Clamp(slot, 0, callsigns.Length - 1)];
        mate.Configure(this, _player, slot, role, sign, human, peerId, networkProxy);
        AddChild(mate);
        mate.SetOrder(_squadOrder, _squadMovePoint);
        _squadMates.Add(mate);
        return mate;
    }

    private void OnRemoteSquadState(long peerId, OperatorRole role, Vector3 position, Vector3 rotation, float health, bool down)
        => TryApplyRemoteSquadState(peerId, role, position, rotation, health, down);

    private bool TryApplyRemoteSquadState(long peerId, OperatorRole role, Vector3 position, Vector3 rotation, float health, bool down)
    {
        if (!_squadDeployed)
        {
            return false;
        }
        var authoritative = IsExtractionNetworkMatch && _squadNetwork.IsHost;
        var assignedSlot = authoritative
            ? _squadNetwork.ExtractionSlotForPeer(peerId)
            : -1;
        if (authoritative && assignedSlot > 0
            && _extractionSquadTombstones.ContainsKey(assignedSlot))
        {
            return false;
        }
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (proxy is null)
        {
            assignedSlot = _squadNetwork.IsExtractionSession
                && _squadNetwork.ExtractionMatchStarted
                ? _squadNetwork.ExtractionSlotForPeer(peerId)
                : -1;
            var occupiedSlots = _squadMates
                .Where(mate => IsInstanceValid(mate) && mate.IsHumanProxy)
                .Select(mate => mate.SquadSlot)
                .ToHashSet();
            var slot = assignedSlot >= 0
                ? assignedSlot
                : Enumerable.Range(1, 2).FirstOrDefault(value => !occupiedSlots.Contains(value));
            if (slot == 0)
            {
                return false;
            }
            var ai = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && !mate.IsHumanProxy && mate.SquadSlot == slot);
            if (ai is not null)
            {
                _squadMates.Remove(ai);
                ai.QueueFree();
            }
            proxy = SpawnSquadMate(slot, role, true, peerId);
            _hud.ShowLocalizedMessage("player_joined", $"SQUADMATE CONNECTED  //  PEER {peerId}", OperatorRoles.Spec(role).Accent);
        }
        if (authoritative && (proxy.IsDowned || proxy.IsBodyBag))
        {
            return false;
        }
        proxy.SetRemoteState(
            role,
            position,
            rotation,
            authoritative ? proxy.Health : health,
            authoritative ? proxy.IsDowned || proxy.IsBodyBag : down);
        return true;
    }

    private void OnRemoteSquadPeerLeft(long peerId)
    {
        var demolitionLobby = !_demolitionMode && _demolitionLobbyDeployment is not null;
        var demolitionPlayer = _demolitionNetworkPlayers.GetValueOrDefault(peerId);
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (proxy is not null)
        {
            _extractionSquadTombstones.Remove(proxy.SquadSlot);
            _squadMates.Remove(proxy);
            proxy.QueueFree();
        }
        EnsureAiSquadFill();
        TryLaunchExtractionWorldIfReady();
        OnDemolitionNetworkPeerLeft(peerId);
        var message = demolitionLobby
            ? "PLAYER DISCONNECTED  //  LOBBY UPDATED"
            : _demolitionMode && demolitionPlayer.PeerId != 0
                && demolitionPlayer.Team != _demolitionLocalNetworkTeam
                ? "OPPONENT DISCONNECTED  //  AI TOOK CONTROL"
                : "SQUADMATE DISCONNECTED  //  AI TOOK CONTROL";
        _hud.ShowLocalizedMessage("player_left", message, new Color(0.95f, 0.68f, 0.26f));
    }

    private void OnRemoteSquadAbility(long peerId, OperatorRole role, Vector3 origin, Vector3 forward)
    {
        _remoteNetworkAbilityCount++;
        if (HandleDemolitionRemoteAbility(peerId, role, origin, forward))
        {
            return;
        }
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (proxy is not null)
        {
            proxy.TriggerRemoteRoleAbility(origin, forward, _squadNetwork.IsHost);
        }
    }

    public void OnLocalRoleAbility(OperatorRole role, Vector3 origin, Vector3 forward)
    {
        _squadNetwork?.BroadcastAbility(role, origin, forward);
    }

    public void OnLocalPlayerShot(Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        _squadNetwork?.BroadcastShot(origin, end, enemyId, damage);
    }

    private void OnRemoteSquadShot(long peerId, Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        _remoteNetworkShotCount++;
        var proxy = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        proxy?.PlayRemoteShot(end);
        if (proxy is not null)
        {
            NotifyAircraftOperatorAttack(proxy, origin, 52.0f);
        }
        if (_remoteDemolitionOpponents.TryGetValue(peerId, out var opponentProxy)
            && IsInstanceValid(opponentProxy))
        {
            opponentProxy.PlayRemoteNetworkShot(end);
        }
        if (enemyId < 0 || damage <= 0.0f)
        {
            return;
        }
        if (_demolitionMode)
        {
            if (!_squadNetwork.IsHost || !_demolitionRoundActive)
            {
                return;
            }
            if (IsDemolitionNetworkHostileShot(peerId, enemyId)
                && IsDemolitionRemoteShotValid(peerId, enemyId, origin, end, out var attacker))
            {
                ApplyDemolitionNetworkDamage(
                    enemyId,
                    Mathf.Clamp(damage, 0.0f, 180.0f),
                    end,
                    attacker);
            }
            return;
        }
        if (IsExtractionNetworkMatch && !_squadNetwork.IsHost)
        {
            return;
        }
        if (IsExtractionNetworkMatch && _squadNetwork.IsHost)
        {
            if (proxy is null || proxy.GlobalPosition.DistanceTo(origin) > 4.5f)
            {
                return;
            }
            var shotDistance = origin.DistanceTo(end);
            if (shotDistance <= 0.05f || shotDistance > 260.0f)
            {
                return;
            }
            ReportGunshot(origin, 52.0f);
            damage = Mathf.Clamp(damage, 0.0f, 180.0f);
        }
        var enemy = _enemies.FirstOrDefault(candidate => IsInstanceValid(candidate) && candidate.NetworkId == enemyId);
        if (enemy is not null && !enemy.IsDead)
        {
            if (IsExtractionNetworkMatch && _squadNetwork.IsHost
                && !IsExtractionRemoteShotClear(proxy!, enemy, origin, end))
            {
                return;
            }
            enemy.TakeDamage(damage, end, proxy);
        }
    }

    private void UpdateSquad(float delta)
    {
        if (!_squadDeployed || _missionEnded || _demolitionMode && !_demolitionRoundActive)
        {
            return;
        }
        UpdateSquadLeaderTrail();
        if (!_demolitionMode && !_player.UiLocked && !_player.IsDead)
        {
            if (Input.IsActionJustPressed(GameInputActions.SquadFollow))
            {
                IssueSquadOrder(SquadOrder.Follow);
            }
            else if (Input.IsActionJustPressed(GameInputActions.SquadHold))
            {
                IssueSquadOrder(SquadOrder.Hold);
            }
            else if (Input.IsActionJustPressed(GameInputActions.SquadMove))
            {
                IssueSquadOrder(SquadOrder.Move);
            }
        }

        _squadHudTimer -= delta;
        if (_squadHudTimer <= 0.0f)
        {
            _squadHudTimer = 0.12f;
            RefreshSquadHud();
        }

        var everyoneDown = _player.IsDead && _squadMates
            .Where(IsInstanceValid)
            .All(mate => mate.IsDowned);
        _allDownTimer = everyoneDown ? _allDownTimer + delta : 0.0f;
        if (everyoneDown && _allDownTimer > 1.25f)
        {
            if (!ShouldObservePlantedDemolitionDevice())
            {
                FailSquadMission();
                return;
            }
        }

        UpdateSquadReviveAi(delta);
        if (_localPlayerEliminated)
        {
            UpdateSquadSpectatorCamera();
            return;
        }

        // Hold-to-revive replaces the old auto-revive timer.
        UpdateManualRevive(delta);
        if (_localPlayerDowned)
        {
            UpdateSquadSpectatorCamera();
            if (_localPlayerDowned)
            {
                var helpIncoming = ReferenceEquals(_aiReviveTarget, _player)
                    && _leaderReviver is not null
                    && IsInstanceValid(_leaderReviver)
                    && !_leaderReviver.IsDowned;
                if (!helpIncoming)
                {
                    // Bleed-out keeps running until a mate commits to the revive.
                    _localPlayerDownedTimer += delta;
                }
                _hud.UpdateDownedState(22.0f - _localPlayerDownedTimer);
                if (_localPlayerDownedTimer >= 22.0f)
                {
                    if (!TryBeginLocalPlayerElimination())
                    {
                        FailSquadMission();
                    }
                }
            }
        }
    }

    private SquadMate? _leaderReviver;
    private ISquadCombatant? _aiReviveTarget;
    private float _leaderReviveChannel;
    private float _reviverStuckTime;
    private float _reviverSnapshotTimer;
    private float _reviverBestPathCost = float.PositiveInfinity;
    private float _reviverNoProgressTime;
    private ISquadCombatant? _abandonedAiReviveTarget;
    private readonly HashSet<ulong> _failedAiReviversForTarget = new();
    private const float AiReviveMinimumHealthRatio = 0.35f;
    private const float AiSquadmateReviveRange = 40.0f;
    private const float AiReviveNoProgressTimeout = 15.0f;
    private const ulong AiReviveSelectionMaximumMicroseconds = 50_000;

    /// <summary>
    /// The nearest available AI commits to the highest-priority downed friendly,
    /// reaches them despite ordinary enemy contact, then channels a revive.
    /// </summary>
    private void UpdateSquadReviveAi(float delta)
    {
        if (_demolitionMode || _missionEnded || IsExtractionNetworkClient)
        {
            ClearLeaderReviveAi();
            return;
        }

        var preferredTarget = FindAiReviveTarget();
        if (preferredTarget is null)
        {
            ClearLeaderReviveAi();
            return;
        }
        if (!ReferenceEquals(_aiReviveTarget, preferredTarget))
        {
            ClearLeaderReviveAi();
            _aiReviveTarget = preferredTarget;
        }

        if (_leaderReviver is null || !IsInstanceValid(_leaderReviver)
            || !CanAiRevive(_leaderReviver, preferredTarget))
        {
            if (_leaderReviver is not null && IsInstanceValid(_leaderReviver))
            {
                ClearSquadNavigation(_leaderReviver);
                _leaderReviver.EndSquadRevive();
            }
            _leaderReviver = null;
            _leaderReviveChannel = 0.0f;
            _reviverStuckTime = 0.0f;
            SquadMate? nearest = null;
            var bestDistanceSquared = float.PositiveInfinity;
            var selectionTargetPosition = preferredTarget.CombatNode.GlobalPosition;
            foreach (var mate in _squadMates)
            {
                if (!CanAiRevive(mate, preferredTarget)
                    || _failedAiReviversForTarget.Contains(mate.GetInstanceId()))
                {
                    continue;
                }
                // Selection runs every frame while a target is pending. Keep it
                // geometric; the bounded planner validates the committed route.
                var distanceSquared = mate.GlobalPosition.DistanceSquaredTo(selectionTargetPosition);
                if (preferredTarget is SquadMate
                    && distanceSquared > AiSquadmateReviveRange * AiSquadmateReviveRange)
                {
                    continue;
                }
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    nearest = mate;
                }
            }
            if (nearest is null)
            {
                return;
            }
            _leaderReviver = nearest;
            _leaderReviver.BeginSquadRevive(preferredTarget);
            BeginLeaderRescueNavigation(nearest, preferredTarget.CombatNode.GlobalPosition);
            _reviverBestPathCost = float.PositiveInfinity;
            _reviverSnapshotTimer = 0.0f;
            if (ReferenceEquals(preferredTarget, _player))
            {
                _hud.ShowLocalizedMessage(
                    "mate_reviving_you",
                    "TEAMMATE MOVING TO REVIVE  //  HOLD ON",
                    new Color(0.55f, 0.92f, 0.68f));
            }
        }

        var reviver = _leaderReviver;
        var target = _aiReviveTarget;
        if (reviver is null || !IsInstanceValid(reviver)
            || target is null || !target.CanBeRevived || !IsInstanceValid(target.CombatNode))
        {
            ClearLeaderReviveAi();
            return;
        }
        var targetPosition = target.CombatNode.GlobalPosition;
        var distanceToTarget = reviver.GlobalPosition.DistanceTo(targetPosition);
        var hasReviveAccess = distanceToTarget <= 2.3f
            && Mathf.Abs(reviver.GlobalPosition.Y - targetPosition.Y) <= 1.25f
            && HasSquadReviveLineOfSight(reviver, target);
        if (!hasReviveAccess)
        {
            _leaderReviveChannel = Mathf.Max(0.0f, _leaderReviveChannel - delta * 1.5f);
            // Route progress matters more than raw movement: circling a wall is not progress.
            _reviverSnapshotTimer += delta;
            if (_reviverSnapshotTimer >= 1.0f)
            {
                var remainingPathCost = GetSquadNavigationRemainingCost(reviver, targetPosition);
                var reducedPathCost = remainingPathCost < _reviverBestPathCost - 0.55f;
                if (reducedPathCost)
                {
                    _reviverStuckTime = 0.0f;
                    _reviverNoProgressTime = 0.0f;
                }
                else
                {
                    _reviverStuckTime += _reviverSnapshotTimer;
                    _reviverNoProgressTime += _reviverSnapshotTimer;
                }
                _reviverBestPathCost = Mathf.Min(_reviverBestPathCost, remainingPathCost);
                _reviverSnapshotTimer = 0.0f;
            }
            if (_reviverNoProgressTime >= AiReviveNoProgressTimeout)
            {
                _failedAiReviversForTarget.Add(reviver.GetInstanceId());
                if (_squadMates.Any(mate => CanAiRevive(mate, target)
                    && !_failedAiReviversForTarget.Contains(mate.GetInstanceId())))
                {
                    ReleaseLeaderReviverForRetry();
                    return;
                }
                _abandonedAiReviveTarget = target;
                ClearLeaderReviveAi();
                return;
            }
            if (_reviverStuckTime >= 2.5f)
            {
                ReplanLeaderRescueNavigation(reviver);
                _reviverStuckTime = 0.0f;
                _reviverSnapshotTimer = 0.0f;
            }
            return;
        }

        _reviverStuckTime = 0.0f;
        _reviverSnapshotTimer = 0.0f;
        _reviverBestPathCost = 0.0f;
        _reviverNoProgressTime = 0.0f;
        _leaderReviveChannel += delta / 2.8f;
        if (_leaderReviveChannel < 1.0f)
        {
            return;
        }

        _leaderReviveChannel = 0.0f;
        var revived = target.TryReceiveRevive(60.0f);
        if (revived)
        {
            ResetAiReviveAbandonment();
        }
        if (revived && target is SquadMate revivedMate)
        {
            _hud.ShowLocalizedMessage(
                "squad_revive",
                $"AI REVIVE  //  {revivedMate.Callsign} STABILIZED",
                new Color(0.55f, 0.92f, 0.68f));
            SpawnMedicSprayEffect(
                reviver.GlobalPosition + Vector3.Up * 1.2f,
                revivedMate.HitPoint(HitRegion.Torso));
        }
        // Player revival clears this synchronously through OnLocalPlayerRevived;
        // mate revival and failed/raced attempts are cleared here.
        ClearLeaderReviveAi();
    }

    private ISquadCombatant? FindAiReviveTarget()
    {
        if (_localPlayerDowned && IsInstanceValid(_player) && _player.CanBeRevived
            && !ReferenceEquals(_abandonedAiReviveTarget, _player))
        {
            return _player;
        }
        if (_aiReviveTarget is SquadMate currentTarget
            && IsInstanceValid(currentTarget)
            && currentTarget.CanBeRevived
            && !ReferenceEquals(_abandonedAiReviveTarget, currentTarget))
        {
            // A downed squad mate is stationary. Keep the committed target until it
            // is revived, abandoned, or invalidated instead of rerunning layered A*
            // for every candidate on every physics frame.
            return currentTarget;
        }
        foreach (var target in _squadMates
                     .Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy && mate.CanBeRevived
                         && !ReferenceEquals(_abandonedAiReviveTarget, mate))
                     .OrderBy(mate => mate.SquadSlot))
        {
            // Reachability is resolved only after assignment. Running layered A*
            // here made one unavailable mate trigger a full physics search per frame.
            var reviverInRange = _squadMates.Any(mate => CanAiRevive(mate, target)
                && mate.GlobalPosition.DistanceSquaredTo(target.GlobalPosition)
                    <= AiSquadmateReviveRange * AiSquadmateReviveRange);
            if (reviverInRange)
            {
                return target;
            }
        }
        return null;
    }

    private static bool CanAiRevive(SquadMate mate, ISquadCombatant target)
    {
        return IsInstanceValid(mate)
            && !mate.IsDowned
            && !mate.IsBodyBag
            && !mate.IsHumanProxy
            && !mate.IsExtractionPassenger
            && mate.ProcessMode != ProcessModeEnum.Disabled
            && mate.Health / Mathf.Max(1.0f, mate.MaxHealth) >= AiReviveMinimumHealthRatio
            && !ReferenceEquals(mate, target);
    }

    private void ClearLeaderReviveAi()
    {
        if (_leaderReviver is not null && IsInstanceValid(_leaderReviver))
        {
            ClearSquadNavigation(_leaderReviver);
            _leaderReviver.EndSquadRevive();
        }
        _leaderReviver = null;
        _aiReviveTarget = null;
        _leaderReviveChannel = 0.0f;
        _reviverStuckTime = 0.0f;
        _reviverSnapshotTimer = 0.0f;
        _reviverBestPathCost = float.PositiveInfinity;
        _reviverNoProgressTime = 0.0f;
        _failedAiReviversForTarget.Clear();
    }

    private void ReleaseLeaderReviverForRetry()
    {
        if (_leaderReviver is not null && IsInstanceValid(_leaderReviver))
        {
            ClearSquadNavigation(_leaderReviver);
            _leaderReviver.EndSquadRevive();
        }
        _leaderReviver = null;
        _leaderReviveChannel = 0.0f;
        _reviverStuckTime = 0.0f;
        _reviverSnapshotTimer = 0.0f;
        _reviverBestPathCost = float.PositiveInfinity;
        _reviverNoProgressTime = 0.0f;
    }

    private void ResetAiReviveAbandonment()
    {
        _abandonedAiReviveTarget = null;
        _reviverNoProgressTime = 0.0f;
    }

    private void BeginSquadMateView()
    {
        _spectatedMate = FindLivingSpectatorTarget();
        if (_spectatedMate is null)
        {
            return;
        }

        var spectatorCamera = EnsureSquadSpectatorCamera();

        SnapSquadSpectatorCamera();
        spectatorCamera.MakeCurrent();
        _hud.ShowLocalizedMessage(
            "spectating_teammate",
            $"SPECTATING  //  {_spectatedMate.Callsign}",
            OperatorRoles.Spec(_spectatedMate.Role).Accent);
    }

    private void UpdateSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is null || !IsInstanceValid(_squadSpectatorCamera))
        {
            BeginSquadMateView();
            return;
        }

        if (_spectatedMate is null || !IsInstanceValid(_spectatedMate)
            || _spectatedMate.IsDowned || _spectatedMate.IsBodyBag)
        {
            _spectatedMate = FindLivingSpectatorTarget();
            if (_spectatedMate is not null)
            {
                _hud.ShowLocalizedMessage(
                    "spectating_teammate",
                    $"SPECTATING  //  {_spectatedMate.Callsign}",
                    OperatorRoles.Spec(_spectatedMate.Role).Accent);
            }
        }
        if (_spectatedMate is null)
        {
            if (!_demolitionObjectiveSpectatorActive && ShouldObservePlantedDemolitionDevice())
            {
                BeginDemolitionObjectiveView();
            }
            return;
        }

        SnapSquadSpectatorCamera();
        if (!_squadSpectatorCamera.Current)
        {
            _squadSpectatorCamera.MakeCurrent();
        }
    }

    private SquadMate? FindLivingSpectatorTarget()
    {
        return _squadMates
            .Where(mate => IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
            .OrderBy(mate => mate.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition))
            .FirstOrDefault();
    }

    private Camera3D EnsureSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is not null && IsInstanceValid(_squadSpectatorCamera))
        {
            return _squadSpectatorCamera;
        }
        _squadSpectatorCamera = new Camera3D
        {
            Name = "SquadSpectatorCamera",
            Fov = 76.0f,
            Near = 0.04f,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off
        };
        AddChild(_squadSpectatorCamera);
        return _squadSpectatorCamera;
    }

    private void SnapSquadSpectatorCamera()
    {
        if (_squadSpectatorCamera is null || _spectatedMate is null
            || !IsInstanceValid(_squadSpectatorCamera) || !IsInstanceValid(_spectatedMate))
        {
            return;
        }

        var basis = _spectatedMate.GlobalBasis.Orthonormalized();
        var eyePosition = _spectatedMate.GlobalPosition
            + Vector3.Up * 1.64f
            - basis.Z * 0.28f;
        _squadSpectatorCamera.GlobalTransform = new Transform3D(basis, eyePosition);
    }

    private void RestoreLocalPlayerView()
    {
        _spectatedMate = null;
        _demolitionObjectiveSpectatorActive = false;
        var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
        playerCamera?.MakeCurrent();
    }

    private bool IsSquadMateViewCurrent =>
        _squadSpectatorCamera is not null
        && IsInstanceValid(_squadSpectatorCamera)
        && GetViewport().GetCamera3D() == _squadSpectatorCamera
        && _spectatedMate is not null
        && IsInstanceValid(_spectatedMate)
        && !_spectatedMate.IsDowned
        && !_spectatedMate.IsBodyBag;

    private bool IsLocalPlayerViewCurrent
    {
        get
        {
            var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
            return playerCamera is not null && GetViewport().GetCamera3D() == playerCamera;
        }
    }

    private float _manualReviveProgress;
    private ISquadCombatant? _manualReviveTarget;

    private void UpdateManualRevive(float delta)
    {
        if (_demolitionMode || !_squadDeployed || _missionEnded || !IsInstanceValid(_player))
        {
            CancelManualRevive();
            return;
        }

        // Downed player cannot revive others.
        if (_player.IsDead || _player.IsInVehicle || _hud.IsLootVisible)
        {
            CancelManualRevive();
            return;
        }

        ISquadCombatant? target = null;
        var best = 2.85f;
        foreach (var friendly in FriendlyCombatants())
        {
            if (friendly == _player || !friendly.CanBeRevived)
            {
                continue;
            }
            var targetPosition = friendly.CombatNode.GlobalPosition;
            var distance = _player.GlobalPosition.DistanceTo(targetPosition);
            if (friendly is SquadMate downedMate
                && (Mathf.Abs(_player.GlobalPosition.Y - targetPosition.Y) > 1.25f
                    || !IsSquadMovementCorridorClear(
                        _player.GlobalPosition,
                        targetPosition,
                        downedMate)))
            {
                continue;
            }
            if (distance < best)
            {
                best = distance;
                target = friendly;
            }
        }

        if (target is null)
        {
            CancelManualRevive();
            return;
        }

        var label = GameLocalization.IsChinese(_languageSetting)
            ? "按住 F 救援队友"
            : "HOLD F  //  REVIVE TEAMMATE";
        if (!Input.IsActionPressed(GameInputActions.Interact) || _interactReleaseRequired)
        {
            _manualReviveProgress = Mathf.Max(0.0f, _manualReviveProgress - delta * 1.4f);
            _manualReviveTarget = target;
            _hud.SetInteraction(label, _manualReviveProgress > 0.02f ? _manualReviveProgress : -1.0f, true);
            return;
        }

        if (!ReferenceEquals(_manualReviveTarget, target))
        {
            _manualReviveTarget = target;
            _manualReviveProgress = 0.0f;
        }

        _manualReviveProgress = Mathf.Min(1.0f, _manualReviveProgress + delta / 2.6f);
        _player.SetSearchPose(true, _manualReviveProgress);
        _hud.SetInteraction(label, _manualReviveProgress, true);
        if (_manualReviveProgress < 1.0f)
        {
            return;
        }

        var networkTarget = IsExtractionNetworkClient ? target as SquadMate : null;
        var networkRequest = networkTarget is not null;
        var revived = false;
        if (networkRequest)
        {
            _squadNetwork.RequestExtractionRevive(networkTarget!.SquadSlot);
        }
        else
        {
            revived = target.TryReceiveRevive(62.0f);
        }
        _manualReviveProgress = 0.0f;
        _manualReviveTarget = null;
        _interactReleaseRequired = true;
        _player.SetSearchPose(false);
        if (networkRequest)
        {
            _hud.ShowLocalizedMessage(
                "squad_revive_sync",
                "REVIVE SENT  //  WAITING FOR HOST CONFIRMATION",
                OperatorRoles.Spec(OperatorRole.Medic).Accent);
        }
        else if (revived)
        {
            ResetAiReviveAbandonment();
            if (ReferenceEquals(target, _player) || target is TacticalPlayer)
            {
                OnLocalPlayerRevived();
            }
            _hud.ShowLocalizedMessage(
                "squad_revive",
                "MANUAL REVIVE  //  TEAMMATE STABILIZED",
                OperatorRoles.Spec(OperatorRole.Medic).Accent);
            SpawnMedicSprayEffect(_player.GlobalPosition + Vector3.Up * 1.2f, target.HitPoint(HitRegion.Torso));
        }
        else
        {
            _hud.ShowLocalizedMessage(
                "revive_exhausted",
                "REVIVE EXHAUSTED  //  NO SECOND CHANCE",
                new Color(1.0f, 0.42f, 0.28f));
        }
    }

    private void CancelManualRevive()
    {
        if (_manualReviveProgress > 0.0f)
        {
            _player.SetSearchPose(false);
        }
        _manualReviveProgress = 0.0f;
        _manualReviveTarget = null;
    }

    private void IssueSquadOrder(SquadOrder order)
    {
        if (!_squadDeployed)
        {
            return;
        }
        _squadOrder = order;
        if (order == SquadOrder.Move)
        {
            _squadMovePoint = _player.GetAimPoint(65.0f);
            ShowSquadMoveMarker(_squadMovePoint);
        }
        else if (order == SquadOrder.Hold)
        {
            _squadMovePoint = _player.GlobalPosition;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.SetOrder(order, order == SquadOrder.Hold ? mate.GlobalPosition : _squadMovePoint);
            }
        }
        _hud.SetSquadOrder(order);
        var accent = order == SquadOrder.Move ? new Color(0.3f, 0.76f, 1.0f) : new Color(0.38f, 0.9f, 0.68f);
        _hud.ShowLocalizedMessage("squad_order", $"SQUAD ORDER  //  {OperatorRoles.Spec(_player.Role).Name} LEAD  //  {order.ToString().ToUpperInvariant()}", accent);
    }

    private void ShowSquadMoveMarker(Vector3 point)
    {
        if (IsInstanceValid(_squadMoveMarker))
        {
            _squadMoveMarker!.QueueFree();
        }
        _squadMoveMarker = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 0.035f, RadialSegments = 28 },
            Position = point + Vector3.Up * 0.05f,
            MaterialOverride = EffectMaterial(new Color(0.24f, 0.75f, 1.0f, 0.75f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_squadMoveMarker);
        var marker = _squadMoveMarker;
        var tween = CreateTween().SetLoops(7);
        tween.TweenProperty(marker, "scale", new Vector3(1.45f, 1.0f, 1.45f), 0.35f);
        tween.TweenProperty(marker, "scale", Vector3.One, 0.35f);
        var cleanup = CreateTween();
        cleanup.TweenInterval(5.0f);
        cleanup.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(marker))
            {
                marker.QueueFree();
            }
        }));
    }

    private void RefreshSquadHud()
    {
        if (!IsInstanceValid(_hud) || !_squadDeployed)
        {
            return;
        }
        var views = new List<SquadMemberView>
        {
            new(
                "RAVEN",
                _player.Role,
                _player.Health,
                _player.MaxHealth,
                true,
                _player.IsDead,
                _squadOrder,
                _player.SkillCooldownRemaining,
                _player.SkillCooldownDuration)
        };
        views.AddRange(_squadMates
            .Where(IsInstanceValid)
            .OrderBy(mate => mate.SquadSlot)
            .Select(mate => new SquadMemberView(
                mate.Callsign,
                mate.Role,
                mate.Health,
                mate.MaxHealth,
                mate.IsHumanProxy,
                mate.IsDowned,
                mate.Order,
                mate.SkillCooldownRemaining,
                mate.SkillCooldownDuration)));
        _hud.SetSquadRoster(views);
    }

    public EnemyOperator? FindNearestEnemy(Vector3 origin, float range)
    {
        EnemyOperator? nearest = null;
        var bestDistance = range * range;
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead)
            {
                continue;
            }
            var distance = origin.DistanceSquaredTo(enemy.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = enemy;
            }
        }
        return nearest;
    }

    public IEnumerable<EnemyOperator> EnumerateSquadEnemies()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy) && !enemy.IsDead && CanSquadEngage(enemy))
            {
                yield return enemy;
            }
        }
    }

    public bool CanSquadEngage(EnemyOperator enemy)
    {
        if (_missionDirector.IsDeploymentProtected())
        {
            return false;
        }
        return enemy.IsWorldBoss || enemy.Alerted || _missionPhase is "CONTACT" or "COMBAT";
    }

    public ISquadCombatant? FindNearestFriendly(Vector3 origin)
    {
        ISquadCombatant? nearest = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var friendly in FriendlyCombatants())
        {
            if (friendly.CombatDead || friendly is SquadMate { IsHumanProxy: true })
            {
                continue;
            }
            var distance = origin.DistanceSquaredTo(friendly.CombatNode.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = friendly;
            }
        }
        return nearest;
    }

    public ISquadCombatant? FindLowestFriendly(float healthRatio, bool includeDowned)
    {
        ISquadCombatant? lowest = null;
        var bestRatio = healthRatio;
        foreach (var friendly in FriendlyCombatants())
        {
            if (friendly.CombatDead && !includeDowned)
            {
                continue;
            }
            var ratio = friendly.CombatHealth / Mathf.Max(1.0f, friendly.CombatMaxHealth);
            if (friendly.CombatDead)
            {
                ratio = -1.0f;
            }
            if (ratio <= bestRatio)
            {
                bestRatio = ratio;
                lowest = friendly;
            }
        }
        return lowest;
    }

    private IEnumerable<ISquadCombatant> FriendlyCombatants()
    {
        if (IsInstanceValid(_player))
        {
            yield return _player;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                yield return mate;
            }
        }
    }

    /// <summary>Revivable player-squad targets that hostile operators may secure at close range.</summary>
    public IEnumerable<Node3D> EnumerateDownedSquadTargets()
    {
        if (IsPlayerProtected())
        {
            yield break;
        }
        if (IsInstanceValid(_player) && _player.CombatDowned && _player.CanBeRevived)
        {
            yield return _player;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate) && mate.CombatDowned && mate.CanBeRevived)
            {
                yield return mate;
            }
        }
    }

    public void ApplyMedicSpray(ISquadCombatant source, Vector3 origin, Vector3 forward)
    {
        ISquadCombatant? target = null;
        var bestScore = float.PositiveInfinity;
        var sourceNode = source.CombatNode;
        var normalizedForward = forward.Normalized();
        foreach (var friendly in FriendlyCombatants())
        {
            var offset = friendly.CombatNode.GlobalPosition - origin;
            var distance = offset.Length();
            if (friendly == source)
            {
                var selfRatio = friendly.CombatHealth / Mathf.Max(1.0f, friendly.CombatMaxHealth);
                if (selfRatio < 0.96f)
                {
                    var selfScore = selfRatio * 4.0f + 2.0f;
                    if (selfScore < bestScore)
                    {
                        bestScore = selfScore;
                        target = friendly;
                    }
                }
                continue;
            }
            if (distance > 8.0f)
            {
                continue;
            }
            var alignment = distance <= 0.01f ? 1.0f : normalizedForward.Dot(offset / distance);
            if (alignment < 0.3f)
            {
                continue;
            }
            var ratio = friendly.CombatDead ? -1.0f : friendly.CombatHealth / Mathf.Max(1.0f, friendly.CombatMaxHealth);
            var score = ratio * 4.0f + distance * 0.08f - alignment;
            if (score < bestScore && (ratio < 0.99f || friendly.CombatDead))
            {
                bestScore = score;
                target = friendly;
            }
        }

        target ??= source;
        var targetPoint = target.HitPoint(HitRegion.Torso);
        var wasDown = target.CombatDowned || target.CombatDead;
        var revived = false;
        if (wasDown)
        {
            // Medic spray still requires the once-per-life revive budget.
            revived = target.TryReceiveRevive(58.0f);
            if (revived)
            {
                ResetAiReviveAbandonment();
            }
            if (revived && target is TacticalPlayer)
            {
                OnLocalPlayerRevived();
            }
        }
        else
        {
            target.RestoreHealth(44.0f);
        }
        if (target != source && !source.CombatDead)
        {
            source.RestoreHealth(18.0f);
        }
        SpawnMedicSprayEffect(origin, targetPoint);
        if (wasDown && !revived)
        {
            _hud.ShowLocalizedMessage(
                "revive_exhausted",
                "REVIVE EXHAUSTED  //  NO SECOND CHANCE",
                new Color(1.0f, 0.42f, 0.28f));
        }
        else
        {
            _hud.ShowLocalizedMessage(
                wasDown ? "squad_revive" : "medic_spray",
                wasDown ? "MEDIC SPRAY  //  SQUADMATE REVIVED" : "MEDIC SPRAY  //  TRAUMA STABILIZED",
                OperatorRoles.Spec(OperatorRole.Medic).Accent);
        }
    }

    public void ApplyAircraftStrike(Vector3 impact, float radius, float damage, Node source)
    {
        foreach (var friendly in FriendlyCombatants().ToArray())
        {
            if (!IsInstanceValid(friendly.CombatNode))
            {
                continue;
            }
            var distance = friendly.CombatNode.GlobalPosition.DistanceTo(impact);
            if (distance > radius)
            {
                continue;
            }
            var falloff = 1.0f - distance / Mathf.Max(0.01f, radius);
            friendly.TakeCombatDamage(damage * falloff, impact, source);
        }
    }

    public void PerformReconScan(ISquadCombatant source, Vector3 origin)
    {
        var scanRange = _demolitionMode ? DemolitionReconScanRange : 72.0f;
        var revealed = 0;
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead || enemy.GlobalPosition.DistanceTo(origin) > scanRange)
            {
                continue;
            }
            enemy.SetScanned(10.0f);
            revealed++;
        }
        SpawnReconPulse(origin, scanRange);
        _hud.ShowLocalizedMessage(
            "recon_scan",
            $"PULSE SCAN  //  {revealed:00} HOSTILES REVEALED",
            OperatorRoles.Spec(OperatorRole.Recon).Accent);
    }

    public void SpawnRoleActivationPulse(Vector3 position, Color color, float radius)
    {
        var pulse = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f, RadialSegments = 12, Rings = 6 },
            Position = position,
            MaterialOverride = EffectMaterial(new Color(color.R, color.G, color.B, 0.55f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(pulse);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(pulse, "scale", Vector3.One * radius, 0.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(pulse, "transparency", 1.0f, 0.55f);
        tween.Chain().TweenCallback(Callable.From(pulse.QueueFree));
    }

    private void SpawnMedicSprayEffect(Vector3 origin, Vector3 target)
    {
        var root = new Node3D { Name = "MedicSprayEffect" };
        AddChild(root);
        var sprayDirection = origin.DirectionTo(target);
        var sprayStart = origin + sprayDirection * 0.08f;
        var mistMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.28f, 0.95f, 0.58f, 0.26f),
            EmissionEnabled = true,
            Emission = new Color(0.18f, 0.65f, 0.38f),
            EmissionEnergyMultiplier = 0.55f
        };
        var jetLength = sprayStart.DistanceTo(target);
        var jet = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.008f,
                BottomRadius = 0.016f,
                Height = jetLength,
                RadialSegments = 8
            },
            Position = sprayStart.Lerp(target, 0.5f),
            Quaternion = new Quaternion(Vector3.Up, sprayDirection),
            MaterialOverride = mistMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        root.AddChild(jet);
        var jetTween = CreateTween();
        jetTween.TweenInterval(0.14f);
        jetTween.TweenProperty(jet, "transparency", 1.0f, 0.3f);
        for (var i = 0; i < 14; i++)
        {
            var t = (i + 1) / 14.0f;
            var mist = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.006f + t * 0.012f, Height = 0.016f + t * 0.024f, RadialSegments = 7, Rings = 4 },
                Position = sprayStart.Lerp(target, t * 0.08f),
                MaterialOverride = mistMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            root.AddChild(mist);
            var scatterRadius = 0.025f + t * 0.16f;
            var scatter = new Vector3(
                _rng.RandfRange(-scatterRadius, scatterRadius),
                _rng.RandfRange(-scatterRadius * 0.55f, scatterRadius),
                _rng.RandfRange(-scatterRadius, scatterRadius));
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(mist, "position", sprayStart.Lerp(target, t) + scatter, 0.34f + t * 0.18f).SetDelay(i * 0.016f);
            tween.TweenProperty(mist, "transparency", 1.0f, 0.38f).SetDelay(0.2f + i * 0.016f);
        }
        var cleanup = CreateTween();
        cleanup.TweenInterval(1.2f);
        cleanup.TweenCallback(Callable.From(root.QueueFree));
    }

    private void SpawnReconPulse(Vector3 origin, float scanRange)
    {
        for (var i = 0; i < 3; i++)
        {
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.985f, OuterRadius = 1.0f, Rings = 48, RingSegments = 5 },
                Position = origin + Vector3.Up * (0.04f + i * 0.09f),
                MaterialOverride = EffectMaterial(new Color(0.24f, 0.68f, 1.0f, 0.28f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(ring);
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(ring, "scale", Vector3.One * (scanRange * 0.4f), 1.0f).SetDelay(i * 0.12f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "transparency", 1.0f, 1.05f).SetDelay(i * 0.12f);
            tween.Chain().TweenCallback(Callable.From(ring.QueueFree));
        }
    }

    private static StandardMaterial3D EffectMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = 2.5f
        };
    }

    private void DamageSquadFromExplosion(Vector3 position, float radius, float maxDamage, Node? source)
    {
        foreach (var mate in _squadMates.ToArray())
        {
            if (!IsInstanceValid(mate) || mate.IsDowned)
            {
                continue;
            }
            var distance = mate.GlobalPosition.DistanceTo(position);
            if (distance < radius)
            {
                mate.TakeCombatDamage(maxDamage * 0.72f * (1.0f - distance / radius), position, source);
            }
        }
    }

    public void OnSquadMateDowned(SquadMate mate)
    {
        ResetAiReviveAbandonment();
        if (_demolitionMode)
        {
            mate.EliminateForDemolitionRound();
            _hud.ShowLocalizedMessage(
                "demolition_teammate_eliminated",
                $"{mate.Callsign} ELIMINATED  //  OUT FOR THIS ROUND",
                new Color(1.0f, 0.34f, 0.22f));
            return;
        }
        _hud.ShowLocalizedMessage("squadmate_down", $"{mate.Callsign} DOWN  //  HOLD F TO REVIVE", new Color(1.0f, 0.34f, 0.22f));
    }

    public void OnSquadMateKia(SquadMate mate)
    {
        _hud.ShowLocalizedMessage(
            "squadmate_kia",
            $"{mate.Callsign} KIA  //  BODY BAG RECOVERABLE",
            new Color(1.0f, 0.22f, 0.16f));
    }

    public void SpawnSquadBodyBag(SquadMate mate)
    {
        if (!IsInstanceValid(mate))
        {
            return;
        }

        var bag = new SquadBodyBag
        {
            Name = $"BodyBag_{mate.Callsign}",
            Position = mate.GlobalPosition + Vector3.Up * 0.05f,
            EnglishName = $"{mate.Callsign} body bag",
            ChineseName = $"{mate.Callsign} 遗体袋"
        };
        if (IsExtractionNetworkMatch && _squadNetwork.IsHost)
        {
            var flags = ExtractionSquadNetworkFlags.Down
                | ExtractionSquadNetworkFlags.BodyBag
                | ExtractionSquadNetworkFlags.ReviveUsed;
            if (mate.IsHumanProxy)
            {
                flags |= ExtractionSquadNetworkFlags.Human;
            }
            _extractionSquadTombstones[mate.SquadSlot] = new ExtractionSquadNetworkState(
                mate.SquadSlot,
                mate.IsHumanProxy ? mate.NetworkPeerId : 0,
                mate.Role,
                mate.GlobalPosition,
                mate.Rotation,
                0.0f,
                (int)flags);
        }
        // Light field kit left on the fallen operator.
        bag.Loot.Add(new LootItem { Kind = LootItemKind.Ammunition, Quantity = 30 });
        bag.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
        if (_rng.Randf() < 0.45f)
        {
            bag.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
        }
        AddChild(bag);
        _lootSources.Add(bag);
        if (IsExtractionNetworkMatch)
        {
            var sourceId = SquadBodyBagSourceBase + mate.SquadSlot;
            RegisterExtractionLootSource(bag, sourceId);
            if (_squadNetwork.IsHost)
            {
                _squadNetwork.BroadcastExtractionLootState(
                    CaptureExtractionLootSourceState(sourceId, bag, granted: false));
            }
        }
        _squadMates.Remove(mate);
    }

    public void SpawnAircraftShell(Vector3 from, Vector3 to, float damage, float blastRadius, Node owner)
    {
        var shell = new AircraftShell
        {
            Name = "HostileAircraftShell",
            Main = this,
            OwnerAircraft = owner,
            Position = from
        };
        AddChild(shell);
        shell.Launch(from, to, damage, blastRadius);
    }

    private bool HandleLocalPlayerDowned()
    {
        if (_demolitionMode)
        {
            return false;
        }
        _player.EjectFromVehicleIfAny();
        var livingMate = _squadMates.Any(mate => IsInstanceValid(mate) && !mate.IsDowned);
        // Second life already used, or nobody left to revive → hard fail path.
        if (_player.ReviveUsed || !_squadDeployed || !livingMate)
        {
            return false;
        }
        if (_hud.IsLootVisible)
        {
            CloseLoot();
        }
        ResetAiReviveAbandonment();
        _localPlayerDowned = true;
        _localPlayerDownedTimer = 0.0f;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        BeginSquadMateView();
        _hud.ShowDownedState(22.0f);
        return true;
    }

    private bool TryBeginLocalPlayerElimination()
    {
        var spectatorTarget = FindLivingSpectatorTarget();
        if (_missionEnded
            || !_squadDeployed
            || spectatorTarget is null && !ShouldObservePlantedDemolitionDevice())
        {
            return false;
        }

        _localPlayerDowned = false;
        _localPlayerEliminated = true;
        _localPlayerDownedTimer = 0.0f;
        _spectatedMate = null;
        ClearLeaderReviveAi();
        if (_demolitionMode)
        {
            _player.MarkEliminatedForDemolitionRound();
        }
        _player.EjectFromVehicleIfAny();
        if (_hud.IsLootVisible)
        {
            CloseLoot();
        }
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _hud.HideDownedState();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        if (spectatorTarget is not null)
        {
            BeginSquadMateView();
        }
        else
        {
            BeginDemolitionObjectiveView();
        }
        return true;
    }

    public void OnLocalPlayerRevived()
    {
        ResetAiReviveAbandonment();
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        _localPlayerDownedTimer = 0.0f;
        _player.UiLocked = false;
        _player.DisarmFireInput();
        _player.RestoreMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        RestoreLocalPlayerView();
        _hud.HideDownedState();
        _hud.ShowLocalizedMessage("player_revived", "REVIVED  //  BACK IN THE FIGHT", OperatorRoles.Spec(OperatorRole.Medic).Accent);
        ClearLeaderReviveAi();
    }

    public void OnLocalPlayerFinishedByHostile()
    {
        if (!_localPlayerDowned || !_player.IsDead)
        {
            return;
        }
        if (!TryBeginLocalPlayerElimination())
        {
            FailSquadMission();
        }
    }

    private void FailSquadMission()
    {
        if (_missionEnded)
        {
            return;
        }
        if (_demolitionMode)
        {
            FinishDemolitionRound(
                false,
                GameLocalization.Get(
                    "demolition_squad_eliminated",
                    _languageSetting,
                    "SQUAD ELIMINATED"));
            return;
        }
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        ClearLeaderReviveAi();
        _player.EjectFromVehicleIfAny();
        _hud.HideDownedState();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(false, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowResult(false);
    }

    private async void ValidateSquadFlow()
    {
        await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.ProcessMode = ProcessModeEnum.Disabled;
            _aircraft.SetPhysicsProcess(false);
        }
        var aircraftShellNodes = GetTree().GetNodesInGroup("aircraft_shells");
        using var aircraftShellNodesBacking = aircraftShellNodes.AsDisposable();
        foreach (var node in aircraftShellNodes)
        {
            if (node is AircraftShell shell && IsInstanceValid(shell))
            {
                shell.QueueFree();
            }
        }
        var defaultFollow = _squadMates.All(mate => mate.Order == SquadOrder.Follow);
        var cooldownMate = _squadMates.First(mate => !mate.IsHumanProxy);
        cooldownMate.SetSkillCooldownForDiagnostics(0.0f);
        var firstAiSkill = cooldownMate.TriggerRoleAbility(_player.GlobalPosition);
        var repeatedAiSkillBlocked = !cooldownMate.TriggerRoleAbility(_player.GlobalPosition);
        var aiCooldownEnforced = firstAiSkill
            && repeatedAiSkillBlocked
            && cooldownMate.SkillCooldownRemaining > OperatorRoles.Spec(cooldownMate.Role).SkillCooldown;
        _player.ConfigureRole(OperatorRole.Medic);
        _player.SetHealthForDiagnostics(72.0f);
        var healthBefore = _player.Health;
        _player.ActivateRoleAbility(false);
        await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);
        var medicSelf = _player.Health > healthBefore;

        _player.ConfigureRole(OperatorRole.Recon);
        _player.ActivateRoleAbility(false);
        await ToSignal(GetTree().CreateTimer(0.9f), SceneTreeTimer.SignalName.Timeout);
        var scanned = _enemies.Count(enemy => enemy.IsScanned);

        _player.ConfigureRole(OperatorRole.Assault);
        _player.ActivateRoleAbility(false);
        var assaultSpeed = _player.RoleMovementMultiplier;
        var assaultFire = _player.RoleFireIntervalMultiplier;
        IssueSquadOrder(SquadOrder.Hold);
        var hold = _squadMates.Where(mate => !mate.IsHumanProxy).All(mate => mate.Order == SquadOrder.Hold);
        IssueSquadOrder(SquadOrder.Move);
        var move = _squadMates.Where(mate => !mate.IsHumanProxy).All(mate => mate.Order == SquadOrder.Move);
        IssueSquadOrder(SquadOrder.Follow);
        var follow = _squadMates.Where(mate => !mate.IsHumanProxy).All(mate => mate.Order == SquadOrder.Follow);
        var follower = _squadMates.First(mate => !mate.IsHumanProxy);
        follower.GlobalPosition = _player.GlobalPosition + new Vector3(12.0f, 0.1f, 0.0f);
        var followDistanceBefore = follower.GlobalPosition.DistanceTo(_player.GlobalPosition);
        await ToSignal(GetTree().CreateTimer(0.65f), SceneTreeTimer.SignalName.Timeout);
        var followMotion = follower.GlobalPosition.DistanceTo(_player.GlobalPosition) < followDistanceBefore - 0.5f;

        // 3-operator fill: player Assault → AI must be Medic + Recon (no third AI).
        _player.ConfigureRole(OperatorRole.Assault, refillHealth: true);
        EnsureAiSquadFill();
        var aiRoles = _squadMates.Where(mate => IsInstanceValid(mate) && !mate.IsHumanProxy).Select(mate => mate.Role).OrderBy(role => role).ToArray();
        var roleFillOk = ActiveSquadCount == 3
            && AiSquadCount == 2
            && aiRoles.Length == 2
            && aiRoles.Contains(OperatorRole.Medic)
            && aiRoles.Contains(OperatorRole.Recon)
            && !aiRoles.Contains(OperatorRole.Assault);

        var squadStairClimbed = false;
        var squadStairGain = 0.0f;
        var squadStairStepUps = 0;
        if (_residentialTowers.Count > 0)
        {
            var stairMate = _squadMates.First(mate => IsInstanceValid(mate) && !mate.IsHumanProxy);
            var otherMateStates = _squadMates
                .Where(mate => IsInstanceValid(mate) && mate != stairMate)
                .Select(mate => (Mate: mate, Position: mate.GlobalPosition, Mode: mate.ProcessMode))
                .ToList();
            var enemyStates = _enemies
                .Where(IsInstanceValid)
                .Select(enemy => (Enemy: enemy, Position: enemy.GlobalPosition, Mode: enemy.ProcessMode))
                .ToList();
            for (var index = 0; index < otherMateStates.Count; index++)
            {
                var state = otherMateStates[index];
                state.Mate.ProcessMode = ProcessModeEnum.Disabled;
                state.Mate.GlobalPosition = new Vector3(330.0f + index * 3.0f, 0.3f, 330.0f);
            }
            for (var index = 0; index < enemyStates.Count; index++)
            {
                var state = enemyStates[index];
                state.Enemy.ProcessMode = ProcessModeEnum.Disabled;
                state.Enemy.GlobalPosition = new Vector3(360.0f + index * 2.0f, 0.3f, 360.0f);
            }

            var stairTower = _residentialTowers[0];
            var stairSpec = ResidentialTowerSpecs[0];
            var stairCoreZ = -Mathf.Min(stairSpec.Footprint.Y * 0.18f, 3.6f);
            var stairStart = stairTower.ToGlobal(new Vector3(
                -1.45f,
                0.25f,
                stairCoreZ + ResidentialStairRun * 0.5f - 0.25f));
            var stairTarget = stairTower.ToGlobal(new Vector3(
                -1.45f,
                ResidentialFloorHeight * 0.5f + 0.25f,
                stairCoreZ - ResidentialStairRun * 0.2f));
            stairMate.ProcessMode = ProcessModeEnum.Disabled;
            stairMate.GlobalPosition = stairStart;
            stairMate.Velocity = Vector3.Zero;
            stairMate.ResetCombatTacticsForDiagnostics();
            stairMate.GrantFireablePrimaryForDiagnostics();
            stairMate.SetOrder(SquadOrder.Move, stairTarget);
            await WaitFrames(3);
            var stairStartY = stairMate.GlobalPosition.Y;
            var stairStepUpsBefore = stairMate.NavigationStepUpsForDiagnostics;
            stairMate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 360 && stairMate.GlobalPosition.Y - stairStartY <= 0.8f; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            squadStairGain = stairMate.GlobalPosition.Y - stairStartY;
            squadStairStepUps = stairMate.NavigationStepUpsForDiagnostics - stairStepUpsBefore;
            squadStairClimbed = squadStairGain > 0.70f && squadStairStepUps >= 4;

            stairMate.SetOrder(SquadOrder.Follow, _player.GlobalPosition);
            stairMate.GlobalPosition = _player.GlobalPosition + new Vector3(3.0f, 0.1f, 1.0f);
            stairMate.Velocity = Vector3.Zero;
            stairMate.ResetCombatTacticsForDiagnostics();
            foreach (var state in otherMateStates)
            {
                state.Mate.GlobalPosition = state.Position;
                state.Mate.ProcessMode = state.Mode;
            }
            foreach (var state in enemyStates)
            {
                state.Enemy.GlobalPosition = state.Position;
                state.Enemy.ProcessMode = state.Mode;
            }
            await WaitFrames(3);
        }

        // Leave deployment protection so live damage/down paths exercise shipped code.
        _missionDirector.ExitDeploymentZone();
        await WaitFrames(4);

        var combatMate = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && !mate.IsHumanProxy);
        var combatEnemy = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy) && !enemy.IsDead && !enemy.IsWorldBoss)
            ?? _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        var combatWallBlocked = false;
        var combatTargetLocked = false;
        var combatFlanked = false;
        var combatSightRecovered = false;
        var combatFired = false;
        var combatDamaged = false;
        var combatFacedMovement = false;
        var closeRangeRetreat = false;
        var closeRangeStrafe = false;
        if (combatMate is not null && combatEnemy is not null)
        {
            foreach (var enemy in _enemies)
            {
                if (!IsInstanceValid(enemy))
                {
                    continue;
                }
                enemy.ProcessMode = ProcessModeEnum.Disabled;
                if (enemy != combatEnemy)
                {
                    enemy.GlobalPosition = new Vector3(220.0f, 0.3f, 220.0f);
                }
            }
            foreach (var squadMate in _squadMates)
            {
                if (!IsInstanceValid(squadMate))
                {
                    continue;
                }
                squadMate.ProcessMode = ProcessModeEnum.Disabled;
                if (squadMate != combatMate)
                {
                    squadMate.GlobalPosition = new Vector3(205.0f, 0.3f, 205.0f);
                }
            }
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = new Vector3(210.0f, 0.3f, 210.0f);

            var combatOrigin = new Vector3(0.0f, 0.3f, 52.0f);
            var combatTargetPosition = new Vector3(0.0f, 0.3f, 68.0f);
            combatMate.GlobalPosition = combatOrigin;
            combatMate.Velocity = Vector3.Zero;
            combatMate.ResetCombatTacticsForDiagnostics();
            combatMate.GrantFireablePrimaryForDiagnostics();
            combatMate.SetOrder(SquadOrder.Move, combatOrigin);
            combatEnemy.ResetTacticalStateForDiagnostics();
            combatEnemy.GrantFireablePrimaryForDiagnostics();
            combatEnemy.GlobalPosition = combatTargetPosition;
            combatEnemy.SetAlerted(combatOrigin);
            combatEnemy.ProcessMode = ProcessModeEnum.Inherit;
            combatEnemy.SetPhysicsProcess(false);

            var combatWall = new StaticBody3D
            {
                Name = "SquadCombatFlankWall",
                Position = new Vector3(0.0f, 1.8f, 60.0f),
                CollisionLayer = 1,
                CollisionMask = 0
            };
            combatWall.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(5.6f, 3.6f, 0.55f) }
            });
            AddChild(combatWall);
            combatMate.ProcessMode = ProcessModeEnum.Inherit;
            await WaitFrames(3);

            combatWallBlocked = !combatMate.HasCombatLineOfSightForDiagnostics(combatEnemy);
            var wallStart = combatMate.GlobalPosition;
            var enemyHealthBefore = combatEnemy.CurrentHealth;
            var shotsBeforeWall = combatMate.CombatShotsFired;
            var maxWallLateral = 0.0f;
            var movementFacingSamples = 0;
            var coherentFacingSamples = 0;
            for (var frame = 0; frame < 300 && !combatEnemy.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                combatTargetLocked |= combatMate.CombatTargetForDiagnostics == combatEnemy;
                combatSightRecovered |= combatMate.CombatHasSightForDiagnostics;
                maxWallLateral = Mathf.Max(maxWallLateral, Mathf.Abs(combatMate.GlobalPosition.X - wallStart.X));
                var planarVelocity = new Vector3(combatMate.Velocity.X, 0.0f, combatMate.Velocity.Z);
                if (!combatMate.CombatHasSightForDiagnostics && planarVelocity.LengthSquared() > 0.16f)
                {
                    movementFacingSamples++;
                    var forward = -combatMate.GlobalBasis.Z;
                    forward.Y = 0.0f;
                    if (forward.Normalized().Dot(planarVelocity.Normalized()) > 0.42f)
                    {
                        coherentFacingSamples++;
                    }
                }
            }
            var traceFrom = combatMate.GlobalPosition + Vector3.Up * 1.55f;
            var traceTo = combatEnemy.GlobalPosition + Vector3.Up * 1.05f;
            var hasTraceHit = PhysicsRaycast.TryHit(
                GetWorld3D().DirectSpaceState,
                traceFrom,
                traceTo,
                combatMate.GetRid(),
                uint.MaxValue,
                out var traceHit);
            var traceCollider = hasTraceHit && traceHit.Collider is Node traceNode
                ? traceNode.Name.ToString()
                : "none";
            var tracePosition = hasTraceHit ? traceHit.Position : Vector3.Zero;
            GD.Print($"SQUAD_COMBAT_WALL_TRACE pos=({combatMate.GlobalPosition.X:0.00},{combatMate.GlobalPosition.Z:0.00}) flank=({combatMate.CombatFlankPositionForDiagnostics.X:0.00},{combatMate.CombatFlankPositionForDiagnostics.Z:0.00}) lateral={maxWallLateral:0.00} sight={combatMate.CombatHasSightForDiagnostics} ray={traceCollider}@({tracePosition.X:0.00},{tracePosition.Z:0.00}) shots={combatMate.CombatShotsFired - shotsBeforeWall} switches={combatMate.CombatTargetSwitches} flank_n={combatMate.CombatFlankSelections} stuck={combatMate.CombatStuckRecoveries}");
            combatFlanked = combatMate.CombatFlankSelections > 0 && maxWallLateral > 2.7f;
            combatFired = combatMate.CombatShotsFired > shotsBeforeWall;
            combatDamaged = combatEnemy.CurrentHealth < enemyHealthBefore - 0.01f || combatEnemy.IsDead;
            combatFacedMovement = movementFacingSamples > 0
                && coherentFacingSamples >= Mathf.CeilToInt(movementFacingSamples * 0.55f);

            combatWall.QueueFree();
            await WaitFrames(3);
            combatEnemy.ResetTacticalStateForDiagnostics();
            combatEnemy.GrantFireablePrimaryForDiagnostics();
            EnsureEnemyRegisteredForDiagnostics(combatEnemy);
            combatMate.GlobalPosition = combatOrigin;
            combatMate.Velocity = Vector3.Zero;
            combatMate.ResetCombatTacticsForDiagnostics();
            combatMate.GrantFireablePrimaryForDiagnostics();
            combatMate.SetOrder(SquadOrder.Move, combatOrigin);
            combatEnemy.GlobalPosition = combatOrigin + new Vector3(0.0f, 0.0f, 5.0f);
            combatEnemy.SetAlerted(combatOrigin);
            combatEnemy.ProcessMode = ProcessModeEnum.Inherit;
            combatEnemy.SetPhysicsProcess(false);
            var closeStartDistance = combatMate.GlobalPosition.DistanceTo(combatEnemy.GlobalPosition);
            var maxCloseDistance = closeStartDistance;
            var maxCloseLateral = 0.0f;
            combatMate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 120 && !combatEnemy.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                maxCloseDistance = Mathf.Max(
                    maxCloseDistance,
                    combatMate.GlobalPosition.DistanceTo(combatEnemy.GlobalPosition));
                maxCloseLateral = Mathf.Max(
                    maxCloseLateral,
                    Mathf.Abs(combatMate.GlobalPosition.X - combatOrigin.X));
            }
            closeRangeRetreat = maxCloseDistance > closeStartDistance + 1.2f;
            closeRangeStrafe = maxCloseLateral > 0.75f;
            combatEnemy.ResetTacticalStateForDiagnostics();
            combatEnemy.SetPhysicsProcess(true);
            combatEnemy.ProcessMode = ProcessModeEnum.Disabled;
            combatMate.ProcessMode = ProcessModeEnum.Disabled;
            _player.ProcessMode = ProcessModeEnum.Inherit;
        }
        var combatAiOk = combatWallBlocked
            && combatTargetLocked
            && combatFlanked
            && combatSightRecovered
            && combatFired
            && combatDamaged
            && combatFacedMovement
            && closeRangeRetreat
            && closeRangeStrafe;

        // Hostile operators retain a nearby downed target, pause briefly, then secure it
        // through the same ballistic fire path used during live combat.
        var finishShooter = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        var finishTarget = new SquadMate
        {
            Name = "DiagnosticFinishTarget",
            Position = new Vector3(0.0f, 0.3f, 60.0f)
        };
        finishTarget.Configure(this, _player, 9, OperatorRole.Assault, "TARGET");
        AddChild(finishTarget);
        finishTarget.SetOrder(SquadOrder.Hold, finishTarget.GlobalPosition);
        _squadMates.Add(finishTarget);
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            if (enemy != finishShooter)
            {
                enemy.GlobalPosition = new Vector3(220.0f, 0.3f, 220.0f);
            }
        }
        foreach (var squadMate in _squadMates)
        {
            if (!IsInstanceValid(squadMate) || squadMate == finishTarget)
            {
                continue;
            }
            squadMate.ProcessMode = ProcessModeEnum.Disabled;
            squadMate.GlobalPosition = new Vector3(180.0f + squadMate.SquadSlot * 3.0f, 0.3f, 180.0f);
        }
        _player.GlobalPosition = new Vector3(180.0f, 0.3f, 190.0f);

        var finishTargetDowned = finishTarget.TakeCombatDamage(
            999.0f,
            finishTarget.HitPoint(HitRegion.Torso),
            this) && finishTarget.CombatDowned;
        var finishTargetAcquired = false;
        var finishLockHeld = false;
        var finishShotFired = false;
        var finishConverted = false;
        if (finishShooter is not null)
        {
            finishShooter.ResetTacticalStateForDiagnostics();
            finishShooter.GrantFireablePrimaryForDiagnostics();
            finishShooter.SentryMode = true;
            finishShooter.GlobalPosition = new Vector3(0.0f, 0.3f, 50.0f);
            finishShooter.LookAt(finishTarget.GlobalPosition, Vector3.Up);
            finishShooter.SetAlerted(finishTarget.GlobalPosition);
            finishShooter.ProcessMode = ProcessModeEnum.Inherit;
            var shotsBeforeFinish = finishShooter.AttackShotsFired;
            for (var frame = 0; frame < 36; frame++)
            {
                finishShooter.ArmWeaponForDiagnostics();
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                finishTargetAcquired |= finishShooter.EngageTargetNode == finishTarget;
            }
            finishLockHeld = finishShooter.AttackShotsFired == shotsBeforeFinish;
            var bagsBeforeFinishNodes = GetTree().GetNodesInGroup("squad_body_bags");
            var bagsBeforeFinish = bagsBeforeFinishNodes.Count;
            bagsBeforeFinishNodes.AsDisposable().Dispose();
            for (var frame = 0; frame < 120 && _squadMates.Contains(finishTarget); frame++)
            {
                finishShooter.ArmWeaponForDiagnostics();
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            finishShotFired = finishShooter.AttackShotsFired > shotsBeforeFinish;
            var bagsAfterFinishNodes = GetTree().GetNodesInGroup("squad_body_bags");
            var bagsAfterFinish = bagsAfterFinishNodes.Count;
            bagsAfterFinishNodes.AsDisposable().Dispose();
            finishConverted = !_squadMates.Contains(finishTarget)
                && bagsAfterFinish > bagsBeforeFinish;
            finishShooter.ProcessMode = ProcessModeEnum.Disabled;
        }
        var aiFinishOk = finishTargetDowned
            && finishTargetAcquired
            && finishLockHeld
            && finishShotFired
            && finishConverted;
        foreach (var squadMate in _squadMates)
        {
            if (IsInstanceValid(squadMate))
            {
                squadMate.ProcessMode = ProcessModeEnum.Inherit;
            }
        }

        // Any living AI role must prioritize a nearby downed mate even while it has
        // ordinary enemy contact and is hurt, but not critically wounded.
        var mate = _squadMates.First(m => IsInstanceValid(m) && !m.IsHumanProxy);
        var mateReviver = _squadMates.First(m => IsInstanceValid(m) && !m.IsHumanProxy && m != mate);
        var mateRescueEnemy = finishShooter;
        var mateRescueOrigin = new Vector3(0.0f, 0.3f, 52.0f);
        ClearLeaderReviveAi();
        foreach (var enemy in _enemies.Where(IsInstanceValid))
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            if (!ReferenceEquals(enemy, mateRescueEnemy))
            {
                enemy.GlobalPosition = new Vector3(260.0f, 0.3f, 260.0f);
            }
        }
        foreach (var squadMate in _squadMates)
        {
            if (!IsInstanceValid(squadMate))
            {
                continue;
            }
            squadMate.ProcessMode = squadMate == mate || squadMate == mateReviver
                ? ProcessModeEnum.Inherit
                : ProcessModeEnum.Disabled;
        }
        _player.GlobalPosition = new Vector3(180.0f, 0.3f, 190.0f);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        mate.GlobalPosition = mateRescueOrigin;
        mate.Velocity = Vector3.Zero;
        mate.SetOrder(SquadOrder.Hold, mateRescueOrigin);
        mateReviver.GlobalPosition = mateRescueOrigin + new Vector3(-48.0f, 0.0f, 0.0f);
        mateReviver.Velocity = Vector3.Zero;
        mateReviver.ResetCombatTacticsForDiagnostics();
        mateReviver.ApplyColdStartUnarmed();
        mateReviver.SetOrder(SquadOrder.Move, mateReviver.GlobalPosition);
        mateReviver.RestoreHealth(mateReviver.MaxHealth);
        mateReviver.TakeCombatDamage(
            mateReviver.MaxHealth * 0.3f,
            mateReviver.HitPoint(HitRegion.Torso),
            this);
        mate.TakeCombatDamage(999.0f, mate.HitPoint(HitRegion.Torso), this);
        var mateDowned = mate.IsDowned && mate.CanBeRevived;
        var farSelectionStarted = Time.GetTicksUsec();
        UpdateSquadReviveAi(1.0f / 60.0f);
        var farSelectionMicroseconds = Time.GetTicksUsec() - farSelectionStarted;
        var farSelectionResponsive = farSelectionMicroseconds <= AiReviveSelectionMaximumMicroseconds;
        var farNavigationCost = EstimateSquadNavigationCost(mateReviver, mate.GlobalPosition);
        var farRescueBlocked = farSelectionResponsive
            && farNavigationCost > AiSquadmateReviveRange
            && _leaderReviver is null
            && _aiReviveTarget is null
            && !mateReviver.IsRevivingFriendly;

        ClearLeaderReviveAi();
        mateReviver.GlobalPosition = mateRescueOrigin + new Vector3(-7.0f, 0.0f, 0.0f);
        mateReviver.Velocity = Vector3.Zero;
        mateReviver.RestoreHealth(mateReviver.MaxHealth);
        mateReviver.TakeCombatDamage(
            mateReviver.MaxHealth * 0.7f,
            mateReviver.HitPoint(HitRegion.Torso),
            this);
        var criticalReviverHealth = mateReviver.Health / mateReviver.MaxHealth;
        UpdateSquadReviveAi(1.0f / 60.0f);
        var criticalRescueBlocked = criticalReviverHealth < AiReviveMinimumHealthRatio
            && _leaderReviver is null
            && _aiReviveTarget is null
            && !mateReviver.IsRevivingFriendly;

        ClearLeaderReviveAi();
        mateReviver.RestoreHealth(mateReviver.MaxHealth);
        mateReviver.TakeCombatDamage(
            mateReviver.MaxHealth * 0.3f,
            mateReviver.HitPoint(HitRegion.Torso),
            this);
        var nonCriticalReviverHealth = mateReviver.Health / mateReviver.MaxHealth is > 0.5f and < 0.8f;
        mateReviver.ProcessMode = ProcessModeEnum.Disabled;
        var mateRescueMaze = BuildSquadRescueMazeForDiagnostics(
            out var reverseTargetPosition,
            out var reverseReviverPosition,
            out var forwardLeaderTrail);
        await WaitFrames(3);
        var eliminatedLeaderPosition = reverseReviverPosition + new Vector3(-4.0f, 0.0f, 0.0f);
        var reverseLeaderTrail = forwardLeaderTrail
            .Concat(forwardLeaderTrail.Reverse().Skip(1))
            .Append(eliminatedLeaderPosition)
            .ToArray();
        _player.GlobalPosition = eliminatedLeaderPosition;
        _player.Velocity = Vector3.Zero;
        mate.GlobalPosition = reverseTargetPosition;
        mate.Velocity = Vector3.Zero;
        mateReviver.GlobalPosition = reverseReviverPosition;
        mateReviver.Velocity = Vector3.Zero;
        mateReviver.SetOrder(SquadOrder.Move, reverseReviverPosition);
        SetSquadLeaderTrailForDiagnostics(reverseLeaderTrail);
        var reverseTrailDirectBlocked = !IsSquadMovementCorridorClear(
            reverseReviverPosition,
            reverseTargetPosition,
            mateReviver);
        var reverseTrailCost = EstimateSquadNavigationCost(mateReviver, reverseTargetPosition);
        var reverseTrailInRange = reverseTrailCost <= AiSquadmateReviveRange;
        _localPlayerEliminated = true;
        _player.UiLocked = true;
        if (mateRescueEnemy is not null)
        {
            mateRescueEnemy.ResetTacticalStateForDiagnostics();
            mateRescueEnemy.GlobalPosition = reverseReviverPosition + new Vector3(0.0f, 0.0f, -2.5f);
            mateRescueEnemy.SetAlerted(mateReviver.GlobalPosition);
            mateRescueEnemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        mateReviver.ProcessMode = ProcessModeEnum.Inherit;

        var holdBefore = mate.GlobalPosition;
        var mateRescueStartDistance = mateReviver.GlobalPosition.DistanceTo(mate.GlobalPosition);
        var mateRescueMinDistance = mateRescueStartDistance;
        var mateRescueAssigned = false;
        var mateRescueEnemyDetected = false;
        var mateHoldDistance = 0.0f;
        for (var frame = 0; frame < 600 && mate.IsDowned; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            mateRescueAssigned |= ReferenceEquals(_leaderReviver, mateReviver)
                && ReferenceEquals(_aiReviveTarget, mate)
                && mateReviver.IsRevivingTarget(mate);
            mateRescueEnemyDetected |= mateRescueEnemy is not null
                && mateReviver.CombatTargetForDiagnostics == mateRescueEnemy;
            mateRescueMinDistance = Mathf.Min(
                mateRescueMinDistance,
                mateReviver.GlobalPosition.DistanceTo(mate.GlobalPosition));
            if (mate.IsDowned)
            {
                mateHoldDistance = Mathf.Max(mateHoldDistance, mate.GlobalPosition.DistanceTo(holdBefore));
            }
        }
        var mateCrawled = mateHoldDistance < 0.35f;
        var firstRevive = !mate.IsDowned && mate.ReviveUsed;
        var mateUp = !mate.IsDowned && firstRevive;
        var mateRescueAfterElimination = _localPlayerEliminated && mateUp;
        var reverseTrailRescue = reverseTrailDirectBlocked
            && reverseTrailInRange
            && LeaderRescueUsedTrailForDiagnostics
            && mateUp;
        var aiMateRescueOk = mateDowned
            && farRescueBlocked
            && criticalRescueBlocked
            && nonCriticalReviverHealth
            && reverseTrailRescue
            && mateRescueAssigned
            && mateRescueEnemyDetected
            && mateRescueMinDistance < mateRescueStartDistance - 2.0f
            && mateRescueAfterElimination
            && mateUp;
        _localPlayerEliminated = false;
        _player.UiLocked = false;
        RestoreLocalPlayerView();
        mateRescueMaze.QueueFree();
        ResetSquadLeaderTrail(_player.GlobalPosition);
        await WaitFrames(2);
        // Second down after revive → permanent body bag (not a sliding human).
        var bagsBeforeNodes = GetTree().GetNodesInGroup("squad_body_bags");
        var bagsBefore = bagsBeforeNodes.Count;
        bagsBeforeNodes.AsDisposable().Dispose();
        var lootBefore = _lootSources.Count;
        mate.TakeCombatDamage(999.0f, mate.HitPoint(HitRegion.Torso), this);
        await WaitFrames(4);
        var bagsAfterNodes = GetTree().GetNodesInGroup("squad_body_bags");
        var bagsAfter = bagsAfterNodes.Count;
        bagsAfterNodes.AsDisposable().Dispose();
        var bodyBagOk = bagsAfter > bagsBefore
            || _lootSources.Count > lootBefore
            || _lootSources.Exists(source => source is SquadBodyBag);
        // Mate is freed when converted; second revive is impossible by design.
        var secondReviveBlocked = bodyBagOk || (IsInstanceValid(mate) && !mate.CanBeRevived);
        ClearLeaderReviveAi();
        mateReviver.GrantFireablePrimaryForDiagnostics();
        mateReviver.ProcessMode = ProcessModeEnum.Inherit;
        _player.ProcessMode = ProcessModeEnum.Inherit;

        // A thin wall must block the revive channel even inside the distance radius.
        // With no usable trail, the assignment must expire and let bleed-out resume.
        mateReviver.ProcessMode = ProcessModeEnum.Disabled;
        var blockedRescueMaze = BuildSquadRescueMazeForDiagnostics(
            out var blockedNorthPosition,
            out var blockedSouthPosition,
            out _);
        await WaitFrames(3);
        var blockedCenter = (blockedNorthPosition + blockedSouthPosition) * 0.5f;
        var blockedPlayerPosition = blockedCenter + new Vector3(0.0f, 0.0f, 0.85f);
        var blockedReviverPosition = blockedCenter + new Vector3(0.0f, 0.0f, -0.85f);
        _player.GlobalPosition = blockedPlayerPosition;
        _player.Velocity = Vector3.Zero;
        mateReviver.GlobalPosition = blockedReviverPosition;
        mateReviver.Velocity = Vector3.Zero;
        mateReviver.RestoreHealth(mateReviver.MaxHealth);
        mateReviver.SetOrder(SquadOrder.Hold, blockedReviverPosition);
        ResetSquadLeaderTrail(blockedReviverPosition);
        mateReviver.ProcessMode = ProcessModeEnum.Inherit;
        _player.SetHealthForDiagnostics(10.0f);
        _player.SetReviveUsedForDiagnostics(false);
        _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        if (!_player.IsDead)
        {
            _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        }
        var closeWallBlocked = !IsSquadMovementCorridorClear(
            blockedReviverPosition,
            blockedPlayerPosition,
            mateReviver);
        UpdateSquadReviveAi(0.5f);
        var wallRescueAssigned = ReferenceEquals(_leaderReviver, mateReviver)
            && ReferenceEquals(_aiReviveTarget, _player);
        var wallChannelBlocked = closeWallBlocked
            && wallRescueAssigned
            && _player.IsDead
            && _leaderReviveChannel <= 0.001f;
        var unreachableElapsed = 0.5f;
        for (var second = 0; second < 20 && !ReferenceEquals(_abandonedAiReviveTarget, _player); second++)
        {
            _ = ResolveSquadNavigationDestination(
                mateReviver,
                blockedPlayerPosition,
                emergency: true);
            UpdateSquadReviveAi(1.0f);
            unreachableElapsed += 1.0f;
        }
        var unreachableGridPlans = LeaderRescueGridPlansForDiagnostics;
        var unreachableGridUsed = LeaderRescueUsedGridForDiagnostics;
        var unreachableAbandoned = ReferenceEquals(_abandonedAiReviveTarget, _player)
            && _leaderReviver is null
            && _aiReviveTarget is null;
        var bleedBeforeAbandonUpdate = _localPlayerDownedTimer;
        UpdateSquad(1.0f);
        var bleedResumedAfterAbandon = _localPlayerDownedTimer >= bleedBeforeAbandonUpdate + 0.99f;
        var timeoutRevived = _player.TryReceiveRevive(50.0f);
        var abandonmentClearedOnRevive = timeoutRevived && _abandonedAiReviveTarget is null;
        _abandonedAiReviveTarget = _player;
        _player.SetHealthForDiagnostics(10.0f);
        _player.SetReviveUsedForDiagnostics(false);
        _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        if (!_player.IsDead)
        {
            _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        }
        var abandonmentClearedOnDown = _localPlayerDowned && _abandonedAiReviveTarget is null;
        var lifecycleCleanupRevived = _player.TryReceiveRevive(50.0f);
        blockedRescueMaze.QueueFree();
        ResetSquadLeaderTrail(_player.GlobalPosition);
        await WaitFrames(3);

        // AI leader revive: down the player and let the remaining AI mate run over and pick them up.
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        var reviverMate = _squadMates.FirstOrDefault(m => IsInstanceValid(m) && !m.IsHumanProxy && !m.IsDowned);
        var aiReviveOk = false;
        var rescueDirectBlocked = false;
        var followDetourReady = false;
        var rescueTrailUsed = false;
        var rescueWaypointAdvances = 0;
        var rescueReplans = 0;
        var squadMateViewOnDown = false;
        var downedInputLocked = false;
        var downedLootBlocked = false;
        var downedBackpackBlocked = false;
        var interruptedClimbLocked = false;
        var eliminatedLootBlocked = false;
        var eliminatedBackpackBlocked = false;
        var spectatorTracksMate = false;
        var downedBannerVisible = false;
        var playerViewAfterRevive = false;
        var interactionProbe = new ResidentialSupplyCache { Name = "SquadInteractionProbe" };
        interactionProbe.Configure(
            ResidentialCacheKind.FamilyStash,
            0,
            0,
            new[]
            {
                new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.CannedCoffee,
                    Grade = LootGrade.Common
                }
            });
        AddChild(interactionProbe);
        if (reviverMate is not null)
        {
            // Deterministic maze: the direct route is walled off, so rescue must use the side door.
            var rescueMaze = BuildSquadRescueMazeForDiagnostics(
                out var rescuePlayerPosition,
                out var rescueReviverPosition,
                out var rescueLeaderTrail);
            await WaitFrames(3);
            _player.GlobalPosition = rescuePlayerPosition;
            _player.Velocity = Vector3.Zero;
            reviverMate.GlobalPosition = rescueReviverPosition;
            reviverMate.Velocity = Vector3.Zero;
            reviverMate.ProcessMode = ProcessModeEnum.Inherit;
            reviverMate.SetOrder(SquadOrder.Follow, rescueReviverPosition);
            SetSquadLeaderTrailForDiagnostics(rescueLeaderTrail);
            rescueDirectBlocked = !IsSquadMovementCorridorClear(
                rescueReviverPosition,
                rescuePlayerPosition,
                reviverMate);
            var followDetour = ResolveSquadNavigationDestination(
                reviverMate,
                rescuePlayerPosition,
                emergency: false);
            followDetourReady = followDetour.Target.DistanceTo(rescuePlayerPosition) > 3.0f
                && followDetour.Target.X > rescueReviverPosition.X + 2.0f;
            var relayForDowned = _residentialRelayStations.FirstOrDefault(IsInstanceValid);
            if (relayForDowned is not null)
            {
                BeginRelayClimb(relayForDowned, descend: false);
            }
            var relayClimbStarted = relayForDowned is not null
                && ReferenceEquals(_relayClimbStation, relayForDowned)
                && _player.UiLocked;
            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            var aiReviveDowned = _player.IsDead && _localPlayerDowned;
            if (relayClimbStarted)
            {
                UpdateRelayClimb(1.0f / 60.0f);
                interruptedClimbLocked = _relayClimbStation is null && _player.UiLocked;
            }
            var openEventsBeforeDowned = interactionProbe.OpenEventCount;
            OpenLoot(interactionProbe);
            downedLootBlocked = interactionProbe.OpenEventCount == openEventsBeforeDowned
                && _openLootSource is null
                && !_hud.IsLootVisible
                && _player.UiLocked;
            OpenPersonalBackpack();
            downedBackpackBlocked = !_personalBackpackOpen
                && !_hud.IsLootVisible
                && _player.UiLocked;
            squadMateViewOnDown = IsSquadMateViewCurrent
                && ReferenceEquals(_spectatedMate, reviverMate);
            downedBannerVisible = _hud.IsDownedBannerVisible;
            var downedHoldPosition = _player.GlobalPosition;
            var downedWasLocked = _player.UiLocked;
            Input.ActionPress("move_forward");
            await WaitFrames(12);
            Input.ActionRelease("move_forward");
            downedInputLocked = downedWasLocked
                && _player.UiLocked
                && HorizontalDistance(_player.GlobalPosition, downedHoldPosition) < 0.08f;
            UpdateSquadSpectatorCamera();
            if (_squadSpectatorCamera is not null && IsInstanceValid(_squadSpectatorCamera))
            {
                var spectatedBasis = reviverMate.GlobalBasis.Orthonormalized();
                var expectedCameraPosition = reviverMate.GlobalPosition
                    + Vector3.Up * 1.64f
                    - spectatedBasis.Z * 0.28f;
                spectatorTracksMate = IsSquadMateViewCurrent
                    && _squadSpectatorCamera.GlobalPosition.DistanceTo(expectedCameraPosition) < 0.20f;
            }
            for (var second = 0; second < 16 && _player.IsDead; second++)
            {
                await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
            }
            rescueTrailUsed = LeaderRescueUsedTrailForDiagnostics;
            rescueWaypointAdvances = LeaderRescueWaypointAdvancesForDiagnostics;
            rescueReplans = LeaderRescueReplansForDiagnostics;
            playerViewAfterRevive = IsLocalPlayerViewCurrent;
            aiReviveOk = aiReviveDowned && !_player.IsDead && _player.ReviveUsed
                && !_localPlayerDowned && !reviverMate.IsRevivingLeader
                && squadMateViewOnDown && downedInputLocked && spectatorTracksMate
                && downedBannerVisible && playerViewAfterRevive && !_player.UiLocked;
            rescueMaze.QueueFree();
            ResetSquadLeaderTrail(_player.GlobalPosition);
        }
        var rescuePathOk = rescueDirectBlocked
            && followDetourReady
            && rescueTrailUsed
            && rescueWaypointAdvances >= 1
            && aiReviveOk;

        // No leader trail at all: the geometric ground grid must route the mate
        // around a sealed wall through the side door without any trail hints.
        var gridMate = _squadMates.FirstOrDefault(m => IsInstanceValid(m) && !m.IsHumanProxy && !m.IsDowned);
        var gridDetourOk = false;
        var gridDetourReady = false;
        var gridRescueUsedGrid = false;
        var gridRescueCompleted = false;
        var gridPathLifecycleOk = false;
        var gridReviverAssigned = false;
        var gridEmergencyPathReady = false;
        if (gridMate is not null)
        {
            var gridMaze = BuildSquadRescueMazeForDiagnostics(
                out var gridPlayerPosition,
                out var gridReviverPosition,
                out var gridRoutePoints);
            await WaitFrames(3);
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.SetReviveUsedForDiagnostics(false);
            OnLocalPlayerRevived();
            foreach (var enemy in _enemies)
            {
                if (IsInstanceValid(enemy))
                {
                    enemy.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
            foreach (var squadMate in _squadMates)
            {
                if (!IsInstanceValid(squadMate) || squadMate == gridMate)
                {
                    continue;
                }
                squadMate.ProcessMode = ProcessModeEnum.Disabled;
                squadMate.GlobalPosition = new Vector3(
                    300.0f + squadMate.SquadSlot * 3.0f,
                    80.25f,
                    300.0f);
            }
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = gridPlayerPosition;
            _player.Velocity = Vector3.Zero;
            gridMate.Velocity = Vector3.Zero;
            gridMate.ResetCombatTacticsForDiagnostics();
            gridMate.GlobalPosition = gridReviverPosition;
            gridMate.SetOrder(SquadOrder.Hold, gridReviverPosition);
            // Trail anchored at the downed player: the mate is fully off-trail and
            // only the geometric grid can route around the sealed wall.
            ResetSquadLeaderTrail(_player.GlobalPosition);
            var gridDirectBlocked = !IsSquadMovementCorridorClear(
                gridReviverPosition,
                gridPlayerPosition,
                gridMate);
            var gridProbePath = new SquadGridPathState
            {
                Emergency = false,
                Destination = gridPlayerPosition,
                Directives = BuildSquadWalkDirectives(gridRoutePoints.Skip(1).ToArray())
            };
            _squadGridPaths[gridMate.GetInstanceId()] = gridProbePath;
            var gridPathCreated = _squadGridPaths.ContainsKey(gridMate.GetInstanceId());
            ResetSquadLeaderTrail(_player.GlobalPosition);
            var gridPathCleared = !_squadGridPaths.ContainsKey(gridMate.GetInstanceId());
            var gridFollowPath = new SquadGridPathState
            {
                Emergency = false,
                Destination = gridPlayerPosition,
                Directives = BuildSquadWalkDirectives(gridRoutePoints.Skip(1).ToArray()),
                NextPlanMilliseconds = ulong.MaxValue,
                FailedPlanAttempts = 2
            };
            _squadGridPaths[gridMate.GetInstanceId()] = gridFollowPath;
            var gridFirstWaypoint = gridFollowPath.Directives[0];
            var gridFollowDirectives = gridFollowPath.Directives;
            var gridFollowCursor = gridFollowPath.Cursor;
            gridPathLifecycleOk = gridPathCreated
                && gridPathCleared
                && gridFollowPath.Directives.Length > 0
                && gridFollowCursor >= 0
                && gridFollowCursor < gridFollowPath.Directives.Length;
            gridDetourReady = gridDirectBlocked
                && gridFirstWaypoint.Target.DistanceTo(gridPlayerPosition) > 3.0f
                && gridFirstWaypoint.Target.X > gridReviverPosition.X + 2.0f
                && IsSquadMovementCorridorClear(gridReviverPosition, gridFirstWaypoint.Target, gridMate);

            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            var gridRescueDowned = _player.IsDead && _localPlayerDowned;
            UpdateSquadReviveAi(1.0f / 60.0f);
            gridReviverAssigned = ReferenceEquals(_leaderReviver, gridMate)
                && gridMate.IsRevivingLeader;
            gridEmergencyPathReady = _squadGridPaths.TryGetValue(
                    gridMate.GetInstanceId(),
                    out var emergencyGridPath)
                && gridFollowPath is not null
                && ReferenceEquals(gridFollowPath, emergencyGridPath)
                && ReferenceEquals(gridFollowDirectives, emergencyGridPath.Directives)
                && emergencyGridPath.Cursor == gridFollowCursor
                && emergencyGridPath.Emergency
                && emergencyGridPath.Destination.DistanceSquaredTo(gridPlayerPosition) <= 0.0001f
                && emergencyGridPath.NextPlanMilliseconds == 0
                && emergencyGridPath.FailedPlanAttempts == 0
                && LeaderRescueUsedGridForDiagnostics
                && LeaderRescueGridPlansForDiagnostics == 0;
            _ = ResolveSquadNavigationDestination(
                gridMate,
                gridPlayerPosition,
                emergency: true);
            gridMate.ProcessMode = ProcessModeEnum.Inherit;
            for (var second = 0; second < 25 && _player.IsDead; second++)
            {
                await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
            }
            gridRescueUsedGrid = LeaderRescueUsedGridForDiagnostics;
            gridRescueCompleted = gridRescueDowned
                && !_player.IsDead
                && _player.ReviveUsed
                && !_localPlayerDowned;
            gridDetourOk = gridDetourReady
                && gridPathLifecycleOk
                && gridReviverAssigned
                && gridEmergencyPathReady
                && gridRescueUsedGrid
                && gridRescueCompleted;
            gridMaze.QueueFree();
            ResetSquadLeaderTrail(_player.GlobalPosition);
            foreach (var squadMate in _squadMates)
            {
                if (IsInstanceValid(squadMate))
                {
                    squadMate.ProcessMode = ProcessModeEnum.Inherit;
                }
            }
            _player.ProcessMode = ProcessModeEnum.Inherit;
            await WaitFrames(2);
        }

        // A capsule-width pinch can look clear to sparse corridor rays while the
        // character body is physically blocked. Rescue recovery must sidestep it.
        var recoveryMate = _squadMates.FirstOrDefault(m => IsInstanceValid(m) && !m.IsHumanProxy && !m.IsDowned);
        var recoveryPinchOk = false;
        var recoveryRayClear = false;
        var recoveryAssigned = false;
        var recoveryCompleted = false;
        var recoveryCount = 0;
        var recoveryLateral = 0.0f;
        if (recoveryMate is not null)
        {
            ClearLeaderReviveAi();
            var recoveryPinch = BuildSquadRecoveryPinchForDiagnostics(
                out var recoveryPlayerPosition,
                out var recoveryReviverPosition);
            await WaitFrames(3);
            foreach (var squadMate in _squadMates)
            {
                if (!IsInstanceValid(squadMate) || squadMate == recoveryMate)
                {
                    continue;
                }
                squadMate.ProcessMode = ProcessModeEnum.Disabled;
                squadMate.GlobalPosition = new Vector3(
                    330.0f + squadMate.SquadSlot * 3.0f,
                    80.25f,
                    330.0f);
            }
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = recoveryPlayerPosition;
            _player.Velocity = Vector3.Zero;
            recoveryMate.ProcessMode = ProcessModeEnum.Disabled;
            recoveryMate.GlobalPosition = recoveryReviverPosition;
            recoveryMate.Velocity = Vector3.Zero;
            recoveryMate.ResetCombatTacticsForDiagnostics();
            recoveryMate.SetOrder(SquadOrder.Hold, recoveryReviverPosition);
            SetSquadLeaderTrailForDiagnostics(new[] { recoveryReviverPosition, recoveryPlayerPosition });
            recoveryRayClear = IsSquadMovementCorridorClear(
                recoveryReviverPosition,
                recoveryPlayerPosition,
                recoveryMate);
            var recoveriesBefore = recoveryMate.CombatStuckRecoveries;

            _player.SetHealthForDiagnostics(10.0f);
            _player.SetReviveUsedForDiagnostics(false);
            _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            if (!_player.IsDead)
            {
                _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
            }
            var recoveryDowned = _player.IsDead && _localPlayerDowned;
            recoveryMate.ProcessMode = ProcessModeEnum.Inherit;
            for (var frame = 0; frame < 960 && _player.IsDead; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                recoveryAssigned |= ReferenceEquals(_leaderReviver, recoveryMate)
                    && ReferenceEquals(_aiReviveTarget, _player);
                recoveryLateral = Mathf.Max(
                    recoveryLateral,
                    Mathf.Abs(recoveryMate.GlobalPosition.X - recoveryReviverPosition.X));
            }
            recoveryCount = recoveryMate.CombatStuckRecoveries - recoveriesBefore;
            recoveryCompleted = recoveryDowned
                && !_player.IsDead
                && _player.ReviveUsed
                && !_localPlayerDowned;
            recoveryPinchOk = !recoveryRayClear
                && recoveryAssigned
                && recoveryLateral > 0.85f
                && recoveryCompleted;

            if (_player.IsDead)
            {
                _player.TryReceiveRevive(50.0f);
            }
            ClearLeaderReviveAi();
            _player.GlobalPosition = DeploymentPoint + Vector3.Up * 0.3f;
            _player.Velocity = Vector3.Zero;
            recoveryMate.GlobalPosition = _player.GlobalPosition + new Vector3(2.0f, 0.0f, 1.0f);
            recoveryMate.Velocity = Vector3.Zero;
            recoveryMate.SetOrder(SquadOrder.Follow, _player.GlobalPosition);
            recoveryPinch.QueueFree();
            ResetSquadLeaderTrail(_player.GlobalPosition);
            foreach (var squadMate in _squadMates)
            {
                if (IsInstanceValid(squadMate))
                {
                    squadMate.ProcessMode = ProcessModeEnum.Inherit;
                }
            }
            _player.ProcessMode = ProcessModeEnum.Inherit;
            await WaitFrames(2);
        }
        GD.Print($"SQUAD_RECOVERY pinch={recoveryPinchOk} ray_clear={recoveryRayClear} assigned={recoveryAssigned} completed={recoveryCompleted} recoveries={recoveryCount} lateral={recoveryLateral:0.00} grid_assigned={gridReviverAssigned} grid_emergency={gridEmergencyPathReady}");
        gridDetourOk &= recoveryPinchOk;

        _player.SetHealthForDiagnostics(10.0f);
        _player.SetReviveUsedForDiagnostics(false);
        _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        if (!_player.IsDead)
        {
            _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        }
        var playerDowned = _player.IsDead && _player.CanBeRevived;
        var playerFirstRevive = _player.TryReceiveRevive(50.0f);
        var playerUp = !_player.IsDead && playerFirstRevive && _player.ReviveUsed;
        _player.TakeCombatDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        var playerSecondBlocked = _player.IsDead && !_player.CanBeRevived && !_player.TryReceiveRevive(50.0f) && _player.ReviveUsed;
        var secondDeathSpectate = _localPlayerEliminated && IsSquadMateViewCurrent && !_missionEnded;
        var openEventsBeforeEliminated = interactionProbe.OpenEventCount;
        OpenLoot(interactionProbe);
        eliminatedLootBlocked = interactionProbe.OpenEventCount == openEventsBeforeEliminated
            && _openLootSource is null
            && !_hud.IsLootVisible
            && _player.UiLocked;
        OpenPersonalBackpack();
        eliminatedBackpackBlocked = !_personalBackpackOpen
            && !_hud.IsLootVisible
            && _player.UiLocked;

        _player.SetHealthForDiagnostics(10.0f);
        _player.SetReviveUsedForDiagnostics(false);
        OnLocalPlayerRevived();
        _player.TakeDamage(999.0f, _player.HitPoint(HitRegion.Torso), this);
        var finishDowned = _player.IsDead && _localPlayerDowned && _player.CanBeRevived;
        var finishAccepted = _player.TryFinishDowned(this);
        var finishedPlayerSpectate = IsSquadMateViewCurrent
            && _localPlayerEliminated
            && !_missionEnded;
        var finishedSpectateOk = finishDowned && finishAccepted
            && finishedPlayerSpectate;

        var reviveOk = mateDowned && firstRevive && mateUp && bodyBagOk && secondReviveBlocked
            && playerDowned && playerFirstRevive && playerUp && playerSecondBlocked && secondDeathSpectate;
        var unreachableTimeoutOk = wallChannelBlocked
            && unreachableAbandoned
            && unreachableGridUsed
            && unreachableGridPlans >= 1
            && unreachableElapsed is >= AiReviveNoProgressTimeout and <= AiReviveNoProgressTimeout + 2.5f
            && bleedResumedAfterAbandon;
        var abandonmentLifecycleOk = abandonmentClearedOnRevive
            && abandonmentClearedOnDown
            && lifecycleCleanupRevived
            && _abandonedAiReviveTarget is null;

        var downedInteractionOk = downedLootBlocked && downedBackpackBlocked && interruptedClimbLocked;
        var eliminatedInteractionOk = eliminatedLootBlocked && eliminatedBackpackBlocked;
        interactionProbe.QueueFree();
        GD.Print($"SQUAD_CHECK members={ActiveSquadCount} ai={AiSquadCount} role_fill={roleFillOk} ai_roles={string.Join("+", aiRoles)} default_follow={defaultFollow} follow_motion={followMotion} stair_climbed={squadStairClimbed} stair_gain={squadStairGain:0.00} stair_steps={squadStairStepUps} ai_cooldown={aiCooldownEnforced} ai_cooldown_seconds={cooldownMate.SkillCooldownDuration:0} medic_self={medicSelf} recon={scanned} assault_speed={assaultSpeed:0.00} assault_fire={assaultFire:0.00} orders={hold && move && follow} combat_ai={combatAiOk} wall_blocked={combatWallBlocked} target_lock={combatTargetLocked} flanked={combatFlanked} sight_recovered={combatSightRecovered} fired={combatFired} damaged={combatDamaged} faced_move={combatFacedMovement} close_retreat={closeRangeRetreat} close_strafe={closeRangeStrafe} revive_once={reviveOk} ai_mate_revive={aiMateRescueOk} far_rescue_blocked={farRescueBlocked} far_cost={farNavigationCost:0.0} revive_select_usec={farSelectionMicroseconds} revive_select_budget={AiReviveSelectionMaximumMicroseconds} critical_rescue_blocked={criticalRescueBlocked} critical_health={criticalReviverHealth:0.00} mate_enemy_contact={mateRescueEnemyDetected} mate_rescue_assigned={mateRescueAssigned} mate_rescue_motion={mateRescueMinDistance < mateRescueStartDistance - 2.0f} mate_reviver_health={mateReviver.Health / mateReviver.MaxHealth:0.00} eliminated_mate_rescue={mateRescueAfterElimination} reverse_trail_rescue={reverseTrailRescue} reverse_wall={reverseTrailDirectBlocked} reverse_cost={reverseTrailCost:0.0} wall_channel_blocked={wallChannelBlocked} unreachable_abandoned={unreachableAbandoned} abandon_seconds={unreachableElapsed:0.0} abandon_grid_used={unreachableGridUsed} abandon_grid_plans={unreachableGridPlans} bleed_resumed={bleedResumedAfterAbandon} abandon_clear_revive={abandonmentClearedOnRevive} abandon_clear_down={abandonmentClearedOnDown} ai_finish={aiFinishOk} finish_target={finishTargetAcquired} finish_lock={finishLockHeld} finish_shot={finishShotFired} finish_kia={finishConverted} ai_leader_revive={aiReviveOk} rescue_path={rescuePathOk} grid_rescue={gridDetourOk} grid_detour={gridDetourReady} grid_lifecycle={gridPathLifecycleOk} grid_used={gridRescueUsedGrid} grid_completed={gridRescueCompleted} rescue_wall={rescueDirectBlocked} follow_detour={followDetourReady} rescue_trail={rescueTrailUsed} rescue_advances={rescueWaypointAdvances} rescue_replans={rescueReplans} first_down_spectate={squadMateViewOnDown} downed_input_locked={downedInputLocked} downed_loot_blocked={downedLootBlocked} downed_backpack_blocked={downedBackpackBlocked} climb_interrupt_locked={interruptedClimbLocked} eliminated_loot_blocked={eliminatedLootBlocked} eliminated_backpack_blocked={eliminatedBackpackBlocked} spectator_tracks={spectatorTracksMate} downed_banner={downedBannerVisible} player_view_after_revive={playerViewAfterRevive} second_death_spectate={secondDeathSpectate} finished_spectate={finishedSpectateOk} immediate_view={finishedPlayerSpectate} body_bag={bodyBagOk} prone_hold={mateCrawled} hud={!_hud.IsSquadLobbyVisible} keys={(long)Key.H}/{(long)Key.F1}/{(long)Key.F2}/{(long)Key.F3}");
        var valid = ActiveSquadCount >= 2 && roleFillOk && combatAiOk
            && squadStairClimbed
            && reviveOk && aiMateRescueOk && unreachableTimeoutOk && abandonmentLifecycleOk
            && aiFinishOk && rescuePathOk && gridDetourOk && downedInteractionOk && eliminatedInteractionOk
            && finishedSpectateOk;
        GD.Print($"SQUAD_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureSquadFrame()
    {
        SetCaptureLanguage("en");
        await ToSignal(GetTree().CreateTimer(0.65f), SceneTreeTimer.SignalName.Timeout);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        var captureCenter = new Vector3(0.0f, 0.15f, 29.5f);
        var captureCamera = new Camera3D
        {
            Name = "SquadCaptureCamera",
            Fov = 42.0f,
            Near = 0.04f
        };
        AddChild(captureCamera);
        captureCamera.GlobalPosition = captureCenter + new Vector3(0.0f, 1.65f, -6.4f);
        captureCamera.LookAt(captureCenter + Vector3.Up * 1.05f, Vector3.Up);
        captureCamera.MakeCurrent();

        var stagedPositions = new[]
        {
            captureCenter + new Vector3(-1.35f, 0.0f, 0.0f),
            captureCenter + new Vector3(1.35f, 0.0f, 0.0f),
            captureCenter + new Vector3(0.0f, 0.0f, 1.25f)
        };
        var staged = _squadMates.OrderBy(mate => mate.SquadSlot).ToArray();
        for (var i = 0; i < staged.Length && i < stagedPositions.Length; i++)
        {
            staged[i].GlobalPosition = stagedPositions[i];
            staged[i].SetOrder(SquadOrder.Hold, stagedPositions[i]);
            staged[i].LookAt(
                new Vector3(captureCamera.GlobalPosition.X, stagedPositions[i].Y, captureCamera.GlobalPosition.Z),
                Vector3.Up);
            staged[i].ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ConfigureRole(OperatorRole.Medic);
        if (staged.Length > 1)
        {
            staged[0].SetSkillCooldownForDiagnostics(5.0f);
            staged[1].TakeCombatDamage(42.0f, staged[1].HitPoint(HitRegion.Torso), this);
        }
        _player.ActivateRoleAbility(false);
        await ToSignal(GetTree().CreateTimer(0.68f), SceneTreeTimer.SignalName.Timeout);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng("user://squad_validation.png");
        GD.Print("CAPTURE_SQUAD user://squad_validation.png");
        GetTree().Quit();
    }

    private async void CaptureSquadLobbyFrame()
    {
        SetCaptureLanguage("en");
        _hud.SetLanRoomBrowseAvailable(true);
        _hud.SetLanRooms(new[]
        {
            new LanRoomInfo(
                "capture-room",
                "STEEL-TIDE-HOST",
                "192.168.10.42",
                SquadNetwork.DefaultPort,
                LanRoomKind.Extraction,
                DeploymentMapCatalog.FreightTerminalId,
                2,
                SquadNetwork.ExtractionSquadCapacity)
        });
        _hud.SelectSquadSessionForDiagnostics(SquadSessionMode.Join);
        await ToSignal(GetTree().CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng("user://squad_lobby_validation.png");
        GD.Print("CAPTURE_SQUAD_LOBBY user://squad_lobby_validation.png");
        GetTree().Quit();
    }

    private async void ValidateNetworkSession(string mode)
    {
        var connectionDeadline = Time.GetTicksMsec() + 20000;
        var connected = false;
        while (Time.GetTicksMsec() < connectionDeadline)
        {
            connected = mode == "host"
                ? _squadNetwork.ConnectedPeerCount > 0
                : _squadNetwork.IsOnline;
            if (connected)
            {
                break;
            }
            await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        }
        await ToSignal(GetTree().CreateTimer(0.35f), SceneTreeTimer.SignalName.Timeout);
        _squadNetwork.BroadcastShot(_player.GlobalPosition + Vector3.Up, _player.GlobalPosition - Vector3.Forward * 4.0f, -1, 0.0f);
        _squadNetwork.BroadcastAbility(OperatorRole.Assault, _player.GlobalPosition + Vector3.Up, -Vector3.Forward);
        if (mode == "client")
        {
            _squadNetwork.BroadcastAbility(OperatorRole.Assault, _player.GlobalPosition + Vector3.Up, -Vector3.Forward);
        }
        await ToSignal(GetTree().CreateTimer(mode == "host" ? 1.5f : 1.7f), SceneTreeTimer.SignalName.Timeout);
        var remoteHumans = _squadMates.Count(mate => IsInstanceValid(mate) && mate.IsHumanProxy);
        var cooldownGate = _remoteNetworkAbilityCount == 1;
        var valid = connected && cooldownGate && remoteHumans == 1;
        GD.Print($"NETWORK_CHECK mode={mode} valid={valid} connected={connected} online={_squadNetwork.IsOnline} peers={_squadNetwork.ConnectedPeerCount} remote_humans={remoteHumans} remote_shots={_remoteNetworkShotCount} remote_abilities={_remoteNetworkAbilityCount} cooldown_gate={cooldownGate} members={ActiveSquadCount} ai={AiSquadCount}");
        if (mode == "host")
        {
            await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
        }
        GetTree().Quit(valid ? 0 : 2);
    }
}
