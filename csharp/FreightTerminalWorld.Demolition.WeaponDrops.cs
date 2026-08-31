using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionWeaponPickupRange = 2.55f;
    private readonly List<DroppedWeaponPickup> _demolitionWeaponDrops = new();
    private readonly HashSet<int> _demolitionWeaponDropActorIds = new();
    private int _nextDemolitionWeaponDropId;

    internal int DemolitionWeaponDropCountForDiagnostics
        => _demolitionWeaponDrops.Count(pickup => IsInstanceValid(pickup));

    private void SpawnDemolitionWeaponDrop(Node3D actor)
    {
        if (!_demolitionMode
            || IsDemolitionNetworkClient
            || _squadNetwork.IsOnline && !_squadNetwork.IsHost
            || !IsInstanceValid(actor)
            || !TryResolveDemolitionWeaponDropActorId(actor, out var actorId)
            || !_demolitionWeaponDropActorIds.Add(actorId))
        {
            return;
        }

        var weaponItem = DetachDemolitionWeaponLoot(actor);
        if (weaponItem is null)
        {
            return;
        }

        var pickup = new DroppedWeaponPickup
        {
            Name = $"DemolitionWeaponDrop_{actorId:000}"
        };
        pickup.Configure(weaponItem);
        pickup.ConfigureNetworkIdentity(
            _demolitionMatch.CurrentRound,
            AllocateDemolitionWeaponDropId(),
            revision: 0);
        AddChild(pickup);
        pickup.GlobalPosition = ResolveDemolitionWeaponDropPosition(actor);
        pickup.RotationDegrees = new Vector3(0.0f, actor.RotationDegrees.Y + 18.0f, 0.0f);
        _demolitionWeaponDrops.Add(pickup);
        _demolitionWeaponDropsById[pickup.DropId] = pickup;
        _lootSources.Add(pickup);
        BroadcastDemolitionWeaponDropState(pickup);
    }

    private int AllocateDemolitionWeaponDropId()
        => _nextDemolitionWeaponDropId++;

    private bool TryResolveDemolitionWeaponDropActorId(Node3D actor, out int actorId)
    {
        actorId = actor switch
        {
            EnemyOperator enemy when enemy.IsDead && _demolitionOpponents.Contains(enemy)
                => enemy.NetworkId,
            SquadMate mate when mate.IsDowned && mate.ReviveUsed && _squadMates.Contains(mate)
                => DemolitionActorId(_demolitionLocalNetworkTeam, mate.SquadSlot),
            TacticalPlayer player when ReferenceEquals(player, _player)
                && player.IsDead
                && player.ReviveUsed
                => LocalDemolitionActorId,
            _ => -1
        };
        return actorId >= DemolitionAlphaActorBase;
    }

    private static LootItem? DetachDemolitionWeaponLoot(Node3D actor)
        => actor switch
        {
            EnemyOperator enemy => DetachDemolitionEnemyWeaponLoot(enemy),
            SquadMate mate => DetachDemolitionSquadWeaponLoot(mate),
            TacticalPlayer player => player.DetachDemolitionDropWeapon(),
            _ => null
        };

    private static LootItem? DetachDemolitionEnemyWeaponLoot(EnemyOperator enemy)
    {
        if (!enemy.HasFireablePrimary)
        {
            return null;
        }

        LootItem? previousWeaponItem = null;
        for (var index = enemy.Loot.Count - 1; index >= 0; index--)
        {
            if (enemy.Loot[index].Kind != LootItemKind.Weapon)
            {
                continue;
            }
            previousWeaponItem ??= enemy.Loot[index];
            enemy.Loot.RemoveAt(index);
        }
        var weaponItem = new LootItem
        {
            Id = previousWeaponItem?.Id ?? Guid.NewGuid().ToString("N"),
            Kind = LootItemKind.Weapon,
            Weapon = enemy.CarriedWeapon.Clone(),
            Grade = previousWeaponItem?.Grade ?? LootGrade.Common,
            Quantity = 1
        };
        enemy.ApplyColdStartUnarmed();
        return weaponItem;
    }

    private static LootItem? DetachDemolitionSquadWeaponLoot(SquadMate mate)
    {
        if (!mate.HasFireablePrimary)
        {
            return null;
        }
        var weaponItem = new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = mate.CarriedWeapon.Clone(),
            Grade = mate.CarriedWeaponGrade,
            Quantity = 1
        };
        mate.ApplyColdStartUnarmed();
        return weaponItem;
    }

    private Vector3 ResolveDemolitionWeaponDropPosition(Node3D actor)
    {
        var origin = actor.GlobalPosition + Vector3.Up * 1.4f;
        var excludedRid = actor is CollisionObject3D collisionActor
            ? collisionActor.GetRid()
            : default;
        if (PhysicsRaycast.TryHit(
                GetWorld3D(),
                origin,
                origin + Vector3.Down * 4.0f,
                excludedRid,
                1,
                out var hit))
        {
            return hit.Position + Vector3.Up * 0.025f;
        }
        return actor.GlobalPosition + Vector3.Up * 0.025f;
    }

    private bool TryUpdateDemolitionWeaponDropInteraction()
    {
        DroppedWeaponPickup? nearest = null;
        var nearestDistanceSquared = DemolitionWeaponPickupRange * DemolitionWeaponPickupRange;
        for (var index = _demolitionWeaponDrops.Count - 1; index >= 0; index--)
        {
            var pickup = _demolitionWeaponDrops[index];
            if (!IsInstanceValid(pickup) || pickup.IsQueuedForDeletion())
            {
                _demolitionWeaponDrops.RemoveAt(index);
                _lootSources.Remove(pickup);
                continue;
            }
            if (!pickup.IsSearchable
                || Mathf.Abs(pickup.GlobalPosition.Y - _player.GlobalPosition.Y) > 2.2f)
            {
                continue;
            }
            var distanceSquared = pickup.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }
            nearestDistanceSquared = distanceSquared;
            nearest = pickup;
        }

        if (nearest is null || !HasClearPlayerLootInteractionLineOfSight(nearest))
        {
            return false;
        }

        _hud.SetInteraction(
            $"{GameLocalization.Get("pick_up", _languageSetting, "PICK UP")}  //  {nearest.DisplayName(_languageSetting)}  //  F",
            -1.0f,
            true);
        if (_interactReleaseRequired || !Input.IsActionJustPressed(GameInputActions.Interact))
        {
            return true;
        }

        _interactReleaseRequired = true;
        if (IsDemolitionNetworkClient)
        {
            RequestDemolitionWeaponDropPickup(nearest);
            return true;
        }
        TryEquipLocalPlayerFromDemolitionWeaponDrop(nearest);
        return true;
    }

    private void RefreshDroppedWeaponPickupPresentation(ILootSource source)
    {
        if (source is DroppedWeaponPickup droppedWeapon)
        {
            droppedWeapon.AdvanceRevision();
            droppedWeapon.RefreshWeaponPresentation();
            BroadcastDemolitionWeaponDropState(droppedWeapon);
        }
    }

    private void RetireEmptyDroppedWeaponPickup(ILootSource? source)
    {
        if (source is not DroppedWeaponPickup droppedWeapon
            || droppedWeapon.Loot.Count > 0
            || !IsInstanceValid(droppedWeapon)
            || droppedWeapon.IsQueuedForDeletion())
        {
            return;
        }
        _lootSources.Remove(droppedWeapon);
        _demolitionWeaponDrops.Remove(droppedWeapon);
        _demolitionWeaponDropsById.Remove(droppedWeapon.DropId);
        droppedWeapon.QueueFree();
    }

    private void ClearDemolitionWeaponDrops()
    {
        foreach (var pickup in _demolitionWeaponDrops)
        {
            _lootSources.Remove(pickup);
            if (IsInstanceValid(pickup))
            {
                pickup.QueueFree();
            }
        }
        _demolitionWeaponDrops.Clear();
        _demolitionWeaponDropsById.Clear();
        _demolitionWeaponDropActorIds.Clear();
        _nextDemolitionWeaponDropId = 0;
        _demolitionRemotePurchasedWeapons.Clear();
        _pendingDemolitionWeaponPickupDropId = -1;
        _pendingDemolitionWeaponPickupRevision = -1;
    }
}
