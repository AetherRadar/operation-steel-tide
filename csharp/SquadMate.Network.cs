using Godot;

namespace OperationSteelTide;

public partial class SquadMate
{
    public bool IsNetworkProxy { get; private set; }

    private void CommitAuthoritativeRemoteCombatState()
    {
        if (!IsNetworkProxy)
        {
            return;
        }
        _remoteHealth = Health;
        _remoteDown = IsDowned || IsBodyBag;
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
        ReviveUsed = reviveUsed;
        IsBodyBag = bodyBag;
        HasFireablePrimary = hasWeapon;
        CollisionLayer = bodyBag ? 0u : 4u;
        CollisionMask = bodyBag ? 0u : 1u;
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
