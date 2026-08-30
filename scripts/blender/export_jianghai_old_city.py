"""Export the hand-authored Jianghai Old City Blender scene to runtime GLB."""

from __future__ import annotations

import json
from math import atan2, cos, hypot, isfinite, pi, radians, sin, tan
from pathlib import Path
import re
import sys
import tempfile

import bmesh
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from jianghai_chinese_district_layout import (
    DENSITY_BUILDING_LAYOUT,
    DENSITY_COLOR0_ATTRIBUTE,
    DENSITY_COLOR0_INFILL_SUFFIXES,
    DENSITY_COLOR0_PROFILE_MATERIALS,
    DENSITY_COLOR0_PROFILE_ROUGHNESS,
    DENSITY_COLOR0_PROFILE_SOURCE_SURFACES,
    ENTERABLE_RESIDENCE_LAYOUT,
    PROFILE_BASE_SCALE,
    QUATERNIUS_DENSITY_MESHES,
)
from jianghai_density_color0 import (
    consolidate_density_profile_mesh,
    validate_density_color0_scene,
)
from jianghai_enterable_residences import (
    ENTERABLE_MESH_SHARE_GROUPS,
    apply_enterable_residences,
)
from jianghai_enterable_interior_liners import (
    EXPECTED_TRIANGLES as INTERIOR_LINER_EXPECTED_TRIANGLES,
    LINER_OBJECT_PREFIX,
    LINER_VISIBILITY_METERS,
)


REPO_ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = REPO_ROOT / "source_art" / "world" / "jianghai_old_city" / "jianghai_old_city.blend"
GLB_PATH = REPO_ROOT / "assets" / "models" / "jianghai_old_city" / "jianghai_old_city.glb"
REFINERY_DOOR_GLB_PATH = (
    REPO_ROOT / "assets" / "models" / "jianghai_old_city" / "rollershutter_window_03.glb"
)
MAX_RUNTIME_TEXTURE_SIZE = 512
MAX_DETAIL_TEXTURE_SIZE = 512
MAX_SMALL_FURNITURE_TEXTURE_SIZE = 256
MAX_RUNTIME_GLB_SIZE_BYTES = 70_000_000
MAX_RUNTIME_INSTANCE_TRIANGLES = 3_200_000
GROUND_INSTANCE_COUNT = 1
GROUND_EXPECTED_TRIANGLES = 168_480
GROUND_EXPECTED_VERTICES = 84_960
GROUND_EXPECTED_WELDED_VERTICES = 2_573
GROUND_EXPECTED_BOUNDARY_EDGES = 1_440
GROUND_EXPECTED_BOUNDARY_COMPONENTS = 2
GROUND_BOUNDARY_SKIRT_DEPTH = 1.50
GROUND_COMPOSITE_ANGLE_SAMPLES = 720
GROUND_COMPOSITE_RADIAL_SAMPLES = 116
GROUND_SAFE_RADIUS = 220.0
GROUND_RELIEF_TRANSITION_END_RADIUS = 260.0
GROUND_SAFE_TOP_MAXIMUM = -0.12
GROUND_RELIEF_BASELINE = -0.35
GROUND_FOUNDATION_MARGIN = 8.0
GROUND_FOUNDATION_RELIEF_END_DISTANCE = 28.0
GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE = 420.0
MOUNTAIN_EXPECTED_TRIANGLES = 14_000
COAST_UV_LAYER_NAME = "CoastGroundUV"
COAST_UV_TILE_SIZE_LOCAL = 7.0
COAST_UV_ROUNDTRIP_TOLERANCE = 0.000012
COAST_UV_MAPPING_METHOD = (
    "continuous affine Cartesian world XY over 7m Gravel Floor 03 PBR"
)
COAST_SURFACE_MATERIAL_NAME = "JianghaiCoastGravelFloorPBR"
COAST_BASE_COLOR_FACTOR = (0.92, 0.78, 0.62, 1.0)
GRAVEL_SURFACE_SOURCE_MD5S = (
    "diffuse=d86981602e03f8f1deeccc5e37a14468; "
    "normal=864d073353dcfbbb0a507cbc07e250b7; "
    "roughness=698b4d00999fa3108d4abc8584dde936"
)
GRAVEL_SURFACE_IMAGE_MD5S = {
    "d86981602e03f8f1deeccc5e37a14468",
    "864d073353dcfbbb0a507cbc07e250b7",
    "698b4d00999fa3108d4abc8584dde936",
}
COAST_INNER_TRANSITION_END_Y = 6.0
COAST_OUTER_TARGET_Y = -49.0
COAST_LATERAL_BASE_FACTOR = 1.30
COAST_OUTER_FLARE_FACTOR = 1.70
COAST_OUTER_HEIGHT_LATERAL_FACTOR = 0.50
COAST_OUTER_HEIGHT_BLEND_SPAN = 14.0
COAST_ENVELOPE_TOP_OFFSET = 0.005
COAST_ENVELOPE_RELIEF_FACTOR = 0.03
NORTH_EDGE_OBJECT_NAMES = (
    "CentralAvenue",
    "CentralAvenueCurbE",
    "CentralAvenueCurbW",
)
NORTH_EDGE_BLEND_LENGTH = 8.0
NORTH_EDGE_TARGET_TOP = -0.13
NORTH_EDGE_UPSTREAM_TOPS = {
    "CentralAvenue": -0.035,
    "CentralAvenueCurbE": 0.18,
    "CentralAvenueCurbW": 0.18,
}
NORTH_EDGE_END_THICKNESSES = {
    "CentralAvenue": 0.12,
    "CentralAvenueCurbE": 0.18,
    "CentralAvenueCurbW": 0.18,
}
NORTH_EDGE_CAMERA_ORIGIN = Vector((-118.0, 1.65, -207.0))
NORTH_EDGE_CAMERA_TARGET = Vector((-25.0, 12.0, -340.0))
NORTH_EDGE_CAMERA_FOV = 68.0
SOUTH_GROUND_CAMERA_ORIGIN = Vector((112.0, 1.65, 86.0))
SOUTH_GROUND_CAMERA_TARGET = Vector((18.0, 11.0, 300.0))
SOUTH_GROUND_CAMERA_FOV = 68.0
LAYOUT_POSITION_TOLERANCE = 0.00001
GROUND_LAYOUT = (
    ((278.0, 60.0), 5.85, -0.025),
    ((197.0, 257.0), 6.05, 0.045),
    ((0.0, 338.0), 5.90, -0.035),
    ((-197.0, 257.0), 6.10, 0.030),
    ((-278.0, 60.0), 5.85, -0.020),
    ((-197.0, -137.0), 6.00, 0.040),
    ((0.0, -218.0), 5.95, -0.030),
    ((197.0, -137.0), 6.05, 0.025),
)
MOUNTAIN_LAYOUT = tuple(
    (
        (cos(index * pi / 3.0) * 630.0,
         60.0 + sin(index * pi / 3.0) * 630.0),
        (285.0, 278.0, 282.0, 276.0, 284.0, 280.0)[index],
        (0.0, 90.0, 180.0, 270.0, 0.0, 90.0)[index] * pi / 180.0,
        (17.8, 19.8, 21.8, 17.8, 19.8, 21.8)[index],
        "inner",
    )
    for index in range(6)
) + tuple(
    (
        (cos((index * 60.0 + 30.0) * pi / 180.0) * 780.0,
         60.0 + sin((index * 60.0 + 30.0) * pi / 180.0) * 780.0),
        (310.0, 300.0, 320.0, 305.0, 315.0, 295.0)[index],
        (90.0, 180.0, 270.0, 0.0, 90.0, 180.0)[index] * pi / 180.0,
        (19.8, 21.8, 17.8, 21.8, 17.8, 19.8)[index],
        "outer",
    )
    for index in range(6)
)
DETAIL_TEXTURE_TOKENS = (
    "barrel_03",
    "coffeecart_01",
    "concrete_road_barrier",
    "exterior_aircon_unit",
    "hand_truck",
    "modular_urban_apartments_facade",
    "old_military_crate",
    "plastic_crate_02",
    "rollershutter_window_03",
    "security_camera_01",
    "television_02",
    "trashbag",
    "utility_box_01",
    "wicker_basket_01",
)
SMALL_FURNITURE_TEXTURE_TOKENS = (
    "chinese_stool",
    "chinese_tea_table",
)
RUNTIME_EMISSION_STRENGTHS = {
    "JianghaiNeonGold": 0.75,
    "JianghaiNeonRed": 0.88,
    "JianghaiNeonTeal": 0.80,
    "JianghaiLampGlass": 1.65,
    "JianghaiTerminalScreenRed": 4.00,
}
FLOATING_MARKET_SIGN_OBJECTS = (
    "OldCityMarketSignBacking",
    "OldCityMarketSignText",
    "OldCityMarketBuySignBacking",
    "OldCityMarketBuySignText",
    "OldCityMarketPawnSignBacking",
    "OldCityMarketPawnSignText",
)
RETIRED_CUSTOM_PROPERTY_KEYS = (
    "blenderkit_old_brick_factory",
)
RUINED_FACTORY_OBJECTS = (
    "RedStarFactoryMainBuilding",
    "RedStarLoadingBayWest",
    "RedStarLoadingBayEast",
)
FACTORY_BUILDING_LAYOUT = (
    ("JianghaiCleared_FactoryOfficeWest", "JianghaiCleared_MarketShop00", (66.0, -7.0, 0.04), (0.48, 0.58, 0.78)),
    ("JianghaiCleared_FactoryWorkshopWest", "JianghaiCleared_MarketShop01", (75.5, -7.0, 0.04), (0.65, 0.80, 0.92)),
    ("JianghaiCleared_FactoryAdmin", "JianghaiCleared_MarketShop02", (85.5, -7.0, 0.04), (0.52, 0.62, 0.88)),
    ("JianghaiCleared_FactoryWorkshopEast", "JianghaiCleared_MarketShop03", (95.5, -7.0, 0.04), (0.65, 0.82, 0.96)),
    ("JianghaiCleared_FactoryOfficeEast", "JianghaiCleared_MarketShop04", (105.0, -7.0, 0.04), (0.48, 0.58, 0.82)),
)
PAWNSHOP_LEGACY_VISIBLE_NAMES = (
    "GuangchangPawnshopSignBacking",
    "GuangchangPawnshopDangPlaqueBacking",
    "PawnshopGatePierL",
    "PawnshopGatePierR",
    "PawnshopGatePierCapL",
    "PawnshopGatePierCapR",
)
PAWNSHOP_LEGACY_WALL_PREFIXES = (
    "PawnshopSouthEast_",
    "PawnshopSouthEastCap_",
    "PawnshopSouthWest_",
    "PawnshopSouthWestCap_",
)
DENSITY_OBJECT_PREFIX = "JianghaiDensity_"
DENSITY_HALL_MESH_NAME = "JianghaiDensity_ChineseTempleHall_LOD"
DENSITY_SHOP_MESH_NAME = "JianghaiDensity_ChineseArcadeShop_LOD"
DENSITY_GATE_MESH_NAME = "JianghaiDensity_ChineseGateHouse_LOD"
DENSITY_QUATERNIUS_LARGE_MESH_NAME = "JianghaiDensity_QuaterniusBuilding1Large_LOD"
DENSITY_QUATERNIUS_BIG_MESH_NAME = "JianghaiDensity_QuaterniusBuilding3Big_LOD"
DENSITY_QUATERNIUS_BUILDING4_MESH_NAME = "JianghaiDensity_QuaterniusBuilding4_LOD"
DENSITY_QUATERNIUS_HOUSE2_MESH_NAME = "JianghaiDensity_QuaterniusHouse2_LOD"
STREET_CADENCE_MESH_PREFIX = "JianghaiStreetCadence_"
DENSITY_SOURCE_PROFILES = {
    "chinese_hall": {
        "source_object": "EastHarborResidence",
        "mesh_name": DENSITY_HALL_MESH_NAME,
        "decimate_ratio": 0.38,
        "base_scale": PROFILE_BASE_SCALE["chinese_hall"],
        "asset_name": "Chinese Temple 2",
        "creator": "Free poly",
        "source_url": "https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/",
    },
    "chinese_shop": {
        "source_object": "WeatheredRollerShop00",
        "mesh_name": DENSITY_SHOP_MESH_NAME,
        "decimate_ratio": 0.42,
        "base_scale": PROFILE_BASE_SCALE["chinese_shop"],
        "asset_name": "Chinese Four-corner Pavilion - Free; Quaternius Buildings Pack; Chinese Temple 2",
        "creator": "VVayToyek; Quaternius; Free poly",
        "source_url": "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free; https://quaternius.com/packs/buildings.html; https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/",
    },
    "chinese_gate": {
        "source_object": "EastGateRow00",
        "mesh_name": DENSITY_GATE_MESH_NAME,
        "decimate_ratio": 0.48,
        "base_scale": PROFILE_BASE_SCALE["chinese_gate"],
        "asset_name": "Chinese Four-corner Pavilion - Free; Quaternius Buildings Pack; Chinese Temple 2",
        "creator": "VVayToyek; Quaternius; Free poly",
        "source_url": "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free; https://quaternius.com/packs/buildings.html; https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/",
    },
    "quaternius_large": {
        "runtime_glb": "assets/models/quaternius_buildings_pack/building1-large.glb",
        "mesh_name": DENSITY_QUATERNIUS_LARGE_MESH_NAME,
        "decimate_ratio": 0.16,
        "base_scale": PROFILE_BASE_SCALE["quaternius_large"],
        "asset_name": "Buildings Pack / Building1_Large",
        "creator": "Quaternius",
        "source_url": "https://quaternius.com/packs/buildings.html",
        "weather_tint": (0.62, 0.56, 0.50),
        "weather_roughness": 0.82,
    },
    "quaternius_big": {
        "runtime_glb": "assets/models/quaternius_buildings_pack/building3-big.glb",
        "mesh_name": DENSITY_QUATERNIUS_BIG_MESH_NAME,
        "decimate_ratio": 0.85,
        "base_scale": PROFILE_BASE_SCALE["quaternius_big"],
        "asset_name": "Buildings Pack / Building3_Big",
        "creator": "Quaternius",
        "source_url": "https://quaternius.com/packs/buildings.html",
        "weather_tint": (0.58, 0.52, 0.46),
        "weather_roughness": 0.84,
    },
    "quaternius_building4": {
        "runtime_glb": "assets/models/quaternius_buildings_pack/building4.glb",
        "mesh_name": DENSITY_QUATERNIUS_BUILDING4_MESH_NAME,
        "decimate_ratio": 0.60,
        "base_scale": PROFILE_BASE_SCALE["quaternius_building4"],
        "asset_name": "Buildings Pack / Building4",
        "creator": "Quaternius",
        "source_url": "https://quaternius.com/packs/buildings.html",
        "weather_tint": (0.54, 0.58, 0.58),
        "weather_roughness": 0.86,
    },
    "quaternius_house2": {
        "runtime_glb": "assets/models/quaternius_buildings_pack/house2.glb",
        "mesh_name": DENSITY_QUATERNIUS_HOUSE2_MESH_NAME,
        "decimate_ratio": 0.30,
        "base_scale": PROFILE_BASE_SCALE["quaternius_house2"],
        "asset_name": "Buildings Pack / House2",
        "creator": "Quaternius",
        "source_url": "https://quaternius.com/packs/buildings.html",
        "weather_tint": (0.52, 0.48, 0.44),
        "weather_roughness": 0.88,
    },
}

STREET_CADENCE_LAYOUT = (
    ("WestClockRow01", "quaternius_large", (-12.20, -24.0, 0.03), 90.0, 1.90),
    ("WestMedicineRow01", "chinese_shop", (-18.50, 0.0, 0.03), 90.0, 1.30),
    ("WestMedicineRow02", "quaternius_building4", (-12.70, 12.0, 0.03), 90.0, 1.60),
    ("WestTheatreRow02", "quaternius_house2", (-14.25, 48.0, 0.03), 90.0, 3.00),
)
MARKET_WALKWAY_LAYOUT = {
    "JianghaiExpansion_MarketTeaCart": (3.40, 126.10, 4.38),
    "JianghaiExpansion_MarketWickerBasket": (5.45, 126.10, 4.38),
    "JianghaiCleared_MarketTeaTable": (-1.25, 125.85, 4.38),
    "JianghaiCleared_MarketStool00": (-2.18, 125.55, 4.38),
    "JianghaiCleared_MarketStool01": (-0.33, 125.55, 4.38),
    "JianghaiCleared_MarketStool02": (-1.23, 125.60, 4.38),
}
CROSS_STREET_INTRUSION_NAMES = (
    "WestTheatreRow01",
    "EastHardwareRow00",
    "OuterWestMidResidence",
    "OuterEastSquareResidence",
    "WestSouthRow01",
    "EastSouthRow01",
    "OuterEastSouthResidence",
)
PAWNSHOP_DOORWAY_CUT_VERSION = 3
ENTRY_FACADE_WALL_SOURCE = (
    REPO_ROOT / "assets" / "models" / "quaternius_downtown_city" / "Brick_Plain_1.gltf"
)
ENTRY_FACADE_FRAME_SOURCE = (
    REPO_ROOT / "assets" / "models" / "quaternius_downtown_city" / "DoorFrame_Trim.gltf"
)
ENTRY_FACADE_LAYOUT = (
    ("PawnshopEntryFacade", "GuangchangPawnshop", -86.0, 112.0),
    ("FactoryEntryFacade", "RedStarElectronicsFactory", 86.0, 7.86),
)
from jianghai_clan_hall_portal import (
    author_clan_hall_gate_portal,
    validate_clan_hall_gate_glb,
    validate_clan_hall_gate_portal,
)
from jianghai_retired_facades import remove_retired_facade_overlays


def tune_runtime_emissions() -> int:
    tuned = 0
    for material_name, strength in RUNTIME_EMISSION_STRENGTHS.items():
        material = bpy.data.materials.get(material_name)
        if material is None or not material.use_nodes or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            if node.type != "BSDF_PRINCIPLED":
                continue
            emission_strength = node.inputs.get("Emission Strength")
            if emission_strength is None:
                continue
            emission_strength.default_value = strength
            tuned += 1
    return tuned


def tune_runtime_materials() -> int:
    material = bpy.data.materials.get("JianghaiSignBacking")
    if material is None or not material.use_nodes or material.node_tree is None:
        return 0
    tuned = 0
    for node in material.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        metallic = node.inputs.get("Metallic")
        roughness = node.inputs.get("Roughness")
        if metallic is not None:
            metallic.default_value = 0.10
        if roughness is not None:
            roughness.default_value = 0.70
        tuned += 1
    return tuned


def remove_floating_market_signs() -> int:
    removed = 0
    for object_name in FLOATING_MARKET_SIGN_OBJECTS:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1
    return removed


def remove_retired_asset_metadata() -> int:
    removed = 0
    blocks = (
        *bpy.data.objects,
        *bpy.data.meshes,
        *bpy.data.materials,
        *bpy.data.images,
        *bpy.data.collections,
        *bpy.data.scenes,
        *bpy.data.worlds,
        *bpy.data.node_groups,
    )
    for block in blocks:
        for key in tuple(block.keys()):
            if str(key).lower() not in RETIRED_CUSTOM_PROPERTY_KEYS:
                continue
            del block[key]
            removed += 1
    return removed


def rebuild_factory_frontage() -> tuple[int, int]:
    removed = 0
    for obj in list(bpy.data.objects):
        if (
            obj.name in RUINED_FACTORY_OBJECTS
            or obj.name.startswith("RedStarMainFacade_")
            or obj.name.startswith("RedStarMainFacade_Cornice_")
            or obj.name.startswith("FactoryMarqueeBracket")
        ):
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1

    factory_root = bpy.data.objects.get("RedStarElectronicsFactory")
    if factory_root is None:
        raise RuntimeError("The Red Star factory root is missing")

    validated = 0
    for object_name, _, _, _ in FACTORY_BUILDING_LAYOUT:
        replacement = bpy.data.objects.get(object_name)
        if (
            replacement is None
            or replacement.type != "MESH"
            or replacement.parent != factory_root
            or replacement.data.get("jianghai_chinese_rebuild_version") != 1
        ):
            raise RuntimeError(f"Rebuilt Chinese factory frontage is invalid: {object_name}")
        replacement["district_role"] = "cleared_cc0_factory_frontage"
        validated += 1

    sign_backing = bpy.data.objects.get("RedStarFactoryMarqueeBacking")
    sign_text = bpy.data.objects.get("RedStarFactoryMarqueeText")
    if sign_backing is not None:
        sign_backing.location = (85.5, -3.90, 7.35)
    if sign_text is not None:
        sign_text.location = (85.5, -3.81, 7.35)
    return removed, validated


def clear_cross_street_intrusions() -> int:
    """Remove seven authored instances whose visible shells overlap the two roads."""

    removed = 0
    for object_name in CROSS_STREET_INTRUSION_NAMES:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1
    return removed


def weather_external_material(
    profile_name: str,
    slot_index: int,
    source: bpy.types.Material,
) -> bpy.types.Material:
    """Create one reusable, muted Jianghai treatment for a CC0 pack material."""

    profile = DENSITY_SOURCE_PROFILES[profile_name]
    material_name = f"Jianghai_{profile_name}_Weathered_{slot_index:02d}"
    material = bpy.data.materials.get(material_name)
    if material is None:
        material = source.copy()
        material.name = material_name
    tint = profile.get("weather_tint", (1.0, 1.0, 1.0))
    roughness_floor = profile.get("weather_roughness", 0.78)
    source_principled = None
    if source.use_nodes and source.node_tree is not None:
        source_principled = next(
            (node for node in source.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
            None,
        )
    if material.use_nodes and material.node_tree is not None:
        for node in material.node_tree.nodes:
            if node.type != "BSDF_PRINCIPLED":
                continue
            base_color = node.inputs.get("Base Color")
            if base_color is not None and not base_color.is_linked:
                source_base = (
                    source_principled.inputs.get("Base Color")
                    if source_principled is not None
                    else None
                )
                color = (
                    source_base.default_value
                    if source_base is not None and not source_base.is_linked
                    else source.diffuse_color
                )
                base_color.default_value = (
                    color[0] * tint[0],
                    color[1] * tint[1],
                    color[2] * tint[2],
                    color[3],
                )
            roughness = node.inputs.get("Roughness")
            if roughness is not None and not roughness.is_linked:
                source_roughness = (
                    source_principled.inputs.get("Roughness")
                    if source_principled is not None
                    else None
                )
                source_roughness_value = (
                    source_roughness.default_value
                    if source_roughness is not None and not source_roughness.is_linked
                    else 0.5
                )
                roughness.default_value = max(source_roughness_value, roughness_floor)
    diffuse = source.diffuse_color
    material.diffuse_color = (
        diffuse[0] * tint[0],
        diffuse[1] * tint[1],
        diffuse[2] * tint[2],
        diffuse[3],
    )
    material["source_asset"] = profile["asset_name"]
    material["source_creator"] = profile["creator"]
    material["source_url"] = profile["source_url"]
    material["license"] = "CC0 1.0 Universal"
    material["authored_adaptation"] = "Muted, roughened Jianghai DCC material treatment"
    return material


def build_authored_profile_mesh(
    profile_name: str,
    mesh_name: str,
    decimate_ratio: float,
) -> bpy.types.Mesh:
    profile = DENSITY_SOURCE_PROFILES[profile_name]
    old_mesh = bpy.data.meshes.get(mesh_name)
    if old_mesh is not None:
        if old_mesh.users != 0:
            raise RuntimeError(f"Generated Jianghai mesh still has live users: {mesh_name}")
        bpy.data.meshes.remove(old_mesh)

    imported_objects: list[bpy.types.Object] = []
    imported_meshes: list[bpy.types.Mesh] = []
    imported_materials: list[bpy.types.Material] = []
    runtime_glb = profile.get("runtime_glb")
    if runtime_glb is not None:
        before_objects = set(bpy.data.objects)
        before_meshes = set(bpy.data.meshes)
        before_materials = set(bpy.data.materials)
        source_path = REPO_ROOT / runtime_glb
        if not source_path.is_file():
            raise RuntimeError(f"CC0 density source GLB is missing: {source_path}")
        bpy.ops.import_scene.gltf(filepath=str(source_path))
        imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
        imported_meshes = [mesh for mesh in bpy.data.meshes if mesh not in before_meshes]
        imported_materials = [mat for mat in bpy.data.materials if mat not in before_materials]
        imported_sources = [obj for obj in imported_objects if obj.type == "MESH"]
        if len(imported_sources) != 1:
            raise RuntimeError(
                f"Expected one authored mesh in {source_path}, found {len(imported_sources)}"
            )
        source = imported_sources[0]
    else:
        source = bpy.data.objects.get(profile["source_object"])
    if source is None or source.type != "MESH":
        raise RuntimeError(f"Density source building is missing for profile: {profile_name}")

    template = source.copy()
    template.data = source.data.copy()
    template.name = f"__DENSITY_SOURCE_{profile_name}"
    template.parent = None
    template.location = (0.0, 0.0, 0.0)
    template.rotation_euler = (0.0, 0.0, 0.0)
    template.scale = (1.0, 1.0, 1.0)
    bpy.context.scene.collection.objects.link(template)
    if runtime_glb is not None:
        for slot_index, material in enumerate(tuple(template.data.materials)):
            if material is None:
                continue
            template.data.materials[slot_index] = weather_external_material(
                profile_name,
                slot_index,
                material,
            )
    bpy.ops.object.select_all(action="DESELECT")
    template.select_set(True)
    bpy.context.view_layer.objects.active = template
    if decimate_ratio < 0.9999:
        modifier = template.modifiers.new(name="AuthoredDistanceLOD", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = decimate_ratio
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh = template.data
    mesh.name = mesh_name
    mesh["authored_derivation"] = (
        "DCC decimated distance building from authored CC0 source"
        if decimate_ratio < 0.9999
        else "Full-resolution authored CC0 building adapted in Blender"
    )
    mesh["source_asset"] = profile["asset_name"]
    mesh["source_creator"] = profile["creator"]
    mesh["source_url"] = profile["source_url"]
    mesh["license"] = "CC0 1.0 Universal"
    if profile_name in QUATERNIUS_DENSITY_MESHES:
        consolidate_density_profile_mesh(mesh, profile_name)
    bpy.data.objects.remove(template, do_unlink=True)
    for obj in imported_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for imported_mesh in imported_meshes:
        if imported_mesh.name in bpy.data.meshes and imported_mesh.users == 0:
            bpy.data.meshes.remove(imported_mesh)
    for imported_material in imported_materials:
        if imported_material.name in bpy.data.materials and imported_material.users == 0:
            bpy.data.materials.remove(imported_material)
    return mesh


def build_density_mesh(profile_name: str) -> bpy.types.Mesh:
    profile = DENSITY_SOURCE_PROFILES[profile_name]
    return build_authored_profile_mesh(
        profile_name,
        profile["mesh_name"],
        profile["decimate_ratio"],
    )


def rebuild_street_cadence() -> int:
    """Validate the rebuilt near-street row without changing reviewed transforms."""

    validated = 0
    for object_name, profile_name, _, _, _ in STREET_CADENCE_LAYOUT:
        target = bpy.data.objects.get(object_name)
        if target is None or target.type != "MESH":
            raise RuntimeError(f"Street cadence target is missing: {object_name}")
        chinese_ready = target.data.get("jianghai_chinese_rebuild_version") == 1
        quaternius_ready = target.get("source_creator") == "Quaternius"
        if profile_name.startswith("chinese_") and not chinese_ready:
            raise RuntimeError(f"Chinese street-cadence profile is missing: {object_name}")
        if profile_name.startswith("quaternius_") and not quaternius_ready:
            raise RuntimeError(f"Quaternius street-cadence profile is missing: {object_name}")
        validated += 1
    return validated


def clear_market_walkway() -> int:
    """Move authored market furniture against the shops, leaving a real 1 m route."""

    adjusted = 0
    for object_name, location in MARKET_WALKWAY_LAYOUT.items():
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            raise RuntimeError(f"Authored market furniture is missing: {object_name}")
        obj.location = location
        adjusted += 1
    return adjusted


def rebuild_dense_perimeter() -> tuple[int, int, dict[str, int]]:
    """Rebuild hand-placed outer blocks from six finished CC0 building sources."""

    removed = 0
    for obj in list(bpy.data.objects):
        if not obj.name.startswith(DENSITY_OBJECT_PREFIX):
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1

    density_root = bpy.data.objects.get("JianghaiTenementDistrict")
    if density_root is None:
        raise RuntimeError("The Jianghai tenement district root is missing")

    meshes = {
        profile_name: build_density_mesh(profile_name)
        for profile_name in DENSITY_SOURCE_PROFILES
    }
    created = 0
    profile_counts = {profile_name: 0 for profile_name in DENSITY_SOURCE_PROFILES}
    for suffix, profile_name, location, yaw_degrees, scale in DENSITY_BUILDING_LAYOUT:
        profile = DENSITY_SOURCE_PROFILES[profile_name]
        building = bpy.data.objects.new(f"{DENSITY_OBJECT_PREFIX}{suffix}", meshes[profile_name])
        bpy.context.scene.collection.objects.link(building)
        building.parent = density_root
        building.location = location
        building.rotation_euler = (0.0, 0.0, radians(yaw_degrees))
        authored_scale = profile["base_scale"] * scale
        building.scale = (authored_scale, authored_scale, authored_scale)
        building["source_asset"] = profile["asset_name"]
        building["source_creator"] = profile["creator"]
        building["source_url"] = profile["source_url"]
        building["license"] = "CC0 1.0 Universal"
        building["authored_adaptation"] = (
            "DCC-decimated distance shell placed by hand for Jianghai perimeter density"
        )
        building["district_role"] = "authored_density_building"
        building["collision_role"] = "building_shell"
        building["building_id"] = building.name
        building["jianghai_gameplay_proxy"] = True
        building["jianghai_proxy_role"] = "density_building_shell"
        created += 1
        profile_counts[profile_name] += 1
    return removed, created, profile_counts


def load_entry_facade_module(source_path: Path, mesh_name: str) -> bpy.types.Mesh:
    """Copy one finished Quaternius module into the packed Jianghai DCC scene."""

    if not source_path.is_file():
        raise RuntimeError(f"CC0 entry-facade source is missing: {source_path}")
    old_mesh = bpy.data.meshes.get(mesh_name)
    if old_mesh is not None:
        if old_mesh.users != 0:
            raise RuntimeError(f"Entry-facade mesh still has live users: {mesh_name}")
        bpy.data.meshes.remove(old_mesh)

    before_objects = set(bpy.data.objects)
    before_meshes = set(bpy.data.meshes)
    before_materials = set(bpy.data.materials)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    imported_meshes = [mesh for mesh in bpy.data.meshes if mesh not in before_meshes]
    imported_materials = [mat for mat in bpy.data.materials if mat not in before_materials]
    mesh_sources = [obj for obj in imported_objects if obj.type == "MESH"]
    if len(mesh_sources) != 1:
        raise RuntimeError(
            f"Expected one mesh in entry-facade module {source_path}, found {len(mesh_sources)}"
        )

    mesh = mesh_sources[0].data.copy()
    mesh.name = mesh_name
    mesh["source_asset"] = f"Downtown City MegaKit / {source_path.stem}"
    mesh["source_creator"] = "Quaternius"
    mesh["source_url"] = "https://quaternius.com/packs/downtowncitymegakit.html"
    mesh["license"] = "CC0 1.0 Universal"
    mesh["authored_derivation"] = (
        "Finished CC0 modular facade component fitted in Blender for a hinged entry"
    )

    for obj in imported_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for imported_mesh in imported_meshes:
        if imported_mesh.name in bpy.data.meshes and imported_mesh.users == 0:
            bpy.data.meshes.remove(imported_mesh)
    for imported_material in imported_materials:
        if imported_material.name in bpy.data.materials and imported_material.users == 0:
            bpy.data.materials.remove(imported_material)
    return mesh


def rebuild_hinged_entry_facades() -> tuple[int, int]:
    """Close the former roller-shutter bays around normal human-scale door apertures."""

    removed = 0
    prefixes = tuple(layout[0] for layout in ENTRY_FACADE_LAYOUT)
    for obj in list(bpy.data.objects):
        if not obj.name.startswith(prefixes):
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1

    wall_mesh = load_entry_facade_module(
        ENTRY_FACADE_WALL_SOURCE,
        "JianghaiEntryFacade_BrickPlain",
    )
    frame_mesh = load_entry_facade_module(
        ENTRY_FACADE_FRAME_SOURCE,
        "JianghaiEntryFacade_DoorFrameTrim",
    )
    created = 0
    for prefix, parent_name, center_x, facade_y in ENTRY_FACADE_LAYOUT:
        parent = bpy.data.objects.get(parent_name)
        if parent is None:
            raise RuntimeError(f"Entry-facade parent is missing: {parent_name}")

        for side_name, side_sign in (("West", -1.0), ("East", 1.0)):
            for row in range(4):
                wall = bpy.data.objects.new(
                    f"{prefix}_Wall_{side_name}_{row:02d}",
                    wall_mesh,
                )
                bpy.context.scene.collection.objects.link(wall)
                wall.parent = parent
                wall.location = (center_x + side_sign * 2.6, facade_y, float(row))
                wall.scale = (1.6, 1.0, 1.0)
                created += 1

        lintel = bpy.data.objects.new(f"{prefix}_Wall_Lintel", wall_mesh)
        bpy.context.scene.collection.objects.link(lintel)
        lintel.parent = parent
        lintel.location = (center_x, facade_y, 3.0)
        created += 1

        frame = bpy.data.objects.new(f"{prefix}_DoorFrame", frame_mesh)
        bpy.context.scene.collection.objects.link(frame)
        frame.parent = parent
        frame.location = (center_x, facade_y - 0.015, 0.0)
        created += 1

        for obj in [
            child
            for child in parent.children
            if child.name.startswith(prefix)
        ]:
            obj["source_asset"] = "Downtown City MegaKit modular facade"
            obj["source_creator"] = "Quaternius"
            obj["source_url"] = "https://quaternius.com/packs/downtowncitymegakit.html"
            obj["license"] = "CC0 1.0 Universal"
            obj["authored_adaptation"] = (
                "DCC-fitted brick infill and trim around a normal hinged doorway"
            )
            obj["collision_role"] = "entry_facade"
            obj["entry_motion"] = "hinged"
    return removed, created


def cut_pawnshop_doorway() -> int:
    """Bisect a real passage through the storefront behind the interactive shutter."""

    storefront = bpy.data.objects.get("JianghaiCleared_PawnshopStorefront")
    if storefront is None or storefront.type != "MESH":
        raise RuntimeError("The cleared pawnshop storefront is missing")
    if storefront.get("doorway_cut_version") == PAWNSHOP_DOORWAY_CUT_VERSION:
        return 0

    if storefront.data.users > 1:
        storefront.data = storefront.data.copy()
    storefront.data.name = "JianghaiPawnshopStorefrontDoorway"
    bpy.ops.object.select_all(action="DESELECT")
    storefront.select_set(True)
    bpy.context.view_layer.objects.active = storefront
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    mesh = storefront.data
    editable = bmesh.new()
    editable.from_mesh(mesh)
    for plane_co, plane_no in (
        ((-3.80, 0.0, 0.0), (1.0, 0.0, 0.0)),
        ((3.80, 0.0, 0.0), (1.0, 0.0, 0.0)),
        ((0.0, 0.0, -0.20), (0.0, 0.0, 1.0)),
        ((0.0, 0.0, 4.40), (0.0, 0.0, 1.0)),
    ):
        bmesh.ops.bisect_plane(
            editable,
            geom=[*editable.verts, *editable.edges, *editable.faces],
            dist=0.0001,
            plane_co=plane_co,
            plane_no=plane_no,
            clear_outer=False,
            clear_inner=False,
        )
    doorway_faces = [
        face
        for face in editable.faces
        if -3.80 < face.calc_center_median().x < 3.80
        and -0.20 < face.calc_center_median().z < 4.40
    ]
    if not doorway_faces:
        editable.free()
        raise RuntimeError("The pawnshop doorway cut did not select any storefront faces")
    bmesh.ops.delete(editable, geom=doorway_faces, context="FACES")
    editable.to_mesh(mesh)
    editable.free()
    mesh.validate(verbose=False)
    mesh.update()

    storefront["doorway_cut_version"] = PAWNSHOP_DOORWAY_CUT_VERSION
    storefront["doorway_width_m"] = 7.60
    storefront["doorway_height_m"] = 4.20
    storefront["collision_role"] = "building_shell"
    return 1


def validate_pawnshop_frontage() -> tuple[int, int]:
    legacy = [
        obj.name
        for obj in bpy.data.objects
        if obj.name in PAWNSHOP_LEGACY_VISIBLE_NAMES
        or obj.name.startswith(PAWNSHOP_LEGACY_WALL_PREFIXES)
    ]
    canopy_root = bpy.data.objects.get("PawnshopAuthoredPavilionGate")
    canopy = [
        obj for obj in bpy.data.objects if obj.name.startswith("PawnshopAuthoredCanopy_")
    ]
    wings = [
        obj for obj in bpy.data.objects if obj.name.startswith("PawnshopAuthoredWing_")
    ]
    if legacy:
        raise RuntimeError(f"Legacy pawnshop programmer art remains visible: {legacy}")
    if (
        canopy_root is None
        or canopy_root.get("source_license") != "CC0 1.0 Universal"
        or canopy_root.get("source_creator") != "VVayToyek"
        or canopy_root.get("source_url")
        != "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free"
        or len(canopy) != 15
        or len(wings) != 16
        or any(
            wing.get("source_creator") != "James Ray Cock"
            or wing.get("source_url")
            != "https://polyhaven.com/a/modular_urban_apartments_facade"
            or wing.get("source_license") != "CC0 1.0 Universal"
            for wing in wings
        )
    ):
        raise RuntimeError(
            "Authored pawnshop frontage is incomplete: "
            f"root={canopy_root is not None} canopy={len(canopy)}/15 wings={len(wings)}/16"
        )
    return len(canopy), len(wings)


def flatten_tiled_images() -> int:
    replacements = 0
    for image in list(bpy.data.images):
        if image.type != "IMAGE" or not image.has_data:
            continue
        if image.source != "TILED" and "<UDIM>" not in image.filepath:
            continue
        flattened = bpy.data.images.new(
            f"{image.name}_Flattened",
            width=image.size[0],
            height=image.size[1],
            alpha=image.channels == 4,
        )
        flattened.colorspace_settings.name = image.colorspace_settings.name
        flattened.file_format = "PNG"
        flattened.pixels[:] = image.pixels[:]
        flattened.pack()
        for material in bpy.data.materials:
            if not material.use_nodes:
                continue
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image == image:
                    node.image = flattened
        bpy.data.images.remove(image)
        replacements += 1
    return replacements


def pack_runtime_jpeg(image: bpy.types.Image, cache_dir: Path, index: int) -> None:
    safe_name = re.sub(r"[^A-Za-z0-9_.-]+", "_", image.name).strip("._") or f"image_{index:03d}"
    path = cache_dir / f"{index:03d}_{safe_name}.jpg"
    image.file_format = "JPEG"
    image.filepath_raw = str(path)
    image.save()
    if image.packed_file is not None:
        image.unpack(method="REMOVE")
    image.reload()
    image.pack()
    image.filepath_raw = ""


def runtime_texture_limit(image: bpy.types.Image) -> int:
    normalized_name = image.name.lower()
    if any(token in normalized_name for token in SMALL_FURNITURE_TEXTURE_TOKENS):
        return MAX_SMALL_FURNITURE_TEXTURE_SIZE
    if any(token in normalized_name for token in DETAIL_TEXTURE_TOKENS):
        return MAX_DETAIL_TEXTURE_SIZE
    return MAX_RUNTIME_TEXTURE_SIZE


def optimize_runtime_textures(cache_dir: Path) -> tuple[int, int]:
    resized = 0
    recompressed = 0
    bpy.context.scene.render.image_settings.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 90
    for index, image in enumerate(bpy.data.images):
        if image.type != "IMAGE":
            continue
        if not image.has_data:
            try:
                _ = image.pixels[0]
            except (IndexError, RuntimeError):
                continue
        width, height = image.size
        longest = max(width, height)
        target_size = runtime_texture_limit(image)
        should_optimize = longest > target_size
        if should_optimize:
            factor = target_size / longest
            image.scale(max(1, round(width * factor)), max(1, round(height * factor)))
            resized += 1
            print(
                f"JIANGHAI_TEXTURE_LIMIT name={image.name!r} "
                f"size={image.size[0]}x{image.size[1]} target={target_size}"
            )
        if should_optimize:
            pack_runtime_jpeg(image, cache_dir, index)
            recompressed += 1
            print(f"JIANGHAI_TEXTURE_JPEG name={image.name!r} quality=90")
    return resized, recompressed


def scene_statistics() -> tuple[int, int, int, int]:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    materials: set[str] = set()
    for obj in meshes:
        materials.update(material.name for material in obj.data.materials if material is not None)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_objects = 0
    evaluated_triangles = 0
    for instance in depsgraph.object_instances:
        obj = instance.object
        # The depsgraph also exposes the source CURVE for every evaluated
        # curve mesh. Count MESH only so each exported surface is counted once.
        if obj.type != "MESH":
            continue
        mesh = obj.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        if mesh is None:
            continue
        try:
            mesh.calc_loop_triangles()
            evaluated_triangles += len(mesh.loop_triangles)
            evaluated_objects += 1
        finally:
            obj.to_mesh_clear()
    return len(meshes), evaluated_objects, evaluated_triangles, len(materials)


def valley_triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def valley_location_angular_gap(objects: list[bpy.types.Object]) -> float:
    angles = sorted(
        atan2(obj.location.y - 60.0, obj.location.x) % (2.0 * pi)
        for obj in objects
    )
    if not angles:
        return float("inf")
    return max(
        (angles[(index + 1) % len(angles)] - angle) % (2.0 * pi)
        for index, angle in enumerate(angles)
    )


def valley_mesh_topology_statistics(mesh: bpy.types.Mesh) -> dict[str, float | int]:
    editable = bmesh.new()
    editable.from_mesh(mesh)
    editable.verts.ensure_lookup_table()
    editable.edges.ensure_lookup_table()
    boundary_edges = [edge for edge in editable.edges if edge.is_boundary]

    visited_vertices: set[int] = set()
    connected_components = 0
    for vertex in editable.verts:
        if vertex.index in visited_vertices:
            continue
        connected_components += 1
        pending = [vertex]
        visited_vertices.add(vertex.index)
        while pending:
            current = pending.pop()
            for edge in current.link_edges:
                adjacent = edge.other_vert(current)
                if adjacent.index not in visited_vertices:
                    visited_vertices.add(adjacent.index)
                    pending.append(adjacent)

    visited_edges: set[int] = set()
    boundary_components = 0
    for edge in boundary_edges:
        if edge.index in visited_edges:
            continue
        boundary_components += 1
        pending_edges = [edge]
        visited_edges.add(edge.index)
        while pending_edges:
            current = pending_edges.pop()
            for vertex in current.verts:
                for adjacent in vertex.link_edges:
                    if adjacent.is_boundary and adjacent.index not in visited_edges:
                        visited_edges.add(adjacent.index)
                        pending_edges.append(adjacent)

    boundary_heights = [vertex.co.z for edge in boundary_edges for vertex in edge.verts]
    terrain_faces = [face for face in editable.faces if abs(face.normal.z) >= 0.5]
    maximum_terrain_edge = max(
        (edge.calc_length() for face in terrain_faces for edge in face.edges),
        default=float("inf"),
    )
    maximum_terrain_edge_area_ratio = max(
        (
            max(edge.calc_length() for edge in face.edges) ** 2
            / max(face.calc_area(), 0.000000000001)
            for face in terrain_faces
        ),
        default=float("inf"),
    )
    result: dict[str, float | int] = {
        "connected_components": connected_components,
        "boundary_edges": len(boundary_edges),
        "boundary_components": boundary_components,
        "nonmanifold_edges": sum(not edge.is_manifold for edge in editable.edges),
        "degenerate_faces": sum(face.calc_area() <= 0.000000000001 for face in editable.faces),
        "invalid_face_normals": sum(
            not all(isfinite(value) for value in face.normal)
            or abs(face.normal.length - 1.0) > 0.0001
            for face in editable.faces
        ),
        "maximum_terrain_edge": maximum_terrain_edge,
        "maximum_terrain_edge_area_ratio": maximum_terrain_edge_area_ratio,
        "boundary_minimum_z": min(boundary_heights, default=float("inf")),
        "boundary_maximum_z": max(boundary_heights, default=float("-inf")),
    }
    editable.free()
    return result


def coast_uv_coordinates(x: float, y: float) -> tuple[float, float]:
    return (
        x / COAST_UV_TILE_SIZE_LOCAL,
        y / COAST_UV_TILE_SIZE_LOCAL,
    )


def valley_coast_uv_statistics(
    mesh: bpy.types.Mesh,
) -> dict[str, float | int | bool]:
    uv_layer = mesh.uv_layers.get(COAST_UV_LAYER_NAME)
    maximum_error = float("inf")
    finite = False
    if uv_layer is not None and len(uv_layer.data) == len(mesh.loops):
        maximum_error = 0.0
        finite = True
        for loop in mesh.loops:
            vertex = mesh.vertices[loop.vertex_index]
            expected = coast_uv_coordinates(vertex.co.x, vertex.co.y)
            uv = uv_layer.data[loop.index].uv
            finite = finite and isfinite(uv.x) and isfinite(uv.y)
            maximum_error = max(
                maximum_error,
                abs(uv.x - expected[0]),
                abs(uv.y - expected[1]),
            )
    return {
        "layer_count": len(mesh.uv_layers),
        "loop_count": 0 if uv_layer is None else len(uv_layer.data),
        "finite": finite,
        "maximum_error": maximum_error,
    }


def valley_world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ Vector(corner)
            minimum = Vector(map(min, minimum, world_corner))
            maximum = Vector(map(max, maximum, world_corner))
    return minimum, maximum


def valley_source_metadata_ready(
    datablock,
    source_url: str,
    creators: str,
    acquisition_date: str,
    source_license: str,
) -> bool:
    return (
        datablock.get("source_license") == source_license
        and datablock.get("source_url") == source_url
        and datablock.get("source_creator") == creators
        and datablock.get("acquisition_date") == acquisition_date
    )


def valley_opaque_material_ready(
    material: bpy.types.Material,
    source_url: str,
    creators: str,
    acquisition_date: str,
    source_license: str = "CC0 1.0 Universal",
) -> bool:
    if (
        not valley_source_metadata_ready(
            material, source_url, creators, acquisition_date, source_license
        )
        or not material.use_nodes
        or material.node_tree is None
    ):
        return False
    shaders = [
        node for node in material.node_tree.nodes
        if node.type == "BSDF_PRINCIPLED"
    ]
    if len(shaders) != 1:
        return False
    alpha = shaders[0].inputs.get("Alpha")
    if alpha is None or alpha.is_linked or abs(alpha.default_value - 1.0) > 0.0001:
        return False
    source_images = {
        node.image
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    return bool(source_images) and all(
        valley_source_metadata_ready(
            image, source_url, creators, acquisition_date, source_license
        )
        and image.packed_file is not None
        for image in source_images
    )


def valley_asset_instance_ready(
    obj: bpy.types.Object,
    spec: dict[str, str | int],
    *,
    require_asset_id: bool,
) -> bool:
    if obj.type != "MESH":
        return False
    materials = [material for material in obj.data.materials if material is not None]
    surface_url = spec.get("surface_url", spec["url"])
    surface_creators = spec.get("surface_creators", spec["creators"])
    surface_acquisition_date = spec.get("surface_acquisition_date", "2026-08-29")
    surface_license = spec.get("surface_license", spec["license"])
    return (
        (not require_asset_id or obj.get("source_asset_id") == spec["asset_id"])
        and obj.data.name == spec["mesh"]
        and valley_triangle_count(obj.data) == spec["triangles"]
        and valley_source_metadata_ready(
            obj, spec["url"], spec["creators"], "2026-08-29", spec["license"]
        )
        and valley_source_metadata_ready(
            obj.data, spec["url"], spec["creators"], "2026-08-29", spec["license"]
        )
        and len(materials) == 1
        and materials[0].name == spec["material"]
        and valley_opaque_material_ready(
            materials[0],
            surface_url,
            surface_creators,
            surface_acquisition_date,
            surface_license,
        )
    )


def valley_uniform_positive_scale(obj: bpy.types.Object) -> bool:
    return (
        min(obj.scale) > 0.0
        and max(obj.scale) / min(obj.scale) <= 1.001
        and obj.matrix_world.determinant() > 0.0
    )


def valley_perimeter_ground_coverage(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
) -> float:
    allowed_objects = [foundation, *ground_scans]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    hits = 0
    samples = 0
    for angle_index in range(72):
        angle = angle_index * (2.0 * pi / 72.0)
        direction_x = cos(angle)
        direction_y = sin(angle)
        edge_distance = min(
            170.0 / max(abs(direction_x), 0.0001),
            160.0 / max(abs(direction_y), 0.0001),
        )
        for fraction in (0.10, 0.32, 0.54, 0.76, 0.92):
            radius = edge_distance + (360.0 - edge_distance) * fraction
            origin = Vector((direction_x * radius, 60.0 + direction_y * radius, 60.0))
            hit = False
            for obj in allowed_objects:
                inverse = obj.matrix_world.inverted_safe()
                local_origin = inverse @ origin
                local_direction = inverse.to_3x3() @ Vector((0.0, 0.0, -1.0))
                local_direction.normalize()
                object_hit, location, _, _ = obj.ray_cast(
                    local_origin,
                    local_direction,
                    depsgraph=depsgraph,
                )
                if not object_hit:
                    continue
                world_location = obj.matrix_world @ location
                vertical_distance = origin.z - world_location.z
                if -0.01 <= vertical_distance <= 120.0:
                    hit = True
                    break
            samples += 1
            if hit:
                hits += 1
    return hits / samples


def valley_highest_ground_height(
    world_x: float,
    world_y: float,
    ground_scans: list[bpy.types.Object],
    depsgraph: bpy.types.Depsgraph,
) -> float | None:
    origin = Vector((world_x, world_y, 80.0))
    highest = None
    for obj in ground_scans:
        inverse = obj.matrix_world.inverted_safe()
        local_origin = inverse @ origin
        local_direction = inverse.to_3x3() @ Vector((0.0, 0.0, -1.0))
        local_direction.normalize()
        hit, location, _, _ = obj.ray_cast(
            local_origin,
            local_direction,
            depsgraph=depsgraph,
        )
        if not hit:
            continue
        world_height = (obj.matrix_world @ location).z
        highest = world_height if highest is None else max(highest, world_height)
    return highest


def valley_ground_vertical_surface_hits(
    world_x: float,
    world_y: float,
    ground_scans: list[bpy.types.Object],
    depsgraph: bpy.types.Depsgraph,
) -> tuple[list[float], list[float]]:
    origin = Vector((world_x, world_y, 80.0))
    top_heights: list[float] = []
    skirt_heights: list[float] = []
    for obj in ground_scans:
        inverse = obj.matrix_world.inverted_safe()
        hit, location, normal, _ = obj.ray_cast(
            inverse @ origin,
            (inverse.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized(),
            depsgraph=depsgraph,
        )
        if not hit:
            continue
        world_height = (obj.matrix_world @ location).z
        world_normal = (obj.matrix_world.to_3x3() @ normal).normalized()
        (top_heights if world_normal.z >= 0.5 else skirt_heights).append(
            world_height
        )
    return top_heights, skirt_heights


def valley_godot_to_blender(vector: Vector) -> Vector:
    return Vector((vector.x, -vector.z, vector.y))


def valley_percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        return 0.0
    index = round((len(ordered) - 1) * fraction)
    return ordered[min(len(ordered) - 1, max(0, index))]


def valley_north_edge_seam_statistics(
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, float | int]:
    """Lock the exact north-camera band that exposed avenue endcap side faces."""
    forward = (NORTH_EDGE_CAMERA_TARGET - NORTH_EDGE_CAMERA_ORIGIN).normalized()
    right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
    camera_up = right.cross(forward).normalized()
    vertical_scale = tan(NORTH_EDGE_CAMERA_FOV * pi / 360.0)
    horizontal_scale = vertical_scale * (1280.0 / 720.0)
    world_origin = valley_godot_to_blender(NORTH_EDGE_CAMERA_ORIGIN)
    samples = 0
    hits = 0
    distant_side_hits = 0
    minimum_normal_z = 1.0
    for pixel_y in range(400, 411):
        screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
        for pixel_x in range(1220, 1280, 2):
            screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
            godot_direction = (
                forward
                + right * screen_x * horizontal_scale
                + camera_up * screen_y * vertical_scale
            ).normalized()
            direction = valley_godot_to_blender(godot_direction)
            hit, location, normal, _, obj, _ = bpy.context.scene.ray_cast(
                depsgraph,
                world_origin,
                direction,
                distance=2_500.0,
            )
            samples += 1
            if not hit:
                continue
            hits += 1
            distance = (location - world_origin).length
            minimum_normal_z = min(minimum_normal_z, normal.z)
            if distance > 80.0 and normal.z < 0.5:
                distant_side_hits += 1
                print(
                    "JIANGHAI_NORTH_EDGE_SIDE "
                    f"pixel=({pixel_x},{pixel_y}) object={obj.name} "
                    f"distance={distance:.3f} normal_z={normal.z:.5f} world="
                    f"({location.x:.3f},{location.y:.3f},{location.z:.3f})"
                )
    return {
        "north_edge_samples": samples,
        "north_edge_hits": hits,
        "north_edge_distant_side_hits": distant_side_hits,
        "north_edge_minimum_normal_z": minimum_normal_z,
    }


def valley_south_ground_seam_statistics(
    ground: bpy.types.Object,
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, float | int]:
    forward = (SOUTH_GROUND_CAMERA_TARGET - SOUTH_GROUND_CAMERA_ORIGIN).normalized()
    right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
    camera_up = right.cross(forward).normalized()
    vertical_scale = tan(SOUTH_GROUND_CAMERA_FOV * pi / 360.0)
    horizontal_scale = vertical_scale * (1280.0 / 720.0)
    world_origin = valley_godot_to_blender(SOUTH_GROUND_CAMERA_ORIGIN)
    samples = 0
    ground_top_hits = 0
    distant_side_hits = 0
    heights = []
    for pixel_y in range(387, 392):
        screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
        for pixel_x in range(1057, 1075):
            screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
            direction = valley_godot_to_blender((
                forward + right * screen_x * horizontal_scale
                + camera_up * screen_y * vertical_scale
            ).normalized())
            hit, location, normal, _, obj, _ = bpy.context.scene.ray_cast(
                depsgraph, world_origin, direction, distance=2_500.0
            )
            samples += 1
            if not hit:
                continue
            distance = (location - world_origin).length
            if obj == ground and normal.z >= 0.5:
                ground_top_hits += 1
                heights.append(location.z)
            elif distance > 80.0 and normal.z < 0.5:
                distant_side_hits += 1
    return {
        "south_ground_samples": samples,
        "south_ground_top_hits": ground_top_hits,
        "south_ground_distant_side_hits": distant_side_hits,
        "south_ground_height_p10_p90": (
            valley_percentile(heights, 0.90) - valley_percentile(heights, 0.10)
        ),
    }


def valley_ground_screen_relief_statistics(
    ground: bpy.types.Object,
    depsgraph: bpy.types.Depsgraph,
) -> list[dict[str, float | int]]:
    camera_specs = (
        (Vector((205.0, 3.2, 145.0)), Vector((157.0, -1.2, 92.0)), 58.0,
         range(330, 481, 10)),
        (SOUTH_GROUND_CAMERA_ORIGIN, SOUTH_GROUND_CAMERA_TARGET, 68.0,
         range(350, 421, 5)),
        (NORTH_EDGE_CAMERA_ORIGIN, NORTH_EDGE_CAMERA_TARGET, 68.0,
         range(350, 421, 5)),
    )
    results = []
    for godot_origin, godot_target, fov, pixel_ys in camera_specs:
        forward = (godot_target - godot_origin).normalized()
        right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
        camera_up = right.cross(forward).normalized()
        vertical_scale = tan(fov * pi / 360.0)
        horizontal_scale = vertical_scale * (1280.0 / 720.0)
        world_origin = valley_godot_to_blender(godot_origin)
        inverse = ground.matrix_world.inverted_safe()
        heights = []
        normal_zs = []
        for pixel_y in pixel_ys:
            screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
            for pixel_x in range(20, 1261, 20):
                screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
                direction = valley_godot_to_blender((
                    forward + right * screen_x * horizontal_scale
                    + camera_up * screen_y * vertical_scale
                ).normalized())
                hit, location, normal, _ = ground.ray_cast(
                    inverse @ world_origin,
                    (inverse.to_3x3() @ direction).normalized(),
                    depsgraph=depsgraph,
                )
                if not hit:
                    continue
                world_normal = (ground.matrix_world.to_3x3() @ normal).normalized()
                if world_normal.z < 0.5:
                    continue
                heights.append((ground.matrix_world @ location).z)
                normal_zs.append(world_normal.z)
        normal_mean = sum(normal_zs) / len(normal_zs) if normal_zs else 1.0
        results.append({
            "samples": len(heights),
            "height_p10_p90": (
                valley_percentile(heights, 0.90)
                - valley_percentile(heights, 0.10)
            ),
            "normal_z_std": (
                sum((value - normal_mean) ** 2 for value in normal_zs)
                / len(normal_zs)
            ) ** 0.5 if normal_zs else 0.0,
        })
    return results


def valley_ground_player_view_statistics(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, int]:
    camera_specs = (
        (Vector((205.0, 3.2, 145.0)), Vector((157.0, -1.2, 92.0)), 58.0,
         range(0, 1280, 10), range(360, 461, 5)),
        (Vector((112.0, 1.65, 86.0)), Vector((18.0, 11.0, 300.0)), 68.0,
         range(0, 1280, 20), range(340, 541, 10)),
        (Vector((-118.0, 1.65, -207.0)), Vector((-25.0, 12.0, -340.0)),
         68.0, range(0, 1280, 20), range(340, 541, 10)),
    )
    samples = 0
    top_hits = 0
    visible_skirt_hits = 0
    foundation_occluded_skirt_hits = 0
    near_coplanar_double_top_hits = 0
    for godot_origin, godot_target, fov, pixel_xs, pixel_ys in camera_specs:
        forward = (godot_target - godot_origin).normalized()
        right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
        camera_up = right.cross(forward).normalized()
        vertical_scale = tan(fov * pi / 360.0)
        horizontal_scale = vertical_scale * (1280.0 / 720.0)
        world_origin = valley_godot_to_blender(godot_origin)
        for pixel_y in pixel_ys:
            screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
            for pixel_x in pixel_xs:
                screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
                world_direction = valley_godot_to_blender((
                    forward
                    + right * screen_x * horizontal_scale
                    + camera_up * screen_y * vertical_scale
                ).normalized())
                foundation_inverse = foundation.matrix_world.inverted_safe()
                hit, location, normal, _ = foundation.ray_cast(
                    foundation_inverse @ world_origin,
                    (foundation_inverse.to_3x3() @ world_direction).normalized(),
                    depsgraph=depsgraph,
                )
                closest = None
                if hit:
                    world_location = foundation.matrix_world @ location
                    closest = (
                        (world_location - world_origin).length,
                        (foundation.matrix_world.to_3x3() @ normal).normalized().z,
                        False,
                    )
                closest_ground = None
                ground_top_depths: list[float] = []
                for obj in ground_scans:
                    inverse = obj.matrix_world.inverted_safe()
                    hit, location, normal, _ = obj.ray_cast(
                        inverse @ world_origin,
                        (inverse.to_3x3() @ world_direction).normalized(),
                        depsgraph=depsgraph,
                    )
                    if not hit:
                        continue
                    world_location = obj.matrix_world @ location
                    candidate = (
                        (world_location - world_origin).length,
                        (obj.matrix_world.to_3x3() @ normal).normalized().z,
                        True,
                    )
                    if candidate[1] >= 0.5:
                        ground_top_depths.append(candidate[0])
                    if closest_ground is None or candidate[0] < closest_ground[0]:
                        closest_ground = candidate
                    if closest is None or candidate[0] < closest[0]:
                        closest = candidate
                samples += 1
                ground_top_depths.sort()
                if (
                    len(ground_top_depths) >= 2
                    and ground_top_depths[1] - ground_top_depths[0] < 0.10
                ):
                    near_coplanar_double_top_hits += 1
                if (
                    closest is not None and not closest[2]
                    and closest_ground is not None and closest_ground[1] < 0.5
                    and closest[0] < closest_ground[0]
                ):
                    foundation_occluded_skirt_hits += 1
                if closest is None or not closest[2]:
                    continue
                if closest[1] >= 0.5:
                    top_hits += 1
                else:
                    visible_skirt_hits += 1
    return {
        "player_view_samples": samples,
        "player_view_top_hits": top_hits,
        "player_view_visible_skirt_hits": visible_skirt_hits,
        "player_view_foundation_occluded_skirt_hits":
            foundation_occluded_skirt_hits,
        "player_view_near_coplanar_double_top_hits":
            near_coplanar_double_top_hits,
    }


def valley_ground_continuity_statistics(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
) -> dict[str, float | int]:
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    foundation_minimum, foundation_maximum = valley_world_bounds([foundation])
    edge_points: list[tuple[float, float]] = []
    for index in range(33):
        fraction = index / 32.0
        x = foundation_minimum.x + (foundation_maximum.x - foundation_minimum.x) * fraction
        y = foundation_minimum.y + (foundation_maximum.y - foundation_minimum.y) * fraction
        edge_points.extend((
            (x, foundation_minimum.y - 2.0),
            (x, foundation_maximum.y + 2.0),
            (foundation_minimum.x - 2.0, y),
            (foundation_maximum.x + 2.0, y),
        ))
    edge_heights = [
        valley_highest_ground_height(x, y, ground_scans, depsgraph)
        for x, y in edge_points
    ]
    edge_hits = [height for height in edge_heights if height is not None]
    edge_gaps = [
        max(0.0, foundation_minimum.z - height)
        for height in edge_hits
    ]

    ring_heights: list[float | None] = []
    top_surface_hits = 0
    multi_top_hits = 0
    near_coplanar_double_top_hits = 0
    critical_overlap_hits = 0
    vertical_skirt_highest_hits = 0
    ring_radii = (180.0, 220.0, 240.0, 260.0, 280.0, 300.0,
                  340.0, 400.0, 460.0, 530.0, 560.0)
    critical_radii = {240.0, 260.0, 280.0, 300.0}
    for radius in ring_radii:
        for angle_index in range(720):
            angle = angle_index * (2.0 * pi / 720.0)
            top_heights, skirt_heights = valley_ground_vertical_surface_hits(
                cos(angle) * radius,
                60.0 + sin(angle) * radius,
                ground_scans,
                depsgraph,
            )
            highest_top = max(top_heights, default=None)
            ring_heights.append(highest_top)
            if highest_top is not None:
                top_surface_hits += 1
            if len(top_heights) >= 2:
                multi_top_hits += 1
                ordered_heights = sorted(top_heights, reverse=True)
                if ordered_heights[0] - ordered_heights[1] < 0.10:
                    near_coplanar_double_top_hits += 1
            if radius in critical_radii and len(top_heights) >= 2:
                critical_overlap_hits += 1
            if skirt_heights and max(skirt_heights) >= max(
                top_heights, default=float("-inf")
            ) - 0.0001:
                vertical_skirt_highest_hits += 1
    ring_hits = [height for height in ring_heights if height is not None]
    player_view = valley_ground_player_view_statistics(
        foundation, ground_scans, depsgraph
    )
    return {
        "foundation_edge_samples": len(edge_points),
        "foundation_edge_hits": len(edge_hits),
        "foundation_edge_coverage": len(edge_hits) / len(edge_points),
        "foundation_edge_maximum_gap": max(edge_gaps, default=float("inf")),
        "foundation_edge_minimum_height": min(edge_hits, default=float("-inf")),
        "foundation_edge_maximum_height": max(edge_hits, default=float("inf")),
        "ring_samples": len(ring_heights),
        "ring_hits": len(ring_hits),
        "ring_coverage": len(ring_hits) / len(ring_heights),
        "ring_minimum_height": min(ring_hits, default=float("-inf")),
        "ring_maximum_height": max(ring_hits, default=float("inf")),
        "top_surface_hits": top_surface_hits,
        "multi_top_hits": multi_top_hits,
        "near_coplanar_double_top_hits": near_coplanar_double_top_hits,
        "critical_overlap_samples": len(critical_radii) * 720,
        "critical_overlap_hits": critical_overlap_hits,
        "critical_overlap_coverage": critical_overlap_hits
            / (len(critical_radii) * 720),
        "vertical_skirt_highest_hits": vertical_skirt_highest_hits,
        **player_view,
    }


def validate_valley_environment() -> tuple[int, int, int]:
    root = bpy.data.objects.get("JianghaiValleyEnvironment")
    foundation = bpy.data.objects.get("OldCityFoundation")
    ground_scans = sorted(
        (obj for obj in bpy.data.objects if obj.name.startswith("JianghaiPerimeterGround")),
        key=lambda obj: obj.name,
    )
    mountains = sorted(
        (obj for obj in bpy.data.objects if obj.name.startswith("JianghaiMountainMassif")),
        key=lambda obj: obj.name,
    )
    if (
        root is None
        or foundation is None
        or foundation.type != "MESH"
        or len(ground_scans) != GROUND_INSTANCE_COUNT
        or len(mountains) != 12
    ):
        raise RuntimeError("The authored Jianghai valley environment is incomplete")
    bpy.context.view_layer.update()
    asset_specs = {
        "coast_line_01": {
            "asset_id": "coast_line_01",
            "url": "https://polyhaven.com/a/coast_line_01",
            "creators": "Rob Tuytel; Rico Cilliers",
            "triangles": GROUND_EXPECTED_TRIANGLES,
            "mesh": "JianghaiCoastLine01CompositeTerrain",
            "material": COAST_SURFACE_MATERIAL_NAME,
            "license": "CC0 1.0 Universal",
            "surface_url": "https://polyhaven.com/a/gravel_floor_03",
            "surface_creators": "Charlotte Baglioni",
            "surface_acquisition_date": "2026-08-28",
            "surface_license": "CC0 1.0 Universal",
        },
        "hero_mountain": {
            "asset_id": "hero_mountain",
            "url": "https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca",
            "creators": "solararchitect",
            "triangles": 14_000,
            "mesh": "JianghaiHeroMountainDistantLOD",
            "material": "JianghaiHeroMountainPBR",
            "license": "CC BY 4.0",
        },
    }
    foundation_triangles = valley_triangle_count(foundation.data)
    ground_triangle_counts = [valley_triangle_count(ground.data) for ground in ground_scans]
    mountain_triangle_counts = [
        valley_triangle_count(mountain.data) for mountain in mountains
    ]
    instance_triangles = (
        foundation_triangles
        + sum(ground_triangle_counts)
        + sum(mountain_triangle_counts)
    )
    minimum, maximum = valley_world_bounds([foundation, *ground_scans, *mountains])
    extent = maximum - minimum
    mountain_bounds = [valley_world_bounds([mountain]) for mountain in mountains]
    mountain_bottoms = [minimum.z for minimum, _ in mountain_bounds]
    mountain_edge_tops = [
        max(
            (mountain.matrix_world @ vertex.co).z
            for vertex in mountain.data.vertices
            if max(abs(vertex.co.x), abs(vertex.co.y)) >= 0.99
        )
        for mountain in mountains
    ]
    mountain_inner_radii = [
        min(
            hypot(world_vertex.x, world_vertex.y - 60.0)
            for world_vertex in (
                mountain.matrix_world @ vertex.co for vertex in mountain.data.vertices
            )
        )
        for mountain in mountains
    ]
    maximum_inner_ring_radius = max(mountain_inner_radii[:6])
    maximum_outer_ring_radius = max(mountain_inner_radii[6:])
    mountains_outside = all(
        bounds_max.x < -170.0 or bounds_min.x > 170.0
        or bounds_max.y < -100.0 or bounds_min.y > 220.0
        for bounds_min, bounds_max in mountain_bounds
    )
    mountain_angles = sorted(
        atan2(((bounds_min + bounds_max) * 0.5).y - 60.0,
              ((bounds_min + bounds_max) * 0.5).x) % (2.0 * pi)
        for bounds_min, bounds_max in mountain_bounds
    )
    max_angular_gap = max(
        (mountain_angles[(index + 1) % len(mountain_angles)] - angle) % (2.0 * pi)
        for index, angle in enumerate(mountain_angles)
    )
    foundation_minimum, foundation_maximum = valley_world_bounds([foundation])
    foundation_extent = foundation_maximum - foundation_minimum
    ground_minimum, ground_maximum = valley_world_bounds(ground_scans)
    ground_extent = ground_maximum - ground_minimum
    ground_meshes = {ground.data for ground in ground_scans}
    mountain_meshes = {mountain.data for mountain in mountains}
    ground_mesh = next(iter(ground_meshes), None)
    ground_topology = (
        valley_mesh_topology_statistics(ground_mesh)
        if ground_mesh is not None
        else {
            "connected_components": 0,
            "boundary_edges": 0,
            "boundary_components": 0,
            "nonmanifold_edges": 0,
            "degenerate_faces": 0,
            "invalid_face_normals": 0,
            "maximum_terrain_edge": float("inf"),
            "maximum_terrain_edge_area_ratio": float("inf"),
            "boundary_minimum_z": float("inf"),
            "boundary_maximum_z": float("-inf"),
        }
    )
    ground_repair_ready = (
        ground_mesh is not None
        and ground_mesh.get("dcc_composite_terrain") is True
        and ground_mesh.get("dcc_source_scan_count") == 8
        and ground_mesh.get("dcc_source_welded_vertices")
            == GROUND_EXPECTED_WELDED_VERTICES
        and ground_mesh.get("single_valued_top_surface") is True
        and len(ground_mesh.vertices) == GROUND_EXPECTED_VERTICES
        and abs(
            ground_mesh.get("boundary_skirt_depth", 0.0)
            - GROUND_BOUNDARY_SKIRT_DEPTH
        ) <= 0.000001
        and ground_mesh.get("boundary_skirt_source_edges")
            == GROUND_EXPECTED_BOUNDARY_EDGES
        and ground_topology["connected_components"] == 1
        and ground_topology["boundary_edges"] == GROUND_EXPECTED_BOUNDARY_EDGES
        and ground_topology["boundary_components"]
            == GROUND_EXPECTED_BOUNDARY_COMPONENTS
        and ground_topology["nonmanifold_edges"] == GROUND_EXPECTED_BOUNDARY_EDGES
        and ground_topology["degenerate_faces"] == 0
        and ground_topology["invalid_face_normals"] == 0
        and ground_topology["maximum_terrain_edge"] <= 9.0
        and ground_topology["maximum_terrain_edge_area_ratio"] <= 15.0
        and len(ground_mesh.polygons) == GROUND_EXPECTED_TRIANGLES
        and all(len(polygon.vertices) == 3 for polygon in ground_mesh.polygons)
        and ground_topology["boundary_maximum_z"]
            <= max(vertex.co.z for vertex in ground_mesh.vertices) - 0.70
        and len(ground_mesh.uv_layers) >= 1
    )
    top_surface_vertex_count = (
        ground_mesh.get("top_surface_vertex_count", 0)
        if ground_mesh is not None else 0
    )
    top_surface_vertices = (
        list(ground_mesh.vertices[:top_surface_vertex_count])
        if ground_mesh is not None else []
    )
    composite_top_relief = (
        max(vertex.co.z for vertex in top_surface_vertices)
        - min(vertex.co.z for vertex in top_surface_vertices)
        if top_surface_vertices else 0.0
    )
    mountain_overlap_ground_minimum = min(
        (
            vertex.co.z
            for vertex in top_surface_vertices
            if hypot(vertex.co.x, vertex.co.y - 60.0) >= 350.0
        ),
        default=float("-inf"),
    )
    mountain_burial_clearance = (
        mountain_overlap_ground_minimum - max(mountain_edge_tops)
    )
    ground_envelope_ready = (
        ground_mesh is not None
        and ground_mesh.get("dcc_composite_terrain") is True
        and ground_mesh.get("top_radial_samples")
            == GROUND_COMPOSITE_RADIAL_SAMPLES
        and ground_mesh.get("top_angular_samples")
            == GROUND_COMPOSITE_ANGLE_SAMPLES
        and ground_mesh.get("top_surface_face_count")
            == (GROUND_COMPOSITE_RADIAL_SAMPLES - 1)
                * GROUND_COMPOSITE_ANGLE_SAMPLES * 2
        and ground_mesh.get("top_surface_quad_count")
            == (GROUND_COMPOSITE_RADIAL_SAMPLES - 1)
                * GROUND_COMPOSITE_ANGLE_SAMPLES
        and ground_mesh.get("top_diagonal_orientation_a") == 41_400
        and ground_mesh.get("top_diagonal_orientation_b") == 41_400
        and ground_mesh.get("boundary_skirts_buried") is True
        and ground_mesh.get("coast_projected_flip_count") == 0
        and 13.0 <= composite_top_relief <= 18.0
        and ground_mesh.get("height_residual_rms", 0.0) >= 0.005
        and ground_mesh.get("height_residual_maximum", 0.0) >= 0.01
        and ground_mesh.get("asset_height_residual_rms", 0.0) >= 0.03
        and 0.05 <= ground_mesh.get("inner_band_height_p10_p90", 0.0) <= 0.40
        and ground_mesh.get("outer_band_height_p10_p90", 0.0) >= 0.45
        and ground_mesh.get("surface_slope_rms", 0.0) >= 0.03
        and ground_mesh.get("surface_slope_p90", 0.0) >= 0.03
        and ground_mesh.get("surface_slope_p99", float("inf")) < 0.30
        and ground_mesh.get("surface_slope_maximum", float("inf")) < 0.80
        and ground_mesh.get("surface_normal_z_standard_deviation", 0.0) >= 0.003
        and ground_mesh.get("surface_normal_z_p10", 1.0) <= 0.9993
        and ground_mesh.get("radial_spacing_rms_deviation", 0.0) >= 0.25
        and ground_mesh.get("foundation_signed_distance_mask") is True
        and ground_mesh.get("foundation_footprint_top_face_count") == 25
        and ground_mesh.get("foundation_footprint_boundary_edge_count") == 16
        and ground_mesh.get("foundation_safe_margin_meters")
            == GROUND_FOUNDATION_MARGIN
        and ground_mesh.get("foundation_relief_end_distance_meters")
            == GROUND_FOUNDATION_RELIEF_END_DISTANCE
        and ground_mesh.get("foundation_relief_full_gain_distance_meters")
            == GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE
        and ground_mesh.get("safe_inner_top_maximum", float("inf")) <= -0.08
        and 0.95 <= ground_mesh.get(
            "foundation_near_0_60_height_p10_p90", 0.0
        ) <= 2.50
        and 3.00 <= ground_mesh.get(
            "foundation_mid_60_160_height_p10_p90", 0.0
        ) <= 6.00
        and ground_mesh.get("asset_lowpass_passes") == 14
        and ground_mesh.get("asset_broad_passes") == 32
        and ground_mesh.get("broad_relief_gain_minimum") == 4.0
        and ground_mesh.get("broad_relief_gain_maximum") == 40.0
        and ground_mesh.get("lowpass_relief_gain_minimum") == 1.2
        and ground_mesh.get("lowpass_relief_gain_maximum") == 10.0
        and ground_mesh.get("relief_gain_easing")
            == "C1 foundation-distance near boost and post-160m smoothstep"
        and 3.50 <= ground_mesh.get("height_p10_p90_400_500", 0.0) <= 9.00
        and 4.50 <= ground_mesh.get("height_p10_p90_500_560", 0.0) <= 14.00
        and 5.00 <= ground_mesh.get("height_p10_p90_560_601", 0.0) <= 14.00
        and ground_mesh.get("safe_transition_slope_p95", float("inf")) < 0.30
        and ground_mesh.get("transition_boundary_slope_p95", float("inf")) < 0.20
        and ground_mesh.get("normal_readability_gate")
            == (
                "asset-derived relief bands paired with slope p99 < 0.30, "
                "slope maximum < 0.80, and finite normal variation"
            )
        and ground_mesh.get("height_source_method")
            == (
                "Foundation-footprint signed-distance blend of low-pass and broad "
                "residual extracted exclusively from eight transformed Coast Line "
                "01 scans; no synthetic height noise"
            )
    )
    ground_uv = (
        valley_coast_uv_statistics(ground_mesh)
        if ground_mesh is not None
        else {
            "layer_count": 0,
            "loop_count": 0,
            "finite": False,
            "maximum_error": float("inf"),
        }
    )
    ground_uv_ready = (
        ground_mesh is not None
        and ground_mesh.get("continuous_planar_uv") is True
        and ground_mesh.get("continuous_world_uv_warp") is False
        and ground_mesh.get("continuous_uv_layer") == COAST_UV_LAYER_NAME
        and abs(ground_mesh.get("uv_tile_size_local", 0.0) - COAST_UV_TILE_SIZE_LOCAL)
            <= 0.000001
        and {layer.name for layer in ground_mesh.uv_layers} == {COAST_UV_LAYER_NAME}
        and ground_uv["loop_count"] == len(ground_mesh.loops)
        and ground_uv["finite"] is True
        and ground_uv["maximum_error"] <= 0.00001
        and abs(ground_mesh.get("uv_normalized_jacobian_minimum", 0.0) - 1.0)
            <= 0.000001
        and abs(ground_mesh.get("uv_normalized_jacobian_maximum", 0.0) - 1.0)
            <= 0.000001
        and ground_mesh.get("uv_mapping_method") == COAST_UV_MAPPING_METHOD
        and ground_mesh.get("uv_macro_warp_method") is None
    )
    ground_world_uv_tiles = [
        COAST_UV_TILE_SIZE_LOCAL * ground.scale.x for ground in ground_scans
    ]
    ground_world_uv_scale_ready = (
        min(ground_world_uv_tiles, default=0.0) >= 6.999
        and max(ground_world_uv_tiles, default=float("inf")) <= 7.001
    )
    ground_materials = (
        [material for material in ground_mesh.materials if material is not None]
        if ground_mesh is not None
        else []
    )
    coast_surface_material = ground_materials[0] if len(ground_materials) == 1 else None
    gravel_surface_material = bpy.data.materials.get("JianghaiCompactedGroundPBR")
    coast_surface_images = {
        node.image
        for node in (
            coast_surface_material.node_tree.nodes
            if coast_surface_material is not None
            and coast_surface_material.use_nodes
            and coast_surface_material.node_tree is not None
            else []
        )
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    gravel_surface_images = {
        node.image
        for node in (
            gravel_surface_material.node_tree.nodes
            if gravel_surface_material is not None
            and gravel_surface_material.use_nodes
            and gravel_surface_material.node_tree is not None
            else []
        )
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    legacy_coast_materials = [
        material.name
        for material in bpy.data.materials
        if material.name.startswith("JianghaiCoastLine01PBR")
        or material.get("source_url") == "https://polyhaven.com/a/coast_line_01"
    ]
    legacy_coast_images = [
        image.name
        for image in bpy.data.images
        if image.name.startswith("coast_line_01_")
        or image.get("source_url") == "https://polyhaven.com/a/coast_line_01"
    ]
    ground_surface_ready = (
        coast_surface_material is not None
        and coast_surface_material.name == COAST_SURFACE_MATERIAL_NAME
        and valley_source_metadata_ready(
            coast_surface_material,
            "https://polyhaven.com/a/gravel_floor_03",
            "Charlotte Baglioni",
            "2026-08-28",
            "CC0 1.0 Universal",
        )
        and coast_surface_material.get("surface_asset_id") == "gravel_floor_03"
        and coast_surface_material.get("surface_source_md5s")
            == GRAVEL_SURFACE_SOURCE_MD5S
        and tuple(coast_surface_material.get("base_color_factor", ()))
            == COAST_BASE_COLOR_FACTOR
        and coast_surface_material.get("continuous_uv_map") == COAST_UV_LAYER_NAME
        and len([
            node for node in coast_surface_material.node_tree.nodes
            if node.type == "UVMAP" and node.uv_map == COAST_UV_LAYER_NAME
        ]) == 1
        and len(coast_surface_images) == 3
        and coast_surface_images == gravel_surface_images
        and {image.get("source_md5") for image in coast_surface_images}
            == GRAVEL_SURFACE_IMAGE_MD5S
        and len([
            node for node in coast_surface_material.node_tree.nodes
            if node.name == "JianghaiCoastBaseColorFactor"
            and node.type == "MIX_RGB"
            and node.blend_type == "MULTIPLY"
            and abs(node.inputs[0].default_value - 1.0) <= 0.000001
            and all(
                abs(node.inputs[2].default_value[index] - value) <= 0.000001
                for index, value in enumerate(COAST_BASE_COLOR_FACTOR)
            )
        ]) == 1
        and bpy.data.materials.get("JianghaiCoastRockyTerrainPBR") is None
        and not legacy_coast_materials
        and not legacy_coast_images
    )
    ground_local_top = (
        max(vertex.co.z for vertex in ground_mesh.vertices)
        if ground_mesh is not None
        else 0.0
    )
    ground_layout_ready = (
        len(ground_scans) == GROUND_INSTANCE_COUNT
        and all(
            ground.name == "JianghaiPerimeterGroundComposite"
            and ground.location.length <= LAYOUT_POSITION_TOLERANCE
            and all(abs(value - 1.0) <= 0.000001 for value in ground.scale)
            and all(abs(value) <= 0.000001 for value in ground.rotation_euler)
            for ground in ground_scans
        )
    )
    ground_source_ready = all(
        valley_asset_instance_ready(
            ground, asset_specs["coast_line_01"], require_asset_id=False
        )
        for ground in ground_scans
    )
    mountain_source_ready = (
        len(mountain_meshes) == 1
        and all(
            valley_asset_instance_ready(
                mountain, asset_specs["hero_mountain"], require_asset_id=True
            )
            for mountain in mountains
        )
    )
    mountain_mesh = next(iter(mountain_meshes))
    mountain_local_minimum = min(vertex.co.z for vertex in mountain_mesh.vertices)
    mountain_layout_ready = all(
        abs(mountain.location.x - layout[0][0]) <= LAYOUT_POSITION_TOLERANCE
        and abs(mountain.location.y - layout[0][1]) <= LAYOUT_POSITION_TOLERANCE
        and abs(
            mountain.location.z
            - (-0.20 - layout[3] - mountain_local_minimum * layout[1])
        ) <= LAYOUT_POSITION_TOLERANCE
        and all(abs(value - layout[1]) <= 0.000001 for value in mountain.scale)
        and abs(mountain.rotation_euler.x) <= 0.000001
        and abs(mountain.rotation_euler.y) <= 0.000001
        and abs(mountain.rotation_euler.z - layout[2]) <= 0.000001
        and abs(mountain.get("explicit_yaw_radians", -100.0) - layout[2]) <= 0.000001
        and mountain.get("mountain_ring") == layout[4]
        and abs(mountain.get("mountain_ring_radius_meters", 0.0)
                - (630.0 if layout[4] == "inner" else 780.0)) <= 0.000001
        and abs(mountain.get("embedded_depth_meters", 0.0) - layout[3]) <= 0.000001
        for mountain, layout in zip(mountains, MOUNTAIN_LAYOUT, strict=True)
    )
    inner_ring_angular_gap = valley_location_angular_gap(mountains[:6])
    outer_ring_angular_gap = valley_location_angular_gap(mountains[6:])
    mountain_ring_angles_ready = (
        abs(inner_ring_angular_gap - pi / 3.0) <= 0.000001
        and abs(outer_ring_angular_gap - pi / 3.0) <= 0.000001
    )
    mountain_boundary_height_delta = max(
        vertex.co.z - mountain_local_minimum
        for vertex in mountain_mesh.vertices
        if max(abs(vertex.co.x), abs(vertex.co.y)) >= 0.99
    )
    uniform_positive_scales = all(
        valley_uniform_positive_scale(obj) for obj in [*ground_scans, *mountains]
    )
    ground_coverage = valley_perimeter_ground_coverage(foundation, ground_scans)
    ground_continuity = valley_ground_continuity_statistics(foundation, ground_scans)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    north_edge_seam = valley_north_edge_seam_statistics(depsgraph)
    south_ground_seam = valley_south_ground_seam_statistics(
        ground_scans[0], depsgraph
    )
    screen_relief = valley_ground_screen_relief_statistics(
        ground_scans[0], depsgraph
    )
    camera_clearances = []
    for godot_camera in (
        Vector((205.0, 3.2, 145.0)),
        Vector((112.0, 1.65, 86.0)),
        Vector((-118.0, 1.65, -207.0)),
    ):
        blender_camera = valley_godot_to_blender(godot_camera)
        surface_height = valley_highest_ground_height(
            blender_camera.x,
            blender_camera.y,
            ground_scans,
            depsgraph,
        )
        camera_clearances.append(
            float("-inf")
            if surface_height is None
            else blender_camera.z - surface_height
        )
    north_edge_endcaps_ready = True
    north_edge_boundary_top = float("-inf")
    for object_name in NORTH_EDGE_OBJECT_NAMES:
        obj = bpy.data.objects.get(object_name)
        if obj is None or obj.type != "MESH":
            north_edge_endcaps_ready = False
            continue
        points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        maximum_y = max(point.y for point in points)
        boundary_heights = [
            point.z for point in points if point.y >= maximum_y - 0.001
        ]
        upstream_heights = [
            point.z
            for point in points
            if point.y <= maximum_y - NORTH_EDGE_BLEND_LENGTH - 0.001
        ]
        boundary_top = max(boundary_heights, default=float("inf"))
        boundary_thickness = boundary_top - min(
            boundary_heights, default=float("-inf")
        )
        north_edge_boundary_top = max(north_edge_boundary_top, boundary_top)
        print(
            "JIANGHAI_DCC_NORTH_ENDCAP "
            f"object={object_name} boundary_top={boundary_top:.6f} "
            f"thickness={boundary_thickness:.6f} "
            f"upstream_top={max(upstream_heights, default=float('inf')):.6f} "
            f"metadata=({obj.get('north_endcap_dcc_buried')},"
            f"{obj.get('north_endcap_blend_length_meters')},"
            f"{obj.get('north_endcap_target_top')})"
        )
        north_edge_endcaps_ready = north_edge_endcaps_ready and (
            obj.get("north_endcap_dcc_buried") is True
            and abs(
                obj.get("north_endcap_blend_length_meters", 0.0)
                - NORTH_EDGE_BLEND_LENGTH
            ) <= 0.000001
            and abs(
                obj.get("north_endcap_target_top", 0.0)
                - NORTH_EDGE_TARGET_TOP
            ) <= 0.000001
            and boundary_top <= NORTH_EDGE_TARGET_TOP + 0.00001
            and abs(
                boundary_thickness - NORTH_EDGE_END_THICKNESSES[object_name]
            ) <= 0.00001
            and abs(
                max(upstream_heights, default=float("inf"))
                - NORTH_EDGE_UPSTREAM_TOPS[object_name]
            ) <= 0.00001
        )
    legacy_valley_data = [
        datablock.name
        for collection in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.images)
        for datablock in collection
        if any(
            token in datablock.name.lower()
            for token in ("mountainside", "mountaincliff", "coastal_cliff", "namaqualand")
        )
    ]
    hero_material = bpy.data.materials.get("JianghaiHeroMountainPBR")
    hero_material_ready = (
        hero_material is not None
        and hero_material.use_nodes
        and hero_material.node_tree is not None
        and len([
            node for node in hero_material.node_tree.nodes
            if node.type == "TEX_IMAGE" and node.image is not None
        ]) == 3
        and not any(
            node.type == "DISPLACEMENT" for node in hero_material.node_tree.nodes
        )
        and len([
            node for node in hero_material.node_tree.nodes
            if node.type == "OUTPUT_MATERIAL"
            and not node.inputs["Displacement"].is_linked
        ]) == 1
        and all(not mountain.modifiers for mountain in mountains)
    )
    foundation_materials = {
        material.name: material
        for material in foundation.data.materials
        if material is not None
    }
    foundation_material_specs = {
        "JianghaiCompactedGroundPBR": (
            "https://polyhaven.com/a/gravel_floor_03",
            "Charlotte Baglioni",
        ),
        "JianghaiRockyValleyPBR": (
            "https://polyhaven.com/a/rocky_terrain",
            "Amal Kumar",
        ),
    }
    foundation_materials_ready = all(
        material_name in foundation_materials
        and valley_opaque_material_ready(
            foundation_materials[material_name], source_url, creators, "2026-08-28"
        )
        for material_name, (source_url, creators) in foundation_material_specs.items()
    )
    valley_objects = [root] + list(root.children_recursive)
    valid = (
        root.parent == bpy.data.objects.get("JianghaiOldCityAuthoredScene")
        and foundation.parent == root
        and all(ground.parent == root for ground in ground_scans)
        and all(mountain.parent == root for mountain in mountains)
        and len(ground_meshes) == 1
        and len(mountain_meshes) == 1
        and ground_source_ready
        and ground_repair_ready
        and ground_envelope_ready
        and ground_uv_ready
        and ground_world_uv_scale_ready
        and ground_surface_ready
        and ground_layout_ready
        and mountain_source_ready
        and mountains_outside
        and max_angular_gap <= 0.55
        and maximum_inner_ring_radius <= 360.0
        and maximum_outer_ring_radius <= 500.0
        and mountain_layout_ready
        and mountain_ring_angles_ready
        and min(mountain_bottoms) >= -22.1
        and max(mountain_bottoms) <= -17.9
        and max(mountain_edge_tops) <= -16.0
        and mountain_boundary_height_delta <= 0.005
        and mountain_mesh.get("boundary_tapered_for_valley_overlap") is True
        and uniform_positive_scales
        and foundation_triangles == 188
        and len(foundation.data.vertices) == 96
        and 320_000 <= instance_triangles <= 370_000
        and extent.x >= 1_750.0
        and extent.y >= 1_750.0
        and extent.z >= 60.0
        and foundation_extent.x >= 339.0
        and foundation_extent.y >= 319.0
        and 0.05 <= foundation_extent.z <= 0.25
        and -0.08 <= foundation_maximum.z <= -0.04
        and ground_extent.x >= 700.0
        and ground_extent.y >= 700.0
        and ground_mesh.get("safe_inner_top_maximum", float("inf")) <= -0.08
        and min(camera_clearances, default=float("-inf")) >= 0.75
        and ground_coverage >= 0.98
        and ground_continuity["foundation_edge_coverage"] >= 0.98
        and ground_continuity["foundation_edge_maximum_gap"] >= 0.10
        and ground_continuity["foundation_edge_maximum_gap"] <= 0.35
        and ground_continuity["ring_coverage"] == 1.0
        and ground_continuity["ring_minimum_height"] >= -18.0
        and ground_continuity["ring_maximum_height"] <= 18.0
        and ground_continuity["multi_top_hits"] == 0
        and ground_continuity["near_coplanar_double_top_hits"] == 0
        and ground_continuity["vertical_skirt_highest_hits"] == 0
        and ground_continuity["player_view_visible_skirt_hits"] == 0
        and ground_continuity["player_view_near_coplanar_double_top_hits"] == 0
        and south_ground_seam["south_ground_top_hits"]
            == south_ground_seam["south_ground_samples"]
        and south_ground_seam["south_ground_distant_side_hits"] == 0
        and all(metric["samples"] >= 100 for metric in screen_relief)
        and all(metric["height_p10_p90"] >= 0.90 for metric in screen_relief)
        and all(metric["normal_z_std"] >= 0.0005 for metric in screen_relief)
        and north_edge_endcaps_ready
        and north_edge_seam["north_edge_hits"]
            == north_edge_seam["north_edge_samples"]
        and north_edge_seam["north_edge_distant_side_hits"] == 0
        and mountain_burial_clearance >= 0.5
        and root.get("collision_role") == "visual_only"
        and root.get("composition_license") == "MIT"
        and root.get("source_asset_licenses") == "CC0 1.0 Universal; CC BY 4.0"
        and root.get("coast_line_01_source_url") == "https://polyhaven.com/a/coast_line_01"
        and root.get("hero_mountain_source_url")
            == "https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca"
        and root.get("hero_mountain_source_creator") == "solararchitect"
        and root.get("hero_mountain_source_license") == "CC BY 4.0"
        and root.get("hero_mountain_source_md5s")
            == (
                "obj=af949f14c8fb8138bf75f2a70769b2be; "
                "color=1480eb4cadc8c531055b0b39ea5ab50d; "
                "normal=7f16993db123397c80fcec42e586729b; "
                "roughness=e46afb87a2dbe6c2843eb14864245ffe"
            )
        and root.get("rocky_terrain_source_url") == "https://polyhaven.com/a/rocky_terrain"
        and root.get("gravel_floor_source_url") == "https://polyhaven.com/a/gravel_floor_03"
        and root.get("north_edge_dcc_endcaps")
            == "; ".join(NORTH_EDGE_OBJECT_NAMES)
        and abs(
            root.get("north_edge_blend_length_meters", 0.0)
            - NORTH_EDGE_BLEND_LENGTH
        ) <= 0.000001
        and abs(
            root.get("north_edge_target_top", 0.0) - NORTH_EDGE_TARGET_TOP
        ) <= 0.000001
        and root.get("north_edge_boundary_top", float("inf"))
            <= NORTH_EDGE_TARGET_TOP + 0.00001
        and root.get("acquisition_dates") == "2026-08-28; 2026-08-29"
        and all(ground.get("collision_role") == "visual_only" for ground in ground_scans)
        and all(
            mountain.get("collision_role") == "visual_only"
            and mountain.get("visual_role") == "authored_distant_mountain"
            and mountain.get("source_license") == "CC BY 4.0"
            and mountain.get("explicit_yaw_radians") is not None
            for mountain in mountains
        )
        and foundation.get("collision_role") == "visual_only"
        and foundation.get("geometry_license") == "MIT"
        and foundation.get("surface_asset_license") == "CC0 1.0 Universal"
        and foundation.get("acquisition_date") == "2026-08-28"
        and {layer.name for layer in foundation.data.uv_layers} >= {"GroundUV", "MountainUV"}
        and foundation_materials_ready
        and {ground.name for ground in ground_scans}
            == {"JianghaiPerimeterGroundComposite"}
        and {mountain.name for mountain in mountains}
            == {f"JianghaiMountainMassif{index:02d}" for index in range(12)}
        and all(count == GROUND_EXPECTED_TRIANGLES for count in ground_triangle_counts)
        and all(count == 14_000 for count in mountain_triangle_counts)
        and not legacy_valley_data
        and hero_material_ready
        and not any(obj.name.lower().endswith(("-col", "-convcol")) for obj in valley_objects)
    )
    if not valid:
        raise RuntimeError(
            "Jianghai valley environment failed export validation: "
            f"foundation={foundation_triangles} "
            f"ground={len(ground_scans)}:{sorted(set(ground_triangle_counts))} "
            f"ground_components={ground_topology['connected_components']} "
            f"ground_boundary={ground_topology['boundary_components']}:"
            f"{ground_topology['boundary_edges']} "
            f"ground_boundary_z=({ground_topology['boundary_minimum_z']:.3f},"
            f"{ground_topology['boundary_maximum_z']:.3f}) "
            f"ground_degenerate={ground_topology['degenerate_faces']} "
            f"ground_invalid_normals={ground_topology['invalid_face_normals']} "
            f"ground_max_edge={ground_topology['maximum_terrain_edge']:.3f} "
            f"ground_max_edge_area_ratio="
            f"{ground_topology['maximum_terrain_edge_area_ratio']:.3f} "
            f"ground_repair={ground_repair_ready} "
            f"ground_envelope={ground_envelope_ready} "
            f"ground_composite_relief={composite_top_relief:.3f} "
            f"ground_height_residual=("
            f"{ground_mesh.get('height_residual_rms', 0.0):.4f},"
            f"{ground_mesh.get('height_residual_maximum', 0.0):.4f}) "
            f"ground_uv={ground_uv['layer_count']}:{ground_uv['loop_count']} "
            f"ground_uv_error={ground_uv['maximum_error']:.8f} "
            f"ground_uv_ready={ground_uv_ready} "
            f"ground_world_uv_tile=({min(ground_world_uv_tiles):.3f},"
            f"{max(ground_world_uv_tiles):.3f}) "
            f"ground_world_uv_scale={ground_world_uv_scale_ready} "
            f"ground_surface={ground_surface_ready} "
            f"legacy_coast_materials={len(legacy_coast_materials)} "
            f"legacy_coast_images={len(legacy_coast_images)} "
            f"ground_layout={ground_layout_ready} "
            f"mountains={len(mountains)}:{sorted(set(mountain_triangle_counts))} "
            f"instances={instance_triangles} "
            f"extent={tuple(round(value, 2) for value in extent)} "
            f"ground_extent={tuple(round(value, 2) for value in ground_extent)} "
            f"ground_top={ground_maximum.z:.3f} coverage={ground_coverage:.3f} "
            f"foundation_edge={ground_continuity['foundation_edge_hits']}/"
            f"{ground_continuity['foundation_edge_samples']}:"
            f"gap={ground_continuity['foundation_edge_maximum_gap']:.3f} "
            f"ground_ring={ground_continuity['ring_hits']}/"
            f"{ground_continuity['ring_samples']}:height=("
            f"{ground_continuity['ring_minimum_height']:.3f},"
            f"{ground_continuity['ring_maximum_height']:.3f}) "
            f"ground_top_surface={ground_continuity['top_surface_hits']}/"
            f"{ground_continuity['ring_samples']} "
            f"ground_multi_top={ground_continuity['multi_top_hits']} "
            f"ground_near_double_top="
            f"{ground_continuity['near_coplanar_double_top_hits']} "
            f"ground_vertical_skirt_highest="
            f"{ground_continuity['vertical_skirt_highest_hits']} "
            f"ground_player_view_skirt="
            f"{ground_continuity['player_view_visible_skirt_hits']}/"
            f"{ground_continuity['player_view_samples']} "
            f"ground_foundation_occluded_skirt="
            f"{ground_continuity['player_view_foundation_occluded_skirt_hits']} "
            f"ground_player_view_near_double_top="
            f"{ground_continuity['player_view_near_coplanar_double_top_hits']} "
            f"outside={mountains_outside} legacy={legacy_valley_data} "
            f"inner_ring_radius={maximum_inner_ring_radius:.1f} "
            f"outer_ring_radius={maximum_outer_ring_radius:.1f} "
            f"layout={mountain_layout_ready} "
            f"ring_gaps=({inner_ring_angular_gap:.3f},"
            f"{outer_ring_angular_gap:.3f}) "
            f"ring_angles={mountain_ring_angles_ready} "
            f"bottoms=({min(mountain_bottoms):.1f},{max(mountain_bottoms):.1f}) "
            f"edge_top={max(mountain_edge_tops):.1f} "
            f"boundary_delta={mountain_boundary_height_delta:.4f} "
            f"hero_material={hero_material_ready} "
            f"angular_gap={max_angular_gap:.3f} metadata="
            f"{ground_source_ready and mountain_source_ready and foundation_materials_ready} "
            f"uniform_scales={uniform_positive_scales}"
        )
    screen_relief_report = ",".join(
        f"{metric['height_p10_p90']:.3f}/"
        f"{metric['normal_z_std']:.5f}/"
        f"{metric['samples']}"
        for metric in screen_relief
    )
    print(
        "JIANGHAI_VALLEY_EXPORT_CHECK valid=True "
        f"ground={len(ground_scans)}:{ground_triangle_counts[0]} "
        f"topology={ground_topology['connected_components']}:"
        f"{ground_topology['boundary_components']}:"
        f"{ground_topology['boundary_edges']} "
        f"edge={ground_topology['maximum_terrain_edge']:.3f} "
        f"edge_area={ground_topology['maximum_terrain_edge_area_ratio']:.3f} "
        f"diagonals=({ground_mesh.get('top_diagonal_orientation_a', 0)},"
        f"{ground_mesh.get('top_diagonal_orientation_b', 0)}) "
        f"relief=(inner={ground_mesh.get('inner_band_height_p10_p90', 0.0):.3f},"
        f"outer={ground_mesh.get('outer_band_height_p10_p90', 0.0):.3f},"
        f"300_400={ground_mesh.get('height_p10_p90_300_400', 0.0):.3f},"
        f"500_560={ground_mesh.get('height_p10_p90_500_560', 0.0):.3f},"
        f"560_601={ground_mesh.get('height_p10_p90_560_601', 0.0):.3f}) "
        f"slope=({ground_mesh.get('surface_slope_rms', 0.0):.4f},"
        f"{ground_mesh.get('surface_slope_p90', 0.0):.4f},"
        f"{ground_mesh.get('surface_slope_maximum', 0.0):.4f}) "
        f"normal=({ground_mesh.get('surface_normal_z_standard_deviation', 0.0):.5f},"
        f"{ground_mesh.get('surface_normal_z_p10', 1.0):.5f}) "
        f"uv={ground_uv['loop_count']}:{ground_uv['maximum_error']:.8f} "
        f"jacobian=({ground_mesh.get('uv_normalized_jacobian_minimum', 0.0):.4f},"
        f"{ground_mesh.get('uv_normalized_jacobian_maximum', 0.0):.4f}) "
        f"gap={ground_continuity['foundation_edge_maximum_gap']:.3f} "
        f"ring={ground_continuity['ring_hits']}/{ground_continuity['ring_samples']} "
        f"multi={ground_continuity['multi_top_hits']} "
        f"near={ground_continuity['near_coplanar_double_top_hits']} "
        f"skirt={ground_continuity['vertical_skirt_highest_hits']}:"
        f"{ground_continuity['player_view_visible_skirt_hits']} "
        f"triangles={instance_triangles}"
    )
    return len(ground_scans), len(mountains), instance_triangles


def export_refinery_door() -> None:
    source = bpy.data.objects.get("JianghaiArtPass_EastShutter00")
    if source is None or source.type != "MESH":
        raise RuntimeError("The authored rollershutter source mesh is missing")

    duplicate = source.copy()
    duplicate.data = source.data
    duplicate.name = "JianghaiRollerShutterDoor"
    bpy.context.scene.collection.objects.link(duplicate)
    duplicate.location = (0.0, 0.0, 0.0)
    duplicate.rotation_euler = (0.0, 0.0, 0.0)
    duplicate.scale = (1.0, 1.0, 1.0)
    bpy.ops.object.select_all(action="DESELECT")
    duplicate.select_set(True)
    bpy.context.view_layer.objects.active = duplicate
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(REFINERY_DOOR_GLB_PATH),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_yup=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
        )
    finally:
        bpy.data.objects.remove(duplicate, do_unlink=True)


def validate_density_color0_glb() -> dict[str, int]:
    data = GLB_PATH.read_bytes()
    if data[:4] != b"glTF" or int.from_bytes(data[4:8], "little") != 2:
        raise RuntimeError("Jianghai runtime output is not a GLB 2.0 file")
    json_length = int.from_bytes(data[12:16], "little")
    if data[16:20] != b"JSON":
        raise RuntimeError("Jianghai runtime GLB is missing its JSON chunk")
    document = json.loads(data[20 : 20 + json_length].decode("utf-8"))
    nodes = {node.get("name"): node for node in document.get("nodes", [])}
    meshes = document.get("meshes", [])
    materials = document.get("materials", [])
    accessors = document.get("accessors", [])
    profile_meshes: dict[str, set[int]] = {
        profile_name: set() for profile_name in QUATERNIUS_DENSITY_MESHES
    }
    profile_materials: dict[str, set[int]] = {
        profile_name: set() for profile_name in QUATERNIUS_DENSITY_MESHES
    }
    instance_surfaces = 0
    infill_surfaces = 0
    expected_infill_names = {
        f"JianghaiDensity_{suffix}" for suffix in DENSITY_COLOR0_INFILL_SUFFIXES
    }
    for suffix, profile_name, _, _, _ in DENSITY_BUILDING_LAYOUT:
        if profile_name not in QUATERNIUS_DENSITY_MESHES:
            continue
        object_name = f"JianghaiDensity_{suffix}"
        node = nodes.get(object_name)
        if node is None or "mesh" not in node:
            raise RuntimeError(f"GLB lost COLOR_0 density node: {object_name}")
        mesh_index = int(node["mesh"])
        profile_meshes[profile_name].add(mesh_index)
        primitives = meshes[mesh_index].get("primitives", [])
        if len(primitives) != 1:
            raise RuntimeError(
                f"GLB COLOR_0 density surface count drifted: {object_name} "
                f"actual={len(primitives)} expected=1"
            )
        primitive = primitives[0]
        attributes = primitive.get("attributes", {})
        if DENSITY_COLOR0_ATTRIBUTE not in attributes:
            raise RuntimeError(f"GLB lost COLOR_0 density colors: {object_name}")
        color_accessor = accessors[int(attributes[DENSITY_COLOR0_ATTRIBUTE])]
        if color_accessor.get("type") not in {"VEC3", "VEC4"}:
            raise RuntimeError(
                f"GLB density COLOR_0 format drifted: {object_name} "
                f"type={color_accessor.get('type')}"
            )
        material_index = primitive.get("material")
        if material_index is None:
            raise RuntimeError(f"GLB density COLOR_0 material is missing: {object_name}")
        material_index = int(material_index)
        profile_materials[profile_name].add(material_index)
        material = materials[material_index]
        pbr = material.get("pbrMetallicRoughness", {})
        if (
            material.get("name") != DENSITY_COLOR0_PROFILE_MATERIALS[profile_name]
            or material.get("alphaMode", "OPAQUE") != "OPAQUE"
            or "baseColorTexture" in pbr
            or abs(
                float(pbr.get("roughnessFactor", 1.0))
                - DENSITY_COLOR0_PROFILE_ROUGHNESS[profile_name]
            )
            > 1.0e-5
            or abs(float(pbr.get("metallicFactor", 1.0))) > 1.0e-5
        ):
            raise RuntimeError(
                f"GLB density COLOR_0 material contract drifted: {object_name}"
            )
        instance_surfaces += 1
        if object_name in expected_infill_names:
            infill_surfaces += 1
    if (
        any(len(indices) != 1 for indices in profile_meshes.values())
        or len({next(iter(indices)) for indices in profile_meshes.values()}) != 4
        or any(len(indices) != 1 for indices in profile_materials.values())
        or len({next(iter(indices)) for indices in profile_materials.values()}) != 4
        or instance_surfaces != 22
        or infill_surfaces != 8
    ):
        raise RuntimeError(
            "GLB density COLOR_0 sharing contract drifted: "
            f"meshes={profile_meshes} materials={profile_materials} "
            f"surfaces={instance_surfaces} infill={infill_surfaces}"
        )
    return {
        "profiles": len(profile_meshes),
        "profile_surfaces": len(profile_meshes),
        "instances": instance_surfaces,
        "instance_surfaces": instance_surfaces,
        "infill_instances": len(expected_infill_names),
        "infill_surfaces": infill_surfaces,
    }


def validate_runtime_glb_roundtrip() -> dict[str, float | int]:
    """Re-import the GLB and hard-lock the runtime Coast composite geometry."""
    density_color0_glb = validate_density_color0_glb()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(GLB_PATH))
    ground = bpy.data.objects.get("JianghaiPerimeterGroundComposite")
    foundation = bpy.data.objects.get("OldCityFoundation")
    mountains = sorted(
        (obj for obj in bpy.data.objects if obj.name.startswith("JianghaiMountainMassif")),
        key=lambda obj: obj.name,
    )
    interior_liners = sorted(
        (
            obj
            for obj in bpy.data.objects
            if obj.name.startswith(LINER_OBJECT_PREFIX) and obj.type == "MESH"
        ),
        key=lambda obj: obj.name,
    )
    liner_meshes = {liner.data for liner in interior_liners}
    liner_mesh = next(iter(liner_meshes), None)
    if liner_mesh is not None:
        liner_mesh.calc_loop_triangles()
    liner_materials = (
        [material for material in liner_mesh.materials if material is not None]
        if liner_mesh is not None
        else []
    )
    liner_materials_opaque = all(
        material.diffuse_color[3] >= 0.9999
        and all(
            node.inputs.get("Alpha") is None
            or (
                not node.inputs["Alpha"].is_linked
                and node.inputs["Alpha"].default_value >= 0.9999
            )
            for node in (
                material.node_tree.nodes
                if material.use_nodes and material.node_tree is not None
                else []
            )
            if node.type == "BSDF_PRINCIPLED"
        )
        for material in liner_materials
    )
    liner_roundtrip_ready = (
        len(interior_liners) == len(ENTERABLE_RESIDENCE_LAYOUT)
        and len(liner_meshes) == 1
        and liner_mesh is not None
        and len(liner_mesh.loop_triangles) == INTERIOR_LINER_EXPECTED_TRIANGLES
        and len(liner_materials) == 2
        and liner_materials_opaque
        and all(
            liner.get("jianghai_interior_liner") is True
            and liner.get("jianghai_liner_opaque") is True
            and abs(
                float(liner.get("jianghai_liner_visibility_m", 0.0))
                - LINER_VISIBILITY_METERS
            )
            <= 0.0001
            and liner.get("jianghai_liner_shadow_mode") == "off"
            for liner in interior_liners
        )
    )
    enterable_mesh_pairs_ready = all(
        bpy.data.objects.get(leader_name) is not None
        and bpy.data.objects.get(follower_name) is not None
        and bpy.data.objects[leader_name].data == bpy.data.objects[follower_name].data
        and sorted(
            obj.name
            for obj in bpy.data.objects
            if obj.type == "MESH" and obj.data == bpy.data.objects[leader_name].data
        )
        == sorted((leader_name, follower_name))
        for leader_name, follower_name in ENTERABLE_MESH_SHARE_GROUPS
    )
    if ground is None or ground.type != "MESH" or foundation is None:
        raise RuntimeError("GLB roundtrip lost the Coast composite or foundation")
    mesh = ground.data
    topology = valley_mesh_topology_statistics(mesh)
    triangle_total = valley_triangle_count(mesh)
    uv_layers = list(mesh.uv_layers)
    uv_error = float("inf")
    uv_finite = False
    if len(uv_layers) == 1 and len(uv_layers[0].data) == len(mesh.loops):
        uv_error = 0.0
        uv_finite = True
        for loop in mesh.loops:
            vertex = mesh.vertices[loop.vertex_index]
            uv = uv_layers[0].data[loop.index].uv
            uv_finite = uv_finite and isfinite(uv.x) and isfinite(uv.y)
            uv_error = max(
                uv_error,
                abs(uv.x - coast_uv_coordinates(vertex.co.x, vertex.co.y)[0]),
                abs(uv.y - coast_uv_coordinates(vertex.co.x, vertex.co.y)[1]),
            )
    materials = [material for material in mesh.materials if material is not None]
    surface_images = {
        node.image
        for material in materials
        for node in (
            material.node_tree.nodes
            if material.use_nodes and material.node_tree is not None else []
        )
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    continuity = valley_ground_continuity_statistics(foundation, [ground])
    depsgraph = bpy.context.evaluated_depsgraph_get()
    north_edge_seam = valley_north_edge_seam_statistics(depsgraph)
    south_ground_seam = valley_south_ground_seam_statistics(ground, depsgraph)
    screen_relief = valley_ground_screen_relief_statistics(ground, depsgraph)
    camera_clearances = []
    for godot_camera in (
        Vector((205.0, 3.2, 145.0)),
        Vector((112.0, 1.65, 86.0)),
        Vector((-118.0, 1.65, -207.0)),
    ):
        blender_camera = valley_godot_to_blender(godot_camera)
        surface_height = valley_highest_ground_height(
            blender_camera.x,
            blender_camera.y,
            [ground],
            depsgraph,
        )
        camera_clearances.append(
            float("-inf")
            if surface_height is None
            else blender_camera.z - surface_height
        )
    north_edge_endcaps_ready = True
    north_edge_boundary_top = float("-inf")
    for object_name in NORTH_EDGE_OBJECT_NAMES:
        obj = bpy.data.objects.get(object_name)
        if obj is None or obj.type != "MESH":
            north_edge_endcaps_ready = False
            continue
        points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        maximum_y = max(point.y for point in points)
        boundary_heights = [
            point.z for point in points if point.y >= maximum_y - 0.001
        ]
        upstream_heights = [
            point.z
            for point in points
            if point.y <= maximum_y - NORTH_EDGE_BLEND_LENGTH - 0.001
        ]
        boundary_top = max(boundary_heights, default=float("inf"))
        boundary_thickness = boundary_top - min(
            boundary_heights, default=float("-inf")
        )
        north_edge_boundary_top = max(north_edge_boundary_top, boundary_top)
        print(
            "JIANGHAI_GLB_ROUNDTRIP_NORTH_ENDCAP "
            f"object={object_name} boundary_top={boundary_top:.6f} "
            f"thickness={boundary_thickness:.6f} "
            f"upstream_top={max(upstream_heights, default=float('inf')):.6f} "
            f"metadata=({obj.get('north_endcap_dcc_buried')},"
            f"{obj.get('north_endcap_blend_length_meters')},"
            f"{obj.get('north_endcap_target_top')})"
        )
        north_edge_endcaps_ready = north_edge_endcaps_ready and (
            obj.get("north_endcap_dcc_buried") is True
            and abs(
                obj.get("north_endcap_blend_length_meters", 0.0)
                - NORTH_EDGE_BLEND_LENGTH
            ) <= 0.000001
            and abs(
                obj.get("north_endcap_target_top", 0.0)
                - NORTH_EDGE_TARGET_TOP
            ) <= 0.000001
            and boundary_top <= NORTH_EDGE_TARGET_TOP + 0.00002
            and boundary_thickness > 0.05
            and boundary_thickness
                <= NORTH_EDGE_END_THICKNESSES[object_name] + 0.00002
            and abs(
                max(upstream_heights, default=float("inf"))
                - NORTH_EDGE_UPSTREAM_TOPS[object_name]
            ) <= 0.00005
        )
    top_surface_vertex_count = mesh.get("top_surface_vertex_count", 0)
    top_surface_vertices = list(mesh.vertices[:top_surface_vertex_count])
    composite_top_relief = (
        max(vertex.co.z for vertex in top_surface_vertices)
        - min(vertex.co.z for vertex in top_surface_vertices)
        if top_surface_vertices else 0.0
    )
    mountain_edge_tops = [
        max(
            (mountain.matrix_world @ vertex.co).z
            for vertex in mountain.data.vertices
            if max(abs(vertex.co.x), abs(vertex.co.y)) >= 0.99
        )
        for mountain in mountains
    ]
    mountain_overlap_ground_minimum = min(
        (
            vertex.co.z
            for vertex in top_surface_vertices
            if hypot(vertex.co.x, vertex.co.y - 60.0) >= 350.0
        ),
        default=float("-inf"),
    )
    mountain_burial_clearance = (
        mountain_overlap_ground_minimum - max(mountain_edge_tops, default=float("inf"))
    )
    minimum, maximum = valley_world_bounds([ground])
    extent = maximum - minimum
    valid = (
        triangle_total == GROUND_EXPECTED_TRIANGLES
        and len(mesh.vertices) == GROUND_EXPECTED_VERTICES
        and mesh.get("dcc_composite_terrain") is True
        and mesh.get("dcc_source_scan_count") == 8
        and mesh.get("single_valued_top_surface") is True
        and topology["connected_components"] == 1
        and topology["boundary_edges"] == GROUND_EXPECTED_BOUNDARY_EDGES
        and topology["boundary_components"] == GROUND_EXPECTED_BOUNDARY_COMPONENTS
        and topology["nonmanifold_edges"] == GROUND_EXPECTED_BOUNDARY_EDGES
        and topology["degenerate_faces"] == 0
        and topology["invalid_face_normals"] == 0
        and topology["maximum_terrain_edge"] <= 9.0
        and topology["maximum_terrain_edge_area_ratio"] <= 15.0
        and len(mesh.polygons) == GROUND_EXPECTED_TRIANGLES
        and all(len(polygon.vertices) == 3 for polygon in mesh.polygons)
        and uv_finite
        and uv_error <= COAST_UV_ROUNDTRIP_TOLERANCE
        and mesh.get("continuous_uv_layer") == COAST_UV_LAYER_NAME
        and mesh.get("continuous_planar_uv") is True
        and mesh.get("continuous_world_uv_warp") is False
        and abs(mesh.get("uv_tile_size_local", 0.0) - COAST_UV_TILE_SIZE_LOCAL)
            <= 0.000001
        and mesh.get("uv_mapping_method") == COAST_UV_MAPPING_METHOD
        and mesh.get("uv_macro_warp_method") is None
        and len(materials) == 1
        and materials[0].name == COAST_SURFACE_MATERIAL_NAME
        and materials[0].get("surface_asset_id") == "gravel_floor_03"
        and materials[0].get("surface_source_md5s") == GRAVEL_SURFACE_SOURCE_MD5S
        and tuple(materials[0].get("base_color_factor", ()))
            == COAST_BASE_COLOR_FACTOR
        and materials[0].get("source_url") == "https://polyhaven.com/a/gravel_floor_03"
        and materials[0].get("source_creator") == "Charlotte Baglioni"
        and materials[0].get("continuous_uv_map") == COAST_UV_LAYER_NAME
        and len(surface_images) == 3
        and mesh.get("top_diagonal_orientation_a") == 41_400
        and mesh.get("top_diagonal_orientation_b") == 41_400
        and mesh.get("coast_projected_flip_count") == 0
        and 13.0 <= composite_top_relief <= 18.0
        and mesh.get("height_residual_rms", 0.0) >= 0.005
        and mesh.get("height_residual_maximum", 0.0) >= 0.01
        and mesh.get("asset_height_residual_rms", 0.0) >= 0.03
        and 0.05 <= mesh.get("inner_band_height_p10_p90", 0.0) <= 0.40
        and mesh.get("outer_band_height_p10_p90", 0.0) >= 0.45
        and mesh.get("surface_slope_rms", 0.0) >= 0.03
        and mesh.get("surface_slope_p90", 0.0) >= 0.03
        and mesh.get("surface_slope_p99", float("inf")) < 0.30
        and mesh.get("surface_slope_maximum", float("inf")) < 0.80
        and mesh.get("surface_normal_z_standard_deviation", 0.0) >= 0.003
        and mesh.get("surface_normal_z_p10", 1.0) <= 0.9993
        and mesh.get("radial_spacing_rms_deviation", 0.0) >= 0.25
        and mesh.get("foundation_signed_distance_mask") is True
        and mesh.get("foundation_footprint_top_face_count") == 25
        and mesh.get("foundation_footprint_boundary_edge_count") == 16
        and mesh.get("foundation_safe_margin_meters") == GROUND_FOUNDATION_MARGIN
        and mesh.get("foundation_relief_end_distance_meters")
            == GROUND_FOUNDATION_RELIEF_END_DISTANCE
        and mesh.get("foundation_relief_full_gain_distance_meters")
            == GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE
        and mesh.get("safe_inner_top_maximum", float("inf")) <= -0.08
        and 0.95 <= mesh.get("foundation_near_0_60_height_p10_p90", 0.0) <= 2.50
        and 3.00 <= mesh.get("foundation_mid_60_160_height_p10_p90", 0.0) <= 6.00
        and mesh.get("asset_lowpass_passes") == 14
        and mesh.get("asset_broad_passes") == 32
        and mesh.get("broad_relief_gain_minimum") == 4.0
        and mesh.get("broad_relief_gain_maximum") == 40.0
        and mesh.get("lowpass_relief_gain_minimum") == 1.2
        and mesh.get("lowpass_relief_gain_maximum") == 10.0
        and mesh.get("relief_gain_easing")
            == "C1 foundation-distance near boost and post-160m smoothstep"
        and 3.50 <= mesh.get("height_p10_p90_400_500", 0.0) <= 9.00
        and 4.50 <= mesh.get("height_p10_p90_500_560", 0.0) <= 14.00
        and 5.00 <= mesh.get("height_p10_p90_560_601", 0.0) <= 14.00
        and mesh.get("safe_transition_slope_p95", float("inf")) < 0.30
        and mesh.get("transition_boundary_slope_p95", float("inf")) < 0.20
        and abs(mesh.get("uv_normalized_jacobian_minimum", 0.0) - 1.0)
            <= 0.000001
        and abs(mesh.get("uv_normalized_jacobian_maximum", 0.0) - 1.0)
            <= 0.000001
        and ground.location.length <= LAYOUT_POSITION_TOLERANCE
        and all(abs(value - 1.0) <= 0.000001 for value in ground.scale)
        and all(abs(value) <= 0.000001 for value in ground.rotation_euler)
        and extent.x >= 1_200.0
        and extent.y >= 1_200.0
        and min(camera_clearances, default=float("-inf")) >= 0.75
        and continuity["foundation_edge_coverage"] == 1.0
        and continuity["foundation_edge_maximum_gap"] >= 0.10
        and continuity["foundation_edge_maximum_gap"] <= 0.35
        and continuity["ring_coverage"] == 1.0
        and continuity["ring_minimum_height"] >= -18.0
        and continuity["ring_maximum_height"] <= 18.0
        and continuity["top_surface_hits"] == continuity["ring_samples"]
        and continuity["multi_top_hits"] == 0
        and continuity["near_coplanar_double_top_hits"] == 0
        and continuity["vertical_skirt_highest_hits"] == 0
        and continuity["player_view_visible_skirt_hits"] == 0
        and continuity["player_view_near_coplanar_double_top_hits"] == 0
        and south_ground_seam["south_ground_top_hits"]
            == south_ground_seam["south_ground_samples"]
        and south_ground_seam["south_ground_distant_side_hits"] == 0
        and all(metric["samples"] >= 100 for metric in screen_relief)
        and all(metric["height_p10_p90"] >= 0.90 for metric in screen_relief)
        and all(metric["normal_z_std"] >= 0.0005 for metric in screen_relief)
        and north_edge_endcaps_ready
        and north_edge_seam["north_edge_hits"] == north_edge_seam["north_edge_samples"]
        and north_edge_seam["north_edge_distant_side_hits"] == 0
        and mountain_burial_clearance >= 0.5
        and len(mountains) == 12
        and len({mountain.data for mountain in mountains}) == 1
        and all(valley_triangle_count(mountain.data) == MOUNTAIN_EXPECTED_TRIANGLES
                for mountain in mountains)
        and liner_roundtrip_ready
        and enterable_mesh_pairs_ready
    )
    screen_relief_report = ",".join(
        f"{metric['height_p10_p90']:.3f}/"
        f"{metric['normal_z_std']:.5f}/"
        f"{metric['samples']}"
        for metric in screen_relief
    )
    print(
        "JIANGHAI_GLB_ROUNDTRIP "
        f"valid={valid} ground=1:{triangle_total}:{len(mesh.vertices)} "
        f"topology={topology['connected_components']}:"
        f"{topology['boundary_components']}:{topology['boundary_edges']} "
        f"degenerate={topology['degenerate_faces']} "
        f"invalid_normals={topology['invalid_face_normals']} "
        f"max_edge={topology['maximum_terrain_edge']:.3f} "
        f"max_edge_area_ratio={topology['maximum_terrain_edge_area_ratio']:.3f} "
        f"uv={len(uv_layers)}:{len(mesh.loops)} error={uv_error:.8f}/"
        f"{COAST_UV_ROUNDTRIP_TOLERANCE:.8f} "
        f"diagonals=({mesh.get('top_diagonal_orientation_a', 0)},"
        f"{mesh.get('top_diagonal_orientation_b', 0)}) "
        f"bands=(300_400:{mesh.get('height_p10_p90_300_400', 0.0):.3f},"
        f"500_560:{mesh.get('height_p10_p90_500_560', 0.0):.3f},"
        f"560_601:{mesh.get('height_p10_p90_560_601', 0.0):.3f}) "
        f"slope_max={mesh.get('surface_slope_maximum', 0.0):.4f} "
        f"uv_jacobian=({mesh.get('uv_normalized_jacobian_minimum', 0.0):.4f},"
        f"{mesh.get('uv_normalized_jacobian_maximum', 0.0):.4f}) "
        f"uv_tile={mesh.get('uv_tile_size_local', 0.0):.3f} "
        f"surface_asset={materials[0].get('surface_asset_id', '') if materials else ''} "
        f"surface_hashes={materials[0].get('surface_source_md5s', '') if materials else ''} "
        f"surface_images={len(surface_images)} extent="
        f"base_color_factor="
        f"{tuple(materials[0].get('base_color_factor', ())) if materials else ()} "
        f"({extent.x:.1f},{extent.y:.1f},{extent.z:.1f}) "
        f"foundation_gap={continuity['foundation_edge_maximum_gap']:.3f} "
        f"ring={continuity['ring_hits']}/{continuity['ring_samples']} "
        f"multi_top={continuity['multi_top_hits']} "
        f"near_double_top={continuity['near_coplanar_double_top_hits']} "
        f"skirt_highest={continuity['vertical_skirt_highest_hits']} "
        f"player_skirt={continuity['player_view_visible_skirt_hits']}/"
        f"{continuity['player_view_samples']} "
        f"player_near_double_top="
        f"{continuity['player_view_near_coplanar_double_top_hits']} "
        f"screen_relief=({screen_relief_report}) "
        f"south_ground={south_ground_seam['south_ground_top_hits']}/"
        f"{south_ground_seam['south_ground_samples']}:"
        f"side={south_ground_seam['south_ground_distant_side_hits']}:"
        f"relief={south_ground_seam['south_ground_height_p10_p90']:.3f} "
        f"safe_top={mesh.get('safe_inner_top_maximum', 0.0):.3f} "
        f"camera_clearance=({','.join(f'{value:.3f}' for value in camera_clearances)}) "
        f"north_endcaps={north_edge_endcaps_ready}:{north_edge_boundary_top:.3f} "
        f"north_ray={north_edge_seam['north_edge_hits']}/"
        f"{north_edge_seam['north_edge_samples']}:"
        f"side={north_edge_seam['north_edge_distant_side_hits']} "
        f"mountain_burial={mountain_burial_clearance:.3f} "
        f"mountains={len(mountains)} "
        f"interior_liners={len(interior_liners)}:{len(liner_meshes)}:"
        f"{len(liner_mesh.loop_triangles) if liner_mesh is not None else 0}:"
        f"opaque={liner_materials_opaque} "
        f"enterable_mesh_pairs={enterable_mesh_pairs_ready}:"
        f"{len(ENTERABLE_MESH_SHARE_GROUPS)} "
        f"density_color0={density_color0_glb['profiles']}:"
        f"surfaces={density_color0_glb['profile_surfaces']}:"
        f"instances={density_color0_glb['instances']}:"
        f"instance_surfaces={density_color0_glb['instance_surfaces']}:"
        f"infill={density_color0_glb['infill_instances']}:"
        f"infill_surfaces={density_color0_glb['infill_surfaces']}"
    )
    if not valid:
        raise RuntimeError("Jianghai runtime GLB Coast composite roundtrip validation failed")
    return {
        "triangles": triangle_total,
        "vertices": len(mesh.vertices),
        "uv_error": uv_error,
        "interior_liners": len(interior_liners),
        "density_color0_surfaces": density_color0_glb["instance_surfaces"],
    }


def main() -> None:
    if Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
        raise RuntimeError(f"Open the authored scene before export: {BLEND_PATH}")
    GLB_PATH.parent.mkdir(parents=True, exist_ok=True)
    tuned_emissions = tune_runtime_emissions()
    tuned_materials = tune_runtime_materials()
    removed_floating_signs = remove_floating_market_signs()
    removed_retired_metadata = remove_retired_asset_metadata()
    removed_facade_overlays = remove_retired_facade_overlays()
    clan_hall_authored = author_clan_hall_gate_portal()
    clan_hall_portal = validate_clan_hall_gate_portal()
    removed_factory_shells, rebuilt_factory_buildings = rebuild_factory_frontage()
    removed_cross_street_intrusions = clear_cross_street_intrusions()
    rebuilt_street_cadence = rebuild_street_cadence()
    adjusted_market_furniture = clear_market_walkway()
    removed_density, rebuilt_density, density_profile_counts = rebuild_dense_perimeter()
    density_color0 = validate_density_color0_scene()
    pawnshop_doorway_cut = cut_pawnshop_doorway()
    removed_entry_facades, rebuilt_entry_facades = rebuild_hinged_entry_facades()
    # Build liners after entry facades so both systems reuse the exact packed
    # Brick_Plain_1 materials and images instead of exporting duplicates.
    enterable = apply_enterable_residences()
    pawnshop_canopy_parts, pawnshop_wings = validate_pawnshop_frontage()
    valley_ground_scans, valley_mountains, valley_triangles = validate_valley_environment()
    flattened = flatten_tiled_images()
    with tempfile.TemporaryDirectory(prefix="jianghai-runtime-textures-") as cache:
        resized, recompressed = optimize_runtime_textures(Path(cache))
        forbidden_fonts = [font.name for font in bpy.data.fonts if font.name != "Bfont"]
        if forbidden_fonts:
            raise RuntimeError(f"Source font data must not ship: {forbidden_fonts}")
        bpy.ops.file.pack_all()
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
        export_refinery_door()
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.gltf(
            filepath=str(GLB_PATH),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_yup=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
        )
    glb_size = GLB_PATH.stat().st_size
    clan_hall_glb_anchor = validate_clan_hall_gate_glb(GLB_PATH)
    if glb_size > MAX_RUNTIME_GLB_SIZE_BYTES:
        raise RuntimeError(
            f"Jianghai runtime GLB exceeds the public-repository budget: "
            f"{glb_size} > {MAX_RUNTIME_GLB_SIZE_BYTES}"
        )
    meshes, evaluated_objects, evaluated_triangles, materials = scene_statistics()
    if evaluated_triangles > MAX_RUNTIME_INSTANCE_TRIANGLES:
        raise RuntimeError(
            "Jianghai runtime scene exceeds the instance-triangle budget: "
            f"{evaluated_triangles} > {MAX_RUNTIME_INSTANCE_TRIANGLES}"
        )
    roundtrip = validate_runtime_glb_roundtrip()
    print(
        "JIANGHAI_EXPORT_COMPLETE "
        f"tuned_emissions={tuned_emissions} tuned_materials={tuned_materials} "
        f"removed_floating_signs={removed_floating_signs} "
        f"removed_retired_metadata={removed_retired_metadata} flattened_udim={flattened} "
        f"removed_facade_overlays={removed_facade_overlays} "
        f"facade_overlay_contract={enterable.retired_overlay_count} "
        f"clan_hall_removed={clan_hall_authored['removed_components']}:"
        f"{clan_hall_authored['removed_vertices']}:"
        f"{clan_hall_authored['removed_triangles']} "
        f"clan_hall_portal={clan_hall_portal['static_components']}:"
        f"aperture={clan_hall_portal['aperture_clear']}/9:"
        f"jambs={clan_hall_portal['jamb_hits']}/6:"
        f"lintel={clan_hall_portal['lintel_hits']}/3:"
        f"threshold={clan_hall_portal['threshold_hits']}/3:"
        f"anchor={clan_hall_portal['anchor_ready']}:"
        f"prefix={clan_hall_portal['anchor_prefix_count']}/1 "
        f"clan_hall_glb_anchor={clan_hall_glb_anchor['anchor_count']}:"
        f"prefix={clan_hall_glb_anchor['anchor_prefix_count']}/1:"
        f"transform={clan_hall_glb_anchor['transform_ready']} "
        f"removed_factory_shells={removed_factory_shells} "
        f"rebuilt_factory_buildings={rebuilt_factory_buildings} "
        f"removed_cross_street_intrusions={removed_cross_street_intrusions} "
        f"rebuilt_street_cadence={rebuilt_street_cadence} "
        f"enterable={enterable.residence_count} cuts={enterable.cut_count} "
        f"door_samples={enterable.aperture_sample_count}/{enterable.wall_sample_count} "
        f"scene_door_samples={enterable.scene_aperture_sample_count} "
        f"liners={enterable.liner_count} liner_triangles={enterable.liner_triangle_count} "
        f"liner_closure={enterable.liner_closure_sample_count} "
        f"liner_entry={enterable.liner_entry_sample_count} "
        f"shared_enterable_mesh_pairs={enterable.shared_mesh_pair_count} "
        f"adjusted_market_furniture={adjusted_market_furniture} "
        f"removed_density={removed_density} rebuilt_density={rebuilt_density} "
        f"density_profiles={','.join(f'{name}:{count}' for name, count in density_profile_counts.items())} "
        f"density_color0={density_color0.profile_count}:"
        f"surfaces={density_color0.profile_surface_count}:"
        f"instances={density_color0.instance_count}:"
        f"instance_surfaces={density_color0.instance_surface_count}:"
        f"infill={density_color0.infill_instance_count}:"
        f"infill_surfaces={density_color0.infill_surface_count} "
        f"pawnshop_doorway_cut={pawnshop_doorway_cut} "
        f"removed_entry_facades={removed_entry_facades} "
        f"rebuilt_entry_facades={rebuilt_entry_facades} "
        f"pawnshop_canopy_parts={pawnshop_canopy_parts} pawnshop_wings={pawnshop_wings} "
        f"valley_ground_scans={valley_ground_scans} valley_mountains={valley_mountains} "
        f"valley_triangles={valley_triangles} "
        f"resized_textures={resized} recompressed_textures={recompressed} "
        f"meshes={meshes} evaluated_objects={evaluated_objects} "
        f"evaluated_triangles={evaluated_triangles} materials={materials} glb_bytes={glb_size} "
        f"roundtrip_ground={roundtrip['triangles']}:{roundtrip['vertices']} "
        f"roundtrip_liners={roundtrip['interior_liners']} "
        f"roundtrip_density_color0_surfaces={roundtrip['density_color0_surfaces']} "
        f"roundtrip_uv_error={roundtrip['uv_error']:.8f} "
        f"blend={BLEND_PATH} glb={GLB_PATH} refinery_door_glb={REFINERY_DOOR_GLB_PATH}"
    )


if __name__ == "__main__":
    main()
