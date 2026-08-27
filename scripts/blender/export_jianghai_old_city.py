"""Export the hand-authored Jianghai Old City Blender scene to runtime GLB."""

from __future__ import annotations

from pathlib import Path
import re
import tempfile

import bpy


REPO_ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = REPO_ROOT / "source_art" / "world" / "jianghai_old_city" / "jianghai_old_city.blend"
GLB_PATH = REPO_ROOT / "assets" / "models" / "jianghai_old_city" / "jianghai_old_city.glb"
REFINERY_DOOR_GLB_PATH = (
    REPO_ROOT / "assets" / "models" / "jianghai_old_city" / "rollershutter_window_03.glb"
)
MAX_RUNTIME_TEXTURE_SIZE = 1024


def flatten_tiled_images() -> int:
    replacements = 0
    for image in list(bpy.data.images):
        if image.type != "IMAGE" or not image.has_data:
            continue
        if image.source != "TILED" and "<UDIM>" not in image.filepath:
            continue
        flattened = bpy.data.images.new(
            f"{image.name}_Flattened",
            width=image.size[0],
            height=image.size[1],
            alpha=image.channels == 4,
        )
        flattened.colorspace_settings.name = image.colorspace_settings.name
        flattened.file_format = "PNG"
        flattened.pixels[:] = image.pixels[:]
        flattened.pack()
        for material in bpy.data.materials:
            if not material.use_nodes:
                continue
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image == image:
                    node.image = flattened
        bpy.data.images.remove(image)
        replacements += 1
    return replacements


def pack_runtime_jpeg(image: bpy.types.Image, cache_dir: Path, index: int) -> None:
    safe_name = re.sub(r"[^A-Za-z0-9_.-]+", "_", image.name).strip("._") or f"image_{index:03d}"
    path = cache_dir / f"{index:03d}_{safe_name}.jpg"
    image.file_format = "JPEG"
    image.filepath_raw = str(path)
    image.save()
    if image.packed_file is not None:
        image.unpack(method="REMOVE")
    image.reload()
    image.pack()
    image.filepath_raw = ""


def optimize_runtime_textures(cache_dir: Path) -> tuple[int, int]:
    resized = 0
    recompressed = 0
    bpy.context.scene.render.image_settings.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 90
    for index, image in enumerate(bpy.data.images):
        if image.type != "IMAGE":
            continue
        if not image.has_data:
            try:
                _ = image.pixels[0]
            except (IndexError, RuntimeError):
                continue
        width, height = image.size
        longest = max(width, height)
        should_optimize = longest > MAX_RUNTIME_TEXTURE_SIZE
        if should_optimize:
            factor = MAX_RUNTIME_TEXTURE_SIZE / longest
            image.scale(max(1, round(width * factor)), max(1, round(height * factor)))
            resized += 1
            print(f"JIANGHAI_TEXTURE_LIMIT name={image.name!r} size={image.size[0]}x{image.size[1]}")
        if should_optimize:
            pack_runtime_jpeg(image, cache_dir, index)
            recompressed += 1
            print(f"JIANGHAI_TEXTURE_JPEG name={image.name!r} quality=90")
    return resized, recompressed


def scene_statistics() -> tuple[int, int, int]:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    triangles = 0
    materials: set[str] = set()
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        materials.update(material.name for material in obj.data.materials if material is not None)
    return len(meshes), triangles, len(materials)


def export_refinery_door() -> None:
    source = bpy.data.objects.get("JianghaiArtPass_EastShutter00")
    if source is None or source.type != "MESH":
        raise RuntimeError("The authored rollershutter source mesh is missing")

    duplicate = source.copy()
    duplicate.data = source.data
    duplicate.name = "JianghaiRollerShutterDoor"
    bpy.context.scene.collection.objects.link(duplicate)
    duplicate.location = (0.0, 0.0, 0.0)
    duplicate.rotation_euler = (0.0, 0.0, 0.0)
    duplicate.scale = (1.0, 1.0, 1.0)
    bpy.ops.object.select_all(action="DESELECT")
    duplicate.select_set(True)
    bpy.context.view_layer.objects.active = duplicate
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(REFINERY_DOOR_GLB_PATH),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_yup=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
        )
    finally:
        bpy.data.objects.remove(duplicate, do_unlink=True)


def main() -> None:
    if Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
        raise RuntimeError(f"Open the authored scene before export: {BLEND_PATH}")
    GLB_PATH.parent.mkdir(parents=True, exist_ok=True)
    flattened = flatten_tiled_images()
    with tempfile.TemporaryDirectory(prefix="jianghai-runtime-textures-") as cache:
        resized, recompressed = optimize_runtime_textures(Path(cache))
        forbidden_fonts = [font.name for font in bpy.data.fonts if font.name != "Bfont"]
        if forbidden_fonts:
            raise RuntimeError(f"Source font data must not ship: {forbidden_fonts}")
        bpy.ops.file.pack_all()
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
        export_refinery_door()
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.gltf(
            filepath=str(GLB_PATH),
            export_format="GLB",
            export_apply=True,
            export_yup=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
        )
    meshes, triangles, materials = scene_statistics()
    print(
        "JIANGHAI_EXPORT_COMPLETE "
        f"flattened_udim={flattened} resized_textures={resized} recompressed_textures={recompressed} "
        f"meshes={meshes} triangles={triangles} materials={materials} "
        f"blend={BLEND_PATH} glb={GLB_PATH} refinery_door_glb={REFINERY_DOOR_GLB_PATH}"
    )


if __name__ == "__main__":
    main()
