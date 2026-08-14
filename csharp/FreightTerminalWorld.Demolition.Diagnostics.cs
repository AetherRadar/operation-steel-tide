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
        _hud.SelectDemolitionLoadoutForDiagnostics(WeaponPlatform.ScarL, 2, WeaponPlatform.M1911);
        var mapPoolReady = _hud.DemolitionMapOptionCount == DemolitionMapCatalog.PoolSize
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideforgeId;
        var lockedMapRejected = _hud.PressDemolitionMapForDiagnostics("harbor_locks") == false
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideforgeId;
        var synchronizedWithoutDeployment = _hud.SelectedDemolitionRole == OperatorRole.Medic
            && _hud.SelectedDemolitionPrimary == WeaponPlatform.ScarL
            && _hud.SelectedDemolitionBuildTier == 2
            && _hud.SelectedDemolitionSidearm == WeaponPlatform.M1911
            && !_squadDeployed
            && !_demolitionMode;

        const string scenePath = "res://ui/DemolitionBriefingView.tscn";
        var packedScene = GD.Load<PackedScene>(scenePath);
        var probe = packedScene?.Instantiate<DemolitionBriefingView>();
        var backRequests = 0;
        var deployRequests = 0;
        var requestedRole = -1;
        var requestedPrimary = -1;
        var requestedBuild = -1;
        var requestedSidearm = -1;
        var requestedMap = string.Empty;
        if (probe is not null)
        {
            probe.Visible = false;
            _hud.AddChild(probe);
            probe.BackRequested += () => backRequests++;
            probe.DeployRequested += (role, primary, build, sidearm, mapId) =>
            {
                deployRequests++;
                requestedRole = role;
                requestedPrimary = primary;
                requestedBuild = build;
                requestedSidearm = sidearm;
                requestedMap = mapId;
            };
            probe.SetLanguage("zh");
            probe.PressRoleForDiagnostics(OperatorRole.Recon);
            probe.SelectLoadoutForDiagnostics(WeaponPlatform.AK74, 2, WeaponPlatform.M1911);
            probe.PressMapForDiagnostics(DemolitionMapCatalog.TideforgeId);
            probe.PressBackForDiagnostics();
            probe.PressDeployForDiagnostics();
        }
        var probeReady = probe is not null
            && probe.SceneFilePath == scenePath
            && probe.UiReady
            && probe.IntentSignalsConnected
            && probe.LanguageMatches("zh")
            && probe.SelectedRole == OperatorRole.Recon
            && probe.MapOptionCount == DemolitionMapCatalog.PoolSize
            && backRequests == 1
            && deployRequests == 1
            && requestedRole == (int)OperatorRole.Recon
            && requestedPrimary == (int)WeaponPlatform.AK74
            && requestedBuild == 2
            && requestedSidearm == (int)WeaponPlatform.M1911
            && requestedMap == DemolitionMapCatalog.TideforgeId;
        probe?.QueueFree();
        await WaitFrames(3);

        _hud.PressDemolitionBackForDiagnostics();
        var backReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsDemolitionBriefingVisible
            && !_squadDeployed
            && !_demolitionMode;
        _hud.SetLanguage(originalLanguage);
        var valid = sceneReady && chineseReady && englishReady && mapPoolReady && lockedMapRejected
            && synchronizedWithoutDeployment && probeReady && backReady;
        GD.Print($"DEMOLITION_BRIEFING_CHECK valid={valid} scene={sceneReady} packed={_hud.DemolitionBriefingUsesPackedScene} ui={_hud.DemolitionBriefingUiReady} signals={_hud.DemolitionBriefingIntentSignalsReady} chinese={chineseReady} english={englishReady} map_pool={mapPoolReady} locked_rejected={lockedMapRejected} sync={synchronizedWithoutDeployment} probe={probeReady} back={backReady}");
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
            && layout.AttackToBPath.Count >= 7
            && layout.AttackMidPath.Count >= 4;
        var balanceReady = layout.HasBalancedSiteTravel
            && layout.SiteTravelDifferenceRatio <= DemolitionArenaLayout.MaximumSiteTravelDifference;
        var sightlinesBlocked = !layout.HasSpawnSightlineToSite(0)
            && !layout.HasSpawnSightlineToSite(1);
        var sitesSeparated = layout.SiteSeparation >= 74.0f
            && HorizontalDistance(layout.AttackSpawn, layout.DefenderSpawn) >= 104.0f;
        var extendedTravel = layout.WorldBounds.Size.Y >= 108.0f
            && layout.AttackToALength >= 75.0f
            && layout.AttackToBLength >= 75.0f
            && layout.SitePositions.All(site => HorizontalDistance(layout.DefenderSpawn, site) >= 38.0f);
        var rotationReady = layout.RotationLength >= 92.0f && layout.RotationLength <= 118.0f;
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
        var valid = lifecycleReady && routesReady && balanceReady && sightlinesBlocked && sitesSeparated
            && extendedTravel && rotationReady && clearanceReady && markersReady && spatialIsolation && sitesReady;
        GD.Print($"DEMOLITION_ARENA_CHECK valid={valid} lifecycle={lifecycleReady} inactive={initiallyIsolated} active={collisionReady} deactivated={deactivatedCleanly} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} routes={routesReady} extended={extendedTravel} site_gap={layout.SiteSeparation:0.00} spawn_gap={HorizontalDistance(layout.AttackSpawn, layout.DefenderSpawn):0.00} path_a={layout.AttackToALength:0.00} path_b={layout.AttackToBLength:0.00} difference={layout.SiteTravelDifferenceRatio:P1} sightlines={sightlinesBlocked} rotation={layout.RotationLength:0.00} clearance={clearanceReady} blockers={routeABlocker}|{routeBBlocker}|{routeMidBlocker}|{rotationBlocker} markers={markersReady} isolation={spatialIsolation} sites={sitesReady}");
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
            Far = 400.0f
        };
        AddChild(camera);
        camera.GlobalPosition = layout.Origin + new Vector3(0.0f, 90.0f, 78.0f);
        camera.LookAt(layout.Origin, Vector3.Up);
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
        _hud.SelectDemolitionLoadoutForDiagnostics(WeaponPlatform.AK74, 2, WeaponPlatform.M1911);
        _hud.PressDemolitionMapForDiagnostics(DemolitionMapCatalog.TideforgeId);
        var briefingSelection = _hud.SelectedDemolitionRole == OperatorRole.Medic
            && _hud.SelectedDemolitionPrimary == WeaponPlatform.AK74
            && _hud.SelectedDemolitionBuildTier == 2
            && _hud.SelectedDemolitionSidearm == WeaponPlatform.M1911
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideforgeId;
        _hud.PressDemolitionDeployForDiagnostics();
        await WaitFrames(5);
        var layout = DemolitionLayout();
        // The $800 opening wallet cannot cover the $4400 full buy, so round 1 deploys the
        // eco kit: MP5A5 tier 0 with the sidearm chosen in the briefing.
        var ecoKit = _player.Role == OperatorRole.Medic
            && _player.HasFireablePrimary
            && _player.EquippedWeapon.Platform == WeaponPlatform.MP5A5
            && _player.HasSecondaryWeapon
            && _player.SecondaryWeaponPlatform == WeaponPlatform.M1911
            && _player.CurrentAmmoGrade == LootGrade.Common
            && _player.ReserveAmmo == 150;
        var ecoBuild = _player.EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Barrel, out var barrel)
            && barrel == "barrel_cqb"
            && _player.EquippedWeapon.Attachments.TryGetValue(AttachmentSlot.Optic, out var optic)
            && optic == "optic_micro"
            && !_player.EquippedWeapon.Attachments.ContainsKey(AttachmentSlot.Muzzle)
            && _player.AmmoReserveFor(AmmoCaliber.Pistol) == 60;
        var slotBindings = InputMap.HasAction("weapon_primary")
            && InputMap.HasAction("weapon_secondary")
            && InputMap.HasAction("weapon_melee")
            && InputMap.ActionGetEvents("weapon_secondary").Count > 0;
        _player.SetMagazineAmmoForDiagnostics(11);
        _player.SelectWeapon((int)PlayerWeaponSlot.Secondary);
        var sidearmSelected = _player.ActiveWeaponSlot == PlayerWeaponSlot.Secondary
            && _player.EquippedWeapon.Platform == WeaponPlatform.M1911;
        _player.SetMagazineAmmoForDiagnostics(4);
        _player.SelectWeapon((int)PlayerWeaponSlot.Primary);
        var primaryMagazineSaved = _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary
            && _player.EquippedWeapon.Platform == WeaponPlatform.MP5A5
            && _player.Ammo == 11;
        _player.SelectWeapon((int)PlayerWeaponSlot.Secondary);
        var secondaryMagazineSaved = _player.Ammo == 4;
        _player.SelectWeapon((int)PlayerWeaponSlot.Primary);
        var weaponSlots = sidearmSelected && primaryMagazineSaved && secondaryMagazineSaved;
        var isolatedEconomy = _operatorProfileStore.Profile.Credits == creditsBefore
            && _operatorProfileStore.Profile.DeploymentCount == deploymentsBefore
            && !_deploymentPurchaseCommitted;
        var deployed = _demolitionMode
            && _demolitionRoundActive
            && _squadDeployed
            && DemolitionSquadSizeTotal == 5
            && DemolitionOpponentCount == 5
            && DemolitionPlayerSide == DemolitionTeam.Attackers
            && DemolitionPlayerScore == 0
            && DemolitionOpponentScore == 0
            && _demolitionSites.All(site => site.Visible)
            && _demolitionArena?.Active == true
            && !_levelRoot.Visible
            && _levelRoot.ProcessMode == ProcessModeEnum.Disabled
            && !_extractionMarker.Visible
            && _hud.IsGameplayHudVisible
            && !_hud.IsDemolitionBriefingVisible
            && !GetTree().Paused;
        var openingStrategy = _demolitionAttackerPlan is not null
            && _demolitionAttackerPlan.Assignments.Count == 5
            && _demolitionDefenderPlan is not null
            && _demolitionDefenderPlan.Assignments.Count == 5
            && _demolitionDefenderPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.AnchorA)
            && _demolitionDefenderPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.AnchorB)
            && _demolitionDefenderPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.MidControl)
            && DemolitionStrategyAssignmentCount == 10
            && !_demolitionDevicePlanted;
        var sitesClear = layout.SitePositions.All(IsDemolitionSitePlacementClear);
        var minimapReady = _hud.MinimapLandmarkCount == layout.Markers.Count
            && _hud.MinimapPlayerPosition.X > 0.0f
            && _hud.MinimapPlayerPosition.Y > 0.0f;

        var hostileAircraftIsolated = !IsInstanceValid(_aircraft)
            || (_aircraft!.ProcessMode == ProcessModeEnum.Disabled && !_aircraft.Visible);
        var demolitionPhase = _missionPhase;
        var defenderCountBeforeReinforcementTick = DemolitionOpponentCount;
        _reinforcementPending = true;
        _reinforcementCountdown = 0.0f;
        _missionPhase = "COMBAT";
        UpdateReinforcements(8.0f);
        var reinforcementsIsolated = _reinforcementPending
            && !_reinforcementsDeployed
            && DemolitionOpponentCount == defenderCountBeforeReinforcementTick
            && _enemies.Count == defenderCountBeforeReinforcementTick;
        _reinforcementPending = false;
        _reinforcementCountdown = 0.0f;
        _missionPhase = demolitionPhase;

        OnPhaseChanged("COMBAT", 18.0f, true);
        OnObjectiveChanged(2, "REACH THE EXTRACTION ZONE", true);
        var directorIsolation = _missionPhase == "DEMOLITION" && !_extractionMarker.Visible;

        var fundsAfterOpeningBuy = DemolitionPlayerFunds;
        // The $2400 eco price clamps to the $800 opening wallet, leaving the player broke.
        var playerBoughtEcoKit = ecoKit && ecoBuild && fundsAfterOpeningBuy == 0;

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
        var retakeStrategy = _demolitionDefenderPlan is not null
            && _demolitionDefenderPlan.Phase == DemolitionStrategyPhase.PostPlant
            && _demolitionDefenderPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.Defuse)
            && _demolitionDefenderPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.CoverDefuser)
            && _demolitionDefenderPlan.Assignments.Any(assignment => assignment.Duty is DemolitionDuty.Retake or DemolitionDuty.Flank)
            && _demolitionAttackerPlan is not null
            && _demolitionAttackerPlan.Phase == DemolitionStrategyPhase.PostPlant
            && _demolitionAttackerPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.SiteGuard)
            && _demolitionAttackerPlan.Assignments.Any(assignment => assignment.Duty == DemolitionDuty.Crossfire);
        var (defuseAi, initialDefuserDistance, finalDefuserDistance, defuseFrames) = await ValidateDemolitionDefuseAi(layout);

        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent))
            {
                opponent.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _demolitionRemaining = 0.05f;
        UpdateDemolitionRound(0.1f);
        var playerFundsAfterWin = DemolitionPlayerFunds;
        var opponentFundsAfterLoss = DemolitionOpponentFunds;
        var roundRecorded = !_missionEnded
            && !_demolitionRoundActive
            && !_hud.IsMissionResultVisible
            && DemolitionPlayerScore == 1
            && DemolitionOpponentScore == 0
            && DemolitionRoundNumber == 2
            && playerFundsAfterWin == System.Math.Min(DemolitionEconomy.MaximumFunds,
                fundsAfterOpeningBuy + DemolitionEconomy.WinReward)
            && opponentFundsAfterLoss == DemolitionEconomy.StartingFunds + DemolitionEconomy.LossBaseReward;
        UpdateDemolitionIntermission(DemolitionIntermissionDuration + 0.1f);
        await WaitFrames(3);
        var roundReset = _demolitionRoundActive
            && !_demolitionDevicePlanted
            && DemolitionRoundNumber == 2
            && DemolitionOpponentCount == 5
            && DemolitionSquadSizeTotal == 5
            && !_player.IsDead
            && Mathf.IsEqualApprox(_player.Health, _player.MaxHealth)
            && _squadMates.Where(IsInstanceValid).All(mate => !mate.IsDowned
                && !mate.IsBodyBag
                && Mathf.IsEqualApprox(mate.Health, mate.MaxHealth))
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Primary
            && _player.EquippedWeapon.Platform == WeaponPlatform.MP5A5
            && _player.SecondaryWeaponPlatform == WeaponPlatform.M1911
            && _player.PrimaryMagazineAmmo == _player.EquippedWeapon.Stats().MagazineSize;

        // Rounds 2-12 are still the attacking half under MR12: run them out on the clock
        // so the halftime swap hands the player squad the defense in round 13.
        while (!_missionEnded && _demolitionRoundActive && DemolitionRoundNumber <= DemolitionMatchState.RoundsPerHalf)
        {
            _demolitionRemaining = 0.05f;
            UpdateDemolitionRound(0.1f);
            if (_missionEnded || _demolitionRoundActive)
            {
                break;
            }
            UpdateDemolitionIntermission(DemolitionIntermissionDuration + 0.1f);
        }
        var defenseRound = await ValidateDemolitionDefenseRound(layout);
        var matchRules = ValidateDemolitionMatchRules();
        var economyRules = ValidateDemolitionEconomyRules();
        var valid = entryButton && briefingSelection && ecoKit && ecoBuild && slotBindings
            && weaponSlots && isolatedEconomy && deployed && openingStrategy && sitesClear
            && minimapReady && hostileAircraftIsolated && reinforcementsIsolated
            && directorIsolation && playerBoughtEcoKit && planted && retakeStrategy && defuseAi
            && roundRecorded && roundReset && defenseRound && matchRules && economyRules;
        GD.Print($"DEMOLITION_CHECK valid={valid} entry_button={entryButton} briefing={briefingSelection} deployed={deployed} arena={IsDemolitionArenaActive} gameplay={_hud.IsGameplayHudVisible} squad={DemolitionSquadSizeTotal} opponents={DemolitionOpponentCount} eco_kit={ecoKit} eco_build={ecoBuild} slots={weaponSlots} bindings={slotBindings} economy={isolatedEconomy} opening_strategy={openingStrategy} retake_strategy={retakeStrategy} assignments={DemolitionStrategyAssignmentCount} minimap={minimapReady} aircraft_isolated={hostileAircraftIsolated} reinforcements_isolated={reinforcementsIsolated} director_isolation={directorIsolation} sites={DemolitionSiteCount} sites_clear={sitesClear} funds_after_buy={fundsAfterOpeningBuy} eco_buy={playerBoughtEcoKit} planted={planted} plant_steps={plantSteps} defuse_ai={defuseAi} defuse_distance={initialDefuserDistance:0.00}->{finalDefuserDistance:0.00} defuse_progress={_demolitionDefuseProgress:0.00} defuse_frames={defuseFrames}/600 round_recorded={roundRecorded} round_reset={roundReset} defense_round={defenseRound} match_rules={matchRules} economy_rules={economyRules} score={DemolitionPlayerScore}:{DemolitionOpponentScore} round={DemolitionRoundNumber} result={_hud.IsMissionResultVisible}");
        GD.Print($"DEMOLITION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    /// <summary>
    /// Round 13 opens the second half with the player squad defending after the halftime
    /// swap: verify the defender-side spawns, the enemy AI carrying and planting the bomb,
    /// and the player defusing it to win the round.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> ValidateDemolitionDefenseRound(DemolitionArenaLayout layout)
    {
        if (_missionEnded || !_demolitionRoundActive
            || DemolitionRoundNumber != DemolitionMatchState.RoundsPerHalf + 1)
        {
            GD.Print($"DEMOLITION_DEFENSE_CHECK valid=False stage=preconditions round={DemolitionRoundNumber} active={_demolitionRoundActive}");
            return false;
        }

        var playerSide = DemolitionPlayerSide;
        var spawnsAtDefenderBarrier = playerSide == DemolitionTeam.Defenders
            && _player.GlobalPosition.Z < layout.Origin.Z - 40.0f;
        var enemiesAttacking = DemolitionOpponentCount == 5
            && _demolitionOpponents.All(opponent => !IsInstanceValid(opponent)
                || opponent.GlobalPosition.Z > layout.Origin.Z + 40.0f);

        // Freeze every combatant except the enemy carrier so the plant run is
        // deterministic; the carrier needs live physics to walk into plant range.
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
        _player.GlobalPosition = layout.Origin + new Vector3(6.0f, 0.2f, 30.0f);
        _player.Velocity = Vector3.Zero;

        var carrier = _demolitionCarrier;
        if (carrier is null || !IsInstanceValid(carrier) || carrier.IsDead)
        {
            GD.Print($"DEMOLITION_DEFENSE_CHECK valid=False stage=carrier_missing round={DemolitionRoundNumber}");
            return false;
        }
        var siteIndex = Mathf.Clamp(_demolitionEnemyTargetSite, 0, layout.SitePositions.Count - 1);
        var site = layout.SitePositions[siteIndex];
        carrier!.GlobalPosition = site + new Vector3(0.0f, 0.2f, 6.0f);
        carrier.Velocity = Vector3.Zero;
        carrier.ProcessMode = ProcessModeEnum.Inherit;

        var plantFrames = 0;
        while (!_demolitionDevicePlanted && plantFrames < 600)
        {
            _ = TryHandleDemolitionDefenderMovement(carrier, 0.05f, null);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            plantFrames++;
        }
        var enemyPlanted = _demolitionDevicePlanted
            && _demolitionActiveSite == siteIndex
            && plantFrames > 1;
        carrier.ProcessMode = ProcessModeEnum.Disabled;
        if (!enemyPlanted)
        {
            GD.Print($"DEMOLITION_DEFENSE_CHECK valid=False stage=plant_failed frames={plantFrames} progress={_demolitionEnemyPlantProgress:0.00} carrier={carrier.GlobalPosition} site={site}");
            return false;
        }

        // Walk the player to the planted device and hold the interact key to defuse.
        _player.GlobalPosition = site + new Vector3(0.0f, 0.2f, 0.6f);
        _interactReleaseRequired = false;
        Input.ActionRelease("interact");
        Input.ActionPress("interact");
        var defuseSteps = 0;
        var maximumDefuseSteps = Mathf.CeilToInt(DemolitionDefuseDuration / 0.1f) + 4;
        while (_demolitionRoundActive && DemolitionPlayerDefuseProgress < 1.0f && defuseSteps < maximumDefuseSteps)
        {
            UpdateDemolitionInteraction(0.1f);
            defuseSteps++;
        }
        Input.ActionRelease("interact");
        var defusedAndWon = !_demolitionRoundActive
            && DemolitionPlayerScore == 2
            && DemolitionOpponentScore == DemolitionMatchState.RoundsPerHalf - 1
            && defuseSteps > 1;

        var valid = spawnsAtDefenderBarrier && enemiesAttacking && enemyPlanted && defusedAndWon;
        GD.Print($"DEMOLITION_DEFENSE_CHECK valid={valid} side_swapped={playerSide == DemolitionTeam.Defenders} spawns_defend={spawnsAtDefenderBarrier} enemies_attacking={enemiesAttacking} ai_planted={enemyPlanted} plant_frames={plantFrames} defused={defusedAndWon} defuse_steps={defuseSteps}");
        return valid;
    }

    private static bool ValidateDemolitionMatchRules()
    {
        var match = new DemolitionMatchState();
        var sidesHold = true;
        for (var round = 1; round <= DemolitionMatchState.RegulationRounds; round++)
        {
            if (match.SideForRound(round) != (round <= DemolitionMatchState.RoundsPerHalf
                ? DemolitionTeam.Attackers
                : DemolitionTeam.Defenders))
            {
                sidesHold = false;
            }
        }
        var overtimeSidesHold = match.SideForRound(25) == DemolitionTeam.Attackers
            && match.SideForRound(29) == DemolitionTeam.Defenders
            && match.SideForRound(33) == DemolitionTeam.Attackers;

        // Trade rounds to 5-5, then tie it 6-6: the round completing the twelfth reports
        // the halftime side swap.
        for (var round = 0; round < 5; round++)
        {
            match.RecordRound(false);
            match.RecordRound(true);
        }
        match.RecordRound(false);
        var halftimeSwapReported = match.RecordRound(true).SideSwap;
        var tiedAfterTwelve = match.CompletedRounds == 12
            && match.PlayerScore == 6
            && match.OpponentScore == 6
            && !match.IsComplete;

        // Trade to 12-12 through all 24 regulation rounds: overtime starts.
        DemolitionRoundResult round24 = default;
        for (var round = 0; round < 6; round++)
        {
            match.RecordRound(true);
            round24 = match.RecordRound(false);
        }
        var enteredOvertimeCleanly = round24.EnteredOvertime
            && match.CompletedRounds == DemolitionMatchState.RegulationRounds
            && match.PlayerScore == 12
            && match.OpponentScore == 12
            && match.IsOvertime
            && !match.IsComplete;

        // Overtime is win-by-two: 13-12 keeps playing, 14-12 finishes the match.
        match.RecordRound(true);
        var onePointLeadDoesNotFinish = !match.IsComplete
            && match.PlayerScore == 13
            && match.OpponentScore == 12;
        var finished = match.RecordRound(true);
        var twoPointLeadFinishes = finished.MatchComplete
            && match.Winner is not null
            && match.PlayerScore == 14
            && match.OpponentScore == 12;

        var regulation = new DemolitionMatchState();
        for (var round = 0; round < 13; round++)
        {
            regulation.RecordRound(true);
        }
        var regulationFirstToThirteen = regulation.IsComplete
            && !regulation.IsOvertime
            && regulation.PlayerScore == 13
            && regulation.OpponentScore == 0;

        return sidesHold && overtimeSidesHold && tiedAfterTwelve && halftimeSwapReported
            && enteredOvertimeCleanly && onePointLeadDoesNotFinish && twoPointLeadFinishes
            && regulationFirstToThirteen;
    }

    private static bool ValidateDemolitionEconomyRules()
    {
        var player = new DemolitionEconomy();
        var opponent = new DemolitionEconomy();
        var startsEqual = player.Funds == DemolitionEconomy.StartingFunds
            && opponent.Funds == DemolitionEconomy.StartingFunds;
        var winReward = player.RecordRound(won: true, objectiveCompleted: false);
        var lossReward = opponent.RecordRound(won: false, objectiveCompleted: false);
        var firstRewards = winReward == DemolitionEconomy.WinReward
            && lossReward == DemolitionEconomy.LossBaseReward;
        var secondLoss = opponent.RecordRound(won: false, objectiveCompleted: false);
        var streaksEscalate = secondLoss == DemolitionEconomy.LossBaseReward + DemolitionEconomy.LossStreakBonus;
        var plantBonus = new DemolitionEconomy()
            .RecordRound(won: false, objectiveCompleted: true)
            == DemolitionEconomy.LossBaseReward + DemolitionEconomy.PlantBonus;
        var lossStreakResets = opponent.LossStreak == 2
            && opponent.RecordRound(won: true, objectiveCompleted: false) == DemolitionEconomy.WinReward
            && opponent.LossStreak == 0;
        player.Reset();
        var fundsCapped = new DemolitionEconomy();
        for (var round = 0; round < 6; round++)
        {
            fundsCapped.RecordRound(won: true, objectiveCompleted: false);
        }
        var capHolds = fundsCapped.Funds == DemolitionEconomy.MaximumFunds;
        return startsEqual && firstRewards && streaksEscalate && plantBonus
            && lossStreakResets && player.Funds == DemolitionEconomy.StartingFunds && capHolds;
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
        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent))
            {
                opponent.ProcessMode = ProcessModeEnum.Disabled;
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
