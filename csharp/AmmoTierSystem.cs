using Godot;

namespace OperationSteelTide;

public static class AmmoTiers
{
    public static float DamageMultiplier(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => 1.0f,
        LootGrade.Rare => 1.06f,
        LootGrade.Epic => 1.12f,
        LootGrade.Legendary => 1.18f,
        _ => 0.94f
    };

    public static float ArmorPenetration(LootGrade grade) => grade switch
    {
        LootGrade.Uncommon => 0.08f,
        LootGrade.Rare => 0.17f,
        LootGrade.Epic => 0.27f,
        LootGrade.Legendary => 0.38f,
        _ => 0.0f
    };

    public static Color Color(LootGrade grade) => LootGrades.GlowColor(grade);

    public static string DisplayName(LootGrade grade, string language)
    {
        var tier = (int)grade + 1;
        return GameLocalization.IsChinese(language)
            ? $"{tier}\u7ea7\u5f39"
            : $"T{tier} AMMO";
    }
}
