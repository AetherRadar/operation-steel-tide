using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public readonly record struct LanternCanalCollisionReport(
    int ShapeCount,
    int TriangleCount,
    int BuildingCount,
    int BridgeCount);

/// <summary>
/// Builds invisible physics from the authored district in its imported coordinate system.
/// The owner supplies the loaded city and a collision parent with the same world lifetime.
/// </summary>
public static class LanternCanalCollisionBuilder
{
    private const float BuildingCollisionCeiling = 14.0f;
    private const float MinimumBuildingTriangleArea = 0.012f;
    private const int MaximumCollisionTriangles = 1_600_000;

    public static LanternCanalCollisionReport Build(Node3D city, Node3D collisionParent)
    {
        var shapeCount = 0;
        var triangleCount = 0;
        var buildingCount = 0;
        var bridgeCount = 0;
        var requiredMeshes = new HashSet<string>(StringComparer.Ordinal)
        {
            "00_Ground_and_quays",
            "31_CanalFootbridge_01",
            "10_House_E_01",
            "10_House_E_05",
            "80_V25_Circulation_ceramics",
            "80_V25_Circulation_silk"
        };
        var meshes = city.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        foreach (var child in meshes)
        {
            if (child is not MeshInstance3D source)
            {
                continue;
            }
            var sourceName = source.Name.ToString();
            var kind = Classify(sourceName);
            if (kind == CollisionKind.None)
            {
                continue;
            }
            if (source.Mesh is not ArrayMesh mesh)
            {
                throw new InvalidOperationException($"Lantern Canal collision source '{sourceName}' requires an authored ArrayMesh.");
            }

            var faces = BuildFaces(source, mesh, kind);
            if (faces.Count == 0)
            {
                throw new InvalidOperationException($"Lantern Canal collision source '{sourceName}' has no structural triangles.");
            }
            triangleCount += faces.Count / 3;
            if (triangleCount > MaximumCollisionTriangles)
            {
                throw new InvalidOperationException("Lantern Canal authored collision exceeds its reviewed triangle budget.");
            }

            var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
            shape.SetFaces(faces.ToArray());
            var body = new StaticBody3D
            {
                Name = "LanternCanalCollision_" + sourceName,
                CollisionLayer = 1,
                CollisionMask = 0
            };
            collisionParent.AddChild(body);
            // Faces are baked into world space: the import's Z-up rotation is never guessed.
            body.GlobalTransform = Transform3D.Identity;
            body.AddChild(new CollisionShape3D { Name = "AuthoredSurface", Shape = shape });
            body.SetMeta("authored_source_node", sourceName);
            body.SetMeta("authored_collision_triangles", faces.Count / 3);
            requiredMeshes.Remove(sourceName);
            shapeCount++;
            buildingCount += kind == CollisionKind.Building ? 1 : 0;
            bridgeCount += kind == CollisionKind.Bridge ? 1 : 0;
        }

        if (requiredMeshes.Count != 0)
        {
            throw new InvalidOperationException("Lantern Canal collision is missing required authored meshes: "
                + string.Join(", ", requiredMeshes));
        }
        AddBoundary(collisionParent, "West", new Vector3(-80.25f, 3.0f, -53.0f), new Vector3(0.5f, 8.0f, 278.0f));
        AddBoundary(collisionParent, "East", new Vector3(80.25f, 3.0f, -53.0f), new Vector3(0.5f, 8.0f, 278.0f));
        AddBoundary(collisionParent, "North", new Vector3(0.0f, 3.0f, -192.25f), new Vector3(160.0f, 8.0f, 0.5f));
        AddBoundary(collisionParent, "South", new Vector3(0.0f, 3.0f, 86.25f), new Vector3(160.0f, 8.0f, 0.5f));
        return new LanternCanalCollisionReport(shapeCount + 4, triangleCount, buildingCount, bridgeCount);
    }

    private static List<Vector3> BuildFaces(MeshInstance3D source, ArrayMesh mesh, CollisionKind kind)
    {
        var faces = new List<Vector3>();
        var transform = source.GlobalTransform;
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            if (kind == CollisionKind.Building
                && !IsStructuralMaterial(mesh.SurfaceGetMaterial(surface)?.ResourceName ?? string.Empty))
            {
                continue;
            }
            if (mesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
            {
                throw new InvalidOperationException($"Lantern Canal collision source '{source.Name}' has a non-triangle surface.");
            }
            using var arrays = mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indexArray = arrays[(int)Mesh.ArrayType.Index];
            var indices = indexArray.VariantType == Variant.Type.Nil ? Array.Empty<int>() : indexArray.AsInt32Array();
            var length = indices.Length > 0 ? indices.Length : vertices.Length;
            for (var index = 0; index < length; index += 3)
            {
                var a = transform * vertices[indices.Length > 0 ? indices[index] : index];
                var b = transform * vertices[indices.Length > 0 ? indices[index + 1] : index + 1];
                var c = transform * vertices[indices.Length > 0 ? indices[index + 2] : index + 2];
                if (kind == CollisionKind.Building
                    && (Mathf.Min(a.Y, Mathf.Min(b.Y, c.Y)) > BuildingCollisionCeiling
                        || (b - a).Cross(c - a).LengthSquared() < 4.0f * MinimumBuildingTriangleArea * MinimumBuildingTriangleArea))
                {
                    continue;
                }
                faces.Add(a);
                faces.Add(b);
                faces.Add(c);
            }
        }
        return faces;
    }

    private static bool IsStructuralMaterial(string name)
    {
        // Keep walls, floors, doors and windows; plants, pots, lettering and lanterns do not
        // become obstacles. Fine joinery and inaccessible upper tower roofs need no physics.
        return name.Contains("timber", StringComparison.OrdinalIgnoreCase)
            || name.Contains("cedar", StringComparison.OrdinalIgnoreCase)
            || name.Contains("walnut", StringComparison.OrdinalIgnoreCase)
            || name.Contains("plaster", StringComparison.OrdinalIgnoreCase)
            || name.Contains("stone", StringComparison.OrdinalIgnoreCase)
            || name.Contains("masonry", StringComparison.OrdinalIgnoreCase)
            || name.Contains("lacquer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("window panes", StringComparison.OrdinalIgnoreCase);
    }

    private static CollisionKind Classify(string name)
    {
        if (name is "00_Ground_and_quays" or "60_V23_Submerged_riverbed")
        {
            return CollisionKind.Ground;
        }
        if (name.StartsWith("31_CanalFootbridge_", StringComparison.Ordinal)
            || name.StartsWith("32_CrossChannelBridge_", StringComparison.Ordinal)
            || name.StartsWith("33_SideLaneBridge_", StringComparison.Ordinal))
        {
            return CollisionKind.Bridge;
        }
        if (name.StartsWith("80_V2", StringComparison.Ordinal)
            || name is "53_V21_Teahouse_quay_and_landing" or "54_V21_Teahouse_small_timber_pier"
                or "56_V21_Teahouse_floorboards_and_ceiling")
        {
            // Circulation meshes contain the authored return stairs, landing and floorboards.
            // Preserve every tread rather than recreating a conflicting central ramp.
            return CollisionKind.Circulation;
        }
        if (name.StartsWith("10_House_", StringComparison.Ordinal)
            || name.StartsWith("11_OuterHouse_", StringComparison.Ordinal)
            || name.StartsWith("12_TowerQuarter_house_", StringComparison.Ordinal)
            || name.StartsWith("20_Tower_", StringComparison.Ordinal))
        {
            return CollisionKind.Building;
        }
        return CollisionKind.None;
    }

    private static void AddBoundary(Node3D parent, string side, Vector3 center, Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = "LanternCanalBoundary" + side,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        parent.AddChild(body);
        body.GlobalPosition = center;
        body.AddChild(new CollisionShape3D { Name = "Boundary", Shape = new BoxShape3D { Size = size } });
    }

    private enum CollisionKind
    {
        None,
        Ground,
        Bridge,
        Building,
        Circulation
    }
}
