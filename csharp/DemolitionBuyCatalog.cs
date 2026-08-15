using System;
using System.Collections.Generic;

namespace OperationSteelTide;

public enum DemolitionBuyCategory
{
    Sidearm,
    Primary,
    Armor,
    Utility
}

public sealed record DemolitionBuyOffer(
    string Id,
    DemolitionBuyCategory Category,
    WeaponPlatform? Platform,
    int Price,
    int ReserveAmmo,
    string LocalizationKey,
    string EnglishName);

public readonly record struct DemolitionPurchaseSelection(
    string SidearmId,
    string PrimaryId,
    bool ArmorSelected,
    int GrenadeCount,
    int SmokeGrenadeCount)
{
    public static DemolitionPurchaseSelection Empty => new(string.Empty, string.Empty, false, 0, 0);
}

public readonly record struct DemolitionPurchaseQuote(
    DemolitionPurchaseSelection Selection,
    int TotalCost,
    int RemainingFunds,
    bool Affordable)
{
    public bool HasFirearm
        => !string.IsNullOrEmpty(Selection.SidearmId)
        || !string.IsNullOrEmpty(Selection.PrimaryId);
}

public readonly record struct DemolitionBuySnapshot(
    int Round,
    int PlayerScore,
    int OpponentScore,
    DemolitionTeam PlayerSide,
    int Funds,
    float SecondsRemaining,
    float Duration,
    bool IsOvertime);

/// <summary>
/// Pure competitive-buy rules. This catalog has no dependency on Godot UI, world state,
/// or the extraction profile wallet.
/// </summary>
public static class DemolitionBuyCatalog
{
    public const string P226Id = "p226";
    public const string M1911Id = "m1911";
    public const string Mp5Id = "mp5a5";
    public const string Ak74Id = "ak74";
    public const string M4A1Id = "m4a1";
    public const string ScarLId = "scarl";
    public const int ArmorPrice = 1000;
    public const int GrenadePrice = 450;
    public const int SmokeGrenadePrice = 300;
    public const int MaximumGrenades = 2;
    public const int MaximumSmokeGrenades = 2;

    public static readonly IReadOnlyList<DemolitionBuyOffer> Sidearms = new[]
    {
        new DemolitionBuyOffer(P226Id, DemolitionBuyCategory.Sidearm, WeaponPlatform.P226,
            500, 45, "weapon_p226", "P226 SERVICE PISTOL"),
        new DemolitionBuyOffer(M1911Id, DemolitionBuyCategory.Sidearm, WeaponPlatform.M1911,
            700, 32, "weapon_m1911", "M1911 TACTICAL")
    };

    public static readonly IReadOnlyList<DemolitionBuyOffer> Primaries = new[]
    {
        new DemolitionBuyOffer(Mp5Id, DemolitionBuyCategory.Primary, WeaponPlatform.MP5A5,
            1700, 120, "weapon_mp5a5", "MP5A5"),
        new DemolitionBuyOffer(Ak74Id, DemolitionBuyCategory.Primary, WeaponPlatform.AK74,
            2900, 90, "demolition_buy_ak74", "AK-74N"),
        new DemolitionBuyOffer(M4A1Id, DemolitionBuyCategory.Primary, WeaponPlatform.M4A1,
            3100, 90, "demolition_buy_m4a1", "M4A1"),
        new DemolitionBuyOffer(ScarLId, DemolitionBuyCategory.Primary, WeaponPlatform.ScarL,
            3600, 80, "demolition_buy_scarl", "SCAR-L")
    };

    public static DemolitionPurchaseSelection Normalize(DemolitionPurchaseSelection selection)
    {
        var sidearm = Find(Sidearms, selection.SidearmId)?.Id ?? string.Empty;
        var primary = Find(Primaries, selection.PrimaryId)?.Id ?? string.Empty;
        return new DemolitionPurchaseSelection(
            sidearm,
            primary,
            selection.ArmorSelected,
            Math.Clamp(selection.GrenadeCount, 0, MaximumGrenades),
            Math.Clamp(selection.SmokeGrenadeCount, 0, MaximumSmokeGrenades));
    }

    public static DemolitionPurchaseQuote Quote(DemolitionPurchaseSelection selection, int funds)
    {
        var normalized = Normalize(selection);
        var total = (Find(Sidearms, normalized.SidearmId)?.Price ?? 0)
            + (Find(Primaries, normalized.PrimaryId)?.Price ?? 0)
            + (normalized.ArmorSelected ? ArmorPrice : 0)
            + normalized.GrenadeCount * GrenadePrice
            + normalized.SmokeGrenadeCount * SmokeGrenadePrice;
        var available = Math.Max(0, funds);
        return new DemolitionPurchaseQuote(
            normalized,
            total,
            available - total,
            total <= available);
    }

    public static DeploymentLoadout BuildLoadout(DemolitionPurchaseQuote quote)
    {
        if (!quote.Affordable)
        {
            throw new InvalidOperationException("Cannot build an unaffordable demolition loadout.");
        }

        var sidearm = Find(Sidearms, quote.Selection.SidearmId);
        var primary = Find(Primaries, quote.Selection.PrimaryId);
        var primaryPlatform = primary?.Platform;
        var sidearmPlatform = sidearm?.Platform;
        return new DeploymentLoadout(
            new DeploymentLoadoutSelection(
                primary?.Id ?? "knife",
                quote.Selection.ArmorSelected ? "competitive" : "none",
                LootGrade.Common,
                primary?.ReserveAmmo ?? 0),
            primaryPlatform.HasValue ? WeaponCatalog.Build(primaryPlatform.Value, 0) : null,
            quote.Selection.ArmorSelected ? "helmet_light" : "helmet_none",
            quote.Selection.ArmorSelected ? "armor_carrier" : "armor_none",
            "pack_sling",
            LootGrade.Common,
            primary?.ReserveAmmo ?? 0,
            quote.TotalCost,
            0,
            sidearmPlatform.HasValue ? WeaponCatalog.Build(sidearmPlatform.Value, 0) : null,
            sidearm?.ReserveAmmo ?? 0);
    }

    public static DemolitionBuyOffer? Sidearm(string id) => Find(Sidearms, id);

    public static DemolitionBuyOffer? Primary(string id) => Find(Primaries, id);

    private static DemolitionBuyOffer? Find(IReadOnlyList<DemolitionBuyOffer> offers, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }
        for (var index = 0; index < offers.Count; index++)
        {
            if (string.Equals(offers[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return offers[index];
            }
        }
        return null;
    }
}
