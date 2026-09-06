"""Audit the actual skinned hand volume instead of trusting socket names."""
import argparse
import json
import sys

import bpy
from mathutils import Vector


def xyz(value):
    return [round(float(item), 6) for item in value]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("asset")
    cfg = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=cfg.asset)
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    rig.animation_data_clear()
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    print("HAND_RIG", rig.name, "matrix", [xyz(row) for row in rig.matrix_world])
    for side in ("Right", "Left"):
        for suffix in ("Arm", "ForeArm", "Hand", "Hand_end"):
            name = side + suffix
            bone = rig.data.bones.get(name)
            if bone is None:
                continue
            world = rig.matrix_world @ bone.matrix_local
            vertices = []
            weights = []
            world_vertices = []
            for mesh in meshes:
                group = mesh.vertex_groups.get(name)
                if group is None:
                    continue
                for vertex in mesh.data.vertices:
                    weight = next((g.weight for g in vertex.groups if g.group == group.index), 0.0)
                    if weight < 0.5:
                        continue
                    point = mesh.matrix_world @ vertex.co
                    vertices.append(world.inverted() @ point)
                    world_vertices.append(point)
                    weights.append(weight)
            bounds = {}
            if vertices:
                bounds = {
                    "local_min": xyz(Vector(min(p[axis] for p in vertices) for axis in range(3))),
                    "local_max": xyz(Vector(max(p[axis] for p in vertices) for axis in range(3))),
                    "local_mean": xyz(sum(vertices, Vector()) / len(vertices)),
                    "world_min": xyz(Vector(min(p[axis] for p in world_vertices) for axis in range(3))),
                    "world_max": xyz(Vector(max(p[axis] for p in world_vertices) for axis in range(3))),
                }
            print("HAND_SKIN", json.dumps({
                "bone": name, "head": xyz(rig.matrix_world @ bone.head_local),
                "tail": xyz(rig.matrix_world @ bone.tail_local),
                "matrix": [xyz(row) for row in world],
                "strong_vertices": len(vertices), **bounds,
            }))
    for obj in bpy.context.scene.objects:
        if obj.name == "WeaponSocket":
            print("HAND_SOCKET", obj.parent_type, obj.parent_bone, xyz(obj.location),
                  [xyz(row) for row in obj.matrix_world])


if __name__ == "__main__":
    main()
