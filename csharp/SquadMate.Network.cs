using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    public bool IsNetworkProxy { get; private set; }
    internal Vector3 NetworkAuthoritativePosition
        => IsNetworkProxy ? _remotePosition : GlobalPosition;
    internal Vector3 NetworkAuthoritativeRotation
        => IsNetworkProxy ? _remoteRotation : Rotation;
    private bool _networkAbilityApplyEffect;

    private void CommitAuthoritativeRemoteCombatState()
    {
        if (!IsNetworkProxy)
        {
            return;
        }
        var down = IsDowned || IsBodyBag;
        _remoteHealth = Health;
        _remoteDown = down;
        if (down)
        {
            _remotePosition = GlobalPosition;
            _remoteRotation = Rotation;
        }
    }

    public void SetExtractionRemoteState(
        OperatorRole role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool down,
        bool bodyBag,
        bool reviveUsed,
        bool hasWeapon)
    {
        if (!IsNetworkProxy)
        {
            return;
        }
        SetRemoteState(role, position, rotation, health, down || bodyBag);
        var wasWeaponReadied = HasFireablePrimary && !IsDowned && !IsBodyBag;
        ReviveUsed = reviveUsed;
        IsBodyBag = bodyBag;
        HasFireablePrimary = hasWeapon;
        var weaponReadied = hasWeapon && !down && !bodyBag;
        if (wasWeaponReadied && !weaponReadied)
        {
            // A remote state packet can arrive between physics frames.  Stow
            // immediately so a downed/unarmed authored mate never renders one
            // frame with the previous ready-hand attachment.
            _authoredOperatorVisual?.SetWeaponReadied(false);
        }
        CollisionLayer = bodyBag ? 0u : 4u;
        CollisionMask = bodyBag
            ? 0u
            : 1u | BreakableGlassField.MovementCollisionLayer;
        if (IsInstanceValid(_rig))
        {
            _rig.Visible = !bodyBag;
        }
        if (IsInstanceValid(_weapon))
        {
            _weapon.Visible = !bodyBag && hasWeapon;
        }
        if (IsInstanceValid(_nameLabel))
        {
            _nameLabel.Visible = !bodyBag;
        }
        if (IsInstanceValid(_healthFill))
        {
            _healthFill.Visible = !bodyBag;
        }
    }
}
