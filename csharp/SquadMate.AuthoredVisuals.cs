using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;
    private AuthoredOperatorAnimator _authoredOperatorAnimator = null!;
    private float _authoredAimHoldRemaining;
    private WeaponPlatform? _authoredCarriedWeaponPlatform;

    internal bool UsesAuthoredOperatorForDiagnostics
        => IsInstanceValid(_authoredOperatorVisual?.Root);
    internal string AuthoredAnimationForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorAnimator.CurrentAnimation
            : string.Empty;
    internal int AuthoredAnimationCountForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? _authoredOperatorAnimator.AnimationCount
            : 0;
    internal bool AuthoredCarriedWeaponMatchesForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            && _authoredCarriedWeaponPlatform == CarriedWeapon.Platform;

    internal bool IsDemolitionRoundFrozenPoseForDiagnostics
        => new Vector2(Velocity.X, Velocity.Z).LengthSquared() <= 0.0001f
        && (IsBodyBag
            || IsDowned && IsDemolitionEliminatedPoseForDiagnostics
            || !IsDowned && (!UsesAuthoredOperatorForDiagnostics
                || AuthoredAnimationForDiagnostics is "idle" or "ready_idle"));

    internal bool IsDemolitionEliminatedPoseForDiagnostics
        => UsesAuthoredOperatorForDiagnostics
            ? AuthoredAnimationForDiagnostics is "death" or "downed"
            : IsInstanceValid(_rig)
                && Mathf.Abs(Mathf.Abs(_rig.Rotation.X) - Mathf.Pi * 0.5f) <= 0.02f;

    internal void SetAuthoredMovementPoseForDiagnostics(float speed, bool aiming = false)
    {
        if (!UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }
        _authoredOperatorVisual.SetWeaponVisible(true);
        _authoredOperatorVisual.SetWeaponReadied(true);
        _authoredOperatorAnimator.Update(
            1.0f,
            speed,
            weaponReadied: true,
            prone: false,
            crouched: false,
            aiming,
            downed: false,
            reviving: false,
            dead: false);
    }

    private void HoldAuthoredAimAfterShot()
        => _authoredAimHoldRemaining = Mathf.Max(_authoredAimHoldRemaining, 0.36f);

    private void SetDemolitionEliminatedPose()
    {
        Velocity = Vector3.Zero;
        _authoredAimHoldRemaining = 0.0f;
        _revivePoseBlend = 0.0f;
        if (UsesAuthoredOperatorForDiagnostics)
        {
            _authoredOperatorVisual.SetWeaponReadied(false);
            _authoredOperatorAnimator.Update(
                0.0f,
                0.0f,
                weaponReadied: false,
                prone: false,
                crouched: false,
                aiming: false,
                downed: !ReviveUsed,
                reviving: false,
                dead: ReviveUsed);
            return;
        }

        _rig.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f);
        var rigPosition = _rig.Position;
        rigPosition.Y = 0.0f;
        _rig.Position = rigPosition;
    }

    internal void SetDemolitionRoundFrozenPose()
    {
        Velocity = Vector3.Zero;
        if (IsBodyBag)
        {
            return;
        }
        if (IsDowned)
        {
            SetDemolitionEliminatedPose();
            return;
        }

        _authoredAimHoldRemaining = 0.0f;
        if (UsesAuthoredOperatorForDiagnostics)
        {
            var weaponReadied = HasFireablePrimary;
            _authoredOperatorVisual.SetWeaponReadied(weaponReadied);
            _authoredOperatorAnimator.SetRestingPose(weaponReadied);
            return;
        }

        _rig.Rotation = Vector3.Zero;
        var rigPosition = _rig.Position;
        rigPosition.Y = 0.0f;
        _rig.Position = rigPosition;
    }

    private void AnimateAuthoredOperator(float delta, float speed)
    {
        _authoredAimHoldRemaining = Mathf.Max(0.0f, _authoredAimHoldRemaining - delta);
        var weaponReadied = HasFireablePrimary && !IsDowned && _revivePoseBlend <= 0.5f;
        var visibleTargetInRange = _combatTarget is not null
            && IsInstanceValid(_combatTarget)
            && !_combatTarget.IsDead
            && _combatHasSight
            && GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) <= 55.0f;
        _authoredOperatorVisual.SetWeaponReadied(weaponReadied);
        _authoredOperatorAnimator.Update(
            delta,
            speed,
            weaponReadied,
            prone: false,
            crouched: false,
            aiming: weaponReadied && (visibleTargetInRange || _authoredAimHoldRemaining > 0.0f),
            downed: IsDowned,
            reviving: _revivePoseBlend > 0.5f,
            dead: false);
    }

    private void AttachAuthoredOperatorVisual()
    {
        AuthoredOperatorVisual? authoredOperator = null;
        try
        {
            authoredOperator = CombatModelLibrary.InstantiateOperator(
                OperatorRoles.Spec(Role).VisualId,
                weaponBuild: HasFireablePrimary ? CarriedWeapon : null,
                attachDefaultWeapon: false,
                helmet: EquippedHelmet,
                bodyArmor: EquippedBodyArmor,
                backpack: EquippedBackpack);
            _rig.AddChild(authoredOperator.Root);
            var authoredAnimator = new AuthoredOperatorAnimator(authoredOperator);
            _authoredOperatorVisual = authoredOperator;
            _authoredOperatorAnimator = authoredAnimator;
            _authoredCarriedWeaponPlatform = CarriedWeapon.Platform;
        }
        catch (Exception exception)
        {
            authoredOperator?.Root.QueueFree();
            GD.PushWarning($"Authored squad operator unavailable; retaining procedural visual: {exception.Message}");
            return;
        }
        var children = _rig.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is MeshInstance3D mesh && mesh != _authoredOperatorVisual.Root)
            {
                mesh.QueueFree();
            }
        }
    }

    private void RefreshAuthoredCarriedWeaponVisual()
    {
        if (!IsInsideTree() || !IsInstanceValid(_rig))
        {
            return;
        }

        AuthoredOperatorVisual? replacement = null;
        try
        {
            replacement = CombatModelLibrary.InstantiateOperator(
                OperatorRoles.Spec(Role).VisualId,
                weaponBuild: HasFireablePrimary ? CarriedWeapon : null,
                attachDefaultWeapon: false,
                helmet: EquippedHelmet,
                bodyArmor: EquippedBodyArmor,
                backpack: EquippedBackpack);
            _rig.AddChild(replacement.Root);
            replacement.SetTeamColor(OperatorRoles.Spec(Role).Accent);
            replacement.SetWeaponVisible(HasFireablePrimary);
            replacement.SetWeaponReadied(HasFireablePrimary && !IsDowned);
            var replacementAnimator = new AuthoredOperatorAnimator(replacement);
            replacementAnimator.SetRestingPose(HasFireablePrimary && !IsDowned);

            var previousRoot = UsesAuthoredOperatorForDiagnostics
                ? _authoredOperatorVisual.Root
                : null;
            _authoredOperatorVisual = replacement;
            _authoredOperatorAnimator = replacementAnimator;
            _authoredCarriedWeaponPlatform = CarriedWeapon.Platform;
            if (IsInstanceValid(previousRoot))
            {
                previousRoot!.Free();
            }
        }
        catch (Exception exception)
        {
            replacement?.Root.Free();
            GD.PushWarning(
                $"Authored {CarriedWeapon.Platform} squad weapon unavailable; retaining prior visual: "
                + exception.Message);
        }
    }

    private void SetAuthoredRoleColor(Color color)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetTeamColor(color);
        }
    }

    private void SetAuthoredWeaponVisible(bool visible)
    {
        if (IsInstanceValid(_authoredOperatorVisual?.Root))
        {
            _authoredOperatorVisual.SetWeaponVisible(visible);
        }
    }

    private void UpdateAuthoredStanceCollider()
    {
        if (!IsInstanceValid(_collider) || _collider.Shape is not CapsuleShape3D capsule)
        {
            return;
        }
        var kneeling = _revivePoseBlend > 0.5f && !IsDowned;
        var height = IsDowned ? 0.72f : kneeling ? 1.18f : 1.76f;
        capsule.Height = height;
        _collider.Position = new Vector3(0.0f, height * 0.5f, 0.0f);
    }
}
