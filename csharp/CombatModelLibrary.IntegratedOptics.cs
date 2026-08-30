using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal readonly record struct IntegratedScopeInspection(
    bool Available,
    int GlassSurfaceCount,
    int RearApertureVertexCount,
    int FrontApertureVertexCount,
    Vector3 RearApertureCenter,
    Vector3 FrontApertureCenter,
    Vector2 RearApertureSize,
    Vector2 FrontApertureSize,
    bool ClearMaterialValid,
    bool MarkerAligned,
    float OpticalAxisResidual)
{
    private const float MinimumApertureSize = 0.02f;
    private const float MinimumOpticalAxisLength = 0.05f;
    private const float MaximumOpticalAxisResidual = 0.001f;

    public Vector3 OpticalAxis => FrontApertureCenter - RearApertureCenter;
    public float OpticalAxisLength => OpticalAxis.Length();

    public bool GeometryValid => Available
        && GlassSurfaceCount > 0
        && RearApertureVertexCount >= 4
        && FrontApertureVertexCount >= 4
        && RearApertureSize.X >= MinimumApertureSize
        && RearApertureSize.Y >= MinimumApertureSize
        && FrontApertureSize.X >= MinimumApertureSize
        && FrontApertureSize.Y >= MinimumApertureSize
        && OpticalAxisLength >= MinimumOpticalAxisLength
        && ClearMaterialValid;

    public bool OpticalAxisAligned => float.IsFinite(OpticalAxisResidual)
        && OpticalAxisResidual <= MaximumOpticalAxisResidual;

    public bool Valid => GeometryValid && MarkerAligned && OpticalAxisAligned;
}

internal static partial class CombatModelLibrary
{
    private const string IntegratedScopeClearLensMaterialName =
        "SteelTideIntegratedScopeClearLens";
    private const float IntegratedScopePlaneTolerance = 0.001f;
    private const float IntegratedScopeMarkerTolerance = 0.001f;

    internal static bool HasIntegratedScope(WeaponPlatform platform)
        => WeaponCatalog.HasFixedIntegratedScope(platform);

    private static void ConfigureIntegratedScopeGlass(
        Node3D source,
        WeaponPlatform platform)
    {
        var clearLens = new StandardMaterial3D
        {
            ResourceName = IntegratedScopeClearLensMaterialName,
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
                        StringComparison.OrdinalIgnoreCase) != true
                    && !string.Equals(
                        material?.ResourceName,
                        IntegratedScopeClearLensMaterialName,
                        StringComparison.Ordinal))
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
                $"Authored {platform} scope has no named glass surface to clear for aiming.");
        }
    }

    internal static IntegratedScopeInspection InspectIntegratedScope(
        Node3D root,
        Node3D? reticleAnchor = null)
    {
        var glassSurfaceCount = 0;
        var clearMaterialValid = true;
        var lensVertices = new List<Vector3>();

        foreach (var meshInstance in MeshesBelow(root))
        {
            if (meshInstance.Mesh is not ArrayMesh arrayMesh)
            {
                continue;
            }

            var toRoot = TransformBelowAncestor(meshInstance, root);
            for (var surface = 0; surface < arrayMesh.GetSurfaceCount(); surface++)
            {
                var activeMaterial = meshInstance.GetSurfaceOverrideMaterial(surface)
                    ?? arrayMesh.SurfaceGetMaterial(surface);
                if (!string.Equals(
                        activeMaterial?.ResourceName,
                        IntegratedScopeClearLensMaterialName,
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
            return default;
        }

        var rearPlane = lensVertices.Max(vertex => vertex.Z);
        var frontPlane = lensVertices.Min(vertex => vertex.Z);
        var rearAperture = MeasureAperturePlane(
            lensVertices,
            rearPlane,
            IntegratedScopePlaneTolerance);
        var frontAperture = MeasureAperturePlane(
            lensVertices,
            frontPlane,
            IntegratedScopePlaneTolerance);
        var opticalAxisResidual = new Vector2(
                frontAperture.Center.X - rearAperture.Center.X,
                frontAperture.Center.Y - rearAperture.Center.Y)
            .Length();
        var markerAligned = reticleAnchor is null
            || TransformBelowAncestor(reticleAnchor, root).Origin
                .DistanceTo(rearAperture.Center) <= IntegratedScopeMarkerTolerance;
        return new IntegratedScopeInspection(
            true,
            glassSurfaceCount,
            rearAperture.VertexCount,
            frontAperture.VertexCount,
            rearAperture.Center,
            frontAperture.Center,
            rearAperture.Size,
            frontAperture.Size,
            clearMaterialValid,
            markerAligned,
            opticalAxisResidual);
    }

    public static IntegratedScopeInspection InspectIntegratedScope(
        WeaponPlatform platform)
    {
        if (!HasIntegratedScope(platform))
        {
            return default;
        }

        AuthoredWeaponVisual? visual = null;
        try
        {
            visual = InstantiateWeapon(platform, firstPerson: true);
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

    private static AperturePlane MeasureAperturePlane(
        IEnumerable<Vector3> vertices,
        float plane,
        float tolerance)
    {
        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var count = 0;
        foreach (var vertex in vertices)
        {
            if (Mathf.Abs(vertex.Z - plane) > tolerance)
            {
                continue;
            }

            minimum.X = Mathf.Min(minimum.X, vertex.X);
            minimum.Y = Mathf.Min(minimum.Y, vertex.Y);
            maximum.X = Mathf.Max(maximum.X, vertex.X);
            maximum.Y = Mathf.Max(maximum.Y, vertex.Y);
            count++;
        }

        if (count == 0)
        {
            return default;
        }

        var center2D = (minimum + maximum) * 0.5f;
        return new AperturePlane(
            count,
            new Vector3(center2D.X, center2D.Y, plane),
            maximum - minimum);
    }

    private readonly record struct AperturePlane(
        int VertexCount,
        Vector3 Center,
        Vector2 Size);
}
