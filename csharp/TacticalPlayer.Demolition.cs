using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    public void ResetForDemolitionRound(
        Vector3 spawn,
        OperatorRole role,
        DeploymentLoadout loadout)
    {
        EjectFromVehicleIfAny();
        ConfigureRole(role);
        ApplyDeploymentLoadout(loadout);
        GlobalPosition = spawn;
        Rotation = Vector3.Zero;
        Velocity = Vector3.Zero;
        Stamina = 100.0f;
        Grenades = 2;
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
        _knifeEquipped = false;
        ResetReloadRig();
        _weaponRoot.Visible = HasFireablePrimary;
        _knifeRoot.Visible = false;
        _weaponLight.Visible = _flashlightOn && HasFireablePrimary;
        if (IsInstanceValid(_camera))
        {
            _camera.Current = true;
        }
        DisarmFireInput();
        RestoreMovementInput();
        Hud?.HideDownedState();
        PushHudStats();
    }
}
