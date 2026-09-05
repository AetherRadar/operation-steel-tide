using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private enum LootSortMode
    {
        Value,
        Grade,
        Category,
        Default
    }

    private LootSortMode CurrentLootSortMode
        => (LootSortMode)Mathf.Clamp(_lootSortMode, 0, 3);

    private IEnumerable<LootItem> OrderedBackpackItems(IReadOnlyList<LootItem> items)
    {
        if (CurrentLootSortMode == LootSortMode.Default)
        {
            foreach (var item in items)
            {
                yield return item;
            }
            yield break;
        }

        var ordered = new List<LootItem>(items.Count);
        foreach (var item in items)
        {
            ordered.Add(item);
        }
        ordered.Sort((left, right) =>
        {
            var result = CurrentLootSortMode switch
            {
                LootSortMode.Value => right.StackValue.CompareTo(left.StackValue),
                LootSortMode.Grade => right.Grade.CompareTo(left.Grade),
                LootSortMode.Category => CategoryRank(left.Kind).CompareTo(CategoryRank(right.Kind)),
                _ => 0
            };
            if (result != 0)
            {
                return result;
            }
            result = right.Grade.CompareTo(left.Grade);
            return result != 0
                ? result
                : string.Compare(
                    left.DisplayName(_language),
                    right.DisplayName(_language),
                    StringComparison.OrdinalIgnoreCase);
        });
        foreach (var item in ordered)
        {
            yield return item;
        }
    }

    private static int CategoryRank(LootItemKind kind) => kind switch
    {
        LootItemKind.Weapon => 0,
        LootItemKind.Attachment => 1,
        LootItemKind.Equipment => 2,
        LootItemKind.ArmorPlate => 3,
        LootItemKind.Medical => 4,
        LootItemKind.Ammunition => 5,
        LootItemKind.KnifeSkin => 6,
        LootItemKind.Valuable => 7,
        _ => 8
    };

    private void CycleLootSort()
    {
        if (!IsLootVisible)
        {
            return;
        }
        _lootSortMode = (_lootSortMode + 1) % 4;
        RefreshLootOverlay();
    }

    private void TransferAllLootToBackpack()
    {
        if (!IsLootVisible || !_shownSourceAvailable || _shownLoot is null)
        {
            return;
        }

        // Snapshot IDs before emitting signals: the world refreshes the source
        // after each successful transfer, so enumerating the live List directly
        // would skip items as it shrinks.
        var itemIds = new List<string>(_shownLoot.Count);
        foreach (var item in _shownLoot)
        {
            itemIds.Add(item.Id);
        }
        foreach (var itemId in itemIds)
        {
            EmitSignal(SignalName.LootTakeRequested, itemId);
        }
    }

    private void UpdateLootToolbarPresentation()
    {
        if (!IsInstanceValid(_lootSortButton)
            || !IsInstanceValid(_lootTakeAllButton)
            || !IsInstanceValid(_lootHint))
        {
            return;
        }

        var sortText = CurrentLootSortMode switch
        {
            LootSortMode.Grade => Text("sort_grade", "SORT: GRADE"),
            LootSortMode.Category => Text("sort_category", "SORT: TYPE"),
            LootSortMode.Default => Text("sort_default", "SORT: FOUND"),
            _ => Text("sort_value", "SORT: VALUE")
        };
        _lootSortButton.Text = sortText;
        _lootTakeAllButton.Text = Text("take_all", "TAKE ALL");
        _lootTakeAllButton.Visible = _shownSourceAvailable && _shownLoot is { Count: > 0 };
        _lootHint.Text = Text(
            "loot_hint",
            "CLICK ITEM  //  ACTIONS     DRAG  //  MOVE     DOUBLE-CLICK  //  QUICK EQUIP");
    }
}
