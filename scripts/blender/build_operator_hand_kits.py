"""Build role-specific first-person hand kits from the authored DJMaesen poses.

The source hands remain the licensed production mesh. This pass creates five
Blender-authored shape variants, shortens the visible sleeve, adds a subtle
role-specific cuff treatment, and exports one scene containing all role kits.
Runtime selects one role subtree; it never reshapes the hands in C#.

Run with Blender 4.5+:
    blender --background --factory-startup --python scripts/blender/build_operator_hand_kits.py --python-exit-code 2
"""

from __future__ import annotations

from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "assets" / "models" / "djmaesen_smg45"
SOURCE_DIR = ROOT / "source_art" / "first_person_hands"
OUT_BLEND = SOURCE_DIR / "operator_hand_kits.blend"
OUT_GLB = OUTPUT_DIR / "operator_hand_kits.glb"

ROLE_SPECS = {
    "Viper": {
        "sleeve_length": 0.72,
        "cuff_width": 0.93,
        "glove_width": 1.03,
        "glove_depth": 1.00,
        "sleeve": (0.18, 0.22, 0.25),
        "glove": (0.08, 0.10, 0.09),
        "band": (0.32, 0.39, 0.23),
    },
    "Heron": {
        "sleeve_length": 0.68,
        "cuff_width": 0.91,
        "glove_width": 0.98,
        "glove_depth": 1.03,
        "sleeve": (0.31, 0.42, 0.43),
        "glove": (0.16, 0.22, 0.21),
        "band": (0.67, 0.82, 0.80),
    },
    "Lynx": {
        "sleeve_length": 0.76,
        "cuff_width": 0.88,
        "glove_width": 0.94,
        "glove_depth": 0.97,
        "sleeve": (0.19, 0.24, 0.20),
        "glove": (0.11, 0.14, 0.12),
        "band": (0.36, 0.47, 0.27),
    },
    "Magpie": {
        "sleeve_length": 0.64,
        "cuff_width": 0.98,
        "glove_width": 1.08,
        "glove_depth": 1.05,
        "sleeve": (0.34, 0.27, 0.16),
        "glove": (0.20, 0.15, 0.10),
        "band": (0.53, 0.41, 0.22),
    },
    "Jackal": {
        "sleeve_length": 0.70,
        "cuff_width": 0.90,
        "glove_width": 1.01,
        "glove_depth": 0.98,
        "sleeve": (0.12, 0.14, 0.15),
        "glove": (0.07, 0.08, 0.08),
        "band": (0.38, 0.42, 0.42),
    },
}

FAMILY_SOURCES = {
    "Rifle": OUTPUT_DIR / "smg45_rifle_arms.glb",
    "PistolService": OUTPUT_DIR / "smg45_pistol_service_arms.glb",
    "PistolLarge": OUTPUT_DIR / "smg45_pistol_large_arms.glb",
    "Smg": OUTPUT_DIR / "smg45_first_person.glb",
}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    return [root, *list(root.children_recursive)]


def find_object(root: bpy.types.Object, name: str) -> bpy.types.Object:
    for obj in descendants(root):
        if obj.name == name or obj.name.startswith(name + "."):
            return obj
    raise RuntimeError(f"Missing {name} below {root.name}")


def imported_root(family: str, imported: set[bpy.types.Object]) -> bpy.types.Object:
    expected = "DJMaesenSMG45FirstPerson" if family == "Smg" else "StaticFirstPersonArms"
    for candidate in imported:
        if candidate.name == expected or candidate.name.startswith(expected + "."):
            return candidate
    raise RuntimeError(f"Imported asset is missing {expected}")


def remove_import_noise(root: bpy.types.Object, imported: set[bpy.types.Object]) -> None:
    keep = set(descendants(root))
    for obj in list(imported):
        if obj not in keep and obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)


def copy_material(
    original: bpy.types.Material,
    name: str,
    tint: tuple[float, float, float],
    roughness: float,
) -> bpy.types.Material:
    material = original.copy()
    material.name = name
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    base_color = bsdf.inputs["Base Color"]
    if base_color.links:
        source_socket = base_color.links[0].from_socket
        for link in list(base_color.links):
            links.remove(link)
        mix = nodes.new("ShaderNodeMixRGB")
        mix.name = f"{name}Tint"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        mix.inputs[2].default_value = (*tint, 1.0)
        links.new(source_socket, mix.inputs[1])
        links.new(mix.outputs[0], base_color)
    else:
        base_color.default_value = (*tint, 1.0)
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    return material


def material_variant(
    role: str,
    mesh: bpy.types.Object,
    spec: dict[str, object],
) -> None:
    if not hasattr(mesh.data, "materials") or len(mesh.data.materials) < 2:
        return
    sleeve_source = mesh.data.materials[0]
    glove_source = mesh.data.materials[1]
    mesh.data.materials.clear()
    mesh.data.materials.append(
        copy_material(
            sleeve_source,
            f"{role}HandsSleeve",
            spec["sleeve"],
            0.74,
        )
    )
    mesh.data.materials.append(
        copy_material(
            glove_source,
            f"{role}HandsGlove",
            spec["glove"],
            0.58,
        )
    )
    mesh.data.materials.append(
        copy_material(
            glove_source,
            f"{role}HandsBand",
            spec["band"],
            0.46,
        )
    )


def mesh_vertex_material_indices(mesh: bpy.types.Mesh) -> tuple[set[int], set[int]]:
    sleeve: set[int] = set()
    glove: set[int] = set()
    for polygon in mesh.polygons:
        target = sleeve if polygon.material_index == 0 else glove
        target.update(polygon.vertices)
    return sleeve, glove


def deform_static_arms(root: bpy.types.Object, role: str) -> None:
    spec = ROLE_SPECS[role]
    for side in ("Left", "Right"):
        mesh = find_object(root, f"{side}ArmMesh")
        palm = find_object(root, f"{side}PalmFrame").matrix_world.translation
        wrist = find_object(root, f"{side}WristFrame").matrix_world.translation
        sleeve_vertices, _ = mesh_vertex_material_indices(mesh.data)
        sleeve_points = [
            mesh.matrix_world @ mesh.data.vertices[index].co
            for index in sleeve_vertices
        ]
        sleeve_center = sum(sleeve_points, Vector()) / max(1, len(sleeve_points))
        sleeve_axis = sleeve_center - wrist
        if sleeve_axis.length < 0.001:
            sleeve_axis = Vector((0.0, -1.0, 0.0))
        sleeve_axis.normalize()
        inverse = mesh.matrix_world.inverted()
        for vertex in mesh.data.vertices:
            point = mesh.matrix_world @ vertex.co
            offset = point - wrist
            along = sleeve_axis * offset.dot(sleeve_axis)
            radial = offset - along
            if vertex.index in sleeve_vertices:
                point = wrist + along * float(spec["sleeve_length"]) + radial * float(spec["cuff_width"])
            else:
                hand_offset = point - palm
                point = palm + Vector((
                    hand_offset.x * float(spec["glove_width"]),
                    hand_offset.y * float(spec["glove_depth"]),
                    hand_offset.z * float(spec["glove_depth"]),
                ))
            vertex.co = inverse @ point
        mesh.data.update()
        material_variant(role, mesh, spec)
        assign_cuff_band(mesh, wrist)


def assign_cuff_band(mesh: bpy.types.Object, wrist: Vector) -> None:
    if len(mesh.data.materials) < 3:
        return
    for polygon in mesh.data.polygons:
        center = mesh.matrix_world @ polygon.center
        if (center - wrist).length <= 0.045:
            polygon.material_index = 2


def deform_animated_smg(root: bpy.types.Object, role: str) -> None:
    """Keep the animation rig intact; use a compact authored sleeve/material variant."""
    spec = ROLE_SPECS[role]
    arms = find_object(root, "AuthoredArms")
    material_variant(role, arms, spec)
    for obj in descendants(root):
        if obj.animation_data is None:
            continue
        for track in obj.animation_data.nla_tracks:
            track.name = "reload"
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = 240
    # The animated source has skinned geometry. Its sleeve silhouette is
    # corrected in build_compact_first_person_hands.py; do not edit rest-space
    # vertices here or the reload pose can develop skinning spikes.


def duplicate_tree(root: bpy.types.Object, parent: bpy.types.Object, name: str) -> bpy.types.Object:
    copy = root.copy()
    if root.data is not None:
        copy.data = root.data.copy()
    copy.name = name
    parent.children.link(copy)
    for child in root.children:
        duplicate_tree(child, copy, child.name)
    return copy


def prefix_tree(root: bpy.types.Object, role: str) -> None:
    prefix = role + "__"
    for obj in descendants(root):
        base_name = obj.name.split(".", 1)[0]
        obj.name = prefix + base_name


def import_family(family: str, role: str, role_root: bpy.types.Object) -> bpy.types.Object:
    path = FAMILY_SOURCES[family]
    if not path.exists():
        raise FileNotFoundError(path)
    existing = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = set(bpy.context.scene.objects) - existing
    source_root = imported_root(family, imported)
    remove_import_noise(source_root, imported)
    source_root.name = family
    source_root.parent = role_root
    if family == "Smg":
        deform_animated_smg(source_root, role)
    else:
        deform_static_arms(source_root, role)
    prefix_tree(source_root, role)
    return source_root


def build_ladder_variant(role: str, role_root: bpy.types.Object) -> bpy.types.Object:
    # Use the rifle's authored hand mesh as the licensed base, then place each
    # hand around a dedicated rail-grip pose. The runtime only adds a small
    # alternating vertical reach while climbing.
    source_path = FAMILY_SOURCES["Rifle"]
    existing = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    imported = set(bpy.context.scene.objects) - existing
    source_root = imported_root("Rifle", imported)
    remove_import_noise(source_root, imported)
    source_root.name = "Ladder"
    source_root.parent = role_root
    deform_static_arms(source_root, role)
    for side, target in (
        ("Left", Vector((-0.24, 0.04, -0.04))),
        ("Right", Vector((0.24, 0.04, -0.25))),
    ):
        arm = find_object(source_root, f"{side}Arm")
        palm = find_object(source_root, f"{side}PalmFrame").matrix_world.translation
        wrist = find_object(source_root, f"{side}WristFrame").matrix_world.translation
        arm.location += target - palm
        arm.rotation_euler.z += 0.10 if side == "Left" else -0.10
        # The mesh stays close to the hand marker; this reduces the floating
        # palm look caused by translating the whole arm after export.
        _ = wrist
    prefix_tree(source_root, role)
    return source_root


def main() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    clear_scene()
    scene_root = bpy.data.objects.new("OperatorFirstPersonHandKits", None)
    bpy.context.scene.collection.objects.link(scene_root)

    for role in ROLE_SPECS:
        role_root = bpy.data.objects.new(role, None)
        role_root.parent = scene_root
        bpy.context.scene.collection.objects.link(role_root)
        for family in ("Rifle", "PistolService", "PistolLarge", "Smg"):
            import_family(family, role, role_root)
        build_ladder_variant(role, role_root)

    bpy.ops.object.select_all(action="DESELECT")
    scene_root.select_set(True)
    for child in scene_root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = scene_root
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_frame_range=True,
        export_force_sampling=True,
        export_animation_mode="NLA_TRACKS",
        export_nla_strips_merged_animation_name="reload",
        export_cameras=False,
        export_lights=False,
        export_apply=False,
        export_image_format="AUTO",
        export_yup=True,
    )
    print(f"Wrote {OUT_BLEND}")
    print(f"Wrote {OUT_GLB}")


if __name__ == "__main__":
    main()
