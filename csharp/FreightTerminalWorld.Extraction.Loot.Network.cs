using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const int EnemyLootSourceBase = 100000;
    private const int SquadBodyBagSourceBase = 200000;
    private const int DynamicLootSourceBase = 300000;

    private readonly Dictionary<int, ILootSource> _extractionLootSources = new();
    private readonly Dictionary<ulong, int> _extractionLootIds = new();
    private readonly Dictionary<int, long> _extractionLootLeaseOwners = new();
    private ILootSource? _pendingExtractionLootOpen;
    private int _nextExtractionDynamicLootId = DynamicLootSourceBase;

    private void InitializeExtractionLootNetwork()
    {
        if (!IsExtractionNetworkMatch)
        {
            return;
        }
        _extractionLootSources.Clear();
        _extractionLootIds.Clear();
        var ordered = _lootSources
            .Where(source => source is not EnemyOperator
                && source is not SquadBodyBag
                && IsInstanceValid(source.LootNode))
            .OrderBy(source => source.GetType().FullName, StringComparer.Ordinal)
            .ThenBy(source => Mathf.RoundToInt(source.LootNode.GlobalPosition.X * 10.0f))
            .ThenBy(source => Mathf.RoundToInt(source.LootNode.GlobalPosition.Y * 10.0f))
            .ThenBy(source => Mathf.RoundToInt(source.LootNode.GlobalPosition.Z * 10.0f))
            .ThenBy(source => source.LootNode.Name.ToString(), StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            RegisterExtractionLootSource(ordered[index], index + 1);
        }
    }

    private void RegisterExtractionLootSource(ILootSource source, int sourceId)
    {
        if (sourceId <= 0 || !IsInstanceValid(source.LootNode))
        {
            return;
        }
        _extractionLootSources[sourceId] = source;
        _extractionLootIds[source.LootNode.GetInstanceId()] = sourceId;
    }

    private int EnsureExtractionLootSourceId(ILootSource source)
    {
        if (_extractionLootIds.TryGetValue(source.LootNode.GetInstanceId(), out var sourceId))
        {
            return sourceId;
        }
        sourceId = source switch
        {
            EnemyOperator enemy => EnemyLootSourceBase + enemy.NetworkId,
            SquadBodyBag => _nextExtractionDynamicLootId++,
            _ => _nextExtractionDynamicLootId++
        };
        RegisterExtractionLootSource(source, sourceId);
        return sourceId;
    }

    private static ExtractionLootSourceKind ExtractionLootKind(ILootSource source)
        => source switch
        {
            EnemyOperator => ExtractionLootSourceKind.EnemyCorpse,
            SquadBodyBag => ExtractionLootSourceKind.SquadBodyBag,
            AircraftSupplyDrop => ExtractionLootSourceKind.SupplyDrop,
            GradedLootPickup graded when graded.Name.ToString().StartsWith("DroppedLoot", StringComparison.Ordinal)
                => ExtractionLootSourceKind.Dropped,
            _ => ExtractionLootSourceKind.Static
        };

    private ExtractionLootSourceNetworkState CaptureExtractionLootSourceState(
        int sourceId,
        ILootSource source,
        bool granted)
        => new(
            sourceId,
            ExtractionLootKind(source),
            source.LootNode.GlobalPosition,
            source is IOpenableLootSource { IsOpened: true },
            granted,
            ExtractionLootNetworkCodec.SerializeItems(source.Loot));

    private void SendAllExtractionLootStates(long peerId)
    {
        foreach (var pair in _extractionLootSources.OrderBy(pair => pair.Key))
        {
            if (IsInstanceValid(pair.Value.LootNode))
            {
                _squadNetwork.SendExtractionLootState(
                    peerId,
                    CaptureExtractionLootSourceState(pair.Key, pair.Value, granted: false));
            }
        }
    }

    private void OnExtractionLootOpenRequested(long peerId, int sourceId)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || !_extractionLootSources.TryGetValue(sourceId, out var source)
            || !IsInstanceValid(source.LootNode))
        {
            return;
        }
        var actor = ResolveExtractionPeerActor(peerId);
        var available = IsInstanceValid(actor)
            && actor!.GlobalPosition.DistanceTo(source.LootNode.GlobalPosition) <= 3.2f
            && source.IsSearchable
            && (!_extractionLootLeaseOwners.TryGetValue(sourceId, out var owner) || owner == peerId);
        if (!available)
        {
            _squadNetwork.SendExtractionLootState(
                peerId,
                CaptureExtractionLootSourceState(sourceId, source, granted: false));
            return;
        }
        _extractionLootLeaseOwners[sourceId] = peerId;
        source.OnSearched();
        _squadNetwork.SendExtractionLootState(
            peerId,
            CaptureExtractionLootSourceState(sourceId, source, granted: true));
    }

    private void OnExtractionLootMutationReceived(
        long peerId,
        int sourceId,
        bool opened,
        string itemsJson)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost
            || itemsJson.Length > 131072
            || !_extractionLootLeaseOwners.TryGetValue(sourceId, out var owner)
            || owner != peerId
            || !_extractionLootSources.TryGetValue(sourceId, out var source)
            || !IsInstanceValid(source.LootNode))
        {
            return;
        }
        var items = ExtractionLootNetworkCodec.DeserializeItems(itemsJson);
        if (items.Count > 64)
        {
            return;
        }
        if (opened)
        {
            source.OnSearched();
        }
        source.Loot.Clear();
        source.Loot.AddRange(items);
        RefreshNetworkLootPresentation(source);
        _squadNetwork.BroadcastExtractionLootState(
            CaptureExtractionLootSourceState(sourceId, source, granted: false));
        RetireEmptyGradedLootPickup(source);
    }

    private void OnExtractionLootCloseRequested(long peerId, int sourceId)
    {
        if (_extractionLootLeaseOwners.TryGetValue(sourceId, out var owner) && owner == peerId)
        {
            _extractionLootLeaseOwners.Remove(sourceId);
        }
    }

    private void OnExtractionLootDropRequested(long peerId, Vector3 position, string itemJson)
    {
        if (!IsExtractionNetworkMatch || !_squadNetwork.IsHost || itemJson.Length > 32768)
        {
            return;
        }
        var actor = ResolveExtractionPeerActor(peerId);
        var items = ExtractionLootNetworkCodec.DeserializeItems(itemJson);
        if (!IsInstanceValid(actor) || items.Count != 1
            || actor!.GlobalPosition.DistanceTo(position) > 4.0f)
        {
            return;
        }
        var item = items[0];
        var pickup = new GradedLootPickup
        {
            Name = $"DroppedLoot{_nextDroppedLootId++}"
        };
        pickup.ConfigureDropped(
            item,
            $"Dropped {item.DisplayName("en")}",
            $"\u4e22\u5f03\u7269  {item.DisplayName("zh")}");
        AddChild(pickup);
        pickup.GlobalPosition = position;
        _lootSources.Add(pickup);
        var sourceId = _nextExtractionDynamicLootId++;
        RegisterExtractionLootSource(pickup, sourceId);
        _squadNetwork.BroadcastExtractionLootState(
            CaptureExtractionLootSourceState(sourceId, pickup, granted: false));
    }

    private void OnExtractionLootState(ExtractionLootSourceNetworkState state)
    {
        if (!IsExtractionNetworkClient)
        {
            return;
        }
        var items = ExtractionLootNetworkCodec.DeserializeItems(state.ItemsJson);
        var source = ResolveExtractionLootSource(state, items);
        if (source is null || !IsInstanceValid(source.LootNode))
        {
            return;
        }
        source.Loot.Clear();
        source.Loot.AddRange(items);
        if (state.Opened)
        {
            source.OnSearched();
        }
        source.LootNode.GlobalPosition = state.Position;
        RefreshNetworkLootPresentation(source);
        if (ReferenceEquals(_pendingExtractionLootOpen, source))
        {
            _pendingExtractionLootOpen = null;
            if (state.Granted)
            {
                OpenLootLocal(source);
            }
            else
            {
                _hud.ShowLocalizedMessage(
                    "loot_busy",
                    "LOOT SOURCE IN USE  //  WAIT FOR SQUADMATE",
                    new Color(1.0f, 0.62f, 0.24f));
            }
        }
        if (ReferenceEquals(_openLootSource, source))
        {
            RefreshLootView();
        }
    }

    private ILootSource? ResolveExtractionLootSource(
        ExtractionLootSourceNetworkState state,
        IReadOnlyList<LootItem> items)
    {
        if (_extractionLootSources.TryGetValue(state.SourceId, out var source)
            && IsInstanceValid(source.LootNode))
        {
            return source;
        }
        if (state.Kind == ExtractionLootSourceKind.EnemyCorpse)
        {
            var enemyId = state.SourceId - EnemyLootSourceBase;
            if (_extractionNetworkEnemies.TryGetValue(enemyId, out var enemy) && IsInstanceValid(enemy))
            {
                RegisterExtractionLootSource(enemy, state.SourceId);
                if (!_lootSources.Contains(enemy))
                {
                    _lootSources.Add(enemy);
                }
                return enemy;
            }
            return null;
        }
        if (state.Kind == ExtractionLootSourceKind.Static || items.Count == 0)
        {
            return null;
        }
        if (state.Kind == ExtractionLootSourceKind.SquadBodyBag)
        {
            var bag = new SquadBodyBag
            {
                Name = $"NetworkBodyBag_{state.SourceId}",
                Position = state.Position,
                EnglishName = "Squad body bag",
                ChineseName = "\u5c0f\u961f\u9057\u4f53\u888b"
            };
            AddChild(bag);
            _lootSources.Add(bag);
            RegisterExtractionLootSource(bag, state.SourceId);
            return bag;
        }
        var item = items[0];
        var pickup = new GradedLootPickup
        {
            Name = $"NetworkDroppedLoot_{state.SourceId}",
            Position = state.Position
        };
        pickup.ConfigureDropped(
            item,
            $"Dropped {item.DisplayName("en")}",
            $"\u4e22\u5f03\u7269  {item.DisplayName("zh")}");
        AddChild(pickup);
        _lootSources.Add(pickup);
        RegisterExtractionLootSource(pickup, state.SourceId);
        return pickup;
    }

    private Node3D? ResolveExtractionPeerActor(long peerId)
        => peerId == 1
            ? _player
            : _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate.IsHumanProxy && mate.NetworkPeerId == peerId);

    private void PublishExtractionLootMutation(ILootSource source)
    {
        if (!IsExtractionNetworkMatch || !IsInstanceValid(source.LootNode))
        {
            return;
        }
        var sourceId = EnsureExtractionLootSourceId(source);
        var opened = source is IOpenableLootSource { IsOpened: true };
        var itemsJson = ExtractionLootNetworkCodec.SerializeItems(source.Loot);
        if (_squadNetwork.IsHost)
        {
            _squadNetwork.BroadcastExtractionLootState(
                CaptureExtractionLootSourceState(sourceId, source, granted: false));
        }
        else
        {
            _squadNetwork.SendExtractionLootMutation(sourceId, opened, itemsJson);
        }
    }

    private static void RefreshNetworkLootPresentation(ILootSource source)
    {
        RefreshGradedLootPickupPresentation(source);
        if (source is EnemyOperator enemy
            && !source.Loot.Any(item => item.Kind == LootItemKind.Weapon))
        {
            enemy.MarkCarriedWeaponRemoved();
        }
    }
}
