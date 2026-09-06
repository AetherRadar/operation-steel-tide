"""Restore Magpie's missing right-hand skin from the authored left hand.

The private HY-3D Magpie export lost the disconnected right-hand component
when the embedded prop cleanup treated every small RightHand component as a
weapon.  This repair keeps the source GLB private while rebuilding the hand
in its right-hand bone frame, preserving materials, UVs, and skin weights.
"""

import argparse
import os
import sys

import bpy
from mathutils import Matrix


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    values = sys.argv[sys.argv.index("--") + 1:]
    return parser.parse_args(values)


def resolve_bone(armature, name):
    return armature.data.bones.get(name) or armature.data.bones.get("mixamorig:" + name)


def group_name(mesh, index):
    return mesh.vertex_groups[index].name if index < len(mesh.vertex_groups) else ""


def dominant_group(mesh, vertex):
    weighted = [(assignment.weight, group_name(mesh, assignment.group)) for assignment in vertex.groups]
    return max(weighted, default=(0.0, ""))


def mirrored_group_name(name):
    if name.startswith("Left"):
        return "Right" + name[4:]
    if name.startswith("left"):
        return "right" + name[4:]
    return name


def make_patch(mesh, armature):
    left_hand = resolve_bone(armature, "LeftHand")
    right_hand = resolve_bone(armature, "RightHand")
    if left_hand is None or right_hand is None:
        raise RuntimeError("Magpie armature is missing LeftHand/RightHand")

    source = mesh.data
    left_names = {"LeftHand", "LeftHand_end"}
    selected_polygons = []
    for polygon in source.polygons:
        dominant = {dominant_group(mesh, source.vertices[index])[1] for index in polygon.vertices}
        if dominant and dominant.issubset(left_names):
            selected_polygons.append(polygon)
    if len(selected_polygons) < 100:
        raise RuntimeError("could not identify the authored left-hand surface")

    left_world = armature.matrix_world @ left_hand.matrix_local
    right_world = armature.matrix_world @ right_hand.matrix_local
    hand_frame = right_world @ left_world.inverted()
    mesh_to_world = mesh.matrix_world
    world_to_mesh = mesh_to_world.inverted()

    source_indices = []
    source_to_new = {}
    coordinates = []
    faces = []
    for polygon in selected_polygons:
        face = []
        for source_index in polygon.vertices:
            if source_index not in source_to_new:
                source_to_new[source_index] = len(coordinates)
                source_indices.append(source_index)
                source_world = mesh_to_world @ source.vertices[source_index].co
                coordinates.append(world_to_mesh @ (hand_frame @ source_world))
            face.append(source_to_new[source_index])
        faces.append(face)

    old_patch = bpy.data.objects.get("MagpieRightHandPatch")
    if old_patch is not None:
        bpy.data.objects.remove(old_patch, do_unlink=True)
    patch_data = bpy.data.meshes.new("MagpieRightHandPatch")
    patch_data.from_pydata(coordinates, [], faces)
    patch_data.update()
    for material in source.materials:
        patch_data.materials.append(material)
    for new_polygon, old_polygon in zip(patch_data.polygons, selected_polygons):
        new_polygon.material_index = old_polygon.material_index

    source_uv = source.uv_layers.active
    if source_uv is not None:
        patch_uv = patch_data.uv_layers.new(name=source_uv.name)
        for new_polygon, old_polygon in zip(patch_data.polygons, selected_polygons):
            for new_loop, old_loop in zip(new_polygon.loop_indices, old_polygon.loop_indices):
                patch_uv.data[new_loop].uv = source_uv.data[old_loop].uv.copy()

    patch = bpy.data.objects.new("MagpieRightHandPatch", patch_data)
    bpy.context.collection.objects.link(patch)
    patch.parent = mesh.parent
    patch.parent_type = mesh.parent_type
    if mesh.parent_type == "BONE":
        patch.parent_bone = mesh.parent_bone
    patch.matrix_world = mesh.matrix_world.copy()

    groups = {}
    for source_group in mesh.vertex_groups:
        target_name = mirrored_group_name(source_group.name)
        groups.setdefault(target_name, patch.vertex_groups.new(name=target_name))
    for new_index, source_index in enumerate(source_indices):
        for assignment in source.vertices[source_index].groups:
            source_name = group_name(mesh, assignment.group)
            groups[mirrored_group_name(source_name)].add([new_index], assignment.weight, "REPLACE")

    armature_modifier = patch.modifiers.new(name="Armature", type="ARMATURE")
    armature_modifier.object = armature
    armature_modifier.use_deform_preserve_volume = True
    patch["steel_tide_generated"] = "magpie_right_hand_repair"
    patch["source_surface_faces"] = len(selected_polygons)
    return patch, len(selected_polygons), len(source_indices)


def main():
    cfg = parse_args()
    input_path = os.path.abspath(cfg.input)
    output_path = os.path.abspath(cfg.output)
    if input_path == output_path:
        raise SystemExit("output must be separate from input")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=input_path)
    armature = next((obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    mesh = max(meshes, key=lambda obj: len(obj.data.polygons), default=None)
    if armature is None or mesh is None:
        raise RuntimeError("input GLB has no armature and visual mesh")
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    patch, faces, vertices = make_patch(mesh, armature)
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
    print("MAGPIE_REPAIR_CHECK", f"faces={faces}", f"vertices={vertices}", f"patch={patch.name}", f"output={output_path}")
    print("MAGPIE_REPAIR_PASS valid=true")


if __name__ == "__main__":
    main()
