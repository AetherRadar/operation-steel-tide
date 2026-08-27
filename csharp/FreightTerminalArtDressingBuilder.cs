using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record FreightTerminalArtDressingResult(
    int AuthoredModelCount,
    int MissingModelCount,
    int PalettedBuildingCount,
    IReadOnlyCollection<string> ScenePaths);

/// <summary>Adds licensed industrial silhouettes to existing freight-terminal collision shells.</summary>
internal sealed class FreightTerminalArtDressingBuilder
{
    private const string IndustrialRoot = "res://assets/models/kenney_city_kit_industrial";
    private readonly Dictionary<string, PackedScene> _scenes = new();
    private readonly FreightIndustrialPalette _palette;

    public FreightTerminalArtDressingBuilder(FreightIndustrialPalette palette)
    {
        _palette = palette;
    }

    public FreightTerminalArtDressingResult Build(Node3D parent)
    {
        var root = new Node3D { Name = "FreightTerminalAuthoredDressing" };
        root.AddToGroup("freight_authored_dressing");
        parent.AddChild(root);

        var scenePaths = new HashSet<string>();
        var authoredModelCount = 0;
        var missingModelCount = 0;
        var palettedBuildingCount = 0;
        foreach (var placement in Placements)
        {
            if (TryAddModel(root, placement, scenePaths, out var paletteApplied))
            {
                authoredModelCount++;
                if (paletteApplied)
                {
                    palettedBuildingCount++;
                }
            }
            else
            {
                missingModelCount++;
            }
        }

        root.SetMeta("authored_model_count", authoredModelCount);
        root.SetMeta("missing_model_count", missingModelCount);
        root.SetMeta("unique_scene_count", scenePaths.Count);
        root.SetMeta("paletted_building_count", palettedBuildingCount);
        return new FreightTerminalArtDressingResult(
            authoredModelCount,
            missingModelCount,
            palettedBuildingCount,
            scenePaths);
    }

    private bool TryAddModel(
        Node3D root,
        ModelPlacement placement,
        HashSet<string> scenePaths,
        out bool paletteApplied)
    {
        paletteApplied = false;
        var path = $"{IndustrialRoot}/{placement.File}";
        if (!_scenes.TryGetValue(path, out var scene))
        {
            scene = GD.Load<PackedScene>(path);
            if (scene is null)
            {
                GD.PushError($"Freight terminal authored dressing is missing: {path}");
                return false;
            }
            _scenes[path] = scene;
        }
        if (scene.Instantiate() is not Node3D model)
        {
            GD.PushError($"Freight terminal authored dressing could not instantiate: {path}");
            return false;
        }

        model.Name = placement.Name;
        model.Position = placement.Position;
        model.Rotation = new Vector3(0, placement.Yaw, 0);
        model.Scale = Vector3.One * placement.Scale;
        model.AddToGroup("freight_authored_model");
        model.SetMeta("freight_scene_path", path);
        ConfigureVisuals(model);
        if (placement.File.StartsWith("building-", StringComparison.Ordinal))
        {
            paletteApplied = _palette.Apply(model) > 0;
        }
        root.AddChild(model);
        scenePaths.Add(path);
        return true;
    }

    private static void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = 330.0f;
            visual.VisibilityRangeEndMargin = 22.0f;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureVisuals(childNode);
            }
        }
    }

    private static readonly ModelPlacement[] Placements =
    {
        new("RailDispatchPlant", "building-j.glb", new Vector3(-105.0f, 0.02f, -72.0f), 0.0f, 6.2f),
        new("RailLoadingPlant", "building-c.glb", new Vector3(-43.0f, 0.02f, -91.0f), Mathf.Pi, 6.6f),
        new("RailNorthProcess", "building-e.glb", new Vector3(-104.0f, 0.02f, -160.0f), Mathf.Pi, 6.8f),
        new("RailSignalOffice", "building-h.glb", new Vector3(-49.0f, 0.02f, -154.0f), Mathf.Pi * 0.5f, 5.6f),

        new("MaintenanceAssemblyPlant", "building-a.glb", new Vector3(-6.0f, 0.02f, -82.0f), 0.0f, 6.4f),
        new("MaintenanceServicePlant", "building-t.glb", new Vector3(27.0f, 0.02f, -92.0f), Mathf.Pi, 5.7f),
        new("CentralPumpHouse", "building-f.glb", new Vector3(21.0f, 0.02f, -118.0f), Mathf.Pi * 0.5f, 5.7f),
        new("CentralControlOffice", "building-j.glb", new Vector3(-19.0f, 0.02f, -119.0f), -Mathf.Pi * 0.5f, 5.8f),

        new("FuelProcessPlant", "building-l.glb", new Vector3(111.0f, 0.02f, -103.0f), Mathf.Pi, 6.3f),
        new("FuelControlPlant", "building-n.glb", new Vector3(108.0f, 0.02f, -140.0f), Mathf.Pi, 5.8f),
        new("FuelTankWest", "detail-tank.glb", new Vector3(58.0f, 0.02f, -108.0f), 0.0f, 8.0f),
        new("FuelTankEast", "detail-tank.glb", new Vector3(87.0f, 0.02f, -108.0f), Mathf.Pi * 0.5f, 8.0f),
        new("FuelStack", "chimney-large.glb", new Vector3(115.0f, 0.02f, -151.0f), 0.0f, 7.2f),

        new("QuayBondedPlant", "building-r.glb", new Vector3(16.0f, 0.02f, -174.0f), 0.0f, 6.6f),
        new("QuayServicePlant", "building-g.glb", new Vector3(54.0f, 0.02f, -174.0f), 0.0f, 6.4f),
        new("QuayPumpPlant", "building-b.glb", new Vector3(102.0f, 0.02f, -175.0f), 0.0f, 6.4f),
        new("QuayStack", "chimney-medium.glb", new Vector3(127.0f, 0.02f, -179.0f), 0.0f, 6.5f),

        new("SouthCustomsPlant", "building-q.glb", new Vector3(-78.0f, 0.02f, 53.0f), Mathf.Pi, 6.4f),
        new("SouthWorkshopPlant", "building-g.glb", new Vector3(-31.0f, 0.02f, 62.0f), Mathf.Pi, 6.0f),
        new("SouthSecurityPlant", "building-h.glb", new Vector3(31.0f, 0.02f, 62.0f), Mathf.Pi, 6.0f),
        new("SouthCommandPlant", "building-a.glb", new Vector3(82.0f, 0.02f, 58.0f), Mathf.Pi, 6.4f),

        new("WestBoundaryPlant", "building-c.glb", new Vector3(-158.0f, 0.02f, -137.0f), Mathf.Pi * 0.5f, 7.0f),
        new("EastBoundaryPlant", "building-e.glb", new Vector3(158.0f, 0.02f, -136.0f), -Mathf.Pi * 0.5f, 7.0f),
        new("WestBoundaryStack", "chimney-large.glb", new Vector3(-158.0f, 0.02f, -181.0f), 0.0f, 7.4f),
        new("EastBoundaryStack", "chimney-large.glb", new Vector3(158.0f, 0.02f, -181.0f), 0.0f, 7.4f)
    };

    private readonly record struct ModelPlacement(
        string Name,
        string File,
        Vector3 Position,
        float Yaw,
        float Scale);
}
