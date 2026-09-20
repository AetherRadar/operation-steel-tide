from __future__ import annotations

import bpy
from mathutils import Vector


def fmt_matrix(matrix):
    return [[round(float(value), 6) for value in row] for row in matrix]


def main():
    print(f"BLEND {bpy.data.filepath}", flush=True)
    for obj in bpy.context.scene.objects:
        if obj.type not in {"ARMATURE", "MESH"}:
            continue
        print("OBJECT", obj.name, obj.type, "location=", tuple(round(float(v), 6) for v in obj.location), "scale=", tuple(round(float(v), 6) for v in obj.scale), "matrix_world=", fmt_matrix(obj.matrix_world), flush=True)
        if obj.type == "MESH":
            for modifier in obj.modifiers:
                if modifier.type == "ARMATURE":
                    print("ARMATURE_MODIFIER", obj.name, modifier.object.name if modifier.object else None, flush=True)
    for rig in [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]:
        print("RIG", rig.name, "pose_position=", rig.data.pose_position, flush=True)
        for name in ("root", "Hips", "Spine", "Head", "LeftHand", "RightHand", "LeftUpLeg", "RightUpLeg"):
            bone = rig.data.bones.get(name)
            if bone is None:
                continue
            print("BONE", name, "matrix_local=", fmt_matrix(bone.matrix_local), "head_local=", tuple(round(float(v), 6) for v in bone.head_local), "tail_local=", tuple(round(float(v), 6) for v in bone.tail_local), "parent=", bone.parent.name if bone.parent else None, flush=True)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or not any(modifier.type == "ARMATURE" and modifier.object for modifier in obj.modifiers):
            continue
        evaluated = obj.evaluated_get(depsgraph)
        corners = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        evaluated_corners = [evaluated.matrix_world @ vertex.co for vertex in evaluated.data.vertices]
        minimum = Vector((min(point.x for point in corners), min(point.y for point in corners), min(point.z for point in corners)))
        maximum = Vector((max(point.x for point in corners), max(point.y for point in corners), max(point.z for point in corners)))
        eval_minimum = Vector((min(point.x for point in evaluated_corners), min(point.y for point in evaluated_corners), min(point.z for point in evaluated_corners)))
        eval_maximum = Vector((max(point.x for point in evaluated_corners), max(point.y for point in evaluated_corners), max(point.z for point in evaluated_corners)))
        print("SKIN_MESH", obj.name, "vertices=", len(obj.data.vertices), "bounds=", tuple(round(float(v), 6) for v in minimum), tuple(round(float(v), 6) for v in maximum), "evaluated_bounds=", tuple(round(float(v), 6) for v in eval_minimum), tuple(round(float(v), 6) for v in eval_maximum), flush=True)


if __name__ == "__main__":
    main()
