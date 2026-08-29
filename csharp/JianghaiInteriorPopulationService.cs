using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Populates the six DCC-authored Old City shops without adding visible primitives.
/// The source mesh supplies orientation and doorway metadata; all furnishing visuals
/// come from the existing redistributable Kenney Furniture Kit.
/// </summary>
internal sealed class JianghaiInteriorPopulationService
{
    public const int ExpectedRoomCount = 6;
    public const int ExpectedDoorCount = 6;
    public const int ExpectedSearchableCount = 8;
    public const int ExpectedResidentCount = 4;
    public const int FurniturePerRoom = 4;
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
            ["EastPhotoHouse"] = "family_home",
            ["EastTeaHouse"] = "tea_house",
            ["WeatheredRollerShop00"] = "family_shop",
            ["WeatheredRollerShop01"] = "family_shop",
            ["WeatheredRollerShop02"] = "tea_house",
            ["WeatheredRollerShop03"] = "repair_shop"
        };

    private readonly record struct StaticFurnitureSpec(
        string Name,
        string ScenePath,
        Vector3 Size);

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
                result);
        }
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
            source.AddToGroup("jianghai_enterable_source");
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
            hingedVisualUsesPivotOrigin: true);
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
        out List<ResidentialSearchableFurniture> searchables,
        out int authoredMeshCount)
    {
        var furniture = new List<Node3D>(FurniturePerRoom);
        searchables = new List<ResidentialSearchableFurniture>();
        authoredMeshCount = 0;
        var searchableCount = roomIndex < 2 ? 2 : 1;
        var staticSpecs = StaticSpecs(archetype);
        var staticCount = FurniturePerRoom - searchableCount;
        for (var index = 0; index < staticCount; index++)
        {
            var spec = staticSpecs[index % staticSpecs.Count];
            var local = StaticSlot(index, width, depth);
            var prop = CreateStaticFurniture(spec, local.Position, local.Yaw, out var meshCount);
            roomRoot.AddChild(prop);
            ConfigureInteriorVisuals(prop);
            furniture.Add(prop);
            authoredMeshCount += meshCount;
        }

        for (var index = 0; index < searchableCount; index++)
        {
            var kind = SearchableKind(archetype, roomIndex, index);
            var local = SearchableSlot(index, width, depth);
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
        StaticFurnitureSpec spec,
        Vector3 position,
        float yaw,
        out int meshCount)
    {
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
        body.AddChild(new CollisionShape3D
        {
            Name = "FurnitureCollision",
            Position = Vector3.Up * (spec.Size.Y * 0.5f),
            Shape = new BoxShape3D { Size = spec.Size }
        });
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
        return body;
    }

    private static IReadOnlyList<StaticFurnitureSpec> StaticSpecs(string archetype)
    {
        var root = ResidentialAuthoredPropLibrary.FurnitureRoot;
        return archetype switch
        {
            "tea_house" => new[]
            {
                new StaticFurnitureSpec("TeaTable", $"{root}/table.glb", new Vector3(1.2f, 0.72f, 0.82f)),
                new StaticFurnitureSpec("TeaSofa", $"{root}/loungeSofa.glb", new Vector3(1.55f, 0.82f, 0.74f)),
                new StaticFurnitureSpec("TeaBookcase", $"{root}/bookcaseClosedDoors.glb", new Vector3(0.82f, 1.72f, 0.42f))
            },
            "repair_shop" => new[]
            {
                new StaticFurnitureSpec("RepairDesk", $"{root}/desk.glb", new Vector3(1.35f, 0.76f, 0.72f)),
                new StaticFurnitureSpec("RepairBookcase", $"{root}/bookcaseClosedDoors.glb", new Vector3(0.84f, 1.76f, 0.44f)),
                new StaticFurnitureSpec("RepairTable", $"{root}/table.glb", new Vector3(1.15f, 0.72f, 0.78f))
            },
            _ => new[]
            {
                new StaticFurnitureSpec("ResidentBed", $"{root}/bedSingle.glb", new Vector3(1.82f, 0.52f, 0.84f)),
                new StaticFurnitureSpec("ResidentTable", $"{root}/table.glb", new Vector3(1.08f, 0.72f, 0.78f)),
                new StaticFurnitureSpec("ResidentSofa", $"{root}/loungeSofa.glb", new Vector3(1.58f, 0.82f, 0.74f))
            }
        };
    }

    private static (Vector3 Position, float Yaw) StaticSlot(int index, float width, float depth)
    {
        var side = Mathf.Max(0.9f, width * 0.31f);
        var back = Mathf.Min(depth * 0.72f, depth - 0.62f);
        return index switch
        {
            0 => (new Vector3(0, 0.02f, -back), 0.0f),
            1 => (new Vector3(-side, 0.02f, -depth * 0.46f), -Mathf.Pi * 0.5f),
            _ => (new Vector3(side, 0.02f, -depth * 0.5f), Mathf.Pi * 0.5f)
        };
    }

    private static (Vector3 Position, float Yaw) SearchableSlot(int index, float width, float depth)
    {
        var side = Mathf.Max(0.82f, width * 0.29f);
        return index == 0
            ? (new Vector3(-side, 0.02f, -depth * 0.25f), 0.0f)
            : (new Vector3(side, 0.02f, -depth * 0.27f), Mathf.Pi);
    }

    private static ResidentialFurnitureKind SearchableKind(
        string archetype,
        int roomIndex,
        int itemIndex)
        => archetype switch
        {
            "tea_house" => itemIndex == 0
                ? ResidentialFurnitureKind.Refrigerator
                : ResidentialFurnitureKind.DeskDrawers,
            "repair_shop" => itemIndex == 0
                ? ResidentialFurnitureKind.DeskDrawers
                : ResidentialFurnitureKind.Wardrobe,
            "family_shop" => itemIndex == 0
                ? ResidentialFurnitureKind.Wardrobe
                : ResidentialFurnitureKind.Nightstand,
            _ => (ResidentialFurnitureKind)Mathf.PosMod(
                roomIndex + itemIndex,
                Enum.GetValues<ResidentialFurnitureKind>().Length)
        };

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
