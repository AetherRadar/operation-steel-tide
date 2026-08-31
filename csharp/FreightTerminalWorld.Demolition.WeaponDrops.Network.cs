using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private sealed class RemoteDemolitionWeaponSlots
    {
        public WeaponBuild? Primary { get; set; }
        public WeaponBuild? Secondary { get; set; }
        public WeaponBuild? Sidearm { get; set; }
        public WeaponBuild? Carried { get; set; }
    }

    private readonly Dictionary<int, DroppedWeaponPickup> _demolitionWeaponDropsById = new();
    private readonly Dictionary<long, RemoteDemolitionWeaponSlots> _demolitionRemotePurchasedWeapons = new();
    private int _pendingDemolitionWeaponPickupDropId = -1;
    private int _pendingDemolitionWeaponPickupRevision = -1;

    private void RecordDemolitionRemotePurchasedWeapon(
        long peerId,
        DemolitionPurchaseQuote quote)
    {
        if (!_squadNetwork.IsHost || peerId <= 1 || !quote.Affordable)
        {
            return;
        }
        var slots = CreateRemoteDemolitionWeaponSlots(quote);
        _demolitionRemotePurchasedWeapons[peerId] = slots;

        if (!_squadNetwork.TryGetDemolitionAssignment(
                peerId,
                out var team,
                out var slot,
                out _))
        {
            return;
        }
        var actor = DemolitionActorForId(DemolitionActorId(team, slot));
        ApplyRemoteDemolitionCarriedWeapon(actor, slots.Carried);
    }

    private static RemoteDemolitionWeaponSlots CreateRemoteDemolitionWeaponSlots(
        DemolitionPurchaseQuote quote)
    {
        var loadout = DemolitionBuyCatalog.BuildLoadout(quote);
        var primary = loadout.Weapon?.Clone();
        var sidearm = loadout.Sidearm?.Clone();
        return new RemoteDemolitionWeaponSlots
        {
            Primary = primary,
            Sidearm = sidearm,
            Carried = primary?.Clone() ?? sidearm?.Clone()
        };
    }

    private void RemoveDemolitionRemotePurchasedWeapon(long peerId)
        => _demolitionRemotePurchasedWeapons.Remove(peerId);

    private void ApplyDemolitionDisconnectBotLoadout(DemolitionPlayerNetworkState departed)
    {
        if (departed.PeerId == 0 || departed.Team != _demolitionLocalNetworkTeam)
        {
            return;
        }
        var replacement = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && !mate.IsHumanProxy
            && mate.SquadSlot == departed.Slot);
        replacement?.ConfigureDemolitionRoundLoadout(
            DemolitionBotLoadoutPlanner.BuildForSlot(
                _demolitionFriendlyBotRoundFunds,
                departed.Slot));
    }

    private static void ApplyRemoteDemolitionCarriedWeapon(Node3D? actor, WeaponBuild? weapon)
    {
        switch (actor)
        {
            case SquadMate mate when weapon is not null:
                mate.EquipWeaponFromLoot(weapon, LootGrade.Common, LootGrade.Common);
                break;
            case SquadMate mate:
                mate.ApplyColdStartUnarmed();
                break;
            case EnemyOperator enemy:
                if (weapon is null)
                {
                    enemy.ApplyColdStartUnarmed();
                }
                else
                {
                    enemy.EquipWeaponFromLoot(weapon);
                }
                SynchronizeDemolitionEnemyWeaponLoot(enemy, weapon);
                break;
        }
    }

    private static void SynchronizeDemolitionEnemyWeaponLoot(
        EnemyOperator enemy,
        WeaponBuild? weapon)
    {
        var previous = enemy.Loot.FirstOrDefault(item => item.Kind == LootItemKind.Weapon);
        for (var index = enemy.Loot.Count - 1; index >= 0; index--)
        {
            if (enemy.Loot[index].Kind == LootItemKind.Weapon)
            {
                enemy.Loot.RemoveAt(index);
            }
        }
        if (weapon is null)
        {
            return;
        }
        enemy.Loot.Add(new LootItem
        {
            Id = previous?.Id ?? System.Guid.NewGuid().ToString("N"),
            Kind = LootItemKind.Weapon,
            Weapon = weapon.Clone(),
            Grade = previous?.Grade ?? LootGrade.Common,
            Quantity = 1
        });
    }

    private void ApplyRecordedRemoteDemolitionCarriedWeapon(long peerId, Node3D actor)
    {
        if (!_demolitionRemotePurchasedWeapons.TryGetValue(peerId, out var slots)
            || DemolitionActorWeaponMatches(actor, slots.Carried))
        {
            return;
        }
        ApplyRemoteDemolitionCarriedWeapon(actor, slots.Carried);
    }

    private void RequestDemolitionWeaponDropPickup(DroppedWeaponPickup pickup)
    {
        if (_pendingDemolitionWeaponPickupDropId >= 0
            || !DemolitionWeaponDropNetworkRules.MatchesCurrentRevision(
                pickup,
                _demolitionMatch.CurrentRound,
                pickup.DropId,
                pickup.Revision))
        {
            return;
        }
        if (_squadNetwork.RequestDemolitionWeaponPickup(
                pickup.DemolitionRound,
                pickup.DropId,
                pickup.Revision))
        {
            _pendingDemolitionWeaponPickupDropId = pickup.DropId;
            _pendingDemolitionWeaponPickupRevision = pickup.Revision;
        }
    }

    private bool TryEquipLocalPlayerFromDemolitionWeaponDrop(DroppedWeaponPickup pickup)
    {
        if (!TryApplyLocalDemolitionWeaponPickup(
                pickup,
                pickup.Revision,
                out _,
                out var state))
        {
            return false;
        }
        _squadNetwork.BroadcastDemolitionWeaponDropState(state);
        return true;
    }

    private bool TryApplyLocalDemolitionWeaponPickup(
        DroppedWeaponPickup pickup,
        int expectedRevision,
        out LootItem? awarded,
        out DemolitionWeaponDropNetworkState state)
    {
        awarded = null;
        if (!DemolitionWeaponDropNetworkRules.MatchesCurrentRevision(
                pickup,
                _demolitionMatch.CurrentRound,
                pickup.DropId,
                expectedRevision))
        {
            state = CaptureDemolitionWeaponDropState(pickup);
            return false;
        }
        var itemIndex = pickup.Loot.FindIndex(
            item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
        if (itemIndex < 0)
        {
            state = CaptureDemolitionWeaponDropState(pickup);
            return false;
        }
        awarded = pickup.Loot[itemIndex];
        var replacement = _player.EquipFromLoot(awarded);
        if (ReferenceEquals(replacement, awarded))
        {
            awarded = null;
            state = CaptureDemolitionWeaponDropState(pickup);
            return false;
        }
        if (replacement is null)
        {
            pickup.Loot.RemoveAt(itemIndex);
        }
        else
        {
            pickup.Loot[itemIndex] = replacement;
        }
        state = FinalizeDemolitionWeaponDropMutation(pickup);
        return true;
    }

    private void OnDemolitionWeaponPickupRequested(
        long peerId,
        int round,
        int dropId,
        int expectedRevision)
    {
        if (!_squadNetwork.IsHost
            || !_demolitionMode
            || !_demolitionRoundActive
            || round != _demolitionMatch.CurrentRound
            || !_demolitionWeaponDropsById.TryGetValue(dropId, out var pickup)
            || !DemolitionWeaponDropNetworkRules.MatchesCurrentRevision(
                pickup,
                round,
                dropId,
                expectedRevision)
            || !_squadNetwork.TryGetDemolitionAssignment(
                peerId,
                out var team,
                out var slot,
                out _))
        {
            SendRejectedDemolitionWeaponPickup(peerId, round, dropId, expectedRevision);
            return;
        }

        var actor = DemolitionActorForId(DemolitionActorId(team, slot));
        if (!IsDemolitionWeaponPickupActorAlive(actor)
            || actor!.GlobalPosition.DistanceSquaredTo(pickup.GlobalPosition)
                > DemolitionWeaponPickupRange * DemolitionWeaponPickupRange
            || Mathf.Abs(actor.GlobalPosition.Y - pickup.GlobalPosition.Y) > 2.2f
            || !HasClearDemolitionWeaponDropLineOfSight(actor, pickup)
            || !_demolitionRemotePurchasedWeapons.TryGetValue(peerId, out var slots))
        {
            SendRejectedDemolitionWeaponPickup(peerId, round, dropId, expectedRevision);
            return;
        }

        var itemIndex = pickup.Loot.FindIndex(
            item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
        if (itemIndex < 0)
        {
            SendRejectedDemolitionWeaponPickup(peerId, round, dropId, expectedRevision);
            return;
        }
        var awarded = pickup.Loot[itemIndex];
        if (!TryEquipRemoteDemolitionWeapon(
                slots,
                actor,
                awarded,
                out var targetSlot,
                out var replacement))
        {
            SendRejectedDemolitionWeaponPickup(peerId, round, dropId, expectedRevision);
            return;
        }
        if (replacement is null)
        {
            pickup.Loot.RemoveAt(itemIndex);
        }
        else
        {
            pickup.Loot[itemIndex] = replacement;
        }
        var state = FinalizeDemolitionWeaponDropMutation(pickup);
        var awardedJson = ExtractionLootNetworkCodec.SerializeItems(new[] { awarded });
        _squadNetwork.SendDemolitionWeaponPickupResult(
            peerId,
            new DemolitionWeaponPickupNetworkResult(
                round,
                dropId,
                expectedRevision,
                true,
                targetSlot,
                awardedJson,
                state));
        _squadNetwork.BroadcastDemolitionWeaponDropState(state);
    }

    private static bool TryEquipRemoteDemolitionWeapon(
        RemoteDemolitionWeaponSlots slots,
        Node3D actor,
        LootItem awarded,
        out PlayerWeaponSlot targetSlot,
        out LootItem? replacement)
    {
        targetSlot = PlayerWeaponSlot.Primary;
        replacement = null;
        if (awarded.Weapon is null)
        {
            return false;
        }
        var sidearm = WeaponCatalog.IsSidearm(awarded.Weapon.Platform);
        if (sidearm)
        {
            targetSlot = PlayerWeaponSlot.Sidearm;
        }
        else if (slots.Primary is null)
        {
            targetSlot = PlayerWeaponSlot.Primary;
        }
        else
        {
            // Demolition purchases fill Primary first. The first recovered long gun
            // therefore occupies Secondary; once both are full, subsequent ground
            // pickups replace Secondary deterministically because active-slot changes
            // are intentionally not trusted from the client.
            targetSlot = PlayerWeaponSlot.Secondary;
        }
        var previous = targetSlot switch
        {
            PlayerWeaponSlot.Primary => slots.Primary,
            PlayerWeaponSlot.Secondary => slots.Secondary,
            _ => slots.Sidearm
        };
        switch (targetSlot)
        {
            case PlayerWeaponSlot.Primary:
                slots.Primary = awarded.Weapon.Clone();
                break;
            case PlayerWeaponSlot.Secondary:
                slots.Secondary = awarded.Weapon.Clone();
                break;
            default:
                slots.Sidearm = awarded.Weapon.Clone();
                break;
        }
        slots.Carried = awarded.Weapon.Clone();
        ApplyRemoteDemolitionCarriedWeapon(actor, awarded.Weapon);
        if (previous is not null)
        {
            replacement = new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = previous.Clone(),
                Grade = LootGrade.Common,
                Quantity = 1
            };
        }
        return true;
    }

    private void SendRejectedDemolitionWeaponPickup(
        long peerId,
        int round,
        int dropId,
        int requestedRevision)
    {
        if (peerId <= 1)
        {
            return;
        }
        var state = _demolitionWeaponDropsById.TryGetValue(dropId, out var pickup)
            && IsInstanceValid(pickup)
                ? CaptureDemolitionWeaponDropState(pickup)
                : new DemolitionWeaponDropNetworkState(
                    Mathf.Max(1, _demolitionMatch.CurrentRound),
                    dropId,
                    Mathf.Max(0, requestedRevision),
                    Vector3.Zero,
                    false,
                    "[]");
        _squadNetwork.SendDemolitionWeaponPickupResult(
            peerId,
            new DemolitionWeaponPickupNetworkResult(
                round,
                dropId,
                requestedRevision,
                false,
                PlayerWeaponSlot.Primary,
                string.Empty,
                state));
    }

    private static bool IsDemolitionWeaponPickupActorAlive(Node3D? actor)
        => actor switch
        {
            SquadMate mate => !mate.IsDowned && !mate.IsBodyBag,
            EnemyOperator enemy => !enemy.IsDead,
            TacticalPlayer player => !player.IsDead,
            _ => false
        };

    private bool HasClearDemolitionWeaponDropLineOfSight(
        Node3D actor,
        DroppedWeaponPickup pickup)
    {
        var exclude = new Godot.Collections.Array<Rid>();
        using var excludeBacking = exclude.AsDisposable();
        if (actor is CollisionObject3D collisionActor)
        {
            exclude.Add(collisionActor.GetRid());
        }
        var from = actor.GlobalPosition + Vector3.Up * 1.25f;
        foreach (var targetHeight in new[] { 0.18f, 0.42f, 0.68f })
        {
            if (!PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    from,
                    pickup.GlobalPosition + Vector3.Up * targetHeight,
                    exclude,
                    1))
            {
                return true;
            }
        }
        return false;
    }

    private DemolitionWeaponDropNetworkState FinalizeDemolitionWeaponDropMutation(
        DroppedWeaponPickup pickup)
    {
        pickup.AdvanceRevision();
        pickup.RefreshWeaponPresentation();
        var state = CaptureDemolitionWeaponDropState(pickup);
        if (!state.Active)
        {
            _lootSources.Remove(pickup);
            _demolitionWeaponDrops.Remove(pickup);
            _demolitionWeaponDropsById.Remove(pickup.DropId);
            pickup.QueueFree();
        }
        return state;
    }

    private static DemolitionWeaponDropNetworkState CaptureDemolitionWeaponDropState(
        DroppedWeaponPickup pickup)
        => new(
            Mathf.Max(1, pickup.DemolitionRound),
            pickup.DropId,
            pickup.Revision,
            pickup.GlobalPosition,
            pickup.IsSearchable,
            ExtractionLootNetworkCodec.SerializeItems(pickup.Loot));

    private void BroadcastDemolitionWeaponDropState(DroppedWeaponPickup pickup)
    {
        if (_squadNetwork.IsOnline && _squadNetwork.IsHost)
        {
            _squadNetwork.BroadcastDemolitionWeaponDropState(
                CaptureDemolitionWeaponDropState(pickup));
        }
    }

}
