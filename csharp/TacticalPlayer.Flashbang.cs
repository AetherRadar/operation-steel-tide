using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer : IFlashbangTarget
{
    public Vector3 FlashbangViewOrigin
        => IsInstanceValid(_camera)
            ? _camera.GlobalPosition
            : GlobalPosition + Vector3.Up * 1.55f;

    public Vector3 FlashbangViewForward
        => IsInstanceValid(_camera)
            ? -_camera.GlobalBasis.Z
            : -GlobalBasis.Z;

    public bool CanReceiveFlashbang => !IsDead;

    public void ApplyFlashbang(FlashbangExposure exposure)
        => Hud?.ShowFlashbangExposure(exposure.Intensity, exposure.DurationSeconds);
}
