using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    private const float DemolitionRecentDamageThreatSeconds = 1.25f;
    private Vector3 _demolitionEscortForward = Vector3.Forward;
    private bool _demolitionEscortForwardInitialized;

    internal Vector3 DemolitionOrderPositionForDiagnostics => _orderPosition;

    private Vector3 ResolveDemolitionEscortForward(Node3D leader)
    {
        var candidate = leader is CharacterBody3D movingLeader
            ? movingLeader.Velocity
            : Vector3.Zero;
        candidate.Y = 0.0f;
        if (candidate.LengthSquared() >= 0.16f)
        {
            candidate = candidate.Normalized();
            if (!_demolitionEscortForwardInitialized)
            {
                _demolitionEscortForward = candidate;
                _demolitionEscortForwardInitialized = true;
            }
            else
            {
                var blended = _demolitionEscortForward.Lerp(candidate, 0.22f);
                if (blended.LengthSquared() > 0.01f)
                {
                    _demolitionEscortForward = blended.Normalized();
                }
            }
        }
        else if (!_demolitionEscortForwardInitialized)
        {
            candidate = -leader.GlobalBasis.Z;
            candidate.Y = 0.0f;
            _demolitionEscortForward = candidate.LengthSquared() > 0.01f
                ? candidate.Normalized()
                : Vector3.Forward;
            _demolitionEscortForwardInitialized = true;
        }
        return _demolitionEscortForward;
    }

    private void ResetDemolitionEscortForward()
    {
        _demolitionEscortForward = Vector3.Forward;
        _demolitionEscortForwardInitialized = false;
    }

    internal bool HasDemolitionThreatWithin(float range)
    {
        if (_combatTarget is not null
            && IsInstanceValid(_combatTarget)
            && !_combatTarget.IsDead
            && GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) < range
            && (_combatHasSight
                || ReferenceEquals(_combatTarget, _combatThreat)
                    && _combatThreatAge <= DemolitionRecentDamageThreatSeconds))
        {
            return true;
        }
        return _combatThreat is not null
            && IsInstanceValid(_combatThreat)
            && !_combatThreat.IsDead
            && _combatThreatAge <= DemolitionRecentDamageThreatSeconds
            && GlobalPosition.DistanceTo(_combatThreat.GlobalPosition) < range;
    }

    internal bool DemolitionMoveTargets(Vector3 position, float tolerance = 0.25f)
        => Order == SquadOrder.Move
            && _orderPosition.DistanceTo(position) <= tolerance;

    internal void RestoreDemolitionOrderForDiagnostics(SquadOrder order, Vector3 orderPosition)
    {
        Order = order;
        _orderPosition = orderPosition;
        UpdateLabel();
    }

    internal bool AreDemolitionCollisionShapesDisabledForDiagnostics
        => CollisionShapesMatchForDiagnostics(disabled: true);

    internal bool AreDemolitionCollisionShapesEnabledForDiagnostics
        => CollisionShapesMatchForDiagnostics(disabled: false);

    internal bool DemolitionNameplateShowsEliminatedForDiagnostics
        => IsDowned
            && ReviveUsed
            && IsInstanceValid(_nameLabel)
            && _nameLabel.Text.EndsWith("//  ELIMINATED", System.StringComparison.Ordinal);

    public void SetDemolitionRemoteState(
        OperatorRole role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool eliminated)
    {
        SetRemoteState(role, position, rotation, health, eliminated);
        if (!IsNetworkProxy || !eliminated || ReviveUsed)
        {
            return;
        }

        // Demolition's network Dead flag means permanent elimination for this round,
        // unlike extraction's revivable Down flag. Snap the final authoritative pose
        // before physics is disabled so a remote proxy cannot freeze at an old run frame.
        GlobalPosition = position;
        Rotation = rotation;
        Health = Mathf.Clamp(health, 0.0f, MaxHealth);
        IsDowned = true;
        IsBodyBag = false;
        EliminateForDemolitionRound();
    }

    private bool CollisionShapesMatchForDiagnostics(bool disabled)
    {
        var foundShape = false;
        var children = GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is not CollisionShape3D collision)
            {
                continue;
            }
            foundShape = true;
            if (collision.Disabled != disabled)
            {
                return false;
            }
        }
        return foundShape;
    }

    public void EliminateForDemolitionRound()
    {
        if (!IsDowned || IsBodyBag)
        {
            return;
        }
        ReviveUsed = true;
        ResetIncendiaryAvoidance();
        SnapDemolitionEliminationToGround();
        SetDemolitionEliminatedPose();
        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
        var children = GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is CollisionShape3D collision)
            {
                collision.Disabled = true;
            }
        }
        UpdateLabel();
    }

    private void SnapDemolitionEliminationToGround()
    {
        if (!IsInsideTree() || !IsInstanceValid(_collider))
        {
            return;
        }

        var from = GlobalPosition + Vector3.Up * 0.5f;
        var to = GlobalPosition + Vector3.Down * 5.0f;
        if (!PhysicsRaycast.TryHit(GetWorld3D(), from, to, GetRid(), 1, out var hit)
            || hit.Normal.Dot(Vector3.Up) < 0.65f)
        {
            return;
        }

        // CharacterBody3D's origin is at the capsule foot, while the collider
        // itself is centered above it. Preserve that contract when the death
        // pose disables physics so a teammate killed mid-jump cannot freeze in
        // the air.
        var footOffset = 0.0f;
        if (_collider.Shape is CapsuleShape3D capsule)
        {
            footOffset = _collider.Position.Y - capsule.Height * 0.5f;
        }
        var groundedPosition = GlobalPosition;
        groundedPosition.Y = hit.Position.Y - footOffset;
        GlobalPosition = groundedPosition;
        Velocity = Vector3.Zero;
    }

    public void ResetForDemolitionRound(Vector3 spawn)
    {
        GlobalPosition = spawn;
        Rotation = Vector3.Zero;
        Velocity = Vector3.Zero;
        Health = MaxHealth;
        _remoteHealth = MaxHealth;
        IsDowned = false;
        IsBodyBag = false;
        ReviveUsed = false;
        IsExtractionPassenger = false;
        _remoteDown = false;
        ResetIncendiaryAvoidance();
        _reviveTarget = null;
        _revivePoseBlend = 0.0f;
        _skillActionTime = 0.0f;
        _overdriveTime = 0.0f;
        _weaponCooldown = 0.0f;
        SetHoldFire(false);
        ProcessMode = ProcessModeEnum.Inherit;
        SetPhysicsProcess(true);
        Visible = true;
        CollisionLayer = 4;
        CollisionMask = 1 | BreakableGlassField.MovementCollisionLayer;
        var children = GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is CollisionShape3D collision)
            {
                collision.Disabled = false;
            }
        }
        _rig.Visible = true;
        _rig.Position = Vector3.Zero;
        _rig.Rotation = Vector3.Zero;
        if (UsesAuthoredOperatorForDiagnostics)
        {
            _authoredOperatorAnimator.SetRestingPose(HasFireablePrimary);
        }
        _weapon.Visible = HasFireablePrimary;
        _muzzle.Visible = HasFireablePrimary;
        _nameLabel.Visible = true;
        _healthFill.Visible = true;
        ResetDemolitionEscortForward();
        ResetMovementProgress();
        InitializeCombatTactics();
        SetOrder(SquadOrder.Follow, spawn);
        UpdateHealthVisual();
        UpdateLabel();
    }

    public void ConfigureDemolitionRoundLoadout(WeaponBuild? build)
    {
        if (build is null)
        {
            ApplyColdStartUnarmed();
            return;
        }
        EquipWeaponFromLoot(
            build,
            LootGrade.Common,
            LootGrade.Common,
            recoveredAmmoQuantity: 0);
    }
}
