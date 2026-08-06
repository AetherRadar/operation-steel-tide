using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

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
public partial class TacticalPlayer : CharacterBody3D
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

    public float Health { get; private set; } = 100.0f;
    public EquipmentItem EquippedHelmet { get; private set; } = EquipmentCatalog.Create("helmet_light");
    public EquipmentItem EquippedBodyArmor { get; private set; } = EquipmentCatalog.Create("armor_carrier");
    public EquipmentItem EquippedBackpack { get; private set; } = EquipmentCatalog.Create("pack_assault");
    public float Armor => EquippedBodyArmor.Definition.MaxDurability <= 0.0f
        ? 0.0f
        : EquippedBodyArmor.Durability / EquippedBodyArmor.Definition.MaxDurability * 100.0f;
    public float Stamina { get; private set; } = 100.0f;
    public int Ammo { get; private set; } = 30;
    public int ReserveAmmo { get; private set; } = 150;
    public int Grenades { get; private set; } = 2;
    public int ArmorPlates { get; private set; } = 2;
    public bool IsDead { get; set; }
    public bool HasMovementIntent { get; private set; }
    public bool IsAiming => _isAiming;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading ? 1.0f - _reloadTime / ReloadDuration : 0.0f;
    public bool FlashlightOn => _flashlightOn;
    public string FireMode => _automaticFire ? "AUTO" : "SEMI";
    public WeaponBuild EquippedWeapon { get; private set; } = WeaponCatalog.StarterWeapon();
    public List<LootItem> Backpack { get; } = new();
    public int BackpackCapacity => 6 + EquippedBackpack.Definition.CapacityBonus;
    public PlayerStance Stance => _stance;
    public bool IsCrouched => _stance == PlayerStance.Crouched;
    public bool IsProne => _stance == PlayerStance.Prone;
    public float LeanAmount => _leanValue;
    public float ViewHeight => IsInstanceValid(_head) ? _head.Position.Y : 0.0f;
    public bool KnifeEquipped => _knifeEquipped;
    public bool UiLocked { get; set; }
    public WeaponStats CurrentWeaponStats => EquippedWeapon.Stats();
    public float MouseSensitivity { get; set; } = 0.00165f;
    public FreightTerminalWorld? Main { get; set; }
    public CombatHUD? Hud { get; set; }

    private bool _isReloading;
    private bool _isAiming;
    private bool _isPlating;
    private bool _automaticFire = true;
    private bool _flashlightOn;
    private bool _knifeEquipped;
    private bool _knifeHitApplied;
    private bool _fireInputArmed;
    private bool _movementInputArmed;
    private float _fireReleaseTime;
    private float _movementReleaseTime;
    private float _fireCooldown;
    private float _reloadTime;
    private int _reloadSoundStage;
    private float _plateTime;
    private float _pitch;
    private float _recoilPitch;
    private float _recoilSide;
    private float _bobTime;
    private float _footstepTimer;
    private float _slideTime;
    private Vector3 _slideDirection;
    private float _leanValue;
    private float _knifeTime;
    private float _searchPose;
    private PlayerStance _stance = PlayerStance.Standing;

    private Node3D _head = null!;
    private Camera3D _camera = null!;
    private Node3D _weaponRoot = null!;
    private Node3D _knifeRoot = null!;
    private Marker3D _muzzle = null!;
    private OmniLight3D _muzzleFlash = null!;
    private MeshInstance3D _muzzleBloom = null!;
    private MeshInstance3D _opticReticle = null!;
    private SpotLight3D _weaponLight = null!;
    private MeshInstance3D _magazine = null!;
    private MeshInstance3D _spareMagazine = null!;
    private Node3D _supportHand = null!;
    private Node3D _supportForearm = null!;
    private MeshInstance3D _receiver = null!;
    private MeshInstance3D _handguard = null!;
    private MeshInstance3D _barrelPart = null!;
    private MeshInstance3D _muzzlePart = null!;
    private MeshInstance3D _foregrip = null!;
    private MeshInstance3D _stock = null!;
    private Node3D _opticRoot = null!;
    private Node3D _reflexSightModel = null!;
    private Node3D _holoSightModel = null!;
    private Node3D _scopeSightModel = null!;
    private MeshInstance3D _chargingHandle = null!;
    private Marker3D _ejectMarker = null!;
    private AudioStreamPlayer3D _gunAudio = null!;
    private AudioStreamPlayer _reloadAudio = null!;
    private AudioStreamPlayer3D _footstepAudio = null!;
    private CollisionShape3D _collider = null!;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        CollisionLayer = 1;
        CollisionMask = 1 | 2;
        BuildBody();
        BuildWeapon();
        BuildKnife();
        ApplyWeaponBuildVisuals();
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
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear
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

        var black = Material(new Color(0.075f, 0.083f, 0.079f), 0.64f, 0.38f);
        var polymer = Material(new Color(0.055f, 0.065f, 0.061f), 0.25f, 0.58f);
        var steel = Material(new Color(0.09f, 0.105f, 0.1f), 0.92f, 0.2f);
        var tan = Material(new Color(0.27f, 0.245f, 0.19f), 0.05f, 0.72f);
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

        _receiver = MeshPart(_weaponRoot, Box(new Vector3(0.13f, 0.15f, 0.46f)), Vector3.Zero, Vector3.Zero, black);
        _handguard = MeshPart(_weaponRoot, Box(new Vector3(0.15f, 0.12f, 0.4f)), new Vector3(0, 0.01f, -0.41f), Vector3.Zero, tan);
        _barrelPart = MeshPart(_weaponRoot, Cylinder(0.031f, 0.53f), new Vector3(0, 0.015f, -0.83f), new Vector3(Mathf.Pi / 2, 0, 0), steel);
        _muzzlePart = MeshPart(_weaponRoot, Cylinder(0.047f, 0.14f), new Vector3(0, 0.015f, -1.15f), new Vector3(Mathf.Pi / 2, 0, 0), black);
        _stock = MeshPart(_weaponRoot, Box(new Vector3(0.14f, 0.13f, 0.38f)), new Vector3(0, -0.01f, 0.38f), Vector3.Zero, polymer);
        MeshPart(_weaponRoot, Box(new Vector3(0.12f, 0.3f, 0.13f)), new Vector3(0, -0.2f, -0.04f), new Vector3(0.2f, 0, 0), polymer);
        _magazine = MeshPart(_weaponRoot, Box(new Vector3(0.09f, 0.26f, 0.14f)), new Vector3(0, -0.2f, -0.31f), new Vector3(-0.19f, 0, 0), black);
        MeshPart(_magazine, Box(new Vector3(0.095f, 0.028f, 0.15f)), new Vector3(0, -0.11f, 0), Vector3.Zero, steel);
        AddMagazineDetail(_magazine, steel);
        MeshPart(_weaponRoot, Box(new Vector3(0.14f, 0.045f, 0.64f)), new Vector3(0, 0.11f, -0.34f), Vector3.Zero, steel);
        BuildReflexSight(black, glass);
        _foregrip = MeshPart(_weaponRoot, Box(new Vector3(0.08f, 0.18f, 0.16f)), new Vector3(0, -0.17f, -0.58f), Vector3.Zero, polymer);

        var glove = GloveFabric(new Color(0.12f, 0.135f, 0.112f));
        var gloveArmor = Material(new Color(0.022f, 0.03f, 0.028f), 0.12f, 0.76f);
        _supportHand = BuildTacticalHand(_weaponRoot, true, new Vector3(-0.03f, -0.2f, -0.58f), new Vector3(0.2f, 0, 0.05f), glove, gloveArmor);
        _supportForearm = BuildSleevedForearm(_weaponRoot, new Vector3(-0.12f, -0.42f, -0.47f), new Vector3(0.25f, 0, -0.26f), glove, gloveArmor);
        BuildTacticalHand(_weaponRoot, false, new Vector3(0.115f, -0.2f, -0.075f), new Vector3(-0.12f, 0.05f, -0.18f), glove, gloveArmor);
        BuildSleevedForearm(_weaponRoot, new Vector3(0.19f, -0.42f, 0.015f), new Vector3(-0.18f, 0.05f, -0.3f), glove, gloveArmor);
        _spareMagazine = MeshPart(_weaponRoot, Box(new Vector3(0.09f, 0.26f, 0.14f)), new Vector3(-0.3f, -0.62f, -0.18f), new Vector3(0.35f, 0, 0.35f), black);
        MeshPart(_spareMagazine, Box(new Vector3(0.095f, 0.028f, 0.15f)), new Vector3(0, -0.11f, 0), Vector3.Zero, steel);
        AddMagazineDetail(_spareMagazine, steel);
        _spareMagazine.Visible = false;
        _chargingHandle = MeshPart(_weaponRoot, Box(new Vector3(0.055f, 0.04f, 0.12f)), new Vector3(0.075f, 0.085f, -0.05f), Vector3.Zero, steel);

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
        MeshPart(_weaponRoot, Cylinder(0.036f, 0.18f), new Vector3(0.09f, -0.015f, -0.73f), new Vector3(Mathf.Pi / 2, 0, 0), black);

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

        _gunAudio = new AudioStreamPlayer3D
        {
            Stream = SoundLab.Gunshot(),
            VolumeDb = -5.0f,
            MaxDistance = 80.0f
        };
        _muzzle.AddChild(_gunAudio);
        _reloadAudio = new AudioStreamPlayer { Stream = SoundLab.ReloadClick(), VolumeDb = -8.0f };
        AddChild(_reloadAudio);
        _footstepAudio = new AudioStreamPlayer3D
        {
            Stream = SoundLab.Footstep(),
            VolumeDb = -15.0f,
            MaxDistance = 16.0f
        };
        AddChild(_footstepAudio);
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
            new SphereMesh { Radius = 0.006f, Height = 0.012f, RadialSegments = 10, Rings = 5 },
            new Vector3(0, 0, -0.052f),
            Vector3.Zero,
            reticleMaterial);
        _opticReticle.Visible = false;
    }

    private void ApplyWeaponBuildVisuals()
    {
        var definition = WeaponCatalog.Weapon(EquippedWeapon.Platform);
        var stats = EquippedWeapon.Stats();
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
            _ => new Color(0.045f, 0.052f, 0.05f)
        };
        var furnitureColor = EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Color(0.24f, 0.12f, 0.055f)
            : EquippedWeapon.Platform == WeaponPlatform.ScarL
                ? new Color(0.29f, 0.255f, 0.18f)
                : new Color(0.18f, 0.17f, 0.13f);
        var receiverMaterial = Material(receiverColor, 0.52f, 0.46f);
        var furnitureMaterial = Material(furnitureColor, 0.12f, 0.68f);

        ((BoxMesh)_receiver.Mesh).Size = EquippedWeapon.Platform switch
        {
            WeaponPlatform.AK74 => new Vector3(0.14f, 0.16f, 0.52f),
            WeaponPlatform.ScarL => new Vector3(0.16f, 0.18f, 0.56f),
            _ => new Vector3(0.13f, 0.15f, 0.46f)
        };
        _receiver.MaterialOverride = receiverMaterial;
        ((BoxMesh)_handguard.Mesh).Size = new Vector3(
            EquippedWeapon.Platform == WeaponPlatform.ScarL ? 0.17f : 0.15f,
            0.12f,
            Mathf.Max(0.28f, barrelLength * 0.72f));
        _handguard.Position = new Vector3(0, 0.01f, -0.29f - barrelLength * 0.25f);
        _handguard.MaterialOverride = furnitureMaterial;
        ((CylinderMesh)_barrelPart.Mesh).Height = barrelLength;
        _barrelPart.Position = new Vector3(0, 0.015f, -0.56f - barrelLength * 0.5f);

        var muzzleLength = 0.14f;
        var muzzleRadius = 0.047f;
        if (EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Muzzle, out var muzzleId))
        {
            var muzzle = WeaponCatalog.Attachment(muzzleId);
            muzzleLength *= muzzle.VisualScale;
            muzzleRadius = muzzleId == "muzzle_suppressor" ? 0.055f : 0.048f;
        }
        ((CylinderMesh)_muzzlePart.Mesh).Height = muzzleLength;
        ((CylinderMesh)_muzzlePart.Mesh).TopRadius = muzzleRadius;
        ((CylinderMesh)_muzzlePart.Mesh).BottomRadius = muzzleRadius;
        var muzzleZ = -0.56f - barrelLength - muzzleLength * 0.5f;
        _muzzlePart.Position = new Vector3(0, 0.015f, muzzleZ);
        _muzzle.Position = new Vector3(0, 0.015f, muzzleZ - muzzleLength * 0.55f);

        var stockScale = EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Stock, out var stockId)
            ? WeaponCatalog.Attachment(stockId).VisualScale
            : 1.0f;
        ((BoxMesh)_stock.Mesh).Size = new Vector3(0.14f * stockScale, 0.13f * stockScale, 0.38f * stockScale);
        _stock.MaterialOverride = furnitureMaterial;
        _stock.Position = new Vector3(0, -0.01f, 0.25f + 0.13f * stockScale);

        var magScale = stats.MagazineSize > 30 ? 1.24f : 1.0f;
        ((BoxMesh)_magazine.Mesh).Size = new Vector3(0.09f, 0.26f * magScale, 0.14f);
        ((BoxMesh)_spareMagazine.Mesh).Size = new Vector3(0.09f, 0.26f * magScale, 0.14f);
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
        _opticRoot.Position = new Vector3(0, opticId == "optic_scope" ? 0.225f : 0.205f, -0.25f);
        _reflexSightModel.Visible = opticId == "optic_micro";
        _holoSightModel.Visible = opticId == "optic_holo";
        _scopeSightModel.Visible = opticId == "optic_scope";
        _opticReticle.Position = opticId switch
        {
            "optic_scope" => new Vector3(0, 0, -0.255f),
            "optic_holo" => new Vector3(0, 0, -0.078f),
            _ => new Vector3(0, 0, -0.052f)
        };

        _weaponLight.SpotRange = stats.EffectiveRange * 0.28f;
        _weaponLight.Position = new Vector3(0.09f, -0.015f, -0.5f - barrelLength * 0.45f);
        _gunAudio.MaxDistance = stats.SoundRadius * 1.9f;
        Ammo = Mathf.Min(Ammo, stats.MagazineSize);
    }

    private void BuildKnife()
    {
        _knifeRoot = new Node3D
        {
            Name = "TacticalKnife",
            Position = new Vector3(0.24f, -0.32f, -0.68f),
            Scale = Vector3.One * 0.78f,
            Visible = false
        };
        _camera.AddChild(_knifeRoot);
        var steel = Material(new Color(0.16f, 0.19f, 0.18f), 0.92f, 0.18f);
        var edge = Material(new Color(0.5f, 0.55f, 0.53f), 0.96f, 0.08f);
        var grip = Material(new Color(0.035f, 0.043f, 0.04f), 0.16f, 0.82f);
        var glove = GloveFabric(new Color(0.115f, 0.13f, 0.108f));
        var gloveArmor = Material(new Color(0.022f, 0.03f, 0.028f), 0.15f, 0.72f);
        MeshPart(_knifeRoot, Box(new Vector3(0.09f, 0.09f, 0.3f)), new Vector3(0, 0, 0.08f), Vector3.Zero, grip);
        for (var ring = -2; ring <= 2; ring++)
        {
            MeshPart(_knifeRoot, Box(new Vector3(0.105f, 0.018f, 0.025f)), new Vector3(0, 0.052f, 0.08f + ring * 0.048f), Vector3.Zero, edge);
        }
        MeshPart(_knifeRoot, Box(new Vector3(0.22f, 0.035f, 0.055f)), new Vector3(0, 0, -0.095f), Vector3.Zero, steel);
        MeshPart(_knifeRoot, new PrismMesh { Size = new Vector3(0.115f, 0.018f, 0.62f) }, new Vector3(0, 0, -0.42f), new Vector3(0, Mathf.Pi, 0), steel);
        MeshPart(_knifeRoot, Box(new Vector3(0.012f, 0.022f, 0.49f)), new Vector3(-0.052f, -0.004f, -0.39f), Vector3.Zero, edge);
        BuildTacticalHand(_knifeRoot, false, new Vector3(0.015f, -0.04f, 0.1f), new Vector3(0.04f, 0, 0), glove, gloveArmor);
        BuildSleevedForearm(_knifeRoot, new Vector3(0.15f, -0.34f, 0.25f), new Vector3(-0.24f, 0, -0.28f), glove, gloveArmor);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseMotion motion
            || Input.MouseMode != Input.MouseModeEnum.Captured
            || IsDead)
        {
            return;
        }

        var rotation = Rotation;
        rotation.Y -= motion.Relative.X * MouseSensitivity;
        Rotation = rotation;
        _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.38f, 1.38f);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        if (IsDead)
        {
            Velocity = Vector3.Zero;
            return;
        }
        if (UiLocked)
        {
            Velocity = Vector3.Zero;
            _isAiming = false;
            Hud?.SetAiming(false);
            return;
        }

        _fireCooldown = Mathf.Max(0.0f, _fireCooldown - dt);
        _knifeTime = Mathf.Max(0.0f, _knifeTime - dt);
        if (!_fireInputArmed)
        {
            if (Input.IsActionPressed("fire"))
            {
                _fireReleaseTime = 0.0f;
            }
            else
            {
                _fireReleaseTime += dt;
                _fireInputArmed = _fireReleaseTime >= 0.2f;
            }
        }

        if (Input.IsActionJustPressed("weapon_primary"))
        {
            SwitchWeapon(false);
        }
        else if (Input.IsActionJustPressed("weapon_melee"))
        {
            SwitchWeapon(true);
        }
        else if (Input.IsActionJustPressed("weapon_cycle"))
        {
            CycleWeapon();
        }

        if (Input.IsActionJustPressed("toggle_fire_mode") && !_knifeEquipped && !_isReloading && !_isPlating)
        {
            _automaticFire = !_automaticFire;
            Hud?.ShowLocalizedMessage(
                _automaticFire ? "fire_auto" : "fire_semi",
                _automaticFire ? "FIRE MODE  //  AUTO" : "FIRE MODE  //  SEMI",
                new Color(0.42f, 0.9f, 0.73f));
        }
        if (Input.IsActionJustPressed("toggle_flashlight") && !_knifeEquipped)
        {
            _flashlightOn = !_flashlightOn;
            _weaponLight.Visible = _flashlightOn;
            Hud?.ShowLocalizedMessage(
                _flashlightOn ? "light_on" : "light_off",
                _flashlightOn ? "WEAPON LIGHT  //  ON" : "WEAPON LIGHT  //  OFF",
                _flashlightOn ? new Color(0.72f, 0.9f, 1.0f) : new Color(0.55f, 0.65f, 0.63f));
        }
        if (Input.IsActionJustPressed("use_plate"))
        {
            StartPlate();
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
        if (!_knifeEquipped && !_isPlating && Input.IsActionJustPressed("reload"))
        {
            StartReload();
        }
        if (!_isPlating && Input.IsActionJustPressed("throw_grenade"))
        {
            ThrowGrenade();
        }

        _isAiming = !_knifeEquipped && Input.IsActionPressed("aim") && !_isReloading && !_isPlating && _slideTime <= 0.0f;
        var fireRequested = _knifeEquipped
            ? Input.IsActionJustPressed("fire")
            : _automaticFire ? Input.IsActionPressed("fire") : Input.IsActionJustPressed("fire");
        if (!_isPlating && _fireInputArmed && fireRequested && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (_knifeEquipped)
            {
                StartKnifeAttack();
            }
            else
            {
                Fire();
            }
        }

        MovePlayer(dt);
        UpdateCameraAndWeapon(dt);
        Hud?.SetStats(Health, Armor, Stamina, Ammo, ReserveAmmo, Grenades);
        Hud?.SetEquipment(
            ArmorPlates,
            _knifeEquipped ? "KNIFE" : _automaticFire ? "AUTO" : "SEMI",
            EquippedWeapon.DisplayName(Hud?.CurrentLanguage ?? "en"));
        Hud?.SetAiming(_isAiming);
        Hud?.SetHeading(Mathf.RadToDeg(Rotation.Y) * -1.0f);
    }

    private void MovePlayer(float delta)
    {
        var input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
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
        var crouching = _stance == PlayerStance.Crouched;
        var prone = _stance == PlayerStance.Prone;
        var sprinting = Input.IsActionPressed("sprint") && input.Y < -0.15f && !crouching && !prone
            && Stamina > 1.0f && !_isAiming;
        var speed = prone ? ProneSpeed : crouching ? CrouchSpeed : sprinting ? SprintSpeed : WalkSpeed;

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
            velocity.X = Mathf.MoveToward(velocity.X, direction.X * speed, delta * 28.0f);
            velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * speed, delta * 28.0f);
        }

        Stamina = sprinting
            ? Mathf.Max(0.0f, Stamina - delta * 21.0f)
            : Mathf.Min(100.0f, Stamina + delta * 14.0f);
        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * delta;
        }
        else if (Input.IsActionJustPressed("jump") && !crouching && !prone)
        {
            velocity.Y = 6.8f;
        }
        Velocity = velocity;
        MoveAndSlide();

        var targetHeadY = prone ? 0.62f : crouching ? 1.16f : 1.57f;
        var targetColliderHeight = prone ? 0.78f : crouching ? 1.2f : 1.75f;
        var headPosition = _head.Position;
        headPosition.Y = Mathf.Lerp(headPosition.Y, targetHeadY, delta * 12.0f);
        _head.Position = headPosition;
        var capsule = (CapsuleShape3D)_collider.Shape;
        capsule.Height = Mathf.Lerp(capsule.Height, targetColliderHeight, delta * 12.0f);
        var colliderPosition = _collider.Position;
        colliderPosition.Y = Mathf.Lerp(colliderPosition.Y, targetColliderHeight * 0.5f, delta * 12.0f);
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
        if (Input.IsActionJustPressed("prone"))
        {
            TrySetStance(_stance == PlayerStance.Prone ? PlayerStance.Crouched : PlayerStance.Prone);
            _slideTime = 0.0f;
            return;
        }
        if (!Input.IsActionJustPressed("crouch"))
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
            var query = PhysicsRayQueryParameters3D.Create(
                GlobalPosition + offset + Vector3.Up * (currentHeight - 0.08f),
                GlobalPosition + offset + Vector3.Up * (targetHeight + 0.08f));
            query.CollisionMask = 1;
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
            if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0)
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

    private void UpdateCameraAndWeapon(float delta)
    {
        _recoilPitch = Mathf.Lerp(_recoilPitch, 0.0f, delta * 11.0f);
        _recoilSide = Mathf.Lerp(_recoilSide, 0.0f, delta * 13.0f);
        var leanInput = Input.GetActionStrength("lean_right") - Input.GetActionStrength("lean_left");
        _leanValue = Mathf.Lerp(_leanValue, _slideTime <= 0.0f ? leanInput : 0.0f, delta * 9.0f);
        _head.Rotation = new Vector3(_pitch + _recoilPitch, 0.0f, _recoilSide * 0.22f + _leanValue * 0.13f);

        var horizontalSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        _bobTime = IsOnFloor() && horizontalSpeed > 0.5f
            ? _bobTime + delta * horizontalSpeed * 1.45f
            : Mathf.Lerp(_bobTime, 0.0f, delta * 3.0f);
        var stanceBob = _stance switch
        {
            PlayerStance.Prone => 0.22f,
            PlayerStance.Crouched => 0.55f,
            _ => 1.0f
        };
        var bobStrength = Mathf.Clamp(horizontalSpeed / SprintSpeed, 0.0f, 1.0f) * stanceBob;
        var bobOffset = new Vector3(
            Mathf.Sin(_bobTime * 0.9f) * 0.021f,
            Mathf.Abs(Mathf.Cos(_bobTime * 1.8f)) * 0.024f,
            0.0f) * bobStrength;
        _camera.Position = bobOffset + new Vector3(_leanValue * 0.17f, _slideTime > 0.0f ? -0.08f : 0.0f, 0.0f);

        var targetFov = _isAiming ? AimFieldOfView() : _slideTime > 0.0f ? 84.0f : horizontalSpeed > 7.0f ? 82.0f : 76.0f;
        var handling = EquippedWeapon.Stats().Handling;
        _camera.Fov = Mathf.Lerp(_camera.Fov, targetFov, delta * (6.5f + handling * 5.0f));
        _weaponRoot.Visible = !_knifeEquipped;
        _knifeRoot.Visible = _knifeEquipped;
        UpdateKnifeAnimation(delta);
        var targetPosition = _isAiming
            ? new Vector3(0.0f, -0.139f, -0.55f)
            : new Vector3(0.34f, -0.33f, -0.58f);
        if (_isReloading)
        {
            targetPosition = new Vector3(0.18f, -0.23f, -0.86f);
        }
        else if (_searchPose > 0.0f)
        {
            targetPosition = new Vector3(0.5f, -0.58f, -0.48f).Lerp(new Vector3(0.32f, -0.48f, -0.72f), _searchPose);
        }
        else if (_isPlating)
        {
            targetPosition += new Vector3(0.22f, -0.34f, 0.12f);
        }
        _weaponRoot.Position = _weaponRoot.Position.Lerp(targetPosition, delta * (_isAiming ? 7.5f + handling * 6.0f : 6.0f + handling * 3.0f));
        var weaponRotation = _weaponRoot.Rotation;
        var searchRoll = _searchPose > 0.0f ? -0.42f : 0.0f;
        var searchPitch = _searchPose > 0.0f ? 0.34f : 0.0f;
        weaponRotation.Z = Mathf.Lerp(weaponRotation.Z, _isReloading ? -0.32f : searchRoll + _recoilSide * 0.35f, delta * 9.0f);
        weaponRotation.X = Mathf.Lerp(weaponRotation.X, _isReloading ? -0.13f : searchPitch + _recoilPitch * 0.55f, delta * 9.0f);
        _weaponRoot.Rotation = weaponRotation;
        _opticReticle.Visible = _isAiming && !_knifeEquipped;
        UpdateReloadAnimation();
    }

    private float AimFieldOfView()
    {
        if (!EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var opticId))
        {
            return 55.0f;
        }
        return opticId switch
        {
            "optic_scope" => 29.0f,
            "optic_holo" => 44.0f,
            _ => 49.0f
        };
    }

    public void SelectWeapon(int slot)
    {
        SwitchWeapon(slot == 1);
    }

    public void CycleWeapon()
    {
        SwitchWeapon(!_knifeEquipped);
    }

    private void SwitchWeapon(bool useKnife)
    {
        if (_knifeEquipped == useKnife || _isPlating)
        {
            return;
        }
        if (_isReloading)
        {
            _isReloading = false;
            _reloadTime = 0.0f;
            ResetReloadRig();
        }
        _knifeEquipped = useKnife;
        _isAiming = false;
        _knifeTime = 0.0f;
        _weaponRoot.Visible = !useKnife;
        _knifeRoot.Visible = useKnife;
        _weaponLight.Visible = !useKnife && _flashlightOn;
        Hud?.ShowLocalizedMessage(
            useKnife ? "knife_ready" : "primary_ready",
            useKnife ? "TACTICAL KNIFE READY" : "PRIMARY WEAPON READY",
            new Color(0.42f, 0.9f, 0.73f));
    }

    private void StartKnifeAttack()
    {
        if (_knifeTime > 0.0f || _fireCooldown > 0.0f)
        {
            return;
        }
        _knifeTime = 0.56f;
        _fireCooldown = 0.56f;
        _knifeHitApplied = false;
    }

    private void UpdateKnifeAnimation(float delta)
    {
        var restingPosition = new Vector3(0.24f, -0.32f, -0.68f);
        var restingRotation = new Vector3(-0.08f, -0.18f, -0.08f);
        if (!_knifeEquipped || _knifeTime <= 0.0f)
        {
            _knifeRoot.Position = _knifeRoot.Position.Lerp(restingPosition, delta * 12.0f);
            _knifeRoot.Rotation = _knifeRoot.Rotation.Lerp(restingRotation, delta * 12.0f);
            return;
        }

        var progress = 1.0f - _knifeTime / 0.56f;
        var thrust = Mathf.Sin(progress * Mathf.Pi);
        var slash = Mathf.Sin(progress * Mathf.Pi * 2.0f) * 0.22f;
        var targetPosition = restingPosition + new Vector3(-0.17f * thrust, 0.12f * thrust, -0.42f * thrust);
        var targetRotation = restingRotation + new Vector3(-0.7f * thrust, -0.85f * thrust, slash);
        _knifeRoot.Position = _knifeRoot.Position.Lerp(targetPosition, delta * 24.0f);
        _knifeRoot.Rotation = _knifeRoot.Rotation.Lerp(targetRotation, delta * 24.0f);
        if (!_knifeHitApplied && progress >= 0.32f)
        {
            _knifeHitApplied = true;
            ResolveKnifeHit();
        }
    }

    private void ResolveKnifeHit()
    {
        var from = _camera.GlobalPosition;
        var to = from - _camera.GlobalBasis.Z * 2.55f;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        query.CollideWithAreas = false;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return;
        }
        var point = hit["position"].AsVector3();
        var target = hit["collider"].AsGodotObject();
        if (target is EnemyOperator enemy)
        {
            var killed = enemy.TakeDamage(_rng.RandfRange(56.0f, 68.0f), point, this);
            EmitSignal(SignalName.HitConfirmed, killed, enemy.LastHitWasHeadshot, enemy.LastHitWasArmored);
        }
        else if (target is ExplosiveBarrel barrel)
        {
            barrel.TakeDamage(24.0f, point, this);
            EmitSignal(SignalName.HitConfirmed, false, false, false);
        }
        Main?.SpawnImpact(point, hit["normal"].AsVector3());
    }

    public void Fire()
    {
        if (_fireCooldown > 0.0f || _isReloading || _isPlating)
        {
            return;
        }
        if (Ammo <= 0)
        {
            StartReload();
            return;
        }
        if (new Vector2(Velocity.X, Velocity.Z).Length() > 7.4f)
        {
            return;
        }

        Ammo--;
        var stats = EquippedWeapon.Stats();
        _fireCooldown = stats.FireInterval;
        Main?.ReportGunshot(GlobalPosition, stats.SoundRadius);
        _gunAudio.PitchScale = _rng.RandfRange(0.94f, 1.06f);
        _gunAudio.Play();
        _muzzleFlash.LightEnergy = 7.0f;
        _muzzleBloom.Visible = true;
        _muzzleBloom.Scale = Vector3.One * _rng.RandfRange(0.78f, 1.22f);
        var bloomRotation = _muzzleBloom.Rotation;
        bloomRotation.Z = _rng.RandfRange(0.0f, Mathf.Tau);
        _muzzleBloom.Rotation = bloomRotation;
        var flashTween = CreateTween();
        flashTween.TweenProperty(_muzzleFlash, "light_energy", 0.0f, 0.045f);
        flashTween.Parallel().TweenProperty(_muzzleBloom, "scale", Vector3.One * 0.15f, 0.055f);
        flashTween.TweenCallback(Callable.From(() => _muzzleBloom.Visible = false));

        var shellVelocity = _camera.GlobalBasis.X * 3.0f + Vector3.Up * 1.25f - _camera.GlobalBasis.Z * 0.45f;
        Main?.SpawnShell(_ejectMarker.GlobalPosition, shellVelocity);
        Hud?.PulseCrosshair();

        var movingPenalty = Mathf.Clamp(new Vector2(Velocity.X, Velocity.Z).Length() / SprintSpeed, 0.0f, 1.0f);
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
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        query.CollideWithAreas = false;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        var end = to;
        var damagedTarget = false;
        var headshot = false;
        if (hit.Count > 0)
        {
            end = hit["position"].AsVector3();
            var target = hit["collider"].AsGodotObject();
            var killed = false;
            if (target is EnemyOperator enemy)
            {
                damagedTarget = true;
                var distance = from.DistanceTo(end);
                var falloff = Mathf.Lerp(1.0f, 0.52f, Mathf.Clamp(distance / maximumRange, 0.0f, 1.0f));
                killed = enemy.TakeDamage(stats.Damage * falloff * _rng.RandfRange(0.94f, 1.06f), end, this);
                headshot = enemy.LastHitWasHeadshot;
                EmitSignal(SignalName.HitConfirmed, killed, headshot, enemy.LastHitWasArmored);
            }
            else if (target is ExplosiveBarrel barrel)
            {
                damagedTarget = true;
                barrel.TakeDamage(stats.Damage * _rng.RandfRange(0.94f, 1.06f), end, this);
                EmitSignal(SignalName.HitConfirmed, false, false, false);
            }
            Main?.SpawnImpact(end, hit["normal"].AsVector3());
        }

        Main?.SpawnTracer(_muzzle.GlobalPosition, end, new Color(1.0f, 0.67f, 0.24f));
        Main?.RecordShot(damagedTarget, headshot);
        var stanceRecoil = _stance switch
        {
            PlayerStance.Prone => 0.62f,
            PlayerStance.Crouched => 0.82f,
            _ => 1.0f
        };
        _recoilPitch -= _rng.RandfRange(0.012f, 0.021f) * stats.Recoil * (_isAiming ? 0.55f : 1.0f) * stanceRecoil;
        _recoilSide += _rng.RandfRange(-0.018f, 0.018f) * stats.Recoil * stanceRecoil;
        var weaponPosition = _weaponRoot.Position;
        weaponPosition.Z += 0.055f;
        _weaponRoot.Position = weaponPosition;
    }

    private void StartReload()
    {
        var magazineSize = EquippedWeapon.Stats().MagazineSize;
        if (_isReloading || _knifeEquipped || Ammo >= magazineSize || ReserveAmmo <= 0 || IsDead)
        {
            return;
        }
        _isReloading = true;
        _reloadTime = ReloadDuration;
        _reloadSoundStage = 0;
    }

    private void FinishReload()
    {
        var amount = Mathf.Min(EquippedWeapon.Stats().MagazineSize - Ammo, ReserveAmmo);
        Ammo += amount;
        ReserveAmmo -= amount;
        _isReloading = false;
        ResetReloadRig();
    }

    private void UpdateReloadAnimation()
    {
        if (!_isReloading)
        {
            return;
        }

        var progress = Mathf.Clamp(ReloadProgress, 0.0f, 1.0f);
        var magazineHome = new Vector3(0, -0.2f, -0.31f);
        var magazineRotation = EquippedWeapon.Platform == WeaponPlatform.AK74
            ? new Vector3(-0.29f, 0, 0)
            : new Vector3(-0.19f, 0, 0);
        var handHome = new Vector3(-0.03f, -0.2f, -0.58f);
        var handAtWell = new Vector3(-0.11f, -0.22f, -0.31f);

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
            var dropped = new Vector3(-0.13f, -0.58f, -0.24f);
            _magazine.Position = magazineHome.Lerp(dropped, t);
            _magazine.Rotation = magazineRotation.Lerp(new Vector3(0.62f, 0.08f, 0.36f), t);
            _supportHand.Position = handAtWell.Lerp(dropped + new Vector3(-0.08f, 0.04f, 0.02f), t);
            if (_reloadSoundStage == 0 && progress > 0.3f)
            {
                _reloadSoundStage = 1;
                _reloadAudio.PitchScale = 0.9f;
                _reloadAudio.Play();
            }
        }
        else if (progress < 0.55f)
        {
            _magazine.Visible = false;
            _spareMagazine.Visible = true;
            var t = SmoothStep((progress - 0.43f) / 0.12f);
            var pickup = new Vector3(-0.3f, -0.62f, -0.18f);
            var ready = new Vector3(-0.18f, -0.46f, -0.25f);
            _spareMagazine.Position = pickup.Lerp(ready, t);
            _supportHand.Position = _spareMagazine.Position + new Vector3(-0.08f, 0.04f, 0.02f);
        }
        else if (progress < 0.78f)
        {
            var t = SmoothStep((progress - 0.55f) / 0.23f);
            var ready = new Vector3(-0.18f, -0.46f, -0.25f);
            _spareMagazine.Position = ready.Lerp(magazineHome, t);
            _spareMagazine.Rotation = new Vector3(0.35f, 0, 0.35f).Lerp(magazineRotation, t);
            _supportHand.Position = _spareMagazine.Position + new Vector3(-0.08f, 0.04f, 0.02f);
        }
        else if (progress < 0.9f)
        {
            _magazine.Visible = true;
            _magazine.Position = magazineHome;
            _magazine.Rotation = magazineRotation;
            _spareMagazine.Visible = false;
            var t = SmoothStep((progress - 0.78f) / 0.12f);
            var handleGrip = new Vector3(0.0f, 0.02f, -0.02f);
            _supportHand.Position = handAtWell.Lerp(handleGrip, t);
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
    }

    private static float SmoothStep(float value)
    {
        var t = Mathf.Clamp(value, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private void StartPlate()
    {
        if (_isPlating || _isReloading || ArmorPlates <= 0 || Armor >= 99.0f || IsDead)
        {
            return;
        }
        _isPlating = true;
        _plateTime = 2.4f;
        Hud?.SetEquipmentActionLocalized("applying_armor", "APPLYING ARMOR", 0.0f, true);
    }

    private void UpdatePlate(float delta)
    {
        if (!_isPlating)
        {
            return;
        }
        var movement = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        if (movement.LengthSquared() > 0.02f || Input.IsActionPressed("fire") || Input.IsActionPressed("sprint"))
        {
            CancelPlate();
            return;
        }
        _plateTime -= delta;
        Hud?.SetEquipmentActionLocalized("applying_armor", "APPLYING ARMOR", 1.0f - _plateTime / 2.4f, true);
        if (_plateTime > 0.0f)
        {
            return;
        }
        var armorDefinition = EquippedBodyArmor.Definition;
        EquippedBodyArmor.Durability = Mathf.Min(
            armorDefinition.MaxDurability,
            EquippedBodyArmor.Durability + armorDefinition.MaxDurability * 0.4f);
        ArmorPlates--;
        _isPlating = false;
        Hud?.SetEquipmentAction(string.Empty, 0.0f, false);
        Hud?.ShowLocalizedMessage("armor_secured", "ARMOR PLATE SECURED", new Color(0.4f, 0.76f, 1.0f));
    }

    private void CancelPlate()
    {
        if (!_isPlating)
        {
            return;
        }
        _isPlating = false;
        _plateTime = 0.0f;
        Hud?.SetEquipmentAction(string.Empty, 0.0f, false);
    }

    public bool TryCollectAmmo(int amount)
    {
        const int maxReserveAmmo = 210;
        if (ReserveAmmo >= maxReserveAmmo)
        {
            return false;
        }
        ReserveAmmo = Mathf.Min(maxReserveAmmo, ReserveAmmo + amount);
        Hud?.ShowLocalizedMessage("ammo_recovered", "AMMUNITION RECOVERED", new Color(0.42f, 0.9f, 0.64f));
        return true;
    }

    public bool TryCollectArmorPlate()
    {
        const int maxArmorPlates = 3;
        if (ArmorPlates >= maxArmorPlates)
        {
            return false;
        }
        ArmorPlates++;
        Hud?.ShowLocalizedMessage("armor_recovered", "SPARE ARMOR RECOVERED", new Color(0.42f, 0.72f, 1.0f));
        return true;
    }

    public bool TryStoreInBackpack(LootItem item)
    {
        if (Backpack.Count >= BackpackCapacity)
        {
            Hud?.ShowLocalizedMessage("backpack_full", "BACKPACK FULL", new Color(1.0f, 0.48f, 0.28f));
            return false;
        }
        Backpack.Add(item);
        Hud?.ShowLocalizedMessage("item_stored", "ITEM STORED", new Color(0.42f, 0.9f, 0.68f));
        return true;
    }

    public LootItem? EquipFromLoot(LootItem item)
    {
        if (item.Kind == LootItemKind.Weapon && item.Weapon is not null)
        {
            var previous = new LootItem { Kind = LootItemKind.Weapon, Weapon = EquippedWeapon.Clone() };
            EquipPrimary(item.Weapon);
            return previous;
        }
        if (item.Kind == LootItemKind.Attachment)
        {
            var attachment = WeaponCatalog.Attachment(item.AttachmentId);
            LootItem? previous = null;
            if (EquippedWeapon.Attachments.TryGetValue(attachment.Slot, out var previousId))
            {
                previous = new LootItem { Kind = LootItemKind.Attachment, AttachmentId = previousId };
            }
            EquippedWeapon.Attachments[attachment.Slot] = attachment.Id;
            ApplyWeaponBuildVisuals();
            Hud?.ShowLocalizedMessage("part_installed", "WEAPON PART INSTALLED", new Color(0.42f, 0.9f, 0.72f));
            return previous;
        }
        if (item.Kind == LootItemKind.Ammunition && TryCollectAmmo(item.Quantity))
        {
            return null;
        }
        if (item.Kind == LootItemKind.ArmorPlate && TryCollectArmorPlate())
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
            switch (incoming.Definition.Slot)
            {
                case EquipmentSlot.Helmet:
                    EquippedHelmet = incoming.Clone();
                    break;
                case EquipmentSlot.BodyArmor:
                    EquippedBodyArmor = incoming.Clone();
                    break;
                case EquipmentSlot.Backpack:
                    EquippedBackpack = incoming.Clone();
                    break;
            }
            Hud?.ShowLocalizedMessage("equipment_replaced", "EQUIPMENT REPLACED", new Color(0.84f, 0.7f, 0.34f));
            return previous is null
                ? null
                : new LootItem { Kind = LootItemKind.Equipment, Equipment = previous.Clone() };
        }
        return item;
    }

    public bool UseBackpackItem(string itemId)
    {
        var index = Backpack.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return false;
        }
        var item = Backpack[index];
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

    private void EquipPrimary(WeaponBuild build)
    {
        EquippedWeapon = build.Clone();
        Ammo = EquippedWeapon.Stats().MagazineSize;
        _isReloading = false;
        ResetReloadRig();
        ApplyWeaponBuildVisuals();
        SwitchWeapon(false);
        Hud?.ShowLocalizedMessage("weapon_equipped", "PRIMARY WEAPON EQUIPPED", new Color(0.4f, 0.86f, 0.7f));
    }

    private void ThrowGrenade()
    {
        if (Grenades <= 0 || _isReloading || IsDead || Main is null)
        {
            return;
        }
        Grenades--;
        Main.ThrowGrenade(_camera.GlobalPosition - _camera.GlobalBasis.Z * 0.7f, -_camera.GlobalBasis.Z, this);
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
        if (IsDead)
        {
            return true;
        }
        if (Main?.IsPlayerProtected() == true)
        {
            return false;
        }

        CancelPlate();

        var region = attacker is EnemyOperator ? ResolveHitRegion(hitPosition) : HitRegion.Torso;
        var adjustedDamage = region switch
        {
            HitRegion.Head => amount * 1.85f,
            HitRegion.Limbs => amount * 0.72f,
            _ => amount
        };
        var protectiveGear = region switch
        {
            HitRegion.Head => EquippedHelmet,
            HitRegion.Torso => EquippedBodyArmor,
            _ => null
        };
        var armorHit = protectiveGear is not null && protectiveGear.Durability > 0.0f;
        if (protectiveGear is not null)
        {
            adjustedDamage = ApplyProtection(protectiveGear, adjustedDamage);
        }
        Health -= adjustedDamage;
        Hud?.ShowDamage();
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
            IsDead = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
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
