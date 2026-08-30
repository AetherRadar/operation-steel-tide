using System;
using System.Collections.Generic;
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
        var mapPoolReady = _hud.DemolitionMapOptionCount == DemolitionMapCatalog.PoolSize
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.BazaarCrossingId
            && _hud.BrowsedDemolitionMapIndex == 0
            && _hud.BrowsedDemolitionMapAvailable
            && _hud.DemolitionBriefingDeployEnabled;
        _hud.PressNextDemolitionMapForDiagnostics();
        var tideforgeCarousel = _hud.BrowsedDemolitionMapId == DemolitionMapCatalog.TideforgeId
            && _hud.BrowsedDemolitionMapIndex == 1
            && _hud.BrowsedDemolitionMapAvailable
            && _hud.DemolitionBriefingDeployEnabled
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideforgeId;
        _hud.PressNextDemolitionMapForDiagnostics();
        var harborCarousel = _hud.BrowsedDemolitionMapId == DemolitionMapCatalog.HarborLocksId
            && _hud.BrowsedDemolitionMapIndex == 2
            && _hud.BrowsedDemolitionMapAvailable
            && _hud.DemolitionBriefingDeployEnabled
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.HarborLocksId;
        _hud.PressNextDemolitionMapForDiagnostics();
        var tideglassCarousel = _hud.BrowsedDemolitionMapId == DemolitionMapCatalog.TideglassReactorId
            && _hud.BrowsedDemolitionMapIndex == 3
            && _hud.BrowsedDemolitionMapAvailable
            && _hud.DemolitionBriefingDeployEnabled
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideglassReactorId;
        _hud.PressNextDemolitionMapForDiagnostics();
        var lockedCarousel = _hud.BrowsedDemolitionMapId == "drydock_yard"
            && _hud.BrowsedDemolitionMapIndex == 4
            && !_hud.BrowsedDemolitionMapAvailable
            && !_hud.DemolitionBriefingDeployEnabled
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideglassReactorId;
        _hud.PressPreviousDemolitionMapForDiagnostics();
        var carouselReturned = _hud.BrowsedDemolitionMapId == DemolitionMapCatalog.TideglassReactorId
            && _hud.BrowsedDemolitionMapAvailable
            && _hud.DemolitionBriefingDeployEnabled
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideglassReactorId;
        var lockedMapRejected = _hud.PressDemolitionMapForDiagnostics("drydock_yard") == false
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideglassReactorId;
        var bazaarCarousel = _hud.PressDemolitionMapForDiagnostics(
                DemolitionMapCatalog.BazaarCrossingId)
            && _hud.BrowsedDemolitionMapId == DemolitionMapCatalog.BazaarCrossingId
            && _hud.BrowsedDemolitionMapIndex == 0
            && _hud.BrowsedDemolitionMapAvailable
            && _hud.DemolitionBriefingDeployEnabled
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.BazaarCrossingId;
        _hud.PressDemolitionMapForDiagnostics(DemolitionMapCatalog.TideforgeId);
        var synchronizedWithoutDeployment = _hud.SelectedDemolitionRole == OperatorRole.Medic
            && _hud.SelectedDemolitionPrimary == WeaponPlatform.M4A1
            && _hud.SelectedDemolitionBuildTier == 1
            && _hud.SelectedDemolitionSidearm == WeaponPlatform.P226
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
        var requestedSessionMode = -1;
        var requestedAddress = string.Empty;
        var requestedNetworkTeam = -1;
        var addressModes = false;
        var networkLobbyState = false;
        if (probe is not null)
        {
            probe.Visible = false;
            _hud.AddChild(probe);
            probe.BackRequested += () => backRequests++;
            probe.DeployRequested += (role, primary, build, sidearm, mapId, sessionMode, address, networkTeam) =>
            {
                deployRequests++;
                requestedRole = role;
                requestedPrimary = primary;
                requestedBuild = build;
                requestedSidearm = sidearm;
                requestedMap = mapId;
                requestedSessionMode = sessionMode;
                requestedAddress = address;
                requestedNetworkTeam = networkTeam;
            };
            probe.SetLanguage("zh");
            probe.PressRoleForDiagnostics(OperatorRole.Recon);
            probe.PressNextMapForDiagnostics();
            probe.PressPreviousMapForDiagnostics();
            probe.SelectNetworkForDiagnostics(SquadSessionMode.Local, DemolitionNetworkTeam.Alpha);
            var localAddressLocked = !probe.IsNetworkAddressEditable;
            probe.SelectNetworkForDiagnostics(SquadSessionMode.Host, DemolitionNetworkTeam.Alpha);
            var hostAddressEditable = probe.IsNetworkAddressEditable;
            probe.SetNetworkConnectionPending(true, "NETWORK ADDRESS VALIDATION");
            var pendingAddressLocked = !probe.IsNetworkAddressEditable;
            probe.SetNetworkConnectionPending(false, "NETWORK ADDRESS VALIDATION");
            var hostAddressRestored = probe.IsNetworkAddressEditable;
            var lobbyRole = probe.SelectedRole;
            var lobbyMap = probe.SelectedMapId;
            probe.SetNetworkLobbyWaiting(
                host: true,
                players: 2,
                capacity: SquadNetwork.DemolitionCapacity,
                canStart: true,
                status: "NETWORK LOBBY VALIDATION");
            probe.PressRoleForDiagnostics(OperatorRole.Assault);
            probe.PressNextMapForDiagnostics();
            var hostLobbyReady = probe.IsNetworkLobbyWaiting
                && probe.NetworkLobbyPlayerCount == 2
                && probe.NetworkLobbyCanStart
                && probe.IsDeployEnabled
                && !probe.IsNetworkAddressEditable
                && probe.SelectedRole == lobbyRole
                && probe.SelectedMapId == lobbyMap;
            probe.ClearNetworkLobbyWaiting();
            networkLobbyState = hostLobbyReady
                && !probe.IsNetworkLobbyWaiting
                && probe.IsNetworkAddressEditable;
            probe.SetLanRoomBrowseAvailable(true);
            probe.SetLanRooms(new[]
            {
                new LanRoomInfo(
                    "demolition-room",
                    "DEMOLITION HOST",
                    "192.168.10.33",
                    30222,
                    LanRoomKind.Demolition,
                    DemolitionMapCatalog.TideforgeId,
                    2,
                    SquadNetwork.DemolitionCapacity)
            });
            probe.SelectLanRoomForDiagnostics(0);
            var lanRoomSelection = probe.LanRoomBrowserUiReady
                && probe.VisibleLanRoomCount == 1
                && probe.SelectedSessionMode == SquadSessionMode.Join
                && probe.NetworkAddress == "192.168.10.33:30222"
                && probe.SelectedMapId == DemolitionMapCatalog.TideforgeId;
            probe.SelectNetworkForDiagnostics(
                SquadSessionMode.Join,
                DemolitionNetworkTeam.Bravo,
                "192.168.10.25");
            var joinAddressEditable = probe.IsNetworkAddressEditable;
            addressModes = localAddressLocked && hostAddressEditable && pendingAddressLocked
                && hostAddressRestored && joinAddressEditable && lanRoomSelection
                && networkLobbyState;
            probe.PressMapForDiagnostics(DemolitionMapCatalog.TideglassReactorId);
            probe.PressDeployForDiagnostics();
            probe.PressBackForDiagnostics();
        }
        var probeReady = probe is not null
            && probe.SceneFilePath == scenePath
            && probe.UiReady
            && probe.IntentSignalsConnected
            && probe.LanguageMatches("zh")
            && probe.SelectedRole == OperatorRole.Recon
            && probe.SelectedMapId == DemolitionMapCatalog.TideglassReactorId
            && probe.MapOptionCount == DemolitionMapCatalog.PoolSize
            && backRequests == 1
            && deployRequests == 1
            && requestedRole == (int)OperatorRole.Recon
            && requestedPrimary == (int)WeaponPlatform.M4A1
            && requestedBuild == 1
            && requestedSidearm == (int)WeaponPlatform.P226
            && requestedMap == DemolitionMapCatalog.TideglassReactorId
            && requestedSessionMode == (int)SquadSessionMode.Join
            && requestedAddress == "192.168.10.25"
            && requestedNetworkTeam == (int)DemolitionNetworkTeam.Bravo
            && addressModes;
        probe?.QueueFree();
        await WaitFrames(3);

        _hud.PressDemolitionBackForDiagnostics();
        var backReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsDemolitionBriefingVisible
            && !_squadDeployed
            && !_demolitionMode;
        _hud.SetLanguage(originalLanguage);
        _hud.PressDemolitionModeForDiagnostics();
        var bazaarSelectedForDeployment = _hud.PressDemolitionMapForDiagnostics(
            DemolitionMapCatalog.BazaarCrossingId);
        _hud.PressDemolitionDeployForDiagnostics();
        await WaitFrames(5);
        var bazaarDeploymentReady = bazaarSelectedForDeployment
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.BazaarCrossingId
            && _demolitionMode
            && _squadDeployed
            && _demolitionArena?.Layout.MapId == DemolitionMapCatalog.BazaarCrossingId
            && _demolitionArena.Root.Name == "BazaarCrossingArena";
        var valid = sceneReady && chineseReady && englishReady && mapPoolReady && tideforgeCarousel
            && harborCarousel
            && tideglassCarousel && lockedCarousel && carouselReturned && lockedMapRejected
            && bazaarCarousel
            && synchronizedWithoutDeployment && addressModes && networkLobbyState
            && probeReady && backReady && bazaarDeploymentReady;
        GD.Print($"DEMOLITION_BRIEFING_CHECK valid={valid} scene={sceneReady} packed={_hud.DemolitionBriefingUsesPackedScene} ui={_hud.DemolitionBriefingUiReady} signals={_hud.DemolitionBriefingIntentSignalsReady} chinese={chineseReady} english={englishReady} map_pool={mapPoolReady} tideforge={tideforgeCarousel} harbor={harborCarousel} tideglass={tideglassCarousel} bazaar={bazaarCarousel} carousel_locked={lockedCarousel} carousel_return={carouselReturned} locked_rejected={lockedMapRejected} sync={synchronizedWithoutDeployment} address_modes={addressModes} network_lobby={networkLobbyState} probe={probeReady} back={backReady} bazaar_deployment={bazaarDeploymentReady}");
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
        var densityReady = layout.HasDenseCentralCover
            && layout.CentralCoverBodyCount >= DemolitionArenaLayout.MinimumCentralCoverBodyCount
            && layout.CoverPoints.Count >= 28
            && layout.CentralPropsDoNotOverlap;
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
        var routePlanner = new DemolitionRoutePlanner(layout);
        var navigationChecks = layout.SitePositions.Select((site, index) =>
        {
            var attackRoute = routePlanner.Plan(layout.AttackSpawn, site);
            var defenseRoute = routePlanner.Plan(layout.DefenderSpawn, site);
            var attackClear = routePlanner.IsRouteClear(layout.AttackSpawn, attackRoute.Waypoints);
            var defenseClear = routePlanner.IsRouteClear(layout.DefenderSpawn, defenseRoute.Waypoints);
            return new
            {
                Site = index,
                Attack = attackRoute,
                Defense = defenseRoute,
                AttackClear = attackClear,
                DefenseClear = defenseClear,
                Valid = attackRoute.ReachesDestination
                    && defenseRoute.ReachesDestination
                    && attackClear
                    && defenseClear
            };
        }).ToArray();
        var navigationReady = navigationChecks.All(check => check.Valid);
        var navigationDetails = string.Join(",", navigationChecks.Select(check =>
            $"{(char)('A' + check.Site)}:atk={check.Attack.ReachesDestination}/{check.AttackClear}/{check.Attack.Waypoints.Count}:def={check.Defense.ReachesDestination}/{check.DefenseClear}/{check.Defense.Waypoints.Count}"));
        var strategyNavigationChecks = DemolitionArenaLayout.StrategyTargetKeys.Select(key =>
        {
            var target = layout.StrategyTarget(key);
            var attackOwned = key.StartsWith("attack_", System.StringComparison.Ordinal)
                || key.StartsWith("postplant_", System.StringComparison.Ordinal);
            var defenseOwned = key.StartsWith("defense_", System.StringComparison.Ordinal)
                || key.StartsWith("retake_", System.StringComparison.Ordinal)
                || key.StartsWith("site_", System.StringComparison.Ordinal);
            var start = attackOwned ? layout.AttackSpawn : layout.DefenderSpawn;
            var route = routePlanner.Plan(start, target);
            return new
            {
                Key = key,
                Owner = attackOwned ? "atk" : defenseOwned ? "def" : "unknown",
                OwnerKnown = attackOwned != defenseOwned,
                Route = route,
                Clear = routePlanner.IsRouteClear(start, route.Waypoints)
            };
        }).ToArray();
        var strategyNavigationReady = strategyNavigationChecks.All(check =>
            check.OwnerKnown && check.Route.ReachesDestination && check.Clear);
        var blockedStrategyTargets = string.Join(",", strategyNavigationChecks
            .Where(check => !check.OwnerKnown || !check.Route.ReachesDestination || !check.Clear)
            .Select(check => $"{check.Owner}:{check.Key}"));
        var centralCollisionPartNames = new[]
        {
            "MidConverterWestDrum", "MidConverterWestExhaust",
            "MidConverterEastDrum", "MidConverterEastExhaust",
            "MidGantryPipe_01", "MidGantryPipe_02", "MidGantryPipe_03"
        };
        var centralCollisionVisualsReady = centralCollisionPartNames.All(name =>
        {
            var body = arena.Root.GetNodeOrNull<StaticBody3D>(name);
            var mesh = body?.GetNodeOrNull<MeshInstance3D>("Visual")?.Mesh as CylinderMesh;
            var shape = body?.GetNodeOrNull<CollisionShape3D>("Collision")?.Shape as CylinderShape3D;
            return IsInstanceValid(body)
                && mesh is not null
                && shape is not null
                && shape.Radius + 0.001f >= Mathf.Max(mesh.TopRadius, mesh.BottomRadius)
                && Mathf.IsEqualApprox(shape.Height, mesh.Height)
                && body!.CollisionLayer == 1;
        });
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
        var dressingRoot = arena.Root.GetNodeOrNull<Node3D>("DemolitionAuthoredDressing");
        var authoredModelCount = dressingRoot?.GetMeta("authored_model_count").AsInt32() ?? 0;
        var missingModelCount = dressingRoot?.GetMeta("missing_model_count").AsInt32() ?? -1;
        var uniqueSceneCount = dressingRoot?.GetMeta("unique_scene_count").AsInt32() ?? 0;
        var palettedBuildingCount = arena.Root
            .GetMeta("low_poly_paletted_building_count", 0)
            .AsInt32();
        var paletteNodes = arena.Root.FindChildren("*", "Node3D", true, false);
        using var paletteNodesBacking = paletteNodes.AsDisposable();
        var gradientPalettedBuildingCount = paletteNodes
            .OfType<Node3D>()
            .Count(node => node.GetMeta("freight_palette", string.Empty).AsString()
                    == FreightIndustrialPalette.PaletteId
                && node.GetMeta("freight_palette_gradient", false).AsBool()
                && node.GetMeta("freight_palette_gradient_height", 0.0f).AsSingle() > 0.0f);
        var authoredDressingReady = IsInstanceValid(dressingRoot)
            && authoredModelCount >= 45
            && missingModelCount == 0
            && uniqueSceneCount >= 12
            && palettedBuildingCount >= 11
            && gradientPalettedBuildingCount >= 11;

        arena.SetActive(false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var deactivatedCleanly = !arena.Active
            && !arena.Root.Visible
            && arena.Root.ProcessMode == ProcessModeEnum.Disabled
            && arena.ActiveCollisionBodyCount == 0
            && arena.AllStaticBodiesUseWorldLayer();
        var lifecycleReady = initiallyIsolated && collisionReady && deactivatedCleanly;
        var valid = lifecycleReady && routesReady && balanceReady && densityReady && sightlinesBlocked && sitesSeparated
            && extendedTravel && rotationReady && clearanceReady && navigationReady && strategyNavigationReady
            && centralCollisionVisualsReady
            && markersReady && spatialIsolation && sitesReady && authoredDressingReady;
        GD.Print($"DEMOLITION_ARENA_CHECK valid={valid} lifecycle={lifecycleReady} inactive={initiallyIsolated} active={collisionReady} deactivated={deactivatedCleanly} bodies={arena.CollisionBodyCount} visuals={arena.VisualPartCount} routes={routesReady} navigation={navigationReady} navigation_details={navigationDetails} strategy_targets={strategyNavigationReady} strategy_blocked={blockedStrategyTargets} density={densityReady} mid_cover={layout.CentralCoverBodyCount} cover_points={layout.CoverPoints.Count} prop_clear={layout.CentralPropsDoNotOverlap} shaped_cover={centralCollisionVisualsReady} extended={extendedTravel} site_gap={layout.SiteSeparation:0.00} spawn_gap={HorizontalDistance(layout.AttackSpawn, layout.DefenderSpawn):0.00} path_a={layout.AttackToALength:0.00} path_b={layout.AttackToBLength:0.00} difference={layout.SiteTravelDifferenceRatio:P1} sightlines={sightlinesBlocked} rotation={layout.RotationLength:0.00} clearance={clearanceReady} blockers={routeABlocker}|{routeBBlocker}|{routeMidBlocker}|{rotationBlocker} markers={markersReady} isolation={spatialIsolation} sites={sitesReady} authored={authoredDressingReady} authored_models={authoredModelCount} authored_scenes={uniqueSceneCount} missing_models={missingModelCount} paletted_buildings={palettedBuildingCount} gradient_buildings={gradientPalettedBuildingCount}");
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

        ApplyDemolitionLighting();

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
        camera.GlobalPosition = layout.Origin + new Vector3(-84.0f, 18.0f, 88.0f);
        camera.LookAt(layout.Origin + new Vector3(-6.0f, 5.0f, 2.0f), Vector3.Up);
        camera.Fov = 46.0f;
        await WaitFrames(28);
        SaveViewportImage("res://demolition_arena_oblique_validation.png");
        camera.GlobalPosition = layout.AttackSpawn + Vector3.Up * 1.57f;
        camera.LookAt(layout.Midpoint + Vector3.Up * 1.45f, Vector3.Up);
        camera.Fov = 68.0f;
        await WaitFrames(28);
        SaveViewportImage("res://demolition_arena_player_validation.png");
        GD.Print($"DEMOLITION_ARENA_CAPTURE valid={cameraCurrent} camera={cameraCurrent} bodies={_demolitionArena.CollisionBodyCount} visuals={_demolitionArena.VisualPartCount} paths=demolition_arena_validation.png,demolition_arena_oblique_validation.png,demolition_arena_player_validation.png");
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
        _hud.PressDemolitionMapForDiagnostics(DemolitionMapCatalog.TideforgeId);
        var briefingSelection = _hud.SelectedDemolitionRole == OperatorRole.Medic
            && _hud.SelectedDemolitionMapId == DemolitionMapCatalog.TideforgeId;
        _hud.PressDemolitionDeployForDiagnostics();
        await WaitFrames(5);
        var layout = DemolitionLayout();
        var initialDeviceCarrier = ResolveDemolitionAttacker(
            _demolitionDeviceLifecycle.CarrierMemberId);
        var deviceModel = CombatModelLibrary.InspectDemolitionDevice();
        var compactDevice = deviceModel.Loaded
            && deviceModel.ContractValid
            && deviceModel.BoundsSize.X <= 0.38f
            && deviceModel.BoundsSize.Y <= 0.22f
            && deviceModel.BoundsSize.Z <= 0.24f
            && deviceModel.HasEmission;
        var deviceAssignedDuringBuy = _demolitionDeviceLifecycle.IsCarried
            && IsInstanceValid(_demolitionDevice)
            && _demolitionDevice!.Visible
            && _demolitionArena?.Owns(_demolitionDevice) == true
            && IsLivingDemolitionAttacker(initialDeviceCarrier)
            && compactDevice;
        var buyTimerPrecedesRound = _demolitionMode
            && _demolitionBuyPhaseActive
            && !_demolitionRoundActive
            && _hud.IsDemolitionBuyVisible
            && DemolitionPlayerFunds == DemolitionEconomy.StartingFunds
            && Mathf.IsEqualApprox(_demolitionRemaining, DemolitionRoundDuration)
            && _player.KnifeEquipped
            && !_player.HasFireablePrimary
            && !_player.HasSecondaryWeapon
            && !_player.HasSidearmWeapon
            && _player.Grenades == 0
            && Mathf.IsZeroApprox(_player.Armor)
            && _player.Backpack.Count == 0
            && _player.ProcessMode == ProcessModeEnum.Disabled
            && _demolitionOpponents.All(opponent => opponent.ProcessMode == ProcessModeEnum.Disabled)
            && !_hud.IsDemolitionPrimaryOfferEnabled(DemolitionBuyCatalog.Mp5Id)
            && !_hud.IsDemolitionPrimaryOfferEnabled(DemolitionBuyCatalog.M4A1Id)
            && _hud.IsDemolitionSidearmOfferEnabled(DemolitionBuyCatalog.P226Id);
        var opponentOpeningPistols = _demolitionOpponents.All(opponent =>
            opponent.CarriedWeapon.Platform == WeaponPlatform.P226);
        _hud.SelectDemolitionBuySidearmForDiagnostics(DemolitionBuyCatalog.P226Id);
        var openingQuote = _hud.DemolitionBuyQuote;
        _hud.PressDemolitionBuyConfirmForDiagnostics();
        await WaitFrames(3);
        var pistolKit = _player.Role == OperatorRole.Medic
            && !_player.HasFireablePrimary
            && !_player.HasSecondaryWeapon
            && _player.HasSidearmWeapon
            && _player.SidearmWeaponPlatform == WeaponPlatform.P226
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Sidearm
            && _player.HasActiveFirearm
            && _player.CurrentAmmoGrade == LootGrade.Common
            && _player.AmmoReserveFor(AmmoCaliber.Pistol) == 45
            && _player.Grenades == 0
            && Mathf.IsZeroApprox(_player.Armor)
            && _player.Backpack.Count == 1
            && _player.Backpack.All(item => item.Kind == LootItemKind.Ammunition
                && item.AmmoCaliber == AmmoCaliber.Pistol
                && item.Quantity == 45);
        GD.Print($"DEMOLITION_PISTOL_CHECK valid={pistolKit} role={_player.Role} primary={_player.HasFireablePrimary} secondary={_player.HasSecondaryWeapon} sidearm={_player.HasSidearmWeapon} platform={_player.SidearmWeaponPlatform} active={_player.ActiveWeaponSlot} firearm={_player.HasActiveFirearm} grade={_player.CurrentAmmoGrade} reserve={_player.AmmoReserveFor(AmmoCaliber.Pistol)} grenades={_player.Grenades} armor={_player.Armor:0.0} backpack={_player.Backpack.Count}");
        var sidearmEvents = InputMap.ActionGetEvents(GameInputActions.WeaponSidearm);
        using var sidearmEventsBacking = sidearmEvents.AsDisposable();
        var slotBindings = InputMap.HasAction(GameInputActions.WeaponPrimary)
            && InputMap.HasAction(GameInputActions.WeaponSecondary)
            && InputMap.HasAction(GameInputActions.WeaponSidearm)
            && InputMap.HasAction(GameInputActions.WeaponMelee)
            && sidearmEvents.Count > 0;
        _player.SetMagazineAmmoForDiagnostics(11);
        var sidearmFires = _player.FireForDiagnostics() && _player.Ammo == 10;
        _player.SelectWeapon((int)PlayerWeaponSlot.Melee);
        _player.CycleWeapon();
        var weaponSlots = sidearmFires
            && _player.ActiveWeaponSlot == PlayerWeaponSlot.Sidearm
            && _player.EquippedWeapon.Platform == WeaponPlatform.P226
            && _player.Ammo == 10;
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
            && !_hud.IsDemolitionBuyVisible
            && !GetTree().Paused;
        var teamStatusReady = _hud.IsDemolitionTeamStatusVisible
            && _hud.DemolitionTeamStatusUiReady
            && _hud.DemolitionTeamStatusUsesPackedScene
            && _hud.DemolitionFriendlyStatusCount == DemolitionSquadSize
            && _hud.DemolitionEnemyStatusCount == DemolitionSquadSize
            && _hud.DemolitionLocalPlayerMarkerCount == 1
            && _hud.DemolitionDeviceMarkerCount == 1;
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
        var playerBoughtPistol = pistolKit
            && openingQuote.Affordable
            && openingQuote.TotalCost == 500
            && openingQuote.RemainingFunds == 300
            && fundsAfterOpeningBuy == 300;

        var designatedMate = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && !mate.IsDowned
            && !mate.IsBodyBag);
        var nonCarrierPlayerRejected = false;
        var playerPickedUpDevice = false;
        var carrierDropHandoff = false;
        if (designatedMate is not null)
        {
            ForceDemolitionDevicePickupRunnerForDiagnostics(designatedMate);
            _player.GlobalPosition = layout.SitePositions[0] + new Vector3(0.0f, 0.1f, 0.0f);
            _player.Velocity = Vector3.Zero;
            _interactReleaseRequired = false;
            Input.ActionRelease("interact");
            Input.ActionPress("interact");
            UpdateDemolitionInteraction(DemolitionPlantDuration + 0.1f);
            Input.ActionRelease("interact");
            nonCarrierPlayerRejected = !_demolitionDevicePlanted
                && Mathf.IsZeroApprox(_demolitionPlantProgress)
                && _demolitionDeviceLifecycle.IsGrounded;

            ForceDemolitionDevicePickupRunnerForDiagnostics(_player);
            _player.GlobalPosition = _demolitionDeviceGroundPosition - Vector3.Up * 0.16f;
            UpdateDemolitionDeviceLifecycle();
            playerPickedUpDevice = PlayerCarriesDemolitionDevice();
            DropDemolitionDevice(_player);
            var replacement = ResolveDemolitionAttacker(
                _demolitionDeviceLifecycle.PickupRunnerMemberId) as SquadMate;
            if (replacement is not null)
            {
                replacement.GlobalPosition = _demolitionDeviceGroundPosition - Vector3.Up * 0.16f;
                UpdateDemolitionDeviceLifecycle();
                carrierDropHandoff = _demolitionDeviceLifecycle.IsCarried
                    && _demolitionDeviceLifecycle.CarrierMemberId == DemolitionMemberId(replacement)
                    && IsInstanceValid(_demolitionDevice)
                    && _demolitionDevice!.Visible;
            }

            ForceDemolitionDevicePickupRunnerForDiagnostics(_player);
            _player.GlobalPosition = _demolitionDeviceGroundPosition - Vector3.Up * 0.16f;
            UpdateDemolitionDeviceLifecycle();
            playerPickedUpDevice &= PlayerCarriesDemolitionDevice();
        }
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
            && _demolitionDeviceLifecycle.IsPlanted
            && IsInstanceValid(_demolitionDevice)
            && _demolitionArena?.Owns(_demolitionDevice!) == true
            && IsInstanceValid(_demolitionDeviceBeacon)
            && _demolitionDeviceBeacon!.Visible
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
        var runningMate = _squadMates.FirstOrDefault(mate =>
            IsInstanceValid(mate)
            && !mate.IsDowned
            && !mate.IsBodyBag
            && mate.UsesAuthoredOperatorForDiagnostics);
        if (runningMate is not null)
        {
            runningMate.Velocity = Vector3.Right * 4.0f;
            runningMate.SetAuthoredMovementPoseForDiagnostics(4.0f);
        }
        var teammateRunningBeforeRoundEnd = runningMate is not null
            && runningMate.AuthoredAnimationForDiagnostics.Contains(
                "run",
                System.StringComparison.Ordinal);
        var detonationsBefore = _demolitionDetonationCount;
        _demolitionRemaining = 0.05f;
        UpdateDemolitionRound(0.1f);
        var deviceDetonated = _demolitionDetonationCount == detonationsBefore + 1
            && _demolitionDeviceLifecycle.Phase == DemolitionDevicePhase.Detonated
            && IsInstanceValid(_demolitionDevice)
            && !_demolitionDevice!.Visible;
        var playerFundsAfterWin = DemolitionPlayerFunds;
        var opponentFundsAfterLoss = DemolitionOpponentFunds;
        var roundResultPresented = _hud.IsDemolitionRoundResultVisible
            && _hud.DemolitionRoundResultUiReady
            && _hud.DemolitionRoundResultUsesPackedScene
            && _hud.DemolitionRoundResultLanguageReady
            && _hud.DemolitionRoundResultVictory
            && _hud.DemolitionRoundResultScore.Contains("1", System.StringComparison.Ordinal)
            && _hud.DemolitionRoundResultScore.Contains("0", System.StringComparison.Ordinal)
            && Mathf.IsEqualApprox(
                _hud.DemolitionRoundResultSeconds,
                DemolitionIntermissionDuration);
        var actorsFrozenAtRoundEnd = _player.ProcessMode == ProcessModeEnum.Disabled
            && _player.Velocity.LengthSquared() <= 0.0001f
            && _squadMates.Where(IsInstanceValid).All(mate =>
                mate.ProcessMode == ProcessModeEnum.Disabled
                && mate.IsDemolitionRoundFrozenPoseForDiagnostics)
            && _demolitionOpponents.Where(IsInstanceValid).All(opponent =>
                opponent.ProcessMode == ProcessModeEnum.Disabled
                && opponent.Velocity.LengthSquared() <= 0.0001f);
        var roundRecorded = !_missionEnded
            && !_demolitionRoundActive
            && !_hud.IsMissionResultVisible
            && DemolitionPlayerScore == 1
            && DemolitionOpponentScore == 0
            && DemolitionRoundNumber == 2
            && playerFundsAfterWin == System.Math.Min(DemolitionEconomy.MaximumFunds,
                fundsAfterOpeningBuy + DemolitionEconomy.WinReward)
            && opponentFundsAfterLoss == DemolitionEconomy.StartingFunds - 500
                + DemolitionEconomy.LossBaseReward;
        UpdateDemolitionIntermission(DemolitionIntermissionDuration + 0.1f);
        await WaitFrames(3);
        var roundReset = _demolitionBuyPhaseActive
            && !_demolitionRoundActive
            && _hud.IsDemolitionBuyVisible
            && !_hud.IsDemolitionRoundResultVisible
            && !_demolitionDevicePlanted
            && DemolitionRoundNumber == 2
            && DemolitionOpponentCount == 5
            && DemolitionSquadSizeTotal == 5
            && !_player.IsDead
            && Mathf.IsEqualApprox(_player.Health, _player.MaxHealth)
            && _squadMates.Where(IsInstanceValid).All(mate => !mate.IsDowned
                && !mate.IsBodyBag
                && Mathf.IsEqualApprox(mate.Health, mate.MaxHealth))
            && _player.KnifeEquipped
            && !_player.HasFireablePrimary
            && !_player.HasSecondaryWeapon
            && !_player.HasSidearmWeapon
            && DemolitionPlayerFunds == playerFundsAfterWin;

        OnDemolitionPurchaseRequested(string.Empty, string.Empty, false, 0, 0);
        await WaitFrames(2);
        var roundTwoLive = _demolitionRoundActive
            && !_demolitionBuyPhaseActive
            && !_hud.IsDemolitionBuyVisible
            && _player.KnifeEquipped;
        var tacticalAi = ValidateDemolitionTacticalAi(layout);

        // Rounds 2-12 are still the attacking half under MR12: run them out on the clock
        // so the halftime swap hands the player squad the defense in round 13.
        var attackTimeoutFails = false;
        while (!_missionEnded && _demolitionRoundActive && DemolitionRoundNumber <= DemolitionMatchState.RoundsPerHalf)
        {
            var timedRound = DemolitionRoundNumber;
            var playerScoreBeforeTimeout = DemolitionPlayerScore;
            var opponentScoreBeforeTimeout = DemolitionOpponentScore;
            _demolitionRemaining = 0.05f;
            UpdateDemolitionRound(0.1f);
            if (timedRound == 2)
            {
                attackTimeoutFails = !_demolitionRoundActive
                    && DemolitionPlayerSide == DemolitionTeam.Attackers
                    && DemolitionPlayerScore == playerScoreBeforeTimeout
                    && DemolitionOpponentScore == opponentScoreBeforeTimeout + 1;
            }
            if (_missionEnded || _demolitionRoundActive)
            {
                break;
            }
            UpdateDemolitionIntermission(DemolitionIntermissionDuration + 0.1f);
            if (_demolitionBuyPhaseActive)
            {
                OnDemolitionPurchaseRequested(string.Empty, string.Empty, false, 0, 0);
            }
        }
        var defenseRound = await ValidateDemolitionDefenseRound(layout);
        var matchRules = ValidateDemolitionMatchRules();
        var economyRules = ValidateDemolitionEconomyRules();
        var valid = entryButton && briefingSelection && buyTimerPrecedesRound && deviceAssignedDuringBuy
            && opponentOpeningPistols
            && pistolKit && slotBindings && weaponSlots && isolatedEconomy && deployed && teamStatusReady
            && openingStrategy && sitesClear
            && minimapReady && hostileAircraftIsolated && reinforcementsIsolated
            && directorIsolation && playerBoughtPistol && nonCarrierPlayerRejected
            && playerPickedUpDevice && carrierDropHandoff && planted && retakeStrategy && defuseAi
            && deviceDetonated && teammateRunningBeforeRoundEnd && roundResultPresented
            && actorsFrozenAtRoundEnd && roundRecorded && roundReset && roundTwoLive && attackTimeoutFails
            && tacticalAi && defenseRound && matchRules && economyRules;
        GD.Print($"DEMOLITION_CHECK valid={valid} entry_button={entryButton} briefing={briefingSelection} buy_phase={buyTimerPrecedesRound} device_assigned={deviceAssignedDuringBuy} device_compact={compactDevice} initial_carrier={DemolitionMemberId(initialDeviceCarrier)} deployed={deployed} team_status={teamStatusReady} arena={IsDemolitionArenaActive} gameplay={_hud.IsGameplayHudVisible} squad={DemolitionSquadSizeTotal} opponents={DemolitionOpponentCount} opponent_pistols={opponentOpeningPistols} pistol_kit={pistolKit} slots={weaponSlots} bindings={slotBindings} economy={isolatedEconomy} opening_strategy={openingStrategy} retake_strategy={retakeStrategy} assignments={DemolitionStrategyAssignmentCount} minimap={minimapReady} aircraft_isolated={hostileAircraftIsolated} reinforcements_isolated={reinforcementsIsolated} director_isolation={directorIsolation} sites={DemolitionSiteCount} sites_clear={sitesClear} funds_after_buy={fundsAfterOpeningBuy} pistol_buy={playerBoughtPistol} noncarrier_rejected={nonCarrierPlayerRejected} player_pickup={playerPickedUpDevice} handoff={carrierDropHandoff} planted={planted} plant_steps={plantSteps} detonated={deviceDetonated} defuse_ai={defuseAi} defuse_distance={initialDefuserDistance:0.00}->{finalDefuserDistance:0.00} defuse_progress={_demolitionDefuseProgress:0.00} defuse_frames={defuseFrames}/600 teammate_running={teammateRunningBeforeRoundEnd} round_result={roundResultPresented} actors_frozen={actorsFrozenAtRoundEnd} round_recorded={roundRecorded} round_reset={roundReset} round_two_live={roundTwoLive} attack_timeout={attackTimeoutFails} tactical_ai={tacticalAi} defense_round={defenseRound} match_rules={matchRules} economy_rules={economyRules} score={DemolitionPlayerScore}:{DemolitionOpponentScore} round={DemolitionRoundNumber} result={_hud.IsMissionResultVisible}");
        GD.Print($"DEMOLITION_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    /// <summary>
    /// Exercises the tactical AI layer: combat-first arbitration with hysteresis, the
    /// mid-channel guard rule, clock-pressure site switching, detour routing around
    /// blocking walls, and squad post conversion from Move to Hold.
    /// </summary>
    private bool ValidateDemolitionTacticalAi(DemolitionArenaLayout layout)
    {
        if (_missionEnded || !_demolitionRoundActive)
        {
            return false;
        }
        var probe = _demolitionOpponents.FirstOrDefault(opponent => IsInstanceValid(opponent) && !opponent.IsDead);
        if (probe is null || !_demolitionOpponentAssignments.ContainsKey(probe))
        {
            return false;
        }

        var savedPlayerTransform = _player.GlobalTransform;
        var savedPlayerVelocity = _player.Velocity;
        var savedRemaining = _demolitionRemaining;
        var savedTargetSite = _demolitionEnemyTargetSite;
        var savedCarrier = _demolitionCarrier;
        var savedPlantProgress = _demolitionEnemyPlantProgress;
        var savedPlayerPlantProgress = _demolitionPlantProgress;
        var savedPlayerDefuseProgress = _demolitionPlayerDefuseProgress;
        var savedDevicePlanted = _demolitionDevicePlanted;
        var savedDeviceRuntime = CaptureDemolitionDeviceRuntimeForDiagnostics();
        var savedActiveSite = _demolitionActiveSite;
        var savedDefuser = _demolitionDefuser;
        var savedDefuseProgress = _demolitionDefuseProgress;
        var savedStrategyRemaining = _demolitionStrategyRemaining;
        var savedAttackerPlan = _demolitionAttackerPlan;
        var savedDefenderPlan = _demolitionDefenderPlan;
        var savedAttackerPlanObjectiveMemberId = _demolitionAttackerPlanObjectiveMemberId;
        var savedRelayMate = _demolitionSquadObjectiveMate;
        var savedRelaySite = _demolitionSquadObjectiveSite;
        var savedSquadPlantProgress = _demolitionSquadPlantProgress;
        var savedSquadDefuseProgress = _demolitionSquadDefuseProgress;
        var savedPlayerDowned = _localPlayerDowned;
        var savedOpponentAssignments = _demolitionOpponentAssignments.ToArray();
        var savedOpponentRoutes = _demolitionOpponentRoutes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.CloneForDiagnostics());
        var savedSquadTargets = _demolitionSquadAssignmentTargets.ToArray();
        var savedCombatBreakoffs = _demolitionCombatBreakoffs.ToArray();
        var savedSquadCombatBreakoffs = _demolitionSquadCombatBreakoffs.ToArray();
        var savedSquadPostTargets = _demolitionSquadActivePostTargets.ToArray();
        var savedSquadPostTimers = _demolitionSquadPostHoldTimers.ToArray();
        var savedSquadPostSteps = _demolitionSquadPostPatrolSteps.ToArray();
        var savedSquadTrailPaths = _squadTrailPaths.ToDictionary(
            pair => pair.Key,
            pair => new SquadTrailPathState
            {
                Cursor = pair.Value.Cursor,
                EndCursor = pair.Value.EndCursor,
                Direction = pair.Value.Direction,
                Revision = pair.Value.Revision,
                Emergency = pair.Value.Emergency,
                Destination = pair.Value.Destination,
                NextDirectCheckMilliseconds = pair.Value.NextDirectCheckMilliseconds
            });
        var savedSquadRoutes = _demolitionSquadRoutes.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.CloneForDiagnostics());
        var savedSquadRouteFallbacks = _demolitionSquadRouteFallbacks.ToArray();
        var savedEscortProjections = _demolitionEscortProjections.ToArray();
        var savedEscortFanOut = _demolitionEscortOpeningFanOut.ToArray();
        var savedEscortForcedRecoveryRetry = _demolitionEscortForcedRecoveryRetry.ToArray();
        var savedSquadRoutePlans = DemolitionSquadRoutePlansForDiagnostics;
        var savedSquadRouteReuses = DemolitionSquadRouteReusesForDiagnostics;
        var savedEscortProjectionPlans = DemolitionEscortProjectionRoutePlansForDiagnostics;
        var savedEscortProjectionMaximumPlans = DemolitionEscortProjectionMaximumPlansForDiagnostics;
        var savedEscortTotalPlans = DemolitionEscortTotalRoutePlansForDiagnostics;
        var savedEscortMaximumRefreshPlans = DemolitionEscortMaximumRefreshPlansForDiagnostics;
        var savedEscortMaximumRefreshMicroseconds = DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics;
        var savedEscortForcedRecoveryRequests = DemolitionEscortForcedRecoveryRequestsForDiagnostics;
        var savedOpponentNavigation = _demolitionOpponents
            .Where(IsInstanceValid)
            .ToDictionary(
                opponent => opponent,
                opponent => (
                    Transform: opponent.GlobalTransform,
                    opponent.Velocity,
                    Navigation: opponent.CaptureScriptedObjectiveNavigationForDiagnostics(),
                    PlantRuntime: opponent.CaptureDemolitionPlantRuntimeForDiagnostics()));
        var savedSentryModes = _demolitionOpponents
            .Where(IsInstanceValid)
            .ToDictionary(opponent => opponent, opponent => opponent.SentryMode);
        var savedMateStates = _squadMates
            .Where(IsInstanceValid)
            .ToDictionary(
                mate => mate,
                mate => (
                    Transform: mate.GlobalTransform,
                    mate.Velocity,
                    mate.Order,
                    mate.DemolitionOrderPositionForDiagnostics,
                    TacticalState: mate.CaptureDemolitionTacticalStateForDiagnostics()));
        SmokeGrenade? diagnosticSmoke = null;
        _hud.BeginRadioMessageSuppressionForDiagnostics();
        try
        {
            DemolitionEscortProjectionRoutePlansForDiagnostics = 0;
            DemolitionEscortProjectionMaximumPlansForDiagnostics = 0;
            DemolitionEscortTotalRoutePlansForDiagnostics = 0;
            DemolitionEscortMaximumRefreshPlansForDiagnostics = 0;
            DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics = 0;
            DemolitionEscortForcedRecoveryRequestsForDiagnostics = 0;
            // Keep the live cursor object untouched so the diagnostic can restore the
            // exact pre-check route instead of returning a newly planned approximation.
            ResetDemolitionOpponentRoute(probe);
            // A hostile inside the engage bubble makes the objective mover yield to the
            // full combat layer; past the resume ring it takes the objective back over.
            _player.GlobalPosition = probe.GlobalPosition + new Vector3(18.0f, 0.2f, 0.0f);
            var yieldedToCombat = !TryHandleDemolitionDefenderMovement(probe, 0.05f, _player)
                && _demolitionCombatBreakoffs.Contains(probe);
            _player.GlobalPosition = probe.GlobalPosition + new Vector3(34.0f, 0.2f, 0.0f);
            var resumedObjective = TryHandleDemolitionDefenderMovement(probe, 0.05f, _player)
                && !_demolitionCombatBreakoffs.Contains(probe);

            // Smoke breaks stale close-range target arbitration. The objective controller
            // must resume while the combat layer has no visible target through the cloud.
            _player.GlobalPosition = probe.GlobalPosition + new Vector3(18.0f, 0.0f, 0.0f);
            var smokeFrom = probe.GlobalPosition + Vector3.Up * 1.45f;
            var smokeTo = _player.GlobalPosition + Vector3.Up;
            diagnosticSmoke = new SmokeGrenade
            {
                Position = smokeFrom.Lerp(smokeTo, 0.5f) - Vector3.Up * 1.45f
            };
            AddChild(diagnosticSmoke);
            diagnosticSmoke.Arm(Vector3.Forward);
            diagnosticSmoke.BeginGroundFuseForDiagnostics();
            diagnosticSmoke._PhysicsProcess(0.4);
            var smokeBlocksTarget = IsLineObscuredBySmoke(smokeFrom, smokeTo);
            var smokeResumesObjective = smokeBlocksTarget
                && TryHandleDemolitionDefenderMovement(probe, 0.05f, _player)
                && !_demolitionCombatBreakoffs.Contains(probe);
            diagnosticSmoke.RemoveFromGroup(SmokeGrenade.ActiveGroupName);
            diagnosticSmoke.QueueFree();
            diagnosticSmoke = null;

            // A carrier mid-plant keeps channeling while the shooter stays beyond the
            // guard range: the plant progress is preserved rather than reset.
            _demolitionCarrier = probe;
            _demolitionEnemyPlantProgress = 0.3f;
            _player.GlobalPosition = probe.GlobalPosition + new Vector3(18.0f, 0.2f, 0.0f);
            var channelHoldsUnderFire = TryHandleDemolitionDefenderMovement(probe, 0.05f, _player)
                && _demolitionEnemyPlantProgress >= 0.3f;

            // Clock pressure: with the planned site unreachable in the remaining time,
            // the carrier commits to the closest site that still fits the clock.
            var carrierPosition = probe.GlobalPosition;
            var routePlanner = new DemolitionRoutePlanner(layout);
            float TravelSeconds(int siteIndex)
            {
                var route = routePlanner.Plan(
                    carrierPosition,
                    layout.SitePositions[siteIndex],
                    DemolitionTeam.Attackers);
                return route.ReachesDestination
                    ? route.Length / 5.1f + DemolitionPlantDuration
                    : float.PositiveInfinity;
            }
            var orderedSites = layout.SitePositions
                .Select((_, index) => (Index: index, Travel: TravelSeconds(index)))
                .OrderBy(entry => entry.Travel)
                .ToList();
            _demolitionEnemyTargetSite = orderedSites[^1].Index;
            _demolitionEnemyPlantProgress = 0.0f;
            ResetDemolitionOpponentRoute(probe);
            _demolitionRemaining = orderedSites[0].Travel + 2.5f;
            ApplyDemolitionTimePressure();
            var switchedUnderPressure = _demolitionEnemyTargetSite == orderedSites[0].Index;

            // Route planning: a straight corridor through the east route wall must
            // produce a clear multi-waypoint route around the blocking geometry.
            probe.GlobalPosition = layout.Origin + new Vector3(15.0f, 0.2f, 6.0f);
            var detourResult = routePlanner.Plan(probe.GlobalPosition, layout.SitePositions[1]);
            var detourRoutesAroundWall = detourResult.Waypoints.Count >= 2
                && detourResult.ReachesDestination
                && routePlanner.IsRouteClear(probe.GlobalPosition, detourResult.Waypoints);
            ResetDemolitionOpponentRoute(probe);
            MoveDemolitionOpponentAlongRoute(
                probe,
                layout.SitePositions[1],
                "diagnostic_route",
                0.05f,
                2.0f,
                4.8f);
            var runtimeRoute = _demolitionOpponentRoutes.TryGetValue(probe, out var runtimeCursor)
                && runtimeCursor.RouteKey == "diagnostic_route"
                && runtimeCursor.ReachesDestination;

            // A destination embedded inside the foundry core is intentionally unreachable.
            // The planner may return a safe frontier, but never the blocked destination.
            var blockedDestination = layout.Origin + new Vector3(0.0f, 0.2f, -13.5f);
            var unreachableResult = routePlanner.Plan(layout.AttackSpawn, blockedDestination);
            var unreachableSafe = !unreachableResult.ReachesDestination
                && routePlanner.IsRouteClear(layout.AttackSpawn, unreachableResult.Waypoints)
                && unreachableResult.Waypoints.All(point =>
                    HorizontalDistance(point, blockedDestination) > 0.5f);
            var teamAwareUnreachableResult = routePlanner.Plan(
                layout.DefenderSpawn,
                blockedDestination,
                DemolitionTeam.Defenders);
            var teamAwareUnreachableSafe = !teamAwareUnreachableResult.ReachesDestination
                && routePlanner.IsRouteClear(
                    layout.DefenderSpawn,
                    teamAwareUnreachableResult.Waypoints)
                && teamAwareUnreachableResult.Waypoints.All(point =>
                    HorizontalDistance(point, blockedDestination) > 0.5f);
            var softPenaltyStart = layout.AttackSpawn + new Vector3(0.8f, 0.0f, -0.8f);
            var softPenaltyDestination = softPenaltyStart + Vector3.Right * 0.5f;
            var softPenaltyDirectRoute = routePlanner.Plan(
                softPenaltyStart,
                softPenaltyDestination,
                DemolitionTeam.Defenders);
            var softPenaltyDirectReachable = softPenaltyDirectRoute.ReachesDestination
                && softPenaltyDirectRoute.Waypoints.Count == 1
                && softPenaltyDirectRoute.Length <= 0.51f
                && routePlanner.IsRouteClear(
                    softPenaltyStart,
                    softPenaltyDirectRoute.Waypoints);
            var unreachableCursor = new DemolitionRouteCursor();
            unreachableCursor.Reset(
                "blocked_diagnostic",
                layout.AttackSpawn,
                blockedDestination,
                unreachableResult,
                countAsReplan: false);
            foreach (var waypoint in unreachableResult.Waypoints)
            {
                unreachableCursor.Advance(
                    waypoint,
                    DemolitionRouteCornerTolerance,
                    DemolitionRouteCornerTolerance);
            }
            var unreachableRetries = unreachableCursor.Complete
                && unreachableCursor.ShouldRetryUnreachable(0.76f);

            var routeCursor = new DemolitionRouteCursor();
            routeCursor.Reset(
                "diagnostic",
                probe.GlobalPosition,
                layout.SitePositions[1],
                detourResult,
                countAsReplan: false);
            var stalled = routeCursor.TrackMovement(probe.GlobalPosition, 0.81f, movementRequested: true);
            routeCursor.Reset(
                "diagnostic",
                probe.GlobalPosition,
                layout.SitePositions[1],
                detourResult,
                countAsReplan: stalled);
            var routeRecovery = stalled && routeCursor.ReplanCount == 1;

            // A deterministic round preference gives both sites opening variety while
            // real route cost remains a hard sanity check. A grossly longer or unreachable
            // preferred route must lose rather than sending the carrier on a comedy lap.
            var tideglassLayout = new DemolitionArenaLayout(
                DemolitionMapCatalog.TideglassReactorId,
                layout.Origin);
            var tideglassPlanner = new DemolitionRoutePlanner(tideglassLayout);
            var tideglassCarrierSpawn = tideglassLayout.AttackSpawns
                .OrderByDescending(spawn => spawn.X)
                .First();
            var tideglassRoutes = tideglassLayout.SitePositions
                .Select(site => tideglassPlanner.Plan(
                    tideglassCarrierSpawn,
                    site,
                    DemolitionTeam.Attackers))
                .ToArray();
            var tideglassRouteLengths = tideglassRoutes
                .Select(route => route.ReachesDestination
                    ? route.Length
                    : float.PositiveInfinity)
                .ToArray();
            var tideglassCarrier = new List<DemolitionAgentSnapshot>
            {
                new(
                    "CARRIER",
                    DemolitionTeam.Attackers,
                    OperatorRole.Recon,
                    1.0f,
                    165.0f,
                    true,
                    false,
                    tideglassCarrierSpawn.X - tideglassLayout.Origin.X,
                    tideglassCarrierSpawn.Z - tideglassLayout.Origin.Z)
            };
            var roundPreferredSites = Enumerable.Range(0, 2)
                .Select(seed => _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    tideglassCarrier,
                    strategySeed: seed,
                    siteCenters: tideglassLayout.LocalSiteCoordinates,
                    objectiveMemberId: "CARRIER",
                    objectiveRouteLengths: tideglassRouteLengths).PrimarySiteIndex)
                .ToArray();
            var roundSiteVariety = tideglassRoutes.All(route => route.ReachesDestination)
                && roundPreferredSites.OrderBy(site => site).SequenceEqual(new[] { 0, 1 });
            var routeCostOverridesRoundPreference = _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    tideglassCarrier,
                    strategySeed: 1,
                    siteCenters: tideglassLayout.LocalSiteCoordinates,
                    objectiveMemberId: "CARRIER",
                    objectiveRouteLengths: new[] { 10.0f, 80.0f })
                .PrimarySiteIndex == 0;
            var unreachableRouteOverridesRoundPreference = _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    tideglassCarrier,
                    strategySeed: 1,
                    siteCenters: tideglassLayout.LocalSiteCoordinates,
                    objectiveMemberId: "CARRIER",
                    objectiveRouteLengths: new[] { 10.0f, float.PositiveInfinity })
                .PrimarySiteIndex == 0;
            var reachableSiteThreats = Enumerable.Range(0, 5)
                .Select(index => new DemolitionAgentSnapshot(
                    $"UNIQUE_DEFENDER_{index}",
                    DemolitionTeam.Defenders,
                    OperatorRole.Assault,
                    1.0f,
                    100.0f,
                    true,
                    false,
                    tideglassLayout.LocalSiteCoordinates[0].X,
                    tideglassLayout.LocalSiteCoordinates[0].Y))
                .ToArray();
            var unreachableRouteRemainsExcluded = _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    tideglassCarrier,
                    knownOpponents: reachableSiteThreats,
                    strategySeed: 1,
                    siteCenters: tideglassLayout.LocalSiteCoordinates,
                    objectiveMemberId: "CARRIER",
                    objectiveRouteLengths: new[] { 10.0f, float.PositiveInfinity })
                .PrimarySiteIndex == 0;

            // Once committed, an ordinary strategy tick keeps the execute. A defender
            // standing on the selected site is strong enough evidence to justify a rotate.
            var committedSiteStable = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                tideglassCarrier,
                strategySeed: 1,
                siteCenters: tideglassLayout.LocalSiteCoordinates,
                objectiveMemberId: "CARRIER",
                committedSiteIndex: 0,
                objectiveRouteLengths: tideglassRouteLengths).PrimarySiteIndex == 0;
            var siteA = tideglassLayout.LocalSiteCoordinates[0];
            var confirmedSiteAThreat = new List<DemolitionAgentSnapshot>
            {
                new(
                    "DEFENDER_A",
                    DemolitionTeam.Defenders,
                    OperatorRole.Assault,
                    1.0f,
                    100.0f,
                    true,
                    false,
                    siteA.X,
                    siteA.Y)
            };
            var confirmedThreatRotates = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                tideglassCarrier,
                knownOpponents: confirmedSiteAThreat,
                strategySeed: 1,
                siteCenters: tideglassLayout.LocalSiteCoordinates,
                objectiveMemberId: "CARRIER",
                committedSiteIndex: 0,
                objectiveRouteLengths: tideglassRouteLengths).PrimarySiteIndex == 1;
            var duplicateReporterThreats = Enumerable.Range(0, 5)
                .Select(_ => new DemolitionAgentSnapshot(
                    "SAME_TARGET",
                    DemolitionTeam.Defenders,
                    OperatorRole.Assault,
                    1.0f,
                    100.0f,
                    true,
                    false,
                    siteA.X + 19.0f,
                    siteA.Y))
                .ToArray();
            var duplicateThreatReportsDeduplicated = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                tideglassCarrier,
                knownOpponents: duplicateReporterThreats,
                siteCenters: tideglassLayout.LocalSiteCoordinates,
                objectiveMemberId: "CARRIER",
                committedSiteIndex: 0,
                objectiveRouteLengths: new[] { 50.0f, 50.0f }).PrimarySiteIndex == 0;
            var urgentCommitmentPreserved = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                tideglassCarrier,
                strategySeed: 1,
                siteCenters: tideglassLayout.LocalSiteCoordinates,
                remainingSeconds: tideglassRouteLengths[0] / 5.1f + 5.9f,
                objectiveMemberId: "CARRIER",
                committedSiteIndex: 0,
                objectiveRouteLengths: tideglassRouteLengths).PrimarySiteIndex == 0;
            var plantChannelLocksSite = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                tideglassCarrier,
                knownOpponents: confirmedSiteAThreat,
                strategySeed: 1,
                siteCenters: tideglassLayout.LocalSiteCoordinates,
                objectiveMemberId: "CARRIER",
                committedSiteIndex: 0,
                objectiveRouteLengths: tideglassRouteLengths,
                lockCommittedSite: true).PrimarySiteIndex == 0;
            var carrierCommitmentScoped = ShouldRetainDemolitionSiteCommitment(
                    deviceCarried: true,
                    plannedObjectiveMemberId: "CARRIER_A",
                    currentObjectiveMemberId: "CARRIER_A")
                && !ShouldRetainDemolitionSiteCommitment(
                    deviceCarried: true,
                    plannedObjectiveMemberId: "CARRIER_A",
                    currentObjectiveMemberId: "CARRIER_B")
                && !ShouldRetainDemolitionSiteCommitment(
                    deviceCarried: false,
                    plannedObjectiveMemberId: "CARRIER_A",
                    currentObjectiveMemberId: "CARRIER_A");

            // Production routing is validated from every attacker spawn on every map.
            // This exposes the real approach timings (rather than only the legacy authored
            // balance paths), and rejects opposite-side openings, blocked routes, deep
            // objective overshoot, enemy-spawn laps, and excessive visibility-graph detours.
            var tideforgeLayout = new DemolitionArenaLayout(
                DemolitionMapCatalog.TideforgeId,
                layout.Origin);
            var tideforgePlanner = new DemolitionRoutePlanner(tideforgeLayout);
            var tideforgeRightSpawn = tideforgeLayout.AttackSpawns
                .OrderByDescending(spawn => spawn.X)
                .First();
            var tideforgeRightRoute = tideforgePlanner.Plan(
                tideforgeRightSpawn,
                tideforgeLayout.SitePositions[1],
                DemolitionTeam.Attackers);
            var rightLaneOpensRight = tideforgeRightRoute.ReachesDestination
                && tideforgeRightRoute.Waypoints.Count > 0
                && tideforgeRightRoute.Waypoints[0].X >= tideforgeRightSpawn.X - 0.25f;
            var demolitionLayouts = new[]
            {
                tideforgeLayout,
                new DemolitionArenaLayout(DemolitionMapCatalog.HarborLocksId, layout.Origin),
                tideglassLayout,
                new DemolitionArenaLayout(DemolitionMapCatalog.BazaarCrossingId, layout.Origin)
            };
            var productionMapCoverage = demolitionLayouts
                .Select(map => map.MapId)
                .OrderBy(mapId => mapId, StringComparer.Ordinal)
                .SequenceEqual(DemolitionMapCatalog.Maps
                    .Where(map => map.Available)
                    .Select(map => map.Id)
                    .OrderBy(mapId => mapId, StringComparer.Ordinal));
            var productionRoutes = demolitionLayouts.SelectMany(map =>
            {
                var planner = new DemolitionRoutePlanner(map);
                return map.AttackSpawns.SelectMany((spawn, spawnIndex) =>
                    map.SitePositions.Select((site, siteIndex) =>
                    {
                        var route = planner.Plan(spawn, site, DemolitionTeam.Attackers);
                        var directDistance = HorizontalDistance(spawn, site);
                        var approach = siteIndex == 0
                            ? map.AttackApproachToAPath
                            : map.AttackApproachToBPath;
                        var expectedLateral = approach.Count >= 2
                            ? approach[1].X - approach[0].X
                            : site.X - map.AttackSpawn.X;
                        var actualLateral = route.Waypoints.Count > 0
                            ? route.Waypoints[0].X - spawn.X
                            : 0.0f;
                        return new
                        {
                            map.MapId,
                            SpawnIndex = spawnIndex,
                            SiteIndex = siteIndex,
                            Route = route,
                            Clear = planner.IsRouteClear(spawn, route.Waypoints),
                            Stretch = route.Length / Mathf.Max(0.1f, directDistance),
                            DefenderClearance = map.DefenderSpawns.Min(defenderSpawn =>
                                MinimumHorizontalRouteClearance(
                                    spawn,
                                    route.Waypoints,
                                    defenderSpawn)),
                            DepthOvershoot = MaximumAttackerRouteDepthOvershoot(
                                map,
                                spawn,
                                route.Waypoints,
                                site),
                            OpensCorrectSide = Mathf.Abs(actualLateral) <= 0.25f
                                || expectedLateral * actualLateral > 0.0f
                        };
                    }));
            }).ToArray();
            var productionApproachesClear = demolitionLayouts.All(map =>
                map.HasCapsuleClearance(map.AttackApproachToAPath, out _)
                && map.HasCapsuleClearance(map.AttackApproachToBPath, out _));
            var productionEscortPoints = demolitionLayouts.SelectMany(map =>
            {
                var planner = new DemolitionRoutePlanner(map);
                return map.AttackSpawns.SelectMany((carrierSpawn, carrierIndex) =>
                {
                    return map.SitePositions.SelectMany((site, siteIndex) =>
                    {
                        var approach = siteIndex == 0
                            ? map.AttackApproachToAPath
                            : map.AttackApproachToBPath;
                        var laneForward = approach.Count >= 2
                            ? approach[1] - approach[0]
                            : site - carrierSpawn;
                        var forwardVariants = new[]
                        {
                            (Name: "midpoint", Forward: map.Midpoint - carrierSpawn),
                            (Name: "site", Forward: site - carrierSpawn),
                            (Name: "lane", Forward: laneForward)
                        };
                        return forwardVariants.SelectMany(variant =>
                            Enumerable.Range(1, map.AttackSpawns.Count - 1)
                                .Where(escortSlot => escortSlot != carrierIndex)
                                .Select(escortSlot =>
                            {
                                var escortStart = map.AttackSpawns[escortSlot];
                                var preferred = ResolveDemolitionEscortPreferredDestination(
                                    escortSlot,
                                    carrierSpawn,
                                    variant.Forward);
                                var projected = TryProjectDemolitionEscortDestination(
                                    map,
                                    planner,
                                    escortStart,
                                    carrierSpawn,
                                    preferred,
                                    DemolitionTeam.Attackers,
                                    out var resolved,
                                    out var projectionPlans);
                                var usedStrategyFallback = !projected;
                                if (usedStrategyFallback)
                                {
                                    resolved = map.StrategyTarget(siteIndex == 0
                                        ? "attack_support_a"
                                        : "attack_support_b");
                                }
                                var route = planner.Plan(
                                    escortStart,
                                    resolved,
                                    DemolitionTeam.Attackers);
                                return new
                                {
                                    map.MapId,
                                    CarrierIndex = carrierIndex,
                                    CarrierX = carrierSpawn.X,
                                    SiteIndex = siteIndex,
                                    variant.Name,
                                    EscortSlot = escortSlot,
                                    ProjectionPlans = projectionPlans,
                                    TotalPlans = projectionPlans + 1,
                                    Resolved = projected || usedStrategyFallback,
                                    Clear = map.HasCapsulePointClearance(resolved, out _),
                                    Reachable = route.ReachesDestination
                                        && planner.IsRouteClear(escortStart, route.Waypoints),
                                    Moves = resolved.DistanceSquaredTo(escortStart) >= 1.0f
                                };
                            }));
                    });
                });
            }).ToArray();
            var expectedProductionEscortPointCount = demolitionLayouts.Sum(map =>
                (map.AttackSpawns.Count - 1) * (map.AttackSpawns.Count - 1)
                    * map.SitePositions.Count * 3);
            var productionEscortPointsValid = productionEscortPoints.All(point =>
                point.Resolved && point.Clear && point.Reachable && point.Moves)
                && productionEscortPoints.Length == expectedProductionEscortPointCount;
            var productionEscortProjectionBudget = productionEscortPoints.All(point =>
                point.ProjectionPlans is >= 0 and <= DemolitionEscortMaximumProjectionRoutePlans
                    && point.TotalPlans <= DemolitionEscortMaximumTotalRoutePlansPerRefresh);
            var bazaarEscortPoints = productionEscortPoints
                .Where(point => point.MapId == DemolitionMapCatalog.BazaarCrossingId)
                .ToArray();
            var bazaarLeftEdge = bazaarEscortPoints.Min(point => point.CarrierX);
            var bazaarRightEdge = bazaarEscortPoints.Max(point => point.CarrierX);
            var bazaarEdgeEscortPointsValid = bazaarEscortPoints
                .Where(point => Mathf.IsEqualApprox(point.CarrierX, bazaarLeftEdge)
                    || Mathf.IsEqualApprox(point.CarrierX, bazaarRightEdge))
                .All(point => point.Resolved
                    && point.Clear
                    && point.Reachable
                    && point.Moves
                    && point.ProjectionPlans <= DemolitionEscortMaximumProjectionRoutePlans);
            var bazaarWallEdgeLateralRecovery = false;
            var bazaarWallEdgeLateralProfile = "none";
            var bazaarLayout = demolitionLayouts.First(map =>
                map.MapId == DemolitionMapCatalog.BazaarCrossingId);
            var bazaarPlanner = new DemolitionRoutePlanner(bazaarLayout);
            foreach (var wall in bazaarLayout.CollisionBoxes
                         .Where(box => box.Size.Y >= 2.0f
                             && Mathf.Min(box.Size.X, box.Size.Z) <= 0.65f
                             && Mathf.Max(box.Size.X, box.Size.Z) >= 3.0f)
                         .OrderBy(box => box.Name, StringComparer.Ordinal))
            {
                var basis = new Basis(Quaternion.FromEuler(wall.Rotation));
                var longAxis = wall.Size.X >= wall.Size.Z
                    ? basis * Vector3.Right
                    : basis * Vector3.Forward;
                longAxis.Y = 0.0f;
                longAxis = longAxis.Normalized();
                var preferred = wall.Center
                    + longAxis * (Mathf.Max(wall.Size.X, wall.Size.Z) * 0.5f - 0.45f);
                preferred.Y = bazaarLayout.AttackSpawn.Y;
                if (bazaarLayout.HasCapsulePointClearance(preferred, out _))
                {
                    continue;
                }
                var start = bazaarLayout.AttackSpawns
                    .OrderBy(spawn => spawn.DistanceSquaredTo(preferred))
                    .First();
                var projected = TryProjectDemolitionEscortDestination(
                    bazaarLayout,
                    bazaarPlanner,
                    start,
                    preferred + longAxis,
                    preferred,
                    DemolitionTeam.Attackers,
                    out var resolved,
                    out var projectionPlans);
                var lateral = new Vector3(-longAxis.Z, 0.0f, longAxis.X);
                var displacement = resolved - preferred;
                var lateralDistance = Mathf.Abs(displacement.Dot(lateral));
                var forwardDistance = Mathf.Abs(displacement.Dot(longAxis));
                if (!projected
                    || lateralDistance < 0.75f
                    || forwardDistance > 0.25f
                    || projectionPlans > DemolitionEscortMaximumProjectionRoutePlans)
                {
                    continue;
                }
                bazaarWallEdgeLateralRecovery = true;
                bazaarWallEdgeLateralProfile = $"{wall.Name}:{projectionPlans}:{lateralDistance:0.00}";
                break;
            }
            var expectedProductionRouteCount = demolitionLayouts.Sum(map =>
                map.AttackSpawns.Count * map.SitePositions.Count);
            var productionRoutesValid = productionMapCoverage
                && productionRoutes.Length == expectedProductionRouteCount
                && productionRoutes.All(route => route.Route.ReachesDestination && route.Clear);
            var productionRoutesOpenCorrectSide = productionRoutes.All(route => route.OpensCorrectSide);
            var productionRoutesDepthSafe = productionRoutes.All(route => route.DepthOvershoot <= 0.25f);
            var productionRoutesEfficient = productionRoutes.All(route => route.Stretch <= 1.35f);
            var attackerRoutesAvoidDefenderSpawn = productionRoutes.All(route =>
                route.DefenderClearance >= 10.0f);
            var productionRouteProfiles = string.Join(",", demolitionLayouts.Select(map =>
            {
                var routes = productionRoutes.Where(route => route.MapId == map.MapId).ToArray();
                var siteA = routes.Where(route => route.SiteIndex == 0).Select(route => route.Route.Length).ToArray();
                var siteB = routes.Where(route => route.SiteIndex == 1).Select(route => route.Route.Length).ToArray();
                return $"{map.MapId}:A={siteA.Min():0.0}-{siteA.Max():0.0}/B={siteB.Min():0.0}-{siteB.Max():0.0}";
            }));
            var defenderProductionRoutes = demolitionLayouts.SelectMany(map =>
            {
                var planner = new DemolitionRoutePlanner(map);
                return map.DefenderSpawns.SelectMany((spawn, spawnIndex) =>
                    map.SitePositions.Select((site, siteIndex) =>
                    {
                        var route = planner.Plan(spawn, site, DemolitionTeam.Defenders);
                        return new
                        {
                            map.MapId,
                            SpawnIndex = spawnIndex,
                            SiteIndex = siteIndex,
                            Route = route,
                            Clear = planner.IsRouteClear(spawn, route.Waypoints),
                            Stretch = route.Length / Mathf.Max(0.1f, HorizontalDistance(spawn, site)),
                            AttackerClearance = map.AttackSpawns.Min(attackerSpawn =>
                                MinimumHorizontalRouteClearance(
                                    spawn,
                                    route.Waypoints,
                                    attackerSpawn))
                        };
                    }));
            }).ToArray();
            var expectedDefenderRouteCount = demolitionLayouts.Sum(map =>
                map.DefenderSpawns.Count * map.SitePositions.Count);
            var defenderProductionRoutesValid = defenderProductionRoutes.Length == expectedDefenderRouteCount
                && defenderProductionRoutes.All(route => route.Route.ReachesDestination && route.Clear);
            var defenderProductionRoutesEfficient = defenderProductionRoutes.All(route =>
                route.Stretch <= 1.40f);
            var defenderRoutesAvoidAttackerSpawn = defenderProductionRoutes.All(route =>
                route.AttackerClearance >= 10.0f);
            var tideforgeSelectionStable = tideforgeLayout.AttackSpawns.All(spawn =>
            {
                var routeLengths = tideforgeLayout.SitePositions.Select(site =>
                {
                    var route = tideforgePlanner.Plan(spawn, site, DemolitionTeam.Attackers);
                    return route.ReachesDestination ? route.Length : float.PositiveInfinity;
                }).ToArray();
                var carrier = new[]
                {
                    new DemolitionAgentSnapshot(
                        "TIDEFORGE_CARRIER",
                        DemolitionTeam.Attackers,
                        OperatorRole.Assault,
                        1.0f,
                        100.0f,
                        true,
                        false,
                        spawn.X - tideforgeLayout.Origin.X,
                        spawn.Z - tideforgeLayout.Origin.Z)
                };
                var seededSites = Enumerable.Range(0, 2).Select(seed =>
                    _demolitionStrategyPlanner.Plan(
                        DemolitionTeam.Attackers,
                        DemolitionStrategyPhase.Opening,
                        carrier,
                        strategySeed: seed,
                        siteCenters: tideforgeLayout.LocalSiteCoordinates,
                        objectiveMemberId: "TIDEFORGE_CARRIER",
                        objectiveRouteLengths: routeLengths).PrimarySiteIndex).ToArray();
                var committedA = _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    carrier,
                    siteCenters: tideforgeLayout.LocalSiteCoordinates,
                    objectiveMemberId: "TIDEFORGE_CARRIER",
                    committedSiteIndex: 0,
                    objectiveRouteLengths: routeLengths).PrimarySiteIndex;
                var committedB = _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    carrier,
                    siteCenters: tideforgeLayout.LocalSiteCoordinates,
                    objectiveMemberId: "TIDEFORGE_CARRIER",
                    committedSiteIndex: 1,
                    objectiveRouteLengths: routeLengths).PrimarySiteIndex;
                return seededSites.OrderBy(site => site).SequenceEqual(new[] { 0, 1 })
                    && committedA == 0
                    && committedB == 1;
            });
            var overshotStart = tideforgeLayout.Origin + new Vector3(-23.0f, 0.2f, 10.0f);
            var overshotRetreatRoute = tideforgePlanner.Plan(
                overshotStart,
                tideforgeLayout.SitePositions[0],
                DemolitionTeam.Attackers);
            var overshotCarrierRetreats = overshotRetreatRoute.ReachesDestination
                && tideforgePlanner.IsRouteClear(overshotStart, overshotRetreatRoute.Waypoints)
                && overshotRetreatRoute.Waypoints.Count > 0
                && MaximumAdditionalAttackerRouteDepth(
                    tideforgeLayout,
                    overshotStart,
                    overshotRetreatRoute.Waypoints) <= 0.25f;
            // Squad posts: a mate standing on the assignment target converts its Move
            // order into Hold so it anchors the position instead of milling around.
            var postedMate = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && _demolitionSquadAssignmentTargets.ContainsKey(mate));
            var postsConverted = false;
            if (postedMate is not null
                && _demolitionSquadAssignmentTargets.TryGetValue(postedMate, out var targetKey))
            {
                postedMate.GlobalPosition = DemolitionLayout().StrategyTarget(targetKey);
                UpdateDemolitionSquadPosts(ignoreEscort: true);
                postsConverted = postedMate.Order == SquadOrder.Hold;
            }

            // Explosion-map squad navigation must leave the off-map generic grid and
            // return a cached authored-lane waypoint instead of holding at spawn.
            var squadRouteNavigation = false;
            var squadRouteReuse = false;
            var frontierFallback = false;
            var frontierFallbackStable = false;
            var reachableRoutePreserved = false;
            var frontierReplans = -1;
            var frontierDirectiveDistance = 0.0f;
            var routeMate = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && !mate.IsHumanProxy
                && !mate.IsDowned
                && !mate.IsBodyBag);
            if (routeMate is not null)
            {
                routeMate.GlobalPosition = layout.AttackSpawn;
                routeMate.Velocity = Vector3.Zero;
                ClearSquadNavigation(routeMate);
                var routePlansBefore = DemolitionSquadRoutePlansForDiagnostics;
                var routeDirective = ResolveSquadNavigationDestination(
                    routeMate,
                    layout.SitePositions[0],
                    emergency: false);
                var routePlansAfter = DemolitionSquadRoutePlansForDiagnostics;
                var routeReusesBefore = DemolitionSquadRouteReusesForDiagnostics;
                _ = ResolveSquadNavigationDestination(
                    routeMate,
                    layout.SitePositions[0],
                    emergency: false);
                squadRouteNavigation = routeDirective.Target.DistanceTo(routeMate.GlobalPosition) > 0.25f;
                squadRouteReuse = routePlansAfter == routePlansBefore + 1
                    && DemolitionSquadRouteReusesForDiagnostics > routeReusesBefore;
                reachableRoutePreserved = _demolitionSquadRoutes.TryGetValue(
                        routeMate,
                        out var reachableCursor)
                    && reachableCursor.ReachesDestination
                    && reachableCursor.ReplanCount == 0
                    && !_demolitionSquadRouteFallbacks.ContainsKey(routeMate);

                // An unreachable frontier is allowed a few cheap authored-route retries,
                // then must yield to the shared trail/grid navigator.  The fallback is
                // destination-scoped so the failed arena route is not rebuilt forever.
                routeMate.GlobalPosition = layout.AttackSpawn;
                routeMate.Velocity = Vector3.Zero;
                ClearSquadNavigation(routeMate);
                ClearDemolitionSquadRouteFallback(routeMate);
                for (var frame = 0; frame < 220; frame++)
                {
                    var fallbackDirective = ResolveSquadNavigationDestination(
                        routeMate,
                        blockedDestination,
                        emergency: false);
                    frontierDirectiveDistance = fallbackDirective.Target.DistanceTo(
                        routeMate.GlobalPosition);
                    if (_demolitionSquadRouteFallbacks.TryGetValue(
                            routeMate,
                            out var fallbackState))
                    {
                        frontierFallback = true;
                        frontierReplans = fallbackState.ReplanCount;
                        break;
                    }
                }
                if (frontierFallback)
                {
                    var plansAtFallback = DemolitionSquadRoutePlansForDiagnostics;
                    _ = ResolveSquadNavigationDestination(
                        routeMate,
                        blockedDestination,
                        emergency: false);
                    frontierFallbackStable = plansAtFallback
                            == DemolitionSquadRoutePlansForDiagnostics
                        && !_demolitionSquadRoutes.ContainsKey(routeMate);
                }
            }

            // Shared intelligence: the pure planner must avoid a site stacked with known
            // defenders and the world blackboard must surface alerted opponents.
            var siteCenters = DemolitionArenaLayout.LocalSiteCenters;
            var stackedDefenders = new List<DemolitionAgentSnapshot>();
            var farDefenders = new List<DemolitionAgentSnapshot>();
            var stackedCenter = siteCenters[0];
            for (var index = 0; index < 3; index++)
            {
                stackedDefenders.Add(new DemolitionAgentSnapshot(
                    $"D{index}", DemolitionTeam.Defenders, OperatorRole.Assault,
                    1.0f, 100.0f, true, false, stackedCenter.X + index * 2.0f, stackedCenter.Y));
            }
            var farCenter = siteCenters[1];
            farDefenders.Add(new DemolitionAgentSnapshot(
                "D9", DemolitionTeam.Defenders, OperatorRole.Assault, 1.0f, 100.0f, true, false,
                farCenter.X, farCenter.Y));
            var attackers = new List<DemolitionAgentSnapshot> { new(
                "A0", DemolitionTeam.Attackers, OperatorRole.Assault, 1.0f, 100.0f, true, false, 0.0f, 40.0f) };
            var plannerAvoidsStackedSite = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                attackers,
                -1,
                stackedDefenders).PrimarySiteIndex == 1
                && _demolitionStrategyPlanner.Plan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    attackers,
                    -1,
                    farDefenders).PrimarySiteIndex == 0;

            var openingTeam = new List<DemolitionAgentSnapshot>();
            for (var index = 0; index < 5; index++)
            {
                openingTeam.Add(new DemolitionAgentSnapshot(
                    $"A{index}",
                    DemolitionTeam.Attackers,
                    index == 0 ? OperatorRole.Assault : index == 1 ? OperatorRole.Recon : OperatorRole.Medic,
                    1.0f,
                    90.0f + index * 12.0f,
                    true,
                    false,
                    index * 1.5f,
                    48.0f));
            }
            var fullExecutePlan = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                openingTeam,
                strategySeed: 1);
            var splitPlan = _demolitionStrategyPlanner.Plan(
                DemolitionTeam.Attackers,
                DemolitionStrategyPhase.Opening,
                openingTeam,
                strategySeed: 3);
            var openingPatterns = fullExecutePlan.OpeningPattern == DemolitionOpeningPattern.FullExecute
                && fullExecutePlan.Assignments.All(assignment => assignment.SiteIndex == fullExecutePlan.PrimarySiteIndex)
                && splitPlan.OpeningPattern == DemolitionOpeningPattern.SplitPressure
                && splitPlan.Assignments.Any(assignment => assignment.SiteIndex != splitPlan.PrimarySiteIndex);
            var objectiveChannels = ValidateDemolitionObjectiveChannelCoordinator();
            var blackboardSeesAlertedOpponents = false;
            EnemyOperator? blackboardProbe = null;
            try
            {
                blackboardProbe = new EnemyOperator
                {
                    Name = "DemolitionBlackboardProbe",
                    Position = layout.Midpoint,
                    Player = _player,
                    Main = this,
                    MissionDirector = _missionDirector,
                    ProcessMode = ProcessModeEnum.Disabled
                };
                AddChild(blackboardProbe);
                blackboardProbe.SetPhysicsProcess(false);
                blackboardProbe.CollisionLayer = 0;
                blackboardProbe.CollisionMask = 0;
                blackboardProbe.SetAlerted(blackboardProbe.GlobalPosition);
                _demolitionOpponents.Add(blackboardProbe);
                var blackboard = CollectDemolitionSightings(
                    DemolitionTeam.Defenders, _demolitionMatch.PlayerSide, layout);
                blackboardSeesAlertedOpponents = blackboard.Any(sighting =>
                    sighting.MemberId == $"KNOWN:{blackboardProbe.Name}");
            }
            finally
            {
                if (blackboardProbe is not null)
                {
                    _demolitionOpponents.Remove(blackboardProbe);
                    _demolitionOpponentAssignments.Remove(blackboardProbe);
                    _demolitionOpponentRoutes.Remove(blackboardProbe);
                    _demolitionCombatBreakoffs.Remove(blackboardProbe);
                    if (IsInstanceValid(blackboardProbe))
                    {
                        blackboardProbe.QueueFree();
                    }
                }
            }

            // Device hand-off: once the local carrier is lost, the closest living mate
            // is assigned to the dropped device and receives objective-priority movement.
            var relay = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && !mate.IsDowned);
            var relayTakesOver = false;
            if (relay is not null)
            {
                foreach (var mate in _squadMates.Where(mate => IsInstanceValid(mate) && mate != relay))
                {
                    mate.GlobalPosition = layout.Origin + new Vector3(24.0f, 0.2f, 24.0f + mate.SquadSlot);
                }
                _player.GlobalPosition = layout.AttackSpawn;
                ForceDemolitionDeviceCarrierForDiagnostics(_player);
                _localPlayerDowned = true;
                relay.GlobalPosition = _player.GlobalPosition + new Vector3(2.0f, 0.0f, 0.0f);
                DropDemolitionDevice(_player);
                UpdateDemolitionSquadObjectiveRelay(0.05f);
                relayTakesOver = ReferenceEquals(_demolitionSquadObjectiveMate, relay)
                    && _demolitionDeviceLifecycle.PickupRunnerMemberId == DemolitionMemberId(relay)
                    && relay.Order == SquadOrder.Move
                    && relay.DemolitionOrderPositionForDiagnostics.DistanceTo(
                        _demolitionDeviceGroundPosition) <= 0.05f;
            }

            // A stale plan can still name a carrier that died between refreshes. Once the
            // channel owner is gone, selection transfers immediately and restarts progress.
            var replacementCarrier = _demolitionOpponents.FirstOrDefault(opponent =>
                IsInstanceValid(opponent) && !opponent.IsDead && opponent != probe);
            var carrierTransferResetsChannel = false;
            if (replacementCarrier is not null)
            {
                var transferPlan = new DemolitionStrategyPlan(
                    DemolitionTeam.Attackers,
                    DemolitionStrategyPhase.Opening,
                    0,
                    DemolitionOpeningPattern.FullExecute,
                    new[]
                    {
                        new DemolitionAssignment("missing_carrier", DemolitionDuty.Entry, 0,
                            "attack_entry_a", "diagnostic missing carrier"),
                        new DemolitionAssignment(replacementCarrier.Name, DemolitionDuty.Flank, 0,
                            "attack_entry_a", "diagnostic live flank replacement")
                    },
                    "DIAGNOSTIC CARRIER TRANSFER");
                _demolitionCarrier = null;
                _demolitionEnemyPlantProgress = 0.35f;
                SelectDemolitionCarrier(transferPlan);
                carrierTransferResetsChannel = _demolitionCarrier == replacementCarrier
                    && Mathf.IsZeroApprox(_demolitionEnemyPlantProgress);
            }

            // A live defuser that has started the channel owns it across the 1.5-second
            // tactical refresh. Replanning may change everyone else, but cannot erase a
            // seven-second objective channel because health or positioning scores moved.
            var progressBeforeRefresh = 0.35f;
            var strategyRefreshPreservesChannel = false;
            var strategyRefreshReassignsLostChannel = false;
            var strategyRefreshClearsChangedPhase = false;
            _demolitionDevicePlanted = true;
            _demolitionActiveSite = 0;
            _demolitionDefuseProgress = 0.0f;
            _demolitionDefuser = null;
            RefreshDemolitionStrategies(false);
            var plannedDefuser = _demolitionDefuser;
            var stickyDefuser = _demolitionOpponents.FirstOrDefault(opponent =>
                IsInstanceValid(opponent)
                && !opponent.IsDead
                && opponent != plannedDefuser);
            if (stickyDefuser is not null)
            {
                _demolitionDefuser = stickyDefuser;
                _demolitionDefuseProgress = progressBeforeRefresh;
                RefreshDemolitionStrategies(false);
                strategyRefreshPreservesChannel = _demolitionDefuser == stickyDefuser
                    && Mathf.IsEqualApprox(_demolitionDefuseProgress, progressBeforeRefresh)
                    && _demolitionOpponentAssignments.TryGetValue(stickyDefuser, out var stickyAssignment)
                    && stickyAssignment.Duty == DemolitionDuty.Defuse;

                _demolitionDefuser = null;
                _demolitionDefuseProgress = progressBeforeRefresh;
                RefreshDemolitionStrategies(false);
                var replacementDefuser = _demolitionDefuser;
                strategyRefreshReassignsLostChannel = IsInstanceValid(replacementDefuser)
                    && replacementDefuser != stickyDefuser
                    && Mathf.IsZeroApprox(_demolitionDefuseProgress)
                    && _demolitionOpponentAssignments.TryGetValue(replacementDefuser!, out var replacementAssignment)
                    && replacementAssignment.Duty == DemolitionDuty.Defuse;

                _demolitionDefuseProgress = progressBeforeRefresh;
                _demolitionDevicePlanted = false;
                _demolitionActiveSite = -1;
                RefreshDemolitionStrategies(false);
                strategyRefreshClearsChangedPhase = _demolitionDefuser is null
                    && Mathf.IsZeroApprox(_demolitionDefuseProgress);
            }

            var friendlyAiPlantsDevice = false;
            if (relay is not null && _demolitionAttackerPlan is not null)
            {
                _localPlayerDowned = false;
                _demolitionDevicePlanted = false;
                _demolitionActiveSite = -1;
                _demolitionSquadObjectiveSite = -1;
                _demolitionSquadPlantProgress = 0.0f;
                // Preserve the relay's combat target and timers. One visible defender
                // remains close enough to prove contact cannot cancel the carrier's plant.
                for (var index = 0; index < _demolitionOpponents.Count; index++)
                {
                    var opponent = _demolitionOpponents[index];
                    if (!IsInstanceValid(opponent))
                    {
                        continue;
                    }
                    opponent.GlobalPosition = layout.AttackSpawn
                        + new Vector3((index - 2) * 1.5f, 0.2f, 0.0f);
                    opponent.Velocity = Vector3.Zero;
                }
                ForceDemolitionDeviceCarrierForDiagnostics(relay);
                var friendlySiteIndex = Mathf.Clamp(
                    _demolitionAttackerPlan.PrimarySiteIndex,
                    0,
                    layout.SitePositions.Count - 1);
                relay.GlobalPosition = layout.SitePositions[friendlySiteIndex]
                    + new Vector3(0.0f, 0.2f, 0.4f);
                relay.Velocity = Vector3.Zero;
                probe.GlobalPosition = relay.GlobalPosition + Vector3.Right * 4.0f;
                probe.Velocity = Vector3.Zero;
                relay.SetDemolitionThreatForDiagnostics(
                    probe,
                    hasSight: true,
                    threatAge: 0.0f);
                TryUpdateDemolitionSquadDeviceObjective(DemolitionPlantDuration + 0.1f);
                friendlyAiPlantsDevice = _demolitionDevicePlanted
                    && _demolitionDeviceLifecycle.IsPlanted
                    && _demolitionActiveSite == friendlySiteIndex;
                relay.SetDemolitionThreatForDiagnostics(
                    null,
                    hasSight: false,
                    threatAge: 100.0f);
            }

            // Deterministic ownership arbitration: an AI carrier must be the anchor for
            // its escorts, while a player carrier remains a valid fallback leader.  This
            // catches the old `as SquadMate` lookup that silently returned to formation.
            var carrierDestinationFollowsDevice = false;
            var carrierThreatKeepsObjective = false;
            var aiCarrierEscort = false;
            var playerCarrierEscort = false;
            var openingEscortFansOut = false;
            var openingEscortFanOutLatched = false;
            var blockedEscortFallsBack = false;
            var escortFallbackMoves = false;
            var escortFallbackSupported = false;
            var escortRouteRejectsVoid = false;
            var physicalEscortLanding = false;
            var runtimeEscortProjectionBudget = false;
            var runtimeEscortTotalBudget = false;
            var noSafeEscortHolds = false;
            var noSafeEscortRetries = false;
            var groundedPlayerRunnerReleased = false;
            var hiddenThreatIgnored = false;
            var visibleThreatYields = false;
            var postPatrolReactivated = false;
            var postPatrolSecondHop = false;
            var staleMoveRecovered = false;
            var defuserPostProtected = false;
            var postPatrolLayouts = ValidateDemolitionPostPatrolLayout(layout)
                && ValidateDemolitionPostPatrolLayout(
                    new DemolitionArenaLayout(
                        DemolitionMapCatalog.HarborLocksId,
                        layout.Origin))
                && ValidateDemolitionPostPatrolLayout(
                    new DemolitionArenaLayout(
                        DemolitionMapCatalog.TideglassReactorId,
                        layout.Origin))
                && ValidateDemolitionPostPatrolLayout(
                    new DemolitionArenaLayout(
                        DemolitionMapCatalog.BazaarCrossingId,
                        layout.Origin));
            var aiCarrier = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && !mate.IsHumanProxy
                && !mate.IsDowned
                && !mate.IsBodyBag);
            var escortMate = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
                && mate != aiCarrier
                && !mate.IsHumanProxy
                && !mate.IsDowned
                && !mate.IsBodyBag);
            if (aiCarrier is not null && escortMate is not null)
            {
                _demolitionDevicePlanted = false;
                aiCarrier.GlobalPosition = layout.AttackSpawn;
                escortMate.GlobalPosition = layout.AttackSpawn + new Vector3(8.0f, 0.0f, 0.0f);
                aiCarrier.SetDemolitionThreatForDiagnostics(null, hasSight: false, threatAge: 100.0f);
                escortMate.SetDemolitionThreatForDiagnostics(null, hasSight: false, threatAge: 100.0f);
                _demolitionSquadCombatBreakoffs.Remove(aiCarrier);
                _demolitionSquadCombatBreakoffs.Remove(escortMate);
                ForceDemolitionDeviceCarrierForDiagnostics(aiCarrier);
                var plannedSite = Mathf.Clamp(
                    _demolitionAttackerPlan?.PrimarySiteIndex ?? 0,
                    0,
                    layout.SitePositions.Count - 1);
                carrierDestinationFollowsDevice = TryGetDemolitionObjectiveDestination(
                    aiCarrier,
                    out var carrierDestination)
                    && carrierDestination.DistanceTo(layout.SitePositions[plannedSite]) < 0.5f;
                probe.GlobalPosition = aiCarrier.GlobalPosition + Vector3.Right * 5.0f;
                probe.Velocity = Vector3.Zero;
                aiCarrier.SetDemolitionThreatForDiagnostics(
                    probe,
                    hasSight: true,
                    threatAge: 0.0f);
                carrierThreatKeepsObjective = TryGetDemolitionObjectiveDestination(
                        aiCarrier,
                        out var contactedCarrierDestination)
                    && contactedCarrierDestination.DistanceTo(layout.SitePositions[plannedSite]) < 0.5f;
                aiCarrier.SetDemolitionThreatForDiagnostics(
                    null,
                    hasSight: false,
                    threatAge: 100.0f);
                aiCarrierEscort = TryGetDemolitionEscortTarget(escortMate, out var aiLeader)
                    && ReferenceEquals(aiLeader, aiCarrier);

                var escortStrategyKey = _demolitionSquadAssignmentTargets.TryGetValue(
                    escortMate,
                    out var assignedEscortStrategy)
                        ? assignedEscortStrategy
                        : "attack_support_a";
                _demolitionSquadAssignmentTargets[escortMate] = escortStrategyKey;
                var escortStrategyTarget = layout.StrategyTarget(escortStrategyKey);
                var edgeSpawns = layout.AttackSpawns
                    .OrderBy(spawn => spawn.X)
                    .ToArray();
                aiCarrier.GlobalPosition = edgeSpawns[0];
                escortMate.GlobalPosition = edgeSpawns[^1];
                ClearSquadNavigation(escortMate);
                var openingPreferred = aiCarrier.GlobalPosition
                    + new Vector3(-4.0f, 0.0f, 2.0f);
                openingEscortFansOut = TryResolveDemolitionEscortDestination(
                        escortMate,
                        aiCarrier,
                        openingPreferred,
                        out var openingResolved)
                    && openingResolved.DistanceTo(escortStrategyTarget) < 0.25f;
                escortMate.GlobalPosition = escortStrategyTarget;
                var fanOutEgressDirection = edgeSpawns[0] - layout.AttackSpawn;
                fanOutEgressDirection.Y = 0.0f;
                fanOutEgressDirection = fanOutEgressDirection.LengthSquared() > 0.01f
                    ? fanOutEgressDirection.Normalized()
                    : Vector3.Left;
                aiCarrier.GlobalPosition = edgeSpawns[0]
                    + fanOutEgressDirection * 8.5f;
                ClearSquadNavigation(escortMate);
                var latchedEscortResolved = TryResolveDemolitionEscortDestination(
                    escortMate,
                    aiCarrier,
                    openingPreferred,
                    out var latchedResolved);
                openingEscortFanOutLatched = layout.AttackSpawns.All(spawn =>
                        HorizontalDistance(spawn, aiCarrier.GlobalPosition)
                            > DemolitionEscortOpeningFanOutEnterRadius)
                    && latchedEscortResolved
                    && latchedResolved.DistanceTo(escortStrategyTarget) < 0.25f;

                aiCarrier.GlobalPosition = layout.Midpoint;
                escortMate.GlobalPosition = layout.AttackSpawn;
                ClearSquadNavigation(escortMate);
                var blockedPreferred = layout.Origin + new Vector3(400.0f, 0.2f, 400.0f);
                blockedEscortFallsBack = TryResolveDemolitionEscortDestination(
                        escortMate,
                        aiCarrier,
                        blockedPreferred,
                        out var blockedResolved)
                    && blockedResolved.DistanceTo(escortStrategyTarget) < 0.25f;
                var fallbackDirective = ResolveSquadNavigationDestination(
                    escortMate,
                    blockedResolved,
                    emergency: false);
                escortFallbackMoves = fallbackDirective.Target.DistanceSquaredTo(
                    escortMate.GlobalPosition) >= 0.25f;
                var validatedFallbackRoute = routePlanner.Plan(
                    escortMate.GlobalPosition,
                    blockedResolved,
                    DemolitionTeam.Attackers);
                escortFallbackSupported = TryGroundDemolitionEscortPoint(
                        escortMate,
                        blockedResolved,
                        out var groundedFallbackDirective)
                    && groundedFallbackDirective.DistanceTo(blockedResolved) < 0.12f
                    && validatedFallbackRoute.ReachesDestination
                    && routePlanner.IsRouteClear(
                        escortMate.GlobalPosition,
                        validatedFallbackRoute.Waypoints)
                    && IsDemolitionEscortRoutePhysicallyTraversable(
                        escortMate,
                        escortMate.GlobalPosition,
                        validatedFallbackRoute.Waypoints);
                var unsupportedVoidRoute = new[]
                {
                    escortMate.GlobalPosition + Vector3.Right * 1.5f,
                    escortMate.GlobalPosition + Vector3.Right * 3.0f + Vector3.Up * 5.0f
                };
                escortRouteRejectsVoid = !IsDemolitionEscortRoutePhysicallyTraversable(
                    escortMate,
                    escortMate.GlobalPosition,
                    unsupportedVoidRoute);

                ClearSquadNavigation(escortMate);
                var carrierForward = layout.SitePositions[plannedSite] - aiCarrier.GlobalPosition;
                var physicalPreferred = ResolveDemolitionEscortPreferredDestination(
                    escortMate.SquadSlot,
                    aiCarrier.GlobalPosition,
                    carrierForward);
                var plansBeforePhysicalProjection = DemolitionEscortProjectionRoutePlansForDiagnostics;
                var physicalEscortResolved = TryResolveDemolitionEscortDestination(
                    escortMate,
                    aiCarrier,
                    physicalPreferred,
                    out var physicalResolved);
                var physicalRoute = routePlanner.Plan(
                    escortMate.GlobalPosition,
                    physicalResolved,
                    DemolitionTeam.Attackers);
                physicalEscortLanding = physicalEscortResolved
                    && TryGroundDemolitionEscortPoint(
                        escortMate,
                        physicalResolved,
                        out var groundedPhysicalResolved)
                    && groundedPhysicalResolved.DistanceTo(physicalResolved) < 0.12f
                    && layout.HasCapsulePointClearance(physicalResolved, out _)
                    && physicalRoute.ReachesDestination
                    && routePlanner.IsRouteClear(escortMate.GlobalPosition, physicalRoute.Waypoints)
                    && IsDemolitionEscortRoutePhysicallyTraversable(
                        escortMate,
                        escortMate.GlobalPosition,
                        physicalRoute.Waypoints);
                runtimeEscortProjectionBudget = DemolitionEscortProjectionRoutePlansForDiagnostics
                        - plansBeforePhysicalProjection
                        <= DemolitionEscortMaximumProjectionRoutePlans
                    && DemolitionEscortProjectionMaximumPlansForDiagnostics
                        <= DemolitionEscortMaximumProjectionRoutePlans;
                runtimeEscortTotalBudget = DemolitionEscortMaximumRefreshPlansForDiagnostics
                        <= DemolitionEscortMaximumTotalRoutePlansPerRefresh
                    && DemolitionEscortTotalRoutePlansForDiagnostics > 0
                    && DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics <= 50000;

                // An airborne escort has no physically continuous route from its current
                // capsule. This is a normal transient miss: it must return false without
                // throwing, hold at the current point, request recovery, then succeed once
                // grounded and the short retry window elapses.
                ClearSquadNavigation(escortMate);
                _demolitionEscortForcedRecoveryRetry.Remove(escortMate);
                escortMate.GlobalPosition = layout.AttackSpawn + Vector3.Up * 8.0f;
                aiCarrier.GlobalPosition = layout.Midpoint;
                var airbornePreferred = ResolveDemolitionEscortPreferredDestination(
                    escortMate.SquadSlot,
                    aiCarrier.GlobalPosition,
                    layout.SitePositions[plannedSite] - aiCarrier.GlobalPosition);
                var recoveryRequestsBefore = DemolitionEscortForcedRecoveryRequestsForDiagnostics;
                var airborneResolved = TryResolveDemolitionEscortDestination(
                    escortMate,
                    aiCarrier,
                    airbornePreferred,
                    out var airborneDestination);
                if (!airborneResolved)
                {
                    RequestDemolitionEscortNavigationRecovery(escortMate);
                }
                ulong forcedRetryAt = 0;
                noSafeEscortHolds = !airborneResolved
                    && airborneDestination.DistanceSquaredTo(escortMate.GlobalPosition) < 0.01f
                    && DemolitionEscortForcedRecoveryRequestsForDiagnostics
                        == recoveryRequestsBefore + 1
                    && _demolitionEscortForcedRecoveryRetry.TryGetValue(
                        escortMate,
                        out forcedRetryAt)
                    && forcedRetryAt > Time.GetTicksMsec();
                RequestDemolitionEscortNavigationRecovery(escortMate);
                noSafeEscortHolds = noSafeEscortHolds
                    && DemolitionEscortForcedRecoveryRequestsForDiagnostics
                        == recoveryRequestsBefore + 1
                    && _demolitionEscortForcedRecoveryRetry.TryGetValue(
                        escortMate,
                        out var unchangedRetryAt)
                    && unchangedRetryAt == forcedRetryAt;

                _demolitionEscortForcedRecoveryRetry.Remove(escortMate);
                escortMate.GlobalPosition = layout.AttackSpawn;
                ClearSquadNavigation(escortMate);
                var groundedPreferred = ResolveDemolitionEscortPreferredDestination(
                    escortMate.SquadSlot,
                    aiCarrier.GlobalPosition,
                    layout.SitePositions[plannedSite] - aiCarrier.GlobalPosition);
                var groundedRetryResolved = TryResolveDemolitionEscortDestination(
                    escortMate,
                    aiCarrier,
                    groundedPreferred,
                    out var groundedRetryDestination);
                var groundedRetryDirective = groundedRetryResolved
                    ? ResolveSquadNavigationDestination(
                        escortMate,
                        groundedRetryDestination,
                        emergency: false)
                    : SquadNavigationDirective.Walk(escortMate.GlobalPosition);
                noSafeEscortRetries = groundedRetryResolved
                    && groundedRetryDestination.DistanceSquaredTo(escortMate.GlobalPosition)
                        >= DemolitionEscortMinimumMoveDistance * DemolitionEscortMinimumMoveDistance
                    && groundedRetryDirective.Target.DistanceSquaredTo(escortMate.GlobalPosition)
                        >= 0.25f
                    && !_demolitionEscortForcedRecoveryRetry.ContainsKey(escortMate);

                // Cached combat memory behind solid geometry is not an actionable
                // contact.  It must not detach an escort or freeze a post forever.
                escortMate.GlobalPosition = layout.Origin + new Vector3(-6.0f, 0.2f, -13.5f);
                probe.GlobalPosition = layout.Origin + new Vector3(6.0f, 0.2f, -13.5f);
                var coreBlocksSight = !escortMate.HasCombatLineOfSightForDiagnostics(probe);
                escortMate.SetDemolitionThreatForDiagnostics(
                    probe,
                    hasSight: false,
                    threatAge: 2.0f);
                _demolitionSquadCombatBreakoffs.Remove(escortMate);
                hiddenThreatIgnored = coreBlocksSight
                    && TryGetDemolitionEscortTarget(
                        escortMate,
                        out var hiddenThreatLeader,
                        out var hiddenThreatPriority)
                    && hiddenThreatPriority
                    && ReferenceEquals(hiddenThreatLeader, aiCarrier);
                escortMate.SetDemolitionThreatForDiagnostics(
                    probe,
                    hasSight: true,
                    threatAge: 2.0f);
                _demolitionSquadCombatBreakoffs.Remove(escortMate);
                visibleThreatYields = TryGetDemolitionEscortTarget(
                        escortMate,
                        out var visibleThreatLeader,
                        out var visibleThreatPriority)
                    && !visibleThreatPriority
                    && ReferenceEquals(visibleThreatLeader, aiCarrier);
                escortMate.SetDemolitionThreatForDiagnostics(
                    null,
                    hasSight: false,
                    threatAge: 100.0f);
                _demolitionSquadCombatBreakoffs.Remove(escortMate);

                // A grounded PLAYER runner who is away from the case cannot be driven.
                // Reassign the autonomous mate at the case before choosing an escort.
                aiCarrier.GlobalPosition = _demolitionDeviceGroundPosition - Vector3.Up * 0.16f;
                _player.GlobalPosition = _demolitionDeviceGroundPosition
                    + new Vector3(18.0f, 0.0f, 0.0f);
                ForceDemolitionDevicePickupRunnerForDiagnostics(_player);
                EnsureDemolitionDevicePickupRunner();
                groundedPlayerRunnerReleased = _demolitionDeviceLifecycle.PickupRunnerMemberId
                        == DemolitionMemberId(aiCarrier)
                    && TryGetDemolitionEscortTarget(escortMate, out var groundedLeader)
                    && ReferenceEquals(groundedLeader, aiCarrier);

                ForceDemolitionDeviceCarrierForDiagnostics(_player);
                playerCarrierEscort = TryGetDemolitionEscortTarget(escortMate, out var playerLeader)
                    && ReferenceEquals(playerLeader, _player);

                // A post Hold is intentionally brief.  After its dwell expires the mate
                // receives the next authored cover target instead of remaining idle.
                foreach (var assignment in _demolitionSquadAssignmentTargets.ToArray())
                {
                    var postMate = assignment.Key;
                    if (!IsInstanceValid(postMate)
                        || postMate.IsDowned
                        || postMate.IsBodyBag
                        || postMate == aiCarrier)
                    {
                        continue;
                    }
                    var postTarget = layout.StrategyTarget(assignment.Value);
                    _demolitionDevicePlanted = true;
                    _demolitionSquadActivePostTargets[postMate] = assignment.Value;
                    _demolitionSquadPostHoldTimers[postMate] = 0.0f;
                    _demolitionSquadPostPatrolSteps[postMate] = 0;
                    postMate.GlobalPosition = postTarget;
                    postMate.Velocity = Vector3.Zero;
                    postMate.SetDemolitionThreatForDiagnostics(
                        null,
                        hasSight: false,
                        threatAge: 100.0f);
                    _demolitionSquadCombatBreakoffs.Remove(postMate);
                    postMate.SetOrder(SquadOrder.Move, postTarget);
                    UpdateDemolitionSquadPosts(0.05f, ignoreEscort: true, ignoreThreat: false);
                    var convertedToHold = postMate.Order == SquadOrder.Hold;
                    UpdateDemolitionSquadPosts(
                        DemolitionPostHoldDuration + 0.1f,
                        ignoreEscort: true,
                        ignoreThreat: false);
                    postPatrolReactivated = convertedToHold
                        && postMate.Order == SquadOrder.Move
                        && postMate.DemolitionOrderPositionForDiagnostics.DistanceTo(postTarget) > 2.5f;

                    var firstPatrolTarget = postMate.DemolitionOrderPositionForDiagnostics;
                    postMate.SetOrder(
                        SquadOrder.Move,
                        firstPatrolTarget + new Vector3(11.0f, 0.0f, 0.0f));
                    UpdateDemolitionSquadPosts(
                        0.05f,
                        ignoreEscort: true,
                        ignoreThreat: false);
                    staleMoveRecovered = postMate.DemolitionMoveTargets(firstPatrolTarget);

                    postMate.GlobalPosition = firstPatrolTarget;
                    UpdateDemolitionSquadPosts(
                        0.05f,
                        ignoreEscort: true,
                        ignoreThreat: false);
                    var firstPatrolHeld = postMate.Order == SquadOrder.Hold;
                    UpdateDemolitionSquadPosts(
                        DemolitionPostHoldDuration + 0.1f,
                        ignoreEscort: true,
                        ignoreThreat: false);
                    postPatrolSecondHop = firstPatrolHeld
                        && postMate.Order == SquadOrder.Move
                        && postMate.DemolitionOrderPositionForDiagnostics.DistanceTo(
                            firstPatrolTarget) > 2.5f;

                    var defuseTarget = layout.SitePositions[0];
                    _demolitionSquadObjectiveMate = postMate;
                    _demolitionSquadAssignmentTargets[postMate] = assignment.Value;
                    _demolitionSquadActivePostTargets[postMate] = assignment.Value;
                    postMate.SetOrder(SquadOrder.Move, defuseTarget);
                    UpdateDemolitionSquadPosts(
                        0.05f,
                        ignoreEscort: true,
                        ignoreThreat: false);
                    defuserPostProtected = postMate.DemolitionMoveTargets(defuseTarget)
                        && !_demolitionSquadActivePostTargets.ContainsKey(postMate);
                    _demolitionSquadObjectiveMate = null;
                    break;
                }
            }

            var valid = yieldedToCombat && resumedObjective && channelHoldsUnderFire
                && smokeResumesObjective
                && switchedUnderPressure && detourRoutesAroundWall && unreachableSafe && unreachableRetries
                && teamAwareUnreachableSafe && softPenaltyDirectReachable
                && routeRecovery && runtimeRoute && frontierFallback && frontierFallbackStable
                && reachableRoutePreserved && postsConverted
                && squadRouteNavigation && squadRouteReuse
                && openingPatterns
                && roundSiteVariety
                && routeCostOverridesRoundPreference
                && unreachableRouteOverridesRoundPreference
                && unreachableRouteRemainsExcluded
                && committedSiteStable
                && confirmedThreatRotates
                && duplicateThreatReportsDeduplicated
                && urgentCommitmentPreserved
                && plantChannelLocksSite
                && carrierCommitmentScoped
                && rightLaneOpensRight
                && productionApproachesClear
                && productionEscortPointsValid
                && productionEscortProjectionBudget
                && bazaarEdgeEscortPointsValid
                && bazaarWallEdgeLateralRecovery
                && productionRoutesValid
                && productionRoutesOpenCorrectSide
                && productionRoutesDepthSafe
                && productionRoutesEfficient
                && attackerRoutesAvoidDefenderSpawn
                && defenderProductionRoutesValid
                && defenderProductionRoutesEfficient
                && defenderRoutesAvoidAttackerSpawn
                && tideforgeSelectionStable
                && overshotCarrierRetreats
                && objectiveChannels
                && plannerAvoidsStackedSite && blackboardSeesAlertedOpponents && relayTakesOver
                && carrierTransferResetsChannel
                && strategyRefreshPreservesChannel
                && strategyRefreshReassignsLostChannel
                && strategyRefreshClearsChangedPhase
                && friendlyAiPlantsDevice
                && carrierDestinationFollowsDevice
                && carrierThreatKeepsObjective
                && aiCarrierEscort
                && playerCarrierEscort
                && openingEscortFansOut
                && openingEscortFanOutLatched
                && blockedEscortFallsBack
                && escortFallbackMoves
                && escortFallbackSupported
                && escortRouteRejectsVoid
                && physicalEscortLanding
                && runtimeEscortProjectionBudget
                && runtimeEscortTotalBudget
                && noSafeEscortHolds
                && noSafeEscortRetries
                && groundedPlayerRunnerReleased
                && hiddenThreatIgnored
                && visibleThreatYields
                && postPatrolReactivated
                && postPatrolSecondHop
                && staleMoveRecovered
                && defuserPostProtected
                && postPatrolLayouts;
            GD.Print($"DEMOLITION_TACTICAL_AI_CHECK valid={valid} yield={yieldedToCombat} resume={resumedObjective} smoke_resume={smokeResumesObjective} channel_guard={channelHoldsUnderFire} carrier_transfer={carrierTransferResetsChannel} strategy_channel={strategyRefreshPreservesChannel} strategy_reassign={strategyRefreshReassignsLostChannel} strategy_phase_clear={strategyRefreshClearsChangedPhase} friendly_ai_plant_under_contact={friendlyAiPlantsDevice} carrier_destination={carrierDestinationFollowsDevice} carrier_contact_priority={carrierThreatKeepsObjective} ai_carrier_escort={aiCarrierEscort} player_carrier_escort={playerCarrierEscort} escort_fanout={openingEscortFansOut} escort_fanout_latched={openingEscortFanOutLatched} escort_fallback={blockedEscortFallsBack} escort_moves={escortFallbackMoves} escort_fallback_supported={escortFallbackSupported} escort_void_rejected={escortRouteRejectsVoid} escort_physical={physicalEscortLanding} escort_plan_budget={runtimeEscortProjectionBudget}:{DemolitionEscortProjectionMaximumPlansForDiagnostics} escort_total_budget={runtimeEscortTotalBudget}:{DemolitionEscortMaximumRefreshPlansForDiagnostics}/{DemolitionEscortMaximumTotalRoutePlansPerRefresh}:{DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics}us escort_no_safe_hold={noSafeEscortHolds} escort_no_safe_retry={noSafeEscortRetries} grounded_player_reassigned={groundedPlayerRunnerReleased} hidden_threat_ignored={hiddenThreatIgnored} visible_threat_yields={visibleThreatYields} post_patrol={postPatrolReactivated} post_second_hop={postPatrolSecondHop} stale_move_recovered={staleMoveRecovered} defuser_post_protected={defuserPostProtected} post_layouts={postPatrolLayouts} pure_channels={objectiveChannels} time_pressure={switchedUnderPressure} detour_points={detourResult.Waypoints.Count} route_clear={detourRoutesAroundWall} unreachable_safe={unreachableSafe} team_unreachable={teamAwareUnreachableSafe} soft_direct={softPenaltyDirectReachable} unreachable_retry={unreachableRetries} route_recovery={routeRecovery} runtime_route={runtimeRoute} frontier_fallback={frontierFallback} frontier_stable={frontierFallbackStable} frontier_replans={frontierReplans} frontier_directive_distance={frontierDirectiveDistance:0.00} reachable_route_preserved={reachableRoutePreserved} opening_patterns={openingPatterns} round_sites={roundSiteVariety}:{roundPreferredSites[0]}/{roundPreferredSites[1]} route_cost_override={routeCostOverridesRoundPreference} unreachable_override={unreachableRouteOverridesRoundPreference} unreachable_excluded={unreachableRouteRemainsExcluded} committed_site_stable={committedSiteStable} confirmed_threat_rotates={confirmedThreatRotates} threat_deduplicated={duplicateThreatReportsDeduplicated} urgent_commitment={urgentCommitmentPreserved} plant_channel_lock={plantChannelLocksSite} carrier_commitment_scoped={carrierCommitmentScoped} tideforge_selection={tideforgeSelectionStable} right_lane_opening={rightLaneOpensRight} production_maps={productionMapCoverage}:{demolitionLayouts.Length} approaches_clear={productionApproachesClear} escort_points={productionEscortPointsValid}:count={productionEscortPoints.Length} escort_point_budget={productionEscortProjectionBudget}:max={productionEscortPoints.Max(point => point.ProjectionPlans)}/{productionEscortPoints.Max(point => point.TotalPlans)} bazaar_edge_escort={bazaarEdgeEscortPointsValid} bazaar_wall_lateral={bazaarWallEdgeLateralRecovery}:{bazaarWallEdgeLateralProfile} production_routes={productionRoutesValid}:count={productionRoutes.Length}:profiles={productionRouteProfiles} opening_sides={productionRoutesOpenCorrectSide} route_depth={productionRoutesDepthSafe} route_efficiency={productionRoutesEfficient}:max={productionRoutes.Max(route => route.Stretch):0.000} spawn_avoidance={attackerRoutesAvoidDefenderSpawn} defender_routes={defenderProductionRoutesValid}:count={defenderProductionRoutes.Length} defender_efficiency={defenderProductionRoutesEfficient}:max={defenderProductionRoutes.Max(route => route.Stretch):0.000} defender_spawn_avoidance={defenderRoutesAvoidAttackerSpawn} overshot_retreat={overshotCarrierRetreats} posts={postsConverted} squad_route={squadRouteNavigation} squad_route_reuse={squadRouteReuse} intel_avoids_stack={plannerAvoidsStackedSite} blackboard={blackboardSeesAlertedOpponents} relay={relayTakesOver}");
            return valid;
        }
        finally
        {
            _hud.EndRadioMessageSuppressionForDiagnostics();
            if (diagnosticSmoke is not null && IsInstanceValid(diagnosticSmoke))
            {
                diagnosticSmoke.RemoveFromGroup(SmokeGrenade.ActiveGroupName);
                diagnosticSmoke.QueueFree();
            }
            _player.GlobalTransform = savedPlayerTransform;
            _player.Velocity = savedPlayerVelocity;
            _demolitionRemaining = savedRemaining;
            _demolitionEnemyTargetSite = savedTargetSite;
            _demolitionCarrier = savedCarrier;
            _demolitionEnemyPlantProgress = savedPlantProgress;
            _demolitionPlantProgress = savedPlayerPlantProgress;
            _demolitionPlayerDefuseProgress = savedPlayerDefuseProgress;
            _demolitionDevicePlanted = savedDevicePlanted;
            RestoreDemolitionDeviceRuntimeForDiagnostics(savedDeviceRuntime);
            _demolitionActiveSite = savedActiveSite;
            _demolitionDefuser = savedDefuser;
            _demolitionDefuseProgress = savedDefuseProgress;
            _demolitionStrategyRemaining = savedStrategyRemaining;
            _demolitionAttackerPlan = savedAttackerPlan;
            _demolitionDefenderPlan = savedDefenderPlan;
            _demolitionAttackerPlanObjectiveMemberId = savedAttackerPlanObjectiveMemberId;
            _demolitionSquadObjectiveMate = savedRelayMate;
            _demolitionSquadObjectiveSite = savedRelaySite;
            _demolitionSquadPlantProgress = savedSquadPlantProgress;
            _demolitionSquadDefuseProgress = savedSquadDefuseProgress;
            _localPlayerDowned = savedPlayerDowned;

            _demolitionOpponentAssignments.Clear();
            foreach (var (opponent, assignment) in savedOpponentAssignments)
            {
                _demolitionOpponentAssignments[opponent] = assignment;
            }
            _demolitionOpponentRoutes.Clear();
            foreach (var (opponent, route) in savedOpponentRoutes)
            {
                _demolitionOpponentRoutes[opponent] = route;
            }
            _demolitionCombatBreakoffs.Clear();
            foreach (var opponent in savedCombatBreakoffs)
            {
                _demolitionCombatBreakoffs.Add(opponent);
            }
            _demolitionSquadCombatBreakoffs.Clear();
            foreach (var mate in savedSquadCombatBreakoffs)
            {
                _demolitionSquadCombatBreakoffs.Add(mate);
            }
            foreach (var (opponent, sentryMode) in savedSentryModes)
            {
                if (IsInstanceValid(opponent))
                {
                    opponent.SentryMode = sentryMode;
                }
            }
            foreach (var (opponent, state) in savedOpponentNavigation)
            {
                if (!IsInstanceValid(opponent))
                {
                    continue;
                }
                opponent.GlobalTransform = state.Transform;
                opponent.Velocity = state.Velocity;
                opponent.RestoreScriptedObjectiveNavigationForDiagnostics(state.Navigation);
                opponent.RestoreDemolitionPlantRuntimeForDiagnostics(state.PlantRuntime);
            }
            foreach (var (mate, state) in savedMateStates)
            {
                if (!IsInstanceValid(mate))
                {
                    continue;
                }
                mate.GlobalTransform = state.Transform;
                mate.Velocity = state.Velocity;
                mate.RestoreDemolitionOrderForDiagnostics(
                    state.Order,
                    state.DemolitionOrderPositionForDiagnostics);
                mate.RestoreDemolitionTacticalStateForDiagnostics(state.TacticalState);
            }
            _squadTrailPaths.Clear();
            foreach (var (mateId, trailState) in savedSquadTrailPaths)
            {
                _squadTrailPaths[mateId] = trailState;
            }
            _demolitionSquadRoutes.Clear();
            foreach (var (mate, route) in savedSquadRoutes)
            {
                _demolitionSquadRoutes[mate] = route;
            }
            _demolitionSquadRouteFallbacks.Clear();
            foreach (var (mate, fallback) in savedSquadRouteFallbacks)
            {
                _demolitionSquadRouteFallbacks[mate] = fallback;
            }
            _demolitionEscortProjections.Clear();
            foreach (var (mate, projection) in savedEscortProjections)
            {
                _demolitionEscortProjections[mate] = projection;
            }
            _demolitionEscortOpeningFanOut.Clear();
            foreach (var (mate, leaderId) in savedEscortFanOut)
            {
                _demolitionEscortOpeningFanOut[mate] = leaderId;
            }
            _demolitionEscortForcedRecoveryRetry.Clear();
            foreach (var (mate, retryAt) in savedEscortForcedRecoveryRetry)
            {
                _demolitionEscortForcedRecoveryRetry[mate] = retryAt;
            }
            DemolitionSquadRoutePlansForDiagnostics = savedSquadRoutePlans;
            DemolitionSquadRouteReusesForDiagnostics = savedSquadRouteReuses;
            DemolitionEscortProjectionRoutePlansForDiagnostics = savedEscortProjectionPlans;
            DemolitionEscortProjectionMaximumPlansForDiagnostics = savedEscortProjectionMaximumPlans;
            DemolitionEscortTotalRoutePlansForDiagnostics = savedEscortTotalPlans;
            DemolitionEscortMaximumRefreshPlansForDiagnostics = savedEscortMaximumRefreshPlans;
            DemolitionEscortMaximumRefreshMicrosecondsForDiagnostics = savedEscortMaximumRefreshMicroseconds;
            DemolitionEscortForcedRecoveryRequestsForDiagnostics = savedEscortForcedRecoveryRequests;
            _demolitionSquadAssignmentTargets.Clear();
            foreach (var (mate, targetKey) in savedSquadTargets)
            {
                _demolitionSquadAssignmentTargets[mate] = targetKey;
            }
            _demolitionSquadActivePostTargets.Clear();
            foreach (var (mate, targetKey) in savedSquadPostTargets)
            {
                _demolitionSquadActivePostTargets[mate] = targetKey;
            }
            _demolitionSquadPostHoldTimers.Clear();
            foreach (var (mate, timer) in savedSquadPostTimers)
            {
                _demolitionSquadPostHoldTimers[mate] = timer;
            }
            _demolitionSquadPostPatrolSteps.Clear();
            foreach (var (mate, step) in savedSquadPostSteps)
            {
                _demolitionSquadPostPatrolSteps[mate] = step;
            }
        }
    }

    private static float MinimumHorizontalRouteClearance(
        Vector3 start,
        IReadOnlyList<Vector3> waypoints,
        Vector3 point)
    {
        var clearance = float.PositiveInfinity;
        var previous = start;
        foreach (var waypoint in waypoints)
        {
            clearance = Mathf.Min(
                clearance,
                HorizontalDistanceToRouteSegment(point, previous, waypoint));
            previous = waypoint;
        }
        return clearance;
    }

    private static float HorizontalDistanceToRouteSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end)
    {
        var segmentX = end.X - start.X;
        var segmentZ = end.Z - start.Z;
        var segmentLengthSquared = segmentX * segmentX + segmentZ * segmentZ;
        if (segmentLengthSquared <= 0.0001f)
        {
            return HorizontalDistance(point, start);
        }
        var amount = Mathf.Clamp(
            ((point.X - start.X) * segmentX + (point.Z - start.Z) * segmentZ)
                / segmentLengthSquared,
            0.0f,
            1.0f);
        var closest = new Vector3(
            start.X + segmentX * amount,
            point.Y,
            start.Z + segmentZ * amount);
        return HorizontalDistance(point, closest);
    }

    private static float MaximumAttackerRouteDepthOvershoot(
        DemolitionArenaLayout layout,
        Vector3 start,
        IReadOnlyList<Vector3> waypoints,
        Vector3 destination)
    {
        var axis = new Vector2(
            layout.DefenderSpawn.X - layout.AttackSpawn.X,
            layout.DefenderSpawn.Z - layout.AttackSpawn.Z).Normalized();
        var origin = new Vector2(layout.AttackSpawn.X, layout.AttackSpawn.Z);
        var destinationDepth = axis.Dot(
            new Vector2(destination.X, destination.Z) - origin);
        var maximumDepth = AttackerRouteDepth(axis, origin, start);
        foreach (var waypoint in waypoints)
        {
            maximumDepth = Mathf.Max(
                maximumDepth,
                AttackerRouteDepth(axis, origin, waypoint));
        }
        return Mathf.Max(0.0f, maximumDepth - destinationDepth);
    }

    private static float MaximumAdditionalAttackerRouteDepth(
        DemolitionArenaLayout layout,
        Vector3 start,
        IReadOnlyList<Vector3> waypoints)
    {
        var axis = new Vector2(
            layout.DefenderSpawn.X - layout.AttackSpawn.X,
            layout.DefenderSpawn.Z - layout.AttackSpawn.Z).Normalized();
        var origin = new Vector2(layout.AttackSpawn.X, layout.AttackSpawn.Z);
        var startDepth = AttackerRouteDepth(axis, origin, start);
        var maximumDepth = startDepth;
        foreach (var waypoint in waypoints)
        {
            maximumDepth = Mathf.Max(
                maximumDepth,
                AttackerRouteDepth(axis, origin, waypoint));
        }
        return Mathf.Max(0.0f, maximumDepth - startDepth);
    }

    private static float AttackerRouteDepth(
        Vector2 axis,
        Vector2 origin,
        Vector3 point)
        => axis.Dot(new Vector2(point.X, point.Z) - origin);

    private static bool ValidateDemolitionObjectiveChannelCoordinator()
    {
        var coordinator = new DemolitionObjectiveChannelCoordinator();
        var attackPlan = new DemolitionStrategyPlan(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.Opening,
            1,
            DemolitionOpeningPattern.FullExecute,
            new[]
            {
                new DemolitionAssignment(
                    "planned_carrier", DemolitionDuty.Entry, 1, "attack_entry_b", "planned carrier"),
                new DemolitionAssignment(
                    "active_carrier", DemolitionDuty.SiteGuard, 1, "postplant_guard_b", "stale duty")
            },
            "DIAGNOSTIC ATTACK");
        var preservedPlant = coordinator.Resolve(
            attackPlan,
            new[] { "planned_carrier", "active_carrier" },
            new DemolitionObjectiveChannelState(
                "active_carrier", null, 0.35f, 0.0f, 0, -1));
        var activeCarrierAssignment = preservedPlant.Assignments.FirstOrDefault(assignment =>
            assignment.MemberId == "active_carrier");
        var plantChannelPreserved = preservedPlant.CarrierMemberId == "active_carrier"
            && preservedPlant.CarrierSiteIndex == 0
            && !preservedPlant.ResetPlantProgress
            && activeCarrierAssignment.MemberId == "active_carrier"
            && DemolitionObjectiveChannelCoordinator.IsCarrierDuty(activeCarrierAssignment.Duty)
            && activeCarrierAssignment.SiteIndex == 0;

        var transferPlan = attackPlan with
        {
            PrimarySiteIndex = 0,
            Assignments = new[]
            {
                new DemolitionAssignment(
                    "lost_carrier", DemolitionDuty.Entry, 0, "attack_entry_a", "lost carrier"),
                new DemolitionAssignment(
                    "flank_carrier", DemolitionDuty.Flank, 0, "attack_entry_a", "live flank")
            }
        };
        var transferredPlant = coordinator.Resolve(
            transferPlan,
            new[] { "flank_carrier" },
            new DemolitionObjectiveChannelState(
                "lost_carrier", null, 0.35f, 0.0f, 1, -1));
        var flankTransferResets = transferredPlant.CarrierMemberId == "flank_carrier"
            && transferredPlant.CarrierSiteIndex == 0
            && transferredPlant.ResetPlantProgress;

        var postPlantAttack = attackPlan with
        {
            Phase = DemolitionStrategyPhase.PostPlant,
            Assignments = new[]
            {
                new DemolitionAssignment(
                    "active_carrier", DemolitionDuty.SiteGuard, 1, "postplant_guard_b", "post-plant")
            }
        };
        var clearedPlant = coordinator.Resolve(
            postPlantAttack,
            new[] { "active_carrier" },
            new DemolitionObjectiveChannelState(
                "active_carrier", null, 0.35f, 0.0f, 1, 1));
        var plantPhaseReset = clearedPlant.CarrierMemberId is null
            && clearedPlant.ResetPlantProgress;

        var defensePlan = new DemolitionStrategyPlan(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.PostPlant,
            0,
            DemolitionOpeningPattern.FullExecute,
            new[]
            {
                new DemolitionAssignment(
                    "planned_defuser", DemolitionDuty.Defuse, 0, "site_a", "planned defuser"),
                new DemolitionAssignment(
                    "active_defuser", DemolitionDuty.Retake, 0, "retake_entry_a", "active retaker")
            },
            "DIAGNOSTIC DEFENSE");
        var preservedDefuse = coordinator.Resolve(
            defensePlan,
            new[] { "planned_defuser", "active_defuser" },
            new DemolitionObjectiveChannelState(
                null, "active_defuser", 0.0f, 0.35f, -1, 0));
        var activeAssignment = preservedDefuse.Assignments.FirstOrDefault(assignment =>
            assignment.MemberId == "active_defuser");
        var replacementAssignment = preservedDefuse.Assignments.FirstOrDefault(assignment =>
            assignment.MemberId == "planned_defuser");
        var defuseChannelPreserved = preservedDefuse.DefuserMemberId == "active_defuser"
            && !preservedDefuse.ResetDefuseProgress
            && activeAssignment.Duty == DemolitionDuty.Defuse
            && replacementAssignment.Duty == DemolitionDuty.CoverDefuser;

        var replacedDefuse = coordinator.Resolve(
            defensePlan,
            new[] { "planned_defuser" },
            new DemolitionObjectiveChannelState(
                null, "lost_defuser", 0.0f, 0.35f, -1, 0));
        var lostDefuserResets = replacedDefuse.DefuserMemberId == "planned_defuser"
            && replacedDefuse.ResetDefuseProgress;

        var openingDefense = defensePlan with
        {
            Phase = DemolitionStrategyPhase.Opening,
            Assignments = new[]
            {
                new DemolitionAssignment(
                    "active_defuser", DemolitionDuty.AnchorA, 0, "defense_anchor_a", "opening")
            }
        };
        var clearedDefuse = coordinator.Resolve(
            openingDefense,
            new[] { "active_defuser" },
            new DemolitionObjectiveChannelState(
                null, "active_defuser", 0.0f, 0.35f, -1, -1));
        var defusePhaseReset = clearedDefuse.DefuserMemberId is null
            && clearedDefuse.ResetDefuseProgress;

        return plantChannelPreserved
            && flankTransferResets
            && plantPhaseReset
            && defuseChannelPreserved
            && lostDefuserResets
            && defusePhaseReset;
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

        var carrier = ResolveDemolitionAttacker(
            _demolitionDeviceLifecycle.CarrierMemberId) as EnemyOperator;
        var deviceAssignedAtRoundStart = _demolitionDeviceLifecycle.IsCarried
            && IsInstanceValid(_demolitionDevice)
            && _demolitionDevice!.Visible
            && IsLivingDemolitionAttacker(carrier);
        if (carrier is null || !IsInstanceValid(carrier) || carrier.IsDead)
        {
            GD.Print($"DEMOLITION_DEFENSE_CHECK valid=False stage=carrier_missing round={DemolitionRoundNumber}");
            return false;
        }
        var plannedCarrier = carrier!;
        var stickyCarrier = _demolitionOpponents.FirstOrDefault(opponent =>
            IsInstanceValid(opponent) && !opponent.IsDead && opponent != plannedCarrier);
        var plantStrategyRefreshPreservesChannel = false;
        if (stickyCarrier is not null)
        {
            var deviceStateBeforeChannelProbe = CaptureDemolitionDeviceRuntimeForDiagnostics();
            var targetSiteBeforeRefresh = _demolitionEnemyTargetSite;
            ForceDemolitionDeviceCarrierForDiagnostics(stickyCarrier);
            _demolitionCarrier = stickyCarrier;
            _demolitionEnemyPlantProgress = 0.35f;
            RefreshDemolitionStrategies(false);
            plantStrategyRefreshPreservesChannel = _demolitionCarrier == stickyCarrier
                && Mathf.IsEqualApprox(_demolitionEnemyPlantProgress, 0.35f)
                && _demolitionEnemyTargetSite == targetSiteBeforeRefresh
                && _demolitionOpponentAssignments.ContainsKey(stickyCarrier);
            RestoreDemolitionDeviceRuntimeForDiagnostics(deviceStateBeforeChannelProbe);
            _demolitionEnemyPlantProgress = 0.0f;
            _demolitionCarrier = plannedCarrier;
            RefreshDemolitionStrategies(false);
            carrier = ResolveDemolitionAttacker(
                _demolitionDeviceLifecycle.CarrierMemberId) as EnemyOperator;
        }
        if (carrier is null || !IsInstanceValid(carrier) || carrier.IsDead)
        {
            GD.Print("DEMOLITION_DEFENSE_CHECK valid=False stage=carrier_restore_failed");
            return false;
        }
        var siteIndex = Mathf.Clamp(_demolitionEnemyTargetSite, 0, layout.SitePositions.Count - 1);
        var site = layout.SitePositions[siteIndex];
        ForceDemolitionDevicePickupRunnerForDiagnostics(carrier);
        var deviceRecoveryGrounded = _demolitionDeviceLifecycle.IsGrounded
            && _demolitionDeviceLifecycle.PickupRunnerMemberId == DemolitionMemberId(carrier)
            && IsInstanceValid(_demolitionDevice)
            && _demolitionDevice!.Visible;
        carrier!.GlobalPosition = _demolitionDeviceGroundPosition + new Vector3(0.0f, -0.06f, 6.0f);
        carrier.Velocity = Vector3.Zero;
        carrier.ProcessMode = ProcessModeEnum.Inherit;

        var pickupFrames = 0;
        var pickupRouteUsed = false;
        while (_demolitionDeviceLifecycle.IsGrounded && pickupFrames < 240)
        {
            _ = TryHandleDemolitionDefenderMovement(carrier, 0.05f, null);
            pickupRouteUsed |= _demolitionOpponentRoutes.TryGetValue(carrier, out var pickupCursor)
                && pickupCursor.RouteKey == "device_pickup"
                && pickupCursor.ReachesDestination;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            pickupFrames++;
        }
        var enemyPickedUp = _demolitionDeviceLifecycle.IsCarried
            && _demolitionDeviceLifecycle.CarrierMemberId == DemolitionMemberId(carrier)
            && pickupFrames > 1;
        if (!enemyPickedUp)
        {
            GD.Print($"DEMOLITION_DEFENSE_CHECK valid=False stage=pickup_failed frames={pickupFrames} route={pickupRouteUsed} carrier={carrier.GlobalPosition} device={_demolitionDeviceGroundPosition}");
            return false;
        }

        carrier.GlobalPosition = site + new Vector3(0.0f, 0.2f, 6.0f);
        carrier.Velocity = Vector3.Zero;
        ResetDemolitionOpponentRoute(carrier);
        var plantFrames = 0;
        var carrierRouteUsed = false;
        while (!_demolitionDevicePlanted && plantFrames < 600)
        {
            _ = TryHandleDemolitionDefenderMovement(carrier, 0.05f, null);
            carrierRouteUsed |= _demolitionOpponentRoutes.TryGetValue(carrier, out var activeCarrierCursor)
                && activeCarrierCursor.RouteKey.StartsWith("carrier_site_", System.StringComparison.Ordinal)
                && activeCarrierCursor.ReachesDestination;
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

        var valid = spawnsAtDefenderBarrier
            && enemiesAttacking
            && deviceAssignedAtRoundStart
            && deviceRecoveryGrounded
            && plantStrategyRefreshPreservesChannel
            && pickupRouteUsed
            && enemyPickedUp
            && carrierRouteUsed
            && enemyPlanted
            && defusedAndWon;
        GD.Print($"DEMOLITION_DEFENSE_CHECK valid={valid} side_swapped={playerSide == DemolitionTeam.Defenders} spawns_defend={spawnsAtDefenderBarrier} enemies_attacking={enemiesAttacking} device_assigned={deviceAssignedAtRoundStart} recovery_grounded={deviceRecoveryGrounded} plant_strategy_channel={plantStrategyRefreshPreservesChannel} pickup_route={pickupRouteUsed} picked_up={enemyPickedUp} pickup_frames={pickupFrames} carrier_route={carrierRouteUsed} ai_planted={enemyPlanted} plant_frames={plantFrames} defused={defusedAndWon} defuse_steps={defuseSteps}");
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
        ResetDemolitionOpponentRoute(defuser);
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
        var defuserRouteUsed = _demolitionOpponentRoutes.TryGetValue(defuser, out var defuserCursor)
            && defuserCursor.RouteKey.StartsWith("defuser_site_", System.StringComparison.Ordinal)
            && defuserCursor.ReachesDestination;
        var valid = initial >= 7.5f
            && final <= 2.4f
            && _demolitionDefuseProgress >= 0.08f
            && defuserRouteUsed
            && !_missionEnded;
        GD.Print($"DEMOLITION_DEFUSER_ROUTE_CHECK valid={defuserRouteUsed} route={defuserCursor?.RouteKey ?? "none"} reaches={defuserCursor?.ReachesDestination ?? false}");
        return (valid, initial, final, frames);
    }
}
