using Godot;

namespace OperationSteelTide;

internal enum FirstPersonReloadMechanism
{
    StraightMagazine,
    RockAndLockMagazine,
    HkSlapMagazine,
    PistolMagazine,
    PrecisionMagazine,
    InternalMagazine
}

internal readonly record struct FirstPersonReloadProfile(
    WeaponPlatform Platform,
    FirstPersonReloadMechanism Mechanism,
    string ClipStem,
    Vector3 MagazineHome,
    Vector3 SpareMagazineHome,
    Vector3 MagazineGripOffset,
    Vector3 ExtractedMagazine,
    Vector3 StowedMagazine,
    Vector3 ReadyMagazine,
    Vector3 MagazineRotation,
    Vector3 ExtractedRotation,
    Vector3 StowedRotation,
    Vector3 ActionHome,
    Vector3 ActionTravel,
    Vector3 ActionGrip,
    Vector3 SupportHome,
    Vector3 SupportRotation,
    Vector3 MagazineHandRotation,
    Vector3 ActionHandRotation,
    float ReachEnd,
    float ExtractEnd,
    float StowEnd,
    float AcquireEnd,
    float InsertEnd,
    float SeatEnd,
    float ActionEnd,
    bool TacticalAction)
{
    public string ClipName(bool emptyReload)
        => $"reload_{ClipStem}_{(emptyReload ? "empty" : "tactical")}";

    public bool UsesAction(bool emptyReload)
        => emptyReload || TacticalAction;
}

internal static class FirstPersonReloadProfileCatalog
{
    private static readonly Vector3 SharedMagazineHome = new(0.0f, -0.20f, -0.31f);
    private static readonly Vector3 SharedSpareHome = new(-0.30f, -0.62f, -0.18f);
    private static readonly Vector3 SharedActionHome = new(0.075f, 0.085f, -0.05f);

    public static FirstPersonReloadProfile For(WeaponPlatform platform)
        => platform switch
        {
            WeaponPlatform.M4A1 => Rifle(
                platform,
                "m4a1",
                new Vector3(-0.060f, 0.080f, -0.020f),
                new Vector3(-0.10f, -0.43f, -0.38f),
                new Vector3(-0.23f, -0.57f, -0.36f),
                new Vector3(-0.14f, -0.32f, -0.36f),
                new Vector3(0.010f, 0.020f, -0.020f),
                new Vector3(0.0f, 0.0f, 0.115f)),
            WeaponPlatform.AK74 => Rifle(
                platform,
                "ak74",
                new Vector3(-0.055f, 0.075f, -0.025f),
                new Vector3(-0.11f, -0.46f, -0.37f),
                new Vector3(-0.24f, -0.58f, -0.35f),
                new Vector3(-0.13f, -0.34f, -0.36f),
                new Vector3(0.080f, 0.015f, -0.030f),
                new Vector3(0.0f, 0.0f, 0.120f),
                FirstPersonReloadMechanism.RockAndLockMagazine,
                magazineRotation: new Vector3(-0.29f, 0.0f, 0.0f),
                extractedRotation: new Vector3(0.68f, 0.10f, 0.42f),
                stowedRotation: new Vector3(0.45f, -0.04f, 0.34f)),
            WeaponPlatform.ScarL => Rifle(
                platform,
                "scarl",
                new Vector3(-0.050f, 0.070f, -0.015f),
                new Vector3(-0.09f, -0.44f, -0.35f),
                new Vector3(-0.22f, -0.56f, -0.34f),
                new Vector3(-0.12f, -0.33f, -0.35f),
                new Vector3(0.020f, 0.035f, -0.090f),
                new Vector3(0.0f, 0.0f, 0.105f)),
            WeaponPlatform.MP5A5 => Rifle(
                platform,
                "mp5a5",
                new Vector3(-0.045f, 0.060f, 0.005f),
                new Vector3(-0.08f, -0.43f, -0.33f),
                new Vector3(-0.22f, -0.55f, -0.30f),
                new Vector3(-0.11f, -0.32f, -0.32f),
                new Vector3(-0.080f, 0.035f, -0.280f),
                new Vector3(0.0f, 0.0f, 0.105f),
                FirstPersonReloadMechanism.HkSlapMagazine),
            WeaponPlatform.VSS => Rifle(
                platform,
                "vss",
                new Vector3(-0.050f, 0.070f, -0.015f),
                new Vector3(-0.10f, -0.44f, -0.36f),
                new Vector3(-0.23f, -0.56f, -0.34f),
                new Vector3(-0.13f, -0.33f, -0.35f),
                new Vector3(0.020f, 0.030f, -0.090f),
                new Vector3(0.0f, 0.0f, 0.100f),
                FirstPersonReloadMechanism.RockAndLockMagazine,
                magazineRotation: new Vector3(-0.25f, 0.0f, 0.0f),
                extractedRotation: new Vector3(0.56f, 0.08f, 0.32f),
                stowedRotation: new Vector3(0.34f, 0.0f, 0.30f)),
            WeaponPlatform.M24 => Precision(
                platform,
                "m24",
                -0.39f,
                FirstPersonReloadMechanism.InternalMagazine,
                tacticalAction: true),
            WeaponPlatform.AXMC => Precision(platform, "axmc", -0.41f),
            WeaponPlatform.AWM => Precision(platform, "awm", -0.44f),
            WeaponPlatform.P226 => Sidearm(platform, "p226"),
            WeaponPlatform.M1911 => Sidearm(platform, "m1911"),
            WeaponPlatform.GSh18 => Sidearm(platform, "gsh18"),
            WeaponPlatform.DesertEagle => Sidearm(platform, "desert_eagle"),
            WeaponPlatform.M3A1 => Rifle(
                platform,
                "m3a1",
                new Vector3(-0.045f, 0.060f, 0.005f),
                new Vector3(-0.08f, -0.43f, -0.33f),
                new Vector3(-0.22f, -0.55f, -0.30f),
                new Vector3(-0.11f, -0.32f, -0.32f),
                new Vector3(-0.080f, 0.035f, -0.280f),
                new Vector3(0.0f, 0.0f, 0.105f)),
            _ => Rifle(
                WeaponPlatform.M4A1,
                "m4a1",
                new Vector3(-0.060f, 0.080f, -0.020f),
                new Vector3(-0.10f, -0.43f, -0.38f),
                new Vector3(-0.23f, -0.57f, -0.36f),
                new Vector3(-0.14f, -0.32f, -0.36f),
                new Vector3(0.010f, 0.020f, -0.020f),
                new Vector3(0.0f, 0.0f, 0.115f))
        };

    private static FirstPersonReloadProfile Rifle(
        WeaponPlatform platform,
        string clipStem,
        Vector3 gripOffset,
        Vector3 extracted,
        Vector3 stowed,
        Vector3 ready,
        Vector3 actionGrip,
        Vector3 actionTravel,
        FirstPersonReloadMechanism mechanism = FirstPersonReloadMechanism.StraightMagazine,
        Vector3? magazineRotation = null,
        Vector3? extractedRotation = null,
        Vector3? stowedRotation = null)
        => new(
            platform,
            mechanism,
            clipStem,
            SharedMagazineHome,
            SharedSpareHome,
            gripOffset,
            extracted,
            stowed,
            ready,
            magazineRotation ?? new Vector3(-0.19f, 0.0f, 0.0f),
            extractedRotation ?? new Vector3(0.56f, 0.08f, 0.32f),
            stowedRotation ?? new Vector3(0.34f, 0.0f, 0.30f),
            SharedActionHome,
            actionTravel,
            actionGrip,
            FirstPersonArmPoseCatalog.For(platform).SupportGrip,
            new Vector3(0.20f, 0.0f, 0.05f),
            new Vector3(0.43f, 0.08f, 0.22f),
            new Vector3(0.36f, -0.10f, 0.12f),
            0.12f,
            0.30f,
            0.42f,
            0.53f,
            0.70f,
            0.77f,
            0.91f,
            false);

    private static FirstPersonReloadProfile Precision(
        WeaponPlatform platform,
        string clipStem,
        float magazineDepth,
        FirstPersonReloadMechanism mechanism = FirstPersonReloadMechanism.PrecisionMagazine,
        bool tacticalAction = false)
        => new(
            platform,
            mechanism,
            clipStem,
            SharedMagazineHome,
            SharedSpareHome,
            new Vector3(-0.045f, 0.065f, -0.005f),
            new Vector3(-0.08f, -0.43f, magazineDepth),
            new Vector3(-0.22f, -0.57f, magazineDepth + 0.03f),
            new Vector3(-0.11f, -0.32f, magazineDepth),
            new Vector3(-0.14f, 0.0f, 0.0f),
            new Vector3(0.44f, 0.06f, 0.25f),
            new Vector3(0.30f, 0.0f, 0.28f),
            SharedActionHome,
            new Vector3(0.0f, 0.0f, 0.135f),
            new Vector3(0.055f, 0.055f, -0.015f),
            FirstPersonArmPoseCatalog.For(platform).SupportGrip,
            new Vector3(0.18f, 0.0f, 0.04f),
            new Vector3(0.40f, 0.06f, 0.20f),
            new Vector3(0.34f, -0.12f, 0.08f),
            0.12f,
            0.29f,
            0.41f,
            0.52f,
            0.69f,
            0.76f,
            0.92f,
            tacticalAction);

    private static FirstPersonReloadProfile Sidearm(
        WeaponPlatform platform,
        string clipStem)
        => new(
            platform,
            FirstPersonReloadMechanism.PistolMagazine,
            clipStem,
            SharedMagazineHome,
            new Vector3(-0.08f, -0.46f, -0.27f),
            new Vector3(0.035f, 0.0f, 0.315f),
            // Pull the installed magazine visibly down out of the grip before
            // it disappears. The old path moved upward first, so the new
            // magazine appeared to brush an unchanged magazine in the pistol.
            new Vector3(-0.02f, -0.24f, -0.29f),
            new Vector3(-0.08f, -0.46f, -0.27f),
            new Vector3(-0.02f, -0.34f, -0.29f),
            new Vector3(-0.19f, 0.0f, 0.0f),
            // Pistol magazines travel straight through the magwell. Keeping
            // one orientation across extract, pouch, and seat avoids the
            // large grip-socket arc that previously snapped the support hand.
            new Vector3(-0.19f, 0.0f, 0.0f),
            new Vector3(-0.19f, 0.0f, 0.0f),
            SharedActionHome,
            new Vector3(0.0f, 0.0f, 0.085f),
            new Vector3(0.015f, 0.005f, -0.045f),
            FirstPersonArmPoseCatalog.For(platform).SupportGrip,
            new Vector3(0.16f, -0.04f, 0.18f),
            new Vector3(0.52f, 0.10f, 0.32f),
            new Vector3(0.30f, -0.14f, 0.08f),
            0.13f,
            0.31f,
            0.43f,
            0.54f,
            0.71f,
            0.78f,
            0.91f,
            false);
}
