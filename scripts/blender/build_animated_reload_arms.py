"""Build deterministic, arms-only skeletal reload clips from DJMaesen's rig.

The CC BY 4.0 source supplies production glove/sleeve geometry, materials,
finger bones, and skin weights.  This adaptation removes every weapon mesh,
turns the authored firing pose into the rest pose, and bakes platform-specific
left-arm IK into named actions.  The right hand and both shoulder roots remain
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
from mathutils import Euler, Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_djmaesen_smg45 import extend_authored_sleeves, refine_authored_sleeves
from build_first_person_arms import (
    evaluated_component_centers,
    frame_at,
    hand_contact_center,
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
EXPECTED_TRIANGLES = 13_700
LEFT_CHAIN = ("L_arm_01", "L_elbow_02", "L_wrist_03")
RIGHT_PALM = "R_palm_039"
LEFT_PALM = "L_palm_015"
LEFT_SHOULDER = "L_arm_01"
RIGHT_SHOULDER = "R_arm_024"
MAX_FIXED_DRIFT_METERS = 0.00025
MAX_FIXED_ROTATION_RADIANS = 0.001
MAX_BIND_SURFACE_ERROR_METERS = 0.00001
MIN_HAND_TRAVEL_METERS = 0.45
MAX_HAND_TRAVEL_METERS = 3.50
MAX_RETURN_ERROR_METERS = 0.025
MIN_PALM_OFFSET_METERS = 0.075
MAX_PALM_OFFSET_METERS = 0.115
MIN_GRIP_CONTACT_METERS = 0.015
MAX_GRIP_CONTACT_METERS = 0.035
MAX_LEFT_BONE_STEP_RADIANS = 0.55
MAX_LEFT_PALM_STEP_METERS = 0.25


@dataclass(frozen=True)
class ReloadProfile:
    name: str
    support: tuple[float, float, float]
    magazine: tuple[float, float, float]
    pocket: tuple[float, float, float]
    insert: tuple[float, float, float]
    action: tuple[float, float, float]
    pole: tuple[float, float, float]
    sidearm: bool = False


# Coordinates are source-rig units in WeaponRoot space.  The exported root
# applies SOURCE_TO_METERS, matching the existing authored-arm adaptations.
# Each entry is intentionally platform-specific rather than a shared family
# guess so a runtime adapter can select a stable, named clip per weapon.
PROFILES = (
    ReloadProfile("m4a1", (5.4, -31.0, 3.2), (4.0, -10.0, -9.0),
                  (31.0, 3.0, -26.0), (3.0, -11.0, -4.0), (2.0, -4.0, 10.0), (48.0, 1.0, -27.0)),
    ReloadProfile("ak74", (5.4, -31.0, 3.2), (7.0, -13.0, -12.0),
                  (32.0, 2.0, -28.0), (8.0, -13.0, -5.0), (13.0, -10.0, 8.0), (50.0, -1.0, -28.0)),
    ReloadProfile("scarl", (5.4, -31.0, 3.2), (4.0, -11.0, -10.0),
                  (31.0, 2.0, -27.0), (4.0, -12.0, -4.0), (12.0, -14.0, 9.0), (48.0, 0.0, -27.0)),
    ReloadProfile("mp5a5", (5.4, -28.0, 3.5), (3.0, -10.0, -12.0),
                  (30.0, 4.0, -27.0), (3.0, -10.0, -5.0), (12.0, -19.0, 11.0), (47.0, 2.0, -27.0)),
    ReloadProfile("m24", (5.0, -39.0, 3.0), (4.0, -9.0, -10.0),
                  (32.0, 4.0, -28.0), (4.0, -10.0, -4.0), (9.0, -4.0, 10.0), (50.0, 0.0, -29.0)),
    ReloadProfile("axmc", (5.0, -40.0, 3.0), (4.0, -10.0, -11.0),
                  (33.0, 3.0, -29.0), (4.0, -11.0, -4.0), (10.0, -5.0, 11.0), (51.0, 0.0, -29.0)),
    ReloadProfile("awm", (5.0, -42.0, 3.0), (5.0, -10.0, -12.0),
                  (34.0, 3.0, -30.0), (5.0, -11.0, -5.0), (10.0, -5.0, 11.0), (52.0, -1.0, -30.0)),
    ReloadProfile("vss", (5.0, -35.0, 3.0), (5.0, -12.0, -11.0),
                  (31.0, 3.0, -28.0), (5.0, -12.0, -4.0), (12.0, -11.0, 9.0), (49.0, 0.0, -28.0)),
    ReloadProfile("p226", (3.0, 0.0, 5.0), (-2.0, -2.0, -5.0),
                  (29.0, 12.0, -27.0), (-2.0, -2.0, -2.0), (1.0, -8.0, 10.0), (40.0, 18.0, -23.0), True),
    ReloadProfile("m1911", (3.0, 0.0, 5.0), (-3.0, -2.0, -5.0),
                  (29.0, 12.0, -27.0), (-3.0, -2.0, -2.0), (1.0, -9.0, 10.0), (40.0, 18.0, -23.0), True),
    ReloadProfile("gsh18", (3.0, 0.0, 5.0), (-2.0, -3.0, -5.0),
                  (29.0, 12.0, -27.0), (-2.0, -3.0, -2.0), (1.0, -9.0, 10.0), (40.0, 18.0, -23.0), True),
    ReloadProfile("desert_eagle", (-7.0, -10.5, 1.0), (-4.0, -3.0, -6.0),
                  (30.0, 11.0, -29.0), (-4.0, -3.0, -2.0), (0.0, -10.0, 11.0), (41.0, 16.0, -25.0), True),
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


def import_and_prepare_source() -> tuple[
    bpy.types.Object,
    bpy.types.Object,
    Matrix,
    Matrix,
    Matrix,
]:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_set(SOURCE_IDLE_FRAME)
    bpy.context.view_layer.update()
    armature = bpy.data.objects["Object_4"]
    arms_mesh = bpy.data.objects["Object_7"]
    refine_authored_sleeves()
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
    bind_vertices = evaluated_vertices(arms_mesh)

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
    arms_mesh.name = "ReloadArmsMesh"
    armature.parent = root
    armature.matrix_parent_inverse.identity()
    armature.matrix_basis.identity()
    retained = {root, armature, arms_mesh}
    for obj in list(bpy.context.scene.objects):
        if obj not in retained:
            bpy.data.objects.remove(obj, do_unlink=True)
    if arms_mesh.parent is not armature:
        raise RuntimeError("Authored arms mesh lost its armature parent")
    if not arms_mesh.vertex_groups or not arms_mesh.data.materials:
        raise RuntimeError("Authored skin weights or materials were not preserved")
    return root, armature, weapon_grip, right_contact, left_contact


def eased_keyframes(target: bpy.types.Object) -> None:
    action = target.animation_data.action
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"
            key.handle_left_type = "AUTO_CLAMPED"
            key.handle_right_type = "AUTO_CLAMPED"


def control_points(profile: ReloadProfile, empty: bool) -> list[tuple[float, Vector, Vector]]:
    support = Vector(profile.support)
    magazine = Vector(profile.magazine)
    pocket = Vector(profile.pocket)
    insert = Vector(profile.insert)
    action = Vector(profile.action)
    old_lower = magazine.lerp(pocket, 0.48) + Vector((0.0, 0.0, -7.0))
    fresh = pocket + Vector((-2.0, -2.0, 5.0))
    approach = insert + Vector((0.0, 4.0, -8.0))
    if not empty:
        return [
            (0.00, support, Vector()),
            (0.15, magazine, Vector((28.0, 1.0, 8.0))),
            (0.28, old_lower, Vector((34.0, 2.0, 13.0))),
            (0.40, pocket, Vector((38.0, 2.0, 16.0))),
            (0.51, fresh, Vector((36.0, 2.0, 15.0))),
            (0.66, approach, Vector((29.0, 1.0, 10.0))),
            (0.77, insert, Vector((25.0, 1.0, 8.0))),
            (0.84, insert + Vector((0.0, -1.0, 4.0)), Vector((20.0, 0.0, 6.0))),
            (1.00, support, Vector()),
        ]
    action_pull = action + Vector((0.0, 7.0, 0.0))
    return [
        (0.00, support, Vector()),
        (0.13, magazine, Vector((28.0, 1.0, 8.0))),
        (0.25, old_lower, Vector((34.0, 2.0, 13.0))),
        (0.36, pocket, Vector((38.0, 2.0, 16.0))),
        (0.46, fresh, Vector((36.0, 2.0, 15.0))),
        (0.59, approach, Vector((29.0, 1.0, 10.0))),
        (0.68, insert, Vector((25.0, 1.0, 8.0))),
        (0.74, insert + Vector((0.0, -1.0, 4.0)), Vector((20.0, 0.0, 6.0))),
        (0.82, action, Vector((8.0, -10.0, -3.0))),
        (0.89, action_pull, Vector((6.0, -14.0, -5.0))),
        (0.94, action, Vector((8.0, -10.0, -3.0))),
        (1.00, support, Vector()),
    ]


def create_control(
    name: str,
    points: list[tuple[float, Vector, Vector]],
    base_rotation: Quaternion,
    end_frame: int,
) -> bpy.types.Object:
    target = bpy.data.objects.new(f"{name}_IKTarget", None)
    target.empty_display_type = "SPHERE"
    target.empty_display_size = 2.0
    bpy.context.collection.objects.link(target)
    target.rotation_mode = "QUATERNION"
    target.animation_data_create()
    target.animation_data.action = bpy.data.actions.new(f"{name}_controls")
    for fraction, location, rotation_degrees in points:
        frame = round(fraction * end_frame)
        target.location = location
        delta = Euler(tuple(math.radians(value) for value in rotation_degrees), "XYZ")
        target.rotation_quaternion = base_rotation @ delta.to_quaternion()
        target.keyframe_insert("location", frame=frame)
        target.keyframe_insert("rotation_quaternion", frame=frame)
    eased_keyframes(target)
    return target


def bake_clip(
    armature: bpy.types.Object,
    profile: ReloadProfile,
    empty: bool,
) -> bpy.types.Action:
    suffix = "empty" if empty else "tactical"
    clip_name = f"reload_{profile.name}_{suffix}"
    duration = 2.15 if empty else 1.80
    end_frame = round(duration * FPS)
    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = end_frame
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    scene.frame_set(0)
    bpy.context.view_layer.update()
    base_rotation = bone_world_matrix(armature, "L_wrist_03").to_quaternion()
    target = create_control(
        clip_name,
        control_points(profile, empty),
        base_rotation,
        end_frame,
    )
    pole = bpy.data.objects.new(f"{clip_name}_ElbowPole", None)
    pole.empty_display_type = "CUBE"
    pole.empty_display_size = 2.0
    pole.location = profile.pole
    bpy.context.collection.objects.link(pole)
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

    action = bpy.data.actions.new(clip_name)
    armature.animation_data_create()
    armature.animation_data.action = action
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
    for bone_name in LEFT_CHAIN:
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
) -> None:
    for name, bone_name, transform in (
        ("LeftPalmFrame", LEFT_PALM, left_contact),
        ("RightPalmFrame", RIGHT_PALM, right_contact),
        ("LeftWristFrame", "L_wrist_03", None),
        ("RightWristFrame", "R_wrist_026", None),
        ("LeftShoulderFrame", LEFT_SHOULDER, None),
        ("RightShoulderFrame", RIGHT_SHOULDER, None),
    ):
        add_bone_marker(armature, name, bone_name, transform)
    add_static_marker(root, "RightGripFrame", weapon_grip)
    add_static_marker(root, "SupportGripFrame", left_contact)
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
    mesh = bpy.data.objects["ReloadArmsMesh"]
    components = evaluated_component_centers(mesh)
    right_bone = bone_world_matrix(armature, RIGHT_PALM)
    left_bone = bone_world_matrix(armature, LEFT_PALM)
    right_center = hand_contact_center(components, right_bone.translation)
    left_center = hand_contact_center(components, left_bone.translation)
    right_marker = bpy.data.objects["RightPalmFrame"].matrix_world
    left_marker = bpy.data.objects["LeftPalmFrame"].matrix_world
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
    support_error = (support_marker.translation - left_marker.translation).length * SOURCE_TO_METERS
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
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError("Animated reload-arm contact contract is invalid")


def validate_clip(armature: bpy.types.Object, action: bpy.types.Action) -> None:
    armature.animation_data.action = action
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
    maximum_left_palm_step = max(
        (right - left).length
        for left, right in zip(left_positions, left_positions[1:])
    ) * SOURCE_TO_METERS
    maximum_left_bone_steps = [
        max(
            shortest_rotation_error(left[index], right[index])
            for left, right in zip(left_chain_frames, left_chain_frames[1:])
        )
        for index in range(len(LEFT_CHAIN))
    ]
    duration = (end - start) / FPS
    valid = (
        shoulder_drift <= MAX_FIXED_DRIFT_METERS
        and right_palm_drift <= MAX_FIXED_DRIFT_METERS
        and right_palm_rotation <= MAX_FIXED_ROTATION_RADIANS
        and right_grip_relation_drift <= MAX_FIXED_DRIFT_METERS
        and right_grip_relation_angle <= MAX_FIXED_ROTATION_RADIANS
        and MIN_HAND_TRAVEL_METERS <= left_travel <= MAX_HAND_TRAVEL_METERS
        and return_error <= MAX_RETURN_ERROR_METERS
        and maximum_left_palm_step <= MAX_LEFT_PALM_STEP_METERS
        and max(maximum_left_bone_steps) <= MAX_LEFT_BONE_STEP_RADIANS
        and 1.70 <= duration <= 2.20
    )
    print(
        "RELOAD_CLIP"
        f" clip={action.name}"
        f" duration={duration:.3f}"
        f" shoulder_root_max={shoulder_drift:.6f}"
        f" right_palm_max={right_palm_drift:.6f}"
        f" right_palm_angle_max={right_palm_rotation:.6f}"
        f" grip_relation_max={right_grip_relation_drift:.6f}"
        f" grip_relation_angle_max={right_grip_relation_angle:.6f}"
        f" left_palm_travel={left_travel:.4f}"
        f" return_error={return_error:.6f}"
        f" left_palm_step={maximum_left_palm_step:.6f}"
        f" left_bone_steps={'/'.join(f'{value:.6f}' for value in maximum_left_bone_steps)}"
        f" valid={valid}"
    )
    if not valid:
        raise RuntimeError(f"Reload clip validation failed: {action.name}")


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


def validate_export(actions: list[bpy.types.Action], expected_triangles: int) -> None:
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
    exported_triangles = sum(
        document["accessors"][primitive["indices"]]["count"] // 3
        for mesh in document.get("meshes", [])
        for primitive in mesh.get("primitives", [])
        if primitive.get("mode", 4) == 4 and "indices" in primitive
    )
    node_names = {node.get("name", "") for node in document.get("nodes", [])}
    expected_nodes = {
        "WeaponRoot", "ReloadArmsSkeleton", "ReloadArmsMesh",
        "LeftPalmFrame", "RightPalmFrame", "LeftWristFrame", "RightWristFrame",
        "LeftShoulderFrame", "RightShoulderFrame", "RightGripFrame", "SupportGripFrame",
        *(f"{profile.name}_ElbowPoleFrame" for profile in PROFILES),
    }
    valid = (
        magic == b"glTF" and version == 2 and chunk_type == 0x4E4F534A
        and binary_type == 0x004E4942 and embedded_images
        and len(document.get("meshes", [])) == 1
        and len(document.get("skins", [])) == 1
        and exported_triangles == expected_triangles
        and actual_clips == expected_clips
        and expected_nodes <= node_names
    )
    print(f"RELOAD_GLB_CHECK meshes={len(document.get('meshes', []))} skins={len(document.get('skins', []))} triangles={exported_triangles} clips={len(actual_clips)} images={len(images)} nodes={len(node_names)} valid={valid}")
    if not valid:
        raise RuntimeError("Exported reload-arm GLB contract is invalid")


def main() -> None:
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(f"Missing tracked CC BY source: {SOURCE_GLB}")
    bpy.context.preferences.filepaths.save_version = 0
    root, armature, weapon_grip, right_contact, left_contact = (
        import_and_prepare_source()
    )
    arms_mesh = bpy.data.objects["ReloadArmsMesh"]
    triangle_count = sum(len(polygon.vertices) - 2 for polygon in arms_mesh.data.polygons)
    if triangle_count != EXPECTED_TRIANGLES:
        raise RuntimeError(
            f"Authored arm triangle count changed: {triangle_count} != {EXPECTED_TRIANGLES}"
        )
    actions = [
        bake_clip(armature, profile, empty)
        for profile in PROFILES
        for empty in (False, True)
    ]
    armature.animation_data.action = None
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    add_contract_markers(root, armature, weapon_grip, right_contact, left_contact)
    validate_contact_contract(armature, weapon_grip)
    for action in actions:
        validate_clip(armature, action)
    armature.animation_data.action = None
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    root["reload_contract_version"] = 2
    root["coordinate_space"] = "WeaponRoot root-scale converts source units to metres"
    root["native_clip_platform"] = "m3a1"
    root["reload_clip_count"] = len(actions)
    root.scale = Vector((SOURCE_TO_METERS,) * 3)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))
    export_asset(root)
    validate_export(actions, triangle_count)
    mesh_count = len([obj for obj in root.children_recursive if obj.type == "MESH"])
    print(
        "RELOAD_ARMS_PASS"
        f" clips={len(actions)}"
        f" meshes={mesh_count}"
        f" triangles={triangle_count}"
        f" glb={OUTPUT_GLB}"
        f" blend={SOURCE_BLEND}"
    )
    if mesh_count != 1:
        raise RuntimeError("Arms-only export contains unexpected visible meshes")


if __name__ == "__main__":
    main()
