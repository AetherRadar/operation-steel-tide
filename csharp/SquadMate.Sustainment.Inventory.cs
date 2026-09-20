using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    internal float EvaluateSustainmentLootUtility(LootItem item)
    {
        if (item.Quantity <= 0)
        {
            return 0.0f;
        }
        if (item.Kind == LootItemKind.Weapon && item.Weapon is not null && item.Quantity == 1)
        {
            // A weapon item does not encode its ammunition stack. Common is the
            // safe fallback; the package-aware service supplies the actual grade.
            return EvaluateSustainmentWeaponUtility(item, LootGrade.Common);
        }
        if (item.Kind == LootItemKind.Equipment
            && item.Equipment is not null
            && item.Quantity == 1)
        {
            var (current, grade) = EquipmentForSlot(item.Equipment.Definition.Slot);
            return SquadSustainmentRules.IsEquipmentUpgrade(current, grade, item.Equipment, item.Grade)
                ? SquadSustainmentRules.EquipmentUtility(item.Equipment, item.Grade)
                    - SquadSustainmentRules.EquipmentUtility(current, grade)
                : 0.0f;
        }
        if (item.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate
            && CountSustainmentSupplies() < SustainmentSupplyCapacity)
        {
            return item.Kind == LootItemKind.Medical
                ? 80.0f
                    + MedicalItems.Definition(item.MedicalKind).HealthRestore
                    + (int)item.Grade * 18.0f
                : 70.0f + ArmorPlateSupplies.RepairFraction(item.Grade) * 100.0f;
        }
        return 0.0f;
    }

    internal float EvaluateSustainmentWeaponUtility(
        LootItem item,
        LootGrade incomingAmmoGrade)
    {
        if (item.Quantity != 1
            || item.Kind != LootItemKind.Weapon
            || item.Weapon is null)
        {
            return 0.0f;
        }
        if (!HasFireablePrimary)
        {
            return 10000.0f + SquadSustainmentRules.WeaponUtility(
                item.Weapon,
                item.Grade,
                incomingAmmoGrade,
                Role);
        }
        if (!SquadSustainmentRules.IsWeaponUpgrade(
                CarriedWeapon,
                _carriedWeaponGrade,
                _ammoGrade,
                item.Weapon,
                item.Grade,
                incomingAmmoGrade,
                Role))
        {
            return 0.0f;
        }
        return SquadSustainmentRules.WeaponUtility(
                item.Weapon,
                item.Grade,
                incomingAmmoGrade,
                Role)
            - SquadSustainmentRules.WeaponUtility(
                CarriedWeapon,
                _carriedWeaponGrade,
                _ammoGrade,
                Role);
    }

    internal bool TryEquipSustainmentWeapon(
        LootItem item,
        LootGrade incomingAmmoGrade,
        int incomingAmmoQuantity,
        out LootItem? replacement,
        out LootItem? replacementAmmo)
    {
        replacement = null;
        replacementAmmo = null;
        if (item.Kind != LootItemKind.Weapon || item.Weapon is null
            || HasFireablePrimary && !SquadSustainmentRules.IsWeaponUpgrade(
                CarriedWeapon,
                _carriedWeaponGrade,
                _ammoGrade,
                item.Weapon,
                item.Grade,
                incomingAmmoGrade,
                Role))
        {
            return false;
        }
        if (HasFireablePrimary && _carriedWeaponRecovered)
        {
            replacement = new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = CarriedWeapon.Clone(),
                Grade = _carriedWeaponGrade
            };
        }
        if (_recoveredAmmoQuantity > 0)
        {
            replacementAmmo = new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = _recoveredAmmoCaliber,
                Quantity = _recoveredAmmoQuantity,
                Grade = _recoveredAmmoGrade
            };
        }
        return EquipWeaponFromLoot(
            item.Weapon,
            incomingAmmoGrade,
            item.Grade,
            incomingAmmoQuantity);
    }

    internal bool TryEquipSustainmentEquipment(LootItem item, out LootItem? replacement)
    {
        replacement = null;
        if (item.Kind != LootItemKind.Equipment || item.Equipment is null)
        {
            return false;
        }
        var slot = item.Equipment.Definition.Slot;
        var (current, currentGrade) = EquipmentForSlot(slot);
        if (!SquadSustainmentRules.IsEquipmentUpgrade(
                current,
                currentGrade,
                item.Equipment,
                item.Grade))
        {
            return false;
        }
        if (EquipmentRecoveredForSlot(slot))
        {
            replacement = new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = current.Clone(),
                Grade = currentGrade
            };
        }
        switch (slot)
        {
            case EquipmentSlot.Helmet:
                _equippedHelmet = item.Equipment.Clone();
                _equippedHelmetGrade = item.Grade;
                _equippedHelmetRecovered = true;
                break;
            case EquipmentSlot.BodyArmor:
                _equippedBodyArmor = item.Equipment.Clone();
                _equippedBodyArmorGrade = item.Grade;
                _equippedBodyArmorRecovered = true;
                break;
            case EquipmentSlot.Backpack:
                _equippedBackpack = item.Equipment.Clone();
                _equippedBackpackGrade = item.Grade;
                _equippedBackpackRecovered = true;
                break;
        }
        return true;
    }

    internal int TryStoreSustainmentSupply(LootItem item, int requestedQuantity)
    {
        if (requestedQuantity <= 0
            || item.Kind is not (LootItemKind.Medical or LootItemKind.ArmorPlate))
        {
            return 0;
        }
        var availableSlots = Math.Max(0, SustainmentSupplyCapacity - CountSustainmentSupplies());
        var stored = Math.Min(requestedQuantity, availableSlots);
        if (stored <= 0)
        {
            return 0;
        }
        if (item.Kind == LootItemKind.Medical)
        {
            _medicalSupplies[(int)item.MedicalKind, (int)item.Grade] += stored;
            _recoveredMedicalSupplies[(int)item.MedicalKind, (int)item.Grade] += stored;
        }
        else
        {
            _armorPlateSupplies[(int)item.Grade] += stored;
            _recoveredArmorPlateSupplies[(int)item.Grade] += stored;
        }
        return stored;
    }

    internal int RecoveredAmmoQuantityForDiagnostics => _recoveredAmmoQuantity;

    internal LootGrade CommitFiredPrimaryRoundForDiagnostics()
        => CommitFiredPrimaryRound();

    private void SetRecoveredAmmo(
        AmmoCaliber caliber,
        LootGrade grade,
        int quantity)
    {
        _recoveredAmmoCaliber = caliber;
        _recoveredAmmoGrade = grade;
        _recoveredAmmoQuantity = Math.Max(0, quantity);
    }

    private void ResetRecoveredAmmo()
    {
        _recoveredAmmoCaliber = AmmoCaliber.Rifle;
        _recoveredAmmoGrade = LootGrade.Common;
        _recoveredAmmoQuantity = 0;
    }

    private void ConsumeRecoveredAmmoShot()
    {
        if (_recoveredAmmoQuantity <= 0)
        {
            return;
        }
        _recoveredAmmoQuantity--;
        if (_recoveredAmmoQuantity == 0)
        {
            // AI retains its existing infinite baseline ammunition behavior, but
            // premium field ammunition only improves the exact rounds recovered.
            _ammoGrade = LootGrade.Common;
        }
    }

    internal void AppendSustainmentLoot(List<LootItem> destination)
    {
        if (HasFireablePrimary && _carriedWeaponRecovered)
        {
            destination.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = CarriedWeapon.Clone(),
                Grade = _carriedWeaponGrade
            });
        }
        if (_equippedHelmetRecovered)
        {
            AppendEquipmentLoot(destination, _equippedHelmet, _equippedHelmetGrade);
        }
        if (_equippedBodyArmorRecovered)
        {
            AppendEquipmentLoot(destination, _equippedBodyArmor, _equippedBodyArmorGrade);
        }
        if (_equippedBackpackRecovered)
        {
            AppendEquipmentLoot(destination, _equippedBackpack, _equippedBackpackGrade);
        }
        if (_recoveredAmmoQuantity > 0)
        {
            destination.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = _recoveredAmmoCaliber,
                Quantity = _recoveredAmmoQuantity,
                Grade = _recoveredAmmoGrade
            });
        }
        for (var kindIndex = 0; kindIndex < _medicalSupplies.GetLength(0); kindIndex++)
        {
            for (var gradeIndex = 0; gradeIndex < _medicalSupplies.GetLength(1); gradeIndex++)
            {
                var count = _recoveredMedicalSupplies[kindIndex, gradeIndex];
                if (count <= 0)
                {
                    continue;
                }
                destination.Add(new LootItem
                {
                    Kind = LootItemKind.Medical,
                    MedicalKind = (MedicalItemKind)kindIndex,
                    Quantity = count,
                    Grade = (LootGrade)gradeIndex
                });
            }
        }
        for (var index = 0; index < _armorPlateSupplies.Length; index++)
        {
            if (_recoveredArmorPlateSupplies[index] <= 0)
            {
                continue;
            }
            destination.Add(new LootItem
            {
                Kind = LootItemKind.ArmorPlate,
                Quantity = _recoveredArmorPlateSupplies[index],
                Grade = (LootGrade)index
            });
        }
    }

    internal void SetSustainmentStateForDiagnostics(
        float health,
        float armorRatio,
        int bandages,
        int plates)
    {
        CancelSustainmentAction(releaseLoot: true);
        Health = Mathf.Clamp(health, 1.0f, MaxHealth);
        _equippedBodyArmor.Durability = _equippedBodyArmor.Definition.MaxDurability
            * Mathf.Clamp(armorRatio, 0.0f, 1.0f);
        Array.Clear(_medicalSupplies);
        Array.Clear(_recoveredMedicalSupplies);
        Array.Clear(_armorPlateSupplies);
        Array.Clear(_recoveredArmorPlateSupplies);
        _medicalSupplies[(int)MedicalItemKind.Bandage, (int)LootGrade.Uncommon]
            = Math.Max(0, bandages);
        _armorPlateSupplies[(int)LootGrade.Uncommon] = Math.Max(0, plates);
        _sustainmentDecisionTimer = 0.0f;
        _sustainmentRecentDamageTimer = 0.0f;
        UpdateHealthVisual();
    }

    internal bool AdvanceSustainmentForDiagnostics(
        float delta,
        EnemyOperator? hostile = null)
    {
        UpdateSustainmentTimers(delta);
        return UpdateSustainment(delta, hostile);
    }

    internal void CompleteSustainmentActionForDiagnostics()
    {
        if (_sustainmentAction != SquadSustainmentActionKind.None)
        {
            _sustainmentActionRemaining = 0.0f;
            CompleteSustainmentAction();
        }
    }

    private int MedicalCount(MedicalItemKind kind)
    {
        var total = 0;
        for (var gradeIndex = 0; gradeIndex < _medicalSupplies.GetLength(1); gradeIndex++)
        {
            total += _medicalSupplies[(int)kind, gradeIndex];
        }
        return total;
    }

    private bool ConsumeMedical(MedicalItemKind kind)
    {
        for (var gradeIndex = 0; gradeIndex < _medicalSupplies.GetLength(1); gradeIndex++)
        {
            var total = _medicalSupplies[(int)kind, gradeIndex];
            if (total <= 0)
            {
                continue;
            }
            _medicalSupplies[(int)kind, gradeIndex]--;
            var recovered = _recoveredMedicalSupplies[(int)kind, gradeIndex];
            var baseline = total - recovered;
            if (baseline <= 0 && recovered > 0)
            {
                _recoveredMedicalSupplies[(int)kind, gradeIndex]--;
            }
            return true;
        }
        return false;
    }

    private bool TrySelectArmorPlate(out LootGrade grade)
    {
        var missingFraction = 1.0f - ArmorRatio;
        var fallback = -1;
        for (var index = 0; index < _armorPlateSupplies.Length; index++)
        {
            if (_armorPlateSupplies[index] <= 0)
            {
                continue;
            }
            fallback = index;
            if (ArmorPlateSupplies.RepairFraction((LootGrade)index) >= missingFraction - 0.02f)
            {
                grade = (LootGrade)index;
                return true;
            }
        }
        grade = fallback >= 0 ? (LootGrade)fallback : LootGrade.Common;
        return fallback >= 0;
    }

    private bool ConsumeArmorPlate(LootGrade grade)
    {
        if (_armorPlateSupplies[(int)grade] <= 0)
        {
            return false;
        }
        _armorPlateSupplies[(int)grade]--;
        var recovered = _recoveredArmorPlateSupplies[(int)grade];
        var baseline = _armorPlateSupplies[(int)grade] + 1 - recovered;
        if (baseline <= 0 && recovered > 0)
        {
            _recoveredArmorPlateSupplies[(int)grade]--;
        }
        return true;
    }

    private (EquipmentItem Item, LootGrade Grade) EquipmentForSlot(EquipmentSlot slot)
        => slot switch
        {
            EquipmentSlot.Helmet => (_equippedHelmet, _equippedHelmetGrade),
            EquipmentSlot.BodyArmor => (_equippedBodyArmor, _equippedBodyArmorGrade),
            _ => (_equippedBackpack, _equippedBackpackGrade)
        };

    private bool EquipmentRecoveredForSlot(EquipmentSlot slot)
        => slot switch
        {
            EquipmentSlot.Helmet => _equippedHelmetRecovered,
            EquipmentSlot.BodyArmor => _equippedBodyArmorRecovered,
            _ => _equippedBackpackRecovered
        };

    private int SustainmentSupplyCapacity
        => 3 + Math.Max(0, _equippedBackpack.Definition.CapacityBonus / 2);

    private int CountSustainmentSupplies()
    {
        var total = 0;
        for (var kindIndex = 0; kindIndex < _medicalSupplies.GetLength(0); kindIndex++)
        {
            for (var gradeIndex = 0;
                gradeIndex < _medicalSupplies.GetLength(1);
                gradeIndex++)
            {
                total += _medicalSupplies[kindIndex, gradeIndex];
            }
        }
        for (var index = 0; index < _armorPlateSupplies.Length; index++)
        {
            total += _armorPlateSupplies[index];
        }
        return total;
    }

    private int CurrentRecoveredSustainmentValue()
    {
        var items = new List<LootItem>();
        AppendSustainmentLoot(items);
        return LootItem.TotalValue(items);
    }

    private static void AppendEquipmentLoot(
        List<LootItem> destination,
        EquipmentItem equipment,
        LootGrade grade)
    {
        if (equipment.Definition.Id.EndsWith("_none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        destination.Add(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = equipment.Clone(),
            Grade = grade
        });
    }
}
