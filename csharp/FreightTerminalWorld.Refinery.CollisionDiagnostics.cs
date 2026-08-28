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
        var authoredCollisionAvailable = _jianghaiAuthoredBuildingCollision is { } collision
            && IsInstanceValid(collision.Body);
        var expectedLegacyProxies = authoredCollisionAvailable
            ? 0
            : RefineryLayout.Models.Count(placement => placement.HasCollision);
        var tenementShapes = AuthoredCollisionAnchorCount("JianghaiTenementDistrict");
        var factoryShapes = AuthoredCollisionAnchorCount("RedStarElectronicsFactory");
        var pawnshopShapes = AuthoredCollisionAnchorCount("GuangchangPawnshop");
        var marketShapes = AuthoredCollisionAnchorCount("OldCityMarketBridge");
        var authoredReady = IsBlackwaterRefineryMap
            && _jianghaiAuthoredBuildingCollisionError is null
            && _jianghaiAuthoredBuildingCollision is { } authored
            && IsInstanceValid(authored.Body)
            && authored.Body.CollisionLayer == 1
            && authored.Body.CollisionMask == 0
            && authored.SourceMeshCount == 220
            && authored.StructuralSourceMeshCount == 107
            && authored.DetailSourceMeshCount == 113
            && authored.CollisionShapeCount == 220
            && authored.ConcaveShapeCount == 220
            && authored.SharedShapeCount > 0
            && authored.BakedShapeCount > 0
            && authored.UniqueMeshCount >= 6
            && authored.InstanceTriangleCount > 0
            && tenementShapes == 94
            && factoryShapes == 11
            && pawnshopShapes == 73
            && marketShapes == 42
            && counts.AuthoredCollisionBodies == 1
            && counts.AuthoredCollisionShapes == 220
            && counts.AuthoredConcaveCollisionShapes == 220
            && counts.AuthoredNonConcaveCollisionShapes == 0;
        var legacyReady = expectedLegacyProxies == 0
            && _refineryCollisionProxyCount == expectedLegacyProxies
            && counts.LegacyCollisionBodies == expectedLegacyProxies
            && counts.LegacyCollisionShapes == expectedLegacyProxies
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
        var valid = authoredReady && legacyReady && physicsReady && lootAccessReady
            && routeReady && landmarksReady;

        GD.Print(
            $"REFINERY_COLLISION_CHECK valid={valid} authored={authoredReady} "
            + $"body={counts.AuthoredCollisionBodies}/1 shapes={counts.AuthoredCollisionShapes}/220 "
            + $"concave={counts.AuthoredConcaveCollisionShapes}/220 "
            + $"sources={_jianghaiAuthoredBuildingCollision?.StructuralSourceMeshCount ?? 0}/"
            + $"{_jianghaiAuthoredBuildingCollision?.DetailSourceMeshCount ?? 0} "
            + $"anchors={tenementShapes}/{factoryShapes}/{pawnshopShapes}/{marketShapes} "
            + $"shared={_jianghaiAuthoredBuildingCollision?.SharedShapeCount ?? 0} "
            + $"baked={_jianghaiAuthoredBuildingCollision?.BakedShapeCount ?? 0} "
            + $"unique={_jianghaiAuthoredBuildingCollision?.UniqueMeshCount ?? 0} "
            + $"triangles={_jianghaiAuthoredBuildingCollision?.InstanceTriangleCount ?? 0} "
            + $"legacy={legacyReady}:{counts.LegacyCollisionBodies}/{counts.LegacyCollisionShapes}/"
            + $"{expectedLegacyProxies} ballistic={physicsReady}:{blockingHits}/5:{clearRoutes}/3:"
            + $"{physicsSummary} routes={routeReady}:{routeCount}:{routeBlocker} "
            + $"loot_access={lootAccessReady}:{accessibleHighValueLoot}/12:{lootAccessBlocker} "
            + $"landmarks={landmarksReady}");
        GD.Print($"REFINERY_COLLISION_PASS valid={valid}");
        await WaitFrames(2);
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
