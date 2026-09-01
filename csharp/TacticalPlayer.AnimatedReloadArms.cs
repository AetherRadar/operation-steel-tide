using System;
using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private AuthoredAnimatedReloadArmsVisual _authoredAnimatedReloadArms = null!;
    private bool _animatedReloadArmsLoadAttempted;

    internal AuthoredAnimatedReloadArmsVisual? AnimatedReloadArmsForDiagnostics
        => IsInstanceValid(_authoredAnimatedReloadArms?.Root)
            ? _authoredAnimatedReloadArms
            : null;

    internal bool UsesAnimatedReloadArmsForDiagnostics
        => _isReloading
            && EquippedWeapon.Platform != WeaponPlatform.M3A1
            && IsInstanceValid(_authoredAnimatedReloadArms?.Root)
            && _authoredAnimatedReloadArms.Root.IsVisibleInTree();

    internal bool UsesAnimatedSidearmForearmsForDiagnostics
        => UsesAnimatedReloadArmsForDiagnostics
            && _authoredAnimatedReloadArms.UsesSidearmForearms;

    internal bool UsesAnimatedFullReloadArmsForDiagnostics
        => UsesAnimatedReloadArmsForDiagnostics
            && _authoredAnimatedReloadArms.UsesFullArms;

    internal string PresentedReloadClipForDiagnostics
        => AnimatedReloadArmsForDiagnostics?.PresentedClipName ?? string.Empty;

    internal float PresentedReloadClipProgressForDiagnostics
        => AnimatedReloadArmsForDiagnostics?.PresentedClipProgress ?? 0.0f;

    internal AnimatedReloadLeftArmPoseInspection
        InspectAnimatedReloadLeftArmPoseForDiagnostics()
    {
        var arms = AnimatedReloadArmsForDiagnostics;
        if (!UsesAnimatedReloadArmsForDiagnostics || arms is null)
        {
            return default;
        }

        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        Transform3D BoneInWeaponRoot(int bone)
            => weaponRootInverse
                * (arms.Skeleton.GlobalTransform
                    * arms.Skeleton.GetBoneGlobalPose(bone));
        return new AnimatedReloadLeftArmPoseInspection(
            true,
            BoneInWeaponRoot(arms.LeftShoulderBone),
            BoneInWeaponRoot(arms.LeftElbowBone),
            BoneInWeaponRoot(arms.LeftWristBone),
            BoneInWeaponRoot(arms.LeftPalmBone));
    }

    internal SidearmReloadEndpointPoseInspection
        InspectSidearmReloadEndpointPoseForDiagnostics()
    {
        var animatedArms = AnimatedReloadArmsForDiagnostics;
        if (!UsesAnimatedSidearmForearmsForDiagnostics
            || animatedArms is null
            || !IsInstanceValid(_weaponRoot)
            || !IsInstanceValid(animatedArms.LeftWristFrame))
        {
            return default;
        }

        var weaponRootInverse = _weaponRoot.GlobalTransform.AffineInverse();
        var animatedWrist = weaponRootInverse
            * animatedArms.LeftWristFrame.GlobalTransform;
        var animatedPalm = weaponRootInverse
            * animatedArms.LeftPalmContactGlobalTransform;
        return new SidearmReloadEndpointPoseInspection(
            true,
            animatedWrist,
            animatedPalm);
    }

    private void EnsureAuthoredAnimatedReloadArms()
    {
        if (_animatedReloadArmsLoadAttempted
            || IsInstanceValid(_authoredAnimatedReloadArms?.Root))
        {
            return;
        }

        _animatedReloadArmsLoadAttempted = true;
        try
        {
            var arms = CombatModelLibrary.InstantiateAnimatedReloadArms();
            arms.Root.Visible = false;
            _weaponRoot.AddChild(arms.Root);
            _authoredAnimatedReloadArms = arms;
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"Required animated first-person reload arms unavailable: {exception.Message}");
        }
    }

    private bool UpdateAnimatedReloadArmsPresentation()
    {
        var animatedReloadArms = _authoredAnimatedReloadArms;
        var sidearmReload = _isReloading
            && WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        var active = _isReloading
            && EquippedWeapon.Platform != WeaponPlatform.M3A1
            && IsInstanceValid(animatedReloadArms?.Root);
        if (IsInstanceValid(animatedReloadArms?.Root))
        {
            animatedReloadArms.Root.Visible = active;
        }

        var staticArms = ActiveAuthoredArms();
        if (staticArms is not null
            && IsInstanceValid(staticArms.Root))
        {
            // The firing hand stays on the weapon's stable authored pose.  The
            // reload layer owns only the support arm, so two complete arm rigs
            // can never cross or stack in front of the camera.
            staticArms.Root.Visible = true;
            staticArms.RightArm.Visible = true;
            staticArms.LeftArm.Visible = !active;
        }
        if (active && IsInstanceValid(_proceduralFirstPersonArms))
        {
            _proceduralFirstPersonArms.Visible = false;
        }

        if (!active)
        {
            if (sidearmReload)
            {
                // Fail closed when the cropped reload asset is unavailable:
                // keep the pistol visible but hide both complete static arms.
                // Falling back to either full sleeve can expose a stretched
                // limb across the camera near plane.
                if (staticArms is not null && IsInstanceValid(staticArms.Root))
                {
                    staticArms.Root.Visible = false;
                    staticArms.RightArm.Visible = false;
                    staticArms.LeftArm.Visible = false;
                }
                if (IsInstanceValid(_proceduralFirstPersonArms))
                {
                    _proceduralFirstPersonArms.Visible = false;
                }
                return true;
            }
            return false;
        }

        var authoredEmptyReload = _reloadStartedEmpty;
        var authoredArmProgress = DirectReloadArmProgress();
        animatedReloadArms!.SetPresentationPlatform(EquippedWeapon.Platform);
        AlignAnimatedReloadArmsToWeapon(animatedReloadArms);
        animatedReloadArms!.SetReloadProgress(
            EquippedWeapon.Platform,
            authoredEmptyReload,
            authoredArmProgress);
        // The DCC clip owns the complete left-arm performance.  Moving the
        // rigid magazine to that hand is considerably more robust than making
        // the skinned arm chase a second procedural trajectory.
        animatedReloadArms.AcceptAuthoredPose();
        AlignReloadMagazineToAuthoredHand(
            animatedReloadArms,
            authoredArmProgress);
        return true;
    }

    private void AlignReloadMagazineToAuthoredHand(
        AuthoredAnimatedReloadArmsVisual animatedReloadArms,
        float progress)
    {
        var weapon = ActiveAuthoredReloadWeapon();
        if (weapon is null || !IsInstanceValid(weapon.Root))
        {
            return;
        }

        var profile = FirstPersonReloadProfileCatalog.For(
            EquippedWeapon.Platform);
        if (profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine)
        {
            return;
        }

        var carryingRemoved = progress >= profile.ReachEnd
            && progress < profile.StowEnd;
        var carryingReplacement = progress >= profile.AcquireEnd
            && progress < profile.SeatEnd;

        // There is deliberately no visible prop during the pouch hand-off.
        // A magazine is rendered only while installed or while physically held
        // by the support hand; it never follows an independent floating path.
        if (progress >= profile.ReachEnd && progress < profile.AcquireEnd)
        {
            weapon.Magazine.Visible = carryingRemoved;
            weapon.SpareMagazine.Visible = false;
        }
        else if (carryingReplacement)
        {
            weapon.Magazine.Visible = false;
            weapon.SpareMagazine.Visible = true;
        }

        if (!carryingRemoved && !carryingReplacement)
        {
            return;
        }

        var sidearm = WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        var handContact = sidearm
            ? animatedReloadArms.LeftPalmCenterGlobalPosition
            : animatedReloadArms.LeftGripAnchorGlobalPosition;
        weapon.AlignMagazineGripToGlobalPosition(
            spare: carryingReplacement,
            handContact);
    }

    private float DirectReloadArmProgress()
    {
        // The DCC clips are authored per platform and variant. Sampling their
        // real presentation clock is what preserves the reach, exchange,
        // seating and action cadence; holding frame zero turns every reload
        // into the same floating-hand pose.
        return Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
    }

    private float SidearmReloadActionContactBlend()
    {
        if (!_isReloading
            || !_reloadStartedEmpty
            || !WeaponCatalog.IsSidearm(EquippedWeapon.Platform))
        {
            return 0.0f;
        }

        var profile = FirstPersonReloadProfileCatalog.For(
            EquippedWeapon.Platform);
        var progress = Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
        if (progress < profile.InsertEnd)
        {
            return 0.0f;
        }

        var reach = SidearmReloadActionReachBlend(profile, progress);
        var retreat = SidearmReloadReturnBlend(profile, progress);
        return reach * (1.0f - retreat);
    }

    private void AlignAnimatedReloadArmsToWeapon(
        AuthoredAnimatedReloadArmsVisual animatedReloadArms)
    {
        if (!IsInstanceValid(animatedReloadArms.Root))
        {
            return;
        }

        var pose = FirstPersonArmPoseCatalog.For(EquippedWeapon.Platform);
        var inheritedScale = Mathf.Max(0.0001f, _weaponRoot.Scale.X);
        var sidearm = WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        var largeSidearm = EquippedWeapon.Platform == WeaponPlatform.DesertEagle;
        var presentationScale = EquippedWeapon.Platform switch
        {
            WeaponPlatform.ScarL => AnimatedScarReloadArmPresentationScale,
            WeaponPlatform.AWM => AnimatedAwmReloadArmPresentationScale,
            WeaponPlatform.DesertEagle =>
                AnimatedLargeSidearmReloadArmPresentationScale,
            _ when sidearm => AnimatedSidearmReloadArmPresentationScale,
            _ => AuthoredArmPresentationScale
        };
        var presentationBasis = new Basis(Vector3.Up, Mathf.Pi);
        if (sidearm)
        {
            var pitch = largeSidearm
                ? AnimatedLargeSidearmReloadArmPitchRadians
                : AnimatedSidearmReloadArmPitchRadians;
            presentationBasis = new Basis(Vector3.Right, pitch) * presentationBasis;
        }

        // Keep the complete rig in the same scale and orientation as the
        // approved static first-person arms. The animated left chain is then
        // retargeted independently, so fitting a platform's support grip cannot
        // rotate or stretch the right arm and shoulder across the viewport.
        var grip = animatedReloadArms.RightGripTransformInRoot;
        var mountedBasis = (presentationBasis
            * grip.Basis.Orthonormalized().Inverse())
            .Scaled(Vector3.One * (presentationScale / inheritedScale));
        // The imported WeaponRoot already carries the DCC source-to-metre
        // conversion. Preserve its converted grip origin, but strip that
        // uniform scale from the orientation before mounting the scene. Both
        // multiplying the conversion again and inverting it would mis-size the
        // complete rig by orders of magnitude.
        animatedReloadArms.Root.Transform = new Transform3D(
            mountedBasis,
            pose.PrimaryGrip - mountedBasis * grip.Origin);
    }

    private void ResetAnimatedReloadArmsPresentation()
    {
        if (IsInstanceValid(_authoredAnimatedReloadArms?.Root))
        {
            _authoredAnimatedReloadArms.Root.Visible = false;
        }
        var staticArmsRestored = false;
        if (ActiveAuthoredArms() is { } staticArms
            && IsInstanceValid(staticArms.Root))
        {
            staticArms.Root.Visible = true;
            staticArms.RightArm.Visible = true;
            staticArms.LeftArm.Visible = true;
            AlignAuthoredArmsToWeapon();
            staticArmsRestored = true;
        }
        if (IsInstanceValid(_proceduralFirstPersonArms))
        {
            _proceduralFirstPersonArms.Visible = !staticArmsRestored
                && EquippedWeapon.Platform != WeaponPlatform.M3A1;
        }
    }
}

internal readonly record struct AnimatedReloadLeftArmPoseInspection(
    bool Available,
    Transform3D Shoulder,
    Transform3D Elbow,
    Transform3D Wrist,
    Transform3D Palm);

internal readonly record struct SidearmReloadEndpointPoseInspection(
    bool Available,
    Transform3D Wrist,
    Transform3D Palm);
