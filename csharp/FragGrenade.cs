using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class FragGrenade : RigidBody3D
{
    private const float GroundFuseDuration = 0.55f;
    private const float MaximumAirborneLifetime = 18.0f;

    public Node? OwnerBody { get; set; }
    public FreightTerminalWorld? Main { get; set; }
    public bool HasTouchedGround { get; private set; }
    public bool FuseStarted => HasTouchedGround && _armed;

    private bool _armed;
    private float _fuse;
    private float _airborneLifetime;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.42f;
        GravityScale = 1.0f;
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 6;
        if (OwnerBody is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
        }

        AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = 0.09f }
        });
        AddChild(GrenadeVisualFactory.CreateFragmentationGrenade(firstPerson: false));
    }

    public void Arm(Vector3 direction)
    {
        LinearVelocity = direction.Normalized() * 15.0f + Vector3.Up * 5.2f;
        AngularVelocity = new Vector3(8.0f, 5.0f, 11.0f);
        _armed = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_armed)
        {
            return;
        }
        if (!HasTouchedGround)
        {
            _airborneLifetime += (float)delta;
            if (_airborneLifetime >= MaximumAirborneLifetime)
            {
                QueueFree();
            }
            return;
        }
        _fuse -= (float)delta;
        if (_fuse > 0.0f)
        {
            return;
        }
        _armed = false;
        Main?.Explode(GlobalPosition, 8.5f, 125.0f, OwnerBody ?? this);
        QueueFree();
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (!_armed || HasTouchedGround)
        {
            return;
        }
        for (var contact = 0; contact < state.GetContactCount(); contact++)
        {
            var normal = (GlobalBasis * state.GetContactLocalNormal(contact)).Normalized();
            if (normal.Dot(Vector3.Up) >= 0.35f && state.LinearVelocity.Y <= 3.0f)
            {
                BeginGroundFuse();
                return;
            }
        }
    }

    internal void BeginGroundFuseForDiagnostics() => BeginGroundFuse();

    private void BeginGroundFuse()
    {
        if (!_armed || HasTouchedGround)
        {
            return;
        }
        HasTouchedGround = true;
        _fuse = GroundFuseDuration;
    }
}
