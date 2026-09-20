"""Bake a common gameplay pivot while retaining each authored body pose."""
from __future__ import annotations

import math

import bpy
from mathutils import Matrix, Vector


def replace_bone_channels(action, name, frames, samples):
    prefix = f'pose.bones["{name}"]'
    for curve in list(action.fcurves):
        if curve.data_path in {prefix + '.' + prop for prop in ('rotation_quaternion', 'location', 'scale')}:
            action.fcurves.remove(curve)
    previous = None
    for rotation, _, _ in samples:
        if previous is not None and rotation.dot(previous) < 0:
            rotation.negate()
        previous = rotation
    for value_index, (prop, count) in enumerate((('rotation_quaternion', 4), ('location', 3), ('scale', 3))):
        for index in range(count):
            curve = action.fcurves.new(prefix + '.' + prop, index=index, action_group=name)
            curve.keyframe_points.add(len(frames))
            for key, frame, sample in zip(curve.keyframe_points, frames, samples):
                key.co = (frame, sample[value_index][index])
                key.interpolation = 'LINEAR'
            curve.update()


def center_actions(rig, sample_action):
    hips = rig.pose.bones['Hips']
    ancestors = hips.parent_recursive
    if len(ancestors) != 1 or ancestors[0].name != 'root':
        raise RuntimeError(f'{rig.name}: expected one authored root above Hips')
    root = ancestors[0]
    inverse = rig.matrix_world.inverted().to_3x3()
    reference = rig.matrix_world @ hips.bone.head_local
    for action in list(bpy.data.actions):
        start, end = action.frame_range
        intervals = max(1, math.ceil(end - start))
        frames = [start + (end - start) * index / intervals for index in range(intervals + 1)]
        sample_action(rig, action, start)
        origin = rig.matrix_world @ hips.head
        offset = inverse @ Vector((origin.x - reference.x, origin.y - reference.y, 0.0))
        hip_samples = []
        root_samples = []
        for frame in frames:
            sample_action(rig, action, frame)
            matrix = hips.matrix.copy()
            matrix.translation -= offset
            # Preserve world body orientation by absorbing the obsolete root
            # rotation into Hips, avoiding a rotation about its distant pivot.
            root.matrix_basis = Matrix.Identity(4)
            bpy.context.view_layer.update()
            hips.matrix = matrix
            hip_samples.append((hips.rotation_quaternion.copy(), hips.location.copy(), hips.scale.copy()))
            root_samples.append((root.rotation_quaternion.copy(), root.location.copy(), root.scale.copy()))
        replace_bone_channels(action, 'root', frames, root_samples)
        replace_bone_channels(action, 'Hips', frames, hip_samples)
    names = ('idle', 'walk', 'run', 'ready_idle', 'ready_walk', 'aim_idle', 'aim_walk',
             'pistol_ready_idle', 'pistol_ready_walk', 'prone_idle', 'downed')
    maximum = 0.0
    for name in names:
        action = bpy.data.actions[name]
        sample_action(rig, action, action.frame_range[0])
        point = rig.matrix_world @ hips.head
        maximum = max(maximum, math.hypot(point.x - reference.x, point.y - reference.y))
    if maximum > 1.0e-5:
        raise RuntimeError(f'Operator action pivots differ by {maximum} meters')
    print(f'OPERATOR_TRANSITION_CHECK clips={len(names)} max_horizontal_offset={maximum:.8f} valid=true', flush=True)
