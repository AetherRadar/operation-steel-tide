using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record RefineryFactoryDistrictResult(
    int AuthoredModelCount,
    int CollisionShapeCount,
    int EntryCount,
    int RoofModuleCount,
    int CatwalkModuleCount,
    int InteriorPropCount,
    int AlleyPropCount,
    Vector3 SouthEntry,
    Vector3 NorthEntry,
    Vector3 InteriorCenter,
    IReadOnlyCollection<string> ScenePaths);

/// <summary>Builds the authored modular cracking hall and its dense approach alleys.</summary>
internal sealed class RefineryFactoryDistrictBuilder
{
    private const string ModelRoot = "res://assets/models/kenney_factory_kit";
    private const float HallWest = -38.0f;
    private const float HallEast = 38.0f;
    private const float HallSouth = -75.0f;
    private const float HallNorth = -163.0f;
    private const float HallCenterZ = -119.0f;
    private const float HallRoofY = 15.5f;

    public RefineryFactoryDistrictResult Build(Node3D parent)
    {
        var root = new Node3D { Name = "CrackingFactoryDistrict" };
        root.AddToGroup("refinery_factory_district");
        parent.AddChild(root);

        var collisionBody = new StaticBody3D
        {
            Name = "CrackingFactoryCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        root.AddChild(collisionBody);

        var scenes = new Dictionary<string, PackedScene>();
        var sources = new HashSet<string>();
        var counts = new BuildCounts();
        BuildHallShell(root, collisionBody, scenes, sources, counts);
        BuildHallInterior(root, collisionBody, scenes, sources, counts);
        BuildApproachAlleys(root, collisionBody, scenes, sources, counts);
        BuildHallLighting(root);

        return new RefineryFactoryDistrictResult(
            counts.AuthoredModels,
            counts.CollisionShapes,
            2,
            counts.RoofModules,
            counts.CatwalkModules,
            counts.InteriorProps,
            counts.AlleyProps,
            new Vector3(0, 0.1f, HallSouth + 2.0f),
            new Vector3(0, 0.1f, HallNorth - 2.0f),
            new Vector3(0, 1.2f, HallCenterZ),
            sources);
    }

    private static void BuildHallShell(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        var sideIndex = 0;
        for (var z = HallNorth + 4.0f; z <= HallSouth - 4.0f; z += 4.0f)
        {
            var file = sideIndex % 4 == 1 ? "structure-window-wide.glb" : "structure-tall.glb";
            AddModel(root, scenes, sources, $"HallWestWall_{sideIndex:00}", file,
                new Vector3(HallWest, 0, z), new Vector3(0, 90, 0), 4.0f, counts);
            AddModel(root, scenes, sources, $"HallEastWall_{sideIndex:00}", file,
                new Vector3(HallEast, 0, z), new Vector3(0, -90, 0), 4.0f, counts);
            sideIndex++;
        }

        var endIndex = 0;
        for (var x = HallWest + 2.0f; x <= HallEast - 2.0f; x += 4.0f)
        {
            if (Mathf.Abs(x) < 10.0f)
            {
                continue;
            }
            var file = endIndex % 3 == 1 ? "structure-high.glb" : "structure-tall.glb";
            AddModel(root, scenes, sources, $"HallSouthWall_{endIndex:00}", file,
                new Vector3(x, 0, HallSouth), Vector3.Zero, 4.0f, counts);
            AddModel(root, scenes, sources, $"HallNorthWall_{endIndex:00}", file,
                new Vector3(x, 0, HallNorth), new Vector3(0, 180, 0), 4.0f, counts);
            endIndex++;
        }

        AddModel(root, scenes, sources, "HallSouthDoorFrame", "structure-doorway-wide.glb",
            new Vector3(0, 0, HallSouth), Vector3.Zero, 10.0f, counts);
        AddModel(root, scenes, sources, "HallSouthDoorOpen", "door-wide-open.glb",
            new Vector3(0, 0, HallSouth - 0.1f), Vector3.Zero, 10.0f, counts);
        AddModel(root, scenes, sources, "HallNorthDoorFrame", "structure-doorway-wide.glb",
            new Vector3(0, 0, HallNorth), new Vector3(0, 180, 0), 10.0f, counts);
        AddModel(root, scenes, sources, "HallNorthDoorOpen", "door-wide-open.glb",
            new Vector3(0, 0, HallNorth + 0.1f), new Vector3(0, 180, 0), 10.0f, counts);

        AddModel(root, scenes, sources, "HallCornerSouthWest", "structure-corner-outer.glb",
            new Vector3(HallWest, 0, HallSouth), Vector3.Zero, 4.0f, counts);
        AddModel(root, scenes, sources, "HallCornerSouthEast", "structure-corner-outer.glb",
            new Vector3(HallEast, 0, HallSouth), new Vector3(0, -90, 0), 4.0f, counts);
        AddModel(root, scenes, sources, "HallCornerNorthWest", "structure-corner-outer.glb",
            new Vector3(HallWest, 0, HallNorth), new Vector3(0, 90, 0), 4.0f, counts);
        AddModel(root, scenes, sources, "HallCornerNorthEast", "structure-corner-outer.glb",
            new Vector3(HallEast, 0, HallNorth), new Vector3(0, 180, 0), 4.0f, counts);

        var roofIndex = 0;
        for (var x = -32.0f; x <= 32.0f; x += 16.0f)
        {
            for (var z = -151.0f; z <= -87.0f; z += 16.0f)
            {
                AddModel(root, scenes, sources, $"HallRoof_{roofIndex:00}", "top-large.glb",
                    new Vector3(x, HallRoofY, z), Vector3.Zero, 8.0f, counts);
                counts.RoofModules++;
                roofIndex++;
            }
        }

        AddCollision(collisionBody, "HallWestCollision",
            new Vector3(HallWest, HallRoofY * 0.5f, HallCenterZ), new Vector3(1.0f, HallRoofY, 88.0f), Vector3.Zero, counts);
        AddCollision(collisionBody, "HallEastCollision",
            new Vector3(HallEast, HallRoofY * 0.5f, HallCenterZ), new Vector3(1.0f, HallRoofY, 88.0f), Vector3.Zero, counts);
        foreach (var x in new[] { -24.0f, 24.0f })
        {
            AddCollision(collisionBody, $"HallSouthCollision_{x:0}",
                new Vector3(x, HallRoofY * 0.5f, HallSouth), new Vector3(28.0f, HallRoofY, 1.0f), Vector3.Zero, counts);
            AddCollision(collisionBody, $"HallNorthCollision_{x:0}",
                new Vector3(x, HallRoofY * 0.5f, HallNorth), new Vector3(28.0f, HallRoofY, 1.0f), Vector3.Zero, counts);
        }
        AddCollision(collisionBody, "HallRoofCollision",
            new Vector3(0, HallRoofY, HallCenterZ), new Vector3(76.0f, 0.8f, 88.0f), Vector3.Zero, counts);
    }

    private static void BuildHallInterior(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        var catwalkIndex = 0;
        foreach (var x in new[] { -24.0f, 24.0f })
        {
            for (var z = -147.0f; z <= -91.0f; z += 4.0f)
            {
                AddModel(root, scenes, sources, $"HallCatwalk_{catwalkIndex:00}", "catwalk-straight.glb",
                    new Vector3(x, 6.0f, z), Vector3.Zero, 4.0f, counts);
                counts.CatwalkModules++;
                catwalkIndex++;
            }
            AddModel(root, scenes, sources, $"HallCatwalkStairs_{x:0}", "catwalk-stairs.glb",
                new Vector3(x, 0, -82.0f), Vector3.Zero, 4.0f, counts);
            counts.CatwalkModules++;
            AddCollision(collisionBody, $"HallCatwalkCollision_{x:0}",
                new Vector3(x, 6.0f, -119.0f), new Vector3(3.4f, 0.3f, 64.0f), Vector3.Zero, counts);
            AddCollision(collisionBody, $"HallCatwalkRamp_{x:0}",
                new Vector3(x, 3.0f, -82.0f), new Vector3(3.2f, 0.35f, 13.2f), new Vector3(27, 0, 0), counts);
        }

        AddModel(root, scenes, sources, "HallOverheadCrane", "crane.glb",
            new Vector3(0, 8.2f, -121.0f), new Vector3(0, 90, 0), 6.5f, counts);
        AddModel(root, scenes, sources, "HallCraneLift", "crane-lift.glb",
            new Vector3(0, 8.2f, -121.0f), new Vector3(0, 90, 0), 6.5f, counts);
        counts.InteriorProps += 2;

        var machines = new[]
        {
            (new Vector3(-27, 0, -98), "machine-fortified.glb", 4.5f, 0.0f, new Vector3(6.5f, 4.8f, 5.5f)),
            (new Vector3(27, 0, -101), "machine-window.glb", 4.5f, 180.0f, new Vector3(6.5f, 4.8f, 5.5f)),
            (new Vector3(-27, 0, -119), "machine.glb", 5.0f, 90.0f, new Vector3(6.0f, 4.5f, 6.0f)),
            (new Vector3(27, 0, -122), "machine-fortified.glb", 4.8f, -90.0f, new Vector3(6.5f, 4.8f, 5.5f)),
            (new Vector3(-27, 0, -142), "machine-window.glb", 4.5f, 0.0f, new Vector3(6.5f, 4.8f, 5.5f)),
            (new Vector3(27, 0, -145), "machine.glb", 5.0f, 180.0f, new Vector3(6.0f, 4.5f, 6.0f))
        };
        for (var index = 0; index < machines.Length; index++)
        {
            var machine = machines[index];
            AddModel(root, scenes, sources, $"HallMachine_{index:00}", machine.Item2,
                machine.Item1, new Vector3(0, machine.Item4, 0), machine.Item3, counts);
            AddCollision(collisionBody, $"HallMachineCollision_{index:00}",
                machine.Item1 + Vector3.Up * (machine.Item5.Y * 0.5f), machine.Item5, Vector3.Zero, counts);
            counts.InteriorProps++;
        }

        var conveyorIndex = 0;
        foreach (var x in new[] { -17.0f, 17.0f })
        {
            foreach (var z in new[] { -104.0f, -120.0f, -136.0f })
            {
                AddModel(root, scenes, sources, $"HallConveyor_{conveyorIndex:00}", "conveyor-long-stripe-sides.glb",
                    new Vector3(x, 0, z), Vector3.Zero, 4.0f, counts);
                AddCollision(collisionBody, $"HallConveyorCollision_{conveyorIndex:00}",
                    new Vector3(x, 1.0f, z), new Vector3(3.2f, 2.0f, 7.0f), Vector3.Zero, counts);
                counts.InteriorProps++;
                conveyorIndex++;
            }
        }

        foreach (var x in new[] { -33.0f, 33.0f })
        {
            AddModel(root, scenes, sources, $"HallPipeLong_{x:0}", "pipe-large-long.glb",
                new Vector3(x, 4.0f, -112.0f), Vector3.Zero, 4.0f, counts);
            AddModel(root, scenes, sources, $"HallPipeBend_{x:0}", "pipe-large-bend.glb",
                new Vector3(x, 4.0f, -132.0f), Vector3.Zero, 4.0f, counts);
            AddModel(root, scenes, sources, $"HallHopper_{x:0}", "hopper-high-round.glb",
                new Vector3(x, 0, -151.0f), Vector3.Zero, 4.5f, counts);
            AddCollision(collisionBody, $"HallHopperCollision_{x:0}",
                new Vector3(x, 2.2f, -151.0f), new Vector3(4.0f, 4.4f, 4.0f), Vector3.Zero, counts);
            counts.InteriorProps += 3;
        }

        var centerIslands = new[]
        {
            (new Vector3(0, 0, -101), "hopper-high-round.glb", 3.8f, new Vector3(4.0f, 3.8f, 4.0f)),
            (new Vector3(0, 0, -120), "machine.glb", 3.6f, new Vector3(4.2f, 3.4f, 4.2f)),
            (new Vector3(0, 0, -139), "hopper-high-round.glb", 3.8f, new Vector3(4.0f, 3.8f, 4.0f))
        };
        for (var index = 0; index < centerIslands.Length; index++)
        {
            var island = centerIslands[index];
            AddModel(root, scenes, sources, $"HallCenterIsland_{index:00}", island.Item2,
                island.Item1, Vector3.Zero, island.Item3, counts);
            AddCollision(collisionBody, $"HallCenterIslandCollision_{index:00}",
                island.Item1 + Vector3.Up * (island.Item4.Y * 0.5f), island.Item4, Vector3.Zero, counts);
            counts.InteriorProps++;
        }
    }

    private static void BuildApproachAlleys(
        Node3D root,
        StaticBody3D collisionBody,
        Dictionary<string, PackedScene> scenes,
        HashSet<string> sources,
        BuildCounts counts)
    {
        var placements = new[]
        {
            (new Vector3(-17, 0, 48), "machine.glb", 3.8f, 0.0f, new Vector3(5.0f, 3.8f, 5.0f)),
            (new Vector3(18, 0, 45), "machine-window.glb", 3.8f, 180.0f, new Vector3(5.0f, 3.8f, 5.0f)),
            (new Vector3(-29, 0, 31), "conveyor-long-stripe-sides.glb", 4.0f, 90.0f, new Vector3(7.0f, 2.0f, 3.2f)),
            (new Vector3(29, 0, 28), "conveyor-long-stripe-sides.glb", 4.0f, -90.0f, new Vector3(7.0f, 2.0f, 3.2f)),
            (new Vector3(-17, 0, 12), "machine-fortified.glb", 4.0f, 90.0f, new Vector3(5.5f, 4.2f, 5.0f)),
            (new Vector3(17, 0, 8), "machine.glb", 4.0f, -90.0f, new Vector3(5.0f, 4.0f, 5.0f)),
            (new Vector3(-29, 0, -8), "pipe-large-long.glb", 4.0f, 0.0f, new Vector3(3.0f, 3.0f, 7.0f)),
            (new Vector3(29, 0, -12), "pipe-large-bend.glb", 4.0f, 180.0f, new Vector3(4.5f, 4.0f, 4.5f)),
            (new Vector3(-17, 0, -29), "conveyor-corner.glb", 4.0f, 0.0f, new Vector3(4.0f, 2.0f, 4.0f)),
            (new Vector3(18, 0, -33), "conveyor-corner.glb", 4.0f, 180.0f, new Vector3(4.0f, 2.0f, 4.0f)),
            (new Vector3(-29, 0, -51), "machine-window.glb", 4.0f, 0.0f, new Vector3(5.5f, 4.2f, 5.0f)),
            (new Vector3(29, 0, -55), "machine-fortified.glb", 4.0f, 180.0f, new Vector3(5.5f, 4.2f, 5.0f))
        };
        for (var index = 0; index < placements.Length; index++)
        {
            var placement = placements[index];
            AddModel(root, scenes, sources, $"ApproachAlleyProp_{index:00}", placement.Item2,
                placement.Item1, new Vector3(0, placement.Item4, 0), placement.Item3, counts);
            AddCollision(collisionBody, $"ApproachAlleyCollision_{index:00}",
                placement.Item1 + Vector3.Up * (placement.Item5.Y * 0.5f), placement.Item5, Vector3.Zero, counts);
            counts.AlleyProps++;
        }

        var laneFillers = new[]
        {
            (new Vector3(-13.5f, 0, 56), "conveyor-corner.glb", 3.5f, 90.0f, new Vector3(3.6f, 1.8f, 3.6f)),
            (new Vector3(13.5f, 0, 53), "conveyor-corner.glb", 3.5f, -90.0f, new Vector3(3.6f, 1.8f, 3.6f)),
            (new Vector3(-13.5f, 0, 39), "machine.glb", 3.4f, 0.0f, new Vector3(4.0f, 3.4f, 4.0f)),
            (new Vector3(13.5f, 0, 36), "machine-window.glb", 3.4f, 180.0f, new Vector3(4.0f, 3.4f, 4.0f)),
            (new Vector3(-13.5f, 0, 20), "pipe-large-bend.glb", 3.2f, 90.0f, new Vector3(3.8f, 3.4f, 3.8f)),
            (new Vector3(13.5f, 0, 17), "pipe-large-bend.glb", 3.2f, -90.0f, new Vector3(3.8f, 3.4f, 3.8f)),
            (new Vector3(-13.5f, 0, 1), "conveyor-long-stripe-sides.glb", 3.2f, 0.0f, new Vector3(2.8f, 1.8f, 5.8f)),
            (new Vector3(13.5f, 0, -2), "conveyor-long-stripe-sides.glb", 3.2f, 180.0f, new Vector3(2.8f, 1.8f, 5.8f)),
            (new Vector3(-13.5f, 0, -19), "machine-fortified.glb", 3.4f, 90.0f, new Vector3(4.2f, 3.6f, 4.0f)),
            (new Vector3(13.5f, 0, -22), "machine.glb", 3.4f, -90.0f, new Vector3(4.0f, 3.4f, 4.0f)),
            (new Vector3(-13.5f, 0, -40), "hopper-high-round.glb", 3.4f, 0.0f, new Vector3(3.6f, 3.6f, 3.6f)),
            (new Vector3(13.5f, 0, -43), "hopper-high-round.glb", 3.4f, 0.0f, new Vector3(3.6f, 3.6f, 3.6f)),
            (new Vector3(-13.5f, 0, -59), "pipe-large-long.glb", 3.2f, 0.0f, new Vector3(2.8f, 2.8f, 5.8f)),
            (new Vector3(13.5f, 0, -62), "pipe-large-long.glb", 3.2f, 0.0f, new Vector3(2.8f, 2.8f, 5.8f))
        };
        for (var index = 0; index < laneFillers.Length; index++)
        {
            var placement = laneFillers[index];
            AddModel(root, scenes, sources, $"ApproachLaneFiller_{index:00}", placement.Item2,
                placement.Item1, new Vector3(0, placement.Item4, 0), placement.Item3, counts);
            AddCollision(collisionBody, $"ApproachLaneFillerCollision_{index:00}",
                placement.Item1 + Vector3.Up * (placement.Item5.Y * 0.5f), placement.Item5, Vector3.Zero, counts);
            counts.AlleyProps++;
        }
    }

    private static void BuildHallLighting(Node3D root)
    {
        var positions = new[]
        {
            new Vector3(-20, 11.5f, -94), new Vector3(20, 11.5f, -94),
            new Vector3(-20, 11.5f, -119), new Vector3(20, 11.5f, -119),
            new Vector3(-20, 11.5f, -144), new Vector3(20, 11.5f, -144)
        };
        for (var index = 0; index < positions.Length; index++)
        {
            root.AddChild(new OmniLight3D
            {
                Name = $"HallLight_{index:00}",
                Position = positions[index],
                LightColor = new Color(1.0f, 0.7f, 0.36f),
                LightEnergy = 1.8f,
                OmniRange = 24.0f,
                ShadowEnabled = false
            });
        }
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
        model.AddToGroup("refinery_factory_authored");
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
        public int RoofModules;
        public int CatwalkModules;
        public int InteriorProps;
        public int AlleyProps;
    }
}
