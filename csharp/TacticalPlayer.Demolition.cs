using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public int SmokeGrenades { get; private set; }
    public int IncendiaryGrenades { get; private set; }
    public DemolitionUtilityType SelectedDemolitionUtility { get; private set; }
        = DemolitionUtilityType.Smoke;
    internal bool DemolitionColliderDisabledForDiagnostics => _collider.Disabled;

    public void ResetForDemolitionRound(
        Vector3 spawn,
        OperatorRole role,
        DeploymentLoadout loadout,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount = 0)
    {
        EjectFromVehicleIfAny();
        CancelLadderClimb(notify: false);
        CancelLowObstacleVault("demolition_round_reset");
        CloseMedicalWheelWithoutUse();
        ResetFieldUseForRound();
        CancelReload();
        ConfigureRole(role);
        ApplyDemolitionRoundLoadout(
            loadout,
            grenadeCount,
            smokeGrenadeCount,
            incendiaryGrenadeCount);
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
        Hud?.HideDownedState();
        PushHudStats();
    }

    public void ApplyDemolitionNetworkHealth(float health, bool dead)
    {
        Health = Mathf.Clamp(health, 0.0f, MaxHealth);
        if (dead)
        {
            CloseMedicalWheelWithoutUse();
            CancelFieldUse(false);
            CancelReload();
            if (!IsDead)
            {
                IsDead = true;
                Velocity = Vector3.Zero;
                MarkEliminatedForDemolitionRound();
                EmitSignal(SignalName.Died);
            }
            return;
        }
        if (IsDead)
        {
            IsDead = false;
            ProcessMode = ProcessModeEnum.Inherit;
            SetPhysicsProcess(true);
            CollisionLayer = 1;
            CollisionMask = 1 | 2 | BreakableGlassField.MovementCollisionLayer;
            _collider.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
            UiLocked = false;
        }
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
    }

    public void ApplyExtractionNetworkHealth(float health, bool down, bool reviveUsed)
    {
        var authoritativeDown = down || health <= 0.0f;
        var becameDown = authoritativeDown && !IsDead;
        Health = Mathf.Clamp(health, 0.0f, MaxHealth);
        ReviveUsed = reviveUsed;
        if (authoritativeDown)
        {
            CloseMedicalWheelWithoutUse();
            CancelFieldUse(false);
            CancelReload();
            if (becameDown)
            {
                Main?.InterruptLootForIncomingDamage();
                EjectFromVehicleIfAny();
            }
            IsDead = true;
            Velocity = Vector3.Zero;
            _stance = PlayerStance.Prone;
        }
        else if (IsDead)
        {
            IsDead = false;
            UiLocked = false;
            _stance = PlayerStance.Crouched;
        }
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
    }

    public void ApplyDemolitionRoundLoadout(
        DeploymentLoadout loadout,
        int grenadeCount,
        int smokeGrenadeCount,
        int incendiaryGrenadeCount = 0)
    {
        Backpack.Clear();
        ApplyDeploymentLoadout(loadout, includeEmergencySupplies: false);
        Grenades = Mathf.Clamp(grenadeCount, 0, DemolitionBuyCatalog.MaximumGrenades);
        SmokeGrenades = Mathf.Clamp(
            smokeGrenadeCount,
            0,
            DemolitionBuyCatalog.MaximumSmokeGrenades);
        IncendiaryGrenades = Mathf.Clamp(
            incendiaryGrenadeCount,
            0,
            DemolitionBuyCatalog.MaximumIncendiaryGrenades);
        SelectedDemolitionUtility = SmokeGrenades > 0
            ? DemolitionUtilityType.Smoke
            : DemolitionUtilityType.Incendiary;
        RefreshDemolitionUtilityHud();
        PushHudStats();
    }

    private bool ThrowSmokeGrenade()
    {
        if (SmokeGrenades <= 0 || _isReloading || MedicalActionBlocksWeapon || IsDead || Main is null)
        {
            return false;
        }
        var authoritativeView = CaptureAuthoritativeViewTransform();
        var origin = authoritativeView.Origin
            - authoritativeView.Basis.Z * 0.7f;
        var direction = -authoritativeView.Basis.Z;
        if (Main.IsDemolitionNetworkClient)
        {
            if (!Main.TryRequestLocalDemolitionUtilityThrow(
                    DemolitionNetworkUtilityKind.Smoke,
                    origin,
                    direction))
            {
                return false;
            }
            OnThrowableConsumed();
            return true;
        }
        SmokeGrenades--;
        Main.ThrowSmokeGrenade(origin, direction, this);
        if (SmokeGrenades <= 0 && IncendiaryGrenades > 0)
        {
            SelectedDemolitionUtility = DemolitionUtilityType.Incendiary;
        }
        RefreshDemolitionUtilityHud();
        PushHudStats();
        OnThrowableConsumed();
        return true;
    }

    private bool ThrowIncendiaryGrenade()
    {
        if (IncendiaryGrenades <= 0
            || _isReloading
            || MedicalActionBlocksWeapon
            || IsDead
            || Main is null)
        {
            return false;
        }
        var authoritativeView = CaptureAuthoritativeViewTransform();
        var origin = authoritativeView.Origin
            - authoritativeView.Basis.Z * 0.7f;
        var direction = -authoritativeView.Basis.Z;
        if (Main.IsDemolitionNetworkClient)
        {
            if (!Main.TryRequestLocalDemolitionUtilityThrow(
                    DemolitionNetworkUtilityKind.Incendiary,
                    origin,
                    direction))
            {
                return false;
            }
            OnThrowableConsumed();
            return true;
        }
        IncendiaryGrenades--;
        Main.ThrowIncendiaryGrenade(origin, direction, this);
        if (IncendiaryGrenades <= 0 && SmokeGrenades > 0)
        {
            SelectedDemolitionUtility = DemolitionUtilityType.Smoke;
        }
        RefreshDemolitionUtilityHud();
        PushHudStats();
        OnThrowableConsumed();
        return true;
    }

    private void RefreshDemolitionUtilityHud()
        => Hud?.SetDemolitionUtilities(
            SmokeGrenades,
            IncendiaryGrenades,
            SelectedDemolitionUtility);

    internal int DemolitionUtilityCount(DemolitionNetworkUtilityKind kind)
        => kind switch
        {
            DemolitionNetworkUtilityKind.Fragmentation => Grenades,
            DemolitionNetworkUtilityKind.Smoke => SmokeGrenades,
            DemolitionNetworkUtilityKind.Incendiary => IncendiaryGrenades,
            _ => 0
        };

    internal bool TryConsumeDemolitionNetworkUtility(DemolitionNetworkUtilityKind kind)
    {
        switch (kind)
        {
            case DemolitionNetworkUtilityKind.Fragmentation when Grenades > 0:
                Grenades--;
                break;
            case DemolitionNetworkUtilityKind.Smoke when SmokeGrenades > 0:
                SmokeGrenades--;
                if (SmokeGrenades <= 0 && IncendiaryGrenades > 0)
                {
                    SelectedDemolitionUtility = DemolitionUtilityType.Incendiary;
                }
                break;
            case DemolitionNetworkUtilityKind.Incendiary when IncendiaryGrenades > 0:
                IncendiaryGrenades--;
                if (IncendiaryGrenades <= 0 && SmokeGrenades > 0)
                {
                    SelectedDemolitionUtility = DemolitionUtilityType.Smoke;
                }
                break;
            default:
                return false;
        }
        RefreshDemolitionUtilityHud();
        PushHudStats();
        return true;
    }

    internal void MarkEliminatedForDemolitionRound()
    {
        if (!IsDead)
        {
            return;
        }
        CloseMedicalWheelWithoutUse();
        CancelFieldUse(false);
        CancelReload();
        ReviveUsed = true;
        CancelLadderClimb(notify: false);
        CancelLowObstacleVault("demolition_eliminated");
        Velocity = Vector3.Zero;
        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
        _collider.Disabled = true;
        Hud?.HideDownedState();
    }

    internal LootItem? DetachDemolitionDropWeapon()
    {
        StoreActiveFirearmState();
        PlayerWeaponSlot? dropSlot = HasActiveFirearm
            ? _activeWeaponSlot
            : HasFireablePrimary && WeaponBuildForSlot(PlayerWeaponSlot.Primary) is not null
                ? PlayerWeaponSlot.Primary
                : WeaponBuildForSlot(PlayerWeaponSlot.Secondary) is not null
                    ? PlayerWeaponSlot.Secondary
                    : WeaponBuildForSlot(PlayerWeaponSlot.Sidearm) is not null
                        ? PlayerWeaponSlot.Sidearm
                        : null;
        if (dropSlot is not PlayerWeaponSlot slot
            || WeaponBuildForSlot(slot) is not WeaponBuild weapon)
        {
            return null;
        }
        var grade = WeaponGradeForSlot(slot);
        SetWeaponSlot(slot, null, grade);
        if (slot == PlayerWeaponSlot.Primary)
        {
            HasFireablePrimary = false;
        }
        if (_activeWeaponSlot == slot)
        {
            ActivateWeaponSlot(PreferredFirearmOrMelee(), false, true, false);
        }
        return new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = weapon.Clone(),
            Grade = grade,
            Quantity = 1
        };
    }
}
