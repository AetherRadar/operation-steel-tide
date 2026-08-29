using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

// File-size retention: TacticalPlayer remains the legacy gameplay compatibility
// facade, and this change only reconnects its existing weapon nodes to authored
// visuals. Follow-up: extract first-person weapon construction and optic setup to
// TacticalPlayer.WeaponVisualSetup.cs, keeping lifecycle ownership here and
// protecting the move with hand, optics, reload, equipment, and HUD diagnostics.

public enum PlayerStance
{
    Standing,
    Crouched,
    Prone
}

public enum HitRegion
{
    Head,
    Torso,
    Limbs
}

[GlobalClass]
public partial class TacticalPlayer : CharacterBody3D, ISquadCombatant
{
    [Signal]
    public delegate void DiedEventHandler();

    [Signal]
    public delegate void HitConfirmedEventHandler(bool killed, bool headshot, bool armorHit);

    private const float WalkSpeed = 5.6f;
    private const float SprintSpeed = 8.8f;
    private const float CrouchSpeed = 3.1f;
    private const float ProneSpeed = 1.65f;
    private const float Gravity = 22.0f;
    private const float ReloadDuration = 2.45f;
    private const float SprintRecoveryThreshold = 28.0f;
    private const float SprintRecoveryDelay = 0.8f;
    private const int MaxArmorPlates = 3;

    public float Health { get; private set; } = 100.0f;
    public EquipmentItem EquippedHelmet { get; private set; } = EquipmentCatalog.Create("helmet_light");
    public EquipmentItem EquippedBodyArmor { get; private set; } = EquipmentCatalog.Create("armor_carrier");
    public EquipmentItem EquippedBackpack { get; private set; } = EquipmentCatalog.Create("pack_assault");
    public LootGrade EquippedHelmetGrade { get; private set; } = LootGrade.Uncommon;
    public LootGrade EquippedBodyArmorGrade { get; private set; } = LootGrade.Rare;
    public LootGrade EquippedBackpackGrade { get; private set; } = LootGrade.Uncommon;
    public float Armor => EquippedBodyArmor.Definition.MaxDurability <= 0.0f
        ? 0.0f
        : EquippedBodyArmor.Durability / EquippedBodyArmor.Definition.MaxDurability * 100.0f;
    public float Stamina { get; private set; } = 100.0f;
    public int Ammo { get; private set; } = 30;
    public int ReserveAmmo => AmmoReserveFor(CurrentAmmoCaliber);
    public int TotalReserveAmmo
    {
        get
        {
            var total = 0;
            foreach (var amount in _gradedAmmoReserves.Values)
            {
                total += amount;
            }
            return total;
        }
    }
    public int Grenades { get; private set; } = 2;
    public int ArmorPlates
        => CaptureFieldSupplySnapshot().ArmorPlates;
    public bool IsDead { get; set; }
    public bool LastHitWasArmored { get; private set; }
    public bool HasMovementIntent { get; private set; }
    public bool IsAiming => _isAiming;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading ? 1.0f - _reloadTime / _activeReloadDuration : 0.0f;
    public bool FlashlightOn => _flashlightOn;
    public bool HasNightVisionHelmet => EquippedHelmet.DefinitionId == "helmet_nvg";
    public bool NightVisionOn => _nvgOn && HasNightVisionHelmet;
    public string FireMode => _automaticFire ? "AUTO" : "SEMI";
    public WeaponBuild EquippedWeapon { get; private set; } = WeaponCatalog.StarterWeapon();
    public LootGrade EquippedWeaponGrade { get; private set; } = LootGrade.Rare;
    public List<LootItem> Backpack { get; } = new();
    public int BackpackCapacity => 6
        + EquippedBackpack.Definition.CapacityBonus
        + OperatorRoles.Spec(Role).BackpackCapacityBonus;
    public PlayerStance Stance => _stance;
    public bool IsCrouched => _stance == PlayerStance.Crouched;
    public bool IsProne => _stance == PlayerStance.Prone;
    public float LeanAmount => _leanValue;
    public float ViewHeight => IsInstanceValid(_head) ? _head.Position.Y : 0.0f;
    public bool KnifeEquipped => _knifeEquipped;
    public string EquippedKnifeSkinId { get; private set; } = KnifeSkinCatalog.DefaultId;
    public LootGrade EquippedKnifeGrade { get; private set; } = LootGrade.Uncommon;
    public AmmoCaliber CurrentAmmoCaliber => WeaponCatalog.Weapon(EquippedWeapon.Platform).Caliber;
    /// <summary>False at cold-start extraction until a looted primary is equipped.</summary>
    public bool HasFireablePrimary { get; private set; } = true;
    public bool HasActiveFirearm
        => IsFirearmQuickSlotSelected
        && !_knifeEquipped
        && _activeWeaponSlot switch
        {
            PlayerWeaponSlot.Primary => HasFireablePrimary && _primaryWeaponSlot is not null,
            PlayerWeaponSlot.Secondary => _secondaryWeaponSlot is not null,
            PlayerWeaponSlot.Sidearm => _sidearmWeaponSlot is not null,
            _ => false
        };
    private bool _uiLocked;
    public bool UiLocked
    {
        get => _uiLocked;
        set
        {
            if (_uiLocked == value)
            {
                return;
            }
            _uiLocked = value;
            if (value)
            {
                // Any modal UI stops first-person actions instead of allowing
                // them to consume supplies or freeze a mechanism off-screen.
                CancelFieldUse(false);
                CancelReload();
                return;
            }
            // Restore the selected held item immediately when the modal closes;
            // waiting for a later physics tick made this state frame-rate
            // dependent in both gameplay and deterministic diagnostics.
            UpdateHeldItemVisibility();
        }
    }
    public bool SprintRecoveryRequired => _sprintRecoveryRequired || _sprintRecoveryDelay > 0.0f;
    public bool IsInVehicle => _vehicle is not null && GodotObject.IsInstanceValid(_vehicle);
    public DriveableVehicle? CurrentVehicle => IsInVehicle ? _vehicle : null;
    public WeaponStats CurrentWeaponStats => EquippedWeapon.Stats();
    public float MouseSensitivity { get; set; } = 0.00165f;
    public FreightTerminalWorld? Main { get; set; }
    public CombatHUD? Hud { get; set; }

    /// <summary>Extraction cold-start: knife only, no magazine — must loot a primary.</summary>
    public void ApplyColdStartUnarmed() => ApplyColdStartUnarmed(true);

    private void ApplyColdStartUnarmed(bool includeEmergencySupplies)
    {
        ClearWeaponSlotsForColdStart();
        Ammo = 0;
        ResetAmmoReserves();
        if (includeEmergencySupplies)
        {
            EnsureEmergencyMedicalLoadout();
            EnsureEmergencyArmorLoadout();
        }
        SwitchWeapon(true);
        if (IsInstanceValid(_weaponRoot))
        {
            _weaponRoot.Visible = false;
        }
    }

    /// <summary>Diagnostics: force a fireable primary for headless combat tests.</summary>
    public void GrantFireablePrimaryForDiagnostics(WeaponBuild? build = null)
    {
        HasFireablePrimary = true;
        EquipPrimary(build ?? WeaponCatalog.StarterWeapon());
        Ammo = EquippedWeapon.Stats().MagazineSize;
        SetAmmoReserve(CurrentAmmoCaliber, Mathf.Max(ReserveAmmo, 60));
        _loadedAmmoGrade = LootGrade.Common;
        _fireCooldown = 0.0f;
        _isPlating = false;
        _knifeEquipped = false;
    }

    public bool IsGlassBreakAudioPlaying => IsInstanceValid(_glassBreakAudio) && _glassBreakAudio.Playing;

    public bool FireForDiagnostics()
    {
        var ammoBefore = Ammo;
        Fire();
        return Ammo < ammoBefore;
    }

    public Vector3 DiagnosticCameraPosition => IsInstanceValid(_camera) ? _camera.GlobalPosition : GlobalPosition;
    public Vector3 DiagnosticCameraForward => IsInstanceValid(_camera) ? -_camera.GlobalBasis.Z : -GlobalBasis.Z;

    public bool HasGlassInCrosshairForDiagnostics(float range = 12.0f)
    {
        var from = DiagnosticCameraPosition;
        return PhysicsRaycast.HasHit(
            GetWorld3D(),
            from,
            from + DiagnosticCameraForward * range,
            BreakableGlassField.GlassCollisionLayer,
            collideWithAreas: true,
            collideWithBodies: false);
    }

    private bool _isReloading;
    private bool _isAiming;
    private bool _isPlating;
    private bool _automaticFire = true;
    private bool _flashlightOn;
    private bool _nvgOn;
    private bool _fireInputArmed;
    private bool _movementInputArmed;
    private readonly Dictionary<AttachmentSlot, LootGrade> _equippedAttachmentGrades = new();
    private DriveableVehicle? _vehicle;
    private bool _vehicleCameraFollow;
    private float _fireReleaseTime;
    private float _movementReleaseTime;
    private float _fireCooldown;
    private float _reloadTime;
    private int _reloadSoundStage;
    private float _plateTime;
    private float _plateDuration;
    private float _plateRepairFraction;
    private string _plateItemId = string.Empty;
    private bool _sprintRecoveryRequired;
    private float _sprintRecoveryDelay;
    private float _pitch;
    private float _recoilPitch;
    private float _recoilSide;
    private float _bobTime;
    private Vector3 _smoothedBobOffset;
    private float _stairViewOffsetY;
    private float _footstepTimer;
    private float _slideTime;
    private Vector3 _slideDirection;
    private float _leanValue;
    private float _searchPose;
    private PlayerStance _stance = PlayerStance.Standing;

    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private Node3D _weaponRoot = null!;
    private Marker3D _muzzle = null!;
    private OmniLight3D _muzzleFlash = null!;
    private MeshInstance3D _muzzleBloom = null!;
    private MeshInstance3D _opticReticle = null!;
    private SpotLight3D _weaponLight = null!;
    private MeshInstance3D _magazine = null!;
    private MeshInstance3D _spareMagazine = null!;
    private Node3D _supportHand = null!;
    private Node3D _supportForearm = null!;
    private Node3D _primaryHand = null!;
    private Node3D _primaryForearm = null!;
    private MeshInstance3D _receiver = null!;
    private MeshInstance3D _handguard = null!;
    private MeshInstance3D _barrelPart = null!;
    private MeshInstance3D _muzzlePart = null!;
    private MeshInstance3D _foregrip = null!;
    private MeshInstance3D _stock = null!;
    private MeshInstance3D _receiverSeam = null!;
    private MeshInstance3D _ejectionPort = null!;
    private MeshInstance3D _boltCarrier = null!;
    private MeshInstance3D _triggerGuardFront = null!;
    private MeshInstance3D _triggerGuardRear = null!;
    private MeshInstance3D _triggerGuardBase = null!;
    private MeshInstance3D _trigger = null!;
    private MeshInstance3D _selector = null!;
    private Node3D _opticRoot = null!;
    private Node3D _reflexSightModel = null!;
    private Node3D _holoSightModel = null!;
    private Node3D _scopeSightModel = null!;
    private MeshInstance3D _chargingHandle = null!;
    private Marker3D _ejectMarker = null!;
    private AudioStreamPlayer _gunAudio = null!;
    private AudioStreamPlayer _reloadAudio = null!;
    private AudioStreamPlayer _glassBreakAudio = null!;
    private AudioStreamPlayer3D _footstepAudio = null!;
    private CollisionShape3D _collider = null!;
    private readonly RandomNumberGenerator _rng = new();
    public override void _Ready()
    {
        _rng.Randomize();
        CollisionLayer = 1;
        CollisionMask = 1 | 2;
        // Thin stair treads (~0.13 m rise); generous snap helps the capsule mount each step.
        FloorSnapLength = 0.95f;
        FloorMaxAngle = Mathf.DegToRad(64.0f);
        FloorConstantSpeed = true;
        FloorStopOnSlope = false;
        SafeMargin = 0.05f;
        BuildBody();
        BuildWeapon();
        BuildKnife();
        BuildHeldThrowables();
        BuildLadderViewModel();
        BuildRoleDevices();
        BuildMedicalDevices();
        ApplyWeaponBuildVisuals();
        ConfigureRole(Role);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        DisarmFireInput();
        DisarmMovementInput();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn)
        {
            DisarmFireInput();
            DisarmMovementInput();
        }
    }

    public void DisarmFireInput()
    {
        _fireInputArmed = false;
        _fireReleaseTime = 0.0f;
    }

    public void DisarmMovementInput()
    {
        _movementInputArmed = false;
        _movementReleaseTime = 0.0f;
        HasMovementIntent = false;
    }

    public void RestoreMovementInput()
    {
        _movementInputArmed = true;
        _movementReleaseTime = 0.0f;
        HasMovementIntent = false;
    }

    /// <summary>Diagnostics: yaw the body toward a world point so move_forward walks that way.</summary>
    public void FaceWorldPointForDiagnostics(Vector3 worldPoint)
    {
        var flat = worldPoint - GlobalPosition;
        flat.Y = 0.0f;
        if (flat.LengthSquared() < 0.0001f)
        {
            return;
        }
        var yaw = Mathf.Atan2(-flat.X, -flat.Z);
        Rotation = new Vector3(0.0f, yaw, 0.0f);
        _pitch = 0.0f;
        if (IsInstanceValid(_head))
        {
            _head.Rotation = Vector3.Zero;
        }
    }

    public void AimCameraAtWorldPointForDiagnostics(Vector3 worldPoint)
    {
        if (IsInstanceValid(_camera) && _camera.GlobalPosition.DistanceSquaredTo(worldPoint) > 0.0001f)
        {
            _camera.LookAt(worldPoint, Vector3.Up);
        }
    }

    public void SetViewPitchForDiagnostics(float pitch)
    {
        _pitch = Mathf.Clamp(pitch, -1.38f, 1.38f);
        _recoilPitch = 0.0f;
        _damageKickPitch = 0.0f;
    }

    public void EnterVehicle(DriveableVehicle vehicle, Node3D seat)
    {
        if (IsDead || vehicle.IsDestroyed)
        {
            return;
        }

        if (_isVaulting)
        {
            CancelLowObstacleVault("vehicle_mount");
        }

        _vehicle = vehicle;
        CloseMedicalWheelWithoutUse();
        CancelFieldUse(false);
        CancelReload();
        UpdateHeldThrowableVisual();
        _vehicleCameraFollow = true;
        Velocity = Vector3.Zero;
        _isAiming = false;
        _slideTime = 0.0f;
        _stance = PlayerStance.Standing;
        CollisionLayer = 0;
        CollisionMask = 0;
        _collider.Disabled = true;
        GlobalPosition = seat.GlobalPosition;
        Reparent(seat, keepGlobalTransform: false);
        Position = Vector3.Zero;
        Rotation = Vector3.Zero;
        // Eye height in the cab (seat local space); look slightly down at the dash / road.
        _head.Position = new Vector3(0.0f, 0.52f, 0.05f);
        _pitch = Mathf.Clamp(_pitch * 0.35f, -0.28f, 0.18f);
        _head.Rotation = new Vector3(_pitch, 0.0f, 0.0f);
        if (IsInstanceValid(_weaponRoot))
        {
            // Firearm stays in hand across the mount so the cab gunner can shoot.
            _weaponRoot.Visible = IsFirearmQuickSlotSelected;
        }
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.Visible = false;
        }
        Hud?.ShowLocalizedMessage("vehicle_entered", "VEHICLE  //  ENGAGED", new Color(0.55f, 0.92f, 0.68f));
    }

    public void ExitVehicle(Vector3 worldExitPoint, bool forced = false)
    {
        if (_vehicle is null)
        {
            return;
        }

        var world = Main;
        _vehicle = null;
        _vehicleCameraFollow = false;
        if (GetParent() is not FreightTerminalWorld && world is not null)
        {
            Reparent(world, keepGlobalTransform: true);
        }
        else if (world is not null && GetParent() != world)
        {
            Reparent(world, keepGlobalTransform: true);
        }

        GlobalPosition = worldExitPoint;
        CollisionLayer = 1;
        CollisionMask = 1 | 2;
        _collider.Disabled = false;
        Velocity = Vector3.Zero;
        _head.Position = new Vector3(0.0f, 1.57f, 0.0f);
        if (IsInstanceValid(_weaponRoot))
        {
            _weaponRoot.Visible = IsFirearmQuickSlotSelected;
        }
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.Visible = _activeQuickSlot == PlayerQuickSlot.Melee;
        }
        UpdateHeldThrowableVisual();
        if (!IsDead)
        {
            RestoreMovementInput();
        }
        if (!forced && !IsDead)
        {
            Hud?.ShowLocalizedMessage("vehicle_exited", "VEHICLE  //  DISMOUNTED", new Color(0.7f, 0.82f, 0.78f));
        }
    }

    /// <summary>Force leave any vehicle (death, mission end, destroy).</summary>
    public void EjectFromVehicleIfAny()
    {
        if (!IsInVehicle || _vehicle is null)
        {
            return;
        }

        _vehicle.ExitDriver(forced: true);
    }

    public void SetSearchPose(bool active, float progress = 0.0f)
    {
        _searchPose = active ? Mathf.Clamp(progress, 0.08f, 1.0f) : 0.0f;
        if (active)
        {
            _isAiming = false;
        }
    }

    private void BuildBody()
    {
        _collider = new CollisionShape3D
        {
            Position = new Vector3(0.0f, 0.88f, 0.0f),
            Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f }
        };
        AddChild(_collider);

        _head = new Node3D { Name = "Head", Position = new Vector3(0.0f, 1.57f, 0.0f) };
        AddChild(_head);
        _camera = new Camera3D
        {
            Name = "CombatCamera",
            Current = true,
            Fov = 76.0f,
            Near = 0.04f
        };
        _head.AddChild(_camera);
    }

    private static StandardMaterial3D Material(Color color, float metallic = 0.0f, float roughness = 0.65f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Metallic = metallic,
            Roughness = roughness
        };
    }

    private static StandardMaterial3D GloveFabric(Color color)
    {
        var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var strand = ((x / 2 + y / 2) & 1) == 0 ? 0.88f : 1.0f;
                var rib = (x % 4 == 0 || y % 4 == 0) ? 0.92f : 1.0f;
                var value = strand * rib;
                image.SetPixel(x, y, new Color(value, value, value, 1.0f));
            }
        }
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            AlbedoTexture = ImageTexture.CreateFromImage(image),
            Roughness = 0.96f,
            Uv1Scale = new Vector3(5.0f, 5.0f, 5.0f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    private static BoxMesh Box(Vector3 size) => new() { Size = size };

    private static CylinderMesh Cylinder(float radius, float height) => new()
    {
        TopRadius = radius,
        BottomRadius = radius,
        Height = height,
        RadialSegments = 12
    };

    private static CylinderMesh OpenCylinder(float radius, float height) => new()
    {
        TopRadius = radius,
        BottomRadius = radius,
        Height = height,
        RadialSegments = 24,
        CapTop = false,
        CapBottom = false
    };

    private static CapsuleMesh Capsule(float radius, float height) => new()
    {
        Radius = radius,
        Height = height,
        RadialSegments = 16,
        Rings = 8
    };

    private static MeshInstance3D MeshPart(
        Node3D parent,
        Mesh mesh,
        Vector3 position,
        Vector3 rotation,
        Godot.Material material)
    {
        var part = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Rotation = rotation,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(part);
        return part;
    }

    private void BuildWeapon()
    {
        _weaponRoot = new Node3D
        {
            Name = "M4A1",
            Position = new Vector3(0.34f, -0.33f, -0.58f),
            Scale = Vector3.One * 0.68f
        };
        _camera.AddChild(_weaponRoot);
        _proceduralWeaponVisual = new Node3D { Name = "ProceduralWeaponVisual" };
        _weaponRoot.AddChild(_proceduralWeaponVisual);
        _platformSignatureRoot = new Node3D { Name = "PlatformSignatureVisual" };
        _proceduralWeaponVisual.AddChild(_platformSignatureRoot);

        var black = TacticalSurfaceLibrary.WeaponFinish(new Color(0.075f, 0.083f, 0.079f), 0.64f, 0.38f);
        var polymer = TacticalSurfaceLibrary.WeaponFinish(new Color(0.055f, 0.065f, 0.061f), 0.18f, 0.62f, 5.5f);
        var steel = TacticalSurfaceLibrary.WeaponFinish(new Color(0.09f, 0.105f, 0.1f), 0.92f, 0.2f, 3.5f);
        var tan = TacticalSurfaceLibrary.WeaponFinish(new Color(0.27f, 0.245f, 0.19f), 0.05f, 0.72f, 5.5f);
        var glass = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(0.08f, 0.48f, 0.52f, 0.2f),
            Metallic = 0.32f,
            Roughness = 0.08f,
            EmissionEnabled = true,
            Emission = new Color(0.02f, 0.22f, 0.24f),
            EmissionEnergyMultiplier = 0.38f
        };

        _receiver = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.13f, 0.15f, 0.46f)), Vector3.Zero, Vector3.Zero, black);
        BuildWeaponMechanismDetails(black, steel);
        _handguard = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.15f, 0.12f, 0.4f)), new Vector3(0, 0.01f, -0.41f), Vector3.Zero, tan);
        _barrelPart = MeshPart(_proceduralWeaponVisual, Cylinder(0.031f, 0.53f), new Vector3(0, 0.015f, -0.83f), new Vector3(Mathf.Pi / 2, 0, 0), steel);
        _muzzlePart = MeshPart(_proceduralWeaponVisual, Cylinder(0.047f, 0.14f), new Vector3(0, 0.015f, -1.15f), new Vector3(Mathf.Pi / 2, 0, 0), black);
        _stock = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.14f, 0.13f, 0.38f)), new Vector3(0, -0.01f, 0.38f), Vector3.Zero, polymer);
        MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.12f, 0.3f, 0.13f)), new Vector3(0, -0.2f, -0.04f), new Vector3(0.2f, 0, 0), polymer);
        _magazine = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.09f, 0.26f, 0.14f)), new Vector3(0, -0.2f, -0.31f), new Vector3(-0.19f, 0, 0), black);
        MeshPart(_magazine, Box(new Vector3(0.095f, 0.028f, 0.15f)), new Vector3(0, -0.11f, 0), Vector3.Zero, steel);
        AddMagazineDetail(_magazine, steel);
        MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.14f, 0.045f, 0.64f)), new Vector3(0, 0.11f, -0.34f), Vector3.Zero, steel);
        BuildReflexSight(black, glass);
        _foregrip = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.08f, 0.18f, 0.16f)), new Vector3(0, -0.17f, -0.58f), Vector3.Zero, polymer);

        var glove = GloveFabric(new Color(0.12f, 0.135f, 0.112f));
        var gloveArmor = Material(new Color(0.022f, 0.03f, 0.028f), 0.12f, 0.76f);
        _proceduralFirstPersonArms = new Node3D { Name = "ProceduralFirstPersonArms" };
        _weaponRoot.AddChild(_proceduralFirstPersonArms);
        _supportHand = BuildTacticalHand(_proceduralFirstPersonArms, true, new Vector3(-0.03f, -0.2f, -0.58f), new Vector3(0.2f, 0, 0.05f), glove, gloveArmor);
        _supportForearm = BuildSleevedForearm(_proceduralFirstPersonArms, new Vector3(-0.12f, -0.42f, -0.47f), new Vector3(0.25f, 0, -0.26f), glove, gloveArmor);
        _primaryHand = BuildTacticalHand(_proceduralFirstPersonArms, false, new Vector3(0.115f, -0.2f, -0.075f), new Vector3(-0.12f, 0.05f, -0.18f), glove, gloveArmor);
        _primaryForearm = BuildSleevedForearm(_proceduralFirstPersonArms, new Vector3(0.19f, -0.42f, 0.015f), new Vector3(-0.18f, 0.05f, -0.3f), glove, gloveArmor);
        _spareMagazine = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.09f, 0.26f, 0.14f)), new Vector3(-0.3f, -0.62f, -0.18f), new Vector3(0.35f, 0, 0.35f), black);
        MeshPart(_spareMagazine, Box(new Vector3(0.095f, 0.028f, 0.15f)), new Vector3(0, -0.11f, 0), Vector3.Zero, steel);
        AddMagazineDetail(_spareMagazine, steel);
        _spareMagazine.Visible = false;
        _chargingHandle = MeshPart(_proceduralWeaponVisual, Box(new Vector3(0.055f, 0.04f, 0.12f)), new Vector3(0.075f, 0.085f, -0.05f), Vector3.Zero, steel);

        _muzzle = new Marker3D { Position = new Vector3(0, 0.015f, -1.24f) };
        _weaponRoot.AddChild(_muzzle);
        _muzzleFlash = new OmniLight3D
        {
            LightColor = new Color(1.0f, 0.49f, 0.18f),
            LightEnergy = 0.0f,
            OmniRange = 5.0f,
            ShadowEnabled = false
        };
        _muzzle.AddChild(_muzzleFlash);

        _muzzleBloom = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.075f, Height = 0.34f, RadialSegments = 8, Rings = 4 },
            Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            Position = new Vector3(0, 0, -0.12f),
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1.0f, 0.31f, 0.035f, 0.92f),
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.12f, 0.01f),
                EmissionEnergyMultiplier = 8.0f
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };
        _muzzle.AddChild(_muzzleBloom);

        _weaponLight = new SpotLight3D
        {
            Name = "WeaponLight",
            Position = new Vector3(0.09f, -0.015f, -0.83f),
            LightColor = new Color(0.88f, 0.94f, 1.0f),
            LightEnergy = 5.2f,
            SpotRange = 34.0f,
            SpotAngle = 27.0f,
            ShadowEnabled = true,
            Visible = false
        };
        _weaponRoot.AddChild(_weaponLight);
        MeshPart(_proceduralWeaponVisual, Cylinder(0.036f, 0.18f), new Vector3(0.09f, -0.015f, -0.73f), new Vector3(Mathf.Pi / 2, 0, 0), black);

        _ejectMarker = new Marker3D { Position = new Vector3(0.13f, 0.08f, -0.12f) };
        _weaponRoot.AddChild(_ejectMarker);
        _camera.AddChild(new OmniLight3D
        {
            Position = new Vector3(-0.2f, 0.35f, 0.3f),
            LightColor = new Color(0.66f, 0.78f, 0.83f),
            LightEnergy = 0.72f,
            OmniRange = 3.0f,
            ShadowEnabled = false
        });

        _gunAudioVoices.Clear();
        for (var voiceIndex = 0; voiceIndex < LocalWeaponReportVoiceCount; voiceIndex++)
        {
            var voice = new AudioStreamPlayer
            {
                Name = $"LocalWeaponReportAudio{voiceIndex + 1}",
                Stream = SoundLab.PlayerWeaponShot(EquippedWeapon),
                VolumeDb = SoundLab.PlayerWeaponShotVolumeDb(EquippedWeapon)
            };
            _camera.AddChild(voice);
            _gunAudioVoices.Add(voice);
        }
        _gunAudio = _gunAudioVoices[0];
        _reloadAudio = new AudioStreamPlayer { Stream = SoundLab.ReloadClick(), VolumeDb = -8.0f };
        AddChild(_reloadAudio);
        _glassBreakAudio = new AudioStreamPlayer
        {
            Name = "PlayerGlassBreakAudio",
            Stream = SoundLab.GlassBreak(),
            VolumeDb = 3.5f
        };
        AddChild(_glassBreakAudio);
        _footstepAudio = new AudioStreamPlayer3D
        {
            Stream = SoundLab.Footstep(),
            VolumeDb = -15.0f,
            MaxDistance = 16.0f
        };
        AddChild(_footstepAudio);
        BuildAuthoredPrimaryWeapon();
    }

    private static void AddMagazineDetail(Node3D magazine, Godot.Material material)
    {
        for (var index = -1; index <= 1; index++)
        {
            MeshPart(
                magazine,
                Box(new Vector3(0.006f, 0.19f, 0.148f)),
                new Vector3(index * 0.026f, 0.005f, 0),
                Vector3.Zero,
                material);
        }
    }

    private void BuildWeaponMechanismDetails(Godot.Material dark, Godot.Material steel)
    {
        _receiverSeam = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.136f, 0.018f, 0.36f)),
            new Vector3(0.0f, -0.076f, -0.015f),
            Vector3.Zero,
            dark);
        _receiverSeam.Name = "LowerReceiverSeam";

        _ejectionPort = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.014f, 0.056f, 0.15f)),
            new Vector3(0.078f, 0.018f, -0.045f),
            Vector3.Zero,
            dark);
        _ejectionPort.Name = "EjectionPort";
        _boltCarrier = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.018f, 0.029f, 0.11f)),
            new Vector3(0.086f, 0.055f, -0.05f),
            Vector3.Zero,
            steel);
        _boltCarrier.Name = "BoltCarrierDetail";

        _triggerGuardFront = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.025f, 0.13f, 0.028f)),
            new Vector3(0.0f, -0.105f, 0.145f),
            new Vector3(0.0f, 0.0f, 0.12f),
            dark);
        _triggerGuardRear = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.025f, 0.13f, 0.028f)),
            new Vector3(0.0f, -0.105f, 0.005f),
            new Vector3(0.0f, 0.0f, -0.12f),
            dark);
        _triggerGuardBase = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.025f, 0.028f, 0.18f)),
            new Vector3(0.0f, -0.166f, 0.075f),
            Vector3.Zero,
            dark);
        _trigger = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.022f, 0.064f, 0.018f)),
            new Vector3(0.0f, -0.088f, 0.078f),
            new Vector3(0.0f, 0.0f, 0.18f),
            steel);
        _selector = MeshPart(
            _proceduralWeaponVisual,
            Box(new Vector3(0.018f, 0.068f, 0.026f)),
            new Vector3(0.09f, 0.008f, 0.085f),
            new Vector3(0.0f, 0.0f, -0.34f),
            steel);
        _triggerGuardFront.Name = "TriggerGuardFront";
        _triggerGuardRear.Name = "TriggerGuardRear";
        _triggerGuardBase.Name = "TriggerGuardBase";
        _trigger.Name = "Trigger";
        _selector.Name = "FireSelector";
    }

    private static Node3D BuildTacticalHand(
        Node3D parent,
        bool left,
        Vector3 position,
        Vector3 rotation,
        Godot.Material fabric,
        Godot.Material armor)
    {
        var hand = new Node3D
        {
            Name = left ? "LeftTacticalHand" : "RightTacticalHand",
            Position = position,
            Rotation = rotation
        };
        parent.AddChild(hand);
        MeshPart(hand, FirstPersonMeshFactory.Palm(left), Vector3.Zero, Vector3.Zero, fabric).Name = "GlovePalm";
        MeshPart(
            hand,
            FirstPersonMeshFactory.BackPlate(0.132f, 0.088f, 0.024f),
            new Vector3(0, 0.046f, -0.012f),
            Vector3.Zero,
            armor).Name = "KnuckleShield";

        var fingerLengths = new[] { 0.127f, 0.154f, 0.146f, 0.118f };
        var fingerWidths = new[] { 0.033f, 0.036f, 0.035f, 0.031f };
        for (var index = 0; index < fingerLengths.Length; index++)
        {
            var finger = new Node3D
            {
                Name = $"Finger{index + 1}",
                Position = new Vector3((index - 1.5f) * 0.041f, -0.002f, -0.116f),
                Rotation = new Vector3(-0.12f - index * 0.018f, 0, (index - 1.5f) * 0.014f)
            };
            hand.AddChild(finger);
            var length = fingerLengths[index];
            var width = fingerWidths[index];
            MeshPart(
                finger,
                FirstPersonMeshFactory.Finger(length, width, width * 1.18f, 0.052f + index * 0.003f),
                Vector3.Zero,
                Vector3.Zero,
                fabric).Name = "ArticulatedGloveFinger";
            MeshPart(
                finger,
                FirstPersonMeshFactory.BackPlate(width * 0.82f, length * 0.19f, 0.012f),
                new Vector3(0, width * 0.55f, -length * 0.27f),
                Vector3.Zero,
                armor).Name = "FingerKnuckleGuard";
        }

        var thumbSide = left ? -1.0f : 1.0f;
        var thumb = new Node3D
        {
            Position = new Vector3(thumbSide * 0.095f, -0.015f, -0.015f),
            Rotation = new Vector3(-0.28f, thumbSide * 0.72f, thumbSide * 0.34f)
        };
        hand.AddChild(thumb);
        MeshPart(
            thumb,
            FirstPersonMeshFactory.Finger(0.152f, 0.049f, 0.052f, 0.043f),
            Vector3.Zero,
            Vector3.Zero,
            fabric).Name = "ArticulatedThumb";
        MeshPart(
            thumb,
            FirstPersonMeshFactory.BackPlate(0.036f, 0.044f, 0.011f),
            new Vector3(0, 0.031f, -0.047f),
            Vector3.Zero,
            armor).Name = "ThumbGuard";
        return hand;
    }

    private static Node3D BuildSleevedForearm(
        Node3D parent,
        Vector3 position,
        Vector3 rotation,
        Godot.Material fabric,
        Godot.Material cuffMaterial)
    {
        var forearm = new Node3D { Position = position, Rotation = rotation };
        parent.AddChild(forearm);
        MeshPart(forearm, FirstPersonMeshFactory.Forearm(), Vector3.Zero, Vector3.Zero, fabric).Name = "TaperedSleeve";
        MeshPart(forearm, FirstPersonMeshFactory.Cuff(), new Vector3(0, 0.195f, 0), Vector3.Zero, cuffMaterial).Name = "GloveCuff";
        MeshPart(
            forearm,
            FirstPersonMeshFactory.BackPlate(0.105f, 0.12f, 0.014f),
            new Vector3(0, 0.018f, -0.083f),
            new Vector3(Mathf.Pi / 2.0f, 0, 0),
            cuffMaterial).Name = "SleeveReinforcement";
        return forearm;
    }

    private void BuildReflexSight(Godot.Material housing, Godot.Material glass)
    {
        var scopeHousing = (StandardMaterial3D)housing.Duplicate();
        scopeHousing.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var sight = new Node3D
        {
            Name = "ReflexSight",
            Position = new Vector3(0, 0.205f, -0.25f)
        };
        _opticRoot = sight;
        _weaponRoot.AddChild(sight);

        _reflexSightModel = new Node3D { Name = "MicroReflex" };
        sight.AddChild(_reflexSightModel);
        MeshPart(_reflexSightModel, Box(new Vector3(0.16f, 0.035f, 0.15f)), new Vector3(0, -0.095f, 0.035f), Vector3.Zero, housing);
        MeshPart(_reflexSightModel, Box(new Vector3(0.145f, 0.022f, 0.04f)), new Vector3(0, 0.062f, -0.025f), Vector3.Zero, housing);
        MeshPart(_reflexSightModel, Box(new Vector3(0.022f, 0.125f, 0.04f)), new Vector3(-0.061f, 0.0f, -0.025f), Vector3.Zero, housing);
        MeshPart(_reflexSightModel, Box(new Vector3(0.022f, 0.125f, 0.04f)), new Vector3(0.061f, 0.0f, -0.025f), Vector3.Zero, housing);
        MeshPart(_reflexSightModel, new QuadMesh { Size = new Vector2(0.1f, 0.095f) }, new Vector3(0, 0, -0.048f), Vector3.Zero, glass);

        _holoSightModel = new Node3D { Name = "HolographicSight", Visible = false };
        sight.AddChild(_holoSightModel);
        MeshPart(_holoSightModel, Box(new Vector3(0.2f, 0.055f, 0.2f)), new Vector3(0, -0.105f, 0.015f), Vector3.Zero, housing);
        MeshPart(_holoSightModel, Box(new Vector3(0.19f, 0.03f, 0.055f)), new Vector3(0, 0.085f, -0.045f), Vector3.Zero, housing);
        MeshPart(_holoSightModel, Box(new Vector3(0.035f, 0.17f, 0.055f)), new Vector3(-0.08f, -0.005f, -0.045f), Vector3.Zero, housing);
        MeshPart(_holoSightModel, Box(new Vector3(0.035f, 0.17f, 0.055f)), new Vector3(0.08f, -0.005f, -0.045f), Vector3.Zero, housing);
        MeshPart(_holoSightModel, new QuadMesh { Size = new Vector2(0.13f, 0.125f) }, new Vector3(0, 0.0f, -0.074f), Vector3.Zero, glass);

        _scopeSightModel = new Node3D { Name = "CombatScope4x", Visible = false };
        sight.AddChild(_scopeSightModel);
        MeshPart(_scopeSightModel, OpenCylinder(0.064f, 0.34f), new Vector3(0, 0, -0.04f), new Vector3(Mathf.Pi / 2.0f, 0, 0), scopeHousing);
        MeshPart(_scopeSightModel, OpenCylinder(0.078f, 0.085f), new Vector3(0, 0, -0.205f), new Vector3(Mathf.Pi / 2.0f, 0, 0), scopeHousing);
        MeshPart(_scopeSightModel, OpenCylinder(0.071f, 0.055f), new Vector3(0, 0, 0.145f), new Vector3(Mathf.Pi / 2.0f, 0, 0), scopeHousing);
        MeshPart(_scopeSightModel, Cylinder(0.059f, 0.003f), new Vector3(0, 0, -0.251f), new Vector3(Mathf.Pi / 2.0f, 0, 0), glass);
        MeshPart(_scopeSightModel, Cylinder(0.053f, 0.003f), new Vector3(0, 0, 0.176f), new Vector3(Mathf.Pi / 2.0f, 0, 0), glass);
        MeshPart(_scopeSightModel, Cylinder(0.026f, 0.052f), new Vector3(0, 0.079f, -0.055f), Vector3.Zero, scopeHousing);
        MeshPart(_scopeSightModel, Cylinder(0.023f, 0.047f), new Vector3(0.079f, 0, -0.055f), new Vector3(0, 0, Mathf.Pi / 2.0f), scopeHousing);
        MeshPart(_scopeSightModel, Box(new Vector3(0.045f, 0.085f, 0.055f)), new Vector3(-0.055f, -0.085f, -0.06f), Vector3.Zero, scopeHousing);
        MeshPart(_scopeSightModel, Box(new Vector3(0.045f, 0.085f, 0.055f)), new Vector3(0.055f, -0.085f, -0.06f), Vector3.Zero, scopeHousing);

        var reticleMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 0.045f, 0.015f, 0.96f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.015f, 0.005f),
            EmissionEnergyMultiplier = 7.0f,
            NoDepthTest = true
        };
        _opticReticle = MeshPart(
            sight,
            new SphereMesh { Radius = 0.003f, Height = 0.006f, RadialSegments = 10, Rings = 5 },
            new Vector3(0, 0, -0.052f),
            Vector3.Zero,
            reticleMaterial);
        _opticReticle.Visible = false;
        InitializeAuthoredOptics();
    }

    private void ApplyWeaponBuildVisuals()
    {
        var definition = WeaponCatalog.Weapon(EquippedWeapon.Platform);
        var stats = EquippedWeapon.Stats();
        var isPistol = WeaponCatalog.IsSidearm(EquippedWeapon.Platform);
        _weaponRoot.Name = definition.Name;
        var barrelPart = EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Barrel, out var barrelId)
            ? WeaponCatalog.Attachment(barrelId)
            : null;
        var barrelScale = barrelPart?.VisualScale ?? 1.0f;
        var barrelLength = definition.BarrelLength * barrelScale;
        var receiverColor = EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Color(0.12f, 0.105f, 0.075f),
            WeaponPlatform.ScarL => new Color(0.34f, 0.29f, 0.2f),
            WeaponPlatform.M24 => new Color(0.16f, 0.19f, 0.17f),
            WeaponPlatform.AXMC => new Color(0.035f, 0.14f, 0.15f),
            WeaponPlatform.MP5A5 => new Color(0.025f, 0.032f, 0.03f),
            WeaponPlatform.M3A1 => new Color(0.17f, 0.2f, 0.185f),
            WeaponPlatform.P226 => new Color(0.055f, 0.06f, 0.065f),
            WeaponPlatform.M1911 => new Color(0.16f, 0.15f, 0.13f),
            WeaponPlatform.AWM => new Color(0.2f, 0.22f, 0.21f),
            WeaponPlatform.VSS => new Color(0.075f, 0.1f, 0.075f),
            WeaponPlatform.DesertEagle => new Color(0.42f, 0.44f, 0.41f),
            WeaponPlatform.GSh18 => new Color(0.045f, 0.052f, 0.05f),
            _ => new Color(0.045f, 0.052f, 0.05f)
        };
        var furnitureColor = EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Color(0.24f, 0.12f, 0.055f),
            WeaponPlatform.ScarL => new Color(0.29f, 0.255f, 0.18f),
            WeaponPlatform.M24 => new Color(0.18f, 0.24f, 0.16f),
            WeaponPlatform.AXMC => new Color(0.08f, 0.29f, 0.28f),
            WeaponPlatform.MP5A5 => new Color(0.055f, 0.065f, 0.06f),
            WeaponPlatform.M3A1 => new Color(0.105f, 0.12f, 0.11f),
            WeaponPlatform.P226 => new Color(0.075f, 0.08f, 0.085f),
            WeaponPlatform.M1911 => new Color(0.22f, 0.12f, 0.065f),
            WeaponPlatform.AWM => new Color(0.15f, 0.18f, 0.16f),
            WeaponPlatform.VSS => new Color(0.16f, 0.24f, 0.14f),
            WeaponPlatform.DesertEagle => new Color(0.07f, 0.075f, 0.07f),
            WeaponPlatform.GSh18 => new Color(0.07f, 0.075f, 0.072f),
            _ => new Color(0.18f, 0.17f, 0.13f)
        };
        var receiverMaterial = TacticalSurfaceLibrary.WeaponFinish(receiverColor, 0.52f, 0.46f);
        var furnitureMaterial = TacticalSurfaceLibrary.WeaponFinish(furnitureColor, 0.12f, 0.68f, 5.5f);

        var receiverSize = EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Vector3(0.14f, 0.16f, 0.52f),
            WeaponPlatform.ScarL => new Vector3(0.16f, 0.18f, 0.56f),
            WeaponPlatform.M24 => new Vector3(0.145f, 0.16f, 0.64f),
            WeaponPlatform.AXMC => new Vector3(0.16f, 0.18f, 0.72f),
            WeaponPlatform.MP5A5 => new Vector3(0.14f, 0.17f, 0.36f),
            WeaponPlatform.M3A1 => new Vector3(0.135f, 0.155f, 0.34f),
            WeaponPlatform.P226 => new Vector3(0.13f, 0.13f, 0.29f),
            WeaponPlatform.M1911 => new Vector3(0.125f, 0.14f, 0.31f),
            WeaponPlatform.AWM => new Vector3(0.165f, 0.18f, 0.76f),
            WeaponPlatform.VSS => new Vector3(0.15f, 0.165f, 0.5f),
            WeaponPlatform.DesertEagle => new Vector3(0.145f, 0.15f, 0.36f),
            WeaponPlatform.GSh18 => new Vector3(0.125f, 0.13f, 0.28f),
            _ => new Vector3(0.13f, 0.15f, 0.46f)
        };
        ((BoxMesh)_receiver.Mesh).Size = receiverSize;
        _receiver.MaterialOverride = receiverMaterial;
        var receiverSideX = receiverSize.X * 0.5f + 0.007f;
        var triggerCenterZ = receiverSize.Z * 0.16f;
        ((BoxMesh)_receiverSeam.Mesh).Size = new Vector3(
            receiverSize.X + 0.006f,
            0.018f,
            receiverSize.Z * 0.78f);
        _receiverSeam.Position = new Vector3(0.0f, -receiverSize.Y * 0.5f - 0.008f, -receiverSize.Z * 0.025f);
        ((BoxMesh)_ejectionPort.Mesh).Size = new Vector3(
            0.014f,
            receiverSize.Y * 0.36f,
            Mathf.Clamp(receiverSize.Z * 0.33f, 0.11f, 0.18f));
        _ejectionPort.Position = new Vector3(receiverSideX, receiverSize.Y * 0.12f, -receiverSize.Z * 0.1f);
        ((BoxMesh)_boltCarrier.Mesh).Size = new Vector3(
            0.018f,
            receiverSize.Y * 0.19f,
            Mathf.Clamp(receiverSize.Z * 0.24f, 0.085f, 0.14f));
        _boltCarrier.Position = new Vector3(receiverSideX + 0.008f, receiverSize.Y * 0.36f, -receiverSize.Z * 0.11f);
        _triggerGuardFront.Position = new Vector3(0.0f, -0.105f, triggerCenterZ + 0.07f);
        _triggerGuardRear.Position = new Vector3(0.0f, -0.105f, triggerCenterZ - 0.07f);
        _triggerGuardBase.Position = new Vector3(0.0f, -0.166f, triggerCenterZ);
        _trigger.Position = new Vector3(0.0f, -0.088f, triggerCenterZ + 0.003f);
        _selector.Position = new Vector3(receiverSideX + 0.011f, 0.008f, triggerCenterZ + 0.01f);
        ((BoxMesh)_handguard.Mesh).Size = isPistol
            ? new Vector3(0.115f, 0.075f, 0.13f)
            : new Vector3(
                EquippedWeapon.Platform is WeaponPlatform.ScarL or WeaponPlatform.M24 or WeaponPlatform.AXMC or WeaponPlatform.AWM
                    ? 0.17f
                    : EquippedWeapon.Platform == WeaponPlatform.M3A1 ? 0.13f : 0.15f,
                0.12f,
                Mathf.Max(0.28f, barrelLength * 0.72f));
        _handguard.Position = isPistol
            ? new Vector3(0, -0.005f, -0.21f)
            : new Vector3(0, 0.01f, -0.29f - barrelLength * 0.25f);
        _handguard.MaterialOverride = furnitureMaterial;
        ((CylinderMesh)_barrelPart.Mesh).Height = barrelLength;
        var barrelBase = isPistol ? -0.25f : -0.56f;
        _barrelPart.Position = new Vector3(0, 0.015f, barrelBase - barrelLength * 0.5f);

        var muzzleLength = isPistol ? 0.055f : 0.14f;
        var muzzleRadius = isPistol ? 0.027f : 0.047f;
        if (EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Muzzle, out var muzzleId))
        {
            var muzzle = WeaponCatalog.Attachment(muzzleId);
            muzzleLength *= muzzle.VisualScale;
            muzzleRadius = muzzleId == "muzzle_suppressor" ? 0.055f : 0.048f;
        }
        ((CylinderMesh)_muzzlePart.Mesh).Height = muzzleLength;
        ((CylinderMesh)_muzzlePart.Mesh).TopRadius = muzzleRadius;
        ((CylinderMesh)_muzzlePart.Mesh).BottomRadius = muzzleRadius;
        var muzzleZ = barrelBase - barrelLength - muzzleLength * 0.5f;
        _muzzlePart.Position = new Vector3(0, 0.015f, muzzleZ);
        _muzzle.Position = new Vector3(0, 0.015f, muzzleZ - muzzleLength * 0.55f);
        _muzzle.Rotation = Vector3.Zero;
        _muzzle.Scale = Vector3.One;
        _ejectMarker.Position = new Vector3(0.13f, 0.08f, -0.12f);
        _ejectMarker.Rotation = Vector3.Zero;
        _ejectMarker.Scale = Vector3.One;

        var stockScale = EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Stock, out var stockId)
            ? WeaponCatalog.Attachment(stockId).VisualScale
            : 1.0f;
        _stock.Visible = !isPistol;
        ((BoxMesh)_stock.Mesh).Size = EquippedWeapon.Platform == WeaponPlatform.M3A1
            ? new Vector3(0.055f, 0.055f, 0.34f)
            : new Vector3(0.14f * stockScale, 0.13f * stockScale, 0.38f * stockScale);
        _stock.MaterialOverride = furnitureMaterial;
        _stock.Position = new Vector3(0, -0.01f, 0.25f + 0.13f * stockScale);

        var magazineSize = EquippedWeapon.Platform switch
        {
            WeaponPlatform.M24 => new Vector3(0.085f, 0.15f, 0.13f),
            WeaponPlatform.AXMC => new Vector3(0.1f, 0.18f, 0.15f),
            WeaponPlatform.MP5A5 => new Vector3(0.075f, stats.MagazineSize > 30 ? 0.36f : 0.3f, 0.11f),
            WeaponPlatform.M3A1 => new Vector3(0.075f, 0.3f, 0.11f),
            WeaponPlatform.P226 => new Vector3(0.065f, 0.2f, 0.085f),
            WeaponPlatform.M1911 => new Vector3(0.06f, 0.18f, 0.08f),
            WeaponPlatform.AWM => new Vector3(0.1f, 0.18f, 0.15f),
            WeaponPlatform.VSS => new Vector3(0.09f, 0.24f, 0.13f),
            WeaponPlatform.DesertEagle => new Vector3(0.075f, 0.2f, 0.1f),
            WeaponPlatform.GSh18 => new Vector3(0.065f, 0.21f, 0.085f),
            _ => new Vector3(0.09f, 0.26f * (stats.MagazineSize > 30 ? 1.24f : 1.0f), 0.14f)
        };
        ((BoxMesh)_magazine.Mesh).Size = magazineSize;
        ((BoxMesh)_spareMagazine.Mesh).Size = magazineSize;
        _magazine.MaterialOverride = EquippedWeapon.Platform == WeaponPlatform.AK74 ? furnitureMaterial : receiverMaterial;
        _spareMagazine.MaterialOverride = _magazine.MaterialOverride;
        _magazine.Rotation = EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Vector3(-0.29f, 0, 0)
            : new Vector3(-0.19f, 0, 0);

        var gripScale = EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Grip, out var gripId)
            ? WeaponCatalog.Attachment(gripId).VisualScale
            : 0.0f;
        _foregrip.Visible = gripScale > 0.0f;
        _foregrip.Scale = new Vector3(1.0f, gripScale, gripId == "grip_angled" ? 1.35f : 1.0f);
        _foregrip.Rotation = gripId == "grip_angled" ? new Vector3(-0.42f, 0, 0) : Vector3.Zero;

        var opticScale = EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var opticId)
            ? WeaponCatalog.Attachment(opticId).VisualScale
            : 0.0f;
        _opticRoot.Visible = opticScale > 0.0f;
        _opticRoot.Scale = Vector3.One;
        var usesIntegratedWeaponOptic = WeaponUsesIntegratedOptic(
            EquippedWeapon.Platform,
            opticId);
        _opticRoot.Position = new Vector3(
            0,
            OpticMountHeight(EquippedWeapon.Platform, opticId),
            -0.25f);
        // Finished DCC assets own every visible sight housing. The retained
        // legacy nodes are invisible compatibility scaffolding only.
        _reflexSightModel.Visible = false;
        _holoSightModel.Visible = false;
        _scopeSightModel.Visible = false;
        var usesExternalAuthoredOptic = RefreshAuthoredOpticPresentation(
            opticId,
            usesIntegratedWeaponOptic);
        if (!usesExternalAuthoredOptic)
        {
            _opticReticle.Position = usesIntegratedWeaponOptic
                ? Vector3.Zero
                : opticId switch
            {
                "optic_scope" => new Vector3(0, 0, -0.255f),
                "optic_7x" => new Vector3(0, 0, -0.255f),
                "optic_sniper" => new Vector3(0, 0, -0.255f),
                "optic_holo" => new Vector3(0, 0, -0.078f),
                _ => new Vector3(0, 0, -0.052f)
            };
        }

        _weaponLight.SpotRange = stats.EffectiveRange * 0.28f;
        _weaponLight.Position = isPistol
            ? new Vector3(0.065f, -0.04f, -0.28f)
            : new Vector3(0.09f, -0.015f, -0.5f - barrelLength * 0.45f);
        foreach (var voice in _gunAudioVoices)
        {
            voice.Stop();
            voice.Stream = SoundLab.PlayerWeaponShot(EquippedWeapon);
            voice.VolumeDb = SoundLab.PlayerWeaponShotVolumeDb(EquippedWeapon);
        }
        _nextGunAudioVoice = 0;
        ResetViewmodelShotImpulse();
        Ammo = Mathf.Min(Ammo, stats.MagazineSize);
        RefreshPlatformSignatureVisual();
        RefreshAuthoredPrimaryWeapon();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseMotion motion
            || Input.MouseMode != Input.MouseModeEnum.Captured
            || IsDead)
        {
            return;
        }

        if (IsInVehicle)
        {
            // Yaw is owned by the vehicle body while driving; only pitch the cabin view.
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -0.55f, 0.42f);
            return;
        }
        if (_isClimbingLadder)
        {
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -0.55f, 0.42f);
            return;
        }
        if (_isVaulting)
        {
            // Keep the body aligned with the authored vault arc while preserving a
            // small first-person look range during the movement lock.
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -0.7f, 0.58f);
            return;
        }

        var rotation = Rotation;
        rotation.Y -= motion.Relative.X * MouseSensitivity;
        Rotation = rotation;
        _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.38f, 1.38f);
    }

    public override void _PhysicsProcess(double delta)
    {
        RecordCombatMovementTrail();
        var dt = (float)delta;
        AdvanceWeaponCycleInput(dt);
        if (IsDead)
        {
            if (_isClimbingLadder)
            {
                CancelLadderClimb(notify: false);
            }
            if (_isVaulting)
            {
                CancelLowObstacleVault("death");
            }
            CloseMedicalWheelWithoutUse();
            CancelFieldUse(false);
            CancelReload();
            UpdateDownedCrawl(dt);
            return;
        }
        UpdateMedicalSystem(dt);
        if (IsInVehicle)
        {
            UpdateVehiclePassenger(dt);
            return;
        }
        if (_isClimbingLadder)
        {
            UpdateLadderClimb(dt);
            if (_isClimbingLadder)
            {
                UpdateCameraAndWeapon(dt);
                PushHudStats();
            }
            return;
        }
        if (_isVaulting && UiLocked)
        {
            CancelLowObstacleVault("ui_locked");
        }
        if (_isVaulting)
        {
            UpdateVaultMovement(dt);
            UpdateCameraAndWeapon(dt);
            PushHudStats();
            Hud?.SetAiming(false);
            return;
        }
        if (HandleMedicalWheelInput())
        {
            PushHudStats();
            return;
        }
        if (UiLocked)
        {
            Velocity = Vector3.Zero;
            _isAiming = false;
            Hud?.SetAiming(false);
            return;
        }

        UpdateRoleAbility(dt);
        if (Input.IsActionJustPressed(GameInputActions.UseClassSkill))
        {
            ActivateRoleAbility();
        }

        _fireCooldown = Mathf.Max(0.0f, _fireCooldown - dt);
        _knifeTime = Mathf.Max(0.0f, _knifeTime - dt);
        if (!_fireInputArmed)
        {
            if (Input.IsActionPressed(GameInputActions.Fire))
            {
                _fireReleaseTime = 0.0f;
            }
            else
            {
                _fireReleaseTime += dt;
                _fireInputArmed = _fireReleaseTime >= 0.2f;
            }
        }

        if (!MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.WeaponPrimary))
        {
            ActivateWeaponSlot(PlayerWeaponSlot.Primary, true);
        }
        else if (!MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.WeaponSecondary))
        {
            ActivateWeaponSlot(PlayerWeaponSlot.Secondary, true);
        }
        else if (!MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.WeaponSidearm))
        {
            ActivateWeaponSlot(PlayerWeaponSlot.Sidearm, true);
        }
        else if (!MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.WeaponMelee))
        {
            ActivateWeaponSlot(PlayerWeaponSlot.Melee, true);
        }
        else if (!MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.WeaponGrenade))
        {
            SelectQuickSlot(PlayerQuickSlot.FragmentationGrenade);
        }
        else if (!MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.WeaponUtility))
        {
            SelectQuickSlot(PlayerQuickSlot.Utility);
        }
        else if (!MedicalActionBlocksWeapon
            && Input.IsActionJustPressed(GameInputActions.WeaponCycle)
            && TryAcceptWeaponCycleInput(Input.IsActionPressed(GameInputActions.Fire)))
        {
            CycleWeaponSlots();
        }

        if (Input.IsActionJustPressed(GameInputActions.ToggleFireMode)
            && IsFirearmQuickSlotSelected
            && !_isReloading
            && !_isPlating
            && !MedicalActionBlocksWeapon
            && WeaponCatalog.Weapon(EquippedWeapon.Platform).SupportsAutomatic)
        {
            _automaticFire = !_automaticFire;
            Hud?.ShowLocalizedMessage(
                _automaticFire ? "fire_auto" : "fire_semi",
                _automaticFire ? "FIRE MODE  //  AUTO" : "FIRE MODE  //  SEMI",
                new Color(0.42f, 0.9f, 0.73f));
        }
        if (Input.IsActionJustPressed(GameInputActions.ToggleFlashlight) && IsFirearmQuickSlotSelected && !MedicalActionBlocksWeapon)
        {
            _flashlightOn = !_flashlightOn;
            _weaponLight.Visible = _flashlightOn;
            Hud?.ShowLocalizedMessage(
                _flashlightOn ? "light_on" : "light_off",
                _flashlightOn ? "WEAPON LIGHT  //  ON" : "WEAPON LIGHT  //  OFF",
                _flashlightOn ? new Color(0.72f, 0.9f, 1.0f) : new Color(0.55f, 0.65f, 0.63f));
        }
        if (Input.IsActionJustPressed(GameInputActions.ToggleNvg) && !MedicalActionBlocksWeapon)
        {
            if (!HasNightVisionHelmet)
            {
                Hud?.ShowLocalizedMessage(
                    "nvg_no_helmet",
                    "NVG HELMET REQUIRED  //  EQUIP NIGHT OPS",
                    new Color(1.0f, 0.55f, 0.28f));
            }
            else
            {
                _nvgOn = !_nvgOn;
                Main?.SetNightVisionActive(_nvgOn);
                Hud?.SetNightVisionActive(_nvgOn);
                Hud?.ShowLocalizedMessage(
                    _nvgOn ? "nvg_on" : "nvg_off",
                    _nvgOn ? "NVG  //  ON  //  N" : "NVG  //  OFF  //  N",
                    _nvgOn ? new Color(0.42f, 0.95f, 0.42f) : new Color(0.55f, 0.65f, 0.55f));
            }
        }
        if (Input.IsActionJustPressed(GameInputActions.UsePlate) && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon)
        {
            if (_isPlating)
            {
                CancelPlate(notify: true);
            }
            else
            {
                StartPlate();
            }
        }

        UpdatePlate(dt);
        if (_isReloading)
        {
            _reloadTime -= dt;
            if (_reloadTime <= 0.0f)
            {
                FinishReload();
            }
        }
        if (IsFirearmQuickSlotSelected && !_isPlating && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.Reload))
        {
            StartReload();
        }
        if (!_isPlating && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon && Input.IsActionJustPressed(GameInputActions.ThrowGrenade))
        {
            ThrowGrenade();
        }
        _isAiming = IsFirearmQuickSlotSelected && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon && Input.IsActionPressed(GameInputActions.Aim) && !_isReloading && !_isPlating && _slideTime <= 0.0f;
        var fireRequested = IsFirearmQuickSlotSelected && _automaticFire
            ? Input.IsActionPressed(GameInputActions.Fire)
            : Input.IsActionJustPressed(GameInputActions.Fire);
        if (!_isPlating && !RoleActionBlocksWeapon && !MedicalActionBlocksWeapon && _fireInputArmed && fireRequested && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (_activeQuickSlot == PlayerQuickSlot.Melee)
            {
                StartKnifeAttack();
            }
            else if (IsThrowableQuickSlotSelected)
            {
                ThrowSelectedQuickSlot();
            }
            else
            {
                Fire();
            }
        }

        MovePlayer(dt);
        UpdateCameraAndWeapon(dt);
        var fieldSupplies = PushHudStats();
        Hud?.SetEquipment(
            fieldSupplies.ArmorPlates,
            _activeQuickSlot switch
            {
                PlayerQuickSlot.Melee => "MELEE",
                PlayerQuickSlot.FragmentationGrenade => "GRENADE",
                PlayerQuickSlot.Utility => "UTILITY",
                _ => _automaticFire ? "AUTO" : "SEMI"
            },
            _activeQuickSlot switch
            {
                PlayerQuickSlot.FragmentationGrenade => GameLocalization.Get("grenade", Hud?.CurrentLanguage ?? "en", "FRAG GRENADE"),
                PlayerQuickSlot.Utility => GameLocalization.Get("smoke_grenade", Hud?.CurrentLanguage ?? "en", "SMOKE GRENADE"),
                PlayerQuickSlot.Melee => CurrentMeleeDefinition.DisplayName(Hud?.CurrentLanguage ?? "en"),
                _ => EquippedWeapon.DisplayName(Hud?.CurrentLanguage ?? "en")
            },
            PrimaryWeaponForHud,
            HasFireablePrimary,
            EquippedKnifeSkinId,
            SecondaryWeaponForHud,
            SidearmWeaponForHud,
            (int)ActiveQuickSlot);
        Hud?.SetAiming(_isAiming);
        var heading = Mathf.RadToDeg(Rotation.Y) * -1.0f;
        Hud?.SetHeading(heading);
        Hud?.SetMinimapPlayer(GlobalPosition, heading);
    }

    private void UpdateVehiclePassenger(float delta)
    {
        Velocity = Vector3.Zero;
        HasMovementIntent = false;
        _isAiming = false;
        _slideTime = 0.0f;
        _fireCooldown = Mathf.Max(0.0f, _fireCooldown - delta);

        if (_vehicle is null || !GodotObject.IsInstanceValid(_vehicle) || _vehicle.IsDestroyed)
        {
            if (_vehicle is not null)
            {
                ExitVehicle(GlobalPosition + GlobalBasis.X * 2.0f + Vector3.Up * 0.2f, forced: true);
            }
            return;
        }

        // Keep the rider seated; look pitch only. The cab gunner keeps the firearm up.
        Position = Vector3.Zero;
        Rotation = Vector3.Zero;
        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(headPosition.Y, 0.52f, delta * 12.0f);
        headPosition.Z = Mathf.Lerp(headPosition.Z, 0.05f, delta * 12.0f);
        _head.Position = headPosition;
        _head.Rotation = new Vector3(_pitch, 0.0f, 0.0f);
        if (IsInstanceValid(_weaponRoot))
        {
            _weaponRoot.Visible = IsFirearmQuickSlotSelected;
        }
        if (IsInstanceValid(_knifeRoot))
        {
            _knifeRoot.Visible = false;
        }
        UpdateHeldThrowableVisual();

        if (IsFirearmQuickSlotSelected
            && !RoleActionBlocksWeapon
            && Input.IsActionJustPressed(GameInputActions.Reload))
        {
            StartReload();
        }
        if (_isReloading)
        {
            _reloadTime -= delta;
            if (_reloadTime <= 0.0f)
            {
                FinishReload();
            }
        }
        UpdateReloadAnimation();
        SyncAuthoredPrimaryWeapon();
        UpdateAuthoredM4ReloadSupportArm();

        if (!_fireInputArmed)
        {
            if (Input.IsActionPressed(GameInputActions.Fire))
            {
                _fireReleaseTime = 0.0f;
            }
            else
            {
                _fireReleaseTime += delta;
                _fireInputArmed = _fireReleaseTime >= 0.2f;
            }
        }
        var fireRequested = IsFirearmQuickSlotSelected && _automaticFire
            ? Input.IsActionPressed(GameInputActions.Fire)
            : Input.IsActionJustPressed(GameInputActions.Fire);
        if (IsFirearmQuickSlotSelected
            && _fireInputArmed
            && fireRequested
            && Input.MouseMode == Input.MouseModeEnum.Captured
            && !RoleActionBlocksWeapon
            && !MedicalActionBlocksWeapon)
        {
            Fire();
        }

        // Light cabin camera sway from vehicle speed (no full weapon bob).
        if (IsInstanceValid(_camera))
        {
            var sway = Mathf.Sin(Time.GetTicksMsec() * 0.008f) * 0.004f;
            _camera.Position = new Vector3(sway, 0.0f, 0.0f);
            _camera.Fov = Mathf.Lerp(_camera.Fov, 72.0f, delta * 8.0f);
        }

        PushHudStats();
        if (IsInstanceValid(_vehicle))
        {
            var ratio = _vehicle.Health / Mathf.Max(1.0f, _vehicle.MaxHealth);
            var fireHint = IsFirearmQuickSlotSelected ? "  LMB FIRE" : string.Empty;
            Hud?.SetInteraction(
                GameLocalization.IsChinese(Hud?.CurrentLanguage ?? "en")
                    ? $"载具耐久  {(int)_vehicle.Health}  //  WASD驾驶  F下车{fireHint}"
                    : $"VEHICLE HP  {(int)_vehicle.Health}  //  WASD DRIVE  F EXIT{fireHint}",
                ratio,
                true);
        }
    }

    /// <summary>Ground speed of the ridden vehicle; adds cab-gun spread, zero on foot.</summary>
    private float PlatformMotionSpeed()
    {
        if (!IsInVehicle || _vehicle is null || !GodotObject.IsInstanceValid(_vehicle))
        {
            return 0.0f;
        }
        var vehicleVelocity = _vehicle.Velocity;
        return new Vector2(vehicleVelocity.X, vehicleVelocity.Z).Length();
    }

    private FieldSupplySnapshot PushHudStats()
    {
        var fieldSupplies = CaptureFieldSupplySnapshot();
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
        Hud?.SetStaminaRecoveryState(SprintRecoveryRequired);
        Hud?.SetAmmoTier(CurrentAmmoGrade);
        Hud?.SetMedicalInventory(fieldSupplies, AdrenalineActive, AdrenalineRemaining);
        return fieldSupplies;
    }

    private void UpdateDownedCrawl(float delta)
    {
        // Soft prone crawl: slow drag while waiting for a teammate revive.
        _stance = PlayerStance.Prone;
        var targetHeadY = 0.42f;
        var targetColliderHeight = 0.72f;
        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(headPosition.Y, targetHeadY, delta * 10.0f);
        _head.Position = headPosition;
        if (_collider.Shape is CapsuleShape3D capsule)
        {
            capsule.Height = Mathf.Lerp(capsule.Height, targetColliderHeight, delta * 10.0f);
            var colliderPosition = _collider.Position;
            colliderPosition.Y = Mathf.Lerp(colliderPosition.Y, targetColliderHeight * 0.5f, delta * 10.0f);
            _collider.Position = colliderPosition;
        }

        if (IsInVehicle)
        {
            EjectFromVehicleIfAny();
        }

        var input = UiLocked
            ? Vector2.Zero
            : Input.GetVector(
                GameInputActions.MoveLeft,
                GameInputActions.MoveRight,
                GameInputActions.MoveForward,
                GameInputActions.MoveBackward);
        var direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        var velocity = Velocity;
        velocity.X = Mathf.MoveToward(velocity.X, direction.X * CrawlSpeed, delta * 10.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * CrawlSpeed, delta * 10.0f);
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * delta;
        }
        else
        {
            velocity.Y = -0.15f;
        }
        Velocity = velocity;
        MoveAndSlide();
        TryStairStepUp(direction);
        HasMovementIntent = input.LengthSquared() > 0.001f;
        _isAiming = false;
        Hud?.SetAiming(false);
        PushHudStats();
        if (IsInstanceValid(_camera))
        {
            _camera.Fov = Mathf.Lerp(_camera.Fov, 68.0f, delta * 6.0f);
        }
    }

    /// <summary>
    /// When walking into a low ledge (stair tread), snap the capsule onto it.
    /// Thin discrete treads need this — default capsule cannot mount ~0.1 m plates alone.
    /// </summary>
    private void TryStairStepUp(Vector3 moveDirection)
    {
        if (!IsOnFloor() || moveDirection.LengthSquared() < 0.01f)
        {
            return;
        }
        const float maxStep = 0.28f;
        var forward = moveDirection.Normalized();
        // Probe a few distances ahead for the next tread top.
        float bestLift = 0.0f;
        Vector3 bestLand = GlobalPosition;
        foreach (var dist in new[] { 0.28f, 0.42f, 0.55f })
        {
            var from = GlobalPosition + Vector3.Up * (maxStep + 0.12f) + forward * dist;
            var to = from + Vector3.Down * (maxStep + 0.45f);
            if (!PhysicsRaycast.TryHit(GetWorld3D(), from, to, GetRid(), 1, out var hit))
            {
                continue;
            }
            var normal = hit.Normal;
            if (normal.Dot(Vector3.Up) < 0.96f)
            {
                continue;
            }
            var land = hit.Position;
            var lift = land.Y - GlobalPosition.Y;
            if (lift > bestLift && lift <= maxStep)
            {
                bestLift = lift;
                bestLand = land;
            }
        }
        if (bestLift > 0.025f)
        {
            var previousY = GlobalPosition.Y;
            GlobalPosition = new Vector3(
                GlobalPosition.X + forward.X * 0.035f,
                bestLand.Y + 0.03f,
                GlobalPosition.Z + forward.Z * 0.035f);
            var appliedLift = Mathf.Max(0.0f, GlobalPosition.Y - previousY);
            _stairViewOffsetY = Mathf.Clamp(_stairViewOffsetY - appliedLift, -0.34f, 0.0f);
            var v = Velocity;
            v.Y = 0.0f;
            Velocity = v;
        }
    }

    private void MovePlayer(float delta)
    {
        var input = Input.GetVector(
            GameInputActions.MoveLeft,
            GameInputActions.MoveRight,
            GameInputActions.MoveForward,
            GameInputActions.MoveBackward);
        if (!_movementInputArmed)
        {
            if (input.LengthSquared() > 0.001f)
            {
                _movementReleaseTime = 0.0f;
            }
            else
            {
                _movementReleaseTime += delta;
                _movementInputArmed = _movementReleaseTime >= 0.2f;
            }
            input = Vector2.Zero;
        }
        HasMovementIntent = _movementInputArmed && input.LengthSquared() > 0.001f;
        var direction = (Transform.Basis * new Vector3(input.X, 0, input.Y)).Normalized();
        var horizontalSpeedBeforeMove = new Vector2(Velocity.X, Velocity.Z).Length();
        UpdateStanceInput(horizontalSpeedBeforeMove);
        var jumpPressed = Input.IsActionJustPressed(GameInputActions.Jump) && IsOnFloor() && !_isPlating;
        if (jumpPressed && _stance != PlayerStance.Standing)
        {
            _slideTime = 0.0f;
            TrySetStance(PlayerStance.Standing);
        }
        if (jumpPressed
            && _stance == PlayerStance.Standing
            && TryVaultLowObstacle(direction))
        {
            UpdateVaultMovement(delta);
            return;
        }
        var crouching = _stance == PlayerStance.Crouched;
        var prone = _stance == PlayerStance.Prone;
        var sprinting = Input.IsActionPressed(GameInputActions.Sprint) && input.Y < -0.15f && !crouching && !prone && !_isPlating
            && Stamina > 1.0f && !SprintRecoveryRequired && !_isAiming;
        var speed = (prone ? ProneSpeed : crouching ? CrouchSpeed : sprinting ? SprintSpeed : WalkSpeed)
            * RoleMovementMultiplier
            * MedicalMovementMultiplier
            * (_isPlating ? 0.68f : 1.0f);

        if (_slideTime > 0.0f)
        {
            crouching = true;
            prone = false;
        }

        var velocity = Velocity;
        if (_slideTime > 0.0f)
        {
            _slideTime = Mathf.Max(0.0f, _slideTime - delta);
            crouching = true;
            var slideSpeed = 5.0f + 4.2f * (_slideTime / 0.72f);
            velocity.X = Mathf.MoveToward(velocity.X, _slideDirection.X * slideSpeed, delta * 4.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, _slideDirection.Z * slideSpeed, delta * 4.0f);
            Stamina = Mathf.Max(0.0f, Stamina - delta * 8.0f);
        }
        else
        {
            var horizontal = new Vector2(velocity.X, velocity.Z);
            var targetHorizontal = new Vector2(direction.X, direction.Z) * speed;
            var response = IsOnFloor()
                ? input.LengthSquared() > 0.001f ? 34.0f : 43.0f
                : 7.5f;
            horizontal = horizontal.MoveToward(targetHorizontal, response * delta);
            velocity.X = horizontal.X;
            velocity.Z = horizontal.Y;
        }

        UpdateStaminaState(delta, sprinting);
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * delta;
        }
        else if (jumpPressed && !crouching && !prone)
        {
            velocity.Y = 6.8f;
        }
        Velocity = velocity;
        MoveAndSlide();
        TryStairStepUp(direction);

        var targetHeadY = prone ? 0.62f : crouching ? 1.16f : 1.57f;
        var targetColliderHeight = prone ? 0.78f : crouching ? 1.2f : 1.75f;
        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(headPosition.Y, targetHeadY, SmoothFactor(12.0f, delta));
        _head.Position = headPosition;
        var capsule = (CapsuleShape3D)_collider.Shape;
        capsule.Height = Mathf.Lerp(capsule.Height, targetColliderHeight, SmoothFactor(12.0f, delta));
        var colliderPosition = _collider.Position;
        colliderPosition.Y = Mathf.Lerp(colliderPosition.Y, targetColliderHeight * 0.5f, SmoothFactor(12.0f, delta));
        _collider.Position = colliderPosition;

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        if (IsOnFloor() && horizontalSpeed > 1.0f && _slideTime <= 0.0f)
        {
            _footstepTimer -= delta;
            if (_footstepTimer <= 0.0f)
            {
                _footstepAudio.PitchScale = _rng.RandfRange(0.9f, 1.08f);
                _footstepAudio.VolumeDb = sprinting ? -11.0f : -15.0f;
                _footstepAudio.Play();
                _footstepTimer = sprinting ? 0.33f : 0.46f;
            }
        }
        else
        {
            _footstepTimer = Mathf.Min(_footstepTimer, 0.08f);
        }
    }

    private void UpdateStanceInput(float horizontalSpeed)
    {
        if (!IsOnFloor())
        {
            return;
        }
        if (Input.IsActionJustPressed(GameInputActions.Prone))
        {
            TrySetStance(_stance == PlayerStance.Prone ? PlayerStance.Crouched : PlayerStance.Prone);
            _slideTime = 0.0f;
            return;
        }
        if (!Input.IsActionJustPressed(GameInputActions.Crouch))
        {
            return;
        }
        if (_stance == PlayerStance.Standing && IsOnFloor() && horizontalSpeed > 7.1f)
        {
            _stance = PlayerStance.Crouched;
            _slideTime = 0.72f;
            _slideDirection = new Vector3(Velocity.X, 0, Velocity.Z).Normalized();
            return;
        }
        var target = _stance switch
        {
            PlayerStance.Standing => PlayerStance.Crouched,
            PlayerStance.Crouched => PlayerStance.Standing,
            _ => PlayerStance.Crouched
        };
        TrySetStance(target);
    }

    public bool TrySetStance(PlayerStance target)
    {
        if (target == _stance)
        {
            return true;
        }
        if (StanceHeight(target) > StanceHeight(_stance) && !HasStanceClearance(StanceHeight(target)))
        {
            Hud?.ShowLocalizedMessage("stance_blocked", "NOT ENOUGH CLEARANCE", new Color(1.0f, 0.58f, 0.28f));
            return false;
        }
        _stance = target;
        if (_stance == PlayerStance.Prone)
        {
            _slideTime = 0.0f;
        }
        return true;
    }

    private bool HasStanceClearance(float targetHeight)
    {
        var currentHeight = StanceHeight(_stance);
        var offsets = new[]
        {
            Vector3.Zero,
            Vector3.Right * 0.28f,
            Vector3.Left * 0.28f,
            Vector3.Forward * 0.28f,
            Vector3.Back * 0.28f
        };
        foreach (var offset in offsets)
        {
            if (PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    GlobalPosition + offset + Vector3.Up * (currentHeight - 0.08f),
                    GlobalPosition + offset + Vector3.Up * (targetHeight + 0.08f),
                    GetRid(),
                    1))
            {
                return false;
            }
        }
        return true;
    }

    private static float StanceHeight(PlayerStance stance) => stance switch
    {
        PlayerStance.Prone => 0.78f,
        PlayerStance.Crouched => 1.2f,
        _ => 1.75f
    };

    private static float SmoothFactor(float response, float delta)
    {
        return 1.0f - Mathf.Exp(-response * Mathf.Max(0.0f, delta));
    }

    private void UpdateStaminaState(float delta, bool sprinting)
    {
        if (sprinting)
        {
            Stamina = Mathf.Max(0.0f, Stamina - delta * 21.0f * MedicalStaminaDrainMultiplier);
            if (Stamina <= 0.01f && !_sprintRecoveryRequired)
            {
                _sprintRecoveryRequired = true;
                _sprintRecoveryDelay = SprintRecoveryDelay;
                Hud?.ShowLocalizedMessage("stamina_exhausted", "STAMINA EXHAUSTED  //  RECOVER", new Color(1.0f, 0.62f, 0.24f));
            }
            return;
        }

        if (_sprintRecoveryDelay > 0.0f)
        {
            var recoveryDelta = Mathf.Max(0.0f, delta - _sprintRecoveryDelay);
            _sprintRecoveryDelay = Mathf.Max(0.0f, _sprintRecoveryDelay - delta);
            if (recoveryDelta <= 0.0f)
            {
                return;
            }
            delta = recoveryDelta;
        }
        Stamina = Mathf.Min(100.0f, Stamina + delta * 14.0f * MedicalStaminaRecoveryMultiplier);
        if (_sprintRecoveryRequired
            && _sprintRecoveryDelay <= 0.0f
            && Stamina >= SprintRecoveryThreshold)
        {
            _sprintRecoveryRequired = false;
        }
    }

    private void UpdateCameraAndWeapon(float delta)
    {
        if (_isClimbingLadder)
        {
            UpdateLadderViewAnimation(delta);
            return;
        }
        if (_isVaulting)
        {
            UpdateVaultViewAnimation(delta);
            return;
        }
        UpdateDamageKick(delta);
        _recoilPitch = Mathf.Lerp(_recoilPitch, 0.0f, SmoothFactor(11.0f, delta));
        _recoilSide = Mathf.Lerp(_recoilSide, 0.0f, SmoothFactor(13.0f, delta));
        UpdateViewmodelShotImpulse(delta);
        var leanInput = Input.GetActionStrength(GameInputActions.LeanRight)
            - Input.GetActionStrength(GameInputActions.LeanLeft);
        _leanValue = Mathf.Lerp(_leanValue, _slideTime <= 0.0f ? leanInput : 0.0f, SmoothFactor(9.0f, delta));
        _head.Rotation = new Vector3(
            _pitch + _recoilPitch + _damageKickPitch,
            _recoilSide * 0.32f,
            _recoilSide * 0.24f + _leanValue * 0.13f + _damageKickRoll);

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        var walking = IsOnFloor() && horizontalSpeed > 0.5f;
        if (walking)
        {
            _bobTime += delta * horizontalSpeed * 1.45f;
        }
        var stanceBob = _stance switch
        {
            PlayerStance.Prone => 0.22f,
            PlayerStance.Crouched => 0.55f,
            _ => 1.0f
        };
        var bobStrength = walking ? Mathf.Clamp(horizontalSpeed / SprintSpeed, 0.0f, 1.0f) * stanceBob : 0.0f;
        var targetBobOffset = new Vector3(
            Mathf.Sin(_bobTime * 0.9f) * 0.021f,
            Mathf.Abs(Mathf.Cos(_bobTime * 1.8f)) * 0.024f,
            0.0f) * bobStrength;
        _smoothedBobOffset = _smoothedBobOffset.Lerp(targetBobOffset, SmoothFactor(walking ? 14.0f : 18.0f, delta));
        _stairViewOffsetY = Mathf.MoveToward(_stairViewOffsetY, 0.0f, delta * 1.7f);
        _camera.Position = _smoothedBobOffset
            + Vector3.Up * _stairViewOffsetY
            + new Vector3(_leanValue * 0.17f, _slideTime > 0.0f ? -0.08f : 0.0f, 0.0f)
            + _damageKickOffset;

        var targetFov = _isAiming ? AimFieldOfView() : _slideTime > 0.0f ? 84.0f : horizontalSpeed > 7.0f ? 82.0f : 76.0f;
        var handling = EquippedWeapon.Stats().Handling;
        _camera.Fov = Mathf.Lerp(_camera.Fov, targetFov, SmoothFactor(6.5f + handling * 5.0f, delta));
        _weaponRoot.Visible = IsFirearmQuickSlotSelected
            && !_isPlating
            && !RoleActionBlocksWeapon
            && !MedicalActionBlocksWeapon;
        _knifeRoot.Visible = _activeQuickSlot == PlayerQuickSlot.Melee
            && !_isPlating
            && !RoleActionBlocksWeapon
            && !MedicalActionBlocksWeapon;
        UpdateHeldThrowableVisual();
        UpdateKnifeAnimation(delta);
        var targetPosition = WeaponViewPositionTarget();
        _weaponRoot.Position = _weaponRoot.Position.Lerp(targetPosition, SmoothFactor(_isAiming ? 7.5f + handling * 6.0f : 6.0f + handling * 3.0f, delta));
        var weaponRotation = _weaponRoot.Rotation;
        if (_isAiming)
        {
            // Vault and ladder poses can carry a temporary yaw; ADS must begin on the optic axis.
            weaponRotation.Y = 0.0f;
        }
        _weaponRoot.Rotation = weaponRotation.Lerp(WeaponViewRotationTarget(), SmoothFactor(9.0f, delta));
        ApplyProceduralHandPose();
        _opticReticle.Visible = _isAiming && IsFirearmQuickSlotSelected;
        UpdateReloadAnimation();
        SyncAuthoredPrimaryWeapon();
        UpdateAuthoredM4ReloadSupportArm();
    }

    private float AimFieldOfView()
    {
        if (WeaponCatalog.IsSidearm(EquippedWeapon.Platform))
        {
            return 72.0f;
        }
        if (!EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var opticId))
        {
            return 55.0f;
        }
        return opticId switch
        {
            "optic_scope" => 29.0f,
            "optic_7x" => 19.0f,
            "optic_sniper" => 17.0f,
            "optic_holo" => 44.0f,
            _ => 49.0f
        };
    }

    public float CurrentAimFieldOfView => AimFieldOfView();

    public void SelectWeapon(int slot)
    {
        SelectQuickSlot((PlayerQuickSlot)Mathf.Clamp(slot, 0, 5));
    }

    public void CycleWeapon()
    {
        CycleWeaponSlots();
    }

    private void SwitchWeapon(bool useKnife)
    {
        ActivateWeaponSlot(useKnife ? PlayerWeaponSlot.Melee : PlayerWeaponSlot.Primary, true);
    }

    public void Fire()
    {
        if (_fireCooldown > 0.0f || _isReloading || _isPlating || RoleActionBlocksWeapon || MedicalActionBlocksWeapon)
        {
            return;
        }
        if (!HasActiveFirearm)
        {
            return;
        }
        if (Ammo <= 0)
        {
            StartReload();
            return;
        }

        Ammo--;
        var stats = EquippedWeapon.Stats();
        _fireCooldown = stats.FireInterval * RoleFireIntervalMultiplier;
        Main?.ReportGunshot(GlobalPosition, stats.SoundRadius);
        Main?.NotifyAircraftOperatorAttack(this, GlobalPosition, stats.SoundRadius);
        PlayLocalWeaponReport();
        var shotImpact = Mathf.Sqrt(Mathf.Max(0.25f, stats.Recoil));
        var suppressedFlash = SoundLab.IsSuppressed(EquippedWeapon) ? 0.48f : 1.0f;
        _muzzleFlash.LightEnergy = Mathf.Lerp(
            9.5f,
            15.0f,
            Mathf.Clamp((shotImpact - 0.7f) / 1.2f, 0.0f, 1.0f))
            * suppressedFlash;
        _muzzleBloom.Visible = true;
        _muzzleBloom.Scale = Vector3.One
            * _rng.RandfRange(1.02f, 1.32f)
            * Mathf.Lerp(1.0f, 1.44f, Mathf.Clamp(shotImpact - 0.65f, 0.0f, 1.0f))
            * Mathf.Lerp(0.72f, 1.0f, suppressedFlash);
        var bloomRotation = _muzzleBloom.Rotation;
        bloomRotation.Z = _rng.RandfRange(0.0f, Mathf.Tau);
        _muzzleBloom.Rotation = bloomRotation;
        var flashTween = CreateTween();
        var flashDuration = Mathf.Lerp(
            0.038f,
            0.055f,
            Mathf.Clamp(shotImpact - 0.7f, 0.0f, 1.0f));
        flashTween.TweenProperty(_muzzleFlash, "light_energy", 0.0f, flashDuration);
        flashTween.Parallel().TweenProperty(
            _muzzleBloom,
            "scale",
            Vector3.One * 0.15f,
            flashDuration + 0.018f);
        flashTween.TweenCallback(Callable.From(() => _muzzleBloom.Visible = false));

        var shellVelocity = _camera.GlobalBasis.X * 3.0f + Vector3.Up * 1.25f - _camera.GlobalBasis.Z * 0.45f;
        Main?.SpawnShell(_ejectMarker.GlobalPosition, shellVelocity);

        // Sprint or vehicle motion degrades accuracy instead of blocking the trigger.
        var movingPenalty = Mathf.Clamp(
            (new Vector2(Velocity.X, Velocity.Z).Length() + PlatformMotionSpeed()) / SprintSpeed,
            0.0f,
            1.6f);
        var stanceAccuracy = _stance switch
        {
            PlayerStance.Prone => 0.52f,
            PlayerStance.Crouched => 0.76f,
            _ => 1.0f
        };
        var spread = ((_isAiming ? 0.0015f : 0.0065f) + movingPenalty * 0.009f) * stanceAccuracy;
        var direction = -_camera.GlobalBasis.Z;
        direction += _camera.GlobalBasis.X * _rng.RandfRange(-spread, spread);
        direction += _camera.GlobalBasis.Y * _rng.RandfRange(-spread, spread);
        direction = direction.Normalized();

        var from = _camera.GlobalPosition;
        var maximumRange = stats.EffectiveRange * 1.35f;
        var to = from + direction * maximumRange;
        var glassBlocked = BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            from,
            to,
            stats.Damage * AmmoTiers.DamageMultiplier(CurrentAmmoGrade),
            direction,
            out var glassHitPosition);
        if (glassBlocked)
        {
            PlayLocalGlassBreak();
        }
        var hit = default(PhysicsRaycastHit);
        var hasHit = !glassBlocked && PhysicsRaycast.TryHit(
            GetWorld3D(),
            from,
            to,
            GetRid(),
            uint.MaxValue,
            out hit);
        var end = glassBlocked ? glassHitPosition : to;
        var damagedTarget = false;
        var headshot = false;
        var networkEnemyId = -1;
        var networkDamage = 0.0f;
        if (hasHit)
        {
            end = hit.Position;
            var target = hit.Collider;
            var killed = false;
            if (target is EnemyOperator enemy)
            {
                damagedTarget = true;
                var distance = from.DistanceTo(end);
                var falloff = Mathf.Lerp(1.0f, 0.52f, Mathf.Clamp(distance / maximumRange, 0.0f, 1.0f));
                networkEnemyId = enemy.NetworkId;
                networkDamage = stats.Damage
                    * falloff
                    * _rng.RandfRange(0.94f, 1.06f)
                    * AmmoTiers.DamageMultiplier(CurrentAmmoGrade);
                killed = enemy.TakeDamage(
                    networkDamage,
                    end,
                    this,
                    AmmoTiers.ArmorPenetration(CurrentAmmoGrade));
                headshot = enemy.LastHitWasHeadshot;
                EmitSignal(SignalName.HitConfirmed, killed, headshot, enemy.LastHitWasArmored);
            }
            else if (target is CivilianNpc civilian)
            {
                damagedTarget = true;
                var distance = from.DistanceTo(end);
                var falloff = Mathf.Lerp(1.0f, 0.55f, Mathf.Clamp(distance / maximumRange, 0.0f, 1.0f));
                killed = civilian.TakeDamage(stats.Damage * falloff * _rng.RandfRange(0.9f, 1.05f), end, this);
                EmitSignal(SignalName.HitConfirmed, killed, false, false);
            }
            else if (target is ExplosiveBarrel barrel)
            {
                damagedTarget = true;
                barrel.TakeDamage(stats.Damage * _rng.RandfRange(0.94f, 1.06f), end, this);
                EmitSignal(SignalName.HitConfirmed, false, false, false);
            }
            else if (target is DriveableVehicle vehicle)
            {
                damagedTarget = true;
                var destroyed = vehicle.TakeDamage(stats.Damage * _rng.RandfRange(0.9f, 1.1f), end, this);
                EmitSignal(SignalName.HitConfirmed, destroyed, false, false);
            }
            else if (target is DestructibleAircraft aircraft)
            {
                damagedTarget = true;
                var destroyed = aircraft.TakeDamage(stats.Damage * _rng.RandfRange(1.05f, 1.2f), end, this);
                EmitSignal(SignalName.HitConfirmed, destroyed, false, false);
            }
            else if (target is AircraftShell shell)
            {
                damagedTarget = true;
                // Slightly generous intercept damage so rifle fire can break shells in the air.
                var destroyed = shell.TakeDamage(stats.Damage * _rng.RandfRange(1.15f, 1.35f), end, this);
                EmitSignal(SignalName.HitConfirmed, destroyed, false, false);
            }
            Main?.SpawnImpact(end, hit.Normal);
        }

        Main?.SpawnTracer(_muzzle.GlobalPosition, end, new Color(1.0f, 0.67f, 0.24f));
        Main?.OnLocalPlayerShot(_muzzle.GlobalPosition, end, networkEnemyId, networkDamage);
        Main?.RecordShot(damagedTarget, headshot);
        var stanceRecoil = _stance switch
        {
            PlayerStance.Prone => 0.62f,
            PlayerStance.Crouched => 0.82f,
            _ => 1.0f
        };
        var verticalRecoil = _rng.RandfRange(0.014f, 0.024f)
            * stats.Recoil
            * (_isAiming ? 0.58f : 1.0f)
            * stanceRecoil
            * RoleRecoilMultiplier;
        var horizontalRecoil = _rng.RandfRange(-0.021f, 0.021f)
            * stats.Recoil
            * stanceRecoil
            * RoleRecoilMultiplier;
        _recoilPitch -= verticalRecoil;
        _recoilSide = Mathf.Clamp(_recoilSide + horizontalRecoil, -0.095f, 0.095f);
        ApplyViewmodelShotImpulse(stats.Recoil, stanceRecoil);
        Hud?.PulseCrosshair(shotImpact, horizontalRecoil);
    }

    private void PlayLocalGlassBreak()
    {
        if (!IsInstanceValid(_glassBreakAudio))
        {
            return;
        }
        _glassBreakAudio.Stop();
        _glassBreakAudio.PitchScale = _rng.RandfRange(0.96f, 1.04f);
        _glassBreakAudio.Play();
    }

    private void StartReload()
    {
        var magazineSize = EquippedWeapon.Stats().MagazineSize;
        if (_isReloading || _knifeEquipped || Ammo >= magazineSize || ReserveAmmo <= 0 || IsDead)
        {
            return;
        }
        _isReloading = true;
        _activeReloadDuration = ReloadDuration * RoleReloadMultiplier;
        _reloadTime = _activeReloadDuration;
        _reloadSoundStage = 0;
    }

    private void FinishReload()
    {
        var grade = Ammo > 0 && AmmoReserveFor(CurrentAmmoCaliber, _loadedAmmoGrade) > 0
            ? _loadedAmmoGrade
            : BestAmmoGrade(CurrentAmmoCaliber);
        var amount = Mathf.Min(
            EquippedWeapon.Stats().MagazineSize - Ammo,
            AmmoReserveFor(CurrentAmmoCaliber, grade));
        Ammo += amount;
        ConsumeAmmoReserve(CurrentAmmoCaliber, grade, amount);
        _loadedAmmoGrade = grade;
        _isReloading = false;
        ResetReloadRig();
        Hud?.SetAmmoTier(CurrentAmmoGrade);
    }

    private void UpdateReloadAnimation()
    {
        if (!_isReloading)
        {
            return;
        }
        if (EquippedWeapon.Platform == WeaponPlatform.M4A1)
        {
            UpdateM4ReloadAnimation();
            return;
        }

        // The coordinates below are authored for the M4 magazine geometry only.
        // Other platforms retain their established reload presentation until
        // they receive a platform-specific authored clip.
        ApplyProceduralHandPose();
    }

    private void UpdateM4ReloadAnimation()
    {
        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        var magazineHome = new Vector3(0, -0.2f, -0.31f);
        var magazineRotation = new Vector3(-0.19f, 0, 0);
        var handHome = new Vector3(-0.03f, -0.2f, -0.58f);
        var handAtWell = magazineHome + M4ReloadMagazineGripOffset;
        var droppedMagazine = new Vector3(-0.1f, -0.43f, -0.38f);
        var sparePickup = new Vector3(-0.2f, -0.42f, -0.42f);
        var spareReady = new Vector3(-0.14f, -0.32f, -0.36f);
        var removedMagazineGrip = droppedMagazine + M4ReloadMagazineGripOffset;

        // Establish the complete mechanism state on every update. Besides
        // making fixed-progress diagnostics deterministic, this avoids a
        // one-frame stale magazine when a reload pose is restored or sampled.
        _magazine.Visible = progress < 0.43f || progress >= 0.78f;
        _magazine.Position = progress is >= 0.43f and < 0.78f
            ? droppedMagazine
            : magazineHome;
        _magazine.Rotation = progress is >= 0.43f and < 0.78f
            ? new Vector3(0.62f, 0.08f, 0.36f)
            : magazineRotation;
        _spareMagazine.Visible = progress is >= 0.43f and < 0.78f;
        _spareMagazine.Position = progress >= 0.78f
            ? magazineHome
            : new Vector3(-0.3f, -0.62f, -0.18f);
        _spareMagazine.Rotation = progress >= 0.78f
            ? magazineRotation
            : new Vector3(0.35f, 0, 0.35f);
        _chargingHandle.Position = new Vector3(0.075f, 0.085f, -0.05f);
        _supportHand.Position = handHome;
        _supportHand.Rotation = new Vector3(0.2f, 0, 0.05f);

        if (progress < 0.18f)
        {
            var t = SmoothStep(progress / 0.18f);
            _supportHand.Position = handHome.Lerp(handAtWell, t);
            _supportHand.Rotation = new Vector3(0.2f, 0, 0.05f)
                .Lerp(new Vector3(0.42f, 0.08f, 0.22f), t);
        }
        else if (progress < 0.43f)
        {
            var t = SmoothStep((progress - 0.18f) / 0.25f);
            _magazine.Position = magazineHome.Lerp(droppedMagazine, t);
            _magazine.Rotation = magazineRotation.Lerp(new Vector3(0.62f, 0.08f, 0.36f), t);
            _supportHand.Position = _magazine.Position + M4ReloadMagazineGripOffset;
            _supportHand.Rotation = new Vector3(0.42f, 0.08f, 0.22f);
            if (_reloadSoundStage == 0 && progress > 0.3f)
            {
                _reloadSoundStage = 1;
                _reloadAudio.PitchScale = 0.9f;
                _reloadAudio.Play();
            }
        }
        else if (progress < 0.55f)
        {
            var t = SmoothStep((progress - 0.43f) / 0.12f);
            _spareMagazine.Position = sparePickup.Lerp(spareReady, t);
            var spareMagazineGrip = _spareMagazine.Position + M4ReloadMagazineGripOffset;
            _supportHand.Position = removedMagazineGrip.Lerp(spareMagazineGrip, t);
            _supportHand.Rotation = new Vector3(0.42f, 0.08f, 0.22f);
        }
        else if (progress < 0.78f)
        {
            var t = SmoothStep((progress - 0.55f) / 0.23f);
            _spareMagazine.Position = spareReady.Lerp(magazineHome, t);
            _spareMagazine.Rotation = new Vector3(0.35f, 0, 0.35f).Lerp(magazineRotation, t);
            _supportHand.Position = _spareMagazine.Position + M4ReloadMagazineGripOffset;
            _supportHand.Rotation = new Vector3(0.42f, 0.08f, 0.22f);
        }
        else if (progress < 0.9f)
        {
            var t = SmoothStep((progress - 0.78f) / 0.12f);
            var handleGrip = new Vector3(0.0f, 0.02f, -0.02f);
            _supportHand.Position = handAtWell.Lerp(handleGrip, t);
            _supportHand.Rotation = new Vector3(0.42f, 0.08f, 0.22f);
            _chargingHandle.Position = new Vector3(0.075f, 0.085f, -0.05f).Lerp(new Vector3(0.075f, 0.085f, 0.08f), t);
            if (_reloadSoundStage == 1)
            {
                _reloadSoundStage = 2;
                _reloadAudio.PitchScale = 1.08f;
                _reloadAudio.Play();
            }
        }
        else
        {
            var t = SmoothStep((progress - 0.9f) / 0.1f);
            _chargingHandle.Position = new Vector3(0.075f, 0.085f, 0.08f).Lerp(new Vector3(0.075f, 0.085f, -0.05f), t);
            _supportHand.Position = new Vector3(0.0f, 0.02f, -0.02f).Lerp(handHome, t);
            _supportHand.Rotation = new Vector3(0.42f, 0.08f, 0.22f)
                .Lerp(new Vector3(0.2f, 0, 0.05f), t);
        }

        _supportForearm.Position = _supportHand.Position + new Vector3(-0.09f, -0.24f, 0.1f);
        _supportForearm.Rotation = new Vector3(0.22f, 0.05f, -0.28f);
    }

    private void ResetReloadRig()
    {
        _magazine.Visible = true;
        _magazine.Position = new Vector3(0, -0.2f, -0.31f);
        _magazine.Rotation = EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Vector3(-0.29f, 0, 0)
            : new Vector3(-0.19f, 0, 0);
        _spareMagazine.Visible = false;
        _spareMagazine.Position = new Vector3(-0.3f, -0.62f, -0.18f);
        _spareMagazine.Rotation = new Vector3(0.35f, 0, 0.35f);
        _supportHand.Position = new Vector3(-0.03f, -0.2f, -0.58f);
        _supportHand.Rotation = new Vector3(0.2f, 0, 0.05f);
        _supportForearm.Position = new Vector3(-0.12f, -0.42f, -0.47f);
        _supportForearm.Rotation = new Vector3(0.25f, 0, -0.26f);
        _chargingHandle.Position = new Vector3(0.075f, 0.085f, -0.05f);
        if (WeaponCatalog.IsSidearm(EquippedWeapon.Platform))
        {
            _supportHand.Position = SidearmSupportHandHome;
            _supportHand.Rotation = SidearmSupportHandRestRotation();
            _supportForearm.Position = _supportHand.Position + new Vector3(-0.08f, -0.23f, 0.10f);
            _supportForearm.Rotation = new Vector3(0.18f, 0.06f, -0.24f);
        }
        SyncAuthoredPrimaryWeapon();
        ResetAuthoredM4ReloadSupportArm();
    }

    internal bool SetM4ReloadPoseForDiagnostics(float progress)
    {
        if (EquippedWeapon.Platform != WeaponPlatform.M4A1)
        {
            return false;
        }

        _isReloading = true;
        _activeReloadDuration = ReloadDuration * RoleReloadMultiplier;
        _reloadTime = _activeReloadDuration
            * (1.0f - Mathf.Clamp(progress, 0.0f, 1.0f));
        ApplyProceduralHandPose();
        UpdateReloadAnimation();
        SyncAuthoredPrimaryWeapon();
        UpdateAuthoredM4ReloadSupportArm();
        return true;
    }

    internal void ClearM4ReloadPoseForDiagnostics()
    {
        _isReloading = false;
        _reloadTime = 0.0f;
        ResetReloadRig();
    }

    private static float SmoothStep(float value)
    {
        var t = Mathf.Clamp(value, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private bool StartPlate(string preferredItemId = "")
    {
        if (_isPlating
            || _isReloading
            || MedicalActionBlocksWeapon
            || RoleActionBlocksWeapon
            || IsInVehicle
            || IsExtractionPassenger
            || UiLocked
            || ArmorPlates <= 0
            || Armor >= 99.0f
            || IsDead)
        {
            return false;
        }
        var plateIndex = !string.IsNullOrEmpty(preferredItemId)
            ? Backpack.FindIndex(item => item.Id == preferredItemId && item.Kind == LootItemKind.ArmorPlate && item.Quantity > 0)
            : -1;
        if (plateIndex < 0)
        {
            for (var index = 0; index < Backpack.Count; index++)
            {
                var item = Backpack[index];
                if (item.Kind != LootItemKind.ArmorPlate || item.Quantity <= 0)
                {
                    continue;
                }
                if (plateIndex < 0 || item.Grade < Backpack[plateIndex].Grade)
                {
                    plateIndex = index;
                }
            }
        }
        if (plateIndex < 0)
        {
            return false;
        }
        var plate = Backpack[plateIndex];
        _isAiming = false;
        _slideTime = 0.0f;
        _isPlating = true;
        _plateDuration = Mathf.Max(1.65f, 2.55f - (int)plate.Grade * 0.16f);
        _plateTime = _plateDuration;
        _plateRepairFraction = ArmorPlateSupplies.RepairFraction(plate.Grade);
        _plateItemId = plate.Id;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentActionLocalized("applying_armor_cancel", "APPLYING ARMOR  //  X CANCEL", 0.0f, true);
        return true;
    }

    private void UpdatePlate(float delta)
    {
        if (!_isPlating)
        {
            return;
        }
        _plateTime -= delta;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentActionLocalized("applying_armor_cancel", "APPLYING ARMOR  //  X CANCEL", 1.0f - _plateTime / Mathf.Max(0.01f, _plateDuration), true);
        if (_plateTime > 0.0f)
        {
            return;
        }
        var plateIndex = Backpack.FindIndex(item => item.Id == _plateItemId && item.Kind == LootItemKind.ArmorPlate && item.Quantity > 0);
        if (plateIndex < 0)
        {
            CancelPlate();
            return;
        }
        var armorDefinition = EquippedBodyArmor.Definition;
        EquippedBodyArmor.Durability = Mathf.Min(
            armorDefinition.MaxDurability,
            EquippedBodyArmor.Durability + armorDefinition.MaxDurability * _plateRepairFraction);
        var plate = Backpack[plateIndex];
        plate.Quantity--;
        if (plate.Quantity <= 0)
        {
            Backpack.RemoveAt(plateIndex);
        }
        _isPlating = false;
        _plateTime = 0.0f;
        _plateDuration = 0.0f;
        _plateItemId = string.Empty;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentAction(string.Empty, 0.0f, false);
        Hud?.SetBackpackValuePlayer(this);
        Hud?.SetMedicalInventory(this);
        Hud?.ShowLocalizedMessage("armor_secured", "ARMOR PLATE SECURED", new Color(0.4f, 0.76f, 1.0f));
    }

    private void CancelPlate(bool notify = false)
    {
        if (!_isPlating)
        {
            return;
        }
        _isPlating = false;
        _plateTime = 0.0f;
        _plateDuration = 0.0f;
        _plateItemId = string.Empty;
        SetMedicalDeviceVisibility();
        Hud?.SetEquipmentAction(string.Empty, 0.0f, false);
        if (notify)
        {
            Hud?.ShowLocalizedMessage("armor_cancelled", "ARMOR APPLICATION CANCELLED", new Color(1.0f, 0.58f, 0.3f));
        }
    }

    public int AmmoReserveFor(AmmoCaliber caliber)
    {
        var total = 0;
        for (var tier = (int)LootGrade.Common; tier <= (int)LootGrade.Legendary; tier++)
        {
            total += AmmoReserveFor(caliber, (LootGrade)tier);
        }
        return total;
    }

    private void SetAmmoReserve(AmmoCaliber caliber, int amount)
    {
        for (var tier = (int)LootGrade.Common; tier <= (int)LootGrade.Legendary; tier++)
        {
            SetAmmoReserve(caliber, (LootGrade)tier, 0);
        }
        SetAmmoReserve(caliber, LootGrade.Common, amount);
    }

    public bool TryCollectAmmo(int amount) => TryCollectAmmo(CurrentAmmoCaliber, amount, LootGrade.Common);

    public bool TryCollectAmmo(AmmoCaliber caliber, int amount, LootGrade grade = LootGrade.Common)
    {
        var maxReserveAmmo = MaximumAmmoReserve(caliber);
        var current = AmmoReserveFor(caliber);
        if (current >= maxReserveAmmo || !CanAddAmmoStack(caliber, grade))
        {
            return false;
        }
        var accepted = Mathf.Min(maxReserveAmmo - current, Mathf.Max(0, amount));
        SetAmmoReserve(caliber, grade, AmmoReserveFor(caliber, grade) + accepted);
        Hud?.ShowLocalizedMessage("ammo_recovered", "AMMUNITION RECOVERED", new Color(0.42f, 0.9f, 0.64f));
        return true;
    }

    public bool TryCollectArmorPlate(LootGrade grade = LootGrade.Uncommon, int quantity = 1)
    {
        if (!TryStoreArmorPlate(grade, quantity))
        {
            return false;
        }
        Hud?.ShowLocalizedMessage("armor_recovered", "SPARE ARMOR RECOVERED", new Color(0.42f, 0.72f, 1.0f));
        Hud?.SetBackpackValuePlayer(this);
        Hud?.SetMedicalInventory(this);
        return true;
    }

    public bool TryStoreInBackpack(LootItem item)
    {
        if (item.Kind == LootItemKind.Ammunition)
        {
            return TryStoreAmmoStack(item);
        }
        if (item.Kind == LootItemKind.ArmorPlate)
        {
            return TryCollectArmorPlate(item.Grade, Mathf.Max(1, item.Quantity));
        }
        if (item.Kind == LootItemKind.Medical)
        {
            var existing = Backpack.Find(candidate => candidate.Kind == LootItemKind.Medical
                && candidate.MedicalKind == item.MedicalKind
                && candidate.Grade == item.Grade);
            if (existing is not null)
            {
                existing.Quantity += Mathf.Max(1, item.Quantity);
                Hud?.ShowLocalizedMessage("medical_recovered", "MEDICAL SUPPLIES RECOVERED", MedicalItems.Definition(item.MedicalKind).Accent);
                Hud?.SetBackpackValuePlayer(this);
                Hud?.SetMedicalInventory(this);
                return true;
            }
        }
        if (item.Kind == LootItemKind.Valuable)
        {
            var existing = Backpack.Find(candidate => candidate.Kind == LootItemKind.Valuable
                && candidate.ValuableKind == item.ValuableKind
                && candidate.Grade == item.Grade);
            if (existing is not null)
            {
                existing.Quantity += Mathf.Max(1, item.Quantity);
                Hud?.ShowLocalizedMessage("valuable_recovered", "VALUABLE SECURED", LootGrades.GlowColor(item.Grade));
                Hud?.SetBackpackValuePlayer(this);
                return true;
            }
        }
        if (Backpack.Count >= BackpackCapacity)
        {
            Hud?.ShowLocalizedMessage("backpack_full", "BACKPACK FULL", new Color(1.0f, 0.48f, 0.28f));
            return false;
        }
        Backpack.Add(item);
        Hud?.ShowLocalizedMessage(
            item.Kind == LootItemKind.Medical ? "medical_recovered" : "item_stored",
            item.Kind == LootItemKind.Medical ? "MEDICAL SUPPLIES RECOVERED" : "ITEM STORED",
            item.Kind == LootItemKind.Medical ? MedicalItems.Definition(item.MedicalKind).Accent : new Color(0.42f, 0.9f, 0.68f));
        Hud?.SetBackpackValuePlayer(this);
        Hud?.SetMedicalInventory(this);
        return true;
    }

    public LootItem? EquipFromLoot(LootItem item)
    {
        if (item.Kind == LootItemKind.Weapon && item.Weapon is not null)
        {
            return EquipLootWeapon(item.Weapon, item.Grade);
        }
        if (item.Kind == LootItemKind.Attachment)
        {
            var attachment = WeaponCatalog.Attachment(item.AttachmentId);
            if (!WeaponCatalog.CanEquipAttachment(EquippedWeapon.Platform, attachment.Id))
            {
                ShowIncompatibleAttachmentMessage();
                return item;
            }
            LootItem? previous = null;
            if (EquippedWeapon.Attachments.TryGetValue(attachment.Slot, out var previousId))
            {
                previous = new LootItem
                {
                    Kind = LootItemKind.Attachment,
                    AttachmentId = previousId,
                    Grade = EquippedAttachmentGrade(attachment.Slot)
                };
            }
            EquippedWeapon.Attachments[attachment.Slot] = attachment.Id;
            _equippedAttachmentGrades[attachment.Slot] = item.Grade;
            ApplyWeaponBuildVisuals();
            Hud?.ShowLocalizedMessage("part_installed", "WEAPON PART INSTALLED", new Color(0.42f, 0.9f, 0.72f));
            return previous;
        }
        if (item.Kind == LootItemKind.Ammunition && TryCollectAmmo(item.AmmoCaliber, item.Quantity, item.Grade))
        {
            return null;
        }
        if (item.Kind == LootItemKind.KnifeSkin && !string.IsNullOrEmpty(item.KnifeSkinId))
        {
            if (string.Equals(item.KnifeSkinId, EquippedKnifeSkinId, System.StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
            var previousSkin = EquippedKnifeSkinId;
            var previousGrade = EquippedKnifeGrade;
            EquippedKnifeSkinId = item.KnifeSkinId;
            EquippedKnifeGrade = item.Grade;
            RebuildKnife();
            Hud?.ShowLocalizedMessage("knife_skin_equipped", "MELEE WEAPON EQUIPPED", new Color(0.88f, 0.72f, 0.34f));
            return new LootItem { Kind = LootItemKind.KnifeSkin, KnifeSkinId = previousSkin, Grade = previousGrade };
        }
        if (item.Kind == LootItemKind.ArmorPlate && TryCollectArmorPlate(item.Grade, item.Quantity))
        {
            return null;
        }
        if (item.Kind == LootItemKind.Equipment && item.Equipment is not null)
        {
            var incoming = item.Equipment;
            if (incoming.Definition.Slot == EquipmentSlot.Backpack
                && Backpack.Count > 6 + incoming.Definition.CapacityBonus)
            {
                Hud?.ShowLocalizedMessage("pack_too_small", "MOVE ITEMS BEFORE EQUIPPING THIS BACKPACK", new Color(1.0f, 0.48f, 0.28f));
                return item;
            }
            var previous = incoming.Definition.Slot switch
            {
                EquipmentSlot.Helmet => EquippedHelmet,
                EquipmentSlot.BodyArmor => EquippedBodyArmor,
                EquipmentSlot.Backpack => EquippedBackpack,
                _ => null
            };
            var previousGrade = EquippedEquipmentGrade(incoming.Definition.Slot);
            switch (incoming.Definition.Slot)
            {
                case EquipmentSlot.Helmet:
                    EquippedHelmet = incoming.Clone();
                    EquippedHelmetGrade = item.Grade;
                    if (incoming.DefinitionId == "helmet_nvg")
                    {
                        _nvgOn = true;
                        Main?.SetNightVisionActive(true);
                        Hud?.SetNightVisionActive(true);
                        Hud?.ShowLocalizedMessage("nvg_auto_on", "NVG AUTO ON // PRESS N TO TOGGLE", new Color(0.42f, 0.95f, 0.42f));
                    }
                    if (!HasNightVisionHelmet && _nvgOn)
                    {
                        _nvgOn = false;
                        Main?.SetNightVisionActive(false);
                        Hud?.SetNightVisionActive(false);
                        Hud?.ShowLocalizedMessage("nvg_off", "NVG  //  OFF  //  N", new Color(0.55f, 0.65f, 0.55f));
                    }
                    break;
                case EquipmentSlot.BodyArmor:
                    EquippedBodyArmor = incoming.Clone();
                    EquippedBodyArmorGrade = item.Grade;
                    break;
                case EquipmentSlot.Backpack:
                    EquippedBackpack = incoming.Clone();
                    EquippedBackpackGrade = item.Grade;
                    break;
            }
            Hud?.ShowLocalizedMessage("equipment_replaced", "EQUIPMENT REPLACED", new Color(0.84f, 0.7f, 0.34f));
            return previous is null
                ? null
                : new LootItem
                {
                    Kind = LootItemKind.Equipment,
                    Equipment = previous.Clone(),
                    Grade = previousGrade
                };
        }
        return item;
    }

    private void ShowIncompatibleAttachmentMessage()
    {
        Hud?.ShowLocalizedMessage(
            "part_incompatible",
            "PART INCOMPATIBLE WITH THIS WEAPON",
            new Color(1.0f, 0.48f, 0.28f));
    }

    public LootItem? EquipFromLootToWeaponSlot(LootItem item, PlayerWeaponSlot slot)
    {
        if (slot == PlayerWeaponSlot.Melee)
        {
            return item.Kind == LootItemKind.KnifeSkin
                ? EquipFromLoot(item)
                : item;
        }
        if (slot is not (PlayerWeaponSlot.Primary or PlayerWeaponSlot.Secondary or PlayerWeaponSlot.Sidearm))
        {
            return item;
        }
        if (item.Kind == LootItemKind.Weapon && item.Weapon is not null)
        {
            return WeaponFitsSlot(item.Weapon.Platform, slot)
                ? EquipLootWeapon(item.Weapon, item.Grade, slot)
                : item;
        }
        return item.Kind == LootItemKind.Attachment
            ? EquipAttachmentToWeaponSlot(item, slot)
            : item;
    }

    public bool UseBackpackItem(string itemId)
    {
        var index = Backpack.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return false;
        }
        var item = Backpack[index];
        if (item.Kind == LootItemKind.Ammunition)
        {
            Hud?.ShowLocalizedMessage(
                "ammo_linked",
                "AMMUNITION IS LINKED TO THE RESERVE",
                new Color(0.42f, 0.9f, 0.64f));
            return false;
        }
        if (item.Kind == LootItemKind.Medical)
        {
            return TryStartMedicalUse(item.MedicalKind);
        }
        if (item.Kind == LootItemKind.ArmorPlate)
        {
            return StartPlate(item.Id);
        }
        var replacement = EquipFromLoot(item);
        if (ReferenceEquals(replacement, item))
        {
            return false;
        }
        if (replacement is null)
        {
            Backpack.RemoveAt(index);
        }
        else
        {
            Backpack[index] = replacement;
        }
        return true;
    }

    public bool UseBackpackItemInWeaponSlot(string itemId, PlayerWeaponSlot slot)
    {
        var index = Backpack.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return false;
        }
        var item = Backpack[index];
        var replacement = EquipFromLootToWeaponSlot(item, slot);
        if (ReferenceEquals(replacement, item))
        {
            return false;
        }
        if (replacement is null)
        {
            Backpack.RemoveAt(index);
        }
        else
        {
            Backpack[index] = replacement;
        }
        return true;
    }

    private bool TryStoreArmorPlate(LootGrade grade, int quantity)
    {
        quantity = Mathf.Max(1, quantity);
        if (ArmorPlates + quantity > MaxArmorPlates)
        {
            return false;
        }
        var existing = Backpack.Find(item => item.Kind == LootItemKind.ArmorPlate && item.Grade == grade);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return true;
        }
        if (Backpack.Count >= BackpackCapacity)
        {
            Hud?.ShowLocalizedMessage("backpack_full", "BACKPACK FULL", new Color(1.0f, 0.48f, 0.28f));
            return false;
        }
        Backpack.Add(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = quantity,
            Grade = grade
        });
        return true;
    }

    private void EquipPrimary(WeaponBuild build, LootGrade grade = LootGrade.Rare)
    {
        InstallPrimaryWeapon(build, grade);
        Hud?.ShowLocalizedMessage("weapon_equipped", "PRIMARY WEAPON EQUIPPED", new Color(0.4f, 0.86f, 0.7f));
    }

    public LootGrade EquippedAttachmentGrade(AttachmentSlot slot)
        => _equippedAttachmentGrades.TryGetValue(slot, out var grade) ? grade : EquippedWeaponGrade;

    public LootGrade EquippedEquipmentGrade(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Helmet => EquippedHelmetGrade,
        EquipmentSlot.BodyArmor => EquippedBodyArmorGrade,
        EquipmentSlot.Backpack => EquippedBackpackGrade,
        _ => LootGrade.Common
    };

    private void ResetEquippedEquipmentGrades()
    {
        EquippedHelmetGrade = DefaultEquipmentGrade(EquippedHelmet);
        EquippedBodyArmorGrade = DefaultEquipmentGrade(EquippedBodyArmor);
        EquippedBackpackGrade = DefaultEquipmentGrade(EquippedBackpack);
    }

    private static LootGrade DefaultEquipmentGrade(EquipmentItem equipment)
    {
        if (equipment.Definition.Id.Contains("patrol", System.StringComparison.OrdinalIgnoreCase)
            || equipment.Definition.Id.Contains("sling", System.StringComparison.OrdinalIgnoreCase))
        {
            return LootGrade.Common;
        }
        if (equipment.Definition.Id.Contains("heavy", System.StringComparison.OrdinalIgnoreCase))
        {
            return equipment.Definition.Slot == EquipmentSlot.BodyArmor ? LootGrade.Epic : LootGrade.Rare;
        }
        return equipment.Definition.Slot == EquipmentSlot.BodyArmor ? LootGrade.Rare : LootGrade.Uncommon;
    }

    private bool ThrowGrenade()
    {
        if (Grenades <= 0 || _isReloading || MedicalActionBlocksWeapon || IsDead || Main is null)
        {
            return false;
        }
        Grenades--;
        Main.ThrowGrenade(_camera.GlobalPosition - _camera.GlobalBasis.Z * 0.7f, -_camera.GlobalBasis.Z, this);
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
        OnThrowableConsumed();
        return true;
    }

    public Vector3 HitPoint(HitRegion region)
    {
        var height = _stance switch
        {
            PlayerStance.Prone => 0.62f,
            PlayerStance.Crouched => 1.16f,
            _ => 1.57f
        };
        var y = region switch
        {
            HitRegion.Head => height,
            HitRegion.Torso => height * 0.65f,
            _ => Mathf.Max(0.22f, height * 0.27f)
        };
        return GlobalPosition + Vector3.Up * y;
    }

    public bool TakeDamage(float amount, Vector3 hitPosition = default, Node? attacker = null)
    {
        LastHitWasArmored = false;
        if (IsDead)
        {
            return true;
        }
        if (Main?.IsExtractionNetworkClient == true
            || Main?.IsDemolitionNetworkClient == true)
        {
            return false;
        }
        // Deployment protection only blocks enemy AI during the protected spawn window.
        // Headless combat checks and live fire still apply once the director leaves deployment.
        if (Main?.IsPlayerProtected() == true && attacker is EnemyOperator)
        {
            // Still allow damage once mission has left pure deployment (double-check phase via Main).
            // Keep protection only while director reports protected.
            return false;
        }

        Main?.InterruptLootForIncomingDamage();
        CancelPlate();
        CancelMedicalUse();

        var region = attacker is EnemyOperator ? ResolveHitRegion(hitPosition) : HitRegion.Torso;
        var adjustedDamage = region switch
        {
            HitRegion.Head => amount * 1.85f,
            HitRegion.Limbs => amount * 0.72f,
            _ => amount
        };
        // Crouched and prone profiles present a smaller target to incoming fire.
        adjustedDamage *= _stance switch
        {
            PlayerStance.Prone => 0.82f,
            PlayerStance.Crouched => 0.9f,
            _ => 1.0f
        };
        var protectiveGear = region switch
        {
            HitRegion.Head => EquippedHelmet,
            HitRegion.Torso => EquippedBodyArmor,
            _ => null
        };
        var armorHit = protectiveGear is not null && protectiveGear.Durability > 0.0f;
        LastHitWasArmored = armorHit;
        if (protectiveGear is not null)
        {
            adjustedDamage = ApplyProtection(protectiveGear, adjustedDamage);
        }
        var healthBefore = Health;
        Health -= adjustedDamage;
        var appliedDamage = Mathf.Min(healthBefore, Mathf.Max(0.0f, adjustedDamage));
        ApplyIncomingDamageFeedback(appliedDamage, region, armorHit, attacker, hitPosition);
        if (armorHit)
        {
            Hud?.ShowLocalizedMessage(
                region == HitRegion.Head ? "helmet_impact" : "armor_impact",
                region == HitRegion.Head ? "HELMET ABSORBED IMPACT" : "BODY ARMOR ABSORBED IMPACT",
                new Color(0.42f, 0.72f, 1.0f));
        }
        if (Health <= 0.0f)
        {
            Health = 0.0f;
            CloseMedicalWheelWithoutUse();
            CancelReload();
            IsDead = true;
            EjectFromVehicleIfAny();
            Velocity = Vector3.Zero;
            _stance = PlayerStance.Prone;
            // Keep mouse captured so the downed operator can crawl; mission fail unlocks it.
            UiLocked = false;
            Hud?.SetStats(0.0f, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
            EmitSignal(SignalName.Died);
            return true;
        }
        return false;
    }

    private HitRegion ResolveHitRegion(Vector3 hitPosition)
    {
        var height = _stance switch
        {
            PlayerStance.Prone => 0.62f,
            PlayerStance.Crouched => 1.16f,
            _ => 1.57f
        };
        var localHeight = hitPosition.Y - GlobalPosition.Y;
        if (localHeight >= height * 0.86f)
        {
            return HitRegion.Head;
        }
        return localHeight >= height * 0.4f ? HitRegion.Torso : HitRegion.Limbs;
    }

    private static float ApplyProtection(EquipmentItem equipment, float damage)
    {
        if (equipment.Durability <= 0.0f || equipment.Definition.Protection <= 0.0f)
        {
            return damage;
        }
        var durabilityRatio = equipment.Durability / equipment.Definition.MaxDurability;
        var effectiveProtection = equipment.Definition.Protection * Mathf.Lerp(0.55f, 1.0f, durabilityRatio);
        equipment.Durability = Mathf.Max(0.0f, equipment.Durability - damage * 0.58f);
        return damage * (1.0f - effectiveProtection);
    }
}
