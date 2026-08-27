using System;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void CaptureResidentialDiversity()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var mate in _squadMates)
        {
            mate.ProcessMode = ProcessModeEnum.Disabled;
        }
        foreach (var civilian in _civilians)
        {
            civilian.ProcessMode = ProcessModeEnum.Disabled;
        }
        _player.ProcessMode = ProcessModeEnum.Disabled;
        _player.Visible = false;
        _hud.Visible = false;

        var camera = new Camera3D
        {
            Name = "ResidentialDiversityCamera",
            Fov = 68.0f,
            Far = 320.0f
        };
        AddChild(camera);
        camera.MakeCurrent();

        var clinicTower = _residentialTowers[1];
        var clinicSpec = ResidentialTowerSpecs[1];
        camera.GlobalPosition = clinicTower.ToGlobal(new Vector3(3.8f, 1.55f, clinicSpec.Footprint.Y * 0.29f));
        camera.LookAt(clinicTower.ToGlobal(new Vector3(clinicSpec.Footprint.X * 0.31f, 1.0f, clinicSpec.Footprint.Y * 0.27f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_layout_clinic_validation.png");

        var workshopTower = _residentialTowers[4];
        var workshopSpec = ResidentialTowerSpecs[4];
        camera.GlobalPosition = workshopTower.ToGlobal(new Vector3(3.8f, 1.55f, workshopSpec.Footprint.Y * 0.29f));
        camera.LookAt(workshopTower.ToGlobal(new Vector3(workshopSpec.Footprint.X * 0.3f, 1.05f, workshopSpec.Footprint.Y * 0.16f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_layout_workshop_validation.png");

        var kitchenTower = _residentialTowers[9];
        var kitchenSpec = ResidentialTowerSpecs[9];
        camera.GlobalPosition = kitchenTower.ToGlobal(new Vector3(3.8f, 1.55f, kitchenSpec.Footprint.Y * 0.29f));
        camera.LookAt(kitchenTower.ToGlobal(new Vector3(kitchenSpec.Footprint.X * 0.3f, 1.0f, kitchenSpec.Footprint.Y * 0.12f)), Vector3.Up);
        await WaitFrames(20);
        SaveViewportImage("res://residential_layout_kitchen_validation.png");

        GD.Print(
            "RESIDENTIAL_DIVERSITY_CAPTURE paths=residential_layout_clinic_validation.png,"
            + "residential_layout_workshop_validation.png,residential_layout_kitchen_validation.png");
        GetTree().Quit();
    }

    private async void ValidateResidentialDiversity()
    {
        foreach (var enemy in _enemies)
        {
            enemy.ProcessMode = ProcessModeEnum.Disabled;
        }
        await WaitFrames(4);

        var floorNodes = GetTree().GetNodesInGroup("residential_floor_layouts");
        using var floorNodesBacking = floorNodes.AsDisposable();
        var floors = floorNodes.OfType<Node3D>().Where(IsInstanceValid).ToList();
        var layouts = floors
            .Where(node => node.HasMeta("residential_layout"))
            .Select(node => node.GetMeta("residential_layout").AsString())
            .ToHashSet(StringComparer.Ordinal);
        var floorGroups = floors
            .Where(node => node.HasMeta("residential_tower_index"))
            .GroupBy(node => node.GetMeta("residential_tower_index").AsInt32())
            .ToDictionary(group => group.Key, group => group.ToList());
        var expectedFloors = ResidentialTowerSpecs.Sum(spec => spec.Floors);
        var floorCountsReady = Enumerable.Range(0, ResidentialTowerSpecs.Length).All(index =>
            floorGroups.TryGetValue(index, out var group)
            && group.Count == ResidentialTowerSpecs[index].Floors);
        var towerLayoutCoverage = Enumerable.Range(0, ResidentialTowerSpecs.Length).All(index =>
            floorGroups.TryGetValue(index, out var group)
            && group.Select(node => node.GetMeta("residential_layout").AsString()).Distinct().Count() >= 3);

        var profileSignatures = _residentialTowers
            .Where(tower => tower.HasMeta("residential_profile_signature"))
            .Select(tower => tower.GetMeta("residential_profile_signature").AsString())
            .ToHashSet(StringComparer.Ordinal);
        var facadeStyles = _residentialTowers
            .Where(tower => tower.HasMeta("residential_facade_style"))
            .Select(tower => tower.GetMeta("residential_facade_style").AsString())
            .ToHashSet(StringComparer.Ordinal);
        var roofStyles = _residentialTowers
            .Where(tower => tower.HasMeta("residential_roof_style"))
            .Select(tower => tower.GetMeta("residential_roof_style").AsString())
            .ToHashSet(StringComparer.Ordinal);

        var authoredNodes = GetTree().GetNodesInGroup("residential_authored_dressing");
        using var authoredNodesBacking = authoredNodes.AsDisposable();
        var authored = authoredNodes.OfType<StaticBody3D>().Where(IsInstanceValid).ToList();
        var authoredByTower = authored
            .Where(node => node.HasMeta("residential_tower_index"))
            .GroupBy(node => node.GetMeta("residential_tower_index").AsInt32())
            .ToDictionary(group => group.Key, group => group.Count());
        var everyTowerAuthored = Enumerable.Range(0, ResidentialTowerSpecs.Length).All(index =>
            authoredByTower.TryGetValue(index, out var count) && count >= 4);
        var sourcePaths = authored
            .Where(node => node.HasMeta("residential_scene_path"))
            .Select(node => node.GetMeta("residential_scene_path").AsString())
            .ToHashSet(StringComparer.Ordinal);
        var modelScenesReady = authored.All(node => node.GetNodeOrNull<Node3D>("Model") is not null);
        var authoredBuildings = authored
            .Where(node => node.GetMeta("residential_scene_path").AsString()
                .Contains("/building-", StringComparison.Ordinal))
            .ToList();
        var paletteReady = authoredBuildings.Count >= ResidentialTowerSpecs.Length * 2
            && authoredBuildings.All(node =>
                node.GetNodeOrNull<Node3D>("Model") is { } model
                && model.GetMeta("freight_palette", string.Empty).AsString()
                    == FreightIndustrialPalette.PaletteId);
        var collisionsReady = authored.All(node =>
            node.GetNodeOrNull<CollisionShape3D>("Collision")?.Shape is BoxShape3D box
            && box.Size.X > 0.1f
            && box.Size.Y > 0.1f
            && box.Size.Z > 0.1f);
        var artResultsReady = _residentialTowerArtResults.Count == ResidentialTowerSpecs.Length
            && _residentialTowerArtResults.All(result =>
                result.AuthoredModelCount >= 4
                && result.AuthoredModelCount == result.CollisionShapeCount
                && result.PalettedBuildingCount >= 2);

        var valid = ResidentialTowerDiversityPlan.All.Count == ResidentialTowerSpecs.Length
            && profileSignatures.Count == ResidentialTowerSpecs.Length
            && facadeStyles.Count == Enum.GetValues<ResidentialFacadeStyle>().Length
            && roofStyles.Count == Enum.GetValues<ResidentialRoofStyle>().Length
            && floors.Count == expectedFloors
            && floorCountsReady
            && layouts.Count == Enum.GetValues<ResidentialFloorLayout>().Length
            && towerLayoutCoverage
            && authored.Count >= ResidentialTowerSpecs.Length * 4
            && everyTowerAuthored
            && sourcePaths.Count >= ResidentialTowerArtBuilder.ExpectedSourceSceneCount
            && modelScenesReady
            && paletteReady
            && collisionsReady
            && artResultsReady;
        GD.Print(
            $"RESIDENTIAL_DIVERSITY_CHECK valid={valid} profiles={profileSignatures.Count}/{ResidentialTowerSpecs.Length} "
            + $"facades={facadeStyles.Count}/{Enum.GetValues<ResidentialFacadeStyle>().Length} "
            + $"roofs={roofStyles.Count}/{Enum.GetValues<ResidentialRoofStyle>().Length} "
            + $"layouts={layouts.Count}/{Enum.GetValues<ResidentialFloorLayout>().Length} "
            + $"floors={floors.Count}/{expectedFloors} floor_counts={floorCountsReady} tower_layouts={towerLayoutCoverage} "
            + $"authored={authored.Count}/{ResidentialTowerSpecs.Length * 4} every_tower={everyTowerAuthored} "
            + $"sources={sourcePaths.Count}/{ResidentialTowerArtBuilder.ExpectedSourceSceneCount} "
            + $"models={modelScenesReady} palette={paletteReady} "
            + $"paletted={authoredBuildings.Count} collision={collisionsReady} results={artResultsReady}");
        GD.Print($"RESIDENTIAL_DIVERSITY_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
