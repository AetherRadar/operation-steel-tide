"""Repair HY-3D carry clips without deforming the authored skin.

Run with Blender 4.x:
    blender -b --python repair_hy3d_operator_carry_actions.py -- in.glb out.glb

The generated HY-3D clips contain upper-body location/rotation tracks that
pull sleeves, fingers, straps, and hair into long spikes when sampled by
Godot.  This DCC pass keeps the original locomotion below the pelvis and
replaces only the upper-body tracks with the stable authored ``idle`` pose.
The game then applies its two-bone arm solver to place the palms on the
weapon's grip markers.  The output is intentionally an external/private GLB;
the source asset may be redistributed only under its own license.
"""

from __future__ import annotations

import os
import sys

import bpy
from math import radians

from mathutils import Quaternion


UPPER_BODY_BONES = (
    "Spine",
    "Neck",
    "Head",
    "Shoulder",
    "Arm",
    "ForeArm",
    "Hand",
    "Index",
    "Middle",
    "Ring",
    "Pinky",
    "Thumb",
    "Clavicle",
)


def bone_name_from_path(data_path: str) -> str:
    parts = data_path.split('"')
    return parts[1] if len(parts) >= 2 else ""


def is_upper_body_curve(data_path: str) -> bool:
    bone_name = bone_name_from_path(data_path)
    return any(token in bone_name for token in UPPER_BODY_BONES)


FINGER_NAMES = tuple(
    f"{side}Hand{finger}{segment}"
    for side in ("Left", "Right")
    for finger in ("Index", "Middle", "Ring", "Pinky", "Thumb")
    for segment in range(1, 4)
)


def is_finger_curve(data_path: str) -> bool:
    return bone_name_from_path(data_path) in FINGER_NAMES


def is_finger_rotation_curve(data_path: str) -> bool:
    """Return true only for quaternion rotation tracks on finger bones.

    The source clips also contain location/scale tracks for the finger bones.
    Removing those tracks makes the animated child translations fall back to
    malformed defaults in Godot, which stretches gloves and fingers into
    spikes.  Carry repair should replace the authored rotations only.
    """
    return is_finger_curve(data_path) and "rotation_quaternion" in data_path


def copy_curve(source, action) -> None:
    target = action.fcurves.new(source.data_path, index=source.array_index)
    for key in source.keyframe_points:
        target.keyframe_points.insert(key.co.x, key.co.y, options={"FAST"})
    target.update()


def capture_idle_finger_pose(idle, frame: float) -> dict[str, Quaternion]:
    """Read one stable local finger pose before replacing carry curves."""
    values: dict[str, Quaternion] = {}
    for bone_name in FINGER_NAMES:
        components: list[float] = []
        for index in range(4):
            curve = next(
                (
                    curve
                    for curve in idle.fcurves
                    if is_finger_rotation_curve(curve.data_path)
                    and bone_name_from_path(curve.data_path) == bone_name
                    and curve.array_index == index
                ),
                None,
            )
            components.append(curve.evaluate(frame) if curve is not None else (1.0 if index == 0 else 0.0))
        values[bone_name] = Quaternion(components)
    return values


def curl_finger_curves(action, baseline: dict[str, Quaternion]) -> None:
    """Close the authored finger chains around a rifle grip.

    HY-3D's idle hand is open.  The imported skeleton uses quaternion tracks;
    applying a local X-axis curl to each phalanx keeps the palm orientation
    untouched while making the fingers visibly wrap the trigger/handguard.
    """
    curls = {}
    for side in ("Left", "Right"):
        # HY-3D mirrors the local finger axes.  Applying the same sign to both
        # hands makes one palm curl away from the rifle; use opposite signs in
        # the authored local X frame.
        side_sign = 1.0 if side == "Left" else -1.0
        for finger in ("Index", "Middle", "Ring", "Pinky"):
            for segment, degrees in ((1, 52.0), (2, 68.0), (3, 76.0)):
                curls[f"{side}Hand{finger}{segment}"] = side_sign * degrees
        for segment, degrees in ((1, 34.0), (2, 45.0), (3, 50.0)):
            curls[f"{side}HandThumb{segment}"] = side_sign * degrees

    for curve in list(action.fcurves):
        if is_finger_rotation_curve(curve.data_path):
            action.fcurves.remove(curve)

    start, end = action.frame_range
    mid = (start + end) * 0.5
    for bone_name, degrees in curls.items():
        offset = Quaternion((1.0, 0.0, 0.0), radians(degrees))
        curled = baseline[bone_name] @ offset
        for index in range(4):
            curve = action.fcurves.new(
                f'pose.bones["{bone_name}"].rotation_quaternion',
                index=index,
            )
            for frame in (start, mid, end):
                curve.keyframe_points.insert(frame, curled[index], options={"FAST"})
            curve.update()


def repair_actions(source_path: str, output_path: str) -> None:
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(source_path))
    idle = bpy.data.actions.get("idle")
    if idle is None:
        raise RuntimeError(f"{source_path} has no idle action")

    idle_finger_pose = capture_idle_finger_pose(idle, sum(idle.frame_range) * 0.5)
    for action in list(bpy.data.actions):
        if action == idle or not (
            action.name.startswith("ready_") or action.name.startswith("aim_")
        ):
            continue
        for curve in list(action.fcurves):
            if is_upper_body_curve(curve.data_path):
                action.fcurves.remove(curve)
        for source_curve in idle.fcurves:
            if is_upper_body_curve(source_curve.data_path) and not is_finger_rotation_curve(source_curve.data_path):
                copy_curve(source_curve, action)
        curl_finger_curves(action, idle_finger_pose)

    bpy.ops.export_scene.gltf(
        filepath=os.path.abspath(output_path),
        export_format="GLB",
        export_animations=True,
        export_skins=True,
        export_materials="EXPORT",
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
    )


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) != 2:
        raise SystemExit("usage: blender -b --python repair_hy3d_operator_carry_actions.py -- in.glb out.glb")
    repair_actions(args[0], args[1])
    print(f"HY3D_CARRY_ACTIONS_REPAIRED {os.path.abspath(args[1])}")
