using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record JianghaiGameplayCollisionResult(
    StaticBody3D Body,
    int SourcePlacementCount,
    int AuthoredSourceMeshCount,
    int CollisionShapeCount,
    int BoxShapeCount,
    int ConcaveShapeCount,
    IReadOnlyDictionary<string, int> DistrictShapeCounts);

/// <summary>
/// Builds deterministic box-only gameplay collision from the stable extraction layout.
/// Visual mesh topology can change without triggering runtime triangle extraction or physics baking.
/// </summary>
internal sealed class JianghaiGameplayCollisionBuilder
{
    public const string CollisionGroup = "jianghai_gameplay_collision";
    public const int ExpectedAuthoredProxyCount = 6;

    public JianghaiGameplayCollisionResult Build(
        RefineryExtractionMapLayout layout,
        Node3D? authoredRoot,
        Node3D parent)
        => BuildInternal(layout, authoredRoot, parent, requireAuthoredProxies: true);

    public JianghaiGameplayCollisionResult BuildPlacementFallback(
        RefineryExtractionMapLayout layout,
        Node3D parent)
        => BuildInternal(layout, null, parent, requireAuthoredProxies: false);

    private static JianghaiGameplayCollisionResult BuildInternal(
        RefineryExtractionMapLayout layout,
        Node3D? authoredRoot,
        Node3D parent,
        bool requireAuthoredProxies)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(parent);

        var body = new StaticBody3D
        {
            Name = "JianghaiGameplayCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup(CollisionGroup);
        parent.AddChild(body);

        var districtCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var placementCount = 0;
        var authoredSourceCount = 0;
        var shapeCount = 0;
        try
        {
            foreach (var placement in layout.Models)
            {
                if (!placement.HasCollision)
                {
                    continue;
                }

                var size = placement.CollisionSize * placement.Scale;
                var offset = placement.CollisionOffset * placement.Scale;
                if (size.X <= 0.01f || size.Y <= 0.01f || size.Z <= 0.01f)
                {
                    continue;
                }

                var basis = Basis.FromEuler(new Vector3(0.0f, placement.Yaw, 0.0f));
                var collision = new CollisionShape3D
                {
                    Name = $"GameplayProxy_{shapeCount + 1:000}_{placement.Name}",
                    Shape = new BoxShape3D { Size = size },
                    Transform = new Transform3D(
                        basis,
                        placement.Position + basis * offset)
                };
                collision.SetMeta("gameplay_source_placement", placement.Name);
                collision.SetMeta("gameplay_district", placement.District);
                body.AddChild(collision);
                placementCount++;
                shapeCount++;
                districtCounts.TryGetValue(placement.District, out var districtCount);
                districtCounts[placement.District] = districtCount + 1;
            }

            if (authoredRoot is not null)
            {
                var authoredMeshes = authoredRoot.FindChildren(
                    "*", "MeshInstance3D", recursive: true, owned: false);
                using var authoredMeshesBacking = authoredMeshes.AsDisposable();
                foreach (var child in authoredMeshes)
                {
                    if (child is not MeshInstance3D { Mesh: not null } meshInstance
                        || !RequestsGameplayProxy(meshInstance))
                    {
                        continue;
                    }

                    var localBounds = meshInstance.GetAabb();
                    var worldScale = meshInstance.GlobalBasis.Scale.Abs();
                    var worldSize = new Vector3(
                        localBounds.Size.X * worldScale.X,
                        localBounds.Size.Y * worldScale.Y,
                        localBounds.Size.Z * worldScale.Z);
                    if (worldSize.X <= 0.05f
                        || worldSize.Y <= 0.05f
                        || worldSize.Z <= 0.05f)
                    {
                        continue;
                    }

                    var basis = meshInstance.GlobalBasis.Orthonormalized();
                    var center = meshInstance.GlobalTransform * localBounds.GetCenter();
                    var doorway = meshInstance.HasMeta("jianghai_collision_doorway")
                        && meshInstance.GetMeta("jianghai_collision_doorway").AsBool();
                    shapeCount += doorway
                        ? AddDoorwayProxy(body, meshInstance, basis, center, worldSize, shapeCount)
                        : AddAuthoredBox(body, meshInstance, basis, center, worldSize, shapeCount);
                    authoredSourceCount++;
                }
                if (authoredSourceCount > 0)
                {
                    districtCounts["authored_edge"] = authoredSourceCount;
                }
            }

            var expectedPlacements = 0;
            foreach (var placement in layout.Models)
            {
                if (placement.HasCollision)
                {
                    expectedPlacements++;
                }
            }
            if (placementCount != expectedPlacements
                || requireAuthoredProxies
                    && authoredSourceCount != ExpectedAuthoredProxyCount)
            {
                throw new InvalidOperationException(
                    "Jianghai gameplay collision contract incomplete "
                    + $"(placements={placementCount}/{expectedPlacements}, "
                    + $"authored={authoredSourceCount}/{ExpectedAuthoredProxyCount}).");
            }

            body.SetMeta("gameplay_source_placement_count", placementCount);
            body.SetMeta("gameplay_authored_source_mesh_count", authoredSourceCount);
            body.SetMeta("gameplay_collision_shape_count", shapeCount);
            return new JianghaiGameplayCollisionResult(
                body,
                placementCount,
                authoredSourceCount,
                shapeCount,
                shapeCount,
                0,
                districtCounts);
        }
        catch
        {
            body.QueueFree();
            throw;
        }
    }

    private static bool RequestsGameplayProxy(MeshInstance3D meshInstance)
    {
        if (meshInstance.HasMeta("jianghai_gameplay_proxy")
            && meshInstance.GetMeta("jianghai_gameplay_proxy").AsBool())
        {
            return true;
        }
        var name = meshInstance.Name.ToString();
        return name.StartsWith("JianghaiGameplayProxy_", StringComparison.Ordinal)
            || name.StartsWith("ChineseEdgeBuilding_", StringComparison.Ordinal)
            || IsDensityEdgeProxyName(name);
    }

    private static bool IsDensityEdgeProxyName(string name)
    {
        var isEdgeBuilding = name.StartsWith(
                "JianghaiDensity_WestEdge",
                StringComparison.Ordinal)
            || name.StartsWith(
                "JianghaiDensity_EastEdge",
                StringComparison.Ordinal);
        return isEdgeBuilding
            && (name.EndsWith("04", StringComparison.Ordinal)
                || name.EndsWith("05", StringComparison.Ordinal)
                || name.EndsWith("06", StringComparison.Ordinal));
    }

    private static int AddAuthoredBox(
        StaticBody3D body,
        MeshInstance3D source,
        Basis basis,
        Vector3 center,
        Vector3 size,
        int shapeIndex)
    {
        AddBox(body, source, basis, center, size, shapeIndex, "shell");
        return 1;
    }

    private static int AddDoorwayProxy(
        StaticBody3D body,
        MeshInstance3D source,
        Basis basis,
        Vector3 center,
        Vector3 size,
        int shapeIndex)
    {
        var doorwayWidth = Mathf.Clamp(size.X * 0.24f, 1.4f, 3.8f);
        var doorwayHeight = Mathf.Clamp(size.Y * 0.34f, 2.2f, 3.8f);
        var sideWidth = Mathf.Max(0.3f, (size.X - doorwayWidth) * 0.5f);
        var sideOffset = (doorwayWidth + sideWidth) * 0.5f;
        AddBox(
            body,
            source,
            basis,
            center + basis.X * -sideOffset,
            new Vector3(sideWidth, size.Y, size.Z),
            shapeIndex,
            "door_left");
        AddBox(
            body,
            source,
            basis,
            center + basis.X * sideOffset,
            new Vector3(sideWidth, size.Y, size.Z),
            shapeIndex + 1,
            "door_right");
        var lintelHeight = Mathf.Max(0.3f, size.Y - doorwayHeight);
        var bottom = center.Y - size.Y * 0.5f;
        var lintelCenter = new Vector3(
            center.X,
            bottom + doorwayHeight + lintelHeight * 0.5f,
            center.Z);
        AddBox(
            body,
            source,
            basis,
            lintelCenter,
            new Vector3(doorwayWidth, lintelHeight, size.Z),
            shapeIndex + 2,
            "door_lintel");
        return 3;
    }

    private static void AddBox(
        StaticBody3D body,
        MeshInstance3D source,
        Basis basis,
        Vector3 center,
        Vector3 size,
        int shapeIndex,
        string role)
    {
        var collision = new CollisionShape3D
        {
            Name = $"AuthoredProxy_{shapeIndex + 1:000}_{source.Name}_{role}",
            Shape = new BoxShape3D { Size = size },
            Transform = new Transform3D(basis, center)
        };
        collision.SetMeta("gameplay_source_node", source.Name.ToString());
        collision.SetMeta("gameplay_proxy_role", role);
        body.AddChild(collision);
    }
}
