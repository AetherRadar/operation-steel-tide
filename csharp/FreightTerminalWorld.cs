using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class FreightTerminalWorld : Node3D
{
    private const float MapWidthMeters = 340.0f;
    private const float MapDepthMeters = 320.0f;
    private const float MapCenterZ = -60.0f;
    private const float DeploymentZoneRadiusMeters = 9.0f;
    private string _activeRuntimeMapId = DeploymentMapCatalog.FreightTerminalId;
    /// <summary>Player deploy pad chosen from edge set each match (not a fixed center apron).</summary>
    private Vector3 DeploymentPoint = new(0, 0.2f, 42.0f);
    private static readonly Vector3 ExtractionPoint = new(0.0f, 0.08f, -60.0f);

    private TacticalPlayer _player = null!;
    private CombatHUD _hud = null!;
    private MissionDirector _missionDirector = null!;
    private readonly List<EnemyOperator> _enemies = new();
    private readonly List<HostileOperatorSquad> _hostileSquads = new();
    private List<Vector3> _assignedHostilePads = new();
    private readonly List<ILootSource> _lootSources = new();
    private readonly List<ExplosiveBarrel> _barrels = new();
    private readonly List<DriveableVehicle> _vehicles = new();
    private readonly List<Vector3> _lootWorldPoints = new();
    private DestructibleAircraft? _aircraft;

    public int HostileSquadCount => _hostileSquads.Count;
    public IReadOnlyList<HostileOperatorSquad> HostileSquads => _hostileSquads;
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();
    private readonly Dictionary<string, PackedScene> _modelScenes = new();
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
        new(-2.8f, 0, 16), new(2.8f, 0, 16), new(0, 0, 13.2f), new(0, 0, 18.8f),
        new(-96, 0, -63), new(-82, 0, -88), new(-66, 0, -111), new(-49, 0, -137),
        new(-24, 0, -58), new(-13, 0, -76), new(18, 0, -70), new(34, 0, -101),
        new(49, 0, -69), new(63, 0, -86), new(85, 0, -111), new(97, 0, -130),
        new(29, 0, -132), new(48, 0, -151), new(68, 0, -136), new(91, 0, -145),
        new(-80, 0, -73), new(-76, 0, -71), new(-60, 0, -103), new(-56, 0, -103),
        new(-19, 0, -98), new(-15, 0, -98), new(60, 0, -84), new(64, 0, -80),
        new(85, 0, -94), new(89, 0, -92), new(-76, 0, -148), new(-72, 0, -148),
        new(5, 0, -147), new(9, 0, -145), new(80, 0, -142), new(84, 0, -140),
        new(-17, 0, -115), new(73, 0, -90), new(-43, 0, -82), new(24, 0, -112),
        new(-5.4f, 0, -69), new(-3.6f, 0, -69), new(3.8f, 0, -86), new(5.4f, 0, -86),
        new(-5.4f, 0, -103), new(-3.6f, 0, -103), new(3.8f, 0, -120), new(5.4f, 0, -120),
        new(-5.4f, 0, -136), new(-3.6f, 0, -136), new(3.8f, 0, -151), new(5.4f, 0, -151)
    };

    private Node3D _levelRoot = null!;
    private Area3D _extractionArea = null!;
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
    private int _nextEnemyNetworkId;
    private int _nextDroppedLootId = 1;
    private const int FixedBuildingLootPlacementCount = 27;
    private int _buildingLootPickupCount;
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
        var args = OS.GetCmdlineUserArgs();
        _activeRuntimeMapId = DeploymentMapRuntime.ResolveStartupMap(args);
        _rng.Randomize();
        LoadSettings();
        InitializeOperatorProgression();
        InitMissionDirector();
        BuildEnvironment();
        BuildLevel();
        BuildHudAndPlayer();
        BuildOperationsOffice();
        BuildSquadSystem();
        SpawnLootCases();
        SpawnBuildingGradedLoot();
        SpawnCivilianValuableLoot();
        SpawnEnemies();
        SpawnHostileOperatorSquads();
        SpawnWorldBoss();
        SpawnExplosives();
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.SetMissionPhase(_missionPhase, _missionDirector.SpawnProtectionSeconds, _missionOnline);
        ApplyQuality(_qualitySetting);

        InitializeOperationsOfficeState(args);
        RuntimeDiagnosticRunner.RunFirst(this, args);
        ResumePendingExtractionDeployment();
    }

    public override void _ExitTree()
    {
        CleanupOperatorProgression();
        // Drop cached resources and stop long-lived nodes before Mono tears down
        // its script bindings. The caches rebuild when the scene is reloaded.
        try
        {
            if (_aircraft is not null && IsInstanceValid(_aircraft))
            {
                _aircraft.SetPhysicsProcess(false);
            }
            foreach (var vehicle in _vehicles)
            {
                if (IsInstanceValid(vehicle))
                {
                    vehicle.SetPhysicsProcess(false);
                }
            }
            foreach (var mate in _squadMates)
            {
                if (IsInstanceValid(mate))
                {
                    mate.SetPhysicsProcess(false);
                }
            }
            _objectiveScreens.Clear();
            _materials.Clear();
            _modelScenes.Clear();
            ReleaseSharedBoxMeshes();
            BreakableGlassField.ReleaseSharedResources();
            ResidentialRelayStation.ReleaseSharedResources();
            ResidentialSupplyCache.ReleaseSharedResources();
            ResidentialSearchableFurniture.ReleaseSharedResources();
        }
        catch
        {
            // Best-effort shutdown hygiene only.
        }
    }

    public override void _Process(double delta)
    {
        UpdateSquad((float)delta);
        UpdateExtractionSequence((float)delta);
        UpdateDemolitionRound((float)delta);
        UpdateDemolitionNetwork((float)delta);
        UpdateWorldBossTracking();
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.RotateY((float)delta * 0.35f);
            var baseScale = _missionPhase == "EXTRACTION" ? 1.12f : 0.94f;
            if (IsExtractionCountdownActive)
            {
                baseScale = 1.24f;
            }
            var pulse = baseScale + Mathf.Sin(Time.GetTicksMsec() * 0.003f) * 0.06f;
            _extractionMarker.Scale = new Vector3(pulse, 1.0f, pulse);
        }

        if (UpdateRelayClimb((float)delta))
        {
            return;
        }

        if (_missionEnded && !IsExtractionDeparturePlaying && Input.IsKeyPressed(Key.Enter))
        {
            GetTree().ReloadCurrentScene();
            return;
        }
        if (!_missionEnded && !_player.IsDead && Input.IsActionJustPressed(GameInputActions.Inventory))
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
            if (!Input.IsActionPressed(GameInputActions.Interact))
            {
                _interactReleaseRequired = false;
            }
            else if (!_interactReleaseRequired && Input.IsActionJustPressed(GameInputActions.Interact))
            {
                _interactReleaseRequired = true;
                CloseLoot();
                return;
            }
        }

        UpdateDeploymentProtection();
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
        if (_demolitionMode)
        {
            return;
        }
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.DetectionRange = enemy.IsWorldBoss
                    ? Mathf.Max(240.0f, enemy.DetectionRange)
                    : detectionRange;
            }
        }
        _hud.SetMissionPhase(_missionPhase, _missionRemaining, _missionOnline);
    }

    private void OnPhaseChanged(string phase, float remaining, bool online)
    {
        if (_demolitionMode)
        {
            return;
        }
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
        if (_demolitionMode)
        {
            return;
        }
        _objectiveStage = index;
        _currentObjective = objective;
        _interactionProgress = 0.0f;
        RefreshLocalizedObjective();
        _hud.SetInteraction(string.Empty, 0.0f, false);
        // Beacon landmark always on; pulse stronger once objectives unlock extraction.
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = true;
            _extractionMarker.Scale = extractionAvailable
                ? new Vector3(1.15f, 1.15f, 1.15f)
                : Vector3.One;
        }
        if (extractionAvailable)
        {
            _hud.ShowLocalizedMessage(
                "extraction_unlocked",
                "EXTRACTION OPEN  //  ENTER THE GREEN ZONE AND HOLD FOR 12 SECONDS",
                new Color(0.3f, 1.0f, 0.68f));
        }
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

    public void ShowVehicleBlockedToast()
    {
        _hud.ShowLocalizedMessage(
            "vehicle_blocked",
            "VEHICLE BLOCKED  //  REVERSE TO BREAK FREE",
            new Color(1.0f, 0.62f, 0.3f));
    }

    public bool TryRepairNearestVehicle(Vector3 origin, float range, float amount)
    {
        DriveableVehicle? nearest = null;
        var nearestDistanceSquared = range * range;
        foreach (var vehicle in _vehicles)
        {
            if (!IsInstanceValid(vehicle) || vehicle.IsDestroyed || vehicle.Health >= vehicle.MaxHealth)
            {
                continue;
            }
            var distanceSquared = origin.DistanceSquaredTo(vehicle.GlobalPosition);
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }
            nearest = vehicle;
            nearestDistanceSquared = distanceSquared;
        }
        return nearest is not null && nearest.RestoreHealth(amount);
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

    private void UpdateDeploymentProtection()
    {
        if (IsInstanceValid(_player) && IsPlayerProtected()
            && IsOutsideDeploymentZone(_player.GlobalPosition))
        {
            _missionDirector.ExitDeploymentZone();
        }
    }

    private bool IsOutsideDeploymentZone(Vector3 position)
    {
        var offset = position - DeploymentPoint;
        return offset.X * offset.X + offset.Z * offset.Z
            > DeploymentZoneRadiusMeters * DeploymentZoneRadiusMeters;
    }

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
        var coverPoints = IsBlackwaterRefineryMap ? RefineryLayout.CoverPoints : _coverPoints;
        foreach (var point in coverPoints)
        {
            var travel = origin.DistanceTo(point);
            if (travel > 18.0f || point.DistanceTo(threat) < 4.0f)
            {
                continue;
            }
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    point + Vector3.Up,
                    threat + Vector3.Up * 1.3f,
                    1,
                    out var hit)
                || hit.Collider == _player)
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
        var glassEffectBudget = 12;
        var glassFields = GetTree().GetNodesInGroup(BreakableGlassField.GroupName);
        using var glassFieldsBacking = glassFields.AsDisposable();
        foreach (var node in glassFields)
        {
            if (node is not BreakableGlassField glass || !IsInstanceValid(glass))
            {
                continue;
            }
            glass.ShatterWithinRadius(position, radius * 1.18f, glassEffectBudget, out var effectsUsed);
            glassEffectBudget = Mathf.Max(0, glassEffectBudget - effectsUsed);
        }
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
        DamageSquadFromExplosion(position, radius, maxDamage, source);
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
        // Assign edge pads before the player exists so deploy position is match-randomized.
        ExtractionSpawnPads.AssignMatchPads(_rng, out var playerPad, out var hostilePads);
        DeploymentPoint = playerPad;
        _assignedHostilePads = hostilePads;

        _hud = new CombatHUD { Name = "CombatHUD" };
        AddChild(_hud);
        _hud.SetDeploymentMapSelection(_activeRuntimeMapId);
        _hud.SetOperatorProfile(_operatorProfileStore.Profile);
        _hud.PauseRequested += TogglePause;
        _hud.RestartRequested += RestartMission;
        _hud.QuitRequested += QuitGame;
        _hud.SensitivityChanged += SetSensitivity;
        _hud.QualityChanged += ApplyQuality;
        _hud.FullscreenChanged += SetFullscreen;
        _hud.LanguageChanged += SetLanguage;
        _hud.LootTakeRequested += TakeLootItem;
        _hud.LootEquipRequested += EquipLootItem;
        _hud.LootReturnRequested += ReturnBackpackItem;
        _hud.BackpackUseRequested += UseBackpackItem;
        _hud.BackpackDropRequested += DropBackpackItemToGround;
        _hud.LootWeaponSlotRequested += EquipLootItemToWeaponSlot;
        _hud.LootClosed += CloseLoot;
        _hud.InventoryToggleRequested += OnInventoryToggleRequested;
        _hud.OperationsQuickStartRequested += OnOperationsQuickStartRequested;
        _hud.DemolitionModeRequested += OnDemolitionModeRequested;
        _hud.DemolitionBackRequested += OnDemolitionBackRequested;
        _hud.DemolitionDeploymentRequested += OnDemolitionDeploymentRequested;
        _hud.DemolitionPurchaseRequested += OnDemolitionPurchaseRequested;
        _hud.OperationsHomeRequested += OnOperationsHomeRequested;

        _player = new TacticalPlayer
        {
            Name = "Player",
            Main = this,
            Hud = _hud,
            Position = DeploymentPoint,
            MouseSensitivity = 0.00165f * _sensitivitySetting
        };
        AddChild(_player);
        _player.ApplyColdStartUnarmed();
        ConfigureTacticalMinimap();
        _hud.WeaponSlotRequested += _player.SelectWeapon;
        _player.HitConfirmed += OnHitConfirmed;
        _player.Died += OnPlayerDied;
        _hud.SetSettings(_sensitivitySetting, _qualitySetting, _fullscreenSetting, _languageSetting);
        _hud.SetBackpackValuePlayer(_player);
        RefreshLocalizedObjective();
    }

    private void OnInventoryToggleRequested()
    {
        if (_missionEnded)
        {
            return;
        }
        if (_hud.IsLootVisible)
        {
            CloseLoot();
        }
        else
        {
            OpenPersonalBackpack();
        }
    }

    private void SpawnLootCases()
    {
        if (IsBlackwaterRefineryMap)
        {
            SpawnRefineryWeaponCases();
            return;
        }
        var cases = new[]
        {
            new
            {
                Position = new Vector3(31.5f, 0.02f, -18.0f),
                Rotation = 0.0f,
                English = "Warehouse armory case",
                Chinese = "仓库军械箱",
                Weapon = WeaponCatalog.Build(WeaponPlatform.M24, 2),
                Parts = new[] { "muzzle_suppressor", "optic_scope" },
                Equipment = new[] { "armor_heavy" },
                KnifeSkin = "knife_crimson"
            },
            new
            {
                Position = new Vector3(-34.0f, 0.02f, -11.0f),
                Rotation = Mathf.Pi / 2.0f,
                English = "Customs office locker",
                Chinese = "海关办公室枪柜",
                Weapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 1),
                Parts = new[] { "optic_holo", "mag_extended" },
                Equipment = new[] { "pack_heavy" },
                KnifeSkin = string.Empty
            },
            new
            {
                Position = new Vector3(13.0f, 0.02f, 11.5f),
                Rotation = -0.12f,
                English = "Maintenance weapon chest",
                Chinese = "维修间武器箱",
                Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 1),
                Parts = new[] { "muzzle_brake", "grip_vertical" },
                Equipment = new[] { "helmet_light" },
                KnifeSkin = "knife_hazard"
            },
            new
            {
                Position = new Vector3(5.0f, 0.42f, 33.2f),
                Rotation = Mathf.Pi / 2.0f,
                English = "Security checkpoint response locker",
                Chinese = "安检站应急装备柜",
                Weapon = WeaponCatalog.Build(WeaponPlatform.MP5A5, 1),
                Parts = new[] { "optic_micro", "mag_extended" },
                Equipment = new[] { "helmet_heavy" },
                KnifeSkin = string.Empty
            },
            new
            {
                Position = new Vector3(-34.0f, 0.02f, 18.0f),
                Rotation = 0.0f,
                English = "Fuel depot hazard locker",
                Chinese = "燃料库危险品装备箱",
                Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 1),
                Parts = new[] { "barrel_cqb", "muzzle_brake" },
                Equipment = new[] { "armor_carrier", "pack_assault" },
                KnifeSkin = string.Empty
            },
            new
            {
                Position = new Vector3(25.0f, 0.02f, 21.5f),
                Rotation = -Mathf.Pi / 2.0f,
                English = "Barracks command locker",
                Chinese = "营房指挥装备柜",
                Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 1),
                Parts = new[] { "stock_precision", "optic_holo" },
                Equipment = new[] { "helmet_heavy", "armor_heavy" },
                KnifeSkin = string.Empty
            },
            new
            {
                Position = new Vector3(-96.0f, 0.18f, -64.0f),
                Rotation = 0.0f,
                English = "Rail dispatch supply case",
                Chinese = "\u94c1\u8def\u8c03\u5ea6\u5ba4\u8865\u7ed9\u7bb1",
                Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 2),
                Parts = new[] { "optic_scope", "stock_precision" },
                Equipment = new[] { "pack_heavy" },
                KnifeSkin = "knife_arctic"
            },
            new
            {
                Position = new Vector3(25.0f, 0.18f, -88.0f),
                Rotation = Mathf.Pi / 2.0f,
                English = "Maintenance hangar tool cache",
                Chinese = "\u7ef4\u4fee\u673a\u5e93\u5de5\u5177\u7bb1",
                Weapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 2),
                Parts = new[] { "grip_vertical", "muzzle_suppressor" },
                Equipment = new[] { "armor_carrier", "helmet_light" },
                KnifeSkin = string.Empty
            },
            new
            {
                Position = new Vector3(44.0f, 0.22f, -146.0f),
                Rotation = -Mathf.Pi / 2.0f,
                English = "Seawall emergency locker",
                Chinese = "\u6d77\u5824\u5e94\u6025\u88c5\u5907\u67dc",
                Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
                Parts = new[] { "optic_holo", "mag_extended" },
                Equipment = new[] { "armor_heavy", "pack_assault" },
                KnifeSkin = string.Empty
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
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = definition.Weapon,
                Grade = LootGrades.FromTier(definition.Weapon.Attachments.Count >= 5 ? 2 : 1)
            });
            foreach (var part in definition.Parts)
            {
                weaponCase.Loot.Add(new LootItem
                {
                    Kind = LootItemKind.Attachment,
                    AttachmentId = part,
                    Grade = LootGrade.Rare
                });
            }
            foreach (var equipmentId in definition.Equipment)
            {
                weaponCase.Loot.Add(new LootItem
                {
                    Kind = LootItemKind.Equipment,
                    Equipment = EquipmentCatalog.Create(equipmentId),
                    Grade = equipmentId.Contains("heavy") ? LootGrade.Epic : LootGrade.Rare
                });
            }
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = WeaponCatalog.Weapon(definition.Weapon.Platform).Caliber,
                Quantity = definition.Weapon.Platform switch
                {
                    WeaponPlatform.M24 => _rng.RandiRange(14, 24),
                    WeaponPlatform.MP5A5 => _rng.RandiRange(55, 85),
                    _ => _rng.RandiRange(35, 65)
                },
                Grade = LootGrade.Common
            });
            if (!string.IsNullOrEmpty(definition.KnifeSkin))
            {
                weaponCase.Loot.Add(new LootItem
                {
                    Kind = LootItemKind.KnifeSkin,
                    KnifeSkinId = definition.KnifeSkin,
                    Grade = LootGrade.Epic
                });
            }
            weaponCase.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate, Grade = LootGrade.Uncommon });
            AddChild(weaponCase);
            _lootSources.Add(weaponCase);
            _lootWorldPoints.Add(definition.Position);
        }
    }

    private void SpawnBuildingGradedLoot()
    {
        _buildingLootPickupCount = 0;
        if (IsBlackwaterRefineryMap)
        {
            SpawnRefineryGradedLoot();
            return;
        }
        // Interior caches inside complex buildings + residential lobbies (glowing graded pickups).
        var spots = new (Vector3 Pos, LootGrade Grade, string En, string Zh)[]
        {
            (new Vector3(-55, 0.2f, -28), LootGrade.Rare, "Customs cache", "海关物资"),
            (new Vector3(-48, 0.2f, -22), LootGrade.Uncommon, "Warehouse crate", "仓库货箱"),
            (new Vector3(48, 0.2f, -48), LootGrade.Epic, "Ops annex safe", "行动附楼保险箱"),
            (new Vector3(42, 0.2f, -44), LootGrade.Rare, "Ops desk stash", "行动桌物资"),
            (new Vector3(58, 0.2f, -118), LootGrade.Legendary, "Fuel hall strongbox", "燃油厅重匣"),
            (new Vector3(52, 0.2f, -114), LootGrade.Uncommon, "Logistics bin", "后勤料箱"),
            (new Vector3(18, 0.2f, -148), LootGrade.Epic, "Bonded storage case", "保税仓货箱"),
            (new Vector3(25, 0.2f, -89), LootGrade.Rare, "Hangar parts locker", "机库零件柜"),
            (new Vector3(-128, 0.2f, -28), LootGrade.Uncommon, "Harbor court drop", "港湾庭院物资"),
            (new Vector3(132, 0.2f, -72), LootGrade.Rare, "East court cache", "东庭物资"),
            (new Vector3(2, 0.2f, -194), LootGrade.Epic, "North quay case", "北堤货箱"),
            (new Vector3(-25, 0.2f, 79), LootGrade.Uncommon, "South court bag", "南庭物资包"),
            (new Vector3(90, 0.2f, 72), LootGrade.Rare, "South tower stash", "南塔藏匿点"),
            (new Vector3(-82, 0.2f, 75), LootGrade.Common, "Courtyard supply", "庭院补给"),
            (new Vector3(35, 0.2f, 77), LootGrade.Legendary, "VIP residential case", "高档住宅箱")
        };
        var districtSpots = new (Vector3 Pos, LootGrade Grade, string En, string Zh)[]
        {
            (new Vector3(-93.3f, 0.82f, -68.2f), LootGrade.Uncommon, "Rail dispatch cache", GameLocalization.Get("loot_rail_dispatch", "zh", "Rail dispatch cache")),
            (new Vector3(-88.5f, 0.2f, -96.0f), LootGrade.Common, "Rail manifest pouch", GameLocalization.Get("loot_rail_manifest", "zh", "Rail manifest pouch")),
            (new Vector3(-72.5f, 0.2f, -121.0f), LootGrade.Uncommon, "Rail tool bin", GameLocalization.Get("loot_rail_tool", "zh", "Rail tool bin")),
            (new Vector3(16.0f, 0.96f, -99.0f), LootGrade.Uncommon, "Maintenance bench kit", GameLocalization.Get("loot_maintenance_bench", "zh", "Maintenance bench kit")),
            (new Vector3(34.0f, 0.2f, -96.5f), LootGrade.Common, "Repair bay supply", GameLocalization.Get("loot_repair_bay", "zh", "Repair bay supply")),
            (new Vector3(73.0f, 0.2f, -75.0f), LootGrade.Uncommon, "Tank valve kit", GameLocalization.Get("loot_tank_valve", "zh", "Tank valve kit")),
            (new Vector3(73.0f, 0.2f, -108.0f), LootGrade.Common, "Tank bund cache", GameLocalization.Get("loot_tank_bund", "zh", "Tank bund cache")),
            (new Vector3(44.0f, 0.22f, -145.0f), LootGrade.Uncommon, "Seawall shelter stock", GameLocalization.Get("loot_seawall_shelter", "zh", "Seawall shelter stock")),
            (new Vector3(55.0f, 0.2f, -149.0f), LootGrade.Common, "Quay rigging kit", GameLocalization.Get("loot_quay_rigging", "zh", "Quay rigging kit")),
            (new Vector3(80.0f, 0.2f, -148.0f), LootGrade.Uncommon, "Quay service cache", GameLocalization.Get("loot_quay_service", "zh", "Quay service cache")),
            (new Vector3(-22.0f, 0.2f, -10.0f), LootGrade.Common, "Container seal stash", GameLocalization.Get("loot_container_seal", "zh", "Container seal stash")),
            (new Vector3(-84.0f, 0.2f, 10.0f), LootGrade.Uncommon, "Overflow yard toolbox", GameLocalization.Get("loot_overflow_tool", "zh", "Overflow yard toolbox"))
        };
        var lootIndex = 0;
        foreach (var spot in spots.Concat(districtSpots))
        {
            var item = CreateGradedLootItem(spot.Grade);
            var pickup = new GradedLootPickup
            {
                Name = $"BuildingLoot_{++lootIndex:000}",
                Position = spot.Pos
            };
            pickup.Configure(item, spot.En, spot.Zh);
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(spot.Pos);
            _buildingLootPickupCount++;
        }
        foreach (var placement in _complexLootPlacements)
        {
            var pickup = new GradedLootPickup
            {
                Name = $"BuildingLoot_{++lootIndex:000}",
                Position = placement.Position
            };
            pickup.Configure(
                CreateGradedLootItem(placement.Grade),
                placement.EnglishName,
                placement.ChineseName);
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
            _buildingLootPickupCount++;
        }
    }

    private LootItem CreateGradedLootItem(LootGrade grade)
    {
        var roll = _rng.Randf();
        if (roll < 0.22f)
        {
            var tier = grade >= LootGrade.Legendary ? 2 : grade >= LootGrade.Rare ? 1 : 0;
            return new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(grade switch
                {
                    LootGrade.Legendary => WeaponPlatform.M24,
                    LootGrade.Epic => WeaponPlatform.ScarL,
                    LootGrade.Rare when _rng.Randf() < 0.45f => WeaponPlatform.MP5A5,
                    _ => WeaponPlatform.M4A1
                }, tier),
                Grade = grade
            };
        }
        if (roll < 0.42f)
        {
            return new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(grade >= LootGrade.Epic ? "armor_heavy" : "armor_carrier"),
                Grade = grade
            };
        }
        if (roll < 0.6f)
        {
            return new LootItem
            {
                Kind = LootItemKind.Attachment,
                AttachmentId = grade >= LootGrade.Rare ? "optic_scope" : "optic_holo",
                Grade = grade
            };
        }
        if (roll < 0.72f)
        {
            return new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = grade >= LootGrade.Rare ? 2 : 1, Grade = grade };
        }
        if (roll < 0.82f)
        {
            return new LootItem
            {
                Kind = LootItemKind.Medical,
                MedicalKind = grade >= LootGrade.Epic
                    ? MedicalItemKind.Adrenaline
                    : grade >= LootGrade.Rare ? MedicalItemKind.FieldMedkit : MedicalItemKind.Bandage,
                Quantity = grade >= LootGrade.Rare ? 2 : 1,
                Grade = grade
            };
        }
        if (roll < 0.95f)
        {
            return new LootItem
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = ValuableItems.SelectForGrade(grade, (int)_rng.Randi()),
                Grade = grade
            };
        }
        var caliber = grade >= LootGrade.Epic
            ? AmmoCaliber.Sniper
            : grade >= LootGrade.Rare && _rng.Randf() < 0.4f ? AmmoCaliber.Smg : AmmoCaliber.Rifle;
        var quantity = caliber switch
        {
            AmmoCaliber.Sniper => 8 + (int)grade * 3,
            AmmoCaliber.Smg => 35 + (int)grade * 18,
            _ => 20 + (int)grade * 15
        };
        return new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = caliber, Quantity = quantity, Grade = grade };
    }

    private void SpawnEnemies()
    {
        // Map garrison NPCs (TeamId 0) — prefer hunting rival squads, loot when idle.
        IReadOnlyList<Vector3> positions = IsBlackwaterRefineryMap
            ? RefineryLayout.GarrisonSpawns
            : new[]
        {
            new Vector3(-12, 0.15f, 11), new Vector3(-22, 0.15f, 7), new Vector3(3, 0.15f, 2),
            new Vector3(20, 0.15f, 8), new Vector3(29, 0.15f, -10), new Vector3(-10, 0.15f, -17),
            new Vector3(-28, 0.15f, -31), new Vector3(4, 0.15f, -32), new Vector3(20, 0.15f, -20),
            new Vector3(-91, 0.15f, -58), new Vector3(-76, 0.15f, -92),
            new Vector3(-58, 0.15f, -126), new Vector3(-88, 0.15f, -146),
            new Vector3(-12, 0.15f, -61), new Vector3(22, 0.15f, -81),
            new Vector3(51, 0.15f, -68), new Vector3(82, 0.15f, -101),
            new Vector3(34, 0.15f, -136), new Vector3(66, 0.15f, -132),
            new Vector3(94, 0.15f, -145)
        };
        foreach (var position in positions)
        {
            SpawnEnemy(position, false, teamId: 0);
        }
        if (!IsBlackwaterRefineryMap)
        {
            SpawnSkybridgeMarksmen();
        }
        _enemiesRemaining = _enemies.Count;
    }

    private void SpawnSkybridgeMarksmen()
    {
        _residentialSkybridgeMarksmanCount = 0;
        foreach (var post in _residentialSniperPosts)
        {
            var marksman = SpawnEnemy(
                post.Position,
                alerted: false,
                teamId: 0,
                initialWeapon: WeaponCatalog.Build(WeaponPlatform.M24, 2),
                sentryMode: true,
                detectionRange: 185.0f);
            marksman.Name = $"SKYWAY_M24_{_residentialSkybridgeMarksmanCount + 1:00}";
            var facing = post.FacingTarget;
            facing.Y = marksman.GlobalPosition.Y;
            if (marksman.GlobalPosition.DistanceSquaredTo(facing) > 0.1f)
            {
                marksman.LookAt(facing, Vector3.Up);
            }
            _residentialSkybridgeMarksmanCount++;
        }
    }

    private void SpawnHostileOperatorSquads()
    {
        // Pads assigned in BuildHudAndPlayer (player edge pad + farthest remaining).
        var chosen = _assignedHostilePads;
        if (chosen is null || chosen.Count == 0)
        {
            ExtractionSpawnPads.AssignMatchPads(_rng, out _, out chosen);
            _assignedHostilePads = chosen;
        }

        var prefixes = new[] { "WOLF", "COBRA", "HAWK", "VIPER" };
        var count = Mathf.Min(chosen.Count, ExtractionSpawnPads.HostileSquadTargetCount);
        for (var i = 0; i < count; i++)
        {
            var teamId = i + 1;
            var squad = new HostileOperatorSquad
            {
                TeamId = teamId,
                SpawnPad = chosen[i],
                CallsignPrefix = prefixes[i % prefixes.Length]
            };
            for (var m = 0; m < ExtractionSpawnPads.SquadSize; m++)
            {
                var member = SpawnEnemy(
                    ExtractionSpawnPads.HostileMemberPosition(chosen[i], m),
                    alerted: false,
                    teamId: teamId);
                member.Name = $"{squad.CallsignPrefix}_{m + 1}";
                squad.Members.Add(member);
            }
            _hostileSquads.Add(squad);
        }
        _enemiesRemaining = _enemies.Count(e => IsInstanceValid(e) && !e.IsDead);
    }

    private EnemyOperator SpawnEnemy(
        Vector3 position,
        bool alerted,
        int teamId = 0,
        WeaponBuild? initialWeapon = null,
        bool sentryMode = false,
        float? detectionRange = null)
    {
        var enemy = new EnemyOperator
        {
            Position = position,
            NetworkId = _nextEnemyNetworkId++,
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            DetectionRange = detectionRange ?? _missionDetectionRange,
            TeamId = teamId,
            SentryMode = sentryMode
        };
        if (initialWeapon is not null)
        {
            enemy.ConfigureInitialLoadout(initialWeapon);
        }
        AddChild(enemy);
        enemy.Eliminated += OnEnemyEliminated;
        _enemies.Add(enemy);
        InvalidateCombatTargetIndex();
        if (alerted)
        {
            enemy.SetAlerted(_player.GlobalPosition);
        }
        return enemy;
    }
    /// <summary>All living combatants that are hostile to the given operator (player squad, other teams, NPCs).</summary>
    public IEnumerable<Node3D> EnumerateHostileTargetsFor(EnemyOperator self)
    {
        if (IsPlayerProtected() && !self.BypassesPlayerProtectionForDiagnostics)
        {
            yield break;
        }
        if (IsInstanceValid(_player) && !_player.IsDead)
        {
            yield return _player;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate) && !mate.IsDowned && !mate.IsBodyBag)
            {
                yield return mate;
            }
        }
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead || enemy == self)
            {
                continue;
            }
            if (self.IsHostileTo(enemy))
            {
                yield return enemy;
            }
        }
    }

    public Vector3? FindNearestLootPoint(Vector3 origin, float range)
    {
        Vector3? best = null;
        var bestDist = range * range;
        foreach (var point in _lootWorldPoints)
        {
            if (Mathf.Abs(point.Y - origin.Y) > 2.4f)
            {
                continue;
            }
            var d = origin.DistanceSquaredTo(point);
            if (d < bestDist)
            {
                bestDist = d;
                best = point;
            }
        }
        foreach (var source in _lootSources)
        {
            if (!source.IsSearchable || !IsInstanceValid(source.LootNode))
            {
                continue;
            }
            if (Mathf.Abs(source.LootNode.GlobalPosition.Y - origin.Y) > 2.4f)
            {
                continue;
            }
            var d = origin.DistanceSquaredTo(source.LootNode.GlobalPosition);
            if (d < bestDist)
            {
                bestDist = d;
                best = source.LootNode.GlobalPosition;
            }
        }
        return best;
    }

    private bool HasClearLootInteractionApproach(ILootSource source)
    {
        if (!source.IsSearchable || !IsInstanceValid(source.LootNode))
        {
            return false;
        }

        var space = GetWorld3D().DirectSpaceState;
        var target = source.LootNode.GlobalPosition + Vector3.Up * 0.28f;
        using var playerShape = new CapsuleShape3D { Radius = 0.38f, Height = 1.75f };
        var clearanceExclude = new Godot.Collections.Array<Rid>();
        using var clearanceExcludeBacking = clearanceExclude.AsDisposable();
        if (source.LootNode is CollisionObject3D collisionSource)
        {
            clearanceExclude.Add(collisionSource.GetRid());
        }
        var approachDirections = new[]
        {
            Vector3.Left,
            Vector3.Right,
            Vector3.Forward,
            Vector3.Back,
            (Vector3.Left + Vector3.Forward).Normalized(),
            (Vector3.Left + Vector3.Back).Normalized(),
            (Vector3.Right + Vector3.Forward).Normalized(),
            (Vector3.Right + Vector3.Back).Normalized()
        };
        foreach (var approachDistance in new[] { 1.25f, 1.75f, 2.3f })
        {
            foreach (var direction in approachDirections)
            {
                var approach = source.LootNode.GlobalPosition + direction * approachDistance;
                if (!PhysicsRaycast.TryHit(
                        space,
                        approach + Vector3.Up * 2.2f,
                        approach + Vector3.Down * 2.2f,
                        1,
                        out var floorHit)
                    || floorHit.Normal.Dot(Vector3.Up) < 0.65f)
                {
                    continue;
                }

                var feet = floorHit.Position + Vector3.Up * 0.03f;
                using var clearanceQuery = new PhysicsShapeQueryParameters3D
                {
                    Shape = playerShape,
                    Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * 0.88f),
                    CollisionMask = 1,
                    CollideWithBodies = true,
                    CollideWithAreas = false,
                    Exclude = clearanceExclude
                };
                if (PhysicsShapeProbe.HasCollision(space, clearanceQuery, 1))
                {
                    continue;
                }

                if (PhysicsRaycast.TryHit(
                        space,
                        target + direction * approachDistance,
                        target,
                        1,
                        out var sightHit)
                    && sightHit.Collider == source.LootNode)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool HasClearPlayerLootInteractionLineOfSight(ILootSource source)
    {
        if (!source.IsSearchable || !IsInstanceValid(source.LootNode) || !IsInstanceValid(_player))
        {
            return false;
        }

        var exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (source.LootNode is CollisionObject3D collisionSource)
        {
            exclude.Add(collisionSource.GetRid());
        }

        var from = _player.GlobalPosition + Vector3.Up * 1.25f;
        foreach (var targetHeight in new[] { 0.28f, 0.72f, 1.16f })
        {
            if (!PhysicsRaycast.HasHit(
                    GetWorld3D(),
                    from,
                    source.LootNode.GlobalPosition + Vector3.Up * targetHeight,
                    exclude,
                    1))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Nearest searchable loot source that still contains a weapon stack (for cold-start re-arm).
    /// Prefers graded world pickups, then corpse/case sources.
    /// </summary>
    public ILootSource? FindNearestWeaponLootSource(Vector3 origin, float range)
    {
        ILootSource? best = null;
        var bestDist = range * range;
        var gradedLoot = GetTree().GetNodesInGroup("graded_loot");
        using var gradedLootBacking = gradedLoot.AsDisposable();
        foreach (var node in gradedLoot)
        {
            if (node is not GradedLootPickup pickup || !IsInstanceValid(pickup) || !pickup.IsSearchable)
            {
                continue;
            }
            if (!pickup.Loot.Exists(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null))
            {
                continue;
            }
            if (Mathf.Abs(pickup.GlobalPosition.Y - origin.Y) > 2.4f)
            {
                continue;
            }
            var d = origin.DistanceSquaredTo(pickup.GlobalPosition);
            if (d < bestDist)
            {
                bestDist = d;
                best = pickup;
            }
        }
        foreach (var source in _lootSources)
        {
            if (source is null || !source.IsSearchable || !IsInstanceValid(source.LootNode))
            {
                continue;
            }
            var hasWeapon = source.Loot.Exists(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
            var sealedWeaponCandidate = source is IDeferredLootSource deferred
                && !deferred.ContentsResolved
                && deferred.MayContainWeapon;
            if (!hasWeapon && !sealedWeaponCandidate)
            {
                continue;
            }
            if (Mathf.Abs(source.LootNode.GlobalPosition.Y - origin.Y) > 2.4f)
            {
                continue;
            }
            var d = origin.DistanceSquaredTo(source.LootNode.GlobalPosition);
            if (d < bestDist)
            {
                bestDist = d;
                best = source;
            }
        }
        return best;
    }

    /// <summary>
    /// Production path: pull one weapon stack from a loot source and equip it on the operator.
    /// Returns true when HasFireablePrimary becomes true via real loot removal (not diagnostics grant).
    /// </summary>
    public bool TryEquipWeaponFromLootSource(EnemyOperator operatorNode, ILootSource source)
    {
        if (operatorNode is null || source is null || !IsInstanceValid(operatorNode) || !source.IsSearchable)
        {
            return false;
        }
        if (source is IDeferredLootSource { ContentsResolved: false })
        {
            source.OnSearched();
        }
        var index = source.Loot.FindIndex(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
        if (index < 0)
        {
            return false;
        }
        var weaponItem = source.Loot[index];
        source.Loot.RemoveAt(index);
        RefreshGradedLootPickupPresentation(source);
        if (source is EnemyOperator corpse)
        {
            corpse.MarkCarriedWeaponRemoved();
        }
        source.OnSearched();
        RetireEmptyGradedLootPickup(source);
        return operatorNode.EquipWeaponFromLoot(weaponItem.Weapon!);
    }

    /// <summary>Player equip from a live loot source (same removal rules as HUD EquipLootItem).</summary>
    public bool TryPlayerEquipWeaponFromLootSource(ILootSource source)
    {
        if (source is null || !source.IsSearchable || !IsInstanceValid(_player))
        {
            return false;
        }
        if (source is IDeferredLootSource { ContentsResolved: false })
        {
            source.OnSearched();
            if (_player.IsDead)
            {
                return false;
            }
        }
        var index = source.Loot.FindIndex(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
        if (index < 0)
        {
            return false;
        }
        var original = source.Loot[index];
        var replacement = _player.EquipFromLoot(original);
        if (ReferenceEquals(replacement, original))
        {
            return false;
        }
        if (replacement is null)
        {
            source.Loot.RemoveAt(index);
        }
        else
        {
            source.Loot[index] = replacement;
        }
        if (source is EnemyOperator enemy)
        {
            enemy.MarkCarriedWeaponRemoved();
        }
        RefreshGradedLootPickupPresentation(source);
        source.OnSearched();
        RetireEmptyGradedLootPickup(source);
        return _player.HasFireablePrimary;
    }

    /// <summary>Squad mate equip from loot source (cold-start re-arm).</summary>
    public bool TryMateEquipWeaponFromLootSource(SquadMate mate, ILootSource source)
    {
        if (mate is null || source is null || !IsInstanceValid(mate) || !source.IsSearchable)
        {
            return false;
        }
        if (source is IDeferredLootSource { ContentsResolved: false })
        {
            source.OnSearched();
        }
        var index = source.Loot.FindIndex(item => item.Kind == LootItemKind.Weapon && item.Weapon is not null);
        if (index < 0)
        {
            return false;
        }
        var weaponItem = source.Loot[index];
        source.Loot.RemoveAt(index);
        RefreshGradedLootPickupPresentation(source);
        if (source is EnemyOperator corpse)
        {
            corpse.MarkCarriedWeaponRemoved();
        }
        source.OnSearched();
        RetireEmptyGradedLootPickup(source);
        return mate.EquipWeaponFromLoot(weaponItem.Weapon!);
    }

    private void SpawnExplosives()
    {
        foreach (var position in new[]
        {
            new Vector3(-6, 0, 18), new Vector3(-20, 0, -6), new Vector3(17, 0, -18),
            new Vector3(31, 0, 8), new Vector3(-30, 0, 28), new Vector3(-82, 0, -74),
            new Vector3(-52, 0, -119), new Vector3(17, 0, -93), new Vector3(55, 0, -72),
            new Vector3(91, 0, -107), new Vector3(30, 0, -149), new Vector3(60, 0, -138)
        })
        {
            var barrel = new ExplosiveBarrel { Main = this, Position = position };
            barrel.AddToGroup("explosives");
            AddChild(barrel);
            _barrels.Add(barrel);
        }
    }

    private void OnEnemyEliminated(EnemyOperator enemy)
    {
        if (enemy.LastDamageAttacker == _player)
        {
            _hud.ShowKnockdown(
                enemy.OperatorCallsign(_languageSetting),
                GameLocalization.Get("you", _languageSetting, "YOU"));
        }
        else if (enemy.LastDamageAttacker is SquadMate mate && _squadMates.Contains(mate))
        {
            _hud.ShowKnockdown(enemy.OperatorCallsign(_languageSetting), mate.Callsign);
        }
        _lootSources.Add(enemy);
        _enemiesRemaining = Mathf.Max(0, _enemiesRemaining - 1);
        _kills++;
        _hud.SetEnemyCount(_enemiesRemaining);
        _enemies.Remove(enemy);
        InvalidateCombatTargetIndex();
    }

    /// <summary>
    /// Diagnostics only: after ResetTacticalStateForDiagnostics revives a dead operator,
    /// put them back on the living roster so EnumerateHostileTargetsFor can see them again.
    /// Phase-1 duel kills remove members via OnEnemyEliminated; mid-loot acquire needs them listed.
    /// </summary>
    public void EnsureEnemyRegisteredForDiagnostics(EnemyOperator enemy)
    {
        if (enemy is null || !IsInstanceValid(enemy) || enemy.IsDead)
        {
            return;
        }
        _lootSources.Remove(enemy);
        if (!_enemies.Contains(enemy))
        {
            _enemies.Add(enemy);
            InvalidateCombatTargetIndex();
        }
        _enemiesRemaining = _enemies.Count(e => IsInstanceValid(e) && !e.IsDead);
        _hud.SetEnemyCount(_enemiesRemaining);
    }

    /// <summary>Civilian corpse becomes a searchable loot source (ammo/plates/sometimes a gun).</summary>
    public void RegisterCivilianCorpse(CivilianNpc civilian)
    {
        if (civilian is null || !IsInstanceValid(civilian))
        {
            return;
        }
        if (!_lootSources.Contains(civilian))
        {
            _lootSources.Add(civilian);
        }
        _hud.ShowLocalizedMessage("civilian_down", "CIVILIAN DOWN  //  F LOOT", new Color(0.95f, 0.55f, 0.35f));
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
        _player.EjectFromVehicleIfAny();
        if (HandleLocalPlayerDowned())
        {
            return;
        }
        if (TryBeginLocalPlayerElimination())
        {
            return;
        }
        if (_demolitionMode)
        {
            _player.MarkEliminatedForDemolitionRound();
            FinishDemolitionRound(
                false,
                GameLocalization.Get(
                    "demolition_squad_eliminated",
                    _languageSetting,
                    "SQUAD ELIMINATED"));
            return;
        }
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        _hud.HideDownedState();
        _missionDirector.CompleteMission(false, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowResult(false);
    }

    private void OnExtractionEntered(Node3D body)
    {
        TryBeginExtractionSequence(body);
    }

    /// <summary>
    /// Team loot-value ranking at extract. Living operators count backpack/loadout value;
    /// body-bag / box entities do NOT contribute to team score.
    /// </summary>
    public List<(string Team, int Value, int Rank)> BuildExtractionLootRanking()
    {
        var rows = new List<(string Team, int Value)>();
        // Player squad (player + living mates only).
        var playerValue = CombatHUD.ComputeBackpackTotalValue(_player);
        foreach (var mate in _squadMates)
        {
            if (!IsInstanceValid(mate) || mate.IsBodyBag || mate.IsDowned)
            {
                continue;
            }
            // Living AI mates contribute a fixed kit residual; bags add nothing.
            playerValue += 80;
        }
        rows.Add((GameLocalization.IsChinese(_languageSetting) ? "我方小队" : "PLAYER SQUAD", playerValue));

        foreach (var squad in _hostileSquads)
        {
            var value = 0;
            var alive = 0;
            foreach (var member in squad.Members)
            {
                if (!IsInstanceValid(member) || member.IsDead)
                {
                    continue;
                }
                alive++;
                value += LootItem.TotalValue(member.Loot);
                if (member.HasFireablePrimary)
                {
                    value += new LootItem
                    {
                        Kind = LootItemKind.Weapon,
                        Weapon = member.CarriedWeapon.Clone(),
                        Grade = LootGrade.Rare
                    }.StackValue;
                }
            }
            // Explicitly ignore body bags / loot sources tagged as squad bags.
            var label = GameLocalization.IsChinese(_languageSetting)
                ? $"敌对小队 {squad.CallsignPrefix}"
                : $"RIVAL {squad.CallsignPrefix}";
            if (alive == 0)
            {
                value = 0; // wiped team (all bagged/dead) scores zero
            }
            rows.Add((label, value));
        }

        rows.Sort((a, b) => b.Value.CompareTo(a.Value));
        var ranked = new List<(string Team, int Value, int Rank)>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            ranked.Add((rows[i].Team, rows[i].Value, i + 1));
        }
        return ranked;
    }

    /// <summary>Pure helper for validators: bagged mates never add value.</summary>
    public static int ScoreLivingSquadValue(int playerBackpackValue, int livingMateBonus, int baggedMateCountIgnored)
    {
        _ = baggedMateCountIgnored; // bags contribute 0 by design
        return Math.Max(0, playerBackpackValue) + Math.Max(0, livingMateBonus);
    }

    private bool LocalPlayerCannotInteract =>
        _missionEnded || _localPlayerDowned || _localPlayerEliminated || _player.IsDead;

    private void UpdateInteraction(float delta)
    {
        if (LocalPlayerCannotInteract)
        {
            _lootSearchTarget = null;
            _interactionProgress = 0.0f;
            _player.SetSearchPose(false);
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }
        if (_demolitionMode)
        {
            UpdateDemolitionInteraction(delta);
            return;
        }
        if (!Input.IsActionPressed(GameInputActions.Interact))
        {
            _interactReleaseRequired = false;
        }
        if (_hud.IsLootVisible)
        {
            return;
        }

        // Vehicle enter / exit takes priority so F never fights loot while driving.
        if (_player.IsInVehicle)
        {
            _lootSearchTarget = null;
            _player.SetSearchPose(false);
            var vehicle = _player.CurrentVehicle;
            if (vehicle is not null)
            {
                _hud.SetInteraction(vehicle.InteractionLabel(_languageSetting), -1.0f, true);
                if (!_interactReleaseRequired && Input.IsActionJustPressed(GameInputActions.Interact))
                {
                    _interactReleaseRequired = true;
                    vehicle.ExitDriver();
                }
            }
            return;
        }

        // Manual revive is handled in UpdateSquad; skip loot prompts while charging.
        if (_manualReviveProgress > 0.02f || (_manualReviveTarget is not null && _manualReviveTarget.CanBeRevived
            && _player.GlobalPosition.DistanceTo(_manualReviveTarget.CombatNode.GlobalPosition) < 3.0f))
        {
            _lootSearchTarget = null;
            return;
        }

        if (TryHandleRoofAccessInteraction())
        {
            return;
        }

        DriveableVehicle? nearestVehicle = null;
        var nearestVehicleDistance = 3.4f;
        for (var i = _vehicles.Count - 1; i >= 0; i--)
        {
            var vehicle = _vehicles[i];
            if (!IsInstanceValid(vehicle) || vehicle.IsDestroyed)
            {
                _vehicles.RemoveAt(i);
                continue;
            }
            var distance = _player.GlobalPosition.DistanceTo(vehicle.GlobalPosition);
            if (distance < nearestVehicleDistance)
            {
                nearestVehicle = vehicle;
                nearestVehicleDistance = distance;
            }
        }
        if (nearestVehicle is not null)
        {
            _lootSearchTarget = null;
            _player.SetSearchPose(false);
            _hud.SetInteraction(nearestVehicle.InteractionLabel(_languageSetting), -1.0f, true);
            if (!_interactReleaseRequired && Input.IsActionJustPressed(GameInputActions.Interact))
            {
                _interactReleaseRequired = true;
                nearestVehicle.TryEnter(_player);
            }
            return;
        }

        CivilianNpc? nearestCivilian = null;
        var nearestCivilianDistance = 2.85f;
        foreach (var civilian in _civilians)
        {
            if (!IsInstanceValid(civilian) || !civilian.CanOfferAssistance)
            {
                continue;
            }
            var distance = _player.GlobalPosition.DistanceTo(civilian.GlobalPosition);
            if (distance >= nearestCivilianDistance)
            {
                continue;
            }
            nearestCivilian = civilian;
            nearestCivilianDistance = distance;
        }
        if (nearestCivilian is not null)
        {
            _lootSearchTarget = null;
            _player.SetSearchPose(false);
            _hud.SetInteraction($"{nearestCivilian.AssistanceLabel(_languageSetting)}  //  F", -1.0f, true);
            if (!_interactReleaseRequired && Input.IsActionJustPressed(GameInputActions.Interact))
            {
                _interactReleaseRequired = true;
                nearestCivilian.TryProvideAssistance(_player);
            }
            return;
        }

        if (UpdateRelayStationInteraction(delta))
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
            if (distance < nearestDistance && HasClearPlayerLootInteractionLineOfSight(source))
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
            var unopenedContainer = nearest is IOpenableLootSource { IsOpened: false };
            if (nearest is ResidentialSearchableFurniture || unopenedContainer)
            {
                var searching = Input.IsActionPressed(GameInputActions.Interact) && !_interactReleaseRequired;
                _interactionProgress = searching
                    ? Mathf.Min(1.0f, _interactionProgress + delta / nearest.SearchDuration)
                    : Mathf.Max(0.0f, _interactionProgress - delta * 2.2f);
                _player.SetSearchPose(_interactionProgress > 0.02f, _interactionProgress);
                var interaction = unopenedContainer
                    ? GameLocalization.Get("open_loot", _languageSetting, "OPEN")
                    : GameLocalization.Get("search", _languageSetting, "SEARCH");
                _hud.SetInteraction($"{interaction}  //  {nearest.DisplayName(_languageSetting)}", _interactionProgress, true);
                if (_interactionProgress >= 1.0f)
                {
                    _interactReleaseRequired = true;
                    OpenLoot(nearest);
                }
            }
            else
            {
                _interactionProgress = 0.0f;
                _player.SetSearchPose(false);
                var open = GameLocalization.Get("open_loot", _languageSetting, "OPEN");
                _hud.SetInteraction($"{open}  //  {nearest.DisplayName(_languageSetting)}", -1.0f, true);
                if (!_interactReleaseRequired && Input.IsActionJustPressed(GameInputActions.Interact))
                {
                    _interactReleaseRequired = true;
                    OpenLoot(nearest);
                }
            }
            return;
        }
        if (_lootSearchTarget is not null)
        {
            _interactionProgress = 0.0f;
        }
        _lootSearchTarget = null;
        _player.SetSearchPose(false);
        UpdateObjectiveInteraction(delta);
    }

    private void OpenLoot(ILootSource source)
    {
        _interactionProgress = 0.0f;
        if (LocalPlayerCannotInteract)
        {
            ClearPendingLootOpen();
            return;
        }
        _openLootSource = source;
        _personalBackpackOpen = false;
        source.OnSearched();
        if (LocalPlayerCannotInteract || !ReferenceEquals(_openLootSource, source))
        {
            ClearPendingLootOpen();
            return;
        }
        _player.UiLocked = true;
        _player.SetSearchPose(true, 1.0f);
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _hud.SetInteraction(string.Empty, 0.0f, false);
        _hud.ShowLoot(source.DisplayName(_languageSetting), source.Loot, _player, true);
    }

    private void ClearPendingLootOpen()
    {
        var pendingSource = _openLootSource;
        _openLootSource = null;
        _personalBackpackOpen = false;
        _lootSearchTarget = null;
        _interactionProgress = 0.0f;
        _player.SetSearchPose(false);
        _hud.SetInteraction(string.Empty, 0.0f, false);
        RetireEmptyGradedLootPickup(pendingSource);
    }

    private void LockLootForMissionTransition(Input.MouseModeEnum mouseMode)
    {
        if (_hud.IsLootVisible)
        {
            _hud.HideLoot();
        }
        ClearPendingLootOpen();
        _interactReleaseRequired = Input.IsActionPressed(GameInputActions.Interact);
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        Input.MouseMode = mouseMode;
    }

    private void OpenPersonalBackpack()
    {
        if (LocalPlayerCannotInteract)
        {
            return;
        }
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
        var closedSource = _openLootSource;
        if (_hud.IsLootVisible)
        {
            _hud.HideLoot();
        }
        _openLootSource = null;
        _personalBackpackOpen = false;
        _lootSearchTarget = null;
        _interactionProgress = 0.0f;
        _interactReleaseRequired = Input.IsActionPressed(GameInputActions.Interact);
        _player.SetSearchPose(false);
        _player.DisarmFireInput();
        RetireEmptyGradedLootPickup(closedSource);
        Input.MouseMode = _missionEnded
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
        if (LocalPlayerCannotInteract)
        {
            _player.UiLocked = true;
            _player.DisarmMovementInput();
            return;
        }
        _player.UiLocked = false;
        _player.RestoreMovementInput();
    }

    internal bool InterruptLootForIncomingDamage()
    {
        if (!_hud.IsLootVisible && _openLootSource is null)
        {
            return false;
        }
        CloseLoot();
        return true;
    }

    private void TakeLootItem(string itemId)
    {
        if (LocalPlayerCannotInteract || _openLootSource is null)
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
        RefreshGradedLootPickupPresentation(_openLootSource);
        if (_openLootSource is EnemyOperator enemy && taken.Kind == LootItemKind.Weapon)
        {
            enemy.MarkCarriedWeaponRemoved();
        }
        RefreshLootView();
    }

    private void EquipLootItem(string itemId)
    {
        if (LocalPlayerCannotInteract || _openLootSource is null)
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
        RefreshGradedLootPickupPresentation(_openLootSource);
        if (_openLootSource is EnemyOperator enemy && original.Kind == LootItemKind.Weapon)
        {
            enemy.MarkCarriedWeaponRemoved();
        }
        RefreshLootView();
    }

    private void UseBackpackItem(string itemId)
    {
        if (LocalPlayerCannotInteract)
        {
            return;
        }
        var item = _player.Backpack.Find(candidate => candidate.Id == itemId);
        if (item?.Kind is LootItemKind.Medical or LootItemKind.ArmorPlate)
        {
            CloseLoot();
            _player.UseBackpackItem(itemId);
            return;
        }
        if (_player.UseBackpackItem(itemId))
        {
            RefreshLootView();
        }
    }

    private void EquipLootItemToWeaponSlot(string itemId, int originValue, int slotValue)
    {
        if (LocalPlayerCannotInteract
            || !System.Enum.IsDefined(typeof(LootDragOrigin), originValue)
            || !System.Enum.IsDefined(typeof(PlayerWeaponSlot), slotValue))
        {
            return;
        }
        var origin = (LootDragOrigin)originValue;
        var slot = (PlayerWeaponSlot)slotValue;
        if (slot == PlayerWeaponSlot.Melee)
        {
            return;
        }
        if (origin == LootDragOrigin.Backpack)
        {
            if (_player.UseBackpackItemInWeaponSlot(itemId, slot))
            {
                RefreshLootView();
            }
            return;
        }
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
        var replacement = _player.EquipFromLootToWeaponSlot(original, slot);
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
        RefreshGradedLootPickupPresentation(_openLootSource);
        if (_openLootSource is EnemyOperator enemy && original.Kind == LootItemKind.Weapon)
        {
            enemy.MarkCarriedWeaponRemoved();
        }
        RefreshLootView();
    }

    private void ReturnBackpackItem(string itemId)
    {
        if (LocalPlayerCannotInteract || _openLootSource is null)
        {
            return;
        }

        if (!_player.TryRemoveBackpackItem(itemId, out var returned))
        {
            return;
        }
        _openLootSource.Loot.Add(returned);
        RefreshGradedLootPickupPresentation(_openLootSource);
        RefreshLootView();
    }

    private void DropBackpackItemToGround(string itemId)
    {
        if (LocalPlayerCannotInteract)
        {
            return;
        }
        if (!_player.TryRemoveBackpackItem(itemId, out var item))
        {
            return;
        }
        var pickup = new GradedLootPickup
        {
            Name = $"DroppedLoot{_nextDroppedLootId++}"
        };
        pickup.ConfigureDropped(
            item,
            $"Dropped {item.DisplayName("en")}",
            $"\u4e22\u5f03\u7269  {item.DisplayName("zh")}");
        AddChild(pickup);
        pickup.GlobalPosition = ResolveDroppedLootPosition();
        if (!pickup.IsInsideTree())
        {
            _player.TryStoreInBackpack(item);
            pickup.QueueFree();
            return;
        }

        _lootSources.Add(pickup);
        RefreshLootView();
    }

    private Vector3 ResolveDroppedLootPosition()
    {
        var forward = -_player.GlobalBasis.Z;
        forward.Y = 0.0f;
        if (forward.LengthSquared() < 0.001f)
        {
            forward = Vector3.Forward;
        }
        forward = forward.Normalized();

        var candidate = _player.GlobalPosition + forward * 1.35f;
        if (PhysicsRaycast.TryHit(
                GetWorld3D(),
                candidate + Vector3.Up * 1.6f,
                candidate + Vector3.Down * 3.2f,
                _player.GetRid(),
                1,
                out var hit))
        {
            return hit.Position + Vector3.Up * 0.03f;
        }
        candidate.Y = _player.GlobalPosition.Y + 0.03f;
        return candidate;
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

    private static void RefreshGradedLootPickupPresentation(ILootSource source)
    {
        if (source is GradedLootPickup graded)
        {
            graded.RefreshContentsPresentation();
        }
    }

    private void RetireEmptyGradedLootPickup(ILootSource? source)
    {
        if (source is not GradedLootPickup graded
            || graded.Loot.Count > 0
            || !IsInstanceValid(graded)
            || graded.IsQueuedForDeletion())
        {
            return;
        }
        if (ReferenceEquals(_openLootSource, graded))
        {
            if (_hud.IsLootVisible)
            {
                RefreshLootView();
            }
            return;
        }

        _lootSources.Remove(graded);
        _lootWorldPoints.Remove(graded.GlobalPosition);
        graded.QueueFree();
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
        _interactionProgress = Input.IsActionPressed(GameInputActions.Interact) && !_interactReleaseRequired
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
        if (_demolitionMode || _missionEnded || _reinforcementsDeployed || _missionPhase != "COMBAT")
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
            new Vector3(-101, 0.15f, 38), new Vector3(101, 0.15f, 38),
            new Vector3(-101, 0.15f, -55), new Vector3(101, 0.15f, -55),
            new Vector3(-101, 0.15f, -156), new Vector3(101, 0.15f, -156)
        };
        Array.Sort(spawnPoints, (left, right) =>
            left.DistanceSquaredTo(_player.GlobalPosition).CompareTo(right.DistanceSquaredTo(_player.GlobalPosition)));
        var deployed = 0;
        foreach (var spawnPoint in spawnPoints)
        {
            if (spawnPoint.DistanceSquaredTo(_player.GlobalPosition) < 45.0f * 45.0f)
            {
                continue;
            }
            SpawnEnemy(spawnPoint, true);
            deployed++;
            if (deployed == 3)
            {
                break;
            }
        }
        _enemiesRemaining += deployed;
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

    private void QuitGame() => GetTree().Quit();

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
            if (_environmentRef.Sky is Sky sky)
            {
                sky.ProcessMode = _qualitySetting >= 2
                    ? Sky.ProcessModeEnum.Realtime
                    : Sky.ProcessModeEnum.Incremental;
                sky.RadianceSize = new[]
                {
                    Sky.RadianceSizeEnum.Size64,
                    Sky.RadianceSizeEnum.Size128,
                    Sky.RadianceSizeEnum.Size256
                }[_qualitySetting];
            }
        }
        GetViewport().Scaling3DScale = new[] { 0.74f, 0.88f, 1.0f }[_qualitySetting];
        if (IsInstanceValid(_sunLight))
        {
            _sunLight.ShadowEnabled = _qualitySetting >= 1;
            _sunLight.DirectionalShadowMaxDistance = new[] { 80.0f, 150.0f, 260.0f }[_qualitySetting];
        }
        ApplyMapDetailQuality();
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
        ApplyLanguage(language, true);
    }

    private void SetCaptureLanguage(string language)
    {
        ApplyLanguage(language, false);
    }

    private void ApplyLanguage(string language, bool persist)
    {
        _languageSetting = GameLocalization.IsChinese(language) ? "zh" : "en";
        _hud.SetLanguage(_languageSetting);
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.SetMissionPhase(_missionPhase, _missionRemaining, _missionOnline);
        RefreshLocalizedObjective();
        RefreshLootView();
        RefreshResidentialLocalization();
        foreach (var drop in _aircraftSupplyDrops)
        {
            if (IsInstanceValid(drop))
            {
                drop.SetLanguage(_languageSetting);
            }
        }
        if (persist)
        {
            SaveSettings();
        }
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
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (IsInstanceValid(member))
                {
                    member.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft.SetPhysicsProcess(false);
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
        _player.GrantFireablePrimaryForDiagnostics();
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
        _player.GrantFireablePrimaryForDiagnostics();
        DisableActorsForSurvivalDiagnostics();
        _missionDirector.ExitDeploymentZone();
        _player.Backpack.RemoveAll(item => item.Kind == LootItemKind.ArmorPlate);
        var plateStored = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.ArmorPlate,
            Quantity = 2,
            Grade = LootGrade.Rare
        });
        _player.SetArmorForDiagnostics(20.0f);
        Input.ActionRelease("move_forward");
        Input.ActionRelease("use_plate");
        await WaitFrames(3);

        var fireModeBefore = _player.FireMode;
        var flashlightBefore = _player.FlashlightOn;
        Input.ActionPress("toggle_fire_mode");
        await WaitFrames(2);
        Input.ActionRelease("toggle_fire_mode");
        Input.ActionPress("toggle_flashlight");
        await WaitFrames(2);
        Input.ActionRelease("toggle_flashlight");

        var armorBefore = _player.Armor;
        var platesBefore = _player.ArmorPlates;
        Input.ActionPress("use_plate");
        await WaitFrames(2);
        Input.ActionRelease("use_plate");
        await WaitFrames(2);
        var plateStarted = _player.IsPlateUseActiveForDiagnostics;
        var cancelHintVisible = _hud.EquipmentCancelHintVisibleForDiagnostics;

        Input.ActionPress("move_forward");
        await WaitFrames(6);
        var movementAllowed = _player.HasMovementIntent && _player.IsPlateUseActiveForDiagnostics;
        Input.ActionRelease("move_forward");
        await WaitFrames(2);

        Input.ActionPress("use_plate");
        await WaitFrames(2);
        Input.ActionRelease("use_plate");
        await WaitFrames(2);
        var cancelledByKey = !_player.IsPlateUseActiveForDiagnostics
            && _player.ArmorPlates == platesBefore
            && Mathf.IsEqualApprox(_player.Armor, armorBefore);

        Input.ActionPress("use_plate");
        await WaitFrames(2);
        Input.ActionRelease("use_plate");
        await WaitFrames(2);
        var restarted = _player.IsPlateUseActiveForDiagnostics;
        var deadline = Time.GetTicksMsec() + 4000;
        while (_player.ArmorPlates == platesBefore && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var completed = !_player.IsPlateUseActiveForDiagnostics
            && _player.ArmorPlates == platesBefore - 1
            && _player.Armor > armorBefore;

        var plateAction = InputMap.HasAction(GameInputActions.UsePlate);
        var plateKeyBound = false;
        if (plateAction)
        {
            var plateEvents = InputMap.ActionGetEvents(GameInputActions.UsePlate);
            using var plateEventsBacking = plateEvents.AsDisposable();
            foreach (var inputEvent in plateEvents)
            {
                if (inputEvent is InputEventKey key && key.PhysicalKeycode == Key.X)
                {
                    plateKeyBound = true;
                    break;
                }
            }
        }
        var secondaryControlsWorked = _player.FireMode != fireModeBefore
            && _player.FlashlightOn != flashlightBefore;
        var valid = plateStored
            && plateStarted
            && cancelHintVisible
            && movementAllowed
            && cancelledByKey
            && restarted
            && completed
            && plateAction
            && plateKeyBound
            && secondaryControlsWorked;
        GD.Print($"EQUIPMENT_CHECK valid={valid} plate_started={plateStarted} movement_allowed={movementAllowed} cancel_hint={cancelHintVisible} cancelled_by_x={cancelledByKey} restarted={restarted} completed={completed} plates={platesBefore}->{_player.ArmorPlates} armor={armorBefore:0.0}->{_player.Armor:0.0} action={plateAction} key_x={plateKeyBound} mode={_player.FireMode} light={_player.FlashlightOn}");
        GD.Print($"EQUIPMENT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
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
        var valid = _player.ReserveAmmo > reserveBefore;
        GD.Print($"PICKUP_CHECK valid={valid} ammo_before={reserveBefore} ammo_after={_player.ReserveAmmo}");
        GD.Print($"PICKUP_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateAmmoInventoryFlow()
    {
        await WaitFrames(6);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (IsInstanceValid(member))
                {
                    member.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }

        _player.GrantFireablePrimaryForDiagnostics(WeaponCatalog.Build(WeaponPlatform.AK74, 1));
        _player.ClearBackpackForDiagnostics();
        var commonStored = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = AmmoCaliber.Rifle,
            Quantity = 12,
            Grade = LootGrade.Common
        });
        var rareStored = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = AmmoCaliber.Rifle,
            Quantity = 36,
            Grade = LootGrade.Rare
        });
        var sniperStored = _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = AmmoCaliber.Sniper,
            Quantity = 8,
            Grade = LootGrade.Epic
        });
        var pickupLinked = commonStored && rareStored && sniperStored
            && _player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Common) == 12
            && _player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Rare) == 36
            && _player.AmmoReserveFor(AmmoCaliber.Sniper, LootGrade.Epic) == 8
            && _player.Backpack.Exists(item => item.Kind == LootItemKind.Ammunition
                && item.AmmoCaliber == AmmoCaliber.Rifle
                && item.Grade == LootGrade.Rare
                && item.Quantity == 36);

        var reloaded = _player.ReloadImmediatelyForDiagnostics(0);
        var reloadLinked = reloaded
            && _player.Ammo == _player.CurrentWeaponStats.MagazineSize
            && _player.CurrentAmmoGrade == LootGrade.Rare
            && _player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Rare) == 6
            && _player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Common) == 12
            && _player.AmmoReserveFor(AmmoCaliber.Sniper, LootGrade.Epic) == 8
            && _player.Backpack.Exists(item => item.Kind == LootItemKind.Ammunition
                && item.AmmoCaliber == AmmoCaliber.Rifle
                && item.Grade == LootGrade.Rare
                && item.Quantity == 6);

        var rareStack = _player.Backpack.Find(item => item.Kind == LootItemKind.Ammunition
            && item.AmmoCaliber == AmmoCaliber.Rifle
            && item.Grade == LootGrade.Rare);
        GradedLootPickup? droppedPickup = null;
        if (rareStack is not null)
        {
            DropBackpackItemToGround(rareStack.Id);
            droppedPickup = _lootSources.OfType<GradedLootPickup>()
                .FirstOrDefault(candidate => candidate.Loot.Exists(item => item.Id == rareStack.Id));
        }
        var dropLinked = rareStack is not null
            && droppedPickup is not null
            && droppedPickup.IsSearchable
            && _player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Rare) == 0
            && !_player.Backpack.Exists(item => item.Id == rareStack.Id);

        var repicked = false;
        if (droppedPickup is not null)
        {
            _openLootSource = droppedPickup;
            TakeLootItem(droppedPickup.Loot[0].Id);
            repicked = droppedPickup.Loot.Count == 0;
            _openLootSource = null;
        }
        var repickLinked = repicked
            && _player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Rare) == 6
            && _player.Backpack.Exists(item => item.Kind == LootItemKind.Ammunition
                && item.AmmoCaliber == AmmoCaliber.Rifle
                && item.Grade == LootGrade.Rare
                && item.Quantity == 6);
        var valid = pickupLinked && reloadLinked && dropLinked && repickLinked;
        GD.Print($"AMMO_INVENTORY_CHECK valid={valid} pickup_linked={pickupLinked} reload_linked={reloadLinked} drop_linked={dropLinked} repick_linked={repickLinked} rifle_common={_player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Common)} rifle_rare={_player.AmmoReserveFor(AmmoCaliber.Rifle, LootGrade.Rare)} sniper_epic={_player.AmmoReserveFor(AmmoCaliber.Sniper, LootGrade.Epic)} backpack_stacks={_player.Backpack.Count(item => item.Kind == LootItemKind.Ammunition)}");
        GD.Print($"AMMO_INVENTORY_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureReloadFrame()
    {
        SetCaptureLanguage("en");
        _player.GrantFireablePrimaryForDiagnostics();
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
        SetCaptureLanguage("en");
        for (var i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].ProcessMode = ProcessModeEnum.Disabled;
            _enemies[i].Visible = i == 0;
        }
        var target = _enemies[0];
        target.GlobalPosition = new Vector3(0, 0.15f, 29.5f);

        var captureCamera = new Camera3D
        {
            Name = "OperatorCaptureCamera",
            Fov = 34.0f,
            Near = 0.04f
        };
        AddChild(captureCamera);
        captureCamera.GlobalPosition = new Vector3(0, 1.55f, 24.65f);
        target.LookAt(
            new Vector3(captureCamera.GlobalPosition.X, target.GlobalPosition.Y, captureCamera.GlobalPosition.Z),
            Vector3.Up);
        captureCamera.LookAt(target.GlobalPosition + Vector3.Up * 1.12f, Vector3.Up);
        captureCamera.MakeCurrent();

        await WaitFrames(24);
        SaveViewportImage("res://operator_validation.png");
        var projected = captureCamera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 1.12f);
        var viewportCenter = GetViewport().GetVisibleRect().Size * 0.5f;
        var framed = projected.DistanceTo(viewportCenter) < 3.0f;
        GD.Print($"OPERATOR_CHECK detailed_model=true visible=1 camera_current={captureCamera.Current} framed={framed} center_offset={projected.DistanceTo(viewportCenter):0.00}");
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
        SetCaptureLanguage("en");
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.KnifeSkin,
            KnifeSkinId = "knife_crimson",
            Grade = LootGrade.Epic
        });
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
        GD.Print($"KNIFE_CHECK equipped={_player.KnifeEquipped} skin={_player.EquippedKnifeSkinId} weapon={_player.EquippedWeapon.Platform} direction=right_to_left_rising_slash");
        GetTree().Quit();
    }

    private async void ValidateLootFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.SetPhysicsProcess(false);
        }
        SetLanguage("zh");
        var source = _lootSources.OfType<WeaponCase>().First();
        var sealedProbeItem = new LootItem
        {
            Kind = LootItemKind.Valuable,
            ValuableKind = ValuableItemKind.AntiqueClock,
            Grade = LootGrade.Legendary
        };
        var sealedPickupProbe = new GradedLootPickup
        {
            Name = "SealedLootVisualProbe",
            Position = new Vector3(0.0f, 0.2f, 420.0f)
        };
        sealedPickupProbe.Configure(
            sealedProbeItem,
            "Sealed visual probe",
            "\u5bc6\u5c01\u5916\u89c2\u6d4b\u8bd5");
        AddChild(sealedPickupProbe);
        await WaitFrames(2);
        var sealedPickupConcealsGrade = sealedPickupProbe.VisualReady
            && sealedPickupProbe.GradeConcealedBeforeOpen
            && !sealedPickupProbe.IsOpened;
        sealedPickupProbe.OnSearched();
        await WaitFrames(30);
        var sealedPickupOpens = sealedPickupProbe.IsOpened && sealedPickupProbe.OpenVisualReady;
        sealedPickupProbe.MarkEmpty();
        var sealedPickupEmptyHidden = sealedPickupProbe.EmptyPresentationHiddenForDiagnostics
            && !sealedPickupProbe.IsSearchable;
        sealedPickupProbe.Loot.Add(sealedProbeItem);
        sealedPickupProbe.RefreshContentsPresentation();
        var sealedPickupReturnRestored = sealedPickupProbe.IsSearchable
            && sealedPickupProbe.VisualReady
            && sealedPickupProbe.OpenVisualReady
            && sealedPickupProbe.CollisionLayer == 1;
        var looseProbeItem = new LootItem
        {
            Kind = LootItemKind.Medical,
            MedicalKind = MedicalItemKind.Bandage,
            Grade = LootGrade.Rare
        };
        sealedPickupProbe.ConfigureDropped(
            looseProbeItem,
            "Loose visual probe",
            "\u6563\u843d\u5916\u89c2\u6d4b\u8bd5");
        await WaitFrames(2);
        var treeReconfiguredLoose = sealedPickupProbe.IsOpened
            && sealedPickupProbe.VisualReady
            && !sealedPickupProbe.GradeConcealedBeforeOpen;
        sealedPickupProbe.Configure(
            sealedProbeItem,
            "Sealed visual probe",
            "\u5bc6\u5c01\u5916\u89c2\u6d4b\u8bd5");
        await WaitFrames(2);
        var treeReconfiguredSealed = !sealedPickupProbe.IsOpened
            && sealedPickupProbe.VisualReady
            && sealedPickupProbe.GradeConcealedBeforeOpen;
        _lootSources.Add(sealedPickupProbe);
        _lootWorldPoints.Add(sealedPickupProbe.GlobalPosition);
        var sealedPickupProbePosition = sealedPickupProbe.GlobalPosition;
        _openLootSource = sealedPickupProbe;
        sealedPickupProbe.MarkEmpty();
        RetireEmptyGradedLootPickup(sealedPickupProbe);
        var openEmptyPickupRetained = _lootSources.Contains(sealedPickupProbe)
            && IsInstanceValid(sealedPickupProbe)
            && !sealedPickupProbe.IsQueuedForDeletion();
        _openLootSource = null;
        RetireEmptyGradedLootPickup(sealedPickupProbe);
        await WaitFrames(2);
        var emptyPickupRetired = !_lootSources.Contains(sealedPickupProbe)
            && !_lootWorldPoints.Contains(sealedPickupProbePosition)
            && !IsInstanceValid(sealedPickupProbe);
        var lineOfSightProbe = new GradedLootPickup
        {
            Name = "LootLineOfSightProbe",
            Position = new Vector3(0.0f, 0.2f, 430.0f)
        };
        lineOfSightProbe.Configure(
            new LootItem { Kind = LootItemKind.ArmorPlate, Grade = LootGrade.Uncommon },
            "Line-of-sight probe",
            "\u89c6\u7ebf\u6d4b\u8bd5\u7269\u8d44");
        AddChild(lineOfSightProbe);
        _lootSources.Add(lineOfSightProbe);
        var lineOfSightWall = new StaticBody3D
        {
            Name = "LootInteractionWallProbe",
            Position = new Vector3(0.0f, 1.6f, 428.9f),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        lineOfSightWall.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(3.0f, 3.2f, 0.25f) }
        });
        AddChild(lineOfSightWall);
        var playerProcessMode = _player.ProcessMode;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = new Vector3(0.0f, 0.2f, 427.8f);
        _lootSearchTarget = lineOfSightProbe;
        _interactionProgress = 0.65f;
        _player.SetSearchPose(true, _interactionProgress);
        await WaitFrames(3);
        var lootWallBlocked = _lootSearchTarget is null
            && _interactionProgress <= 0.001f
            && !HasClearPlayerLootInteractionLineOfSight(lineOfSightProbe);
        lineOfSightWall.QueueFree();
        await WaitFrames(3);
        var lineOfSightDeadline = Time.GetTicksMsec() + 1000;
        while (!ReferenceEquals(_lootSearchTarget, lineOfSightProbe)
            && Time.GetTicksMsec() < lineOfSightDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var lootWallCleared = ReferenceEquals(_lootSearchTarget, lineOfSightProbe)
            && HasClearPlayerLootInteractionLineOfSight(lineOfSightProbe);
        _lootSources.Remove(lineOfSightProbe);
        _lootSearchTarget = null;
        lineOfSightProbe.QueueFree();
        _player.ProcessMode = playerProcessMode;
        await WaitFrames(2);
        _player.GlobalPosition = source.LootNode.GlobalPosition + new Vector3(0, 0.2f, 0.85f);
        _missionDirector.ExitDeploymentZone();
        var targetDeadline = Time.GetTicksMsec() + 1000;
        while (!ReferenceEquals(_lootSearchTarget, source) && Time.GetTicksMsec() < targetDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var targetMatched = ReferenceEquals(_lootSearchTarget, source);
        var sourceSealedBeforeOpen = !source.IsOpened;
        var openedAt = Time.GetTicksMsec();
        Input.ActionPress("interact");
        await WaitFrames(8);
        var contentsConcealedDuringSearch = !_hud.IsLootVisible && !source.IsOpened;
        var deadline = Time.GetTicksMsec() + 2500;
        while (!_hud.IsLootVisible && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var opened = _hud.IsLootVisible;
        var firstOpenMilliseconds = Time.GetTicksMsec() - openedAt;
        await WaitFrames(28);
        var sourceOpenVisualReady = source.OpenVisualReady;
        var item = source.Loot.Find(candidate => candidate.Kind == LootItemKind.Weapon);
        var sourceClickActivated = false;
        var emptyPrimaryAutoEquipped = false;
        if (item is not null)
        {
            var expectedPlatform = item.Weapon?.Platform;
            var startedUnarmed = !_player.HasFireablePrimary;
            sourceClickActivated = _hud.ActivateLootCardForDiagnostics(item.Id, LootDragOrigin.Source);
            await WaitFrames(2);
            emptyPrimaryAutoEquipped = startedUnarmed
                && _player.HasFireablePrimary
                && expectedPlatform.HasValue
                && _player.EquippedWeapon.Platform == expectedPlatform.Value
                && source.Loot.TrueForAll(candidate => candidate.Id != item.Id);
        }
        var policyValid = LootInteractionPolicy.ResolveSourceActivation(LootItemKind.Weapon, false)
                == LootSourceActivationAction.EquipPrimaryWeapon
            && LootInteractionPolicy.ResolveSourceActivation(LootItemKind.Weapon, true)
                == LootSourceActivationAction.MoveToBackpack
            && LootInteractionPolicy.ResolveSourceActivation(LootItemKind.Valuable, false)
                == LootSourceActivationAction.MoveToBackpack
            && LootInteractionPolicy.GetBackpackMenuCapabilities(LootItemKind.Weapon)
                == new LootBackpackMenuCapabilities(true, true)
            && LootInteractionPolicy.GetBackpackMenuCapabilities(LootItemKind.Valuable)
                == new LootBackpackMenuCapabilities(false, true);

        var menuWeapon = new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.MP5A5, 1),
            Grade = LootGrade.Uncommon
        };
        var menuWeaponStored = _player.TryStoreInBackpack(menuWeapon);
        RefreshLootView();
        await WaitFrames(2);
        var weaponBeforeMenuOpen = _player.EquippedWeapon.Platform;
        var weaponMenuActivated = menuWeaponStored
            && _hud.ActivateLootCardForDiagnostics(menuWeapon.Id, LootDragOrigin.Backpack);
        await WaitFrames(2);
        var weaponMenuReady = _hud.LootActionMenuReady
            && _hud.LootActionMenuVisible
            && _hud.LootActionMenuCanEquip
            && _hud.LootActionMenuItemId == menuWeapon.Id
            && _hud.LootActionMenuEquipText == GameLocalization.Get("equip", "zh", "EQUIP")
            && _hud.LootActionMenuDropText == GameLocalization.Get("drop_to_ground", "zh", "DROP TO GROUND")
            && _player.EquippedWeapon.Platform == weaponBeforeMenuOpen
            && _player.Backpack.Exists(candidate => candidate.Id == menuWeapon.Id);
        if (weaponMenuReady)
        {
            _hud.PressLootMenuEquipForDiagnostics();
            await WaitFrames(2);
        }
        var weaponMenuEquipped = weaponMenuReady
            && _player.EquippedWeapon.Platform == WeaponPlatform.MP5A5
            && _player.Backpack.TrueForAll(candidate => candidate.Id != menuWeapon.Id);

        var menuValuable = new LootItem
        {
            Kind = LootItemKind.Valuable,
            ValuableKind = ValuableItemKind.CannedCoffee,
            Grade = LootGrade.Rare
        };
        var menuValuableStored = _player.TryStoreInBackpack(menuValuable);
        RefreshLootView();
        await WaitFrames(2);
        var itemMenuActivated = menuValuableStored
            && _hud.ActivateLootCardForDiagnostics(menuValuable.Id, LootDragOrigin.Backpack);
        await WaitFrames(2);
        var itemMenuDropOnly = _hud.LootActionMenuVisible
            && !_hud.LootActionMenuCanEquip
            && _hud.LootActionMenuItemId == menuValuable.Id
            && _player.Backpack.Exists(candidate => candidate.Id == menuValuable.Id);
        if (itemMenuDropOnly)
        {
            _hud.PressLootMenuDropForDiagnostics();
            await WaitFrames(2);
        }
        var itemMenuDropped = itemMenuDropOnly
            && !_player.Backpack.Exists(candidate => candidate.Id == menuValuable.Id)
            && _lootSources.OfType<GradedLootPickup>()
                .Any(candidate => candidate.Loot.Exists(drop => drop.Id == menuValuable.Id));
        var dragCandidate = source.Loot.Find(candidate => candidate.Kind != LootItemKind.Weapon);
        var returnedToSource = false;
        var dragDropRouted = false;
        var groundDropRouted = false;
        var droppedRegistered = false;
        var droppedVisible = false;
        var searchStorageExpanded = false;
        var searchStorageFits = false;
        var storageAtCapacity = false;
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

            TakeLootItem(dragCandidate.Id);
            var groundProbe = new LootDropZone { Target = LootDropTarget.Ground };
            groundProbe.Dropped += (itemId, origin, target) =>
            {
                groundDropRouted = origin == LootDragOrigin.Backpack && target == LootDropTarget.Ground;
                DropBackpackItemToGround(itemId);
            };
            var equipmentSlot = dragCandidate.Kind == LootItemKind.Equipment && dragCandidate.Equipment is not null
                ? (int)dragCandidate.Equipment.Definition.Slot
                : -1;
            var groundDragData = new Godot.Collections.Dictionary
            {
                ["item_id"] = dragCandidate.Id,
                ["origin"] = (int)LootDragOrigin.Backpack,
                ["kind"] = (int)dragCandidate.Kind,
                ["slot"] = equipmentSlot
            };
            if (groundProbe._CanDropData(Vector2.Zero, groundDragData))
            {
                groundProbe._DropData(Vector2.Zero, groundDragData);
            }
            groundProbe.Free();
            var droppedPickup = _lootSources
                .OfType<GradedLootPickup>()
                .FirstOrDefault(candidate => candidate.Loot.Exists(item => item.Id == dragCandidate.Id));
            droppedRegistered = droppedPickup is not null
                && _lootSources.Contains(droppedPickup)
                && droppedPickup.IsSearchable
                && !_player.Backpack.Exists(candidate => candidate.Id == dragCandidate.Id);
            droppedVisible = droppedPickup is not null
                && droppedPickup.VisualReady
                && droppedPickup.GlobalPosition.DistanceTo(_player.GlobalPosition) <= 2.5f;
        }
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquipmentCatalog.Create("pack_heavy"),
            Grade = LootGrade.Rare
        });
        _player.TryStoreInBackpack(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.AXMC, 3),
            Grade = LootGrade.Legendary
        });
        while (_player.Backpack.Count < _player.BackpackCapacity)
        {
            _player.TryStoreInBackpack(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(_player.Backpack.Count % 2 == 0 ? "helmet_heavy" : "armor_heavy"),
                Grade = LootGrade.Rare
            });
        }
        RefreshLootView();
        await WaitFrames(6);
        searchStorageExpanded = _hud.LootSearchStorageExpanded;
        searchStorageFits = _hud.LootBackpackContentFits;
        storageAtCapacity = _player.Backpack.Count == _player.BackpackCapacity;
        var searchSourceAvailable = _hud.LootSourceAvailableForDiagnostics;
        var searchSourceSize = _hud.LootSourceZoneSizeForDiagnostics;
        var searchBackpackSize = _hud.LootBackpackZoneSizeForDiagnostics;
        var searchSourceCards = _hud.LootSourceCardWidthsForDiagnostics;
        var compactComparisonsComplete = _hud.LootCompactWeaponComparisonsFullyRendered;
        var compactDirectionsVisible = _hud.LootCompactWeaponShowsBothDirections;
        var renderedComparisonsComplete = _hud.LootComparisonsFullyRendered;
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
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        OpenPersonalBackpack();
        await WaitFrames(3);
        var damageViewOpened = _hud.IsLootVisible && _player.UiLocked;
        var healthBeforeDamage = _player.Health;
        _player.TakeDamage(8.0f, _player.GlobalPosition + Vector3.Up, this);
        await WaitFrames(3);
        var damageOverlayClosed = !_hud.IsLootVisible;
        var damageUiUnlocked = !_player.UiLocked;
        var damageMouseCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;
        var damageMouseObservable = DisplayServer.GetName() != "headless";
        var damageMouseRestored = damageMouseCaptured || !damageMouseObservable;
        var damageApplied = _player.Health < healthBeforeDamage;
        var damageClosedLoot = damageViewOpened
            && damageOverlayClosed
            && damageUiUnlocked
            && damageMouseRestored
            && damageApplied;
        Input.ActionPress("move_forward");
        await WaitFrames(4);
        var damageMovementRestored = _player.HasMovementIntent;
        Input.ActionRelease("move_forward");
        var damageSource = new ResidentialSupplyCache { Name = "LootDamageInterruptProbe" };
        damageSource.Configure(
            ResidentialCacheKind.FamilyStash,
            0,
            0,
            new[]
            {
                new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.CannedCoffee,
                    Grade = LootGrade.Common
                }
            });
        AddChild(damageSource);
        damageSource.FirstOpened += _ =>
            _player.TakeDamage(8.0f, _player.GlobalPosition + Vector3.Up, this);
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        var searchDamageHealthBefore = _player.Health;
        OpenLoot(damageSource);
        await WaitFrames(2);
        var searchDamageMouseCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;
        var searchDamageMouseRestored = searchDamageMouseCaptured || !damageMouseObservable;
        var searchDamageAborted = _player.Health < searchDamageHealthBefore
            && !_hud.IsLootVisible
            && !_player.UiLocked
            && _openLootSource is null
            && !_personalBackpackOpen
            && searchDamageMouseRestored;
        damageSource.QueueFree();
        await WaitFrames(24);
        SaveViewportImage("res://modular_weapon_validation.png");

        var fatalStateSource = new ResidentialSupplyCache { Name = "LootFatalStateProbe" };
        fatalStateSource.Configure(
            ResidentialCacheKind.FamilyStash,
            0,
            0,
            new[]
            {
                new LootItem
                {
                    Kind = LootItemKind.Valuable,
                    ValuableKind = ValuableItemKind.CannedCoffee,
                    Grade = LootGrade.Common
                }
            });
        AddChild(fatalStateSource);
        fatalStateSource.FirstOpened += _ =>
        {
            _missionEnded = true;
            LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        };
        _missionEnded = false;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        Input.ActionRelease("fire");
        Input.MouseMode = Input.MouseModeEnum.Captured;
        var fatalInputPrimeDeadline = Time.GetTicksMsec() + 1000UL;
        while (!_player.FireInputArmedForDiagnostics
            && Time.GetTicksMsec() < fatalInputPrimeDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var fatalInputPrimed = _player.FireInputArmedForDiagnostics
            && _player.MovementInputArmedForDiagnostics;
        OpenLoot(fatalStateSource);
        await WaitFrames(2);
        var fatalMouseObservable = DisplayServer.GetName() != "headless";
        var fatalMouseVisible = Input.MouseMode == Input.MouseModeEnum.Visible;
        var fatalControlLocked = _player.UiLocked
            && !_player.FireInputArmedForDiagnostics
            && !_player.MovementInputArmedForDiagnostics
            && !_player.HasMovementIntent;
        var fatalSearchStateHandled = _missionEnded
            && fatalInputPrimed
            && fatalControlLocked
            && !_hud.IsLootVisible
            && _openLootSource is null
            && !_personalBackpackOpen
            && (fatalMouseVisible || !fatalMouseObservable);

        _missionEnded = false;
        _player.UiLocked = false;
        _player.RestoreMovementInput();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        OpenLoot(fatalStateSource);
        await WaitFrames(2);
        var activeLootOpenedBeforeEnd = _hud.IsLootVisible
            && ReferenceEquals(_openLootSource, fatalStateSource);
        static string InventoryFingerprint(IEnumerable<LootItem> items)
            => string.Join('|', items.Select(item => $"{item.Id}:{item.Quantity}"));
        var sourceBeforeEnd = InventoryFingerprint(fatalStateSource.Loot);
        var backpackBeforeEnd = InventoryFingerprint(_player.Backpack);
        var droppedLootIdBeforeEnd = _nextDroppedLootId;
        var sourceItemId = fatalStateSource.Loot.FirstOrDefault()?.Id ?? string.Empty;
        var backpackItemId = _player.Backpack.FirstOrDefault()?.Id ?? string.Empty;
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        _hud.ShowResult(false);
        TakeLootItem(sourceItemId);
        EquipLootItem(sourceItemId);
        ReturnBackpackItem(backpackItemId);
        UseBackpackItem(backpackItemId);
        DropBackpackItemToGround(backpackItemId);
        CloseLoot();
        var activeLootEndHandled = activeLootOpenedBeforeEnd
            && _hud.IsMissionResultVisible
            && !_hud.IsLootVisible
            && _openLootSource is null
            && !_personalBackpackOpen
            && _player.UiLocked
            && !_player.FireInputArmedForDiagnostics
            && !_player.MovementInputArmedForDiagnostics
            && InventoryFingerprint(fatalStateSource.Loot) == sourceBeforeEnd
            && InventoryFingerprint(_player.Backpack) == backpackBeforeEnd
            && _nextDroppedLootId == droppedLootIdBeforeEnd
            && (Input.MouseMode == Input.MouseModeEnum.Visible || !fatalMouseObservable);
        fatalStateSource.QueueFree();
        var valid = opened
            && targetMatched
            && sealedPickupConcealsGrade
            && sealedPickupOpens
            && sealedPickupEmptyHidden
            && sealedPickupReturnRestored
            && treeReconfiguredLoose
            && treeReconfiguredSealed
            && openEmptyPickupRetained
            && emptyPickupRetired
            && lootWallBlocked
            && lootWallCleared
            && sourceSealedBeforeOpen
            && contentsConcealedDuringSearch
            && firstOpenMilliseconds >= 650
            && firstOpenMilliseconds <= 2200
            && sourceOpenVisualReady
            && sourceClickActivated
            && emptyPrimaryAutoEquipped
            && policyValid
            && weaponMenuActivated
            && weaponMenuEquipped
            && itemMenuActivated
            && itemMenuDropped
            && heldInputBlocked
            && dragDropRouted
            && returnedToSource
            && groundDropRouted
            && droppedRegistered
            && droppedVisible
            && searchStorageExpanded
            && searchStorageFits
            && storageAtCapacity
            && compactComparisonsComplete
            && compactDirectionsVisible
            && renderedComparisonsComplete
            && reopenedEmpty
            && closedByInteract
            && movementRestored
            && damageClosedLoot
            && damageMovementRestored
            && searchDamageAborted
            && fatalSearchStateHandled
            && activeLootEndHandled;
        GD.Print($"LOOT_CHECK valid={valid} target_matched={targetMatched} open={_hud.IsLootVisible} wall_blocked={lootWallBlocked} wall_cleared={lootWallCleared} sealed_grade={sealedPickupConcealsGrade} sealed_open={sealedPickupOpens} sealed_empty_hidden={sealedPickupEmptyHidden} sealed_return_restored={sealedPickupReturnRestored} tree_loose={treeReconfiguredLoose} tree_sealed={treeReconfiguredSealed} open_empty_retained={openEmptyPickupRetained} empty_retired={emptyPickupRetired} source_sealed={sourceSealedBeforeOpen} search_concealed={contentsConcealedDuringSearch} first_open_ms={firstOpenMilliseconds} source_open_visual={sourceOpenVisualReady} single_click={sourceClickActivated} auto_equip={emptyPrimaryAutoEquipped} policy={policyValid} weapon_menu={weaponMenuActivated}/{weaponMenuReady}/{weaponMenuEquipped} item_menu={itemMenuActivated}/{itemMenuDropOnly}/{itemMenuDropped} held_blocked={heldInputBlocked} drag_drop={dragDropRouted} returned={returnedToSource} ground_route={groundDropRouted} dropped_registered={droppedRegistered} dropped_visible={droppedVisible} storage_expanded={searchStorageExpanded} source_available={searchSourceAvailable} source_size={searchSourceSize} backpack_size={searchBackpackSize} source_cards={searchSourceCards} storage_fits={searchStorageFits} storage_full={storageAtCapacity} compact_comparisons={compactComparisonsComplete} compact_directions={compactDirectionsVisible} rendered_all={renderedComparisonsComplete} reopened_empty={reopenedEmpty} f_closed={closedByInteract} movement={movementRestored} damage_opened={damageViewOpened} damage_overlay_closed={damageOverlayClosed} damage_unlocked={damageUiUnlocked} damage_mouse={damageMouseCaptured} damage_mouse_observable={damageMouseObservable} damage_applied={damageApplied} damage_closed={damageClosedLoot} damage_movement={damageMovementRestored} search_damage_aborted={searchDamageAborted} search_damage_mouse={searchDamageMouseCaptured} fatal_search_state={fatalSearchStateHandled} fatal_input_primed={fatalInputPrimed} fatal_controls_locked={fatalControlLocked} active_loot_end={activeLootEndHandled} fatal_result={_hud.IsMissionResultVisible} fatal_mouse={fatalMouseVisible} fatal_mouse_observable={fatalMouseObservable} equipped={_player.EquippedWeapon.Platform} source_items={source.Loot.Count} backpack={_player.Backpack.Count} damage={stats.Damage:0.0} range={stats.EffectiveRange:0.0} recoil={stats.Recoil:0.00}");
        GD.Print($"LOOT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateCorpseLootFlow()
    {
        var bodyBagProbe = new SquadBodyBag
        {
            Name = "CorpseLootBodyBagProbe",
            Position = new Vector3(0.0f, 0.2f, 82.0f)
        };
        bodyBagProbe.Loot.Add(new LootItem { Kind = LootItemKind.ArmorPlate });
        AddChild(bodyBagProbe);
        await WaitFrames(2);
        var bodyBagClosedVisualReady = bodyBagProbe.ClosedVisualReady;
        bodyBagProbe.ProcessMode = ProcessModeEnum.Disabled;
        bodyBagProbe.OnSearched();
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        var bodyBagOpenVisualReady = bodyBagProbe.OpenVisualReady;
        var bodyBagFlapParts = bodyBagProbe.FlapPartCountForDiagnostics;
        bodyBagProbe.QueueFree();

        var target = _enemies[0];
        var backpackHiddenWhileAlive = !target.CorpseBackpackVisualReady && !target.IsOpened;
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        target.TakeDamage(500.0f, target.GlobalPosition + Vector3.Up * 1.1f, _player);
        await WaitFrames(42);
        var closedBackpackReady = target.CorpseBackpackVisualReady && !target.IsOpened;
        _player.GlobalPosition = target.GlobalPosition + new Vector3(0, 0.2f, 1.5f);
        _missionDirector.ExitDeploymentZone();
        var targetDeadline = Time.GetTicksMsec() + 1000;
        while (!ReferenceEquals(_lootSearchTarget, target) && Time.GetTicksMsec() < targetDeadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        var targetMatched = ReferenceEquals(_lootSearchTarget, target);
        var openedAt = Time.GetTicksMsec();
        Input.ActionPress("interact");
        await WaitFrames(8);
        var contentsConcealedDuringSearch = !_hud.IsLootVisible && !target.IsOpened;
        var deadline = Time.GetTicksMsec() + 2600;
        while (!_hud.IsLootVisible && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ActionRelease("interact");
        var opened = _hud.IsLootVisible;
        var firstOpenMilliseconds = Time.GetTicksMsec() - openedAt;
        await ToSignal(GetTree().CreateTimer(0.55f), SceneTreeTimer.SignalName.Timeout);
        var openedBackpackReady = target.CorpseBackpackOpenVisualReady;
        var weapon = target.Loot.Find(item => item.Kind == LootItemKind.Weapon);
        if (weapon is not null)
        {
            EquipLootItem(weapon.Id);
        }
        await WaitFrames(4);
        var paperDollVisible = _hud.LootPaperDollReady;
        var backpackSlotSeparated = _hud.LootBackpackSlotSeparated;
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
        var targetDeadBeforeReset = target.IsDead;
        var backpackOpenRequests = target.CorpseBackpackOpenRequestsForDiagnostics;
        var backpackOpenBlockedDead = target.CorpseBackpackOpenBlockedDeadForDiagnostics;
        var backpackOpenBlockedVisual = target.CorpseBackpackOpenBlockedVisualForDiagnostics;
        var backpackFlapRotation = target.CorpseBackpackFlapRotationForDiagnostics;
        target.ResetTacticalStateForDiagnostics();
        target.ProcessMode = ProcessModeEnum.Disabled;
        await WaitFrames(2);
        var diagnosticResetClearedBackpack = !target.IsDead
            && !target.CorpseBackpackVisualReady
            && !target.IsOpened;
        var valid = targetDeadBeforeReset
            && targetMatched
            && bodyBagClosedVisualReady
            && bodyBagOpenVisualReady
            && backpackHiddenWhileAlive
            && closedBackpackReady
            && contentsConcealedDuringSearch
            && firstOpenMilliseconds >= 850
            && firstOpenMilliseconds <= 2500
            && opened
            && openedBackpackReady
            && backpackOpenRequests >= 1
            && backpackOpenBlockedDead == 0
            && backpackOpenBlockedVisual == 0
            && reopenedEmpty
            && !target.CarriedWeaponVisible
            && paperDollVisible
            && backpackSlotSeparated
            && diagnosticResetClearedBackpack;
        GD.Print($"CORPSE_LOOT_CHECK valid={valid} dead_before_reset={targetDeadBeforeReset} target_matched={targetMatched} body_bag_closed={bodyBagClosedVisualReady} body_bag_open={bodyBagOpenVisualReady} body_bag_flap_parts={bodyBagFlapParts} backpack_alive_hidden={backpackHiddenWhileAlive} backpack_closed={closedBackpackReady} search_concealed={contentsConcealedDuringSearch} first_open_ms={firstOpenMilliseconds} open={opened} backpack_open={openedBackpackReady} reset_cleared={diagnosticResetClearedBackpack} open_requests={backpackOpenRequests} open_blocked_dead={backpackOpenBlockedDead} open_blocked_visual={backpackOpenBlockedVisual} flap={backpackFlapRotation:0.000} reopened_empty={reopenedEmpty} weapon_visible={target.CarriedWeaponVisible} equipment={equipmentCount} items={target.Loot.Count} paper_doll={paperDollVisible} backpack_isolated={backpackSlotSeparated} equipped={_player.EquippedWeapon.Platform}");
        GD.Print($"CORPSE_LOOT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureBackpackFrame()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        SetCaptureLanguage("en");
        _player.ClearBackpackForDiagnostics();
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Bandage, Quantity = 2, Grade = LootGrade.Common });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.FieldMedkit, Quantity = 1, Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Medical, MedicalKind = MedicalItemKind.Adrenaline, Quantity = 1, Grade = LootGrade.Epic });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.ArmorPlate, Quantity = 2, Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Sniper, Quantity = 18, Grade = LootGrade.Epic });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.KnifeSkin, KnifeSkinId = "knife_arctic", Grade = LootGrade.Epic });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Weapon, Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 2), Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("armor_heavy"), Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("pack_heavy"), Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Equipment, Equipment = EquipmentCatalog.Create("armor_patrol"), Grade = LootGrade.Common });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Valuable, ValuableKind = ValuableItemKind.GraphicsCard, Grade = LootGrade.Rare });
        _player.TryStoreInBackpack(new LootItem { Kind = LootItemKind.Valuable, ValuableKind = ValuableItemKind.AntiqueClock, Grade = LootGrade.Legendary });
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
        _player.GrantFireablePrimaryForDiagnostics();
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
        var originalWeaponGrade = _player.EquippedWeaponGrade;
        var swappedWeapon = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 2),
            Grade = LootGrade.Legendary
        });
        var firstWeaponGradePreserved = swappedWeapon is null
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Secondary
            && _player.EquippedWeaponGrade == LootGrade.Legendary
            && _player.PrimaryWeaponBuild?.Platform == WeaponPlatform.M4A1;
        var returnedWeapon = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.VSS, 1),
            Grade = originalWeaponGrade
        });
        var weaponGradeRoundTrip = firstWeaponGradePreserved
            && _player.EquippedWeaponGrade == originalWeaponGrade
            && returnedWeapon?.Weapon?.Platform == WeaponPlatform.AK74
            && returnedWeapon.Grade == LootGrade.Legendary
            && _player.PrimaryWeaponBuild?.Platform == WeaponPlatform.M4A1;
        var originalOpticGrade = _player.EquippedAttachmentGrade(AttachmentSlot.Optic);
        var swappedOptic = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Attachment,
            AttachmentId = "optic_scope",
            Grade = LootGrade.Epic
        });
        var firstOpticGradePreserved = _player.EquippedAttachmentGrade(AttachmentSlot.Optic) == LootGrade.Epic
            && swappedOptic?.Grade == originalOpticGrade;
        var returnedOptic = swappedOptic is null ? null : _player.EquipFromLoot(swappedOptic);
        var attachmentGradeRoundTrip = firstOpticGradePreserved
            && _player.EquippedAttachmentGrade(AttachmentSlot.Optic) == originalOpticGrade
            && returnedOptic?.Grade == LootGrade.Epic;
        var originalKnifeSkin = _player.EquippedKnifeSkinId;
        var originalKnifeGrade = _player.EquippedKnifeGrade;
        var swappedKnife = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.KnifeSkin,
            KnifeSkinId = "knife_arctic",
            Grade = LootGrade.Legendary
        });
        var firstKnifeGradePreserved = _player.EquippedKnifeSkinId == "knife_arctic"
            && _player.EquippedKnifeGrade == LootGrade.Legendary
            && swappedKnife is not null
            && swappedKnife.KnifeSkinId == originalKnifeSkin
            && swappedKnife.Grade == originalKnifeGrade;
        var returnedKnife = swappedKnife is null ? null : _player.EquipFromLoot(swappedKnife);
        var knifeGradeRoundTrip = firstKnifeGradePreserved
            && _player.EquippedKnifeSkinId == originalKnifeSkin
            && _player.EquippedKnifeGrade == originalKnifeGrade
            && returnedKnife is not null
            && returnedKnife.KnifeSkinId == "knife_arctic"
            && returnedKnife.Grade == LootGrade.Legendary;
        var targetedPrimary = new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.AXMC, 3),
            Grade = LootGrade.Legendary
        };
        var targetedOptic = new LootItem
        {
            Kind = LootItemKind.Attachment,
            AttachmentId = "optic_7x",
            Grade = LootGrade.Epic
        };
        _player.ClearBackpackForDiagnostics();
        var targetedPrimaryStored = _player.TryStoreInBackpack(targetedPrimary);
        var targetedOpticStored = _player.TryStoreInBackpack(targetedOptic);
        OpenPersonalBackpack();
        await WaitFrames(6);
        var secondaryBeforeTargetedDrop = _player.SecondaryWeaponBuild?.Platform;
        var primaryDropRouted = _hud.DropLootOnWeaponSlotForDiagnostics(
            targetedPrimary,
            PlayerWeaponSlot.Primary);
        await WaitFrames(2);
        var primarySlotReplaced = targetedPrimaryStored
            && primaryDropRouted
            && _player.PrimaryWeaponBuild?.Platform == WeaponPlatform.AXMC
            && _player.PrimaryWeaponGrade == LootGrade.Legendary
            && _player.SecondaryWeaponBuild?.Platform == secondaryBeforeTargetedDrop
            && _player.Backpack.Any(item => item.Kind == LootItemKind.Weapon
                && item.Weapon?.Platform == WeaponPlatform.M4A1
                && item.Grade == originalWeaponGrade);
        var primaryOpticBeforeAttachmentDrop = _player.PrimaryWeaponBuild?.Attachments
            .GetValueOrDefault(AttachmentSlot.Optic);
        var secondaryOpticDropRouted = _hud.DropLootOnWeaponSlotForDiagnostics(
            targetedOptic,
            PlayerWeaponSlot.Secondary);
        await WaitFrames(2);
        var secondaryOpticAfterTargetedDrop = _player.SecondaryWeaponBuild?.Attachments
            .GetValueOrDefault(AttachmentSlot.Optic);
        var primaryOpticAfterTargetedDrop = _player.PrimaryWeaponBuild?.Attachments
            .GetValueOrDefault(AttachmentSlot.Optic);
        var secondaryAttachmentTargeted = targetedOpticStored
            && secondaryOpticDropRouted
            && secondaryOpticAfterTargetedDrop == "optic_7x"
            && primaryOpticAfterTargetedDrop == primaryOpticBeforeAttachmentDrop;
        var comparisonCards = _hud.LootComparisonCardCount;
        var comparisonDirections = _hud.LootComparisonHasUpgrade && _hud.LootComparisonHasDowngrade;
        var gradeColorsStable = _hud.LootGradeColorsConsistent;
        var renderedComparisonsComplete = _hud.LootComparisonsFullyRendered;
        var attachmentComparisonRendered = _hud.LootAttachmentComparisonRendered;
        var equippedGradeStylesStable = _hud.LootEquippedGradeStylesConsistent;
        _hud.ShowWeaponDetails(_player.EquippedWeapon);
        await WaitFrames(2);
        var detailsOpened = _hud.IsWeaponDetailVisible;
        var footerRuntimeSeparated = _hud.FooterHudRuntimeSeparatedForDiagnostics;
        var footerResponsive = _hud.FooterHudResponsiveScenariosValidForDiagnostics;
        var footerInitiallyVisible = _hud.WeaponHudVisibleForDiagnostics
            && _hud.ClassSkillHudVisibleForDiagnostics;
        _hud.ShowDownedState(10.0f);
        var downedFooterSuppressed = _hud.IsDownedBannerVisible
            && _hud.DownedFooterSuppressedForDiagnostics;
        _hud.HideDownedState();
        var downedFooterRestored = !_hud.IsDownedBannerVisible
            && _hud.WeaponHudVisibleForDiagnostics
            && _hud.ClassSkillHudVisibleForDiagnostics;
        var valid = cycledToKnife
            && cycledToPrimary
            && detailsOpened
            && comparisonCards >= 2
            && comparisonDirections
            && gradeColorsStable
            && renderedComparisonsComplete
            && attachmentComparisonRendered
            && equippedGradeStylesStable
            && weaponGradeRoundTrip
            && attachmentGradeRoundTrip
            && primarySlotReplaced
            && secondaryAttachmentTargeted
            && knifeGradeRoundTrip
            && footerRuntimeSeparated
            && footerResponsive
            && footerInitiallyVisible
            && downedFooterSuppressed
            && downedFooterRestored;
        GD.Print($"WEAPON_UI_CHECK valid={valid} knife={cycledToKnife} primary={cycledToPrimary} details={detailsOpened} platform={_player.EquippedWeapon.Platform} comparisons={comparisonCards} directions={comparisonDirections} rendered_all={renderedComparisonsComplete} attachment_comparison={attachmentComparisonRendered} grade_colors={gradeColorsStable} equipped_grade_styles={equippedGradeStylesStable} weapon_grade={weaponGradeRoundTrip} attachment_grade={attachmentGradeRoundTrip} targeted_primary={primarySlotReplaced} targeted_primary_stored={targetedPrimaryStored} targeted_primary_routed={primaryDropRouted} targeted_attachment={secondaryAttachmentTargeted} targeted_attachment_stored={targetedOpticStored} targeted_attachment_routed={secondaryOpticDropRouted} targeted_secondary_optic={secondaryOpticAfterTargetedDrop ?? "none"} targeted_primary_optic={primaryOpticAfterTargetedDrop ?? "none"} targeted_primary_optic_before={primaryOpticBeforeAttachmentDrop ?? "none"} knife_grade={knifeGradeRoundTrip} footer_runtime={footerRuntimeSeparated} footer_responsive={footerResponsive} footer_visible={footerInitiallyVisible} downed_suppressed={downedFooterSuppressed} downed_restored={downedFooterRestored} {_hud.FooterHudLayoutForDiagnostics}");
        GD.Print($"WEAPON_UI_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateArsenalFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        await WaitFrames(4);

        var sniper = WeaponCatalog.Build(WeaponPlatform.M24, 2);
        var magnum = WeaponCatalog.Build(WeaponPlatform.AXMC, 3);
        var smg = WeaponCatalog.Build(WeaponPlatform.MP5A5, 1);
        var sniperDefinition = WeaponCatalog.Weapon(WeaponPlatform.M24);
        var magnumDefinition = WeaponCatalog.Weapon(WeaponPlatform.AXMC);
        var smgDefinition = WeaponCatalog.Weapon(WeaponPlatform.MP5A5);
        var hasSevenPowerOptic = magnum.Attachments.TryGetValue(AttachmentSlot.Optic, out var magnumOptic)
            && magnumOptic == "optic_7x";
        var catalogOk = WeaponCatalog.AllWeapons.Count >= 7
            && sniperDefinition.Caliber == AmmoCaliber.Sniper
            && !sniperDefinition.SupportsAutomatic
            && sniper.Stats().MagazineSize == 5
            && sniper.Stats().EffectiveRange >= 380.0f
            && magnumDefinition.Caliber == AmmoCaliber.Magnum338
            && !magnumDefinition.SupportsAutomatic
            && magnum.Stats().MagazineSize == 5
            && magnum.Stats().Damage >= 145.0f
            && magnum.Stats().EffectiveRange >= 700.0f
            && hasSevenPowerOptic
            && smgDefinition.Caliber == AmmoCaliber.Smg
            && smgDefinition.SupportsAutomatic
            && smg.Stats().FireInterval <= 0.08f;

        _player.ApplyColdStartUnarmed();
        _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Weapon, Weapon = sniper, Grade = LootGrade.Legendary });
        var sniperEquipped = _player.EquippedWeapon.Platform == WeaponPlatform.M24
            && _player.CurrentAmmoCaliber == AmmoCaliber.Sniper
            && _player.Ammo == 5;
        _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Rifle, Quantity = 30 });
        var wrongCaliberSeparated = _player.ReserveAmmo == 0
            && _player.AmmoReserveFor(AmmoCaliber.Rifle) == 30;
        _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Sniper, Quantity = 18 });
        var sniperAmmoLoaded = _player.ReserveAmmo == 18;

        var oldSkin = _player.EquippedKnifeSkinId;
        var replacedSkin = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.KnifeSkin,
            KnifeSkinId = "knife_crimson",
            Grade = LootGrade.Epic
        });
        var skinEquipped = _player.EquippedKnifeSkinId == "knife_crimson"
            && replacedSkin?.Kind == LootItemKind.KnifeSkin
            && replacedSkin.KnifeSkinId == oldSkin
            && KnifeSkinCatalog.All.Count >= 4;

        _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Weapon, Weapon = smg, Grade = LootGrade.Rare });
        var independentSmgReserve = _player.CurrentAmmoCaliber == AmmoCaliber.Smg
            && _player.ReserveAmmo == 0
            && _player.AmmoReserveFor(AmmoCaliber.Sniper) == 18;
        _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Ammunition, AmmoCaliber = AmmoCaliber.Smg, Quantity = 72 });
        independentSmgReserve = independentSmgReserve && _player.ReserveAmmo == 72;

        _player.EquipFromLoot(new LootItem { Kind = LootItemKind.Weapon, Weapon = magnum, Grade = LootGrade.Legendary });
        var magnumEquipped = _player.EquippedWeapon.Platform == WeaponPlatform.AXMC
            && _player.CurrentAmmoCaliber == AmmoCaliber.Magnum338
            && _player.ReserveAmmo == 0
            && _player.Ammo == 5
            && Mathf.IsEqualApprox(_player.CurrentAimFieldOfView, 19.0f)
            && _player.AmmoReserveFor(AmmoCaliber.Sniper) == 18
            && _player.AmmoReserveFor(AmmoCaliber.Smg) == 72;
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = AmmoCaliber.Magnum338,
            Quantity = 30,
            Grade = LootGrade.Legendary
        });
        magnumEquipped = magnumEquipped && _player.ReserveAmmo == 30;

        var worldM24 = false;
        var worldMp5 = false;
        var worldSniperAmmo = false;
        var worldKnifeSkins = 0;
        foreach (var source in _lootSources)
        {
            foreach (var item in source.Loot)
            {
                worldM24 |= item.Weapon?.Platform == WeaponPlatform.M24;
                worldMp5 |= item.Weapon?.Platform == WeaponPlatform.MP5A5;
                worldSniperAmmo |= item.Kind == LootItemKind.Ammunition && item.AmmoCaliber == AmmoCaliber.Sniper;
                if (item.Kind == LootItemKind.KnifeSkin)
                {
                    worldKnifeSkins++;
                }
            }
        }
        var worldLootOk = worldM24 && worldMp5 && worldSniperAmmo && worldKnifeSkins >= 3;
        var bossRewardReady = IsInstanceValid(_worldBoss)
            && _worldBoss!.Loot.Any(item => item.Weapon?.Platform == WeaponPlatform.AXMC)
            && _worldBoss.Loot.Any(item => item.Kind == LootItemKind.Attachment && item.AttachmentId == "optic_7x")
            && _worldBoss.Loot.Any(item => item.Kind == LootItemKind.Ammunition && item.AmmoCaliber == AmmoCaliber.Magnum338);
        var valid = catalogOk && sniperEquipped && wrongCaliberSeparated && sniperAmmoLoaded
            && skinEquipped && independentSmgReserve && magnumEquipped && worldLootOk && bossRewardReady;
        GD.Print($"ARSENAL_CHECK valid={valid} catalog={catalogOk} sniper={sniperEquipped} dedicated_ammo={wrongCaliberSeparated && sniperAmmoLoaded} smg={independentSmgReserve} magnum={magnumEquipped} optic_7x={hasSevenPowerOptic} ads_fov={_player.CurrentAimFieldOfView:0.0} skins={KnifeSkinCatalog.All.Count} world_m24={worldM24} world_mp5={worldMp5} world_sniper_ammo={worldSniperAmmo} world_skins={worldKnifeSkins} boss_reward={bossRewardReady}");
        GD.Print($"ARSENAL_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureOpticsFrames()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var optics = new[]
        {
            (Id: "optic_micro", File: "optic_micro_validation.png", Platform: WeaponPlatform.M4A1),
            (Id: "optic_holo", File: "optic_holo_validation.png", Platform: WeaponPlatform.M4A1),
            (Id: "optic_scope", File: "optic_scope_validation.png", Platform: WeaponPlatform.ScarL),
            (Id: "optic_7x", File: "optic_7x_validation.png", Platform: WeaponPlatform.AXMC),
            (Id: "optic_sniper", File: "optic_sniper_validation.png", Platform: WeaponPlatform.M24)
        };
        foreach (var optic in optics)
        {
            var build = WeaponCatalog.Build(optic.Platform, 3);
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
            GD.Print($"OPTIC_CHECK id={optic.Id} platform={optic.Platform} visible=true aiming={aiming} handling={_player.CurrentWeaponStats.Handling:0.00}");
        }
        GetTree().Quit();
    }

    private async void ValidateStanceAndArmorFlow()
    {
        _player.GrantFireablePrimaryForDiagnostics();
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        _missionDirector.ExitDeploymentZone();
        _player.GlobalPosition = new Vector3(8.0f, 0.2f, -8.0f);
        _player.Velocity = Vector3.Zero;
        await WaitFrames(3);
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
        Input.ActionPress("jump");
        await WaitFrames(2);
        Input.ActionRelease("jump");
        var crouchJumpedStanding = _player.Stance == PlayerStance.Standing && _player.Velocity.Y > 0.1f;
        for (var frame = 0; frame < 100 && !_player.IsOnFloor(); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var prone = _player.TrySetStance(PlayerStance.Prone);
        await WaitFrames(18);
        var proneHeight = _player.ViewHeight;
        Input.ActionPress("jump");
        await WaitFrames(2);
        Input.ActionRelease("jump");
        var proneJumpedStanding = _player.Stance == PlayerStance.Standing && _player.Velocity.Y > 0.1f;
        var healthBefore = _player.Health;
        var helmetBefore = _player.EquippedHelmet.Durability;
        _player.TakeDamage(20.0f, _player.HitPoint(HitRegion.Head), attacker);
        var helmetAfter = _player.EquippedHelmet.Durability;
        var armorBefore = _player.EquippedBodyArmor.Durability;
        _player.TakeDamage(20.0f, _player.HitPoint(HitRegion.Torso), attacker);
        var armorAfter = _player.EquippedBodyArmor.Durability;
        var valid = crouched && crouchLean && crouchJumpedStanding && prone && proneJumpedStanding
            && crouchHeight > proneHeight
            && helmetAfter < helmetBefore
            && armorAfter < armorBefore;
        GD.Print($"STANCE_ARMOR_CHECK valid={valid} crouched={crouched} crouch_height={crouchHeight:0.00} crouch_lean_ads={crouchLean} crouch_jump_stand={crouchJumpedStanding} prone={prone} prone_height={proneHeight:0.00} prone_jump_stand={proneJumpedStanding} health_loss={healthBefore - _player.Health:0.0} helmet_loss={helmetBefore - helmetAfter:0.0} armor_loss={armorBefore - armorAfter:0.0}");
        GD.Print($"STANCE_ARMOR_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureExpandedMapFrame()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        var aircraft = _aircraft ?? _levelRoot.GetNodeOrNull<Node3D>("DistantTiltRotor");
        var aircraftStart = aircraft?.Position ?? Vector3.Zero;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _hud.Visible = false;
        _extractionMarker.Visible = true;
        var overview = new Camera3D
        {
            Name = "ExpansionOverviewCamera",
            Fov = 62.0f,
            Far = 680.0f
        };
        AddChild(overview);
        overview.GlobalPosition = new Vector3(0, 325.0f, MapCenterZ);
        overview.LookAt(new Vector3(0, 0, MapCenterZ), Vector3.Forward);
        overview.MakeCurrent();
        await WaitFrames(48);
        SaveViewportImage("res://expanded_map_validation.png");

        overview.GlobalPosition = new Vector3(-20.0f, 18.0f, 38.0f);
        overview.Fov = 52.0f;
        overview.LookAt(new Vector3(35.0f, 12.0f, 33.0f), Vector3.Up);
        await WaitFrames(22);
        SaveViewportImage("res://radar_spire_validation.png");

        overview.GlobalPosition = new Vector3(-4.0f, 12.0f, -58.0f);
        overview.Fov = 58.0f;
        overview.LookAt(new Vector3(-5.0f, 1.8f, -124.0f), Vector3.Up);
        await WaitFrames(22);
        SaveViewportImage("res://cover_density_validation.png");

        overview.GlobalPosition = new Vector3(-18.0f, 58.0f, 28.0f);
        overview.Fov = 64.0f;
        overview.LookAt(new Vector3(2.0f, 5.0f, -68.0f), Vector3.Up);
        await WaitFrames(22);
        SaveViewportImage("res://district_network_validation.png");

        var stairHub = _districtRouteHubs.First(hub => hub.Id == "OpsGate");
        overview.GlobalPosition = new Vector3(59.0f, 8.2f, -24.0f);
        overview.Fov = 54.0f;
        overview.LookAt((stairHub.StairStart + stairHub.DeckCenter) * 0.5f + Vector3.Up * 0.35f, Vector3.Up);
        await WaitFrames(22);
        SaveViewportImage("res://district_stair_validation.png");

        var landmarksPresent = _levelRoot.GetNodeOrNull<Node3D>("CommandCore") is not null
            && _levelRoot.GetNodeOrNull<Node3D>("RadarFoundation") is not null;
        var aircraftMoving = aircraft is not null && aircraft.Position.DistanceTo(aircraftStart) > 0.1f;
        var dynamicSky = _environmentRef.Sky?.SkyMaterial is ShaderMaterial;
        GD.Print($"MAP_CHECK width={MapWidthMeters:0} depth={MapDepthMeters:0} loot_sources={_lootSources.Count} hostiles={_enemiesRemaining} extraction_distance={DeploymentPoint.DistanceTo(ExtractionPoint):0.0} landmarks={landmarksPresent} cover_points={_coverPoints.Length} dynamic_sky={dynamicSky} aircraft_moving={aircraftMoving} residential_towers={ResidentialTowerCount} civilians={ResidentialCivilianCount} complex_buildings={ComplexBuildingCount} complex_rooms={ComplexRoomCount} complex_props={ComplexInteriorPropCount}");
        GetTree().Quit();
    }

    private async void ValidateAircraftCombat()
    {
        await WaitFrames(8);
        var aircraft = _aircraft ?? _levelRoot.GetNodeOrNull<DestructibleAircraft>("DistantTiltRotor");
        if (aircraft is null)
        {
            GD.Print("AIRCRAFT_COMBAT_CHECK valid=False reason=missing");
            GetTree().Quit(2);
            return;
        }

        var patrolStart = aircraft.GlobalPosition;
        var patrolDistanceStart = aircraft.PatrolDistanceTravelled;
        var maximumPatrolStep = 0.0f;
        for (var frame = 0; frame < 30; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            maximumPatrolStep = Mathf.Max(maximumPatrolStep, aircraft.LastPatrolStepDistance);
        }
        var patrolDistance = aircraft.PatrolDistanceTravelled - patrolDistanceStart;
        var patrolDisplacement = aircraft.GlobalPosition.DistanceTo(patrolStart);
        aircraft.SetPatrolPhaseForDiagnostics(Mathf.Tau - 0.001f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var patrolWrapStep = aircraft.LastPatrolStepDistance;
        var smoothPatrol = maximumPatrolStep < 0.75f
            && patrolWrapStep < 0.75f
            && patrolDistance > 6.0f
            && patrolDisplacement > 5.0f;
        var durable = aircraft.MaxHealth >= 1200.0f
            && Mathf.IsEqualApprox(aircraft.Health, aircraft.MaxHealth);

        _missionDirector.ExitDeploymentZone();
        var salvosBefore = aircraft.AttackSalvosFired;
        var fired = false;
        var roofBlocked = false;
        var narrowAttack = false;
        var attackOrbitReady = false;
        var maximumAttackStep = 0.0f;
        var attackGroundPoint = new Vector3(150.0f, 0.0f, 82.0f);
        if (PhysicsRaycast.TryHit(
                GetWorld3D().DirectSpaceState,
                attackGroundPoint + Vector3.Up * 90.0f,
                attackGroundPoint + Vector3.Down * 6.0f,
                aircraft.GetRid(),
                1,
                out var attackGroundHit))
        {
            var attackSurface = attackGroundHit.Position;
            _player.ProcessMode = ProcessModeEnum.Disabled;
            _player.GlobalPosition = attackSurface + Vector3.Up * 0.3f;
            _player.Velocity = Vector3.Zero;
            aircraft.SetAttackTargetForDiagnostics(_player, Vector3.Right * 36.0f);
            for (var frame = 0; frame < 180 && !attackOrbitReady; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                maximumAttackStep = Mathf.Max(maximumAttackStep, aircraft.LastPatrolStepDistance);
                attackOrbitReady = aircraft.IsAttackOrbitActive
                    && aircraft.AttackHorizontalDistance <= DestructibleAircraft.AttackOrbitRadius + 1.5f;
            }
            attackOrbitReady &= maximumAttackStep < 0.5f;

            var roof = new StaticBody3D
            {
                Name = "AircraftAttackRoofDiagnostic",
                Position = _player.GlobalPosition + Vector3.Up * 3.0f,
                CollisionLayer = 1,
                CollisionMask = 0
            };
            roof.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(10.0f, 0.4f, 10.0f) }
            });
            AddChild(roof);
            await WaitFrames(2);
            aircraft.SetAttackTargetForDiagnostics(
                _player,
                Vector3.Right * DestructibleAircraft.AttackOrbitRadius);
            var blockedSalvos = aircraft.AttackSalvosFired;
            roofBlocked = !aircraft.TryAttackTarget(_player, ignoreCooldown: true)
                && aircraft.AttackSalvosFired == blockedSalvos
                && !aircraft.LastAttackPathClear;
            roof.QueueFree();
            await WaitFrames(2);

            aircraft.SetAttackTargetForDiagnostics(
                _player,
                Vector3.Right * DestructibleAircraft.AttackOrbitRadius);
            fired = aircraft.TryAttackTarget(_player, ignoreCooldown: true);
            narrowAttack = fired
                && aircraft.LastAttackPathClear
                && aircraft.LastAttackAngleDegrees <= DestructibleAircraft.MaximumAttackAngleDegrees;
        }
        await WaitFrames(6);
        var shellNodes = GetTree().GetNodesInGroup("aircraft_shells");
        using var shellNodesBacking = shellNodes.AsDisposable();
        var shellSpawned = shellNodes.Count > 0 || aircraft.AttackSalvosFired > salvosBefore;
        AircraftShell? shell = null;
        foreach (var node in shellNodes)
        {
            if (node is AircraftShell candidate && IsInstanceValid(candidate) && !candidate.IsDestroyed)
            {
                shell = candidate;
                break;
            }
        }
        // If the first bomb already landed, spawn a diagnostic bomb for invulnerability proof.
        if (shell is null)
        {
            SpawnAircraftShell(
                aircraft.GlobalPosition + Vector3.Down * 2.0f,
                aircraft.GlobalPosition + new Vector3(0, -8.0f, 12.0f),
                40.0f,
                12.0f,
                aircraft);
            await WaitFrames(2);
            var diagnosticShellNodes = GetTree().GetNodesInGroup("aircraft_shells");
            using var diagnosticShellNodesBacking = diagnosticShellNodes.AsDisposable();
            foreach (var node in diagnosticShellNodes)
            {
                if (node is AircraftShell candidate && IsInstanceValid(candidate) && !candidate.IsDestroyed)
                {
                    shell = candidate;
                    break;
                }
            }
        }

        var uninterruptible = false;
        var ownerCollisionExcluded = false;
        if (shell is not null)
        {
            ownerCollisionExcluded = shell.OwnerCollisionExcluded;
            var interrupted = shell.TakeDamage(999.0f, shell.GlobalPosition, _player);
            await WaitFrames(3);
            uninterruptible = !interrupted && !shell.InterceptedInAir && !shell.IsDestroyed;
        }

        var landingPoint = new Vector3(150.0f, 0.0f, 82.0f);
        var missileLanded = false;
        var missileLandingLow = false;
        if (PhysicsRaycast.TryHit(
                GetWorld3D().DirectSpaceState,
                landingPoint + Vector3.Up * 90.0f,
                landingPoint + Vector3.Down * 6.0f,
                aircraft.GetRid(),
                1,
                out var landingHit))
        {
            var surface = landingHit.Position;
            var landingShell = new AircraftShell
            {
                Name = "AircraftLandingDiagnosticShell",
                Main = this,
                OwnerAircraft = aircraft,
                Position = surface + Vector3.Up * 12.0f
            };
            landingShell.Detonated += onGround =>
            {
                missileLanded = onGround;
                missileLandingLow = landingShell.GlobalPosition.Y <= surface.Y + 1.0f;
            };
            AddChild(landingShell);
            landingShell.Launch(surface + Vector3.Up * 12.0f, surface, 1.0f, 1.0f);
            for (var frame = 0; frame < 90 && !missileLanded; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
        }

        // Ground blast path still hurts operators when a shell is allowed to land.
        var healthBefore = _player.Health;
        ApplyAircraftStrike(_player.GlobalPosition + Vector3.Up, 10.0f, 28.0f, aircraft);
        await WaitFrames(2);
        var playerHurt = _player.Health < healthBefore || _player.IsDead;
        var stillAlive = !aircraft.IsDestroyed;
        var salvosFired = aircraft.AttackSalvosFired;
        var lastAttackDamage = aircraft.LastAttackDamage;
        var dropsBefore = _aircraftSupplyDrops.Count(drop => IsInstanceValid(drop));
        aircraft.GlobalPosition = new Vector3(22.0f, 1.3f, 42.0f);
        var lethalDamage = aircraft.MaxHealth + 1.0f;
        var destroyed = aircraft.TakeDamage(lethalDamage, aircraft.GlobalPosition, _player);
        var repeatDamageIgnored = !aircraft.TakeDamage(lethalDamage, aircraft.GlobalPosition, _player);
        for (var frame = 0; frame < 45 && _aircraftSupplyDrops.Count(drop => IsInstanceValid(drop)) == dropsBefore; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var dropsAfter = _aircraftSupplyDrops.Count(drop => IsInstanceValid(drop));
        var supplyDrop = _aircraftSupplyDrops.LastOrDefault(drop => IsInstanceValid(drop));
        var dropSpawnedOnce = dropsAfter == dropsBefore + 1;
        var dropRegistered = supplyDrop is not null && _lootSources.Contains(supplyDrop);
        var dropStocked = supplyDrop is not null
            && supplyDrop.Loot.Count >= 7
            && supplyDrop.Loot.Any(item => item.Kind == LootItemKind.Weapon && item.Grade >= LootGrade.Epic)
            && supplyDrop.Loot.Any(item => item.Kind == LootItemKind.Equipment && item.Grade >= LootGrade.Epic)
            && supplyDrop.Loot.Any(item => item.Kind == LootItemKind.Medical)
            && supplyDrop.Loot.Any(item => item.Kind == LootItemKind.Valuable && item.Grade == LootGrade.Legendary);
        var dropVisible = supplyDrop is not null
            && supplyDrop.HasBeacon
            && supplyDrop.VisualPartCount >= 12;
        var dropGrounded = false;
        var dropSearchable = false;
        if (supplyDrop is not null)
        {
            dropGrounded = supplyDrop.GroundResolved
                && PhysicsRaycast.HasHit(
                    GetWorld3D().DirectSpaceState,
                    supplyDrop.GlobalPosition + Vector3.Up * 0.12f,
                    supplyDrop.GlobalPosition + Vector3.Down * 0.45f,
                    supplyDrop.GetRid(),
                    1);
            supplyDrop.OnSearched();
            dropSearchable = supplyDrop.IsSearchable && supplyDrop.IsOpened;
        }
        var slowEnoughToDodge = AircraftShell.TravelSpeed <= 22.0f;
        var valid = fired
            && roofBlocked
            && narrowAttack
            && attackOrbitReady
            && shellSpawned
            && uninterruptible
            && ownerCollisionExcluded
            && missileLanded
            && missileLandingLow
            && smoothPatrol
            && durable
            && slowEnoughToDodge
            && playerHurt
            && stillAlive
            && destroyed
            && repeatDamageIgnored
            && dropSpawnedOnce
            && dropRegistered
            && dropStocked
            && dropVisible
            && dropGrounded
            && dropSearchable;
        GD.Print($"AIRCRAFT_COMBAT_CHECK valid={valid} fired={fired} roof_blocked={roofBlocked} narrow_attack={narrowAttack} attack_orbit={attackOrbitReady} attack_distance={aircraft.AttackHorizontalDistance:0.0} attack_angle={aircraft.LastAttackAngleDegrees:0.0}/{DestructibleAircraft.MaximumAttackAngleDegrees:0.0} attack_step={maximumAttackStep:0.00} path_clear={aircraft.LastAttackPathClear} shell={shellSpawned} uninterruptible={uninterruptible} owner_excluded={ownerCollisionExcluded} missile_landed={missileLanded} missile_low={missileLandingLow} shell_speed={AircraftShell.TravelSpeed:0.0} cruise_speed={DestructibleAircraft.CruiseSpeed:0.0} patrol_distance={patrolDistance:0.0} max_step={maximumPatrolStep:0.00} wrap_step={patrolWrapStep:0.00} smooth_patrol={smoothPatrol} health={aircraft.MaxHealth:0} durable={durable} salvos={salvosFired} last_damage={lastAttackDamage:0.0} player_hurt={playerHurt} destroyed={destroyed} repeat_ignored={repeatDamageIgnored} drop_once={dropSpawnedOnce} drop_registered={dropRegistered} drop_stocked={dropStocked} drop_visible={dropVisible} drop_grounded={dropGrounded} drop_searchable={dropSearchable}");
        GD.Print($"AIRCRAFT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateMapDensity()
    {
        await WaitFrames(4);
        var customs = _levelRoot.GetNodeOrNull<Node3D>("CustomsWarehouseComplex") is not null;
        var ops = _levelRoot.GetNodeOrNull<Node3D>("OpsAnnexComplex") is not null;
        var fuel = _levelRoot.GetNodeOrNull<Node3D>("FuelLogisticsHall") is not null;
        var quay = _levelRoot.GetNodeOrNull<Node3D>("QuayBondedStorage") is not null;
        var hangarEnriched = _levelRoot.GetNodeOrNull("MaintenanceDistrict/HangarOfficeFloor_E") is not null
            || (_levelRoot.GetNodeOrNull<Node3D>("MaintenanceDistrict")?.FindChild("HangarOfficeFloor_E", true, false) is not null);
        var specialLandmarks = SpecialLandmarkCount == 4
            && SpecialLandmarkLootCount >= 28
            && SpecialLandmarkVerticalRouteCount >= 5;
        var buildingLootReachable = 0;
        var unreachableBuildingLoot = new List<string>();
        foreach (var pickup in _lootSources.OfType<GradedLootPickup>())
        {
            if (!pickup.Name.ToString().StartsWith("BuildingLoot_", StringComparison.Ordinal))
            {
                continue;
            }
            if (HasClearLootInteractionApproach(pickup))
            {
                buildingLootReachable++;
            }
            else
            {
                unreachableBuildingLoot.Add(
                    $"{pickup.Name}@({pickup.GlobalPosition.X:0.0},{pickup.GlobalPosition.Y:0.0},{pickup.GlobalPosition.Z:0.0})");
            }
        }
        // Baseline: need multiple large complexes and interior density above empty-shell thresholds.
        var valid = ComplexBuildingCount >= 5
            && ComplexRoomCount >= 12
            && ComplexInteriorPropCount >= 40
            && ComplexRoomLootCount >= ComplexRoomCount
            && _buildingLootPickupCount >= ComplexRoomCount + FixedBuildingLootPlacementCount
            && buildingLootReachable == _buildingLootPickupCount
            && customs && ops && fuel && quay
            && hangarEnriched
            && ResidentialTowerCount >= 11
            && specialLandmarks;
        GD.Print($"MAP_DENSITY_CHECK valid={valid} buildings={ComplexBuildingCount} rooms={ComplexRoomCount} room_loot={ComplexRoomLootCount}/{ComplexRoomCount} building_loot={_buildingLootPickupCount}/{ComplexRoomCount + FixedBuildingLootPlacementCount} reachable_loot={buildingLootReachable}/{_buildingLootPickupCount} unreachable={string.Join(';', unreachableBuildingLoot)} props={ComplexInteriorPropCount} customs={customs} ops={ops} fuel={fuel} quay={quay} hangar={hangarEnriched} towers={ResidentialTowerCount} special_landmarks={SpecialLandmarkCount} special_loot={SpecialLandmarkLootCount} vertical_routes={SpecialLandmarkVerticalRouteCount}");
        GD.Print($"MAP_DENSITY_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateExtractionSpawns()
    {
        await WaitFrames(6);
        EnsureAiSquadFill();
        var playerSquadOk = ActiveSquadCount == 3 && AiSquadCount == 2;
        var hostileOk = HostileSquadCount == ExtractionSpawnPads.HostileSquadTargetCount;
        var teamTotalOk = HostileSquadCount + 1 == ExtractionSpawnPads.OperatorTeamCount;
        var sizesOk = true;
        var nearestHostileMember = float.PositiveInfinity;
        var pads = new List<Vector3> { DeploymentPoint };
        var playerOnEdge = false;
        foreach (var edge in ExtractionSpawnPads.Pads)
        {
            if (edge.DistanceTo(DeploymentPoint) < 1.0f)
            {
                playerOnEdge = true;
                break;
            }
        }
        foreach (var squad in _hostileSquads)
        {
            if (squad.Members.Count != ExtractionSpawnPads.SquadSize)
            {
                sizesOk = false;
            }
            pads.Add(squad.SpawnPad);
            foreach (var member in squad.Members)
            {
                if (!IsInstanceValid(member) || member.TeamId != squad.TeamId || !member.IsRivalSquad)
                {
                    sizesOk = false;
                    continue;
                }
                nearestHostileMember = Mathf.Min(
                    nearestHostileMember,
                    HorizontalDistance(member.GlobalPosition, DeploymentPoint));
            }
        }
        var minDist = ExtractionSpawnPads.MinPairwiseDistance(pads);
        var separated = minDist >= ExtractionSpawnPads.MinPadSeparationMeters * 0.95f;
        var playerClear = true;
        foreach (var squad in _hostileSquads)
        {
            if (squad.SpawnPad.DistanceTo(DeploymentPoint) < ExtractionSpawnPads.MinPadSeparationMeters * 0.9f)
            {
                playerClear = false;
            }
        }
        var hostileMembersClear = nearestHostileMember
            >= ExtractionSpawnPads.MinPlayerHostileSeparationMeters - 3.0f;
        var everyPadHasFourSafeRivals = ExtractionSpawnPads.Pads.All(playerPad =>
            ExtractionSpawnPads.Pads.Count(candidate =>
                candidate.DistanceTo(playerPad) > 1.0f
                && candidate.DistanceTo(playerPad) >= ExtractionSpawnPads.MinPlayerHostileSeparationMeters)
            >= ExtractionSpawnPads.HostileSquadTargetCount);
        var spawnGeometry = InspectExtractionSpawnGeometry();
        var localDeploymentBoundary = !IsOutsideDeploymentZone(
                DeploymentPoint + Vector3.Right * (DeploymentZoneRadiusMeters - 0.1f))
            && IsOutsideDeploymentZone(
                DeploymentPoint + Vector3.Back * (DeploymentZoneRadiusMeters + 0.1f));
        var sampleHostile = _hostileSquads
            .SelectMany(squad => squad.Members)
            .FirstOrDefault(member => IsInstanceValid(member) && !member.IsDead);
        var protectedBeforeAlarm = IsPlayerProtected();
        var openingTruce = sampleHostile is not null
            && !EnumerateHostileTargetsFor(sampleHostile).Any();
        _missionDirector.RaiseConfirmedAlarm();
        var protectionSurvivesEnemyAlarm = IsPlayerProtected()
            && sampleHostile is not null
            && !EnumerateHostileTargetsFor(sampleHostile).Any();
        _player.GlobalPosition = DeploymentPoint + Vector3.Right * (DeploymentZoneRadiusMeters + 1.0f);
        UpdateDeploymentProtection();
        var physicalExitClearsProtection = !IsPlayerProtected();
        var extractVisible = IsInstanceValid(_extractionMarker) && _extractionMarker.Visible;
        var valid = playerSquadOk && hostileOk && teamTotalOk && sizesOk
            && separated && playerClear && hostileMembersClear && playerOnEdge
            && everyPadHasFourSafeRivals
            && spawnGeometry.Clear
            && protectedBeforeAlarm && openingTruce
            && protectionSurvivesEnemyAlarm && physicalExitClearsProtection
            && localDeploymentBoundary && extractVisible;
        GD.Print($"EXTRACTION_SPAWNS_CHECK valid={valid} player_squad={ActiveSquadCount} hostile_squads={HostileSquadCount} teams={HostileSquadCount + 1} sizes_ok={sizesOk} min_pad_m={minDist:0.0} nearest_hostile_m={nearestHostileMember:0.0} separated={separated} player_clear={playerClear} member_clear={hostileMembersClear} all_pad_choices={everyPadHasFourSafeRivals} spawn_geometry={spawnGeometry.Clear} spawn_positions={spawnGeometry.CheckedPositions} spawn_blockers={spawnGeometry.Blockers} player_edge={playerOnEdge} protected={protectedBeforeAlarm} opening_truce={openingTruce} alarm_protected={protectionSurvivesEnemyAlarm} physical_exit={physicalExitClearsProtection} local_boundary={localDeploymentBoundary} extract_beacon={extractVisible}");
        GD.Print($"EXTRACTION_SPAWNS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateExtractionAi()
    {
        await WaitFrames(8);
        _missionDirector.ExitDeploymentZone();

        // Two rivals from different teams — must fight each other via raw Engage/FireAtNode.
        var teamA = _hostileSquads.FirstOrDefault(s => s.TeamId == 1);
        var teamB = _hostileSquads.FirstOrDefault(s => s.TeamId == 2);
        var npc = _enemies.FirstOrDefault(e => IsInstanceValid(e) && !e.IsRivalSquad && !e.IsDead);
        var rivalA = teamA?.Members.FirstOrDefault(m => IsInstanceValid(m) && !m.IsDead);
        var rivalB = teamB?.Members.FirstOrDefault(m => IsInstanceValid(m) && !m.IsDead);
        if (rivalA is null || rivalB is null || npc is null)
        {
            GD.Print("EXTRACTION_AI_CHECK valid=False reason=missing_actors");
            GetTree().Quit(2);
            return;
        }

        // Park everyone else far away so acquisition is unambiguous.
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy == rivalA || enemy == rivalB || enemy == npc)
            {
                continue;
            }
            enemy.GlobalPosition = new Vector3(200.0f, 0.2f, 200.0f);
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.GlobalPosition = DeploymentPoint + new Vector3(0.0f, 0.0f, 30.0f);
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        // Open flat pad (terminal apron) — no geometry blocking LOS.
        // Cold-start rivals are unarmed; grant guns so combat engagement path is exercised.
        rivalA.GrantFireablePrimaryForDiagnostics();
        rivalB.GrantFireablePrimaryForDiagnostics();
        npc.GrantFireablePrimaryForDiagnostics();
        var arena = new Vector3(8.0f, 0.2f, 18.0f);
        rivalA.GlobalPosition = arena;
        rivalB.GlobalPosition = arena + new Vector3(0.0f, 0.0f, 11.0f);
        rivalA.LookAt(rivalB.GlobalPosition, Vector3.Up);
        rivalB.LookAt(rivalA.GlobalPosition, Vector3.Up);
        rivalA.SetAlerted(rivalB.GlobalPosition);
        rivalB.SetAlerted(rivalA.GlobalPosition);
        var hpA = rivalA.CurrentHealth;
        var hpB = rivalB.CurrentHealth;
        var shotsA0 = rivalA.AttackShotsFired;
        var shotsB0 = rivalB.AttackShotsFired;
        var sawVsEnemyTarget = false;
        for (var i = 0; i < 100; i++)
        {
            rivalA.ArmWeaponForDiagnostics();
            rivalB.ArmWeaponForDiagnostics();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            sawVsEnemyTarget |= rivalA.EngageTargetNode == rivalB || rivalB.EngageTargetNode == rivalA;
        }
        var vsEnemyTarget = sawVsEnemyTarget;
        var vsEnemyShots = rivalA.AttackShotsFired > shotsA0 || rivalB.AttackShotsFired > shotsB0;
        var vsEnemyDamage = rivalA.CurrentHealth < hpA - 0.01f || rivalB.CurrentHealth < hpB - 0.01f
            || rivalA.IsDead || rivalB.IsDead;

        // --- 2) Rival fires on player through FireAtSquad ---
        _missionDirector.ExitDeploymentZone();
        await WaitFrames(6);
        rivalA.ResetTacticalStateForDiagnostics();
        rivalB.ResetTacticalStateForDiagnostics();
        rivalA.GrantFireablePrimaryForDiagnostics();
        if (_player.IsDead)
        {
            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.IsDead = false;
        }
        else
        {
            _player.SetHealthForDiagnostics(_player.MaxHealth);
        }
        rivalA.ProcessMode = ProcessModeEnum.Inherit;
        _player.ProcessMode = ProcessModeEnum.Inherit;
        // Fresh open arena far from cover geometry so Engage does not dig into cover first.
        var fireArena = new Vector3(5.0f, 0.25f, 25.0f);
        rivalA.GlobalPosition = fireArena;
        rivalB.GlobalPosition = new Vector3(180.0f, 0.2f, 180.0f);
        _player.GlobalPosition = fireArena + new Vector3(0.0f, 0.0f, -8.5f);
        _player.RestoreMovementInput();
        rivalA.LookAt(_player.GlobalPosition, Vector3.Up);
        rivalA.SetAlerted(_player.GlobalPosition);
        for (var i = 0; i < 12; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var playerHp = _player.Health;
        var shotsBeforePlayer = rivalA.AttackShotsFired;
        for (var i = 0; i < 130; i++)
        {
            rivalA.ArmWeaponForDiagnostics();
            if (IsInstanceValid(rivalA) && IsInstanceValid(_player) && !_player.IsDead)
            {
                var face = new Vector3(_player.GlobalPosition.X, rivalA.GlobalPosition.Y, _player.GlobalPosition.Z);
                if (rivalA.GlobalPosition.DistanceTo(face) > 0.2f)
                {
                    rivalA.LookAt(face, Vector3.Up);
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var firedAtPlayer = rivalA.AttackShotsFired > shotsBeforePlayer;
        var playerHurtByFire = _player.Health < playerHp - 0.01f || _player.IsDead;
        var engagedPlayer = rivalA.EngageTargetNode == _player || firedAtPlayer;

        // --- 3) Stance/cover from AI tick near cover points ---
        rivalA.ResetTacticalStateForDiagnostics();
        rivalA.GrantFireablePrimaryForDiagnostics();
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        var coverSpot = _coverPoints.OrderBy(p => p.DistanceTo(fireArena)).First();
        rivalA.GlobalPosition = coverSpot + new Vector3(0.8f, 0.15f, 0.8f);
        _player.GlobalPosition = rivalA.GlobalPosition + new Vector3(0.0f, 0.15f, 18.0f);
        rivalA.SetAlerted(_player.GlobalPosition);
        var sawProneOrCover = false;
        for (var i = 0; i < 160; i++)
        {
            rivalA.ArmWeaponForDiagnostics();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (rivalA.IsProne || rivalA.UsesCover)
            {
                sawProneOrCover = true;
                break;
            }
        }

        // --- 4) NPC loot via REAL _PhysicsProcess idle path (no TryBeginLootSearch bypass) ---
        var farLoot = FindNearestLootPoint(new Vector3(-100.0f, 0.2f, 40.0f), 120.0f)
            ?? new Vector3(-55.0f, 0.2f, -28.0f);
        npc.ResetTacticalStateForDiagnostics();
        npc.ClearAlertForDiagnostics();
        npc.GlobalPosition = farLoot + new Vector3(2.0f, 0.15f, 0.0f);
        npc.ProcessMode = ProcessModeEnum.Inherit;
        // Park all operators far outside ContactAcquireRange so EngageTargetNode stays null.
        _player.GlobalPosition = new Vector3(200.0f, 0.2f, 200.0f);
        rivalA.GlobalPosition = new Vector3(210.0f, 0.2f, 210.0f);
        rivalB.GlobalPosition = new Vector3(220.0f, 0.2f, 220.0f);
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.GlobalPosition = new Vector3(230.0f, 0.2f, 230.0f);
            }
        }
        // Advance the no-contact timer past the idle threshold, then ONLY tick PhysicsProcess.
        npc.ForceNoContactTimerForDiagnostics(7.0f);
        var lootStarted = false;
        for (var i = 0; i < 45; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (npc.IsSearchingLoot)
            {
                lootStarted = true;
                break;
            }
        }
        var wasLooting = lootStarted;

        // Mid-loot contact: bring a rival operator into range — must drop loot and engage.
        // Phase-1 duel can kill+unlist rivals from _enemies; revive+relist so acquire sees them.
        var contactRival = rivalA;
        contactRival.ResetTacticalStateForDiagnostics(); // also EnsureEnemyRegisteredForDiagnostics
        EnsureEnemyRegisteredForDiagnostics(npc);
        EnsureEnemyRegisteredForDiagnostics(contactRival);
        contactRival.GlobalPosition = npc.GlobalPosition + new Vector3(0.0f, 0.15f, 8.0f);
        contactRival.ProcessMode = ProcessModeEnum.Inherit;
        contactRival.LookAt(npc.GlobalPosition, Vector3.Up);
        contactRival.SetAlerted(npc.GlobalPosition);
        if (npc.GlobalPosition.DistanceTo(contactRival.GlobalPosition) > 0.2f)
        {
            npc.LookAt(contactRival.GlobalPosition, Vector3.Up);
        }
        var leftLootForCombat = false;
        var npcTargetedOp = false;
        for (var i = 0; i < 120; i++)
        {
            npc.ArmWeaponForDiagnostics();
            contactRival.ArmWeaponForDiagnostics();
            // Keep faces locked so view-cone / mid-loot contact stay valid on open ground.
            if (IsInstanceValid(npc) && IsInstanceValid(contactRival) && !npc.IsDead && !contactRival.IsDead)
            {
                var face = new Vector3(contactRival.GlobalPosition.X, npc.GlobalPosition.Y, contactRival.GlobalPosition.Z);
                if (npc.GlobalPosition.DistanceTo(face) > 0.2f)
                {
                    npc.LookAt(face, Vector3.Up);
                }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (wasLooting && !npc.IsSearchingLoot && npc.Alerted)
            {
                leftLootForCombat = true;
            }
            var engage = npc.EngageTargetNode;
            if (engage is TacticalPlayer || engage is SquadMate || (engage is EnemyOperator eo && eo.IsRivalSquad))
            {
                npcTargetedOp = true;
            }
            if (leftLootForCombat && npcTargetedOp)
            {
                break;
            }
        }
        if (wasLooting && npc.Alerted && !npc.IsSearchingLoot)
        {
            leftLootForCombat = true;
        }

        // --- 5) Pursuit memory, squad contact sharing, and local wall avoidance ---
        var pursuitPair = teamA?.Members
            .Where(member => IsInstanceValid(member))
            .Take(2)
            .ToArray()
            ?? Array.Empty<EnemyOperator>();
        var pursuitPairReady = pursuitPair.Length == 2;
        var pursuitRetained = false;
        var pursuitAdvanced = false;
        var memoryStayedFrozen = false;
        var squadContactShared = false;
        var damageThreatLocked = false;
        var wallFlanked = false;
        if (pursuitPairReady)
        {
            var pursuer = pursuitPair[0];
            var wingman = pursuitPair[1];
            pursuer.ResetTacticalStateForDiagnostics();
            wingman.ResetTacticalStateForDiagnostics();
            pursuer.GrantFireablePrimaryForDiagnostics();
            wingman.GrantFireablePrimaryForDiagnostics();
            EnsureEnemyRegisteredForDiagnostics(pursuer);
            EnsureEnemyRegisteredForDiagnostics(wingman);
            foreach (var enemy in _enemies.ToArray())
            {
                if (!IsInstanceValid(enemy) || enemy == pursuer || enemy == wingman)
                {
                    continue;
                }
                enemy.GlobalPosition = new Vector3(205.0f, 0.2f, 205.0f);
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
            foreach (var mate in _squadMates)
            {
                if (IsInstanceValid(mate))
                {
                    mate.GlobalPosition = new Vector3(220.0f, 0.2f, 220.0f);
                    mate.ProcessMode = ProcessModeEnum.Disabled;
                }
            }

            _player.SetHealthForDiagnostics(_player.MaxHealth);
            _player.IsDead = false;
            _player.ProcessMode = ProcessModeEnum.Inherit;
            _player.RestoreMovementInput();
            var pursuitOrigin = new Vector3(8.0f, 0.25f, 18.0f);
            var contactPoint = pursuitOrigin + new Vector3(0.0f, 0.0f, 10.0f);
            pursuer.GlobalPosition = pursuitOrigin;
            wingman.GlobalPosition = pursuitOrigin + new Vector3(-3.0f, 0.0f, -0.5f);
            _player.GlobalPosition = contactPoint;
            pursuer.ProcessMode = ProcessModeEnum.Inherit;
            wingman.ProcessMode = ProcessModeEnum.Inherit;
            pursuer.SentryMode = true;
            wingman.SentryMode = true;
            pursuer.LookAt(contactPoint, Vector3.Up);
            wingman.LookAt(contactPoint, Vector3.Up);
            var sharedBefore = wingman.SquadContactsReceived;
            pursuer.TakeDamage(0.1f, pursuer.GlobalPosition + Vector3.Up, _player);
            for (var i = 0; i < 18; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            damageThreatLocked = pursuer.EngageTargetNode == _player && pursuer.IsPursuing;
            squadContactShared = wingman.SquadContactsReceived > sharedBefore && wingman.IsPursuing;

            pursuer.SentryMode = false;
            pursuer.Velocity = Vector3.Zero;
            wingman.ProcessMode = ProcessModeEnum.Disabled;
            var pursuitStartPosition = pursuer.GlobalPosition;
            var wallCenter = (pursuitStartPosition + contactPoint) * 0.5f;
            var pursuitWall = new StaticBody3D
            {
                Name = "PursuitTestWall",
                Position = new Vector3(wallCenter.X, 1.8f, wallCenter.Z),
                CollisionLayer = 1,
                CollisionMask = 0
            };
            pursuitWall.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(3.4f, 3.6f, 0.45f) }
            });
            AddChild(pursuitWall);
            _player.GlobalPosition = contactPoint + new Vector3(0.0f, 0.0f, 70.0f);
            _player.ProcessMode = ProcessModeEnum.Disabled;
            await WaitFrames(2);

            var startDistance = pursuitStartPosition.DistanceTo(contactPoint);
            var maxLateralOffset = 0.0f;
            for (var i = 0; i < 210; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                maxLateralOffset = Mathf.Max(
                    maxLateralOffset,
                    Mathf.Abs(pursuer.GlobalPosition.X - pursuitStartPosition.X));
            }
            var endDistance = pursuer.GlobalPosition.DistanceTo(contactPoint);
            pursuitRetained = pursuer.IsPursuing && pursuer.EngageTargetNode == _player;
            pursuitAdvanced = endDistance < startDistance - 1.5f;
            memoryStayedFrozen = pursuer.LastKnownTargetPosition.DistanceTo(contactPoint) < 1.25f
                && pursuer.LastKnownTargetPosition.DistanceTo(_player.GlobalPosition) > 50.0f;
            wallFlanked = maxLateralOffset > 0.8f;
            pursuitWall.QueueFree();
        }

        var valid = vsEnemyTarget && vsEnemyShots && vsEnemyDamage
            && firedAtPlayer && playerHurtByFire && engagedPlayer
            && sawProneOrCover
            && lootStarted && leftLootForCombat && npcTargetedOp
            && pursuitPairReady && pursuitRetained && pursuitAdvanced
            && memoryStayedFrozen && squadContactShared && damageThreatLocked && wallFlanked;
        GD.Print($"EXTRACTION_AI_CHECK valid={valid} vs_enemy_target={vsEnemyTarget} vs_enemy_shots={vsEnemyShots} vs_enemy_dmg={vsEnemyDamage} fire_player={firedAtPlayer} player_hurt={playerHurtByFire} engaged_player={engagedPlayer} prone_or_cover={sawProneOrCover} loot_start={lootStarted} loot_via_physics=True loot_to_combat={leftLootForCombat} npc_target_op={npcTargetedOp} pursuit_pair={pursuitPairReady} pursuit_retained={pursuitRetained} pursuit_advanced={pursuitAdvanced} memory_frozen={memoryStayedFrozen} squad_shared={squadContactShared} damage_threat={damageThreatLocked} wall_flanked={wallFlanked}");
        GD.Print($"EXTRACTION_AI_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateExtractionLoadout()
    {
        await WaitFrames(2);
        EnsureAiSquadFill();
        // Freeze actors so autonomous movement cannot affect the production-loadout snapshot.
        foreach (var squadMate in _squadMates)
        {
            if (IsInstanceValid(squadMate))
            {
                squadMate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy) && enemy.IsRivalSquad)
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _player.ApplyColdStartUnarmed();
        await WaitFrames(2);
        var playerUnarmed = !_player.HasFireablePrimary && _player.Ammo == 0;
        var matesArmed = _squadMates
            .Where(m => IsInstanceValid(m) && !m.IsHumanProxy)
            .All(m => m.HasFireablePrimary);
        var rivalsArmed = true;
        var rivalCount = 0;
        EnemyOperator? sampleRival = null;
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (!IsInstanceValid(member) || member.IsDead)
                {
                    continue;
                }
                rivalCount++;
                sampleRival ??= member;
                if (!member.HasFireablePrimary)
                {
                    rivalsArmed = false;
                }
            }
        }
        var npc = _enemies.FirstOrDefault(e => IsInstanceValid(e) && !e.IsRivalSquad && !e.IsDead);
        var npcArmed = npc is not null && npc.HasFireablePrimary && npc.CarriedWeaponVisible;

        // Real equip path: place a graded weapon pickup and EquipFromLoot / TryEquipWeaponFromLootSource.
        var weaponLoot = new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.M4A1, 1),
            Grade = LootGrade.Rare
        };
        var playerCache = new GradedLootPickup { Position = _player.GlobalPosition + new Vector3(1.2f, 0.0f, 0.4f) };
        playerCache.Configure(weaponLoot, "Loadout Test Rifle", "测试步枪");
        AddChild(playerCache);
        await WaitFrames(2);
        var playerArmedAfterLoot = TryPlayerEquipWeaponFromLootSource(playerCache)
            && _player.HasFireablePrimary
            && _player.Ammo > 0;

        // Rival re-arm via the same production loot removal path.
        var rivalArmedAfterLoot = false;
        if (sampleRival is not null)
        {
            sampleRival.ProcessMode = ProcessModeEnum.Inherit;
            if (sampleRival.HasFireablePrimary)
            {
                sampleRival.ApplyColdStartUnarmed();
            }
            var rivalWeapon = new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.AK74, 0),
                Grade = LootGrade.Uncommon
            };
            var rivalCache = new GradedLootPickup { Position = sampleRival.GlobalPosition + new Vector3(0.8f, 0.0f, 0.5f) };
            rivalCache.Configure(rivalWeapon, "Rival Cache", "敌对物资");
            AddChild(rivalCache);
            await WaitFrames(2);
            rivalArmedAfterLoot = TryEquipWeaponFromLootSource(sampleRival, rivalCache) && sampleRival.HasFireablePrimary;
        }

        // Mate re-arm via the production path. Keep the subject frozen so its
        // autonomous loot hunt cannot consume the cache before this assertion.
        var testMate = _squadMates.FirstOrDefault(m => IsInstanceValid(m) && !m.IsHumanProxy);
        var mateArmedAfterLoot = false;
        if (testMate is not null)
        {
            testMate.ApplyColdStartUnarmed();
            var mateWeapon = new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 0),
                Grade = LootGrade.Common
            };
            var mateCache = new GradedLootPickup { Position = testMate.GlobalPosition + new Vector3(-0.9f, 0.0f, 0.4f) };
            mateCache.Configure(mateWeapon, "Mate Cache", "队友物资");
            AddChild(mateCache);
            await WaitFrames(2);
            mateArmedAfterLoot = TryMateEquipWeaponFromLootSource(testMate, mateCache) && testMate.HasFireablePrimary;
        }

        var valid = playerUnarmed && matesArmed && rivalsArmed && rivalCount >= 4 && npcArmed
            && playerArmedAfterLoot && rivalArmedAfterLoot && mateArmedAfterLoot;
        GD.Print($"EXTRACTION_LOADOUT_CHECK valid={valid} player_unarmed={playerUnarmed} mates_armed={matesArmed} rivals_armed={rivalsArmed} rival_n={rivalCount} npc_armed={npcArmed} player_after_loot={playerArmedAfterLoot} rival_after_loot={rivalArmedAfterLoot} mate_after_loot={mateArmedAfterLoot}");
        GD.Print($"EXTRACTION_LOADOUT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateExtractionLos()
    {
        await WaitFrames(6);
        _missionDirector.ExitDeploymentZone();
        EnsureAiSquadFill();
        var shooter = _enemies.FirstOrDefault(e => IsInstanceValid(e) && !e.IsRivalSquad && !e.IsDead)
            ?? _hostileSquads.FirstOrDefault()?.Members.FirstOrDefault();
        var target = _hostileSquads.SelectMany(s => s.Members)
            .FirstOrDefault(m => IsInstanceValid(m) && !m.IsDead && m != shooter);
        if (shooter is null || target is null)
        {
            GD.Print("EXTRACTION_LOS_CHECK valid=False reason=missing_actors");
            GetTree().Quit(2);
            return;
        }
        shooter.GrantFireablePrimaryForDiagnostics();
        shooter.ResetTacticalStateForDiagnostics();
        target.ResetTacticalStateForDiagnostics();
        EnsureEnemyRegisteredForDiagnostics(shooter);
        EnsureEnemyRegisteredForDiagnostics(target);

        // Elevated open pad far from buildings so only the intentional wall can block.
        var open = new Vector3(0.0f, 0.35f, 55.0f);
        shooter.GlobalPosition = open;
        target.GlobalPosition = open + new Vector3(0.0f, 0.0f, 12.0f);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var muzzleOpen = shooter.GlobalPosition + Vector3.Up * 1.5f;
        var aimOpen = target.GlobalPosition + Vector3.Up * 1.2f;
        var clearOpen = Ballistics.HasClearShot(GetWorld3D(), muzzleOpen, aimOpen, target, shooter.GetRid());
        var hpOpen = target.CurrentHealth;
        if (clearOpen)
        {
            target.TakeDamage(28.0f, aimOpen, shooter);
        }
        var openDamaged = target.CurrentHealth < hpOpen - 0.01f;

        // Rebuild target health and place a solid wall between shooter and target.
        target.ResetTacticalStateForDiagnostics();
        EnsureEnemyRegisteredForDiagnostics(target);
        target.GlobalPosition = open + new Vector3(0.0f, 0.0f, 12.0f);
        shooter.GlobalPosition = open;
        var wallPos = open + new Vector3(0.0f, 1.6f, 6.0f);
        var wall = new StaticBody3D
        {
            Name = "LosTestWall",
            Position = wallPos,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(8.0f, 3.6f, 0.4f) }
        };
        wall.AddChild(shape);
        AddChild(wall);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var hpWall = target.CurrentHealth;
        var muzzleWall = shooter.GlobalPosition + Vector3.Up * 1.5f;
        var aimWall = target.GlobalPosition + Vector3.Up * 1.2f;
        var blocked = !Ballistics.HasClearShot(GetWorld3D(), muzzleWall, aimWall, target, shooter.GetRid());
        // Attempt damage through wall via Ballistics-gated path (must not apply).
        if (Ballistics.HasClearShot(GetWorld3D(), muzzleWall, aimWall, target, shooter.GetRid()))
        {
            target.TakeDamage(40.0f, aimWall, shooter);
        }
        var wallNoDamage = target.CurrentHealth >= hpWall - 0.01f;
        wall.QueueFree();
        var valid = clearOpen && openDamaged && blocked && wallNoDamage;
        GD.Print($"EXTRACTION_LOS_CHECK valid={valid} clear_open={clearOpen} open_dmg={openDamaged} blocked={blocked} wall_no_dmg={wallNoDamage}");
        GD.Print($"EXTRACTION_LOS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateExtractRank()
    {
        await WaitFrames(6);
        EnsureAiSquadFill();
        var extractPresent = IsInstanceValid(_extractionArea) && IsInstanceValid(_extractionMarker) && _extractionMarker.Visible;
        _player.GrantFireablePrimaryForDiagnostics();
        var livingValue = CombatHUD.ComputeBackpackTotalValue(_player);
        var bagBonusIgnored = ScoreLivingSquadValue(livingValue, livingMateBonus: 80, baggedMateCountIgnored: 3);
        var noBag = ScoreLivingSquadValue(livingValue, livingMateBonus: 80, baggedMateCountIgnored: 0);
        var bagsDoNotAdd = bagBonusIgnored == noBag;
        // Convert one mate to body bag — ranking must not count bag loot toward team.
        var mate = _squadMates.FirstOrDefault(m => IsInstanceValid(m) && !m.IsHumanProxy);
        var bagExcluded = true;
        if (mate is not null)
        {
            mate.TakeCombatDamage(999.0f, mate.HitPoint(HitRegion.Torso), this);
            mate.TryReceiveRevive(55.0f);
            mate.TakeCombatDamage(999.0f, mate.HitPoint(HitRegion.Torso), this);
            await WaitFrames(6);
            var ranks = BuildExtractionLootRanking();
            var playerRow = ranks.FirstOrDefault(r => r.Rank >= 1 && (r.Team.Contains("PLAYER") || r.Team.Contains("我方")));
            // Living backpack still counts; bagged mate must not inflate beyond player+one living mate residual.
            bagExcluded = playerRow.Value <= livingValue + 80 + 5;
        }
        var ranksOk = BuildExtractionLootRanking().Count >= 2;
        var zhRank = GameLocalization.Get("extract_rank_title", "zh", "EXTRACTION LOOT RANKING");
        var zhOk = zhRank.Contains("撤离", StringComparison.Ordinal) || zhRank.Contains("排名", StringComparison.Ordinal);
        var valid = extractPresent && bagsDoNotAdd && bagExcluded && ranksOk && zhOk;
        GD.Print($"EXTRACT_RANK_CHECK valid={valid} extract={extractPresent} bags_zero={bagsDoNotAdd} bag_excluded={bagExcluded} ranks={ranksOk} zh={zhOk}");
        GD.Print($"EXTRACT_RANK_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateStairsClimb()
    {
        // Same proven walk path as ValidateResidentialCommunity (no teleport settle).
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (_residentialTowers.Count == 0)
        {
            GD.Print("STAIRS_CHECK valid=False reason=no_towers");
            GD.Print("STAIRS_PASS valid=False");
            GetTree().Quit(2);
            return;
        }
        var firstTower = _residentialTowers[0];
        var firstSpec = ResidentialTowerSpecs[0];
        var firstCoreZ = -Mathf.Min(firstSpec.Footprint.Y * 0.18f, 3.6f);

        // Structural: thin StairStep plates exist; no StairRamp slabs under this tower.
        var stepBodies = 0;
        var stepShapes = 0;
        var rampBodies = 0;
        var rampShapes = 0;
        var consolidatedBodies = 0;
        var landingShapes = 0;
        var compactLandingShapes = 0;
        var centerSpineShapes = 0;
        var centerSpineVisuals = 0;
        var innerGuardShapes = 0;
        var floorSlabShapes = 0;
        var intrudingFloorSlabs = 0;
        var corridorRunnerMeshes = 0;
        var intrudingCorridorRunners = 0;
        var openingWidth = Mathf.Min(ResidentialStairOpeningWidth, firstSpec.Footprint.X - 5.0f);
        var openingHalfWidth = openingWidth * 0.5f;
        var openingNorth = firstCoreZ - ResidentialStairOpeningNorthDepth;
        var openingSouth = firstCoreZ + ResidentialStairOpeningSouthDepth;
        var firstTowerChildren = firstTower.GetChildren();
        using var firstTowerChildrenBacking = firstTowerChildren.AsDisposable();
        foreach (var child in firstTowerChildren)
        {
            var childName = child.Name.ToString();
            if (childName.Contains("ResidentialStairSpine", StringComparison.OrdinalIgnoreCase))
            {
                centerSpineVisuals++;
            }
            if (child is MeshInstance3D runner
                && runner.IsInGroup("residential_corridor_runners")
                && runner.Mesh is BoxMesh runnerBox)
            {
                corridorRunnerMeshes++;
                var runnerMinX = runner.Position.X - runnerBox.Size.X * 0.5f;
                var runnerMaxX = runner.Position.X + runnerBox.Size.X * 0.5f;
                var runnerMinZ = runner.Position.Z - runnerBox.Size.Z * 0.5f;
                var runnerMaxZ = runner.Position.Z + runnerBox.Size.Z * 0.5f;
                const float runnerClearanceTolerance = 0.01f;
                var overlapsOpeningX = runnerMinX < openingHalfWidth - runnerClearanceTolerance
                    && runnerMaxX > -openingHalfWidth + runnerClearanceTolerance;
                var overlapsOpeningZ = runnerMinZ < openingSouth - runnerClearanceTolerance
                    && runnerMaxZ > openingNorth + runnerClearanceTolerance;
                if (overlapsOpeningX && overlapsOpeningZ)
                {
                    intrudingCorridorRunners++;
                }
            }
            if (child is not StaticBody3D body)
            {
                continue;
            }
            var n = body.Name.ToString();
            if (n.Contains("StairStep", StringComparison.OrdinalIgnoreCase)
                || n.Contains("StairLanding", StringComparison.OrdinalIgnoreCase))
            {
                stepBodies++;
            }
            if (n.Contains("ResidentialStairCollision", StringComparison.OrdinalIgnoreCase))
            {
                consolidatedBodies++;
            }
            if (n.Contains("StairRamp", StringComparison.OrdinalIgnoreCase)
                && !n.Contains("StairStep", StringComparison.OrdinalIgnoreCase))
            {
                rampBodies++;
            }
            var bodyChildren = body.GetChildren();
            using var bodyChildrenBacking = bodyChildren.AsDisposable();
            foreach (var bodyChild in bodyChildren)
            {
                if (bodyChild is not CollisionShape3D shape)
                {
                    continue;
                }
                var shapeName = shape.Name.ToString();
                if (shapeName.Contains("StairStep", StringComparison.OrdinalIgnoreCase)
                    || shapeName.Contains("StairLanding", StringComparison.OrdinalIgnoreCase))
                {
                    stepShapes++;
                }
                if (shapeName.Contains("StairLanding", StringComparison.OrdinalIgnoreCase))
                {
                    landingShapes++;
                    compactLandingShapes += shape.Shape is BoxShape3D landingBox
                        && landingBox.Size.X <= ResidentialStairLandingWidth + 0.01f
                        && landingBox.Size.Z <= ResidentialStairLandingDepth + 0.01f
                        ? 1
                        : 0;
                }
                if (shapeName.Contains("ResidentialStairSpine", StringComparison.OrdinalIgnoreCase))
                {
                    centerSpineShapes++;
                }
                if (shapeName.Contains("ResidentialStairInnerGuard", StringComparison.OrdinalIgnoreCase))
                {
                    innerGuardShapes++;
                }
                if (shapeName.Contains("StairRamp", StringComparison.OrdinalIgnoreCase)
                    && !shapeName.Contains("StairStep", StringComparison.OrdinalIgnoreCase))
                {
                    rampShapes++;
                }
                if (body.IsInGroup("residential_floor_slabs") && shape.Shape is BoxShape3D floorSlab)
                {
                    floorSlabShapes++;
                    var center = body.Position + shape.Position;
                    var slabMinX = center.X - floorSlab.Size.X * 0.5f;
                    var slabMaxX = center.X + floorSlab.Size.X * 0.5f;
                    var slabMinZ = center.Z - floorSlab.Size.Z * 0.5f;
                    var slabMaxZ = center.Z + floorSlab.Size.Z * 0.5f;
                    const float clearanceTolerance = 0.01f;
                    var overlapsOpeningX = slabMinX < openingHalfWidth - clearanceTolerance
                        && slabMaxX > -openingHalfWidth + clearanceTolerance;
                    var overlapsOpeningZ = slabMinZ < openingSouth - clearanceTolerance
                        && slabMaxZ > openingNorth + clearanceTolerance;
                    if (overlapsOpeningX && overlapsOpeningZ)
                    {
                        intrudingFloorSlabs++;
                    }
                }
            }
        }
        var steppedCollider = stepShapes >= firstSpec.Floors * 32 || stepBodies >= firstSpec.Floors * 32;
        var consolidated = consolidatedBodies == firstSpec.Floors;
        var rampSlabAbsent = rampBodies == 0 && rampShapes == 0;
        var compactLandings = landingShapes == firstSpec.Floors
            && compactLandingShapes == landingShapes;
        var openCenter = centerSpineShapes == 0
            && centerSpineVisuals == 0
            && innerGuardShapes == firstSpec.Floors * 2;
        var floorSlabClearance = floorSlabShapes == (firstSpec.Floors + 1) * 4
            && intrudingFloorSlabs == 0;
        var corridorRunnerClearance = corridorRunnerMeshes == firstSpec.Floors * 2
            && intrudingCorridorRunners == 0;
        // Hangar must not keep the old rotated ramp name.
        var hangar = _levelRoot.GetNodeOrNull<Node3D>("MaintenanceDistrict");
        var hangarRampGone = hangar is null || hangar.GetNodeOrNull("HangarStair") is null;

        _player.GlobalPosition = firstTower.ToGlobal(new Vector3(
            -1.45f,
            0.25f,
            firstCoreZ + ResidentialStairRun * 0.5f - 0.25f));
        _player.RestoreMovementInput();
        for (var frame = 0; frame < 10; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        // Full ground -> floor-2 climb through the switchback core, waypoint by waypoint.
        var waypoints = new (Vector3 Local, string Label)[]
        {
            (new Vector3(-1.45f, ResidentialFloorHeight * 0.5f + 0.25f, firstCoreZ - ResidentialStairRun * 0.5f - 1.0f), "landing_entry"),
            (new Vector3(1.45f, ResidentialFloorHeight * 0.5f + 0.25f, firstCoreZ - ResidentialStairRun * 0.5f - 1.0f), "landing_turn"),
            (new Vector3(1.45f, ResidentialFloorHeight + 0.25f, firstCoreZ + ResidentialStairRun * 0.5f - 0.3f), "upper_top"),
            (new Vector3(1.45f, ResidentialFloorHeight + 0.25f, firstCoreZ + ResidentialStairRun * 0.5f + 0.75f), "upper_exit"),
            (new Vector3(0.0f, ResidentialFloorHeight + 0.25f, firstCoreZ + ResidentialStairRun * 0.5f + 1.6f), "floor2_corridor")
        };
        var walkStartY = _player.GlobalPosition.Y;
        var reachedIndex = -1;
        Input.ActionPress("move_forward");
        Input.ActionPress("sprint");
        foreach (var (local, label) in waypoints)
        {
            var target = firstTower.ToGlobal(local);
            var reached = false;
            for (var frame = 0; frame < 500; frame++)
            {
                if (frame % 5 == 0)
                {
                    _player.FaceWorldPointForDiagnostics(target);
                }
                if (_player.GlobalPosition.DistanceTo(target) < 0.75f)
                {
                    reached = true;
                    break;
                }
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            if (!reached)
            {
                var localPos = firstTower.ToLocal(_player.GlobalPosition);
                GD.Print($"STAIRS_STALL waypoint={label} pos=({localPos.X:0.00},{localPos.Y:0.00},{localPos.Z:0.00})");
                break;
            }
            reachedIndex++;
        }
        Input.ActionRelease("sprint");
        Input.ActionRelease("move_forward");
        var walkGain = _player.GlobalPosition.Y - walkStartY;
        var walked = reachedIndex >= waypoints.Length - 1 && walkGain > ResidentialFloorHeight - 0.4f;
        var valid = steppedCollider && consolidated && rampSlabAbsent && compactLandings
            && openCenter && floorSlabClearance && corridorRunnerClearance
            && hangarRampGone && walked && _residentialStairFlightCount > 0;
        GD.Print($"STAIRS_CHECK valid={valid} step_bodies={stepBodies} step_shapes={stepShapes} consolidated_bodies={consolidatedBodies} consolidated={consolidated} ramp_bodies={rampBodies} ramp_shapes={rampShapes} stepped={steppedCollider} no_ramp_slab={rampSlabAbsent} compact_landings={compactLandings} landing_shapes={compactLandingShapes}/{landingShapes} open_center={openCenter} spine={centerSpineShapes}/{centerSpineVisuals} inner_guards={innerGuardShapes}/{firstSpec.Floors * 2} slab_clear={floorSlabClearance} slab_intrusions={intrudingFloorSlabs} slab_shapes={floorSlabShapes}/{(firstSpec.Floors + 1) * 4} runner_clear={corridorRunnerClearance} runner_intrusions={intrudingCorridorRunners} runner_meshes={corridorRunnerMeshes}/{firstSpec.Floors * 2} hangar_ok={hangarRampGone} walk_h={walkGain:0.00} reached={reachedIndex + 1}/{waypoints.Length} climbed={walked} flights={_residentialStairFlightCount}");
        GD.Print($"STAIRS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateBackpackTab()
    {
        await WaitFrames(4);
        var originalPackId = _player.EquippedBackpack.DefinitionId;
        var originalPackGrade = _player.EquippedBackpackGrade;
        var swappedPack = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquipmentCatalog.Create("pack_heavy"),
            Grade = LootGrade.Legendary
        });
        var firstPackGradePreserved = _player.EquippedBackpackGrade == LootGrade.Legendary
            && swappedPack is not null
            && swappedPack.Equipment?.DefinitionId == originalPackId
            && swappedPack.Grade == originalPackGrade;
        var returnedPack = swappedPack is null ? null : _player.EquipFromLoot(swappedPack);
        var packGradeRoundTrip = firstPackGradePreserved
            && _player.EquippedBackpack.DefinitionId == originalPackId
            && _player.EquippedBackpackGrade == originalPackGrade
            && returnedPack is not null
            && returnedPack.Equipment?.DefinitionId == "pack_heavy"
            && returnedPack.Grade == LootGrade.Legendary;
        _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquipmentCatalog.Create("pack_heavy"),
            Grade = LootGrade.Rare
        });
        var originalHelmetId = _player.EquippedHelmet.DefinitionId;
        var originalHelmetGrade = _player.EquippedHelmetGrade;
        var swappedHelmet = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquipmentCatalog.Create("helmet_heavy"),
            Grade = LootGrade.Legendary
        });
        var firstHelmetGradePreserved = _player.EquippedHelmetGrade == LootGrade.Legendary
            && swappedHelmet is not null
            && swappedHelmet.Equipment?.DefinitionId == originalHelmetId
            && swappedHelmet.Grade == originalHelmetGrade;
        var returnedHelmet = swappedHelmet is null ? null : _player.EquipFromLoot(swappedHelmet);
        var helmetGradeRoundTrip = firstHelmetGradePreserved
            && _player.EquippedHelmet.DefinitionId == originalHelmetId
            && _player.EquippedHelmetGrade == originalHelmetGrade
            && returnedHelmet is not null
            && returnedHelmet.Equipment?.DefinitionId == "helmet_heavy"
            && returnedHelmet.Grade == LootGrade.Legendary;
        var originalArmorGrade = _player.EquippedBodyArmorGrade;
        var swappedArmor = _player.EquipFromLoot(new LootItem
        {
            Kind = LootItemKind.Equipment,
            Equipment = EquipmentCatalog.Create("armor_heavy"),
            Grade = LootGrade.Legendary
        });
        var firstArmorGradePreserved = _player.EquippedBodyArmorGrade == LootGrade.Legendary
            && swappedArmor?.Grade == originalArmorGrade;
        var returnedArmor = swappedArmor is null ? null : _player.EquipFromLoot(swappedArmor);
        var armorGradeRoundTrip = firstArmorGradePreserved
            && _player.EquippedBodyArmorGrade == originalArmorGrade
            && returnedArmor?.Grade == LootGrade.Legendary;
        while (_player.Backpack.Count < _player.BackpackCapacity)
        {
            var equipmentId = (_player.Backpack.Count % 3) switch
            {
                0 => "helmet_heavy",
                1 => "armor_heavy",
                _ => "armor_patrol"
            };
            _player.TryStoreInBackpack(new LootItem
            {
                Kind = LootItemKind.Equipment,
                Equipment = EquipmentCatalog.Create(equipmentId),
                Grade = LootGrade.Rare
            });
        }
        var weaponsInBackpack = 0;
        var comparableItems = 0;
        foreach (var item in _player.Backpack)
        {
            if (item.Kind == LootItemKind.Weapon)
            {
                weaponsInBackpack++;
            }
            if (item.Kind is LootItemKind.Weapon or LootItemKind.Equipment)
            {
                comparableItems++;
            }
        }
        var tabDown = new InputEventKey { Pressed = true, PhysicalKeycode = Key.Tab };
        Input.ParseInputEvent(tabDown);
        await WaitFrames(6);
        var opened = _hud.IsLootVisible;
        var tabUp = new InputEventKey { Pressed = false, PhysicalKeycode = Key.Tab };
        Input.ParseInputEvent(tabUp);
        var paperDollVisible = _hud.LootPaperDollReady;
        var backpackSlotSeparated = _hud.LootBackpackSlotSeparated;
        var expanded = _hud.LootBackpackPanelExpanded;
        var contentFits = _hud.LootBackpackContentFits;
        var groundDropReady = _hud.LootGroundDropReady;
        var groundDropInvisible = _hud.LootGroundDropInvisible;
        var comparisonCards = _hud.LootComparisonCardCount;
        var comparisonDirections = _hud.LootComparisonHasUpgrade && _hud.LootComparisonHasDowngrade;
        var gradeColorsStable = _hud.LootGradeColorsConsistent;
        var renderedComparisonsComplete = _hud.LootComparisonsFullyRendered;
        var equippedGradeStylesStable = _hud.LootEquippedGradeStylesConsistent;
        var emptyPrimaryGradeHidden = _hud.LootEmptyPrimaryGradeHidden;
        var atCapacity = _player.Backpack.Count == _player.BackpackCapacity;
        var menuCandidate = _player.Backpack.FirstOrDefault();
        var itemMenuActivated = menuCandidate is not null
            && _hud.ActivateLootCardForDiagnostics(menuCandidate.Id, LootDragOrigin.Backpack);
        await WaitFrames(2);
        var dropOnlyMenu = itemMenuActivated
            && _hud.LootActionMenuReady
            && _hud.LootActionMenuVisible
            && !_hud.LootActionMenuCanEquip
            && _hud.LootActionMenuItemId == menuCandidate!.Id;
        var valid = opened && weaponsInBackpack == 0 && paperDollVisible && backpackSlotSeparated
            && expanded && contentFits && groundDropReady && groundDropInvisible && atCapacity
            && dropOnlyMenu
            && comparisonCards == comparableItems
            && comparisonDirections
            && gradeColorsStable
            && renderedComparisonsComplete
            && equippedGradeStylesStable
            && emptyPrimaryGradeHidden
            && helmetGradeRoundTrip
            && armorGradeRoundTrip
            && packGradeRoundTrip;
        GD.Print($"BACKPACK_TAB_CHECK valid={valid} opened={opened} backpack_items={_player.Backpack.Count} capacity={_player.BackpackCapacity} full={atCapacity} weapons={weaponsInBackpack} unarmed={!_player.HasFireablePrimary} paper_doll={paperDollVisible} backpack_isolated={backpackSlotSeparated} expanded={expanded} content_fits={contentFits} ground_drop={groundDropReady} drop_invisible={groundDropInvisible} action_menu={itemMenuActivated}/{dropOnlyMenu} comparisons={comparisonCards}/{comparableItems} directions={comparisonDirections} rendered_all={renderedComparisonsComplete} grade_colors={gradeColorsStable} equipped_grade_styles={equippedGradeStylesStable} empty_primary_grade_hidden={emptyPrimaryGradeHidden} helmet_grade={helmetGradeRoundTrip} armor_grade={armorGradeRoundTrip} pack_grade={packGradeRoundTrip}");
        GD.Print($"BACKPACK_TAB_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateSkyLinks()
    {
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var commonFloorGraph = new List<int>[ResidentialTowerSpecs.Length];
        for (var index = 0; index < commonFloorGraph.Length; index++)
        {
            commonFloorGraph[index] = new List<int>();
        }
        foreach (var candidate in ResidentialSkyLinks)
        {
            if (!Array.Exists(candidate.Floors, value => value == 2))
            {
                continue;
            }
            commonFloorGraph[candidate.From].Add(candidate.To);
            commonFloorGraph[candidate.To].Add(candidate.From);
        }
        var visited = new HashSet<int> { 0 };
        var frontier = new Queue<int>();
        frontier.Enqueue(0);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in commonFloorGraph[current])
            {
                if (visited.Add(neighbor))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }
        var ringConnected = visited.Count == ResidentialTowerSpecs.Length
            && commonFloorGraph.All(neighbors => neighbors.Count >= 2);

        var bridgeSightlineClear = new bool[_residentialSkybridgeCount];
        var clearSightlines = _residentialSkybridgeSightlines.Count == _residentialSkybridgeCount * 2;
        var blockedSightline = -1;
        var blockedCollider = "none";
        for (var sightlineIndex = 0; sightlineIndex < _residentialSkybridgeSightlines.Count; sightlineIndex++)
        {
            var sightline = _residentialSkybridgeSightlines[sightlineIndex];
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    sightline.From,
                    sightline.To,
                    1,
                    out var hit))
            {
                bridgeSightlineClear[sightline.BridgeIndex] = true;
            }
            else if (blockedSightline < 0)
            {
                blockedSightline = sightlineIndex;
                blockedCollider = hit.Collider is Node colliderNode ? colliderNode.Name : "unknown";
            }
        }
        clearSightlines &= bridgeSightlineClear.All(value => value);
        var expectedBridgeCount = ResidentialSkyLinks.Sum(candidate => candidate.Floors.Length);
        var marksmenReady = _enemies.Count(enemy => IsInstanceValid(enemy)
            && enemy.SentryMode
            && enemy.HasFireablePrimary
            && enemy.CarriedWeapon.Platform == WeaponPlatform.M24);
        var architectureReady = _residentialSkybridgeCount == expectedBridgeCount
            && _residentialSkybridgeWindowCount == expectedBridgeCount * 3
            && _residentialSkybridgeFrameCount >= expectedBridgeCount * 4
            && _residentialSniperPosts.Count >= 6
            && marksmenReady == _residentialSkybridgeMarksmanCount
            && marksmenReady >= 6;

        var link = ResidentialSkyLinks[0];
        const int floor = 2;
        var floorY = floor * ResidentialFloorHeight;
        var specA = ResidentialTowerSpecs[link.From];
        var specB = ResidentialTowerSpecs[link.To];
        var towerA = _residentialTowers[link.From];
        var towerB = _residentialTowers[link.To];
        var sideA = ResidentialLinkSide(specA, specB);
        var sideB = ResidentialLinkSide(specB, specA);
        var doorZA = _residentialLinkSlots[link.From][sideA].DoorZ;
        var doorZB = _residentialLinkSlots[link.To][sideB].DoorZ;
        var worldA = towerA.ToGlobal(ResidentialLinkAnchor(specA, sideA, floorY, doorZA));
        var worldB = towerB.ToGlobal(ResidentialLinkAnchor(specB, sideB, floorY, doorZB));
        var direction = worldA.DirectionTo(worldB);
        direction.Y = 0.0f;
        direction = direction.Normalized();
        var walkHeight = Vector3.Up * 0.25f;
        _player.GlobalPosition = worldA - direction * 2.0f + walkHeight;
        var target = worldB + direction * 2.0f + walkHeight;
        _player.FaceWorldPointForDiagnostics(target);
        _player.RestoreMovementInput();
        for (var frame = 0; frame < 10; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        var waypoints = new[]
        {
            worldA + direction * 1.0f + walkHeight,
            (worldA + worldB) * 0.5f + walkHeight,
            worldB - direction * 1.0f + walkHeight,
            target
        };
        Input.ActionPress("move_forward");
        Input.ActionRelease("sprint");
        var reachedWaypoints = 0;
        foreach (var waypoint in waypoints)
        {
            var reachedWaypoint = false;
            for (var frame = 0; frame < 650; frame++)
            {
                // Synthetic actions can be released by another diagnostic cleanup path.
                // Reassert walking so this check measures bridge collision, not input lifetime.
                Input.ActionPress("move_forward");
                _player.FaceWorldPointForDiagnostics(waypoint);
                if (frame > 2
                    && Input.IsActionPressed(GameInputActions.MoveForward)
                    && !_player.HasMovementIntent)
                {
                    _player.RestoreMovementInput();
                }
                if (_player.GlobalPosition.DistanceTo(waypoint) < 1.6f)
                {
                    reachedWaypoint = true;
                    break;
                }
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            if (!reachedWaypoint)
            {
                var colliders = new List<string>();
                for (var collisionIndex = 0; collisionIndex < _player.GetSlideCollisionCount(); collisionIndex++)
                {
                    var collision = _player.GetSlideCollision(collisionIndex);
                    if (collision.GetCollider() is Node colliderNode)
                    {
                        var normal = collision.GetNormal();
                        var position = collision.GetPosition();
                        colliders.Add($"{colliderNode.Name}@n({normal.X:0.0},{normal.Y:0.0},{normal.Z:0.0})p({position.X:0.0},{position.Y:0.0},{position.Z:0.0})");
                    }
                }
                var progress = (_player.GlobalPosition - worldA).Dot(direction);
                var hasTraceHit = PhysicsRaycast.TryHit(
                    GetWorld3D().DirectSpaceState,
                    _player.GlobalPosition + Vector3.Up * 0.75f,
                    waypoint + Vector3.Up * 0.75f,
                    _player.GetRid(),
                    1,
                    out var traceHit);
                var traceCollider = hasTraceHit && traceHit.Collider is Node traceNode
                    ? traceNode.Name.ToString()
                    : "none";
                static string TraceObstacle(
                    PhysicsDirectSpaceState3D space,
                    TacticalPlayer player,
                    Vector3 waypoint,
                    float height)
                {
                    var from = player.GlobalPosition + Vector3.Up * height;
                    var to = new Vector3(waypoint.X, player.GlobalPosition.Y + height, waypoint.Z);
                    if (!PhysicsRaycast.TryHit(
                            space,
                            from,
                            to,
                            player.GetRid(),
                            1,
                            out var hit))
                    {
                        return "none";
                    }
                    var collider = hit.Collider as Node;
                    var position = hit.Position;
                    return $"{collider?.Name ?? "unknown"}@({position.X:0.0},{position.Y:0.0},{position.Z:0.0})";
                }
                var lowTrace = TraceObstacle(GetWorld3D().DirectSpaceState, _player, waypoint, 0.32f);
                var waistTrace = TraceObstacle(GetWorld3D().DirectSpaceState, _player, waypoint, 0.68f);
                var contactBody = _player.GetSlideCollisionCount() > 0
                    ? _player.GetSlideCollision(0).GetCollider() as StaticBody3D
                    : null;
                CollisionShape3D? contactShape = null;
                if (contactBody is not null)
                {
                    var contactChildren = contactBody.GetChildren();
                    using var contactChildrenBacking = contactChildren.AsDisposable();
                    contactShape = contactChildren.OfType<CollisionShape3D>().FirstOrDefault();
                }
                var contactLocal = contactBody is not null ? contactBody.ToLocal(_player.GlobalPosition) : Vector3.Zero;
                var contactSize = contactShape?.Shape is BoxShape3D box ? box.Size : Vector3.Zero;
                GD.Print($"SKYLINK_STALL waypoint={reachedWaypoints} dist={_player.GlobalPosition.DistanceTo(waypoint):0.0} progress={progress:0.0} player=({_player.GlobalPosition.X:0.0},{_player.GlobalPosition.Y:0.0},{_player.GlobalPosition.Z:0.0}) target=({waypoint.X:0.0},{waypoint.Y:0.0},{waypoint.Z:0.0}) trace={traceCollider} low_trace={lowTrace} waist_trace={waistTrace} contact_local=({contactLocal.X:0.0},{contactLocal.Y:0.0},{contactLocal.Z:0.0}) contact_size=({contactSize.X:0.0},{contactSize.Y:0.0},{contactSize.Z:0.0}) health={_player.Health:0.0} dead={_player.IsDead} intent={_player.HasMovementIntent} stamina={_player.Stamina:0.0} velocity=({_player.Velocity.X:0.0},{_player.Velocity.Y:0.0},{_player.Velocity.Z:0.0}) colliders={string.Join(',', colliders)}");
                SaveViewportImage("res://skylink_stall_validation.png");
                break;
            }
            reachedWaypoints++;
            _player.Velocity = Vector3.Zero;
        }
        Input.ActionRelease("sprint");
        Input.ActionRelease("move_forward");
        var arrived = _player.GlobalPosition.DistanceTo(target) < 3.0f && Mathf.Abs(_player.GlobalPosition.Y - (floorY + 0.3f)) < 0.7f;
        var valid = arrived && ringConnected && clearSightlines && architectureReady;
        GD.Print($"SKYLINK_CHECK valid={valid} walk={arrived} ring_connected={ringConnected} towers={visited.Count}/{ResidentialTowerSpecs.Length} bridges={_residentialSkybridgeCount}/{expectedBridgeCount} windows={_residentialSkybridgeWindowCount} frames={_residentialSkybridgeFrameCount} sniper_los={clearSightlines} blocked_line={blockedSightline} blocker={blockedCollider} marksmen={marksmenReady} dist={_player.GlobalPosition.DistanceTo(target):0.0} y={_player.GlobalPosition.Y:0.0}");
        GD.Print($"SKYLINK_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateVehicleDrive()
    {
        await WaitFrames(4);
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        var vehicle = _vehicles[0];
        var forward = -vehicle.GlobalBasis.Z;
        forward.Y = 0.0f;
        forward = forward.Normalized();

        // Synthetic low curb ahead: the truck must step up and drive over it.
        var curb = new StaticBody3D
        {
            Name = "DriveCheckCurb",
            Position = vehicle.GlobalPosition + forward * 14.0f + Vector3.Up * 0.2f,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        curb.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(8.0f, 0.4f, 1.2f) }
        });
        AddChild(curb);

        _player.GlobalPosition = vehicle.GlobalPosition + vehicle.GlobalBasis.X * -2.6f
            + Vector3.Up * 0.3f - vehicle.GlobalBasis.Z * 0.5f;
        await WaitFrames(3);
        var entered = vehicle.TryEnter(_player);
        await WaitFrames(3);

        var forwardStart = vehicle.GlobalPosition;
        Input.ActionPress("move_forward");
        Node3D? firstBlocker = null;
        for (var frame = 0; frame < 300; frame++)
        {
            if (firstBlocker is null && vehicle.GetSlideCollisionCount() > 0
                && vehicle.GetSlideCollision(0).GetCollider() is Node3D blockerNode
                && blockerNode != _player
                && vehicle.GetSlideCollision(0).GetNormal().Y < 0.5f)
            {
                firstBlocker = blockerNode;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward");
        var forwardDistance = vehicle.GlobalPosition.DistanceTo(forwardStart);
        var curbCleared = (vehicle.GlobalPosition - curb.GlobalPosition).Dot(forward) > 0.0f;

        // Isolated reverse check on open ground (the straight -Z run ends in the extraction pad).
        vehicle.GlobalPosition = new Vector3(8.0f, 0.05f, -8.0f);
        vehicle.Rotation = Vector3.Zero;
        await WaitFrames(3);
        var reverseStart = vehicle.GlobalPosition;
        Input.ActionPress("move_backward");
        for (var frame = 0; frame < 120; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_backward");
        var reverseDistance = vehicle.GlobalPosition.DistanceTo(reverseStart);

        var valid = entered && forwardDistance > 18.0f && curbCleared && reverseDistance > 3.0f;
        GD.Print($"VEHICLE_DRIVE_CHECK entered={entered} forward={forwardDistance:0.00} curb_cleared={curbCleared} reverse={reverseDistance:0.00} blocker={(firstBlocker is null ? "none" : firstBlocker.Name)}");
        GD.Print($"VEHICLE_DRIVE_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateExtractionLoot()
    {
        await WaitFrames(4);
        var before = CombatHUD.ComputeBackpackTotalValue(_player);
        var common = new LootItem { Kind = LootItemKind.Ammunition, Quantity = 10, Grade = LootGrade.Common };
        var legendary = new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 2),
            Grade = LootGrade.Legendary
        };
        var commonValue = common.StackValue;
        var legendaryValue = legendary.StackValue;
        var gradeOrderOk = legendaryValue > commonValue && LootGrades.BaseValue(LootGrade.Epic) > LootGrades.BaseValue(LootGrade.Uncommon);
        _player.TryStoreInBackpack(common);
        var mid = CombatHUD.ComputeBackpackTotalValue(_player);
        _player.TryStoreInBackpack(legendary);
        var after = CombatHUD.ComputeBackpackTotalValue(_player);
        var valueRises = after > mid && mid >= before;
        var glowOk = LootGrades.GlowColor(LootGrade.Legendary).R > 0.5f;
        var zhGrade = LootGrades.DisplayName(LootGrade.Epic, "zh");
        var zhBackpack = GameLocalization.Get("backpack_button", "zh", "TAB  BACKPACK");
        var zhOk = zhGrade == "史诗" && zhBackpack.Contains("背包", StringComparison.Ordinal);
        var gradedPickupNodes = GetTree().GetNodesInGroup("graded_loot");
        using var gradedPickupNodesBacking = gradedPickupNodes.AsDisposable();
        var gradedPickups = gradedPickupNodes.Count;
        var buildingLootOk = gradedPickups >= 8;
        var supplyDropGsh18 = CreateAircraftSupplyDropLoot().Any(item =>
            item.Kind == LootItemKind.Weapon && item.Weapon?.Platform == WeaponPlatform.GSh18);
        var residentialGsh18 = false;
        var gsh18Room = new ResidentialRoomId(0, 0, 1, ResidentialRoomZone.North);
        for (uint salt = 0; salt < 4096 && !residentialGsh18; salt++)
        {
            var plan = ResidentialRoomLootRules.Plan(
                gsh18Room,
                ResidentialRoomArchetype.CommunitySecurity,
                salt);
            residentialGsh18 = ResidentialRoomLootRules.Resolve(plan).Items.Any(item =>
                item.Kind == LootItemKind.Weapon && item.Weapon?.Platform == WeaponPlatform.GSh18);
        }
        var valid = gradeOrderOk
            && valueRises
            && glowOk
            && zhOk
            && buildingLootOk
            && supplyDropGsh18
            && residentialGsh18
            && after > 0;
        GD.Print($"EXTRACTION_LOOT_CHECK valid={valid} grade_order={gradeOrderOk} value_before={before} mid={mid} after={after} rises={valueRises} glow={glowOk} zh={zhOk} graded_pickups={gradedPickups} gsh18_drop={supplyDropGsh18} gsh18_residential={residentialGsh18}");
        GD.Print($"EXTRACTION_LOOT_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateGoalPack()
    {
        // Combined short gate used by the goal harness.
        await WaitFrames(6);
        var aircraft = _aircraft ?? _levelRoot.GetNodeOrNull<DestructibleAircraft>("DistantTiltRotor");
        var aircraftOk = false;
        if (aircraft is not null)
        {
            var before = aircraft.AttackSalvosFired;
            aircraftOk = aircraft.TryAttackTarget(_player, ignoreCooldown: true) && aircraft.AttackSalvosFired > before;
            await WaitFrames(3);
            // Prove the real projectile ignores weapon damage and continues flying.
            SpawnAircraftShell(aircraft.GlobalPosition, aircraft.GlobalPosition + Vector3.Down * 6.0f, 30.0f, 11.0f, aircraft);
            await WaitFrames(2);
            var aircraftShellNodes = GetTree().GetNodesInGroup("aircraft_shells");
            using var aircraftShellNodesBacking = aircraftShellNodes.AsDisposable();
            foreach (var node in aircraftShellNodes)
            {
                if (node is AircraftShell shell && IsInstanceValid(shell) && !shell.IsDestroyed)
                {
                    var interrupted = shell.TakeDamage(999.0f, shell.GlobalPosition, _player);
                    aircraftOk = aircraftOk && !interrupted && !shell.InterceptedInAir && !shell.IsDestroyed;
                    break;
                }
            }
        }
        Node strikeSource = aircraft is not null ? aircraft : this;
        ApplyAircraftStrike(_player.GlobalPosition, 4.0f, 12.0f, strikeSource);

        EnsureAiSquadFill();
        var squadOk = ActiveSquadCount == 3 && AiSquadCount == 2;
        var roles = _squadMates.Where(m => IsInstanceValid(m)).Select(m => m.Role).ToHashSet();
        var fillOk = roles.Count == 2 && !roles.Contains(_player.Role);

        var mate = _squadMates.FirstOrDefault(m => IsInstanceValid(m));
        var reviveOk = false;
        if (mate is not null)
        {
            mate.TakeCombatDamage(999.0f, mate.GlobalPosition + Vector3.Up, this);
            var r1 = mate.TryReceiveRevive(40.0f);
            mate.TakeCombatDamage(999.0f, mate.GlobalPosition + Vector3.Up, this);
            var r2 = mate.TryReceiveRevive(40.0f);
            reviveOk = r1 && !r2 && mate.ReviveUsed;
        }

        var densityOk = ComplexBuildingCount >= 5 && ComplexRoomCount >= 12 && ComplexInteriorPropCount >= 40;
        var valid = aircraftOk && squadOk && fillOk && reviveOk && densityOk;
        GD.Print($"GOAL_PACK_CHECK valid={valid} aircraft={aircraftOk} squad3={squadOk} role_fill={fillOk} revive_once={reviveOk} density={densityOk} buildings={ComplexBuildingCount} rooms={ComplexRoomCount} props={ComplexInteriorPropCount}");
        GD.Print($"GOAL_PACK_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureExtractionFrame()
    {
        SetCaptureLanguage("en");
        _player.GrantFireablePrimaryForDiagnostics();
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (IsInstanceValid(member))
                {
                    member.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft.SetPhysicsProcess(false);
        }
        _missionDirector.ExitDeploymentZone();
        while (_objectiveStage < _objectiveTerminals.Count)
        {
            _missionDirector.AdvanceObjective();
        }
        await WaitFrames(4);
        _player.GlobalPosition = ExtractionPoint + new Vector3(-4.5f, 0.12f, 5.0f);
        _player.FaceWorldPointForDiagnostics(ExtractionPoint);
        TryBeginExtractionSequence(_player);
        _extractionAircraft?.ForceBoardingReadyForValidation();
        if (_extractionAircraft is not null)
        {
            _player.AimCameraAtWorldPointForDiagnostics(_extractionAircraft.GlobalPosition + Vector3.Up * 0.25f);
        }
        _extractionRemaining = 5.8f;
        UpdateExtractionHud();
        await WaitFrames(18);
        SaveViewportImage("res://extraction_validation.png");
        GD.Print($"EXTRACTION_CAPTURE position={ExtractionPoint} radius=7 beacon={_extractionMarker.Visible} countdown={_extractionRemaining:0.0} aircraft={_extractionAircraft?.Phase}");
        GetTree().Quit();
    }

    private async void ValidateLargeMapFlow()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var squad in _hostileSquads)
        {
            foreach (var member in squad.Members)
            {
                if (IsInstanceValid(member))
                {
                    member.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft.SetPhysicsProcess(false);
        }
        var districtNames = new[]
        {
            "SouthOverflowYard",
            "NorthernRailYard",
            "MaintenanceDistrict",
            "TankFarmDistrict",
            "SeawallDistrict",
            "SalvageBazaar",
            "TideglassConservatory",
            "TidalObservatory",
            "DrydockRepairCradle",
            "ExtractionSite"
        };
        var districtsPresent = 0;
        foreach (var districtName in districtNames)
        {
            if (_levelRoot.GetNodeOrNull<Node3D>(districtName) is not null)
            {
                districtsPresent++;
            }
        }
        var oceanBackdropPresent = _levelRoot.GetNodeOrNull<MeshInstance3D>("OceanBackdrop") is not null;

        var extractionDistance = DeploymentPoint.DistanceTo(ExtractionPoint);
        var markerInitiallyHidden = false; // beacon landmark always visible (findable extract)
        _missionDirector.ExitDeploymentZone();
        while (_objectiveStage < _objectiveTerminals.Count)
        {
            _missionDirector.AdvanceObjective();
        }
        await WaitFrames(4);
        var extractionUnlocked = _missionPhase == "EXTRACTION" && _extractionMarker.Visible;
        _player.GlobalPosition = new Vector3(
            _extractionArea.GlobalPosition.X,
            0.2f,
            _extractionArea.GlobalPosition.Z);
        for (var i = 0; i < 8; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        var areaStarted = _extractionCountdownActive;
        _skipExtractionCinematicForValidation = true;
        _extractionAircraft?.ForceBoardingReadyForValidation();
        UpdateExtractionSequence(ExtractionCountdownDuration + 0.2f);
        await WaitFrames(1);
        var completed = _missionEnded && _missionPhase == "COMPLETE";
        // Edge player pads → center extract is still a long run; beacon is always visible.
        var valid = districtsPresent == districtNames.Length
            && oceanBackdropPresent
            && extractionDistance > 80.0f
            && _extractionMarker.Visible
            && extractionUnlocked
            && areaStarted
            && completed;
        GD.Print($"LARGE_MAP_CHECK valid={valid} size={MapWidthMeters:0}x{MapDepthMeters:0} districts={districtsPresent}/{districtNames.Length} ocean_backdrop={oceanBackdropPresent} extraction_distance={extractionDistance:0.0} hidden={markerInitiallyHidden} unlocked={extractionUnlocked} area_started={areaStarted} completed={completed}");
        if (!valid)
        {
            GD.PushError("Large map validation failed.");
        }
        GetTree().Quit(valid ? 0 : 2);
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
