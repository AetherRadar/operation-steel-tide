using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record DemolitionArenaDressingResult(
    int AuthoredModelCount,
    int MissingModelCount,
    int PalettedBuildingCount,
    IReadOnlyCollection<string> ScenePaths);

/// <summary>Layers licensed authored art over the demolition arenas without changing gameplay collision.</summary>
internal sealed class DemolitionArenaDressingBuilder
{
    private const string IndustrialRoot = "res://assets/models/kenney_city_kit_industrial";
    private const string DowntownRoot = "res://assets/models/quaternius_downtown_city";
    private const string FactoryRoot = "res://assets/models/kenney_factory_kit";
    private const string MajadroidRoot = "res://assets/models/majadroid_construction_site";
    private const string RoadsRoot = "res://assets/models/kenney_city_kit_roads";
    private const string TreyIndustrialRoot = "res://assets/models/trey_modular_industrial";
    private const string BazaarRoot = "res://assets/models/bazaar_crossing";

    private readonly Dictionary<string, PackedScene> _scenes = new();
    private readonly FreightIndustrialPalette _palette;

    public DemolitionArenaDressingBuilder(FreightIndustrialPalette palette)
    {
        _palette = palette;
    }

    public DemolitionArenaDressingResult Build(Node3D parent, DemolitionArenaLayout layout)
    {
        var root = new Node3D { Name = "DemolitionAuthoredDressing" };
        root.AddToGroup("demolition_authored_dressing");
        parent.AddChild(root);

        var scenePaths = new HashSet<string>();
        var placements = layout.MapId == DemolitionMapCatalog.HarborLocksId
            ? HarborLocksPlacements(layout.Origin)
            : layout.MapId == DemolitionMapCatalog.TideglassReactorId
                ? TideglassReactorPlacements(layout.Origin)
                : layout.MapId == DemolitionMapCatalog.BazaarCrossingId
                    ? BazaarCrossingPlacements(layout.Origin)
                    : TideforgePlacements(layout.Origin);
        var authoredModelCount = 0;
        var missingModelCount = 0;
        var palettedBuildingCount = 0;
        foreach (var placement in placements)
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
        return new DemolitionArenaDressingResult(
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
        var path = placement.Source switch
        {
            ModelSource.Downtown => $"{DowntownRoot}/{placement.File}",
            ModelSource.Factory => $"{FactoryRoot}/{placement.File}",
            ModelSource.Majadroid => $"{MajadroidRoot}/{placement.File}",
            ModelSource.Roads => $"{RoadsRoot}/{placement.File}",
            ModelSource.TreyIndustrial => $"{TreyIndustrialRoot}/{placement.File}",
            ModelSource.BazaarCrossing => $"{BazaarRoot}/{placement.File}",
            _ => $"{IndustrialRoot}/{placement.File}"
        };
        if (!_scenes.TryGetValue(path, out var scene))
        {
            scene = GD.Load<PackedScene>(path);
            if (scene is null)
            {
                GD.PushError($"Demolition authored dressing is missing: {path}");
                return false;
            }
            _scenes[path] = scene;
        }
        if (scene.Instantiate() is not Node3D model)
        {
            GD.PushError($"Demolition authored dressing could not instantiate: {path}");
            return false;
        }

        model.Name = placement.Name;
        model.Position = placement.Position;
        model.RotationDegrees = new Vector3(0, placement.YawDegrees, 0);
        model.Scale = placement.Scale;
        model.AddToGroup("demolition_authored_model");
        model.SetMeta("demolition_scene_path", path);
        ConfigureVisuals(model);
        root.AddChild(model);
        if (placement.Source == ModelSource.Industrial
            && placement.File.StartsWith("building-", System.StringComparison.Ordinal))
        {
            paletteApplied = _palette.Apply(model, placement.Name) > 0;
        }
        scenePaths.Add(path);
        return true;
    }

    private static IReadOnlyList<ModelPlacement> TideforgePlacements(Vector3 origin)
    {
        var placements = new List<ModelPlacement>
        {
            Industrial("FoundryBackdropNorth", "building-l.glb", origin, new(-44.0f, 0.02f, -14.0f), 90, 7.4f),
            Industrial("FoundryBackdropSouth", "building-c.glb", origin, new(-44.0f, 0.02f, 29.0f), 90, 7.2f),
            Industrial("AssemblyBackdropNorth", "building-a.glb", origin, new(44.0f, 0.02f, -35.0f), -90, 7.6f),
            Industrial("AssemblyBackdropSouth", "building-e.glb", origin, new(44.0f, 0.02f, 12.0f), -90, 7.2f),
            Industrial("DefenderPlantWest", "building-q.glb", origin, new(-20.0f, 0.02f, -61.0f), 0, 7.0f),
            Industrial("DefenderPlantEast", "building-b.glb", origin, new(20.0f, 0.02f, -61.0f), 180, 7.0f),
            Industrial("AttackPlantWest", "building-r.glb", origin, new(-22.0f, 0.02f, 61.0f), 180, 6.6f),
            Industrial("AttackPlantEast", "building-g.glb", origin, new(22.0f, 0.02f, 61.0f), 180, 6.6f),

            Industrial("MidFoundryHousing", "building-f.glb", origin, new(0.0f, 0.02f, -13.5f), 0, 4.35f),
            Industrial("RelayHousing", "building-j.glb", origin, new(23.0f, 0.02f, 22.0f), 90, 4.2f),
            Industrial("MaintenanceHousing", "building-g.glb", origin, new(-22.0f, 0.02f, -26.0f), 180, 4.1f),
            Industrial("FoundryFurnaceTank", "detail-tank.glb", origin, new(-35.5f, 0.02f, 14.0f), 90, 5.1f),
            Industrial("FoundryMachineTank", "detail-tank.glb", origin, new(-35.5f, 0.02f, 29.0f), 0, 5.0f),
            Industrial("AssemblyMachineTank", "detail-tank.glb", origin, new(36.5f, 0.02f, -28.0f), 90, 5.0f),
            Industrial("WestConverterTankA", "detail-tank.glb", origin, new(-6.8f, 0.02f, 15.9f), 0, 4.6f),
            Industrial("WestConverterTankB", "detail-tank.glb", origin, new(-6.8f, 0.02f, 19.2f), 0, 4.6f),
            Industrial("EastConverterTankA", "detail-tank.glb", origin, new(6.8f, 0.02f, 10.6f), 0, 4.6f),
            Industrial("EastConverterTankB", "detail-tank.glb", origin, new(6.8f, 0.02f, 13.5f), 0, 4.6f),
            Industrial("FoundryStackLarge", "chimney-large.glb", origin, new(-37.2f, 0.02f, 5.0f), 0, 5.8f),
            Industrial("FoundryStackMedium", "chimney-medium.glb", origin, new(-34.0f, 0.02f, 5.0f), 0, 5.2f),
            Industrial("AssemblyStack", "chimney-basic.glb", origin, new(36.0f, 0.02f, -40.0f), 0, 5.0f)
        };

        AddPanelRun(placements, "FoundryWestFacade", "Metal_FirstFloor_Window.gltf", origin,
            new Vector3(-38.46f, 0.0f, 3.0f), Vector3.Back * 4.0f, 10, 90);
        AddPanelRun(placements, "FoundryNorthFacade", "Metal_Window.gltf", origin,
            new Vector3(-39.0f, 0.0f, -0.96f), Vector3.Right * 4.0f, 2, 0);
        AddPanelRun(placements, "FoundrySouthFacade", "Metal_Window.gltf", origin,
            new Vector3(-39.0f, 0.0f, 42.96f), Vector3.Right * 4.0f, 2, 180);
        AddPanelRun(placements, "AssemblyEastFacade", "Metal_FirstFloor_Window.gltf", origin,
            new Vector3(39.46f, 0.0f, -29.0f), Vector3.Back * 4.0f, 6, -90);
        AddPanelRun(placements, "AssemblyNorthFacade", "Metal_Window.gltf", origin,
            new Vector3(16.0f, 0.0f, -44.96f), Vector3.Right * 4.0f, 6, 0);
        AddPanelRun(placements, "AssemblySouthFacade", "Metal_Window.gltf", origin,
            new Vector3(20.0f, 0.0f, -3.04f), Vector3.Right * 4.0f, 4, 180);
        return placements;
    }

    private static IReadOnlyList<ModelPlacement> BazaarCrossingPlacements(Vector3 origin)
    {
        return new[]
        {
            Authored(
                "BazaarCrossingAuthoredEnvironment",
                "bazaar_crossing.glb",
                origin,
                Vector3.Zero,
                0.0f,
                1.0f,
                ModelSource.BazaarCrossing)
        };
    }

    private static IReadOnlyList<ModelPlacement> HarborLocksPlacements(Vector3 origin)
    {
        var placements = new List<ModelPlacement>
        {
            Industrial("WestLockDoor", "door-wide-closed.glb", origin, new(-31.0f, 0.02f, 0.45f), 0, new Vector3(8.2f, 2.15f, 1.0f), ModelSource.Factory),
            Industrial("EastLockDoor", "door-wide-closed.glb", origin, new(31.0f, 0.02f, -0.45f), 180, new Vector3(8.2f, 2.15f, 1.0f), ModelSource.Factory),
            Industrial("WestQuayStack", "chimney-medium.glb", origin, new(-52.0f, 0.02f, -8.0f), 0, 5.2f),
            Industrial("EastQuayStack", "chimney-medium.glb", origin, new(52.0f, 0.02f, 9.0f), 0, 5.2f),
            Industrial("NorthLockTank", "detail-tank.glb", origin, new(6.0f, 0.02f, -10.5f), 90, 4.6f),
            Industrial("SouthLockTank", "detail-tank.glb", origin, new(-6.0f, 0.02f, 10.5f), 90, 4.6f)
        };
        AddPanelRun(placements, "WestTurbineFacade", "Metal_FirstFloor_Window.gltf", origin,
            new Vector3(-49.0f, 0.0f, -10.5f), Vector3.Right * 4.0f, 4, 0);
        AddPanelRun(placements, "SouthAnnexFacade", "Metal_Window.gltf", origin,
            new Vector3(-14.0f, 0.0f, 28.46f), Vector3.Right * 4.0f, 5, 0);
        return placements;
    }

    private static IReadOnlyList<ModelPlacement> TideglassReactorPlacements(Vector3 origin)
    {
        return new[]
        {
            Authored("TideglassRoadBase", "road.glb", origin, new(0.0f, -0.136f, 0.0f), 0.0f, new Vector3(0.82f, 0.78f, 0.70f), ModelSource.Majadroid),
            Authored("TideglassPerimeterFence", "fence.glb", origin, new(0.0f, 0.02f, 0.0f), 0.0f, new Vector3(1.109f, 1.5f, 0.949f), ModelSource.Majadroid),
            Authored("EastPerimeterSecurityGate", "east-security-gate.glb", origin, new(55.5f, 0.02f, -23.63f), -90.0f, new Vector3(1.009f, 0.9375f, 1.0f), ModelSource.TreyIndustrial),
            Authored("WestPerimeterServiceGate", "west-service-gate.glb", origin, new(-55.5f, 0.02f, -37.96f), 90.0f, new Vector3(0.988f, 0.9375f, 1.0f), ModelSource.TreyIndustrial),
            Authored("ConstructionGround", "ground.glb", origin, new(-31.0f, 0.02f, 18.0f), 0.0f, 0.34f, ModelSource.Majadroid),
            Authored("ConstructionBuilding", "building.glb", origin, new(-45.0f, 0.02f, 18.0f), 90.0f, 1.0f, ModelSource.Majadroid),
            Authored("ConstructionCrane", "crane-on-ground.glb", origin, new(-39.0f, 0.02f, -6.2f), -12.0f, 0.85f, ModelSource.Majadroid),

            Authored("OldBrickReactorHall", "Building_Large_2.gltf", origin, new(43.0f, 0.02f, -9.75f), -90.0f, 0.75f, ModelSource.Downtown),

            Authored("OrangeArchGateway", "arch-gateway.glb", origin, new(0.0f, 0.02f, -31.5f), 0.0f, 1.25f, ModelSource.TreyIndustrial),
            Authored("CivicElevatedWalkway", "elevated-walkway.glb", origin, new(0.0f, 0.02f, 26.5f), 0.0f, 1.15f, ModelSource.TreyIndustrial),

            Authored("CivicRoadNorthWest", "road-side.glb", origin, new(-18.0f, -0.095f, 18.0f), 0.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicRoadNorth", "road-straight.glb", origin, new(0.0f, -0.095f, 18.0f), 0.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicRoadNorthEast", "road-intersection.glb", origin, new(18.0f, -0.095f, 18.0f), 90.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicRoadWest", "road-bend.glb", origin, new(-18.0f, -0.095f, 0.0f), 0.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicCrossroad", "road-crossroad.glb", origin, new(0.0f, -0.095f, 0.0f), 0.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicRoadEast", "road-crossing.glb", origin, new(18.0f, -0.095f, 0.0f), 90.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicRoadSouthWest", "road-driveway-double.glb", origin, new(-18.0f, -0.095f, -18.0f), 180.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),
            Authored("CivicRoadSouthEast", "road-square.glb", origin, new(18.0f, -0.095f, -18.0f), 0.0f, new Vector3(18.0f, 6.0f, 18.0f), ModelSource.Roads),

            Authored("ConstructionCone", "construction-cone.glb", origin, new(-25.0f, 0.02f, 12.0f), 0.0f, 1.2f, ModelSource.Roads),
            Authored("ConstructionWorkLight", "construction-light.glb", origin, new(-25.0f, 0.02f, 22.0f), 0.0f, 1.4f, ModelSource.Roads),
            Authored("CivicPowerPole", "electricity-pole-wide.glb", origin, new(19.0f, 0.02f, 24.0f), 90.0f, 1.4f, ModelSource.Roads),
            Authored("ConstructionStreetlight", "light-curved.glb", origin, new(-32.0f, 0.02f, 7.0f), 0.0f, 1.35f, ModelSource.Roads),
            Authored("ReactorStreetlight", "light-square-double.glb", origin, new(35.0f, 0.02f, -29.0f), 0.0f, 1.35f, ModelSource.Roads),
            Authored("ConstructionWarningSign", "road-sign-warning.glb", origin, new(-24.0f, 0.02f, 31.0f), 35.0f, 1.3f, ModelSource.Roads),
            Authored("CrossingStopSign", "road-sign-stop.glb", origin, new(10.0f, 0.02f, 3.0f), -90.0f, 1.3f, ModelSource.Roads),
            Authored("ReactorHangingSignal", "traffic-light-hanging.glb", origin, new(27.0f, 0.02f, -7.0f), 90.0f, 1.0f, ModelSource.Roads)
        };
    }

    private static void AddPanelRun(
        List<ModelPlacement> placements,
        string prefix,
        string file,
        Vector3 origin,
        Vector3 first,
        Vector3 step,
        int count,
        float yawDegrees)
    {
        for (var index = 0; index < count; index++)
        {
            placements.Add(new ModelPlacement(
                $"{prefix}_{index + 1:00}",
                file,
                origin + first + step * index,
                yawDegrees,
                Vector3.One * 2.0f,
                ModelSource.Downtown));
        }
    }

    private static ModelPlacement Industrial(
        string name,
        string file,
        Vector3 origin,
        Vector3 localPosition,
        float yawDegrees,
        float scale)
        => Industrial(name, file, origin, localPosition, yawDegrees, Vector3.One * scale, ModelSource.Industrial);

    private static ModelPlacement Industrial(
        string name,
        string file,
        Vector3 origin,
        Vector3 localPosition,
        float yawDegrees,
        Vector3 scale,
        ModelSource source)
        => new(name, file, origin + localPosition, yawDegrees, scale, source);

    private static ModelPlacement Authored(
        string name,
        string file,
        Vector3 origin,
        Vector3 localPosition,
        float yawDegrees,
        float scale,
        ModelSource source)
        => new(name, file, origin + localPosition, yawDegrees, Vector3.One * scale, source);

    private static ModelPlacement Authored(
        string name,
        string file,
        Vector3 origin,
        Vector3 localPosition,
        float yawDegrees,
        Vector3 scale,
        ModelSource source)
        => new(name, file, origin + localPosition, yawDegrees, scale, source);

    private static void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = 240.0f;
            visual.VisibilityRangeEndMargin = 18.0f;
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

    private enum ModelSource
    {
        Industrial,
        Downtown,
        Factory,
        Majadroid,
        Roads,
        TreyIndustrial,
        BazaarCrossing
    }

    private readonly record struct ModelPlacement(
        string Name,
        string File,
        Vector3 Position,
        float YawDegrees,
        Vector3 Scale,
        ModelSource Source);
}
