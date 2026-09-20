"""Author upright running support and a closed rifle recoil in Blender."""
import math
import bpy
from mathutils import Vector
import repair_hy3d_gait as gait
from finalize_operator_contacts import _solve_leg
from author_hy3d_rifle_carry import solve_two_bone, world_pose


def refine_running(rig):
    if rig.get('steel_tide_upright_run_revision') == 1:
        return
    gait.sample_action(rig, bpy.data.actions['aim_idle'], 0)
    standing = rig.pose.bones['Hips'].head.z
    legs = tuple(side + part for side in ('Left', 'Right') for part in ('UpLeg', 'Leg', 'Foot'))
    for family in ('run', 'sprint'):
        action = bpy.data.actions[family]
        start, end = action.frame_range
        frames = [start + (end - start) * i / 128 for i in range(129)]
        corrected = []
        heights = []
        for frame in frames:
            gait.sample_action(rig, action, frame)
            hips = rig.pose.bones['Hips']
            old_height = hips.head.z
            targets = {}
            max_lift = standing * .97 - old_height
            for side in ('Left', 'Right'):
                thigh, shin, foot = [rig.pose.bones[side + p] for p in ('UpLeg', 'Leg', 'Foot')]
                target = foot.head.copy()
                target.y = hips.head.y + (target.y - hips.head.y) * .72
                targets[side] = target
                reach = ((shin.head - thigh.head).length + (foot.head - shin.head).length) * .985
                horizontal = (target.x - thigh.head.x)**2 + (target.y - thigh.head.y)**2
                max_lift = min(max_lift, target.z + math.sqrt(max(0, reach**2 - horizontal)) - thigh.head.z)
            matrix = hips.matrix.copy()
            matrix.translation.z += max(0, max_lift)
            hips.matrix = matrix
            bpy.context.view_layer.update()
            for side, target in targets.items():
                _solve_leg(rig, side, target)
            heights.append(hips.head.z)
            channels = gait.pose_channels(rig)
            corrected.append({name: channels[name] for name in ('Hips',) + legs})
        corrected[-1] = corrected[0]
        for prefix in ('', 'ready_', 'aim_', 'pistol_ready_', 'pistol_aim_'):
            original = bpy.data.actions[prefix + family]
            samples = []
            for frame, correction in zip(frames, corrected):
                gait.sample_action(rig, original, frame)
                channels = gait.pose_channels(rig)
                channels.update({name: tuple(v.copy() for v in values) for name, values in correction.items()})
                samples.append(channels)
            gait.write_action(rig, prefix + family, frames, samples)
        print(f'UPRIGHT_RUN_CHECK gait={family} standing_hips={standing:.4f} min_hips={min(heights):.4f} max_hips={max(heights):.4f}', flush=True)
    rig['steel_tide_upright_run_revision'] = 1


def author_closed_recoil(rig):
    for prefix in ('', 'pistol_'):
        reference = bpy.data.actions[prefix + 'aim_idle']
        gait.sample_action(rig, reference, 0)
        hands = {side: world_pose(rig, side + 'Hand') for side in ('Left', 'Right')}
        poles = {side: world_pose(rig, side + 'ForeArm').translation for side in hands}
        frames = [0, 1, 2, 3, 4, 5, 6]
        samples = []
        for frame in frames:
            gait.sample_action(rig, reference, 0)
            recoil = (0, .65, 1, .55, .2, .04, 0)[frame]
            for side in hands:
                target = hands[side].translation + Vector((0, .014 * recoil, .003 * recoil))
                error = solve_two_bone(rig, side, target, poles[side])
                if error > .005:
                    raise RuntimeError(f'{prefix}shoot: unreachable recoil contact')
                matrix = hands[side].copy()
                matrix.translation = target
                rig.pose.bones[side + 'Hand'].matrix = rig.matrix_world.inverted() @ matrix
                bpy.context.view_layer.update()
            samples.append(gait.pose_channels(rig))
        gait.write_action(rig, prefix + 'shoot', frames, samples)
        print(f'CLOSED_RECOIL_CHECK action={prefix}shoot travel=0.0143 closed=true', flush=True)
