from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

import bpy


REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_GLB = REPO_ROOT / "assets/models/tide_hunter_monster/tide_hunter_monster.glb"
OUTPUT_BLEND = REPO_ROOT / "source_art/third_party/tide_hunter_monster/tide_hunter_monster.blend"
OUTPUT_TEXTURE_DIR = OUTPUT_GLB.parent
TEXTURE_NAMES = {
    "albedo": "test_StingrayPBS1SG_AlbedoTransparency.png",
    "metallic": "test_StingrayPBS1SG_MetallicSmoothness.png",
    "normal": "test_StingrayPBS1SG_Normal.png",
}
ACTION_FILES = {
    "idle": "Idle.fbx",
    "walk": "Walk.fbx",
    "run": "Run.fbx",
}


def parse_source_dir() -> Path:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--source-dir",
        type=Path,
        default=os.environ.get("TIDE_HUNTER_SOURCE_DIR"),
        help="Extracted OpenGameArt Poses directory containing Idle.fbx and UnityTexture.",
    )
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])
    if args.source_dir is None:
        raise RuntimeError("Pass --source-dir or set TIDE_HUNTER_SOURCE_DIR.")
    source_dir = args.source_dir.resolve()
    for filename in ACTION_FILES.values():
        require_file(source_dir / filename)
    for filename in TEXTURE_NAMES.values():
        require_file(source_dir / "UnityTexture" / filename)
    return source_dir


def require_file(path: Path) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.actions,
        bpy.data.armatures,
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
    ):
        for block in list(collection):
            collection.remove(block)


def import_fbx(path: Path) -> tuple[list[bpy.types.Object], list[bpy.types.Action]]:
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    actions = [action for action in bpy.data.actions if action not in before_actions]
    return objects, actions


def deform_armature(objects: list[bpy.types.Object]) -> bpy.types.Object:
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    if not armatures:
        raise RuntimeError("Monster FBX has no armature.")
    armature = max(armatures, key=lambda obj: len(obj.data.bones))
    if len(armature.data.bones) < 60:
        raise RuntimeError(f"Expected the deform rig, found only {len(armature.data.bones)} bones.")
    return armature


def deform_mesh(objects: list[bpy.types.Object], armature: bpy.types.Object) -> bpy.types.Object:
    candidates = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        if any(modifier.type == "ARMATURE" and modifier.object == armature for modifier in obj.modifiers):
            candidates.append(obj)
    if len(candidates) != 1:
        raise RuntimeError(f"Expected one skinned monster mesh, found {len(candidates)}.")
    return candidates[0]


def active_action(armature: bpy.types.Object) -> bpy.types.Action:
    if armature.animation_data is None or armature.animation_data.action is None:
        raise RuntimeError(f"Armature {armature.name} has no active action.")
    return armature.animation_data.action


def copy_action(source: bpy.types.Action, name: str) -> bpy.types.Action:
    action = source.copy()
    action.name = name
    action.asset_mark()
    action["loop"] = True
    return action


def remove_import(objects: list[bpy.types.Object], actions: list[bpy.types.Action]) -> None:
    for obj in reversed(objects):
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in actions:
        if action.name in bpy.data.actions:
            bpy.data.actions.remove(action)


def load_texture(path: Path, colorspace: str) -> bpy.types.Image:
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = path.stem
    image.colorspace_settings.name = colorspace
    if image.size[0] > 1024 or image.size[1] > 1024:
        image.scale(1024, 1024)
    return image


def export_texture(image: bpy.types.Image, source_name: str) -> None:
    OUTPUT_TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    output_path = OUTPUT_TEXTURE_DIR / f"tide_hunter_monster_{source_name}"
    source_path = image.filepath_raw
    source_format = image.file_format
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()
    image.filepath_raw = source_path
    image.file_format = source_format


def build_material(source_dir: Path) -> bpy.types.Material:
    texture_dir = source_dir / "UnityTexture"
    albedo = load_texture(texture_dir / TEXTURE_NAMES["albedo"], "sRGB")
    metallic_smoothness = load_texture(texture_dir / TEXTURE_NAMES["metallic"], "Non-Color")
    normal = load_texture(texture_dir / TEXTURE_NAMES["normal"], "Non-Color")
    export_texture(albedo, TEXTURE_NAMES["albedo"])
    export_texture(metallic_smoothness, TEXTURE_NAMES["metallic"])
    export_texture(normal, TEXTURE_NAMES["normal"])

    material = bpy.data.materials.new("TideHunterPBR")
    material.use_nodes = True
    material.diffuse_color = (0.24, 0.12, 0.095, 1.0)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (680, 0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (380, 0)
    principled.inputs["Roughness"].default_value = 0.72
    principled.inputs["Specular IOR Level"].default_value = 0.34
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    albedo_node = nodes.new("ShaderNodeTexImage")
    albedo_node.name = "TideHunterAlbedo"
    albedo_node.image = albedo
    albedo_node.location = (-620, 180)
    links.new(albedo_node.outputs["Color"], principled.inputs["Base Color"])

    metallic_node = nodes.new("ShaderNodeTexImage")
    metallic_node.name = "TideHunterMetallicSmoothness"
    metallic_node.image = metallic_smoothness
    metallic_node.location = (-620, -40)
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.location = (-360, -40)
    invert = nodes.new("ShaderNodeMath")
    invert.operation = "SUBTRACT"
    invert.inputs[0].default_value = 1.0
    invert.location = (-80, -130)
    links.new(metallic_node.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Red"], principled.inputs["Metallic"])
    links.new(metallic_node.outputs["Alpha"], invert.inputs[1])
    links.new(invert.outputs["Value"], principled.inputs["Roughness"])

    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.name = "TideHunterNormal"
    normal_node.image = normal
    normal_node.location = (-620, -330)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.82
    normal_map.location = (-260, -330)
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    return material


def retain_base_visual(
    objects: list[bpy.types.Object],
    armature: bpy.types.Object,
    mesh: bpy.types.Object,
) -> bpy.types.Object:
    root = bpy.data.objects.new("TideHunterMonster", None)
    bpy.context.collection.objects.link(root)
    armature_world = armature.matrix_world.copy()
    mesh_world = mesh.matrix_world.copy()
    armature.parent = root
    mesh.parent = armature
    armature.matrix_world = armature_world
    mesh.matrix_world = mesh_world
    for obj in reversed(objects):
        if obj not in {armature, mesh} and obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    armature.name = "TideHunterRig"
    armature.data.name = "TideHunterSkeleton"
    mesh.name = "TideHunterMesh"
    mesh.data.name = "TideHunterMeshData"
    return root


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_nla_strips=False,
        export_optimize_animation_size=True,
        export_force_sampling=True,
        export_frame_range=False,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


def main() -> None:
    source_dir = parse_source_dir()
    clear_scene()
    bpy.context.scene.render.fps = 30
    bpy.context.scene.render.fps_base = 1.0

    base_objects, base_actions = import_fbx(source_dir / ACTION_FILES["idle"])
    armature = deform_armature(base_objects)
    mesh = deform_mesh(base_objects, armature)
    actions = {"idle": copy_action(active_action(armature), "idle")}
    root = retain_base_visual(base_objects, armature, mesh)
    for action in base_actions:
        if action != actions["idle"] and action.name in bpy.data.actions:
            bpy.data.actions.remove(action)

    for name in ("walk", "run"):
        imported_objects, imported_actions = import_fbx(source_dir / ACTION_FILES[name])
        source_armature = deform_armature(imported_objects)
        actions[name] = copy_action(active_action(source_armature), name)
        remove_import(imported_objects, imported_actions)

    armature.animation_data_create()
    armature.animation_data.action = actions["idle"]
    mesh.data.materials.clear()
    mesh.data.materials.append(build_material(source_dir))
    mesh["source_creator"] = "HorrorGameMaker.com"
    mesh["source_license"] = "CC0 1.0 Universal"
    root["source_url"] = "https://opengameart.org/content/3d-horror-game-monster"
    root["acquired"] = "2026-08-20"
    save_source()
    export_asset(root)
    print(
        "TIDE_HUNTER_EXPORT "
        f"glb={OUTPUT_GLB} blend={OUTPUT_BLEND} "
        f"vertices={len(mesh.data.vertices)} actions={sorted(actions)}"
    )


if __name__ == "__main__":
    main()
