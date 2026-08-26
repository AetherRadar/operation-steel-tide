using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Shared Kenney Furniture Kit loader for searchable furniture and room dressing.
/// Collision stays on the caller; this only instantiates authored visuals.
/// </summary>
internal static class ResidentialAuthoredPropLibrary
{
    public const string FurnitureRoot = "res://assets/models/kenney_furniture_kit";

    private static readonly Dictionary<string, PackedScene> Scenes = new();

    public static string PathFor(ResidentialFurnitureKind kind) => kind switch
    {
        ResidentialFurnitureKind.Refrigerator => $"{FurnitureRoot}/kitchenFridgeLarge.glb",
        ResidentialFurnitureKind.Wardrobe => $"{FurnitureRoot}/bookcaseClosedDoors.glb",
        ResidentialFurnitureKind.DeskDrawers => $"{FurnitureRoot}/cabinetBedDrawerTable.glb",
        _ => $"{FurnitureRoot}/cabinetBedDrawer.glb"
    };

    public static string PathForRoomProp(string name)
    {
        if (name.StartsWith("ClinicCot") || name.StartsWith("EvacBunkLow"))
        {
            return $"{FurnitureRoot}/bedSingle.glb";
        }

        if (name.StartsWith("EvacBunkHigh"))
        {
            return $"{FurnitureRoot}/bedBunk.glb";
        }

        if (name.Contains("Desk", System.StringComparison.Ordinal)
            || name.Contains("Workbench", System.StringComparison.Ordinal)
            || name.Contains("Bench", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/desk.glb";
        }

        if (name.Contains("Locker", System.StringComparison.Ordinal)
            || name.Contains("Wardrobe", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/bookcaseClosedDoors.glb";
        }

        if (name.Contains("Fridge", System.StringComparison.Ordinal)
            || name.Contains("ColdStore", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/kitchenFridgeLarge.glb";
        }

        if (name.Contains("Island", System.StringComparison.Ordinal)
            || name.Contains("Cabinet", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/kitchenCabinet.glb";
        }

        if (name.Contains("Table", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/table.glb";
        }

        if (name.Contains("Crate", System.StringComparison.Ordinal)
            || name.Contains("Luggage", System.StringComparison.Ordinal)
            || name.Contains("ToyChest", System.StringComparison.Ordinal)
            || name.Contains("Ration", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/cardboardBoxClosed.glb";
        }

        if (name.Contains("Sofa", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/loungeSofa.glb";
        }

        if (name.Contains("Screen", System.StringComparison.Ordinal)
            || name.Contains("ShieldRack", System.StringComparison.Ordinal))
        {
            return $"{FurnitureRoot}/computerScreen.glb";
        }

        return $"{FurnitureRoot}/kitchenCabinetDrawer.glb";
    }

    public static bool TryCreateVisual(string scenePath, Vector3 targetSize, out Node3D visual, out int meshCount)
    {
        visual = null!;
        meshCount = 0;
        if (!TryLoad(scenePath, out var scene) || scene.Instantiate() is not Node3D model)
        {
            return false;
        }

        model.Name = "AuthoredModel";
        var bounds = CollectAabb(model, Transform3D.Identity);
        if (bounds.Size.X <= 0.001f || bounds.Size.Y <= 0.001f || bounds.Size.Z <= 0.001f)
        {
            model.QueueFree();
            return false;
        }

        var scale = new Vector3(
            targetSize.X / bounds.Size.X,
            targetSize.Y / bounds.Size.Y,
            targetSize.Z / bounds.Size.Z);
        model.Scale = scale;
        var scaledCenter = bounds.GetCenter() * scale;
        model.Position = new Vector3(
            -scaledCenter.X,
            -targetSize.Y * 0.5f - bounds.Position.Y * scale.Y,
            -scaledCenter.Z);
        ConfigureVisuals(model);
        meshCount = CountMeshes(model);
        visual = model;
        return meshCount > 0;
    }

    public static void HidePrimitiveMeshes(Node root)
    {
        if (root is MeshInstance3D mesh && mesh.Name != "AuthoredModel")
        {
            mesh.Visible = false;
        }

        var children = root.GetChildren();
        using var backing = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node node && node.Name != "AuthoredModel")
            {
                HidePrimitiveMeshes(node);
            }
        }
    }

    public static void ReleaseSharedResources()
    {
        Scenes.Clear();
    }

    private static bool TryLoad(string path, out PackedScene scene)
    {
        if (Scenes.TryGetValue(path, out scene!))
        {
            return true;
        }

        scene = GD.Load<PackedScene>(path);
        if (scene is null)
        {
            GD.PushError($"Residential authored furniture is missing: {path}");
            return false;
        }

        Scenes[path] = scene;
        return true;
    }

    private static void ConfigureVisuals(Node node)
    {
        if (node is GeometryInstance3D visual)
        {
            visual.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            visual.VisibilityRangeEnd = 80.0f;
            visual.VisibilityRangeEndMargin = 12.0f;
        }

        var children = node.GetChildren();
        using var backing = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                ConfigureVisuals(childNode);
            }
        }
    }

    private static Aabb CollectAabb(Node node, Transform3D parent)
    {
        var transform = parent;
        if (node is Node3D node3D)
        {
            transform = parent * node3D.Transform;
        }

        var bounds = new Aabb();
        var hasBounds = false;
        if (node is MeshInstance3D mesh && mesh.Mesh is not null)
        {
            bounds = transform * mesh.Mesh.GetAabb();
            hasBounds = true;
        }

        var children = node.GetChildren();
        using var backing = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is not Node childNode)
            {
                continue;
            }

            var childBounds = CollectAabb(childNode, transform);
            if (childBounds.Size == Vector3.Zero)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = childBounds;
                hasBounds = true;
            }
            else
            {
                bounds = bounds.Merge(childBounds);
            }
        }

        return hasBounds ? bounds : new Aabb();
    }

    private static int CountMeshes(Node node)
    {
        var count = node is MeshInstance3D ? 1 : 0;
        var children = node.GetChildren();
        using var backing = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                count += CountMeshes(childNode);
            }
        }

        return count;
    }
}
