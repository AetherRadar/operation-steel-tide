namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public void ApplyDeploymentLoadout(DeploymentLoadout loadout, bool includeEmergencySupplies = true)
    {
        EquippedHelmet = EquipmentCatalog.Create(loadout.HelmetId);
        EquippedBodyArmor = EquipmentCatalog.Create(loadout.BodyArmorId);
        EquippedBackpack = EquipmentCatalog.Create(loadout.BackpackId);
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
        Hud?.SetAmmoTier(CurrentAmmoGrade);
        Hud?.SetBackpackValuePlayer(this);
    }
}
