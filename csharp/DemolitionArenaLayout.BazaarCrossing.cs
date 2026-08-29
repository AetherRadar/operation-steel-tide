using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    private const float BazaarGroundRouteHeight = 0.2f;
    private const float BazaarGalleryRouteHeight = 3.2f;
    private const float BazaarBalconyRouteHeight = 2.8f;

    private IReadOnlyList<Vector2> BuildBazaarCrossingLocalSiteCoordinates()
        => Array.AsReadOnly(new[] { new Vector2(-43.0f, -22.0f), new Vector2(43.0f, -22.0f) });

    private IReadOnlyList<Vector3> BuildBazaarCrossingSitePositions() => WorldPoints(
        new(-43.0f, 0.18f, -22.0f),
        new(43.0f, 0.18f, -22.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackSpawns() => WorldPoints(
        new(-4.0f, 0.22f, 50.0f),
        new(4.0f, 0.22f, 50.0f),
        new(-7.0f, 0.22f, 47.0f),
        new(7.0f, 0.22f, 47.0f),
        new(0.0f, 0.22f, 48.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingDefenderSpawns() => WorldPoints(
        new(-4.0f, 0.22f, -50.0f),
        new(4.0f, 0.22f, -50.0f),
        new(-7.0f, 0.22f, -47.0f),
        new(7.0f, 0.22f, -47.0f),
        new(0.0f, 0.22f, -48.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingCoverPoints() => WorldPoints(
        new(-47.0f, 0.2f, 30.0f), new(-49.0f, 0.2f, 17.0f),
        new(-46.0f, 0.2f, 6.0f), new(-48.0f, 0.2f, -8.0f),
        new(-46.0f, 0.2f, -18.0f), new(-39.0f, 0.2f, -24.0f),
        new(47.0f, 0.2f, 30.0f), new(49.0f, 0.2f, 18.0f),
        new(46.0f, 0.2f, 7.0f), new(49.0f, 0.2f, -7.0f),
        new(46.0f, 0.2f, -17.0f), new(39.0f, 0.2f, -24.0f),
        new(-5.0f, 0.2f, 27.0f), new(-4.0f, 0.2f, 17.0f),
        new(4.0f, 0.2f, 9.0f), new(-2.0f, 0.2f, 3.0f),
        new(-10.0f, 0.2f, -8.0f), new(10.0f, 0.2f, -8.0f),
        new(-37.0f, 0.2f, -31.0f), new(37.0f, 0.2f, -31.0f),
        new(-59.0f, BazaarGalleryRouteHeight, -13.0f),
        new(-56.0f, BazaarGalleryRouteHeight, -21.0f),
        new(-53.0f, BazaarGalleryRouteHeight, -27.0f),
        new(-10.0f, BazaarGalleryRouteHeight, 0.0f),
        new(0.0f, BazaarGalleryRouteHeight, 0.0f),
        new(10.0f, BazaarGalleryRouteHeight, 0.0f),
        new(59.0f, BazaarBalconyRouteHeight, -16.0f),
        new(56.0f, BazaarBalconyRouteHeight, -22.0f),
        new(53.0f, BazaarBalconyRouteHeight, -28.0f));

    private IReadOnlyList<DemolitionArenaBox> BuildBazaarCrossingCollisionBoxes()
    {
        return new[]
        {
            BazaarCollisionBox("ArenaFloor", new(0.0f, -0.48f, 0.0f), new(136.0f, 1.0f, 112.0f)),
            BazaarCollisionBox("NorthPerimeter", new(0.0f, 3.0f, -55.5f), new(136.0f, 6.0f, 1.0f)),
            BazaarCollisionBox("SouthPerimeter", new(0.0f, 3.0f, 55.5f), new(136.0f, 6.0f, 1.0f)),
            BazaarCollisionBox("WestPerimeter", new(-67.5f, 3.0f, 0.0f), new(1.0f, 6.0f, 112.0f)),
            BazaarCollisionBox("EastPerimeter", new(67.5f, 3.0f, 0.0f), new(1.0f, 6.0f, 112.0f)),

            // South market blocks force both long lanes to turn before reaching a site.
            BazaarCollisionBox("SightBlockAttackWest", new(-18.0f, 3.2f, 31.0f), new(18.0f, 6.4f, 12.0f)),
            BazaarCollisionBox("SightBlockAttackEast", new(18.0f, 3.2f, 31.0f), new(18.0f, 6.4f, 12.0f)),

            // Four old-city blocks define the central lane while leaving a clear cross-axis
            // for both approaches to the elevated mid bridge.
            BazaarCollisionBox("MidCoverWestSouthBlock", new(-24.0f, 3.5f, 10.0f), new(20.0f, 7.0f, 12.0f)),
            BazaarCollisionBox("MidCoverWestNorthBlock", new(-25.0f, 3.5f, -10.0f), new(18.0f, 7.0f, 12.0f)),
            BazaarCollisionBox("MidCoverEastSouthBlock", new(25.0f, 3.5f, 10.0f), new(18.0f, 7.0f, 12.0f)),
            BazaarCollisionBox("MidCoverEastNorthBlock", new(24.0f, 3.5f, -10.0f), new(20.0f, 7.0f, 12.0f)),

            // Defender arcades hide both sites from the north spawn but preserve the
            // outer back-market rotation around their northern edges.
            BazaarCollisionBox("SightBlockDefenderWest", new(-22.0f, 3.4f, -37.0f), new(18.0f, 6.8f, 8.0f)),
            BazaarCollisionBox("SightBlockDefenderEast", new(22.0f, 3.4f, -37.0f), new(18.0f, 6.8f, 8.0f)),

            // Alternating mid obstructions create information slices instead of a single
            // spawn-to-spawn sniper lane.
            BazaarCollisionBox("MidCoverSouthKink", new(6.5f, 2.8f, 23.0f), new(7.0f, 5.6f, 9.0f)),
            BazaarCollisionBox("MidCoverMarketKink", new(-6.5f, 2.8f, 10.0f), new(7.0f, 5.6f, 7.0f)),
            BazaarCollisionBox("SightBlockMidNorth", new(0.0f, 3.2f, -16.0f), new(12.0f, 6.4f, 7.0f)),
            BazaarCollisionBox("SightBlockSitePair", new(0.0f, 3.2f, -22.0f), new(10.0f, 6.4f, 4.0f)),
            BazaarCollisionBox("MidCoverWestMarketCart", new(-12.0f, 1.0f, 20.0f), new(2.6f, 2.0f, 2.2f)),
            BazaarCollisionBox("MidCoverEastMarketCart", new(12.0f, 1.0f, 16.0f), new(2.6f, 2.0f, 2.2f)),

            // Site-edge cover leaves every bomb plate on the ground and outside the
            // elevated gallery footprints.
            BazaarCollisionBox("SiteCoverAWest", new(-49.0f, 1.15f, -18.0f), new(2.4f, 2.3f, 3.2f)),
            BazaarCollisionBox("SiteCoverAEast", new(-37.5f, 1.15f, -27.5f), new(3.0f, 2.3f, 2.4f)),
            BazaarCollisionBox("SiteCoverBEast", new(49.0f, 1.15f, -18.0f), new(2.4f, 2.3f, 3.2f)),
            BazaarCollisionBox("SiteCoverBWest", new(37.5f, 1.15f, -27.5f), new(3.0f, 2.3f, 2.4f)),

            // Low parapet volumes line up with authored high-level stalls without
            // obstructing either complete two-way elevated traversal path.
            BazaarCollisionBox("GalleryCoverWest", new(-62.0f, 3.85f, -20.0f), new(1.5f, 1.7f, 4.0f)),
            BazaarCollisionBox("GalleryCoverNorth", new(-56.0f, 3.85f, -29.0f), new(3.0f, 1.7f, 1.4f)),
            BazaarCollisionBox("BalconyCoverEast", new(62.0f, 3.45f, -21.0f), new(1.5f, 1.7f, 4.0f)),
            BazaarCollisionBox("BalconyCoverNorth", new(56.0f, 3.45f, -30.0f), new(3.0f, 1.7f, 1.4f)),

            // Rail collision follows the authored railings and leaves a full 3.2m gap at
            // every stair landing. Mid rails are high enough for the ground lane below.
            BazaarCollisionBox("GalleryRailWest", new(-62.85f, 3.55f, -20.0f), new(0.3f, 1.1f, 20.0f)),
            BazaarCollisionBox("GalleryRailNorth", new(-57.0f, 3.55f, -29.85f), new(12.0f, 1.1f, 0.3f)),
            BazaarCollisionBox("GalleryRailSouthWest", new(-61.8f, 3.55f, -10.15f), new(2.4f, 1.1f, 0.3f)),
            BazaarCollisionBox("GalleryRailSouthEast", new(-54.2f, 3.55f, -10.15f), new(6.4f, 1.1f, 0.3f)),
            BazaarCollisionBox("GalleryRailEastNorth", new(-51.15f, 3.55f, -29.3f), new(0.3f, 1.1f, 1.4f)),
            BazaarCollisionBox("GalleryRailEastSouth", new(-51.15f, 3.55f, -17.7f), new(0.3f, 1.1f, 15.4f)),
            BazaarCollisionBox("MidBridgeRailNorth", new(0.0f, 3.55f, -1.65f), new(26.0f, 1.1f, 0.3f)),
            BazaarCollisionBox("MidBridgeRailSouth", new(0.0f, 3.55f, 1.65f), new(26.0f, 1.1f, 0.3f)),
            BazaarCollisionBox("BalconyRailEast", new(62.85f, 3.15f, -22.0f), new(0.3f, 1.1f, 18.0f)),
            BazaarCollisionBox("BalconyRailNorth", new(57.0f, 3.15f, -30.85f), new(12.0f, 1.1f, 0.3f)),
            BazaarCollisionBox("BalconyRailSouthWest", new(54.2f, 3.15f, -13.15f), new(6.4f, 1.1f, 0.3f)),
            BazaarCollisionBox("BalconyRailSouthEast", new(61.8f, 3.15f, -13.15f), new(2.4f, 1.1f, 0.3f)),
            BazaarCollisionBox("BalconyRailWestNorth", new(51.15f, 3.15f, -29.8f), new(0.3f, 1.1f, 2.4f)),
            BazaarCollisionBox("BalconyRailWestSouth", new(51.15f, 3.15f, -19.2f), new(0.3f, 1.1f, 12.4f))
        };
    }

    private IReadOnlyList<DemolitionArenaBox> BuildBazaarCrossingTraversalBoxes()
    {
        const float galleryAngle = 0.299366f;
        const float balconyAngle = 0.302885f;
        return new[]
        {
            BazaarCollisionBox("TraversalAGalleryDeck", new(-57.0f, 2.84f, -20.0f), new(12.0f, 0.32f, 20.0f)),
            BazaarCollisionBox("TraversalAGallerySouthRamp", new(-59.0f, 1.395f, -5.14f), new(3.2f, 0.22f, 10.1724f), new(galleryAngle, 0.0f, 0.0f)),
            BazaarCollisionBox("TraversalAGalleryEastRamp", new(-46.14f, 1.395f, -27.0f), new(10.1724f, 0.22f, 3.2f), new(0.0f, 0.0f, -galleryAngle)),

            BazaarCollisionBox("TraversalMidBridgeDeck", new(0.0f, 2.85f, 0.0f), new(26.0f, 0.3f, 3.6f)),
            BazaarCollisionBox("TraversalMidBridgeWestRamp", new(-17.86f, 1.395f, 0.0f), new(10.1724f, 0.22f, 3.2f), new(0.0f, 0.0f, galleryAngle)),
            BazaarCollisionBox("TraversalMidBridgeEastRamp", new(17.86f, 1.395f, 0.0f), new(10.1724f, 0.22f, 3.2f), new(0.0f, 0.0f, -galleryAngle)),

            BazaarCollisionBox("TraversalBBalconyDeck", new(57.0f, 2.45f, -22.0f), new(12.0f, 0.3f, 18.0f)),
            BazaarCollisionBox("TraversalBBalconySouthRamp", new(59.0f, 1.195f, -8.84f), new(3.2f, 0.22f, 8.7168f), new(balconyAngle, 0.0f, 0.0f)),
            BazaarCollisionBox("TraversalBBalconyWestRamp", new(46.84f, 1.195f, -27.0f), new(8.7168f, 0.22f, 3.2f), new(0.0f, 0.0f, balconyAngle))
        };
    }

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackToAPath() => WorldPoints(
        new(0.0f, 0.2f, 49.0f), new(-18.0f, 0.2f, 42.0f),
        new(-38.0f, 0.2f, 38.0f), new(-47.0f, 0.2f, 30.0f),
        new(-49.0f, 0.2f, 19.0f), new(-46.0f, 0.2f, 9.0f),
        new(-50.0f, 0.2f, 1.0f), new(-49.0f, 0.2f, -9.0f),
        new(-45.0f, 0.2f, -14.0f), new(-43.0f, 0.2f, -22.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackToBPath() => WorldPoints(
        new(0.0f, 0.2f, 49.0f), new(18.0f, 0.2f, 42.0f),
        new(38.0f, 0.2f, 38.0f), new(47.0f, 0.2f, 31.0f),
        new(49.0f, 0.2f, 21.0f), new(46.0f, 0.2f, 13.0f),
        new(50.0f, 0.2f, 5.0f), new(48.0f, 0.2f, -5.0f),
        new(45.0f, 0.2f, -13.0f), new(43.0f, 0.2f, -22.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackMidPath() => WorldPoints(
        new(0.0f, 0.2f, 49.0f), new(0.0f, 0.2f, 40.0f),
        new(0.0f, 0.2f, 32.0f), new(-4.0f, 0.2f, 27.0f),
        new(-4.0f, 0.2f, 18.0f), new(1.0f, 0.2f, 14.5f),
        new(4.0f, 0.2f, 9.0f), new(1.0f, 0.2f, 5.0f),
        new(0.0f, 0.2f, -8.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingDefenderToAPath() => WorldPoints(
        new(0.0f, 0.2f, -49.0f), new(-12.0f, 0.2f, -46.0f),
        new(-31.0f, 0.2f, -45.0f), new(-40.0f, 0.2f, -40.0f),
        new(-39.0f, 0.2f, -34.0f), new(-34.0f, 0.2f, -31.0f),
        new(-33.0f, 0.2f, -26.0f), new(-35.0f, 0.2f, -22.0f),
        new(-43.0f, 0.2f, -22.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingDefenderToBPath() => WorldPoints(
        new(0.0f, 0.2f, -49.0f), new(12.0f, 0.2f, -46.0f),
        new(31.0f, 0.2f, -45.0f), new(40.0f, 0.2f, -40.0f),
        new(39.0f, 0.2f, -34.0f), new(34.0f, 0.2f, -31.0f),
        new(33.0f, 0.2f, -26.0f), new(35.0f, 0.2f, -22.0f),
        new(43.0f, 0.2f, -22.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingSiteRotationPath() => WorldPoints(
        new(-43.0f, 0.2f, -22.0f), new(-35.0f, 0.2f, -20.0f),
        new(-27.0f, 0.2f, -18.0f), new(-18.0f, 0.2f, -18.0f),
        new(-12.0f, 0.2f, -18.0f), new(-9.0f, 0.2f, -9.0f),
        new(-8.0f, 0.2f, -4.0f), new(0.0f, 0.2f, -4.0f),
        new(8.0f, 0.2f, -4.0f), new(9.0f, 0.2f, -9.0f),
        new(12.0f, 0.2f, -18.0f), new(18.0f, 0.2f, -18.0f),
        new(27.0f, 0.2f, -18.0f), new(35.0f, 0.2f, -20.0f),
        new(43.0f, 0.2f, -22.0f));

    private IReadOnlyList<IReadOnlyList<Vector3>> BuildBazaarCrossingAuxiliaryPaths()
    {
        return new IReadOnlyList<Vector3>[]
        {
            BuildBazaarNorthBackMarketPath(),
            BuildBazaarAGalleryPath(),
            BuildBazaarMidBridgePath(),
            BuildBazaarBBalconyPath()
        };
    }

    private IReadOnlyList<Vector3> BuildBazaarNorthBackMarketPath() => WorldPoints(
        new(-43.0f, 0.2f, -22.0f), new(-35.0f, 0.2f, -25.0f),
        new(-36.0f, 0.2f, -32.0f), new(-42.0f, 0.2f, -40.0f),
        new(-31.0f, 0.2f, -47.0f), new(0.0f, 0.2f, -49.0f),
        new(31.0f, 0.2f, -47.0f), new(42.0f, 0.2f, -40.0f),
        new(36.0f, 0.2f, -32.0f), new(35.0f, 0.2f, -25.0f),
        new(43.0f, 0.2f, -22.0f));

    private IReadOnlyList<Vector3> BuildBazaarAGalleryPath()
    {
        var points = new List<Vector3>();
        AddBazaarSlopePoints(points, new(-59.0f, 0.2f, -0.28f), new(-59.0f, 3.2f, -10.0f), 6);
        points.Add(new(-59.0f, 3.2f, -17.0f));
        points.Add(new(-57.0f, 3.2f, -23.0f));
        points.Add(new(-53.0f, 3.2f, -27.0f));
        AddBazaarSlopePoints(points, new(-51.0f, 3.2f, -27.0f), new(-41.28f, 0.2f, -27.0f), 6);
        return BazaarWorldPoints(points);
    }

    private IReadOnlyList<Vector3> BuildBazaarMidBridgePath()
    {
        var points = new List<Vector3>();
        AddBazaarSlopePoints(points, new(-22.72f, 0.2f, 0.0f), new(-13.0f, 3.2f, 0.0f), 6);
        points.Add(new(-6.0f, 3.2f, 0.0f));
        points.Add(new(0.0f, 3.2f, 0.0f));
        points.Add(new(6.0f, 3.2f, 0.0f));
        AddBazaarSlopePoints(points, new(13.0f, 3.2f, 0.0f), new(22.72f, 0.2f, 0.0f), 6);
        return BazaarWorldPoints(points);
    }

    private IReadOnlyList<Vector3> BuildBazaarBBalconyPath()
    {
        var points = new List<Vector3>();
        AddBazaarSlopePoints(points, new(59.0f, 0.2f, -4.68f), new(59.0f, 2.8f, -13.0f), 6);
        points.Add(new(59.0f, 2.8f, -18.0f));
        points.Add(new(57.0f, 2.8f, -23.0f));
        points.Add(new(53.0f, 2.8f, -27.0f));
        AddBazaarSlopePoints(points, new(51.0f, 2.8f, -27.0f), new(42.68f, 0.2f, -27.0f), 6);
        return BazaarWorldPoints(points);
    }

    private static void AddBazaarSlopePoints(
        List<Vector3> points,
        Vector3 start,
        Vector3 end,
        int segments)
    {
        for (var index = 0; index <= segments; index++)
        {
            points.Add(start.Lerp(end, index / (float)segments));
        }
    }

    private IReadOnlyList<Vector3> BazaarWorldPoints(IReadOnlyList<Vector3> localPoints)
    {
        var points = new Vector3[localPoints.Count];
        for (var index = 0; index < localPoints.Count; index++)
        {
            points[index] = World(localPoints[index]);
        }
        return points;
    }

    private DemolitionArenaBox BazaarCollisionBox(
        string name,
        Vector3 localCenter,
        Vector3 size,
        Vector3 rotation = default)
        => Box(name, localCenter, size, "concrete_dark", rotation, visible: false);

    private Vector3 BazaarCrossingStrategyTarget(string key) => key switch
    {
        "attack_entry_a" => World(new Vector3(-46.0f, BazaarGroundRouteHeight, -14.0f)),
        "attack_entry_b" => World(new Vector3(45.0f, BazaarGroundRouteHeight, -13.0f)),
        "attack_support_a" => World(new Vector3(-50.0f, BazaarGroundRouteHeight, -9.0f)),
        "attack_support_b" => World(new Vector3(48.0f, BazaarGroundRouteHeight, -7.0f)),
        "attack_mid_recon" => World(new Vector3(0.0f, BazaarGroundRouteHeight, -8.0f)),
        "defense_anchor_a" => World(new Vector3(-39.0f, BazaarGroundRouteHeight, -24.0f)),
        "defense_anchor_b" => World(new Vector3(39.0f, BazaarGroundRouteHeight, -24.0f)),
        "defense_mid" => World(new Vector3(0.0f, BazaarGalleryRouteHeight, 0.0f)),
        "defense_rotate_a" => World(new Vector3(-18.0f, BazaarGroundRouteHeight, -18.0f)),
        "defense_rotate_b" => World(new Vector3(18.0f, BazaarGroundRouteHeight, -18.0f)),
        "retake_entry_a" => World(new Vector3(-35.0f, BazaarGroundRouteHeight, -20.0f)),
        "retake_entry_b" => World(new Vector3(35.0f, BazaarGroundRouteHeight, -20.0f)),
        "retake_cover_a" => World(new Vector3(-55.0f, BazaarGalleryRouteHeight, -26.0f)),
        "retake_cover_b" => World(new Vector3(55.0f, BazaarBalconyRouteHeight, -26.0f)),
        "retake_flank_a" => World(new Vector3(-49.0f, BazaarGroundRouteHeight, -8.0f)),
        "retake_flank_b" => World(new Vector3(49.0f, BazaarGroundRouteHeight, -7.0f)),
        "postplant_guard_a" => World(new Vector3(-56.0f, BazaarGalleryRouteHeight, -21.0f)),
        "postplant_guard_b" => World(new Vector3(56.0f, BazaarBalconyRouteHeight, -22.0f)),
        "postplant_crossfire_a" => World(new Vector3(-34.0f, BazaarGroundRouteHeight, -27.0f)),
        "postplant_crossfire_b" => World(new Vector3(34.0f, BazaarGroundRouteHeight, -27.0f)),
        "postplant_lurk_a" => World(new Vector3(-10.0f, BazaarGroundRouteHeight, -8.0f)),
        "postplant_lurk_b" => World(new Vector3(10.0f, BazaarGroundRouteHeight, -8.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
        _ => Midpoint
    };
}
