using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public void ApplyDeploymentLoadout(DeploymentLoadout loadout, bool includeEmergencySupplies = true)
    {
        EquippedHelmet = EquipmentCatalog.Create(loadout.HelmetId);
        EquippedBodyArmor = EquipmentCatalog.Create(loadout.BodyArmorId);
        EquippedBackpack = EquipmentCatalog.Create(loadout.BackpackId);
        // Auto-enable NVG when spawning with NVG helmet — default should be on
        if (loadout.HelmetId == "helmet_nvg")
        {
            _nvgOn = true;
            Main?.SetNightVisionActive(true);
            Hud?.SetNightVisionActive(true);
            Hud?.ShowLocalizedMessage("nvg_auto_on", "NVG AUTO ON // PRESS N TO TOGGLE", new Color(0.42f, 0.95f, 0.42f));
        }
        else if (_nvgOn)
        {
            _nvgOn = false;
            Main?.SetNightVisionActive(false);
            Hud?.SetNightVisionActive(false);
        }
        ResetEquippedEquipmentGrades();
        ResetAmmoReserves();

        if (loadout.Weapon is null)
        {
            ApplyColdStartUnarmed(includeEmergencySupplies);
            InstallSidearmWeapon(loadout.Sidearm, LootGrade.Uncommon);
            if (loadout.Sidearm is not null)
            {
                var sidearmCaliber = WeaponCatalog.Weapon(loadout.Sidearm.Platform).Caliber;
                SetAmmoReserve(sidearmCaliber, LootGrade.Common, loadout.SidearmReserveAmmo);
                ActivateWeaponSlot(PlayerWeaponSlot.Sidearm, false, true, false);
            }
            if (includeEmergencySupplies)
            {
                ApplyReputationPerks(loadout);
            }
            Hud?.SetAmmoTier(CurrentAmmoGrade);
            Hud?.SetBackpackValuePlayer(this);
            return;
        }

        var weaponTier = loadout.WeaponBuildTier >= 0
            ? loadout.WeaponBuildTier
            : DeploymentCatalog.Weapon(loadout.Selection.WeaponId).BuildTier;
        InstallSecondaryWeapon(null, LootGrade.Uncommon);
        InstallPrimaryWeapon(loadout.Weapon, LootGrades.FromTier(weaponTier));
        _loadedAmmoGrade = loadout.AmmoGrade;
        SetAmmoReserve(CurrentAmmoCaliber, loadout.AmmoGrade, loadout.ReserveAmmo);
        Ammo = EquippedWeapon.Stats().MagazineSize;
        _primaryMagazineAmmo = Ammo;
        _primaryLoadedAmmoGrade = _loadedAmmoGrade;
        InstallSidearmWeapon(loadout.Sidearm, LootGrade.Uncommon);
        if (loadout.Sidearm is not null)
        {
            var sidearmCaliber = WeaponCatalog.Weapon(loadout.Sidearm.Platform).Caliber;
            SetAmmoReserve(sidearmCaliber, LootGrade.Common, loadout.SidearmReserveAmmo);
        }
        ActivateWeaponSlot(PlayerWeaponSlot.Primary, false);
        if (includeEmergencySupplies)
        {
            ApplyReputationPerks(loadout);
        }
        Hud?.SetAmmoTier(CurrentAmmoGrade);
        Hud?.SetBackpackValuePlayer(this);
    }

    /// <summary>Reputation perks grant non-weapon starting gear; raid deployments only.</summary>
    private void ApplyReputationPerks(DeploymentLoadout loadout)
    {
        var reputationLevel = loadout.ReputationLevel;
        if (reputationLevel < OperatorReputation.SmokeGrenadePerkLevel)
        {
            return;
        }
        SmokeGrenades = Mathf.Clamp(
            SmokeGrenades + 1,
            0,
            DemolitionBuyCatalog.MaximumSmokeGrenades);
        Hud?.SetDemolitionSmokeGrenades(SmokeGrenades);
        if (reputationLevel >= OperatorReputation.ReserveAmmoPerkLevel && loadout.Weapon is not null)
        {
            var caliber = WeaponCatalog.Weapon(loadout.Weapon.Platform).Caliber;
            SetAmmoReserve(
                caliber,
                loadout.AmmoGrade,
                AmmoReserveFor(caliber, loadout.AmmoGrade) + OperatorReputation.ReserveAmmoBonus);
        }
        if (reputationLevel >= OperatorReputation.ArmorPlatePerkLevel)
        {
            TryStoreArmorPlate(LootGrade.Uncommon, 1);
            Hud?.SetMedicalInventory(this);
        }
    }
}
