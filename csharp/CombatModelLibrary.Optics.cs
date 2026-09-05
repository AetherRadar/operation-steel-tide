using System;
using Godot;

namespace OperationSteelTide;

internal sealed class AuthoredOpticsVisual
{
    internal AuthoredOpticsVisual(Node3D root)
    {
        Root = root;
        Micro = CombatModelLibrary.RequireNode(root, "MicroOptic");
        Holo = CombatModelLibrary.RequireNode(root, "HoloOptic");
        Scope = CombatModelLibrary.RequireNode(root, "ScopeOptic");
        MicroGeometry = CombatModelLibrary.RequireNode(root, "MicroGeometry");
        HoloGeometry = CombatModelLibrary.RequireNode(root, "HoloGeometry");
        ScopeGeometry = CombatModelLibrary.RequireNode(root, "ScopeGeometry");
        MicroReticleAnchor = CombatModelLibrary.RequireNode(root, "MicroReticleAnchor");
        HoloReticleAnchor = CombatModelLibrary.RequireNode(root, "HoloReticleAnchor");
        ScopeReticleAnchor = CombatModelLibrary.RequireNode(root, "ScopeReticleAnchor");
        MicroRearApertureAnchor = CombatModelLibrary.RequireNode(
            Micro,
            "MicroRearApertureAnchor");
        MicroFrontApertureAnchor = CombatModelLibrary.RequireNode(
            Micro,
            "MicroFrontApertureAnchor");
        HoloRearApertureAnchor = CombatModelLibrary.RequireNode(
            Holo,
            "HoloRearApertureAnchor");
        HoloFrontApertureAnchor = CombatModelLibrary.RequireNode(
            Holo,
            "HoloFrontApertureAnchor");
        ScopeRearApertureAnchor = CombatModelLibrary.RequireNode(
            Scope,
            "ScopeRearApertureAnchor");
        ScopeFrontApertureAnchor = CombatModelLibrary.RequireNode(
            Scope,
            "ScopeFrontApertureAnchor");
        RequireDirectChild(Micro, MicroGeometry);
        RequireDirectChild(Micro, MicroReticleAnchor);
        RequireDirectChild(Micro, MicroRearApertureAnchor);
        RequireDirectChild(Micro, MicroFrontApertureAnchor);
        RequireDirectChild(Holo, HoloGeometry);
        RequireDirectChild(Holo, HoloReticleAnchor);
        RequireDirectChild(Holo, HoloRearApertureAnchor);
        RequireDirectChild(Holo, HoloFrontApertureAnchor);
        RequireDirectChild(Scope, ScopeGeometry);
        RequireDirectChild(Scope, ScopeReticleAnchor);
        RequireDirectChild(Scope, ScopeRearApertureAnchor);
        RequireDirectChild(Scope, ScopeFrontApertureAnchor);
        Configure(null, showExternalModel: false);
    }

    public Node3D Root { get; }
    public Node3D Micro { get; }
    public Node3D Holo { get; }
    public Node3D Scope { get; }
    public Node3D MicroGeometry { get; }
    public Node3D HoloGeometry { get; }
    public Node3D ScopeGeometry { get; }
    public Node3D MicroReticleAnchor { get; }
    public Node3D HoloReticleAnchor { get; }
    public Node3D ScopeReticleAnchor { get; }
    public Node3D MicroRearApertureAnchor { get; }
    public Node3D MicroFrontApertureAnchor { get; }
    public Node3D HoloRearApertureAnchor { get; }
    public Node3D HoloFrontApertureAnchor { get; }
    public Node3D ScopeRearApertureAnchor { get; }
    public Node3D ScopeFrontApertureAnchor { get; }
    public Node3D? ActiveReticleAnchor { get; private set; }
    public Node3D? ActiveRearApertureAnchor { get; private set; }
    public Node3D? ActiveFrontApertureAnchor { get; private set; }

    public bool ActiveGeometryVisible
        => Micro.Visible
            ? HasVisibleRenderableGeometry(MicroGeometry)
            : Holo.Visible
                ? HasVisibleRenderableGeometry(HoloGeometry)
                : Scope.Visible
                    && HasVisibleRenderableGeometry(ScopeGeometry);

    public bool Configure(string? opticId, bool showExternalModel)
    {
        var micro = showExternalModel && opticId == "optic_micro";
        var holo = showExternalModel && opticId == "optic_holo";
        var scope = showExternalModel
            && opticId is "optic_scope" or "optic_7x" or "optic_sniper";
        var knownOptic = micro || holo || scope;
        if (showExternalModel && !knownOptic)
        {
            throw new InvalidOperationException(
                $"Authored optic presentation has no model for {opticId ?? "none"}.");
        }

        Root.Visible = knownOptic;
        Micro.Visible = micro;
        Holo.Visible = holo;
        Scope.Visible = scope;
        ActiveReticleAnchor = micro
            ? MicroReticleAnchor
            : holo
                ? HoloReticleAnchor
                : scope
                    ? ScopeReticleAnchor
                    : null;
        ActiveRearApertureAnchor = micro
            ? MicroRearApertureAnchor
            : holo
                ? HoloRearApertureAnchor
                : scope
                    ? ScopeRearApertureAnchor
                    : null;
        ActiveFrontApertureAnchor = micro
            ? MicroFrontApertureAnchor
            : holo
                ? HoloFrontApertureAnchor
                : scope
                    ? ScopeFrontApertureAnchor
                    : null;
        return knownOptic;
    }

    public bool PresentationMatches(string? opticId, bool externalExpected)
    {
        var expectedMicro = externalExpected && opticId == "optic_micro";
        var expectedHolo = externalExpected && opticId == "optic_holo";
        var expectedScope = externalExpected
            && opticId is "optic_scope" or "optic_7x" or "optic_sniper";
        var expectedVisible = expectedMicro || expectedHolo || expectedScope;
        var microGeometryVisible = HasVisibleRenderableGeometry(MicroGeometry);
        var holoGeometryVisible = HasVisibleRenderableGeometry(HoloGeometry);
        var scopeGeometryVisible = HasVisibleRenderableGeometry(ScopeGeometry);
        return Root.Visible == expectedVisible
            && Micro.Visible == expectedMicro
            && Holo.Visible == expectedHolo
            && Scope.Visible == expectedScope
            && microGeometryVisible == expectedMicro
            && holoGeometryVisible == expectedHolo
            && scopeGeometryVisible == expectedScope
            && ReferenceEquals(
                ActiveReticleAnchor,
                expectedMicro
                    ? MicroReticleAnchor
                    : expectedHolo
                        ? HoloReticleAnchor
                        : expectedScope
                            ? ScopeReticleAnchor
                            : null)
            && ReferenceEquals(
                ActiveRearApertureAnchor,
                expectedMicro
                    ? MicroRearApertureAnchor
                    : expectedHolo
                        ? HoloRearApertureAnchor
                        : expectedScope
                            ? ScopeRearApertureAnchor
                            : null)
            && ReferenceEquals(
                ActiveFrontApertureAnchor,
                expectedMicro
                    ? MicroFrontApertureAnchor
                    : expectedHolo
                        ? HoloFrontApertureAnchor
                        : expectedScope
                            ? ScopeFrontApertureAnchor
                            : null);
    }

    private static bool HasVisibleRenderableGeometry(Node3D geometry)
    {
        if (!geometry.IsVisibleInTree())
        {
            return false;
        }

        if (geometry is MeshInstance3D rootMesh && HasVisibleTriangles(rootMesh))
        {
            return true;
        }
        foreach (var meshInstance in CombatModelLibrary.MeshesBelow(geometry))
        {
            if (HasVisibleTriangles(meshInstance))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasVisibleTriangles(MeshInstance3D meshInstance)
    {
        if (!meshInstance.IsVisibleInTree()
            || meshInstance.Mesh is not ArrayMesh mesh
            || mesh.GetSurfaceCount() == 0)
        {
            return false;
        }

        for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            if (mesh.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
            {
                continue;
            }

            var vertexCount = mesh.SurfaceGetArrayLen(surface);
            var indexCount = mesh.SurfaceGetArrayIndexLen(surface);
            if (vertexCount >= 3 && (indexCount > 0 ? indexCount : vertexCount) >= 3)
            {
                return true;
            }
        }
        return false;
    }

    private static void RequireDirectChild(Node3D expectedParent, Node3D child)
    {
        if (!ReferenceEquals(child.GetParent(), expectedParent))
        {
            throw new InvalidOperationException(
                $"Authored optic node {child.Name} is not a direct child of {expectedParent.Name}.");
        }
    }
}

internal readonly record struct AuthoredOpticsInspection(
    bool Loaded,
    bool RequiredNodes,
    bool AxisAnchorsValid,
    int MeshCount,
    int MaterialCount,
    int VertexCount,
    int TriangleCount,
    Vector3 MicroSize,
    Vector3 HoloSize,
    Vector3 ScopeSize)
{
    public bool Valid => Loaded
        && RequiredNodes
        && AxisAnchorsValid
        && MeshCount == 3
        && MaterialCount == 6
        && VertexCount >= 2_300
        && TriangleCount >= 1_200
        && MicroSize.X > 0.08f
        && HoloSize.X > MicroSize.X
        && ScopeSize.Z > HoloSize.Z * 2.5f
        && ScopeSize.Z > MicroSize.Z * 3.0f;
}

internal static partial class CombatModelLibrary
{
    internal const string AuthoredOpticsScenePath =
        "res://assets/models/steel_tide_optics/steel_tide_optics.glb";

    private static readonly string[] AuthoredOpticsNodes =
    {
        "SteelTideAuthoredOptics",
        "MicroOptic",
        "MicroGeometry",
        "MicroReticleAnchor",
        "MicroRearApertureAnchor",
        "MicroFrontApertureAnchor",
        "HoloOptic",
        "HoloGeometry",
        "HoloReticleAnchor",
        "HoloRearApertureAnchor",
        "HoloFrontApertureAnchor",
        "ScopeOptic",
        "ScopeGeometry",
        "ScopeReticleAnchor",
        "ScopeRearApertureAnchor",
        "ScopeFrontApertureAnchor"
    };

    public static AuthoredOpticsVisual InstantiateAuthoredOptics(bool firstPerson)
    {
        var root = InstantiateRequired(AuthoredOpticsScenePath, AuthoredOpticsNodes);
        root.Name = "AuthoredOpticsVisual";
        if (firstPerson)
        {
            foreach (var geometry in GeometryBelow(root))
            {
                geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
        return new AuthoredOpticsVisual(root);
    }

    public static AuthoredOpticsInspection InspectAuthoredOptics()
    {
        AuthoredOpticsVisual? visual = null;
        try
        {
            visual = InstantiateAuthoredOptics(firstPerson: false);
            var geometry = CountGeometry(MeshesBelow(visual.Root));
            var micro = ComputeBounds(visual.Micro);
            var holo = ComputeBounds(visual.Holo);
            var scope = ComputeBounds(visual.Scope);
            var requiredNodes = micro.MeshCount == 1
                && holo.MeshCount == 1
                && scope.MeshCount == 1
                && ReferenceEquals(visual.MicroGeometry.GetParent(), visual.Micro)
                && ReferenceEquals(visual.HoloGeometry.GetParent(), visual.Holo)
                && ReferenceEquals(visual.ScopeGeometry.GetParent(), visual.Scope);
            var axisAnchorsValid = ApertureAxisValid(
                    visual.Micro,
                    visual.MicroRearApertureAnchor,
                    visual.MicroFrontApertureAnchor,
                    visual.MicroReticleAnchor)
                && ApertureAxisValid(
                    visual.Holo,
                    visual.HoloRearApertureAnchor,
                    visual.HoloFrontApertureAnchor,
                    visual.HoloReticleAnchor)
                && ApertureAxisValid(
                    visual.Scope,
                    visual.ScopeRearApertureAnchor,
                    visual.ScopeFrontApertureAnchor,
                    visual.ScopeReticleAnchor);
            return new AuthoredOpticsInspection(
                true,
                requiredNodes,
                axisAnchorsValid,
                micro.MeshCount + holo.MeshCount + scope.MeshCount,
                CountMaterials(visual.Root),
                geometry.VertexCount,
                geometry.TriangleCount,
                micro.Size,
                holo.Size,
                scope.Size);
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

    private static bool ApertureAxisValid(
        Node3D optic,
        Node3D rear,
        Node3D front,
        Node3D reticle)
    {
        var axis = front.Position - rear.Position;
        return ReferenceEquals(rear.GetParent(), optic)
            && ReferenceEquals(front.GetParent(), optic)
            && rear.Position.DistanceTo(reticle.Position) <= 0.001f
            && axis.Length() >= 0.05f
            && Mathf.Abs(axis.X) <= 0.001f
            && Mathf.Abs(axis.Y) <= 0.001f
            && axis.Z < -0.05f;
    }
}
