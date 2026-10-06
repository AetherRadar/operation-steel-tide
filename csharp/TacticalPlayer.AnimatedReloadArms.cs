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
            && UsesRifleAnimatedReloadArms(EquippedWeapon.Platform)
            && IsInstanceValid(_authoredAnimatedReloadArms?.Root)
            && _authoredAnimatedReloadArms.Root.IsVisibleInTree();

    internal bool UsesAnimatedSidearmForearmsForDiagnostics
        => UsesAnimatedReloadArmsForDiagnostics
            && _authoredAnimatedReloadArms.UsesSidearmForearms;

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
        if (!UsesRifleAnimatedReloadArms(EquippedWeapon.Platform))
        {
            return;
        }
        if (_animatedReloadArmsLoadAttempted
            || IsInstanceValid(_authoredAnimatedReloadArms?.Root))
        {
            return;
        }

        _animatedReloadArmsLoadAttempted = true;
        var arms = CombatModelLibrary.InstantiateAnimatedReloadArms(Role);
        arms.Root.Visible = false;
        _weaponRoot.AddChild(arms.Root);
        _authoredAnimatedReloadArms = arms;
    }

    private static bool UsesRifleAnimatedReloadArms(WeaponPlatform platform)
        => platform != WeaponPlatform.M3A1
            && !WeaponCatalog.IsSidearm(platform);

    private bool UpdateAnimatedReloadArmsPresentation()
    {
        var active = _isReloading
            && UsesRifleAnimatedReloadArms(EquippedWeapon.Platform)
            && IsInstanceValid(_authoredAnimatedReloadArms?.Root);
        if (IsInstanceValid(_authoredAnimatedReloadArms?.Root))
        {
            _authoredAnimatedReloadArms.Root.Visible = active;
        }

        var staticArms = ActiveAuthoredArms();
        if (staticArms is not null && IsInstanceValid(staticArms.Root))
        {
            // The stable firing hand remains on the weapon. The role-specific
            // animated crop owns only the support hand while reloading.
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
            if (IsInstanceValid(_proceduralFirstPersonArms))
            {
                _proceduralFirstPersonArms.Visible = staticArms is null
                    && EquippedWeapon.Platform != WeaponPlatform.M3A1;
            }
            return false;
        }

        var animatedArms = _authoredAnimatedReloadArms;
        if (animatedArms is null
            || !GodotObject.IsInstanceValid(animatedArms.Root))
        {
            return false;
        }
        var progress = Mathf.Clamp(PresentationReloadProgress, 0.0f, 1.0f);
        animatedArms.SetPresentationPlatform(EquippedWeapon.Platform);
        AlignAnimatedReloadArmsToWeapon(animatedArms);
        animatedArms.SetReloadProgress(
            EquippedWeapon.Platform,
            _reloadStartedEmpty,
            progress);
        animatedArms.AcceptAuthoredPose();
        AlignReloadMagazineToAuthoredHand(animatedArms, progress);
        return true;
    }

    private void AlignReloadMagazineToAuthoredHand(
        AuthoredAnimatedReloadArmsVisual animatedArms,
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

        var handContact = WeaponCatalog.IsSidearm(EquippedWeapon.Platform)
            ? animatedArms.LeftPalmCenterGlobalPosition
            : animatedArms.LeftGripAnchorGlobalPosition;
        weapon.AlignMagazineGripToGlobalPosition(
            spare: carryingReplacement,
            handContact);
    }

    private void AlignAnimatedReloadArmsToWeapon(
        AuthoredAnimatedReloadArmsVisual animatedArms)
    {
        if (!IsInstanceValid(animatedArms.Root))
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
            presentationBasis = new Basis(Vector3.Right, pitch)
                * presentationBasis;
        }

        var grip = animatedArms.RightGripTransformInRoot;
        var mountedBasis = (presentationBasis
            * grip.Basis.Orthonormalized().Inverse())
            .Scaled(Vector3.One * (presentationScale / inheritedScale));
        animatedArms.Root.Transform = new Transform3D(
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
