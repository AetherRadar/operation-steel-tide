using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace OperationSteelTide;

public sealed record DeploymentLoadoutSelection(
    string WeaponId,
    string ArmorId,
    LootGrade AmmoGrade,
    int AmmoQuantity = 0);

public sealed record DeploymentWeaponOffer(
    string Id,
    WeaponPlatform? Platform,
    int BuildTier,
    int Price,
    int ReserveAmmo,
    string LocalizationKey,
    string EnglishName,
    int IncludedCommonAmmo = 0);

public sealed record DeploymentArmorOffer(
    string Id,
    string HelmetId,
    string BodyArmorId,
    string BackpackId,
    int Price,
    string LocalizationKey,
    string EnglishName);

public sealed record DeploymentPresetOffer(
    string Id,
    string WeaponId,
    string ArmorId,
    LootGrade AmmoGrade,
    string LocalizationKey,
    string EnglishName,
    int AmmoQuantity = 0);

public sealed record DeploymentAmmoPackOffer(
    int Quantity,
    string LocalizationKey,
    string EnglishName);

public sealed record DeploymentLoadout(
    DeploymentLoadoutSelection Selection,
    WeaponBuild? Weapon,
    string HelmetId,
    string BodyArmorId,
    string BackpackId,
    LootGrade AmmoGrade,
    int ReserveAmmo,
    int TotalCost,
    int WeaponBuildTier = -1,
    WeaponBuild? Sidearm = null,
    int SidearmReserveAmmo = 0,
    int ReputationLevel = 1);

public sealed class OperatorProfileData
{
    public int Version { get; set; } = 1;
    public int Credits { get; set; } = OperatorProfileStore.StartingCredits;
    public int LifetimeExtractedValue { get; set; }
    public int SuccessfulExtractions { get; set; }
    public int DeploymentCount { get; set; }
    public int ReputationPoints { get; set; }
    public string LastWeaponId { get; set; } = "m3a1";
    public string LastArmorId { get; set; } = "patrol";
    public LootGrade LastAmmoGrade { get; set; } = LootGrade.Common;
    public int LastAmmoQuantity { get; set; } = 60;

    public OperatorProfileData Clone() => new()
    {
        Version = Version,
        Credits = Credits,
        LifetimeExtractedValue = LifetimeExtractedValue,
        SuccessfulExtractions = SuccessfulExtractions,
        DeploymentCount = DeploymentCount,
        ReputationPoints = ReputationPoints,
        LastWeaponId = LastWeaponId,
        LastArmorId = LastArmorId,
        LastAmmoGrade = LastAmmoGrade,
        LastAmmoQuantity = LastAmmoQuantity
    };
}

/// <summary>
/// Reputation levels convert extracted loot value into a long-term progression curve.
/// Higher levels grant starting perks; deployment access requirements are owned by
/// <see cref="DeploymentAccessPolicy"/> and may be disabled independently.
/// </summary>
public static class OperatorReputation
{
    public const int MaxLevel = 12;
    public const int SmokeGrenadePerkLevel = 3;
    public const int ReserveAmmoPerkLevel = 5;
    public const int ArmorPlatePerkLevel = 7;
    public const int ReserveAmmoBonus = 30;

    public static int PointsForLevel(int level)
        => 4000 * Math.Max(0, level - 1) * Math.Max(0, level - 1);

    public static int LevelForPoints(int points)
    {
        var level = 1;
        while (level < MaxLevel && points >= PointsForLevel(level + 1))
        {
            level++;
        }
        return level;
    }

    public static int RequiredLevelForWeapon(string weaponId) => weaponId switch
    {
        "scarl" => 2,
        "m24" => 3,
        _ => 1
    };

    public static int RequiredLevelForArmor(string armorId) => armorId switch
    {
        "heavy" => 3,
        "nvg" => 2,
        _ => 1
    };

    public static int RequiredLevelForAmmoGrade(LootGrade grade) => grade switch
    {
        LootGrade.Rare => 2,
        LootGrade.Epic => 4,
        LootGrade.Legendary => 6,
        _ => 1
    };

    public static int RequiredLevelForMap(string mapId)
    {
        if (string.Equals(mapId, DeploymentMapCatalog.OrbitalComplexId, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return string.Equals(mapId, DeploymentMapCatalog.BlackwaterRefineryId, StringComparison.OrdinalIgnoreCase)
            ? 2
            : 1;
    }

    public static int RequiredLevelForPreset(DeploymentPresetOffer preset)
    {
        var level = Math.Max(
            RequiredLevelForWeapon(preset.WeaponId),
            RequiredLevelForArmor(preset.ArmorId));
        return Math.Max(level, RequiredLevelForAmmoGrade(preset.AmmoGrade));
    }
}

public static class DeploymentCatalog
{
    public static readonly IReadOnlyList<DeploymentWeaponOffer> Weapons = new[]
    {
        new DeploymentWeaponOffer("none", null, 0, 0, 0, "loadout_scavenger", "SCAVENGER / KNIFE ONLY"),
        new DeploymentWeaponOffer("m3a1", WeaponPlatform.M3A1, 0, 0, 60, "loadout_m3a1", "SMG-45 RECRUIT", 60),
        new DeploymentWeaponOffer("m4a1", WeaponPlatform.M4A1, 1, 4200, 90, "loadout_m4a1", "M4A1 ASSAULT"),
        new DeploymentWeaponOffer("ak74", WeaponPlatform.AK74, 1, 3900, 90, "loadout_ak74", "AK-47 ASSAULT"),
        new DeploymentWeaponOffer("scarl", WeaponPlatform.ScarL, 2, 6200, 60, "loadout_scarl", "SCAR-L SPECIALIST"),
        new DeploymentWeaponOffer("mp5a5", WeaponPlatform.MP5A5, 1, 3600, 120, "loadout_mp5", "MP5A5 CQB"),
        new DeploymentWeaponOffer("m24", WeaponPlatform.M24, 2, 7800, 60, "loadout_m24", "M24 PRECISION")
    };

    public static readonly IReadOnlyList<DeploymentAmmoPackOffer> AmmoPacks = new[]
    {
        new DeploymentAmmoPackOffer(30, "ammo_pack_30", "30 ROUNDS"),
        new DeploymentAmmoPackOffer(60, "ammo_pack_60", "60 ROUNDS"),
        new DeploymentAmmoPackOffer(90, "ammo_pack_90", "90 ROUNDS"),
        new DeploymentAmmoPackOffer(180, "ammo_pack_180", "180 ROUNDS")
    };

    public static readonly IReadOnlyList<DeploymentArmorOffer> Armor = new[]
    {
        new DeploymentArmorOffer(
            "patrol",
            "helmet_patrol",
            "armor_patrol",
            "pack_sling",
            0,
            "loadout_patrol_armor",
            "PATROL STARTER KIT"),
        new DeploymentArmorOffer(
            "standard",
            "helmet_light",
            "armor_carrier",
            "pack_assault",
            1600,
            "loadout_standard_armor",
            "STANDARD FIELD KIT"),
        new DeploymentArmorOffer(
            "heavy",
            "helmet_heavy",
            "armor_heavy",
            "pack_heavy",
            3800,
            "loadout_heavy_armor",
            "HEAVY ASSAULT KIT"),
        new DeploymentArmorOffer(
            "nvg",
            "helmet_nvg",
            "armor_carrier",
            "pack_assault",
            2600,
            "loadout_nvg_armor",
            "NIGHT OPS KIT")
    };

    public static readonly IReadOnlyList<DeploymentPresetOffer> Presets = new[]
    {
        new DeploymentPresetOffer("scavenger", "none", "patrol", LootGrade.Common, "preset_scavenger", "SCAVENGER", 0),
        new DeploymentPresetOffer("recruit", "m3a1", "patrol", LootGrade.Common, "preset_recruit", "RECRUIT", 60),
        new DeploymentPresetOffer("assault", "m4a1", "standard", LootGrade.Uncommon, "preset_assault", "ASSAULT", 90),
        new DeploymentPresetOffer("breacher", "mp5a5", "heavy", LootGrade.Rare, "preset_breacher", "BREACHER", 120),
        new DeploymentPresetOffer("overwatch", "m24", "standard", LootGrade.Epic, "preset_overwatch", "OVERWATCH", 60)
    };

    public static DeploymentWeaponOffer Weapon(string id)
    {
        foreach (var offer in Weapons)
        {
            if (string.Equals(offer.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return offer;
            }
        }
        return Weapons[0];
    }

    public static DeploymentArmorOffer ArmorKit(string id)
    {
        foreach (var offer in Armor)
        {
            if (string.Equals(offer.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return offer;
            }
        }
        return Armor[0];
    }

    public static DeploymentPresetOffer Preset(string id)
    {
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }
        return Presets[0];
    }

    public static DeploymentAmmoPackOffer AmmoPack(int quantity)
    {
        foreach (var pack in AmmoPacks)
        {
            if (pack.Quantity == quantity)
            {
                return pack;
            }
        }
        return AmmoPacks[0];
    }

    public static int NormalizeAmmoQuantity(int quantity, int fallback)
    {
        if (quantity > 0)
        {
            foreach (var pack in AmmoPacks)
            {
                if (pack.Quantity == quantity)
                {
                    return quantity;
                }
            }
        }
        foreach (var pack in AmmoPacks)
        {
            if (pack.Quantity == fallback)
            {
                return fallback;
            }
        }
        return AmmoPacks[0].Quantity;
    }

    public static int AmmoPrice(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => 900,
        LootGrade.Rare => 1800,
        LootGrade.Epic => 3300,
        LootGrade.Legendary => 5600,
        _ => 450
    };

    public static int AmmoPrice(LootGrade grade, AmmoCaliber caliber, int quantity)
    {
        if (quantity <= 0)
        {
            return 0;
        }
        var caliberMultiplier = caliber switch
        {
            AmmoCaliber.Magnum338 => 2.1f,
            AmmoCaliber.Sniper => 1.35f,
            AmmoCaliber.Smg => 0.8f,
            AmmoCaliber.Pistol => 0.9f,
            _ => 1.0f
        };
        var rawPrice = AmmoPrice(grade) * quantity / 90.0f * caliberMultiplier;
        return Math.Max(50, (int)(Math.Round(rawPrice / 50.0f, MidpointRounding.AwayFromZero) * 50));
    }

    public static int AmmoCost(DeploymentWeaponOffer weapon, LootGrade grade, int quantity)
    {
        if (weapon.Platform is null || quantity <= 0)
        {
            return 0;
        }
        var billableQuantity = grade == LootGrade.Common
            ? Math.Max(0, quantity - weapon.IncludedCommonAmmo)
            : quantity;
        return AmmoPrice(grade, WeaponCatalog.Weapon(weapon.Platform.Value).Caliber, billableQuantity);
    }

    public static DeploymentLoadout Resolve(DeploymentLoadoutSelection selection)
    {
        var weapon = Weapon(selection.WeaponId);
        var armor = ArmorKit(selection.ArmorId);
        var quantity = weapon.Platform is null
            ? 0
            : NormalizeAmmoQuantity(selection.AmmoQuantity, weapon.ReserveAmmo);
        var ammoCost = AmmoCost(weapon, selection.AmmoGrade, quantity);
        return new DeploymentLoadout(
            new DeploymentLoadoutSelection(weapon.Id, armor.Id, selection.AmmoGrade, quantity),
            weapon.Platform is null ? null : WeaponCatalog.Build(weapon.Platform.Value, weapon.BuildTier),
            armor.HelmetId,
            armor.BodyArmorId,
            armor.BackpackId,
            selection.AmmoGrade,
            quantity,
            weapon.Price + armor.Price + ammoCost,
            weapon.BuildTier);
    }
}

/// <summary>Deploy-time threat level: harder garrisons for richer extraction payouts.</summary>
public enum DeploymentThreatLevel
{
    Standard = 0,
    Elevated = 1,
    Maximum = 2
}

public static class ThreatLevels
{
    public static float DetectionMultiplier(DeploymentThreatLevel level)
        => 1.0f + 0.16f * (int)level;

    public static float AccuracyBonus(DeploymentThreatLevel level)
        => 0.05f * (int)level;

    /// <summary>Reinforcement threat accrues sooner; applied as a threshold reduction.</summary>
    public static int ReinforcementThresholdShift(DeploymentThreatLevel level)
        => -8 * (int)level;

    public static float PayoutMultiplier(DeploymentThreatLevel level)
        => 1.0f + 0.2f * (int)level;

    public static int RequiredReputationLevel(DeploymentThreatLevel level)
        => level switch
        {
            DeploymentThreatLevel.Elevated => 2,
            DeploymentThreatLevel.Maximum => 4,
            _ => 1
        };

    public static string DisplayName(DeploymentThreatLevel level, string language)
    {
        var chinese = GameLocalization.IsChinese(language);
        return level switch
        {
            DeploymentThreatLevel.Elevated => chinese ? "\u5347\u7ea7\u5a01\u80c1" : "ELEVATED",
            DeploymentThreatLevel.Maximum => chinese ? "\u6700\u9ad8\u5a01\u80c1" : "MAXIMUM",
            _ => chinese ? "\u6807\u51c6\u5a01\u80c1" : "STANDARD"
        };
    }
}

public sealed class OperatorProfileStore
{
    public const int StartingCredits = 18000;
    private const string DefaultFileName = "operator_profile.json";
    private readonly object _sync = new();
    private readonly string _path;

    public OperatorProfileData Profile { get; private set; }

    public OperatorProfileStore(string? path = null)
    {
        _path = path ?? ProjectSettings.GlobalizePath($"user://{DefaultFileName}");
        Profile = LoadProfile();
    }

    public bool TryCommitDeployment(
        DeploymentLoadoutSelection selection,
        out DeploymentLoadout loadout,
        out string failure)
    {
        lock (_sync)
        {
            loadout = DeploymentCatalog.Resolve(selection);
            if (Profile.Credits < loadout.TotalCost)
            {
                failure = "insufficient_credits";
                return false;
            }
            var reputationLevel = OperatorReputation.LevelForPoints(Profile.ReputationPoints);
            var requiredLevel = Math.Max(
                OperatorReputation.RequiredLevelForWeapon(loadout.Selection.WeaponId),
                Math.Max(
                    OperatorReputation.RequiredLevelForArmor(loadout.Selection.ArmorId),
                    OperatorReputation.RequiredLevelForAmmoGrade(loadout.Selection.AmmoGrade)));
            if (DeploymentAccessPolicy.IsReputationLocked(reputationLevel, requiredLevel))
            {
                failure = "reputation_locked";
                return false;
            }
            loadout = loadout with { ReputationLevel = reputationLevel };

            var previous = Profile.Clone();
            Profile.Credits -= loadout.TotalCost;
            Profile.DeploymentCount++;
            Profile.LastWeaponId = loadout.Selection.WeaponId;
            Profile.LastArmorId = loadout.Selection.ArmorId;
            Profile.LastAmmoGrade = loadout.Selection.AmmoGrade;
            Profile.LastAmmoQuantity = loadout.Selection.AmmoQuantity;
            if (!TrySave())
            {
                Profile = previous;
                failure = "profile_save_failed";
                return false;
            }
            failure = string.Empty;
            return true;
        }
    }

    public bool CreditExtraction(int extractedValue)
    {
        lock (_sync)
        {
            var value = Math.Max(0, extractedValue);
            var previous = Profile.Clone();
            Profile.Credits += value;
            Profile.LifetimeExtractedValue += value;
            Profile.ReputationPoints += value;
            Profile.SuccessfulExtractions++;
            if (TrySave())
            {
                return true;
            }
            Profile = previous;
            return false;
        }
    }

    private OperatorProfileData LoadProfile()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new OperatorProfileData();
            }
            var json = File.ReadAllText(_path, Encoding.UTF8);
            var profile = JsonSerializer.Deserialize<OperatorProfileData>(json) ?? new OperatorProfileData();
            profile.Credits = Math.Max(0, profile.Credits);
            profile.LifetimeExtractedValue = Math.Max(0, profile.LifetimeExtractedValue);
            profile.SuccessfulExtractions = Math.Max(0, profile.SuccessfulExtractions);
            profile.DeploymentCount = Math.Max(0, profile.DeploymentCount);
            profile.ReputationPoints = Math.Max(0, profile.ReputationPoints);
            profile.LastAmmoGrade = (LootGrade)Math.Clamp((int)profile.LastAmmoGrade, 0, 4);
            profile.LastAmmoQuantity = Math.Max(0, profile.LastAmmoQuantity);
            return profile;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Operator profile load failed; using a fresh local profile: {exception.Message}");
            return new OperatorProfileData();
        }
    }

    private bool TrySave()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var temporaryPath = _path + ".tmp";
            var json = JsonSerializer.Serialize(Profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, _path, true);
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"Operator profile save failed: {exception.Message}");
            return false;
        }
    }
}
