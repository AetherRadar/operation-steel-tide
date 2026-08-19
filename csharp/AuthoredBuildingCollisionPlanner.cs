using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal readonly record struct AuthoredCollisionBox(
    string Name,
    Vector3 Position,
    Vector3 Size);

internal readonly record struct AuthoredDoorwayMetrics(
    float Width,
    float Height);

/// <summary>Plans box-only collision for imported map buildings.</summary>
internal sealed class AuthoredBuildingCollisionPlanner
{
    public IReadOnlyList<AuthoredCollisionBox> Plan(
        Vector3 size,
        Vector3 offset,
        bool hasDoorway)
    {
        if (!hasDoorway)
        {
            return new[] { new AuthoredCollisionBox("ModelCollision", offset, size) };
        }

        var doorway = DoorwayMetrics(size);
        var sideWidth = Mathf.Max(0.35f, (size.X - doorway.Width) * 0.5f);
        var wallThickness = Mathf.Clamp(size.Z * 0.12f, 0.18f, 0.72f);
        var rearDepth = Mathf.Min(wallThickness, size.Z * 0.25f);
        var sideCenter = (doorway.Width + sideWidth) * 0.5f;
        return new[]
        {
            new AuthoredCollisionBox(
                "DoorwayWallL",
                offset + new Vector3(-sideCenter, 0, 0),
                new Vector3(sideWidth, size.Y, size.Z)),
            new AuthoredCollisionBox(
                "DoorwayWallR",
                offset + new Vector3(sideCenter, 0, 0),
                new Vector3(sideWidth, size.Y, size.Z)),
            new AuthoredCollisionBox(
                "DoorwayLintel",
                offset + new Vector3(0, doorway.Height * 0.5f, 0),
                new Vector3(doorway.Width, size.Y - doorway.Height, size.Z)),
            new AuthoredCollisionBox(
                "DoorwayRearWall",
                new Vector3(
                    offset.X,
                    doorway.Height * 0.5f,
                    offset.Z - Mathf.Max(0, (size.Z - rearDepth) * 0.5f)),
                new Vector3(doorway.Width, doorway.Height, rearDepth))
        };
    }

    public AuthoredDoorwayMetrics DoorwayMetrics(Vector3 size)
        => new(
            Mathf.Clamp(size.X * 0.22f, 1.25f, 3.8f),
            Mathf.Clamp(size.Y * 0.34f, 2.05f, 3.6f));
}
