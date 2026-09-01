using System;

namespace OperationSteelTide;

public readonly record struct DemolitionBotUtilityInventory(
    int FragmentationGrenades,
    int SmokeGrenades,
    int IncendiaryGrenades,
    int FlashbangGrenades)
{
    public DemolitionBotUtilityInventory(
        int fragmentationGrenades,
        int smokeGrenades,
        int incendiaryGrenades)
        : this(fragmentationGrenades, smokeGrenades, incendiaryGrenades, 0)
    {
    }

    public static DemolitionBotUtilityInventory Empty => new(0, 0, 0, 0);

    public int TotalCost
        => FragmentationGrenades * DemolitionBuyCatalog.GrenadePrice
        + SmokeGrenades * DemolitionBuyCatalog.SmokeGrenadePrice
        + IncendiaryGrenades * DemolitionBuyCatalog.IncendiaryGrenadePrice
        + FlashbangGrenades * DemolitionBuyCatalog.FlashbangGrenadePrice;
}

/// <summary>
/// Pure per-slot AI utility economy. Each bot receives the same round funds, pays for
/// its own planned firearm, then selects at most one affordable contextual throwable.
/// </summary>
public static class DemolitionBotUtilityBudgetPlanner
{
    public static DemolitionBotUtilityInventory Plan(
        int round,
        int slot,
        int roundFunds,
        WeaponBuild? weapon)
    {
        var remaining = Math.Max(0, roundFunds) - WeaponPrice(weapon);
        if (remaining < DemolitionBuyCatalog.SmokeGrenadePrice)
        {
            return DemolitionBotUtilityInventory.Empty;
        }

        // Preserve the established opening-round smoke contract while introducing
        // flashbangs into the later-round per-slot rotation.
        var preferred = round <= 1
            ? 0
            : Math.Abs((slot + Math.Max(1, round)) % 4);
        if (preferred == 1 && remaining >= DemolitionBuyCatalog.IncendiaryGrenadePrice)
        {
            return new DemolitionBotUtilityInventory(0, 0, 1);
        }
        if (preferred == 2 && remaining >= DemolitionBuyCatalog.GrenadePrice)
        {
            return new DemolitionBotUtilityInventory(1, 0, 0);
        }
        if (preferred == 3 && remaining >= DemolitionBuyCatalog.FlashbangGrenadePrice)
        {
            return new DemolitionBotUtilityInventory(0, 0, 0, 1);
        }
        return new DemolitionBotUtilityInventory(0, 1, 0);
    }

    public static int WeaponPrice(WeaponBuild? weapon)
        => weapon?.Platform switch
        {
            WeaponPlatform.P226 => DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.P226Id)?.Price ?? 500,
            WeaponPlatform.GSh18 => DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.Gsh18Id)?.Price ?? 650,
            WeaponPlatform.M1911 => DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.M1911Id)?.Price ?? 700,
            WeaponPlatform.MP5A5 => DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.Mp5Id)?.Price ?? 1700,
            WeaponPlatform.AK74 => DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.Ak74Id)?.Price ?? 2900,
            WeaponPlatform.M4A1 => DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.M4A1Id)?.Price ?? 3100,
            WeaponPlatform.ScarL => DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.ScarLId)?.Price ?? 3600,
            WeaponPlatform.M24 => DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.M24Id)?.Price ?? 4300,
            null => 0,
            _ => int.MaxValue
        };
}
