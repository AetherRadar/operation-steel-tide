using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private enum RelayInteractionMode
    {
        None,
        Activate,
        Climb,
        Descend
    }

    private readonly List<ResidentialRelayStation> _residentialRelayStations = new();
    private readonly List<ResidentialSupplyCache> _relayCaches = new();
    private readonly List<Node3D> _relayLootMarkers = new();
    private ResidentialRelayStation? _relayInteractionTarget;
    private RelayInteractionMode _relayInteractionMode;
    private float _relayInteractionProgress;
    private float _relayActivationHealth;
    private ResidentialRelayStation? _relayClimbStation;
    private Vector3 _relayClimbStart;
    private Vector3 _relayClimbEnd;
    private float _relayClimbProgress;
    private int _relayActivationCount;
    private int _relayActivationInterruptedCount;
    private int _relayLastEnemyRevealCount;
    private int _relayLastLootRevealCount;

    public int ResidentialRelayStationCount => _residentialRelayStations.Count;
    public int ResidentialRelayCacheCount => _relayCaches.Count;
    public int ResidentialRelayActivationCount => _relayActivationCount;

    private bool UpdateRelayStationInteraction(float delta)
    {
        var (station, mode) = FindRelayInteraction();
        if (station is null || mode == RelayInteractionMode.None)
        {
            ResetRelayInteraction(_relayInteractionProgress > 0.02f);
            return false;
        }

        if (!ReferenceEquals(_relayInteractionTarget, station) || _relayInteractionMode != mode)
        {
            ResetRelayInteraction(_relayInteractionProgress > 0.02f);
            _relayInteractionTarget = station;
            _relayInteractionMode = mode;
        }
        _lootSearchTarget = null;
        _player.SetSearchPose(false);

        if (mode is RelayInteractionMode.Climb or RelayInteractionMode.Descend)
        {
            var key = mode == RelayInteractionMode.Descend ? "relay_descend" : "relay_climb";
            var english = mode == RelayInteractionMode.Descend ? "DESCEND RELAY LADDER" : "CLIMB RELAY LADDER";
            _hud.SetInteraction(GameLocalization.Get(key, _languageSetting, english), -1.0f, true);
            if (!_interactReleaseRequired && Input.IsActionJustPressed("interact"))
            {
                _interactReleaseRequired = true;
                BeginRelayClimb(station, mode == RelayInteractionMode.Descend);
            }
            return true;
        }

        if (station.IsActivated)
        {
            _hud.SetInteraction(
                GameLocalization.Get("relay_online", _languageSetting, "RELAY ONLINE // ROOF CACHE"),
                -1.0f,
                true);
            return true;
        }

        var held = Input.IsActionPressed("interact") && !_interactReleaseRequired;
        var completed = AdvanceRelayStationActivation(station, delta, held, notifyDamage: true);
        _hud.SetInteraction(
            GameLocalization.Get("relay_activate", _languageSetting, "ACTIVATE RELAY // SCAN + ROOF CACHE"),
            _relayInteractionProgress,
            true);
        if (completed)
        {
            ResetRelayInteraction(notify: false);
        }
        return true;
    }

    private bool AdvanceRelayStationActivation(
        ResidentialRelayStation station,
        float delta,
        bool held,
        bool notifyDamage)
    {
        if (_relayInteractionProgress > 0.001f && _player.Health < _relayActivationHealth - 0.01f)
        {
            InterruptRelayActivation(station, notifyDamage);
            return false;
        }
        if (held && _relayInteractionProgress <= 0.001f)
        {
            _relayActivationHealth = _player.Health;
        }
        _relayInteractionProgress = held
            ? Mathf.Min(1.0f, _relayInteractionProgress + delta / station.ActivationDuration)
            : Mathf.Max(0.0f, _relayInteractionProgress - delta * 1.25f);
        station.SetActivationProgress(_relayInteractionProgress);
        _player.SetSearchPose(_relayInteractionProgress > 0.02f, _relayInteractionProgress);
        if (_relayInteractionProgress < 1.0f)
        {
            return false;
        }
        _interactReleaseRequired = true;
        return CompleteRelayStationActivation(station);
    }

    private (ResidentialRelayStation? Station, RelayInteractionMode Mode) FindRelayInteraction()
    {
        ResidentialRelayStation? best = null;
        var bestMode = RelayInteractionMode.None;
        var bestDistance = float.PositiveInfinity;
        foreach (var station in _residentialRelayStations)
        {
            if (!IsInstanceValid(station))
            {
                continue;
            }
            if (station.IsOnRoof(_player.GlobalPosition))
            {
                // The unlocked roof cache wins unless the player steps right up to the
                // ladder opening, so descending never masks the loot prompt.
                var distance = _player.GlobalPosition.DistanceTo(station.RoofLandingPoint);
                if (distance <= 1.05f && distance < bestDistance)
                {
                    best = station;
                    bestMode = RelayInteractionMode.Descend;
                    bestDistance = distance;
                }
                continue;
            }

            var ladderDistance = _player.GlobalPosition.DistanceTo(station.LadderApproachPoint);
            if (ladderDistance <= 2.35f && ladderDistance < bestDistance)
            {
                best = station;
                bestMode = RelayInteractionMode.Climb;
                bestDistance = ladderDistance;
            }
            var terminalDistance = _player.GlobalPosition.DistanceTo(station.TerminalApproachPoint);
            if (terminalDistance <= 2.45f && terminalDistance < bestDistance - 0.05f)
            {
                best = station;
                bestMode = RelayInteractionMode.Activate;
                bestDistance = terminalDistance;
            }
        }
        return (best, bestMode);
    }

    private void ResetRelayInteraction(bool notify)
    {
        if (_relayInteractionTarget is not null && IsInstanceValid(_relayInteractionTarget))
        {
            if (notify && _relayInteractionMode == RelayInteractionMode.Activate)
            {
                _relayActivationInterruptedCount++;
                _hud.ShowLocalizedMessage(
                    "relay_interrupted",
                    "RELAY UPLINK INTERRUPTED",
                    new Color(1.0f, 0.48f, 0.24f));
            }
            _relayInteractionTarget.CancelActivation();
        }
        _relayInteractionTarget = null;
        _relayInteractionMode = RelayInteractionMode.None;
        _relayInteractionProgress = 0.0f;
        _player.SetSearchPose(false);
    }

    private void InterruptRelayActivation(ResidentialRelayStation station, bool notify)
    {
        station.CancelActivation();
        _relayInteractionProgress = 0.0f;
        _relayActivationHealth = _player.Health;
        _interactReleaseRequired = true;
        _player.SetSearchPose(false);
        _relayActivationInterruptedCount++;
        if (notify)
        {
            _hud.ShowLocalizedMessage(
                "relay_interrupted",
                "RELAY UPLINK INTERRUPTED // DAMAGE RECEIVED",
                new Color(1.0f, 0.42f, 0.22f));
        }
    }

    private void BeginRelayClimb(ResidentialRelayStation station, bool descend)
    {
        if (_relayClimbStation is not null || !IsInstanceValid(station))
        {
            return;
        }
        ResetRelayInteraction(notify: false);
        _relayClimbStation = station;
        _relayClimbStart = _player.GlobalPosition;
        _relayClimbEnd = descend ? station.LadderApproachPoint : station.RoofLandingPoint;
        _relayClimbProgress = 0.0f;
        _player.UiLocked = true;
        _player.Velocity = Vector3.Zero;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _player.SetSearchPose(true, 0.35f);
    }

    private bool UpdateRelayClimb(float delta)
    {
        if (_relayClimbStation is null)
        {
            return false;
        }
        if (!IsInstanceValid(_relayClimbStation) || _player.IsDead)
        {
            FinishRelayClimb();
            return false;
        }
        _relayClimbProgress = Mathf.Min(1.0f, _relayClimbProgress + delta / 1.05f);
        var t = _relayClimbProgress * _relayClimbProgress * (3.0f - 2.0f * _relayClimbProgress);
        var position = _relayClimbStart.Lerp(_relayClimbEnd, t);
        position += Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 0.1f;
        _player.GlobalPosition = position;
        _player.Velocity = Vector3.Zero;
        _player.SetSearchPose(true, Mathf.Lerp(0.35f, 0.8f, t));
        _hud.SetInteraction(
            GameLocalization.Get("relay_climbing", _languageSetting, "CLIMBING RELAY LADDER"),
            _relayClimbProgress,
            true);
        if (_relayClimbProgress >= 1.0f)
        {
            FinishRelayClimb();
            return false;
        }
        return true;
    }

    private void FinishRelayClimb()
    {
        _relayClimbStation = null;
        _relayClimbProgress = 0.0f;
        _player.UiLocked = false;
        _player.Velocity = Vector3.Zero;
        _player.SetSearchPose(false);
        _player.DisarmFireInput();
        _player.RestoreMovementInput();
        _hud.SetInteraction(string.Empty, 0.0f, false);
    }

    private bool CompleteRelayStationActivation(ResidentialRelayStation station)
    {
        if (!IsInstanceValid(station) || !station.CompleteActivation())
        {
            return false;
        }

        _relayActivationCount++;
        _relayLastEnemyRevealCount = 0;
        foreach (var enemy in _enemies)
        {
            if (!IsInstanceValid(enemy) || enemy.IsDead || enemy.GlobalPosition.DistanceTo(station.GlobalPosition) > 72.0f)
            {
                continue;
            }
            enemy.SetScanned(12.0f);
            _relayLastEnemyRevealCount++;
        }
        _relayLastLootRevealCount = RevealNearbyRelayLoot(station.GlobalPosition, 48.0f);
        var cache = SpawnRelayRoofCache(station);
        station.UnlockCache();
        SpawnRoleActivationPulse(station.GlobalPosition + Vector3.Up * 0.4f, station.Accent, 8.0f);
        ReportGunshot(station.GlobalPosition, 46.0f);
        _hud.ShowLocalizedMessage(
            "relay_unlocked",
            $"RELAY ONLINE // {_relayLastEnemyRevealCount:00} HOSTILES + {_relayLastLootRevealCount:00} CACHES MARKED // ROOF CACHE UNLOCKED",
            station.Accent);
        return cache is not null;
    }

    private ResidentialSupplyCache? SpawnRelayRoofCache(ResidentialRelayStation station)
    {
        if (_relayCaches.Any(cache => IsInstanceValid(cache)
            && cache.TowerIndex == station.TowerIndex
            && cache.FloorIndex == -station.CornerIndex - 1))
        {
            return null;
        }
        var kind = station.Kind switch
        {
            ResidentialRelayKind.Medical => ResidentialCacheKind.MedicalCabinet,
            ResidentialRelayKind.Security => ResidentialCacheKind.SecurityArmory,
            ResidentialRelayKind.Utility => ResidentialCacheKind.WorkshopLocker,
            _ => ResidentialCacheKind.EvacuationLocker
        };
        var cache = new ResidentialSupplyCache
        {
            Name = $"RelayRoofCache_T{station.TowerIndex + 1:00}_C{station.CornerIndex:00}",
            Position = station.ToLocal(station.RoofCachePoint),
            Rotation = new Vector3(0, station.FrontSign > 0 ? Mathf.Pi : 0.0f, 0)
        };
        cache.Configure(kind, station.TowerIndex, -station.CornerIndex - 1, CreateResidentialCacheLoot(kind));
        RegisterResidentialLanguageRefresher(cache.SetLanguage);
        station.AddChild(cache);
        _relayCaches.Add(cache);
        _lootSources.Add(cache);
        _lootWorldPoints.Add(cache.GlobalPosition);
        return cache;
    }

    private int RevealNearbyRelayLoot(Vector3 origin, float range)
    {
        var revealed = 0;
        foreach (var source in _lootSources.ToArray())
        {
            if (!source.IsSearchable || !IsInstanceValid(source.LootNode)
                || source.LootNode.GlobalPosition.DistanceTo(origin) > range)
            {
                continue;
            }
            var marker = BuildRelayLootMarker();
            _levelRoot.AddChild(marker);
            marker.GlobalPosition = source.LootNode.GlobalPosition + Vector3.Up * 1.35f;
            _relayLootMarkers.Add(marker);
            RemoveRelayLootMarkerAfterDelay(marker);
            revealed++;
        }
        return revealed;
    }

    private static Node3D BuildRelayLootMarker()
    {
        var root = new Node3D { Name = "RelayLootSignal" };
        var color = new Color(0.24f, 0.88f, 0.72f, 0.82f);
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = color,
            EmissionEnabled = true,
            Emission = new Color(color.R, color.G, color.B),
            EmissionEnergyMultiplier = 2.4f,
            NoDepthTest = true
        };
        root.AddChild(new MeshInstance3D
        {
            Name = "RelayLootRing",
            Mesh = new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.3f, Rings = 20, RingSegments = 8 },
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0, 0),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        root.AddChild(new Label3D
        {
            Name = "RelayLootLabel",
            Position = Vector3.Up * 0.48f,
            Text = "SUPPLY // SIGNAL",
            FontSize = 14,
            OutlineSize = 6,
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            VisibilityRangeEnd = 54.0f
        });
        return root;
    }

    private async void RemoveRelayLootMarkerAfterDelay(Node3D marker)
    {
        await ToSignal(GetTree().CreateTimer(12.0f), SceneTreeTimer.SignalName.Timeout);
        _relayLootMarkers.Remove(marker);
        if (IsInstanceValid(marker))
        {
            marker.QueueFree();
        }
    }

    private async void ValidateRelayStations()
    {
        DisableActorsForSurvivalDiagnostics();
        await WaitFrames(4);
        var expected = ResidentialTowerSpecs.Length * 4;
        var grouped = GetTree().GetNodesInGroup("residential_relay_stations");
        var kinds = _residentialRelayStations.Select(station => station.Kind).Distinct().Count();
        var structureReady = _residentialRelayStations.All(station => IsInstanceValid(station)
            && station.CollisionLayer == 1
            && station.LadderRungCount >= 8
            && station.HasRoofCollision);
        var first = _residentialRelayStations[0];
        var rise = first.RoofLandingPoint.Y - first.LadderApproachPoint.Y;

        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.GlobalPosition = first.LadderApproachPoint;
        var climbStartY = _player.GlobalPosition.Y;
        BeginRelayClimb(first, descend: false);
        for (var step = 0; step < 80 && _relayClimbStation is not null; step++)
        {
            UpdateRelayClimb(1.0f / 60.0f);
        }
        await WaitFrames(1);
        var climbed = _relayClimbStation is null
            && _player.GlobalPosition.Y >= first.RoofLandingPoint.Y - 0.08f
            && _player.GlobalPosition.Y - climbStartY > 2.0f;

        var diagnosticLoot = new WeaponCase
        {
            Name = "DiagnosticRelayLoot"
        };
        diagnosticLoot.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Medical,
            MedicalKind = MedicalItemKind.FieldMedkit,
            Quantity = 1,
            Grade = LootGrade.Rare
        });
        AddChild(diagnosticLoot);
        diagnosticLoot.GlobalPosition = first.TerminalApproachPoint + Vector3.Right * 2.2f;
        _lootSources.Add(diagnosticLoot);

        var scanTarget = _enemies.FirstOrDefault(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        if (scanTarget is not null)
        {
            scanTarget.GlobalPosition = first.GlobalPosition + Vector3.Right * 8.0f;
            scanTarget.ProcessMode = ProcessModeEnum.Disabled;
        }

        var interruptedStation = _residentialRelayStations[1];
        _player.GlobalPosition = interruptedStation.TerminalApproachPoint;
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        var (terminalTarget, terminalMode) = FindRelayInteraction();
        var terminalTargeted = ReferenceEquals(terminalTarget, interruptedStation)
            && terminalMode == RelayInteractionMode.Activate;
        _relayInteractionTarget = interruptedStation;
        _relayInteractionMode = RelayInteractionMode.Activate;
        _relayInteractionProgress = 0.0f;
        AdvanceRelayStationActivation(interruptedStation, 0.5f, held: true, notifyDamage: false);
        var activationStarted = interruptedStation.ActivationProgress > 0.08f;
        var interruptionCountBefore = _relayActivationInterruptedCount;
        _player.SetHealthForDiagnostics(_player.Health - 18.0f);
        AdvanceRelayStationActivation(interruptedStation, 1.0f / 60.0f, held: true, notifyDamage: false);
        var damageInterrupted = activationStarted
            && !interruptedStation.IsActivated
            && interruptedStation.ActivationProgress <= 0.001f
            && _relayActivationInterruptedCount == interruptionCountBefore + 1;

        _player.GlobalPosition = first.TerminalApproachPoint;
        _player.SetHealthForDiagnostics(_player.MaxHealth);
        _relayInteractionTarget = first;
        _relayInteractionMode = RelayInteractionMode.Activate;
        _relayInteractionProgress = 0.0f;
        for (var step = 0; step < 220 && !first.IsActivated; step++)
        {
            AdvanceRelayStationActivation(first, 1.0f / 60.0f, held: true, notifyDamage: false);
        }
        await WaitFrames(1);
        var activated = first.IsActivated && _relayActivationCount == 1;
        var scanned = scanTarget is not null && scanTarget.IsScanned && _relayLastEnemyRevealCount >= 1;
        var lootMarked = _relayLastLootRevealCount >= 1
            && _relayLootMarkers.Any(marker => IsInstanceValid(marker));
        var cacheCount = _relayCaches.Count;
        var cache = _relayCaches.FirstOrDefault(candidate => IsInstanceValid(candidate)
            && candidate.TowerIndex == first.TowerIndex
            && candidate.FloorIndex == -first.CornerIndex - 1);
        var rewardReady = first.CacheUnlocked
            && cache is not null
            && _lootSources.Contains(cache)
            && cache.Loot.Count >= 3
            && cache.GlobalPosition.DistanceTo(first.RoofLandingPoint) < 2.85f;
        var repeatRejected = !CompleteRelayStationActivation(first)
            && _relayCaches.Count == cacheCount
            && _relayActivationCount == 1;

        var truckLaneStart = new Vector3(-0.5f, 0.0f, -11.5f);
        var truckLaneEnd = new Vector3(-0.5f, 0.0f, -34.0f);
        var truckLaneClear = _residentialRelayStations.All(station =>
            DistanceToHorizontalSegment(station.GlobalPosition, truckLaneStart, truckLaneEnd) > 3.2f);
        var valid = _residentialRelayStations.Count == expected
            && grouped.Count == expected
            && kinds == Enum.GetValues<ResidentialRelayKind>().Length
            && structureReady
            && rise > 2.0f
            && climbed
            && terminalTargeted
            && damageInterrupted
            && activated
            && scanned
            && lootMarked
            && rewardReady
            && repeatRejected
            && truckLaneClear;
        GD.Print($"RELAY_STATION_CHECK valid={valid} stations={_residentialRelayStations.Count}/{expected} grouped={grouped.Count} kinds={kinds} structure={structureReady} rungs={first.LadderRungCount} rise={rise:0.00} climbed={climbed} terminal_targeted={terminalTargeted} activation_started={activationStarted} damage_interrupted={damageInterrupted} activated={activated} enemies={_relayLastEnemyRevealCount} loot_marks={_relayLastLootRevealCount} reward={rewardReady} cache_once={_relayCaches.Count == 1} repeat_rejected={repeatRejected} truck_lane={truckLaneClear}");
        GD.Print($"RELAY_STATION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static float DistanceToHorizontalSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        var p = new Vector2(point.X, point.Z);
        var a = new Vector2(start.X, start.Z);
        var b = new Vector2(end.X, end.Z);
        var segment = b - a;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.001f)
        {
            return p.DistanceTo(a);
        }
        var t = Mathf.Clamp((p - a).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        return p.DistanceTo(a + segment * t);
    }

    private async void CaptureRelayStation()
    {
        DisableActorsForSurvivalDiagnostics();
        foreach (var civilian in _civilians)
        {
            if (IsInstanceValid(civilian))
            {
                civilian.GlobalPosition = new Vector3(0, -40.0f, 0);
            }
        }
        var interactionStation = _residentialRelayStations[1];
        var interactionOutward = interactionStation.TerminalApproachPoint - interactionStation.GlobalPosition;
        interactionOutward.Y = 0.0f;
        interactionOutward = interactionOutward.Normalized();
        _languageSetting = "zh";
        _hud.SetLanguage("zh");
        _player.GlobalPosition = interactionStation.TerminalApproachPoint + interactionOutward * 1.35f;
        _player.FaceWorldPointForDiagnostics(interactionStation.GlobalPosition + Vector3.Up * 0.45f);
        _interactReleaseRequired = false;
        await WaitFrames(5);
        SaveViewportImage("res://relay_station_interaction_validation.png");

        var station = _residentialRelayStations[0];
        CompleteRelayStationActivation(station);
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Visible = false;
        _hud.Visible = false;
        var outward = station.TerminalApproachPoint - station.GlobalPosition;
        outward.Y = 0.0f;
        outward = outward.Normalized();
        var camera = new Camera3D
        {
            Name = "RelayStationValidationCamera",
            Fov = 63.0f,
            Near = 0.05f,
            Far = 180.0f
        };
        AddChild(camera);
        camera.GlobalPosition = station.GlobalPosition + outward * 6.2f + Vector3.Up * 1.65f;
        camera.LookAt(station.GlobalPosition + Vector3.Up * 0.75f, Vector3.Up);
        camera.MakeCurrent();
        await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
        await WaitFrames(2);
        SaveViewportImage("res://relay_station_validation.png");
        GD.Print($"RELAY_STATION_CAPTURE stations={_residentialRelayStations.Count} cache={station.CacheUnlocked} paths=relay_station_interaction_validation.png,relay_station_validation.png");
        GetTree().Quit();
    }
}
