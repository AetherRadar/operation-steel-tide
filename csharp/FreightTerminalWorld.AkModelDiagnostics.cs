using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal readonly record struct Ak47ModelVariantInspection(
    bool Loaded,
    bool RequiredNodes,
    int MeshCount,
    int NonEmptyMeshCount,
    int MaterialCount,
    int TexturedMaterialCount,
    int VertexCount,
    int TriangleCount,
    Vector3 Size,
    bool MagazineMesh,
    bool SpareMagazineMesh,
    bool ChargingHandleMesh,
    bool RearIronMesh,
    bool FrontIronMesh,
    bool DistinctIronMeshes,
    bool RailMarker,
    bool RailMarkerWithinFootprint,
    bool EjectionMarker,
    float RailContactGap)
{
    public bool RuntimeContractValid => Loaded
        && RequiredNodes
        && MeshCount >= 11
        && NonEmptyMeshCount >= 11
        && MaterialCount >= 6
        && TexturedMaterialCount >= 1
        && MagazineMesh
        && SpareMagazineMesh
        && ChargingHandleMesh
        && RearIronMesh
        && FrontIronMesh
        && DistinctIronMeshes
        && RailMarker
        && RailMarkerWithinFootprint
        && EjectionMarker
        && float.IsFinite(RailContactGap)
        && Mathf.Abs(RailContactGap) <= 0.003f
        && Size.X is >= 0.06f and <= 0.25f
        && Size.Y is >= 0.3f and <= 0.9f
        // The AK is intentionally compacted around its firing grip to a
        // 1.40 m authored presentation length. Keep a small tolerance for
        // exporter round-off while rejecting the former 1.58 m regression.
        && Size.Z is >= 1.34f and <= 1.46f;
}

internal readonly record struct Ak47ModelQualityInspection(
    string FirstPersonPath,
    string WorldPath,
    bool PathsDistinct,
    Ak47ModelVariantInspection FirstPerson,
    Ak47ModelVariantInspection World)
{
    public bool Valid => PathsDistinct
        && FirstPerson.RuntimeContractValid
        && World.RuntimeContractValid
        && FirstPerson.TriangleCount is >= 90_000 and <= 125_000
        && World.TriangleCount is >= 23_000 and <= 32_000
        && FirstPerson.TriangleCount >= World.TriangleCount * 3
        && FirstPerson.TriangleCount != World.TriangleCount;
}

public partial class FreightTerminalWorld
{
    private static readonly string[] Ak47RequiredRuntimeNodes =
    {
        "SteelTideAK47",
        "WeaponBodyGeometry",
        "ReceiverGeometry",
        "FurnitureGeometry",
        "BoltHardwareGeometry",
        "OpticRailAdapterGeometry",
        "Magazine",
        "MagazineGrip",
        "MagazineGeometry",
        "SpareMagazine",
        "SpareMagazineGrip",
        "SpareMagazineGeometry",
        "ChargingHandle",
        "ChargingHandleGeometry",
        "Stock",
        "StockWoodGeometry",
        "StockButtpadGeometry",
        "RearIronSight",
        "RearIronGeometry",
        "FrontIronSight",
        "FrontIronGeometry",
        "Foregrip",
        "MuzzleDevice",
        "MuzzleDeviceTip",
        "Suppressor",
        "SuppressorTip",
        "OpticMount",
        "OpticRailContact",
        "OpticReticleAnchor",
        "EjectionPort"
    };

    private static Ak47ModelQualityInspection InspectAk47ModelQuality()
    {
        var firstPersonPath = CombatModelLibrary.Ak47FirstPersonScenePath;
        var worldPath = CombatModelLibrary.Ak47WorldScenePath;
        var pathsDistinct = !string.Equals(
            firstPersonPath,
            worldPath,
            StringComparison.Ordinal);
        return new Ak47ModelQualityInspection(
            firstPersonPath,
            worldPath,
            pathsDistinct,
            InspectAk47ModelVariant(firstPersonPath),
            InspectAk47ModelVariant(worldPath));
    }

    private static Ak47ModelVariantInspection InspectAk47ModelVariant(string path)
    {
        Node3D? root = null;
        try
        {
            var scene = GD.Load<PackedScene>(path);
            if (scene is null)
            {
                return default;
            }

            root = scene.Instantiate<Node3D>();
            var requiredNodes = Ak47RequiredRuntimeNodes.All(
                name => CombatModelLibrary.FindOptionalNode(root, name) is not null);
            var meshes = CombatModelLibrary.MeshesBelow(root)
                .Where(mesh => mesh.Mesh is not null)
                .ToArray();
            var geometry = CountAk47Geometry(meshes);
            var nonEmptyMeshCount = meshes.Count(HasNonEmptyAk47Mesh);
            var materials = new HashSet<ulong>();
            var texturedMaterials = new HashSet<ulong>();
            foreach (var meshInstance in meshes)
            {
                var mesh = meshInstance.Mesh!;
                for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                {
                    var material = meshInstance.GetSurfaceOverrideMaterial(surface)
                        ?? mesh.SurfaceGetMaterial(surface);
                    if (material is null)
                    {
                        continue;
                    }

                    materials.Add(material.GetInstanceId());
                    if (material is BaseMaterial3D baseMaterial
                        && baseMaterial.AlbedoTexture is not null)
                    {
                        texturedMaterials.Add(material.GetInstanceId());
                    }
                }
            }

            var magazine = CombatModelLibrary.FindOptionalNode(root, "Magazine");
            var spareMagazine = CombatModelLibrary.FindOptionalNode(root, "SpareMagazine");
            var chargingHandle = CombatModelLibrary.FindOptionalNode(root, "ChargingHandle");
            var rearIron = CombatModelLibrary.FindOptionalNode(root, "RearIronSight");
            var frontIron = CombatModelLibrary.FindOptionalNode(root, "FrontIronSight");
            var railMarker = CombatModelLibrary.FindOptionalNode(root, "OpticRailContact");
            var ejectionMarker = CombatModelLibrary.FindOptionalNode(root, "EjectionPort");
            var railAdapter = CombatModelLibrary.FindOptionalNode(
                root,
                "OpticRailAdapterGeometry");
            var rearMeshes = MeshesBelowAk47Node(rearIron).ToArray();
            var frontMeshes = MeshesBelowAk47Node(frontIron).ToArray();
            var magazineMesh = HasNonEmptyAk47Geometry(magazine);
            var spareMagazineMesh = HasNonEmptyAk47Geometry(spareMagazine);
            var chargingHandleMesh = HasNonEmptyAk47Geometry(chargingHandle);
            var rearIronMesh = rearMeshes.Any(HasNonEmptyAk47Mesh);
            var frontIronMesh = frontMeshes.Any(HasNonEmptyAk47Mesh);
            var distinctIronMeshes = rearMeshes.Length > 0
                && frontMeshes.Length > 0
                && rearMeshes.All(rear => frontMeshes.All(front =>
                    rear.GetInstanceId() != front.GetInstanceId()
                    && rear.Mesh?.GetInstanceId() != front.Mesh?.GetInstanceId()));
            var railContact = InspectAk47RailContact(
                root,
                railMarker,
                railAdapter);
            var size = ComputeAk47Bounds(root);
            return new Ak47ModelVariantInspection(
                true,
                requiredNodes,
                meshes.Length,
                nonEmptyMeshCount,
                materials.Count,
                texturedMaterials.Count,
                geometry.VertexCount,
                geometry.TriangleCount,
                size,
                magazineMesh,
                spareMagazineMesh,
                chargingHandleMesh,
                rearIronMesh,
                frontIronMesh,
                distinctIronMeshes,
                IsFiniteNonOriginMarker(railMarker),
                railContact.WithinFootprint,
                IsFiniteNonOriginMarker(ejectionMarker),
                railContact.Gap);
        }
        catch (Exception exception)
        {
            GD.PushError($"AK-47 model validation failed for {path}: {exception}");
            return default;
        }
        finally
        {
            root?.Free();
        }
    }

    private static IEnumerable<MeshInstance3D> MeshesBelowAk47Node(Node3D? root)
    {
        if (root is MeshInstance3D mesh)
        {
            yield return mesh;
        }
        if (root is null)
        {
            yield break;
        }
        foreach (var descendant in CombatModelLibrary.MeshesBelow(root))
        {
            yield return descendant;
        }
    }

    private static bool HasNonEmptyAk47Geometry(Node3D? root)
        => MeshesBelowAk47Node(root).Any(HasNonEmptyAk47Mesh);

    private static bool HasNonEmptyAk47Mesh(MeshInstance3D mesh)
        => CountAk47Geometry(new[] { mesh }).TriangleCount > 0;

    private static (int VertexCount, int TriangleCount) CountAk47Geometry(
        IEnumerable<MeshInstance3D> meshInstances)
    {
        var vertexCount = 0;
        var triangleCount = 0;
        foreach (var meshInstance in meshInstances)
        {
            if (meshInstance.Mesh is not ArrayMesh mesh)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
                {
                    continue;
                }

                var vertices = mesh.SurfaceGetArrayLen(surface);
                var indices = mesh.SurfaceGetArrayIndexLen(surface);
                vertexCount += vertices;
                triangleCount += (indices > 0 ? indices : vertices) / 3;
            }
        }
        return (vertexCount, triangleCount);
    }

    private static Vector3 ComputeAk47Bounds(Node3D root)
    {
        var minimum = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity);
        var maximum = new Vector3(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity);
        var spareMagazine = CombatModelLibrary.FindOptionalNode(root, "SpareMagazine");
        foreach (var mesh in CombatModelLibrary.MeshesBelow(root))
        {
            if (mesh.Mesh is null || IsAk47NodeBelow(mesh, spareMagazine))
            {
                continue;
            }

            AccumulateAk47MeshBounds(root, mesh, ref minimum, ref maximum);
        }

        return float.IsFinite(minimum.X) && float.IsFinite(maximum.X)
            ? maximum - minimum
            : Vector3.Zero;
    }

    private static bool IsAk47NodeBelow(Node node, Node3D? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }
        for (var current = node.GetParent(); current is not null; current = current.GetParent())
        {
            if (current == ancestor)
            {
                return true;
            }
        }
        return false;
    }

    private static (float Gap, bool WithinFootprint) InspectAk47RailContact(
        Node3D root,
        Node3D? marker,
        Node3D? railAdapter)
    {
        if (marker is null || railAdapter is null)
        {
            return (float.PositiveInfinity, false);
        }

        const float footprintTolerance = 0.003f;
        var markerPosition = Ak47TransformRelativeToRoot(root, marker).Origin;
        var railMinimum = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity);
        var railMaximum = new Vector3(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity);
        var railTop = float.NegativeInfinity;
        foreach (var mesh in MeshesBelowAk47Node(railAdapter))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            var bounds = mesh.Mesh.GetAabb();
            var toRoot = Ak47TransformRelativeToRoot(root, mesh);
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                var point = toRoot * bounds.GetEndpoint(endpoint);
                railMinimum = railMinimum.Min(point);
                railMaximum = railMaximum.Max(point);
                railTop = Mathf.Max(
                    railTop,
                    point.Y);
            }
        }

        if (!float.IsFinite(railTop))
        {
            return (float.PositiveInfinity, false);
        }

        var withinFootprint = markerPosition.X >= railMinimum.X - footprintTolerance
            && markerPosition.X <= railMaximum.X + footprintTolerance
            && markerPosition.Z >= railMinimum.Z - footprintTolerance
            && markerPosition.Z <= railMaximum.Z + footprintTolerance;
        return (markerPosition.Y - railTop, withinFootprint);
    }

    private static void AccumulateAk47MeshBounds(
        Node3D root,
        MeshInstance3D mesh,
        ref Vector3 minimum,
        ref Vector3 maximum)
    {
        var bounds = mesh.Mesh!.GetAabb();
        var toRoot = Ak47TransformRelativeToRoot(root, mesh);
        for (var endpoint = 0; endpoint < 8; endpoint++)
        {
            var point = toRoot * bounds.GetEndpoint(endpoint);
            minimum = minimum.Min(point);
            maximum = maximum.Max(point);
        }
    }

    private static Transform3D Ak47TransformRelativeToRoot(
        Node3D root,
        Node3D node)
    {
        var transform = Transform3D.Identity;
        Node3D? current = node;
        while (current is not null && current != root)
        {
            transform = current.Transform * transform;
            current = current.GetParent() as Node3D;
        }
        return current == root ? transform : Transform3D.Identity;
    }

    private static bool IsFiniteNonOriginMarker(Node3D? marker)
        => marker is not null
            && float.IsFinite(marker.Position.X)
            && float.IsFinite(marker.Position.Y)
            && float.IsFinite(marker.Position.Z)
            && marker.Position.LengthSquared() > 0.000001f;

    private static string FormatAk47ModelQuality(Ak47ModelQualityInspection inspection)
        => $"valid={inspection.Valid};paths_distinct={inspection.PathsDistinct};"
            + $"fp_path={inspection.FirstPersonPath};"
            + $"fp={FormatAk47ModelVariant(inspection.FirstPerson)};"
            + $"world_path={inspection.WorldPath};"
            + $"world={FormatAk47ModelVariant(inspection.World)}";

    private static string FormatAk47ModelVariant(Ak47ModelVariantInspection inspection)
        => $"contract={inspection.RuntimeContractValid},loaded={inspection.Loaded},"
            + $"nodes={inspection.RequiredNodes},meshes={inspection.MeshCount},"
            + $"nonempty={inspection.NonEmptyMeshCount},materials={inspection.MaterialCount},"
            + $"textured={inspection.TexturedMaterialCount},vertices={inspection.VertexCount},"
            + $"triangles={inspection.TriangleCount},size={inspection.Size},"
            + $"magazine={inspection.MagazineMesh},"
            + $"spare_magazine={inspection.SpareMagazineMesh},"
            + $"charging={inspection.ChargingHandleMesh},"
            + $"rear_iron={inspection.RearIronMesh},front_iron={inspection.FrontIronMesh},"
            + $"distinct_irons={inspection.DistinctIronMeshes},"
            + $"rail_marker={inspection.RailMarker},"
            + $"rail_footprint={inspection.RailMarkerWithinFootprint},"
            + $"ejection_marker={inspection.EjectionMarker},"
            + $"rail_gap={inspection.RailContactGap:0.000000}";
}
