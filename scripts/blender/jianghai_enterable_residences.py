"""Author and validate real ground-floor apertures for selected Jianghai shops.

The Chinese district uses shared meshes for most street buildings.  A door cut
on a shared mesh would open every copy, so this module duplicates only the
twelve reviewed street objects before applying an exact Blender cut.  The
resulting unique facades and one shared modular interior liner remain part of
the authoritative Old City blend.
"""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import blake2b
from math import atan2, cos, radians, sin
from struct import pack

import bmesh
import bpy
from mathutils import Euler
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from jianghai_chinese_district_layout import (
    ENTERABLE_COLLISION_LAYOUT,
    ENTERABLE_POSITION_OVERRIDES,
    ENTERABLE_RESIDENCE_LAYOUT,
    ENTERABLE_YAW_DEGREES,
)
from jianghai_enterable_interior_liners import (
    rebuild_interior_liners,
    validate_interior_liners,
)


ENTERABLE_VERSION = 2
LEGACY_ENTERABLE_VERSION = 1
DOOR_WIDTH_METERS = 1.58
DOOR_HEIGHT_METERS = 2.48
DOOR_FLOOR_OVERLAP_METERS = 0.04
MINIMUM_INTERIOR_DEPTH_METERS = 3.8
FACADE_PROBE_OUTSET_METERS = 0.30
FACADE_PROBE_LENGTH_METERS = 1.20
SCENE_APERTURE_OUTSET_METERS = 0.55
SCENE_APERTURE_CLEARANCE_METERS = 1.20
SCENE_APERTURE_RAY_LENGTH_METERS = 12.0
REPLACED_INSERT_VERSION = 1
REPLACED_INSERT_NAME = "JianghaiExpansion_Facade_EastPhoto_F0_C1_Insert"
REPLACED_INSERT_PARENT_NAME = "JianghaiExpansion_UrbanFacades"
REPLACED_INSERT_MESH_NAME = "Cube.004"
REPLACED_INSERT_SURVIVOR_NAME = "JianghaiExpansion_Facade_WestClock_F0_C1_Insert"
RETAINED_INSERT_WALL_NAME = "JianghaiExpansion_Facade_EastPhoto_F0_C1_Wall"
REPLACED_INSERT_MARKER = "jianghai_enterable_replaced_insert_version"
ENTERABLE_MESH_SHARE_GROUPS = (
    ("EastPhotoHouse", "EastGateRow00"),
    ("OuterEastMidResidence", "OuterWestSquareResidence"),
    ("WeatheredRollerShop02", "WeatheredRollerShop03"),
)


@dataclass(frozen=True)
class EnterableResidenceMetrics:
    residence_count: int
    cut_count: int
    aperture_sample_count: int
    wall_sample_count: int
    triangle_count: int
    scene_aperture_sample_count: int = 0
    removed_insert_count: int = 0
    liner_count: int = 0
    liner_triangle_count: int = 0
    liner_closure_sample_count: int = 0
    liner_entry_sample_count: int = 0
    liner_opaque_material_count: int = 0
    shared_mesh_pair_count: int = 0


def _mesh_bounds(mesh: bpy.types.Mesh) -> tuple[Vector, Vector]:
    coordinates = [vertex.co for vertex in mesh.vertices]
    if not coordinates:
        raise RuntimeError(f"Mesh has no vertices: {mesh.name}")
    minimum = Vector(tuple(min(vertex[axis] for vertex in coordinates) for axis in range(3)))
    maximum = Vector(tuple(max(vertex[axis] for vertex in coordinates) for axis in range(3)))
    return minimum, maximum


def _world_scale(obj: bpy.types.Object) -> Vector:
    scale = obj.matrix_world.to_scale()
    absolute = Vector((abs(scale.x), abs(scale.y), abs(scale.z)))
    if min(absolute) <= 0.0001:
        raise RuntimeError(f"Enterable residence has invalid scale: {obj.name} {tuple(scale)}")
    return absolute


def _apply_reviewed_orientation(obj: bpy.types.Object) -> None:
    yaw = radians(ENTERABLE_YAW_DEGREES[obj.name])
    obj.rotation_mode = "XYZ"
    obj.rotation_euler = Euler((0.0, 0.0, yaw), "XYZ")
    if obj.name in ENTERABLE_POSITION_OVERRIDES:
        obj.location = Vector(ENTERABLE_POSITION_OVERRIDES[obj.name])


def _vector_matches(actual, expected: tuple[float, float, float]) -> bool:
    return (Vector(actual) - Vector(expected)).length <= 0.0001


def _validate_retained_insert_wall() -> None:
    wall = bpy.data.objects.get(RETAINED_INSERT_WALL_NAME)
    if wall is None or wall.type != "MESH":
        raise RuntimeError(f"Retained East Photo facade wall is missing: {RETAINED_INSERT_WALL_NAME}")
    if wall.parent is None or wall.parent.name != REPLACED_INSERT_PARENT_NAME:
        raise RuntimeError(
            f"Retained East Photo facade wall parent drifted: {wall.parent}"
        )
    if (
        not _vector_matches(wall.location, (13.38, 1.50, 0.03))
        or not _vector_matches(wall.rotation_euler, (0.0, 0.0, radians(90.0)))
        or not _vector_matches(wall.scale, (1.0, 1.0, 1.0))
        or not _vector_matches(wall.dimensions, (3.0, 0.0, 3.0))
        or len(wall.data.polygons) != 2_256
        or tuple(material.name for material in wall.data.materials if material)
        != ("modular_urban_apartments_facade_plaster",)
    ):
        raise RuntimeError(
            f"Retained East Photo facade wall contract drifted: {RETAINED_INSERT_WALL_NAME}"
        )


def _validate_replaced_insert_contract(obj: bpy.types.Object) -> None:
    mesh_users = sorted(
        candidate.name
        for candidate in bpy.data.objects
        if candidate.type == "MESH" and candidate.data == obj.data
    )
    materials = tuple(material.name for material in obj.data.materials if material)
    if (
        obj.type != "MESH"
        or obj.parent is None
        or obj.parent.name != REPLACED_INSERT_PARENT_NAME
        or tuple(collection.name for collection in obj.users_collection) != ("Scene Collection",)
        or obj.rotation_mode != "XYZ"
        or not _vector_matches(obj.location, (13.38, 1.50, 0.03))
        or not _vector_matches(obj.rotation_euler, (0.0, 0.0, radians(90.0)))
        or not _vector_matches(obj.scale, (1.0, 1.0, 1.0))
        or not _vector_matches(obj.dimensions, (2.0800056, 0.4400001, 2.8099997))
        or obj.data.name != REPLACED_INSERT_MESH_NAME
        or len(obj.data.vertices) != 2_263
        or len(obj.data.polygons) != 1_711
        or mesh_users != [REPLACED_INSERT_NAME, REPLACED_INSERT_SURVIVOR_NAME]
        or materials
        != (
            "modular_urban_apartments_facade_objects",
            "modular_urban_apartments_facade_glass",
        )
    ):
        raise RuntimeError(
            f"Legacy East Photo door insert contract drifted: {REPLACED_INSERT_NAME} "
            f"mesh={obj.data.name} users={mesh_users} materials={materials}"
        )


def _replace_legacy_door_insert() -> int:
    parent = bpy.data.objects.get(REPLACED_INSERT_PARENT_NAME)
    if parent is None:
        raise RuntimeError(f"Facade expansion root is missing: {REPLACED_INSERT_PARENT_NAME}")
    _validate_retained_insert_wall()
    insert = bpy.data.objects.get(REPLACED_INSERT_NAME)
    if insert is None:
        if parent.get(REPLACED_INSERT_MARKER) != REPLACED_INSERT_VERSION:
            raise RuntimeError(
                f"Legacy East Photo insert disappeared without replacement provenance: "
                f"{REPLACED_INSERT_NAME}"
            )
        return 0
    _validate_replaced_insert_contract(insert)
    parent[REPLACED_INSERT_MARKER] = REPLACED_INSERT_VERSION
    parent["jianghai_enterable_replaced_insert_name"] = REPLACED_INSERT_NAME
    parent["jianghai_enterable_replacement_role"] = "hinged_chinese_lattice_door"
    bpy.data.objects.remove(insert, do_unlink=True)
    bpy.context.view_layer.update()
    return 1


def _validate_legacy_insert_replacement() -> None:
    parent = bpy.data.objects.get(REPLACED_INSERT_PARENT_NAME)
    if (
        parent is None
        or parent.get(REPLACED_INSERT_MARKER) != REPLACED_INSERT_VERSION
        or parent.get("jianghai_enterable_replaced_insert_name") != REPLACED_INSERT_NAME
        or parent.get("jianghai_enterable_replacement_role")
        != "hinged_chinese_lattice_door"
        or bpy.data.objects.get(REPLACED_INSERT_NAME) is not None
    ):
        raise RuntimeError("East Photo legacy door insert replacement provenance is invalid")
    _validate_retained_insert_wall()
    survivor = bpy.data.objects.get(REPLACED_INSERT_SURVIVOR_NAME)
    if (
        survivor is None
        or survivor.type != "MESH"
        or survivor.data.name != REPLACED_INSERT_MESH_NAME
        or survivor.data.users != 1
    ):
        raise RuntimeError(
            f"Shared West Clock facade insert was damaged: {REPLACED_INSERT_SURVIVOR_NAME}"
        )


def _cut_unique_mesh(obj: bpy.types.Object) -> None:
    source_mesh = obj.data
    source_minimum, source_maximum = _mesh_bounds(source_mesh)
    scale = _world_scale(obj)
    unique_mesh = source_mesh.copy()
    local_half_width = DOOR_WIDTH_METERS * 0.5 / scale.x
    local_height = DOOR_HEIGHT_METERS / scale.z
    local_depth = min(
        (source_maximum.y - source_minimum.y) * 0.46,
        3.5 / scale.y,
    )
    center_x = (source_minimum.x + source_maximum.x) * 0.5
    cut_min_x = center_x - local_half_width
    cut_max_x = center_x + local_half_width
    cut_max_y = source_minimum.y + local_depth
    cut_max_z = source_minimum.z + local_height

    # These joined pavilion/shop meshes are intentionally non-manifold.  A
    # volume boolean can therefore invert the cutter surface and leave an
    # invisible plug in the doorway.  Plane-splitting the authored mesh and
    # deleting only the faces inside the portal is deterministic for both open
    # and closed source components, while retaining the surrounding facade.
    mesh_data = bmesh.new()
    try:
        mesh_data.from_mesh(unique_mesh)
        split_planes = (
            (Vector((cut_min_x, 0.0, 0.0)), Vector((1.0, 0.0, 0.0))),
            (Vector((cut_max_x, 0.0, 0.0)), Vector((1.0, 0.0, 0.0))),
            (Vector((0.0, cut_max_y, 0.0)), Vector((0.0, 1.0, 0.0))),
            (Vector((0.0, 0.0, cut_max_z)), Vector((0.0, 0.0, 1.0))),
        )
        for plane_co, plane_no in split_planes:
            geometry = [*mesh_data.verts, *mesh_data.edges, *mesh_data.faces]
            bmesh.ops.bisect_plane(
                mesh_data,
                geom=geometry,
                dist=0.00001,
                plane_co=plane_co,
                plane_no=plane_no,
                clear_inner=False,
                clear_outer=False,
            )
        portal_faces = []
        for face in mesh_data.faces:
            center = face.calc_center_median()
            if (
                cut_min_x - 0.0001 <= center.x <= cut_max_x + 0.0001
                and center.y <= cut_max_y + 0.0001
                and center.z <= cut_max_z + 0.0001
            ):
                portal_faces.append(face)
        if not portal_faces:
            raise RuntimeError(f"Door cut found no facade faces: {obj.name}")
        bmesh.ops.delete(mesh_data, geom=portal_faces, context="FACES")
        mesh_data.to_mesh(unique_mesh)
        unique_mesh.update()
        if len(unique_mesh.polygons) <= 0:
            raise RuntimeError(f"Door cut removed the complete residence mesh: {obj.name}")

        unique_mesh.name = f"JianghaiEnterable_{obj.name}_LOD"
        unique_mesh["jianghai_enterable_mesh_version"] = ENTERABLE_VERSION
        unique_mesh["authored_derivation"] = (
            "Blender-authored unique Chinese arcade-shop mesh with a real human-scale doorway"
        )
        unique_mesh["source_asset"] = source_mesh.get(
            "source_asset",
            "Chinese Four-corner Pavilion - Free; Quaternius Buildings Pack; Chinese Temple 2",
        )
        unique_mesh["source_creator"] = source_mesh.get(
            "source_creator",
            "VVayToyek; Quaternius; Free poly",
        )
        unique_mesh["source_url"] = source_mesh.get("source_url", "")
        unique_mesh["license"] = source_mesh.get("license", "CC0 1.0 Universal")
        obj.data = unique_mesh
    finally:
        mesh_data.free()


def _ensure_enterable_mesh(obj: bpy.types.Object) -> bool:
    """Cut untouched facades and migrate already-cut v1 meshes without recutting."""

    mesh_version = obj.data.get("jianghai_enterable_mesh_version")
    if mesh_version is None:
        _cut_unique_mesh(obj)
        return True
    if int(mesh_version) == LEGACY_ENTERABLE_VERSION:
        if obj.data.users != 1:
            raise RuntimeError(
                f"Legacy enterable mesh is unexpectedly shared: {obj.name} users={obj.data.users}"
            )
        obj.data["jianghai_enterable_mesh_version"] = ENTERABLE_VERSION
        obj.data["authored_derivation"] = (
            "Blender-authored unique Chinese arcade-shop mesh with a real human-scale doorway; "
            "v2 adds a shared opaque modular interior liner without recutting the facade"
        )
        return False
    if int(mesh_version) != ENTERABLE_VERSION:
        raise RuntimeError(
            f"Unsupported enterable mesh version: {obj.name} version={mesh_version}"
        )
    return False


def _mesh_geometry_digest(mesh: bpy.types.Mesh) -> str:
    digest = blake2b(digest_size=20)
    digest.update(pack("<II", len(mesh.vertices), len(mesh.polygons)))
    for vertex in mesh.vertices:
        digest.update(pack("<3d", vertex.co.x, vertex.co.y, vertex.co.z))
    for polygon in mesh.polygons:
        digest.update(pack("<II", polygon.material_index, len(polygon.vertices)))
        for vertex_index in polygon.vertices:
            digest.update(pack("<I", vertex_index))
    return digest.hexdigest()


def _share_compatible_enterable_meshes() -> int:
    """Instance only reviewed cut meshes whose source and local cut are identical."""

    shared_pairs = 0
    for leader_name, follower_name in ENTERABLE_MESH_SHARE_GROUPS:
        leader = bpy.data.objects.get(leader_name)
        follower = bpy.data.objects.get(follower_name)
        if (
            leader is None
            or follower is None
            or leader.type != "MESH"
            or follower.type != "MESH"
        ):
            raise RuntimeError(
                f"Reviewed enterable mesh pair is missing: {leader_name}/{follower_name}"
            )
        if (_world_scale(leader) - _world_scale(follower)).length > 0.0001:
            raise RuntimeError(
                f"Enterable mesh pair scale drifted: {leader_name}/{follower_name}"
            )
        if ENTERABLE_COLLISION_LAYOUT[leader_name] != ENTERABLE_COLLISION_LAYOUT[follower_name]:
            raise RuntimeError(
                f"Enterable mesh pair collision contract drifted: {leader_name}/{follower_name}"
            )
        if leader.data != follower.data:
            if tuple(leader.data.materials) != tuple(follower.data.materials):
                raise RuntimeError(
                    f"Enterable mesh pair materials differ: {leader_name}/{follower_name}"
                )
            if _mesh_geometry_digest(leader.data) != _mesh_geometry_digest(follower.data):
                raise RuntimeError(
                    f"Enterable mesh pair geometry differs: {leader_name}/{follower_name}"
                )
            redundant_mesh = follower.data
            follower.data = leader.data
            if redundant_mesh.users == 0:
                bpy.data.meshes.remove(redundant_mesh)
        leader.data["jianghai_enterable_shared_instances"] = (
            f"{leader_name};{follower_name}"
        )
        shared_pairs += 1
    return shared_pairs


def _validate_shared_enterable_meshes() -> int:
    for leader_name, follower_name in ENTERABLE_MESH_SHARE_GROUPS:
        leader = bpy.data.objects.get(leader_name)
        follower = bpy.data.objects.get(follower_name)
        if leader is None or follower is None or leader.data != follower.data:
            raise RuntimeError(
                f"Reviewed enterable mesh pair is no longer instanced: "
                f"{leader_name}/{follower_name}"
            )
        mesh_users = sorted(
            obj.name
            for obj in bpy.data.objects
            if obj.type == "MESH" and obj.data == leader.data
        )
        if mesh_users != sorted((leader_name, follower_name)):
            raise RuntimeError(
                f"Enterable mesh leaked to an unreviewed solid building: "
                f"pair={leader_name}/{follower_name} users={mesh_users}"
            )
        if leader.data.get("jianghai_enterable_shared_instances") != (
            f"{leader_name};{follower_name}"
        ):
            raise RuntimeError(
                f"Enterable mesh sharing provenance drifted: {leader_name}/{follower_name}"
            )
    return len(ENTERABLE_MESH_SHARE_GROUPS)


def _set_runtime_metadata(obj: bpy.types.Object, archetype: str) -> None:
    scale = _world_scale(obj)
    minimum, maximum = _mesh_bounds(obj.data)
    world_width = (maximum.x - minimum.x) * scale.x
    world_depth = (maximum.y - minimum.y) * scale.y
    obj["jianghai_enterable"] = True
    obj["jianghai_enterable_version"] = ENTERABLE_VERSION
    obj["jianghai_room_archetype"] = archetype
    obj["jianghai_door_width_m"] = DOOR_WIDTH_METERS
    obj["jianghai_door_height_m"] = DOOR_HEIGHT_METERS
    obj["jianghai_door_front"] = "local_positive_z_godot"
    (
        front_inset,
        collision_width,
        collision_depth,
        collision_height,
        facade_width,
        wing_front_inset,
        rear_wing_inset,
        wing_inner_half_width,
        wing_outer_half_width,
        side_half_width,
        side_front_inset,
        side_rear_inset,
    ) = ENTERABLE_COLLISION_LAYOUT[obj.name]
    # Keep the visible liner just inside the reviewed gameplay envelope.  This
    # guarantees the visible side/rear surfaces and collision agree even in the
    # compact roller shops, whose ornamental exterior AABB is much deeper.
    obj["jianghai_room_width_m"] = min(
        max(world_width - 1.2, 3.8),
        7.2,
        collision_width - 0.35,
    )
    obj["jianghai_room_depth_m"] = min(
        max(world_depth - 1.3, MINIMUM_INTERIOR_DEPTH_METERS),
        6.4,
        collision_depth - 0.30,
    )
    obj["jianghai_door_front_inset_m"] = front_inset
    obj["jianghai_collision_width_m"] = collision_width
    obj["jianghai_collision_depth_m"] = collision_depth
    obj["jianghai_collision_height_m"] = collision_height
    obj["jianghai_collision_facade_width_m"] = facade_width
    obj["jianghai_collision_wing_front_inset_m"] = wing_front_inset
    obj["jianghai_collision_rear_wing_inset_m"] = rear_wing_inset
    obj["jianghai_collision_wing_inner_half_width_m"] = wing_inner_half_width
    obj["jianghai_collision_wing_outer_half_width_m"] = wing_outer_half_width
    obj["jianghai_collision_side_half_width_m"] = side_half_width
    obj["jianghai_collision_side_front_inset_m"] = side_front_inset
    obj["jianghai_collision_side_rear_inset_m"] = side_rear_inset
    obj["jianghai_gameplay_proxy"] = True
    obj["jianghai_proxy_role"] = "enterable_building_shell"


def _ray_hits(tree: BVHTree, origin: Vector, direction: Vector, distance: float) -> bool:
    return tree.ray_cast(origin, direction, distance)[0] is not None


def _validate_scene_aperture(
    obj: bpy.types.Object,
    facade: Vector,
    scale: Vector,
    front_inset: float,
) -> int:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    outward = Vector((0.0, -1.0, 0.0))
    inward = -outward
    tangent = Vector((1.0, 0.0, 0.0))
    samples = 0
    for lateral in (-0.42, 0.0, 0.42):
        for height in (0.45, 1.20, 2.18):
            center = facade + tangent * (lateral / scale.x)
            center += Vector((0.0, 0.0, height / scale.z))
            # Start beyond the complete ornamental AABB, then follow the true
            # doorway through the authored scene.  Three lateral by three
            # vertical samples catch narrow leftover facade strips as well as
            # detached inserts; the rear liner remains outside the 1.2 m guard.
            origin = obj.matrix_world @ (
                center
                + outward
                * ((front_inset + SCENE_APERTURE_OUTSET_METERS) / scale.y)
            )
            end = obj.matrix_world @ (
                center
                + inward
                * (
                    (SCENE_APERTURE_RAY_LENGTH_METERS - front_inset)
                    / scale.y
                )
            )
            ray = end - origin
            hit, location, _, _, blocker, _ = bpy.context.scene.ray_cast(
                depsgraph,
                origin,
                ray.normalized(),
                distance=ray.length,
            )
            if hit:
                distance = (location - origin).length
                if distance < SCENE_APERTURE_CLEARANCE_METERS - 0.0001:
                    blocker_name = blocker.name if blocker is not None else "unknown"
                    raise RuntimeError(
                        f"Door approach remains blocked: {obj.name} x={lateral:.2f} "
                        f"height={height:.2f} distance={distance:.3f}m "
                        f"blocker={blocker_name}"
                    )
            samples += 1
    return samples


def _validate_residence(obj: bpy.types.Object) -> tuple[int, int, int, int]:
    if obj.data.get("jianghai_enterable_mesh_version") != ENTERABLE_VERSION:
        raise RuntimeError(f"Enterable residence mesh is stale: {obj.name}")
    minimum, maximum = _mesh_bounds(obj.data)
    scale = _world_scale(obj)
    expected_yaw = radians(ENTERABLE_YAW_DEGREES[obj.name])
    yaw_error = atan2(
        sin(obj.rotation_euler.z - expected_yaw),
        cos(obj.rotation_euler.z - expected_yaw),
    )
    if obj.rotation_mode != "XYZ" or abs(yaw_error) > 0.00001:
        raise RuntimeError(
            f"Enterable residence orientation drifted: {obj.name} "
            f"mode={obj.rotation_mode} yaw={obj.rotation_euler.z:.6f}"
        )
    expected_position = ENTERABLE_POSITION_OVERRIDES.get(obj.name)
    if expected_position is not None and (
        obj.location - Vector(expected_position)
    ).length > 0.0001:
        raise RuntimeError(
            f"Enterable residence position drifted: {obj.name} "
            f"actual={tuple(obj.location)} expected={expected_position}"
        )
    expected_collision = ENTERABLE_COLLISION_LAYOUT[obj.name]
    actual_collision = (
        obj.get("jianghai_door_front_inset_m"),
        obj.get("jianghai_collision_width_m"),
        obj.get("jianghai_collision_depth_m"),
        obj.get("jianghai_collision_height_m"),
        obj.get("jianghai_collision_facade_width_m"),
        obj.get("jianghai_collision_wing_front_inset_m"),
        obj.get("jianghai_collision_rear_wing_inset_m"),
        obj.get("jianghai_collision_wing_inner_half_width_m"),
        obj.get("jianghai_collision_wing_outer_half_width_m"),
        obj.get("jianghai_collision_side_half_width_m"),
        obj.get("jianghai_collision_side_front_inset_m"),
        obj.get("jianghai_collision_side_rear_inset_m"),
    )
    if any(
        actual is None or abs(float(actual) - expected) > 0.0001
        for actual, expected in zip(actual_collision, expected_collision)
    ):
        raise RuntimeError(
            f"Enterable residence collision envelope drifted: {obj.name} "
            f"actual={actual_collision} expected={expected_collision}"
        )
    room_width = float(obj.get("jianghai_room_width_m", 0.0))
    room_depth = float(obj.get("jianghai_room_depth_m", 0.0))
    if (
        room_width < 3.8
        or room_width > expected_collision[1] - 0.35 + 0.0001
        or room_depth < MINIMUM_INTERIOR_DEPTH_METERS
        or room_depth > expected_collision[2] - 0.30 + 0.0001
    ):
        raise RuntimeError(
            f"Visible room exceeds its collision envelope: {obj.name} "
            f"room=({room_width:.3f},{room_depth:.3f}) "
            f"collision=({expected_collision[1]:.3f},{expected_collision[2]:.3f})"
        )
    tree = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in obj.data.vertices],
        [tuple(polygon.vertices) for polygon in obj.data.polygons],
        all_triangles=False,
    )
    outward = Vector((0.0, -1.0, 0.0))
    inward = -outward
    # The mesh AABB starts at the ornamental roof/eave projection.  In the
    # deeper East Photo and East Tea houses that projection sits more than a
    # metre ahead of the actual ground-floor facade, so the old 1.35 m probe
    # could end before reaching any wall and falsely report a clear doorway.
    # The reviewed collision envelope stores the real facade inset in metres;
    # anchor every aperture, jamb, and lintel ray to that architectural plane.
    front_inset = expected_collision[0]
    facade = Vector(
        (
            (minimum.x + maximum.x) * 0.5,
            minimum.y + front_inset / scale.y,
            minimum.z,
        )
    )

    aperture_samples = 0
    for lateral in (-0.42, 0.0, 0.42):
        for height in (0.45, 1.20, 2.18):
            center = facade + Vector(
                (lateral / scale.x, 0.0, height / scale.z)
            )
            aperture_origin = center + outward * (FACADE_PROBE_OUTSET_METERS / scale.y)
            aperture_hit = tree.ray_cast(
                aperture_origin,
                inward,
                FACADE_PROBE_LENGTH_METERS / scale.y,
            )[0]
            if aperture_hit is not None:
                facade_depth = (aperture_hit.y - facade.y) * scale.y
                raise RuntimeError(
                    f"Door aperture remains blocked: {obj.name} x={lateral:.2f} "
                    f"height={height:.2f} facade_depth={facade_depth:.2f}m"
                )
            aperture_samples += 1

    tangent = Vector((1.0, 0.0, 0.0))
    wall_samples = 0
    for side in (-1.0, 1.0):
        side_wall_found = False
        for offset in (0.22, 0.48, 0.82, 1.18):
            point = facade + tangent * side * (
                (DOOR_WIDTH_METERS * 0.5 + offset) / scale.x
            )
            point += Vector((0.0, 0.0, 1.20 / scale.z))
            if _ray_hits(
                tree,
                point + outward * (FACADE_PROBE_OUTSET_METERS / scale.y),
                inward,
                FACADE_PROBE_LENGTH_METERS / scale.y,
            ):
                side_wall_found = True
                break
        if not side_wall_found:
            raise RuntimeError(f"Door side wall was over-cut: {obj.name} side={side:+.0f}")
        wall_samples += 1
    lintel_found = False
    for offset in (0.18, 0.38, 0.65, 0.95):
        lintel = facade + Vector(
            (0.0, 0.0, (DOOR_HEIGHT_METERS + offset) / scale.z)
        )
        if _ray_hits(
            tree,
            lintel + outward * (FACADE_PROBE_OUTSET_METERS / scale.y),
            inward,
            FACADE_PROBE_LENGTH_METERS / scale.y,
        ):
            lintel_found = True
            break
    if not lintel_found:
        raise RuntimeError(f"Door lintel was over-cut: {obj.name}")
    wall_samples += 1

    scene_aperture_samples = _validate_scene_aperture(
        obj,
        facade,
        scale,
        front_inset,
    )

    # The city ground supplies gameplay floor collision; this probe makes sure
    # the authored shell still has enough depth for furniture and occupants.
    local_interior_y = minimum.y + MINIMUM_INTERIOR_DEPTH_METERS / scale.y
    if local_interior_y >= maximum.y - 0.25 / scale.y:
        raise RuntimeError(f"Residence has no usable interior depth: {obj.name}")
    obj.data.calc_loop_triangles()
    return (
        aperture_samples,
        wall_samples,
        len(obj.data.loop_triangles),
        scene_aperture_samples,
    )


def apply_enterable_residences() -> EnterableResidenceMetrics:
    removed_insert_count = _replace_legacy_door_insert()
    cut_count = 0
    for object_name, archetype in ENTERABLE_RESIDENCE_LAYOUT:
        obj = bpy.data.objects.get(object_name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Enterable Jianghai residence is missing: {object_name}")
        _apply_reviewed_orientation(obj)
        if _ensure_enterable_mesh(obj):
            cut_count += 1
        _set_runtime_metadata(obj, archetype)
    _share_compatible_enterable_meshes()
    rebuild_interior_liners(
        tuple(object_name for object_name, _ in ENTERABLE_RESIDENCE_LAYOUT)
    )
    metrics = validate_enterable_residences()
    return EnterableResidenceMetrics(
        metrics.residence_count,
        cut_count,
        metrics.aperture_sample_count,
        metrics.wall_sample_count,
        metrics.triangle_count,
        metrics.scene_aperture_sample_count,
        removed_insert_count,
        metrics.liner_count,
        metrics.liner_triangle_count,
        metrics.liner_closure_sample_count,
        metrics.liner_entry_sample_count,
        metrics.liner_opaque_material_count,
        metrics.shared_mesh_pair_count,
    )


def validate_enterable_residences() -> EnterableResidenceMetrics:
    _validate_legacy_insert_replacement()
    aperture_samples = 0
    wall_samples = 0
    triangles = 0
    scene_aperture_samples = 0
    for object_name, archetype in ENTERABLE_RESIDENCE_LAYOUT:
        obj = bpy.data.objects.get(object_name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Enterable Jianghai residence is missing: {object_name}")
        if obj.get("jianghai_enterable") is not True:
            raise RuntimeError(f"Enterable metadata is missing: {object_name}")
        if obj.get("jianghai_room_archetype") != archetype:
            raise RuntimeError(f"Enterable archetype drifted: {object_name}")
        (
            aperture_count,
            wall_count,
            triangle_count,
            scene_aperture_count,
        ) = _validate_residence(obj)
        aperture_samples += aperture_count
        wall_samples += wall_count
        triangles += triangle_count
        scene_aperture_samples += scene_aperture_count
    liner_metrics = validate_interior_liners(
        tuple(object_name for object_name, _ in ENTERABLE_RESIDENCE_LAYOUT)
    )
    shared_mesh_pairs = _validate_shared_enterable_meshes()
    return EnterableResidenceMetrics(
        len(ENTERABLE_RESIDENCE_LAYOUT),
        0,
        aperture_samples,
        wall_samples,
        triangles,
        scene_aperture_samples,
        0,
        liner_metrics.liner_count,
        liner_metrics.triangle_count,
        liner_metrics.closure_sample_count,
        liner_metrics.entry_sample_count,
        liner_metrics.opaque_material_count,
        shared_mesh_pairs,
    )
