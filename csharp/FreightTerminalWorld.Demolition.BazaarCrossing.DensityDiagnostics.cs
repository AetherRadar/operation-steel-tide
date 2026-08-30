using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct BazaarDensityResult(
        bool Ready,
        int SiteVisiblePairs,
        int SitePairCount,
        int HighVisibilityViolations,
        float LongestSightline,
        float LargestOpenDiameter,
        float InteriorRatio,
        float PlantZoneACoverage,
        float PlantZoneBCoverage,
        bool InteriorContractReady,
        bool PlantZoneCoverageReady,
        bool GroundDoorContractReady,
        int DetachedFullHeightBaffleCount,
        bool DefenderRoutesEfficient,
        float MaximumDefenderRouteStretch,
        string DefenderRouteProfile,
        string Failures);

    private BazaarDensityResult BazaarV2DensityReady(DemolitionArenaLayout layout)
    {
        var failures = new List<string>();
        var detachedFullHeightBaffles = BazaarDetachedFullHeightBaffles(layout);
        if (detachedFullHeightBaffles.Count > 0)
        {
            failures.Add($"detached-baffles-{string.Join(',', detachedFullHeightBaffles)}");
        }
        var blockers = layout.CollisionBoxes
            .Where(BazaarIsFullHeightArchitecture)
            .ToArray();
        var siteASamples = BazaarSiteEyeSamples(layout, layout.SitePositions[0]);
        var siteBSamples = BazaarSiteEyeSamples(layout, layout.SitePositions[1]);
        var visibleSitePairs = 0;
        foreach (var siteA in siteASamples)
        {
            foreach (var siteB in siteBSamples)
            {
                if (!PhysicsRaycast.HasHit(GetWorld3D(), siteA, siteB, 1))
                {
                    visibleSitePairs++;
                }
            }
        }
        var sitePairCount = siteASamples.Count * siteBSamples.Count;
        if (siteASamples.Count < 48 || siteBSamples.Count < 48 || visibleSitePairs != 0)
        {
            failures.Add($"site-los-{visibleSitePairs}/{sitePairCount}-{siteASamples.Count}/{siteBSamples.Count}");
        }

        var highViolations = BazaarHighVisibilityViolations(layout, out var highFailure);
        if (highViolations != 0)
        {
            failures.Add($"high-visibility-{highViolations}-{highFailure}");
        }

        var longestSightline = BazaarLongestSampledSightline(
            layout, blockers, out var longestFrom, out var longestTo);
        if (longestSightline > 45.25f)
        {
            failures.Add($"longest-los-{longestSightline:0.00}-{longestFrom}-{longestTo}");
        }

        var largestOpenDiameter = BazaarLargestOpenDiameter(
            layout, blockers, out var openCenter);
        if (largestOpenDiameter > 18.01f)
        {
            failures.Add($"open-diameter-{largestOpenDiameter:0.00}-{openCenter}");
        }

        var interiorContractReady = BazaarInteriorContractReady(
            layout, out var interiorRatio, out var interiorFailure);
        if (!interiorContractReady)
        {
            failures.Add(interiorFailure);
        }

        var plantZoneCoverageReady = BazaarPlantZoneCoverageReady(
            layout, out var plantZoneACoverage, out var plantZoneBCoverage);
        if (!plantZoneCoverageReady)
        {
            failures.Add($"plant-coverage-{plantZoneACoverage:0.000}/{plantZoneBCoverage:0.000}");
        }

        var groundDoorContractReady = BazaarGroundDoorContractReady(layout, out var doorFailure);
        if (!groundDoorContractReady)
        {
            failures.Add(doorFailure);
        }

        var planner = new DemolitionRoutePlanner(layout);
        var defenderRoutes = layout.DefenderSpawns.SelectMany((spawn, spawnIndex) =>
            layout.SitePositions.Select((site, siteIndex) =>
            {
                var route = planner.Plan(spawn, site, DemolitionTeam.Defenders);
                return new
                {
                    SpawnIndex = spawnIndex,
                    SiteIndex = siteIndex,
                    Route = route,
                    Clear = planner.IsRouteClear(spawn, route.Waypoints),
                    Stretch = route.Length / Mathf.Max(0.1f, HorizontalDistance(spawn, site))
                };
            })).ToArray();
        var maximumDefenderRoute = defenderRoutes
            .OrderByDescending(route => route.Stretch)
            .First();
        var defenderRouteProfile = string.Join(',', defenderRoutes.Select(route =>
            $"s{route.SpawnIndex}p{route.SiteIndex}:"
            + $"{route.Route.Length:0.00}m/{route.Stretch:0.000}"));
        var defenderRoutesEfficient = defenderRoutes.All(route =>
            route.Route.ReachesDestination
            && route.Clear
            && route.Stretch <= 1.40f);
        if (!defenderRoutesEfficient)
        {
            failures.Add($"defender-route-s{maximumDefenderRoute.SpawnIndex}p{maximumDefenderRoute.SiteIndex}:"
                + $"{maximumDefenderRoute.Route.Length:0.00}m/{maximumDefenderRoute.Stretch:0.000}");
        }

        return new BazaarDensityResult(
            failures.Count == 0,
            visibleSitePairs,
            sitePairCount,
            highViolations,
            longestSightline,
            largestOpenDiameter,
            interiorRatio,
            plantZoneACoverage,
            plantZoneBCoverage,
            interiorContractReady,
            plantZoneCoverageReady,
            groundDoorContractReady,
            detachedFullHeightBaffles.Count,
            defenderRoutesEfficient,
            maximumDefenderRoute.Stretch,
            defenderRouteProfile,
            string.Join('|', failures));
    }

    private static IReadOnlyList<Vector3> BazaarSiteEyeSamples(
        DemolitionArenaLayout layout,
        Vector3 site)
    {
        var samples = new List<Vector3>(81);
        for (var xIndex = -4; xIndex <= 4; xIndex++)
        {
            for (var zIndex = -4; zIndex <= 4; zIndex++)
            {
                var sample = new Vector3(
                    site.X + xIndex,
                    layout.Origin.Y + 1.55f,
                    site.Z + zIndex);
                if (!layout.CollisionBoxes.Any(box =>
                        BazaarIsFullHeightArchitecture(box)
                        && BazaarPointInsideBoxAtHeight(sample, box, 0.25f)))
                {
                    samples.Add(sample);
                }
            }
        }
        return samples;
    }

    private int BazaarHighVisibilityViolations(
        DemolitionArenaLayout layout,
        out string failure)
    {
        var siteEyes = layout.SitePositions
            .Select(site => new Vector3(site.X, layout.Origin.Y + 1.55f, site.Z))
            .ToArray();
        var mainEntries = new[]
        {
            new[]
            {
                layout.Origin + new Vector3(-47.0f, 1.55f, -4.0f),
                layout.Origin + new Vector3(-34.0f, 1.55f, -10.0f)
            },
            new[]
            {
                layout.Origin + new Vector3(46.0f, 1.55f, -6.0f),
                layout.Origin + new Vector3(34.0f, 1.55f, -14.0f)
            }
        };
        var violations = 0;
        var details = new List<string>();
        foreach (var route in BazaarElevatedRoutes(layout))
        {
            var maximumY = route.Points.Max(point => point.Y);
            var highSamples = route.Points
                .Where(point => point.Y >= maximumY - 0.05f)
                .Select(point => point + new Vector3(0.0f, 1.3f, 0.0f))
                .Distinct()
                .ToArray();
            foreach (var sample in highSamples)
            {
                var seesSites = siteEyes.Count(site =>
                    !PhysicsRaycast.HasHit(GetWorld3D(), sample, site, 1));
                var seesBothEntries = mainEntries.Any(entries => entries.All(entry =>
                    !PhysicsRaycast.HasHit(GetWorld3D(), sample, entry, 1)));
                if (seesSites > 1 || seesBothEntries)
                {
                    violations++;
                    details.Add($"{route.Name}@{sample}:sites{seesSites}:entries{seesBothEntries}");
                }
            }
        }
        failure = string.Join(',', details.Take(8));
        return violations;
    }

    private float BazaarLongestSampledSightline(
        DemolitionArenaLayout layout,
        IReadOnlyList<DemolitionArenaBox> blockers,
        out Vector3 longestFrom,
        out Vector3 longestTo)
    {
        const float sampleSpacing = 4.0f;
        const float rayLength = 60.0f;
        const int directionCount = 24;
        var longest = 0.0f;
        longestFrom = Vector3.Zero;
        longestTo = Vector3.Zero;
        for (var x = layout.WorldBounds.Position.X + 2.0f;
             x <= layout.WorldBounds.End.X - 2.0f;
             x += sampleSpacing)
        {
            for (var z = layout.WorldBounds.Position.Y + 2.0f;
                 z <= layout.WorldBounds.End.Y - 2.0f;
                 z += sampleSpacing)
            {
                var from = new Vector3(x, layout.Origin.Y + 1.55f, z);
                if (blockers.Any(box => BazaarPointInsideBoxAtHeight(from, box, 0.45f)))
                {
                    continue;
                }
                for (var directionIndex = 0; directionIndex < directionCount; directionIndex++)
                {
                    var angle = Mathf.Tau * directionIndex / directionCount;
                    var direction = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                    var to = from + direction * rayLength;
                    var distance = PhysicsRaycast.TryHit(
                        GetWorld3D(), from, to, 1, out var hit)
                        ? from.DistanceTo(hit.Position)
                        : rayLength;
                    if (distance > longest)
                    {
                        longest = distance;
                        longestFrom = from;
                        longestTo = PhysicsRaycast.TryHit(
                            GetWorld3D(), from, to, 1, out var detailHit)
                            ? detailHit.Position
                            : to;
                    }
                }
            }
        }
        return longest;
    }

    private static float BazaarLargestOpenDiameter(
        DemolitionArenaLayout layout,
        IReadOnlyList<DemolitionArenaBox> blockers,
        out Vector3 openCenter)
    {
        const float sampleSpacing = 1.5f;
        var maximumRadius = 0.0f;
        openCenter = Vector3.Zero;
        for (var x = layout.WorldBounds.Position.X + 1.0f;
             x <= layout.WorldBounds.End.X - 1.0f;
             x += sampleSpacing)
        {
            for (var z = layout.WorldBounds.Position.Y + 1.0f;
                 z <= layout.WorldBounds.End.Y - 1.0f;
                 z += sampleSpacing)
            {
                var sample = new Vector3(x, layout.Origin.Y + 1.2f, z);
                if (blockers.Any(box => BazaarPointInsideBoxAtHeight(sample, box, 0.35f)))
                {
                    continue;
                }
                var nearest = Mathf.Min(
                    Mathf.Min(x - layout.WorldBounds.Position.X, layout.WorldBounds.End.X - x),
                    Mathf.Min(z - layout.WorldBounds.Position.Y, layout.WorldBounds.End.Y - z));
                foreach (var box in blockers)
                {
                    if (sample.Y < box.Center.Y - box.Size.Y * 0.5f
                        || sample.Y > box.Center.Y + box.Size.Y * 0.5f)
                    {
                        continue;
                    }
                    var deltaX = Mathf.Max(Mathf.Abs(x - box.Center.X) - box.Size.X * 0.5f, 0.0f);
                    var deltaZ = Mathf.Max(Mathf.Abs(z - box.Center.Z) - box.Size.Z * 0.5f, 0.0f);
                    nearest = Mathf.Min(nearest, Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ));
                }
                if (nearest > maximumRadius)
                {
                    maximumRadius = nearest;
                    openCenter = sample;
                }
            }
        }
        return maximumRadius * 2.0f;
    }

    private static bool BazaarInteriorContractReady(
        DemolitionArenaLayout layout,
        out float ratio,
        out string failure)
    {
        var roofs = layout.CollisionBoxes.Where(box =>
            box.Name.StartsWith("Roof", StringComparison.Ordinal)).ToArray();
        var masses = layout.CollisionBoxes.Where(box =>
            box.Name.StartsWith("Mass", StringComparison.Ordinal)).ToArray();
        var hasA = roofs.Count(box => box.Name.StartsWith("RoofA_", StringComparison.Ordinal)) >= 4
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("WallA_", StringComparison.Ordinal))
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("PartitionA_", StringComparison.Ordinal));
        var hasB = roofs.Any(box => box.Name.StartsWith("RoofB_", StringComparison.Ordinal))
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("WallB_", StringComparison.Ordinal))
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("PartitionB_", StringComparison.Ordinal));
        var hasMid = roofs.Count(box => box.Name.StartsWith("RoofMid_", StringComparison.Ordinal)) >= 4
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("WallMid", StringComparison.Ordinal))
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("PartitionMid", StringComparison.Ordinal));
        var hasBack = roofs.Count(box => box.Name.StartsWith("RoofBack_", StringComparison.Ordinal)) >= 4
            && layout.CollisionBoxes.Any(box => box.Name.StartsWith("WallBack_", StringComparison.Ordinal));
        var decks = layout.TraversalBoxes.Where(box => box.Name.EndsWith("Deck", StringComparison.Ordinal)).ToArray();
        var ramps = layout.TraversalBoxes.Where(box => box.Name.EndsWith("Ramp", StringComparison.Ordinal)).ToArray();
        var areaGeometryReady = roofs.Concat(masses).All(BazaarIsAxisAlignedFootprint)
            && decks.Length == 3 && ramps.Length == 6 && decks.Length + ramps.Length == layout.TraversalBoxes.Count
            && decks.All(box => BazaarIsAxisAlignedFootprint(box)
                && box.Size.Y <= 0.25f
                && box.Center.Y - box.Size.Y * 0.5f >= layout.Origin.Y + 2.5f);
        var blockedGroundArea = BazaarFootprintArea(layout.WorldBounds, masses, Array.Empty<DemolitionArenaBox>());
        var groundPlayableArea = layout.WorldBounds.Size.X * layout.WorldBounds.Size.Y - blockedGroundArea;
        var groundRoofedArea = BazaarFootprintArea(layout.WorldBounds, roofs, masses);
        var elevatedDeckArea = decks.Sum(box => box.Size.X * box.Size.Z);
        // Ramps connect levels; only genuinely additional horizontal decks count on both sides.
        ratio = (groundRoofedArea + elevatedDeckArea) / Mathf.Max(groundPlayableArea + elevatedDeckArea, 1.0f);
        var ratioReady = ratio is >= 0.35f and <= 0.45f;
        var ready = hasA && hasB && hasMid && hasBack && masses.Length >= 17
            && areaGeometryReady && ratioReady;
        failure = ready
            ? string.Empty
            : $"interiors-{hasA}/{hasB}/{hasMid}/{hasBack}-m{masses.Length}"
                + $"-surface={areaGeometryReady}-r{ratio:0.000}"
                + $"-ground={groundPlayableArea:0.0}-roofed={groundRoofedArea:0.0}"
                + $"-deck={elevatedDeckArea:0.0}";
        return ready;
    }

    private static float BazaarFootprintArea(Rect2 bounds, IReadOnlyList<DemolitionArenaBox> included,
        IReadOnlyList<DemolitionArenaBox> excluded)
    {
        var boxes = included.Concat(excluded).ToArray();
        var xCuts = boxes.SelectMany(box => new[] {
            Mathf.Clamp(box.Center.X - box.Size.X * 0.5f, bounds.Position.X, bounds.End.X),
            Mathf.Clamp(box.Center.X + box.Size.X * 0.5f, bounds.Position.X, bounds.End.X) })
            .Append(bounds.Position.X).Append(bounds.End.X).Distinct().OrderBy(value => value).ToArray();
        var zCuts = boxes.SelectMany(box => new[] {
            Mathf.Clamp(box.Center.Z - box.Size.Z * 0.5f, bounds.Position.Y, bounds.End.Y),
            Mathf.Clamp(box.Center.Z + box.Size.Z * 0.5f, bounds.Position.Y, bounds.End.Y) })
            .Append(bounds.Position.Y).Append(bounds.End.Y).Distinct().OrderBy(value => value).ToArray();
        var area = 0.0f;
        for (var x = 0; x < xCuts.Length - 1; x++)
        {
            for (var z = 0; z < zCuts.Length - 1; z++)
            {
                var sample = new Vector3((xCuts[x] + xCuts[x + 1]) * 0.5f, 0.0f,
                    (zCuts[z] + zCuts[z + 1]) * 0.5f);
                if (included.Any(box => BazaarPointInsideFootprint(sample, box, 0.0f))
                    && !excluded.Any(box => BazaarPointInsideFootprint(sample, box, 0.0f)))
                {
                    area += (xCuts[x + 1] - xCuts[x]) * (zCuts[z + 1] - zCuts[z]);
                }
            }
        }
        return area;
    }

    private static bool BazaarIsAxisAlignedFootprint(DemolitionArenaBox box) =>
        box.Rotation.IsEqualApprox(Vector3.Zero);

    private bool BazaarGroundDoorContractReady(
        DemolitionArenaLayout layout,
        out string failure)
    {
        const float capsuleRadius = 0.38f;
        var thresholds = layout.BazaarGroundThresholds();
        var siteACount = thresholds.Count(threshold => threshold.Site == "a");
        var siteBCount = thresholds.Count(threshold => threshold.Site == "b");
        var invalidWidths = thresholds.Where(threshold =>
            threshold.Width is < 2.8f or > 3.6f).ToArray();
        var blocked = new List<string>();
        foreach (var threshold in thresholds)
        {
            var normal = threshold.Normal.Normalized();
            var tangent = new Vector3(-normal.Z, 0.0f, normal.X);
            foreach (var lateralOffset in new[] { -capsuleRadius, 0.0f, capsuleRadius })
            {
                foreach (var height in new[] { 0.35f, 1.2f, 2.3f })
                {
                    var center = new Vector3(
                        threshold.Center.X,
                        layout.Origin.Y + height,
                        threshold.Center.Z) + tangent * lateralOffset;
                    if (PhysicsRaycast.HasHit(
                            GetWorld3D(),
                            center - normal * 1.4f,
                            center + normal * 1.4f,
                            1))
                    {
                        blocked.Add($"{threshold.Name}@{lateralOffset:0.00}/{height:0.00}");
                    }
                }
            }
        }
        var ready = thresholds.Count == 10
            && siteACount == 5
            && siteBCount == 5
            && invalidWidths.Length == 0
            && blocked.Count == 0
            && layout.BazaarSiteShellReady("a")
            && layout.BazaarSiteShellReady("b");
        failure = ready
            ? string.Empty
            : $"ground-doors-{siteACount}/{siteBCount}/{thresholds.Count}"
                + $"-widths={string.Join(',', invalidWidths.Select(threshold => $"{threshold.Name}:{threshold.Width:0.00}"))}"
                + $"-blocked={string.Join(',', blocked.Take(12))}"
                + $"-shells={layout.BazaarSiteShellReady("a")}/{layout.BazaarSiteShellReady("b")}";
        return ready;
    }

    private static bool BazaarPlantZoneCoverageReady(
        DemolitionArenaLayout layout,
        out float siteACoverage,
        out float siteBCoverage)
    {
        var roofs = layout.CollisionBoxes.Where(box =>
            box.Name.StartsWith("Roof", StringComparison.Ordinal)).ToArray();
        var aCourtyardBounded = layout.BazaarSiteShellReady("a");
        var aCourtyardBounds = new Rect2(
            layout.Origin.X - 60.0f,
            layout.Origin.Z - 31.0f,
            26.0f,
            27.0f);
        siteACoverage = BazaarPlantZoneCoverage(
            layout.SitePositions[0], roofs, aCourtyardBounded, aCourtyardBounds);
        siteBCoverage = BazaarPlantZoneCoverage(
            layout.SitePositions[1], roofs, courtyardBounded: false, default);
        return siteACoverage >= 0.75f && siteBCoverage >= 0.75f;
    }

    private static float BazaarPlantZoneCoverage(
        Vector3 site,
        IReadOnlyList<DemolitionArenaBox> roofs,
        bool courtyardBounded,
        Rect2 courtyardBounds)
    {
        var covered = 0;
        const int samplesPerAxis = 9;
        for (var xIndex = -4; xIndex <= 4; xIndex++)
        {
            for (var zIndex = -4; zIndex <= 4; zIndex++)
            {
                var sample = site + new Vector3(xIndex, 0.0f, zIndex);
                var roofed = roofs.Any(roof => BazaarPointInsideFootprint(sample, roof, 0.0f));
                var insideCourtyard = courtyardBounded
                    && courtyardBounds.HasPoint(new Vector2(sample.X, sample.Z));
                if (roofed || insideCourtyard)
                {
                    covered++;
                }
            }
        }
        return covered / (float)(samplesPerAxis * samplesPerAxis);
    }

    private static bool BazaarIsFullHeightArchitecture(DemolitionArenaBox box)
        => box.Size.Y >= 2.4f
            && (box.Name.StartsWith("Mass", StringComparison.Ordinal)
                || box.Name.StartsWith("Wall", StringComparison.Ordinal)
                || box.Name.StartsWith("Partition", StringComparison.Ordinal)
                || box.Name.StartsWith("Column", StringComparison.Ordinal));

    private static IReadOnlyList<string> BazaarDetachedFullHeightBaffles(
        DemolitionArenaLayout layout)
    {
        const float attachmentTolerance = 0.12f;
        var anchors = layout.CollisionBoxes.Where(box =>
            BazaarIsFullHeightArchitecture(box)
            && !box.Name.Contains("Baffle", StringComparison.Ordinal)).ToArray();
        return layout.CollisionBoxes.Where(box =>
                BazaarIsFullHeightArchitecture(box)
                && box.Name.Contains("Baffle", StringComparison.Ordinal)
                && !anchors.Any(anchor => BazaarFootprintsTouch(
                    box,
                    anchor,
                    attachmentTolerance)))
            .Select(box => box.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool BazaarFootprintsTouch(
        DemolitionArenaBox left,
        DemolitionArenaBox right,
        float tolerance)
    {
        var xGap = Mathf.Max(
            Mathf.Abs(left.Center.X - right.Center.X)
                - (left.Size.X + right.Size.X) * 0.5f,
            0.0f);
        var zGap = Mathf.Max(
            Mathf.Abs(left.Center.Z - right.Center.Z)
                - (left.Size.Z + right.Size.Z) * 0.5f,
            0.0f);
        return new Vector2(xGap, zGap).Length() <= tolerance;
    }

    private static bool BazaarPointInsideFootprint(
        Vector3 point,
        DemolitionArenaBox box,
        float margin)
        => Mathf.Abs(point.X - box.Center.X) <= box.Size.X * 0.5f + margin
            && Mathf.Abs(point.Z - box.Center.Z) <= box.Size.Z * 0.5f + margin;

    private static bool BazaarPointInsideBoxAtHeight(
        Vector3 point,
        DemolitionArenaBox box,
        float footprintMargin)
        => BazaarPointInsideFootprint(point, box, footprintMargin)
            && point.Y >= box.Center.Y - box.Size.Y * 0.5f
            && point.Y <= box.Center.Y + box.Size.Y * 0.5f;
}
