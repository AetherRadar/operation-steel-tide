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
    // All grips are WeaponRoot-local metres. Each entry is fitted against the
    // corresponding authored mesh surface; sharing one guessed family pose was
    // visibly separating palms from receivers and handguards on differently
    // proportioned marketplace assets.
    private static readonly FirstPersonArmPoseDefinition P226Sidearm = new(
        FirstPersonArmPoseKind.Sidearm,
        new Vector3(0.0f, -0.03017f, 0.23701f),
        new Vector3(-0.09566f, -0.04028f, 0.23902f));

    private static readonly FirstPersonArmPoseDefinition M1911Sidearm = new(
        FirstPersonArmPoseKind.Sidearm,
        new Vector3(-0.01132f, -0.03f, 0.245f),
        new Vector3(-0.10717f, -0.04f, 0.24f));

    private static readonly FirstPersonArmPoseDefinition LargeSidearm = new(
        FirstPersonArmPoseKind.Sidearm,
        new Vector3(0.00144f, 0.00954f, 0.23571f),
        new Vector3(-0.06896f, 0.01426f, 0.23169f));

    private static readonly FirstPersonArmPoseDefinition Gsh18Sidearm = new(
        FirstPersonArmPoseKind.Sidearm,
        new Vector3(0.0f, -0.04006f, 0.22579f),
        new Vector3(-0.09510f, -0.04929f, 0.22227f));

    private static readonly FirstPersonArmPoseDefinition Mp5Compact = new(
        FirstPersonArmPoseKind.Compact,
        new Vector3(0.0f, -0.09769f, -0.10293f),
        new Vector3(-0.04143f, 0.03383f, -0.38662f));

    private static readonly FirstPersonArmPoseDefinition M3Compact = new(
        FirstPersonArmPoseKind.Compact,
        new Vector3(0.0f, -0.16f, -0.05f),
        new Vector3(-0.08f, 0.0f, -0.39f));

    private static readonly FirstPersonArmPoseDefinition M4Rifle = new(
        FirstPersonArmPoseKind.Rifle,
        new Vector3(0.0f, -0.15f, -0.05f),
        new Vector3(0.0f, 0.0f, -0.58f));

    private static readonly FirstPersonArmPoseDefinition AkRifle = new(
        FirstPersonArmPoseKind.Rifle,
        new Vector3(0.0f, -0.07310f, -0.12176f),
        new Vector3(-0.00120f, 0.01912f, -0.58830f));

    private static readonly FirstPersonArmPoseDefinition ScarRifle = new(
        FirstPersonArmPoseKind.Rifle,
        new Vector3(0.0f, -0.09949f, -0.06684f),
        new Vector3(0.0f, -0.01173f, -0.49201f));

    private static readonly FirstPersonArmPoseDefinition M24LongRifle = new(
        FirstPersonArmPoseKind.LongRifle,
        new Vector3(-0.00537f, -0.09765f, 0.04146f),
        new Vector3(-0.02067f, 0.07128f, -0.71731f));

    private static readonly FirstPersonArmPoseDefinition AxmcLongRifle = new(
        FirstPersonArmPoseKind.LongRifle,
        new Vector3(-0.00473f, -0.10981f, -0.00562f),
        new Vector3(-0.02440f, 0.03824f, -0.72f));

    private static readonly FirstPersonArmPoseDefinition VssRifle = new(
        FirstPersonArmPoseKind.Rifle,
        new Vector3(-0.01020f, -0.05022f, -0.12157f),
        new Vector3(-0.01930f, 0.05102f, -0.59912f));

    private static readonly FirstPersonArmPoseDefinition AwmLongRifle = new(
        FirstPersonArmPoseKind.LongRifle,
        new Vector3(0.0f, -0.06961f, -0.06117f),
        new Vector3(-0.05723f, 0.01712f, -0.92248f));

    public static FirstPersonArmPoseDefinition For(WeaponPlatform platform)
        => platform switch
        {
            WeaponPlatform.P226 => P226Sidearm,
            WeaponPlatform.M1911 => M1911Sidearm,
            WeaponPlatform.GSh18 => Gsh18Sidearm,
            WeaponPlatform.DesertEagle => LargeSidearm,
            WeaponPlatform.MP5A5 => Mp5Compact,
            WeaponPlatform.M3A1 => M3Compact,
            WeaponPlatform.M4A1 => M4Rifle,
            WeaponPlatform.AK74 => AkRifle,
            WeaponPlatform.ScarL => ScarRifle,
            WeaponPlatform.AWM => AwmLongRifle,
            WeaponPlatform.M24 => M24LongRifle,
            WeaponPlatform.AXMC => AxmcLongRifle,
            WeaponPlatform.VSS => VssRifle,
            _ => M4Rifle
        };
}
