using Godot;

namespace OperationSteelTide;

public partial class DriveableVehicle
{
    private static readonly Vector3 WindshieldHostileAimPoint = new(0.0f, 1.64f, -2.12f);
    private static readonly Vector3 LeftWindowHostileAimPoint = new(-0.94f, 1.62f, -1.15f);
    private static readonly Vector3 RightWindowHostileAimPoint = new(0.94f, 1.62f, -1.15f);

    internal bool IsDrivenBy(TacticalPlayer player)
        => HasDriver && ReferenceEquals(_driver, player);

    /// <summary>
    /// Returns a deterministic cabin-window aim point for hostile fire. The point remains
    /// inside the vehicle collider so the ballistic ray resolves against the vehicle body.
    /// </summary>
    internal Vector3 HostileAimPoint(Vector3 shooterPosition)
    {
        var localShooter = ToLocal(shooterPosition);
        if (localShooter.Z < -0.2f
            && Mathf.Abs(localShooter.Z) >= Mathf.Abs(localShooter.X) * 0.72f)
        {
            return ToGlobal(WindshieldHostileAimPoint);
        }

        return ToGlobal(localShooter.X < 0.0f
            ? LeftWindowHostileAimPoint
            : RightWindowHostileAimPoint);
    }
}
