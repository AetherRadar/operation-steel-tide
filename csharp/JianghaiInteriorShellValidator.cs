using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal readonly record struct JianghaiInteriorShellValidation(
    bool Valid,
    int ShellCount,
    int SharedMeshCount,
    int BatchedShellCount,
    int OpaqueSurfaceCount,
    int ClosedDirectionCount,
    int TriangleCount,
    string Failure);

/// <summary>
/// Verifies that every enterable Jianghai building carries the shared authored
/// five-sided visual liner which prevents doors and windows from seeing through
/// the exterior building kit.
/// </summary>
internal static class JianghaiInteriorShellValidator
{
    public const string ShellPrefix = "JianghaiInteriorShell_";
    public const string ShellMeta = "jianghai_interior_liner";
    public const string ShellSourceMeta = "jianghai_liner_source_name";
    public const string ShellOpaqueMeta = "jianghai_liner_opaque";
    public const string ShellVisibilityMeta = "jianghai_liner_visibility_m";
    public const float RequiredVisibilityRange = 460.0f;

    private const float RayEpsilon = 0.00001f;

    public static JianghaiInteriorShellValidation Validate(Node3D authoredRoot)
    {
        ArgumentNullException.ThrowIfNull(authoredRoot);
        var nodes = authoredRoot.FindChildren("*", "MeshInstance3D", recursive: true, owned: false);
        using var nodesBacking = nodes.AsDisposable();
        var shells = nodes.OfType<MeshInstance3D>()
            .Where(IsShell)
            .OrderBy(shell => shell.Name.ToString(), StringComparer.Ordinal)
            .ToArray();
        var expectedNames = JianghaiGameplayCollisionContract.ExpectedEnterableSourceNames;
        var failures = new List<string>();
        var shellSources = new HashSet<string>(StringComparer.Ordinal);
        var meshIds = new HashSet<ulong>();
        var batchedShellCount = 0;
        var opaqueSurfaceCount = 0;
        var closedDirectionCount = 0;
        var triangleCount = 0;

        foreach (var shell in shells)
        {
            var shellName = shell.Name.ToString();
            var importedSourceName = shell.GetMeta(ShellSourceMeta, string.Empty).AsString();
            var sourceName = !string.IsNullOrEmpty(importedSourceName)
                ? importedSourceName
                : shellName.StartsWith(ShellPrefix, StringComparison.Ordinal)
                    ? shellName[ShellPrefix.Length..]
                    : string.Empty;
            if (string.IsNullOrEmpty(sourceName) || !shellSources.Add(sourceName))
            {
                failures.Add($"source:{shell.Name}:{sourceName}");
            }
            if (!shellName.Equals(
                    $"{ShellPrefix}{sourceName}",
                    StringComparison.Ordinal))
            {
                failures.Add($"name:{shell.Name}:{sourceName}");
            }
            if ((shell.HasMeta(ShellOpaqueMeta)
                    && !shell.GetMeta(ShellOpaqueMeta).AsBool())
                || (shell.HasMeta(ShellVisibilityMeta)
                    && Mathf.Abs(shell.GetMeta(ShellVisibilityMeta).AsSingle()
                        - RequiredVisibilityRange) > 0.01f))
            {
                failures.Add($"metadata:{shell.Name}");
            }
            if (shell.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() == 0)
            {
                failures.Add($"mesh:{shell.Name}");
                continue;
            }

            meshIds.Add(mesh.GetInstanceId());
            if (shell.GetMeta("jianghai_render_batched_source", false).AsBool()
                && shell.Layers == 0)
            {
                batchedShellCount++;
            }
            var policy = JianghaiAuthoredRenderBatcher.CreateQualityPolicy(shell);
            var source = authoredRoot.FindChild(
                sourceName,
                recursive: true,
                owned: false) as MeshInstance3D;
            var sourceRange = source is null
                ? float.PositiveInfinity
                : JianghaiAuthoredRenderBatcher.CreateQualityPolicy(source).BaseVisibilityRange;
            var shellBounds = shell.GetAabb();
            var shellScale = shell.GlobalBasis.Scale.Abs();
            var shellWorldWidth = shellBounds.Size.X * shellScale.X;
            var shellWorldDepth = shellBounds.Size.Z * shellScale.Z;
            var sourceRoomWidth = source?.GetMeta("jianghai_room_width_m", 0.0f).AsSingle()
                ?? 0.0f;
            var sourceRoomDepth = source?.GetMeta("jianghai_room_depth_m", 0.0f).AsSingle()
                ?? 0.0f;
            if (Mathf.Abs(policy.BaseVisibilityRange - RequiredVisibilityRange) > 0.01f
                || policy.BaseVisibilityRange + 0.01f < sourceRange
                || !policy.AlwaysDisableShadow
                || !policy.IsDetail
                || Mathf.Abs(shellWorldWidth - sourceRoomWidth) > 0.02f
                || Mathf.Abs(shellWorldDepth - sourceRoomDepth) > 0.02f)
            {
                failures.Add($"policy:{shell.Name}");
            }

            var surfacesOpaque = true;
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var material = shell.GetActiveMaterial(surface)
                    ?? mesh.SurfaceGetMaterial(surface);
                if (material is not BaseMaterial3D baseMaterial
                    || baseMaterial.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
                    || baseMaterial.AlbedoColor.A < 0.999f)
                {
                    surfacesOpaque = false;
                    failures.Add($"opaque:{shell.Name}:{surface}");
                }
                else
                {
                    opaqueSurfaceCount++;
                }
                triangleCount += SurfaceTriangleCount(mesh, surface);
            }

            var bounds = shell.GetAabb();
            var center = bounds.GetCenter();
            var rayLength = bounds.Size.Length() + 0.5f;
            foreach (var direction in new[]
            {
                Vector3.Left,
                Vector3.Right,
                Vector3.Forward,
                Vector3.Up,
                Vector3.Down
            })
            {
                if (RayHitsMesh(mesh, center, direction, rayLength))
                {
                    closedDirectionCount++;
                }
                else
                {
                    failures.Add($"open:{shell.Name}:{direction}");
                }
            }
            if (!surfacesOpaque
                || RayHitsMesh(mesh, center, Vector3.Back, bounds.Size.Z * 0.6f))
            {
                failures.Add($"front:{shell.Name}");
            }
        }

        var expectedSourceSet = expectedNames.ToHashSet(StringComparer.Ordinal);
        if (!shellSources.SetEquals(expectedSourceSet))
        {
            failures.Add(
                $"sources:{string.Join(',', shellSources.Order())}/"
                + string.Join(',', expectedSourceSet.Order()));
        }
        var expectedShellCount = expectedNames.Count;
        var valid = failures.Count == 0
            && shells.Length == expectedShellCount
            && meshIds.Count == 1
            && batchedShellCount == expectedShellCount
            && closedDirectionCount == expectedShellCount * 5
            && triangleCount > 0;
        return new JianghaiInteriorShellValidation(
            valid,
            shells.Length,
            meshIds.Count,
            batchedShellCount,
            opaqueSurfaceCount,
            closedDirectionCount,
            triangleCount,
            failures.Count == 0 ? "none" : string.Join('|', failures.Take(8)));
    }

    private static bool IsShell(MeshInstance3D mesh)
        => mesh.Name.ToString().StartsWith(ShellPrefix, StringComparison.Ordinal)
            || mesh.GetMeta(ShellMeta, false).AsBool();

    private static int SurfaceTriangleCount(ArrayMesh mesh, int surface)
    {
        if (mesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
        {
            return 0;
        }
        var indexCount = mesh.SurfaceGetArrayIndexLen(surface);
        return (indexCount > 0 ? indexCount : mesh.SurfaceGetArrayLen(surface)) / 3;
    }

    private static bool RayHitsMesh(
        ArrayMesh mesh,
        Vector3 origin,
        Vector3 direction,
        float maximumDistance)
    {
        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            if (mesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
            {
                continue;
            }
            using var arrays = mesh.SurfaceGetArrays(surface);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].VariantType
                    == Variant.Type.PackedInt32Array
                ? arrays[(int)Mesh.ArrayType.Index].AsInt32Array()
                : Array.Empty<int>();
            var elementCount = indices.Length > 0 ? indices.Length : vertices.Length;
            for (var index = 0; index + 2 < elementCount; index += 3)
            {
                var first = vertices[indices.Length > 0 ? indices[index] : index];
                var second = vertices[indices.Length > 0 ? indices[index + 1] : index + 1];
                var third = vertices[indices.Length > 0 ? indices[index + 2] : index + 2];
                if (RayIntersectsTriangle(
                        origin,
                        direction,
                        first,
                        second,
                        third,
                        out var distance)
                    && distance <= maximumDistance)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool RayIntersectsTriangle(
        Vector3 origin,
        Vector3 direction,
        Vector3 first,
        Vector3 second,
        Vector3 third,
        out float distance)
    {
        distance = 0.0f;
        var firstEdge = second - first;
        var secondEdge = third - first;
        var cross = direction.Cross(secondEdge);
        var determinant = firstEdge.Dot(cross);
        if (Mathf.Abs(determinant) <= RayEpsilon)
        {
            return false;
        }
        var inverse = 1.0f / determinant;
        var fromFirst = origin - first;
        var firstCoordinate = fromFirst.Dot(cross) * inverse;
        if (firstCoordinate < 0.0f || firstCoordinate > 1.0f)
        {
            return false;
        }
        var secondCross = fromFirst.Cross(firstEdge);
        var secondCoordinate = direction.Dot(secondCross) * inverse;
        if (secondCoordinate < 0.0f || firstCoordinate + secondCoordinate > 1.0f)
        {
            return false;
        }
        distance = secondEdge.Dot(secondCross) * inverse;
        return distance > RayEpsilon;
    }
}
