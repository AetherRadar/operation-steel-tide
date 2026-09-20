"""Author prone rifle contact poses on the corrected Blender body animation.

The source prone clips remain available for unarmed characters. Authored rifle
variants hold the same rifle/palm frame used upright, with the muzzle level in
the horizontal pose. Invoke after repair_locomotion, before pistol authoring.
"""

from __future__ import annotations

import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from author_hy3d_rifle_carry import (
    ARM_BONES, FINGER_BONES, add_bone_marker, pose_bone,
    remove_curves, solve_two_bone, world_pose,
)
from repair_hy3d_gait import sample_action


def author_rifle_prone_pose(rig):
    sample_action(rig, bpy.data.actions['ready_idle'], 0)
    right_hand = world_pose(rig, 'RightHand')
    left_hand = world_pose(rig, 'LeftHand')
    right_palm = bpy.data.objects['RightPalmFrame'].matrix_world.translation.copy()
    left_palm = bpy.data.objects['LeftPalmFrame'].matrix_world.translation.copy()
    rifle = bpy.data.objects['RifleCarrySocket']
    rifle_world = rifle.matrix_world.copy()
    right_offset = right_hand.inverted() @ right_palm
    left_offset = left_hand.inverted() @ left_palm
    barrel = (rifle_world.to_3x3() @ Vector((0, 1, 0))).normalized()
    level = Vector((barrel.x, barrel.y, 0)).normalized()
    correction = barrel.rotation_difference(level).to_matrix()
    right_basis = correction @ right_hand.to_3x3().normalized()
    left_basis = correction @ left_hand.to_3x3().normalized()
    palm_span = correction @ (left_palm - right_palm)
    # The wrist carries the unchanged authored rifle frame. It follows the
    # new prone arm keys without inheriting the torso's 90-degree prone turn.
    add_bone_marker(rig, 'RifleCarrySocket', 'RightHand', rifle_world, 'two_hand_rifle_root')
    maximum_error = 0.0
    for source_name in ('prone_idle', 'prone_crawl'):
        source = bpy.data.actions[source_name]
        start, end = (int(v) for v in source.frame_range)
        frames = list(range(start, end + 1))
        poses = []
        for frame in frames:
            for bone in rig.pose.bones:
                bone.matrix_basis = Matrix.Identity(4)
            sample_action(rig, source, frame)
            # Arm lengths belong to the source rig; no imported translation
            # curve is allowed to stretch a forearm while solving the grip.
            for name in ARM_BONES:
                pose_bone(rig, name).location = Vector((0, 0, 0))
                pose_bone(rig, name).scale = Vector((1, 1, 1))
            bpy.context.view_layer.update()
            right_shoulder = world_pose(rig, 'RightArm').translation
            left_shoulder = world_pose(rig, 'LeftArm').translation
            primary = right_shoulder + Vector((0.015, -0.06, -0.035))
            primary.z = max(0.13, primary.z)
            # Fit the rigid two-palm frame inside both arms' reach before
            # solving the elbows. The prone torso rolls through its crawl
            # cycle, so a fixed shoulder offset can exceed the shorter arm
            # by a centimetre. This DCC placement keeps the weapon/palm span
            # intact and leaves a small bend in both arms in every key.
            limits = []
            for side, shoulder, basis, offset, span in (
                ('Right', right_shoulder, right_basis, right_offset, Vector()),
                ('Left', left_shoulder, left_basis, left_offset, palm_span),
            ):
                elbow = world_pose(rig, side + 'ForeArm').translation
                wrist = world_pose(rig, side + 'Hand').translation
                reach = (elbow - shoulder).length + (wrist - elbow).length - .008
                limits.append((shoulder + basis @ offset - span, reach))
            for _ in range(24):
                for center, reach in limits:
                    displacement = primary - center
                    if displacement.length > reach:
                        primary = center + displacement.normalized() * reach
                primary.z = max(.13, primary.z)
            if any((primary - center).length > reach + .0001 for center, reach in limits):
                raise RuntimeError('authored rifle palm span cannot fit both prone arms')
            support = primary + palm_span
            for side, target, basis, offset, shoulder, pole in (
                ('Right', primary, right_basis, right_offset, right_shoulder, Vector((-.25, .03, -.15))),
                ('Left', support, left_basis, left_offset, left_shoulder, Vector((.20, .03, -.15))),
            ):
                error = solve_two_bone(rig, side, target - basis @ offset, shoulder + pole)
                maximum_error = max(maximum_error, error)
                hand = pose_bone(rig, side + 'Hand')
                hand.matrix = rig.matrix_world.inverted() @ (
                    Matrix.Translation(world_pose(rig, side + 'Hand').translation) @ basis.to_4x4())
                bpy.context.view_layer.update()
            poses.append({name: pose_bone(rig, name).rotation_quaternion.copy() for name in ARM_BONES})
        name = 'rifle_' + source_name
        previous = bpy.data.actions.get(name)
        if previous:
            bpy.data.actions.remove(previous, do_unlink=True)
        action = source.copy()
        action.name = name
        action.use_fake_user = True
        remove_curves(action, ARM_BONES + FINGER_BONES)
        for bone in ARM_BONES:
            for index in range(4):
                curve = action.fcurves.new(f'pose.bones["{bone}"].rotation_quaternion',
                                          index=index, action_group=bone)
                for frame, pose in zip(frames, poses):
                    key = curve.keyframe_points.insert(frame, pose[bone][index], options={'FAST'})
                    key.interpolation = 'LINEAR'
                curve.update()
        print(f'RIFLE_PRONE_AUTHOR_CHECK action={name} frames={len(frames)} error={maximum_error:.7f}')
    if maximum_error > .005:
        raise RuntimeError(f'prone rifle support exceeds arm reach: {maximum_error}')
    rig['steel_tide_authored_rifle_prone'] = 'level_barrel_two_hand_v1'
    sample_action(rig, bpy.data.actions['ready_idle'], 0)
    return {'actions': 2, 'maximum_contact_error': maximum_error}
