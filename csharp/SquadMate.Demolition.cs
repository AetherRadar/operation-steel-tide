using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    internal Vector3 DemolitionOrderPositionForDiagnostics => _orderPosition;

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
        foreach (var child in GetChildren())
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
        foreach (var child in GetChildren())
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
        foreach (var child in GetChildren())
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
        ResetMovementProgress();
        InitializeCombatTactics();
        SetOrder(SquadOrder.Follow, spawn);
        UpdateHealthVisual();
        UpdateLabel();
    }
}
