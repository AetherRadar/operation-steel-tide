"""Bake source carry wrists onto a HY-3D armature with a two-bone IK solve.

The Tencent armatures and the Quaternius carry clips use different shoulder
spans.  Copying the source rotations therefore leaves the target wrists too
far apart for a rifle.  This post-process evaluates both clips frame by frame,
maps the source torso frame into the target torso frame, and solves only the
target upper-arm and forearm rotations.  Wrist and finger channels stay in the
target action, so authored hand/finger animation and skin translations survive
the bake.

The input and output are separate files.  Keep the HY-3D source private.

Usage::

    blender -b --python bake_hy3d_carry_pose.py -- \
        --input operator.rebound.glb \
        --source assets/models/quaternius_operators/viper.glb \
        --output C:/Temp/operator.carry-ik.glb

By default all ``ready_*`` and ``aim_*`` actions found in both files are
processed.  Pass ``--actions aim_idle,ready_idle`` to limit the bake.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from collections.abc import Iterable

import bpy
from mathutils import Matrix, Vector


SIDES = ("Left", "Right")
ARM_BONES = (
    "LeftArm",
    "LeftForeArm",
    "RightArm",
    "RightForeArm",
)
TORSO_BONES = ("Hips", "Head", "LeftShoulder", "RightShoulder")

# The GLB files used by the game are authored in Blender's Z-up frame.  The
# exporter converts this to Godot as ``x=x, y=z, z=-y``; consequently the
# operator's forward direction is Blender -Y.  Do not derive the carry frame
# from Head-Hips on every animation frame.  Several source clips contain a
# small head/neck roll and that basis can cross its pole, mirroring the mapped
# hands front-to-back.  Both the HY-3D and Quaternius files share this fixed
# character frame, so a canonical frame is deterministic and survives clips
# whose torso is animated.
BLENDER_CHARACTER_RIGHT = Vector((1.0, 0.0, 0.0))
BLENDER_CHARACTER_FORWARD = Vector((0.0, -1.0, 0.0))
BLENDER_CHARACTER_UP = Vector((0.0, 0.0, 1.0))


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, help="target HY-3D GLB")
    parser.add_argument("--source", required=True, help="Quaternius source GLB")
    parser.add_argument("--output", required=True, help="separate output GLB")
    parser.add_argument(
        "--actions",
        default="",
        help="comma-separated action names; default is all ready_*/aim_* clips",
    )
    parser.add_argument(
        "--pole-offset",
        type=float,
        default=0.12,
        help="fallback pole offset from the mapped source elbow (metres)",
    )
    return parser.parse_args(argv)


def import_asset(path: str) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    return [obj for obj in bpy.context.scene.objects if obj not in before]


def resolve_pose_bone(armature: bpy.types.Object, canonical: str) -> bpy.types.PoseBone | None:
    exact = armature.pose.bones.get(canonical)
    if exact is not None:
        return exact
    candidates = [
        bone
        for bone in armature.pose.bones
        if bone.name.endswith((":" + canonical, "/" + canonical, "|" + canonical))
    ]
    return min(candidates, key=lambda bone: (len(bone.name), bone.name)) if candidates else None


def resolve_data_bone(armature: bpy.types.Object, canonical: str) -> bpy.types.Bone | None:
    exact = armature.data.bones.get(canonical)
    if exact is not None:
        return exact
    candidates = [
        bone
        for bone in armature.data.bones
        if bone.name.endswith((":" + canonical, "/" + canonical, "|" + canonical))
    ]
    return min(candidates, key=lambda bone: (len(bone.name), bone.name)) if candidates else None


def armature_from(objects: Iterable[bpy.types.Object]) -> bpy.types.Object:
    armature = next((obj for obj in objects if obj.type == "ARMATURE"), None)
    if armature is None:
        raise RuntimeError("GLB has no armature")
    return armature


def action_base(name: str) -> str:
    return name.split(".", 1)[0]


def action_map(actions: Iterable[bpy.types.Action]) -> dict[str, bpy.types.Action]:
    result: dict[str, bpy.types.Action] = {}
    for action in sorted(actions, key=lambda item: item.name):
        result.setdefault(action_base(action.name), action)
    return result


def canonicalize_actions(
    actions: Iterable[bpy.types.Action], *, rename_canonical: bool
) -> set[bpy.types.Action]:
    """Collapse importer-created ``name.001`` action duplicates.

    Importing a GLB that was exported with a broadcast animation can create
    two datablocks for every clip.  Keeping both makes the next GLB export
    ambiguous: Blender may broadcast the unbaked sibling under the canonical
    name, while Godot then selects that wrong clip.  Prefer an exact canonical
    name, remove its suffixed siblings, and optionally rename a suffixed-only
    group to the canonical name.  The source set is kept separate from the
    target set, so source actions are never exported.
    """

    groups: dict[str, list[bpy.types.Action]] = {}
    for action in actions:
        groups.setdefault(action_base(action.name), []).append(action)
    kept: set[bpy.types.Action] = set()
    for base, candidates in groups.items():
        candidates.sort(key=lambda item: (item.name != base, len(item.name), item.name))
        keep = candidates[0]
        for duplicate in candidates[1:]:
            if duplicate.name in bpy.data.actions:
                bpy.data.actions.remove(duplicate)
        if rename_canonical and keep.name != base:
            keep.name = base
        kept.add(keep)
    return kept


def world_matrix(armature: bpy.types.Object, bone_name: str) -> Matrix:
    bone = resolve_pose_bone(armature, bone_name)
    if bone is None:
        raise RuntimeError(f"missing bone {bone_name}")
    return armature.matrix_world @ bone.matrix


def torso_points(armature: bpy.types.Object) -> dict[str, Vector]:
    return {
        name: world_matrix(armature, name).translation.copy()
        for name in TORSO_BONES
    }


def character_basis() -> Matrix:
    """Return the fixed, right-handed Blender character frame.

    ``forward`` is deliberately not used as the second column directly: the
    ordered columns are right/backward/up, which keeps the matrix a proper
    rotation (determinant +1).  With the canonical Blender axes this is the
    identity matrix, while spelling the frame out here documents the GLB to
    Godot convention and prevents a future sign regression.
    """

    backward = -BLENDER_CHARACTER_FORWARD
    return Matrix(
        (
            BLENDER_CHARACTER_RIGHT,
            backward,
            BLENDER_CHARACTER_UP,
        )
    ).transposed()


def pose_torso_basis(points: dict[str, Vector]) -> Matrix:
    """Build a torso frame from one pose, then keep it fixed for that clip."""

    up = (points["Head"] - points["Hips"]).normalized()
    right = (points["LeftShoulder"] - points["RightShoulder"]).normalized()
    forward = right.cross(up)
    if forward.length_squared < 1.0e-8:
        forward = BLENDER_CHARACTER_FORWARD.copy()
    forward.normalize()
    right = up.cross(forward).normalized()
    columns = (right, forward, up)

    # Keep the handedness while choosing the sign combination closest to the
    # authored world frame.  This removes the occasional front/back mirror
    # caused by a nearly-collinear Head-Hips vector without changing the
    # source character's stance orientation.
    canonical = (
        BLENDER_CHARACTER_RIGHT,
        BLENDER_CHARACTER_FORWARD,
        BLENDER_CHARACTER_UP,
    )
    best_columns = columns
    best_score = -float("inf")
    for signs in ((1.0, 1.0, 1.0), (1.0, -1.0, -1.0), (-1.0, 1.0, -1.0), (-1.0, -1.0, 1.0)):
        candidate = tuple(axis * sign for axis, sign in zip(columns, signs))
        score = sum(axis.dot(reference) for axis, reference in zip(candidate, canonical))
        if score > best_score:
            best_score = score
            best_columns = candidate
    return Matrix(best_columns).transposed()


def torso_basis(points: dict[str, Vector]) -> Matrix:
    """Compatibility wrapper for callers that need a one-shot pose frame."""

    return pose_torso_basis(points)


def torso_transform(source: bpy.types.Object, target: bpy.types.Object) -> Matrix:
    """Map a source world point into the target's current torso frame."""

    return torso_transform_points(torso_points(source), torso_points(target))


def torso_transform_points(
    source_points: dict[str, Vector],
    target_points: dict[str, Vector],
    source_basis: Matrix | None = None,
    target_basis: Matrix | None = None,
    source_height: float | None = None,
    target_height: float | None = None,
) -> Matrix:
    """Build a stable similarity mapping between source and target frames.

    ``source_basis`` and ``target_basis`` are sampled once from each clip's
    neutral frame and then reused for every sample.  Passing them avoids
    rebuilding a frame from a rolling head/neck pose.  Heights are likewise
    optional fixed reference measurements; when omitted, the current sample
    is used for backwards-compatible one-shot calls.
    """

    source_frame = source_basis if source_basis is not None else torso_basis(source_points)
    target_frame = target_basis if target_basis is not None else torso_basis(target_points)
    rotation = target_frame @ source_frame.transposed()
    source_height = source_height or (source_points["Head"] - source_points["Hips"]).length
    target_height = target_height or (target_points["Head"] - target_points["Hips"]).length
    source_height = max(1.0e-4, source_height)
    target_height = max(1.0e-4, target_height)
    scale = target_height / source_height
    result = rotation.to_4x4() @ Matrix.Diagonal((scale, scale, scale, 1.0))
    result.translation = target_points["Hips"] - rotation @ (
        source_points["Hips"] * scale
    )
    return result


def remove_rotation_curves(action: bpy.types.Action, bone_names: set[str]) -> None:
    for curve in list(action.fcurves):
        if not any(
            f'pose.bones["{name}"]' in curve.data_path for name in bone_names
        ):
            continue
        if curve.data_path.endswith("rotation_quaternion") or curve.data_path.endswith(
            "rotation_euler"
        ):
            action.fcurves.remove(curve)


def curve_for(
    action: bpy.types.Action,
    data_path: str,
    index: int,
    group_name: str,
) -> bpy.types.FCurve:
    curve = action.fcurves.new(data_path, index=index, action_group=group_name)
    return curve


def insert_quaternion(
    curves: dict[tuple[str, int], bpy.types.FCurve],
    action: bpy.types.Action,
    bone: bpy.types.PoseBone,
    frame: float,
) -> None:
    path = f'pose.bones["{bone.name}"].rotation_quaternion'
    for index, value in enumerate(bone.rotation_quaternion):
        curve = curves.get((bone.name, index))
        if curve is None:
            curve = curve_for(action, path, index, bone.name)
            curves[(bone.name, index)] = curve
        curve.keyframe_points.insert(frame, float(value), options={"FAST"})


def _orthogonal_pole(direction: Vector, candidate: Vector, fallback: Vector) -> Vector:
    pole = candidate - direction * candidate.dot(direction)
    if pole.length_squared < 1.0e-8:
        pole = fallback - direction * fallback.dot(direction)
    if pole.length_squared < 1.0e-8:
        pole = Vector((0.0, 0.0, 1.0)) - direction * direction.z
    if pole.length_squared < 1.0e-8:
        pole = Vector((1.0, 0.0, 0.0)) - direction * direction.x
    return pole.normalized()


def hand_span_metrics(armature: bpy.types.Object) -> tuple[float, float, float, float]:
    """Return span, forward, lateral and vertical components for both hands."""

    left = world_matrix(armature, "LeftHand").translation
    right = world_matrix(armature, "RightHand").translation
    delta = left - right
    return (
        delta.length,
        delta.dot(BLENDER_CHARACTER_FORWARD),
        abs(delta.x),
        abs(delta.z),
    )


def validate_hand_span(
    action_name: str,
    spans: list[float],
    forwards: list[float],
    laterals: list[float],
    verticals: list[float],
) -> None:
    """Reject a bake that puts the support hand behind or far from the rifle.

    The thresholds leave room for crouch and sprint clips while catching the
    characteristic failure mode this tool is intended to prevent: a torso
    basis sign flip that sends the support hand backward, or a scale mismatch
    that leaves the two palms too far apart for the weapon grip markers.
    """

    if not spans:
        raise RuntimeError(f"action {action_name} produced no hand samples")
    min_span = min(spans)
    max_span = max(spans)
    min_forward = min(forwards)
    dominant_failures = sum(
        1
        for forward, lateral, vertical in zip(forwards, laterals, verticals)
        if forward < 0.02 or abs(forward) + 0.01 < max(lateral, vertical)
    )
    # A valid rifle carry has a compact two-palm span and the left hand lies
    # toward Blender -Y (the GLTF/Godot forward-facing side).
    if min_span < 0.18 or max_span > 0.38:
        raise RuntimeError(
            f"action {action_name} hand span out of range "
            f"min={min_span:.4f} max={max_span:.4f}"
        )
    if dominant_failures > max(1, len(spans) // 10):
        raise RuntimeError(
            f"action {action_name} hand direction is not Blender -Y dominant "
            f"bad_samples={dominant_failures}/{len(spans)} "
            f"forward_min={min_forward:.4f}"
        )


def solve_two_bone(
    armature: bpy.types.Object,
    shoulder_name: str,
    elbow_name: str,
    wrist_name: str,
    target_wrist: Vector,
    pole_target: Vector,
) -> float:
    """Orient shoulder/upper-arm and forearm while retaining wrist channels."""

    shoulder = resolve_pose_bone(armature, shoulder_name)
    elbow = resolve_pose_bone(armature, elbow_name)
    wrist = resolve_pose_bone(armature, wrist_name)
    if shoulder is None or elbow is None or wrist is None:
        raise RuntimeError(
            f"missing arm chain {shoulder_name}/{elbow_name}/{wrist_name}"
        )
    object_inverse = armature.matrix_world.inverted()
    shoulder_world = armature.matrix_world @ shoulder.matrix
    elbow_world = armature.matrix_world @ elbow.matrix
    wrist_world = armature.matrix_world @ wrist.matrix
    start = shoulder_world.translation.copy()
    elbow_point = elbow_world.translation.copy()
    current_wrist = wrist_world.translation.copy()
    first_length = max(1.0e-5, (elbow_point - start).length)
    second_length = max(1.0e-5, (current_wrist - elbow_point).length)
    delta = target_wrist - start
    distance = max(
        abs(first_length - second_length) + 1.0e-5,
        min(delta.length, first_length + second_length - 1.0e-5),
    )
    direction = delta.normalized() if delta.length_squared > 1.0e-10 else Vector((0.0, -1.0, 0.0))
    pole = _orthogonal_pole(direction, pole_target - start, elbow_point - start)
    cosine = (first_length * first_length - second_length * second_length + distance * distance) / (
        2.0 * distance * first_length
    )
    along = max(-1.0, min(1.0, cosine)) * first_length
    height = math.sqrt(max(0.0, first_length * first_length - along * along))
    desired_elbow = start + direction * along + pole * height
    desired_wrist = start + direction * distance

    current_axis = (elbow_point - start).normalized()
    desired_axis = (desired_elbow - start).normalized()
    rotation = current_axis.rotation_difference(desired_axis)
    basis = rotation.to_matrix() @ shoulder_world.to_3x3().normalized()
    shoulder.matrix = object_inverse @ (Matrix.Translation(start) @ basis.to_4x4())
    bpy.context.view_layer.update()

    # Re-evaluate the forearm after the upper-arm rotation.  The wrist bone's
    # local rotation is untouched; only its parent chain is repositioned.
    elbow_world = armature.matrix_world @ elbow.matrix
    wrist_world = armature.matrix_world @ wrist.matrix
    elbow_point = elbow_world.translation.copy()
    forearm_axis = (wrist_world.translation - elbow_point).normalized()
    desired_forearm_axis = (desired_wrist - elbow_point).normalized()
    forearm_rotation = forearm_axis.rotation_difference(desired_forearm_axis)
    forearm_basis = forearm_rotation.to_matrix() @ elbow_world.to_3x3().normalized()
    elbow.matrix = object_inverse @ (
        Matrix.Translation(elbow_point) @ forearm_basis.to_4x4()
    )
    bpy.context.view_layer.update()
    final_wrist = (armature.matrix_world @ wrist.matrix).translation
    return (final_wrist - target_wrist).length


def bake_action(
    source: bpy.types.Object,
    target: bpy.types.Object,
    source_action: bpy.types.Action,
    target_action: bpy.types.Action,
    pole_offset: float,
) -> tuple[float, float, int]:
    """Bake one pair, returning max error, mean error, and sampled frames."""

    source.animation_data_create()
    target.animation_data_create()
    source.animation_data.action = source_action
    # Evaluate from an immutable duplicate while writing into target_action.
    evaluation_action = target_action.copy()
    evaluation_action.name = "__SteelTideCarryEval_" + target_action.name
    target.animation_data.action = evaluation_action
    carry_bones = set(ARM_BONES)
    target_start, target_end = [int(round(value)) for value in target_action.frame_range]
    source_start, source_end = [int(round(value)) for value in source_action.frame_range]
    remove_rotation_curves(target_action, carry_bones)
    curves: dict[tuple[str, int], bpy.types.FCurve] = {}
    if target_end < target_start:
        raise RuntimeError(f"empty target action {target_action.name}")

    # Capture each clip's neutral torso once.  The source clips often start
    # from a different shoulder/neck stance than the HY-3D rest skeleton, so
    # the reference frame supplies the useful orientation while the
    # canonical sign selection above prevents a head-roll mirror.  Reusing
    # these frames for the whole clip is the important part: a per-frame
    # Head-Hips basis can rotate the mapped carry pose behind the torso.
    target.animation_data.action = evaluation_action
    bpy.context.scene.frame_set(target_start)
    bpy.context.view_layer.update()
    target_reference_points = torso_points(target)
    target_reference_basis = torso_basis(target_reference_points)
    target_reference_height = (
        target_reference_points["Head"] - target_reference_points["Hips"]
    ).length
    source.animation_data.action = source_action
    bpy.context.scene.frame_set(source_start)
    bpy.context.view_layer.update()
    source_reference_points = torso_points(source)
    source_reference_basis = torso_basis(source_reference_points)
    source_reference_height = (
        source_reference_points["Head"] - source_reference_points["Hips"]
    ).length

    errors: list[float] = []
    spans: list[float] = []
    forwards: list[float] = []
    laterals: list[float] = []
    verticals: list[float] = []
    for frame in range(target_start, target_end + 1):
        source_frame = source_start
        if target_end > target_start:
            fraction = (frame - target_start) / float(target_end - target_start)
            source_frame = source_start + fraction * (source_end - source_start)
        # Blender's scene frame is integer, but source actions are sampled at
        # integer frames in the supplied Quaternius clips.  The normalized map
        # above keeps clips with a different frame range usable as well.
        target.animation_data.action = evaluation_action
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        target_points = torso_points(target)
        source.animation_data.action = source_action
        bpy.context.scene.frame_set(int(round(source_frame)))
        bpy.context.view_layer.update()
        source_points = torso_points(source)
        source_wrist_points = {
            side: world_matrix(source, side + "Hand").translation.copy()
            for side in SIDES
        }
        source_elbow_points = {
            side: world_matrix(source, side + "ForeArm").translation.copy()
            for side in SIDES
        }
        # Restore the target sample before solving and leave the source sample
        # captured above; both armatures share Blender's scene frame.
        target.animation_data.action = evaluation_action
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        transform = torso_transform_points(
            source_points,
            target_points,
            source_basis=source_reference_basis,
            target_basis=target_reference_basis,
            source_height=source_reference_height,
            target_height=target_reference_height,
        )
        for side in SIDES:
            source_wrist = source_wrist_points[side]
            source_elbow = source_elbow_points[side]
            desired_wrist = transform @ source_wrist
            desired_pole = transform @ source_elbow
            # A tiny lateral fallback avoids a straight-arm pole flip when the
            # source clip places elbow, shoulder and wrist on one line.
            if (desired_pole - desired_wrist).length_squared < 1.0e-8:
                desired_pole += Vector((pole_offset if side == "Right" else -pole_offset, 0.0, 0.0))
            error = solve_two_bone(
                target,
                side + "Arm",
                side + "ForeArm",
                side + "Hand",
                desired_wrist,
                desired_pole,
            )
            errors.append(error)
        span, forward, lateral, vertical = hand_span_metrics(target)
        spans.append(span)
        forwards.append(forward)
        laterals.append(lateral)
        verticals.append(vertical)
        for name in ARM_BONES:
            bone = resolve_pose_bone(target, name)
            if bone is not None:
                bone.rotation_mode = "QUATERNION"
                insert_quaternion(curves, target_action, bone, frame)
    for curve in curves.values():
        curve.update()
        for point in curve.keyframe_points:
            point.interpolation = "LINEAR"
    validate_hand_span(target_action.name, spans, forwards, laterals, verticals)
    target_action["steel_tide_carry_ik_baked"] = True
    target_action["steel_tide_carry_ik_frames"] = len(range(target_start, target_end + 1))
    target_action["steel_tide_carry_ik_max_error"] = max(errors) if errors else 0.0
    target_action["steel_tide_carry_ik_mean_error"] = (
        sum(errors) / len(errors) if errors else 0.0
    )
    target_action["steel_tide_carry_hand_span_min"] = min(spans)
    target_action["steel_tide_carry_hand_span_max"] = max(spans)
    target_action["steel_tide_carry_hand_forward_min"] = min(forwards)
    bpy.data.actions.remove(evaluation_action)
    target.animation_data.action = target_action
    return (
        max(errors) if errors else 0.0,
        sum(errors) / len(errors) if errors else 0.0,
        len(range(target_start, target_end + 1)),
    )


def is_helper_mesh(obj: bpy.types.Object) -> bool:
    if obj.type != "MESH":
        return False
    lowered = obj.name.casefold()
    if lowered == "cube" or lowered.startswith("cube."):
        return len(obj.data.polygons) <= 128
    return lowered == "icosphere" or lowered.startswith("icosphere.")


def remove_helper_meshes(keep: Iterable[bpy.types.Object] = ()) -> list[str]:
    keep_set = set(keep)
    removed: list[str] = []
    for obj in list(bpy.data.objects):
        if obj in keep_set or not is_helper_mesh(obj):
            continue
        removed.append(obj.name)
        bpy.data.objects.remove(obj, do_unlink=True)
    return removed


def export_scene(output_path: str, target_objects: Iterable[bpy.types.Object]) -> None:
    leftovers = [obj.name for obj in bpy.data.objects if is_helper_mesh(obj)]
    if leftovers:
        raise RuntimeError("helper meshes remain before export: " + ", ".join(leftovers))
    bpy.ops.object.select_all(action="DESELECT")
    selected = list(target_objects)
    for obj in selected:
        try:
            name = obj.name
        except ReferenceError:
            continue
        if bpy.data.objects.get(name) is not None:
            obj.select_set(True)
    armature = None
    for obj in selected:
        try:
            if obj.type == "ARMATURE" and bpy.data.objects.get(obj.name) is not None:
                armature = obj
                break
        except ReferenceError:
            continue
    if armature is None:
        raise RuntimeError("target object list has no armature")
    bpy.context.view_layer.objects.active = armature
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=output_path,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_skins=True,
        export_animations=True,
        export_animation_mode="BROADCAST",
        export_nla_strips=False,
        export_def_bones=True,
        export_leaf_bone=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_all_influences=False,
    )


def main() -> None:
    config = parse_args()
    input_path = os.path.abspath(config.input)
    source_path = os.path.abspath(config.source)
    output_path = os.path.abspath(config.output)
    if os.path.normcase(output_path) in {
        os.path.normcase(input_path),
        os.path.normcase(source_path),
    }:
        raise SystemExit("output must be separate from input and source")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    target_objects = import_asset(input_path)
    target = armature_from(target_objects)
    # Clear any importer-selected action before collapsing duplicate clips;
    # the bake below assigns each canonical action explicitly.
    if target.animation_data is not None:
        target.animation_data.action = None
    target_actions = canonicalize_actions(
        set(bpy.data.actions), rename_canonical=True
    )
    source_objects = import_asset(source_path)
    source = armature_from(source_objects)
    source_actions = set(bpy.data.actions) - target_actions
    source_actions = canonicalize_actions(source_actions, rename_canonical=False)
    target_by_base = action_map(target_actions)
    source_by_base = action_map(source_actions)
    requested = {
        value.strip()
        for value in config.actions.split(",")
        if value.strip()
    }
    if requested:
        names = sorted(requested)
    else:
        names = sorted(
            name
            for name in target_by_base
            if name.startswith(("ready_", "aim_")) and name in source_by_base
        )
    if not names:
        raise RuntimeError("no shared ready_/aim_ actions found")
    summaries: list[tuple[str, float, float, int]] = []
    for name in names:
        target_action = target_by_base.get(name)
        source_action = source_by_base.get(name)
        if target_action is None or source_action is None:
            print(f"HY3D_CARRY_IK_SKIP action={name} missing_target_or_source")
            continue
        result = bake_action(
            source,
            target,
            source_action,
            target_action,
            config.pole_offset,
        )
        summaries.append((name, *result))
        print(
            "HY3D_CARRY_IK_ACTION",
            f"name={name}",
            f"frames={result[2]}",
            f"max_error={result[0]:.5f}",
            f"mean_error={result[1]:.5f}",
        )
    for obj in source_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    # Source actions are no longer needed and must not become duplicate GLB
    # clips.  Keep all target actions, including clips outside the carry set.
    for action in list(source_actions):
        if action.name in bpy.data.actions and action not in target_actions:
            bpy.data.actions.remove(action)
    visual_meshes = [
        obj
        for obj in target_objects
        if obj.type == "MESH" and len(obj.data.polygons) > 100
    ]
    removed_helpers = remove_helper_meshes(keep=visual_meshes)
    target["steel_tide_carry_ik_actions"] = ",".join(name for name, *_ in summaries)
    target["steel_tide_carry_ik_removed_helpers"] = ",".join(sorted(removed_helpers))
    live_target_objects: list[bpy.types.Object] = []
    for obj in target_objects:
        try:
            if bpy.data.objects.get(obj.name) is not None:
                live_target_objects.append(obj)
        except ReferenceError:
            continue
    export_scene(output_path, live_target_objects)
    max_error = max((item[1] for item in summaries), default=0.0)
    print(
        "HY3D_CARRY_IK_CHECK",
        f"actions={len(summaries)}",
        f"helpers_removed={len(removed_helpers)}",
        f"max_error={max_error:.5f}",
        f"output={output_path}",
    )
    print("HY3D_CARRY_IK_PASS valid=true")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"HY3D_CARRY_IK_FAIL error={error}")
        raise
