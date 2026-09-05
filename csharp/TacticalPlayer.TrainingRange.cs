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
    private LootGrade _trainingRangeAmmoGrade = LootGrade.Legendary;

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
        // A range deploy is a fresh first-person spawn.  Clear any pitch/head pose or
        // camera basis left by a prior vehicle, downed state, or mission capture so the
        // player faces the firing lanes instead of the floor/sky.
        // The range spawn faces the target wall; a small downward bias keeps the
        // firing line and first target row in view instead of wasting the top half
        // of the first-person capture on sky.
        _pitch = -0.08f;
        _head.Position = new Vector3(0.0f, 1.57f, 0.0f);
        _head.Rotation = Vector3.Zero;
        _cameraLocalBasis = Basis.Identity;
        _cameraLocalOffset = Vector3.Zero;
        ResetFirstPersonTransformInterpolation();
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
        ShowTrainingRangeAttachmentControls();
    }

    public void CycleTrainingRangeWeapon()
    {
        if (Main?.IsTrainingRangeActive != true)
        {
            return;
        }
        _trainingRangeWeaponIndex = (_trainingRangeWeaponIndex + 1) % TrainingRangeWeapons.Length;
        InstallTrainingRangeWeapon(TrainingRangeWeapons[_trainingRangeWeaponIndex], notify: true);
        // Installing a new weapon restores the production slot's last/default
        // ammo grade.  Reapply the range selector after every cycle so AP/HP/
        // tracer and its T-level remain active immediately, without requiring a
        // manual reload first.
        ApplyTrainingRangeAmmoProfile(_trainingRangeAmmoType, _trainingRangeAmmoLevel);
    }

    public void RefillTrainingRangeAmmo()
    {
        if (IsFirearmQuickSlotSelected && !_knifeEquipped)
        {
            // Keep the reserve infinite without overwriting the live magazine.
            // The old per-frame assignment made R appear broken: every shot was
            // immediately restored before the reload state could be observed.
            // InstallTrainingRangeWeapon/ApplyTrainingRangeAmmoProfile still
            // fill a fresh weapon once; from then on normal reload timing owns
            // the magazine exactly as it does in a mission.
            SetAmmoReserve(CurrentAmmoCaliber, _trainingRangeAmmoGrade, 9999);
        }
        Grenades = 99;
        SmokeGrenades = 99;
        IncendiaryGrenades = 99;
        FlashbangGrenades = 99;
        PushHudStats();
    }

    private void InstallTrainingRangeWeapon(WeaponPlatform platform, bool notify)
    {
        var build = WeaponCatalog.BuildTrainingRangeDefault(platform);
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
