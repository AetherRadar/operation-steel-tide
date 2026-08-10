using System.Collections.Generic;

namespace OperationSteelTide;

public enum ValuableItemKind
{
    CannedCoffee,
    CeramicTeaSet,
    HandToolSet,
    SmartPhone,
    Wristwatch,
    VintageCamera,
    GraphicsCard,
    DesignerPerfume,
    CollectorCoin,
    GoldJewelry,
    EncryptedDrive,
    AntiqueClock,
    TideHunterTransponder
}

public readonly record struct ValuableItemDefinition(
    ValuableItemKind Kind,
    string Name,
    string LocalizationKey,
    string DetailKey,
    string EnglishDetail,
    int BaseValue,
    LootGrade NativeGrade);

public static class ValuableItems
{
    private static readonly Dictionary<ValuableItemKind, ValuableItemDefinition> Definitions = new()
    {
        [ValuableItemKind.CannedCoffee] = Item(ValuableItemKind.CannedCoffee, "Imported coffee tin", "valuable_coffee", "valuable_coffee_detail", "SEALED HOUSEHOLD FOOD  //  CIVILIAN TRADE GOOD", 25, LootGrade.Common),
        [ValuableItemKind.CeramicTeaSet] = Item(ValuableItemKind.CeramicTeaSet, "Ceramic tea set", "valuable_tea_set", "valuable_tea_set_detail", "INTACT DOMESTIC SET  //  FRAGILE COLLECTIBLE", 45, LootGrade.Common),
        [ValuableItemKind.HandToolSet] = Item(ValuableItemKind.HandToolSet, "Professional hand tools", "valuable_tools", "valuable_tools_detail", "WORKSHOP-GRADE TOOLS  //  HIGH LOCAL DEMAND", 95, LootGrade.Uncommon),
        [ValuableItemKind.SmartPhone] = Item(ValuableItemKind.SmartPhone, "Flagship smartphone", "valuable_phone", "valuable_phone_detail", "LOCKED CIVILIAN ELECTRONICS  //  SALVAGEABLE", 125, LootGrade.Uncommon),
        [ValuableItemKind.Wristwatch] = Item(ValuableItemKind.Wristwatch, "Automatic wristwatch", "valuable_watch", "valuable_watch_detail", "MECHANICAL TIMEPIECE  //  GOOD CONDITION", 150, LootGrade.Uncommon),
        [ValuableItemKind.VintageCamera] = Item(ValuableItemKind.VintageCamera, "Vintage rangefinder camera", "valuable_camera", "valuable_camera_detail", "OPTICAL COLLECTIBLE  //  ORIGINAL LENS", 260, LootGrade.Rare),
        [ValuableItemKind.GraphicsCard] = Item(ValuableItemKind.GraphicsCard, "High-end graphics card", "valuable_gpu", "valuable_gpu_detail", "UNDAMAGED COMPUTE HARDWARE  //  SEALED CONTACTS", 320, LootGrade.Rare),
        [ValuableItemKind.DesignerPerfume] = Item(ValuableItemKind.DesignerPerfume, "Designer perfume", "valuable_perfume", "valuable_perfume_detail", "UNOPENED LUXURY GOODS  //  VERIFIED BOTTLE", 240, LootGrade.Rare),
        [ValuableItemKind.CollectorCoin] = Item(ValuableItemKind.CollectorCoin, "Commemorative gold coin", "valuable_coin", "valuable_coin_detail", "LIMITED MINTING  //  COLLECTOR DEMAND", 620, LootGrade.Epic),
        [ValuableItemKind.GoldJewelry] = Item(ValuableItemKind.GoldJewelry, "Gold jewelry case", "valuable_jewelry", "valuable_jewelry_detail", "PRECIOUS METAL SET  //  INTACT CLASPS", 720, LootGrade.Epic),
        [ValuableItemKind.EncryptedDrive] = Item(ValuableItemKind.EncryptedDrive, "Encrypted enterprise drive", "valuable_drive", "valuable_drive_detail", "CORPORATE DATA STORAGE  //  UNKNOWN CONTENTS", 790, LootGrade.Epic),
        [ValuableItemKind.AntiqueClock] = Item(ValuableItemKind.AntiqueClock, "Antique marine clock", "valuable_clock", "valuable_clock_detail", "NUMBERED MARITIME ANTIQUE  //  MUSEUM GRADE", 1480, LootGrade.Legendary),
        [ValuableItemKind.TideHunterTransponder] = Item(ValuableItemKind.TideHunterTransponder, "Tide Hunter transponder", "valuable_tidehunter", "valuable_tidehunter_detail", "ROAMING HUNTER IFF CORE  //  BOUNTY PROOF", 4800, LootGrade.Legendary)
    };

    private static readonly ValuableItemKind[][] GradePools =
    {
        new[] { ValuableItemKind.CannedCoffee, ValuableItemKind.CeramicTeaSet },
        new[] { ValuableItemKind.HandToolSet, ValuableItemKind.SmartPhone, ValuableItemKind.Wristwatch },
        new[] { ValuableItemKind.VintageCamera, ValuableItemKind.GraphicsCard, ValuableItemKind.DesignerPerfume },
        new[] { ValuableItemKind.CollectorCoin, ValuableItemKind.GoldJewelry, ValuableItemKind.EncryptedDrive },
        new[] { ValuableItemKind.AntiqueClock }
    };

    public static ValuableItemDefinition Definition(ValuableItemKind kind) => Definitions[kind];

    public static IReadOnlyCollection<ValuableItemDefinition> All => Definitions.Values;

    public static ValuableItemKind SelectForGrade(LootGrade grade, int selector)
    {
        var pool = GradePools[(int)grade];
        return pool[(int)((uint)selector % (uint)pool.Length)];
    }

    public static string DisplayName(ValuableItemKind kind, string language)
    {
        var definition = Definition(kind);
        return GameLocalization.Get(definition.LocalizationKey, language, definition.Name);
    }

    public static string Detail(ValuableItemKind kind, string language)
    {
        var definition = Definition(kind);
        return GameLocalization.Get(definition.DetailKey, language, definition.EnglishDetail);
    }

    private static ValuableItemDefinition Item(
        ValuableItemKind kind,
        string name,
        string localizationKey,
        string detailKey,
        string englishDetail,
        int baseValue,
        LootGrade nativeGrade)
    {
        return new ValuableItemDefinition(kind, name, localizationKey, detailKey, englishDetail, baseValue, nativeGrade);
    }
}
