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
        var expectedLandmarkShapes = _oldTownLandmarks?.CollisionShapeCount ?? 0;
        var gameplayReady = IsBlackwaterRefineryMap
            && _jianghaiGameplayCollisionError is null
            && _jianghaiGameplayCollision is { } gameplay
            && IsInstanceValid(gameplay.Body)
            && gameplay.Body.CollisionLayer == 1
            && gameplay.Body.CollisionMask == 0
            && gameplay.SourcePlacementCount == expectedPlacements
            && gameplay.AuthoredSourceMeshCount == 6
            && gameplay.CollisionShapeCount == expectedPlacements + 6
            && gameplay.BoxShapeCount == gameplay.CollisionShapeCount
            && gameplay.ConcaveShapeCount == 0
            && gameplay.DistrictShapeCounts.Count >= 10
            && counts.GameplayCollisionBodies == 2
            && counts.GameplayCollisionShapes
                == gameplay.CollisionShapeCount + expectedLandmarkShapes
            && counts.GameplayBoxCollisionShapes == counts.GameplayCollisionShapes
            && counts.GameplayNonBoxCollisionShapes == 0;
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
            + $"authored_proxies={_jianghaiGameplayCollision?.AuthoredSourceMeshCount ?? 0}/6 "
            + $"districts={_jianghaiGameplayCollision?.DistrictShapeCounts.Count ?? 0} "
            + $"legacy={legacyReady}:{counts.LegacyCollisionBodies}/{counts.LegacyCollisionShapes}/0 "
            + $"ballistic={physicsReady}:{blockingHits}/11:{clearRoutes}/3:{physicsSummary} "
            + $"routes={routeReady}:{routeCount}:{routeBlocker} "
            + $"loot_access={lootAccessReady}:{accessibleHighValueLoot}/12:{lootAccessBlocker} "
            + $"landmarks={landmarksReady}");
        GD.Print($"REFINERY_COLLISION_PASS valid={valid}");
        await WaitFrames(2);
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
