using System;
using System.Collections.Generic;

namespace OperationSteelTide;

/// <summary>
/// Symmetric, deterministic AI buy bands. Funds represent each bot's own wallet,
/// so filling four AI slots never consumes the local human player's economy.
/// </summary>
public static class DemolitionBotLoadoutPlanner
{
    public const int SniperFundsThreshold = 4300;

    public static IReadOnlyList<WeaponBuild?> Plan(int funds, int count, int sniperSlot = 2)
    {
        var safeCount = Math.Max(0, count);
        var safeFunds = Math.Max(0, funds);
        var loadouts = new WeaponBuild?[safeCount];
        for (var index = 0; index < safeCount; index++)
        {
            loadouts[index] = BuildForSlot(safeFunds, index, sniperSlot);
        }
        return loadouts;
    }

    public static WeaponBuild? BuildForSlot(int funds, int slot, int sniperSlot = 2)
    {
        if (funds >= SniperFundsThreshold && slot == sniperSlot)
        {
            return WeaponCatalog.Build(WeaponPlatform.M24, 0);
        }
        if (funds >= 3600)
        {
            return WeaponCatalog.Build(WeaponPlatform.ScarL, 0);
        }
        if (funds >= 3100)
        {
            return WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
        }
        if (funds >= 2900)
        {
            return WeaponCatalog.Build(WeaponPlatform.AK74, 0);
        }
        if (funds >= 1700)
        {
            return WeaponCatalog.Build(WeaponPlatform.MP5A5, 0);
        }
        if (funds >= 500)
        {
            return WeaponCatalog.Build(WeaponPlatform.P226, 0);
        }
        return null;
    }
}
