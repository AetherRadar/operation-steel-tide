using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
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
        var weaponsValid = _lootSources.Count(source => source is WeaponCase) >= 3;
        var suppliesValid = _buildingLootPickupCount >= ResidentialSurvivalSupplySpots.Length;
        var zombiesValid = _survivalZombies.Count >= 6;
        var valid = mapSelected && spawnValid && extractionValid && weaponsValid && suppliesValid && zombiesValid;
        GD.Print($"RESIDENTIAL_SURVIVAL_CHECK map={mapSelected} spawn={spawnValid} extraction={extractionValid} weapons={weaponsValid} supplies={suppliesValid} zombies={zombiesValid} wave={_survivalWave}");
        GD.Print($"RESIDENTIAL_SURVIVAL_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
