using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record OldTownLandmarksResult(
    int LandmarkCount,
    int HighValueZoneCount,
    int AuthoredModelCount,
    int CollisionShapeCount,
    int EntryCount,
    int RooftopRouteCount,
    Vector3 HotelCenter,
    Vector3 HotelEntry,
    Vector3 HotelInterior,
    Vector3 TreasuryCenter,
    Vector3 TreasuryEntry,
    Vector3 TreasuryInterior,
    IReadOnlyList<Vector3> RooftopRoute,
    IReadOnlyCollection<string> ScenePaths);

/// <summary>Composes the two loot courtyards and the elevated market route from CC0 authored modules.</summary>
internal sealed class OldTownLandmarksBuilder
{
    private const string ModelRoot = "res://assets/models/quaternius_downtown_city";
    private static readonly Vector3 HotelCenter = RefineryExtractionMapBuilder.HotelCenter;
    private static readonly Vector3 TreasuryCenter = RefineryExtractionMapBuilder.TreasuryCenter;
    private const float RooftopZ = -126.0f;

    public OldTownLandmarksResult Build(Node3D parent)
    {
        var root = new Node3D { Name = "OldTownLandmarks" };
        root.AddToGroup("refinery_authored_model");
        root.AddToGroup("old_town_landmarks");
        parent.AddChild(root);

        var collisionBody = new StaticBody3D
        {
            Name = "OldTownLandmarkCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(collisionBody);

        var scenes = new Dictionary<string, PackedScene>();
        var sources = new HashSet<string>();
        var counts = new BuildCounts();
        BuildGrandHotel(root, collisionBody, scenes, sources, counts);
        BuildMunicipalTreasury(root, collisionBody, scenes, sources, counts);
        var rooftopRoute = BuildMarketRooftop(root, collisionBody, scenes, sources, counts);
        AddDistrictLabels(root);

        return new OldTownLandmarksResult(
            3,
            2,
            counts.AuthoredModels,
            counts.CollisionShapes,
            2,
            1,
            HotelCenter,
            HotelCenter + new Vector3(0, 1.0f, 13.8f),
            HotelCenter + new Vector3(0, 1.0f, 7.0f),
            TreasuryCenter,
            TreasuryCenter + new Vector3(0, 1.0f, -13.8f),
            TreasuryCenter + new Vector3(0, 1.0f, -7.0f),
            rooftopRoute,
            sources);
    }

    private static void BuildGrandHotel(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        AddCourtyardFloor(root, scenes, sources, HotelCenter, "Hotel", counts);
        AddPanelRow(root, scenes, sources, "HotelNorth", "Brick_Window_CurvedDouble.gltf",
            HotelCenter + new Vector3(0, 0, -12), 0.0f, horizontal: true, counts);
        AddPanelRow(root, scenes, sources, "HotelWest", "Brick_Window_Trim.gltf",
            HotelCenter + new Vector3(-12, 0, 0), 90.0f, horizontal: false, counts);
        AddPanelRow(root, scenes, sources, "HotelEast", "Brick_Window_Trim.gltf",
            HotelCenter + new Vector3(12, 0, 0), -90.0f, horizontal: false, counts);
        AddEntrySide(root, scenes, sources, "HotelSouth", "Brick_RedWhite_DoubleWindow.gltf",
            HotelCenter + new Vector3(0, 0, 12), 180.0f, counts);
        AddModel(root, scenes, sources, "HotelGate", "DoorFrame_Trim.gltf",
            HotelCenter + new Vector3(0, 0, 12.05f), new Vector3(0, 180, 0), 2.0f, counts);
        AddModel(root, scenes, sources, "HotelSteps", "Stairs_Entrance_Concrete.gltf",
            HotelCenter + new Vector3(0, 0, 14.0f), new Vector3(0, 180, 0), 2.0f, counts);

        AddCourtyardCollision(collisionBody, "Hotel", HotelCenter, entrySouth: true, counts);
        AddCourtyardPlanters(root, scenes, sources, HotelCenter, "Hotel", counts);
    }

    private static void BuildMunicipalTreasury(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        AddCourtyardFloor(root, scenes, sources, TreasuryCenter, "Treasury", counts);
        AddPanelRow(root, scenes, sources, "TreasurySouth", "Metal_Window.gltf",
            TreasuryCenter + new Vector3(0, 0, 12), 180.0f, horizontal: true, counts);
        AddPanelRow(root, scenes, sources, "TreasuryWest", "Metal_FirstFloor_Window.gltf",
            TreasuryCenter + new Vector3(-12, 0, 0), 90.0f, horizontal: false, counts);
        AddPanelRow(root, scenes, sources, "TreasuryEast", "Metal_FirstFloor_Window.gltf",
            TreasuryCenter + new Vector3(12, 0, 0), -90.0f, horizontal: false, counts);
        AddEntrySide(root, scenes, sources, "TreasuryNorth", "Metal_Window.gltf",
            TreasuryCenter + new Vector3(0, 0, -12), 0.0f, counts);
        AddModel(root, scenes, sources, "TreasuryGate", "DoorFrame_Trim.gltf",
            TreasuryCenter + new Vector3(0, 0, -12.05f), Vector3.Zero, 2.0f, counts);
        AddModel(root, scenes, sources, "TreasurySteps", "Stairs_Entrance_Concrete.gltf",
            TreasuryCenter + new Vector3(0, 0, -14.0f), Vector3.Zero, 2.0f, counts);

        AddCourtyardCollision(collisionBody, "Treasury", TreasuryCenter, entrySouth: false, counts);
        AddCourtyardPlanters(root, scenes, sources, TreasuryCenter, "Treasury", counts);
    }

    private static IReadOnlyList<Vector3> BuildMarketRooftop(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        var tileIndex = 0;
        for (var x = -20.0f; x <= 20.0f; x += 4.0f)
        {
            AddModel(root, scenes, sources, $"RooftopDeck_{tileIndex++:00}", "Floor_4x4.gltf",
                new Vector3(x, 4.25f, RooftopZ), Vector3.Zero, 1.0f, counts);
            AddModel(root, scenes, sources, $"RooftopNorthRail_{tileIndex:00}", "Brick_Plain_1.gltf",
                new Vector3(x, 4.35f, RooftopZ - 2.1f), Vector3.Zero, 2.0f, counts);
            AddModel(root, scenes, sources, $"RooftopSouthRail_{tileIndex:00}", "Brick_Plain_1.gltf",
                new Vector3(x, 4.35f, RooftopZ + 2.1f), new Vector3(0, 180, 0), 2.0f, counts);
        }

        for (var step = 0; step < 3; step++)
        {
            var rise = step * 1.4f;
            AddModel(root, scenes, sources, $"RooftopWestStair_{step:00}", "Stairs_Entrance_Concrete.gltf",
                new Vector3(-34.0f + step * 3.4f, rise, RooftopZ), new Vector3(0, -90, 0), 1.4f, counts);
            AddModel(root, scenes, sources, $"RooftopEastStair_{step:00}", "Stairs_Entrance_Concrete.gltf",
                new Vector3(34.0f - step * 3.4f, rise, RooftopZ), new Vector3(0, 90, 0), 1.4f, counts);
        }

        AddCollision(collisionBody, "RooftopDeckCollision", new Vector3(0, 4.14f, RooftopZ),
            new Vector3(45.0f, 0.34f, 4.4f), Vector3.Zero, counts);
        AddCollision(collisionBody, "RooftopNorthGuard", new Vector3(0, 5.1f, RooftopZ - 2.15f),
            new Vector3(45.0f, 1.9f, 0.3f), Vector3.Zero, counts);
        AddCollision(collisionBody, "RooftopSouthGuard", new Vector3(0, 5.1f, RooftopZ + 2.15f),
            new Vector3(45.0f, 1.9f, 0.3f), Vector3.Zero, counts);
        AddCollision(collisionBody, "RooftopWestRamp", new Vector3(-29.0f, 2.05f, RooftopZ),
            new Vector3(12.5f, 0.38f, 4.0f), new Vector3(0, 0, 18.7f), counts);
        AddCollision(collisionBody, "RooftopEastRamp", new Vector3(29.0f, 2.05f, RooftopZ),
            new Vector3(12.5f, 0.38f, 4.0f), new Vector3(0, 0, -18.7f), counts);

        return new[]
        {
            new Vector3(-36.0f, 0.2f, RooftopZ),
            new Vector3(-31.0f, 1.35f, RooftopZ),
            new Vector3(-26.0f, 3.0f, RooftopZ),
            new Vector3(-21.0f, 4.45f, RooftopZ),
            new Vector3(0, 4.45f, RooftopZ),
            new Vector3(21.0f, 4.45f, RooftopZ),
            new Vector3(26.0f, 3.0f, RooftopZ),
            new Vector3(31.0f, 1.35f, RooftopZ),
            new Vector3(36.0f, 0.2f, RooftopZ)
        };
    }

    private static void AddCourtyardFloor(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        Vector3 center,
        string prefix,
        BuildCounts counts)
    {
        var index = 0;
        foreach (var x in new[] { -9.0f, -3.0f, 3.0f, 9.0f })
        {
            foreach (var z in new[] { -9.0f, -3.0f, 3.0f, 9.0f })
            {
                AddModel(root, scenes, sources, $"{prefix}Floor_{index++:00}", "Floor_4x4.gltf",
                    center + new Vector3(x, 0.07f, z), Vector3.Zero, 1.5f, counts);
            }
        }
    }

    private static void AddPanelRow(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        string prefix,
        string file,
        Vector3 center,
        float yawDegrees,
        bool horizontal,
        BuildCounts counts)
    {
        var offsets = new[] { -10.0f, -6.0f, -2.0f, 2.0f, 6.0f, 10.0f };
        for (var index = 0; index < offsets.Length; index++)
        {
            var offset = horizontal
                ? new Vector3(offsets[index], 0, 0)
                : new Vector3(0, 0, offsets[index]);
            AddModel(root, scenes, sources, $"{prefix}_{index:00}", file,
                center + offset, new Vector3(0, yawDegrees, 0), 2.0f, counts);
        }
    }

    private static void AddEntrySide(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        string prefix,
        string file,
        Vector3 center,
        float yawDegrees,
        BuildCounts counts)
    {
        var offsets = new[] { -10.0f, -6.0f, 6.0f, 10.0f };
        for (var index = 0; index < offsets.Length; index++)
        {
            AddModel(root, scenes, sources, $"{prefix}_{index:00}", file,
                center + Vector3.Right * offsets[index], new Vector3(0, yawDegrees, 0), 2.0f, counts);
        }
    }

    private static void AddCourtyardCollision(
        StaticBody3D collisionBody,
        string prefix,
        Vector3 center,
        bool entrySouth,
        BuildCounts counts)
    {
        var backZ = entrySouth ? -12.0f : 12.0f;
        var entryZ = -backZ;
        AddCollision(collisionBody, $"{prefix}BackWall", center + new Vector3(0, 3.0f, backZ),
            new Vector3(24.5f, 6.0f, 0.5f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}WestWall", center + new Vector3(-12.0f, 3.0f, 0),
            new Vector3(0.5f, 6.0f, 24.5f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}EastWall", center + new Vector3(12.0f, 3.0f, 0),
            new Vector3(0.5f, 6.0f, 24.5f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}EntryLeft", center + new Vector3(-8.0f, 3.0f, entryZ),
            new Vector3(8.0f, 6.0f, 0.5f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}EntryRight", center + new Vector3(8.0f, 3.0f, entryZ),
            new Vector3(8.0f, 6.0f, 0.5f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}EntryLintel", center + new Vector3(0, 5.2f, entryZ),
            new Vector3(8.0f, 1.6f, 0.5f), Vector3.Zero, counts);
    }

    private static void AddCourtyardPlanters(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        Vector3 center,
        string prefix,
        BuildCounts counts)
    {
        var offsets = new[]
        {
            new Vector3(-7.5f, 0.0f, -7.5f), new Vector3(7.5f, 0.0f, -7.5f),
            new Vector3(-7.5f, 0.0f, 7.5f), new Vector3(7.5f, 0.0f, 7.5f)
        };
        for (var index = 0; index < offsets.Length; index++)
        {
            AddModel(root, scenes, sources, $"{prefix}Planter_{index:00}", "Prop_Planter_Single.gltf",
                center + offsets[index], new Vector3(0, index * 90.0f, 0), 1.0f, counts);
        }
    }

    private static void AddDistrictLabels(Node3D root)
    {
        AddLabel(root, "GrandHotelLabel", HotelCenter + new Vector3(0, 7.2f, 12.4f), "GRAND HOTEL  //  HIGH VALUE");
        AddLabel(root, "TreasuryLabel", TreasuryCenter + new Vector3(0, 7.2f, -12.4f), "MUNICIPAL TREASURY  //  HIGH VALUE");
        AddLabel(root, "RooftopLabel", new Vector3(0, 6.9f, RooftopZ), "MARKET ROOFTOP");
    }

    private static void AddLabel(Node3D root, string name, Vector3 position, string text)
    {
        root.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Text = text,
            FontSize = 38,
            Modulate = new Color(0.92f, 0.78f, 0.42f),
            OutlineSize = 8,
            VisibilityRangeEnd = 145.0f
        });
    }

    private static void AddModel(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        string name,
        string file,
        Vector3 position,
        Vector3 rotationDegrees,
        float scale,
        BuildCounts counts)
    {
        var path = $"{ModelRoot}/{file}";
        if (!scenes.TryGetValue(path, out var scene))
        {
            scene = GD.Load<PackedScene>(path);
            if (scene is null)
            {
                return;
            }
            scenes[path] = scene;
        }
        if (scene.Instantiate() is not Node3D model)
        {
            return;
        }
        model.Name = name;
        model.Position = position;
        model.RotationDegrees = rotationDegrees;
        model.Scale = Vector3.One * scale;
        ConfigureImportedModel(model, 260.0f, castShadow: true);
        root.AddChild(model);
        sources.Add(path);
        counts.AuthoredModels++;
    }

    internal static void ConfigureImportedModel(
        Node node,
        float visibilityRange,
        bool castShadow)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = castShadow
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off;
            visual.VisibilityRangeEnd = visibilityRange;
            visual.VisibilityRangeEndMargin = 18.0f;
        }
        if (node is MeshInstance3D mesh)
        {
            for (var surface = 0; surface < mesh.GetSurfaceOverrideMaterialCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is BaseMaterial3D material)
                {
                    material.VertexColorUseAsAlbedo = false;
                    material.NormalEnabled = false;
                    material.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
                }
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureImportedModel(childNode, visibilityRange, castShadow);
            }
        }
    }

    private static void AddCollision(
        StaticBody3D body,
        string name,
        Vector3 position,
        Vector3 size,
        Vector3 rotationDegrees,
        BuildCounts counts)
    {
        body.AddChild(new CollisionShape3D
        {
            Name = name,
            Position = position,
            RotationDegrees = rotationDegrees,
            Shape = new BoxShape3D { Size = size }
        });
        counts.CollisionShapes++;
    }

    private sealed class BuildCounts
    {
        public int AuthoredModels;
        public int CollisionShapes;
    }
}
