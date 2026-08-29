using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Builds deterministic box-only gameplay collision from the stable extraction layout.
/// Visual mesh topology can change without triggering runtime triangle extraction or physics baking.
/// </summary>
internal sealed class JianghaiGameplayCollisionBuilder
{
    public const string CollisionGroup = "jianghai_gameplay_collision";

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
        var placementShapeCount = 0;
        var suppressedPlacementCount = 0;
        var suppressedPlacementNames = new List<string>();
        var authoredSourceCount = 0;
        var authoredShapeCount = 0;
        var densitySourceCount = 0;
        var solidSourceCount = 0;
        var enterableSourceCount = 0;
        var enterableShapeCount = 0;
        var shapeCount = 0;
        var authoredSourceNames = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var authoredSources = CollectAuthoredSources(authoredRoot);
            var enterableSources = authoredSources
                .Where(source => source.IsEnterable)
                .ToArray();
            var enterableFootprints = enterableSources
                .Select(source => new JianghaiCollisionFootprint(
                    source.Room.Center,
                    source.Basis,
                    source.Room.Size))
                .ToArray();
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
                var center = placement.Position + basis * offset;
                placementCount++;
                var carved = false;
                IReadOnlyList<JianghaiPlacementCollisionFragment> fragments =
                    placement.IsTallScene
                    ? JianghaiGameplayCollisionGeometry.CarvePlacementProxy(
                        center,
                        basis,
                        size,
                        enterableFootprints,
                        out carved)
                    : new[] { new JianghaiPlacementCollisionFragment(center, size) };
                if (placement.IsTallScene && carved)
                {
                    suppressedPlacementCount++;
                    suppressedPlacementNames.Add(placement.Name);
                }
                if (fragments.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Jianghai placement carve removed every proxy fragment for '{placement.Name}'.");
                }
                for (var fragmentIndex = 0; fragmentIndex < fragments.Count; fragmentIndex++)
                {
                    var fragment = fragments[fragmentIndex];
                    var collision = new CollisionShape3D
                    {
                        Name = $"GameplayProxy_{shapeCount + 1:000}_{placement.Name}_"
                            + $"{fragmentIndex + 1:00}",
                        Shape = new BoxShape3D { Size = fragment.Size },
                        Transform = new Transform3D(basis, fragment.Center)
                    };
                    collision.SetMeta("gameplay_source_placement", placement.Name);
                    collision.SetMeta("gameplay_district", placement.District);
                    collision.SetMeta("gameplay_proxy_fragment", fragmentIndex);
                    body.AddChild(collision);
                    placementShapeCount++;
                    shapeCount++;
                    districtCounts.TryGetValue(placement.District, out var districtCount);
                    districtCounts[placement.District] = districtCount + 1;
                }
            }

            foreach (var source in authoredSources)
            {
                var sourceName = source.Source.Name.ToString();
                if (!authoredSourceNames.Add(sourceName))
                {
                    throw new InvalidOperationException(
                        $"Duplicate Jianghai gameplay collision source '{sourceName}'.");
                }

                var addedShapes = source.IsEnterable
                    ? JianghaiEnterableCollisionShellBuilder.Build(
                        body,
                        source.Source,
                        source.Basis,
                        source.Room,
                        shapeCount)
                    : source.IsSolid
                        ? AddAuthoredSolidBox(body, source, shapeCount)
                        : AddAuthoredBox(
                        body,
                        source.Source,
                        source.Basis,
                        source.Center,
                        source.Size,
                        shapeCount);
                shapeCount += addedShapes;
                authoredShapeCount += addedShapes;
                authoredSourceCount++;
                if (source.IsEnterable)
                {
                    enterableSourceCount++;
                    enterableShapeCount += addedShapes;
                }
                else if (source.IsDensity)
                {
                    densitySourceCount++;
                }
                else if (source.IsSolid)
                {
                    solidSourceCount++;
                }
            }
            districtCounts["authored_density"] = densitySourceCount;
            districtCounts["authored_solid"] = solidSourceCount;
            districtCounts["authored_enterable"] = enterableShapeCount;

            var expectedPlacements = 0;
            foreach (var placement in layout.Models)
            {
                if (placement.HasCollision)
                {
                    expectedPlacements++;
                }
            }
            var missingAuthoredSources = JianghaiGameplayCollisionContract
                .ExpectedAuthoredSourceNames
                .Where(name => !authoredSourceNames.Contains(name))
                .ToArray();
            var unexpectedAuthoredSources = authoredSourceNames
                .Where(name => !JianghaiGameplayCollisionContract
                    .IsExpectedAuthoredSource(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (placementCount != expectedPlacements
                || requireAuthoredProxies
                    && (authoredSourceCount
                            != JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount
                        || authoredShapeCount
                            != JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount
                        || densitySourceCount
                            != JianghaiGameplayCollisionContract.ExpectedDensitySourceCount
                        || solidSourceCount
                            != JianghaiGameplayCollisionContract.ExpectedSolidSourceCount
                        || enterableSourceCount
                            != JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount
                        || enterableShapeCount
                            != JianghaiGameplayCollisionContract.ExpectedEnterableShapeCount
                        || missingAuthoredSources.Length > 0
                        || unexpectedAuthoredSources.Length > 0))
            {
                throw new InvalidOperationException(
                    "Jianghai gameplay collision contract incomplete "
                    + $"(placements={placementCount}/{expectedPlacements}, "
                    + $"placement_shapes={placementShapeCount}, "
                    + $"suppressed={suppressedPlacementCount}, "
                    + $"authored_sources={authoredSourceCount}/"
                    + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount}, "
                    + $"authored_shapes={authoredShapeCount}/"
                    + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount}, "
                    + $"density={densitySourceCount}/"
                    + $"{JianghaiGameplayCollisionContract.ExpectedDensitySourceCount}, "
                    + $"solid={solidSourceCount}/"
                    + $"{JianghaiGameplayCollisionContract.ExpectedSolidSourceCount}, "
                    + $"enterable={enterableSourceCount}/"
                    + $"{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount}:"
                    + $"{enterableShapeCount}/"
                    + $"{JianghaiGameplayCollisionContract.ExpectedEnterableShapeCount}, "
                    + $"missing={FormatSourceNames(missingAuthoredSources)}, "
                    + $"unexpected={FormatSourceNames(unexpectedAuthoredSources)}).");
            }

            body.SetMeta("gameplay_source_placement_count", placementCount);
            body.SetMeta("gameplay_placement_shape_count", placementShapeCount);
            body.SetMeta("gameplay_suppressed_placement_count", suppressedPlacementCount);
            body.SetMeta(
                "gameplay_suppressed_placement_names",
                string.Join(',', suppressedPlacementNames));
            body.SetMeta("gameplay_authored_source_mesh_count", authoredSourceCount);
            body.SetMeta("gameplay_authored_shape_count", authoredShapeCount);
            body.SetMeta("gameplay_solid_source_count", solidSourceCount);
            body.SetMeta("gameplay_enterable_source_count", enterableSourceCount);
            body.SetMeta("gameplay_enterable_shape_count", enterableShapeCount);
            body.SetMeta("gameplay_collision_shape_count", shapeCount);
            return new JianghaiGameplayCollisionResult(
                body,
                placementCount,
                placementShapeCount,
                suppressedPlacementCount,
                suppressedPlacementNames,
                authoredSourceCount,
                authoredShapeCount,
                densitySourceCount,
                solidSourceCount,
                enterableSourceCount,
                enterableShapeCount,
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
        if (IsAuthoredDensityBuilding(meshInstance)
            || IsAuthoredSolidBuilding(meshInstance)
            || IsEnterableBuilding(meshInstance))
        {
            return true;
        }
        if (meshInstance.HasMeta("jianghai_gameplay_proxy")
            && meshInstance.GetMeta("jianghai_gameplay_proxy").AsBool())
        {
            return true;
        }
        var name = meshInstance.Name.ToString();
        return name.StartsWith("JianghaiGameplayProxy_", StringComparison.Ordinal)
            || name.StartsWith("ChineseEdgeBuilding_", StringComparison.Ordinal)
            || JianghaiGameplayCollisionContract.IsExpectedAuthoredSource(name);
    }

    private static bool IsAuthoredDensityBuilding(MeshInstance3D meshInstance)
    {
        var metadataReady = string.Equals(
                meshInstance.GetMeta("district_role", string.Empty).AsString(),
                JianghaiGameplayCollisionContract.AuthoredDensityDistrictRole,
                StringComparison.Ordinal)
            && string.Equals(
                meshInstance.GetMeta("collision_role", string.Empty).AsString(),
                JianghaiGameplayCollisionContract.AuthoredDensityCollisionRole,
                StringComparison.Ordinal);
        return metadataReady || JianghaiGameplayCollisionContract.IsExpectedDensitySource(
            meshInstance.Name.ToString());
    }

    private static bool IsEnterableBuilding(MeshInstance3D meshInstance)
        => meshInstance.GetMeta("jianghai_enterable", false).AsBool()
            || JianghaiGameplayCollisionContract.IsExpectedEnterableSource(
                meshInstance.Name.ToString());

    private static bool IsAuthoredSolidBuilding(MeshInstance3D meshInstance)
        => JianghaiGameplayCollisionContract.IsExpectedSolidSource(
            meshInstance.Name.ToString());

    private static List<AuthoredProxyGeometry> CollectAuthoredSources(Node3D? authoredRoot)
    {
        var sources = new List<AuthoredProxyGeometry>(
            JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount);
        if (authoredRoot is null)
        {
            return sources;
        }

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
            var worldSize = localBounds.Size * worldScale;
            if (worldSize.X <= 0.05f || worldSize.Y <= 0.05f || worldSize.Z <= 0.05f)
            {
                continue;
            }

            var basis = meshInstance.GlobalBasis.Orthonormalized();
            var center = meshInstance.GlobalTransform * localBounds.GetCenter();
            var enterable = IsEnterableBuilding(meshInstance);
            sources.Add(new AuthoredProxyGeometry(
                meshInstance,
                basis,
                center,
                worldSize,
                IsAuthoredDensityBuilding(meshInstance),
                IsAuthoredSolidBuilding(meshInstance),
                enterable,
                enterable
                    ? JianghaiGameplayCollisionGeometry.ResolveEnterableRoom(
                        meshInstance,
                        basis,
                        center,
                        worldSize)
                    : default));
        }
        return sources;
    }

    private static bool OverlapsFootprint(
        Vector3 firstCenter,
        Basis firstBasis,
        Vector3 firstSize,
        Vector3 secondCenter,
        Basis secondBasis,
        Vector3 secondSize)
        => JianghaiGameplayCollisionGeometry.OverlapsFootprint(
            firstCenter,
            firstBasis,
            firstSize,
            secondCenter,
            secondBasis,
            secondSize);

    private static string FormatSourceNames(IReadOnlyCollection<string> names)
        => names.Count == 0 ? "none" : string.Join(',', names);

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

    private static int AddAuthoredSolidBox(
        StaticBody3D body,
        AuthoredProxyGeometry source,
        int shapeIndex)
    {
        var geometry = JianghaiGameplayCollisionGeometry.ResolveSolidBuilding(
            source.Source,
            source.Basis,
            source.Center,
            source.Size);
        return AddAuthoredBox(
            body,
            source.Source,
            source.Basis,
            geometry.Center,
            geometry.Size,
            shapeIndex);
    }

    private static CollisionShape3D AddBox(
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
        var densitySource = IsAuthoredDensityBuilding(source);
        collision.SetMeta(
            "gameplay_source_district_role",
            densitySource
                ? JianghaiGameplayCollisionContract.AuthoredDensityDistrictRole
                : source.GetMeta(
                    "district_role",
                    "authored_chinese_shop").AsString());
        collision.SetMeta(
            "gameplay_source_collision_role",
            JianghaiGameplayCollisionContract.AuthoredDensityCollisionRole);
        collision.SetMeta(
            "gameplay_source_kind",
            IsEnterableBuilding(source)
                ? "enterable"
                : densitySource
                    ? "density"
                    : IsAuthoredSolidBuilding(source) ? "solid" : "legacy");
        collision.SetMeta("gameplay_proxy_role", role);
        body.AddChild(collision);
        return collision;
    }

    private readonly record struct AuthoredProxyGeometry(
        MeshInstance3D Source,
        Basis Basis,
        Vector3 Center,
        Vector3 Size,
        bool IsDensity,
        bool IsSolid,
        bool IsEnterable,
        JianghaiEnterableRoomGeometry Room);
}
