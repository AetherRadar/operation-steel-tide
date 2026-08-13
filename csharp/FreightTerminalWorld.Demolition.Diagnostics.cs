using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionBriefing()
    {
        await WaitFrames(3);
        var originalLanguage = _hud.CurrentLanguage;
        _hud.PressDemolitionModeForDiagnostics();
        var sceneReady = _hud.IsDemolitionBriefingVisible
            && !_hud.IsOperationsOfficeVisible
            && _hud.DemolitionBriefingUiReady
            && _hud.DemolitionBriefingUsesPackedScene
            && _hud.DemolitionBriefingIntentSignalsReady;

        _hud.SetLanguage("zh");
        var chineseReady = _hud.DemolitionBriefingLanguageReady;
        _hud.SetLanguage("en");
        var englishReady = _hud.DemolitionBriefingLanguageReady;
        _hud.PressDemolitionRoleForDiagnostics(OperatorRole.Medic);
        var synchronizedWithoutDeployment = _hud.SelectedDemolitionRole == OperatorRole.Medic
            && !_squadDeployed
            && !_demolitionMode;

        const string scenePath = "res://ui/DemolitionBriefingView.tscn";
        var packedScene = GD.Load<PackedScene>(scenePath);
        var probe = packedScene?.Instantiate<DemolitionBriefingView>();
        var backRequests = 0;
        var deployRequests = 0;
        var requestedRole = -1;
        if (probe is not null)
        {
            probe.Visible = false;
            _hud.AddChild(probe);
            probe.BackRequested += () => backRequests++;
            probe.DeployRequested += role =>
            {
                deployRequests++;
                requestedRole = role;
            };
            probe.SetLanguage("zh");
            probe.PressRoleForDiagnostics(OperatorRole.Recon);
            probe.PressBackForDiagnostics();
            probe.PressDeployForDiagnostics();
        }
        var probeReady = probe is not null
            && probe.SceneFilePath == scenePath
            && probe.UiReady
            && probe.IntentSignalsConnected
            && probe.LanguageMatches("zh")
            && probe.SelectedRole == OperatorRole.Recon
            && backRequests == 1
            && deployRequests == 1
            && requestedRole == (int)OperatorRole.Recon;
        probe?.QueueFree();
        await WaitFrames(3);

        _hud.PressDemolitionBackForDiagnostics();
        var backReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsDemolitionBriefingVisible
            && !_squadDeployed
            && !_demolitionMode;
        _hud.SetLanguage(originalLanguage);
        var valid = sceneReady && chineseReady && englishReady
            && synchronizedWithoutDeployment && probeReady && backReady;
        GD.Print($"DEMOLITION_BRIEFING_CHECK valid={valid} scene={sceneReady} packed={_hud.DemolitionBriefingUsesPackedScene} ui={_hud.DemolitionBriefingUiReady} signals={_hud.DemolitionBriefingIntentSignalsReady} chinese={chineseReady} english={englishReady} sync={synchronizedWithoutDeployment} probe={probeReady} back={backReady}");
        GD.Print($"DEMOLITION_BRIEFING_PASS valid={valid}");
        GetTree().Paused = false;
        await WaitFrames(180);
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void ValidateDemolitionArena()
    {
        await WaitFrames(3);
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.Print("DEMOLITION_ARENA_CHECK valid=False built=False");
            GD.Print("DEMOLITION_ARENA_PASS valid=False");
            GetTree().Paused = false;
            GetTree().Quit(2);
            return;
        }

        var arena = _demolitionArena;
        var layout = arena.Layout;
        var initiallyIsolated = !arena.Active
            && !arena.Root.Visible
            && arena.Root.ProcessMode == ProcessModeEnum.Disabled
            && arena.ActiveCollisionBodyCount == 0
            && arena.AllStaticBodiesUseWorldLayer();
        GetTree().Paused = false;
        DisableActorsForSurvivalDiagnostics();
        arena.SetActive(true);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var collisionReady = arena.Active
            && arena.Root.Visible
            && arena.Root.ProcessMode == ProcessModeEnum.Inherit
            && arena.CollisionBodyCount >= 35
            && arena.ActiveCollisionBodyCount == arena.CollisionBodyCount
            && arena.AllStaticBodiesUseWorldLayer();
        var routesReady = layout.HasThreeAttackRoutes
            && layout.AttackToAPath.Count >= 6
            && layout.AttackToBPath.Count >= 6
            && layout.AttackMidPath.Count >= 4;
        var balanceReady = layout.HasBalancedSiteTravel
            && layout.SiteTravelDifferenceRatio <= DemolitionArenaLayout.MaximumSiteTravelDifference;
        var sightlinesBlocked = !layout.HasSpawnSightlineToSite(0)
            && !layout.HasSpawnSightlineToSite(1);
        var rotationReady = layout.RotationLength >= 45.0f && layout.RotationLength <= 80.0f;
        var routeAClear = layout.HasCapsuleClearance(layout.AttackToAPath, out var routeABlocker);
        var routeBClear = layout.HasCapsuleClearance(layout.AttackToBPath, out var routeBBlocker);
        var routeMidClear = layout.HasCapsuleClearance(layout.AttackMidPath, out var routeMidBlocker);
        var rotationClear = layout.HasCapsuleClearance(layout.SiteRotationPath, out var rotationBlocker);
        var clearanceReady = layout.HasPlayerClearance
            && routeAClear
            && routeBClear
            && routeMidClear
            && rotationClear;
        var markersReady = layout.Markers.Count == 5
            && layout.Markers.Select(marker => marker.LocalizationKey).Distinct().Count() == layout.Markers.Count
            && layout.Markers.All(marker => layout.IsInsideArena(marker.Position, 0.1f))
            && layout.Markers.All(marker => GameLocalization.Get(marker.LocalizationKey, "zh", marker.EnglishName) != marker.EnglishName);
        var extractionBounds = new Rect2(-170.0f, -220.0f, MapWidthMeters, MapDepthMeters);
        var spatialIsolation = !layout.WorldBounds.Intersects(extractionBounds)
            && layout.Origin.DistanceTo(OperationsOfficeOrigin) >= 300.0f;
        var sitesReady = arena.Sites.Count == 2
            && layout.SitePositions.All(position => layout.IsInsideArena(position))
            && arena.Sites.Select((site, index) => site.GlobalPosition.DistanceTo(layout.SitePositions[index]))
                .All(distance => distance <= 0.01f);

        arena.SetActive(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var deactivatedCleanly = !arena.Active
            && !arena.Root.Visible
            && arena.Root.ProcessMode == ProcessModeEnum.Disabled
            && arena.ActiveCollisionBodyCount == 0
            && arena.AllStaticBodiesUseWorldLayer();
        var lifecycleReady = initiallyIsolated && collisionReady && deactivatedCleanly;
        var valid = lifecycleReady && routesReady && balanceReady && sightlinesBlocked
            && rotationReady && clearanceReady && markersReady && spatialIsolation && sitesReady;
        GD.Print($"DEMOLITION_ARENA_CHECK valid={valid} lifecycle={lifecycleReady} inactive={initiallyIsolated} active={collisionReady} deactivated={deactivatedCleanly} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} routes={routesReady} path_a={layout.AttackToALength:0.00} path_b={layout.AttackToBLength:0.00} difference={layout.SiteTravelDifferenceRatio:P1} sightlines={sightlinesBlocked} rotation={layout.RotationLength:0.00} clearance={clearanceReady} blockers={routeABlocker}|{routeBBlocker}|{routeMidBlocker}|{rotationBlocker} markers={markersReady} isolation={spatialIsolation} sites={sitesReady}");
        GD.Print($"DEMOLITION_ARENA_PASS valid={valid}");
        var arenaRoot = arena.Root;
        _demolitionArena = null;
        _demolitionSites.Clear();
        arenaRoot.QueueFree();
        arena = null!;
        layout = null!;
        arenaRoot = null!;
        await WaitFrames(3);
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        await WaitFrames(24);
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureDemolitionArena()
    {
        EnsureDemolitionArenaBuilt();
        if (_demolitionArena is null)
        {
            GD.PushError("Demolition arena is unavailable for capture.");
            GetTree().Paused = false;
            GetTree().Quit(2);
            return;
        }

        GetTree().Paused = false;
        DisableActorsForSurvivalDiagnostics();
        _levelRoot.Visible = false;
        _operationsOfficeScene.Visible = false;
        _demolitionArena.SetActive(true);
        _hud.Visible = false;
        _player.Visible = false;
        _player.ProcessMode = ProcessModeEnum.Disabled;
        if (IsInstanceValid(_worldBoss))
        {
            _worldBoss!.Visible = false;
        }
        if (IsInstanceValid(_aircraft))
        {
            _aircraft!.Visible = false;
            _aircraft.SetPhysicsProcess(false);
        }

        var layout = _demolitionArena.Layout;
        var camera = new Camera3D
        {
            Name = "DemolitionArenaCaptureCamera",
            Fov = 58.0f,
            Near = 0.05f,
            Far = 320.0f
        };
        AddChild(camera);
        camera.GlobalPosition = layout.Origin + new Vector3(0.0f, 66.0f, 62.0f);
        camera.LookAt(layout.Origin + new Vector3(0.0f, 0.0f, -5.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(48);
        var cameraCurrent = GetViewport().GetCamera3D() == camera;
        SaveViewportImage("res://demolition_arena_validation.png");
        GD.Print($"DEMOLITION_ARENA_CAPTURE valid={cameraCurrent} camera={cameraCurrent} bodies={_demolitionArena.CollisionBodyCount} visuals={_demolitionArena.VisualPartCount} path=demolition_arena_validation.png");
        _demolitionArena.SetActive(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var arenaRoot = _demolitionArena.Root;
        _demolitionArena = null;
        _demolitionSites.Clear();
        arenaRoot.QueueFree();
        camera.QueueFree();
        camera = null!;
        layout = null!;
        arenaRoot = null!;
        await WaitFrames(3);
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        await WaitFrames(24);
        GetTree().Quit(cameraCurrent ? 0 : 2);
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
        await WaitFrames(5);
        var layout = DemolitionLayout();
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
            && _demolitionArena?.Active == true
            && !_levelRoot.Visible
            && _levelRoot.ProcessMode == ProcessModeEnum.Disabled
            && !_extractionMarker.Visible
            && _hud.IsGameplayHudVisible
            && !_hud.IsDemolitionBriefingVisible
            && !GetTree().Paused;
        var sitesClear = layout.SitePositions.All(IsDemolitionSitePlacementClear);
        var minimapReady = _hud.MinimapLandmarkCount == layout.Markers.Count
            && _hud.MinimapPlayerPosition.X > 0.0f
            && _hud.MinimapPlayerPosition.Y > 0.0f;

        var hostileAircraftIsolated = !IsInstanceValid(_aircraft)
            || (_aircraft!.ProcessMode == ProcessModeEnum.Disabled && !_aircraft.Visible);
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
        var directorIsolation = _missionPhase == "DEMOLITION" && !_extractionMarker.Visible;

        _player.GlobalPosition = layout.SitePositions[0] + new Vector3(0, 0.1f, 0);
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
            && _demolitionArena?.Owns(_demolitionDevice!) == true
            && !_extractionCountdownActive
            && plantSteps > 1;
        var (defuseAi, initialDefuserDistance, finalDefuserDistance, defuseFrames) = await ValidateDemolitionDefuseAi(layout);

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
            && minimapReady && hostileAircraftIsolated && reinforcementsIsolated
            && directorIsolation && planted && defuseAi && completed;
        GD.Print($"DEMOLITION_CHECK valid={valid} entry_button={entryButton} role_button={roleButton} deployed={deployed} arena={IsDemolitionArenaActive} gameplay={_hud.IsGameplayHudVisible} squad={ActiveSquadCount} defenders={DemolitionDefenderCount} fixed_kit={fixedKit} economy={isolatedEconomy} minimap={minimapReady} aircraft_isolated={hostileAircraftIsolated} reinforcements_isolated={reinforcementsIsolated} director_isolation={directorIsolation} sites={DemolitionSiteCount} sites_clear={sitesClear} planted={planted} plant_steps={plantSteps} defuse_ai={defuseAi} defuse_distance={initialDefuserDistance:0.00}->{finalDefuserDistance:0.00} defuse_progress={_demolitionDefuseProgress:0.00} defuse_frames={defuseFrames}/600 completed={completed} result={_hud.IsMissionResultVisible}");
        GD.Print($"DEMOLITION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async System.Threading.Tasks.Task<(bool Valid, float Initial, float Final, int Frames)> ValidateDemolitionDefuseAi(
        DemolitionArenaLayout layout)
    {
        SelectDemolitionDefuser();
        var defuser = _demolitionDefuser;
        if (defuser is null)
        {
            return (false, 0.0f, float.PositiveInfinity, 0);
        }

        _player.GlobalPosition = layout.Origin + new Vector3(0.0f, 0.2f, 38.0f);
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.GlobalPosition = layout.Origin + new Vector3(3.0f + mate.SquadSlot * 2.0f, 0.2f, 38.0f);
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
        var site = layout.SitePositions[0];
        defuser.GlobalPosition = site + new Vector3(0, 0, 8.0f);
        defuser.Velocity = Vector3.Zero;
        defuser.SentryMode = false;
        defuser.ResetScriptedObjectiveNavigation();
        PlanDemolitionDefuseRoute();
        defuser.ProcessMode = ProcessModeEnum.Inherit;
        var initial = HorizontalDistance(defuser.GlobalPosition, site);
        const int maximumFrames = 600;
        var frames = 0;
        while (frames < maximumFrames && _demolitionDefuseProgress < 0.12f && !_missionEnded)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            frames++;
        }
        var final = HorizontalDistance(defuser.GlobalPosition, site);
        var valid = initial >= 7.5f
            && final <= 2.4f
            && _demolitionDefuseProgress >= 0.08f
            && !_missionEnded;
        return (valid, initial, final, frames);
    }
}
