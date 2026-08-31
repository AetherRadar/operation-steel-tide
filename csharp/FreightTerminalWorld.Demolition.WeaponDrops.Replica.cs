using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private void OnDemolitionWeaponDropState(
        DemolitionWeaponDropNetworkState state)
    {
        if (!IsDemolitionNetworkClient
            || state.Round != _demolitionMatch.CurrentRound
            || !DemolitionWeaponDropNetworkRules.IsStatePayloadValid(state))
        {
            return;
        }
        _demolitionWeaponDropsById.TryGetValue(state.DropId, out var existing);
        if (!DemolitionWeaponDropNetworkRules.IsNewerThanLocal(state, existing))
        {
            return;
        }
        if (!state.Active)
        {
            RemoveDemolitionWeaponDropReplica(existing);
            return;
        }
        var items = ExtractionLootNetworkCodec.DeserializeItems(state.ItemsJson);
        var weaponItem = items.SingleOrDefault(
            item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
        if (weaponItem is null || items.Count != 1)
        {
            return;
        }
        var pickup = existing;
        if (!IsInstanceValid(pickup))
        {
            pickup = new DroppedWeaponPickup
            {
                Name = $"DemolitionWeaponDropReplica_{state.DropId:00}"
            };
            pickup.Configure(weaponItem);
            pickup.ConfigureNetworkIdentity(state.Round, state.DropId, state.Revision);
            AddChild(pickup);
            _demolitionWeaponDrops.Add(pickup);
            _demolitionWeaponDropsById[state.DropId] = pickup;
            _lootSources.Add(pickup);
        }
        else
        {
            pickup!.Configure(weaponItem);
            pickup.ConfigureNetworkIdentity(state.Round, state.DropId, state.Revision);
        }
        pickup!.GlobalPosition = state.Position;
    }

    private void OnDemolitionWeaponPickupResult(DemolitionWeaponPickupNetworkResult result)
    {
        if (!IsDemolitionNetworkClient
            || result.Round != _demolitionMatch.CurrentRound
            || result.DropId != _pendingDemolitionWeaponPickupDropId
            || result.RequestedRevision != _pendingDemolitionWeaponPickupRevision)
        {
            return;
        }
        _pendingDemolitionWeaponPickupDropId = -1;
        _pendingDemolitionWeaponPickupRevision = -1;
        if (result.Approved)
        {
            var items = ExtractionLootNetworkCodec.DeserializeItems(result.AwardedItemJson);
            var awarded = items.SingleOrDefault(
                item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
            if (awarded is not null && items.Count == 1)
            {
                _player.EquipFromLootToWeaponSlot(awarded, result.TargetSlot);
            }
        }
        OnDemolitionWeaponDropState(result.State);
    }

    private void RemoveDemolitionWeaponDropReplica(DroppedWeaponPickup? pickup)
    {
        if (!IsInstanceValid(pickup))
        {
            return;
        }
        _lootSources.Remove(pickup!);
        _demolitionWeaponDrops.Remove(pickup!);
        _demolitionWeaponDropsById.Remove(pickup!.DropId);
        pickup.QueueFree();
    }
}
