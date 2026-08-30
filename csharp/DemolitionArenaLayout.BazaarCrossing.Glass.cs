using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public sealed partial class DemolitionArenaLayout
{
    private const float BazaarPortalGlassHeight = 3.0f;
    private const float BazaarPortalGlassThickness = 0.10f;

    internal readonly record struct BazaarGlassPortal(
        string Name,
        string WallName,
        Vector3 WorldCenter,
        Vector3 Size,
        Vector3 Normal)
    {
        public float Width => Mathf.Max(Size.X, Size.Z);
        public bool Horizontal => Mathf.Abs(Normal.Z) > 0.5f;
    }

    private IReadOnlyList<BazaarGlassPortal>? _bazaarGlassPortals;

    internal IReadOnlyList<BazaarGlassPortal> BazaarGlassPortals
        => MapId == DemolitionMapCatalog.BazaarCrossingId
            ? _bazaarGlassPortals ??= BuildBazaarGlassPortals()
            : Array.Empty<BazaarGlassPortal>();

    private IReadOnlyList<BazaarGlassPortal> BuildBazaarGlassPortals()
    {
        return Array.AsReadOnly(new[]
        {
            HorizontalBazaarGlassPortal(
                "Bazaar_A_Caravanserai_South_Portal01", "WallA_South", -47.0f, -4.0f, 3.4f, Vector3.Back),
            HorizontalBazaarGlassPortal(
                "Bazaar_A_Caravanserai_North_Portal00", "WallA_North", -52.0f, -31.0f, 3.2f, Vector3.Forward),
            HorizontalBazaarGlassPortal(
                "Bazaar_A_Caravanserai_North_Portal01", "WallA_North", -37.0f, -31.0f, 3.2f, Vector3.Forward),
            VerticalBazaarGlassPortal(
                "Bazaar_A_Caravanserai_West_Portal00", "WallA_West", -60.0f, -12.0f, 3.2f, Vector3.Left),
            VerticalBazaarGlassPortal(
                "Bazaar_A_Caravanserai_East_Portal00", "WallA_East", -34.0f, -10.0f, 3.2f, Vector3.Right),

            HorizontalBazaarGlassPortal(
                "Bazaar_A_RearWarehouse_Portal_-56.0", "PartitionA_Rear", -56.0f, -23.0f, 4.0f, Vector3.Back),
            HorizontalBazaarGlassPortal(
                "Bazaar_A_RearWarehouse_CenterPortal", "PartitionA_Rear", -46.0f, -23.0f, 4.0f, Vector3.Back),
            HorizontalBazaarGlassPortal(
                "Bazaar_A_RearWarehouse_Portal_-38.0", "PartitionA_Rear", -38.0f, -23.0f, 4.0f, Vector3.Back),

            HorizontalBazaarGlassPortal(
                "Bazaar_B_MarketWarehouse_South_Portal00", "WallB_South", 46.0f, -6.0f, 3.4f, Vector3.Back),
            HorizontalBazaarGlassPortal(
                "Bazaar_B_MarketWarehouse_North_Portal00", "WallB_North", 40.0f, -30.0f, 3.2f, Vector3.Forward),
            HorizontalBazaarGlassPortal(
                "Bazaar_B_MarketWarehouse_North_Portal01", "WallB_North", 55.0f, -30.0f, 3.2f, Vector3.Forward),
            VerticalBazaarGlassPortal(
                "Bazaar_B_MarketWarehouse_West_Portal00", "WallB_West", 34.0f, -14.0f, 3.2f, Vector3.Left),
            VerticalBazaarGlassPortal(
                "Bazaar_B_MarketWarehouse_East_Portal00", "WallB_East", 60.0f, -12.0f, 3.2f, Vector3.Right),

            VerticalBazaarGlassPortal(
                "Bazaar_B_Loading_Portal00", "PartitionB_Loading", 40.0f, -25.3f, 3.4f, Vector3.Right),
            VerticalBazaarGlassPortal(
                "Bazaar_B_Loading_Portal01", "PartitionB_Loading", 40.0f, -14.4f, 3.2f, Vector3.Right),
            VerticalBazaarGlassPortal(
                "Bazaar_B_Stockroom_Portal00", "PartitionB_Stockroom", 52.0f, -27.0f, 3.2f, Vector3.Right),
            VerticalBazaarGlassPortal(
                "Bazaar_B_Stockroom_Portal01", "PartitionB_Stockroom", 52.0f, -23.4f, 3.2f, Vector3.Right),
            VerticalBazaarGlassPortal(
                "Bazaar_B_Stockroom_Portal02", "PartitionB_Stockroom", 52.0f, -12.4f, 3.2f, Vector3.Right),

            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_NorthConnector_North_Portal00", "WallMidConnector_North", 4.0f, -24.0f, 3.2f, Vector3.Forward),
            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_NorthConnector_South_Portal00", "WallMidConnector_South", -5.0f, -7.0f, 3.2f, Vector3.Back),
            VerticalBazaarGlassPortal(
                "Bazaar_Mid_NorthConnector_West_Portal00", "WallMidConnector_West", -9.0f, -18.0f, 3.2f, Vector3.Left),
            VerticalBazaarGlassPortal(
                "Bazaar_Mid_NorthConnector_East_Portal00", "WallMidConnector_East", 9.0f, -14.0f, 3.2f, Vector3.Right),

            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_NorthTeaHall_North_Portal00", "WallMidTea_North", -5.0f, -8.0f, 3.2f, Vector3.Forward),
            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_NorthTeaHall_South_Portal00", "WallMidTea_South", -1.0f, 6.0f, 3.2f, Vector3.Back),
            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_CenterProduceHall_North_Portal00", "WallMidProduce_North", 0.0f, 5.0f, 3.2f, Vector3.Forward),
            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_CenterProduceHall_South_Portal00", "WallMidProduce_South", 1.0f, 20.0f, 3.2f, Vector3.Back),
            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_SouthCarpetHall_North_Portal01", "WallMidCarpet_North", 0.0f, 19.0f, 3.2f, Vector3.Forward),
            HorizontalBazaarGlassPortal(
                "Bazaar_Mid_SouthCarpetHall_South_Portal01", "WallMidCarpet_South", 0.0f, 34.0f, 3.2f, Vector3.Back)
        });
    }

    private BazaarGlassPortal HorizontalBazaarGlassPortal(
        string name,
        string wallName,
        float x,
        float z,
        float width,
        Vector3 normal)
        => new(
            name,
            wallName,
            World(new Vector3(x, BazaarPortalGlassHeight * 0.5f, z)),
            new Vector3(width, BazaarPortalGlassHeight, BazaarPortalGlassThickness),
            normal);

    private BazaarGlassPortal VerticalBazaarGlassPortal(
        string name,
        string wallName,
        float x,
        float z,
        float width,
        Vector3 normal)
        => new(
            name,
            wallName,
            World(new Vector3(x, BazaarPortalGlassHeight * 0.5f, z)),
            new Vector3(BazaarPortalGlassThickness, BazaarPortalGlassHeight, width),
            normal);

    private BazaarOpening BazaarGlassOpening(string name)
    {
        var portal = BazaarGlassPortals.Single(candidate => candidate.Name == name);
        var localCenter = portal.WorldCenter - Origin;
        return new BazaarOpening(
            portal.Horizontal ? localCenter.X : localCenter.Z,
            portal.Width);
    }

    private void AddBazaarGlassPortalLintels(List<DemolitionArenaBox> boxes)
    {
        foreach (var portal in BazaarGlassPortals)
        {
            var wallHeight = portal.WallName switch
            {
                "WallA_South" or "WallA_North" or "WallA_West" or "WallA_East" => 6.4f,
                "WallB_South" or "WallB_North" or "WallB_West" or "WallB_East" => 6.5f,
                var name when name.StartsWith("WallMid", StringComparison.Ordinal) => 6.2f,
                _ => BazaarPortalGlassHeight
            };
            var lintelHeight = wallHeight - BazaarPortalGlassHeight;
            if (lintelHeight <= 0.05f)
            {
                continue;
            }

            var localCenter = portal.WorldCenter - Origin;
            localCenter.Y = BazaarPortalGlassHeight + lintelHeight * 0.5f;
            var size = portal.Horizontal
                ? new Vector3(portal.Width, lintelHeight, BazaarWallThickness)
                : new Vector3(BazaarWallThickness, lintelHeight, portal.Width);
            boxes.Add(BazaarCollisionBox(
                $"{portal.WallName}_Lintel_{portal.Name}",
                localCenter,
                size));
        }
    }

    private void AddBazaarSiteAWalls(List<DemolitionArenaBox> boxes)
    {
        AddBazaarHorizontalWall(boxes, "WallA_South", -4.0f, -60.0f, -34.0f, 6.4f,
            new BazaarOpening(-56.0f, 5.2f),
            BazaarGlassOpening("Bazaar_A_Caravanserai_South_Portal01"));
        AddBazaarHorizontalWall(boxes, "WallA_North", -31.0f, -60.0f, -34.0f, 6.4f,
            BazaarGlassOpening("Bazaar_A_Caravanserai_North_Portal00"),
            BazaarGlassOpening("Bazaar_A_Caravanserai_North_Portal01"));
        AddBazaarVerticalWall(boxes, "WallA_West", -60.0f, -31.0f, -4.0f, 6.4f,
            BazaarGlassOpening("Bazaar_A_Caravanserai_West_Portal00"));
        AddBazaarVerticalWall(boxes, "WallA_East", -34.0f, -31.0f, -4.0f, 6.4f,
            BazaarGlassOpening("Bazaar_A_Caravanserai_East_Portal00"));
        AddBazaarHorizontalWall(boxes, "PartitionA_Rear", -23.0f, -60.0f, -34.0f, 3.0f,
            BazaarGlassOpening("Bazaar_A_RearWarehouse_Portal_-56.0"),
            BazaarGlassOpening("Bazaar_A_RearWarehouse_CenterPortal"),
            BazaarGlassOpening("Bazaar_A_RearWarehouse_Portal_-38.0"));
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
            BazaarGlassOpening("Bazaar_B_MarketWarehouse_South_Portal00"),
            new BazaarOpening(56.0f, 5.2f));
        AddBazaarHorizontalWall(boxes, "WallB_North", -30.0f, 34.0f, 60.0f, 6.5f,
            BazaarGlassOpening("Bazaar_B_MarketWarehouse_North_Portal00"),
            BazaarGlassOpening("Bazaar_B_MarketWarehouse_North_Portal01"));
        AddBazaarVerticalWall(boxes, "WallB_West", 34.0f, -30.0f, -6.0f, 6.5f,
            BazaarGlassOpening("Bazaar_B_MarketWarehouse_West_Portal00"));
        AddBazaarVerticalWall(boxes, "WallB_East", 60.0f, -30.0f, -6.0f, 6.5f,
            BazaarGlassOpening("Bazaar_B_MarketWarehouse_East_Portal00"));
        AddBazaarVerticalWall(boxes, "PartitionB_Loading", 40.0f, -28.0f, -6.0f, 3.0f,
            BazaarGlassOpening("Bazaar_B_Loading_Portal00"),
            BazaarGlassOpening("Bazaar_B_Loading_Portal01"));
        AddBazaarVerticalWall(boxes, "PartitionB_Stockroom", 52.0f, -30.0f, -6.0f, 3.0f,
            BazaarGlassOpening("Bazaar_B_Stockroom_Portal00"),
            BazaarGlassOpening("Bazaar_B_Stockroom_Portal01"),
            BazaarGlassOpening("Bazaar_B_Stockroom_Portal02"));
        foreach (var x in new[] { 39.0f, 45.0f, 51.0f, 57.0f })
        {
            foreach (var z in new[] { -25.5f, -17.5f, -9.5f })
            {
                // The southwest grid column overlapped Loading Portal00's breach and
                // provided no distinct cover value; keep the full 3.4 m doorway clear.
                if (x == 39.0f && z == -25.5f)
                {
                    continue;
                }
                boxes.Add(BazaarCollisionBox($"ColumnB_Warehouse_{x:0}_{z:0}",
                    new(x, 3.125f, z), new(0.52f, 6.25f, 0.52f)));
            }
        }
    }

    private void AddBazaarMidWalls(List<DemolitionArenaBox> boxes)
    {
        AddBazaarHorizontalWall(boxes, "WallMidConnector_North", -24.0f, -9.0f, 9.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_NorthConnector_North_Portal00"));
        AddBazaarHorizontalWall(boxes, "WallMidConnector_South", -7.0f, -9.0f, 9.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_NorthConnector_South_Portal00"));
        AddBazaarVerticalWall(boxes, "WallMidConnector_West", -9.0f, -24.0f, -7.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_NorthConnector_West_Portal00"));
        AddBazaarVerticalWall(boxes, "WallMidConnector_East", 9.0f, -24.0f, -7.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_NorthConnector_East_Portal00"));
        AddBazaarHorizontalWall(boxes, "PartitionMidConnector_WestBaffle", -16.7f,
            -8.8f, 1.5f, 3.0f);
        AddBazaarHorizontalWall(boxes, "PartitionMidConnector_EastBaffle", -12.7f,
            -1.5f, 8.8f, 3.0f);

        AddBazaarHorizontalWall(boxes, "WallMidTea_North", -8.0f, -9.0f, 3.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_NorthTeaHall_North_Portal00"));
        AddBazaarHorizontalWall(boxes, "WallMidTea_South", 6.0f, -9.0f, 3.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_NorthTeaHall_South_Portal00"));
        AddBazaarVerticalWall(boxes, "WallMidTea_West", -9.0f, -8.0f, 6.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidTea_East", 3.0f, -8.0f, 6.0f, 6.2f);

        AddBazaarHorizontalWall(boxes, "WallMidProduce_North", 5.0f, -3.0f, 9.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_CenterProduceHall_North_Portal00"));
        AddBazaarHorizontalWall(boxes, "WallMidProduce_South", 20.0f, -3.0f, 9.0f, 6.2f,
            BazaarGlassOpening("Bazaar_Mid_CenterProduceHall_South_Portal00"));
        AddBazaarVerticalWall(boxes, "WallMidProduce_West", -3.0f, 5.0f, 20.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidProduce_East", 9.0f, 5.0f, 20.0f, 6.2f);

        AddBazaarHorizontalWall(boxes, "WallMidCarpet_North", 19.0f, -9.0f, 3.0f, 6.2f,
            new BazaarOpening(-6.0f, 5.2f),
            BazaarGlassOpening("Bazaar_Mid_SouthCarpetHall_North_Portal01"));
        AddBazaarHorizontalWall(boxes, "WallMidCarpet_South", 34.0f, -9.0f, 3.0f, 6.2f,
            new BazaarOpening(-6.0f, 5.2f),
            BazaarGlassOpening("Bazaar_Mid_SouthCarpetHall_South_Portal01"));
        AddBazaarHorizontalWall(boxes, "WallMidCarpet_SouthReturn", 34.0f, 3.0f, 8.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidCarpet_West", -9.0f, 19.0f, 34.0f, 6.2f);
        AddBazaarVerticalWall(boxes, "WallMidCarpet_East", 3.0f, 19.0f, 34.0f, 6.2f);
    }
}
