using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class EnemyOperator : CharacterBody3D, ILootSource, IOpenableLootSource, IFlashbangTarget
{
    private static readonly Dictionary<Vector3, BoxMesh> SharedBoxMeshes = new();
    private static readonly Dictionary<Vector2, CapsuleMesh> SharedCapsuleMeshes = new();
    private static readonly Dictionary<Vector2, CylinderMesh> SharedCylinderMeshes = new();

    [Signal]
    public delegate void EliminatedEventHandler(EnemyOperator enemy);

    public TacticalPlayer Player { get; set; } = null!;
    public FreightTerminalWorld? Main { get; set; }
    public MissionDirector? MissionDirector { get; set; }
    public float DetectionRange { get; set; } = 34.0f;
    /// <summary>Flat aim bonus from the deploy-time threat level.</summary>
    public float AccuracyBonus { get; set; }
    public bool SentryMode { get; set; }
    public int NetworkId { get; set; } = -1;
    public ulong SimulationSeed { get; set; }
    /// <summary>Authored character identity chosen before _Ready; garrison defenders never randomize.</summary>
    public OperatorVisualId OperatorVisual { get; set; } = OperatorVisualId.Garrison;
    /// <summary>0 = legacy map NPC garrison. ≥1 = rival extraction squad team.</summary>
    public int TeamId { get; set; }
    public bool IsRivalSquad => TeamId > 0;
    public bool IsProne { get; private set; }
    public bool UsesCover => _inCover || _seekingCover;
    public bool IsSearchingLoot => _searchingLoot;
    public float CurrentHealth => _health;
    public int AttackShotsFired { get; private set; }
    internal int PatrolRouteWaypointCountForDiagnostics => _patrolRoute.Length;
    internal int PatrolRouteIndexForDiagnostics => _patrolRouteIndex;
    /// <summary>Engage target node, including a revivable hostile waiting to be finished.</summary>
    public Node3D? EngageTargetNode =>
        _combatTarget is not null
            ? IsAttackableCombatant(_combatTarget) ? _combatTarget.CombatNode : null
            : GodotObject.IsInstanceValid(_rawTarget) ? _rawTarget : null;
    public float Suspicion { get; private set; }
    public bool Alerted { get; private set; }
    public bool IsDead { get; private set; }
    public WeaponBuild CarriedWeapon { get; private set; } = WeaponCatalog.Build(WeaponPlatform.M4A1, 0);
    /// <summary>False for rival cold-start operators until they loot a firearm. Map NPCs stay armed.</summary>
    public bool HasFireablePrimary { get; private set; } = true;
    public EquipmentItem EquippedHelmet { get; private set; } = EquipmentCatalog.Create("helmet_none");
    public EquipmentItem EquippedBodyArmor { get; private set; } = EquipmentCatalog.Create("armor_none");
    public EquipmentItem EquippedBackpack { get; private set; } = EquipmentCatalog.Create("pack_none");
    public bool LastHitWasHeadshot { get; private set; }
    public bool LastHitWasArmored { get; private set; }
    public Node? LastDamageAttacker { get; private set; }
    public List<LootItem> Loot { get; } = new();
    public Node3D LootNode => this;
    public bool IsSearchable => IsDead;
    public float SearchDuration => 1.15f;
    public bool CarriedWeaponVisible => IsInstanceValid(_carriedWeaponRoot) && _carriedWeaponRoot.Visible;

    public string OperatorCallsign(string language)
    {
        if (IsWorldBoss)
        {
            return GameLocalization.Get("boss_name", language, "TIDE HUNTER");
        }
        if (IsRivalSquad)
        {
            return Name.ToString().Replace('_', '-');
        }
        return GameLocalization.IsChinese(language)
            ? $"\u9a7b\u9632\u5e72\u5458-{NetworkId + 1:00}"
            : $"GARRISON-{NetworkId + 1:00}";
    }

    /// <summary>Rival extraction cold-start: strip carried firearm mesh + remove weapon loot stacks.</summary>
    public void ApplyColdStartUnarmed()
    {
        HasFireablePrimary = false;
        MarkCarriedWeaponRemoved();
        for (var i = Loot.Count - 1; i >= 0; i--)
        {
            if (Loot[i].Kind == LootItemKind.Weapon)
            {
                Loot.RemoveAt(i);
            }
        }
    }

    /// <summary>Diagnostics only: restore a fireable primary without consuming world loot.</summary>
    public void GrantFireablePrimaryForDiagnostics(WeaponBuild? build = null)
    {
        HasFireablePrimary = true;
        CarriedWeapon = (build ?? WeaponCatalog.Build(WeaponPlatform.M4A1, 0)).Clone();
        RefreshShotAudio();
        if (IsInstanceValid(_carriedWeaponRoot))
        {
            _carriedWeaponRoot.Visible = true;
        }
        SetAuthoredWeaponVisible(true);
    }

    public void ConfigureInitialLoadout(WeaponBuild build)
    {
        _configuredLoadout = build.Clone();
    }

    /// <summary>Production path: equip a weapon taken from a loot source (sets HasFireablePrimary).</summary>
    public bool EquipWeaponFromLoot(WeaponBuild build)
    {
        if (build is null)
        {
            return false;
        }
        CarriedWeapon = build.Clone();
        HasFireablePrimary = true;
        RefreshShotAudio();
        if (IsInstanceValid(_carriedWeaponRoot))
        {
            _carriedWeaponRoot.Visible = true;
        }
        SetAuthoredWeaponVisible(true);
        // Keep a clone on the corpse loot table only when already dead searchable.
        return HasFireablePrimary;
    }

    private float _health = 100.0f;
    private Vector3 _patrolOrigin;
    private Vector3 _patrolTarget;
    private Vector3[] _patrolRoute = Array.Empty<Vector3>();
    private int _patrolRouteIndex;
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
    private ISquadCombatant? _combatTarget;
    private Node3D? _rawTarget;
    private float _proneTimer;
    private float _stanceDecisionTimer;
    private float _lootSearchTimer;
    private float _noContactTimer;
    private bool _searchingLoot;
    private Vector3 _lootTarget;
    private WeaponBuild? _configuredLoadout;
    private readonly List<Node3D> _combatTargetCandidates = new(24);
    private float _combatTargetAcquireTimer;
    private float _lineOfSightProbeTimer;
    private bool _cachedLineOfSight;
    private ulong _cachedLineOfSightTargetId;
    private float _crowdSchedulePhase;
    private float _stationaryMoveTimer;
    private Tween? _deathTween;
    // Training-range targets use a reusable downed/reset loop instead of the
    // normal terminal death tween.  The Eliminated signal is synchronous, so
    // the range controller marks this before Die() resumes after EmitSignal.
    private bool _suppressDeathAnimationForTrainingRange;
    /// <summary>How long without in-range contact before map NPCs start looting.</summary>
    private const float NpcLootIdleSeconds = 3.5f;
    /// <summary>Beyond this distance a living hostile is ignored for engagement (still exists on map).</summary>
    private const float DefaultContactAcquireRange = 65.0f;
    private const float SniperContactAcquireRange = 185.0f;
    private const float DownedFinishAcquireRange = 22.0f;
    private const float DownedFinishScorePenalty = 32.0f * 32.0f;
    private const float RivalPlayerSquadPreference = 9.0f * 9.0f;
    private const float GarrisonPlayerSquadPreference = 5.0f * 5.0f;
    private const float ForeignRivalPreference = 30.0f * 30.0f;
    private const float ForeignOperatorPreference = 24.0f * 24.0f;
    private const float DownedFinishLockSeconds = 1.5f;
    private const float ActiveTargetAcquireInterval = 0.24f;
    private const float IdleTargetAcquireInterval = 0.48f;
    private const float ActiveLineOfSightInterval = 0.09f;
    private const float IdleLineOfSightInterval = 0.16f;
    private bool UsesLongRangeRifle => CarriedWeapon.Platform is WeaponPlatform.M24 or WeaponPlatform.AXMC;
    private float CurrentContactAcquireRange => IsWorldBoss
        ? 240.0f
        : SentryMode || HasFireablePrimary && UsesLongRangeRifle
            ? SniperContactAcquireRange
            : DefaultContactAcquireRange;
    private float CurrentFireRange => CarriedWeapon.Platform switch
    {
        WeaponPlatform.AXMC => 235.0f,
        WeaponPlatform.M24 => 175.0f,
        _ => 52.0f
    };

    private static bool IsAttackableCombatant(ISquadCombatant combatant)
        => !combatant.CombatDead || combatant.CombatDowned && combatant.CanBeRevived;

    private ISquadCombatant? _downedFinishTarget;
    private float _downedFinishLockTimer;

    private readonly RandomNumberGenerator _rng = new();
    private Node3D _bodyRoot = null!;
    private CollisionShape3D _collider = null!;
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
        if (SimulationSeed != 0)
        {
            _rng.Seed = SimulationSeed;
        }
        else
        {
            _rng.Randomize();
        }
        _strafeSign = _rng.Randf() < 0.5f ? -1.0f : 1.0f;
        var scheduleSeed = NetworkId >= 0
            ? NetworkId + 1
            : (int)(GetInstanceId() % 1021UL) + 1;
        _crowdSchedulePhase = scheduleSeed * 0.61803398875f % 1.0f;
        _combatTargetAcquireTimer = _crowdSchedulePhase * IdleTargetAcquireInterval;
        _lineOfSightProbeTimer = _crowdSchedulePhase * IdleLineOfSightInterval;
        _squadShareCooldown = _crowdSchedulePhase * 0.55f;
        CollisionLayer = 2;
        // Player/world collision remains bidirectional, while operators no longer solve
        // CharacterBody collisions against every other operator in a dense firefight.
        CollisionMask = 1 | BreakableGlassField.MovementCollisionLayer;
        FloorSnapLength = 0.35f;
        AddToGroup(FlashbangGrenade.TargetGroupName);
        BuildLootInventory();
        BuildOperator();
        if (IsWorldBoss)
        {
            BuildWorldBossVisuals();
        }
        _patrolOrigin = GlobalPosition;
        PickPatrolTarget();
        InitializePursuitState();
        if (IsWorldBoss)
        {
            AddToGroup("world_boss");
        }
        else if (IsRivalSquad)
        {
            AddToGroup("rival_operators");
            DetectionRange = Mathf.Max(DetectionRange, 42.0f);
        }
        else
        {
            AddToGroup("map_npc_operators");
        }
    }

    public string DisplayName(string language)
    {
        if (IsWorldBoss)
        {
            return GameLocalization.Get("boss_loot", language, "Tide Hunter legendary gear");
        }
        return GameLocalization.IsChinese(language)
        ? (IsRivalSquad ? "敌对干员小队装备" : "敌方驻守干员装备")
            : (IsRivalSquad ? "Rival squad gear" : "Enemy operator gear");
    }

    public void OnSearched()
    {
        OpenCorpseLootBackpack();
    }

    public void MarkCarriedWeaponRemoved()
    {
        // Loot can be removed outside the actor's next physics tick.  Stow the
        // authored weapon immediately so a cold-start/unloot transition does
        // not keep solving invisible rifle IK against the hands.
        _authoredOperatorVisual?.SetWeaponReadied(false);
        if (IsInstanceValid(_carriedWeaponRoot))
        {
            _carriedWeaponRoot.Visible = false;
        }
        SetAuthoredWeaponVisible(false);
    }

    private void BuildLootInventory()
    {
        if (IsWorldBoss)
        {
            BuildWorldBossLootInventory();
            return;
        }
        var roll = _rng.Randf();
        var platform = roll < 0.55f ? WeaponPlatform.M4A1 : roll < 0.86f ? WeaponPlatform.AK74 : WeaponPlatform.ScarL;
        var tier = _configuredLoadout is not null
            ? 2
            : _rng.Randf() < 0.16f ? 2 : _rng.Randf() < 0.52f ? 1 : 0;
        CarriedWeapon = _configuredLoadout?.Clone() ?? WeaponCatalog.Build(platform, tier);
        var weaponGrade = LootGrades.FromTier(tier);
        Loot.Add(new LootItem { Kind = LootItemKind.Weapon, Weapon = CarriedWeapon.Clone(), Grade = weaponGrade });
        var availableParts = new List<AttachmentDefinition>(WeaponCatalog.AllAttachments);
        for (var count = 0; count < (tier >= 2 ? 2 : 1); count++)
        {
            var part = availableParts[_rng.RandiRange(0, availableParts.Count - 1)];
            Loot.Add(new LootItem
            {
                Kind = LootItemKind.Attachment,
                AttachmentId = part.Id,
                Grade = tier >= 2 ? LootGrade.Rare : LootGrade.Uncommon
            });
            availableParts.Remove(part);
        }
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = WeaponCatalog.Weapon(CarriedWeapon.Platform).Caliber,
            Quantity = _rng.RandiRange(20, 48),
            Grade = LootGrade.Common
        });
        if (_rng.Randf() < 0.32f)
        {
            Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Grade = LootGrade.Uncommon });
        }
        EquippedHelmet = EquipmentCatalog.Create(_rng.Randf() < 0.24f ? "helmet_heavy" : "helmet_light");
        EquippedBodyArmor = EquipmentCatalog.Create(_rng.Randf() < 0.22f ? "armor_heavy" : "armor_carrier");
        EquippedBackpack = EquipmentCatalog.Create(_rng.Randf() < 0.18f ? "pack_heavy" : "pack_assault");
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquippedHelmet,
            Grade = EquippedHelmet.Definition.Id.Contains("heavy") ? LootGrade.Rare : LootGrade.Uncommon
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquippedBodyArmor,
            Grade = EquippedBodyArmor.Definition.Id.Contains("heavy") ? LootGrade.Epic : LootGrade.Rare
        });
        Loot.Add(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquippedBackpack,
            Grade = LootGrade.Uncommon
        });
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

    internal static void ReleaseSharedPrimitiveMeshes()
    {
        SharedBoxMeshes.Clear();
        SharedCapsuleMeshes.Clear();
        SharedCylinderMeshes.Clear();
    }

    private static BoxMesh Box(Vector3 size)
    {
        if (!SharedBoxMeshes.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = size };
            SharedBoxMeshes[size] = mesh;
        }
        return mesh;
    }

    private static CapsuleMesh Capsule(float radius, float height)
    {
        var key = new Vector2(radius, height);
        if (!SharedCapsuleMeshes.TryGetValue(key, out var mesh))
        {
            mesh = new CapsuleMesh
            {
                Radius = radius,
                Height = height,
                RadialSegments = 16,
                Rings = 8
            };
            SharedCapsuleMeshes[key] = mesh;
        }
        return mesh;
    }

    private static CylinderMesh Cylinder(float radius, float height)
    {
        var key = new Vector2(radius, height);
        if (!SharedCylinderMeshes.TryGetValue(key, out var mesh))
        {
            mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = height,
                RadialSegments = 16
            };
            SharedCylinderMeshes[key] = mesh;
        }
        return mesh;
    }

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
        _collider = new CollisionShape3D
        {
            Position = new Vector3(0, 0.89f, 0),
            Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.78f }
        };
        AddChild(_collider);
        _bodyRoot = new Node3D { Name = "EnemyRig" };
        AddChild(_bodyRoot);

        var uniformShift = _rng.RandfRange(-0.018f, 0.025f);
        _mainMaterial = TacticalSurfaceLibrary.Fabric(
            new Color(0.105f + uniformShift, 0.135f + uniformShift, 0.105f + uniformShift),
            0.92f,
            7.5f);
        var armor = Material(new Color(0.052f, 0.067f, 0.062f), 0.28f, 0.58f);
        var armorEdge = Material(new Color(0.025f, 0.034f, 0.032f), 0.58f, 0.4f);
        var fabric = TacticalSurfaceLibrary.Fabric(new Color(0.17f, 0.155f, 0.105f), 0.96f, 9.0f);
        var skin = Material(new Color(0.34f, 0.235f, 0.17f), 0.0f, 0.92f);
        var mask = Material(new Color(0.045f, 0.052f, 0.047f), 0.02f, 0.9f);
        var carriedDefinition = WeaponCatalog.Weapon(CarriedWeapon.Platform);
        var barrelScale = CarriedWeapon.Attachments.TryGetValue(AttachmentSlot.Barrel, out var carriedBarrelId)
            ? WeaponCatalog.Attachment(carriedBarrelId).VisualScale
            : 1.0f;
        var carriedBarrelLength = carriedDefinition.BarrelLength * barrelScale;
        var gunColor = WeaponPlatformVisualConfig.ThirdPersonGunColor(CarriedWeapon.Platform);
        var gun = TacticalSurfaceLibrary.WeaponFinish(gunColor, 0.88f, 0.25f);
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

        AttachAuthoredOperatorVisual();
        _carriedWeaponRoot = new Node3D { Name = "CarriedWeapon" };
        _bodyRoot.AddChild(_carriedWeaponRoot);
        var receiverWidth = CarriedWeapon.Platform switch
        {
            WeaponPlatform.ScarL => 0.16f,
            WeaponPlatform.M24 => 0.15f,
            WeaponPlatform.AXMC => 0.16f,
            WeaponPlatform.MP5A5 => 0.14f,
            WeaponPlatform.M3A1 => 0.135f,
            _ => 0.13f
        };
        RigPart(
            _carriedWeaponRoot,
            Box(new Vector3(receiverWidth, 0.14f, carriedDefinition.ReceiverLength)),
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
            Stream = SoundLab.EnemyShot(CarriedWeapon),
            VolumeDb = SoundLab.WeaponShotVolumeDb(CarriedWeapon, distant: true),
            MaxDistance = Mathf.Max(90.0f, CarriedWeapon.Stats().SoundRadius * 1.9f),
            UnitSize = 12.0f
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
        if (UsesAuthoredOperatorForDiagnostics)
        {
            foreach (var mesh in CombatModelLibrary.MeshesBelow(_carriedWeaponRoot))
            {
                mesh.Visible = false;
            }
            SetAuthoredWeaponVisible(HasFireablePrimary);
        }
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
        ResetPursuitNavigationMotorFrame();
        if (IsDead || !GodotObject.IsInstanceValid(Player))
        {
            return;
        }

        if (!TryGetSimulationDelta((float)delta, out var dt))
        {
            return;
        }
        RecordCombatMovementTrail();

        UpdatePursuitTimers(dt);
        UpdateCombatTarget(dt);
        UpdateDownedFinishLock(dt);
        UpdateWorldBossState(dt);
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
        UpdateCombatMovementTimers(dt);
        _proneTimer = Mathf.Max(0.0f, _proneTimer - dt);
        if (IsProne && _proneTimer <= 0.0f)
        {
            SetProne(false);
        }
        _stanceDecisionTimer -= dt;
        _lootSearchTimer -= dt;

        // Clear dead raw enemy targets (EnemyOperator is not ISquadCombatant).
        if (_rawTarget is EnemyOperator rawEnemy && rawEnemy.IsDead)
        {
            _rawTarget = null;
        }
        if (_combatTarget is not null && !IsAttackableCombatant(_combatTarget))
        {
            _combatTarget = null;
            _rawTarget = null;
        }

        var hasEngageTarget = EngageTargetNode is not null;
        var smokeEscapeFallback = hasEngageTarget
            ? GlobalPosition - CurrentTargetPosition()
            : -GlobalBasis.Z;
        var smokeEscapeDirection = Vector3.Zero;
        var isInsideSmoke = Main is not null
            && Main.TryGetSmokeEscapeDirection(
                GlobalPosition + Vector3.Up * 0.9f,
                smokeEscapeFallback,
                out smokeEscapeDirection);
        if (hasEngageTarget)
        {
            _noContactTimer = 0.0f;
        }
        else
        {
            _noContactTimer += dt;
            if (Alerted && !IsRivalSquad && !IsPursuing)
            {
                // Drop stale alert when nothing is in contact range so loot can resume.
                Alerted = false;
                Suspicion = Mathf.Max(0.0f, Suspicion - dt * 25.0f);
            }
        }

        if (!hasEngageTarget)
        {
            var followsDemolitionObjective = !isInsideSmoke
                && Main?.TryHandleDemolitionDefenderMovement(
                this,
                dt,
                combatTarget: null,
                targetVisible: false) == true;
            if (followsDemolitionObjective)
            {
                _searchingLoot = false;
            }
            else if (IsWorldBoss)
            {
                _searchingLoot = false;
                UpdateWorldBossPatrol(dt);
            }
            else if (SentryMode)
            {
                _searchingLoot = false;
                HoldSentryPosition(dt);
            }
            else if (IsPursuing)
            {
                UpdateLostContactMovement(dt);
            }
            else
            {
                // Unarmed operators (rivals + NPCs after strip) hunt weapons quickly; armed NPCs loot after idle timeout.
                var lootIdle = HasFireablePrimary ? NpcLootIdleSeconds : 1.2f;
                if (!_searchingLoot && !Alerted && _noContactTimer >= lootIdle && _lootSearchTimer <= 0.0f)
                {
                    // Rivals only search when still unarmed; NPCs always may loot after timeout.
                    if (!HasFireablePrimary || !IsRivalSquad)
                    {
                        BeginLootSearch();
                    }
                }
                if (_searchingLoot)
                {
                    UpdateLootSearch(dt);
                }
                else if (!Alerted)
                {
                    Patrol(dt);
                }
            }
            if (isInsideSmoke)
            {
                ApplySmokeEvasion(dt, smokeEscapeDirection);
            }
            MoveOperator(dt);
            AnimateBody(dt);
            return;
        }

        var distance = GlobalPosition.DistanceTo(CurrentTargetPosition());
        var occupiedVehicleAwareness = HasOccupiedVehicleAwareness(distance);
        var sightEligible = distance < CurrentTargetDetectionRange() && WithinViewCone();
        var hasSight = UpdateCachedLineOfSight(dt, sightEligible);
        // Mid-loot contact: any acquired hostile inside a hard contact bubble ends looting immediately.
        var midLootContact = _searchingLoot && distance < 22.0f;
        if (!Alerted)
        {
            if (MissionDirector?.IsDeploymentProtected() == true && !IsRivalSquad)
            {
                Suspicion = Mathf.Max(0.0f, Suspicion - dt * 40.0f);
            }
            else if (hasSight)
            {
                var proximity = Mathf.Clamp(1.0f - distance / DetectionRange, 0.0f, 1.0f);
                Suspicion = Mathf.Min(100.0f, Suspicion + dt * (18.0f + proximity * 58.0f));
            }
            else if (occupiedVehicleAwareness)
            {
                Suspicion = 100.0f;
            }
            else if (distance < CurrentContactAcquireRange * 0.65f)
            {
                // Close contact without perfect LOS still builds pressure.
                Suspicion = Mathf.Min(100.0f, Suspicion + dt * 22.0f);
            }
            else
            {
                Suspicion = Mathf.Max(0.0f, Suspicion - dt * 13.0f);
            }

            // Rivals press any in-range hostile; NPCs need sight/suspicion/close contact.
            // Looting NPCs always flip to combat on mid-loot contact.
            if (Suspicion >= 100.0f || hasSight || midLootContact || occupiedVehicleAwareness
                || (IsRivalSquad && hasEngageTarget && distance < 24.0f)
                || (!IsRivalSquad && distance < 16.0f))
            {
                Alerted = true;
                _searchingLoot = false;
                MissionDirector?.RaiseConfirmedAlarm();
            }
        }

        // Record confirmed sight before demolition arbitration. Objective runners may
        // keep moving at long range, but they must not erase the contact by turning
        // toward a route waypoint and failing their own view-cone check next frame.
        if (Alerted && hasSight)
        {
            RefreshVisiblePursuitContact();
        }

        // Demolition objective movement is decided after the cached visibility probe.
        // Pursuit therefore cannot starve a plant/defuse route, while a genuinely
        // visible close threat can still hand control to the full combat motor.
        var followsVisibleDemolitionObjective = !isInsideSmoke
            && Main?.TryHandleDemolitionDefenderMovement(
            this,
            dt,
            EngageTargetNode,
            hasSight) == true;
        if (followsVisibleDemolitionObjective)
        {
            _searchingLoot = false;
            FireWhileFollowingDemolitionObjective(distance, hasSight);
            MoveOperator(dt);
            AnimateBody(dt);
            return;
        }

        if (Alerted && !hasSight && !IsPursuing)
        {
            BeginPursuitFromCurrentTarget(shareContact: true);
        }

        if (Alerted || hasSight || midLootContact)
        {
            // Contact mid-loot always drops search and opens fire.
            _searchingLoot = false;
            var combatDistance = hasSight
                ? distance
                : GlobalPosition.DistanceTo(CurrentPursuitDestination());
            UpdateStance(dt, combatDistance, hasSight);
            Engage(dt, combatDistance, hasSight);
        }
        else if (_searchingLoot)
        {
            // Hostile exists but is still far — keep looting until they enter the contact bubble.
            UpdateLootSearch(dt);
        }
        else if (SentryMode)
        {
            HoldSentryPosition(dt);
        }
        else
        {
            Patrol(dt);
        }
        if (isInsideSmoke)
        {
            ApplySmokeEvasion(dt, smokeEscapeDirection);
        }
        MoveOperator(dt);
        AnimateBody(dt);
    }

    /// <summary>
    /// Returns fire without touching the velocity chosen by the demolition objective
    /// motor. This deliberately reuses the normal weapon cadence, damage, and accuracy.
    /// </summary>
    private void FireWhileFollowingDemolitionObjective(float distance, bool hasSight)
    {
        if (Main?.IsDemolitionOpponentChanneling(this) == true)
        {
            return;
        }

        if (TryHandlePendingAirborneAttackShot(distance, hasSight))
        {
            return;
        }

        // Velocity remains owned by the objective motor, while aim remains owned by
        // the recently confirmed hostile. This prevents route LookAt from turning a
        // firing bot away and making it permanently fail its own view cone.
        if (HasFreshConfirmedCombatContact)
        {
            FaceCombatContact(hasSight);
        }

        if (!hasSight
            || _fireTimer > 0.0f
            || distance >= CurrentFireRange)
        {
            return;
        }
        if (_combatTarget is not null)
        {
            FireAtSquad(distance);
        }
        else if (_rawTarget is EnemyOperator rival && !rival.IsDead)
        {
            FireAtNode(rival, distance);
        }
    }

    private void MoveOperator(float delta)
    {
        var escapingIncendiaryFire = ApplyIncendiaryAvoidance(delta);
        if (escapingIncendiaryFire)
        {
            // Pursuit may have prepared a route step before hazard movement took
            // ownership of velocity. Discard that frame so it cannot lift the
            // operator back along the stale route after escaping the fire.
            ResetPursuitNavigationMotorFrame();
        }
        else
        {
            PrepareCombatMovementBeforeMove(delta);
        }
        _stationaryMoveTimer -= delta;
        var stationary = IsOnFloor()
            && Mathf.Abs(Velocity.X) < 0.02f
            && Mathf.Abs(Velocity.Y) < 0.2f
            && Mathf.Abs(Velocity.Z) < 0.02f;
        if (stationary && _stationaryMoveTimer > 0.0f)
        {
            return;
        }

        MoveAndSlide();
        BreakableGlassField.TryShatterMovementBlockerFromCollisions(this);
        if (!escapingIncendiaryFire)
        {
            TryPursuitNavigationStepUp();
        }
        _stationaryMoveTimer = stationary
            ? 0.25f * (0.85f + _crowdSchedulePhase * 0.3f)
            : 0.0f;
    }

    private void HoldSentryPosition(float delta)
    {
        var stopped = Velocity;
        stopped.X = Mathf.MoveToward(stopped.X, 0.0f, delta * 14.0f);
        stopped.Z = Mathf.MoveToward(stopped.Z, 0.0f, delta * 14.0f);
        Velocity = stopped;
    }

    private void UpdateCombatTarget(float delta)
    {
        _combatTargetAcquireTimer -= delta;
        var previousTarget = AssignedCombatTargetNode();
        if (!IsValidHostileTarget(previousTarget))
        {
            _combatTargetAcquireTimer = 0.0f;
        }
        if (_combatTargetAcquireTimer > 0.0f)
        {
            return;
        }

        AcquireCombatTarget();
        TargetAcquisitionCountForDiagnostics++;
        var active = AssignedCombatTargetNode() is not null || Alerted || IsPursuing;
        var interval = active ? ActiveTargetAcquireInterval : IdleTargetAcquireInterval;
        _combatTargetAcquireTimer = interval * (0.85f + _crowdSchedulePhase * 0.3f);
        if (AssignedCombatTargetNode() != previousTarget)
        {
            InvalidateLineOfSightCache();
        }
    }

    private bool UpdateCachedLineOfSight(float delta, bool eligible)
    {
        _lineOfSightProbeTimer -= delta;
        var target = CurrentBallisticTargetNode();
        var targetId = target is not null && GodotObject.IsInstanceValid(target)
            ? target.GetInstanceId()
            : 0UL;
        if (targetId != _cachedLineOfSightTargetId)
        {
            _cachedLineOfSightTargetId = targetId;
            _lineOfSightProbeTimer = 0.0f;
            _cachedLineOfSight = false;
        }
        if (!eligible || targetId == 0UL)
        {
            _cachedLineOfSight = false;
            return false;
        }
        if (_lineOfSightProbeTimer > 0.0f)
        {
            return _cachedLineOfSight;
        }

        _cachedLineOfSight = HasLineOfSight();
        var interval = Alerted || IsPursuing
            ? ActiveLineOfSightInterval
            : IdleLineOfSightInterval;
        _lineOfSightProbeTimer = interval * (0.85f + _crowdSchedulePhase * 0.3f);
        return _cachedLineOfSight;
    }

    private void InvalidateLineOfSightCache()
    {
        _cachedLineOfSight = false;
        _cachedLineOfSightTargetId = 0UL;
        _lineOfSightProbeTimer = 0.0f;
    }

    private void AcquireCombatTarget()
    {
        var previousTarget = AssignedCombatTargetNode();
        // A direct hit wins the normal proximity rescore for the brief reaction window.
        // This keeps the recorded attacker and blind-fire contact coherent inside smoke.
        if (HasRecentDamageThreat && IsValidHostileTarget(previousTarget))
        {
            return;
        }
        var retainPrevious = CanRetainPursuitTarget(previousTarget);
        _combatTarget = null;
        _rawTarget = null;
        var contactAcquireRange = CurrentContactAcquireRange;
        var contactAcquireRangeSq = contactAcquireRange * contactAcquireRange;
        if (Main is null)
        {
            // Without a world, only lock the player if actually nearby.
            if (GodotObject.IsInstanceValid(Player) && IsAttackableCombatant(Player)
                && GlobalPosition.DistanceSquaredTo(Player.GlobalPosition) <= contactAcquireRangeSq)
            {
                _combatTarget = Player;
                _rawTarget = Player;
            }
            else if (retainPrevious)
            {
                AssignCombatTarget(previousTarget);
            }
            return;
        }

        // Prefer active hostiles within contact range. Revivable downed targets remain
        // attackable at close range so operators can secure them before a rescue.
        Node3D? bestNode = null;
        ISquadCombatant? bestCombatant = null;
        var bestScore = float.PositiveInfinity;
        var acquireRangeSq = IsRivalSquad
            ? contactAcquireRangeSq * 1.35f * 1.35f
            : contactAcquireRangeSq;

        Main.CollectHostileTargetsFor(this, Mathf.Sqrt(acquireRangeSq), _combatTargetCandidates);
        foreach (var candidate in _combatTargetCandidates)
        {
            TargetCandidateEvaluationCountForDiagnostics++;
            if (!TryScoreCombatCandidate(
                    candidate,
                    acquireRangeSq,
                    out var score,
                    out var candidateCombatant)
                || score >= bestScore)
            {
                continue;
            }
            bestScore = score;
            bestNode = candidate;
            bestCombatant = candidateCombatant;
        }

        if (bestCombatant is not null)
        {
            _combatTarget = bestCombatant;
            _rawTarget = bestCombatant.CombatNode;
        }
        else if (bestNode is EnemyOperator enemyTarget)
        {
            // EnemyOperator is not ISquadCombatant — engage via raw node + FireAtNode.
            _combatTarget = null;
            _rawTarget = enemyTarget;
        }
        else if (bestNode is not null)
        {
            _rawTarget = bestNode;
        }
        var previousWasDowned = previousTarget is ISquadCombatant { CombatDowned: true };
        if (retainPrevious && !previousWasDowned && previousTarget is not null
            && (bestNode is null || bestNode != previousTarget && bestScore > 18.0f * 18.0f))
        {
            AssignCombatTarget(previousTarget);
        }
        else if (bestNode != previousTarget && IsPursuing)
        {
            ClearPursuitMemory(clearTarget: false);
        }
        // No fallback to infinitely-distant player — leave EngageTargetNode null so loot idle can run.
    }

    private bool TryScoreCombatCandidate(
        Node3D candidate,
        float acquireRangeSquared,
        out float score,
        out ISquadCombatant? candidateCombatant)
    {
        score = float.PositiveInfinity;
        candidateCombatant = null;
        if (!GodotObject.IsInstanceValid(candidate))
        {
            return false;
        }

        var distanceSquared = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
        if (distanceSquared > CandidateAcquireRangeSquared(candidate, acquireRangeSquared))
        {
            return false;
        }
        candidateCombatant = candidate as ISquadCombatant;
        if (candidateCombatant?.CombatDowned == true
            && distanceSquared > DownedFinishAcquireRange * DownedFinishAcquireRange)
        {
            return false;
        }

        var bias = 0.0f;
        if (candidate is TacticalPlayer || candidate is SquadMate)
        {
            bias = IsRivalSquad
                ? -RivalPlayerSquadPreference
                : -GarrisonPlayerSquadPreference;
        }
        else if (candidate is EnemyOperator other && IsHostileTo(other))
        {
            bias = IsRivalSquad && other.IsRivalSquad
                ? -ForeignRivalPreference
                : -ForeignOperatorPreference;
        }
        if (candidateCombatant?.CombatDowned == true)
        {
            bias += DownedFinishScorePenalty;
        }
        bias += OccupiedVehicleTargetBias(candidate);
        score = distanceSquared + bias;
        return true;
    }

    private void UpdateDownedFinishLock(float delta)
    {
        if (_combatTarget is not { CombatDowned: true, CanBeRevived: true })
        {
            _downedFinishTarget = null;
            _downedFinishLockTimer = 0.0f;
            return;
        }
        if (!ReferenceEquals(_downedFinishTarget, _combatTarget))
        {
            _downedFinishTarget = _combatTarget;
            _downedFinishLockTimer = DownedFinishLockSeconds;
            return;
        }
        _downedFinishLockTimer = Mathf.Max(0.0f, _downedFinishLockTimer - delta);
    }

    private ILootSource? _lootSourceTarget;

    private void BeginLootSearch()
    {
        _lootSourceTarget = null;
        // Prefer a weapon cache when unarmed (cold-start re-arm path).
        if (!HasFireablePrimary)
        {
            var weaponSource = Main?.FindNearestWeaponLootSource(GlobalPosition, 90.0f);
            if (weaponSource is not null && IsInstanceValid(weaponSource.LootNode))
            {
                _lootSourceTarget = weaponSource;
                _searchingLoot = true;
                _lootTarget = weaponSource.LootNode.GlobalPosition;
                _lootSearchTimer = _rng.RandfRange(2.5f, 4.5f);
                return;
            }
        }
        var point = Main?.FindNearestLootPoint(GlobalPosition, 55.0f);
        if (point is null)
        {
            // Retry later instead of spinning every frame.
            _lootSearchTimer = _rng.RandfRange(3.0f, 6.0f);
            return;
        }
        _searchingLoot = true;
        _lootTarget = point.Value;
        // Hold at the cache for a while before returning to patrol.
        _lootSearchTimer = _rng.RandfRange(4.0f, 7.0f);
    }

    /// <summary>
    /// Diagnostics only: force the no-contact timer past the idle threshold so the next
    /// _PhysicsProcess tick can enter loot via the real BeginLootSearch path (not a bypass).
    /// </summary>
    public void ForceNoContactTimerForDiagnostics(float seconds)
    {
        _noContactTimer = Mathf.Max(0.0f, seconds);
        _lootSearchTimer = 0.0f;
        Alerted = false;
        Suspicion = 0.0f;
        _combatTarget = null;
        _rawTarget = null;
        _searchingLoot = false;
        ClearPursuitMemory(clearTarget: false);
    }

    /// <summary>Zero fire cooldown so the next Engage tick can call the real FireAt* path immediately.</summary>
    public void ArmWeaponForDiagnostics()
    {
        _fireTimer = 0.0f;
        _stanceDecisionTimer = 0.0f;
    }

    /// <summary>Reset cover/prone/loot so headless scenarios start from a known AI state.</summary>
    public void ResetTacticalStateForDiagnostics()
    {
        _deathTween?.Kill();
        _deathTween = null;
        _suppressDeathAnimationForTrainingRange = false;
        SetPhysicsProcess(true);
        ResetCorpseLootBackpackForDiagnostics();
        if (IsDead)
        {
            // Revive dead diagnostics subjects so multi-phase validators can reuse them.
            IsDead = false;
            CollisionLayer = 2;
            CollisionMask = 1 | BreakableGlassField.MovementCollisionLayer;
            SetPhysicsProcess(true);
            ProcessMode = ProcessModeEnum.Inherit;
            if (IsInstanceValid(_bodyRoot))
            {
                _bodyRoot.Rotation = Vector3.Zero;
                _bodyRoot.Position = Vector3.Zero;
            }
        }
        _health = MaxHealth;
        _seekingCover = false;
        _inCover = false;
        _coverTimer = 0.0f;
        _searchingLoot = false;
        _lootSearchTimer = 0.0f;
        _noContactTimer = 0.0f;
        _hitStun = 0.0f;
        _fireTimer = 0.0f;
        _stanceDecisionTimer = 0.0f;
        _proneTimer = 0.0f;
        SetProne(false);
        ResetCombatMovementState();
        Suspicion = 0.0f;
        Alerted = false;
        _combatTarget = null;
        _rawTarget = null;
        _downedFinishTarget = null;
        _downedFinishLockTimer = 0.0f;
        _combatTargetAcquireTimer = 0.0f;
        InvalidateLineOfSightCache();
        ResetPursuitStateForDiagnostics();
        Main?.InvalidateCombatContactRelayForDiagnostics(TeamId);
        // Phase-1 duel may have removed this unit from the world roster on death.
        // Re-list so later validator phases (and AcquireCombatTarget) can see us again.
        Main?.EnsureEnemyRegisteredForDiagnostics(this);
    }

    /// <summary>Clear alert so idle behaviors (loot search) can run on the real path.</summary>
    public void ClearAlertForDiagnostics()
    {
        Alerted = false;
        Suspicion = 0.0f;
        _searchingLoot = false;
        SetProne(false);
        SetCombatCrouched(false);
        var leftCover = _seekingCover || _inCover;
        _seekingCover = false;
        _inCover = false;
        if (leftCover)
        {
            UpdateAuthoredStanceCollider();
        }
        _noContactTimer = 0.0f;
        _combatTarget = null;
        _rawTarget = null;
        ClearPursuitMemory(clearTarget: false);
    }

    private void UpdateLootSearch(float delta)
    {
        // Refresh target position if the source is still valid (pickups don't move, corpses might).
        if (_lootSourceTarget is not null && GodotObject.IsInstanceValid(_lootSourceTarget.LootNode))
        {
            _lootTarget = _lootSourceTarget.LootNode.GlobalPosition;
        }
        var targetFlat = new Vector3(_lootTarget.X, GlobalPosition.Y, _lootTarget.Z);
        if (GlobalPosition.DistanceTo(targetFlat) < 1.6f)
        {
            var stopped = Velocity;
            stopped.X = 0.0f;
            stopped.Z = 0.0f;
            Velocity = stopped;
            // At cache: if unarmed, claim a weapon from the real loot source (production path).
            if (!HasFireablePrimary && Main is not null)
            {
                var source = _lootSourceTarget;
                if (source is null || !source.IsSearchable)
                {
                    source = Main.FindNearestWeaponLootSource(GlobalPosition, 3.5f);
                }
                if (source is not null && Main.TryEquipWeaponFromLootSource(this, source))
                {
                    _searchingLoot = false;
                    _lootSourceTarget = null;
                    _lootSearchTimer = _rng.RandfRange(2.0f, 5.0f);
                    return;
                }
            }
            // "Searching" hold, then resume patrol.
            if (_lootSearchTimer <= 0.0f)
            {
                _searchingLoot = false;
                _lootSourceTarget = null;
                _lootSearchTimer = _rng.RandfRange(10.0f, 18.0f);
            }
            return;
        }
        LookAt(targetFlat, Vector3.Up);
        var direction = GlobalPosition.DirectionTo(targetFlat);
        direction.Y = 0.0f;
        var speed = HasFireablePrimary ? 2.6f : 3.4f; // unarmed hustle to the cache
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, delta * 10.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, delta * 10.0f);
        Velocity = velocity;
    }

    private void UpdateStance(float delta, float distance, bool hasSight)
    {
        var isDemolitionCombatant = Main?.IsDemolitionMode == true;
        if (IsWorldBoss)
        {
            if (IsProne)
            {
                SetProne(false);
            }
            _seekingCover = false;
            _inCover = false;
            return;
        }
        if (SentryMode && !isDemolitionCombatant)
        {
            if (IsProne)
            {
                SetProne(false);
            }
            _seekingCover = false;
            _inCover = false;
            return;
        }
        if (isDemolitionCombatant)
        {
            UpdateDemolitionCombatStance(distance, hasSight);
            return;
        }
        if (_stanceDecisionTimer > 0.0f)
        {
            return;
        }
        _stanceDecisionTimer = _rng.RandfRange(0.9f, 1.8f);
        // Rivals prone more aggressively to break headshot lines; NPCs less so.
        var proneChance = IsRivalSquad ? 0.72f : 0.28f;
        var wantProne = !_seekingCover
            && (hasSight || distance < 28.0f)
            && distance > 9.0f
            && distance < 40.0f
            && (_inCover || _rng.Randf() < proneChance);
        if (wantProne && !IsProne)
        {
            SetProne(true);
            _proneTimer = _rng.RandfRange(1.2f, 2.6f);
        }
        else if (IsProne && (distance < 7.0f || !hasSight && distance > 42.0f))
        {
            SetProne(false);
        }
        var coverSeekChance = IsRivalSquad ? 0.55f : 0.0f;
        if (coverSeekChance > 0.0f
            && !_seekingCover
            && !_inCover
            && !IsProne
            && Main is not null
            && distance > 12.0f
            && _rng.Randf() < coverSeekChance)
        {
            var cover = Main.FindCoverPoint(GlobalPosition, CurrentThreatPosition(hasSight));
            if (cover.Y > -500.0f && cover.DistanceTo(GlobalPosition) < 22.0f)
            {
                _seekingCover = true;
                _coverTarget = cover;
            }
        }
    }

    public void SetProne(bool prone)
        => _ = TrySetPronePosture(prone, StandingColliderHeight);

    public bool IsHostileTo(EnemyOperator other)
    {
        if (other is null || other == this || other.IsDead)
        {
            return false;
        }
        return TeamId != other.TeamId;
    }

    private Vector3 CurrentTargetPoint()
    {
        if (TryResolveOccupiedVehicleTarget(out var vehicle))
        {
            return vehicle!.HostileAimPoint(GlobalPosition);
        }
        if (_combatTarget is not null && IsAttackableCombatant(_combatTarget))
        {
            return _combatTarget.HitPoint(HitRegion.Torso);
        }
        if (_rawTarget is not null && GodotObject.IsInstanceValid(_rawTarget))
        {
            var targetHeight = _rawTarget is EnemyOperator rival
                ? rival.CombatAimHeight
                : 1.2f;
            return _rawTarget.GlobalPosition + Vector3.Up * targetHeight;
        }
        return Player.HitPoint(HitRegion.Torso);
    }

    private Vector3 CurrentTargetPosition()
    {
        if (TryResolveOccupiedVehicleTarget(out var vehicle))
        {
            return vehicle!.GlobalPosition;
        }
        if (_combatTarget is not null)
        {
            return _combatTarget.CombatNode.GlobalPosition;
        }
        if (_rawTarget is not null && GodotObject.IsInstanceValid(_rawTarget))
        {
            return _rawTarget.GlobalPosition;
        }
        return Player.GlobalPosition;
    }

    private bool HasLineOfSight()
    {
        LineOfSightProbeCountForDiagnostics++;
        var targetNode = CurrentBallisticTargetNode();
        if (targetNode is null || !GodotObject.IsInstanceValid(targetNode))
        {
            return false;
        }
        if (_combatTarget is not null && !IsAttackableCombatant(_combatTarget))
        {
            return false;
        }
        if (FlashbangSuppressesVision)
        {
            return false;
        }
        var from = GlobalPosition + Vector3.Up * CombatEyeHeight;
        var to = CurrentTargetPoint();
        if (Main?.IsLineObscuredBySmoke(from, to) == true)
        {
            return false;
        }
        if (!PhysicsRaycast.TryHit(
                GetWorld3D(),
                from,
                to,
                GetRid(),
                BreakableGlassField.SightCollisionMask,
                out var hit))
        {
            return false;
        }
        var collider = hit.Collider;
        return collider == targetNode || collider is Node node && targetNode.IsAncestorOf(node);
    }

    private Vector3 RawMuzzlePosition
    {
        get
        {
            if (!IsInstanceValid(_muzzle))
            {
                return GlobalPosition + Vector3.Up * CombatMuzzleHeight;
            }
            var muzzle = _muzzle.GlobalPosition;
            if (IsProne || IsCrouched)
            {
                muzzle.Y = GlobalPosition.Y + CombatMuzzleHeight;
            }
            return muzzle;
        }
    }

    private Vector3 ResolveBallisticShotOrigin()
    {
        var bodyOrigin = GlobalPosition + Vector3.Up * CombatMuzzleHeight;
        return Ballistics.ResolveShotOrigin(GetWorld3D(), bodyOrigin, RawMuzzlePosition, GetRid());
    }

    internal bool HasClearBallisticPath(Node target, Vector3 aimPoint)
    {
        if (!GodotObject.IsInstanceValid(target))
        {
            return false;
        }
        var origin = ResolveBallisticShotOrigin();
        return Main?.IsLineObscuredBySmoke(origin, aimPoint) != true
            && Ballistics.HasClearShot(GetWorld3D(), origin, aimPoint, target, GetRid());
    }

    internal Vector3 RawMuzzlePositionForDiagnostics => RawMuzzlePosition;
    internal Vector3 ResolvedShotOriginForDiagnostics => ResolveBallisticShotOrigin();

    private bool WithinViewCone()
    {
        if (FlashbangSuppressesVision)
        {
            return false;
        }
        if (SentryMode)
        {
            return true;
        }
        var eye = GlobalPosition + Vector3.Up * CombatEyeHeight;
        var target = CurrentTargetPoint();
        var direction = eye.DirectionTo(target);
        var threshold = Mathf.Lerp(0.42f, 0.72f, FlashbangIntensity / FlashVisionSuppressionThreshold);
        return (-GlobalBasis.Z).Dot(direction) > threshold;
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
            RememberInvestigationPoint(origin, CurrentPursuitDuration * 0.65f);
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
        _combatTargetAcquireTimer = 0.0f;
        RememberInvestigationPoint(investigatePosition, CurrentPursuitDuration * 0.7f);
        _fireTimer = _rng.RandfRange(0.45f, 0.9f);
    }

    private void Engage(float delta, float distance, bool hasSight)
    {
        if (TryHandlePendingAirborneAttackShot(distance, hasSight))
        {
            return;
        }

        if (_hitStun > 0.0f)
        {
            // Repeated hits must not stunlock a back-turned operator forever. Damage
            // memory supplies the attacker's last confirmed direction even before the
            // normal view cone can establish line of sight.
            if (HasRecentDamageThreat || hasSight)
            {
                FaceCombatContact(hasSight);
            }
            var stunnedVelocity = Velocity;
            stunnedVelocity.X = Mathf.MoveToward(stunnedVelocity.X, 0.0f, delta * 18.0f);
            stunnedVelocity.Z = Mathf.MoveToward(stunnedVelocity.Z, 0.0f, delta * 18.0f);
            Velocity = stunnedVelocity;
            // Still allow return fire while stunned at reduced cadence.
            var canReturnFireThroughSmoke = CanReturnFireAtRecentDamageThreat();
            if (_fireTimer <= 0.0f
                && distance < CurrentFireRange
                && (hasSight || canReturnFireThroughSmoke))
            {
                if (!hasSight)
                {
                    FireAtRecentDamageThreat(distance);
                }
                else if (_combatTarget is not null)
                {
                    FireAtSquad(distance);
                }
                else if (_rawTarget is EnemyOperator stunnedRival && !stunnedRival.IsDead)
                {
                    FireAtNode(stunnedRival, distance);
                }
            }
            return;
        }

        if (!hasSight)
        {
            UpdateLostContactMovement(delta);
            if (HasRecentDamageThreat)
            {
                FaceCombatContact(hasSight: false);
            }
            if (CanReturnFireAtRecentDamageThreat()
                && _fireTimer <= 0.0f
                && distance < CurrentFireRange)
            {
                FireAtRecentDamageThreat(distance);
            }
            return;
        }

        var visibleTarget = AssignedCombatTargetNode();
        if (ShouldUseVisiblePursuitNavigation(visibleTarget))
        {
            var routeSpeed = (distance > 8.0f ? 5.5f : 3.8f)
                * (IsRivalSquad ? 1.08f : 1.0f)
                * (IsWorldBoss ? WorldBossMoveMultiplier : 1.0f);
            if (UpdatePursuitNavigationMovement(
                    delta,
                    visibleTarget,
                    CurrentTargetPosition(),
                    routeSpeed,
                    requireRoute: true))
            {
                if (_fireTimer <= 0.0f && distance < CurrentFireRange)
                {
                    if (_combatTarget is not null)
                    {
                        FireAtSquad(distance);
                    }
                    else if (_rawTarget is EnemyOperator routedRival && !routedRival.IsDead)
                    {
                        FireAtNode(routedRival, distance);
                    }
                }
                return;
            }
        }

        var combatPosition = CurrentTargetPosition();
        // Cover movement can run, but never fully suppress shooting once a target is locked.
        var holdingCover = (!SentryMode || Main?.IsDemolitionMode == true)
            && UpdateCover(delta, combatPosition);
        var targetFlat = new Vector3(combatPosition.X, GlobalPosition.Y, combatPosition.Z);
        if (GlobalPosition.DistanceTo(targetFlat) > 0.1f)
        {
            LookAt(targetFlat, Vector3.Up);
        }
        var forward = -GlobalBasis.Z;
        var right = GlobalBasis.X;
        var desired = Vector3.Zero;
        if (!IsProne)
        {
            var preferredRange = IsWorldBoss ? 62.0f : UsesLongRangeRifle ? 42.0f : 19.0f;
            if (distance > preferredRange)
            {
                desired += forward;
            }
            else if (distance < preferredRange * 0.48f)
            {
                desired -= forward;
            }
            if (hasSight && distance < 32.0f)
            {
                // Strafe around cover edges instead of face-tanking.
                desired += right * _strafeSign * 0.58f;
            }
        }
        if (_repathTimer <= 0.0f)
        {
            _repathTimer = _rng.RandfRange(1.0f, 2.1f);
            if (_rng.Randf() < 0.5f)
            {
                _strafeSign *= -1.0f;
            }
            // Rival squads periodically re-path to cover while under fire.
            if (IsRivalSquad && hasSight && Main is not null && _rng.Randf() < 0.4f)
            {
                var cover = Main.FindCoverPoint(GlobalPosition, combatPosition);
                if (cover.Y > -500.0f)
                {
                    _seekingCover = true;
                    _coverTarget = cover;
                }
            }
        }

        if (!holdingCover)
        {
            var speed = SentryMode
                ? 0.0f
                : IsProne ? 1.1f : IsCrouched ? 1.85f : distance > 19.0f ? 5.2f : 2.4f;
            if (IsRivalSquad)
            {
                speed *= 1.08f;
            }
            if (IsWorldBoss)
            {
                speed *= WorldBossMoveMultiplier;
            }
            var movement = desired.LengthSquared() > 0.01f ? desired.Normalized() * speed : Vector3.Zero;
            var velocity = Velocity;
            velocity.X = Mathf.MoveToward(velocity.X, movement.X, delta * 11.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, movement.Z, delta * 11.0f);
            Velocity = velocity;
        }
        // Fire when we have a live engage target in range. Prefer LOS, but still allow
        // close-range pressure shots so squads do not soft-lock when LOS is noisy.
        var canFire = CanFireDuringFlashbang
            && distance < CurrentFireRange
            && _fireTimer <= 0.0f
            && (hasSight || distance < 18.0f);
        if (canFire)
        {
            if (_combatTarget is not null)
            {
                FireAtSquad(distance);
            }
            else if (_rawTarget is EnemyOperator rival && !rival.IsDead)
            {
                FireAtNode(rival, distance);
            }
        }
    }

    private void FaceCombatContact(bool hasSight)
    {
        var contact = hasSight
            ? CurrentTargetPosition()
            : HasFreshConfirmedCombatContact
                ? ConfirmedCombatContactPosition
                : LastKnownTargetPosition;
        var contactFlat = new Vector3(contact.X, GlobalPosition.Y, contact.Z);
        if (GlobalPosition.DistanceSquaredTo(contactFlat) > 0.01f)
        {
            LookAt(contactFlat, Vector3.Up);
        }
    }

    private bool CanReturnFireAtRecentDamageThreat()
    {
        if (!HasRecentDamageThreat || Main is null || !CanFireDuringFlashbang)
        {
            return false;
        }
        var from = GlobalPosition + Vector3.Up * CombatMuzzleHeight;
        var to = ConfirmedCombatContactPosition + Vector3.Up * 1.05f;
        return Main.IsLineObscuredBySmoke(from, to);
    }

    private bool UpdateCover(float delta, Vector3 combatPosition)
    {
        if (_seekingCover)
        {
            _ = TryStandForCombatMovement(clearCoverState: false);
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
            var targetFlat = new Vector3(combatPosition.X, GlobalPosition.Y, combatPosition.Z);
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
        if (IsWorldBoss)
        {
            UpdateWorldBossPatrol(delta);
            return;
        }
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

    /// <summary>
    /// Assign a fixed multi-district patrol loop. The enemy enters the route at the waypoint
    /// nearest its spawn so it never deadheads across the map to reach the start.
    /// </summary>
    public void AssignPatrolRoute(Vector3[] waypoints)
    {
        if (waypoints is null || waypoints.Length == 0)
        {
            return;
        }
        _patrolRoute = waypoints;
        var bestIndex = 0;
        var bestDistance = float.PositiveInfinity;
        for (var i = 0; i < waypoints.Length; i++)
        {
            var distance = GlobalPosition.DistanceSquaredTo(waypoints[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        _patrolRouteIndex = bestIndex;
        _patrolTarget = waypoints[bestIndex];
        _patrolTimer = RoutedPatrolLegSeconds(_patrolTarget);
    }

    private float RoutedPatrolLegSeconds(Vector3 waypoint)
    {
        // Timeout headroom per leg keeps routed patrols advancing even when geometry blocks a leg.
        return Mathf.Clamp(GlobalPosition.DistanceTo(waypoint) / 1.3f + 2.0f, 4.0f, 26.0f);
    }

    private void PickPatrolTarget()
    {
        if (IsWorldBoss)
        {
            SelectNextWorldBossPatrolPoint();
            return;
        }
        if (_patrolRoute.Length > 0)
        {
            _patrolRouteIndex = (_patrolRouteIndex + 1) % _patrolRoute.Length;
            _patrolTarget = _patrolRoute[_patrolRouteIndex];
            _patrolTimer = RoutedPatrolLegSeconds(_patrolTarget);
            return;
        }
        _patrolTarget = _patrolOrigin + new Vector3(
            _rng.RandfRange(-4.0f, 4.0f),
            0.0f,
            _rng.RandfRange(-4.0f, 4.0f));
        _patrolTimer = _rng.RandfRange(4.0f, 8.0f);
    }

    private void FireAtRecentDamageThreat(float distance)
    {
        var target = AssignedCombatTargetNode();
        if (!HasFireablePrimary
            || !CanFireDuringFlashbang
            || !CanReturnFireAtRecentDamageThreat()
            || target is null
            || !GodotObject.IsInstanceValid(target))
        {
            return;
        }

        BeginMuzzleFlash();
        var stats = CarriedWeapon.Stats();
        _fireTimer = _rng.RandfRange(stats.FireInterval * 3.6f, stats.FireInterval * 6.2f)
            * (IsWorldBoss ? WorldBossFireCadenceMultiplier : 1.0f)
            * FlashbangFireCadenceMultiplier;
        var accuracy = Mathf.Clamp(
            0.4f - distance * 0.007f + AccuracyBonus,
            0.14f,
            0.42f) * FlashbangAccuracyMultiplier;
        var aimPoint = ConfirmedCombatContactPosition + Vector3.Up * 1.05f;
        var shotOrigin = ResolveBallisticShotOrigin();
        if (BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            shotOrigin,
            aimPoint,
            stats.Damage * 0.3f,
            shotOrigin.DirectionTo(aimPoint),
            out var glassHitPosition))
        {
            Main?.SpawnTracer(shotOrigin, glassHitPosition, CurrentTracerColor);
            return;
        }

        var clear = Ballistics.HasClearShot(
            GetWorld3D(),
            shotOrigin,
            aimPoint,
            target,
            GetRid());
        var targetRemainsNearContact = target.GlobalPosition.DistanceSquaredTo(
            ConfirmedCombatContactPosition) <= 1.75f * 1.75f;
        if (clear && targetRemainsNearContact && _rng.Randf() < accuracy)
        {
            if (_combatTarget is { CombatDowned: false } combatTarget)
            {
                combatTarget.TakeCombatDamage(
                    stats.Damage * _rng.RandfRange(0.24f, 0.38f),
                    aimPoint,
                    this);
            }
            else if (target is EnemyOperator rival && !rival.IsDead)
            {
                rival.TakeDamage(
                    stats.Damage * _rng.RandfRange(0.22f, 0.34f),
                    aimPoint,
                    this);
            }
        }
        else if (!clear)
        {
            if (PhysicsRaycast.TryHit(
                GetWorld3D(),
                shotOrigin,
                aimPoint,
                GetRid(),
                uint.MaxValue,
                out var hit))
            {
                aimPoint = hit.Position;
            }
        }
        else
        {
            aimPoint += Scatter() * 1.4f;
        }
        Main?.SpawnTracer(shotOrigin, aimPoint, CurrentTracerColor);
    }

    private void FireAtSquad(float distance)
    {
        if (!HasFireablePrimary || !CanFireDuringFlashbang)
        {
            return;
        }
        if (_combatTarget is null || !IsAttackableCombatant(_combatTarget))
        {
            if (_rawTarget is EnemyOperator rivalFallback && !rivalFallback.IsDead)
            {
                FireAtNode(rivalFallback, distance);
            }
            return;
        }
        if (_combatTarget.CombatDowned && _downedFinishLockTimer > 0.0f)
        {
            return;
        }
        if (TryResolveOccupiedVehicleTarget(out var vehicle))
        {
            FireAtOccupiedVehicle(vehicle!, distance);
            return;
        }
        BeginMuzzleFlash();
        var stats = CarriedWeapon.Stats();
        _fireTimer = _rng.RandfRange(stats.FireInterval * 2.4f, stats.FireInterval * 4.8f)
            * (IsWorldBoss ? WorldBossFireCadenceMultiplier : 1.0f)
            * FlashbangFireCadenceMultiplier;
        var rangeFactor = Mathf.Clamp(stats.EffectiveRange / 150.0f, 0.7f, 1.25f);
        // Rivals are more accurate at medium range so multi-squad fights resolve.
        var baseAcc = IsWorldBoss ? 0.95f : IsRivalSquad ? 0.97f : 0.9f;
        // Demolition gains cover behavior, not extra aim. Preserve the ordinary
        // extraction prone bonus while keeping demolition base accuracy unchanged.
        var proneAccuracyBonus = IsProne && Main?.IsDemolitionMode != true ? 0.02f : 0.0f;
        var accuracy = Mathf.Clamp(
            baseAcc + proneAccuracyBonus - distance * 0.005f / rangeFactor + AccuracyBonus,
            0.55f,
            0.98f) * FlashbangAccuracyMultiplier;
        var regionRoll = _rng.Randf();
        var hitRegion = IsProne
            ? HitRegion.Torso
            : regionRoll < 0.12f ? HitRegion.Head : regionRoll < 0.78f ? HitRegion.Torso : HitRegion.Limbs;
        var aimPoint = _combatTarget.HitPoint(hitRegion);
        var shotOrigin = ResolveBallisticShotOrigin();
        if (BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            shotOrigin,
            aimPoint,
            stats.Damage * 0.4f,
            shotOrigin.DirectionTo(aimPoint),
            out var glassHitPosition))
        {
            Main?.SpawnTracer(shotOrigin, glassHitPosition, CurrentTracerColor);
            return;
        }
        var clear = Ballistics.HasClearShot(GetWorld3D(), shotOrigin, aimPoint, _combatTarget.CombatNode, GetRid());
        if (clear && _rng.Randf() < accuracy)
        {
            if (_combatTarget.CombatDowned)
            {
                _combatTarget.TryFinishDowned(this);
            }
            else
            {
                _combatTarget.TakeCombatDamage(stats.Damage * _rng.RandfRange(0.32f, 0.48f), aimPoint, this);
            }
        }
        else if (!clear)
        {
            // Tracer stops at the wall hit instead of ghosting through.
            if (PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    shotOrigin,
                    aimPoint,
                    GetRid(),
                    uint.MaxValue,
                    out var hit))
            {
                aimPoint = hit.Position;
            }
        }
        else
        {
            // Near-miss still close enough for tracers; keep pressure high.
            aimPoint += Scatter() * 0.35f;
        }
        Main?.SpawnTracer(shotOrigin, aimPoint, CurrentTracerColor);
    }

    private void FireAtNode(EnemyOperator rival, float distance)
    {
        if (!HasFireablePrimary || !CanFireDuringFlashbang)
        {
            return;
        }
        BeginMuzzleFlash();
        var stats = CarriedWeapon.Stats();
        _fireTimer = _rng.RandfRange(stats.FireInterval * 3.2f, stats.FireInterval * 6.8f)
            * (IsWorldBoss ? WorldBossFireCadenceMultiplier : 1.0f)
            * FlashbangFireCadenceMultiplier;
        var accuracy = Mathf.Clamp(0.9f - distance * 0.008f + AccuracyBonus, 0.4f, 0.94f)
            * FlashbangAccuracyMultiplier;
        var aimPoint = rival.GlobalPosition + Vector3.Up * rival.CombatAimHeight;
        var shotOrigin = ResolveBallisticShotOrigin();
        if (BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            shotOrigin,
            aimPoint,
            stats.Damage * 0.36f,
            shotOrigin.DirectionTo(aimPoint),
            out var glassHitPosition))
        {
            Main?.SpawnTracer(shotOrigin, glassHitPosition, CurrentTracerColor);
            return;
        }
        var clear = Ballistics.HasClearShot(GetWorld3D(), shotOrigin, aimPoint, rival, GetRid());
        if (clear && _rng.Randf() < accuracy)
        {
            rival.TakeDamage(stats.Damage * _rng.RandfRange(0.28f, 0.4f), aimPoint, this);
        }
        else if (!clear)
        {
            if (PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    shotOrigin,
                    aimPoint,
                    GetRid(),
                    uint.MaxValue,
                    out var hit))
            {
                aimPoint = hit.Position;
            }
        }
        else
        {
            aimPoint += Scatter();
        }
        Main?.SpawnTracer(shotOrigin, aimPoint, CurrentTracerColor);
    }

    private void BeginMuzzleFlash()
    {
        AttackShotsFired++;
        HoldAuthoredAimAfterShot();
        Main?.NotifyAircraftOperatorAttack(this, GlobalPosition, CarriedWeapon.Stats().SoundRadius);
        _shotAudio.PitchScale = _rng.RandfRange(0.88f, 1.08f);
        _shotAudio.Play();
        _muzzleLight.LightEnergy = 5.5f;
        _muzzleBloom.Visible = true;
        _muzzleBloom.Scale = Vector3.One * _rng.RandfRange(0.75f, 1.2f);
        var flash = CreateTween();
        flash.TweenProperty(_muzzleLight, "light_energy", 0.0f, 0.05f);
        flash.Parallel().TweenProperty(_muzzleBloom, "scale", Vector3.One * 0.1f, 0.06f);
        flash.TweenCallback(Callable.From(() => _muzzleBloom.Visible = false));
    }

    private Vector3 Scatter() => new(
        _rng.RandfRange(-1.9f, 1.9f),
        _rng.RandfRange(-1.1f, 1.4f),
        _rng.RandfRange(-1.9f, 1.9f));

    private void AnimateBody(float delta)
    {
        var speed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (UsesTideHunterMonsterForDiagnostics)
        {
            UpdateTideHunterVisual(speed);
            UpdateAuthoredStanceCollider();
            return;
        }
        if (UsesAuthoredOperatorForDiagnostics)
        {
            // The authored set has no dedicated jump clip. Holding its armed aim pose
            // in the air reads as an intentional jump shot and avoids sprinting in place.
            var locomotionSpeed = IsOnFloor() ? speed : 0.0f;
            AnimateAuthoredOperator(delta, locomotionSpeed);
            UpdateAuthoredStanceCollider();
            return;
        }
        if (!IsOnFloor())
        {
            speed = 0.0f;
        }
        _animationPhase += delta * (4.0f + speed * 1.7f);
        var coverOffset = IsCrouched ? -0.38f : 0.0f;
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

    public bool TakeDamage(
        float amount,
        Vector3 hitPosition,
        Node? attacker = null,
        float armorPenetration = 0.0f)
    {
        if (IsDead)
        {
            return true;
        }
        if (IsNetworkProxy
            && (Main?.IsExtractionNetworkClient == true
                || Main?.IsDemolitionNetworkClient == true))
        {
            var proxyHitHeight = hitPosition.Y - GlobalPosition.Y;
            var proxyRegion = ResolveIncomingHitRegion(proxyHitHeight);
            LastHitWasHeadshot = proxyRegion == HitRegion.Head;
            LastHitWasArmored = proxyRegion is HitRegion.Head or HitRegion.Torso;
            return false;
        }
        Alerted = true;
        LastDamageAttacker = attacker;
        if (attacker is TacticalPlayer tacticalPlayer)
        {
            Player = tacticalPlayer;
        }
        RegisterDamageThreat(attacker);
        RegisterCombatPressure();
        var localHeight = hitPosition.Y - GlobalPosition.Y;
        var region = ResolveIncomingHitRegion(localHeight);
        LastHitWasHeadshot = region == HitRegion.Head;
        var adjustedDamage = region switch
        {
            HitRegion.Head => amount * 2.3f,
            HitRegion.Limbs => amount * 0.72f,
            _ => amount
        };
        var protectiveGear = region switch
        {
            HitRegion.Head => EquippedHelmet,
            HitRegion.Torso => EquippedBodyArmor,
            _ => null
        };
        LastHitWasArmored = protectiveGear is not null && protectiveGear.Durability > 0.0f;
        if (protectiveGear is not null)
        {
            adjustedDamage = ApplyProtection(protectiveGear, adjustedDamage, armorPenetration);
        }
        _health -= adjustedDamage;
        if (_health > 0.0f && UsesAuthoredOperatorForDiagnostics && !UsesTideHunterMonsterForDiagnostics)
        {
            _authoredOperatorAnimator.PlayHit();
        }
        OnWorldBossDamaged();
        _hitStun = 0.14f;
        var original = _mainMaterial.AlbedoColor;
        _mainMaterial.AlbedoColor = new Color(0.62f, 0.12f, 0.07f);
        CreateTween().TweenProperty(_mainMaterial, "albedo_color", original, 0.11f);

        var shouldSeekCover = Main?.IsDemolitionMode == true
            ? _health < 62.0f
            : _health < 76.0f || _rng.Randf() < 0.4f;
        if (_health > 0.0f
            && !SentryMode
            && !_seekingCover
            && !_inCover
            && Main is not null
            && shouldSeekCover)
        {
            var threatPosition = CurrentThreatPosition(hasSight: false);
            var candidate = Main.FindCoverPoint(GlobalPosition, threatPosition);
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

    private static float ApplyProtection(EquipmentItem equipment, float damage, float armorPenetration)
    {
        if (equipment.Durability <= 0.0f || equipment.Definition.Protection <= 0.0f)
        {
            return damage;
        }
        var durabilityRatio = equipment.Durability / equipment.Definition.MaxDurability;
        var effectiveProtection = equipment.Definition.Protection
            * Mathf.Lerp(0.55f, 1.0f, durabilityRatio)
            * (1.0f - Mathf.Clamp(armorPenetration, 0.0f, 0.72f));
        equipment.Durability = Mathf.Max(0.0f, equipment.Durability - damage * 0.58f);
        return damage * (1.0f - effectiveProtection);
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }
        IsDead = true;
        SetProne(false);
        SetCombatCrouched(false);
        ShowCorpseLootBackpack();
        ClearPursuitMemory(clearTarget: true);
        ResetIncendiaryAvoidance();
        CollisionLayer = 0;
        CollisionMask = 0;
        Velocity = Vector3.Zero;
        EmitSignal(SignalName.Eliminated, this);
        // A training-range listener may have converted this elimination into a
        // short reusable downed pose.  Do not start the mission death animation
        // after the listener returns; it would overwrite the downed clip and its
        // tween would keep the operator tilted until another hit wakes it up.
        if (_suppressDeathAnimationForTrainingRange)
        {
            _deathTween = null;
            return;
        }
        if (UsesTideHunterMonsterForDiagnostics)
        {
            if (IsInstanceValid(_worldBossChargeRing))
            {
                _worldBossChargeRing!.Visible = false;
            }
            if (IsInstanceValid(_worldBossLabel))
            {
                _worldBossLabel!.Visible = false;
            }
            _deathTween = _tideHunterMonsterVisual!.BeginDeath(_rng.Randf() < 0.5f);
            _deathTween.Finished += () =>
            {
                if (IsDead)
                {
                    SetPhysicsProcess(false);
                }
                _deathTween = null;
            };
            return;
        }
        if (UsesAuthoredOperatorForDiagnostics)
        {
            // The normal animation loop will not run again once IsDead is set,
            // so stow the weapon before starting the terminal clip.  Leaving
            // the visual in its readied state also leaves the last arm IK pose
            // attached to the death animation.
            _authoredOperatorVisual.SetWeaponReadied(false);
            _authoredOperatorAnimator.Update(
                0.0f,
                0.0f,
                weaponReadied: false,
                prone: false,
                crouched: false,
                aiming: false,
                downed: false,
                reviving: false,
                dead: true);
            _deathTween = CreateTween();
            _deathTween.TweenInterval(1.9f);
            _deathTween.Finished += () =>
            {
                if (IsDead)
                {
                    SetPhysicsProcess(false);
                }
                _deathTween = null;
            };
            return;
        }
        _deathTween = CreateTween().SetParallel(true);
        _deathTween.TweenProperty(_bodyRoot, "rotation:z", _rng.Randf() < 0.5f ? -1.38f : 1.38f, 0.52f)
            .SetTrans(Tween.TransitionType.Quad);
        _deathTween.TweenProperty(_bodyRoot, "position:y", 0.18f, 0.52f);
        _deathTween.Finished += () =>
        {
            if (IsDead)
            {
                SetPhysicsProcess(false);
            }
            _deathTween = null;
        };
    }
}
