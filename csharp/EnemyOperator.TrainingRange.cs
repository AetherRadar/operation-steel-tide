using System;
using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    /// <summary>
    /// Prepare a range target's presentation after the actor has been built.
    ///
    /// The live-fire venue intentionally keeps targets visually simple: the base
    /// authored operator already contains the helmet/vest silhouette, while the
    /// optional paper-doll overlays are inventory previews with source-space
    /// offsets.  Attaching those overlays to an animated bone socket makes them
    /// read as floating crates in-world, so hide them for targets and normalize
    /// any imported 100x socket scale before solving the rifle.
    /// </summary>
    internal void PrepareTrainingRangeVisualForDiagnostics()
    {
        if (!UsesAuthoredOperatorForDiagnostics)
        {
            return;
        }

        HideTrainingRangeEquipmentOverlays();
        if (CombatModelLibrary.UsesHy3dOperator(OperatorVisual))
        {
            NormalizeTrainingRangeAttachmentSockets();
        }

        // Re-apply the weapon socket after the imported rig scale has been fixed.
        // Toggling through the back socket forces AuthoredOperatorVisual to recompute
        // its scale from the now-normalized parent instead of retaining a giant
        // first-frame value.
        _authoredOperatorVisual.SetWeaponReadied(false);
        _authoredOperatorVisual.SetWeaponReadied(true);
        SetAuthoredCombatPoseForDiagnostics();
    }

    private void HideTrainingRangeEquipmentOverlays()
    {
        var root = _authoredOperatorVisual.Root;
        foreach (var child in root.GetChildren())
        {
            HideTrainingRangeEquipmentNode(child);
        }
    }

    private void HideTrainingRangeEquipmentNode(Node node)
    {
        if (node is Node3D node3d
            && (node.Name.ToString().StartsWith("Equipped", StringComparison.Ordinal)
                || node.Name.ToString().EndsWith("EquipmentVisual", StringComparison.Ordinal)))
        {
            // Range targets are silhouettes, not inventory mannequins.  The
            // authored base operators already include their clothing/armor, while
            // detachable paper-doll equipment varies in source bounds and can read
            // as a large floating crate from the firing line.  Hide every optional
            // overlay in this mode so only the intended operator and rifle remain.
            node3d.Visible = false;
            return;
        }
        foreach (var child in node.GetChildren())
        {
            HideTrainingRangeEquipmentNode(child);
        }
    }

    private void NormalizeTrainingRangeAttachmentSockets()
    {
        var root = _authoredOperatorVisual.Root;
        var socketNames = new[]
        {
            "WeaponSocket", "BackWeaponSocket", "HeadSocket", "VestSocket",
            "BackpackSocket", "TeamPatchSocket"
        };
        foreach (var socketName in socketNames)
        {
            var socket = root.FindChild(socketName, recursive: true, owned: false) as Node3D;
            if (socket is null)
            {
                continue;
            }

            var scale = socket.Scale;
            // Authored bamen/HY-3D exports express socket transforms in centimetres
            // while their rig is scaled by 0.01.  A normal socket is approximately
            // unit scale; only repair the unmistakable import error.
            if (scale.X > 4.0f || scale.X < 0.25f
                || scale.Y > 4.0f || scale.Y < 0.25f
                || scale.Z > 4.0f || scale.Z < 0.25f)
            {
                socket.Scale = Vector3.One;
            }
        }
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

        // Keep the target visibly down on the lane while the looping clip advances.
        // The animator applies the matching root-height correction; this explicit
        // call is intentionally idempotent because the range controller invokes it
        // every frame during the short reset window.
        _authoredOperatorVisual.Root.Position = Vector3.Down * 0.46f;
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
