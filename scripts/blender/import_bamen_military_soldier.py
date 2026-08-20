"""Prepare BAMEN's CC BY military soldier for the Godot preview UI.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup \
        --python scripts/blender/import_bamen_military_soldier.py -- \
        --source source_art/third_party/bamen_military_soldier/bamen_military_soldier.fbx

The checked-in FBX remains the unmodified redistribution source. This script
normalizes naming and materials, preserves an editable Blender source, then
discards the malformed supplied action pose before exporting the preview GLB.
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = REPO_ROOT / "assets" / "models" / "bamen_military_soldier"
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "bamen_military_soldier"
PREVIEW_DIR = REPO_ROOT / "build" / "art-previews"


MATERIALS = {
    "armor": ((0.115, 0.135, 0.095, 1.0), 0.08, 0.58),
    "boots": ((0.025, 0.029, 0.027, 1.0), 0.0, 0.78),
    "clothes": ((0.075, 0.105, 0.09, 1.0), 0.0, 0.88),
    "elements": ((0.045, 0.055, 0.052, 1.0), 0.12, 0.5),
    "fabric": ((0.18, 0.19, 0.13, 1.0), 0.0, 0.92),
    "flesh": ((0.22, 0.17, 0.115, 1.0), 0.0, 0.72),
    "glass": ((0.018, 0.035, 0.032, 1.0), 0.72, 0.16),
    "iron": ((0.08, 0.095, 0.09, 1.0), 0.86, 0.27),
    "metal": ((0.22, 0.245, 0.235, 1.0), 0.8, 0.24),
    "shoes": ((0.035, 0.04, 0.037, 1.0), 0.0, 0.76),
    # The model's face is fully covered by the helmet; reuse its skin slot for
    # low-reflectance tactical gloves so no bare hands break the uniform design.
    "skin": ((0.018, 0.024, 0.023, 1.0), 0.0, 0.64),
}


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--skip-preview", action="store_true")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def tune_material(material: bpy.types.Material) -> None:
    key = next((name for name in MATERIALS if f"({name})" in material.name.lower()), None)
    if key is None:
        return
    color, metallic, roughness = MATERIALS[key]
    material.name = f"Soldier_{key.title()}"
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        for corner in evaluated.bound_box:
            point = evaluated.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, point.x)
            minimum.y = min(minimum.y, point.y)
            minimum.z = min(minimum.z, point.z)
            maximum.x = max(maximum.x, point.x)
            maximum.y = max(maximum.y, point.y)
            maximum.z = max(maximum.z, point.z)
    return minimum, maximum


def prepare_scene(source: Path) -> tuple[bpy.types.Object, bpy.types.Object]:
    clear_scene()
    bpy.ops.import_scene.fbx(filepath=str(source.resolve()))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 1 or len(armatures) != 1:
        raise RuntimeError(f"Expected one mesh and one armature, found {len(meshes)} and {len(armatures)}")

    mesh = meshes[0]
    armature = armatures[0]
    mesh.name = "BamenMilitarySoldierMesh"
    mesh.data.name = "BamenMilitarySoldierGeometry"
    armature.name = "BamenMilitarySoldierRig"
    armature.data.name = "BamenMilitarySoldierSkeleton"
    for material in mesh.data.materials:
        tune_material(material)

    root = bpy.data.objects.new("BamenMilitarySoldier", None)
    bpy.context.collection.objects.link(root)
    armature.parent = root
    root["creator"] = "BAMEN"
    root["source"] = "https://sketchfab.com/3d-models/free-military-soldier-rigged-e9c56308a67d4a3db62e914fafa4d198"
    root["license"] = "CC BY 4.0"

    for obj in list(bpy.context.scene.objects):
        if obj not in {root, armature, mesh}:
            bpy.data.objects.remove(obj, do_unlink=True)
    return armature, mesh


def save_editable_source() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / "bamen_military_soldier_clean.blend"))


def clear_supplied_pose(armature: bpy.types.Object) -> None:
    armature.data.pose_position = "REST"
    for bone in armature.pose.bones:
        bone.matrix_basis.identity()
    bpy.context.view_layer.update()


def export_glb() -> Path:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    output = ASSET_DIR / "bamen_military_soldier.glb"
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )
    return output


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_preview(mesh: bpy.types.Object) -> Path:
    minimum, maximum = world_bounds([mesh])
    center = (minimum + maximum) * 0.5
    size = maximum - minimum

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.018, 0.024, 0.023)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(size.x, size.z) * 1.3
    camera.location = center + Vector((0.0, -4.5, size.z * 0.06))
    look_at(camera, center)

    key_data = bpy.data.lights.new("KeyLight", "AREA")
    key_data.energy = 900
    key_data.shape = "DISK"
    key_data.size = 4.0
    key = bpy.data.objects.new("KeyLight", key_data)
    bpy.context.collection.objects.link(key)
    key.location = center + Vector((-2.5, -3.0, 3.5))
    look_at(key, center)

    rim_data = bpy.data.lights.new("RimLight", "AREA")
    rim_data.energy = 650
    rim_data.color = (0.2, 0.65, 0.8)
    rim_data.size = 3.0
    rim = bpy.data.objects.new("RimLight", rim_data)
    bpy.context.collection.objects.link(rim)
    rim.location = center + Vector((2.0, 1.5, 2.0))
    look_at(rim, center)

    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    output = PREVIEW_DIR / "bamen_military_soldier.png"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return output


def main() -> None:
    args = arguments()
    source = args.source if args.source.is_absolute() else REPO_ROOT / args.source
    if not source.is_file():
        raise FileNotFoundError(source)
    armature, mesh = prepare_scene(source)
    clear_supplied_pose(armature)
    save_editable_source()
    output = export_glb()
    preview = "skipped" if args.skip_preview else str(render_preview(mesh))
    minimum, maximum = world_bounds([mesh])
    print(
        "BAMEN_SOLDIER_EXPORT "
        f"glb={output} preview={preview} size={tuple(round(value, 4) for value in maximum - minimum)}"
    )


if __name__ == "__main__":
    main()
