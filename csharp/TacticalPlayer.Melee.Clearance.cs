using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private MeleePose ApplyMeleeWallClearance(
        MeleePose rest,
        KnifeSkinDefinition definition,
        float delta,
        out float obstruction)
    {
        var rawObstruction = MeasureMeleeWallObstruction(definition);
        var response = rawObstruction > _meleeWallObstruction ? 24.0f : 8.0f;
        _meleeWallObstruction = Mathf.Lerp(
            _meleeWallObstruction,
            rawObstruction,
            SmoothFactor(response, delta));
        obstruction = _meleeWallObstruction;
        return ClearedMeleeRest(rest, obstruction, emergency: false);
    }

    private float MeasureMeleeWallObstruction(KnifeSkinDefinition definition)
    {
        var from = _camera.GlobalPosition;
        var probeDistance = definition.TwoHanded ? 1.7f : 0.95f;
        var obstruction = ProbeMeleeWall(
            from,
            from - _camera.GlobalBasis.Z * probeDistance);
        if (IsInstanceValid(_authoredMelee?.BladeBase)
            && IsInstanceValid(_authoredMelee?.BladeTip))
        {
            var bladeBase = _authoredMelee.BladeBase.GlobalPosition;
            var bladeTip = _authoredMelee.BladeTip.GlobalPosition;
            obstruction = Mathf.Max(obstruction, ProbeMeleeWall(from, bladeBase));
            obstruction = Mathf.Max(
                obstruction,
                ProbeMeleeWall(from, bladeBase.Lerp(bladeTip, 0.5f)));
            obstruction = Mathf.Max(obstruction, ProbeMeleeWall(from, bladeTip));
        }
        return obstruction;
    }

    private float ProbeMeleeWall(Vector3 from, Vector3 to)
    {
        var distance = from.DistanceTo(to);
        if (distance <= 0.08f)
        {
            return 0.0f;
        }

        if (!TryFindMeleeWorldBlocker(from, to, out var hit))
        {
            return 0.0f;
        }
        if (hit.Collider is null)
        {
            return 1.0f;
        }
        return 1.0f - Mathf.Clamp(
            (from.DistanceTo(hit.Position) - 0.08f) / distance,
            0.0f,
            1.0f);
    }

    private bool FinalizeMeleePoseClearance(
        MeleePose target,
        MeleePose rest,
        KnifeSkinDefinition definition)
    {
        var hardTuck = ClearedMeleeRest(rest, 1.0f, emergency: true);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var obstruction = MeasureMeleeMarkerObstruction();
            if (obstruction <= 0.015f)
            {
                return UpdateMeleeClearanceVisibility(obstruction);
            }
            target = LerpPose(
                target,
                hardTuck,
                attempt == 2
                    ? 1.0f
                    : Mathf.Clamp(0.3f + obstruction * 1.5f, 0.3f, 0.85f));
            SetMeleePose(target);
        }
        var residual = MeasureMeleeMarkerObstruction();
        if (residual > 0.02f)
        {
            _meleeClearanceSuppressed = true;
            _meleeClearanceClearFrames = 0;
            _knifeRoot.Visible = false;
            ClearMeleeTrail();
            _meleeSweepPrimed = false;
            return false;
        }
        return UpdateMeleeClearanceVisibility(residual);
    }

    private bool UpdateMeleeClearanceVisibility(float residual)
    {
        if (!_meleeClearanceSuppressed)
        {
            _knifeRoot.Visible = true;
            return true;
        }
        _meleeClearanceClearFrames = residual <= 0.008f
            ? _meleeClearanceClearFrames + 1
            : 0;
        if (_meleeClearanceClearFrames >= 2)
        {
            _meleeClearanceSuppressed = false;
            _meleeClearanceClearFrames = 0;
            _knifeRoot.Visible = true;
            return true;
        }
        _knifeRoot.Visible = false;
        ClearMeleeTrail();
        _meleeSweepPrimed = false;
        return false;
    }

    private float MeasureMeleeMarkerObstruction()
    {
        if (!IsInstanceValid(_authoredMelee?.BladeBase)
            || !IsInstanceValid(_authoredMelee?.BladeTip))
        {
            return 1.0f;
        }
        var from = _camera.GlobalPosition;
        var bladeBase = _authoredMelee.BladeBase.GlobalPosition;
        var bladeTip = _authoredMelee.BladeTip.GlobalPosition;
        var obstruction = 0.0f;
        for (var sample = 0; sample <= 4; sample++)
        {
            obstruction = Mathf.Max(
                obstruction,
                ProbeMeleeWall(from, bladeBase.Lerp(bladeTip, sample / 4.0f)));
        }
        return obstruction;
    }

    private bool TryFindMeleeWorldBlocker(
        Vector3 from,
        Vector3 to,
        out PhysicsRaycastHit hit)
    {
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    from,
                    to,
                    exclude,
                    BreakableGlassField.SightCollisionMask,
                    out hit))
            {
                return false;
            }
            if (IsMeleeDamageTarget(hit.Collider)
                && hit.Collider is CollisionObject3D damageTarget)
            {
                exclude.Add(damageTarget.GetRid());
                continue;
            }
            return true;
        }
        hit = default;
        return true;
    }

    private static MeleePose ClearedMeleeRest(
        MeleePose rest,
        float obstruction,
        bool emergency)
    {
        var positionOffset = emergency
            ? new Vector3(0.0f, -0.4f, 0.62f)
            : new Vector3(0.0f, -0.22f, 0.3f);
        var rotationOffset = emergency
            ? new Vector3(1.05f, 0.0f, 0.0f)
            : new Vector3(0.72f, 0.0f, 0.0f);
        return new MeleePose(
            rest.Position + positionOffset * obstruction,
            rest.Rotation + rotationOffset * obstruction);
    }

    private static MeleePose ConstrainMeleePose(
        MeleePose pose,
        MeleePose baseRest,
        MeleePose clearedRest,
        float obstruction,
        bool longBlade)
    {
        var minimumAmplitude = longBlade ? 0.18f : 0.38f;
        var amplitude = Mathf.Lerp(1.0f, minimumAmplitude, obstruction);
        return new MeleePose(
            clearedRest.Position + (pose.Position - baseRest.Position) * amplitude,
            clearedRest.Rotation + (pose.Rotation - baseRest.Rotation) * amplitude);
    }

    private void SetMeleePose(MeleePose pose)
    {
        _knifeRoot.Position = pose.Position;
        _knifeRoot.Rotation = pose.Rotation;
    }
}
