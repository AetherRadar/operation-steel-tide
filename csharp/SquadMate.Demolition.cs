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
        Velocity = Vector3.Zero;
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
        _reviveTarget = null;
        _revivePoseBlend = 0.0f;
        _skillActionTime = 0.0f;
        _overdriveTime = 0.0f;
        _weaponCooldown = 0.0f;
        ProcessMode = ProcessModeEnum.Inherit;
        SetPhysicsProcess(true);
        Visible = true;
        CollisionLayer = 4;
        CollisionMask = 1;
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
}
