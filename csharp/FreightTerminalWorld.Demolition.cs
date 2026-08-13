using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionPlantDuration = 3.4f;
    private const float DemolitionFuseDuration = 32.0f;
    private const float DemolitionDefuseDuration = 6.5f;

    private readonly List<Node3D> _demolitionSites = new();
    private readonly List<EnemyOperator> _demolitionDefenders = new();
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

    public bool IsDemolitionMode => _demolitionMode;
    public bool IsDemolitionRoundActive => _demolitionRoundActive;
    public bool IsDemolitionDevicePlanted => _demolitionDevicePlanted;
    public int DemolitionSiteCount => _demolitionSites.Count;
    public int DemolitionDefenderCount => _demolitionDefenders.Count(defender => IsInstanceValid(defender) && !defender.IsDead);
    public float DemolitionSecondsRemaining => _demolitionRemaining;
    public float DemolitionDefuseProgress => _demolitionDefuseProgress;
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

    private void OnDemolitionDeploymentRequested(int role)
    {
        if (_squadDeployed || _missionEnded)
        {
            return;
        }

        PrepareDemolitionBattlefield();
        var layout = DemolitionLayout();
        _player.GlobalPosition = layout.AttackSpawn;
        _player.Rotation = Vector3.Zero;
        _player.ApplyDeploymentLoadout(BuildDemolitionLoadout());
        DeploySquad((OperatorRole)role, SquadSessionMode.Local, "127.0.0.1");
        _missionDirector.ExitDeploymentZone();
        _missionDirector.RaiseConfirmedAlarm();
        _missionPhase = "DEMOLITION";
        _hud.SetMissionPhase(_missionPhase, DemolitionFuseDuration, false);
        _hud.SetObjective(GameLocalization.Get(
            "demolition_objective_plant",
            _languageSetting,
            "CHOOSE SITE A OR B  //  HOLD F TO PLANT"));
        _hud.ShowLocalizedMessage(
            "demolition_deployed",
            "DEMOLITION TEAM DEPLOYED  //  FIXED KIT  //  PLANT AT A OR B",
            new Color(1.0f, 0.58f, 0.2f));
    }

    private static DeploymentLoadout BuildDemolitionLoadout()
    {
        return new DeploymentLoadout(
            new DeploymentLoadoutSelection("m4a1", "standard", LootGrade.Common, 120),
            WeaponCatalog.Build(WeaponPlatform.M4A1, 1),
            "helmet_patrol",
            "armor_carrier",
            "pack_assault",
            LootGrade.Common,
            120,
            0);
    }

    private void PrepareDemolitionBattlefield()
    {
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            throw new System.InvalidOperationException("Demolition arena was not built before deployment.");
        }
        _demolitionMode = true;
        _demolitionRoundActive = true;
        _demolitionDevicePlanted = false;
        _demolitionActiveSite = -1;
        _demolitionPlantProgress = 0.0f;
        _demolitionRemaining = DemolitionFuseDuration;
        _demolitionDefuseProgress = 0.0f;
        _demolitionDefuser = null;
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

        _demolitionDefenders.Clear();
        var spawns = _demolitionArena.Layout.DefenderSpawns;
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
            defender.LookAt(_demolitionArena.Layout.Midpoint, Vector3.Up);
            _demolitionDefenders.Add(defender);
        }
        _enemiesRemaining = _demolitionDefenders.Count;
        _hud.SetEnemyCount(_enemiesRemaining);
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
        if (!_demolitionMode || !_demolitionRoundActive || _missionEnded)
        {
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
        if (!_demolitionDevicePlanted)
        {
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
        _hud.SetMissionPhase("DEMOLITION", _demolitionRemaining, false);
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

    private void SelectDemolitionDefuser()
    {
        if (!_demolitionDevicePlanted || _demolitionActiveSite < 0)
        {
            _demolitionDefuser = null;
            return;
        }
        if (IsInstanceValid(_demolitionDefuser) && !_demolitionDefuser!.IsDead)
        {
            return;
        }
        var devicePosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
        _demolitionDefuser = _demolitionDefenders
            .Where(defender => IsInstanceValid(defender) && !defender.IsDead)
            .OrderBy(defender => defender.GlobalPosition.DistanceSquaredTo(devicePosition))
            .FirstOrDefault();
        _demolitionDefuser?.ResetScriptedObjectiveNavigation();
        PlanDemolitionDefuseRoute();
        _demolitionDefuseProgress = 0.0f;
    }

    private void PlanDemolitionDefuseRoute()
    {
        _demolitionDefuseRoute = System.Array.Empty<Vector3>();
        _demolitionDefuseRouteIndex = 0;
        if (!IsInstanceValid(_demolitionDefuser) || _demolitionActiveSite < 0)
        {
            return;
        }

        var destination = DemolitionLayout().SitePositions[_demolitionActiveSite];
        if (_demolitionDefuser!.IsScriptedObjectiveCorridorClear(destination))
        {
            _demolitionDefuseRoute = new[] { destination };
            return;
        }

        var start = _demolitionDefuser.GlobalPosition;
        var forward = destination - start;
        forward.Y = 0.0f;
        forward = forward.Normalized();
        var side = new Vector3(-forward.Z, 0.0f, forward.X);
        var bestLength = float.PositiveInfinity;
        foreach (var sideSign in new[] { 1.0f, -1.0f })
        {
            foreach (var lateral in new[] { 2.5f, 3.5f, 4.5f, 5.5f })
            {
                foreach (var forwardOffset in new[] { 0.0f, 2.0f, 4.0f })
                {
                    var waypoint = start + side * (sideSign * lateral) + forward * forwardOffset;
                    waypoint.Y = start.Y;
                    if (!_demolitionDefuser.IsScriptedObjectiveCorridorClear(waypoint))
                    {
                        continue;
                    }

                    var oldPosition = _demolitionDefuser.GlobalPosition;
                    _demolitionDefuser.GlobalPosition = waypoint;
                    var destinationClear = _demolitionDefuser.IsScriptedObjectiveCorridorClear(destination);
                    _demolitionDefuser.GlobalPosition = oldPosition;
                    if (!destinationClear)
                    {
                        continue;
                    }

                    var length = HorizontalDistance(start, waypoint) + HorizontalDistance(waypoint, destination);
                    if (length < bestLength)
                    {
                        bestLength = length;
                        _demolitionDefuseRoute = new[] { waypoint, destination };
                    }
                }
            }
        }

        if (_demolitionDefuseRoute.Length == 0)
        {
            _demolitionDefuseRoute = new[] { destination };
        }
    }

    public bool TryHandleDemolitionDefenderMovement(EnemyOperator defender, float delta, Node3D? combatTarget)
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || !_demolitionDevicePlanted
            || defender != _demolitionDefuser
            || defender.IsDead
            || _demolitionActiveSite < 0)
        {
            return false;
        }

        var targetDistance = combatTarget is null || !IsInstanceValid(combatTarget)
            ? float.PositiveInfinity
            : defender.GlobalPosition.DistanceTo(combatTarget.GlobalPosition);
        if (targetDistance < 12.0f)
        {
            _demolitionDefuseProgress = Mathf.Max(0.0f, _demolitionDefuseProgress - delta * 0.35f);
            return false;
        }

        var devicePosition = DemolitionLayout().SitePositions[_demolitionActiveSite];
        var flatDevice = new Vector3(devicePosition.X, defender.GlobalPosition.Y, devicePosition.Z);
        var distance = defender.GlobalPosition.DistanceTo(flatDevice);
        var velocity = defender.Velocity;
        if (distance > 2.15f)
        {
            if (_demolitionDefuseRoute.Length == 0)
            {
                PlanDemolitionDefuseRoute();
            }
            while (_demolitionDefuseRouteIndex < _demolitionDefuseRoute.Length - 1
                && HorizontalDistance(defender.GlobalPosition, _demolitionDefuseRoute[_demolitionDefuseRouteIndex]) < 0.85f)
            {
                _demolitionDefuseRouteIndex++;
                defender.ResetScriptedObjectiveNavigation();
            }
            var movementTarget = _demolitionDefuseRoute[
                Mathf.Clamp(_demolitionDefuseRouteIndex, 0, _demolitionDefuseRoute.Length - 1)];
            movementTarget.Y = defender.GlobalPosition.Y;
            var direction = defender.GlobalPosition.DirectionTo(movementTarget);
            direction.Y = 0.0f;
            if (direction.LengthSquared() > 0.01f)
            {
                direction = defender.ResolveScriptedObjectiveDirection(movementTarget, delta);
                defender.LookAt(defender.GlobalPosition + direction, Vector3.Up);
                velocity.X = Mathf.MoveToward(velocity.X, direction.X * 5.3f, delta * 12.0f);
                velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * 5.3f, delta * 12.0f);
            }
            defender.Velocity = velocity;
            return true;
        }

        velocity.X = Mathf.MoveToward(velocity.X, 0.0f, delta * 18.0f);
        velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, delta * 18.0f);
        defender.Velocity = velocity;
        _demolitionDefuseProgress = Mathf.Min(1.0f, _demolitionDefuseProgress + delta / DemolitionDefuseDuration);
        if (_demolitionDefuseProgress >= 1.0f)
        {
            var siteName = ((char)('A' + _demolitionActiveSite)).ToString();
            FinishDemolitionRound(
                false,
                GameLocalization.Format(
                    "demolition_device_defused",
                    _languageSetting,
                    "SITE {0} DEVICE DEFUSED",
                    siteName));
        }
        return true;
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
        if (_missionEnded)
        {
            return;
        }
        _demolitionRoundActive = false;
        _missionEnded = true;
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
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _missionDirector.CompleteMission(victory, _kills, _headshots, _shotsFired, _shotsHit);
        _hud.ShowDemolitionResult(victory, reason);
    }
}
