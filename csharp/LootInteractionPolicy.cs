namespace OperationSteelTide;

/// <summary>Action produced when a player activates an item in a loot source.</summary>
public enum LootSourceActivationAction
{
    MoveToBackpack,
    EquipPrimaryWeapon
}

/// <summary>Actions that the backpack item menu may expose.</summary>
public readonly record struct LootBackpackMenuCapabilities(bool CanEquip, bool CanDrop);

/// <summary>
/// Pure interaction rules for moving loot between a source, the backpack, and equipment slots.
/// </summary>
public static class LootInteractionPolicy
{
    /// <summary>
    /// Resolves a source-item click. Only a weapon may bypass the backpack, and only while the
    /// primary weapon slot is empty.
    /// </summary>
    public static LootSourceActivationAction ResolveSourceActivation(
        LootItemKind itemKind,
        bool hasPrimaryWeapon)
    {
        return itemKind == LootItemKind.Weapon && !hasPrimaryWeapon
            ? LootSourceActivationAction.EquipPrimaryWeapon
            : LootSourceActivationAction.MoveToBackpack;
    }

    /// <summary>Returns the actions shown after an item has reached the backpack.</summary>
    public static LootBackpackMenuCapabilities GetBackpackMenuCapabilities(LootItemKind itemKind)
    {
        return new LootBackpackMenuCapabilities(
            CanEquip: itemKind == LootItemKind.Weapon,
            CanDrop: true);
    }
}
