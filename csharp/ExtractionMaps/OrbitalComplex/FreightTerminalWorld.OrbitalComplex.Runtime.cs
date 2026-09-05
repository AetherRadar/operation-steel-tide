using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Compatibility adapter for MAP 03.  The map definition owns deterministic gameplay
/// coordinates and the world assembler owns authored art/collision; this partial only
/// connects those two services to the existing extraction-world facade while MAP 03 is
/// migrated out of FreightTerminalWorld.
///
/// Temporary size exception: this adapter stays cohesive while the map is migrated and
/// currently exceeds the 800-line guidance because it carries assembly, content, and
/// encounter hand-off seams together.  Follow-up extraction: move the loot and enemy
/// hand-off methods (SpawnOrbitalComplexRuntime* below) into a dedicated
/// FreightTerminalWorld.OrbitalComplex.Content.cs partial once the migration lands.
/// </summary>
public partial class FreightTerminalWorld
{
    private const ulong OrbitalComplexRuntimeFallbackSeed = 0x4F52424954414C03UL;

    private readonly OrbitalComplexWorldAssembler _orbitalComplexRuntimeAssembler = new();
    private OrbitalComplexMapLayout? _orbitalComplexRuntimeLayout;
    private OrbitalComplexWorldBuildResult? _orbitalComplexRuntimeBuild;
    private Node3D? _orbitalComplexRuntimeExtractionSite;
    private string? _orbitalComplexRuntimeLoadError;
    private bool _orbitalComplexRuntimeReady;
    private bool _orbitalComplexRuntimeWeaponCasesSpawned;
    private bool _orbitalComplexRuntimeGradedLootSpawned;
    private bool _orbitalComplexRuntimeValuablesSpawned;
    private bool _orbitalComplexRuntimeExplosivesSpawned;
    private bool _orbitalComplexRuntimeEnemiesSpawned;
    private bool _orbitalComplexRuntimeHostilesSpawned;
    private int _orbitalComplexRuntimeTraversalLinkCount;
    private int _orbitalComplexRuntimeQrfStage;
    private readonly List<OmniLight3D> _orbitalComplexRuntimeLights = new();
    private bool _orbitalComplexRuntimeLightingBuilt;
    private float _orbitalComplexRuntimePresentationTime;

    private bool IsOrbitalComplexRuntimeMapSelected
        => string.Equals(
            _activeRuntimeMapId,
            DeploymentMapCatalog.OrbitalComplexId,
            StringComparison.OrdinalIgnoreCase);

    private ulong OrbitalComplexRuntimeSeed
    {
        get
        {
            var seed = DeploymentMapRuntime.CurrentWorldSeed;
            return seed == 0
                ? OrbitalComplexRuntimeFallbackSeed
                : unchecked((ulong)seed);
        }
    }

    private OrbitalComplexMapLayout OrbitalComplexRuntimeLayout
        => _orbitalComplexRuntimeLayout ??= OrbitalComplexMapDefinition.Build(
            OrbitalComplexRuntimeSeed);

    private Vector3 OrbitalComplexRuntimeExtractionPosition
        => OrbitalComplexRuntimeLayout.Extraction.Position;

    private float OrbitalComplexRuntimeExtractionRadius
        => Mathf.Max(1.0f, OrbitalComplexRuntimeLayout.Extraction.Radius);

    private IReadOnlyList<Vector3> OrbitalComplexRuntimeBossRoute
        => OrbitalComplexRuntimeLayout.BossRoute;

    private bool OrbitalComplexRuntimeSceneReady
        => _orbitalComplexRuntimeReady
            && _orbitalComplexRuntimeBuild is not null
            && IsInstanceValid(_orbitalComplexRuntimeBuild.AuthoredArtRoot)
            && IsInstanceValid(_orbitalComplexRuntimeBuild.GameplayRoot);

    /// <summary>Read-only projection used by core-world call sites and diagnostics.</summary>
    internal bool IsOrbitalComplexRuntimeReady => OrbitalComplexRuntimeSceneReady;

    internal OrbitalComplexWorldBuildResult? OrbitalComplexRuntimeBuild
        => _orbitalComplexRuntimeBuild;

    internal Vector3 OrbitalComplexRuntimeExtractionPoint
        => OrbitalComplexRuntimeExtractionPosition;

    internal float OrbitalComplexRuntimeExtractionZoneRadius
        => OrbitalComplexRuntimeExtractionRadius;

    internal float OrbitalComplexRuntimePlayerYaw
    {
        get
        {
            var pads = OrbitalComplexRuntimeLayout.PlayerSpawnPads;
            if (pads.Count == 0)
            {
                return 0.0f;
            }
            var direction = pads[0].LookTarget - pads[0].Position;
            return direction.LengthSquared() <= 0.001f
                ? 0.0f
                : Mathf.Atan2(-direction.X, -direction.Z);
        }
    }

    /// <summary>
    /// Builds MAP 03 under the supplied FreightTerminalWorld root.  Return false for other
    /// maps so the caller can continue with its existing map branch.  In diagnostic mode a
    /// missing authored scene is recorded and a gameplay-only shell is retained so the
    /// loading diagnostic can report a machine-readable failure instead of crashing first.
    /// </summary>
    private bool TryBuildOrbitalComplexRuntimeLevel()
    {
        if (!IsOrbitalComplexRuntimeMapSelected)
        {
            return false;
        }

        var layout = OrbitalComplexRuntimeLayout;
        var validation = OrbitalComplexLayoutValidator.Validate(layout);
        if (!validation.Valid)
        {
            var validationError =
                "MAP 03 gameplay layout failed validation: " + validation.MachineSummary;
            if (!_diagnosticSceneLoadFallbackAllowed)
            {
                throw new InvalidOperationException(validationError);
            }
            // Keep the world alive long enough for the explicit MAP 03 diagnostic to
            // print its machine-readable failure and exit cleanly.  Normal play still
            // fails fast above, while diagnostic mode avoids a null-world error loop.
            _orbitalComplexRuntimeLoadError = validationError;
            GD.PrintErr($"ORBITAL_LAYOUT_ERROR {validation.MachineSummary}");
        }

        _levelRoot = new Node3D { Name = "FalltideRecoveryArray" };
        _levelRoot.SetMeta("map_id", OrbitalComplexMapDefinition.MapId);
        _levelRoot.SetMeta("map_display_name", "FALLTIDE RECOVERY ARRAY");
        _levelRoot.SetMeta("map_indoor", true);
        _levelRoot.SetMeta("map_width_m", OrbitalComplexMapDefinition.WidthMeters);
        _levelRoot.SetMeta("map_depth_m", OrbitalComplexMapDefinition.DepthMeters);
        // The south pressure shell has a DCC-authored tidal observation gap.
        // Keep the sea outside the playable boundary so it reads as an actual
        // exterior world through the opening without covering the lower pit.
        _levelRoot.AddChild(OceanBackdropFactory.Create(
            new Vector3(0.0f, -15.68f, 142.0f), new Vector2(280.0f, 84.0f)));
        _levelRoot.AddChild(OceanBackdropFactory.Create(
            new Vector3(0.0f, -15.68f, -262.0f), new Vector2(280.0f, 84.0f)));
        _levelRoot.SetMeta("falltide_exterior_world", "tidal_ocean_and_open_sky");
        AddChild(_levelRoot);

        if (validation.Valid)
        {
            _orbitalComplexRuntimeLoadError = null;
        }
        _orbitalComplexRuntimeBuild = null;
        _orbitalComplexRuntimeReady = false;
        var coreBuildSucceeded = false;
        try
        {
            _orbitalComplexRuntimeBuild = _orbitalComplexRuntimeAssembler.Build(
                _levelRoot,
                layout,
                _objectiveStage,
                layout.SharedWorldSeed);
            _levelRoot.SetMeta("authored_scene_path", OrbitalComplexWorldAssembler.DefaultScenePath);
            _levelRoot.SetMeta("authored_scene_group", OrbitalComplexWorldAssembler.AuthoredSceneGroup);
            if (!BuildOrbitalComplexRuntimeWaterPresentation())
            {
                throw new InvalidOperationException("MAP 03 authored blackwater surface is missing");
            }
            BuildOrbitalComplexRuntimeLighting();
            BuildOrbitalComplexRuntimeAccess();
            BuildOrbitalComplexRuntimePresentationFX();
            // A diagnostic fallback may continue after a failed data-contract
            // validation, but that shell is not a ready runtime even if assembly
            // itself happened to succeed.
            coreBuildSucceeded = validation.Valid;
        }
        catch (Exception exception) when (_diagnosticSceneLoadFallbackAllowed)
        {
            _orbitalComplexRuntimeReady = false;
            _orbitalComplexRuntimeLoadError = string.IsNullOrEmpty(
                _orbitalComplexRuntimeLoadError)
                ? exception.Message
                : $"{_orbitalComplexRuntimeLoadError};{exception.Message}";
            GD.PrintErr($"ORBITAL_AUTHORED_SCENE_ERROR {_orbitalComplexRuntimeLoadError}");
        }

        // The optional helper builders use OrbitalComplexRuntimeSceneReady as their
        // prerequisite.  Expose the successfully assembled core only while this
        // synchronous helper phase runs; the final assignment below remains the
        // authoritative all-helpers-complete result.
        _orbitalComplexRuntimeReady = coreBuildSucceeded;
        try
        {
            BuildOrbitalComplexRuntimeObjectiveTerminals(layout);
            BuildOrbitalComplexRuntimeExtraction(layout);
            BuildOrbitalComplexRuntimeBleedValve(layout);
            BuildOrbitalComplexRuntimeUndertowSump(layout);
            BuildOrbitalComplexTacticalSignals();
            RegisterOrbitalComplexRuntimeTraversal(layout);
            ApplyOrbitalComplexRuntimeObjectiveStage(_objectiveStage);
            _orbitalComplexRuntimeReady = coreBuildSucceeded;
        }
        catch (Exception exception) when (_diagnosticSceneLoadFallbackAllowed)
        {
            _orbitalComplexRuntimeReady = false;
            _orbitalComplexRuntimeLoadError = string.IsNullOrEmpty(
                _orbitalComplexRuntimeLoadError)
                ? exception.Message
                : $"{_orbitalComplexRuntimeLoadError};{exception.Message}";
            GD.PrintErr($"ORBITAL_RUNTIME_HELPER_ERROR {_orbitalComplexRuntimeLoadError}");
        }
        return true;
    }

    /// <summary>Convenience wrapper for callers that already selected MAP 03.</summary>
    private void BuildOrbitalComplexRuntimeLevel()
    {
        if (!TryBuildOrbitalComplexRuntimeLevel())
        {
            throw new InvalidOperationException("MAP 03 runtime was requested for another map.");
        }
    }

    private void BuildOrbitalComplexRuntimeObjectiveTerminals(
        OrbitalComplexMapLayout layout)
    {
        for (var index = 0; index < layout.Objectives.Count; index++)
        {
            var objective = layout.Objectives[index];
            var relay = objective.CompletionSignal.Contains(
                "breaker",
                StringComparison.OrdinalIgnoreCase);
            BuildObjectiveTerminal(
                $"OrbitalObjective_{index + 1:00}_{objective.Id}",
                objective.Position,
                objective.YawRadians,
                relay,
                authoredCollisionSize: new Vector3(0.92f, 2.1f, 0.74f));

            var authoredAnchor = FindOrbitalComplexAuthoredNode(
                _orbitalComplexRuntimeBuild?.AuthoredArtRoot,
                ObjectiveAnchorNames(objective));
            authoredAnchor?.SetMeta("falltide_objective_id", objective.Id);
        }
    }

    private static string[] ObjectiveAnchorNames(OrbitalComplexObjectiveDefinition objective)
        => objective.District switch
        {
            "breaker_yard" => new[]
            {
                "POI_BreakerYard", "Map03Objective_Core", "Map03Objective_Relay"
            },
            "quarantine_archive" => new[]
            {
                "POI_QuarantineArchive", "Map03Objective_Core", "Map03Objective_Relay"
            },
            _ => new[] { "POI_TelemetryDish", "Map03Objective_Core" }
        };

    private static Node3D? FindOrbitalComplexAuthoredNode(
        Node3D? root,
        IReadOnlyList<string> names)
    {
        if (root is null || !IsInstanceValid(root))
        {
            return null;
        }
        foreach (var name in names)
        {
            if (root.FindChild(name, recursive: true, owned: false) is Node3D node)
            {
                return node;
            }
        }
        return null;
    }

    private void BuildOrbitalComplexRuntimeExtraction(OrbitalComplexMapLayout layout)
    {
        var extraction = layout.Extraction;
        var site = new Node3D
        {
            Name = "OrbitalComplexExtractionSite",
            Position = extraction.Position
        };
        site.AddToGroup("orbital_complex_extraction");
        site.SetMeta("falltide_extraction_id", extraction.Id);
        site.SetMeta("falltide_extraction_radius", extraction.Radius);
        _levelRoot.AddChild(site);
        _orbitalComplexRuntimeExtractionSite = site;

        _extractionArea = new Area3D
        {
            Name = "OrbitalComplexExtractionZone",
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true
        };
        _extractionArea.AddToGroup("orbital_complex_extraction");
        _extractionArea.AddChild(new CollisionShape3D
        {
            Name = "OrbitalComplexExtractionVolume",
            Shape = new CylinderShape3D
            {
                Radius = OrbitalComplexRuntimeExtractionRadius,
                Height = 3.6f
            }
        });
        _extractionArea.BodyEntered += OnExtractionEntered;
        _extractionArea.BodyExited += OnExtractionExited;
        site.AddChild(_extractionArea);

        // The beacon is a gameplay readability marker; all major visible architecture remains
        // in the authored GLB.  Keep the marker separate so stage presentation can dim it.
        _extractionMarker = new Node3D
        {
            Name = "ActiveOrbitalExtractionBeacon",
            Position = Vector3.Up * 0.36f
        };
        _extractionMarker.AddToGroup("orbital_complex_extraction");
        var markerMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.08f, 0.92f, 0.72f, 0.25f),
            EmissionEnabled = true,
            Emission = new Color(0.08f, 0.92f, 0.72f),
            EmissionEnergyMultiplier = 2.8f
        };
        foreach (var radius in new[]
                 {
                     OrbitalComplexRuntimeExtractionRadius * 0.72f,
                     OrbitalComplexRuntimeExtractionRadius * 0.9f
                 })
        {
            _extractionMarker.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh
                {
                    InnerRadius = Mathf.Max(0.2f, radius - 0.045f),
                    OuterRadius = radius,
                    Rings = 48,
                    RingSegments = 8
                },
                MaterialOverride = markerMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        _extractionMarker.AddChild(new OmniLight3D
        {
            Name = "OrbitalExtractionBeaconLight",
            Position = Vector3.Up * 1.8f,
            LightColor = new Color(0.08f, 1.0f, 0.72f),
            LightEnergy = 2.2f,
            OmniRange = Mathf.Max(5.0f, extraction.Radius * 1.8f),
            ShadowEnabled = false
        });
        site.AddChild(_extractionMarker);

        var authoredExtraction = FindOrbitalComplexAuthoredNode(
            _orbitalComplexRuntimeBuild?.AuthoredArtRoot,
            new[]
            {
                "Extraction_TideGate", "Map03Extraction", "Extraction_MaintenanceSkiff"
            });
        authoredExtraction?.SetMeta("falltide_extraction_anchor", true);
        authoredExtraction?.SetMeta("falltide_extraction_radius", extraction.Radius);
    }

    private void RegisterOrbitalComplexRuntimeTraversal(OrbitalComplexMapLayout layout)
    {
        _orbitalComplexRuntimeTraversalLinkCount = 0;
        var coverPointCount = 0;
        foreach (var route in layout.PatrolRoutes)
        {
            var kind = route.Layer == OrbitalComplexVerticalLayer.Catwalk
                ? SquadTraversalKind.Step
                : SquadTraversalKind.Walk;
            if (RegisterSquadTraversalLink(
                    $"orbital_complex:{route.Id}",
                    kind,
                    bidirectional: true,
                    route.Waypoints,
                    costMultiplier: route.Layer == OrbitalComplexVerticalLayer.Catwalk
                        ? 1.12f
                        : 1.0f) >= 0)
            {
                _orbitalComplexRuntimeTraversalLinkCount++;
            }
        }
        foreach (var ramp in layout.Ramps)
        {
            if (RegisterSquadTraversalLink(
                    $"orbital_complex:ramp:{ramp.Id}",
                    SquadTraversalKind.Step,
                    bidirectional: true,
                    new[] { ramp.LowApproach, ramp.HighApproach },
                    costMultiplier: 1.08f) >= 0)
            {
                _orbitalComplexRuntimeTraversalLinkCount++;
            }
        }

        foreach (var point in layout.CoverPoints)
        {
            if (!InsideOrbitalComplexRuntimeBounds(layout.Bounds, point)
                || _registeredCoverPoints.Any(existing =>
                    existing.DistanceSquaredTo(point) < 1.0f))
            {
                continue;
            }
            // Unlike RegisterCoverPoint, preserve the authored vertical layer.  This list is
            // consumed by FindCoverPoint and therefore supports dry-dock/catwalk cover too.
            _registeredCoverPoints.Add(point);
            coverPointCount++;
        }
        _levelRoot.SetMeta("falltide_runtime_cover_point_count", coverPointCount);
    }

    private static bool InsideOrbitalComplexRuntimeBounds(
        OrbitalComplexMapBounds bounds,
        Vector3 point)
    {
        var minimum = bounds.Horizontal.Position;
        var maximum = bounds.Horizontal.End;
        return point.X >= minimum.X - 0.01f
            && point.X <= maximum.X + 0.01f
            && point.Z >= minimum.Y - 0.01f
            && point.Z <= maximum.Y + 0.01f
            && point.Y >= bounds.MinimumY - 0.01f
            && point.Y <= bounds.MaximumY + 0.01f;
    }

    private void ConfigureOrbitalComplexRuntimeSpawnSelection()
    {
        if (!IsOrbitalComplexRuntimeMapSelected)
        {
            return;
        }
        var layout = OrbitalComplexRuntimeLayout;
        if (layout.PlayerSpawnPads.Count == 0)
        {
            throw new InvalidOperationException("MAP 03 has no player deployment pads.");
        }

        var diagnostic = Array.Exists(
            OS.GetCmdlineUserArgs(),
            argument => argument.StartsWith("--validate-orbital-", StringComparison.Ordinal)
                || argument == "--capture-orbital-map"
                || argument == "--capture-orbital-coast");
        var playerIndex = diagnostic
            ? 0
            : (int)(_rng.Randi() % (uint)layout.PlayerSpawnPads.Count);
        DeploymentPoint = layout.PlayerSpawnPads[playerIndex].Position;
        _assignedHostilePads = layout.RivalSpawnPads
            .Select(pad => pad.Position)
            .ToList();
    }

    private void ConfigureOrbitalComplexRuntimeMinimap()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || !IsInstanceValid(_hud))
        {
            return;
        }
        var layout = OrbitalComplexRuntimeLayout;
        var landmarks = new List<TacticalMapLandmark>
        {
            new(DeploymentPoint, "minimap_deploy", "DEPLOY", new Color(0.36f, 0.82f, 1.0f)),
            new(
                layout.Extraction.Position,
                "minimap_extract",
                layout.Extraction.EnglishName,
                new Color(0.28f, 1.0f, 0.7f))
        };
        landmarks.AddRange(layout.MinimapLandmarks.Select(landmark => new TacticalMapLandmark(
            landmark.Position,
            landmark.LocalizationKey,
            landmark.EnglishName,
            landmark.Color)));
        var bounds = layout.Bounds.Horizontal;
        _hud.ConfigureMinimap(
            new Rect2(bounds.Position.X, bounds.Position.Y, bounds.Size.X, bounds.Size.Y),
            landmarks);
        if (IsInstanceValid(_player))
        {
            _hud.SetMinimapPlayer(_player.GlobalPosition, _player.Rotation.Y);
        }
    }

    private void ConfigureOrbitalComplexRuntimeMission()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || !IsInstanceValid(_missionDirector))
        {
            return;
        }
        var objectives = OrbitalComplexRuntimeLayout.Objectives
            .Select(objective => objective.EnglishName)
            .ToArray();
        var objectiveIds = OrbitalComplexRuntimeLayout.Objectives
            .Select(objective => objective.Id)
            .ToArray();
        var objectiveLocalizationKeys = OrbitalComplexRuntimeLayout.Objectives
            .Select(objective => objective.LocalizationKey)
            .ToArray();
        _missionDirector.ConfigureMission(
            MissionDirector.FalltideBackendMissionId,
            objectives,
            objectiveIds,
            objectiveLocalizationKeys);
    }

    /// <summary>Returns the interaction prompt for the objective selected by the shared seed.</summary>
    private string OrbitalComplexObjectiveInteractionLabel(int objectiveStage)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || objectiveStage < 0
            || objectiveStage >= OrbitalComplexRuntimeLayout.Objectives.Count)
        {
            return string.Empty;
        }

        var objective = OrbitalComplexRuntimeLayout.Objectives[objectiveStage];
        if (IsOrbitalComplexBreakerObjective(objective))
        {
            return GameLocalization.Get(
                "stabilize_breakers",
                _languageSetting,
                "STABILIZE STORM-GRID BREAKERS");
        }
        if (IsOrbitalComplexArchiveObjective(objective))
        {
            return GameLocalization.Get(
                "authorize_quarantine",
                _languageSetting,
                "AUTHORIZE QUARANTINE RELEASE");
        }
        return GameLocalization.Objective(objective.EnglishName, _languageSetting);
    }

    private bool IsOrbitalComplexBreakerObjective(int objectiveStage)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || objectiveStage < 0
            || objectiveStage >= OrbitalComplexRuntimeLayout.Objectives.Count)
        {
            return false;
        }

        return IsOrbitalComplexBreakerObjective(
            OrbitalComplexRuntimeLayout.Objectives[objectiveStage]);
    }

    private static bool IsOrbitalComplexBreakerObjective(
        OrbitalComplexObjectiveDefinition objective)
        => objective.Id == OrbitalComplexMapDefinition.BreakerObjectiveId
            || string.Equals(
                objective.CompletionSignal,
                "falltide_breaker_bus_rerouted",
                StringComparison.Ordinal);

    private static bool IsOrbitalComplexArchiveObjective(
        OrbitalComplexObjectiveDefinition objective)
        => objective.Id == OrbitalComplexMapDefinition.QuarantineObjectiveId
            || string.Equals(
                objective.CompletionSignal,
                "falltide_archive_purged",
                StringComparison.Ordinal);

    private bool IsOrbitalComplexArchiveObjective(int objectiveStage)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || objectiveStage < 0
            || objectiveStage >= OrbitalComplexRuntimeLayout.Objectives.Count)
        {
            return false;
        }

        return IsOrbitalComplexArchiveObjective(
            OrbitalComplexRuntimeLayout.Objectives[objectiveStage]);
    }

    private void ShowOrbitalComplexObjectiveCompletion(int objectiveStage)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || objectiveStage < 0
            || objectiveStage >= OrbitalComplexRuntimeLayout.Objectives.Count
            || !IsInstanceValid(_hud))
        {
            return;
        }

        var objective = OrbitalComplexRuntimeLayout.Objectives[objectiveStage];
        if (IsOrbitalComplexBreakerObjective(objective))
        {
            _hud.ShowLocalizedMessage(
                "falltide_breakers_stabilized",
                "STORM-GRID BREAKERS STABILIZED  //  RESPONSE DELAYED",
                new Color(0.35f, 0.92f, 0.72f));
        }
        else if (IsOrbitalComplexArchiveObjective(objective))
        {
            _hud.ShowLocalizedMessage(
                "falltide_quarantine_authorized",
                "QUARANTINE RELEASE AUTHORIZED  //  ARCHIVE ACCESS GRANTED",
                new Color(0.42f, 0.78f, 1.0f));
        }
    }

    private void ApplyOrbitalComplexRuntimeObjectiveStage(int stage)
    {
        if (!IsOrbitalComplexRuntimeMapSelected)
        {
            return;
        }
        var clampedStage = Mathf.Clamp(
            stage,
            0,
            OrbitalComplexPowerRules.MaximumObjectiveStage);
        var power = _orbitalComplexRuntimeBuild?.ApplyObjectiveStage(
            clampedStage,
            OrbitalComplexRuntimeLayout.SharedWorldSeed);
        _levelRoot?.SetMeta("falltide_objective_stage", clampedStage);
        _levelRoot?.SetMeta("falltide_extraction_enabled", power?.ExtractionEnabled ?? false);
        _levelRoot?.SetMeta("falltide_power_mode", power is null ? -1 : (int)power.Mode);
        if (IsInstanceValid(_extractionMarker))
        {
            _extractionMarker.Visible = true;
            _extractionMarker.SetMeta("falltide_extraction_enabled", power?.ExtractionEnabled ?? false);
            _extractionMarker.SetMeta(
                "falltide_hold_seconds",
                power?.ExtractionHoldSeconds ?? 0.0f);
            var pulse = power?.ExtractionEnabled == true ? 1.16f : 0.92f;
            _extractionMarker.Scale = Vector3.One * pulse;
        }
    }

    private void ApplyOrbitalComplexRuntimeAtmosphere(DeploymentTimeOfDay timeOfDay)
    {
        if (!IsOrbitalComplexRuntimeMapSelected || !IsInstanceValid(_environmentRef))
        {
            return;
        }

        // The open pressure courts and south-facing shaft must show the real
        // outside world. Keep the authored time-of-day sky instead of a flat
        // black background that makes every opening read like a sealed room.
        var style = TimeOfDayStyles.Style(timeOfDay);
        _environmentRef.BackgroundMode = Godot.Environment.BGMode.Sky;
        _environmentRef.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        _environmentRef.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        _environmentRef.AmbientLightColor = new Color(0.30f, 0.43f, 0.52f);
        _environmentRef.AmbientLightSkyContribution = 0.62f;
        _environmentRef.AmbientLightEnergy = Mathf.Clamp(style.AmbientEnergy * 0.92f, 0.36f, 0.84f);
        _environmentRef.TonemapMode = Godot.Environment.ToneMapper.Aces;
        _environmentRef.TonemapExposure = Mathf.Clamp(style.Exposure + 0.22f, 0.96f, 1.34f);
        _environmentRef.FogEnabled = true;
        _environmentRef.FogLightColor = new Color(0.12f, 0.27f, 0.34f);
        _environmentRef.FogLightEnergy = 0.34f;
        _environmentRef.FogDensity = timeOfDay == DeploymentTimeOfDay.Night
            ? 0.0044f
            : 0.0032f;
        _environmentRef.FogSkyAffect = 0.08f;
        _environmentRef.Sky ??= new Sky();
        _environmentRef.Sky.SkyMaterial = BuildDynamicSkyMaterial(timeOfDay);
        _environmentRef.Sky.ProcessMode = Sky.ProcessModeEnum.Incremental;
        _environmentRef.Sky.RadianceSize = Sky.RadianceSizeEnum.Size128;
        _levelRoot?.SetMeta("falltide_indoor_atmosphere", true);
        _levelRoot?.SetMeta("falltide_open_sky", true);
        _levelRoot?.SetMeta("falltide_time_of_day", (int)timeOfDay);
    }

    private void BuildOrbitalComplexRuntimeLighting()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeLightingBuilt
            || !IsInstanceValid(_levelRoot))
        {
            return;
        }

        // The authored GLB intentionally exports meshes/materials only. These
        // low-cost local fixtures make the enclosed halls readable at player
        // height and let the power-state presentation have a real light source.
        var fixtures = new[]
        {
            ("Intake", new Vector3(0, -8.0f, 72), new Color(0.08f, 0.42f, 0.78f), 8.0f, 42.0f),
            ("Breaker", new Vector3(-100, -8.0f, -6), new Color(1.0f, 0.35f, 0.08f), 7.0f, 36.0f),
            ("Archive", new Vector3(100, -8.0f, -6), new Color(0.30f, 0.36f, 1.0f), 7.0f, 36.0f),
            ("Reactor", new Vector3(0, -10.0f, -34), new Color(1.0f, 0.12f, 0.035f), 10.0f, 48.0f),
            ("Drydock", new Vector3(0, -30.0f, -34), new Color(0.08f, 0.54f, 0.88f), 8.0f, 32.0f),
            ("Vault", new Vector3(0, -8.0f, -52), new Color(0.44f, 0.24f, 1.0f), 6.0f, 28.0f),
            ("TideGate", new Vector3(0, -8.0f, -194), new Color(0.08f, 0.95f, 0.66f), 9.0f, 40.0f),
            ("WestService", new Vector3(-148, -8.0f, -92), new Color(0.08f, 0.42f, 0.78f), 6.0f, 34.0f),
            ("EastService", new Vector3(148, -8.0f, -92), new Color(0.30f, 0.36f, 1.0f), 6.0f, 34.0f),
            ("LaunchSilo", new Vector3(0, -8.0f, -184), new Color(0.08f, 0.65f, 0.92f), 8.0f, 34.0f),
            ("CatwalkWest", new Vector3(-68, -0.5f, -34), new Color(0.10f, 0.48f, 0.92f), 5.0f, 28.0f),
            ("CatwalkEast", new Vector3(68, -0.5f, -34), new Color(0.10f, 0.48f, 0.92f), 5.0f, 28.0f)
        };
        foreach (var fixture in fixtures)
        {
            var light = new OmniLight3D
            {
                Name = $"FalltideRuntimeLight_{fixture.Item1}",
                Position = fixture.Item2,
                LightColor = fixture.Item3,
                LightEnergy = fixture.Item4,
                OmniRange = fixture.Item5,
                ShadowEnabled = false
            };
            light.AddToGroup("orbital_complex_runtime_lighting");
            light.SetMeta("falltide_fixture", fixture.Item1);
            _levelRoot.AddChild(light);
            _orbitalComplexRuntimeLights.Add(light);
        }
        _orbitalComplexRuntimeLightingBuilt = true;
        _levelRoot.SetMeta("falltide_runtime_light_count", _orbitalComplexRuntimeLights.Count);
    }

    private void UpdateOrbitalComplexRuntimePresentation(float delta)
    {
        var build = _orbitalComplexRuntimeBuild;
        if (!OrbitalComplexRuntimeSceneReady || build is null)
        {
            return;
        }

        _orbitalComplexRuntimePresentationTime += Mathf.Max(0.0f, delta);
        UpdateOrbitalComplexBleedValvePresentation(delta);
        UpdateOrbitalComplexUndertowPresentation(delta);
        UpdateOrbitalComplexRuntimePresentationFX(delta);
        UpdateOrbitalComplexTacticalSignals(delta);
        var power = build.PowerState;
        if (build.PresentationNodes.TryGetValue("DishYaw", out var dish))
        {
            var speed = power.Presentation.DishRotationSpeedRadiansPerSecond;
            dish.RotateY(speed * delta);
        }

        foreach (var gate in build.Gates.Values)
        {
            if (gate.AuthoredVisual is not { } visual || !IsInstanceValid(visual))
            {
                continue;
            }
            var fraction = visual.GetMeta("falltide_target_open_fraction", 0.0f).AsSingle();
            if (!visual.HasMeta("falltide_base_position"))
            {
                visual.SetMeta("falltide_base_position", visual.Position);
                visual.SetMeta("falltide_base_rotation", visual.Rotation);
            }
            var basePosition = visual.GetMeta("falltide_base_position").AsVector3();
            var baseRotation = visual.GetMeta("falltide_base_rotation").AsVector3();
            var targetPosition = basePosition;
            var targetRotation = baseRotation;
            if (gate.Definition.Id == "stormglass_vault")
            {
                var direction = visual.Name.ToString().EndsWith("Left", StringComparison.Ordinal) ? -1.0f : 1.0f;
                targetPosition.X += direction * 5.5f * fraction;
            }
            else if (gate.Definition.Id == "upper_catwalk_bypass")
            {
                targetPosition.Y += 5.2f * fraction;
            }
            else
            {
                var direction = visual.Name.ToString().EndsWith("Left", StringComparison.Ordinal) ? -1.0f : 1.0f;
                targetRotation.Y += direction * 1.32f * fraction;
            }
            visual.Position = visual.Position.Lerp(targetPosition, Mathf.Clamp(delta * 5.0f, 0.0f, 1.0f));
            visual.Rotation = visual.Rotation.Lerp(targetRotation, Mathf.Clamp(delta * 5.0f, 0.0f, 1.0f));
        }

        var energyScale = power.Mode == OrbitalComplexPowerMode.Blackout ? 0.42f
            : power.Mode == OrbitalComplexPowerMode.EmergencyPower ? 0.72f : 1.0f;
        var flicker = 0.94f + Mathf.Sin(_orbitalComplexRuntimePresentationTime * 7.0f) * 0.06f;
        foreach (var light in _orbitalComplexRuntimeLights)
        {
            if (IsInstanceValid(light))
            {
                if (!light.HasMeta("falltide_base_energy"))
                {
                    light.SetMeta("falltide_base_energy", light.LightEnergy);
                }
                light.LightEnergy = light.GetMeta("falltide_base_energy").AsSingle()
                    * energyScale * flicker;
            }
        }
    }

    private void SpawnOrbitalComplexRuntimeWeaponCases()
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || _orbitalComplexRuntimeWeaponCasesSpawned)
        {
            return;
        }
        foreach (var placement in OrbitalComplexRuntimeLayout.WeaponCases)
        {
            var weapon = WeaponCatalog.Build(placement.Platform, placement.BuildTier);
            var weaponCase = new WeaponCase
            {
                Name = $"OrbitalWeaponCase_{placement.Id}",
                Position = placement.Position,
                Rotation = new Vector3(0, placement.YawRadians, 0),
                EnglishName = placement.EnglishName,
                ChineseName = placement.ChineseName
            };
            weaponCase.SetMeta("falltide_loot_id", placement.Id);
            weaponCase.SetMeta("falltide_loot_risk", (int)placement.Risk);
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Weapon,
                Weapon = weapon,
                Grade = placement.Grade
            });
            foreach (var attachment in OrbitalComplexAttachmentIds(placement))
            {
                weaponCase.Loot.Add(new LootItem
                {
                    Kind = LootItemKind.Attachment,
                    AttachmentId = attachment,
                    Grade = placement.Grade >= LootGrade.Epic
                        ? LootGrade.Epic
                        : LootGrade.Rare
                });
            }
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.Ammunition,
                AmmoCaliber = WeaponCatalog.Weapon(placement.Platform).Caliber,
                Quantity = placement.Platform is WeaponPlatform.M24 or WeaponPlatform.AWM
                    ? 24 + (int)placement.Grade * 4
                    : 42 + (int)placement.Grade * 12,
                Grade = placement.Grade >= LootGrade.Epic
                    ? LootGrade.Rare
                    : LootGrade.Common
            });
            weaponCase.Loot.Add(new LootItem
            {
                Kind = LootItemKind.ArmorPlate,
                Grade = placement.Grade >= LootGrade.Epic
                    ? LootGrade.Rare
                    : LootGrade.Uncommon
            });
            AddChild(weaponCase);
            _lootSources.Add(weaponCase);
            _lootWorldPoints.Add(placement.Position);
        }
        _orbitalComplexRuntimeWeaponCasesSpawned = true;
    }

    private static IReadOnlyList<string> OrbitalComplexAttachmentIds(
        OrbitalComplexWeaponCasePlacement placement)
        => placement.Platform switch
        {
            WeaponPlatform.P226 => new[] { "optic_micro" },
            WeaponPlatform.M3A1 or WeaponPlatform.MP5A5 => new[] { "optic_micro", "mag_extended" },
            WeaponPlatform.AK74 => new[] { "muzzle_brake", "grip_vertical" },
            WeaponPlatform.M4A1 or WeaponPlatform.ScarL => new[] { "optic_holo", "mag_extended" },
            WeaponPlatform.M24 or WeaponPlatform.AWM => new[] { "optic_scope", "stock_precision" },
            _ => Array.Empty<string>()
        };

    private void SpawnOrbitalComplexRuntimeGradedLoot()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeGradedLootSpawned)
        {
            return;
        }
        var index = 0;
        foreach (var placement in OrbitalComplexRuntimeLayout.GradedLoot)
        {
            var pickup = new GradedLootPickup
            {
                Name = $"OrbitalGradedLoot_{++index:000}_{placement.Id}",
                Position = placement.Position,
                EnglishName = placement.EnglishName,
                ChineseName = placement.ChineseName
            };
            pickup.SetMeta("falltide_loot_id", placement.Id);
            pickup.SetMeta("falltide_loot_risk", (int)placement.Risk);
            pickup.Configure(
                CreateGradedLootItem(
                    placement.Grade,
                    allowHighTierAmmo: placement.Grade >= LootGrade.Epic),
                placement.EnglishName,
                placement.ChineseName);
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
            _buildingLootPickupCount++;
        }
        _orbitalComplexRuntimeGradedLootSpawned = true;
    }

    private void SpawnOrbitalComplexRuntimeValuables()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeValuablesSpawned)
        {
            return;
        }
        var index = 0;
        foreach (var placement in OrbitalComplexRuntimeLayout.Valuables)
        {
            var item = new LootItem
            {
                Kind = LootItemKind.Valuable,
                ValuableKind = placement.Kind,
                Grade = placement.Grade
            };
            var pickup = new GradedLootPickup
            {
                Name = $"OrbitalValuable_{++index:000}_{placement.Id}",
                Position = placement.Position
            };
            pickup.SetMeta("falltide_loot_id", placement.Id);
            pickup.SetMeta("falltide_loot_risk", (int)placement.Risk);
            pickup.Configure(
                item,
                ValuableItems.DisplayName(placement.Kind, "en"),
                ValuableItems.DisplayName(placement.Kind, "zh"));
            AddChild(pickup);
            _lootSources.Add(pickup);
            _lootWorldPoints.Add(placement.Position);
        }
        _orbitalComplexRuntimeValuablesSpawned = true;
    }

    private void SpawnOrbitalComplexRuntimeExplosives()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeExplosivesSpawned)
        {
            return;
        }
        foreach (var placement in OrbitalComplexRuntimeLayout.Explosives)
        {
            var barrel = new ExplosiveBarrel
            {
                Name = $"OrbitalExplosive_{placement.Id}",
                Main = this,
                Position = placement.Position,
                Scale = Vector3.One * Mathf.Clamp(placement.BlastScale, 0.75f, 1.3f)
            };
            barrel.SetMeta("falltide_chain_group", placement.ChainGroup);
            barrel.SetMeta("falltide_blast_scale", placement.BlastScale);
            AddChild(barrel);
            _barrels.Add(barrel);
        }
        _orbitalComplexRuntimeExplosivesSpawned = true;
    }

    private void SpawnOrbitalComplexRuntimeEnemies()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeEnemiesSpawned)
        {
            return;
        }
        var layout = OrbitalComplexRuntimeLayout;
        foreach (var position in layout.GarrisonSpawns)
        {
            var garrison = SpawnEnemy(position, alerted: false, teamId: 0);
            if (OrbitalComplexRuntimePatrolRouteFor(position) is { } route)
            {
                garrison.AssignPatrolRoute(route.Waypoints.ToArray());
            }
        }
        _orbitalComplexRuntimeEnemiesSpawned = true;
        _enemiesRemaining = _enemies.Count;
    }

    private OrbitalComplexPatrolRoute? OrbitalComplexRuntimePatrolRouteFor(Vector3 position)
    {
        var layout = OrbitalComplexRuntimeLayout;
        var bestDistance = float.PositiveInfinity;
        OrbitalComplexPatrolRoute? best = null;
        foreach (var route in layout.PatrolRoutes)
        {
            foreach (var waypoint in route.Waypoints)
            {
                var distance = waypoint.DistanceSquaredTo(position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = route;
                }
            }
        }
        return best;
    }

    private void SpawnOrbitalComplexRuntimeHostileSquads()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeHostilesSpawned)
        {
            return;
        }
        var pads = OrbitalComplexRuntimeLayout.RivalSpawnPads;
        var prefixes = new[] { "ORBIT", "ECHO", "VECTOR", "NOVA" };
        var count = Mathf.Min(pads.Count, ExtractionSpawnPads.HostileSquadTargetCount);
        for (var index = 0; index < count; index++)
        {
            var pad = pads[index];
            var squad = new HostileOperatorSquad
            {
                TeamId = index + 1,
                SpawnPad = pad.Position,
                CallsignPrefix = prefixes[index % prefixes.Length]
            };
            for (var memberIndex = 0; memberIndex < ExtractionSpawnPads.SquadSize; memberIndex++)
            {
                var member = SpawnEnemy(
                    ExtractionSpawnPads.HostileMemberPosition(pad.Position, memberIndex),
                    alerted: false,
                    teamId: squad.TeamId);
                member.Name = $"{squad.CallsignPrefix}_{memberIndex + 1}";
                squad.Members.Add(member);
            }
            _hostileSquads.Add(squad);
        }
        _orbitalComplexRuntimeHostilesSpawned = true;
        _enemiesRemaining = _enemies.Count(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
    }

    private int SpawnOrbitalComplexRuntimeQrf(int objectiveStage)
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || objectiveStage < 1
            || objectiveStage <= _orbitalComplexRuntimeQrfStage)
        {
            return 0;
        }
        var layout = OrbitalComplexRuntimeLayout;
        var midpoint = layout.QrfSpawns.Count / 2;
        var start = _orbitalComplexRuntimeQrfStage == 0
            ? 0
            : midpoint;
        var end = objectiveStage >= 2 ? layout.QrfSpawns.Count : midpoint;
        var spawned = 0;
        for (var index = start; index < end; index++)
        {
            var enemy = SpawnEnemy(layout.QrfSpawns[index], alerted: true, teamId: 0);
            enemy.Name = $"FALLTIDE_QRF_{index + 1:00}";
            if (OrbitalComplexRuntimePatrolRouteFor(layout.QrfSpawns[index]) is { } route)
            {
                enemy.AssignPatrolRoute(route.Waypoints.ToArray());
            }
            spawned++;
        }
        _orbitalComplexRuntimeQrfStage = objectiveStage;
        _enemiesRemaining = _enemies.Count(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
        return spawned;
    }

    private void SpawnOrbitalComplexRuntimeWorldBoss()
    {
        if (!IsOrbitalComplexRuntimeMapSelected
            || IsInstanceValid(_worldBoss)
            || OrbitalComplexRuntimeBossRoute.Count == 0)
        {
            return;
        }
        var route = OrbitalComplexRuntimeBossRoute.ToArray();
        var networkId = _nextEnemyNetworkId++;
        var boss = new EnemyOperator
        {
            Name = "FALLTIDE_TIDE_HUNTER",
            Position = route[0],
            NetworkId = networkId,
            SimulationSeed = ExtractionEntitySeed(networkId),
            Player = _player,
            Main = this,
            MissionDirector = _missionDirector,
            TeamId = EnemyOperator.WorldBossTeamId,
            DetectionRange = 240.0f
        };
        boss.ConfigureWorldBoss(route);
        AddChild(boss);
        boss.Eliminated += OnEnemyEliminated;
        boss.Eliminated += OnWorldBossEliminated;
        _enemies.Add(boss);
        RegisterExtractionNetworkEnemy(boss);
        _worldBoss = boss;
        _worldBossDefeated = false;
        _enemiesRemaining = _enemies.Count(enemy => IsInstanceValid(enemy) && !enemy.IsDead);
    }
}
