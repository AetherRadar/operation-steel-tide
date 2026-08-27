using System.Linq;
using Godot;

namespace OperationSteelTide;

internal static class LowPolyBuildingArtValidation
{
    public static bool IsRenderable(
        Node3D building,
        string artGroup,
        int minimumInstanceCount)
    {
        var buildingChildren = building.GetChildren();
        using var buildingChildrenBacking = buildingChildren.AsDisposable();
        var artRoots = buildingChildren
            .OfType<Node3D>()
            .Where(child => child.IsInGroup(artGroup)
                && child.GetMeta("low_poly_style", string.Empty).AsString()
                    == LowPolyBuildingArtBuilder.StyleId)
            .ToList();
        if (artRoots.Count != 1)
        {
            return false;
        }

        var artChildren = artRoots[0].GetChildren();
        using var artChildrenBacking = artChildren.AsDisposable();
        var visuals = artChildren.OfType<MultiMeshInstance3D>().ToList();
        var roofCollisions = artChildren
            .OfType<StaticBody3D>()
            .Where(body => body.IsInGroup("low_poly_roof_collision"))
            .ToList();
        var massingCollisions = artChildren
            .OfType<StaticBody3D>()
            .Where(body => body.IsInGroup("low_poly_massing_collision"))
            .ToList();
        if (visuals.Count != 5
            || roofCollisions.Count != 1
            || massingCollisions.Count != 1)
        {
            return false;
        }

        var totalInstances = 0;
        var boxBatches = 0;
        var prismBatches = 0;
        var cylinderBatches = 0;
        foreach (var visual in visuals)
        {
            var multiMesh = visual.Multimesh;
            if (multiMesh?.Mesh is null
                || multiMesh.InstanceCount <= 0
                || visual.MaterialOverride is not ShaderMaterial shaderMaterial
                || shaderMaterial.Shader is null
                || !shaderMaterial.GetMeta("low_poly_gradient", false).AsBool()
                || shaderMaterial.GetMeta("low_poly_gradient_height", 0.0f).AsSingle() <= 0.0f
                || visual.GetMeta("low_poly_style", string.Empty).AsString()
                    != LowPolyBuildingArtBuilder.StyleId)
            {
                return false;
            }
            boxBatches += multiMesh.Mesh is BoxMesh ? 1 : 0;
            prismBatches += multiMesh.Mesh is PrismMesh ? 1 : 0;
            cylinderBatches += multiMesh.Mesh is CylinderMesh ? 1 : 0;
            totalInstances += multiMesh.InstanceCount;
        }

        var roofCollisionChildren = roofCollisions[0].GetChildren();
        using var roofCollisionChildrenBacking = roofCollisionChildren.AsDisposable();
        var roofCollisionShapes = roofCollisionChildren
            .OfType<CollisionShape3D>()
            .Where(shape => shape.Shape is BoxShape3D
                or ConvexPolygonShape3D
                or CylinderShape3D)
            .ToList();
        var roofCollisionCount = roofCollisionShapes.Count;
        var massingCollisionChildren = massingCollisions[0].GetChildren();
        using var massingCollisionChildrenBacking = massingCollisionChildren.AsDisposable();
        var massingCollisionShapes = massingCollisionChildren
            .OfType<CollisionShape3D>()
            .Where(shape => shape.Shape is BoxShape3D or ConvexPolygonShape3D)
            .ToList();
        var massingCollisionCount = massingCollisionShapes.Count;
        return totalInstances >= minimumInstanceCount
            && totalInstances == building.GetMeta("low_poly_detail_count", 0).AsInt32()
            && boxBatches == 3
            && prismBatches == 1
            && cylinderBatches == 1
            && building.GetMeta("low_poly_gradient", false).AsBool()
            && building.GetMeta("low_poly_massing_count", 0).AsInt32() >= 6
            && !string.IsNullOrWhiteSpace(building
                .GetMeta("low_poly_massing_style", string.Empty)
                .AsString())
            && !string.IsNullOrWhiteSpace(building
                .GetMeta("low_poly_architecture_signature", string.Empty)
                .AsString())
            && roofCollisionCount > 0
            && roofCollisionShapes.Any(shape => shape.Shape is ConvexPolygonShape3D)
            && roofCollisionShapes.Any(shape => shape.Shape is CylinderShape3D)
            && roofCollisionCount == building
                .GetMeta("low_poly_roof_collision_count", 0)
                .AsInt32()
            && massingCollisionCount > 0
            && massingCollisionShapes.Any(shape => shape.Shape is ConvexPolygonShape3D)
            && massingCollisionCount == building
                .GetMeta("low_poly_massing_collision_count", 0)
                .AsInt32();
    }
}
