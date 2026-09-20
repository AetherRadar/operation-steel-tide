"""Bake the CC0 UAL gait and floor contact into the authored operator rig.

Invoke repair_locomotion(rig, source_path) from the final Blender art pipeline.
Source animation timing and complete cycles are retained; no runtime bone or
visual-root correction is needed. Existing rifle arm/finger contact poses are
preserved as local channels over the moving torso.
"""
from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

BONES = {
    'pelvis': 'Hips', 'spine_01': 'Spine', 'spine_02': 'Spine1',
    'spine_03': 'Spine2', 'neck_01': 'Neck', 'Head': 'Head',
    'clavicle_l': 'LeftShoulder', 'upperarm_l': 'LeftArm',
    'lowerarm_l': 'LeftForeArm', 'hand_l': 'LeftHand',
    'clavicle_r': 'RightShoulder', 'upperarm_r': 'RightArm',
    'lowerarm_r': 'RightForeArm', 'hand_r': 'RightHand',
    'thigh_l': 'LeftUpLeg', 'calf_l': 'LeftLeg', 'foot_l': 'LeftFoot',
    'thigh_r': 'RightUpLeg', 'calf_r': 'RightLeg', 'foot_r': 'RightFoot',
}
SOURCES = {
    'walk': 'Walk_Loop', 'run': 'Jog_Fwd_Loop', 'sprint': 'Jog_Fwd_Loop',
    'crouch_idle': 'Crouch_Idle_Loop', 'crouch_walk': 'Crouch_Fwd_Loop',
    'prone_crawl': 'Swim_Fwd_Loop',
    'death': 'Death01',
}
CARRY_BONES = tuple(
    f'{side}{part}' for side in ('Left', 'Right')
    for part in ('Shoulder', 'Arm', 'ForeArm', 'Hand')
)


def sample_action(rig, action, frame):
    rig.animation_data_create()
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    # Updating the assignment alone does not invalidate a previously sampled
    # frame in Blender. Force evaluation before the requested sample.
    bpy.context.scene.frame_set(-1)
    bpy.context.scene.frame_set(math.floor(frame), subframe=frame % 1.0)
    bpy.context.view_layer.update()


def reset_pose(rig):
    rig.animation_data_create()
    rig.animation_data.action = None
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
        bone.rotation_mode = 'QUATERNION'


def pose_channels(rig):
    return {b.name: (b.rotation_quaternion.copy(), b.location.copy(), b.scale.copy())
            for b in rig.pose.bones}


def write_action(rig, name, frames, samples):
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old, do_unlink=True)
    action = bpy.data.actions.new(name)
    slot = action.slots.new(rig.id_type, rig.name)
    strip = action.layers.new('Authored motion').strips.new(type='KEYFRAME')
    bag = strip.channelbag(slot, ensure=True)
    for bone in rig.pose.bones:
        values = [sample[bone.name] for sample in samples]
        previous = None
        for rotation, _, _ in values:
            if previous is not None and rotation.dot(previous) < 0:
                rotation.negate()
            previous = rotation
        for value_index, (prop, count) in enumerate((('rotation_quaternion', 4), ('location', 3), ('scale', 3))):
            for index in range(count):
                curve = bag.fcurves.new(data_path=f'pose.bones["{bone.name}"].{prop}', index=index)
                curve.keyframe_points.add(len(frames))
                for key, frame, channels in zip(curve.keyframe_points, frames, values):
                    key.co = (frame, channels[value_index][index])
                    key.interpolation = 'LINEAR'
                curve.update()
    action.use_fake_user = True
    return action


def mesh_floor(mesh):
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    return min((evaluated.matrix_world @ Vector(corner)).z for corner in evaluated.bound_box)


def floor_pose(rig, mesh, clearance=0.008):
    shift = clearance - mesh_floor(mesh)
    hips = rig.pose.bones['Hips']
    matrix = hips.matrix.copy()
    matrix.translation += rig.matrix_world.inverted().to_3x3() @ Vector((0, 0, shift))
    hips.matrix = matrix
    bpy.context.view_layer.update()


def settle_prone_gaze(rig, meshes, presentation_scale):
    """Keep the swimming source's raised gaze inside a low crawling profile.

    Tall helmets otherwise rise above the prone silhouette at the stroke's
    highest sample. This authors a small downward head pitch only where the
    actual mesh needs it, retaining the pelvis, limbs and ground contacts.
    """
    def height():
        graph = bpy.context.evaluated_depsgraph_get()
        values = [
            (evaluated.matrix_world @ Vector(corner)).z
            for mesh in meshes for evaluated in (mesh.evaluated_get(graph),)
            for corner in evaluated.bound_box
        ]
        return (max(values) - min(values)) * presentation_scale

    if height() <= 0.695:
        return 0
    head = rig.pose.bones['Head']
    original = rig.matrix_world @ head.matrix
    initial_height = height()
    best_height = initial_height
    best_direction = 0
    for direction in (1, -1):
        for degrees in range(1, 61):
            matrix = Matrix.Rotation(direction * math.radians(degrees), 4, 'X') @ original
            matrix.translation = original.translation
            head.matrix = rig.matrix_world.inverted() @ matrix
            bpy.context.view_layer.update()
            current_height = height()
            if current_height < best_height:
                best_height = current_height
                best_direction = direction * degrees
            if current_height <= 0.695:
                print(f'PRONE_SETTLE_CHECK initial={initial_height:.6f} final={current_height:.6f} '
                      f'head_degrees={direction * degrees} valid=true', flush=True)
                return direction * degrees
    head.matrix = rig.matrix_world.inverted() @ original
    bpy.context.view_layer.update()
    print(f'PRONE_SETTLE_CHECK initial={initial_height:.6f} best={best_height:.6f} '
          f'head_degrees={best_direction} valid=false', flush=True)
    raise RuntimeError('Prone silhouette cannot settle by adjusting the authored gaze')


def rest_frame(rig, names):
    def head(name):
        return rig.matrix_world @ rig.data.bones[names[name]].head_local
    # Both source and authored assets stand on Blender's XY floor. The
    # target's relaxed spine lean is posture, not a coordinate-system tilt.
    up = Vector((0.0, 0.0, 1.0))
    right = (head('left') - head('right')).normalized()
    forward = right.cross(up).normalized()
    right = up.cross(forward).normalized()
    return Matrix((right, forward, up)).transposed()


def repair_locomotion(rig, source_path):
    target_objects = set(bpy.data.objects)
    target_actions = set(bpy.data.actions)
    meshes = [o for o in rig.children_recursive if o.type == 'MESH']
    mesh = meshes[0]
    rest_heights = [(mesh.matrix_world @ vertex.co).z for mesh in meshes for vertex in mesh.data.vertices]
    presentation_scale = 1.86 / (max(rest_heights) - min(rest_heights))
    carry = {}
    for prefix in ('ready', 'aim'):
        sample_action(rig, bpy.data.actions[f'{prefix}_idle'], 8)
        carry[prefix] = {name: bone.matrix_basis.copy() for name, bone in rig.pose.bones.items()
                         if name in CARRY_BONES or 'Hand' in name}
    bpy.ops.import_scene.gltf(filepath=str(Path(source_path).resolve()))
    source_objects = set(bpy.data.objects) - target_objects
    source_actions = set(bpy.data.actions) - target_actions
    source = next(o for o in source_objects if o.type == 'ARMATURE')
    source_map = {a.name: a for a in source_actions}
    source_frame = rest_frame(source, {'head':'Head', 'hips':'pelvis', 'left':'clavicle_l', 'right':'clavicle_r'})
    target_frame = rest_frame(rig, {'head':'Head', 'hips':'Hips', 'left':'LeftShoulder', 'right':'RightShoulder'})
    frame_rotation = target_frame @ source_frame.transposed()
    target_inverse = rig.matrix_world.inverted()
    ordered = sorted(BONES.items(), key=lambda item: len(rig.data.bones[item[1]].parent_recursive))
    generated = {}
    for name, source_name in SOURCES.items():
        action = source_map[source_name]
        start, end = action.frame_range
        intervals = max(1, math.ceil(end - start))
        frames = [(end - start) * i / intervals for i in range(intervals + 1)]
        poses = []
        for frame in frames:
            sample_action(source, action, start + frame)
            reset_pose(rig)
            posed = {b.name: b.matrix_local.copy() for b in rig.data.bones}
            for source_bone, target_bone in ordered:
                src = source.pose.bones[source_bone]
                dst = rig.pose.bones[target_bone]
                rest = rig.matrix_world @ dst.bone.matrix_local
                rest_direction = (rig.matrix_world.to_3x3() @ (dst.bone.tail_local - dst.bone.head_local)).normalized()
                direction = frame_rotation @ (source.matrix_world.to_3x3() @ (src.tail - src.head)).normalized()
                if name == 'sprint' and target_bone in ('Spine', 'Spine1', 'Spine2', 'Neck', 'Head'):
                    direction = Matrix.Rotation(math.radians(4.0), 3, 'X') @ direction
                rotation = rest_direction.rotation_difference(direction)
                desired = rotation.to_matrix().to_4x4() @ rest
                # The current joint head follows its posed parent. Only the
                # pelvis receives translation; skin segment lengths stay fixed.
                parent = dst.bone.parent
                parent_pose = posed[parent.name] if parent else Matrix.Identity(4)
                parent_rest = parent.matrix_local if parent else Matrix.Identity(4)
                local = target_inverse @ desired
                local.translation = (parent_pose @ parent_rest.inverted() @ dst.bone.matrix_local).translation
                dst.matrix_basis = dst.bone.convert_local_to_pose(
                    local, dst.bone.matrix_local, parent_matrix=parent_pose,
                    parent_matrix_local=parent_rest, invert=True)
                dst.location = Vector((0, 0, 0))
                dst.scale = Vector((1, 1, 1))
                posed[target_bone] = local
            bpy.context.view_layer.update()
            floor_pose(rig, mesh)
            if name == 'prone_crawl':
                settle_prone_gaze(rig, meshes, presentation_scale)
            poses.append(pose_channels(rig))
        if name != 'death':
            poses[-1] = {n: tuple(c.copy() for c in v) for n, v in poses[0].items()}
        generated[name] = (frames, poses)
        write_action(rig, name, frames, poses)
        print(f'AUTHORED_GAIT_CHECK action={name} frames={len(frames)} duration={frames[-1]/bpy.context.scene.render.fps:.4f}', flush=True)
    # Swim_Idle_Loop is upright treading water. A stationary prone operator
    # instead holds the horizontal body pose from the crawl's middle sample.
    crawl_poses = generated['prone_crawl'][1]
    prone_hold = crawl_poses[len(crawl_poses) // 2]
    write_action(rig, 'prone_idle', [0, 24], [
        {n: tuple(v.copy() for v in values) for n, values in prone_hold.items()},
        {n: tuple(v.copy() for v in values) for n, values in prone_hold.items()},
    ])
    for prefix, arm_pose in carry.items():
        for name in ('walk', 'run', 'sprint', 'crouch_idle', 'crouch_walk'):
            frames, base_poses = generated[name]
            poses = []
            for channels in base_poses:
                pose = {n: tuple(v.copy() for v in values) for n, values in channels.items()}
                for bone_name, matrix in arm_pose.items():
                    location, rotation, scale = matrix.decompose()
                    pose[bone_name] = (rotation, location, scale)
                poses.append(pose)
            write_action(rig, f'{prefix}_{name}', frames, poses)
    # A complete terminal death pose holds after playback and is also the
    # downed body pose; no upright pelvis or one raised knee remains.
    terminal = generated['death'][1][-1]
    write_action(rig, 'downed', [0, 24], [terminal, {n: tuple(v.copy() for v in c) for n,c in terminal.items()}])
    for action in source_actions:
        bpy.data.actions.remove(action, do_unlink=True)
    for obj in source_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    sample_action(rig, bpy.data.actions['walk'], 0)
    print('AUTHORED_GAIT_PASS valid=true')
