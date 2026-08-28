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
MAX_DETAIL_TEXTURE_SIZE = 512
MAX_SMALL_FURNITURE_TEXTURE_SIZE = 256
MAX_RUNTIME_GLB_SIZE_BYTES = 99_000_000
DETAIL_TEXTURE_TOKENS = (
    "barrel_03",
    "coffeecart_01",
    "concrete_road_barrier",
    "exterior_aircon_unit",
    "hand_truck",
    "modular_urban_apartments_facade",
    "old_military_crate",
    "plastic_crate_02",
    "rollershutter_window_03",
    "security_camera_01",
    "television_02",
    "trashbag",
    "utility_box_01",
    "wicker_basket_01",
)
SMALL_FURNITURE_TEXTURE_TOKENS = (
    "chinese_stool",
    "chinese_tea_table",
)
RUNTIME_EMISSION_STRENGTHS = {
    "JianghaiNeonGold": 0.75,
    "JianghaiNeonRed": 0.88,
    "JianghaiNeonTeal": 0.80,
    "JianghaiLampGlass": 1.65,
    "JianghaiTerminalScreenRed": 4.00,
}
FLOATING_MARKET_SIGN_OBJECTS = (
    "OldCityMarketSignBacking",
    "OldCityMarketSignText",
    "OldCityMarketBuySignBacking",
    "OldCityMarketBuySignText",
    "OldCityMarketPawnSignBacking",
    "OldCityMarketPawnSignText",
)
RETIRED_CUSTOM_PROPERTY_KEYS = (
    "blenderkit_old_brick_factory",
)
RUINED_FACTORY_OBJECTS = (
    "RedStarFactoryMainBuilding",
    "RedStarLoadingBayWest",
    "RedStarLoadingBayEast",
)
FACTORY_BUILDING_LAYOUT = (
    ("JianghaiCleared_FactoryOfficeWest", "JianghaiCleared_MarketShop00", (66.0, -7.0, 0.04), (0.48, 0.58, 0.78)),
    ("JianghaiCleared_FactoryWorkshopWest", "JianghaiCleared_MarketShop01", (75.5, -7.0, 0.04), (0.65, 0.80, 0.92)),
    ("JianghaiCleared_FactoryAdmin", "JianghaiCleared_MarketShop02", (85.5, -7.0, 0.04), (0.52, 0.62, 0.88)),
    ("JianghaiCleared_FactoryWorkshopEast", "JianghaiCleared_MarketShop03", (95.5, -7.0, 0.04), (0.65, 0.82, 0.96)),
    ("JianghaiCleared_FactoryOfficeEast", "JianghaiCleared_MarketShop04", (105.0, -7.0, 0.04), (0.48, 0.58, 0.82)),
)


def tune_runtime_emissions() -> int:
    tuned = 0
    for material_name, strength in RUNTIME_EMISSION_STRENGTHS.items():
        material = bpy.data.materials.get(material_name)
        if material is None or not material.use_nodes or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            if node.type != "BSDF_PRINCIPLED":
                continue
            emission_strength = node.inputs.get("Emission Strength")
            if emission_strength is None:
                continue
            emission_strength.default_value = strength
            tuned += 1
    return tuned


def tune_runtime_materials() -> int:
    material = bpy.data.materials.get("JianghaiSignBacking")
    if material is None or not material.use_nodes or material.node_tree is None:
        return 0
    tuned = 0
    for node in material.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        metallic = node.inputs.get("Metallic")
        roughness = node.inputs.get("Roughness")
        if metallic is not None:
            metallic.default_value = 0.10
        if roughness is not None:
            roughness.default_value = 0.70
        tuned += 1
    return tuned


def remove_floating_market_signs() -> int:
    removed = 0
    for object_name in FLOATING_MARKET_SIGN_OBJECTS:
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1
    return removed


def remove_retired_asset_metadata() -> int:
    removed = 0
    blocks = (
        *bpy.data.objects,
        *bpy.data.meshes,
        *bpy.data.materials,
        *bpy.data.images,
        *bpy.data.collections,
        *bpy.data.scenes,
        *bpy.data.worlds,
        *bpy.data.node_groups,
    )
    for block in blocks:
        for key in tuple(block.keys()):
            if str(key).lower() not in RETIRED_CUSTOM_PROPERTY_KEYS:
                continue
            del block[key]
            removed += 1
    return removed


def rebuild_factory_frontage() -> tuple[int, int]:
    removed = 0
    for obj in list(bpy.data.objects):
        if (
            obj.name in RUINED_FACTORY_OBJECTS
            or obj.name.startswith("RedStarMainFacade_")
            or obj.name.startswith("RedStarMainFacade_Cornice_")
            or obj.name.startswith("FactoryMarqueeBracket")
        ):
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1

    factory_root = bpy.data.objects.get("RedStarElectronicsFactory")
    if factory_root is None:
        raise RuntimeError("The Red Star factory root is missing")

    rebuilt = 0
    for object_name, source_name, location, scale in FACTORY_BUILDING_LAYOUT:
        source = bpy.data.objects.get(source_name)
        if source is None or source.type != "MESH":
            raise RuntimeError(f"Factory replacement source is missing: {source_name}")
        replacement = bpy.data.objects.get(object_name)
        if replacement is None:
            replacement = source.copy()
            replacement.data = source.data
            replacement.name = object_name
            bpy.context.scene.collection.objects.link(replacement)
            rebuilt += 1
        replacement.parent = factory_root
        replacement.location = location
        replacement.rotation_euler = (0.0, 0.0, 0.0)
        replacement.scale = scale
        replacement["district_role"] = "cleared_cc0_factory_frontage"

    sign_backing = bpy.data.objects.get("RedStarFactoryMarqueeBacking")
    sign_text = bpy.data.objects.get("RedStarFactoryMarqueeText")
    if sign_backing is not None:
        sign_backing.location = (85.5, -3.90, 7.35)
    if sign_text is not None:
        sign_text.location = (85.5, -3.81, 7.35)
    return removed, rebuilt


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


def runtime_texture_limit(image: bpy.types.Image) -> int:
    normalized_name = image.name.lower()
    if any(token in normalized_name for token in SMALL_FURNITURE_TEXTURE_TOKENS):
        return MAX_SMALL_FURNITURE_TEXTURE_SIZE
    if any(token in normalized_name for token in DETAIL_TEXTURE_TOKENS):
        return MAX_DETAIL_TEXTURE_SIZE
    return MAX_RUNTIME_TEXTURE_SIZE


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
        target_size = runtime_texture_limit(image)
        should_optimize = longest > target_size
        if should_optimize:
            factor = target_size / longest
            image.scale(max(1, round(width * factor)), max(1, round(height * factor)))
            resized += 1
            print(
                f"JIANGHAI_TEXTURE_LIMIT name={image.name!r} "
                f"size={image.size[0]}x{image.size[1]} target={target_size}"
            )
        if should_optimize:
            pack_runtime_jpeg(image, cache_dir, index)
            recompressed += 1
            print(f"JIANGHAI_TEXTURE_JPEG name={image.name!r} quality=90")
    return resized, recompressed


def scene_statistics() -> tuple[int, int, int, int]:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    materials: set[str] = set()
    for obj in meshes:
        materials.update(material.name for material in obj.data.materials if material is not None)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_objects = 0
    evaluated_triangles = 0
    for instance in depsgraph.object_instances:
        obj = instance.object
        # The depsgraph also exposes the source CURVE for every evaluated
        # curve mesh. Count MESH only so each exported surface is counted once.
        if obj.type != "MESH":
            continue
        mesh = obj.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        if mesh is None:
            continue
        try:
            mesh.calc_loop_triangles()
            evaluated_triangles += len(mesh.loop_triangles)
            evaluated_objects += 1
        finally:
            obj.to_mesh_clear()
    return len(meshes), evaluated_objects, evaluated_triangles, len(materials)


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
    tuned_emissions = tune_runtime_emissions()
    tuned_materials = tune_runtime_materials()
    removed_floating_signs = remove_floating_market_signs()
    removed_retired_metadata = remove_retired_asset_metadata()
    removed_factory_shells, rebuilt_factory_buildings = rebuild_factory_frontage()
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
            use_selection=True,
            export_apply=True,
            export_yup=True,
            export_materials="EXPORT",
            export_cameras=False,
            export_lights=False,
            export_extras=True,
        )
    glb_size = GLB_PATH.stat().st_size
    if glb_size > MAX_RUNTIME_GLB_SIZE_BYTES:
        raise RuntimeError(
            f"Jianghai runtime GLB exceeds the public-repository budget: "
            f"{glb_size} > {MAX_RUNTIME_GLB_SIZE_BYTES}"
        )
    meshes, evaluated_objects, evaluated_triangles, materials = scene_statistics()
    print(
        "JIANGHAI_EXPORT_COMPLETE "
        f"tuned_emissions={tuned_emissions} tuned_materials={tuned_materials} "
        f"removed_floating_signs={removed_floating_signs} "
        f"removed_retired_metadata={removed_retired_metadata} flattened_udim={flattened} "
        f"removed_factory_shells={removed_factory_shells} "
        f"rebuilt_factory_buildings={rebuilt_factory_buildings} "
        f"resized_textures={resized} recompressed_textures={recompressed} "
        f"meshes={meshes} evaluated_objects={evaluated_objects} "
        f"evaluated_triangles={evaluated_triangles} materials={materials} glb_bytes={glb_size} "
        f"blend={BLEND_PATH} glb={GLB_PATH} refinery_door_glb={REFINERY_DOOR_GLB_PATH}"
    )


if __name__ == "__main__":
    main()
