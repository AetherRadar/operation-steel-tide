using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionTeamStatus()
    {
        await WaitFrames(3);
        var originalLanguage = _hud.CurrentLanguage;
        var snapshot = DemolitionTeamStatusDiagnosticSnapshot();
        var packedScene = GD.Load<PackedScene>(DemolitionTeamStatusView.ScenePath);
        var probe = packedScene?.Instantiate<DemolitionTeamStatusView>();
        var sceneReady = false;
        var englishReady = false;
        if (probe is not null)
        {
            _hud.AddChild(probe);
            probe.Visible = true;
            probe.SetLanguage("zh");
            probe.SetSnapshot(snapshot);
            sceneReady = probe.SceneFilePath == DemolitionTeamStatusView.ScenePath
                && probe.UiReady
                && probe.FriendlyCount == DemolitionSquadSize
                && probe.EnemyCount == DemolitionSquadSize
                && probe.LocalPlayerMarkerCount == 1
                && probe.DeviceMarkerCount == 1
                && probe.OutCount == 2
                && probe.ScoreText == "6  :  5"
                && probe.TimerText == "01:36"
                && probe.LanguageMatches("zh")
                && probe.PhaseText == GameLocalization.Get(
                    "demolition_buy_attack",
                    "zh",
                    "ATTACK");

            probe.SetLanguage("en");
            englishReady = probe.LanguageMatches("en")
                && probe.PhaseText == "ATTACK";
        }
        probe?.QueueFree();

        _hud.SetLanguage("zh");
        _hud.SetDemolitionGameplayPresentation(true);
        _hud.SetDemolitionTeamStatus(snapshot);
        var hudReady = _hud.IsDemolitionTeamStatusVisible
            && _hud.DemolitionTeamStatusUiReady
            && _hud.DemolitionTeamStatusUsesPackedScene
            && _hud.DemolitionTeamStatusLanguageReady
            && _hud.AreDemolitionLegacyTopLabelsHidden
            && _hud.DemolitionFriendlyStatusCount == DemolitionSquadSize
            && _hud.DemolitionEnemyStatusCount == DemolitionSquadSize
            && _hud.DemolitionLocalPlayerMarkerCount == 1
            && _hud.DemolitionDeviceMarkerCount == 1
            && _hud.DemolitionOutStatusCount == 2
            && _hud.DemolitionTeamStatusScoreText == "6  :  5"
            && _hud.DemolitionTeamStatusTimerText == "01:36";

        var model = CombatModelLibrary.InspectDemolitionDevice();
        var modelReady = model.Loaded
            && model.ContractValid
            && model.MeshCount >= 36
            && model.MaterialCount >= 8
            && model.BoundsSize.X is >= 0.30f and <= 0.38f
            && model.BoundsSize.Y is >= 0.12f and <= 0.22f
            && model.BoundsSize.Z is >= 0.16f and <= 0.24f
            && model.HasEmission;

        _hud.SetDemolitionGameplayPresentation(false);
        var hidden = !_hud.IsDemolitionTeamStatusVisible;
        _hud.SetLanguage(originalLanguage);
        _hud.ShowOperationsOffice();
        await WaitFrames(3);

        var valid = sceneReady && englishReady && hudReady && modelReady && hidden;
        GD.Print(
            $"DEMOLITION_TEAM_STATUS_CHECK valid={valid} scene={sceneReady} "
            + $"signals=read_only english={englishReady} hud={hudReady} hidden={hidden} "
            + $"friendly={_hud.DemolitionFriendlyStatusCount} enemy={_hud.DemolitionEnemyStatusCount} "
            + $"self_marker=1 device_marker=1 out=2 model={modelReady} meshes={model.MeshCount} "
            + $"materials={model.MaterialCount} bounds={model.BoundsSize} emission={model.HasEmission}");
        GD.Print($"DEMOLITION_TEAM_STATUS_PASS valid={valid}");
        GetTree().Paused = false;
        GetTree().Quit(valid ? 0 : 2);
    }

    private static DemolitionTeamStatusSnapshot DemolitionTeamStatusDiagnosticSnapshot()
    {
        DemolitionTeamStatusMember[] friendly =
        {
            new("PLAYER", "GARRISON", OperatorRole.Assault, true, true, false),
            new("MATE:1", "HERON", OperatorRole.Medic, true, false, true),
            new("MATE:2", "LYNX", OperatorRole.Recon, true, false, false),
            new("MATE:3", "MAGPIE", OperatorRole.Scavenger, false, false, false),
            new("MATE:4", "JACKAL", OperatorRole.Locksmith, true, false, false)
        };
        DemolitionTeamStatusMember[] enemy =
        {
            new("ENEMY:0", "ENEMY 1", OperatorRole.Assault, true, false, false),
            new("ENEMY:1", "ENEMY 2", OperatorRole.Medic, true, false, false),
            new("ENEMY:2", "ENEMY 3", OperatorRole.Recon, false, false, false),
            new("ENEMY:3", "ENEMY 4", OperatorRole.Assault, true, false, false),
            new("ENEMY:4", "ENEMY 5", OperatorRole.Recon, true, false, false)
        };
        return new DemolitionTeamStatusSnapshot(
            friendly,
            enemy,
            DemolitionTeam.Attackers,
            DemolitionTeamStatusPhase.Live,
            6,
            5,
            12,
            95.2f,
            false);
    }

    private async void CaptureDemolitionDeviceStatus()
    {
        await WaitFrames(5);
        _hud.PressDemolitionModeForDiagnostics();
        _hud.PressDemolitionRoleForDiagnostics(OperatorRole.Assault);
        _hud.PressDemolitionMapForDiagnostics(DemolitionMapCatalog.TideforgeId);
        _hud.PressDemolitionDeployForDiagnostics();
        await WaitFrames(5);
        if (_demolitionBuyPhaseActive)
        {
            _hud.SelectDemolitionBuySidearmForDiagnostics(DemolitionBuyCatalog.P226Id);
            _hud.PressDemolitionBuyConfirmForDiagnostics();
        }
        await WaitFrames(5);

        var carrier = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && !mate.IsDowned
            && !mate.IsBodyBag);
        if (carrier is null)
        {
            GD.Print("DEMOLITION_DEVICE_STATUS_CAPTURE valid=False reason=no_carrier");
            GetTree().Quit(2);
            return;
        }

        var layout = DemolitionLayout();
        carrier.GlobalPosition = layout.AttackSpawn + Vector3.Up * 0.2f;
        carrier.LookAt(layout.Midpoint, Vector3.Up);
        carrier.Velocity = Vector3.Zero;
        foreach (var mate in _squadMates.Where(IsInstanceValid))
        {
            if (mate != carrier)
            {
                mate.GlobalPosition = carrier.GlobalPosition
                    - carrier.GlobalBasis.Z * (8.0f + mate.SquadSlot);
            }
        }
        ForceDemolitionDeviceCarrierForDiagnostics(carrier);
        SetDemolitionActorsFrozen(true);
        SyncDemolitionDeviceVisual();
        _hud.SetLanguage("zh");
        _hud.SetDemolitionGameplayPresentation(true);
        UpdateDemolitionTeamStatusHud();

        var camera = new Camera3D
        {
            Name = "DemolitionDeviceStatusCaptureCamera",
            Fov = 46.0f
        };
        AddChild(camera);
        camera.GlobalPosition = carrier.GlobalPosition
            + carrier.GlobalBasis.X * 1.35f
            - carrier.GlobalBasis.Z * 2.1f
            + Vector3.Up * 1.15f;
        camera.LookAt(carrier.GlobalPosition + Vector3.Up * 0.82f, Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(18);
        SaveViewportImage("res://demolition_device_status_validation.png");
        GD.Print(
            "DEMOLITION_DEVICE_STATUS_CAPTURE valid=True "
            + "path=demolition_device_status_validation.png carrier="
            + carrier.Callsign);
        GetTree().Quit();
    }
}
