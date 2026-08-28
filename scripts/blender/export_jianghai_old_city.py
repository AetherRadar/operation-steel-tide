"""Export the hand-authored Jianghai Old City Blender scene to runtime GLB."""

from __future__ import annotations

from math import radians
from pathlib import Path
import re
import tempfile

import bmesh
import bpy


REPO_ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = REPO_ROOT / "source_art" / "world" / "jianghai_old_city" / "jianghai_old_city.blend"
GLB_PATH = REPO_ROOT / "assets" / "models" / "jianghai_old_city" / "jianghai_old_city.glb"
REFINERY_DOOR_GLB_PATH = (
    REPO_ROOT / "assets" / "models" / "jianghai_old_city" / "rollershutter_window_03.glb"
)
MAX_RUNTIME_TEXTURE_SIZE = 1024
MAX_DETAIL_TEXTURE_SIZE = 512
MAX_SMALL_FURNITURE_TEXTURE_SIZE = 256
MAX_RUNTIME_GLB_SIZE_BYTES = 99_000_000
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
DENSITY_URBAN_MESH_NAME = "JianghaiDensity_OldUrban_LOD"
DENSITY_BRICK_MESH_NAME = "JianghaiDensity_ScanStreet_LOD"
DENSITY_QUATERNIUS_LARGE_MESH_NAME = "JianghaiDensity_QuaterniusBuilding1Large_LOD"
DENSITY_QUATERNIUS_BIG_MESH_NAME = "JianghaiDensity_QuaterniusBuilding3Big_LOD"
DENSITY_QUATERNIUS_BUILDING4_MESH_NAME = "JianghaiDensity_QuaterniusBuilding4_LOD"
DENSITY_QUATERNIUS_HOUSE2_MESH_NAME = "JianghaiDensity_QuaterniusHouse2_LOD"
STREET_CADENCE_MESH_PREFIX = "JianghaiStreetCadence_"
DENSITY_SOURCE_PROFILES = {
    "urban": {
        "source_object": "NorthwestGateHouse",
        "mesh_name": DENSITY_URBAN_MESH_NAME,
        "decimate_ratio": 0.16,
        "base_scale": 1.0,
        "asset_name": "Old Urban building",
        "creator": "Abobla O.S",
        "source_url": "https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/",
    },
    "brick": {
        "source_object": "WeatheredRollerShop00",
        "mesh_name": DENSITY_BRICK_MESH_NAME,
        "decimate_ratio": 0.065,
        "base_scale": 1.0,
        "asset_name": "Scan Old Building Street",
        "creator": "Free poly",
        "source_url": "https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/",
    },
    "quaternius_large": {
        "runtime_glb": "assets/models/quaternius_buildings_pack/building1-large.glb",
        "mesh_name": DENSITY_QUATERNIUS_LARGE_MESH_NAME,
        "decimate_ratio": 0.16,
        "base_scale": 1.90,
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
        "base_scale": 1.55,
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
        "base_scale": 1.60,
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
        "base_scale": 3.0,
        "asset_name": "Buildings Pack / House2",
        "creator": "Quaternius",
        "source_url": "https://quaternius.com/packs/buildings.html",
        "weather_tint": (0.52, 0.48, 0.44),
        "weather_roughness": 0.88,
    },
}

# Hand-placed perimeter blocks close the visible horizon without entering the two
# cross streets, the central truck lane, or either high-value courtyard. These
# are DCC placements of authored CC0 buildings, not runtime procedural geometry.
DENSITY_BUILDING_LAYOUT = (
    ("SouthWall01", "brick", (-103.0, -82.2, 0.03), 0.0, 1.10),
    ("SouthWall02", "urban", (-78.0, -82.0, 0.03), 0.0, 0.96),
    ("SouthWall03", "quaternius_large", (-51.0, -82.1, 0.03), 0.0, 1.08),
    ("SouthWall04", "brick", (-25.0, -86.0, 0.03), 0.0, 1.14),
    ("SouthWall05", "urban", (25.0, -86.6, 0.03), 0.0, 1.02),
    ("SouthWall06", "brick", (51.0, -82.0, 0.03), 0.0, 1.12),
    ("SouthWall07", "quaternius_building4", (78.0, -82.2, 0.03), 0.0, 0.98),
    ("SouthWall08", "quaternius_house2", (100.0, -82.0, 0.03), 0.0, 1.10),
    ("NorthWall01", "brick", (-105.0, 194.2, 0.03), 180.0, 1.14),
    ("NorthWall02", "urban", (-78.0, 194.0, 0.03), 180.0, 0.98),
    ("NorthWall03", "quaternius_large", (-51.0, 194.1, 0.03), 180.0, 1.06),
    ("NorthWall04", "brick", (-25.0, 194.0, 0.03), 180.0, 1.12),
    ("NorthWall05", "urban", (25.0, 194.1, 0.03), 180.0, 1.00),
    ("NorthWall06", "brick", (51.0, 194.0, 0.03), 180.0, 1.16),
    ("NorthWall07", "quaternius_big", (78.0, 194.2, 0.03), 180.0, 1.04),
    ("NorthWall08", "quaternius_house2", (105.0, 194.0, 0.03), 180.0, 1.10),
    ("WestEdge01", "brick", (-154.2, -40.0, 0.03), 90.0, 1.12),
    ("WestEdge02", "quaternius_house2", (-154.0, -18.0, 0.03), 90.0, 0.98),
    ("WestEdge03", "quaternius_large", (-154.1, 4.0, 0.03), 90.0, 1.08),
    ("WestEdge07", "brick", (-154.1, 116.0, 0.03), 90.0, 1.10),
    ("WestEdge08", "urban", (-154.0, 150.0, 0.03), 90.0, 1.08),
    ("EastEdge01", "brick", (154.2, -40.0, 0.03), -90.0, 1.10),
    ("EastEdge02", "quaternius_house2", (154.0, -18.0, 0.03), -90.0, 0.96),
    ("EastEdge03", "quaternius_large", (154.1, 4.0, 0.03), -90.0, 1.06),
    ("EastEdge07", "brick", (154.1, 116.0, 0.03), -90.0, 1.12),
    ("EastEdge08", "urban", (154.0, 150.0, 0.03), -90.0, 1.04),
    ("WestInfill00", "urban", (-116.0, -72.0, 0.03), 90.0, 1.04),
    ("WestInfill01", "brick", (-116.0, -20.0, 0.03), 90.0, 1.12),
    ("WestInfill02", "quaternius_big", (-116.0, 40.0, 0.03), 90.0, 1.00),
    ("WestInfill03", "brick", (-134.0, 124.0, 0.03), 90.0, 1.10),
    ("WestInfill04", "quaternius_building4", (-117.0, 150.0, 0.03), 90.0, 1.06),
    ("EastInfill00", "urban", (116.0, -74.0, 0.03), -90.0, 1.06),
    ("EastInfill01", "brick", (116.0, -22.0, 0.03), -90.0, 1.10),
    ("EastInfill02", "quaternius_big", (116.0, 36.0, 0.03), -90.0, 1.02),
    ("EastInfill03", "brick", (116.0, 124.0, 0.03), -90.0, 1.14),
    ("EastInfill04", "quaternius_building4", (116.0, 150.0, 0.03), -90.0, 1.04),
)
STREET_CADENCE_LAYOUT = (
    ("WestClockRow01", "quaternius_large", (-12.20, -24.0, 0.03), 90.0, 1.90),
    ("WestMedicineRow01", "brick", (-18.50, 0.0, 0.03), 90.0, 1.30),
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
PAWNSHOP_DOORWAY_CUT_VERSION = 2


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

    rebuilt = 0
    for object_name, source_name, location, scale in FACTORY_BUILDING_LAYOUT:
        source = bpy.data.objects.get(source_name)
        if source is None or source.type != "MESH":
            raise RuntimeError(f"Factory replacement source is missing: {source_name}")
        replacement = bpy.data.objects.get(object_name)
        if replacement is None:
            replacement = source.copy()
            replacement.data = source.data
            replacement.name = object_name
            bpy.context.scene.collection.objects.link(replacement)
            rebuilt += 1
        replacement.parent = factory_root
        replacement.location = location
        replacement.rotation_euler = (0.0, 0.0, 0.0)
        replacement.scale = scale
        replacement["district_role"] = "cleared_cc0_factory_frontage"

    sign_backing = bpy.data.objects.get("RedStarFactoryMarqueeBacking")
    sign_text = bpy.data.objects.get("RedStarFactoryMarqueeText")
    if sign_backing is not None:
        sign_backing.location = (85.5, -3.90, 7.35)
    if sign_text is not None:
        sign_text.location = (85.5, -3.81, 7.35)
    return removed, rebuilt


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
    """Break the cloned near-street row with four distinct full authored buildings."""

    base_source = bpy.data.objects.get("NorthwestGateHouse")
    if base_source is None or base_source.type != "MESH":
        raise RuntimeError("The Old Urban cadence reset source is missing")
    targets = []
    for object_name, _, _, _, _ in STREET_CADENCE_LAYOUT:
        target = bpy.data.objects.get(object_name)
        if target is None or target.type != "MESH":
            raise RuntimeError(f"Street cadence target is missing: {object_name}")
        target.data = base_source.data
        targets.append(target)
    for mesh in list(bpy.data.meshes):
        if mesh.name.startswith(STREET_CADENCE_MESH_PREFIX) and mesh.users == 0:
            bpy.data.meshes.remove(mesh)

    meshes = {
        "brick": bpy.data.objects["WeatheredRollerShop00"].data,
        "quaternius_large": build_authored_profile_mesh(
            "quaternius_large",
            f"{STREET_CADENCE_MESH_PREFIX}Building1Large",
            1.0,
        ),
        "quaternius_building4": build_authored_profile_mesh(
            "quaternius_building4",
            f"{STREET_CADENCE_MESH_PREFIX}Building4",
            1.0,
        ),
        "quaternius_house2": build_authored_profile_mesh(
            "quaternius_house2",
            f"{STREET_CADENCE_MESH_PREFIX}House2",
            1.0,
        ),
    }
    for target, layout in zip(targets, STREET_CADENCE_LAYOUT, strict=True):
        _, profile_name, location, yaw_degrees, scale = layout
        profile = DENSITY_SOURCE_PROFILES[profile_name]
        target.data = meshes[profile_name]
        target.location = location
        target.rotation_euler = (0.0, 0.0, radians(yaw_degrees))
        target.scale = (scale, scale, scale)
        target["source_asset"] = profile["asset_name"]
        target["source_creator"] = profile["creator"]
        target["source_url"] = profile["source_url"]
        target["license"] = "CC0 1.0 Universal"
        target["authored_adaptation"] = (
            "Full authored CC0 building fitted and weathered in Blender to break street cadence"
        )
        target["district_role"] = "authored_street_cadence_building"
        target["collision_role"] = "building_shell"
        target["building_id"] = target.name
    return len(targets)


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
        created += 1
        profile_counts[profile_name] += 1
    return removed, created, profile_counts


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


def main() -> None:
    if Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
        raise RuntimeError(f"Open the authored scene before export: {BLEND_PATH}")
    GLB_PATH.parent.mkdir(parents=True, exist_ok=True)
    tuned_emissions = tune_runtime_emissions()
    tuned_materials = tune_runtime_materials()
    removed_floating_signs = remove_floating_market_signs()
    removed_retired_metadata = remove_retired_asset_metadata()
    removed_factory_shells, rebuilt_factory_buildings = rebuild_factory_frontage()
    removed_cross_street_intrusions = clear_cross_street_intrusions()
    rebuilt_street_cadence = rebuild_street_cadence()
    adjusted_market_furniture = clear_market_walkway()
    removed_density, rebuilt_density, density_profile_counts = rebuild_dense_perimeter()
    pawnshop_doorway_cut = cut_pawnshop_doorway()
    pawnshop_canopy_parts, pawnshop_wings = validate_pawnshop_frontage()
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
    if glb_size > MAX_RUNTIME_GLB_SIZE_BYTES:
        raise RuntimeError(
            f"Jianghai runtime GLB exceeds the public-repository budget: "
            f"{glb_size} > {MAX_RUNTIME_GLB_SIZE_BYTES}"
        )
    meshes, evaluated_objects, evaluated_triangles, materials = scene_statistics()
    print(
        "JIANGHAI_EXPORT_COMPLETE "
        f"tuned_emissions={tuned_emissions} tuned_materials={tuned_materials} "
        f"removed_floating_signs={removed_floating_signs} "
        f"removed_retired_metadata={removed_retired_metadata} flattened_udim={flattened} "
        f"removed_factory_shells={removed_factory_shells} "
        f"rebuilt_factory_buildings={rebuilt_factory_buildings} "
        f"removed_cross_street_intrusions={removed_cross_street_intrusions} "
        f"rebuilt_street_cadence={rebuilt_street_cadence} "
        f"adjusted_market_furniture={adjusted_market_furniture} "
        f"removed_density={removed_density} rebuilt_density={rebuilt_density} "
        f"density_profiles={','.join(f'{name}:{count}' for name, count in density_profile_counts.items())} "
        f"pawnshop_doorway_cut={pawnshop_doorway_cut} "
        f"pawnshop_canopy_parts={pawnshop_canopy_parts} pawnshop_wings={pawnshop_wings} "
        f"resized_textures={resized} recompressed_textures={recompressed} "
        f"meshes={meshes} evaluated_objects={evaluated_objects} "
        f"evaluated_triangles={evaluated_triangles} materials={materials} glb_bytes={glb_size} "
        f"blend={BLEND_PATH} glb={GLB_PATH} refinery_door_glb={REFINERY_DOOR_GLB_PATH}"
    )


if __name__ == "__main__":
    main()
