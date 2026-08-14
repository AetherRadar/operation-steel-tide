using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
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
        _revivingLeader = false;
        _revivePoseBlend = 0.0f;
        _skillActionTime = 0.0f;
        _overdriveTime = 0.0f;
        _weaponCooldown = 0.0f;
        ProcessMode = ProcessModeEnum.Inherit;
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
