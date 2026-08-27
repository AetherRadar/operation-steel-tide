using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed partial class LowPolyBuildingArtBuilder
{
    private int AddCollision(
        Node3D parent,
        string name,
        string group,
        IReadOnlyList<Transform3D> boxTransforms,
        IReadOnlyList<Transform3D> prismTransforms,
        IReadOnlyList<Transform3D> cylinderTransforms)
    {
        var body = new StaticBody3D
        {
            Name = name,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        body.AddToGroup(group);
        parent.AddChild(body);
        var collisionIndex = 0;
        foreach (var transform in boxTransforms)
        {
            body.AddChild(new CollisionShape3D
            {
                Name = $"ArtShape{collisionIndex:00}",
                Transform = new Transform3D(
                    transform.Basis.Orthonormalized(),
                    transform.Origin),
                Shape = new BoxShape3D { Size = transform.Basis.Scale.Abs() }
            });
            collisionIndex++;
        }
        foreach (var transform in prismTransforms)
        {
            body.AddChild(new CollisionShape3D
            {
                Name = $"ArtShape{collisionIndex:00}",
                Transform = new Transform3D(
                    transform.Basis.Orthonormalized(),
                    transform.Origin),
                Shape = CreatePrismCollisionShape(transform.Basis.Scale.Abs())
            });
            collisionIndex++;
        }
        foreach (var transform in cylinderTransforms)
        {
            var size = transform.Basis.Scale.Abs();
            body.AddChild(new CollisionShape3D
            {
                Name = $"ArtShape{collisionIndex:00}",
                Transform = new Transform3D(
                    transform.Basis.Orthonormalized(),
                    transform.Origin),
                Shape = new CylinderShape3D
                {
                    Radius = Mathf.Max(size.X, size.Z) * 0.5f,
                    Height = size.Y
                }
            });
            collisionIndex++;
        }
        return collisionIndex;
    }

    private static ConvexPolygonShape3D CreatePrismCollisionShape(Vector3 size)
    {
        var halfWidth = size.X * 0.5f;
        var halfHeight = size.Y * 0.5f;
        var halfDepth = size.Z * 0.5f;
        return new ConvexPolygonShape3D
        {
            Points = new[]
            {
                new Vector3(-halfWidth, -halfHeight, -halfDepth),
                new Vector3(halfWidth, -halfHeight, -halfDepth),
                new Vector3(0.0f, halfHeight, -halfDepth),
                new Vector3(-halfWidth, -halfHeight, halfDepth),
                new Vector3(halfWidth, -halfHeight, halfDepth),
                new Vector3(0.0f, halfHeight, halfDepth)
            }
        };
    }
}
