using Godot;

namespace OperationSteelTide;

internal static partial class CombatModelLibrary
{
    internal const string AnimatedReloadArmsScenePath =
        "res://assets/models/djmaesen_smg45/animated_reload_arms.glb";

    private static readonly string[] AnimatedReloadArmsNodes =
    {
        "WeaponRoot", "ReloadArmsSkeleton", "ReloadArmsMesh",
        "FullReloadArmsAuditMesh", "LongGunReloadForearmsMesh",
        "SidearmReloadForearmsMesh",
        "RightGripFrame", "SupportGripFrame",
        "LeftPalmFrame", "LeftGripAnchorFrame",
        "LeftSidearmMagazineAnchorFrame", "RightPalmFrame",
        "LeftWristFrame", "RightWristFrame",
        "LeftShoulderFrame", "RightShoulderFrame",
        "m4a1_ElbowPoleFrame", "ak74_ElbowPoleFrame",
        "scarl_ElbowPoleFrame", "mp5a5_ElbowPoleFrame",
        "m24_ElbowPoleFrame", "axmc_ElbowPoleFrame",
        "awm_ElbowPoleFrame", "vss_ElbowPoleFrame",
        "p226_ElbowPoleFrame", "m1911_ElbowPoleFrame",
        "gsh18_ElbowPoleFrame", "desert_eagle_ElbowPoleFrame"
    };

    public static AuthoredAnimatedReloadArmsVisual InstantiateAnimatedReloadArms()
    {
        var root = InstantiateRequired(
            AnimatedReloadArmsScenePath,
            AnimatedReloadArmsNodes);
        root.Name = "AuthoredAnimatedReloadArmsVisual";
        foreach (var geometry in GeometryBelow(root))
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        return new AuthoredAnimatedReloadArmsVisual(root);
    }
}
