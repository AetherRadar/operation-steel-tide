using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateRefineryCollision()
    {
        await WaitFrames(4);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        var counts = new RefineryRuntimeCounts();
        CountRefineryNodes(_levelRoot, false, false, false, false, ref counts);
        var expectedPlacements = RefineryLayout.Models.Count(placement => placement.HasCollision);
        var representedPlacements = _jianghaiGameplayCollision?.Body.GetChildren()
            .OfType<CollisionShape3D>()
            .Where(shape => shape.HasMeta("gameplay_source_placement"))
            .Select(shape => shape.GetMeta(
                "gameplay_source_placement",
                string.Empty).AsString())
            .Distinct(StringComparer.Ordinal)
            .Count() ?? 0;
        var expectedLandmarkShapes = _oldTownLandmarks?.CollisionShapeCount ?? 0;
        var authoredSourcesReady = ValidateAuthoredCollisionSources(
            _jianghaiGameplayCollision,
            out var authoredNamedSources,
            out var authoredPhysicsHits,
            out var enterableDoorClears,
            out var enterableFacadeHits,
            out var enterableSideHits,
            out var enterableBackHits,
            out var enterableLinerHits,
            out var enterableWingHits,
            out var enterableOverhangClears,
            out var authoredSourceSummary);
        var gameplayReady = IsBlackwaterRefineryMap
            && _jianghaiGameplayCollisionError is null
            && _jianghaiGameplayCollision is { } gameplay
            && IsInstanceValid(gameplay.Body)
            && gameplay.Body.CollisionLayer == 1
            && gameplay.Body.CollisionMask == 0
            && gameplay.SourcePlacementCount == expectedPlacements
            && gameplay.SuppressedPlacementCount > 0
            && gameplay.PlacementShapeCount >= expectedPlacements
            && representedPlacements == expectedPlacements
            && gameplay.AuthoredSourceMeshCount
                == JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount
            && gameplay.AuthoredShapeCount
                == JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount
            && gameplay.DensitySourceCount
                == JianghaiGameplayCollisionContract.ExpectedDensitySourceCount
            && gameplay.SolidSourceCount
                == JianghaiGameplayCollisionContract.ExpectedSolidSourceCount
            && gameplay.EnterableSourceCount
                == JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount
            && gameplay.EnterableShapeCount
                == JianghaiGameplayCollisionContract.ExpectedEnterableShapeCount
            && gameplay.CollisionShapeCount
                == gameplay.PlacementShapeCount + gameplay.AuthoredShapeCount
            && gameplay.BoxShapeCount == gameplay.CollisionShapeCount
            && gameplay.ConcaveShapeCount == 0
            && gameplay.DistrictShapeCounts.Count >= 10
            && counts.GameplayCollisionBodies == 2
            && counts.GameplayCollisionShapes
                == gameplay.CollisionShapeCount + expectedLandmarkShapes
            && counts.GameplayBoxCollisionShapes == counts.GameplayCollisionShapes
            && counts.GameplayNonBoxCollisionShapes == 0
            && authoredSourcesReady;
        var legacyReady = counts.LegacyCollisionBodies == 0
            && counts.LegacyCollisionShapes == 0
            && counts.NonBoxLegacyCollisionShapes == 0;
        var physicsReady = ValidateJianghaiBuildingCollision(
            out var blockingHits,
            out var clearRoutes,
            out var physicsSummary);
        var lootAccessReady = ValidateHighValueLootAccess(
            out var accessibleHighValueLoot,
            out var lootAccessBlocker);
        var routeReady = ValidateOldTownRouteProbes(out var routeCount, out var routeBlocker);
        var landmarksReady = ValidateOldTownLandmarks();
        var valid = gameplayReady && legacyReady && physicsReady && lootAccessReady
            && routeReady && landmarksReady;

        GD.Print(
            $"REFINERY_COLLISION_CHECK valid={valid} gameplay={gameplayReady} "
            + $"body={counts.GameplayCollisionBodies}/2 "
            + $"shapes={counts.GameplayCollisionShapes}/"
            + $"{(_jianghaiGameplayCollision?.CollisionShapeCount ?? 0) + expectedLandmarkShapes} "
            + $"boxes={counts.GameplayBoxCollisionShapes}/{counts.GameplayCollisionShapes} "
            + $"concave={_jianghaiGameplayCollision?.ConcaveShapeCount ?? -1}/0 "
            + $"placements={_jianghaiGameplayCollision?.SourcePlacementCount ?? 0} "
            + $"placement_shapes={_jianghaiGameplayCollision?.PlacementShapeCount ?? 0}:"
            + $"{representedPlacements} "
            + $"carved={_jianghaiGameplayCollision?.SuppressedPlacementCount ?? 0} "
            + $"suppressed_names={FormatCollisionSourceNames(
                _jianghaiGameplayCollision?.SuppressedPlacementNames ?? Array.Empty<string>())} "
            + $"authored_proxies={_jianghaiGameplayCollision?.AuthoredSourceMeshCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount} "
            + $"authored_shapes={_jianghaiGameplayCollision?.AuthoredShapeCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount} "
            + $"density={_jianghaiGameplayCollision?.DensitySourceCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedDensitySourceCount} "
            + $"solid={_jianghaiGameplayCollision?.SolidSourceCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedSolidSourceCount} "
            + $"enterable={_jianghaiGameplayCollision?.EnterableSourceCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount}:"
            + $"{_jianghaiGameplayCollision?.EnterableShapeCount ?? 0}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedEnterableShapeCount} "
            + $"authored_sources={authoredSourcesReady}:{authoredNamedSources}/"
            + $"{JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount}:"
            + $"physics={authoredPhysicsHits}/{JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount}:"
            + $"doors={enterableDoorClears}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount}:"
            + $"facades={enterableFacadeHits}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 10}:"
            + $"sides={enterableSideHits}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 2}:"
            + $"backs={enterableBackHits}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount}:"
            + $"liners={enterableLinerHits}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 4}:"
            + $"wings={enterableWingHits}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 4}:"
            + $"overhangs={enterableOverhangClears}/{JianghaiGameplayCollisionContract.ExpectedEnterableSourceCount * 4}:"
            + $"{authoredSourceSummary} "
            + $"districts={_jianghaiGameplayCollision?.DistrictShapeCounts.Count ?? 0} "
            + $"legacy={legacyReady}:{counts.LegacyCollisionBodies}/{counts.LegacyCollisionShapes}/0 "
            + $"ballistic={physicsReady}:{blockingHits}/"
            + $"{JianghaiBuildingBlockingProbeCount}:{clearRoutes}/3:{physicsSummary} "
            + $"routes={routeReady}:{routeCount}:{routeBlocker} "
            + $"loot_access={lootAccessReady}:{accessibleHighValueLoot}/12:{lootAccessBlocker} "
            + $"landmarks={landmarksReady}");
        GD.Print($"REFINERY_COLLISION_PASS valid={valid}");
        await WaitFrames(2);
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private bool ValidateAuthoredCollisionSources(
        JianghaiGameplayCollisionResult? gameplay,
        out int namedSources,
        out int physicsHits,
        out int doorClears,
        out int facadeWallHits,
        out int sideWallHits,
        out int backWallHits,
        out int linerSurfaceHits,
        out int wingWallHits,
        out int overhangClears,
        out string summary)
    {
        namedSources = 0;
        physicsHits = 0;
        doorClears = 0;
        facadeWallHits = 0;
        sideWallHits = 0;
        backWallHits = 0;
        linerSurfaceHits = 0;
        wingWallHits = 0;
        overhangClears = 0;
        summary = "missing_body";
        if (gameplay is null || !IsInstanceValid(gameplay.Body))
        {
            return false;
        }

        var expectedNames = JianghaiGameplayCollisionContract.ExpectedAuthoredSourceNames;
        var expectedNameSet = new HashSet<string>(expectedNames, StringComparer.Ordinal);
        var sourceShapes = gameplay.Body.GetChildren()
            .OfType<CollisionShape3D>()
            .Where(shape => shape.HasMeta("gameplay_source_node"))
            .ToArray();
        var sourceGroups = sourceShapes
            .GroupBy(
                shape => shape.GetMeta("gameplay_source_node", string.Empty).AsString(),
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var actualNames = new HashSet<string>(sourceGroups.Keys, StringComparer.Ordinal);
        var missingNames = expectedNames
            .Where(name => !actualNames.Contains(name))
            .ToArray();
        var unexpectedNames = actualNames
            .Where(name => !expectedNameSet.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var invalidCountNames = sourceGroups
            .Where(pair => pair.Value.Length != (
                JianghaiGameplayCollisionContract.ExpectedEnterableSourceNames.Contains(
                    pair.Key,
                    StringComparer.Ordinal)
                    ? JianghaiGameplayCollisionContract.EnterableShapesPerSource
                    : 1))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var invalidShapes = sourceShapes
            .Where(shape => shape.Shape is not BoxShape3D
                || shape.GetMeta(
                    "gameplay_source_collision_role",
                    string.Empty).AsString()
                    != JianghaiGameplayCollisionContract.AuthoredDensityCollisionRole)
            .Select(shape => shape.GetMeta(
                "gameplay_source_node",
                string.Empty).AsString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var invalidKinds = sourceGroups
            .Where(pair => pair.Value.Any(shape =>
                shape.GetMeta("gameplay_source_kind", string.Empty).AsString()
                    != ExpectedAuthoredSourceKind(pair.Key)
                || ExpectedDensitySource(pair.Key)
                    && shape.GetMeta(
                        "gameplay_source_district_role",
                        string.Empty).AsString()
                        != JianghaiGameplayCollisionContract.AuthoredDensityDistrictRole))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var invalidEnterableRoles = sourceGroups
            .Where(pair => ExpectedEnterableSource(pair.Key)
                && !HasEnterableShellRoles(pair.Value))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        namedSources = expectedNames.Count(name => actualNames.Contains(name));
        if (sourceShapes.Length != JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount
            || missingNames.Length > 0
            || unexpectedNames.Length > 0
            || invalidCountNames.Length > 0
            || invalidShapes.Length > 0
            || invalidKinds.Length > 0
            || invalidEnterableRoles.Length > 0)
        {
            summary = "contract"
                + $":missing={FormatCollisionSourceNames(missingNames)}"
                + $":unexpected={FormatCollisionSourceNames(unexpectedNames)}"
                + $":counts={FormatCollisionSourceNames(invalidCountNames)}"
                + $":invalid={FormatCollisionSourceNames(invalidShapes)}"
                + $":kinds={FormatCollisionSourceNames(invalidKinds)}"
                + $":roles={FormatCollisionSourceNames(invalidEnterableRoles)}";
            return false;
        }

        using var probeShape = new SphereShape3D { Radius = 0.05f };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = probeShape,
            CollisionMask = 1,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Margin = 0.001f
        };
        var expectedBodyId = gameplay.Body.GetInstanceId();
        var space = GetWorld3D().DirectSpaceState;
        foreach (var shape in sourceShapes)
        {
            query.Transform = new Transform3D(Basis.Identity, shape.GlobalPosition);
            var hits = space.IntersectShape(query, 64);
            using var hitsBacking = hits.AsDisposable();
            var bodyHit = false;
            for (var index = 0; index < hits.Count; index++)
            {
                using var hit = hits[index];
                using var colliderValue = hit[GodotPhysicsResultKeys.Collider];
                if (colliderValue.AsGodotObject() is Node collider
                    && collider.GetInstanceId() == expectedBodyId)
                {
                    bodyHit = true;
                    break;
                }
            }
            if (!bodyHit)
            {
                summary = "physics_missing:"
                    + shape.GetMeta("gameplay_source_node", string.Empty).AsString()
                    + ':'
                    + shape.GetMeta("gameplay_proxy_role", string.Empty).AsString();
                return false;
            }
            physicsHits++;
        }

        if (!ValidateEnterableDoorways(
                gameplay,
                sourceGroups,
                out doorClears,
                out facadeWallHits,
                out sideWallHits,
                out backWallHits,
                out linerSurfaceHits,
                out wingWallHits,
                out overhangClears,
                out summary))
        {
            return false;
        }

        summary = "ok";
        return namedSources == JianghaiGameplayCollisionContract.ExpectedAuthoredSourceCount
            && physicsHits == JianghaiGameplayCollisionContract.ExpectedAuthoredShapeCount;
    }

    private static bool ExpectedDensitySource(string sourceName)
        => JianghaiGameplayCollisionContract.IsExpectedDensitySource(sourceName);

    private static bool ExpectedEnterableSource(string sourceName)
        => JianghaiGameplayCollisionContract.IsExpectedEnterableSource(sourceName);

    private static string ExpectedAuthoredSourceKind(string sourceName)
        => ExpectedDensitySource(sourceName)
            ? "density"
            : JianghaiGameplayCollisionContract.IsExpectedSolidSource(sourceName)
                ? "solid"
                : "enterable";

    private static bool HasEnterableShellRoles(IReadOnlyList<CollisionShape3D> shapes)
    {
        var roles = shapes
            .Select(shape => shape.GetMeta(
                "gameplay_proxy_role",
                string.Empty).AsString())
            .ToHashSet(StringComparer.Ordinal);
        return roles.SetEquals(new[]
        {
            "side_left",
            "side_right",
            "back",
            "front_left",
            "front_right",
            "front_wing_left",
            "front_wing_right",
            "front_connector_left",
            "front_connector_right",
            "rear_wing_left",
            "rear_wing_right",
            "rear_connector_left",
            "rear_connector_right",
            "front_outer_connector_left",
            "front_outer_connector_right",
            "rear_outer_connector_left",
            "rear_outer_connector_right",
            "front_lintel",
            "ceiling",
            "liner_left",
            "liner_right",
            "liner_back",
            "liner_ceiling"
        });
    }

    private static string FormatCollisionSourceNames(IReadOnlyCollection<string> names)
        => names.Count == 0 ? "none" : string.Join(',', names);
}
