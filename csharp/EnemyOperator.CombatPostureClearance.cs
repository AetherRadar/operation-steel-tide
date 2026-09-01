using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    private bool TrySetPronePosture(bool prone, float expansionHeight)
    {
        if (!prone
            && IsProne
            && !IsDead
            && !HasCombatPostureClearance(expansionHeight))
        {
            _proneTimer = Mathf.Max(_proneTimer, 0.18f);
            return false;
        }

        var changed = IsProne != prone;
        IsProne = prone;
        if (changed)
        {
            OnCombatProneStateChanged(prone);
        }
        if (prone)
        {
            var stopped = Velocity;
            stopped.X = 0.0f;
            stopped.Z = 0.0f;
            Velocity = stopped;
        }
        UpdateAuthoredStanceCollider();
        if (IsInstanceValid(_bodyRoot))
        {
            _bodyRoot.Position = Vector3.Zero;
            _bodyRoot.Rotation = UsesAuthoredOperatorForDiagnostics
                ? Vector3.Zero
                : new Vector3(prone ? Mathf.Pi * 0.48f : 0.0f, 0.0f, 0.0f);
        }
        return true;
    }

    private bool TryStandForCombatMovement(bool clearCoverState = true)
    {
        var wasLow = IsProne || IsCrouched;
        if (wasLow && !HasCombatPostureClearance(StandingColliderHeight))
        {
            if (!IsProne)
            {
                _combatCrouched = true;
            }
            if (clearCoverState)
            {
                _seekingCover = false;
                _inCover = false;
            }
            UpdateAuthoredStanceCollider();
            return false;
        }

        if (IsProne
            && !TrySetPronePosture(false, StandingColliderHeight))
        {
            return false;
        }
        var leftCover = clearCoverState && (_seekingCover || _inCover);
        if (clearCoverState)
        {
            _seekingCover = false;
            _inCover = false;
        }
        SetCombatCrouched(false);
        if (leftCover)
        {
            UpdateAuthoredStanceCollider();
        }
        return !IsProne && !_combatCrouched;
    }

    private bool HasCombatPostureClearance(float targetHeight)
    {
        var currentHeight = CombatColliderHeight;
        if (targetHeight <= currentHeight + 0.01f
            || !IsInsideTree()
            || !IsInstanceValid(_collider))
        {
            return true;
        }

        var expansion = targetHeight - currentHeight;
        using var clearanceShape = new BoxShape3D
        {
            Size = new Vector3(0.7f, Mathf.Max(0.04f, expansion - 0.03f), 0.7f)
        };
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = clearanceShape,
            Transform = new Transform3D(
                Basis.Identity,
                GlobalPosition + Vector3.Up * (currentHeight + expansion * 0.5f)),
            CollisionMask = CollisionMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.005f,
            Exclude = exclude
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 1);
        using var hitsBacking = hits.AsDisposable();
        return hits.Count == 0;
    }
}
