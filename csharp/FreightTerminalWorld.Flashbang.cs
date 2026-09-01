using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int MaximumActiveFlashbangGrenades = 6;
    private readonly List<FlashbangGrenade> _activeFlashbangGrenades = new();

    internal int ActiveFlashbangCountForDiagnostics => _activeFlashbangGrenades.Count;

    public void ThrowFlashbangGrenade(Vector3 origin, Vector3 direction, Node source)
        => ThrowFlashbangGrenade(origin, direction, source, 14.0f, 5.0f);

    public void ThrowFlashbangGrenade(
        Vector3 origin,
        Vector3 direction,
        Node source,
        float speed,
        float loft)
    {
        var grenade = SpawnFlashbangGrenade(
            origin,
            direction,
            source,
            speed,
            loft,
            networkSpawnId: 0,
            networkRound: 0,
            waitForAuthoritativeDetonation: false);
        var spawn = NotifyHostDemolitionUtilitySpawned(
            DemolitionNetworkUtilityKind.Flashbang,
            origin,
            direction,
            source,
            speed,
            loft);
        if (spawn is { } networkSpawn)
        {
            grenade.ConfigureNetworkReplication(
                networkSpawn.SpawnId,
                networkSpawn.Round,
                waitForAuthoritativeDetonation: false);
        }
    }

    internal FlashbangGrenade SpawnReplicatedFlashbangGrenade(
        DemolitionUtilityThrowSpawn spawn,
        Node source,
        bool waitForAuthoritativeDetonation)
        => SpawnFlashbangGrenade(
            spawn.Origin,
            spawn.Direction,
            source,
            spawn.Speed,
            spawn.Loft,
            spawn.SpawnId,
            spawn.Round,
            waitForAuthoritativeDetonation);

    internal void NotifyAuthoritativeFlashbangDetonated(
        FlashbangGrenade grenade,
        Vector3 position)
    {
        if (!_squadNetwork.IsOnline
            || !_squadNetwork.IsHost
            || !_demolitionRoundActive
            || grenade.NetworkSpawnId < 1
            || grenade.NetworkRound != _demolitionMatch.CurrentRound)
        {
            return;
        }
        _squadNetwork.BroadcastDemolitionFlashbangDetonation(
            new DemolitionFlashbangDetonation(
                grenade.NetworkSpawnId,
                grenade.NetworkRound,
                position));
    }

    internal void RegisterActiveFlashbangGrenade(FlashbangGrenade grenade)
    {
        if (_activeFlashbangGrenades.Contains(grenade))
        {
            return;
        }
        for (var index = _activeFlashbangGrenades.Count - 1; index >= 0; index--)
        {
            if (!IsInstanceValid(_activeFlashbangGrenades[index]))
            {
                _activeFlashbangGrenades.RemoveAt(index);
            }
        }
        while (_activeFlashbangGrenades.Count >= MaximumActiveFlashbangGrenades)
        {
            var oldest = _activeFlashbangGrenades[0];
            _activeFlashbangGrenades.RemoveAt(0);
            if (IsInstanceValid(oldest))
            {
                oldest.QueueFree();
            }
        }
        _activeFlashbangGrenades.Add(grenade);
    }

    internal void UnregisterActiveFlashbangGrenade(FlashbangGrenade grenade)
    {
        _activeFlashbangGrenades.Remove(grenade);
        if (grenade.NetworkSpawnId >= 1
            && _replicatedFlashbangsBySpawnId.TryGetValue(
                grenade.NetworkSpawnId,
                out var replicated)
            && replicated == grenade)
        {
            _replicatedFlashbangsBySpawnId.Remove(grenade.NetworkSpawnId);
        }
    }

    private FlashbangGrenade SpawnFlashbangGrenade(
        Vector3 origin,
        Vector3 direction,
        Node source,
        float speed,
        float loft,
        int networkSpawnId,
        int networkRound,
        bool waitForAuthoritativeDetonation)
    {
        var grenade = new FlashbangGrenade
        {
            Position = origin,
            OwnerBody = source
        };
        grenade.ConfigureNetworkReplication(
            networkSpawnId,
            networkRound,
            waitForAuthoritativeDetonation);
        AddChild(grenade);
        grenade.Arm(direction, speed, loft);
        return grenade;
    }
}
