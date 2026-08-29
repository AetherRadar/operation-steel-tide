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
        var active = _isReloading
            && EquippedWeapon.Platform != WeaponPlatform.M3A1
            && IsInstanceValid(animatedReloadArms?.Root);
        if (IsInstanceValid(animatedReloadArms?.Root))
        {
            animatedReloadArms.Root.Visible = active;
        }

        if (ActiveAuthoredArms() is { } staticArms
            && IsInstanceValid(staticArms.Root))
        {
            staticArms.Root.Visible = !active;
        }

        if (!active)
        {
            return false;
        }

        animatedReloadArms!.SetReloadProgress(
            EquippedWeapon.Platform,
            _reloadStartedEmpty,
            0.0f);
        AlignAnimatedReloadArmsToWeapon(animatedReloadArms);
        animatedReloadArms!.SetReloadProgress(
            EquippedWeapon.Platform,
            _reloadStartedEmpty,
            ReloadProgress);
        animatedReloadArms.RetargetLeftPalm(ReloadSupportTargetGlobal());
        return true;
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
                ? AuthoredLargeSidearmArmPitchRadians
                : AuthoredSidearmArmPitchRadians;
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
        if (ActiveAuthoredArms() is { } staticArms && IsInstanceValid(staticArms.Root))
        {
            staticArms.Root.Visible = true;
            AlignAuthoredArmsToWeapon();
        }
    }
}
