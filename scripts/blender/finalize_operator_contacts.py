"""Bake complete running stance contacts into authored Blender actions."""

import math
import bpy
import numpy as np
from mathutils import Vector

import repair_hy3d_gait as gait


def _solve_leg(rig, side, target):
    thigh, shin, foot = [rig.pose.bones[side + part] for part in ('UpLeg', 'Leg', 'Foot')]
    hip, knee, ankle = [bone.head.copy() for bone in (thigh, shin, foot)]
    foot_matrix = foot.matrix.copy()
    upper, lower = (knee - hip).length, (ankle - knee).length
    axis = target - hip
    distance = min(axis.length, (upper + lower) * .999)
    if distance <= 1.e-6 or upper <= 1.e-6 or lower <= 1.e-6:
        raise RuntimeError(f'{side}: degenerate authored leg chain')
    axis.normalize()
    pole = knee - hip - axis * (knee - hip).dot(axis)
    if pole.length <= 1.e-6:
        raise RuntimeError(f'{side}: authored knee has no bend direction')
    pole.normalize()
    along = (upper * upper - lower * lower + distance * distance) / (2 * distance)
    bend = math.sqrt(max(0, upper * upper - along * along))
    desired_knee = hip + axis * along + pole * bend
    desired_ankle = hip + axis * distance
    for bone, start, end in ((thigh, hip, desired_knee), (shin, desired_knee, desired_ankle)):
        matrix = bone.matrix.copy()
        rotation = (bone.tail - bone.head).rotation_difference(end - start)
        matrix = rotation.to_matrix().to_4x4() @ matrix
        matrix.translation = start
        bone.matrix = matrix
        bpy.context.view_layer.update()
    foot_matrix.translation = desired_ankle
    foot.matrix = foot_matrix
    bpy.context.view_layer.update()


def plant_running_contacts(rig):
    if rig.get('steel_tide_running_contact_revision') == 2:
        return
    legs = tuple(side + part for side in ('Left', 'Right') for part in ('UpLeg', 'Leg', 'Foot'))
    for family in ('run', 'sprint'):
        action = bpy.data.actions[family]
        start, end = action.frame_range
        count = 128
        dt = (end - start) / count / bpy.context.scene.render.fps
        frames = [start + (end - start) * i / count for i in range(count + 1)]
        feet = []
        for frame in frames[:-1]:
            gait.sample_action(rig, action, frame)
            feet.append([np.array(rig.pose.bones[side + 'Foot'].head)
                         for side in ('Left', 'Right')])
        feet = np.asarray(feet)
        # Include touchdown's low stationary frames, not only the short push-off.
        heights = sum(np.roll(feet[:, :, 2], offset, axis=0) for offset in range(-4, 5)) / 9
        stance = heights < heights.min(axis=0) + .085
        intervals = []
        for side in range(2):
            starts = np.flatnonzero(stance[:, side] & ~np.roll(stance[:, side], 1))
            spans = []
            for first in starts:
                length = 0
                while length < count and stance[(first + length) % count, side]:
                    length += 1
                spans.append((int(first), length))
            if not spans:
                raise RuntimeError(f'{family}: no authored stance for side {side}')
            intervals.append(max(spans, key=lambda span: span[1]))
        if any(length < 4 or length > count // 2 for _, length in intervals):
            raise RuntimeError(f'{family}: invalid authored stance intervals {intervals}')
        speeds = [(feet[(first + length - 1) % count, side, 1] - feet[first, side, 1])
                  / ((length - 1) * dt) for side, (first, length) in enumerate(intervals)]
        speed = float(np.mean(speeds))
        if not math.isfinite(speed) or speed <= 0:
            raise RuntimeError(f'{family}: invalid authored stance speed {speed}')
        targets = feet.copy()
        for side, (first, length) in enumerate(intervals):
            center = first + (length - 1) / 2
            anchor = (feet[first, side] + feet[(first + length - 1) % count, side]) * .5
            fade = 10
            for relative in range(-fade, length + fade):
                index = (first + relative) % count
                offset = first + relative - center
                desired = anchor.copy()
                desired[1] += offset * dt * speed
                desired[2] = feet[index, side, 2]
                weight = 1.0
                if relative < 0:
                    weight = .5 + .5 * math.cos(math.pi * relative / fade)
                elif relative >= length:
                    weight = .5 + .5 * math.cos(math.pi * (relative - length + 1) / fade)
                targets[index, side] += (desired - feet[index, side]) * weight
        corrected = []
        for index, frame in enumerate(frames[:-1]):
            gait.sample_action(rig, action, frame)
            for side_index, side in enumerate(('Left', 'Right')):
                _solve_leg(rig, side, Vector(targets[index, side_index]))
            corrected.append({name: tuple(value.copy() for value in gait.pose_channels(rig)[name]) for name in legs})
        corrected.append(corrected[0])
        for prefix in ('', 'ready_', 'aim_', 'pistol_ready_', 'pistol_aim_'):
            name = prefix + family
            original = bpy.data.actions[name]
            samples = []
            for frame, channels in zip(frames, corrected):
                gait.sample_action(rig, original, frame)
                pose = gait.pose_channels(rig)
                pose.update({name: tuple(value.copy() for value in values) for name, values in channels.items()})
                samples.append(pose)
            gait.write_action(rig, name, frames, samples)
        print(f'OPERATOR_CONTACT_AUTHOR_CHECK gait={family} speed={speed:.6f} valid=true', flush=True)
    rig['steel_tide_running_contact_revision'] = 2
