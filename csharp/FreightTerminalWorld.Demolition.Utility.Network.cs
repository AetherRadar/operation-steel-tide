using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float MaximumRemoteUtilityOriginError = 4.25f;
    private readonly Dictionary<long, DemolitionBotUtilityInventory>
        _demolitionRemoteUtilityInventories = new();
    private readonly Dictionary<long, int> _demolitionRemoteUtilityRequestIds = new();
    private readonly Dictionary<int, DemolitionNetworkUtilityKind>
        _pendingLocalDemolitionUtilityThrows = new();
    private readonly HashSet<int> _receivedDemolitionUtilitySpawnIds = new();
    private bool _demolitionUtilityNetworkAttached;
    private bool _suppressDemolitionUtilityReplication;
    private int _nextLocalDemolitionUtilityRequestId;
    private int _nextDemolitionUtilitySpawnId;
    private int _demolitionFriendlyBotRoundFunds;
    private int _demolitionOpponentBotRoundFunds;

    internal int DemolitionFriendlyBotRoundFunds => _demolitionFriendlyBotRoundFunds;
    internal int DemolitionOpponentBotRoundFunds => _demolitionOpponentBotRoundFunds;

    private void AttachDemolitionUtilityNetwork()
    {
        if (_demolitionUtilityNetworkAttached || !IsInstanceValid(_squadNetwork))
        {
            return;
        }
        _squadNetwork.DemolitionUtilityThrowRequested += OnDemolitionUtilityThrowRequested;
        _squadNetwork.DemolitionUtilityThrowSpawnReceived += OnDemolitionUtilityThrowSpawnReceived;
        _squadNetwork.DemolitionUtilityThrowRejected += OnDemolitionUtilityThrowRejected;
        _demolitionUtilityNetworkAttached = true;
    }

    private void DetachDemolitionUtilityNetwork()
    {
        if (!_demolitionUtilityNetworkAttached || !IsInstanceValid(_squadNetwork))
        {
            _demolitionUtilityNetworkAttached = false;
            return;
        }
        _squadNetwork.DemolitionUtilityThrowRequested -= OnDemolitionUtilityThrowRequested;
        _squadNetwork.DemolitionUtilityThrowSpawnReceived -= OnDemolitionUtilityThrowSpawnReceived;
        _squadNetwork.DemolitionUtilityThrowRejected -= OnDemolitionUtilityThrowRejected;
        _demolitionUtilityNetworkAttached = false;
    }

    private void CaptureDemolitionBotUtilityBudgetsForRound()
    {
        _demolitionFriendlyBotRoundFunds = _demolitionPlayerEconomy.Funds;
        _demolitionOpponentBotRoundFunds = _demolitionOpponentEconomy.Funds;
    }

    private void ResetDemolitionUtilityNetworkForRound()
    {
        _demolitionRemoteUtilityInventories.Clear();
        _demolitionRemoteUtilityRequestIds.Clear();
        _pendingLocalDemolitionUtilityThrows.Clear();
        _receivedDemolitionUtilitySpawnIds.Clear();
        _nextLocalDemolitionUtilityRequestId = 0;
        _nextDemolitionUtilitySpawnId = 0;
    }

    private void RecordDemolitionRemoteUtilityPurchase(
        long peerId,
        int round,
        DemolitionPurchaseQuote quote)
    {
        if (!_squadNetwork.IsHost
            || peerId <= 1
            || round != _demolitionMatch.CurrentRound
            || !quote.Affordable)
        {
            return;
        }
        _demolitionRemoteUtilityInventories[peerId] = DemolitionRemoteUtilityInventoryForQuote(quote);
    }

    private static DemolitionBotUtilityInventory DemolitionRemoteUtilityInventoryForQuote(
        DemolitionPurchaseQuote quote)
    {
        var selection = DemolitionBuyCatalog.Normalize(quote.Selection);
        return new DemolitionBotUtilityInventory(
            selection.GrenadeCount,
            selection.SmokeGrenadeCount,
            selection.IncendiaryGrenadeCount);
    }

    internal bool TryRequestLocalDemolitionUtilityThrow(
        DemolitionNetworkUtilityKind kind,
        Vector3 origin,
        Vector3 direction)
    {
        if (!IsDemolitionNetworkClient
            || !_demolitionRoundActive
            || _missionEnded
            || !DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                _demolitionMatch.CurrentRound,
                1,
                (int)kind,
                origin,
                direction))
        {
            return false;
        }
        var available = _player.DemolitionUtilityCount(kind);
        var pending = 0;
        foreach (var pendingKind in _pendingLocalDemolitionUtilityThrows.Values)
        {
            if (pendingKind == kind)
            {
                pending++;
            }
        }
        if (available <= pending)
        {
            return false;
        }

        var requestId = ++_nextLocalDemolitionUtilityRequestId;
        _pendingLocalDemolitionUtilityThrows[requestId] = kind;
        if (_squadNetwork.RequestDemolitionUtilityThrow(
                _demolitionMatch.CurrentRound,
                requestId,
                kind,
                origin,
                direction.Normalized()))
        {
            return true;
        }
        _pendingLocalDemolitionUtilityThrows.Remove(requestId);
        return false;
    }

    private void OnDemolitionUtilityThrowRequested(DemolitionUtilityThrowRequest request)
    {
        if (!_squadNetwork.IsHost || request.PeerId <= 1)
        {
            return;
        }
        if (!TryAdvanceDemolitionUtilityRequestHighWater(
                request.PeerId,
                request.Round,
                request.RequestId))
        {
            _squadNetwork.RejectDemolitionUtilityThrow(request.PeerId, request.RequestId);
            return;
        }

        var registered = TryResolveRemoteDemolitionUtilityActor(
            request.PeerId,
            out var actor,
            out var actorId,
            out var alive);
        var expectedOrigin = registered
            ? DemolitionUtilityOrigin(actor!)
            : Vector3.Zero;
        var sourceNearActor = registered
            && expectedOrigin.DistanceTo(request.Origin) <= MaximumRemoteUtilityOriginError;
        var inventoryCount = _demolitionRemoteUtilityInventories.TryGetValue(
                request.PeerId,
                out var inventory)
            ? UtilityCount(inventory, request.Kind)
            : 0;
        var authorized = DemolitionUtilityNetworkContract.HostMayAuthorize(
            _squadNetwork.IsHost,
            _demolitionMode && _squadNetwork.IsDemolitionSession,
            _demolitionRoundActive && !_missionEnded,
            registered,
            alive,
            request.Round == _demolitionMatch.CurrentRound,
            sourceNearActor,
            inventoryCount);
        if (!authorized)
        {
            _squadNetwork.RejectDemolitionUtilityThrow(request.PeerId, request.RequestId);
            return;
        }

        _demolitionRemoteUtilityInventories[request.PeerId] = ConsumeUtility(
            inventory,
            request.Kind);
        UtilityBallistics(request.Kind, out var speed, out var loft);
        var spawn = new DemolitionUtilityThrowSpawn(
            ++_nextDemolitionUtilitySpawnId,
            request.Round,
            request.PeerId,
            actorId,
            request.RequestId,
            request.Kind,
            expectedOrigin,
            request.Direction.Normalized(),
            speed,
            loft);
        SpawnDemolitionUtilityProjectile(spawn, actor!, authoritativeDamage: true);
        _squadNetwork.BroadcastDemolitionUtilityThrow(spawn);
    }

    private bool TryAdvanceDemolitionUtilityRequestHighWater(
        long peerId,
        int requestRound,
        int requestId)
    {
        // Reliable packets can arrive after a round reset. Reject the stale/future
        // packet before it can poison the fresh round's request-id high-water mark.
        if (requestRound != _demolitionMatch.CurrentRound
            || _demolitionRemoteUtilityRequestIds.TryGetValue(peerId, out var previousRequestId)
                && requestId <= previousRequestId)
        {
            return false;
        }
        _demolitionRemoteUtilityRequestIds[peerId] = requestId;
        return true;
    }

    private void OnDemolitionUtilityThrowSpawnReceived(DemolitionUtilityThrowSpawn spawn)
    {
        if (!IsDemolitionNetworkClient
            || spawn.Round != _demolitionMatch.CurrentRound
            || !_receivedDemolitionUtilitySpawnIds.Add(spawn.SpawnId))
        {
            return;
        }
        if (spawn.SourcePeerId == Multiplayer.GetUniqueId() && spawn.RequestId > 0)
        {
            _pendingLocalDemolitionUtilityThrows.Remove(spawn.RequestId);
            _player.TryConsumeDemolitionNetworkUtility(spawn.Kind);
        }
        var owner = DemolitionActorForId(spawn.SourceActorId) ?? this;
        SpawnDemolitionUtilityProjectile(spawn, owner, authoritativeDamage: false);
    }

    private void OnDemolitionUtilityThrowRejected(int requestId)
        => _pendingLocalDemolitionUtilityThrows.Remove(requestId);

    private void NotifyHostDemolitionUtilitySpawned(
        DemolitionNetworkUtilityKind kind,
        Vector3 origin,
        Vector3 direction,
        Node source,
        float speed,
        float loft)
    {
        if (_suppressDemolitionUtilityReplication
            || !_demolitionMode
            || !_demolitionRoundActive
            || !_squadNetwork.IsOnline
            || !_squadNetwork.IsHost
            || source is not Node3D actor)
        {
            return;
        }
        var actorId = DemolitionActorIdForNode(actor);
        if (actorId < DemolitionAlphaActorBase)
        {
            return;
        }
        var sourcePeerId = actor switch
        {
            SquadMate mate when mate.IsHumanProxy && mate.NetworkPeerId > 1 => mate.NetworkPeerId,
            EnemyOperator enemy when enemy.IsHumanProxy && enemy.NetworkPeerId > 1 => enemy.NetworkPeerId,
            _ => 1
        };
        var spawn = new DemolitionUtilityThrowSpawn(
            ++_nextDemolitionUtilitySpawnId,
            _demolitionMatch.CurrentRound,
            sourcePeerId,
            actorId,
            0,
            kind,
            origin,
            direction.Normalized(),
            speed,
            loft);
        _squadNetwork.BroadcastDemolitionUtilityThrow(spawn);
    }

    private void SpawnDemolitionUtilityProjectile(
        DemolitionUtilityThrowSpawn spawn,
        Node owner,
        bool authoritativeDamage)
    {
        _suppressDemolitionUtilityReplication = true;
        try
        {
            switch (spawn.Kind)
            {
                case DemolitionNetworkUtilityKind.Fragmentation:
                    var frag = new FragGrenade
                    {
                        Position = spawn.Origin,
                        OwnerBody = owner,
                        Main = this,
                        DamageEnabled = authoritativeDamage
                    };
                    AddChild(frag);
                    frag.Arm(spawn.Direction, spawn.Speed, spawn.Loft);
                    break;
                case DemolitionNetworkUtilityKind.Smoke:
                    ThrowSmokeGrenade(
                        spawn.Origin,
                        spawn.Direction,
                        owner,
                        spawn.Speed,
                        spawn.Loft);
                    break;
                case DemolitionNetworkUtilityKind.Incendiary:
                    var incendiary = new IncendiaryGrenade
                    {
                        Position = spawn.Origin,
                        OwnerBody = owner,
                        DamageEnabled = authoritativeDamage
                    };
                    AddChild(incendiary);
                    incendiary.Arm(spawn.Direction, spawn.Speed, spawn.Loft);
                    break;
            }
        }
        finally
        {
            _suppressDemolitionUtilityReplication = false;
        }
    }

    internal void PresentReplicatedFragExplosion(Vector3 position)
    {
        ReportGunshot(position, 70.0f);
        SpawnExplosionEffect(position);
    }

    private bool TryResolveRemoteDemolitionUtilityActor(
        long peerId,
        out Node3D? actor,
        out int actorId,
        out bool alive)
    {
        actor = null;
        actorId = -1;
        alive = false;
        if (!_squadNetwork.TryGetDemolitionAssignment(peerId, out var team, out var slot, out _)
            || !_demolitionNetworkPlayers.TryGetValue(peerId, out var state)
            || state.Team != team
            || state.Slot != slot)
        {
            return false;
        }
        actorId = DemolitionActorId(team, slot);
        actor = DemolitionActorForId(actorId);
        if (!IsInstanceValid(actor))
        {
            return false;
        }
        alive = !state.Dead && actor switch
        {
            SquadMate mate => !mate.IsDowned && !mate.IsBodyBag,
            EnemyOperator enemy => !enemy.IsDead,
            TacticalPlayer player => !player.IsDead,
            _ => false
        };
        return true;
    }

    private static Vector3 DemolitionUtilityOrigin(Node3D actor)
        => actor switch
        {
            SquadMate mate => mate.DemolitionUtilityThrowOrigin,
            EnemyOperator enemy => enemy.DemolitionUtilityThrowOrigin,
            _ => actor.GlobalPosition + Vector3.Up * 1.35f
        };

    private static void UtilityBallistics(
        DemolitionNetworkUtilityKind kind,
        out float speed,
        out float loft)
    {
        speed = kind == DemolitionNetworkUtilityKind.Fragmentation ? 15.0f : 14.0f;
        loft = kind == DemolitionNetworkUtilityKind.Fragmentation ? 5.2f : 5.0f;
    }

    private static int UtilityCount(
        DemolitionBotUtilityInventory inventory,
        DemolitionNetworkUtilityKind kind)
        => kind switch
        {
            DemolitionNetworkUtilityKind.Fragmentation => inventory.FragmentationGrenades,
            DemolitionNetworkUtilityKind.Smoke => inventory.SmokeGrenades,
            DemolitionNetworkUtilityKind.Incendiary => inventory.IncendiaryGrenades,
            _ => 0
        };

    private static DemolitionBotUtilityInventory ConsumeUtility(
        DemolitionBotUtilityInventory inventory,
        DemolitionNetworkUtilityKind kind)
        => kind switch
        {
            DemolitionNetworkUtilityKind.Fragmentation => inventory with
            {
                FragmentationGrenades = Mathf.Max(0, inventory.FragmentationGrenades - 1)
            },
            DemolitionNetworkUtilityKind.Smoke => inventory with
            {
                SmokeGrenades = Mathf.Max(0, inventory.SmokeGrenades - 1)
            },
            DemolitionNetworkUtilityKind.Incendiary => inventory with
            {
                IncendiaryGrenades = Mathf.Max(0, inventory.IncendiaryGrenades - 1)
            },
            _ => inventory
        };
}
