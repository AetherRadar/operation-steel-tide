"""Build deterministic, arms-only skeletal reload clips from DJMaesen's rig.

The CC BY 4.0 source supplies production glove/sleeve geometry, materials,
finger bones, and skin weights.  This adaptation removes every weapon mesh,
turns the authored firing pose into the rest pose, and bakes platform-specific
camera-safe pose-to-pose actions. Long guns use family-calibrated left-arm IK;
pistol crops use a deterministic analytical shoulder-elbow solve. Dedicated
long-gun and sidearm forearm meshes preserve the authored gloves, sleeves,
skin, materials, and UVs while excluding the upper-arm cloth that can cross a
first-person camera near plane. The complete authored arms remain in the file
as a hidden runtime audit layer. The right hand and both shoulder roots remain
fixed so runtime animation cannot detach the arms from the body or primary
grip.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup --python-exit-code 1 --python scripts/blender/build_animated_reload_arms.py
"""
from __future__ import annotations

from dataclasses import dataclass
import json
import math
from pathlib import Path
import struct
import sys

import bpy
import bmesh
from mathutils import Euler, Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_djmaesen_smg45 import extend_authored_sleeves, refine_authored_sleeves
from build_first_person_arms import (
    evaluate_pose as evaluate_static_arm_pose,
    evaluated_component_centers,
    frame_at,
    hand_contact_center,
    weapon_cross_section_frame,
)


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "djmaesen_fps_smg45"
SOURCE_GLB = SOURCE_DIR / "fps_animated_smg.glb"
SOURCE_BLEND = SOURCE_DIR / "animated_reload_arms.blend"
OUTPUT_GLB = (
    REPO_ROOT
    / "assets"
    / "models"
    / "djmaesen_smg45"
    / "animated_reload_arms.glb"
)
SOURCE_IDLE_FRAME = 155
SOURCE_TO_METERS = 0.015
FPS = 30
TACTICAL_DURATION_SECONDS = 1.80
EMPTY_DURATION_SECONDS = 2.15
EXPECTED_TRIANGLES = 13_700
EXPECTED_LONG_GUN_TRIANGLES = 12_686
EXPECTED_SIDEARM_TRIANGLES = 9_334
LONG_GUN_FOREARM_CUFF_LENGTH = 40.0
SIDEARM_FOREARM_CUFF_LENGTH = 16.0
LEFT_CHAIN = ("L_arm_01", "L_elbow_02", "L_wrist_03")
LEFT_HAND_BONES = (
    "L_thumb1_04", "L_thumb2_05", "L_thumb3_00",
    "L_point1_07", "L_point2_08", "L_point3_09",
    "L_middle1_011", "L_middle2_012", "L_middle3_013",
    "L_palm_015",
    "L_ring1_016", "L_ring2_017", "L_ring3_018",
    "L_pink1_020", "L_pink2_021", "L_pink3_022",
)
LEFT_ANIMATED_BONES = LEFT_CHAIN + LEFT_HAND_BONES
RIGHT_PALM = "R_palm_039"
LEFT_PALM = "L_palm_015"
LEFT_SHOULDER = "L_arm_01"
RIGHT_SHOULDER = "R_arm_024"
MAX_FIXED_DRIFT_METERS = 0.00025
MAX_FIXED_ROTATION_RADIANS = 0.001
MAX_BIND_SURFACE_ERROR_METERS = 0.00001
MIN_HAND_TRAVEL_METERS = 0.25
MAX_HAND_TRAVEL_METERS = 2.50
MAX_RETURN_ERROR_METERS = 0.025
MAX_CONTROL_EXCURSION_METERS = 0.65
MAX_CAMERA_SAFE_EXCURSION_METERS = 0.85
MAX_POSE_HOLD_DRIFT_METERS = 0.025
MIN_MECHANICAL_BEAT_METERS = 0.030
MIN_PALM_OFFSET_METERS = 0.075
MAX_PALM_OFFSET_METERS = 0.115
MIN_GRIP_CONTACT_METERS = 0.015
MAX_GRIP_CONTACT_METERS = 0.035
MIN_SUPPORT_ANCHOR_OFFSET_METERS = 0.050
MAX_SUPPORT_ANCHOR_OFFSET_METERS = 0.075
MAX_SIDEARM_ANCHOR_SURFACE_ERROR_METERS = 0.0051
MIN_SIDEARM_GRASP_CLEARANCE_METERS = 0.045
MAX_SIDEARM_GRASP_CLEARANCE_METERS = 0.060
MIN_SIDEARM_DIGIT_CURL_RADIANS = 0.65
MIN_SIDEARM_ANCHOR_BELOW_PALM_METERS = 0.060
MAX_SIDEARM_ANCHOR_BELOW_PALM_METERS = 0.063
MIN_SIDEARM_ANCHOR_LATERAL_METERS = -0.041
MAX_SIDEARM_ANCHOR_LATERAL_METERS = -0.038
MIN_SIDEARM_ANCHOR_FORWARD_METERS = -0.013
MAX_SIDEARM_ANCHOR_FORWARD_METERS = -0.010
MIN_SIDEARM_MAGAZINE_ANCHOR_OFFSET_METERS = 0.073
MAX_SIDEARM_MAGAZINE_ANCHOR_OFFSET_METERS = 0.095
MAX_SIDEARM_ANCHOR_TARGET_SNAP_METERS = 0.005
SIDEARM_ANCHOR_TARGET_LATERAL_METERS = -0.0397
SIDEARM_ANCHOR_TARGET_FORWARD_METERS = -0.0118
SIDEARM_ANCHOR_TARGET_BELOW_METERS = 0.0613
MAX_LEFT_BONE_STEP_RADIANS = 0.55
MAX_LEFT_PALM_STEP_METERS = 0.25
SIDEARM_MAX_LEFT_BONE_STEP_RADIANS = 0.24
SIDEARM_MAX_LEFT_JOINT_STEP_METERS = 0.045
SIDEARM_MAX_LEFT_PALM_STEP_METERS = 0.045
SIDEARM_MIN_HAND_TRAVEL_METERS = 0.10
SIDEARM_MAX_HAND_TRAVEL_METERS = 1.10
SIDEARM_MAX_CONTROL_EXCURSION_METERS = 0.32
SIDEARM_MAX_CAMERA_SAFE_EXCURSION_METERS = 0.45
SIDEARM_MAGAZINE_WRIST_POSES = {
    "p226": (-10.0, 25.0, 42.0),
    "m1911": (-10.0, 25.0, 42.0),
    "gsh18": (0.0, 5.0, 42.0),
    "desert_eagle": (-9.0, 28.0, 30.0),
}
SIDEARM_ACTION_WRIST_POSES = {
    "p226": (-12.0, -5.0, 10.0),
    "m1911": (-13.0, -6.0, 9.0),
    "gsh18": (-7.0, -3.0, 12.0),
    "desert_eagle": (-14.0, 8.0, 12.0),
}
SIDEARM_IK_BLEND_IN_START = 0.02
SIDEARM_IK_BLEND_IN_END = 0.18
SIDEARM_DESERT_EAGLE_BLEND_IN_END = 0.23
SIDEARM_TACTICAL_BLEND_OUT_START = 0.82
SIDEARM_EMPTY_BLEND_OUT_START = 0.890
SIDEARM_ENDPOINT_MAX_BASIS_ERROR_RADIANS = 0.01
SIDEARM_ENDPOINT_MAX_POSITION_ERROR_METERS = 0.003
SIDEARM_HAND_SOURCE_FRAMES = {
    "ready": SOURCE_IDLE_FRAME,
    "open": 40,
    "pinch": 20,
    "wrap": 0,
    "rack": 120,
}
MAX_LONG_GUN_CAMERA_HORIZONTAL_SPAN_METERS = 1.20
# Depth span includes the cuff extending away from the lens as well as toward
# it.  The explicit rear-extent gate below is the near-plane safety bound; this
# wider total span admits a natural 40-unit elbow cuff while still rejecting
# the 2.90 m full-arm audit silhouette by a large margin.
MAX_LONG_GUN_CAMERA_DEPTH_SPAN_METERS = 1.40
MAX_LONG_GUN_CAMERA_VERTICAL_SPAN_METERS = 1.05
MAX_LONG_GUN_CAMERA_REAR_EXTENT_METERS = 0.75
CAMERA_ENVELOPE_FRAME_STEP = 2


@dataclass(frozen=True)
class ReloadProfile:
    name: str
    family: str
    support: tuple[float, float, float]
    magazine: tuple[float, float, float]
    exchange: tuple[float, float, float]
    insert: tuple[float, float, float]
    action: tuple[float, float, float]
    pole: tuple[float, float, float]
    sidearm: bool = False
    tactical_action: bool = False


@dataclass(frozen=True)
class ReloadControlPoint:
    fraction: float
    location: Vector
    rotation_degrees: Vector
    beat: str = ""


@dataclass(frozen=True)
class SidearmStaticPose:
    """Exact local/global support-arm pose used by the static pistol asset."""

    local_basis: dict[str, Matrix]
    global_pose: dict[str, Matrix]


@dataclass(frozen=True)
class SidearmHandPoseLibrary:
    """Source-authored finger poses stored relative to their parent bones."""

    relative_pose: dict[str, dict[str, Matrix]]


# Coordinates are source-rig units in WeaponRoot space.  The exported root
# applies SOURCE_TO_METERS, matching the existing authored-arm adaptations.
# Each entry is intentionally platform-specific rather than a shared family
# guess so a runtime adapter can select a stable, named clip per weapon.
PROFILES = (
    ReloadProfile("m4a1", "straight_rifle", (5.4, -31.0, 3.2),
                  (4.0, -10.0, -9.0), (13.0, -4.0, -20.0),
                  (3.0, -11.0, -4.0), (2.0, -4.0, 10.0),
                  (48.0, 1.0, -27.0)),
    ReloadProfile("ak74", "rock_and_lock", (5.4, -31.0, 3.2),
                  (7.0, -13.0, -12.0), (15.0, -5.0, -22.0),
                  (8.0, -13.0, -5.0), (13.0, -10.0, 8.0),
                  (50.0, -1.0, -28.0)),
    ReloadProfile("scarl", "straight_rifle", (5.4, -31.0, 3.2),
                  (4.0, -11.0, -10.0), (13.0, -4.0, -21.0),
                  (4.0, -12.0, -4.0), (12.0, -14.0, 9.0),
                  (48.0, 0.0, -27.0)),
    ReloadProfile("mp5a5", "mp5", (5.4, -28.0, 3.5),
                  (3.0, -10.0, -12.0), (12.0, -4.0, -21.0),
                  (3.0, -10.0, -5.0), (12.0, -19.0, 11.0),
                  (47.0, 2.0, -27.0)),
    ReloadProfile("m24", "internal_precision", (5.0, -39.0, 3.0),
                  (4.0, -9.0, -10.0), (12.0, -7.0, -19.0),
                  (4.0, -10.0, -4.0), (9.0, -4.0, 10.0),
                  (50.0, 0.0, -29.0), tactical_action=True),
    ReloadProfile("axmc", "precision", (5.0, -40.0, 3.0),
                  (4.0, -10.0, -11.0), (13.0, -8.0, -21.0),
                  (4.0, -11.0, -4.0), (10.0, -5.0, 11.0),
                  (51.0, 0.0, -29.0)),
    ReloadProfile("awm", "precision", (5.0, -42.0, 3.0),
                  (5.0, -10.0, -12.0), (14.0, -9.0, -22.0),
                  (5.0, -11.0, -5.0), (10.0, -5.0, 11.0),
                  (52.0, -1.0, -30.0)),
    ReloadProfile("vss", "rock_and_lock", (5.0, -35.0, 3.0),
                  (5.0, -12.0, -11.0), (14.0, -5.0, -21.0),
                  (5.0, -12.0, -4.0), (12.0, -11.0, 9.0),
                  (49.0, 0.0, -28.0)),
    ReloadProfile("p226", "service_pistol", (3.0, 0.0, 5.0),
                  (-2.0, -2.0, -5.0), (0.0, 0.0, -11.0),
                  (-2.0, -2.0, -2.0), (0.0, -6.0, 7.0),
                  (40.0, 18.0, -23.0), True),
    ReloadProfile("m1911", "service_pistol", (3.0, 0.0, 5.0),
                  (-3.0, -2.0, -5.0), (-1.0, 0.0, -11.0),
                  (-3.0, -2.0, -2.0), (0.0, -6.0, 7.0),
                  (40.0, 18.0, -23.0), True),
    ReloadProfile("gsh18", "service_pistol", (3.0, 0.0, 5.0),
                  (-2.0, -3.0, -5.0), (0.0, -1.0, -11.0),
                  (-2.0, -3.0, -2.0), (0.0, -6.0, 7.0),
                  (40.0, 18.0, -23.0), True),
    ReloadProfile("desert_eagle", "desert_eagle", (-7.0, -10.5, 1.0),
                  (-4.0, -3.0, -6.0), (-3.75, -1.75, -9.0),
                  (-4.0, -3.0, -2.0), (-3.5, -11.5, 5.5),
                  (41.0, 16.0, -25.0), True),
)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def bone_world_matrix(armature: bpy.types.Object, bone_name: str) -> Matrix:
    return armature.matrix_world @ armature.pose.bones[bone_name].matrix


def evaluated_vertices(source: bpy.types.Object) -> list[Vector]:
    evaluated = source.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        if len(mesh.vertices) != len(source.data.vertices):
            raise RuntimeError("Authored arm evaluation changed vertex topology")
        return [vertex.co.copy() for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()


def evaluated_hand_surface(
    source: bpy.types.Object,
    palm_origin: Vector,
) -> tuple[BVHTree, int, int]:
    """Build a BVH from the two disconnected glove pieces nearest a palm."""
    palm_local = source.matrix_world.inverted() @ palm_origin
    components = evaluated_component_centers(source)
    glove_components = sorted(
        components,
        key=lambda item: (item[1] - palm_local).length,
    )[:2]
    glove_vertices = {
        index
        for _, _, component_vertices in glove_components
        for index in component_vertices
    }
    if len(glove_components) != 2 or len(glove_vertices) < 1_000:
        raise RuntimeError("Could not isolate the authored glove surface")

    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        vertices = [
            evaluated.matrix_world @ vertex.co
            for vertex in mesh.vertices
        ]
        polygons = [
            tuple(polygon.vertices)
            for polygon in mesh.polygons
            if all(index in glove_vertices for index in polygon.vertices)
        ]
        if len(polygons) < 1_000:
            raise RuntimeError("Authored glove surface contains too few faces")
        surface = BVHTree.FromPolygons(
            vertices,
            polygons,
            all_triangles=False,
        )
    finally:
        evaluated.to_mesh_clear()
    if surface is None:
        raise RuntimeError("Could not build the authored glove surface BVH")
    return surface, len(glove_vertices), len(polygons)


def sidearm_magazine_anchor_frame(
    source: bpy.types.Object,
    palm_frame: Matrix,
) -> Matrix:
    """Resolve the fingertip-side glove surface used to hold a magazine.

    Aim below and forward from the evaluated palm center.  The signed lateral
    offset is chosen from the actual first-person camera projection so aligning
    this marker to the magazine leaves the visible support palm screen-left of
    the firing hand.  Snap the target to an actual authored glove triangle.
    """
    surface, vertex_count, face_count = evaluated_hand_surface(
        source,
        palm_frame.translation,
    )
    surface_target = palm_frame.translation + Vector(
        (
            SIDEARM_ANCHOR_TARGET_LATERAL_METERS / SOURCE_TO_METERS,
            -SIDEARM_ANCHOR_TARGET_FORWARD_METERS / SOURCE_TO_METERS,
            -SIDEARM_ANCHOR_TARGET_BELOW_METERS / SOURCE_TO_METERS,
        )
    )
    location, normal, face_index, snap_distance = surface.find_nearest(
        surface_target,
    )
    if location is None or normal is None or face_index is None:
        raise RuntimeError("Could not project the sidearm anchor onto the glove")
    anchor = frame_at(location, palm_frame)
    _, _, _, surface_error = surface.find_nearest(anchor.translation)
    palm_delta = palm_frame.translation - anchor.translation
    anchor_below_palm = palm_delta.z * SOURCE_TO_METERS
    anchor_lateral = -palm_delta.x * SOURCE_TO_METERS
    anchor_forward = palm_delta.y * SOURCE_TO_METERS
    anchor_offset = palm_delta.length * SOURCE_TO_METERS
    valid = (
        surface_error * SOURCE_TO_METERS
            <= MAX_SIDEARM_ANCHOR_SURFACE_ERROR_METERS
        and MIN_SIDEARM_ANCHOR_BELOW_PALM_METERS
            <= anchor_below_palm
            <= MAX_SIDEARM_ANCHOR_BELOW_PALM_METERS
        and MIN_SIDEARM_ANCHOR_LATERAL_METERS
            <= anchor_lateral
            <= MAX_SIDEARM_ANCHOR_LATERAL_METERS
        and MIN_SIDEARM_ANCHOR_FORWARD_METERS
            <= anchor_forward
            <= MAX_SIDEARM_ANCHOR_FORWARD_METERS
        and MIN_SIDEARM_MAGAZINE_ANCHOR_OFFSET_METERS
            <= anchor_offset
            <= MAX_SIDEARM_MAGAZINE_ANCHOR_OFFSET_METERS
        and snap_distance * SOURCE_TO_METERS
            <= MAX_SIDEARM_ANCHOR_TARGET_SNAP_METERS
    )
    print(
        "SIDEARM_MAGAZINE_ANCHOR"
        f" glove_vertices={vertex_count}"
        f" glove_faces={face_count}"
        f" face={face_index}"
        f" surface_error={surface_error * SOURCE_TO_METERS:.9f}"
        f" target_snap={snap_distance * SOURCE_TO_METERS:.6f}"
        f" below_palm={anchor_below_palm:.6f}"
        f" lateral={anchor_lateral:.6f}"
        f" forward={anchor_forward:.6f}"
        f" palm_offset={anchor_offset:.6f}"
        f" normal={normal.x:.4f}/{normal.y:.4f}/{normal.z:.4f}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError("Sidearm magazine anchor is not on the lower glove surface")
    return anchor


def create_forearm_crop(
    source: bpy.types.Object,
    object_name: str,
    cuff_length: float,
    report_name: str,
) -> bpy.types.Object:
    """Create a skinned forearm copy without exposing either upper arm.

    The two largest disconnected source components are the authored cloth
    sleeves. Bisect only those components in the source bind pose, retaining
    the complete gloves/hands and a role-specific length of authored cuff.
    BMesh interpolates the existing UV and deform layers at the local cut, so
    this does not need the broad sleeve-cap operation used by other assets.
    """
    result = source.copy()
    result.data = source.data.copy()
    result.name = object_name
    result.data.name = f"{object_name}Data"
    source.users_collection[0].objects.link(result)

    mesh = result.data
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        bm.verts.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        bm.faces.ensure_lookup_table()

        unseen = {vertex.index: vertex for vertex in bm.verts}
        components: list[set[bmesh.types.BMVert]] = []
        while unseen:
            seed_index = min(unseen)
            seed = unseen.pop(seed_index)
            component = {seed}
            stack = [seed]
            while stack:
                vertex = stack.pop()
                neighbors = sorted(
                    (edge.other_vert(vertex) for edge in vertex.link_edges),
                    key=lambda item: item.index,
                )
                for neighbor in neighbors:
                    if neighbor.index in unseen:
                        unseen.pop(neighbor.index)
                        component.add(neighbor)
                        stack.append(neighbor)
            components.append(component)

        sleeve_components = sorted(
            components,
            key=lambda item: (-len(item), min(vertex.index for vertex in item)),
        )[:2]
        if len(sleeve_components) != 2 or any(
            len(component) < 1_500 for component in sleeve_components
        ):
            raise RuntimeError("Could not identify both authored sleeve components")

        cut_reports = []
        for component in sleeve_components:
            minimum_y = min(vertex.co.y for vertex in component)
            cut_y = minimum_y + cuff_length
            component_edges = {
                edge for vertex in component for edge in vertex.link_edges
            }
            component_faces = {
                face for vertex in component for face in vertex.link_faces
            }
            geometry = [
                *sorted(component, key=lambda item: item.index),
                *sorted(component_edges, key=lambda item: item.index),
                *sorted(component_faces, key=lambda item: item.index),
            ]
            result_geometry = bmesh.ops.bisect_plane(
                bm,
                geom=geometry,
                dist=0.00001,
                plane_co=Vector((0.0, cut_y, 0.0)),
                plane_no=Vector((0.0, 1.0, 0.0)),
                clear_outer=True,
                clear_inner=False,
            )
            cut_reports.append((cut_y, len(result_geometry.get("geom_cut", []))))

        bm.normal_update()
        bm.to_mesh(mesh)
        mesh.update()
    finally:
        bm.free()

    if tuple(group.name for group in result.vertex_groups) != tuple(
        group.name for group in source.vertex_groups
    ):
        raise RuntimeError(f"{report_name} crop lost authored skin groups")
    if tuple(layer.name for layer in mesh.uv_layers) != tuple(
        layer.name for layer in source.data.uv_layers
    ):
        raise RuntimeError(f"{report_name} crop lost authored UV layers")
    if tuple(mesh.materials) != tuple(source.data.materials):
        raise RuntimeError(f"{report_name} crop lost authored materials")
    weight_sums = [
        sum(group.weight for group in vertex.groups) for vertex in mesh.vertices
    ]
    if not weight_sums or min(weight_sums) < 0.999 or max(weight_sums) > 1.001:
        raise RuntimeError(f"{report_name} crop produced invalid skin weights")
    print(
        f"{report_name}_CROP"
        f" vertices={len(mesh.vertices)}"
        f" triangles={sum(len(polygon.vertices) - 2 for polygon in mesh.polygons)}"
        f" cuff_length={cuff_length:.3f}"
        f" cut_y={'/'.join(f'{cut_y:.6f}' for cut_y, _ in cut_reports)}"
        f" cut_elements={'/'.join(str(count) for _, count in cut_reports)}"
        f" uv_layers={len(mesh.uv_layers)}"
        f" material_slots={len(mesh.materials)}"
        f" valid=True"
    )
    return result


def create_long_gun_forearms(source: bpy.types.Object) -> bpy.types.Object:
    """Keep complete hands plus a camera-safe elbow-length rifle cuff."""
    return create_forearm_crop(
        source,
        "LongGunReloadForearmsMesh",
        LONG_GUN_FOREARM_CUFF_LENGTH,
        "LONG_GUN_FOREARM",
    )


def create_sidearm_forearms(source: bpy.types.Object) -> bpy.types.Object:
    """Keep complete hands plus the compact pistol cuff."""
    return create_forearm_crop(
        source,
        "SidearmReloadForearmsMesh",
        SIDEARM_FOREARM_CUFF_LENGTH,
        "SIDEARM_FOREARM",
    )


def capture_sidearm_hand_pose_library(
    armature: bpy.types.Object,
) -> SidearmHandPoseLibrary:
    """Retain the source animator's real open, pinch, wrap, and rack hand poses.

    Storing each finger transform relative to its animated parent makes the
    hand performance independent of the source SMG arm motion.  The poses can
    then be placed on the newly solved pistol wrist without approximating a
    grasp from Euler guesses or leaving the original firing hand frozen.
    """
    scene = bpy.context.scene
    original_frame = scene.frame_current
    relative_pose: dict[str, dict[str, Matrix]] = {}
    for pose_name, frame in SIDEARM_HAND_SOURCE_FRAMES.items():
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        relative_pose[pose_name] = {
            bone_name: (
                armature.pose.bones[bone_name].parent.matrix.inverted()
                @ armature.pose.bones[bone_name].matrix
            )
            for bone_name in LEFT_HAND_BONES
        }
    magazine_grasp = {
        bone_name: matrix.copy()
        for bone_name, matrix in relative_pose["wrap"].items()
    }
    finger_chains = (
        ("L_point1_07", "L_point2_08", "L_point3_09"),
        ("L_middle1_011", "L_middle2_012", "L_middle3_013"),
        ("L_ring1_016", "L_ring2_017", "L_ring3_018"),
        ("L_pink1_020", "L_pink2_021", "L_pink3_022"),
    )
    for chain in finger_chains:
        for bone_name, degrees in zip(chain, (-50.0, -45.0, -32.5)):
            magazine_grasp[bone_name] = (
                magazine_grasp[bone_name]
                @ Matrix.Rotation(math.radians(degrees), 4, "X")
            )
    for bone_name, degrees in zip(
        ("L_thumb1_04", "L_thumb2_05", "L_thumb3_00"),
        (-25.0, -22.5, -16.25),
    ):
        magazine_grasp[bone_name] = (
            magazine_grasp[bone_name]
            @ Matrix.Rotation(math.radians(degrees), 4, "Z")
        )
    relative_pose["magazine_grasp"] = magazine_grasp
    scene.frame_set(original_frame)
    bpy.context.view_layer.update()
    print(
        "SIDEARM_HAND_POSE_LIBRARY"
        f" poses={len(relative_pose)}"
        f" bones={len(LEFT_HAND_BONES)}"
        f" source_frames={','.join(str(value) for value in SIDEARM_HAND_SOURCE_FRAMES.values())}"
        " valid=True"
    )
    return SidearmHandPoseLibrary(relative_pose)


def import_and_prepare_source() -> tuple[
    bpy.types.Object,
    bpy.types.Object,
    Matrix,
    Matrix,
    Matrix,
    Matrix,
    Matrix,
    SidearmHandPoseLibrary,
]:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_set(SOURCE_IDLE_FRAME)
    bpy.context.view_layer.update()
    armature = bpy.data.objects["Object_4"]
    arms_mesh = bpy.data.objects["Object_7"]
    sidearm_hand_poses = capture_sidearm_hand_pose_library(armature)
    scene.frame_set(SOURCE_IDLE_FRAME)
    bpy.context.view_layer.update()
    refine_authored_sleeves()
    # Crop the runtime layers from the authored sleeve before the audit-only
    # shoulder extension is added.  Cropping the extended mesh retained a
    # straight, oversized tube at the cut and made the MP5/AK support forearm
    # look detached whenever the wrist crossed the lower viewport edge.
    long_gun_arms_mesh = create_long_gun_forearms(arms_mesh)
    sidearm_arms_mesh = create_sidearm_forearms(arms_mesh)
    extend_authored_sleeves()
    bpy.context.view_layer.update()
    components = evaluated_component_centers(arms_mesh)
    right_palm = bone_world_matrix(armature, RIGHT_PALM)
    left_palm = bone_world_matrix(armature, LEFT_PALM)
    right_contact = frame_at(
        hand_contact_center(components, right_palm.translation),
        right_palm,
    )
    left_contact = frame_at(
        hand_contact_center(components, left_palm.translation),
        left_palm,
    )
    weapon_grip = bpy.data.objects["smg45"].matrix_world.copy()
    left_grip_anchor = weapon_cross_section_frame(
        weapon_grip,
        left_contact.translation,
    )
    left_sidearm_magazine_anchor = sidearm_magazine_anchor_frame(
        arms_mesh,
        left_contact,
    )
    bind_vertices = evaluated_vertices(arms_mesh)
    long_gun_bind_vertices = evaluated_vertices(long_gun_arms_mesh)
    sidearm_bind_vertices = evaluated_vertices(sidearm_arms_mesh)

    # Make the authored two-hand firing pose the new bind/rest pose.  This
    # preserves the evaluated appearance and real skin weights while making
    # every untouched bone (especially the right arm) immutable by default.
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    # pose.armature_apply changes the bone rest matrices but leaves the mesh's
    # undeformed T-pose coordinates behind.  Bake the evaluated frame-155
    # surface into those same vertices so the new bind pose keeps the authored
    # grip, fingers, materials, topology, and weights.
    for vertex, position in zip(arms_mesh.data.vertices, bind_vertices):
        vertex.co = position
    arms_mesh.data.update()
    for vertex, position in zip(
        long_gun_arms_mesh.data.vertices,
        long_gun_bind_vertices,
    ):
        vertex.co = position
    long_gun_arms_mesh.data.update()
    for vertex, position in zip(
        sidearm_arms_mesh.data.vertices,
        sidearm_bind_vertices,
    ):
        vertex.co = position
    sidearm_arms_mesh.data.update()
    for obj in bpy.context.scene.objects:
        if obj.animation_data is not None:
            obj.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
        pose_bone.custom_shape = None
    bpy.context.view_layer.update()
    rebound_vertices = evaluated_vertices(arms_mesh)
    bind_surface_error = max(
        (actual - expected).length
        for actual, expected in zip(rebound_vertices, bind_vertices)
    ) * SOURCE_TO_METERS
    print(
        "RELOAD_BIND_CHECK"
        f" vertices={len(bind_vertices)}"
        f" surface_max={bind_surface_error:.9f}"
        f" valid={bind_surface_error <= MAX_BIND_SURFACE_ERROR_METERS}"
    )
    if bind_surface_error > MAX_BIND_SURFACE_ERROR_METERS:
        raise RuntimeError("Frame-155 authored arm surface was not preserved")

    # Rebuild a minimal hierarchy with no visible source weapon geometry.
    root = bpy.data.objects.new("WeaponRoot", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 5.0
    bpy.context.collection.objects.link(root)
    armature.name = "ReloadArmsSkeleton"
    armature.data.name = "ReloadArmsSkeletonData"
    arms_mesh.name = "FullReloadArmsAuditMesh"
    arms_mesh.data.name = "FullReloadArmsAuditMeshData"
    armature.parent = root
    armature.matrix_parent_inverse.identity()
    armature.matrix_basis.identity()
    compatibility_layer = bpy.data.objects.new("ReloadArmsMesh", None)
    compatibility_layer.empty_display_type = "PLAIN_AXES"
    compatibility_layer.empty_display_size = 1.0
    bpy.context.collection.objects.link(compatibility_layer)
    compatibility_layer.parent = root
    compatibility_layer.matrix_parent_inverse.identity()
    compatibility_layer.matrix_basis.identity()
    compatibility_layer["presentation_role"] = (
        "long_gun_forearms_compatibility_layer"
    )
    arms_mesh["presentation_role"] = "full_arms_non_runtime_audit"
    long_gun_arms_mesh["presentation_role"] = "long_gun_forearms_runtime"
    sidearm_arms_mesh["presentation_role"] = "sidearm_forearms_runtime"
    retained = {
        root,
        armature,
        arms_mesh,
        long_gun_arms_mesh,
        sidearm_arms_mesh,
        compatibility_layer,
    }
    for obj in list(bpy.context.scene.objects):
        if obj not in retained:
            bpy.data.objects.remove(obj, do_unlink=True)
    for visible_mesh in (arms_mesh, long_gun_arms_mesh, sidearm_arms_mesh):
        if visible_mesh.parent is not armature:
            raise RuntimeError(
                f"{visible_mesh.name} lost its authored armature parent"
            )
        if not visible_mesh.vertex_groups or not visible_mesh.data.materials:
            raise RuntimeError(
                f"{visible_mesh.name} lost authored skin weights or materials"
            )
    return (
        root,
        armature,
        weapon_grip,
        right_contact,
        left_contact,
        left_grip_anchor,
        left_sidearm_magazine_anchor,
        sidearm_hand_poses,
    )


def eased_keyframes(target: bpy.types.Object) -> None:
    action = target.animation_data.action
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"
            key.handle_left_type = "AUTO_CLAMPED"
            key.handle_right_type = "AUTO_CLAMPED"


def reset_armature_pose(armature: bpy.types.Object) -> None:
    if armature.animation_data is not None:
        armature.animation_data.action = None
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    bpy.context.view_layer.update()


def capture_sidearm_static_pose(
    armature: bpy.types.Object,
    kind: str,
) -> SidearmStaticPose:
    """Evaluate the same pistol pose used by the static first-person asset.

    Calling the shared DCC pose builder avoids a second approximation of its
    service-pistol shoulder offset, support-hand pole, and large-pistol grip.
    The evaluated matrices are converted back to constraint-free local bases so
    each reload clip can use the exact static pose beneath its magazine IK.
    """
    reset_armature_pose(armature)
    scene = bpy.context.scene
    scene.frame_set(SOURCE_IDLE_FRAME)
    bpy.context.view_layer.update()
    original_name = armature.name
    existing_objects = set(bpy.data.objects.keys())
    existing_constraints = {
        pose_bone.name: {constraint.name for constraint in pose_bone.constraints}
        for pose_bone in armature.pose.bones
    }
    try:
        armature.name = "Object_4"
        evaluate_static_arm_pose(kind)
        bpy.context.view_layer.update()
        global_pose = {
            bone_name: armature.pose.bones[bone_name].matrix.copy()
            for bone_name in LEFT_CHAIN
        }
    finally:
        armature.name = original_name
        for pose_bone in armature.pose.bones:
            retained = existing_constraints[pose_bone.name]
            for constraint in list(pose_bone.constraints):
                if constraint.name not in retained:
                    pose_bone.constraints.remove(constraint)
        for object_name in list(bpy.data.objects.keys()):
            if object_name not in existing_objects:
                bpy.data.objects.remove(
                    bpy.data.objects[object_name],
                    do_unlink=True,
                )

    reset_armature_pose(armature)
    for bone_name in LEFT_CHAIN:
        armature.pose.bones[bone_name].matrix = global_pose[bone_name]
        bpy.context.view_layer.update()
    local_basis = {
        bone_name: armature.pose.bones[bone_name].matrix_basis.copy()
        for bone_name in LEFT_CHAIN
    }
    reconstructed = {
        bone_name: armature.pose.bones[bone_name].matrix.copy()
        for bone_name in LEFT_CHAIN
    }
    maximum_basis_error = max(
        shortest_rotation_error(global_pose[name], reconstructed[name])
        for name in LEFT_CHAIN
    )
    maximum_position_error = max(
        (global_pose[name].translation - reconstructed[name].translation).length
        for name in LEFT_CHAIN
    ) * SOURCE_TO_METERS
    valid = (
        maximum_basis_error <= SIDEARM_ENDPOINT_MAX_BASIS_ERROR_RADIANS
        and maximum_position_error <= SIDEARM_ENDPOINT_MAX_POSITION_ERROR_METERS
    )
    print(
        "SIDEARM_STATIC_REFERENCE"
        f" kind={kind}"
        f" basis_error={maximum_basis_error:.9f}"
        f" position_error={maximum_position_error:.9f}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError(f"Could not preserve the {kind} static arm pose")
    reset_armature_pose(armature)
    scene.frame_set(0)
    bpy.context.view_layer.update()
    return SidearmStaticPose(local_basis, global_pose)


def apply_sidearm_static_pose(
    armature: bpy.types.Object,
    static_pose: SidearmStaticPose,
) -> None:
    for bone_name in LEFT_CHAIN:
        armature.pose.bones[bone_name].matrix_basis = static_pose.local_basis[
            bone_name
        ].copy()
    bpy.context.view_layer.update()


def remove_baked_constraint_curves(action: bpy.types.Action) -> None:
    for curve in list(action.fcurves):
        if ".constraints[" in curve.data_path:
            action.fcurves.remove(curve)


def control_point(
    fraction: float,
    location: Vector,
    rotation_degrees: Vector | tuple[float, float, float],
    beat: str = "",
) -> ReloadControlPoint:
    return ReloadControlPoint(
        fraction,
        location.copy(),
        Vector(rotation_degrees),
        beat,
    )


def magazine_pose_degrees(profile: ReloadProfile) -> tuple[Vector, ...]:
    """Return family-specific wrist poses for extraction through seating."""
    if profile.family == "rock_and_lock":
        return tuple(
            Vector(value)
            for value in (
                (36.0, -8.0, 22.0),
                (45.0, -10.0, 34.0),
                (42.0, -8.0, 30.0),
                (38.0, -6.0, 25.0),
                (32.0, -4.0, 18.0),
                (20.0, -2.0, 10.0),
            )
        )
    if profile.family == "mp5":
        return tuple(
            Vector(value)
            for value in (
                (30.0, 0.0, 10.0),
                (35.0, 2.0, 16.0),
                (34.0, 2.0, 15.0),
                (30.0, 1.0, 11.0),
                (25.0, 0.0, 8.0),
                (18.0, -1.0, 5.0),
            )
        )
    if profile.family in {"precision", "internal_precision"}:
        return tuple(
            Vector(value)
            for value in (
                (25.0, 0.0, 7.0),
                (31.0, 2.0, 12.0),
                (30.0, 2.0, 12.0),
                (27.0, 1.0, 9.0),
                (22.0, 0.0, 6.0),
                (16.0, -1.0, 4.0),
            )
        )
    return tuple(
        Vector(value)
        for value in (
            (28.0, 1.0, 8.0),
            (34.0, 2.0, 13.0),
            (33.0, 2.0, 13.0),
            (29.0, 1.0, 10.0),
            (24.0, 0.0, 7.0),
            (18.0, -1.0, 5.0),
        )
    )


def mechanical_control_points(
    profile: ReloadProfile,
) -> list[ReloadControlPoint]:
    """Author a visible contact-pull-hold-release mechanical beat."""
    action = Vector(profile.action)
    if profile.family == "mp5":
        contact = action + Vector((-1.0, 1.0, -1.0))
        peak = action + Vector((0.0, 6.0, 0.0))
        release = action + Vector((-2.0, -2.0, 3.0))
        rotations = (
            Vector((-10.0, -18.0, -18.0)),
            Vector((-16.0, -28.0, -24.0)),
            Vector((-20.0, -20.0, -16.0)),
        )
    elif profile.family in {"service_pistol", "desert_eagle"}:
        contact = action
        slide_travel = 2.56 if profile.family == "desert_eagle" else 4.1
        peak = action + Vector((0.0, slide_travel, 0.0))
        # The support hand opens at full rearward travel; the recoil spring
        # sends the slide forward without dragging the hand along with it.
        release = peak
        rotations = (
            Vector(SIDEARM_ACTION_WRIST_POSES[profile.name]),
            Vector(SIDEARM_ACTION_WRIST_POSES[profile.name])
            + Vector((-3.0, -7.0, -3.0)),
            Vector(SIDEARM_ACTION_WRIST_POSES[profile.name])
            + Vector((-3.0, -7.0, -3.0)),
        )
        fractions = (
            (0.859375, 0.890625, 0.906250, 0.921875)
            if profile.family == "desert_eagle"
            else (0.906250, 0.921875, 0.937500, 0.984375)
        )
    elif profile.family in {"precision", "internal_precision"}:
        contact = action
        peak = action + Vector((0.0, 4.5, 0.0))
        release = action
        rotations = (
            Vector((7.0, -12.0, -4.0)),
            Vector((3.0, -22.0, -8.0)),
            Vector((7.0, -12.0, -4.0)),
        )
    else:
        contact = action
        peak = action + Vector((0.0, 5.5, 0.0))
        release = action
        rotations = (
            Vector((8.0, -10.0, -3.0)),
            Vector((4.0, -20.0, -8.0)),
            Vector((8.0, -10.0, -3.0)),
        )
    if profile.family not in {"service_pistol", "desert_eagle"}:
        fractions = (0.840, 0.870, 0.895, 0.915)
    return [
        control_point(fractions[0], contact, rotations[0], "action_contact"),
        control_point(fractions[1], peak, rotations[1], "action_peak"),
        control_point(fractions[2], peak, rotations[1], "action_peak_hold"),
        control_point(fractions[3], release, rotations[2], "action_release"),
    ]


def append_camera_safe_return(
    points: list[ReloadControlPoint],
    support: Vector,
    sidearm: bool = False,
) -> None:
    """Split the action-to-support return so no sleeve crosses in one frame."""
    release = points[-1]
    if sidearm:
        # The analytical solver already fades its effective target back to the
        # exact static wrist. One uncluttered Bezier return avoids multiplying
        # that fade by a stepped intermediate control and keeps the crop calm.
        points.append(control_point(1.000, support, Vector()))
        return
    points.append(
        control_point(
            0.955,
            release.location.lerp(support, 0.52),
            release.rotation_degrees.lerp(Vector(), 0.52),
            "return_clear",
        )
    )
    points.append(control_point(1.000, support, Vector()))


def sidearm_control_points(
    profile: ReloadProfile,
    empty: bool,
) -> list[ReloadControlPoint]:
    """Build a compact pistol exchange on the cropped authored forearms.

    Each location is the physical palm contact point, not the wrist origin.
    The bake solves the wrist from that surface point and layers the source
    animator's finger performance onto it. Runtime therefore plays a complete
    grasp rather than translating the hidden shoulder until a frozen fist is
    near the prop. Empty clips add an authored overhand slide beat after seat.
    """
    magazine_anchor = Vector(profile.magazine)
    old_clear = magazine_anchor + Vector((0.0, 1.0, -4.0))
    exchange = Vector(profile.exchange)
    insert = Vector(profile.insert)
    approach = insert + Vector((1.0, 2.0, -4.0))
    seated = magazine_anchor
    try:
        wrist_pose = Vector(SIDEARM_MAGAZINE_WRIST_POSES[profile.name])
    except KeyError as error:
        raise RuntimeError(
            f"Missing sidearm magazine wrist pose: {profile.name}"
        ) from error
    reach_end = sidearm_reach_end(profile)
    seat_fraction = 0.750 if profile.name == "desert_eagle" else 0.780
    seat_hold_fraction = (
        0.765625 if profile.name == "desert_eagle" else 0.795
    )
    points = [
        control_point(0.000, magazine_anchor, Vector()),
        control_point(0.040, magazine_anchor, Vector()),
        control_point(reach_end, magazine_anchor, wrist_pose, "old_mag_grip"),
        control_point(0.310, old_clear, wrist_pose + Vector((3.0, 1.0, 3.0)),
                      "old_mag_out"),
        control_point(0.340, old_clear, wrist_pose + Vector((3.0, 1.0, 3.0)),
                      "old_mag_out_hold"),
        control_point(0.430, exchange, wrist_pose + Vector((2.0, 0.0, 2.0))),
        control_point(0.540, exchange, wrist_pose + Vector((2.0, 0.0, 2.0)),
                      "fresh_mag_ready"),
        control_point(0.630, approach, wrist_pose),
        control_point(0.710, insert, wrist_pose, "new_mag_insert"),
        control_point(seat_fraction, seated,
                      wrist_pose - Vector((4.0, 1.0, 3.0)),
                      "new_mag_seat"),
        control_point(seat_hold_fraction, seated,
                      wrist_pose - Vector((4.0, 1.0, 3.0)),
                      "new_mag_seat_hold"),
    ]
    if empty:
        mechanical = mechanical_control_points(profile)
        contact = mechanical[0]
        seated_rotation = wrist_pose - Vector((4.0, 1.0, 3.0))
        if profile.name == "desert_eagle":
            approach_samples = (
                control_point(
                    0.781250,
                    seated.lerp(contact.location, 1.0 / 6.0),
                    seated_rotation.lerp(
                        contact.rotation_degrees,
                        1.0 / 6.0,
                    ),
                    "action_approach_early",
                ),
                control_point(
                    0.796875,
                    seated.lerp(contact.location, 2.0 / 6.0),
                    seated_rotation.lerp(
                        contact.rotation_degrees,
                        2.0 / 6.0,
                    ),
                    "action_approach_middle",
                ),
                control_point(
                    0.812500,
                    seated.lerp(contact.location, 3.0 / 6.0),
                    seated_rotation.lerp(
                        contact.rotation_degrees,
                        3.0 / 6.0,
                    ),
                    "action_approach_late",
                ),
                control_point(
                    0.828125,
                    seated.lerp(contact.location, 4.0 / 6.0),
                    seated_rotation.lerp(
                        contact.rotation_degrees,
                        4.0 / 6.0,
                    ),
                    "action_approach_contact",
                ),
                control_point(
                    0.843750,
                    seated.lerp(contact.location, 5.0 / 6.0),
                    seated_rotation.lerp(
                        contact.rotation_degrees,
                        5.0 / 6.0,
                    ),
                ),
            )
        else:
            approach_samples = (
                control_point(
                    0.812500,
                    seated.lerp(contact.location, 0.14),
                    seated_rotation.lerp(contact.rotation_degrees, 0.14),
                    "action_approach_early",
                ),
                control_point(
                    0.828125,
                    seated.lerp(contact.location, 0.29),
                    seated_rotation.lerp(contact.rotation_degrees, 0.29),
                    "action_approach_middle",
                ),
                control_point(
                    0.843750,
                    seated.lerp(contact.location, 0.43),
                    seated_rotation.lerp(contact.rotation_degrees, 0.43),
                    "action_approach_late",
                ),
                control_point(
                    0.859375,
                    seated.lerp(contact.location, 0.57),
                    seated_rotation.lerp(contact.rotation_degrees, 0.57),
                    "action_approach_contact",
                ),
                control_point(
                    0.875000,
                    seated.lerp(contact.location, 0.71),
                    seated_rotation.lerp(contact.rotation_degrees, 0.71),
                ),
                control_point(
                    0.890625,
                    seated.lerp(contact.location, 0.86),
                    seated_rotation.lerp(contact.rotation_degrees, 0.86),
                ),
            )
        points.extend(approach_samples)
        points.extend(mechanical)
        append_camera_safe_return(points, magazine_anchor, sidearm=True)
    else:
        points.append(
            control_point(0.860, seated,
                          wrist_pose - Vector((4.0, 1.0, 3.0)))
        )
        points.append(control_point(1.000, magazine_anchor, Vector()))
    return points


def box_magazine_control_points(
    profile: ReloadProfile,
    empty: bool,
) -> list[ReloadControlPoint]:
    support = Vector(profile.support)
    magazine = Vector(profile.magazine)
    exchange = Vector(profile.exchange)
    insert = Vector(profile.insert)
    if profile.family == "rock_and_lock":
        old_clear = magazine + Vector((7.0, 3.0, -3.0))
        fresh_ready = insert + Vector((7.0, 4.0, -7.0))
        approach = insert + Vector((3.0, 3.0, -4.0))
    elif profile.family == "mp5":
        old_clear = magazine + Vector((4.0, 2.0, -8.0))
        fresh_ready = insert + Vector((6.0, 4.0, -8.0))
        approach = insert + Vector((2.0, 3.0, -5.0))
    else:
        old_clear = magazine + Vector((4.0, 2.0, -8.0))
        fresh_ready = insert + Vector((6.0, 4.0, -8.0))
        approach = insert + Vector((2.0, 3.0, -5.0))
    seated = insert + Vector((0.0, -1.0, 3.0))
    magazine_pose, old_pose, exchange_pose, ready_pose, insert_pose, seat_pose = (
        magazine_pose_degrees(profile)
    )
    points = [
        control_point(0.000, support, Vector()),
        control_point(0.120, magazine, magazine_pose),
        control_point(0.270, old_clear, old_pose, "old_mag_out"),
        control_point(0.320, old_clear, old_pose, "old_mag_out_hold"),
        control_point(0.420, exchange, exchange_pose),
        control_point(0.530, fresh_ready, ready_pose, "fresh_mag_ready"),
        control_point(0.680, approach, ready_pose),
        control_point(0.730, insert, insert_pose, "new_mag_insert"),
        control_point(0.750, seated, seat_pose, "new_mag_seat"),
        control_point(0.790, seated, seat_pose, "new_mag_seat_hold"),
    ]
    if empty or profile.tactical_action:
        points.extend(mechanical_control_points(profile))
        append_camera_safe_return(points, support)
    else:
        points.append(control_point(0.860, seated, seat_pose))
        points.append(control_point(1.000, support, Vector()))
    return points


def internal_magazine_control_points(
    profile: ReloadProfile,
) -> list[ReloadControlPoint]:
    """Feed the M24 from a compact cartridge path instead of a box-mag arc."""
    support = Vector(profile.support)
    reserve = Vector(profile.exchange)
    port = Vector(profile.magazine)
    insert = Vector(profile.insert)
    staged_round = port + Vector((6.0, 4.0, -6.0))
    approach = insert + Vector((3.0, 3.0, -4.0))
    seated = insert + Vector((0.0, -1.0, 2.5))
    magazine_pose, acquire_pose, ready_pose, _, insert_pose, seat_pose = (
        magazine_pose_degrees(profile)
    )
    points = [
        control_point(0.000, support, Vector()),
        control_point(0.120, reserve, acquire_pose, "ammunition_acquired"),
        control_point(0.280, reserve, acquire_pose,
                      "ammunition_acquired_hold"),
        control_point(0.420, staged_round, ready_pose, "fresh_round_ready"),
        control_point(0.560, port, magazine_pose),
        control_point(0.680, approach, ready_pose),
        control_point(0.730, insert, insert_pose, "new_round_insert"),
        control_point(0.750, seated, seat_pose, "new_round_seat"),
        control_point(0.790, seated, seat_pose, "new_round_seat_hold"),
    ]
    points.extend(mechanical_control_points(profile))
    append_camera_safe_return(points, support)
    return points


def control_points(
    profile: ReloadProfile,
    empty: bool,
) -> list[ReloadControlPoint]:
    if profile.sidearm:
        return sidearm_control_points(profile, empty)
    if profile.family == "internal_precision":
        return internal_magazine_control_points(profile)
    return box_magazine_control_points(profile, empty)


def validate_control_point_contract(
    profile: ReloadProfile,
    empty: bool,
    points: list[ReloadControlPoint],
) -> None:
    fractions = [point.fraction for point in points]
    if (
        not fractions
        or fractions[0] != 0.0
        or fractions[-1] != 1.0
        or any(right <= left for left, right in zip(fractions, fractions[1:]))
    ):
        raise RuntimeError(
            f"Reload control times are not strictly ordered: {profile.name}"
        )
    labels = {point.beat for point in points if point.beat}
    if profile.family == "internal_precision":
        required = {
            "ammunition_acquired", "ammunition_acquired_hold",
            "new_round_seat", "new_round_seat_hold",
        }
    else:
        required = {
            "old_mag_out", "old_mag_out_hold",
            "new_mag_seat", "new_mag_seat_hold",
        }
    if empty or profile.tactical_action:
        required |= {
            "action_contact", "action_peak", "action_peak_hold",
            "action_release",
        }
    missing = required - labels
    if missing:
        raise RuntimeError(
            f"Reload control beats missing for {profile.name}: {sorted(missing)}"
        )
    support = points[0].location
    maximum_excursion = max(
        (point.location - support).length for point in points
    ) * SOURCE_TO_METERS
    excursion_limit = (
        SIDEARM_MAX_CONTROL_EXCURSION_METERS
        if profile.sidearm
        else MAX_CONTROL_EXCURSION_METERS
    )
    valid = maximum_excursion <= excursion_limit
    print(
        "RELOAD_CONTROL_CHECK"
        f" profile={profile.name}"
        f" family={profile.family}"
        f" variant={'empty' if empty else 'tactical'}"
        f" keys={len(points)}"
        f" max_excursion={maximum_excursion:.4f}"
        f" limit={excursion_limit:.4f}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError(
            f"Reload control path leaves camera-safe envelope: {profile.name}"
        )


def create_control(
    name: str,
    points: list[ReloadControlPoint],
    base_rotation: Quaternion,
    end_frame: int,
    linear: bool = False,
) -> bpy.types.Object:
    target = bpy.data.objects.new(f"{name}_IKTarget", None)
    target.empty_display_type = "SPHERE"
    target.empty_display_size = 2.0
    bpy.context.collection.objects.link(target)
    target.rotation_mode = "QUATERNION"
    target.animation_data_create()
    target.animation_data.action = bpy.data.actions.new(f"{name}_controls")
    for point in points:
        frame = round(point.fraction * end_frame)
        target.location = point.location
        delta = Euler(
            tuple(math.radians(value) for value in point.rotation_degrees),
            "XYZ",
        )
        target.rotation_quaternion = base_rotation @ delta.to_quaternion()
        target.keyframe_insert("location", frame=frame)
        target.keyframe_insert("rotation_quaternion", frame=frame)
    if linear:
        for curve in target.animation_data.action.fcurves:
            for key in curve.keyframe_points:
                key.interpolation = "LINEAR"
    else:
        eased_keyframes(target)
    return target


def smooth_step(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def sidearm_reach_end(profile: ReloadProfile) -> float:
    """Give the physically larger Desert Eagle a longer grasp approach."""
    if profile.name == "desert_eagle":
        return SIDEARM_DESERT_EAGLE_BLEND_IN_END
    return SIDEARM_IK_BLEND_IN_END


def sidearm_pose_influence(
    fraction: float,
    empty: bool,
    reach_end: float = SIDEARM_IK_BLEND_IN_END,
    profile_name: str = "",
) -> float:
    blend_out_start = (
        0.890625
        if empty and profile_name == "desert_eagle"
        else SIDEARM_EMPTY_BLEND_OUT_START
        if empty
        else SIDEARM_TACTICAL_BLEND_OUT_START
    )
    if fraction <= SIDEARM_IK_BLEND_IN_START:
        return 0.0
    if fraction < reach_end:
        return smooth_step(
            (fraction - SIDEARM_IK_BLEND_IN_START)
            / (reach_end - SIDEARM_IK_BLEND_IN_START)
        )
    if fraction <= blend_out_start:
        return 1.0
    return_amount = (
        (fraction - blend_out_start) / (1.0 - blend_out_start)
    )
    # A linear authored return avoids the 1.5x mid-curve velocity spike of a
    # cubic smooth step. That spike was visible on the large Desert Eagle as
    # a late wrist snap even though both endpoints were correctly posed.
    return 1.0 - max(0.0, min(1.0, return_amount))


def sidearm_hand_pose_keys(
    empty: bool,
    reach_end: float = SIDEARM_IK_BLEND_IN_END,
    profile_name: str = "",
) -> tuple[tuple[float, str], ...]:
    """Stage a professional release, regrip, seat, and slide manipulation."""
    seat_hold = 0.765625 if profile_name == "desert_eagle" else 0.795
    magazine_exchange = (
        (0.000, "ready"),
        (0.045, "open"),
        (reach_end - 0.050, "pinch"),
        (reach_end, "magazine_grasp"),
        (0.410, "magazine_grasp"),
        (0.455, "open"),
        (0.500, "open"),
        (0.520, "pinch"),
        (0.540, "magazine_grasp"),
        (seat_hold, "magazine_grasp"),
    )
    if empty:
        if profile_name == "desert_eagle":
            return magazine_exchange + (
                (0.770, "open"),
                (0.855, "rack"),
                (0.906250, "rack"),
                (0.921875, "open"),
                (1.000, "ready"),
            )
        return magazine_exchange + (
            (0.805, "open"),
            (0.900, "rack"),
            (0.9375, "rack"),
            (0.984375, "open"),
            (1.000, "ready"),
        )
    return magazine_exchange + (
        (0.820, "open"),
        (1.000, "ready"),
    )


def interpolated_matrix(left: Matrix, right: Matrix, amount: float) -> Matrix:
    left_location, left_rotation, left_scale = left.decompose()
    right_location, right_rotation, right_scale = right.decompose()
    eased = smooth_step(amount)
    return Matrix.LocRotScale(
        left_location.lerp(right_location, eased),
        left_rotation.slerp(right_rotation, eased),
        left_scale.lerp(right_scale, eased),
    )


def sidearm_hand_relative_pose(
    library: SidearmHandPoseLibrary,
    fraction: float,
    empty: bool,
    reach_end: float = SIDEARM_IK_BLEND_IN_END,
    profile_name: str = "",
) -> dict[str, Matrix]:
    keys = sidearm_hand_pose_keys(empty, reach_end, profile_name)
    left_fraction, left_name = keys[0]
    right_fraction, right_name = keys[-1]
    for left, right in zip(keys, keys[1:]):
        if fraction <= right[0]:
            left_fraction, left_name = left
            right_fraction, right_name = right
            break
    span = max(0.000001, right_fraction - left_fraction)
    amount = max(0.0, min(1.0, (fraction - left_fraction) / span))
    return {
        bone_name: interpolated_matrix(
            library.relative_pose[
                "ready" if bone_name == LEFT_PALM else left_name
            ][bone_name],
            library.relative_pose[
                "ready" if bone_name == LEFT_PALM else right_name
            ][bone_name],
            amount,
        )
        for bone_name in LEFT_HAND_BONES
    }


def apply_sidearm_hand_pose(
    armature: bpy.types.Object,
    relative_pose: dict[str, Matrix],
) -> None:
    # LEFT_HAND_BONES is parent-first, including L_palm before ring and pinky.
    for bone_name in LEFT_HAND_BONES:
        pose_bone = armature.pose.bones[bone_name]
        pose_bone.matrix = pose_bone.parent.matrix @ relative_pose[bone_name]
        bpy.context.view_layer.update()


def pose_matrix(
    location: Vector,
    rotation: Quaternion,
) -> Matrix:
    return Matrix.LocRotScale(location, rotation, Vector((1.0, 1.0, 1.0)))


def swing_pose_matrix(
    source: Matrix,
    source_direction: Vector,
    target_direction: Vector,
    location: Vector,
) -> Matrix:
    swing = source_direction.normalized().rotation_difference(
        target_direction.normalized()
    )
    rotation = swing @ source.to_quaternion()
    rotation.normalize()
    return pose_matrix(location, rotation)


def analytical_sidearm_pose(
    static_pose: SidearmStaticPose,
    target_location: Vector,
    target_rotation: Quaternion,
    pole_location: Vector,
) -> dict[str, Matrix]:
    """Solve a stable shoulder-elbow-wrist pose without an IK branch flip."""
    shoulder_pose = static_pose.global_pose[LEFT_CHAIN[0]]
    elbow_pose = static_pose.global_pose[LEFT_CHAIN[1]]
    wrist_pose = static_pose.global_pose[LEFT_CHAIN[2]]
    shoulder = shoulder_pose.translation
    static_elbow = elbow_pose.translation
    static_wrist = wrist_pose.translation
    upper_direction = static_elbow - shoulder
    forearm_direction = static_wrist - static_elbow
    upper_length = upper_direction.length
    forearm_length = forearm_direction.length
    shoulder_to_target = target_location - shoulder
    requested_distance = shoulder_to_target.length
    if requested_distance <= 0.000001:
        raise RuntimeError("Sidearm analytical target collapsed onto shoulder")
    direction = shoulder_to_target / requested_distance
    minimum_distance = abs(upper_length - forearm_length) + 0.001
    maximum_distance = upper_length + forearm_length - 0.001
    solved_distance = max(
        minimum_distance,
        min(maximum_distance, requested_distance),
    )
    solved_wrist = shoulder + direction * solved_distance
    bend = static_elbow - shoulder
    bend -= direction * bend.dot(direction)
    if bend.length <= 0.0001:
        bend = pole_location - shoulder
        bend -= direction * bend.dot(direction)
    if bend.length <= 0.0001:
        raise RuntimeError("Sidearm analytical bend plane is degenerate")
    bend.normalize()
    along = (
        upper_length * upper_length
        - forearm_length * forearm_length
        + solved_distance * solved_distance
    ) / (2.0 * solved_distance)
    height = math.sqrt(max(0.0, upper_length * upper_length - along * along))
    solved_elbow = shoulder + direction * along + bend * height
    solved_upper_direction = solved_elbow - shoulder
    solved_forearm_direction = solved_wrist - solved_elbow
    return {
        LEFT_CHAIN[0]: swing_pose_matrix(
            shoulder_pose,
            upper_direction,
            solved_upper_direction,
            shoulder,
        ),
        LEFT_CHAIN[1]: swing_pose_matrix(
            elbow_pose,
            forearm_direction,
            solved_forearm_direction,
            solved_elbow,
        ),
        LEFT_CHAIN[2]: pose_matrix(solved_wrist, target_rotation),
    }


def bake_sidearm_clip(
    armature: bpy.types.Object,
    profile: ReloadProfile,
    empty: bool,
    static_pose: SidearmStaticPose,
    hand_pose_library: SidearmHandPoseLibrary,
    magazine_anchor_in_palm: Matrix,
    target: bpy.types.Object,
    end_frame: int,
    clip_name: str,
) -> bpy.types.Action:
    """Bake an articulated pistol reload with palm-driven prop contact."""
    action = bpy.data.actions.new(clip_name)
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    scene = bpy.context.scene
    static_wrist = static_pose.global_pose[LEFT_CHAIN[2]]
    static_wrist_location = static_wrist.translation
    static_wrist_rotation = static_wrist.to_quaternion()
    pole_location = Vector(profile.pole)
    apply_sidearm_static_pose(armature, static_pose)
    apply_sidearm_hand_pose(
        armature,
        hand_pose_library.relative_pose["ready"],
    )
    static_anchor_location = (
        bone_world_matrix(armature, LEFT_PALM)
        @ magazine_anchor_in_palm
    ).translation
    for pose_bone in armature.pose.bones:
        if pose_bone.name in LEFT_ANIMATED_BONES:
            pose_bone.rotation_mode = "QUATERNION"
    for frame in range(0, end_frame + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        target_location = target.location.copy()
        target_rotation = target.rotation_quaternion.copy()
        fraction = frame / end_frame
        reach_end = sidearm_reach_end(profile)
        influence = sidearm_pose_influence(
            fraction,
            empty,
            reach_end,
            profile.name,
        )
        desired_contact = static_anchor_location.lerp(
            target_location,
            influence,
        )
        solved_rotation = static_wrist_rotation.slerp(
            target_rotation,
            influence,
        )
        hand_pose = sidearm_hand_relative_pose(
            hand_pose_library,
            fraction,
            empty,
            reach_end,
            profile.name,
        )
        apply_sidearm_static_pose(armature, static_pose)
        apply_sidearm_hand_pose(armature, hand_pose)
        posed_anchor = (
            bone_world_matrix(armature, LEFT_PALM)
            @ magazine_anchor_in_palm
        ).translation
        static_offset = posed_anchor - static_wrist_location
        rotation_delta = solved_rotation @ static_wrist_rotation.inverted()
        solved_location = desired_contact - rotation_delta @ static_offset
        solved = analytical_sidearm_pose(
            static_pose,
            solved_location,
            solved_rotation,
            pole_location,
        )
        for bone_name in LEFT_CHAIN:
            armature.pose.bones[bone_name].matrix = solved[bone_name]
            bpy.context.view_layer.update()
        apply_sidearm_hand_pose(armature, hand_pose)
        actual_contact = (
            bone_world_matrix(armature, LEFT_PALM)
            @ magazine_anchor_in_palm
        ).translation
        contact_correction = desired_contact - actual_contact
        if contact_correction.length > 0.000001:
            solved = analytical_sidearm_pose(
                static_pose,
                solved_location + contact_correction,
                solved_rotation,
                pole_location,
            )
            for bone_name in LEFT_CHAIN:
                armature.pose.bones[bone_name].matrix = solved[bone_name]
                bpy.context.view_layer.update()
            apply_sidearm_hand_pose(armature, hand_pose)
        for bone_name in LEFT_ANIMATED_BONES:
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.keyframe_insert("location", frame=frame, group=bone_name)
            pose_bone.keyframe_insert(
                "rotation_quaternion",
                frame=frame,
                group=bone_name,
            )
            pose_bone.keyframe_insert("scale", frame=frame, group=bone_name)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "LINEAR"
    make_baked_quaternions_continuous(action)
    armature.animation_data.action = None
    control_action = target.animation_data.action
    target.animation_data_clear()
    bpy.data.actions.remove(control_action)
    bpy.data.objects.remove(target, do_unlink=True)
    return action


def bake_clip(
    armature: bpy.types.Object,
    profile: ReloadProfile,
    empty: bool,
    sidearm_static_pose: SidearmStaticPose | None = None,
    sidearm_hand_poses: SidearmHandPoseLibrary | None = None,
    sidearm_magazine_anchor_in_palm: Matrix | None = None,
) -> bpy.types.Action:
    suffix = "empty" if empty else "tactical"
    clip_name = f"reload_{profile.name}_{suffix}"
    duration = (
        EMPTY_DURATION_SECONDS
        if empty
        else TACTICAL_DURATION_SECONDS
    )
    end_frame = round(duration * FPS)
    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = end_frame
    reset_armature_pose(armature)
    if profile.sidearm:
        if sidearm_static_pose is None:
            raise RuntimeError(
                f"Missing static sidearm endpoint pose for {profile.name}"
            )
        if sidearm_hand_poses is None or sidearm_magazine_anchor_in_palm is None:
            raise RuntimeError(
                f"Missing articulated hand contract for {profile.name}"
            )
        apply_sidearm_static_pose(armature, sidearm_static_pose)
    scene.frame_set(0)
    bpy.context.view_layer.update()
    base_rotation = bone_world_matrix(armature, "L_wrist_03").to_quaternion()
    points = control_points(profile, empty)
    validate_control_point_contract(profile, empty, points)
    target = create_control(
        clip_name,
        points,
        base_rotation,
        end_frame,
        linear=profile.sidearm,
    )
    if profile.sidearm:
        return bake_sidearm_clip(
            armature,
            profile,
            empty,
            sidearm_static_pose,
            sidearm_hand_poses,
            sidearm_magazine_anchor_in_palm,
            target,
            end_frame,
            clip_name,
        )
    pole = bpy.data.objects.new(f"{clip_name}_ElbowPole", None)
    pole.empty_display_type = "CUBE"
    pole.empty_display_size = 2.0
    pole.location = profile.pole
    bpy.context.collection.objects.link(pole)
    action = bpy.data.actions.new(clip_name)
    armature.animation_data_create()
    armature.animation_data.action = action
    ik = armature.pose.bones["L_wrist_03"].constraints.new("IK")
    ik.name = f"{clip_name}_LeftArmIK"
    ik.target = target
    ik.pole_target = pole
    ik.pole_angle = math.radians(90.0)
    ik.chain_count = 3
    ik.iterations = 256
    # Let IK solve position only. Matching the control's rotation through the
    # IK constraint made Blender distribute wrist twist through the complete
    # chain; the sidearm poses then crossed a solver singularity and flipped
    # the shoulder/elbow by roughly 160 degrees in one frame. A separate wrist
    # constraint preserves the authored hand orientation without twisting the
    # upper arm.
    ik.use_rotation = False
    ik.use_stretch = False
    wrist_rotation = armature.pose.bones["L_wrist_03"].constraints.new(
        "COPY_ROTATION"
    )
    wrist_rotation.name = f"{clip_name}_LeftWristRotation"
    wrist_rotation.target = target
    wrist_rotation.target_space = "WORLD"
    wrist_rotation.owner_space = "WORLD"
    wrist_rotation.mix_mode = "REPLACE"
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in armature.pose.bones:
        pose_bone.bone.select = pose_bone.name in LEFT_CHAIN
    bpy.ops.nla.bake(
        frame_start=0,
        frame_end=end_frame,
        step=1,
        only_selected=True,
        visual_keying=True,
        clear_constraints=True,
        clear_parents=False,
        use_current_action=True,
        # Keep the full sampled quaternion sets until their signs are made
        # hemisphere-continuous below. Cleaning component curves independently
        # can remove different frames from W/X/Y/Z and makes safe quaternion
        # normalization impossible.
        clean_curves=False,
        bake_types={"POSE"},
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    action.name = clip_name
    action.use_fake_user = True
    remove_baked_constraint_curves(action)
    make_baked_quaternions_continuous(action)
    armature.animation_data.action = None
    control_action = target.animation_data.action
    target.animation_data_clear()
    bpy.data.actions.remove(control_action)
    bpy.data.objects.remove(target, do_unlink=True)
    bpy.data.objects.remove(pole, do_unlink=True)
    return action


def make_baked_quaternions_continuous(action: bpy.types.Action) -> None:
    """Keep adjacent baked quaternion keys in the same hemisphere.

    Blender matrices treat q and -q as the same orientation, but glTF LINEAR
    interpolation crosses through zero when adjacent component keys use
    opposite signs. Normalizing every left-chain track before export prevents
    a visually identical 30 Hz bake from twitching between its keyframes.
    """
    animated_bones = (
        LEFT_ANIMATED_BONES
        if any(
            curve.data_path
                == f'pose.bones["{LEFT_HAND_BONES[0]}"].rotation_quaternion'
            for curve in action.fcurves
        )
        else LEFT_CHAIN
    )
    for bone_name in animated_bones:
        data_path = f'pose.bones["{bone_name}"].rotation_quaternion'
        curves = [
            next(
                (
                    curve
                    for curve in action.fcurves
                    if curve.data_path == data_path
                    and curve.array_index == component
                ),
                None,
            )
            for component in range(4)
        ]
        if any(curve is None for curve in curves):
            raise RuntimeError(
                f"Baked clip {action.name} is missing {bone_name} quaternion curves"
            )
        typed_curves = [curve for curve in curves if curve is not None]
        key_count = len(typed_curves[0].keyframe_points)
        if any(len(curve.keyframe_points) != key_count for curve in typed_curves):
            raise RuntimeError(
                f"Baked clip {action.name} has mismatched {bone_name} quaternion keys"
            )
        previous = None
        for key_index in range(key_count):
            current = Quaternion(
                tuple(
                    curve.keyframe_points[key_index].co[1]
                    for curve in typed_curves
                )
            )
            current.normalize()
            if previous is not None and previous.dot(current) < 0.0:
                for curve in typed_curves:
                    key = curve.keyframe_points[key_index]
                    key.co[1] = -key.co[1]
                    key.handle_left[1] = -key.handle_left[1]
                    key.handle_right[1] = -key.handle_right[1]
                current.negate()
            previous = current
        for curve in typed_curves:
            curve.update()


def add_static_marker(root: bpy.types.Object, name: str, transform: Matrix) -> None:
    marker = bpy.data.objects.new(name, None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 2.0
    marker.matrix_world = transform
    bpy.context.collection.objects.link(marker)
    marker.parent = root
    marker.matrix_parent_inverse = root.matrix_world.inverted()
    marker.matrix_world = transform


def add_bone_marker(
    armature: bpy.types.Object,
    name: str,
    bone_name: str,
    transform: Matrix | None = None,
) -> None:
    marker = bpy.data.objects.new(name, None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 2.0
    marker_transform = (
        transform if transform is not None else bone_world_matrix(armature, bone_name)
    )
    bpy.context.collection.objects.link(marker)
    marker.parent = armature
    marker.parent_type = "BONE"
    marker.parent_bone = bone_name
    marker.matrix_world = marker_transform


def add_contract_markers(
    root: bpy.types.Object,
    armature: bpy.types.Object,
    weapon_grip: Matrix,
    right_contact: Matrix,
    left_contact: Matrix,
    left_grip_anchor: Matrix,
    left_sidearm_magazine_anchor: Matrix,
) -> None:
    for name, bone_name, transform in (
        ("LeftPalmFrame", LEFT_PALM, left_contact),
        ("LeftGripAnchorFrame", LEFT_PALM, left_grip_anchor),
        (
            "LeftSidearmMagazineAnchorFrame",
            LEFT_PALM,
            left_sidearm_magazine_anchor,
        ),
        ("RightPalmFrame", RIGHT_PALM, right_contact),
        ("LeftWristFrame", "L_wrist_03", None),
        ("RightWristFrame", "R_wrist_026", None),
        ("LeftShoulderFrame", LEFT_SHOULDER, None),
        ("RightShoulderFrame", RIGHT_SHOULDER, None),
    ):
        add_bone_marker(armature, name, bone_name, transform)
    add_static_marker(root, "RightGripFrame", weapon_grip)
    add_static_marker(root, "SupportGripFrame", left_grip_anchor)
    for profile in PROFILES:
        add_static_marker(
            root,
            f"{profile.name}_ElbowPoleFrame",
            Matrix.Translation(Vector(profile.pole)),
        )


def rotation_error(left: Matrix, right: Matrix) -> float:
    return left.to_quaternion().rotation_difference(right.to_quaternion()).angle


def shortest_rotation_error(left: Matrix, right: Matrix) -> float:
    raw = rotation_error(left, right)
    return min(raw, abs(math.tau - raw))


def validate_contact_contract(
    armature: bpy.types.Object,
    weapon_grip: Matrix,
) -> None:
    mesh = bpy.data.objects["FullReloadArmsAuditMesh"]
    components = evaluated_component_centers(mesh)
    right_bone = bone_world_matrix(armature, RIGHT_PALM)
    left_bone = bone_world_matrix(armature, LEFT_PALM)
    right_center = hand_contact_center(components, right_bone.translation)
    left_center = hand_contact_center(components, left_bone.translation)
    right_marker = bpy.data.objects["RightPalmFrame"].matrix_world
    left_marker = bpy.data.objects["LeftPalmFrame"].matrix_world
    left_grip_anchor = bpy.data.objects["LeftGripAnchorFrame"].matrix_world
    left_sidearm_anchor = bpy.data.objects[
        "LeftSidearmMagazineAnchorFrame"
    ].matrix_world
    grip_marker = bpy.data.objects["RightGripFrame"].matrix_world
    support_marker = bpy.data.objects["SupportGripFrame"].matrix_world
    right_center_error = (right_marker.translation - right_center).length * SOURCE_TO_METERS
    left_center_error = (left_marker.translation - left_center).length * SOURCE_TO_METERS
    right_offset = (right_marker.translation - right_bone.translation).length * SOURCE_TO_METERS
    left_offset = (left_marker.translation - left_bone.translation).length * SOURCE_TO_METERS
    grip_error = (grip_marker.translation - weapon_grip.translation).length * SOURCE_TO_METERS
    grip_angle_error = rotation_error(grip_marker, weapon_grip)
    grip_local = grip_marker.inverted() @ right_marker.translation
    grip_contact = grip_local.length * SOURCE_TO_METERS
    grip_lateral = math.hypot(grip_local.x, grip_local.y) * SOURCE_TO_METERS
    grip_longitudinal = grip_local.z * SOURCE_TO_METERS
    right_basis_error = rotation_error(right_marker, right_bone)
    left_basis_error = rotation_error(left_marker, left_bone)
    support_error = (
        support_marker.translation - left_grip_anchor.translation
    ).length * SOURCE_TO_METERS
    support_anchor_offset = (
        left_grip_anchor.translation - left_marker.translation
    ).length * SOURCE_TO_METERS
    sidearm_surface, _, _ = evaluated_hand_surface(
        mesh,
        left_bone.translation,
    )
    _, _, _, sidearm_surface_distance = sidearm_surface.find_nearest(
        left_sidearm_anchor.translation,
    )
    sidearm_surface_error = sidearm_surface_distance * SOURCE_TO_METERS
    sidearm_palm_delta = (
        left_marker.translation - left_sidearm_anchor.translation
    )
    sidearm_anchor_below_palm = sidearm_palm_delta.z * SOURCE_TO_METERS
    sidearm_anchor_lateral = -sidearm_palm_delta.x * SOURCE_TO_METERS
    sidearm_anchor_forward = sidearm_palm_delta.y * SOURCE_TO_METERS
    sidearm_anchor_offset = sidearm_palm_delta.length * SOURCE_TO_METERS
    sidearm_basis_error = rotation_error(left_sidearm_anchor, left_bone)
    valid = (
        right_center_error <= MAX_FIXED_DRIFT_METERS
        and left_center_error <= MAX_FIXED_DRIFT_METERS
        and MIN_PALM_OFFSET_METERS <= right_offset <= MAX_PALM_OFFSET_METERS
        and MIN_PALM_OFFSET_METERS <= left_offset <= MAX_PALM_OFFSET_METERS
        and grip_error <= MAX_FIXED_DRIFT_METERS
        and grip_angle_error <= MAX_FIXED_ROTATION_RADIANS
        and MIN_GRIP_CONTACT_METERS <= grip_contact <= MAX_GRIP_CONTACT_METERS
        and grip_lateral <= 0.012
        and -MAX_GRIP_CONTACT_METERS <= grip_longitudinal <= -MIN_GRIP_CONTACT_METERS
        and right_basis_error <= MAX_FIXED_ROTATION_RADIANS
        and left_basis_error <= MAX_FIXED_ROTATION_RADIANS
        and support_error <= MAX_FIXED_DRIFT_METERS
        and MIN_SUPPORT_ANCHOR_OFFSET_METERS
            <= support_anchor_offset
            <= MAX_SUPPORT_ANCHOR_OFFSET_METERS
        and sidearm_surface_error <= MAX_SIDEARM_ANCHOR_SURFACE_ERROR_METERS
        and MIN_SIDEARM_ANCHOR_BELOW_PALM_METERS
            <= sidearm_anchor_below_palm
            <= MAX_SIDEARM_ANCHOR_BELOW_PALM_METERS
        and MIN_SIDEARM_ANCHOR_LATERAL_METERS
            <= sidearm_anchor_lateral
            <= MAX_SIDEARM_ANCHOR_LATERAL_METERS
        and MIN_SIDEARM_ANCHOR_FORWARD_METERS
            <= sidearm_anchor_forward
            <= MAX_SIDEARM_ANCHOR_FORWARD_METERS
        and MIN_SIDEARM_MAGAZINE_ANCHOR_OFFSET_METERS
            <= sidearm_anchor_offset
            <= MAX_SIDEARM_MAGAZINE_ANCHOR_OFFSET_METERS
        and sidearm_basis_error <= MAX_FIXED_ROTATION_RADIANS
    )
    print(
        "RELOAD_CONTACT_CHECK"
        f" right_center_error={right_center_error:.6f}"
        f" left_center_error={left_center_error:.6f}"
        f" right_palm_offset={right_offset:.6f}"
        f" left_palm_offset={left_offset:.6f}"
        f" grip_contact={grip_contact:.6f}"
        f" grip_lateral={grip_lateral:.6f}"
        f" grip_longitudinal={grip_longitudinal:.6f}"
        f" grip_angle_error={grip_angle_error:.6f}"
        f" palm_basis_error={max(right_basis_error, left_basis_error):.6f}"
        f" support_error={support_error:.6f}"
        f" support_anchor_offset={support_anchor_offset:.6f}"
        f" sidearm_surface_error={sidearm_surface_error:.9f}"
        f" sidearm_below_palm={sidearm_anchor_below_palm:.6f}"
        f" sidearm_lateral={sidearm_anchor_lateral:.6f}"
        f" sidearm_forward={sidearm_anchor_forward:.6f}"
        f" sidearm_anchor_offset={sidearm_anchor_offset:.6f}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError("Animated reload-arm contact contract is invalid")


def validate_sidearm_magazine_anchor_keyframes(
    armature: bpy.types.Object,
    action: bpy.types.Action,
) -> None:
    profile = next(
        (
            profile
            for profile in PROFILES
            if profile.sidearm
            and action.name.startswith(f"reload_{profile.name}_")
        ),
        None,
    )
    if profile is None:
        return
    _, end = (round(value) for value in action.frame_range)
    empty = action.name.endswith("_empty")
    points_by_beat = {
        point.beat: point
        for point in sidearm_control_points(profile, empty)
        if point.beat
    }
    fractions = (
        points_by_beat["old_mag_out"].fraction,
        points_by_beat["new_mag_seat"].fraction,
    )
    mesh = bpy.data.objects["FullReloadArmsAuditMesh"]
    digit_chains = (
        ("L_thumb1_04", "L_thumb2_05", "L_thumb3_00"),
        ("L_point1_07", "L_point2_08", "L_point3_09"),
        ("L_middle1_011", "L_middle2_012", "L_middle3_013"),
        ("L_ring1_016", "L_ring2_017", "L_ring3_018"),
        ("L_pink1_020", "L_pink2_021", "L_pink3_022"),
    )
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    ready_relative = {
        bone_name: (
            armature.pose.bones[bone_name].parent.matrix.inverted()
            @ armature.pose.bones[bone_name].matrix
        )
        for chain in digit_chains
        for bone_name in chain
    }
    for stage, fraction in zip(("extract", "insert"), fractions):
        frame = round(fraction * end)
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        marker = bpy.data.objects[
            "LeftSidearmMagazineAnchorFrame"
        ].matrix_world.translation
        surface, _, _ = evaluated_hand_surface(
            mesh,
            bone_world_matrix(armature, LEFT_PALM).translation,
        )
        location, _, face_index, distance = surface.find_nearest(marker)
        if location is None or face_index is None or distance is None:
            raise RuntimeError(
                f"Could not resolve animated glove surface for {action.name}"
            )
        grasp_clearance = distance * SOURCE_TO_METERS
        digit_curls = tuple(
            sum(
                shortest_rotation_error(
                    ready_relative[bone_name],
                    armature.pose.bones[bone_name].parent.matrix.inverted()
                    @ armature.pose.bones[bone_name].matrix,
                )
                for bone_name in chain
            )
            for chain in digit_chains
        )
        stage_valid = (
            MIN_SIDEARM_GRASP_CLEARANCE_METERS
            <= grasp_clearance
            <= MAX_SIDEARM_GRASP_CLEARANCE_METERS
            and min(digit_curls) >= MIN_SIDEARM_DIGIT_CURL_RADIANS
        )
        print(
            "SIDEARM_MAGAZINE_ANCHOR_KEY"
            f" clip={action.name}"
            f" platform={profile.name}"
            f" stage={stage}"
            f" frame={frame}"
            f" face={face_index}"
            f" grasp_clearance={grasp_clearance:.9f}"
            f" digit_curls={'/'.join(f'{value:.6f}' for value in digit_curls)}"
            f" valid={stage_valid}"
        )
        if not stage_valid:
            raise RuntimeError(
                "Sidearm magazine grasp lost enclosure or finger curl: "
                f"{action.name}"
            )


def reload_profile_for_action(
    action: bpy.types.Action,
) -> tuple[ReloadProfile, bool]:
    for profile in PROFILES:
        for empty in (False, True):
            suffix = "empty" if empty else "tactical"
            if action.name == f"reload_{profile.name}_{suffix}":
                return profile, empty
    raise RuntimeError(f"No reload profile owns clip {action.name}")


def validate_pose_beats(
    action: bpy.types.Action,
    left_positions: list[Vector],
) -> tuple[bool, float, float, float, float, str]:
    """Measure authored holds, mechanical travel, and camera-safe excursion."""
    profile, empty = reload_profile_for_action(action)
    points = control_points(profile, empty)
    by_beat = {
        point.beat: point
        for point in points
        if point.beat
    }
    _, end = (round(value) for value in action.frame_range)

    def position_at(beat: str) -> Vector:
        frame = round(by_beat[beat].fraction * end)
        return left_positions[frame]

    if profile.family == "internal_precision":
        exchange_hold = (
            position_at("ammunition_acquired_hold")
            - position_at("ammunition_acquired")
        ).length * SOURCE_TO_METERS
        seat_hold = (
            position_at("new_round_seat_hold")
            - position_at("new_round_seat")
        ).length * SOURCE_TO_METERS
    else:
        exchange_hold = (
            position_at("old_mag_out_hold")
            - position_at("old_mag_out")
        ).length * SOURCE_TO_METERS
        seat_hold = (
            position_at("new_mag_seat_hold")
            - position_at("new_mag_seat")
        ).length * SOURCE_TO_METERS
    uses_mechanical_beat = empty or profile.tactical_action
    mechanical_travel = (
        (
            position_at("action_peak")
            - position_at("action_contact")
        ).length * SOURCE_TO_METERS
        if uses_mechanical_beat
        else 0.0
    )
    maximum_excursion = max(
        (position - left_positions[0]).length
        for position in left_positions
    ) * SOURCE_TO_METERS
    excursion_limit = (
        SIDEARM_MAX_CAMERA_SAFE_EXCURSION_METERS
        if profile.sidearm
        else MAX_CAMERA_SAFE_EXCURSION_METERS
    )
    valid = (
        exchange_hold <= MAX_POSE_HOLD_DRIFT_METERS
        and seat_hold <= MAX_POSE_HOLD_DRIFT_METERS
        and (
            not uses_mechanical_beat
            or mechanical_travel >= MIN_MECHANICAL_BEAT_METERS
        )
        and maximum_excursion <= excursion_limit
    )
    print(
        "RELOAD_BEAT_CHECK"
        f" clip={action.name}"
        f" family={profile.family}"
        f" exchange_hold={exchange_hold:.6f}"
        f" seat_hold={seat_hold:.6f}"
        f" mechanical_travel={mechanical_travel:.6f}"
        f" max_excursion={maximum_excursion:.6f}"
        f" valid={valid}"
    )
    return (
        valid,
        exchange_hold,
        seat_hold,
        mechanical_travel,
        maximum_excursion,
        profile.family,
    )


def validate_sidearm_endpoint_pose(
    armature: bpy.types.Object,
    action: bpy.types.Action,
    static_pose: SidearmStaticPose | None,
) -> None:
    if static_pose is None:
        return
    armature.animation_data.action = action
    start, end = (round(value) for value in action.frame_range)
    maximum_basis_error = 0.0
    maximum_position_error = 0.0
    for endpoint, frame in (("start", start), ("end", end)):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        basis_error = max(
            shortest_rotation_error(
                static_pose.global_pose[bone_name],
                armature.pose.bones[bone_name].matrix,
            )
            for bone_name in LEFT_CHAIN
        )
        position_error = max(
            (
                static_pose.global_pose[bone_name].translation
                - armature.pose.bones[bone_name].matrix.translation
            ).length
            for bone_name in LEFT_CHAIN
        ) * SOURCE_TO_METERS
        maximum_basis_error = max(maximum_basis_error, basis_error)
        maximum_position_error = max(maximum_position_error, position_error)
        endpoint_valid = (
            basis_error <= SIDEARM_ENDPOINT_MAX_BASIS_ERROR_RADIANS
            and position_error <= SIDEARM_ENDPOINT_MAX_POSITION_ERROR_METERS
        )
        print(
            "SIDEARM_ENDPOINT_POSE"
            f" clip={action.name}"
            f" endpoint={endpoint}"
            f" frame={frame}"
            f" basis_error={basis_error:.9f}"
            f" position_error={position_error:.9f}"
            f" valid={endpoint_valid}"
        )
    if (
        maximum_basis_error > SIDEARM_ENDPOINT_MAX_BASIS_ERROR_RADIANS
        or maximum_position_error > SIDEARM_ENDPOINT_MAX_POSITION_ERROR_METERS
    ):
        raise RuntimeError(
            f"Sidearm reload endpoints do not match the static pose: {action.name}"
        )


def validate_clip(
    armature: bpy.types.Object,
    action: bpy.types.Action,
    sidearm_static_pose: SidearmStaticPose | None = None,
) -> None:
    armature.animation_data.action = action
    validate_sidearm_endpoint_pose(armature, action, sidearm_static_pose)
    start, end = (round(value) for value in action.frame_range)
    scene = bpy.context.scene
    left_positions = []
    left_chain_frames = []
    left_shoulders = []
    right_shoulders = []
    right_palms = []
    right_grip_relatives = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        left_positions.append(bpy.data.objects["LeftPalmFrame"].matrix_world.translation.copy())
        left_chain_frames.append(
            [bone_world_matrix(armature, bone_name) for bone_name in LEFT_CHAIN]
        )
        left_shoulders.append(bone_world_matrix(armature, LEFT_SHOULDER).translation)
        right_shoulders.append(bone_world_matrix(armature, RIGHT_SHOULDER).translation)
        right_palms.append(bpy.data.objects["RightPalmFrame"].matrix_world.copy())
        grip = bpy.data.objects["RightGripFrame"].matrix_world
        right_grip_relatives.append(grip.inverted() @ right_palms[-1])
    shoulder_drift = max(
        max((value - left_shoulders[0]).length for value in left_shoulders),
        max((value - right_shoulders[0]).length for value in right_shoulders),
    ) * SOURCE_TO_METERS
    right_palm_drift = max(
        (value.translation - right_palms[0].translation).length for value in right_palms
    ) * SOURCE_TO_METERS
    right_palm_rotation = max(
        rotation_error(value, right_palms[0]) for value in right_palms
    )
    right_grip_relation_drift = max(
        (value.translation - right_grip_relatives[0].translation).length
        for value in right_grip_relatives
    ) * SOURCE_TO_METERS
    right_grip_relation_angle = max(
        rotation_error(value, right_grip_relatives[0])
        for value in right_grip_relatives
    )
    left_travel = sum(
        (right - left).length for left, right in zip(left_positions, left_positions[1:])
    ) * SOURCE_TO_METERS
    return_error = (left_positions[-1] - left_positions[0]).length * SOURCE_TO_METERS
    left_palm_steps = [
        (right - left).length
        for left, right in zip(left_positions, left_positions[1:])
    ]
    maximum_left_palm_step = max(left_palm_steps) * SOURCE_TO_METERS
    maximum_left_palm_step_frame = (
        left_palm_steps.index(max(left_palm_steps)) + start + 1
    )
    left_bone_step_samples = [
        [
            shortest_rotation_error(left[index], right[index])
            for left, right in zip(left_chain_frames, left_chain_frames[1:])
        ]
        for index in range(len(LEFT_CHAIN))
    ]
    maximum_left_bone_steps = [max(samples) for samples in left_bone_step_samples]
    maximum_left_bone_step_frames = [
        samples.index(max(samples)) + start + 1
        for samples in left_bone_step_samples
    ]
    left_joint_step_samples = [
        [
            (
                right[index].translation
                - left[index].translation
            ).length
            for left, right in zip(left_chain_frames, left_chain_frames[1:])
        ]
        for index in (1, 2)
    ]
    maximum_left_joint_position_steps = [
        max(samples) * SOURCE_TO_METERS
        for samples in left_joint_step_samples
    ]
    maximum_left_joint_step_frames = [
        samples.index(max(samples)) + start + 1
        for samples in left_joint_step_samples
    ]
    profile, empty = reload_profile_for_action(action)
    sidearm = profile.sidearm
    (
        pose_beats_valid,
        exchange_hold,
        seat_hold,
        mechanical_travel,
        maximum_excursion,
        family,
    ) = validate_pose_beats(action, left_positions)
    minimum_hand_travel = (
        SIDEARM_MIN_HAND_TRAVEL_METERS
        if sidearm
        else MIN_HAND_TRAVEL_METERS
    )
    maximum_hand_travel = (
        SIDEARM_MAX_HAND_TRAVEL_METERS
        if sidearm
        else MAX_HAND_TRAVEL_METERS
    )
    maximum_palm_step = (
        SIDEARM_MAX_LEFT_PALM_STEP_METERS
        if sidearm
        else MAX_LEFT_PALM_STEP_METERS
    )
    maximum_bone_step = (
        SIDEARM_MAX_LEFT_BONE_STEP_RADIANS
        if sidearm
        else MAX_LEFT_BONE_STEP_RADIANS
    )
    duration = (end - start) / FPS
    expected_duration = round(
        (
            EMPTY_DURATION_SECONDS
            if empty
            else TACTICAL_DURATION_SECONDS
        ) * FPS
    ) / FPS
    valid = (
        shoulder_drift <= MAX_FIXED_DRIFT_METERS
        and right_palm_drift <= MAX_FIXED_DRIFT_METERS
        and right_palm_rotation <= MAX_FIXED_ROTATION_RADIANS
        and right_grip_relation_drift <= MAX_FIXED_DRIFT_METERS
        and right_grip_relation_angle <= MAX_FIXED_ROTATION_RADIANS
        and minimum_hand_travel <= left_travel <= maximum_hand_travel
        and return_error <= MAX_RETURN_ERROR_METERS
        and maximum_left_palm_step <= maximum_palm_step
        and max(maximum_left_bone_steps) <= maximum_bone_step
        and (
            not sidearm
            or max(maximum_left_joint_position_steps)
                <= SIDEARM_MAX_LEFT_JOINT_STEP_METERS
        )
        and abs(duration - expected_duration) <= 0.000001
        and pose_beats_valid
    )
    print(
        "RELOAD_CLIP"
        f" clip={action.name}"
        f" family={family}"
        f" duration={duration:.3f}"
        f" shoulder_root_max={shoulder_drift:.6f}"
        f" right_palm_max={right_palm_drift:.6f}"
        f" right_palm_angle_max={right_palm_rotation:.6f}"
        f" grip_relation_max={right_grip_relation_drift:.6f}"
        f" grip_relation_angle_max={right_grip_relation_angle:.6f}"
        f" left_palm_travel={left_travel:.4f}"
        f" return_error={return_error:.6f}"
        f" left_palm_step={maximum_left_palm_step:.6f}"
        f" left_palm_step_frame={maximum_left_palm_step_frame}"
        f" left_bone_steps={'/'.join(f'{value:.6f}' for value in maximum_left_bone_steps)}"
        f" left_bone_step_frames={'/'.join(str(value) for value in maximum_left_bone_step_frames)}"
        f" left_joint_steps={'/'.join(f'{value:.6f}' for value in maximum_left_joint_position_steps)}"
        f" left_joint_step_frames={'/'.join(str(value) for value in maximum_left_joint_step_frames)}"
        f" exchange_hold={exchange_hold:.6f}"
        f" seat_hold={seat_hold:.6f}"
        f" mechanical_travel={mechanical_travel:.6f}"
        f" max_excursion={maximum_excursion:.6f}"
        f" sidearm={sidearm}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError(f"Reload clip validation failed: {action.name}")
    validate_sidearm_magazine_anchor_keyframes(armature, action)


def evaluated_grip_space_bounds(
    mesh_object: bpy.types.Object,
    grip_inverse: Matrix,
) -> tuple[Vector, Vector]:
    """Return evaluated mesh bounds in the right-grip camera proxy frame."""
    evaluated = mesh_object.evaluated_get(
        bpy.context.evaluated_depsgraph_get()
    )
    mesh = evaluated.to_mesh()
    try:
        points = [
            grip_inverse @ (evaluated.matrix_world @ vertex.co)
            for vertex in mesh.vertices
        ]
    finally:
        evaluated.to_mesh_clear()
    if not points:
        raise RuntimeError(f"Camera envelope mesh is empty: {mesh_object.name}")
    minimum = Vector(
        tuple(min(point[axis] for point in points) for axis in range(3))
    ) * SOURCE_TO_METERS
    maximum = Vector(
        tuple(max(point[axis] for point in points) for axis in range(3))
    ) * SOURCE_TO_METERS
    return minimum, maximum


def camera_envelope_valid(minimum: Vector, maximum: Vector) -> bool:
    span = maximum - minimum
    return (
        span.x <= MAX_LONG_GUN_CAMERA_HORIZONTAL_SPAN_METERS
        and span.y <= MAX_LONG_GUN_CAMERA_DEPTH_SPAN_METERS
        and span.z <= MAX_LONG_GUN_CAMERA_VERTICAL_SPAN_METERS
        and maximum.y <= MAX_LONG_GUN_CAMERA_REAR_EXTENT_METERS
    )


def validate_long_gun_camera_envelope(
    armature: bpy.types.Object,
    actions: list[bpy.types.Action],
    weapon_grip: Matrix,
) -> None:
    """Reject the full-sleeve near-plane failure seen in Godot captures.

    The source weapon grip is the stable camera proxy used by the runtime
    mount. Every evaluated long-gun frame must keep the cropped silhouette in
    a bounded grip-space volume. The retained full-arm audit layer must fail
    the same envelope, proving this gate distinguishes the former giant-sleeve
    presentation instead of merely restating the clip/bone checks.
    """
    long_gun_mesh = bpy.data.objects["LongGunReloadForearmsMesh"]
    full_audit_mesh = bpy.data.objects["FullReloadArmsAuditMesh"]
    grip_inverse = weapon_grip.inverted()
    long_actions = [
        action
        for action in actions
        if not reload_profile_for_action(action)[0].sidearm
    ]
    all_valid = True
    for action in long_actions:
        armature.animation_data.action = action
        start, end = (round(value) for value in action.frame_range)
        frames = list(range(start, end + 1, CAMERA_ENVELOPE_FRAME_STEP))
        if frames[-1] != end:
            frames.append(end)
        worst_span = Vector((0.0, 0.0, 0.0))
        maximum_rear = -math.inf
        action_valid = True
        for frame in frames:
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            minimum, maximum = evaluated_grip_space_bounds(
                long_gun_mesh,
                grip_inverse,
            )
            span = maximum - minimum
            worst_span.x = max(worst_span.x, span.x)
            worst_span.y = max(worst_span.y, span.y)
            worst_span.z = max(worst_span.z, span.z)
            maximum_rear = max(maximum_rear, maximum.y)
            action_valid = action_valid and camera_envelope_valid(
                minimum,
                maximum,
            )
        all_valid = all_valid and action_valid
        print(
            "LONG_GUN_CAMERA_ENVELOPE"
            f" clip={action.name}"
            f" samples={len(frames)}"
            f" horizontal_span={worst_span.x:.6f}"
            f" depth_span={worst_span.y:.6f}"
            f" vertical_span={worst_span.z:.6f}"
            f" rear_extent={maximum_rear:.6f}"
            f" valid={action_valid}"
        )

    reference_action = next(
        action for action in long_actions if action.name == "reload_m4a1_empty"
    )
    armature.animation_data.action = reference_action
    bpy.context.scene.frame_set(round(reference_action.frame_range[0]))
    bpy.context.view_layer.update()
    audit_minimum, audit_maximum = evaluated_grip_space_bounds(
        full_audit_mesh,
        grip_inverse,
    )
    audit_span = audit_maximum - audit_minimum
    full_rejected = not camera_envelope_valid(audit_minimum, audit_maximum)
    print(
        "FULL_ARM_CAMERA_REJECTION"
        f" clip={reference_action.name}"
        f" horizontal_span={audit_span.x:.6f}"
        f" depth_span={audit_span.y:.6f}"
        f" vertical_span={audit_span.z:.6f}"
        f" rear_extent={audit_maximum.y:.6f}"
        f" rejected={full_rejected}"
    )
    armature.animation_data.action = None
    reset_armature_pose(armature)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    if not all_valid or not full_rejected:
        raise RuntimeError("Long-gun camera silhouette envelope is invalid")


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_force_sampling=False,
        export_frame_range=False,
        export_cameras=False,
        export_lights=False,
        export_apply=False,
        export_extras=True,
        export_image_format="AUTO",
        export_yup=True,
    )


def validate_export(
    actions: list[bpy.types.Action],
    expected_full_triangles: int,
    expected_long_gun_triangles: int,
    expected_sidearm_triangles: int,
) -> None:
    with OUTPUT_GLB.open("rb") as stream:
        magic, version, _ = struct.unpack("<4sII", stream.read(12))
        chunk_length, chunk_type = struct.unpack("<II", stream.read(8))
        document = json.loads(stream.read(chunk_length).decode("utf-8"))
        binary_length, binary_type = struct.unpack("<II", stream.read(8))
        binary = stream.read(binary_length)
    images = document.get("images", [])
    embedded_images = len(images) == 3 and all("bufferView" in image and "uri" not in image and image.get("mimeType") == "image/png" for image in images)
    for index, image in enumerate(images):
        view = document["bufferViews"][image["bufferView"]]
        offset = view.get("byteOffset", 0)
        sidecar = OUTPUT_GLB.with_name(f"{OUTPUT_GLB.stem}_{image.get('name', f'Image_{index}')}.png")
        sidecar.write_bytes(binary[offset:offset + view["byteLength"]])
    expected_clips = {action.name for action in actions}
    actual_clips = {item["name"] for item in document.get("animations", [])}
    expected_durations = {
        action.name: (
            round(action.frame_range[1]) - round(action.frame_range[0])
        ) / FPS
        for action in actions
    }
    duration_contract = actual_clips == expected_clips
    for animation in sorted(
        document.get("animations", []),
        key=lambda item: item["name"],
    ):
        ranges = [
            document["accessors"][sampler["input"]]
            for sampler in animation.get("samplers", [])
        ]
        duration = (
            max(accessor["max"][0] for accessor in ranges)
            - min(accessor["min"][0] for accessor in ranges)
            if ranges
            else -1.0
        )
        expected_duration = expected_durations.get(animation["name"], -2.0)
        clip_duration_valid = abs(duration - expected_duration) <= 0.0001
        duration_contract = duration_contract and clip_duration_valid
        print(
            "RELOAD_GLB_CLIP"
            f" clip={animation['name']}"
            f" duration={duration:.3f}"
            f" expected={expected_duration:.3f}"
            f" valid={clip_duration_valid}"
        )

    def mesh_triangles(mesh_index: int) -> int:
        return sum(
            document["accessors"][primitive["indices"]]["count"] // 3
            for primitive in document["meshes"][mesh_index].get("primitives", [])
            if primitive.get("mode", 4) == 4 and "indices" in primitive
        )

    nodes_by_name = {
        node.get("name", ""): node for node in document.get("nodes", [])
    }
    compatibility_node = nodes_by_name.get("ReloadArmsMesh", {})
    full_node = nodes_by_name.get("FullReloadArmsAuditMesh", {})
    long_gun_node = nodes_by_name.get("LongGunReloadForearmsMesh", {})
    sidearm_node = nodes_by_name.get("SidearmReloadForearmsMesh", {})
    exported_full_triangles = (
        mesh_triangles(full_node["mesh"]) if "mesh" in full_node else -1
    )
    exported_sidearm_triangles = (
        mesh_triangles(sidearm_node["mesh"]) if "mesh" in sidearm_node else -1
    )
    exported_long_gun_triangles = (
        mesh_triangles(long_gun_node["mesh"])
        if "mesh" in long_gun_node
        else -1
    )
    skinned_mesh_contract = (
        "skin" in full_node
        and "skin" in long_gun_node
        and "skin" in sidearm_node
        and full_node["skin"] == long_gun_node["skin"]
        and full_node["skin"] == sidearm_node["skin"]
        and all(
            {
                "POSITION",
                "NORMAL",
                "TEXCOORD_0",
                "JOINTS_0",
                "WEIGHTS_0",
            }
            <= set(primitive.get("attributes", {}))
            and "material" in primitive
            for node in (full_node, long_gun_node, sidearm_node)
            if "mesh" in node
            for primitive in document["meshes"][node["mesh"]].get("primitives", [])
        )
    )
    presentation_roles = {
        compatibility_node.get("extras", {}).get("presentation_role"),
        full_node.get("extras", {}).get("presentation_role"),
        long_gun_node.get("extras", {}).get("presentation_role"),
        sidearm_node.get("extras", {}).get("presentation_role"),
    }
    presentation_role_contract = presentation_roles == {
        "long_gun_forearms_compatibility_layer",
        "full_arms_non_runtime_audit",
        "long_gun_forearms_runtime",
        "sidearm_forearms_runtime",
    }
    node_names = {node.get("name", "") for node in document.get("nodes", [])}
    expected_nodes = {
        "WeaponRoot", "ReloadArmsSkeleton", "ReloadArmsMesh",
        "FullReloadArmsAuditMesh", "LongGunReloadForearmsMesh",
        "SidearmReloadForearmsMesh",
        "LeftPalmFrame", "LeftGripAnchorFrame",
        "LeftSidearmMagazineAnchorFrame", "RightPalmFrame",
        "LeftWristFrame", "RightWristFrame",
        "LeftShoulderFrame", "RightShoulderFrame", "RightGripFrame", "SupportGripFrame",
        *(f"{profile.name}_ElbowPoleFrame" for profile in PROFILES),
    }
    valid = (
        magic == b"glTF" and version == 2 and chunk_type == 0x4E4F534A
        and binary_type == 0x004E4942 and embedded_images
        and len(document.get("meshes", [])) == 3
        and len(document.get("skins", [])) == 1
        and exported_full_triangles == expected_full_triangles
        and exported_long_gun_triangles == expected_long_gun_triangles
        and exported_sidearm_triangles == expected_sidearm_triangles
        and skinned_mesh_contract
        and presentation_role_contract
        and "mesh" not in compatibility_node
        and actual_clips == expected_clips
        and duration_contract
        and expected_nodes <= node_names
    )
    print(
        "RELOAD_GLB_CHECK"
        f" meshes={len(document.get('meshes', []))}"
        f" skins={len(document.get('skins', []))}"
        f" full_triangles={exported_full_triangles}"
        f" long_gun_triangles={exported_long_gun_triangles}"
        f" sidearm_triangles={exported_sidearm_triangles}"
        f" shared_skin={skinned_mesh_contract}"
        f" presentation_roles={presentation_role_contract}"
        f" clips={len(actual_clips)}"
        f" clip_durations={duration_contract}"
        f" images={len(images)}"
        f" nodes={len(node_names)}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError("Exported reload-arm GLB contract is invalid")


def main() -> None:
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(f"Missing tracked CC BY source: {SOURCE_GLB}")
    bpy.context.preferences.filepaths.save_version = 0
    (
        root,
        armature,
        weapon_grip,
        right_contact,
        left_contact,
        left_grip_anchor,
        left_sidearm_magazine_anchor,
        sidearm_hand_poses,
    ) = import_and_prepare_source()
    arms_mesh = bpy.data.objects["FullReloadArmsAuditMesh"]
    triangle_count = sum(len(polygon.vertices) - 2 for polygon in arms_mesh.data.polygons)
    long_gun_arms_mesh = bpy.data.objects["LongGunReloadForearmsMesh"]
    long_gun_triangle_count = sum(
        len(polygon.vertices) - 2
        for polygon in long_gun_arms_mesh.data.polygons
    )
    sidearm_arms_mesh = bpy.data.objects["SidearmReloadForearmsMesh"]
    sidearm_triangle_count = sum(
        len(polygon.vertices) - 2
        for polygon in sidearm_arms_mesh.data.polygons
    )
    if triangle_count != EXPECTED_TRIANGLES:
        raise RuntimeError(
            f"Authored arm triangle count changed: {triangle_count} != {EXPECTED_TRIANGLES}"
        )
    if long_gun_triangle_count != EXPECTED_LONG_GUN_TRIANGLES:
        raise RuntimeError(
            "Long-gun forearm triangle count changed: "
            f"{long_gun_triangle_count} != {EXPECTED_LONG_GUN_TRIANGLES}"
        )
    if sidearm_triangle_count != EXPECTED_SIDEARM_TRIANGLES:
        raise RuntimeError(
            "Sidearm forearm triangle count changed: "
            f"{sidearm_triangle_count} != {EXPECTED_SIDEARM_TRIANGLES}"
        )
    static_pose_by_kind = {
        kind: capture_sidearm_static_pose(armature, kind)
        for kind in ("pistol_service", "pistol_large")
    }
    sidearm_static_poses = {
        profile.name: static_pose_by_kind[
            "pistol_large"
            if profile.name == "desert_eagle"
            else "pistol_service"
        ]
        for profile in PROFILES
        if profile.sidearm
    }
    sidearm_magazine_anchor_in_palm = (
        bone_world_matrix(armature, LEFT_PALM).inverted()
        @ left_sidearm_magazine_anchor
    )
    actions = [
        bake_clip(
            armature,
            profile,
            empty,
            sidearm_static_poses.get(profile.name),
            sidearm_hand_poses if profile.sidearm else None,
            sidearm_magazine_anchor_in_palm if profile.sidearm else None,
        )
        for profile in PROFILES
        for empty in (False, True)
    ]
    armature.animation_data.action = None
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    add_contract_markers(
        root,
        armature,
        weapon_grip,
        right_contact,
        left_contact,
        left_grip_anchor,
        left_sidearm_magazine_anchor,
    )
    validate_contact_contract(armature, weapon_grip)
    for action in actions:
        sidearm_profile = next(
            (
                profile
                for profile in PROFILES
                if profile.sidearm
                and action.name.startswith(f"reload_{profile.name}_")
            ),
            None,
        )
        validate_clip(
            armature,
            action,
            sidearm_static_poses.get(sidearm_profile.name)
            if sidearm_profile is not None
            else None,
        )
    validate_long_gun_camera_envelope(armature, actions, weapon_grip)
    armature.animation_data.action = None
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    root["reload_contract_version"] = 8
    root["coordinate_space"] = "WeaponRoot root-scale converts source units to metres"
    root["native_clip_platform"] = "m3a1"
    root["reload_clip_count"] = len(actions)
    root["reload_motion_revision"] = (
        "authored_articulated_sidearm_magazine_grasp_2026_09_01"
    )
    root["runtime_long_gun_mesh"] = "LongGunReloadForearmsMesh"
    root["runtime_sidearm_mesh"] = "SidearmReloadForearmsMesh"
    root["non_runtime_audit_mesh"] = "FullReloadArmsAuditMesh"
    root["reload_family_count"] = 6
    root["reload_families"] = ",".join(
        (
            "straight_rifle", "rock_and_lock", "mp5",
            "precision_and_internal", "service_pistol", "desert_eagle",
        )
    )
    root.scale = Vector((SOURCE_TO_METERS,) * 3)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))
    export_asset(root)
    validate_export(
        actions,
        triangle_count,
        long_gun_triangle_count,
        sidearm_triangle_count,
    )
    mesh_count = len([obj for obj in root.children_recursive if obj.type == "MESH"])
    print(
        "RELOAD_ARMS_PASS"
        f" clips={len(actions)}"
        f" meshes={mesh_count}"
        f" full_triangles={triangle_count}"
        f" long_gun_triangles={long_gun_triangle_count}"
        f" sidearm_triangles={sidearm_triangle_count}"
        f" glb={OUTPUT_GLB}"
        f" blend={SOURCE_BLEND}"
    )
    if mesh_count != 3:
        raise RuntimeError("Arms-only export contains unexpected visible meshes")


if __name__ == "__main__":
    main()
