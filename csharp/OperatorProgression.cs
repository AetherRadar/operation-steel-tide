using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace OperationSteelTide;

public sealed record DeploymentLoadoutSelection(string WeaponId, string ArmorId, LootGrade AmmoGrade);

public sealed record DeploymentWeaponOffer(
    string Id,
    WeaponPlatform? Platform,
    int BuildTier,
    int Price,
    int ReserveAmmo,
    string LocalizationKey,
    string EnglishName);

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
    string EnglishName);

public sealed record DeploymentLoadout(
    DeploymentLoadoutSelection Selection,
    WeaponBuild? Weapon,
    string HelmetId,
    string BodyArmorId,
    string BackpackId,
    LootGrade AmmoGrade,
    int ReserveAmmo,
    int TotalCost);

public sealed class OperatorProfileData
{
    public int Version { get; set; } = 1;
    public int Credits { get; set; } = OperatorProfileStore.StartingCredits;
    public int LifetimeExtractedValue { get; set; }
    public int SuccessfulExtractions { get; set; }
    public int DeploymentCount { get; set; }
    public string LastWeaponId { get; set; } = "m4a1";
    public string LastArmorId { get; set; } = "standard";
    public LootGrade LastAmmoGrade { get; set; } = LootGrade.Uncommon;

    public OperatorProfileData Clone() => new()
    {
        Version = Version,
        Credits = Credits,
        LifetimeExtractedValue = LifetimeExtractedValue,
        SuccessfulExtractions = SuccessfulExtractions,
        DeploymentCount = DeploymentCount,
        LastWeaponId = LastWeaponId,
        LastArmorId = LastArmorId,
        LastAmmoGrade = LastAmmoGrade
    };
}

public static class DeploymentCatalog
{
    public static readonly IReadOnlyList<DeploymentWeaponOffer> Weapons = new[]
    {
        new DeploymentWeaponOffer("none", null, 0, 0, 0, "loadout_scavenger", "SCAVENGER / KNIFE ONLY"),
        new DeploymentWeaponOffer("m4a1", WeaponPlatform.M4A1, 1, 4200, 90, "loadout_m4a1", "M4A1 ASSAULT"),
        new DeploymentWeaponOffer("mp5a5", WeaponPlatform.MP5A5, 1, 3600, 150, "loadout_mp5", "MP5A5 CQB"),
        new DeploymentWeaponOffer("m24", WeaponPlatform.M24, 2, 7800, 30, "loadout_m24", "M24 PRECISION")
    };

    public static readonly IReadOnlyList<DeploymentArmorOffer> Armor = new[]
    {
        new DeploymentArmorOffer(
            "standard",
            "helmet_light",
            "armor_carrier",
            "pack_assault",
            0,
            "loadout_standard_armor",
            "STANDARD FIELD KIT"),
        new DeploymentArmorOffer(
            "heavy",
            "helmet_heavy",
            "armor_heavy",
            "pack_heavy",
            3800,
            "loadout_heavy_armor",
            "HEAVY ASSAULT KIT")
    };

    public static readonly IReadOnlyList<DeploymentPresetOffer> Presets = new[]
    {
        new DeploymentPresetOffer("scavenger", "none", "standard", LootGrade.Common, "preset_scavenger", "SCAVENGER"),
        new DeploymentPresetOffer("assault", "m4a1", "standard", LootGrade.Uncommon, "preset_assault", "ASSAULT"),
        new DeploymentPresetOffer("breacher", "mp5a5", "heavy", LootGrade.Rare, "preset_breacher", "BREACHER"),
        new DeploymentPresetOffer("overwatch", "m24", "standard", LootGrade.Epic, "preset_overwatch", "OVERWATCH")
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

    public static int AmmoPrice(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => 900,
        LootGrade.Rare => 1800,
        LootGrade.Epic => 3300,
        LootGrade.Legendary => 5600,
        _ => 450
    };

    public static DeploymentLoadout Resolve(DeploymentLoadoutSelection selection)
    {
        var weapon = Weapon(selection.WeaponId);
        var armor = ArmorKit(selection.ArmorId);
        var ammoCost = weapon.Platform is null ? 0 : AmmoPrice(selection.AmmoGrade);
        return new DeploymentLoadout(
            new DeploymentLoadoutSelection(weapon.Id, armor.Id, selection.AmmoGrade),
            weapon.Platform is null ? null : WeaponCatalog.Build(weapon.Platform.Value, weapon.BuildTier),
            armor.HelmetId,
            armor.BodyArmorId,
            armor.BackpackId,
            selection.AmmoGrade,
            weapon.ReserveAmmo,
            weapon.Price + armor.Price + ammoCost);
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

            var previous = Profile.Clone();
            Profile.Credits -= loadout.TotalCost;
            Profile.DeploymentCount++;
            Profile.LastWeaponId = loadout.Selection.WeaponId;
            Profile.LastArmorId = loadout.Selection.ArmorId;
            Profile.LastAmmoGrade = loadout.Selection.AmmoGrade;
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
            profile.LastAmmoGrade = (LootGrade)Math.Clamp((int)profile.LastAmmoGrade, 0, 4);
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
