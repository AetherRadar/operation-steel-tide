using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

internal sealed record JianghaiGameplayCollisionResult(
    StaticBody3D Body,
    int SourcePlacementCount,
    int PlacementShapeCount,
    int SuppressedPlacementCount,
    IReadOnlyList<string> SuppressedPlacementNames,
    int AuthoredSourceMeshCount,
    int AuthoredShapeCount,
    int DensitySourceCount,
    int SolidSourceCount,
    int EnterableSourceCount,
    int EnterableShapeCount,
    int CollisionShapeCount,
    int BoxShapeCount,
    int ConcaveShapeCount,
    IReadOnlyDictionary<string, int> DistrictShapeCounts);

internal readonly record struct JianghaiEnterableRoomContract(
    float FrontInset,
    float CollisionWidth,
    float CollisionDepth,
    float CollisionHeight,
    float FacadeWidth,
    float WingFrontInset,
    float RearWingInset,
    float WingInnerHalfWidth,
    float WingOuterHalfWidth,
    float SideHalfWidth,
    float SideFrontInset,
    float SideRearInset,
    float InteriorWidth,
    float InteriorDepth,
    float DoorWidth,
    float DoorHeight);

internal enum JianghaiSolidBuildingProfile
{
    Hall,
    Shop,
    Gate,
    Chimney
}

internal static class JianghaiGameplayCollisionContract
{
    public const string AuthoredDensityDistrictRole = "authored_density_building";
    public const string AuthoredDensityCollisionRole = "building_shell";
    public const int ExpectedDensitySourceCount = 50;
    public const int ExpectedSolidSourceCount = 57;
    public const int ExpectedEnterableSourceCount = 12;
    public const int EnterableShapesPerSource = 23;
    public const int ExpectedAuthoredSourceCount =
        ExpectedDensitySourceCount
        + ExpectedSolidSourceCount
        + ExpectedEnterableSourceCount;
    public const int ExpectedEnterableShapeCount =
        ExpectedEnterableSourceCount * EnterableShapesPerSource;
    public const int ExpectedAuthoredShapeCount =
        ExpectedDensitySourceCount
        + ExpectedSolidSourceCount
        + ExpectedEnterableShapeCount;

    private static readonly string[] DensitySourceNameValues =
    {
        "JianghaiDensity_EastEdge01",
        "JianghaiDensity_EastEdge02",
        "JianghaiDensity_EastEdge03",
        "JianghaiDensity_EastEdge04",
        "JianghaiDensity_EastEdge05",
        "JianghaiDensity_EastEdge06",
        "JianghaiDensity_EastEdge07",
        "JianghaiDensity_EastEdge08",
        "JianghaiDensity_EastInfill00",
        "JianghaiDensity_EastInfill01",
        "JianghaiDensity_EastInfill02",
        "JianghaiDensity_EastInfill03",
        "JianghaiDensity_EastInfill04",
        "JianghaiDensity_EastInfill05",
        "JianghaiDensity_EastInfill06",
        "JianghaiDensity_EastInfill07",
        "JianghaiDensity_EastInfill08",
        "JianghaiDensity_NorthWall01",
        "JianghaiDensity_NorthWall02",
        "JianghaiDensity_NorthWall03",
        "JianghaiDensity_NorthWall04",
        "JianghaiDensity_NorthWall05",
        "JianghaiDensity_NorthWall06",
        "JianghaiDensity_NorthWall07",
        "JianghaiDensity_NorthWall08",
        "JianghaiDensity_SouthWall01",
        "JianghaiDensity_SouthWall02",
        "JianghaiDensity_SouthWall03",
        "JianghaiDensity_SouthWall04",
        "JianghaiDensity_SouthWall05",
        "JianghaiDensity_SouthWall06",
        "JianghaiDensity_SouthWall07",
        "JianghaiDensity_SouthWall08",
        "JianghaiDensity_WestEdge01",
        "JianghaiDensity_WestEdge02",
        "JianghaiDensity_WestEdge03",
        "JianghaiDensity_WestEdge04",
        "JianghaiDensity_WestEdge05",
        "JianghaiDensity_WestEdge06",
        "JianghaiDensity_WestEdge07",
        "JianghaiDensity_WestEdge08",
        "JianghaiDensity_WestInfill00",
        "JianghaiDensity_WestInfill01",
        "JianghaiDensity_WestInfill02",
        "JianghaiDensity_WestInfill03",
        "JianghaiDensity_WestInfill04",
        "JianghaiDensity_WestInfill05",
        "JianghaiDensity_WestInfill06",
        "JianghaiDensity_WestInfill07",
        "JianghaiDensity_WestInfill08"
    };
    private static readonly string[] EnterableSourceNameValues =
    {
        "EastGateRow00",
        "EastPhotoHouse",
        "EastTeaHouse",
        "NorthwestGateHouse",
        "OuterEastMidResidence",
        "OuterWestSquareResidence",
        "WeatheredRollerShop00",
        "WeatheredRollerShop01",
        "WeatheredRollerShop02",
        "WeatheredRollerShop03",
        "WestMarketResidence",
        "WestMedicineRow01"
    };
    private static readonly string[] SolidSourceNameValues =
    {
        "EastGateRow02",
        "EastHarborResidence",
        "EastHardwareHouse",
        "EastHardwareRow02",
        "EastMarketResidence",
        "EastMarketRow01",
        "EastOldHotel",
        "EastPhotoRow00",
        "EastPhotoRow02",
        "EastSquareRow01",
        "EastSquareRow02",
        "EastTeaRow01",
        "FarEastResidence",
        "FarEastSouthResidence",
        "FarWestNorthResidence",
        "FarWestResidence",
        "FactoryChimney",
        "JianghaiCleared_FactoryAdmin",
        "JianghaiCleared_FactoryOfficeEast",
        "JianghaiCleared_FactoryOfficeWest",
        "JianghaiCleared_FactoryWorkshopEast",
        "JianghaiCleared_FactoryWorkshopWest",
        "JianghaiCleared_MarketRearHouseEast",
        "JianghaiCleared_MarketRearHouseWest",
        "JianghaiCleared_MarketShop00",
        "JianghaiCleared_MarketShop01",
        "JianghaiCleared_MarketShop02",
        "JianghaiCleared_MarketShop03",
        "JianghaiCleared_MarketShop04",
        "JianghaiCleared_PawnshopWestHouse00",
        "JianghaiCleared_PawnshopWestHouse01",
        "JianghaiExpansion_PawnshopBackdrop",
        "NortheastGateHouse",
        "OuterEastHarborResidence",
        "OuterEastMarketResidence",
        "OuterEastNorthResidence",
        "OuterEastTeaResidence",
        "OuterNortheastResidence",
        "OuterNorthwestResidence",
        "OuterWestClockResidence",
        "OuterWestHarborResidence",
        "OuterWestMarketResidence",
        "OuterWestNorthResidence",
        "OuterWestSouthResidence",
        "WestClockHouse",
        "WestClockRow01",
        "WestGateRow01",
        "WestGateRow02",
        "WestHarborResidence",
        "WestMarketRow01",
        "WestMedicineHouse",
        "WestMedicineRow02",
        "WestSquareRow01",
        "WestSquareRow02",
        "WestTeaWarehouse",
        "WestTheatreHouse",
        "WestTheatreRow02"
    };
    private static readonly HashSet<string> ExplicitShopSourceNames = new(
        new[]
        {
            "JianghaiCleared_FactoryWorkshopEast",
            "JianghaiCleared_FactoryWorkshopWest",
            "JianghaiCleared_MarketShop01",
            "JianghaiCleared_MarketShop03"
        },
        StringComparer.Ordinal);
    private static readonly string[] AuthoredSourceNameValues = DensitySourceNameValues
        .Concat(SolidSourceNameValues)
        .Concat(EnterableSourceNameValues)
        .ToArray();
    private static readonly HashSet<string> DensitySourceNames = new(
        DensitySourceNameValues,
        StringComparer.Ordinal);
    private static readonly HashSet<string> EnterableSourceNames = new(
        EnterableSourceNameValues,
        StringComparer.Ordinal);
    private static readonly HashSet<string> SolidSourceNames = new(
        SolidSourceNameValues,
        StringComparer.Ordinal);
    private static readonly HashSet<string> AuthoredSourceNames = new(
        AuthoredSourceNameValues,
        StringComparer.Ordinal);

    public static IReadOnlyList<string> ExpectedAuthoredSourceNames
        => AuthoredSourceNameValues;

    public static IReadOnlyList<string> ExpectedDensitySourceNames
        => DensitySourceNameValues;

    public static IReadOnlyList<string> ExpectedEnterableSourceNames
        => EnterableSourceNameValues;

    public static IReadOnlyList<string> ExpectedSolidSourceNames
        => SolidSourceNameValues;

    public static bool IsExpectedDensitySource(string sourceName)
        => DensitySourceNames.Contains(sourceName);

    public static bool IsExpectedEnterableSource(string sourceName)
        => EnterableSourceNames.Contains(sourceName);

    public static bool IsExpectedSolidSource(string sourceName)
        => SolidSourceNames.Contains(sourceName);

    public static bool IsExpectedAuthoredSource(string sourceName)
        => AuthoredSourceNames.Contains(sourceName);

    public static JianghaiSolidBuildingProfile SolidProfileFor(string sourceName)
    {
        if (!SolidSourceNames.Contains(sourceName))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceName),
                sourceName,
                "Unknown Jianghai authored solid building.");
        }
        if (sourceName == "FactoryChimney")
        {
            return JianghaiSolidBuildingProfile.Chimney;
        }
        if (ExplicitShopSourceNames.Contains(sourceName)
            || sourceName.Sum(character => character) % 5 == 0)
        {
            return JianghaiSolidBuildingProfile.Shop;
        }
        return sourceName.Contains("Gate", StringComparison.Ordinal)
            || sourceName.Sum(character => character) % 4 == 0
            ? JianghaiSolidBuildingProfile.Gate
            : JianghaiSolidBuildingProfile.Hall;
    }

    public static bool TryGetEnterableRoom(
        string sourceName,
        out JianghaiEnterableRoomContract room)
    {
        room = sourceName switch
        {
            "WeatheredRollerShop00" => new(
                0.77f, 9.84f, 4.55f, 3.69f, 2.80f,
                1.444f, 4.792f, 1.85f, 4.20f,
                5.023f, 1.934f, 4.299f, 7.20f, 4.25f, 1.58f, 2.48f),
            "WeatheredRollerShop01" => new(
                0.80f, 10.14f, 4.72f, 3.80f, 2.80f,
                1.507f, 5.000f, 1.90f, 4.40f,
                5.242f, 2.022f, 4.487f, 7.20f, 4.42f, 1.58f, 2.48f),
            "WeatheredRollerShop02" or "WeatheredRollerShop03" => new(
                0.78f, 9.92f, 4.62f, 3.75f, 2.80f,
                1.475f, 4.896f, 1.90f, 4.30f,
                5.132f, 1.980f, 4.395f, 7.20f, 4.32f, 1.58f, 2.48f),
            "EastPhotoHouse" => new(
                1.29f, 12.70f, 7.47f, 4.61f, 4.00f,
                2.376f, 7.886f, 2.70f, 6.20f,
                7.447f, 3.186f, 7.076f, 7.20f, 6.40f, 1.58f, 2.48f),
            "EastGateRow00" => new(
                1.290f, 12.700f, 7.470f, 4.610f, 4.000f,
                2.376f, 7.886f, 2.700f, 6.200f,
                7.447f, 3.186f, 7.076f, 7.20f, 6.40f, 1.58f, 2.48f),
            "EastTeaHouse" => new(
                1.32f, 13.11f, 7.67f, 4.68f, 4.00f,
                2.422f, 8.041f, 2.70f, 6.35f,
                7.593f, 3.247f, 7.217f, 7.20f, 6.40f, 1.58f, 2.48f),
            "NorthwestGateHouse" => new(
                1.366f, 13.447f, 7.909f, 4.881f, 4.235f,
                2.516f, 8.350f, 2.859f, 6.565f,
                7.885f, 3.373f, 7.492f, 7.20f, 6.40f, 1.58f, 2.48f),
            "WestMedicineRow01" => new(
                1.108f, 12.103f, 6.414f, 5.109f, 3.812f,
                2.040f, 6.772f, 2.573f, 5.909f,
                7.097f, 2.736f, 6.076f, 7.20f, 6.114f, 1.58f, 2.48f),
            "WestMarketResidence" => new(
                1.391f, 13.696f, 8.056f, 4.972f, 4.314f,
                2.562f, 8.505f, 2.912f, 6.686f,
                8.031f, 3.436f, 7.631f, 7.20f, 6.40f, 1.58f, 2.48f),
            "OuterEastMidResidence" or "OuterWestSquareResidence" => new(
                1.341f, 13.198f, 7.763f, 4.791f, 4.157f,
                2.469f, 8.196f, 2.806f, 6.443f,
                7.739f, 3.311f, 7.354f, 7.20f, 6.40f, 1.58f, 2.48f),
            _ => default
        };
        return room.CollisionDepth > 0.0f;
    }
}
