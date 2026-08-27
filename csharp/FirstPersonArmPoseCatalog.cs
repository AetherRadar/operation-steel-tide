using Godot;

namespace OperationSteelTide;

internal enum FirstPersonArmPoseKind
{
    Sidearm,
    Compact,
    Rifle,
    LongRifle
}

internal readonly record struct FirstPersonArmPoseDefinition(
    FirstPersonArmPoseKind Kind,
    Vector3 PrimaryGrip,
    Vector3 SupportGrip);

internal readonly record struct FirstPersonArmRigSnapshot(
    WeaponPlatform Platform,
    FirstPersonArmPoseKind PoseKind,
    bool Active,
    bool NativeSmgRig,
    bool ProceduralVisible,
    ulong InstanceId,
    Vector3 RootScale,
    Vector3 PrimaryPalmLocal,
    Vector3 SupportPalmLocal,
    Vector3 PrimaryPalmGlobal,
    Vector3 SupportPalmGlobal,
    float PrimaryGripError,
    float SupportGripError,
    Vector2 PrimaryScreen,
    Vector2 SupportScreen,
    bool PrimaryBehindCamera,
    bool SupportBehindCamera);

internal static class FirstPersonArmPoseCatalog
{
    // All grips are WeaponRoot-local metres. The authored arm asset is mounted
    // once and only these two palm anchors vary by weapon family.
    private static readonly FirstPersonArmPoseDefinition Sidearm = new(
        FirstPersonArmPoseKind.Sidearm,
        new Vector3(0.0f, -0.03f, 0.245f),
        new Vector3(-0.12f, -0.04f, 0.24f));

    private static readonly FirstPersonArmPoseDefinition Gsh18Sidearm = new(
        FirstPersonArmPoseKind.Sidearm,
        new Vector3(0.0f, -0.03f, 0.03f),
        new Vector3(-0.12f, -0.04f, 0.025f));

    private static readonly FirstPersonArmPoseDefinition Compact = new(
        FirstPersonArmPoseKind.Compact,
        new Vector3(0.0f, -0.16f, -0.05f),
        new Vector3(-0.08f, 0.0f, -0.39f));

    private static readonly FirstPersonArmPoseDefinition Rifle = new(
        FirstPersonArmPoseKind.Rifle,
        new Vector3(0.0f, -0.15f, -0.05f),
        new Vector3(0.0f, 0.0f, -0.58f));

    private static readonly FirstPersonArmPoseDefinition LongRifle = new(
        FirstPersonArmPoseKind.LongRifle,
        new Vector3(0.0f, -0.17f, 0.02f),
        new Vector3(-0.02f, 0.0f, -0.72f));

    private static readonly FirstPersonArmPoseDefinition VssRifle = new(
        FirstPersonArmPoseKind.Rifle,
        new Vector3(0.0f, -0.15f, -0.05f),
        new Vector3(0.0f, 0.0f, -0.60f));

    private static readonly FirstPersonArmPoseDefinition AwmLongRifle = new(
        FirstPersonArmPoseKind.LongRifle,
        new Vector3(0.0f, -0.17f, 0.02f),
        new Vector3(-0.02f, 0.0f, -0.86f));

    public static FirstPersonArmPoseDefinition For(WeaponPlatform platform)
        => platform switch
        {
            WeaponPlatform.GSh18 => Gsh18Sidearm,
            WeaponPlatform.P226 or WeaponPlatform.M1911 or WeaponPlatform.DesertEagle
                => Sidearm,
            WeaponPlatform.MP5A5 or WeaponPlatform.M3A1 => Compact,
            WeaponPlatform.AWM => AwmLongRifle,
            WeaponPlatform.M24 or WeaponPlatform.AXMC => LongRifle,
            WeaponPlatform.VSS => VssRifle,
            _ => Rifle
        };
}
