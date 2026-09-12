"""Bake a balanced, upright paper-doll pose into the delivered HY-3D rig.

Run in Blender 4.5 with --input <operator.glb> --output <operator.glb>
--blend <operator.blend>. Existing gameplay actions, skin, and rest bones are
preserved. The additional preview_stand action owns the display pose; Godot
only selects and pauses it.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def world(rig, name):
    return rig.matrix_world @ rig.pose.bones[name].matrix


def rotate_world(rig, name, rotation):
    current = world(rig, name)
    basis = rotation.to_matrix() @ current.to_3x3()
    rig.pose.bones[name].matrix = rig.matrix_world.inverted() @ (
        Matrix.Translation(current.translation) @ basis.to_4x4())
    bpy.context.view_layer.update()


def reset_pose(rig):
    rig.animation_data_clear()
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def solve_leg(rig, side, foot_transform, knee_pole):
    thigh_name, shin_name, foot_name = (side + part for part in ("UpLeg", "Leg", "Foot"))
    start = world(rig, thigh_name).translation
    knee = world(rig, shin_name).translation
    ankle = world(rig, foot_name).translation
    target = foot_transform.translation
    upper, lower = (knee - start).length, (ankle - knee).length
    delta = target - start
    distance = min(delta.length, upper + lower - 0.00001)
    if distance <= abs(upper - lower):
        raise RuntimeError(f"{side} leg cannot reach the authored foot position")
    direction = delta.normalized()
    pole = knee_pole - start
    pole -= direction * pole.dot(direction)
    if pole.length < 0.00001:
        raise RuntimeError(f"{side} leg has no stable knee plane")
    pole.normalize()
    along = (upper * upper - lower * lower + distance * distance) / (2 * distance)
    height = math.sqrt(max(0, upper * upper - along * along))
    desired_knee = start + direction * along + pole * height
    rotate_world(rig, thigh_name, (knee - start).rotation_difference(desired_knee - start))
    knee = world(rig, shin_name).translation
    ankle = world(rig, foot_name).translation
    rotate_world(rig, shin_name, (ankle - knee).rotation_difference(target - knee))
    error = (world(rig, foot_name).translation - target).length
    if error > 0.002:
        raise RuntimeError(f"{side} foot moved {error:.5f}m during balance solve")
    # Keep the boot and its child toes in the original planted orientation.
    rig.pose.bones[foot_name].matrix = rig.matrix_world.inverted() @ foot_transform
    bpy.context.view_layer.update()


def level_spine_segment(rig, parent, child):
    delta = world(rig, child).translation - world(rig, parent).translation
    angle = -math.atan2(delta.x, delta.z)
    rotate_world(rig, parent, Matrix.Rotation(angle, 4, "Y").to_quaternion())


def author_pose(rig):
    reset_pose(rig)
    feet = {side: world(rig, side + "Foot").copy() for side in ("Left", "Right")}
    knees = {side: world(rig, side + "Leg").translation.copy() for side in feet}
    hips = world(rig, "Hips")
    midpoint = (feet["Left"].translation.x + feet["Right"].translation.x) / 2
    shift = midpoint - hips.translation.x
    height_shift = 0.0
    for side, foot in feet.items():
        thigh = world(rig, side + "UpLeg").translation
        knee = world(rig, side + "Leg").translation
        reach = (knee - thigh).length + (foot.translation - knee).length - 0.001
        thigh.x += shift
        horizontal = (Vector((thigh.x, thigh.y)) - Vector((foot.translation.x, foot.translation.y))).length
        if horizontal >= reach:
            raise RuntimeError(f"{side} leg cannot balance over the planted feet")
        height_shift = min(height_shift, foot.translation.z + math.sqrt(reach * reach - horizontal * horizontal) - thigh.z)
    hips.translation.x = midpoint
    hips.translation.z += height_shift
    rig.pose.bones["Hips"].matrix = rig.matrix_world.inverted() @ hips
    bpy.context.view_layer.update()
    for side in feet:
        solve_leg(rig, side, feet[side], knees[side])

    for parent, child in (("Spine", "Spine1"), ("Spine1", "Spine2"),
                          ("Spine2", "Neck"), ("Neck", "Head")):
        level_spine_segment(rig, parent, child)
    head_up = world(rig, "Head").to_3x3().col[1]
    rotate_world(rig, "Head", Matrix.Rotation(
        -math.atan2(head_up.x, head_up.z), 4, "Y").to_quaternion())

    shoulder_height = sum(world(rig, side + "Arm").translation.z for side in feet) / 2
    for side in feet:
        shoulder = world(rig, side + "Shoulder").translation
        arm = world(rig, side + "Arm").translation
        target = arm.copy()
        target.z = shoulder_height
        delta = arm - shoulder
        lateral_squared = delta.length_squared - delta.y * delta.y - (target.z - shoulder.z) ** 2
        if lateral_squared <= 0:
            raise RuntimeError(f"{side} collarbone cannot reach the level shoulder line")
        target.x = shoulder.x + math.copysign(math.sqrt(lateral_squared), delta.x)
        rotate_world(rig, side + "Shoulder", (arm - shoulder).rotation_difference(target - shoulder))

    old = bpy.data.actions.get("preview_stand")
    if old is not None:
        bpy.data.actions.remove(old)
    action = bpy.data.actions.new("preview_stand")
    rig.animation_data_create()
    rig.animation_data.action = action
    for bone in rig.pose.bones:
        for frame in (0, 1):
            for channel in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(data_path=channel, frame=frame, group=bone.name)
    action.use_fake_user = True
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "CONSTANT"
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    head = world(rig, "Head").translation
    hips = world(rig, "Hips").translation
    shoulder_delta = abs(world(rig, "LeftArm").translation.z - world(rig, "RightArm").translation.z)
    head_offset = abs(head.x - hips.x)
    if head_offset > 0.008 or shoulder_delta > 0.008:
        raise RuntimeError(f"pose remains tilted: head_offset={head_offset:.5f}, shoulders={shoulder_delta:.5f}")
    print(f"PREVIEW_STAND_AUTHOR_CHECK head_offset={head_offset:.5f} shoulder_delta={shoulder_delta:.5f}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--blend", required=True, type=Path)
    config = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if config.input.suffix == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(config.input.resolve()))
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(config.input.resolve()))
    rig = bpy.data.objects["QuaterniusOperatorRig"]
    root = bpy.data.objects["QuaterniusOperator"]
    # Importer actions with no live user must survive saving the editable source.
    for action in bpy.data.actions:
        action.use_fake_user = True
    author_pose(rig)
    root["steel_tide_preview_pose"] = "preview_stand: balanced feet, upright spine, level shoulders"
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    config.output.parent.mkdir(parents=True, exist_ok=True)
    config.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(config.blend.resolve()), compress=True)
    bpy.ops.export_scene.gltf(
        filepath=str(config.output.resolve()), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=False, export_skins=True,
        export_animations=True, export_animation_mode="BROADCAST", export_nla_strips=False,
        export_def_bones=True, export_leaf_bone=False, export_materials="EXPORT",
        export_image_format="AUTO", export_texcoords=True, export_normals=True,
        export_tangents=False, export_all_influences=False,
    )
    print(f"PREVIEW_STAND_AUTHOR_PASS valid=true output={config.output}")


if __name__ == "__main__":
    main()
