using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace OperationSteelTide;

internal sealed record FreightIndustrialPortalLayout(
    Vector2 Center,
    Vector2 OutwardNormal,
    float Width,
    float Height,
    BuildingDoorMotionStyle MotionStyle);

internal sealed record FreightIndustrialBuildingLayout(
    string ModelId,
    Rect2 Bounds,
    Vector2 InteriorAnchor,
    IReadOnlyList<FreightIndustrialPortalLayout> Portals);

internal readonly record struct FreightIndustrialBuildingPlacement(
    string Name,
    string ModelId,
    Vector3 Position,
    float Yaw,
    float Scale,
    bool IsLandmark = false);

/// <summary>Shared runtime catalog for the industrial buildings edited by the Blender pipeline.</summary>
internal static class FreightIndustrialBuildingCatalog
{
    public const string LayoutCatalogPath =
        "res://assets/models/kenney_city_kit_industrial/enterable_layouts.json";
    public const string EnterableModelRoot =
        "res://assets/models/kenney_city_kit_industrial/enterable";

    public static readonly IReadOnlyList<FreightIndustrialBuildingPlacement> Placements =
        new FreightIndustrialBuildingPlacement[]
        {
            new("WestRailAdministrationTower", "building-q", new Vector3(-132, 0.02f, -84), 0.0f, 8.0f, true),
            new("EastTankProcessTower", "building-l", new Vector3(128, 0.02f, -48), Mathf.Pi, 7.8f, true),
            new("SouthFreightControlTower", "building-a", new Vector3(-125, 0.02f, 22), 0.0f, 8.2f, true),
            new("NorthQuaySignalTower", "building-r", new Vector3(44, 0.02f, -204), Mathf.Pi, 7.4f, true),

            new("RailDispatchPlant", "building-j", new Vector3(-105.0f, 0.02f, -72.0f), 0.0f, 6.2f),
            new("RailLoadingPlant", "building-c", new Vector3(-61.0f, 0.02f, -91.0f), Mathf.Pi, 6.6f),
            new("RailNorthProcess", "building-e", new Vector3(-104.0f, 0.02f, -160.0f), Mathf.Pi, 6.8f),
            new("RailSignalOffice", "building-h", new Vector3(-49.0f, 0.02f, -154.0f), Mathf.Pi * 0.5f, 5.6f),

            new("MaintenanceAssemblyPlant", "building-a", new Vector3(-6.0f, 0.02f, -82.0f), 0.0f, 6.4f),
            new("MaintenanceServicePlant", "building-t", new Vector3(27.0f, 0.02f, -80.0f), Mathf.Pi, 5.7f),
            new("CentralPumpHouse", "building-f", new Vector3(21.0f, 0.02f, -118.0f), Mathf.Pi * 0.5f, 5.7f),
            new("CentralControlOffice", "building-j", new Vector3(-19.0f, 0.02f, -119.0f), -Mathf.Pi * 0.5f, 5.8f),

            new("FuelProcessPlant", "building-l", new Vector3(123.0f, 0.02f, -115.0f), Mathf.Pi, 6.3f),
            new("FuelControlPlant", "building-n", new Vector3(108.0f, 0.02f, -140.0f), Mathf.Pi, 5.8f),
            new("QuayBondedPlant", "building-r", new Vector3(16.0f, 0.02f, -174.0f), 0.0f, 6.6f),
            new("QuayServicePlant", "building-g", new Vector3(54.0f, 0.02f, -174.0f), 0.0f, 6.4f),
            new("QuayPumpPlant", "building-b", new Vector3(122.0f, 0.02f, -204.0f), 0.0f, 6.4f),

            new("SouthCustomsPlant", "building-q", new Vector3(-78.0f, 0.02f, 42.0f), Mathf.Pi, 6.4f),
            new("SouthWorkshopPlant", "building-g", new Vector3(-37.0f, 0.02f, 56.0f), Mathf.Pi, 6.0f),
            new("SouthSecurityPlant", "building-h", new Vector3(31.0f, 0.02f, 62.0f), Mathf.Pi, 6.0f),
            new("SouthCommandPlant", "building-a", new Vector3(116.0f, 0.02f, 58.0f), Mathf.Pi, 6.4f),

            new("WestBoundaryPlant", "building-c", new Vector3(-158.0f, 0.02f, -137.0f), Mathf.Pi * 0.5f, 7.0f),
            new("EastBoundaryPlant", "building-e", new Vector3(158.0f, 0.02f, -136.0f), -Mathf.Pi * 0.5f, 7.0f)
        };

    public static IReadOnlyDictionary<string, FreightIndustrialBuildingLayout> LoadLayouts()
    {
        var json = FileAccess.GetFileAsString(LayoutCatalogPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Industrial layout catalog is empty: {LayoutCatalogPath}");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1)
        {
            throw new InvalidOperationException("Unsupported industrial layout catalog version.");
        }

        var layouts = new Dictionary<string, FreightIndustrialBuildingLayout>(StringComparer.Ordinal);
        foreach (var model in root.GetProperty("models").EnumerateArray())
        {
            var id = model.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Industrial model id is missing.");
            var bounds = model.GetProperty("bounds");
            var minimum = new Vector2(bounds[0].GetSingle(), bounds[1].GetSingle());
            var maximum = new Vector2(bounds[2].GetSingle(), bounds[3].GetSingle());
            var interior = model.GetProperty("interior");
            var interiorAnchor = new Vector2(
                interior[0].GetSingle(),
                interior[1].GetSingle());
            var portals = new List<FreightIndustrialPortalLayout>();
            foreach (var portal in model.GetProperty("portals").EnumerateArray())
            {
                var center = portal.GetProperty("center");
                var normal = portal.GetProperty("normal");
                var style = portal.GetProperty("style").GetString();
                portals.Add(new FreightIndustrialPortalLayout(
                    new Vector2(center[0].GetSingle(), center[1].GetSingle()),
                    new Vector2(normal[0].GetSingle(), normal[1].GetSingle()).Normalized(),
                    portal.GetProperty("width").GetSingle(),
                    portal.GetProperty("height").GetSingle(),
                    style == "hinged"
                        ? BuildingDoorMotionStyle.Hinged
                        : BuildingDoorMotionStyle.Overhead));
            }

            if (portals.Count == 0 || !layouts.TryAdd(
                    id,
                    new FreightIndustrialBuildingLayout(
                        id,
                        new Rect2(minimum, maximum - minimum),
                        interiorAnchor,
                        portals)))
            {
                throw new InvalidOperationException($"Invalid or duplicate industrial layout: {id}");
            }
        }
        return layouts;
    }

    public static string ScenePath(string modelId)
        => $"{EnterableModelRoot}/{modelId}-enterable.glb";
}
