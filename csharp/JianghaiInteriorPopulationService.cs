using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Populates the twelve DCC-authored Old City shops without adding visible primitives.
/// The source mesh supplies orientation and doorway metadata; all furnishing visuals
/// come from the existing redistributable Kenney Furniture Kit and static props are
/// folded into short-range spatial render batches.
/// </summary>
internal sealed class JianghaiInteriorPopulationService
{
    public const int ExpectedRoomCount = 12;
    public const int ExpectedDoorCount = 12;
    public const int ExpectedSearchableCount = 12;
    public const int ExpectedResidentCount = 4;
    public const int FurniturePerRoom = 4;
    public const int StaticFurniturePerRoom = FurniturePerRoom - 1;
    public const int ExpectedStaticFurnitureCount =
        ExpectedRoomCount * StaticFurniturePerRoom;
    public const string EnterableSourceGroup = "jianghai_enterable_source";
    public const float InteriorVisibilityRange = 42.0f;
    public const string LatticeDoorScenePath =
        "res://assets/models/jianghai_old_city/jianghai_lattice_door.glb";

    private const float DefaultDoorWidth = 1.58f;
    private const float DefaultDoorHeight = 2.48f;
    private const float DefaultRoomWidth = 4.8f;
    private const float DefaultRoomDepth = 5.2f;

    private static readonly IReadOnlyDictionary<string, string> ExpectedSources =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EastGateRow00"] = "family_shop",
            ["EastPhotoHouse"] = "family_home",
            ["EastTeaHouse"] = "tea_house",
            ["NorthwestGateHouse"] = "family_home",
            ["OuterEastMidResidence"] = "family_home",
            ["OuterWestSquareResidence"] = "family_home",
            ["WeatheredRollerShop00"] = "family_shop",
            ["WeatheredRollerShop01"] = "family_shop",
            ["WeatheredRollerShop02"] = "tea_house",
            ["WeatheredRollerShop03"] = "repair_shop",
            ["WestMarketResidence"] = "family_home",
            ["WestMedicineRow01"] = "repair_shop"
        };

    public JianghaiInteriorBuildResult Build(
        Node3D authoredRoot,
        Node3D parent,
        int firstDoorId,
        Func<ResidentialFurnitureKind, string, int, int, IEnumerable<LootItem>> createLoot)
    {
        ArgumentNullException.ThrowIfNull(authoredRoot);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(createLoot);

        var result = new JianghaiInteriorBuildResult();
        var staticFurnitureBatcher = new JianghaiInteriorFurnitureBatcher(parent);
        var sources = FindEnterableSources(authoredRoot, out var unexpectedSourceCount);
        result.SourceCount = sources.Count;
        result.UnexpectedSourceCount = unexpectedSourceCount;
        for (var roomIndex = 0; roomIndex < sources.Count; roomIndex++)
        {
            BuildRoom(
                sources[roomIndex],
                parent,
                firstDoorId + roomIndex,
                roomIndex,
                createLoot,
                staticFurnitureBatcher,
                result);
        }
        staticFurnitureBatcher.Build(result);
        return result;
    }

    public static bool IsExpectedSourceName(string name)
        => ExpectedSources.ContainsKey(name);

    public static string ExpectedArchetypeFor(string name)
        => ExpectedSources.TryGetValue(name, out var archetype) ? archetype : string.Empty;

    private static List<MeshInstance3D> FindEnterableSources(
        Node3D authoredRoot,
        out int unexpectedSourceCount)
    {
        var result = new List<MeshInstance3D>();
        unexpectedSourceCount = 0;
        var nodes = authoredRoot.FindChildren("*", "MeshInstance3D", recursive: true, owned: false);
        using var nodesBacking = nodes.AsDisposable();
        foreach (var child in nodes)
        {
            if (child is not MeshInstance3D source)
            {
                continue;
            }
            var sourceName = source.Name.ToString();
            var hasImportedMetadata = source.HasMeta("jianghai_enterable")
                && source.GetMeta("jianghai_enterable").AsBool();
            if (!ExpectedSources.TryGetValue(sourceName, out var fallbackArchetype))
            {
                unexpectedSourceCount += hasImportedMetadata ? 1 : 0;
                continue;
            }
            ApplyImportedContractFallback(source, fallbackArchetype, hasImportedMetadata);
            source.AddToGroup(EnterableSourceGroup);
            result.Add(source);
        }
        result.Sort(static (left, right) => string.Compare(
            left.Name.ToString(),
            right.Name.ToString(),
            StringComparison.Ordinal));
        return result;
    }

    private static void ApplyImportedContractFallback(
        MeshInstance3D source,
        string fallbackArchetype,
        bool hasImportedMetadata)
    {
        var bounds = source.GetAabb();
        var worldWidth = bounds.Size.X * source.GlobalBasis.X.Length();
        var worldDepth = bounds.Size.Z * source.GlobalBasis.Z.Length();
        source.SetMeta("jianghai_enterable", true);
        if (!source.HasMeta("jianghai_room_archetype"))
        {
            source.SetMeta("jianghai_room_archetype", fallbackArchetype);
        }
        if (!source.HasMeta("jianghai_door_width_m"))
        {
            source.SetMeta("jianghai_door_width_m", DefaultDoorWidth);
        }
        if (!source.HasMeta("jianghai_door_height_m"))
        {
            source.SetMeta("jianghai_door_height_m", DefaultDoorHeight);
        }
        if (!source.HasMeta("jianghai_door_front"))
        {
            source.SetMeta("jianghai_door_front", "local_positive_z_godot");
        }
        if (!source.HasMeta("jianghai_room_width_m"))
        {
            source.SetMeta("jianghai_room_width_m", Mathf.Clamp(worldWidth - 1.2f, 3.8f, 7.2f));
        }
        if (!source.HasMeta("jianghai_room_depth_m"))
        {
            source.SetMeta("jianghai_room_depth_m", Mathf.Clamp(worldDepth - 1.3f, 3.8f, 6.4f));
        }
        if (JianghaiGameplayCollisionContract.TryGetEnterableRoom(
                source.Name.ToString(),
                out var collisionContract))
        {
            source.SetMeta("jianghai_room_width_m", collisionContract.InteriorWidth);
            source.SetMeta("jianghai_room_depth_m", collisionContract.InteriorDepth);
            SetMetaFallback(
                source,
                "jianghai_door_front_inset_m",
                collisionContract.FrontInset);
            SetMetaFallback(
                source,
                "jianghai_collision_width_m",
                collisionContract.CollisionWidth);
            SetMetaFallback(
                source,
                "jianghai_collision_depth_m",
                collisionContract.CollisionDepth);
            SetMetaFallback(
                source,
                "jianghai_collision_height_m",
                collisionContract.CollisionHeight);
            SetMetaFallback(
                source,
                "jianghai_collision_facade_width_m",
                collisionContract.FacadeWidth);
            SetMetaFallback(
                source,
                "jianghai_collision_wing_front_inset_m",
                collisionContract.WingFrontInset);
            SetMetaFallback(
                source,
                "jianghai_collision_rear_wing_inset_m",
                collisionContract.RearWingInset);
            SetMetaFallback(
                source,
                "jianghai_collision_wing_inner_half_width_m",
                collisionContract.WingInnerHalfWidth);
            SetMetaFallback(
                source,
                "jianghai_collision_wing_outer_half_width_m",
                collisionContract.WingOuterHalfWidth);
            SetMetaFallback(
                source,
                "jianghai_collision_side_half_width_m",
                collisionContract.SideHalfWidth);
            SetMetaFallback(
                source,
                "jianghai_collision_side_front_inset_m",
                collisionContract.SideFrontInset);
            SetMetaFallback(
                source,
                "jianghai_collision_side_rear_inset_m",
                collisionContract.SideRearInset);
        }
        source.SetMeta(
            "jianghai_enterable_contract_source",
            hasImportedMetadata ? "gltf_extras" : "exact_name_fallback");
    }

    private static void BuildRoom(
        MeshInstance3D source,
        Node3D parent,
        int doorId,
        int roomIndex,
        Func<ResidentialFurnitureKind, string, int, int, IEnumerable<LootItem>> createLoot,
        JianghaiInteriorFurnitureBatcher staticFurnitureBatcher,
        JianghaiInteriorBuildResult result)
    {
        var bounds = source.GetAabb();
        var doorWidth = MetaFloat(source, "jianghai_door_width_m", DefaultDoorWidth);
        var doorHeight = MetaFloat(source, "jianghai_door_height_m", DefaultDoorHeight);
        var roomWidth = MetaFloat(source, "jianghai_room_width_m", DefaultRoomWidth);
        var roomDepth = MetaFloat(source, "jianghai_room_depth_m", DefaultRoomDepth);
        var frontInset = MetaFloat(source, "jianghai_door_front_inset_m", 0.0f);
        var archetype = source.GetMeta("jianghai_room_archetype", "family_home").AsString();
        var doorLocal = new Vector3(bounds.GetCenter().X, bounds.Position.Y, bounds.End.Z);
        var visualFrontWorld = source.GlobalTransform * doorLocal;
        var outward = source.GlobalBasis.Z;
        outward.Y = 0.0f;
        outward = outward.LengthSquared() > 0.001f ? outward.Normalized() : Vector3.Forward;
        var doorWorld = visualFrontWorld - outward * frontInset;
        var yaw = Mathf.Atan2(outward.X, outward.Z);

        var roomRoot = new Node3D
        {
            Name = $"JianghaiInterior_{source.Name}"
        };
        roomRoot.AddToGroup("jianghai_enterable_room");
        roomRoot.SetMeta("jianghai_source_name", source.Name.ToString());
        roomRoot.SetMeta("jianghai_room_archetype", archetype);
        roomRoot.SetMeta("jianghai_room_width_m", roomWidth);
        roomRoot.SetMeta("jianghai_room_depth_m", roomDepth);
        roomRoot.SetMeta("jianghai_door_front_inset_m", frontInset);
        parent.AddChild(roomRoot);
        roomRoot.GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw), doorWorld);

        var door = new InteractiveBuildingDoor
        {
            Name = $"JianghaiLatticeDoor_{source.Name}"
        };
        door.Configure(
            doorId,
            doorwayWidth: doorWidth,
            doorwayHeight: doorHeight,
            frontZ: 0.0f,
            visibilityRange: 80.0f,
            motionStyle: BuildingDoorMotionStyle.Hinged,
            visualScenePath: LatticeDoorScenePath,
            sourceWidth: 0.8f,
            sourceHeight: 1.6f,
            hingedVisualUsesPivotOrigin: true,
            disableVisualShadows: true);
        door.AddToGroup("jianghai_enterable_door");
        door.SetMeta("jianghai_door_visual_scene", LatticeDoorScenePath);
        roomRoot.AddChild(door);

        var furniture = BuildFurniture(
            roomRoot,
            archetype,
            roomIndex,
            roomWidth,
            roomDepth,
            createLoot,
            staticFurnitureBatcher,
            out var searchables,
            out var authoredMeshCount);
        result.AuthoredFurnitureMeshCount += authoredMeshCount;
        result.Doors.Add(door);
        result.Searchables.AddRange(searchables);
        result.Rooms.Add(new JianghaiInteriorRoom(
            source.Name.ToString(),
            archetype,
            source,
            roomRoot,
            door,
            furniture,
            searchables,
            roomRoot.ToGlobal(new Vector3(0, 0.12f, 1.05f)),
            roomRoot.ToGlobal(new Vector3(0, 0.12f, -1.05f)),
            new Vector3(0, 0.14f, -Mathf.Min(roomDepth * 0.36f, 1.85f)),
            new Vector3(0, 0.14f, -Mathf.Min(roomDepth * 0.58f, 2.7f)),
            roomWidth,
            roomDepth,
            frontInset));
    }

    private static List<Node3D> BuildFurniture(
        Node3D roomRoot,
        string archetype,
        int roomIndex,
        float width,
        float depth,
        Func<ResidentialFurnitureKind, string, int, int, IEnumerable<LootItem>> createLoot,
        JianghaiInteriorFurnitureBatcher staticFurnitureBatcher,
        out List<ResidentialSearchableFurniture> searchables,
        out int authoredMeshCount)
    {
        var furniture = new List<Node3D>(FurniturePerRoom);
        searchables = new List<ResidentialSearchableFurniture>();
        authoredMeshCount = 0;
        const int searchableCount = 1;
        var staticSpecs = JianghaiInteriorFurnitureLayout.StaticSpecs(archetype);
        var staticCount = FurniturePerRoom - searchableCount;
        for (var index = 0; index < staticCount; index++)
        {
            var spec = staticSpecs[index % staticSpecs.Count];
            var local = JianghaiInteriorFurnitureLayout.StaticSlot(index, width, depth);
            var prop = CreateStaticFurniture(
                spec,
                local.Position,
                local.Yaw,
                out var authoredVisual,
                out var meshCount);
            roomRoot.AddChild(prop);
            if (authoredVisual is not null)
            {
                ConfigureInteriorVisuals(authoredVisual);
                staticFurnitureBatcher.Capture(prop, authoredVisual);
            }
            furniture.Add(prop);
            authoredMeshCount += meshCount;
        }

        for (var index = 0; index < searchableCount; index++)
        {
            var kind = JianghaiInteriorFurnitureLayout.SearchableKind(
                archetype,
                roomIndex,
                index);
            var local = JianghaiInteriorFurnitureLayout.SearchableSlot(index, width, depth);
            var searchable = new ResidentialSearchableFurniture
            {
                Name = $"JianghaiSearchable_{roomIndex + 1:00}_{index + 1:00}_{kind}",
                Position = local.Position,
                Rotation = new Vector3(0, local.Yaw, 0)
            };
            searchable.Configure(
                kind,
                ResidentialRoomEventKind.None,
                220 + roomIndex,
                0,
                index == 0 ? -1 : 1,
                createLoot(kind, archetype, roomIndex, index));
            searchable.AddToGroup("jianghai_interior_loot");
            searchable.SetMeta(
                "jianghai_authored_furniture_scene",
                ResidentialAuthoredPropLibrary.PathFor(kind));
            roomRoot.AddChild(searchable);
            ConfigureInteriorVisuals(searchable);
            furniture.Add(searchable);
            searchables.Add(searchable);
            authoredMeshCount += CountVisibleMeshes(searchable);
        }
        return furniture;
    }

    private static Node3D CreateStaticFurniture(
        JianghaiStaticFurnitureSpec spec,
        Vector3 position,
        float yaw,
        out Node3D? authoredVisual,
        out int meshCount)
    {
        authoredVisual = null;
        var body = new StaticBody3D
        {
            Name = spec.Name,
            Position = position,
            Rotation = new Vector3(0, yaw, 0),
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup("jianghai_interior_furniture");
        body.SetMeta("jianghai_authored_furniture_scene", spec.ScenePath);
        body.SetMeta("jianghai_non_primitive_visual", true);
        var shapeOwner = body.CreateShapeOwner(body);
        body.ShapeOwnerSetTransform(
            shapeOwner,
            new Transform3D(Basis.Identity, Vector3.Up * (spec.Size.Y * 0.5f)));
        body.ShapeOwnerAddShape(shapeOwner, new BoxShape3D { Size = spec.Size });
        body.SetMeta("jianghai_static_furniture_shape_owner", shapeOwner);
        if (!ResidentialAuthoredPropLibrary.TryCreateVisual(
                spec.ScenePath,
                spec.Size,
                out var visual,
                out meshCount))
        {
            meshCount = 0;
            return body;
        }
        visual.Position += Vector3.Up * (spec.Size.Y * 0.5f);
        body.AddChild(visual);
        authoredVisual = visual;
        return body;
    }

    private static void ConfigureInteriorVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            visual.VisibilityRangeEnd = InteriorVisibilityRange;
            visual.VisibilityRangeEndMargin = 6.0f;
            visual.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        }
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureInteriorVisuals(childNode);
            }
        }
    }

    private static int CountVisibleMeshes(Node node)
    {
        var count = node is MeshInstance3D { Visible: true } ? 1 : 0;
        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                count += CountVisibleMeshes(childNode);
            }
        }
        return count;
    }

    private static float MetaFloat(Node node, string key, float fallback)
        => node.HasMeta(key) ? (float)node.GetMeta(key).AsDouble() : fallback;

    private static void SetMetaFallback(Node node, string key, float value)
    {
        if (!node.HasMeta(key))
        {
            node.SetMeta(key, value);
        }
    }
}
