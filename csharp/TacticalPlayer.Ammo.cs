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
        _loadedAmmoGrade = LootGrade.Common;
    }

    private void SetAmmoReserve(AmmoCaliber caliber, LootGrade grade, int amount)
    {
        var key = (caliber, grade);
        var clamped = Mathf.Max(0, amount);
        if (clamped == 0)
        {
            _gradedAmmoReserves.Remove(key);
            return;
        }
        _gradedAmmoReserves[key] = clamped;
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

    public void SetAmmoGradeForDiagnostics(LootGrade grade, int reserve)
    {
        ResetAmmoReserves();
        _loadedAmmoGrade = grade;
        SetAmmoReserve(CurrentAmmoCaliber, grade, reserve);
        Hud?.SetAmmoTier(grade);
    }
}
