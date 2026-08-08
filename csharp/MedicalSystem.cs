using Godot;

namespace OperationSteelTide;

public enum MedicalItemKind
{
    Bandage,
    FieldMedkit,
    Adrenaline
}

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
