using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class IncendiaryGrenade : RigidBody3D
{
    private const float GroundFuseDuration = 0.4f;
    private const float MaximumAirborneLifetime = 18.0f;
    private const float DamageInterval = 0.4f;
    private const float DamagePerTick = 6.0f;

    public const string ActiveGroupName = "active_incendiary_grenades";
    public const float FireRadius = 4.0f;
    public const float FireDuration = 7.2f;

    public Node? OwnerBody { get; set; }
    public bool DamageEnabled { get; set; } = true;
    public bool IsBurning { get; private set; }
    public float RemainingDuration { get; private set; }
    public bool HasTouchedGround { get; private set; }
    public bool FuseStarted => HasTouchedGround && _armed;
    public int ParticleEmitterCount
        => IsInstanceValid(_fireParticles) ? 1 : 0;

    private MeshInstance3D _casing = null!;
    private GpuParticles3D _fireParticles = null!;
    private bool _armed;
    private float _fuse;
    private float _airborneLifetime;
    private float _damageTimer;
    private FreightTerminalWorld? _registeredWorld;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.48f;
        GravityScale = 1.0f;
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 6;
        AddToGroup(ActiveGroupName);
        _registeredWorld = GetParent() as FreightTerminalWorld;
        _registeredWorld?.RegisterActiveIncendiaryGrenade(this);
        if (OwnerBody is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
        }

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.075f, Height = 0.19f }
        });
        _casing = new MeshInstance3D { Name = "IncendiaryCasingVisibility" };
        AddChild(_casing);
        _casing.AddChild(GrenadeVisualFactory.CreateIncendiaryGrenade(firstPerson: false));
    }

    public override void _ExitTree()
    {
        if (_registeredWorld is not null && IsInstanceValid(_registeredWorld))
        {
            _registeredWorld.UnregisterActiveIncendiaryGrenade(this);
        }
        _registeredWorld = null;
    }

    public void Arm(Vector3 direction, float speed = 14.0f, float loft = 5.0f)
    {
        LinearVelocity = direction.Normalized() * speed + Vector3.Up * loft;
        AngularVelocity = new Vector3(6.0f, 11.0f, 7.0f);
        _armed = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;
        if (!IsBurning)
        {
            if (!_armed)
            {
                return;
            }
            if (!HasTouchedGround)
            {
                _airborneLifetime += step;
                if (_airborneLifetime >= MaximumAirborneLifetime)
                {
                    QueueFree();
                }
                return;
            }
            _fuse -= step;
            if (_fuse <= 0.0f)
            {
                Ignite();
            }
            return;
        }

        RemainingDuration = Mathf.Max(0.0f, RemainingDuration - step);
        _damageTimer -= step;
        if (DamageEnabled && _damageTimer <= 0.0f)
        {
            _damageTimer += DamageInterval;
            _registeredWorld?.ApplyIncendiaryDamageTick(
                GlobalPosition,
                FireRadius,
                DamagePerTick,
                OwnerBody ?? this,
                this);
        }
        if (RemainingDuration <= 0.0f)
        {
            QueueFree();
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (!_armed || HasTouchedGround || IsBurning)
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
        if (!_armed || HasTouchedGround || IsBurning)
        {
            return;
        }
        HasTouchedGround = true;
        _fuse = GroundFuseDuration;
    }

    private void Ignite()
    {
        if (IsBurning)
        {
            return;
        }
        IsBurning = true;
        RemainingDuration = FireDuration;
        _damageTimer = 0.0f;
        _armed = false;
        Freeze = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        _casing.Visible = false;
        BuildFirePresentation();
    }

    private void BuildFirePresentation()
    {
        var fireMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            AlbedoColor = new Color(1.0f, 0.22f, 0.025f, 0.86f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.08f, 0.01f),
            EmissionEnergyMultiplier = 3.2f
        };
        _fireParticles = new GpuParticles3D
        {
            Name = "SharedFireParticles",
            Amount = 56,
            Lifetime = 0.72f,
            Randomness = 0.45f,
            Explosiveness = 0.12f,
            DrawPass1 = new QuadMesh
            {
                Size = new Vector2(0.36f, 0.7f),
                Material = fireMaterial
            },
            ProcessMaterial = new ParticleProcessMaterial
            {
                Direction = Vector3.Up,
                Spread = 38.0f,
                InitialVelocityMin = 0.9f,
                InitialVelocityMax = 2.6f,
                Gravity = new Vector3(0.0f, 0.65f, 0.0f),
                ScaleMin = 0.55f,
                ScaleMax = 1.35f,
                Color = new Color(1.0f, 0.38f, 0.04f, 0.9f)
            },
            Emitting = true,
            VisibilityAabb = new Aabb(
                new Vector3(-FireRadius, -0.2f, -FireRadius),
                new Vector3(FireRadius * 2.0f, 3.5f, FireRadius * 2.0f))
        };
        AddChild(_fireParticles);

        AddChild(new MeshInstance3D
        {
            Name = "FireGroundGlow",
            Mesh = new CylinderMesh
            {
                TopRadius = FireRadius * 0.78f,
                BottomRadius = FireRadius * 0.9f,
                Height = 0.025f,
                RadialSegments = 28
            },
            Position = Vector3.Up * 0.035f,
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1.0f, 0.12f, 0.015f, 0.2f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.06f, 0.005f),
                EmissionEnergyMultiplier = 1.8f
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
}
