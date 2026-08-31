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
            staticArms.Root.Visible = !active;
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

        // Empty sidearm reloads reuse the compact tactical hand choreography.
        // The runtime slide-release pulse below still cycles the real slide;
        // avoiding the authored empty clip prevents its old cross-gun rack
        // gesture from sweeping the cropped support forearm across the pistol.
        var authoredEmptyReload = _reloadStartedEmpty
            && !WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        var authoredArmProgress = DirectReloadArmProgress();
        animatedReloadArms!.SetPresentationPlatform(EquippedWeapon.Platform);
        animatedReloadArms.SetReloadProgress(
            EquippedWeapon.Platform,
            authoredEmptyReload,
            0.0f);
        AlignAnimatedReloadArmsToWeapon(animatedReloadArms);
        animatedReloadArms!.SetReloadProgress(
            EquippedWeapon.Platform,
            authoredEmptyReload,
            authoredArmProgress);
        var supportTarget = ReloadSupportTargetGlobal();
        // Bring the support wrist in from the lower-left of the magazine.  A
        // lower-right exit overlaps the firing hand and pushes the cuff below
        // frame, making a valid skinned glove appear missing.
        var reloadProfile = FirstPersonReloadProfileCatalog.For(
            EquippedWeapon.Platform);
        var wristDirectionInWeaponRoot = WeaponCatalog.IsSidearm(
                EquippedWeapon.Platform)
            ? new Vector3(0.32f, -0.50f, -0.80f)
            : new Vector3(0.30f, -0.67f, -0.68f);
        var safeWristDirection = reloadProfile.Mechanism
                == FirstPersonReloadMechanism.InternalMagazine
            ? Vector3.Zero
            : _weaponRoot.GlobalTransform.Basis.Orthonormalized()
                * wristDirectionInWeaponRoot.Normalized();
        animatedReloadArms.RetargetLeftPalm(
            EquippedWeapon.Platform,
            supportTarget,
            safeWristDirection,
            SidearmReloadMagazineAnchorBlend());
        return true;
    }

    private float DirectReloadArmProgress()
    {
        var profile = FirstPersonReloadProfileCatalog.For(
            EquippedWeapon.Platform);
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        if (profile.Mechanism == FirstPersonReloadMechanism.InternalMagazine)
        {
            return progress;
        }

        // Keep one compact, camera-safe grip for the direct exchange. The
        // target supplies all down-and-up motion; replaying the old pouch clip
        // turns the wrist behind the near plane and leaves a floating hand.
        return 0.0f;
    }

    private float SidearmReloadMagazineAnchorBlend()
    {
        if (!_isReloading
            || !WeaponCatalog.IsSidearm(EquippedWeapon.Platform))
        {
            return 0.0f;
        }

        var profile = FirstPersonReloadProfileCatalog.For(
            EquippedWeapon.Platform);
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        if (progress < profile.ReachEnd)
        {
            return SmoothSegment(progress, 0.0f, profile.ReachEnd);
        }
        if (progress < profile.SeatEnd)
        {
            return 1.0f;
        }
        if (progress < profile.ActionEnd)
        {
            return 1.0f - SmoothSegment(
                progress,
                profile.SeatEnd,
                profile.ActionEnd);
        }
        return 0.0f;
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
