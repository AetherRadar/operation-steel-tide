using Godot;

namespace OperationSteelTide;

public partial class TacticalPlayer
{
    private void UpdateMeleeWallContactFeedback(
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack,
        float progress,
        Vector3 rawBladeBase,
        Vector3 rawBladeTip,
        bool clearanceSafe)
    {
        if (!IsInstanceValid(_authoredMelee?.BladeBase)
            || !IsInstanceValid(_authoredMelee?.BladeTip))
        {
            _rawMeleeContactPrimed = false;
            return;
        }
        if (!_rawMeleeContactPrimed)
        {
            _previousRawMeleeBladeBase = rawBladeBase;
            _previousRawMeleeBladeTip = rawBladeTip;
            _previousRawMeleeAttackProgress = progress;
            _rawMeleeContactPrimed = true;
            return;
        }

        var damageWindowStart = Mathf.Max(0.18f, attack.HitProgress - 0.12f);
        var damageWindowEnd = Mathf.Min(0.78f, attack.HitProgress + 0.16f);
        var crossesDamageWindow = progress >= damageWindowStart
            && _previousRawMeleeAttackProgress <= damageWindowEnd;
        if ((!clearanceSafe || _meleeWallObstruction > 0.015f)
            && !_meleeWorldImpactSpawned
            && crossesDamageWindow
            && TryFindMeleeWallContact(
                rawBladeBase,
                rawBladeTip,
                out var hit,
                out var bladeTravel))
        {
            SpawnMeleeWorldImpact(hit, bladeTravel, definition);
            _meleeWorldImpactSpawned = true;
        }

        _previousRawMeleeBladeBase = rawBladeBase;
        _previousRawMeleeBladeTip = rawBladeTip;
        _previousRawMeleeAttackProgress = progress;
    }

    private bool TryFindMeleeWallContact(
        Vector3 rawBladeBase,
        Vector3 rawBladeTip,
        out PhysicsRaycastHit closestHit,
        out Vector3 bladeTravel)
    {
        closestHit = default;
        bladeTravel = Vector3.Zero;
        var closestDistanceSquared = float.MaxValue;
        var found = false;
        for (var sample = 0; sample <= 4; sample++)
        {
            var weight = sample / 4.0f;
            var rawPoint = rawBladeBase.Lerp(rawBladeTip, weight);
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    _camera.GlobalPosition,
                    rawPoint,
                    GetRid(),
                    uint.MaxValue,
                    out var hit)
                || hit.Collider is null)
            {
                continue;
            }

            var distanceSquared = _camera.GlobalPosition.DistanceSquaredTo(hit.Position);
            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }
            closestDistanceSquared = distanceSquared;
            closestHit = hit;
            var previousRawPoint = _previousRawMeleeBladeBase.Lerp(
                _previousRawMeleeBladeTip,
                weight);
            bladeTravel = rawPoint - previousRawPoint;
            found = true;
        }
        if (found && IsLivingMeleeContact(closestHit.Collider))
        {
            return false;
        }
        if (found && bladeTravel.LengthSquared() <= 0.0001f)
        {
            bladeTravel = rawBladeTip - rawBladeBase;
        }
        return found;
    }

    private static bool IsLivingMeleeContact(GodotObject? target)
        => target is EnemyOperator
            or CivilianNpc
            or SquadMate
            or TacticalPlayer;

    private void UpdateMeleeBladeSweep(
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack,
        float progress,
        bool damageEnabled)
    {
        if (!IsInstanceValid(_authoredMelee?.BladeBase)
            || !IsInstanceValid(_authoredMelee?.BladeTip))
        {
            _meleeSweepPrimed = false;
            return;
        }
        if (!damageEnabled)
        {
            _meleeSweepPrimed = false;
            return;
        }

        var currentBase = _authoredMelee.BladeBase.GlobalPosition;
        var currentTip = _authoredMelee.BladeTip.GlobalPosition;
        if (!_meleeSweepPrimed)
        {
            _previousMeleeBladeBase = currentBase;
            _previousMeleeBladeTip = currentTip;
            _previousMeleeAttackProgress = progress;
            _meleeSweepPrimed = true;
            return;
        }

        var intervalLength = progress - _previousMeleeAttackProgress;
        var damageWindowStart = Mathf.Max(0.18f, attack.HitProgress - 0.12f);
        var damageWindowEnd = Mathf.Min(0.78f, attack.HitProgress + 0.16f);
        var overlapStart = Mathf.Max(_previousMeleeAttackProgress, damageWindowStart);
        var overlapEnd = Mathf.Min(progress, damageWindowEnd);
        if (damageEnabled && intervalLength > 0.0001f && overlapEnd >= overlapStart)
        {
            _meleeSweepSampleAtMsec = _meleeSwingStartedAtMsec
                + (long)(overlapEnd * attack.Duration * 1000.0f);
            var startWeight = Mathf.Clamp(
                (overlapStart - _previousMeleeAttackProgress) / intervalLength,
                0.0f,
                1.0f);
            var endWeight = Mathf.Clamp(
                (overlapEnd - _previousMeleeAttackProgress) / intervalLength,
                0.0f,
                1.0f);
            ResolveMeleeSweep(
                definition,
                attack,
                _previousMeleeBladeBase.Lerp(currentBase, startWeight),
                _previousMeleeBladeTip.Lerp(currentTip, startWeight),
                _previousMeleeBladeBase.Lerp(currentBase, endWeight),
                _previousMeleeBladeTip.Lerp(currentTip, endWeight));
            _meleeBladeSweepResolved = true;
        }

        _previousMeleeBladeBase = currentBase;
        _previousMeleeBladeTip = currentTip;
        _previousMeleeAttackProgress = progress;
    }

    private void ResolveMeleeSweep(
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack,
        Vector3 previousBase,
        Vector3 previousTip,
        Vector3 currentBase,
        Vector3 currentTip)
    {
        if (_meleeHitTargets.Count >= attack.MaxTargets)
        {
            return;
        }

        var previousDirection = previousTip - previousBase;
        var currentDirection = currentTip - currentBase;
        var endpointTravel = Mathf.Max(
            previousBase.DistanceTo(currentBase),
            previousTip.DistanceTo(currentTip));
        var angleTravel = previousDirection.LengthSquared() > 0.0001f
            && currentDirection.LengthSquared() > 0.0001f
                ? previousDirection.Normalized().AngleTo(currentDirection.Normalized())
                : 0.0f;
        var temporalSteps = Mathf.Clamp(
            Mathf.Max(
                Mathf.CeilToInt(endpointTravel / 0.08f),
                Mathf.CeilToInt(angleTravel / Mathf.DegToRad(4.0f))),
            1,
            24);
        var bladeSamples = Mathf.Clamp(attack.SweepSamples, 3, 12);
        var glassBroken = false;
        var impactSpawned = _meleeWorldImpactSpawned;
        var stop = false;
        var exclude = new Godot.Collections.Array<Rid> { GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        foreach (var targetRid in _meleeHitTargetRids)
        {
            exclude.Add(targetRid);
        }

        for (var step = 1; step <= temporalSteps && !stop; step++)
        {
            var previousWeight = (step - 1.0f) / temporalSteps;
            var currentWeight = step / (float)temporalSteps;
            var frameBaseBefore = previousBase.Lerp(currentBase, previousWeight);
            var frameTipBefore = previousTip.Lerp(currentTip, previousWeight);
            var frameBaseNow = previousBase.Lerp(currentBase, currentWeight);
            var frameTipNow = previousTip.Lerp(currentTip, currentWeight);
            for (var sample = 0; sample < bladeSamples && !stop; sample++)
            {
                var bladeWeight = bladeSamples <= 1
                    ? 0.5f
                    : sample / (bladeSamples - 1.0f);
                stop = TraceMeleeBladeSegment(
                    frameBaseBefore.Lerp(frameTipBefore, bladeWeight),
                    frameBaseNow.Lerp(frameTipNow, bladeWeight),
                    definition,
                    attack,
                    exclude,
                    ref glassBroken,
                    ref impactSpawned);
            }
            if (!stop)
            {
                stop = TraceMeleeBladeSegment(
                    frameBaseNow,
                    frameTipNow,
                    definition,
                    attack,
                    exclude,
                    ref glassBroken,
                    ref impactSpawned);
            }
        }
        if (glassBroken)
        {
            PlayLocalGlassBreak();
        }
        _meleeWorldImpactSpawned = impactSpawned;
    }

    private bool TraceMeleeBladeSegment(
        Vector3 from,
        Vector3 to,
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack,
        Godot.Collections.Array<Rid> exclude,
        ref bool glassBroken,
        ref bool impactSpawned)
    {
        if (_meleeHitTargets.Count >= attack.MaxTargets)
        {
            return true;
        }

        if (from.DistanceSquaredTo(to) <= 0.000016f)
        {
            return false;
        }

        if (!MeleeSweepAnchorVisible(from, exclude, out var anchorBlocker))
        {
            if (!impactSpawned && anchorBlocker.Collider is not null)
            {
                impactSpawned = true;
                SpawnMeleeWorldImpact(anchorBlocker, to - from, definition);
            }
            return true;
        }

        _meleeSweepRayCount++;
        var hasHit = PhysicsRaycast.TryHit(
            GetWorld3D(),
            from,
            to,
            exclude,
            uint.MaxValue,
            out var hit);
        var glassTraceEnd = hasHit ? hit.Position : to;
        if (!glassBroken
            && TryShatterVisibleMeleeGlass(
                from,
                glassTraceEnd,
                definition.BaseDamage * 0.4f))
        {
            glassBroken = true;
        }
        if (!hasHit || hit.Collider is not { } target)
        {
            return false;
        }

        var targetId = target.GetInstanceId();
        if (_meleeHitTargets.Contains(targetId))
        {
            if (target is CollisionObject3D previousTarget
                && !_meleeHitTargetRids.Contains(previousTarget.GetRid()))
            {
                _meleeHitTargetRids.Add(previousTarget.GetRid());
                exclude.Add(previousTarget.GetRid());
            }
            return false;
        }
        if (IsMeleeDamageTarget(target)
            && !MeleeDamageLineClear(target, hit.Position, exclude, out var blocker))
        {
            if (!impactSpawned && blocker.Collider is not null)
            {
                impactSpawned = true;
                SpawnMeleeWorldImpact(blocker, to - from, definition);
            }
            return false;
        }
        if (_meleeHitTargets.Count >= attack.MaxTargets)
        {
            return true;
        }
        if (ApplyMeleeDamage(target, hit.Position, definition, attack))
        {
            _meleeHitTargets.Add(targetId);
            if (target is CollisionObject3D collisionTarget)
            {
                var targetRid = collisionTarget.GetRid();
                _meleeHitTargetRids.Add(targetRid);
                exclude.Add(targetRid);
            }
            if (target is EnemyOperator or CivilianNpc)
            {
                Main?.SpawnImpact(hit.Position, hit.Normal);
            }
            else if (!impactSpawned)
            {
                impactSpawned = true;
                SpawnMeleeWorldImpact(hit, to - from, definition);
            }
            return _meleeHitTargets.Count >= attack.MaxTargets;
        }
        if (!impactSpawned)
        {
            impactSpawned = true;
            SpawnMeleeWorldImpact(hit, to - from, definition);
        }
        return false;
    }

    private void SpawnMeleeWorldImpact(
        PhysicsRaycastHit hit,
        Vector3 bladeTravel,
        KnifeSkinDefinition definition)
    {
        if (hit.Collider is null)
        {
            return;
        }
        Main?.SpawnMeleeSurfaceImpact(
            hit.Position,
            hit.Normal,
            bladeTravel,
            hit.Collider,
            hit.Shape,
            definition.Style);
    }

    private bool MeleeDamageLineClear(
        GodotObject target,
        Vector3 hitPoint,
        Godot.Collections.Array<Rid> exclude,
        out PhysicsRaycastHit blocker)
    {
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                _camera.GlobalPosition,
                hitPoint,
                exclude,
                uint.MaxValue,
                out blocker))
        {
            return false;
        }
        return blocker.Collider == target
            || blocker.Collider is Node colliderNode
                && target is Node targetNode
                && targetNode.IsAncestorOf(colliderNode);
    }

    private bool MeleeSweepAnchorVisible(
        Vector3 point,
        Godot.Collections.Array<Rid> exclude,
        out PhysicsRaycastHit blocker)
    {
        var cameraPosition = _camera.GlobalPosition;
        if (cameraPosition.DistanceSquaredTo(point) <= 0.0064f
            || !TryFindMeleeWorldBlocker(cameraPosition, point, out blocker))
        {
            blocker = default;
            return true;
        }
        return blocker.Position.DistanceSquaredTo(point) <= 0.01f;
    }

    private bool TryShatterVisibleMeleeGlass(Vector3 from, Vector3 to, float damage)
    {
        var bladeDirection = from.DirectionTo(to);
        var glassQueryEnd = to + bladeDirection * 0.04f;
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                from,
                glassQueryEnd,
                BreakableGlassField.GlassCollisionLayer,
                out var glassHit,
                collideWithAreas: true,
                collideWithBodies: false)
            || glassHit.Collider is not BreakableGlassField glass)
        {
            return false;
        }
        if (TryFindMeleeWorldBlocker(
                _camera.GlobalPosition,
                glassHit.Position,
                out var blocker)
            && !(blocker.Collider is StaticBody3D movementBody
                && movementBody.GetParent() == glass
                && blocker.Position.DistanceSquaredTo(glassHit.Position) <= 0.04f))
        {
            return false;
        }
        var direction = from.DirectionTo(glassHit.Position);
        var impacted = BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            from,
            glassHit.Position + direction * 0.04f,
            damage,
            direction,
            out _);
        if (impacted)
        {
            Main?.OnLocalPlayerGlassImpact(
                from,
                glassHit.Position,
                damage,
                melee: true);
        }
        return impacted;
    }

    private bool ApplyMeleeDamage(
        GodotObject target,
        Vector3 point,
        KnifeSkinDefinition definition,
        MeleeAttackDefinition attack)
    {
        if (target is EnemyOperator enemy)
        {
            var damage = definition.BaseDamage
                * attack.DamageMultiplier
                * _rng.RandfRange(0.92f, 1.08f);
            var toAttacker = GlobalPosition - enemy.GlobalPosition;
            var facing = -enemy.GlobalBasis.Z;
            toAttacker.Y = 0.0f;
            facing.Y = 0.0f;
            var backstab = facing.LengthSquared() > 0.01f
                && toAttacker.LengthSquared() > 0.01f
                && facing.Normalized().Dot(toAttacker.Normalized()) < -0.35f;
            var finalDamage = damage * (backstab ? 1.6f : 1.0f);
            var applyLocally = Main?.ShouldApplyLocalMeleeDamage ?? true;
            var killed = applyLocally && enemy.TakeDamage(finalDamage, point, this);
            if (applyLocally)
            {
                EmitSignal(
                    SignalName.HitConfirmed,
                    killed,
                    enemy.LastHitWasHeadshot,
                    enemy.LastHitWasArmored);
            }
            Main?.OnLocalPlayerMeleeHit(
                _camera.GlobalPosition,
                point,
                enemy.NetworkId,
                definition.Id,
                _meleeAttackIndex,
                _meleeSwingSequence,
                _meleeSweepSampleAtMsec,
                finalDamage,
                killed,
                applyLocally && enemy.LastHitWasArmored);
            return true;
        }
        if (target is CivilianNpc civilian)
        {
            var damage = definition.BaseDamage
                * attack.DamageMultiplier
                * _rng.RandfRange(0.92f, 1.08f);
            var killed = civilian.TakeDamage(damage * 0.82f, point, this);
            EmitSignal(SignalName.HitConfirmed, killed, false, false);
            return true;
        }
        if (target is ExplosiveBarrel barrel)
        {
            var damage = definition.BaseDamage
                * attack.DamageMultiplier
                * _rng.RandfRange(0.92f, 1.08f);
            barrel.TakeDamage(damage * 0.4f, point, this);
            EmitSignal(SignalName.HitConfirmed, false, false, false);
            return true;
        }
        if (target is DriveableVehicle vehicle)
        {
            var damage = definition.BaseDamage
                * attack.DamageMultiplier
                * _rng.RandfRange(0.92f, 1.08f);
            var destroyed = vehicle.TakeDamage(damage * 0.55f, point, this);
            EmitSignal(SignalName.HitConfirmed, destroyed, false, false);
            return true;
        }
        if (target is DestructibleAircraft aircraft)
        {
            var damage = definition.BaseDamage
                * attack.DamageMultiplier
                * _rng.RandfRange(0.92f, 1.08f);
            var destroyed = aircraft.TakeDamage(damage * 0.65f, point, this);
            EmitSignal(SignalName.HitConfirmed, destroyed, false, false);
            return true;
        }
        if (target is AircraftShell shell)
        {
            var damage = definition.BaseDamage
                * attack.DamageMultiplier
                * _rng.RandfRange(0.92f, 1.08f);
            var destroyed = shell.TakeDamage(damage * 0.65f, point, this);
            EmitSignal(SignalName.HitConfirmed, destroyed, false, false);
            return true;
        }
        return false;
    }

    private static bool IsMeleeDamageTarget(GodotObject? target)
        => target is EnemyOperator
            or CivilianNpc
            or ExplosiveBarrel
            or DriveableVehicle
            or DestructibleAircraft
            or AircraftShell;

    internal int ResolveMeleeSweepForDiagnostics(
        string definitionId,
        int attackIndex,
        Vector3 previousBase,
        Vector3 previousTip,
        Vector3 currentBase,
        Vector3 currentTip,
        bool beginSwing)
    {
        var definition = KnifeSkinCatalog.Definition(definitionId);
        var attack = MeleeAttackCatalog.AttackFor(definition.Style, attackIndex);
        if (beginSwing)
        {
            _meleeAttackIndex = attackIndex;
            _meleeSwingSequence = unchecked(_meleeSwingSequence + 1);
            if (_meleeSwingSequence <= 0)
            {
                _meleeSwingSequence = 1;
            }
            _meleeHitTargets.Clear();
            _meleeHitTargetRids.Clear();
            _meleeWorldImpactSpawned = false;
        }
        ResolveMeleeSweep(
            definition,
            attack,
            previousBase,
            previousTip,
            currentBase,
            currentTip);
        return _meleeHitTargets.Count;
    }

    internal bool ResolveSuppressedMeleeWallContactForDiagnostics(
        string definitionId,
        int attackIndex,
        Vector3 previousBase,
        Vector3 previousTip,
        Vector3 currentBase,
        Vector3 currentTip)
    {
        var definition = KnifeSkinCatalog.Definition(definitionId);
        var attack = MeleeAttackCatalog.AttackFor(definition.Style, attackIndex);
        var windowStart = Mathf.Max(0.18f, attack.HitProgress - 0.12f);
        _rawMeleeContactPrimed = false;
        _meleeWorldImpactSpawned = false;
        _meleeBladeSweepResolved = false;
        UpdateMeleeWallContactFeedback(
            definition,
            attack,
            windowStart - 0.01f,
            previousBase,
            previousTip,
            clearanceSafe: false);
        UpdateMeleeWallContactFeedback(
            definition,
            attack,
            attack.HitProgress,
            currentBase,
            currentTip,
            clearanceSafe: false);
        return _meleeWorldImpactSpawned;
    }

    internal void PrepareMeleeCombatFixtureForDiagnostics()
    {
        CancelMeleeAction();
        _fireCooldown = 0.0f;
        SetPhysicsProcess(false);
    }

    internal void ConfirmAuthoritativeMeleeHit(bool killed, bool armorHit)
        => EmitSignal(SignalName.HitConfirmed, killed, false, armorHit);
}
