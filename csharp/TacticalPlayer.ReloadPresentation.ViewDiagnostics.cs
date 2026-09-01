using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private Vector3 SidearmReloadViewPositionOffset()
    {
        var progress = Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
        var emphasis = Mathf.Sin(progress * Mathf.Pi);
        // Competitive FPS pistol reloads pull the complete workspace inward
        // and away from the lens. That keeps a real magazine exchange readable
        // while preventing a few centimetres of wrist depth from projecting
        // as a full-screen glove near the camera plane.
        return new Vector3(-0.080f, 0.080f, -0.150f) * emphasis;
    }

    private Vector3 SidearmReloadViewRotation()
    {
        var progress = Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
        var emphasis = Mathf.Sin(progress * Mathf.Pi);
        return new Vector3(-0.055f, 0.025f, -0.075f) * emphasis;
    }

    private Vector3 PlatformReloadViewPositionOffset()
    {
        var progress = Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
        var emphasis = Mathf.Sin(progress * Mathf.Pi);
        var exchange = ReloadExchangeWorkspaceEnvelope(progress);
        var workspaceLift = EquippedWeapon.Platform switch
        {
            WeaponPlatform.M4A1 => 0.130f,
            WeaponPlatform.AXMC or WeaponPlatform.AWM => 0.040f,
            WeaponPlatform.VSS => 0.025f,
            _ => 0.0f
        };
        var exchangeLift = EquippedWeapon.Platform switch
        {
            WeaponPlatform.M4A1 => 0.130f,
            WeaponPlatform.AK74 or WeaponPlatform.MP5A5 => 0.050f,
            WeaponPlatform.M24 => 0.250f,
            WeaponPlatform.AXMC or WeaponPlatform.AWM => 0.100f,
            // The long VSS magazine otherwise leaves the glove and magazine
            // surface on the bottom readability line during extraction.
            WeaponPlatform.VSS => 0.105f,
            _ => 0.0f
        };
        return Vector3.Up * (workspaceLift * emphasis + exchangeLift * exchange);
    }

    private float ReloadExchangeWorkspaceEnvelope(float progress)
    {
        var profile = FirstPersonReloadProfileCatalog.For(
            EquippedWeapon.Platform);
        var enter = SmoothSegment(
            progress,
            Mathf.Max(0.0f, profile.ReachEnd - 0.08f),
            profile.ReachEnd);
        var sidearm = WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        var leave = 1.0f - SmoothSegment(
            progress,
            sidearm ? profile.ExtractEnd : profile.InsertEnd,
            sidearm ? profile.AcquireEnd : profile.SeatEnd);
        return enter * leave;
    }

    internal void SetReloadVariantForDiagnostics(bool emptyReload)
        => _reloadStartedEmpty = emptyReload;

    internal AuthoredPlatformReloadInspection InspectAuthoredPlatformReloadForDiagnostics()
    {
        var arms = ActiveAuthoredArms();
        var animatedArms = UsesAnimatedReloadArmsForDiagnostics
            ? AnimatedReloadArmsForDiagnostics
            : null;
        var weapon = ActiveAuthoredReloadWeapon();
        if ((!UsesPlatformReloadPresentation() && !UsesSidearmReloadPresentation())
            || (animatedArms is null
                && (arms is null || !IsInstanceValid(arms.Root)))
            || weapon is null
            || !IsInstanceValid(weapon.Root))
        {
            return default;
        }

        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var rightGrip = animatedArms?.RightGripFrame.GlobalPosition
            ?? arms!.RightGripFrame.GlobalPosition;
        var leftGrip = animatedArms?.LeftSupportAnchorGlobalPosition(
                EquippedWeapon.Platform)
            ?? arms!.LeftGripFrame.GlobalPosition;
        var supportTarget = animatedArms
            ?.PresentedLeftSupportTargetGlobalPosition
            ?? ReloadSupportTargetGlobal();
        var primaryMagazine = weapon.Magazine.GlobalPosition;
        var spareMagazine = weapon.SpareMagazine.GlobalPosition;
        var activeMagazine = weapon.SpareMagazine.Visible
            ? weapon.SpareMagazine
            : weapon.Magazine;
        var logicalViewportSize = _camera.GetViewport().GetVisibleRect().Size;
        var windowSize = GetWindow().Size;
        var screenSize = new Vector2(windowSize.X, windowSize.Y);
        var rightPalmPosition = animatedArms?.RightPalmContactGlobalPosition
            ?? arms!.RightPalmFrame.GlobalPosition;
        var leftPalmPosition = animatedArms is not null
            && WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
                ? animatedArms.LeftPalmCenterGlobalPosition
                : animatedArms?.LeftPalmCenterGlobalPosition
                    ?? arms!.LeftPalmFrame.GlobalPosition;
        var rightWristPosition = animatedArms?.RightWristGlobalPosition
            ?? arms!.RightWristFrame.GlobalPosition;
        var leftWristPosition = animatedArms?.LeftWristGlobalPosition
            ?? arms!.LeftWristFrame.GlobalPosition;
        var rightArmTransform = animatedArms is not null
            ? weaponRootInverse * animatedArms.RightPalmContactGlobalTransform
            : arms!.RightArm.Transform;
        var leftArmTransform = animatedArms is not null
            ? weaponRootInverse * animatedArms.LeftPalmContactGlobalTransform
            : arms!.LeftArm.Transform;
        var activeMagazineContact = InspectVisibleMeshSurface(
            activeMagazine,
            leftGrip);
        var bodyContinuity = InspectReloadBodyContinuity(
            animatedArms?.Skeleton,
            rightPalmAvailable: true,
            rightPalmPosition,
            leftPalmAvailable: true,
            leftPalmPosition,
            animatedArms?.Mesh);
        var leftPalmViewport = _camera.UnprojectPosition(leftPalmPosition);
        var leftPalmScreen = new Vector2(
            leftPalmViewport.X * screenSize.X / logicalViewportSize.X,
            leftPalmViewport.Y * screenSize.Y / logicalViewportSize.Y);
        return new AuthoredPlatformReloadInspection(
            animatedArms is not null
                ? animatedArms.Root.IsVisibleInTree()
                    && animatedArms.Mesh.IsVisibleInTree()
                : arms!.Root.IsVisibleInTree()
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
            leftPalmScreen,
            primaryMagazine,
            spareMagazine,
            (weaponRootInverse * rightGrip).DistanceTo(pose.PrimaryGrip),
            leftGrip.DistanceTo(supportTarget),
            activeMagazineContact.Distance,
            leftPalmPosition.DistanceTo(leftWristPosition),
            rightPalmPosition.DistanceTo(rightWristPosition),
            rightArmTransform,
            leftArmTransform,
            WeaponViewPositionTarget(),
            WeaponViewRotationTarget(),
            bodyContinuity);
    }

    private AuthoredWeaponVisual? ActiveAuthoredReloadWeapon()
    {
        if (EquippedWeapon.Platform == WeaponPlatform.M4A1
            && IsInstanceValid(_authoredPrimaryWeapon?.Root))
        {
            return _authoredPrimaryWeapon;
        }
        return _authoredPlatformWeapons.TryGetValue(EquippedWeapon.Platform, out var weapon)
            && IsInstanceValid(weapon.Root)
                ? weapon
                : null;
    }

    internal SidearmReloadInspection InspectSidearmReloadForDiagnostics()
    {
        var arms = ActiveAuthoredArms();
        if (!UsesSidearmReloadPresentation()
            || arms is null
            || !IsInstanceValid(arms.Root)
            || !IsInstanceValid(ActiveAuthoredWeaponRootForDiagnostics))
        {
            return default;
        }

        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var rightGrip = arms.RightGripFrame.GlobalPosition;
        var leftGrip = arms.LeftGripFrame.GlobalPosition;
        var supportTarget = ReloadSupportTargetGlobal();
        return new SidearmReloadInspection(
            arms.Root.IsVisibleInTree()
                && arms.RightArm.IsVisibleInTree()
                && arms.LeftArm.IsVisibleInTree()
                && UsesAuthoredHandRigForDiagnostics,
            _isReloading,
            ReloadProgress,
            (weaponRootInverse * rightGrip).DistanceTo(pose.PrimaryGrip),
            leftGrip.DistanceTo(supportTarget),
            rightGrip,
            leftGrip,
            supportTarget,
            arms.RightArm.Transform,
            arms.LeftArm.Transform,
            _magazine.Visible,
            _spareMagazine.Visible,
            _chargingHandle.Position.DistanceTo(PlatformChargingHandleHome),
            WeaponViewPositionTarget(),
            WeaponViewRotationTarget());
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
    Vector2 LeftPalmScreen,
    Vector3 PrimaryMagazinePosition,
    Vector3 SpareMagazinePosition,
    float RightGripResidual,
    float SupportTargetDistance,
    float ActiveMagazineSurfaceDistance,
    float LeftSleeveWristLength,
    float RightSleeveWristLength,
    Transform3D RightArmTransform,
    Transform3D LeftArmTransform,
    Vector3 ReloadViewTarget,
    Vector3 ReloadRotationTarget,
    ReloadBodyContinuityInspection BodyContinuity)
{
    public bool SleevesReachFrameBottom
        // Animated arms are a single skinned mesh. Projecting its undeformed
        // source vertices produces a tiny, stale AABB unrelated to the visible
        // sleeves. The posed bone chains instead prove that both weighted arms
        // remain connected through a viewport body edge.
        => BodyContinuity.RightArm.BodyEdgeConnected
            && BodyContinuity.LeftArm.BodyEdgeConnected;
}

internal readonly record struct SidearmReloadInspection(
    bool AuthoredArmsActive,
    bool Reloading,
    float Progress,
    float RightGripResidual,
    float SupportTargetDistance,
    Vector3 RightGrip,
    Vector3 LeftGrip,
    Vector3 SupportTarget,
    Transform3D RightArmTransform,
    Transform3D LeftArmTransform,
    bool PrimaryMagazineVisible,
    bool SpareMagazineVisible,
    float SlideTravel,
    Vector3 ReloadViewTarget,
    Vector3 ReloadRotationTarget);
