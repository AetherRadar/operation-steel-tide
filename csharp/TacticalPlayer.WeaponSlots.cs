using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum PlayerWeaponSlot
{
    Primary = 0,
    Secondary = 1,
    Sidearm = 2,
    Melee = 3
}

public partial class TacticalPlayer
{
    private WeaponBuild? _primaryWeaponSlot = WeaponCatalog.StarterWeapon();
    private WeaponBuild? _secondaryWeaponSlot;
    private WeaponBuild? _sidearmWeaponSlot;
    private LootGrade _primaryWeaponSlotGrade = LootGrade.Rare;
    private LootGrade _secondaryWeaponSlotGrade = LootGrade.Uncommon;
    private LootGrade _sidearmWeaponSlotGrade = LootGrade.Uncommon;
    private readonly Dictionary<AttachmentSlot, LootGrade> _primaryAttachmentGrades = new();
    private readonly Dictionary<AttachmentSlot, LootGrade> _secondaryAttachmentGrades = new();
    private readonly Dictionary<AttachmentSlot, LootGrade> _sidearmAttachmentGrades = new();
    private int _primaryMagazineAmmo = 30;
    private int _secondaryMagazineAmmo;
    private int _sidearmMagazineAmmo;
    private LootGrade _primaryLoadedAmmoGrade = LootGrade.Common;
    private LootGrade _secondaryLoadedAmmoGrade = LootGrade.Common;
    private LootGrade _sidearmLoadedAmmoGrade = LootGrade.Common;
    private PlayerWeaponSlot _activeWeaponSlot = PlayerWeaponSlot.Primary;

    public PlayerWeaponSlot ActiveWeaponSlot => _activeWeaponSlot;
    public bool HasSecondaryWeapon => _secondaryWeaponSlot is not null;
    public bool HasSidearmWeapon => _sidearmWeaponSlot is not null;
    public WeaponPlatform? SecondaryWeaponPlatform => _secondaryWeaponSlot?.Platform;
    public WeaponPlatform? SidearmWeaponPlatform => _sidearmWeaponSlot?.Platform;
    public WeaponBuild? PrimaryWeaponBuild => WeaponSnapshotForSlot(PlayerWeaponSlot.Primary);
    public WeaponBuild? SecondaryWeaponBuild => WeaponSnapshotForSlot(PlayerWeaponSlot.Secondary);
    public WeaponBuild? SidearmWeaponBuild => WeaponSnapshotForSlot(PlayerWeaponSlot.Sidearm);
    internal WeaponBuild? PrimaryWeaponForHud => WeaponViewForSlot(PlayerWeaponSlot.Primary);
    internal WeaponBuild? SecondaryWeaponForHud => WeaponViewForSlot(PlayerWeaponSlot.Secondary);
    internal WeaponBuild? SidearmWeaponForHud => WeaponViewForSlot(PlayerWeaponSlot.Sidearm);
    public LootGrade PrimaryWeaponGrade => _primaryWeaponSlotGrade;
    public LootGrade SecondaryWeaponGrade => _secondaryWeaponSlotGrade;
    public LootGrade SidearmWeaponGrade => _sidearmWeaponSlotGrade;
    public int PrimaryMagazineAmmo => MagazineAmmoForSlot(PlayerWeaponSlot.Primary);
    public int SecondaryMagazineAmmo => MagazineAmmoForSlot(PlayerWeaponSlot.Secondary);
    public int SidearmMagazineAmmo => MagazineAmmoForSlot(PlayerWeaponSlot.Sidearm);

    private void ClearWeaponSlotsForColdStart()
    {
        _primaryWeaponSlot = null;
        _secondaryWeaponSlot = null;
        _sidearmWeaponSlot = null;
        _primaryMagazineAmmo = 0;
        _secondaryMagazineAmmo = 0;
        _sidearmMagazineAmmo = 0;
        _primaryAttachmentGrades.Clear();
        _secondaryAttachmentGrades.Clear();
        _sidearmAttachmentGrades.Clear();
        HasFireablePrimary = false;
    }

    private void InstallPrimaryWeapon(WeaponBuild build, LootGrade grade)
    {
        StoreActiveFirearmState();
        SetWeaponSlot(PlayerWeaponSlot.Primary, build, grade);
        HasFireablePrimary = true;
        ActivateWeaponSlot(PlayerWeaponSlot.Primary, true, true, false);
    }

    private void InstallSecondaryWeapon(WeaponBuild? build, LootGrade grade)
    {
        StoreActiveFirearmState();
        SetWeaponSlot(PlayerWeaponSlot.Secondary, build, grade);
        RefreshOrLeaveRemovedSlot(PlayerWeaponSlot.Secondary);
    }

    private void InstallSidearmWeapon(WeaponBuild? build, LootGrade grade)
    {
        StoreActiveFirearmState();
        SetWeaponSlot(PlayerWeaponSlot.Sidearm, build, grade);
        RefreshOrLeaveRemovedSlot(PlayerWeaponSlot.Sidearm);
    }

    private LootItem? EquipLootWeapon(
        WeaponBuild build,
        LootGrade grade,
        PlayerWeaponSlot? requestedSlot = null)
    {
        var target = requestedSlot ?? (WeaponCatalog.IsSidearm(build.Platform)
            ? PlayerWeaponSlot.Sidearm
            : EmptyLongGunSlot());
        var previousBuild = WeaponBuildForSlot(target)?.Clone();
        var previousGrade = WeaponGradeForSlot(target);
        switch (target)
        {
            case PlayerWeaponSlot.Primary:
                InstallPrimaryWeapon(build, grade);
                break;
            case PlayerWeaponSlot.Secondary:
                InstallSecondaryWeapon(build, grade);
                ActivateWeaponSlot(PlayerWeaponSlot.Secondary, true, true, false);
                break;
            case PlayerWeaponSlot.Sidearm:
                InstallSidearmWeapon(build, grade);
                ActivateWeaponSlot(PlayerWeaponSlot.Sidearm, true, true, false);
                break;
        }
        return previousBuild is null
            ? null
            : new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = previousBuild,
                Grade = previousGrade
            };
    }

    private static bool WeaponFitsSlot(WeaponPlatform platform, PlayerWeaponSlot slot)
        => WeaponCatalog.IsSidearm(platform)
            ? slot == PlayerWeaponSlot.Sidearm
            : slot is PlayerWeaponSlot.Primary or PlayerWeaponSlot.Secondary;

    private LootItem? EquipAttachmentToWeaponSlot(LootItem item, PlayerWeaponSlot slot)
    {
        var build = WeaponBuildForSlot(slot);
        if (build is null || slot == PlayerWeaponSlot.Primary && !HasFireablePrimary)
        {
            return item;
        }
        var attachment = WeaponCatalog.Attachment(item.AttachmentId);
        if (!WeaponCatalog.CanEquipAttachment(build.Platform, attachment.Id))
        {
            ShowIncompatibleAttachmentMessage();
            return item;
        }
        var grades = AttachmentGradesForSlot(slot);
        LootItem? previous = null;
        if (build.Attachments.TryGetValue(attachment.Slot, out var previousId))
        {
            previous = new LootItem
            {
                Kind = LootItemKind.Attachment,
                AttachmentId = previousId,
                Grade = grades.GetValueOrDefault(attachment.Slot, WeaponGradeForSlot(slot))
            };
        }
        if (!_knifeEquipped && _activeWeaponSlot == slot)
        {
            EquippedWeapon.Attachments[attachment.Slot] = attachment.Id;
            _equippedAttachmentGrades[attachment.Slot] = item.Grade;
            StoreActiveFirearmState();
            ApplyWeaponBuildVisuals();
        }
        else
        {
            build.Attachments[attachment.Slot] = attachment.Id;
            grades[attachment.Slot] = item.Grade;
        }
        Hud?.ShowLocalizedMessage(
            "part_installed",
            "WEAPON PART INSTALLED",
            new Color(0.42f, 0.9f, 0.72f));
        return previous;
    }

    private PlayerWeaponSlot EmptyLongGunSlot()
    {
        if (_primaryWeaponSlot is null || !HasFireablePrimary)
        {
            return PlayerWeaponSlot.Primary;
        }
        if (_secondaryWeaponSlot is null)
        {
            return PlayerWeaponSlot.Secondary;
        }
        return _activeWeaponSlot is PlayerWeaponSlot.Primary or PlayerWeaponSlot.Secondary
            ? _activeWeaponSlot
            : PlayerWeaponSlot.Secondary;
    }

    private void SetWeaponSlot(PlayerWeaponSlot slot, WeaponBuild? build, LootGrade grade)
    {
        var clone = build is null ? null : WeaponCatalog.NormalizeBuild(build);
        var magazine = clone?.Stats().MagazineSize ?? 0;
        var loadedGrade = clone is null
            ? LootGrade.Common
            : BestAmmoGrade(WeaponCatalog.Weapon(clone.Platform).Caliber);
        switch (slot)
        {
            case PlayerWeaponSlot.Primary:
                _primaryWeaponSlot = clone;
                _primaryWeaponSlotGrade = grade;
                _primaryMagazineAmmo = magazine;
                _primaryLoadedAmmoGrade = loadedGrade;
                CopyAttachmentGrades(_primaryAttachmentGrades, clone, grade);
                break;
            case PlayerWeaponSlot.Secondary:
                _secondaryWeaponSlot = clone;
                _secondaryWeaponSlotGrade = grade;
                _secondaryMagazineAmmo = magazine;
                _secondaryLoadedAmmoGrade = loadedGrade;
                CopyAttachmentGrades(_secondaryAttachmentGrades, clone, grade);
                break;
            case PlayerWeaponSlot.Sidearm:
                _sidearmWeaponSlot = clone;
                _sidearmWeaponSlotGrade = grade;
                _sidearmMagazineAmmo = magazine;
                _sidearmLoadedAmmoGrade = loadedGrade;
                CopyAttachmentGrades(_sidearmAttachmentGrades, clone, grade);
                break;
        }
    }

    private void RefreshOrLeaveRemovedSlot(PlayerWeaponSlot slot)
    {
        if (_activeWeaponSlot != slot)
        {
            return;
        }
        if (WeaponBuildForSlot(slot) is not null)
        {
            ActivateWeaponSlot(slot, false, true, false);
            return;
        }
        ActivateWeaponSlot(PreferredFirearmOrMelee(), false, true, false);
    }

    private void StoreActiveFirearmState()
    {
        if (_knifeEquipped)
        {
            return;
        }
        switch (_activeWeaponSlot)
        {
            case PlayerWeaponSlot.Primary when _primaryWeaponSlot is not null:
                _primaryWeaponSlot = EquippedWeapon.Clone();
                _primaryWeaponSlotGrade = EquippedWeaponGrade;
                _primaryMagazineAmmo = Ammo;
                _primaryLoadedAmmoGrade = _loadedAmmoGrade;
                CopyAttachmentGrades(_primaryAttachmentGrades, _equippedAttachmentGrades);
                break;
            case PlayerWeaponSlot.Secondary when _secondaryWeaponSlot is not null:
                _secondaryWeaponSlot = EquippedWeapon.Clone();
                _secondaryWeaponSlotGrade = EquippedWeaponGrade;
                _secondaryMagazineAmmo = Ammo;
                _secondaryLoadedAmmoGrade = _loadedAmmoGrade;
                CopyAttachmentGrades(_secondaryAttachmentGrades, _equippedAttachmentGrades);
                break;
            case PlayerWeaponSlot.Sidearm when _sidearmWeaponSlot is not null:
                _sidearmWeaponSlot = EquippedWeapon.Clone();
                _sidearmWeaponSlotGrade = EquippedWeaponGrade;
                _sidearmMagazineAmmo = Ammo;
                _sidearmLoadedAmmoGrade = _loadedAmmoGrade;
                CopyAttachmentGrades(_sidearmAttachmentGrades, _equippedAttachmentGrades);
                break;
        }
    }

    private bool ActivateWeaponSlot(
        PlayerWeaponSlot slot,
        bool notify,
        bool force = false,
        bool storeCurrent = true)
    {
        if (_isPlating)
        {
            return false;
        }
        var melee = slot == PlayerWeaponSlot.Melee;
        var build = melee ? null : WeaponBuildForSlot(slot);
        if (!melee && (build is null || slot == PlayerWeaponSlot.Primary && !HasFireablePrimary))
        {
            return false;
        }
        if (!force
            && _activeWeaponSlot == slot
            && _activeQuickSlot == (PlayerQuickSlot)(int)slot
            && melee == _knifeEquipped)
        {
            return true;
        }
        CancelReload();
        if (storeCurrent)
        {
            StoreActiveFirearmState();
        }

        _activeWeaponSlot = slot;
        _activeQuickSlot = (PlayerQuickSlot)(int)slot;
        _returnQuickSlot = _activeQuickSlot;
        _knifeEquipped = melee;
        _isAiming = false;
        CancelMeleeAction();
        if (melee)
        {
            BeginMeleeDraw();
        }
        if (!melee)
        {
            EquippedWeapon = build!.Clone();
            EquippedWeaponGrade = WeaponGradeForSlot(slot);
            Ammo = StoredMagazineAmmoForSlot(slot);
            _loadedAmmoGrade = LoadedAmmoGradeForSlot(slot);
            _automaticFire = WeaponCatalog.Weapon(EquippedWeapon.Platform).SupportsAutomatic;
            _equippedAttachmentGrades.Clear();
            CopyAttachmentGrades(_equippedAttachmentGrades, AttachmentGradesForSlot(slot));
            ApplyWeaponBuildVisuals();
        }
        UpdateHeldItemVisibility();

        if (notify)
        {
            var (key, english) = slot switch
            {
                PlayerWeaponSlot.Secondary => ("secondary_ready", "SECONDARY WEAPON READY"),
                PlayerWeaponSlot.Sidearm => ("sidearm_ready", "SIDEARM READY"),
                PlayerWeaponSlot.Melee => ("melee_ready", "MELEE WEAPON READY"),
                _ => ("primary_ready", "PRIMARY WEAPON READY")
            };
            Hud?.ShowLocalizedMessage(key, english, new Color(0.42f, 0.9f, 0.73f));
        }
        return true;
    }

    private void CycleWeaponSlots()
    {
        const int quickSlotCount = (int)PlayerQuickSlot.Utility + 1;
        var start = (int)_activeQuickSlot;
        for (var offset = 1; offset <= quickSlotCount; offset++)
        {
            var candidate = (PlayerQuickSlot)((start + offset) % quickSlotCount);
            if (SelectQuickSlot(candidate, true))
            {
                return;
            }
        }
    }

    private PlayerWeaponSlot PreferredFirearmOrMelee()
    {
        if (HasFireablePrimary && _primaryWeaponSlot is not null)
        {
            return PlayerWeaponSlot.Primary;
        }
        if (_secondaryWeaponSlot is not null)
        {
            return PlayerWeaponSlot.Secondary;
        }
        return _sidearmWeaponSlot is not null ? PlayerWeaponSlot.Sidearm : PlayerWeaponSlot.Melee;
    }

    private WeaponBuild? WeaponBuildForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Primary => _primaryWeaponSlot,
        PlayerWeaponSlot.Secondary => _secondaryWeaponSlot,
        PlayerWeaponSlot.Sidearm => _sidearmWeaponSlot,
        _ => null
    };

    private WeaponBuild? WeaponSnapshotForSlot(PlayerWeaponSlot slot)
    {
        return WeaponViewForSlot(slot)?.Clone();
    }

    private WeaponBuild? WeaponViewForSlot(PlayerWeaponSlot slot)
    {
        var stored = WeaponBuildForSlot(slot);
        return stored is null
            ? null
            : !_knifeEquipped && _activeWeaponSlot == slot ? EquippedWeapon : stored;
    }

    private LootGrade WeaponGradeForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Secondary => _secondaryWeaponSlotGrade,
        PlayerWeaponSlot.Sidearm => _sidearmWeaponSlotGrade,
        _ => _primaryWeaponSlotGrade
    };

    private int StoredMagazineAmmoForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Secondary => _secondaryMagazineAmmo,
        PlayerWeaponSlot.Sidearm => _sidearmMagazineAmmo,
        _ => _primaryMagazineAmmo
    };

    private int MagazineAmmoForSlot(PlayerWeaponSlot slot)
        => _activeWeaponSlot == slot && !_knifeEquipped ? Ammo : StoredMagazineAmmoForSlot(slot);

    private LootGrade LoadedAmmoGradeForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Secondary => _secondaryLoadedAmmoGrade,
        PlayerWeaponSlot.Sidearm => _sidearmLoadedAmmoGrade,
        _ => _primaryLoadedAmmoGrade
    };

    private Dictionary<AttachmentSlot, LootGrade> AttachmentGradesForSlot(PlayerWeaponSlot slot) => slot switch
    {
        PlayerWeaponSlot.Secondary => _secondaryAttachmentGrades,
        PlayerWeaponSlot.Sidearm => _sidearmAttachmentGrades,
        _ => _primaryAttachmentGrades
    };

    internal void SetMagazineAmmoForDiagnostics(int amount)
    {
        if (_knifeEquipped)
        {
            return;
        }
        Ammo = Mathf.Clamp(amount, 0, EquippedWeapon.Stats().MagazineSize);
    }

    private static void CopyAttachmentGrades(
        Dictionary<AttachmentSlot, LootGrade> destination,
        WeaponBuild? build,
        LootGrade grade)
    {
        destination.Clear();
        if (build is null)
        {
            return;
        }
        foreach (var slot in build.Attachments.Keys)
        {
            destination[slot] = grade;
        }
    }

    private static void CopyAttachmentGrades(
        Dictionary<AttachmentSlot, LootGrade> destination,
        Dictionary<AttachmentSlot, LootGrade> source)
    {
        destination.Clear();
        foreach (var pair in source)
        {
            destination[pair.Key] = pair.Value;
        }
    }
}
