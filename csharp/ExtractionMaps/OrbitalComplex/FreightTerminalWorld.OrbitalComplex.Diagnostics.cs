using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

/// <summary>Deterministic MAP 03 diagnostics kept separate from normal runtime assembly.</summary>
public partial class FreightTerminalWorld
{
    private enum OrbitalComplexDiagnosticKind
    {
        Map,
        Collision,
        Gameplay,
        Spawns,
        Loading,
        Atmosphere,
        Water
    }

    private readonly record struct OrbitalComplexDiagnosticSnapshot(
        bool Selected,
        bool LayoutValid,
        bool SceneReady,
        bool ExtractionReady,
        bool ObjectivesReady,
        bool CollisionReady,
        bool RoutesReady,
        bool SpawnsReady,
        bool LoadingReady,
        bool AtmosphereReady,
        bool WaterReady,
        int AuthoredMeshCount,
        int CollisionShapeCount,
        int ObjectiveAnchorCount,
        int TraversalLinkCount,
        string Error)
    {
        public bool For(OrbitalComplexDiagnosticKind kind)
            => kind switch
            {
                OrbitalComplexDiagnosticKind.Map => Selected
                    && LayoutValid
                    && SceneReady
                    && ExtractionReady
                    && ObjectivesReady,
                OrbitalComplexDiagnosticKind.Collision => Selected
                    && LayoutValid
                    && CollisionReady,
                OrbitalComplexDiagnosticKind.Gameplay => Selected
                    && LayoutValid
                    && SceneReady
                    && ExtractionReady
                    && ObjectivesReady
                    && RoutesReady,
                OrbitalComplexDiagnosticKind.Spawns => Selected
                    && LayoutValid
                    && SpawnsReady,
                OrbitalComplexDiagnosticKind.Loading => Selected && LoadingReady,
                OrbitalComplexDiagnosticKind.Atmosphere => Selected
                    && SceneReady
                    && AtmosphereReady,
                OrbitalComplexDiagnosticKind.Water => Selected
                    && SceneReady
                    && WaterReady,
                _ => false
            };
    }

    private async void ValidateOrbitalMap()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Map);

    private async void ValidateOrbitalCollision()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Collision);

    private async void ValidateOrbitalGameplay()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Gameplay);

    private async void ValidateOrbitalSpawns()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Spawns);

    private async void ValidateOrbitalLoading()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Loading);

    private async void ValidateOrbitalAtmosphere()
        => await RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind.Atmosphere);

    private async void ValidateOrbitalWater()
    {
        await WaitFrames(8);
        var snapshot = CaptureOrbitalComplexDiagnosticSnapshot();
        var staticValid = snapshot.For(OrbitalComplexDiagnosticKind.Water);
        var motionValid = false;
        var rise = 0.0f;
        var sink = 0.0f;
        if (staticValid && IsInstanceValid(_player))
        {
            var originalPosition = _player.GlobalPosition;
            _player.ProcessMode = ProcessModeEnum.Pausable;
            _player.GlobalPosition = new Vector3(0.0f, -30.0f, -34.0f);
            _player.PrepareOrbitalComplexSwimmingDiagnostics();
            Input.ActionPress(GameInputActions.Jump);
            await WaitFrames(12);
            Input.ActionRelease(GameInputActions.Jump);
            rise = _player.GlobalPosition.Y;
            _player.Velocity = Vector3.Zero;
            _player.SetOrbitalComplexSwimmingDiagnosticSink(true);
            await WaitFrames(12);
            _player.SetOrbitalComplexSwimmingDiagnosticSink(false);
            sink = _player.GlobalPosition.Y;
            motionValid = rise > -30.0f + 0.06f && sink < rise - 0.06f;
            _player.GlobalPosition = originalPosition;
            _player.Velocity = Vector3.Zero;
        }
        Input.ActionRelease(GameInputActions.Jump);
        Input.ActionRelease(GameInputActions.Crouch);
        var valid = staticValid && motionValid;
        GD.Print(
            $"ORBITAL_WATER_CHECK valid={valid} selected={snapshot.Selected} "
            + $"static={staticValid} swim_rise={rise:0.00} swim_sink={sink:0.00} "
            + $"water={snapshot.WaterReady} error={DiagnosticToken(snapshot.Error)}");
        GD.Print($"ORBITAL_WATER_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async Task RunOrbitalComplexDiagnostic(OrbitalComplexDiagnosticKind kind)
    {
        await WaitFrames(3);
        var snapshot = CaptureOrbitalComplexDiagnosticSnapshot();
        var valid = snapshot.For(kind);
        var label = kind.ToString().ToUpperInvariant();
        GD.Print(
            $"ORBITAL_{label}_CHECK valid={valid} selected={snapshot.Selected} "
            + $"layout={snapshot.LayoutValid} scene={snapshot.SceneReady} "
            + $"loading={snapshot.LoadingReady} extraction={snapshot.ExtractionReady} "
            + $"objectives={snapshot.ObjectivesReady} collision={snapshot.CollisionReady} "
            + $"routes={snapshot.RoutesReady}:{snapshot.TraversalLinkCount} "
            + $"spawns={snapshot.SpawnsReady} atmosphere={snapshot.AtmosphereReady} "
            + $"water={snapshot.WaterReady} "
            + $"meshes={snapshot.AuthoredMeshCount} shapes={snapshot.CollisionShapeCount} "
            + $"anchors={snapshot.ObjectiveAnchorCount} error={DiagnosticToken(snapshot.Error)}");
        GD.Print($"ORBITAL_{label}_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private OrbitalComplexDiagnosticSnapshot CaptureOrbitalComplexDiagnosticSnapshot()
    {
        if (!IsOrbitalComplexRuntimeMapSelected)
        {
            return new OrbitalComplexDiagnosticSnapshot(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                "map_not_selected");
        }

        OrbitalComplexMapLayout layout;
        try
        {
            layout = OrbitalComplexRuntimeLayout;
        }
        catch (Exception exception)
        {
            return new OrbitalComplexDiagnosticSnapshot(
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                exception.Message);
        }

        var layoutValidation = OrbitalComplexLayoutValidator.Validate(layout);
        var build = _orbitalComplexRuntimeBuild;
        var sceneReady = OrbitalComplexRuntimeSceneReady;
        var authoredMeshes = build is null
            ? 0
            : CountOrbitalComplexMeshes(build.AuthoredArtRoot);
        var collisionShapes = build is null
            ? 0
            : CountOrbitalComplexCollisionShapes(build.GameplayRoot, requireShape: true);
        var objectiveAnchors = build?.ObjectiveAnchors.Count ?? 0;
        var objectivesReady = layout.Objectives.Count >= 2
            && objectiveAnchors == layout.Objectives.Count
            && _objectiveTerminals.Count >= layout.Objectives.Count;
        var extractionReady = IsInstanceValid(_extractionArea)
            && IsInstanceValid(_extractionMarker)
            && _orbitalComplexRuntimeExtractionSite is not null
            && IsInstanceValid(_orbitalComplexRuntimeExtractionSite);
        var collisionReady = build is not null
            && build.CollisionShapeCount
                >= layout.CollisionBoxes.Count + layout.Ramps.Count + layout.PowerGates.Count
            && collisionShapes >= build.CollisionShapeCount
            && build.GameplayRoot.IsInGroup(OrbitalComplexWorldAssembler.GameplayCollisionGroup);
        var routesReady = _orbitalComplexRuntimeTraversalLinkCount
                >= layout.PatrolRoutes.Count + layout.Ramps.Count
            && layout.RouteProbes.Count >= 12;
        var spawnsReady = layout.PlayerSpawnPads.Count >= 4
            && layout.RivalSpawnPads.Count >= 4
            && layout.GarrisonSpawns.Count >= 20
            && layout.QrfSpawns.Count >= 6
            && layout.BossRoute.Count >= 12
            && layout.PlayerSpawnPads.Any(pad =>
                pad.Position.DistanceSquaredTo(DeploymentPoint) <= 0.01f)
            && layout.PlayerSpawnPads.All(pad =>
                InsideOrbitalComplexRuntimeBounds(layout.Bounds, pad.Position))
            && layout.RivalSpawnPads.All(pad =>
                InsideOrbitalComplexRuntimeBounds(layout.Bounds, pad.Position));
        var loadingReady = GD.Load<PackedScene>(OrbitalComplexWorldAssembler.DefaultScenePath)
                is not null
            && string.IsNullOrEmpty(_orbitalComplexRuntimeLoadError)
            && sceneReady;
        var atmosphereReady = IsInstanceValid(_environmentRef)
            && _environmentRef.BackgroundMode == Godot.Environment.BGMode.Sky
            && _environmentRef.AmbientLightSource == Godot.Environment.AmbientSource.Sky
            && _environmentRef.Sky?.SkyMaterial is not null
            && (_levelRoot?.GetMeta("falltide_open_sky", false).AsBool() ?? false)
            && (_levelRoot?.GetMeta("falltide_indoor_atmosphere", false).AsBool() ?? false)
            && (build?.PresentationNodes.ContainsKey("PowerZone_Blackout") ?? false)
            && (build?.PresentationNodes.ContainsKey("PowerZone_Powered") ?? false);
        var pool = build?.AuthoredArtRoot.FindChild(
            "BlackwaterPoolSurface*", recursive: true, owned: false) as MeshInstance3D;
        var waterReady = sceneReady
            && (_levelRoot?.GetMeta("falltide_blackwater_ready", false).AsBool() ?? false)
            && (_levelRoot?.GetMeta("falltide_ocean_backdrop_count", 0).AsInt32() ?? 0) >= 2
            && pool?.GetMeta("swimmable_water_surface", false).AsBool() == true
            && pool.MaterialOverride is ShaderMaterial
            && OrbitalComplexMapDefinition.IsInBlackwaterSwimVolume(
                new Vector3(0.0f, -28.0f, -34.0f))
            && !OrbitalComplexMapDefinition.IsInBlackwaterSwimVolume(
                new Vector3(40.0f, -28.0f, -34.0f));

        return new OrbitalComplexDiagnosticSnapshot(
            true,
            layoutValidation.Valid,
            sceneReady,
            extractionReady,
            objectivesReady,
            collisionReady,
            routesReady,
            spawnsReady,
            loadingReady,
            atmosphereReady,
            waterReady,
            authoredMeshes,
            collisionShapes,
            objectiveAnchors,
            _orbitalComplexRuntimeTraversalLinkCount,
            _orbitalComplexRuntimeLoadError ?? string.Empty);
    }

    private static int CountOrbitalComplexMeshes(Node root)
    {
        var count = root is GeometryInstance3D ? 1 : 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Node childNode)
            {
                count += CountOrbitalComplexMeshes(childNode);
            }
        }
        return count;
    }

    private static int CountOrbitalComplexCollisionShapes(Node root, bool requireShape)
    {
        var count = root is CollisionShape3D shape
            && (!requireShape || shape.Shape is not null)
            ? 1
            : 0;
        foreach (var child in root.GetChildren())
        {
            if (child is Node childNode)
            {
                count += CountOrbitalComplexCollisionShapes(childNode, requireShape);
            }
        }
        return count;
    }

    /// <summary>
    /// Exercises the MAP 03 seams that the focused layout diagnostics cannot observe:
    /// deployment selection/HUD, mission objective contract, authored spawn hand-off, and
    /// tide-gate extraction presentation. All checks use the deterministic startup seed.
    /// </summary>
    private async void ValidateOrbitalComplexIntegration()
    {
        await WaitFrames(8);
        var valid = false;
        var failure = string.Empty;
        var selected = false;
        var missionReady = false;
        var objectiveIdsReady = false;
        var objectiveTextReady = false;
        var objectiveLocalizationReady = false;
        var hudReady = false;
        var mapSelectionReady = false;
        var tideGateHudReady = false;
        var minimapReady = false;
        var extractionReady = false;
        var extractionHudReady = false;
        var stageReady = false;
        var weaponCases = 0;
        var gradedLoot = 0;
        var hostileSquads = 0;
        var enemies = 0;
        var explosives = 0;
        var gateStage0Ready = false;
        var gateStage1Ready = false;
        var gateStage2Ready = false;
        var markerStage2Ready = false;
        var runtimeReady = false;
        var authoredRootValid = false;
        var gameplayRootValid = false;

        try
        {
            selected = IsOrbitalComplexRuntimeMapSelected;
            if (!selected)
            {
                throw new InvalidOperationException("map_not_selected");
            }

            var layout = OrbitalComplexRuntimeLayout;
            var layoutSnapshot = CaptureOrbitalComplexDiagnosticSnapshot();
            runtimeReady = _orbitalComplexRuntimeReady;
            authoredRootValid = _orbitalComplexRuntimeBuild is not null
                && IsInstanceValid(_orbitalComplexRuntimeBuild.AuthoredArtRoot)
                && _orbitalComplexRuntimeBuild.AuthoredArtRoot.IsInsideTree();
            gameplayRootValid = _orbitalComplexRuntimeBuild is not null
                && IsInstanceValid(_orbitalComplexRuntimeBuild.GameplayRoot)
                && _orbitalComplexRuntimeBuild.GameplayRoot.IsInsideTree();
            var expectedIds = layout.Objectives.Select(objective => objective.Id).ToArray();
            var expectedTexts = layout.Objectives.Select(objective => objective.EnglishName).ToArray();
            var expectedKeys = layout.Objectives
                .Select(objective => objective.LocalizationKey)
                .ToArray();

            missionReady = IsInstanceValid(_missionDirector)
                && _missionDirector.BackendMissionId == MissionDirector.FalltideBackendMissionId
                && _missionDirector.BackendObjectiveContractValid;
            objectiveIdsReady = missionReady
                && OrbitalSequenceEqual(_missionDirector.ObjectiveIds, expectedIds);
            objectiveTextReady = missionReady
                && OrbitalSequenceEqual(_missionDirector.Objectives, expectedTexts);
            objectiveLocalizationReady = expectedKeys.Length == expectedTexts.Length
                && expectedKeys.All(key => !string.IsNullOrWhiteSpace(key))
                && layout.Objectives.All(objective =>
                    GameLocalization.Get(
                        objective.LocalizationKey,
                        "zh",
                        objective.EnglishName) == objective.ChineseName
                    && GameLocalization.Objective(
                        objective.EnglishName,
                        "zh") == objective.ChineseName)
                && OrbitalSequenceEqual(
                    _missionDirector.ObjectiveLocalizationKeys,
                    expectedKeys);

            hudReady = IsInstanceValid(_hud) && _hud.DeploymentUiReady;
            if (hudReady)
            {
                _hud.SetDeploymentMapSelection(DeploymentMapCatalog.OrbitalComplexId);
            }
            mapSelectionReady = hudReady
                && _hud.SelectedDeploymentMapId == DeploymentMapCatalog.OrbitalComplexId
                && _hud.DeploymentMapCount == DeploymentMapCatalog.Maps.Count
                && _hud.DeploymentMapAvailable;
            tideGateHudReady = hudReady && _hud.ExtractionUsesTideGate;
            minimapReady = hudReady
                && _hud.MinimapLandmarkCount == layout.MinimapLandmarks.Count + 2;

            var extractionShape = IsInstanceValid(_extractionArea)
                ? _extractionArea.GetChildren().OfType<CollisionShape3D>().FirstOrDefault()
                : null;
            extractionReady = IsInstanceValid(_extractionArea)
                && IsInstanceValid(_extractionMarker)
                && IsInstanceValid(_orbitalComplexRuntimeExtractionSite)
                && extractionShape?.Shape is CylinderShape3D cylinder
                && Mathf.IsEqualApprox(cylinder.Radius, layout.Extraction.Radius)
                && Mathf.IsEqualApprox(
                    _orbitalComplexRuntimeExtractionSite!.GlobalPosition.DistanceTo(
                        layout.Extraction.Position),
                    0.0f);

            weaponCases = _lootSources.OfType<WeaponCase>()
                .Count(source => source.HasMeta("falltide_loot_id"));
            gradedLoot = _lootSources.OfType<GradedLootPickup>()
                .Count(source => source.HasMeta("falltide_loot_id"));
            hostileSquads = _hostileSquads.Count(squad =>
                squad.Members.Count == ExtractionSpawnPads.SquadSize
                && squad.Members.All(IsInstanceValid));
            enemies = _enemies.Count(IsInstanceValid);
            explosives = _barrels.Count(barrel =>
                IsInstanceValid(barrel) && barrel.HasMeta("falltide_chain_group"));
            var contentReady = _orbitalComplexRuntimeWeaponCasesSpawned
                && _orbitalComplexRuntimeGradedLootSpawned
                && _orbitalComplexRuntimeValuablesSpawned
                && _orbitalComplexRuntimeExplosivesSpawned
                && weaponCases == layout.WeaponCases.Count
                && gradedLoot == layout.GradedLoot.Count + layout.Valuables.Count
                && explosives == layout.Explosives.Count;
            var encountersReady = _orbitalComplexRuntimeEnemiesSpawned
                && _orbitalComplexRuntimeHostilesSpawned
                && hostileSquads == layout.RivalSpawnPads.Count
                && enemies >= layout.GarrisonSpawns.Count
                    + layout.RivalSpawnPads.Count * ExtractionSpawnPads.SquadSize;

            extractionHudReady = false;
            if (hudReady)
            {
                _hud.SetExtractionCountdown(
                    OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds,
                    OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds,
                    OrbitalComplexExtraction.TransportReady(1),
                    3,
                    3);
                var emergencyHud = _hud.IsExtractionCountdownVisible
                    && _hud.ExtractionAircraftReady
                    && Mathf.IsEqualApprox(
                        _hud.ExtractionCountdownSeconds,
                        OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds);
                _hud.SetExtractionCountdown(
                    OrbitalComplexExtractionStrategy.FullPowerCountdownSeconds,
                    OrbitalComplexExtractionStrategy.FullPowerCountdownSeconds,
                    OrbitalComplexExtraction.TransportReady(2),
                    3,
                    3);
                var fullHud = _hud.IsExtractionCountdownVisible
                    && _hud.ExtractionAircraftReady
                    && Mathf.IsEqualApprox(
                        _hud.ExtractionCountdownSeconds,
                        OrbitalComplexExtractionStrategy.FullPowerCountdownSeconds);
                extractionHudReady = emergencyHud && fullHud;
                _hud.HideExtractionCountdown();
            }

            var originalStage = _objectiveStage;
            OrbitalComplexGateRuntime? tideGate = null;
            OrbitalComplexGateRuntime? vaultGate = null;
            var gateLookupReady = false;
            if (_orbitalComplexRuntimeBuild is not null
                && _orbitalComplexRuntimeBuild.Gates.TryGetValue(
                    "north_tide_gate",
                    out var tideGateValue)
                && _orbitalComplexRuntimeBuild.Gates.TryGetValue(
                    "stormglass_vault",
                    out var vaultGateValue))
            {
                gateLookupReady = true;
                tideGate = tideGateValue;
                vaultGate = vaultGateValue;
            }
            ApplyOrbitalComplexRuntimeObjectiveStage(0);
            var zero = _orbitalComplexRuntimeBuild?.PowerState;
            var stage0TideOpen = tideGate?.IsOpen == true;
            var stage0VaultOpen = vaultGate?.IsOpen == true;
            ApplyOrbitalComplexRuntimeObjectiveStage(1);
            var one = _orbitalComplexRuntimeBuild?.PowerState;
            var stage1TideOpen = tideGate?.IsOpen == true;
            var stage1VaultOpen = vaultGate?.IsOpen == true;
            ApplyOrbitalComplexRuntimeObjectiveStage(2);
            var two = _orbitalComplexRuntimeBuild?.PowerState;
            var stage2TideOpen = tideGate?.IsOpen == true;
            var stage2VaultOpen = vaultGate?.IsOpen == true;
            gateStage0Ready = gateLookupReady
                && zero is not null
                && !zero.ExtractionEnabled
                && !stage0TideOpen
                && !stage0VaultOpen;
            gateStage1Ready = gateLookupReady
                && one is not null
                && one.ExtractionEnabled
                && stage1TideOpen
                && !stage1VaultOpen;
            gateStage2Ready = gateLookupReady
                && two is not null
                && two.ExtractionEnabled
                && stage2TideOpen
                && stage2VaultOpen;
            markerStage2Ready = IsInstanceValid(_extractionMarker)
                && _extractionMarker.GetMeta("falltide_extraction_enabled", false).AsBool();
            var gatesReady = gateStage0Ready
                && gateStage1Ready
                && gateStage2Ready
                && markerStage2Ready;
            ApplyOrbitalComplexRuntimeObjectiveStage(originalStage);
            stageReady = gatesReady
                && !OrbitalComplexExtraction.CanExtract(0)
                && OrbitalComplexExtraction.CanExtract(1)
                && OrbitalComplexExtraction.CanExtract(2)
                && Mathf.IsEqualApprox(
                    OrbitalComplexExtraction.CountdownSeconds(1),
                    OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds)
                && Mathf.IsEqualApprox(
                    OrbitalComplexExtraction.CountdownSeconds(2),
                    OrbitalComplexExtractionStrategy.FullPowerCountdownSeconds)
                && OrbitalComplexExtraction.StatusLocalizationKey(0)
                    == "falltide_extract_locked"
                && OrbitalComplexExtraction.StatusLocalizationKey(1)
                    == "falltide_extract_emergency"
                && OrbitalComplexExtraction.StatusLocalizationKey(2)
                    == "falltide_extract_full";

            valid = layoutSnapshot.LayoutValid
                && layoutSnapshot.SceneReady
                && layoutSnapshot.CollisionReady
                && layoutSnapshot.RoutesReady
                && layoutSnapshot.SpawnsReady
                && layoutSnapshot.LoadingReady
                && selected
                && missionReady
                && objectiveIdsReady
                && objectiveTextReady
                && objectiveLocalizationReady
                && hudReady
                && mapSelectionReady
                && tideGateHudReady
                && minimapReady
                && extractionReady
                && extractionHudReady
                && contentReady
                && encountersReady
                && stageReady;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            GD.PushError($"ORBITAL_INTEGRATION_EXCEPTION {DiagnosticToken(failure)}");
        }

        GD.Print(
            $"ORBITAL_INTEGRATION_CHECK valid={valid} selected={selected} "
            + $"mission={missionReady} objective_ids={objectiveIdsReady} "
            + $"objective_text={objectiveTextReady} localization={objectiveLocalizationReady} "
            + $"hud={hudReady} map_selection={mapSelectionReady} minimap={minimapReady} "
            + $"tide_gate_hud={tideGateHudReady} "
            + $"extraction={extractionReady} extraction_hud={extractionHudReady} "
            + $"stage={stageReady} gate0={gateStage0Ready} gate1={gateStage1Ready} "
            + $"gate2={gateStage2Ready} marker2={markerStage2Ready} "
            + $"runtime_ready={runtimeReady} authored_root={authoredRootValid} "
            + $"gameplay_root={gameplayRootValid} "
            + $"weapon_cases={weaponCases} graded_loot={gradedLoot} "
            + $"hostile_squads={hostileSquads} enemies={enemies} explosives={explosives} "
            + $"failure={DiagnosticToken(failure)}");
        GD.Print($"ORBITAL_INTEGRATION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private static bool OrbitalSequenceEqual(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static string DiagnosticToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }
        var token = value.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_');
        return token.Length > 120 ? token[..120] : token;
    }

    private async void CaptureOrbitalMap()
    {
        await WaitFrames(6);
        var snapshot = CaptureOrbitalComplexDiagnosticSnapshot();
        if (!snapshot.SceneReady || _orbitalComplexRuntimeBuild is null)
        {
            GD.Print("ORBITAL_CAPTURE valid=False reason=authored_scene_not_ready");
            GetTree().Quit(2);
            return;
        }

        if (IsInstanceValid(_hud))
        {
            _hud.Visible = false;
        }
        if (IsInstanceValid(_player))
        {
            _player.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Capture the signature space in its powered state so the authored dish,
        // reactor pit, and cross-level shell are all legible in the review image.
        // This branch is only reachable through --capture-orbital-map and does not
        // alter a normal deployment's blackout progression.
        ApplyOrbitalComplexRuntimeObjectiveStage(2);
        if (IsInstanceValid(_environmentRef))
        {
            _environmentRef.AmbientLightEnergy = 0.18f;
            _environmentRef.TonemapExposure = 0.74f;
            _environmentRef.FogDensity = 0.0018f;
            _environmentRef.FogLightEnergy = 0.18f;
        }
        foreach (var light in _orbitalComplexRuntimeLights)
        {
            light.Visible = false;
        }

        var target = OrbitalComplexMapDefinition.StormglassArrayCenter + new Vector3(0.0f, 6.0f, 0.0f);
        var captureFill = new OmniLight3D
        {
            Name = "OrbitalComplexCaptureFill",
            GlobalPosition = target + new Vector3(12.0f, 18.0f, 18.0f),
            LightColor = new Color(0.42f, 0.72f, 1.0f),
            LightEnergy = 1.8f,
            OmniRange = 82.0f,
            ShadowEnabled = false
        };
        AddChild(captureFill);
        var camera = new Camera3D
        {
            Name = "OrbitalComplexCaptureCamera",
            Fov = 58.0f,
            Near = 0.05f,
            Far = 700.0f
        };
        AddChild(camera);
        camera.GlobalPosition = target + new Vector3(38.0f, 16.0f, 42.0f);
        camera.LookAt(target, Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(12);
        SaveViewportImage("res://orbital_complex_map_validation.png");
        GD.Print(
            $"ORBITAL_CAPTURE valid=True path=orbital_complex_map_validation.png "
            + $"target={target} camera={camera.GlobalPosition} meshes={snapshot.AuthoredMeshCount}");
        GetTree().Quit();
    }

    private async void CaptureOrbitalCoast()
    {
        await WaitFrames(8);
        if (!OrbitalComplexRuntimeSceneReady)
        {
            GD.Print("ORBITAL_COAST_CAPTURE valid=False reason=authored_scene_not_ready");
            GetTree().Quit(2);
            return;
        }

        if (IsInstanceValid(_player))
        {
            _player.ProcessMode = ProcessModeEnum.Disabled;
        }
        var camera = new Camera3D
        {
            Name = "OrbitalComplexCoastCaptureCamera",
            Fov = 68.0f,
            Near = 0.05f,
            Far = 700.0f,
            Position = new Vector3(0.0f, -7.4f, 42.0f)
        };
        AddChild(camera);
        camera.LookAt(new Vector3(0.0f, -11.0f, 142.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(12);
        SaveViewportImage("res://orbital_complex_coast_validation.png");
        GD.Print("ORBITAL_COAST_CAPTURE valid=True path=orbital_complex_coast_validation.png");
        GetTree().Quit();
    }
}
