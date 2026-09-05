"""Build the authored FALLTIDE RECOVERY ARRAY extraction map.

Run with Blender 4.5 or newer:
    blender --background --python scripts/blender/build_orbital_complex.py

The map is an original DCC composition. Major structures and hero props are
instances or non-destructive edits of tracked authored sources. The meshes
created here are limited to terrain, roads, sea defenses, dry-dock hardscape,
minor luminaires, and gameplay markers; they are not box-built buildings.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Iterable, Sequence

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "orbital_complex"
WORLD_SOURCE_DIR = REPO_ROOT / "source_art" / "world" / "orbital_complex"
PREVIEW_DIR = WORLD_SOURCE_DIR / "previews"
SOURCE_BLEND = WORLD_SOURCE_DIR / "orbital_complex.blend"
OUTPUT_GLB = OUTPUT_DIR / "orbital_complex.glb"
BUILD_REPORT = WORLD_SOURCE_DIR / "build_report.json"

NASA_ROOT = REPO_ROOT / "source_art" / "third_party" / "nasa_3d"
TREY_ROOT = REPO_ROOT / "assets" / "models" / "trey_modular_industrial"
OPERATIONS_ROOT = REPO_ROOT / "assets" / "models" / "operations_office"
MAJADROID_ROOT = REPO_ROOT / "assets" / "models" / "majadroid_construction_site"
TEXTURE_ROOT = REPO_ROOT / "assets" / "textures"

ROOT_NAME = "FalltideRecoveryArray"
MAP_SIZE = (340.0, 320.0)
MAP_CENTER_BLENDER = Vector((0.0, 60.0, 0.0))
MAP_CENTER_GODOT = (0.0, 0.0, -60.0)
ACQUISITION_DATE = "2026-09-01"
# The underground composition keeps the source pack's authored atlas materials
# so the industrial halls retain their silhouette and colour blocking.  The
# original open-air builder can still opt into its deterministic palette by
# leaving this disabled; the underground wrapper enables it explicitly.
PRESERVE_AUTHORED_MATERIALS = False

INTERACTIVE_NODES = {
    "DishYaw",
    "DishPitch",
    "TideGateLeft",
    "TideGateRight",
    "VaultDoorLeft",
    "VaultDoorRight",
    "UpperBypassBarrier",
    "PowerZone_Blackout",
    "PowerZone_Powered",
    "AlarmLight_Central",
    "AlarmLight_Breaker",
    "AlarmLight_Archive",
    "AlarmLight_TideGate",
}

GAMEPLAY_ANCHORS = {
    "POI_IntakeCauseway": (0.0, -68.0, 1.0),
    "POI_CapsuleDrydock": (0.0, 30.0, -3.2),
    "POI_BreakerYard": (-96.0, 60.0, 1.0),
    "POI_QuarantineArchive": (100.0, 70.0, 1.0),
    "POI_TelemetryDish": (0.0, 83.0, 1.0),
    "POI_TideGate": (0.0, 187.0, 1.0),
    "Spawn_SouthWest": (-115.0, -60.0, 1.0),
    "Spawn_SouthEast": (118.0, -50.0, 1.0),
    "Spawn_WestService": (-145.0, 110.0, 1.0),
    "Spawn_EastService": (145.0, 125.0, 1.0),
    "Extraction_TideGate": (0.0, 202.0, 1.0),
    "Extraction_MaintenanceSkiff": (-142.0, 178.0, 1.0),
}


@dataclass(frozen=True)
class AssetSpec:
    key: str
    path: Path
    creator: str
    title: str
    license_name: str
    source_url: str
    default_material: str | None = None


@dataclass(frozen=True)
class MeshPrototype:
    name: str
    data: bpy.types.Mesh
    matrix: Matrix


@dataclass
class AssetTemplate:
    spec: AssetSpec
    meshes: list[MeshPrototype]
    dimensions: Vector


@dataclass(frozen=True)
class Placement:
    asset: str
    name: str
    parent: str
    location: tuple[float, float, float]
    yaw: float = 0.0
    scale: tuple[float, float, float] = (1.0, 1.0, 1.0)


ASSETS = {
    "operations_office": AssetSpec(
        "operations_office",
        OPERATIONS_ROOT / "operations_office_set.glb",
        "Trey Ramm / minime453 and Kenney",
        "Special Operations authored command hall",
        "CC0-1.0",
        "https://opengameart.org/content/modular-industrial-kit",
    ),
    "dish": AssetSpec(
        "dish",
        NASA_ROOT / "nasa_70_meter_dish.glb",
        "NASA",
        "70 Meter Dish",
        "NASA media usage guidelines; U.S. Government work",
        "https://science.nasa.gov/3d-resources/70-meter-dish/",
        "DishWeatheredWhite",
    ),
    "capsule": AssetSpec(
        "capsule",
        NASA_ROOT / "nasa_orion_capsule_no_fbc.stl",
        "NASA",
        "Orion Capsule source mesh",
        "NASA media usage guidelines; U.S. Government work",
        "https://science.nasa.gov/3d-resources/orion-capsule/",
        "CapsuleCeramic",
    ),
}

TEXTURE_SOURCES = {
    "concrete_floor": {
        "creator": "eye-candy.xyz",
        "source_url": "https://polyhaven.com/a/concrete_floor",
        "license": "CC0-1.0",
        "acquired": "2026-08-06",
    },
    "asphalt_03": {
        "creator": "Charlotte Baglioni and Dario Barresi",
        "source_url": "https://polyhaven.com/a/asphalt_03",
        "license": "CC0-1.0",
        "acquired": "2026-08-06",
    },
    "rusty_painted_metal": {
        "creator": "Amal Kumar",
        "source_url": "https://polyhaven.com/a/rusty_painted_metal",
        "license": "CC0-1.0",
        "acquired": "2026-08-06",
    },
}


def register_glb_asset(
    key: str,
    root: Path,
    filename: str,
    creator: str,
    title: str,
    license_name: str,
    source_url: str,
    material: str | None,
) -> None:
    ASSETS[key] = AssetSpec(
        key,
        root / filename,
        creator,
        title,
        license_name,
        source_url,
        material,
    )


TREY_SOURCE_URL = "https://opengameart.org/content/modular-industrial-kit"
for _key, _filename, _material in (
    ("reactor_annex", "reactor-annex.glb", "FadedAerospaceWhite"),
    ("turbine_workshop", "turbine-workshop.glb", "OxidizedRedSteel"),
    ("switchgear_hall", "switchgear-hall.glb", "FadedAerospaceWhite"),
    ("boiler_workshop", "boiler-workshop.glb", "OxidizedRedSteel"),
    ("pump_house", "pump-house.glb", "FadedAerospaceWhite"),
    ("transformer_works", "transformer-works.glb", "OxidizedRedSteel"),
    ("glassworks_office", "glassworks-office.glb", "FadedAerospaceWhite"),
    ("cooling_hall", "cooling-service-hall.glb", "CeramicCyan"),
    ("control_room", "control-room.glb", "FadedAerospaceWhite"),
    ("maintenance_depot", "maintenance-depot.glb", "OxidizedRedSteel"),
    ("foundry_warehouse", "foundry-warehouse.glb", "WetBlackMetal"),
    ("crew_canteen", "crew-canteen.glb", "FadedAerospaceWhite"),
    ("elevated_walkway", "elevated-walkway.glb", "OxidizedRedSteel"),
    ("east_gate", "east-security-gate.glb", "OxidizedRedSteel"),
    ("west_gate", "west-service-gate.glb", "FadedAerospaceWhite"),
    ("arch_gateway", "arch-gateway.glb", "OxidizedRedSteel"),
):
    register_glb_asset(
        _key,
        TREY_ROOT,
        _filename,
        "Trey Ramm / minime453",
        "Modular Industrial Pieces authored composition",
        "CC0-1.0",
        TREY_SOURCE_URL,
        _material,
    )

MAJADROID_SOURCE_URL = "https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0"
for _key, _filename, _material in (
    ("construction_crane", "crane-on-ground.glb", "OxidizedRedSteel"),
    ("container_office", "containers-office.glb", "FadedAerospaceWhite"),
    ("cargo_containers", "containers-cargo.glb", "CeramicCyan"),
    ("construction_materials", "construction-materials.glb", "WetBlackMetal"),
):
    register_glb_asset(
        _key,
        MAJADROID_ROOT,
        _filename,
        "Majadroid",
        "Construction Site Assets",
        "CC0-1.0",
        MAJADROID_SOURCE_URL,
        _material,
    )


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.images,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def configure_scene() -> None:
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 18
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_percentage = 100
    scene.render.use_file_extension = True
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    if background is None:
        raise RuntimeError("Expected a World Background node")
    background.inputs["Color"].default_value = (0.024, 0.038, 0.055, 1.0)
    background.inputs["Strength"].default_value = 0.52


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    roughness: float,
    metallic: float = 0.0,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Material {name} has no Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    if emission is not None:
        emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        if emission_input is not None:
            emission_input.default_value = emission
        strength_input = principled.inputs.get("Emission Strength")
        if strength_input is not None:
            strength_input.default_value = emission_strength
    material.diffuse_color = color
    return material


def make_pbr_texture_material(
    name: str,
    prefix: str,
    metallic: float,
    tint: tuple[float, float, float, float] | None = None,
) -> bpy.types.Material:
    paths = {
        "base": TEXTURE_ROOT / f"{prefix}_diff_1k.jpg",
        "normal": TEXTURE_ROOT / f"{prefix}_normal_1k.jpg",
        "roughness": TEXTURE_ROOT / f"{prefix}_rough_1k.jpg",
    }
    missing = [str(path) for path in paths.values() if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing PBR texture maps: " + ", ".join(missing))
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Textured material {name} has no Principled BSDF")
    principled.inputs["Metallic"].default_value = metallic
    base_texture = nodes.new("ShaderNodeTexImage")
    base_texture.name = f"{name}_BaseColor"
    base_texture.image = bpy.data.images.load(str(paths["base"]), check_existing=True)
    base_texture.image.pack()
    roughness_texture = nodes.new("ShaderNodeTexImage")
    roughness_texture.name = f"{name}_Roughness"
    roughness_texture.image = bpy.data.images.load(str(paths["roughness"]), check_existing=True)
    roughness_texture.image.colorspace_settings.name = "Non-Color"
    roughness_texture.image.pack()
    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.name = f"{name}_Normal"
    normal_texture.image = bpy.data.images.load(str(paths["normal"]), check_existing=True)
    normal_texture.image.colorspace_settings.name = "Non-Color"
    normal_texture.image.pack()
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.62
    if tint is None:
        links.new(base_texture.outputs["Color"], principled.inputs["Base Color"])
    else:
        tint_multiply = nodes.new("ShaderNodeMixRGB")
        tint_multiply.name = f"{name}_SurfaceTint"
        tint_multiply.blend_type = "MULTIPLY"
        tint_multiply.inputs[0].default_value = 1.0
        tint_multiply.inputs[2].default_value = tint
        links.new(base_texture.outputs["Color"], tint_multiply.inputs[1])
        links.new(tint_multiply.outputs["Color"], principled.inputs["Base Color"])
    links.new(roughness_texture.outputs["Color"], principled.inputs["Roughness"])
    links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    return material


def build_materials() -> dict[str, bpy.types.Material]:
    materials = {
        # Keep the underground pool visibly distinct from the black metal
        # rim in offline previews.  The runtime shader still drives the
        # animated surface in Godot, but this authored albedo preserves the
        # water read in the source BLEND/GLB as well.
        "StormWater": make_material("StormWater", (0.018, 0.105, 0.155, 1.0), 0.08, 0.14),
        "WetConcrete": make_pbr_texture_material(
            "WetConcrete", "concrete_floor", 0.06, (0.30, 0.34, 0.37, 1.0)
        ),
        "WetBlackMetal": make_material("WetBlackMetal", (0.055, 0.064, 0.070, 1.0), 0.48, 0.68),
        "OxidizedRedSteel": make_pbr_texture_material("OxidizedRedSteel", "rusty_painted_metal", 0.52),
        "FadedAerospaceWhite": make_material(
            "FadedAerospaceWhite", (0.66, 0.68, 0.64, 1.0), 0.56, 0.18
        ),
        "CeramicCyan": make_material("CeramicCyan", (0.032, 0.30, 0.33, 1.0), 0.42, 0.24),
        "RoadAsphalt": make_pbr_texture_material("RoadAsphalt", "asphalt_03", 0.04),
        "LaneMarking": make_material("LaneMarking", (0.66, 0.55, 0.20, 1.0), 0.68, 0.05),
        "SafetyOrange": make_material("SafetyOrange", (0.82, 0.20, 0.025, 1.0), 0.46, 0.18),
        "SodiumEmission": make_material(
            "SodiumEmission",
            (0.64, 0.16, 0.015, 1.0),
            0.28,
            0.15,
            (1.0, 0.16, 0.012, 1.0),
            5.0,
        ),
        "CyanEmission": make_material(
            "CyanEmission",
            (0.01, 0.35, 0.42, 1.0),
            0.24,
            0.10,
            (0.01, 0.72, 1.0, 1.0),
            3.5,
        ),
        "BlackoutGlass": make_material("BlackoutGlass", (0.006, 0.009, 0.011, 1.0), 0.18, 0.40),
        "DishWeatheredWhite": make_material(
            "DishWeatheredWhite", (0.62, 0.61, 0.56, 1.0), 0.63, 0.22
        ),
        "DishOxide": make_material("DishOxide", (0.34, 0.075, 0.040, 1.0), 0.61, 0.51),
        "CapsuleCeramic": make_material("CapsuleCeramic", (0.36, 0.40, 0.39, 1.0), 0.66, 0.14),
        "CapsuleScorch": make_material("CapsuleScorch", (0.018, 0.014, 0.012, 1.0), 0.88, 0.04),
    }
    storm_water = materials["StormWater"]
    storm_water.diffuse_color = (0.018, 0.105, 0.155, 1.0)
    storm_water_principled = storm_water.node_tree.nodes.get("Principled BSDF")
    if storm_water_principled is not None:
        storm_water_principled.inputs["Base Color"].default_value = (0.018, 0.105, 0.155, 1.0)
        storm_water_principled.inputs["Metallic"].default_value = 0.08
        storm_water_principled.inputs["Roughness"].default_value = 0.11
        coat_weight = storm_water_principled.inputs.get("Coat Weight")
        coat_roughness = storm_water_principled.inputs.get("Coat Roughness")
        if coat_weight is not None:
            coat_weight.default_value = 0.42
        if coat_roughness is not None:
            coat_roughness.default_value = 0.09
        # The pool is deliberately dark and legible at player distance. A
        # low-contrast procedural breakup keeps it from reading as a blue
        # decal without introducing emissive rings or arcade-like markers.
        nodes = storm_water.node_tree.nodes
        links = storm_water.node_tree.links
        noise = nodes.new("ShaderNodeTexNoise")
        noise.name = "StormWaterSurfaceNoise"
        noise.inputs["Scale"].default_value = 0.22
        noise.inputs["Detail"].default_value = 3.0
        noise.inputs["Roughness"].default_value = 0.68
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.name = "StormWaterSurfaceColor"
        ramp.color_ramp.elements[0].position = 0.22
        ramp.color_ramp.elements[0].color = (0.008, 0.050, 0.075, 1.0)
        ramp.color_ramp.elements[1].position = 0.78
        ramp.color_ramp.elements[1].color = (0.028, 0.145, 0.19, 1.0)
        bump = nodes.new("ShaderNodeBump")
        bump.name = "StormWaterSurfaceBump"
        bump.inputs["Strength"].default_value = 0.13
        bump.inputs["Distance"].default_value = 0.08
        links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], storm_water_principled.inputs["Base Color"])
        links.new(noise.outputs["Fac"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], storm_water_principled.inputs["Normal"])
    wet_concrete_principled = materials["WetConcrete"].node_tree.nodes.get("Principled BSDF")
    if wet_concrete_principled is not None:
        coat_weight = wet_concrete_principled.inputs.get("Coat Weight")
        coat_roughness = wet_concrete_principled.inputs.get("Coat Roughness")
        if coat_weight is not None:
            coat_weight.default_value = 0.24
        if coat_roughness is not None:
            coat_roughness.default_value = 0.19
    return materials


def require_sources() -> None:
    missing = [str(spec.path) for spec in ASSETS.values() if not spec.path.is_file()]
    if missing:
        raise FileNotFoundError("Missing authored source assets: " + ", ".join(missing))


def points_bounds(objects: Iterable[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not points:
        raise RuntimeError("Cannot calculate bounds without visible mesh objects")
    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return minimum, maximum


def import_asset_template(
    spec: AssetSpec, materials: dict[str, bpy.types.Material]
) -> AssetTemplate:
    before = set(bpy.data.objects)
    if spec.path.suffix.lower() == ".stl":
        result = bpy.ops.wm.stl_import(filepath=str(spec.path))
    else:
        result = bpy.ops.import_scene.gltf(filepath=str(spec.path), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {spec.path}: {result}")
    imported = [obj for obj in bpy.data.objects if obj not in before]
    mesh_objects = [obj for obj in imported if obj.type == "MESH"]
    if spec.key == "dish":
        mesh_objects = [obj for obj in mesh_objects if "70m" in obj.name.lower()]
    elif spec.key == "capsule":
        mesh_objects = [obj for obj in mesh_objects if "capsule" in obj.name.lower()]
    if not mesh_objects:
        raise RuntimeError(f"No expected authored meshes found in {spec.path}")

    # Static source compositions arrive as deep glTF hierarchies (the command
    # hall alone has hundreds of placement nodes). Consolidate each source into
    # one multi-material mesh before map instancing. This preserves every
    # authored triangle/UV/material while preventing the finished map from
    # shipping a four-digit MeshInstance count. Movable map parts are still
    # instantiated under separate Dish/Gate/Vault roots later in the build.
    if len(mesh_objects) > 1:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in mesh_objects:
            world = obj.matrix_world.copy()
            obj.parent = None
            obj.matrix_world = world
            obj.select_set(True)
        active = mesh_objects[0]
        bpy.context.view_layer.objects.active = active
        result = bpy.ops.object.join()
        if "FINISHED" not in result:
            raise RuntimeError(f"Could not consolidate authored source {spec.path}: {result}")
        mesh_objects = [active]
        old_materials = list(active.data.materials)
        unique_materials: list[bpy.types.Material | None] = []
        remap: dict[int, int] = {}
        for index, material in enumerate(old_materials):
            if material not in unique_materials:
                unique_materials.append(material)
            remap[index] = unique_materials.index(material)
        for polygon in active.data.polygons:
            polygon.material_index = remap.get(polygon.material_index, 0)
        active.data.materials.clear()
        for material in unique_materials:
            if material is not None:
                active.data.materials.append(material)

    minimum, maximum = points_bounds(mesh_objects)
    center = (minimum + maximum) * 0.5
    normalization = Matrix.Translation(Vector((-center.x, -center.y, -minimum.z)))
    prototypes: list[MeshPrototype] = []
    for index, obj in enumerate(mesh_objects, start=1):
        data = obj.data
        if spec.default_material is not None and not PRESERVE_AUTHORED_MATERIALS:
            data.materials.clear()
            data.materials.append(materials[spec.default_material])
            for polygon in data.polygons:
                polygon.material_index = 0
        data["authored_source"] = spec.path.relative_to(REPO_ROOT).as_posix()
        data["source_creator"] = spec.creator
        data["source_license"] = spec.license_name
        prototypes.append(
            MeshPrototype(
                f"{spec.key}_{index:02d}_{obj.name}",
                data,
                normalization @ obj.matrix_world,
            )
        )

    remaining_imported = [obj for obj in bpy.data.objects if obj not in before]
    for obj in remaining_imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    return AssetTemplate(spec, prototypes, maximum - minimum)


def load_templates(materials: dict[str, bpy.types.Material]) -> dict[str, AssetTemplate]:
    templates = {key: import_asset_template(spec, materials) for key, spec in ASSETS.items()}
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)
    # Do not purge zero-user palette entries here. Several authored hardscape
    # materials are intentionally created before source import and receive
    # their first user while the original map composition is assembled below.
    return templates


def create_empty(
    name: str,
    parent: bpy.types.Object | None,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 1.5
    obj.location = location
    obj.parent = parent
    return obj


def instantiate_asset(
    template: AssetTemplate,
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    yaw: float = 0.0,
    scale: tuple[float, float, float] = (1.0, 1.0, 1.0),
    preserve_root_name: bool = False,
) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    root = create_empty(name, parent, location)
    root.rotation_euler.z = math.radians(yaw)
    root.scale = scale
    root["authored_source"] = template.spec.path.relative_to(REPO_ROOT).as_posix()
    root["source_creator"] = template.spec.creator
    root["source_asset"] = template.spec.title
    root["source_license"] = template.spec.license_name
    root["source_url"] = template.spec.source_url
    root["acquired"] = ACQUISITION_DATE if template.spec.creator == "NASA" else "2026-08-27"
    root["dcc_assembly"] = True
    objects: list[bpy.types.Object] = []
    for index, prototype in enumerate(template.meshes, start=1):
        obj_name = f"{name}_Mesh_{index:02d}"
        if preserve_root_name and len(template.meshes) == 1:
            obj_name = f"{name}_AuthoredMesh"
        obj = bpy.data.objects.new(obj_name, prototype.data)
        bpy.context.collection.objects.link(obj)
        obj.parent = root
        obj.matrix_local = prototype.matrix.copy()
        obj["authored_source"] = root["authored_source"]
        obj["source_creator"] = template.spec.creator
        obj["source_license"] = template.spec.license_name
        obj["authored_instance"] = name
        objects.append(obj)
    return root, objects


def rounded_rectangle(
    center: tuple[float, float], width: float, depth: float, radius: float, segments: int = 4
) -> list[tuple[float, float]]:
    cx, cy = center
    radius = min(radius, width * 0.5, depth * 0.5)
    corners = (
        (cx + width * 0.5 - radius, cy + depth * 0.5 - radius, 0.0),
        (cx - width * 0.5 + radius, cy + depth * 0.5 - radius, 90.0),
        (cx - width * 0.5 + radius, cy - depth * 0.5 + radius, 180.0),
        (cx + width * 0.5 - radius, cy - depth * 0.5 + radius, 270.0),
    )
    points: list[tuple[float, float]] = []
    for corner_x, corner_y, start in corners:
        for step in range(segments + 1):
            angle = math.radians(start + step * 90.0 / segments)
            points.append((corner_x + radius * math.cos(angle), corner_y + radius * math.sin(angle)))
    return points


def assign_world_scale_uv(mesh: bpy.types.Mesh, meters_per_tile: float = 8.0) -> None:
    """Create deterministic triplanar-style UVs for authored hardscape meshes."""
    layer = mesh.uv_layers.new(name="FalltideUV")
    scale = 1.0 / meters_per_tile
    for polygon in mesh.polygons:
        normal = polygon.normal
        for loop_index in polygon.loop_indices:
            coordinate = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if abs(normal.z) >= max(abs(normal.x), abs(normal.y)):
                uv = (coordinate.x * scale, coordinate.y * scale)
            elif abs(normal.x) >= abs(normal.y):
                uv = (coordinate.y * scale, coordinate.z * scale)
            else:
                uv = (coordinate.x * scale, coordinate.z * scale)
            layer.data[loop_index].uv = uv


def create_extruded_polygon(
    name: str,
    points: Sequence[tuple[float, float]],
    bottom: float,
    top: float,
    material: bpy.types.Material,
    parent: bpy.types.Object,
    source_note: str,
) -> bpy.types.Object:
    count = len(points)
    vertices = [(x, y, bottom) for x, y in points] + [(x, y, top) for x, y in points]
    faces: list[tuple[int, ...]] = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    assign_world_scale_uv(mesh, 9.0)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["authored_source"] = source_note
    obj["source_creator"] = "Operation Steel Tide art team"
    obj["source_license"] = "MIT"
    obj["geometry_role"] = "terrain_or_hardscape"
    bevel = obj.modifiers.new("DCC edge weathering", "BEVEL")
    bevel.width = 0.18
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    return obj


def create_ring(
    name: str,
    outer: Sequence[tuple[float, float]],
    inner: Sequence[tuple[float, float]],
    bottom: float,
    top: float,
    material: bpy.types.Material,
    parent: bpy.types.Object,
) -> bpy.types.Object:
    if len(outer) != len(inner):
        raise ValueError("Ring outlines must use matching segment counts")
    count = len(outer)
    vertices = (
        [(x, y, bottom) for x, y in outer]
        + [(x, y, top) for x, y in outer]
        + [(x, y, bottom) for x, y in inner]
        + [(x, y, top) for x, y in inner]
    )
    faces: list[tuple[int, ...]] = []
    for index in range(count):
        nxt = (index + 1) % count
        ob, ot, ib, it = index, count + index, count * 2 + index, count * 3 + index
        nob, not_, nib, nit = nxt, count + nxt, count * 2 + nxt, count * 3 + nxt
        faces.extend(((ot, not_, nit, it), (ob, nob, not_, ot), (ib, it, nit, nib)))
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    assign_world_scale_uv(mesh, 7.5)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["authored_source"] = "Original DCC-modeled dry-dock hardscape"
    obj["source_creator"] = "Operation Steel Tide art team"
    obj["source_license"] = "MIT"
    obj["geometry_role"] = "terrain_or_hardscape"
    return obj


def ribbon_offsets(points: Sequence[tuple[float, float]], half_width: float) -> list[tuple[float, float]]:
    offsets: list[tuple[float, float]] = []
    for index, _point in enumerate(points):
        previous = Vector(points[max(0, index - 1)])
        following = Vector(points[min(len(points) - 1, index + 1)])
        tangent = following - previous
        if tangent.length < 0.001:
            tangent = Vector((0.0, 1.0))
        tangent.normalize()
        normal = Vector((-tangent.y, tangent.x)) * half_width
        offsets.append((normal.x, normal.y))
    return offsets


def create_ribbon_surface(
    name: str,
    points: Sequence[tuple[float, float]],
    width: float,
    z: float,
    material: bpy.types.Material,
    parent: bpy.types.Object,
    role: str,
) -> bpy.types.Object:
    offsets = ribbon_offsets(points, width * 0.5)
    vertices: list[tuple[float, float, float]] = []
    for (x, y), (ox, oy) in zip(points, offsets):
        vertices.extend(((x - ox, y - oy, z), (x + ox, y + oy, z)))
    faces = [(index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2) for index in range(len(points) - 1)]
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    assign_world_scale_uv(mesh, 6.0)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["authored_source"] = "Original DCC-modeled route surface"
    obj["source_creator"] = "Operation Steel Tide art team"
    obj["source_license"] = "MIT"
    obj["geometry_role"] = role
    return obj


def create_ribbon_prism(
    name: str,
    points: Sequence[tuple[float, float]],
    width: float,
    bottom: float,
    top: float,
    material: bpy.types.Material,
    parent: bpy.types.Object,
) -> bpy.types.Object:
    offsets = ribbon_offsets(points, width * 0.5)
    vertices: list[tuple[float, float, float]] = []
    for (x, y), (ox, oy) in zip(points, offsets):
        vertices.extend(
            (
                (x - ox, y - oy, bottom),
                (x + ox, y + oy, bottom),
                (x - ox, y - oy, top),
                (x + ox, y + oy, top),
            )
        )
    faces: list[tuple[int, ...]] = []
    for index in range(len(points) - 1):
        a = index * 4
        b = (index + 1) * 4
        faces.extend(
            (
                (a + 2, a + 3, b + 3, b + 2),
                (a, b, b + 1, a + 1),
                (a, a + 2, b + 2, b),
                (a + 1, b + 1, b + 3, a + 3),
            )
        )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    assign_world_scale_uv(mesh, 6.0)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["authored_source"] = "Original DCC-modeled storm sea defense"
    obj["source_creator"] = "Operation Steel Tide art team"
    obj["source_license"] = "MIT"
    obj["geometry_role"] = "terrain_or_hardscape"
    bevel = obj.modifiers.new("Wave battered bevel", "BEVEL")
    bevel.width = 0.22
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    return obj


def create_water(
    parent: bpy.types.Object, material: bpy.types.Material, columns: int = 18, rows: int = 17
) -> bpy.types.Object:
    min_x, max_x = -170.0, 170.0
    min_y, max_y = -100.0, 220.0
    vertices: list[tuple[float, float, float]] = []
    for row in range(rows):
        y = min_y + (max_y - min_y) * row / (rows - 1)
        for column in range(columns):
            x = min_x + (max_x - min_x) * column / (columns - 1)
            edge = column in {0, columns - 1} or row in {0, rows - 1}
            wave = 0.0 if edge else 0.18 * math.sin(x * 0.071 + y * 0.053) + 0.08 * math.sin(y * 0.16)
            vertices.append((x, y, -7.2 + wave))
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows - 1):
        for column in range(columns - 1):
            index = row * columns + column
            faces.append((index, index + 1, index + columns + 1, index + columns))
    mesh = bpy.data.meshes.new("StormBasinWater_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new("StormBasinWater", mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["authored_source"] = "Original DCC-modeled storm-water surface"
    obj["source_creator"] = "Operation Steel Tide art team"
    obj["source_license"] = "MIT"
    obj["geometry_role"] = "terrain"
    return obj


def create_beacon_mesh(
    name: str,
    parent: bpy.types.Object,
    material: bpy.types.Material,
    radius: float = 0.48,
    height: float = 1.4,
) -> bpy.types.Object:
    segments = 16
    vertices: list[tuple[float, float, float]] = []
    for z, ring_radius in ((0.0, radius * 0.72), (0.18, radius), (height, radius * 0.78)):
        for step in range(segments):
            angle = math.tau * step / segments
            vertices.append((math.cos(angle) * ring_radius, math.sin(angle) * ring_radius, z))
    faces: list[tuple[int, ...]] = []
    for ring in range(2):
        for step in range(segments):
            nxt = (step + 1) % segments
            a = ring * segments + step
            b = ring * segments + nxt
            c = (ring + 1) * segments + nxt
            d = (ring + 1) * segments + step
            faces.append((a, b, c, d))
    faces.extend((tuple(reversed(range(segments))), tuple(range(segments * 2, segments * 3))))
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    mesh.update()
    obj = bpy.data.objects.new(f"{name}_Lens", mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["authored_source"] = "Original DCC-modeled minor emergency luminaire"
    obj["source_creator"] = "Operation Steel Tide art team"
    obj["source_license"] = "MIT"
    obj["geometry_role"] = "minor_prop"
    return obj


def create_root_hierarchy() -> tuple[bpy.types.Object, dict[str, bpy.types.Object]]:
    root = create_empty(ROOT_NAME, None)
    root["map_id"] = "orbital_complex"
    root["display_name"] = "FALLTIDE RECOVERY ARRAY"
    root["original_composition"] = True
    root["coordinate_system"] = "Blender Z-up; glTF Y-up; Godot X/Y/Z = Blender X/Z/-Y"
    root["map_width_m"] = MAP_SIZE[0]
    root["map_depth_m"] = MAP_SIZE[1]
    root["map_center_godot"] = "0,0,-60"
    root["art_direction"] = "oxidized red steel, faded aerospace white, wet black concrete, cyan ceramic, sodium orange"
    root["license_summary"] = "Original MIT composition using CC0 and NASA media-guideline sources"
    groups: dict[str, bpy.types.Object] = {}
    for name in (
        "Environment_Static",
        "District_IntakeCauseway",
        "District_CapsuleDrydock",
        "District_BreakerYard",
        "District_QuarantineArchive",
        "District_TelemetrySpine",
        "District_TideGate",
        "PowerZone_Blackout",
        "PowerZone_Powered",
        "GameplayAnchors",
    ):
        groups[name] = create_empty(name, root)
    groups["PowerZone_Blackout"]["default_visible"] = True
    groups["PowerZone_Blackout"]["runtime_state"] = "outage"
    groups["PowerZone_Powered"]["default_visible"] = False
    groups["PowerZone_Powered"]["runtime_state"] = "restored_power"
    return root, groups


def build_hardscape(groups: dict[str, bpy.types.Object], materials: dict[str, bpy.types.Material]) -> None:
    static = groups["Environment_Static"]
    create_water(static, materials["StormWater"])
    deck_outline = [
        (-154.0, -84.0),
        (-118.0, -94.0),
        (-72.0, -88.0),
        (-34.0, -96.0),
        (34.0, -96.0),
        (74.0, -86.0),
        (122.0, -92.0),
        (154.0, -76.0),
        (158.0, -22.0),
        (150.0, 35.0),
        (158.0, 92.0),
        (150.0, 150.0),
        (128.0, 207.0),
        (62.0, 213.0),
        (0.0, 207.0),
        (-62.0, 214.0),
        (-128.0, 207.0),
        (-154.0, 152.0),
        (-160.0, 94.0),
        (-151.0, 34.0),
        (-159.0, -22.0),
    ]
    south_deck = [
        (-154.0, -84.0), (-118.0, -94.0), (-72.0, -88.0), (-34.0, -96.0),
        (34.0, -96.0), (74.0, -86.0), (122.0, -92.0), (154.0, -76.0),
        (158.0, -22.0), (154.0, 4.0), (-153.0, 4.0), (-159.0, -22.0),
    ]
    west_middle_deck = [(-153.0, 4.0), (-36.0, 4.0), (-36.0, 60.0), (-157.0, 60.0)]
    east_middle_deck = [(36.0, 4.0), (154.0, 4.0), (157.0, 60.0), (36.0, 60.0)]
    north_deck = [
        (-157.0, 60.0), (157.0, 60.0), (158.0, 92.0), (150.0, 150.0),
        (128.0, 207.0), (62.0, 213.0), (0.0, 207.0), (-62.0, 214.0),
        (-128.0, 207.0), (-154.0, 152.0), (-160.0, 94.0),
    ]
    for name, outline in (
        ("RecoveryDeckSouth", south_deck),
        ("RecoveryDeckWestDock", west_middle_deck),
        ("RecoveryDeckEastDock", east_middle_deck),
        ("RecoveryDeckNorth", north_deck),
    ):
        create_extruded_polygon(
            name,
            outline,
            -6.2,
            0.0,
            materials["WetConcrete"],
            static,
            "Original DCC-modeled reclaimed storm-barrier terrain with open dry dock",
        )
    seawall_path = deck_outline + [deck_outline[0]]
    create_ribbon_prism(
        "StormwardSeawall",
        seawall_path,
        4.2,
        -1.6,
        2.2,
        materials["WetBlackMetal"],
        static,
    )

    create_extruded_polygon(
        "IntakeCausewayFoundation",
        [(-23.0, -98.0), (23.0, -98.0), (28.0, -47.0), (18.0, -34.0), (-18.0, -34.0), (-28.0, -47.0)],
        -2.0,
        0.75,
        materials["WetConcrete"],
        groups["District_IntakeCauseway"],
        "Original DCC-modeled intake causeway",
    )

    outer_dock = rounded_rectangle((0.0, 30.0), 72.0, 55.0, 8.0, 6)
    inner_dock = rounded_rectangle((0.0, 30.0), 51.0, 35.0, 5.0, 6)
    create_ring(
        "CapsuleDrydockRim",
        outer_dock,
        inner_dock,
        -4.3,
        1.1,
        materials["WetBlackMetal"],
        groups["District_CapsuleDrydock"],
    )
    create_extruded_polygon(
        "CapsuleDrydockFloor",
        rounded_rectangle((0.0, 30.0), 48.0, 32.0, 4.0, 6),
        -5.0,
        -3.9,
        materials["WetConcrete"],
        groups["District_CapsuleDrydock"],
        "Original DCC-modeled sloped dry-dock floor",
    )
    create_ribbon_surface(
        "CapsuleImpactScorch",
        [(-12.0, 16.0), (-4.0, 24.0), (3.0, 32.0), (12.0, 43.0)],
        6.5,
        -3.82,
        materials["CapsuleScorch"],
        groups["District_CapsuleDrydock"],
        "weathering_decal",
    )

    road_parent = groups["Environment_Static"]
    routes = {
        "MainServiceSpineSouth": [(0.0, -92.0), (2.0, -55.0), (-2.0, -6.0)],
        "MainServiceSpineNorth": [(0.0, 64.0), (0.0, 112.0), (0.0, 202.0)],
        "WestDrydockBypass": [(-2.0, -6.0), (-43.0, 5.0), (-43.0, 56.0), (0.0, 68.0)],
        "EastDrydockBypass": [(2.0, -6.0), (43.0, 5.0), (43.0, 56.0), (0.0, 68.0)],
        "BreakerLoop": [(0.0, -10.0), (-45.0, 3.0), (-96.0, 40.0), (-133.0, 82.0), (-120.0, 144.0), (-52.0, 126.0), (0.0, 112.0)],
        "ArchiveLoop": [(0.0, -10.0), (48.0, 5.0), (92.0, 36.0), (132.0, 82.0), (120.0, 146.0), (54.0, 126.0), (0.0, 112.0)],
        "NorthernCrossdeck": [(-132.0, 155.0), (-62.0, 150.0), (0.0, 160.0), (62.0, 150.0), (132.0, 158.0)],
    }
    for name, points in routes.items():
        create_ribbon_surface(name, points, 11.5, 0.08, materials["RoadAsphalt"], road_parent, "service_road")
        create_ribbon_surface(f"{name}_Centerline", points, 0.28, 0.11, materials["LaneMarking"], road_parent, "road_marking")

    west_pad = [(-146.0, 9.0), (-58.0, 3.0), (-44.0, 66.0), (-68.0, 123.0), (-140.0, 137.0), (-151.0, 80.0)]
    east_pad = [(146.0, 8.0), (60.0, 3.0), (46.0, 65.0), (68.0, 130.0), (141.0, 140.0), (153.0, 80.0)]
    create_extruded_polygon(
        "BreakerYardPad", west_pad, -0.3, 0.35, materials["WetConcrete"], groups["District_BreakerYard"], "Original DCC district hardscape"
    )
    create_extruded_polygon(
        "ArchivePad", east_pad, -0.3, 0.35, materials["WetConcrete"], groups["District_QuarantineArchive"], "Original DCC district hardscape"
    )


def authored_placements() -> list[Placement]:
    placements = [
        Placement("maintenance_depot", "IntakeMaintenanceWest", "District_IntakeCauseway", (-61.0, -58.0, 0.35), 14.0, (2.0, 2.0, 1.35)),
        Placement("maintenance_depot", "IntakeMaintenanceEast", "District_IntakeCauseway", (61.0, -56.0, 0.35), -16.0, (2.0, 2.0, 1.35)),
        Placement("arch_gateway", "IntakePortalWest", "District_IntakeCauseway", (-12.0, -49.0, 0.76), 90.0, (1.6, 1.6, 1.5)),
        Placement("arch_gateway", "IntakePortalEast", "District_IntakeCauseway", (12.0, -49.0, 0.76), 90.0, (1.6, 1.6, 1.5)),
        Placement("container_office", "IntakeCustomsOffice", "District_IntakeCauseway", (91.0, -47.0, 0.35), 164.0, (1.4, 1.4, 1.3)),
        Placement("cargo_containers", "IntakeCeramicCrates", "District_IntakeCauseway", (-92.0, -34.0, 0.35), 18.0, (1.8, 1.8, 1.8)),
        Placement("foundry_warehouse", "SouthwestReturnStores", "District_IntakeCauseway", (-129.0, -64.0, 0.35), 10.0, (1.55, 1.55, 1.45)),
        Placement("cooling_hall", "SoutheastCoolantStores", "District_IntakeCauseway", (128.0, -64.0, 0.35), -10.0, (1.55, 1.55, 1.45)),
        Placement("pump_house", "WestIntakeLift", "District_IntakeCauseway", (-71.0, -11.0, 0.35), 72.0, (1.75, 1.75, 1.45)),
        Placement("pump_house", "EastIntakeLift", "District_IntakeCauseway", (72.0, -10.0, 0.35), -72.0, (1.75, 1.75, 1.45)),
        Placement("boiler_workshop", "BreakerConversionHall", "District_BreakerYard", (-103.0, 47.0, 0.35), 20.0, (2.1, 2.1, 1.65)),
        Placement("transformer_works", "BreakerBusHall", "District_BreakerYard", (-126.0, 84.0, 0.35), -8.0, (1.9, 1.9, 1.65)),
        Placement("turbine_workshop", "BreakerRotorHouse", "District_BreakerYard", (-82.0, 82.0, 0.35), 31.0, (2.2, 2.2, 1.55)),
        Placement("switchgear_hall", "BreakerRelayHall", "District_BreakerYard", (-116.0, 19.0, 0.35), 92.0, (1.75, 1.75, 1.55)),
        Placement("foundry_warehouse", "BreakerSalvageWarehouse", "District_BreakerYard", (-119.0, 119.0, 0.35), -17.0, (1.8, 1.8, 1.55)),
        Placement("pump_house", "BreakerFloodPump", "District_BreakerYard", (-61.0, 35.0, 0.35), 65.0, (2.0, 2.0, 1.5)),
        Placement("crew_canteen", "BreakerCrewBlock", "District_BreakerYard", (-146.0, 42.0, 0.35), 88.0, (1.65, 1.65, 1.45)),
        Placement("maintenance_depot", "BreakerCableDepot", "District_BreakerYard", (-143.0, 137.0, 0.35), -84.0, (1.55, 1.55, 1.45)),
        Placement("reactor_annex", "BreakerShoreAnnex", "District_BreakerYard", (-78.0, -9.0, 0.35), 8.0, (1.65, 1.65, 1.45)),
        Placement("cargo_containers", "BreakerSalvageStacks", "District_BreakerYard", (-145.0, 105.0, 0.35), 34.0, (2.5, 2.5, 2.5)),
        Placement("construction_crane", "DrydockRecoveryCrane", "District_CapsuleDrydock", (-39.0, 35.0, 1.0), -12.0, (1.25, 1.25, 1.25)),
        Placement("construction_materials", "DrydockRiggingWest", "District_CapsuleDrydock", (-29.0, 3.0, 1.1), 22.0, (2.2, 2.2, 2.2)),
        Placement("construction_materials", "DrydockRiggingEast", "District_CapsuleDrydock", (30.0, 52.0, 1.1), -30.0, (2.0, 2.0, 2.0)),
        Placement("elevated_walkway", "DrydockAccessWest", "District_CapsuleDrydock", (-31.0, 30.0, -3.75), 90.0, (0.90, 0.90, 1.05)),
        Placement("elevated_walkway", "DrydockAccessEast", "District_CapsuleDrydock", (31.0, 30.0, -3.75), -90.0, (0.90, 0.90, 1.05)),
        Placement("operations_office", "QuarantineArchiveMain", "District_QuarantineArchive", (107.0, 73.0, 0.35), 180.0, (1.32, 1.32, 1.32)),
        Placement("control_room", "ArchiveTelemetryControl", "District_QuarantineArchive", (68.0, 42.0, 0.35), -42.0, (2.0, 2.0, 1.7)),
        Placement("glassworks_office", "ArchiveLabWing", "District_QuarantineArchive", (126.0, 30.0, 0.35), 74.0, (1.75, 1.75, 1.5)),
        Placement("cooling_hall", "ArchiveColdStore", "District_QuarantineArchive", (132.0, 117.0, 0.35), 101.0, (1.8, 1.8, 1.55)),
        Placement("crew_canteen", "ArchiveDeconBarracks", "District_QuarantineArchive", (77.0, 125.0, 0.35), -12.0, (1.9, 1.9, 1.55)),
        Placement("crew_canteen", "ArchiveShoreDormitory", "District_QuarantineArchive", (146.0, 47.0, 0.35), -88.0, (1.65, 1.65, 1.45)),
        Placement("maintenance_depot", "ArchiveSampleDepot", "District_QuarantineArchive", (143.0, 140.0, 0.35), 84.0, (1.55, 1.55, 1.45)),
        Placement("reactor_annex", "ArchiveShoreAnnex", "District_QuarantineArchive", (79.0, -7.0, 0.35), -8.0, (1.65, 1.65, 1.45)),
        Placement("cargo_containers", "ArchiveSealedStacks", "District_QuarantineArchive", (145.0, 106.0, 0.35), -34.0, (2.5, 2.5, 2.5)),
        Placement("reactor_annex", "TelemetryPowerAnnexWest", "District_TelemetrySpine", (-34.0, 95.0, 0.35), 77.0, (1.75, 1.75, 1.55)),
        Placement("reactor_annex", "TelemetryPowerAnnexEast", "District_TelemetrySpine", (35.0, 96.0, 0.35), -78.0, (1.75, 1.75, 1.55)),
        Placement("control_room", "TelemetrySignalHouse", "District_TelemetrySpine", (0.0, 129.0, 0.35), 180.0, (1.75, 1.75, 1.65)),
        Placement("switchgear_hall", "TelemetryLowerRelayWest", "District_TelemetrySpine", (-45.0, 3.0, 0.35), 18.0, (1.65, 1.65, 1.45)),
        Placement("switchgear_hall", "TelemetryLowerRelayEast", "District_TelemetrySpine", (45.0, 3.0, 0.35), -18.0, (1.65, 1.65, 1.45)),
        Placement("foundry_warehouse", "TideGateWestHall", "District_TideGate", (-66.0, 173.0, 0.35), -8.0, (2.0, 2.0, 1.65)),
        Placement("foundry_warehouse", "TideGateEastHall", "District_TideGate", (66.0, 173.0, 0.35), 8.0, (2.0, 2.0, 1.65)),
        Placement("cooling_hall", "TideGateWestPump", "District_TideGate", (-118.0, 184.0, 0.35), 82.0, (1.75, 1.75, 1.55)),
        Placement("cooling_hall", "TideGateEastPump", "District_TideGate", (118.0, 184.0, 0.35), -82.0, (1.75, 1.75, 1.55)),
        Placement("container_office", "SkiffControlOffice", "District_TideGate", (-141.0, 169.0, 0.35), 93.0, (1.55, 1.55, 1.45)),
        Placement("boiler_workshop", "NorthwestBallastHall", "District_TideGate", (-91.0, 202.0, 0.35), 2.0, (1.55, 1.55, 1.45)),
        Placement("boiler_workshop", "NortheastBallastHall", "District_TideGate", (91.0, 202.0, 0.35), -2.0, (1.55, 1.55, 1.45)),
        Placement("container_office", "EastGateInspectionPost", "District_TideGate", (141.0, 168.0, 0.35), -93.0, (1.55, 1.55, 1.45)),
        Placement("control_room", "TideGateControlWest", "District_TideGate", (-28.0, 178.0, 0.35), 12.0, (1.45, 1.45, 1.55)),
        Placement("control_room", "TideGateControlEast", "District_TideGate", (28.0, 178.0, 0.35), -12.0, (1.45, 1.45, 1.55)),
        Placement("arch_gateway", "TideGateCrownWest", "District_TideGate", (-9.0, 185.0, 0.35), 0.0, (1.55, 1.55, 2.1)),
        Placement("arch_gateway", "TideGateCrownEast", "District_TideGate", (9.0, 185.0, 0.35), 180.0, (1.55, 1.55, 2.1)),
    ]
    walkway_coordinates = (
        (-72.0, 66.0, 0.0), (-56.0, 66.0, 0.0), (-40.0, 66.0, 0.0),
        (72.0, 65.0, 180.0), (56.0, 65.0, 180.0), (40.0, 65.0, 180.0),
        (-52.0, 142.0, 0.0), (-34.0, 145.0, 0.0), (-16.0, 148.0, 0.0),
        (16.0, 148.0, 180.0), (34.0, 145.0, 180.0), (52.0, 142.0, 180.0),
        (-94.0, 107.0, 90.0), (-94.0, 125.0, 90.0),
        (94.0, 108.0, 90.0), (94.0, 126.0, 90.0),
    )
    for index, (x, y, yaw) in enumerate(walkway_coordinates, start=1):
        parent = "District_BreakerYard" if x < -55.0 else "District_QuarantineArchive" if x > 55.0 else "District_TelemetrySpine"
        placements.append(
            Placement("elevated_walkway", f"UpperCatwalk_{index:02d}", parent, (x, y, 1.0), yaw, (1.35, 1.35, 1.25))
        )
    return placements


def build_authored_districts(
    templates: dict[str, AssetTemplate], groups: dict[str, bpy.types.Object]
) -> list[bpy.types.Object]:
    created: list[bpy.types.Object] = []
    for placement in authored_placements():
        _, meshes = instantiate_asset(
            templates[placement.asset],
            placement.name,
            groups[placement.parent],
            placement.location,
            placement.yaw,
            placement.scale,
        )
        created.extend(meshes)
    return created


def polygon_subset_mesh(
    source: bpy.types.Mesh,
    polygon_indices: Iterable[int],
    name: str,
) -> bpy.types.Mesh:
    """Copy a face subset while preserving source UVs and split normals."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[list[int]] = []
    corner_normals: list[tuple[float, float, float]] = []
    smooth_values: list[bool] = []
    source_uv_layers = list(source.uv_layers)
    uv_values: dict[str, list[tuple[float, float]]] = {
        layer.name: [] for layer in source_uv_layers
    }
    for polygon_index in sorted(polygon_indices):
        polygon = source.polygons[polygon_index]
        face: list[int] = []
        for loop_index in polygon.loop_indices:
            source_vertex = source.vertices[source.loops[loop_index].vertex_index]
            face.append(len(vertices))
            vertices.append(tuple(source_vertex.co))
            corner_normals.append(tuple(source.corner_normals[loop_index].vector))
            for layer in source_uv_layers:
                uv_values[layer.name].append(tuple(layer.data[loop_index].uv))
        faces.append(face)
        smooth_values.append(polygon.use_smooth)
    subset = bpy.data.meshes.new(name)
    subset.from_pydata(vertices, [], faces)
    subset.update()
    for source_layer in source_uv_layers:
        target_layer = subset.uv_layers.new(name=source_layer.name)
        for loop_index, uv in enumerate(uv_values[source_layer.name]):
            target_layer.data[loop_index].uv = uv
    if len(corner_normals) == len(subset.loops):
        subset.normals_split_custom_set(corner_normals)
    for polygon, use_smooth in zip(subset.polygons, smooth_values):
        polygon.use_smooth = use_smooth
    subset.update()
    return subset


def split_dish_static_base(
    source: bpy.types.Mesh,
    source_to_world: Matrix,
    name_prefix: str,
) -> tuple[bpy.types.Mesh, bpy.types.Mesh]:
    """Separate the authored pedestal from the movable antenna components.

    NASA's triangulated source duplicates vertices per face. Connectivity is
    recovered from quantized vertex positions rather than vertex indices. Only
    the low support component is classified as static; reflector, feed, and
    truss components remain together beneath the authored motion pivots.
    """
    key_to_polygons: dict[tuple[float, float, float], list[int]] = {}
    polygon_keys: list[list[tuple[float, float, float]]] = []
    for polygon in source.polygons:
        keys: list[tuple[float, float, float]] = []
        for vertex_index in polygon.vertices:
            coordinate = source.vertices[vertex_index].co
            key = (round(coordinate.x, 5), round(coordinate.y, 5), round(coordinate.z, 5))
            keys.append(key)
            key_to_polygons.setdefault(key, []).append(polygon.index)
        polygon_keys.append(keys)

    remaining = set(range(len(source.polygons)))
    static_polygons: set[int] = set()
    moving_polygons: set[int] = set()
    while remaining:
        seed = remaining.pop()
        component = {seed}
        frontier = [seed]
        component_vertex_indices: set[int] = set()
        while frontier:
            polygon_index = frontier.pop()
            polygon = source.polygons[polygon_index]
            component_vertex_indices.update(polygon.vertices)
            for key in polygon_keys[polygon_index]:
                for neighbour in key_to_polygons[key]:
                    if neighbour in remaining:
                        remaining.remove(neighbour)
                        component.add(neighbour)
                        frontier.append(neighbour)
        world_heights = [
            (source_to_world @ source.vertices[vertex_index].co).z
            for vertex_index in component_vertex_indices
        ]
        is_static_pedestal = min(world_heights) < 8.0 and max(world_heights) < 24.0
        (static_polygons if is_static_pedestal else moving_polygons).update(component)

    if len(static_polygons) < 1000 or len(moving_polygons) < 10000:
        raise RuntimeError(
            "Dish component split did not find the expected authored pedestal/reflector "
            f"coverage: static={len(static_polygons)} moving={len(moving_polygons)}"
        )
    if len(static_polygons) + len(moving_polygons) != len(source.polygons):
        raise RuntimeError("Dish component split lost source polygons")
    static_mesh = polygon_subset_mesh(source, static_polygons, f"{name_prefix}_StaticBase")
    moving_mesh = polygon_subset_mesh(source, moving_polygons, f"{name_prefix}_MovingAssembly")
    static_mesh["source_polygon_count"] = len(static_polygons)
    moving_mesh["source_polygon_count"] = len(moving_polygons)
    return static_mesh, moving_mesh


def finish_dish_materials(
    data: bpy.types.Mesh,
    materials: dict[str, bpy.types.Material],
) -> None:
    data.materials.clear()
    data.materials.append(materials["DishWeatheredWhite"])
    data.materials.append(materials["DishOxide"])
    data.update()
    for polygon_index, polygon in enumerate(data.polygons):
        center = polygon.center
        weather = math.sin(center.x * 0.43 + center.y * 0.19 + polygon_index * 0.017)
        polygon.material_index = 1 if center.z < 28.0 or weather > 0.965 else 0


def build_dish(
    template: AssetTemplate,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    yaw_root = create_empty("DishYaw", root, (0.0, 83.0, 1.05))
    yaw_root["animation_axis"] = "local_y"
    yaw_root["animation_axis_space"] = "Godot Y-up runtime"
    yaw_root["source_axis_blender"] = "local_z"
    yaw_root["animation_range_degrees"] = "-155..155"
    yaw_root["pivot_role"] = "telemetry_azimuth_base"
    pitch_root = create_empty("DishPitch", yaw_root, (0.0, 0.0, 19.6))
    pitch_root["animation_axis"] = "local_x"
    pitch_root["animation_axis_space"] = "Godot Y-up runtime"
    pitch_root["source_axis_blender"] = "local_x"
    pitch_root["animation_range_degrees"] = "-6..18"
    pitch_root["pivot_role"] = "telemetry_elevation_trunnion"
    pitch_root.rotation_euler.x = math.radians(-4.0)
    pitch_root["rest_angle_degrees"] = -4.0
    created: list[bpy.types.Object] = []
    for index, prototype in enumerate(template.meshes, start=1):
        scale_matrix = Matrix.Diagonal((0.62, 0.62, 0.62, 1.0))
        source_to_world = (
            Matrix.Translation(Vector((0.0, 83.0, 1.05))) @ scale_matrix @ prototype.matrix
        )
        static_data, moving_data = split_dish_static_base(
            prototype.data,
            source_to_world,
            f"TelemetryDish_{index:02d}",
        )
        for data in (static_data, moving_data):
            finish_dish_materials(data, materials)
            data["authored_source"] = template.spec.path.relative_to(REPO_ROOT).as_posix()
            data["source_creator"] = template.spec.creator
            data["source_license"] = template.spec.license_name

        static_obj = bpy.data.objects.new(f"TelemetryDishStaticBase_{index:02d}", static_data)
        bpy.context.collection.objects.link(static_obj)
        static_obj.parent = root
        static_obj.matrix_local = source_to_world
        static_obj["dish_motion_role"] = "static pedestal outside azimuth/elevation axes"

        moving_obj = bpy.data.objects.new(
            f"TelemetryDishMovingAssembly_{index:02d}", moving_data
        )
        bpy.context.collection.objects.link(moving_obj)
        moving_obj.parent = pitch_root
        moving_obj.matrix_local = (
            Matrix.Translation(Vector((0.0, 0.0, -19.6)))
            @ scale_matrix
            @ prototype.matrix
        )
        moving_obj["dish_motion_role"] = "reflector/feed/truss under DishYaw and DishPitch"

        for obj in (static_obj, moving_obj):
            obj["authored_source"] = template.spec.path.relative_to(REPO_ROOT).as_posix()
            obj["source_creator"] = template.spec.creator
            obj["source_license"] = template.spec.license_name
            obj["modification"] = (
                "Scaled, reoriented, de-branded material treatment, oxide weathering; "
                "pedestal separated at authored component boundary for credible motion"
            )
            created.append(obj)
    return created


def build_capsule(
    template: AssetTemplate,
    parent: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    capsule_root = create_empty("RecoveredCapsule_Fictional", parent, (2.0, 30.0, -3.55))
    capsule_root.rotation_euler = (math.radians(68.0), math.radians(-9.0), math.radians(23.0))
    capsule_root.scale = (0.33, 0.33, 0.33)
    capsule_root["fictional_identity"] = "Falltide atmospheric return article; not presented as Orion"
    capsule_root["authored_source"] = template.spec.path.relative_to(REPO_ROOT).as_posix()
    capsule_root["source_creator"] = template.spec.creator
    capsule_root["source_license"] = template.spec.license_name
    created: list[bpy.types.Object] = []
    for index, prototype in enumerate(template.meshes, start=1):
        data = prototype.data
        data.materials.clear()
        data.materials.append(materials["CapsuleCeramic"])
        data.materials.append(materials["CapsuleScorch"])
        data.update()
        for polygon_index, polygon in enumerate(data.polygons):
            center = polygon.center
            heat = math.sin(center.x * 0.91 - center.y * 0.63 + center.z * 0.31)
            polygon.material_index = 1 if polygon.normal.z < -0.16 or heat > 0.72 or polygon_index % 41 == 0 else 0
        obj = bpy.data.objects.new(f"RecoveredCapsuleAuthoredMesh_{index:02d}", data)
        bpy.context.collection.objects.link(obj)
        obj.parent = capsule_root
        obj.matrix_local = prototype.matrix.copy()
        obj["authored_source"] = template.spec.path.relative_to(REPO_ROOT).as_posix()
        obj["source_creator"] = template.spec.creator
        obj["source_license"] = template.spec.license_name
        obj["modification"] = "Fictional ceramic recolor, scorch mask, impact pose; NASA marks absent"
        created.append(obj)
    return created


def instantiate_movable_gate(
    template: AssetTemplate,
    node_name: str,
    parent: bpy.types.Object,
    pivot: tuple[float, float, float],
    mesh_offset: tuple[float, float, float],
    yaw: float,
    scale: tuple[float, float, float],
    motion: str,
) -> list[bpy.types.Object]:
    movement_root = create_empty(node_name, parent, pivot)
    movement_root.rotation_euler.z = math.radians(yaw)
    movement_root.scale = scale
    movement_root["animation_motion"] = motion
    movement_root["animation_axis_space"] = "Godot Y-up runtime"
    movement_root["source_up_axis_blender"] = "local_z"
    movement_root["closed_transform"] = "identity"
    movement_root["pivot_role"] = "authored_gate_hinge_or_slide_origin"
    movement_root["authored_source"] = template.spec.path.relative_to(REPO_ROOT).as_posix()
    created: list[bpy.types.Object] = []
    offset = Matrix.Translation(Vector(mesh_offset))
    for index, prototype in enumerate(template.meshes, start=1):
        obj = bpy.data.objects.new(f"{node_name}_AuthoredMesh_{index:02d}", prototype.data)
        bpy.context.collection.objects.link(obj)
        obj.parent = movement_root
        obj.matrix_local = offset @ prototype.matrix
        obj["authored_source"] = movement_root["authored_source"]
        obj["source_creator"] = template.spec.creator
        obj["source_license"] = template.spec.license_name
        created.append(obj)
    return created


def build_interactive_structures(
    templates: dict[str, AssetTemplate],
    root: bpy.types.Object,
    groups: dict[str, bpy.types.Object],
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    created = build_dish(templates["dish"], root, materials)
    created.extend(build_capsule(templates["capsule"], groups["District_CapsuleDrydock"], materials))
    created.extend(
        instantiate_movable_gate(
            templates["east_gate"], "TideGateLeft", root, (-5.8, 188.0, 0.35), (-4.2, 0.0, 0.0), 0.0, (1.6, 1.6, 1.75), "swing_local_y:+76deg"
        )
    )
    created.extend(
        instantiate_movable_gate(
            templates["east_gate"], "TideGateRight", root, (5.8, 188.0, 0.35), (4.2, 0.0, 0.0), 180.0, (1.6, 1.6, 1.75), "swing_local_y:-76deg"
        )
    )
    created.extend(
        instantiate_movable_gate(
            templates["west_gate"], "VaultDoorLeft", root, (-4.8, 63.0, 1.0), (-3.0, 0.0, 0.0), 0.0, (0.82, 0.82, 1.35), "slide_local_x:-5.5m"
        )
    )
    created.extend(
        instantiate_movable_gate(
            templates["west_gate"], "VaultDoorRight", root, (4.8, 63.0, 1.0), (3.0, 0.0, 0.0), 180.0, (0.82, 0.82, 1.35), "slide_local_x:+5.5m"
        )
    )
    created.extend(
        instantiate_movable_gate(
            templates["east_gate"],
            "UpperBypassBarrier",
            root,
            (-82.0, 66.0, 2.5),
            (0.0, 0.0, -1.6),
            90.0,
            (0.455, 2.5, 1.5625),
            "slide_local_y:+5.2m",
        )
    )
    upper_barrier = bpy.data.objects["UpperBypassBarrier"]
    upper_barrier["stage_contract"] = "closed_stage0; open_stage1_stage2"
    upper_barrier["closed_godot_position"] = "(-82.0, 2.5, -66.0)"
    upper_barrier["open_visibility"] = "runtime may hide after upward slide"

    alarm_locations = {
        "AlarmLight_Central": (0.0, 64.0, 7.3),
        "AlarmLight_Breaker": (-95.0, 62.0, 12.5),
        "AlarmLight_Archive": (101.0, 69.0, 9.5),
        "AlarmLight_TideGate": (0.0, 181.0, 7.4),
    }
    for name, location in alarm_locations.items():
        alarm = create_empty(name, groups["PowerZone_Powered"], location)
        alarm["animation_motion"] = "rotate_local_y:240rpm; emissive_pulse:0.3..1.0"
        alarm["animation_axis_space"] = "Godot Y-up runtime"
        alarm["source_up_axis_blender"] = "local_z"
        alarm["light_color"] = "#FF4B08"
        created.append(create_beacon_mesh(name, alarm, materials["SodiumEmission"]))

    powered_routes = (
        ("PoweredSpineStrip", [(0.0, -84.0), (0.0, -24.0), (0.0, 1.0)], 0.34),
        ("PoweredVaultStrip", [(-24.0, 61.0), (0.0, 63.0), (24.0, 61.0)], 0.30),
        ("PoweredNorthStrip", [(-26.0, 178.0), (0.0, 181.0), (26.0, 178.0)], 0.34),
        ("PoweredBreakerStrip", [(-58.0, 61.0), (-94.0, 62.0), (-128.0, 80.0)], 0.24),
        ("PoweredArchiveStrip", [(58.0, 61.0), (98.0, 69.0), (132.0, 90.0)], 0.24),
    )
    for name, points, width in powered_routes:
        created.append(
            create_ribbon_surface(name, points, width, 0.17, materials["CyanEmission"], groups["PowerZone_Powered"], "powered_guidance_strip")
        )
    blackout_markers = (
        (-95.0, 62.0, 7.0), (101.0, 69.0, 7.0), (0.0, 64.0, 5.0), (0.0, 181.0, 7.0)
    )
    for index, location in enumerate(blackout_markers, start=1):
        marker = create_empty(f"BlackoutFixture_{index:02d}", groups["PowerZone_Blackout"], location)
        created.append(create_beacon_mesh(f"BlackoutFixture_{index:02d}", marker, materials["BlackoutGlass"], 0.36, 0.65))
    return created


def build_gameplay_anchors(groups: dict[str, bpy.types.Object]) -> list[bpy.types.Object]:
    anchors: list[bpy.types.Object] = []
    for name, location in GAMEPLAY_ANCHORS.items():
        anchor = create_empty(name, groups["GameplayAnchors"], location)
        anchor["godot_position"] = f"{location[0]:.3f},{location[2]:.3f},{-location[1]:.3f}"
        anchor["anchor_role"] = "extraction" if name.startswith("Extraction") else "spawn" if name.startswith("Spawn") else "poi"
        anchors.append(anchor)
    return anchors


def scene_descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    descendants: list[bpy.types.Object] = []
    queue = list(root.children)
    while queue:
        obj = queue.pop(0)
        descendants.append(obj)
        queue.extend(obj.children)
    return descendants


def mesh_statistics(objects: Iterable[bpy.types.Object]) -> dict[str, int]:
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    unique_meshes = {obj.data for obj in mesh_objects}
    triangle_count = 0
    for mesh in unique_meshes:
        mesh.calc_loop_triangles()
        triangle_count += len(mesh.loop_triangles)
    instance_triangle_count = 0
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
        instance_triangle_count += len(obj.data.loop_triangles)
    materials = {material for mesh in unique_meshes for material in mesh.materials if material is not None}
    images = {
        node.image
        for material in materials
        if material.use_nodes and material.node_tree is not None
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    image_bytes = sum(max(1, image.size[0]) * max(1, image.size[1]) * 4 for image in images)
    return {
        "mesh_nodes": len(mesh_objects),
        "unique_meshes": len(unique_meshes),
        "unique_triangles": triangle_count,
        "instance_triangles": instance_triangle_count,
        "materials": len(materials),
        "images": len(images),
        "estimated_texture_memory_bytes": image_bytes,
    }


def quantize_uvs(root: bpy.types.Object, decimals: int = 4) -> None:
    """Clamp authored UV coordinates to stable float precision before export.

    Blender's glTF exporter can otherwise preserve sub-micron last-bit drift
    between clean background runs when trigonometric hardscape coordinates are
    converted to float32.  Four decimal places is below one texel at the 1K
    maps' working scale while making procedural hardscape payloads stable.
    """
    descendants = [root] + scene_descendants(root)
    for obj in descendants:
        if obj.type != "MESH":
            continue
        for layer in obj.data.uv_layers:
            for uv in layer.data:
                uv.uv.x = round(float(uv.uv.x), decimals)
                uv.uv.y = round(float(uv.uv.y), decimals)


def validate_scene(root: bpy.types.Object) -> dict[str, object]:
    descendants = scene_descendants(root)
    mesh_objects = [obj for obj in descendants if obj.type == "MESH"]
    minimum, maximum = points_bounds(mesh_objects)
    dimensions = maximum - minimum
    center = (minimum + maximum) * 0.5
    if abs(dimensions.x - MAP_SIZE[0]) > 0.025 or abs(dimensions.y - MAP_SIZE[1]) > 0.025:
        raise RuntimeError(
            f"Map horizontal bounds must be 340x320m, got {dimensions.x:.3f}x{dimensions.y:.3f}"
        )
    if abs(center.x) > 0.025 or abs(center.y - MAP_CENTER_BLENDER.y) > 0.025:
        raise RuntimeError(f"Unexpected map center: {tuple(center)}")
    names = [obj.name for obj in [root] + descendants]
    duplicates = sorted({name for name in names if names.count(name) > 1})
    if duplicates:
        raise RuntimeError(f"Duplicate node names: {duplicates}")
    missing_nodes = sorted(INTERACTIVE_NODES - set(names))
    if missing_nodes:
        raise RuntimeError(f"Missing interactive nodes: {missing_nodes}")
    dish_pitch = next((obj for obj in descendants if obj.name == "DishPitch"), None)
    dish_yaw = next((obj for obj in descendants if obj.name == "DishYaw"), None)
    dish_static = [obj for obj in mesh_objects if obj.name.startswith("TelemetryDishStaticBase_")]
    dish_moving = [
        obj for obj in mesh_objects if obj.name.startswith("TelemetryDishMovingAssembly_")
    ]
    if dish_pitch is None or dish_yaw is None or dish_pitch.parent != dish_yaw:
        raise RuntimeError("DishYaw -> DishPitch hierarchy is invalid")
    if not dish_static or not dish_moving:
        raise RuntimeError("Dish static pedestal or moving reflector assembly is missing")
    if any(obj.parent != root for obj in dish_static):
        raise RuntimeError("Dish static pedestal must remain outside both motion axes")
    if any(obj.parent != dish_pitch for obj in dish_moving):
        raise RuntimeError("Dish moving assembly must remain beneath DishPitch")
    missing_anchors = sorted(set(GAMEPLAY_ANCHORS) - set(names))
    if missing_anchors:
        raise RuntimeError(f"Missing gameplay anchors: {missing_anchors}")
    empty_materials = sorted(
        obj.name
        for obj in mesh_objects
        if len(obj.data.materials) == 0 or any(material is None for material in obj.data.materials)
    )
    if empty_materials:
        raise RuntimeError(f"Meshes with empty material slots: {empty_materials[:12]}")
    if not any(obj.get("source_creator") == "NASA" for obj in descendants):
        raise RuntimeError("NASA source provenance is missing from hero objects")
    if sum(1 for obj in descendants if obj.get("source_license") == "CC0-1.0") < 20:
        raise RuntimeError("Authored CC0 structure coverage is unexpectedly low")
    return {
        "minimum_blender": [round(value, 5) for value in minimum],
        "maximum_blender": [round(value, 5) for value in maximum],
        "dimensions_blender": [round(value, 5) for value in dimensions],
        "center_blender": [round(value, 5) for value in center],
        "map_dimensions_godot_xz": [round(dimensions.x, 5), round(dimensions.y, 5)],
        "map_center_godot": list(MAP_CENTER_GODOT),
        "duplicate_node_names": duplicates,
        "empty_material_meshes": empty_materials,
        "interactive_nodes": sorted(INTERACTIVE_NODES),
        "gameplay_anchors": sorted(GAMEPLAY_ANCHORS),
        "dish_motion_partition": {
            "static_base_nodes": sorted(obj.name for obj in dish_static),
            "moving_assembly_nodes": sorted(obj.name for obj in dish_moving),
        },
    }


def point_camera(camera: bpy.types.Object, target: tuple[float, float, float]) -> None:
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_preview_lighting() -> list[bpy.types.Object]:
    lights: list[bpy.types.Object] = []
    sun_data = bpy.data.lights.new("StormBreakSun", "SUN")
    sun_data.energy = 3.8
    sun_data.angle = math.radians(18.0)
    sun_data.color = (0.62, 0.72, 0.84)
    sun = bpy.data.objects.new("StormBreakSun", sun_data)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(42.0), math.radians(-18.0), math.radians(-31.0))
    lights.append(sun)
    for name, location, color, energy, size in (
        ("SodiumFillWest", (-92.0, 54.0, 28.0), (1.0, 0.18, 0.035), 2600.0, 18.0),
        ("CyanFillEast", (93.0, 76.0, 30.0), (0.02, 0.44, 1.0), 2300.0, 20.0),
        ("DrydockFill", (0.0, 20.0, 26.0), (1.0, 0.28, 0.05), 2600.0, 18.0),
        ("TideGateFill", (0.0, 178.0, 24.0), (1.0, 0.23, 0.04), 2800.0, 20.0),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.shape = "DISK"
        light_data.size = size
        light_data.color = color
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        point_camera(light, (location[0] * 0.65, location[1], 0.0))
        lights.append(light)
    return lights


def render_previews(groups: dict[str, bpy.types.Object]) -> list[dict[str, object]]:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    add_preview_lighting()
    cameras = (
        ("overview_top.png", (0.0, 60.0, 390.0), (0.0, 60.0, 0.0), 48.0, "Full 340 x 320 m authored composition"),
        ("south_player_height.png", (-8.0, -84.0, 2.25), (0.0, -30.0, 4.2), 54.0, "South intake toward capsule dry dock and telemetry silhouette"),
        ("central_landmark.png", (32.0, -8.0, 14.0), (0.0, 44.0, 8.0), 28.0, "Recovered capsule dry dock with telemetry landmark beyond"),
        ("north_tide_gate_powered.png", (3.0, 120.0, 15.0), (0.0, 188.0, 7.0), 48.0, "Powered sodium/cyan lighting at Tide Gate extraction"),
    )
    preview_records: list[dict[str, object]] = []
    for filename, location, target, lens, description in cameras:
        camera_data = bpy.data.cameras.new(f"Preview_{filename}_Camera")
        camera_data.lens = lens
        camera_data.sensor_width = 36.0
        camera_data.clip_start = 0.1
        camera_data.clip_end = 800.0
        if filename == "overview_top.png":
            camera_data.type = "ORTHO"
            camera_data.ortho_scale = 360.0
        camera = bpy.data.objects.new(f"Preview_{filename}_Camera", camera_data)
        bpy.context.collection.objects.link(camera)
        camera.location = location
        point_camera(camera, target)
        scene.camera = camera
        scene.render.filepath = str(PREVIEW_DIR / filename)
        bpy.ops.render.render(write_still=True)
        if not (PREVIEW_DIR / filename).is_file():
            raise RuntimeError(f"Preview render missing: {filename}")
        preview_records.append(
            {
                "file": f"previews/{filename}",
                "description": description,
                "camera_blender": list(location),
                "target_blender": list(target),
            }
        )
        bpy.data.objects.remove(camera, do_unlink=True)
        bpy.data.cameras.remove(camera_data)
    return preview_records


def purge_unused_datablocks() -> None:
    """Keep the authored .blend free of discarded import materials and images."""
    datablock_groups = (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.node_groups,
    )
    for _pass in range(3):
        removed = 0
        for datablocks in datablock_groups:
            for datablock in list(datablocks):
                if datablock.users == 0:
                    datablocks.remove(datablock)
                    removed += 1
        if removed == 0:
            break


def save_blend() -> None:
    WORLD_SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    result = bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND), compress=True, check_existing=False)
    if "FINISHED" not in result or not SOURCE_BLEND.is_file():
        raise RuntimeError("Blender did not save the authoritative Falltide source")


def export_glb(root: bpy.types.Object) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    descendants = scene_descendants(root)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in descendants:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
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


def glb_document(path: Path) -> dict[str, object]:
    payload = path.read_bytes()
    if len(payload) < 20 or payload[:4] != b"glTF":
        raise RuntimeError("Output is not a binary glTF")
    _magic, version, length = struct.unpack_from("<III", payload, 0)
    chunk_length, chunk_type = struct.unpack_from("<II", payload, 12)
    if version != 2 or length != len(payload) or chunk_type != 0x4E4F534A:
        raise RuntimeError("Output GLB header is invalid")
    return json.loads(payload[20 : 20 + chunk_length].decode("utf-8"))


def verify_embedded_glb(path: Path) -> dict[str, int]:
    document = glb_document(path)
    external_buffers = [entry for entry in document.get("buffers", []) if "uri" in entry]
    external_images = [entry for entry in document.get("images", []) if "uri" in entry]
    if external_buffers or external_images:
        raise RuntimeError("Runtime GLB contains external buffers or images")
    return {
        "gltf_nodes": len(document.get("nodes", [])),
        "gltf_meshes": len(document.get("meshes", [])),
        "gltf_primitives": sum(
            len(mesh.get("primitives", [])) for mesh in document.get("meshes", [])
        ),
        "gltf_materials": len(document.get("materials", [])),
        "gltf_images": len(document.get("images", [])),
    }


def roundtrip_audit(expected: dict[str, object]) -> dict[str, object]:
    clear_scene()
    configure_scene()
    result = bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError("Blender could not re-import the runtime GLB")
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None:
        raise RuntimeError("Round-trip lost the Falltide root")
    audit = validate_scene(root)
    expected_dimensions = expected["dimensions_blender"]
    actual_dimensions = audit["dimensions_blender"]
    if any(abs(float(a) - float(b)) > 0.03 for a, b in zip(expected_dimensions, actual_dimensions)):
        raise RuntimeError(f"Round-trip dimensions changed: {expected_dimensions} -> {actual_dimensions}")
    names = {obj.name for obj in [root] + scene_descendants(root)}
    if not INTERACTIVE_NODES.issubset(names):
        raise RuntimeError("Round-trip lost one or more animation nodes")
    if bpy.data.objects["DishPitch"].parent != bpy.data.objects["DishYaw"]:
        raise RuntimeError("Round-trip lost DishYaw -> DishPitch hierarchy")
    return {"scene": audit, "statistics": mesh_statistics(scene_descendants(root))}


def source_record(spec: AssetSpec) -> dict[str, object]:
    digest = hashlib.sha256(spec.path.read_bytes()).hexdigest().upper()
    return {
        "key": spec.key,
        "local_path": spec.path.relative_to(REPO_ROOT).as_posix(),
        "creator": spec.creator,
        "title": spec.title,
        "source_url": spec.source_url,
        "license": spec.license_name,
        "acquired": ACQUISITION_DATE if spec.creator == "NASA" else "2026-08-27",
        "sha256": digest,
    }


def texture_source_record(prefix: str, metadata: dict[str, str]) -> dict[str, object]:
    maps = {}
    for role, suffix in (("base_color", "diff"), ("normal", "normal"), ("roughness", "rough")):
        path = TEXTURE_ROOT / f"{prefix}_{suffix}_1k.jpg"
        maps[role] = {
            "local_path": path.relative_to(REPO_ROOT).as_posix(),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest().upper(),
        }
    return {
        "key": prefix,
        "creator": metadata["creator"],
        "source_url": metadata["source_url"],
        "license": metadata["license"],
        "acquired": metadata["acquired"],
        "maps": maps,
    }


def build_report(
    validation: dict[str, object],
    statistics: dict[str, int],
    roundtrip: dict[str, object],
    gltf_counts: dict[str, int],
    previews: list[dict[str, object]],
) -> dict[str, object]:
    output_digest = hashlib.sha256(OUTPUT_GLB.read_bytes()).hexdigest().upper()
    return {
        "asset": "FALLTIDE RECOVERY ARRAY",
        "map_id": "orbital_complex",
        "generated_on": date.today().isoformat(),
        "blender_version": bpy.app.version_string,
        "coordinate_contract": {
            "blender": "meters, Z-up",
            "gltf": "meters, Y-up",
            "godot_mapping": "X = Blender X, Y = Blender Z, Z = -Blender Y",
            "map_bounds_m": [340.0, 320.0],
            "map_center_godot": list(MAP_CENTER_GODOT),
        },
        "scene_audit": validation,
        "statistics_before_export": statistics,
        "roundtrip_audit": roundtrip,
        "gltf_document": gltf_counts,
        "files": {
            "blend": {
                "path": SOURCE_BLEND.relative_to(REPO_ROOT).as_posix(),
                "bytes": SOURCE_BLEND.stat().st_size,
                "sha256": hashlib.sha256(SOURCE_BLEND.read_bytes()).hexdigest().upper(),
            },
            "glb": {
                "path": OUTPUT_GLB.relative_to(REPO_ROOT).as_posix(),
                "bytes": OUTPUT_GLB.stat().st_size,
                "sha256": output_digest,
                "embedded": True,
            },
        },
        "sources": [source_record(spec) for spec in ASSETS.values()],
        "surface_sources": [
            texture_source_record(prefix, metadata)
            for prefix, metadata in TEXTURE_SOURCES.items()
        ],
        "previews": previews,
        "authored_changes": [
            "Original 340 x 320 m offshore recovery-array composition and circulation topology",
            "Modeled storm basin, reclaimed deck, sea defenses, causeway, service roads, dry dock, powered guidance layers",
            "NASA 70 Meter Dish scaled to 0.62, fictionally weathered, and partitioned at authored component boundaries into a static pedestal and double-axis moving assembly",
            "NASA capsule source scaled to 0.33, fictionally recolored/scorched and impact posed without NASA marks",
            "CC0 industrial buildings recomposed as distinct Breaker Yard, Quarantine Archive, Tide Gate, and Intake districts",
            "Stable authored interaction pivots for dish, gates, vault, alarms, and power-state groups",
            "Authored UpperBypassBarrier gate aligned to the west catwalk blocker contract and staged upward-open motion",
        ],
        "validation": {
            "valid": True,
            "authored_major_geometry": True,
            "roundtrip_import": True,
            "embedded_runtime_asset": True,
            "empty_material_meshes": 0,
            "duplicate_node_names": 0,
            "interactive_nodes_present": True,
        },
    }


def write_report(report: dict[str, object]) -> None:
    BUILD_REPORT.parent.mkdir(parents=True, exist_ok=True)
    BUILD_REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def build() -> None:
    clear_scene()
    configure_scene()
    require_sources()
    materials = build_materials()
    templates = load_templates(materials)
    root, groups = create_root_hierarchy()
    build_hardscape(groups, materials)
    build_authored_districts(templates, groups)
    build_interactive_structures(templates, root, groups, materials)
    build_gameplay_anchors(groups)
    bpy.context.view_layer.update()
    quantize_uvs(root)
    validation = validate_scene(root)
    statistics = mesh_statistics(scene_descendants(root))
    previews = render_previews(groups)
    purge_unused_datablocks()
    save_blend()
    export_glb(root)
    gltf_counts = verify_embedded_glb(OUTPUT_GLB)
    roundtrip = roundtrip_audit(validation)
    report = build_report(validation, statistics, roundtrip, gltf_counts, previews)
    write_report(report)
    print(
        "ORBITAL_COMPLEX_ASSET "
        f"bounds={validation['dimensions_blender']} center_godot={MAP_CENTER_GODOT} "
        f"meshes={statistics['mesh_nodes']} unique_meshes={statistics['unique_meshes']} "
        f"unique_triangles={statistics['unique_triangles']} "
        f"instance_triangles={statistics['instance_triangles']} materials={statistics['materials']} "
        f"images={statistics['images']} texture_bytes={statistics['estimated_texture_memory_bytes']} "
        f"blend_bytes={SOURCE_BLEND.stat().st_size} glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"sha256={report['files']['glb']['sha256']}"
    )
    print("ORBITAL_COMPLEX_PASS valid=True authored_geometry=True animated_nodes=True roundtrip=True")


if __name__ == "__main__":
    build()
