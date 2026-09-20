"""Bake an authored operator presentation root into Blender skin data.

The exported skeleton and meshes must agree in one neutral object space. This
pass moves the outer presentation matrix into edit-bone rest data and skinned
mesh vertices, then leaves the armature and presentation root at identity.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import bpy
from mathutils import Matrix


PRESENTATION_ROOT = "AuthoredOperatorPresentation"


def hierarchy_root(rig):
    root = rig
    while root.parent is not None:
        root = root.parent
    return root


def object_depth(obj):
    depth = 0
    while obj.parent is not None:
        depth += 1
        obj = obj.parent
    return depth


def bake_operator_presentation(rig):
    root = hierarchy_root(rig)
    if root.name != PRESENTATION_ROOT:
        return False
    transform = root.matrix_world.copy()
    worlds = {obj: obj.matrix_world.copy() for obj in root.children_recursive}
    meshes = [obj for obj in root.children_recursive
              if obj.type == "MESH" and any(
                  modifier.type == "ARMATURE" and modifier.object == rig
                  for modifier in obj.modifiers)]
    containers = []
    ancestor = rig.parent
    while ancestor is not None and ancestor != root:
        containers.append(ancestor)
        ancestor = ancestor.parent

    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        for bone in rig.data.edit_bones:
            bone.matrix = transform @ bone.matrix
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")

    # Neutralize the object hierarchy before placing the authored data in it.
    root.matrix_world = Matrix.Identity(4)
    for container in containers:
        container.matrix_world = Matrix.Identity(4)
    rig.matrix_world = Matrix.Identity(4)
    for mesh in meshes:
        matrix = worlds[mesh]
        for vertex in mesh.data.vertices:
            vertex.co = matrix @ vertex.co
        mesh.matrix_world = Matrix.Identity(4)

    for obj in sorted(root.children_recursive, key=object_depth):
        if obj in meshes or obj == rig or obj in containers:
            continue
        obj.matrix_world = worlds[obj]
    bpy.context.view_layer.update()

    root["steel_tide_presentation_baked"] = True
    root["steel_tide_presentation_source_matrix"] = [
        float(transform[row][column]) for row in range(4) for column in range(4)
    ]
    return True


def export(rig, path, export_apply=False):
    bpy.ops.object.select_all(action="DESELECT")
    root = hierarchy_root(rig)
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(path), export_format="GLB", use_selection=True,
        export_apply=export_apply, export_skins=True, export_animations=True,
        export_animation_mode="BROADCAST", export_nla_strips=False,
        export_rest_position_armature=True,
        export_def_bones=True, export_leaf_bone=False, export_morph=False,
        export_materials="EXPORT", export_image_format="AUTO",
        export_texcoords=True, export_normals=True, export_tangents=False,
        export_all_influences=False, export_cameras=False, export_lights=False,
    )


def main():
    argv = __import__("sys").argv[__import__("sys").argv.index("--") + 1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output-blend", type=Path, required=True)
    parser.add_argument("--output-glb", type=Path, required=True)
    parser.add_argument("--export-apply", action="store_true")
    args = parser.parse_args(argv)
    bpy.ops.wm.open_mainfile(filepath=str(args.input))
    rig = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    valid = bake_operator_presentation(rig)
    if not valid:
        raise RuntimeError("The input operator has no authored presentation root")
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output_blend))
    export(rig, args.output_glb, export_apply=args.export_apply)
    print(f"OPERATOR_PRESENTATION_BAKE role={rig.name} valid=true", flush=True)


if __name__ == "__main__":
    main()
