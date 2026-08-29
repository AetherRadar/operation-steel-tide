"""Deterministic named-anchor and perimeter layout for Jianghai's Chinese district."""

OLD_URBAN_TARGETS = (
    "EastGateRow00", "EastGateRow02", "EastHarborResidence", "EastHardwareHouse",
    "EastHardwareRow02", "EastMarketResidence", "EastMarketRow01", "EastOldHotel",
    "EastPhotoHouse", "EastPhotoRow00", "EastPhotoRow02", "EastSquareRow01",
    "EastSquareRow02", "EastTeaHouse", "EastTeaRow01", "FarEastResidence",
    "FarEastSouthResidence", "FarWestNorthResidence", "FarWestResidence",
    "JianghaiCleared_FactoryAdmin", "JianghaiCleared_FactoryOfficeEast",
    "JianghaiCleared_FactoryOfficeWest", "JianghaiCleared_FactoryWorkshopEast",
    "JianghaiCleared_FactoryWorkshopWest", "JianghaiCleared_MarketRearHouseEast",
    "JianghaiCleared_MarketRearHouseWest", "JianghaiCleared_MarketShop00",
    "JianghaiCleared_MarketShop01", "JianghaiCleared_MarketShop02",
    "JianghaiCleared_MarketShop03", "JianghaiCleared_MarketShop04",
    "JianghaiCleared_PawnshopWestHouse00", "JianghaiCleared_PawnshopWestHouse01",
    "JianghaiExpansion_PawnshopBackdrop", "NortheastGateHouse", "NorthwestGateHouse",
    "OuterEastHarborResidence", "OuterEastMarketResidence", "OuterEastMidResidence",
    "OuterEastNorthResidence", "OuterEastTeaResidence", "OuterNortheastResidence",
    "OuterNorthwestResidence", "OuterWestClockResidence", "OuterWestHarborResidence",
    "OuterWestMarketResidence", "OuterWestNorthResidence", "OuterWestSouthResidence",
    "OuterWestSquareResidence", "WeatheredRollerShop00", "WeatheredRollerShop01",
    "WeatheredRollerShop02", "WeatheredRollerShop03", "WestClockHouse",
    "WestGateRow01", "WestGateRow02", "WestHarborResidence", "WestMarketResidence",
    "WestMarketRow01", "WestMedicineHouse", "WestMedicineRow01", "WestSquareRow01",
    "WestSquareRow02", "WestTeaWarehouse", "WestTheatreHouse",
    "JianghaiCleared_PawnshopStorefront",
)

SHOP_TARGETS = {
    "JianghaiCleared_FactoryWorkshopEast", "JianghaiCleared_FactoryWorkshopWest",
    "JianghaiCleared_MarketShop01", "JianghaiCleared_MarketShop03",
    "WeatheredRollerShop00", "WeatheredRollerShop01", "WeatheredRollerShop02",
    "WeatheredRollerShop03", "WestMedicineRow01",
}

QUATERNIUS_DENSITY_MESHES = {
    "quaternius_large": "JianghaiDensity_QuaterniusBuilding1Large_LOD",
    "quaternius_big": "JianghaiDensity_QuaterniusBuilding3Big_LOD",
    "quaternius_building4": "JianghaiDensity_QuaterniusBuilding4_LOD",
    "quaternius_house2": "JianghaiDensity_QuaterniusHouse2_LOD",
}

PROFILE_BASE_SCALE = {
    "chinese_hall": 1.28,
    "chinese_shop": 1.20,
    "chinese_gate": 1.00,
    "quaternius_large": 1.90,
    "quaternius_big": 1.55,
    "quaternius_building4": 1.60,
    "quaternius_house2": 3.00,
}

# Mirrors JianghaiExtractionSpawnLayout after Godot (x, z) -> Blender (x, -y).
JIANGHAI_DEPLOYMENT_POINTS = (
    (8.0, -48.0, 0.0),
    (-148.0, 198.0, 0.0), (148.0, 198.0, 0.0),
    (-148.0, -72.0, 0.0), (148.0, -72.0, 0.0),
)

# Edge04/06 flank side lanes while Edge05 is recessed 28 m toward town.
DENSITY_BUILDING_LAYOUT = (
    ("SouthWall01", "chinese_shop", (-103.0, -82.2, 0.03), 0.0, 1.10),
    ("SouthWall02", "chinese_hall", (-78.0, -82.0, 0.03), 0.0, 0.96),
    ("SouthWall03", "quaternius_large", (-51.0, -82.1, 0.03), 0.0, 1.08),
    ("SouthWall04", "chinese_shop", (-25.0, -96.0, 0.03), 0.0, 1.14),
    ("SouthWall05", "chinese_hall", (25.0, -96.0, 0.03), 0.0, 1.02),
    ("SouthWall06", "chinese_shop", (51.0, -82.0, 0.03), 0.0, 1.12),
    ("SouthWall07", "quaternius_building4", (78.0, -82.2, 0.03), 0.0, 0.98),
    ("SouthWall08", "quaternius_house2", (100.0, -82.0, 0.03), 0.0, 1.10),
    ("NorthWall01", "chinese_shop", (-105.0, 194.2, 0.03), 180.0, 1.14),
    ("NorthWall02", "chinese_hall", (-78.0, 194.0, 0.03), 180.0, 0.98),
    ("NorthWall03", "quaternius_large", (-51.0, 194.1, 0.03), 180.0, 1.06),
    ("NorthWall04", "chinese_shop", (-25.0, 194.0, 0.03), 180.0, 1.12),
    ("NorthWall05", "chinese_hall", (25.0, 194.1, 0.03), 180.0, 1.00),
    ("NorthWall06", "chinese_shop", (51.0, 194.0, 0.03), 180.0, 1.16),
    ("NorthWall07", "quaternius_big", (78.0, 194.2, 0.03), 180.0, 1.04),
    ("NorthWall08", "quaternius_house2", (105.0, 194.0, 0.03), 180.0, 1.10),
    ("WestEdge01", "chinese_shop", (-154.2, -40.0, 0.03), 90.0, 1.12),
    ("WestEdge02", "quaternius_house2", (-154.0, -18.0, 0.03), 90.0, 0.98),
    ("WestEdge03", "quaternius_large", (-154.1, 4.0, 0.03), 90.0, 1.08),
    ("WestEdge04", "chinese_gate", (-154.0, 32.0, 0.03), 90.0, 0.92),
    ("WestEdge05", "chinese_shop", (-127.0, 60.0, 0.03), 90.0, 1.02),
    ("WestEdge06", "chinese_gate", (-154.0, 88.0, 0.03), 90.0, 0.94),
    ("WestEdge07", "chinese_shop", (-154.1, 116.0, 0.03), 90.0, 1.10),
    ("WestEdge08", "chinese_hall", (-154.0, 150.0, 0.03), 90.0, 1.08),
    ("EastEdge01", "chinese_shop", (154.2, -40.0, 0.03), -90.0, 1.10),
    ("EastEdge02", "quaternius_house2", (154.0, -18.0, 0.03), -90.0, 0.96),
    ("EastEdge03", "quaternius_large", (154.1, 4.0, 0.03), -90.0, 1.06),
    ("EastEdge04", "chinese_gate", (154.0, 32.0, 0.03), -90.0, 0.92),
    ("EastEdge05", "chinese_shop", (127.0, 60.0, 0.03), -90.0, 1.02),
    ("EastEdge06", "chinese_gate", (154.0, 88.0, 0.03), -90.0, 0.94),
    ("EastEdge07", "chinese_shop", (154.1, 116.0, 0.03), -90.0, 1.12),
    ("EastEdge08", "chinese_hall", (154.0, 150.0, 0.03), -90.0, 1.04),
    ("WestInfill00", "chinese_hall", (-118.0, -72.0, 0.03), 90.0, 1.04),
    ("WestInfill01", "chinese_shop", (-116.0, -20.0, 0.03), 90.0, 1.12),
    ("WestInfill02", "quaternius_big", (-116.0, 40.0, 0.03), 90.0, 1.00),
    ("WestInfill03", "chinese_shop", (-134.0, 124.0, 0.03), 90.0, 1.10),
    ("WestInfill04", "quaternius_building4", (-117.0, 150.0, 0.03), 90.0, 1.06),
    ("EastInfill00", "chinese_hall", (116.0, -74.0, 0.03), -90.0, 1.06),
    ("EastInfill01", "chinese_shop", (116.0, -22.0, 0.03), -90.0, 1.10),
    ("EastInfill02", "quaternius_big", (116.0, 36.0, 0.03), -90.0, 1.02),
    ("EastInfill03", "chinese_shop", (116.0, 124.0, 0.03), -90.0, 1.14),
    ("EastInfill04", "quaternius_building4", (116.0, 150.0, 0.03), -90.0, 1.04),
)
