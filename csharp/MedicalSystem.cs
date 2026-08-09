using Godot;

namespace OperationSteelTide;

public enum MedicalItemKind
{
    Bandage,
    FieldMedkit,
    Adrenaline
}

public enum FieldUseKind
{
    Bandage,
    FieldMedkit,
    Adrenaline,
    ArmorPlate
}

public readonly record struct FieldUseDefinition(
    FieldUseKind Kind,
    string Name,
    string LocalizationKey,
    string EffectKey,
    string EnglishEffect,
    string Glyph,
    Color Accent);

public readonly record struct MedicalItemDefinition(
    MedicalItemKind Kind,
    string Name,
    string LocalizationKey,
    string EffectKey,
    string EnglishEffect,
    string Glyph,
    float UseDuration,
    float HealthRestore,
    int UnitValue,
    Color Accent);

public static class MedicalItems
{
    public static MedicalItemDefinition Definition(MedicalItemKind kind) => kind switch
    {
        MedicalItemKind.FieldMedkit => new MedicalItemDefinition(
            kind,
            "Field trauma kit",
            "medical_medkit",
            "medical_medkit_effect",
            "RESTORE 72 HEALTH  //  2.35s",
            "+",
            2.35f,
            72.0f,
            180,
            new Color(0.25f, 0.9f, 0.58f)),
        MedicalItemKind.Adrenaline => new MedicalItemDefinition(
            kind,
            "Adrenaline injector",
            "medical_adrenaline",
            "medical_adrenaline_effect",
            "RESTORE 12 HEALTH + STAMINA  //  14s BOOST",
            ">>",
            0.85f,
            12.0f,
            240,
            new Color(0.96f, 0.64f, 0.18f)),
        _ => new MedicalItemDefinition(
            MedicalItemKind.Bandage,
            "Hemostatic bandage",
            "medical_bandage",
            "medical_bandage_effect",
            "RESTORE 32 HEALTH  //  1.15s",
            "B",
            1.15f,
            32.0f,
            75,
            new Color(0.78f, 0.9f, 0.86f))
    };

    public static string DisplayName(MedicalItemKind kind, string language)
    {
        var definition = Definition(kind);
        return GameLocalization.Get(definition.LocalizationKey, language, definition.Name);
    }

    public static string EffectDescription(MedicalItemKind kind, string language)
    {
        var definition = Definition(kind);
        return GameLocalization.Get(definition.EffectKey, language, definition.EnglishEffect);
    }
}

public static class FieldUseItems
{
    public static FieldUseDefinition Definition(FieldUseKind kind)
    {
        if (kind == FieldUseKind.ArmorPlate)
        {
            return new FieldUseDefinition(
                kind,
                "Composite armor plate",
                "armor_plate",
                "armor_plate_effect",
                "REPAIR EQUIPPED ARMOR  //  QUALITY SCALES REPAIR",
                "P",
                new Color(0.32f, 0.68f, 1.0f));
        }
        var medical = MedicalItems.Definition(ToMedical(kind));
        return new FieldUseDefinition(
            kind,
            medical.Name,
            medical.LocalizationKey,
            medical.EffectKey,
            medical.EnglishEffect,
            medical.Glyph,
            medical.Accent);
    }

    public static MedicalItemKind ToMedical(FieldUseKind kind) => kind switch
    {
        FieldUseKind.FieldMedkit => MedicalItemKind.FieldMedkit,
        FieldUseKind.Adrenaline => MedicalItemKind.Adrenaline,
        _ => MedicalItemKind.Bandage
    };

    public static FieldUseKind FromMedical(MedicalItemKind kind) => kind switch
    {
        MedicalItemKind.FieldMedkit => FieldUseKind.FieldMedkit,
        MedicalItemKind.Adrenaline => FieldUseKind.Adrenaline,
        _ => FieldUseKind.Bandage
    };

    public static string DisplayName(FieldUseKind kind, string language)
    {
        var definition = Definition(kind);
        return GameLocalization.Get(definition.LocalizationKey, language, definition.Name);
    }

    public static string EffectDescription(FieldUseKind kind, string language)
    {
        var definition = Definition(kind);
        return GameLocalization.Get(definition.EffectKey, language, definition.EnglishEffect);
    }
}

public static class ArmorPlateSupplies
{
    public static float RepairFraction(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => 0.35f,
        LootGrade.Rare => 0.46f,
        LootGrade.Epic => 0.62f,
        LootGrade.Legendary => 0.82f,
        _ => 0.26f
    };
}
