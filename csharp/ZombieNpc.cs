using Godot;

namespace OperationSteelTide;

/// <summary>Close-range infected used only by the residential survival ruleset.</summary>
public partial class ZombieNpc : CharacterBody3D
{
    public FreightTerminalWorld Main { get; set; } = null!;
    public int Wave { get; set; } = 1;
    public float Health { get; private set; } = 70.0f;
    public bool IsDead { get; private set; }
    private float _attackCooldown;
    private Node3D? _visual;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1;
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.86f, 0),
            Shape = new CapsuleShape3D { Radius = 0.32f, Height = 1.72f }
        });
        try
        {
            var authored = CombatModelLibrary.InstantiateOperator(OperatorVisualId.Garrison, attachDefaultWeapon: false);
            _visual = authored.Root;
            _visual.Name = "InfectedVisual";
            AddChild(_visual);
        }
        catch
        {
            // Authored operator assets are expected in production builds; collision remains usable if unavailable.
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead || !IsInstanceValid(Main) || Main.LocalPlayerRef is null || Main.LocalPlayerRef.IsDead)
            return;
        var player = Main.LocalPlayerRef;
        var toPlayer = player.GlobalPosition - GlobalPosition;
        toPlayer.Y = 0;
        var distance = toPlayer.Length();
        if (distance > 1.35f)
        {
            var direction = toPlayer.Normalized();
            Velocity = new Vector3(direction.X, Velocity.Y, direction.Z) * (1.55f + Wave * 0.08f);
            if (!IsOnFloor()) Velocity += Vector3.Down * 18.0f * (float)delta;
            MoveAndSlide();
            LookAt(GlobalPosition + direction, Vector3.Up);
        }
        else
        {
            Velocity = Vector3.Zero;
            _attackCooldown -= (float)delta;
            if (_attackCooldown <= 0.0f)
            {
                _attackCooldown = Mathf.Max(0.55f, 1.15f - Wave * 0.04f);
                player.TakeDamage(10.0f + Wave * 1.5f, player.GlobalPosition, this);
                Main.NotifySurvivalBreach(GlobalPosition);
            }
        }
    }

    public bool TakeDamage(float amount, Vector3 hitPosition = default, Node? attacker = null)
    {
        if (IsDead) return true;
        Health -= Mathf.Max(0.0f, amount);
        if (Health > 0.0f) return false;
        IsDead = true;
        Main.NotifyZombieEliminated(this);
        QueueFree();
        return true;
    }
}
