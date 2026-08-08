namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public void ApplyDeploymentLoadout(DeploymentLoadout loadout)
    {
        EquippedHelmet = EquipmentCatalog.Create(loadout.HelmetId);
        EquippedBodyArmor = EquipmentCatalog.Create(loadout.BodyArmorId);
        EquippedBackpack = EquipmentCatalog.Create(loadout.BackpackId);
        ResetAmmoReserves();

        if (loadout.Weapon is null)
        {
            ApplyColdStartUnarmed();
            Hud?.SetAmmoTier(CurrentAmmoGrade);
            Hud?.SetBackpackValuePlayer(this);
            return;
        }

        EquipPrimary(loadout.Weapon);
        _loadedAmmoGrade = loadout.AmmoGrade;
        SetAmmoReserve(CurrentAmmoCaliber, loadout.AmmoGrade, loadout.ReserveAmmo);
        Ammo = EquippedWeapon.Stats().MagazineSize;
        Hud?.SetAmmoTier(CurrentAmmoGrade);
        Hud?.SetBackpackValuePlayer(this);
    }
}
