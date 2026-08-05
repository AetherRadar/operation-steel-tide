using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class EnemyOperator : CharacterBody3D, ILootSource
{
    [Signal]
    public delegate void EliminatedEventHandler(EnemyOperator enemy);

    public TacticalPlayer Player { get; set; } = null!;
    public FreightTerminalWorld? Main { get; set; }
    public MissionDirector? MissionDirector { get; set; }
    public float DetectionRange { get; set; } = 34.0f;
    public float Suspicion { get; private set; }
    public bool Alerted { get; private set; }
    public bool IsDead { get; private set; }
    public WeaponBuild CarriedWeapon { get; private set; } = WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => IsDead && Loot.Count > 0;
    public float SearchDuration => 1.15f;
    public bool CarriedWeaponVisible => IsInstanceValid(_carriedWeaponRoot) && _carriedWeaponRoot.Visible;

    private float _health = 100.0f;
    private Vector3 _patrolOrigin;
    private Vector3 _patrolTarget;
    private float _fireTimer = 0.5f;
    private float _repathTimer;
    private float _patrolTimer;
    private float _strafeSign = 1.0f;
    private float _animationPhase;
    private bool _seekingCover;
    private bool _inCover;
    private Vector3 _coverTarget;
    private float _coverTimer;
    private float _hitStun;

    private readonly RandomNumberGenerator _rng = new();
    private Node3D _bodyRoot = null!;
    private Marker3D _muzzle = null!;
    private AudioStreamPlayer3D _shotAudio = null!;
    private StandardMaterial3D _mainMaterial = null!;
    private OmniLight3D _muzzleLight = null!;
    private MeshInstance3D _muzzleBloom = null!;
    private Node3D _leftLegRig = null!;
    private Node3D _rightLegRig = null!;
    private Node3D _carriedWeaponRoot = null!;

    public override void _Ready()
    {
        _rng.Randomize();
        _strafeSign = _rng.Randf() < 0.5f ? -1.0f : 1.0f;
        CollisionLayer = 2;
        CollisionMask = 1 | 2;
        FloorSnapLength = 0.35f;
        BuildLootInventory();
        BuildOperator();
        _patrolOrigin = GlobalPosition;
        PickPatrolTarget();
    }

    public string DisplayName(string language) => GameLocalization.IsChinese(language) ? "敌方干员装备" : "Enemy operator gear";

    public void OnSearched()
    {
    }

    public void MarkCarriedWeaponRemoved()
    {
        if (IsInstanceValid(_carriedWeaponRoot))
        {
            _carriedWeaponRoot.Visible = false;
        }
    }

    private void BuildLootInventory()
    {
        var roll = _rng.Randf();
        var platform = roll < 0.55f ? WeaponPlatform.M4A1 : roll < 0.86f ? WeaponPlatform.AK74 : WeaponPlatform.ScarL;
        var tier = _rng.Randf() < 0.16f ? 2 : _rng.Randf() < 0.52f ? 1 : 0;
        CarriedWeapon = WeaponCatalog.Build(platform, tier);
        Loot.Add(new LootItem { Kind = LootItemKind.Weapon, Weapon = CarriedWeapon.Clone() });
        var availableParts = new List<AttachmentDefinition>(WeaponCatalog.AllAttachments);
        for (var count = 0; count < (tier >= 2 ? 2 : 1); count++)
        {
            var part = availableParts[_rng.RandiRange(0, availableParts.Count - 1)];
            Loot.Add(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = part.Id });
            availableParts.Remove(part);
        }
        Loot.Add(new LootItem { Kind = LootItemKind.Ammunition, Quantity = _rng.RandiRange(20, 48) });
        if (_rng.Randf() < 0.32f)
        {
            Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
        }
    }

    private static StandardMaterial3D Material(Color color, float metallic = 0.0f, float roughness = 0.7f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
    }

    private static BoxMesh Box(Vector3 size) => new() { Size = size };

    private static CapsuleMesh Capsule(float radius, float height) => new()
    {
        Radius = radius,
        Height = height,
        RadialSegments = 16,
        Rings = 8
    };

    private static CylinderMesh Cylinder(float radius, float height) => new()
    {
        TopRadius = radius,
        BottomRadius = radius,
        Height = height,
        RadialSegments = 16
    };

    private MeshInstance3D Part(
        PrimitiveMesh mesh,
        Vector3 position,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material
        };
        _bodyRoot.AddChild(part);
        return part;
    }

    private static MeshInstance3D RigPart(
        Node3D parent,
        PrimitiveMesh mesh,
        Vector3 position,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material
        };
        parent.AddChild(part);
        return part;
    }

    private void BuildOperator()
    {
        AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, 0.89f, 0),
            Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.78f }
        });
        _bodyRoot = new Node3D { Name = "EnemyRig" };
        AddChild(_bodyRoot);

        var uniformShift = _rng.RandfRange(-0.018f, 0.025f);
        _mainMaterial = Material(new Color(0.105f + uniformShift, 0.135f + uniformShift, 0.105f + uniformShift), 0.04f, 0.88f);
        var armor = Material(new Color(0.052f, 0.067f, 0.062f), 0.28f, 0.58f);
        var armorEdge = Material(new Color(0.025f, 0.034f, 0.032f), 0.58f, 0.4f);
        var fabric = Material(new Color(0.17f, 0.155f, 0.105f), 0.0f, 0.94f);
        var skin = Material(new Color(0.34f, 0.235f, 0.17f), 0.0f, 0.92f);
        var mask = Material(new Color(0.045f, 0.052f, 0.047f), 0.02f, 0.9f);
        var carriedDefinition = WeaponCatalog.Weapon(CarriedWeapon.Platform);
        var barrelScale = CarriedWeapon.Attachments.TryGetValue(AttachmentSlot.Barrel, out var carriedBarrelId)
            ? WeaponCatalog.Attachment(carriedBarrelId).VisualScale
            : 1.0f;
        var carriedBarrelLength = carriedDefinition.BarrelLength * barrelScale;
        var gunColor = CarriedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Color(0.15f, 0.09f, 0.045f),
            WeaponPlatform.ScarL => new Color(0.3f, 0.25f, 0.17f),
            _ => new Color(0.018f, 0.023f, 0.022f)
        };
        var gun = Material(gunColor, 0.88f, 0.25f);
        var lens = Material(new Color(0.025f, 0.16f, 0.15f), 0.62f, 0.08f);

        Part(Capsule(0.27f, 0.9f), new Vector3(0, 1.08f, 0.02f), _mainMaterial);
        Part(Box(new Vector3(0.58f, 0.54f, 0.24f)), new Vector3(0, 1.19f, -0.015f), armor);
        Part(Box(new Vector3(0.52f, 0.48f, 0.08f)), new Vector3(0, 1.2f, 0.16f), armorEdge);
        Part(Box(new Vector3(0.42f, 0.16f, 0.29f)), new Vector3(0, 0.91f, 0.01f), fabric);
        Part(Box(new Vector3(0.41f, 0.09f, 0.04f)), new Vector3(0, 1.35f, -0.155f), armorEdge);
        for (var pouch = -1; pouch <= 1; pouch++)
        {
            Part(Box(new Vector3(0.13f, 0.17f, 0.1f)), new Vector3(pouch * 0.145f, 0.98f, -0.17f), fabric);
        }
        Part(Box(new Vector3(0.13f, 0.23f, 0.08f)), new Vector3(0.25f, 1.16f, -0.17f), armorEdge);
        Part(Box(new Vector3(0.33f, 0.46f, 0.19f)), new Vector3(0, 1.2f, 0.2f), armor);
        Part(Box(new Vector3(0.035f, 0.38f, 0.035f)), new Vector3(0.14f, 1.58f, 0.23f), armorEdge, new Vector3(0.08f, 0, 0.04f));

        _leftLegRig = BuildLeg(-0.17f, _mainMaterial, armor, gun);
        _rightLegRig = BuildLeg(0.17f, _mainMaterial, armor, gun);

        Part(Capsule(0.12f, 0.48f), new Vector3(-0.34f, 1.29f, -0.04f), fabric, new Vector3(0.66f, 0, -0.16f));
        Part(Capsule(0.12f, 0.48f), new Vector3(0.34f, 1.29f, -0.04f), fabric, new Vector3(0.66f, 0, 0.16f));
        Part(Capsule(0.1f, 0.42f), new Vector3(-0.29f, 1.09f, -0.29f), _mainMaterial, new Vector3(1.18f, 0, -0.1f));
        Part(Capsule(0.1f, 0.42f), new Vector3(0.29f, 1.09f, -0.29f), _mainMaterial, new Vector3(1.18f, 0, 0.1f));
        Part(Box(new Vector3(0.2f, 0.17f, 0.2f)), new Vector3(-0.34f, 1.35f, -0.02f), armor);
        Part(Box(new Vector3(0.2f, 0.17f, 0.2f)), new Vector3(0.34f, 1.35f, -0.02f), armor);
        Part(Box(new Vector3(0.14f, 0.13f, 0.16f)), new Vector3(-0.18f, 1.07f, -0.45f), mask);
        Part(Box(new Vector3(0.14f, 0.13f, 0.16f)), new Vector3(0.18f, 1.07f, -0.45f), mask);

        Part(Cylinder(0.105f, 0.14f), new Vector3(0, 1.51f, 0), skin);
        Part(Capsule(0.158f, 0.34f), new Vector3(0, 1.7f, 0), skin);
        Part(Box(new Vector3(0.28f, 0.13f, 0.055f)), new Vector3(0, 1.67f, -0.145f), mask);
        Part(Box(new Vector3(0.34f, 0.075f, 0.08f)), new Vector3(0, 1.75f, -0.16f), lens);
        Part(Capsule(0.19f, 0.25f), new Vector3(0, 1.84f, 0.01f), armor);
        Part(Box(new Vector3(0.42f, 0.055f, 0.31f)), new Vector3(0, 1.78f, 0), armorEdge);
        Part(Cylinder(0.055f, 0.08f), new Vector3(-0.18f, 1.72f, 0), armorEdge, new Vector3(0, 0, Mathf.Pi / 2));
        Part(Cylinder(0.055f, 0.08f), new Vector3(0.18f, 1.72f, 0), armorEdge, new Vector3(0, 0, Mathf.Pi / 2));
        Part(Box(new Vector3(0.025f, 0.025f, 0.22f)), new Vector3(-0.2f, 1.65f, -0.09f), armorEdge, new Vector3(0.2f, 0.2f, 0));

        _carriedWeaponRoot = new Node3D { Name = "CarriedWeapon" };
        _bodyRoot.AddChild(_carriedWeaponRoot);
        RigPart(
            _carriedWeaponRoot,
            Box(new Vector3(CarriedWeapon.Platform == WeaponPlatform.ScarL ? 0.16f : 0.13f, 0.14f, carriedDefinition.ReceiverLength)),
            new Vector3(0, 1.23f, -0.22f - carriedDefinition.ReceiverLength * 0.5f),
            gun);
        RigPart(_carriedWeaponRoot, Box(new Vector3(0.16f, 0.13f, 0.22f)), new Vector3(0, 1.22f, -0.21f), gun);
        RigPart(_carriedWeaponRoot, Box(new Vector3(0.09f, 0.27f, 0.13f)), new Vector3(0, 1.07f, -0.39f), gun, new Vector3(-0.2f, 0, 0));
        RigPart(_carriedWeaponRoot, Cylinder(0.028f, carriedBarrelLength), new Vector3(0, 1.23f, -0.55f - carriedBarrelLength * 0.5f), gun, new Vector3(Mathf.Pi / 2, 0, 0));
        RigPart(_carriedWeaponRoot, Cylinder(0.045f, 0.13f), new Vector3(0, 1.23f, -0.62f - carriedBarrelLength), gun, new Vector3(Mathf.Pi / 2, 0, 0));
        RigPart(_carriedWeaponRoot, Box(new Vector3(0.11f, 0.1f, 0.13f)), new Vector3(0, 1.36f, -0.43f), gun);
        RigPart(_carriedWeaponRoot, Cylinder(0.036f, 0.03f), new Vector3(0, 1.36f, -0.51f), lens, new Vector3(Mathf.Pi / 2, 0, 0));
        RigPart(_carriedWeaponRoot, Box(new Vector3(0.14f, 0.16f, 0.28f)), new Vector3(0, 1.23f, -0.02f), armorEdge);

        _muzzle = new Marker3D { Position = new Vector3(0, 1.23f, -0.72f - carriedBarrelLength) };
        _carriedWeaponRoot.AddChild(_muzzle);
        _shotAudio = new AudioStreamPlayer3D
        {
            Stream = SoundLab.EnemyShot(),
            VolumeDb = -8.0f,
            MaxDistance = 90.0f
        };
        _muzzle.AddChild(_shotAudio);
        _muzzleLight = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.28f, 0.07f),
            LightEnergy = 0.0f,
            OmniRange = 4.5f,
            ShadowEnabled = false
        };
        _muzzle.AddChild(_muzzleLight);

        _muzzleBloom = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.055f, Height = 0.26f, RadialSegments = 7, Rings = 4 },
            Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            Position = new Vector3(0, 0, -0.1f),
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1.0f, 0.2f, 0.025f, 0.95f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.08f, 0.01f),
                EmissionEnergyMultiplier = 7.0f
            },
            Visible = false
        };
        _muzzle.AddChild(_muzzleBloom);
    }

    private Node3D BuildLeg(float x, Godot.Material uniform, Godot.Material armor, Godot.Material boot)
    {
        var rig = new Node3D { Position = new Vector3(x, 0.82f, 0) };
        _bodyRoot.AddChild(rig);
        RigPart(rig, Capsule(0.13f, 0.48f), new Vector3(0, -0.2f, 0), uniform);
        RigPart(rig, Box(new Vector3(0.2f, 0.13f, 0.18f)), new Vector3(0, -0.43f, -0.055f), armor);
        RigPart(rig, Capsule(0.105f, 0.42f), new Vector3(0, -0.61f, 0.015f), uniform);
        RigPart(rig, Box(new Vector3(0.22f, 0.14f, 0.34f)), new Vector3(0, -0.79f, -0.075f), boot);
        return rig;
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (IsDead || !GodotObject.IsInstanceValid(Player))
        {
            return;
        }

        var velocity = Velocity;
        if (!IsOnFloor())
        {
            velocity.Y -= 22.0f * dt;
        }
        Velocity = velocity;
        _fireTimer -= dt;
        _repathTimer -= dt;
        _patrolTimer -= dt;
        _hitStun = Mathf.Max(0.0f, _hitStun - dt);

        var distance = GlobalPosition.DistanceTo(Player.GlobalPosition);
        var hasSight = distance < DetectionRange && WithinViewCone() && HasLineOfSight();
        if (!Alerted)
        {
            if (MissionDirector?.IsDeploymentProtected() == true)
            {
                Suspicion = Mathf.Max(0.0f, Suspicion - dt * 40.0f);
            }
            else if (hasSight)
            {
                var proximity = Mathf.Clamp(1.0f - distance / DetectionRange, 0.0f, 1.0f);
                Suspicion = Mathf.Min(100.0f, Suspicion + dt * (18.0f + proximity * 58.0f));
            }
            else
            {
                Suspicion = Mathf.Max(0.0f, Suspicion - dt * 13.0f);
            }

            if (Suspicion >= 100.0f)
            {
                Alerted = true;
                MissionDirector?.RaiseConfirmedAlarm();
            }
        }

        if (Alerted)
        {
            Engage(dt, distance, hasSight);
        }
        else
        {
            Patrol(dt);
        }
        MoveAndSlide();
        AnimateBody(dt);
    }

    private bool HasLineOfSight()
    {
        if (Player.IsDead)
        {
            return false;
        }
        var from = GlobalPosition + Vector3.Up * 1.55f;
        var to = Player.GlobalPosition + Vector3.Up * 1.35f;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        query.CollideWithAreas = false;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 && hit["collider"].AsGodotObject() == Player;
    }

    private bool WithinViewCone()
    {
        var eye = GlobalPosition + Vector3.Up * 1.5f;
        var target = Player.GlobalPosition + Vector3.Up * 1.2f;
        var direction = eye.DirectionTo(target);
        return (-GlobalBasis.Z).Dot(direction) > 0.42f;
    }

    public void HearGunshot(Vector3 origin, float radius)
    {
        if (IsDead)
        {
            return;
        }
        var distance = GlobalPosition.DistanceTo(origin);
        if (distance > radius)
        {
            return;
        }
        var strength = Mathf.Clamp((1.0f - distance / radius) * 115.0f, 28.0f, 100.0f);
        Suspicion = Mathf.Max(Suspicion, strength);
        _patrolTarget = origin;
        _patrolTimer = 2.0f;
        if (distance < radius * 0.42f)
        {
            Alerted = true;
            MissionDirector?.RaiseConfirmedAlarm();
        }
    }

    public void SetAlerted(Vector3 investigatePosition)
    {
        if (IsDead)
        {
            return;
        }
        Suspicion = 100.0f;
        Alerted = true;
        _patrolTarget = investigatePosition;
        _fireTimer = _rng.RandfRange(0.45f, 0.9f);
    }

    private void Engage(float delta, float distance, bool hasSight)
    {
        if (UpdateCover(delta))
        {
            return;
        }
        if (_hitStun > 0.0f)
        {
            var stunnedVelocity = Velocity;
            stunnedVelocity.X = Mathf.MoveToward(stunnedVelocity.X, 0.0f, delta * 18.0f);
            stunnedVelocity.Z = Mathf.MoveToward(stunnedVelocity.Z, 0.0f, delta * 18.0f);
            Velocity = stunnedVelocity;
            return;
        }

        var targetFlat = new Vector3(Player.GlobalPosition.X, GlobalPosition.Y, Player.GlobalPosition.Z);
        if (GlobalPosition.DistanceTo(targetFlat) > 0.1f)
        {
            LookAt(targetFlat, Vector3.Up);
        }
        var forward = -GlobalBasis.Z;
        var right = GlobalBasis.X;
        var desired = Vector3.Zero;
        if (distance > 19.0f)
        {
            desired += forward;
        }
        else if (distance < 8.0f)
        {
            desired -= forward;
        }
        if (hasSight && distance < 32.0f)
        {
            desired += right * _strafeSign * 0.58f;
        }
        if (_repathTimer <= 0.0f)
        {
            _repathTimer = _rng.RandfRange(1.0f, 2.1f);
            if (_rng.Randf() < 0.5f)
            {
                _strafeSign *= -1.0f;
            }
        }

        var speed = distance > 19.0f ? 3.7f : 2.4f;
        var movement = desired.Normalized() * speed;
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, movement.X, delta * 11.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, movement.Z, delta * 11.0f);
        Velocity = velocity;
        if (hasSight && distance < 52.0f && _fireTimer <= 0.0f)
        {
            FireAtPlayer(distance);
        }
    }

    private bool UpdateCover(float delta)
    {
        if (_seekingCover)
        {
            var targetFlat = new Vector3(_coverTarget.X, GlobalPosition.Y, _coverTarget.Z);
            var direction = GlobalPosition.DirectionTo(targetFlat);
            if (GlobalPosition.DistanceTo(targetFlat) < 0.85f)
            {
                _seekingCover = false;
                _inCover = true;
                _coverTimer = _rng.RandfRange(1.3f, 2.5f);
                var stopped = Velocity;
                stopped.X = 0.0f;
                stopped.Z = 0.0f;
                Velocity = stopped;
                return true;
            }
            if (direction.LengthSquared() > 0.05f)
            {
                LookAt(targetFlat, Vector3.Up);
            }
            var velocity = Velocity;
            velocity.X = Mathf.MoveToward(velocity.X, direction.X * 4.4f, delta * 14.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * 4.4f, delta * 14.0f);
            Velocity = velocity;
            return true;
        }

        if (_inCover)
        {
            _coverTimer -= delta;
            var velocity = Velocity;
            velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 12.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 12.0f);
            Velocity = velocity;
            var targetFlat = new Vector3(Player.GlobalPosition.X, GlobalPosition.Y, Player.GlobalPosition.Z);
            if (GlobalPosition.DistanceTo(targetFlat) > 0.1f)
            {
                LookAt(targetFlat, Vector3.Up);
            }
            if (_coverTimer <= 0.0f)
            {
                _inCover = false;
                _fireTimer = 0.0f;
                return false;
            }
            return true;
        }
        return false;
    }

    private void Patrol(float delta)
    {
        var targetFlat = new Vector3(_patrolTarget.X, GlobalPosition.Y, _patrolTarget.Z);
        var direction = GlobalPosition.DirectionTo(targetFlat);
        if (GlobalPosition.DistanceTo(targetFlat) < 1.1f || _patrolTimer <= 0.0f)
        {
            PickPatrolTarget();
        }
        if (direction.LengthSquared() > 0.1f)
        {
            LookAt(targetFlat, Vector3.Up);
        }
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * 1.55f, delta * 5.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * 1.55f, delta * 5.0f);
        Velocity = velocity;
    }

    private void PickPatrolTarget()
    {
        _patrolTarget = _patrolOrigin + new Vector3(
            _rng.RandfRange(-4.0f, 4.0f),
            0.0f,
            _rng.RandfRange(-4.0f, 4.0f));
        _patrolTimer = _rng.RandfRange(4.0f, 8.0f);
    }

    private void FireAtPlayer(float distance)
    {
        var stats = CarriedWeapon.Stats();
        _fireTimer = _rng.RandfRange(stats.FireInterval * 3.2f, stats.FireInterval * 6.8f);
        _shotAudio.PitchScale = _rng.RandfRange(0.88f, 1.08f);
        _shotAudio.Play();
        _muzzleLight.LightEnergy = 5.5f;
        _muzzleBloom.Visible = true;
        _muzzleBloom.Scale = Vector3.One * _rng.RandfRange(0.75f, 1.2f);
        var flash = CreateTween();
        flash.TweenProperty(_muzzleLight, "light_energy", 0.0f, 0.05f);
        flash.Parallel().TweenProperty(_muzzleBloom, "scale", Vector3.One * 0.1f, 0.06f);
        flash.TweenCallback(Callable.From(() => _muzzleBloom.Visible = false));

        var rangeFactor = Mathf.Clamp(stats.EffectiveRange / 150.0f, 0.7f, 1.25f);
        var accuracy = Mathf.Clamp(0.86f - distance * 0.011f / rangeFactor, 0.24f, 0.8f);
        var aimPoint = Player.GlobalPosition + Vector3.Up * 1.25f;
        if (_rng.Randf() < accuracy)
        {
            Player.TakeDamage(stats.Damage * _rng.RandfRange(0.24f, 0.34f), aimPoint, this);
        }
        else
        {
            aimPoint += new Vector3(
                _rng.RandfRange(-1.9f, 1.9f),
                _rng.RandfRange(-1.1f, 1.4f),
                _rng.RandfRange(-1.9f, 1.9f));
        }
        Main?.SpawnTracer(_muzzle.GlobalPosition, aimPoint, new Color(1.0f, 0.34f, 0.13f));
    }

    private void AnimateBody(float delta)
    {
        var speed = new Vector2(Velocity.X, Velocity.Z).Length();
        _animationPhase += delta * (4.0f + speed * 1.7f);
        var coverOffset = _inCover ? -0.38f : 0.0f;
        var position = _bodyRoot.Position;
        position.Y = Mathf.Lerp(
            position.Y,
            coverOffset + Mathf.Sin(_animationPhase * 2.0f) * 0.015f * Mathf.Clamp(speed, 0.0f, 1.0f),
            delta * 9.0f);
        _bodyRoot.Position = position;
        var rotation = _bodyRoot.Rotation;
        rotation.Z = Mathf.Lerp(
            rotation.Z,
            Mathf.Sin(_animationPhase) * 0.018f * Mathf.Clamp(speed, 0.0f, 1.0f),
            delta * 8.0f);
        _bodyRoot.Rotation = rotation;
        var stride = Mathf.Sin(_animationPhase) * 0.34f * Mathf.Clamp(speed / 3.7f, 0.0f, 1.0f);
        _leftLegRig.Rotation = new Vector3(stride, 0, 0);
        _rightLegRig.Rotation = new Vector3(-stride, 0, 0);
    }

    public bool TakeDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (IsDead)
        {
            return true;
        }
        Alerted = true;
        if (attacker is TacticalPlayer tacticalPlayer)
        {
            Player = tacticalPlayer;
        }
        var headshot = hitPosition.Y > GlobalPosition.Y + 1.5f;
        _health -= amount * (headshot ? 2.25f : 1.0f);
        _hitStun = 0.14f;
        var original = _mainMaterial.AlbedoColor;
        _mainMaterial.AlbedoColor = new Color(0.62f, 0.12f, 0.07f);
        CreateTween().TweenProperty(_mainMaterial, "albedo_color", original, 0.11f);

        if (_health > 0.0f && !_seekingCover && !_inCover && Main is not null
            && (_health < 76.0f || _rng.Randf() < 0.4f))
        {
            var candidate = Main.FindCoverPoint(GlobalPosition, Player.GlobalPosition);
            if (candidate.Y > -100.0f)
            {
                _coverTarget = candidate;
                _seekingCover = true;
            }
        }
        if (_health <= 0.0f)
        {
            Die();
            return true;
        }
        return false;
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }
        IsDead = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;
        EmitSignal(SignalName.Eliminated, this);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(_bodyRoot, "rotation:z", _rng.Randf() < 0.5f ? -1.38f : 1.38f, 0.52f)
            .SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(_bodyRoot, "position:y", 0.18f, 0.52f);
        tween.Finished += () => SetPhysicsProcess(false);
    }
}
