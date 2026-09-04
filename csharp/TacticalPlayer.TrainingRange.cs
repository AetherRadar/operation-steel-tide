using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly WeaponPlatform[] TrainingRangeWeapons =
    {
        WeaponPlatform.M4A1,
        WeaponPlatform.AK74,
        WeaponPlatform.ScarL,
        WeaponPlatform.MP5A5,
        WeaponPlatform.M3A1,
        WeaponPlatform.VSS,
        WeaponPlatform.M24,
        WeaponPlatform.AXMC,
        WeaponPlatform.AWM,
        WeaponPlatform.P226,
        WeaponPlatform.M1911,
        WeaponPlatform.DesertEagle,
        WeaponPlatform.GSh18
    };

    private int _trainingRangeWeaponIndex;

    public int TrainingRangeWeaponCount => TrainingRangeWeapons.Length;
    public int TrainingRangeWeaponIndex => _trainingRangeWeaponIndex;
    public WeaponPlatform TrainingRangeWeaponPlatform
        => TrainingRangeWeapons[_trainingRangeWeaponIndex];

    public void PrepareTrainingRangeLoadout(Vector3 spawn)
    {
        EjectFromVehicleIfAny();
        CancelLadderClimb(notify: false);
        CancelLowObstacleVault("training_range_reset");
        CloseMedicalWheelWithoutUse();
        ResetFieldUseForRound();
        CancelReload();
        ConfigureRole(OperatorRole.Assault);
        ApplyColdStartUnarmed(includeEmergencySupplies: false);
        _trainingRangeWeaponIndex = 0;
        InstallTrainingRangeWeapon(TrainingRangeWeapons[_trainingRangeWeaponIndex], notify: false);

        GlobalPosition = spawn;
        Rotation = Vector3.Zero;
        Velocity = Vector3.Zero;
        Stamina = 100.0f;
        IsDead = false;
        IsExtractionPassenger = false;
        UiLocked = false;
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
        SetPhysicsProcess(true);
        CollisionLayer = 1;
        CollisionMask = 1 | 2 | BreakableGlassField.MovementCollisionLayer;
        _collider.Disabled = false;
        _stance = PlayerStance.Standing;
        _isPlating = false;
        _plateTime = 0.0f;
        _isAiming = false;
        _slideTime = 0.0f;
        CancelMeleeAction();
        ResetReloadRig();
        _weaponRoot.Visible = HasActiveFirearm;
        _knifeRoot.Visible = _knifeEquipped;
        _weaponLight.Visible = _flashlightOn && HasActiveFirearm;
        UpdateHeldThrowableVisual();
        if (IsInstanceValid(_camera))
        {
            _camera.Current = true;
        }
        DisarmFireInput();
        RestoreMovementInput();
        RefillTrainingRangeAmmo();
    }

    public void CycleTrainingRangeWeapon()
    {
        if (Main?.IsTrainingRangeActive != true)
        {
            return;
        }
        _trainingRangeWeaponIndex = (_trainingRangeWeaponIndex + 1) % TrainingRangeWeapons.Length;
        InstallTrainingRangeWeapon(TrainingRangeWeapons[_trainingRangeWeaponIndex], notify: true);
    }

    public void RefillTrainingRangeAmmo()
    {
        if (IsFirearmQuickSlotSelected && !_knifeEquipped)
        {
            Ammo = EquippedWeapon.Stats().MagazineSize;
            SetAmmoReserve(CurrentAmmoCaliber, LootGrade.Legendary, 9999);
        }
        Grenades = 99;
        SmokeGrenades = 99;
        IncendiaryGrenades = 99;
        FlashbangGrenades = 99;
        PushHudStats();
    }

    private void InstallTrainingRangeWeapon(WeaponPlatform platform, bool notify)
    {
        var build = WeaponCatalog.Build(platform, 3);
        if (WeaponCatalog.IsSidearm(platform))
        {
            InstallSidearmWeapon(build, LootGrade.Legendary);
            ActivateWeaponSlot(PlayerWeaponSlot.Sidearm, notify, force: true, storeCurrent: false);
        }
        else
        {
            HasFireablePrimary = true;
            InstallPrimaryWeapon(build, LootGrade.Legendary);
            ActivateWeaponSlot(PlayerWeaponSlot.Primary, notify, force: true, storeCurrent: false);
        }
        RefillTrainingRangeAmmo();
        if (notify)
        {
            var definition = WeaponCatalog.Weapon(platform);
            Hud?.ShowLocalizedMessage(
                "training_range_status",
                $"TRAINING RANGE  //  {definition.Name}",
                new Color(0.42f, 0.82f, 1.0f));
        }
    }
}
