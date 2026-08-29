"""Print deterministic geometry, image, and instancing statistics for Jianghai Old City."""

from __future__ import annotations

from collections import Counter
from collections.abc import Mapping
from math import atan2, cos, hypot, isclose, isfinite, pi, sin, sqrt, tan
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0, str(Path(__file__).resolve().parent))
from jianghai_chinese_district_layout import JIANGHAI_DEPLOYMENT_POINTS


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
MOUNTAIN_INSTANCE_COUNT = 12
MOUNTAIN_EXPECTED_TRIANGLES = 14_000
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
SCENE_TRIANGLE_BUDGET = 3_200_000
MAX_RUNTIME_TEXTURE_SIZE = 512
RETIRED_VISIBLE_MESH_NAMES = {
    "Cube.286",
    "hhugu.001",
    "JianghaiDensity_OldUrban_LOD",
    "JianghaiDensity_ScanStreet_LOD",
}


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


def coast_uv_coordinates(x: float, y: float) -> tuple[float, float]:
    return (
        x / COAST_UV_TILE_SIZE_LOCAL,
        y / COAST_UV_TILE_SIZE_LOCAL,
    )


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


def object_world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in corners) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in corners) for axis in range(3))),
    )


def overlap_depths(first: bpy.types.Object, second: bpy.types.Object) -> Vector:
    first_min, first_max = object_world_bounds(first)
    second_min, second_max = object_world_bounds(second)
    return Vector(
        tuple(
            min(first_max[axis], second_max[axis])
            - max(first_min[axis], second_min[axis])
            for axis in range(3)
        )
    )


def evaluated_geometry_statistics() -> tuple[int, int, Counter[str]]:
    """Count the geometry Blender actually evaluates for the scene export."""

    depsgraph = bpy.context.evaluated_depsgraph_get()
    geometry_objects = 0
    triangles = 0
    triangles_by_type: Counter[str] = Counter()
    for instance in depsgraph.object_instances:
        obj = instance.object
        # Blender exposes each bevelled Curve twice here: once as the source
        # CURVE and once as its evaluated MESH. Counting only evaluated MESH
        # instances matches glTF/Godot and avoids double-counting every curve.
        if obj.type != "MESH":
            continue
        mesh = obj.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        if mesh is None:
            continue
        try:
            mesh.calc_loop_triangles()
            object_triangles = len(mesh.loop_triangles)
            triangles += object_triangles
            triangles_by_type[obj.type] += object_triangles
            geometry_objects += 1
        finally:
            obj.to_mesh_clear()
    return geometry_objects, triangles, triangles_by_type


mesh_users = Counter(obj.data.name for obj in bpy.context.scene.objects if obj.type == "MESH")
rows = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    rows.append((triangle_count(obj.data), len(obj.data.vertices), mesh_users[obj.data.name], obj.name, obj.data.name))

print("JIANGHAI_AUDIT_BEGIN")
for triangles, vertices, users, object_name, mesh_name in sorted(rows, reverse=True)[:80]:
    print(
        f"JIANGHAI_MESH triangles={triangles} vertices={vertices} users={users} "
        f"object={object_name!r} data={mesh_name!r}"
    )

images = []
for image in bpy.data.images:
    if image.type != "IMAGE":
        continue
    width, height = image.size
    images.append((width * height, width, height, image.source, image.packed_file is not None, image.name, image.filepath))
for _, width, height, source, packed, name, filepath in sorted(images, reverse=True):
    print(
        f"JIANGHAI_IMAGE size={width}x{height} source={source} packed={packed} "
        f"name={name!r} path={filepath!r}"
    )

print(
    f"JIANGHAI_AUDIT_END objects={len(rows)} unique_meshes={len(mesh_users)} "
    f"object_triangles={sum(row[0] for row in rows)} "
    f"unique_triangles={sum(triangle_count(mesh) for mesh in bpy.data.meshes if mesh.users > 0)}"
)

evaluated_objects, evaluated_triangles, evaluated_by_type = evaluated_geometry_statistics()
print(
    "JIANGHAI_EVALUATED_GEOMETRY "
    f"objects={evaluated_objects} triangles={evaluated_triangles} "
    f"by_type={','.join(f'{key}:{value}' for key, value in sorted(evaluated_by_type.items()))}"
)

required_anchors = {
    "AuthoredStreetNetwork",
    "JianghaiValleyEnvironment",
    "JianghaiTenementDistrict",
    "RedStarElectronicsFactory",
    "GuangchangPawnshop",
    "OldCityMarketBridge",
    "GrandHotelSecurityTerminalVisual",
    "MunicipalTreasuryManifestTerminalVisual",
}
missing_anchors = sorted(required_anchors.difference(bpy.data.objects.keys()))
valley_root = bpy.data.objects.get("JianghaiValleyEnvironment")
valley_foundation = bpy.data.objects.get("OldCityFoundation")
bpy.context.view_layer.update()
valley_ground_scans = sorted(
    (obj for obj in bpy.data.objects if obj.name.startswith("JianghaiPerimeterGround")),
    key=lambda obj: obj.name,
)
valley_mountains = sorted(
    (obj for obj in bpy.data.objects if obj.name.startswith("JianghaiMountainMassif")),
    key=lambda obj: obj.name,
)
valley_asset_specs = {
    "coast_line_01": {
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
        "url": "https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca",
        "creators": "solararchitect",
        "triangles": MOUNTAIN_EXPECTED_TRIANGLES,
        "mesh": "JianghaiHeroMountainDistantLOD",
        "material": "JianghaiHeroMountainPBR",
        "license": "CC BY 4.0",
    },
}


def source_metadata_ready(
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


def opaque_material_ready(
    material: bpy.types.Material,
    source_url: str,
    creators: str,
    acquisition_date: str,
    source_license: str = "CC0 1.0 Universal",
) -> bool:
    if (
        not source_metadata_ready(
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
    if alpha is None or alpha.is_linked or not isclose(alpha.default_value, 1.0, abs_tol=0.0001):
        return False
    source_images = {
        node.image
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    return bool(source_images) and all(
        source_metadata_ready(
            image, source_url, creators, acquisition_date, source_license
        )
        and image.packed_file is not None
        for image in source_images
    )


def asset_instance_ready(
    obj: bpy.types.Object,
    asset_id: str,
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
        (not require_asset_id or obj.get("source_asset_id") == asset_id)
        and obj.data.name == spec["mesh"]
        and triangle_count(obj.data) == spec["triangles"]
        and source_metadata_ready(
            obj, spec["url"], spec["creators"], "2026-08-29", spec["license"]
        )
        and source_metadata_ready(
            obj.data, spec["url"], spec["creators"], "2026-08-29", spec["license"]
        )
        and len(materials) == 1
        and materials[0].name == spec["material"]
        and opaque_material_ready(
            materials[0],
            surface_url,
            surface_creators,
            surface_acquisition_date,
            surface_license,
        )
    )


def uniform_positive_scale(obj: bpy.types.Object) -> bool:
    return (
        min(obj.scale) > 0.0
        and max(obj.scale) / min(obj.scale) <= 1.001
        and obj.matrix_world.determinant() > 0.0
    )


def perimeter_ground_coverage(
    foundation: bpy.types.Object | None,
    ground_scans: list[bpy.types.Object],
) -> float:
    if foundation is None or not ground_scans:
        return 0.0
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


def godot_to_blender(vector: Vector) -> Vector:
    return Vector((vector.x, -vector.z, vector.y))


def percentile(values: list[float], fraction: float) -> float:
    ordered = sorted(values)
    if not ordered:
        return 0.0
    index = round((len(ordered) - 1) * fraction)
    return ordered[min(len(ordered) - 1, max(0, index))]


def north_edge_seam_statistics(
    depsgraph: bpy.types.Depsgraph,
) -> dict[str, float | int]:
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
                    f"distance={distance:.3f} normal_z={normal.z:.5f}"
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
            direction = godot_to_blender((
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
            percentile(heights, 0.90) - percentile(heights, 0.10)
        ),
    }


def ground_screen_relief_statistics(
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
        world_origin = godot_to_blender(godot_origin)
        inverse = ground.matrix_world.inverted_safe()
        heights = []
        normal_zs = []
        for pixel_y in pixel_ys:
            screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
            for pixel_x in range(20, 1261, 20):
                screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
                direction = godot_to_blender((
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
            "height_p10_p90": percentile(heights, 0.90) - percentile(heights, 0.10),
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
        world_origin = godot_to_blender(godot_origin)
        for pixel_y in pixel_ys:
            screen_y = 1.0 - 2.0 * (pixel_y + 0.5) / 720.0
            for pixel_x in pixel_xs:
                screen_x = 2.0 * (pixel_x + 0.5) / 1280.0 - 1.0
                world_direction = godot_to_blender((
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


def ground_continuity_statistics(
    foundation: bpy.types.Object | None,
    ground_scans: list[bpy.types.Object],
) -> dict[str, float | int]:
    if foundation is None or not ground_scans:
        return {
            "foundation_edge_samples": 0,
            "foundation_edge_hits": 0,
            "foundation_edge_coverage": 0.0,
            "foundation_edge_maximum_gap": float("inf"),
            "foundation_edge_minimum_height": float("-inf"),
            "foundation_edge_maximum_height": float("inf"),
            "ring_samples": 0,
            "ring_hits": 0,
            "ring_coverage": 0.0,
            "ring_minimum_height": float("-inf"),
            "ring_maximum_height": float("inf"),
            "top_surface_hits": 0,
            "multi_top_hits": 0,
            "near_coplanar_double_top_hits": 0,
            "critical_overlap_samples": 0,
            "critical_overlap_hits": 0,
            "critical_overlap_coverage": 0.0,
            "vertical_skirt_highest_hits": 0,
            "player_view_samples": 0,
            "player_view_top_hits": 0,
            "player_view_visible_skirt_hits": 0,
            "player_view_foundation_occluded_skirt_hits": 0,
            "player_view_near_coplanar_double_top_hits": 0,
        }
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    foundation_minimum, foundation_maximum = object_world_bounds(foundation)
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
                top_heights, default=float("-inf")
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


valley_foundation_triangles = (
    triangle_count(valley_foundation.data)
    if valley_foundation is not None and valley_foundation.type == "MESH"
    else 0
)
valley_ground_triangle_counts = [
    triangle_count(ground.data) for ground in valley_ground_scans if ground.type == "MESH"
]
valley_mountain_triangle_counts = [
    triangle_count(mountain.data)
    for mountain in valley_mountains
    if mountain.type == "MESH"
]
valley_instance_triangles = (
    valley_foundation_triangles
    + sum(valley_ground_triangle_counts)
    + sum(valley_mountain_triangle_counts)
)
valley_ground_instance_triangles = sum(valley_ground_triangle_counts)
valley_objects = (
    [valley_foundation, *valley_ground_scans, *valley_mountains]
    if valley_foundation is not None
    else [*valley_ground_scans, *valley_mountains]
)
valley_bounds = [object_world_bounds(obj) for obj in valley_objects]
valley_minimum = Vector(
    tuple(min(bounds[0][axis] for bounds in valley_bounds) for axis in range(3))
) if valley_bounds else Vector()
valley_maximum = Vector(
    tuple(max(bounds[1][axis] for bounds in valley_bounds) for axis in range(3))
) if valley_bounds else Vector()
valley_extent = valley_maximum - valley_minimum
valley_foundation_bounds = (
    object_world_bounds(valley_foundation)
    if valley_foundation is not None and valley_foundation.type == "MESH"
    else (Vector(), Vector())
)
valley_foundation_extent = valley_foundation_bounds[1] - valley_foundation_bounds[0]
valley_ground_bounds = [object_world_bounds(ground) for ground in valley_ground_scans]
valley_ground_minimum = Vector(
    tuple(min(bounds[0][axis] for bounds in valley_ground_bounds) for axis in range(3))
) if valley_ground_bounds else Vector()
valley_ground_maximum = Vector(
    tuple(max(bounds[1][axis] for bounds in valley_ground_bounds) for axis in range(3))
) if valley_ground_bounds else Vector()
valley_ground_extent = valley_ground_maximum - valley_ground_minimum
valley_ground_coverage = perimeter_ground_coverage(
    valley_foundation,
    valley_ground_scans,
)
valley_ground_continuity = ground_continuity_statistics(
    valley_foundation,
    valley_ground_scans,
)
valley_depsgraph = bpy.context.evaluated_depsgraph_get()
valley_north_edge_seam = north_edge_seam_statistics(valley_depsgraph)
valley_south_ground_seam = south_ground_seam_statistics(
    valley_ground_scans[0], valley_depsgraph
) if valley_ground_scans else {
    "south_ground_samples": 0,
    "south_ground_top_hits": 0,
    "south_ground_distant_side_hits": 1,
    "south_ground_height_p10_p90": 0.0,
}
valley_screen_relief = ground_screen_relief_statistics(
    valley_ground_scans[0], valley_depsgraph
) if valley_ground_scans else []
valley_camera_clearances = []
for godot_camera in (
    Vector((205.0, 3.2, 145.0)),
    Vector((112.0, 1.65, 86.0)),
    Vector((-118.0, 1.65, -207.0)),
):
    blender_camera = godot_to_blender(godot_camera)
    surface_height = highest_ground_height(
        blender_camera.x,
        blender_camera.y,
        valley_ground_scans,
        valley_depsgraph,
    )
    valley_camera_clearances.append(
        float("-inf")
        if surface_height is None
        else blender_camera.z - surface_height
    )
valley_north_edge_endcaps_ready = True
valley_north_edge_boundary_top = float("-inf")
for object_name in NORTH_EDGE_OBJECT_NAMES:
    obj = bpy.data.objects.get(object_name)
    if obj is None or obj.type != "MESH":
        valley_north_edge_endcaps_ready = False
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
    valley_north_edge_boundary_top = max(
        valley_north_edge_boundary_top, boundary_top
    )
    valley_north_edge_endcaps_ready = valley_north_edge_endcaps_ready and (
        obj.get("north_endcap_dcc_buried") is True
        and abs(
            obj.get("north_endcap_blend_length_meters", 0.0)
            - NORTH_EDGE_BLEND_LENGTH
        ) <= 0.000001
        and abs(
            obj.get("north_endcap_target_top", 0.0) - NORTH_EDGE_TARGET_TOP
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
valley_mountain_bounds = [object_world_bounds(mountain) for mountain in valley_mountains]
valley_mountain_bottoms = [minimum.z for minimum, _ in valley_mountain_bounds]
valley_mountain_edge_tops = [
    max(
        (mountain.matrix_world @ vertex.co).z
        for vertex in mountain.data.vertices
        if max(abs(vertex.co.x), abs(vertex.co.y)) >= 0.99
    )
    for mountain in valley_mountains
]
valley_mountain_inner_radii = [
    min(
        hypot(world_vertex.x, world_vertex.y - 60.0)
        for world_vertex in (
            mountain.matrix_world @ vertex.co for vertex in mountain.data.vertices
        )
    )
    for mountain in valley_mountains
]
valley_maximum_inner_ring_radius = (
    max(valley_mountain_inner_radii[:6]) if valley_mountain_inner_radii else float("inf")
)
valley_maximum_outer_ring_radius = (
    max(valley_mountain_inner_radii[6:]) if valley_mountain_inner_radii else float("inf")
)
valley_mountains_outside = all(
    maximum.x < -170.0 or minimum.x > 170.0
    or maximum.y < -100.0 or minimum.y > 220.0
    for minimum, maximum in valley_mountain_bounds
)
valley_mountain_angles = sorted(
    atan2(((minimum + maximum) * 0.5).y - 60.0,
          ((minimum + maximum) * 0.5).x) % (2.0 * pi)
    for minimum, maximum in valley_mountain_bounds
)
valley_max_angular_gap = max(
    (valley_mountain_angles[(index + 1) % len(valley_mountain_angles)] - angle)
    % (2.0 * pi)
    for index, angle in enumerate(valley_mountain_angles)
) if valley_mountain_angles else float("inf")
valley_ground_meshes = {
    ground.data for ground in valley_ground_scans if ground.type == "MESH"
}
valley_mountain_meshes = {
    mountain.data for mountain in valley_mountains if mountain.type == "MESH"
}
valley_ground_mesh = next(iter(valley_ground_meshes), None)
valley_ground_topology = (
    mesh_topology_statistics(valley_ground_mesh)
    if valley_ground_mesh is not None
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
valley_ground_repair_ready = (
    valley_ground_mesh is not None
    and valley_ground_mesh.get("dcc_composite_terrain") is True
    and valley_ground_mesh.get("dcc_source_scan_count") == 8
    and valley_ground_mesh.get("dcc_source_welded_vertices")
        == GROUND_EXPECTED_WELDED_VERTICES
    and valley_ground_mesh.get("single_valued_top_surface") is True
    and len(valley_ground_mesh.vertices) == GROUND_EXPECTED_VERTICES
    and abs(
        valley_ground_mesh.get("boundary_skirt_depth", 0.0)
        - GROUND_BOUNDARY_SKIRT_DEPTH
    ) <= 0.000001
    and valley_ground_mesh.get("boundary_skirt_source_edges")
        == GROUND_EXPECTED_BOUNDARY_EDGES
    and valley_ground_topology["connected_components"] == 1
    and valley_ground_topology["boundary_edges"] == GROUND_EXPECTED_BOUNDARY_EDGES
    and valley_ground_topology["boundary_components"]
        == GROUND_EXPECTED_BOUNDARY_COMPONENTS
    and valley_ground_topology["nonmanifold_edges"] == GROUND_EXPECTED_BOUNDARY_EDGES
    and valley_ground_topology["degenerate_faces"] == 0
    and valley_ground_topology["invalid_face_normals"] == 0
    and valley_ground_topology["maximum_terrain_edge"] <= 9.0
    and valley_ground_topology["maximum_terrain_edge_area_ratio"] <= 15.0
    and len(valley_ground_mesh.polygons) == GROUND_EXPECTED_TRIANGLES
    and all(len(polygon.vertices) == 3 for polygon in valley_ground_mesh.polygons)
    and valley_ground_topology["boundary_maximum_z"]
        <= max(vertex.co.z for vertex in valley_ground_mesh.vertices) - 0.70
    and len(valley_ground_mesh.uv_layers) >= 1
)
valley_top_surface_vertex_count = (
    valley_ground_mesh.get("top_surface_vertex_count", 0)
    if valley_ground_mesh is not None else 0
)
valley_top_surface_vertices = (
    list(valley_ground_mesh.vertices[:valley_top_surface_vertex_count])
    if valley_ground_mesh is not None else []
)
valley_composite_top_relief = (
    max(vertex.co.z for vertex in valley_top_surface_vertices)
    - min(vertex.co.z for vertex in valley_top_surface_vertices)
    if valley_top_surface_vertices else 0.0
)
valley_mountain_overlap_ground_minimum = min(
    (
        vertex.co.z
        for vertex in valley_top_surface_vertices
        if hypot(vertex.co.x, vertex.co.y - 60.0) >= 350.0
    ),
    default=float("-inf"),
)
valley_mountain_burial_clearance = (
    valley_mountain_overlap_ground_minimum - max(valley_mountain_edge_tops)
)
valley_ground_envelope_ready = (
    valley_ground_mesh is not None
    and valley_ground_mesh.get("dcc_composite_terrain") is True
    and valley_ground_mesh.get("top_radial_samples")
        == GROUND_COMPOSITE_RADIAL_SAMPLES
    and valley_ground_mesh.get("top_angular_samples")
        == GROUND_COMPOSITE_ANGLE_SAMPLES
    and valley_ground_mesh.get("top_surface_face_count")
        == (GROUND_COMPOSITE_RADIAL_SAMPLES - 1)
            * GROUND_COMPOSITE_ANGLE_SAMPLES * 2
    and valley_ground_mesh.get("top_surface_quad_count")
        == (GROUND_COMPOSITE_RADIAL_SAMPLES - 1) * GROUND_COMPOSITE_ANGLE_SAMPLES
    and valley_ground_mesh.get("top_diagonal_orientation_a") == 41_400
    and valley_ground_mesh.get("top_diagonal_orientation_b") == 41_400
    and valley_ground_mesh.get("boundary_skirts_buried") is True
    and valley_ground_mesh.get("coast_projected_flip_count") == 0
    and 13.0 <= valley_composite_top_relief <= 18.0
    and valley_ground_mesh.get("height_residual_rms", 0.0) >= 0.005
    and valley_ground_mesh.get("height_residual_maximum", 0.0) >= 0.01
    and valley_ground_mesh.get("asset_height_residual_rms", 0.0) >= 0.03
    and 0.05 <= valley_ground_mesh.get("inner_band_height_p10_p90", 0.0) <= 0.40
    and valley_ground_mesh.get("outer_band_height_p10_p90", 0.0) >= 0.45
    and valley_ground_mesh.get("surface_slope_rms", 0.0) >= 0.03
    and valley_ground_mesh.get("surface_slope_p90", 0.0) >= 0.03
    and valley_ground_mesh.get("surface_slope_p99", float("inf")) < 0.30
    and valley_ground_mesh.get("surface_slope_maximum", float("inf")) < 0.80
    and valley_ground_mesh.get("surface_normal_z_standard_deviation", 0.0) >= 0.003
    and valley_ground_mesh.get("surface_normal_z_p10", 1.0) <= 0.9993
    and valley_ground_mesh.get("radial_spacing_rms_deviation", 0.0) >= 0.25
    and valley_ground_mesh.get("foundation_signed_distance_mask") is True
    and valley_ground_mesh.get("foundation_footprint_top_face_count") == 25
    and valley_ground_mesh.get("foundation_footprint_boundary_edge_count") == 16
    and valley_ground_mesh.get("foundation_safe_margin_meters")
        == GROUND_FOUNDATION_MARGIN
    and valley_ground_mesh.get("foundation_relief_end_distance_meters")
        == GROUND_FOUNDATION_RELIEF_END_DISTANCE
    and valley_ground_mesh.get("foundation_relief_full_gain_distance_meters")
        == GROUND_FOUNDATION_RELIEF_FULL_GAIN_DISTANCE
    and valley_ground_mesh.get("safe_inner_top_maximum", float("inf")) <= -0.08
    and 0.95 <= valley_ground_mesh.get(
        "foundation_near_0_60_height_p10_p90", 0.0
    ) <= 2.50
    and 3.00 <= valley_ground_mesh.get(
        "foundation_mid_60_160_height_p10_p90", 0.0
    ) <= 6.00
    and valley_ground_mesh.get("asset_lowpass_passes") == 14
    and valley_ground_mesh.get("asset_broad_passes") == 32
    and valley_ground_mesh.get("broad_relief_gain_minimum") == 4.0
    and valley_ground_mesh.get("broad_relief_gain_maximum") == 40.0
    and valley_ground_mesh.get("lowpass_relief_gain_minimum") == 1.2
    and valley_ground_mesh.get("lowpass_relief_gain_maximum") == 10.0
    and valley_ground_mesh.get("relief_gain_easing")
        == "C1 foundation-distance near boost and post-160m smoothstep"
    and 3.50 <= valley_ground_mesh.get("height_p10_p90_400_500", 0.0) <= 9.00
    and 4.50 <= valley_ground_mesh.get("height_p10_p90_500_560", 0.0) <= 14.00
    and 5.00 <= valley_ground_mesh.get("height_p10_p90_560_601", 0.0) <= 14.00
    and valley_ground_mesh.get("safe_transition_slope_p95", float("inf")) < 0.30
    and valley_ground_mesh.get("transition_boundary_slope_p95", float("inf")) < 0.20
    and valley_ground_mesh.get("normal_readability_gate")
        == (
            "asset-derived relief bands paired with slope p99 < 0.30, "
            "slope maximum < 0.80, and finite normal variation"
        )
    and valley_ground_mesh.get("height_source_method")
        == (
            "Foundation-footprint signed-distance blend of low-pass and broad "
            "residual extracted exclusively from eight transformed Coast Line "
            "01 scans; no synthetic height noise"
        )
)
valley_ground_uv = (
    coast_uv_statistics(valley_ground_mesh)
    if valley_ground_mesh is not None
    else {
        "layer_count": 0,
        "loop_count": 0,
        "finite": False,
        "maximum_error": float("inf"),
    }
)
valley_ground_uv_ready = (
    valley_ground_mesh is not None
    and valley_ground_mesh.get("continuous_planar_uv") is True
    and valley_ground_mesh.get("continuous_world_uv_warp") is False
    and valley_ground_mesh.get("continuous_uv_layer") == COAST_UV_LAYER_NAME
    and abs(
        valley_ground_mesh.get("uv_tile_size_local", 0.0)
        - COAST_UV_TILE_SIZE_LOCAL
    ) <= 0.000001
    and {layer.name for layer in valley_ground_mesh.uv_layers} == {COAST_UV_LAYER_NAME}
    and valley_ground_uv["loop_count"] == len(valley_ground_mesh.loops)
    and valley_ground_uv["finite"] is True
    and valley_ground_uv["maximum_error"] <= 0.00001
    and abs(valley_ground_mesh.get("uv_normalized_jacobian_minimum", 0.0) - 1.0)
        <= 0.000001
    and abs(valley_ground_mesh.get("uv_normalized_jacobian_maximum", 0.0) - 1.0)
        <= 0.000001
    and valley_ground_mesh.get("uv_mapping_method") == COAST_UV_MAPPING_METHOD
    and valley_ground_mesh.get("uv_macro_warp_method") is None
)
valley_ground_world_uv_tiles = [
    COAST_UV_TILE_SIZE_LOCAL * ground.scale.x for ground in valley_ground_scans
]
valley_ground_world_uv_scale_ready = (
    min(valley_ground_world_uv_tiles, default=0.0) >= 6.999
    and max(valley_ground_world_uv_tiles, default=float("inf")) <= 7.001
)
valley_ground_materials = (
    [material for material in valley_ground_mesh.materials if material is not None]
    if valley_ground_mesh is not None
    else []
)
valley_coast_surface_material = (
    valley_ground_materials[0] if len(valley_ground_materials) == 1 else None
)
valley_gravel_surface_material = bpy.data.materials.get("JianghaiCompactedGroundPBR")
valley_coast_surface_images = {
    node.image
    for node in (
        valley_coast_surface_material.node_tree.nodes
        if valley_coast_surface_material is not None
        and valley_coast_surface_material.use_nodes
        and valley_coast_surface_material.node_tree is not None
        else []
    )
    if node.type == "TEX_IMAGE" and node.image is not None
}
valley_gravel_surface_images = {
    node.image
    for node in (
        valley_gravel_surface_material.node_tree.nodes
        if valley_gravel_surface_material is not None
        and valley_gravel_surface_material.use_nodes
        and valley_gravel_surface_material.node_tree is not None
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
valley_ground_surface_ready = (
    valley_coast_surface_material is not None
    and valley_coast_surface_material.name == COAST_SURFACE_MATERIAL_NAME
    and source_metadata_ready(
        valley_coast_surface_material,
        "https://polyhaven.com/a/gravel_floor_03",
        "Charlotte Baglioni",
        "2026-08-28",
        "CC0 1.0 Universal",
    )
    and valley_coast_surface_material.get("surface_asset_id") == "gravel_floor_03"
    and valley_coast_surface_material.get("surface_source_md5s")
        == GRAVEL_SURFACE_SOURCE_MD5S
    and tuple(valley_coast_surface_material.get("base_color_factor", ()))
        == COAST_BASE_COLOR_FACTOR
    and valley_coast_surface_material.get("continuous_uv_map") == COAST_UV_LAYER_NAME
    and len([
        node for node in valley_coast_surface_material.node_tree.nodes
        if node.type == "UVMAP" and node.uv_map == COAST_UV_LAYER_NAME
    ]) == 1
    and len(valley_coast_surface_images) == 3
    and valley_coast_surface_images == valley_gravel_surface_images
    and {image.get("source_md5") for image in valley_coast_surface_images}
        == GRAVEL_SURFACE_IMAGE_MD5S
    and len([
        node for node in valley_coast_surface_material.node_tree.nodes
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
valley_ground_local_top = (
    max(vertex.co.z for vertex in valley_ground_mesh.vertices)
    if valley_ground_mesh is not None
    else 0.0
)
valley_ground_layout_ready = (
    len(valley_ground_scans) == GROUND_INSTANCE_COUNT
    and all(
        ground.name == "JianghaiPerimeterGroundComposite"
        and ground.location.length <= LAYOUT_POSITION_TOLERANCE
        and all(abs(value - 1.0) <= 0.000001 for value in ground.scale)
        and all(abs(value) <= 0.000001 for value in ground.rotation_euler)
        for ground in valley_ground_scans
    )
)
valley_mountain_source_ready = (
    len(valley_mountain_meshes) == 1
    and all(
        asset_instance_ready(
            mountain,
            "hero_mountain",
            valley_asset_specs["hero_mountain"],
            require_asset_id=True,
        )
        for mountain in valley_mountains
    )
)
valley_mountain_mesh = next(iter(valley_mountain_meshes), None)
valley_mountain_local_minimum = (
    min(vertex.co.z for vertex in valley_mountain_mesh.vertices)
    if valley_mountain_mesh is not None
    else 0.0
)
valley_mountain_layout_ready = all(
    abs(mountain.location.x - layout[0][0]) <= LAYOUT_POSITION_TOLERANCE
    and abs(mountain.location.y - layout[0][1]) <= LAYOUT_POSITION_TOLERANCE
    and abs(
        mountain.location.z
        - (-0.20 - layout[3] - valley_mountain_local_minimum * layout[1])
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
    for mountain, layout in zip(valley_mountains, MOUNTAIN_LAYOUT, strict=True)
)
valley_inner_ring_angular_gap = location_angular_gap(valley_mountains[:6])
valley_outer_ring_angular_gap = location_angular_gap(valley_mountains[6:])
valley_mountain_ring_angles_ready = (
    abs(valley_inner_ring_angular_gap - pi / 3.0) <= 0.000001
    and abs(valley_outer_ring_angular_gap - pi / 3.0) <= 0.000001
)
valley_mountain_boundary_height_delta = (
    max(
        vertex.co.z - valley_mountain_local_minimum
        for vertex in valley_mountain_mesh.vertices
        if max(abs(vertex.co.x), abs(vertex.co.y)) >= 0.99
    )
    if valley_mountain_mesh is not None
    else float("inf")
)
valley_ground_spec = valley_asset_specs["coast_line_01"]
valley_ground_source_ready = all(
    asset_instance_ready(
        ground,
        "coast_line_01",
        valley_ground_spec,
        require_asset_id=False,
    )
    for ground in valley_ground_scans
)
valley_uniform_positive_scales = all(
    uniform_positive_scale(obj) for obj in [*valley_ground_scans, *valley_mountains]
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
    and not any(node.type == "DISPLACEMENT" for node in hero_material.node_tree.nodes)
    and len([
        node for node in hero_material.node_tree.nodes
        if node.type == "OUTPUT_MATERIAL"
        and not node.inputs["Displacement"].is_linked
    ]) == 1
    and all(not mountain.modifiers for mountain in valley_mountains)
)
valley_surface_material_specs = {
    "JianghaiCompactedGroundPBR": (
        "https://polyhaven.com/a/gravel_floor_03",
        "Charlotte Baglioni",
    ),
    "JianghaiRockyValleyPBR": (
        "https://polyhaven.com/a/rocky_terrain",
        "Amal Kumar",
    ),
}
valley_foundation_materials = (
    {
        material.name: material
        for material in valley_foundation.data.materials
        if material is not None
    }
    if valley_foundation is not None and valley_foundation.type == "MESH"
    else {}
)
valley_foundation_materials_ready = all(
    material_name in valley_foundation_materials
    and opaque_material_ready(
        valley_foundation_materials[material_name], source_url, creators, "2026-08-28"
    )
    for material_name, (source_url, creators) in valley_surface_material_specs.items()
)
valley_collision_named = any(
    obj.name.lower().endswith(("-col", "-convcol"))
    for obj in ([valley_root] + list(valley_root.children_recursive))
) if valley_root is not None else True
valley_ready = (
    valley_root is not None
    and valley_root.parent == bpy.data.objects.get("JianghaiOldCityAuthoredScene")
    and valley_foundation is not None
    and valley_foundation.parent == valley_root
    and valley_foundation.get("collision_role") == "visual_only"
    and valley_foundation.get("geometry_license") == "MIT"
    and valley_foundation.get("surface_asset_license") == "CC0 1.0 Universal"
    and valley_foundation.get("acquisition_date") == "2026-08-28"
    and len(valley_ground_scans) == GROUND_INSTANCE_COUNT
    and all(ground.parent == valley_root for ground in valley_ground_scans)
    and all(ground.get("collision_role") == "visual_only" for ground in valley_ground_scans)
    and {ground.name for ground in valley_ground_scans}
        == {"JianghaiPerimeterGroundComposite"}
    and len(valley_ground_meshes) == 1
    and valley_ground_source_ready
    and valley_ground_instance_triangles
        == GROUND_INSTANCE_COUNT * GROUND_EXPECTED_TRIANGLES
    and valley_ground_repair_ready
    and valley_ground_envelope_ready
    and valley_ground_uv_ready
    and valley_ground_world_uv_scale_ready
    and valley_ground_surface_ready
    and valley_ground_layout_ready
    and len(valley_mountains) == MOUNTAIN_INSTANCE_COUNT
    and all(mountain.parent == valley_root for mountain in valley_mountains)
    and all(
        mountain.get("collision_role") == "visual_only"
        for mountain in valley_mountains
    )
    and all(
        mountain.get("visual_role") == "authored_distant_mountain"
        and mountain.get("source_license") == "CC BY 4.0"
        and mountain.get("explicit_yaw_radians") is not None
        for mountain in valley_mountains
    )
    and {mountain.name for mountain in valley_mountains}
        == {
            f"JianghaiMountainMassif{index:02d}"
            for index in range(MOUNTAIN_INSTANCE_COUNT)
        }
    and len(valley_mountain_meshes) == 1
    and valley_mountain_source_ready
    and all(
        count == MOUNTAIN_EXPECTED_TRIANGLES
        for count in valley_mountain_triangle_counts
    )
    and valley_mountains_outside
    and valley_max_angular_gap <= 0.55
    and valley_maximum_inner_ring_radius <= 360.0
    and valley_maximum_outer_ring_radius <= 500.0
    and valley_mountain_layout_ready
    and valley_mountain_ring_angles_ready
    and min(valley_mountain_bottoms) >= -22.1
    and max(valley_mountain_bottoms) <= -17.9
    and max(valley_mountain_edge_tops) <= -16.0
    and valley_mountain_boundary_height_delta <= 0.005
    and valley_mountain_mesh.get("boundary_tapered_for_valley_overlap") is True
    and valley_uniform_positive_scales
    and not valley_collision_named
    and valley_foundation_triangles == 188
    and len(valley_foundation.data.vertices) == 96
    and 320_000 <= valley_instance_triangles <= 370_000
    and evaluated_triangles <= SCENE_TRIANGLE_BUDGET
    and valley_extent.x >= 1_750.0
    and valley_extent.y >= 1_750.0
    and valley_extent.z >= 60.0
    and valley_foundation_extent.x >= 339.0
    and valley_foundation_extent.y >= 319.0
    and 0.05 <= valley_foundation_extent.z <= 0.25
    and -0.08 <= valley_foundation_bounds[1].z <= -0.04
    and {layer.name for layer in valley_foundation.data.uv_layers}
        >= {"GroundUV", "MountainUV"}
    and valley_foundation_materials_ready
    and valley_ground_extent.x >= 700.0
    and valley_ground_extent.y >= 700.0
    and valley_ground_mesh.get("safe_inner_top_maximum", float("inf")) <= -0.08
    and min(valley_camera_clearances, default=float("-inf")) >= 0.75
    and valley_ground_coverage >= 0.98
    and valley_ground_continuity["foundation_edge_coverage"] >= 0.98
    and valley_ground_continuity["foundation_edge_maximum_gap"] >= 0.10
    and valley_ground_continuity["foundation_edge_maximum_gap"] <= 0.35
    and valley_ground_continuity["ring_coverage"] == 1.0
    and valley_ground_continuity["ring_minimum_height"] >= -18.0
    and valley_ground_continuity["ring_maximum_height"] <= 18.0
    and valley_ground_continuity["multi_top_hits"] == 0
    and valley_ground_continuity["near_coplanar_double_top_hits"] == 0
    and valley_ground_continuity["vertical_skirt_highest_hits"] == 0
    and valley_ground_continuity["player_view_visible_skirt_hits"] == 0
    and valley_ground_continuity["player_view_near_coplanar_double_top_hits"] == 0
    and valley_south_ground_seam["south_ground_top_hits"]
        == valley_south_ground_seam["south_ground_samples"]
    and valley_south_ground_seam["south_ground_distant_side_hits"] == 0
    and all(metric["samples"] >= 100 for metric in valley_screen_relief)
    and all(metric["height_p10_p90"] >= 0.90 for metric in valley_screen_relief)
    and all(metric["normal_z_std"] >= 0.0005 for metric in valley_screen_relief)
    and valley_north_edge_endcaps_ready
    and valley_north_edge_seam["north_edge_hits"]
        == valley_north_edge_seam["north_edge_samples"]
    and valley_north_edge_seam["north_edge_distant_side_hits"] == 0
    and valley_mountain_burial_clearance >= 0.5
    and valley_root.get("collision_role") == "visual_only"
    and valley_root.get("composition_license") == "MIT"
    and valley_root.get("source_asset_licenses")
        == "CC0 1.0 Universal; CC BY 4.0"
    and valley_root.get("coast_line_01_source_url") == "https://polyhaven.com/a/coast_line_01"
    and valley_root.get("hero_mountain_source_url")
        == "https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca"
    and valley_root.get("hero_mountain_source_creator") == "solararchitect"
    and valley_root.get("hero_mountain_source_license") == "CC BY 4.0"
    and valley_root.get("hero_mountain_source_md5s")
        == (
            "obj=af949f14c8fb8138bf75f2a70769b2be; "
            "color=1480eb4cadc8c531055b0b39ea5ab50d; "
            "normal=7f16993db123397c80fcec42e586729b; "
            "roughness=e46afb87a2dbe6c2843eb14864245ffe"
        )
    and valley_root.get("rocky_terrain_source_url") == "https://polyhaven.com/a/rocky_terrain"
    and valley_root.get("gravel_floor_source_url") == "https://polyhaven.com/a/gravel_floor_03"
    and valley_root.get("north_edge_dcc_endcaps")
        == "; ".join(NORTH_EDGE_OBJECT_NAMES)
    and abs(
        valley_root.get("north_edge_blend_length_meters", 0.0)
        - NORTH_EDGE_BLEND_LENGTH
    ) <= 0.000001
    and abs(
        valley_root.get("north_edge_target_top", 0.0)
        - NORTH_EDGE_TARGET_TOP
    ) <= 0.000001
    and valley_root.get("north_edge_boundary_top", float("inf"))
        <= NORTH_EDGE_TARGET_TOP + 0.00001
    and valley_root.get("acquisition_dates") == "2026-08-28; 2026-08-29"
    and not legacy_valley_data
    and hero_material_ready
)
terminal_checks = []
for terminal_name in (
    "GrandHotelSecurityTerminalVisual",
    "MunicipalTreasuryManifestTerminalVisual",
):
    terminal = bpy.data.objects.get(terminal_name)
    meshes = [] if terminal is None else [child for child in terminal.children_recursive if child.type == "MESH"]
    finished_parts = [
        child for child in meshes
        if child.name.startswith("JianghaiArtPass_")
        and ("_CRT" in child.name or "Weather" in child.name)
        and triangle_count(child.data) >= 2_000
    ]
    terminal_checks.append(
        terminal is not None
        and len(meshes) == 7
        and len(finished_parts) == 2
        and any("AuthoredStatusScreen" in child.name for child in meshes)
    )

grand_root = bpy.data.objects.get("GrandHotelSecurityTerminalVisual")
municipal_root = bpy.data.objects.get("MunicipalTreasuryManifestTerminalVisual")
grand_screen = bpy.data.objects.get("GrandHotelSecurityTerminalVisual_AuthoredStatusScreen")
municipal_screen = bpy.data.objects.get("MunicipalTreasuryManifestTerminalVisual_AuthoredStatusScreen")
terminal_orientation_ready = (
    grand_root is not None
    and municipal_root is not None
    and grand_screen is not None
    and municipal_screen is not None
    and isclose(grand_root.rotation_euler.z, 0.0, abs_tol=0.001)
    and isclose(municipal_root.rotation_euler.z, 0.0, abs_tol=0.001)
    and grand_screen.matrix_world.translation.y < grand_root.matrix_world.translation.y - 0.15
    and municipal_screen.matrix_world.translation.y > municipal_root.matrix_world.translation.y + 0.15
)

facade_props = [
    obj
    for obj in bpy.context.scene.objects
    if obj.name.startswith(("JianghaiArtPass_EastAircon", "JianghaiArtPass_WestAircon"))
    or obj.name.startswith(("JianghaiArtPass_EastShutter", "JianghaiArtPass_WestShutter"))
]
facade_props_ready = len(facade_props) == 22
for prop in facade_props:
    bounds_x = [(prop.matrix_world @ Vector(corner)).x for corner in prop.bound_box]
    east_side = "_East" in prop.name
    expected_yaw = -pi * 0.5 if east_side else pi * 0.5
    facade_props_ready &= (
        isclose(prop.rotation_euler.z, expected_yaw, abs_tol=0.001)
        and (min(bounds_x) >= 9.5 if east_side else max(bounds_x) <= -9.5)
    )

factory_duplicate_shutter_removed = (
    bpy.data.objects.get("JianghaiArtPass_FactoryHeroShutter") is None
)

factory_gate_names = (
    "FactoryGatePortal_PierL",
    "FactoryGatePortal_PierR",
    "FactoryGatePortal_PierCapL",
    "FactoryGatePortal_PierCapR",
    "FactoryGatePortal_Roof",
)
factory_gate_objects = [bpy.data.objects.get(name) for name in factory_gate_names]
factory_gate_root = bpy.data.objects.get("RedStarElectronicsFactory")
factory_gate_portal_ready = all(obj is not None for obj in factory_gate_objects)
if factory_gate_portal_ready:
    left_pier, right_pier, left_cap, right_cap, roof = factory_gate_objects

    left_min, left_max = object_world_bounds(left_pier)
    right_min, right_max = object_world_bounds(right_pier)
    roof_min, roof_max = object_world_bounds(roof)
    door_half_width = 7.2956 * 0.5
    factory_gate_portal_ready &= (
        all(obj.parent == factory_gate_root for obj in factory_gate_objects)
        and all(isclose(obj.matrix_world.translation.y, 7.9245, abs_tol=0.001) for obj in factory_gate_objects[:4])
        and isclose(left_max.x, 86.0 - door_half_width, abs_tol=0.002)
        and isclose(right_min.x, 86.0 + door_half_width, abs_tol=0.002)
        and left_min.z <= 0.001
        and right_min.z <= 0.001
        and left_max.z >= 4.39
        and right_max.z >= 4.39
        and roof_min.x <= left_min.x - 0.5
        and roof_max.x >= right_max.x + 0.5
        and roof_min.y <= 7.9245 - 1.0
        and roof_max.y >= 7.9245 + 1.0
        and roof_min.z <= 4.22
        and roof_max.z >= 5.9
        and isclose(left_cap.matrix_world.translation.x, left_pier.matrix_world.translation.x, abs_tol=0.001)
        and isclose(right_cap.matrix_world.translation.x, right_pier.matrix_world.translation.x, abs_tol=0.001)
    )

object_triangles = sum(row[0] for row in rows)
images_ready = all(
    packed and width <= MAX_RUNTIME_TEXTURE_SIZE and height <= MAX_RUNTIME_TEXTURE_SIZE
    for _, width, height, _, packed, _, _ in images
)
retired_visible_objects = sorted(
    obj.name
    for obj in bpy.context.scene.objects
    if obj.type == "MESH" and obj.data is not None and obj.data.name in RETIRED_VISIBLE_MESH_NAMES
)
forbidden_export_objects = sorted(
    obj.name
    for obj in bpy.context.scene.objects
    if obj.name.startswith("__SOURCE_") or obj.type in {"ARMATURE", "FONT"} or obj.library is not None
)
finite_transforms = all(
    all(isfinite(value) for row in obj.matrix_world for value in row)
    for obj in bpy.context.scene.objects
)


def iter_property_text(value, depth=0):
    """Yield nested custom-property text without trusting marketplace metadata."""

    if depth > 8:
        return
    if isinstance(value, Mapping) or hasattr(value, "items"):
        for key, nested in value.items():
            yield str(key)
            yield from iter_property_text(nested, depth + 1)
        return
    if hasattr(value, "to_list"):
        yield from iter_property_text(value.to_list(), depth + 1)
        return
    if isinstance(value, (list, tuple)):
        for nested in value:
            yield from iter_property_text(nested, depth + 1)
        return
    yield str(value)


marketplace_marker_tokens = (
    "3d66",
    "www.3d66.com",
    "chinese wood house wall",
    "blenderkit_wood_house",
    "scan old brick building red small",
    "blenderkit_old_brick_factory",
    "fc8376f8-7c79-48b3-8a3c-bf061ace53e0",
)
marketplace_marker_hits = []
custom_property_sources = [
    *(f"object:{item.name}" for item in bpy.data.objects),
    *(f"mesh:{item.name}" for item in bpy.data.meshes),
    *(f"material:{item.name}" for item in bpy.data.materials),
    *(f"image:{item.name}" for item in bpy.data.images),
    *(f"collection:{item.name}" for item in bpy.data.collections),
    *(f"scene:{item.name}" for item in bpy.data.scenes),
    *(f"world:{item.name}" for item in bpy.data.worlds),
    *(f"node_group:{item.name}" for item in bpy.data.node_groups),
]
custom_property_blocks = [
    *bpy.data.objects,
    *bpy.data.meshes,
    *bpy.data.materials,
    *bpy.data.images,
    *bpy.data.collections,
    *bpy.data.scenes,
    *bpy.data.worlds,
    *bpy.data.node_groups,
]
for source_name, block in zip(custom_property_sources, custom_property_blocks, strict=True):
    custom_text = " ".join(iter_property_text({key: block[key] for key in block.keys()})).lower()
    matching_tokens = sorted(token for token in marketplace_marker_tokens if token in custom_text)
    if matching_tokens:
        marketplace_marker_hits.append((source_name, matching_tokens))

urban_life_names = {
    "JianghaiExpansion_UrbanFacades",
    "JianghaiExpansion_StreetLife",
    "JianghaiExpansion_Bicycle00",
    "JianghaiExpansion_Bicycle01",
    "JianghaiExpansion_Bicycle02",
    "JianghaiExpansion_MarketTeaCart",
    "JianghaiExpansion_MarketWickerBasket",
    "JianghaiExpansion_PawnshopTeaTable",
    "JianghaiExpansion_PawnshopStool00",
    "JianghaiExpansion_PawnshopStool01",
    "JianghaiExpansion_PawnshopStool02",
    "JianghaiExpansion_PawnshopBackdrop",
    "JianghaiExpansion_FactoryHandTruck",
    "JianghaiExpansion_WestClockLantern",
    "JianghaiCleared_MarketTeaTable",
    "JianghaiCleared_MarketStool00",
    "JianghaiCleared_MarketStool01",
    "JianghaiCleared_MarketStool02",
}
urban_life_ready = urban_life_names.issubset(bpy.data.objects.keys())
facade_expansion = [
    obj
    for obj in bpy.context.scene.objects
    if obj.name.startswith("JianghaiExpansion_Facade_")
]
facade_expansion_count = len(facade_expansion)
facade_expansion_aligned = all(
    (
        "_EastPhoto_" in obj.name
        and isclose(obj.location.x, 13.38, abs_tol=0.001)
        and isclose(obj.rotation_euler.z, pi * 0.5, abs_tol=0.001)
        and min((obj.matrix_world @ Vector(corner)).x for corner in obj.bound_box) >= 13.0
    )
    or (
        "_WestClock_" in obj.name
        and isclose(obj.location.x, -13.48, abs_tol=0.001)
        and isclose(obj.rotation_euler.z, -pi * 0.5, abs_tol=0.001)
        and max((obj.matrix_world @ Vector(corner)).x for corner in obj.bound_box) <= -13.0
    )
    for obj in facade_expansion
)
legacy_wood_house_nodes = sorted(
    obj.name
    for obj in bpy.data.objects
    if obj.name.startswith(("LingnanTimberShop", "ElevatedMarketShop"))
)
floating_market_signs_removed = not any(
    object_name in bpy.data.objects
    for object_name in (
        "OldCityMarketSignBacking",
        "OldCityMarketSignText",
        "OldCityMarketBuySignBacking",
        "OldCityMarketBuySignText",
        "OldCityMarketPawnSignBacking",
        "OldCityMarketPawnSignText",
    )
)
replacement_pawnshop = bpy.data.objects.get("JianghaiCleared_PawnshopStorefront")
pawnshop_root = bpy.data.objects.get("GuangchangPawnshop")
pawnshop_legacy_gate_names = {
    "GuangchangPawnshopSignBacking",
    "GuangchangPawnshopDangPlaqueBacking",
    "PawnshopGatePierL",
    "PawnshopGatePierR",
    "PawnshopGatePierCapL",
    "PawnshopGatePierCapR",
}
pawnshop_legacy_visible_names = pawnshop_legacy_gate_names.intersection(bpy.data.objects.keys())
pawnshop_legacy_visible_names.update(
    obj.name
    for obj in bpy.data.objects
    if obj.name.startswith(
        (
            "PawnshopSouthEast_",
            "PawnshopSouthEastCap_",
            "PawnshopSouthWest_",
            "PawnshopSouthWestCap_",
        )
    )
)
pawnshop_canopy_root = bpy.data.objects.get("PawnshopAuthoredPavilionGate")
pawnshop_canopy_parts = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("PawnshopAuthoredCanopy_")
    ),
    key=lambda obj: obj.name,
)
pawnshop_wings = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("PawnshopAuthoredWing_")
    ),
    key=lambda obj: obj.name,
)
pawnshop_canopy_triangles = sum(triangle_count(obj.data) for obj in pawnshop_canopy_parts)
pawnshop_wall_wings = [obj for obj in pawnshop_wings if obj.name.endswith("_Wall")]
pawnshop_insert_wings = [obj for obj in pawnshop_wings if obj.name.endswith("_Insert")]
pawnshop_columns_clear = all(
    not (-90.0 < obj.matrix_world.translation.x < -82.0)
    for obj in pawnshop_canopy_parts
    if str(obj.get("source_part_name", "")).startswith("檐柱")
)
pawnshop_frontage_ready = (
    not pawnshop_legacy_visible_names
    and pawnshop_root is not None
    and pawnshop_canopy_root is not None
    and pawnshop_canopy_root.parent == pawnshop_root
    and pawnshop_canopy_root.get("source_license") == "CC0 1.0 Universal"
    and pawnshop_canopy_root.get("source_creator") == "VVayToyek"
    and pawnshop_canopy_root.get("source_url")
    == "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free"
    and len(pawnshop_canopy_parts) == 15
    and pawnshop_canopy_triangles >= 15_000
    and all(obj.parent == pawnshop_canopy_root for obj in pawnshop_canopy_parts)
    and all(obj.type == "MESH" and len(obj.data.vertices) > 8 for obj in pawnshop_canopy_parts)
    and len(pawnshop_wall_wings) == 8
    and len(pawnshop_insert_wings) == 8
    and all(obj.parent == pawnshop_root for obj in pawnshop_wings)
    and all(
        obj.get("source_creator") == "James Ray Cock"
        and obj.get("source_url")
        == "https://polyhaven.com/a/modular_urban_apartments_facade"
        and obj.get("source_license") == "CC0 1.0 Universal"
        for obj in pawnshop_wings
    )
    and all(min(obj.dimensions.x, obj.dimensions.y) >= 0.17 for obj in pawnshop_wall_wings)
    and pawnshop_columns_clear
)
replacement_market_shops = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("JianghaiCleared_MarketShop")
    ),
    key=lambda obj: obj.name,
)
replacement_factory_buildings = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("JianghaiCleared_Factory")
    ),
    key=lambda obj: obj.name,
)


def is_cleared_authored_storefront(obj, expected_parent):
    return (
        obj is not None
        and obj.type == "MESH"
        and obj.data is not None
        and obj.data.get("jianghai_chinese_rebuild_version") == 1
        and obj.data.name not in RETIRED_VISIBLE_MESH_NAMES
        and obj.parent == expected_parent
        and len(obj.material_slots) > 0
        and obj.get("license") == "CC0 1.0 Universal"
        and obj.get("source_creator") in {"Free poly", "VVayToyek; Quaternius; Free poly"}
        and all(isfinite(value) for row in obj.matrix_world for value in row)
    )


replacement_storefronts_ready = (
    is_cleared_authored_storefront(replacement_pawnshop, bpy.data.objects.get("GuangchangPawnshop"))
    and len(replacement_market_shops) == 5
    and all(
        is_cleared_authored_storefront(shop, bpy.data.objects.get("OldCityMarketBridge"))
        for shop in replacement_market_shops
    )
    and not legacy_wood_house_nodes
    and not marketplace_marker_hits
    and floating_market_signs_removed
    and not any(obj.name.startswith(("MarketCanopy", "MarketAwning", "MarketLantern")) for obj in bpy.data.objects)
)
replacement_factory_ready = (
    len(replacement_factory_buildings) == 5
    and all(
        is_cleared_authored_storefront(building, bpy.data.objects.get("RedStarElectronicsFactory"))
        for building in replacement_factory_buildings
    )
    and not any(
        object_name in bpy.data.objects
        for object_name in ("RedStarFactoryMainBuilding", "RedStarLoadingBayWest", "RedStarLoadingBayEast")
    )
    and not any(obj.name.startswith("RedStarMainFacade_") for obj in bpy.data.objects)
)


def is_structural_building(obj):
    return (
        obj.type == "MESH"
        and obj.dimensions.z >= 4.5
        and min(obj.dimensions.x, obj.dimensions.y) >= 4.0
    )


def segment_hits_mesh(obj, start, end, depsgraph):
    inverse = obj.matrix_world.inverted_safe()
    local_start = inverse @ start
    local_end = inverse @ end
    local_ray = local_end - local_start
    if local_ray.length_squared <= 0.000001:
        return False
    tree = BVHTree.FromObject(obj, depsgraph)
    if tree is None:
        return False
    hit, _, _, _ = tree.ray_cast(local_start, local_ray.normalized(), local_ray.length)
    return hit is not None


density_buildings = sorted(
    (
        obj
        for obj in bpy.context.scene.objects
        if obj.name.startswith("JianghaiDensity_")
    ),
    key=lambda obj: obj.name,
)
density_spawn_clearances = tuple(Vector(point) for point in JIANGHAI_DEPLOYMENT_POINTS)
density_ready = (
    len(density_buildings) == 42
    and sum(obj.data.name == "JianghaiDensity_ChineseTempleHall_LOD" for obj in density_buildings) == 8
    and sum(obj.data.name == "JianghaiDensity_ChineseArcadeShop_LOD" for obj in density_buildings) == 16
    and sum(obj.data.name == "JianghaiDensity_ChineseGateHouse_LOD" for obj in density_buildings) == 4
    and sum(
        obj.data.name == "JianghaiDensity_QuaterniusBuilding1Large_LOD"
        for obj in density_buildings
    )
    == 4
    and sum(
        obj.data.name == "JianghaiDensity_QuaterniusBuilding3Big_LOD"
        for obj in density_buildings
    )
    == 3
    and sum(
        obj.data.name == "JianghaiDensity_QuaterniusBuilding4_LOD"
        for obj in density_buildings
    )
    == 3
    and sum(
        obj.data.name == "JianghaiDensity_QuaterniusHouse2_LOD"
        for obj in density_buildings
    )
    == 4
    and all(obj.parent == bpy.data.objects.get("JianghaiTenementDistrict") for obj in density_buildings)
    and all(obj.get("license") == "CC0 1.0 Universal" for obj in density_buildings)
    and all(
        obj.get("source_creator") in {"Free poly", "VVayToyek; Quaternius; Free poly", "Quaternius"}
        for obj in density_buildings
    )
    and all(obj.get("collision_role") == "building_shell" for obj in density_buildings)
    and all(obj.get("building_id") == obj.name for obj in density_buildings)
    and all(
        isclose(obj.scale.x, obj.scale.y, abs_tol=0.0001)
        and isclose(obj.scale.y, obj.scale.z, abs_tol=0.0001)
        for obj in density_buildings
    )
    and all(
        min((obj.matrix_world.translation - spawn).xy.length for spawn in density_spawn_clearances)
        >= 24.0
        for obj in density_buildings
    )
    and all(
        bpy.data.objects.get(f"JianghaiDensity_{side}Edge{edge:02d}") is not None
        and bpy.data.objects[f"JianghaiDensity_{side}Edge{edge:02d}"].get("jianghai_gameplay_proxy") is True
        for side in ("West", "East")
        for edge in range(4, 7)
    )
)
cross_street_intrusions_removed = not any(
    object_name in bpy.data.objects
    for object_name in (
        "WestTheatreRow01",
        "EastHardwareRow00",
        "OuterWestMidResidence",
        "OuterEastSquareResidence",
        "WestSouthRow01",
        "EastSouthRow01",
        "OuterEastSouthResidence",
    )
)
structural_anchors = {
    name: bpy.data.objects.get(name)
    for name in (
        "JianghaiTenementDistrict",
        "RedStarElectronicsFactory",
        "GuangchangPawnshop",
        "OldCityMarketBridge",
    )
}
collision_source_counts = {
    name: sum(is_structural_building(obj) for obj in anchor.children_recursive)
    if anchor is not None
    else 0
    for name, anchor in structural_anchors.items()
}
collision_sources = [
    obj
    for anchor in structural_anchors.values()
    if anchor is not None
    for obj in anchor.children_recursive
    if is_structural_building(obj)
]
factory_detail_collision_sources = [
    obj
    for obj in bpy.data.objects
    if obj.name.startswith(("FactoryGatePortal_", "FactoryEntryFacade_"))
]
pawnshop_detail_collision_sources = [
    obj
    for obj in bpy.data.objects
    if (
        obj.name.startswith("PawnshopAuthoredCanopy_")
        or (
            obj.name.startswith("PawnshopAuthoredWing_")
            and obj.name.endswith("_Wall")
        )
        or obj.name.startswith("PawnshopNorthWall_")
        or obj.name.startswith("PawnshopNorthWallCap_")
        or obj.name.startswith("PawnshopWestWall_")
        or obj.name.startswith("PawnshopWestWallCap_")
        or obj.name.startswith("PawnshopEastWall_")
        or obj.name.startswith("PawnshopEastWallCap_")
        or obj.name.startswith("PawnshopEntryFacade_")
    )
]
market_detail_collision_sources = [
    obj
    for obj in bpy.data.objects
    if (
        obj.name.startswith("MarketRail_")
        or obj.name.startswith("MarketRailPost_")
        or obj.name in {"MarketBridgeDeck", "MarketEastRamp", "MarketWestRamp"}
    )
]
detail_collision_source_counts = {
    "RedStarElectronicsFactory": len(factory_detail_collision_sources),
    "GuangchangPawnshop": len(pawnshop_detail_collision_sources),
    "OldCityMarketBridge": len(market_detail_collision_sources),
}
detail_collision_sources = (
    factory_detail_collision_sources
    + pawnshop_detail_collision_sources
    + market_detail_collision_sources
)
entry_facade_objects = {
    prefix: sorted(
        (obj for obj in bpy.data.objects if obj.name.startswith(prefix)),
        key=lambda obj: obj.name,
    )
    for prefix in ("PawnshopEntryFacade_", "FactoryEntryFacade_")
}
entry_facades_ready = all(
    len(objects) == 10
    and sum(obj.name.endswith("DoorFrame") for obj in objects) == 1
    and sum("_Wall_" in obj.name for obj in objects) == 9
    and all(
        obj.type == "MESH"
        and obj.get("source_creator") == "Quaternius"
        and obj.get("license") == "CC0 1.0 Universal"
        and obj.get("entry_motion") == "hinged"
        for obj in objects
    )
    for objects in entry_facade_objects.values()
)
density_intersections = []
for density in density_buildings:
    for other in collision_sources:
        if other == density or other.name.startswith("JianghaiDensity_"):
            continue
        overlap = overlap_depths(density, other)
        if overlap.x > 0.40 and overlap.y > 0.40 and overlap.z > 0.40:
            density_intersections.append(
                (density.name, other.name, tuple(round(value, 3) for value in overlap))
            )
for index, density in enumerate(density_buildings):
    for other in density_buildings[index + 1 :]:
        overlap = overlap_depths(density, other)
        if overlap.x > 0.40 and overlap.y > 0.40 and overlap.z > 0.40:
            density_intersections.append(
                (density.name, other.name, tuple(round(value, 3) for value in overlap))
            )
density_intersections_ready = not density_intersections

street_cadence_expectations = {
    "WestClockRow01": ("JianghaiStreetCadence_Building1Large", Vector((-12.20, -24.0, 0.03))),
    "WestMedicineRow01": ("JianghaiChineseArcadeShop_LOD", Vector((-18.50, 0.0, 0.03))),
    "WestMedicineRow02": ("JianghaiStreetCadence_Building4", Vector((-12.70, 12.0, 0.03))),
    "WestTheatreRow02": ("JianghaiStreetCadence_House2", Vector((-14.25, 48.0, 0.03))),
}
street_cadence_objects = {
    name: bpy.data.objects.get(name) for name in street_cadence_expectations
}
street_cadence_ready = all(
    obj is not None
    and obj.data.name == street_cadence_expectations[name][0]
    and (obj.location - street_cadence_expectations[name][1]).length <= 0.001
    and obj.get("license") == "CC0 1.0 Universal"
    and obj.get("collision_role") == "building_shell"
    and obj.get("building_id") == obj.name
    for name, obj in street_cadence_objects.items()
)
street_cadence_row = [
    bpy.data.objects.get(name)
    for name in (
        "WestClockRow01",
        "WestMedicineHouse",
        "WestMedicineRow01",
        "WestMedicineRow02",
        "WestTheatreHouse",
        "WestTheatreRow02",
    )
]
street_cadence_ready &= (
    all(obj is not None for obj in street_cadence_row)
    and len({obj.data.name for obj in street_cadence_row if obj is not None}) >= 3
    and not any(obj.data.name in RETIRED_VISIBLE_MESH_NAMES for obj in street_cadence_row if obj is not None)
)

market_walkway_names = {
    "JianghaiExpansion_MarketTeaCart",
    "JianghaiExpansion_MarketWickerBasket",
    "JianghaiCleared_MarketTeaTable",
    "JianghaiCleared_MarketStool00",
    "JianghaiCleared_MarketStool01",
    "JianghaiCleared_MarketStool02",
}
market_walkway_objects = [bpy.data.objects.get(name) for name in market_walkway_names]
market_walkway_meshes = [
    mesh
    for obj in market_walkway_objects
    if obj is not None
    for mesh in (obj, *obj.children_recursive)
    if mesh.type == "MESH"
]
market_walkway_ready = (
    all(obj is not None for obj in market_walkway_objects)
    and market_walkway_meshes
    and min(object_world_bounds(mesh)[0].y for mesh in market_walkway_meshes) >= 125.25
)
depsgraph = bpy.context.evaluated_depsgraph_get()
cross_street_clear = all(
    not any(segment_hits_mesh(obj, start, end, depsgraph) for obj in collision_sources)
    for start, end in (
        (Vector((-160.0, 21.6, 1.35)), Vector((160.0, 21.6, 1.35))),
        (Vector((-160.0, 98.4, 1.35)), Vector((160.0, 98.4, 1.35))),
    )
)
pawnshop_doorway_clear = (
    replacement_pawnshop is not None
    and replacement_pawnshop.get("doorway_cut_version") == 3
    and segment_hits_mesh(
        replacement_pawnshop,
        Vector((-86.0, 110.9, 1.35)),
        Vector((-86.0, 113.1, 1.35)),
        depsgraph,
    )
    is False
)
authored_collision_sources_ready = (
    len(collision_sources) == 112
    and collision_source_counts
    == {
        "JianghaiTenementDistrict": 100,
        "RedStarElectronicsFactory": 6,
        "GuangchangPawnshop": 1,
        "OldCityMarketBridge": 5,
    }
    and len(detail_collision_sources) == 133
    and detail_collision_source_counts
    == {
        "RedStarElectronicsFactory": 15,
        "GuangchangPawnshop": 81,
        "OldCityMarketBridge": 37,
    }
    and entry_facades_ready
    and cross_street_clear
    and pawnshop_doorway_clear
    and density_intersections_ready
    and street_cadence_ready
    and market_walkway_ready
)
root = bpy.data.objects.get("JianghaiOldCityAuthoredScene")
root_provenance_ready = (
    root is not None
    and "blenderkit_wood_house" not in root
    and "blenderkit_old_brick_factory" not in root
    and "poly_haven_apartments_evaluated_not_used" not in root
    and "poly_haven_apartments" in root
    and "cleared_storefront_pass" in root
    and "jianghai_valley_environment" in root
    and root.get("chinese_district_rebuild_version") == 1
    and root.get("retired_visible_asset_instances") == 0
)
valid = (
    not missing_anchors
    and valley_ready
    and all(terminal_checks)
    and terminal_orientation_ready
    and facade_props_ready
    and factory_duplicate_shutter_removed
    and factory_gate_portal_ready
    and not legacy_wood_house_nodes
    and not marketplace_marker_hits
    and evaluated_triangles <= SCENE_TRIANGLE_BUDGET
    and images_ready
    and not retired_visible_objects
    and not forbidden_export_objects
    and finite_transforms
    and urban_life_ready
    and facade_expansion_count == 36
    and facade_expansion_aligned
    and replacement_storefronts_ready
    and pawnshop_frontage_ready
    and replacement_factory_ready
    and density_ready
    and cross_street_intrusions_removed
    and authored_collision_sources_ready
    and root_provenance_ready
)
valley_ground_band_report = ",".join(
    f"{token}:{valley_ground_mesh.get(f'height_p10_p90_{token}', 0.0):.3f}/"
    f"{valley_ground_mesh.get(f'slope_p90_{token}', 0.0):.4f}/"
    f"{valley_ground_mesh.get(f'normal_z_std_{token}', 0.0):.5f}"
    for token in (
        "140_180", "180_220", "220_300", "300_400",
        "400_500", "500_560", "560_601",
    )
)
valley_screen_relief_report = ",".join(
    f"{metric['height_p10_p90']:.3f}/"
    f"{metric['normal_z_std']:.5f}/"
    f"{metric['samples']}"
    for metric in valley_screen_relief
)
print(
    f"JIANGHAI_PASS valid={valid} anchors={len(required_anchors) - len(missing_anchors)}/{len(required_anchors)} "
    f"valley={valley_ready} valley_foundation_triangles={valley_foundation_triangles} "
    f"valley_ground={len(valley_ground_scans)}:{sorted(set(valley_ground_triangle_counts))} "
    f"valley_ground_instance_triangles={valley_ground_instance_triangles} "
    f"valley_ground_components={valley_ground_topology['connected_components']} "
    f"valley_ground_boundary={valley_ground_topology['boundary_components']}:"
    f"{valley_ground_topology['boundary_edges']} "
    f"valley_ground_boundary_z=({valley_ground_topology['boundary_minimum_z']:.3f},"
    f"{valley_ground_topology['boundary_maximum_z']:.3f}) "
    f"valley_ground_degenerate={valley_ground_topology['degenerate_faces']} "
    f"valley_ground_invalid_normals={valley_ground_topology['invalid_face_normals']} "
    f"valley_ground_max_edge={valley_ground_topology['maximum_terrain_edge']:.3f} "
    f"valley_ground_max_edge_area_ratio="
    f"{valley_ground_topology['maximum_terrain_edge_area_ratio']:.3f} "
    f"valley_ground_repair={valley_ground_repair_ready} "
    f"valley_ground_envelope={valley_ground_envelope_ready} "
    f"valley_ground_composite_relief={valley_composite_top_relief:.3f} "
    f"valley_ground_height_residual=("
    f"{valley_ground_mesh.get('height_residual_rms', 0.0):.4f},"
    f"{valley_ground_mesh.get('height_residual_maximum', 0.0):.4f}) "
    f"valley_ground_asset_relief=(rms="
    f"{valley_ground_mesh.get('asset_height_residual_rms', 0.0):.4f},"
    f"inner={valley_ground_mesh.get('inner_band_height_p10_p90', 0.0):.3f},"
    f"outer={valley_ground_mesh.get('outer_band_height_p10_p90', 0.0):.3f}) "
    f"valley_ground_foundation_distance_relief=(0_60:"
    f"{valley_ground_mesh.get('foundation_near_0_60_height_p10_p90', 0.0):.3f},"
    f"60_160:{valley_ground_mesh.get('foundation_mid_60_160_height_p10_p90', 0.0):.3f}) "
    f"valley_ground_slope=(rms={valley_ground_mesh.get('surface_slope_rms', 0.0):.4f},"
    f"p90={valley_ground_mesh.get('surface_slope_p90', 0.0):.4f},"
    f"p99={valley_ground_mesh.get('surface_slope_p99', 0.0):.4f},"
    f"max={valley_ground_mesh.get('surface_slope_maximum', 0.0):.4f}) "
    f"valley_ground_normal=(std="
    f"{valley_ground_mesh.get('surface_normal_z_standard_deviation', 0.0):.5f},"
    f"p10={valley_ground_mesh.get('surface_normal_z_p10', 1.0):.5f}) "
    f"valley_ground_diagonals=("
    f"{valley_ground_mesh.get('top_diagonal_orientation_a', 0)},"
    f"{valley_ground_mesh.get('top_diagonal_orientation_b', 0)}) "
    f"valley_ground_bands={valley_ground_band_report} "
    f"valley_ground_uv={valley_ground_uv['layer_count']}:"
    f"{valley_ground_uv['loop_count']} "
    f"valley_ground_uv_error={valley_ground_uv['maximum_error']:.8f} "
    f"valley_ground_uv_jacobian=("
    f"{valley_ground_mesh.get('uv_normalized_jacobian_minimum', 0.0):.4f},"
    f"{valley_ground_mesh.get('uv_normalized_jacobian_maximum', 0.0):.4f}) "
    f"valley_ground_uv_ready={valley_ground_uv_ready} "
    f"valley_ground_world_uv_tile=("
    f"{min(valley_ground_world_uv_tiles):.3f},"
    f"{max(valley_ground_world_uv_tiles):.3f}) "
    f"valley_ground_world_uv_scale={valley_ground_world_uv_scale_ready} "
    f"valley_ground_surface={valley_ground_surface_ready} "
    f"valley_ground_base_color_factor="
    f"{tuple(round(value, 3) for value in valley_coast_surface_material.get('base_color_factor', ())) if valley_coast_surface_material else ()} "
    f"legacy_coast_materials={len(legacy_coast_materials)} "
    f"legacy_coast_images={len(legacy_coast_images)} "
    f"valley_ground_layout={valley_ground_layout_ready} "
    f"valley_mountains={len(valley_mountains)}:{sorted(set(valley_mountain_triangle_counts))} "
    f"valley_instance_triangles={valley_instance_triangles} "
    f"valley_extent={tuple(round(value, 1) for value in valley_extent)} "
    f"valley_ground_extent={tuple(round(value, 1) for value in valley_ground_extent)} "
    f"valley_ground_safe_top="
    f"{valley_ground_mesh.get('safe_inner_top_maximum', 0.0):.3f} "
    f"valley_ground_camera_clearance=("
    f"{','.join(f'{value:.3f}' for value in valley_camera_clearances)}) "
    f"valley_ground_coverage={valley_ground_coverage:.3f} "
    f"valley_foundation_edge={valley_ground_continuity['foundation_edge_hits']}/"
    f"{valley_ground_continuity['foundation_edge_samples']}:"
    f"gap={valley_ground_continuity['foundation_edge_maximum_gap']:.3f} "
    f"valley_ground_ring={valley_ground_continuity['ring_hits']}/"
    f"{valley_ground_continuity['ring_samples']}:height=("
    f"{valley_ground_continuity['ring_minimum_height']:.3f},"
    f"{valley_ground_continuity['ring_maximum_height']:.3f}) "
    f"valley_ground_top_surface={valley_ground_continuity['top_surface_hits']}/"
    f"{valley_ground_continuity['ring_samples']} "
    f"valley_ground_multi_top={valley_ground_continuity['multi_top_hits']} "
    f"valley_ground_near_double_top="
    f"{valley_ground_continuity['near_coplanar_double_top_hits']} "
    f"valley_ground_vertical_skirt_highest="
    f"{valley_ground_continuity['vertical_skirt_highest_hits']} "
    f"valley_ground_player_view_skirt="
    f"{valley_ground_continuity['player_view_visible_skirt_hits']}/"
    f"{valley_ground_continuity['player_view_samples']} "
    f"valley_ground_foundation_occluded_skirt="
    f"{valley_ground_continuity['player_view_foundation_occluded_skirt_hits']} "
    f"valley_ground_player_view_near_double_top="
    f"{valley_ground_continuity['player_view_near_coplanar_double_top_hits']} "
    f"valley_ground_screen_relief=({valley_screen_relief_report}) "
    f"valley_south_ground_ray={valley_south_ground_seam['south_ground_top_hits']}/"
    f"{valley_south_ground_seam['south_ground_samples']}:"
    f"side={valley_south_ground_seam['south_ground_distant_side_hits']}:"
    f"relief={valley_south_ground_seam['south_ground_height_p10_p90']:.3f} "
    f"valley_north_edge_endcaps={valley_north_edge_endcaps_ready}:"
    f"{valley_north_edge_boundary_top:.3f} "
    f"valley_north_edge_ray={valley_north_edge_seam['north_edge_hits']}/"
    f"{valley_north_edge_seam['north_edge_samples']}:"
    f"side={valley_north_edge_seam['north_edge_distant_side_hits']} "
    f"valley_mountains_outside={valley_mountains_outside} "
    f"valley_max_angular_gap={valley_max_angular_gap:.3f} "
    f"valley_max_inner_ring_radius={valley_maximum_inner_ring_radius:.1f} "
    f"valley_max_outer_ring_radius={valley_maximum_outer_ring_radius:.1f} "
    f"valley_mountain_layout={valley_mountain_layout_ready} "
    f"valley_mountain_ring_gaps=({valley_inner_ring_angular_gap:.3f},"
    f"{valley_outer_ring_angular_gap:.3f}) "
    f"valley_mountain_ring_angles={valley_mountain_ring_angles_ready} "
    f"valley_mountain_bottoms=({min(valley_mountain_bottoms):.1f},{max(valley_mountain_bottoms):.1f}) "
    f"valley_mountain_edge_top={max(valley_mountain_edge_tops):.1f} "
    f"valley_mountain_boundary_delta={valley_mountain_boundary_height_delta:.4f} "
    f"valley_mountain_burial_clearance={valley_mountain_burial_clearance:.3f} "
    f"valley_meshes={len(valley_ground_meshes)}+{len(valley_mountain_meshes)} "
    f"valley_metadata={valley_ground_source_ready and valley_mountain_source_ready and valley_foundation_materials_ready} "
    f"valley_legacy_data={len(legacy_valley_data)} "
    f"valley_hero_material={hero_material_ready} "
    f"valley_uniform_scales={valley_uniform_positive_scales} "
    f"valley_foundation_extent={tuple(round(value, 2) for value in valley_foundation_extent)} "
    f"valley_foundation_top={valley_foundation_bounds[1].z:.3f} "
    f"terminals={sum(terminal_checks)}/{len(terminal_checks)} terminal_orientation="
    f"{terminal_orientation_ready} facade_props={len(facade_props)}/22 facade_props_aligned="
    f"{facade_props_ready} factory_duplicate_shutter_removed={factory_duplicate_shutter_removed} "
    f"factory_gate_portal={sum(obj is not None for obj in factory_gate_objects)}/5 "
    f"factory_gate_portal_aligned={factory_gate_portal_ready} "
    f"legacy_wood_house_nodes={len(legacy_wood_house_nodes)} "
    f"marketplace_marker_hits={len(marketplace_marker_hits)} "
    f"floating_market_signs_removed={floating_market_signs_removed} "
    f"images_512_packed={images_ready} evaluated_triangles={evaluated_triangles}/{SCENE_TRIANGLE_BUDGET} "
    f"retired_visible={len(retired_visible_objects)} "
    f"forbidden_export_objects={len(forbidden_export_objects)} finite_transforms={finite_transforms} "
    f"urban_life={urban_life_ready} facade_expansion={facade_expansion_count}/36 "
    f"facade_expansion_aligned={facade_expansion_aligned} "
    f"replacement_storefronts_ready={replacement_storefronts_ready} "
    f"pawnshop_frontage_ready={pawnshop_frontage_ready} "
    f"pawnshop_canopy={len(pawnshop_canopy_parts)}/15 "
    f"pawnshop_canopy_triangles={pawnshop_canopy_triangles}/15000 "
    f"pawnshop_wings={len(pawnshop_wings)}/16 "
    f"pawnshop_legacy_visible={len(pawnshop_legacy_visible_names)} "
    f"pawnshop_columns_clear={pawnshop_columns_clear} "
    f"replacement_factory_ready={replacement_factory_ready} "
    f"density={len(density_buildings)}/42 density_ready={density_ready} "
    f"density_intersections={len(density_intersections)} "
    f"street_cadence={street_cadence_ready}:{len(street_cadence_objects)}/4 "
    f"market_walkway_clear={market_walkway_ready} "
    f"cross_street_intrusions_removed={cross_street_intrusions_removed} "
    f"cross_street_clear={cross_street_clear} pawnshop_doorway_clear={pawnshop_doorway_clear} "
    f"collision_sources={len(collision_sources)}/112 "
    f"collision_source_counts={','.join(f'{key}:{value}' for key, value in collision_source_counts.items())} "
    f"detail_collision_sources={len(detail_collision_sources)}/133 "
    f"detail_collision_source_counts={','.join(f'{key}:{value}' for key, value in detail_collision_source_counts.items())} "
    f"entry_facades={entry_facades_ready}:"
    f"{','.join(f'{key}:{len(value)}' for key, value in entry_facade_objects.items())} "
    f"authored_collision_sources_ready={authored_collision_sources_ready} "
    f"root_provenance_ready={root_provenance_ready}"
)
for source_name, tokens in marketplace_marker_hits:
    print(f"JIANGHAI_MARKETPLACE_MARKER source={source_name!r} tokens={','.join(tokens)}")
for first_name, second_name, overlap in density_intersections:
    print(
        f"JIANGHAI_DENSITY_INTERSECTION first={first_name!r} second={second_name!r} "
        f"overlap={overlap}"
    )
if not valid:
    sys.exit(2)
