using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionPlantDuration = 3.4f;
    private const float DemolitionFuseDuration = 32.0f;
    private const float DemolitionDefuseDuration = 6.5f;
    private static readonly Vector3 DemolitionAttackSpawn = new(0.0f, 0.2f, 31.0f);
    private static readonly Vector3[] DemolitionSitePositions =
    {
        new(21.0f, 0.18f, -12.0f),
        new(-26.0f, 0.18f, -5.0f)
    };

    private readonly List<Node3D> _demolitionSites = new();
    private readonly List<EnemyOperator> _demolitionDefenders = new();
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

    private void BuildDemolitionSites()
    {
        var baseMaterial = Mat("demolition_site_base", new Color(0.12f, 0.13f, 0.12f), 0.54f, 0.56f);
        var orange = Mat(
            "demolition_site_orange",
            new Color(1.0f, 0.45f, 0.08f),
            0.12f,
            0.3f,
            new Color(0.82f, 0.16f, 0.01f));
        var dark = Mat("demolition_site_dark", new Color(0.025f, 0.035f, 0.032f), 0.72f, 0.34f);
        for (var index = 0; index < DemolitionSitePositions.Length; index++)
        {
            var site = new Node3D
            {
                Name = $"DemolitionSite_{(char)('A' + index)}",
                Position = DemolitionSitePositions[index],
                Visible = false
            };
            _levelRoot.AddChild(site);
            site.AddChild(new MeshInstance3D
            {
                Name = "SiteRing",
                Mesh = new TorusMesh { InnerRadius = 2.8f, OuterRadius = 3.05f, Rings = 36, RingSegments = 8 },
                MaterialOverride = orange
            });
            OfficeBox(site, "SitePlate", new Vector3(0, 0.04f, 0), new Vector3(4.7f, 0.08f, 4.7f), baseMaterial);
            OfficeBox(site, "SiteCaseLeft", new Vector3(-1.85f, 0.38f, -1.65f), new Vector3(1.2f, 0.7f, 0.75f), dark);
            OfficeBox(site, "SiteCaseRight", new Vector3(1.85f, 0.38f, 1.65f), new Vector3(1.2f, 0.7f, 0.75f), dark);
            var label = new Label3D
            {
                Name = "SiteLabel",
                Text = ((char)('A' + index)).ToString(),
                Position = new Vector3(0, 0.18f, 0),
                RotationDegrees = new Vector3(-90, 0, 0),
                FontSize = 170,
                Modulate = new Color(1.0f, 0.58f, 0.16f),
                OutlineSize = 8,
                NoDepthTest = false
            };
            site.AddChild(label);
            _demolitionSites.Add(site);
        }
    }

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
            if (hit["collider"].AsGodotObject() is StaticBody3D)
            {
                return false;
            }
        }
        return true;
    }

    private void OnDemolitionDeploymentRequested(int role)
    {
        if (_squadDeployed || _missionEnded)
        {
            return;
        }

        PrepareDemolitionBattlefield();
        _player.GlobalPosition = DemolitionAttackSpawn;
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
        foreach (var site in _demolitionSites)
        {
            site.Visible = true;
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
        var spawns = new[]
        {
            DemolitionSitePositions[0] + new Vector3(-5.0f, 0, 2.5f),
            DemolitionSitePositions[0] + new Vector3(4.5f, 0, -2.0f),
            DemolitionSitePositions[0] + new Vector3(0.5f, 0, -7.0f),
            DemolitionSitePositions[1] + new Vector3(-4.5f, 0, -2.0f),
            DemolitionSitePositions[1] + new Vector3(4.8f, 0, 2.8f),
            DemolitionSitePositions[1] + new Vector3(0.0f, 0, -7.0f),
            new Vector3(0.0f, 0.2f, -8.0f)
        };
        for (var index = 0; index < spawns.Length; index++)
        {
            var weapon = index % 3 == 0
                ? WeaponCatalog.Build(WeaponPlatform.MP5A5, 1)
                : WeaponCatalog.Build(WeaponPlatform.M4A1, 1);
            var defender = SpawnEnemy(
                spawns[index],
                alerted: true,
                teamId: 0,
                initialWeapon: weapon,
                sentryMode: false,
                detectionRange: 62.0f);
            defender.Name = $"DemolitionDefender_{index + 1:00}";
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
        for (var index = 0; index < DemolitionSitePositions.Length; index++)
        {
            var distance = HorizontalDistance(_player.GlobalPosition, DemolitionSitePositions[index]);
            if (distance < nearestDistance && Mathf.Abs(_player.GlobalPosition.Y - DemolitionSitePositions[index].Y) < 2.8f)
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
        if (_demolitionDevicePlanted || siteIndex < 0 || siteIndex >= DemolitionSitePositions.Length)
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
            Position = DemolitionSitePositions[siteIndex] + new Vector3(0, 0.34f, 0)
        };
        _levelRoot.AddChild(_demolitionDevice);
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
        var devicePosition = DemolitionSitePositions[_demolitionActiveSite];
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

        var destination = DemolitionSitePositions[_demolitionActiveSite];
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

        var devicePosition = DemolitionSitePositions[_demolitionActiveSite];
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

    private async void ValidateDemolitionMode()
    {
        await WaitFrames(5);
        var creditsBefore = _operatorProfileStore.Profile.Credits;
        var deploymentsBefore = _operatorProfileStore.Profile.DeploymentCount;

        _hud.PressDemolitionModeForDiagnostics();
        var entryButton = _hud.IsDemolitionBriefingVisible && !_hud.IsOperationsOfficeVisible;
        _hud.PressDemolitionRoleForDiagnostics(OperatorRole.Medic);
        var roleButton = _hud.SelectedDemolitionRole == OperatorRole.Medic;
        _hud.PressDemolitionDeployForDiagnostics();
        await WaitFrames(4);
        var fixedKit = _player.Role == OperatorRole.Medic
            && _player.HasFireablePrimary
            && _player.EquippedWeapon.Platform == WeaponPlatform.M4A1
            && _player.CurrentAmmoGrade == LootGrade.Common
            && _player.ReserveAmmo == 120;
        var isolatedEconomy = _operatorProfileStore.Profile.Credits == creditsBefore
            && _operatorProfileStore.Profile.DeploymentCount == deploymentsBefore
            && !_deploymentPurchaseCommitted;
        var deployed = _demolitionMode
            && _demolitionRoundActive
            && _squadDeployed
            && ActiveSquadCount == 3
            && DemolitionDefenderCount == 7
            && _demolitionSites.All(site => site.Visible)
            && !_extractionMarker.Visible
            && _hud.IsGameplayHudVisible
            && !_hud.IsDemolitionBriefingVisible
            && !GetTree().Paused;
        var sitesClear = DemolitionSitePositions.All(IsDemolitionSitePlacementClear);

        var hostileAircraftIsolated = !IsInstanceValid(_aircraft)
            || (_aircraft!.ProcessMode == ProcessModeEnum.Disabled
                && !_aircraft.Visible);
        var demolitionPhase = _missionPhase;
        var defenderCountBeforeReinforcementTick = DemolitionDefenderCount;
        _reinforcementPending = true;
        _reinforcementCountdown = 0.0f;
        _missionPhase = "COMBAT";
        UpdateReinforcements(8.0f);
        var reinforcementsIsolated = _reinforcementPending
            && !_reinforcementsDeployed
            && DemolitionDefenderCount == defenderCountBeforeReinforcementTick
            && _enemies.Count == defenderCountBeforeReinforcementTick;
        _reinforcementPending = false;
        _reinforcementCountdown = 0.0f;
        _missionPhase = demolitionPhase;

        OnPhaseChanged("COMBAT", 18.0f, true);
        OnObjectiveChanged(2, "REACH THE EXTRACTION ZONE", true);
        var directorIsolation = _missionPhase == "DEMOLITION"
            && !_extractionMarker.Visible;

        _player.GlobalPosition = DemolitionSitePositions[0] + new Vector3(0, 0.1f, 0);
        _player.Velocity = Vector3.Zero;
        _interactReleaseRequired = false;
        Input.ActionRelease("interact");
        Input.ActionPress("interact");
        var plantSteps = 0;
        var maximumPlantSteps = Mathf.CeilToInt(DemolitionPlantDuration / 0.1f) + 2;
        while (!_demolitionDevicePlanted && plantSteps < maximumPlantSteps)
        {
            UpdateDemolitionInteraction(0.1f);
            plantSteps++;
        }
        Input.ActionRelease("interact");
        var planted = _demolitionDevicePlanted
            && _demolitionActiveSite == 0
            && IsInstanceValid(_demolitionDevice)
            && !_extractionCountdownActive
            && plantSteps > 1;
        SelectDemolitionDefuser();
        var defuser = _demolitionDefuser;
        var defuseAi = false;
        var initialDefuserDistance = 0.0f;
        var finalDefuserDistance = float.PositiveInfinity;
        var defuseFrames = 0;
        if (defuser is not null)
        {
            _player.GlobalPosition = new Vector3(100.0f, 0.2f, 100.0f);
            foreach (var mate in _squadMates)
            {
                if (IsInstanceValid(mate))
                {
                    mate.GlobalPosition = new Vector3(104.0f + mate.SquadSlot * 2.0f, 0.2f, 104.0f);
                    mate.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
            foreach (var defender in _demolitionDefenders)
            {
                if (IsInstanceValid(defender))
                {
                    defender.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
            defuser.GlobalPosition = DemolitionSitePositions[0] + new Vector3(0, 0, 8.0f);
            defuser.Velocity = Vector3.Zero;
            defuser.ResetScriptedObjectiveNavigation();
            PlanDemolitionDefuseRoute();
            defuser.ProcessMode = ProcessModeEnum.Inherit;
            initialDefuserDistance = HorizontalDistance(defuser.GlobalPosition, DemolitionSitePositions[0]);
            const int maximumDefuseFrames = 600;
            while (defuseFrames < maximumDefuseFrames
                && _demolitionDefuseProgress < 0.12f
                && !_missionEnded)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                defuseFrames++;
            }
            finalDefuserDistance = HorizontalDistance(defuser.GlobalPosition, DemolitionSitePositions[0]);
            defuseAi = initialDefuserDistance >= 7.5f
                && finalDefuserDistance <= 2.4f
                && _demolitionDefuseProgress >= 0.08f
                && !_missionEnded;
        }
        foreach (var defender in _demolitionDefenders)
        {
            if (IsInstanceValid(defender))
            {
                defender.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _demolitionRemaining = 0.05f;
        UpdateDemolitionRound(0.1f);
        var completed = _missionEnded
            && !_demolitionRoundActive
            && _hud.IsMissionResultVisible
            && _operatorProfileStore.Profile.Credits == creditsBefore
            && _operatorProfileStore.Profile.DeploymentCount == deploymentsBefore;
        var valid = entryButton && roleButton && fixedKit && isolatedEconomy && deployed && sitesClear
            && hostileAircraftIsolated && reinforcementsIsolated
            && directorIsolation && planted && defuseAi && completed;
        GD.Print($"DEMOLITION_CHECK valid={valid} entry_button={entryButton} role_button={roleButton} deployed={deployed} gameplay={_hud.IsGameplayHudVisible} squad={ActiveSquadCount} defenders={DemolitionDefenderCount} fixed_kit={fixedKit} economy={isolatedEconomy} aircraft_isolated={hostileAircraftIsolated} reinforcements_isolated={reinforcementsIsolated} director_isolation={directorIsolation} sites={DemolitionSiteCount} sites_clear={sitesClear} planted={planted} plant_steps={plantSteps} defuse_ai={defuseAi} defuse_distance={initialDefuserDistance:0.00}->{finalDefuserDistance:0.00} defuse_progress={_demolitionDefuseProgress:0.00} defuse_frames={defuseFrames}/600 completed={completed} result={_hud.IsMissionResultVisible}");
        GD.Print($"DEMOLITION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }
}
