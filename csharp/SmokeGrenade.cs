using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class SmokeGrenade : RigidBody3D
{
    public const string ActiveGroupName = "active_smoke_grenades";
    public const float CloudRadius = 5.6f;
    public const float CloudDuration = 13.0f;
    public const float VisualCoverageRadius = 5.55f;

    public Node? OwnerBody { get; set; }
    public bool IsDeployed { get; private set; }
    public float RemainingDuration { get; private set; }
    public bool OwnerCollisionExcluded { get; private set; }
    public int CloudVisualCount => IsInstanceValid(_cloudRoot) ? _cloudRoot.GetChildCount() : 0;

    private MeshInstance3D _casing = null!;
    private Node3D _cloudRoot = null!;
    private bool _armed;
    private bool _fading;
    private float _fuse = 1.45f;

    public override void _Ready()
    {
        CollisionLayer = 4;
        CollisionMask = 1 | 2;
        Mass = 0.46f;
        GravityScale = 1.0f;
        ContinuousCd = true;
        AddToGroup(ActiveGroupName);
        if (OwnerBody is PhysicsBody3D owner && IsInstanceValid(owner))
        {
            AddCollisionExceptionWith(owner);
            OwnerCollisionExcluded = true;
        }

        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.075f, Height = 0.19f }
        });
        _casing = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.075f, Height = 0.19f, RadialSegments = 12, Rings = 4 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.68f, 0.72f, 0.67f),
                Metallic = 0.42f,
                Roughness = 0.48f
            }
        };
        AddChild(_casing);
    }

    public void Arm(Vector3 direction)
    {
        LinearVelocity = direction.Normalized() * 14.0f + Vector3.Up * 5.0f;
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
            Position = Vector3.Up * 1.25f
        };
        AddChild(_cloudRoot);
        for (var index = 0; index < 24; index++)
        {
            AddSmokeLobe(index);
        }
    }

    private void AddSmokeLobe(int index)
    {
        var angle = index * Mathf.Tau / 24.0f + (index % 3) * 0.21f;
        var ring = index % 4;
        var radial = 0.9f + ring * 0.95f;
        var target = new Vector3(
            Mathf.Cos(angle) * radial,
            0.3f + (index % 5) * 0.48f,
            Mathf.Sin(angle) * radial);
        var alpha = 0.3f + (index % 4) * 0.045f;
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.56f, 0.6f, 0.57f, alpha),
            Roughness = 1.0f
        };
        var lobe = new MeshInstance3D
        {
            Name = $"SmokeLobe_{index + 1:00}",
            Mesh = new SphereMesh
            {
                Radius = 0.82f,
                Height = 1.64f,
                RadialSegments = 10,
                Rings = 6
            },
            Position = Vector3.Up * 0.15f,
            Scale = Vector3.One * 0.12f,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _cloudRoot.AddChild(lobe);
        var scale = new Vector3(
            1.7f + (index % 3) * 0.25f,
            1.35f + (index % 4) * 0.18f,
            1.7f + ((index + 1) % 3) * 0.25f);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(lobe, "position", target, 0.82f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(lobe, "scale", scale, 0.9f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    private void FadeCloud()
    {
        if (!IsInstanceValid(_cloudRoot))
        {
            return;
        }
        foreach (var child in _cloudRoot.GetChildren())
        {
            if (child is not MeshInstance3D lobe)
            {
                continue;
            }
            CreateTween().TweenProperty(lobe, "transparency", 1.0f, 1.5f);
        }
    }
}
