using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private AuthoredOperatorVisual _authoredOperatorVisual = null!;
    private AuthoredOperatorAnimator _authoredOperatorAnimator = null!;
    private float _authoredAimHoldRemaining;
    private Vector3 _authoredPreviousPosition;
    private bool _authoredPositionSampled;
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
            || !IsDowned && AuthoredAnimationForDiagnostics is "idle" or "ready_idle" or "pistol_ready_idle");

    internal bool IsDemolitionEliminatedPoseForDiagnostics
        => AuthoredAnimationForDiagnostics is "death" or "downed";

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
        var weaponReadied = HasFireablePrimary;
        _authoredOperatorVisual.SetWeaponReadied(weaponReadied);
        _authoredOperatorAnimator.SetRestingPose(weaponReadied);
    }

    private void AnimateAuthoredOperator(float delta)
    {
        // Sample net travel after collision and navigation movement. Commanded
        // velocity can remain nonzero while an operator is blocked by a wall.
        var displacement = GlobalPosition - _authoredPreviousPosition;
        _authoredPreviousPosition = GlobalPosition;
        var horizontalDistance = new Vector2(displacement.X, displacement.Z).Length();
        var speed = _authoredPositionSampled && delta > 0.0f && horizontalDistance < 1.0f
            ? horizontalDistance / delta
            : 0.0f;
        _authoredPositionSampled = true;
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
        var authoredOperator = CombatModelLibrary.InstantiateOperator(
            OperatorRoles.Spec(Role).VisualId,
            weaponBuild: HasFireablePrimary ? CarriedWeapon : null,
            attachDefaultWeapon: false);
        _rig.AddChild(authoredOperator.Root);
        var authoredAnimator = new AuthoredOperatorAnimator(authoredOperator);
        _authoredOperatorVisual = authoredOperator;
        _authoredOperatorAnimator = authoredAnimator;
        _authoredCarriedWeaponPlatform = CarriedWeapon.Platform;
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
                attachDefaultWeapon: false);
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
        catch
        {
            replacement?.Root.Free();
            throw;
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
