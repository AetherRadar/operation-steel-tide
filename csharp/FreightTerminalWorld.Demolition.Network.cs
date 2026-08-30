using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int DemolitionAlphaActorBase = 100;
    private const int DemolitionBravoActorBase = 200;
    private const float DemolitionNetworkSnapshotInterval = 0.1f;

    private readonly Dictionary<long, DemolitionPlayerNetworkState> _demolitionNetworkPlayers = new();
    private readonly Dictionary<long, EnemyOperator> _remoteDemolitionOpponents = new();
    private DemolitionNetworkTeam _demolitionLocalNetworkTeam = DemolitionNetworkTeam.Alpha;
    private int _demolitionLocalNetworkSlot;
    private bool _demolitionNetworkClient;
    private float _demolitionNetworkSnapshotTimer;
    private int _demolitionNetworkRound;
    private DemolitionNetworkPhase _demolitionNetworkPhase = DemolitionNetworkPhase.Lobby;
    private DemolitionDevicePhase _networkDevicePhase = DemolitionDevicePhase.Inactive;
    private int _networkDeviceCarrierActorId = -1;

    public DemolitionNetworkTeam DemolitionLocalNetworkTeam => _demolitionLocalNetworkTeam;
    public int DemolitionLocalNetworkSlot => _demolitionLocalNetworkSlot;
    public int DemolitionNetworkHumanCount => 1 + _demolitionNetworkPlayers.Count;
    public int DemolitionNetworkFriendlyHumanCount => 1 + _demolitionNetworkPlayers.Values.Count(
        state => state.Team == _demolitionLocalNetworkTeam);
    public int DemolitionNetworkOpponentHumanCount => _demolitionNetworkPlayers.Values.Count(
        state => state.Team != _demolitionLocalNetworkTeam);
    public DemolitionNetworkPhase DemolitionNetworkCurrentPhase => _demolitionNetworkPhase;

    internal bool IsDemolitionNetworkClient
        => _demolitionMode && _demolitionNetworkClient;

    private DemolitionTeam LocalDemolitionSide
        => _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? _demolitionMatch.PlayerSide
            : DemolitionOtherSide(_demolitionMatch.PlayerSide);

    private int LocalDemolitionScore
        => _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? _demolitionMatch.PlayerScore
            : _demolitionMatch.OpponentScore;

    private int OpposingDemolitionScore
        => _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? _demolitionMatch.OpponentScore
            : _demolitionMatch.PlayerScore;

    private DemolitionNetworkTeam OpposingLocalNetworkTeam
        => _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? DemolitionNetworkTeam.Bravo
            : DemolitionNetworkTeam.Alpha;

    private int LocalDemolitionActorId
        => DemolitionActorId(_demolitionLocalNetworkTeam, _demolitionLocalNetworkSlot);

    private static int DemolitionActorId(DemolitionNetworkTeam team, int slot)
        => (team == DemolitionNetworkTeam.Alpha ? DemolitionAlphaActorBase : DemolitionBravoActorBase)
        + Mathf.Clamp(slot, 0, DemolitionSquadSize - 1);

    private static DemolitionNetworkTeam DemolitionActorTeam(int actorId)
        => actorId >= DemolitionBravoActorBase
            ? DemolitionNetworkTeam.Bravo
            : DemolitionNetworkTeam.Alpha;

    private static int DemolitionActorSlot(int actorId) => actorId % 100;

    private Node3D? DemolitionActorForId(int actorId)
    {
        if (actorId == LocalDemolitionActorId)
        {
            return _player;
        }
        if (actorId < DemolitionAlphaActorBase)
        {
            return null;
        }
        var team = DemolitionActorTeam(actorId);
        var slot = DemolitionActorSlot(actorId);
        if (team == _demolitionLocalNetworkTeam)
        {
            return _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate.SquadSlot == slot);
        }
        return _demolitionOpponents.FirstOrDefault(opponent => IsInstanceValid(opponent)
            && opponent.NetworkId == actorId);
    }

    private void InitializeDemolitionNetworkRuntime(SquadSessionMode mode)
    {
        _demolitionLocalNetworkTeam = mode == SquadSessionMode.Join
            ? _squadNetwork.RequestedDemolitionTeam
            : DemolitionNetworkTeam.Alpha;
        _demolitionLocalNetworkSlot = mode == SquadSessionMode.Join
            ? Mathf.Clamp(_squadNetwork.LocalDemolitionSlot, 0, DemolitionSquadSize - 1)
            : 0;
        _demolitionNetworkClient = mode == SquadSessionMode.Join;
        _demolitionNetworkPlayers.Clear();
        _remoteDemolitionOpponents.Clear();
        _demolitionNetworkRound = 0;
        _demolitionNetworkPhase = DemolitionNetworkPhase.Lobby;
        _networkDevicePhase = DemolitionDevicePhase.Inactive;
        _networkDeviceCarrierActorId = -1;
        _demolitionNetworkActionReceivedForDiagnostics = false;
        _demolitionNetworkActionAppliedForDiagnostics = false;
        _demolitionNetworkActionDistanceForDiagnostics = -1.0f;
        InitializeRemoteDemolitionEconomies();
        ConfigureDemolitionGlassNetwork();
    }

    private void OnDemolitionNetworkAssignment(DemolitionNetworkTeam team, int slot)
    {
        if (!_demolitionNetworkClient && _demolitionLobbyDeployment?.Mode != SquadSessionMode.Join)
        {
            return;
        }
        if (slot < 0)
        {
            _demolitionJoinRejectionCode = slot;
            var status = slot switch
            {
                -2 => "JOIN FAILED  //  HOST IS USING A DIFFERENT MAP",
                -3 => "JOIN FAILED  //  MATCH ALREADY STARTED",
                _ => "ROOM FULL  //  SELECT ANOTHER TEAM OR ROOM"
            };
            _squadNetwork.Close();
            CancelDemolitionNetworkLobby(closeNetwork: false);
            _hud.SetDemolitionNetworkConnectionPending(false, status);
            return;
        }
        _demolitionLocalNetworkTeam = team;
        _demolitionLocalNetworkSlot = Mathf.Clamp(slot, 0, DemolitionSquadSize - 1);
        if (!_demolitionMode)
        {
            return;
        }
        var conflictingAi = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && !mate.IsHumanProxy && mate.SquadSlot == _demolitionLocalNetworkSlot);
        if (conflictingAi is not null)
        {
            ClearDemolitionSquadMateState(conflictingAi);
            _squadMates.Remove(conflictingAi);
            conflictingAi.QueueFree();
        }
        var spawns = DemolitionSpawnsFor(LocalDemolitionSide);
        _player.GlobalPosition = spawns[Mathf.Clamp(_demolitionLocalNetworkSlot, 0, spawns.Count - 1)];
        EnsureAiSquadFill();
    }

    private void OnDemolitionPlayerState(DemolitionPlayerNetworkState state)
    {
        if (!_demolitionMode || state.PeerId == Multiplayer.GetUniqueId())
        {
            return;
        }
        if (state.Team == _demolitionLocalNetworkTeam)
        {
            RemoveRemoteDemolitionOpponent(state.PeerId);
            var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.IsHumanProxy && candidate.NetworkPeerId == state.PeerId);
            if (mate is null)
            {
                var ai = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                    && candidate.SquadSlot == state.Slot);
                if (ai is not null)
                {
                    ClearDemolitionSquadMateState(ai);
                    _squadMates.Remove(ai);
                    ai.QueueFree();
                }
                mate = SpawnSquadMate(state.Slot, state.Role, true, state.PeerId);
            }
            var authoritativeHealth = _squadNetwork.IsHost ? mate.Health : state.Health;
            var authoritativeDead = _squadNetwork.IsHost
                ? mate.IsDowned || mate.IsBodyBag
                : state.Dead;
            var acceptedState = state with
            {
                Health = authoritativeHealth,
                Dead = authoritativeDead
            };
            _demolitionNetworkPlayers[state.PeerId] = acceptedState;
            mate.SetDemolitionRemoteState(
                acceptedState.Role,
                acceptedState.Position,
                acceptedState.Rotation,
                acceptedState.Health,
                acceptedState.Dead);
            if (_squadNetwork.IsHost)
            {
                _squadNetwork.RelayDemolitionPlayerState(acceptedState);
            }
            return;
        }

        var opponent = EnsureRemoteDemolitionOpponent(state);
        var opponentHealth = _squadNetwork.IsHost ? opponent.CurrentHealth : state.Health;
        var opponentDead = _squadNetwork.IsHost ? opponent.IsDead : state.Dead;
        var acceptedOpponentState = state with
        {
            Health = opponentHealth,
            Dead = opponentDead
        };
        _demolitionNetworkPlayers[state.PeerId] = acceptedOpponentState;
        opponent.SetRemoteNetworkState(
            acceptedOpponentState.Role,
            acceptedOpponentState.Position,
            acceptedOpponentState.Rotation,
            acceptedOpponentState.Health,
            acceptedOpponentState.Dead);
        if (_squadNetwork.IsHost)
        {
            _squadNetwork.RelayDemolitionPlayerState(acceptedOpponentState);
        }
    }

    private EnemyOperator EnsureRemoteDemolitionOpponent(DemolitionPlayerNetworkState state)
    {
        if (_remoteDemolitionOpponents.TryGetValue(state.PeerId, out var existing)
            && IsInstanceValid(existing))
        {
            existing.ConfigureNetworkProxy(state.PeerId, state.Role, human: true);
            return existing;
        }
        var actorId = DemolitionActorId(state.Team, state.Slot);
        var opponent = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.NetworkId == actorId);
        if (opponent is null)
        {
            opponent = SpawnDemolitionOpponentAtSlot(state.Slot, state.Team);
        }
        opponent.ConfigureNetworkProxy(state.PeerId, state.Role, human: true);
        _remoteDemolitionOpponents[state.PeerId] = opponent;
        return opponent;
    }

    private EnemyOperator SpawnDemolitionOpponentAtSlot(int slot, DemolitionNetworkTeam team)
    {
        var side = team == DemolitionNetworkTeam.Alpha
            ? _demolitionMatch.PlayerSide
            : DemolitionOtherSide(_demolitionMatch.PlayerSide);
        var spawns = DemolitionSpawnsFor(side);
        var layout = DemolitionLayout();
        var spawnIndex = Mathf.Clamp(slot, 0, spawns.Count - 1);
        var opponent = SpawnEnemy(
            spawns[spawnIndex],
            alerted: false,
            teamId: 0,
            initialWeapon: _demolitionOpponentRoundWeapon,
            sentryMode: side == DemolitionTeam.Defenders,
            detectionRange: 52.0f);
        opponent.NetworkId = DemolitionActorId(team, spawnIndex);
        opponent.Name = $"DemolitionOpponent_{spawnIndex + 1:00}";
        opponent.LookAt(layout.Midpoint, Vector3.Up);
        _demolitionOpponents.Add(opponent);
        return opponent;
    }

    private void RemoveRemoteDemolitionOpponent(long peerId)
    {
        if (!_remoteDemolitionOpponents.Remove(peerId, out var opponent)
            || !IsInstanceValid(opponent))
        {
            return;
        }
        var slot = DemolitionActorSlot(opponent.NetworkId);
        var team = DemolitionActorTeam(opponent.NetworkId);
        _demolitionOpponents.Remove(opponent);
        _enemies.Remove(opponent);
        opponent.QueueFree();
        if (_demolitionMode && _squadNetwork.IsHost)
        {
            SpawnDemolitionOpponentAtSlot(slot, team);
        }
    }

    private void OnDemolitionNetworkPeerLeft(long peerId)
    {
        _demolitionNetworkPlayers.Remove(peerId);
        _demolitionBuyReadyPeers.Remove(peerId);
        _demolitionRemoteEconomies.Remove(peerId);
        RemoveRemoteDemolitionOpponent(peerId);
        if (_demolitionMode)
        {
            EnsureAiSquadFill();
        }
        if (_squadNetwork.IsHost && _demolitionBuyPhaseActive)
        {
            TryBeginNetworkDemolitionLivePhase();
        }
    }

    private void UpdateDemolitionNetwork(float delta)
    {
        if (!_demolitionMode || !_squadNetwork.IsOnline || !_squadNetwork.IsDemolitionSession)
        {
            return;
        }
        if (!_squadNetwork.IsHost)
        {
            return;
        }
        _demolitionNetworkSnapshotTimer -= delta;
        if (_demolitionNetworkSnapshotTimer > 0.0f)
        {
            return;
        }
        _demolitionNetworkSnapshotTimer = DemolitionNetworkSnapshotInterval;
        BroadcastDemolitionActors();
        _squadNetwork.BroadcastDemolitionMatchState(CaptureDemolitionMatchNetworkState());
    }

    private void BroadcastDemolitionActors()
    {
        _squadNetwork.BroadcastDemolitionActorState(new DemolitionActorNetworkState(
            DemolitionActorId(DemolitionNetworkTeam.Alpha, 0),
            _player.Role,
            _player.GlobalPosition,
            _player.Rotation,
            _player.Health,
            _player.IsDead,
            true));
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            _squadNetwork.BroadcastDemolitionActorState(new DemolitionActorNetworkState(
                DemolitionActorId(DemolitionNetworkTeam.Alpha, mate.SquadSlot),
                mate.Role,
                mate.GlobalPosition,
                mate.Rotation,
                mate.Health,
                mate.IsDowned || mate.IsBodyBag,
                mate.IsHumanProxy));
        }
        foreach (var opponent in _demolitionOpponents.Where(IsInstanceValid))
        {
            _squadNetwork.BroadcastDemolitionActorState(new DemolitionActorNetworkState(
                opponent.NetworkId,
                opponent.NetworkRole,
                opponent.GlobalPosition,
                opponent.Rotation,
                opponent.CurrentHealth,
                opponent.IsDead,
                opponent.IsHumanProxy));
        }
    }

    private DemolitionMatchNetworkState CaptureDemolitionMatchNetworkState()
    {
        var carrier = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId);
        var carrierActorId = DemolitionActorIdForNode(carrier);
        var position = IsInstanceValid(_demolitionDevice)
            ? _demolitionDevice!.GlobalPosition
            : _demolitionDeviceGroundPosition;
        var phase = _demolitionMatch.IsComplete || _missionEnded
            ? DemolitionNetworkPhase.Complete
            : _demolitionBuyPhaseActive
                ? DemolitionNetworkPhase.Buy
                : _demolitionRoundActive
                    ? DemolitionNetworkPhase.Live
                    : _demolitionIntermissionRemaining > 0.0f
                        ? DemolitionNetworkPhase.Intermission
                        : DemolitionNetworkPhase.Lobby;
        var phaseRemaining = phase switch
        {
            DemolitionNetworkPhase.Buy => _demolitionBuyRemaining,
            DemolitionNetworkPhase.Live => _demolitionRemaining,
            DemolitionNetworkPhase.Intermission => _demolitionIntermissionRemaining,
            _ => 0.0f
        };
        _demolitionNetworkPhase = phase;
        return new DemolitionMatchNetworkState(
            _demolitionMatch.CurrentRound,
            _demolitionMatch.PlayerScore,
            _demolitionMatch.OpponentScore,
            _demolitionMatch.IsOvertime,
            _demolitionMatch.IsComplete,
            phase,
            phaseRemaining,
            _demolitionPlayerEconomy.Funds,
            _demolitionOpponentEconomy.Funds,
            (int)_demolitionDeviceLifecycle.Phase,
            _demolitionActiveSite,
            carrierActorId,
            position,
            CaptureDemolitionBazaarGlassMask());
    }

    private int DemolitionActorIdForNode(Node3D? actor)
    {
        if (!IsInstanceValid(actor))
        {
            return -1;
        }
        if (actor == _player)
        {
            return DemolitionActorId(DemolitionNetworkTeam.Alpha, 0);
        }
        if (actor is SquadMate mate)
        {
            return DemolitionActorId(DemolitionNetworkTeam.Alpha, mate.SquadSlot);
        }
        return actor is EnemyOperator enemy ? enemy.NetworkId : -1;
    }

    private void OnDemolitionActorState(DemolitionActorNetworkState state)
    {
        if (!IsDemolitionNetworkClient || state.ActorId < DemolitionAlphaActorBase)
        {
            return;
        }
        if (state.ActorId == LocalDemolitionActorId)
        {
            _player.ApplyDemolitionNetworkHealth(state.Health, state.Dead);
            return;
        }
        var team = DemolitionActorTeam(state.ActorId);
        var slot = DemolitionActorSlot(state.ActorId);
        if (team == _demolitionLocalNetworkTeam)
        {
            var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.SquadSlot == slot);
            if (mate is null)
            {
                mate = SpawnSquadMate(slot, state.Role, true, -state.ActorId);
            }
            mate.SetDemolitionRemoteState(
                state.Role,
                state.Position,
                state.Rotation,
                state.Health,
                state.Dead);
            return;
        }
        var opponent = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.NetworkId == state.ActorId)
            ?? SpawnDemolitionOpponentAtSlot(slot, team);
        var proxyPeerId = state.Human && opponent.IsHumanProxy && opponent.NetworkPeerId > 0
            ? opponent.NetworkPeerId
            : -state.ActorId;
        opponent.ConfigureNetworkProxy(proxyPeerId, state.Role, state.Human);
        opponent.SetRemoteNetworkState(state.Role, state.Position, state.Rotation, state.Health, state.Dead);
    }

    private void OnDemolitionMatchState(DemolitionMatchNetworkState state)
    {
        if (!IsDemolitionNetworkClient)
        {
            return;
        }
        var previousRound = _demolitionNetworkRound;
        var previousPhase = _demolitionNetworkPhase;
        var previousLocalScore = LocalDemolitionScore;
        var previousOpponentScore = OpposingDemolitionScore;
        _demolitionNetworkRound = state.CurrentRound;
        _demolitionNetworkPhase = state.Phase;
        _demolitionMatch.ApplyNetworkState(
            state.CurrentRound, state.AlphaScore, state.BravoScore, state.Overtime, state.Complete);
        var opponentFunds = _demolitionLocalNetworkTeam == DemolitionNetworkTeam.Alpha
            ? state.BravoFunds
            : state.AlphaFunds;
        _demolitionOpponentEconomy.ApplyNetworkFunds(opponentFunds);
        _networkDevicePhase = (DemolitionDevicePhase)state.DevicePhase;
        _demolitionDevicePlanted = state.DevicePhase == (int)DemolitionDevicePhase.Planted;
        _demolitionActiveSite = state.ActiveSite;
        _networkDeviceCarrierActorId = state.CarrierActorId;
        if (IsInstanceValid(_demolitionDevice))
        {
            _demolitionDevice!.GlobalPosition = state.DevicePosition;
            _demolitionDevice.Visible = state.DevicePhase is not (int)DemolitionDevicePhase.Inactive
                and not (int)DemolitionDevicePhase.Detonated;
        }
        ApplyDemolitionNetworkClientPhase(
            state,
            previousRound != state.CurrentRound,
            previousPhase != state.Phase);
        ApplyDemolitionGlassSnapshot(
            state.BazaarGlassMask,
            previousRound != state.CurrentRound);
        if (state.Phase == DemolitionNetworkPhase.Intermission
            && previousPhase != DemolitionNetworkPhase.Intermission)
        {
            var localWon = LocalDemolitionScore == previousLocalScore + 1
                && OpposingDemolitionScore == previousOpponentScore;
            var localLost = OpposingDemolitionScore == previousOpponentScore + 1
                && LocalDemolitionScore == previousLocalScore;
            if (localWon || localLost)
            {
                _hud.ShowDemolitionRoundResult(
                    localWon,
                    GameLocalization.Get(
                        "demolition_round_complete",
                        _languageSetting,
                        "ROUND COMPLETE"),
                    LocalDemolitionScore,
                    OpposingDemolitionScore,
                    state.PhaseRemaining);
            }
        }
        if ((state.Complete || state.Phase == DemolitionNetworkPhase.Complete) && !_missionEnded)
        {
            CompleteDemolitionMatch(GameLocalization.Get(
                "mission_complete",
                _languageSetting,
                "MATCH COMPLETE"));
            return;
        }
    }

    private void ApplyDemolitionNetworkClientPhase(
        DemolitionMatchNetworkState state,
        bool roundChanged,
        bool phaseChanged)
    {
        switch (state.Phase)
        {
            case DemolitionNetworkPhase.Buy:
                if (roundChanged || phaseChanged || !_demolitionBuyPhaseActive)
                {
                    PrepareDemolitionRoundRuntime(resolveOpponentBuy: false);
                    BeginDemolitionBuyPhase(state.PhaseRemaining);
                }
                else
                {
                    _demolitionBuyRemaining = state.PhaseRemaining;
                    if (!_demolitionLocalBuyReady)
                    {
                        _hud.UpdateDemolitionBuy(DemolitionBuyState());
                    }
                }
                break;
            case DemolitionNetworkPhase.Live:
                if (!_demolitionRoundActive || phaseChanged)
                {
                    ApplyDemolitionNetworkBuyFallback();
                    _demolitionBuyPhaseActive = false;
                    _demolitionBuyRemaining = 0.0f;
                    _demolitionRoundActive = true;
                    _hud.HideDemolitionBuy();
                    SetDemolitionActorsFrozen(false);
                    RefreshDemolitionStrategies(true);
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                }
                _demolitionRemaining = state.PhaseRemaining;
                UpdateDemolitionRoundHud();
                break;
            case DemolitionNetworkPhase.Intermission:
                _demolitionBuyPhaseActive = false;
                _demolitionRoundActive = false;
                _demolitionIntermissionRemaining = state.PhaseRemaining;
                _hud.HideDemolitionBuy();
                SetDemolitionActorsFrozen(true);
                UpdateDemolitionNetworkIntermissionHud();
                break;
            case DemolitionNetworkPhase.Complete:
                _demolitionBuyPhaseActive = false;
                _demolitionRoundActive = false;
                _demolitionIntermissionRemaining = 0.0f;
                _hud.HideDemolitionBuy();
                SetDemolitionActorsFrozen(true);
                break;
            default:
                _demolitionBuyPhaseActive = false;
                _demolitionRoundActive = false;
                SetDemolitionActorsFrozen(true);
                break;
        }
    }

    private void UpdateDemolitionNetworkIntermissionHud()
    {
        _hud.UpdateDemolitionRoundResult(_demolitionIntermissionRemaining);
        var label = GameLocalization.IsChinese(_languageSetting)
            ? $"\u4e0b\u4e00\u5c40  //  {_demolitionIntermissionRemaining:0.0}s  //  \u5df1\u65b9 {LocalDemolitionScore}:{OpposingDemolitionScore} \u654c\u65b9"
            : $"NEXT ROUND  //  {_demolitionIntermissionRemaining:0.0}s  //  YOU {LocalDemolitionScore}:{OpposingDemolitionScore} ENEMY";
        _hud.SetMissionPhase(label, _demolitionIntermissionRemaining, false);
        _hud.SetObjective(label);
    }

    private void UpdateDemolitionNetworkClientRound(float delta)
    {
        if (_demolitionNetworkPhase == DemolitionNetworkPhase.Buy)
        {
            if (!_demolitionLocalBuyReady)
            {
                _hud.UpdateDemolitionBuy(DemolitionBuyState());
            }
            return;
        }
        if (_demolitionNetworkPhase == DemolitionNetworkPhase.Intermission)
        {
            UpdateDemolitionNetworkIntermissionHud();
            return;
        }
        if (_demolitionNetworkPhase != DemolitionNetworkPhase.Live || !_demolitionRoundActive)
        {
            return;
        }
        UpdateDemolitionInteraction(delta);
        UpdateDemolitionNetworkClientLiveHud();
    }

    private void UpdateDemolitionNetworkClientLiveHud()
    {
        if (!_demolitionDevicePlanted || _demolitionActiveSite < 0)
        {
            UpdateDemolitionRoundHud();
            return;
        }

        var siteName = ((char)('A' + _demolitionActiveSite)).ToString();
        var defending = LocalDemolitionSide == DemolitionTeam.Defenders;
        var defuse = defending && _demolitionPlayerDefuseProgress > 0.01f
            ? GameLocalization.Format(
                "demolition_defuse_suffix",
                _languageSetting,
                "  //  DEFUSE {0:00}%",
                Mathf.RoundToInt(_demolitionPlayerDefuseProgress * 100.0f))
            : string.Empty;
        var squadEliminated = IsLocalDemolitionSquadEliminated();
        var objectiveKey = squadEliminated
            ? "demolition_squad_eliminated_device_objective"
            : defending
                ? "demolition_defuse_hold"
                : "demolition_defend";
        var objectiveEnglish = squadEliminated
            ? "SQUAD ELIMINATED  //  DEVICE ACTIVE AT {0}  //  {1:00.0}s{2}"
            : defending
                ? "DEFUSE SITE {0}  //  {1:00.0}s{2}"
                : "DEFEND SITE {0}  //  {1:00.0}s{2}";
        _hud.SetObjective(GameLocalization.Format(
            objectiveKey,
            _languageSetting,
            objectiveEnglish,
            siteName,
            _demolitionRemaining,
            defuse));

        var sideLabel = defending
            ? GameLocalization.IsChinese(_languageSetting) ? "\u9632\u5b88" : "DEFEND"
            : GameLocalization.IsChinese(_languageSetting) ? "\u8fdb\u653b" : "ATTACK";
        var phase = GameLocalization.IsChinese(_languageSetting)
            ? $"\u7b2c {_demolitionMatch.CurrentRound} \u5c40  //  \u5df1\u65b9 {LocalDemolitionScore}:{OpposingDemolitionScore} \u654c\u65b9  //  {sideLabel}"
            : $"ROUND {_demolitionMatch.CurrentRound}  //  YOU {LocalDemolitionScore}:{OpposingDemolitionScore} ENEMY  //  {sideLabel}";
        _hud.SetMissionPhase(phase, _demolitionRemaining, false);
    }

    private void OnDemolitionNetworkAction(long peerId, DemolitionNetworkAction action, int siteIndex)
    {
        if (!_demolitionMode || !_demolitionRoundActive || !_squadNetwork.IsHost
            || !_demolitionNetworkPlayers.TryGetValue(peerId, out var playerState))
        {
            return;
        }
        _demolitionNetworkActionReceivedForDiagnostics = true;
        Node3D? actor = playerState.Team == DemolitionNetworkTeam.Alpha
            ? _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate.IsHumanProxy && mate.NetworkPeerId == peerId)
            : _remoteDemolitionOpponents.GetValueOrDefault(peerId);
        if (!IsInstanceValid(actor) || siteIndex < 0 || siteIndex >= DemolitionLayout().SitePositions.Count)
        {
            return;
        }
        var side = playerState.Team == DemolitionNetworkTeam.Alpha
            ? _demolitionMatch.PlayerSide
            : DemolitionOtherSide(_demolitionMatch.PlayerSide);
        var sitePosition = DemolitionLayout().SitePositions[siteIndex];
        _demolitionNetworkActionDistanceForDiagnostics = HorizontalDistance(actor!.GlobalPosition, sitePosition);
        if (_demolitionNetworkActionDistanceForDiagnostics > 3.5f)
        {
            return;
        }
        if (action == DemolitionNetworkAction.Plant && side == DemolitionTeam.Attackers
            && DemolitionActorIdForNode(actor) == DemolitionActorIdForNode(
                ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId)))
        {
            PlantDemolitionDevice(siteIndex,
                byPlayerTeam: playerState.Team == DemolitionNetworkTeam.Alpha, actor);
            _demolitionNetworkActionAppliedForDiagnostics = _demolitionDevicePlanted
                && _demolitionActiveSite == siteIndex;
        }
        else if (action == DemolitionNetworkAction.Defuse && side == DemolitionTeam.Defenders
            && _demolitionDevicePlanted && siteIndex == _demolitionActiveSite)
        {
            _demolitionNetworkActionAppliedForDiagnostics = true;
            FinishDemolitionRound(
                playerState.Team == DemolitionNetworkTeam.Alpha,
                GameLocalization.Get("demolition_device_defused", _languageSetting, "DEVICE DEFUSED"));
        }
    }

    private void ApplyDemolitionNetworkDamage(
        int actorId,
        float damage,
        Vector3 hitPosition,
        Node? attacker,
        bool melee = false)
    {
        if (!_demolitionMode || actorId < DemolitionAlphaActorBase || damage <= 0.0f)
        {
            return;
        }
        var team = DemolitionActorTeam(actorId);
        var slot = DemolitionActorSlot(actorId);
        if (team == _demolitionLocalNetworkTeam)
        {
            if (slot == _demolitionLocalNetworkSlot)
            {
                _player.TakeDamage(damage, hitPosition, attacker);
                return;
            }
            var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.SquadSlot == slot);
            if (melee)
            {
                mate?.TakeMeleeCombatDamage(damage, hitPosition, attacker);
            }
            else
            {
                mate?.TakeCombatDamage(damage, hitPosition, attacker);
            }
            return;
        }
        var opponent = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.NetworkId == actorId);
        opponent?.TakeDamage(damage, hitPosition, attacker);
    }

    private bool IsDemolitionNetworkHostileShot(long peerId, int actorId)
    {
        if (actorId < DemolitionAlphaActorBase)
        {
            return false;
        }
        var shooterTeam = peerId == 1
            ? DemolitionNetworkTeam.Alpha
            : _demolitionNetworkPlayers.TryGetValue(peerId, out var playerState)
                ? playerState.Team
                : (DemolitionNetworkTeam?)null;
        return shooterTeam.HasValue && shooterTeam.Value != DemolitionActorTeam(actorId);
    }

    private bool IsDemolitionRemoteShotValid(
        long peerId,
        int actorId,
        Vector3 origin,
        Vector3 end,
        out Node3D? shooter)
    {
        shooter = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && mate.IsHumanProxy && mate.NetworkPeerId == peerId);
        if (!IsInstanceValid(shooter)
            && _remoteDemolitionOpponents.TryGetValue(peerId, out var opponent)
            && IsInstanceValid(opponent))
        {
            shooter = opponent;
        }
        if (!IsInstanceValid(shooter)
            || shooter!.GlobalPosition.DistanceTo(origin) > 4.5f)
        {
            return false;
        }
        var shotDistance = origin.DistanceTo(end);
        if (shotDistance is <= 0.05f or > 260.0f)
        {
            return false;
        }

        Node3D? target = null;
        var team = DemolitionActorTeam(actorId);
        var slot = DemolitionActorSlot(actorId);
        if (team == DemolitionNetworkTeam.Alpha)
        {
            target = slot == 0
                ? _player
                : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                    && mate.SquadSlot == slot);
        }
        else
        {
            target = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.NetworkId == actorId);
        }
        return IsInstanceValid(target)
            && end.DistanceTo(target!.GlobalPosition + Vector3.Up * 0.9f) <= 2.4f;
    }

}
