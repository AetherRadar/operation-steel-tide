using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private LootItemActionMenuView _lootItemActionMenu = null!;

    internal bool LootActionMenuReady
        => IsInstanceValid(_lootItemActionMenu) && _lootItemActionMenu.UiReady;

    internal bool LootActionMenuVisible
        => IsInstanceValid(_lootItemActionMenu) && _lootItemActionMenu.Visible;

    internal bool LootActionMenuCanEquip
        => IsInstanceValid(_lootItemActionMenu) && _lootItemActionMenu.CanEquip;

    internal string LootActionMenuItemId
        => IsInstanceValid(_lootItemActionMenu) ? _lootItemActionMenu.ItemId : string.Empty;

    internal string LootActionMenuEquipText
        => IsInstanceValid(_lootItemActionMenu) ? _lootItemActionMenu.EquipText : string.Empty;

    internal string LootActionMenuDropText
        => IsInstanceValid(_lootItemActionMenu) ? _lootItemActionMenu.DropText : string.Empty;

    private void BuildLootItemActionMenu()
    {
        _lootItemActionMenu = HudPackedSceneCache.Instantiate<LootItemActionMenuView>(
            LootItemActionMenuView.ScenePath);
        _lootItemActionMenu.Name = "LootItemActionMenu";
        _lootItemActionMenu.EquipRequested += itemId =>
            EmitSignal(SignalName.BackpackUseRequested, itemId);
        _lootItemActionMenu.DropRequested += itemId =>
            EmitSignal(SignalName.BackpackDropRequested, itemId);
        AddChild(_lootItemActionMenu);
    }

    private void HandleLootCardActivated(LootItem item, LootDragOrigin origin, LootDragCard card)
    {
        if (origin == LootDragOrigin.Source)
        {
            var action = LootSourceActivationAction.MoveToBackpack;
            if (item.Kind == LootItemKind.Weapon
                && item.Weapon is not null
                && _shownPlayer is { } player)
            {
                action = LootInteractionPolicy.ResolveSourceActivation(
                    item.Kind,
                    WeaponCatalog.IsSidearm(item.Weapon.Platform),
                    player.HasFireablePrimary,
                    player.HasSecondaryWeapon,
                    player.HasSidearmWeapon);
            }
            EmitSignal(
                action == LootSourceActivationAction.EquipWeapon
                    ? SignalName.LootEquipRequested
                    : SignalName.LootTakeRequested,
                item.Id);
            return;
        }

        var backpackItem = _shownPlayer?.Backpack.Find(candidate => candidate.Id == item.Id);
        if (backpackItem is null || !IsInstanceValid(_lootItemActionMenu))
        {
            return;
        }
        var capabilities = LootInteractionPolicy.GetBackpackMenuCapabilities(backpackItem.Kind);
        _lootItemActionMenu.OpenNear(
            card,
            backpackItem.Id,
            backpackItem.DisplayName(_language),
            _language,
            capabilities.CanEquip);
    }

    private void HandleLootCardQuickActivated(LootItem item, LootDragCard card)
    {
        var origin = card.Origin;
        if (origin == LootDragOrigin.Source)
        {
            // Source cards retain the existing one-click transfer/equip policy;
            // a double click should never bypass backpack capacity checks.
            var action = LootSourceActivationAction.MoveToBackpack;
            if (item.Kind == LootItemKind.Weapon
                && item.Weapon is not null
                && _shownPlayer is { } player)
            {
                action = LootInteractionPolicy.ResolveSourceActivation(
                    item.Kind,
                    WeaponCatalog.IsSidearm(item.Weapon.Platform),
                    player.HasFireablePrimary,
                    player.HasSecondaryWeapon,
                    player.HasSidearmWeapon);
            }
            EmitSignal(
                action == LootSourceActivationAction.EquipWeapon
                    ? SignalName.LootEquipRequested
                    : SignalName.LootTakeRequested,
                item.Id);
            return;
        }

        var backpackItem = _shownPlayer?.Backpack.Find(candidate => candidate.Id == item.Id);
        if (backpackItem is null)
        {
            return;
        }
        var capabilities = LootInteractionPolicy.GetBackpackMenuCapabilities(backpackItem.Kind);
        if (capabilities.CanEquip)
        {
            EmitSignal(SignalName.BackpackUseRequested, backpackItem.Id);
        }
        else if (backpackItem.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate)
        {
            EmitSignal(SignalName.BackpackUseRequested, backpackItem.Id);
        }
        else
        {
            // Non-equipable stacks still get the contextual menu on a double
            // click instead of silently consuming or discarding them.
            HandleLootCardActivated(item, origin, card);
        }
    }

    private void DismissLootItemActionMenu()
    {
        if (IsInstanceValid(_lootItemActionMenu) && _lootItemActionMenu.Visible)
        {
            _lootItemActionMenu.Hide();
        }
    }

    internal bool ActivateLootCardForDiagnostics(string itemId, LootDragOrigin origin)
    {
        var list = origin == LootDragOrigin.Source ? _lootSourceList : _backpackList;
        if (!IsInstanceValid(list))
        {
            return false;
        }
        var children = list.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is LootDragCard card && card.ItemId == itemId)
            {
                card.ActivateForDiagnostics();
                return true;
            }
        }
        return false;
    }

    internal void PressLootMenuEquipForDiagnostics()
        => _lootItemActionMenu.PressEquipForDiagnostics();

    internal void PressLootMenuDropForDiagnostics()
        => _lootItemActionMenu.PressDropForDiagnostics();
}
