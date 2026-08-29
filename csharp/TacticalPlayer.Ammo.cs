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
        AmmoCaliber.Pistol => 180,
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

    private void CancelReload()
    {
        if (!_isReloading)
        {
            return;
        }

        _isReloading = false;
        _reloadTime = 0.0f;
        _activeReloadDuration = 0.0f;
        _reloadSoundStage = 0;
        ResetReloadRig();
    }

    private void UpdateReloadTimer(float delta)
    {
        if (!_isReloading)
        {
            return;
        }

        _reloadTime -= delta;
        if (_reloadTime <= 0.0f)
        {
            FinishReload();
        }
    }

    internal bool SetReloadPoseForDiagnostics(float progress, bool emptyReload = false)
    {
        if (EquippedWeapon.Platform != WeaponPlatform.M3A1
            && !UsesPlatformReloadPresentation()
            && !UsesSidearmReloadPresentation())
        {
            return false;
        }
        var normalizedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
        _reloadStartedEmpty = emptyReload;
        _isReloading = false;
        _isAiming = false;
        _activeReloadDuration = ReloadDuration * RoleReloadMultiplier;
        _reloadTime = _activeReloadDuration;
        _weaponRoot.Position = WeaponViewPositionTarget();
        _weaponRoot.Rotation = WeaponViewRotationTarget();
        _isReloading = true;

        // The auditors disable player processing to keep every mechanism
        // sample deterministic. Recreate the complete runtime viewmodel path
        // with the same response function instead of snapping to a target that
        // moving gameplay would only approach. Fixed 60 Hz steps match the
        // physics presentation loop and preserve its staging lag on the way
        // into and back out of the reload workspace.
        const float simulationStep = 1.0f / 60.0f;
        var handling = EquippedWeapon.Stats().Handling;
        UpdateWeaponViewPose(simulationStep, handling);
        var lastVisibleProgress = LastVisibleReloadProgressForDiagnostics;
        var viewProgress = Mathf.Min(normalizedProgress, lastVisibleProgress);
        var targetElapsed = _activeReloadDuration * viewProgress;
        var elapsed = 0.0f;
        while (elapsed < targetElapsed)
        {
            var step = Mathf.Min(simulationStep, targetElapsed - elapsed);
            elapsed += step;
            _reloadTime = _activeReloadDuration - elapsed;
            UpdateWeaponViewPose(step, handling);
        }
        _reloadTime = _activeReloadDuration * (1.0f - normalizedProgress);
        UpdateReloadAnimation();
        SyncAuthoredPrimaryWeapon();
        UpdateAuthoredM4ReloadSupportArm();
        return true;
    }

    internal void ClearReloadPoseForDiagnostics()
    {
        _isReloading = false;
        _reloadTime = 0.0f;
        _activeReloadDuration = 0.0f;
        ResetReloadRig();
        UpdateWeaponViewPose(1.0f / 60.0f, EquippedWeapon.Stats().Handling);
        ApplyProceduralHandPose();
        SyncAuthoredPrimaryWeapon();
        UpdateAuthoredM4ReloadSupportArm();
    }

    internal float LastVisibleReloadProgressForDiagnostics
    {
        get
        {
            const float simulationStep = 1.0f / 60.0f;
            var duration = ReloadDuration * RoleReloadMultiplier;
            var remaining = duration;
            while (remaining - simulationStep > 0.0f)
            {
                remaining -= simulationStep;
            }
            return duration > 0.0f
                ? Mathf.Clamp(1.0f - remaining / duration, 0.0f, 1.0f)
                : 0.0f;
        }
    }
}
