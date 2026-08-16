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
                if (card.RenderedComparisonCount > 0)
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
                if (card.RenderedHasUpgradeComparison)
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
                if (card.RenderedHasDowngradeComparison)
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

    public bool LootComparisonsFullyRendered
    {
        get
        {
            var found = false;
            foreach (var card in VisibleLootCards())
            {
                if (card.ComparisonCount == 0)
                {
                    continue;
                }
                found = true;
                if (card.RenderedComparisonCount != card.ComparisonCount)
                {
                    return false;
                }
            }
            return found;
        }
    }

    public bool LootCompactWeaponComparisonsFullyRendered
    {
        get
        {
            if (!_shownSourceAvailable)
            {
                return false;
            }
            foreach (var card in VisibleLootCards())
            {
                if (card.Origin == LootDragOrigin.Backpack && card.ItemKind == LootItemKind.Weapon)
                {
                    return card.ComparisonCount == 4 && card.RenderedComparisonCount == 4;
                }
            }
            return false;
        }
    }

    public bool LootCompactWeaponShowsBothDirections
    {
        get
        {
            if (!_shownSourceAvailable)
            {
                return false;
            }
            foreach (var card in VisibleLootCards())
            {
                if (card.Origin == LootDragOrigin.Backpack && card.ItemKind == LootItemKind.Weapon)
                {
                    return card.RenderedHasUpgradeComparison && card.RenderedHasDowngradeComparison;
                }
            }
            return false;
        }
    }

    public bool LootAttachmentComparisonRendered
    {
        get
        {
            foreach (var card in VisibleLootCards())
            {
                if (card.ItemKind == LootItemKind.Attachment)
                {
                    return card.ComparisonCount == 4 && card.RenderedComparisonCount == 4;
                }
            }
            return false;
        }
    }

    public bool LootEquippedGradeStylesConsistent
    {
        get
        {
            if (_shownPlayer is null)
            {
                return false;
            }
            return IsInstanceValid(_lootWeaponRack)
                && _lootWeaponRack.GradeStylesConsistent
                && ZoneColorMatchesGrade(_helmetSlot, _shownPlayer.EquippedHelmetGrade)
                && ZoneColorMatchesGrade(_armorSlot, _shownPlayer.EquippedBodyArmorGrade)
                && ZoneColorMatchesGrade(_packSlot, _shownPlayer.EquippedBackpackGrade);
        }
    }

    public bool LootEmptyPrimaryGradeHidden => _shownPlayer is { HasFireablePrimary: false }
        && IsInstanceValid(_lootWeaponRack)
        && _lootWeaponRack.EmptyCaptionsHaveNoGrade;

    public Vector2 LootSourceZoneSizeForDiagnostics => _lootSourceZone.Size;
    public Vector2 LootBackpackZoneSizeForDiagnostics => _backpackZone.Size;
    public bool LootSourceAvailableForDiagnostics => _shownSourceAvailable;
    public string LootSourceCardWidthsForDiagnostics
    {
        get
        {
            var result = string.Empty;
            if (!IsInstanceValid(_lootSourceList))
            {
                return result;
            }
            foreach (var child in _lootSourceList.GetChildren())
            {
                if (child is LootDragCard card)
                {
                    result += $"{card.ItemKind}:{card.Size.X:0}/{card.GetCombinedMinimumSize().X:0} ";
                }
            }
            return result.TrimEnd();
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
            AddWeaponComparisons(comparisons, item.Weapon.Stats(), _shownPlayer.CurrentWeaponStats);
            return comparisons;
        }

        if (item.Kind == LootItemKind.Attachment && !string.IsNullOrEmpty(item.AttachmentId))
        {
            if (!_shownPlayer.HasFireablePrimary)
            {
                return comparisons;
            }
            var attachment = WeaponCatalog.Attachment(item.AttachmentId);
            var candidate = _shownPlayer.EquippedWeapon.Clone();
            candidate.Attachments[attachment.Slot] = attachment.Id;
            AddWeaponComparisons(comparisons, candidate.Stats(), _shownPlayer.CurrentWeaponStats);
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

    private static bool ZoneColorMatchesGrade(LootDropZone zone, LootGrade grade)
    {
        var expected = new Color(LootGrades.GlowColor(grade), 0.75f);
        return zone.GetThemeStylebox("panel") is StyleBoxFlat style
            && ColorsMatch(style.BorderColor, expected);
    }

    private static bool ColorsMatch(Color actual, Color expected)
    {
        return Mathf.IsEqualApprox(actual.R, expected.R)
            && Mathf.IsEqualApprox(actual.G, expected.G)
            && Mathf.IsEqualApprox(actual.B, expected.B)
            && Mathf.IsEqualApprox(actual.A, expected.A);
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

    private void AddWeaponComparisons(
        ICollection<LootStatComparison> comparisons,
        WeaponStats candidate,
        WeaponStats equipped)
    {
        AddComparison(comparisons, Text("stat_damage", "DMG"), candidate.Damage, equipped.Damage, true, "0");
        AddComparison(comparisons, Text("stat_range", "RANGE"), candidate.EffectiveRange, equipped.EffectiveRange, true, "0", "m");
        AddComparison(comparisons, Text("stat_recoil", "RECOIL"), candidate.Recoil, equipped.Recoil, false, "0.00");
        AddComparison(comparisons, Text("stat_handling", "HANDLING"), candidate.Handling, equipped.Handling, true, "0.00");
    }
}
