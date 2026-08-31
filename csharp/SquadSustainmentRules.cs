using System;

namespace OperationSteelTide;

internal enum SquadSustainmentActionKind
{
    None,
    Loot,
    Heal,
    RepairArmor
}

/// <summary>
/// Pure utility and eligibility rules for AI squad sustainment. Runtime nodes own
/// navigation and inventory mutation; this type only answers cheap decisions.
/// </summary>
internal static class SquadSustainmentRules
{
    internal const float CriticalHealthRatio = 0.42f;
    internal const float OpportunisticHealthRatio = 0.68f;
    internal const float ArmorRepairRatio = 0.52f;
    internal const float NearbyThreatRange = 18.0f;
    internal const float MinimumWeaponUpgradeRatio = 1.08f;
    internal const float MinimumEquipmentUpgradeRatio = 1.06f;

    internal static bool CanStartSustainment(
        bool downed,
        bool bodyBag,
        bool reviving,
        bool evacuating,
        bool recentlyDamaged,
        float hostileDistance)
        => !downed
            && !bodyBag
            && !reviving
            && !evacuating
            && !recentlyDamaged
            && hostileDistance >= NearbyThreatRange;

    internal static MedicalItemKind? SelectMedical(
        float health,
        float maxHealth,
        int bandages,
        int fieldMedkits,
        int adrenaline)
    {
        var missing = MathF.Max(0.0f, maxHealth - health);
        if (missing <= 1.0f)
        {
            return null;
        }

        var ratio = health / MathF.Max(1.0f, maxHealth);
        if (ratio <= CriticalHealthRatio && fieldMedkits > 0)
        {
            return MedicalItemKind.FieldMedkit;
        }
        if (bandages > 0 && (ratio <= OpportunisticHealthRatio || missing >= 24.0f))
        {
            return MedicalItemKind.Bandage;
        }
        if (fieldMedkits > 0 && ratio <= OpportunisticHealthRatio)
        {
            return MedicalItemKind.FieldMedkit;
        }
        return adrenaline > 0 && ratio <= CriticalHealthRatio
            ? MedicalItemKind.Adrenaline
            : null;
    }

    internal static float WeaponUtility(
        WeaponBuild weapon,
        LootGrade weaponGrade,
        LootGrade ammoGrade,
        OperatorRole role)
    {
        var stats = weapon.Stats();
        var fireRate = 1.0f / MathF.Max(0.065f, stats.FireInterval);
        var sustainedDamage = stats.Damage * fireRate;
        var gradeMultiplier = AmmoTiers.DamageMultiplier(ammoGrade)
            * (1.0f + (int)weaponGrade * 0.025f);
        var roleDamageWeight = role == OperatorRole.Recon ? 0.28f : 0.58f;
        var roleRangeWeight = role == OperatorRole.Recon ? 0.95f : 0.34f;
        var precisionDamage = role == OperatorRole.Recon ? stats.Damage * 2.1f : stats.Damage;
        return (sustainedDamage * roleDamageWeight
                + precisionDamage
                + stats.EffectiveRange * roleRangeWeight
                + stats.Handling * 58.0f
                + stats.MagazineSize * 0.7f
                - stats.Recoil * 18.0f)
            * gradeMultiplier;
    }

    internal static bool IsWeaponUpgrade(
        WeaponBuild current,
        LootGrade currentWeaponGrade,
        LootGrade currentAmmoGrade,
        WeaponBuild incoming,
        LootGrade incomingWeaponGrade,
        LootGrade incomingAmmoGrade,
        OperatorRole role)
        => WeaponUtility(incoming, incomingWeaponGrade, incomingAmmoGrade, role)
            >= WeaponUtility(current, currentWeaponGrade, currentAmmoGrade, role)
                * MinimumWeaponUpgradeRatio;

    internal static float EquipmentUtility(EquipmentItem equipment, LootGrade grade)
    {
        var definition = equipment.Definition;
        var durabilityRatio = definition.MaxDurability <= 0.01f
            ? 0.0f
            : Math.Clamp(equipment.Durability / definition.MaxDurability, 0.0f, 1.0f);
        var gradeBonus = (int)grade * 5.0f;
        if (definition.Slot == EquipmentSlot.Backpack)
        {
            return definition.CapacityBonus * 90.0f
                + definition.MaxDurability * 0.2f
                + durabilityRatio * 18.0f
                + gradeBonus;
        }
        return definition.Protection * 1000.0f * durabilityRatio
            + definition.MaxDurability * 1.35f * durabilityRatio
            + gradeBonus;
    }

    internal static bool IsEquipmentUpgrade(
        EquipmentItem current,
        LootGrade currentGrade,
        EquipmentItem incoming,
        LootGrade incomingGrade)
        => current.Definition.Slot == incoming.Definition.Slot
            && incoming.Definition.MaxDurability > 0.01f
            && incoming.Durability / incoming.Definition.MaxDurability >= 0.18f
            && EquipmentUtility(incoming, incomingGrade)
                >= EquipmentUtility(current, currentGrade) * MinimumEquipmentUpgradeRatio;
}
