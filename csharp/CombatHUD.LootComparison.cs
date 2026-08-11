using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace OperationSteelTide;

public enum LootComparisonTone
{
    Neutral,
    Upgrade,
    Downgrade
}

public readonly record struct LootStatComparison(string Text, LootComparisonTone Tone);

public partial class CombatHUD
{
    public int LootComparisonCardCount
    {
        get
        {
            var count = 0;
            foreach (var card in VisibleLootCards())
            {
                if (card.ComparisonCount > 0)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public bool LootComparisonHasUpgrade
    {
        get
        {
            foreach (var card in VisibleLootCards())
            {
                if (card.HasUpgradeComparison)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public bool LootComparisonHasDowngrade
    {
        get
        {
            foreach (var card in VisibleLootCards())
            {
                if (card.HasDowngradeComparison)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public bool LootGradeColorsConsistent
    {
        get
        {
            var found = false;
            foreach (var card in VisibleLootCards())
            {
                found = true;
                if (!card.QualityColorMatchesGrade)
                {
                    return false;
                }
            }
            return found;
        }
    }

    private IReadOnlyList<LootStatComparison> BuildLootComparisons(LootItem item)
    {
        var comparisons = new List<LootStatComparison>(4);
        if (_shownPlayer is null)
        {
            return comparisons;
        }

        if (item.Kind == LootItemKind.Weapon && item.Weapon is not null)
        {
            if (!_shownPlayer.HasFireablePrimary)
            {
                comparisons.Add(new LootStatComparison(
                    $"\u25b2 {Text("comparison_new_primary", "NEW PRIMARY")}",
                    LootComparisonTone.Upgrade));
                return comparisons;
            }
            var candidate = item.Weapon.Stats();
            var equipped = _shownPlayer.CurrentWeaponStats;
            AddComparison(comparisons, Text("stat_damage", "DMG"), candidate.Damage, equipped.Damage, true, "0");
            AddComparison(comparisons, Text("stat_range", "RANGE"), candidate.EffectiveRange, equipped.EffectiveRange, true, "0", "m");
            AddComparison(comparisons, Text("stat_recoil", "RECOIL"), candidate.Recoil, equipped.Recoil, false, "0.00");
            AddComparison(comparisons, Text("stat_handling", "HANDLING"), candidate.Handling, equipped.Handling, true, "0.00");
            return comparisons;
        }

        if (item.Kind != LootItemKind.Equipment || item.Equipment is null)
        {
            return comparisons;
        }

        var incoming = item.Equipment;
        var current = incoming.Definition.Slot switch
        {
            EquipmentSlot.Helmet => _shownPlayer.EquippedHelmet,
            EquipmentSlot.BodyArmor => _shownPlayer.EquippedBodyArmor,
            EquipmentSlot.Backpack => _shownPlayer.EquippedBackpack,
            _ => null
        };
        if (current is null)
        {
            return comparisons;
        }

        if (incoming.Definition.Slot == EquipmentSlot.Backpack)
        {
            AddComparison(
                comparisons,
                Text("stat_capacity", "CAPACITY"),
                incoming.Definition.CapacityBonus,
                current.Definition.CapacityBonus,
                true,
                "0");
        }
        else
        {
            AddComparison(
                comparisons,
                Text("stat_protection", "PROTECTION"),
                incoming.Definition.Protection * 100.0f,
                current.Definition.Protection * 100.0f,
                true,
                "0",
                "%");
        }
        AddComparison(
            comparisons,
            Text("stat_durability", "DURABILITY"),
            incoming.Durability,
            current.Durability,
            true,
            "0");
        return comparisons;
    }

    private void RefreshEquippedQualityStyles()
    {
        if (_shownPlayer is null)
        {
            return;
        }
        StyleEquippedSlot(
            _primarySlot,
            _primarySlotCaption,
            Text("primary_weapon", "PRIMARY WEAPON"),
            _shownPlayer.HasFireablePrimary ? _shownPlayer.EquippedWeaponGrade : LootGrade.Common);
        StyleEquippedSlot(
            _helmetSlot,
            _helmetSlotCaption,
            Text("helmet", "HELMET"),
            _shownPlayer.EquippedHelmetGrade);
        StyleEquippedSlot(
            _armorSlot,
            _armorSlotCaption,
            Text("body_armor", "BODY ARMOR"),
            _shownPlayer.EquippedBodyArmorGrade);
        StyleEquippedSlot(
            _packSlot,
            _packSlotCaption,
            Text("backpack_container", "BACKPACK CONTAINER"),
            _shownPlayer.EquippedBackpackGrade);
    }

    private void StyleEquippedSlot(LootDropZone zone, Label caption, string slotName, LootGrade grade)
    {
        var color = LootGrades.GlowColor(grade);
        zone.AddThemeStyleboxOverride("panel", LootDropZone.ZoneStyle(color));
        caption.Text = $"{slotName}  //  {LootGrades.DisplayName(grade, _language)}";
        caption.AddThemeColorOverride("font_color", color);
    }

    private IEnumerable<LootDragCard> VisibleLootCards()
    {
        if (IsInstanceValid(_lootSourceList))
        {
            foreach (var child in _lootSourceList.GetChildren())
            {
                if (child is LootDragCard card)
                {
                    yield return card;
                }
            }
        }
        if (IsInstanceValid(_backpackList))
        {
            foreach (var child in _backpackList.GetChildren())
            {
                if (child is LootDragCard card)
                {
                    yield return card;
                }
            }
        }
    }

    private static void AddComparison(
        ICollection<LootStatComparison> comparisons,
        string label,
        float candidate,
        float equipped,
        bool higherIsBetter,
        string numberFormat,
        string suffix = "")
    {
        var delta = candidate - equipped;
        if (Mathf.Abs(delta) < 0.005f)
        {
            comparisons.Add(new LootStatComparison($"= {label}", LootComparisonTone.Neutral));
            return;
        }

        var increased = delta > 0.0f;
        var arrow = increased ? "\u25b2" : "\u25bc";
        var sign = increased ? "+" : "-";
        var amount = Mathf.Abs(delta).ToString(numberFormat, CultureInfo.InvariantCulture);
        var tone = increased == higherIsBetter ? LootComparisonTone.Upgrade : LootComparisonTone.Downgrade;
        comparisons.Add(new LootStatComparison($"{arrow} {label} {sign}{amount}{suffix}", tone));
    }
}
