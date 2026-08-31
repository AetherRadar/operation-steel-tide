using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static bool TideglassWalkwayCollisionReady(
        Node3D arenaRoot,
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failure)
    {
        failure = "none";
        var model = dressingRoot?.GetNodeOrNull<Node3D>("CivicElevatedWalkway");
        var body = arenaRoot.GetNodeOrNull<StaticBody3D>("CivicElevatedWalkwayAuthoredCollision");
        if (!IsInstanceValid(model)
            || !IsInstanceValid(body)
            || body!.GetMeta("authored_source_model").AsString() != "CivicElevatedWalkway"
            || layout.CollisionBoxes.Any(box => box.Name.StartsWith("CivicWalkway", StringComparison.Ordinal)))
        {
            failure = "configuration";
            return false;
        }

        var meshNodes = model!.FindChildren("*", "MeshInstance3D", true, false);
        using var meshNodesBacking = meshNodes.AsDisposable();
        var meshes = meshNodes.OfType<MeshInstance3D>().ToArray();
        var floors = meshes.Where(mesh => mesh.Name.ToString().Contains(
            "IndFloorGreyPlatformFull",
            StringComparison.Ordinal)).ToArray();
        var stairs = meshes.Where(mesh => mesh.Name.ToString().Contains(
            "IndStairsWideFull",
            StringComparison.Ordinal)).ToArray();
        var pillars = meshes.Where(mesh => mesh.Name.ToString().Contains(
            "IndColumnFree",
            StringComparison.Ordinal)).ToArray();
        var rails = meshes.Where(mesh => mesh.Name.ToString().Contains(
            "IndRoofTrimBStraightFull",
            StringComparison.Ordinal)).ToArray();
        var compositionReady = meshes.Length == 22
            && floors.Length == 4
            && stairs.Length == 2
            && pillars.Length == 8
            && rails.Length == 8
            && meshes.All(mesh => !mesh.Name.ToString().Contains("Platform45", StringComparison.Ordinal))
            && meshes.All(mesh => !mesh.Name.ToString().Contains("StairsCorner", StringComparison.Ordinal))
            && body.GetMeta("authored_shape_count").AsInt32() == meshes.Length;
        if (!compositionReady)
        {
            failure = $"composition={meshes.Length}:{floors.Length}:{stairs.Length}:{pillars.Length}:{rails.Length}"
                + $":shapes={body.GetMeta("authored_shape_count").AsInt32()}";
            return false;
        }

        var shapeNodes = body.FindChildren("*", "CollisionShape3D", true, false);
        using var shapeNodesBacking = shapeNodes.AsDisposable();
        var backfacesReady = shapeNodes
            .OfType<CollisionShape3D>()
            .Count(shape => shape.Shape is ConcavePolygonShape3D { BackfaceCollision: true }) == meshes.Length;
        if (!backfacesReady)
        {
            failure = $"backfaces={backfacesReady}:shapes={shapeNodes.Count}/{meshes.Length}";
            return false;
        }

        if (!TideglassTryGetMeshFaceBounds(floors, out var floorMinimum, out var floorMaximum)
            || !TideglassTryGetMeshFaceBounds(stairs, out _, out _))
        {
            failure = "bounds-missing";
            return false;
        }
        var stairBounds = stairs.Select(stair =>
        {
            var ready = TideglassTryGetMeshFaceBounds(
                new[] { stair },
                out var minimum,
                out var maximum);
            return (Ready: ready, Minimum: minimum, Maximum: maximum);
        }).OrderBy(bounds => bounds.Minimum.X).ToArray();
        var left = stairBounds[0];
        var right = stairBounds[1];
        const float tolerance = 0.015f;
        var stairsMeetDeck = left.Ready
            && right.Ready
            && Mathf.Abs(left.Maximum.X - floorMinimum.X) <= tolerance
            && Mathf.Abs(right.Minimum.X - floorMaximum.X) <= tolerance
            && Mathf.Abs(left.Minimum.X + right.Maximum.X - layout.Origin.X * 2.0f) <= tolerance
            && Mathf.Abs(left.Maximum.X + right.Minimum.X - layout.Origin.X * 2.0f) <= tolerance
            && Mathf.Abs(left.Minimum.Z - floorMinimum.Z) <= tolerance
            && Mathf.Abs(left.Maximum.Z - floorMaximum.Z) <= tolerance
            && Mathf.Abs(right.Minimum.Z - floorMinimum.Z) <= tolerance
            && Mathf.Abs(right.Maximum.Z - floorMaximum.Z) <= tolerance
            && Mathf.Abs(left.Maximum.Y - floorMaximum.Y) <= tolerance
            && Mathf.Abs(right.Maximum.Y - floorMaximum.Y) <= tolerance;
        if (!stairsMeetDeck)
        {
            failure = $"deck={floorMinimum}..{floorMaximum}:left={left.Minimum}..{left.Maximum}"
                + $":right={right.Minimum}..{right.Maximum}";
            return false;
        }

        var floorBounds = floors.Select(floor =>
        {
            var ready = TideglassTryGetMeshFaceBounds(
                new[] { floor },
                out var minimum,
                out var maximum);
            return (Ready: ready, Minimum: minimum, Maximum: maximum);
        }).ToArray();
        var railBounds = rails.Select(rail =>
        {
            var ready = TideglassTryGetMeshFaceBounds(
                new[] { rail },
                out var minimum,
                out var maximum);
            return (Ready: ready, Minimum: minimum, Maximum: maximum);
        }).ToArray();
        var maximumRailBaseGap = railBounds.Max(rail => floorBounds.Min(floor =>
            Mathf.Abs(rail.Minimum.Y - floor.Maximum.Y)));
        var railsMeetStructure = floorBounds.All(floor => floor.Ready)
            && railBounds.All(rail => rail.Ready)
            && maximumRailBaseGap <= tolerance
            && floorBounds.All(floor => railBounds.Count(rail =>
                Mathf.Abs(
                    (rail.Minimum.X + rail.Maximum.X) * 0.5f
                    - (floor.Minimum.X + floor.Maximum.X) * 0.5f) <= tolerance) == 2)
            && railBounds.All(rail =>
            {
                var floor = floorBounds.MinBy(candidate => Mathf.Abs(
                    (rail.Minimum.X + rail.Maximum.X) * 0.5f
                    - (candidate.Minimum.X + candidate.Maximum.X) * 0.5f));
                var crossesMinimumEdge = rail.Minimum.Z <= floor.Minimum.Z + tolerance
                    && rail.Maximum.Z >= floor.Minimum.Z - tolerance;
                var crossesMaximumEdge = rail.Minimum.Z <= floor.Maximum.Z + tolerance
                    && rail.Maximum.Z >= floor.Maximum.Z - tolerance;
                return crossesMinimumEdge != crossesMaximumEdge;
            });
        if (!railsMeetStructure)
        {
            var floorReport = string.Join('|', floorBounds.Select(floor =>
                $"{(floor.Minimum.X + floor.Maximum.X) * 0.5f:0.000}"
                + $"@{floor.Minimum.Z:0.000}..{floor.Maximum.Z:0.000}"));
            var railReport = string.Join('|', railBounds.Select(rail =>
                $"{(rail.Minimum.X + rail.Maximum.X) * 0.5f:0.000}"
                + $"@{rail.Minimum.Z:0.000}..{rail.Maximum.Z:0.000}"));
            failure = $"rails-attached={railsMeetStructure}:base-gap={maximumRailBaseGap:0.0000}"
                + $":floors={floorReport}:rails={railReport}";
            return false;
        }

        var expectedPillars = new[]
        {
            new Vector3(-3.45f, 1.745f, 25.6375f),
            new Vector3(-3.45f, 1.745f, 27.3625f),
            new Vector3(-1.15f, 1.745f, 25.6375f),
            new Vector3(-1.15f, 1.745f, 27.3625f),
            new Vector3(1.15f, 1.745f, 25.6375f),
            new Vector3(1.15f, 1.745f, 27.3625f),
            new Vector3(3.45f, 1.745f, 25.6375f),
            new Vector3(3.45f, 1.745f, 27.3625f)
        };
        var navigationPillars = layout.NavigationBoxes
            .Where(box => box.Name.StartsWith("CivicWalkwayPillarNavigation_", StringComparison.Ordinal))
            .OrderBy(box => box.Name, StringComparer.Ordinal)
            .ToArray();
        var westStairNavigation = layout.NavigationBoxes.SingleOrDefault(
            box => box.Name == "CivicWalkwayWestStairNavigation");
        var eastStairNavigation = layout.NavigationBoxes.SingleOrDefault(
            box => box.Name == "CivicWalkwayEastStairNavigation");
        var towerNavigation = layout.NavigationBoxes.SingleOrDefault(
            box => box.Name == "ConstructionTowerNavigationFootprint");
        var navigationReady = layout.NavigationBoxes.Count == 14
            && navigationPillars.Length == expectedPillars.Length
            && navigationPillars.Select((pillar, index) =>
                    (pillar.Center - layout.Origin).DistanceTo(expectedPillars[index]) <= tolerance
                    && pillar.Size.IsEqualApprox(new Vector3(0.46f, 3.45f, 0.46f)))
                .All(ready => ready)
            && westStairNavigation.Name == "CivicWalkwayWestStairNavigation"
            && (westStairNavigation.Center - layout.Origin).DistanceTo(
                new Vector3(-5.75f, 1.17f, 26.5f)) <= tolerance
            && westStairNavigation.Size.IsEqualApprox(new Vector3(2.3f, 2.3f, 2.3f))
            && eastStairNavigation.Name == "CivicWalkwayEastStairNavigation"
            && (eastStairNavigation.Center - layout.Origin).DistanceTo(
                new Vector3(5.75f, 1.17f, 26.5f)) <= tolerance
            && eastStairNavigation.Size.IsEqualApprox(new Vector3(2.3f, 2.3f, 2.3f))
            && towerNavigation.Name == "ConstructionTowerNavigationFootprint"
            && (towerNavigation.Center - layout.Origin).DistanceTo(
                new Vector3(-55.0f, 0.92f, 22.0f)) <= tolerance
            && towerNavigation.Size.IsEqualApprox(new Vector3(13.9f, 1.8f, 16.6f));
        if (!navigationReady)
        {
            failure = $"navigation={layout.NavigationBoxes.Count}:{navigationPillars.Length}";
            return false;
        }
        failure = $"meshes={meshes.Length}:shapes={body.GetMeta("authored_shape_count").AsInt32()}"
            + $":floors={floors.Length}:stairs={stairs.Length}:pillars={pillars.Length}:rails={rails.Length}"
            + $":rail-gap={maximumRailBaseGap:0.0000}:backfaces={backfacesReady}";
        return true;
    }

    private static bool TideglassTryGetMeshFaceBounds(
        IEnumerable<MeshInstance3D> meshes,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (var mesh in meshes)
        {
            if (mesh.Mesh is null)
            {
                continue;
            }
            foreach (var face in mesh.Mesh.GetFaces())
            {
                var point = mesh.ToGlobal(face);
                minimum = new Vector3(
                    Mathf.Min(minimum.X, point.X),
                    Mathf.Min(minimum.Y, point.Y),
                    Mathf.Min(minimum.Z, point.Z));
                maximum = new Vector3(
                    Mathf.Max(maximum.X, point.X),
                    Mathf.Max(maximum.Y, point.Y),
                    Mathf.Max(maximum.Z, point.Z));
            }
        }
        return !float.IsInfinity(minimum.X);
    }

    private static bool TideglassGatewayCollisionReady(
        Node3D? dressingRoot,
        DemolitionArenaLayout layout,
        out string failure)
    {
        failure = "none";
        var model = dressingRoot?.GetNodeOrNull<Node3D>("OrangeArchGateway");
        var west = layout.CollisionBoxes.SingleOrDefault(box => box.Name == "GatewayWestPillar");
        var east = layout.CollisionBoxes.SingleOrDefault(box => box.Name == "GatewayEastPillar");
        if (!IsInstanceValid(model)
            || west.Name != "GatewayWestPillar"
            || east.Name != "GatewayEastPillar"
            || !west.Size.IsEqualApprox(new Vector3(0.85f, 4.0f, 0.75f))
            || !east.Size.IsEqualApprox(new Vector3(0.85f, 4.0f, 0.75f)))
        {
            failure = "configuration";
            return false;
        }

        const float eyeHeight = 1.57f;
        const float sampleStep = 0.0125f;
        var westVisibleMinimum = float.PositiveInfinity;
        var westVisibleMaximum = float.NegativeInfinity;
        var eastVisibleMinimum = float.PositiveInfinity;
        var eastVisibleMaximum = float.NegativeInfinity;
        for (var localX = -3.2f; localX <= 3.2f; localX += sampleStep)
        {
            var rayFrom = layout.Origin + new Vector3(localX, eyeHeight, -32.2f);
            var rayTo = layout.Origin + new Vector3(localX, eyeHeight, -30.8f);
            var visible = TideglassVisibleMeshBlocksSegment(model!, rayFrom, rayTo);
            if (!visible)
            {
                continue;
            }
            if (localX < 0.0f)
            {
                westVisibleMinimum = Mathf.Min(westVisibleMinimum, localX);
                westVisibleMaximum = Mathf.Max(westVisibleMaximum, localX);
            }
            else
            {
                eastVisibleMinimum = Mathf.Min(eastVisibleMinimum, localX);
                eastVisibleMaximum = Mathf.Max(eastVisibleMaximum, localX);
            }
        }

        var westMinimum = west.Center.X - layout.Origin.X - west.Size.X * 0.5f;
        var westMaximum = west.Center.X - layout.Origin.X + west.Size.X * 0.5f;
        var eastMinimum = east.Center.X - layout.Origin.X - east.Size.X * 0.5f;
        var eastMaximum = east.Center.X - layout.Origin.X + east.Size.X * 0.5f;
        const float coverageTolerance = sampleStep * 1.5f;
        const float maximumExtra = 0.06f;
        var visibleRangesReady = !float.IsInfinity(westVisibleMinimum)
            && !float.IsInfinity(eastVisibleMinimum);
        var westReady = visibleRangesReady
            && westMinimum <= westVisibleMinimum + coverageTolerance
            && westMaximum >= westVisibleMaximum - coverageTolerance
            && westVisibleMinimum - westMinimum <= maximumExtra
            && westMaximum - westVisibleMaximum <= maximumExtra;
        var eastReady = visibleRangesReady
            && eastMinimum <= eastVisibleMinimum + coverageTolerance
            && eastMaximum >= eastVisibleMaximum - coverageTolerance
            && eastVisibleMinimum - eastMinimum <= maximumExtra
            && eastMaximum - eastVisibleMaximum <= maximumExtra;
        if (!westReady || !eastReady)
        {
            failure = $"visible={westVisibleMinimum:0.000}..{westVisibleMaximum:0.000}"
                + $"|{eastVisibleMinimum:0.000}..{eastVisibleMaximum:0.000}"
                + $":collision={westMinimum:0.000}..{westMaximum:0.000}"
                + $"|{eastMinimum:0.000}..{eastMaximum:0.000}";
            return false;
        }
        return true;
    }
}
