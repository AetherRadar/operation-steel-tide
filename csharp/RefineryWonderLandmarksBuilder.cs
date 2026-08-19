using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record RefineryWonderLandmarksResult(
    int LandmarkCount,
    int AuthoredModelCount,
    int CollisionShapeCount,
    int ElevatedBridgeModuleCount,
    int EnterableLandmarkCount,
    Vector3 GateCenter,
    Vector3 WestEntry,
    Vector3 WestInterior,
    Vector3 EastEntry,
    Vector3 EastInterior,
    IReadOnlyCollection<string> ScenePaths);

/// <summary>Builds distinctive authored landmarks around the refinery's central approach.</summary>
internal sealed class RefineryWonderLandmarksBuilder
{
    private const string FactoryRoot = "res://assets/models/kenney_factory_kit";
    private const float GateZ = -67.0f;
    private static readonly Vector3 WestCenter = new(-78.0f, 0, -39.0f);
    private static readonly Vector3 EastCenter = new(78.0f, 0, -41.0f);

    public RefineryWonderLandmarksResult Build(Node3D parent)
    {
        var root = new Node3D { Name = "RefineryWonderLandmarks" };
        root.AddToGroup("refinery_wonder_landmarks");
        parent.AddChild(root);

        var collisionBody = new StaticBody3D
        {
            Name = "RefineryWonderCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(collisionBody);

        var scenes = new Dictionary<string, PackedScene>();
        var sources = new HashSet<string>();
        var counts = new BuildCounts();
        BuildSkybridgeGate(root, collisionBody, scenes, sources, counts);
        BuildCycloneSanctum(root, collisionBody, scenes, sources, counts);
        BuildReactorCrown(root, collisionBody, scenes, sources, counts);
        BuildLandmarkLabels(root);

        return new RefineryWonderLandmarksResult(
            3,
            counts.AuthoredModels,
            counts.CollisionShapes,
            counts.ElevatedBridgeModules,
            2,
            new Vector3(0, 10.0f, GateZ),
            WestCenter + new Vector3(0, 1.0f, 10.5f),
            WestCenter + new Vector3(0, 1.0f, 4.0f),
            EastCenter + new Vector3(0, 1.0f, 10.5f),
            EastCenter + new Vector3(0, 1.0f, 4.0f),
            sources);
    }

    private static void BuildSkybridgeGate(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        foreach (var side in new[] { -1.0f, 1.0f })
        {
            var centerX = side * 43.0f;
            var prefix = side < 0 ? "West" : "East";
            AddModel(root, scenes, sources, $"Gate{prefix}SouthCorner", "structure-corner-outer.glb",
                new Vector3(centerX - side * 4.0f, 0, GateZ + 4.0f), new Vector3(0, side < 0 ? 0 : -90, 0), 4.0f, counts);
            AddModel(root, scenes, sources, $"Gate{prefix}NorthCorner", "structure-corner-outer.glb",
                new Vector3(centerX + side * 4.0f, 0, GateZ - 4.0f), new Vector3(0, side < 0 ? 180 : 90, 0), 4.0f, counts);
            AddModel(root, scenes, sources, $"Gate{prefix}Window", "structure-window-wide.glb",
                new Vector3(centerX, 0, GateZ - 4.0f), new Vector3(0, 180, 0), 4.0f, counts);
            AddModel(root, scenes, sources, $"Gate{prefix}Door", "structure-doorway-wide.glb",
                new Vector3(centerX, 0, GateZ + 4.0f), Vector3.Zero, 4.0f, counts);
            AddModel(root, scenes, sources, $"Gate{prefix}Roof", "top-large.glb",
                new Vector3(centerX, 15.5f, GateZ), Vector3.Zero, 4.0f, counts);

            AddCollision(collisionBody, $"Gate{prefix}OuterWall",
                new Vector3(centerX + side * 4.5f, 7.75f, GateZ), new Vector3(1.0f, 15.5f, 10.0f), Vector3.Zero, counts);
            AddCollision(collisionBody, $"Gate{prefix}InnerWall",
                new Vector3(centerX - side * 4.5f, 7.75f, GateZ), new Vector3(1.0f, 15.5f, 10.0f), Vector3.Zero, counts);
            AddCollision(collisionBody, $"Gate{prefix}NorthWall",
                new Vector3(centerX, 7.75f, GateZ - 4.5f), new Vector3(8.0f, 15.5f, 1.0f), Vector3.Zero, counts);
            AddCollision(collisionBody, $"Gate{prefix}SouthWallLeft",
                new Vector3(centerX - 3.0f, 7.75f, GateZ + 4.5f), new Vector3(2.0f, 15.5f, 1.0f), Vector3.Zero, counts);
            AddCollision(collisionBody, $"Gate{prefix}SouthWallRight",
                new Vector3(centerX + 3.0f, 7.75f, GateZ + 4.5f), new Vector3(2.0f, 15.5f, 1.0f), Vector3.Zero, counts);
        }

        var bridgeIndex = 0;
        for (var x = -32.0f; x <= 32.0f; x += 4.0f)
        {
            AddModel(root, scenes, sources, $"SkybridgeDeck_{bridgeIndex:00}", "catwalk-straight.glb",
                new Vector3(x, 10.0f, GateZ), new Vector3(0, 90, 0), 4.0f, counts);
            counts.ElevatedBridgeModules++;
            bridgeIndex++;
        }
        AddModel(root, scenes, sources, "SkybridgeCrane", "crane.glb",
            new Vector3(0, 11.0f, GateZ), Vector3.Zero, 5.2f, counts);
        AddModel(root, scenes, sources, "SkybridgeCraneLift", "crane-lift.glb",
            new Vector3(0, 11.0f, GateZ), Vector3.Zero, 5.2f, counts);
        AddCollision(collisionBody, "SkybridgeDeckCollision",
            new Vector3(0, 10.0f, GateZ), new Vector3(68.0f, 0.35f, 3.4f), Vector3.Zero, counts);
    }

    private static void BuildCycloneSanctum(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        BuildOpenShell(root, scenes, sources, "Cyclone", WestCenter, counts);
        for (var level = 0; level < 3; level++)
        {
            AddModel(root, scenes, sources, $"CycloneCore_{level:00}", "hopper-high-round.glb",
                WestCenter + new Vector3(0, level * 5.2f, 0), new Vector3(0, level * 45.0f, 0), 4.4f, counts);
        }
        AddSquareCatwalk(root, scenes, sources, "CycloneRing", WestCenter + Vector3.Up * 6.0f, counts);
        foreach (var offset in new[]
                 {
                     new Vector3(-7, 8.0f, -7), new Vector3(7, 8.0f, -7),
                     new Vector3(-7, 8.0f, 7), new Vector3(7, 8.0f, 7)
                 })
        {
            AddModel(root, scenes, sources, $"CyclonePipe_{counts.AuthoredModels:00}", "pipe-large-bend.glb",
                WestCenter + offset, new Vector3(0, offset.X < 0 ? 90 : -90, 0), 3.8f, counts);
        }
        AddOpenShellCollision(collisionBody, "Cyclone", WestCenter, counts);
        AddCollision(collisionBody, "CycloneCoreCollision", WestCenter + Vector3.Up * 8.0f,
            new Vector3(4.5f, 16.0f, 4.5f), Vector3.Zero, counts);
    }

    private static void BuildReactorCrown(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        BuildOpenShell(root, scenes, sources, "Reactor", EastCenter, counts);
        var columns = new[]
        {
            (new Vector3(-6, 0, 0), 0.0f), (new Vector3(-3, 2.2f, 0), 35.0f),
            (new Vector3(0, 4.4f, 0), 70.0f), (new Vector3(3, 2.2f, 0), 105.0f),
            (new Vector3(6, 0, 0), 140.0f)
        };
        for (var index = 0; index < columns.Length; index++)
        {
            AddModel(root, scenes, sources, $"ReactorColumn_{index:00}", "hopper-high-round.glb",
                EastCenter + columns[index].Item1, new Vector3(0, columns[index].Item2, 0), 3.6f, counts);
        }
        AddSquareCatwalk(root, scenes, sources, "ReactorCrown", EastCenter + Vector3.Up * 8.0f, counts);
        AddModel(root, scenes, sources, "ReactorCrownCrane", "crane.glb",
            EastCenter + new Vector3(0, 10.5f, 0), new Vector3(0, 90, 0), 4.2f, counts);
        AddOpenShellCollision(collisionBody, "Reactor", EastCenter, counts);
        for (var index = 0; index < columns.Length; index++)
        {
            AddCollision(collisionBody, $"ReactorColumnCollision_{index:00}",
                EastCenter + columns[index].Item1 + Vector3.Up * 5.0f,
                new Vector3(3.2f, 10.0f, 3.2f), Vector3.Zero, counts);
        }
    }

    private static void BuildOpenShell(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        string prefix,
        Vector3 center,
        BuildCounts counts)
    {
        AddModel(root, scenes, sources, $"{prefix}NorthWall", "structure-window-wide.glb",
            center + new Vector3(0, 0, -8), new Vector3(0, 180, 0), 6.0f, counts);
        AddModel(root, scenes, sources, $"{prefix}WestWall", "structure-tall.glb",
            center + new Vector3(-8, 0, 0), new Vector3(0, 90, 0), 6.0f, counts);
        AddModel(root, scenes, sources, $"{prefix}EastWall", "structure-tall.glb",
            center + new Vector3(8, 0, 0), new Vector3(0, -90, 0), 6.0f, counts);
        AddModel(root, scenes, sources, $"{prefix}SouthDoor", "structure-doorway-wide.glb",
            center + new Vector3(0, 0, 8), Vector3.Zero, 6.0f, counts);
        foreach (var tierY in new[] { 6.0f, 12.0f })
        {
            AddModel(root, scenes, sources, $"{prefix}NorthUpper_{tierY:00}", "structure-window-wide.glb",
                center + new Vector3(0, tierY, -8), new Vector3(0, 180, 0), 6.0f, counts);
            AddModel(root, scenes, sources, $"{prefix}WestUpper_{tierY:00}", "structure-tall.glb",
                center + new Vector3(-8, tierY, 0), new Vector3(0, 90, 0), 6.0f, counts);
            AddModel(root, scenes, sources, $"{prefix}EastUpper_{tierY:00}", "structure-tall.glb",
                center + new Vector3(8, tierY, 0), new Vector3(0, -90, 0), 6.0f, counts);
        }
        AddModel(root, scenes, sources, $"{prefix}Roof", "top-large.glb",
            center + Vector3.Up * 18.0f, Vector3.Zero, 8.0f, counts);
    }

    private static void AddSquareCatwalk(
        Node3D root,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        string prefix,
        Vector3 center,
        BuildCounts counts)
    {
        var index = 0;
        foreach (var offset in new[]
                 {
                     new Vector3(-6, 0, -6), new Vector3(0, 0, -6), new Vector3(6, 0, -6),
                     new Vector3(-6, 0, 6), new Vector3(0, 0, 6), new Vector3(6, 0, 6)
                 })
        {
            AddModel(root, scenes, sources, $"{prefix}_{index:00}", "catwalk-straight.glb",
                center + offset, new Vector3(0, 90, 0), 4.0f, counts);
            index++;
        }
        foreach (var x in new[] { -6.0f, 6.0f })
        {
            AddModel(root, scenes, sources, $"{prefix}_{index:00}", "catwalk-straight.glb",
                center + new Vector3(x, 0, 0), Vector3.Zero, 4.0f, counts);
            index++;
        }
    }

    private static void AddOpenShellCollision(
        StaticBody3D collisionBody,
        string prefix,
        Vector3 center,
        BuildCounts counts)
    {
        AddCollision(collisionBody, $"{prefix}NorthCollision", center + new Vector3(0, 9.0f, -8),
            new Vector3(16.0f, 18.0f, 1.0f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}WestCollision", center + new Vector3(-8, 9.0f, 0),
            new Vector3(1.0f, 18.0f, 16.0f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}EastCollision", center + new Vector3(8, 9.0f, 0),
            new Vector3(1.0f, 18.0f, 16.0f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}SouthLeftCollision", center + new Vector3(-6, 9.0f, 8),
            new Vector3(4.0f, 18.0f, 1.0f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}SouthRightCollision", center + new Vector3(6, 9.0f, 8),
            new Vector3(4.0f, 18.0f, 1.0f), Vector3.Zero, counts);
        AddCollision(collisionBody, $"{prefix}RoofCollision", center + Vector3.Up * 18.0f,
            new Vector3(16.0f, 0.5f, 16.0f), Vector3.Zero, counts);
    }

    private static void BuildLandmarkLabels(Node3D root)
    {
        AddLabel(root, "SkybridgeGateLabel", new Vector3(0, 16.8f, GateZ + 1.8f), "SKYBRIDGE GATE");
        AddLabel(root, "CycloneSanctumLabel", WestCenter + new Vector3(0, 19.6f, 8.6f), "CYCLONE SANCTUM");
        AddLabel(root, "ReactorCrownLabel", EastCenter + new Vector3(0, 19.6f, 8.6f), "REACTOR CROWN");
    }

    private static void AddLabel(Node3D root, string name, Vector3 position, string label)
    {
        root.AddChild(new Label3D
        {
            Name = name,
            Position = position,
            Text = label,
            FontSize = 38,
            Modulate = new Color(1.0f, 0.64f, 0.18f),
            OutlineSize = 7,
            VisibilityRangeEnd = 190.0f
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
        var path = $"{FactoryRoot}/{file}";
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
        model.AddToGroup("refinery_wonder_authored");
        ConfigureVisuals(model);
        root.AddChild(model);
        sources.Add(path);
        counts.AuthoredModels++;
    }

    private static void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = 360.0f;
            visual.VisibilityRangeEndMargin = 24.0f;
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
        public int ElevatedBridgeModules;
    }
}
