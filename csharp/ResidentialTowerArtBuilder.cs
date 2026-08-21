using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record ResidentialTowerArtResult(
    int AuthoredModelCount,
    int CollisionShapeCount,
    IReadOnlyCollection<string> ScenePaths);

/// <summary>Places licensed authored annex and rooftop models around an enterable tower shell.</summary>
internal sealed class ResidentialTowerArtBuilder
{
    private const string CityRoot = "res://assets/models/kenney_city_kit_industrial";
    private const string FactoryRoot = "res://assets/models/kenney_factory_kit";

    private static readonly ModelAsset FamilyAnnex = City(
        "building-t.glb", new Vector3(1.72f, 1.01f, 1.39f), new Vector3(0, 0.505f, 0));
    private static readonly ModelAsset ClinicAnnex = City(
        "building-q.glb", new Vector3(2.14f, 0.88f, 1.77f), new Vector3(-0.23f, 0.44f, 0.015f));
    private static readonly ModelAsset MarketAnnex = City(
        "building-r.glb", new Vector3(2.48f, 1.39f, 1.27f), new Vector3(0, 0.695f, 0));
    private static readonly ModelAsset WorkshopAnnex = City(
        "building-g.glb", new Vector3(1.68f, 1.28f, 1.28f), new Vector3(0, 0.64f, 0));
    private static readonly ModelAsset SecurityAnnex = City(
        "building-f.glb", new Vector3(1.79f, 1.925f, 1.28f), new Vector3(-0.453f, 0.963f, 0.254f));
    private static readonly ModelAsset ShelterAnnex = City(
        "building-l.glb", new Vector3(2.084f, 1.925f, 1.87f), new Vector3(-0.422f, 0.963f, 0.224f));
    private static readonly ModelAsset RooftopStore = City(
        "building-j.glb", new Vector3(1.03f, 0.86f, 1.3f), new Vector3(-0.435f, 0.43f, 0.27f));
    private static readonly ModelAsset WaterTank = City(
        "detail-tank.glb", new Vector3(0.85f, 0.42f, 0.52f), new Vector3(0, 0.21f, 0));
    private static readonly ModelAsset RelayMast = City(
        "chimney-small.glb", new Vector3(0.3f, 0.75f, 0.3f), new Vector3(0, 0.375f, 0));
    private static readonly ModelAsset WorkshopMachine = Factory(
        "machine.glb", new Vector3(1.2f, 1.3f, 1.5f), new Vector3(0, 0.65f, 0));
    private static readonly ModelAsset ClinicMachine = Factory(
        "machine-window.glb", new Vector3(1.2f, 1.29f, 1.5f), new Vector3(0, 0.645f, 0));
    private static readonly ModelAsset ShelterHopper = Factory(
        "hopper-high-round.glb", new Vector3(1.12f, 1.5f, 1.12f), new Vector3(0, 0.75f, 0));
    private static readonly ModelAsset WorkshopPipe = Factory(
        "pipe-large-bend.glb", new Vector3(1.9f, 1.0f, 1.9f), new Vector3(0.05f, 0.5f, -0.45f));

    private readonly Dictionary<string, PackedScene> _scenes = new();

    public ResidentialTowerArtResult Build(
        Node3D tower,
        ResidentialTowerDiversityProfile profile,
        Vector2 footprint,
        float roofY)
    {
        var placements = BuildPlacements(profile, footprint, roofY);
        var scenePaths = new HashSet<string>();
        var modelCount = 0;
        var collisionCount = 0;
        foreach (var placement in placements)
        {
            if (!TryAddModel(tower, profile.TowerIndex, placement, scenePaths))
            {
                continue;
            }
            modelCount++;
            collisionCount++;
        }
        return new ResidentialTowerArtResult(modelCount, collisionCount, scenePaths);
    }

    private static List<ModelPlacement> BuildPlacements(
        ResidentialTowerDiversityProfile profile,
        Vector2 footprint,
        float roofY)
    {
        var placements = new List<ModelPlacement>(5);
        var mirror = profile.TowerIndex % 2 == 0 ? -1.0f : 1.0f;
        var (annex, annexScale) = profile.ArtTheme switch
        {
            ResidentialArtTheme.ClinicServices => (ClinicAnnex, 2.25f),
            ResidentialArtTheme.ShelterUtilities => (ShelterAnnex, 2.05f),
            ResidentialArtTheme.WorkshopPlant => (WorkshopAnnex, 2.35f),
            ResidentialArtTheme.SecurityRelay => (SecurityAnnex, 2.15f),
            ResidentialArtTheme.MarketPodium => (MarketAnnex, 1.95f),
            _ => (FamilyAnnex, 2.25f)
        };
        var annexHalfWidth = annex.Size.X * annexScale * 0.5f;
        placements.Add(new ModelPlacement(
            "Annex",
            annex,
            new Vector3(
                mirror * (footprint.X * 0.5f + annexHalfWidth + 0.45f),
                0.06f,
                footprint.Y * 0.24f),
            mirror < 0 ? 90.0f : -90.0f,
            annexScale,
            "annex"));

        var (roofHouse, roofHouseScale) = profile.ArtTheme switch
        {
            ResidentialArtTheme.ClinicServices => (ClinicAnnex, 2.65f),
            ResidentialArtTheme.ShelterUtilities => (ShelterAnnex, 2.45f),
            ResidentialArtTheme.WorkshopPlant => (WorkshopAnnex, 2.7f),
            ResidentialArtTheme.SecurityRelay => (SecurityAnnex, 2.55f),
            ResidentialArtTheme.MarketPodium => (MarketAnnex, 2.35f),
            _ => (RooftopStore, 2.75f)
        };
        placements.Add(new ModelPlacement(
            "RoofHouse",
            roofHouse,
            new Vector3(mirror * footprint.X * 0.32f, roofY + 0.12f, -footprint.Y * 0.24f),
            mirror < 0 ? 180.0f : 0.0f,
            roofHouseScale,
            "roof_house"));

        var roofA = new Vector3(-mirror * footprint.X * 0.29f, roofY + 0.12f, -footprint.Y * 0.29f);
        var roofB = new Vector3(mirror * footprint.X * 0.29f, roofY + 0.12f, footprint.Y * 0.27f);
        switch (profile.ArtTheme)
        {
            case ResidentialArtTheme.ClinicServices:
                placements.Add(new ModelPlacement("ClinicAirHandler", ClinicMachine, roofA, 90, 2.25f, "roof"));
                placements.Add(new ModelPlacement("ClinicWaterReserve", WaterTank, roofB, 0, 3.1f, "roof"));
                break;
            case ResidentialArtTheme.ShelterUtilities:
                placements.Add(new ModelPlacement("ShelterHopper", ShelterHopper, roofA, 0, 2.15f, "roof"));
                placements.Add(new ModelPlacement("ShelterWaterReserve", WaterTank, roofB, 90, 3.25f, "roof"));
                break;
            case ResidentialArtTheme.WorkshopPlant:
                placements.Add(new ModelPlacement("WorkshopMachine", WorkshopMachine, roofA, 90, 2.35f, "roof"));
                placements.Add(new ModelPlacement("WorkshopPipe", WorkshopPipe, roofB, mirror < 0 ? 0 : 180, 1.8f, "roof"));
                break;
            case ResidentialArtTheme.SecurityRelay:
                placements.Add(new ModelPlacement("RelayConsole", ClinicMachine, roofA, 0, 1.9f, "roof"));
                for (var mastIndex = 0; mastIndex < 3; mastIndex++)
                {
                    placements.Add(new ModelPlacement(
                        $"RelayMast_{mastIndex + 1:00}",
                        RelayMast,
                        new Vector3(
                            mirror * footprint.X * (0.16f + mastIndex * 0.09f),
                            roofY + 0.12f,
                            footprint.Y * (0.2f - mastIndex * 0.12f)),
                        0,
                        3.2f + mastIndex * 0.45f,
                        "roof"));
                }
                break;
            case ResidentialArtTheme.MarketPodium:
                placements.Add(new ModelPlacement("MarketRoofStore", RooftopStore, roofA, 180, 2.15f, "roof"));
                placements.Add(new ModelPlacement("MarketWaterReserve", WaterTank, roofB, 90, 3.0f, "roof"));
                break;
            default:
                placements.Add(new ModelPlacement("FamilyRoofStore", RooftopStore, roofA, 0, 2.0f, "roof"));
                placements.Add(new ModelPlacement("FamilyWaterReserve", WaterTank, roofB, 0, 3.2f, "roof"));
                break;
        }
        return placements;
    }

    private bool TryAddModel(
        Node3D tower,
        int towerIndex,
        ModelPlacement placement,
        HashSet<string> scenePaths)
    {
        if (!_scenes.TryGetValue(placement.Asset.Path, out var scene))
        {
            scene = GD.Load<PackedScene>(placement.Asset.Path);
            if (scene is null)
            {
                GD.PushError($"Residential authored model is missing: {placement.Asset.Path}");
                return false;
            }
            _scenes[placement.Asset.Path] = scene;
        }
        if (scene.Instantiate() is not Node3D model)
        {
            GD.PushError($"Residential authored model could not instantiate: {placement.Asset.Path}");
            return false;
        }

        var body = new StaticBody3D
        {
            Name = $"ResidentialAuthored_T{towerIndex + 1:00}_{placement.Name}",
            Position = placement.Position,
            RotationDegrees = new Vector3(0, placement.YawDegrees, 0),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup("residential_authored_dressing");
        body.SetMeta("residential_tower_index", towerIndex);
        body.SetMeta("residential_scene_path", placement.Asset.Path);
        body.SetMeta("residential_placement_kind", placement.Kind);

        model.Name = "Model";
        model.Scale = Vector3.One * placement.Scale;
        ConfigureVisuals(model);
        body.AddChild(model);
        body.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Position = placement.Asset.Center * placement.Scale,
            Shape = new BoxShape3D { Size = placement.Asset.Size * placement.Scale }
        });
        tower.AddChild(body);
        scenePaths.Add(placement.Asset.Path);
        return true;
    }

    private static void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = 260.0f;
            visual.VisibilityRangeEndMargin = 20.0f;
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

    private static ModelAsset City(string file, Vector3 size, Vector3 center)
        => new($"{CityRoot}/{file}", size, center);

    private static ModelAsset Factory(string file, Vector3 size, Vector3 center)
        => new($"{FactoryRoot}/{file}", size, center);

    private readonly record struct ModelAsset(string Path, Vector3 Size, Vector3 Center);

    private readonly record struct ModelPlacement(
        string Name,
        ModelAsset Asset,
        Vector3 Position,
        float YawDegrees,
        float Scale,
        string Kind);
}
