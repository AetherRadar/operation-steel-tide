using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarGuardRailExpectation(
        string Name,
        string TraversalName,
        Vector3 Center,
        Vector3 Size,
        Vector3 Rotation,
        Vector3 ChannelDirection,
        bool StairRail);

    private readonly record struct BazaarGuardRailCheck(
        bool Ready,
        bool ContractReady,
        bool RuntimeReady,
        bool EdgeCoverageReady,
        bool ChannelReady,
        int RailCount,
        int ExactRayHits,
        int EdgeBarrierHits,
        float MinimumChannelClearance,
        string Failures);

    private static BazaarGuardRailCheck BazaarGuardRailPhysicsReady(
        World3D world,
        DemolitionArenaRuntime arena)
    {
        const float positionTolerance = 0.015f;
        const float rotationTolerance = 0.002f;
        var layout = arena.Layout;
        var expected = BazaarGuardRailExpectations(layout);
        var definitions = layout.CollisionBoxes
            .Where(box => box.Name.StartsWith("GuardRail", StringComparison.Ordinal))
            .ToArray();
        var failures = new List<string>();
        var contractReady = expected.Count == 15
            && definitions.Length == 15
            && definitions.Select(box => box.Name).Distinct(StringComparer.Ordinal).Count() == 15
            && layout.TraversalBoxes.All(box =>
                !box.Name.StartsWith("GuardRail", StringComparison.Ordinal));
        if (!contractReady)
        {
            failures.Add($"contract-{expected.Count}/{definitions.Length}");
        }

        var exactRayHits = 0;
        var edgeBarrierHits = 0;
        var runtimeReady = true;
        var edgeCoverageReady = true;
        foreach (var rail in expected)
        {
            var matches = definitions.Where(box => box.Name == rail.Name).ToArray();
            if (matches.Length != 1)
            {
                contractReady = false;
                failures.Add($"definition-{rail.Name}-{matches.Length}");
                continue;
            }
            var definition = matches[0];
            if (!BazaarGuardRailVectorNear(definition.Center, rail.Center, positionTolerance)
                || !BazaarGuardRailVectorNear(definition.Size, rail.Size, positionTolerance)
                || !BazaarGuardRailVectorNear(definition.Rotation, rail.Rotation, rotationTolerance)
                || definition.Visible)
            {
                contractReady = false;
                failures.Add($"geometry-{rail.Name}");
            }

            var traversal = layout.TraversalBoxes.SingleOrDefault(box =>
                box.Name == rail.TraversalName);
            if (string.IsNullOrEmpty(traversal.Name)
                || !BazaarGuardRailCoversTraversalEdge(rail, traversal))
            {
                edgeCoverageReady = false;
                failures.Add($"edge-{rail.Name}");
            }

            var body = arena.Root.GetNodeOrNull<StaticBody3D>(rail.Name);
            var shapeNode = body?.GetNodeOrNull<CollisionShape3D>("Collision");
            var boxShape = shapeNode?.Shape as BoxShape3D;
            var bodyReady = IsInstanceValid(body)
                && body!.CollisionLayer == 1
                && body.CollisionMask == 0
                && BazaarGuardRailVectorNear(body.Position, rail.Center, positionTolerance)
                && BazaarGuardRailVectorNear(body.Rotation, rail.Rotation, rotationTolerance)
                && shapeNode is not null
                && !shapeNode.Disabled
                && boxShape is not null
                && BazaarGuardRailVectorNear(boxShape.Size, rail.Size, positionTolerance)
                && body.GetNodeOrNull<MeshInstance3D>("Visual") is null;
            if (!bodyReady)
            {
                runtimeReady = false;
                failures.Add($"runtime-{rail.Name}");
                continue;
            }

            var rayFrom = rail.Center + rail.ChannelDirection * 1.0f;
            var rayTo = rail.Center - rail.ChannelDirection * 1.0f;
            var exclusions = new Godot.Collections.Array<Rid>();
            using var exclusionsBacking = exclusions.AsDisposable();
            foreach (var other in arena.StaticBodies)
            {
                if (!ReferenceEquals(other, body))
                {
                    exclusions.Add(other.GetRid());
                }
            }
            if (PhysicsRaycast.TryHit(
                    world,
                    rayFrom,
                    rayTo,
                    exclusions,
                    1u,
                    out var exactHit)
                && exactHit.Collider is StaticBody3D exactBody
                && exactBody.Name == rail.Name
                && exactHit.Position.DistanceTo(rail.Center) <= 0.24f)
            {
                exactRayHits++;
            }
            else
            {
                failures.Add($"ray-{rail.Name}");
            }

            if (PhysicsRaycast.TryHit(world, rayFrom, rayTo, 1u, out var barrierHit)
                && barrierHit.Collider is StaticBody3D
                && barrierHit.Position.DistanceTo(rail.Center) <= 0.32f)
            {
                edgeBarrierHits++;
            }
            else
            {
                failures.Add($"barrier-{rail.Name}");
            }
        }

        var stairGroups = expected
            .Where(rail => rail.StairRail)
            .GroupBy(rail => rail.TraversalName, StringComparer.Ordinal)
            .ToArray();
        var minimumChannelClearance = stairGroups
            .Where(group => group.Count() == 2)
            .Select(group =>
            {
                var pair = group.ToArray();
                return BazaarGuardRailHorizontalDistance(pair[0].Center, pair[1].Center)
                    - Mathf.Min(pair[0].Size.X, pair[0].Size.Z);
            })
            .DefaultIfEmpty(0.0f)
            .Min();
        var platformCenterClearance = expected
            .Where(rail => !rail.StairRail)
            .Select(rail =>
            {
                var traversal = layout.TraversalBoxes.Single(box =>
                    box.Name == rail.TraversalName);
                return BazaarGuardRailHorizontalDistance(rail.Center, traversal.Center)
                    - Mathf.Min(rail.Size.X, rail.Size.Z) * 0.5f;
            })
            .DefaultIfEmpty(0.0f)
            .Min();
        var channelReady = stairGroups.Length == 6
            && stairGroups.All(group => group.Count() == 2)
            && minimumChannelClearance >= 3.0f
            && platformCenterClearance >= 2.8f;
        if (!channelReady)
        {
            failures.Add($"channel-{minimumChannelClearance:0.00}/{platformCenterClearance:0.00}");
        }

        runtimeReady &= exactRayHits == expected.Count
            && edgeBarrierHits == expected.Count;
        var ready = contractReady
            && runtimeReady
            && edgeCoverageReady
            && channelReady;
        return new BazaarGuardRailCheck(
            ready,
            contractReady,
            runtimeReady,
            edgeCoverageReady,
            channelReady,
            expected.Count,
            exactRayHits,
            edgeBarrierHits,
            minimumChannelClearance,
            string.Join('|', failures.Take(24)));
    }

    private static IReadOnlyList<BazaarGuardRailExpectation> BazaarGuardRailExpectations(
        DemolitionArenaLayout layout)
    {
        const float thickness = 0.28f;
        const float height = 1.10f;
        var rails = new List<BazaarGuardRailExpectation>(15)
        {
            new(
                "GuardRailAGalleryInner",
                "TraversalAGalleryDeck",
                layout.Origin + new Vector3(-53.0f, 4.15f, -16.4f),
                new Vector3(thickness, height, 14.8f),
                Vector3.Zero,
                Vector3.Left,
                false),
            new(
                "GuardRailBBalconyInner",
                "TraversalBBalconyDeck",
                layout.Origin + new Vector3(53.0f, 3.95f, -16.4f),
                new Vector3(thickness, height, 14.8f),
                Vector3.Zero,
                Vector3.Right,
                false),
            new(
                "GuardRailMidMezzanineInner",
                "TraversalMidMezzanineDeck",
                layout.Origin + new Vector3(-3.0f, 3.75f, 24.0f),
                new Vector3(thickness, height, 14.0f),
                Vector3.Zero,
                Vector3.Left,
                false)
        };
        BazaarAddExpectedStairGuardRails(
            rails,
            layout,
            "GuardRailAGallerySouth",
            "TraversalAGallerySouthRamp",
            new(-56.0f, 0.0f, 2.1f),
            new(-56.0f, 3.6f, -9.0f));
        BazaarAddExpectedStairGuardRails(
            rails,
            layout,
            "GuardRailAGalleryRear",
            "TraversalAGalleryRearRamp",
            new(-41.9f, 0.0f, -27.0f),
            new(-53.0f, 3.6f, -27.0f));
        BazaarAddExpectedStairGuardRails(
            rails,
            layout,
            "GuardRailMidMezzanineSouth",
            "TraversalMidMezzanineSouthRamp",
            new(-6.0f, 0.0f, 40.85f),
            new(-6.0f, 3.2f, 31.0f));
        BazaarAddExpectedStairGuardRails(
            rails,
            layout,
            "GuardRailMidMezzanineNorth",
            "TraversalMidMezzanineNorthRamp",
            new(-6.0f, 0.0f, 7.15f),
            new(-6.0f, 3.2f, 17.0f));
        BazaarAddExpectedStairGuardRails(
            rails,
            layout,
            "GuardRailBBalconySouth",
            "TraversalBBalconySouthRamp",
            new(56.0f, 0.0f, 1.5f),
            new(56.0f, 3.4f, -9.0f));
        BazaarAddExpectedStairGuardRails(
            rails,
            layout,
            "GuardRailBBalconyRear",
            "TraversalBBalconyRearRamp",
            new(42.5f, 0.0f, -27.0f),
            new(53.0f, 3.4f, -27.0f));
        return rails;
    }

    private static void BazaarAddExpectedStairGuardRails(
        List<BazaarGuardRailExpectation> rails,
        DemolitionArenaLayout layout,
        string namePrefix,
        string traversalName,
        Vector3 lowSurface,
        Vector3 highSurface)
    {
        const float thickness = 0.28f;
        const float height = 1.10f;
        const float lateralOffset = 1.66f;
        var delta = highSurface - lowSurface;
        var horizontalRun = new Vector2(delta.X, delta.Z).Length();
        var angle = Mathf.Atan2(delta.Y, horizontalRun);
        var length = delta.Length();
        var runsAlongX = Mathf.Abs(delta.X) > Mathf.Abs(delta.Z);
        var rotation = runsAlongX
            ? new Vector3(0.0f, 0.0f, Mathf.Sign(delta.X) * angle)
            : new Vector3(-Mathf.Sign(delta.Z) * angle, 0.0f, 0.0f);
        var size = runsAlongX
            ? new Vector3(length, height, thickness)
            : new Vector3(thickness, height, length);
        var lateral = new Vector3(-delta.Z, 0.0f, delta.X).Normalized();
        var normal = Basis.FromEuler(rotation) * Vector3.Up;
        var center = layout.Origin + (lowSurface + highSurface) * 0.5f
            + normal * (height * 0.5f);
        rails.Add(new BazaarGuardRailExpectation(
            $"{namePrefix}Left",
            traversalName,
            center - lateral * lateralOffset,
            size,
            rotation,
            lateral,
            true));
        rails.Add(new BazaarGuardRailExpectation(
            $"{namePrefix}Right",
            traversalName,
            center + lateral * lateralOffset,
            size,
            rotation,
            -lateral,
            true));
    }

    private static bool BazaarGuardRailCoversTraversalEdge(
        BazaarGuardRailExpectation rail,
        DemolitionArenaBox traversal)
    {
        const float tolerance = 0.025f;
        if (rail.Size.Y < 1.09f
            || Mathf.Min(rail.Size.X, rail.Size.Z) < 0.27f)
        {
            return false;
        }
        if (rail.StairRail)
        {
            var railLength = Mathf.Max(rail.Size.X, rail.Size.Z);
            var traversalLength = Mathf.Max(traversal.Size.X, traversal.Size.Z);
            return Mathf.Abs(railLength - traversalLength) <= tolerance
                && BazaarGuardRailVectorNear(rail.Rotation, traversal.Rotation, 0.002f);
        }
        var railBottom = rail.Center.Y - rail.Size.Y * 0.5f;
        var deckTop = traversal.Center.Y + traversal.Size.Y * 0.5f;
        return Mathf.Abs(railBottom - deckTop) <= tolerance
            && Mathf.Max(rail.Size.X, rail.Size.Z) >= 14.0f;
    }

    private static bool BazaarGuardRailVectorNear(
        Vector3 left,
        Vector3 right,
        float tolerance)
    {
        var delta = (left - right).Abs();
        return delta.X <= tolerance
            && delta.Y <= tolerance
            && delta.Z <= tolerance;
    }

    private static float BazaarGuardRailHorizontalDistance(Vector3 left, Vector3 right)
        => new Vector2(left.X - right.X, left.Z - right.Z).Length();
}
