using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionPlantDuration = 3.4f;
    private const float DemolitionFuseDuration = 32.0f;
    private const float DemolitionDefuseDuration = 6.5f;
    private const float DemolitionRoundDuration = 105.0f;
    private const float DemolitionIntermissionDuration = 4.5f;
    private const float DemolitionStrategyRefreshDuration = 1.5f;

    private readonly List<Node3D> _demolitionSites = new();
    private readonly List<EnemyOperator> _demolitionDefenders = new();
    private readonly Dictionary<EnemyOperator, DemolitionAssignment> _demolitionDefenderAssignments = new();
    private readonly Dictionary<SquadMate, string> _demolitionSquadAssignmentTargets = new();
    private readonly DemolitionMatchState _demolitionMatch = new();
    private readonly DemolitionStrategyPlanner _demolitionStrategyPlanner = new();
    private DemolitionArenaRuntime? _demolitionArena;
    private Node3D? _demolitionDevice;
    private EnemyOperator? _demolitionDefuser;
    private Vector3[] _demolitionDefuseRoute = System.Array.Empty<Vector3>();
    private int _demolitionDefuseRouteIndex;
    private bool _demolitionMode;
    private bool _demolitionRoundActive;
    private bool _demolitionDevicePlanted;
    private int _demolitionActiveSite = -1;
    private float _demolitionPlantProgress;
    private float _demolitionRemaining = DemolitionFuseDuration;
    private float _demolitionDefuseProgress;
    private float _demolitionPulse;
    private float _demolitionIntermissionRemaining;
    private float _demolitionStrategyRemaining;
    private OperatorRole _demolitionPlayerRole = OperatorRole.Assault;
    private DeploymentLoadout _demolitionPlayerLoadout = BuildDemolitionLoadout();
    private DemolitionStrategyPlan? _demolitionAttackerPlan;
    private DemolitionStrategyPlan? _demolitionDefenderPlan;

    public bool IsDemolitionMode => _demolitionMode;
    public bool IsDemolitionRoundActive => _demolitionRoundActive;
    public bool IsDemolitionDevicePlanted => _demolitionDevicePlanted;
    public int DemolitionSiteCount => _demolitionSites.Count;
    public int DemolitionDefenderCount => _demolitionDefenders.Count(defender => IsInstanceValid(defender) && !defender.IsDead);
    public float DemolitionSecondsRemaining => _demolitionRemaining;
    public float DemolitionDefuseProgress => _demolitionDefuseProgress;
    public int DemolitionRoundNumber => _demolitionMatch.CurrentRound;
    public int DemolitionAttackerScore => _demolitionMatch.AttackerScore;
    public int DemolitionDefenderScore => _demolitionMatch.DefenderScore;
    public bool IsDemolitionOvertime => _demolitionMatch.IsOvertime;
    public bool IsDemolitionMatchComplete => _demolitionMatch.IsComplete;
    public int DemolitionStrategyAssignmentCount
        => (_demolitionAttackerPlan?.Assignments.Count ?? 0)
        + (_demolitionDefenderPlan?.Assignments.Count ?? 0);
    public bool IsDemolitionArenaActive => _demolitionArena?.Active == true;
    public int DemolitionArenaCollisionBodyCount => _demolitionArena?.CollisionBodyCount ?? 0;
    public int DemolitionArenaVisualPartCount => _demolitionArena?.VisualPartCount ?? 0;

    private void EnsureDemolitionArenaBuilt()
    {
        if (_demolitionArena is not null)
        {
            return;
        }
        var builder = new DemolitionArenaBuilder(Mat, GroundMaterial);
        _demolitionArena = builder.Build(this, new DemolitionArenaLayout());
        _demolitionSites.Clear();
        _demolitionSites.AddRange(_demolitionArena.Sites);
    }

    private void OnDemolitionDeploymentRequested(
        int role,
        int primaryPlatform,
        int buildTier,
        int sidearmPlatform)
    {
        if (_squadDeployed || _missionEnded)
        {
            return;
        }

        _demolitionPlayerRole = (OperatorRole)role;
        var primary = (WeaponPlatform)primaryPlatform;
        if (primary is not (WeaponPlatform.M4A1 or WeaponPlatform.AK74 or WeaponPlatform.MP5A5 or WeaponPlatform.ScarL))
        {
            primary = WeaponPlatform.M4A1;
        }
        var sidearm = (WeaponPlatform)sidearmPlatform;
        if (sidearm is not (WeaponPlatform.P226 or WeaponPlatform.M1911))
        {
            sidearm = WeaponPlatform.P226;
        }
        _demolitionPlayerLoadout = BuildDemolitionLoadout(primary, Mathf.Clamp(buildTier, 0, 2), sidearm);
        PrepareDemolitionBattlefield();
        var layout = DemolitionLayout();
        _player.GlobalPosition = layout.AttackSpawn;
        _player.Rotation = Vector3.Zero;
        DeploySquad((OperatorRole)role, SquadSessionMode.Local, "127.0.0.1");
        StartDemolitionRound();
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _missionPhase = "DEMOLITION";
        _hud.ShowLocalizedMessage(
            "demolition_deployed",
            "DEMOLITION TEAM DEPLOYED  //  12 ROUND MATCH  //  PLANT AT A OR B",
            new Color(1.0f, 0.58f, 0.2f));
    }

    private static DeploymentLoadout BuildDemolitionLoadout(
        WeaponPlatform primary = WeaponPlatform.M4A1,
        int buildTier = 1,
        WeaponPlatform sidearm = WeaponPlatform.P226)
    {
        var weaponId = primary switch
        {
            WeaponPlatform.AK74 => "ak74",
            WeaponPlatform.MP5A5 => "mp5a5",
            WeaponPlatform.ScarL => "scarl",
            _ => "m4a1"
        };
        var reserve = WeaponCatalog.Weapon(primary).Caliber == AmmoCaliber.Smg ? 150 : 120;
        return new DeploymentLoadout(
            new DeploymentLoadoutSelection(weaponId, "standard", LootGrade.Common, reserve),
            WeaponCatalog.Build(primary, buildTier),
            "helmet_patrol",
            "armor_carrier",
            "pack_assault",
            LootGrade.Common,
            reserve,
            0,
            buildTier,
            WeaponCatalog.Build(sidearm, 0),
            60);
    }

    private void PrepareDemolitionBattlefield()
    {
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            throw new System.InvalidOperationException("Demolition arena was not built before deployment.");
        }
        _demolitionMode = true;
        _demolitionRoundActive = false;
        _demolitionMatch.Reset();
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
        _demolitionPlantProgress = 0.0f;
        _demolitionRemaining = DemolitionRoundDuration;
        _demolitionDefuseProgress = 0.0f;
        _demolitionDefuser = null;
        _demolitionIntermissionRemaining = 0.0f;
        _demolitionStrategyRemaining = 0.0f;
        _demolitionAttackerPlan = null;
        _demolitionDefenderPlan = null;
        _demolitionDefenderAssignments.Clear();
        _demolitionSquadAssignmentTargets.Clear();
        _reinforcementPending = false;
        _reinforcementCountdown = 0.0f;
        _levelRoot.Visible = false;
        _levelRoot.ProcessMode = ProcessModeEnum.Disabled;
        _demolitionArena.SetActive(true);
        ConfigureDemolitionMinimap();
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = false;
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.ProcessMode = ProcessModeEnum.Disabled;
            _aircraft.SetPhysicsProcess(false);
            _aircraft.Visible = false;
        }
        foreach (var enemy in _enemies.ToArray())
        {
            if (!IsInstanceValid(enemy))
            {
                continue;
            }
            enemy.ProcessMode = ProcessModeEnum.Disabled;
            enemy.Visible = false;
        }
        _enemies.Clear();
        _hostileSquads.Clear();
        if (IsInstanceValid(_worldBoss))
        {
            _worldBoss!.ProcessMode = ProcessModeEnum.Disabled;
            _worldBoss.Visible = false;
        }
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.ProcessMode = ProcessModeEnum.Disabled;
                civilian.Visible = false;
            }
        }
        ClearDemolitionDefenders();
    }

    private void SpawnDemolitionDefenders()
    {
        ClearDemolitionDefenders();
        var spawns = DemolitionLayout().DefenderSpawns;
        for (var index = 0; index < spawns.Count; index++)
        {
            var weapon = index % 3 == 0
                ? WeaponCatalog.Build(WeaponPlatform.MP5A5, 1)
                : WeaponCatalog.Build(WeaponPlatform.M4A1, 1);
            var defender = SpawnEnemy(
                spawns[index],
                alerted: false,
                teamId: 0,
                initialWeapon: weapon,
                sentryMode: true,
                detectionRange: 52.0f);
            defender.Name = $"DemolitionDefender_{index + 1:00}";
            defender.LookAt(DemolitionLayout().Midpoint, Vector3.Up);
            _demolitionDefenders.Add(defender);
        }
        _enemiesRemaining = _demolitionDefenders.Count;
        _hud.SetEnemyCount(_enemiesRemaining);
    }

    private void ClearDemolitionDefenders()
    {
        foreach (var defender in _demolitionDefenders)
        {
            if (!IsInstanceValid(defender))
            {
                continue;
            }
            defender.ProcessMode = ProcessModeEnum.Disabled;
            _enemies.Remove(defender);
            defender.QueueFree();
        }
        _demolitionDefenders.Clear();
        _demolitionDefenderAssignments.Clear();
        _demolitionDefuser = null;
        _demolitionDefuseRoute = System.Array.Empty<Vector3>();
        _demolitionDefuseRouteIndex = 0;
    }

    private void StartDemolitionRound()
    {
        if (!_demolitionMode || _demolitionMatch.IsComplete)
        {
            return;
        }

        ClearDemolitionDevice();
        ResetDemolitionSquad();
        SpawnDemolitionDefenders();
        _demolitionRoundActive = true;
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
        _demolitionPlantProgress = 0.0f;
        _demolitionDefuseProgress = 0.0f;
        _demolitionRemaining = DemolitionRoundDuration;
        _demolitionPulse = 0.0f;
        _demolitionIntermissionRemaining = 0.0f;
        _demolitionStrategyRemaining = 0.0f;
        _missionPhase = "DEMOLITION";
        RefreshDemolitionStrategies(true);
        UpdateDemolitionRoundHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void ResetDemolitionSquad()
    {
        foreach (var source in _lootSources.OfType<SquadBodyBag>().ToArray())
        {
            _lootSources.Remove(source);
            if (IsInstanceValid(source))
            {
                source.QueueFree();
            }
        }
        foreach (var orphan in GetTree().GetNodesInGroup("player_squad_ai").OfType<SquadMate>().ToArray())
        {
            if (!_squadMates.Contains(orphan) && IsInstanceValid(orphan))
            {
                orphan.QueueFree();
            }
        }

        var layout = DemolitionLayout();
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        _localPlayerDownedTimer = 0.0f;
        _allDownTimer = 0.0f;
        ClearLeaderReviveAi();
        RestoreLocalPlayerView();
        _player.ResetForDemolitionRound(layout.AttackSpawn, _demolitionPlayerRole, _demolitionPlayerLoadout);
        EnsureAiSquadFill();
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            var offset = mate.SquadSlot == 1
                ? new Vector3(-2.6f, 0.0f, 3.2f)
                : new Vector3(2.6f, 0.0f, 3.2f);
            mate.ResetForDemolitionRound(layout.AttackSpawn + offset);
        }
        ResetSquadLeaderTrail(_player.GlobalPosition);
        _hud.HideDownedState();
        RefreshSquadHud();
    }

    private void ClearDemolitionDevice()
    {
        if (IsInstanceValid(_demolitionDevice))
        {
            _demolitionDevice!.QueueFree();
        }
        _demolitionDevice = null;
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
    }

    private void UpdateDemolitionRoundHud()
    {
        var overtime = _demolitionMatch.IsOvertime
            ? GameLocalization.IsChinese(_languageSetting) ? "  //  加时" : "  //  OVERTIME"
            : string.Empty;
        var score = GameLocalization.IsChinese(_languageSetting)
            ? $"第 {_demolitionMatch.CurrentRound} 局  //  进攻 {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} 防守{overtime}"
            : $"ROUND {_demolitionMatch.CurrentRound}  //  ATTACK {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} DEFEND{overtime}";
        _hud.SetMissionPhase(score, _demolitionRemaining, false);
        if (!_demolitionDevicePlanted)
        {
            _hud.SetObjective(GameLocalization.Format(
                "demolition_round_objective",
                _languageSetting,
                "{0}  //  CHOOSE SITE A OR B  //  HOLD F TO PLANT",
                score));
        }
    }

    private void UpdateDemolitionIntermission(float delta)
    {
        if (_demolitionIntermissionRemaining <= 0.0f)
        {
            return;
        }
        _demolitionIntermissionRemaining = Mathf.Max(0.0f, _demolitionIntermissionRemaining - delta);
        var label = GameLocalization.IsChinese(_languageSetting)
            ? $"下一局  //  {_demolitionIntermissionRemaining:0.0}s  //  进攻 {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} 防守"
            : $"NEXT ROUND  //  {_demolitionIntermissionRemaining:0.0}s  //  ATTACK {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} DEFEND";
        _hud.SetMissionPhase(label, _demolitionIntermissionRemaining, false);
        _hud.SetObjective(label);
        if (_demolitionIntermissionRemaining <= 0.0f)
        {
            StartDemolitionRound();
        }
    }

    private void UpdateDemolitionInteraction(float delta)
    {
        if (!_demolitionRoundActive || _missionEnded)
        {
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }
        if (_demolitionDevicePlanted)
        {
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }

        var nearestIndex = -1;
        var nearestDistance = 3.25f;
        var layout = DemolitionLayout();
        for (var index = 0; index < layout.SitePositions.Count; index++)
        {
            var sitePosition = layout.SitePositions[index];
            var distance = HorizontalDistance(_player.GlobalPosition, sitePosition);
            if (distance < nearestDistance && Mathf.Abs(_player.GlobalPosition.Y - sitePosition.Y) < 2.8f)
            {
                nearestDistance = distance;
                nearestIndex = index;
            }
        }
        if (nearestIndex < 0)
        {
            _demolitionPlantProgress = Mathf.Max(0.0f, _demolitionPlantProgress - delta * 1.4f);
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }

        var planting = Input.IsActionPressed("interact") && !_interactReleaseRequired;
        _demolitionPlantProgress = planting
            ? Mathf.Min(1.0f, _demolitionPlantProgress + delta / DemolitionPlantDuration)
            : Mathf.Max(0.0f, _demolitionPlantProgress - delta * 1.2f);
        var siteName = ((char)('A' + nearestIndex)).ToString();
        var action = GameLocalization.Format(
            "demolition_interaction_plant",
            _languageSetting,
            "PLANT DEMOLITION DEVICE  //  SITE {0}",
            siteName);
        _hud.SetInteraction(action, _demolitionPlantProgress, true);
        if (_demolitionPlantProgress >= 1.0f)
        {
            PlantDemolitionDevice(nearestIndex);
        }
    }

    private void PlantDemolitionDevice(int siteIndex)
    {
        var layout = DemolitionLayout();
        if (_demolitionDevicePlanted || siteIndex < 0 || siteIndex >= layout.SitePositions.Count)
        {
            return;
        }
        _demolitionDevicePlanted = true;
        _demolitionActiveSite = siteIndex;
        _demolitionRemaining = DemolitionFuseDuration;
        _demolitionPlantProgress = 0.0f;
        _demolitionDefuseProgress = 0.0f;
        var orange = Mat(
            "demolition_device_orange",
            new Color(1.0f, 0.24f, 0.04f),
            0.16f,
            0.24f,
            new Color(1.0f, 0.05f, 0.01f));
        var dark = Mat("demolition_device_dark", new Color(0.018f, 0.025f, 0.023f), 0.72f, 0.3f);
        _demolitionDevice = new Node3D
        {
            Name = "PlantedDemolitionDevice",
            Position = layout.SitePositions[siteIndex] + new Vector3(0, 0.34f, 0)
        };
        _demolitionArena!.Root.AddChild(_demolitionDevice);
        OfficeBox(_demolitionDevice, "DeviceCase", Vector3.Zero, new Vector3(0.9f, 0.48f, 0.62f), dark);
        OfficeBox(_demolitionDevice, "DeviceScreen", new Vector3(0, 0.1f, -0.33f), new Vector3(0.52f, 0.2f, 0.035f), orange);
        _demolitionDevice.AddChild(new OmniLight3D
        {
            Name = "DeviceBeacon",
            Position = new Vector3(0, 0.45f, 0),
            LightColor = new Color(1.0f, 0.12f, 0.02f),
            LightEnergy = 5.5f,
            OmniRange = 9.0f,
            ShadowEnabled = false
        });
        foreach (var defender in _demolitionDefenders)
        {
            if (IsInstanceValid(defender) && !defender.IsDead)
            {
                defender.SentryMode = false;
                defender.SetAlerted(_player.GlobalPosition);
            }
        }
        RefreshDemolitionStrategies(true);
        var siteName = ((char)('A' + siteIndex)).ToString();
        _hud.ShowRadioMessage(
            GameLocalization.Format(
                "demolition_planted",
                _languageSetting,
                "DEVICE PLANTED AT SITE {0}  //  DEFEND FOR {1:0} SECONDS",
                siteName,
                DemolitionFuseDuration),
            new Color(1.0f, 0.5f, 0.16f));
    }

    private void UpdateDemolitionRound(float delta)
    {
        if (!_demolitionMode || _missionEnded)
        {
            return;
        }
        if (!_demolitionRoundActive)
        {
            UpdateDemolitionIntermission(delta);
            return;
        }

        var defendersAlive = DemolitionDefenderCount;
        if (defendersAlive == 0)
        {
            FinishDemolitionRound(
                true,
                GameLocalization.Get(
                    "demolition_defenders_eliminated",
                    _languageSetting,
                    "DEFENDING FORCE ELIMINATED"));
            return;
        }
        _demolitionStrategyRemaining -= delta;
        if (_demolitionStrategyRemaining <= 0.0f)
        {
            RefreshDemolitionStrategies(false);
        }
        if (!_demolitionDevicePlanted)
        {
            _demolitionRemaining = Mathf.Max(0.0f, _demolitionRemaining - delta);
            UpdateDemolitionRoundHud();
            if (_demolitionRemaining <= 0.0f)
            {
                FinishDemolitionRound(
                    false,
                    GameLocalization.Get(
                        "demolition_round_timeout",
                        _languageSetting,
                        "ATTACK WINDOW EXPIRED"));
            }
            return;
        }

        _demolitionPulse += delta;
        _demolitionRemaining = Mathf.Max(0.0f, _demolitionRemaining - delta);
        if (IsInstanceValid(_demolitionDevice))
        {
            var pulse = 1.0f + Mathf.Sin(_demolitionPulse * (5.0f + (1.0f - _demolitionRemaining / DemolitionFuseDuration) * 8.0f)) * 0.08f;
            _demolitionDevice!.Scale = Vector3.One * pulse;
        }
        SelectDemolitionDefuser();
        var siteName = ((char)('A' + _demolitionActiveSite)).ToString();
        var defuse = _demolitionDefuseProgress > 0.01f
            ? GameLocalization.Format(
                "demolition_defuse_suffix",
                _languageSetting,
                "  //  DEFUSE {0:00}%",
                Mathf.RoundToInt(_demolitionDefuseProgress * 100.0f))
            : string.Empty;
        _hud.SetObjective(GameLocalization.Format(
            "demolition_defend",
            _languageSetting,
            "DEFEND SITE {0}  //  {1:00.0}s{2}",
            siteName,
            _demolitionRemaining,
            defuse));
        var phase = GameLocalization.IsChinese(_languageSetting)
            ? $"第 {_demolitionMatch.CurrentRound} 局  //  进攻 {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} 防守"
            : $"ROUND {_demolitionMatch.CurrentRound}  //  ATTACK {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} DEFEND";
        _hud.SetMissionPhase(phase, _demolitionRemaining, false);
        if (_demolitionRemaining <= 0.0f)
        {
            FinishDemolitionRound(
                true,
                GameLocalization.Format(
                    "demolition_site_destroyed",
                    _languageSetting,
                    "SITE {0} DESTROYED",
                    siteName));
        }
    }

    private DemolitionArenaLayout DemolitionLayout()
        => _demolitionArena?.Layout
            ?? throw new System.InvalidOperationException("Demolition arena is unavailable.");

    private bool IsDemolitionSitePlacementClear(Vector3 position)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = new Vector3(4.2f, 1.55f, 4.2f) },
            Transform = new Transform3D(Basis.Identity, position + Vector3.Up * 1.05f),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 64);
        foreach (var hit in hits)
        {
            if (hit["collider"].AsGodotObject() is StaticBody3D body
                && _demolitionArena?.Owns(body) != true)
            {
                return false;
            }
        }
        return true;
    }

    private void FinishDemolitionRound(bool victory, string reason)
    {
        if (_missionEnded || !_demolitionRoundActive)
        {
            return;
        }
        _demolitionRoundActive = false;
        _player.EjectFromVehicleIfAny();
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        foreach (var defender in _demolitionDefenders)
        {
            if (IsInstanceValid(defender))
            {
                defender.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        var result = _demolitionMatch.RecordRound(
            victory ? DemolitionTeam.Attackers : DemolitionTeam.Defenders);
        if (result.MatchComplete)
        {
            CompleteDemolitionMatch(reason);
            return;
        }

        _demolitionIntermissionRemaining = DemolitionIntermissionDuration;
        var overtime = result.EnteredOvertime
            ? GameLocalization.IsChinese(_languageSetting) ? "  //  进入加时" : "  //  OVERTIME STARTS"
            : string.Empty;
        var roundMessage = GameLocalization.IsChinese(_languageSetting)
            ? $"本局结束  //  {reason}  //  进攻 {result.AttackerScore}:{result.DefenderScore} 防守{overtime}"
            : $"ROUND COMPLETE  //  {reason}  //  ATTACK {result.AttackerScore}:{result.DefenderScore} DEFEND{overtime}";
        _hud.ShowRadioMessage(roundMessage, victory
            ? new Color(1.0f, 0.62f, 0.22f)
            : new Color(0.35f, 0.85f, 0.7f));
    }

    private void CompleteDemolitionMatch(string finalRoundReason)
    {
        _missionEnded = true;
        _demolitionIntermissionRemaining = 0.0f;
        var playerVictory = _demolitionMatch.Winner == DemolitionTeam.Attackers;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(playerVictory, _kills, _headshots, _shotsFired, _shotsHit);
        var overtime = _demolitionMatch.IsOvertime
            ? GameLocalization.IsChinese(_languageSetting) ? "加时" : "OVERTIME"
            : GameLocalization.IsChinese(_languageSetting) ? "常规阶段" : "REGULATION";
        var result = GameLocalization.IsChinese(_languageSetting)
            ? $"{finalRoundReason}\n最终比分  进攻 {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} 防守  //  {overtime}"
            : $"{finalRoundReason}\nFINAL SCORE  ATTACK {_demolitionMatch.AttackerScore}:{_demolitionMatch.DefenderScore} DEFEND  //  {overtime}";
        _hud.ShowDemolitionResult(playerVictory, result);
    }
}
