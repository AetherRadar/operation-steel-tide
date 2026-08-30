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
    "WeatheredRollerShop03", "EastPhotoHouse", "EastTeaHouse", "EastGateRow00",
    "NorthwestGateHouse", "WestMedicineRow01", "WestMarketResidence",
    "OuterEastMidResidence", "OuterWestSquareResidence",
}

# Full-resolution ground-floor arcade shops selected for real, furnished interiors.
# The twelve anchors cover the avenue, gate, market, and two outer residential
# lanes.  They share one low-cost modular interior liner at runtime, so expanding
# usable street frontage does not multiply unique meshes or texture memory.
ENTERABLE_RESIDENCE_LAYOUT = (
    ("WeatheredRollerShop00", "family_shop"),
    ("WeatheredRollerShop01", "family_shop"),
    ("WeatheredRollerShop02", "tea_house"),
    ("WeatheredRollerShop03", "repair_shop"),
    ("EastPhotoHouse", "family_home"),
    ("EastTeaHouse", "tea_house"),
    ("EastGateRow00", "family_shop"),
    ("NorthwestGateHouse", "family_home"),
    ("WestMedicineRow01", "repair_shop"),
    ("WestMarketResidence", "family_home"),
    ("OuterEastMidResidence", "family_home"),
    ("OuterWestSquareResidence", "family_home"),
)

# Reviewed one-storey interior envelopes in Godot metres.  These values stop
# gameplay collision at the actual wall faces rather than at ornamental eaves
# included in each full visual AABB.  Width is the complete interior span.
ENTERABLE_COLLISION_LAYOUT = {
    "WeatheredRollerShop00": (
        0.77, 9.84, 4.55, 3.69, 2.80, 1.444, 4.792, 1.85, 4.20, 5.023, 1.934, 4.299,
    ),
    "WeatheredRollerShop01": (
        0.80, 10.14, 4.72, 3.80, 2.80, 1.507, 5.000, 1.90, 4.40, 5.242, 2.022, 4.487,
    ),
    "WeatheredRollerShop02": (
        0.78, 9.92, 4.62, 3.75, 2.80, 1.475, 4.896, 1.90, 4.30, 5.132, 1.980, 4.395,
    ),
    "WeatheredRollerShop03": (
        0.78, 9.92, 4.62, 3.75, 2.80, 1.475, 4.896, 1.90, 4.30, 5.132, 1.980, 4.395,
    ),
    "EastPhotoHouse": (
        1.29, 12.70, 7.47, 4.61, 4.00, 2.376, 7.886, 2.70, 6.20, 7.447, 3.186, 7.076,
    ),
    "EastTeaHouse": (
        1.32, 13.11, 7.67, 4.68, 4.00, 2.422, 8.041, 2.70, 6.35, 7.593, 3.247, 7.217,
    ),
    "EastGateRow00": (
        1.290, 12.700, 7.470, 4.610, 4.000, 2.376, 7.886, 2.700, 6.200, 7.447, 3.186, 7.076,
    ),
    "NorthwestGateHouse": (
        1.366, 13.447, 7.909, 4.881, 4.235, 2.516, 8.350, 2.859, 6.565, 7.885, 3.373, 7.492,
    ),
    "WestMedicineRow01": (
        1.108, 12.103, 6.414, 5.109, 3.812, 2.040, 6.772, 2.573, 5.909, 7.097, 2.736, 6.076,
    ),
    "WestMarketResidence": (
        1.391, 13.696, 8.056, 4.972, 4.314, 2.562, 8.505, 2.912, 6.686, 8.031, 3.436, 7.631,
    ),
    "OuterEastMidResidence": (
        1.341, 13.198, 7.763, 4.791, 4.157, 2.469, 8.196, 2.806, 6.443, 7.739, 3.311, 7.354,
    ),
    "OuterWestSquareResidence": (
        1.341, 13.198, 7.763, 4.791, 4.157, 2.469, 8.196, 2.806, 6.443, 7.739, 3.311, 7.354,
    ),
}

# Four roller shops historically carried Euler values while remaining in
# quaternion mode, so their visible transforms ignored the intended yaw.
ENTERABLE_YAW_DEGREES = {
    "WeatheredRollerShop00": 90.0,
    "WeatheredRollerShop01": -90.0,
    "WeatheredRollerShop02": 90.0,
    "WeatheredRollerShop03": -90.0,
    "EastPhotoHouse": -90.0,
    "EastTeaHouse": -90.0,
    "EastGateRow00": -90.0,
    "NorthwestGateHouse": 90.0,
    "WestMedicineRow01": 90.0,
    "WestMarketResidence": 90.0,
    "OuterEastMidResidence": 180.0,
    "OuterWestSquareResidence": 0.0,
}

# Shop01's dormant yaw made its original centre appear road-safe.  Once the
# intended side-facing transform is active, this reviewed offset restores the
# same 1.35 m clearance from the southern cross-street centreline.
ENTERABLE_POSITION_OVERRIDES = {
    "WeatheredRollerShop01": (31.0, 13.65, 0.035),
}

QUATERNIUS_DENSITY_MESHES = {
    "quaternius_large": "JianghaiDensity_QuaterniusBuilding1Large_LOD",
    "quaternius_big": "JianghaiDensity_QuaterniusBuilding3Big_LOD",
    "quaternius_building4": "JianghaiDensity_QuaterniusBuilding4_LOD",
    "quaternius_house2": "JianghaiDensity_QuaterniusHouse2_LOD",
}

# The Quaternius density shells use scalar-only, texture-free materials.  Keep
# their exact per-face palette in one active corner-color layer, leaving one
# opaque material/surface per shared profile mesh.  This preserves the authored
# colors and profile roughness while preventing every density instance from
# submitting four to seven material surfaces to the four-split sun shadow pass.
DENSITY_COLOR0_VERSION = 1
DENSITY_COLOR0_ATTRIBUTE = "COLOR_0"
DENSITY_COLOR0_PROFILE_MATERIALS = {
    "quaternius_large": "JianghaiDensity_QuaterniusLarge_COLOR0",
    "quaternius_big": "JianghaiDensity_QuaterniusBig_COLOR0",
    "quaternius_building4": "JianghaiDensity_QuaterniusBuilding4_COLOR0",
    "quaternius_house2": "JianghaiDensity_QuaterniusHouse2_COLOR0",
}
DENSITY_COLOR0_PROFILE_ROUGHNESS = {
    "quaternius_large": 0.82,
    "quaternius_big": 0.84,
    "quaternius_building4": 0.86,
    "quaternius_house2": 0.88,
}
DENSITY_COLOR0_PROFILE_SOURCE_SURFACES = {
    "quaternius_large": 7,
    "quaternius_big": 6,
    "quaternius_building4": 7,
    "quaternius_house2": 4,
}
DENSITY_COLOR0_INFILL_SUFFIXES = (
    "WestInfill05",
    "WestInfill06",
    "WestInfill07",
    "WestInfill08",
    "EastInfill05",
    "EastInfill06",
    "EastInfill07",
    "EastInfill08",
)

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
    # A second sparse inner ring closes long skyline gaps with eight shared,
    # low-poly Quaternius meshes.  The pairs stop short of both cross streets
    # and remain more than 24 m from every deployment point.
    ("WestInfill05", "quaternius_large", (-92.0, -65.0, 0.03), 90.0, 1.00),
    ("WestInfill06", "quaternius_big", (-92.0, 52.0, 0.03), 90.0, 1.00),
    ("WestInfill07", "quaternius_building4", (-92.0, 78.0, 0.03), 90.0, 1.00),
    ("WestInfill08", "quaternius_house2", (-92.0, 158.0, 0.03), 90.0, 1.00),
    ("EastInfill05", "quaternius_large", (92.0, -65.0, 0.03), -90.0, 1.00),
    ("EastInfill06", "quaternius_big", (92.0, 52.0, 0.03), -90.0, 1.00),
    ("EastInfill07", "quaternius_building4", (92.0, 78.0, 0.03), -90.0, 1.00),
    ("EastInfill08", "quaternius_house2", (92.0, 158.0, 0.03), -90.0, 1.00),
)
