using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureResidentialSurvival()
    {
        foreach (var zombie in _survivalZombies)
        {
            if (IsInstanceValid(zombie))
            {
                zombie.ProcessMode = ProcessModeEnum.Disabled;
            }
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _hud.Visible = false;
        var camera = new Camera3D
        {
            Name = "ResidentialSurvivalReviewCamera",
            Fov = 58.0f,
            Far = 260.0f
        };
        AddChild(camera);
        camera.GlobalPosition = new Vector3(-75.0f, 14.0f, 52.0f);
        camera.LookAt(new Vector3(-52.0f, 3.2f, 43.0f), Vector3.Up);
        camera.MakeCurrent();
        await WaitFrames(30);
        SaveViewportImage("res://residential_survival_exterior_validation.png");
        camera.GlobalPosition = new Vector3(-52.0f, 6.0f, 55.0f);
        camera.LookAt(new Vector3(-52.0f, 2.0f, 43.0f), Vector3.Up);
        camera.Fov = 62.0f;
        await WaitFrames(24);
        SaveViewportImage("res://residential_survival_plaza_validation.png");
        GD.Print("RESIDENTIAL_SURVIVAL_CAPTURE paths=residential_survival_exterior_validation.png,residential_survival_plaza_validation.png");
        GetTree().Quit();
    }

    private void ValidateResidentialSurvival()
    {
        var mapSelected = IsSurvivalMode;
        var horizontalSpawnDistance = new Vector2(DeploymentPoint.X, DeploymentPoint.Z)
            .DistanceTo(new Vector2(ResidentialSurvivalSpawnPads[1].X, ResidentialSurvivalSpawnPads[1].Z));
        var spawnValid = mapSelected
            && horizontalSpawnDistance < 0.1f
            && DeploymentPoint.X > -90.0f && DeploymentPoint.X < -20.0f
            && DeploymentPoint.Z > 20.0f && DeploymentPoint.Z < 70.0f;
        var extractionValid = ExtractionPoint.DistanceTo(ResidentialSurvivalExtractionPoint) < 0.1f;
        var extractionHidden = !IsInstanceValid(_extractionMarker) || !_extractionMarker.Visible;
        var weaponsValid = _lootSources.Count(source => source is WeaponCase) >= 3;
        var suppliesValid = _buildingLootPickupCount >= ResidentialSurvivalSupplySpots.Length;
        var zombiesValid = _survivalZombies.Count >= 6;
        var localRules = _missionDirector.BackendMissionId == "residential-survival"
            && !_missionDirector.IsOnline
            && IsInstanceValid(_hud)
            && _hud.SurvivalPresentationConfigured;
        var waveCapValid = ResidentialSurvivalWaveLimit >= 6;
        // The mode takeover contract is asserted last, because committing demolition
        // presentation is a real hand-off: the wave module stops owning the scene and
        // the survival readout leaves the HUD for good.
        var takeoverReleaseValid = ValidateResidentialSurvivalTakeoverRelease();
        var valid = mapSelected
            && spawnValid
            && extractionValid
            && extractionHidden
            && weaponsValid
            && suppliesValid
            && zombiesValid
            && localRules
            && waveCapValid
            && takeoverReleaseValid;
        GD.Print($"RESIDENTIAL_SURVIVAL_CHECK map={mapSelected} spawn={spawnValid} extraction={extractionValid} extraction_hidden={extractionHidden} weapons={weaponsValid} supplies={suppliesValid} zombies={zombiesValid} local_rules={localRules} backend_id={_missionDirector.BackendMissionId} online={_missionDirector.IsOnline} hud_status={_hud.SurvivalPresentationVisible} wave_cap={waveCapValid} wave={_survivalWave} takeover_release={takeoverReleaseValid}");
        GD.Print($"RESIDENTIAL_SURVIVAL_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    /// <summary>
    /// Reproduces the reported leak where a demolition session started from the
    /// operations office kept the standalone wave arena alive: selecting another mode
    /// must release the world slot, stop the wave module, clear the infected, and pull
    /// the survival readout out of the shared top-left HUD corner.
    /// </summary>
    private bool ValidateResidentialSurvivalTakeoverRelease()
    {
        _hud.SetSurvivalStatus(_survivalWave, _survivalWaveTimer, _survivalZombies.Count, _survivalEliminations);
        var readoutShown = _hud.SurvivalPresentationVisible;
        var infectedBefore = _survivalZombies.Count;
        OnDemolitionModeRequested();
        var briefingVisible = _hud.IsDemolitionBriefingVisible;
        var slotReleased = string.Equals(
            DeploymentMapRuntime.SelectedMapIdForDiagnostics,
            DeploymentMapCatalog.FreightTerminalId,
            StringComparison.OrdinalIgnoreCase);
        PrepareDemolitionBattlefield();
        _hud.SetDemolitionGameplayPresentation(true);
        var moduleStopped = IsSurvivalMode && !IsSurvivalArenaRunning;
        var readoutHidden = !_hud.SurvivalPresentationVisible;
        var infectedCleared = infectedBefore > 0 && _survivalZombies.Count == 0;
        var valid = readoutShown
            && briefingVisible
            && slotReleased
            && moduleStopped
            && readoutHidden
            && infectedCleared;
        GD.Print($"RESIDENTIAL_SURVIVAL_TAKEOVER_CHECK readout_shown={readoutShown} briefing={briefingVisible} slot_released={slotReleased} slot={DeploymentMapRuntime.SelectedMapIdForDiagnostics} module_stopped={moduleStopped} readout_hidden={readoutHidden} infected_cleared={infectedCleared} infected_before={infectedBefore} survival_world={IsSurvivalMode} valid={valid}");
        return valid;
    }
}
