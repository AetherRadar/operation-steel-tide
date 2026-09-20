"""Bake the idle rifle contact frame over moving torsos, preserving leg keys."""

import math

import bpy
from mathutils import Matrix, Vector

from author_hy3d_rifle_carry import ARM_BONES, remove_curves, solve_two_bone, world_pose
from repair_hy3d_gait import sample_action


def stabilize_rifle_actions(rig):
    for family in ('ready', 'aim'):
        sample_action(rig, bpy.data.actions[family + '_idle'], 0)
        chest = world_pose(rig, 'Spine2').translation
        hands = {side: world_pose(rig, side + 'Hand') for side in ('Right', 'Left')}
        offset = hands['Right'].translation - chest
        span = hands['Left'].translation - hands['Right'].translation
        poles = {side: world_pose(rig, side + 'ForeArm').translation
                 - world_pose(rig, side + 'Arm').translation for side in hands}
        for gait in ('walk', 'run', 'sprint', 'crouch_idle', 'crouch_walk'):
            action = bpy.data.actions[family + '_' + gait]
            start, end = action.frame_range
            frames = [start + (end - start) * index / math.ceil(end - start)
                      for index in range(math.ceil(end - start) + 1)]
            poses = []
            maximum_error = 0.0
            for frame in frames:
                sample_action(rig, action, frame)
                chest = world_pose(rig, 'Spine2').translation
                primary = chest + offset
                shoulders = {side: world_pose(rig, side + 'Arm').translation for side in hands}
                limits = []
                minimum_right_reach = 0.0
                for side in hands:
                    elbow = world_pose(rig, side + 'ForeArm').translation
                    wrist = world_pose(rig, side + 'Hand').translation
                    upper = (elbow - shoulders[side]).length
                    lower = (wrist - elbow).length
                    reach = upper + lower - .004
                    if side == 'Right':
                        minimum_right_reach = math.sqrt(upper * upper + lower * lower
                                                        - 2 * upper * lower * math.cos(math.radians(57)))
                    limits.append((shoulders[side] - (span if side == 'Left' else Vector()), reach))
                # Project the rigid two-wrist contact frame into both arms' reach.
                for _ in range(128):
                    for center, reach in limits:
                        delta = primary - center
                        if delta.length > reach:
                            primary = center + delta.normalized() * reach
                    primary.y = min(primary.y, chest.y - .025)
                    delta = primary - shoulders['Right']
                    if delta.length < minimum_right_reach:
                        primary = shoulders['Right'] + delta.normalized() * minimum_right_reach
                if any((primary - center).length > reach + .0001 for center, reach in limits):
                    raise RuntimeError(f'{action.name}:{frame}: rifle contacts exceed arm reach')
                for side in hands:
                    target = primary + (span if side == 'Left' else Vector())
                    error = solve_two_bone(rig, side, target, shoulders[side] + poles[side])
                    maximum_error = max(maximum_error, error)
                    basis = hands[side].to_3x3().normalized().to_4x4()
                    rig.pose.bones[side + 'Hand'].matrix = rig.matrix_world.inverted() @ (
                        Matrix.Translation(world_pose(rig, side + 'Hand').translation) @ basis)
                    bpy.context.view_layer.update()
                pose = {name: rig.pose.bones[name].rotation_quaternion.copy() for name in ARM_BONES}
                if poses:
                    for name, rotation in pose.items():
                        if rotation.dot(poses[-1][name]) < 0:
                            rotation.negate()
                poses.append(pose)
            if maximum_error > .005:
                raise RuntimeError(f'{action.name}: rifle contact error {maximum_error}')
            remove_curves(action, ARM_BONES)
            for name in ARM_BONES:
                for index in range(4):
                    curve = action.fcurves.new(f'pose.bones["{name}"].rotation_quaternion',
                                              index=index, action_group=name)
                    for frame, pose in zip(frames, poses):
                        key = curve.keyframe_points.insert(frame, pose[name][index], options={'FAST'})
                        key.interpolation = 'LINEAR'
                    curve.update()
            print(f'OPERATOR_RIFLE_STABILITY_CHECK action={action.name} '
                  f'frames={len(frames)} error={maximum_error:.7f} valid=true', flush=True)
