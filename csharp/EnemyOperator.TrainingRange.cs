using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    /// <summary>
    /// Convert the synchronous elimination callback into the range target's
    /// reusable downed state.  Must be called from the Eliminated signal handler
    /// before EnemyOperator.Die() resumes and creates its normal death tween.
    /// </summary>
    internal void SuppressDeathAnimationForTrainingRange()
    {
        _suppressDeathAnimationForTrainingRange = true;
        _deathTween?.Kill();
        _deathTween = null;
        if (IsInstanceValid(_bodyRoot))
        {
            _bodyRoot.Position = Vector3.Zero;
            _bodyRoot.Rotation = Vector3.Zero;
        }
    }

    /// <summary>Keep the authored range silhouette unarmed without altering its sockets.</summary>
    internal void PrepareTrainingRangeVisualForDiagnostics()
    {
        if (!UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }

        ApplyColdStartUnarmed();
        _authoredOperatorVisual.SetWeaponReadied(false);
        _authoredOperatorVisual.SetWeaponVisible(false);
        _authoredOperatorAnimator.Update(
            0.0f,
            0.0f,
            weaponReadied: false,
            prone: false,
            crouched: false,
            aiming: false,
            downed: false,
            reviving: false,
            dead: false);
    }

    /// <summary>
    /// Revive one dedicated-range target in place.  The same EnemyOperator node is kept
    /// throughout the cycle, so the player sees the authored death pose, then the target
    /// stands up at its lane instead of disappearing and popping in as a new corpse.
    /// </summary>
    public void ReviveForTrainingRange(Vector3 position)
    {
        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }

        // This reset clears corpse loot and tactical transients while restoring the
        // authored animator's standing state.  The range immediately freezes the AI
        // again below; patrol/reactive motion is driven by the world range controller.
        ResetTacticalStateForDiagnostics();
        GlobalPosition = position;
        Velocity = Vector3.Zero;
        Visible = true;
        CollisionLayer = 2;
        CollisionMask = 1 | BreakableGlassField.MovementCollisionLayer;
        SetAuthoredCombatPoseForDiagnostics();
        PrepareTrainingRangeVisualForDiagnostics();
        if (IsInstanceValid(_bodyRoot))
        {
            _bodyRoot.Position = Vector3.Zero;
            _bodyRoot.Rotation = Vector3.Zero;
        }
        // Pause only the AI tick.  Keeping the CharacterBody3D inherited avoids
        // briefly unregistering its hit collider before the range controller
        // resumes the target in the same frame.
        ProcessMode = ProcessModeEnum.Inherit;
        SetPhysicsProcess(false);
    }

    /// <summary>
    /// Keep a human target readable as a knockdown during the short reset window.
    /// The normal enemy death animation is intentionally replaced with the authored
    /// downed clip here; the target remains visible and non-collidable until the
    /// range controller calls <see cref="ReviveForTrainingRange"/>.
    /// </summary>
    public void SetTrainingRangeDownedPose(float delta = 1.0f / 60.0f)
    {
        if (!GodotObject.IsInstanceValid(this) || !UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }
        _deathTween?.Kill();
        _deathTween = null;
        // Select the authored downed pose without advancing it every frame.  The
        // previous implementation replayed the clip continuously while the target
        // was waiting to reset, accumulating root rotation and producing the tilted
        // / upside-down bodies visible in the range.
        _authoredOperatorAnimator.Update(
            0.0f,
            0.0f,
            weaponReadied: false,
            prone: false,
            crouched: false,
            aiming: false,
            downed: true,
            reviving: false,
            dead: false);

        // The authored downed clip already contains its ground contact.
        _authoredOperatorVisual.Root.Rotation = Vector3.Zero;
        if (IsInstanceValid(_bodyRoot))
        {
            _bodyRoot.Position = Vector3.Zero;
            _bodyRoot.Rotation = Vector3.Zero;
        }
    }

    /// <summary>Move a target while leaving its AI disabled and its hit collider active.</summary>
    public void SetTrainingRangeTargetPose(Vector3 position, float yawRadians)
    {
        if (IsDead || !GodotObject.IsInstanceValid(this))
        {
            return;
        }
        GlobalPosition = position;
        Rotation = new Vector3(0.0f, yawRadians, 0.0f);
        Velocity = Vector3.Zero;
    }
}
