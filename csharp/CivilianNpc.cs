using System.Collections.Generic;
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
public partial class CivilianNpc : CharacterBody3D, ILootSource
{
    public CivilianRole Role { get; private set; }
    public int TowerIndex { get; private set; }
    public int FloorIndex { get; private set; }
    public bool IsSpecial => Role != CivilianRole.Resident;
    public bool IsDead { get; private set; }
    public bool AssistanceUsed { get; private set; }
    public bool CanOfferAssistance => !IsDead && !AssistanceUsed;
    public float Health { get; private set; } = 45.0f;
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => IsDead && Loot.Count > 0;
    public float SearchDuration => 0.7f;

    private FreightTerminalWorld _main = null!;
    private readonly RandomNumberGenerator _rng = new();
    private Transform3D _towerTransform;
    private Vector3 _homeLocal;
    private Vector3 _targetLocal;
    private Vector2 _roamHalfExtents;
    private float _decisionTimer;
    private float _threatCheckTimer;
    private float _animationTime;
    private float _simulationAccumulator;
    private float _lodCheckTimer;
    private bool _reducedSimulation;
    private bool _cowering;
    private Node3D _rig = null!;
    private Node3D _leftArm = null!;
    private Node3D _rightArm = null!;
    private Node3D _leftLeg = null!;
    private Node3D _rightLeg = null!;
    private Label3D _roleLabel = null!;
    private string _language = "en";

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
        _lodCheckTimer = ((towerIndex * 7 + floorIndex * 3) % 10) * 0.04f;
        Position = _towerTransform * homeLocal;
        Rotation = new Vector3(0, towerTransform.Basis.GetEuler().Y, 0);
    }

    public void SetLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        RefreshRoleLabel();
    }

    public override void _Ready()
    {
        _rng.Randomize();
        // Layer 1 so player weapon rays (default mask) hit civilians without special masks.
        CollisionLayer = 1;
        CollisionMask = 1;
        FloorSnapLength = 0.3f;
        AddToGroup("civilians");
        if (IsSpecial)
        {
            AddToGroup("special_civilians");
        }
        BuildCivilian();
        BuildPersonalLoot();
        PickWanderTarget();
    }

    public string DisplayName(string language) => GameLocalization.IsChinese(language)
        ? Role switch
        {
            CivilianRole.Evacuee => "待撤离人员随身物",
            CivilianRole.VolunteerMedic => "医疗志愿者随身物",
            CivilianRole.CommunityGuard => "社区安保随身物",
            CivilianRole.UtilityWorker => "抢修人员随身物",
            _ => "居民随身物"
        }
        : $"{Role} belongings";

    public void OnSearched()
    {
    }

    public string AssistanceLabel(string language)
    {
        var key = Role switch
        {
            CivilianRole.VolunteerMedic => "civilian_medic_request",
            CivilianRole.CommunityGuard => "civilian_guard_request",
            CivilianRole.UtilityWorker => "civilian_repair_request",
            CivilianRole.Evacuee => "civilian_evac_request",
            _ => "civilian_resident_request"
        };
        var english = Role switch
        {
            CivilianRole.VolunteerMedic => "REQUEST MEDICAL AID",
            CivilianRole.CommunityGuard => "REQUEST LOCAL INTEL",
            CivilianRole.UtilityWorker => "REQUEST FIELD REPAIR",
            CivilianRole.Evacuee => "REQUEST EVAC SUPPLIES",
            _ => "REQUEST RESIDENT SUPPLIES"
        };
        return GameLocalization.Get(key, language, english);
    }

    public bool TryProvideAssistance(TacticalPlayer player)
    {
        if (!CanOfferAssistance || player.IsDead)
        {
            return false;
        }

        var provided = false;
        var messageKey = "resident_supplies";
        var message = "RESIDENT SUPPLIES RECEIVED";
        switch (Role)
        {
            case CivilianRole.VolunteerMedic:
                var healthBefore = player.Health;
                player.RestoreHealth(45.0f);
                provided = player.Health > healthBefore || player.TryCollectArmorPlate() || player.TryCollectAmmo(12);
                messageKey = "civilian_medic_aid";
                message = "MEDICAL AID  //  CONDITION STABILIZED";
                break;
            case CivilianRole.CommunityGuard:
                _main.PerformReconScan(player, GlobalPosition);
                player.TryCollectAmmo(24);
                provided = true;
                messageKey = "civilian_local_intel";
                message = "LOCAL INTEL  //  HOSTILES MARKED";
                break;
            case CivilianRole.UtilityWorker:
                provided = _main.TryRepairNearestVehicle(GlobalPosition, 22.0f, 75.0f)
                    || player.TryCollectArmorPlate()
                    || player.TryCollectAmmo(18);
                messageKey = "civilian_field_repair";
                message = "FIELD REPAIR  //  VEHICLE OR KIT SERVICED";
                break;
            case CivilianRole.Evacuee:
                provided = player.TryCollectAmmo(36) || player.TryCollectArmorPlate();
                messageKey = "civilian_evac_supply";
                message = "EVAC SUPPLIES  //  AMMUNITION RECOVERED";
                break;
            default:
                provided = player.TryCollectAmmo(20) || player.TryCollectArmorPlate();
                break;
        }
        if (!provided)
        {
            return false;
        }

        AssistanceUsed = true;
        _decisionTimer = 5.0f;
        RefreshRoleLabel();
        player.Hud?.ShowLocalizedMessage(messageKey, message, RoleColor());
        return true;
    }

    /// <summary>Player/AI damage path — kills drop searchable personal loot.</summary>
    public bool TakeDamage(float amount, Vector3 hitPosition, Node? attacker = null)
    {
        if (IsDead)
        {
            return true;
        }
        _ = hitPosition;
        _ = attacker;
        Health -= amount;
        _cowering = true;
        if (Health > 0.0f)
        {
            return false;
        }
        Die();
        return true;
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }
        IsDead = true;
        Velocity = Vector3.Zero;
        CollisionLayer = 1;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        if (IsInstanceValid(_rig))
        {
            _rig.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f);
            _rig.Position = new Vector3(0.0f, 0.28f, 0.0f);
        }
        if (IsInstanceValid(_roleLabel))
        {
            _roleLabel.Modulate = new Color(0.9f, 0.35f, 0.28f);
        }
        RefreshRoleLabel();
        _main?.RegisterCivilianCorpse(this);
    }

    private void BuildPersonalLoot()
    {
        Loot.Clear();
        // Always some cash-value ammo / plate; specials can drop better gear.
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            Quantity = _rng.RandiRange(8, 24),
            Grade = LootGrade.Common
        });
        if (_rng.Randf() < 0.55f || IsSpecial)
        {
            Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 1, Grade = LootGrade.Uncommon });
        }
        if (Role == CivilianRole.CommunityGuard || _rng.Randf() < 0.18f)
        {
            Loot.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 0),
                Grade = LootGrade.Uncommon
            });
        }
        if (Role == CivilianRole.VolunteerMedic)
        {
            Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 2, Grade = LootGrade.Rare });
        }
        if (Role == CivilianRole.UtilityWorker && _rng.Randf() < 0.4f)
        {
            Loot.Add(new LootItem
            {
                Kind = LootItemKind.Attachment,
                AttachmentId = "optic_holo",
                Grade = LootGrade.Uncommon
            });
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            return;
        }
        var frameDelta = (float)delta;
        _decisionTimer -= frameDelta;
        _threatCheckTimer -= frameDelta;
        _simulationAccumulator += frameDelta;
        _lodCheckTimer -= frameDelta;
        if (_lodCheckTimer <= 0.0f)
        {
            _lodCheckTimer = 0.45f;
            _reducedSimulation = IsOnFloor()
                && _main.ShouldUseReducedCivilianSimulation(GlobalPosition);
        }
        if (_reducedSimulation && _simulationAccumulator < 0.1f)
        {
            return;
        }
        var dt = Mathf.Min(_simulationAccumulator, 0.12f);
        _simulationAccumulator = 0.0f;
        if (_threatCheckTimer <= 0.0f)
        {
            _threatCheckTimer = 0.45f + _rng.RandfRange(0.0f, 0.25f);
            var cowering = _main.FindNearestEnemy(GlobalPosition, 24.0f) is not null;
            if (_cowering != cowering)
            {
                _cowering = cowering;
                RefreshRoleLabel();
            }
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
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    from,
                    from + direction * 0.9f,
                    GetRid(),
                    1))
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
            Name = "CivilianRoleLabel",
            Position = new Vector3(0, 2.08f, 0),
            Text = RoleStatusLabel(),
            FontSize = 19,
            OutlineSize = 6,
            Modulate = RoleColor(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            VisibilityRangeEnd = 24.0f,
            VisibilityRangeEndMargin = 4.0f
        };
        _roleLabel.AddToGroup("residential_localized_labels");
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

    private void RefreshRoleLabel()
    {
        if (IsInstanceValid(_roleLabel))
        {
            _roleLabel.Text = RoleStatusLabel();
        }
    }

    private string RoleStatusLabel()
    {
        if (IsDead)
        {
            return GameLocalization.Get("civilian_down", _language, "BODY  //  F LOOT");
        }
        var status = AssistanceUsed
            ? GameLocalization.Get("civilian_status_assisted", _language, "ASSISTED")
            : _cowering
                ? GameLocalization.Get("civilian_status_sheltering", _language, "SHELTERING")
                : string.Empty;
        return string.IsNullOrEmpty(status) ? RoleLabel() : $"{RoleLabel()}  //  {status}";
    }

    private string RoleLabel()
    {
        var key = Role switch
        {
            CivilianRole.Evacuee => "civilian_role_evacuee",
            CivilianRole.VolunteerMedic => "civilian_role_medic",
            CivilianRole.CommunityGuard => "civilian_role_guard",
            CivilianRole.UtilityWorker => "civilian_role_utility",
            _ => "civilian_role_resident"
        };
        var english = Role switch
        {
            CivilianRole.Evacuee => "EVACUEE",
            CivilianRole.VolunteerMedic => "MEDICAL VOLUNTEER",
            CivilianRole.CommunityGuard => "COMMUNITY GUARD",
            CivilianRole.UtilityWorker => "UTILITY WORKER",
            _ => "RESIDENT"
        };
        return GameLocalization.Get(key, _language, english);
    }

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
