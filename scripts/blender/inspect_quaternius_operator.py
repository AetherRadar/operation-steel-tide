"""Print geometry and material details for a Quaternius operator source/runtime file."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy


def parse_args() -> argparse.Namespace:
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    return parser.parse_args(script_args)


def evaluated_counts(obj: bpy.types.Object) -> tuple[int, int, int]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        return len(mesh.vertices), len(mesh.polygons), sum(len(face.vertices) - 2 for face in mesh.polygons)
    finally:
        evaluated.to_mesh_clear()


def deform_group_indices(obj: bpy.types.Object) -> set[int]:
    armature_modifier = next(
        (
            modifier
            for modifier in obj.modifiers
            if modifier.type == "ARMATURE" and modifier.object is not None
        ),
        None,
    )
    if armature_modifier is None:
        return set()
    deform_bones = {
        bone.name
        for bone in armature_modifier.object.data.bones
        if bone.use_deform
    }
    return {
        group.index
        for group in obj.vertex_groups
        if group.name in deform_bones
    }


def main() -> None:
    args = parse_args()
    resolved = args.path.resolve()
    if resolved.suffix.lower() == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(resolved))
    elif resolved.suffix.lower() in {".glb", ".gltf"}:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(resolved))
    else:
        raise RuntimeError(f"Unsupported operator file: {resolved}")
    for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name):
        print(
            "OPERATOR_OBJECT "
            f"name={obj.name} type={obj.type} "
            f"parent={obj.parent.name if obj.parent is not None else '<root>'} "
            f"parent_type={obj.parent_type} parent_bone={obj.parent_bone or '<none>'}"
        )
    total_vertices = 0
    total_faces = 0
    total_triangles = 0
    runtime_names = {"OperatorBody", "OperatorFeet", "OperatorHead", "OperatorLegs"}
    character_meshes = [
        item
        for item in bpy.context.scene.objects
        if item.type == "MESH"
        and (
            resolved.suffix.lower() == ".blend"
            or item.name in runtime_names
        )
        and not any(collection.name == "glTF_not_exported" for collection in item.users_collection)
    ]
    for obj in sorted(character_meshes, key=lambda item: item.name):
        vertices, faces, triangles = evaluated_counts(obj)
        total_vertices += vertices
        total_faces += faces
        total_triangles += triangles
        modifiers = ",".join(f"{mod.type}:{mod.name}" for mod in obj.modifiers) or "none"
        materials = ",".join(slot.material.name if slot.material else "<empty>" for slot in obj.material_slots)
        print(
            "OPERATOR_MESH "
            f"name={obj.name} source_vertices={len(obj.data.vertices)} source_faces={len(obj.data.polygons)} "
            f"evaluated_vertices={vertices} evaluated_faces={faces} evaluated_triangles={triangles} "
            f"uv_layers={len(obj.data.uv_layers)} modifiers={modifiers} materials={materials}"
        )
        deform_groups = deform_group_indices(obj)
        if deform_groups:
            influence_counts = [
                sum(
                    group.group in deform_groups and group.weight > 1.0e-8
                    for group in vertex.groups
                )
                for vertex in obj.data.vertices
            ]
            weight_sums = [
                sum(
                    group.weight
                    for group in vertex.groups
                    if group.group in deform_groups and group.weight > 1.0e-8
                )
                for vertex in obj.data.vertices
            ]
            print(
                "OPERATOR_WEIGHTS "
                f"name={obj.name} max_influences={max(influence_counts, default=0)} "
                f"over_limit={sum(count > 4 for count in influence_counts)} "
                f"sum={min(weight_sums, default=0.0):.6f}..{max(weight_sums, default=0.0):.6f}"
            )
    print(
        "OPERATOR_TOTAL "
        f"meshes={len(character_meshes)} "
        f"evaluated_vertices={total_vertices} evaluated_faces={total_faces} evaluated_triangles={total_triangles}"
    )
    for material in sorted(bpy.data.materials, key=lambda item: item.name):
        principled = material.node_tree.nodes.get("Principled BSDF") if material.use_nodes and material.node_tree else None
        if principled is None:
            print(f"OPERATOR_MATERIAL name={material.name} nodes=false")
            continue
        print(
            "OPERATOR_MATERIAL "
            f"name={material.name} base={tuple(round(v, 4) for v in principled.inputs['Base Color'].default_value)} "
            f"roughness={principled.inputs['Roughness'].default_value:.3f} "
            f"metallic={principled.inputs['Metallic'].default_value:.3f}"
        )
    action_names = sorted(action.name for action in bpy.data.actions)
    print(f"OPERATOR_ACTIONS count={len(action_names)} names={action_names}")


if __name__ == "__main__":
    main()
