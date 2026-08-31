using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class SmokeGrenade : RigidBody3D
{
    private const float GroundFuseDuration = 0.35f;
    private const float MaximumAirborneLifetime = 18.0f;

    public const string ActiveGroupName = "active_smoke_grenades";
    public const float CloudRadius = 5.6f;
    public const float CloudDuration = 13.0f;
    public const float VisualCoverageRadius = 5.55f;

    public Node? OwnerBody { get; set; }
    public bool IsDeployed { get; private set; }
    public float RemainingDuration { get; private set; }
    public bool OwnerCollisionExcluded { get; private set; }
    public bool HasTouchedGround { get; private set; }
    public bool FuseStarted => HasTouchedGround && _armed;
    public int CloudVisualCount
        => IsInstanceValid(_cloudInstances) && _cloudInstances.Multimesh is not null
            ? _cloudInstances.Multimesh.InstanceCount
            : 0;

    private MeshInstance3D _casing = null!;
    private Node3D _cloudRoot = null!;
    private MultiMeshInstance3D _cloudInstances = null!;
    private bool _armed;
    private bool _fading;
    private float _fuse;
    private float _airborneLifetime;
    private FreightTerminalWorld? _registeredWorld;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.46f;
        GravityScale = 1.0f;
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 6;
        AddToGroup(ActiveGroupName);
        _registeredWorld = GetParent() as FreightTerminalWorld;
        _registeredWorld?.RegisterActiveSmokeGrenade(this);
        if (OwnerBody is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
            OwnerCollisionExcluded = true;
        }

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.075f, Height = 0.19f }
        });
        _casing = new MeshInstance3D { Name = "SmokeCasingVisibility" };
        AddChild(_casing);
        _casing.AddChild(GrenadeVisualFactory.CreateSmokeGrenade(firstPerson: false));
    }

    public override void _ExitTree()
    {
        if (_registeredWorld is not null && IsInstanceValid(_registeredWorld))
        {
            _registeredWorld.UnregisterActiveSmokeGrenade(this);
        }
        _registeredWorld = null;
    }

    public void Arm(Vector3 direction, float speed = 14.0f, float loft = 5.0f)
    {
        LinearVelocity = direction.Normalized() * speed + Vector3.Up * loft;
        AngularVelocity = new Vector3(7.0f, 10.0f, 6.0f);
        _armed = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        var step = (float)delta;
        if (!IsDeployed)
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
                DeploySmoke();
            }
            return;
        }

        RemainingDuration = Mathf.Max(0.0f, RemainingDuration - step);
        if (!_fading && RemainingDuration <= 1.6f)
        {
            _fading = true;
            FadeCloud();
        }
        if (RemainingDuration <= 0.0f)
        {
            QueueFree();
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (!_armed || HasTouchedGround || IsDeployed)
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

    public bool ObscuresSegment(Vector3 from, Vector3 to)
    {
        if (!IsDeployed || RemainingDuration <= 0.0f)
        {
            return false;
        }
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return false;
        }
        var center = GlobalPosition + Vector3.Up * 1.45f;
        var factor = Mathf.Clamp((center - from).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        var closest = from + segment * factor;
        return closest.DistanceSquaredTo(center) <= CloudRadius * CloudRadius;
    }

    private void DeploySmoke()
    {
        if (IsDeployed)
        {
            return;
        }
        IsDeployed = true;
        RemainingDuration = CloudDuration;
        _armed = false;
        Freeze = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        _casing.Visible = false;

        _cloudRoot = new Node3D
        {
            Name = "SmokeCloud",
            Position = Vector3.Up * 1.25f,
            Scale = Vector3.One * 0.12f
        };
        AddChild(_cloudRoot);
        BuildSmokeInstances();
        CreateTween().TweenProperty(_cloudRoot, "scale", Vector3.One, 0.9f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    private void BeginGroundFuse()
    {
        if (!_armed || HasTouchedGround || IsDeployed)
        {
            return;
        }
        HasTouchedGround = true;
        _fuse = GroundFuseDuration;
    }

    private void BuildSmokeInstances()
    {
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.56f, 0.6f, 0.57f, 0.36f),
            Roughness = 1.0f
        };
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = 24,
            Mesh = new SphereMesh
            {
                Radius = 0.82f,
                Height = 1.64f,
                RadialSegments = 10,
                Rings = 6
            }
        };
        _cloudInstances = new MultiMeshInstance3D
        {
            Name = "SmokeLobesMultiMesh",
            Multimesh = multimesh,
            MaterialOverride = material,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        for (var index = 0; index < multimesh.InstanceCount; index++)
        {
            var angle = index * Mathf.Tau / 24.0f + (index % 3) * 0.21f;
            var ring = index % 4;
            var radial = 0.9f + ring * 0.95f;
            var position = new Vector3(
                Mathf.Cos(angle) * radial,
                0.3f + (index % 5) * 0.48f,
                Mathf.Sin(angle) * radial);
            var scale = new Vector3(
                1.7f + (index % 3) * 0.25f,
                1.35f + (index % 4) * 0.18f,
                1.7f + ((index + 1) % 3) * 0.25f);
            multimesh.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(scale), position));
        }
        _cloudRoot.AddChild(_cloudInstances);
    }

    private void FadeCloud()
    {
        if (!IsInstanceValid(_cloudRoot))
        {
            return;
        }
        if (IsInstanceValid(_cloudInstances))
        {
            CreateTween().TweenProperty(_cloudInstances, "transparency", 1.0f, 1.5f);
        }
    }
}
