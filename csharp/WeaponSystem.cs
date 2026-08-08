using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public enum WeaponPlatform
{
    M4A1,
    AK74,
    ScarL,
    M24,
    MP5A5
}

public enum AmmoCaliber
{
    Rifle,
    Sniper,
    Smg
}

public enum AttachmentSlot
{
    Optic,
    Barrel,
    Muzzle,
    Grip,
    Stock,
    Magazine
}

public enum LootItemKind
{
    Weapon,
    Attachment,
    Ammunition,
    ArmorPlate,
    Equipment,
    KnifeSkin,
    Medical
}

/// <summary>Extraction-style item rarity. Higher grade = higher sell value and brighter world glow.</summary>
public enum LootGrade
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

public static class LootGrades
{
    public static Color GlowColor(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => new Color(0.28f, 0.92f, 0.42f),
        LootGrade.Rare => new Color(0.28f, 0.55f, 1.0f),
        LootGrade.Epic => new Color(0.72f, 0.32f, 1.0f),
        LootGrade.Legendary => new Color(1.0f, 0.62f, 0.12f),
        _ => new Color(0.72f, 0.76f, 0.74f)
    };

    public static string DisplayName(LootGrade grade, string language)
    {
        if (GameLocalization.IsChinese(language))
        {
            return grade switch
            {
                LootGrade.Uncommon => "优良",
                LootGrade.Rare => "稀有",
                LootGrade.Epic => "史诗",
                LootGrade.Legendary => "传说",
                _ => "普通"
            };
        }
        return grade switch
        {
            LootGrade.Uncommon => "UNCOMMON",
            LootGrade.Rare => "RARE",
            LootGrade.Epic => "EPIC",
            LootGrade.Legendary => "LEGENDARY",
            _ => "COMMON"
        };
    }

    public static int BaseValue(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => 120,
        LootGrade.Rare => 320,
        LootGrade.Epic => 780,
        LootGrade.Legendary => 1600,
        _ => 40
    };

    public static LootGrade FromTier(int tier) => tier switch
    {
        >= 3 => LootGrade.Legendary,
        2 => LootGrade.Epic,
        1 => LootGrade.Rare,
        _ => LootGrade.Uncommon
    };
}

public enum EquipmentSlot
{
    Helmet,
    BodyArmor,
    Backpack
}

public sealed class EquipmentDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ChineseName { get; init; }
    public required EquipmentSlot Slot { get; init; }
    public float Protection { get; init; }
    public float MaxDurability { get; init; }
    public int CapacityBonus { get; init; }
}

public sealed class EquipmentItem
{
    public required string DefinitionId { get; init; }
    public float Durability { get; set; }

    public EquipmentDefinition Definition => EquipmentCatalog.Definition(DefinitionId);

    public EquipmentItem Clone() => new()
    {
        DefinitionId = DefinitionId,
        Durability = Durability
    };

    public string DisplayName(string language) =>
        GameLocalization.IsChinese(language) ? Definition.ChineseName : Definition.Name;

    public string Detail(string language)
    {
        var definition = Definition;
        var durability = $"{Durability:0}/{definition.MaxDurability:0}";
        if (definition.Slot == EquipmentSlot.Backpack)
        {
            return GameLocalization.IsChinese(language)
                ? $"容量 +{definition.CapacityBonus}  耐久 {durability}"
                : $"CAPACITY +{definition.CapacityBonus}  DURABILITY {durability}";
        }
        return GameLocalization.IsChinese(language)
            ? $"减伤 {definition.Protection * 100:0}%  耐久 {durability}"
            : $"PROTECTION {definition.Protection * 100:0}%  DURABILITY {durability}";
    }
}

public sealed class WeaponDefinition
{
    public required WeaponPlatform Platform { get; init; }
    public required string Name { get; init; }
    public required string ChineseName { get; init; }
    public string LocalizationKey { get; init; } = string.Empty;
    public AmmoCaliber Caliber { get; init; } = AmmoCaliber.Rifle;
    public bool SupportsAutomatic { get; init; } = true;
    public float Damage { get; init; }
    public float EffectiveRange { get; init; }
    public float Recoil { get; init; }
    public float Handling { get; init; }
    public float FireInterval { get; init; }
    public int MagazineSize { get; init; }
    public float SoundRadius { get; init; }
    public float ReceiverLength { get; init; }
    public float BarrelLength { get; init; }
}

public sealed class AttachmentDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ChineseName { get; init; }
    public required AttachmentSlot Slot { get; init; }
    public float DamageAdd { get; init; }
    public float RangeAdd { get; init; }
    public float RecoilMultiplier { get; init; } = 1.0f;
    public float HandlingAdd { get; init; }
    public float FireIntervalMultiplier { get; init; } = 1.0f;
    public int MagazineAdd { get; init; }
    public float SoundMultiplier { get; init; } = 1.0f;
    public float VisualScale { get; init; } = 1.0f;

    public string EffectDetail(string language)
    {
        var effects = new List<string>();
        var chinese = GameLocalization.IsChinese(language);
        if (MathF.Abs(DamageAdd) > 0.01f)
        {
            effects.Add(chinese ? $"伤害 {DamageAdd:+0;-0}" : $"DMG {DamageAdd:+0;-0}");
        }
        if (MathF.Abs(RangeAdd) > 0.01f)
        {
            effects.Add(chinese ? $"射程 {RangeAdd:+0;-0}m" : $"RANGE {RangeAdd:+0;-0}m");
        }
        if (MathF.Abs(RecoilMultiplier - 1.0f) > 0.005f)
        {
            var change = (RecoilMultiplier - 1.0f) * 100.0f;
            effects.Add(chinese ? $"后坐 {change:+0;-0}%" : $"RECOIL {change:+0;-0}%");
        }
        if (MathF.Abs(HandlingAdd) > 0.005f)
        {
            effects.Add(chinese ? $"操控 {HandlingAdd:+0.00;-0.00}" : $"HANDLING {HandlingAdd:+0.00;-0.00}");
        }
        if (MathF.Abs(FireIntervalMultiplier - 1.0f) > 0.005f)
        {
            var change = (FireIntervalMultiplier - 1.0f) * 100.0f;
            effects.Add(chinese ? $"射击间隔 {change:+0;-0}%" : $"FIRE INTERVAL {change:+0;-0}%");
        }
        if (MagazineAdd != 0)
        {
            effects.Add(chinese ? $"弹匣 {MagazineAdd:+0;-0}" : $"MAG {MagazineAdd:+0;-0}");
        }
        if (MathF.Abs(SoundMultiplier - 1.0f) > 0.005f)
        {
            var change = (SoundMultiplier - 1.0f) * 100.0f;
            effects.Add(chinese ? $"枪声 {change:+0;-0}%" : $"REPORT {change:+0;-0}%");
        }
        return effects.Count == 0
            ? chinese ? "标准规格" : "STANDARD SPEC"
            : string.Join("   ", effects);
    }
}

public readonly record struct WeaponStats(
    float Damage,
    float EffectiveRange,
    float Recoil,
    float Handling,
    float FireInterval,
    int MagazineSize,
    float SoundRadius);

public sealed class WeaponBuild
{
    public WeaponPlatform Platform { get; set; }
    public Dictionary<AttachmentSlot, string> Attachments { get; } = new();

    public WeaponBuild Clone()
    {
        var clone = new WeaponBuild { Platform = Platform };
        foreach (var pair in Attachments)
        {
            clone.Attachments[pair.Key] = pair.Value;
        }
        return clone;
    }

    public WeaponStats Stats()
    {
        var weapon = WeaponCatalog.Weapon(Platform);
        var damage = weapon.Damage;
        var range = weapon.EffectiveRange;
        var recoil = weapon.Recoil;
        var handling = weapon.Handling;
        var fireInterval = weapon.FireInterval;
        var magazineSize = weapon.MagazineSize;
        var soundRadius = weapon.SoundRadius;
        foreach (var id in Attachments.Values)
        {
            var attachment = WeaponCatalog.Attachment(id);
            damage += attachment.DamageAdd;
            range += attachment.RangeAdd;
            recoil *= attachment.RecoilMultiplier;
            handling += attachment.HandlingAdd;
            fireInterval *= attachment.FireIntervalMultiplier;
            magazineSize += attachment.MagazineAdd;
            soundRadius *= attachment.SoundMultiplier;
        }
        return new WeaponStats(
            MathF.Max(8.0f, damage),
            MathF.Max(25.0f, range),
            MathF.Max(0.45f, recoil),
            Math.Clamp(handling, 0.35f, 1.25f),
            MathF.Max(0.065f, fireInterval),
            Math.Max(1, magazineSize),
            MathF.Max(12.0f, soundRadius));
    }

    public string DisplayName(string language)
    {
        var weapon = WeaponCatalog.Weapon(Platform);
        return GameLocalization.IsChinese(language)
            ? GameLocalization.Get(weapon.LocalizationKey, language, weapon.ChineseName)
            : weapon.Name;
    }
}

public sealed class KnifeSkinDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string LocalizationKey { get; init; }
    public required Color BladeColor { get; init; }
    public required Color EdgeColor { get; init; }
    public required Color GripColor { get; init; }

    public string DisplayName(string language) => GameLocalization.Get(LocalizationKey, language, Name);
}

public static class KnifeSkinCatalog
{
    public const string DefaultId = "knife_carbon";

    private static readonly Dictionary<string, KnifeSkinDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultId] = Skin(DefaultId, "Carbon Black", "knife_skin_carbon", new Color(0.09f, 0.12f, 0.115f), new Color(0.48f, 0.58f, 0.56f), new Color(0.025f, 0.035f, 0.032f)),
        ["knife_crimson"] = Skin("knife_crimson", "Crimson Circuit", "knife_skin_crimson", new Color(0.38f, 0.035f, 0.045f), new Color(1.0f, 0.24f, 0.16f), new Color(0.09f, 0.018f, 0.022f)),
        ["knife_arctic"] = Skin("knife_arctic", "Arctic Glass", "knife_skin_arctic", new Color(0.22f, 0.58f, 0.72f), new Color(0.72f, 0.96f, 1.0f), new Color(0.055f, 0.13f, 0.17f)),
        ["knife_hazard"] = Skin("knife_hazard", "Hazard Stripe", "knife_skin_hazard", new Color(0.72f, 0.52f, 0.04f), new Color(1.0f, 0.82f, 0.18f), new Color(0.08f, 0.075f, 0.025f))
    };

    public static KnifeSkinDefinition Definition(string id) => Definitions[id];
    public static IReadOnlyCollection<KnifeSkinDefinition> All => Definitions.Values;

    private static KnifeSkinDefinition Skin(string id, string name, string localizationKey, Color blade, Color edge, Color grip) => new()
    {
        Id = id,
        Name = name,
        LocalizationKey = localizationKey,
        BladeColor = blade,
        EdgeColor = edge,
        GripColor = grip
    };
}

public sealed class LootItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public LootItemKind Kind { get; init; }
    public WeaponBuild? Weapon { get; init; }
    public string AttachmentId { get; init; } = string.Empty;
    public EquipmentItem? Equipment { get; init; }
    public AmmoCaliber AmmoCaliber { get; init; } = AmmoCaliber.Rifle;
    public string KnifeSkinId { get; init; } = string.Empty;
    public MedicalItemKind MedicalKind { get; init; } = MedicalItemKind.Bandage;
    public int Quantity { get; set; } = 1;
    public LootGrade Grade { get; init; } = LootGrade.Common;

    public string DisplayName(string language)
    {
        var gradeTag = LootGrades.DisplayName(Grade, language);
        var core = Kind switch
        {
            LootItemKind.Weapon => Weapon?.DisplayName(language) ?? (GameLocalization.IsChinese(language) ? "武器" : "Weapon"),
            LootItemKind.Attachment => LocalizedAttachment(language),
            LootItemKind.Ammunition => $"{WeaponCatalog.AmmoDisplayName(AmmoCaliber, language)} T{(int)Grade + 1} x{Quantity}",
            LootItemKind.ArmorPlate => GameLocalization.IsChinese(language) ? $"复合护甲板 x{Quantity}" : $"Composite armor plate x{Quantity}",
            LootItemKind.Equipment => Equipment?.DisplayName(language) ?? (GameLocalization.IsChinese(language) ? "装备" : "Equipment"),
            LootItemKind.KnifeSkin => string.IsNullOrEmpty(KnifeSkinId)
                ? GameLocalization.Get("knife_skin", language, "Knife finish")
                : KnifeSkinCatalog.Definition(KnifeSkinId).DisplayName(language),
            LootItemKind.Medical => $"{MedicalItems.DisplayName(MedicalKind, language)} x{Quantity}",
            _ => GameLocalization.IsChinese(language) ? "物品" : "Item"
        };
        return GameLocalization.IsChinese(language) ? $"[{gradeTag}] {core}" : $"[{gradeTag}] {core}";
    }

    public string Detail(string language)
    {
        var valueLine = GameLocalization.IsChinese(language)
            ? $"估值 {UnitValue * Mathf.Max(1, Quantity)}"
            : $"VALUE {UnitValue * Mathf.Max(1, Quantity)}";
        if (Kind == LootItemKind.Weapon && Weapon is not null)
        {
            var stats = Weapon.Stats();
            return GameLocalization.IsChinese(language)
                ? $"伤害 {stats.Damage:0}  射程 {stats.EffectiveRange:0}m  后坐 {stats.Recoil:0.00}  操控 {stats.Handling:0.00}  {valueLine}"
                : $"DMG {stats.Damage:0}  RANGE {stats.EffectiveRange:0}m  RECOIL {stats.Recoil:0.00}  HANDLING {stats.Handling:0.00}  {valueLine}";
        }
        if (Kind == LootItemKind.Attachment)
        {
            var part = WeaponCatalog.Attachment(AttachmentId);
            var slot = GameLocalization.IsChinese(language) ? WeaponCatalog.SlotChinese(part.Slot) : part.Slot.ToString().ToUpperInvariant();
            return GameLocalization.IsChinese(language)
                ? $"{slot}零件，可安装至当前主武器  {valueLine}"
                : $"{slot} part, installs on the equipped primary  {valueLine}";
        }
        if (Kind == LootItemKind.Equipment && Equipment is not null)
        {
            return Equipment.Detail(language) + "  " + valueLine;
        }
        if (Kind == LootItemKind.KnifeSkin && !string.IsNullOrEmpty(KnifeSkinId))
        {
            return GameLocalization.Get("knife_skin_detail", language, "Equips a permanent tactical knife finish") + "  " + valueLine;
        }
        if (Kind == LootItemKind.Medical)
        {
            return MedicalItems.EffectDescription(MedicalKind, language) + "  " + valueLine;
        }
        return GameLocalization.IsChinese(language)
            ? $"可放入个人背包  {valueLine}"
            : $"Can be stored in the backpack  {valueLine}";
    }

    /// <summary>Single-unit sell value used by backpack total.</summary>
    public int UnitValue
    {
        get
        {
            var baseValue = LootGrades.BaseValue(Grade);
            return Kind switch
            {
                LootItemKind.Weapon => baseValue + (int)Grade * 180 + 200,
                LootItemKind.Attachment => baseValue + 60,
                LootItemKind.Equipment => baseValue + 90,
                LootItemKind.ArmorPlate => 35 + (int)Grade * 25,
                LootItemKind.Ammunition => 2 + (int)Grade,
                LootItemKind.KnifeSkin => baseValue + 140,
                LootItemKind.Medical => MedicalItems.Definition(MedicalKind).UnitValue + (int)Grade * 30,
                _ => baseValue
            };
        }
    }

    public int StackValue => UnitValue * Mathf.Max(1, Quantity);

    public static int TotalValue(IEnumerable<LootItem> items)
    {
        var total = 0;
        foreach (var item in items)
        {
            total += item.StackValue;
        }
        return total;
    }

    private string LocalizedAttachment(string language)
    {
        var part = WeaponCatalog.Attachment(AttachmentId);
        return GameLocalization.IsChinese(language) ? part.ChineseName : part.Name;
    }
}

public static class EquipmentCatalog
{
    private static readonly Dictionary<string, EquipmentDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["helmet_light"] = new EquipmentDefinition
        {
            Id = "helmet_light", Name = "Light combat helmet", ChineseName = "轻型战术头盔",
            Slot = EquipmentSlot.Helmet, Protection = 0.38f, MaxDurability = 55.0f
        },
        ["helmet_heavy"] = new EquipmentDefinition
        {
            Id = "helmet_heavy", Name = "Heavy composite helmet", ChineseName = "重型复合头盔",
            Slot = EquipmentSlot.Helmet, Protection = 0.58f, MaxDurability = 85.0f
        },
        ["armor_carrier"] = new EquipmentDefinition
        {
            Id = "armor_carrier", Name = "Plate carrier", ChineseName = "插板式防弹衣",
            Slot = EquipmentSlot.BodyArmor, Protection = 0.54f, MaxDurability = 100.0f
        },
        ["armor_heavy"] = new EquipmentDefinition
        {
            Id = "armor_heavy", Name = "Heavy assault armor", ChineseName = "重型突击护甲",
            Slot = EquipmentSlot.BodyArmor, Protection = 0.68f, MaxDurability = 125.0f
        },
        ["pack_assault"] = new EquipmentDefinition
        {
            Id = "pack_assault", Name = "Assault backpack", ChineseName = "突击背包",
            Slot = EquipmentSlot.Backpack, Protection = 0.0f, MaxDurability = 80.0f, CapacityBonus = 6
        },
        ["pack_heavy"] = new EquipmentDefinition
        {
            Id = "pack_heavy", Name = "Heavy expedition pack", ChineseName = "重型远征背包",
            Slot = EquipmentSlot.Backpack, Protection = 0.0f, MaxDurability = 110.0f, CapacityBonus = 10
        }
    };

    public static EquipmentDefinition Definition(string id) => Definitions[id];

    public static EquipmentItem Create(string id)
    {
        var definition = Definition(id);
        return new EquipmentItem { DefinitionId = id, Durability = definition.MaxDurability };
    }

    public static IReadOnlyCollection<EquipmentDefinition> All => Definitions.Values;
}

public static class WeaponCatalog
{
    private static readonly Dictionary<WeaponPlatform, WeaponDefinition> Weapons = new()
    {
        [WeaponPlatform.M4A1] = new WeaponDefinition
        {
            Platform = WeaponPlatform.M4A1, Name = "M4A1 Carbine", ChineseName = "M4A1 卡宾枪",
            Damage = 31, EffectiveRange = 125, Recoil = 1.0f, Handling = 0.94f,
            FireInterval = 0.092f, MagazineSize = 30, SoundRadius = 42, ReceiverLength = 0.46f, BarrelLength = 0.54f
        },
        [WeaponPlatform.AK74] = new WeaponDefinition
        {
            Platform = WeaponPlatform.AK74, Name = "AK-74N", ChineseName = "AK-74N 突击步枪",
            Damage = 35, EffectiveRange = 150, Recoil = 1.22f, Handling = 0.78f,
            FireInterval = 0.105f, MagazineSize = 30, SoundRadius = 46, ReceiverLength = 0.5f, BarrelLength = 0.62f
        },
        [WeaponPlatform.ScarL] = new WeaponDefinition
        {
            Platform = WeaponPlatform.ScarL, Name = "SCAR-L", ChineseName = "SCAR-L 特种步枪",
            Damage = 38, EffectiveRange = 175, Recoil = 1.14f, Handling = 0.7f,
            FireInterval = 0.115f, MagazineSize = 20, SoundRadius = 48, ReceiverLength = 0.55f, BarrelLength = 0.67f
        },
        [WeaponPlatform.M24] = new WeaponDefinition
        {
            Platform = WeaponPlatform.M24, Name = "M24 Precision Rifle", ChineseName = "M24",
            LocalizationKey = "weapon_m24", Caliber = AmmoCaliber.Sniper, SupportsAutomatic = false,
            Damage = 96, EffectiveRange = 320, Recoil = 2.35f, Handling = 0.43f,
            FireInterval = 1.05f, MagazineSize = 5, SoundRadius = 78, ReceiverLength = 0.64f, BarrelLength = 0.92f
        },
        [WeaponPlatform.MP5A5] = new WeaponDefinition
        {
            Platform = WeaponPlatform.MP5A5, Name = "MP5A5 Submachine Gun", ChineseName = "MP5A5",
            LocalizationKey = "weapon_mp5a5", Caliber = AmmoCaliber.Smg,
            Damage = 24, EffectiveRange = 88, Recoil = 0.72f, Handling = 1.12f,
            FireInterval = 0.067f, MagazineSize = 30, SoundRadius = 31, ReceiverLength = 0.36f, BarrelLength = 0.3f
        }
    };

    private static readonly Dictionary<string, AttachmentDefinition> Attachments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["optic_micro"] = Part("optic_micro", "Micro reflex sight", "微型反射瞄具", AttachmentSlot.Optic, handling: 0.04f, visual: 0.82f),
        ["optic_holo"] = Part("optic_holo", "Holographic sight", "全息瞄具", AttachmentSlot.Optic, range: 6, handling: -0.02f, visual: 1.08f),
        ["optic_scope"] = Part("optic_scope", "4x combat optic", "四倍战斗瞄具", AttachmentSlot.Optic, range: 28, handling: -0.09f, visual: 1.28f),
        ["optic_sniper"] = Part("optic_sniper", "8x precision optic", "8x", AttachmentSlot.Optic, range: 85, handling: -0.16f, visual: 1.55f),
        ["barrel_cqb"] = Part("barrel_cqb", "CQB barrel", "近战短枪管", AttachmentSlot.Barrel, damage: -2, range: -24, recoil: 1.12f, handling: 0.13f, visual: 0.72f),
        ["barrel_standard"] = Part("barrel_standard", "Service barrel", "制式枪管", AttachmentSlot.Barrel),
        ["barrel_marksman"] = Part("barrel_marksman", "Marksman barrel", "精确射手枪管", AttachmentSlot.Barrel, damage: 3, range: 38, recoil: 0.91f, handling: -0.12f, visual: 1.28f),
        ["muzzle_brake"] = Part("muzzle_brake", "Three-port brake", "三室制退器", AttachmentSlot.Muzzle, recoil: 0.78f, sound: 1.15f, visual: 1.05f),
        ["muzzle_suppressor"] = Part("muzzle_suppressor", "Tactical suppressor", "战术消音器", AttachmentSlot.Muzzle, damage: -1, range: 12, recoil: 0.92f, handling: -0.06f, sound: 0.55f, visual: 1.55f),
        ["grip_angled"] = Part("grip_angled", "Angled foregrip", "斜角前握把", AttachmentSlot.Grip, recoil: 0.91f, handling: 0.06f, visual: 0.82f),
        ["grip_vertical"] = Part("grip_vertical", "Vertical foregrip", "垂直前握把", AttachmentSlot.Grip, recoil: 0.82f, handling: -0.03f, visual: 1.0f),
        ["stock_light"] = Part("stock_light", "Lightweight stock", "轻量枪托", AttachmentSlot.Stock, recoil: 1.08f, handling: 0.12f, visual: 0.86f),
        ["stock_precision"] = Part("stock_precision", "Precision stock", "精确射击枪托", AttachmentSlot.Stock, recoil: 0.79f, handling: -0.1f, visual: 1.12f),
        ["mag_standard"] = Part("mag_standard", "Standard magazine", "标准弹匣", AttachmentSlot.Magazine),
        ["mag_extended"] = Part("mag_extended", "Extended magazine", "扩容弹匣", AttachmentSlot.Magazine, recoil: 1.04f, handling: -0.11f, magazine: 15, visual: 1.24f)
    };

    public static WeaponDefinition Weapon(WeaponPlatform platform) => Weapons[platform];
    public static AttachmentDefinition Attachment(string id) => Attachments[id];
    public static IReadOnlyCollection<WeaponDefinition> AllWeapons => Weapons.Values;
    public static IReadOnlyCollection<AttachmentDefinition> AllAttachments => Attachments.Values;

    public static string AmmoDisplayName(AmmoCaliber caliber, string language) => caliber switch
    {
        AmmoCaliber.Sniper => GameLocalization.Get("ammo_sniper", language, "7.62 precision ammunition"),
        AmmoCaliber.Smg => GameLocalization.Get("ammo_smg", language, "9 mm submachine-gun ammunition"),
        _ => GameLocalization.Get("ammo_rifle", language, "Rifle ammunition")
    };

    public static WeaponBuild StarterWeapon()
    {
        var build = new WeaponBuild { Platform = WeaponPlatform.M4A1 };
        build.Attachments[AttachmentSlot.Optic] = "optic_micro";
        build.Attachments[AttachmentSlot.Barrel] = "barrel_standard";
        build.Attachments[AttachmentSlot.Grip] = "grip_angled";
        build.Attachments[AttachmentSlot.Stock] = "stock_light";
        build.Attachments[AttachmentSlot.Magazine] = "mag_standard";
        return build;
    }

    public static WeaponBuild Build(WeaponPlatform platform, int tier)
    {
        var build = new WeaponBuild { Platform = platform };
        if (platform == WeaponPlatform.M24)
        {
            build.Attachments[AttachmentSlot.Barrel] = "barrel_marksman";
            build.Attachments[AttachmentSlot.Optic] = "optic_sniper";
            build.Attachments[AttachmentSlot.Stock] = "stock_precision";
            build.Attachments[AttachmentSlot.Magazine] = "mag_standard";
            if (tier >= 2)
            {
                build.Attachments[AttachmentSlot.Muzzle] = "muzzle_suppressor";
            }
            return build;
        }
        if (platform == WeaponPlatform.MP5A5)
        {
            build.Attachments[AttachmentSlot.Barrel] = "barrel_cqb";
            build.Attachments[AttachmentSlot.Optic] = tier >= 1 ? "optic_holo" : "optic_micro";
            build.Attachments[AttachmentSlot.Grip] = "grip_angled";
            build.Attachments[AttachmentSlot.Stock] = "stock_light";
            build.Attachments[AttachmentSlot.Magazine] = tier >= 1 ? "mag_extended" : "mag_standard";
            if (tier >= 2)
            {
                build.Attachments[AttachmentSlot.Muzzle] = "muzzle_suppressor";
            }
            return build;
        }
        build.Attachments[AttachmentSlot.Barrel] = tier >= 2 ? "barrel_marksman" : tier == 0 ? "barrel_cqb" : "barrel_standard";
        build.Attachments[AttachmentSlot.Optic] = tier >= 2 ? "optic_scope" : tier == 1 ? "optic_holo" : "optic_micro";
        build.Attachments[AttachmentSlot.Grip] = tier >= 1 ? "grip_vertical" : "grip_angled";
        build.Attachments[AttachmentSlot.Stock] = tier >= 2 ? "stock_precision" : "stock_light";
        build.Attachments[AttachmentSlot.Magazine] = tier >= 2 ? "mag_extended" : "mag_standard";
        if (tier >= 1)
        {
            build.Attachments[AttachmentSlot.Muzzle] = tier == 1 ? "muzzle_brake" : "muzzle_suppressor";
        }
        return build;
    }

    public static string SlotChinese(AttachmentSlot slot) => slot switch
    {
        AttachmentSlot.Optic => "瞄具",
        AttachmentSlot.Barrel => "枪管",
        AttachmentSlot.Muzzle => "枪口",
        AttachmentSlot.Grip => "握把",
        AttachmentSlot.Stock => "枪托",
        AttachmentSlot.Magazine => "弹匣",
        _ => "武器"
    };

    private static AttachmentDefinition Part(
        string id,
        string name,
        string chinese,
        AttachmentSlot slot,
        float damage = 0,
        float range = 0,
        float recoil = 1,
        float handling = 0,
        float interval = 1,
        int magazine = 0,
        float sound = 1,
        float visual = 1)
    {
        return new AttachmentDefinition
        {
            Id = id,
            Name = name,
            ChineseName = chinese,
            Slot = slot,
            DamageAdd = damage,
            RangeAdd = range,
            RecoilMultiplier = recoil,
            HandlingAdd = handling,
            FireIntervalMultiplier = interval,
            MagazineAdd = magazine,
            SoundMultiplier = sound,
            VisualScale = visual
        };
    }
}
