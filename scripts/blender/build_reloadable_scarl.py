"""Build the mechanism-ready Steel Tide SCAR-L DCC derivative.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup \
        --python scripts/blender/build_reloadable_scarl.py

The source is AdamKokrito's authored CC BY 3.0 SCAR-L. Its independent
magazine, bolt, and charging handle remain independent, visible mechanisms.
This Blender build applies a conservative hard-surface finishing pass, assigns
physically differentiated scalar materials, normalizes to the project metre
contract, and exports deterministic gameplay pivots and sockets.
"""

from __future__ import annotations

import hashlib
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import closest_point_on_tri


REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_scarl"
SOURCE_GLB = (
    REPO_ROOT
    / "source_art"
    / "third_party"
    / "adamkokrito_scarl"
    / "adamkokrito_scarl.glb"
)
OUTPUT_GLB = OUTPUT_DIR / "scarl_reloadable.glb"
OUTPUT_BLEND = (
    REPO_ROOT
    / "source_art"
    / "reloadable_weapons"
    / "scarl_reloadable.blend"
)
PREVIEW_PATH = OUTPUT_DIR / "scarl_reloadable_preview.png"

SOURCE_CREATOR = "AdamKokrito"
SOURCE_TITLE = "ScarL"
SOURCE_URL = "https://poly.pizza/m/ab1V8RlPDc"
SOURCE_LICENSE = "CC BY 3.0"
SOURCE_ACQUISITION_DATE = "2026-08-29"
SOURCE_SHA256 = "B67738744962E20E1829008064153D7817D3B4532391D759582028DB225E3308"
SOURCE_BYTES = 114_000

ROOT_NAME = "SteelTideReloadableScarL"
SOURCE_ROOT_NAME = "RootNode"
SOURCE_MINIMUM = Vector((-4.105200, -0.377230, -1.561584))
SOURCE_MAXIMUM = Vector((4.124213, 0.289445, 1.566676))
SOURCE_MESH_AUDIT = {
    "Base": (1_767, 1_104, ("Highlight", "Primary", "Secondary")),
    "Bolt": (22, 12, ("Secondary",)),
    "CharginHandle": (246, 140, ("Highlight", "Secondary")),
    "IronSight": (262, 148, ("Secondary",)),
    "Mag": (40, 20, ("Highlight",)),
    "Safety": (146, 80, ("Secondary",)),
    "Stock": (244, 124, ("Primary", "Secondary")),
    "Trigger": (72, 36, ("Highlight",)),
}
SOURCE_TRIANGLE_COUNT = sum(value[1] for value in SOURCE_MESH_AUDIT.values())

TARGET_LENGTH_METERS = 1.58
TARGET_STOCK_Z = 0.32
OPTIC_RAIL_Z = -0.25
PRIMARY_GRIP_Z_RANGE = (-0.27, -0.08)
PRIMARY_GRIP_MAXIMUM_Y = -0.075
SUPPORT_GRIP_Z_RANGE = (-0.69, -0.61)
SUPPORT_GRIP_MAXIMUM_Y = 0.12
SPARE_MAGAZINE_OFFSET = Vector((-0.30, -0.42, 0.13))
BEVEL_WIDTH_METERS = 0.0014
BEVEL_SEGMENTS = 2
BEVEL_ANGLE_RADIANS = math.radians(34.0)
BOUNDS_TOLERANCE = 0.0001
IDENTITY_TOLERANCE = 0.00001
SOCKET_TOLERANCE = 0.0001

MATERIAL_PHYSICS = {
    "Primary": (0.02, 0.48, "tan_polymer_stock_and_handguard"),
    "Secondary": (0.75, 0.27, "phosphated_receiver_and_controls"),
    "Highlight": (0.86, 0.23, "finished_magazine_and_hardware"),
}
OUTPUT_NODE_TRIANGLES = {
    "WeaponBodyGeometry": 4_579,
    "FrontIronSight": 669,
    "StockGeometry": 668,
    "TriggerGeometry": 228,
    "SafetyGeometry": 540,
    "IronSightGeometry": 832,
    "MagazineGeometry": 132,
    "SpareMagazineGeometry": 132,
    "BoltGeometry": 85,
    "ChargingHandleGeometry": 852,
}
EXPECTED_PRIMARY_MINIMUM = Vector((-0.063999, -0.299936, -1.260000))
EXPECTED_PRIMARY_MAXIMUM = Vector((0.063999, 0.300062, 0.320000))
EXPECTED_SCENE_MINIMUM = Vector((-0.330012, -0.719936, -1.260000))
EXPECTED_SCENE_MAXIMUM = Vector((0.063999, 0.300062, 0.320000))
OUTPUT_GLB_SHA256 = "13F03B7D9E6CD2C50A85B5FA5E3C803530AB8AC165A1CB70CB296AC0BB8188BC"
OUTPUT_GLB_BYTES = 178_048
OUTPUT_PREVIEW_SHA256 = "830158C4F22B38A6BD461957FCBCD2D1A2AB8B6F9015D4FA3A6CD70FFA6AE6A0"
OUTPUT_PREVIEW_BYTES = 1_493_438

SOCKET_NAMES = (
    "PrimaryGripSocket",
    "SupportGripSocket",
    "MagazineGripSocket",
    "MagazineWellSocket",
    "ChargingHandleSocket",
    "OpticRailSocket",
    "MuzzleSocket",
)
GEOMETRY_NAMES = (
    "WeaponBodyGeometry",
    "FrontIronSight",
    "StockGeometry",
    "TriggerGeometry",
    "SafetyGeometry",
    "IronSightGeometry",
    "MagazineGeometry",
    "SpareMagazineGeometry",
    "BoltGeometry",
    "ChargingHandleGeometry",
)

FRONT_IRON_SIGHT_SOURCE_FACES = 112
FRONT_IRON_SIGHT_MINIMUM = Vector(
    (-0.0295619294, 1.0381450653, 0.1344571412)
)
FRONT_IRON_SIGHT_MAXIMUM = Vector(
    (0.0127077205, 1.0825898647, 0.3003039956)
)


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
        raise RuntimeError("Cannot calculate bounds for empty SCAR-L geometry.")
    return (
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


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
                f"SCAR-L {label} {bound_name} drifted: "
                f"{tuple(actual_bound)} != {tuple(expected_bound)}"
            )


def source_world_points(obj: bpy.types.Object) -> list[Vector]:
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def import_and_validate_source() -> dict[str, bpy.types.Object]:
    if not SOURCE_GLB.is_file():
        raise RuntimeError(f"Missing tracked AdamKokrito SCAR-L source: {SOURCE_GLB}")
    if SOURCE_GLB.stat().st_size != SOURCE_BYTES or sha256(SOURCE_GLB) != SOURCE_SHA256:
        raise RuntimeError("AdamKokrito SCAR-L source identity drifted.")

    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    source_root = bpy.data.objects.get(SOURCE_ROOT_NAME)
    if source_root is None:
        raise RuntimeError("AdamKokrito SCAR-L RootNode is unavailable.")
    meshes: dict[str, bpy.types.Object] = {}
    for name, (vertices, triangles, materials) in SOURCE_MESH_AUDIT.items():
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH" or obj.parent != source_root:
            raise RuntimeError(f"SCAR-L authored source node {name!r} is unavailable.")
        actual_materials = tuple(material.name for material in obj.data.materials)
        if (
            len(obj.data.vertices) != vertices
            or len(obj.data.polygons) != triangles
            or actual_materials != materials
        ):
            raise RuntimeError(
                f"SCAR-L source node {name} drifted: "
                f"vertices={len(obj.data.vertices)} triangles={len(obj.data.polygons)} "
                f"materials={actual_materials}"
            )
        meshes[name] = obj
    require_bounds(
        "authored source",
        point_bounds([point for obj in meshes.values() for point in source_world_points(obj)]),
        (SOURCE_MINIMUM, SOURCE_MAXIMUM),
    )
    if sum(len(obj.data.polygons) for obj in meshes.values()) != SOURCE_TRIANGLE_COUNT:
        raise RuntimeError("SCAR-L source triangle audit drifted.")
    return meshes


def canonical_transform() -> Matrix:
    source_size = SOURCE_MAXIMUM - SOURCE_MINIMUM
    source_center = (SOURCE_MINIMUM + SOURCE_MAXIMUM) * 0.5
    scale = TARGET_LENGTH_METERS / source_size.x
    target_center_z = TARGET_STOCK_Z - TARGET_LENGTH_METERS * 0.5
    # Source X=barrel, Y=thickness, Z=up. glTF maps Blender (X,Y,Z) to
    # Godot (X,Z,-Y), yielding X=lateral, Y=up, Z=stock-to-muzzle.
    return Matrix(
        (
            (0.0, -scale, 0.0, scale * source_center.y),
            (scale, 0.0, 0.0, -scale * source_center.x - target_center_z),
            (0.0, 0.0, scale, -scale * source_center.z),
            (0.0, 0.0, 0.0, 1.0),
        )
    )


def bake_object_transform(obj: bpy.types.Object, transform: Matrix) -> None:
    obj.data.transform(transform @ obj.matrix_world)
    obj.data.update()
    obj.parent = None
    obj.matrix_world = Matrix.Identity(4)


def configure_materials() -> None:
    for name, (metallic, roughness, role) in MATERIAL_PHYSICS.items():
        material = bpy.data.materials.get(name)
        if material is None:
            raise RuntimeError(f"SCAR-L material {name!r} is unavailable.")
        material.metallic = metallic
        material.roughness = roughness
        material["surface_role"] = role
        material["scalar_pbr_only"] = True
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is None:
            raise RuntimeError(f"SCAR-L material {name!r} lacks Principled BSDF.")
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness


def apply_surface_finish(obj: bpy.types.Object) -> None:
    # The authored low-poly GLB intentionally carries split vertices at hard
    # corners. Weld only exactly coincident positions before beveling so the
    # finishing pass can create real shared-edge chamfers without altering the
    # silhouette or inventing replacement geometry.
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.000001)
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("MicroBevel_1p4mm", "BEVEL")
    bevel.width = BEVEL_WIDTH_METERS
    bevel.segments = BEVEL_SEGMENTS
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = BEVEL_ANGLE_RADIANS
    bevel.affect = "EDGES"
    bevel.use_clamp_overlap = True
    bevel.loop_slide = True
    bevel.harden_normals = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)

    weighted = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=weighted.name)

    triangulate = obj.modifiers.new("DeterministicTriangulation", "TRIANGULATE")
    triangulate.quad_method = "BEAUTY"
    triangulate.ngon_method = "BEAUTY"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    # The source uses only scalar PBR materials and has no texture/image
    # resources. Drop its unused UV layer after beveling: Blender's bevel UV
    # interpolation can differ at the final float bit across headless runs,
    # while carrying no visual information here.
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    obj.select_set(False)
    obj.data.update()


def separate_front_iron_sight(body: bpy.types.Object) -> bpy.types.Object:
    """Split the source's welded flip-up front sight into hideable geometry."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")

    selected_faces = []
    for polygon in body.data.polygons:
        points = [body.data.vertices[index].co for index in polygon.vertices]
        polygon.select = all(
            1.037 <= point.y <= 1.084 and point.z >= 0.134
            for point in points
        )
        if polygon.select:
            selected_faces.append(polygon)
    if len(selected_faces) != FRONT_IRON_SIGHT_SOURCE_FACES:
        raise RuntimeError(
            "SCAR-L welded front-sight selection drifted: "
            f"{len(selected_faces)} != {FRONT_IRON_SIGHT_SOURCE_FACES}"
        )
    selected_points = [
        body.data.vertices[index].co
        for index in {
            vertex_index
            for polygon in selected_faces
            for vertex_index in polygon.vertices
        }
    ]
    require_bounds(
        "welded front sight",
        point_bounds(selected_points),
        (FRONT_IRON_SIGHT_MINIMUM, FRONT_IRON_SIGHT_MAXIMUM),
    )

    previous_objects = set(bpy.context.scene.objects)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    separated = [
        obj
        for obj in bpy.context.scene.objects
        if obj not in previous_objects and obj.type == "MESH"
    ]
    if len(separated) != 1:
        raise RuntimeError(
            f"SCAR-L front-sight separation created {len(separated)} objects."
        )
    front_sight = separated[0]
    front_sight.name = "FrontIronSight"
    front_sight.data.name = "FrontIronSightMesh"
    front_sight["source_node"] = "Base/front_flip_sight"
    front_sight["mechanism_role"] = "hideable_front_iron_sight"
    return front_sight


def godot_to_blender(position: Vector) -> Vector:
    return Vector((position.x, -position.z, position.y))


def blender_to_godot(position: Vector) -> Vector:
    return Vector((position.x, position.z, -position.y))


def mesh_godot_points(obj: bpy.types.Object) -> list[Vector]:
    return [
        blender_to_godot(obj.matrix_world @ vertex.co)
        for vertex in obj.data.vertices
    ]


def mesh_surface_distance(obj: bpy.types.Object, point: Vector) -> float:
    points = mesh_godot_points(obj)
    obj.data.calc_loop_triangles()
    distances: list[float] = []
    for triangle in obj.data.loop_triangles:
        a, b, c = (points[index] for index in triangle.vertices)
        nearest = closest_point_on_tri(point, a, b, c)
        distances.append((nearest - point).length)
    if not distances:
        raise RuntimeError(f"SCAR-L mesh {obj.name!r} has no contact surface.")
    return min(distances)


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


def derive_socket_positions(
    body: bpy.types.Object,
    magazine: bpy.types.Object,
    handle: bpy.types.Object,
    authored_action_home: Vector,
) -> dict[str, Vector]:
    body_points = mesh_godot_points(body)
    primary_grip_points = [
        point for point in body_points
        if PRIMARY_GRIP_Z_RANGE[0] <= point.z <= PRIMARY_GRIP_Z_RANGE[1]
        and point.y <= PRIMARY_GRIP_MAXIMUM_Y
    ]
    support_grip_points = [
        point for point in body_points
        if SUPPORT_GRIP_Z_RANGE[0] <= point.z <= SUPPORT_GRIP_Z_RANGE[1]
        and point.y <= SUPPORT_GRIP_MAXIMUM_Y
    ]
    if not primary_grip_points or not support_grip_points:
        raise RuntimeError("Unable to sample the SCAR-L authored hand-contact volumes.")
    primary_minimum, primary_maximum = point_bounds(primary_grip_points)
    support_minimum, support_maximum = point_bounds(support_grip_points)
    primary_grip = (primary_minimum + primary_maximum) * 0.5
    support_grip = (support_minimum + support_maximum) * 0.5
    rail_points = [
        point
        for point in body_points
        if abs(point.z - OPTIC_RAIL_Z) <= 0.055
    ]
    if not rail_points:
        raise RuntimeError("Unable to sample the SCAR-L optic rail contact surface.")
    rail_height = max(point.y for point in rail_points)
    if not 0.16 <= rail_height <= 0.31:
        raise RuntimeError(f"SCAR-L rail height is implausible: {rail_height:.6f}")

    magazine_points = mesh_godot_points(magazine)
    magazine_minimum, magazine_maximum = point_bounds(magazine_points)
    magazine_top = [
        point for point in magazine_points if point.y >= magazine_maximum.y - 0.012
    ]
    well_z = (
        min(point.z for point in magazine_top)
        + max(point.z for point in magazine_top)
    ) * 0.5
    magazine_well = Vector((0.0, magazine_maximum.y, well_z))

    muzzle_z = min(point.z for point in body_points)
    muzzle_points = [point for point in body_points if point.z <= muzzle_z + 0.018]
    muzzle = Vector(
        (
            (min(point.x for point in muzzle_points) + max(point.x for point in muzzle_points)) * 0.5,
            (min(point.y for point in muzzle_points) + max(point.y for point in muzzle_points)) * 0.5,
            muzzle_z,
        )
    )
    handle_minimum, handle_maximum = point_bounds(mesh_godot_points(handle))
    if any(
        authored_action_home[index] < handle_minimum[index] - SOCKET_TOLERANCE
        or authored_action_home[index] > handle_maximum[index] + SOCKET_TOLERANCE
        for index in range(3)
    ):
        raise RuntimeError(
            "SCAR-L authored charging-handle origin left its visible geometry."
        )
    sockets = {
        "PrimaryGripSocket": primary_grip,
        "SupportGripSocket": support_grip,
        "MagazineGripSocket": Vector(
            (
                magazine_minimum.x,
                (magazine_minimum.y + magazine_maximum.y) * 0.5,
                (magazine_minimum.z + magazine_maximum.z) * 0.5,
            )
        ),
        "MagazineWellSocket": magazine_well,
        "ChargingHandleSocket": authored_action_home,
        "OpticRailSocket": Vector((0.0, rail_height, OPTIC_RAIL_Z)),
        "MuzzleSocket": muzzle,
    }
    contact_distances = {
        "PrimaryGripSocket": mesh_surface_distance(body, sockets["PrimaryGripSocket"]),
        "SupportGripSocket": mesh_surface_distance(body, sockets["SupportGripSocket"]),
        "MagazineGripSocket": mesh_surface_distance(
            magazine, sockets["MagazineGripSocket"]
        ),
        "MagazineWellSocket": mesh_surface_distance(
            magazine, sockets["MagazineWellSocket"]
        ),
        "ChargingHandleSocket": mesh_surface_distance(
            handle, sockets["ChargingHandleSocket"]
        ),
        "OpticRailSocket": mesh_surface_distance(body, sockets["OpticRailSocket"]),
        "MuzzleSocket": mesh_surface_distance(body, sockets["MuzzleSocket"]),
    }
    limits = {
        "PrimaryGripSocket": 0.055,
        "SupportGripSocket": 0.055,
        "MagazineGripSocket": 0.0015,
        "MagazineWellSocket": 0.010,
        "ChargingHandleSocket": 0.030,
        "OpticRailSocket": 0.010,
        # The marker intentionally sits at the centre of the open bore, so its
        # nearest visible triangle is the authored muzzle-ring radius away.
        "MuzzleSocket": 0.015,
    }
    for name, distance in contact_distances.items():
        if distance > limits[name]:
            raise RuntimeError(
                f"SCAR-L {name} is {distance:.6f}m from its authored contact "
                f"surface (limit {limits[name]:.6f}m)."
            )
    if abs(sockets["MagazineGripSocket"].x - magazine_minimum.x) > SOCKET_TOLERANCE:
        raise RuntimeError("SCAR-L magazine grip socket left the authored side face.")
    if abs(sockets["MagazineWellSocket"].y - magazine_maximum.y) > SOCKET_TOLERANCE:
        raise RuntimeError("SCAR-L magazine well socket left the authored top face.")
    if abs(sockets["MuzzleSocket"].z - muzzle_z) > SOCKET_TOLERANCE:
        raise RuntimeError("SCAR-L muzzle socket left the authored muzzle face.")
    for name, distance in contact_distances.items():
        print(f"SCARL_SOCKET_CONTACT name={name} distance_m={distance:.6f}")
    return sockets


def build_runtime_asset() -> tuple[bpy.types.Object, dict[str, Vector]]:
    clear_scene()
    source = import_and_validate_source()
    transform = canonical_transform()
    authored_action_home = blender_to_godot(
        transform @ source["CharginHandle"].matrix_world.translation
    )
    for obj in source.values():
        bake_object_transform(obj, transform)
    front_iron_sight = separate_front_iron_sight(source["Base"])
    configure_materials()

    names = {
        "Base": "WeaponBodyGeometry",
        "Stock": "StockGeometry",
        "Trigger": "TriggerGeometry",
        "Safety": "SafetyGeometry",
        "IronSight": "IronSightGeometry",
        "Mag": "MagazineGeometry",
        "Bolt": "BoltGeometry",
        "CharginHandle": "ChargingHandleGeometry",
    }
    for source_name, runtime_name in names.items():
        obj = source[source_name]
        obj.name = runtime_name
        obj.data.name = f"{runtime_name}Mesh"
        obj["source_node"] = source_name
        obj["dcc_surface_finish"] = "1.4mm two-segment bevel; weighted normals"
        apply_surface_finish(obj)
    front_iron_sight["dcc_surface_finish"] = (
        "1.4mm two-segment bevel; weighted normals"
    )
    apply_surface_finish(front_iron_sight)

    body = source["Base"]
    magazine_geometry = source["Mag"]
    bolt_geometry = source["Bolt"]
    handle_geometry = source["CharginHandle"]
    sockets = derive_socket_positions(
        body,
        magazine_geometry,
        handle_geometry,
        authored_action_home,
    )

    root = bpy.data.objects.new(ROOT_NAME, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    root["runtime_asset"] = True
    root["weapon_platform"] = "ScarL"
    root["source_creator"] = SOURCE_CREATOR
    root["source_title"] = SOURCE_TITLE
    root["source_url"] = SOURCE_URL
    root["source_license"] = SOURCE_LICENSE
    root["source_sha256"] = SOURCE_SHA256
    root["source_acquisition_date"] = SOURCE_ACQUISITION_DATE
    root["adaptation_date"] = SOURCE_ACQUISITION_DATE
    root["visible_action_geometry"] = True
    root["action_geometry"] = "authored Bolt + authored CharginHandle"
    root["surface_finish"] = "1.4mm two-segment bevel; weighted normals; scalar PBR"
    root["coordinate_contract"] = "Godot metres: X lateral, Y up, Z stock-to-muzzle"
    bpy.context.collection.objects.link(root)

    for name in ("Base", "Stock", "Trigger", "Safety", "IronSight"):
        source[name].parent = root
    front_iron_sight.parent = root

    magazine = new_empty(
        "Magazine", root, sockets["MagazineWellSocket"], "primary_magazine_pivot"
    )
    magazine_geometry.parent = magazine
    magazine_geometry.location = -magazine.location
    magazine_geometry["runtime_asset"] = True
    magazine_geometry["mechanism_role"] = "authored_detachable_magazine"
    new_empty(
        "MagazineGripSocket",
        magazine,
        sockets["MagazineGripSocket"] - sockets["MagazineWellSocket"],
        "MagazineGripSocket",
    )

    spare_position = sockets["MagazineWellSocket"] + SPARE_MAGAZINE_OFFSET
    spare_magazine = new_empty(
        "SpareMagazine", root, spare_position, "spare_magazine_pivot"
    )
    spare_geometry = magazine_geometry.copy()
    spare_geometry.data = magazine_geometry.data.copy()
    spare_geometry.name = "SpareMagazineGeometry"
    spare_geometry.data.name = "SpareMagazineGeometryMesh"
    spare_geometry["runtime_asset"] = True
    spare_geometry["mechanism_role"] = "authored_spare_detachable_magazine"
    bpy.context.collection.objects.link(spare_geometry)
    spare_geometry.parent = spare_magazine
    spare_geometry.location = -magazine.location

    action = new_empty(
        "ChargingHandle", root, sockets["ChargingHandleSocket"], "action_pivot"
    )
    for geometry in (bolt_geometry, handle_geometry):
        geometry.parent = action
        geometry.location = -action.location
        geometry["runtime_asset"] = True
        geometry["mechanism_role"] = "empty_reload_action_geometry"
    new_empty("ChargingHandleSocket", action, Vector(), "ChargingHandleSocket")

    for name in SOCKET_NAMES:
        if name in {"MagazineGripSocket", "ChargingHandleSocket"}:
            continue
        new_empty(name, root, sockets[name], name)
    root["optic_rail_socket_y"] = sockets["OpticRailSocket"].y
    root["optic_rail_socket_z"] = sockets["OpticRailSocket"].z

    bpy.context.view_layer.update()
    keep = {root, *root.children_recursive}
    for obj in list(bpy.context.scene.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)

    actual_counts = {
        obj.name: len(obj.data.polygons)
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
    }
    if OUTPUT_NODE_TRIANGLES is not None and actual_counts != OUTPUT_NODE_TRIANGLES:
        raise RuntimeError(
            f"Refined SCAR-L topology drifted: {actual_counts} != {OUTPUT_NODE_TRIANGLES}"
        )
    if magazine_geometry.data is spare_geometry.data:
        raise RuntimeError("SCAR-L magazines are not independent mesh instances.")
    if len(bolt_geometry.data.polygons) == 0 or len(handle_geometry.data.polygons) == 0:
        raise RuntimeError("SCAR-L action pivot lacks authored visible geometry.")
    return root, sockets


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (root, *root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB), export_format="GLB", use_selection=True,
        export_apply=False, export_yup=True, export_attributes=True,
        export_extras=True, export_animations=False, export_cameras=False,
        export_lights=False,
    )
    if OUTPUT_GLB_SHA256 and (
        sha256(OUTPUT_GLB) != OUTPUT_GLB_SHA256
        or OUTPUT_GLB.stat().st_size != OUTPUT_GLB_BYTES
    ):
        raise RuntimeError(
            "Deterministic SCAR-L GLB output drifted: "
            f"sha256={sha256(OUTPUT_GLB)} bytes={OUTPUT_GLB.stat().st_size}"
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
    # Review the authored left-side charging handle and keep both magazines in
    # frame. A near-side orthographic three-quarter view makes mechanism and
    # silhouette review much less ambiguous than the old barrel-on angle.
    target = Vector((0.0, 0.45, -0.23))
    camera.location = Vector((-2.65, 0.18, 0.58))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.25
    scene.camera = camera
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for stamp_property in (
        "use_stamp_camera", "use_stamp_date", "use_stamp_filename",
        "use_stamp_frame", "use_stamp_frame_range", "use_stamp_hostname",
        "use_stamp_lens", "use_stamp_marker", "use_stamp_memory",
        "use_stamp_note", "use_stamp_render_time", "use_stamp_scene",
        "use_stamp_sequencer_strip", "use_stamp_time",
    ):
        setattr(scene.render, stamp_property, False)
    scene.render.filepath = str(PREVIEW_PATH)
    scene.world.color = (0.045, 0.055, 0.065)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    if OUTPUT_PREVIEW_SHA256 and (
        sha256(PREVIEW_PATH) != OUTPUT_PREVIEW_SHA256
        or PREVIEW_PATH.stat().st_size != OUTPUT_PREVIEW_BYTES
    ):
        raise RuntimeError(
            "Deterministic SCAR-L preview drifted: "
            f"sha256={sha256(PREVIEW_PATH)} bytes={PREVIEW_PATH.stat().st_size}"
        )
    if root.name not in bpy.context.scene.objects:
        raise RuntimeError("SCAR-L preview lost the runtime root.")


def require_unique_node(name: str) -> bpy.types.Object:
    matches = [obj for obj in bpy.context.scene.objects if obj.name == name]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one exported node {name!r}, found {len(matches)}")
    return matches[0]


def godot_mesh_bounds(
    root: bpy.types.Object,
    meshes: tuple[bpy.types.Object, ...],
) -> tuple[Vector, Vector]:
    inverse = root.matrix_world.inverted()
    return point_bounds(
        [
            blender_to_godot(inverse @ obj.matrix_world @ vertex.co)
            for obj in meshes for vertex in obj.data.vertices
        ]
    )


def validate_exported_asset(
    expected_sockets: dict[str, Vector],
) -> tuple[tuple[Vector, Vector], tuple[Vector, Vector], dict[str, int]]:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    root = require_unique_node(ROOT_NAME)
    magazine = require_unique_node("Magazine")
    spare_magazine = require_unique_node("SpareMagazine")
    action = require_unique_node("ChargingHandle")
    meshes = {name: require_unique_node(name) for name in GEOMETRY_NAMES}
    expected_nodes = {
        ROOT_NAME, "Magazine", "SpareMagazine", "ChargingHandle",
        *GEOMETRY_NAMES, *SOCKET_NAMES,
    }
    actual_nodes = {obj.name for obj in bpy.context.scene.objects}
    if actual_nodes != expected_nodes:
        raise RuntimeError(f"Exported SCAR-L node contract drifted: {sorted(actual_nodes)}")

    identity = Matrix.Identity(4)
    if any(
        abs(root.matrix_world[row][column] - identity[row][column])
        > IDENTITY_TOLERANCE
        for row in range(4) for column in range(4)
    ):
        raise RuntimeError("SCAR-L runtime root is not identity in metre space.")

    root_children = (
        "WeaponBodyGeometry", "FrontIronSight", "StockGeometry", "TriggerGeometry",
        "SafetyGeometry", "IronSightGeometry",
    )
    if any(meshes[name].parent != root for name in root_children):
        raise RuntimeError("SCAR-L fixed geometry hierarchy is invalid.")
    for name in root_children:
        if any(
            abs(meshes[name].matrix_local[row][column] - identity[row][column])
            > IDENTITY_TOLERANCE
            for row in range(4) for column in range(4)
        ):
            raise RuntimeError(f"SCAR-L fixed geometry {name} is not identity-local.")
    if meshes["MagazineGeometry"].parent != magazine:
        raise RuntimeError("SCAR-L installed magazine hierarchy is invalid.")
    if meshes["SpareMagazineGeometry"].parent != spare_magazine:
        raise RuntimeError("SCAR-L spare magazine hierarchy is invalid.")
    if any(meshes[name].parent != action for name in ("BoltGeometry", "ChargingHandleGeometry")):
        raise RuntimeError("SCAR-L authored action hierarchy is invalid.")
    if meshes["MagazineGeometry"].data == meshes["SpareMagazineGeometry"].data:
        raise RuntimeError("SCAR-L exported magazines share mesh data.")
    if not root.get("visible_action_geometry", False):
        raise RuntimeError("SCAR-L does not disclose visible authored action geometry.")

    counts = {name: len(mesh.data.polygons) for name, mesh in meshes.items()}
    if OUTPUT_NODE_TRIANGLES is not None and counts != OUTPUT_NODE_TRIANGLES:
        raise RuntimeError(f"Exported SCAR-L topology drifted: {counts}")
    for name in ("BoltGeometry", "ChargingHandleGeometry"):
        if counts[name] <= 0:
            raise RuntimeError(f"SCAR-L {name} has no visible triangles.")

    for name in SOCKET_NAMES:
        socket = require_unique_node(name)
        if name == "MagazineGripSocket":
            expected_parent = magazine
        elif name == "ChargingHandleSocket":
            expected_parent = action
        else:
            expected_parent = root
        if socket.parent != expected_parent:
            raise RuntimeError(f"SCAR-L socket {name} has invalid parent.")
        actual = blender_to_godot(root.matrix_world.inverted() @ socket.matrix_world.translation)
        if (actual - expected_sockets[name]).length > 0.0001:
            raise RuntimeError(
                f"SCAR-L socket {name} drifted: {tuple(actual)} "
                f"!= {tuple(expected_sockets[name])}"
            )

    if abs(expected_sockets["OpticRailSocket"].z - OPTIC_RAIL_Z) > 0.0001:
        raise RuntimeError("SCAR-L optic rail socket left the z=-0.25 contact zone.")
    installed = tuple(
        meshes[name] for name in GEOMETRY_NAMES if name != "SpareMagazineGeometry"
    )
    primary_bounds = godot_mesh_bounds(root, installed)
    scene_bounds = godot_mesh_bounds(root, tuple(meshes.values()))
    if EXPECTED_PRIMARY_MINIMUM is not None and EXPECTED_PRIMARY_MAXIMUM is not None:
        require_bounds(
            "body plus installed mechanisms", primary_bounds,
            (EXPECTED_PRIMARY_MINIMUM, EXPECTED_PRIMARY_MAXIMUM),
        )
    if EXPECTED_SCENE_MINIMUM is not None and EXPECTED_SCENE_MAXIMUM is not None:
        require_bounds(
            "full scene", scene_bounds,
            (EXPECTED_SCENE_MINIMUM, EXPECTED_SCENE_MAXIMUM),
        )
    if abs((primary_bounds[1].z - primary_bounds[0].z) - TARGET_LENGTH_METERS) > 0.0001:
        raise RuntimeError("SCAR-L metre length contract drifted.")
    return primary_bounds, scene_bounds, counts


def format_vector(vector: Vector) -> str:
    return "(" + ",".join(f"{value:.6f}" for value in vector) + ")"


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    root, sockets = build_runtime_asset()
    export_asset(root)
    save_editable_source()
    render_preview(root)
    primary_bounds, scene_bounds, counts = validate_exported_asset(sockets)
    print(
        "RELOADABLE_SCARL_EXPORT "
        f"source_sha256={SOURCE_SHA256} "
        f"glb_sha256={sha256(OUTPUT_GLB)} "
        f"blend_sha256={sha256(OUTPUT_BLEND)} "
        f"preview_sha256={sha256(PREVIEW_PATH)} "
        f"source_triangles={SOURCE_TRIANGLE_COUNT} "
        f"output_triangles={sum(counts.values())} "
        f"node_triangles={counts} "
        f"action_home={format_vector(sockets['ChargingHandleSocket'])} "
        f"rail_socket={format_vector(sockets['OpticRailSocket'])} "
        f"sockets={{{','.join(f'{name}:{format_vector(sockets[name])}' for name in SOCKET_NAMES)}}} "
        f"primary_bounds={format_vector(primary_bounds[0])}..{format_vector(primary_bounds[1])} "
        f"scene_bounds={format_vector(scene_bounds[0])}..{format_vector(scene_bounds[1])} "
        f"glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"blend_bytes={OUTPUT_BLEND.stat().st_size} "
        f"preview_bytes={PREVIEW_PATH.stat().st_size}"
    )


if __name__ == "__main__":
    main()
