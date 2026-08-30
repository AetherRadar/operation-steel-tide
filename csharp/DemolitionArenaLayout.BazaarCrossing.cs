using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    private const float BazaarGroundRouteHeight = 0.2f;
    private const float BazaarGallerySurfaceHeight = 3.6f;
    private const float BazaarGalleryRouteHeight = 3.8f;
    private const float BazaarBalconySurfaceHeight = 3.4f;
    private const float BazaarBalconyRouteHeight = 3.6f;
    private const float BazaarMezzanineSurfaceHeight = 3.2f;
    private const float BazaarMezzanineRouteHeight = 3.4f;
    private const float BazaarWallThickness = 0.42f;
    private const float BazaarGuardRailThickness = 0.28f;
    private const float BazaarGuardRailHeight = 1.10f;
    private const float BazaarStairGuardRailOffset = 1.66f;

    private readonly record struct BazaarOpening(float Center, float Width);

    internal readonly record struct BazaarThreshold(
        string Name,
        string Site,
        Vector3 Center,
        Vector3 Normal,
        float Width,
        bool StairTransition);

    private readonly record struct BazaarWallScan(
        string Name,
        string Site,
        string Prefix,
        bool Horizontal,
        float FixedCoordinate,
        float Minimum,
        float Maximum,
        Vector3 Normal);

    private readonly record struct BazaarInterval(float Minimum, float Maximum);

    private IReadOnlyList<Vector2> BuildBazaarCrossingLocalSiteCoordinates()
        => Array.AsReadOnly(new[] { new Vector2(-46.0f, -18.0f), new Vector2(46.0f, -18.0f) });

    private IReadOnlyList<Vector3> BuildBazaarCrossingSitePositions() => WorldPoints(
        new(-46.0f, 0.18f, -18.0f),
        new(46.0f, 0.18f, -18.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackSpawns() => WorldPoints(
        new(-4.0f, 0.22f, 50.0f),
        new(4.0f, 0.22f, 50.0f),
        new(-7.0f, 0.22f, 47.0f),
        new(7.0f, 0.22f, 47.0f),
        new(0.0f, 0.22f, 48.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingDefenderSpawns() => WorldPoints(
        new(-4.0f, 0.22f, -50.0f),
        new(4.0f, 0.22f, -50.0f),
        new(-6.0f, 0.22f, -47.0f),
        new(6.0f, 0.22f, -47.0f),
        new(0.0f, 0.22f, -49.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingCoverPoints() => WorldPoints(
        new(-12.5f, 0.2f, 38.5f), new(-12.5f, 0.2f, 20.0f),
        new(-31.0f, 0.2f, 9.0f), new(-47.0f, 0.2f, 1.0f),
        new(-47.0f, 0.2f, -10.0f), new(-45.0f, 0.2f, -16.0f),
        new(-43.0f, 0.2f, -18.0f), new(-55.5f, 0.2f, -26.5f),
        new(12.5f, 0.2f, 38.5f), new(12.5f, 0.2f, 20.0f),
        new(31.0f, 0.2f, 9.0f), new(46.0f, 0.2f, 0.0f),
        new(46.0f, 0.2f, -10.0f), new(43.0f, 0.2f, -18.0f),
        new(49.0f, 0.2f, -18.0f), new(56.0f, 0.2f, -25.0f),
        new(55.5f, 0.2f, 4.0f),
        new(-5.0f, 0.2f, 30.0f), new(4.0f, 0.2f, 19.0f),
        new(-1.0f, 0.2f, 6.0f), new(-5.0f, 0.2f, -7.0f),
        new(-6.0f, 0.2f, -17.0f), new(4.0f, 0.2f, -22.0f),
        new(-44.4f, 0.2f, -37.5f),
        new(-17.0f, 0.2f, -47.0f),
        new(17.0f, 0.2f, -47.0f), new(36.0f, 0.2f, -37.5f),
        new(-56.0f, BazaarGalleryRouteHeight, -18.0f),
        new(-56.0f, BazaarGalleryRouteHeight, -25.0f),
        new(-6.0f, BazaarMezzanineRouteHeight, 21.0f),
        new(-6.0f, BazaarMezzanineRouteHeight, 28.0f),
        new(56.0f, BazaarBalconyRouteHeight, -17.0f),
        new(56.0f, BazaarBalconyRouteHeight, -24.0f));

    private IReadOnlyList<DemolitionArenaBox> BuildBazaarCrossingCollisionBoxes()
    {
        var boxes = new List<DemolitionArenaBox>
        {
            BazaarCollisionBox("ArenaFloor", new(0.0f, -0.48f, 0.0f), new(136.0f, 1.0f, 112.0f)),
            BazaarCollisionBox("PerimeterNorth", new(0.0f, 3.0f, -55.6f), new(136.0f, 6.0f, 0.8f)),
            BazaarCollisionBox("PerimeterSouth", new(0.0f, 3.0f, 55.6f), new(136.0f, 6.0f, 0.8f)),
            BazaarCollisionBox("MassBoundaryWest", new(-65.5f, 4.0f, 0.0f), new(3.0f, 8.0f, 112.0f)),
            BazaarCollisionBox("MassBoundaryEast", new(65.5f, 4.0f, 0.0f), new(3.0f, 8.0f, 112.0f)),

            // South city masses split the attack foyer into three immediate commitments.
            BazaarCollisionBox("MassAttackWest", new(-36.5f, 4.0f, 48.5f), new(43.0f, 8.0f, 13.0f)),
            BazaarCollisionBox("MassAttackEast", new(36.5f, 4.0f, 48.5f), new(43.0f, 8.0f, 13.0f)),
            BazaarCollisionBox("MassAttackWestEntryWing", new(-12.0f, 4.0f, 48.25f), new(6.0f, 8.0f, 13.5f)),
            BazaarCollisionBox("MassAttackEastEntryWing", new(12.0f, 4.0f, 48.25f), new(6.0f, 8.0f, 13.5f)),
            BazaarCollisionBox("MassSouthWest", new(-37.0f, 4.2f, 24.25f), new(42.0f, 8.4f, 24.5f)),
            BazaarCollisionBox("MassSouthEast", new(37.0f, 4.2f, 24.25f), new(42.0f, 8.4f, 24.5f)),
            BazaarCollisionBox("MassSouthWestLaneClosure", new(-45.5f, 4.2f, 39.25f), new(7.0f, 8.4f, 5.5f)),
            BazaarCollisionBox("MassSouthEastLaneClosure", new(45.5f, 4.2f, 39.25f), new(7.0f, 8.4f, 5.5f)),
            BazaarCollisionBox("MassWestServiceClosure", new(-62.0f, 4.0f, 4.0f), new(4.0f, 8.0f, 16.0f)),
            BazaarCollisionBox("MassEastServiceClosure", new(62.0f, 4.0f, 4.0f), new(4.0f, 8.0f, 16.0f)),
            BazaarCollisionBox("WallWestApproachSightReturn", new(-49.0f, 4.0f, 4.0f), new(0.42f, 8.0f, 16.0f)),
            BazaarCollisionBox("WallEastServicePocketClosure", new(56.0f, 4.0f, 9.4f), new(8.0f, 8.0f, 0.42f)),

            // Offset gaps through these blocks are the earned Mid-to-site splits.
            BazaarCollisionBox("MassSeparationWestNorth", new(-19.25f, 3.9f, -26.0f), new(19.5f, 7.8f, 10.0f)),
            BazaarCollisionBox("MassSeparationWestSouth", new(-19.25f, 3.7f, -4.5f), new(19.5f, 7.4f, 21.0f)),
            BazaarCollisionBox("MassSeparationEastNorth", new(19.25f, 4.0f, -24.5f), new(19.5f, 8.0f, 13.0f)),
            BazaarCollisionBox("MassSeparationEastSouth", new(19.25f, 3.7f, -3.0f), new(19.5f, 7.4f, 18.0f)),
            BazaarCollisionBox("WallWestConnectorSightBaffle", new(-20.0f, 4.0f, -19.4f), new(0.42f, 8.0f, 3.2f)),
            BazaarCollisionBox("WallEastConnectorSightBaffle", new(20.0f, 4.0f, -13.4f), new(0.42f, 8.0f, 2.8f)),

            // Full-height north blocks leave only the folded 5-8 m back-market chain.
            BazaarCollisionBox("MassNorthWestOuter", new(-58.5f, 3.8f, -43.5f), new(11.0f, 7.6f, 25.0f)),
            BazaarCollisionBox("MassNorthWestCap", new(-44.0f, 3.9f, -49.0f), new(18.0f, 7.8f, 14.0f)),
            BazaarCollisionBox("MassNorthWestShoulder", new(-12.0f, 3.6f, -37.0f), new(10.0f, 7.2f, 12.0f)),
            BazaarCollisionBox("MassNorthCenter", new(0.0f, 3.8f, -37.0f), new(14.0f, 7.6f, 12.0f)),
            BazaarCollisionBox("MassNorthEastShoulder", new(13.5f, 3.6f, -37.0f), new(13.0f, 7.2f, 12.0f)),
            BazaarCollisionBox("MassNorthEastCap", new(44.0f, 3.9f, -49.0f), new(18.0f, 7.8f, 14.0f)),
            BazaarCollisionBox("MassNorthEastOuter", new(58.5f, 3.8f, -45.0f), new(11.0f, 7.6f, 22.0f)),
            BazaarCollisionBox("WallDefenderFoyerWestSightBaffle", new(-20.0f, 3.8f, -51.1f), new(0.42f, 7.6f, 9.8f)),
            BazaarCollisionBox("WallDefenderFoyerEastSightBaffle", new(20.0f, 3.8f, -51.1f), new(0.42f, 7.6f, 9.8f)),
            BazaarCollisionBox("WallDefenderFoyerWestReturn", new(-13.75f, 3.8f, -46.2f), new(12.5f, 7.6f, 0.42f)),
            BazaarCollisionBox("WallDefenderFoyerEastReturn", new(13.75f, 3.8f, -46.2f), new(12.5f, 7.6f, 0.42f)),

            // Roof collision follows the authored enterable interiors.
            BazaarCollisionBox("RoofA_WestArcade", new(-55.5f, 6.37f, -17.5f), new(9.0f, 0.14f, 27.0f)),
            BazaarCollisionBox("RoofA_EastRooms", new(-37.5f, 6.37f, -17.5f), new(7.0f, 0.14f, 27.0f)),
            BazaarCollisionBox("RoofA_RearWarehouse", new(-46.0f, 6.37f, -27.0f), new(10.0f, 0.14f, 8.0f)),
            BazaarCollisionBox("RoofA_SouthVestibule", new(-46.0f, 6.37f, -8.5f), new(10.0f, 0.14f, 9.0f)),
            BazaarCollisionBox("RoofB_MarketWarehouse", new(47.0f, 6.49f, -18.0f), new(26.0f, 0.18f, 24.0f)),
            BazaarCollisionBox("RoofMid_NorthConnector", new(0.0f, 6.17f, -15.5f), new(18.0f, 0.14f, 17.0f)),
            BazaarCollisionBox("RoofMid_TeaHall", new(-3.0f, 6.17f, -1.0f), new(12.0f, 0.14f, 14.0f)),
            BazaarCollisionBox("RoofMid_ProduceHall", new(3.0f, 6.17f, 12.5f), new(12.0f, 0.14f, 15.0f)),
            BazaarCollisionBox("RoofMid_CarpetHall", new(-3.0f, 6.17f, 26.5f), new(12.0f, 0.14f, 15.0f)),

            // Architectural counters and columns replace loose-crate cover.
            BazaarCollisionBox("CoverA_SpiceCounter", new(-49.8f, 0.59f, -17.9f), new(0.62f, 1.18f, 5.8f)),
            BazaarCollisionBox("CoverA_WarehouseDesk", new(-35.5f, 0.59f, -27.0f), new(0.62f, 1.18f, 5.0f)),
            BazaarCollisionBox("CoverA_EntryDesk", new(-46.35f, 0.59f, -9.5f), new(4.7f, 1.18f, 0.62f)),
            BazaarCollisionBox("CoverB_FishCounter", new(42.0f, 0.58f, -17.5f), new(0.62f, 1.16f, 7.0f)),
            BazaarCollisionBox("CoverB_TextileCounter", new(49.0f, 0.58f, -11.0f), new(4.0f, 1.16f, 0.62f)),
            BazaarCollisionBox("CoverB_LoadingDesk", new(37.4f, 0.58f, -19.0f), new(3.2f, 1.16f, 0.62f)),
            BazaarCollisionBox("CoverB_ServiceCounter", new(58.5f, 0.58f, 3.7f), new(0.62f, 1.16f, 4.6f)),
            BazaarCollisionBox("CoverMid_ProduceCounter", new(3.75f, 0.575f, 9.0f), new(5.5f, 1.15f, 0.62f)),
            BazaarCollisionBox("CoverMid_CarpetDivider", new(-7.0f, 0.59f, 25.75f), new(0.62f, 1.18f, 5.5f)),

            // Hanging upper-storey screens stop one balcony from holding two
            // complete entrances while preserving the ground-level rooms.
            BazaarCollisionBox("WallA_GalleryPrivacyScreen", new(-51.0f, 5.0f, -17.4f), new(0.42f, 2.8f, 17.2f)),
            BazaarCollisionBox("WallB_BalconyPrivacyScreen", new(53.0f, 4.95f, -16.75f), new(0.42f, 3.1f, 15.5f))
        };

        AddBazaarVerticalWall(boxes, "WallEastApproachSightReturn", 52.0f, -6.0f, 12.0f, 8.0f,
            new BazaarOpening(6.2f, 3.2f));
        AddBazaarApproachStairVestibuleCollision(
            boxes, "A", -56.0f, -4.0f, 2.1f, 6.3f, entryOnEast: true);
        AddBazaarApproachStairVestibuleCollision(
            boxes, "B", 56.0f, -6.0f, 1.5f, 6.4f, entryOnEast: false);
        AddBazaarApproachStairVestibuleCollision(
            boxes, "Mid", -6.0f, 34.0f, 40.85f, 6.1f, entryOnEast: true);
        AddBazaarSiteAWalls(boxes);
        AddBazaarSiteBWalls(boxes);
        AddBazaarMidWalls(boxes);
        AddBazaarBackMarketCollision(boxes);
        AddBazaarGuardRailCollision(boxes);
        return boxes;
    }

    private void AddBazaarApproachStairVestibuleCollision(
        List<DemolitionArenaBox> boxes,
        string name,
        float centerX,
        float buildingZ,
        float stairBottomZ,
        float roofHeight,
        bool entryOnEast)
    {
        const float halfWidth = 1.8f;
        const float entryWidth = 3.2f;
        const float landingDepth = 4.3f;
        var outerZ = stairBottomZ + landingDepth;
        var entryCenterZ = stairBottomZ + 2.2f;
        var westX = centerX - halfWidth;
        var eastX = centerX + halfWidth;
        var opening = new BazaarOpening(entryCenterZ, entryWidth);

        if (entryOnEast)
        {
            AddBazaarVerticalWall(boxes, $"Wall{name}SouthStairVestibuleWest", westX,
                buildingZ, outerZ, roofHeight);
            AddBazaarVerticalWall(boxes, $"Wall{name}SouthStairVestibuleEast", eastX,
                buildingZ, outerZ, roofHeight, opening);
        }
        else
        {
            AddBazaarVerticalWall(boxes, $"Wall{name}SouthStairVestibuleWest", westX,
                buildingZ, outerZ, roofHeight, opening);
            AddBazaarVerticalWall(boxes, $"Wall{name}SouthStairVestibuleEast", eastX,
                buildingZ, outerZ, roofHeight);
        }
        AddBazaarHorizontalWall(boxes, $"Wall{name}SouthStairVestibuleOuter", outerZ,
            westX, eastX, roofHeight);
        boxes.Add(BazaarCollisionBox(
            $"Roof{name}SouthStairVestibule",
            new(centerX, roofHeight + 0.07f, (buildingZ + outerZ) * 0.5f),
            new(halfWidth * 2.0f + 0.2f, 0.14f, outerZ - buildingZ)));
    }

    private void AddBazaarSiteAWalls(List<DemolitionArenaBox> boxes)
    {
        AddBazaarHorizontalWall(boxes, "WallA_South", -4.0f, -60.0f, -34.0f, 6.4f,
            new BazaarOpening(-56.0f, 3.2f), new BazaarOpening(-47.0f, 3.4f));
        AddBazaarHorizontalWall(boxes, "WallA_North", -31.0f, -60.0f, -34.0f, 6.4f,
            new BazaarOpening(-52.0f, 3.2f), new BazaarOpening(-37.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallA_West", -60.0f, -31.0f, -4.0f, 6.4f,
            new BazaarOpening(-12.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallA_East", -34.0f, -31.0f, -4.0f, 6.4f,
            new BazaarOpening(-10.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "PartitionA_Rear", -23.0f, -60.0f, -34.0f, 3.0f,
            new BazaarOpening(-56.0f, 3.2f), new BazaarOpening(-38.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "PartitionA_Warehouse", -47.0f, -31.0f, -23.0f, 3.0f,
            new BazaarOpening(-27.0f, 3.2f));
        foreach (var x in new[] { -51.0f, -41.0f })
        {
            foreach (var z in new[] { -22.0f, -18.0f, -14.0f })
            {
                boxes.Add(BazaarCollisionBox($"ColumnA_Arcade_{x:0}_{z:0}",
                    new(x, 1.5f, z), new(0.68f, 3.0f, 0.68f)));
            }
        }
    }

    private void AddBazaarSiteBWalls(List<DemolitionArenaBox> boxes)
    {
        AddBazaarHorizontalWall(boxes, "WallB_South", -6.0f, 34.0f, 60.0f, 6.5f,
            new BazaarOpening(46.0f, 3.4f), new BazaarOpening(56.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallB_North", -30.0f, 34.0f, 60.0f, 6.5f,
            new BazaarOpening(40.0f, 3.2f), new BazaarOpening(55.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallB_West", 34.0f, -30.0f, -6.0f, 6.5f,
            new BazaarOpening(-14.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallB_East", 60.0f, -30.0f, -6.0f, 6.5f,
            new BazaarOpening(-12.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "PartitionB_Loading", 40.0f, -28.0f, -6.0f, 3.0f,
            new BazaarOpening(-25.3f, 3.4f), new BazaarOpening(-14.4f, 3.2f));
        AddBazaarVerticalWall(boxes, "PartitionB_Stockroom", 52.0f, -30.0f, -6.0f, 3.0f,
            new BazaarOpening(-27.0f, 3.2f), new BazaarOpening(-23.4f, 3.2f),
            new BazaarOpening(-12.4f, 3.2f));
        foreach (var x in new[] { 39.0f, 45.0f, 51.0f, 57.0f })
        {
            foreach (var z in new[] { -25.5f, -17.5f, -9.5f })
            {
                boxes.Add(BazaarCollisionBox($"ColumnB_Warehouse_{x:0}_{z:0}",
                    new(x, 3.125f, z), new(0.52f, 6.25f, 0.52f)));
            }
        }
    }

    private void AddBazaarMidWalls(List<DemolitionArenaBox> boxes)
    {
        AddBazaarHorizontalWall(boxes, "WallMidConnector_North", -24.0f, -9.0f, 9.0f, 6.2f,
            new BazaarOpening(4.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallMidConnector_South", -7.0f, -9.0f, 9.0f, 6.2f,
            new BazaarOpening(-5.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallMidConnector_West", -9.0f, -24.0f, -7.0f, 6.2f,
            new BazaarOpening(-18.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallMidConnector_East", 9.0f, -24.0f, -7.0f, 6.2f,
            new BazaarOpening(-14.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "PartitionMidConnector_WestBaffle", -16.7f,
            -8.8f, 1.5f, 3.0f);
        AddBazaarHorizontalWall(boxes, "PartitionMidConnector_EastBaffle", -12.7f,
            -1.5f, 8.8f, 3.0f);

        AddBazaarHorizontalWall(boxes, "WallMidTea_North", -8.0f, -9.0f, 3.0f, 6.2f,
            new BazaarOpening(-5.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallMidTea_South", 6.0f, -9.0f, 3.0f, 6.2f,
            new BazaarOpening(-1.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallMidTea_West", -9.0f, -8.0f, 6.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidTea_East", 3.0f, -8.0f, 6.0f, 6.2f);

        AddBazaarHorizontalWall(boxes, "WallMidProduce_North", 5.0f, -3.0f, 9.0f, 6.2f,
            new BazaarOpening(0.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallMidProduce_South", 20.0f, -3.0f, 9.0f, 6.2f,
            new BazaarOpening(1.0f, 3.2f));
        AddBazaarVerticalWall(boxes, "WallMidProduce_West", -3.0f, 5.0f, 20.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidProduce_East", 9.0f, 5.0f, 20.0f, 6.2f);

        AddBazaarHorizontalWall(boxes, "WallMidCarpet_North", 19.0f, -9.0f, 3.0f, 6.2f,
            new BazaarOpening(0.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallMidCarpet_South", 34.0f, -9.0f, 3.0f, 6.2f,
            new BazaarOpening(-6.0f, 3.2f), new BazaarOpening(0.0f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallMidCarpet_SouthReturn", 34.0f, 3.0f, 8.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidCarpet_West", -9.0f, 19.0f, 34.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidCarpet_East", 3.0f, 19.0f, 34.0f, 6.2f);
    }

    private void AddBazaarBackMarketCollision(List<DemolitionArenaBox> boxes)
    {
        foreach (var roof in new[]
                 {
                     (Name: "WestRear", Center: new Vector3(-40.0f, 4.23f, -38.0f), Size: new Vector3(26.0f, 0.16f, 8.0f)),
                     (Name: "WestSpawn", Center: new Vector3(-17.0f, 4.23f, -47.0f), Size: new Vector3(20.0f, 0.16f, 8.0f)),
                     (Name: "EastSpawn", Center: new Vector3(17.0f, 4.23f, -47.0f), Size: new Vector3(20.0f, 0.16f, 8.0f)),
                     (Name: "EastRear", Center: new Vector3(40.0f, 4.23f, -38.0f), Size: new Vector3(26.0f, 0.16f, 8.0f))
                 })
        {
            boxes.Add(BazaarCollisionBox($"RoofBack_{roof.Name}", roof.Center, roof.Size));
        }
        AddBazaarHorizontalWall(boxes, "WallBack_WestRearSouth", -34.0f, -53.0f, -27.0f, 3.0f,
            new BazaarOpening(-44.42f, 3.2f), new BazaarOpening(-33.24f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallBack_WestSpawnSouth", -43.0f, -27.0f, -7.0f, 3.0f,
            new BazaarOpening(-20.4f, 3.2f), new BazaarOpening(-11.8f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallBack_EastSpawnSouth", -43.0f, 7.0f, 27.0f, 3.0f,
            new BazaarOpening(13.6f, 3.2f), new BazaarOpening(22.2f, 3.2f));
        AddBazaarHorizontalWall(boxes, "WallBack_EastRearSouth", -34.0f, 27.0f, 53.0f, 3.0f,
            new BazaarOpening(35.58f, 3.2f), new BazaarOpening(46.76f, 3.2f));
    }

    private void AddBazaarGuardRailCollision(List<DemolitionArenaBox> boxes)
    {
        // These invisible world-layer boxes match the finished open rails authored in
        // Blender. They are blockers, not walkable traversal surfaces.
        boxes.Add(BazaarCollisionBox(
            "GuardRailAGalleryInner",
            new(-53.0f, 4.15f, -16.4f),
            new(BazaarGuardRailThickness, BazaarGuardRailHeight, 14.8f)));
        boxes.Add(BazaarCollisionBox(
            "GuardRailBBalconyInner",
            new(53.0f, 3.95f, -16.4f),
            new(BazaarGuardRailThickness, BazaarGuardRailHeight, 14.8f)));
        boxes.Add(BazaarCollisionBox(
            "GuardRailMidMezzanineInner",
            new(-3.0f, 3.75f, 24.0f),
            new(BazaarGuardRailThickness, BazaarGuardRailHeight, 14.0f)));

        AddBazaarStairGuardRails(
            boxes,
            "GuardRailAGallerySouth",
            new(-56.0f, 0.0f, 2.1f),
            new(-56.0f, 3.6f, -9.0f));
        AddBazaarStairGuardRails(
            boxes,
            "GuardRailAGalleryRear",
            new(-41.9f, 0.0f, -27.0f),
            new(-53.0f, 3.6f, -27.0f));
        AddBazaarStairGuardRails(
            boxes,
            "GuardRailMidMezzanineSouth",
            new(-6.0f, 0.0f, 40.85f),
            new(-6.0f, 3.2f, 31.0f));
        AddBazaarStairGuardRails(
            boxes,
            "GuardRailMidMezzanineNorth",
            new(-6.0f, 0.0f, 7.15f),
            new(-6.0f, 3.2f, 17.0f));
        AddBazaarStairGuardRails(
            boxes,
            "GuardRailBBalconySouth",
            new(56.0f, 0.0f, 1.5f),
            new(56.0f, 3.4f, -9.0f));
        AddBazaarStairGuardRails(
            boxes,
            "GuardRailBBalconyRear",
            new(42.5f, 0.0f, -27.0f),
            new(53.0f, 3.4f, -27.0f));
    }

    private void AddBazaarStairGuardRails(
        List<DemolitionArenaBox> boxes,
        string namePrefix,
        Vector3 lowSurface,
        Vector3 highSurface)
    {
        var delta = highSurface - lowSurface;
        var horizontalRun = new Vector2(delta.X, delta.Z).Length();
        var angle = Mathf.Atan2(delta.Y, horizontalRun);
        var length = delta.Length();
        var runsAlongX = Mathf.Abs(delta.X) > Mathf.Abs(delta.Z);
        var rotation = runsAlongX
            ? new Vector3(0.0f, 0.0f, Mathf.Sign(delta.X) * angle)
            : new Vector3(-Mathf.Sign(delta.Z) * angle, 0.0f, 0.0f);
        var size = runsAlongX
            ? new Vector3(length, BazaarGuardRailHeight, BazaarGuardRailThickness)
            : new Vector3(BazaarGuardRailThickness, BazaarGuardRailHeight, length);
        var lateral = new Vector3(-delta.Z, 0.0f, delta.X).Normalized();
        var surfaceNormal = Basis.FromEuler(rotation) * Vector3.Up;
        var center = (lowSurface + highSurface) * 0.5f
            + surfaceNormal * (BazaarGuardRailHeight * 0.5f);
        boxes.Add(BazaarCollisionBox(
            $"{namePrefix}Left",
            center - lateral * BazaarStairGuardRailOffset,
            size,
            rotation));
        boxes.Add(BazaarCollisionBox(
            $"{namePrefix}Right",
            center + lateral * BazaarStairGuardRailOffset,
            size,
            rotation));
    }

    private void AddBazaarHorizontalWall(
        List<DemolitionArenaBox> boxes,
        string name,
        float z,
        float minimumX,
        float maximumX,
        float height,
        params BazaarOpening[] openings)
    {
        Array.Sort(openings, (left, right) => left.Center.CompareTo(right.Center));
        var cursor = minimumX;
        var segment = 0;
        foreach (var opening in openings)
        {
            var openingMinimum = Mathf.Clamp(opening.Center - opening.Width * 0.5f, minimumX, maximumX);
            var openingMaximum = Mathf.Clamp(opening.Center + opening.Width * 0.5f, minimumX, maximumX);
            AddBazaarHorizontalSegment(boxes, name, segment++, z, cursor, openingMinimum, height);
            cursor = Mathf.Max(cursor, openingMaximum);
        }
        AddBazaarHorizontalSegment(boxes, name, segment, z, cursor, maximumX, height);
    }

    private void AddBazaarHorizontalSegment(
        List<DemolitionArenaBox> boxes,
        string name,
        int segment,
        float z,
        float minimumX,
        float maximumX,
        float height)
    {
        if (maximumX - minimumX <= 0.05f)
        {
            return;
        }
        boxes.Add(BazaarCollisionBox($"{name}_Segment{segment:00}",
            new((minimumX + maximumX) * 0.5f, height * 0.5f, z),
            new(maximumX - minimumX, height, BazaarWallThickness)));
    }

    private void AddBazaarVerticalWall(
        List<DemolitionArenaBox> boxes,
        string name,
        float x,
        float minimumZ,
        float maximumZ,
        float height,
        params BazaarOpening[] openings)
    {
        Array.Sort(openings, (left, right) => left.Center.CompareTo(right.Center));
        var cursor = minimumZ;
        var segment = 0;
        foreach (var opening in openings)
        {
            var openingMinimum = Mathf.Clamp(opening.Center - opening.Width * 0.5f, minimumZ, maximumZ);
            var openingMaximum = Mathf.Clamp(opening.Center + opening.Width * 0.5f, minimumZ, maximumZ);
            AddBazaarVerticalSegment(boxes, name, segment++, x, cursor, openingMinimum, height);
            cursor = Mathf.Max(cursor, openingMaximum);
        }
        AddBazaarVerticalSegment(boxes, name, segment, x, cursor, maximumZ, height);
    }

    private void AddBazaarVerticalSegment(
        List<DemolitionArenaBox> boxes,
        string name,
        int segment,
        float x,
        float minimumZ,
        float maximumZ,
        float height)
    {
        if (maximumZ - minimumZ <= 0.05f)
        {
            return;
        }
        boxes.Add(BazaarCollisionBox($"{name}_Segment{segment:00}",
            new(x, height * 0.5f, (minimumZ + maximumZ) * 0.5f),
            new(BazaarWallThickness, height, maximumZ - minimumZ)));
    }

    internal IReadOnlyList<BazaarThreshold> BazaarExteriorThresholds()
    {
        var thresholds = new List<BazaarThreshold>();
        foreach (var scan in BazaarSiteWallScans())
        {
            var intervals = CollisionBoxes
                .Where(box => box.Name.StartsWith(scan.Prefix + "_Segment", StringComparison.Ordinal)
                    && box.Size.Y >= 2.8f
                    && Mathf.Abs((scan.Horizontal ? box.Center.Z : box.Center.X)
                        - scan.FixedCoordinate) <= 0.05f)
                .Select(box =>
                {
                    var center = scan.Horizontal ? box.Center.X : box.Center.Z;
                    var size = scan.Horizontal ? box.Size.X : box.Size.Z;
                    return new BazaarInterval(
                        Mathf.Max(scan.Minimum, center - size * 0.5f),
                        Mathf.Min(scan.Maximum, center + size * 0.5f));
                })
                .Where(interval => interval.Maximum - interval.Minimum > 0.01f)
                .OrderBy(interval => interval.Minimum)
                .ToArray();
            var cursor = scan.Minimum;
            var gapIndex = 0;
            foreach (var interval in intervals)
            {
                if (interval.Minimum - cursor > 0.05f)
                {
                    AddBazaarThreshold(thresholds, scan, cursor, interval.Minimum, gapIndex++);
                }
                cursor = Mathf.Max(cursor, interval.Maximum);
            }
            if (scan.Maximum - cursor > 0.05f)
            {
                AddBazaarThreshold(thresholds, scan, cursor, scan.Maximum, gapIndex);
            }
        }
        return thresholds;
    }

    internal IReadOnlyList<BazaarThreshold> BazaarGroundThresholds()
        => BazaarExteriorThresholds()
            .Where(threshold => !threshold.StairTransition)
            .ToArray();

    internal bool BazaarSiteShellReady(string site)
    {
        var scans = BazaarSiteWallScans().Where(scan => scan.Site == site).ToArray();
        var thresholds = BazaarExteriorThresholds()
            .Where(threshold => threshold.Site == site)
            .ToArray();
        var segmentsPresent = scans.All(scan => CollisionBoxes.Any(box =>
            box.Name.StartsWith(scan.Prefix + "_Segment", StringComparison.Ordinal)
            && box.Size.Y >= 2.8f
            && Mathf.Abs((scan.Horizontal ? box.Center.Z : box.Center.X)
                - scan.FixedCoordinate) <= 0.05f));
        return segmentsPresent
            && thresholds.Length == 6
            && thresholds.Count(threshold => threshold.StairTransition) == 1
            && thresholds.Count(threshold => !threshold.StairTransition) == 5
            && thresholds.All(threshold => threshold.Width is >= 2.8f and <= 3.6f);
    }

    private IReadOnlyList<float> BuildBazaarCrossingCriticalPassageWidths()
        => BazaarGroundThresholds()
            .Select(threshold => threshold.Width)
            .Concat(new[] { 4.5f, 7.0f })
            .ToArray();

    private IReadOnlyList<float> BuildBazaarCrossingCriticalPassageHeights()
        => BazaarGroundThresholds()
            .Select(BazaarThresholdClearHeight)
            .ToArray();

    private float BazaarThresholdClearHeight(BazaarThreshold threshold)
    {
        var overheadBottom = CollisionBoxes.Concat(TraversalBoxes)
            .Where(box => box.Center.Y - box.Size.Y * 0.5f
                    >= Origin.Y + MinimumPassageHeight
                && Mathf.Abs(threshold.Center.X - box.Center.X) <= box.Size.X * 0.5f
                && Mathf.Abs(threshold.Center.Z - box.Center.Z) <= box.Size.Z * 0.5f)
            .Select(box => box.Center.Y - box.Size.Y * 0.5f)
            .DefaultIfEmpty(Origin.Y + 8.0f)
            .Min();
        return overheadBottom - Origin.Y;
    }

    private IReadOnlyList<BazaarWallScan> BazaarSiteWallScans() => new[]
    {
        new BazaarWallScan("a-south", "a", "WallA_South", true,
            Origin.Z - 4.0f, Origin.X - 60.0f, Origin.X - 34.0f, Vector3.Forward),
        new BazaarWallScan("a-north", "a", "WallA_North", true,
            Origin.Z - 31.0f, Origin.X - 60.0f, Origin.X - 34.0f, Vector3.Forward),
        new BazaarWallScan("a-west", "a", "WallA_West", false,
            Origin.X - 60.0f, Origin.Z - 31.0f, Origin.Z - 4.0f, Vector3.Right),
        new BazaarWallScan("a-east", "a", "WallA_East", false,
            Origin.X - 34.0f, Origin.Z - 31.0f, Origin.Z - 4.0f, Vector3.Right),
        new BazaarWallScan("b-south", "b", "WallB_South", true,
            Origin.Z - 6.0f, Origin.X + 34.0f, Origin.X + 60.0f, Vector3.Forward),
        new BazaarWallScan("b-north", "b", "WallB_North", true,
            Origin.Z - 30.0f, Origin.X + 34.0f, Origin.X + 60.0f, Vector3.Forward),
        new BazaarWallScan("b-west", "b", "WallB_West", false,
            Origin.X + 34.0f, Origin.Z - 30.0f, Origin.Z - 6.0f, Vector3.Right),
        new BazaarWallScan("b-east", "b", "WallB_East", false,
            Origin.X + 60.0f, Origin.Z - 30.0f, Origin.Z - 6.0f, Vector3.Right)
    };

    private void AddBazaarThreshold(
        List<BazaarThreshold> thresholds,
        BazaarWallScan scan,
        float minimum,
        float maximum,
        int gapIndex)
    {
        var centerCoordinate = (minimum + maximum) * 0.5f;
        var center = scan.Horizontal
            ? new Vector3(centerCoordinate, Origin.Y + 1.2f, scan.FixedCoordinate)
            : new Vector3(scan.FixedCoordinate, Origin.Y + 1.2f, centerCoordinate);
        var stairTransition = TraversalBoxes
            .Where(box => box.Name.EndsWith("Ramp", StringComparison.Ordinal))
            .Any(ramp => BazaarRampIntersectsGap(ramp, scan, minimum, maximum));
        thresholds.Add(new BazaarThreshold(
            $"{scan.Name}-{gapIndex + 1}",
            scan.Site,
            center,
            scan.Normal,
            maximum - minimum,
            stairTransition));
    }

    private static bool BazaarRampIntersectsGap(
        DemolitionArenaBox ramp,
        BazaarWallScan scan,
        float minimum,
        float maximum)
    {
        var halfX = Mathf.Abs(ramp.Rotation.Z) > 0.001f
            ? Mathf.Abs(Mathf.Cos(ramp.Rotation.Z)) * ramp.Size.X * 0.5f
                + Mathf.Abs(Mathf.Sin(ramp.Rotation.Z)) * ramp.Size.Y * 0.5f
            : ramp.Size.X * 0.5f;
        var halfZ = Mathf.Abs(ramp.Rotation.X) > 0.001f
            ? Mathf.Abs(Mathf.Cos(ramp.Rotation.X)) * ramp.Size.Z * 0.5f
                + Mathf.Abs(Mathf.Sin(ramp.Rotation.X)) * ramp.Size.Y * 0.5f
            : ramp.Size.Z * 0.5f;
        if (scan.Horizontal)
        {
            return Mathf.Abs(ramp.Center.Z - scan.FixedCoordinate) <= halfZ + 0.05f
                && ramp.Center.X + halfX >= minimum
                && ramp.Center.X - halfX <= maximum;
        }
        return Mathf.Abs(ramp.Center.X - scan.FixedCoordinate) <= halfX + 0.05f
            && ramp.Center.Z + halfZ >= minimum
            && ramp.Center.Z - halfZ <= maximum;
    }

    private IReadOnlyList<DemolitionArenaBox> BuildBazaarCrossingTraversalBoxes()
    {
        return new[]
        {
            BazaarCollisionBox("TraversalAGalleryDeck", new(-56.0f, 3.54f, -18.0f), new(6.0f, 0.12f, 18.0f)),
            BazaarRampBox("TraversalAGallerySouthRamp", new(-56.0f, 0.0f, 2.1f), new(-56.0f, 3.6f, -9.0f)),
            BazaarRampBox("TraversalAGalleryRearRamp", new(-41.9f, 0.0f, -27.0f), new(-53.0f, 3.6f, -27.0f)),
            BazaarCollisionBox("TraversalMidMezzanineDeck", new(-6.0f, 3.14f, 24.0f), new(6.0f, 0.12f, 14.0f)),
            BazaarRampBox("TraversalMidMezzanineSouthRamp", new(-6.0f, 0.0f, 40.85f), new(-6.0f, 3.2f, 31.0f)),
            BazaarRampBox("TraversalMidMezzanineNorthRamp", new(-6.0f, 0.0f, 7.15f), new(-6.0f, 3.2f, 17.0f)),
            BazaarCollisionBox("TraversalBBalconyDeck", new(56.0f, 3.34f, -18.0f), new(6.0f, 0.12f, 18.0f)),
            BazaarRampBox("TraversalBBalconySouthRamp", new(56.0f, 0.0f, 1.5f), new(56.0f, 3.4f, -9.0f)),
            BazaarRampBox("TraversalBBalconyRearRamp", new(42.5f, 0.0f, -27.0f), new(53.0f, 3.4f, -27.0f))
        };
    }

    private DemolitionArenaBox BazaarRampBox(string name, Vector3 lowSurface, Vector3 highSurface)
    {
        const float width = 3.2f;
        const float thickness = 0.22f;
        var delta = highSurface - lowSurface;
        var horizontalRun = new Vector2(delta.X, delta.Z).Length();
        var angle = Mathf.Atan2(delta.Y, horizontalRun);
        var length = Mathf.Sqrt(horizontalRun * horizontalRun + delta.Y * delta.Y);
        var rotation = Mathf.Abs(delta.X) > Mathf.Abs(delta.Z)
            ? new Vector3(0.0f, 0.0f, Mathf.Sign(delta.X) * angle)
            : new Vector3(-Mathf.Sign(delta.Z) * angle, 0.0f, 0.0f);
        var size = Mathf.Abs(delta.X) > Mathf.Abs(delta.Z)
            ? new Vector3(length, thickness, width)
            : new Vector3(width, thickness, length);
        var center = (lowSurface + highSurface) * 0.5f;
        center.Y -= thickness * Mathf.Cos(angle) * 0.5f;
        return BazaarCollisionBox(name, center, size, rotation);
    }

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackToAPath() => WorldPoints(
        new(0.0f, 0.2f, 49.0f), new(-8.4f, 0.2f, 48.0f),
        new(-8.4f, 0.2f, 41.0f), new(-11.5f, 0.2f, 40.0f), new(-12.5f, 0.2f, 38.5f),
        new(-12.5f, 0.2f, 20.0f), new(-15.5f, 0.2f, 11.2f),
        new(-24.0f, 0.2f, 9.0f), new(-37.0f, 0.2f, 9.0f),
        new(-47.0f, 0.2f, 8.0f), new(-47.0f, 0.2f, 1.0f),
        new(-47.0f, 0.2f, -4.0f), new(-43.0f, 0.2f, -8.5f),
        new(-43.0f, 0.2f, -12.0f),
        new(-46.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackToBPath() => WorldPoints(
        new(0.0f, 0.2f, 49.0f), new(8.0f, 0.2f, 49.0f),
        new(8.0f, 0.2f, 41.0f), new(11.5f, 0.2f, 40.0f), new(12.5f, 0.2f, 38.5f),
        new(12.5f, 0.2f, 20.0f), new(12.5f, 0.2f, 10.0f),
        new(25.0f, 0.2f, 9.0f), new(38.0f, 0.2f, 9.0f),
        new(46.0f, 0.2f, 7.0f), new(46.0f, 0.2f, 0.0f),
        new(46.0f, 0.2f, -6.0f), new(46.0f, 0.2f, -11.0f),
        new(46.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingAttackMidPath() => WorldPoints(
        new(0.0f, 0.2f, 49.0f), new(0.0f, 0.2f, 41.0f),
        new(0.0f, 0.2f, 38.0f), new(0.0f, 0.2f, 34.0f),
        new(0.0f, 0.2f, 27.0f), new(1.0f, 0.2f, 20.0f),
        new(1.0f, 0.2f, 19.5f), new(0.0f, 0.2f, 18.7f),
        new(1.0f, 0.2f, 12.0f), new(-1.0f, 0.2f, 6.0f),
        new(0.0f, 0.2f, 5.5f), new(-5.0f, 0.2f, -7.0f),
        new(-5.0f, 0.2f, -10.0f), new(-5.0f, 0.2f, -14.0f),
        new(3.0f, 0.2f, -15.0f), new(3.0f, 0.2f, -18.0f),
        new(4.0f, 0.2f, -23.0f), new(4.0f, 0.2f, -24.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingDefenderToAPath() => WorldPoints(
        new(0.0f, 0.2f, -49.0f), new(-6.0f, 0.2f, -50.0f),
        new(-6.0f, 0.2f, -44.6f), new(-17.0f, 0.2f, -44.6f),
        new(-20.4f, 0.2f, -43.8f), new(-20.4f, 0.2f, -42.0f),
        new(-28.0f, 0.2f, -41.0f), new(-32.0f, 0.2f, -38.0f),
        new(-33.24f, 0.2f, -35.5f), new(-33.24f, 0.2f, -34.0f),
        new(-33.24f, 0.2f, -32.5f), new(-37.0f, 0.2f, -32.0f),
        new(-37.0f, 0.2f, -31.0f), new(-37.0f, 0.2f, -27.0f),
        new(-37.0f, 0.2f, -23.0f),
        new(-37.0f, 0.2f, -20.5f), new(-46.0f, 0.2f, -20.5f),
        new(-46.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingDefenderToBPath() => WorldPoints(
        new(0.0f, 0.2f, -49.0f), new(6.0f, 0.2f, -50.0f),
        new(6.0f, 0.2f, -44.6f), new(17.0f, 0.2f, -44.6f),
        new(22.2f, 0.2f, -43.8f), new(22.2f, 0.2f, -42.0f),
        new(28.0f, 0.2f, -41.0f), new(36.0f, 0.2f, -37.5f),
        new(35.58f, 0.2f, -34.0f), new(35.58f, 0.2f, -32.0f),
        new(40.0f, 0.2f, -32.0f),
        new(40.0f, 0.2f, -30.0f), new(40.0f, 0.2f, -28.8f),
        new(38.0f, 0.2f, -28.8f),
        new(38.0f, 0.2f, -24.5f), new(41.0f, 0.2f, -24.5f),
        new(41.0f, 0.2f, -23.0f), new(43.0f, 0.2f, -22.5f),
        new(47.0f, 0.2f, -22.5f), new(47.0f, 0.2f, -20.0f),
        new(46.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildBazaarCrossingSiteRotationPath() => WorldPoints(
        new(-46.0f, 0.2f, -18.0f), new(-40.0f, 0.2f, -16.0f),
        new(-34.0f, 0.2f, -10.0f), new(-31.5f, 0.2f, -12.0f),
        new(-31.5f, 0.2f, -18.0f), new(-27.0f, 0.2f, -16.4f),
        new(-22.0f, 0.2f, -16.4f), new(-18.0f, 0.2f, -16.4f),
        new(-12.0f, 0.2f, -18.0f),
        new(-9.0f, 0.2f, -18.0f), new(3.0f, 0.2f, -18.0f),
        new(3.0f, 0.2f, -14.0f),
        new(9.0f, 0.2f, -14.0f), new(12.0f, 0.2f, -14.0f),
        new(12.0f, 0.2f, -16.2f), new(18.0f, 0.2f, -16.2f),
        new(22.0f, 0.2f, -16.2f), new(30.0f, 0.2f, -16.2f),
        new(34.0f, 0.2f, -14.0f), new(40.0f, 0.2f, -14.4f),
        new(41.0f, 0.2f, -14.4f), new(41.0f, 0.2f, -12.2f),
        new(44.0f, 0.2f, -12.2f), new(47.0f, 0.2f, -13.0f),
        new(47.0f, 0.2f, -18.0f),
        new(46.0f, 0.2f, -18.0f));

    private IReadOnlyList<IReadOnlyList<Vector3>> BuildBazaarCrossingAuxiliaryPaths()
    {
        return new IReadOnlyList<Vector3>[]
        {
            BuildBazaarNorthBackMarketPath(),
            BuildBazaarAGalleryPath(),
            BuildBazaarMidMezzaninePath(),
            BuildBazaarBBalconyPath()
        };
    }

    private IReadOnlyList<Vector3> BuildBazaarNorthBackMarketPath() => WorldPoints(
        new(-46.0f, 0.2f, -18.0f), new(-46.0f, 0.2f, -20.5f),
        new(-37.0f, 0.2f, -20.5f), new(-37.0f, 0.2f, -23.0f),
        new(-37.0f, 0.2f, -27.0f), new(-37.0f, 0.2f, -31.0f),
        new(-37.0f, 0.2f, -32.0f), new(-44.42f, 0.2f, -32.0f),
        new(-44.42f, 0.2f, -34.0f), new(-44.42f, 0.2f, -35.5f),
        new(-44.4f, 0.2f, -38.0f),
        new(-32.0f, 0.2f, -39.0f), new(-28.0f, 0.2f, -41.0f),
        new(-20.4f, 0.2f, -42.0f), new(-20.4f, 0.2f, -43.8f),
        new(-17.0f, 0.2f, -44.6f), new(-6.0f, 0.2f, -44.6f),
        new(-6.0f, 0.2f, -50.0f), new(0.0f, 0.2f, -52.0f),
        new(6.0f, 0.2f, -50.0f), new(6.0f, 0.2f, -44.6f),
        new(17.0f, 0.2f, -44.6f), new(22.2f, 0.2f, -43.8f),
        new(22.2f, 0.2f, -42.0f),
        new(28.0f, 0.2f, -41.0f),
        new(36.0f, 0.2f, -38.0f), new(35.58f, 0.2f, -34.0f),
        new(35.58f, 0.2f, -32.0f), new(40.0f, 0.2f, -32.0f),
        new(40.0f, 0.2f, -30.0f), new(40.0f, 0.2f, -28.8f),
        new(38.0f, 0.2f, -28.8f),
        new(38.0f, 0.2f, -24.5f), new(41.0f, 0.2f, -24.5f),
        new(41.0f, 0.2f, -23.0f), new(43.0f, 0.2f, -22.5f),
        new(47.0f, 0.2f, -22.5f), new(47.0f, 0.2f, -20.0f),
        new(46.0f, 0.2f, -18.0f));

    private IReadOnlyList<Vector3> BuildBazaarAGalleryPath()
    {
        var points = new List<Vector3>();
        AddBazaarSlopePoints(points, new(-56.0f, 0.2f, 2.1f), new(-56.0f, BazaarGalleryRouteHeight, -9.0f), 8);
        points.Add(new(-56.0f, BazaarGalleryRouteHeight, -18.0f));
        points.Add(new(-56.0f, BazaarGalleryRouteHeight, -25.0f));
        points.Add(new(-53.0f, BazaarGalleryRouteHeight, -27.0f));
        AddBazaarSlopePoints(points, new(-53.0f, BazaarGalleryRouteHeight, -27.0f), new(-41.9f, 0.2f, -27.0f), 8);
        return BazaarWorldPoints(points);
    }

    private IReadOnlyList<Vector3> BuildBazaarMidMezzaninePath()
    {
        var points = new List<Vector3>();
        AddBazaarSlopePoints(points, new(-6.0f, 0.2f, 40.85f), new(-6.0f, BazaarMezzanineRouteHeight, 31.0f), 8);
        points.Add(new(-6.0f, BazaarMezzanineRouteHeight, 24.0f));
        points.Add(new(-6.0f, BazaarMezzanineRouteHeight, 17.0f));
        AddBazaarSlopePoints(points, new(-6.0f, BazaarMezzanineRouteHeight, 17.0f), new(-6.0f, 0.2f, 7.15f), 8);
        return BazaarWorldPoints(points);
    }

    private IReadOnlyList<Vector3> BuildBazaarBBalconyPath()
    {
        var points = new List<Vector3>();
        AddBazaarSlopePoints(points, new(56.0f, 0.2f, 1.5f), new(56.0f, BazaarBalconyRouteHeight, -9.0f), 8);
        points.Add(new(56.0f, BazaarBalconyRouteHeight, -17.0f));
        points.Add(new(56.0f, BazaarBalconyRouteHeight, -24.0f));
        points.Add(new(53.0f, BazaarBalconyRouteHeight, -27.0f));
        AddBazaarSlopePoints(points, new(53.0f, BazaarBalconyRouteHeight, -27.0f), new(42.5f, 0.2f, -27.0f), 8);
        return BazaarWorldPoints(points);
    }

    private static void AddBazaarSlopePoints(List<Vector3> points, Vector3 start, Vector3 end, int segments)
    {
        for (var index = 0; index <= segments; index++)
        {
            var point = start.Lerp(end, index / (float)segments);
            if (points.Count == 0 || points[^1].DistanceSquaredTo(point) > 0.0001f)
            {
                points.Add(point);
            }
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
        "attack_entry_a" => World(new Vector3(-47.0f, BazaarGroundRouteHeight, -6.0f)),
        "attack_entry_b" => World(new Vector3(46.0f, BazaarGroundRouteHeight, -8.0f)),
        "attack_support_a" => World(new Vector3(-47.0f, BazaarGroundRouteHeight, 1.0f)),
        "attack_support_b" => World(new Vector3(46.0f, BazaarGroundRouteHeight, 0.0f)),
        "attack_mid_recon" => World(new Vector3(4.0f, BazaarGroundRouteHeight, -22.0f)),
        "defense_anchor_a" => World(new Vector3(-43.0f, BazaarGroundRouteHeight, -18.0f)),
        "defense_anchor_b" => World(new Vector3(43.0f, BazaarGroundRouteHeight, -18.0f)),
        "defense_mid" => World(new Vector3(-6.0f, BazaarMezzanineRouteHeight, 24.0f)),
        "defense_rotate_a" => World(new Vector3(-44.4f, BazaarGroundRouteHeight, -38.0f)),
        "defense_rotate_b" => World(new Vector3(36.0f, BazaarGroundRouteHeight, -38.0f)),
        "retake_entry_a" => World(new Vector3(-52.0f, BazaarGroundRouteHeight, -27.0f)),
        "retake_entry_b" => World(new Vector3(40.0f, BazaarGroundRouteHeight, -26.0f)),
        "retake_cover_a" => World(new Vector3(-56.0f, BazaarGalleryRouteHeight, -25.0f)),
        "retake_cover_b" => World(new Vector3(56.0f, BazaarBalconyRouteHeight, -24.0f)),
        "retake_flank_a" => World(new Vector3(-34.0f, BazaarGroundRouteHeight, -10.0f)),
        "retake_flank_b" => World(new Vector3(34.0f, BazaarGroundRouteHeight, -14.0f)),
        "postplant_guard_a" => World(new Vector3(-56.0f, BazaarGalleryRouteHeight, -18.0f)),
        "postplant_guard_b" => World(new Vector3(56.0f, BazaarBalconyRouteHeight, -17.0f)),
        "postplant_crossfire_a" => World(new Vector3(-43.0f, BazaarGroundRouteHeight, -18.0f)),
        "postplant_crossfire_b" => World(new Vector3(49.0f, BazaarGroundRouteHeight, -18.0f)),
        "postplant_lurk_a" => World(new Vector3(-31.5f, BazaarGroundRouteHeight, -18.0f)),
        "postplant_lurk_b" => World(new Vector3(30.0f, BazaarGroundRouteHeight, -14.0f)),
        "site_a" => SitePositions[0],
        "site_b" => SitePositions[1],
        _ => Midpoint
    };
}
