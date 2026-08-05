using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class FragGrenade : RigidBody3D
{
    public Node? OwnerBody { get; set; }
    public FreightTerminalWorld? Main { get; set; }

    private bool _armed;
    private float _fuse = 2.35f;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.42f;
        GravityScale = 1.0f;
        ContinuousCd = true;

        AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = 0.09f }
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.11f, 0.14f, 0.09f),
                Metallic = 0.55f,
                Roughness = 0.52f
            }
        });
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
        _fuse -= (float)delta;
        if (_fuse > 0.0f)
        {
            return;
        }
        _armed = false;
        Main?.Explode(GlobalPosition, 8.5f, 125.0f, OwnerBody ?? this);
        QueueFree();
    }
}
