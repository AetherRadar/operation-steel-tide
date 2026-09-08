using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private AuthoredAnimatedReloadArmsVisual _authoredAnimatedReloadArms = null!;

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
