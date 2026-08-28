"""Inspect evaluated Quaternius operator geometry for animation skinning spikes."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import sys
import traceback

import bpy
from mathutils import Vector


RUNTIME_MESH_NAMES = {
    "OperatorBody",
    "OperatorFeet",
    "OperatorHead",
    "OperatorLegs",
}


def parse_args() -> argparse.Namespace:
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--action", default="downed")
    parser.add_argument("--frame", type=float, default=0.0)
    parser.add_argument("--max-height", type=float, default=0.45)
    parser.add_argument("--max-edge", type=float, default=0.20)
    return parser.parse_args(script_args)


def find_action(name: str) -> bpy.types.Action:
    for action in bpy.data.actions:
        if action.name == name or action.name.rsplit("|", 1)[-1] == name:
            return action
    available = ", ".join(sorted(action.name for action in bpy.data.actions))
    raise RuntimeError(f"Missing action {name}; available actions: {available}")


def vertex_weights(obj: bpy.types.Object, vertex_index: int) -> str:
    memberships = sorted(
        (
            (obj.vertex_groups[item.group].name, item.weight)
            for item in obj.data.vertices[vertex_index].groups
            if item.weight > 1.0e-8
        ),
        key=lambda item: item[1],
        reverse=True,
    )
    return ",".join(f"{name}:{weight:.5f}" for name, weight in memberships) or "none"


def inspect_mesh(obj: bpy.types.Object) -> tuple[Vector, float, int, int]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
        maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
        max_edge = 0.0
        edge_vertices = (0, 0)
        for polygon in mesh.polygons:
            indices = tuple(polygon.vertices)
            for index, first in enumerate(indices):
                second = indices[(index + 1) % len(indices)]
                length = (points[first] - points[second]).length
                if length > max_edge:
                    max_edge = length
                    edge_vertices = (first, second)
        return maximum - minimum, max_edge, edge_vertices[0], edge_vertices[1]
    finally:
        evaluated.to_mesh_clear()


def main() -> bool:
    args = parse_args()
    resolved = args.path.resolve()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(resolved))
    armature = next(
        (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"),
        None,
    )
    if armature is None:
        raise RuntimeError(f"Operator {resolved} has no armature")
    action = find_action(args.action)
    armature.animation_data_create()
    armature.animation_data.action = action
    bpy.context.scene.frame_set(round(args.frame))
    bpy.context.view_layer.update()

    max_dimension = 0.0
    max_edge = 0.0
    max_height = 0.0
    mesh_count = 0
    for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name):
        if obj.type != "MESH" or obj.name not in RUNTIME_MESH_NAMES:
            continue
        mesh_count += 1
        size, edge, first, second = inspect_mesh(obj)
        max_dimension = max(max_dimension, max(size))
        max_edge = max(max_edge, edge)
        max_height = max(max_height, size.z)
        print(
            "OPERATOR_DEFORMATION_MESH "
            f"file={resolved.name} action={args.action} frame={args.frame:.3f} "
            f"mesh={obj.name} size={tuple(round(value, 5) for value in size)} "
            f"max_edge={edge:.5f} edge={first}/{second} "
            f"weights_a={vertex_weights(obj, first)} "
            f"weights_b={vertex_weights(obj, second)}"
        )
    valid = (
        mesh_count == len(RUNTIME_MESH_NAMES)
        and max_height <= args.max_height
        and max_edge <= args.max_edge
    )
    print(
        "OPERATOR_DEFORMATION_CHECK "
        f"file={resolved.name} action={args.action} frame={args.frame:.3f} "
        f"meshes={mesh_count}/{len(RUNTIME_MESH_NAMES)} "
        f"max_dimension={max_dimension:.5f} max_height={max_height:.5f} "
        f"max_edge={max_edge:.5f} valid={valid}"
    )
    print(f"OPERATOR_DEFORMATION_PASS valid={valid}")
    return valid


if __name__ == "__main__":
    try:
        valid = main()
    except Exception:
        traceback.print_exc()
        valid = False
    if not valid:
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(2)
