using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record FreightIndustrialDoorway(
    InteractiveBuildingDoor Door,
    Vector3 OutsidePoint,
    Vector3 InsidePoint);

internal sealed record FreightIndustrialRoom(
    int Index,
    string Name,
    string ModelId,
    Node3D Root,
    Rect2 LocalBounds,
    Vector3 ContentLocalPoint,
    Vector3 GuardLocalPointA,
    Vector3 GuardLocalPointB,
    Vector3 FacingLocalPoint,
    IReadOnlyList<FreightIndustrialDoorway> Doorways)
{
    public Vector3 ContentWorldPoint => Root.ToGlobal(ContentLocalPoint);
    public Vector3 GuardWorldPointA => Root.ToGlobal(GuardLocalPointA);
    public Vector3 GuardWorldPointB => Root.ToGlobal(GuardLocalPointB);
    public Vector3 FacingWorldPoint => Root.ToGlobal(FacingLocalPoint);
}

internal sealed record FreightIndustrialInteriorBuildResult(
    IReadOnlyList<FreightIndustrialRoom> Rooms,
    IReadOnlyList<InteractiveBuildingDoor> Doors,
    IReadOnlyCollection<string> ScenePaths,
    int AuthoredBuildingCount,
    int LandmarkCount,
    int PalettedBuildingCount,
    int CollisionShapeCount,
    int AuthoredInteriorModelCount);

/// <summary>Builds authored enterable shells, their ballistic collision, doors, and room dressing.</summary>
internal sealed class FreightIndustrialInteriorBuilder
{
    private const string BunkPath =
        "res://assets/models/kenney_furniture_kit/bedBunk.glb";
    private const string DeskPath =
        "res://assets/models/kenney_furniture_kit/desk.glb";
    private const string SofaPath =
        "res://assets/models/kenney_furniture_kit/loungeSofa.glb";
    private const string CratePath =
        "res://assets/models/kenney_furniture_kit/cardboardBoxClosed.glb";

    private readonly FreightIndustrialPalette _palette;
    private readonly Dictionary<string, PackedScene> _scenes = new(StringComparer.Ordinal);

    public FreightIndustrialInteriorBuilder(FreightIndustrialPalette palette)
    {
        _palette = palette;
    }

    public FreightIndustrialInteriorBuildResult Build(Node3D parent, int firstDoorId)
    {
        var layouts = FreightIndustrialBuildingCatalog.LoadLayouts();
        var rooms = new List<FreightIndustrialRoom>(FreightIndustrialBuildingCatalog.Placements.Count);
        var doors = new List<InteractiveBuildingDoor>();
        var scenePaths = new HashSet<string>(StringComparer.Ordinal);
        var landmarkCount = 0;
        var palettedCount = 0;
        var collisionShapeCount = 0;
        var interiorModelCount = 0;

        var buildingsRoot = new Node3D { Name = "FreightEnterableIndustrialBuildings" };
        buildingsRoot.AddToGroup("freight_enterable_industrial_root");
        parent.AddChild(buildingsRoot);

        foreach (var placement in FreightIndustrialBuildingCatalog.Placements)
        {
            if (!layouts.TryGetValue(placement.ModelId, out var layout))
            {
                throw new InvalidOperationException(
                    $"No industrial layout exists for {placement.ModelId}.");
            }

            var scenePath = FreightIndustrialBuildingCatalog.ScenePath(placement.ModelId);
            var scene = LoadScene(scenePath);
            if (scene.Instantiate() is not Node3D visual)
            {
                throw new InvalidOperationException(
                    $"Industrial building could not instantiate: {scenePath}");
            }

            var building = new Node3D
            {
                Name = placement.Name,
                Position = placement.Position,
                Rotation = new Vector3(0, placement.Yaw, 0)
            };
            building.AddToGroup("freight_authored_model");
            building.AddToGroup("freight_terminal_accessible_building");
            building.AddToGroup("industrial_enterable_building");
            building.SetMeta("freight_scene_path", scenePath);
            building.SetMeta("industrial_model_id", placement.ModelId);
            buildingsRoot.AddChild(building);

            visual.Name = $"{placement.Name}AuthoredShell";
            visual.Scale = Vector3.One * placement.Scale;
            ConfigureVisuals(visual, 340.0f);
            building.AddChild(visual);
            if (_palette.Apply(visual, placement.Name) > 0)
            {
                palettedCount++;
            }
            scenePaths.Add(scenePath);

            var collisionBody = AddImportedBallisticCollision(building, visual);
            collisionShapeCount++;
            collisionShapeCount += AddInteriorFloorCollision(
                collisionBody,
                layout,
                placement.Scale);
            collisionBody.SetMeta("collision_shape_count", 2);
            interiorModelCount += AddAuthoredInterior(building, layout, placement.Scale, rooms.Count);

            var roomDoorways = AddDoors(
                building,
                layout,
                placement.Scale,
                firstDoorId + doors.Count,
                doors);
            var bounds = ScaledBounds(layout.Bounds, placement.Scale, inset: 0.46f);
            var center = layout.InteriorAnchor * placement.Scale;
            var firstPortal = layout.Portals[0];
            var facingPoint = new Vector3(
                firstPortal.Center.X * placement.Scale,
                1.1f,
                firstPortal.Center.Y * placement.Scale);
            var room = new FreightIndustrialRoom(
                rooms.Count,
                placement.Name,
                placement.ModelId,
                building,
                bounds,
                new Vector3(center.X, 0.12f, center.Y),
                new Vector3(center.X - 0.42f, 0.12f, center.Y + 0.30f),
                new Vector3(center.X + 0.44f, 0.12f, center.Y - 0.32f),
                facingPoint,
                roomDoorways);
            rooms.Add(room);
            building.SetMeta("industrial_room_index", room.Index);
            building.SetMeta("industrial_portal_count", roomDoorways.Count);
            if (placement.IsLandmark)
            {
                building.AddToGroup("freight_authored_landmark");
                landmarkCount++;
            }
        }

        return new FreightIndustrialInteriorBuildResult(
            rooms,
            doors,
            scenePaths,
            rooms.Count,
            landmarkCount,
            palettedCount,
            collisionShapeCount,
            interiorModelCount);
    }

    private IReadOnlyList<FreightIndustrialDoorway> AddDoors(
        Node3D building,
        FreightIndustrialBuildingLayout layout,
        float scale,
        int firstDoorId,
        List<InteractiveBuildingDoor> allDoors)
    {
        var doorways = new List<FreightIndustrialDoorway>(layout.Portals.Count);
        for (var index = 0; index < layout.Portals.Count; index++)
        {
            var portal = layout.Portals[index];
            var outward = new Vector3(portal.OutwardNormal.X, 0, portal.OutwardNormal.Y);
            var center = new Vector3(portal.Center.X * scale, 0, portal.Center.Y * scale);
            var door = new InteractiveBuildingDoor
            {
                Name = $"{building.Name}Door{index + 1:00}"
            };
            door.Configure(
                firstDoorId + index,
                portal.Width * scale,
                portal.Height * scale,
                frontZ: 0.0f,
                visibilityRange: 300.0f,
                motionStyle: portal.MotionStyle,
                mountPosition: center,
                mountYaw: Mathf.Atan2(outward.X, outward.Z));
            door.AddToGroup("industrial_room_door");
            building.AddChild(door);
            allDoors.Add(door);

            var outside = building.ToGlobal(center + outward * 1.15f + Vector3.Up * 0.12f);
            var inside = building.ToGlobal(center - outward * 1.15f + Vector3.Up * 0.12f);
            doorways.Add(new FreightIndustrialDoorway(door, outside, inside));
        }
        return doorways;
    }

    private static StaticBody3D AddImportedBallisticCollision(Node3D building, Node3D visual)
    {
        var faces = new List<Vector3>();
        CollectMeshFaces(visual, Transform3D.Identity, faces);
        if (faces.Count == 0 || faces.Count % 3 != 0)
        {
            throw new InvalidOperationException(
                $"Industrial building has invalid collision geometry: {building.Name}");
        }

        var shape = new ConcavePolygonShape3D
        {
            BackfaceCollision = true
        };
        shape.SetFaces(faces.ToArray());
        var body = new StaticBody3D
        {
            Name = "AuthoredBallisticShell",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup("industrial_bulletproof_shell");
        body.AddChild(new CollisionShape3D
        {
            Name = "ImportedShellShape",
            Shape = shape
        });
        building.AddChild(body);
        body.SetMeta("collision_triangle_count", faces.Count / 3);
        return body;
    }

    private static void CollectMeshFaces(
        Node node,
        Transform3D parentTransform,
        List<Vector3> combinedFaces)
    {
        var transform = node is Node3D spatial
            ? parentTransform * spatial.Transform
            : parentTransform;
        if (node is MeshInstance3D { Mesh: not null } meshInstance)
        {
            foreach (var vertex in meshInstance.Mesh.GetFaces())
            {
                combinedFaces.Add(transform * vertex);
            }
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CollectMeshFaces(childNode, transform, combinedFaces);
            }
        }
    }

    private static int AddInteriorFloorCollision(
        StaticBody3D body,
        FreightIndustrialBuildingLayout layout,
        float scale)
    {
        var bounds = ScaledBounds(layout.Bounds, scale, inset: 0.34f);
        var center = bounds.GetCenter();
        body.AddToGroup("industrial_bulletproof_floor");
        body.AddChild(new CollisionShape3D
        {
            Name = "IndustrialInteriorFloorShape",
            Position = new Vector3(center.X, -0.055f, center.Y),
            Shape = new BoxShape3D
            {
                Size = new Vector3(bounds.Size.X, 0.12f, bounds.Size.Y)
            }
        });
        return 1;
    }

    private int AddAuthoredInterior(
        Node3D building,
        FreightIndustrialBuildingLayout layout,
        float scale,
        int roomIndex)
    {
        var bounds = ScaledBounds(layout.Bounds, scale, inset: 0.55f);
        var center = layout.InteriorAnchor * scale;
        var count = 0;
        var offsetX = Mathf.Min(0.85f, bounds.Size.X * 0.08f);
        var offsetZ = Mathf.Min(0.78f, bounds.Size.Y * 0.08f);
        var backZ = center.Y - offsetZ;
        var frontZ = center.Y + offsetZ;
        var leftX = center.X - offsetX;
        var rightX = center.X + offsetX;
        if (roomIndex % 3 == 0)
        {
            count += AddInteriorModel(building, BunkPath, "RestBunk", new Vector3(leftX, 0.03f, backZ), Mathf.Pi * 0.5f, Vector3.One * 1.05f);
            count += AddInteriorModel(building, DeskPath, "DutyDesk", new Vector3(rightX, 0.03f, frontZ), Mathf.Pi, Vector3.One * 1.05f);
        }
        else if (roomIndex % 3 == 1)
        {
            count += AddInteriorModel(building, SofaPath, "RestSofa", new Vector3(leftX, 0.03f, backZ), Mathf.Pi * 0.5f, Vector3.One * 1.05f);
            count += AddInteriorModel(building, DeskPath, "LogisticsDesk", new Vector3(rightX, 0.03f, frontZ), Mathf.Pi, Vector3.One);
        }
        else
        {
            count += AddInteriorModel(building, DeskPath, "SecurityDesk", new Vector3(leftX, 0.03f, backZ), 0.0f, Vector3.One * 1.05f);
            count += AddInteriorModel(building, CratePath, "StoredCartons", new Vector3(rightX, 0.03f, frontZ), roomIndex * 0.37f, Vector3.One * 1.2f);
        }

        building.AddChild(new OmniLight3D
        {
            Name = "IndustrialInteriorLight",
            Position = new Vector3(center.X, 2.55f, center.Y),
            LightColor = new Color(1.0f, 0.78f, 0.54f),
            LightEnergy = 1.15f,
            OmniRange = Mathf.Min(9.5f, Mathf.Max(bounds.Size.X, bounds.Size.Y) * 0.78f),
            ShadowEnabled = false
        });
        return count;
    }

    private int AddInteriorModel(
        Node3D parent,
        string scenePath,
        string name,
        Vector3 position,
        float yaw,
        Vector3 scale)
    {
        if (LoadScene(scenePath).Instantiate() is not Node3D model)
        {
            return 0;
        }
        model.Name = name;
        model.Position = position;
        model.Rotation = new Vector3(0, yaw, 0);
        model.Scale = scale;
        model.AddToGroup("industrial_authored_interior_model");
        model.SetMeta("industrial_scene_path", scenePath);
        ApplyIndustrialInteriorFinish(model, scenePath);
        ConfigureVisuals(model, 150.0f);
        parent.AddChild(model);
        return 1;
    }

    private static void ApplyIndustrialInteriorFinish(Node node, string scenePath)
    {
        if (node is MeshInstance3D { Mesh: not null } meshInstance)
        {
            var tint = scenePath switch
            {
                BunkPath => new Color(0.32f, 0.37f, 0.27f),
                SofaPath => new Color(0.27f, 0.34f, 0.25f),
                DeskPath => new Color(0.34f, 0.28f, 0.20f),
                _ => new Color(0.48f, 0.39f, 0.27f)
            };
            for (var surface = 0; surface < meshInstance.Mesh.GetSurfaceCount(); surface++)
            {
                if (meshInstance.Mesh.SurfaceGetMaterial(surface) is not BaseMaterial3D source
                    || source.Duplicate(true) is not BaseMaterial3D finish)
                {
                    continue;
                }
                finish.AlbedoColor = new Color(
                    source.AlbedoColor.R * tint.R,
                    source.AlbedoColor.G * tint.G,
                    source.AlbedoColor.B * tint.B,
                    source.AlbedoColor.A);
                finish.Metallic = scenePath == SofaPath ? 0.0f : Mathf.Max(source.Metallic, 0.08f);
                finish.Roughness = Mathf.Max(source.Roughness, 0.76f);
                meshInstance.SetSurfaceOverrideMaterial(surface, finish);
            }
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ApplyIndustrialInteriorFinish(childNode, scenePath);
            }
        }
    }

    private PackedScene LoadScene(string path)
    {
        if (_scenes.TryGetValue(path, out var scene))
        {
            return scene;
        }
        scene = GD.Load<PackedScene>(path)
            ?? throw new InvalidOperationException($"Authored industrial asset is missing: {path}");
        _scenes[path] = scene;
        return scene;
    }

    private static Rect2 ScaledBounds(Rect2 bounds, float scale, float inset)
    {
        var scaled = new Rect2(bounds.Position * scale, bounds.Size * scale);
        var safeInset = Mathf.Min(inset, Mathf.Min(scaled.Size.X, scaled.Size.Y) * 0.12f);
        return scaled.Grow(-safeInset);
    }

    private static void ConfigureVisuals(Node node, float visibilityRange)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = visibilityRange;
            visual.VisibilityRangeEndMargin = Mathf.Min(22.0f, visibilityRange * 0.12f);
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureVisuals(childNode, visibilityRange);
            }
        }
    }
}
