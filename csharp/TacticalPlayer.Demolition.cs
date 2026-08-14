using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public void ResetForDemolitionRound(
        Vector3 spawn,
        OperatorRole role,
        DeploymentLoadout loadout,
        int grenadeCount)
    {
        EjectFromVehicleIfAny();
        ConfigureRole(role);
        ApplyDemolitionRoundLoadout(loadout, grenadeCount);
        GlobalPosition = spawn;
        Rotation = Vector3.Zero;
        Velocity = Vector3.Zero;
        Stamina = 100.0f;
        IsDead = false;
        IsExtractionPassenger = false;
        UiLocked = false;
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
        CollisionLayer = 1;
        CollisionMask = 1 | 2;
        _collider.Disabled = false;
        _stance = PlayerStance.Standing;
        _isReloading = false;
        _reloadTime = 0.0f;
        _isPlating = false;
        _plateTime = 0.0f;
        _isAiming = false;
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

    public void ApplyDemolitionRoundLoadout(DeploymentLoadout loadout, int grenadeCount)
    {
        Backpack.Clear();
        ApplyDeploymentLoadout(loadout, includeEmergencySupplies: false);
        Grenades = Mathf.Clamp(grenadeCount, 0, DemolitionBuyCatalog.MaximumGrenades);
        PushHudStats();
    }
}
