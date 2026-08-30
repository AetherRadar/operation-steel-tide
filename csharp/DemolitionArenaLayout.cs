using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public readonly record struct DemolitionArenaBox(
    string Name,
    Vector3 Center,
    Vector3 Size,
    string Material,
    Vector3 Rotation = default,
    bool Visible = true);

public readonly record struct DemolitionArenaProp(
    string Name,
    string ScenePath,
    Vector3 Position,
    float Yaw,
    float Scale,
    Vector3 CollisionSize,
    Vector3 CollisionOffset);

public readonly record struct DemolitionArenaMarker(
    Vector3 Position,
    string LocalizationKey,
    string EnglishName,
    Color Accent);

/// <summary>
/// Authored demolition arena coordinates and pure balance checks. This data type owns no
/// scene nodes, so route timing and clearance rules can be validated without a running tree.
/// Sites sit on opposite diagonal corners of each arena so rotations always cross mid.
/// </summary>
public sealed partial class DemolitionArenaLayout
{
    public const float MinimumPassageWidth = 2.6f;
    public const float MinimumPassageHeight = 2.45f;
    public const float MaximumSiteTravelDifference = 0.12f;
    public const int MinimumCentralCoverBodyCount = 8;
    internal static IReadOnlyList<string> StrategyTargetKeys { get; } = Array.AsReadOnly(new[]
    {
        "attack_entry_a", "attack_entry_b", "attack_support_a", "attack_support_b", "attack_mid_recon",
        "defense_anchor_a", "defense_anchor_b", "defense_mid", "defense_rotate_a", "defense_rotate_b",
        "retake_entry_a", "retake_entry_b", "retake_cover_a", "retake_cover_b", "retake_flank_a", "retake_flank_b",
        "postplant_guard_a", "postplant_guard_b", "postplant_crossfire_a", "postplant_crossfire_b",
        "postplant_lurk_a", "postplant_lurk_b", "site_a", "site_b"
    });

    public static readonly Vector3 WorldOrigin = new(285.0f, 0.0f, -35.0f);

    /// <summary>Site centers in arena-local coordinates, indexed by site number.</summary>
    public static readonly Vector2[] LocalSiteCenters =
    {
        new(-33.0f, 21.0f),
        new(33.0f, -19.0f)
    };

    public string MapId { get; }
    public string EnglishName => DemolitionMapCatalog.Resolve(MapId).EnglishName;
    public string LocalizationKey => DemolitionMapCatalog.Resolve(MapId).LocalizationKey;
    public Vector3 Origin { get; }
    public Vector3 AttackSpawn { get; }
    public Vector3 DefenderSpawn { get; }
    public Vector3 Midpoint { get; }
    public Rect2 WorldBounds { get; }
    public IReadOnlyList<Vector3> SitePositions { get; }
    public IReadOnlyList<Vector2> LocalSiteCoordinates { get; }
    public IReadOnlyList<Vector3> DefenderSpawns { get; }
    public IReadOnlyList<Vector3> AttackSpawns { get; }
    public IReadOnlyList<Vector3> CoverPoints { get; }
    public IReadOnlyList<DemolitionArenaBox> CollisionBoxes { get; }
    /// <summary>Walkable collision excluded from route-clearance blocker checks.</summary>
    public IReadOnlyList<DemolitionArenaBox> TraversalBoxes { get; }
    public IReadOnlyList<DemolitionArenaBox> NavigationBoxes { get; }
    public IReadOnlyList<DemolitionArenaBox> DetailBoxes { get; }
    public IReadOnlyList<DemolitionArenaProp> Props { get; }
    public IReadOnlyList<DemolitionArenaMarker> Markers { get; }
    public IReadOnlyList<Vector3> AttackToAPath { get; }
    public IReadOnlyList<Vector3> AttackToBPath { get; }
    public IReadOnlyList<Vector3> AttackApproachToAPath { get; }
    public IReadOnlyList<Vector3> AttackApproachToBPath { get; }
    public IReadOnlyList<Vector3> AttackMidPath { get; }
    public IReadOnlyList<Vector3> DefenderToAPath { get; }
    public IReadOnlyList<Vector3> DefenderToBPath { get; }
    public IReadOnlyList<Vector3> SiteRotationPath { get; }
    /// <summary>Additional authored graph links, including complete elevation transitions.</summary>
    public IReadOnlyList<IReadOnlyList<Vector3>> AuxiliaryPaths { get; }
    public IReadOnlyList<float> CriticalPassageWidths { get; }
    public IReadOnlyList<float> CriticalPassageHeights { get; }
    public int CentralCoverBodyCount { get; }
    public bool CentralPropsDoNotOverlap { get; }

    public DemolitionArenaLayout(Vector3? origin = null)
        : this(DemolitionMapCatalog.TideforgeId, origin)
    {
    }

    public DemolitionArenaLayout(string mapId, Vector3? origin = null)
    {
        MapId = string.Equals(mapId, DemolitionMapCatalog.HarborLocksId, StringComparison.OrdinalIgnoreCase)
            ? DemolitionMapCatalog.HarborLocksId
            : string.Equals(mapId, DemolitionMapCatalog.TideglassReactorId, StringComparison.OrdinalIgnoreCase)
                ? DemolitionMapCatalog.TideglassReactorId
                : string.Equals(mapId, DemolitionMapCatalog.BazaarCrossingId, StringComparison.OrdinalIgnoreCase)
                    ? DemolitionMapCatalog.BazaarCrossingId
                    : DemolitionMapCatalog.TideforgeId;
        Origin = origin ?? WorldOrigin;
        var harborLocks = MapId == DemolitionMapCatalog.HarborLocksId;
        var tideglassReactor = MapId == DemolitionMapCatalog.TideglassReactorId;
        var bazaarCrossing = MapId == DemolitionMapCatalog.BazaarCrossingId;
        if (harborLocks)
        {
            AttackSpawn = World(new Vector3(-32.0f, 0.22f, 35.0f));
            DefenderSpawn = World(new Vector3(32.0f, 0.22f, -35.0f));
            Midpoint = World(new Vector3(0.0f, 0.12f, 0.0f));
            WorldBounds = new Rect2(Origin.X - 58.0f, Origin.Z - 42.0f, 116.0f, 84.0f);
            LocalSiteCoordinates = Array.AsReadOnly(new[]
            {
                new Vector2(-41.0f, -20.0f),
                new Vector2(41.0f, 20.0f)
            });
            SitePositions = WorldPoints(
                new(-41.0f, 0.18f, -20.0f),
                new(41.0f, 0.18f, 20.0f));
            AttackSpawns = WorldPoints(
                new(-35.0f, 0.22f, 36.0f),
                new(-31.0f, 0.22f, 36.0f),
                new(-37.0f, 0.22f, 33.0f),
                new(-29.0f, 0.22f, 33.0f),
                new(-33.0f, 0.22f, 34.0f));
            DefenderSpawns = WorldPoints(
                new(29.0f, 0.22f, -36.0f),
                new(33.0f, 0.22f, -36.0f),
                new(27.0f, 0.22f, -33.0f),
                new(35.0f, 0.22f, -33.0f),
                new(31.0f, 0.22f, -34.0f));
            CoverPoints = WorldPoints(
                new(-50.0f, 0.2f, -24.0f), new(-45.0f, 0.2f, -14.0f),
                new(-34.0f, 0.2f, -20.0f), new(-34.0f, 0.2f, -25.0f),
                new(50.0f, 0.2f, 24.0f), new(46.0f, 0.2f, 14.0f),
                new(34.0f, 0.2f, 20.0f), new(34.0f, 0.2f, 25.0f),
                new(-30.0f, 0.2f, 5.0f), new(-20.0f, 0.2f, 3.0f),
                new(-9.0f, 0.2f, 1.0f), new(0.0f, 0.2f, 4.0f),
                new(9.0f, 0.2f, -1.0f), new(20.0f, 0.2f, -3.0f),
                new(30.0f, 0.2f, -5.0f), new(-24.0f, 0.2f, 24.0f),
                new(-12.0f, 0.2f, 21.0f), new(12.0f, 0.2f, -22.0f),
                new(24.0f, 0.2f, -24.0f), new(-52.0f, 0.2f, 8.0f),
                new(52.0f, 0.2f, -8.0f), new(-4.0f, 0.2f, -25.0f),
                new(4.0f, 0.2f, 25.0f), new(28.0f, 0.2f, 12.0f));
        }
        else if (tideglassReactor)
        {
            AttackSpawn = World(new Vector3(52.0f, 0.22f, 48.0f));
            DefenderSpawn = World(new Vector3(-52.0f, 0.22f, -48.0f));
            Midpoint = World(new Vector3(0.0f, 0.12f, 0.0f));
            WorldBounds = new Rect2(Origin.X - 68.0f, Origin.Z - 56.0f, 136.0f, 112.0f);
            LocalSiteCoordinates = Array.AsReadOnly(new[]
            {
                new Vector2(-40.0f, 24.0f),
                new Vector2(42.0f, -25.0f)
            });
            SitePositions = WorldPoints(
                new(-40.0f, 0.18f, 24.0f),
                new(42.0f, 0.18f, -25.0f));
            AttackSpawns = WorldPoints(
                new(49.0f, 0.22f, 49.0f),
                new(53.0f, 0.22f, 49.0f),
                new(47.0f, 0.22f, 46.0f),
                new(55.0f, 0.22f, 46.0f),
                new(51.0f, 0.22f, 47.0f));
            DefenderSpawns = WorldPoints(
                new(-55.0f, 0.22f, -49.0f),
                new(-51.0f, 0.22f, -49.0f),
                new(-57.0f, 0.22f, -46.0f),
                new(-49.0f, 0.22f, -46.0f),
                new(-53.0f, 0.22f, -47.0f));
            CoverPoints = WorldPoints(
                new(-46.0f, 0.2f, 32.0f), new(-44.0f, 0.2f, 15.0f),
                new(-36.0f, 0.2f, 29.0f), new(-24.0f, 0.2f, 19.0f),
                new(53.0f, 0.2f, -31.0f), new(45.0f, 0.2f, -17.0f),
                new(42.0f, 0.2f, -30.0f), new(40.0f, 0.2f, -17.0f),
                new(-21.5f, 0.2f, 5.0f), new(-22.0f, 0.2f, 10.0f),
                new(-9.0f, 0.2f, -8.0f), new(8.5f, 0.2f, 9.0f),
                new(27.0f, 0.2f, -12.0f), new(46.0f, 0.2f, 12.0f),
                new(-37.0f, 0.2f, -7.0f), new(29.0f, 0.2f, 11.0f),
                new(-55.0f, 0.2f, 8.0f), new(56.0f, 0.2f, -1.0f),
                new(-14.0f, 0.2f, 31.0f), new(12.0f, 0.2f, -34.0f),
                new(-40.0f, 0.2f, 24.0f), new(42.0f, 0.2f, -25.0f),
                new(31.0f, 0.2f, 34.0f), new(-31.0f, 0.2f, -34.0f),
                new(61.0f, 0.2f, 17.0f), new(-59.0f, 0.2f, -19.0f));
        }
        else if (bazaarCrossing)
        {
            AttackSpawn = World(new Vector3(0.0f, 0.22f, 49.0f));
            DefenderSpawn = World(new Vector3(0.0f, 0.22f, -49.0f));
            Midpoint = World(new Vector3(0.0f, 0.2f, -14.5f));
            WorldBounds = new Rect2(Origin.X - 68.0f, Origin.Z - 56.0f, 136.0f, 112.0f);
            LocalSiteCoordinates = BuildBazaarCrossingLocalSiteCoordinates();
            SitePositions = BuildBazaarCrossingSitePositions();
            AttackSpawns = BuildBazaarCrossingAttackSpawns();
            DefenderSpawns = BuildBazaarCrossingDefenderSpawns();
            CoverPoints = BuildBazaarCrossingCoverPoints();
        }
        else
        {
            AttackSpawn = World(new Vector3(0.0f, 0.22f, 54.0f));
            DefenderSpawn = World(new Vector3(0.0f, 0.22f, -54.0f));
            Midpoint = World(new Vector3(0.0f, 0.12f, 2.0f));
            WorldBounds = new Rect2(Origin.X - 40.0f, Origin.Z - 56.0f, 80.0f, 112.0f);
            LocalSiteCoordinates = Array.AsReadOnly(LocalSiteCenters);
            SitePositions = WorldPoints(
                new(-33.0f, 0.18f, 21.0f),
                new(33.0f, 0.18f, -19.0f));
            AttackSpawns = WorldPoints(
                new(-3.0f, 0.22f, 54.0f),
                new(3.0f, 0.22f, 54.0f),
                new(-6.0f, 0.22f, 51.0f),
                new(6.0f, 0.22f, 51.0f),
                new(0.0f, 0.22f, 52.0f));
            DefenderSpawns = WorldPoints(
                new(-3.0f, 0.22f, -52.0f),
                new(3.0f, 0.22f, -52.0f),
                new(-8.0f, 0.22f, -50.0f),
                new(8.0f, 0.22f, -50.0f),
                new(0.0f, 0.22f, -51.0f));
            CoverPoints = WorldPoints(
                new(-29.0f, 0.2f, 27.0f), new(-27.0f, 0.2f, 14.0f),
                new(-36.0f, 0.2f, 24.0f), new(-30.0f, 0.2f, 8.0f),
                new(30.0f, 0.2f, -27.0f), new(36.0f, 0.2f, -22.0f),
                new(26.0f, 0.2f, -16.0f), new(21.0f, 0.2f, -8.0f),
                new(-8.0f, 0.2f, 4.0f), new(8.0f, 0.2f, 4.0f),
                new(-8.0f, 0.2f, -7.0f), new(8.0f, 0.2f, -7.0f),
                new(-15.0f, 0.2f, 31.0f), new(13.0f, 0.2f, 26.0f),
                new(-22.0f, 0.2f, 6.0f), new(22.0f, 0.2f, 2.0f),
                new(-9.3f, 0.2f, 17.5f), new(-4.3f, 0.2f, 17.5f),
                new(4.3f, 0.2f, 12.0f), new(9.3f, 0.2f, 12.0f),
                new(-6.7f, 0.2f, 31.0f), new(-2.9f, 0.2f, 31.0f),
                new(2.7f, 0.2f, 27.0f), new(6.5f, 0.2f, 27.0f),
                new(-9.5f, 0.2f, -31.0f), new(-4.5f, 0.2f, -31.0f),
                new(4.5f, 0.2f, -36.0f), new(9.5f, 0.2f, -36.0f),
                new(18.5f, 0.2f, 22.0f), new(27.5f, 0.2f, 22.0f),
                new(23.0f, 0.2f, 16.0f), new(23.0f, 0.2f, 28.0f),
                new(-27.0f, 0.2f, -26.0f), new(-17.0f, 0.2f, -26.0f),
                new(-22.0f, 0.2f, -20.5f), new(-22.0f, 0.2f, -31.5f));
        }

        CollisionBoxes = harborLocks
            ? BuildHarborLocksCollisionBoxes()
            : tideglassReactor
                ? BuildTideglassReactorCollisionBoxes()
                : bazaarCrossing
                    ? BuildBazaarCrossingCollisionBoxes()
                    : BuildCollisionBoxes();
        TraversalBoxes = bazaarCrossing
            ? BuildBazaarCrossingTraversalBoxes()
            : Array.Empty<DemolitionArenaBox>();
        NavigationBoxes = tideglassReactor
            ? BuildTideglassReactorNavigationBoxes()
            : Array.Empty<DemolitionArenaBox>();
        DetailBoxes = bazaarCrossing ? Array.Empty<DemolitionArenaBox>()
            : harborLocks ? BuildHarborLocksDetailBoxes()
            : tideglassReactor ? BuildTideglassReactorDetailBoxes()
            : BuildDetailBoxes();
        Props = bazaarCrossing ? Array.Empty<DemolitionArenaProp>()
            : harborLocks ? BuildHarborLocksProps()
            : tideglassReactor ? BuildTideglassReactorProps()
            : BuildProps();
        CentralCoverBodyCount = CollisionBoxes.Count(box => box.Name.StartsWith("MidCover", StringComparison.Ordinal))
            + Props.Count(prop => prop.Name.StartsWith("MidCover", StringComparison.Ordinal));
        CentralPropsDoNotOverlap = !Props.Any(prop => CollisionBoxes.Any(box =>
            box.Name.StartsWith("MidCover", StringComparison.Ordinal)
            && GroundFootprintsOverlap(box, prop)));
        Markers = BuildMarkers();
        AttackToAPath = harborLocks ? BuildHarborLocksAttackToAPath()
            : tideglassReactor ? BuildTideglassReactorAttackToAPath()
            : bazaarCrossing ? BuildBazaarCrossingAttackToAPath() : WorldPoints(
            new(0, 0.2f, 54), new(0, 0.2f, 46),
            new(-6, 0.2f, 36), new(-11, 0.2f, 26),
            new(-12, 0.2f, 10), new(-18, 0.2f, 5),
            new(-23, 0.2f, 10), new(-30, 0.2f, 15),
            new(-28, 0.2f, 18), new(-33, 0.2f, 21));
        AttackToBPath = harborLocks ? BuildHarborLocksAttackToBPath()
            : tideglassReactor ? BuildTideglassReactorAttackToBPath()
            : bazaarCrossing ? BuildBazaarCrossingAttackToBPath() : WorldPoints(
            new(0, 0.2f, 54), new(0, 0.2f, 46),
            new(6, 0.2f, 33), new(8, 0.2f, 20),
            new(15, 0.2f, 10), new(15, 0.2f, 5),
            new(25, 0.2f, 5), new(25, 0.2f, -4),
            new(29, 0.2f, -10), new(33, 0.2f, -19));
        AttackApproachToAPath = harborLocks || tideglassReactor || bazaarCrossing
            ? AttackToAPath
            : WorldPoints(
                new(-3, 0.2f, 54), new(-6, 0.2f, 46),
                new(-14, 0.2f, 46), new(-20, 0.2f, 42),
                new(-24, 0.2f, 38), new(-25, 0.2f, 30),
                new(-33, 0.2f, 21));
        AttackApproachToBPath = harborLocks || tideglassReactor || bazaarCrossing
            ? AttackToBPath
            : WorldPoints(
                    new(3, 0.2f, 51), new(9, 0.2f, 48),
                    new(13, 0.2f, 43), new(16, 0.2f, 35),
                    new(16, 0.2f, 29), new(13, 0.2f, 24),
                    new(12, 0.2f, 17),
                    new(15, 0.2f, 10), new(15, 0.2f, 5),
                    new(25, 0.2f, 5), new(25, 0.2f, -4),
                    new(29, 0.2f, -10), new(33, 0.2f, -19));
        AttackMidPath = harborLocks ? BuildHarborLocksAttackMidPath()
            : tideglassReactor ? BuildTideglassReactorAttackMidPath()
            : bazaarCrossing ? BuildBazaarCrossingAttackMidPath() : WorldPoints(
            new(0, 0.2f, 54), new(0, 0.2f, 46),
            new(0, 0.2f, 38), new(0, 0.2f, 12),
            new(0, 0.2f, 4));
        DefenderToAPath = harborLocks ? BuildHarborLocksDefenderToAPath()
            : tideglassReactor ? BuildTideglassReactorDefenderToAPath()
            : bazaarCrossing ? BuildBazaarCrossingDefenderToAPath() : WorldPoints(
            new(0, 0.2f, -54), new(0, 0.2f, -46),
            new(0, 0.2f, -40), new(-4.0f, 0.2f, -35),
            new(-4.0f, 0.2f, -27), new(-8.0f, 0.2f, -22),
            new(-8.0f, 0.2f, -7), new(-8.0f, 0.2f, -5),
            new(-16.0f, 0.2f, 3), new(-24.0f, 0.2f, 4),
            new(-24.0f, 0.2f, 16), new(-28.0f, 0.2f, 21),
            new(-33.0f, 0.2f, 21));
        DefenderToBPath = harborLocks ? BuildHarborLocksDefenderToBPath()
            : tideglassReactor ? BuildTideglassReactorDefenderToBPath()
            : bazaarCrossing ? BuildBazaarCrossingDefenderToBPath() : WorldPoints(
            new(0, 0.2f, -54), new(0, 0.2f, -46),
            new(7.0f, 0.2f, -43), new(11.0f, 0.2f, -39),
            new(11.0f, 0.2f, -28), new(20.0f, 0.2f, -24),
            new(28.0f, 0.2f, -20), new(33.0f, 0.2f, -19));
        SiteRotationPath = harborLocks ? BuildHarborLocksSiteRotationPath()
            : tideglassReactor ? BuildTideglassReactorSiteRotationPath()
            : bazaarCrossing ? BuildBazaarCrossingSiteRotationPath() : WorldPoints(
            new(-33, 0.2f, 21), new(-28, 0.2f, 21), new(-24, 0.2f, 16),
            new(-24, 0.2f, 4), new(-16, 0.2f, 3), new(-15, 0.2f, 0),
            new(-8, 0.2f, -1), new(-8, 0.2f, -5), new(0, 0.2f, -5),
            new(6, 0.2f, -1), new(14.6f, 0.2f, -1), new(15, 0.2f, 4),
            new(15, 0.2f, 6), new(21, 0.2f, 6), new(24, 0.2f, -2), new(24, 0.2f, -9),
            new(30, 0.2f, -11), new(33, 0.2f, -19));
        AuxiliaryPaths = bazaarCrossing
            ? BuildBazaarCrossingAuxiliaryPaths()
            : Array.Empty<IReadOnlyList<Vector3>>();
        CriticalPassageWidths = harborLocks
            ? new[] { 3.2f, 3.8f, 4.4f, 5.0f, 7.2f }
            : tideglassReactor
                ? new[] { 3.4f, 3.8f, 4.6f, 5.4f, 7.0f }
                : bazaarCrossing
                    ? BuildBazaarCrossingCriticalPassageWidths()
                    : new[] { 3.8f, 4.2f, 4.5f, 5.2f, 6.0f };
        CriticalPassageHeights = harborLocks
            ? new[] { 3.0f, 3.8f, 5.0f, 7.5f }
            : tideglassReactor
                ? new[] { 3.0f, 3.6f, 4.8f, 8.0f }
                : bazaarCrossing
                    ? BuildBazaarCrossingCriticalPassageHeights()
                    : new[] { 2.7f, 3.2f, 4.2f, 6.0f };
    }

    public float AttackToALength => PathLength(AttackToAPath);
    public float AttackToBLength => PathLength(AttackToBPath);
    public float RotationLength => PathLength(SiteRotationPath);
    public float SiteSeparation => SitePositions[0].DistanceTo(SitePositions[1]);
    public float SiteTravelDifferenceRatio
        => Mathf.Abs(AttackToALength - AttackToBLength) / Mathf.Max(AttackToALength, AttackToBLength);
    public bool HasBalancedSiteTravel => SiteTravelDifferenceRatio <= MaximumSiteTravelDifference;
    public bool HasThreeAttackRoutes => AttackToAPath.Count >= 5
        && AttackToBPath.Count >= 5
        && AttackMidPath.Count >= 4;
    public bool HasDenseCentralCover => CentralCoverBodyCount >= MinimumCentralCoverBodyCount;
    public bool HasPlayerClearance
        => AllAtLeast(CriticalPassageWidths, MinimumPassageWidth)
        && AllAtLeast(CriticalPassageHeights, MinimumPassageHeight);

    public bool HasCapsuleClearance(IReadOnlyList<Vector3> route, out string blockerName)
    {
        blockerName = "none";
        if (route.Count < 2)
        {
            blockerName = "missing_route";
            return false;
        }

        for (var segment = 1; segment < route.Count; segment++)
        {
            var start = route[segment - 1];
            var end = route[segment];
            if (TryFindCapsuleSegmentBlocker(start, end, out blockerName))
            {
                blockerName = $"{blockerName}@{segment}";
                return false;
            }
        }
        return true;
    }

    public bool HasCapsulePointClearance(Vector3 point, out string blockerName)
    {
        if (!IsInsideArena(point))
        {
            blockerName = "outside_arena";
            return false;
        }
        return !TryFindCapsuleSegmentBlocker(point, point, out blockerName);
    }

    public Vector3 SitePosition(int index) => SitePositions[Mathf.Clamp(index, 0, SitePositions.Count - 1)];

    public Vector3 StrategyTarget(string key)
    {
        if (MapId == DemolitionMapCatalog.HarborLocksId)
        {
            return HarborLocksStrategyTarget(key);
        }
        if (MapId == DemolitionMapCatalog.TideglassReactorId)
        {
            return TideglassReactorStrategyTarget(key);
        }
        if (MapId == DemolitionMapCatalog.BazaarCrossingId)
        {
            return BazaarCrossingStrategyTarget(key);
        }
        return key switch
        {
        "attack_entry_a" => World(new Vector3(-24.0f, 0.2f, 14.0f)),
        "attack_entry_b" => World(new Vector3(23.0f, 0.2f, -7.0f)),
        "attack_support_a" => World(new Vector3(-23.0f, 0.2f, 17.0f)),
        "attack_support_b" => World(new Vector3(24.0f, 0.2f, -8.0f)),
        "attack_mid_recon" => World(new Vector3(0.0f, 0.2f, 3.0f)),
        "defense_anchor_a" => World(new Vector3(-30.0f, 0.2f, 12.0f)),
        "defense_anchor_b" => World(new Vector3(31.0f, 0.2f, -33.0f)),
        "defense_mid" => World(new Vector3(0.0f, 0.2f, -8.0f)),
        "defense_rotate_a" => World(new Vector3(-12.0f, 0.2f, -16.0f)),
        "defense_rotate_b" => World(new Vector3(12.0f, 0.2f, -16.0f)),
        "retake_entry_a" => World(new Vector3(-24.0f, 0.2f, 8.0f)),
        "retake_entry_b" => World(new Vector3(24.0f, 0.2f, 0.0f)),
        "retake_cover_a" => World(new Vector3(-30.0f, 0.2f, 28.0f)),
        "retake_cover_b" => World(new Vector3(36.0f, 0.2f, -24.0f)),
        "retake_flank_a" => World(new Vector3(-24.0f, 0.2f, 14.0f)),
        "retake_flank_b" => World(new Vector3(26.0f, 0.2f, -16.0f)),
        "postplant_guard_a" => World(new Vector3(-30.0f, 0.2f, 18.0f)),
        "postplant_guard_b" => World(new Vector3(30.0f, 0.2f, -15.0f)),
        "postplant_crossfire_a" => World(new Vector3(-29.0f, 0.2f, 27.0f)),
        "postplant_crossfire_b" => World(new Vector3(30.0f, 0.2f, -27.0f)),
        "postplant_lurk_a" => World(new Vector3(-20.0f, 0.2f, 4.0f)),
        "postplant_lurk_b" => World(new Vector3(20.0f, 0.2f, 2.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
            _ => Midpoint
        };
    }

    public bool IsInsideArena(Vector3 worldPosition, float margin = 0.0f)
    {
        return worldPosition.X >= WorldBounds.Position.X - margin
            && worldPosition.X <= WorldBounds.End.X + margin
            && worldPosition.Z >= WorldBounds.Position.Y - margin
            && worldPosition.Z <= WorldBounds.End.Y + margin;
    }

    public bool HasSpawnSightlineToSite(int siteIndex)
    {
        var site = SitePosition(siteIndex);
        var direction = new Vector2(site.X - AttackSpawn.X, site.Z - AttackSpawn.Z);
        if (direction.LengthSquared() <= 0.01f)
        {
            return true;
        }
        foreach (var wall in CollisionBoxes)
        {
            var bazaarArchitecture = MapId == DemolitionMapCatalog.BazaarCrossingId
                && (wall.Name.StartsWith("Mass", StringComparison.Ordinal)
                    || wall.Name.StartsWith("Wall", StringComparison.Ordinal)
                    || wall.Name.StartsWith("Partition", StringComparison.Ordinal));
            if (!wall.Name.StartsWith("SightBlock", StringComparison.Ordinal)
                && !bazaarArchitecture)
            {
                continue;
            }
            var center = new Vector2(wall.Center.X, wall.Center.Z);
            var half = new Vector2(wall.Size.X, wall.Size.Z) * 0.5f;
            if (SegmentIntersectsRect(
                    new Vector2(AttackSpawn.X, AttackSpawn.Z),
                    new Vector2(site.X, site.Z),
                    new Rect2(center - half, half * 2.0f)))
            {
                return false;
            }
        }
        foreach (var prop in Props)
        {
            if (!prop.Name.StartsWith("SightBlock", StringComparison.Ordinal))
            {
                continue;
            }
            var basis = new Basis(Vector3.Up, prop.Yaw);
            var half = prop.CollisionSize * prop.Scale * 0.5f;
            var center3 = prop.Position + basis * (prop.CollisionOffset * prop.Scale);
            var extentX = Mathf.Abs(basis.X.X) * half.X + Mathf.Abs(basis.Z.X) * half.Z;
            var extentZ = Mathf.Abs(basis.X.Z) * half.X + Mathf.Abs(basis.Z.Z) * half.Z;
            var center = new Vector2(center3.X, center3.Z);
            var extent = new Vector2(extentX, extentZ);
            if (SegmentIntersectsRect(
                    new Vector2(AttackSpawn.X, AttackSpawn.Z),
                    new Vector2(site.X, site.Z),
                    new Rect2(center - extent, extent * 2.0f)))
            {
                return false;
            }
        }
        return true;
    }

    private IReadOnlyList<DemolitionArenaBox> BuildCollisionBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("ArenaFloor", new(0, -0.48f, 0), new(80, 1.0f, 112), "ground"),
            Box("NorthPerimeter", new(0, 2.5f, -55.5f), new(80, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeterLeft", new(-24, 2.5f, 55.5f), new(32, 5.0f, 1.0f), "concrete_dark"),
            Box("SouthPerimeterRight", new(24, 2.5f, 55.5f), new(32, 5.0f, 1.0f), "concrete_dark"),
            Box("WestPerimeter", new(-39.5f, 2.5f, 0), new(1.0f, 5.0f, 112), "concrete_dark"),
            Box("EastPerimeter", new(39.5f, 2.5f, 0), new(1.0f, 5.0f, 112), "concrete_dark"),

            Box("SightBlockLeft", new(-12.0f, 1.65f, 36.0f), new(1.4f, 3.3f, 17.0f), "rust"),
            Box("SightBlockRight", new(10.0f, 1.65f, 34.0f), new(1.4f, 3.3f, 17.0f), "rust"),
            Box("SpawnGateLeft", new(-14.4f, 1.6f, 52.0f), new(13.0f, 3.2f, 1.0f), "concrete_dark"),
            Box("SpawnGateRight", new(14.4f, 1.6f, 52.0f), new(13.0f, 3.2f, 1.0f), "concrete_dark"),

            Box("WestRouteWall", new(-17.0f, 1.7f, 24.0f), new(1.0f, 3.4f, 26.0f), "steel_dark"),
            Box("EastRouteWall", new(17.0f, 1.7f, -10.0f), new(1.0f, 3.4f, 26.0f), "steel_dark"),
            Box("MidDividerWest", new(-8.0f, 1.7f, 2.0f), new(12.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidDividerEast", new(8.0f, 1.7f, 2.0f), new(12.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidCrossNorth", new(0, 1.7f, -2.0f), new(4.0f, 3.4f, 1.0f), "steel_dark"),
            Box("MidPipeRackColumn", new(0, 2.9f, -1.0f), new(1.2f, 5.8f, 1.2f), "steel"),
            Box("MidFoundryCore", new(0, 2.25f, -13.5f), new(8.0f, 4.5f, 8.0f), "rust", visible: false),
            Box("MidCoverWestConverter", new(-6.8f, 1.35f, 17.5f), new(4.0f, 2.7f, 7.0f), "rust"),
            Box("MidCoverEastConverter", new(6.8f, 1.35f, 12.0f), new(4.0f, 2.7f, 6.5f), "steel"),
            Box("MidCoverAttackWest", new(-4.8f, 1.05f, 31.0f), new(2.6f, 2.1f, 4.2f), "steel"),
            Box("MidCoverAttackEast", new(4.6f, 1.05f, 27.0f), new(2.6f, 2.1f, 4.2f), "concrete_dark"),
            Box("MidCoverGantryWest", new(-14.0f, 2.6f, 22.0f), new(0.8f, 5.2f, 0.8f), "steel_dark"),
            Box("MidCoverGantryEast", new(14.0f, 2.6f, 22.0f), new(0.8f, 5.2f, 0.8f), "steel_dark"),
            Box("MidCoverDefenderWest", new(-7.0f, 1.15f, -31.0f), new(4.0f, 2.3f, 5.5f), "concrete_dark"),
            Box("MidCoverDefenderEast", new(7.0f, 1.15f, -36.0f), new(4.0f, 2.3f, 5.5f), "steel_dark"),
            Box("MidCoverRelayCore", new(23.0f, 2.7f, 22.0f), new(5.0f, 5.4f, 5.5f), "steel_dark", visible: false),
            Box("MidCoverRelayWing", new(18.8f, 1.25f, 22.0f), new(3.2f, 2.5f, 7.0f), "steel"),
            Box("MidCoverMaintenanceBay", new(-22.0f, 1.65f, -26.0f), new(7.0f, 3.3f, 8.0f), "concrete_dark", visible: false),
            Box("MidCoverMaintenanceVent", new(-17.0f, 1.0f, -29.0f), new(2.5f, 2.0f, 3.5f), "rust"),

            Box("FoundryNorthWall", new(-36.0f, 3.0f, -1.0f), new(7.0f, 6.0f, 1.0f), "concrete_dark", visible: false),
            Box("FoundrySouthWall", new(-36.0f, 3.0f, 43.0f), new(7.0f, 6.0f, 1.0f), "concrete_dark", visible: false),
            Box("FoundryWestWall", new(-38.5f, 3.0f, 21.0f), new(1.0f, 6.0f, 45.0f), "concrete_dark", visible: false),
            Box("SightBlockA1", new(-33.0f, 1.65f, 10.0f), new(12.0f, 3.3f, 1.0f), "rust"),
            Box("SightBlockA2", new(-33.0f, 1.65f, 33.0f), new(12.0f, 3.3f, 1.0f), "rust"),
            Box("FoundryFurnace", new(-35.5f, 2.2f, 14.0f), new(4.6f, 4.4f, 5.0f), "rust", visible: false),
            Box("FoundryMachine", new(-35.5f, 1.35f, 29.0f), new(4.5f, 2.7f, 6.5f), "steel", visible: false),

            Box("AssemblyEastWall", new(39.5f, 3.5f, -20.0f), new(1.0f, 7.0f, 20.0f), "steel_dark", visible: false),
            Box("AssemblyNorthWall", new(26.0f, 3.5f, -45.0f), new(26.0f, 7.0f, 1.0f), "steel_dark", visible: false),
            Box("AssemblySouthLeft", new(20.0f, 3.5f, -3.0f), new(5.0f, 7.0f, 1.0f), "steel_dark", visible: false),
            Box("AssemblySouthRight", new(31.0f, 3.5f, -3.0f), new(7.0f, 7.0f, 1.0f), "steel_dark", visible: false),
            Box("AssemblyRoof", new(31.0f, 7.0f, -20.0f), new(18.0f, 0.4f, 26.0f), "steel"),
            Box("AssemblyMachine", new(36.5f, 1.35f, -28.0f), new(4.5f, 2.7f, 6.5f), "steel", visible: false),
            Box("AssemblyPillarNorth", new(20.0f, 3.5f, -37.0f), new(3.0f, 7.0f, 3.0f), "concrete_dark"),
            Box("AssemblyPillarSouth", new(25.0f, 3.5f, -13.0f), new(3.0f, 7.0f, 3.0f), "concrete_dark"),

            Box("DefenderGateLeft", new(-11.0f, 1.65f, -47.0f), new(14.0f, 3.3f, 1.0f), "concrete_dark"),
            Box("DefenderGateRight", new(11.0f, 1.65f, -47.0f), new(14.0f, 3.3f, 1.0f), "concrete_dark")
        };
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaBox> BuildDetailBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            Box("AttackApron", new(0, 0.035f, 56), new(18, 0.07f, 9), "spawn_floor"),
            Box("AttackBorderNorth", new(0, 0.08f, 51.6f), new(18, 0.05f, 0.16f), "warning"),
            Box("AttackBorderSouth", new(0, 0.08f, 60.4f), new(18, 0.05f, 0.16f), "warning"),
            Box("AttackBorderLeft", new(-8.9f, 0.08f, 56), new(0.16f, 0.05f, 8.8f), "warning"),
            Box("AttackBorderRight", new(8.9f, 0.08f, 56), new(0.16f, 0.05f, 8.8f), "warning"),
            Box("AttackApproachSurface", new(0, 0.035f, 40), new(10, 0.07f, 18), "mid_floor"),
            Box("MidLaneSurface", new(0, 0.04f, 3.0f), new(6.2f, 0.08f, 20), "mid_floor"),
            Box("MidGuideLeft", new(-3.0f, 0.09f, 3.0f), new(0.12f, 0.04f, 19), "marking"),
            Box("MidGuideRight", new(3.0f, 0.09f, 3.0f), new(0.12f, 0.04f, 19), "marking"),
            Box("FoundryFloor", new(-33, 0.045f, 21), new(13, 0.09f, 40), "foundry_floor"),
            Box("FoundryCanopyNorth", new(-36.0f, 5.8f, -1.2f), new(7.2f, 0.3f, 3.6f), "rust"),
            Box("FoundryCanopySouth", new(-36.0f, 5.8f, 43.2f), new(7.2f, 0.3f, 3.6f), "rust"),
            Box("AssemblyFloor", new(30, 0.045f, -22), new(24, 0.09f, 40), "assembly_floor"),
            Box("AssemblyRoofStripe", new(31, 7.23f, -22), new(15, 0.07f, 2.0f), "warning"),
            Box("AssemblyWindowBand", new(39.54f, 4.4f, -20), new(0.05f, 1.4f, 15), "window"),
            Box("DefenderApron", new(0, 0.04f, -56), new(18, 0.08f, 7.5f), "spawn_floor"),
            Box("DefenderBorder", new(0, 0.09f, -52.3f), new(18, 0.04f, 0.16f), "cyan"),
            Box("DefenderApproachSurface", new(0, 0.035f, -40), new(10, 0.07f, 16), "mid_floor"),
            Box("MidPipeRackTop", new(0, 5.8f, -1), new(16, 0.35f, 1.2f), "steel"),
            Box("MidCoverGantryBeam", new(0, 5.25f, 22.0f), new(28.8f, 0.5f, 0.8f), "steel"),
            Box("MidCoverGantryStripe", new(0, 5.54f, 22.0f), new(27.0f, 0.08f, 0.9f), "warning"),
            Box("MidCoverAttackWestCap", new(-4.8f, 2.16f, 31.0f), new(2.9f, 0.12f, 4.5f), "steel_dark"),
            Box("MidCoverAttackWestStripe", new(-4.8f, 2.24f, 31.0f), new(2.0f, 0.04f, 0.22f), "warning"),
            Box("MidCoverAttackEastCap", new(4.6f, 2.16f, 27.0f), new(2.9f, 0.12f, 4.5f), "steel_dark"),
            Box("MidCoverAttackEastStripe", new(4.6f, 2.24f, 27.0f), new(2.0f, 0.04f, 0.22f), "cyan"),
            Box("MidCoverWestCap", new(-6.8f, 2.78f, 17.5f), new(4.4f, 0.16f, 7.4f), "steel_dark"),
            Box("MidCoverWestTopStripe", new(-6.8f, 2.88f, 17.5f), new(3.2f, 0.04f, 0.28f), "warning"),
            Box("MidCoverWestPanel", new(-6.8f, 1.45f, 21.03f), new(2.7f, 1.4f, 0.08f), "warning"),
            Box("MidCoverEastCap", new(6.8f, 2.78f, 12.0f), new(4.4f, 0.16f, 6.9f), "steel_dark"),
            Box("MidCoverEastTopStripe", new(6.8f, 2.88f, 12.0f), new(3.2f, 0.04f, 0.28f), "cyan"),
            Box("MidCoverEastPanel", new(6.8f, 1.45f, 15.28f), new(2.7f, 1.4f, 0.08f), "cyan"),
            Box("MidCoverDefenderWestCap", new(-7.0f, 2.36f, -31.0f), new(4.4f, 0.12f, 5.9f), "steel"),
            Box("MidCoverDefenderWestStripe", new(-7.0f, 2.44f, -31.0f), new(3.0f, 0.04f, 0.24f), "marking"),
            Box("MidCoverDefenderEastCap", new(7.0f, 2.36f, -36.0f), new(4.4f, 0.12f, 5.9f), "steel"),
            Box("MidCoverDefenderEastStripe", new(7.0f, 2.44f, -36.0f), new(3.0f, 0.04f, 0.24f), "cyan"),
            Box("MidCoverRelayCap", new(23.0f, 5.46f, 22.0f), new(5.4f, 0.12f, 5.9f), "steel"),
            Box("MidCoverRelayBeacon", new(23.0f, 5.58f, 22.0f), new(1.2f, 0.12f, 1.2f), "cyan"),
            Box("MidCoverRelayWingCap", new(18.8f, 2.56f, 22.0f), new(3.5f, 0.12f, 7.3f), "steel_dark"),
            Box("MidCoverRelayPanel", new(20.44f, 1.45f, 22.0f), new(0.08f, 1.5f, 4.8f), "cyan"),
            Box("MidCoverMaintenanceRoof", new(-22.0f, 3.36f, -26.0f), new(7.4f, 0.12f, 8.4f), "steel"),
            Box("MidCoverMaintenanceStripe", new(-22.0f, 3.44f, -26.0f), new(5.4f, 0.04f, 0.28f), "warning"),
            Box("MidCoverMaintenancePanel", new(-18.46f, 1.55f, -26.0f), new(0.08f, 1.6f, 4.8f), "warning"),
            Box("MidCoverMaintenanceVentCap", new(-17.0f, 2.06f, -29.0f), new(2.8f, 0.12f, 3.8f), "steel_dark"),
            Box("DefenderSignBeam", new(0, 4.5f, -47), new(8.0f, 0.3f, 0.5f), "warning")
        };
        for (var index = 0; index < 6; index++)
        {
            boxes.Add(Box($"WestLaneStripe_{index}", new(-24.0f, 0.04f, 4 - index * 7), new(0.18f, 0.08f, 3.2f), "warning"));
            boxes.Add(Box($"EastLaneStripe_{index}", new(24.0f, 0.04f, -2 - index * 7), new(0.18f, 0.08f, 3.2f), "warning"));
        }
        return boxes;
    }

    private IReadOnlyList<DemolitionArenaProp> BuildProps()
    {
        const string barrier = "res://assets/models/concrete_road_barrier/concrete_road_barrier.gltf";
        const string crate = "res://assets/models/old_military_crate/old_military_crate.gltf";
        var props = new List<DemolitionArenaProp>();
        var barrierPositions = new[]
        {
            new Vector3(-20.0f, 0.02f, 30.0f), new(-25.0f, 0.02f, 38.0f),
            new(7.0f, 0.02f, 44.0f), new(-8.0f, 0.02f, 48.0f),
            new(14.0f, 0.02f, -30.0f), new(35.0f, 0.02f, -8.0f),
            new(-14.0f, 0.02f, -14.0f), new(10.0f, 0.02f, -16.0f),
            new(-31.0f, 0.02f, 6.0f), new(28.0f, 0.02f, 14.0f),
            new(0.0f, 0.02f, -24.0f), new(-32.0f, 0.02f, -6.0f)
        };
        for (var index = 0; index < barrierPositions.Length; index++)
        {
            props.Add(new DemolitionArenaProp(
                $"ArenaBarrier_{index + 1:00}", barrier, World(barrierPositions[index]),
                index % 2 == 0 ? 0.12f : Mathf.Pi * 0.5f, 1.18f,
                new Vector3(1.55f, 0.84f, 0.64f), new Vector3(0, 0.41f, 0)));
        }
        var cratePositions = new[]
        {
            new Vector3(-36.0f, 0.02f, 24.0f), new(-30.0f, 0.02f, 31.0f),
            new(36.0f, 0.02f, -14.0f), new(24.0f, 0.02f, -40.0f),
            new(-7.5f, 0.02f, 8.0f), new(5.0f, 0.02f, -12.0f),
            new(13.5f, 0.02f, 31.0f), new(-17.0f, 0.02f, -24.0f),
            new(32.0f, 0.02f, 4.0f), new(-34.0f, 0.02f, 36.0f)
        };
        for (var index = 0; index < cratePositions.Length; index++)
        {
            props.Add(new DemolitionArenaProp(
                $"ArenaCrate_{index + 1:00}", crate, World(cratePositions[index]),
                index * 0.31f, 1.5f,
                new Vector3(0.82f, 0.42f, 0.68f), new Vector3(-0.06f, 0.21f, 0.1f)));
        }
        return props;
    }

    private IReadOnlyList<DemolitionArenaMarker> BuildMarkers()
    {
        return new[]
        {
            new DemolitionArenaMarker(AttackSpawn, "demolition_minimap_attack", "ATTACK", new Color(0.34f, 0.8f, 1.0f)),
            new DemolitionArenaMarker(Midpoint, "demolition_minimap_mid", "MID", new Color(0.88f, 0.82f, 0.56f)),
            new DemolitionArenaMarker(SitePositions[0], "demolition_minimap_a", "A", new Color(1.0f, 0.46f, 0.17f)),
            new DemolitionArenaMarker(SitePositions[1], "demolition_minimap_b", "B", new Color(1.0f, 0.46f, 0.17f)),
            new DemolitionArenaMarker(DefenderSpawn, "demolition_minimap_defend", "DEFEND", new Color(0.4f, 0.92f, 0.64f))
        };
    }

    private DemolitionArenaBox Box(
        string name,
        Vector3 center,
        Vector3 size,
        string material,
        Vector3 rotation = default,
        bool visible = true)
        => new(name, World(center), size, material, rotation, visible);

    private Vector3 World(Vector3 local) => Origin + local;

    private IReadOnlyList<Vector3> WorldPoints(params Vector3[] points)
    {
        var world = new Vector3[points.Length];
        for (var index = 0; index < points.Length; index++)
        {
            world[index] = World(points[index]);
        }
        return world;
    }

    private static float PathLength(IReadOnlyList<Vector3> points)
    {
        var length = 0.0f;
        for (var index = 1; index < points.Count; index++)
        {
            length += points[index - 1].DistanceTo(points[index]);
        }
        return length;
    }

    private bool TryFindCapsuleSegmentBlocker(Vector3 start, Vector3 end, out string blockerName)
    {
        const float radius = 0.38f;
        const float centerOffset = 0.9f;
        const float halfHeight = 0.875f;
        var capsuleBottom = Mathf.Min(start.Y, end.Y) + centerOffset - halfHeight;
        var capsuleTop = Mathf.Max(start.Y, end.Y) + centerOffset + halfHeight;
        foreach (var box in CollisionBoxes.Concat(NavigationBoxes))
        {
            var half = box.Size * 0.5f;
            if (box.Center.Y + half.Y < capsuleBottom || box.Center.Y - half.Y > capsuleTop)
            {
                continue;
            }
            var inverse = new Basis(Quaternion.FromEuler(box.Rotation)).Inverse();
            var localStart = inverse * (start - box.Center);
            var localEnd = inverse * (end - box.Center);
            var bounds = new Rect2(
                new Vector2(-half.X - radius, -half.Z - radius),
                new Vector2((half.X + radius) * 2.0f, (half.Z + radius) * 2.0f));
            if (SegmentIntersectsRect(
                    new Vector2(localStart.X, localStart.Z),
                    new Vector2(localEnd.X, localEnd.Z),
                    bounds))
            {
                blockerName = box.Name;
                return true;
            }
        }

        foreach (var prop in Props)
        {
            var yaw = new Basis(Vector3.Up, prop.Yaw);
            var half = prop.CollisionSize * prop.Scale * 0.5f;
            var center = prop.Position + yaw * (prop.CollisionOffset * prop.Scale);
            if (center.Y + half.Y < capsuleBottom || center.Y - half.Y > capsuleTop)
            {
                continue;
            }
            var inverse = yaw.Inverse();
            var localStart = inverse * (start - center);
            var localEnd = inverse * (end - center);
            var bounds = new Rect2(
                new Vector2(-half.X - radius, -half.Z - radius),
                new Vector2((half.X + radius) * 2.0f, (half.Z + radius) * 2.0f));
            if (SegmentIntersectsRect(
                    new Vector2(localStart.X, localStart.Z),
                    new Vector2(localEnd.X, localEnd.Z),
                    bounds))
            {
                blockerName = prop.Name;
                return true;
            }
        }

        blockerName = "none";
        return false;
    }

    private static bool AllAtLeast(IReadOnlyList<float> values, float minimum)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] < minimum)
            {
                return false;
            }
        }
        return true;
    }

    private static bool GroundFootprintsOverlap(DemolitionArenaBox box, DemolitionArenaProp prop)
    {
        const float separationMargin = 0.05f;
        var boxBasis = new Basis(Quaternion.FromEuler(box.Rotation));
        var boxHalf = box.Size * 0.5f;
        var boxExtentX = Mathf.Abs(boxBasis.X.X) * boxHalf.X + Mathf.Abs(boxBasis.Z.X) * boxHalf.Z;
        var boxExtentZ = Mathf.Abs(boxBasis.X.Z) * boxHalf.X + Mathf.Abs(boxBasis.Z.Z) * boxHalf.Z;

        var propBasis = new Basis(Vector3.Up, prop.Yaw);
        var propHalf = prop.CollisionSize * prop.Scale * 0.5f;
        var propCenter = prop.Position + propBasis * (prop.CollisionOffset * prop.Scale);
        var propExtentX = Mathf.Abs(propBasis.X.X) * propHalf.X + Mathf.Abs(propBasis.Z.X) * propHalf.Z;
        var propExtentZ = Mathf.Abs(propBasis.X.Z) * propHalf.X + Mathf.Abs(propBasis.Z.Z) * propHalf.Z;
        return Mathf.Abs(box.Center.X - propCenter.X) < boxExtentX + propExtentX + separationMargin
            && Mathf.Abs(box.Center.Z - propCenter.Z) < boxExtentZ + propExtentZ + separationMargin;
    }

    private static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Rect2 rect)
    {
        var direction = end - start;
        var minimum = 0.0f;
        var maximum = 1.0f;
        return Clip(-direction.X, start.X - rect.Position.X, ref minimum, ref maximum)
            && Clip(direction.X, rect.End.X - start.X, ref minimum, ref maximum)
            && Clip(-direction.Y, start.Y - rect.Position.Y, ref minimum, ref maximum)
            && Clip(direction.Y, rect.End.Y - start.Y, ref minimum, ref maximum);
    }

    private static bool Clip(float denominator, float numerator, ref float minimum, ref float maximum)
    {
        if (Mathf.IsZeroApprox(denominator))
        {
            return numerator >= 0.0f;
        }
        var value = numerator / denominator;
        if (denominator < 0.0f)
        {
            if (value > maximum)
            {
                return false;
            }
            minimum = Mathf.Max(minimum, value);
        }
        else
        {
            if (value < minimum)
            {
                return false;
            }
            maximum = Mathf.Min(maximum, value);
        }
        return true;
    }
}
