"""Author a distinct, compact two-hand pistol hold and gun sockets in Blender.

Call ``author_pistol_pose(rig)`` after skin and locomotion repairs in an open
operator .blend. This pass edits source arm poses and adds the complete pistol
action family. Runtime code selects these clips and the matching authored
socket; it must not move either hand to fit a weapon.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from author_hy3d_rifle_carry import (
    ARM_BONES, FINGER_BONES, add_bone_marker, insert_constant_rotation,
    pose_bone, remove_curves, reset_pose, solve_two_bone, world_pose,
)


PISTOL_ACTIONS = (
    "ready_idle", "ready_walk", "ready_run", "ready_sprint",
    "ready_crouch_idle", "ready_crouch_walk", "aim_idle", "aim_walk",
    "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk", "shoot",
)

# These contacts are in each existing third-person weapon root, before the
# operator's authored 0.40 carry scale. GSh-18 and service-pistol values are
# read from their Blender-exported PrimaryGripSocket. Desert Eagle's source
# does not have a grip marker, so its contact is authored from the centred
# mesh silhouette used by its existing third-person import contract.
PISTOL_ASSETS = {
    "GSh18": ("gsh18_reloadable.glb", 0.78 / 0.43),
}


def pistol_grip_coordinates(repo: Path) -> dict[str, Vector]:
    """Read original marker coordinates without retaining reference objects."""
    result = {}
    for platform, (filename, scale) in PISTOL_ASSETS.items():
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(
            repo / "assets/models/steel_tide_reloadable_weapons" / filename))
        imported = set(bpy.data.objects) - before
        marker = next((obj for obj in imported if obj.name == "PrimaryGripSocket"), None)
        if marker is None:
            raise RuntimeError(f"{platform} is missing the authored PrimaryGripSocket")
        result[platform] = marker.matrix_world.translation.copy() * scale
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
    # Service pistols have authored magazine/slide sockets but no palm node.
    # Use the mesh-fitted grip coordinates recorded by FirstPersonArmPoseCatalog;
    # the geometry and these coordinates share the same source metre frame.
    # Godot (x, y, z) becomes Blender (x, -z, y).
    result["P226"] = Vector((0.0, -0.23701, -0.03017))
    result["M1911"] = Vector((-0.01132, -0.245, -0.03))
    result["DesertEagle"] = Vector((0.00144, -0.23571, 0.00954)) * (1.05 / 0.48)
    return result


def author_context_action(rig, source_name, basis, left_basis, right_offset, left_offset):
    """Bake grip contacts in prone/reload source frames, keeping body motion."""
    source = bpy.data.actions.get(source_name)
    if source is None:
        raise RuntimeError(f"missing source action {source_name}")
    start, end = (int(value) for value in source.frame_range)
    frames = list(range(start, end + 1))
    poses = []
    maximum_error = 0.0
    maximum_error_context = None
    for frame in frames:
        for bone in rig.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        rig.animation_data.action = source
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        right_shoulder = world_pose(rig, "RightArm").translation
        left_shoulder = world_pose(rig, "LeftArm").translation
        midpoint = (right_shoulder + left_shoulder) * 0.5
        if source_name.startswith("prone_"):
            primary = midpoint + Vector((-0.025, -0.25, -0.015))
            primary.z = max(0.14, primary.z)
            support = primary + Vector((0.048, 0.015, -0.008))
        else:
            # Reload brings the firing hand toward the right shoulder. The
            # source torso turns during this action; an actor-centred aim
            # target would pull the rearward firing shoulder beyond reach.
            primary = right_shoulder + Vector((0.12, -0.25, -0.15))
            travel = math.sin(math.pi * (frame - start) / max(1, end - start)) ** 2
            support = primary + Vector((0.048, 0.015 + 0.055 * travel, -0.008 - 0.13 * travel))
        for side, palm, hand_basis, offset, shoulder, pole in (
            ("Right", primary, basis, right_offset, right_shoulder, Vector((-0.23, 0.02, -0.18))),
            ("Left", support, left_basis, left_offset, left_shoulder, Vector((0.23, 0.02, -0.18))),
        ):
            error = solve_two_bone(rig, side, palm - hand_basis @ offset, shoulder + pole)
            if error > maximum_error:
                maximum_error_context = (frame, side, tuple(shoulder), tuple(palm))
            maximum_error = max(maximum_error, error)
            hand = pose_bone(rig, side + "Hand")
            hand.matrix = rig.matrix_world.inverted() @ (
                Matrix.Translation(world_pose(rig, side + "Hand").translation) @ hand_basis.to_4x4())
            bpy.context.view_layer.update()
        poses.append({name: pose_bone(rig, name).rotation_quaternion.copy() for name in ARM_BONES})
    if maximum_error > 0.005:
        raise RuntimeError(f"{source_name} pistol contact exceeds arm reach: {maximum_error}; {maximum_error_context}")
    name = "pistol_" + source_name
    previous = bpy.data.actions.get(name)
    if previous is not None:
        bpy.data.actions.remove(previous, do_unlink=True)
    action = source.copy()
    action.name = name
    action.use_fake_user = True
    remove_curves(action, ARM_BONES + FINGER_BONES)
    for bone_name in ARM_BONES:
        for index in range(4):
            curve = action.fcurves.new(f'pose.bones["{bone_name}"].rotation_quaternion',
                                      index=index, action_group=bone_name)
            for frame, pose in zip(frames, poses):
                key = curve.keyframe_points.insert(frame, pose[bone_name][index], options={"FAST"})
                key.interpolation = "LINEAR"
            curve.update()
    rig.animation_data.action = None
    print(f"PISTOL_CONTEXT_AUTHOR_CHECK action={name} frames={len(frames)} error={maximum_error:.7f}")
    return name


def preserve_authored_glove_shape(rig, actions):
    """Keep the existing curled glove in its authored rest shape in all clips.

    The generated distal joints sit outside the visible fingers. Their source
    translation/rotation channels pull one skin vertex away from its adjacent
    hand-weighted vertex in prone and death poses. Baking their local identity
    makes every distal skin influence follow the wrist as the original glove
    does; no runtime deformation or replacement geometry is involved.
    """
    fingers = tuple(bone.name for bone in rig.pose.bones
                    if any(bone.name.startswith(side + "Hand")
                           and bone.name != side + "Hand" for side in ("Left", "Right")))
    if len(fingers) < 30:
        raise RuntimeError("operator is missing the authored finger-chain contract")
    for action in actions:
        start, end = action.frame_range
        remove_curves(action, fingers)
        for name in fingers:
            for property_name, values in (
                ("rotation_quaternion", (1.0, 0.0, 0.0, 0.0)),
                ("location", (0.0, 0.0, 0.0)),
                ("scale", (1.0, 1.0, 1.0)),
            ):
                for index, value in enumerate(values):
                    curve = action.fcurves.new(f'pose.bones["{name}"].{property_name}',
                                              index=index, action_group=name)
                    for frame in (start, end):
                        key = curve.keyframe_points.insert(frame, value, options={"FAST"})
                        key.interpolation = "LINEAR"
                    curve.update()
    rig["steel_tide_glove_animation_repair"] = "authored_closed_glove_v1"
    print(f"AUTHORED_GLOVE_CHECK bones={len(fingers)} actions={len(actions)}")


def author_pistol_pose(rig: bpy.types.Object) -> dict:
    repo = Path(__file__).resolve().parents[2]
    references = pistol_grip_coordinates(repo)
    reset_pose(rig)
    rig.animation_data_create()
    meshes = [obj for obj in rig.children_recursive if obj.type == "MESH"
              and len(obj.data.vertices) > 1000]
    if not meshes:
        raise RuntimeError("operator is missing its authored skinned surface")
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    height = max(point.z for point in points) - min(point.z for point in points)
    carry_scale = 0.40 * height / 1.86
    right_shoulder = world_pose(rig, "RightArm").translation
    left_shoulder = world_pose(rig, "LeftArm").translation
    shoulder_midpoint = (right_shoulder + left_shoulder) * 0.5
    forward = Vector((0.0, -1.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    weapon_right = Vector((-1.0, 0.0, 0.0))
    basis = Matrix((up, forward, -weapon_right)).transposed()
    left_basis = Matrix((-up, forward, weapon_right)).transposed()
    right_palm = shoulder_midpoint + Vector((-0.025, -0.34, -0.15))
    # The support hand encloses the firing hand immediately beside the grip.
    # It must never remain at the rifle's distant handguard contact.
    left_palm = right_palm + Vector((0.048, 0.015, -0.008))
    # Contacts are inside the existing curled glove, not beyond its knuckles.
    # The rifle palm marker was 9 cm forward and 5 cm across the hand, placing
    # a pistol above the fingertips. These offsets put the backstrap between
    # the thumb web and closed fingers on the authored glove surface.
    right_offset = Vector((-0.025, 0.065, 0.025))
    left_offset = Vector((0.020, 0.065, 0.025))
    errors = []
    for side, palm, hand_basis, offset, shoulder, pole in (
        ("Right", right_palm, basis, right_offset, right_shoulder, Vector((-0.23, 0.02, -0.30))),
        ("Left", left_palm, left_basis, left_offset, left_shoulder, Vector((0.23, 0.02, -0.30))),
    ):
        errors.append(solve_two_bone(rig, side, palm - hand_basis @ offset, shoulder + pole))
        hand = pose_bone(rig, side + "Hand")
        transform = world_pose(rig, side + "Hand")
        hand.matrix = rig.matrix_world.inverted() @ (
            Matrix.Translation(transform.translation) @ hand_basis.to_4x4())
        bpy.context.view_layer.update()
    if max(errors) > 0.005:
        raise RuntimeError(f"authored pistol hold is beyond arm reach: {errors}")
    arm_pose = {name: pose_bone(rig, name).rotation_quaternion.copy() for name in ARM_BONES}
    # Aim and shoot raise the joined hands while retaining the same wrist
    # frame. A right-hand-parented authored socket follows this lift with a
    # level barrel; pitching the chest would instead point the gun upward.
    aim_lift = Vector((0.0, -0.055, 0.17))
    for side, palm, hand_basis, offset, shoulder, pole in (
        ("Right", right_palm, basis, right_offset, right_shoulder, Vector((-0.23, 0.02, -0.26))),
        ("Left", left_palm, left_basis, left_offset, left_shoulder, Vector((0.23, 0.02, -0.26))),
    ):
        errors.append(solve_two_bone(rig, side, palm + aim_lift - hand_basis @ offset, shoulder + pole))
        hand = pose_bone(rig, side + "Hand")
        hand.matrix = rig.matrix_world.inverted() @ (
            Matrix.Translation(world_pose(rig, side + "Hand").translation) @ hand_basis.to_4x4())
        bpy.context.view_layer.update()
    if max(errors) > 0.005:
        raise RuntimeError(f"authored pistol aim is beyond arm reach: {errors}")
    aim_pose = {name: pose_bone(rig, name).rotation_quaternion.copy() for name in ARM_BONES}
    authored = []
    for source_name in PISTOL_ACTIONS:
        source = bpy.data.actions.get(source_name)
        if source is None:
            raise RuntimeError(f"missing source action {source_name}")
        action_name = "pistol_" + source_name
        previous = bpy.data.actions.get(action_name)
        if previous is not None:
            bpy.data.actions.remove(previous, do_unlink=True)
        action = source.copy()
        action.name = action_name
        action.use_fake_user = True
        # Preserve the closed authored glove. Synthetic distal finger curves
        # in the old source action use joints outside that glove and must not
        # deform its surface during a shot or carry transition.
        remove_curves(action, ARM_BONES + FINGER_BONES)
        frames = list(range(int(action.frame_range.x), int(action.frame_range.y) + 1))
        selected_pose = aim_pose if source_name.startswith("aim_") or source_name == "shoot" else arm_pose
        for name in ARM_BONES:
            bone = pose_bone(rig, name)
            bone.rotation_quaternion = selected_pose[name]
            insert_constant_rotation(action, bone, frames)
        authored.append(action_name)
    rig.animation_data.action = None
    for name, rotation in arm_pose.items():
        pose_bone(rig, name).rotation_quaternion = rotation
    bpy.context.view_layer.update()
    # Converted firearm assets use Blender +Y for muzzle-forward. Turn them
    # around to match the operator's -Y facing in the Blender source file.
    weapon_rotation = Matrix.Rotation(math.pi, 4, "Z")
    scale_basis = weapon_rotation @ Matrix.Scale(carry_scale, 4)
    for platform, grip in references.items():
        transform = scale_basis.copy()
        transform.translation = right_palm - scale_basis.to_3x3() @ grip
        socket = add_bone_marker(rig, "PistolCarrySocket_" + platform, "RightHand", transform,
                                 "two_hand_pistol_root_" + platform)
        contact_name = "PistolGripContact_" + platform
        previous = bpy.data.objects.get(contact_name)
        if previous is not None:
            bpy.data.objects.remove(previous, do_unlink=True)
        contact = bpy.data.objects.new(contact_name, None)
        bpy.context.collection.objects.link(contact)
        contact.parent = socket
        contact.location = grip
        contact["steel_tide_contact_role"] = "authored_pistol_grip_surface"
    add_bone_marker(rig, "PistolRightPalmFrame", "RightHand", Matrix.Translation(right_palm),
                    "pistol_primary_palm")
    add_bone_marker(rig, "PistolLeftPalmFrame", "LeftHand", Matrix.Translation(left_palm),
                    "pistol_support_palm")
    back_socket = bpy.data.objects.get("BackWeaponSocket")
    if back_socket is None:
        raise RuntimeError("operator is missing BackWeaponSocket")
    back_socket.scale = (carry_scale,) * 3
    for context in ("prone_idle", "prone_crawl", "reload"):
        authored.append(author_context_action(rig, context, basis, left_basis, right_offset, left_offset))
    preserve_authored_glove_shape(rig, list(bpy.data.actions))
    rig["steel_tide_authored_pistol_pose"] = "compact_two_hand_v1"
    result = {"actions": len(authored), "right_error": errors[0], "left_error": errors[1],
              "aim_right_error": errors[2], "aim_left_error": errors[3],
              "palm_separation": (right_palm - left_palm).length}
    print("PISTOL_CARRY_AUTHOR_CHECK", result)
    return result
