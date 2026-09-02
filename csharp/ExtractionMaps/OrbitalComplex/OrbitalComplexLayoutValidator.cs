using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public sealed record OrbitalComplexValidationSnapshot(
    bool Valid,
    bool BoundsValid,
    bool UniqueIdsValid,
    bool SpawnSeparationValid,
    bool ExtractionDistanceValid,
    bool ObjectiveOrderValid,
    bool RiskGradientValid,
    bool EncounterDensityValid,
    bool RouteCoverageValid,
    bool VerticalityValid,
    bool CollisionValid,
    bool PowerStagesValid,
    float MinimumPlayerToRivalDistance,
    float MinimumPlayerToExtractionDistance,
    int GarrisonCount,
    int PatrolRouteCount,
    int CoverPointCount,
    int LootPlacementCount,
    int RouteProbeCount,
    int CollisionShapeDefinitionCount,
    IReadOnlyList<string> Failures)
{
    public string MachineSummary => string.Format(
        CultureInfo.InvariantCulture,
        "valid={0} bounds={1} ids={2} spawn={3}:{4:0.0} extract={5}:{6:0.0} "
        + "objectives={7} risk={8} encounters={9}:{10}/{11}/{12} routes={13}:{14}/{15} "
        + "vertical={16} collision={17}:{18} power={19} failures={20}",
        Valid,
        BoundsValid,
        UniqueIdsValid,
        SpawnSeparationValid,
        MinimumPlayerToRivalDistance,
        ExtractionDistanceValid,
        MinimumPlayerToExtractionDistance,
        ObjectiveOrderValid,
        RiskGradientValid,
        EncounterDensityValid,
        GarrisonCount,
        PatrolRouteCount,
        CoverPointCount,
        RouteCoverageValid,
        RouteProbeCount,
        LootPlacementCount,
        VerticalityValid,
        CollisionValid,
        CollisionShapeDefinitionCount,
        PowerStagesValid,
        Failures.Count == 0 ? "none" : string.Join('|', Failures));
}

/// <summary>Pure deterministic validation for the Falltide layout and stage contract.</summary>
public static class OrbitalComplexLayoutValidator
{
    private const float Epsilon = 0.01f;

    public static OrbitalComplexValidationSnapshot Validate(OrbitalComplexMapLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var failures = new List<string>();
        var boundsValid = ValidateBounds(layout);
        AddFailure(failures, boundsValid, "bounds");
        var uniqueIdsValid = ValidateUniqueIds(layout);
        AddFailure(failures, uniqueIdsValid, "duplicate_ids");
        var minimumSpawnDistance = MinimumDistance(
            layout.PlayerSpawnPads.Select(pad => pad.Position),
            layout.RivalSpawnPads.Select(pad => pad.Position));
        var spawnSeparationValid = layout.PlayerSpawnPads.Count == 4
            && layout.RivalSpawnPads.Count >= 4
            && minimumSpawnDistance >= 100.0f
            && MaximumPairDistance(layout.PlayerSpawnPads.Select(pad => pad.Position)) <= 16.0f;
        AddFailure(failures, spawnSeparationValid, "spawn_separation");
        var minimumExtractionDistance = layout.PlayerSpawnPads
            .Min(pad => HorizontalDistance(pad.Position, layout.Extraction.Position));
        var extractionDistanceValid = layout.Extraction.Radius is >= 6.0f and <= 12.0f
            && minimumExtractionDistance >= 250.0f;
        AddFailure(failures, extractionDistanceValid, "extraction_distance");
        var objectiveOrderValid = ValidateObjectiveOrder(layout);
        AddFailure(failures, objectiveOrderValid, "objective_order");
        var riskGradientValid = ValidateRiskGradient(layout);
        AddFailure(failures, riskGradientValid, "risk_gradient");
        var encounterDensityValid = layout.GarrisonSpawns.Count >= 20
            && layout.PatrolRoutes.Count >= 5
            && layout.CoverPoints.Count >= 32
            && layout.QrfSpawns.Count >= 6
            && layout.BossRoute.Count >= 12;
        AddFailure(failures, encounterDensityValid, "encounter_density");
        var routeCoverageValid = ValidateRoutes(layout);
        AddFailure(failures, routeCoverageValid, "route_coverage");
        var verticalityValid = ValidateVerticality(layout);
        AddFailure(failures, verticalityValid, "verticality");
        var collisionValid = ValidateCollision(layout);
        AddFailure(failures, collisionValid, "collision");
        var powerStagesValid = ValidatePowerStages(layout);
        AddFailure(failures, powerStagesValid, "power_stages");

        var lootCount = layout.WeaponCases.Count + layout.GradedLoot.Count + layout.Valuables.Count;
        var collisionCount = layout.CollisionBoxes.Count + layout.Ramps.Count + layout.PowerGates.Count;
        return new OrbitalComplexValidationSnapshot(
            failures.Count == 0,
            boundsValid,
            uniqueIdsValid,
            spawnSeparationValid,
            extractionDistanceValid,
            objectiveOrderValid,
            riskGradientValid,
            encounterDensityValid,
            routeCoverageValid,
            verticalityValid,
            collisionValid,
            powerStagesValid,
            minimumSpawnDistance,
            minimumExtractionDistance,
            layout.GarrisonSpawns.Count,
            layout.PatrolRoutes.Count,
            layout.CoverPoints.Count,
            lootCount,
            layout.RouteProbes.Count,
            collisionCount,
            failures);
    }

    private static bool ValidateBounds(OrbitalComplexMapLayout layout)
    {
        var bounds = layout.Bounds;
        var dimensionsReady = Mathf.Abs(bounds.Horizontal.Size.X
                - OrbitalComplexMapDefinition.WidthMeters) <= Epsilon
            && Mathf.Abs(bounds.Horizontal.Size.Y
                - OrbitalComplexMapDefinition.DepthMeters) <= Epsilon
            && Mathf.Abs(bounds.Horizontal.GetCenter().X) <= Epsilon
            && Mathf.Abs(bounds.Horizontal.GetCenter().Y
                - OrbitalComplexMapDefinition.CenterZ) <= Epsilon
                && bounds.MinimumY <= OrbitalComplexMapDefinition.MinimumY
                && bounds.MaximumY >= OrbitalComplexMapDefinition.MaximumY;
        return dimensionsReady && GameplayPositions(layout).All(position => Inside(bounds, position));
    }

    private static IEnumerable<Vector3> GameplayPositions(OrbitalComplexMapLayout layout)
    {
        foreach (var pad in layout.PlayerSpawnPads) yield return pad.Position;
        foreach (var pad in layout.RivalSpawnPads) yield return pad.Position;
        foreach (var point in layout.GarrisonSpawns) yield return point;
        foreach (var route in layout.PatrolRoutes)
            foreach (var point in route.Waypoints) yield return point;
        foreach (var point in layout.CoverPoints) yield return point;
        foreach (var point in layout.QrfSpawns) yield return point;
        foreach (var point in layout.BossRoute) yield return point;
        foreach (var objective in layout.Objectives) yield return objective.Position;
        yield return layout.Extraction.Position;
        foreach (var placement in layout.WeaponCases) yield return placement.Position;
        foreach (var placement in layout.GradedLoot) yield return placement.Position;
        foreach (var placement in layout.Valuables) yield return placement.Position;
        foreach (var placement in layout.Explosives) yield return placement.Position;
        foreach (var landmark in layout.MinimapLandmarks) yield return landmark.Position;
        foreach (var probe in layout.RouteProbes)
        {
            yield return probe.From;
            yield return probe.To;
        }
        foreach (var ramp in layout.Ramps)
        {
            yield return ramp.LowApproach;
            yield return ramp.HighApproach;
        }
    }

    private static bool Inside(OrbitalComplexMapBounds bounds, Vector3 point)
    {
        var minimum = bounds.Horizontal.Position;
        var maximum = bounds.Horizontal.End;
        return point.X >= minimum.X - Epsilon
            && point.X <= maximum.X + Epsilon
            && point.Z >= minimum.Y - Epsilon
            && point.Z <= maximum.Y + Epsilon
            && point.Y >= bounds.MinimumY - Epsilon
            && point.Y <= bounds.MaximumY + Epsilon;
    }

    private static bool ValidateUniqueIds(OrbitalComplexMapLayout layout)
        => Unique(layout.PlayerSpawnPads.Select(item => item.Id))
            && Unique(layout.RivalSpawnPads.Select(item => item.Id))
            && Unique(layout.PatrolRoutes.Select(item => item.Id))
            && Unique(layout.Objectives.Select(item => item.Id))
            && Unique(layout.WeaponCases.Select(item => item.Id))
            && Unique(layout.GradedLoot.Select(item => item.Id))
            && Unique(layout.Valuables.Select(item => item.Id))
            && Unique(layout.Explosives.Select(item => item.Id))
            && Unique(layout.MinimapLandmarks.Select(item => item.Id))
            && Unique(layout.RouteProbes.Select(item => item.Id))
            && Unique(layout.CollisionBoxes.Select(item => item.Id))
            && Unique(layout.Ramps.Select(item => item.Id))
            && Unique(layout.PowerGates.Select(item => item.Id));

    private static bool Unique(IEnumerable<string> ids)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !set.Add(id))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ValidateObjectiveOrder(OrbitalComplexMapLayout layout)
    {
        if (layout.Objectives.Count != 2)
        {
            return false;
        }
        var expectedFirst = (layout.SharedWorldSeed & 1UL) == 0UL
            ? "reroute_breaker_bus"
            : "purge_quarantine_archive";
        return layout.Objectives[0].Id == expectedFirst
            && layout.Objectives[0].Id != layout.Objectives[1].Id
            && layout.Objectives[0].Position.DistanceTo(layout.Objectives[1].Position) >= 150.0f;
    }

    private static bool ValidateRiskGradient(OrbitalComplexMapLayout layout)
    {
        var gradesValid = layout.WeaponCases.All(item => RiskAllows(item.Risk, item.Grade))
            && layout.GradedLoot.All(item => RiskAllows(item.Risk, item.Grade))
            && layout.Valuables.All(item => RiskAllows(item.Risk, item.Grade));
        var starterCasesReady = layout.WeaponCases.Count(item =>
                item.Risk == OrbitalComplexLootRisk.OuterRing
                && HorizontalDistance(item.Position,
                    OrbitalComplexMapDefinition.IntakeCausewayCenter) <= 50.0f) >= 2;
        var lockdownPositions = layout.WeaponCases
            .Where(item => item.Risk == OrbitalComplexLootRisk.StormglassLockdown)
            .Select(item => item.Position)
            .Concat(layout.GradedLoot
                .Where(item => item.Risk == OrbitalComplexLootRisk.StormglassLockdown)
                .Select(item => item.Position))
            .Concat(layout.Valuables
                .Where(item => item.Risk == OrbitalComplexLootRisk.StormglassLockdown)
                .Select(item => item.Position))
            .ToArray();
        return gradesValid
            && starterCasesReady
            && lockdownPositions.Length >= 4
            && lockdownPositions.All(position => HorizontalDistance(
                position, OrbitalComplexMapDefinition.StormglassArrayCenter) <= 18.0f)
            && layout.GradedLoot.Any(item => item.Grade == LootGrade.Common)
            && layout.GradedLoot.Any(item => item.Grade == LootGrade.Uncommon)
            && layout.GradedLoot.Any(item => item.Grade == LootGrade.Rare)
            && layout.GradedLoot.Any(item => item.Grade == LootGrade.Epic)
            && layout.GradedLoot.Any(item => item.Grade == LootGrade.Legendary);
    }

    private static bool RiskAllows(OrbitalComplexLootRisk risk, LootGrade grade)
        => risk switch
        {
            OrbitalComplexLootRisk.OuterRing => grade is LootGrade.Common or LootGrade.Uncommon,
            OrbitalComplexLootRisk.ObjectiveDistrict => grade is LootGrade.Rare or LootGrade.Epic,
            OrbitalComplexLootRisk.StormglassLockdown => grade == LootGrade.Legendary,
            _ => false
        };

    private static bool ValidateRoutes(OrbitalComplexMapLayout layout)
        => layout.PatrolRoutes.All(route => route.Loop && route.Waypoints.Count >= 4)
            && layout.RouteProbes.Count >= 12
            && layout.RouteProbes.All(probe => probe.MinimumClearance >= 2.0f
                && probe.RequiredObjectiveStage is >= 0 and <= 2
                && probe.From.DistanceTo(probe.To) >= 8.0f)
            && layout.RouteProbes.Any(probe => probe.RequiredObjectiveStage == 1)
            && layout.RouteProbes.Any(probe => probe.RequiredObjectiveStage == 2)
            && layout.Ramps.Count >= 4;

    private static bool ValidateVerticality(OrbitalComplexMapLayout layout)
    {
        var routeLayers = layout.PatrolRoutes.Select(route => route.Layer).ToHashSet();
        var landmarkLayers = layout.MinimapLandmarks.Select(item => item.Layer).ToHashSet();
        // MAP 03 is authored below the shared world origin.  Keep the verticality
        // contract relative to the service deck instead of using the old outdoor
        // absolute thresholds; this remains correct if the whole bunker is shifted.
        var serviceDeckY = OrbitalComplexMapDefinition.StormglassArrayCenter.Y;
        var lowerLayerThreshold = serviceDeckY - 8.0f;
        var upperLayerThreshold = serviceDeckY + 8.0f;
        var dryDockGameplay = layout.GarrisonSpawns.Count(point =>
                point.Y < lowerLayerThreshold) >= 4
            && layout.GradedLoot.Count(item =>
                item.Position.Y < lowerLayerThreshold) >= 2;
        var catwalkGameplay = layout.GarrisonSpawns.Count(point =>
                point.Y > upperLayerThreshold) >= 2
            && layout.WeaponCases.Any(item =>
                item.Position.Y > upperLayerThreshold);
        return routeLayers.Count == 3
            && landmarkLayers.Count == 3
            && dryDockGameplay
            && catwalkGameplay
            && layout.Ramps.Any(ramp =>
                ramp.DestinationLayer == OrbitalComplexVerticalLayer.Catwalk)
            && layout.Ramps.Any(ramp => ramp.LowApproach.Y < lowerLayerThreshold);
    }

    private static bool ValidateCollision(OrbitalComplexMapLayout layout)
    {
        var primitiveCollisionValid = layout.CollisionBoxes.Count >= 20
            && layout.CollisionBoxes.All(box => Positive(box.Size))
            && layout.Ramps.All(ramp => Positive(ramp.Size)
                && ramp.LowApproach.DistanceTo(ramp.HighApproach) >= 8.0f)
            && layout.CollisionBoxes.Count(box => box.Purpose == "boundary") == 4
            && layout.CollisionBoxes.Any(box => box.Purpose == "dry_dock")
            && layout.CollisionBoxes.Count(box => box.Purpose == "catwalk_deck") >= 3;

        // The bunker is enclosed on every side.  A short wall can pass a simple
        // count check while still allowing a player, vehicle, or grenade to leak
        // through the reactor pit or the upper shell, so require each perimeter
        // shape to span the declared vertical envelope and sit on a footprint edge.
        var bounds = layout.Bounds;
        var boundaries = layout.CollisionBoxes
            .Where(box => string.Equals(
                box.Purpose,
                "boundary",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var fullHeightBoundaries = boundaries.Length == 4
            && boundaries.All(box =>
                IsAxisAligned(box.RotationRadians)
                && CoversVerticalBounds(box, bounds)
                && IsBoundaryEdge(box, bounds));
        var perimeterCoverage = fullHeightBoundaries
            && boundaries.Any(box => CoversNorthEdge(box, bounds))
            && boundaries.Any(box => CoversSouthEdge(box, bounds))
            && boundaries.Any(box => CoversWestEdge(box, bounds))
            && boundaries.Any(box => CoversEastEdge(box, bounds));

        // BunkerCeiling is visible authored geometry, but gameplay must also have
        // an invisible roof shape.  Accept one full-footprint roof or a future
        // tiled equivalent that is represented by a single contract box.
        var ceilingCollision = layout.CollisionBoxes
            .Where(IsCeilingCollision)
            .Any(box => IsAxisAligned(box.RotationRadians)
                && box.Size.Y >= 0.5f
                && CoversHorizontalBounds(box, bounds)
                && IntersectsRoofPlane(box, bounds.MaximumY));

        return primitiveCollisionValid
            && fullHeightBoundaries
            && perimeterCoverage
            && ceilingCollision;
    }

    private static bool IsAxisAligned(Vector3 rotation)
        => Mathf.IsZeroApprox(rotation.X)
            && Mathf.IsZeroApprox(rotation.Y)
            && Mathf.IsZeroApprox(rotation.Z);

    private static bool CoversVerticalBounds(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        var halfHeight = box.Size.Y * 0.5f;
        return box.Position.Y - halfHeight <= bounds.MinimumY + Epsilon
            && box.Position.Y + halfHeight >= bounds.MaximumY - Epsilon;
    }

    private static bool IsBoundaryEdge(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        return CoversNorthEdge(box, bounds)
            || CoversSouthEdge(box, bounds)
            || CoversWestEdge(box, bounds)
            || CoversEastEdge(box, bounds);
    }

    private static bool CoversNorthEdge(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        var halfWidth = box.Size.X * 0.5f;
        var horizontal = bounds.Horizontal;
        return box.Size.X >= horizontal.Size.X - 2.0f
            && box.Size.Z <= 4.0f
            && Mathf.Abs(box.Position.Z - horizontal.Position.Y) <= 2.0f
            && box.Position.X - halfWidth <= horizontal.Position.X + Epsilon
            && box.Position.X + halfWidth >= horizontal.End.X - Epsilon;
    }

    private static bool CoversSouthEdge(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        var halfWidth = box.Size.X * 0.5f;
        var horizontal = bounds.Horizontal;
        return box.Size.X >= horizontal.Size.X - 2.0f
            && box.Size.Z <= 4.0f
            && Mathf.Abs(box.Position.Z - horizontal.End.Y) <= 2.0f
            && box.Position.X - halfWidth <= horizontal.Position.X + Epsilon
            && box.Position.X + halfWidth >= horizontal.End.X - Epsilon;
    }

    private static bool CoversWestEdge(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        var halfDepth = box.Size.Z * 0.5f;
        var horizontal = bounds.Horizontal;
        return box.Size.Z >= horizontal.Size.Y - 2.0f
            && box.Size.X <= 4.0f
            && Mathf.Abs(box.Position.X - horizontal.Position.X) <= 2.0f
            && box.Position.Z - halfDepth <= horizontal.Position.Y + Epsilon
            && box.Position.Z + halfDepth >= horizontal.End.Y - Epsilon;
    }

    private static bool CoversEastEdge(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        var halfDepth = box.Size.Z * 0.5f;
        var horizontal = bounds.Horizontal;
        return box.Size.Z >= horizontal.Size.Y - 2.0f
            && box.Size.X <= 4.0f
            && Mathf.Abs(box.Position.X - horizontal.End.X) <= 2.0f
            && box.Position.Z - halfDepth <= horizontal.Position.Y + Epsilon
            && box.Position.Z + halfDepth >= horizontal.End.Y - Epsilon;
    }

    private static bool CoversHorizontalBounds(
        OrbitalComplexCollisionBox box,
        OrbitalComplexMapBounds bounds)
    {
        var halfWidth = box.Size.X * 0.5f;
        var halfDepth = box.Size.Z * 0.5f;
        var horizontal = bounds.Horizontal;
        return box.Position.X - halfWidth <= horizontal.Position.X + Epsilon
            && box.Position.X + halfWidth >= horizontal.End.X - Epsilon
            && box.Position.Z - halfDepth <= horizontal.Position.Y + Epsilon
            && box.Position.Z + halfDepth >= horizontal.End.Y - Epsilon;
    }

    private static bool IntersectsRoofPlane(
        OrbitalComplexCollisionBox box,
        float roofY)
    {
        var halfHeight = box.Size.Y * 0.5f;
        return box.Position.Y - halfHeight <= roofY + Epsilon
            && box.Position.Y + halfHeight >= roofY - Epsilon;
    }

    private static bool IsCeilingCollision(OrbitalComplexCollisionBox box)
        => string.Equals(box.Purpose, "ceiling", StringComparison.OrdinalIgnoreCase)
            || string.Equals(box.Purpose, "roof", StringComparison.OrdinalIgnoreCase)
            || box.Id.Contains("ceiling", StringComparison.OrdinalIgnoreCase)
            || box.Id.Contains("roof", StringComparison.OrdinalIgnoreCase);

    private static bool ValidatePowerStages(OrbitalComplexMapLayout layout)
    {
        var zero = OrbitalComplexPowerRules.Derive(0, layout.SharedWorldSeed);
        var one = OrbitalComplexPowerRules.Derive(1, layout.SharedWorldSeed);
        var two = OrbitalComplexPowerRules.Derive(2, layout.SharedWorldSeed);
        var gatesReady = layout.PowerGates.Count == 3
            && layout.PowerGates.Count(gate => gate.OpensAtObjectiveStage == 1) == 2
            && layout.PowerGates.Count(gate => gate.OpensAtObjectiveStage == 2) == 1
            && layout.PowerGates.All(gate => Positive(gate.Size));
        return gatesReady
            && zero.Mode == OrbitalComplexPowerMode.Blackout
            && !zero.ExtractionEnabled && !zero.UpperBypassOpen && !zero.VaultOpen
            && one.Mode == OrbitalComplexPowerMode.EmergencyPower
            && one.ExtractionEnabled && one.UpperBypassOpen && !one.VaultOpen
            && Mathf.IsEqualApprox(one.ExtractionHoldSeconds,
                OrbitalComplexExtractionStrategy.EmergencyPowerCountdownSeconds)
            && two.Mode == OrbitalComplexPowerMode.FullReroute
            && two.ExtractionEnabled && two.UpperBypassOpen && two.VaultOpen
            && two.ExtractionHoldSeconds < one.ExtractionHoldSeconds
            && Mathf.IsEqualApprox(two.Presentation.TideGateOpeningFraction, 1.0f)
            && Mathf.IsEqualApprox(two.Presentation.VaultDoorOpeningFraction, 1.0f)
            && one.Presentation.QrfActivationRecommended
            && !one.Presentation.BossActivationRecommended
            && two.Presentation.BossActivationRecommended;
    }

    private static bool Positive(Vector3 size)
        => size.X > Epsilon && size.Y > Epsilon && size.Z > Epsilon;

    private static float MinimumDistance(
        IEnumerable<Vector3> first,
        IEnumerable<Vector3> second)
    {
        var minimum = float.PositiveInfinity;
        foreach (var a in first)
            foreach (var b in second)
            {
                minimum = Mathf.Min(minimum, HorizontalDistance(a, b));
            }
        return minimum;
    }

    private static float MaximumPairDistance(IEnumerable<Vector3> points)
    {
        var positions = points.ToArray();
        var maximum = 0.0f;
        for (var first = 0; first < positions.Length; first++)
            for (var second = first + 1; second < positions.Length; second++)
            {
                maximum = Mathf.Max(maximum, HorizontalDistance(
                    positions[first], positions[second]));
            }
        return maximum;
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
        => new Vector2(first.X - second.X, first.Z - second.Z).Length();

    private static void AddFailure(List<string> failures, bool valid, string failure)
    {
        if (!valid)
        {
            failures.Add(failure);
        }
    }
}
