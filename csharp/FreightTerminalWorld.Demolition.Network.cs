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
    private bool _demolitionJoinPending;
    private OperatorRole _pendingDemolitionRole;
    private string _pendingDemolitionMapId = string.Empty;
    private string _pendingDemolitionAddress = string.Empty;
    private DemolitionNetworkTeam _pendingDemolitionTeam;
    private int _networkDeviceCarrierActorId = -1;

    public DemolitionNetworkTeam DemolitionLocalNetworkTeam => _demolitionLocalNetworkTeam;
    public int DemolitionLocalNetworkSlot => _demolitionLocalNetworkSlot;
    public int DemolitionNetworkHumanCount => 1 + _demolitionNetworkPlayers.Count;
    public int DemolitionNetworkFriendlyHumanCount => 1 + _demolitionNetworkPlayers.Values.Count(
        state => state.Team == _demolitionLocalNetworkTeam);
    public int DemolitionNetworkOpponentHumanCount => _demolitionNetworkPlayers.Values.Count(
        state => state.Team != _demolitionLocalNetworkTeam);

    private bool IsDemolitionNetworkClient
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

    private void ConfigureDemolitionNetwork(
        SquadSessionMode mode,
        string address,
        DemolitionNetworkTeam team)
    {
        _demolitionLocalNetworkTeam = mode == SquadSessionMode.Join
            ? team
            : DemolitionNetworkTeam.Alpha;
        _demolitionLocalNetworkSlot = 0;
        _demolitionNetworkClient = mode == SquadSessionMode.Join;
        _demolitionNetworkPlayers.Clear();
        _remoteDemolitionOpponents.Clear();
        _demolitionNetworkRound = 0;
        _networkDeviceCarrierActorId = -1;
        _demolitionNetworkActionReceivedForDiagnostics = false;
        _demolitionNetworkActionAppliedForDiagnostics = false;
        _demolitionNetworkActionDistanceForDiagnostics = -1.0f;
        _squadNetwork.ConfigureDemolitionSession(_demolitionSelectedMapId, _demolitionLocalNetworkTeam);
    }

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
        _demolitionJoinPending = true;
        _pendingDemolitionRole = role;
        _pendingDemolitionMapId = mapId;
        _pendingDemolitionAddress = address;
        _pendingDemolitionTeam = team;
        _hud.SetDemolitionNetworkConnectionPending(true, $"CONNECTING  //  {address}");
        var error = _squadNetwork.Join(address);
        if (error != Error.Ok)
        {
            CancelPendingNetworkDeployment();
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
        OnDemolitionDeploymentRequested(
            (int)role,
            (int)WeaponPlatform.M4A1,
            1,
            (int)WeaponPlatform.P226,
            mapId,
            (int)SquadSessionMode.Join,
            address,
            (int)team);
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

    private void OnDemolitionNetworkAssignment(DemolitionNetworkTeam team, int slot)
    {
        if (!_demolitionMode || !_demolitionNetworkClient)
        {
            return;
        }
        _demolitionLocalNetworkTeam = team;
        _demolitionLocalNetworkSlot = Mathf.Clamp(slot, 0, DemolitionSquadSize - 1);
        var conflictingAi = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && !mate.IsHumanProxy && mate.SquadSlot == _demolitionLocalNetworkSlot);
        if (conflictingAi is not null)
        {
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
        _demolitionNetworkPlayers[state.PeerId] = state;
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
                    _squadMates.Remove(ai);
                    ai.QueueFree();
                }
                mate = SpawnSquadMate(state.Slot, state.Role, true, state.PeerId);
            }
            var authoritativeHealth = _squadNetwork.IsHost ? mate.Health : state.Health;
            var authoritativeDead = _squadNetwork.IsHost
                ? mate.IsDowned || mate.IsBodyBag
                : state.Dead;
            mate.SetRemoteState(state.Role, state.Position, state.Rotation,
                authoritativeHealth, authoritativeDead);
            return;
        }

        var opponent = EnsureRemoteDemolitionOpponent(state);
        var opponentHealth = _squadNetwork.IsHost ? opponent.CurrentHealth : state.Health;
        var opponentDead = _squadNetwork.IsHost ? opponent.IsDead : state.Dead;
        opponent.SetRemoteNetworkState(state.Role, state.Position, state.Rotation,
            opponentHealth, opponentDead);
    }

    private EnemyOperator EnsureRemoteDemolitionOpponent(DemolitionPlayerNetworkState state)
    {
        if (_remoteDemolitionOpponents.TryGetValue(state.PeerId, out var existing)
            && IsInstanceValid(existing))
        {
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
        RemoveRemoteDemolitionOpponent(peerId);
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
        return new DemolitionMatchNetworkState(
            _demolitionMatch.CurrentRound,
            _demolitionMatch.PlayerScore,
            _demolitionMatch.OpponentScore,
            _demolitionMatch.IsOvertime,
            _demolitionMatch.IsComplete,
            _demolitionRoundActive,
            _demolitionBuyPhaseActive,
            _demolitionRemaining,
            (int)_demolitionDeviceLifecycle.Phase,
            _demolitionActiveSite,
            carrierActorId,
            position);
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
            mate.SetRemoteState(state.Role, state.Position, state.Rotation, state.Health, state.Dead);
            return;
        }
        var opponent = _demolitionOpponents.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.NetworkId == state.ActorId)
            ?? SpawnDemolitionOpponentAtSlot(slot, team);
        opponent.ConfigureNetworkProxy(-state.ActorId, state.Role, state.Human);
        opponent.SetRemoteNetworkState(state.Role, state.Position, state.Rotation, state.Health, state.Dead);
    }

    private void OnDemolitionMatchState(DemolitionMatchNetworkState state)
    {
        if (!IsDemolitionNetworkClient)
        {
            return;
        }
        var roundChanged = _demolitionNetworkRound != 0 && _demolitionNetworkRound != state.CurrentRound;
        _demolitionNetworkRound = state.CurrentRound;
        _demolitionMatch.ApplyNetworkState(
            state.CurrentRound, state.AlphaScore, state.BravoScore, state.Overtime, state.Complete);
        _demolitionRoundActive = state.RoundActive;
        _demolitionBuyPhaseActive = state.BuyActive;
        _demolitionRemaining = state.Remaining;
        _demolitionDevicePlanted = state.DevicePhase == (int)DemolitionDevicePhase.Planted;
        _demolitionActiveSite = state.ActiveSite;
        _networkDeviceCarrierActorId = state.CarrierActorId;
        if (roundChanged)
        {
            ResetDemolitionSquad();
            SpawnDemolitionOpponents();
        }
        if (IsInstanceValid(_demolitionDevice))
        {
            _demolitionDevice!.GlobalPosition = state.DevicePosition;
            _demolitionDevice.Visible = state.DevicePhase is not (int)DemolitionDevicePhase.Inactive
                and not (int)DemolitionDevicePhase.Detonated;
        }
        if (state.Complete && !_missionEnded)
        {
            CompleteDemolitionMatch(GameLocalization.Get(
                "mission_complete",
                _languageSetting,
                "MATCH COMPLETE"));
            return;
        }
        UpdateDemolitionRoundHud();
    }

    private void UpdateDemolitionNetworkClientRound(float delta)
    {
        if (_demolitionBuyPhaseActive)
        {
            UpdateDemolitionBuyPhase(delta);
            return;
        }
        if (!_demolitionRoundActive)
        {
            UpdateDemolitionRoundHud();
            return;
        }
        UpdateDemolitionInteraction(delta);
        UpdateDemolitionRoundHud();
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

    private void ApplyDemolitionNetworkDamage(int actorId, float damage, Vector3 hitPosition, Node? attacker)
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
            mate?.TakeCombatDamage(damage, hitPosition, attacker);
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

}
