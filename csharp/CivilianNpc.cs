using Godot;

namespace OperationSteelTide;

public enum CivilianRole
{
    Resident,
    Evacuee,
    VolunteerMedic,
    CommunityGuard,
    UtilityWorker
}

[GlobalClass]
public partial class CivilianNpc : CharacterBody3D
{
    public CivilianRole Role { get; private set; }
    public int TowerIndex { get; private set; }
    public int FloorIndex { get; private set; }
    public bool IsSpecial => Role != CivilianRole.Resident;

    private FreightTerminalWorld _main = null!;
    private readonly RandomNumberGenerator _rng = new();
    private Transform3D _towerTransform;
    private Vector3 _homeLocal;
    private Vector3 _targetLocal;
    private Vector2 _roamHalfExtents;
    private float _decisionTimer;
    private float _threatCheckTimer;
    private float _animationTime;
    private bool _cowering;
    private Node3D _rig = null!;
    private Node3D _leftArm = null!;
    private Node3D _rightArm = null!;
    private Node3D _leftLeg = null!;
    private Node3D _rightLeg = null!;
    private Label3D _roleLabel = null!;

    public void Configure(
        FreightTerminalWorld main,
        CivilianRole role,
        int towerIndex,
        int floorIndex,
        Transform3D towerTransform,
        Vector3 homeLocal,
        Vector2 roamHalfExtents)
    {
        _main = main;
        Role = role;
        TowerIndex = towerIndex;
        FloorIndex = floorIndex;
        _towerTransform = towerTransform;
        _homeLocal = homeLocal;
        _targetLocal = homeLocal;
        _roamHalfExtents = roamHalfExtents;
        Position = _towerTransform * homeLocal;
        Rotation = new Vector3(0, towerTransform.Basis.GetEuler().Y, 0);
    }

    public override void _Ready()
    {
        _rng.Randomize();
        CollisionLayer = 8;
        CollisionMask = 1;
        FloorSnapLength = 0.3f;
        AddToGroup("civilians");
        if (IsSpecial)
        {
            AddToGroup("special_civilians");
        }
        BuildCivilian();
        PickWanderTarget();
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _decisionTimer -= dt;
        _threatCheckTimer -= dt;
        if (_threatCheckTimer <= 0.0f)
        {
            _threatCheckTimer = 0.45f + _rng.RandfRange(0.0f, 0.25f);
            _cowering = _main.FindNearestEnemy(GlobalPosition, 24.0f) is not null;
        }

        if (_cowering)
        {
            Velocity = new Vector3(
                Mathf.MoveToward(Velocity.X, 0.0f, dt * 8.0f),
                IsOnFloor() ? -0.15f : Velocity.Y - 18.0f * dt,
                Mathf.MoveToward(Velocity.Z, 0.0f, dt * 8.0f));
        }
        else
        {
            if (_decisionTimer <= 0.0f || GlobalPosition.DistanceTo(_towerTransform * _targetLocal) < 0.55f)
            {
                PickWanderTarget();
            }
            MoveTowardTarget(dt);
        }

        MoveAndSlide();
        AnimateCivilian(dt);
    }

    private void PickWanderTarget()
    {
        _decisionTimer = _rng.RandfRange(2.4f, 5.8f);
        _targetLocal = _homeLocal + new Vector3(
            _rng.RandfRange(-_roamHalfExtents.X, _roamHalfExtents.X),
            0.0f,
            _rng.RandfRange(-_roamHalfExtents.Y, _roamHalfExtents.Y));
    }

    private void MoveTowardTarget(float delta)
    {
        var target = _towerTransform * _targetLocal;
        target.Y = GlobalPosition.Y;
        var direction = GlobalPosition.DirectionTo(target);
        direction.Y = 0.0f;
        if (direction.LengthSquared() > 0.01f)
        {
            var from = GlobalPosition + Vector3.Up * 0.8f;
            var query = PhysicsRayQueryParameters3D.Create(from, from + direction * 0.9f);
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            query.CollisionMask = 1;
            query.CollideWithAreas = false;
            if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0)
            {
                PickWanderTarget();
                direction = new Vector3(-direction.Z, 0.0f, direction.X);
            }
        }
        var speed = Role == CivilianRole.Evacuee ? 0.72f : 1.05f;
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, delta * 3.2f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, delta * 3.2f);
        velocity.Y = IsOnFloor() ? -0.15f : velocity.Y - 18.0f * delta;
        Velocity = velocity;
        if (direction.LengthSquared() > 0.01f)
        {
            var targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
            Rotation = new Vector3(Rotation.X, Mathf.LerpAngle(Rotation.Y, targetYaw, delta * 4.0f), Rotation.Z);
        }
    }

    private void AnimateCivilian(float delta)
    {
        var speed = new Vector2(Velocity.X, Velocity.Z).Length();
        _animationTime += delta * (2.2f + speed * 5.0f);
        var stride = _cowering ? 0.0f : Mathf.Sin(_animationTime) * Mathf.Clamp(speed, 0.0f, 1.0f) * 0.48f;
        _leftLeg.Rotation = new Vector3(stride, 0, 0);
        _rightLeg.Rotation = new Vector3(-stride, 0, 0);
        _leftArm.Rotation = new Vector3(_cowering ? -1.1f : -stride * 0.7f, 0, 0.08f);
        _rightArm.Rotation = new Vector3(_cowering ? -1.1f : stride * 0.7f, 0, -0.08f);
        _rig.Position = new Vector3(0, Mathf.Lerp(_rig.Position.Y, _cowering ? -0.42f : 0.0f, delta * 6.0f), 0);
        _roleLabel.Text = _cowering ? $"{RoleLabel()}  //  SHELTERING" : RoleLabel();
    }

    private void BuildCivilian()
    {
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.86f, 0),
            Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.72f }
        });
        _rig = new Node3D { Name = "CivilianRig" };
        AddChild(_rig);

        var palette = RolePalette();
        var trousers = Material(new Color(0.12f, 0.15f, 0.16f), 0.0f, 0.9f);
        var skin = Material(new Color(0.5f, 0.34f, 0.24f), 0.0f, 0.95f);
        Part(_rig, new CapsuleMesh { Radius = 0.24f, Height = 0.76f, RadialSegments = 12, Rings = 6 }, new Vector3(0, 1.08f, 0), palette);
        Part(_rig, new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 12, Rings = 6 }, new Vector3(0, 1.68f, 0), skin);
        _leftLeg = new Node3D { Position = new Vector3(-0.15f, 0.76f, 0) };
        _rightLeg = new Node3D { Position = new Vector3(0.15f, 0.76f, 0) };
        _leftArm = new Node3D { Position = new Vector3(-0.31f, 1.32f, 0) };
        _rightArm = new Node3D { Position = new Vector3(0.31f, 1.32f, 0) };
        _rig.AddChild(_leftLeg);
        _rig.AddChild(_rightLeg);
        _rig.AddChild(_leftArm);
        _rig.AddChild(_rightArm);
        Part(_leftLeg, new CapsuleMesh { Radius = 0.1f, Height = 0.7f, RadialSegments = 10, Rings = 5 }, new Vector3(0, -0.34f, 0), trousers);
        Part(_rightLeg, new CapsuleMesh { Radius = 0.1f, Height = 0.7f, RadialSegments = 10, Rings = 5 }, new Vector3(0, -0.34f, 0), trousers);
        Part(_leftArm, new CapsuleMesh { Radius = 0.08f, Height = 0.56f, RadialSegments = 10, Rings = 5 }, new Vector3(0, -0.25f, 0), palette);
        Part(_rightArm, new CapsuleMesh { Radius = 0.08f, Height = 0.56f, RadialSegments = 10, Rings = 5 }, new Vector3(0, -0.25f, 0), palette);
        BuildRoleAccessory(palette);

        _roleLabel = new Label3D
        {
            Position = new Vector3(0, 2.08f, 0),
            Text = RoleLabel(),
            FontSize = 19,
            OutlineSize = 6,
            Modulate = RoleColor(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            VisibilityRangeEnd = 24.0f,
            VisibilityRangeEndMargin = 4.0f
        };
        AddChild(_roleLabel);
    }

    private void BuildRoleAccessory(Godot.Material clothing)
    {
        var dark = Material(new Color(0.045f, 0.055f, 0.055f), 0.15f, 0.76f);
        switch (Role)
        {
            case CivilianRole.Evacuee:
                Part(_rig, new BoxMesh { Size = new Vector3(0.4f, 0.48f, 0.18f) }, new Vector3(0, 1.1f, 0.22f), dark);
                break;
            case CivilianRole.VolunteerMedic:
                var cross = Material(new Color(0.2f, 0.95f, 0.55f), 0.0f, 0.35f, true);
                Part(_rig, new BoxMesh { Size = new Vector3(0.28f, 0.055f, 0.025f) }, new Vector3(0, 1.2f, -0.235f), cross);
                Part(_rig, new BoxMesh { Size = new Vector3(0.055f, 0.28f, 0.025f) }, new Vector3(0, 1.2f, -0.236f), cross);
                break;
            case CivilianRole.CommunityGuard:
                Part(_rig, new BoxMesh { Size = new Vector3(0.42f, 0.44f, 0.08f) }, new Vector3(0, 1.16f, -0.22f), dark);
                Part(_rig, new CylinderMesh { TopRadius = 0.19f, BottomRadius = 0.19f, Height = 0.1f, RadialSegments = 12 }, new Vector3(0, 1.86f, 0), clothing);
                break;
            case CivilianRole.UtilityWorker:
                var helmet = Material(new Color(0.98f, 0.68f, 0.08f), 0.05f, 0.58f);
                Part(_rig, new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.12f, RadialSegments = 12 }, new Vector3(0, 1.86f, 0), helmet);
                break;
        }
    }

    private string RoleLabel() => Role switch
    {
        CivilianRole.Evacuee => "EVACUEE  /  \u5f85\u64a4\u79bb\u4eba\u5458",
        CivilianRole.VolunteerMedic => "MEDICAL VOLUNTEER  /  \u533b\u7597\u5fd7\u613f\u8005",
        CivilianRole.CommunityGuard => "COMMUNITY GUARD  /  \u793e\u533a\u5b89\u4fdd",
        CivilianRole.UtilityWorker => "UTILITY WORKER  /  \u62a2\u4fee\u4eba\u5458",
        _ => "RESIDENT  /  \u5c45\u6c11"
    };

    private Color RoleColor() => Role switch
    {
        CivilianRole.Evacuee => new Color(1.0f, 0.72f, 0.25f),
        CivilianRole.VolunteerMedic => new Color(0.25f, 0.94f, 0.58f),
        CivilianRole.CommunityGuard => new Color(0.32f, 0.7f, 1.0f),
        CivilianRole.UtilityWorker => new Color(1.0f, 0.83f, 0.2f),
        _ => new Color(0.78f, 0.84f, 0.81f)
    };

    private StandardMaterial3D RolePalette() => Material(Role switch
    {
        CivilianRole.Evacuee => new Color(0.72f, 0.28f, 0.12f),
        CivilianRole.VolunteerMedic => new Color(0.76f, 0.82f, 0.78f),
        CivilianRole.CommunityGuard => new Color(0.12f, 0.25f, 0.42f),
        CivilianRole.UtilityWorker => new Color(0.78f, 0.5f, 0.08f),
        _ => new Color(0.3f + _rng.RandfRange(0.0f, 0.18f), 0.34f, 0.38f)
    }, 0.0f, 0.86f);

    private static StandardMaterial3D Material(Color color, float metallic, float roughness, bool emission = false)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness,
            EmissionEnabled = emission,
            Emission = emission ? color : Colors.Black,
            EmissionEnergyMultiplier = emission ? 1.8f : 1.0f
        };
    }

    private static MeshInstance3D Part(Node parent, PrimitiveMesh mesh, Vector3 position, Godot.Material material)
    {
        var part = new MeshInstance3D { Mesh = mesh, Position = position, MaterialOverride = material };
        parent.AddChild(part);
        return part;
    }
}
