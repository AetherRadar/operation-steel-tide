using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private readonly Dictionary<(AmmoCaliber Caliber, LootGrade Grade), int> _gradedAmmoReserves = new()
    {
        [(AmmoCaliber.Rifle, LootGrade.Common)] = 150
    };
    private LootGrade _loadedAmmoGrade = LootGrade.Common;

    public LootGrade CurrentAmmoGrade => _loadedAmmoGrade;

    public int AmmoReserveFor(AmmoCaliber caliber, LootGrade grade)
        => _gradedAmmoReserves.TryGetValue((caliber, grade), out var amount) ? amount : 0;

    private void ResetAmmoReserves()
    {
        _gradedAmmoReserves.Clear();
        Backpack.RemoveAll(item => item.Kind == LootItemKind.Ammunition);
        _loadedAmmoGrade = LootGrade.Common;
    }

    private void SetAmmoReserve(AmmoCaliber caliber, LootGrade grade, int amount)
    {
        var key = (caliber, grade);
        var clamped = Mathf.Max(0, amount);
        if (clamped == 0)
        {
            _gradedAmmoReserves.Remove(key);
            Backpack.RemoveAll(item => item.Kind == LootItemKind.Ammunition
                && item.AmmoCaliber == caliber
                && item.Grade == grade);
            return;
        }
        _gradedAmmoReserves[key] = clamped;
        var stack = Backpack.Find(item => item.Kind == LootItemKind.Ammunition
            && item.AmmoCaliber == caliber
            && item.Grade == grade);
        if (stack is not null)
        {
            stack.Quantity = clamped;
            return;
        }
        Backpack.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = caliber,
            Grade = grade,
            Quantity = clamped
        });
    }

    private LootGrade BestAmmoGrade(AmmoCaliber caliber)
    {
        for (var tier = (int)LootGrade.Legendary; tier >= (int)LootGrade.Common; tier--)
        {
            var grade = (LootGrade)tier;
            if (AmmoReserveFor(caliber, grade) > 0)
            {
                return grade;
            }
        }
        return LootGrade.Common;
    }

    private int ConsumeAmmoReserve(AmmoCaliber caliber, LootGrade grade, int requested)
    {
        var available = AmmoReserveFor(caliber, grade);
        var consumed = Mathf.Min(Mathf.Max(0, requested), available);
        SetAmmoReserve(caliber, grade, available - consumed);
        return consumed;
    }

    private static int MaximumAmmoReserve(AmmoCaliber caliber) => caliber switch
    {
        AmmoCaliber.Magnum338 => 40,
        AmmoCaliber.Sniper => 60,
        AmmoCaliber.Smg => 270,
        _ => 210
    };

    private bool CanAddAmmoStack(AmmoCaliber caliber, LootGrade grade)
        => Backpack.Exists(item => item.Kind == LootItemKind.Ammunition
            && item.AmmoCaliber == caliber
            && item.Grade == grade)
        || Backpack.Count < BackpackCapacity;

    private bool TryStoreAmmoStack(LootItem item)
    {
        var requested = Mathf.Max(1, item.Quantity);
        var available = MaximumAmmoReserve(item.AmmoCaliber) - AmmoReserveFor(item.AmmoCaliber);
        if (available < requested || !CanAddAmmoStack(item.AmmoCaliber, item.Grade))
        {
            Hud?.ShowLocalizedMessage(
                available < requested ? "ammo_full" : "backpack_full",
                available < requested ? "AMMUNITION RESERVE FULL" : "BACKPACK FULL",
                new Color(1.0f, 0.48f, 0.28f));
            return false;
        }

        var stack = Backpack.Find(candidate => candidate.Kind == LootItemKind.Ammunition
            && candidate.AmmoCaliber == item.AmmoCaliber
            && candidate.Grade == item.Grade);
        if (stack is null)
        {
            item.Quantity = requested;
            Backpack.Add(item);
        }
        SetAmmoReserve(
            item.AmmoCaliber,
            item.Grade,
            AmmoReserveFor(item.AmmoCaliber, item.Grade) + requested);
        Hud?.ShowLocalizedMessage("ammo_recovered", "AMMUNITION RECOVERED", new Color(0.42f, 0.9f, 0.64f));
        Hud?.SetBackpackValuePlayer(this);
        return true;
    }

    public bool TryRemoveBackpackItem(string itemId, out LootItem item)
    {
        var index = Backpack.FindIndex(candidate => candidate.Id == itemId);
        if (index < 0)
        {
            item = null!;
            return false;
        }

        item = Backpack[index];
        Backpack.RemoveAt(index);
        if (item.Kind == LootItemKind.Ammunition)
        {
            // The visible stack is the complete reserve for this caliber/grade.
            // Removing it from the backpack must remove that reserve as well.
            SetAmmoReserve(item.AmmoCaliber, item.Grade, 0);
        }
        Hud?.SetBackpackValuePlayer(this);
        Hud?.SetMedicalInventory(this);
        return true;
    }

    public void ClearBackpackForDiagnostics()
    {
        Backpack.Clear();
        _gradedAmmoReserves.Clear();
        _loadedAmmoGrade = LootGrade.Common;
    }

    public void SetAmmoGradeForDiagnostics(LootGrade grade, int reserve)
    {
        ResetAmmoReserves();
        _loadedAmmoGrade = grade;
        SetAmmoReserve(CurrentAmmoCaliber, grade, reserve);
        Hud?.SetAmmoTier(grade);
    }

    public bool ReloadImmediatelyForDiagnostics(int magazineAmmo)
    {
        Ammo = Mathf.Clamp(magazineAmmo, 0, EquippedWeapon.Stats().MagazineSize);
        StartReload();
        if (!_isReloading)
        {
            return false;
        }
        FinishReload();
        return true;
    }
}
