"""Author the Jianghai Old City valley terrain and mountain backdrop in Blender."""

from __future__ import annotations

from collections import Counter
from math import atan2, cos, hypot, isfinite, pi, sin, sqrt, tan
from pathlib import Path
import hashlib
import os

import bmesh
import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = (
    REPO_ROOT / "source_art" / "world" / "jianghai_old_city" / "jianghai_old_city.blend"
)
ACQUISITION_ENV = "JIANGHAI_VALLEY_ACQUISITION_ROOT"
VALLEY_ROOT_NAME = "JianghaiValleyEnvironment"
FOUNDATION_NAME = "OldCityFoundation"
GROUND_PREFIX = "JianghaiPerimeterGround"
MOUNTAIN_PREFIX = "JianghaiMountainMassif"
CC0_LICENSE = "CC0 1.0 Universal"
HERO_MOUNTAIN_LICENSE = "CC BY 4.0"
ACQUISITION_DATE = "2026-08-28"
MAX_DCC_TEXTURE_SIZE = 1024
GROUND_INSTANCE_COUNT = 1
GROUND_TARGET_TRIANGLES = 20_000
GROUND_EXPECTED_TRIANGLES = 168_480
GROUND_EXPECTED_VERTICES = 84_960
GROUND_EXPECTED_WELDED_VERTICES = 2_573
GROUND_EXPECTED_BOUNDARY_EDGES = 1_440
GROUND_EXPECTED_BOUNDARY_COMPONENTS = 2
GROUND_BOUNDARY_SKIRT_DEPTH = 1.50
GROUND_COMPOSITE_ANGLE_SAMPLES = 720
GROUND_COMPOSITE_INNER_RADIUS = 140.0
GROUND_COMPOSITE_OUTER_RADIUS = 600.0
GROUND_COMPOSITE_RADIAL_STEP = 4.0
GROUND_COMPOSITE_RADIAL_SAMPLES = 116
GROUND_SAFE_RADIUS = 220.0
GROUND_RELIEF_TRANSITION_END_RADIUS = 260.0
GROUND_SAFE_TOP_MAXIMUM = -0.12
GROUND_RELIEF_BASELINE = -0.35
GROUND_FOUNDATION_MARGIN = 8.0
GROUND_FOUNDATION_RELIEF_END_DISTANCE = 28.0
GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE = 420.0
COAST_UV_LAYER_NAME = "CoastGroundUV"
COAST_UV_TILE_SIZE_LOCAL = 7.0
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
COAST_INNER_TRANSITION_START_Y = -1.0
COAST_INNER_TRANSITION_END_Y = 6.0
COAST_OUTER_TRANSITION_START_Y = 0.0
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
MOUNTAIN_INSTANCE_COUNT = 12
MOUNTAIN_TARGET_TRIANGLES = 14_000
MOUNTAIN_EXPECTED_TRIANGLES = 14_000
ROCKY_TERRAIN_URL = "https://polyhaven.com/a/rocky_terrain"
GRAVEL_FLOOR_URL = "https://polyhaven.com/a/gravel_floor_03"
COAST_LINE_URL = "https://polyhaven.com/a/coast_line_01"
HERO_MOUNTAIN_URL = (
    "https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca"
)

SOURCE_FILES = {
    "coast_line_01/coast_line_01_2k.gltf": "bae8b0b77271b1d3c9cc50a710fbce02",
    "coast_line_01/coast_line_01.bin": "ce691184592e23391d202f37a13c9b97",
    "coast_line_01/textures/coast_line_01_diff_2k.jpg": "cc178fb4bcca93110b98037c4c55d5c4",
    "coast_line_01/textures/coast_line_01_arm_2k.jpg": "ce69b67920b01875421c8e67e28f0012",
    "coast_line_01/textures/coast_line_01_nor_gl_2k.jpg": "03a6f2d453ee92b2d42b9781f8d31e80",
    "hero_mountain/Mesh_05K_hero_mountain01.obj": "af949f14c8fb8138bf75f2a70769b2be",
    "hero_mountain/Color__hero_mountain01.jpg": "1480eb4cadc8c531055b0b39ea5ab50d",
    "hero_mountain/Normal_hero_mountain01.png": "7f16993db123397c80fcec42e586729b",
    "hero_mountain/Roughness__hero_mountain01.jpg": "e46afb87a2dbe6c2843eb14864245ffe",
    "rocky_terrain/textures/rocky_terrain_diff_2k.jpg": "4abb5d65394b6af07752099bd34ddd02",
    "rocky_terrain/textures/rocky_terrain_disp_2k.png": "8146d9555199ee5ca526d2346d97df45",
    "rocky_terrain/textures/rocky_terrain_nor_gl_2k.jpg": "05034535c6a4d24bf1886bd6331b9d39",
    "rocky_terrain/textures/rocky_terrain_rough_2k.jpg": "e773e576ac20318199c85ca84abfe2fe",
    "gravel_floor_03/textures/gravel_floor_03_diff_2k.jpg": "d86981602e03f8f1deeccc5e37a14468",
    "gravel_floor_03/textures/gravel_floor_03_disp_2k.png": "d6bc2d30510434f80f725baf72b215a7",
    "gravel_floor_03/textures/gravel_floor_03_nor_gl_2k.jpg": "864d073353dcfbbb0a507cbc07e250b7",
    "gravel_floor_03/textures/gravel_floor_03_rough_2k.jpg": "698b4d00999fa3108d4abc8584dde936",
}

ASSET_SPECS = {
    "coast_line_01": {
        "url": COAST_LINE_URL,
        "creators": "Rob Tuytel; Rico Cilliers",
        "target_triangles": GROUND_TARGET_TRIANGLES,
        "mesh_name": "JianghaiCoastLine01CompositeTerrain",
        "material_name": COAST_SURFACE_MATERIAL_NAME,
        "license": CC0_LICENSE,
    },
    "hero_mountain": {
        "url": HERO_MOUNTAIN_URL,
        "creators": "solararchitect",
        "target_triangles": MOUNTAIN_TARGET_TRIANGLES,
        "mesh_name": "JianghaiHeroMountainDistantLOD",
        "material_name": "JianghaiHeroMountainPBR",
        "license": HERO_MOUNTAIN_LICENSE,
    },
}

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

if len(GROUND_LAYOUT) != 8:
    raise RuntimeError("Jianghai Coast source layout contract is inconsistent")

MOUNTAIN_LAYOUT = tuple(
    (
        (
            cos(index * pi / 3.0) * 630.0,
            60.0 + sin(index * pi / 3.0) * 630.0,
        ),
        (285.0, 278.0, 282.0, 276.0, 284.0, 280.0)[index],
        (0.0, 90.0, 180.0, 270.0, 0.0, 90.0)[index] * pi / 180.0,
        (17.8, 19.8, 21.8, 17.8, 19.8, 21.8)[index],
        "inner",
    )
    for index in range(6)
) + tuple(
    (
        (
            cos((index * 60.0 + 30.0) * pi / 180.0) * 780.0,
            60.0 + sin((index * 60.0 + 30.0) * pi / 180.0) * 780.0,
        ),
        (310.0, 300.0, 320.0, 305.0, 315.0, 295.0)[index],
        (90.0, 180.0, 270.0, 0.0, 90.0, 180.0)[index] * pi / 180.0,
        (19.8, 21.8, 17.8, 21.8, 17.8, 19.8)[index],
        "outer",
    )
    for index in range(6)
)


def md5(path: Path) -> str:
    digest = hashlib.md5()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def acquisition_root() -> Path:
    value = os.environ.get(ACQUISITION_ENV, "").strip()
    if not value:
        raise RuntimeError(
            f"Set {ACQUISITION_ENV} to the verified valley acquisition cache."
        )
    root = Path(value).resolve()
    for relative_path, expected_md5 in SOURCE_FILES.items():
        source = root / relative_path
        if not source.is_file():
            raise RuntimeError(f"Missing valley source asset: {source}")
        actual_md5 = md5(source)
        if actual_md5 != expected_md5:
            raise RuntimeError(
                f"Source hash mismatch for {source}: {actual_md5} != {expected_md5}"
            )
    return root


def detach_foundation(root: bpy.types.Object) -> None:
    foundation = bpy.data.objects.get(FOUNDATION_NAME)
    if foundation is None or foundation not in root.children_recursive:
        return
    fallback_parent = bpy.data.objects.get("AuthoredStreetNetwork")
    if fallback_parent is None:
        fallback_parent = bpy.data.objects.get("JianghaiOldCityAuthoredScene")
    world_matrix = foundation.matrix_world.copy()
    foundation.parent = fallback_parent
    foundation.matrix_world = world_matrix


def remove_hierarchy(root: bpy.types.Object) -> None:
    detach_foundation(root)
    descendants = list(root.children_recursive)
    owned_meshes = {
        obj.data for obj in descendants if obj.type == "MESH" and obj.data is not None
    }
    owned_materials = {
        material
        for mesh in owned_meshes
        for material in mesh.materials
        if material is not None
    }
    owned_images = {
        node.image
        for material in owned_materials
        if material.use_nodes and material.node_tree is not None
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    for obj in reversed(descendants):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.objects.remove(root, do_unlink=True)
    for mesh in owned_meshes:
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for material in owned_materials:
        if material.users == 0:
            bpy.data.materials.remove(material)
    for image in owned_images:
        if image.users == 0:
            bpy.data.images.remove(image)
    purge_orphaned_valley_data()


def purge_orphaned_valley_data() -> None:
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0 and mesh.name.startswith(
            (
                "JianghaiValleyTerrainMesh",
                "JianghaiMountainsideDistantLOD",
                "JianghaiCoastLine01DistantLOD",
                "JianghaiHeroMountainDistantLOD",
                "JianghaiCoastalCliff",
                "JianghaiNamaqualandCliff",
            )
        ):
            bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        if material.users == 0 and material.name.startswith(
            (
                "JianghaiCompactedGroundPBR",
                "JianghaiRockyValleyPBR",
                "JianghaiMountainsidePBR",
                "JianghaiCoastLine01PBR",
                "JianghaiHeroMountainPBR",
                "JianghaiCoastalCliff",
                "JianghaiNamaqualandCliff",
            )
        ):
            bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image.users == 0 and image.name.startswith(
            (
                "JianghaiGravelFloor03_",
                "JianghaiRockyTerrain_",
                "mountainside_",
                "coast_line_01_",
                "coastal_cliff_01_",
                "coastal_cliff_02_",
                "namaqualand_cliff_02_",
                "drone_rock_02_",
                "JianghaiHeroMountain_",
            )
        ):
            bpy.data.images.remove(image)


def load_image(
    path: Path,
    name: str,
    color_space: str,
    source_url: str,
    source_creator: str,
    source_license: str = CC0_LICENSE,
    acquisition_date: str = ACQUISITION_DATE,
) -> bpy.types.Image:
    existing = bpy.data.images.get(name)
    if existing is not None:
        bpy.data.images.remove(existing)
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = name
    image.colorspace_settings.name = color_space
    resize_and_pack_image(image)
    image["source_license"] = source_license
    image["source_url"] = source_url
    image["source_creator"] = source_creator
    image["acquisition_date"] = acquisition_date
    return image


def resize_and_pack_image(image: bpy.types.Image) -> None:
    width, height = image.size
    longest_side = max(width, height)
    if longest_side > MAX_DCC_TEXTURE_SIZE:
        scale = MAX_DCC_TEXTURE_SIZE / longest_side
        image.scale(
            max(1, round(width * scale)),
            max(1, round(height * scale)),
        )
    image.pack()


def create_surface_material(
    root: Path, asset_name: str, material_name: str,
    image_prefix: str, source_url: str, source_creator: str,
    uv_map_name: str, normal_strength: float,
) -> bpy.types.Material:
    texture_root = root / asset_name / "textures"
    def asset_image(suffix: str, extension: str, label: str, color_space: str):
        image = load_image(
            texture_root / f"{asset_name}_{suffix}_2k.{extension}",
            f"{image_prefix}_{label}", color_space, source_url, source_creator,
        )
        relative_path = f"{asset_name}/textures/{asset_name}_{suffix}_2k.{extension}"
        image["source_md5"] = SOURCE_FILES[relative_path]
        return image

    diffuse = asset_image("diff", "jpg", "Diffuse", "sRGB")
    normal = asset_image("nor_gl", "jpg", "NormalGL", "Non-Color")
    roughness = asset_image("rough", "jpg", "Roughness", "Non-Color")

    material = bpy.data.materials.get(material_name)
    if material is None:
        material = bpy.data.materials.new(material_name)
    material.use_nodes = True
    material.use_backface_culling = True
    material["source_license"] = CC0_LICENSE
    material["source_url"] = source_url
    material["source_creator"] = source_creator
    material["acquisition_date"] = ACQUISITION_DATE
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["IOR"].default_value = 1.46
    diffuse_node = nodes.new("ShaderNodeTexImage")
    diffuse_node.image = diffuse
    diffuse_node.extension = "REPEAT"
    roughness_node = nodes.new("ShaderNodeTexImage")
    roughness_node.image = roughness
    roughness_node.extension = "REPEAT"
    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.image = normal
    normal_node.extension = "REPEAT"
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = normal_strength
    uv_map = nodes.new("ShaderNodeUVMap")
    uv_map.uv_map = uv_map_name
    links = material.node_tree.links
    for texture_node in (diffuse_node, roughness_node, normal_node):
        links.new(uv_map.outputs["UV"], texture_node.inputs["Vector"])
    links.new(diffuse_node.outputs["Color"], shader.inputs["Base Color"])
    links.new(roughness_node.outputs["Color"], shader.inputs["Roughness"])
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def create_coast_surface_material() -> bpy.types.Material:
    gravel_material = bpy.data.materials.get("JianghaiCompactedGroundPBR")
    if (
        gravel_material is None
        or not gravel_material.use_nodes
        or gravel_material.node_tree is None
    ):
        raise RuntimeError("JianghaiCompactedGroundPBR must exist before Coast setup")
    for legacy_name in (
        COAST_SURFACE_MATERIAL_NAME,
        "JianghaiCoastRockyTerrainPBR",
    ):
        existing = bpy.data.materials.get(legacy_name)
        if existing is not None:
            bpy.data.materials.remove(existing)
    material = gravel_material.copy()
    material.name = COAST_SURFACE_MATERIAL_NAME
    material.use_backface_culling = False
    material["source_license"] = CC0_LICENSE
    material["source_url"] = GRAVEL_FLOOR_URL
    material["source_creator"] = "Charlotte Baglioni"
    material["acquisition_date"] = ACQUISITION_DATE
    material["surface_asset_id"] = "gravel_floor_03"
    material["surface_source_md5s"] = GRAVEL_SURFACE_SOURCE_MD5S
    material["continuous_uv_map"] = COAST_UV_LAYER_NAME
    material["base_color_factor"] = COAST_BASE_COLOR_FACTOR
    for node in material.node_tree.nodes:
        if node.type == "UVMAP":
            node.uv_map = COAST_UV_LAYER_NAME
    shader = next(
        node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"
    )
    diffuse_node = next(
        node
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE"
        and node.image is not None
        and node.image.colorspace_settings.name == "sRGB"
    )
    for link in list(shader.inputs["Base Color"].links):
        material.node_tree.links.remove(link)
    tint = material.node_tree.nodes.new("ShaderNodeMixRGB")
    tint.name = "JianghaiCoastBaseColorFactor"
    tint.label = "Coast soil tint 0.92 0.78 0.62"
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1.0
    tint.inputs[2].default_value = COAST_BASE_COLOR_FACTOR
    material.node_tree.links.new(diffuse_node.outputs["Color"], tint.inputs[1])
    material.node_tree.links.new(tint.outputs["Color"], shader.inputs["Base Color"])
    return material


def smoothstep(value: float) -> float:
    clamped = max(0.0, min(1.0, value))
    return clamped * clamped * (3.0 - 2.0 * clamped)


def foundation_footprint(
    foundation: bpy.types.Object,
) -> tuple[
    list[list[tuple[float, float]]],
    list[tuple[tuple[float, float], tuple[float, float]]],
]:
    """Extract the real upward-facing Foundation projection and its boundary."""
    top_polygons: list[list[tuple[float, float]]] = []
    edge_counts: Counter[tuple[int, int]] = Counter()
    world_normal_matrix = foundation.matrix_world.to_3x3()
    for polygon in foundation.data.polygons:
        world_normal = (world_normal_matrix @ polygon.normal).normalized()
        if world_normal.z <= 0.5:
            continue
        top_polygons.append([
            tuple((foundation.matrix_world @ foundation.data.vertices[index].co)[:2])
            for index in polygon.vertices
        ])
        edge_counts.update(tuple(sorted(edge)) for edge in polygon.edge_keys)
    boundary_edges = [edge for edge, count in edge_counts.items() if count == 1]
    boundary_segments = [
        (
            tuple((foundation.matrix_world @ foundation.data.vertices[first].co)[:2]),
            tuple((foundation.matrix_world @ foundation.data.vertices[second].co)[:2]),
        )
        for first, second in boundary_edges
    ]
    if not top_polygons or not boundary_segments:
        raise RuntimeError("OldCityFoundation has no usable projected top footprint")
    return top_polygons, boundary_segments


def point_in_projected_polygon(
    x: float,
    y: float,
    polygon: list[tuple[float, float]],
) -> bool:
    inside = False
    previous_x, previous_y = polygon[-1]
    for current_x, current_y in polygon:
        if (current_y > y) != (previous_y > y):
            crossing_x = (
                (previous_x - current_x) * (y - current_y)
                / (previous_y - current_y)
                + current_x
            )
            if x < crossing_x:
                inside = not inside
        previous_x, previous_y = current_x, current_y
    return inside


def point_segment_distance(
    x: float,
    y: float,
    first: tuple[float, float],
    second: tuple[float, float],
) -> float:
    delta_x = second[0] - first[0]
    delta_y = second[1] - first[1]
    length_squared = delta_x * delta_x + delta_y * delta_y
    if length_squared <= 0.000000000001:
        return hypot(x - first[0], y - first[1])
    fraction = max(0.0, min(1.0, (
        (x - first[0]) * delta_x + (y - first[1]) * delta_y
    ) / length_squared))
    closest_x = first[0] + delta_x * fraction
    closest_y = first[1] + delta_y * fraction
    return hypot(x - closest_x, y - closest_y)


def signed_foundation_distance(
    x: float,
    y: float,
    top_polygons: list[list[tuple[float, float]]],
    boundary_segments: list[tuple[tuple[float, float], tuple[float, float]]],
) -> float:
    distance = min(
        point_segment_distance(x, y, first, second)
        for first, second in boundary_segments
    )
    inside = any(
        point_in_projected_polygon(x, y, polygon) for polygon in top_polygons
    )
    return -distance if inside else distance


def deform_coast_ground(mesh: bpy.types.Mesh) -> None:
    mesh.update()
    original_projected_areas = []
    for polygon in mesh.polygons:
        coordinates = [mesh.vertices[index].co for index in polygon.vertices]
        original_projected_areas.append(sum(
            coordinates[index].x * coordinates[(index + 1) % len(coordinates)].y
            - coordinates[(index + 1) % len(coordinates)].x * coordinates[index].y
            for index in range(len(coordinates))
        ))
    local_top = max(vertex.co.z for vertex in mesh.vertices)
    original_minimum_y = min(vertex.co.y for vertex in mesh.vertices)
    outer_span = COAST_OUTER_TRANSITION_START_Y - original_minimum_y
    if outer_span <= 0.0:
        raise RuntimeError("Coast outer transition has no source geometry span")
    inner_span = COAST_INNER_TRANSITION_END_Y - COAST_INNER_TRANSITION_START_Y
    for vertex in mesh.vertices:
        source_x = vertex.co.x
        source_y = vertex.co.y
        source_z = vertex.co.z
        relief = max(0.0, local_top - source_z)
        envelope_height = (
            local_top
            - COAST_ENVELOPE_TOP_OFFSET
            - relief * COAST_ENVELOPE_RELIEF_FACTOR
        )

        inner_blend = smoothstep(
            (source_y - COAST_INNER_TRANSITION_START_Y) / inner_span
        )
        outer_blend = smoothstep(
            (COAST_OUTER_TRANSITION_START_Y - source_y) / outer_span
        )
        outer_height_proxy = max(
            0.0,
            -source_y + abs(source_x) * COAST_OUTER_HEIGHT_LATERAL_FACTOR,
        )
        outer_height_blend = smoothstep(
            outer_height_proxy / COAST_OUTER_HEIGHT_BLEND_SPAN
        )
        vertex.co.z = (
            source_z
            + (envelope_height - source_z) * max(inner_blend, outer_height_blend)
        )
        vertex.co.x = source_x * (
            COAST_LATERAL_BASE_FACTOR
            + (COAST_OUTER_FLARE_FACTOR - COAST_LATERAL_BASE_FACTOR)
            * outer_blend
        )
        vertex.co.y = source_y + (
            COAST_OUTER_TARGET_Y - original_minimum_y
        ) * outer_blend

    mesh.update()
    editable = bmesh.new()
    editable.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(editable, faces=list(editable.faces))
    editable.to_mesh(mesh)
    editable.free()
    mesh.update()
    projected_flip_count = 0
    for polygon, original_area in zip(
        mesh.polygons,
        original_projected_areas,
        strict=True,
    ):
        coordinates = [mesh.vertices[index].co for index in polygon.vertices]
        deformed_area = sum(
            coordinates[index].x * coordinates[(index + 1) % len(coordinates)].y
            - coordinates[(index + 1) % len(coordinates)].x * coordinates[index].y
            for index in range(len(coordinates))
        )
        if abs(original_area) > 0.0000000001 and original_area * deformed_area <= 0.0:
            projected_flip_count += 1
    mesh["coast_envelope_deformed"] = True
    mesh["coast_inner_transition_y"] = (
        COAST_INNER_TRANSITION_START_Y,
        COAST_INNER_TRANSITION_END_Y,
    )
    mesh["coast_outer_transition_start_y"] = COAST_OUTER_TRANSITION_START_Y
    mesh["coast_outer_original_minimum_y"] = original_minimum_y
    mesh["coast_outer_target_y"] = COAST_OUTER_TARGET_Y
    mesh["coast_lateral_base_factor"] = COAST_LATERAL_BASE_FACTOR
    mesh["coast_outer_flare_factor"] = COAST_OUTER_FLARE_FACTOR
    mesh["coast_outer_height_lateral_factor"] = COAST_OUTER_HEIGHT_LATERAL_FACTOR
    mesh["coast_outer_height_blend_span"] = COAST_OUTER_HEIGHT_BLEND_SPAN
    mesh["coast_envelope_top_offset"] = COAST_ENVELOPE_TOP_OFFSET
    mesh["coast_envelope_relief_factor"] = COAST_ENVELOPE_RELIEF_FACTOR
    mesh["coast_projected_flip_count"] = projected_flip_count
    mesh["normals_recalculated_after_deformation"] = True


def create_hero_mountain_material(root: Path) -> bpy.types.Material:
    texture_root = root / "hero_mountain"
    creator = ASSET_SPECS["hero_mountain"]["creators"]
    diffuse = load_image(
        texture_root / "Color__hero_mountain01.jpg",
        "JianghaiHeroMountain_Color",
        "sRGB",
        HERO_MOUNTAIN_URL,
        creator,
        HERO_MOUNTAIN_LICENSE,
        "2026-08-29",
    )
    roughness = load_image(
        texture_root / "Roughness__hero_mountain01.jpg",
        "JianghaiHeroMountain_Roughness",
        "Non-Color",
        HERO_MOUNTAIN_URL,
        creator,
        HERO_MOUNTAIN_LICENSE,
        "2026-08-29",
    )
    normal = load_image(
        texture_root / "Normal_hero_mountain01.png",
        "JianghaiHeroMountain_Normal",
        "Non-Color",
        HERO_MOUNTAIN_URL,
        creator,
        HERO_MOUNTAIN_LICENSE,
        "2026-08-29",
    )
    material = bpy.data.materials.new("JianghaiHeroMountainPBR")
    material.use_nodes = True
    material.use_backface_culling = False
    material["source_license"] = HERO_MOUNTAIN_LICENSE
    material["source_url"] = HERO_MOUNTAIN_URL
    material["source_creator"] = creator
    material["acquisition_date"] = "2026-08-29"
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["IOR"].default_value = 1.46
    diffuse_node = nodes.new("ShaderNodeTexImage")
    diffuse_node.image = diffuse
    roughness_node = nodes.new("ShaderNodeTexImage")
    roughness_node.image = roughness
    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.image = normal
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.70
    links = material.node_tree.links
    links.new(diffuse_node.outputs["Color"], shader.inputs["Base Color"])
    links.new(roughness_node.outputs["Color"], shader.inputs["Roughness"])
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def configure_foundation(
    parent: bpy.types.Object,
    ground_material: bpy.types.Material,
    mountain_material: bpy.types.Material,
) -> bpy.types.Object:
    foundation = bpy.data.objects.get(FOUNDATION_NAME)
    if foundation is None or foundation.type != "MESH":
        raise RuntimeError(f"The authored {FOUNDATION_NAME} mesh is missing")
    unexpected_modifiers = [
        modifier.name for modifier in foundation.modifiers if modifier.type != "BEVEL"
    ]
    if unexpected_modifiers:
        raise RuntimeError(
            f"Unexpected {FOUNDATION_NAME} modifiers: {unexpected_modifiers}"
        )
    if foundation.modifiers:
        bpy.ops.object.select_all(action="DESELECT")
        foundation.select_set(True)
        bpy.context.view_layer.objects.active = foundation
        for modifier in list(foundation.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    world_matrix = foundation.matrix_world.copy()
    foundation.parent = parent
    foundation.matrix_world = world_matrix
    bpy.context.view_layer.update()
    _, maximum = world_bounds([foundation])
    world_matrix = foundation.matrix_world.copy()
    world_matrix.translation.z += -0.06 - maximum.z
    foundation.matrix_world = world_matrix

    mesh = foundation.data
    mesh.materials.clear()
    mesh.materials.append(ground_material)
    mesh.materials.append(mountain_material)
    for uv_layer in list(mesh.uv_layers):
        mesh.uv_layers.remove(uv_layer)
    ground_uv_layer = mesh.uv_layers.new(name="GroundUV")
    mountain_uv_layer = mesh.uv_layers.new(name="MountainUV")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index]
            world_vertex = foundation.matrix_world @ vertex.co
            ground_uv_layer.data[loop_index].uv = (
                world_vertex.x / 6.0,
                world_vertex.y / 6.0,
            )
            if abs(polygon.normal.x) > abs(polygon.normal.y):
                rocky_uv = (world_vertex.y / 90.0, world_vertex.z / 90.0)
            elif abs(polygon.normal.y) > 0.5:
                rocky_uv = (world_vertex.x / 90.0, world_vertex.z / 90.0)
            else:
                rocky_uv = (world_vertex.x / 90.0, world_vertex.y / 90.0)
            mountain_uv_layer.data[loop_index].uv = rocky_uv
        polygon.use_smooth = False
        polygon.material_index = 0 if polygon.normal.z > 0.5 else 1
    mesh.update()
    foundation["visual_role"] = "project_authored_valley_ground"
    foundation["collision_role"] = "visual_only"
    foundation["geometry_license"] = "MIT"
    foundation["geometry_source"] = "Operation Steel Tide authored OldCityFoundation"
    foundation["surface_asset_license"] = CC0_LICENSE
    foundation["surface_source_urls"] = f"{GRAVEL_FLOOR_URL}; {ROCKY_TERRAIN_URL}"
    foundation["surface_source_creators"] = "Charlotte Baglioni; Amal Kumar"
    foundation["acquisition_date"] = ACQUISITION_DATE
    foundation["playable_bounds_blender"] = "x[-170,170] y[-100,220]"
    return foundation


def bury_north_avenue_endcaps() -> tuple[int, float]:
    """Smoothly bury only the non-playable north endcaps into the Coast surface."""
    adjusted_objects = 0
    boundary_top = float("-inf")
    for object_name in NORTH_EDGE_OBJECT_NAMES:
        obj = bpy.data.objects.get(object_name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Missing north avenue endcap mesh: {object_name}")
        maximum_y = max((obj.matrix_world @ vertex.co).y for vertex in obj.data.vertices)
        blend_start_y = maximum_y - NORTH_EDGE_BLEND_LENGTH
        boundary_vertices = [
            vertex
            for vertex in obj.data.vertices
            if (obj.matrix_world @ vertex.co).y >= maximum_y - 0.001
        ]
        if not boundary_vertices:
            raise RuntimeError(f"No north boundary vertices found for {object_name}")
        current_top = max(
            (obj.matrix_world @ vertex.co).z for vertex in boundary_vertices
        )
        drop = max(0.0, current_top - NORTH_EDGE_TARGET_TOP)
        if drop > 0.000001:
            inverse = obj.matrix_world.inverted_safe()
            for vertex in obj.data.vertices:
                world = obj.matrix_world @ vertex.co
                fraction = smoothstep(
                    (world.y - blend_start_y) / NORTH_EDGE_BLEND_LENGTH
                )
                if fraction <= 0.0:
                    continue
                world.z -= drop * fraction
                vertex.co = inverse @ world
            obj.data.update()
            adjusted_objects += 1
        final_top = max(
            (obj.matrix_world @ vertex.co).z for vertex in boundary_vertices
        )
        boundary_top = max(boundary_top, final_top)
        obj["north_endcap_dcc_buried"] = True
        obj["north_endcap_blend_length_meters"] = NORTH_EDGE_BLEND_LENGTH
        obj["north_endcap_target_top"] = NORTH_EDGE_TARGET_TOP
        obj["north_endcap_maximum_y"] = maximum_y
    return adjusted_objects, boundary_top


def import_and_decimate_asset(
    source_root: Path,
    asset_id: str,
) -> bpy.types.Object:
    spec = ASSET_SPECS[asset_id]
    before = set(bpy.context.scene.objects)
    if asset_id == "hero_mountain":
        bpy.ops.wm.obj_import(
            filepath=str(source_root / asset_id / "Mesh_05K_hero_mountain01.obj"),
            forward_axis="NEGATIVE_Z",
            up_axis="Y",
        )
    else:
        bpy.ops.import_scene.gltf(
            filepath=str(source_root / asset_id / f"{asset_id}_2k.gltf")
        )
    imported = [obj for obj in bpy.context.scene.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one {asset_id} mesh, found {len(meshes)}")
    source = meshes[0]
    source.rotation_mode = "XYZ"
    source.location = (0.0, 0.0, 0.0)
    source.scale = (1.0, 1.0, 1.0)
    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    if asset_id == "hero_mountain":
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        local_minimum_height = min(vertex.co.z for vertex in source.data.vertices)
        for vertex in source.data.vertices:
            edge_coordinate = max(abs(vertex.co.x), abs(vertex.co.y))
            if edge_coordinate <= 0.82:
                continue
            blend = min(1.0, (edge_coordinate - 0.82) / 0.18)
            smooth_blend = blend * blend * (3.0 - 2.0 * blend)
            vertex.co.z = (
                local_minimum_height
                + (vertex.co.z - local_minimum_height) * (1.0 - smooth_blend)
            )
        source.data.update()
    else:
        source.rotation_euler = (0.0, 0.0, 0.0)
    if asset_id == "coast_line_01":
        weld_mesh = bmesh.new()
        weld_mesh.from_mesh(source.data)
        vertices_before_weld = len(weld_mesh.verts)
        bmesh.ops.remove_doubles(
            weld_mesh,
            verts=list(weld_mesh.verts),
            dist=0.000001,
        )
        bmesh.ops.recalc_face_normals(weld_mesh, faces=list(weld_mesh.faces))
        vertices_after_weld = len(weld_mesh.verts)
        weld_mesh.to_mesh(source.data)
        weld_mesh.free()
        source.data.update()
        source.data["pre_decimate_exact_weld"] = True
        source.data["pre_decimate_weld_distance"] = 0.000001
        source.data["pre_decimate_welded_vertices"] = (
            vertices_before_weld - vertices_after_weld
        )
    original_triangles = triangle_count(source.data)
    modifier = source.modifiers.new("JianghaiDistantLOD", "DECIMATE")
    modifier.ratio = min(1.0, spec["target_triangles"] / original_triangles)
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    if asset_id == "coast_line_01":
        deform_coast_ground(source.data)
        skirt_mesh = bmesh.new()
        skirt_mesh.from_mesh(source.data)
        original_vertices = set(skirt_mesh.verts)
        boundary_edges = [edge for edge in skirt_mesh.edges if edge.is_boundary]
        skirt_result = bmesh.ops.extrude_edge_only(
            skirt_mesh,
            edges=boundary_edges,
            use_select_history=False,
        )
        skirt_vertices = [
            element
            for element in skirt_result["geom"]
            if isinstance(element, bmesh.types.BMVert)
            and element not in original_vertices
        ]
        skirt_faces = [
            element
            for element in skirt_result["geom"]
            if isinstance(element, bmesh.types.BMFace)
        ]
        bmesh.ops.translate(
            skirt_mesh,
            verts=skirt_vertices,
            vec=Vector((0.0, 0.0, -GROUND_BOUNDARY_SKIRT_DEPTH)),
        )
        bmesh.ops.triangulate(
            skirt_mesh,
            faces=skirt_faces,
            quad_method="BEAUTY",
            ngon_method="BEAUTY",
        )
        skirt_mesh.to_mesh(source.data)
        skirt_mesh.free()
        source.data.update()
        source.data["boundary_skirt_depth"] = GROUND_BOUNDARY_SKIRT_DEPTH
        source.data["boundary_skirt_source_edges"] = len(boundary_edges)
        for uv_layer in list(source.data.uv_layers):
            source.data.uv_layers.remove(uv_layer)
        coast_uv_layer = source.data.uv_layers.new(name=COAST_UV_LAYER_NAME)
        for loop in source.data.loops:
            vertex = source.data.vertices[loop.vertex_index]
            coast_uv_layer.data[loop.index].uv = (
                vertex.co.x / COAST_UV_TILE_SIZE_LOCAL,
                vertex.co.y / COAST_UV_TILE_SIZE_LOCAL,
            )
        source.data["continuous_planar_uv"] = True
        source.data["continuous_uv_layer"] = COAST_UV_LAYER_NAME
        source.data["uv_tile_size_local"] = COAST_UV_TILE_SIZE_LOCAL
    reduced_triangles = triangle_count(source.data)
    source.data.name = spec["mesh_name"]
    for polygon in source.data.polygons:
        polygon.use_smooth = True
    source.data["source_license"] = spec["license"]
    source.data["source_url"] = spec["url"]
    source.data["source_creator"] = spec["creators"]
    source.data["acquisition_date"] = "2026-08-29"
    source.data["original_triangles"] = original_triangles
    source.data["distant_lod_triangles"] = reduced_triangles
    if asset_id == "hero_mountain":
        source.data["boundary_tapered_for_valley_overlap"] = True
        source.data["boundary_taper_inner_coordinate"] = 0.82
    if asset_id == "hero_mountain":
        source.data.materials.clear()
        source.data.materials.append(create_hero_mountain_material(source_root))
    elif asset_id == "coast_line_01":
        legacy_materials = {
            material for material in source.data.materials if material is not None
        }
        legacy_images = {
            node.image
            for material in legacy_materials
            if material.use_nodes and material.node_tree is not None
            for node in material.node_tree.nodes
            if node.type == "TEX_IMAGE" and node.image is not None
        }
        source.data.materials.clear()
        source.data.materials.append(create_coast_surface_material())
        for material in legacy_materials:
            if material.users == 0:
                bpy.data.materials.remove(material)
        for image in legacy_images:
            if image.users == 0:
                bpy.data.images.remove(image)
    asset_images = set()
    surface_license = spec["license"]
    surface_url = spec["url"]
    surface_creator = spec["creators"]
    surface_acquisition_date = "2026-08-29"
    if asset_id == "coast_line_01":
        surface_license = CC0_LICENSE
        surface_url = GRAVEL_FLOOR_URL
        surface_creator = "Charlotte Baglioni"
        surface_acquisition_date = ACQUISITION_DATE
    for material in source.data.materials:
        if material is None:
            continue
        material.name = spec["material_name"]
        material["source_license"] = surface_license
        material["source_url"] = surface_url
        material["source_creator"] = surface_creator
        material["acquisition_date"] = surface_acquisition_date
        material.use_backface_culling = False
        if material.use_nodes and material.node_tree is not None:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image is not None:
                    asset_images.add(node.image)
                if node.type == "BSDF_PRINCIPLED":
                    alpha = node.inputs.get("Alpha")
                    if alpha is not None:
                        for link in list(alpha.links):
                            material.node_tree.links.remove(link)
                        alpha.default_value = 1.0
    for image in asset_images:
        resize_and_pack_image(image)
        image["source_license"] = surface_license
        image["source_url"] = surface_url
        image["source_creator"] = surface_creator
        image["acquisition_date"] = surface_acquisition_date
    for obj in imported:
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)
    print(
        "JIANGHAI_VALLEY_ASSET "
        f"asset={asset_id} original_triangles={original_triangles} "
        f"lod_triangles={reduced_triangles}"
    )
    return source


def instance_from_source(
    asset_id: str,
    sources: dict[str, bpy.types.Object],
    usage_counts: dict[str, int],
) -> bpy.types.Object:
    source = sources[asset_id]
    count = usage_counts.get(asset_id, 0)
    instance = source if count == 0 else source.copy()
    if count > 0:
        instance.data = source.data
        bpy.context.scene.collection.objects.link(instance)
    usage_counts[asset_id] = count + 1
    return instance


def create_perimeter_ground(
    parent: bpy.types.Object,
    sources: dict[str, bpy.types.Object],
    usage_counts: dict[str, int],
) -> list[bpy.types.Object]:
    ground_source = sources["coast_line_01"]
    local_top = max(vertex.co.z for vertex in ground_source.data.vertices)
    ground_scans = []
    for index, (position, uniform_scale, yaw_offset) in enumerate(GROUND_LAYOUT):
        ground = instance_from_source("coast_line_01", sources, usage_counts)
        x, y = position
        radial_angle = atan2(y - 60.0, x)
        ground.name = f"{GROUND_PREFIX}{index:02d}"
        ground.parent = parent
        ground.location = (
            x,
            y,
            -0.10 - (index % 3) * 0.04 - local_top * uniform_scale,
        )
        ground.rotation_mode = "XYZ"
        ground.rotation_euler = (0.0, 0.0, radial_angle + pi * 0.5 + yaw_offset)
        ground.scale = (uniform_scale, uniform_scale, uniform_scale)
        ground["visual_role"] = "authored_perimeter_ground_scan"
        ground["collision_role"] = "visual_only"
        ground["source_license"] = CC0_LICENSE
        ground["source_url"] = COAST_LINE_URL
        ground["source_creator"] = ASSET_SPECS["coast_line_01"]["creators"]
        ground["acquisition_date"] = "2026-08-29"
        ground_scans.append(ground)
    return ground_scans


def percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        return 0.0
    index = round((len(ordered) - 1) * fraction)
    return ordered[min(len(ordered) - 1, max(0, index))]


def coast_uv_coordinates(x: float, y: float) -> tuple[float, float]:
    """Continuous affine Cartesian world UV at a seven-meter period."""
    return (
        x / COAST_UV_TILE_SIZE_LOCAL,
        y / COAST_UV_TILE_SIZE_LOCAL,
    )


def coast_uv_normalized_jacobian(x: float, y: float) -> float:
    del x, y
    return 1.0


def create_composite_ground(
    parent: bpy.types.Object,
    foundation: bpy.types.Object,
    source_scans: list[bpy.types.Object],
) -> bpy.types.Object:
    """Retopologize the eight Coast scans into one single-valued DCC terrain."""
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    source_mesh = source_scans[0].data
    source_material = source_mesh.materials[0]
    radial_samples = GROUND_COMPOSITE_RADIAL_SAMPLES
    angular_samples = GROUND_COMPOSITE_ANGLE_SAMPLES
    expected_radial_samples = round(
        (GROUND_COMPOSITE_OUTER_RADIUS - GROUND_COMPOSITE_INNER_RADIUS)
        / GROUND_COMPOSITE_RADIAL_STEP
    ) + 1
    if radial_samples != expected_radial_samples:
        raise RuntimeError("Composite Coast radial sampling contract is inconsistent")
    footprint_polygons, footprint_segments = foundation_footprint(foundation)

    planar_points: list[list[tuple[float, float]]] = []
    foundation_distances: list[list[float]] = []
    heights: list[list[float]] = []
    for radial_index in range(radial_samples):
        base_radius = (
            GROUND_COMPOSITE_INNER_RADIUS
            + radial_index * GROUND_COMPOSITE_RADIAL_STEP
        )
        point_row = []
        distance_row = []
        height_row = []
        for angular_index in range(angular_samples):
            base_angle = angular_index * (2.0 * pi / angular_samples)
            radial_jitter = (
                0.72 * sin(angular_index * 0.371 + radial_index * 1.913)
                + 0.28 * sin(angular_index * 1.117 - radial_index * 0.619)
                + 0.12 * sin(angular_index * 0.193 + radial_index * 2.417)
            )
            angular_jitter = (
                0.00105 * sin(angular_index * 0.733 + radial_index * 1.271)
                + 0.00035
                    * sin(angular_index * 1.319 - radial_index * 0.487)
            )
            radius = base_radius + radial_jitter
            angle = base_angle + angular_jitter
            world_x = cos(angle) * radius
            world_y = 60.0 + sin(angle) * radius
            distance_row.append(signed_foundation_distance(
                world_x,
                world_y,
                footprint_polygons,
                footprint_segments,
            ))
            if radius < 180.0:
                source_sample_radius = 180.0
            elif radius > 560.0:
                extension_fraction = min(1.0, max(0.0, (radius - 560.0) / 40.0))
                extension_ease = (
                    extension_fraction
                    * extension_fraction
                    * (3.0 - 2.0 * extension_fraction)
                )
                source_sample_radius = 560.0 - 40.0 * extension_ease
            else:
                source_sample_radius = radius
            sample_x = cos(angle) * source_sample_radius
            sample_y = 60.0 + sin(angle) * source_sample_radius
            origin = Vector((sample_x, sample_y, 80.0))
            top_heights = []
            for scan in source_scans:
                inverse = scan.matrix_world.inverted_safe()
                hit, location, normal, _ = scan.ray_cast(
                    inverse @ origin,
                    (inverse.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized(),
                    depsgraph=depsgraph,
                )
                if not hit:
                    continue
                world_normal = (scan.matrix_world.to_3x3() @ normal).normalized()
                if world_normal.z < 0.5:
                    continue
                top_heights.append((scan.matrix_world @ location).z)
            if not top_heights:
                raise RuntimeError(
                    "Composite Coast sampling found an uncovered top-surface point: "
                    f"radius={source_sample_radius:.3f} angle={angle:.6f}"
                )
            point_row.append((world_x, world_y))
            height_row.append(max(top_heights))
        planar_points.append(point_row)
        foundation_distances.append(distance_row)
        heights.append(height_row)

    raw_heights = [row[:] for row in heights]

    def smooth_height_field(
        source: list[list[float]],
        passes: int,
        blend: float,
    ) -> list[list[float]]:
        result = [row[:] for row in source]
        for _ in range(passes):
            smoothed = [row[:] for row in result]
            for radial_index in range(radial_samples):
                inner_index = max(0, radial_index - 1)
                outer_index = min(radial_samples - 1, radial_index + 1)
                for angular_index in range(angular_samples):
                    neighbor_average = (
                        result[inner_index][angular_index]
                        + result[outer_index][angular_index]
                        + result[radial_index][(angular_index - 1) % angular_samples]
                        + result[radial_index][(angular_index + 1) % angular_samples]
                    ) * 0.25
                    smoothed[radial_index][angular_index] = (
                        result[radial_index][angular_index] * (1.0 - blend)
                        + neighbor_average * blend
                    )
            result = smoothed
        return result

    lowpass_reference = smooth_height_field(raw_heights, 14, 0.24)
    broad_reference = smooth_height_field(lowpass_reference, 32, 0.26)
    heights = [row[:] for row in broad_reference]
    for radial_index in range(radial_samples):
        ring_broad_mean = sum(broad_reference[radial_index]) / angular_samples
        ring_safe_top = max(lowpass_reference[radial_index])
        for angular_index in range(angular_samples):
            footprint_distance = foundation_distances[radial_index][angular_index]
            transition = smoothstep(
                (footprint_distance - GROUND_FOUNDATION_MARGIN)
                / (
                    GROUND_FOUNDATION_RELIEF_END_DISTANCE
                    - GROUND_FOUNDATION_MARGIN
                )
            )
            relief_progress = smoothstep(
                (footprint_distance - 160.0)
                / (
                    GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE
                    - 160.0
                )
            )
            near_gain = (
                smoothstep(
                    (footprint_distance - GROUND_FOUNDATION_MARGIN)
                    / (
                        GROUND_FOUNDATION_RELIEF_END_DISTANCE
                        - GROUND_FOUNDATION_MARGIN
                    )
                )
                * (1.0 - smoothstep((footprint_distance - 35.0) / 35.0))
            )
            broad_gain = 4.0 + 12.0 * near_gain + 36.0 * relief_progress
            lowpass_gain = 1.2 + 3.6 * near_gain + 8.8 * relief_progress
            broad_residual = (
                broad_reference[radial_index][angular_index] - ring_broad_mean
            )
            lowpass_residual = (
                lowpass_reference[radial_index][angular_index]
                - broad_reference[radial_index][angular_index]
            )
            safe_height = (
                GROUND_SAFE_TOP_MAXIMUM
                + lowpass_reference[radial_index][angular_index]
                - ring_safe_top
            )
            relief_height = (
                GROUND_RELIEF_BASELINE
                + broad_residual * broad_gain
                + lowpass_residual * lowpass_gain
            )
            blended_height = (
                safe_height * (1.0 - transition)
                + relief_height * transition
            )
            if footprint_distance < 60.0:
                near_ceiling = GROUND_SAFE_TOP_MAXIMUM + 2.30 * smoothstep(
                    (footprint_distance - GROUND_FOUNDATION_MARGIN)
                    / (60.0 - GROUND_FOUNDATION_MARGIN)
                )
                blended_height = min(blended_height, near_ceiling)
            if blended_height < -12.50:
                excess = -12.50 - blended_height
                blended_height = -12.50 - excess / (1.0 + excess / 0.40)
            heights[radial_index][angular_index] = blended_height

    residuals = []
    for radial_index in range(1, radial_samples - 1):
        for angular_index in range(angular_samples):
            neighbor_average = (
                heights[radial_index - 1][angular_index]
                + heights[radial_index + 1][angular_index]
                + heights[radial_index][(angular_index - 1) % angular_samples]
                + heights[radial_index][(angular_index + 1) % angular_samples]
            ) * 0.25
            residuals.append(
                heights[radial_index][angular_index] - neighbor_average
            )

    vertices = [
        (planar_points[radial_index][angular_index][0],
         planar_points[radial_index][angular_index][1],
         heights[radial_index][angular_index])
        for radial_index in range(radial_samples)
        for angular_index in range(angular_samples)
    ]
    faces = []
    top_diagonal_a = 0
    top_diagonal_b = 0
    for radial_index in range(radial_samples - 1):
        for angular_index in range(angular_samples):
            next_angle = (angular_index + 1) % angular_samples
            inner = radial_index * angular_samples
            outer = (radial_index + 1) * angular_samples
            inner_current = inner + angular_index
            inner_next = inner + next_angle
            outer_current = outer + angular_index
            outer_next = outer + next_angle
            if (radial_index + angular_index) % 2 == 0:
                faces.extend((
                    (inner_current, outer_current, outer_next),
                    (inner_current, outer_next, inner_next),
                ))
                top_diagonal_a += 1
            else:
                faces.extend((
                    (inner_current, outer_current, inner_next),
                    (outer_current, outer_next, inner_next),
                ))
                top_diagonal_b += 1

    inner_skirt_start = len(vertices)
    for angular_index in range(angular_samples):
        x, y = planar_points[0][angular_index]
        vertices.append((
            x,
            y,
            heights[0][angular_index] - GROUND_BOUNDARY_SKIRT_DEPTH,
        ))
    outer_skirt_start = len(vertices)
    for angular_index in range(angular_samples):
        x, y = planar_points[-1][angular_index]
        vertices.append((
            x,
            y,
            heights[-1][angular_index] - GROUND_BOUNDARY_SKIRT_DEPTH,
        ))
    outer_top_start = (radial_samples - 1) * angular_samples
    for angular_index in range(angular_samples):
        next_angle = (angular_index + 1) % angular_samples
        inner_top_current = angular_index
        inner_top_next = next_angle
        inner_bottom_current = inner_skirt_start + angular_index
        inner_bottom_next = inner_skirt_start + next_angle
        faces.extend((
            (inner_top_current, inner_top_next, inner_bottom_next),
            (inner_top_current, inner_bottom_next, inner_bottom_current),
        ))
        outer_top_current = outer_top_start + angular_index
        outer_top_next = outer_top_start + next_angle
        outer_bottom_current = outer_skirt_start + angular_index
        outer_bottom_next = outer_skirt_start + next_angle
        faces.extend((
            (outer_top_current, outer_bottom_current, outer_bottom_next),
            (outer_top_current, outer_bottom_next, outer_top_next),
        ))

    top_triangle_count = (radial_samples - 1) * angular_samples * 2
    projected_flip_count = 0
    for face in faces[:top_triangle_count]:
        first = vertices[face[0]]
        second = vertices[face[1]]
        third = vertices[face[2]]
        signed_area = (
            (second[0] - first[0]) * (third[1] - first[1])
            - (second[1] - first[1]) * (third[0] - first[0])
        )
        if signed_area <= 0.00000001:
            projected_flip_count += 1

    radial_spacings = []
    angular_spacings = []
    surface_slopes = []
    for radial_index in range(radial_samples):
        for angular_index in range(angular_samples):
            current = planar_points[radial_index][angular_index]
            next_angle = planar_points[radial_index][
                (angular_index + 1) % angular_samples
            ]
            angular_distance = hypot(
                next_angle[0] - current[0],
                next_angle[1] - current[1],
            )
            angular_spacings.append(angular_distance)
            surface_slopes.append(
                abs(
                    heights[radial_index][(angular_index + 1) % angular_samples]
                    - heights[radial_index][angular_index]
                ) / max(angular_distance, 0.000001)
            )
            if radial_index + 1 >= radial_samples:
                continue
            next_radius = planar_points[radial_index + 1][angular_index]
            radial_distance = hypot(
                next_radius[0] - current[0],
                next_radius[1] - current[1],
            )
            radial_spacings.append(radial_distance)
            surface_slopes.append(
                abs(
                    heights[radial_index + 1][angular_index]
                    - heights[radial_index][angular_index]
                ) / max(radial_distance, 0.000001)
            )
    radial_spacing_mean = sum(radial_spacings) / len(radial_spacings)
    radial_spacing_rms_deviation = sqrt(
        sum((value - radial_spacing_mean) ** 2 for value in radial_spacings)
        / len(radial_spacings)
    )
    surface_slope_rms = sqrt(
        sum(value * value for value in surface_slopes) / len(surface_slopes)
    )
    foundation_transition_slopes = []
    foundation_boundary_slopes = []
    for radial_index in range(radial_samples):
        for angular_index in range(angular_samples):
            neighbors = [
                (radial_index, (angular_index + 1) % angular_samples),
            ]
            if radial_index + 1 < radial_samples:
                neighbors.append((radial_index + 1, angular_index))
            for neighbor_radius, neighbor_angle in neighbors:
                midpoint_distance = 0.5 * (
                    foundation_distances[radial_index][angular_index]
                    + foundation_distances[neighbor_radius][neighbor_angle]
                )
                if not 4.0 <= midpoint_distance <= 54.0:
                    continue
                current = planar_points[radial_index][angular_index]
                neighbor = planar_points[neighbor_radius][neighbor_angle]
                planar_distance = hypot(
                    neighbor[0] - current[0],
                    neighbor[1] - current[1],
                )
                slope = abs(
                    heights[neighbor_radius][neighbor_angle]
                    - heights[radial_index][angular_index]
                ) / max(planar_distance, 0.000001)
                foundation_transition_slopes.append(slope)
                if (
                    4.0 <= midpoint_distance <= 12.0
                    or 46.0 <= midpoint_distance <= 54.0
                ):
                    foundation_boundary_slopes.append(slope)
    asset_height_residuals = [
        heights[radial_index][angular_index]
        - broad_reference[radial_index][angular_index]
        for radial_index in range(radial_samples)
        for angular_index in range(angular_samples)
    ]
    safe_foundation_heights = [
        heights[radial_index][angular_index]
        for radial_index in range(radial_samples)
        for angular_index in range(angular_samples)
        if foundation_distances[radial_index][angular_index]
            <= GROUND_FOUNDATION_MARGIN
    ]
    foundation_near_band_heights = [
        heights[radial_index][angular_index]
        for radial_index in range(radial_samples)
        for angular_index in range(angular_samples)
        if 0.0 <= foundation_distances[radial_index][angular_index] < 60.0
    ]
    foundation_mid_band_heights = [
        heights[radial_index][angular_index]
        for radial_index in range(radial_samples)
        for angular_index in range(angular_samples)
        if 60.0 <= foundation_distances[radial_index][angular_index] < 160.0
    ]
    outer_band_heights = [
        heights[radial_index][angular_index]
        for radial_index in range(radial_samples)
        if GROUND_COMPOSITE_INNER_RADIUS
            + radial_index * GROUND_COMPOSITE_RADIAL_STEP >= 500.0
        for angular_index in range(angular_samples)
    ]
    uv_jacobians = [
        coast_uv_normalized_jacobian(x, y)
        for row in planar_points
        for x, y in row
    ]
    relief_band_ranges = (
        (140.0, 180.0),
        (180.0, 220.0),
        (220.0, 300.0),
        (300.0, 400.0),
        (400.0, 500.0),
        (500.0, 560.0),
        (560.0, 601.0),
    )
    band_height_relief: dict[str, float] = {}
    band_slope_p90: dict[str, float] = {}
    for lower, upper in relief_band_ranges:
        band_token = f"{int(lower)}_{int(upper)}"
        band_indices = [
            radial_index
            for radial_index in range(radial_samples)
            if lower <= (
                GROUND_COMPOSITE_INNER_RADIUS
                + radial_index * GROUND_COMPOSITE_RADIAL_STEP
            ) < upper
        ]
        band_heights = [
            heights[radial_index][angular_index]
            for radial_index in band_indices
            for angular_index in range(angular_samples)
        ]
        band_slopes = []
        for radial_index in band_indices:
            for angular_index in range(angular_samples):
                current = planar_points[radial_index][angular_index]
                next_angle_index = (angular_index + 1) % angular_samples
                next_angle = planar_points[radial_index][next_angle_index]
                angular_distance = hypot(
                    next_angle[0] - current[0],
                    next_angle[1] - current[1],
                )
                band_slopes.append(
                    abs(
                        heights[radial_index][next_angle_index]
                        - heights[radial_index][angular_index]
                    ) / max(angular_distance, 0.000001)
                )
                if radial_index + 1 < radial_samples:
                    next_radius = planar_points[radial_index + 1][angular_index]
                    radial_distance = hypot(
                        next_radius[0] - current[0],
                        next_radius[1] - current[1],
                    )
                    band_slopes.append(
                        abs(
                            heights[radial_index + 1][angular_index]
                            - heights[radial_index][angular_index]
                        ) / max(radial_distance, 0.000001)
                    )
        band_height_relief[band_token] = (
            percentile(band_heights, 0.90) - percentile(band_heights, 0.10)
        )
        band_slope_p90[band_token] = percentile(band_slopes, 0.90)

    composite_mesh = bpy.data.meshes.new("JianghaiCoastLine01CompositeTerrain")
    composite_mesh.from_pydata(vertices, [], faces)
    composite_mesh.update()
    for polygon in composite_mesh.polygons:
        polygon.use_smooth = True
    top_normal_z = [
        polygon.normal.z
        for polygon in composite_mesh.polygons[:top_triangle_count]
    ]
    top_normal_z_mean = sum(top_normal_z) / len(top_normal_z)
    band_normal_z_deviation: dict[str, float] = {}
    for lower, upper in relief_band_ranges:
        band_token = f"{int(lower)}_{int(upper)}"
        band_normals = []
        for radial_index in range(radial_samples - 1):
            midpoint_radius = (
                GROUND_COMPOSITE_INNER_RADIUS
                + (radial_index + 0.5) * GROUND_COMPOSITE_RADIAL_STEP
            )
            if lower <= midpoint_radius < upper:
                start = radial_index * angular_samples * 2
                band_normals.extend(
                    top_normal_z[start:start + angular_samples * 2]
                )
        band_mean = sum(band_normals) / len(band_normals)
        band_normal_z_deviation[band_token] = sqrt(
            sum((value - band_mean) ** 2 for value in band_normals)
            / len(band_normals)
        )
    composite_mesh.materials.append(source_material)
    uv_layer = composite_mesh.uv_layers.new(name=COAST_UV_LAYER_NAME)
    for loop in composite_mesh.loops:
        vertex = composite_mesh.vertices[loop.vertex_index]
        uv_layer.data[loop.index].uv = coast_uv_coordinates(vertex.co.x, vertex.co.y)

    composite_mesh["source_license"] = CC0_LICENSE
    composite_mesh["source_url"] = COAST_LINE_URL
    composite_mesh["source_creator"] = ASSET_SPECS["coast_line_01"]["creators"]
    composite_mesh["acquisition_date"] = "2026-08-29"
    composite_mesh["dcc_composite_terrain"] = True
    composite_mesh["dcc_composite_method"] = (
        "highest Coast Line 01 top-surface sampling, irregular polar retopology, "
        "real Foundation top-footprint signed-distance masking, and asset-derived "
        "low-pass/broad residual height reprojection"
    )
    composite_mesh["dcc_source_scan_count"] = len(source_scans)
    composite_mesh["dcc_source_welded_vertices"] = (
        source_mesh.get("pre_decimate_welded_vertices")
    )
    composite_mesh["single_valued_top_surface"] = True
    composite_mesh["top_radial_samples"] = radial_samples
    composite_mesh["top_angular_samples"] = angular_samples
    composite_mesh["top_surface_vertex_count"] = radial_samples * angular_samples
    composite_mesh["top_surface_face_count"] = top_triangle_count
    composite_mesh["top_surface_quad_count"] = (
        (radial_samples - 1) * angular_samples
    )
    composite_mesh["top_diagonal_orientation_a"] = top_diagonal_a
    composite_mesh["top_diagonal_orientation_b"] = top_diagonal_b
    composite_mesh["boundary_skirt_depth"] = GROUND_BOUNDARY_SKIRT_DEPTH
    composite_mesh["boundary_skirt_source_edges"] = 2 * angular_samples
    composite_mesh["boundary_skirts_buried"] = True
    composite_mesh["continuous_planar_uv"] = True
    composite_mesh["continuous_world_uv_warp"] = False
    composite_mesh["continuous_uv_layer"] = COAST_UV_LAYER_NAME
    composite_mesh["uv_tile_size_local"] = COAST_UV_TILE_SIZE_LOCAL
    composite_mesh["uv_mapping_method"] = COAST_UV_MAPPING_METHOD
    composite_mesh["uv_normalized_jacobian_minimum"] = min(uv_jacobians)
    composite_mesh["uv_normalized_jacobian_maximum"] = max(uv_jacobians)
    composite_mesh["coast_projected_flip_count"] = projected_flip_count
    composite_mesh["radial_spacing_mean"] = radial_spacing_mean
    composite_mesh["radial_spacing_rms_deviation"] = radial_spacing_rms_deviation
    composite_mesh["height_residual_rms"] = sqrt(
        sum(value * value for value in residuals) / len(residuals)
    )
    composite_mesh["height_residual_maximum"] = max(
        abs(value) for value in residuals
    )
    composite_mesh["asset_height_residual_rms"] = sqrt(
        sum(value * value for value in asset_height_residuals)
        / len(asset_height_residuals)
    )
    composite_mesh["inner_band_height_p10_p90"] = (
        percentile(safe_foundation_heights, 0.90)
        - percentile(safe_foundation_heights, 0.10)
    )
    composite_mesh["outer_band_height_p10_p90"] = (
        percentile(outer_band_heights, 0.90)
        - percentile(outer_band_heights, 0.10)
    )
    composite_mesh["surface_slope_rms"] = surface_slope_rms
    composite_mesh["surface_slope_p90"] = percentile(surface_slopes, 0.90)
    composite_mesh["surface_slope_p99"] = percentile(surface_slopes, 0.99)
    composite_mesh["surface_slope_maximum"] = max(surface_slopes)
    composite_mesh["foundation_footprint_top_face_count"] = len(footprint_polygons)
    composite_mesh["foundation_footprint_boundary_edge_count"] = len(footprint_segments)
    composite_mesh["foundation_signed_distance_mask"] = True
    composite_mesh["foundation_safe_margin_meters"] = GROUND_FOUNDATION_MARGIN
    composite_mesh["foundation_relief_end_distance_meters"] = (
        GROUND_FOUNDATION_RELIEF_END_DISTANCE
    )
    composite_mesh["foundation_relief_full_gain_distance_meters"] = (
        GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE
    )
    composite_mesh["safe_inner_top_maximum"] = max(safe_foundation_heights)
    composite_mesh["foundation_near_0_60_height_p10_p90"] = (
        percentile(foundation_near_band_heights, 0.90)
        - percentile(foundation_near_band_heights, 0.10)
    )
    composite_mesh["foundation_mid_60_160_height_p10_p90"] = (
        percentile(foundation_mid_band_heights, 0.90)
        - percentile(foundation_mid_band_heights, 0.10)
    )
    composite_mesh["relief_baseline"] = GROUND_RELIEF_BASELINE
    composite_mesh["asset_lowpass_passes"] = 14
    composite_mesh["asset_broad_passes"] = 32
    composite_mesh["broad_relief_gain_minimum"] = 4.0
    composite_mesh["broad_relief_gain_maximum"] = 40.0
    composite_mesh["lowpass_relief_gain_minimum"] = 1.2
    composite_mesh["lowpass_relief_gain_maximum"] = 10.0
    composite_mesh["relief_gain_easing"] = (
        "C1 foundation-distance near boost and post-160m smoothstep"
    )
    composite_mesh["safe_transition_slope_p95"] = percentile(
        foundation_transition_slopes, 0.95
    )
    composite_mesh["transition_boundary_slope_p95"] = percentile(
        foundation_boundary_slopes, 0.95
    )
    composite_mesh["outer_top_maximum"] = max(
        heights[radial_index][angular_index]
        for radial_index in range(radial_samples)
        if GROUND_COMPOSITE_INNER_RADIUS
            + radial_index * GROUND_COMPOSITE_RADIAL_STEP >= 500.0
        for angular_index in range(angular_samples)
    )
    composite_mesh["normal_readability_gate"] = (
        "asset-derived relief bands paired with slope p99 < 0.30, "
        "slope maximum < 0.80, and finite normal variation"
    )
    composite_mesh["surface_normal_z_standard_deviation"] = sqrt(
        sum(
            (value - top_normal_z_mean) ** 2
            for value in top_normal_z
        ) / len(top_normal_z)
    )
    composite_mesh["surface_normal_z_p10"] = percentile(top_normal_z, 0.10)
    composite_mesh["height_source_method"] = (
        "Foundation-footprint signed-distance blend of low-pass and broad residual "
        "extracted exclusively from eight transformed Coast Line 01 scans; no "
        "synthetic height noise"
    )
    for band_token, value in band_height_relief.items():
        composite_mesh[f"height_p10_p90_{band_token}"] = value
        composite_mesh[f"slope_p90_{band_token}"] = band_slope_p90[band_token]
        composite_mesh[f"normal_z_std_{band_token}"] = (
            band_normal_z_deviation[band_token]
        )
    composite_mesh["top_height_minimum"] = min(min(row) for row in heights)
    composite_mesh["top_height_maximum"] = max(max(row) for row in heights)

    for scan in source_scans:
        bpy.data.objects.remove(scan, do_unlink=True)
    if source_mesh.users == 0:
        bpy.data.meshes.remove(source_mesh)
    composite_mesh.name = ASSET_SPECS["coast_line_01"]["mesh_name"]

    composite = bpy.data.objects.new(f"{GROUND_PREFIX}Composite", composite_mesh)
    parent.users_collection[0].objects.link(composite)
    composite.parent = parent
    composite.location = (0.0, 0.0, 0.0)
    composite.rotation_euler = (0.0, 0.0, 0.0)
    composite.scale = (1.0, 1.0, 1.0)
    composite["visual_role"] = "authored_perimeter_ground_dcc_composite"
    composite["collision_role"] = "visual_only"
    composite["source_license"] = CC0_LICENSE
    composite["source_url"] = COAST_LINE_URL
    composite["source_creator"] = ASSET_SPECS["coast_line_01"]["creators"]
    composite["acquisition_date"] = "2026-08-29"
    composite["source_asset_id"] = "coast_line_01"
    return composite


def create_mountains(
    parent: bpy.types.Object,
    sources: dict[str, bpy.types.Object],
    usage_counts: dict[str, int],
) -> list[bpy.types.Object]:
    mountains = []
    source = sources["hero_mountain"]
    local_bottom = min(vertex.co.z for vertex in source.data.vertices)
    for index, (position, uniform_scale, yaw, embedded_depth, ring) in enumerate(
        MOUNTAIN_LAYOUT
    ):
        mountain = instance_from_source("hero_mountain", sources, usage_counts)
        x, y = position
        mountain.name = f"{MOUNTAIN_PREFIX}{index:02d}"
        mountain.parent = parent
        mountain.location = (
            x,
            y,
            -0.20 - embedded_depth - local_bottom * uniform_scale,
        )
        mountain.rotation_mode = "XYZ"
        mountain.rotation_euler = (0.0, 0.0, yaw)
        mountain.scale = (uniform_scale, uniform_scale, uniform_scale)
        mountain["visual_role"] = "authored_distant_mountain"
        mountain["collision_role"] = "visual_only"
        mountain["source_license"] = HERO_MOUNTAIN_LICENSE
        mountain["source_url"] = HERO_MOUNTAIN_URL
        mountain["source_creator"] = ASSET_SPECS["hero_mountain"]["creators"]
        mountain["acquisition_date"] = "2026-08-29"
        mountain["source_asset_id"] = "hero_mountain"
        mountain["mountain_ring"] = ring
        mountain["mountain_ring_radius_meters"] = 630.0 if ring == "inner" else 780.0
        mountain["embedded_depth_meters"] = embedded_depth
        mountain["explicit_yaw_radians"] = yaw
        mountains.append(mountain)
    return mountains


def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def location_angular_gap(objects: list[bpy.types.Object]) -> float:
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


def mesh_topology_statistics(mesh: bpy.types.Mesh) -> dict[str, float | int]:
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


def coast_uv_statistics(mesh: bpy.types.Mesh) -> dict[str, float | int | bool]:
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


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    minimum = Vector((float("inf"), float("inf"), float("inf")))
    maximum = Vector((float("-inf"), float("-inf"), float("-inf")))
    for obj in objects:
        for corner in obj.bound_box:
            world_corner = obj.matrix_world @ Vector(corner)
            minimum = Vector(map(min, minimum, world_corner))
            maximum = Vector(map(max, maximum, world_corner))
    return minimum, maximum


def perimeter_ground_coverage(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
) -> float:
    bpy.context.view_layer.update()
    allowed_objects = [foundation, *ground_scans]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    hits = 0
    samples = 0
    for angle_index in range(72):
        angle = angle_index * (2.0 * pi / 72.0)
        direction_x = cos(angle)
        direction_y = sin(angle)
        x_edge = 170.0 / max(abs(direction_x), 0.0001)
        y_edge = 160.0 / max(abs(direction_y), 0.0001)
        edge_distance = min(x_edge, y_edge)
        for fraction in (0.10, 0.32, 0.54, 0.76, 0.92):
            radius = edge_distance + (360.0 - edge_distance) * fraction
            origin = Vector((
                direction_x * radius,
                60.0 + direction_y * radius,
                60.0,
            ))
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


def highest_ground_height(
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


def ground_vertical_surface_hits(
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
        local_origin = inverse @ origin
        local_direction = inverse.to_3x3() @ Vector((0.0, 0.0, -1.0))
        local_direction.normalize()
        hit, location, normal, _ = obj.ray_cast(
            local_origin,
            local_direction,
            depsgraph=depsgraph,
        )
        if not hit:
            continue
        world_height = (obj.matrix_world @ location).z
        world_normal = (obj.matrix_world.to_3x3() @ normal).normalized()
        if world_normal.z >= 0.5:
            top_heights.append(world_height)
        else:
            skirt_heights.append(world_height)
    return top_heights, skirt_heights


def godot_to_blender(vector: Vector) -> Vector:
    return Vector((vector.x, -vector.z, vector.y))


def north_edge_seam_statistics(
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, float | int]:
    """Lock the exact north-camera band that exposed avenue endcap side faces."""
    forward = (NORTH_EDGE_CAMERA_TARGET - NORTH_EDGE_CAMERA_ORIGIN).normalized()
    right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
    camera_up = right.cross(forward).normalized()
    vertical_scale = tan(NORTH_EDGE_CAMERA_FOV * pi / 360.0)
    horizontal_scale = vertical_scale * (1280.0 / 720.0)
    world_origin = godot_to_blender(NORTH_EDGE_CAMERA_ORIGIN)
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
            direction = godot_to_blender(godot_direction)
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


def south_ground_seam_statistics(
    ground: bpy.types.Object,
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, float | int]:
    """Lock the south-camera pixels that exposed a low-angle Coast silhouette."""
    forward = (SOUTH_GROUND_CAMERA_TARGET - SOUTH_GROUND_CAMERA_ORIGIN).normalized()
    right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
    camera_up = right.cross(forward).normalized()
    vertical_scale = tan(SOUTH_GROUND_CAMERA_FOV * pi / 360.0)
    horizontal_scale = vertical_scale * (1280.0 / 720.0)
    world_origin = godot_to_blender(SOUTH_GROUND_CAMERA_ORIGIN)
    samples = 0
    ground_top_hits = 0
    distant_side_hits = 0
    heights = []
    for pixel_y in range(387, 392):
        screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
        for pixel_x in range(1057, 1075):
            screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
            godot_direction = (
                forward
                + right * screen_x * horizontal_scale
                + camera_up * screen_y * vertical_scale
            ).normalized()
            direction = godot_to_blender(godot_direction)
            hit, location, normal, _, obj, _ = bpy.context.scene.ray_cast(
                depsgraph,
                world_origin,
                direction,
                distance=2_500.0,
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
                print(
                    "JIANGHAI_SOUTH_GROUND_SIDE "
                    f"pixel=({pixel_x},{pixel_y}) object={obj.name} "
                    f"distance={distance:.3f} normal_z={normal.z:.5f}"
                )
    return {
        "south_ground_samples": samples,
        "south_ground_top_hits": ground_top_hits,
        "south_ground_distant_side_hits": distant_side_hits,
        "south_ground_height_p10_p90": (
            percentile(heights, 0.90) - percentile(heights, 0.10)
        ),
    }


def ground_screen_relief_statistics(
    ground: bpy.types.Object,
    depsgraph: bpy.types.Depsgraph,
) -> list[dict[str, float | int]]:
    """Measure asset relief that is actually sampled by the three fixed cameras."""
    camera_specs = (
        (
            Vector((205.0, 3.2, 145.0)), Vector((157.0, -1.2, 92.0)), 58.0,
            range(330, 481, 10),
        ),
        (
            SOUTH_GROUND_CAMERA_ORIGIN, SOUTH_GROUND_CAMERA_TARGET, 68.0,
            range(350, 421, 5),
        ),
        (
            NORTH_EDGE_CAMERA_ORIGIN, NORTH_EDGE_CAMERA_TARGET, 68.0,
            range(350, 421, 5),
        ),
    )
    results = []
    for godot_origin, godot_target, fov, pixel_ys in camera_specs:
        forward = (godot_target - godot_origin).normalized()
        right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
        camera_up = right.cross(forward).normalized()
        vertical_scale = tan(fov * pi / 360.0)
        horizontal_scale = vertical_scale * (1280.0 / 720.0)
        world_origin = godot_to_blender(godot_origin)
        inverse = ground.matrix_world.inverted_safe()
        heights = []
        normal_zs = []
        for pixel_y in pixel_ys:
            screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
            for pixel_x in range(20, 1261, 20):
                screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
                godot_direction = (
                    forward
                    + right * screen_x * horizontal_scale
                    + camera_up * screen_y * vertical_scale
                ).normalized()
                direction = godot_to_blender(godot_direction)
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
                percentile(heights, 0.90) - percentile(heights, 0.10)
            ),
            "normal_z_std": sqrt(
                sum((value - normal_mean) ** 2 for value in normal_zs)
                / len(normal_zs)
            ) if normal_zs else 0.0,
        })
    return results


def ground_player_view_statistics(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, int]:
    camera_specs = (
        (
            Vector((205.0, 3.2, 145.0)),
            Vector((157.0, -1.2, 92.0)),
            58.0,
            range(0, 1280, 10),
            range(360, 461, 5),
        ),
        (
            Vector((112.0, 1.65, 86.0)),
            Vector((18.0, 11.0, 300.0)),
            68.0,
            range(0, 1280, 20),
            range(340, 541, 10),
        ),
        (
            Vector((-118.0, 1.65, -207.0)),
            Vector((-25.0, 12.0, -340.0)),
            68.0,
            range(0, 1280, 20),
            range(340, 541, 10),
        ),
    )
    samples = 0
    top_hits = 0
    visible_skirt_hits = 0
    foundation_occluded_skirt_hits = 0
    near_coplanar_double_top_hits = 0
    for camera_index, (
        godot_origin,
        godot_target,
        fov,
        pixel_xs,
        pixel_ys,
    ) in enumerate(camera_specs):
        forward = (godot_target - godot_origin).normalized()
        right = forward.cross(Vector((0.0, 1.0, 0.0))).normalized()
        camera_up = right.cross(forward).normalized()
        vertical_scale = tan(fov * pi / 360.0)
        horizontal_scale = vertical_scale * (1280.0 / 720.0)
        world_origin = godot_to_blender(godot_origin)
        for pixel_y in pixel_ys:
            screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
            for pixel_x in pixel_xs:
                screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
                godot_direction = (
                    forward
                    + right * screen_x * horizontal_scale
                    + camera_up * screen_y * vertical_scale
                ).normalized()
                world_direction = godot_to_blender(godot_direction)
                closest = None
                closest_ground = None
                ground_top_depths = []
                foundation_inverse = foundation.matrix_world.inverted_safe()
                foundation_hit, foundation_location, foundation_normal, _ = (
                    foundation.ray_cast(
                        foundation_inverse @ world_origin,
                        (foundation_inverse.to_3x3() @ world_direction).normalized(),
                        depsgraph=depsgraph,
                    )
                )
                if foundation_hit:
                    foundation_world_location = (
                        foundation.matrix_world @ foundation_location
                    )
                    closest = (
                        (foundation_world_location - world_origin).length,
                        (foundation.matrix_world.to_3x3() @ foundation_normal)
                            .normalized().z,
                        foundation.name,
                        foundation_world_location.copy(),
                        False,
                    )
                for obj in ground_scans:
                    inverse = obj.matrix_world.inverted_safe()
                    local_origin = inverse @ world_origin
                    local_direction = inverse.to_3x3() @ world_direction
                    local_direction.normalize()
                    hit, location, normal, _ = obj.ray_cast(
                        local_origin,
                        local_direction,
                        depsgraph=depsgraph,
                    )
                    if not hit:
                        continue
                    world_location = obj.matrix_world @ location
                    distance = (world_location - world_origin).length
                    world_normal = (obj.matrix_world.to_3x3() @ normal).normalized()
                    if closest_ground is None or distance < closest_ground[0]:
                        closest_ground = (distance, world_normal.z)
                    if world_normal.z >= 0.5:
                        ground_top_depths.append(distance)
                    if closest is None or distance < closest[0]:
                        closest = (
                            distance,
                            world_normal.z,
                            obj.name,
                            world_location.copy(),
                            True,
                        )
                samples += 1
                ground_top_depths.sort()
                if (
                    len(ground_top_depths) >= 2
                    and ground_top_depths[1] - ground_top_depths[0] < 0.10
                ):
                    near_coplanar_double_top_hits += 1
                if (
                    closest is not None
                    and not closest[4]
                    and closest_ground is not None
                    and closest_ground[1] < 0.5
                    and closest[0] < closest_ground[0]
                ):
                    foundation_occluded_skirt_hits += 1
                if closest is None or not closest[4]:
                    continue
                if closest[1] >= 0.5:
                    top_hits += 1
                else:
                    visible_skirt_hits += 1
                    print(
                        "JIANGHAI_VISIBLE_SKIRT "
                        f"camera={camera_index} pixel=({pixel_x},{pixel_y}) "
                        f"object={closest[2]} world="
                        f"({closest[3].x:.3f},{closest[3].y:.3f},"
                        f"{closest[3].z:.3f}) normal_z={closest[1]:.5f}"
                    )
    return {
        "player_view_samples": samples,
        "player_view_top_hits": top_hits,
        "player_view_visible_skirt_hits": visible_skirt_hits,
        "player_view_foundation_occluded_skirt_hits":
            foundation_occluded_skirt_hits,
        "player_view_near_coplanar_double_top_hits":
            near_coplanar_double_top_hits,
    }


def ground_continuity_statistics(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
) -> dict[str, float | int]:
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    foundation_minimum, foundation_maximum = world_bounds([foundation])
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
        highest_ground_height(x, y, ground_scans, depsgraph)
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
            top_heights, skirt_heights = ground_vertical_surface_hits(
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
                top_heights,
                default=float("-inf"),
            ) - 0.0001:
                vertical_skirt_highest_hits += 1
    ring_hits = [height for height in ring_heights if height is not None]
    player_view = ground_player_view_statistics(foundation, ground_scans, depsgraph)
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


def materials_are_opaque(objects: list[bpy.types.Object]) -> bool:
    materials = {
        material
        for obj in objects
        for material in obj.data.materials
        if material is not None
    }
    for material in materials:
        if not material.use_nodes or material.node_tree is None:
            return False
        shaders = [node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"]
        if len(shaders) != 1:
            return False
        alpha = shaders[0].inputs.get("Alpha")
        if alpha is None or alpha.is_linked or abs(alpha.default_value - 1.0) > 0.0001:
            return False
    return True


def validate_environment(
    foundation: bpy.types.Object,
    ground_scans: list[bpy.types.Object],
    mountains: list[bpy.types.Object],
) -> tuple[int, int, Vector]:
    bpy.context.view_layer.update()
    foundation_triangles = triangle_count(foundation.data)
    ground_triangle_counts = [triangle_count(ground.data) for ground in ground_scans]
    mountain_triangle_counts = [triangle_count(mountain.data) for mountain in mountains]
    instance_triangles = (
        foundation_triangles
        + sum(ground_triangle_counts)
        + sum(mountain_triangle_counts)
    )
    valley_objects = [foundation, *ground_scans, *mountains]
    minimum, maximum = world_bounds(valley_objects)
    extent = maximum - minimum
    foundation_minimum, foundation_maximum = world_bounds([foundation])
    foundation_extent = foundation_maximum - foundation_minimum
    ground_minimum, ground_maximum = world_bounds(ground_scans)
    ground_extent = ground_maximum - ground_minimum
    mountain_bounds = [world_bounds([mountain]) for mountain in mountains]
    mountain_bottoms = [bounds_min.z for bounds_min, _ in mountain_bounds]
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
    angular_gaps = [
        (mountain_angles[(index + 1) % len(mountain_angles)] - angle) % (2.0 * pi)
        for index, angle in enumerate(mountain_angles)
    ]
    max_angular_gap = max(angular_gaps)
    unique_ground_meshes = {ground.data for ground in ground_scans}
    unique_mountain_meshes = {mountain.data for mountain in mountains}
    ground_mesh = next(iter(unique_ground_meshes), None)
    ground_topology = (
        mesh_topology_statistics(ground_mesh)
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
        vertex.co.z
        for vertex in top_surface_vertices
        if hypot(vertex.co.x, vertex.co.y - 60.0) >= 350.0
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
        coast_uv_statistics(ground_mesh)
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
        or material.get("source_url") == COAST_LINE_URL
    ]
    legacy_coast_images = [
        image.name
        for image in bpy.data.images
        if image.name.startswith("coast_line_01_")
        or image.get("source_url") == COAST_LINE_URL
    ]
    ground_surface_ready = (
        coast_surface_material is not None
        and coast_surface_material.name == COAST_SURFACE_MATERIAL_NAME
        and coast_surface_material.get("source_license") == CC0_LICENSE
        and coast_surface_material.get("source_url") == GRAVEL_FLOOR_URL
        and coast_surface_material.get("source_creator") == "Charlotte Baglioni"
        and coast_surface_material.get("acquisition_date") == ACQUISITION_DATE
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
    ground_layout_ready = (
        len(ground_scans) == 1
        and ground_scans[0].name == f"{GROUND_PREFIX}Composite"
        and ground_scans[0].location.length <= LAYOUT_POSITION_TOLERANCE
        and all(abs(value - 1.0) <= 0.000001 for value in ground_scans[0].scale)
        and all(abs(value) <= 0.000001 for value in ground_scans[0].rotation_euler)
    )
    mountain_mesh = next(iter(unique_mountain_meshes))
    mountain_local_minimum = min(vertex.co.z for vertex in mountain_mesh.vertices)
    mountain_boundary_height_delta = max(
        vertex.co.z - mountain_local_minimum
        for vertex in mountain_mesh.vertices
        if max(abs(vertex.co.x), abs(vertex.co.y)) >= 0.99
    )
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
    inner_ring_angular_gap = location_angular_gap(mountains[:6])
    outer_ring_angular_gap = location_angular_gap(mountains[6:])
    mountain_ring_angles_ready = (
        abs(inner_ring_angular_gap - pi / 3.0) <= 0.000001
        and abs(outer_ring_angular_gap - pi / 3.0) <= 0.000001
    )
    uniform_positive_scales = all(
        min(obj.scale) > 0.0
        and max(obj.scale) / min(obj.scale) <= 1.001
        and obj.matrix_world.determinant() > 0.0
        for obj in [*ground_scans, *mountains]
    )
    ground_coverage = perimeter_ground_coverage(foundation, ground_scans)
    ground_continuity = ground_continuity_statistics(foundation, ground_scans)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    north_edge_seam = north_edge_seam_statistics(depsgraph)
    south_ground_seam = south_ground_seam_statistics(ground_scans[0], depsgraph)
    screen_relief = ground_screen_relief_statistics(ground_scans[0], depsgraph)
    camera_clearances = []
    for godot_camera in (
        Vector((205.0, 3.2, 145.0)),
        Vector((112.0, 1.65, 86.0)),
        Vector((-118.0, 1.65, -207.0)),
    ):
        blender_camera = godot_to_blender(godot_camera)
        surface_height = highest_ground_height(
            blender_camera.x,
            blender_camera.y,
            ground_scans,
            depsgraph,
        )
        if surface_height is None:
            camera_clearances.append(float("-inf"))
        else:
            camera_clearances.append(blender_camera.z - surface_height)
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
        boundary_thickness = (
            boundary_top - min(boundary_heights, default=float("-inf"))
        )
        north_edge_boundary_top = max(north_edge_boundary_top, boundary_top)
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
    ground_instance_triangles = sum(ground_triangle_counts)
    opaque_materials = materials_are_opaque([*ground_scans, *mountains])
    visual_only = all(obj.get("collision_role") == "visual_only" for obj in valley_objects)
    material_names = {material.name for material in foundation.data.materials if material is not None}
    legacy_valley_data = any(
        token in datablock.name.lower()
        for collection in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.images)
        for datablock in collection
        for token in ("mountainside", "mountaincliff", "coastal_cliff", "namaqualand")
    )
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
    valid = (
        foundation_triangles == 188
        and len(foundation.data.vertices) == 96
        and len(ground_scans) == GROUND_INSTANCE_COUNT
        and len(unique_ground_meshes) == 1
        and all(count == GROUND_EXPECTED_TRIANGLES for count in ground_triangle_counts)
        and ground_instance_triangles
            == GROUND_INSTANCE_COUNT * GROUND_EXPECTED_TRIANGLES
        and ground_repair_ready
        and ground_envelope_ready
        and ground_uv_ready
        and ground_world_uv_scale_ready
        and ground_surface_ready
        and ground_layout_ready
        and len(mountains) == MOUNTAIN_INSTANCE_COUNT
        and len(unique_mountain_meshes) == 1
        and all(count == MOUNTAIN_EXPECTED_TRIANGLES for count in mountain_triangle_counts)
        and 320_000 <= instance_triangles <= 370_000
        and extent.x >= 1_750.0
        and extent.y >= 1_750.0
        and extent.z >= 60.0
        and foundation_extent.x >= 339.0
        and foundation_extent.y >= 319.0
        and 0.05 <= foundation_extent.z <= 0.25
        and -0.08 <= foundation_maximum.z <= -0.04
        and foundation.get("geometry_license") == "MIT"
        and foundation.get("surface_asset_license") == CC0_LICENSE
        and {layer.name for layer in foundation.data.uv_layers} >= {"GroundUV", "MountainUV"}
        and material_names >= {"JianghaiCompactedGroundPBR", "JianghaiRockyValleyPBR"}
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
        and mountain_burial_clearance >= 0.5
        and not legacy_valley_data
        and hero_material_ready
        and uniform_positive_scales
        and opaque_materials
        and visual_only
    )
    ground_band_report = ",".join(
        f"{token}:{ground_mesh.get(f'height_p10_p90_{token}', 0.0):.3f}/"
        f"{ground_mesh.get(f'slope_p90_{token}', 0.0):.4f}/"
        f"{ground_mesh.get(f'normal_z_std_{token}', 0.0):.5f}"
        for token in (
            "140_180", "180_220", "220_300", "300_400",
            "400_500", "500_560", "560_601",
        )
    )
    screen_relief_report = ",".join(
        f"{metric['height_p10_p90']:.3f}/"
        f"{metric['normal_z_std']:.5f}/"
        f"{metric['samples']}"
        for metric in screen_relief
    )
    print(
        "JIANGHAI_VALLEY_CHECK "
        f"valid={valid} foundation_triangles={foundation_triangles} "
        f"ground={len(ground_scans)}:{ground_triangle_counts[0]} "
        f"ground_instance_triangles={ground_instance_triangles} "
        f"ground_components={ground_topology['connected_components']} "
        f"ground_boundary={ground_topology['boundary_components']}:{ground_topology['boundary_edges']} "
        f"ground_boundary_z=({ground_topology['boundary_minimum_z']:.3f},"
        f"{ground_topology['boundary_maximum_z']:.3f}) "
        f"ground_degenerate={ground_topology['degenerate_faces']} "
        f"ground_invalid_normals={ground_topology['invalid_face_normals']} "
        f"ground_max_edge={ground_topology['maximum_terrain_edge']:.3f} "
        f"ground_max_edge_area_ratio="
        f"{ground_topology['maximum_terrain_edge_area_ratio']:.3f} "
        f"ground_repair_ready={ground_repair_ready} "
        f"ground_envelope_ready={ground_envelope_ready} "
        f"ground_composite_relief={composite_top_relief:.3f} "
        f"ground_height_residual=("
        f"{ground_mesh.get('height_residual_rms', 0.0):.4f},"
        f"{ground_mesh.get('height_residual_maximum', 0.0):.4f}) "
        f"ground_asset_relief=(rms="
        f"{ground_mesh.get('asset_height_residual_rms', 0.0):.4f},"
        f"inner_p10p90={ground_mesh.get('inner_band_height_p10_p90', 0.0):.3f},"
        f"outer_p10p90={ground_mesh.get('outer_band_height_p10_p90', 0.0):.3f}) "
        f"ground_foundation_distance_relief=(0_60:"
        f"{ground_mesh.get('foundation_near_0_60_height_p10_p90', 0.0):.3f},"
        f"60_160:"
        f"{ground_mesh.get('foundation_mid_60_160_height_p10_p90', 0.0):.3f}) "
        f"ground_slope=(rms={ground_mesh.get('surface_slope_rms', 0.0):.4f},"
        f"p90={ground_mesh.get('surface_slope_p90', 0.0):.4f},"
        f"p99={ground_mesh.get('surface_slope_p99', 0.0):.4f},"
        f"max={ground_mesh.get('surface_slope_maximum', 0.0):.4f}) "
        f"ground_normal=(std="
        f"{ground_mesh.get('surface_normal_z_standard_deviation', 0.0):.5f},"
        f"p10={ground_mesh.get('surface_normal_z_p10', 1.0):.5f}) "
        f"ground_diagonals=("
        f"{ground_mesh.get('top_diagonal_orientation_a', 0)},"
        f"{ground_mesh.get('top_diagonal_orientation_b', 0)}) "
        f"ground_radial_spacing_dev="
        f"{ground_mesh.get('radial_spacing_rms_deviation', 0.0):.4f} "
        f"ground_bands={ground_band_report} "
        f"ground_uv={ground_uv['layer_count']}:{ground_uv['loop_count']} "
        f"ground_uv_error={ground_uv['maximum_error']:.8f} "
        f"ground_uv_jacobian=("
        f"{ground_mesh.get('uv_normalized_jacobian_minimum', 0.0):.4f},"
        f"{ground_mesh.get('uv_normalized_jacobian_maximum', 0.0):.4f}) "
        f"ground_uv_ready={ground_uv_ready} "
        f"ground_world_uv_tile=({min(ground_world_uv_tiles):.3f},"
        f"{max(ground_world_uv_tiles):.3f}) "
        f"ground_world_uv_scale_ready={ground_world_uv_scale_ready} "
        f"ground_surface_ready={ground_surface_ready} "
        f"ground_base_color_factor="
        f"{tuple(round(value, 3) for value in coast_surface_material.get('base_color_factor', ())) if coast_surface_material else ()} "
        f"legacy_coast_materials={len(legacy_coast_materials)} "
        f"legacy_coast_images={len(legacy_coast_images)} "
        f"ground_layout_ready={ground_layout_ready} "
        f"mountains={len(mountains)}:{sorted(set(mountain_triangle_counts))} "
        f"unique_mountain_meshes={len(unique_mountain_meshes)} "
        f"instance_triangles={instance_triangles} "
        f"extent=({extent.x:.1f},{extent.y:.1f},{extent.z:.1f}) "
        f"foundation_extent=({foundation_extent.x:.1f},{foundation_extent.y:.1f},{foundation_extent.z:.2f}) "
        f"foundation_top={foundation_maximum.z:.3f} "
        f"ground_extent=({ground_extent.x:.1f},{ground_extent.y:.1f},{ground_extent.z:.1f}) "
        f"ground_safe_top={ground_mesh.get('safe_inner_top_maximum', 0.0):.3f} "
        f"ground_camera_clearance=({','.join(f'{value:.3f}' for value in camera_clearances)}) "
        f"ground_coverage={ground_coverage:.3f} "
        f"foundation_edge={ground_continuity['foundation_edge_hits']}/"
        f"{ground_continuity['foundation_edge_samples']}:"
        f"gap={ground_continuity['foundation_edge_maximum_gap']:.3f} "
        f"ground_ring={ground_continuity['ring_hits']}/"
        f"{ground_continuity['ring_samples']}:"
        f"height=({ground_continuity['ring_minimum_height']:.3f},"
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
        f"ground_player_view_foundation_occluded_skirt="
        f"{ground_continuity['player_view_foundation_occluded_skirt_hits']} "
        f"ground_player_view_near_double_top="
        f"{ground_continuity['player_view_near_coplanar_double_top_hits']} "
        f"ground_screen_relief=({screen_relief_report}) "
        f"south_ground_ray={south_ground_seam['south_ground_top_hits']}/"
        f"{south_ground_seam['south_ground_samples']}:"
        f"side={south_ground_seam['south_ground_distant_side_hits']}:"
        f"relief={south_ground_seam['south_ground_height_p10_p90']:.3f} "
        f"north_edge_endcaps={north_edge_endcaps_ready}:"
        f"{north_edge_boundary_top:.3f} "
        f"north_edge_ray={north_edge_seam['north_edge_hits']}/"
        f"{north_edge_seam['north_edge_samples']}:"
        f"side={north_edge_seam['north_edge_distant_side_hits']} "
        f"mountains_outside={mountains_outside} max_angular_gap={max_angular_gap:.3f} "
        f"max_inner_ring_radius={maximum_inner_ring_radius:.1f} "
        f"max_outer_ring_radius={maximum_outer_ring_radius:.1f} "
        f"mountain_layout_ready={mountain_layout_ready} "
        f"mountain_ring_gaps=({inner_ring_angular_gap:.3f},"
        f"{outer_ring_angular_gap:.3f}) "
        f"mountain_ring_angles_ready={mountain_ring_angles_ready} "
        f"mountain_bottoms=({min(mountain_bottoms):.1f},{max(mountain_bottoms):.1f}) "
        f"mountain_edge_top={max(mountain_edge_tops):.1f} "
        f"mountain_boundary_delta={mountain_boundary_height_delta:.4f} "
        f"mountain_burial_clearance={mountain_burial_clearance:.3f} "
        f"legacy_valley_data={legacy_valley_data} "
        f"hero_material_ready={hero_material_ready} "
        f"uniform_positive_scales={uniform_positive_scales} "
        f"opaque_materials={opaque_materials} visual_only={visual_only}"
    )
    if not valid:
        raise RuntimeError("Jianghai valley environment failed its DCC quality gate")
    return foundation_triangles, instance_triangles, extent


def main() -> None:
    if Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
        raise RuntimeError(f"Open the authoritative Jianghai scene before authoring: {BLEND_PATH}")
    source_root = acquisition_root()
    existing = bpy.data.objects.get(VALLEY_ROOT_NAME)
    if existing is not None:
        remove_hierarchy(existing)

    city_root = bpy.data.objects.get("JianghaiOldCityAuthoredScene")
    if city_root is None:
        raise RuntimeError("JianghaiOldCityAuthoredScene root is missing")
    valley_root = bpy.data.objects.new(VALLEY_ROOT_NAME, None)
    bpy.context.scene.collection.objects.link(valley_root)
    valley_root.parent = city_root
    valley_root["visual_role"] = "authored_valley_environment"
    valley_root["collision_role"] = "visual_only"
    valley_root["composition_license"] = "MIT"
    valley_root["source_asset_licenses"] = f"{CC0_LICENSE}; {HERO_MOUNTAIN_LICENSE}"
    valley_root["rights_boundary"] = (
        "Project-authored foundation/composition (MIT); "
        "Poly Haven source assets (CC0); solararchitect Hero Mountain (CC BY 4.0)"
    )
    valley_root["coast_line_01_source_url"] = COAST_LINE_URL
    valley_root["hero_mountain_source_url"] = HERO_MOUNTAIN_URL
    valley_root["hero_mountain_source_creator"] = "solararchitect"
    valley_root["hero_mountain_source_license"] = HERO_MOUNTAIN_LICENSE
    valley_root["hero_mountain_source_md5s"] = (
        "obj=af949f14c8fb8138bf75f2a70769b2be; "
        "color=1480eb4cadc8c531055b0b39ea5ab50d; "
        "normal=7f16993db123397c80fcec42e586729b; "
        "roughness=e46afb87a2dbe6c2843eb14864245ffe"
    )
    valley_root["rocky_terrain_source_url"] = ROCKY_TERRAIN_URL
    valley_root["gravel_floor_source_url"] = GRAVEL_FLOOR_URL
    valley_root["source_creators"] = (
        "Rob Tuytel; Rico Cilliers; Amal Kumar; Charlotte Baglioni; "
        "solararchitect"
    )
    valley_root["acquisition_dates"] = "2026-08-28; 2026-08-29"

    ground_material = create_surface_material(
        source_root, "gravel_floor_03", "JianghaiCompactedGroundPBR",
        "JianghaiGravelFloor03", GRAVEL_FLOOR_URL, "Charlotte Baglioni", "GroundUV", 0.78,
    )
    mountain_material = create_surface_material(
        source_root, "rocky_terrain", "JianghaiRockyValleyPBR",
        "JianghaiRockyTerrain", ROCKY_TERRAIN_URL, "Amal Kumar", "MountainUV", 0.36,
    )
    foundation = configure_foundation(valley_root, ground_material, mountain_material)
    north_edge_adjusted, north_edge_boundary_top = bury_north_avenue_endcaps()
    valley_root["north_edge_dcc_endcaps"] = "; ".join(NORTH_EDGE_OBJECT_NAMES)
    valley_root["north_edge_blend_length_meters"] = NORTH_EDGE_BLEND_LENGTH
    valley_root["north_edge_target_top"] = NORTH_EDGE_TARGET_TOP
    valley_root["north_edge_adjusted_objects"] = north_edge_adjusted
    valley_root["north_edge_boundary_top"] = north_edge_boundary_top
    sources = {
        asset_id: import_and_decimate_asset(source_root, asset_id)
        for asset_id in ASSET_SPECS
    }
    usage_counts: dict[str, int] = {}
    source_ground_scans = create_perimeter_ground(
        valley_root,
        sources,
        usage_counts,
    )
    ground_scans = [
        create_composite_ground(valley_root, foundation, source_ground_scans)
    ]
    mountains = create_mountains(valley_root, sources, usage_counts)
    foundation_triangles, instance_triangles, extent = validate_environment(
        foundation,
        ground_scans,
        mountains,
    )

    city_root["jianghai_valley_environment"] = (
        "Project-authored OldCityFoundation (MIT) + Poly Haven Coast Line 01, "
        "Rocky Terrain, and Gravel Floor 03 (CC0) + solararchitect Hero Mountain "
        "(CC BY 4.0); acquired 2026-08-28 and 2026-08-29"
    )
    valley_root["foundation_triangles"] = foundation_triangles
    valley_root["perimeter_ground_count"] = len(ground_scans)
    valley_root["mountain_count"] = len(mountains)
    valley_root["source_mesh_count"] = len({obj.data for obj in [*ground_scans, *mountains]})
    valley_root["instance_triangles"] = instance_triangles
    valley_root["extent_meters"] = tuple(round(value, 3) for value in extent)
    if os.environ.get("JIANGHAI_VALLEY_VALIDATE_ONLY") == "1":
        print(
            "JIANGHAI_VALLEY_VALIDATE_ONLY_COMPLETE "
            "validated=True saved=False"
        )
        return
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    print(
        "JIANGHAI_VALLEY_BUILD_COMPLETE "
        f"blend={BLEND_PATH} foundation={FOUNDATION_NAME} "
        f"ground_scans={len(ground_scans)} mountains={len(mountains)} "
        f"north_edge_adjusted={north_edge_adjusted} "
        f"instance_triangles={instance_triangles}"
    )


if __name__ == "__main__":
    main()
