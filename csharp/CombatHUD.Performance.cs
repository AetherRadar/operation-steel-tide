using System;
using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private static readonly string[] CompassDirections = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private int _alertPresentationState = int.MinValue;
    private int _alertPresentationValue = int.MinValue;
    private string _alertPresentationLanguage = string.Empty;
    private bool _statsPresentationInitialized;
    private int _statsHealth;
    private int _statsArmor;
    private float _statsStamina;
    private int _statsAmmo;
    private int _statsReserve;
    private int _statsGrenades;
    private bool _statsHealthCritical;
    private bool _staminaRecoveryPresentationInitialized;
    private bool _staminaRecovering;
    private bool _equipmentPresentationInitialized;
    private int _equipmentArmorPlates;
    private string _equipmentFireMode = string.Empty;
    private string _equipmentWeaponName = string.Empty;
    private string _equipmentLanguage = string.Empty;
    private bool _equipmentHasPrimary;
    private int _equipmentPrimaryFingerprint;
    private int _equipmentSecondaryFingerprint;
    private int _equipmentSidearmFingerprint;
    private string _equipmentKnifeSkinId = string.Empty;
    private int _equipmentActiveWeaponSlot;
    private int _headingDegrees = int.MinValue;
    private int _headingDirection = int.MinValue;
    private int _medicalBandages = int.MinValue;
    private int _medicalFieldMedkits = int.MinValue;
    private int _medicalAdrenaline = int.MinValue;
    private int _medicalArmorPlates = int.MinValue;
    private bool _medicalBoostInitialized;
    private bool _medicalBoostActive;
    private int _medicalBoostTenths = int.MinValue;
    private string _medicalLanguage = string.Empty;
    private int _statsPresentationUpdateCount;
    private int _equipmentPresentationUpdateCount;
    private int _headingPresentationUpdateCount;
    private int _medicalPresentationUpdateCount;

    internal int StatsPresentationUpdateCountForDiagnostics => _statsPresentationUpdateCount;
    internal int EquipmentPresentationUpdateCountForDiagnostics => _equipmentPresentationUpdateCount;
    internal int HeadingPresentationUpdateCountForDiagnostics => _headingPresentationUpdateCount;
    internal int MedicalPresentationUpdateCountForDiagnostics => _medicalPresentationUpdateCount;
    internal int QuickSlotPresentationUpdateCountForDiagnostics
        => IsInstanceValid(_quickSlotBar) ? _quickSlotBar.PresentationUpdateCountForDiagnostics : 0;

    internal void ResetPresentationPerformanceCountersForDiagnostics()
    {
        _statsPresentationUpdateCount = 0;
        _equipmentPresentationUpdateCount = 0;
        _headingPresentationUpdateCount = 0;
        _medicalPresentationUpdateCount = 0;
        if (IsInstanceValid(_quickSlotBar))
        {
            _quickSlotBar.ResetPresentationUpdateCountForDiagnostics();
        }
    }

    private bool BeginAlertPresentationUpdate(int state, int displayedValue)
    {
        if (_alertPresentationState == state
            && _alertPresentationValue == displayedValue
            && _alertPresentationLanguage == _language)
        {
            return false;
        }

        _alertPresentationState = state;
        _alertPresentationValue = displayedValue;
        _alertPresentationLanguage = _language;
        return true;
    }

    private void ApplyStatsPresentation(
        float health,
        float armor,
        float stamina,
        int ammo,
        int reserve,
        int grenades)
    {
        var healthValue = Mathf.Max(0, (int)health);
        var armorValue = Mathf.Max(0, (int)armor);
        var grenadeValue = Mathf.Max(0, grenades);
        var healthCritical = health < 30.0f;
        var changed = false;

        if (!_statsPresentationInitialized || _statsHealth != healthValue)
        {
            _healthLabel.Text = $"{healthValue:000}";
            _statsHealth = healthValue;
            changed = true;
        }
        if (!_statsPresentationInitialized || _statsArmor != armorValue)
        {
            _armorLabel.Text = $"{armorValue:00}";
            _statsArmor = armorValue;
            changed = true;
        }
        if (!_statsPresentationInitialized || !Mathf.IsEqualApprox(_statsStamina, stamina))
        {
            _staminaBar.Value = stamina;
            _statsStamina = stamina;
            changed = true;
        }
        if (!_statsPresentationInitialized || _statsAmmo != ammo)
        {
            _ammoLabel.Text = $"{ammo:00}";
            _statsAmmo = ammo;
            changed = true;
        }
        if (!_statsPresentationInitialized || _statsReserve != reserve)
        {
            _reserveLabel.Text = $"/ {reserve:000}";
            _statsReserve = reserve;
            changed = true;
        }
        if (!_statsPresentationInitialized || _statsGrenades != grenadeValue)
        {
            _grenadeCount = grenadeValue;
            _statsGrenades = grenadeValue;
            RefreshQuickSlotBar();
            changed = true;
        }
        if (!_statsPresentationInitialized || _statsHealthCritical != healthCritical)
        {
            _healthLabel.AddThemeColorOverride(
                "font_color",
                healthCritical
                    ? new Color(1.0f, 0.36f, 0.25f)
                    : new Color(0.88f, 0.96f, 0.92f));
            _statsHealthCritical = healthCritical;
            changed = true;
        }

        _statsPresentationInitialized = true;
        if (changed)
        {
            _statsPresentationUpdateCount++;
        }
    }

    private void ApplyStaminaRecoveryPresentation(bool recovering)
    {
        if (_staminaRecoveryPresentationInitialized && _staminaRecovering == recovering)
        {
            return;
        }
        _staminaBar.Modulate = recovering
            ? new Color(1.0f, 0.55f, 0.22f)
            : new Color(0.46f, 0.92f, 0.68f);
        _staminaRecovering = recovering;
        _staminaRecoveryPresentationInitialized = true;
        _statsPresentationUpdateCount++;
    }

    private void ApplyEquipmentPresentation(
        int armorPlates,
        string fireMode,
        string weaponName,
        WeaponBuild? primaryWeapon,
        bool hasPrimary,
        string knifeSkinId,
        WeaponBuild? secondaryWeapon,
        WeaponBuild? sidearmWeapon,
        int activeWeaponSlot)
    {
        var primaryFingerprint = WeaponPresentationFingerprint(primaryWeapon);
        var secondaryFingerprint = WeaponPresentationFingerprint(secondaryWeapon);
        var sidearmFingerprint = WeaponPresentationFingerprint(sidearmWeapon);
        var clampedSlot = Mathf.Clamp(activeWeaponSlot, 0, 5);
        var quickSlotsChanged = !_equipmentPresentationInitialized
            || _equipmentLanguage != _language
            || _equipmentHasPrimary != hasPrimary
            || _equipmentPrimaryFingerprint != primaryFingerprint
            || _equipmentSecondaryFingerprint != secondaryFingerprint
            || _equipmentSidearmFingerprint != sidearmFingerprint
            || !string.Equals(_equipmentKnifeSkinId, knifeSkinId, StringComparison.Ordinal)
            || _equipmentActiveWeaponSlot != clampedSlot;
        var modeChanged = !_equipmentPresentationInitialized
            || _equipmentLanguage != _language
            || !string.Equals(_equipmentFireMode, fireMode, StringComparison.Ordinal)
            || !string.Equals(_equipmentWeaponName, weaponName, StringComparison.Ordinal);
        var platesChanged = !_equipmentPresentationInitialized || _equipmentArmorPlates != armorPlates;

        if (quickSlotsChanged)
        {
            _hasPrimary = hasPrimary;
            _activeWeaponSlot = clampedSlot;
            _quickPrimaryBuild = hasPrimary ? primaryWeapon : null;
            _quickSecondaryBuild = secondaryWeapon;
            _quickSidearmBuild = sidearmWeapon;
            _quickKnifeSkinId = knifeSkinId;
            RefreshQuickSlotBar();
        }
        if (platesChanged)
        {
            _plateReserveLabel.Text = $"x{armorPlates}";
        }
        if (modeChanged)
        {
            _lastFireMode = fireMode;
            var mode = fireMode switch
            {
                "AUTO" => Text("auto", "AUTO"),
                "SEMI" => Text("semi", "SEMI"),
                "GRENADE" => Text("quick_throw", "THROW"),
                "UTILITY" => Text("quick_deploy", "DEPLOY"),
                _ => Text("knife", "KNIFE")
            };
            var displayWeapon = fireMode == "KNIFE" ? Text("tactical_knife", "TACTICAL KNIFE") : weaponName;
            _weaponModeLabel.Text = $"{displayWeapon}   {mode}";
        }

        _equipmentArmorPlates = armorPlates;
        _equipmentFireMode = fireMode;
        _equipmentWeaponName = weaponName;
        _equipmentLanguage = _language;
        _equipmentHasPrimary = hasPrimary;
        _equipmentPrimaryFingerprint = primaryFingerprint;
        _equipmentSecondaryFingerprint = secondaryFingerprint;
        _equipmentSidearmFingerprint = sidearmFingerprint;
        _equipmentKnifeSkinId = knifeSkinId;
        _equipmentActiveWeaponSlot = clampedSlot;
        _equipmentPresentationInitialized = true;
        if (quickSlotsChanged || modeChanged || platesChanged)
        {
            _equipmentPresentationUpdateCount++;
        }
    }

    private void ApplyHeadingPresentation(float degrees)
    {
        var normalized = Mathf.PosMod(degrees, 360.0f);
        var direction = (int)Mathf.Round(normalized / 45.0f) % CompassDirections.Length;
        var displayedDegrees = Mathf.RoundToInt(normalized);
        if (_headingDegrees == displayedDegrees && _headingDirection == direction)
        {
            return;
        }
        _headingDegrees = displayedDegrees;
        _headingDirection = direction;
        _compassLabel.Text = $"{CompassDirections[(direction + 7) % 8]}      {displayedDegrees:000}      {CompassDirections[direction]}";
        _headingPresentationUpdateCount++;
    }

    private void ApplyMedicalPresentation(
        FieldSupplySnapshot supplies,
        bool adrenalineActive,
        float adrenalineRemaining)
    {
        if (!IsInstanceValid(_medicalStatusLabel))
        {
            return;
        }

        var languageChanged = _medicalLanguage != _language;
        var statusChanged = languageChanged
            || _medicalBandages != supplies.Bandages
            || _medicalFieldMedkits != supplies.FieldMedkits
            || _medicalAdrenaline != supplies.Adrenaline
            || _medicalArmorPlates != supplies.ArmorPlates;
        var boostTenths = adrenalineActive
            ? Mathf.RoundToInt(Mathf.Max(0.0f, adrenalineRemaining) * 10.0f)
            : -1;
        var boostChanged = !_medicalBoostInitialized
            || languageChanged
            || _medicalBoostActive != adrenalineActive
            || adrenalineActive && _medicalBoostTenths != boostTenths;

        if (statusChanged)
        {
            var caption = Text("medical_hotkey", "B  FIELD SUPPLIES");
            _medicalStatusLabel.Text = $"{caption}  //  B{supplies.Bandages}  +{supplies.FieldMedkits}  A{supplies.Adrenaline}  P{supplies.ArmorPlates}";
        }
        if (boostChanged)
        {
            _medicalBoostLabel.Text = adrenalineActive
                ? $"{Text("medical_boost", "ADRENALINE BOOST")}  {boostTenths / 10.0f:0.0}s"
                : Text("medical_hold_hint", "HOLD B FOR RADIAL SELECT");
            if (!_medicalBoostInitialized || _medicalBoostActive != adrenalineActive)
            {
                _medicalBoostLabel.AddThemeColorOverride(
                    "font_color",
                    adrenalineActive
                        ? new Color(1.0f, 0.66f, 0.2f)
                        : new Color(0.42f, 0.56f, 0.53f));
            }
        }

        _medicalBandages = supplies.Bandages;
        _medicalFieldMedkits = supplies.FieldMedkits;
        _medicalAdrenaline = supplies.Adrenaline;
        _medicalArmorPlates = supplies.ArmorPlates;
        _medicalBoostActive = adrenalineActive;
        _medicalBoostTenths = boostTenths;
        _medicalBoostInitialized = true;
        _medicalLanguage = _language;
        if (statusChanged || boostChanged)
        {
            _medicalPresentationUpdateCount++;
        }
    }

    private static int WeaponPresentationFingerprint(WeaponBuild? weapon)
    {
        if (weapon is null)
        {
            return int.MinValue;
        }
        unchecked
        {
            var hash = 17 * 31 + (int)weapon.Platform;
            hash = hash * 31 + weapon.Attachments.Count;
            foreach (var attachment in weapon.Attachments)
            {
                hash += ((int)attachment.Key + 1) * 397
                    ^ StringComparer.Ordinal.GetHashCode(attachment.Value);
            }
            return hash;
        }
    }
}
