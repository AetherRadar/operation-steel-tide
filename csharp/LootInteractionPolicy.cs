namespace OperationSteelTide;

/// <summary>Action produced when a player activates an item in a loot source.</summary>
public enum LootSourceActivationAction
{
    MoveToBackpack,
    EquipWeapon
}

/// <summary>Actions that the backpack item menu may expose.</summary>
public readonly record struct LootBackpackMenuCapabilities(bool CanEquip, bool CanDrop);

/// <summary>
/// Pure interaction rules for moving loot between a source, the backpack, and equipment slots.
/// </summary>
public static class LootInteractionPolicy
{
    /// <summary>Fill an empty compatible slot, otherwise replace only a strictly lower grade.</summary>
    public static PlayerWeaponSlot? SelectAutomaticWeaponSlot(
        bool isSidearm, LootGrade incoming, LootGrade? primary, LootGrade? secondary, LootGrade? sidearm)
    {
        if (isSidearm)
            return sidearm is null || incoming > sidearm ? PlayerWeaponSlot.Sidearm : null;
        if (primary is null) return PlayerWeaponSlot.Primary;
        if (secondary is null) return PlayerWeaponSlot.Secondary;
        var weakest = primary <= secondary ? PlayerWeaponSlot.Primary : PlayerWeaponSlot.Secondary;
        return incoming > (weakest == PlayerWeaponSlot.Primary ? primary : secondary) ? weakest : null;
    }

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

    /// <summary>Returns the actions shown after an item has reached the backpack.</summary>
    public static LootBackpackMenuCapabilities GetBackpackMenuCapabilities(LootItemKind itemKind)
    {
        return new LootBackpackMenuCapabilities(
            CanEquip: itemKind is LootItemKind.Weapon or LootItemKind.KnifeSkin,
            CanDrop: true);
    }
}
