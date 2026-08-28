"""Build the Steel Tide reloadable AK-74N DCC derivative.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup --python scripts/blender/build_reloadable_ak74.py

The source is the finished CC0 Quaternius AK silhouette already tracked in the
repository.  This DCC pass preserves its materials and topology while separating
the authored magazine faces into a mechanism-ready mesh node.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_GLB = (
    REPO_ROOT / "assets" / "models" / "quaternius_ultimate_guns" / "ak74.glb"
)
OUTPUT_GLB = (
    REPO_ROOT / "assets" / "models" / "steel_tide_ak74" / "ak74_reloadable.glb"
)
OUTPUT_BLEND = (
    REPO_ROOT / "source_art" / "reloadable_weapons" / "ak74_reloadable.blend"
)
PREVIEW_PATH = REPO_ROOT / "build" / "art-previews" / "ak74_reloadable.png"

SOURCE_OBJECT = "AssaultRifle_4"
SOURCE_VERTEX_COUNT = 2682
SOURCE_TRIANGLE_COUNT = 1382
MAGAZINE_TRIANGLE_COUNT = 227
BODY_TRIANGLE_COUNT = SOURCE_TRIANGLE_COUNT - MAGAZINE_TRIANGLE_COUNT
SOURCE_MATERIALS = ("DarkMetal", "Metal", "Wood", "Black", "DarkWood")

SOURCE_CREATOR = "Quaternius"
SOURCE_URL = "https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F"
SOURCE_LICENSE = "CC0-1.0"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def world_face_center(
    obj: bpy.types.Object,
    polygon: bpy.types.MeshPolygon,
) -> Vector:
    result = Vector()
    for vertex_index in polygon.vertices:
        result += obj.matrix_world @ obj.data.vertices[vertex_index].co
    return result / len(polygon.vertices)


def is_magazine_face(
    obj: bpy.types.Object,
    polygon: bpy.types.MeshPolygon,
) -> bool:
    if obj.data.materials[polygon.material_index].name != "Black":
        return False
    center = world_face_center(obj, polygon)
    return 0.65 <= center.x <= 1.80 and center.z < 0.15


def import_and_validate_source() -> bpy.types.Object:
    if not SOURCE_GLB.is_file():
        raise RuntimeError(f"Missing tracked AK source: {SOURCE_GLB}")
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    source = bpy.data.objects.get(SOURCE_OBJECT)
    if source is None or source.type != "MESH":
        raise RuntimeError(f"Source object {SOURCE_OBJECT!r} is unavailable.")
    materials = tuple(material.name for material in source.data.materials)
    if materials != SOURCE_MATERIALS:
        raise RuntimeError(f"Unexpected AK material layout: {materials}")
    if (
        len(source.data.vertices) != SOURCE_VERTEX_COUNT
        or len(source.data.polygons) != SOURCE_TRIANGLE_COUNT
    ):
        raise RuntimeError(
            "Unexpected AK source topology: "
            f"vertices={len(source.data.vertices)} "
            f"triangles={len(source.data.polygons)}"
        )
    source["source_creator"] = SOURCE_CREATOR
    source["source_url"] = SOURCE_URL
    source["source_license"] = SOURCE_LICENSE
    source["source_sha256"] = hashlib.sha256(SOURCE_GLB.read_bytes()).hexdigest()
    return source


def separate_magazine(
    source: bpy.types.Object,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in source.data.polygons:
        polygon.select = is_magazine_face(source, polygon)
    selected = sum(1 for polygon in source.data.polygons if polygon.select)
    if selected != MAGAZINE_TRIANGLE_COUNT:
        raise RuntimeError(
            f"Magazine selection drifted: {selected} != {MAGAZINE_TRIANGLE_COUNT}"
        )

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    separated = [
        obj
        for obj in bpy.context.selected_objects
        if obj.type == "MESH" and obj != source
    ]
    if len(separated) != 1:
        raise RuntimeError(f"Expected one separated magazine, found {len(separated)}")
    magazine = separated[0]
    source.name = "WeaponBodyGeometry"
    source.data.name = "WeaponBodyMesh"
    magazine.name = "MagazineGeometry"
    magazine.data.name = "MagazineMesh"
    if (
        len(source.data.polygons) != BODY_TRIANGLE_COUNT
        or len(magazine.data.polygons) != MAGAZINE_TRIANGLE_COUNT
    ):
        raise RuntimeError(
            "Separated topology mismatch: "
            f"body={len(source.data.polygons)} magazine={len(magazine.data.polygons)}"
        )
    magazine["mechanism_role"] = "detachable_magazine"
    return source, magazine


def build_runtime_asset() -> bpy.types.Object:
    source = import_and_validate_source()
    body, magazine = separate_magazine(source)
    body_world = body.matrix_world.copy()
    magazine_world = magazine.matrix_world.copy()

    root = bpy.data.objects.new("SteelTideReloadableAK74", None)
    bpy.context.collection.objects.link(root)
    root["source_creator"] = SOURCE_CREATOR
    root["source_url"] = SOURCE_URL
    root["source_license"] = SOURCE_LICENSE
    root["dcc_adaptation"] = "Separated finished source magazine faces for reload"
    for obj, world_transform in (
        (body, body_world),
        (magazine, magazine_world),
    ):
        obj.parent = root
        obj.matrix_world = world_transform

    imported_roots = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "EMPTY" and obj != root and obj.name == "RootNode"
    ]
    for imported_root in imported_roots:
        bpy.data.objects.remove(imported_root, do_unlink=True)

    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    if {obj.name for obj in meshes} != {"WeaponBodyGeometry", "MagazineGeometry"}:
        raise RuntimeError(f"Unexpected runtime mesh contract: {[obj.name for obj in meshes]}")
    if sum(len(obj.data.polygons) for obj in meshes) != SOURCE_TRIANGLE_COUNT:
        raise RuntimeError("DCC split did not preserve the complete source topology.")
    return root


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (root, *root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_yup=True,
        export_attributes=True,
        export_cameras=False,
        export_lights=False,
    )


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


def render_preview(root: bpy.types.Object) -> None:
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = Vector((1.05, -9.0, 0.03))
    camera.rotation_euler = (
        Vector((1.05, 0.0, 0.03)) - camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.25
    scene.camera = camera
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    root = build_runtime_asset()
    export_asset(root)
    save_source()
    render_preview(root)
    print(
        "RELOADABLE_AK74_EXPORT "
        f"source_sha256={hashlib.sha256(SOURCE_GLB.read_bytes()).hexdigest()} "
        f"vertices={SOURCE_VERTEX_COUNT} triangles={SOURCE_TRIANGLE_COUNT} "
        f"body_triangles={BODY_TRIANGLE_COUNT} "
        f"magazine_triangles={MAGAZINE_TRIANGLE_COUNT} "
        f"glb={OUTPUT_GLB} blend={OUTPUT_BLEND} preview={PREVIEW_PATH}"
    )


if __name__ == "__main__":
    main()
