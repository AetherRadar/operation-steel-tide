using Godot;

namespace OperationSteelTide;

/// <summary>
/// Slow hostile aircraft bomb. It can be dodged, but weapon damage cannot interrupt it.
/// </summary>
[GlobalClass]
public partial class AircraftShell : CharacterBody3D
{
    [Signal]
    public delegate void DetonatedEventHandler(bool onGround);

    public FreightTerminalWorld? Main { get; set; }
    public Node? OwnerAircraft { get; set; }
    public float Damage { get; private set; } = 42.0f;
    public float BlastRadius { get; private set; } = 11.5f;
    public float Health { get; private set; } = 28.0f;
    public bool IsDestroyed { get; private set; }
    public bool DetonatedOnGround { get; private set; }
    public bool InterceptedInAir { get; private set; }
    public bool OwnerCollisionExcluded { get; private set; }

    public const float TravelSpeed = 20.0f;
    private const float MaxLifetime = 10.0f;
    private const float AirBurstRadius = 3.2f;
    private const float AirBurstDamageScale = 0.28f;

    private Vector3 _direction = Vector3.Down;
    private float _life;
    private MeshInstance3D _mesh = null!;
    private CollisionShape3D _collider = null!;
    private OmniLight3D _glow = null!;
    private bool _armed;

    public void Launch(Vector3 from, Vector3 to, float damage, float blastRadius)
    {
        GlobalPosition = from;
        var delta = to - from;
        _direction = delta.LengthSquared() > 0.001f ? delta.Normalized() : Vector3.Down;
        Damage = damage;
        BlastRadius = blastRadius;
        _life = 0.0f;
        _armed = true;
        var up = Mathf.Abs(_direction.Dot(Vector3.Up)) > 0.98f ? Vector3.Forward : Vector3.Up;
        LookAt(GlobalPosition + _direction, up);
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 1;
        if (OwnerAircraft is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
            OwnerCollisionExcluded = true;
        }
        AddToGroup("aircraft_shells");
        BuildVisual();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_armed || IsDestroyed)
        {
            return;
        }

        var dt = (float)delta;
        _life += dt;
        if (_life > MaxLifetime)
        {
            Detonate(airBurst: GlobalPosition.Y > 2.5f);
            return;
        }

        var motion = _direction * TravelSpeed * dt;
        var hit = MoveAndCollide(motion);
        if (hit is not null)
        {
            // Hit world / structure → full ground blast.
            Detonate(airBurst: false);
            return;
        }

        // Soft ground plane fallback (map sits near y=0).
        if (GlobalPosition.Y <= 0.35f)
        {
            GlobalPosition = new Vector3(GlobalPosition.X, 0.35f, GlobalPosition.Z);
            Detonate(airBurst: false);
        }
    }

    public bool TakeDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (IsDestroyed || amount <= 0.0f)
        {
            return false;
        }

        // Bomb casings are gameplay-invulnerable. Hits provide feedback without changing flight.
        Main?.SpawnImpact(hitPosition, -_direction);
        return false;
    }

    private void Detonate(bool airBurst)
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;
        _armed = false;
        DetonatedOnGround = !airBurst;
        SetPhysicsProcess(false);
        CollisionLayer = 0;
        CollisionMask = 0;
        if (IsInstanceValid(_mesh))
        {
            _mesh.Visible = false;
        }
        if (IsInstanceValid(_glow))
        {
            _glow.Visible = false;
        }
        if (IsInstanceValid(_collider))
        {
            _collider.Disabled = true;
        }

        var radius = airBurst ? AirBurstRadius : BlastRadius;
        var damage = airBurst ? Damage * AirBurstDamageScale : Damage;
        Main?.Explode(GlobalPosition + Vector3.Up * (airBurst ? 0.4f : 0.2f), radius, damage, OwnerAircraft ?? this);
        if (!airBurst)
        {
            // Extra operator splash through the dedicated aircraft strike path.
            Main?.ApplyAircraftStrike(GlobalPosition, radius * 0.85f, damage * 0.55f, OwnerAircraft ?? this);
        }
        else
        {
            Main?.SpawnImpact(GlobalPosition, Vector3.Up);
        }

        EmitSignal(SignalName.Detonated, !airBurst);
        QueueFree();
    }

    private void BuildVisual()
    {
        _collider = new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = 0.38f }
        };
        AddChild(_collider);

        var body = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.2f, 0.16f),
            Metallic = 0.72f,
            Roughness = 0.35f
        };
        var tip = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.12f, 0.05f),
            Metallic = 0.4f,
            Roughness = 0.4f,
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.25f, 0.05f),
            EmissionEnergyMultiplier = 1.8f
        };
        var trail = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.55f, 0.15f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.45f, 0.08f),
            EmissionEnergyMultiplier = 3.2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        _mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.08f,
                BottomRadius = 0.14f,
                Height = 0.85f,
                RadialSegments = 10
            },
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f),
            MaterialOverride = body
        };
        AddChild(_mesh);
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0.0f, 0.0f, -0.48f),
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.22f, RadialSegments = 10, Rings = 6 },
            MaterialOverride = tip
        });
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0.0f, 0.0f, 0.42f),
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = trail
        });
        _glow = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.45f, 0.12f),
            LightEnergy = 1.6f,
            OmniRange = 6.0f,
            ShadowEnabled = false
        };
        AddChild(_glow);
    }
}
