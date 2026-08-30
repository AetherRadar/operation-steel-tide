using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct JianghaiRenderQualityPolicy(
    float WorldDiagonal,
    float BaseVisibilityRange,
    bool IsDetail,
    bool IsFineDetail,
    bool AlwaysDisableShadow,
    GeometryInstance3D.ShadowCastingSetting AuthoredShadowSetting);

internal readonly record struct JianghaiRenderBatchValidation(
    bool Valid,
    int BatchCount,
    int SourceCount,
    int NonOriginBatchCount,
    float MaximumCentroidError,
    float MaximumPositionError,
    float MaximumBasisError,
    float MaximumBatchRadius,
    float MaximumVisibilityShortfall);

/// <summary>
/// Replaces safe repeated authored render instances with spatially culled MultiMesh batches.
/// Source nodes stay on render layer zero while gameplay proxies are built. Structural
/// diagnostics retain them; production releases safe leaves after the MultiMeshes take over.
/// </summary>
internal sealed partial class JianghaiAuthoredRenderBatcher
{
    public const int ExpectedBatchCount = 78;
    public const int ExpectedSourceCount = 292;
    public const int ExpectedProductionReleasedSourceCount = 290;
    public const int ExpectedProductionRetainedSourceCount = 2;

    private const float CityBucketSize = 192.0f;
    private const float ValleyBucketSize = 420.0f;
    private const string BatchedSourceMeta = "jianghai_render_batched_source";
    private const string SourceLayersMeta = "jianghai_render_source_layers";
    private const string BatchGroup = "jianghai_authored_render_batch";

    private readonly List<BatchedSource> _sources = new();
    private readonly List<RenderBatch> _batches = new();

    public int SourceCount => _sources.Count;
    public int BatchCount => _batches.Count;

    public void Rebuild(Node3D cityRoot)
    {
        ArgumentNullException.ThrowIfNull(cityRoot);
        Clear();

        var rootInverse = cityRoot.GlobalTransform.AffineInverse();
        var candidates = CollectCandidates(cityRoot, rootInverse);
        var groups = new Dictionary<BatchKey, List<BatchCandidate>>();
        foreach (var candidate in candidates)
        {
            if (!groups.TryGetValue(candidate.Key, out var group))
            {
                group = new List<BatchCandidate>();
                groups[candidate.Key] = group;
            }
            group.Add(candidate);
        }

        var batchIndex = 0;
        foreach (var group in groups.Values)
        {
            if (group.Count < 2 && !IsInteriorLiner(group[0].Source))
            {
                continue;
            }

            var representative = group[0];
            var centroid = CalculateCentroid(group);
            var batchRadius = CalculateRadius(group, centroid);
            var batchTransform = new Transform3D(Basis.Identity, centroid);
            var batchInverse = batchTransform.AffineInverse();
            var expectedRootTransforms = new Transform3D[group.Count];
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = representative.Source.Mesh,
                InstanceCount = group.Count
            };
            for (var index = 0; index < group.Count; index++)
            {
                expectedRootTransforms[index] = group[index].RootTransform;
                multiMesh.SetInstanceTransform(
                    index,
                    batchInverse * expectedRootTransforms[index]);
            }

            var batch = new MultiMeshInstance3D
            {
                Name = $"JianghaiAuthoredRenderBatch_{++batchIndex:000}",
                Multimesh = multiMesh,
                Transform = batchTransform,
                Layers = representative.Source.Layers,
                CastShadow = representative.Policy.AuthoredShadowSetting
            };
            batch.AddToGroup(BatchGroup);
            batch.SetMeta("jianghai_batch_source_count", group.Count);
            batch.SetMeta("jianghai_batch_centroid", centroid);
            cityRoot.AddChild(batch);
            _batches.Add(new RenderBatch(
                batch,
                representative.Policy,
                expectedRootTransforms,
                batchRadius));

            foreach (var candidate in group)
            {
                var sourceLayers = candidate.Source.Layers;
                candidate.Source.SetMeta(BatchedSourceMeta, true);
                candidate.Source.SetMeta(SourceLayersMeta, sourceLayers);
                candidate.Source.Layers = 0;
                _sources.Add(new BatchedSource(candidate.Source, sourceLayers));
            }
        }

        cityRoot.SetMeta("jianghai_render_batch_count", BatchCount);
        cityRoot.SetMeta("jianghai_render_batched_source_count", SourceCount);
    }

    public JianghaiRenderBatchValidation ValidateBatches()
    {
        var instanceCountsReady = true;
        var nonOriginBatchCount = 0;
        var maximumCentroidError = 0.0f;
        var maximumPositionError = 0.0f;
        var maximumBasisError = 0.0f;
        var maximumBatchRadius = 0.0f;
        var maximumVisibilityShortfall = 0.0f;
        foreach (var batch in _batches)
        {
            if (!GodotObject.IsInstanceValid(batch.Instance)
                || batch.Instance.Multimesh is not { } multiMesh
                || multiMesh.InstanceCount != batch.ExpectedRootTransforms.Length)
            {
                instanceCountsReady = false;
                continue;
            }

            var centroid = CalculateCentroid(batch.ExpectedRootTransforms);
            var batchOrigin = batch.Instance.Transform.Origin;
            maximumCentroidError = Mathf.Max(
                maximumCentroidError,
                batchOrigin.DistanceTo(centroid));
            if (batchOrigin.LengthSquared() > 0.0001f)
            {
                nonOriginBatchCount++;
            }

            for (var index = 0; index < batch.ExpectedRootTransforms.Length; index++)
            {
                var expected = batch.ExpectedRootTransforms[index];
                var reconstructed = batch.Instance.Transform
                    * multiMesh.GetInstanceTransform(index);
                maximumPositionError = Mathf.Max(
                    maximumPositionError,
                    reconstructed.Origin.DistanceTo(expected.Origin));
                maximumBasisError = Mathf.Max(
                    maximumBasisError,
                    BasisDistance(reconstructed.Basis, expected.Basis));
                maximumBatchRadius = Mathf.Max(
                    maximumBatchRadius,
                    expected.Origin.DistanceTo(centroid));
            }
            maximumVisibilityShortfall = Mathf.Max(
                maximumVisibilityShortfall,
                batch.RequiredVisibilityRangeEnd - batch.Instance.VisibilityRangeEnd);
        }

        var valid = BatchCount > 0
            && SourceCount > BatchCount
            && nonOriginBatchCount > 0
            && instanceCountsReady
            && maximumCentroidError <= 0.001f
            && maximumPositionError <= 0.001f
            && maximumBasisError <= 0.001f
            && maximumVisibilityShortfall <= 0.001f;
        return new JianghaiRenderBatchValidation(
            valid,
            BatchCount,
            SourceCount,
            nonOriginBatchCount,
            maximumCentroidError,
            maximumPositionError,
            maximumBasisError,
            maximumBatchRadius,
            maximumVisibilityShortfall);
    }

    public bool IsBatchedSource(MeshInstance3D source)
        => source.HasMeta(BatchedSourceMeta) && source.GetMeta(BatchedSourceMeta).AsBool();

    public int ApplyQuality(int qualityTier)
    {
        var normalizedTier = Mathf.Clamp(qualityTier, 0, 2);
        var distanceScale = normalizedTier switch
        {
            0 => 0.68f,
            1 => 0.84f,
            _ => 1.0f
        };
        var shadowCasterSourceCount = 0;
        foreach (var batch in _batches)
        {
            if (!GodotObject.IsInstanceValid(batch.Instance))
            {
                continue;
            }

            var endDistance = batch.Policy.BaseVisibilityRange * distanceScale
                + batch.BatchRadius;
            batch.RequiredVisibilityRangeEnd = endDistance;
            batch.Instance.VisibilityRangeEnd = endDistance;
            batch.Instance.VisibilityRangeEndMargin = Mathf.Min(28.0f, endDistance * 0.12f);
            batch.Instance.VisibilityRangeFadeMode =
                GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
            var allowShadow = normalizedTier switch
            {
                0 => !batch.Policy.IsDetail,
                1 => !batch.Policy.IsFineDetail && batch.Policy.WorldDiagonal > 4.0f,
                _ => !batch.Policy.IsFineDetail
            };
            batch.Instance.CastShadow = batch.Policy.AlwaysDisableShadow || !allowShadow
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : batch.Policy.AuthoredShadowSetting;
            if (batch.Instance.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off)
            {
                shadowCasterSourceCount += batch.ExpectedRootTransforms.Length;
            }
        }
        return shadowCasterSourceCount;
    }

    public void Clear()
    {
        foreach (var source in _sources)
        {
            if (!GodotObject.IsInstanceValid(source.Instance))
            {
                continue;
            }
            source.Instance.Layers = source.Layers;
            source.Instance.RemoveMeta(BatchedSourceMeta);
            source.Instance.RemoveMeta(SourceLayersMeta);
        }
        foreach (var batch in _batches)
        {
            if (GodotObject.IsInstanceValid(batch.Instance))
            {
                batch.Instance.QueueFree();
            }
        }
        _sources.Clear();
        _batches.Clear();
    }

    public static JianghaiRenderQualityPolicy CreateQualityPolicy(
        MeshInstance3D meshInstance)
    {
        var localSize = meshInstance.GetAabb().Size;
        var globalScale = meshInstance.GlobalTransform.Basis.Scale.Abs();
        var worldSize = new Vector3(
            localSize.X * globalScale.X,
            localSize.Y * globalScale.Y,
            localSize.Z * globalScale.Z);
        var diagonal = worldSize.Length();
        var name = meshInstance.Name.ToString();
        var isInteriorLiner = IsInteriorLiner(meshInstance);
        var isValleyEnvironment = ContainsAny(
            name,
            "OldCityFoundation",
            "JianghaiPerimeterGround",
            "JianghaiMountainMassif");
        var baseVisibilityRange = isInteriorLiner
            ? JianghaiInteriorShellValidator.RequiredVisibilityRange
            : isValleyEnvironment ? 1200.0f : diagonal switch
        {
            <= 1.2f => 105.0f,
            <= 4.0f => 180.0f,
            <= 12.0f => 285.0f,
            _ => 460.0f
        };
        var isFineDetail = diagonal <= 1.2f
            || ContainsAny(name, "Screen", "Indicator", "Fastener", "Text", "Cable", "Lens");
        var isDetail = isInteriorLiner
            || diagonal <= 12.0f
            || ContainsAny(
                name,
                "Aircon",
                "Rollershutter",
                "Trashbag",
                "UtilityBox",
                "Barrel",
                "Crate",
                "SecurityCamera",
                "Television");
        var alwaysDisableShadow = isInteriorLiner
            || isValleyEnvironment
            || diagonal <= 0.45f
            || ContainsAny(name, "ScreenTrace", "StatusScreen", "Indicator", "Fastener", "Text");
        return new JianghaiRenderQualityPolicy(
            diagonal,
            baseVisibilityRange,
            isDetail,
            isFineDetail,
            alwaysDisableShadow,
            meshInstance.CastShadow);
    }

    private static List<BatchCandidate> CollectCandidates(
        Node3D cityRoot,
        Transform3D rootInverse)
    {
        var result = new List<BatchCandidate>();
        var nodes = cityRoot.FindChildren("*", "MeshInstance3D", recursive: true, owned: false);
        using var nodesBacking = nodes.AsDisposable();
        foreach (var node in nodes)
        {
            if (node is not MeshInstance3D { Mesh: { } mesh } source
                || !CanBatch(source, mesh))
            {
                continue;
            }

            var rootTransform = rootInverse * source.GlobalTransform;
            var policy = CreateQualityPolicy(source);
            var bucketSize = policy.BaseVisibilityRange >= 1200.0f
                ? ValleyBucketSize
                : CityBucketSize;
            var origin = rootTransform.Origin;
            var cell = new Vector3I(
                Mathf.FloorToInt(origin.X / bucketSize),
                Mathf.FloorToInt(origin.Y / bucketSize),
                Mathf.FloorToInt(origin.Z / bucketSize));
            var key = new BatchKey(
                mesh.GetInstanceId(),
                cell,
                source.Layers,
                Mathf.RoundToInt(policy.BaseVisibilityRange),
                policy.IsDetail,
                policy.IsFineDetail,
                policy.AlwaysDisableShadow,
                policy.AuthoredShadowSetting);
            result.Add(new BatchCandidate(source, rootTransform, policy, key));
        }
        return result;
    }

    private static Vector3 CalculateCentroid(IReadOnlyList<BatchCandidate> group)
    {
        var centroid = Vector3.Zero;
        foreach (var candidate in group)
        {
            centroid += candidate.RootTransform.Origin;
        }
        return centroid / group.Count;
    }

    private static Vector3 CalculateCentroid(IReadOnlyList<Transform3D> transforms)
    {
        var centroid = Vector3.Zero;
        foreach (var transform in transforms)
        {
            centroid += transform.Origin;
        }
        return centroid / transforms.Count;
    }

    private static float CalculateRadius(
        IReadOnlyList<BatchCandidate> group,
        Vector3 centroid)
    {
        var radius = 0.0f;
        foreach (var candidate in group)
        {
            radius = Mathf.Max(
                radius,
                candidate.RootTransform.Origin.DistanceTo(centroid));
        }
        return radius;
    }

    private static float BasisDistance(Basis left, Basis right)
        => Mathf.Max(
            left.X.DistanceTo(right.X),
            Mathf.Max(
                left.Y.DistanceTo(right.Y),
                left.Z.DistanceTo(right.Z)));

    private static bool CanBatch(MeshInstance3D source, Mesh mesh)
    {
        if (!source.Visible
            || source.Layers == 0
            || source.GetChildCount() != 0
            || source.MaterialOverride is not null
            || IsTerminalVisual(source)
            || source.Name.ToString().Contains("StatusScreen", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            if (source.GetSurfaceOverrideMaterial(surface) is not null)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsTerminalVisual(Node node)
    {
        for (var current = node; current is not null; current = current.GetParent())
        {
            var name = current.Name.ToString();
            if (name is "GrandHotelSecurityTerminalVisual"
                or "MunicipalTreasuryManifestTerminalVisual")
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInteriorLiner(MeshInstance3D meshInstance)
        => meshInstance.Name.ToString().StartsWith(
                "JianghaiInteriorShell_",
                StringComparison.OrdinalIgnoreCase)
            || meshInstance.GetMeta("jianghai_interior_liner", false).AsBool();

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private readonly record struct BatchKey(
        ulong MeshId,
        Vector3I Cell,
        uint Layers,
        int VisibilityRange,
        bool IsDetail,
        bool IsFineDetail,
        bool AlwaysDisableShadow,
        GeometryInstance3D.ShadowCastingSetting ShadowSetting);

    private sealed record BatchCandidate(
        MeshInstance3D Source,
        Transform3D RootTransform,
        JianghaiRenderQualityPolicy Policy,
        BatchKey Key);

    private sealed record BatchedSource(MeshInstance3D Instance, uint Layers);

    private sealed class RenderBatch
    {
        public MultiMeshInstance3D Instance { get; }
        public JianghaiRenderQualityPolicy Policy { get; }
        public Transform3D[] ExpectedRootTransforms { get; }
        public float BatchRadius { get; }
        public float RequiredVisibilityRangeEnd { get; set; }

        public RenderBatch(
            MultiMeshInstance3D instance,
            JianghaiRenderQualityPolicy policy,
            Transform3D[] expectedRootTransforms,
            float batchRadius)
        {
            Instance = instance;
            Policy = policy;
            ExpectedRootTransforms = expectedRootTransforms;
            BatchRadius = batchRadius;
        }
    }
}
