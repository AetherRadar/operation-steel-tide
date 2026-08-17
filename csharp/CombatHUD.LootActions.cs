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
        var scene = GD.Load<PackedScene>(LootItemActionMenuView.ScenePath);
        _lootItemActionMenu = scene.Instantiate<LootItemActionMenuView>();
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
            var action = LootInteractionPolicy.ResolveSourceActivation(
                item.Kind,
                _shownPlayer?.HasFireablePrimary ?? true);
            EmitSignal(
                action == LootSourceActivationAction.EquipPrimaryWeapon
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
