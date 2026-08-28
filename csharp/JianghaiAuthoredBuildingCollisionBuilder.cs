using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record JianghaiAuthoredBuildingCollisionResult(
    StaticBody3D Body,
    int SourceMeshCount,
    int StructuralSourceMeshCount,
    int DetailSourceMeshCount,
    int CollisionShapeCount,
    int ConcaveShapeCount,
    int SharedShapeCount,
    int BakedShapeCount,
    int UniqueMeshCount,
    long InstanceTriangleCount,
    IReadOnlyDictionary<string, int> AnchorShapeCounts);

/// <summary>Builds exact static collision from the DCC-authored old-city building shells.</summary>
internal sealed class JianghaiAuthoredBuildingCollisionBuilder
{
    public const string CollisionGroup = "jianghai_authored_building_collision";

    private const float MinimumHeight = 4.5f;
    private const float MinimumFootprint = 4.0f;
    private const float UniformScaleTolerance = 0.001f;
    private const int ExpectedStructuralSourceCount = 107;
    private const int ExpectedDetailSourceCount = 113;

    private static readonly string[] StructuralAnchorNames =
    {
        "JianghaiTenementDistrict",
        "RedStarElectronicsFactory",
        "GuangchangPawnshop",
        "OldCityMarketBridge"
    };

    private static readonly IReadOnlyDictionary<string, int> ExpectedAnchorShapeCounts
        = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["JianghaiTenementDistrict"] = 94,
            ["RedStarElectronicsFactory"] = 11,
            ["GuangchangPawnshop"] = 73,
            ["OldCityMarketBridge"] = 42
        };

    public JianghaiAuthoredBuildingCollisionResult Build(Node3D authoredRoot, Node3D parent)
    {
        ArgumentNullException.ThrowIfNull(authoredRoot);
        ArgumentNullException.ThrowIfNull(parent);

        var body = new StaticBody3D
        {
            Name = "JianghaiAuthoredBuildingCollision",
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup(CollisionGroup);
        parent.AddChild(body);

        var sharedShapes = new Dictionary<ulong, SharedCollisionShape>();
        var uniqueMeshes = new HashSet<ulong>();
        var anchorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sourceMeshCount = 0;
        var structuralSourceMeshCount = 0;
        var detailSourceMeshCount = 0;
        var concaveShapeCount = 0;
        var sharedShapeCount = 0;
        var bakedShapeCount = 0;
        long instanceTriangleCount = 0;

        try
        {
            foreach (var anchorName in StructuralAnchorNames)
            {
                var anchor = authoredRoot.FindChild(anchorName, recursive: true, owned: false) as Node3D
                    ?? throw new InvalidOperationException(
                        $"The authored building anchor '{anchorName}' is missing.");
                var anchorShapeCount = 0;
                var candidates = new List<MeshInstance3D>();
                var candidateIds = new HashSet<ulong>();
                var meshes = anchor.FindChildren("*", "MeshInstance3D", recursive: true, owned: false);
                using var meshesBacking = meshes.AsDisposable();
                foreach (var child in meshes)
                {
                    if (child is MeshInstance3D meshInstance
                        && (IsStructuralBuildingMesh(meshInstance)
                            || IsExplicitAuthoredCollisionMesh(anchorName, meshInstance.Name))
                        && candidateIds.Add(meshInstance.GetInstanceId()))
                    {
                        candidates.Add(meshInstance);
                    }
                }

                var authoredMeshes = authoredRoot.FindChildren(
                    "*", "MeshInstance3D", recursive: true, owned: false);
                using var authoredMeshesBacking = authoredMeshes.AsDisposable();
                foreach (var child in authoredMeshes)
                {
                    if (child is MeshInstance3D meshInstance
                        && IsExplicitAuthoredCollisionMesh(anchorName, meshInstance.Name)
                        && candidateIds.Add(meshInstance.GetInstanceId()))
                    {
                        candidates.Add(meshInstance);
                    }
                }

                foreach (var meshInstance in candidates)
                {
                    if (meshInstance.Mesh is not { } mesh)
                    {
                        continue;
                    }

                    var isStructural = IsStructuralBuildingMesh(meshInstance);

                    var meshId = mesh.GetInstanceId();
                    uniqueMeshes.Add(meshId);
                    var scale = meshInstance.GlobalTransform.Basis.Scale.Abs();
                    var isUniformScale = Mathf.Abs(scale.X - scale.Y) <= UniformScaleTolerance
                        && Mathf.Abs(scale.Y - scale.Z) <= UniformScaleTolerance;
                    ConcavePolygonShape3D shape;
                    Transform3D collisionTransform;
                    int triangleCount;
                    if (isUniformScale)
                    {
                        if (!sharedShapes.TryGetValue(meshId, out var cachedShape))
                        {
                            if (mesh.GetFaces() is not { Length: >= 3 } faces)
                            {
                                continue;
                            }
                            shape = CreateShape(faces);
                            triangleCount = faces.Length / 3;
                            sharedShapes[meshId] = new SharedCollisionShape(shape, triangleCount);
                        }
                        else
                        {
                            shape = cachedShape.Shape;
                            triangleCount = cachedShape.TriangleCount;
                            sharedShapeCount++;
                        }
                        collisionTransform = meshInstance.GlobalTransform;
                    }
                    else
                    {
                        if (mesh.GetFaces() is not { Length: >= 3 } faces)
                        {
                            continue;
                        }
                        var rigidTransform = new Transform3D(
                            meshInstance.GlobalBasis.Orthonormalized(),
                            meshInstance.GlobalPosition);
                        var rigidInverse = rigidTransform.AffineInverse();
                        var bakedFaces = new Vector3[faces.Length];
                        for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++)
                        {
                            bakedFaces[faceIndex] = rigidInverse * meshInstance.ToGlobal(faces[faceIndex]);
                        }
                        shape = CreateShape(bakedFaces);
                        triangleCount = faces.Length / 3;
                        collisionTransform = rigidTransform;
                        bakedShapeCount++;
                    }

                    var collision = new CollisionShape3D
                    {
                        Name = $"BuildingCollision_{sourceMeshCount + 1:000}",
                        Shape = shape
                    };
                    collision.SetMeta("authored_anchor", anchorName);
                    collision.SetMeta("authored_source_node", meshInstance.Name.ToString());
                    collision.SetMeta("authored_source_path", meshInstance.GetPath().ToString());
                    collision.SetMeta("authored_triangle_count", triangleCount);
                    collision.SetMeta("authored_collision_role", isStructural ? "structure" : "detail");
                    body.AddChild(collision);
                    collision.GlobalTransform = collisionTransform;

                    sourceMeshCount++;
                    if (isStructural)
                    {
                        structuralSourceMeshCount++;
                    }
                    else
                    {
                        detailSourceMeshCount++;
                    }
                    concaveShapeCount++;
                    anchorShapeCount++;
                    instanceTriangleCount += triangleCount;
                }
                anchorCounts[anchorName] = anchorShapeCount;
            }

            var anchorCountsReady = anchorCounts.Count == ExpectedAnchorShapeCounts.Count;
            foreach (var expected in ExpectedAnchorShapeCounts)
            {
                anchorCountsReady &= anchorCounts.TryGetValue(expected.Key, out var actual)
                    && actual == expected.Value;
            }
            if (structuralSourceMeshCount != ExpectedStructuralSourceCount
                || detailSourceMeshCount != ExpectedDetailSourceCount
                || !anchorCountsReady)
            {
                throw new InvalidOperationException(
                    "The authored old-city scene did not provide its complete structural and detail "
                    + $"collision contract (structural={structuralSourceMeshCount}/"
                    + $"{ExpectedStructuralSourceCount}, detail={detailSourceMeshCount}/"
                    + $"{ExpectedDetailSourceCount}).");
            }

            body.SetMeta("authored_source_mesh_count", sourceMeshCount);
            body.SetMeta("authored_unique_mesh_count", uniqueMeshes.Count);
            body.SetMeta("authored_instance_triangle_count", instanceTriangleCount);
            return new JianghaiAuthoredBuildingCollisionResult(
                body,
                sourceMeshCount,
                structuralSourceMeshCount,
                detailSourceMeshCount,
                sourceMeshCount,
                concaveShapeCount,
                sharedShapeCount,
                bakedShapeCount,
                uniqueMeshes.Count,
                instanceTriangleCount,
                anchorCounts);
        }
        catch
        {
            body.QueueFree();
            throw;
        }
    }

    private static bool IsStructuralBuildingMesh(MeshInstance3D meshInstance)
    {
        var localSize = meshInstance.GetAabb().Size;
        var scale = meshInstance.GlobalTransform.Basis.Scale.Abs();
        var worldSize = new Vector3(
            localSize.X * scale.X,
            localSize.Y * scale.Y,
            localSize.Z * scale.Z);
        return worldSize.Y >= MinimumHeight
            && Mathf.Min(worldSize.X, worldSize.Z) >= MinimumFootprint;
    }

    private static bool IsExplicitAuthoredCollisionMesh(
        string anchorName,
        StringName nodeName)
    {
        var name = nodeName.ToString();
        return anchorName switch
        {
            "RedStarElectronicsFactory" => name.StartsWith(
                "FactoryGatePortal_", StringComparison.Ordinal),
            "GuangchangPawnshop" => name.StartsWith(
                    "PawnshopAuthoredCanopy_", StringComparison.Ordinal)
                || (name.StartsWith("PawnshopAuthoredWing_", StringComparison.Ordinal)
                    && name.EndsWith("_Wall", StringComparison.Ordinal))
                || name.StartsWith("PawnshopNorthWall_", StringComparison.Ordinal)
                || name.StartsWith("PawnshopNorthWallCap_", StringComparison.Ordinal)
                || name.StartsWith("PawnshopWestWall_", StringComparison.Ordinal)
                || name.StartsWith("PawnshopWestWallCap_", StringComparison.Ordinal)
                || name.StartsWith("PawnshopEastWall_", StringComparison.Ordinal)
                || name.StartsWith("PawnshopEastWallCap_", StringComparison.Ordinal),
            "OldCityMarketBridge" => name.StartsWith("MarketRail_", StringComparison.Ordinal)
                || name.StartsWith("MarketRailPost_", StringComparison.Ordinal)
                || name is "MarketBridgeDeck" or "MarketEastRamp" or "MarketWestRamp",
            _ => false
        };
    }

    private static ConcavePolygonShape3D CreateShape(Vector3[] faces)
    {
        var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
        shape.SetFaces(faces);
        return shape;
    }

    private readonly record struct SharedCollisionShape(
        ConcavePolygonShape3D Shape,
        int TriangleCount);
}
