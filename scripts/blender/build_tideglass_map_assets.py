"""Convert Majadroid's CC0 construction-site kit into Godot-ready GLBs.

Run with Blender 4.5 or newer:
    blender --background --python scripts/blender/build_tideglass_map_assets.py

Pass ``-- --only building crane-on-ground`` to rebuild selected assets. The
source FBX files remain untouched; each exported scene is centered on the
horizontal plane, grounded at Z=0, and authored in meters for Godot's Y-up
glTF importer.
"""

from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "majadroid_construction_site"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "majadroid_construction_site"
PALETTE_PATH = SOURCE_DIR / "ImphenziaPalette01-256-Gradient.png"


@dataclass(frozen=True)
class AssetSpec:
    slug: str
    source_name: str
    output_name: str
    root_name: str


ASSETS = (
    AssetSpec("building", "Building.fbx", "building.glb", "MajadroidConstructionBuilding"),
    AssetSpec(
        "crane-on-ground",
        "Crane-On-Ground.fbx",
        "crane-on-ground.glb",
        "MajadroidGroundCrane",
    ),
    AssetSpec(
        "containers-office",
        "Containers-Office.fbx",
        "containers-office.glb",
        "MajadroidOfficeContainers",
    ),
    AssetSpec(
        "containers-cargo",
        "Containers-Cargo.fbx",
        "containers-cargo.glb",
        "MajadroidCargoContainers",
    ),
    AssetSpec(
        "construction-materials",
        "Construction-Materials.fbx",
        "construction-materials.glb",
        "MajadroidConstructionMaterials",
    ),
    AssetSpec("fence", "Fence.fbx", "fence.glb", "MajadroidConstructionFence"),
    AssetSpec("ground", "Ground.fbx", "ground.glb", "MajadroidConstructionGround"),
    AssetSpec("road", "Road.fbx", "road.glb", "MajadroidConstructionRoad"),
    AssetSpec(
        "trucks",
        "Trucks.fbx",
        "concrete-truck-red.glb",
        "MajadroidConcreteTruckRed",
    ),
)

SELECTED_MESHES = {
    "containers-office": ("Office Container Stack",),
    "containers-cargo": ("Cargo Container Blue Boxes",),
    "construction-materials": ("Planks Wood V3", "Box Stack Brown", "Barrel"),
    "trucks": ("Concrete Truck Red",),
}


def parse_args() -> set[str]:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--only",
        nargs="+",
        choices=[asset.slug for asset in ASSETS],
        help="Rebuild only the listed asset slugs.",
    )
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(blender_args)
    return set(args.only or [asset.slug for asset in ASSETS])


def require_sources() -> None:
    missing = [asset.source_name for asset in ASSETS if not (SOURCE_DIR / asset.source_name).is_file()]
    if not PALETTE_PATH.is_file():
        missing.append(PALETTE_PATH.name)
    if missing:
        raise FileNotFoundError(f"Missing Majadroid source files: {', '.join(missing)}")


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.images,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def import_fbx(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.fbx(
        filepath=str(path),
        global_scale=1.0,
        use_manual_orientation=False,
        bake_space_transform=False,
        use_image_search=True,
        use_anim=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {path.name}: {result}")

    imported = [obj for obj in bpy.data.objects if obj not in before]
    for obj in list(imported):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            imported.remove(obj)
    if not any(obj.type == "MESH" for obj in imported):
        raise RuntimeError(f"No mesh objects were imported from {path.name}")
    return imported


def select_asset_geometry(
    objects: list[bpy.types.Object],
    spec: AssetSpec,
) -> list[bpy.types.Object]:
    selected_names = SELECTED_MESHES.get(spec.slug)
    if selected_names is None:
        return objects

    selected = [obj for obj in objects if obj.type == "MESH" and obj.name in selected_names]
    found_names = {obj.name for obj in selected}
    if found_names != set(selected_names) or len(selected) != len(selected_names):
        names = ", ".join(sorted(obj.name for obj in objects if obj.type == "MESH"))
        raise RuntimeError(
            f"Expected {selected_names!r} in {spec.source_name}; found: {names}"
        )

    selected_set = set(selected)
    for obj in list(objects):
        if obj not in selected_set:
            bpy.data.objects.remove(obj, do_unlink=True)
    return selected


def adapt_asset_geometry(objects: list[bpy.types.Object], spec: AssetSpec) -> None:
    if spec.slug == "construction-materials":
        offsets = {
            "Box Stack Brown": Vector((0.0, 3.7, 0.0)),
            "Barrel": Vector((0.0, -3.5, 0.0)),
        }
        for obj in objects:
            if obj.name in offsets:
                obj.matrix_world = Matrix.Translation(offsets[obj.name]) @ obj.matrix_world
        bpy.context.view_layer.update()
        return

    if spec.slug != "building":
        return

    # The source tower is authored at realistic floor height but has a 61.5 m
    # footprint. Compress its horizontal axes heavily and its vertical axis
    # lightly so it fits the arena with a playable 2.56 m floor spacing.
    footprint_and_height = Matrix.Diagonal(Vector((0.27, 0.27, 0.8, 1.0)))
    imported_set = set(objects)
    for obj in objects:
        if obj.parent not in imported_set:
            obj.matrix_world = footprint_and_height @ obj.matrix_world
    bpy.context.view_layer.update()


def load_palette() -> bpy.types.Image:
    image = bpy.data.images.get(PALETTE_PATH.name)
    if image is None:
        image = bpy.data.images.load(str(PALETTE_PATH), check_existing=True)
    image.name = PALETTE_PATH.name
    image.filepath = str(PALETTE_PATH)
    image.colorspace_settings.name = "sRGB"
    return image


def ensure_palette_materials(objects: list[bpy.types.Object], palette: bpy.types.Image) -> list[str]:
    warnings: list[str] = []
    materials = {
        material
        for obj in objects
        if obj.type == "MESH"
        for material in obj.data.materials
        if material is not None
    }
    if not materials:
        fallback = bpy.data.materials.new("MajadroidPalette")
        materials.add(fallback)
        for obj in objects:
            if obj.type == "MESH":
                obj.data.materials.append(fallback)
        warnings.append("source had no material slots; assigned MajadroidPalette")

    for index, material in enumerate(sorted(materials, key=lambda item: item.name)):
        material.name = f"MajadroidPalette_{index + 1:02d}"
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        principled = nodes.get("Principled BSDF")
        if principled is None:
            principled = nodes.new("ShaderNodeBsdfPrincipled")
            output = nodes.get("Material Output") or nodes.new("ShaderNodeOutputMaterial")
            links.new(principled.outputs["BSDF"], output.inputs["Surface"])

        texture_nodes = [node for node in nodes if node.type == "TEX_IMAGE"]
        if texture_nodes:
            for node in texture_nodes:
                node.image = palette
            if not any(link.to_node == principled and link.to_socket.name == "Base Color" for link in links):
                links.new(texture_nodes[0].outputs["Color"], principled.inputs["Base Color"])
        else:
            texture = nodes.new("ShaderNodeTexImage")
            texture.name = "MajadroidPaletteTexture"
            texture.label = "CC0 Imphenzia Palette"
            texture.image = palette
            texture.interpolation = "Closest"
            texture.location = (principled.location.x - 320.0, principled.location.y + 80.0)
            links.new(texture.outputs["Color"], principled.inputs["Base Color"])
            warnings.append(f"{material.name} lacked an image node; connected the source palette")
        principled.inputs["Roughness"].default_value = 0.72
        specular = principled.inputs.get("Specular IOR Level") or principled.inputs.get("Specular")
        if specular is not None:
            specular.default_value = 0.3
    return warnings


def mesh_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not corners:
        raise RuntimeError("Cannot calculate bounds without meshes")
    minimum = Vector(
        (min(point.x for point in corners), min(point.y for point in corners), min(point.z for point in corners))
    )
    maximum = Vector(
        (max(point.x for point in corners), max(point.y for point in corners), max(point.z for point in corners))
    )
    return minimum, maximum


def object_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[index] for point in corners) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in corners) for index in range(3)))
    return minimum, maximum


def validate_distinct_mesh_bounds(objects: list[bpy.types.Object], spec: AssetSpec) -> None:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    for first_index, first in enumerate(meshes):
        first_minimum, first_maximum = object_bounds(first)
        first_size = first_maximum - first_minimum
        first_volume = first_size.x * first_size.y * first_size.z
        for second in meshes[first_index + 1 :]:
            second_minimum, second_maximum = object_bounds(second)
            second_size = second_maximum - second_minimum
            second_volume = second_size.x * second_size.y * second_size.z
            overlap = Vector(
                tuple(
                    max(
                        0.0,
                        min(first_maximum[index], second_maximum[index])
                        - max(first_minimum[index], second_minimum[index]),
                    )
                    for index in range(3)
                )
            )
            overlap_volume = overlap.x * overlap.y * overlap.z
            smaller_volume = min(first_volume, second_volume)
            if smaller_volume > 0.0 and overlap_volume / smaller_volume >= 0.8:
                raise RuntimeError(
                    f"{spec.slug} contains high-overlap meshes {first.name!r} and {second.name!r}: "
                    f"ratio={overlap_volume / smaller_volume:.3f}"
                )


def normalize_asset(objects: list[bpy.types.Object], spec: AssetSpec) -> bpy.types.Object:
    minimum, maximum = mesh_bounds(objects)
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
    translation = Matrix.Translation(offset)
    imported_set = set(objects)
    top_level = [obj for obj in objects if obj.parent not in imported_set]
    for obj in top_level:
        obj.matrix_world = translation @ obj.matrix_world

    root = bpy.data.objects.new(spec.root_name, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 1.0
    root["source_creator"] = "Majadroid / Maik Hoffmann"
    root["source_asset"] = "3D House Construction Site Lowpoly"
    root["source_url"] = "https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0"
    root["license"] = "CC0-1.0"
    root["units"] = "meters"
    for obj in top_level:
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    bpy.context.view_layer.update()
    return root


def validate_dimensions(objects: list[bpy.types.Object], spec: AssetSpec) -> Vector:
    minimum, maximum = mesh_bounds(objects)
    dimensions = maximum - minimum
    if min(dimensions) < 0.005:
        raise RuntimeError(f"{spec.slug} has a collapsed dimension: {tuple(dimensions)}")
    if max(dimensions) > 200.0:
        raise RuntimeError(f"{spec.slug} is not in plausible meter scale: {tuple(dimensions)}")
    if abs(minimum.z) > 0.002:
        raise RuntimeError(f"{spec.slug} is not grounded at Z=0 (minimum Z={minimum.z:.6f})")
    return dimensions


def mesh_statistics(objects: list[bpy.types.Object]) -> tuple[int, int, int]:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    materials = {material for obj in meshes for material in obj.data.materials if material is not None}
    return len(meshes), triangles, len(materials)


def export_glb(root: bpy.types.Object, objects: list[bpy.types.Object], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_extras=True,
    )
    if "FINISHED" not in result or not output_path.is_file():
        raise RuntimeError(f"Blender could not export {output_path.name}: {result}")


def verify_glb(output_path: Path, expected_dimensions: Vector) -> tuple[Vector, int]:
    clear_scene()
    configure_scene()
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.gltf(filepath=str(output_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not verify {output_path.name}: {result}")
    imported = [obj for obj in bpy.data.objects if obj not in before]
    minimum, maximum = mesh_bounds(imported)
    dimensions = maximum - minimum
    if any(abs(dimensions[index] - expected_dimensions[index]) > 0.005 for index in range(3)):
        raise RuntimeError(
            f"{output_path.name} changed dimensions during glTF round-trip: "
            f"expected={tuple(expected_dimensions)} actual={tuple(dimensions)}"
        )
    if abs(minimum.z) > 0.005:
        raise RuntimeError(f"{output_path.name} moved off Z=0 during glTF round-trip")

    images = {
        node.image
        for material in bpy.data.materials
        if material.use_nodes
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    if not images or any(image.size[0] == 0 or image.size[1] == 0 for image in images):
        raise RuntimeError(f"{output_path.name} has no readable embedded palette texture")
    if not any(obj.get("license") == "CC0-1.0" for obj in imported):
        raise RuntimeError(f"{output_path.name} lost its CC0 source metadata")
    return dimensions, len(images)


def build_asset(spec: AssetSpec) -> None:
    clear_scene()
    configure_scene()
    objects = import_fbx(SOURCE_DIR / spec.source_name)
    objects = select_asset_geometry(objects, spec)
    raw_minimum, raw_maximum = mesh_bounds(objects)
    adapt_asset_geometry(objects, spec)
    validate_distinct_mesh_bounds(objects, spec)
    palette = load_palette()
    warnings = ensure_palette_materials(objects, palette)
    root = normalize_asset(objects, spec)
    dimensions = validate_dimensions(objects, spec)
    mesh_count, triangle_count, material_count = mesh_statistics(objects)
    output_path = OUTPUT_DIR / spec.output_name
    export_glb(root, objects, output_path)
    verified_dimensions, embedded_image_count = verify_glb(output_path, dimensions)

    raw_dimensions = raw_maximum - raw_minimum
    print(
        "TIDEGLASS_ASSET "
        f"slug={spec.slug} "
        f"raw_m={raw_dimensions.x:.3f}x{raw_dimensions.y:.3f}x{raw_dimensions.z:.3f} "
        f"normalized_m={dimensions.x:.3f}x{dimensions.y:.3f}x{dimensions.z:.3f} "
        f"verified_m={verified_dimensions.x:.3f}x{verified_dimensions.y:.3f}x{verified_dimensions.z:.3f} "
        f"meshes={mesh_count} triangles={triangle_count} materials={material_count} "
        f"embedded_images={embedded_image_count} bytes={output_path.stat().st_size}"
    )
    for warning in warnings:
        print(f"TIDEGLASS_ASSET_WARNING slug={spec.slug} detail={warning}")


def main() -> None:
    selected = parse_args()
    require_sources()
    for spec in ASSETS:
        if spec.slug in selected:
            build_asset(spec)
    print(f"TIDEGLASS_ASSET_PASS built={len(selected)} output={OUTPUT_DIR}")


if __name__ == "__main__":
    main()
