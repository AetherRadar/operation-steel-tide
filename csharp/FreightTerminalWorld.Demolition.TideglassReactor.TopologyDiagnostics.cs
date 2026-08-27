using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static bool TideglassLayoutsDiffer(DemolitionArenaLayout first, DemolitionArenaLayout second)
    {
        var boundsDiffer = first.WorldBounds.Size.DistanceTo(second.WorldBounds.Size) >= 2.0f;
        var spawnsDiffer = HorizontalDistance(first.AttackSpawn, second.AttackSpawn) >= 10.0f
            && HorizontalDistance(first.DefenderSpawn, second.DefenderSpawn) >= 10.0f;
        var routesDiffer = TideglassRoutesDiffer(first.AttackToAPath, second.AttackToAPath)
            || TideglassRoutesDiffer(first.AttackToBPath, second.AttackToBPath)
            || TideglassRoutesDiffer(first.AttackMidPath, second.AttackMidPath);
        var collisionNamesDiffer = !first.CollisionBoxes.Select(box => box.Name)
            .SequenceEqual(second.CollisionBoxes.Select(box => box.Name), StringComparer.Ordinal);
        return first.MapId != second.MapId
            && boundsDiffer
            && spawnsDiffer
            && routesDiffer
            && collisionNamesDiffer;
    }

    private static bool TideglassRoutesDiffer(
        IReadOnlyList<Vector3> first,
        IReadOnlyList<Vector3> second)
    {
        if (first.Count != second.Count)
        {
            return true;
        }
        for (var index = 0; index < first.Count; index++)
        {
            if (HorizontalDistance(first[index], second[index]) >= 3.0f)
            {
                return true;
            }
        }
        return false;
    }

    private static bool TideglassPointsSeparated(IReadOnlyList<Vector3> points, float minimumDistance)
    {
        for (var first = 0; first < points.Count; first++)
        {
            for (var second = first + 1; second < points.Count; second++)
            {
                if (HorizontalDistance(points[first], points[second]) < minimumDistance)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool TideglassRoutesStayInside(DemolitionArenaLayout layout)
        => layout.AttackToAPath
            .Concat(layout.AttackToBPath)
            .Concat(layout.AttackMidPath)
            .Concat(layout.DefenderToAPath)
            .Concat(layout.DefenderToBPath)
            .Concat(layout.SiteRotationPath)
            .All(point => layout.IsInsideArena(point));

    private static bool TideglassPropModelLoaded(Node3D root, DemolitionArenaProp prop)
    {
        var body = root.GetNodeOrNull<StaticBody3D>(prop.Name);
        var model = body?.GetNodeOrNull<Node3D>("Model");
        return IsInstanceValid(body)
            && IsInstanceValid(model)
            && TideglassAuthoredModelHasMesh(model!);
    }

    private static bool TideglassAuthoredModelHasMesh(Node3D model)
    {
        if (model is MeshInstance3D)
        {
            return true;
        }
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
        using var meshesBacking = meshes.AsDisposable();
        return meshes.Count > 0;
    }

    private static string TideglassSourcePack(string path)
    {
        const string root = "res://assets/models/";
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return "outside-model-root";
        }
        var relative = path[root.Length..];
        var separator = relative.IndexOf('/');
        return separator > 0 ? relative[..separator] : relative;
    }
}
