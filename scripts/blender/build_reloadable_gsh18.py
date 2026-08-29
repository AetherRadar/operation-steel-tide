"""Build the reloadable GSh-18 presentation asset from TastyTony's source.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup \
        --python scripts/blender/build_reloadable_gsh18.py

The complete authored pistol topology is preserved.  The source's independent
outer slide and two slide-only detail meshes are grouped under a real moving
action pivot.  The source has no internal magazine volume, so the detachable
18-round magazine is a Blender DCC-authored rounded loft with separate feed
lips, follower, and floor plate; no runtime primitive or CSG is used.
"""

from __future__ import annotations

import hashlib
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE = (
    REPO_ROOT
    / "assets"
    / "models"
    / "tastytony_gsh18"
    / "low-poly_gsh-18.glb"
)
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_reloadable_weapons"
OUTPUT_GLB = OUTPUT_DIR / "gsh18_reloadable.glb"
OUTPUT_PREVIEW = OUTPUT_DIR / "gsh18_reloadable_preview.png"
OUTPUT_BLEND = (
    REPO_ROOT
    / "source_art"
    / "reloadable_weapons"
    / "gsh18_reloadable.blend"
)

SOURCE_SHA256 = "56E8CB31AE1CE1DEA689A3D890A95DAC7E1D30334C809CBB2D9E43038CBBC6B9"
SOURCE_BYTES = 916_616
SOURCE_CREATOR = "TastyTony"
SOURCE_LICENSE = "CC-BY-4.0"
SOURCE_ACQUISITION_DATE = "2026-08-20"
ADAPTATION_DATE = "2026-08-29"

ROOT_NAME = "SteelTideReloadableGSh18"
EXCLUDED_SOURCE_MESHES = {"Cube", "Icosphere"}
TARGET_LENGTH_METERS = 0.43
TARGET_STOCK_Z = 0.32
MAGAZINE_HOME = Vector((0.0, -0.20, -0.31))
SPARE_MAGAZINE_HOME = Vector((-0.30, -0.62, -0.18))
ACTION_HOME = Vector((0.075, 0.085, -0.05))
ACTION_TRAVEL = Vector((0.0, 0.0, 0.085))
PRIMARY_GRIP_SOCKET = Vector((0.0, -0.04006, 0.22579))
SUPPORT_GRIP_SOCKET = Vector((-0.09510, -0.04929, 0.22227))

SOURCE_MESH_AUDIT = {
    "Object_21": (2_223, 936, ("Material.004",)),
    "Object_22": (236, 88, ("Material.007",)),
    "Object_23": (835, 288, ("Material.003",)),
    "Object_24": (2_765, 936, ("Material.002",)),
    "Object_25": (5_567, 1_944, ("Material.001",)),
    "Object_26": (563, 266, ("Material.005",)),
    "Object_27": (900, 364, ("Material.008",)),
    "Object_28": (214, 92, ("Material.006",)),
    "Object_29": (3_657, 1_387, ("Material",)),
    "Object_30": (162, 60, ("Material.009",)),
}
SOURCE_TRIANGLE_COUNT = sum(item[1] for item in SOURCE_MESH_AUDIT.values())
MAIN_SLIDE_COMPONENT_TRIANGLES = (404, 246, 226)
FIXED_SLIDE_COMPONENT_TRIANGLES = (40, 20)
MAIN_SLIDE_TRIANGLE_COUNT = sum(MAIN_SLIDE_COMPONENT_TRIANGLES)
ACTION_SOURCE_NAMES = {"Object_21", "Object_28", "Object_30"}
ACTION_TRIANGLE_COUNT = (
    MAIN_SLIDE_TRIANGLE_COUNT
    + SOURCE_MESH_AUDIT["Object_28"][1]
    + SOURCE_MESH_AUDIT["Object_30"][1]
)
BODY_TRIANGLE_COUNT = SOURCE_TRIANGLE_COUNT - ACTION_TRIANGLE_COUNT

OUTPUT_GLTF_SHA256: str | None = (
    "887DD398F720393074335D31A210F4770A02AF4F4740FF5E5FD322E89FB2B405"
)
OUTPUT_GLTF_BYTES: int | None = 499_464
OUTPUT_PREVIEW_SHA256: str | None = (
    "7BED8C18CE7019A8AD564A9C85F40F0AE7AF8015BE346015752DF8BEC67F1371"
)
OUTPUT_PREVIEW_BYTES: int | None = 1_444_201
EXPECTED_PRIMARY_MINIMUM: Vector | None = Vector((-0.041350, -0.157354, -0.110000))
EXPECTED_PRIMARY_MAXIMUM: Vector | None = Vector((0.041350, 0.157354, 0.320000))
EXPECTED_SCENE_MINIMUM: Vector | None = Vector((-0.326490, -0.571000, -0.110000))
EXPECTED_SCENE_MAXIMUM: Vector | None = Vector((0.041350, 0.157354, 0.422944))

BOUNDS_TOLERANCE = 0.0001
POSITION_TOLERANCE = 0.0001
SOCKET_SURFACE_TOLERANCE = 0.000002
LEFT_SURFACE_ZONE_FRACTION = 0.10
ACTION_SOCKET_TARGET_REAR_FRACTION = 0.80
ACTION_SOCKET_MIN_REAR_FRACTION = 0.70

FIXED_OUTPUT_NAMES = {
    "Object_22": "WeaponBodyControlGeometry",
    "Object_23": "GripTextureDetailA",
    "Object_24": "GripTextureDetailB",
    "Object_25": "GripTextureDetailC",
    "Object_26": "TriggerAndFrameDetailGeometry",
    "Object_27": "FrameAndBarrelDetailGeometry",
    "Object_29": "WeaponBodyGeometry",
}
ACTION_OUTPUT_NAMES = {
    "Object_28": "SlideInsertGeometry",
    "Object_30": "SlideTopGeometry",
}
FIXED_NODE_NAMES = (*FIXED_OUTPUT_NAMES.values(), "SlideLowerControlGeometry")
ACTION_NODE_NAMES = ("ChargingHandleGeometry", *ACTION_OUTPUT_NAMES.values())
SOCKET_NAMES = (
    "PrimaryGripSocket",
    "SupportGripSocket",
    "MagazineGripSocket",
    "MagazineWellSocket",
    "ChargingHandleSocket",
    "OpticRailSocket",
    "MuzzleSocket",
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


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


def point_bounds(points: list[Vector]) -> tuple[Vector, Vector]:
    if not points:
        raise RuntimeError("Cannot calculate bounds for empty geometry.")
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def require_bounds(
    label: str,
    actual: tuple[Vector, Vector],
    expected: tuple[Vector, Vector],
) -> None:
    for bound_name, actual_bound, expected_bound in zip(
        ("minimum", "maximum"), actual, expected
    ):
        if (actual_bound - expected_bound).length > BOUNDS_TOLERANCE:
            raise RuntimeError(
                f"{label} {bound_name} drifted: "
                f"{tuple(actual_bound)} != {tuple(expected_bound)}"
            )


def import_and_validate_source() -> list[bpy.types.Object]:
    if not SOURCE.is_file():
        raise FileNotFoundError(SOURCE)
    if SOURCE.stat().st_size != SOURCE_BYTES or sha256(SOURCE) != SOURCE_SHA256:
        raise RuntimeError(
            "TastyTony GSh-18 source identity drifted: "
            f"bytes={SOURCE.stat().st_size} sha256={sha256(SOURCE)}"
        )

    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    source_meshes = {
        obj.name: obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.name not in EXCLUDED_SOURCE_MESHES
    }
    if set(source_meshes) != set(SOURCE_MESH_AUDIT):
        raise RuntimeError(
            "Unexpected GSh-18 source mesh layout: "
            f"{sorted(source_meshes)}"
        )
    for name, (vertices, triangles, materials) in SOURCE_MESH_AUDIT.items():
        obj = source_meshes[name]
        actual_materials = tuple(material.name for material in obj.data.materials)
        if (
            len(obj.data.vertices) != vertices
            or len(obj.data.polygons) != triangles
            or actual_materials != materials
        ):
            raise RuntimeError(
                f"GSh-18 source topology drifted on {name}: "
                f"vertices={len(obj.data.vertices)} "
                f"triangles={len(obj.data.polygons)} "
                f"materials={actual_materials}"
            )
        if any(len(polygon.vertices) != 3 for polygon in obj.data.polygons):
            raise RuntimeError(f"GSh-18 source {name} is no longer triangulated.")
    return [source_meshes[name] for name in sorted(source_meshes)]


def evaluated_mesh_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    mesh.transform(source.matrix_world)
    mesh.transform(Matrix.Scale(0.001, 4))
    result.matrix_world = Matrix.Identity(4)
    result.visible_shadow = False
    result["runtime_asset"] = True
    result["source_object"] = source.name
    result["source_topology_preserved"] = True
    return result


def welded_face_components(obj: bpy.types.Object) -> list[set[int]]:
    key_faces: dict[tuple[int, int, int], set[int]] = {}
    face_keys: dict[int, tuple[tuple[int, int, int], ...]] = {}
    for polygon in obj.data.polygons:
        keys = tuple(
            tuple(round(value * 1_000_000) for value in obj.data.vertices[index].co)
            for index in polygon.vertices
        )
        face_keys[polygon.index] = keys
        for key in keys:
            key_faces.setdefault(key, set()).add(polygon.index)

    unseen = set(face_keys)
    components: list[set[int]] = []
    while unseen:
        seed = min(unseen)
        unseen.remove(seed)
        component = {seed}
        pending = [seed]
        while pending:
            face_index = pending.pop()
            for key in face_keys[face_index]:
                for neighbor in key_faces[key]:
                    if neighbor in unseen:
                        unseen.remove(neighbor)
                        component.add(neighbor)
                        pending.append(neighbor)
        components.append(component)
    return sorted(components, key=lambda item: (-len(item), min(item)))


def separate_authored_slide(
    combined: bpy.types.Object,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    components = welded_face_components(combined)
    component_sizes = tuple(len(component) for component in components)
    expected_sizes = (*MAIN_SLIDE_COMPONENT_TRIANGLES, *FIXED_SLIDE_COMPONENT_TRIANGLES)
    if component_sizes != expected_sizes:
        raise RuntimeError(
            "GSh-18 Object_21 connected components drifted: "
            f"{component_sizes} != {expected_sizes}"
        )
    moving_faces = set().union(*components[: len(MAIN_SLIDE_COMPONENT_TRIANGLES)])

    bpy.ops.object.select_all(action="DESELECT")
    combined.select_set(True)
    bpy.context.view_layer.objects.active = combined
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in combined.data.polygons:
        polygon.select = polygon.index in moving_faces
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    separated = [
        obj
        for obj in bpy.context.selected_objects
        if obj.type == "MESH" and obj != combined
    ]
    if len(separated) != 1:
        raise RuntimeError(
            f"Expected one separated GSh-18 slide, found {len(separated)}"
        )
    moving = separated[0]
    if (
        len(moving.data.polygons) != MAIN_SLIDE_TRIANGLE_COUNT
        or len(combined.data.polygons) != sum(FIXED_SLIDE_COMPONENT_TRIANGLES)
    ):
        raise RuntimeError(
            "GSh-18 slide topology split failed: "
            f"moving={len(moving.data.polygons)} fixed={len(combined.data.polygons)}"
        )
    moving["source_object"] = "Object_21"
    moving["source_connected_components"] = "404,246,226 triangles"
    moving["source_topology_preserved"] = True
    combined["source_connected_components"] = "40,20 triangles"
    return moving, combined


def canonical_transform(
    source_minimum: Vector,
    source_maximum: Vector,
) -> Matrix:
    source_size = source_maximum - source_minimum
    if source_size.x <= 0.001:
        raise RuntimeError(f"GSh-18 source length is invalid: {source_size.x}")
    source_center = (source_minimum + source_maximum) * 0.5
    scale = TARGET_LENGTH_METERS / source_size.x
    target_center_z = TARGET_STOCK_Z - TARGET_LENGTH_METERS * 0.5

    # Source axes are X=muzzle, Y=thickness, Z=up.  The glTF exporter maps
    # Blender (X,Y,Z) to Godot (X,Z,-Y), yielding root-local metres with
    # X=lateral, Y=up, Z=stock-to-muzzle and identity runtime transforms.
    return Matrix(
        (
            (0.0, -scale, 0.0, scale * source_center.y),
            (scale, 0.0, 0.0, -scale * source_center.x - target_center_z),
            (0.0, 0.0, scale, -scale * source_center.z),
            (0.0, 0.0, 0.0, 1.0),
        )
    )


def godot_to_blender(position: Vector) -> Vector:
    return Vector((position.x, -position.z, position.y))


def blender_to_godot(position: Vector) -> Vector:
    return Vector((position.x, position.z, -position.y))


def mesh_godot_points(obj: bpy.types.Object) -> list[Vector]:
    return [
        blender_to_godot(obj.matrix_world @ vertex.co)
        for vertex in obj.data.vertices
    ]


def mesh_surface_contact_godot(
    objects: tuple[bpy.types.Object, ...],
    target: Vector,
) -> tuple[Vector, Vector, str, int, float]:
    bpy.context.view_layer.update()
    vertices: list[Vector] = []
    polygons: list[tuple[int, ...]] = []
    face_owners: list[str] = []
    for obj in objects:
        offset = len(vertices)
        vertices.extend(mesh_godot_points(obj))
        polygons.extend(
            tuple(offset + vertex_index for vertex_index in polygon.vertices)
            for polygon in obj.data.polygons
        )
        face_owners.extend([obj.name] * len(obj.data.polygons))
    surface = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)
    location, normal, face_index, distance = surface.find_nearest(target)
    if (
        location is None
        or normal is None
        or face_index is None
        or distance is None
    ):
        raise RuntimeError("Unable to derive a GSh-18 mesh-surface contact.")
    return location, normal, face_owners[face_index], face_index, distance


def left_surface_fraction(
    point: Vector,
    bounds: tuple[Vector, Vector],
) -> float:
    width = bounds[1].x - bounds[0].x
    if width <= 0.000001:
        raise RuntimeError("GSh-18 surface has no lateral width.")
    return (point.x - bounds[0].x) / width


def rear_fraction(
    point: Vector,
    bounds: tuple[Vector, Vector],
) -> float:
    length = bounds[1].z - bounds[0].z
    if length <= 0.000001:
        raise RuntimeError("GSh-18 action has no longitudinal length.")
    return (point.z - bounds[0].z) / length


def new_empty(
    name: str,
    parent: bpy.types.Object,
    godot_position: Vector,
    role: str,
) -> bpy.types.Object:
    node = bpy.data.objects.new(name, None)
    node.empty_display_type = "PLAIN_AXES"
    node.empty_display_size = 0.025
    node.location = godot_to_blender(godot_position)
    node["runtime_asset"] = True
    node["socket_role"] = role
    bpy.context.collection.objects.link(node)
    node.parent = parent
    return node


def add_ring(
    vertices: list[tuple[float, float, float]],
    center_y: float,
    center_z: float,
    half_x: float,
    half_z: float,
    corner: float,
) -> list[int]:
    profile = (
        (-half_x + corner, -half_z),
        (half_x - corner, -half_z),
        (half_x, -half_z + corner),
        (half_x, half_z - corner),
        (half_x - corner, half_z),
        (-half_x + corner, half_z),
        (-half_x, half_z - corner),
        (-half_x, -half_z + corner),
    )
    indices = []
    for x, z_offset in profile:
        indices.append(len(vertices))
        vertices.append(tuple(godot_to_blender(Vector((x, center_y, center_z + z_offset)))))
    return indices


def connect_rings(
    first: list[int],
    second: list[int],
    faces: list[tuple[int, int, int]],
    materials: list[int],
    material_index: int,
) -> None:
    for index in range(len(first)):
        following = (index + 1) % len(first)
        a, b = first[index], first[following]
        c, d = second[index], second[following]
        faces.extend(((a, c, b), (b, c, d)))
        materials.extend((material_index, material_index))


def cap_ring(
    ring: list[int],
    center: Vector,
    reverse: bool,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, int, int]],
    materials: list[int],
    material_index: int,
) -> None:
    center_index = len(vertices)
    vertices.append(tuple(godot_to_blender(center)))
    for index in range(len(ring)):
        following = (index + 1) % len(ring)
        face = (center_index, ring[following], ring[index])
        if reverse:
            face = tuple(reversed(face))
        faces.append(face)
        materials.append(material_index)


def add_wedge(
    corners_bottom: tuple[Vector, Vector, Vector, Vector],
    corners_top: tuple[Vector, Vector, Vector, Vector],
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, int, int]],
    materials: list[int],
    material_index: int,
) -> None:
    start = len(vertices)
    vertices.extend(
        tuple(godot_to_blender(point))
        for point in (*corners_bottom, *corners_top)
    )
    quads = (
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    )
    for a, b, c, d in quads:
        faces.extend(
            ((start + a, start + b, start + c), (start + a, start + c, start + d))
        )
        materials.extend((material_index, material_index))


def magazine_materials() -> tuple[bpy.types.Material, ...]:
    steel = bpy.data.materials.new("GSh18MagazineSteel")
    steel.diffuse_color = (0.035, 0.042, 0.048, 1.0)
    steel.metallic = 0.82
    steel.roughness = 0.28
    steel["surface_role"] = "blued_steel_double_stack_magazine"
    steel["dcc_authored"] = True

    floor = bpy.data.materials.new("GSh18MagazineFloorPlate")
    floor.diffuse_color = (0.028, 0.032, 0.034, 1.0)
    floor.metallic = 0.10
    floor.roughness = 0.52
    floor["surface_role"] = "impact_resistant_floor_plate"
    floor["dcc_authored"] = True

    follower = bpy.data.materials.new("GSh18MagazineFollower")
    follower.diffuse_color = (0.62, 0.18, 0.035, 1.0)
    follower.metallic = 0.04
    follower.roughness = 0.48
    follower["surface_role"] = "high_visibility_polymer_follower"
    follower["dcc_authored"] = True

    for material in (steel, floor, follower):
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is None:
            raise RuntimeError(f"Material {material.name} lacks Principled BSDF.")
        principled.inputs["Base Color"].default_value = material.diffuse_color
        principled.inputs["Metallic"].default_value = material.metallic
        principled.inputs["Roughness"].default_value = material.roughness
    return steel, floor, follower


def create_magazine_geometry(name: str) -> bpy.types.Object:
    """Create a hand-shaped GSh-18 double-stack magazine in Godot metres."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int]] = []
    face_materials: list[int] = []

    # Six rounded sections follow the real grip rake; the body narrows toward
    # its feed tower and expands slightly at the serviceable floor plate.
    sections = (
        (0.018, 0.202, 0.0230, 0.0270, 0.0050),
        (0.006, 0.207, 0.0240, 0.0280, 0.0050),
        (-0.018, 0.216, 0.0245, 0.0280, 0.0050),
        (-0.072, 0.237, 0.0240, 0.0260, 0.0050),
        (-0.126, 0.258, 0.0230, 0.0240, 0.0050),
        (-0.145, 0.265, 0.0230, 0.0230, 0.0050),
    )
    rings = [
        add_ring(vertices, y, z, half_x, half_z, corner)
        for y, z, half_x, half_z, corner in sections
    ]
    for first, second in zip(rings, rings[1:]):
        connect_rings(first, second, faces, face_materials, 0)
    cap_ring(
        rings[0],
        Vector((0.0, sections[0][0], sections[0][1])),
        False,
        vertices,
        faces,
        face_materials,
        0,
    )
    cap_ring(
        rings[-1],
        Vector((0.0, sections[-1][0], sections[-1][1])),
        True,
        vertices,
        faces,
        face_materials,
        0,
    )

    # Two asymmetrical feed lips keep the top recognisable during the reload.
    for side in (-1.0, 1.0):
        inner = side * 0.008
        outer = side * 0.022
        add_wedge(
            (
                Vector((inner, 0.016, 0.181)),
                Vector((outer, 0.016, 0.183)),
                Vector((outer, 0.016, 0.218)),
                Vector((inner, 0.016, 0.216)),
            ),
            (
                Vector((inner, 0.029, 0.184)),
                Vector((outer, 0.027, 0.186)),
                Vector((outer, 0.025, 0.217)),
                Vector((inner, 0.027, 0.214)),
            ),
            vertices,
            faces,
            face_materials,
            0,
        )

    # The follower is a sloped insert visible between the lips.
    add_wedge(
        (
            Vector((-0.0075, 0.019, 0.188)),
            Vector((0.0075, 0.019, 0.188)),
            Vector((0.0075, 0.019, 0.211)),
            Vector((-0.0075, 0.019, 0.211)),
        ),
        (
            Vector((-0.0075, 0.026, 0.190)),
            Vector((0.0075, 0.026, 0.190)),
            Vector((0.0075, 0.024, 0.209)),
            Vector((-0.0075, 0.024, 0.209)),
        ),
        vertices,
        faces,
        face_materials,
        2,
    )

    # A wider rounded plate closes the base without a runtime box primitive.
    lower_plate = add_ring(vertices, -0.151, 0.267, 0.0265, 0.0260, 0.0060)
    upper_plate = add_ring(vertices, -0.145, 0.265, 0.0260, 0.0250, 0.0060)
    connect_rings(lower_plate, upper_plate, faces, face_materials, 1)
    cap_ring(
        lower_plate,
        Vector((0.0, -0.151, 0.267)),
        True,
        vertices,
        faces,
        face_materials,
        1,
    )

    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.clear()
    for material in magazine_materials():
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, face_materials):
        polygon.material_index = material_index
        polygon.use_smooth = True
    mesh.validate(clean_customdata=False)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj["runtime_asset"] = True
    obj["mechanism_role"] = "detachable_18_round_magazine"
    obj["dcc_method"] = (
        "hand-shaped six-section rounded loft with feed lips, follower, and floor plate"
    )
    obj["uses_runtime_primitive"] = False
    obj["uses_csg"] = False

    bevel = obj.modifiers.new("MagazineEdgeBevel", "BEVEL")
    bevel.width = 0.00065
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(24.0)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    triangulate = obj.modifiers.new("Triangulate", "TRIANGULATE")
    triangulate.quad_method = "FIXED"
    triangulate.ngon_method = "BEAUTY"
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    weighted = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    bpy.ops.object.modifier_apply(modifier=weighted.name)
    obj.select_set(False)
    if not obj.data.polygons or any(
        len(polygon.vertices) != 3 for polygon in obj.data.polygons
    ):
        raise RuntimeError("GSh-18 DCC magazine did not finish as triangles.")
    return obj


def source_copy_bounds(copies: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    return point_bounds(
        [obj.matrix_world @ vertex.co for obj in copies for vertex in obj.data.vertices]
    )


def parent_preserving_world(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    if child.matrix_world != Matrix.Identity(4):
        raise RuntimeError(
            f"{child.name} must be baked to identity before mechanism parenting."
        )
    child.parent = parent
    child.matrix_parent_inverse = Matrix.Identity(4)
    # Every mechanism pivot is translation-only.  Using the authored local
    # translation directly avoids dependency-graph lag immediately after a new
    # empty is linked and keeps the visible mesh exactly at its baked surface.
    child.matrix_basis = Matrix.Translation(-parent.location)


def derive_socket_positions(
    body_objects: tuple[bpy.types.Object, ...],
    action_objects: tuple[bpy.types.Object, ...],
    magazine_geometry: bpy.types.Object,
) -> dict[str, Vector]:
    body_points = [point for obj in body_objects for point in mesh_godot_points(obj)]
    action_points = [point for obj in action_objects for point in mesh_godot_points(obj)]
    all_points = [*body_points, *action_points]
    magazine_points = mesh_godot_points(magazine_geometry)
    magazine_minimum, magazine_maximum = point_bounds(magazine_points)
    magazine_size = magazine_maximum - magazine_minimum
    magazine_target = Vector(
        (
            magazine_minimum.x - magazine_size.x * 4.0,
            (magazine_minimum.y + magazine_maximum.y) * 0.5,
            (magazine_minimum.z + magazine_maximum.z) * 0.5,
        )
    )
    magazine_contact, _, magazine_owner, _, _ = mesh_surface_contact_godot(
        (magazine_geometry,),
        magazine_target,
    )
    magazine_bounds = (magazine_minimum, magazine_maximum)
    magazine_side_fraction = left_surface_fraction(
        magazine_contact,
        magazine_bounds,
    )
    if (
        magazine_owner != magazine_geometry.name
        or magazine_side_fraction > LEFT_SURFACE_ZONE_FRACTION
    ):
        raise RuntimeError(
            "GSh-18 magazine contact left the real left side surface: "
            f"owner={magazine_owner} side_fraction={magazine_side_fraction:.6f}"
        )

    action_minimum, action_maximum = point_bounds(action_points)
    action_size = action_maximum - action_minimum
    action_target = Vector(
        (
            action_minimum.x - action_size.x * 4.0,
            (action_minimum.y + action_maximum.y) * 0.5,
            action_minimum.z
            + action_size.z * ACTION_SOCKET_TARGET_REAR_FRACTION,
        )
    )
    action_contact, _, action_owner, _, _ = mesh_surface_contact_godot(
        action_objects,
        action_target,
    )
    action_bounds = (action_minimum, action_maximum)
    action_side_fraction = left_surface_fraction(action_contact, action_bounds)
    action_rear_fraction = rear_fraction(action_contact, action_bounds)
    if (
        action_owner != "ChargingHandleGeometry"
        or action_side_fraction > LEFT_SURFACE_ZONE_FRACTION
        or action_rear_fraction < ACTION_SOCKET_MIN_REAR_FRACTION
    ):
        raise RuntimeError(
            "GSh-18 action contact left the real rear slide surface: "
            f"owner={action_owner} side_fraction={action_side_fraction:.6f} "
            f"rear_fraction={action_rear_fraction:.6f}"
        )

    rear_slide_points = [point for point in action_points if point.z >= 0.08]
    if not rear_slide_points:
        raise RuntimeError("Unable to sample the GSh-18 slide top.")
    optic_y = max(point.y for point in rear_slide_points)
    optic_z = sum(point.z for point in rear_slide_points if point.y >= optic_y - 0.004)
    optic_samples = [point for point in rear_slide_points if point.y >= optic_y - 0.004]
    if not optic_samples:
        raise RuntimeError("Unable to sample the GSh-18 rear sight plane.")
    optic_z /= len(optic_samples)

    muzzle_z = min(point.z for point in all_points)
    muzzle_points = [point for point in all_points if point.z <= muzzle_z + 0.012]
    muzzle_x = (min(point.x for point in muzzle_points) + max(point.x for point in muzzle_points)) * 0.5
    muzzle_y = (min(point.y for point in muzzle_points) + max(point.y for point in muzzle_points)) * 0.5

    return {
        "PrimaryGripSocket": PRIMARY_GRIP_SOCKET,
        "SupportGripSocket": SUPPORT_GRIP_SOCKET,
        "MagazineGripSocket": magazine_contact,
        "MagazineWellSocket": Vector((0.0, magazine_maximum.y, 0.205)),
        "ChargingHandleSocket": action_contact,
        "OpticRailSocket": Vector((0.0, optic_y, optic_z)),
        "MuzzleSocket": Vector((muzzle_x, muzzle_y, muzzle_z)),
    }


def configure_source_materials() -> None:
    # Preserve every authored material and texture while giving the imported
    # surfaces explicit scalar PBR fallbacks for engines without texture slots.
    roles = {
        "Material.004": (0.64, 0.31, "finished_steel_slide"),
        "Material": (0.12, 0.57, "polymer_frame_and_grip"),
        "Material.005": (0.52, 0.34, "steel_trigger_and_controls"),
        "Material.006": (0.69, 0.29, "slide_insert_and_barrel_detail"),
        "Material.009": (0.58, 0.33, "slide_top_detail"),
    }
    for name, (metallic, roughness, role) in roles.items():
        material = bpy.data.materials.get(name)
        if material is None:
            raise RuntimeError(f"GSh-18 source material {name!r} is unavailable.")
        material.metallic = metallic
        material.roughness = roughness
        material["surface_role"] = role
        material["authored_texture_preserved"] = True
        if material.use_nodes:
            principled = material.node_tree.nodes.get("Principled BSDF")
            if principled is not None:
                principled.inputs["Metallic"].default_value = metallic
                principled.inputs["Roughness"].default_value = roughness


def build_asset() -> tuple[
    bpy.types.Object,
    dict[str, Vector],
    int,
]:
    clear_scene()
    sources = import_and_validate_source()
    copies_by_source = {
        source.name: evaluated_mesh_copy(source, f"SourceCopy_{source.name}")
        for source in sources
    }
    source_minimum, source_maximum = source_copy_bounds(list(copies_by_source.values()))
    transform = canonical_transform(source_minimum, source_maximum)
    for copy in copies_by_source.values():
        copy.data.transform(transform)
        copy.data.update()
    slide_geometry, slide_lower_control = separate_authored_slide(
        copies_by_source.pop("Object_21")
    )
    configure_source_materials()

    root = bpy.data.objects.new(ROOT_NAME, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.045
    root["runtime_asset"] = True
    root["weapon_platform"] = "GSh18"
    root["coordinate_contract"] = "identity metres; X lateral, Y up, Z stock-to-muzzle"
    root["presentation_length_m"] = TARGET_LENGTH_METERS
    root["source_creator"] = SOURCE_CREATOR
    root["source_license"] = SOURCE_LICENSE
    root["source_sha256"] = SOURCE_SHA256
    root["source_acquisition_date"] = SOURCE_ACQUISITION_DATE
    root["adaptation_date"] = ADAPTATION_DATE
    root["visible_action_geometry"] = True
    root["action_geometry_source"] = "authored TastyTony slide mesh"
    root["action_component_sources"] = (
        "Object_21 connected components 404+246+226, Object_28, Object_30"
    )
    root["magazine_geometry_source"] = "Steel Tide Blender DCC adaptation"
    root["action_travel_m"] = ACTION_TRAVEL.length
    root["source_topology_preserved"] = True
    bpy.context.collection.objects.link(root)

    body_objects = []
    for source_name, output_name in FIXED_OUTPUT_NAMES.items():
        obj = copies_by_source[source_name]
        obj.name = output_name
        obj.data.name = f"{output_name}Mesh"
        obj["mechanism_role"] = "fixed_weapon_body"
        obj.parent = root
        body_objects.append(obj)
    slide_lower_control.name = "SlideLowerControlGeometry"
    slide_lower_control.data.name = "SlideLowerControlGeometryMesh"
    slide_lower_control["mechanism_role"] = "fixed_lower_slide_control"
    slide_lower_control.parent = root
    body_objects.append(slide_lower_control)

    charging_handle = new_empty(
        "ChargingHandle", root, ACTION_HOME, "slide_action_pivot"
    )
    slide_geometry.name = "ChargingHandleGeometry"
    slide_geometry.data.name = "ChargingHandleGeometryMesh"
    slide_geometry["mechanism_role"] = "empty_reload_slide_action"
    parent_preserving_world(slide_geometry, charging_handle)
    action_objects = [slide_geometry]
    for source_name, output_name in ACTION_OUTPUT_NAMES.items():
        obj = copies_by_source[source_name]
        obj.name = output_name
        obj.data.name = f"{output_name}Mesh"
        obj["mechanism_role"] = "empty_reload_slide_action"
        parent_preserving_world(obj, charging_handle)
        action_objects.append(obj)

    magazine_geometry = create_magazine_geometry("MagazineGeometry")
    magazine_triangle_count = len(magazine_geometry.data.polygons)
    magazine = new_empty("Magazine", root, MAGAZINE_HOME, "primary_magazine_pivot")
    parent_preserving_world(magazine_geometry, magazine)

    spare_magazine = new_empty(
        "SpareMagazine", root, SPARE_MAGAZINE_HOME, "spare_magazine_pivot"
    )
    spare_geometry = magazine_geometry.copy()
    spare_geometry.data = magazine_geometry.data.copy()
    spare_geometry.name = "SpareMagazineGeometry"
    spare_geometry.data.name = "SpareMagazineMesh"
    spare_geometry["mechanism_role"] = "spare_detachable_18_round_magazine"
    bpy.context.collection.objects.link(spare_geometry)
    spare_geometry.parent = spare_magazine
    spare_geometry.matrix_parent_inverse = Matrix.Identity(4)
    spare_geometry.matrix_basis = magazine_geometry.matrix_basis.copy()
    bpy.context.view_layer.update()

    sockets = derive_socket_positions(
        tuple(body_objects), tuple(action_objects), magazine_geometry
    )
    for name in SOCKET_NAMES:
        if name == "ChargingHandleSocket":
            socket = new_empty(
                name,
                charging_handle,
                sockets[name] - ACTION_HOME,
                "rear_slide_surface_contact",
            )
            socket["derived_from_mesh"] = "ChargingHandleGeometry"
            socket["surface_region"] = "left_rear_slide"
        elif name == "MagazineGripSocket":
            socket = new_empty(
                name,
                magazine,
                sockets[name] - MAGAZINE_HOME,
                "left_magazine_surface_contact",
            )
            socket["derived_from_mesh"] = "MagazineGeometry"
            socket["surface_region"] = "left_middle_magazine_wall"
        else:
            new_empty(name, root, sockets[name], name)
    root["optic_socket_y"] = sockets["OpticRailSocket"].y
    root["optic_socket_z"] = sockets["OpticRailSocket"].z

    keep = {root, *root.children_recursive}
    for obj in list(bpy.context.scene.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)

    if sum(len(obj.data.polygons) for obj in body_objects) != BODY_TRIANGLE_COUNT:
        raise RuntimeError("Fixed GSh-18 source topology was not preserved.")
    if sum(len(obj.data.polygons) for obj in action_objects) != ACTION_TRIANGLE_COUNT:
        raise RuntimeError("Moving GSh-18 source topology was not preserved.")
    if magazine_geometry.data is spare_geometry.data:
        raise RuntimeError("GSh-18 magazines are not independent mesh instances.")
    return root, sockets, magazine_triangle_count


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
    if OUTPUT_GLTF_SHA256 is not None and (
        actual_hash != OUTPUT_GLTF_SHA256 or actual_bytes != OUTPUT_GLTF_BYTES
    ):
        raise RuntimeError(
            "Deterministic GSh-18 GLB output drifted: "
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
    target = Vector((-0.13, -0.05, -0.235))
    camera.location = Vector((0.82, -1.18, 0.52))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.25
    scene.camera = camera
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for property_name in (
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
        setattr(scene.render, property_name, False)
    scene.render.filepath = str(OUTPUT_PREVIEW)
    scene.world.color = (0.035, 0.045, 0.055)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    actual_hash = sha256(OUTPUT_PREVIEW)
    actual_bytes = OUTPUT_PREVIEW.stat().st_size
    if OUTPUT_PREVIEW_SHA256 is not None and (
        actual_hash != OUTPUT_PREVIEW_SHA256 or actual_bytes != OUTPUT_PREVIEW_BYTES
    ):
        raise RuntimeError(
            "Deterministic GSh-18 preview output drifted: "
            f"sha256={actual_hash} bytes={actual_bytes}"
        )
    if root.name not in bpy.context.scene.objects:
        raise RuntimeError("GSh-18 preview lost the runtime root.")


def require_unique_node(name: str) -> bpy.types.Object:
    matches = [obj for obj in bpy.context.scene.objects if obj.name == name]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one exported node {name!r}, found {len(matches)}")
    return matches[0]


def root_local_godot_position(
    root: bpy.types.Object,
    obj: bpy.types.Object,
) -> Vector:
    return blender_to_godot(root.matrix_world.inverted() @ obj.matrix_world.translation)


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


def bounds_overlap(
    first: tuple[Vector, Vector],
    second: tuple[Vector, Vector],
) -> bool:
    return all(
        first[0][axis] <= second[1][axis] + 0.002
        and second[0][axis] <= first[1][axis] + 0.002
        for axis in range(3)
    )


def validate_exported_asset(
    expected_sockets: dict[str, Vector],
    magazine_triangle_count: int,
) -> tuple[
    tuple[Vector, Vector],
    tuple[Vector, Vector],
    dict[str, float],
]:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    root = require_unique_node(ROOT_NAME)
    magazine = require_unique_node("Magazine")
    spare_magazine = require_unique_node("SpareMagazine")
    magazine_geometry = require_unique_node("MagazineGeometry")
    spare_geometry = require_unique_node("SpareMagazineGeometry")
    charging_handle = require_unique_node("ChargingHandle")
    body_objects = tuple(require_unique_node(name) for name in FIXED_NODE_NAMES)
    action_objects = tuple(require_unique_node(name) for name in ACTION_NODE_NAMES)

    expected_nodes = {
        ROOT_NAME,
        *FIXED_NODE_NAMES,
        "Magazine",
        "MagazineGeometry",
        "SpareMagazine",
        "SpareMagazineGeometry",
        "ChargingHandle",
        *ACTION_NODE_NAMES,
        *SOCKET_NAMES,
    }
    actual_nodes = {obj.name for obj in bpy.context.scene.objects}
    if actual_nodes != expected_nodes:
        raise RuntimeError(
            "Exported GSh-18 node contract drifted: "
            f"{sorted(actual_nodes)} != {sorted(expected_nodes)}"
        )
    if root.parent is not None or root.matrix_world != Matrix.Identity(4):
        raise RuntimeError("GSh-18 root is not an identity metre contract.")
    if any(obj.parent != root for obj in body_objects):
        raise RuntimeError("GSh-18 fixed body hierarchy is invalid.")
    if magazine.parent != root or spare_magazine.parent != root:
        raise RuntimeError("GSh-18 magazine pivot hierarchy is invalid.")
    if magazine_geometry.parent != magazine or spare_geometry.parent != spare_magazine:
        raise RuntimeError("GSh-18 magazine geometry hierarchy is invalid.")
    if charging_handle.parent != root or any(
        obj.parent != charging_handle for obj in action_objects
    ):
        raise RuntimeError("GSh-18 visible slide hierarchy is invalid.")
    if magazine_geometry.data is spare_geometry.data:
        raise RuntimeError("Exported GSh-18 magazines share mesh data.")

    body_triangles = sum(len(obj.data.polygons) for obj in body_objects)
    action_triangles = sum(len(obj.data.polygons) for obj in action_objects)
    magazine_triangles = len(magazine_geometry.data.polygons)
    spare_triangles = len(spare_geometry.data.polygons)
    if body_triangles != BODY_TRIANGLE_COUNT:
        raise RuntimeError(f"GSh-18 body topology drifted: {body_triangles}")
    if action_triangles != ACTION_TRIANGLE_COUNT:
        raise RuntimeError(f"GSh-18 action topology drifted: {action_triangles}")
    if magazine_triangles != magazine_triangle_count or spare_triangles != magazine_triangle_count:
        raise RuntimeError(
            "GSh-18 magazine topology drifted: "
            f"primary={magazine_triangles} spare={spare_triangles}"
        )
    if body_triangles + action_triangles != SOURCE_TRIANGLE_COUNT:
        raise RuntimeError("GSh-18 authored source triangle conservation failed.")
    if not root.get("visible_action_geometry", False):
        raise RuntimeError("GSh-18 visible slide geometry metadata is missing.")

    if (root_local_godot_position(root, magazine) - MAGAZINE_HOME).length > POSITION_TOLERANCE:
        raise RuntimeError("GSh-18 primary magazine pivot left the profile home.")
    if (
        root_local_godot_position(root, spare_magazine) - SPARE_MAGAZINE_HOME
    ).length > POSITION_TOLERANCE:
        raise RuntimeError("GSh-18 spare magazine pivot left the profile home.")
    if (
        root_local_godot_position(root, charging_handle) - ACTION_HOME
    ).length > POSITION_TOLERANCE:
        raise RuntimeError("GSh-18 slide pivot left the profile home.")

    for name in SOCKET_NAMES:
        socket = require_unique_node(name)
        expected_parent = (
            magazine
            if name == "MagazineGripSocket"
            else charging_handle
            if name == "ChargingHandleSocket"
            else root
        )
        if socket.parent != expected_parent:
            raise RuntimeError(f"GSh-18 socket {name} has an invalid parent.")
        actual = root_local_godot_position(root, socket)
        if (actual - expected_sockets[name]).length > POSITION_TOLERANCE:
            raise RuntimeError(
                f"GSh-18 socket {name} drifted: "
                f"{tuple(actual)} != {tuple(expected_sockets[name])}"
            )

    body_bounds = godot_mesh_bounds(root, body_objects)
    action_bounds = godot_mesh_bounds(root, action_objects)
    magazine_bounds = godot_mesh_bounds(root, (magazine_geometry,))
    magazine_socket_position = root_local_godot_position(
        root,
        require_unique_node("MagazineGripSocket"),
    )
    (
        magazine_surface,
        _,
        magazine_surface_owner,
        _,
        magazine_surface_distance,
    ) = mesh_surface_contact_godot(
        (magazine_geometry,),
        magazine_socket_position,
    )
    magazine_side_fraction = left_surface_fraction(
        magazine_surface,
        magazine_bounds,
    )
    magazine_height_fraction = (
        (magazine_surface.y - magazine_bounds[0].y)
        / (magazine_bounds[1].y - magazine_bounds[0].y)
    )
    magazine_length_fraction = (
        (magazine_surface.z - magazine_bounds[0].z)
        / (magazine_bounds[1].z - magazine_bounds[0].z)
    )
    if (
        magazine_surface_owner != "MagazineGeometry"
        or magazine_surface_distance > SOCKET_SURFACE_TOLERANCE
        or magazine_side_fraction > LEFT_SURFACE_ZONE_FRACTION
        or not 0.25 <= magazine_height_fraction <= 0.75
        or not 0.25 <= magazine_length_fraction <= 0.75
    ):
        raise RuntimeError(
            "GSh-18 magazine grip socket left its real side surface: "
            f"owner={magazine_surface_owner} "
            f"distance={magazine_surface_distance:.9f} "
            f"side_fraction={magazine_side_fraction:.6f} "
            f"height_fraction={magazine_height_fraction:.6f} "
            f"length_fraction={magazine_length_fraction:.6f}"
        )

    action_socket_position = root_local_godot_position(
        root,
        require_unique_node("ChargingHandleSocket"),
    )
    (
        action_surface,
        _,
        action_surface_owner,
        _,
        action_surface_distance,
    ) = mesh_surface_contact_godot(
        action_objects,
        action_socket_position,
    )
    action_side_fraction = left_surface_fraction(action_surface, action_bounds)
    action_rear_fraction = rear_fraction(action_surface, action_bounds)
    if (
        action_surface_owner != "ChargingHandleGeometry"
        or action_surface_distance > SOCKET_SURFACE_TOLERANCE
        or action_side_fraction > LEFT_SURFACE_ZONE_FRACTION
        or action_rear_fraction < ACTION_SOCKET_MIN_REAR_FRACTION
    ):
        raise RuntimeError(
            "GSh-18 action socket left the authored rear slide surface: "
            f"owner={action_surface_owner} "
            f"distance={action_surface_distance:.9f} "
            f"side_fraction={action_side_fraction:.6f} "
            f"rear_fraction={action_rear_fraction:.6f}"
        )
    if not bounds_overlap(body_bounds, action_bounds):
        raise RuntimeError("GSh-18 moving slide is floating away from the frame.")
    if not bounds_overlap(body_bounds, magazine_bounds):
        raise RuntimeError("GSh-18 installed magazine is floating away from the grip.")
    if ACTION_TRAVEL.length < 0.020:
        raise RuntimeError("GSh-18 authored empty-reload action travel is under 20 mm.")

    rest_location = charging_handle.location.copy()
    charging_handle.location += godot_to_blender(ACTION_TRAVEL)
    bpy.context.view_layer.update()
    cycled_action_bounds = godot_mesh_bounds(root, action_objects)
    charging_handle.location = rest_location
    bpy.context.view_layer.update()
    require_bounds(
        "GSh-18 cycled visible slide",
        cycled_action_bounds,
        (action_bounds[0] + ACTION_TRAVEL, action_bounds[1] + ACTION_TRAVEL),
    )

    primary_bounds = godot_mesh_bounds(
        root, (*body_objects, *action_objects, magazine_geometry)
    )
    scene_bounds = godot_mesh_bounds(
        root, (*body_objects, *action_objects, magazine_geometry, spare_geometry)
    )
    if EXPECTED_PRIMARY_MINIMUM is not None and EXPECTED_PRIMARY_MAXIMUM is not None:
        require_bounds(
            "GSh-18 primary asset",
            primary_bounds,
            (EXPECTED_PRIMARY_MINIMUM, EXPECTED_PRIMARY_MAXIMUM),
        )
    if EXPECTED_SCENE_MINIMUM is not None and EXPECTED_SCENE_MAXIMUM is not None:
        require_bounds(
            "GSh-18 full scene",
            scene_bounds,
            (EXPECTED_SCENE_MINIMUM, EXPECTED_SCENE_MAXIMUM),
        )
    return primary_bounds, scene_bounds, {
        "magazine_surface_distance": magazine_surface_distance,
        "magazine_side_fraction": magazine_side_fraction,
        "action_surface_distance": action_surface_distance,
        "action_side_fraction": action_side_fraction,
        "action_rear_fraction": action_rear_fraction,
    }


def format_vector(vector: Vector) -> str:
    return "(" + ",".join(f"{value:.6f}" for value in vector) + ")"


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    root, sockets, magazine_triangles = build_asset()
    export_asset(root)
    save_editable_source()
    render_preview(root)
    primary_bounds, scene_bounds, surface_audit = validate_exported_asset(
        sockets, magazine_triangles
    )
    print(
        "RELOADABLE_GSH18_EXPORT "
        f"source_sha256={SOURCE_SHA256} "
        f"glb_sha256={sha256(OUTPUT_GLB)} "
        f"blend_sha256={sha256(OUTPUT_BLEND)} "
        f"preview_sha256={sha256(OUTPUT_PREVIEW)} "
        f"body_triangles={BODY_TRIANGLE_COUNT} "
        f"action_triangles={ACTION_TRIANGLE_COUNT} "
        f"magazine_triangles={magazine_triangles} "
        f"spare_triangles={magazine_triangles} "
        f"scene_triangles={SOURCE_TRIANGLE_COUNT + 2 * magazine_triangles} "
        f"magazine_home={format_vector(MAGAZINE_HOME)} "
        f"action_home={format_vector(ACTION_HOME)} "
        f"magazine_grip_socket={format_vector(sockets['MagazineGripSocket'])} "
        f"magazine_surface_distance="
        f"{surface_audit['magazine_surface_distance']:.9f} "
        f"action_socket={format_vector(sockets['ChargingHandleSocket'])} "
        f"action_surface_distance={surface_audit['action_surface_distance']:.9f} "
        f"action_rear_fraction={surface_audit['action_rear_fraction']:.6f} "
        f"optic_socket={format_vector(sockets['OpticRailSocket'])} "
        f"muzzle_socket={format_vector(sockets['MuzzleSocket'])} "
        f"primary_bounds={format_vector(primary_bounds[0])}.."
        f"{format_vector(primary_bounds[1])} "
        f"scene_bounds={format_vector(scene_bounds[0])}.."
        f"{format_vector(scene_bounds[1])} "
        f"glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"blend_bytes={OUTPUT_BLEND.stat().st_size} "
        f"preview_bytes={OUTPUT_PREVIEW.stat().st_size}"
    )


if __name__ == "__main__":
    main()
