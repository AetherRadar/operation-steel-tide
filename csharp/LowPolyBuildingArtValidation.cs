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
        var collisions = artChildren
            .OfType<StaticBody3D>()
            .Where(body => body.IsInGroup("low_poly_roof_collision"))
            .ToList();
        if (visuals.Count != 3 || collisions.Count != 1)
        {
            return false;
        }

        var totalInstances = 0;
        foreach (var visual in visuals)
        {
            var multiMesh = visual.Multimesh;
            if (multiMesh?.Mesh is null
                || multiMesh.InstanceCount <= 0
                || visual.MaterialOverride is not ShaderMaterial shaderMaterial
                || shaderMaterial.Shader is null
                || visual.GetMeta("low_poly_style", string.Empty).AsString()
                    != LowPolyBuildingArtBuilder.StyleId)
            {
                return false;
            }
            totalInstances += multiMesh.InstanceCount;
        }

        var collisionChildren = collisions[0].GetChildren();
        using var collisionChildrenBacking = collisionChildren.AsDisposable();
        var collisionCount = collisionChildren
            .OfType<CollisionShape3D>()
            .Count(shape => shape.Shape is BoxShape3D);
        return totalInstances >= minimumInstanceCount
            && collisionCount > 0
            && collisionCount == building
                .GetMeta("low_poly_roof_collision_count", 0)
                .AsInt32();
    }
}
