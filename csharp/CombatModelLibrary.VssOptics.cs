using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal readonly record struct VssIntegratedScopeInspection(
    bool Available,
    int GlassSurfaceCount,
    int RearApertureVertexCount,
    Vector3 RearApertureCenter,
    Vector2 RearApertureSize,
    bool ClearMaterialValid,
    bool MarkerAligned)
{
    public bool GeometryValid => Available
        && GlassSurfaceCount > 0
        && RearApertureVertexCount >= 4
        && RearApertureSize.X >= 0.02f
        && RearApertureSize.Y >= 0.02f
        && ClearMaterialValid;

    public bool Valid => GeometryValid && MarkerAligned;
}

internal static partial class CombatModelLibrary
{
    private const string VssClearLensMaterialName = "SteelTideVssClearScopeLens";

    private static void ConfigureVssIntegratedScopeGlass(Node3D source)
    {
        var clearLens = new StandardMaterial3D
        {
            ResourceName = VssClearLensMaterialName,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.07f, 0.11f, 0.09f, 0.055f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.08f,
            Metallic = 0.0f
        };
        var glassSurfaces = 0;
        foreach (var meshInstance in MeshesBelow(source))
        {
            var mesh = meshInstance.Mesh;
            if (mesh is null)
            {
                continue;
            }
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var material = meshInstance.GetSurfaceOverrideMaterial(surface)
                    ?? mesh.SurfaceGetMaterial(surface);
                if (material?.ResourceName.Contains(
                        "Glass",
                        StringComparison.OrdinalIgnoreCase) != true)
                {
                    continue;
                }
                meshInstance.SetSurfaceOverrideMaterial(surface, clearLens);
                glassSurfaces++;
            }
        }
        if (glassSurfaces == 0)
        {
            throw new InvalidOperationException(
                "Authored VSS scope has no named glass surface to clear for first-person aiming.");
        }
    }

    internal static VssIntegratedScopeInspection InspectVssIntegratedScope(
        Node3D root,
        Node3D? reticleAnchor = null)
    {
        const float rearPlaneTolerance = 0.001f;
        var glassSurfaceCount = 0;
        var clearMaterialValid = true;
        var lensVertices = new List<Vector3>();

        foreach (var meshInstance in MeshesBelow(root))
        {
            if (meshInstance.Mesh is not ArrayMesh arrayMesh)
            {
                continue;
            }

            var toRoot = TransformRelativeToAncestor(meshInstance, root);
            for (var surface = 0; surface < arrayMesh.GetSurfaceCount(); surface++)
            {
                var activeMaterial = meshInstance.GetSurfaceOverrideMaterial(surface)
                    ?? arrayMesh.SurfaceGetMaterial(surface);
                if (!string.Equals(
                        activeMaterial?.ResourceName,
                        VssClearLensMaterialName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                glassSurfaceCount++;
                clearMaterialValid &= activeMaterial is StandardMaterial3D clearLens
                    && clearLens.Transparency == BaseMaterial3D.TransparencyEnum.Alpha
                    && clearLens.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded
                    && clearLens.AlbedoColor.A is > 0.0f and <= 0.08f;

                var meshData = new MeshDataTool();
                if (meshData.CreateFromSurface(arrayMesh, surface) != Error.Ok)
                {
                    clearMaterialValid = false;
                    continue;
                }
                for (var vertex = 0; vertex < meshData.GetVertexCount(); vertex++)
                {
                    lensVertices.Add(toRoot * meshData.GetVertex(vertex));
                }
            }
        }

        if (glassSurfaceCount == 0 || lensVertices.Count == 0)
        {
            return new VssIntegratedScopeInspection(
                false,
                glassSurfaceCount,
                0,
                Vector3.Zero,
                Vector2.Zero,
                ClearMaterialValid: false,
                MarkerAligned: false);
        }

        var rearPlane = lensVertices.Max(vertex => vertex.Z);
        var rearVertices = lensVertices
            .Where(vertex => rearPlane - vertex.Z <= rearPlaneTolerance)
            .ToArray();
        if (rearVertices.Length == 0)
        {
            return new VssIntegratedScopeInspection(
                false,
                glassSurfaceCount,
                0,
                Vector3.Zero,
                Vector2.Zero,
                clearMaterialValid,
                MarkerAligned: false);
        }

        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var vertex in rearVertices)
        {
            minimum.X = Mathf.Min(minimum.X, vertex.X);
            minimum.Y = Mathf.Min(minimum.Y, vertex.Y);
            maximum.X = Mathf.Max(maximum.X, vertex.X);
            maximum.Y = Mathf.Max(maximum.Y, vertex.Y);
        }
        var apertureCenter2D = (minimum + maximum) * 0.5f;
        var apertureCenter = new Vector3(apertureCenter2D.X, apertureCenter2D.Y, rearPlane);
        var markerAligned = reticleAnchor is null
            || TransformRelativeToAncestor(reticleAnchor, root).Origin
                .DistanceTo(apertureCenter) <= 0.001f;
        return new VssIntegratedScopeInspection(
            true,
            glassSurfaceCount,
            rearVertices.Length,
            apertureCenter,
            maximum - minimum,
            clearMaterialValid,
            markerAligned);
    }

    public static VssIntegratedScopeInspection InspectVssIntegratedScope()
    {
        AuthoredWeaponVisual? visual = null;
        try
        {
            visual = InstantiateWeapon(WeaponPlatform.VSS, firstPerson: true);
            return visual.IntegratedOpticInspection;
        }
        catch
        {
            return default;
        }
        finally
        {
            visual?.Root.Free();
        }
    }

    private static Transform3D TransformRelativeToAncestor(
        Node3D descendant,
        Node3D ancestor)
    {
        var relative = Transform3D.Identity;
        Node? current = descendant;
        while (current is not null && !ReferenceEquals(current, ancestor))
        {
            if (current is not Node3D current3D)
            {
                throw new InvalidOperationException(
                    $"Node {descendant.Name} crosses a non-3D parent before {ancestor.Name}.");
            }
            relative = current3D.Transform * relative;
            current = current.GetParent();
        }
        if (!ReferenceEquals(current, ancestor))
        {
            throw new InvalidOperationException(
                $"Node {descendant.Name} is not below {ancestor.Name}.");
        }
        return relative;
    }
}
