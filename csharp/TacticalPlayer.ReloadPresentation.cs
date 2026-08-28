using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 PlatformMagazineHome = new(0.0f, -0.2f, -0.31f);
    private static readonly Vector3 PlatformSpareMagazineHome = new(-0.3f, -0.62f, -0.18f);
    private static readonly Vector3 PlatformChargingHandleHome = new(0.075f, 0.085f, -0.05f);

    private bool UsesPlatformReloadPresentation()
        => EquippedWeapon.Platform is WeaponPlatform.AK74
            or WeaponPlatform.ScarL
            or WeaponPlatform.MP5A5
            or WeaponPlatform.VSS;

    private void UpdatePlatformReloadAnimation()
    {
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        var magazineRotation = PlatformMagazineRotation();
        var gripOffset = PlatformMagazineGripOffset();
        var handHome = PlatformSupportHandHome();
        var handAtWell = PlatformMagazineHome + gripOffset;
        var removedMagazine = PlatformRemovedMagazinePosition();
        var pocketMagazine = PlatformPocketMagazinePosition();
        var handleGrip = PlatformChargingHandleGrip();

        // Re-establish every transform for fixed-progress diagnostics and normal
        // playback alike. The old non-M4 path never drove these mechanism nodes,
        // so the support hand stayed on the fore-end while the weapon floated.
        // Keep a magazine in the support hand for the complete pouch transfer.
        // The former 0.38-0.50 gap hid both magazine nodes while the hand still
        // travelled downward, producing a conspicuous empty-hand pop.
        _magazine.Visible = progress < 0.50f || progress >= 0.78f;
        _magazine.Position = progress is >= 0.38f and < 0.78f
            ? removedMagazine
            : PlatformMagazineHome;
        _magazine.Rotation = progress is >= 0.38f and < 0.78f
            ? RemovedMagazineRotation()
            : magazineRotation;
        _spareMagazine.Visible = progress is >= 0.50f and < 0.78f;
        _spareMagazine.Position = PlatformSpareMagazineHome;
        _spareMagazine.Rotation = PocketMagazineRotation();
        _chargingHandle.Position = PlatformChargingHandleHome;
        _supportHand.Position = handHome;
        _supportHand.Rotation = PlatformSupportHandRestRotation();

        if (progress < 0.15f)
        {
            var t = SmoothStep(progress / 0.15f);
            _supportHand.Position = handHome.Lerp(handAtWell, t);
            _supportHand.Rotation = PlatformSupportHandRestRotation()
                .Lerp(PlatformMagazineHandRotation(), t);
        }
        else if (progress < 0.38f)
        {
            var t = SmoothStep((progress - 0.15f) / 0.23f);
            _magazine.Position = PlatformMagazineHome.Lerp(removedMagazine, t);
            _magazine.Rotation = magazineRotation.Lerp(RemovedMagazineRotation(), t);
            _supportHand.Position = _magazine.Position + gripOffset;
            _supportHand.Rotation = PlatformMagazineHandRotation();
            PlayPlatformReloadSound(stage: 0, progress, threshold: 0.29f, pitch: 0.90f);
        }
        else if (progress < 0.50f)
        {
            var t = SmoothStep((progress - 0.38f) / 0.12f);
            _magazine.Position = removedMagazine.Lerp(pocketMagazine, t);
            _magazine.Rotation = RemovedMagazineRotation()
                .Lerp(PocketMagazineRotation(), t);
            _supportHand.Position = _magazine.Position + gripOffset;
            _supportHand.Rotation = PlatformMagazineHandRotation();
        }
        else if (progress < 0.78f)
        {
            var t = SmoothStep((progress - 0.50f) / 0.28f);
            _spareMagazine.Position = pocketMagazine.Lerp(PlatformMagazineHome, t);
            _spareMagazine.Rotation = PocketMagazineRotation().Lerp(magazineRotation, t);
            _supportHand.Position = _spareMagazine.Position + gripOffset;
            _supportHand.Rotation = PlatformMagazineHandRotation();
            PlayPlatformReloadSound(stage: 1, progress, threshold: 0.72f, pitch: 1.04f);
        }
        else if (progress < 0.90f)
        {
            var t = SmoothStep((progress - 0.78f) / 0.12f);
            _supportHand.Position = handAtWell.Lerp(handleGrip, t);
            _supportHand.Rotation = PlatformMagazineHandRotation()
                .Lerp(PlatformChargingHandRotation(), t);
            _chargingHandle.Position = PlatformChargingHandleHome.Lerp(
                PlatformChargingHandleHome + new Vector3(0.0f, 0.0f, 0.12f),
                t);
        }
        else
        {
            var t = SmoothStep((progress - 0.90f) / 0.10f);
            _chargingHandle.Position = (PlatformChargingHandleHome
                    + new Vector3(0.0f, 0.0f, 0.12f))
                .Lerp(PlatformChargingHandleHome, t);
            _supportHand.Position = handleGrip.Lerp(handHome, t);
            _supportHand.Rotation = PlatformChargingHandRotation()
                .Lerp(PlatformSupportHandRestRotation(), t);
        }

        _supportForearm.Position = _supportHand.Position + new Vector3(-0.09f, -0.24f, 0.1f);
        _supportForearm.Rotation = new Vector3(0.22f, 0.05f, -0.28f);
    }

    private void PlayPlatformReloadSound(
        int stage,
        float progress,
        float threshold,
        float pitch)
    {
        if (_reloadSoundStage != stage || progress <= threshold)
        {
            return;
        }

        _reloadSoundStage = stage + 1;
        if (IsInstanceValid(_reloadAudio))
        {
            _reloadAudio.PitchScale = pitch;
            _reloadAudio.Play();
        }
    }

    private Vector3 PlatformSupportHandHome()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.MP5A5 => new Vector3(-0.035f, -0.19f, -0.42f),
            WeaponPlatform.VSS => new Vector3(-0.02f, -0.18f, -0.62f),
            _ => RifleForegripAnchor
        };

    private Vector3 PlatformMagazineGripOffset()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Vector3(-0.055f, 0.075f, -0.025f),
            WeaponPlatform.MP5A5 => new Vector3(-0.045f, 0.06f, 0.005f),
            _ => new Vector3(-0.05f, 0.07f, -0.015f)
        };

    private Vector3 PlatformRemovedMagazinePosition()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Vector3(-0.11f, -0.46f, -0.37f),
            WeaponPlatform.MP5A5 => new Vector3(-0.08f, -0.43f, -0.33f),
            _ => new Vector3(-0.10f, -0.44f, -0.36f)
        };

    private Vector3 PlatformPocketMagazinePosition()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.MP5A5 => new Vector3(-0.22f, -0.55f, -0.30f),
            _ => new Vector3(-0.23f, -0.56f, -0.34f)
        };

    private Vector3 PlatformChargingHandleGrip()
        => EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Vector3(0.08f, 0.015f, -0.03f),
            WeaponPlatform.MP5A5 => new Vector3(-0.08f, 0.035f, -0.28f),
            _ => new Vector3(0.015f, 0.025f, -0.08f)
        };

    private Vector3 PlatformMagazineRotation()
        => EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Vector3(-0.29f, 0.0f, 0.0f)
            : new Vector3(-0.19f, 0.0f, 0.0f);

    private Vector3 RemovedMagazineRotation()
        => EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Vector3(0.68f, 0.10f, 0.42f)
            : new Vector3(0.56f, 0.08f, 0.32f);

    private Vector3 PocketMagazineRotation()
        => EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Vector3(0.45f, -0.04f, 0.34f)
            : new Vector3(0.34f, 0.0f, 0.30f);

    private static Vector3 PlatformSupportHandRestRotation()
        => new(0.20f, 0.0f, 0.05f);

    private static Vector3 PlatformMagazineHandRotation()
        => new(0.43f, 0.08f, 0.22f);

    private static Vector3 PlatformChargingHandRotation()
        => new(0.36f, -0.10f, 0.12f);

    internal AuthoredPlatformReloadInspection InspectAuthoredPlatformReloadForDiagnostics()
    {
        var arms = ActiveAuthoredArms();
        if (!UsesPlatformReloadPresentation()
            || arms is null
            || !IsInstanceValid(arms.Root)
            || !_authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var weapon)
            || !IsInstanceValid(weapon.Root))
        {
            return default;
        }

        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var rightGrip = arms.RightGripFrame.GlobalPosition;
        var leftGrip = arms.LeftGripFrame.GlobalPosition;
        var supportTarget = ReloadSupportTargetGlobal();
        var primaryMagazine = weapon.Magazine.GlobalPosition;
        var spareMagazine = weapon.SpareMagazine.GlobalPosition;
        var activeMagazine = weapon.SpareMagazine.Visible
            ? spareMagazine
            : primaryMagazine;
        var logicalViewportSize = _camera.GetViewport().GetVisibleRect().Size;
        var windowSize = GetWindow().Size;
        var screenSize = new Vector2(windowSize.X, windowSize.Y);
        return new AuthoredPlatformReloadInspection(
            arms.Root.IsVisibleInTree()
                && arms.RightArm.IsVisibleInTree()
                && arms.LeftArm.IsVisibleInTree()
                && UsesAuthoredHandRigForDiagnostics,
            weapon.Magazine.Visible,
            weapon.SpareMagazine.Visible,
            weapon.Magazine.GetInstanceId() != weapon.SpareMagazine.GetInstanceId(),
            weapon.HasVisibleMagazineMechanism,
            rightGrip,
            leftGrip,
            supportTarget,
            primaryMagazine,
            spareMagazine,
            (weaponRootInverse * rightGrip).DistanceTo(pose.PrimaryGrip),
            leftGrip.DistanceTo(supportTarget),
            leftGrip.DistanceTo(activeMagazine),
            arms.LeftPalmFrame.GlobalPosition.DistanceTo(arms.LeftWristFrame.GlobalPosition),
            arms.RightPalmFrame.GlobalPosition.DistanceTo(arms.RightWristFrame.GlobalPosition),
            arms.RightArm.Transform,
            arms.LeftArm.Transform,
            WeaponViewPositionTarget(),
            WeaponViewRotationTarget(),
            InspectVisibleMeshScreenProjection(arms.RightArm, logicalViewportSize, screenSize),
            InspectVisibleMeshScreenProjection(arms.LeftArm, logicalViewportSize, screenSize));
    }
}

internal readonly record struct AuthoredPlatformReloadInspection(
    bool AuthoredArmsActive,
    bool PrimaryMagazineVisible,
    bool SpareMagazineVisible,
    bool SeparateMagazineNodes,
    bool VisibleMagazineMechanism,
    Vector3 RightGrip,
    Vector3 LeftGrip,
    Vector3 SupportTarget,
    Vector3 PrimaryMagazinePosition,
    Vector3 SpareMagazinePosition,
    float RightGripResidual,
    float SupportTargetDistance,
    float ActiveMagazineDistance,
    float LeftSleeveWristLength,
    float RightSleeveWristLength,
    Transform3D RightArmTransform,
    Transform3D LeftArmTransform,
    Vector3 ReloadViewTarget,
    Vector3 ReloadRotationTarget,
    VisibleMeshScreenProjection RightArmScreen,
    VisibleMeshScreenProjection LeftArmScreen)
{
    public bool SleevesReachFrameBottom
        => RightArmScreen.Available
            && LeftArmScreen.Available
            && RightArmScreen.BottomGapRatio <= 0.05f
            && LeftArmScreen.BottomGapRatio <= 0.08f;
}
