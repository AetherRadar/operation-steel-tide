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
import math
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "majadroid_construction_site"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "majadroid_construction_site"
PALETTE_PATH = SOURCE_DIR / "ImphenziaPalette01-256-Gradient.png"
TREY_SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "trey_modular_industrial"
TREY_PALETTE_PATH = TREY_SOURCE_DIR / "PacificNorthwestGradientAtlas.png"
TREY_STAIR_PATH = TREY_SOURCE_DIR / "Meshes" / "Details" / "IndStairsWideFull.fbx"
TREY_PLATFORM_PATH = (
    TREY_SOURCE_DIR / "Meshes" / "Floors" / "IndFloorGreyPlatformFull.fbx"
)


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
    for path in (TREY_PALETTE_PATH, TREY_STAIR_PATH, TREY_PLATFORM_PATH):
        if not path.is_file():
            missing.append(str(path.relative_to(REPO_ROOT)))
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


def open_office_platform_doorway(objects: list[bpy.types.Object], spec: AssetSpec) -> None:
    if spec.slug != "containers-office":
        return

    meshes = [obj for obj in objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(
            f"containers-office doorway edit expects one mesh; found {len(meshes)}"
        )

    # The upper office door at the ramp landing is modeled as a closed leaf over a
    # solid container wall. Cut the full player-sized opening in Blender so the
    # rendered doorway and the authored concave collision share the same topology.
    doorway_minimum = Vector((0.85, -2.10, 2.55))
    doorway_maximum = Vector((1.65, -0.25, 5.05))
    office = meshes[0]
    inverse = office.matrix_world.inverted()
    mesh = office.data
    editable = bmesh.new()
    editable.from_mesh(mesh)
    editable.faces.ensure_lookup_table()
    for axis, boundary in (
        (Vector((0.0, 1.0, 0.0)), doorway_minimum.y),
        (Vector((0.0, 1.0, 0.0)), doorway_maximum.y),
        (Vector((0.0, 0.0, 1.0)), doorway_minimum.z),
        (Vector((0.0, 0.0, 1.0)), doorway_maximum.z),
    ):
        world_point = Vector((0.0, 0.0, 0.0))
        if axis.y != 0.0:
            world_point.y = boundary
        else:
            world_point.z = boundary
        bmesh.ops.bisect_plane(
            editable,
            geom=list(editable.verts) + list(editable.edges) + list(editable.faces),
            dist=0.00001,
            plane_co=inverse @ world_point,
            plane_no=office.matrix_world.to_3x3().transposed() @ axis,
            clear_inner=False,
            clear_outer=False,
        )

    doorway_faces = []
    for face in editable.faces:
        world_center = office.matrix_world @ face.calc_center_median()
        if (
            doorway_minimum.x <= world_center.x <= doorway_maximum.x
            and doorway_minimum.y < world_center.y < doorway_maximum.y
            and doorway_minimum.z < world_center.z < doorway_maximum.z
        ):
            doorway_faces.append(face)
    if len(doorway_faces) < 8:
        editable.free()
        raise RuntimeError(
            f"Upper office doorway edit found too little wall geometry: {len(doorway_faces)} faces"
        )
    bmesh.ops.delete(editable, geom=doorway_faces, context="FACES")
    loose_vertices = [vertex for vertex in editable.verts if not vertex.link_edges]
    if loose_vertices:
        bmesh.ops.delete(editable, geom=loose_vertices, context="VERTS")
    editable.to_mesh(mesh)
    editable.free()
    mesh.update()
    bpy.context.view_layer.update()

    depsgraph = bpy.context.evaluated_depsgraph_get()
    for vertical in (2.88, 3.72, 4.56):
        hit, *_ = bpy.context.scene.ray_cast(
            depsgraph,
            Vector((1.82, -1.18, vertical)),
            Vector((-1.0, 0.0, 0.0)),
            distance=0.94,
        )
        if hit:
            raise RuntimeError(
                f"Upper office doorway remains blocked at vertical={vertical:.2f}"
            )

    floor_hit, *_ = bpy.context.scene.ray_cast(
        depsgraph,
        Vector((0.52, -1.18, 3.70)),
        Vector((0.0, 0.0, -1.0)),
        distance=1.20,
    )
    if not floor_hit:
        raise RuntimeError("Upper office doorway edit removed the playable room floor")


def cut_construction_tower_stairwell(
    objects: list[bpy.types.Object],
    spec: AssetSpec,
) -> None:
    if spec.slug != "building":
        return

    building_meshes = [
        obj for obj in objects if obj.type == "MESH" and len(obj.data.polygons) > 100
    ]
    if len(building_meshes) != 1:
        raise RuntimeError(
            f"construction tower stairwell edit expects one structural mesh; "
            f"found {len(building_meshes)}"
        )

    # The source tower already reserves a tiny repeated service opening but ships
    # without vertical circulation. Widen that shaft through the upper slabs and
    # column fragments in Blender before installing the authored switchback stairs.
    # Start below the first landing.  The source tower carries low structural
    # fragments through this corner as well as the repeated upper-floor slabs;
    # leaving them in place blocks the first switchback even when every upper
    # stairwell opening is clear.
    shaft_minimum = Vector((1.45, -5.70, 0.10))
    shaft_maximum = Vector((5.00, -1.30, 46.42))
    tower = building_meshes[0]
    inverse = tower.matrix_world.inverted()
    editable = bmesh.new()
    editable.from_mesh(tower.data)
    for axis, boundary in (
        (Vector((1.0, 0.0, 0.0)), shaft_minimum.x),
        (Vector((1.0, 0.0, 0.0)), shaft_maximum.x),
        (Vector((0.0, 1.0, 0.0)), shaft_minimum.y),
        (Vector((0.0, 1.0, 0.0)), shaft_maximum.y),
        (Vector((0.0, 0.0, 1.0)), shaft_minimum.z),
        (Vector((0.0, 0.0, 1.0)), shaft_maximum.z),
    ):
        world_point = Vector((0.0, 0.0, 0.0))
        if axis.x:
            world_point.x = boundary
        elif axis.y:
            world_point.y = boundary
        else:
            world_point.z = boundary
        bmesh.ops.bisect_plane(
            editable,
            geom=list(editable.verts) + list(editable.edges) + list(editable.faces),
            dist=0.00001,
            plane_co=inverse @ world_point,
            plane_no=tower.matrix_world.to_3x3().transposed() @ axis,
            clear_inner=False,
            clear_outer=False,
        )

    removed_faces = []
    for face in editable.faces:
        center = tower.matrix_world @ face.calc_center_median()
        if (
            shaft_minimum.x < center.x < shaft_maximum.x
            and shaft_minimum.y < center.y < shaft_maximum.y
            and shaft_minimum.z < center.z < shaft_maximum.z
        ):
            removed_faces.append(face)
    if len(removed_faces) < 150:
        editable.free()
        raise RuntimeError(
            f"Construction tower stairwell removed too little structure: "
            f"{len(removed_faces)} faces"
        )
    bmesh.ops.delete(editable, geom=removed_faces, context="FACES")
    loose_vertices = [vertex for vertex in editable.verts if not vertex.link_edges]
    if loose_vertices:
        bmesh.ops.delete(editable, geom=loose_vertices, context="VERTS")
    editable.to_mesh(tower.data)
    editable.free()
    tower.data.update()
    bpy.context.view_layer.update()

    depsgraph = bpy.context.evaluated_depsgraph_get()
    for level in range(1, 19):
        floor_height = 2.56 * level
        for x, y in (
            (3.25, -5.25),
            (1.80, -4.50),
            (3.25, -3.50),
            (4.70, -2.50),
            (3.25, -1.75),
        ):
            hit, *_ = bpy.context.scene.ray_cast(
                depsgraph,
                Vector((x, y, floor_height + 0.20)),
                Vector((0.0, 0.0, -1.0)),
                distance=0.48,
            )
            if hit:
                raise RuntimeError(
                    f"Construction tower stairwell remains blocked at level={level} "
                    f"sample=({x:.2f},{y:.2f})"
                )
        floor_hit, *_ = bpy.context.scene.ray_cast(
            depsgraph,
            Vector((0.0, 0.0, floor_height + 0.20)),
            Vector((0.0, 0.0, -1.0)),
            distance=0.48,
        )
        if not floor_hit:
            raise RuntimeError(
                f"Construction tower stairwell edit damaged floor {level} outside the shaft"
            )


def load_trey_palette() -> bpy.types.Image:
    image = bpy.data.images.get(TREY_PALETTE_PATH.name)
    if image is None:
        image = bpy.data.images.load(str(TREY_PALETTE_PATH), check_existing=True)
    image.name = TREY_PALETTE_PATH.name
    image.filepath = str(TREY_PALETTE_PATH)
    image.colorspace_settings.name = "sRGB"
    return image


def import_trey_module(path: Path, expected_mesh: str) -> bpy.types.Object:
    imported = import_fbx(path)
    selected = [
        obj for obj in imported if obj.type == "MESH" and expected_mesh in obj.name
    ]
    if len(selected) != 1:
        raise RuntimeError(
            f"Expected one {expected_mesh} mesh in {path.name}; found "
            f"{[obj.name for obj in imported if obj.type == 'MESH']}"
        )
    module = selected[0]
    for obj in imported:
        if obj != module:
            bpy.data.objects.remove(obj, do_unlink=True)
    return module


def open_trey_stair_exit(stair: bpy.types.Object) -> None:
    """Remove the source module's full-height end cap before adjoining a landing."""
    editable = bmesh.new()
    editable.from_mesh(stair.data)
    exit_faces = [
        face
        for face in editable.faces
        if all(abs(vertex.co.y) < 0.005 for vertex in face.verts)
    ]
    if len(exit_faces) != 2:
        editable.free()
        raise RuntimeError(
            f"Expected two Trey stair exit cap faces; found {len(exit_faces)}"
        )
    bmesh.ops.delete(editable, geom=exit_faces, context="FACES")
    editable.to_mesh(stair.data)
    editable.free()
    stair.data.update()


def configure_trey_material(objects: list[bpy.types.Object]) -> bpy.types.Material:
    palette = load_trey_palette()
    material = bpy.data.materials.new("TreyTowerStairPalette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    if principled is None:
        principled = nodes.new("ShaderNodeBsdfPrincipled")
        output = nodes.get("Material Output") or nodes.new("ShaderNodeOutputMaterial")
        links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "TreyTowerStairPaletteTexture"
    texture.image = palette
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    principled.inputs["Roughness"].default_value = 0.68
    for obj in objects:
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for polygon in obj.data.polygons:
            polygon.material_index = 0
    return material


def add_construction_tower_stairs(
    objects: list[bpy.types.Object],
    spec: AssetSpec,
    root: bpy.types.Object,
) -> None:
    if spec.slug != "building":
        return

    stair_source = import_trey_module(TREY_STAIR_PATH, "StairsWideFull")
    platform_source = import_trey_module(TREY_PLATFORM_PATH, "FloorGreyPlatformFull")
    # Trey closes the high end of this module with a vertical face. That face is
    # useful for a standalone stair but becomes an invisible capsule blocker when
    # a landing continues from the final tread, so open it in the authored mesh.
    open_trey_stair_exit(stair_source)
    configure_trey_material([stair_source, platform_source])
    stair_parts: list[bpy.types.Object] = []

    for storey in range(18):
        base_height = 2.56 * storey
        transforms = (
            (
                stair_source,
                Matrix.Translation(Vector((2.30, -2.50, base_height)))
                @ Matrix.Diagonal(Vector((0.75, 1.0, 0.64, 1.0))),
                f"TowerStair_{storey + 1:02d}_A",
            ),
            (
                stair_source,
                Matrix.Translation(Vector((4.15, -4.50, base_height + 1.28)))
                @ Matrix.Rotation(math.pi, 4, "Z")
                @ Matrix.Diagonal(Vector((0.75, 1.0, 0.64, 1.0))),
                f"TowerStair_{storey + 1:02d}_B",
            ),
            (
                platform_source,
                Matrix.Translation(Vector((3.225, -2.50, base_height + 1.24)))
                @ Matrix.Diagonal(Vector((1.70, 0.35, 1.0, 1.0))),
                f"TowerLanding_{storey + 1:02d}_Mid",
            ),
            (
                platform_source,
                Matrix.Translation(Vector((3.225, -1.75, base_height + 1.24)))
                @ Matrix.Diagonal(Vector((1.70, 0.40, 1.0, 1.0))),
                f"TowerLanding_{storey + 1:02d}_MidTurnApron",
            ),
            (
                platform_source,
                Matrix.Translation(Vector((3.225, -4.50, base_height + 2.52)))
                @ Matrix.Diagonal(Vector((1.70, 0.35, 1.0, 1.0))),
                f"TowerLanding_{storey + 1:02d}_Floor",
            ),
            (
                platform_source,
                Matrix.Translation(Vector((3.225, -5.25, base_height + 2.52)))
                @ Matrix.Diagonal(Vector((1.70, 0.40, 1.0, 1.0))),
                f"TowerLanding_{storey + 1:02d}_FloorTurnApron",
            ),
        )
        for source, transform, name in transforms:
            part = source.copy()
            part.data = source.data
            bpy.context.collection.objects.link(part)
            part.name = name
            part.matrix_world = transform @ source.matrix_world
            stair_parts.append(part)

    bpy.data.objects.remove(stair_source, do_unlink=True)
    bpy.data.objects.remove(platform_source, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for part in stair_parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = stair_parts[0]
    result = bpy.ops.object.join()
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not join construction tower stairs: {result}")
    stairs = stair_parts[0]
    stairs.name = "ConstructionTowerAuthoredStairs"
    stairs.data.name = "ConstructionTowerAuthoredStairsMesh"
    world = stairs.matrix_world.copy()
    stairs.parent = root
    stairs.matrix_world = world
    stairs["source_creator"] = "Trey Ramm / minime453"
    stairs["source_asset"] = "Modular Industrial Pieces"
    stairs["source_url"] = "https://opengameart.org/content/modular-industrial-kit"
    stairs["license"] = "CC0-1.0"
    stairs["authored_flights"] = 36
    stairs["authored_landings"] = 36
    stairs["reachable_storeys"] = 19
    objects.append(stairs)
    root["secondary_source_creator"] = "Trey Ramm / minime453"
    root["secondary_source_asset"] = "Modular Industrial Pieces"
    root["secondary_source_url"] = (
        "https://opengameart.org/content/modular-industrial-kit"
    )
    root["secondary_source_license"] = "CC0-1.0"
    root["authored_stair_flights"] = 36
    root["authored_stair_landings"] = 36
    root["reachable_storeys"] = 19
    root["floor_spacing_m"] = 2.56
    bpy.context.view_layer.update()

    stair_minimum, stair_maximum = object_bounds(stairs)
    expected_minimum = Vector((1.52, -5.65, 0.0))
    expected_maximum = Vector((4.92, -1.35, 46.08))
    if any(stair_minimum[index] > expected_minimum[index] + 0.08 for index in range(3)):
        raise RuntimeError(
            f"Construction tower stairs do not reach expected lower bounds: "
            f"{tuple(stair_minimum)}"
        )
    if any(stair_maximum[index] < expected_maximum[index] - 0.08 for index in range(3)):
        raise RuntimeError(
            f"Construction tower stairs do not reach expected upper bounds: "
            f"{tuple(stair_maximum)}"
        )


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


def write_godot_texture_sidecars(output_path: Path, spec: AssetSpec) -> None:
    """Materialize deterministic copies referenced by Godot's GLB import cache."""
    shutil.copyfile(
        PALETTE_PATH,
        output_path.with_name(f"{output_path.stem}_{PALETTE_PATH.name}"),
    )
    if spec.slug == "building":
        shutil.copyfile(
            TREY_PALETTE_PATH,
            output_path.with_name(f"{output_path.stem}_{TREY_PALETTE_PATH.name}"),
        )


def verify_glb(output_path: Path, expected_dimensions: Vector) -> tuple[Vector, int]:
    clear_scene()
    configure_scene()
    before = set(bpy.data.objects)
    # Blender's verification import materializes embedded images beside the GLB.
    # Keep those stable sidecars: Godot records them as external texture resources
    # while importing the scene, even though the source GLB also embeds each image.
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
    open_office_platform_doorway(objects, spec)
    cut_construction_tower_stairwell(objects, spec)
    add_construction_tower_stairs(objects, spec, root)
    dimensions = validate_dimensions(objects, spec)
    mesh_count, triangle_count, material_count = mesh_statistics(objects)
    output_path = OUTPUT_DIR / spec.output_name
    export_glb(root, objects, output_path)
    write_godot_texture_sidecars(output_path, spec)
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
