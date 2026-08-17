using Godot;

namespace OperationSteelTide;

public partial class EnemyOperator
{
    public bool IsNetworkProxy { get; private set; }
    public bool IsHumanProxy { get; private set; }
    public long NetworkPeerId { get; private set; }
    public OperatorRole NetworkRole { get; private set; } = OperatorRole.Assault;

    public void ConfigureNetworkProxy(long peerId, OperatorRole role, bool human)
    {
        IsNetworkProxy = true;
        IsHumanProxy = human;
        NetworkPeerId = peerId;
        NetworkRole = role;
        SentryMode = false;
        Alerted = false;
        Suspicion = 0.0f;
        _combatTarget = null;
        _rawTarget = null;
        _searchingLoot = false;
        SetPhysicsProcess(false);
        Name = human ? $"NetworkOpponent_{peerId}" : $"NetworkOpponentAi_{NetworkId}";
    }

    public void SetRemoteNetworkState(
        OperatorRole role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool dead)
    {
        if (!IsNetworkProxy)
        {
            return;
        }
        NetworkRole = role;
        GlobalPosition = position;
        Rotation = rotation;
        Velocity = Vector3.Zero;
        _health = Mathf.Clamp(health, 0.0f, MaxHealth);
        if (dead)
        {
            if (!IsDead)
            {
                Die();
            }
            return;
        }
        if (IsDead)
        {
            IsDead = false;
            CollisionLayer = 2;
            CollisionMask = 1 | 2;
            if (IsInstanceValid(_bodyRoot))
            {
                _bodyRoot.Position = Vector3.Zero;
                _bodyRoot.Rotation = Vector3.Zero;
            }
        }
        SetPhysicsProcess(false);
    }

    public void PlayRemoteNetworkShot(Vector3 end)
    {
        if (!IsNetworkProxy || IsDead || !IsInstanceValid(_muzzle))
        {
            return;
        }
        BeginMuzzleFlash();
        Main?.SpawnTracer(_muzzle.GlobalPosition, end, new Color(1.0f, 0.24f, 0.08f));
    }
}
