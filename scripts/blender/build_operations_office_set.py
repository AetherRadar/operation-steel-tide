"""Build the authored Special Operations command-hall set.

Run with Blender 4.5 or newer:
    blender --background --python scripts/blender/build_operations_office_set.py

Every visible object is an instance of a tracked CC0 source mesh. The script
creates only hierarchy and gameplay-anchor empties; it never creates visible
primitive geometry.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
from dataclasses import dataclass
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
TREY_ROOT = REPO_ROOT / "source_art" / "third_party" / "trey_modular_industrial"
KENNEY_ROOT = REPO_ROOT / "assets" / "models" / "kenney_furniture_kit"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "operations_office"
SOURCE_BLEND = REPO_ROOT / "source_art" / "operations_office" / "operations_office_set.blend"
OUTPUT_GLB = OUTPUT_DIR / "operations_office_set.glb"
TREY_PALETTE = TREY_ROOT / "PacificNorthwestGradientAtlas.png"

TREY_LICENSE = "CC0-1.0"
KENNEY_LICENSE = "CC0-1.0"
ROOT_NAME = "OperationsOfficeAuthoredSet"
ANCHORS = {
    "CameraAnchor": (7.6, -7.4, 2.65),
    "NeutralLookAnchor": (3.2, 4.2, 1.72),
    "QuickLookAnchor": (-4.0, 1.4, 1.45),
    "DemolitionLookAnchor": (5.1, 2.5, 1.05),
    "OperatorStandAnchor": (3.4, 0.1, 0.0),
    "OperatorDeskAnchor": (-4.0, -0.35, 0.0),
    "AircraftAnchor": (4.0, 21.0, 1.55),
    "QuickLightAnchor": (-4.2, -1.0, 2.7),
    "DemolitionLightAnchor": (5.2, -0.8, 2.8),
}


@dataclass(frozen=True)
class Placement:
    asset: str
    name: str
    location: tuple[float, float, float]
    yaw: float = 0.0
    scale: tuple[float, float, float] = (1.0, 1.0, 1.0)


@dataclass
class SourceTemplate:
    asset: str
    source_path: Path
    objects: list[bpy.types.Object]
    is_trey: bool


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.world.color = (0.012, 0.018, 0.024)


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


def source_path(asset: str) -> tuple[Path, bool]:
    if asset.startswith("trey:"):
        return TREY_ROOT / asset.removeprefix("trey:"), True
    if asset.startswith("kenney:"):
        return KENNEY_ROOT / asset.removeprefix("kenney:"), False
    raise ValueError(f"Unknown authored asset namespace: {asset}")


def required_assets(placements: list[Placement]) -> set[str]:
    return {placement.asset for placement in placements}


def require_sources(placements: list[Placement]) -> None:
    missing = [
        str(path)
        for asset in sorted(required_assets(placements))
        for path, _ in [source_path(asset)]
        if not path.is_file()
    ]
    if not TREY_PALETTE.is_file():
        missing.append(str(TREY_PALETTE))
    if missing:
        raise FileNotFoundError("Missing authored sources: " + ", ".join(missing))


def import_source(asset: str) -> SourceTemplate:
    path, is_trey = source_path(asset)
    before = set(bpy.data.objects)
    if is_trey:
        result = bpy.ops.import_scene.fbx(
            filepath=str(path),
            global_scale=1.0,
            use_manual_orientation=False,
            bake_space_transform=False,
            use_image_search=True,
            use_anim=False,
        )
    else:
        result = bpy.ops.import_scene.gltf(filepath=str(path), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {path}: {result}")

    imported = [obj for obj in bpy.data.objects if obj not in before]
    for obj in list(imported):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            imported.remove(obj)
    if not any(obj.type == "MESH" for obj in imported):
        raise RuntimeError(f"No authored mesh was imported from {path}")

    relative = path.relative_to(REPO_ROOT).as_posix()
    creator = "Trey Ramm / minime453" if is_trey else "Kenney"
    source_title = "Modular Industrial Pieces" if is_trey else "Furniture Kit"
    for obj in imported:
        obj["authored_source"] = relative
        obj["source_creator"] = creator
        obj["source_asset"] = source_title
        obj["source_license"] = TREY_LICENSE if is_trey else KENNEY_LICENSE
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
    return SourceTemplate(asset, path, imported, is_trey)


def create_trey_material() -> bpy.types.Material:
    material = bpy.data.materials.new("TreyIndustrialPalette")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    output = nodes.get("Material Output")
    if principled is None or output is None:
        raise RuntimeError("Blender did not create the expected material nodes")
    image = bpy.data.images.load(str(TREY_PALETTE), check_existing=True)
    image.name = TREY_PALETTE.name
    image.pack()
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "TreyIndustrialPaletteTexture"
    texture.label = "CC0 Pacific Northwest Gradient Atlas"
    texture.image = image
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    principled.inputs["Roughness"].default_value = 0.66
    principled.inputs["Metallic"].default_value = 0.08
    return material


def prepare_templates(assets: set[str]) -> dict[str, SourceTemplate]:
    templates = {asset: import_source(asset) for asset in sorted(assets)}
    trey_material = create_trey_material()
    for template in templates.values():
        if not template.is_trey:
            continue
        for obj in template.objects:
            if obj.type == "MESH":
                obj.data.materials.clear()
                obj.data.materials.append(trey_material)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)
    tune_monitor_materials(templates)
    return templates


def tune_monitor_materials(templates: dict[str, SourceTemplate]) -> None:
    template = templates.get("kenney:computerScreen.glb")
    if template is None:
        return
    materials = {
        material
        for obj in template.objects
        if obj.type == "MESH"
        for material in obj.data.materials
        if material is not None
    }
    for material in materials:
        if not material.name.startswith("metalDark"):
            continue
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is None:
            continue
        emission = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        if emission is not None:
            emission.default_value = (0.015, 0.34, 0.25, 1.0)
        strength = principled.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = 0.72
        principled.inputs["Roughness"].default_value = 0.24


def placement_matrix(placement: Placement) -> Matrix:
    return (
        Matrix.Translation(Vector(placement.location))
        @ Matrix.Rotation(math.radians(placement.yaw), 4, "Z")
        @ Matrix.Diagonal((*placement.scale, 1.0))
    )


def planar_offset(
    origin: tuple[float, float],
    local_offset: tuple[float, float],
    yaw: float,
) -> tuple[float, float]:
    angle = math.radians(yaw)
    cos_angle = math.cos(angle)
    sin_angle = math.sin(angle)
    local_x, local_y = local_offset
    return (
        origin[0] + local_x * cos_angle - local_y * sin_angle,
        origin[1] + local_x * sin_angle + local_y * cos_angle,
    )


def instantiate(
    template: SourceTemplate,
    placement: Placement,
    collection: bpy.types.Collection,
) -> list[bpy.types.Object]:
    mapping: dict[bpy.types.Object, bpy.types.Object] = {}
    for index, source in enumerate(template.objects, start=1):
        clone = source.copy()
        clone.name = f"{placement.name}_{index:02d}_{source.name}"
        clone["authored_instance"] = placement.name
        collection.objects.link(clone)
        mapping[source] = clone

    source_set = set(template.objects)
    for source, clone in mapping.items():
        clone.parent = mapping.get(source.parent)
        clone.matrix_parent_inverse = source.matrix_parent_inverse.copy()
        clone.matrix_basis = source.matrix_basis.copy()
    transform = placement_matrix(placement)
    for source, clone in mapping.items():
        if source.parent not in source_set:
            clone.matrix_world = transform @ source.matrix_world
            clone["authored_instance_root"] = True
    return list(mapping.values())


def structural_placements() -> list[Placement]:
    floor = "trey:Meshes/Floors/IndFloorGreyFull.fbx"
    roof = "trey:Meshes/Roofs/IndRoofDarkGreyFull.fbx"
    wall = "trey:Meshes/Walls/IndWallFull.fbx"
    window = "trey:Meshes/Windows/IndWindowBFull.fbx"
    window_frame = "trey:Meshes/Windows/IndWindowEFrame.fbx"
    column = "trey:Meshes/Details/IndColumnFree.fbx"
    placements: list[Placement] = []

    for x in range(-13, 14, 2):
        for y in range(-7, 8, 2):
            placements.append(Placement(floor, f"CommandFloor_{x}_{y}", (x, y, 0.0)))
            placements.append(Placement(roof, f"CommandRoof_{x}_{y}", (x, y, 4.17)))

    for y in range(-7, 8, 2):
        placements.append(
            Placement(wall, f"LeftWall_{y}", (-14.1, y, 0.0), 90.0, (1.0, 1.0, 1.35))
        )
        placements.append(
            Placement(wall, f"RightWall_{y}", (14.1, y, 0.0), -90.0, (1.0, 1.0, 1.35))
        )
    for x in range(-13, 14, 2):
        if x in {1, 3, 5, 7}:
            placements.append(
                Placement(
                    window_frame,
                    f"HelipadVistaFrame_{x}",
                    (x, 8.1, 0.15),
                    0.0,
                    (1.25, 1.0, 0.75),
                )
            )
        else:
            placements.append(
                Placement(
                    window,
                    f"PanoramicWindow_{x}",
                    (x, 8.1, 0.0),
                    0.0,
                    (1.0, 1.0, 1.35),
                )
            )
    for x in range(-14, 15, 4):
        placements.append(
            Placement(column, f"RearColumn_{x}", (x, 8.0, 0.0), 0.0, (1.0, 1.0, 1.35))
        )
    for y in (-8.0, -4.0, 0.0, 4.0, 8.0):
        placements.append(
            Placement(column, f"LeftColumn_{y}", (-14.0, y, 0.0), 0.0, (1.0, 1.0, 1.35))
        )
        placements.append(
            Placement(column, f"RightColumn_{y}", (14.0, y, 0.0), 0.0, (1.0, 1.0, 1.35))
        )
    return placements


def helipad_placements() -> list[Placement]:
    platform = "trey:Meshes/Floors/IndFloorGreyPlatformFull.fbx"
    trim = "trey:Meshes/Trims/IndRoofTrimBStraightFull.fbx"
    placements: list[Placement] = []
    for x in (1.0, 3.0, 5.0, 7.0):
        for y in (10.0, 12.0):
            placements.append(Placement(platform, f"BridgeDeck_{x}_{y}", (x, y, 0.4)))
    for y in (10.0, 12.0):
        placements.append(Placement(trim, f"BridgeTrimLeft_{y}", (0.0, y, 0.4), 90.0))
        placements.append(Placement(trim, f"BridgeTrimRight_{y}", (8.0, y, 0.4), -90.0))

    for x in range(-5, 14, 2):
        for y in range(14, 29, 2):
            placements.append(Placement(platform, f"HelipadDeck_{x}_{y}", (x, y, 0.4)))
    for y in range(14, 29, 2):
        placements.append(Placement(trim, f"HelipadTrimLeft_{y}", (-6.0, y, 0.4), 90.0))
        placements.append(Placement(trim, f"HelipadTrimRight_{y}", (14.0, y, 0.4), -90.0))
    for x in range(-5, 14, 2):
        placements.append(Placement(trim, f"HelipadTrimRear_{x}", (x, 30.0, 0.4), 180.0))
    return placements


def ceiling_service_placements() -> list[Placement]:
    primary_rib = "trey:Meshes/Trims/IndRoofTrimBStraightFull.fbx"
    service_spine = "trey:Meshes/Trims/IndRoofTrimAStraight.fbx"
    connector = "trey:Meshes/Details/IndColumnFreeCap.fbx"
    placements: list[Placement] = []

    cross_rib_y = (-6.5, -2.0, 2.5, 6.5)
    for y in cross_rib_y:
        for x in range(-13, 14, 2):
            placements.append(
                Placement(
                    primary_rib,
                    f"CeilingCrossRib_{y}_{x}",
                    (x, y, 3.7),
                    0.0,
                    (1.0, 1.0, 0.42),
                )
            )
    for x in (-9.0, 0.0, 9.0):
        for y in range(-7, 8, 2):
            placements.append(
                Placement(service_spine, f"CeilingServiceSpine_{x}_{y}", (x, y, 3.72), 90.0)
            )
    for x in (-9.0, 0.0, 9.0):
        for y in cross_rib_y:
            placements.append(Placement(connector, f"CeilingConnector_{x}_{y}", (x, y, 0.72)))
    return placements


def workstation_placements() -> list[Placement]:
    desk = "kenney:desk.glb"
    chair = "kenney:chairDesk.glb"
    screen = "kenney:computerScreen.glb"
    placements: list[Placement] = []
    stations = (
        # Index, position, desk yaw, chair offset/yaw, monitor local x/y/yaw/scale.
        (1, (-8.45, 0.25), -13.0, (0.18, -1.12, 7.0), ((-0.12, 0.04, -4.0, 1.8),)),
        (
            2,
            (-5.05, 1.55),
            7.0,
            (-0.12, -0.98, -5.0),
            ((-0.43, 0.02, -10.0, 1.65), (0.47, 0.11, 12.0, 1.65)),
        ),
        (3, (-1.55, -0.2), -6.0, (0.15, -1.2, 8.0), ((0.03, 0.03, 0.0, 1.85),)),
    )
    for index, (x, y), yaw, chair_spec, monitors in stations:
        placements.append(
            Placement(
                desk,
                f"Workstation{index}_Desk",
                (x, y, 0.0),
                yaw,
                (2.45, 2.45, 2.45),
            )
        )
        chair_x, chair_y = planar_offset((x, y), chair_spec[:2], yaw)
        placements.append(
            Placement(
                chair,
                f"Workstation{index}_Chair",
                (chair_x, chair_y, 0.0),
                yaw + chair_spec[2],
                (2.35, 2.35, 2.35),
            )
        )
        for monitor, (local_x, local_y, yaw_delta, monitor_scale) in enumerate(
            monitors, start=1
        ):
            screen_x, screen_y = planar_offset((x, y), (local_x, local_y), yaw)
            placements.append(
                Placement(
                    screen,
                    f"Workstation{index}_Screen{monitor}",
                    (screen_x, screen_y, 0.93),
                    yaw + yaw_delta,
                    (monitor_scale, monitor_scale, monitor_scale),
                )
            )
    return placements


def furniture_placements() -> list[Placement]:
    table = "kenney:table.glb"
    chair = "kenney:chairDesk.glb"
    screen = "kenney:computerScreen.glb"
    cabinet = "kenney:bookcaseClosedDoors.glb"
    sofa = "kenney:loungeSofa.glb"
    side_table = "kenney:sideTableDrawers.glb"
    coffee_table = "kenney:tableCoffee.glb"
    equipment_counter = "kenney:kitchenBar.glb"
    drawer_cabinet = "kenney:kitchenCabinetDrawer.glb"
    equipment_case = "kenney:cardboardBoxClosed.glb"
    placements = [
        Placement(table, "CentralTacticalTable", (5.1, 2.5, 0.0), 14.0, (3.9, 3.35, 2.8)),
        Placement(chair, "TacticalChairLead", (4.85, 0.9, 0.0), 185.0, (2.35, 2.35, 2.35)),
        Placement(chair, "TacticalChairPlanner", (3.55, 3.85, 0.0), -38.0, (2.35, 2.35, 2.35)),
        Placement(chair, "TacticalChairSpecialist", (7.35, 2.75, 0.0), 96.0, (2.35, 2.35, 2.35)),
        Placement(screen, "TacticalConsoleLeadA", (4.48, 1.95, 1.0), 173.0, (1.7, 1.7, 1.7)),
        Placement(screen, "TacticalConsolePlanner", (5.72, 3.08, 1.0), 12.0, (1.62, 1.62, 1.62)),
        Placement(sofa, "ReadyAreaSofa", (-3.9, 5.3, 0.0), 176.0, (2.35, 2.35, 2.35)),
        Placement(coffee_table, "ReadyAreaCoffeeTable", (-4.8, 4.0, 0.0), -12.0, (2.15, 2.15, 2.15)),
        Placement(side_table, "ReadyAreaSideTable", (-1.75, 5.15, 0.0), 4.0, (2.25, 2.25, 2.25)),
        Placement(cabinet, "ReadyAreaGearLocker", (-7.8, 6.55, 0.0), 178.0, (2.4, 2.4, 2.4)),
        Placement(equipment_counter, "DemolitionPreparationCounter", (8.1, 4.1, 0.0), -12.0, (2.25, 2.25, 2.25)),
        Placement(drawer_cabinet, "DemolitionDrawerCabinet", (8.65, 1.05, 0.0), 82.0, (2.3, 2.3, 2.3)),
        Placement(cabinet, "DemolitionEquipmentLockerA", (11.2, 1.3, 0.0), -90.0, (2.4, 2.4, 2.4)),
        Placement(cabinet, "DemolitionEquipmentLockerB", (10.45, 5.65, 0.0), -164.0, (2.4, 2.4, 2.4)),
        Placement(equipment_case, "ReadyAreaCaseA", (-2.25, 4.35, 0.0), 21.0, (1.45, 1.45, 1.45)),
        Placement(equipment_case, "ReadyAreaCaseB", (-1.75, 4.6, 0.0), -14.0, (1.1, 1.1, 1.1)),
        Placement(equipment_case, "DemolitionCaseA", (7.35, 4.65, 0.0), -8.0, (1.45, 1.45, 1.45)),
        Placement(equipment_case, "DemolitionCaseB", (7.95, 4.85, 0.0), 17.0, (1.15, 1.15, 1.15)),
    ]
    return placements


def all_placements() -> list[Placement]:
    return (
        structural_placements()
        + ceiling_service_placements()
        + helipad_placements()
        + workstation_placements()
        + furniture_placements()
    )


def create_root() -> bpy.types.Object:
    root = bpy.data.objects.new(ROOT_NAME, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 1.0
    root["source_creators"] = "Trey Ramm / minime453; Kenney"
    root["source_assets"] = "Modular Industrial Pieces; Furniture Kit"
    root["source_urls"] = (
        "https://opengameart.org/content/modular-industrial-kit; "
        "https://kenney.nl/assets/furniture-kit"
    )
    root["license"] = "CC0-1.0"
    root["assembly"] = "special-operations-command-hall"
    root["units"] = "meters"
    root["visible_geometry_policy"] = "authored-source-meshes-only"
    return root


def parent_to_root(root: bpy.types.Object, objects: list[bpy.types.Object]) -> None:
    object_set = set(objects)
    for obj in objects:
        if obj.parent in object_set:
            continue
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world


def create_anchors(root: bpy.types.Object) -> list[bpy.types.Object]:
    anchors: list[bpy.types.Object] = []
    for name, location in ANCHORS.items():
        anchor = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(anchor)
        anchor.parent = root
        anchor.location = location
        anchor.empty_display_type = "SPHERE"
        anchor.empty_display_size = 0.28
        anchor["gameplay_anchor"] = True
        anchors.append(anchor)
    return anchors


def remove_templates(templates: dict[str, SourceTemplate]) -> None:
    for template in templates.values():
        for obj in template.objects:
            bpy.data.objects.remove(obj, do_unlink=True)


def mesh_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not corners:
        raise RuntimeError("Operations office has no visible authored meshes")
    return (
        Vector(tuple(min(point[index] for point in corners) for index in range(3))),
        Vector(tuple(max(point[index] for point in corners) for index in range(3))),
    )


def mesh_statistics(objects: list[bpy.types.Object]) -> tuple[int, int, int, int]:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    mesh_data = {obj.data for obj in meshes}
    materials = {
        material
        for obj in meshes
        for material in obj.data.materials
        if material is not None
    }
    return len(meshes), len(mesh_data), triangles, len(materials)


def validate_authored_geometry(objects: list[bpy.types.Object]) -> None:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    missing = [obj.name for obj in meshes if not obj.get("authored_source")]
    if missing:
        raise RuntimeError("Visible meshes lack authored provenance: " + ", ".join(missing[:8]))
    unsupported = [
        obj.name
        for obj in meshes
        if not str(obj["authored_source"]).startswith(
            ("source_art/third_party/trey_modular_industrial/", "assets/models/kenney_furniture_kit/")
        )
    ]
    if unsupported:
        raise RuntimeError("Visible meshes use unsupported sources: " + ", ".join(unsupported[:8]))

    required_instances = {
        "CentralTacticalTable",
        "Workstation1_Desk",
        "Workstation1_Chair",
        "Workstation1_Screen1",
        "Workstation2_Desk",
        "Workstation2_Chair",
        "Workstation2_Screen1",
        "Workstation3_Desk",
        "Workstation3_Chair",
        "Workstation3_Screen1",
        "ReadyAreaSofa",
        "ReadyAreaCoffeeTable",
        "ReadyAreaGearLocker",
        "ReadyAreaCaseA",
        "DemolitionPreparationCounter",
        "DemolitionDrawerCabinet",
        "DemolitionCaseA",
    }
    present = {str(obj.get("authored_instance", "")) for obj in meshes}
    absent = sorted(required_instances - present)
    if absent:
        raise RuntimeError("Required authored office groups are missing: " + ", ".join(absent))


def validate_layout(objects: list[bpy.types.Object]) -> Vector:
    minimum, maximum = mesh_bounds(objects)
    dimensions = maximum - minimum
    object_extents = [
        (
            obj.name,
            min((obj.matrix_world @ Vector(corner)).x for corner in obj.bound_box),
            max((obj.matrix_world @ Vector(corner)).x for corner in obj.bound_box),
            min((obj.matrix_world @ Vector(corner)).y for corner in obj.bound_box),
            max((obj.matrix_world @ Vector(corner)).y for corner in obj.bound_box),
        )
        for obj in objects
        if obj.type == "MESH"
    ]
    min_x = min(object_extents, key=lambda item: item[1])
    max_x = max(object_extents, key=lambda item: item[2])
    min_y = min(object_extents, key=lambda item: item[3])
    max_y = max(object_extents, key=lambda item: item[4])
    extent_summary = f"extremes={min_x},{max_x},{min_y},{max_y}"
    if not (27.0 <= dimensions.x <= 31.0):
        raise RuntimeError(f"Unexpected office width: {dimensions.x:.3f}m {extent_summary}")
    if not (37.0 <= dimensions.y <= 41.0):
        raise RuntimeError(f"Unexpected office depth: {dimensions.y:.3f}m {extent_summary}")
    if not (4.0 <= dimensions.z <= 4.5):
        raise RuntimeError(f"Unexpected office height: {dimensions.z:.3f}m")
    if minimum.y > -7.8 or maximum.y < 29.8:
        raise RuntimeError(f"Open hall or helipad depth is incomplete: {minimum.y:.3f}..{maximum.y:.3f}")
    return dimensions


def save_blend() -> None:
    SOURCE_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND), compress=True, check_existing=False)
    if not SOURCE_BLEND.is_file():
        raise RuntimeError("Blender did not save the authoritative operations office source")


def export_glb(root: bpy.types.Object, objects: list[bpy.types.Object]) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
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
    if "FINISHED" not in result or not OUTPUT_GLB.is_file():
        raise RuntimeError(f"Blender could not export {OUTPUT_GLB}: {result}")


def glb_json(path: Path) -> dict[str, object]:
    payload = path.read_bytes()
    if len(payload) < 20 or payload[:4] != b"glTF":
        raise RuntimeError(f"{path.name} is not a binary glTF")
    _, version, length = struct.unpack_from("<III", payload, 0)
    chunk_length, chunk_type = struct.unpack_from("<II", payload, 12)
    if version != 2 or length != len(payload) or chunk_type != 0x4E4F534A:
        raise RuntimeError(f"{path.name} has an invalid GLB header")
    return json.loads(payload[20 : 20 + chunk_length].decode("utf-8"))


def verify_embedded_payload(path: Path) -> None:
    document = glb_json(path)
    external_buffers = [entry for entry in document.get("buffers", []) if "uri" in entry]
    external_images = [entry for entry in document.get("images", []) if "uri" in entry]
    if external_buffers or external_images:
        raise RuntimeError(f"{path.name} depends on external buffers or images")


def verify_glb(expected_dimensions: Vector) -> tuple[Vector, int, int, int, int]:
    verify_embedded_payload(OUTPUT_GLB)
    clear_scene()
    configure_scene()
    result = bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not round-trip {OUTPUT_GLB.name}: {result}")
    imported = list(bpy.context.scene.objects)
    minimum, maximum = mesh_bounds(imported)
    dimensions = maximum - minimum
    if any(abs(dimensions[index] - expected_dimensions[index]) > 0.015 for index in range(3)):
        raise RuntimeError(
            "GLB dimensions changed during round trip: "
            f"expected={tuple(expected_dimensions)} actual={tuple(dimensions)}"
        )
    root = next((obj for obj in imported if obj.name == ROOT_NAME), None)
    if root is None or root.get("license") != "CC0-1.0":
        raise RuntimeError("GLB lost its authored-set root or CC0 metadata")
    imported_anchors = {obj.name: obj for obj in imported if obj.name in ANCHORS}
    if set(imported_anchors) != set(ANCHORS):
        raise RuntimeError(
            "GLB anchor mismatch: "
            f"expected={sorted(ANCHORS)} actual={sorted(imported_anchors)}"
        )
    for name, expected in ANCHORS.items():
        actual = imported_anchors[name].matrix_world.translation
        if (actual - Vector(expected)).length > 0.005:
            raise RuntimeError(f"Anchor {name} moved during round trip: {tuple(actual)}")
    validate_authored_geometry(imported)
    return dimensions, *mesh_statistics(imported)


def build() -> None:
    clear_scene()
    configure_scene()
    placements = all_placements()
    require_sources(placements)
    templates = prepare_templates(required_assets(placements))
    root = create_root()
    objects: list[bpy.types.Object] = []
    for placement in placements:
        objects.extend(instantiate(templates[placement.asset], placement, bpy.context.collection))
    remove_templates(templates)
    parent_to_root(root, objects)
    anchors = create_anchors(root)
    bpy.context.view_layer.update()
    validate_authored_geometry(objects)
    dimensions = validate_layout(objects)
    mesh_count, mesh_data_count, triangles, material_count = mesh_statistics(objects)
    save_blend()
    export_glb(root, objects + anchors)
    verified_dimensions, verified_meshes, verified_mesh_data, verified_triangles, verified_materials = (
        verify_glb(dimensions)
    )
    digest = hashlib.sha256(OUTPUT_GLB.read_bytes()).hexdigest().upper()
    print(
        "OPERATIONS_OFFICE_ASSET "
        f"dimensions_m={dimensions.x:.3f}x{dimensions.y:.3f}x{dimensions.z:.3f} "
        f"verified_m={verified_dimensions.x:.3f}x{verified_dimensions.y:.3f}x{verified_dimensions.z:.3f} "
        f"placements={len(placements)} meshes={mesh_count} unique_meshes={mesh_data_count} "
        f"triangles={triangles} materials={material_count} anchors={len(ANCHORS)} "
        f"roundtrip_meshes={verified_meshes} roundtrip_unique_meshes={verified_mesh_data} "
        f"roundtrip_triangles={verified_triangles} roundtrip_materials={verified_materials} "
        f"blend_bytes={SOURCE_BLEND.stat().st_size} glb_bytes={OUTPUT_GLB.stat().st_size} sha256={digest}"
    )
    print("OPERATIONS_OFFICE_PASS valid=True authored_geometry=True embedded=True")


if __name__ == "__main__":
    build()
