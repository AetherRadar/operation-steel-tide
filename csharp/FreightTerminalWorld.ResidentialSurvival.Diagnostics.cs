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
        var valid = mapSelected
            && spawnValid
            && extractionValid
            && extractionHidden
            && weaponsValid
            && suppliesValid
            && zombiesValid
            && localRules
            && waveCapValid;
        GD.Print($"RESIDENTIAL_SURVIVAL_CHECK map={mapSelected} spawn={spawnValid} extraction={extractionValid} extraction_hidden={extractionHidden} weapons={weaponsValid} supplies={suppliesValid} zombies={zombiesValid} local_rules={localRules} backend_id={_missionDirector.BackendMissionId} online={_missionDirector.IsOnline} hud_status={_hud.SurvivalPresentationVisible} wave_cap={waveCapValid} wave={_survivalWave}");
        GD.Print($"RESIDENTIAL_SURVIVAL_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
