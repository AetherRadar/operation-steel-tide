namespace OperationSteelTide;

/// <summary>Action produced when a player activates an item in a loot source.</summary>
public enum LootSourceActivationAction
{
    MoveToBackpack,
    EquipWeapon,
    EquipPrimaryWeapon = EquipWeapon
}

/// <summary>Actions that the backpack item menu may expose.</summary>
public readonly record struct LootBackpackMenuCapabilities(bool CanEquip, bool CanDrop);

/// <summary>
/// Pure interaction rules for moving loot between a source, the backpack, and equipment slots.
/// </summary>
public static class LootInteractionPolicy
{
    /// <summary>
    /// Resolves a source-item click. A weapon bypasses the backpack while a compatible weapon
    /// slot is empty.
    /// </summary>
    public static LootSourceActivationAction ResolveSourceActivation(
        LootItemKind itemKind,
        bool isSidearm,
        bool hasPrimaryWeapon,
        bool hasSecondaryWeapon,
        bool hasSidearmWeapon)
    {
        var hasCompatibleEmptySlot = isSidearm
            ? !hasSidearmWeapon
            : !hasPrimaryWeapon || !hasSecondaryWeapon;
        return itemKind == LootItemKind.Weapon && hasCompatibleEmptySlot
            ? LootSourceActivationAction.EquipWeapon
            : LootSourceActivationAction.MoveToBackpack;
    }

    public static LootSourceActivationAction ResolveSourceActivation(
        LootItemKind itemKind,
        bool hasPrimaryWeapon)
    {
        return ResolveSourceActivation(
            itemKind,
            isSidearm: false,
            hasPrimaryWeapon,
            hasSecondaryWeapon: true,
            hasSidearmWeapon: true);
    }

    /// <summary>Returns the actions shown after an item has reached the backpack.</summary>
    public static LootBackpackMenuCapabilities GetBackpackMenuCapabilities(LootItemKind itemKind)
    {
        return new LootBackpackMenuCapabilities(
            CanEquip: itemKind is LootItemKind.Weapon or LootItemKind.KnifeSkin,
            CanDrop: true);
    }
}
