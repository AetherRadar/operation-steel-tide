using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static bool FurnitureAuthoredReady(Node3D furniture)
    {
        var scenePath = furniture.GetMeta(
            "jianghai_authored_furniture_scene",
            string.Empty).AsString();
        var meshes = VisibleFurnitureMeshes(furniture).ToArray();
        var collisionReady = HasDirectFurnitureBoxCollision(furniture)
            || furniture.FindChild(
                    "FurnitureCollision",
                    recursive: true,
                    owned: false)
                is CollisionShape3D { Shape: BoxShape3D };
        var batchedStaticReady = furniture.GetMeta(
                "jianghai_static_furniture_batched",
                false).AsBool()
            && furniture.GetMeta(
                "jianghai_static_furniture_collision_retained",
                false).AsBool()
            && furniture.GetMeta(
                "jianghai_static_furniture_mesh_count",
                0).AsInt32() > 0
            && meshes.Length == 0;
        var independentSearchableReady = meshes.Length > 0
            && meshes.All(mesh => HasAuthoredModelAncestor(mesh, furniture));
        return !string.IsNullOrWhiteSpace(scenePath)
            && ResourceLoader.Exists(scenePath)
            && collisionReady
            && (batchedStaticReady || independentSearchableReady);
    }

    private static bool HasDirectFurnitureBoxCollision(Node3D furniture)
    {
        if (furniture is not CollisionObject3D collision)
        {
            return false;
        }
        var owners = collision.GetShapeOwners();
        foreach (var owner in owners)
        {
            var ownerId = (uint)owner;
            if (collision.ShapeOwnerGetOwner(ownerId) is CollisionShape3D
                || collision.ShapeOwnerGetShapeCount(ownerId) != 1
                || collision.ShapeOwnerGetShape(ownerId, 0) is not BoxShape3D)
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private string DescribePlayerLootBlockers(ILootSource source)
    {
        var descriptions = new List<string>();
        var exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        using var excludeBacking = exclude.AsDisposable();
        if (source.LootNode is CollisionObject3D collisionSource)
        {
            exclude.Add(collisionSource.GetRid());
        }
        var from = _player.GlobalPosition + Vector3.Up * 1.25f;
        foreach (var targetHeight in new[] { 0.28f, 0.72f, 1.16f })
        {
            if (!PhysicsRaycast.TryHit(
                    GetWorld3D(),
                    from,
                    source.LootNode.GlobalPosition + Vector3.Up * targetHeight,
                    exclude,
                    1,
                    out var hit))
            {
                descriptions.Add($"{targetHeight:0.00}:clear");
                continue;
            }
            var colliderName = hit.Collider is Node collider
                ? collider.Name.ToString()
                : hit.Collider?.GetClass() ?? "unknown";
            var ownerName = "none";
            if (hit.Collider is CollisionObject3D collision && hit.Shape >= 0)
            {
                var ownerId = collision.ShapeFindOwner(hit.Shape);
                if (collision.ShapeOwnerGetOwner(ownerId) is Node owner)
                {
                    ownerName = owner.Name.ToString();
                }
            }
            descriptions.Add($"{targetHeight:0.00}:{colliderName}/{ownerName}#{hit.Shape}");
        }
        return string.Join(',', descriptions);
    }

    private static IEnumerable<MeshInstance3D> VisibleFurnitureMeshes(Node3D furniture)
    {
        var nodes = furniture.FindChildren(
            "*",
            "MeshInstance3D",
            recursive: true,
            owned: false);
        using var nodesBacking = nodes.AsDisposable();
        foreach (var child in nodes)
        {
            if (child is MeshInstance3D { Visible: true } mesh)
            {
                yield return mesh;
            }
        }
    }

    private static bool HasAuthoredModelAncestor(Node node, Node stop)
    {
        for (var current = node.GetParent();
            current is not null && current != stop;
            current = current.GetParent())
        {
            if (current.Name == "AuthoredModel")
            {
                return true;
            }
        }
        return false;
    }
}
