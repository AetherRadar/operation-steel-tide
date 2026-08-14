using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum PlayerWeaponSlot
{
    Primary = 0,
    Secondary = 1,
    Melee = 2
}

public partial class TacticalPlayer
{
    private WeaponBuild? _primaryWeaponSlot = WeaponCatalog.StarterWeapon();
    private WeaponBuild? _secondaryWeaponSlot;
    private LootGrade _primaryWeaponSlotGrade = LootGrade.Rare;
    private LootGrade _secondaryWeaponSlotGrade = LootGrade.Uncommon;
    private readonly Dictionary<AttachmentSlot, LootGrade> _primaryAttachmentGrades = new();
    private readonly Dictionary<AttachmentSlot, LootGrade> _secondaryAttachmentGrades = new();
    private int _primaryMagazineAmmo = 30;
    private int _secondaryMagazineAmmo;
    private LootGrade _primaryLoadedAmmoGrade = LootGrade.Common;
    private LootGrade _secondaryLoadedAmmoGrade = LootGrade.Common;
    private PlayerWeaponSlot _activeWeaponSlot = PlayerWeaponSlot.Primary;

    public PlayerWeaponSlot ActiveWeaponSlot => _activeWeaponSlot;
    public bool HasSecondaryWeapon => _secondaryWeaponSlot is not null;
    public WeaponPlatform? SecondaryWeaponPlatform => _secondaryWeaponSlot?.Platform;
    public WeaponBuild? PrimaryWeaponBuild => _primaryWeaponSlot?.Clone();
    public int PrimaryMagazineAmmo => _activeWeaponSlot == PlayerWeaponSlot.Primary && !_knifeEquipped
        ? Ammo
        : _primaryMagazineAmmo;
    public int SecondaryMagazineAmmo => _activeWeaponSlot == PlayerWeaponSlot.Secondary && !_knifeEquipped
        ? Ammo
        : _secondaryMagazineAmmo;
    public WeaponBuild? SecondaryWeaponBuild => _secondaryWeaponSlot?.Clone();

    private void ClearWeaponSlotsForColdStart()
    {
        _primaryWeaponSlot = null;
        _secondaryWeaponSlot = null;
        _primaryMagazineAmmo = 0;
        _secondaryMagazineAmmo = 0;
        _primaryAttachmentGrades.Clear();
        _secondaryAttachmentGrades.Clear();
        HasFireablePrimary = false;
    }

    private void InstallPrimaryWeapon(WeaponBuild build, LootGrade grade)
    {
        StoreActiveFirearmState();
        _primaryWeaponSlot = build.Clone();
        _primaryWeaponSlotGrade = grade;
        _primaryMagazineAmmo = build.Stats().MagazineSize;
        _primaryLoadedAmmoGrade = BestAmmoGrade(WeaponCatalog.Weapon(build.Platform).Caliber);
        CopyAttachmentGrades(_primaryAttachmentGrades, build, grade);
        HasFireablePrimary = true;
        ActivateWeaponSlot(PlayerWeaponSlot.Primary, true, true, false);
    }

    private void InstallSecondaryWeapon(WeaponBuild? build, LootGrade grade)
    {
        StoreActiveFirearmState();
        _secondaryWeaponSlot = build?.Clone();
        _secondaryWeaponSlotGrade = grade;
        _secondaryMagazineAmmo = build?.Stats().MagazineSize ?? 0;
        _secondaryLoadedAmmoGrade = LootGrade.Common;
        _secondaryAttachmentGrades.Clear();
        if (build is not null)
        {
            CopyAttachmentGrades(_secondaryAttachmentGrades, build, grade);
        }
        if (_secondaryWeaponSlot is null && _activeWeaponSlot == PlayerWeaponSlot.Secondary)
        {
            ActivateWeaponSlot(HasFireablePrimary ? PlayerWeaponSlot.Primary : PlayerWeaponSlot.Melee, false);
        }
        else if (_secondaryWeaponSlot is not null && _activeWeaponSlot == PlayerWeaponSlot.Secondary)
        {
            ActivateWeaponSlot(PlayerWeaponSlot.Secondary, false, true, false);
        }
    }

    private void StoreActiveFirearmState()
    {
        if (_knifeEquipped)
        {
            return;
        }
        if (_activeWeaponSlot == PlayerWeaponSlot.Primary && _primaryWeaponSlot is not null)
        {
            _primaryWeaponSlot = EquippedWeapon.Clone();
            _primaryWeaponSlotGrade = EquippedWeaponGrade;
            _primaryMagazineAmmo = Ammo;
            _primaryLoadedAmmoGrade = _loadedAmmoGrade;
            CopyAttachmentGrades(_primaryAttachmentGrades, _equippedAttachmentGrades);
        }
        else if (_activeWeaponSlot == PlayerWeaponSlot.Secondary && _secondaryWeaponSlot is not null)
        {
            _secondaryWeaponSlot = EquippedWeapon.Clone();
            _secondaryWeaponSlotGrade = EquippedWeaponGrade;
            _secondaryMagazineAmmo = Ammo;
            _secondaryLoadedAmmoGrade = _loadedAmmoGrade;
            CopyAttachmentGrades(_secondaryAttachmentGrades, _equippedAttachmentGrades);
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
        if (slot == PlayerWeaponSlot.Primary && (_primaryWeaponSlot is null || !HasFireablePrimary))
        {
            return false;
        }
        if (slot == PlayerWeaponSlot.Secondary && _secondaryWeaponSlot is null)
        {
            return false;
        }
        if (!force && _activeWeaponSlot == slot && (slot == PlayerWeaponSlot.Melee) == _knifeEquipped)
        {
            return true;
        }
        if (_isReloading)
        {
            _isReloading = false;
            _reloadTime = 0.0f;
            ResetReloadRig();
        }

        if (storeCurrent)
        {
            StoreActiveFirearmState();
        }
        _activeWeaponSlot = slot;
        _knifeEquipped = slot == PlayerWeaponSlot.Melee;
        _isAiming = false;
        _knifeTime = 0.0f;
        if (!_knifeEquipped)
        {
            var build = slot == PlayerWeaponSlot.Primary ? _primaryWeaponSlot! : _secondaryWeaponSlot!;
            EquippedWeapon = build.Clone();
            EquippedWeaponGrade = slot == PlayerWeaponSlot.Primary
                ? _primaryWeaponSlotGrade
                : _secondaryWeaponSlotGrade;
            Ammo = slot == PlayerWeaponSlot.Primary ? _primaryMagazineAmmo : _secondaryMagazineAmmo;
            _loadedAmmoGrade = slot == PlayerWeaponSlot.Primary
                ? _primaryLoadedAmmoGrade
                : _secondaryLoadedAmmoGrade;
            _automaticFire = WeaponCatalog.Weapon(EquippedWeapon.Platform).SupportsAutomatic;
            _equippedAttachmentGrades.Clear();
            CopyAttachmentGrades(
                _equippedAttachmentGrades,
                slot == PlayerWeaponSlot.Primary ? _primaryAttachmentGrades : _secondaryAttachmentGrades);
            ApplyWeaponBuildVisuals();
        }

        _weaponRoot.Visible = !_knifeEquipped;
        _knifeRoot.Visible = _knifeEquipped;
        _weaponLight.Visible = !_knifeEquipped && _flashlightOn;
        if (notify)
        {
            var (key, english) = slot switch
            {
                PlayerWeaponSlot.Secondary => ("secondary_ready", "SIDEARM READY"),
                PlayerWeaponSlot.Melee => ("knife_ready", "TACTICAL KNIFE READY"),
                _ => ("primary_ready", "PRIMARY WEAPON READY")
            };
            Hud?.ShowLocalizedMessage(key, english, new Color(0.42f, 0.9f, 0.73f));
        }
        return true;
    }

    private void CycleWeaponSlots()
    {
        var next = _activeWeaponSlot switch
        {
            PlayerWeaponSlot.Primary when HasSecondaryWeapon => PlayerWeaponSlot.Secondary,
            PlayerWeaponSlot.Primary => PlayerWeaponSlot.Melee,
            PlayerWeaponSlot.Secondary => PlayerWeaponSlot.Melee,
            _ => HasFireablePrimary ? PlayerWeaponSlot.Primary : PlayerWeaponSlot.Melee
        };
        ActivateWeaponSlot(next, true);
    }

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
        WeaponBuild build,
        LootGrade grade)
    {
        destination.Clear();
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
