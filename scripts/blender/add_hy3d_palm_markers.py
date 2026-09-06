"""Add authored palm/contact markers to a HY-3D operator GLB.

The HY-3D finger bones are not reliable palm contact points: ``Index1`` can
sit 15--25 cm from the wrist while the visible palm is only 3--10 cm away.
This DCC pass measures the weighted ``LeftHand``/``RightHand`` skin groups in
the carry reference pose and exports bone-parented ``LeftPalmFrame`` and
``RightPalmFrame`` markers.  Runtime code can use those markers as hand-contact
points without changing the source GLB or guessing an index-bone offset.

The input and output must be separate files.  The source is never overwritten.
Magpie-like damaged hand groups are rejected instead of receiving a misleading
marker.  The output is intended for a private asset store; it contains the
same source meshes and animations plus marker nodes.

Usage::

    blender -b --python add_hy3d_palm_markers.py -- \
        --input operator.clean.glb --output operator.palm-marked.glb
"""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Iterable

import bpy
from mathutils import Vector

SIDES = ("Left", "Right")
MINIMUM_SAMPLES = 20
MINIMUM_TOTAL_WEIGHT = 100.0


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, help="source GLB/GLTF")
    parser.add_argument("--output", required=True, help="separate output GLB")
    return parser.parse_args(argv)


def find_armature() -> bpy.types.Object:
    value = next(
        (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None
    )
    if value is None:
        raise RuntimeError("input GLB has no armature")
    return value


def find_bone(armature: bpy.types.Object, name: str) -> bpy.types.PoseBone | None:
    return armature.pose.bones.get(name) or next(
        (bone for bone in armature.pose.bones if bone.name.endswith(name)), None
    )


def find_visual_meshes() -> list[bpy.types.Object]:
    return [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and len(obj.data.polygons) > 100
    ]


def group_centroid(
    meshes: Iterable[bpy.types.Object],
    group_name: str,
    depsgraph: bpy.types.Depsgraph,
) -> tuple[Vector, int, float]:
    points: list[Vector] = []
    weights: list[float] = []
    for mesh in meshes:
        group = mesh.vertex_groups.get(group_name)
        if group is None:
            continue
        evaluated = mesh.evaluated_get(depsgraph)
        evaluated_mesh = evaluated.to_mesh()
        try:
            for vertex in mesh.data.vertices:
                weight = next(
                    (
                        assignment.weight
                        for assignment in vertex.groups
                        if assignment.group == group.index
                    ),
                    0.0,
                )
                if weight > 0.05:
                    points.append(mesh.matrix_world @ evaluated_mesh.vertices[vertex.index].co)
                    weights.append(weight)
        finally:
            evaluated.to_mesh_clear()
    total = sum(weights)
    if len(points) < MINIMUM_SAMPLES or total < MINIMUM_TOTAL_WEIGHT:
        raise RuntimeError(
            f"{group_name} hand group is incomplete: "
            f"samples={len(points)} total_weight={total:.3f}"
        )
    return (
        sum((point * weight for point, weight in zip(points, weights)), Vector()) / total,
        len(points),
        total,
    )


def add_bone_marker(
    armature: bpy.types.Object,
    name: str,
    bone_name: str,
    world_position: Vector,
) -> bpy.types.Object:
    previous = bpy.data.objects.get(name)
    if previous is not None:
        bpy.data.objects.remove(previous, do_unlink=True)
    marker = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(marker)
    marker.parent = armature
    marker.parent_type = "BONE"
    marker.parent_bone = bone_name
    # Assign after parenting.  Blender's bone-parent local frame is based on
    # the rest bone, so setting local coordinates from a pose matrix is wrong.
    marker.matrix_world.translation = world_position
    marker["steel_tide_palm_frame"] = True
    marker["steel_tide_source_group"] = f"{bone_name} skin group"
    return marker


def export_scene(output_path: str, armature: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="SELECT")
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
    output_path = os.path.abspath(config.output)
    if os.path.normcase(input_path) == os.path.normcase(output_path):
        raise SystemExit("output must be separate from input")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=input_path)
    armature = find_armature()
    meshes = find_visual_meshes()
    if not meshes:
        raise RuntimeError("input GLB has no visual mesh")

    # Use the authored aim reference pose for the static local marker offset.
    action = next(
        (candidate for candidate in bpy.data.actions if candidate.name == "aim_idle"),
        None,
    )
    if action is not None:
        armature.animation_data_create()
        armature.animation_data.action = action
        start, end = action.frame_range
        bpy.context.scene.frame_set(round((start + end) * 0.5))
        bpy.context.view_layer.update()

    depsgraph = bpy.context.evaluated_depsgraph_get()
    for side in SIDES:
        bone_name = f"{side}Hand"
        bone = find_bone(armature, bone_name)
        if bone is None:
            raise RuntimeError(f"missing wrist bone: {bone_name}")
        palm, samples, total_weight = group_centroid(
            meshes,
            bone_name,
            depsgraph,
        )
        add_bone_marker(armature, f"{side}PalmFrame", bone_name, palm)
        wrist = armature.matrix_world @ bone.matrix
        offset = palm - wrist.translation
        print(
            "HY3D_PALM_FRAME",
            f"side={side}",
            f"samples={samples}",
            f"weight={total_weight:.3f}",
            f"offset=({offset.x:.5f},{offset.y:.5f},{offset.z:.5f})",
        )

    # Keep the existing runtime feature gate explicit and bone-parented so the
    # marker survives glTF export and follows the right wrist animation.
    right_wrist = find_bone(armature, "RightHand")
    if right_wrist is None:
        raise RuntimeError("missing RightHand bone for carry marker")
    add_bone_marker(
        armature,
        "SteelTideAuthoredCarryPose",
        "RightHand",
        armature.matrix_world @ right_wrist.matrix.translation,
    )
    export_scene(output_path, armature)
    print(
        "HY3D_PALM_MARKER_CHECK",
        f"sides={len(SIDES)}",
        f"output={output_path}",
    )
    print("HY3D_PALM_MARKER_PASS valid=true")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"HY3D_PALM_MARKER_FAIL error={error}")
        raise

