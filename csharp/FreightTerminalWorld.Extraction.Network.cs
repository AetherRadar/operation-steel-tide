using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float ExtractionWorldSnapshotInterval = 0.1f;
    private const float ExtractionMissionSnapshotInterval = 0.25f;

    private readonly Dictionary<int, EnemyOperator> _extractionNetworkEnemies = new();
    private readonly Dictionary<int, ExtractionSquadNetworkState> _extractionSquadTombstones = new();
    private float _extractionWorldSnapshotTimer;
    private float _extractionMissionSnapshotTimer;
    private int _extractionWorldSequence;
    private int _lastExtractionWorldSequence = -1;
    private int _minimumExtractionWorldSequence;
    private bool _applyingExtractionNetworkState;

    internal bool IsExtractionNetworkClient => !_demolitionMode
        && _squadDeployed
        && _squadNetwork.IsOnline
        && _squadNetwork.IsExtractionSession
        && _squadNetwork.ExtractionMatchStarted
        && !_squadNetwork.IsHost;

    private bool IsExtractionNetworkMatch => !_demolitionMode
        && _squadDeployed
        && _squadNetwork.IsOnline
        && _squadNetwork.IsExtractionSession
        && _squadNetwork.ExtractionMatchStarted;

    private void InitializeExtractionNetworkWorld()
    {
        if (!IsExtractionNetworkMatch)
        {
            return;
        }
        foreach (var enemy in GetChildren().OfType<EnemyOperator>())
        {
            RegisterExtractionNetworkEnemy(enemy);
        }
        if (IsExtractionNetworkClient)
        {
            foreach (var enemy in _extractionNetworkEnemies.Values)
            {
                if (IsInstanceValid(enemy))
                {
                    enemy.ConfigureExtractionNetworkProxy();
                }
            }
            _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        }
        _extractionWorldSnapshotTimer = 0.0f;
        _extractionMissionSnapshotTimer = 0.0f;
    }

    private void RegisterExtractionNetworkEnemy(EnemyOperator enemy)
    {
        if (enemy.NetworkId >= 0)
        {
            _extractionNetworkEnemies[enemy.NetworkId] = enemy;
        }
    }

    private ulong ExtractionEntitySeed(int networkId)
    {
        var seed = unchecked((ulong)DeploymentMapRuntime.CurrentWorldSeed);
        if (seed == 0)
        {
            seed = 0xA0761D6478BD642FUL;
        }
        seed ^= unchecked((ulong)(networkId + 1)) * 0xE7037ED1A0B428DBUL;
        seed ^= seed >> 31;
        return seed == 0 ? 1UL : seed;
    }

    private void UpdateExtractionNetwork(float delta)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || _extractionWorldLaunchPending)
        {
            return;
        }
        _extractionWorldSnapshotTimer -= delta;
        if (_extractionWorldSnapshotTimer <= 0.0f)
        {
            _extractionWorldSnapshotTimer = ExtractionWorldSnapshotInterval;
            BroadcastExtractionWorldSnapshot();
        }
        _extractionMissionSnapshotTimer -= delta;
        if (_extractionMissionSnapshotTimer <= 0.0f)
        {
            _extractionMissionSnapshotTimer = ExtractionMissionSnapshotInterval;
            _squadNetwork.BroadcastExtractionMissionState(CaptureExtractionMissionState());
        }
    }

    private void BroadcastExtractionWorldSnapshot()
        => _squadNetwork.BroadcastExtractionWorldState(
            ExtractionWorldStateCodec.Encode(CaptureExtractionWorldState()));

    private ExtractionWorldNetworkState CaptureExtractionWorldState()
    {
        var enemies = _extractionNetworkEnemies.Values
            .Where(IsInstanceValid)
            .OrderBy(enemy => enemy.NetworkId)
            .Select(CaptureExtractionEnemyState)
            .ToArray();
        var squad = new List<ExtractionSquadNetworkState>(SquadNetwork.ExtractionSquadCapacity)
        {
            new(
                0,
                1,
                _player.Role,
                _player.GlobalPosition,
                _player.Rotation,
                _player.Health,
                (int)(ExtractionSquadNetworkFlags.Human
                    | (_player.IsDead ? ExtractionSquadNetworkFlags.Down : 0)
                    | (_localPlayerEliminated ? ExtractionSquadNetworkFlags.BodyBag : 0)
                    | (_player.ReviveUsed ? ExtractionSquadNetworkFlags.ReviveUsed : 0)
                    | (_player.HasFireablePrimary ? ExtractionSquadNetworkFlags.HasWeapon : 0)))
        };
        foreach (var mate in _squadMates.Where(IsInstanceValid).OrderBy(mate => mate.SquadSlot))
        {
            var flags = ExtractionSquadNetworkFlags.None;
            if (mate.IsHumanProxy)
            {
                flags |= ExtractionSquadNetworkFlags.Human;
            }
            if (mate.IsDowned)
            {
                flags |= ExtractionSquadNetworkFlags.Down;
            }
            if (mate.IsBodyBag)
            {
                flags |= ExtractionSquadNetworkFlags.BodyBag;
            }
            if (mate.ReviveUsed)
            {
                flags |= ExtractionSquadNetworkFlags.ReviveUsed;
            }
            if (mate.HasFireablePrimary)
            {
                flags |= ExtractionSquadNetworkFlags.HasWeapon;
            }
            squad.Add(new ExtractionSquadNetworkState(
                mate.SquadSlot,
                mate.IsHumanProxy ? mate.NetworkPeerId : 0,
                mate.Role,
                mate.GlobalPosition,
                mate.Rotation,
                mate.Health,
                (int)flags));
        }
        foreach (var tombstone in _extractionSquadTombstones.Values.OrderBy(state => state.Slot))
        {
            if (squad.All(state => state.Slot != tombstone.Slot))
            {
                squad.Add(tombstone);
            }
        }
        return new ExtractionWorldNetworkState(
            ++_extractionWorldSequence,
            enemies,
            squad.ToArray());
    }

    private static ExtractionEnemyNetworkState CaptureExtractionEnemyState(EnemyOperator enemy)
    {
        var flags = ExtractionEnemyNetworkFlags.None;
        if (enemy.IsDead)
        {
            flags |= ExtractionEnemyNetworkFlags.Dead;
        }
        if (enemy.IsProne)
        {
            flags |= ExtractionEnemyNetworkFlags.Prone;
        }
        if (enemy.Alerted)
        {
            flags |= ExtractionEnemyNetworkFlags.Alerted;
        }
        if (enemy.HasFireablePrimary)
        {
            flags |= ExtractionEnemyNetworkFlags.HasWeapon;
        }
        if (enemy.IsWorldBoss)
        {
            flags |= ExtractionEnemyNetworkFlags.WorldBoss;
        }
        if (enemy.SentryMode)
        {
            flags |= ExtractionEnemyNetworkFlags.Sentry;
        }
        if (enemy.CarriedWeaponVisible)
        {
            flags |= ExtractionEnemyNetworkFlags.CarriedWeaponVisible;
        }
        return new ExtractionEnemyNetworkState(
            enemy.NetworkId,
            enemy.TeamId,
            enemy.GlobalPosition,
            enemy.Rotation,
            enemy.CurrentHealth,
            (int)enemy.CarriedWeapon.Platform,
            (int)flags);
    }

    private void OnExtractionWorldState(byte[] payload)
    {
        if (!IsExtractionNetworkClient
            || !ExtractionWorldStateCodec.TryDecode(payload, out var state)
            || state.Sequence < _minimumExtractionWorldSequence
            || state.Sequence <= _lastExtractionWorldSequence)
        {
            return;
        }
        _lastExtractionWorldSequence = state.Sequence;
        _applyingExtractionNetworkState = true;
        try
        {
            ApplyExtractionEnemyStates(state.Enemies);
            ApplyExtractionSquadStates(state.Squad);
        }
        finally
        {
            _applyingExtractionNetworkState = false;
        }
    }

    private void ApplyExtractionEnemyStates(IReadOnlyList<ExtractionEnemyNetworkState> states)
    {
        var seen = new HashSet<int>();
        foreach (var state in states)
        {
            seen.Add(state.NetworkId);
            if (!_extractionNetworkEnemies.TryGetValue(state.NetworkId, out var enemy)
                || !IsInstanceValid(enemy))
            {
                enemy = SpawnExtractionEnemyProxy(state);
            }
            enemy.ApplyExtractionNetworkState(state);
        }
        foreach (var pair in _extractionNetworkEnemies.ToArray())
        {
            if (seen.Contains(pair.Key) || !IsInstanceValid(pair.Value) || !pair.Value.IsNetworkProxy)
            {
                continue;
            }
            _enemies.Remove(pair.Value);
            pair.Value.QueueFree();
            _extractionNetworkEnemies.Remove(pair.Key);
        }
    }

    private EnemyOperator SpawnExtractionEnemyProxy(ExtractionEnemyNetworkState state)
    {
        var platform = Enum.IsDefined(typeof(WeaponPlatform), state.WeaponPlatform)
            ? (WeaponPlatform)state.WeaponPlatform
            : WeaponPlatform.M4A1;
        var enemy = new EnemyOperator
        {
            Position = state.Position,
            NetworkId = state.NetworkId,
            SimulationSeed = ExtractionEntitySeed(state.NetworkId),
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            TeamId = state.TeamId,
            DetectionRange = _missionDetectionRange
        };
        enemy.ConfigureInitialLoadout(WeaponCatalog.Build(platform, 0));
        if (((ExtractionEnemyNetworkFlags)state.Flags).HasFlag(ExtractionEnemyNetworkFlags.WorldBoss))
        {
            enemy.ConfigureWorldBoss(ActiveWorldBossPatrolRoute);
            _worldBoss = enemy;
        }
        AddChild(enemy);
        enemy.Eliminated += OnEnemyEliminated;
        if (enemy.IsWorldBoss)
        {
            enemy.Eliminated += OnWorldBossEliminated;
        }
        enemy.ConfigureExtractionNetworkProxy();
        _extractionNetworkEnemies[state.NetworkId] = enemy;
        if (!((ExtractionEnemyNetworkFlags)state.Flags).HasFlag(ExtractionEnemyNetworkFlags.Dead))
        {
            _enemies.Add(enemy);
        }
        return enemy;
    }

    private void ApplyExtractionSquadStates(IReadOnlyList<ExtractionSquadNetworkState> states)
    {
        var seenSlots = new HashSet<int>();
        foreach (var state in states)
        {
            seenSlots.Add(state.Slot);
            var flags = (ExtractionSquadNetworkFlags)state.Flags;
            if (state.Slot == _extractionLocalSquadSlot)
            {
                ApplyExtractionLocalPlayerState(
                    state.Health,
                    flags.HasFlag(ExtractionSquadNetworkFlags.Down),
                    flags.HasFlag(ExtractionSquadNetworkFlags.BodyBag),
                    flags.HasFlag(ExtractionSquadNetworkFlags.ReviveUsed));
                continue;
            }
            var mate = _squadMates.FirstOrDefault(candidate => IsInstanceValid(candidate)
                && candidate.SquadSlot == state.Slot);
            if (mate is null)
            {
                var human = flags.HasFlag(ExtractionSquadNetworkFlags.Human);
                mate = SpawnSquadMate(
                    state.Slot,
                    state.Role,
                    human,
                    state.PeerId,
                    networkProxy: true);
            }
            mate.SetExtractionRemoteState(
                state.Role,
                state.Position,
                state.Rotation,
                state.Health,
                flags.HasFlag(ExtractionSquadNetworkFlags.Down),
                flags.HasFlag(ExtractionSquadNetworkFlags.BodyBag),
                flags.HasFlag(ExtractionSquadNetworkFlags.ReviveUsed),
                flags.HasFlag(ExtractionSquadNetworkFlags.HasWeapon));
        }
        for (var index = _squadMates.Count - 1; index >= 0; index--)
        {
            var mate = _squadMates[index];
            if (!IsInstanceValid(mate) || !mate.IsNetworkProxy || seenSlots.Contains(mate.SquadSlot))
            {
                continue;
            }
            _squadMates.RemoveAt(index);
            mate.QueueFree();
        }
        RefreshSquadHud();
    }

    private ExtractionMissionNetworkState CaptureExtractionMissionState()
        => new(
            _missionPhase,
            _missionRemaining,
            _missionOnline,
            _objectiveStage,
            _currentObjective,
            _missionDirector.IsDeploymentProtected(),
            _reinforcementPending,
            _reinforcementsDeployed,
            _reinforcementCountdown,
            _enemiesRemaining,
            _extractionCountdownActive,
            _extractionRemaining,
            _missionEnded,
            _extractionDeparturePlaying,
            _extractionMissionSucceeded,
            _worldBossDefeated);

    private void OnExtractionMissionState(ExtractionMissionNetworkState state)
    {
        if (!IsExtractionNetworkClient)
        {
            return;
        }
        _missionDirector.ApplyExtractionNetworkState(
            state.Phase,
            state.Remaining,
            state.ObjectiveStage,
            state.DeploymentProtected,
            state.MissionEnded);
        _missionPhase = state.Phase;
        _missionRemaining = state.Remaining;
        _missionOnline = state.Online;
        _objectiveStage = state.ObjectiveStage;
        _currentObjective = state.Objective;
        _reinforcementPending = state.ReinforcementPending;
        _reinforcementsDeployed = state.ReinforcementsDeployed;
        _reinforcementCountdown = state.ReinforcementCountdown;
        _enemiesRemaining = state.EnemiesRemaining;
        _missionEnded = state.MissionEnded;
        _extractionMissionSucceeded = state.MissionSucceeded;
        _worldBossDefeated = state.WorldBossDefeated;
        _hud.SetMissionPhase(state.Phase, state.Remaining, state.Online);
        _hud.SetEnemyCount(state.EnemiesRemaining);
        RefreshLocalizedObjective();
        ApplyNetworkObjectiveVisuals(state.ObjectiveStage);
        if (state.ExtractionActive)
        {
            _extractionCountdownActive = true;
            _extractionRemaining = state.ExtractionRemaining;
            _extractionAircraft?.BeginInbound();
            UpdateExtractionHud();
        }
        else if (_extractionCountdownActive)
        {
            _extractionCountdownActive = false;
            _extractionRemaining = ExtractionCountdownDuration;
            _hud.HideExtractionCountdown();
            _extractionAircraft?.AbortPickup();
        }
        UpdateWorldBossTracking();
        ApplyExtractionNetworkOutcome(state);
    }

    private void ApplyNetworkObjectiveVisuals(int completedCount)
    {
        for (var index = 0; index < _objectiveScreens.Count; index++)
        {
            if (index >= completedCount)
            {
                continue;
            }
            _objectiveScreens[index].AlbedoColor = new Color(0.1f, 0.9f, 0.58f);
            _objectiveScreens[index].Emission = new Color(0.04f, 0.95f, 0.5f);
            _objectiveLights[index].LightColor = new Color(0.06f, 1.0f, 0.5f);
        }
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = true;
            _extractionMarker.Scale = completedCount >= _objectiveTerminals.Count
                ? Vector3.One * 1.15f
                : Vector3.One;
        }
    }

    private void OnExtractionWorldReady(long peerId)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || _squadNetwork.ExtractionWorldLaunchStarted
            || (peerId != 1 && _squadNetwork.ExtractionSlotForPeer(peerId) < 1))
        {
            return;
        }
        BroadcastExtractionWorldSnapshot();
        _squadNetwork.BroadcastExtractionMissionState(CaptureExtractionMissionState());
        if (peerId > 1)
        {
            SendAllExtractionLootStates(peerId);
            SendAllExtractionDoorStates(peerId);
        }
        TryLaunchExtractionWorldIfReady();
    }

    private void TryLaunchExtractionWorldIfReady()
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || _squadNetwork.ExtractionWorldLaunchStarted)
        {
            return;
        }
        var expectedReady = _squadNetwork.RegisteredExtractionPlayerCount;
        if (_squadNetwork.ExtractionWorldReadyPlayerCount >= expectedReady)
        {
            var bootstrap = ExtractionWorldStateCodec.Encode(CaptureExtractionWorldState());
            for (var slot = 1; slot < SquadNetwork.ExtractionSquadCapacity; slot++)
            {
                var peerId = _squadNetwork.ExtractionPeerForSlot(slot);
                if (peerId <= 1)
                {
                    continue;
                }
                _squadNetwork.SendExtractionWorldBootstrapState(peerId, bootstrap);
                SendAllExtractionLootStates(peerId);
                SendAllExtractionDoorStates(peerId);
            }
            _squadNetwork.BroadcastExtractionMissionState(CaptureExtractionMissionState());
            _squadNetwork.BroadcastExtractionWorldLaunch();
            return;
        }
        _hud.SetSquadStatus(
            $"WORLD READY  //  {_squadNetwork.ExtractionWorldReadyPlayerCount}/{expectedReady} OPERATORS");
    }
}
