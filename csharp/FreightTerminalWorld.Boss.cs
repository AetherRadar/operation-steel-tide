using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly Vector3[] WorldBossPatrolRoute =
    {
        new(108.0f, 0.2f, 24.0f),
        new(44.0f, 0.2f, 38.0f),
        new(8.0f, 0.2f, 26.0f),
        new(-76.0f, 0.2f, 4.0f),
        new(-114.0f, 0.2f, 43.0f),
        new(-98.0f, 0.2f, -54.0f),
        new(-62.0f, 0.2f, -116.0f),
        new(-88.0f, 0.2f, -152.0f),
        new(-18.0f, 0.2f, -166.0f),
        new(2.0f, 0.2f, -64.0f),
        new(77.0f, 0.2f, -151.0f),
        new(102.0f, 0.2f, -114.0f),
        new(82.0f, 0.2f, -69.0f),
        new(116.0f, 0.2f, 8.0f)
    };

    private EnemyOperator? _worldBoss;
    private bool _worldBossDefeated;

    public EnemyOperator? ActiveWorldBoss => IsInstanceValid(_worldBoss) ? _worldBoss : null;
    public bool WorldBossDefeated => _worldBossDefeated;

    private void SpawnWorldBoss()
    {
        var boss = new EnemyOperator
        {
            Name = "TIDE_HUNTER",
            Position = WorldBossPatrolRoute[0],
            NetworkId = _nextEnemyNetworkId++,
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            TeamId = EnemyOperator.WorldBossTeamId,
            DetectionRange = 240.0f
        };
        boss.ConfigureWorldBoss(WorldBossPatrolRoute);
        AddChild(boss);
        boss.Eliminated += OnEnemyEliminated;
        boss.Eliminated += OnWorldBossEliminated;
        _enemies.Add(boss);
        _worldBoss = boss;
        _worldBossDefeated = false;
        _enemiesRemaining = _enemies.Count(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        _hud.SetEnemyCount(_enemiesRemaining);
        _hud.ShowLocalizedMessage(
            "boss_spawned",
            "ROGUE HUNTER ACTIVE  //  HOSTILE TO EVERY FACTION",
            new Color(0.18f, 1.0f, 0.76f));
        UpdateWorldBossHud();
    }

    private void UpdateWorldBossHud()
    {
        var active = IsInstanceValid(_worldBoss) && !_worldBoss!.IsDead;
        if (!active)
        {
            _hud.SetWorldBossStatus(false, 0.0f, EnemyOperator.WorldBossMaxHealth, 1, 0.0f, false);
            _hud.SetMinimapWorldBoss(Vector3.Zero, false);
            return;
        }

        var distance = IsInstanceValid(_player)
            ? _player.GlobalPosition.DistanceTo(_worldBoss!.GlobalPosition)
            : 0.0f;
        _hud.SetWorldBossStatus(
            true,
            _worldBoss!.CurrentHealth,
            _worldBoss.MaxHealth,
            _worldBoss.WorldBossPhase,
            distance,
            _worldBoss.IsWorldBossPulseCharging);
        _hud.SetMinimapWorldBoss(_worldBoss.GlobalPosition, true);
    }

    private void OnWorldBossEliminated(EnemyOperator boss)
    {
        if (!boss.IsWorldBoss)
        {
            return;
        }
        _worldBossDefeated = true;
        UpdateWorldBossHud();
        _hud.ShowLocalizedMessage(
            "boss_defeated",
            "TIDE HUNTER ELIMINATED  //  LEGENDARY CACHE AVAILABLE",
            new Color(1.0f, 0.68f, 0.18f));
    }

    public void NotifyWorldBossPhaseChanged(int phase)
    {
        var message = phase >= 3
            ? "TIDE HUNTER  //  RIPTIDE OVERDRIVE"
            : "TIDE HUNTER  //  TIDAL PULSE ONLINE";
        var key = phase >= 3 ? "boss_phase_riptide" : "boss_phase_surge";
        _hud.ShowLocalizedMessage(key, message, phase >= 3
            ? new Color(1.0f, 0.34f, 0.2f)
            : new Color(0.2f, 1.0f, 0.76f));
    }

    public void TriggerWorldBossPulse(EnemyOperator boss, float radius, float maxDamage)
    {
        if (!IsInstanceValid(boss) || boss.IsDead || !boss.IsWorldBoss)
        {
            return;
        }

        var position = boss.GlobalPosition + Vector3.Up * 0.55f;
        ReportGunshot(position, 92.0f);
        foreach (var enemy in _enemies.ToArray())
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead || enemy == boss || !boss.IsHostileTo(enemy))
            {
                continue;
            }
            var distance = enemy.GlobalPosition.DistanceTo(position);
            if (distance < radius)
            {
                var damage = maxDamage * (1.0f - distance / radius);
                enemy.TakeDamage(damage, enemy.GlobalPosition + Vector3.Up, boss, 0.35f);
            }
        }

        if (!IsPlayerProtected())
        {
            if (IsInstanceValid(_player) && !_player.IsDead)
            {
                var playerDistance = _player.GlobalPosition.DistanceTo(position);
                if (playerDistance < radius)
                {
                    _player.TakeDamage(maxDamage * 0.72f * (1.0f - playerDistance / radius), position, boss);
                }
            }
            DamageSquadFromExplosion(position, radius, maxDamage, boss);
        }
        SpawnWorldBossPulseEffect(position, radius);
    }

    private void SpawnWorldBossPulseEffect(Vector3 position, float radius)
    {
        var root = new Node3D { Name = "WorldBossPulse", Position = position };
        AddChild(root);
        var color = new Color(0.12f, 1.0f, 0.76f, 0.82f);
        for (var index = 0; index < 3; index++)
        {
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.82f, OuterRadius = 0.9f, Rings = 42, RingSegments = 10 },
                MaterialOverride = new StandardMaterial3D
                {
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = color,
                    EmissionEnabled = true,
                    Emission = new Color(color.R, color.G, color.B),
                    EmissionEnergyMultiplier = 4.0f
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Scale = Vector3.One * 0.2f
            };
            root.AddChild(ring);
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(ring, "scale", Vector3.One * radius, 0.72f)
                .SetDelay(index * 0.1f)
                .SetTrans(Tween.TransitionType.Expo)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "transparency", 1.0f, 0.82f).SetDelay(index * 0.1f);
        }
        var audio = new AudioStreamPlayer3D
        {
            Stream = SoundLab.WorldBossPulse(),
            VolumeDb = 1.5f,
            MaxDistance = 125.0f
        };
        root.AddChild(audio);
        audio.Play();
        var cleanup = CreateTween();
        cleanup.TweenInterval(1.8f);
        cleanup.TweenCallback(Callable.From(root.QueueFree));
    }

    private async void ValidateWorldBoss()
    {
        _missionDirector.ExitDeploymentZone();
        await WaitFrames(3);
        var boss = _worldBoss;
        if (!IsInstanceValid(boss))
        {
            GD.Print("BOSS_CHECK valid=False spawned=False");
            GD.Print("BOSS_PASS valid=False");
            GetTree().Quit(2);
            return;
        }

        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        var garrison = _enemies.FirstOrDefault(enemy => enemy != boss && enemy.TeamId == 0 && !enemy.IsDead);
        var rival = _enemies.FirstOrDefault(enemy => enemy.TeamId > 0 && !enemy.IsDead);
        var factionReady = garrison is not null
            && rival is not null
            && boss!.TeamId == EnemyOperator.WorldBossTeamId
            && boss.IsHostileTo(garrison)
            && garrison.IsHostileTo(boss)
            && boss.IsHostileTo(rival)
            && rival.IsHostileTo(boss);
        var targets = EnumerateHostileTargetsFor(boss!).ToArray();
        var allFactionTargets = targets.Contains(_player)
            && garrison is not null && targets.Contains(garrison)
            && rival is not null && targets.Contains(rival)
            && _squadMates.Any(mate => targets.Contains(mate));

        var weapon = boss!.CarriedWeapon;
        var hasSevenPowerOptic = weapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var optic)
            && optic == "optic_7x";
        var arsenalReady = weapon.Platform == WeaponPlatform.AXMC
            && WeaponCatalog.Weapon(weapon.Platform).Caliber == AmmoCaliber.Magnum338
            && hasSevenPowerOptic;
        var routeReady = boss.WorldBossPatrolRouteCount >= 12
            && boss.WorldBossPatrolSpan.X >= 220.0f
            && boss.WorldBossPatrolSpan.Y >= 185.0f;
        var routeIndex = boss.WorldBossPatrolRouteIndex;
        var routeTarget = boss.WorldBossPatrolTarget;
        boss.AdvanceWorldBossPatrolForDiagnostics();
        var patrolAdvances = boss.WorldBossPatrolRouteIndex != routeIndex
            && boss.WorldBossPatrolTarget.DistanceTo(routeTarget) > 10.0f;

        boss.SetWorldBossHealthRatioForDiagnostics(0.6f);
        var surgePhase = boss.WorldBossPhase == 2;
        boss.SetWorldBossHealthRatioForDiagnostics(0.25f);
        var riptidePhase = boss.WorldBossPhase == 3;
        UpdateWorldBossHud();
        var hudReady = _hud.WorldBossHudVisible
            && _hud.WorldBossHudPhase == 3
            && _hud.WorldBossHudHealthRatio > 0.2f
            && _hud.WorldBossHudHealthRatio < 0.3f
            && _hud.MinimapWorldBossVisible;

        var pulseDamagedFaction = false;
        if (garrison is not null)
        {
            garrison.ResetTacticalStateForDiagnostics();
            garrison.ProcessMode = ProcessModeEnum.Disabled;
            garrison.GlobalPosition = boss.GlobalPosition + new Vector3(3.0f, 0.0f, 0.0f);
            var healthBefore = garrison.CurrentHealth;
            boss.ForceWorldBossPulseForDiagnostics();
            pulseDamagedFaction = garrison.CurrentHealth < healthBefore && boss.WorldBossPulseCount > 0;
        }

        var rewardsReady = boss.Loot.Any(item => item.Weapon?.Platform == WeaponPlatform.AXMC && item.Grade == LootGrade.Legendary)
            && boss.Loot.Any(item => item.Kind == LootItemKind.Attachment && item.AttachmentId == "optic_7x")
            && boss.Loot.Any(item => item.Kind == LootItemKind.Ammunition && item.AmmoCaliber == AmmoCaliber.Magnum338 && item.Quantity >= 30)
            && boss.Loot.Any(item => item.Kind == LootItemKind.KnifeSkin && item.KnifeSkinId == "knife_tidehunter")
            && boss.Loot.Any(item => item.Kind == LootItemKind.Valuable && item.ValuableKind == ValuableItemKind.TideHunterTransponder)
            && LootItem.TotalValue(boss.Loot) >= 10000;

        boss.TakeDamage(5000.0f, boss.GlobalPosition + Vector3.Up, _player, 1.0f);
        await WaitFrames(2);
        UpdateWorldBossHud();
        var deathReady = boss.IsDead
            && boss.IsSearchable
            && _worldBossDefeated
            && _lootSources.Contains(boss)
            && !_hud.WorldBossHudVisible
            && !_hud.MinimapWorldBossVisible;
        var valid = factionReady
            && allFactionTargets
            && arsenalReady
            && routeReady
            && patrolAdvances
            && surgePhase
            && riptidePhase
            && hudReady
            && pulseDamagedFaction
            && rewardsReady
            && deathReady;
        GD.Print($"BOSS_CHECK valid={valid} faction={factionReady} all_targets={allFactionTargets} team={boss.TeamId} health={boss.MaxHealth:0} weapon={weapon.Platform} optic_7x={hasSevenPowerOptic} caliber={WeaponCatalog.Weapon(weapon.Platform).Caliber} route={boss.WorldBossPatrolRouteCount} span=({boss.WorldBossPatrolSpan.X:0},{boss.WorldBossPatrolSpan.Y:0}) patrol={patrolAdvances} surge={surgePhase} riptide={riptidePhase} hud={hudReady} pulse={pulseDamagedFaction} rewards={rewardsReady} reward_value={LootItem.TotalValue(boss.Loot)} corpse={deathReady}");
        GD.Print($"BOSS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureWorldBoss()
    {
        if (!IsInstanceValid(_worldBoss))
        {
            GD.Print("BOSS_CAPTURE valid=False");
            GetTree().Quit(2);
            return;
        }
        foreach (var enemy in _enemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.Visible = false;
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _player.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _worldBoss!.GlobalPosition = new Vector3(-24.0f, 0.2f, -58.0f);
        _worldBoss!.SetWorldBossHealthRatioForDiagnostics(0.25f);
        UpdateWorldBossHud();

        var camera = new Camera3D { Name = "WorldBossCaptureCamera", Fov = 52.0f, Far = 420.0f };
        AddChild(camera);
        camera.GlobalPosition = _worldBoss.GlobalPosition + new Vector3(4.6f, 2.65f, 5.8f);
        camera.LookAt(_worldBoss.GlobalPosition + Vector3.Up * 1.2f, Vector3.Up);
        var faceCamera = camera.GlobalPosition;
        faceCamera.Y = _worldBoss.GlobalPosition.Y;
        _worldBoss.LookAt(faceCamera, Vector3.Up);
        camera.MakeCurrent();
        var captureLight = new OmniLight3D
        {
            LightColor = new Color(0.58f, 0.9f, 0.84f),
            LightEnergy = 5.0f,
            OmniRange = 15.0f,
            ShadowEnabled = false
        };
        camera.AddChild(captureLight);
        await WaitFrames(12);
        SaveViewportImage("res://world_boss_validation.png");
        GD.Print($"BOSS_CAPTURE valid=True phase={_worldBoss.WorldBossPhase} health={_worldBoss.CurrentHealth:0}/{_worldBoss.MaxHealth:0} route={_worldBoss.WorldBossPatrolRouteCount} path=world_boss_validation.png");
        GetTree().Quit();
    }
}
