using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public int SmokeGrenades { get; private set; }
    internal bool DemolitionColliderDisabledForDiagnostics => _collider.Disabled;

    public void ResetForDemolitionRound(
        Vector3 spawn,
        OperatorRole role,
        DeploymentLoadout loadout,
        int grenadeCount,
        int smokeGrenadeCount)
    {
        EjectFromVehicleIfAny();
        CancelLadderClimb(notify: false);
        CancelLowObstacleVault("demolition_round_reset");
        ConfigureRole(role);
        ApplyDemolitionRoundLoadout(loadout, grenadeCount, smokeGrenadeCount);
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
        CollisionMask = 1 | 2;
        _collider.Disabled = false;
        _stance = PlayerStance.Standing;
        _isReloading = false;
        _reloadTime = 0.0f;
        _isPlating = false;
        _plateTime = 0.0f;
        _isAiming = false;
        _slideTime = 0.0f;
        _knifeTime = 0.0f;
        ResetReloadRig();
        _weaponRoot.Visible = HasActiveFirearm;
        _knifeRoot.Visible = _knifeEquipped;
        _weaponLight.Visible = _flashlightOn && HasActiveFirearm;
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
            CollisionMask = 1 | 2;
            _collider.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
            UiLocked = false;
        }
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
    }

    public void ApplyDemolitionRoundLoadout(
        DeploymentLoadout loadout,
        int grenadeCount,
        int smokeGrenadeCount)
    {
        Backpack.Clear();
        ApplyDeploymentLoadout(loadout, includeEmergencySupplies: false);
        Grenades = Mathf.Clamp(grenadeCount, 0, DemolitionBuyCatalog.MaximumGrenades);
        SmokeGrenades = Mathf.Clamp(
            smokeGrenadeCount,
            0,
            DemolitionBuyCatalog.MaximumSmokeGrenades);
        Hud?.SetDemolitionSmokeGrenades(SmokeGrenades);
        PushHudStats();
    }

    private bool ThrowSmokeGrenade()
    {
        if (SmokeGrenades <= 0 || _isReloading || MedicalActionBlocksWeapon || IsDead || Main is null)
        {
            return false;
        }
        SmokeGrenades--;
        Main.ThrowSmokeGrenade(
            _camera.GlobalPosition - _camera.GlobalBasis.Z * 0.7f,
            -_camera.GlobalBasis.Z,
            this);
        Hud?.SetDemolitionSmokeGrenades(SmokeGrenades);
        PushHudStats();
        OnThrowableConsumed();
        return true;
    }

    internal void MarkEliminatedForDemolitionRound()
    {
        if (!IsDead)
        {
            return;
        }
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
}
