"""Build the animated BAMEN operator used by combat AI.

The script retargets selected CC0 Quaternius actions onto the authored BAMEN
Mixamo skeleton, keeps locomotion in-place, adds attachment sockets, and
authors the missing prone/downed support poses needed by Steel Tide.

Run from the repository root with Blender 5.2 LTS or newer:
    blender --background --factory-startup \
        --python scripts/blender/build_animated_bamen_operator.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = REPO_ROOT / "source_art" / "third_party" / "bamen_military_soldier" / "bamen_military_soldier_clean.blend"
UAL1_GLB = REPO_ROOT / "source_art" / "third_party" / "quaternius_universal_animation_library" / "UAL1_Standard.glb"
UAL2_GLB = REPO_ROOT / "source_art" / "third_party" / "quaternius_universal_animation_library" / "UAL2_Standard.glb"
M4_GLB = REPO_ROOT / "assets" / "models" / "steel_tide_m4a1" / "steel_tide_m4a1.glb"
OUTPUT_BLEND = REPO_ROOT / "source_art" / "third_party" / "bamen_military_soldier" / "bamen_military_soldier_animated.blend"
OUTPUT_GLB = REPO_ROOT / "assets" / "models" / "bamen_military_soldier" / "bamen_military_soldier_animated.glb"
PREVIEW_DIR = REPO_ROOT / "build" / "art-previews" / "animated-operator"
RIFLE_AIM_ORIGIN = Vector((-0.16, -0.18, 1.61))
RIFLE_AIM_FORWARD = Vector((0.12, -0.993, 0.0)).normalized()
RIFLE_AIM_UP = Vector((0.0, 0.0, 1.0))
RIFLE_READY_ORIGIN = Vector((-0.10, -0.15, 1.40))
RIFLE_READY_FORWARD = Vector((0.48, -0.87, -0.10)).normalized()
RIFLE_READY_UP = Vector((0.05, -0.09, 0.995)).normalized()
RIFLE_SCALE = 0.476


BONE_MAP = {
    "pelvis": "mixamorig:Hips",
    "spine_01": "mixamorig:Spine",
    "spine_02": "mixamorig:Spine1",
    "spine_03": "mixamorig:Spine2",
    "neck_01": "mixamorig:Neck",
    "Head": "mixamorig:Head",
    "clavicle_l": "mixamorig:LeftShoulder",
    "upperarm_l": "mixamorig:LeftArm",
    "lowerarm_l": "mixamorig:LeftForeArm",
    "hand_l": "mixamorig:LeftHand",
    "clavicle_r": "mixamorig:RightShoulder",
    "upperarm_r": "mixamorig:RightArm",
    "lowerarm_r": "mixamorig:RightForeArm",
    "hand_r": "mixamorig:RightHand",
    "thigh_l": "mixamorig:LeftUpLeg",
    "calf_l": "mixamorig:LeftLeg",
    "foot_l": "mixamorig:LeftFoot",
    "ball_l": "mixamorig:LeftToeBase",
    "thigh_r": "mixamorig:RightUpLeg",
    "calf_r": "mixamorig:RightLeg",
    "foot_r": "mixamorig:RightFoot",
    "ball_r": "mixamorig:RightToeBase",
}


ACTION_SOURCES = {
    "idle": ("ual1", "Idle_Loop", True),
    "walk": ("ual1", "Walk_Loop", True),
    "run": ("ual1", "Jog_Fwd_Loop", True),
    "sprint": ("ual1", "Sprint_Loop", True),
    "crouch_idle": ("ual1", "Crouch_Idle_Loop", True),
    "crouch_walk": ("ual1", "Crouch_Fwd_Loop", True),
    "prone_idle": ("ual1", "Swim_Idle_Loop", True),
    "prone_crawl": ("ual1", "Swim_Fwd_Loop", True),
    "aim_idle": ("ual1", "Pistol_Aim_Neutral", True),
    "hit": ("ual1", "Hit_Chest", False),
    "death": ("ual1", "Death01", False),
    "revive_kneel": ("ual1", "Fixing_Kneeling", True),
    "revived": ("ual2", "LayToIdle", False),
    # Full third-person action coverage. These clips are CC0 Quaternius
    # actions retargeted and baked onto the authored operator skeleton.
    "shoot": ("ual1", "Pistol_Shoot", False),
    "reload": ("ual1", "Pistol_Reload", False),
    "melee": ("ual1", "Punch_Cross", False),
    "throw": ("ual2", "OverhandThrow", False),
    "interact": ("ual1", "Interact", False),
    "pickup": ("ual1", "PickUp_Table", False),
    "heal": ("ual2", "Consume", False),
    "jump_start": ("ual1", "Jump_Start", False),
    "jump_loop": ("ual1", "Jump_Loop", True),
    "jump_land": ("ual1", "Jump_Land", False),
    "slide_start": ("ual2", "Slide_Start", False),
    "slide_loop": ("ual2", "Slide_Loop", True),
    "slide_exit": ("ual2", "Slide_Exit", False),
}


AIM_LOCOMOTION_SOURCES = {
    "aim_walk": "walk",
    "aim_run": "run",
    "aim_sprint": "sprint",
    "aim_crouch_idle": "crouch_idle",
    "aim_crouch_walk": "crouch_walk",
}


READY_LOCOMOTION_SOURCES = {
    "ready_walk": "walk",
    "ready_run": "run",
    "ready_sprint": "sprint",
    "ready_crouch_idle": "crouch_idle",
    "ready_crouch_walk": "crouch_walk",
}


LOOP_ACTIONS = {
    name for name, (_, _, loop) in ACTION_SOURCES.items() if loop
} | set(AIM_LOCOMOTION_SOURCES) | set(READY_LOCOMOTION_SOURCES) | {
    "ready_idle", "prone_idle", "prone_crawl", "downed", "jump_loop", "slide_loop"
}


def require_file(path: Path) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)


def rotation_only(matrix: Matrix) -> Quaternion:
    return matrix.to_quaternion().normalized()


def matrix_from_rotation_translation(rotation: Quaternion, translation: Vector) -> Matrix:
    matrix = rotation.to_matrix().to_4x4()
    matrix.translation = translation
    return matrix


def rifle_world_rotation(rifle_forward: Vector, rifle_up: Vector) -> Quaternion:
    rifle_right = rifle_forward.cross(rifle_up).normalized()
    corrected_rifle_up = rifle_right.cross(rifle_forward).normalized()
    return Matrix((rifle_right, rifle_forward, corrected_rifle_up)).transposed().to_quaternion()


def set_pose_bone_world_rotation(
    armature: bpy.types.Object,
    bone_name: str,
    desired_world_rotation: Quaternion,
) -> None:
    bone = armature.pose.bones[bone_name]
    desired_local_rotation = (
        rotation_only(armature.matrix_world).inverted() @ desired_world_rotation
    ).normalized()
    bone.matrix = matrix_from_rotation_translation(desired_local_rotation, bone.matrix.translation)


def align_pose_bone_world_direction(
    armature: bpy.types.Object,
    bone_name: str,
    desired_direction: Vector,
) -> None:
    bone = armature.pose.bones[bone_name]
    current_direction = (
        armature.matrix_world @ bone.tail - armature.matrix_world @ bone.head
    ).normalized()
    current_world_rotation = rotation_only(armature.matrix_world @ bone.matrix)
    desired_world_rotation = (
        current_direction.rotation_difference(desired_direction.normalized())
        @ current_world_rotation
    ).normalized()
    desired_local_rotation = (
        rotation_only(armature.matrix_world).inverted() @ desired_world_rotation
    ).normalized()
    bone.matrix = matrix_from_rotation_translation(desired_local_rotation, bone.matrix.translation)


def reset_pose(armature: bpy.types.Object) -> None:
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.location = Vector((0.0, 0.0, 0.0))
        bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        bone.scale = Vector((1.0, 1.0, 1.0))


def mesh_world_bounds(mesh: bpy.types.Object) -> tuple[Vector, Vector]:
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for corner in evaluated.bound_box:
        point = evaluated.matrix_world @ Vector(corner)
        minimum.x = min(minimum.x, point.x)
        minimum.y = min(minimum.y, point.y)
        minimum.z = min(minimum.z, point.z)
        maximum.x = max(maximum.x, point.x)
        maximum.y = max(maximum.y, point.y)
        maximum.z = max(maximum.z, point.z)
    return minimum, maximum


def import_animation_library(path: Path, label: str) -> bpy.types.Object:
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    armatures = [obj for obj in imported_objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one {label} armature, found {len(armatures)}")
    source = armatures[0]
    source.name = f"{label}_RetargetSource"
    source["retarget_library"] = label
    for action in bpy.data.actions:
        if action not in before_actions:
            action["retarget_library"] = label
    for obj in imported_objects:
        obj.hide_render = True
        obj.hide_viewport = True
    source.hide_viewport = False
    return source


def find_source_action(label: str, name: str) -> bpy.types.Action:
    matches = [
        action for action in bpy.data.actions
        if action.name == name and action.get("retarget_library") == label
    ]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one {label}:{name} action, found {len(matches)}")
    return matches[0]


def target_height(armature: bpy.types.Object) -> float:
    foot = armature.data.bones["mixamorig:LeftFoot"]
    head = armature.data.bones["mixamorig:Head"]
    return (armature.matrix_world @ head.tail_local).z - (armature.matrix_world @ foot.head_local).z


def source_height(armature: bpy.types.Object) -> float:
    foot = armature.data.bones["foot_l"]
    head = armature.data.bones["Head"]
    return (armature.matrix_world @ head.tail_local).z - (armature.matrix_world @ foot.head_local).z


def retarget_action(
    target: bpy.types.Object,
    source: bpy.types.Object,
    source_action: bpy.types.Action,
    output_name: str,
) -> bpy.types.Action:
    scene = bpy.context.scene
    source.animation_data_create()
    source.animation_data.action = source_action
    target.animation_data_create()
    action = bpy.data.actions.new(output_name)
    target.animation_data.action = action
    scale = target_height(target) / source_height(source)
    start = int(math.floor(source_action.frame_range[0]))
    end = int(math.ceil(source_action.frame_range[1]))
    target_rest_world = {
        name: target.matrix_world @ target.data.bones[name].matrix_local
        for name in BONE_MAP.values()
    }
    target_rest_directions = {
        name: (
            target.matrix_world @ target.data.bones[name].tail_local
            - target.matrix_world @ target.data.bones[name].head_local
        ).normalized()
        for name in BONE_MAP.values()
    }
    target_world_inverse = target.matrix_world.inverted()

    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        desired_rotations: dict[str, Quaternion] = {}
        for source_name, target_name in BONE_MAP.items():
            source_pose_bone = source.pose.bones[source_name]
            source_pose_direction = (
                source.matrix_world @ source_pose_bone.tail
                - source.matrix_world @ source_pose_bone.head
            ).normalized()
            direction_alignment = target_rest_directions[target_name].rotation_difference(
                source_pose_direction
            )
            desired_rotations[target_name] = (
                direction_alignment @ rotation_only(target_rest_world[target_name])
            ).normalized()

        for source_name, target_name in BONE_MAP.items():
            pose_bone = target.pose.bones[target_name]
            target_bone = target.data.bones[target_name]
            if target_bone.parent is None or target_bone.parent.name not in desired_rotations:
                source_pose_head_world = source.matrix_world @ source.pose.bones[source_name].head
                source_rest_head_world = source.matrix_world @ source.data.bones[source_name].head_local
                translation_world = source_pose_head_world - source_rest_head_world
                desired_head_world = target.matrix_world @ target_bone.head_local + translation_world * scale
                desired_head_local = target_world_inverse @ desired_head_world
            else:
                parent_pose = target.pose.bones[target_bone.parent.name].matrix
                relative_rest = target_bone.parent.matrix_local.inverted() @ target_bone.matrix_local
                desired_head_local = (parent_pose @ relative_rest).translation

            desired_rotation_local = (
                rotation_only(target.matrix_world).inverted() @ desired_rotations[target_name]
            ).normalized()
            pose_bone.matrix = matrix_from_rotation_translation(desired_rotation_local, desired_head_local)
            pose_bone.keyframe_insert(data_path="location", frame=frame, group=target_name)
            pose_bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=target_name)

    action.name = output_name
    action["loop"] = output_name in LOOP_ACTIONS
    reset_pose(target)
    return action


def ground_action(
    armature: bpy.types.Object,
    mesh: bpy.types.Object,
    action: bpy.types.Action,
    clearance: float = 0.025,
) -> None:
    armature.animation_data.action = action
    start = int(math.floor(action.frame_range[0]))
    end = int(math.ceil(action.frame_range[1]))
    for frame in range(start, end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        minimum, _ = mesh_world_bounds(mesh)
        world_shift = Vector((0.0, 0.0, clearance - minimum.z))
        armature_shift = armature.matrix_world.inverted().to_3x3() @ world_shift
        hips = armature.pose.bones["mixamorig:Hips"]
        hips_matrix = hips.matrix.copy()
        hips_matrix.translation += armature_shift
        hips.matrix = hips_matrix
        hips.keyframe_insert(data_path="location", frame=frame, group=hips.name)

def author_downed_hold(armature: bpy.types.Object, death: bpy.types.Action) -> None:
    armature.animation_data.action = death
    bpy.context.scene.frame_set(int(math.ceil(death.frame_range[1])))
    bpy.context.view_layer.update()


FINGER_LAYOUT = {
    "Thumb": (0.030, 0.018),
    "Index": (0.016, 0.012),
    "Middle": (0.000, 0.008),
    "Ring": (-0.016, 0.004),
    "Pinky": (-0.031, 0.000),
}


def author_finger_rig(armature: bpy.types.Object, meshes: list[bpy.types.Object]) -> None:
    """Add usable phalanges to mitten-style presets and curl them around weapons.

    The original Quaternius female meshes expose only a single hand joint. The
    small authored chains below preserve that licensed mesh while splitting
    the hand influence into five bendable contact bands. Existing Mixamo hands
    already contain these chains, so the operation is idempotent for BAMEN.
    """
    required = "mixamorig:LeftHandThumb1"
    if armature.data.bones.get(required) is None:
        bpy.ops.object.mode_set(mode="EDIT")
        for side_name, side in (("Left", 1.0), ("Right", -1.0)):
            hand_name = f"mixamorig:{side_name}Hand"
            hand = armature.data.edit_bones.get(hand_name)
            if hand is None:
                continue
            for finger, (y_offset, z_offset) in FINGER_LAYOUT.items():
                parent = hand
                for segment in range(1, 4):
                    name = f"mixamorig:{side_name}Hand{finger}{segment}"
                    if armature.data.edit_bones.get(name) is not None:
                        parent = armature.data.edit_bones[name]
                        continue
                    head = hand.tail.copy() if segment == 1 else parent.tail.copy()
                    head += Vector((side * 0.008, y_offset, z_offset - (segment - 1) * 0.004))
                    tail = head + Vector((side * (0.030 if finger == "Thumb" else 0.026), 0.0, -0.004))
                    bone = armature.data.edit_bones.new(name)
                    bone.head = head
                    bone.tail = tail
                    bone.parent = parent
                    bone.use_connect = segment > 1
                    parent = bone
        bpy.ops.object.mode_set(mode="OBJECT")

    for mesh in meshes:
        if mesh.type != "MESH" or len(mesh.vertex_groups) == 0:
            continue
        inverse = (armature.matrix_world.inverted() @ mesh.matrix_world)
        for side_name, side in (("Left", 1.0), ("Right", -1.0)):
            hand_name = f"mixamorig:{side_name}Hand"
            hand = armature.data.bones.get(hand_name)
            if hand is None:
                continue
            hand_x = hand.head_local.x
            for finger, (y_offset, z_offset) in FINGER_LAYOUT.items():
                for segment in range(1, 4):
                    bone_name = f"mixamorig:{side_name}Hand{finger}{segment}"
                    bone = armature.data.bones.get(bone_name)
                    if bone is None or mesh.vertex_groups.get(bone_name) is not None:
                        continue
                    group = mesh.vertex_groups.new(name=bone_name)
                    center = (bone.head_local + bone.tail_local) * 0.5
                    for vertex in mesh.data.vertices:
                        point = inverse @ vertex.co
                        if side * (point.x - hand_x) < 0.0:
                            continue
                        distance = point.distance_to(center)
                        if distance > 0.040:
                            continue
                        weight = max(0.0, min(0.72, 0.72 * (1.0 - distance / 0.040)))
                        if weight <= 0.01:
                            continue
                        for existing in mesh.vertex_groups:
                            if existing.index == group.index:
                                continue
                            try:
                                current = existing.weight(vertex.index)
                            except RuntimeError:
                                continue
                            if current > 0.0:
                                existing.add([vertex.index], current * (1.0 - weight), "REPLACE")
                        group.add([vertex.index], weight, "REPLACE")


def author_finger_animation(armature: bpy.types.Object, actions: dict[str, bpy.types.Action]) -> None:
    """Key curl at the actual hand joints for each gameplay action."""
    for action_name, action in actions.items():
        armature.animation_data_create()
        armature.animation_data.action = action
        start, end = [int(round(value)) for value in action.frame_range]
        frames = sorted({start, start + max(1, (end - start) // 2), end})
        armed = action_name.startswith(("ready_", "aim_")) or action_name in {
            "shoot", "reload", "jump_start", "jump_loop", "jump_land", "slide_start", "slide_loop", "slide_exit",
        }
        curl = math.radians(58.0 if armed else 18.0)
        if action_name in {"throw", "melee"}:
            curl = math.radians(28.0)
        for frame in frames:
            bpy.context.scene.frame_set(frame)
            for side_name, side in (("Left", 1.0), ("Right", -1.0)):
                for finger in FINGER_LAYOUT:
                    for segment in range(1, 4):
                        bone = armature.pose.bones.get(f"mixamorig:{side_name}Hand{finger}{segment}")
                        if bone is None:
                            continue
                        bone.rotation_mode = "XYZ"
                        bone.rotation_euler.z = -side * curl * (1.0 if segment == 1 else 0.82)
                        bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone.name)
        action["finger_rig"] = True
    armature.animation_data.action = None
    pose = {
        bone.name: (bone.location.copy(), bone.rotation_quaternion.copy())
        for bone in armature.pose.bones
    }
    action = bpy.data.actions.new("downed")
    armature.animation_data.action = action
    for frame in (0, 30):
        for name, (location, rotation) in pose.items():
            bone = armature.pose.bones[name]
            bone.location = location
            bone.rotation_mode = "QUATERNION"
            bone.rotation_quaternion = rotation
            bone.keyframe_insert(data_path="location", frame=frame, group=name)
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=name)
    action["loop"] = True


def author_pose_hold(
    armature: bpy.types.Object,
    source: bpy.types.Action,
    source_frame: int,
    output_name: str,
) -> bpy.types.Action:
    armature.animation_data.action = source
    bpy.context.scene.frame_set(source_frame)
    bpy.context.view_layer.update()
    pose = {
        bone.name: (bone.location.copy(), bone.rotation_quaternion.copy())
        for bone in armature.pose.bones
    }
    action = bpy.data.actions.new(output_name)
    armature.animation_data.action = action
    for frame in (0, 30):
        for name, (location, rotation) in pose.items():
            bone = armature.pose.bones[name]
            bone.location = location
            bone.rotation_mode = "QUATERNION"
            bone.rotation_quaternion = rotation
            bone.keyframe_insert(data_path="location", frame=frame, group=name)
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=name)
    action["loop"] = True
    return action


def author_rifle_hold(
    armature: bpy.types.Object,
    source: bpy.types.Action,
    output_name: str,
    rifle_origin: Vector,
    rifle_forward: Vector,
    rifle_up: Vector,
    neck_direction: Vector,
    head_direction: Vector,
    support_hand_offset: Vector | None = None,
    right_hand_world_rotation: Quaternion | None = None,
    right_elbow_pole_offset: Vector | None = None,
    right_elbow_pole_angle_degrees: float = 0.0,
    remove_source: bool = False,
) -> bpy.types.Action:
    armature.animation_data.action = source
    bpy.context.scene.frame_set(int(math.floor(source.frame_range[0])))
    bpy.context.view_layer.update()
    before_objects = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(M4_GLB))
    imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    rifle = next(obj for obj in imported_objects if obj.name == "SteelTideM4A1")
    rifle_matrix = rifle_world_rotation(rifle_forward, rifle_up).to_matrix().to_4x4()
    rifle_matrix.translation = rifle_origin
    rifle.matrix_world = rifle_matrix @ Matrix.Diagonal((RIFLE_SCALE, RIFLE_SCALE, RIFLE_SCALE, 1.0))
    bpy.context.view_layer.update()
    foregrip = next(obj for obj in imported_objects if obj.name == "Foregrip")
    # Center the support wrist over the angled foregrip. The previous target sat
    # on the receiver-facing side, so the palm could never visibly wrap the grip.
    left_wrist = foregrip.matrix_world @ Vector((0.0, -0.02, -0.11))
    if support_hand_offset is not None:
        left_wrist += support_hand_offset
    constraints: list[tuple[bpy.types.PoseBone, bpy.types.Constraint]] = []
    targets: list[bpy.types.Object] = []
    hand_targets = {
        "mixamorig:RightForeArm": rifle_origin,
        "mixamorig:LeftForeArm": left_wrist,
    }
    for bone_name, location in hand_targets.items():
        target = bpy.data.objects.new(f"{bone_name}_RifleAimTarget", None)
        bpy.context.collection.objects.link(target)
        target.location = location
        constraint = armature.pose.bones[bone_name].constraints.new("IK")
        constraint.target = target
        constraint.chain_count = 2
        constraint.use_tail = True
        if bone_name == "mixamorig:RightForeArm" and right_elbow_pole_offset is not None:
            pole = bpy.data.objects.new("mixamorig:RightForeArm_RifleAimPole", None)
            bpy.context.collection.objects.link(pole)
            right_shoulder = armature.pose.bones["mixamorig:RightArm"]
            pole.location = (
                armature.matrix_world @ right_shoulder.head
                + armature.matrix_world.to_3x3() @ right_elbow_pole_offset
            )
            constraint.pole_target = pole
            constraint.pole_angle = math.radians(right_elbow_pole_angle_degrees)
            targets.append(pole)
        constraints.append((armature.pose.bones[bone_name], constraint))
        targets.append(target)
    bpy.context.view_layer.update()

    hand_pose_rotation = RIFLE_AIM_FORWARD.rotation_difference(rifle_forward)
    hand_directions = {
        "mixamorig:RightHand": hand_pose_rotation
        @ Vector((0.0, 0.12, -0.993)).normalized(),
        "mixamorig:LeftHand": hand_pose_rotation
        @ Vector((0.02, 0.05, 0.998)).normalized(),
    }
    for bone_name, desired_direction in hand_directions.items():
        if bone_name == "mixamorig:RightHand" and right_hand_world_rotation is not None:
            set_pose_bone_world_rotation(armature, bone_name, right_hand_world_rotation)
            continue
        align_pose_bone_world_direction(armature, bone_name, desired_direction)
    bpy.context.view_layer.update()
    left_hand = armature.pose.bones["mixamorig:LeftHand"]
    left_hand.matrix = left_hand.matrix @ Matrix.Rotation(math.radians(-90.0), 4, "Y")
    bpy.context.view_layer.update()
    for bone_name, desired_direction in {
        "mixamorig:Neck": neck_direction,
        "mixamorig:Head": head_direction,
    }.items():
        align_pose_bone_world_direction(armature, bone_name, desired_direction)
        bpy.context.view_layer.update()

    finger_curls = {
        "Thumb": (28.0, 36.0, 38.0, 34.0),
        "Index": (32.0, 46.0, 52.0, 42.0),
        "Middle": (54.0, 64.0, 68.0, 48.0),
        "Ring": (58.0, 68.0, 72.0, 52.0),
        "Pinky": (62.0, 72.0, 74.0, 54.0),
    }
    for side in ("Left", "Right"):
        for finger, angles in finger_curls.items():
            for segment, angle in enumerate(angles, start=1):
                bone = armature.pose.bones.get(f"mixamorig:{side}Hand{finger}{segment}")
                if bone is None:
                    continue
                bone.rotation_mode = "XYZ"
                if side == "Left":
                    bone.rotation_euler.z = math.radians(-angle)
                else:
                    bone.rotation_euler.x = math.radians(-angle)
    bpy.context.view_layer.update()
    pose_matrices = {
        bone.name: bone.matrix.copy()
        for bone in armature.pose.bones
    }
    for bone, constraint in constraints:
        bone.constraints.remove(constraint)
    for target in targets:
        bpy.data.objects.remove(target, do_unlink=True)
    for obj in reversed(imported_objects):
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    if source.name == output_name:
        source.name = f"{output_name}_source"
    action = bpy.data.actions.new(output_name)
    armature.animation_data.action = action
    reset_pose(armature)
    for frame in (0, 30):
        for bone in armature.pose.bones:
            bone.matrix = pose_matrices[bone.name]
            bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=bone.name)
    action["loop"] = True
    armature.animation_data.action = None
    if remove_source:
        bpy.data.actions.remove(source)
    return action


def author_upper_body_locomotion(
    armature: bpy.types.Object,
    locomotion: bpy.types.Action,
    upper_body_hold: bpy.types.Action,
    output_name: str,
) -> bpy.types.Action:
    armature.animation_data.action = upper_body_hold
    bpy.context.scene.frame_set(int(math.floor(upper_body_hold.frame_range[0])))
    bpy.context.view_layer.update()
    upper_root = armature.pose.bones["mixamorig:Spine"]
    upper_bones = [
        bone for bone in armature.pose.bones
        if bone == upper_root or upper_root in bone.parent_recursive
    ]
    hierarchy = sorted(
        armature.pose.bones,
        key=lambda bone: len(bone.parent_recursive),
    )
    upper_bone_names = {bone.name for bone in upper_bones}
    upper_body_local_matrices = {
        bone.name: local_pose_matrix(bone)
        for bone in upper_bones
    }

    action = bpy.data.actions.new(output_name)
    start = int(math.floor(locomotion.frame_range[0]))
    end = int(math.ceil(locomotion.frame_range[1]))
    for frame in range(start, end + 1):
        armature.animation_data.action = locomotion
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        locomotion_local_matrices = {
            bone.name: local_pose_matrix(bone)
            for bone in armature.pose.bones
        }

        armature.animation_data.action = action
        reset_pose(armature)
        bpy.context.view_layer.update()
        for bone in hierarchy:
            local_matrix = (
                upper_body_local_matrices[bone.name]
                if bone.name in upper_bone_names
                else locomotion_local_matrices[bone.name]
            )
            bone.matrix = (
                bone.parent.matrix @ local_matrix
                if bone.parent is not None
                else local_matrix
            )
        bpy.context.view_layer.update()
        for bone in armature.pose.bones:
            bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=bone.name)

    action["loop"] = True
    reset_pose(armature)
    return action


def local_pose_matrix(bone: bpy.types.PoseBone) -> Matrix:
    return (
        bone.parent.matrix.inverted() @ bone.matrix
        if bone.parent is not None
        else bone.matrix.copy()
    )


def add_socket(
    armature: bpy.types.Object,
    name: str,
    bone_name: str,
    world_location: tuple[float, float, float] | None = None,
    world_rotation_degrees: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> None:
    socket = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(socket)
    socket.parent = armature
    socket.parent_type = "BONE"
    socket.parent_bone = bone_name
    socket.matrix_parent_inverse = Matrix.Identity(4)
    if world_location is None:
        world_location = tuple(armature.matrix_world @ armature.data.bones[bone_name].head_local)
    rotation = Euler(tuple(math.radians(value) for value in world_rotation_degrees)).to_matrix().to_4x4()
    rotation.translation = Vector(world_location)
    socket.matrix_world = rotation
    socket.empty_display_type = "PLAIN_AXES"
    socket.empty_display_size = 8.0


def cleanup_sources(sources: list[bpy.types.Object]) -> None:
    source_roots = set()
    for source in sources:
        root = source
        while root.parent is not None:
            root = root.parent
        source_roots.add(root)
    for root in source_roots:
        descendants = [root] + list(root.children_recursive)
        for obj in reversed(descendants):
            if obj.name in bpy.data.objects:
                bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        if action.get("retarget_library"):
            bpy.data.actions.remove(action)


def set_action_export_metadata() -> None:
    for action in bpy.data.actions:
        action.asset_mark()
        action.use_fake_user = True
        if action.name in LOOP_ACTIONS:
            action["loop"] = True


def export_asset(armature: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root = armature.parent
    root.select_set(True)
    armature.select_set(True)
    for child in armature.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=False,
        export_optimize_animation_size=True,
        export_force_sampling=True,
        export_frame_range=False,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


def main() -> None:
    for path in (SOURCE_BLEND, UAL1_GLB, UAL2_GLB, M4_GLB):
        require_file(path)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    target = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    target_mesh = next(obj for obj in scene.objects if obj.type == "MESH")
    target.data.pose_position = "POSE"
    target.animation_data_create()
    target.animation_data.action = None
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    reset_pose(target)
    # Keep the licensed Quaternius hand chains when present and author the
    # same five-finger fallback for older wrist-only source blends.
    author_finger_rig(target, [target_mesh])

    ual1 = import_animation_library(UAL1_GLB, "ual1")
    ual2 = import_animation_library(UAL2_GLB, "ual2")
    sources = {"ual1": ual1, "ual2": ual2}
    generated: dict[str, bpy.types.Action] = {}
    for output_name, (library, source_name, _) in ACTION_SOURCES.items():
        generated[output_name] = retarget_action(
            target,
            sources[library],
            find_source_action(library, source_name),
            output_name,
        )
    generated["aim_idle"] = author_rifle_hold(
        target,
        generated["aim_idle"],
        "aim_idle",
        RIFLE_AIM_ORIGIN,
        RIFLE_AIM_FORWARD,
        RIFLE_AIM_UP,
        Vector((0.0, -0.50, 0.866)),
        Vector((0.0, -0.62, 0.785)),
        remove_source=True,
    )
    target.animation_data.action = generated["aim_idle"]
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    aim_hand_world_rotation = rotation_only(
        target.matrix_world @ target.pose.bones["mixamorig:RightHand"].matrix
    )
    ready_hand_world_rotation = (
        rifle_world_rotation(RIFLE_READY_FORWARD, RIFLE_READY_UP)
        @ rifle_world_rotation(RIFLE_AIM_FORWARD, RIFLE_AIM_UP).inverted()
        @ aim_hand_world_rotation
    ).normalized()
    generated["ready_idle"] = author_rifle_hold(
        target,
        generated["idle"],
        "ready_idle",
        RIFLE_READY_ORIGIN,
        RIFLE_READY_FORWARD,
        RIFLE_READY_UP,
        Vector((0.0, -0.12, 0.993)).normalized(),
        Vector((0.0, -0.18, 0.984)).normalized(),
        right_hand_world_rotation=ready_hand_world_rotation,
    )
    for output_name, source_name in READY_LOCOMOTION_SOURCES.items():
        generated[output_name] = author_upper_body_locomotion(
            target,
            generated[source_name],
            generated["ready_idle"],
            output_name,
        )
    for output_name, source_name in AIM_LOCOMOTION_SOURCES.items():
        generated[output_name] = author_upper_body_locomotion(
            target,
            generated[source_name],
            generated["aim_idle"],
            output_name,
        )
    ground_action(target, target_mesh, generated["prone_idle"])
    ground_action(target, target_mesh, generated["prone_crawl"])
    stale_prone_idle = generated["prone_idle"]
    target.animation_data.action = None
    bpy.data.actions.remove(stale_prone_idle)
    generated["prone_idle"] = author_pose_hold(
        target,
        generated["prone_crawl"],
        12,
        "prone_idle",
    )
    author_downed_hold(target, generated["death"])
    finger_actions = dict(generated)
    downed_action = bpy.data.actions.get("downed")
    if downed_action is not None:
        finger_actions["downed"] = downed_action
    author_finger_animation(target, finger_actions)
    reset_pose(target)
    cleanup_sources([ual1, ual2])
    right_hand = tuple(target.matrix_world @ target.data.bones["mixamorig:RightHand"].head_local)
    add_socket(
        target,
        "WeaponSocket",
        "mixamorig:RightHand",
        world_location=right_hand,
        world_rotation_degrees=(0.0, 0.0, 180.0),
    )
    add_socket(
        target,
        "BackWeaponSocket",
        "mixamorig:Spine2",
        world_location=(0.22, 0.14, 1.28),
        world_rotation_degrees=(90.0, 0.0, -8.0),
    )
    add_socket(target, "HeadSocket", "mixamorig:Head")
    add_socket(target, "VestSocket", "mixamorig:Spine2")
    add_socket(target, "BackpackSocket", "mixamorig:Spine2")
    add_socket(target, "TeamPatchSocket", "mixamorig:LeftShoulder")
    set_action_export_metadata()
    save_source()
    export_asset(target)
    print(
        "BAMEN_ANIMATED_EXPORT "
        f"glb={OUTPUT_GLB} actions={sorted(action.name for action in bpy.data.actions)}"
    )


if __name__ == "__main__":
    main()
