using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionPlantDuration = 3.4f;
    private const float DemolitionFuseDuration = 40.0f;
    private const float DemolitionDefuseDuration = 7.0f;
    private const float DemolitionRoundDuration = 100.0f;
    private const float DemolitionIntermissionDuration = 5.0f;
    private const float DemolitionStrategyRefreshDuration = 1.5f;
    private const float DemolitionCombatEngageRange = 24.0f;
    private const float DemolitionCombatResumeRange = 30.0f;
    private const float DemolitionChannelGuardRange = 12.0f;
    internal const float DemolitionReconScanRange = 34.0f;
    private const int DemolitionSquadSize = 5;

    private readonly List<Node3D> _demolitionSites = new();
    private readonly List<EnemyOperator> _demolitionOpponents = new();
    private readonly Dictionary<EnemyOperator, DemolitionAssignment> _demolitionOpponentAssignments = new();
    private readonly Dictionary<EnemyOperator, DemolitionRouteCursor> _demolitionOpponentRoutes = new();
    private readonly Dictionary<SquadMate, string> _demolitionSquadAssignmentTargets = new();
    private readonly HashSet<EnemyOperator> _demolitionCombatBreakoffs = new();
    private readonly DemolitionMatchState _demolitionMatch = new();
    private readonly DemolitionStrategyPlanner _demolitionStrategyPlanner = new();
    private readonly DemolitionObjectiveChannelCoordinator _demolitionObjectiveChannelCoordinator = new();
    private readonly DemolitionEconomy _demolitionPlayerEconomy = new();
    private readonly DemolitionEconomy _demolitionOpponentEconomy = new();
    private DemolitionArenaRuntime? _demolitionArena;
    private DemolitionRoutePlanner? _demolitionRoutePlanner;
    private Node3D? _demolitionDevice;
    private EnemyOperator? _demolitionDefuser;
    private EnemyOperator? _demolitionCarrier;
    private int _demolitionEnemyTargetSite;
    private SquadMate? _demolitionSquadObjectiveMate;
    private int _demolitionSquadObjectiveSite = -1;
    private float _demolitionSquadPlantProgress;
    private float _demolitionSquadDefuseProgress;
    private bool _demolitionMode;
    private bool _demolitionRoundActive;
    private bool _demolitionDevicePlanted;
    private bool _demolitionObjectiveSpectatorActive;
    private int _demolitionActiveSite = -1;
    private float _demolitionPlantProgress;
    private float _demolitionRemaining = DemolitionRoundDuration;
    private float _demolitionPlayerDefuseProgress;
    private float _demolitionEnemyPlantProgress;
    private float _demolitionDefuseProgress;
    private float _demolitionPulse;
    private float _demolitionIntermissionRemaining;
    private float _demolitionStrategyRemaining;
    private OperatorRole _demolitionPlayerRole = OperatorRole.Assault;
    private string _demolitionSelectedMapId = DemolitionMapCatalog.TideforgeId;
    private DemolitionStrategyPlan? _demolitionAttackerPlan;
    private DemolitionStrategyPlan? _demolitionDefenderPlan;

    public bool IsDemolitionMode => _demolitionMode;
    public bool IsDemolitionRoundActive => _demolitionRoundActive;
    public bool IsDemolitionDevicePlanted => _demolitionDevicePlanted;
    public int DemolitionSiteCount => _demolitionSites.Count;
    public int DemolitionOpponentCount => _demolitionOpponents.Count(opponent => IsInstanceValid(opponent) && !opponent.IsDead);
    public int DemolitionSquadSizeTotal => 1 + _squadMates.Count(mate => IsInstanceValid(mate));
    public float DemolitionSecondsRemaining => _demolitionRemaining;
    public float DemolitionPlayerDefuseProgress => _demolitionPlayerDefuseProgress;
    public float DemolitionAiDefuseProgress => _demolitionDefuseProgress;
    public float DemolitionEnemyPlantProgress => _demolitionEnemyPlantProgress;
    public int DemolitionRoundNumber => _demolitionMatch.CurrentRound;
    public int DemolitionPlayerScore => LocalDemolitionScore;
    public int DemolitionOpponentScore => OpposingDemolitionScore;
    public bool IsDemolitionOvertime => _demolitionMatch.IsOvertime;
    public bool IsDemolitionMatchComplete => _demolitionMatch.IsComplete;
    public DemolitionTeam DemolitionPlayerSide => LocalDemolitionSide;
    public int DemolitionPlayerFunds => _demolitionPlayerEconomy.Funds;
    public int DemolitionOpponentFunds => _demolitionOpponentEconomy.Funds;
    public string DemolitionSelectedMapId => _demolitionSelectedMapId;
    public int DemolitionStrategyAssignmentCount
        => (_demolitionAttackerPlan?.Assignments.Count ?? 0)
        + (_demolitionDefenderPlan?.Assignments.Count ?? 0);
    public bool IsDemolitionArenaActive => _demolitionArena?.Active == true;
    public int DemolitionArenaCollisionBodyCount => _demolitionArena?.CollisionBodyCount ?? 0;
    public int DemolitionArenaVisualPartCount => _demolitionArena?.VisualPartCount ?? 0;

    private static DemolitionTeam DemolitionOtherSide(DemolitionTeam side)
        => side == DemolitionTeam.Attackers ? DemolitionTeam.Defenders : DemolitionTeam.Attackers;

    private void EnsureDemolitionArenaBuilt()
    {
        if (_demolitionArena is not null)
        {
            if (_demolitionArena.Layout.MapId == _demolitionSelectedMapId)
            {
                _demolitionRoutePlanner ??= new DemolitionRoutePlanner(_demolitionArena.Layout);
                return;
            }

            _demolitionArena.SetActive(false);
            _demolitionArena.Root.QueueFree();
            _demolitionArena = null;
            _demolitionRoutePlanner = null;
            _demolitionSites.Clear();
        }
        var layout = new DemolitionArenaLayout(_demolitionSelectedMapId);
        var builder = new DemolitionArenaBuilder(Mat, GroundMaterial);
        _demolitionArena = builder.Build(this, layout);
        _demolitionRoutePlanner = new DemolitionRoutePlanner(layout);
        _demolitionSites.Clear();
        _demolitionSites.AddRange(_demolitionArena.Sites);
    }

    private void OnDemolitionDeploymentRequested(
        int role,
        int primaryPlatform,
        int buildTier,
        int sidearmPlatform,
        string mapId,
        int sessionMode,
        string address,
        int networkTeam)
    {
        if (_squadDeployed || _missionEnded)
        {
            return;
        }

        var mode = System.Enum.IsDefined(typeof(SquadSessionMode), sessionMode)
            ? (SquadSessionMode)sessionMode
            : SquadSessionMode.Local;
        var team = System.Enum.IsDefined(typeof(DemolitionNetworkTeam), networkTeam)
            ? (DemolitionNetworkTeam)networkTeam
            : DemolitionNetworkTeam.Alpha;
        if (mode == SquadSessionMode.Join
            && !SquadNetwork.TryParseEndpoint(address, SquadNetwork.DefaultPort, out _, out _))
        {
            _hud.SetSquadStatus("JOIN FAILED  //  INVALID HOST OR PORT");
            return;
        }
        _demolitionPlayerRole = (OperatorRole)role;
        _demolitionSelectedMapId = DemolitionMapCatalog.Resolve(mapId).Id;
        _demolitionLocalNetworkTeam = mode == SquadSessionMode.Join
            ? team
            : DemolitionNetworkTeam.Alpha;
        _demolitionLocalNetworkSlot = 0;
        _demolitionNetworkClient = mode == SquadSessionMode.Join;
        PrepareDemolitionBattlefield();
        ConfigureDemolitionNetwork(mode, address, team);
        DeploySquad((OperatorRole)role, mode, address);
        _hud.SetDemolitionGameplayPresentation(true);
        StartDemolitionRound();
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _missionPhase = "DEMOLITION";
        _hud.SetSquadStatus("DEMOLITION  //  5 V 5  //  ATTACK THEN DEFEND");
        _hud.ShowLocalizedMessage(
            "demolition_deployed",
            "DEMOLITION 5V5  //  FIRST TO 13  //  SIDES SWAP AFTER ROUND 12",
            new Color(1.0f, 0.58f, 0.2f));
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
        _demolitionPlayerEconomy.Reset();
        _demolitionOpponentEconomy.Reset();
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
        _demolitionPlantProgress = 0.0f;
        _demolitionPlayerDefuseProgress = 0.0f;
        _demolitionEnemyPlantProgress = 0.0f;
        _demolitionDefuseProgress = 0.0f;
        _demolitionDefuser = null;
        _demolitionCarrier = null;
        _demolitionIntermissionRemaining = 0.0f;
        _demolitionBuyPhaseActive = false;
        _demolitionBuyRemaining = 0.0f;
        _demolitionStrategyRemaining = 0.0f;
        _demolitionAttackerPlan = null;
        _demolitionDefenderPlan = null;
        _demolitionOpponentAssignments.Clear();
        _demolitionOpponentRoutes.Clear();
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
        ClearDemolitionOpponents();
    }

    private IReadOnlyList<Vector3> DemolitionSpawnsFor(DemolitionTeam side)
    {
        var layout = DemolitionLayout();
        return side == DemolitionTeam.Attackers ? layout.AttackSpawns : layout.DefenderSpawns;
    }

    private void SpawnDemolitionOpponents()
    {
        ClearDemolitionOpponents();
        var opponentSide = DemolitionOtherSide(LocalDemolitionSide);
        var opponentTeam = OpposingLocalNetworkTeam;
        var spawns = DemolitionSpawnsFor(opponentSide);
        var layout = DemolitionLayout();
        var count = Mathf.Min(DemolitionSquadSize, spawns.Count);
        for (var index = 0; index < count; index++)
        {
            var opponent = SpawnEnemy(
                spawns[index],
                alerted: false,
                teamId: 0,
                initialWeapon: _demolitionOpponentRoundWeapon,
                sentryMode: opponentSide == DemolitionTeam.Defenders,
                detectionRange: 52.0f);
            opponent.Name = $"DemolitionOpponent_{index + 1:00}";
            opponent.NetworkId = DemolitionActorId(opponentTeam, index);
            if (IsDemolitionNetworkClient)
            {
                opponent.ConfigureNetworkProxy(-opponent.NetworkId, OperatorRole.Assault, human: false);
            }
            opponent.LookAt(layout.Midpoint, Vector3.Up);
            _demolitionOpponents.Add(opponent);
        }
        _enemiesRemaining = _demolitionOpponents.Count;
        _hud.SetEnemyCount(_enemiesRemaining);
    }

    private void ClearDemolitionOpponents()
    {
        foreach (var opponent in _demolitionOpponents)
        {
            if (!IsInstanceValid(opponent))
            {
                continue;
            }
            opponent.ProcessMode = ProcessModeEnum.Disabled;
            _enemies.Remove(opponent);
            opponent.QueueFree();
        }
        _demolitionOpponents.Clear();
        _demolitionOpponentAssignments.Clear();
        _demolitionOpponentRoutes.Clear();
        _demolitionCombatBreakoffs.Clear();
        _demolitionDefuser = null;
        _demolitionCarrier = null;
    }

    private void StartDemolitionRound()
    {
        if (!_demolitionMode || _demolitionMatch.IsComplete)
        {
            return;
        }

        ClearDemolitionDevice();
        ResetDemolitionSquad();
        ResolveDemolitionOpponentBuy();
        SpawnDemolitionOpponents();
        _demolitionRoundActive = false;
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
        _demolitionPlantProgress = 0.0f;
        _demolitionPlayerDefuseProgress = 0.0f;
        _demolitionEnemyPlantProgress = 0.0f;
        _demolitionDefuseProgress = 0.0f;
        _demolitionRemaining = DemolitionRoundDuration;
        _demolitionPulse = 0.0f;
        _demolitionIntermissionRemaining = 0.0f;
        _demolitionStrategyRemaining = 0.0f;
        _demolitionEnemyTargetSite = _demolitionMatch.CompletedRounds % 2;
        _demolitionSquadAssignmentTargets.Clear();
        _demolitionCombatBreakoffs.Clear();
        _demolitionOpponentRoutes.Clear();
        _demolitionSquadObjectiveMate = null;
        _demolitionSquadObjectiveSite = -1;
        _demolitionObjectiveSpectatorActive = false;
        _demolitionSquadPlantProgress = 0.0f;
        _demolitionSquadDefuseProgress = 0.0f;
        BeginDemolitionDeviceRound();
        _missionPhase = "DEMOLITION";
        RefreshDemolitionStrategies(true);
        BeginDemolitionBuyPhase();
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
        var squadNodes = GetTree().GetNodesInGroup("player_squad_ai");
        using var squadNodesBacking = squadNodes.AsDisposable();
        foreach (var orphan in squadNodes.OfType<SquadMate>().ToArray())
        {
            if (!_squadMates.Contains(orphan) && IsInstanceValid(orphan))
            {
                orphan.QueueFree();
            }
        }

        var layout = DemolitionLayout();
        var playerSide = LocalDemolitionSide;
        var spawns = DemolitionSpawnsFor(playerSide);
        _localPlayerDowned = false;
        _localPlayerEliminated = false;
        _localPlayerDownedTimer = 0.0f;
        _allDownTimer = 0.0f;
        ClearLeaderReviveAi();
        RestoreLocalPlayerView();
        var emptyQuote = DemolitionBuyCatalog.Quote(DemolitionPurchaseSelection.Empty, _demolitionPlayerEconomy.Funds);
        var loadout = DemolitionBuyCatalog.BuildLoadout(emptyQuote);
        var playerSpawnIndex = IsDemolitionNetworkClient
            ? Mathf.Clamp(_demolitionLocalNetworkSlot, 0, spawns.Count - 1)
            : 0;
        _player.ResetForDemolitionRound(
            spawns[playerSpawnIndex],
            _demolitionPlayerRole,
            loadout,
            0,
            0);
        _player.LookAt(layout.Midpoint, Vector3.Up);
        EnsureAiSquadFill();
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            var spawnIndex = Mathf.Clamp(mate.SquadSlot, 0, spawns.Count - 1);
            mate.ResetForDemolitionRound(spawns[spawnIndex]);
            mate.LookAt(layout.Midpoint, Vector3.Up);
        }
        ResetSquadLeaderTrail(_player.GlobalPosition);
        _hud.HideDownedState();
        RefreshSquadHud();
    }

    private bool ShouldObservePlantedDemolitionDevice()
        => _demolitionDevicePlanted
        && LocalDemolitionSide == DemolitionTeam.Attackers;

    private void BeginDemolitionObjectiveView()
    {
        if (!ShouldObservePlantedDemolitionDevice() || _demolitionActiveSite < 0)
        {
            return;
        }

        var camera = EnsureSquadSpectatorCamera();
        var layout = DemolitionLayout();
        var focus = IsInstanceValid(_demolitionDevice)
            ? _demolitionDevice!.GlobalPosition
            : layout.SitePositions[_demolitionActiveSite];
        var outward = focus - layout.Midpoint;
        outward.Y = 0.0f;
        if (outward.LengthSquared() < 0.01f)
        {
            outward = Vector3.Right;
        }
        camera.GlobalPosition = focus + outward.Normalized() * 7.5f + Vector3.Up * 6.0f;
        camera.LookAt(focus + Vector3.Up * 0.55f, Vector3.Up);
        camera.MakeCurrent();
        _demolitionObjectiveSpectatorActive = true;
        _hud.ShowLocalizedMessage(
            "demolition_spectating_device",
            "SPECTATING  //  PLANTED DEVICE",
            new Color(1.0f, 0.62f, 0.24f));
    }

    private void ClearDemolitionDevice()
    {
        _demolitionDeviceLifecycle.Clear();
        if (IsInstanceValid(_demolitionDevice))
        {
            _demolitionDevice!.QueueFree();
        }
        _demolitionDevice = null;
        _demolitionDeviceBeacon = null;
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
    }

    private void UpdateDemolitionRoundHud()
    {
        var sideLabel = GameLocalization.IsChinese(_languageSetting) ? "进攻" : "ATTACK";
        if (LocalDemolitionSide == DemolitionTeam.Defenders)
        {
            sideLabel = GameLocalization.IsChinese(_languageSetting) ? "防守" : "DEFEND";
        }
        var overtime = _demolitionMatch.IsOvertime
            ? GameLocalization.IsChinese(_languageSetting) ? "  //  加时" : "  //  OVERTIME"
            : string.Empty;
        var score = GameLocalization.IsChinese(_languageSetting)
            ? $"第 {_demolitionMatch.CurrentRound} 局  //  己方 {LocalDemolitionScore}:{OpposingDemolitionScore} 敌方  //  {sideLabel}  //  ${_demolitionPlayerEconomy.Funds}{overtime}"
            : $"ROUND {_demolitionMatch.CurrentRound}  //  YOU {LocalDemolitionScore}:{OpposingDemolitionScore} ENEMY  //  {sideLabel}  //  ${_demolitionPlayerEconomy.Funds}{overtime}";
        _hud.SetMissionPhase(score, _demolitionRemaining, false);
        if (!_demolitionDevicePlanted)
        {
            var objective = LocalDemolitionSide == DemolitionTeam.Attackers
                ? DemolitionAttackerObjective(score)
                : GameLocalization.Format(
                    "demolition_defend_hold",
                    _languageSetting,
                    "{0}  //  HOLD A AND B  //  DENY THE PLANT",
                    score);
            _hud.SetObjective(objective);
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
            ? $"下一局  //  {_demolitionIntermissionRemaining:0.0}s  //  己方 {LocalDemolitionScore}:{OpposingDemolitionScore} 敌方"
            : $"NEXT ROUND  //  {_demolitionIntermissionRemaining:0.0}s  //  YOU {LocalDemolitionScore}:{OpposingDemolitionScore} ENEMY";
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
        if (!_demolitionDevicePlanted)
        {
            UpdateDemolitionPlantInteraction(delta);
            return;
        }
        UpdateDemolitionDefuseInteraction(delta);
    }

    private void UpdateDemolitionPlantInteraction(float delta)
    {
        if (LocalDemolitionSide != DemolitionTeam.Attackers)
        {
            _demolitionPlantProgress = 0.0f;
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }
        if (!PlayerCarriesDemolitionDevice())
        {
            _demolitionPlantProgress = 0.0f;
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

        var planting = Input.IsActionPressed(GameInputActions.Interact) && !_interactReleaseRequired;
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
            if (IsDemolitionNetworkClient)
            {
                _squadNetwork.RequestDemolitionAction(DemolitionNetworkAction.Plant, nearestIndex);
                _demolitionPlantProgress = 0.0f;
            }
            else
            {
                PlantDemolitionDevice(nearestIndex, byPlayerTeam: true, _player);
            }
        }
    }

    private void UpdateDemolitionDefuseInteraction(float delta)
    {
        if (LocalDemolitionSide != DemolitionTeam.Defenders || _demolitionActiveSite < 0)
        {
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }
        var layout = DemolitionLayout();
        var devicePosition = layout.SitePositions[_demolitionActiveSite];
        var distance = HorizontalDistance(_player.GlobalPosition, devicePosition);
        if (distance > 3.25f || Mathf.Abs(_player.GlobalPosition.Y - devicePosition.Y) > 2.8f)
        {
            _demolitionPlayerDefuseProgress = Mathf.Max(0.0f, _demolitionPlayerDefuseProgress - delta * 1.4f);
            _hud.SetInteraction(string.Empty, 0.0f, false);
            return;
        }

        var defusing = Input.IsActionPressed(GameInputActions.Interact) && !_interactReleaseRequired;
        _demolitionPlayerDefuseProgress = defusing
            ? Mathf.Min(1.0f, _demolitionPlayerDefuseProgress + delta / DemolitionDefuseDuration)
            : Mathf.Max(0.0f, _demolitionPlayerDefuseProgress - delta * 1.2f);
        var siteName = ((char)('A' + _demolitionActiveSite)).ToString();
        var action = GameLocalization.Format(
            "demolition_interaction_defuse",
            _languageSetting,
            "DEFUSE DEMOLITION DEVICE  //  SITE {0}",
            siteName);
        _hud.SetInteraction(action, _demolitionPlayerDefuseProgress, true);
        if (_demolitionPlayerDefuseProgress >= 1.0f)
        {
            if (IsDemolitionNetworkClient)
            {
                _squadNetwork.RequestDemolitionAction(DemolitionNetworkAction.Defuse, _demolitionActiveSite);
                _demolitionPlayerDefuseProgress = 0.0f;
            }
            else
            {
                FinishDemolitionRound(
                    true,
                    GameLocalization.Format(
                        "demolition_device_defused",
                        _languageSetting,
                        "SITE {0} DEVICE DEFUSED",
                        siteName));
            }
        }
    }

    private void PlantDemolitionDevice(int siteIndex, bool byPlayerTeam, Node3D planter)
    {
        var layout = DemolitionLayout();
        var planterId = DemolitionMemberId(planter);
        if (_demolitionDevicePlanted
            || siteIndex < 0
            || siteIndex >= layout.SitePositions.Count
            || planterId is null
            || !_demolitionDeviceLifecycle.TryPlant(planterId))
        {
            return;
        }
        _demolitionDevicePlanted = true;
        _demolitionActiveSite = siteIndex;
        _demolitionRemaining = DemolitionFuseDuration;
        _demolitionPlantProgress = 0.0f;
        _demolitionEnemyPlantProgress = 0.0f;
        _demolitionDefuseProgress = 0.0f;
        _demolitionPlayerDefuseProgress = 0.0f;
        if (!IsInstanceValid(_demolitionDevice))
        {
            BuildDemolitionDeviceVisual();
        }
        _demolitionDevice!.Name = $"PlantedDemolitionDevice_{_demolitionMatch.CurrentRound:00}";
        _demolitionDevice.GlobalPosition = layout.SitePositions[siteIndex] + Vector3.Up * 0.34f;
        _demolitionDevice.Scale = Vector3.One;
        _demolitionDevice.Visible = true;
        SetDemolitionDeviceBeacon(active: true, energy: 5.5f, range: 9.0f);
        var previousCarrier = _demolitionCarrier;
        _demolitionCarrier = null;
        ResetDemolitionOpponentRoute(previousCarrier);
        _demolitionSquadObjectiveMate = null;
        _demolitionSquadPlantProgress = 0.0f;
        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent) && !opponent.IsDead)
            {
                opponent.SentryMode = false;
                opponent.SetAlerted(_player.GlobalPosition);
            }
        }
        RefreshDemolitionStrategies(true);
        var plantedSiteName = ((char)('A' + siteIndex)).ToString();
        var messageKey = byPlayerTeam ? "demolition_planted" : "demolition_planted_by_enemy";
        var messageEnglish = byPlayerTeam
            ? "DEVICE PLANTED AT SITE {0}  //  DEFEND FOR {1:0} SECONDS"
            : "ENEMY DEVICE PLANTED AT SITE {0}  //  DEFUSE OR LOSE THE ROUND";
        _hud.ShowRadioMessage(
            GameLocalization.Format(
                messageKey,
                _languageSetting,
                messageEnglish,
                plantedSiteName,
                DemolitionFuseDuration),
            byPlayerTeam
                ? new Color(1.0f, 0.5f, 0.16f)
                : new Color(1.0f, 0.3f, 0.2f));
    }

    private void UpdateDemolitionRound(float delta)
    {
        if (!_demolitionMode || _missionEnded)
        {
            return;
        }
        if (IsDemolitionNetworkClient)
        {
            UpdateDemolitionNetworkClientRound(delta);
            return;
        }
        if (_demolitionBuyPhaseActive)
        {
            UpdateDemolitionBuyPhase(delta);
            return;
        }
        if (!_demolitionRoundActive)
        {
            UpdateDemolitionIntermission(delta);
            return;
        }

        UpdateDemolitionDeviceLifecycle();

        var playerSide = _demolitionMatch.PlayerSide;
        var opponentsAlive = DemolitionOpponentCount;
        var playerSquadEliminated = _player.IsDead && _squadMates
            .Where(IsInstanceValid)
            .All(mate => mate.IsDowned || mate.IsBodyBag);
        if (playerSquadEliminated
            && DemolitionRoundRules.EliminationEndsRound(playerSide, _demolitionDevicePlanted))
        {
            FinishDemolitionRound(
                false,
                GameLocalization.Get(
                    "demolition_squad_eliminated",
                    _languageSetting,
                    "SQUAD ELIMINATED"));
            return;
        }
        var opponentSide = playerSide == DemolitionTeam.Attackers
            ? DemolitionTeam.Defenders
            : DemolitionTeam.Attackers;
        if (opponentsAlive == 0
            && DemolitionRoundRules.EliminationEndsRound(opponentSide, _demolitionDevicePlanted))
        {
            FinishDemolitionRound(
                true,
                GameLocalization.Get(
                    "demolition_opponents_eliminated",
                    _languageSetting,
                    "OPPOSING SQUAD ELIMINATED"));
            return;
        }
        _demolitionStrategyRemaining -= delta;
        if (_demolitionStrategyRemaining <= 0.0f)
        {
            RefreshDemolitionStrategies(false);
        }
        UpdateDemolitionSquadPosts();
        UpdateDemolitionSquadObjectiveRelay(delta);
        if (!_demolitionDevicePlanted)
        {
            EnsureDemolitionDevicePickupRunner();
            _demolitionRemaining = Mathf.Max(0.0f, _demolitionRemaining - delta);
            UpdateDemolitionRoundHud();
            if (_demolitionRemaining <= 0.0f)
            {
                var defendersWin = true;
                var timeoutKey = "demolition_round_timeout_defended";
                var timeoutEnglish = "ATTACK WINDOW EXPIRED  //  SITES HELD";
                FinishDemolitionRound(
                    defendersWin == (playerSide == DemolitionTeam.Defenders),
                    GameLocalization.Get(timeoutKey, _languageSetting, timeoutEnglish));
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
        var siteName = ((char)('A' + _demolitionActiveSite)).ToString();
        if (playerSide == DemolitionTeam.Attackers)
        {
            SelectDemolitionDefuser();
        }
        var defusePercent = playerSide == DemolitionTeam.Defenders
            ? _demolitionPlayerDefuseProgress
            : _demolitionDefuseProgress;
        var defuse = defusePercent > 0.01f
            ? GameLocalization.Format(
                "demolition_defuse_suffix",
                _languageSetting,
                "  //  DEFUSE {0:00}%",
                Mathf.RoundToInt(defusePercent * 100.0f))
            : string.Empty;
        var objectiveKey = playerSide == DemolitionTeam.Attackers
            ? "demolition_defend"
            : "demolition_defuse_hold";
        var objectiveEnglish = playerSide == DemolitionTeam.Attackers
            ? "DEFEND SITE {0}  //  {1:00.0}s{2}"
            : "DEFUSE SITE {0}  //  {1:00.0}s{2}";
        _hud.SetObjective(GameLocalization.Format(
            objectiveKey,
            _languageSetting,
            objectiveEnglish,
            siteName,
            _demolitionRemaining,
            defuse));
        var sideLabel = playerSide == DemolitionTeam.Attackers
            ? (GameLocalization.IsChinese(_languageSetting) ? "进攻" : "ATTACK")
            : (GameLocalization.IsChinese(_languageSetting) ? "防守" : "DEFEND");
        var phase = GameLocalization.IsChinese(_languageSetting)
            ? $"第 {_demolitionMatch.CurrentRound} 局  //  己方 {_demolitionMatch.PlayerScore}:{_demolitionMatch.OpponentScore} 敌方  //  {sideLabel}"
            : $"ROUND {_demolitionMatch.CurrentRound}  //  YOU {_demolitionMatch.PlayerScore}:{_demolitionMatch.OpponentScore} ENEMY  //  {sideLabel}";
        _hud.SetMissionPhase(phase, _demolitionRemaining, false);
        if (_demolitionRemaining <= 0.0f)
        {
            DetonateDemolitionDevice();
            FinishDemolitionRound(
                playerSide == DemolitionTeam.Attackers,
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
        using var shape = new BoxShape3D { Size = new Vector3(4.2f, 1.55f, 4.2f) };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, position + Vector3.Up * 1.05f),
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 64);
        using var hitsBacking = hits.AsDisposable();
        for (var index = 0; index < hits.Count; index++)
        {
            using var hit = hits[index];
            using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
            if (colliderValue.AsGodotObject() is StaticBody3D body
                && _demolitionArena?.Owns(body) != true)
            {
                return false;
            }
        }
        return true;
    }

    private void FinishDemolitionRound(bool playerWon, string reason)
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
        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent))
            {
                opponent.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        var defused = _demolitionPlayerDefuseProgress >= 1.0f;
        var playerPlanted = _demolitionDevicePlanted
            && _demolitionMatch.PlayerSide == DemolitionTeam.Attackers;
        var opponentsPlanted = _demolitionDevicePlanted
            && _demolitionMatch.PlayerSide == DemolitionTeam.Defenders;
        _demolitionPlayerEconomy.RecordRound(playerWon, defused || playerPlanted && !playerWon);
        _demolitionOpponentEconomy.RecordRound(!playerWon, opponentsPlanted && playerWon);

        var result = _demolitionMatch.RecordRound(playerWon);
        if (result.MatchComplete)
        {
            CompleteDemolitionMatch(reason);
            return;
        }

        if (result.SideSwap)
        {
            _demolitionPlayerEconomy.Reset();
            _demolitionOpponentEconomy.Reset();
        }

        _demolitionIntermissionRemaining = DemolitionIntermissionDuration;
        var swap = result.SideSwap
            ? GameLocalization.IsChinese(_languageSetting)
                ? "  //  攻防互换，资金重置"
                : "  //  SIDES SWAP  //  FUNDS RESET"
            : string.Empty;
        var overtime = result.EnteredOvertime
            ? GameLocalization.IsChinese(_languageSetting) ? "  //  进入加时" : "  //  OVERTIME STARTS"
            : string.Empty;
        var roundMessage = GameLocalization.IsChinese(_languageSetting)
            ? $"本局结束  //  {reason}  //  己方 {result.PlayerScore}:{result.OpponentScore} 敌方{swap}{overtime}  //  ${_demolitionPlayerEconomy.Funds}"
            : $"ROUND COMPLETE  //  {reason}  //  YOU {result.PlayerScore}:{result.OpponentScore} ENEMY{swap}{overtime}  //  ${_demolitionPlayerEconomy.Funds}";
        _hud.ShowRadioMessage(roundMessage, playerWon
            ? new Color(1.0f, 0.62f, 0.22f)
            : new Color(0.35f, 0.85f, 0.7f));
    }

    private void CompleteDemolitionMatch(string finalRoundReason)
    {
        _missionEnded = true;
        LockLootForMissionTransition(Input.MouseModeEnum.Visible);
        _demolitionIntermissionRemaining = 0.0f;
        var playerVictory = LocalDemolitionScore > OpposingDemolitionScore;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(playerVictory, _kills, _headshots, _shotsFired, _shotsHit);
        var overtime = _demolitionMatch.IsOvertime
            ? GameLocalization.IsChinese(_languageSetting) ? "加时" : "OVERTIME"
            : GameLocalization.IsChinese(_languageSetting) ? "常规阶段" : "REGULATION";
        var result = GameLocalization.IsChinese(_languageSetting)
            ? $"{finalRoundReason}\n最终比分  己方 {LocalDemolitionScore}:{OpposingDemolitionScore} 敌方  //  {overtime}"
            : $"{finalRoundReason}\nFINAL SCORE  YOU {LocalDemolitionScore}:{OpposingDemolitionScore} ENEMY  //  {overtime}";
        _hud.ShowDemolitionResult(playerVictory, result);
    }
}
