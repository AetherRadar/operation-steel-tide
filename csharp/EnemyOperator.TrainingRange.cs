using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
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
        ProcessMode = ProcessModeEnum.Disabled;
        SetPhysicsProcess(false);
    }

    /// <summary>
    /// Keep a human target readable as a knockdown during the short reset window.
    /// The normal enemy death animation is intentionally replaced with the authored
    /// downed clip here; the target remains visible and non-collidable until the
    /// range controller calls <see cref="ReviveForTrainingRange"/>.
    /// </summary>
    public void SetTrainingRangeDownedPose()
    {
        if (!GodotObject.IsInstanceValid(this) || !UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }
        _deathTween?.Kill();
        _deathTween = null;
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
