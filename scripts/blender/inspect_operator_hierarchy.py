from __future__ import annotations

import bpy


def fmt(matrix):
    return [[round(float(value), 6) for value in row] for row in matrix]


for obj in bpy.context.scene.objects:
    if obj.type not in {"ARMATURE", "MESH", "EMPTY"}:
        continue
    parent = obj.parent.name if obj.parent else None
    print("HIERARCHY", obj.name, obj.type, "parent=", parent,
          "basis=", fmt(obj.matrix_basis), "world=", fmt(obj.matrix_world), flush=True)
