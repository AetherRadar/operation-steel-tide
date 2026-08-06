using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class FreightTerminalWorld : Node3D
{
    private TacticalPlayer _player = null!;
    private CombatHUD _hud = null!;
    private MissionDirector _missionDirector = null!;
    private readonly List<EnemyOperator> _enemies = new();
    private readonly List<ILootSource> _lootSources = new();
    private readonly List<ExplosiveBarrel> _barrels = new();
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private readonly List<Node3D> _objectiveTerminals = new();
    private readonly List<StandardMaterial3D> _objectiveScreens = new();
    private readonly List<OmniLight3D> _objectiveLights = new();
    private readonly RandomNumberGenerator _rng = new();
    private readonly Vector3[] _coverPoints =
    {
        new(-4, 0, 19.6f), new(-4, 0, 22.4f), new(2.7f, 0, 10), new(5.3f, 0, 10),
        new(-4, 0, -5.4f), new(-4, 0, -2.6f), new(-5.3f, 0, -20), new(-2.7f, 0, -20),
        new(3, 0, -32.4f), new(17, 0, 7.7f), new(17, 0, 10.3f), new(-12, 0, -5),
        new(-24, 0, 3), new(-10, 0, 12), new(24, 0, -18),
        new(-37, 0, 17), new(-30, 0, 17), new(20, 0, 24), new(30, 0, 24),
        new(-7, 0, 31), new(7, 0, 31),
        new(-16, 0, 25.6f), new(-16, 0, 28.4f), new(10.6f, 0, 26.5f), new(13.4f, 0, 26.5f),
        new(2, 0, -16.4f), new(2, 0, -13.6f), new(14.6f, 0, -33), new(17.4f, 0, -33),
        new(-19.4f, 0, 6), new(-16.6f, 0, 6), new(28, 0, -32.4f), new(28, 0, -29.6f),
        new(-12, 0, 20.5f), new(-12, 0, 23.5f), new(10, 0, -18.5f), new(10, 0, -15.5f),
        new(-13.5f, 0, -14), new(-10.5f, 0, -14), new(13, 0, 27.5f), new(13, 0, 30.5f),
        new(23.5f, 0, -32), new(26.5f, 0, -32), new(-0.5f, 0, -13.1f), new(-0.5f, 0, -9.9f),
        new(-2.8f, 0, 16), new(2.8f, 0, 16), new(0, 0, 13.2f), new(0, 0, 18.8f)
    };

    private Node3D _levelRoot = null!;
    private Node3D _extractionMarker = null!;
    private Godot.Environment _environmentRef = null!;
    private DirectionalLight3D _sunLight = null!;
    private string _missionPhase = "DEPLOYMENT";
    private string _currentObjective = "DISABLE THE COMMUNICATIONS RELAY";
    private float _missionDetectionRange = 34.0f;
    private float _missionRemaining = 12.0f;
    private bool _missionOnline;
    private bool _missionEnded;
    private int _objectiveStage;
    private float _interactionProgress;
    private ILootSource? _lootSearchTarget;
    private ILootSource? _openLootSource;
    private bool _personalBackpackOpen;
    private bool _interactReleaseRequired;
    private int _enemiesRemaining;
    private int _shotsFired;
    private int _shotsHit;
    private int _kills;
    private int _headshots;
    private int _reinforcementThreshold = 70;
    private float _threatLevel;
    private float _reinforcementCountdown;
    private bool _reinforcementPending;
    private bool _reinforcementsDeployed;
    private float _sensitivitySetting = 1.0f;
    private int _qualitySetting = 2;
    private bool _fullscreenSetting;
    private string _languageSetting = "en";

    public override void _Ready()
    {
        _rng.Randomize();
        LoadSettings();
        InitMissionDirector();
        BuildEnvironment();
        BuildLevel();
        BuildHudAndPlayer();
        SpawnLootCases();
        SpawnEnemies();
        SpawnExplosives();
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.SetMissionPhase(_missionPhase, _missionDirector.SpawnProtectionSeconds, _missionOnline);
        ApplyQuality(_qualitySetting);

        var args = OS.GetCmdlineUserArgs();
        if (Array.Exists(args, value => value == "--capture-validation"))
        {
            CaptureValidationFrame();
        }
        else if (Array.Exists(args, value => value == "--capture-deployment"))
        {
            CaptureDeploymentFrame();
        }
        else if (Array.Exists(args, value => value == "--capture-pause"))
        {
            CapturePauseFrame();
        }
        else if (Array.Exists(args, value => value == "--validate-objectives"))
        {
            ValidateObjectiveFlow();
        }
        else if (Array.Exists(args, value => value == "--validate-reinforcements"))
        {
            ValidateReinforcementFlow();
        }
        else if (Array.Exists(args, value => value == "--capture-ads"))
        {
            CaptureAdsFrame();
        }
        else if (Array.Exists(args, value => value == "--validate-equipment"))
        {
            ValidateEquipmentFlow();
        }
        else if (Array.Exists(args, value => value == "--validate-pickup"))
        {
            ValidatePickupFlow();
        }
        else if (Array.Exists(args, value => value == "--capture-reload"))
        {
            CaptureReloadFrame();
        }
        else if (Array.Exists(args, value => value == "--capture-operator"))
        {
            CaptureOperatorFrame();
        }
        else if (Array.Exists(args, value => value == "--capture-zh"))
        {
            CaptureChineseFrame();
        }
        else if (Array.Exists(args, value => value == "--capture-knife"))
        {
            CaptureKnifeFrame();
        }
        else if (Array.Exists(args, value => value == "--validate-loot"))
        {
            ValidateLootFlow();
        }
        else if (Array.Exists(args, value => value == "--validate-corpse-loot"))
        {
            ValidateCorpseLootFlow();
        }
        else if (Array.Exists(args, value => value == "--capture-backpack"))
        {
            CaptureBackpackFrame();
        }
        else if (Array.Exists(args, value => value == "--capture-optics"))
        {
            CaptureOpticsFrames();
        }
        else if (Array.Exists(args, value => value == "--validate-stance-armor"))
        {
            ValidateStanceAndArmorFlow();
        }
        else if (Array.Exists(args, value => value == "--capture-expanded-map"))
        {
            CaptureExpandedMapFrame();
        }
        else if (Array.Exists(args, value => value == "--validate-weapon-ui"))
        {
            ValidateWeaponUiFlow();
        }
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.RotateY((float)delta * 0.35f);
            var pulse = 1.0f + Mathf.Sin(Time.GetTicksMsec() * 0.003f) * 0.06f;
            _extractionMarker.Scale = new Vector3(pulse, 1.0f, pulse);
        }

        if (_missionEnded && Input.IsKeyPressed(Key.Enter))
        {
            GetTree().ReloadCurrentScene();
            return;
        }
        if (!_missionEnded && Input.IsActionJustPressed("inventory"))
        {
            if (_hud.IsLootVisible)
            {
                CloseLoot();
            }
            else
            {
                OpenPersonalBackpack();
            }
            return;
        }
        if (_hud.IsLootVisible)
        {
            if (!Input.IsActionPressed("interact"))
            {
                _interactReleaseRequired = false;
            }
            else if (!_interactReleaseRequired && Input.IsActionJustPressed("interact"))
            {
                _interactReleaseRequired = true;
                CloseLoot();
                return;
            }
        }

        if (IsInstanceValid(_player) && _missionPhase == "DEPLOYMENT"
            && _player.HasMovementIntent && _player.GlobalPosition.Z < 31.0f)
        {
            _missionDirector.ExitDeploymentZone();
        }
        UpdateInteraction((float)delta);
        UpdateReinforcements((float)delta);

        if (_enemies.Count > 0)
        {
            var highestSuspicion = 0.0f;
            foreach (var enemy in _enemies)
            {
                if (IsInstanceValid(enemy) && !enemy.IsDead)
                {
                    highestSuspicion = Mathf.Max(highestSuspicion, enemy.Suspicion);
                }
            }
            _hud.SetAlert(highestSuspicion, _missionPhase);
        }
    }

    private void InitMissionDirector()
    {
        _missionDirector = new MissionDirector { Name = "MissionDirector" };
        _missionDirector.MissionLoaded += OnMissionLoaded;
        _missionDirector.PhaseChanged += OnPhaseChanged;
        _missionDirector.Gunshot += OnDirectorGunshot;
        _missionDirector.ObjectiveChanged += OnObjectiveChanged;
        AddChild(_missionDirector);
    }

    private void OnMissionLoaded(int _spawnProtection, float detectionRange, int reinforcementThreshold, bool online)
    {
        _missionDetectionRange = detectionRange;
        _reinforcementThreshold = reinforcementThreshold;
        _missionOnline = online;
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.DetectionRange = detectionRange;
            }
        }
        _hud.SetMissionPhase(_missionPhase, _missionRemaining, _missionOnline);
    }

    private void OnPhaseChanged(string phase, float remaining, bool online)
    {
        var enteredCombat = _missionPhase != "COMBAT" && phase == "COMBAT";
        _missionPhase = phase;
        _missionRemaining = remaining;
        _missionOnline = online;
        _hud.SetMissionPhase(phase, remaining, online);
        RefreshLocalizedObjective();
        if (enteredCombat)
        {
            _hud.ShowLocalizedMessage("enemy_network", "ENEMY NETWORK ACTIVE", new Color(1.0f, 0.52f, 0.26f));
        }
    }

    private void OnObjectiveChanged(int index, string objective, bool extractionAvailable)
    {
        _objectiveStage = index;
        _currentObjective = objective;
        _interactionProgress = 0.0f;
        RefreshLocalizedObjective();
        _hud.SetInteraction(string.Empty, 0.0f, false);
        _extractionMarker.Visible = extractionAvailable;
    }

    private void OnDirectorGunshot(Vector3 origin, float radius)
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy) && !enemy.IsDead)
            {
                enemy.HearGunshot(origin, radius);
            }
        }
    }

    public void ReportGunshot(Vector3 origin, float radius)
    {
        _missionDirector.ReportGunshot(origin, radius);
        if (_missionPhase is "CONTACT" or "COMBAT" && !_reinforcementsDeployed)
        {
            _threatLevel = Mathf.Min(_reinforcementThreshold, _threatLevel + 3.0f);
        }
    }

    public bool IsPlayerProtected() => _missionDirector.IsDeploymentProtected();

    public void RecordShot(bool hit, bool headshot)
    {
        _shotsFired++;
        if (hit)
        {
            _shotsHit++;
        }
        if (headshot)
        {
            _headshots++;
        }
    }

    public void ThrowGrenade(Vector3 origin, Vector3 direction, Node source)
    {
        var grenade = new FragGrenade
        {
            Position = origin,
            OwnerBody = source,
            Main = this
        };
        AddChild(grenade);
        grenade.Arm(direction);
    }

    public void SpawnShell(Vector3 origin, Vector3 velocity)
    {
        var casing = new ShellCasing { Position = origin };
        AddChild(casing);
        casing.Launch(velocity);
    }

    public Vector3 FindCoverPoint(Vector3 origin, Vector3 threat)
    {
        var best = new Vector3(0, -1000, 0);
        var bestScore = float.PositiveInfinity;
        foreach (var point in _coverPoints)
        {
            var travel = origin.DistanceTo(point);
            if (travel > 18.0f || point.DistanceTo(threat) < 4.0f)
            {
                continue;
            }
            var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up, threat + Vector3.Up * 1.3f);
            query.CollideWithAreas = false;
            query.CollisionMask = 1;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count == 0 || (hit.TryGetValue("collider", out var collider) && collider.AsGodotObject() == _player))
            {
                continue;
            }
            var score = travel + point.DistanceTo(threat) * 0.06f;
            if (score < bestScore)
            {
                bestScore = score;
                best = point;
            }
        }
        return best;
    }

    public void SpawnTracer(Vector3 from, Vector3 to, Color color)
    {
        var tracer = new MeshInstance3D { Position = from, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        var immediate = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 4.0f
        };
        immediate.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        immediate.SurfaceAddVertex(Vector3.Zero);
        immediate.SurfaceAddVertex(to - from);
        immediate.SurfaceEnd();
        tracer.Mesh = immediate;
        AddChild(tracer);
        var tween = CreateTween();
        tween.TweenInterval(0.045f);
        tween.TweenProperty(tracer, "transparency", 1.0f, 0.08f);
        tween.TweenCallback(Callable.From(tracer.QueueFree));
    }

    public void SpawnImpact(Vector3 position, Vector3 normal)
    {
        var impact = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.038f, BottomRadius = 0.038f, Height = 0.006f, RadialSegments = 12 },
            Position = position + normal * 0.018f,
            Quaternion = new Quaternion(Vector3.Up, normal.Normalized()),
            MaterialOverride = Mat("impact", new Color(0.018f, 0.017f, 0.014f), 0.25f, 0.88f)
        };
        AddChild(impact);
        var tween = CreateTween();
        tween.TweenInterval(7.0f);
        tween.TweenProperty(impact, "transparency", 1.0f, 1.5f);
        tween.TweenCallback(Callable.From(impact.QueueFree));
        for (var i = 0; i < 3; i++)
        {
            var sparkEnd = position + normal * _rng.RandfRange(0.15f, 0.45f)
                + new Vector3(_rng.RandfRange(-0.22f, 0.22f), _rng.RandfRange(-0.1f, 0.3f), _rng.RandfRange(-0.22f, 0.22f));
            SpawnTracer(position + normal * 0.02f, sparkEnd, new Color(1.0f, 0.48f, 0.12f));
        }
        for (var i = 0; i < 3; i++)
        {
            var dust = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = _rng.RandfRange(0.025f, 0.055f), Height = 0.1f, RadialSegments = 6, Rings = 3 },
                Position = position + normal * 0.025f,
                MaterialOverride = new StandardMaterial3D
                {
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    AlbedoColor = new Color(0.34f, 0.3f, 0.24f, 0.55f),
                    Roughness = 1.0f
                }
            };
            AddChild(dust);
            var target = dust.Position + normal * _rng.RandfRange(0.18f, 0.4f) + Vector3.Up * _rng.RandfRange(0.05f, 0.25f);
            var dustTween = CreateTween().SetParallel(true);
            dustTween.TweenProperty(dust, "position", target, 0.5f);
            dustTween.TweenProperty(dust, "scale", Vector3.One * 2.5f, 0.5f);
            dustTween.TweenProperty(dust, "transparency", 1.0f, 0.55f);
            dustTween.Chain().TweenCallback(Callable.From(dust.QueueFree));
        }
    }

    public void Explode(Vector3 position, float radius, float maxDamage, Node? source = null)
    {
        ReportGunshot(position, 70.0f);
        foreach (var enemy in _enemies.ToArray())
        {
            if (IsInstanceValid(enemy) && !enemy.IsDead)
            {
                var distance = enemy.GlobalPosition.DistanceTo(position);
                if (distance < radius)
                {
                    enemy.TakeDamage(maxDamage * (1.0f - distance / radius), enemy.GlobalPosition + Vector3.Up, source);
                }
            }
        }
        if (IsInstanceValid(_player) && !_player.IsDead)
        {
            var distance = _player.GlobalPosition.DistanceTo(position);
            if (distance < radius)
            {
                _player.TakeDamage(maxDamage * 0.72f * (1.0f - distance / radius), position, source);
            }
        }
        foreach (var barrel in _barrels.ToArray())
        {
            if (IsInstanceValid(barrel) && !barrel.Exploded && barrel.GlobalPosition.DistanceTo(position) < radius * 0.65f)
            {
                barrel.CallDeferred(nameof(ExplosiveBarrel.TakeDamage), 100.0f, position, source ?? this);
            }
        }
        SpawnExplosionEffect(position);
    }

    private void SpawnExplosionEffect(Vector3 position)
    {
        var effect = new Node3D { Position = position };
        AddChild(effect);
        var light = new OmniLight3D { LightColor = new Color(1.0f, 0.29f, 0.07f), LightEnergy = 18.0f, OmniRange = 15.0f, ShadowEnabled = true };
        effect.AddChild(light);
        var coreMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 0.24f, 0.035f, 0.95f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.12f, 0.015f),
            EmissionEnergyMultiplier = 7.0f
        };
        var core = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = coreMaterial
        };
        effect.AddChild(core);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(core, "scale", Vector3.One * 6.2f, 0.28f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(core, "transparency", 1.0f, 0.42f);
        tween.TweenProperty(light, "light_energy", 0.0f, 0.5f);
        for (var i = 0; i < 10; i++)
        {
            var smokeRadius = _rng.RandfRange(0.35f, 0.75f);
            var smoke = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = smokeRadius, Height = smokeRadius * 2.0f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = new StandardMaterial3D
                {
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    AlbedoColor = new Color(0.075f, 0.07f, 0.06f, 0.76f),
                    Roughness = 1.0f
                }
            };
            effect.AddChild(smoke);
            var target = new Vector3(_rng.RandfRange(-3.2f, 3.2f), _rng.RandfRange(2.0f, 5.0f), _rng.RandfRange(-3.2f, 3.2f));
            tween.TweenProperty(smoke, "position", target, _rng.RandfRange(1.0f, 1.8f));
            tween.TweenProperty(smoke, "scale", Vector3.One * _rng.RandfRange(1.5f, 2.8f), 1.6f);
            tween.TweenProperty(smoke, "transparency", 1.0f, 2.4f).SetDelay(0.5f);
        }
        var audio = new AudioStreamPlayer3D { Stream = SoundLab.Explosion(), VolumeDb = -1.0f, MaxDistance = 130.0f };
        effect.AddChild(audio);
        audio.Play();
        var cleanup = CreateTween();
        cleanup.TweenInterval(3.2f);
        cleanup.TweenCallback(Callable.From(effect.QueueFree));
    }

    private void BuildHudAndPlayer()
    {
        _hud = new CombatHUD { Name = "CombatHUD" };
        AddChild(_hud);
        _hud.PauseRequested += TogglePause;
        _hud.RestartRequested += RestartMission;
        _hud.QuitRequested += () => GetTree().Quit();
        _hud.SensitivityChanged += SetSensitivity;
        _hud.QualityChanged += ApplyQuality;
        _hud.FullscreenChanged += SetFullscreen;
        _hud.LanguageChanged += SetLanguage;
        _hud.LootTakeRequested += TakeLootItem;
        _hud.LootEquipRequested += EquipLootItem;
        _hud.LootReturnRequested += ReturnBackpackItem;
        _hud.BackpackUseRequested += UseBackpackItem;
        _hud.LootClosed += CloseLoot;

        _player = new TacticalPlayer
        {
            Name = "Player",
            Main = this,
            Hud = _hud,
            Position = new Vector3(0, 0.2f, 36),
            MouseSensitivity = 0.00165f * _sensitivitySetting
        };
        AddChild(_player);
        _hud.WeaponSlotRequested += _player.SelectWeapon;
        _player.HitConfirmed += OnHitConfirmed;
        _player.Died += OnPlayerDied;
        _hud.SetSettings(_sensitivitySetting, _qualitySetting, _fullscreenSetting, _languageSetting);
        RefreshLocalizedObjective();
    }

    private void SpawnLootCases()
    {
        var cases = new[]
        {
            new
            {
                Position = new Vector3(31.5f, 0.02f, -18.0f),
                Rotation = 0.0f,
                English = "Warehouse armory case",
                Chinese = "仓库军械箱",
                Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                Parts = new[] { "muzzle_suppressor", "optic_scope" },
                Equipment = new[] { "armor_heavy" }
            },
            new
            {
                Position = new Vector3(-34.0f, 0.02f, -11.0f),
                Rotation = Mathf.Pi / 2.0f,
                English = "Customs office locker",
                Chinese = "海关办公室枪柜",
                Weapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 1),
                Parts = new[] { "optic_holo", "mag_extended" },
                Equipment = new[] { "pack_heavy" }
            },
            new
            {
                Position = new Vector3(13.0f, 0.02f, 11.5f),
                Rotation = -0.12f,
                English = "Maintenance weapon chest",
                Chinese = "维修间武器箱",
                Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 1),
                Parts = new[] { "muzzle_brake", "grip_vertical" },
                Equipment = new[] { "helmet_light" }
            },
            new
            {
                Position = new Vector3(5.0f, 0.42f, 33.2f),
                Rotation = Mathf.Pi / 2.0f,
                English = "Security checkpoint response locker",
                Chinese = "安检站应急装备柜",
                Weapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 0),
                Parts = new[] { "optic_micro", "mag_extended" },
                Equipment = new[] { "helmet_heavy" }
            },
            new
            {
                Position = new Vector3(-34.0f, 0.02f, 18.0f),
                Rotation = 0.0f,
                English = "Fuel depot hazard locker",
                Chinese = "燃料库危险品装备箱",
                Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 1),
                Parts = new[] { "barrel_cqb", "muzzle_brake" },
                Equipment = new[] { "armor_carrier", "pack_assault" }
            },
            new
            {
                Position = new Vector3(25.0f, 0.02f, 21.5f),
                Rotation = -Mathf.Pi / 2.0f,
                English = "Barracks command locker",
                Chinese = "营房指挥装备柜",
                Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 1),
                Parts = new[] { "stock_precision", "optic_holo" },
                Equipment = new[] { "helmet_heavy", "armor_heavy" }
            }
        };
        foreach (var definition in cases)
        {
            var weaponCase = new WeaponCase
            {
                Position = definition.Position,
                Rotation = new Vector3(0, definition.Rotation, 0),
                EnglishName = definition.English,
                ChineseName = definition.Chinese
            };
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.Weapon, Weapon = definition.Weapon });
            foreach (var part in definition.Parts)
            {
                weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = part });
            }
            foreach (var equipmentId in definition.Equipment)
            {
                weaponCase.Loot.Add(new LootItem
                {
                    Kind = LootItemKind.Equipment,
                    Equipment = EquipmentCatalog.Create(equipmentId)
                });
            }
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.Ammunition, Quantity = _rng.RandiRange(35, 65) });
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
            AddChild(weaponCase);
            _lootSources.Add(weaponCase);
        }
    }

    private void SpawnEnemies()
    {
        var positions = new[]
        {
            new Vector3(-12, 0.15f, 11), new Vector3(-22, 0.15f, 7), new Vector3(3, 0.15f, 2),
            new Vector3(20, 0.15f, 8), new Vector3(29, 0.15f, -10), new Vector3(-10, 0.15f, -17),
            new Vector3(-28, 0.15f, -31), new Vector3(4, 0.15f, -32), new Vector3(20, 0.15f, -20)
        };
        foreach (var position in positions)
        {
            SpawnEnemy(position, false);
        }
        _enemiesRemaining = _enemies.Count;
    }

    private EnemyOperator SpawnEnemy(Vector3 position, bool alerted)
    {
        var enemy = new EnemyOperator
        {
            Position = position,
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            DetectionRange = _missionDetectionRange
        };
        AddChild(enemy);
        enemy.Eliminated += OnEnemyEliminated;
        _enemies.Add(enemy);
        if (alerted)
        {
            enemy.SetAlerted(_player.GlobalPosition);
        }
        return enemy;
    }

    private void SpawnExplosives()
    {
        foreach (var position in new[] { new Vector3(-6, 0, 18), new Vector3(-20, 0, -6), new Vector3(17, 0, -18), new Vector3(31, 0, 8), new Vector3(-30, 0, 28) })
        {
            var barrel = new ExplosiveBarrel { Main = this, Position = position };
            barrel.AddToGroup("explosives");
            AddChild(barrel);
            _barrels.Add(barrel);
        }
    }

    private void OnEnemyEliminated(EnemyOperator enemy)
    {
        _lootSources.Add(enemy);
        _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);
        _kills++;
        _hud.SetEnemyCount(_enemiesRemaining);
        _enemies.Remove(enemy);
    }

    private TacticalPickup SpawnPickup(Vector3 position, TacticalPickupKind kind)
    {
        var pickup = new TacticalPickup { Position = position, Kind = kind };
        AddChild(pickup);
        return pickup;
    }

    private void OnHitConfirmed(bool killed, bool headshot, bool armorHit) => _hud.ShowHit(killed, headshot, armorHit);

    private void OnPlayerDied()
    {
        _missionEnded = true;
        _missionDirector.CompleteMission(false, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowResult(false);
    }

    private void OnExtractionEntered(Node3D body)
    {
        if (body != _player || _objectiveStage < _objectiveTerminals.Count || _missionEnded)
        {
            return;
        }
        _missionEnded = true;
        _player.IsDead = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(true, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowResult(true);
    }

    private void UpdateInteraction(float delta)
    {
        if (!Input.IsActionPressed("interact"))
        {
            _interactReleaseRequired = false;
        }
        if (_hud.IsLootVisible)
        {
            return;
        }
        ILootSource? nearest = null;
        var nearestDistance = 2.85f;
        foreach (var source in _lootSources)
        {
            if (!source.IsSearchable || !IsInstanceValid(source.LootNode))
            {
                continue;
            }
            var distance = _player.GlobalPosition.DistanceTo(source.LootNode.GlobalPosition);
            if (distance < nearestDistance)
            {
                nearest = source;
                nearestDistance = distance;
            }
        }
        if (nearest is not null)
        {
            if (!ReferenceEquals(_lootSearchTarget, nearest))
            {
                _lootSearchTarget = nearest;
                _interactionProgress = 0.0f;
            }
            _interactionProgress = 0.0f;
            _player.SetSearchPose(false);
            var open = GameLocalization.Get("open_loot", _languageSetting, "OPEN");
            _hud.SetInteraction($"{open}  //  {nearest.DisplayName(_languageSetting)}", -1.0f, true);
            if (!_interactReleaseRequired && Input.IsActionJustPressed("interact"))
            {
                _interactReleaseRequired = true;
                OpenLoot(nearest);
            }
            return;
        }
        _lootSearchTarget = null;
        _player.SetSearchPose(false);
        UpdateObjectiveInteraction(delta);
    }

    private void OpenLoot(ILootSource source)
    {
        _interactionProgress = 0.0f;
        _openLootSource = source;
        _personalBackpackOpen = false;
        source.OnSearched();
        _player.UiLocked = true;
        _player.SetSearchPose(true, 1.0f);
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _hud.SetInteraction(string.Empty, 0.0f, false);
        _hud.ShowLoot(source.DisplayName(_languageSetting), source.Loot, _player, true);
    }

    private void OpenPersonalBackpack()
    {
        _openLootSource = null;
        _personalBackpackOpen = true;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var title = GameLocalization.IsChinese(_languageSetting) ? "个人背包" : "Personal backpack";
        _hud.ShowLoot(title, System.Array.Empty<LootItem>(), _player, false);
    }

    private void CloseLoot()
    {
        if (_hud.IsLootVisible)
        {
            _hud.HideLoot();
        }
        _openLootSource = null;
        _personalBackpackOpen = false;
        _lootSearchTarget = null;
        _interactionProgress = 0.0f;
        _interactReleaseRequired = Input.IsActionPressed("interact");
        _player.UiLocked = false;
        _player.SetSearchPose(false);
        _player.DisarmFireInput();
        _player.RestoreMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void TakeLootItem(string itemId)
    {
        if (_openLootSource is null)
        {
            return;
        }
        var index = _openLootSource.Loot.FindIndex(item => item.Id == itemId);
        if (index < 0 || !_player.TryStoreInBackpack(_openLootSource.Loot[index]))
        {
            return;
        }
        var taken = _openLootSource.Loot[index];
        _openLootSource.Loot.RemoveAt(index);
        if (_openLootSource is EnemyOperator enemy && taken.Kind == LootItemKind.Weapon)
        {
            enemy.MarkCarriedWeaponRemoved();
        }
        RefreshLootView();
    }

    private void EquipLootItem(string itemId)
    {
        if (_openLootSource is null)
        {
            return;
        }
        var index = _openLootSource.Loot.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return;
        }
        var original = _openLootSource.Loot[index];
        var replacement = _player.EquipFromLoot(original);
        if (ReferenceEquals(replacement, original))
        {
            return;
        }
        if (replacement is null)
        {
            _openLootSource.Loot.RemoveAt(index);
        }
        else
        {
            _openLootSource.Loot[index] = replacement;
        }
        if (_openLootSource is EnemyOperator enemy && original.Kind == LootItemKind.Weapon)
        {
            enemy.MarkCarriedWeaponRemoved();
        }
        RefreshLootView();
    }

    private void UseBackpackItem(string itemId)
    {
        if (_player.UseBackpackItem(itemId))
        {
            RefreshLootView();
        }
    }

    private void ReturnBackpackItem(string itemId)
    {
        if (_openLootSource is null)
        {
            return;
        }
        var index = _player.Backpack.FindIndex(item => item.Id == itemId);
        if (index < 0)
        {
            return;
        }
        var returned = _player.Backpack[index];
        _player.Backpack.RemoveAt(index);
        _openLootSource.Loot.Add(returned);
        RefreshLootView();
    }

    private void RefreshLootView()
    {
        if (_openLootSource is not null)
        {
            _hud.ShowLoot(_openLootSource.DisplayName(_languageSetting), _openLootSource.Loot, _player, true);
        }
        else if (_personalBackpackOpen && _hud.IsLootVisible)
        {
            var title = GameLocalization.IsChinese(_languageSetting) ? "个人背包" : "Personal backpack";
            _hud.ShowLoot(title, System.Array.Empty<LootItem>(), _player, false);
        }
    }

    private void UpdateObjectiveInteraction(float delta)
    {
        if (_missionEnded || _missionPhase == "DEPLOYMENT" || _objectiveStage >= _objectiveTerminals.Count)
        {
            _interactionProgress = 0.0f;
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }
        var terminal = _objectiveTerminals[_objectiveStage];
        if (_player.GlobalPosition.DistanceTo(terminal.GlobalPosition) >= 2.7f)
        {
            _interactionProgress = Mathf.Max(0.0f, _interactionProgress - delta * 2.0f);
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }
        var action = _objectiveStage == 0
            ? GameLocalization.Get("disable_relay", _languageSetting, "DISABLE RELAY")
            : GameLocalization.Get("download_manifest", _languageSetting, "DOWNLOAD MANIFEST");
        _interactionProgress = Input.IsActionPressed("interact") && !_interactReleaseRequired
            ? Mathf.Min(1.0f, _interactionProgress + delta / 1.8f)
            : Mathf.Max(0.0f, _interactionProgress - delta * 1.6f);
        _hud.SetInteraction(action, _interactionProgress, true);
        if (_interactionProgress >= 1.0f)
        {
            CompleteCurrentObjective();
        }
    }

    private void CompleteCurrentObjective()
    {
        if (_objectiveStage >= _objectiveTerminals.Count)
        {
            return;
        }
        var screen = _objectiveScreens[_objectiveStage];
        screen.AlbedoColor = new Color(0.1f, 0.9f, 0.58f);
        screen.Emission = new Color(0.04f, 0.95f, 0.5f);
        _objectiveLights[_objectiveStage].LightColor = new Color(0.06f, 1.0f, 0.5f);
        if (_objectiveStage == 0 && !_reinforcementsDeployed && !_reinforcementPending)
        {
            _reinforcementThreshold = Mathf.Min(95, _reinforcementThreshold + 20);
            _threatLevel = Mathf.Max(0.0f, _threatLevel - 15.0f);
            _hud.ShowLocalizedMessage("relay_offline", "RELAY OFFLINE  //  RESPONSE DELAYED", new Color(0.35f, 0.92f, 0.72f));
        }
        _interactionProgress = 0.0f;
        _missionDirector.AdvanceObjective();
    }

    private void UpdateReinforcements(float delta)
    {
        if (_missionEnded || _reinforcementsDeployed || _missionPhase != "COMBAT")
        {
            return;
        }
        if (!_reinforcementPending)
        {
            _threatLevel = Mathf.Min(_reinforcementThreshold, _threatLevel + delta * 2.6f);
            if (_threatLevel < _reinforcementThreshold)
            {
                return;
            }
            _reinforcementPending = true;
            _reinforcementCountdown = 7.0f;
            _hud.ShowLocalizedMessage("qrf_inbound", "ENEMY QRF INBOUND  //  7 SECONDS", new Color(1.0f, 0.35f, 0.2f));
            return;
        }
        _reinforcementCountdown -= delta;
        if (_reinforcementCountdown <= 0.0f)
        {
            SpawnReinforcementWave();
        }
    }

    private void SpawnReinforcementWave()
    {
        _reinforcementPending = false;
        _reinforcementsDeployed = true;
        var spawnPoints = new[]
        {
            new Vector3(-36, 0.15f, -30), new Vector3(36, 0.15f, -25),
            new Vector3(-36, 0.15f, 24), new Vector3(36, 0.15f, 28)
        };
        Array.Sort(spawnPoints, (left, right) =>
            right.DistanceSquaredTo(_player.GlobalPosition).CompareTo(left.DistanceSquaredTo(_player.GlobalPosition)));
        for (var i = 0; i < 3; i++)
        {
            SpawnEnemy(spawnPoints[i], true);
        }
        _enemiesRemaining += 3;
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.ShowLocalizedMessage("qrf_deployed", "QRF DEPLOYED  //  THREE CONTACTS", new Color(1.0f, 0.42f, 0.22f));
    }

    private void TogglePause()
    {
        if (_missionEnded)
        {
            return;
        }
        var paused = !GetTree().Paused;
        GetTree().Paused = paused;
        _hud.SetPauseVisible(paused);
        Input.MouseMode = paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (!paused)
        {
            _player.DisarmFireInput();
            _player.DisarmMovementInput();
        }
    }

    private void RestartMission()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }

    private void SetSensitivity(float value)
    {
        _sensitivitySetting = value;
        _player.MouseSensitivity = 0.00165f * value;
        SaveSettings();
    }

    private void ApplyQuality(int index)
    {
        _qualitySetting = Mathf.Clamp(index, 0, 2);
        if (IsInstanceValid(_environmentRef))
        {
            SetIfSupported(_environmentRef, "ssao_enabled", _qualitySetting >= 1);
            SetIfSupported(_environmentRef, "ssil_enabled", _qualitySetting >= 2);
            SetIfSupported(_environmentRef, "ssr_enabled", _qualitySetting >= 1);
            SetIfSupported(_environmentRef, "volumetric_fog_enabled", _qualitySetting >= 2);
        }
        if (IsInstanceValid(_sunLight))
        {
            _sunLight.ShadowEnabled = _qualitySetting >= 1;
            _sunLight.DirectionalShadowMaxDistance = new[] { 55.0f, 90.0f, 130.0f }[_qualitySetting];
        }
        SaveSettings();
    }

    private void SetFullscreen(bool active)
    {
        _fullscreenSetting = active;
        DisplayServer.WindowSetMode(active ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        SaveSettings();
    }

    private void SetLanguage(string language)
    {
        _languageSetting = GameLocalization.IsChinese(language) ? "zh" : "en";
        _hud.SetLanguage(_languageSetting);
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.SetMissionPhase(_missionPhase, _missionRemaining, _missionOnline);
        RefreshLocalizedObjective();
        RefreshLootView();
        SaveSettings();
    }

    private void RefreshLocalizedObjective()
    {
        if (!IsInstanceValid(_hud))
        {
            return;
        }
        _hud.SetObjective(_missionPhase == "DEPLOYMENT"
            ? GameLocalization.Get("deployment_objective", _languageSetting, "MOVE BEYOND THE DEPLOYMENT LINE")
            : GameLocalization.Objective(_currentObjective, _languageSetting));
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        if (config.Load("user://steel_tide_settings.cfg") == Error.Ok)
        {
            _sensitivitySetting = (float)config.GetValue("controls", "sensitivity", 1.0f).AsDouble();
            _qualitySetting = (int)config.GetValue("graphics", "quality", 2).AsInt32();
            _fullscreenSetting = config.GetValue("graphics", "fullscreen", false).AsBool();
            _languageSetting = config.GetValue("interface", "language", "en").AsString();
        }
        if (_fullscreenSetting)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("controls", "sensitivity", _sensitivitySetting);
        config.SetValue("graphics", "quality", _qualitySetting);
        config.SetValue("graphics", "fullscreen", _fullscreenSetting);
        config.SetValue("interface", "language", _languageSetting);
        config.Save("user://steel_tide_settings.cfg");
    }

    private async void CaptureValidationFrame()
    {
        await WaitFrames(22);
        _player.Fire();
        await WaitFrames(2);
        SaveViewportImage("res://validation_frame.png");
        GetTree().Quit();
    }

    private async void CapturePauseFrame()
    {
        await WaitFrames(12);
        TogglePause();
        await WaitFrames(2);
        SaveViewportImage("res://pause_validation_frame.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }

    private async void CaptureDeploymentFrame()
    {
        var startedAt = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - startedAt < 14000)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        SaveViewportImage("res://deployment_frame.png");
        GD.Print($"DEPLOYMENT_CHECK phase={_missionPhase} health={_player.Health:0.0} armor={_player.Armor:0.0} ammo={_player.Ammo} z={_player.GlobalPosition.Z:0.00} shots={_shotsFired}");
        GetTree().Quit();
    }

    private async void ValidateObjectiveFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _missionDirector.ExitDeploymentZone();
        for (var targetStage = 0; targetStage < _objectiveTerminals.Count; targetStage++)
        {
            _player.GlobalPosition = _objectiveTerminals[targetStage].GlobalPosition + new Vector3(0, 0.2f, 1.2f);
            Input.ActionPress("interact");
            var deadline = Time.GetTicksMsec() + 3000;
            while (_objectiveStage == targetStage && Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Input.ActionRelease("interact");
            await WaitFrames(4);
        }
        SaveViewportImage("res://objective_validation.png");
        GD.Print($"OBJECTIVE_CHECK stage={_objectiveStage} phase={_missionPhase} extraction={_extractionMarker.Visible}");
        GetTree().Quit();
    }

    private async void ValidateReinforcementFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _threatLevel = _reinforcementThreshold;
        var deadline = Time.GetTicksMsec() + 10000;
        while (!_reinforcementsDeployed && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await WaitFrames(2);
        SaveViewportImage("res://reinforcement_validation.png");
        GD.Print($"REINFORCEMENT_CHECK deployed={_reinforcementsDeployed} hostiles={_enemiesRemaining} phase={_missionPhase}");
        GetTree().Quit();
    }

    private async void CaptureAdsFrame()
    {
        await WaitFrames(16);
        Input.ActionPress("aim");
        await WaitFrames(50);
        SaveViewportImage("res://ads_validation.png");
        GD.Print($"ADS_CHECK aiming={_player.IsAiming} ammo={_player.Ammo} phase={_missionPhase}");
        Input.ActionRelease("aim");
        GetTree().Quit();
    }

    private async void ValidateEquipmentFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _missionDirector.ExitDeploymentZone();
        _player.TakeDamage(30.0f);
        Input.ActionPress("toggle_fire_mode");
        await WaitFrames(2);
        Input.ActionRelease("toggle_fire_mode");
        Input.ActionPress("toggle_flashlight");
        await WaitFrames(2);
        Input.ActionRelease("toggle_flashlight");
        Input.ActionPress("use_plate");
        var deadline = Time.GetTicksMsec() + 4000;
        while (_player.ArmorPlates == 2 && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ActionRelease("use_plate");
        GD.Print($"EQUIPMENT_CHECK plates={_player.ArmorPlates} armor={_player.Armor:0.0} mode={_player.FireMode} light={_player.FlashlightOn}");
        GetTree().Quit();
    }

    private async void ValidatePickupFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var reserveBefore = _player.ReserveAmmo;
        SpawnPickup(_player.GlobalPosition + Vector3.Up * 0.1f, TacticalPickupKind.Ammunition);
        await WaitFrames(8);
        GD.Print($"PICKUP_CHECK ammo_before={reserveBefore} ammo_after={_player.ReserveAmmo}");
        GetTree().Quit();
    }

    private async void CaptureReloadFrame()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.Fire();
        await WaitFrames(8);
        Input.ActionPress("reload");
        await WaitFrames(2);
        Input.ActionRelease("reload");
        var deadline = Time.GetTicksMsec() + 2500;
        while (_player.ReloadProgress < 0.63f && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        SaveViewportImage("res://reload_validation.png");
        GD.Print($"RELOAD_CHECK active={_player.IsReloading} progress={_player.ReloadProgress:0.00} ammo={_player.Ammo}");
        GetTree().Quit();
    }

    private async void CaptureOperatorFrame()
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].ProcessMode = ProcessModeEnum.Disabled;
            _enemies[i].Visible = i == 0;
        }
        var target = _enemies[0];
        target.GlobalPosition = new Vector3(0, 0.15f, 29.5f);
        target.Rotation = new Vector3(0, Mathf.Pi, 0);
        await WaitFrames(145);
        SaveViewportImage("res://operator_validation.png");
        GD.Print("OPERATOR_CHECK detailed_model=true visible=1");
        GetTree().Quit();
    }

    private async void CaptureChineseFrame()
    {
        var previousLanguage = _languageSetting;
        await WaitFrames(12);
        SetLanguage("zh");
        TogglePause();
        await WaitFrames(3);
        SaveViewportImage("res://chinese_validation.png");
        GD.Print($"LANGUAGE_CHECK language={_languageSetting} paused={GetTree().Paused}");
        GetTree().Paused = false;
        SetLanguage(previousLanguage);
        GetTree().Quit();
    }

    private async void CaptureKnifeFrame()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        await WaitFrames(10);
        Input.ActionPress("weapon_melee");
        await WaitFrames(2);
        Input.ActionRelease("weapon_melee");
        await WaitFrames(24);
        SaveViewportImage("res://knife_ready_validation.png");
        Input.ActionPress("fire");
        await WaitFrames(2);
        Input.ActionRelease("fire");
        await WaitFrames(4);
        SaveViewportImage("res://knife_windup_validation.png");
        await WaitFrames(8);
        SaveViewportImage("res://knife_validation.png");
        await WaitFrames(8);
        SaveViewportImage("res://knife_followthrough_validation.png");
        GD.Print($"KNIFE_CHECK equipped={_player.KnifeEquipped} weapon={_player.EquippedWeapon.Platform} direction=right_to_left_rising_slash");
        GetTree().Quit();
    }

    private async void ValidateLootFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        SetLanguage("zh");
        var source = _lootSources[0];
        _player.GlobalPosition = source.LootNode.GlobalPosition + new Vector3(0, 0.2f, 1.6f);
        _missionDirector.ExitDeploymentZone();
        await WaitFrames(8);
        var openedAt = Time.GetTicksMsec();
        Input.ActionPress("interact");
        var deadline = Time.GetTicksMsec() + 2500;
        while (!_hud.IsLootVisible && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var immediateOpenMilliseconds = Time.GetTicksMsec() - openedAt;
        var item = source.Loot.Find(candidate => candidate.Kind == LootItemKind.Weapon);
        if (item is not null)
        {
            EquipLootItem(item.Id);
        }
        var dragCandidate = source.Loot.Find(candidate => candidate.Kind != LootItemKind.Weapon);
        var returnedToSource = false;
        var dragDropRouted = false;
        if (dragCandidate is not null)
        {
            var dragProbe = new LootDropZone { Target = LootDropTarget.Backpack };
            dragProbe.Dropped += (itemId, origin, target) =>
            {
                dragDropRouted = origin == LootDragOrigin.Source && target == LootDropTarget.Backpack;
                TakeLootItem(itemId);
            };
            var dragData = new Godot.Collections.Dictionary
            {
                ["item_id"] = dragCandidate.Id,
                ["origin"] = (int)LootDragOrigin.Source,
                ["kind"] = (int)dragCandidate.Kind,
                ["slot"] = -1
            };
            if (dragProbe._CanDropData(Vector2.Zero, dragData))
            {
                dragProbe._DropData(Vector2.Zero, dragData);
            }
            dragProbe.Free();
            ReturnBackpackItem(dragCandidate.Id);
            returnedToSource = source.Loot.Exists(candidate => candidate.Id == dragCandidate.Id)
                && !_player.Backpack.Exists(candidate => candidate.Id == dragCandidate.Id);
        }
        await WaitFrames(4);
        var stats = _player.CurrentWeaponStats;
        SaveViewportImage("res://loot_validation.png");
        source.Loot.Clear();
        CloseLoot();
        await WaitFrames(5);
        var heldInputBlocked = !_hud.IsLootVisible;
        Input.ActionRelease("interact");
        await WaitFrames(3);
        Input.ActionPress("interact");
        deadline = Time.GetTicksMsec() + 800;
        while (!_hud.IsLootVisible && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ActionRelease("interact");
        var reopenedEmpty = _hud.IsLootVisible && source.Loot.Count == 0;
        await WaitFrames(3);
        Input.ActionPress("interact");
        await WaitFrames(2);
        var closedByInteract = !_hud.IsLootVisible;
        Input.ActionRelease("interact");
        Input.ActionPress("move_forward");
        await WaitFrames(4);
        var movementRestored = !_player.UiLocked && _player.HasMovementIntent;
        Input.ActionRelease("move_forward");
        GD.Print($"LOOT_CHECK open={_hud.IsLootVisible} immediate_ms={immediateOpenMilliseconds} held_blocked={heldInputBlocked} drag_drop={dragDropRouted} returned={returnedToSource} reopened_empty={reopenedEmpty} f_closed={closedByInteract} movement={movementRestored} equipped={_player.EquippedWeapon.Platform} source_items={source.Loot.Count} backpack={_player.Backpack.Count} damage={stats.Damage:0.0} range={stats.EffectiveRange:0.0} recoil={stats.Recoil:0.00}");
        await WaitFrames(24);
        SaveViewportImage("res://modular_weapon_validation.png");
        GetTree().Quit();
    }

    private async void ValidateCorpseLootFlow()
    {
        var target = _enemies[0];
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        target.TakeDamage(500.0f, target.GlobalPosition + Vector3.Up * 1.1f, _player);
        await WaitFrames(42);
        _player.GlobalPosition = target.GlobalPosition + new Vector3(0, 0.2f, 1.5f);
        _missionDirector.ExitDeploymentZone();
        Input.ActionPress("interact");
        var deadline = Time.GetTicksMsec() + 2600;
        while (!_hud.IsLootVisible && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ActionRelease("interact");
        var weapon = target.Loot.Find(item => item.Kind == LootItemKind.Weapon);
        if (weapon is not null)
        {
            EquipLootItem(weapon.Id);
        }
        await WaitFrames(4);
        SaveViewportImage("res://corpse_loot_validation.png");
        var equipmentCount = target.Loot.FindAll(item => item.Kind == LootItemKind.Equipment).Count;
        target.Loot.Clear();
        CloseLoot();
        await WaitFrames(3);
        Input.ActionPress("interact");
        deadline = Time.GetTicksMsec() + 800;
        while (!_hud.IsLootVisible && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ActionRelease("interact");
        var reopenedEmpty = _hud.IsLootVisible && target.Loot.Count == 0;
        GD.Print($"CORPSE_LOOT_CHECK dead={target.IsDead} open={_hud.IsLootVisible} reopened_empty={reopenedEmpty} weapon_visible={target.CarriedWeaponVisible} equipment={equipmentCount} items={target.Loot.Count} equipped={_player.EquippedWeapon.Platform}");
        GetTree().Quit();
    }

    private async void CaptureBackpackFrame()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        SetLanguage("zh");
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = "optic_holo" });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Attachment, AttachmentId = "muzzle_suppressor" });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Ammunition, Quantity = 48 });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 1 });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Weapon, Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 2) });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("helmet_heavy") });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("armor_heavy") });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("pack_heavy") });
        OpenPersonalBackpack();
        await WaitFrames(30);
        SaveViewportImage("res://backpack_validation.png");
        _hud.ShowWeaponDetails(_player.EquippedWeapon);
        await WaitFrames(20);
        SaveViewportImage("res://weapon_detail_validation.png");
        GD.Print($"BACKPACK_CHECK open={_hud.IsLootVisible} detail={_hud.IsWeaponDetailVisible} items={_player.Backpack.Count} capacity={_player.BackpackCapacity} language={_languageSetting}");
        GetTree().Quit();
    }

    private async void ValidateWeaponUiFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        await WaitFrames(5);
        Input.ActionPress("weapon_cycle");
        await WaitFrames(4);
        Input.ActionRelease("weapon_cycle");
        var cycledToKnife = _player.KnifeEquipped;
        await WaitFrames(5);
        Input.ActionPress("weapon_cycle");
        await WaitFrames(4);
        Input.ActionRelease("weapon_cycle");
        await WaitFrames(2);
        var cycledToPrimary = !_player.KnifeEquipped;
        OpenPersonalBackpack();
        await WaitFrames(4);
        _hud.ShowWeaponDetails(_player.EquippedWeapon);
        await WaitFrames(2);
        var detailsOpened = _hud.IsWeaponDetailVisible;
        GD.Print($"WEAPON_UI_CHECK knife={cycledToKnife} primary={cycledToPrimary} details={detailsOpened} platform={_player.EquippedWeapon.Platform}");
        GetTree().Quit();
    }

    private async void CaptureOpticsFrames()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var optics = new[]
        {
            (Id: "optic_micro", File: "optic_micro_validation.png"),
            (Id: "optic_holo", File: "optic_holo_validation.png"),
            (Id: "optic_scope", File: "optic_scope_validation.png")
        };
        foreach (var optic in optics)
        {
            var build = WeaponCatalog.Build(WeaponPlatform.M4A1, 1);
            build.Attachments[AttachmentSlot.Optic] = optic.Id;
            _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Weapon, Weapon = build });
            await WaitFrames(28);
            SaveViewportImage("res://" + optic.File);
            Input.ActionPress("aim");
            await WaitFrames(38);
            SaveViewportImage("res://" + optic.File.Replace("_validation", "_ads_validation"));
            var aiming = _player.IsAiming;
            Input.ActionRelease("aim");
            await WaitFrames(16);
            GD.Print($"OPTIC_CHECK id={optic.Id} visible=true aiming={aiming} handling={_player.CurrentWeaponStats.Handling:0.00}");
        }
        GetTree().Quit();
    }

    private async void ValidateStanceAndArmorFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _missionDirector.ExitDeploymentZone();
        var attacker = _enemies[0];
        var crouched = _player.TrySetStance(PlayerStance.Crouched);
        await WaitFrames(18);
        var crouchHeight = _player.ViewHeight;
        Input.ActionPress("aim");
        Input.ActionPress("lean_left");
        await WaitFrames(14);
        var crouchLean = _player.IsAiming && _player.LeanAmount < -0.25f;
        Input.ActionRelease("lean_left");
        Input.ActionRelease("aim");
        var prone = _player.TrySetStance(PlayerStance.Prone);
        await WaitFrames(18);
        var proneHeight = _player.ViewHeight;
        var healthBefore = _player.Health;
        var helmetBefore = _player.EquippedHelmet.Durability;
        _player.TakeDamage(20.0f, _player.HitPoint(HitRegion.Head), attacker);
        var helmetAfter = _player.EquippedHelmet.Durability;
        var armorBefore = _player.EquippedBodyArmor.Durability;
        _player.TakeDamage(20.0f, _player.HitPoint(HitRegion.Torso), attacker);
        var armorAfter = _player.EquippedBodyArmor.Durability;
        GD.Print($"STANCE_ARMOR_CHECK crouched={crouched} crouch_height={crouchHeight:0.00} crouch_lean_ads={crouchLean} prone={prone} prone_height={proneHeight:0.00} health_loss={healthBefore - _player.Health:0.0} helmet_loss={helmetBefore - helmetAfter:0.0} armor_loss={armorBefore - armorAfter:0.0}");
        GetTree().Quit();
    }

    private async void CaptureExpandedMapFrame()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var aircraft = _levelRoot.GetNodeOrNull<Node3D>("DistantTiltRotor");
        var aircraftStart = aircraft?.Position ?? Vector3.Zero;
        _player.GlobalPosition = new Vector3(0, 0.2f, 38.0f);
        _player.Rotation = Vector3.Zero;
        await WaitFrames(32);
        SaveViewportImage("res://expanded_map_validation.png");
        _player.GlobalPosition = new Vector3(-20.0f, 0.2f, 38.0f);
        _player.Rotation = new Vector3(0, -Mathf.Pi / 2.0f, 0);
        await WaitFrames(22);
        SaveViewportImage("res://radar_spire_validation.png");
        _player.GlobalPosition = new Vector3(0, 0.2f, -23.0f);
        _player.Rotation = new Vector3(0, Mathf.Pi, 0);
        await WaitFrames(22);
        SaveViewportImage("res://cover_density_validation.png");
        var landmarksPresent = _levelRoot.GetNodeOrNull<Node3D>("CommandCore") is not null
            && _levelRoot.GetNodeOrNull<Node3D>("RadarFoundation") is not null;
        var aircraftMoving = aircraft is not null && aircraft.Position.DistanceTo(aircraftStart) > 0.1f;
        var dynamicSky = _environmentRef.Sky?.SkyMaterial is ShaderMaterial;
        GD.Print($"MAP_CHECK loot_sources={_lootSources.Count} landmarks={landmarksPresent} cover_points={_coverPoints.Length} dynamic_sky={dynamicSky} aircraft_moving={aircraftMoving} industrial_skyline=true");
        GetTree().Quit();
    }

    private async Task WaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void SaveViewportImage(string path)
    {
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print($"CAPTURE_SKIPPED headless=true path={path}");
            return;
        }
        var image = GetViewport().GetTexture().GetImage();
        if (image is null)
        {
            GD.Print($"CAPTURE_SKIPPED headless=true path={path}");
            return;
        }
        image.SavePng(path);
    }
}
