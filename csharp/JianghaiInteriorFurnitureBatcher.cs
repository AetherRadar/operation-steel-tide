using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Converts authored static-furniture visuals into short-range spatial MultiMesh batches.
/// Furniture collision remains on each room prop while render nodes are shared by mesh.
/// </summary>
internal sealed class JianghaiInteriorFurnitureBatcher
{
    public const float MaximumBatchRadius = 46.0f;
    public const float MaximumVisibilityRange = 88.0f;

    private const float BucketSize = 64.0f;
    private const string BatchGroup = "jianghai_interior_furniture_batch";

    private readonly Node3D _batchParent;
    private readonly Transform3D _parentInverse;
    private readonly List<FurnitureVisualCandidate> _candidates = new();

    public JianghaiInteriorFurnitureBatcher(Node3D batchParent)
    {
        ArgumentNullException.ThrowIfNull(batchParent);
        _batchParent = batchParent;
        _parentInverse = batchParent.GlobalTransform.AffineInverse();
    }

    public int SourceMeshCount { get; private set; }

    public void Capture(Node3D collisionBody, Node3D authoredVisual)
    {
        ArgumentNullException.ThrowIfNull(collisionBody);
        ArgumentNullException.ThrowIfNull(authoredVisual);

        var meshes = FindMeshes(authoredVisual);
        if (meshes.Count == 0 || meshes.Exists(HasSurfaceOverride))
        {
            return;
        }

        foreach (var meshInstance in meshes)
        {
            var parentTransform = _parentInverse * meshInstance.GlobalTransform;
            var origin = parentTransform.Origin;
            var key = new FurnitureBatchKey(
                meshInstance.Mesh!.GetInstanceId(),
                meshInstance.MaterialOverride?.GetInstanceId() ?? 0,
                meshInstance.Layers,
                new Vector2I(
                    Mathf.FloorToInt(origin.X / BucketSize),
                    Mathf.FloorToInt(origin.Z / BucketSize)));
            _candidates.Add(new FurnitureVisualCandidate(
                meshInstance.Mesh,
                meshInstance.MaterialOverride,
                parentTransform,
                key));
        }

        SourceMeshCount += meshes.Count;
        collisionBody.SetMeta("jianghai_static_furniture_batched", true);
        collisionBody.SetMeta("jianghai_static_furniture_mesh_count", meshes.Count);
        collisionBody.SetMeta("jianghai_static_furniture_collision_retained", true);
        collisionBody.RemoveChild(authoredVisual);
        authoredVisual.Free();
        CapturedPropCount++;
    }

    public void Build(JianghaiInteriorBuildResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.StaticFurniturePropCount = CapturedPropCount;
        var batchValidationReady = _candidates.Count > 0;
        var groups = new Dictionary<FurnitureBatchKey, List<FurnitureVisualCandidate>>();
        foreach (var candidate in _candidates)
        {
            if (!groups.TryGetValue(candidate.Key, out var group))
            {
                group = new List<FurnitureVisualCandidate>();
                groups[candidate.Key] = group;
            }
            group.Add(candidate);
        }

        var batchIndex = 0;
        foreach (var group in groups.Values)
        {
            var centroid = CalculateCentroid(group);
            var batchTransform = new Transform3D(Basis.Identity, centroid);
            var batchInverse = batchTransform.AffineInverse();
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = group[0].Mesh,
                InstanceCount = group.Count
            };
            var radius = 0.0f;
            for (var index = 0; index < group.Count; index++)
            {
                multiMesh.SetInstanceTransform(
                    index,
                    batchInverse * group[index].ParentTransform);
                radius = Mathf.Max(
                    radius,
                    group[index].ParentTransform.Origin.DistanceTo(centroid));
            }

            var batch = new MultiMeshInstance3D
            {
                Name = $"JianghaiInteriorFurnitureBatch_{++batchIndex:000}",
                Transform = batchTransform,
                Multimesh = multiMesh,
                Layers = group[0].Key.Layers,
                MaterialOverride = group[0].MaterialOverride,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = Mathf.Min(
                    MaximumVisibilityRange,
                    JianghaiInteriorPopulationService.InteriorVisibilityRange + radius),
                VisibilityRangeEndMargin = 6.0f,
                VisibilityRangeFadeMode =
                    GeometryInstance3D.VisibilityRangeFadeModeEnum.Self
            };
            batch.AddToGroup(BatchGroup);
            batch.SetMeta("jianghai_static_furniture_instance_count", group.Count);
            batch.SetMeta("jianghai_static_furniture_radius_m", radius);
            _batchParent.AddChild(batch);
            result.StaticFurnitureBatches.Add(batch);
            result.StaticFurnitureInstanceCount += group.Count;
            result.StaticFurnitureMaximumBatchRadius = Mathf.Max(
                result.StaticFurnitureMaximumBatchRadius,
                radius);
            result.StaticFurnitureMaximumVisibilityRange = Mathf.Max(
                result.StaticFurnitureMaximumVisibilityRange,
                batch.VisibilityRangeEnd);
            batchValidationReady &= multiMesh.Mesh?.GetInstanceId()
                    == group[0].Key.MeshId
                && (batch.MaterialOverride?.GetInstanceId() ?? 0)
                    == group[0].Key.MaterialId
                && batch.Layers == group[0].Key.Layers;
            for (var index = 0; index < group.Count; index++)
            {
                var reconstructed = batch.Transform
                    * multiMesh.GetInstanceTransform(index);
                result.StaticFurnitureMaximumPositionError = Mathf.Max(
                    result.StaticFurnitureMaximumPositionError,
                    reconstructed.Origin.DistanceTo(group[index].ParentTransform.Origin));
                result.StaticFurnitureMaximumBasisError = Mathf.Max(
                    result.StaticFurnitureMaximumBasisError,
                    BasisDistance(reconstructed.Basis, group[index].ParentTransform.Basis));
            }
        }
        result.StaticFurnitureBatchValidationReady = batchValidationReady
            && result.StaticFurnitureInstanceCount == _candidates.Count
            && result.StaticFurnitureMaximumBatchRadius <= MaximumBatchRadius
            && result.StaticFurnitureMaximumVisibilityRange <= MaximumVisibilityRange
            && result.StaticFurnitureMaximumPositionError <= 0.001f
            && result.StaticFurnitureMaximumBasisError <= 0.001f;
    }

    private int CapturedPropCount { get; set; }

    private static List<MeshInstance3D> FindMeshes(Node root)
    {
        var meshes = new List<MeshInstance3D>();
        CollectMeshes(root, meshes);
        return meshes;
    }

    private static void CollectMeshes(Node node, List<MeshInstance3D> meshes)
    {
        if (node is MeshInstance3D { Mesh: not null, Visible: true } mesh)
        {
            meshes.Add(mesh);
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                CollectMeshes(childNode, meshes);
            }
        }
    }

    private static bool HasSurfaceOverride(MeshInstance3D meshInstance)
    {
        for (var surface = 0; surface < meshInstance.Mesh!.GetSurfaceCount(); surface++)
        {
            if (meshInstance.GetSurfaceOverrideMaterial(surface) is not null)
            {
                return true;
            }
        }
        return false;
    }

    private static Vector3 CalculateCentroid(
        IReadOnlyList<FurnitureVisualCandidate> candidates)
    {
        var centroid = Vector3.Zero;
        foreach (var candidate in candidates)
        {
            centroid += candidate.ParentTransform.Origin;
        }
        return centroid / candidates.Count;
    }

    private static float BasisDistance(Basis first, Basis second)
        => Mathf.Max(
            first.X.DistanceTo(second.X),
            Mathf.Max(
                first.Y.DistanceTo(second.Y),
                first.Z.DistanceTo(second.Z)));

    private readonly record struct FurnitureBatchKey(
        ulong MeshId,
        ulong MaterialId,
        uint Layers,
        Vector2I Cell);

    private sealed record FurnitureVisualCandidate(
        Mesh Mesh,
        Material? MaterialOverride,
        Transform3D ParentTransform,
        FurnitureBatchKey Key);
}
