using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public enum OrbitalComplexVerticalLayer
{
    DryDock,
    ServiceDeck,
    Catwalk
}

public enum OrbitalComplexLootRisk
{
    OuterRing,
    ObjectiveDistrict,
    StormglassLockdown
}

public readonly record struct OrbitalComplexMapBounds(
    Rect2 Horizontal,
    float MinimumY,
    float MaximumY)
{
    public Vector3 Center => new(
        Horizontal.GetCenter().X,
        (MinimumY + MaximumY) * 0.5f,
        Horizontal.GetCenter().Y);
}

public readonly record struct OrbitalComplexSpawnPad(
    string Id,
    Vector3 Position,
    Vector3 LookTarget,
    OrbitalComplexVerticalLayer Layer);

public readonly record struct OrbitalComplexPatrolRoute(
    string Id,
    IReadOnlyList<Vector3> Waypoints,
    bool Loop,
    OrbitalComplexVerticalLayer Layer);

public readonly record struct OrbitalComplexObjectiveDefinition(
    string Id,
    string EnglishName,
    string ChineseName,
    string District,
    Vector3 Position,
    float YawRadians,
    OrbitalComplexVerticalLayer Layer,
    string CompletionSignal)
{
    /// <summary>
    /// Stable localization key carried with the objective so UI and backend payloads can
    /// validate the same contract without comparing translated text.
    /// </summary>
    public string LocalizationKey => Id switch
    {
        OrbitalComplexMapDefinition.BreakerObjectiveId
            => OrbitalComplexMapDefinition.BreakerObjectiveLocalizationKey,
        OrbitalComplexMapDefinition.QuarantineObjectiveId
            => OrbitalComplexMapDefinition.QuarantineObjectiveLocalizationKey,
        _ => string.Empty
    };
}

public readonly record struct OrbitalComplexExtractionDefinition(
    string Id,
    Vector3 Position,
    float Radius,
    string EnglishName,
    string ChineseName);

public readonly record struct OrbitalComplexWeaponCasePlacement(
    string Id,
    Vector3 Position,
    float YawRadians,
    WeaponPlatform Platform,
    int BuildTier,
    LootGrade Grade,
    OrbitalComplexLootRisk Risk,
    string EnglishName,
    string ChineseName);

public readonly record struct OrbitalComplexLootPlacement(
    string Id,
    Vector3 Position,
    LootGrade Grade,
    OrbitalComplexLootRisk Risk,
    string EnglishName,
    string ChineseName);

public readonly record struct OrbitalComplexValuablePlacement(
    string Id,
    Vector3 Position,
    ValuableItemKind Kind,
    LootGrade Grade,
    OrbitalComplexLootRisk Risk);

public readonly record struct OrbitalComplexExplosivePlacement(
    string Id,
    Vector3 Position,
    float BlastScale,
    string ChainGroup);

public readonly record struct OrbitalComplexMinimapLandmark(
    string Id,
    Vector3 Position,
    string LocalizationKey,
    string EnglishName,
    Color Color,
    OrbitalComplexVerticalLayer Layer);

public readonly record struct OrbitalComplexRouteProbe(
    string Id,
    Vector3 From,
    Vector3 To,
    float MinimumClearance,
    int RequiredObjectiveStage,
    OrbitalComplexVerticalLayer Layer);

public readonly record struct OrbitalComplexCollisionBox(
    string Id,
    Vector3 Position,
    Vector3 Size,
    Vector3 RotationRadians,
    string Purpose);

public readonly record struct OrbitalComplexRampDefinition(
    string Id,
    Vector3 Position,
    Vector3 Size,
    Vector3 RotationRadians,
    Vector3 LowApproach,
    Vector3 HighApproach,
    OrbitalComplexVerticalLayer DestinationLayer);

public readonly record struct OrbitalComplexPowerGateDefinition(
    string Id,
    string AuthoredVisualNodeName,
    Vector3 Position,
    Vector3 Size,
    Vector3 RotationRadians,
    int OpensAtObjectiveStage,
    bool HideVisualWhenOpen);

public sealed record OrbitalComplexMapLayout(
    ulong SharedWorldSeed,
    OrbitalComplexMapBounds Bounds,
    IReadOnlyList<OrbitalComplexSpawnPad> PlayerSpawnPads,
    IReadOnlyList<OrbitalComplexSpawnPad> RivalSpawnPads,
    IReadOnlyList<Vector3> GarrisonSpawns,
    IReadOnlyList<OrbitalComplexPatrolRoute> PatrolRoutes,
    IReadOnlyList<Vector3> CoverPoints,
    IReadOnlyList<Vector3> QrfSpawns,
    IReadOnlyList<Vector3> BossRoute,
    IReadOnlyList<OrbitalComplexObjectiveDefinition> Objectives,
    OrbitalComplexExtractionDefinition Extraction,
    IReadOnlyList<OrbitalComplexWeaponCasePlacement> WeaponCases,
    IReadOnlyList<OrbitalComplexLootPlacement> GradedLoot,
    IReadOnlyList<OrbitalComplexValuablePlacement> Valuables,
    IReadOnlyList<OrbitalComplexExplosivePlacement> Explosives,
    IReadOnlyList<OrbitalComplexMinimapLandmark> MinimapLandmarks,
    IReadOnlyList<OrbitalComplexRouteProbe> RouteProbes,
    IReadOnlyList<OrbitalComplexCollisionBox> CollisionBoxes,
    IReadOnlyList<OrbitalComplexRampDefinition> Ramps,
    IReadOnlyList<OrbitalComplexPowerGateDefinition> PowerGates);
