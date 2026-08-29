"""Build authored reload mechanisms for the remaining Quaternius weapons.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup \
        --python scripts/blender/build_supplemental_reload_mechanisms.py

The finished CC0 weapon bodies remain unchanged except for safely separating
their authored P226 and M1911 slide components into real action pivots.
Missing magazines, internal loading components, and bolt handles are authored
here as custom lofted or swept Blender meshes with bevels, weighted normals,
and scalar PBR materials. No runtime primitive, CSG, or marker-only mechanism
is used for visible art.
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import closest_point_on_tri


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "assets" / "models" / "quaternius_ultimate_guns"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_reloadable_weapons"
SOURCE_ART_DIR = REPO_ROOT / "source_art" / "reloadable_weapons"

MAGAZINE_HOME_VALUES = (0.0, -0.20, -0.31)
SPARE_MAGAZINE_HOME_VALUES = (-0.30, -0.62, -0.18)
MAGAZINE_HOME = Vector(MAGAZINE_HOME_VALUES)
SPARE_MAGAZINE_HOME = Vector(SPARE_MAGAZINE_HOME_VALUES)
SPARE_MAGAZINE_DELTA = SPARE_MAGAZINE_HOME - MAGAZINE_HOME
ACTION_HOME = Vector((0.075, 0.085, -0.05))
TARGET_STOCK_Z = 0.32
SOURCE_CREATOR = "Quaternius"
SOURCE_URL = "https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F"
SOURCE_LICENSE = "CC0-1.0"
SOURCE_ACQUISITION_DATE = "2026-08-20"
ADAPTATION_DATE = "2026-08-29"


@dataclass(frozen=True)
class WeaponSpec:
    platform: str
    filename: str
    source_object: str
    source_sha256: str
    source_bytes: int
    source_triangles: int
    target_length: float
    mechanism: str
    magazine_width: float
    magazine_height: float
    magazine_depth: float
    magazine_curve: float
    action_kind: str


@dataclass(frozen=True)
class PistolSlideAudit:
    component_triangles: tuple[int, ...]
    main_component_triangles: int
    detail_component_triangles: int
    detail_minimum_y: float
    action_triangles: int
    body_triangles: int
    action_minimum: tuple[float, float, float]
    action_maximum: tuple[float, float, float]
    action_contact: tuple[float, float, float]


@dataclass(frozen=True)
class PistolMagazinePlacement:
    grip_component_triangles: int
    grip_component_count: int
    lateral_surface_mode: str
    lateral_minimum_x: float
    lateral_maximum_x: float
    pca_center_yz: tuple[float, float]
    pca_axis_yz: tuple[float, float]
    desired_top: tuple[float, float, float]
    rotation_x: float
    geometry_local_translation: tuple[float, float, float]
    socket_root: tuple[float, float, float]
    socket_magazine_local: tuple[float, float, float]
    installed_minimum: tuple[float, float, float]
    installed_maximum: tuple[float, float, float]


PISTOL_SLIDE_AUDITS = {
    "P226": PistolSlideAudit(
        (
            304, 196, 76, 72, 56, 28, 28, 28, 28, 20,
            12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        ),
        304,
        12,
        0.060,
        436,
        532,
        (-0.030161, 0.058990, -0.066914),
        (0.030161, 0.131769, 0.313653),
        (-0.030000, 0.096000, 0.238000),
    ),
    "M1911": PistolSlideAudit(
        (
            476, 192, 192, 168, 76, 60, 56, 36, 36, 36, 18,
            12, 12, 12, 12, 12, 12, 12, 12,
        ),
        476,
        12,
        0.120,
        572,
        870,
        (-0.032622, 0.036076, -0.069582),
        (0.032622, 0.140180, 0.313720),
        (-0.032000, 0.096000, 0.225000),
    ),
}

PISTOL_MAGAZINE_PLACEMENTS = {
    "P226": PistolMagazinePlacement(
        28,
        4,
        "outer",
        -0.028706759214,
        0.028706798330,
        (-0.059633532901, 0.213830570139),
        (-0.971051413752, 0.238870575523),
        (0.0, 0.030000000000, 0.191781466160),
        -0.241202589926,
        (0.0, 0.230000000000, 0.501781466160),
        (0.0, -0.049237795362, 0.211273305123),
        (0.0, 0.150762204638, 0.521273305123),
        (-0.025209999, -0.139753439, 0.171670992),
        (0.025209999, 0.034947009, 0.251392939),
    ),
    "M1911": PistolMagazinePlacement(
        192,
        2,
        "inner",
        -0.024678397924,
        0.024678425863,
        (-0.061318231464, 0.253674267513),
        (-0.935083697577, 0.354426972064),
        (0.0, 0.030000000000, 0.219061703573),
        -0.362301186800,
        (0.0, 0.230000000000, 0.529061703573),
        (0.0, -0.044058628848, 0.247132319760),
        (0.0, 0.155941371152, 0.557132319760),
        (-0.024120001, -0.130859882, 0.200715333),
        (0.024120001, 0.036953859, 0.294878572),
    ),
}

ACTION_TRAVEL = Vector((0.0, 0.0, 0.085))
PISTOL_BOUNDS_TOLERANCE = 0.0001
PISTOL_PCA_TOLERANCE = 0.000000001
PISTOL_SOURCE_SYMMETRY_TOLERANCE = 0.0000001
PISTOL_GLTF_POSE_TOLERANCE = 0.000002
PISTOL_MAGAZINE_BOUNDS_TOLERANCE = 0.00001
PISTOL_MAGWELL_MOUTH_Y = 0.030000000
PISTOL_MINIMUM_SIDE_CLEARANCE = 0.0004
PISTOL_MAXIMUM_BASEPLATE_EXPOSURE = 0.010
ACTION_ENDPOINT_X_TOLERANCE = 0.000002
ACTION_SURFACE_DISTANCE_TOLERANCE = 0.000001
ACTION_SOCKET_ROUND_TRIP_TOLERANCE = 0.000002

LONG_GUN_ACTION_SOCKET_AUDITS = {
    "M24": (-0.061999998987, -0.001527629793, 0.020006507635),
    "AXMC": (-0.061999998987, -0.001527629793, 0.020006507635),
    "AWM": (-0.061999998987, -0.001527629793, 0.020006507635),
    "VSS": (-0.061999998987, -0.001540400088, 0.038010638207),
}

# Reviewed metadata-free outputs, enforced after every build.
OUTPUT_AUDITS: dict[str, tuple[str, int, str, int]] = {
    "M24": (
        "F057513B5DF9B90A43D3129FEE7CBFEFBFBF3415C85C822B07ADB39979650454",
        165_308,
        "A699277406ADADDB6414DBCECE5483DDC3917220CF6D6F4E9D19353D569C06A7",
        810_858,
    ),
    "AXMC": (
        "872A4848F3B2D78E5923F4C9EE97D759A42E5CC445635B4D55D99FBBEB1F989D",
        122_880,
        "F0C1403D052F522501837FE58F5143FFC870BF9B3D0AC2E23ED138695E9C4DEB",
        815_182,
    ),
    "AWM": (
        "FFA2FE9DD07771650D55D60FAAC6715336ECD087D57373BA5C4139B3E0C73807",
        122_700,
        "72BAA67013EFD979DFC7CBD78D584929C86E0371794999293535FED2D5C2DBC9",
        832_830,
    ),
    "VSS": (
        "EAD6F895A66662F127949CA1F8A556873C627D9C0E39F472D57BE42882D21FBB",
        100_852,
        "B8F3650B19711F73D3692A17AB55D472EDD40FCF4B4F5FF60BAE16686F5543F0",
        817_219,
    ),
    "P226": (
        "579CB38E8F861ECAC5B7C7739946C4620046FFFAF94EE5E073CB69B913DB72FC",
        75_592,
        "DB6CE77A67DAB77502E818D6F01D670AF0193AD07CBE22DC30D949B325C4AD68",
        805_122,
    ),
    "M1911": (
        "08B5DC8D4ABC14B88B6728F2B4EB007284DA54ADC30D5295DBD703E5551D3C10",
        105_280,
        "A5371D16207493EF433A62F849601AD3A7CA76595E19499396CCE5A91DA87EEA",
        813_613,
    ),
}


SPECS = (
    WeaponSpec(
        "M24", "m24.glb", "SniperRifle_1",
        "A780E291A22BABE8C3472AE9FD0C0F4B98F22382E25C7DA3F5507A5761DFFC5B",
        76_652, 1_382, 1.74, "internal", 0.082, 0.030, 0.135, 0.0, "bolt",
    ),
    WeaponSpec(
        "AXMC", "axmc.glb", "SniperRifle_3",
        "7CDCE34DEC9A9B1AAE6C9E2EF554C88ECDC19554407DECC239B159E13D295F3F",
        95_356, 1_722, 1.74, "box", 0.100, 0.165, 0.145, 0.010, "bolt",
    ),
    WeaponSpec(
        "AWM", "awm.glb", "SniperRifle_5",
        "095E918BD89823B1CA726EAC0016D7C9DAEE15CC6F71010AB251FB0365819F02",
        95_200, 1_688, 2.00, "box", 0.102, 0.175, 0.150, 0.012, "bolt",
    ),
    WeaponSpec(
        "VSS", "vss.glb", "SniperRifle_4",
        "C69B8D4088176580819C20F44FC80D7742E6AD00BA1CA09CD064D8677B1C4BE5",
        74_692, 1_344, 1.58, "rock", 0.098, 0.205, 0.145, 0.045, "charging",
    ),
    WeaponSpec(
        "P226", "p226.glb", "Pistol_5",
        "4622AB2909AA0F4E88B74A13F52F9E28183A6FF5FCA5896FC7E98D44008F2148",
        53_776, 968, 0.40, "pistol", 0.047, 0.170, 0.038, 0.0, "slide",
    ),
    WeaponSpec(
        "M1911", "m1911.glb", "Pistol_3",
        "6DC98CF2E44DC8CD052E402D72B5FE21AF70AFBFC48A70F3139E22008991FD47",
        79_648, 1_442, 0.40, "pistol", 0.045, 0.165, 0.036, 0.0, "slide",
    ),
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


def godot_to_blender(position: Vector) -> Vector:
    return Vector((position.x, -position.z, position.y))


def blender_to_godot(position: Vector) -> Vector:
    return Vector((position.x, position.z, -position.y))


def point_bounds(points: list[Vector]) -> tuple[Vector, Vector]:
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def source_bounds(source: bpy.types.Object) -> tuple[Vector, Vector]:
    return point_bounds([source.matrix_world @ vertex.co for vertex in source.data.vertices])


def canonical_transform(
    minimum: Vector,
    maximum: Vector,
    target_length: float,
) -> Matrix:
    size = maximum - minimum
    if size.x <= 0.001:
        raise RuntimeError(f"Invalid source length {size.x:.6f}")
    center = (minimum + maximum) * 0.5
    scale = target_length / size.x
    target_center_z = TARGET_STOCK_Z - target_length * 0.5
    return Matrix(
        (
            (0.0, -scale, 0.0, scale * center.y),
            (scale, 0.0, 0.0, -scale * center.x - target_center_z),
            (0.0, 0.0, scale, -scale * center.z),
            (0.0, 0.0, 0.0, 1.0),
        )
    )


def import_body(spec: WeaponSpec) -> bpy.types.Object:
    source_path = SOURCE_DIR / spec.filename
    if source_path.stat().st_size != spec.source_bytes or sha256(source_path) != spec.source_sha256:
        raise RuntimeError(f"{spec.platform} source identity drifted")
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    source = bpy.data.objects.get(spec.source_object)
    if source is None or source.type != "MESH":
        raise RuntimeError(f"Missing {spec.platform} source mesh {spec.source_object}")
    if len(source.data.polygons) != spec.source_triangles:
        raise RuntimeError(f"{spec.platform} source topology drifted")
    minimum, maximum = source_bounds(source)
    source.data.transform(
        canonical_transform(minimum, maximum, spec.target_length) @ source.matrix_world
    )
    source.matrix_world = Matrix.Identity(4)
    source.name = "WeaponBodyGeometry"
    source.data.name = f"{spec.platform}WeaponBodyMesh"
    source["source_creator"] = SOURCE_CREATOR
    source["source_url"] = SOURCE_URL
    source["source_license"] = SOURCE_LICENSE
    source["source_sha256"] = spec.source_sha256
    return source


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


def component_godot_bounds(
    obj: bpy.types.Object,
    component: set[int],
) -> tuple[Vector, Vector]:
    return point_bounds(
        [
            blender_to_godot(obj.data.vertices[vertex].co)
            for face in component
            for vertex in obj.data.polygons[face].vertices
        ]
    )


def mesh_godot_bounds(
    root: bpy.types.Object,
    obj: bpy.types.Object,
) -> tuple[Vector, Vector]:
    inverse = root.matrix_world.inverted()
    return point_bounds(
        [
            blender_to_godot(inverse @ obj.matrix_world @ vertex.co)
            for vertex in obj.data.vertices
        ]
    )


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
        raise RuntimeError(f"{obj.name} has no action surface triangles")
    return minimum_distance


def audit_terminal_action_socket(
    geometry: bpy.types.Object,
    action: bpy.types.Object,
    platform: str,
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
        raise RuntimeError(f"{platform} action has no terminal surface region")
    terminal_maximum_y = max(point.y for point in terminal_points)
    if (
        abs(socket_local.x - minimum.x) > ACTION_ENDPOINT_X_TOLERANCE
        or terminal_maximum_y - socket_local.y > ACTION_ENDPOINT_X_TOLERANCE
        or socket_local.length < 0.045
    ):
        raise RuntimeError(
            f"{platform} action socket left the outer terminal region: "
            f"socket={tuple(socket_local)} terminal_x={minimum.x:.9f} "
            f"terminal_y={terminal_maximum_y:.9f}"
        )
    expected = Vector(LONG_GUN_ACTION_SOCKET_AUDITS[platform])
    require_vector(
        f"{platform} terminal action socket",
        socket_local,
        expected,
        ACTION_SOCKET_ROUND_TRIP_TOLERANCE,
    )
    surface_distance = point_to_mesh_surface_distance(
        action,
        geometry,
        socket_local,
    )
    if surface_distance > ACTION_SURFACE_DISTANCE_TOLERANCE:
        raise RuntimeError(
            f"{platform} action socket is {surface_distance:.9f} m "
            "from the visible terminal surface"
        )
    return surface_distance


def derive_terminal_action_socket(
    geometry: bpy.types.Object,
    action: bpy.types.Object,
    platform: str,
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
    audit_terminal_action_socket(
        geometry,
        action,
        platform,
        socket_local,
    )
    return socket_local


def require_bounds(
    label: str,
    actual: tuple[Vector, Vector],
    expected: tuple[Vector, Vector],
    tolerance: float = PISTOL_BOUNDS_TOLERANCE,
) -> None:
    for bound_name, actual_bound, expected_bound in zip(
        ("minimum", "maximum"), actual, expected
    ):
        if (actual_bound - expected_bound).length > tolerance:
            raise RuntimeError(
                f"{label} {bound_name} drifted: "
                f"{tuple(actual_bound)} != {tuple(expected_bound)} "
                f"(tolerance {tolerance:.12f})"
            )


def require_value(label: str, actual: float, expected: float, tolerance: float) -> None:
    if abs(actual - expected) > tolerance:
        raise RuntimeError(
            f"{label} drifted: {actual:.12f} != {expected:.12f} "
            f"(tolerance {tolerance:.12f})"
        )


def require_vector(
    label: str,
    actual: Vector,
    expected: Vector,
    tolerance: float,
) -> None:
    if (actual - expected).length > tolerance:
        raise RuntimeError(
            f"{label} drifted: {tuple(actual)} != {tuple(expected)} "
            f"(tolerance {tolerance:.12f})"
        )


def require_tuple_values(
    label: str,
    actual: tuple[float, ...],
    expected: tuple[float, ...],
    tolerance: float,
) -> None:
    if len(actual) != len(expected):
        raise RuntimeError(f"{label} component count drifted")
    for index, (actual_value, expected_value) in enumerate(zip(actual, expected)):
        require_value(
            f"{label} component {index}",
            actual_value,
            expected_value,
            tolerance,
        )


def rotate_godot_x(vector: Vector, angle: float) -> Vector:
    cosine = math.cos(angle)
    sine = math.sin(angle)
    return Vector(
        (
            vector.x,
            cosine * vector.y - sine * vector.z,
            sine * vector.y + cosine * vector.z,
        )
    )


def derive_pistol_magazine_placement(
    body: bpy.types.Object,
    spec: WeaponSpec,
) -> tuple[Vector, float, Vector, Vector, Vector]:
    audit = PISTOL_MAGAZINE_PLACEMENTS[spec.platform]
    grip_components = [
        component
        for component in welded_face_components(body)
        if len(component) == audit.grip_component_triangles
    ]
    if len(grip_components) != audit.grip_component_count:
        raise RuntimeError(
            f"{spec.platform} grip PCA component routing drifted: "
            f"{len(grip_components)} != {audit.grip_component_count}"
        )

    vertex_indices = {
        vertex
        for component in grip_components
        for face in component
        for vertex in body.data.polygons[face].vertices
    }
    points = [
        blender_to_godot(body.data.vertices[index].co)
        for index in sorted(vertex_indices)
    ]
    if not points:
        raise RuntimeError(f"{spec.platform} grip PCA has no source vertices")

    negative_x = [point.x for point in points if point.x < 0.0]
    positive_x = [point.x for point in points if point.x > 0.0]
    if not negative_x or not positive_x:
        raise RuntimeError(f"{spec.platform} grip lacks bilateral source surfaces")
    if audit.lateral_surface_mode == "outer":
        lateral_minimum_x = min(negative_x)
        lateral_maximum_x = max(positive_x)
    elif audit.lateral_surface_mode == "inner":
        lateral_minimum_x = max(negative_x)
        lateral_maximum_x = min(positive_x)
    else:
        raise RuntimeError(
            f"{spec.platform} unknown lateral surface mode "
            f"{audit.lateral_surface_mode}"
        )
    require_value(
        f"{spec.platform} source magwell left surface",
        lateral_minimum_x,
        audit.lateral_minimum_x,
        PISTOL_PCA_TOLERANCE,
    )
    require_value(
        f"{spec.platform} source magwell right surface",
        lateral_maximum_x,
        audit.lateral_maximum_x,
        PISTOL_PCA_TOLERANCE,
    )

    inverse_count = 1.0 / len(points)
    center_x = sum(point.x for point in points) * inverse_count
    center_y = sum(point.y for point in points) * inverse_count
    center_z = sum(point.z for point in points) * inverse_count
    require_value(
        f"{spec.platform} grip PCA symmetry center X",
        center_x,
        0.0,
        PISTOL_SOURCE_SYMMETRY_TOLERANCE,
    )

    covariance_yy = sum((point.y - center_y) ** 2 for point in points) * inverse_count
    covariance_yz = sum(
        (point.y - center_y) * (point.z - center_z)
        for point in points
    ) * inverse_count
    covariance_zz = sum((point.z - center_z) ** 2 for point in points) * inverse_count
    discriminant = math.sqrt(
        (covariance_yy - covariance_zz) ** 2
        + 4.0 * covariance_yz * covariance_yz
    )
    eigenvalue = (covariance_yy + covariance_zz + discriminant) * 0.5
    axis_y = covariance_yz
    axis_z = eigenvalue - covariance_yy
    axis_length = math.hypot(axis_y, axis_z)
    if axis_length <= 0.000000000001:
        axis_y = eigenvalue - covariance_zz
        axis_z = covariance_yz
        axis_length = math.hypot(axis_y, axis_z)
    if axis_length <= 0.000000000001:
        raise RuntimeError(f"{spec.platform} grip PCA principal axis is degenerate")
    axis_y /= axis_length
    axis_z /= axis_length
    if axis_y > 0.0:
        axis_y = -axis_y
        axis_z = -axis_z

    expected_center_y, expected_center_z = audit.pca_center_yz
    expected_axis_y, expected_axis_z = audit.pca_axis_yz
    require_value(
        f"{spec.platform} grip PCA center Y",
        center_y,
        expected_center_y,
        PISTOL_PCA_TOLERANCE,
    )
    require_value(
        f"{spec.platform} grip PCA center Z",
        center_z,
        expected_center_z,
        PISTOL_PCA_TOLERANCE,
    )
    require_value(
        f"{spec.platform} grip PCA axis Y",
        axis_y,
        expected_axis_y,
        PISTOL_PCA_TOLERANCE,
    )
    require_value(
        f"{spec.platform} grip PCA axis Z",
        axis_z,
        expected_axis_z,
        PISTOL_PCA_TOLERANCE,
    )

    # Y=30 mm is the reviewed DCC mouth plane: it sits 2.088 mm inside the
    # M1911 receiver above its grip panels and safely inside the P226 frame.
    travel = (PISTOL_MAGWELL_MOUTH_Y - center_y) / axis_y
    desired_top_values = (
        0.0,
        PISTOL_MAGWELL_MOUTH_Y,
        center_z + axis_z * travel,
    )
    rotation_x = -math.atan2(axis_z, -axis_y)
    geometry_local_values = (
        desired_top_values[0] - MAGAZINE_HOME_VALUES[0],
        desired_top_values[1] - MAGAZINE_HOME_VALUES[1],
        desired_top_values[2] - MAGAZINE_HOME_VALUES[2],
    )
    contact_distance = -spec.magazine_height * 0.48
    socket_root_values = (
        0.0,
        desired_top_values[1] + math.cos(rotation_x) * contact_distance,
        desired_top_values[2] + math.sin(rotation_x) * contact_distance,
    )
    socket_local_values = (
        socket_root_values[0] - MAGAZINE_HOME_VALUES[0],
        socket_root_values[1] - MAGAZINE_HOME_VALUES[1],
        socket_root_values[2] - MAGAZINE_HOME_VALUES[2],
    )

    require_tuple_values(
        f"{spec.platform} PCA-derived magazine top",
        desired_top_values,
        audit.desired_top,
        PISTOL_PCA_TOLERANCE,
    )
    require_value(
        f"{spec.platform} PCA-derived magazine pitch",
        rotation_x,
        audit.rotation_x,
        PISTOL_PCA_TOLERANCE,
    )
    require_tuple_values(
        f"{spec.platform} magazine geometry local translation",
        geometry_local_values,
        audit.geometry_local_translation,
        PISTOL_PCA_TOLERANCE,
    )
    require_tuple_values(
        f"{spec.platform} magazine contact in root",
        socket_root_values,
        audit.socket_root,
        PISTOL_PCA_TOLERANCE,
    )
    require_tuple_values(
        f"{spec.platform} magazine socket local translation",
        socket_local_values,
        audit.socket_magazine_local,
        PISTOL_PCA_TOLERANCE,
    )
    return (
        Vector(desired_top_values),
        rotation_x,
        Vector(geometry_local_values),
        Vector(socket_root_values),
        Vector(socket_local_values),
    )


def separate_authored_pistol_slide(
    body: bpy.types.Object,
    spec: WeaponSpec,
) -> bpy.types.Object:
    audit = PISTOL_SLIDE_AUDITS[spec.platform]
    components = welded_face_components(body)
    component_sizes = tuple(len(component) for component in components)
    if component_sizes != audit.component_triangles:
        raise RuntimeError(
            f"{spec.platform} welded component audit drifted: "
            f"{component_sizes} != {audit.component_triangles}"
        )

    moving_components: list[set[int]] = []
    for component in components:
        minimum, _ = component_godot_bounds(body, component)
        is_main_slide = len(component) == audit.main_component_triangles
        is_slide_detail = (
            len(component) == audit.detail_component_triangles
            and minimum.y >= audit.detail_minimum_y
        )
        if is_main_slide or is_slide_detail:
            moving_components.append(component)
    moving_faces = set().union(*moving_components)
    if len(moving_faces) != audit.action_triangles:
        raise RuntimeError(
            f"{spec.platform} authored slide selection drifted: "
            f"{len(moving_faces)} != {audit.action_triangles}"
        )

    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in body.data.polygons:
        polygon.select = polygon.index in moving_faces
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    separated = [
        obj
        for obj in bpy.context.selected_objects
        if obj.type == "MESH" and obj != body
    ]
    if len(separated) != 1:
        raise RuntimeError(
            f"Expected one separated {spec.platform} slide, found {len(separated)}"
        )
    slide = separated[0]
    slide.name = "ChargingHandleGeometry"
    slide.data.name = f"{spec.platform}AuthoredSlideMesh"
    slide["runtime_asset"] = True
    slide["mechanism_role"] = "complete_empty_reload_slide"
    slide["source_creator"] = SOURCE_CREATOR
    slide["source_license"] = SOURCE_LICENSE
    slide["source_sha256"] = spec.source_sha256
    slide["source_component_triangles"] = ",".join(
        str(len(component)) for component in moving_components
    )
    slide["visible_action_geometry"] = True
    body.data.name = f"{spec.platform}FixedWeaponBodyMesh"

    if (
        len(body.data.polygons) != audit.body_triangles
        or len(slide.data.polygons) != audit.action_triangles
        or len(body.data.polygons) + len(slide.data.polygons) != spec.source_triangles
    ):
        raise RuntimeError(
            f"{spec.platform} source triangle conservation failed after slide split"
        )
    require_bounds(
        f"{spec.platform} authored slide",
        component_godot_bounds(slide, set(range(len(slide.data.polygons)))),
        (Vector(audit.action_minimum), Vector(audit.action_maximum)),
    )
    return slide


def mechanism_material(name: str, color: tuple[float, float, float, float], metallic: float, roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    material.use_nodes = True
    material["dcc_authored"] = True
    material["scalar_pbr_only"] = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Material {name} lacks Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def finish_mesh(obj: bpy.types.Object, bevel_width: float) -> None:
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("AuthoredEdgeBreak", "BEVEL")
    bevel.width = bevel_width
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(24.0)
    bevel.harden_normals = True
    weighted = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.object.modifier_apply(modifier=weighted.name)
    obj.select_set(False)


def new_empty(name: str, parent: bpy.types.Object, position: Vector, role: str) -> bpy.types.Object:
    node = bpy.data.objects.new(name, None)
    node.empty_display_type = "PLAIN_AXES"
    node.empty_display_size = 0.028
    node.location = godot_to_blender(position)
    node["runtime_asset"] = True
    node["mechanism_role"] = role
    bpy.context.collection.objects.link(node)
    node.parent = parent
    return node


def rounded_outline(width: float, depth: float) -> tuple[tuple[float, float], ...]:
    corner = min(width, depth) * 0.18
    half_width = width * 0.5
    half_depth = depth * 0.5
    return (
        (-half_width + corner, -half_depth),
        (half_width - corner, -half_depth),
        (half_width, -half_depth + corner),
        (half_width, half_depth - corner),
        (half_width - corner, half_depth),
        (-half_width + corner, half_depth),
        (-half_width, half_depth - corner),
        (-half_width, -half_depth + corner),
    )


def create_lofted_magazine(spec: WeaponSpec, name: str) -> bpy.types.Object:
    outline = rounded_outline(spec.magazine_width, spec.magazine_depth)
    levels = (
        (0.0, 1.00, 0.0),
        (-spec.magazine_height * 0.08, 1.00, spec.magazine_curve * 0.02),
        (-spec.magazine_height * 0.46, 0.96, spec.magazine_curve * 0.28),
        (-spec.magazine_height * 0.88, 0.90, spec.magazine_curve * 0.78),
        (-spec.magazine_height, 0.94, spec.magazine_curve),
    )
    vertices: list[tuple[float, float, float]] = []
    for y, scale, curve in levels:
        for x, z in outline:
            point = Vector((x * scale, y, z * scale + curve))
            vertices.append(tuple(godot_to_blender(point)))
    faces: list[tuple[int, ...]] = []
    side_count = len(outline)
    for level in range(len(levels) - 1):
        first = level * side_count
        second = (level + 1) * side_count
        for side in range(side_count):
            following = (side + 1) % side_count
            faces.append((first + side, second + side, second + following, first + following))
    faces.append(tuple(reversed(range(side_count))))
    last = (len(levels) - 1) * side_count
    faces.append(tuple(last + side for side in range(side_count)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(
        mechanism_material(
            f"{spec.platform}MagazineSteel",
            (0.028, 0.034, 0.038, 1.0),
            0.78,
            0.30,
        )
    )
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj["dcc_method"] = "custom five-section tapered hard-surface loft"
    finish_mesh(obj, min(spec.magazine_width, spec.magazine_depth) * 0.045)
    return obj


def create_cartridge_bundle(spec: WeaponSpec, name: str) -> bpy.types.Object:
    material = mechanism_material(
        f"{spec.platform}CartridgeBrass",
        (0.42, 0.25, 0.055, 1.0),
        0.68,
        0.24,
    )
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    sides = 12
    # Five .308-style cartridges in a shallow stripper/hand loading fan.
    for cartridge in range(5):
        x_offset = (cartridge - 2) * 0.014
        z_offset = abs(cartridge - 2) * 0.004
        profile = (
            (0.0, 0.0048),
            (0.050, 0.0052),
            (0.058, 0.0045),
            (0.071, 0.0032),
            (0.080, 0.0008),
        )
        base = len(vertices)
        for y, radius in profile:
            for side in range(sides):
                angle = math.tau * side / sides
                point = Vector(
                    (
                        x_offset + math.cos(angle) * radius,
                        -y,
                        z_offset + math.sin(angle) * radius,
                    )
                )
                vertices.append(tuple(godot_to_blender(point)))
        for level in range(len(profile) - 1):
            first = base + level * sides
            second = first + sides
            for side in range(sides):
                following = (side + 1) % sides
                faces.append((first + side, second + side, second + following, first + following))
        faces.append(tuple(reversed([base + side for side in range(sides)])))
        tip = base + (len(profile) - 1) * sides
        faces.append(tuple(tip + side for side in range(sides)))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj["dcc_method"] = "five custom lathed ogive cartridge profiles"
    finish_mesh(obj, 0.00035)
    return obj


def create_swept_action(spec: WeaponSpec, name: str) -> bpy.types.Object:
    if spec.action_kind == "slide":
        raise RuntimeError(
            f"{spec.platform} must use its complete separated Quaternius slide; "
            "a supplemental cap is not an acceptable pistol action."
        )

    points = (
        Vector((0.0, 0.0, 0.0)),
        Vector((-0.018, 0.0, 0.004)),
        Vector((-0.042, -0.006, 0.010)),
        Vector((-0.062, -0.015, 0.020)),
    )
    if spec.action_kind == "charging":
        points = tuple(point + Vector((0.0, 0.0, index * 0.006)) for index, point in enumerate(points))
    sides = 12
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for index, center in enumerate(points):
        radius = 0.007 + index * 0.0022
        for side in range(sides):
            angle = math.tau * side / sides
            point = center + Vector((0.0, math.cos(angle) * radius, math.sin(angle) * radius))
            vertices.append(tuple(godot_to_blender(point)))
    for section in range(len(points) - 1):
        first = section * sides
        second = (section + 1) * sides
        for side in range(sides):
            following = (side + 1) % sides
            faces.append((first + side, second + side, second + following, first + following))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(
        mechanism_material(
            f"{spec.platform}ActionSteel",
            (0.022, 0.026, 0.030, 1.0),
            0.86,
            0.22,
        )
    )
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj["dcc_method"] = "custom four-ring swept bolt-handle profile"
    finish_mesh(obj, 0.0008)
    return obj


def parent_geometry_at_pivot(
    geometry: bpy.types.Object,
    pivot: bpy.types.Object,
    preserve_weapon_root_position: bool = False,
) -> None:
    geometry.parent = pivot
    geometry.matrix_parent_inverse = Matrix.Identity(4)
    geometry.location = -pivot.location if preserve_weapon_root_position else Vector()
    geometry["runtime_asset"] = True


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def build_asset(spec: WeaponSpec) -> tuple[bpy.types.Object, dict[str, int]]:
    clear_scene()
    body = import_body(spec)
    authored_slide = (
        separate_authored_pistol_slide(body, spec)
        if spec.platform in PISTOL_SLIDE_AUDITS
        else None
    )
    pistol_magazine_pose = (
        derive_pistol_magazine_placement(body, spec)
        if spec.platform in PISTOL_MAGAZINE_PLACEMENTS
        else None
    )
    root = bpy.data.objects.new(f"SteelTideReloadable{spec.platform}", None)
    bpy.context.collection.objects.link(root)
    root["weapon_platform"] = spec.platform
    root["source_creator"] = SOURCE_CREATOR
    root["source_url"] = SOURCE_URL
    root["source_license"] = SOURCE_LICENSE
    root["source_sha256"] = spec.source_sha256
    root["source_acquisition_date"] = SOURCE_ACQUISITION_DATE
    root["adaptation_date"] = ADAPTATION_DATE
    root["visible_action_geometry"] = True
    root["coordinate_contract"] = "Godot metres: X lateral, Y up, Z stock-to-muzzle"
    root["reload_mechanism"] = spec.mechanism
    if authored_slide is not None:
        root["action_geometry_source"] = "Quaternius CC0 authored complete pistol slide"
        root["source_slide_separated_without_duplication"] = True
        root["action_travel_m"] = ACTION_TRAVEL.length
    if pistol_magazine_pose is not None:
        desired_top, rotation_x, _, socket_root, _ = pistol_magazine_pose
        root["magazine_alignment_source"] = "source grip component PCA"
        root["magazine_mouth_plane_y_m"] = PISTOL_MAGWELL_MOUTH_Y
        root["magazine_top_godot_m"] = tuple(desired_top)
        root["magazine_pitch_x_rad"] = rotation_x
        root["magazine_contact_godot_m"] = tuple(socket_root)
    body.parent = root

    magazine = new_empty("Magazine", root, MAGAZINE_HOME, "installed_loading_component")
    spare = new_empty("SpareMagazine", root, SPARE_MAGAZINE_HOME, "replacement_loading_component")
    if spec.mechanism == "internal":
        primary_geometry = create_lofted_magazine(spec, "MagazineGeometry")
        spare_geometry = create_cartridge_bundle(spec, "SpareMagazineGeometry")
    else:
        primary_geometry = create_lofted_magazine(spec, "MagazineGeometry")
        spare_geometry = primary_geometry.copy()
        spare_geometry.data = primary_geometry.data.copy()
        spare_geometry.name = "SpareMagazineGeometry"
        spare_geometry.data.name = f"{spec.platform}SpareMagazineMesh"
        bpy.context.collection.objects.link(spare_geometry)
    parent_geometry_at_pivot(primary_geometry, magazine)
    parent_geometry_at_pivot(spare_geometry, spare)
    if pistol_magazine_pose is not None:
        _, rotation_x, geometry_local_translation, _, socket_local = (
            pistol_magazine_pose
        )
        for geometry in (primary_geometry, spare_geometry):
            geometry.location = godot_to_blender(geometry_local_translation)
            geometry.rotation_euler.x = rotation_x
    else:
        socket_local = Vector((0.0, -spec.magazine_height * 0.48, 0.0))
    new_empty(
        "MagazineGripSocket",
        magazine,
        socket_local,
        "magazine_hand_contact",
    )

    action = new_empty("ChargingHandle", root, ACTION_HOME, "action_pivot")
    action_geometry = (
        authored_slide
        if authored_slide is not None
        else create_swept_action(spec, "ChargingHandleGeometry")
    )
    parent_geometry_at_pivot(
        action_geometry,
        action,
        preserve_weapon_root_position=authored_slide is not None,
    )
    if authored_slide is not None:
        action_contact = (
            Vector(PISTOL_SLIDE_AUDITS[spec.platform].action_contact)
            - ACTION_HOME
        )
    else:
        bpy.context.view_layer.update()
        action_contact = derive_terminal_action_socket(
            action_geometry,
            action,
            spec.platform,
        )
        root["action_socket_source"] = "visible outer terminal surface"
        root["action_socket_local_godot_m"] = tuple(action_contact)
    new_empty(
        "ChargingHandleSocket",
        action,
        action_contact,
        "action_hand_contact",
    )

    keep = {root, *root.children_recursive}
    for obj in list(bpy.context.scene.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    counts = {
        obj.name: triangle_count(obj)
        for obj in root.children_recursive
        if obj.type == "MESH"
    }
    expected_body_triangles = (
        PISTOL_SLIDE_AUDITS[spec.platform].body_triangles
        if authored_slide is not None
        else spec.source_triangles
    )
    if counts.get("WeaponBodyGeometry") != expected_body_triangles:
        raise RuntimeError(f"{spec.platform} body topology was not conserved")
    if not counts.get("MagazineGeometry") or not counts.get("SpareMagazineGeometry"):
        raise RuntimeError(f"{spec.platform} loading geometry is empty")
    if not counts.get("ChargingHandleGeometry"):
        raise RuntimeError(f"{spec.platform} action geometry is empty")
    if authored_slide is not None:
        audit = PISTOL_SLIDE_AUDITS[spec.platform]
        if (
            counts["ChargingHandleGeometry"] != audit.action_triangles
            or counts["WeaponBodyGeometry"] + counts["ChargingHandleGeometry"]
            != spec.source_triangles
        ):
            raise RuntimeError(
                f"{spec.platform} authored slide did not conserve source triangles"
            )
    return root, counts


def export_asset(root: bpy.types.Object, spec: WeaponSpec) -> Path:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    output = OUTPUT_DIR / f"{spec.platform.lower()}_reloadable.glb"
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (root, *root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(output),
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
    return output


def save_source(spec: WeaponSpec) -> Path:
    SOURCE_ART_DIR.mkdir(parents=True, exist_ok=True)
    output = SOURCE_ART_DIR / f"{spec.platform.lower()}_reloadable.blend"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    return output


def render_preview(root: bpy.types.Object, spec: WeaponSpec) -> Path:
    preview = OUTPUT_DIR / f"{spec.platform.lower()}_reloadable_preview.png"
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
    is_pistol_slide = spec.platform in PISTOL_SLIDE_AUDITS
    target = (
        Vector((-0.12, -0.05, -0.23))
        if is_pistol_slide
        else Vector((0.0, 0.05, 0.02))
    )
    camera.location = (
        Vector((0.82, -1.18, 0.48))
        if is_pistol_slide
        else Vector((1.45, -2.65, 1.05))
    )
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = (
        1.35 if is_pistol_slide else max(0.75, spec.target_length * 1.08)
    )
    scene.camera = camera
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 675
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.use_stamp = False
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
    scene.render.filepath = str(preview)
    scene.world.color = (0.045, 0.055, 0.065)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    if root.name not in bpy.context.scene.objects:
        raise RuntimeError(f"{spec.platform} preview lost its runtime root")
    return preview


def validate_export(
    output: Path,
    spec: WeaponSpec,
    expected_counts: dict[str, int],
) -> float | None:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(output))
    required = {
        f"SteelTideReloadable{spec.platform}",
        "WeaponBodyGeometry",
        "Magazine",
        "MagazineGeometry",
        "SpareMagazine",
        "SpareMagazineGeometry",
        "ChargingHandle",
        "ChargingHandleGeometry",
        "MagazineGripSocket",
        "ChargingHandleSocket",
    }
    actual = {obj.name for obj in bpy.context.scene.objects}
    if actual != required:
        raise RuntimeError(f"{spec.platform} node contract drifted: {sorted(actual)}")
    actual_counts = {
        obj.name: triangle_count(obj)
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
    }
    if actual_counts != expected_counts:
        raise RuntimeError(
            f"{spec.platform} exported topology drifted: {actual_counts} != {expected_counts}"
        )
    action_surface_distance: float | None = None
    root = bpy.data.objects[f"SteelTideReloadable{spec.platform}"]
    body = bpy.data.objects["WeaponBodyGeometry"]
    magazine = bpy.data.objects["Magazine"]
    spare = bpy.data.objects["SpareMagazine"]
    action = bpy.data.objects["ChargingHandle"]
    action_geometry = bpy.data.objects["ChargingHandleGeometry"]
    magazine_geometry = bpy.data.objects["MagazineGeometry"]
    spare_geometry = bpy.data.objects["SpareMagazineGeometry"]
    magazine_socket = bpy.data.objects["MagazineGripSocket"]
    action_socket = bpy.data.objects["ChargingHandleSocket"]
    if (
        root.parent is not None
        or root.matrix_world != Matrix.Identity(4)
        or body.parent != root
        or magazine.parent != root
        or spare.parent != root
        or action.parent != root
        or magazine_geometry.parent != magazine
        or spare_geometry.parent != spare
        or action_geometry.parent != action
        or magazine_socket.parent != magazine
        or action_socket.parent != action
    ):
        raise RuntimeError(f"{spec.platform} exported mechanism hierarchy drifted")

    root_inverse = root.matrix_world.inverted()
    magazine_home = blender_to_godot(
        root_inverse @ magazine.matrix_world.translation
    )
    spare_home = blender_to_godot(root_inverse @ spare.matrix_world.translation)
    require_vector(
        f"{spec.platform} shared magazine pivot rest",
        magazine_home,
        MAGAZINE_HOME,
        PISTOL_GLTF_POSE_TOLERANCE,
    )
    require_vector(
        f"{spec.platform} shared spare pivot rest",
        spare_home,
        SPARE_MAGAZINE_HOME,
        PISTOL_GLTF_POSE_TOLERANCE,
    )
    require_vector(
        f"{spec.platform} shared spare pivot delta",
        spare_home - magazine_home,
        SPARE_MAGAZINE_DELTA,
        PISTOL_GLTF_POSE_TOLERANCE,
    )

    if spec.platform in PISTOL_MAGAZINE_PLACEMENTS:
        magazine_audit = PISTOL_MAGAZINE_PLACEMENTS[spec.platform]
        desired_top = Vector(magazine_audit.desired_top)
        installed_origin = blender_to_godot(
            root_inverse @ magazine_geometry.matrix_world.translation
        )
        spare_origin = blender_to_godot(
            root_inverse @ spare_geometry.matrix_world.translation
        )
        socket_root = blender_to_godot(
            root_inverse @ magazine_socket.matrix_world.translation
        )
        require_vector(
            f"{spec.platform} installed magazine top after glTF round trip",
            installed_origin,
            desired_top,
            PISTOL_GLTF_POSE_TOLERANCE,
        )
        require_vector(
            f"{spec.platform} spare magazine top after glTF round trip",
            spare_origin,
            desired_top + SPARE_MAGAZINE_DELTA,
            PISTOL_GLTF_POSE_TOLERANCE,
        )
        require_vector(
            f"{spec.platform} magazine socket after glTF round trip",
            socket_root,
            Vector(magazine_audit.socket_root),
            PISTOL_GLTF_POSE_TOLERANCE,
        )

        expected_down = rotate_godot_x(
            Vector((0.0, -1.0, 0.0)),
            magazine_audit.rotation_x,
        )
        for label, geometry in (
            ("installed", magazine_geometry),
            ("spare", spare_geometry),
        ):
            relative_basis = (
                root_inverse @ geometry.matrix_world
            ).to_3x3()
            actual_down = blender_to_godot(
                relative_basis @ godot_to_blender(Vector((0.0, -1.0, 0.0)))
            ).normalized()
            require_vector(
                f"{spec.platform} {label} magazine PCA pitch",
                actual_down,
                expected_down,
                PISTOL_GLTF_POSE_TOLERANCE,
            )

        expected_installed_bounds = (
            Vector(magazine_audit.installed_minimum),
            Vector(magazine_audit.installed_maximum),
        )
        actual_installed_bounds = mesh_godot_bounds(root, magazine_geometry)
        require_bounds(
            f"{spec.platform} installed magazine bounds",
            actual_installed_bounds,
            expected_installed_bounds,
            PISTOL_MAGAZINE_BOUNDS_TOLERANCE,
        )
        require_bounds(
            f"{spec.platform} spare magazine bounds",
            mesh_godot_bounds(root, spare_geometry),
            (
                expected_installed_bounds[0] + SPARE_MAGAZINE_DELTA,
                expected_installed_bounds[1] + SPARE_MAGAZINE_DELTA,
            ),
            PISTOL_MAGAZINE_BOUNDS_TOLERANCE,
        )

        left_clearance = (
            actual_installed_bounds[0].x - magazine_audit.lateral_minimum_x
        )
        right_clearance = (
            magazine_audit.lateral_maximum_x - actual_installed_bounds[1].x
        )
        if min(left_clearance, right_clearance) < PISTOL_MINIMUM_SIDE_CLEARANCE:
            raise RuntimeError(
                f"{spec.platform} magazine lacks bilateral magwell clearance: "
                f"left={left_clearance:.9f} right={right_clearance:.9f}"
            )
        body_bounds = mesh_godot_bounds(root, body)
        baseplate_exposure = max(
            0.0,
            body_bounds[0].y - actual_installed_bounds[0].y,
        )
        if baseplate_exposure > PISTOL_MAXIMUM_BASEPLATE_EXPOSURE:
            raise RuntimeError(
                f"{spec.platform} magazine protrudes too far below its grip: "
                f"{baseplate_exposure:.9f} m"
            )
        if (
            actual_installed_bounds[1].y
            > body_bounds[1].y + PISTOL_MAGAZINE_BOUNDS_TOLERANCE
            or actual_installed_bounds[0].z
            < body_bounds[0].z - PISTOL_MAGAZINE_BOUNDS_TOLERANCE
            or actual_installed_bounds[1].z
            > body_bounds[1].z + PISTOL_MAGAZINE_BOUNDS_TOLERANCE
        ):
            raise RuntimeError(
                f"{spec.platform} installed magazine left the authored grip envelope"
            )

    if spec.platform in LONG_GUN_ACTION_SOCKET_AUDITS:
        if root.get("action_socket_source") != "visible outer terminal surface":
            raise RuntimeError(
                f"{spec.platform} does not disclose its terminal action socket"
            )
        action_socket_local = blender_to_godot(
            action.matrix_world.inverted()
            @ action_socket.matrix_world.translation
        )
        action_surface_distance = audit_terminal_action_socket(
            action_geometry,
            action,
            spec.platform,
            action_socket_local,
        )
        if action_surface_distance > ACTION_SURFACE_DISTANCE_TOLERANCE:
            raise RuntimeError(
                f"{spec.platform} action socket surface round trip failed"
            )

    if spec.platform in PISTOL_SLIDE_AUDITS:
        audit = PISTOL_SLIDE_AUDITS[spec.platform]
        if (
            actual_counts["WeaponBodyGeometry"] != audit.body_triangles
            or actual_counts["ChargingHandleGeometry"] != audit.action_triangles
            or actual_counts["WeaponBodyGeometry"]
            + actual_counts["ChargingHandleGeometry"]
            != spec.source_triangles
        ):
            raise RuntimeError(
                f"{spec.platform} exported slide duplicated or lost source triangles"
            )
        action_bounds = mesh_godot_bounds(root, action_geometry)
        body_bounds = mesh_godot_bounds(root, body)
        require_bounds(
            f"{spec.platform} exported authored slide",
            action_bounds,
            (Vector(audit.action_minimum), Vector(audit.action_maximum)),
        )
        action_home = blender_to_godot(
            root_inverse @ action.matrix_world.translation
        )
        if (action_home - ACTION_HOME).length > PISTOL_BOUNDS_TOLERANCE:
            raise RuntimeError(
                f"{spec.platform} action pivot left its runtime profile home"
            )
        action_contact = blender_to_godot(
            root_inverse @ action_socket.matrix_world.translation
        )
        if (
            action_contact - Vector(audit.action_contact)
        ).length > PISTOL_BOUNDS_TOLERANCE:
            raise RuntimeError(
                f"{spec.platform} action contact left the authored slide surface"
            )
        rest_location = action.location.copy()
        action.location += godot_to_blender(ACTION_TRAVEL)
        bpy.context.view_layer.update()
        require_bounds(
            f"{spec.platform} cycled authored slide",
            mesh_godot_bounds(root, action_geometry),
            (action_bounds[0] + ACTION_TRAVEL, action_bounds[1] + ACTION_TRAVEL),
        )
        require_bounds(
            f"{spec.platform} fixed body during slide cycle",
            mesh_godot_bounds(root, body),
            body_bounds,
        )
        action.location = rest_location
        bpy.context.view_layer.update()

    points = [
        blender_to_godot(root.matrix_world.inverted() @ obj.matrix_world @ vertex.co)
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
        for vertex in obj.data.vertices
    ]
    minimum, maximum = point_bounds(points)
    if maximum.z - minimum.z < spec.target_length * 0.92:
        raise RuntimeError(f"{spec.platform} runtime length is implausible")
    return action_surface_distance


def validate_output_hashes(
    output: Path,
    preview: Path,
    spec: WeaponSpec,
) -> None:
    expected = OUTPUT_AUDITS.get(spec.platform)
    if expected is None:
        return
    glb_hash, glb_bytes, preview_hash, preview_bytes = expected
    if not glb_hash or glb_bytes <= 0 or not preview_hash or preview_bytes <= 0:
        return
    actual = (sha256(output), output.stat().st_size, sha256(preview), preview.stat().st_size)
    if actual != expected:
        raise RuntimeError(
            f"{spec.platform} deterministic output drifted: "
            f"{actual} != {expected}"
        )


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    selected_specs = (
        tuple(spec for spec in SPECS if spec.platform in PISTOL_SLIDE_AUDITS)
        if "--pistols-only" in sys.argv
        else tuple(
            spec for spec in SPECS
            if spec.platform in LONG_GUN_ACTION_SOCKET_AUDITS
        )
        if "--long-guns-only" in sys.argv
        else SPECS
    )
    for spec in selected_specs:
        root, counts = build_asset(spec)
        output = export_asset(root, spec)
        blend = save_source(spec)
        preview = render_preview(root, spec)
        action_surface_distance = validate_export(output, spec, counts)
        validate_output_hashes(output, preview, spec)
        action_socket = LONG_GUN_ACTION_SOCKET_AUDITS.get(spec.platform)
        action_socket_log = (
            f" action_socket={action_socket} "
            f"action_surface_distance_m={action_surface_distance:.9f}"
            if action_socket is not None and action_surface_distance is not None
            else ""
        )
        print(
            "SUPPLEMENTAL_RELOADABLE_EXPORT "
            f"platform={spec.platform} mechanism={spec.mechanism} "
            f"source_sha256={spec.source_sha256} "
            f"glb_sha256={sha256(output)} blend_sha256={sha256(blend)} "
            f"preview_sha256={sha256(preview)} "
            f"counts={counts} glb_bytes={output.stat().st_size} "
            f"blend_bytes={blend.stat().st_size} valid=True"
            f"{action_socket_log}"
        )


if __name__ == "__main__":
    main()
