using System;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static readonly Vector3 OperationsOfficeOrigin = new(520.0f, 42.0f, 380.0f);
    public static readonly Vector3 OperationsOfficeHelipad = OperationsOfficeOrigin + new Vector3(4.0f, 1.55f, -21.0f);

    private Node3D _operationsOfficeScene = null!;
    private Camera3D _operationsOfficeCamera = null!;
    private OperationsOfficeBackdrop _operationsOfficeBackdrop = null!;
    private bool _operationsOfficeActive;

    public bool IsOperationsOfficeActive => _operationsOfficeActive;
    public bool IsOperationsOfficeCameraCurrent
        => IsInstanceValid(_operationsOfficeCamera)
        && GetViewport().GetCamera3D() == _operationsOfficeCamera;
    public int OperationsOfficeScenePartCount
        => IsInstanceValid(_operationsOfficeBackdrop) ? _operationsOfficeBackdrop.AuthoredMeshCount : 0;
    public int OperationsOfficeGlassPaneCount
        => IsInstanceValid(_operationsOfficeBackdrop) ? _operationsOfficeBackdrop.AuthoredWindowCount : 0;
    public bool OperationsOfficeUsesSingleSurfaceGlass
        => IsInstanceValid(_operationsOfficeBackdrop)
        && _operationsOfficeBackdrop.UsesAuthoredSet
        && _operationsOfficeBackdrop.AuthoredWindowCount >= 4;
    public bool OperationsOfficeInteractiveBackdropReady
        => IsInstanceValid(_operationsOfficeBackdrop)
        && _operationsOfficeBackdrop.IsPresentationReady;

    private void BuildOperationsOffice()
    {
        const string scenePath = "res://scenes/OperationsOfficeBackdrop.tscn";
        var scene = GD.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException($"Unable to load {scenePath}");
        _operationsOfficeBackdrop = scene.Instantiate<OperationsOfficeBackdrop>();
        _operationsOfficeBackdrop.Name = "RemoteOperationsOffice";
        _operationsOfficeBackdrop.Position = OperationsOfficeOrigin;
        AddChild(_operationsOfficeBackdrop);
        _operationsOfficeScene = _operationsOfficeBackdrop;
        _operationsOfficeCamera = _operationsOfficeBackdrop.Camera;
        _hud.OperationsBackdropFocusChanged += _operationsOfficeBackdrop.SetFocusFromUi;
    }

    private static MeshInstance3D OfficeBox(
        Node parent,
        string name,
        Vector3 position,
        Vector3 size,
        Godot.Material material,
        Vector3 rotation = default)
    {
        var mesh = new MeshInstance3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = SharedBoxMesh(size),
            MaterialOverride = material
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private void InitializeOperationsOfficeState(string[] args)
    {
        if (_squadDeployed)
        {
            return;
        }

        EnterOperationsOffice();
        if (Array.Exists(args, value => value == "--capture-squad-lobby"))
        {
            OnOperationsQuickStartRequested();
        }
        else if (Array.Exists(args, value => value == "--capture-demolition-briefing"))
        {
            OnDemolitionModeRequested();
        }
    }

    private void EnterOperationsOffice()
    {
        _squadNetwork?.StopLanRoomBrowsing();
        _operationsOfficeActive = true;
        _player.UiLocked = true;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _missionDirector.ProcessMode = ProcessModeEnum.Disabled;
        _operationsOfficeBackdrop.SetPresentationActive(true);
        _operationsOfficeCamera.MakeCurrent();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _hud.ShowOperationsOffice(GameLocalization.Get(
            "operations_status_ready",
            _languageSetting,
            "FIELD TEAM STANDING BY  //  HELIPAD CLEAR"));
        GetTree().Paused = true;
    }

    private void ActivateBattlefieldFromOperationsOffice()
    {
        _squadNetwork.StopLanRoomBrowsing();
        GetTree().Paused = false;
        _operationsOfficeActive = false;
        _operationsOfficeBackdrop.SetPresentationActive(false);
        _missionDirector.ProcessMode = ProcessModeEnum.Always;
        var playerCamera = _player.GetNodeOrNull<Camera3D>("Head/CombatCamera");
        playerCamera?.MakeCurrent();
        _hud.HideOperationsMenus();
    }

    private void OnOperationsQuickStartRequested()
    {
        _operationsOfficeBackdrop.SetPresentationActive(false);
        if (string.Equals(
                _hud.SelectedDeploymentMapId,
                DeploymentMapCatalog.BlackwaterRefineryId,
                StringComparison.OrdinalIgnoreCase))
        {
            JianghaiMapPreloadCache.Request();
        }
        _squadNetwork.StartLanRoomBrowsing();
        _hud.ShowSquadLobby(GameLocalization.Get(
            "operations_quick_status",
            _languageSetting,
            "LOCAL SQUAD  //  3 OPERATORS  //  YOU PICK  //  AI FILLS THE REST"));
    }

    private void OnDemolitionModeRequested()
    {
        _operationsOfficeBackdrop.SetPresentationActive(false);
        _squadNetwork.StartLanRoomBrowsing();
        _hud.ShowDemolitionBriefing();
    }

    private void OnDemolitionBackRequested()
    {
        if (_demolitionLobbyDeployment is not null || _demolitionJoinPending)
        {
            CancelDemolitionNetworkLobby();
        }
        EnterOperationsOffice();
    }

    private void OnOperationsHomeRequested()
    {
        if (_missionEnded || _squadDeployed)
        {
            RestartMission();
            return;
        }
        _deploymentLoadGeneration++;
        _jianghaiDeploymentLoadPending = false;
        _networkMatchReloadQueued = false;
        JianghaiMapPreloadCache.Release();
        DeploymentMapRuntime.ClearTransientDeployment();
        if (_networkLobbyDeployment is not null || _pendingNetworkExtractionDeployment is not null)
        {
            _networkLobbyDeployment = null;
            _pendingNetworkExtractionDeployment = null;
            _squadNetwork.Close();
            _hud.ClearSquadLobbyWaiting();
        }
        if (_demolitionLobbyDeployment is not null || _demolitionJoinPending)
        {
            CancelDemolitionNetworkLobby();
        }
        EnterOperationsOffice();
    }

    private async void ValidateOperationsOffice()
    {
        await WaitFrames(12);
        var uiReady = _hud.OperationsOfficeUiReady;
        var packedUiReady = _hud.OperationsOfficeUsesPackedScene;
        var authoredReady = _operationsOfficeBackdrop.UsesAuthoredSet
            && _operationsOfficeBackdrop.RequiredAnchorsReady
            && _operationsOfficeBackdrop.DecorativeOperatorCount == 2
            && _operationsOfficeBackdrop.UsesAuthoredAircraft;
        var sceneReady = OperationsOfficeInteractiveBackdropReady
            && OperationsOfficeScenePartCount >= 120
            && OperationsOfficeGlassPaneCount >= 4
            && _operationsOfficeCamera.KeepAspect == Camera3D.KeepAspectEnum.Height
            && OperationsOfficeUsesSingleSurfaceGlass;
        var homeReady = _operationsOfficeActive
            && _hud.IsOperationsOfficeVisible
            && !_hud.IsSquadLobbyVisible
            && IsOperationsOfficeCameraCurrent
            && GetTree().Paused
            && _operationsOfficeBackdrop.PresentationActive
            && _operationsOfficeBackdrop.PresentationResourcesActive
            && _missionDirector.ProcessMode == ProcessModeEnum.Disabled;

        var pausedMotionStart = _operationsOfficeBackdrop.PresentationTimeForDiagnostics;
        await WaitFrames(4);
        var pausedMotionReady = GetTree().Paused
            && _operationsOfficeBackdrop.PresentationTimeForDiagnostics > pausedMotionStart;

        _hud.FocusOperationsModeForDiagnostics(OperationsOfficeFocus.Demolition);
        var demolitionFocusSignalReady = _operationsOfficeBackdrop.Focus == OperationsOfficeFocus.Demolition;
        _hud.FocusOperationsModeForDiagnostics(OperationsOfficeFocus.QuickExtraction);
        var quickFocusSignalReady = _operationsOfficeBackdrop.Focus == OperationsOfficeFocus.QuickExtraction;
        var focusSignalReady = demolitionFocusSignalReady && quickFocusSignalReady;

        _operationsOfficeBackdrop.SetPresentationFrozenForDiagnostics(true);
        var quickPointer = new Vector2(0.82f, 0.54f);
        var quickTransform = ReplayOperationsOfficePresentation(
            OperationsOfficeFocus.QuickExtraction,
            quickPointer);
        var quickReady = _operationsOfficeBackdrop.Focus == OperationsOfficeFocus.QuickExtraction
            && _operationsOfficeBackdrop.PointerForDiagnostics.DistanceTo(quickPointer) < 0.02f
            && _operationsOfficeBackdrop.CameraOffsetForDiagnostics > 0.3f
            && _operationsOfficeBackdrop.QuickLightEnergyForDiagnostics
                > _operationsOfficeBackdrop.DemolitionLightEnergyForDiagnostics;
        var repeatedQuickTransform = ReplayOperationsOfficePresentation(
            OperationsOfficeFocus.QuickExtraction,
            quickPointer);
        var replayReady = OperationsOfficeTransformsMatch(quickTransform, repeatedQuickTransform);

        var demolitionTransform = ReplayOperationsOfficePresentation(
            OperationsOfficeFocus.Demolition,
            new Vector2(-0.74f, -0.38f));
        var demolitionVisualReady = _operationsOfficeBackdrop.Focus == OperationsOfficeFocus.Demolition
            && _operationsOfficeBackdrop.DemolitionLightEnergyForDiagnostics
                > _operationsOfficeBackdrop.QuickLightEnergyForDiagnostics
            && demolitionTransform.Origin.DistanceTo(quickTransform.Origin) > 0.25f;
        _operationsOfficeBackdrop.ClearPointerForDiagnostics();
        _operationsOfficeBackdrop.SetPresentationFrozenForDiagnostics(false);

        _hud.PressDemolitionModeForDiagnostics();
        var demolitionReady = _hud.IsDemolitionBriefingVisible
            && _hud.DemolitionBriefingUiReady
            && !_hud.IsOperationsOfficeVisible
            && !_operationsOfficeBackdrop.PresentationActive
            && _operationsOfficeBackdrop.PresentationResourcesSuspended
            && _squadNetwork.IsLanRoomBrowsingRequested;
        _hud.PressDemolitionRoleForDiagnostics(OperatorRole.Recon);
        var roleReady = _hud.SelectedDemolitionRole == OperatorRole.Recon;
        _hud.PressDemolitionBackForDiagnostics();
        var backReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsDemolitionBriefingVisible
            && IsOperationsOfficeCameraCurrent
            && _operationsOfficeBackdrop.PresentationActive
            && _operationsOfficeBackdrop.PresentationResourcesActive
            && !_squadNetwork.IsLanRoomBrowsingRequested;

        _hud.SetLanguage("zh");
        var chineseReady = _hud.OperationsOfficeLanguageReady;
        _hud.SetLanguage("en");
        var englishReady = _hud.OperationsOfficeLanguageReady;

        _hud.PressOperationsQuickStartForDiagnostics();
        var loadoutReady = _hud.IsSquadLobbyVisible
            && !_hud.IsOperationsOfficeVisible
            && _hud.SquadLobbyHomeUiReady
            && !_operationsOfficeBackdrop.PresentationActive
            && _operationsOfficeBackdrop.PresentationResourcesSuspended
            && _squadNetwork.IsLanRoomBrowsingRequested
            && GetTree().Paused;
        _hud.PressSquadLobbyHomeForDiagnostics();
        var loadoutBackReady = _hud.IsOperationsOfficeVisible
            && !_hud.IsSquadLobbyVisible
            && IsOperationsOfficeCameraCurrent
            && _operationsOfficeBackdrop.PresentationActive
            && _operationsOfficeBackdrop.PresentationResourcesActive
            && !_squadNetwork.IsLanRoomBrowsingRequested
            && GetTree().Paused;
        var languageReady = chineseReady && englishReady;
        var interactionReady = pausedMotionReady
            && focusSignalReady
            && quickReady
            && demolitionVisualReady
            && replayReady;
        var valid = uiReady
            && packedUiReady
            && authoredReady
            && sceneReady
            && homeReady
            && interactionReady
            && demolitionReady
            && roleReady
            && backReady
            && languageReady
            && loadoutReady
            && loadoutBackReady;
        GD.Print($"OPERATIONS_OFFICE_CHECK valid={valid} ui={uiReady} packed_ui={packedUiReady} authored={authoredReady} scene={sceneReady} parts={OperationsOfficeScenePartCount} glass={OperationsOfficeGlassPaneCount} single_surface={OperationsOfficeUsesSingleSurfaceGlass} home={homeReady} interaction={interactionReady} paused_motion={pausedMotionReady} focus_signal={focusSignalReady} quick_visual={quickReady} demolition_visual={demolitionVisualReady} replay={replayReady} demolition={demolitionReady} role={roleReady} back={backReady} language={languageReady} loadout={loadoutReady} loadout_back={loadoutBackReady} paused={GetTree().Paused} camera={IsOperationsOfficeCameraCurrent}");
        GD.Print($"OPERATIONS_OFFICE_PASS valid={valid}");
        _operationsOfficeBackdrop.ClearPointerForDiagnostics();
        _operationsOfficeBackdrop.SetPresentationActive(false);
        GetTree().Paused = false;
        await WaitFrames(180);
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureOperationsOffice()
    {
        var previousScaling = GetViewport().Scaling3DScale;
        var previousSunShadow = _sunLight.ShadowEnabled;
        GetViewport().Scaling3DScale = 1.0f;
        _sunLight.ShadowEnabled = true;
        _operationsOfficeBackdrop.ApplyQuality(2);
        _hud.SetLanguage("zh");
        _operationsOfficeBackdrop.SetPresentationActive(true);
        _operationsOfficeCamera.MakeCurrent();
        _hud.ShowOperationsOffice(GameLocalization.Get(
            "operations_status_ready",
            "zh",
            "FIELD TEAM STANDING BY  //  HELIPAD CLEAR"));
        await WaitFrames(18);
        _operationsOfficeBackdrop.SetPresentationFrozenForDiagnostics(true);

        ReplayOperationsOfficePresentation(OperationsOfficeFocus.Neutral, Vector2.Zero);
        await WaitFrames(3);
        SaveViewportImage("res://operations_office_validation.png");
        GD.Print("OPERATIONS_OFFICE_CAPTURE state=neutral path=operations_office_validation.png");

        _hud.FocusOperationsModeForDiagnostics(OperationsOfficeFocus.QuickExtraction);
        ReplayOperationsOfficePresentation(
            OperationsOfficeFocus.QuickExtraction,
            new Vector2(0.72f, 0.22f));
        await WaitFrames(3);
        SaveViewportImage("res://operations_office_quick_validation.png");
        GD.Print("OPERATIONS_OFFICE_CAPTURE state=quick path=operations_office_quick_validation.png");

        _hud.FocusOperationsModeForDiagnostics(OperationsOfficeFocus.Demolition);
        ReplayOperationsOfficePresentation(
            OperationsOfficeFocus.Demolition,
            new Vector2(-0.46f, 0.18f));
        await WaitFrames(3);
        SaveViewportImage("res://operations_office_demolition_validation.png");
        GD.Print("OPERATIONS_OFFICE_CAPTURE state=demolition path=operations_office_demolition_validation.png");

        _operationsOfficeBackdrop.ClearPointerForDiagnostics();
        _operationsOfficeBackdrop.SetPresentationFrozenForDiagnostics(false);
        _operationsOfficeBackdrop.SetPresentationActive(false);
        _operationsOfficeBackdrop.ApplyQuality(_qualitySetting);
        _sunLight.ShadowEnabled = previousSunShadow;
        GetViewport().Scaling3DScale = previousScaling;
        GetTree().Paused = false;
        GetTree().Quit();
    }

    private Transform3D ReplayOperationsOfficePresentation(
        OperationsOfficeFocus focus,
        Vector2 pointer)
    {
        _operationsOfficeBackdrop.ResetPresentationForDiagnostics();
        _operationsOfficeBackdrop.SetPointerForDiagnostics(pointer);
        _operationsOfficeBackdrop.SetFocus(focus);
        for (var frame = 0; frame < 120; frame++)
        {
            _operationsOfficeBackdrop.AdvancePresentationForDiagnostics(1.0 / 60.0);
        }
        return _operationsOfficeCamera.Transform;
    }

    private static bool OperationsOfficeTransformsMatch(Transform3D first, Transform3D second)
        => first.Origin.DistanceTo(second.Origin) < 0.0001f
        && first.Basis.X.DistanceTo(second.Basis.X) < 0.0001f
        && first.Basis.Y.DistanceTo(second.Basis.Y) < 0.0001f
        && first.Basis.Z.DistanceTo(second.Basis.Z) < 0.0001f;

    private async void CaptureDemolitionBriefing()
    {
        _hud.SetLanRoomBrowseAvailable(true);
        _hud.SetLanRooms(new[]
        {
            new LanRoomInfo(
                "capture-demolition-room",
                "STEEL-TIDE-HOST",
                "192.168.10.42",
                SquadNetwork.DefaultPort,
                LanRoomKind.Demolition,
                DemolitionMapCatalog.TideglassReactorId,
                2,
                SquadNetwork.DemolitionCapacity)
        });
        _hud.SelectDemolitionNetworkForDiagnostics(
            SquadSessionMode.Join,
            DemolitionNetworkTeam.Alpha,
            string.Empty);
        _hud.PressDemolitionMapForDiagnostics(DemolitionMapCatalog.TideglassReactorId);
        await WaitFrames(18);
        SaveViewportImage("res://demolition_briefing_validation.png");
        GD.Print("DEMOLITION_BRIEFING_CAPTURE path=demolition_briefing_validation.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }
}
