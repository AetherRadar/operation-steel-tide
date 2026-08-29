using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private static readonly Vector3 PlatformMagazineHome = new(0.0f, -0.20f, -0.31f);
    private static readonly Vector3 PlatformSpareMagazineHome = new(-0.30f, -0.62f, -0.18f);
    private static readonly Vector3 PlatformChargingHandleHome = new(0.075f, 0.085f, -0.05f);
    private bool _reloadStartedEmpty;

    internal bool ReloadStartedEmptyForDiagnostics => _reloadStartedEmpty;

    private bool UsesPlatformReloadPresentation()
        => EquippedWeapon.Platform != WeaponPlatform.M3A1
            && !WeaponCatalog.IsSidearm(EquippedWeapon.Platform);

    private bool UsesSidearmReloadPresentation()
        => WeaponCatalog.IsSidearm(EquippedWeapon.Platform);

    private void UpdateSidearmReloadAnimation()
        => UpdateProfiledReloadAnimation();

    private void UpdatePlatformReloadAnimation()
        => UpdateProfiledReloadAnimation();

    private void UpdateProfiledReloadAnimation()
    {
        if (!_isReloading || EquippedWeapon.Platform == WeaponPlatform.M3A1)
        {
            return;
        }

        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        var profile = FirstPersonReloadProfileCatalog.For(EquippedWeapon.Platform);
        var wellGrip = profile.MagazineHome + profile.MagazineGripOffset;
        var extractedGrip = profile.ExtractedMagazine + profile.MagazineGripOffset;
        var stowedGrip = profile.StowedMagazine + profile.MagazineGripOffset;
        var spareGrip = profile.SpareMagazineHome + profile.MagazineGripOffset;
        var readyGrip = profile.ReadyMagazine + profile.MagazineGripOffset;

        if (profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine)
        {
            UpdateInternalMagazineReloadAnimation(profile, progress);
            UpdateProfiledFallbackForearm();
            return;
        }

        _magazine.Visible = progress < profile.StowEnd;
        _magazine.Position = profile.MagazineHome;
        _magazine.Rotation = profile.MagazineRotation;
        _spareMagazine.Visible = progress >= profile.StowEnd;
        _spareMagazine.Position = profile.SpareMagazineHome;
        _spareMagazine.Rotation = profile.StowedRotation;
        _chargingHandle.Position = profile.ActionHome;
        _supportHand.Position = profile.SupportHome;
        _supportHand.Rotation = profile.SupportRotation;

        if (progress < profile.ReachEnd)
        {
            var t = SmoothSegment(progress, 0.0f, profile.ReachEnd);
            _supportHand.Position = profile.SupportHome.Lerp(wellGrip, t);
            _supportHand.Rotation = profile.SupportRotation.Lerp(
                profile.MagazineHandRotation,
                t);
        }
        else if (progress < profile.ExtractEnd)
        {
            var t = SmoothSegment(progress, profile.ReachEnd, profile.ExtractEnd);
            ApplyMagazineExtraction(profile, t);
            _supportHand.Position = _magazine.Position + profile.MagazineGripOffset;
            _supportHand.Rotation = profile.MagazineHandRotation;
            PlayPlatformReloadSound(0, progress, profile.ReachEnd + 0.08f, 0.91f);
        }
        else if (progress < profile.StowEnd)
        {
            var t = SmoothSegment(progress, profile.ExtractEnd, profile.StowEnd);
            _magazine.Position = profile.ExtractedMagazine.Lerp(profile.StowedMagazine, t);
            _magazine.Rotation = profile.ExtractedRotation.Lerp(profile.StowedRotation, t);
            _supportHand.Position = extractedGrip.Lerp(stowedGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.AcquireEnd)
        {
            // Keep the removed magazine at its pouch endpoint while the hand
            // changes over to the spare. Resetting this hidden node to the
            // magwell made the physical hand target jump at StowEnd.
            _magazine.Position = profile.StowedMagazine;
            _magazine.Rotation = profile.StowedRotation;
            var t = SmoothSegment(progress, profile.StowEnd, profile.AcquireEnd);
            _supportHand.Position = stowedGrip.Lerp(spareGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.InsertEnd)
        {
            var t = SmoothSegment(progress, profile.AcquireEnd, profile.InsertEnd);
            _spareMagazine.Position = profile.SpareMagazineHome.Lerp(profile.ReadyMagazine, t);
            _spareMagazine.Rotation = profile.StowedRotation.Lerp(
                profile.ExtractedRotation,
                t);
            _supportHand.Position = spareGrip.Lerp(readyGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.SeatEnd)
        {
            var t = SmoothSegment(progress, profile.InsertEnd, profile.SeatEnd);
            ApplyMagazineInsertion(profile, t);
            _supportHand.Position = _spareMagazine.Position + profile.MagazineGripOffset;
            _supportHand.Rotation = profile.MagazineHandRotation;
            PlayPlatformReloadSound(1, progress, profile.InsertEnd + 0.035f, 1.04f);
        }
        else if (progress < profile.ActionEnd)
        {
            UpdateReloadAction(profile, progress, wellGrip);
        }
        else
        {
            var usesAction = profile.UsesAction(_reloadStartedEmpty);
            var fromAction = usesAction ? profile.ActionGrip : wellGrip;
            var fromRotation = usesAction
                ? profile.ActionHandRotation
                : profile.MagazineHandRotation;
            var t = SmoothSegment(progress, profile.ActionEnd, 1.0f);
            _spareMagazine.Position = profile.MagazineHome;
            _spareMagazine.Rotation = profile.MagazineRotation;
            _supportHand.Position = fromAction.Lerp(profile.SupportHome, t);
            _supportHand.Rotation = fromRotation.Lerp(profile.SupportRotation, t);
        }

        // These nodes remain invisible compatibility targets when the authored
        // skinned reload rig is active. The fallback pose is deterministic, but
        // production presentation never moves a static whole-arm mesh here.
        UpdateProfiledFallbackForearm();
    }

    private void UpdateInternalMagazineReloadAnimation(
        FirstPersonReloadProfile profile,
        float progress)
    {
        var portGrip = profile.MagazineHome + profile.MagazineGripOffset;
        var pouchGrip = profile.SpareMagazineHome + profile.MagazineGripOffset;
        var stagedGrip = profile.StowedMagazine + profile.MagazineGripOffset;
        var readyGrip = profile.ReadyMagazine + profile.MagazineGripOffset;

        // The M24 keeps its hinged floorplate/internal box in the receiver.
        // Five individually authored cartridges are the replacement loading
        // component and travel from the pouch into the open loading port.
        _magazine.Visible = true;
        _magazine.Position = profile.MagazineHome;
        _magazine.Rotation = profile.MagazineRotation;
        _spareMagazine.Visible = progress >= profile.ReachEnd
            && progress < profile.SeatEnd;
        _spareMagazine.Position = profile.SpareMagazineHome;
        _spareMagazine.Rotation = profile.StowedRotation;
        _chargingHandle.Position = profile.ActionHome;
        _supportHand.Position = profile.SupportHome;
        _supportHand.Rotation = profile.SupportRotation;

        if (progress < profile.ReachEnd)
        {
            var t = SmoothSegment(progress, 0.0f, profile.ReachEnd);
            _supportHand.Position = profile.SupportHome.Lerp(pouchGrip, t);
            _supportHand.Rotation = profile.SupportRotation.Lerp(
                profile.MagazineHandRotation,
                t);
        }
        else if (progress < profile.ExtractEnd)
        {
            var t = SmoothSegment(progress, profile.ReachEnd, profile.ExtractEnd);
            _spareMagazine.Position = profile.SpareMagazineHome.Lerp(
                profile.StowedMagazine,
                t);
            _supportHand.Position = pouchGrip.Lerp(stagedGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
            PlayPlatformReloadSound(0, progress, profile.ReachEnd + 0.06f, 0.96f);
        }
        else if (progress < profile.StowEnd)
        {
            var t = SmoothSegment(progress, profile.ExtractEnd, profile.StowEnd);
            _spareMagazine.Position = profile.StowedMagazine.Lerp(
                profile.ReadyMagazine,
                t);
            _spareMagazine.Rotation = profile.StowedRotation.Lerp(
                profile.ExtractedRotation,
                t);
            _supportHand.Position = stagedGrip.Lerp(readyGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.AcquireEnd)
        {
            _spareMagazine.Position = profile.ReadyMagazine;
            _spareMagazine.Rotation = profile.ExtractedRotation;
            _supportHand.Position = readyGrip;
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.InsertEnd)
        {
            var t = SmoothSegment(progress, profile.AcquireEnd, profile.InsertEnd);
            _spareMagazine.Position = profile.ReadyMagazine.Lerp(
                profile.MagazineHome,
                t);
            _spareMagazine.Rotation = profile.ExtractedRotation.Lerp(
                profile.MagazineRotation,
                t);
            _supportHand.Position = readyGrip.Lerp(portGrip, t);
            _supportHand.Rotation = profile.MagazineHandRotation;
        }
        else if (progress < profile.SeatEnd)
        {
            var t = SmoothSegment(progress, profile.InsertEnd, profile.SeatEnd);
            var feedPulse = Mathf.Sin(t * Mathf.Pi * 5.0f) * (1.0f - t) * 0.012f;
            _spareMagazine.Position = profile.MagazineHome
                + Vector3.Forward * feedPulse;
            _spareMagazine.Rotation = profile.MagazineRotation;
            _supportHand.Position = _spareMagazine.Position
                + profile.MagazineGripOffset;
            _supportHand.Rotation = profile.MagazineHandRotation;
            PlayPlatformReloadSound(1, progress, profile.InsertEnd + 0.025f, 1.08f);
        }
        else if (progress < profile.ActionEnd)
        {
            UpdateReloadAction(profile, progress, portGrip);
        }
        else
        {
            var t = SmoothSegment(progress, profile.ActionEnd, 1.0f);
            _supportHand.Position = profile.ActionGrip.Lerp(profile.SupportHome, t);
            _supportHand.Rotation = profile.ActionHandRotation.Lerp(
                profile.SupportRotation,
                t);
        }
    }

    private void UpdateProfiledFallbackForearm()
    {
        var sidearm = WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        _supportForearm.Position = _supportHand.Position
            + (sidearm
                ? new Vector3(-0.08f, -0.23f, 0.10f)
                : new Vector3(-0.09f, -0.24f, 0.10f));
        _supportForearm.Rotation = sidearm
            ? new Vector3(0.18f, 0.06f, -0.24f)
            : new Vector3(0.22f, 0.05f, -0.28f);
    }

    private void ApplyMagazineExtraction(FirstPersonReloadProfile profile, float t)
    {
        if (profile.Mechanism == FirstPersonReloadMechanism.RockAndLockMagazine)
        {
            var rock = SmoothStep(Mathf.Clamp(t * 1.8f, 0.0f, 1.0f));
            var pull = SmoothStep(Mathf.Clamp((t - 0.25f) / 0.75f, 0.0f, 1.0f));
            _magazine.Position = profile.MagazineHome.Lerp(profile.ExtractedMagazine, pull);
            _magazine.Rotation = profile.MagazineRotation.Lerp(
                profile.ExtractedRotation,
                rock);
            return;
        }

        _magazine.Position = profile.MagazineHome.Lerp(profile.ExtractedMagazine, t);
        _magazine.Rotation = profile.MagazineRotation.Lerp(profile.ExtractedRotation, t);
    }

    private void ApplyMagazineInsertion(FirstPersonReloadProfile profile, float t)
    {
        if (profile.Mechanism == FirstPersonReloadMechanism.RockAndLockMagazine)
        {
            var hook = SmoothStep(Mathf.Clamp(t / 0.62f, 0.0f, 1.0f));
            var lockRotation = SmoothStep(Mathf.Clamp((t - 0.35f) / 0.65f, 0.0f, 1.0f));
            _spareMagazine.Position = profile.ReadyMagazine.Lerp(profile.MagazineHome, hook);
            _spareMagazine.Rotation = profile.ExtractedRotation.Lerp(
                profile.MagazineRotation,
                lockRotation);
            return;
        }

        // A short final over-travel makes the magazine visibly seat instead of
        // dissolving into the magwell at constant speed.
        var seat = SmoothStep(Mathf.Clamp(t / 0.82f, 0.0f, 1.0f));
        var impact = Mathf.Sin(Mathf.Clamp((t - 0.80f) / 0.20f, 0.0f, 1.0f) * Mathf.Pi)
            * 0.008f;
        _spareMagazine.Position = profile.ReadyMagazine.Lerp(profile.MagazineHome, seat)
            + Vector3.Down * impact;
        _spareMagazine.Rotation = profile.ExtractedRotation.Lerp(
            profile.MagazineRotation,
            seat);
    }

    private void UpdateReloadAction(
        FirstPersonReloadProfile profile,
        float progress,
        Vector3 wellGrip)
    {
        _spareMagazine.Position = profile.MagazineHome;
        _spareMagazine.Rotation = profile.MagazineRotation;
        if (!profile.UsesAction(_reloadStartedEmpty))
        {
            var t = SmoothSegment(progress, profile.SeatEnd, profile.ActionEnd);
            _supportHand.Position = wellGrip.Lerp(profile.SupportHome, t);
            _supportHand.Rotation = profile.MagazineHandRotation.Lerp(
                profile.SupportRotation,
                t);
            return;
        }

        var actionProgress = Mathf.InverseLerp(profile.SeatEnd, profile.ActionEnd, progress);
        var reach = SmoothStep(Mathf.Clamp(actionProgress / 0.36f, 0.0f, 1.0f));
        var cycle = Mathf.Clamp((actionProgress - 0.34f) / 0.66f, 0.0f, 1.0f);
        var travel = Mathf.Sin(cycle * Mathf.Pi);
        _supportHand.Position = wellGrip.Lerp(profile.ActionGrip, reach);
        _supportHand.Rotation = profile.MagazineHandRotation.Lerp(
            profile.ActionHandRotation,
            reach);
        _chargingHandle.Position = profile.ActionHome + profile.ActionTravel * travel;
        PlayPlatformReloadSound(2, progress, profile.SeatEnd + 0.065f, 1.10f);
    }

    private void ResetProfiledReloadRig()
    {
        var profile = FirstPersonReloadProfileCatalog.For(EquippedWeapon.Platform);
        _magazine.Visible = true;
        _magazine.Position = profile.MagazineHome;
        _magazine.Rotation = profile.MagazineRotation;
        _spareMagazine.Visible = false;
        _spareMagazine.Position = profile.SpareMagazineHome;
        _spareMagazine.Rotation = profile.StowedRotation;
        _chargingHandle.Position = profile.ActionHome;
        _supportHand.Position = profile.SupportHome;
        _supportHand.Rotation = profile.SupportRotation;
        _reloadStartedEmpty = false;
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

    private static float SmoothSegment(float value, float start, float end)
        => SmoothStep(Mathf.InverseLerp(start, end, value));

    private Vector3 SidearmReloadViewPositionOffset()
    {
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        var emphasis = Mathf.Sin(progress * Mathf.Pi);
        var exchange = ReloadExchangeWorkspaceEnvelope(progress);
        var extractionLift = EquippedWeapon.Platform == WeaponPlatform.DesertEagle
            ? 0.170f
            : 0.150f;
        // Lift the pistol into the player's working space while the support
        // hand exchanges magazines. A phase-shaped extraction lift keeps the
        // removed magazine, feed lips, and gripping palm above the footer HUD;
        // it fades before the slide/charging action so the pistol never floats
        // in the centre of the view for the remainder of the reload.
        // Move the rig slightly away from the near plane as it rises. Keeping
        // the elbows in front of the camera is what prevents a large pistol
        // pose from turning into isolated hands or clipped sleeve openings.
        return new Vector3(-0.025f, 0.260f, -0.080f) * emphasis
            + Vector3.Up * extractionLift * exchange;
    }

    private Vector3 SidearmReloadViewRotation()
    {
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        var emphasis = Mathf.Sin(progress * Mathf.Pi);
        return new Vector3(-0.055f, 0.025f, -0.075f) * emphasis;
    }

    private Vector3 PlatformReloadViewPositionOffset()
    {
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
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
            WeaponPlatform.VSS => 0.080f,
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
        var leftGrip = animatedArms?.LeftPalmContactGlobalPosition
            ?? arms!.LeftGripFrame.GlobalPosition;
        var supportTarget = ReloadSupportTargetGlobal();
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
        var leftPalmPosition = animatedArms?.LeftPalmContactGlobalPosition
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
            leftPalmPosition);
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
