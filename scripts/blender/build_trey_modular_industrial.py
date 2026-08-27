"""Assemble Trey Ramm's CC0 industrial modules into Godot-ready GLBs.

Run with Blender 4.5 or newer:
    blender --background --python scripts/blender/build_trey_modular_industrial.py

Pass ``-- --only arch-gateway loading-bay`` to rebuild selected assemblies.
Every visible mesh comes from the original Modular Industrial Pieces pack;
the script only places, names, and exports those authored modules.
"""

from __future__ import annotations

import argparse
import math
import sys
from dataclasses import dataclass
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "trey_modular_industrial"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "trey_modular_industrial"
PALETTE_PATH = SOURCE_DIR / "PacificNorthwestGradientAtlas.png"


@dataclass(frozen=True)
class Module:
    source: str
    location: tuple[float, float, float] = (0.0, 0.0, 0.0)
    yaw: float = 0.0


@dataclass(frozen=True)
class Assembly:
    slug: str
    output_name: str
    root_name: str
    modules: tuple[Module, ...]


ASSEMBLIES = (
    Assembly(
        "east-security-gate",
        "east-security-gate.glb",
        "TreyIndustrialEastSecurityGate",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx"),
            Module("Meshes/Details/IndColumnFreeCap.fbx"),
            Module("Meshes/Details/IndColumnFree.fbx", (4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (4.2, 0.0, 0.0)),
        ),
    ),
    Assembly(
        "west-service-gate",
        "west-service-gate.glb",
        "TreyIndustrialWestServiceGate",
        (
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx"),
            Module("Meshes/Doors/IndGarageWhite.fbx"),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (4.2, 0.0, 0.0)),
        ),
    ),
    Assembly(
        "arch-gateway",
        "arch-gateway.glb",
        "TreyIndustrialArchGateway",
        (
            Module("Meshes/Walls/IndWallArchDouble.fbx"),
            Module("Meshes/Walls/IndWallArchDoubleColumns.fbx"),
            Module("Meshes/Walls/IndWallArchDoubleCapGrey.fbx", (0.0, 0.0, 3.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-2.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-2.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (2.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (2.2, 0.0, 0.0)),
        ),
    ),
    Assembly(
        "loading-bay",
        "loading-bay.glb",
        "TreyIndustrialLoadingBay",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx"),
            Module("Meshes/Doors/IndGarageWhite.fbx"),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 3.0, 3.0)),
        ),
    ),
    Assembly(
        "elevated-walkway",
        "elevated-walkway.glb",
        "TreyIndustrialElevatedWalkway",
        (
            Module("Meshes/Details/IndColumnFree.fbx", (-3.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-3.0, 0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-1.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-1.0, 0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (1.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (1.0, 0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (3.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (3.0, 0.75, 0.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (-3.0, 0.0, 2.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (-1.0, 0.0, 2.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (1.0, 0.0, 2.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (3.0, 0.0, 2.0)),
            Module("Meshes/Details/IndStairsWideFull.fbx", (-4.0, 0.0, 0.0), -90.0),
            Module("Meshes/Details/IndStairsWideFull.fbx", (4.0, 0.0, 0.0), 90.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-3.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-1.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (1.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (3.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-3.0, 1.0, 2.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-1.0, 1.0, 2.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (1.0, 1.0, 2.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (3.0, 1.0, 2.0), 180.0),
        ),
    ),
    Assembly(
        "window-hall",
        "window-hall.glb",
        "TreyIndustrialWindowHall",
        (
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 3.0, 3.0)),
        ),
    ),
    Assembly(
        "sawtooth-service-hall",
        "sawtooth-service-hall.glb",
        "TreyIndustrialServiceHall",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 3.0, 3.0)),
        ),
    ),
    Assembly(
        "utility-office",
        "utility-office.glb",
        "TreyIndustrialUtilityOffice",
        (
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 2.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 2.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-2.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (2.0, 1.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Trims/IndCornerTrimBFull.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Trims/IndCornerTrimBFull.fbx", (2.0, 0.0, 0.0), 90.0),
        ),
    ),
)


def parse_args() -> set[str]:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--only",
        nargs="+",
        choices=[assembly.slug for assembly in ASSEMBLIES],
        help="Rebuild only the listed assembly slugs.",
    )
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(blender_args)
    return set(args.only or [assembly.slug for assembly in ASSEMBLIES])


def require_sources() -> None:
    missing = sorted(
        {
            module.source
            for assembly in ASSEMBLIES
            for module in assembly.modules
            if not (SOURCE_DIR / module.source).is_file()
        }
    )
    if not PALETTE_PATH.is_file():
        missing.append(PALETTE_PATH.name)
    if missing:
        raise FileNotFoundError(f"Missing Trey source files: {', '.join(missing)}")


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


def import_module(module: Module, index: int) -> list[bpy.types.Object]:
    path = SOURCE_DIR / module.source
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
        raise RuntimeError(f"Blender could not import {module.source}: {result}")

    imported = [obj for obj in bpy.data.objects if obj not in before]
    for obj in list(imported):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            imported.remove(obj)
    if not any(obj.type == "MESH" for obj in imported):
        raise RuntimeError(f"No mesh objects were imported from {module.source}")

    transform = Matrix.Translation(Vector(module.location)) @ Matrix.Rotation(
        math.radians(module.yaw), 4, "Z"
    )
    imported_set = set(imported)
    for obj in imported:
        obj.name = f"Part_{index:02d}_{path.stem}_{obj.name}"
    for obj in [candidate for candidate in imported if candidate.parent not in imported_set]:
        obj.matrix_world = transform @ obj.matrix_world
    return imported


def load_palette() -> bpy.types.Image:
    image = bpy.data.images.get(PALETTE_PATH.name)
    if image is None:
        image = bpy.data.images.load(str(PALETTE_PATH), check_existing=True)
    image.name = PALETTE_PATH.name
    image.filepath = str(PALETTE_PATH)
    image.colorspace_settings.name = "sRGB"
    return image


def ensure_palette_materials(objects: list[bpy.types.Object], palette: bpy.types.Image) -> None:
    materials = {
        material
        for obj in objects
        if obj.type == "MESH"
        for material in obj.data.materials
        if material is not None
    }
    if not materials:
        fallback = bpy.data.materials.new("TreyIndustrialPalette")
        materials.add(fallback)
        for obj in objects:
            if obj.type == "MESH":
                obj.data.materials.append(fallback)

    for index, material in enumerate(sorted(materials, key=lambda item: item.name)):
        material.name = f"TreyIndustrialPalette_{index + 1:02d}"
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        principled = nodes.get("Principled BSDF")
        if principled is None:
            principled = nodes.new("ShaderNodeBsdfPrincipled")
            output = nodes.get("Material Output") or nodes.new("ShaderNodeOutputMaterial")
            links.new(principled.outputs["BSDF"], output.inputs["Surface"])

        texture_nodes = [node for node in nodes if node.type == "TEX_IMAGE"]
        texture = texture_nodes[0] if texture_nodes else nodes.new("ShaderNodeTexImage")
        texture.name = "TreyIndustrialPaletteTexture"
        texture.label = "CC0 Pacific Northwest Gradient Atlas"
        texture.image = palette
        texture.interpolation = "Closest"
        if not any(link.to_node == principled and link.to_socket.name == "Base Color" for link in links):
            links.new(texture.outputs["Color"], principled.inputs["Base Color"])
        principled.inputs["Roughness"].default_value = 0.68
        emission = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        if emission is not None:
            emission.default_value = (0.0, 0.0, 0.0, 1.0)
        emission_strength = principled.inputs.get("Emission Strength")
        if emission_strength is not None:
            emission_strength.default_value = 0.0


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
    return (
        Vector(
            (
                min(point.x for point in corners),
                min(point.y for point in corners),
                min(point.z for point in corners),
            )
        ),
        Vector(
            (
                max(point.x for point in corners),
                max(point.y for point in corners),
                max(point.z for point in corners),
            )
        ),
    )


def world_mesh_bvh(obj: bpy.types.Object) -> tuple[BVHTree, list[Vector]]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    triangles = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    if not vertices or not triangles:
        raise RuntimeError(f"Cannot calculate surface distance for {obj.name}")
    return BVHTree.FromPolygons(vertices, triangles, all_triangles=True), vertices


def mesh_surface_distance(first: bpy.types.Object, second: bpy.types.Object) -> float:
    first_bvh, first_vertices = world_mesh_bvh(first)
    second_bvh, second_vertices = world_mesh_bvh(second)
    if first_bvh.overlap(second_bvh):
        return 0.0

    minimum = math.inf
    for point in first_vertices:
        nearest = second_bvh.find_nearest(point)
        if nearest is not None:
            minimum = min(minimum, nearest[3])
    for point in second_vertices:
        nearest = first_bvh.find_nearest(point)
        if nearest is not None:
            minimum = min(minimum, nearest[3])
    return minimum


def intervals_cover(
    intervals: list[tuple[float, float]],
    expected_start: float,
    expected_end: float,
    tolerance: float = 0.18,
) -> bool:
    if not intervals:
        return False

    merged_end = expected_start
    for start, end in sorted(intervals):
        if end < merged_end - tolerance:
            continue
        if start > merged_end + tolerance:
            return False
        merged_end = max(merged_end, end)
        if merged_end >= expected_end - tolerance:
            return True
    return merged_end >= expected_end - tolerance


def validate_closed_building_perimeter(
    objects: list[bpy.types.Object], assembly: Assembly
) -> None:
    if assembly.slug not in {
        "loading-bay",
        "window-hall",
        "sawtooth-service-hall",
        "utility-office",
    }:
        return

    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    perimeter = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and any(
            marker in obj.name
            for marker in ("IndWall", "IndWindow", "IndDoor", "IndGarage")
        )
    ]
    if not roofs or not perimeter:
        raise RuntimeError(f"{assembly.slug} is missing roof or perimeter meshes")

    roof_minimum, roof_maximum = mesh_bounds(roofs)
    wall_bounds = [(obj.name, *object_bounds(obj)) for obj in perimeter]
    plane_tolerance = 0.35
    sides = {
        "front": (
            [
                (minimum.x, maximum.x)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.y + maximum.y) * 0.5 - roof_minimum.y) <= plane_tolerance
            ],
            roof_minimum.x,
            roof_maximum.x,
        ),
        "back": (
            [
                (minimum.x, maximum.x)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.y + maximum.y) * 0.5 - roof_maximum.y) <= plane_tolerance
            ],
            roof_minimum.x,
            roof_maximum.x,
        ),
        "left": (
            [
                (minimum.y, maximum.y)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.x + maximum.x) * 0.5 - roof_minimum.x) <= plane_tolerance
            ],
            roof_minimum.y,
            roof_maximum.y,
        ),
        "right": (
            [
                (minimum.y, maximum.y)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.x + maximum.x) * 0.5 - roof_maximum.x) <= plane_tolerance
            ],
            roof_minimum.y,
            roof_maximum.y,
        ),
    }
    for side, (intervals, expected_start, expected_end) in sides.items():
        if not intervals_cover(intervals, expected_start, expected_end):
            raise RuntimeError(
                f"{assembly.slug} has an open {side} perimeter: "
                f"expected={expected_start:.3f}..{expected_end:.3f} intervals={intervals}"
            )


def normalize_assembly(objects: list[bpy.types.Object], assembly: Assembly) -> bpy.types.Object:
    minimum, maximum = mesh_bounds(objects)
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
    translation = Matrix.Translation(offset)
    imported_set = set(objects)
    top_level = [obj for obj in objects if obj.parent not in imported_set]
    for obj in top_level:
        obj.matrix_world = translation @ obj.matrix_world

    root = bpy.data.objects.new(assembly.root_name, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 1.0
    root["source_creator"] = "Trey Ramm / minime453"
    root["source_asset"] = "Modular Industrial Pieces"
    root["source_url"] = "https://opengameart.org/content/modular-industrial-kit"
    root["license"] = "CC0-1.0"
    root["assembly"] = assembly.slug
    root["units"] = "meters"
    for obj in top_level:
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    bpy.context.view_layer.update()
    return root


def validate_dimensions(objects: list[bpy.types.Object], assembly: Assembly) -> Vector:
    minimum, maximum = mesh_bounds(objects)
    dimensions = maximum - minimum
    if min(dimensions) < 0.05:
        raise RuntimeError(f"{assembly.slug} has a collapsed dimension: {tuple(dimensions)}")
    if max(dimensions) > 80.0:
        raise RuntimeError(f"{assembly.slug} is not in plausible meter scale: {tuple(dimensions)}")
    if abs(minimum.z) > 0.002:
        raise RuntimeError(f"{assembly.slug} is not grounded at Z=0 (minimum Z={minimum.z:.6f})")
    return dimensions


def validate_arch_gateway_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "arch-gateway":
        return

    column_bodies = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and "IndColumnFree" in obj.name
        and "IndColumnFreeCap" not in obj.name
    ]
    column_caps = [
        obj for obj in objects if obj.type == "MESH" and "IndColumnFreeCap" in obj.name
    ]
    wall_caps = [
        obj for obj in objects if obj.type == "MESH" and "IndWallArchDoubleCapGrey" in obj.name
    ]
    if len(column_bodies) != 2 or len(column_caps) != 2 or len(wall_caps) != 1:
        raise RuntimeError(
            "arch-gateway must contain two free columns, two column caps, and one wall cap"
        )

    body_minimum, body_maximum = mesh_bounds(column_bodies)
    cap_minimum, cap_maximum = mesh_bounds(column_caps + wall_caps)
    if body_minimum.z < -0.005 or body_minimum.z > 0.15:
        raise RuntimeError(f"arch-gateway columns are not grounded: minimum={body_minimum.z:.3f}")
    if cap_minimum.z < body_maximum.z - 0.15 or cap_minimum.z > body_maximum.z + 0.15:
        raise RuntimeError(
            "arch-gateway caps do not meet the column tops: "
            f"column_top={body_maximum.z:.3f} cap_bottom={cap_minimum.z:.3f}"
        )
    if cap_maximum.z > body_maximum.z + 0.35:
        raise RuntimeError(
            "arch-gateway caps extend implausibly above the columns: "
            f"column_top={body_maximum.z:.3f} cap_top={cap_maximum.z:.3f}"
        )


def validate_elevated_walkway_parts(
    objects: list[bpy.types.Object], assembly: Assembly
) -> None:
    if assembly.slug != "elevated-walkway":
        return

    floors = [obj for obj in objects if obj.type == "MESH" and "IndFloor" in obj.name]
    stairs = [obj for obj in objects if obj.type == "MESH" and "IndStairs" in obj.name]
    pillars = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and "IndColumnFree" in obj.name
        and "IndColumnFreeCap" not in obj.name
    ]
    rails = [obj for obj in objects if obj.type == "MESH" and "IndRoofTrim" in obj.name]
    if len(floors) != 4 or any("IndFloorGreyPlatformFull" not in floor.name for floor in floors):
        raise RuntimeError("elevated-walkway must use four complete platform modules")
    if len(stairs) != 2 or any("IndStairsWideFull" not in stair.name for stair in stairs):
        raise RuntimeError("elevated-walkway must use two straight wide stair modules")
    if len(pillars) != 8 or len(rails) != 8:
        raise RuntimeError(
            "elevated-walkway must contain eight pillars and eight authored rail modules"
        )

    floor_minimum, floor_maximum = mesh_bounds(floors)
    stair_bounds = sorted(
        (object_bounds(stair) for stair in stairs), key=lambda bounds: bounds[0].x
    )
    left_minimum, left_maximum = stair_bounds[0]
    right_minimum, right_maximum = stair_bounds[1]
    rail_minimum, rail_maximum = mesh_bounds(rails)
    tolerance = 0.01
    ready = (
        abs(left_maximum.x - floor_minimum.x) <= tolerance
        and abs(right_minimum.x - floor_maximum.x) <= tolerance
        and abs(left_minimum.x + right_maximum.x) <= tolerance
        and abs(left_maximum.x + right_minimum.x) <= tolerance
        and abs(left_minimum.y - floor_minimum.y) <= tolerance
        and abs(left_maximum.y - floor_maximum.y) <= tolerance
        and abs(right_minimum.y - floor_minimum.y) <= tolerance
        and abs(right_maximum.y - floor_maximum.y) <= tolerance
        and abs(left_maximum.z - floor_maximum.z) <= tolerance
        and abs(right_maximum.z - floor_maximum.z) <= tolerance
        and abs(rail_minimum.y + rail_maximum.y) <= tolerance
    )
    if not ready:
        raise RuntimeError(
            "elevated-walkway stairs do not meet the platform symmetrically: "
            f"floor={tuple(floor_minimum)}..{tuple(floor_maximum)} "
            f"left={tuple(left_minimum)}..{tuple(left_maximum)} "
            f"right={tuple(right_minimum)}..{tuple(right_maximum)}"
        )

    rail_gaps = [
        (rail.name, min(mesh_surface_distance(rail, support) for support in floors + pillars))
        for rail in rails
    ]
    maximum_rail_gap = max(gap for _, gap in rail_gaps)
    if maximum_rail_gap > tolerance:
        raise RuntimeError(
            "elevated-walkway rails are detached from the deck structure: "
            f"gaps={rail_gaps}"
        )
    print(
        "TREY_WALKWAY_CHECK "
        f"rails={len(rail_gaps)} maximum_attachment_gap_m={maximum_rail_gap:.6f}"
    )


def validate_utility_office_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "utility-office":
        return

    windows = [obj for obj in objects if obj.type == "MESH" and "IndWindow" in obj.name]
    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    if len(windows) != 1 or "IndWindowBFull" not in windows[0].name or len(roofs) != 2:
        raise RuntimeError("utility-office must use one single-storey window and two roof modules")

    _, office_maximum = mesh_bounds(objects)
    window_minimum, window_maximum = mesh_bounds(windows)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    if office_maximum.z > 3.25:
        raise RuntimeError(f"utility-office exceeds one storey: maximum={office_maximum.z:.3f}")
    if window_minimum.z < -0.005 or window_maximum.z > 3.15:
        raise RuntimeError(
            "utility-office window leaves the wall height: "
            f"minimum={window_minimum.z:.3f} maximum={window_maximum.z:.3f}"
        )
    if roof_minimum.z < 2.85 or roof_maximum.z > 3.25:
        raise RuntimeError(
            "utility-office roof is not seated at the storey top: "
            f"minimum={roof_minimum.z:.3f} maximum={roof_maximum.z:.3f}"
        )


def validate_window_hall_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "window-hall":
        return

    windows = [obj for obj in objects if obj.type == "MESH" and "IndWindow" in obj.name]
    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    if len(windows) != 3 or any("IndWindowBFull" not in window.name for window in windows):
        raise RuntimeError("window-hall must use three single-storey windows")
    if len(roofs) != 8:
        raise RuntimeError("window-hall must contain eight roof modules")

    _, hall_maximum = mesh_bounds(objects)
    window_minimum, window_maximum = mesh_bounds(windows)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    if hall_maximum.z > 3.25:
        raise RuntimeError(f"window-hall exceeds one storey: maximum={hall_maximum.z:.3f}")
    if window_minimum.z < -0.005 or window_maximum.z > 3.15:
        raise RuntimeError(
            "window-hall windows leave the wall height: "
            f"minimum={window_minimum.z:.3f} maximum={window_maximum.z:.3f}"
        )
    if roof_minimum.z < 2.85 or roof_maximum.z > 3.25:
        raise RuntimeError(
            "window-hall roof is not seated at the storey top: "
            f"minimum={roof_minimum.z:.3f} maximum={roof_maximum.z:.3f}"
        )


def validate_service_hall_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "sawtooth-service-hall":
        return

    windows = [obj for obj in objects if obj.type == "MESH" and "IndWindow" in obj.name]
    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    if len(windows) != 2 or any("IndWindowBFull" not in window.name for window in windows):
        raise RuntimeError("service hall must use two single-storey front windows")
    if len(roofs) != 8 or any("IndRoofDarkGreyFull" not in roof.name for roof in roofs):
        raise RuntimeError("service hall must use eight closed flat-roof modules")
    _, hall_maximum = mesh_bounds(objects)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    if hall_maximum.z > 3.25 or roof_minimum.z < 2.85 or roof_maximum.z > 3.25:
        raise RuntimeError(
            "service hall roof is not seated at the wall top: "
            f"hall_top={hall_maximum.z:.3f} roof={roof_minimum.z:.3f}..{roof_maximum.z:.3f}"
        )


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


def build_assembly(assembly: Assembly) -> None:
    clear_scene()
    configure_scene()
    objects: list[bpy.types.Object] = []
    for index, module in enumerate(assembly.modules, start=1):
        objects.extend(import_module(module, index))
    palette = load_palette()
    ensure_palette_materials(objects, palette)
    root = normalize_assembly(objects, assembly)
    dimensions = validate_dimensions(objects, assembly)
    validate_arch_gateway_parts(objects, assembly)
    validate_elevated_walkway_parts(objects, assembly)
    validate_utility_office_parts(objects, assembly)
    validate_window_hall_parts(objects, assembly)
    validate_service_hall_parts(objects, assembly)
    validate_closed_building_perimeter(objects, assembly)
    mesh_count, triangle_count, material_count = mesh_statistics(objects)
    output_path = OUTPUT_DIR / assembly.output_name
    export_glb(root, objects, output_path)
    verified_dimensions, embedded_image_count = verify_glb(output_path, dimensions)
    print(
        "TREY_INDUSTRIAL_ASSET "
        f"slug={assembly.slug} "
        f"dimensions_m={dimensions.x:.3f}x{dimensions.y:.3f}x{dimensions.z:.3f} "
        f"verified_m={verified_dimensions.x:.3f}x{verified_dimensions.y:.3f}x{verified_dimensions.z:.3f} "
        f"modules={len(assembly.modules)} meshes={mesh_count} triangles={triangle_count} "
        f"materials={material_count} embedded_images={embedded_image_count} "
        f"bytes={output_path.stat().st_size}"
    )


def main() -> None:
    selected = parse_args()
    require_sources()
    for assembly in ASSEMBLIES:
        if assembly.slug in selected:
            build_assembly(assembly)
    print(f"TREY_INDUSTRIAL_PASS built={len(selected)} output={OUTPUT_DIR}")


if __name__ == "__main__":
    main()
