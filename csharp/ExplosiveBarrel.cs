using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class ExplosiveBarrel : StaticBody3D
{
    public FreightTerminalWorld? Main { get; set; }
    public bool Exploded { get; private set; }

    private float _health = 42.0f;

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 0;
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.56f, 0.0f),
            Shape = new CylinderShape3D { Radius = 0.38f, Height = 1.12f }
        });

        var barrelMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.38f, 0.075f, 0.035f),
            Metallic = 0.72f,
            Roughness = 0.46f
        };
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0.0f, 0.56f, 0.0f),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.38f,
                BottomRadius = 0.38f,
                Height = 1.12f,
                RadialSegments = 20
            },
            MaterialOverride = barrelMaterial
        });

        var bandMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.075f, 0.07f, 0.06f),
            Metallic = 0.9f,
            Roughness = 0.35f
        };
        foreach (var y in new[] { 0.13f, 0.56f, 0.99f })
        {
            AddChild(new MeshInstance3D
            {
                Position = new Vector3(0.0f, y, 0.0f),
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.405f,
                    BottomRadius = 0.405f,
                    Height = 0.055f,
                    RadialSegments = 20
                },
                MaterialOverride = bandMaterial
            });
        }
    }

    public bool TakeDamage(float amount, Vector3 _hitPosition, Node? attacker = null)
    {
        if (Exploded)
        {
            return false;
        }
        _health -= amount;
        if (_health <= 0.0f)
        {
            Exploded = true;
            Main?.Explode(GlobalPosition + Vector3.Up * 0.5f, 9.0f, 135.0f, attacker ?? this);
            QueueFree();
        }
        return false;
    }
}
