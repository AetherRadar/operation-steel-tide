using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record JianghaiOldCityValleyInspection(
    int FoundationMeshCount,
    long FoundationTriangleCount,
    int MountainMeshCount,
    long InstanceTriangleCount,
    Aabb WorldBounds,
    Aabb FoundationWorldBounds,
    Aabb MountainWorldBounds,
    int ExpectedMountainNameCount,
    int UniqueMountainMeshCount,
    int CollisionNodeCount,
    bool HierarchyReady,
    bool MountainsOutsidePlayableBounds,
    float MountainMaxAngularGapRadians,
    int FoundationVertexCount,
    float FoundationMinHeight,
    float FoundationMaxHeight,
    bool FoundationGeometryReady,
    bool FoundationMaterialsReady,
    bool FoundationUvReady);

/// <summary>Validates the authored foundation, perimeter ground scans, and mountain massif ring.</summary>
internal static class JianghaiOldCityValleyInspector
{
    private const string ValleyRootName = "JianghaiValleyEnvironment";
    private const string AuthoredSceneRootName = "JianghaiOldCityAuthoredScene";
    private const string FoundationName = "OldCityFoundation";
    private const string PerimeterGroundPrefix = "JianghaiPerimeterGround";
    private const string PerimeterGroundName = "JianghaiPerimeterGroundComposite";
    private const string MountainPrefix = "JianghaiMountainMassif";
    private const string LegacyTerrainName = "JianghaiValleyTerrain";
    private const string LegacyMountainPrefix = "JianghaiBackdrop_Cliff_";
    private const string LegacyPhotogrammetryMountainPrefix = "JianghaiMountainCliff";
    private const int ExpectedPerimeterGroundCount = 1;
    private const int ExpectedPerimeterGroundTriangleCount = 168_480;
    private const int ExpectedPerimeterGroundVertexCount = 84_960;
    private const int ExpectedMountainCount = 12;
    private const int ExpectedMountainFamilyCount = 1;
    private const int ExpectedMountainFamilyInstanceCount = 12;
    private const int ExpectedFoundationTriangleCount = 188;
    private const int ExpectedFoundationVertexCount = 112;
    private const float MinimumPerimeterGroundTopMeters = 4.5f;
    private const float MaximumPerimeterGroundTopMeters = 5.5f;
    private const float MinimumPerimeterGroundReliefMeters = 17.0f;
    private const float MaximumPerimeterGroundReliefMeters = 18.5f;
    private const float MaximumMountainAngularGapRadians = 0.55f;

    private static readonly HashSet<string> ExpectedMountainNames = CreateExpectedMountainNames();
    private static readonly string[] ExpectedMountainFamilies =
    {
        "hero_mountain"
    };

    public static JianghaiOldCityValleyInspection Inspect(Node3D cityRoot)
    {
        ArgumentNullException.ThrowIfNull(cityRoot);

        var state = new InspectionState(cityRoot);
        InspectNode(cityRoot, state, containingValleyRoot: null);
        var mountainsOutside = ValidateMountainPlacement(
            state,
            out var maxAngularGapRadians);
        var hierarchyReady = state.ValleyRootCount == 1
            && state.FoundationMeshCount == 1
            && state.PerimeterGroundMeshCount == ExpectedPerimeterGroundCount
            && state.PerimeterGroundNames.Count == ExpectedPerimeterGroundCount
            && state.PerimeterGroundMeshResourceIds.Count == 1
            && state.PerimeterGroundTriangleCount == ExpectedPerimeterGroundTriangleCount
            && state.PerimeterGroundVertexCount == ExpectedPerimeterGroundVertexCount
            && state.PerimeterGroundIdentityReady
            && state.PerimeterGroundSurfaceReady
            && state.PerimeterGroundElevationReady
            && state.PerimeterGroundWorldBounds.Size.X >= 1_190.0f
            && state.PerimeterGroundWorldBounds.Size.Z >= 1_190.0f
            && state.MountainMeshCount == ExpectedMountainCount
            && state.MountainNames.Count == ExpectedMountainCount
            && state.MountainMeshResourceIds.Count == ExpectedMountainFamilyCount
            && AllInstanceCountsMatch(
                state.MountainMeshInstanceCounts,
                ExpectedMountainFamilyInstanceCount)
            && HasExpectedMountainFamilies(state.MountainFamilyCounts)
            && state.InvalidHierarchyCount == 0;
        var foundationGeometryReady = state.FoundationTriangleCount
                == ExpectedFoundationTriangleCount
            && state.FoundationVertices.Count == ExpectedFoundationVertexCount;
        return new JianghaiOldCityValleyInspection(
            state.FoundationMeshCount,
            state.FoundationTriangleCount,
            state.MountainMeshCount,
            state.InstanceTriangleCount,
            state.WorldBounds,
            state.FoundationWorldBounds,
            state.MountainWorldBounds,
            ExpectedMountainCount,
            state.MountainMeshResourceIds.Count,
            state.CollisionNodeCount,
            hierarchyReady,
            mountainsOutside,
            maxAngularGapRadians,
            state.FoundationVertices.Count,
            state.FoundationWorldBounds.Position.Y,
            state.FoundationWorldBounds.Position.Y + state.FoundationWorldBounds.Size.Y,
            foundationGeometryReady,
            state.FoundationMaterialsReady,
            state.FoundationUvReady);
    }

    private static void InspectNode(
        Node node,
        InspectionState state,
        Node3D? containingValleyRoot)
    {
        var nodeName = node.Name.ToString();
        var activeValleyRoot = containingValleyRoot;
        if (string.Equals(nodeName, ValleyRootName, StringComparison.Ordinal))
        {
            state.ValleyRootCount++;
            if (node is not Node3D valleyRoot
                || containingValleyRoot is not null
                || !IsAuthoredSceneParent(node.GetParent(), state.CityRoot))
            {
                state.InvalidHierarchyCount++;
            }
            else
            {
                activeValleyRoot = valleyRoot;
            }
        }

        if (activeValleyRoot is not null
            && node is CollisionObject3D or CollisionShape3D)
        {
            state.CollisionNodeCount++;
        }

        if (node is MeshInstance3D { Mesh: { } mesh } meshInstance)
        {
            var isFoundation = string.Equals(nodeName, FoundationName, StringComparison.Ordinal);
            var hasPerimeterGroundPrefix = nodeName.StartsWith(
                PerimeterGroundPrefix,
                StringComparison.Ordinal);
            var hasMountainPrefix = nodeName.StartsWith(MountainPrefix, StringComparison.Ordinal);
            var isLegacyValleyMesh = string.Equals(
                    nodeName,
                    LegacyTerrainName,
                    StringComparison.Ordinal)
                || nodeName.StartsWith(LegacyMountainPrefix, StringComparison.Ordinal)
                || nodeName.StartsWith(
                    LegacyPhotogrammetryMountainPrefix,
                    StringComparison.Ordinal)
                || MeshOrMaterialIdentityContains(
                    meshInstance,
                    mesh,
                    "Mountainside",
                    "JianghaiCoastalCliff",
                    "JianghaiNamaqualandCliff");
            if (isLegacyValleyMesh)
            {
                state.InvalidHierarchyCount++;
            }
            else if (isFoundation)
            {
                InspectFoundation(meshInstance, mesh, state, activeValleyRoot);
            }
            else if (hasPerimeterGroundPrefix)
            {
                InspectPerimeterGround(meshInstance, mesh, state, activeValleyRoot);
            }
            else if (hasMountainPrefix)
            {
                InspectMountain(meshInstance, mesh, state, activeValleyRoot);
            }
            else if (activeValleyRoot is not null)
            {
                state.InvalidHierarchyCount++;
            }
        }

        var children = node.GetChildren();
        using var childrenBacking = children.AsDisposable();
        foreach (var child in children)
        {
            if (child is Node childNode)
            {
                InspectNode(childNode, state, activeValleyRoot);
            }
        }
    }

    private static void InspectFoundation(
        MeshInstance3D meshInstance,
        Mesh mesh,
        InspectionState state,
        Node3D? valleyRoot)
    {
        state.FoundationMeshCount++;
        if (valleyRoot is null
            || meshInstance.GetParent() != valleyRoot
            || !meshInstance.Visible)
        {
            state.InvalidHierarchyCount++;
        }

        var metrics = InspectMesh(meshInstance, mesh, includeVertices: true);
        state.FoundationTriangleCount += metrics.TriangleCount;
        state.InstanceTriangleCount += metrics.TriangleCount;
        state.IncludeWorldBounds(metrics.WorldBounds);
        state.IncludeFoundationBounds(metrics.WorldBounds);
        foreach (var vertex in metrics.UniqueVertices)
        {
            state.FoundationVertices.Add(vertex);
        }
        state.FoundationMaterialsReady = metrics.HasGravelMaterial
            && metrics.HasRockyMaterial;
        state.FoundationUvReady = metrics.HasCompleteUvLayout;
    }

    private static bool IsAuthoredSceneParent(Node? parent, Node3D importedRoot)
        => parent == importedRoot
            || parent is not null
                && parent.GetParent() == importedRoot
                && string.Equals(
                    parent.Name.ToString(),
                    AuthoredSceneRootName,
                    StringComparison.Ordinal);

    private static void InspectPerimeterGround(
        MeshInstance3D meshInstance,
        Mesh mesh,
        InspectionState state,
        Node3D? valleyRoot)
    {
        var nodeName = meshInstance.Name.ToString();
        state.PerimeterGroundMeshCount++;
        if (!string.Equals(nodeName, PerimeterGroundName, StringComparison.Ordinal)
            || !state.PerimeterGroundNames.Add(nodeName)
            || valleyRoot is null
            || meshInstance.GetParent() != valleyRoot
            || !meshInstance.Visible)
        {
            state.InvalidHierarchyCount++;
        }

        var metrics = InspectMesh(meshInstance, mesh, includeVertices: false);
        state.InstanceTriangleCount += metrics.TriangleCount;
        state.PerimeterGroundTriangleCount += metrics.TriangleCount;
        state.PerimeterGroundVertexCount += metrics.VertexCount;
        state.PerimeterGroundMeshResourceIds.Add(mesh.GetInstanceId());
        state.PerimeterGroundIdentityReady &= MeshOrMaterialIdentityContains(
            meshInstance,
            mesh,
            "JianghaiCoastLine01",
            "coast_line_01");
        state.PerimeterGroundSurfaceReady &= metrics.HasGravelMaterial
            && !metrics.HasRockyMaterial
            && metrics.HasCompleteUvLayout;
        state.PerimeterGroundElevationReady &= metrics.WorldBounds.End.Y
                >= MinimumPerimeterGroundTopMeters
            && metrics.WorldBounds.End.Y <= MaximumPerimeterGroundTopMeters
            && metrics.WorldBounds.Size.Y >= MinimumPerimeterGroundReliefMeters
            && metrics.WorldBounds.Size.Y <= MaximumPerimeterGroundReliefMeters;
        state.IncludeWorldBounds(metrics.WorldBounds);
        state.IncludePerimeterGroundBounds(metrics.WorldBounds);
    }

    private static void InspectMountain(
        MeshInstance3D meshInstance,
        Mesh mesh,
        InspectionState state,
        Node3D? valleyRoot)
    {
        var nodeName = meshInstance.Name.ToString();
        state.MountainMeshCount++;
        if (!ExpectedMountainNames.Contains(nodeName)
            || !state.MountainNames.Add(nodeName)
            || valleyRoot is null
            || meshInstance.GetParent() != valleyRoot
            || !meshInstance.Visible)
        {
            state.InvalidHierarchyCount++;
        }

        var metrics = InspectMesh(meshInstance, mesh, includeVertices: false);
        state.InstanceTriangleCount += metrics.TriangleCount;
        var meshResourceId = mesh.GetInstanceId();
        state.MountainMeshResourceIds.Add(meshResourceId);
        IncrementCount(state.MountainMeshInstanceCounts, meshResourceId);
        var family = IdentifyMountainFamily(meshInstance, mesh);
        if (family is null)
        {
            state.InvalidHierarchyCount++;
        }
        else
        {
            IncrementCount(state.MountainFamilyCounts, family);
        }
        state.MountainBounds.Add(metrics.WorldBounds);
        state.IncludeWorldBounds(metrics.WorldBounds);
        state.IncludeMountainBounds(metrics.WorldBounds);
    }

    private static string? IdentifyMountainFamily(MeshInstance3D meshInstance, Mesh mesh)
    {
        if (MeshOrMaterialIdentityContains(
                meshInstance,
                mesh,
                "JianghaiHeroMountain",
                "hero_mountain"))
        {
            return "hero_mountain";
        }
        return null;
    }

    private static bool MeshOrMaterialIdentityContains(
        MeshInstance3D meshInstance,
        Mesh mesh,
        params string[] fragments)
    {
        if (ResourceIdentityContains(mesh, fragments))
        {
            return true;
        }

        for (var surfaceIndex = 0; surfaceIndex < mesh.GetSurfaceCount(); surfaceIndex++)
        {
            var material = meshInstance.GetActiveMaterial(surfaceIndex)
                ?? mesh.SurfaceGetMaterial(surfaceIndex);
            if (MaterialIdentityContains(material, fragments))
            {
                return true;
            }
        }
        return false;
    }

    private static MeshMetrics InspectMesh(
        MeshInstance3D meshInstance,
        Mesh mesh,
        bool includeVertices)
    {
        var triangleCount = 0L;
        var vertexCount = 0;
        var hasGravelMaterial = false;
        var hasRockyMaterial = false;
        var hasCompleteUvLayout = mesh is ArrayMesh;
        var uniqueVertices = new HashSet<QuantizedVertex>();
        for (var surfaceIndex = 0; surfaceIndex < mesh.GetSurfaceCount(); surfaceIndex++)
        {
            if (mesh is ArrayMesh arrayMesh)
            {
                var indexCount = arrayMesh.SurfaceGetArrayIndexLen(surfaceIndex);
                var surfaceVertexCount = arrayMesh.SurfaceGetArrayLen(surfaceIndex);
                triangleCount += (indexCount > 0 ? indexCount : surfaceVertexCount) / 3;
                using var arrays = arrayMesh.SurfaceGetArrays(surfaceIndex);
                var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var textureCoordinates = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                vertexCount += vertices.Length;
                hasCompleteUvLayout &= vertices.Length > 0
                    && textureCoordinates.Length == vertices.Length;
                if (includeVertices)
                {
                    foreach (var vertex in vertices)
                    {
                        uniqueVertices.Add(QuantizedVertex.From(vertex));
                    }
                }
            }
            else
            {
                hasCompleteUvLayout = false;
            }

            var material = meshInstance.GetActiveMaterial(surfaceIndex)
                ?? mesh.SurfaceGetMaterial(surfaceIndex);
            hasGravelMaterial |= MaterialIdentityContains(
                material,
                "CompactedGround",
                "GravelFloor03",
                "JianghaiCoastGravelFloorPBR",
                "gravel_floor_03");
            hasRockyMaterial |= MaterialIdentityContains(
                material,
                "RockyValley",
                "RockyTerrain");
        }

        return new MeshMetrics(
            triangleCount,
            vertexCount,
            CalculateWorldBounds(meshInstance),
            uniqueVertices,
            hasGravelMaterial,
            hasRockyMaterial,
            hasCompleteUvLayout);
    }

    private static bool MaterialIdentityContains(Material? material, params string[] fragments)
    {
        if (material is null)
        {
            return false;
        }
        if (ResourceIdentityContains(material, fragments))
        {
            return true;
        }
        if (material is not BaseMaterial3D baseMaterial)
        {
            return false;
        }

        return ResourceIdentityContains(baseMaterial.AlbedoTexture, fragments)
            || ResourceIdentityContains(baseMaterial.RoughnessTexture, fragments)
            || ResourceIdentityContains(baseMaterial.NormalTexture, fragments);
    }

    private static bool ResourceIdentityContains(Resource? resource, IReadOnlyList<string> fragments)
    {
        if (resource is null)
        {
            return false;
        }

        var resourceName = resource.ResourceName.ToString();
        var resourcePath = resource.ResourcePath;
        foreach (var fragment in fragments)
        {
            if (resourceName.Contains(fragment, StringComparison.OrdinalIgnoreCase)
                || resourcePath.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ValidateMountainPlacement(
        InspectionState state,
        out float maxAngularGapRadians)
    {
        maxAngularGapRadians = float.PositiveInfinity;
        if (state.MountainBounds.Count != ExpectedMountainCount
            || state.MountainNames.Count != ExpectedMountainCount)
        {
            return false;
        }

        var angles = new List<float>(state.MountainBounds.Count);
        foreach (var bounds in state.MountainBounds)
        {
            var minimum = bounds.Position;
            var maximum = bounds.Position + bounds.Size;
            var outsidePlayableArea = maximum.X < -170.0f
                || minimum.X > 170.0f
                || maximum.Z < -220.0f
                || minimum.Z > 100.0f;
            if (!outsidePlayableArea)
            {
                return false;
            }

            var center = bounds.Position + bounds.Size * 0.5f;
            var angle = Mathf.Atan2(center.Z + 60.0f, center.X);
            angles.Add(angle < 0.0f ? angle + Mathf.Tau : angle);
        }

        angles.Sort();
        maxAngularGapRadians = 0.0f;
        for (var index = 0; index < angles.Count; index++)
        {
            var next = angles[(index + 1) % angles.Count];
            var gap = next - angles[index];
            if (gap < 0.0f)
            {
                gap += Mathf.Tau;
            }
            maxAngularGapRadians = Mathf.Max(maxAngularGapRadians, gap);
        }
        return maxAngularGapRadians <= MaximumMountainAngularGapRadians;
    }

    private static Aabb CalculateWorldBounds(MeshInstance3D meshInstance)
    {
        var localBounds = meshInstance.GetAabb();
        var initialized = false;
        var minimum = Vector3.Zero;
        var maximum = Vector3.Zero;
        for (var x = 0; x <= 1; x++)
        {
            for (var y = 0; y <= 1; y++)
            {
                for (var z = 0; z <= 1; z++)
                {
                    var corner = localBounds.Position + new Vector3(
                        localBounds.Size.X * x,
                        localBounds.Size.Y * y,
                        localBounds.Size.Z * z);
                    var worldCorner = meshInstance.GlobalTransform * corner;
                    if (!initialized)
                    {
                        minimum = worldCorner;
                        maximum = worldCorner;
                        initialized = true;
                    }
                    else
                    {
                        minimum = minimum.Min(worldCorner);
                        maximum = maximum.Max(worldCorner);
                    }
                }
            }
        }
        return new Aabb(minimum, maximum - minimum);
    }

    private static bool HasExpectedMountainFamilies(
        IReadOnlyDictionary<string, int> familyCounts)
    {
        if (familyCounts.Count != ExpectedMountainFamilyCount)
        {
            return false;
        }
        foreach (var family in ExpectedMountainFamilies)
        {
            if (!familyCounts.TryGetValue(family, out var instanceCount)
                || instanceCount != ExpectedMountainFamilyInstanceCount)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllInstanceCountsMatch(
        IReadOnlyDictionary<ulong, int> instanceCounts,
        int expectedCount)
    {
        if (instanceCounts.Count == 0)
        {
            return false;
        }
        foreach (var instanceCount in instanceCounts.Values)
        {
            if (instanceCount != expectedCount)
            {
                return false;
            }
        }
        return true;
    }

    private static void IncrementCount<TKey>(Dictionary<TKey, int> counts, TKey key)
        where TKey : notnull
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private static HashSet<string> CreateExpectedNames(string prefix, int count)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            names.Add($"{prefix}{index:00}");
        }
        return names;
    }

    private static HashSet<string> CreateExpectedMountainNames()
        => CreateExpectedNames(MountainPrefix, ExpectedMountainCount);

    private sealed class InspectionState
    {
        public InspectionState(Node3D cityRoot)
        {
            CityRoot = cityRoot;
        }

        public Node3D CityRoot { get; }
        public int ValleyRootCount;
        public int FoundationMeshCount;
        public long FoundationTriangleCount;
        public int PerimeterGroundMeshCount;
        public long PerimeterGroundTriangleCount;
        public int PerimeterGroundVertexCount;
        public int MountainMeshCount;
        public long InstanceTriangleCount;
        public int CollisionNodeCount;
        public int InvalidHierarchyCount;
        public Aabb WorldBounds;
        public Aabb FoundationWorldBounds;
        public Aabb PerimeterGroundWorldBounds;
        public Aabb MountainWorldBounds;
        public bool FoundationMaterialsReady;
        public bool FoundationUvReady;
        public bool PerimeterGroundIdentityReady = true;
        public bool PerimeterGroundSurfaceReady = true;
        public bool PerimeterGroundElevationReady = true;
        private bool _worldBoundsInitialized;
        private bool _foundationBoundsInitialized;
        private bool _perimeterGroundBoundsInitialized;
        private bool _mountainBoundsInitialized;
        public HashSet<string> PerimeterGroundNames { get; } = new(StringComparer.Ordinal);
        public HashSet<string> MountainNames { get; } = new(StringComparer.Ordinal);
        public HashSet<ulong> PerimeterGroundMeshResourceIds { get; } = new();
        public HashSet<ulong> MountainMeshResourceIds { get; } = new();
        public Dictionary<ulong, int> MountainMeshInstanceCounts { get; } = new();
        public Dictionary<string, int> MountainFamilyCounts { get; } =
            new(StringComparer.Ordinal);
        public HashSet<QuantizedVertex> FoundationVertices { get; } = new();
        public List<Aabb> MountainBounds { get; } = new();

        public void IncludeWorldBounds(Aabb bounds)
            => IncludeBounds(ref WorldBounds, ref _worldBoundsInitialized, bounds);

        public void IncludeFoundationBounds(Aabb bounds)
            => IncludeBounds(ref FoundationWorldBounds, ref _foundationBoundsInitialized, bounds);

        public void IncludePerimeterGroundBounds(Aabb bounds)
            => IncludeBounds(
                ref PerimeterGroundWorldBounds,
                ref _perimeterGroundBoundsInitialized,
                bounds);

        public void IncludeMountainBounds(Aabb bounds)
            => IncludeBounds(ref MountainWorldBounds, ref _mountainBoundsInitialized, bounds);

        private static void IncludeBounds(
            ref Aabb accumulated,
            ref bool initialized,
            Aabb bounds)
        {
            if (!initialized)
            {
                accumulated = bounds;
                initialized = true;
                return;
            }
            accumulated = accumulated.Merge(bounds);
        }
    }

    private sealed record MeshMetrics(
        long TriangleCount,
        int VertexCount,
        Aabb WorldBounds,
        HashSet<QuantizedVertex> UniqueVertices,
        bool HasGravelMaterial,
        bool HasRockyMaterial,
        bool HasCompleteUvLayout);

    private readonly record struct QuantizedVertex(int X, int Y, int Z)
    {
        private const float Precision = 1000.0f;

        public static QuantizedVertex From(Vector3 vertex)
            => new(
                Mathf.RoundToInt(vertex.X * Precision),
                Mathf.RoundToInt(vertex.Y * Precision),
                Mathf.RoundToInt(vertex.Z * Precision));
    }
}
