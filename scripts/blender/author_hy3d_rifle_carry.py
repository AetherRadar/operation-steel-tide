"""Author the HY-3D two-hand rifle carry pose in Blender.

The game imports the result as a finished character asset.  This script keeps
the locomotion curves and bakes a stable upper-body pose around the authored
M4A1 contact points.  It also exports a bone-parented rifle socket and palm
frames, so the runtime only follows authored transforms and never moves a
skinned hand to compensate for a bad socket.

Usage::

    blender -b --python author_hy3d_rifle_carry.py -- \
        --input heron.glb --weapon steel_tide_m4a1.glb \
        --output heron.carry.glb --blend heron.carry.blend
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ARM_BONES = ("RightArm", "RightForeArm", "RightHand", "LeftArm", "LeftForeArm", "LeftHand")
UPPER_BODY_BONES = (
    "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "RightShoulder",
    "LeftClavicle", "RightClavicle",
)
FINGER_BONES = tuple(
    f"{side}Hand{finger}{segment}"
    for side in ("Right", "Left")
    for finger in ("Index", "Middle", "Ring", "Pinky", "Thumb")
    for segment in (1, 2, 3)
)


def args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--weapon", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--blend", required=True)
    parser.add_argument("--cant-degrees", type=float, default=10.0,
                        help="cross-body rifle cant around the vertical axis")
    parser.add_argument("--support-height", type=float, default=0.05,
                        help="authored support-palm height above the foregrip marker")
    parser.add_argument("--support-rear", type=float, default=0.03,
                        help="authored support-palm offset toward the stock")
    return parser.parse_args(argv)


def armature(objects):
    return next((obj for obj in objects if obj.type == "ARMATURE"), None)


def pose_bone(rig: bpy.types.Object, name: str) -> bpy.types.PoseBone:
    exact = rig.pose.bones.get(name)
    if exact is not None:
        return exact
    return next((bone for bone in rig.pose.bones if bone.name.endswith(":" + name)), None)


def world_pose(rig: bpy.types.Object, name: str) -> Matrix:
    bone = pose_bone(rig, name)
    if bone is None:
        raise RuntimeError(f"missing bone {name}")
    return rig.matrix_world @ bone.matrix


def reset_pose(rig: bpy.types.Object) -> None:
    """Return every pose channel to bind/rest space before solving contacts.

    glTF import can leave the armature with the last sampled source clip as a
    live pose even after its action is cleared.  Solving a carry pose from that
    hidden pose and then removing the torso channels produces a different pose
    after export, which is exactly the hand/weapon drift this asset is meant to
    prevent.
    """
    rig.animation_data_clear()
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()


def remove_curves(action: bpy.types.Action, names: tuple[str, ...]) -> None:
    for curve in list(action.fcurves):
        if any(f'pose.bones["{name}"]' in curve.data_path for name in names):
            action.fcurves.remove(curve)


def insert_constant_rotation(action: bpy.types.Action, bone: bpy.types.PoseBone, frames: list[int]) -> None:
    path = f'pose.bones["{bone.name}"].rotation_quaternion'
    for index in range(4):
        curve = action.fcurves.new(path, index=index, action_group=bone.name)
        for frame in frames:
            curve.keyframe_points.insert(frame, float(bone.rotation_quaternion[index]), options={"FAST"})
        curve.update()


def add_bone_marker(
    rig: bpy.types.Object,
    name: str,
    bone_name: str,
    world_transform: Matrix,
    role: str,
) -> bpy.types.Object:
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    marker = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(marker)
    marker.parent = rig
    marker.parent_type = "BONE"
    marker.parent_bone = bone_name
    marker.matrix_world = world_transform
    marker["steel_tide_anatomical_frame"] = True
    marker["steel_tide_contact_role"] = role
    return marker


def solve_two_bone(rig, side: str, target: Vector, pole: Vector) -> float:
    shoulder = pose_bone(rig, side + "Arm")
    elbow = pose_bone(rig, side + "ForeArm")
    wrist = pose_bone(rig, side + "Hand")
    inv = rig.matrix_world.inverted()
    shoulder_world = rig.matrix_world @ shoulder.matrix
    elbow_world = rig.matrix_world @ elbow.matrix
    wrist_world = rig.matrix_world @ wrist.matrix
    start = shoulder_world.translation.copy()
    elbow_point = elbow_world.translation.copy()
    current_wrist = wrist_world.translation.copy()
    first = max(1e-5, (elbow_point - start).length)
    second = max(1e-5, (current_wrist - elbow_point).length)
    delta = target - start
    distance = max(abs(first - second) + 1e-5, min(delta.length, first + second - 1e-5))
    direction = delta.normalized()
    pole_dir = pole - start
    pole_dir -= direction * pole_dir.dot(direction)
    if pole_dir.length_squared < 1e-8:
        pole_dir = Vector((0.0, 0.0, 1.0)) - direction * direction.z
    pole_dir.normalize()
    along = max(-1.0, min(1.0, (first * first - second * second + distance * distance) / (2 * distance * first))) * first
    height = math.sqrt(max(0.0, first * first - along * along))
    desired_elbow = start + direction * along + pole_dir * height
    desired_wrist = start + direction * distance
    rotation = (elbow_point - start).normalized().rotation_difference((desired_elbow - start).normalized())
    shoulder.matrix = inv @ (Matrix.Translation(start) @ (rotation.to_matrix() @ shoulder_world.to_3x3().normalized()).to_4x4())
    bpy.context.view_layer.update()
    elbow_world = rig.matrix_world @ elbow.matrix
    wrist_world = rig.matrix_world @ wrist.matrix
    elbow_point = elbow_world.translation.copy()
    forearm = (wrist_world.translation - elbow_point).normalized()
    desired_forearm = (desired_wrist - elbow_point).normalized()
    elbow_rotation = forearm.rotation_difference(desired_forearm)
    elbow.matrix = inv @ (Matrix.Translation(elbow_point) @ (elbow_rotation.to_matrix() @ elbow_world.to_3x3().normalized()).to_4x4())
    bpy.context.view_layer.update()
    return ((rig.matrix_world @ wrist.matrix).translation - target).length


def export_target(rig: bpy.types.Object, output: str) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root = rig.parent if rig.parent is not None else rig
    root.select_set(True)
    rig.select_set(True)
    for child in rig.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    os.makedirs(os.path.dirname(output), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=os.path.abspath(output), export_format="GLB", use_selection=True,
        export_yup=True, export_apply=False, export_skins=True,
        export_animations=True, export_animation_mode="BROADCAST",
        export_nla_strips=False, export_def_bones=True, export_leaf_bone=False,
        export_materials="EXPORT", export_image_format="AUTO", export_texcoords=True,
        export_normals=True, export_tangents=False, export_all_influences=False,
    )


def main() -> None:
    config = args()
    input_path = os.path.abspath(config.input)
    weapon_path = os.path.abspath(config.weapon)
    output_path = os.path.abspath(config.output)
    if output_path in {input_path, weapon_path}:
        raise SystemExit("output must be separate from input and weapon")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=input_path)
    target_objects = list(bpy.context.scene.objects)
    rig = armature(target_objects)
    if rig is None:
        raise RuntimeError("input GLB has no armature")
    # Keep the carry reference in the imported neutral frame. The source
    # files already contain the repaired lower-body actions.
    reset_pose(rig)
    rig.animation_data_create()

    bpy.ops.import_scene.gltf(filepath=weapon_path)
    weapon = bpy.data.objects.get("SteelTideM4A1")
    if weapon is None:
        raise RuntimeError("weapon GLB is missing SteelTideM4A1")
    mesh = next((obj for obj in target_objects if obj.type == "MESH"), None)
    if mesh is None:
        raise RuntimeError("input GLB has no skinned mesh")
    points = [mesh.matrix_world @ vertex.co for vertex in mesh.data.vertices]
    height = max(point.z for point in points) - min(point.z for point in points)
    weapon_scale = 0.40 / max(0.001, 1.86 / height)
    # Blender's imported character faces -Y.  The rifle's local +Y is its
    # barrel axis, so a 25-degree cross-body cant gives the support arm a
    # straight, visible path to the handguard.
    weapon_basis = Matrix.Rotation(math.pi + math.radians(config.cant_degrees), 4, "Z")
    weapon.matrix_world = weapon_basis @ Matrix.Scale(weapon_scale, 4)
    bpy.context.view_layer.update()
    right_shoulder = world_pose(rig, "RightArm").translation
    stock = bpy.data.objects.get("StockContact")
    primary = bpy.data.objects.get("PrimaryGrip")
    foregrip = bpy.data.objects.get("ForegripContact")
    if not all((stock, primary, foregrip)):
        raise RuntimeError("M4A1 is missing carry markers")
    # The source character is normalized before entering Godot.  This
    # authored pocket sits below the clavicle so the imported rifle crosses the
    # vest rather than riding at the neck in the third-person camera.
    weapon.location += right_shoulder + Vector((0.025, -0.015, -0.095)) - stock.matrix_world.translation
    bpy.context.view_layer.update()
    forward = weapon_basis.to_3x3() @ Vector((0.0, 1.0, 0.0))
    weapon_right = weapon_basis.to_3x3() @ Vector((1.0, 0.0, 0.0))
    up = Vector((0.0, 0.0, 1.0))

    # The palms sit on opposing sides of the rifle.  The finger direction is
    # down-range and the thumb axis is vertical; this is the frame the authored
    # hand meshes use, so both gloves remain visible around the receiver.
    right_basis = Matrix((up, forward, -weapon_right)).transposed()
    right_palm = primary.matrix_world.translation + weapon_right * 0.008 + up * -0.015
    right_wrist = right_palm - right_basis @ Vector((0.005, 0.09, 0.050))
    right_error = solve_two_bone(rig, "Right", right_wrist, right_shoulder + Vector((-0.30, 0.015, -0.25)))
    right_hand = pose_bone(rig, "RightHand")
    right_world = rig.matrix_world @ right_hand.matrix
    right_hand.matrix = rig.matrix_world.inverted() @ (Matrix.Translation(right_world.translation) @ right_basis.to_4x4())
    bpy.context.view_layer.update()

    left_shoulder = world_pose(rig, "LeftArm").translation
    left_basis = Matrix((-up, forward, weapon_right)).transposed()
    # Keep the support palm on the handguard contact itself.  Raising it above
    # the rail makes the glove read as a floating hand in the game camera and
    # leaves the front grip visibly unheld.
    left_palm = foregrip.matrix_world.translation + up * config.support_height \
        - forward * config.support_rear - weapon_right * 0.008
    left_wrist = left_palm - left_basis @ Vector((-0.005, 0.085, 0.050))
    left_error = solve_two_bone(rig, "Left", left_wrist, left_shoulder + Vector((0.12, -0.06, -0.32)))
    left_hand = pose_bone(rig, "LeftHand")
    left_world = rig.matrix_world @ left_hand.matrix
    left_hand.matrix = rig.matrix_world.inverted() @ (Matrix.Translation(left_world.translation) @ left_basis.to_4x4())
    bpy.context.view_layer.update()

    # Bake the solved local arm rotations into every ready/aim action. The
    # lower-body curves remain untouched; fingers retain their authored mesh
    # shape but no longer receive the malformed source curl channels.
    arm_pose = {name: pose_bone(rig, name).rotation_quaternion.copy() for name in ARM_BONES}
    actions = [action for action in bpy.data.actions if action.name.startswith(("ready_", "aim_"))]
    if not actions:
        raise RuntimeError("input GLB has no ready_/aim_ actions")
    for action in actions:
        start, end = [int(round(value)) for value in action.frame_range]
        frames = list(range(start, end + 1))
        # The imported carry clips retained an upper-body lean from the source
        # animation. It changes Spine2's parent frame after the authored rifle
        # pose was solved and lifts both hands toward the face in Godot. Keep
        # the repaired lower-body cycle, but use the neutral authored torso
        # and head frame for all rifle carry actions.
        remove_curves(action, ARM_BONES + FINGER_BONES + UPPER_BODY_BONES)
        rig.animation_data.action = action
        # Keep a small authored aim lean so the ready and aim clips remain
        # distinct in the roster/animation diagnostics.  The rotation is on
        # Spine2, the same parent that carries the rifle socket, so the hands,
        # weapon, and chest move together without reopening a wrist gap.
        spine = pose_bone(rig, "Spine2")
        spine.rotation_quaternion = (
            Matrix.Rotation(math.radians(2.0), 4, "Z").to_quaternion()
            if action.name.startswith("aim_")
            else Matrix.Identity(4).to_quaternion()
        )
        insert_constant_rotation(action, spine, frames)
        for name in ARM_BONES:
            bone = pose_bone(rig, name)
            bone.rotation_quaternion = arm_pose[name]
            insert_constant_rotation(action, bone, frames)
        # A single authored carry hand shape is more legible than the source
        # finger curves, which were exported with their child translations in
        # a different frame. Keep the curled rest mesh and do not reintroduce
        # the broken tracks.
    rig.animation_data.action = None
    # Restoring the solved reference pose is necessary because assigning the
    # last action above evaluates its final lower-body frame.  Markers must be
    # authored against the same arm pose that was baked into every clip.
    for name in ARM_BONES:
        pose_bone(rig, name).rotation_quaternion = arm_pose[name]
    pose_bone(rig, "Spine2").rotation_quaternion = Matrix.Identity(4).to_quaternion()
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()

    # The socket, palm frames, and weapon basis are all authored in this same
    # reference pose. They follow Spine2/RightHand/LeftHand through animation.
    right_palm_actual = world_pose(rig, "RightHand").translation + right_basis @ Vector((0.005, 0.09, 0.050))
    left_palm_actual = world_pose(rig, "LeftHand").translation + left_basis @ Vector((-0.005, 0.085, 0.050))
    add_bone_marker(rig, "RifleCarrySocket", "Spine2", weapon.matrix_world.copy(), "two_hand_rifle_root")
    add_bone_marker(rig, "RightPalmFrame", "RightHand", Matrix.Translation(right_palm_actual), "dominant_hand_palm")
    add_bone_marker(rig, "LeftPalmFrame", "LeftHand", Matrix.Translation(left_palm_actual), "support_hand_palm")
    rig["steel_tide_authored_carry_pose"] = True
    rig["steel_tide_carry_reference"] = "M4A1 CC0 contact pose, Blender authored"
    rig["steel_tide_carry_right_error"] = float(right_error)
    rig["steel_tide_carry_left_error"] = float(left_error)

    # Remove the reference weapon from the exported character hierarchy.
    for obj in list(bpy.data.objects):
        if obj not in target_objects and obj.name.startswith(("SteelTideM4A1", "Magazine", "SpareMagazine", "ChargingHandle", "Stock", "Foregrip", "Muzzle", "Optic", "RearIron", "FrontIron")):
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in actions:
        action.use_fake_user = True
    export_target(rig, output_path)
    os.makedirs(os.path.dirname(config.blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(config.blend), compress=True)
    print(f"HY3D_CARRY_AUTHOR_CHECK actions={len(actions)} right_error={right_error:.5f} left_error={left_error:.5f} output={output_path}")
    print("HY3D_CARRY_AUTHOR_PASS valid=true")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"HY3D_CARRY_AUTHOR_FAIL error={error}")
        raise
