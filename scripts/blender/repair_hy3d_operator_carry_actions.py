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


def copy_curve(source, action) -> None:
    target = action.fcurves.new(source.data_path, index=source.array_index)
    for key in source.keyframe_points:
        target.keyframe_points.insert(key.co.x, key.co.y, options={"FAST"})
    target.update()


def curl_finger_curves(action) -> None:
    """Close the authored finger chains around a rifle grip.

    HY-3D's idle hand is open.  The imported skeleton uses quaternion tracks;
    applying a local X-axis curl to each phalanx keeps the palm orientation
    untouched while making the fingers visibly wrap the trigger/handguard.
    """
    curls = {}
    for side in ("Left", "Right"):
        for finger in ("Index", "Middle", "Ring", "Pinky"):
            for segment, degrees in ((1, 52.0), (2, 68.0), (3, 76.0)):
                curls[f"{side}Hand{finger}{segment}"] = degrees
        for segment, degrees in ((1, 34.0), (2, 45.0), (3, 50.0)):
            curls[f"{side}HandThumb{segment}"] = degrees

    grouped = {}
    for curve in action.fcurves:
        if curve.data_path.endswith("rotation_quaternion"):
            bone_name = bone_name_from_path(curve.data_path)
            if bone_name in curls:
                grouped.setdefault(bone_name, {})[curve.array_index] = curve
    for bone_name, curves in grouped.items():
        if set(curves) != {0, 1, 2, 3}:
            continue
        offset = Quaternion((1.0, 0.0, 0.0), curls[bone_name] * 3.14159265 / 180.0)
        for key_index in range(len(curves[0].keyframe_points)):
            source = Quaternion(tuple(curves[index].keyframe_points[key_index].co.y for index in range(4)))
            curled = source @ offset
            for index in range(4):
                curves[index].keyframe_points[key_index].co.y = curled[index]


def repair_actions(source_path: str, output_path: str) -> None:
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(source_path))
    idle = bpy.data.actions.get("idle")
    if idle is None:
        raise RuntimeError(f"{source_path} has no idle action")

    for action in list(bpy.data.actions):
        if action == idle or not (
            action.name.startswith("ready_") or action.name.startswith("aim_")
        ):
            continue
        for curve in list(action.fcurves):
            if is_upper_body_curve(curve.data_path):
                action.fcurves.remove(curve)
        for source_curve in idle.fcurves:
            if is_upper_body_curve(source_curve.data_path):
                copy_curve(source_curve, action)
        curl_finger_curves(action)

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
