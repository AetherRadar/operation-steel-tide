"""Audit Quaternius weapon reloadability and build the valid MP5A5 derivative.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup \
        --python scripts/blender/build_reloadable_quaternius_weapons.py

The MP5A5 derivative separates the complete authored external magazine by
welded source topology and preserves exact source triangle conservation. Its
welded static charging control is replaced with a hand-shaped Blender DCC
swept profile attached to a real action pivot. The remaining tracked sources
are audited here until their dedicated DCC mechanisms are completed.
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import closest_point_on_tri


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "assets" / "models" / "quaternius_ultimate_guns"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_reloadable_weapons"
OUTPUT_GLB = OUTPUT_DIR / "mp5a5_reloadable.glb"
OUTPUT_BLEND = (
    REPO_ROOT
    / "source_art"
    / "reloadable_weapons"
    / "mp5a5_reloadable.blend"
)
PREVIEW_PATH = OUTPUT_DIR / "mp5a5_reloadable_preview.png"

SOURCE_CREATOR = "Quaternius"
SOURCE_URL = "https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F"
SOURCE_LICENSE = "CC0-1.0"
SOURCE_ACQUISITION_DATE = "2026-08-20"
ADAPTATION_DATE = "2026-08-29"

BOUNDS_TOLERANCE = 0.0001
TARGET_LENGTH_METERS = 1.17
TARGET_STOCK_Z = 0.32
OPTIC_RAIL_Z = -0.25
OPTIC_RAIL_SAMPLE_HALF_LENGTH = 0.04
MAGAZINE_HOME = Vector((0.0, -0.20, -0.31))
SPARE_MAGAZINE_HOME = Vector((-0.30, -0.62, -0.18))
PRIMARY_GRIP_SOCKET = Vector((0.0, -0.09769, -0.10293))
SUPPORT_GRIP_SOCKET = Vector((-0.04143, 0.03383, -0.38662))
CHARGING_HANDLE_HOME = Vector((-0.052, 0.108, -0.648))
ACTION_SOCKET_LOCAL_AUDIT = Vector((-0.050, 0.017, -0.013))
ACTION_ENDPOINT_X_TOLERANCE = 0.000002
ACTION_SURFACE_DISTANCE_TOLERANCE = 0.000001
ACTION_SOCKET_ROUND_TRIP_TOLERANCE = 0.000002

MAGAZINE_TRIANGLE_COUNT = 60
BODY_TRIANGLE_COUNT = 1_314
SOURCE_TRIANGLE_COUNT = 1_374
ACTION_TRIANGLE_COUNT = 120
SCENE_TRIANGLE_COUNT = 1_554
SOURCE_MATERIALS = ("DarkMetal", "Metal", "Black", "Grey")
SOURCE_MATERIAL_TRIANGLES = {
    "DarkMetal": 692,
    "Metal": 372,
    "Black": 94,
    "Grey": 216,
}
MAGAZINE_MATERIAL_TRIANGLES = {
    "DarkMetal": 22,
    "Black": 38,
}
BODY_MATERIAL_TRIANGLES = {
    "DarkMetal": 670,
    "Metal": 372,
    "Black": 56,
    "Grey": 216,
}

ROOT_NAME = "SteelTideReloadableMP5A5"
SOCKET_NAMES = (
    "PrimaryGripSocket",
    "SupportGripSocket",
    "MagazineGripSocket",
    "MagazineWellSocket",
    "ChargingHandleSocket",
    "OpticRailSocket",
    "MuzzleSocket",
)

# Filled with reviewed deterministic outputs after the first complete build.
OUTPUT_GLB_SHA256 = "A21A1AF05BFDC91F307F7DD1EF431773A74B135B0CE5F8845354EC6029678101"
OUTPUT_GLB_BYTES = 82_708
OUTPUT_PREVIEW_SHA256 = "FD367AFB42BF7E91E3EBBD49006D99022C7484DE7B6AA928496509E78AF9E606"
OUTPUT_PREVIEW_BYTES = 1_427_943
EXPECTED_PRIMARY_MINIMUM = Vector((-0.102000, -0.267235, -0.850000))
EXPECTED_PRIMARY_MAXIMUM = Vector((0.046081, 0.267235, 0.320000))
EXPECTED_SCENE_MINIMUM = Vector((-0.321852, -0.687235, -0.850000))
EXPECTED_SCENE_MAXIMUM = Vector((0.046081, 0.267235, 0.320000))


@dataclass(frozen=True)
class SourceAudit:
    filename: str
    object_name: str
    sha256: str
    byte_count: int
    vertex_count: int
    triangle_count: int
    materials: tuple[str, ...]
    minimum: tuple[float, float, float]
    maximum: tuple[float, float, float]
    blocker: str | None


SOURCE_AUDITS = {
    "M24": SourceAudit(
        "m24.glb",
        "SniperRifle_1",
        "A780E291A22BABE8C3472AE9FD0C0F4B98F22382E25C7DA3F5507A5761DFFC5B",
        76_652,
        2_602,
        1_382,
        ("Black", "Green", "DarkMetal", "Glass", "Grey"),
        (-2.043425, -0.304352, -0.787850),
        (5.251208, 0.154958, 0.696123),
        "No authored external box magazine or complete removable internal magazine volume.",
    ),
    "MP5A5": SourceAudit(
        "mp5a5.glb",
        "SubmachineGun_2",
        "69DF22D1AA8603D66366D20C46755CCA2A19E1CABE8C1DB1B72EDB491AE48699",
        70_976,
        2_411,
        SOURCE_TRIANGLE_COUNT,
        SOURCE_MATERIALS,
        (-1.795080066, -0.159272641, -0.860740721),
        (2.248869181, 0.159272701, 0.986583292),
        None,
    ),
    "AXMC": SourceAudit(
        "axmc.glb",
        "SniperRifle_3",
        "7CDCE34DEC9A9B1AAE6C9E2EF554C88ECDC19554407DECC239B159E13D295F3F",
        95_356,
        3_296,
        1_722,
        ("Black", "DarkMetal", "Glass", "Green", "Grey"),
        (-1.933177, -0.304352, -0.383566),
        (5.311476, 0.141385, 0.817220),
        "The authored silhouette has no external magazine body below the receiver.",
    ),
    "AWM": SourceAudit(
        "awm.glb",
        "SniperRifle_5",
        "095E918BD89823B1CA726EAC0016D7C9DAEE15CC6F71010AB251FB0365819F02",
        95_200,
        3_254,
        1_688,
        ("LightMetal", "Metal", "DarkMetal", "Black", "Grey", "Glass"),
        (-2.357932, -0.627783, -0.912703),
        (3.977676, 0.620067, 0.986577),
        "Only the pistol grip, trigger group, and bipod exist below the receiver; no magazine body exists.",
    ),
    "VSS": SourceAudit(
        "vss.glb",
        "SniperRifle_4",
        "C69B8D4088176580819C20F44FC80D7742E6AD00BA1CA09CD064D8677B1C4BE5",
        74_692,
        2_530,
        1_344,
        ("Metal", "Black", "DarkMetal", "Glass", "Grey"),
        (-1.377546, -0.304352, -0.713719),
        (4.958062, 0.154958, 0.634547),
        "The authored generic rifle body contains no external VSS-style magazine geometry.",
    ),
    "P226": SourceAudit(
        "p226.glb",
        "Pistol_5",
        "4622AB2909AA0F4E88B74A13F52F9E28183A6FF5FCA5896FC7E98D44008F2148",
        53_776,
        1_840,
        968,
        ("Metal", "Black", "LightMetal"),
        (-0.366820, -0.137178, -0.476826),
        (1.452487, 0.137178, 0.721819),
        "The grip shell and base outline are authored, but no internal pistol magazine volume is modeled.",
    ),
    "M1911": SourceAudit(
        "m1911.glb",
        "Pistol_3",
        "6DC98CF2E44DC8CD052E402D72B5FE21AF70AFBFC48A70F3139E22008991FD47",
        79_648,
        2_756,
        1_442,
        ("Wood", "Metal", "Black", "LightMetal"),
        (-0.339644, -0.164360, -0.426102),
        (1.505308, 0.164361, 0.867026),
        "The authored grip panels and frame enclose an empty volume; no removable magazine mesh exists.",
    ),
}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def point_bounds(points: list[Vector]) -> tuple[Vector, Vector]:
    if not points:
        raise RuntimeError("Cannot calculate bounds for empty weapon geometry.")
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def require_bounds(
    label: str,
    actual: tuple[Vector, Vector],
    expected: tuple[Vector, Vector],
) -> None:
    for bound_name, actual_bound, expected_bound in zip(
        ("minimum", "maximum"),
        actual,
        expected,
    ):
        if (actual_bound - expected_bound).length > BOUNDS_TOLERANCE:
            raise RuntimeError(
                f"{label} {bound_name} drifted: "
                f"{tuple(actual_bound)} != {tuple(expected_bound)}"
            )


def source_world_bounds(source: bpy.types.Object) -> tuple[Vector, Vector]:
    return point_bounds(
        [source.matrix_world @ vertex.co for vertex in source.data.vertices]
    )


def import_and_validate_source(platform: str) -> bpy.types.Object:
    audit = SOURCE_AUDITS[platform]
    source_path = SOURCE_DIR / audit.filename
    if not source_path.is_file():
        raise RuntimeError(f"Missing tracked {platform} source: {source_path}")
    actual_bytes = source_path.stat().st_size
    actual_hash = sha256(source_path)
    if actual_bytes != audit.byte_count or actual_hash != audit.sha256:
        raise RuntimeError(
            f"{platform} source identity drifted: "
            f"bytes={actual_bytes} sha256={actual_hash}"
        )

    bpy.ops.import_scene.gltf(filepath=str(source_path))
    source = bpy.data.objects.get(audit.object_name)
    if source is None or source.type != "MESH":
        raise RuntimeError(f"Source object {audit.object_name!r} is unavailable.")
    source_meshes = [
        obj for obj in bpy.context.scene.objects if obj.type == "MESH"
    ]
    if source_meshes != [source]:
        raise RuntimeError(
            f"{platform} source unexpectedly contains {len(source_meshes)} meshes."
        )
    materials = tuple(material.name for material in source.data.materials)
    if materials != audit.materials:
        raise RuntimeError(
            f"Unexpected {platform} material layout: {materials}"
        )
    if (
        len(source.data.vertices) != audit.vertex_count
        or len(source.data.polygons) != audit.triangle_count
    ):
        raise RuntimeError(
            f"Unexpected {platform} topology: "
            f"vertices={len(source.data.vertices)} "
            f"triangles={len(source.data.polygons)}"
        )
    require_bounds(
        f"{platform} source",
        source_world_bounds(source),
        (Vector(audit.minimum), Vector(audit.maximum)),
    )
    return source


def audit_blocked_sources() -> None:
    for platform, audit in SOURCE_AUDITS.items():
        if audit.blocker is None:
            continue
        clear_scene()
        source = import_and_validate_source(platform)
        print(
            "RELOADABLE_WEAPON_BLOCKED "
            f"platform={platform} "
            f"source_triangles={len(source.data.polygons)} "
            f"reason={audit.blocker}"
        )


def world_face_center(
    obj: bpy.types.Object,
    polygon: bpy.types.MeshPolygon,
) -> Vector:
    center = Vector()
    for vertex_index in polygon.vertices:
        center += obj.matrix_world @ obj.data.vertices[vertex_index].co
    return center / len(polygon.vertices)


def welded_vertex_key(obj: bpy.types.Object, vertex_index: int) -> tuple[int, int, int]:
    point = obj.matrix_world @ obj.data.vertices[vertex_index].co
    return tuple(round(value * 100_000) for value in point)


def mp5a5_magazine_face_indices(obj: bpy.types.Object) -> set[int]:
    black_slot = next(
        index
        for index, material in enumerate(obj.data.materials)
        if material.name == "Black"
    )
    seeds = {
        polygon.index
        for polygon in obj.data.polygons
        if polygon.material_index == black_slot
        and 0.68 <= world_face_center(obj, polygon).x <= 1.02
        and abs(world_face_center(obj, polygon).y) <= 0.08
        and world_face_center(obj, polygon).z <= 0.20
    }
    if len(seeds) != 38:
        raise RuntimeError(
            f"MP5A5 magazine seed selection drifted: {len(seeds)} != 38"
        )

    key_faces: dict[tuple[int, int, int], set[int]] = {}
    face_keys: dict[int, tuple[tuple[int, int, int], ...]] = {}
    for polygon in obj.data.polygons:
        keys = tuple(welded_vertex_key(obj, index) for index in polygon.vertices)
        face_keys[polygon.index] = keys
        for key in keys:
            key_faces.setdefault(key, set()).add(polygon.index)

    selected = set(seeds)
    pending = list(sorted(seeds, reverse=True))
    while pending:
        face_index = pending.pop()
        for key in face_keys[face_index]:
            for neighbor in sorted(key_faces[key], reverse=True):
                if neighbor not in selected:
                    selected.add(neighbor)
                    pending.append(neighbor)
    return selected


def material_triangle_counts(obj: bpy.types.Object) -> dict[str, int]:
    counts: dict[str, int] = {}
    for polygon in obj.data.polygons:
        material_name = obj.data.materials[polygon.material_index].name
        counts[material_name] = counts.get(material_name, 0) + 1
    return counts


def separate_magazine(
    source: bpy.types.Object,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    if material_triangle_counts(source) != SOURCE_MATERIAL_TRIANGLES:
        raise RuntimeError("MP5A5 source material triangle partition drifted.")

    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")

    magazine_faces = mp5a5_magazine_face_indices(source)
    for polygon in source.data.polygons:
        polygon.select = polygon.index in magazine_faces
    selected_counts: dict[str, int] = {}
    for polygon in source.data.polygons:
        if not polygon.select:
            continue
        material_name = source.data.materials[polygon.material_index].name
        selected_counts[material_name] = selected_counts.get(material_name, 0) + 1
    if selected_counts != MAGAZINE_MATERIAL_TRIANGLES:
        raise RuntimeError(
            "MP5A5 magazine material partition drifted: "
            f"{selected_counts} != {MAGAZINE_MATERIAL_TRIANGLES}"
        )

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    separated = [
        obj
        for obj in bpy.context.selected_objects
        if obj.type == "MESH" and obj != source
    ]
    if len(separated) != 1:
        raise RuntimeError(
            f"Expected one separated MP5A5 magazine, found {len(separated)}"
        )
    magazine = separated[0]
    if (
        len(source.data.polygons) != BODY_TRIANGLE_COUNT
        or len(magazine.data.polygons) != MAGAZINE_TRIANGLE_COUNT
    ):
        raise RuntimeError(
            "Separated MP5A5 topology mismatch: "
            f"body={len(source.data.polygons)} "
            f"magazine={len(magazine.data.polygons)}"
        )
    if material_triangle_counts(source) != BODY_MATERIAL_TRIANGLES:
        raise RuntimeError("MP5A5 body material triangle partition drifted.")
    if material_triangle_counts(magazine) != MAGAZINE_MATERIAL_TRIANGLES:
        raise RuntimeError("MP5A5 separated magazine materials drifted.")
    source.name = "WeaponBodyGeometry"
    source.data.name = "WeaponBodyMesh"
    magazine.name = "MagazineGeometry"
    magazine.data.name = "MagazineMesh"
    return source, magazine


def canonical_transform(
    source_minimum: Vector,
    source_maximum: Vector,
) -> Matrix:
    source_size = source_maximum - source_minimum
    if source_size.x <= 0.001:
        raise RuntimeError(f"MP5A5 source length is invalid: {source_size.x}")
    source_center = (source_minimum + source_maximum) * 0.5
    scale = TARGET_LENGTH_METERS / source_size.x
    target_center_z = TARGET_STOCK_Z - TARGET_LENGTH_METERS * 0.5

    # Blender source coordinates are X=barrel, Y=thickness, Z=up. The glTF
    # exporter maps Blender (X,Y,Z) to Godot (X,Z,-Y), so this baked transform
    # yields X=lateral, Y=up, Z=stock-to-muzzle in root-local metres.
    return Matrix(
        (
            (0.0, -scale, 0.0, scale * source_center.y),
            (
                scale,
                0.0,
                0.0,
                -scale * source_center.x - target_center_z,
            ),
            (0.0, 0.0, scale, -scale * source_center.z),
            (0.0, 0.0, 0.0, 1.0),
        )
    )


def bake_object_transform(obj: bpy.types.Object, transform: Matrix) -> None:
    obj.data.transform(transform @ obj.matrix_world)
    obj.data.update()
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)


def godot_to_blender(position: Vector) -> Vector:
    return Vector((position.x, -position.z, position.y))


def blender_to_godot(position: Vector) -> Vector:
    return Vector((position.x, position.z, -position.y))


def configure_mp5a5_materials() -> None:
    physics = {
        "DarkMetal": (0.76, 0.28, "phosphated_receiver_and_action"),
        "Metal": (0.68, 0.31, "finished_barrel_and_hardware"),
        "Black": (0.08, 0.50, "polymer_grip_stock_and_magazine_insert"),
        "Grey": (0.48, 0.34, "painted_rail_and_controls"),
    }
    for name, (metallic, roughness, role) in physics.items():
        material = bpy.data.materials.get(name)
        if material is None:
            raise RuntimeError(f"MP5A5 material {name!r} is unavailable.")
        material.metallic = metallic
        material.roughness = roughness
        material["surface_role"] = role
        material["scalar_pbr_only"] = True
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is None:
            raise RuntimeError(f"MP5A5 material {name!r} lacks Principled BSDF.")
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness


def create_mp5a5_charging_handle() -> bpy.types.Object:
    """Author a swept MP5 tubular charging handle; no primitives or CSG."""
    material = bpy.data.materials.new("ActionSteel")
    material.diffuse_color = (0.025, 0.030, 0.034, 1.0)
    material.metallic = 0.84
    material.roughness = 0.24
    material["surface_role"] = "blued_tubular_charging_handle"
    material["dcc_authored"] = True
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = material.diffuse_color
    principled.inputs["Metallic"].default_value = material.metallic
    principled.inputs["Roughness"].default_value = material.roughness

    # Five hand-shaped rings form the receiver stem, neck, and enlarged
    # cylindrical knob. Ring centres bend forward like the real MP5 control.
    sections = (
        (Vector((0.012, 0.000, 0.004)), 0.008, 0.010),
        (Vector((0.004, 0.000, 0.002)), 0.010, 0.012),
        (Vector((-0.008, 0.000, -0.003)), 0.008, 0.009),
        (Vector((-0.032, 0.002, -0.010)), 0.015, 0.018),
        (Vector((-0.050, 0.004, -0.013)), 0.013, 0.016),
    )
    side_count = 12
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int]] = []
    for offset, radius_y, radius_z in sections:
        center = CHARGING_HANDLE_HOME + offset
        for side in range(side_count):
            angle = math.tau * side / side_count
            point = center + Vector(
                (0.0, math.cos(angle) * radius_y, math.sin(angle) * radius_z)
            )
            vertices.append(tuple(godot_to_blender(point)))
    for section in range(len(sections) - 1):
        first = section * side_count
        second = (section + 1) * side_count
        for side in range(side_count):
            following = (side + 1) % side_count
            a, b = first + side, first + following
            c, d = second + side, second + following
            faces.extend(((a, c, b), (b, c, d)))
    first_center = len(vertices)
    vertices.append(tuple(godot_to_blender(CHARGING_HANDLE_HOME + sections[0][0])))
    last_center = len(vertices)
    vertices.append(tuple(godot_to_blender(CHARGING_HANDLE_HOME + sections[-1][0])))
    last_ring = (len(sections) - 1) * side_count
    for side in range(side_count):
        following = (side + 1) % side_count
        faces.append((first_center, following, side))
        faces.append((last_center, last_ring + side, last_ring + following))

    mesh = bpy.data.meshes.new("ChargingHandleMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    geometry = bpy.data.objects.new("ChargingHandleGeometry", mesh)
    geometry["runtime_asset"] = True
    geometry["mechanism_role"] = "empty_reload_action_geometry"
    geometry["dcc_method"] = "hand-shaped five-ring swept profile; weighted normals"
    bpy.context.collection.objects.link(geometry)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    weighted = geometry.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    bpy.context.view_layer.objects.active = geometry
    geometry.select_set(True)
    bpy.ops.object.modifier_apply(modifier=weighted.name)
    geometry.select_set(False)
    if len(mesh.polygons) != ACTION_TRIANGLE_COUNT:
        raise RuntimeError(
            f"MP5A5 DCC action topology drifted: {len(mesh.polygons)} "
            f"!= {ACTION_TRIANGLE_COUNT}"
        )
    return geometry


def new_empty(
    name: str,
    parent: bpy.types.Object,
    godot_position: Vector,
    role: str,
) -> bpy.types.Object:
    node = bpy.data.objects.new(name, None)
    node.empty_display_type = "PLAIN_AXES"
    node.empty_display_size = 0.035
    node.location = godot_to_blender(godot_position)
    node["runtime_asset"] = True
    node["socket_role"] = role
    bpy.context.collection.objects.link(node)
    node.parent = parent
    return node


def mesh_godot_points(obj: bpy.types.Object) -> list[Vector]:
    return [
        blender_to_godot(obj.matrix_world @ vertex.co)
        for vertex in obj.data.vertices
    ]


def mesh_godot_points_below(
    ancestor: bpy.types.Object,
    obj: bpy.types.Object,
) -> list[Vector]:
    inverse = ancestor.matrix_world.inverted()
    return [
        blender_to_godot(inverse @ obj.matrix_world @ vertex.co)
        for vertex in obj.data.vertices
    ]


def point_to_mesh_surface_distance(
    ancestor: bpy.types.Object,
    obj: bpy.types.Object,
    point: Vector,
) -> float:
    obj.data.calc_loop_triangles()
    object_to_ancestor = ancestor.matrix_world.inverted() @ obj.matrix_world
    point_blender = godot_to_blender(point)
    minimum_distance = math.inf
    for triangle in obj.data.loop_triangles:
        first, second, third = (
            object_to_ancestor @ obj.data.vertices[index].co
            for index in triangle.vertices
        )
        closest = closest_point_on_tri(
            point_blender,
            first,
            second,
            third,
        )
        minimum_distance = min(
            minimum_distance,
            (point_blender - closest).length,
        )
    if not math.isfinite(minimum_distance):
        raise RuntimeError("MP5A5 charging handle has no surface triangles.")
    return minimum_distance


def audit_action_socket(
    geometry: bpy.types.Object,
    action: bpy.types.Object,
    socket_local: Vector,
) -> float:
    points = mesh_godot_points_below(action, geometry)
    minimum, _ = point_bounds(points)
    terminal_points = [
        point
        for point in points
        if point.x <= minimum.x + ACTION_ENDPOINT_X_TOLERANCE
    ]
    if not terminal_points:
        raise RuntimeError("MP5A5 charging handle has no terminal surface region.")
    terminal_maximum_y = max(point.y for point in terminal_points)
    if (
        abs(socket_local.x - minimum.x) > ACTION_ENDPOINT_X_TOLERANCE
        or terminal_maximum_y - socket_local.y > ACTION_ENDPOINT_X_TOLERANCE
        or socket_local.length < 0.045
    ):
        raise RuntimeError(
            "MP5A5 action socket left the outer handle terminal: "
            f"socket={tuple(socket_local)} terminal_x={minimum.x:.9f} "
            f"terminal_y={terminal_maximum_y:.9f}"
        )
    if (
        socket_local - ACTION_SOCKET_LOCAL_AUDIT
    ).length > ACTION_SOCKET_ROUND_TRIP_TOLERANCE:
        raise RuntimeError(
            "MP5A5 action socket identity drifted: "
            f"{tuple(socket_local)} != {tuple(ACTION_SOCKET_LOCAL_AUDIT)}"
        )
    surface_distance = point_to_mesh_surface_distance(
        action,
        geometry,
        socket_local,
    )
    if surface_distance > ACTION_SURFACE_DISTANCE_TOLERANCE:
        raise RuntimeError(
            f"MP5A5 action socket is {surface_distance:.9f} m "
            "from the visible terminal surface."
        )
    return surface_distance


def derive_action_socket(
    geometry: bpy.types.Object,
    action: bpy.types.Object,
) -> Vector:
    points = mesh_godot_points_below(action, geometry)
    terminal_x = min(point.x for point in points)
    terminal_points = [
        point
        for point in points
        if point.x <= terminal_x + ACTION_ENDPOINT_X_TOLERANCE
    ]
    socket_local = max(
        terminal_points,
        key=lambda point: (point.y, point.z, -point.x),
    )
    audit_action_socket(geometry, action, socket_local)
    return socket_local


def derive_socket_positions(
    body: bpy.types.Object,
    magazine_geometry: bpy.types.Object,
) -> dict[str, Vector]:
    body_points = mesh_godot_points(body)
    magazine_points = mesh_godot_points(magazine_geometry)

    rail_points = [
        point
        for point in body_points
        if abs(point.z - OPTIC_RAIL_Z) <= OPTIC_RAIL_SAMPLE_HALF_LENGTH
    ]
    if not rail_points:
        raise RuntimeError("Unable to sample the MP5A5 optic rail surface.")
    rail_height = max(point.y for point in rail_points)
    if not 0.235 <= rail_height <= 0.250:
        raise RuntimeError(
            f"MP5A5 optic rail height is implausible: {rail_height:.6f}"
        )

    magazine_minimum, magazine_maximum = point_bounds(magazine_points)
    magazine_top = [
        point
        for point in magazine_points
        if point.y >= magazine_maximum.y - 0.018
    ]
    well_z = (
        min(point.z for point in magazine_top)
        + max(point.z for point in magazine_top)
    ) * 0.5

    muzzle_z = min(point.z for point in body_points)
    muzzle_points = [
        point for point in body_points if point.z <= muzzle_z + 0.018
    ]
    muzzle_x = (
        min(point.x for point in muzzle_points)
        + max(point.x for point in muzzle_points)
    ) * 0.5
    muzzle_y = (
        min(point.y for point in muzzle_points)
        + max(point.y for point in muzzle_points)
    ) * 0.5

    # The static source welds this control into the receiver. The DCC-authored
    # replacement begins flush with the real left-front receiver contact zone.
    if not any(
        abs(point.z - CHARGING_HANDLE_HOME.z) <= 0.07
        and abs(point.y - CHARGING_HANDLE_HOME.y) <= 0.09
        for point in body_points
    ):
        raise RuntimeError("MP5A5 charging-handle contact left the receiver surface.")

    return {
        "PrimaryGripSocket": PRIMARY_GRIP_SOCKET,
        "SupportGripSocket": SUPPORT_GRIP_SOCKET,
        "MagazineGripSocket": Vector(
            (
                magazine_minimum.x,
                (magazine_minimum.y + magazine_maximum.y) * 0.5,
                (magazine_minimum.z + magazine_maximum.z) * 0.5,
            )
        ),
        "MagazineWellSocket": Vector((0.0, magazine_maximum.y, well_z)),
        "ChargingHandleSocket": CHARGING_HANDLE_HOME,
        "OpticRailSocket": Vector((0.0, rail_height, OPTIC_RAIL_Z)),
        "MuzzleSocket": Vector((muzzle_x, muzzle_y, muzzle_z)),
    }


def build_runtime_asset() -> tuple[bpy.types.Object, dict[str, Vector]]:
    clear_scene()
    source = import_and_validate_source("MP5A5")
    source_minimum, source_maximum = source_world_bounds(source)
    body, magazine_geometry = separate_magazine(source)
    transform = canonical_transform(source_minimum, source_maximum)
    for obj in (body, magazine_geometry):
        bake_object_transform(obj, transform)
    configure_mp5a5_materials()

    root = bpy.data.objects.new(ROOT_NAME, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    root["runtime_asset"] = True
    root["weapon_platform"] = "MP5A5"
    root["source_creator"] = SOURCE_CREATOR
    root["source_url"] = SOURCE_URL
    root["source_license"] = SOURCE_LICENSE
    root["source_sha256"] = SOURCE_AUDITS["MP5A5"].sha256
    root["source_acquisition_date"] = SOURCE_ACQUISITION_DATE
    root["adaptation_date"] = ADAPTATION_DATE
    root["visible_action_geometry"] = True
    root["action_geometry_source"] = (
        "Steel Tide Blender DCC adaptation of the real MP5 tubular control"
    )
    root["action_geometry_method"] = (
        "hand-shaped swept five-ring profile; no primitive or CSG"
    )
    root["blocked_source_platforms"] = "M24,AXMC,AWM,VSS,P226,M1911"
    bpy.context.collection.objects.link(root)
    body.parent = root

    magazine = new_empty(
        "Magazine",
        root,
        MAGAZINE_HOME,
        "primary_magazine_pivot",
    )
    magazine_geometry.parent = magazine
    magazine_geometry.location = -magazine.location
    magazine_geometry["runtime_asset"] = True
    magazine_geometry["mechanism_role"] = "detachable_magazine"

    spare_magazine = new_empty(
        "SpareMagazine",
        root,
        SPARE_MAGAZINE_HOME,
        "spare_magazine_pivot",
    )
    spare_geometry = magazine_geometry.copy()
    spare_geometry.data = magazine_geometry.data.copy()
    spare_geometry.name = "SpareMagazineGeometry"
    spare_geometry.data.name = "SpareMagazineMesh"
    spare_geometry["runtime_asset"] = True
    spare_geometry["mechanism_role"] = "spare_detachable_magazine"
    bpy.context.collection.objects.link(spare_geometry)
    spare_geometry.parent = spare_magazine
    spare_geometry.location = -magazine.location
    bpy.context.view_layer.update()

    sockets = derive_socket_positions(body, magazine_geometry)
    charging_handle = new_empty(
        "ChargingHandle",
        root,
        CHARGING_HANDLE_HOME,
        "action_pivot",
    )
    charging_geometry = create_mp5a5_charging_handle()
    charging_geometry.parent = charging_handle
    charging_geometry.location = -charging_handle.location
    bpy.context.view_layer.update()
    action_socket_local = derive_action_socket(
        charging_geometry,
        charging_handle,
    )
    sockets["ChargingHandleSocket"] = (
        CHARGING_HANDLE_HOME + action_socket_local
    )
    root["action_socket_source"] = "visible outer handle terminal surface"
    root["action_socket_local_godot_m"] = tuple(action_socket_local)
    for name in SOCKET_NAMES:
        if name == "ChargingHandleSocket":
            new_empty(name, charging_handle, action_socket_local, name)
            continue
        socket = new_empty(name, root, sockets[name], name)
        if name == "MagazineGripSocket":
            socket.parent = magazine
            socket.location = godot_to_blender(sockets[name] - MAGAZINE_HOME)
    root["optic_rail_socket_y"] = sockets["OpticRailSocket"].y
    root["optic_rail_socket_z"] = sockets["OpticRailSocket"].z

    keep = {root, *root.children_recursive}
    for obj in list(bpy.context.scene.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)

    counts = (
        len(body.data.polygons),
        len(magazine_geometry.data.polygons),
        len(spare_geometry.data.polygons),
        len(charging_geometry.data.polygons),
    )
    if counts[0] + counts[1] != SOURCE_TRIANGLE_COUNT:
        raise RuntimeError("MP5A5 source topology was not conserved.")
    if sum(counts) != SCENE_TRIANGLE_COUNT:
        raise RuntimeError("MP5A5 scene triangle count is invalid.")
    if magazine_geometry.data is spare_geometry.data:
        raise RuntimeError("MP5A5 magazines are not independent meshes.")
    return root, sockets


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (root, *root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_yup=True,
        export_attributes=True,
        export_extras=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
    )
    actual_hash = sha256(OUTPUT_GLB)
    actual_bytes = OUTPUT_GLB.stat().st_size
    if OUTPUT_GLB_SHA256 and (
        actual_hash != OUTPUT_GLB_SHA256 or actual_bytes != OUTPUT_GLB_BYTES
    ):
        raise RuntimeError(
            "Deterministic MP5A5 GLB output drifted: "
            f"sha256={actual_hash} bytes={actual_bytes}"
        )


def save_editable_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


def render_preview(root: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.show_specular_highlight = True
    scene.display.render_aa = "FXAA"

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    target = Vector((0.0, 0.27, 0.02))
    camera.location = Vector((1.70, -2.55, 1.25))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.75
    scene.camera = camera
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for stamp_property in (
        "use_stamp_camera",
        "use_stamp_date",
        "use_stamp_filename",
        "use_stamp_frame",
        "use_stamp_frame_range",
        "use_stamp_hostname",
        "use_stamp_lens",
        "use_stamp_marker",
        "use_stamp_memory",
        "use_stamp_note",
        "use_stamp_render_time",
        "use_stamp_scene",
        "use_stamp_sequencer_strip",
        "use_stamp_time",
    ):
        setattr(scene.render, stamp_property, False)
    scene.render.filepath = str(PREVIEW_PATH)
    scene.world.color = (0.045, 0.055, 0.065)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)

    actual_hash = sha256(PREVIEW_PATH)
    actual_bytes = PREVIEW_PATH.stat().st_size
    if OUTPUT_PREVIEW_SHA256 and (
        actual_hash != OUTPUT_PREVIEW_SHA256
        or actual_bytes != OUTPUT_PREVIEW_BYTES
    ):
        raise RuntimeError(
            "Deterministic MP5A5 preview output drifted: "
            f"sha256={actual_hash} bytes={actual_bytes}"
        )
    if root.name not in bpy.context.scene.objects:
        raise RuntimeError("MP5A5 preview lost the runtime root.")


def require_unique_node(name: str) -> bpy.types.Object:
    matches = [obj for obj in bpy.context.scene.objects if obj.name == name]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one exported node {name!r}, found {len(matches)}")
    return matches[0]


def godot_mesh_bounds(
    root: bpy.types.Object,
    meshes: tuple[bpy.types.Object, ...],
) -> tuple[Vector, Vector]:
    root_inverse = root.matrix_world.inverted()
    return point_bounds(
        [
            blender_to_godot(root_inverse @ obj.matrix_world @ vertex.co)
            for obj in meshes
            for vertex in obj.data.vertices
        ]
    )


def validate_exported_asset(
    expected_sockets: dict[str, Vector],
) -> tuple[tuple[Vector, Vector], tuple[Vector, Vector], float]:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    root = require_unique_node(ROOT_NAME)
    body = require_unique_node("WeaponBodyGeometry")
    magazine = require_unique_node("Magazine")
    spare_magazine = require_unique_node("SpareMagazine")
    magazine_geometry = require_unique_node("MagazineGeometry")
    spare_geometry = require_unique_node("SpareMagazineGeometry")
    charging_handle = require_unique_node("ChargingHandle")
    charging_geometry = require_unique_node("ChargingHandleGeometry")

    expected_nodes = {
        ROOT_NAME,
        "WeaponBodyGeometry",
        "Magazine",
        "MagazineGeometry",
        "SpareMagazine",
        "SpareMagazineGeometry",
        "ChargingHandle",
        "ChargingHandleGeometry",
        *SOCKET_NAMES,
    }
    actual_nodes = {obj.name for obj in bpy.context.scene.objects}
    if actual_nodes != expected_nodes:
        raise RuntimeError(
            "Exported MP5A5 node contract drifted: "
            f"{sorted(actual_nodes)} != {sorted(expected_nodes)}"
        )
    if body.type != "MESH" or magazine_geometry.type != "MESH" or spare_geometry.type != "MESH":
        raise RuntimeError("Exported MP5A5 mechanism nodes are missing geometry.")
    if body.parent != root or magazine.parent != root or spare_magazine.parent != root:
        raise RuntimeError("Exported MP5A5 root hierarchy is invalid.")
    if magazine_geometry.parent != magazine or spare_geometry.parent != spare_magazine:
        raise RuntimeError("Exported MP5A5 magazine hierarchy is invalid.")
    if charging_handle.parent != root or charging_geometry.parent != charging_handle:
        raise RuntimeError("Exported MP5A5 action hierarchy is invalid.")
    if magazine_geometry.data == spare_geometry.data:
        raise RuntimeError("Exported MP5A5 magazines do not use independent mesh data.")

    counts = (
        len(body.data.polygons),
        len(magazine_geometry.data.polygons),
        len(spare_geometry.data.polygons),
        len(charging_geometry.data.polygons),
    )
    if counts != (
        BODY_TRIANGLE_COUNT,
        MAGAZINE_TRIANGLE_COUNT,
        MAGAZINE_TRIANGLE_COUNT,
        ACTION_TRIANGLE_COUNT,
    ):
        raise RuntimeError(f"Exported MP5A5 topology drifted: {counts}")
    if counts[0] + counts[1] != SOURCE_TRIANGLE_COUNT or sum(counts) != SCENE_TRIANGLE_COUNT:
        raise RuntimeError("Exported MP5A5 triangle conservation failed.")
    if material_triangle_counts(body) != BODY_MATERIAL_TRIANGLES:
        raise RuntimeError("Exported MP5A5 body material partition drifted.")
    for geometry in (magazine_geometry, spare_geometry):
        if material_triangle_counts(geometry) != MAGAZINE_MATERIAL_TRIANGLES:
            raise RuntimeError(
                f"Exported MP5A5 material partition drifted on {geometry.name}."
            )

    primary_origin = blender_to_godot(
        root.matrix_world.inverted() @ magazine_geometry.matrix_world.translation
    )
    spare_origin = blender_to_godot(
        root.matrix_world.inverted() @ spare_geometry.matrix_world.translation
    )
    expected_spare_origin = SPARE_MAGAZINE_HOME - MAGAZINE_HOME
    if primary_origin.length > 0.0001:
        raise RuntimeError(
            f"Exported MP5A5 primary magazine left its well: {tuple(primary_origin)}"
        )
    if (spare_origin - expected_spare_origin).length > 0.0001:
        raise RuntimeError(
            "Exported MP5A5 spare magazine staging drifted: "
            f"{tuple(spare_origin)} != {tuple(expected_spare_origin)}"
        )

    if not root.get("visible_action_geometry", False):
        raise RuntimeError("MP5A5 does not disclose visible action geometry.")
    if len(charging_geometry.data.polygons) != ACTION_TRIANGLE_COUNT:
        raise RuntimeError("MP5A5 exported action geometry is empty or drifted.")
    if root.get("action_socket_source") != "visible outer handle terminal surface":
        raise RuntimeError("MP5A5 does not disclose its terminal action socket.")

    for name in SOCKET_NAMES:
        socket = require_unique_node(name)
        if name == "MagazineGripSocket":
            expected_parent = magazine
        elif name == "ChargingHandleSocket":
            expected_parent = charging_handle
        else:
            expected_parent = root
        if socket.parent != expected_parent:
            raise RuntimeError(
                f"MP5A5 socket {name} has invalid parent {socket.parent}"
            )
        actual = blender_to_godot(
            root.matrix_world.inverted() @ socket.matrix_world.translation
        )
        if (actual - expected_sockets[name]).length > 0.0001:
            raise RuntimeError(
                f"MP5A5 socket {name} drifted: "
                f"{tuple(actual)} != {tuple(expected_sockets[name])}"
            )

    rail = expected_sockets["OpticRailSocket"]
    if abs(rail.z - OPTIC_RAIL_Z) > 0.0001 or not 0.235 <= rail.y <= 0.250:
        raise RuntimeError(f"MP5A5 rail socket is invalid: {tuple(rail)}")

    action_origin = blender_to_godot(
        root.matrix_world.inverted() @ charging_handle.matrix_world.translation
    )
    if (action_origin - CHARGING_HANDLE_HOME).length > BOUNDS_TOLERANCE:
        raise RuntimeError(
            "MP5A5 shared action pivot drifted: "
            f"{tuple(action_origin)} != {tuple(CHARGING_HANDLE_HOME)}"
        )
    charging_socket = require_unique_node("ChargingHandleSocket")
    action_socket_local = blender_to_godot(
        charging_handle.matrix_world.inverted()
        @ charging_socket.matrix_world.translation
    )
    action_surface_distance = audit_action_socket(
        charging_geometry,
        charging_handle,
        action_socket_local,
    )

    primary_bounds = godot_mesh_bounds(
        root, (body, magazine_geometry, charging_geometry)
    )
    scene_bounds = godot_mesh_bounds(
        root, (body, magazine_geometry, spare_geometry, charging_geometry)
    )
    if EXPECTED_PRIMARY_MINIMUM is not None and EXPECTED_PRIMARY_MAXIMUM is not None:
        require_bounds(
            "MP5A5 body plus primary magazine",
            primary_bounds,
            (EXPECTED_PRIMARY_MINIMUM, EXPECTED_PRIMARY_MAXIMUM),
        )
    if EXPECTED_SCENE_MINIMUM is not None and EXPECTED_SCENE_MAXIMUM is not None:
        require_bounds(
            "MP5A5 full scene",
            scene_bounds,
            (EXPECTED_SCENE_MINIMUM, EXPECTED_SCENE_MAXIMUM),
        )
    return primary_bounds, scene_bounds, action_surface_distance


def format_vector(vector: Vector) -> str:
    return "(" + ",".join(f"{value:.6f}" for value in vector) + ")"


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    audit_blocked_sources()
    root, sockets = build_runtime_asset()
    export_asset(root)
    save_editable_source()
    render_preview(root)
    primary_bounds, scene_bounds, action_surface_distance = (
        validate_exported_asset(sockets)
    )

    print(
        "RELOADABLE_MP5A5_EXPORT "
        f"source_sha256={SOURCE_AUDITS['MP5A5'].sha256} "
        f"glb_sha256={sha256(OUTPUT_GLB)} "
        f"blend_sha256={sha256(OUTPUT_BLEND)} "
        f"preview_sha256={sha256(PREVIEW_PATH)} "
        f"source_triangles={SOURCE_TRIANGLE_COUNT} "
        f"body_triangles={BODY_TRIANGLE_COUNT} "
        f"magazine_triangles={MAGAZINE_TRIANGLE_COUNT} "
        f"spare_triangles={MAGAZINE_TRIANGLE_COUNT} "
        f"action_triangles={ACTION_TRIANGLE_COUNT} "
        f"scene_triangles={SCENE_TRIANGLE_COUNT} "
        f"charging_socket={format_vector(sockets['ChargingHandleSocket'])} "
        f"charging_socket_local={format_vector(ACTION_SOCKET_LOCAL_AUDIT)} "
        f"action_surface_distance_m={action_surface_distance:.9f} "
        f"rail_socket={format_vector(sockets['OpticRailSocket'])} "
        f"primary_bounds={format_vector(primary_bounds[0])}.."
        f"{format_vector(primary_bounds[1])} "
        f"scene_bounds={format_vector(scene_bounds[0])}.."
        f"{format_vector(scene_bounds[1])} "
        f"glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"blend_bytes={OUTPUT_BLEND.stat().st_size} "
        f"preview_bytes={PREVIEW_PATH.stat().st_size} "
        "blocked=M24,AXMC,AWM,VSS,P226,M1911"
    )


if __name__ == "__main__":
    main()
