using System;
using System.Collections.Generic;
using System.Linq;

namespace OperationSteelTide;

public enum WeaponPlatform
{
    M4A1,
    AK74,
    ScarL
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
    Equipment
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
            Math.Max(10, magazineSize),
            MathF.Max(12.0f, soundRadius));
    }

    public string DisplayName(string language)
    {
        var weapon = WeaponCatalog.Weapon(Platform);
        return GameLocalization.IsChinese(language) ? weapon.ChineseName : weapon.Name;
    }
}

public sealed class LootItem
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public LootItemKind Kind { get; init; }
    public WeaponBuild? Weapon { get; init; }
    public string AttachmentId { get; init; } = string.Empty;
    public EquipmentItem? Equipment { get; init; }
    public int Quantity { get; set; } = 1;

    public string DisplayName(string language)
    {
        return Kind switch
        {
            LootItemKind.Weapon => Weapon?.DisplayName(language) ?? "Weapon",
            LootItemKind.Attachment => LocalizedAttachment(language),
            LootItemKind.Ammunition => GameLocalization.IsChinese(language) ? $"步枪弹药 x{Quantity}" : $"Rifle ammunition x{Quantity}",
            LootItemKind.ArmorPlate => GameLocalization.IsChinese(language) ? $"复合护甲板 x{Quantity}" : $"Composite armor plate x{Quantity}",
            LootItemKind.Equipment => Equipment?.DisplayName(language) ?? "Equipment",
            _ => "Item"
        };
    }

    public string Detail(string language)
    {
        if (Kind == LootItemKind.Weapon && Weapon is not null)
        {
            var stats = Weapon.Stats();
            return GameLocalization.IsChinese(language)
                ? $"伤害 {stats.Damage:0}  射程 {stats.EffectiveRange:0}m  后坐 {stats.Recoil:0.00}  操控 {stats.Handling:0.00}"
                : $"DMG {stats.Damage:0}  RANGE {stats.EffectiveRange:0}m  RECOIL {stats.Recoil:0.00}  HANDLING {stats.Handling:0.00}";
        }
        if (Kind == LootItemKind.Attachment)
        {
            var part = WeaponCatalog.Attachment(AttachmentId);
            var slot = GameLocalization.IsChinese(language) ? WeaponCatalog.SlotChinese(part.Slot) : part.Slot.ToString().ToUpperInvariant();
            return GameLocalization.IsChinese(language) ? $"{slot}零件，可安装至当前主武器" : $"{slot} part, installs on the equipped primary";
        }
        if (Kind == LootItemKind.Equipment && Equipment is not null)
        {
            return Equipment.Detail(language);
        }
        return GameLocalization.IsChinese(language) ? "可放入个人背包" : "Can be stored in the backpack";
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
        }
    };

    private static readonly Dictionary<string, AttachmentDefinition> Attachments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["optic_micro"] = Part("optic_micro", "Micro reflex sight", "微型反射瞄具", AttachmentSlot.Optic, handling: 0.04f, visual: 0.82f),
        ["optic_holo"] = Part("optic_holo", "Holographic sight", "全息瞄具", AttachmentSlot.Optic, range: 6, handling: -0.02f, visual: 1.08f),
        ["optic_scope"] = Part("optic_scope", "4x combat optic", "四倍战斗瞄具", AttachmentSlot.Optic, range: 28, handling: -0.09f, visual: 1.28f),
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
    public static IReadOnlyCollection<AttachmentDefinition> AllAttachments => Attachments.Values;

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
