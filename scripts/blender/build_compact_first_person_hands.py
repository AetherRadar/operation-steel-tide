"""Author hands-first DJMaesen derivatives; run with Blender 4.5 --background.

Rebuild from the licensed source every time. Grip frames remain unchanged;
proportion and cuff corrections live in the saved Blender mesh, never C#.
"""
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_first_person_arms as source
import build_djmaesen_smg45 as smg

ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "source_art/first_person_hands"
PALETTES = {
    "Assault": ((0.28, 0.32, 0.39), (0.24, 0.28, 0.20)),
    "Medic": ((0.54, 0.65, 0.65), (0.34, 0.39, 0.22)),
    "Recon": ((0.32, 0.38, 0.32), (0.19, 0.23, 0.20)),
    "Scavenger": ((0.52, 0.43, 0.28), (0.32, 0.29, 0.21)),
    "Locksmith": ((0.23, 0.26, 0.28), (0.33, 0.35, 0.31)),
}


def tinted_material(original, name, tint):
    material = original.copy()
    material.name = name
    nodes = material.node_tree.nodes
    bsdf = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    color = bsdf.inputs["Base Color"]
    texture_output = color.links[0].from_socket
    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    mix.inputs[2].default_value = (*tint, 1.0)
    material.node_tree.links.new(texture_output, mix.inputs[1])
    material.node_tree.links.new(mix.outputs[0], color)
    return material


def assign_materials(mesh, sleeve_vertices):
    original = mesh.data.materials[0]
    mesh.data.materials.clear()
    for label, tint in zip(("Sleeve", "Glove"), PALETTES["Assault"]):
        mesh.data.materials.append(tinted_material(original, "Hands" + label, tint))
    for polygon in mesh.data.polygons:
        polygon.material_index = 0 if all(i in sleeve_vertices for i in polygon.vertices) else 1


def export_palette():
    reference = bpy.data.objects["RightArmMesh"]
    palette = []
    for role, colors in PALETTES.items():
        obj = reference.copy()
        obj.data = reference.data.copy()
        obj.name = role
        obj.parent = None
        bpy.context.collection.objects.link(obj)
        for index, tint in enumerate(colors):
            material = obj.data.materials[index].copy()
            material.name = role + ("Sleeve" if index == 0 else "Glove")
            mix = next(n for n in material.node_tree.nodes if n.type == "MIX_RGB")
            mix.inputs[2].default_value = (*tint, 1.0)
            obj.data.materials[index] = material
        palette.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in palette:
        obj.select_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / "palette.blend"))
    bpy.ops.export_scene.gltf(filepath=str(source.OUTPUT_DIR / "hands_palette.glb"),
        export_format="GLB", use_selection=True, export_animations=False)
    for obj in palette:
        bpy.data.objects.remove(obj, do_unlink=True)


def smooth(value):
    value = min(1.0, max(0.0, value))
    return value * value * (3.0 - 2.0 * value)


def polish_static():
    for side in ("Right", "Left"):
        mesh = bpy.data.objects[side + "ArmMesh"]
        wrist = bpy.data.objects[side + "WristFrame"].matrix_world.translation
        palm = bpy.data.objects[side + "PalmFrame"].matrix_world.translation
        inverse = mesh.matrix_world.inverted()
        components = source.evaluated_component_centers(mesh)
        sleeve_vertices = set()
        for _, _, indices in components:
            points = [mesh.matrix_world @ mesh.data.vertices[i].co for i in indices]
            span = Vector(tuple(max(p[j] for p in points) - min(p[j] for p in points)
                                for j in range(3)))
            sleeve = span.length > 0.38
            if sleeve:
                sleeve_vertices.update(indices)
            for index, point in zip(indices, points):
                if sleeve:
                    distance = (point - wrist).length
                    cuff = 1.0 - smooth((distance - 0.035) / 0.18)
                    point += (point - wrist) * 0.28 * cuff
                    bend = smooth((distance - 0.075) / 0.40)
                    point.z -= 0.56 * bend
                    point.x -= (point.x - wrist.x) * 0.32 * bend
                else:
                    point = palm + (point - palm) * 1.32
                mesh.data.vertices[index].co = inverse @ point
        mesh.data.update()
        assign_materials(mesh, sleeve_vertices)


def save(kind):
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / (kind + ".blend")))
    bpy.ops.export_scene.gltf(
        filepath=str(source.OUTPUT_DIR / ("smg45_" + kind + "_arms.glb")),
        export_format="GLB", export_animations=False, export_cameras=False,
        export_lights=False, export_yup=True)


def main():
    for kind in ("rifle", "pistol_service", "pistol_large"):
        filename = "smg45_" + kind + "_arms.glb"
        source.export_static_arms(kind, filename)
        source.clear_scene()
        bpy.ops.import_scene.gltf(filepath=str(source.OUTPUT_DIR / filename))
        polish_static()
        save(kind)
        if kind == "rifle":
            export_palette()
            build_ladder()
    build_native_smg()


def build_ladder():
    for side, x, height in (("Left", -0.24, -0.04), ("Right", 0.24, -0.25)):
        arm = bpy.data.objects[side + "Arm"]
        wrist = bpy.data.objects[side + "WristFrame"].matrix_world.translation.copy()
        palm = bpy.data.objects[side + "PalmFrame"].matrix_world.translation.copy()
        rotation = (palm - wrist).rotation_difference(Vector((0.0, 0.08, 0.14)))
        target = Vector((x, 0.04, height))
        arm.matrix_world = Matrix.Translation(target) @ rotation.to_matrix().to_4x4() @ Matrix.Scale(0.80, 4) @ Matrix.Translation(-palm)
    bpy.context.view_layer.update()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / "ladder.blend"))
    bpy.ops.export_scene.gltf(filepath=str(source.OUTPUT_DIR / "hands_ladder.glb"),
        export_format="GLB", export_animations=False)


def build_native_smg():
    smg.build_first_person()
    arms = bpy.data.objects["AuthoredArms"]
    components = source.evaluated_component_centers(arms)
    sleeve_vertices = set(components[0][2]) | set(components[1][2])
    gloves = [v for v in arms.data.vertices if v.index not in sleeve_vertices]
    for positive in (False, True):
        vertices = [v for v in gloves if (v.co.x > 0) == positive]
        center = sum((v.co.copy() for v in vertices), Vector()) / len(vertices)
        for vertex in vertices:
            vertex.co = center + (vertex.co - center) * 1.24
    assign_materials(arms, sleeve_vertices)
    arms.data.update()
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / "smg_reload.blend"))
    smg.export_root(bpy.data.objects["DJMaesenSMG45FirstPerson"], smg.FIRST_PERSON_GLB, animated=True)


if __name__ == "__main__":
    main()
