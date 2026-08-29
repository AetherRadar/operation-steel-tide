"""Build the production Steel Tide AK-47 first-person and world assets.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup --python scripts/blender/build_taradavies_ak47.py

The finished gun geometry comes from taradavies' CC0 OpenGameArt AK-47.  The
source file references unpacked images of unknown provenance, so this build
removes every source image and material before creating project-owned PBR
materials and a deterministic laminated-wood texture inside Blender.
"""

from __future__ import annotations

import hashlib
import json
from math import pi
from pathlib import Path
import struct

import bpy
import numpy as np
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = (
    REPO_ROOT
    / "source_art"
    / "third_party"
    / "taradavies_ak47"
    / "ak47_taradavies.blend"
)
OUTPUT_DIRECTORY = REPO_ROOT / "assets" / "models" / "steel_tide_ak74"
OUTPUT_FP_GLB = OUTPUT_DIRECTORY / "ak47_reloadable_fp.glb"
OUTPUT_WORLD_GLB = OUTPUT_DIRECTORY / "ak47_reloadable_world.glb"
OUTPUT_BLEND = (
    REPO_ROOT / "source_art" / "reloadable_weapons" / "ak47_reloadable.blend"
)
TEXTURE_DIRECTORY = (
    REPO_ROOT / "source_art" / "reloadable_weapons" / "textures"
)
WOOD_BASE_COLOR = TEXTURE_DIRECTORY / "ak47_laminated_wood_base_color.png"
WOOD_ROUGHNESS = TEXTURE_DIRECTORY / "ak47_laminated_wood_roughness.png"
PREVIEW_PATH = OUTPUT_DIRECTORY / "ak47_studio_preview.png"

SOURCE_URL = "https://opengameart.org/content/ak-47-1"
SOURCE_CREATOR = "taradavies"
SOURCE_LICENSE = "CC0-1.0"
SOURCE_SHA256 = "F3E4D7708EEB95DBEBE9E240C868148C4F03BEDA363FB904141861FFC8EC1392"
SOURCE_BYTE_COUNT = 2_044_772

SOURCE_MESH_NAMES = {
    *("Cube" if index == 0 else f"Cube.{index:03d}" for index in range(20)),
    *("Cylinder" if index == 0 else f"Cylinder.{index:03d}" for index in range(9)),
    "Plane",
    "Plane.001",
    "weapon.001",
}
SOURCE_VERTEX_COUNT = 11_345
SOURCE_POLYGON_COUNT = 11_161
SOURCE_TRIANGLE_COUNT = 22_394
SOURCE_MIN_X = -4.76798916
SOURCE_MAX_X = 4.77696609

# The source is +X muzzle.  Normalize it into the project's Blender weapon
# convention: +Y muzzle, +Z up, 1.58 m overall length, stock at -0.32 m and
# muzzle at +1.26 m.  The glTF exporter maps that to Godot -Z forward/+Y up.
SOURCE_SCALE = 0.1648085108
NORMALIZATION = (
    Matrix.Translation((0.0, 0.4727153319, -0.1323747021))
    @ Matrix.Rotation(pi * 0.5, 4, "Z")
    @ Matrix.Scale(SOURCE_SCALE, 4)
)

MAGAZINE_SOURCE = "Cube.006"
CHARGING_HANDLE_SOURCES = ("weapon.001", "Plane.001")
REAR_IRON_SOURCES = ("Cube.017", "Cube.018")
STOCK_SOURCES = ("Cube", "Cube.001")
WOOD_SOURCES = {"Cube", "Cube.003", "Cube.008", "Cube.009"}
RUBBER_SOURCES = {"Cube.001"}
MAGAZINE_SOURCES = {MAGAZINE_SOURCE}
BOLT_SOURCES = {"weapon.001", "Plane.001", "Cube.016", "Cube.019"}

# The front sight is connected to the source barrel object.  These thresholds
# isolate only its authored upper tower/post before subdivision.
FRONT_IRON_MIN_SOURCE_X = 3.55
FRONT_IRON_MIN_SOURCE_Z = 1.02
FRONT_IRON_SOURCE_POLYGONS = 498

MAGAZINE_PIVOT = Vector((0.0, 0.4311, 0.0068))
SPARE_MAGAZINE_PIVOT = Vector((-0.30, 0.18, -0.62))
# Invisible hand-target markers preserve the magazine's physically correct
# rock-in pivot while giving the first-person rig explicit DCC-authored grips.
# Blender +Y/+Z export to Godot -Z/+Y respectively.
MAGAZINE_GRIP = Vector((-0.0055, 0.0148, -0.1244))
SPARE_MAGAZINE_GRIP = Vector((-0.0440, 0.0068, 0.0559))
CHARGING_HANDLE_PIVOT = Vector((0.0370, 0.5307, 0.0437))
MUZZLE_TIP = Vector((0.0, 1.260, 0.015))
SUPPRESSOR_TIP = Vector((0.0, 1.395, 0.015))
EJECTION_PORT = Vector((0.052, 0.485, 0.045))

RUNTIME_NODE_PARENTS = {
    "SteelTideAK47": None,
    "BoltHardwareGeometry": "SteelTideAK47",
    "ChargingHandle": "SteelTideAK47",
    "ChargingHandleGeometry": "ChargingHandle",
    "EjectionPort": "SteelTideAK47",
    "Foregrip": "SteelTideAK47",
    "FrontIronSight": "SteelTideAK47",
    "FrontIronGeometry": "FrontIronSight",
    "FurnitureGeometry": "SteelTideAK47",
    "Magazine": "SteelTideAK47",
    "MagazineGeometry": "Magazine",
    "MagazineGrip": "Magazine",
    "MuzzleDevice": "SteelTideAK47",
    "MuzzleDeviceTip": "MuzzleDevice",
    "OpticMount": "SteelTideAK47",
    "OpticReticleAnchor": "OpticMount",
    "OpticRailAdapterGeometry": "SteelTideAK47",
    "OpticRailContact": "SteelTideAK47",
    "RearIronSight": "SteelTideAK47",
    "RearIronGeometry": "RearIronSight",
    "ReceiverGeometry": "SteelTideAK47",
    "SpareMagazine": "SteelTideAK47",
    "SpareMagazineGeometry": "SpareMagazine",
    "SpareMagazineGrip": "SpareMagazine",
    "Stock": "SteelTideAK47",
    "StockButtpadGeometry": "Stock",
    "StockWoodGeometry": "Stock",
    "Suppressor": "SteelTideAK47",
    "SuppressorTip": "Suppressor",
    "WeaponBodyGeometry": "SteelTideAK47",
}
RUNTIME_NODE_NAMES = frozenset(RUNTIME_NODE_PARENTS)
EXPECTED_RUNTIME_NODE_COUNT = 30
EXPECTED_RUNTIME_MESH_COUNT = 11
EXPECTED_RUNTIME_MATERIAL_COUNT = 6
EXPECTED_RUNTIME_IMAGE_COUNT = 2
EXPECTED_RUNTIME_TEXTURE_COUNT = 2
EXPECTED_FP_TRIANGLE_COUNT = 97_372
EXPECTED_WORLD_TRIANGLE_COUNT = 24_488

if len(RUNTIME_NODE_NAMES) != EXPECTED_RUNTIME_NODE_COUNT:
    raise RuntimeError(
        "The authored AK hierarchy contract must contain exactly "
        f"{EXPECTED_RUNTIME_NODE_COUNT} nodes."
    )


def require_exact_node_names(actual_names: list[str], stage: str) -> None:
    actual_set = set(actual_names)
    duplicates = sorted(
        name for name in actual_set if actual_names.count(name) > 1
    )
    if (
        len(actual_names) != EXPECTED_RUNTIME_NODE_COUNT
        or actual_set != RUNTIME_NODE_NAMES
    ):
        raise RuntimeError(
            f"Unexpected AK node contract at {stage}: "
            f"count={len(actual_names)}/{EXPECTED_RUNTIME_NODE_COUNT} "
            f"missing={sorted(RUNTIME_NODE_NAMES - actual_set)} "
            f"extra={sorted(actual_set - RUNTIME_NODE_NAMES)} "
            f"duplicates={duplicates}"
        )


def require_exact_parent_contract(
    actual_parents: dict[str, str | None],
    stage: str,
) -> None:
    if actual_parents == RUNTIME_NODE_PARENTS:
        return
    mismatches = {
        name: (RUNTIME_NODE_PARENTS.get(name), actual_parents.get(name))
        for name in sorted(RUNTIME_NODE_NAMES | actual_parents.keys())
        if RUNTIME_NODE_PARENTS.get(name) != actual_parents.get(name)
    }
    raise RuntimeError(
        f"Unexpected AK parent contract at {stage}: {mismatches}"
    )


def validate_blender_runtime_hierarchy(stage: str) -> None:
    objects = list(bpy.data.objects)
    require_exact_node_names([obj.name for obj in objects], stage)
    require_exact_parent_contract(
        {
            obj.name: obj.parent.name if obj.parent is not None else None
            for obj in objects
        },
        stage,
    )


def require_source() -> None:
    if not SOURCE_BLEND.is_file():
        raise RuntimeError(f"Missing tracked AK source: {SOURCE_BLEND}")
    source_bytes = SOURCE_BLEND.read_bytes()
    digest = hashlib.sha256(source_bytes).hexdigest().upper()
    if len(source_bytes) != SOURCE_BYTE_COUNT or digest != SOURCE_SHA256:
        raise RuntimeError(
            "Unexpected taradavies AK source payload: "
            f"bytes={len(source_bytes)} sha256={digest}"
        )


def open_and_validate_source() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND), load_ui=False)
    source_meshes = {
        obj.name: obj for obj in bpy.data.objects if obj.type == "MESH"
    }
    actual_names = set(source_meshes) - {"Plane.002"}
    if actual_names != SOURCE_MESH_NAMES:
        raise RuntimeError(
            "Unexpected AK source object contract: "
            f"missing={sorted(SOURCE_MESH_NAMES - actual_names)} "
            f"extra={sorted(actual_names - SOURCE_MESH_NAMES)}"
        )
    if "Plane.002" not in source_meshes:
        raise RuntimeError("The excluded source background plane is missing.")

    vertices = sum(len(source_meshes[name].data.vertices) for name in actual_names)
    polygons = sum(len(source_meshes[name].data.polygons) for name in actual_names)
    triangles = sum(
        len(polygon.vertices) - 2
        for name in actual_names
        for polygon in source_meshes[name].data.polygons
    )
    minimum_x = min(
        (source_meshes[name].matrix_world @ vertex.co).x
        for name in actual_names
        for vertex in source_meshes[name].data.vertices
    )
    maximum_x = max(
        (source_meshes[name].matrix_world @ vertex.co).x
        for name in actual_names
        for vertex in source_meshes[name].data.vertices
    )
    if (
        vertices != SOURCE_VERTEX_COUNT
        or polygons != SOURCE_POLYGON_COUNT
        or triangles != SOURCE_TRIANGLE_COUNT
        or abs(minimum_x - SOURCE_MIN_X) > 0.00001
        or abs(maximum_x - SOURCE_MAX_X) > 0.00001
    ):
        raise RuntimeError(
            "Unexpected AK source topology or bounds: "
            f"vertices={vertices} polygons={polygons} triangles={triangles} "
            f"x={minimum_x:.8f}..{maximum_x:.8f}"
        )

    for obj in list(bpy.data.objects):
        if obj.type != "MESH" or obj.name not in SOURCE_MESH_NAMES:
            bpy.data.objects.remove(obj, do_unlink=True)

    # None of the source images are packed and their provenance is not
    # established.  Remove all source material/image datablocks before any
    # runtime material is constructed.
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.data.materials.clear()
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        bpy.data.images.remove(image)


def write_texture(
    name: str,
    path: Path,
    pixels: np.ndarray,
    non_color: bool,
) -> None:
    height, width, channels = pixels.shape
    if channels != 4:
        raise RuntimeError(f"Texture {name} is not RGBA.")
    pixel_values = np.ascontiguousarray(pixels, dtype=np.float32).reshape(-1)
    if not np.isfinite(pixel_values).all():
        raise RuntimeError(f"Texture {name} contains non-finite values.")
    image = bpy.data.images.new(name, width=width, height=height, alpha=True)
    # Blender 4.5 clears a generated image's color payload when its color
    # space is assigned after pixel upload (even when assigning "sRGB" to the
    # default sRGB value), producing a valid but all-black PNG.  Establish the
    # color space first, then upload the contiguous buffer in one operation.
    image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    image.pixels.foreach_set(pixel_values)
    image.update()
    image.file_format = "PNG"
    image.filepath_raw = str(path)
    image.save()
    image.reload()
    disk_values = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(disk_values)
    disk_rgba = disk_values.reshape((-1, 4))
    disk_rgb = disk_rgba[:, :3]
    disk_rgb_min = float(disk_rgb.min())
    disk_rgb_max = float(disk_rgb.max())
    disk_rgb_mean = float(disk_rgb.mean())
    if disk_rgb_max <= 0.0001:
        raise RuntimeError(f"Saved texture {path} contains no color payload.")
    if float(disk_rgba[:, 3].min()) < 0.999:
        raise RuntimeError(f"Saved texture {path} lost its opaque alpha payload.")
    print(
        "AK47_TEXTURE "
        f"name={name} size={width}x{height} colorspace={image.colorspace_settings.name} "
        f"rgb={disk_rgb_min:.4f}..{disk_rgb_max:.4f} mean={disk_rgb_mean:.4f}"
    )
    bpy.data.images.remove(image)


def generate_project_textures() -> None:
    TEXTURE_DIRECTORY.mkdir(parents=True, exist_ok=True)
    size = 1024
    u = np.linspace(0.0, 1.0, size, endpoint=False, dtype=np.float32)
    v = np.linspace(0.0, 1.0, size, endpoint=False, dtype=np.float32)
    uu, vv = np.meshgrid(u, v)
    # Broad, softly wandering laminate bands carry the read at player-camera
    # distance.  Fine grain and pores remain subordinate so the finish reads
    # as stained walnut rather than bright striped plastic.
    broad = np.sin(2.0 * pi * (uu * 2.25 + np.sin(vv * 2.0 * pi) * 0.12))
    fine = np.sin(2.0 * pi * (uu * 13.0 + vv * 0.45))
    pores = np.sin(2.0 * pi * (uu * 43.0 - vv * 2.0))
    grain = np.clip(0.50 + broad * 0.22 + fine * 0.035 + pores * 0.008, 0.0, 1.0)
    dark = np.array((0.024, 0.008, 0.004), dtype=np.float32)
    warm = np.array((0.105, 0.034, 0.012), dtype=np.float32)
    highlight = np.array((0.215, 0.076, 0.022), dtype=np.float32)
    mid = dark[None, None, :] * (1.0 - grain[..., None]) + warm[None, None, :] * grain[..., None]
    bright = np.clip((grain - 0.72) / 0.28, 0.0, 1.0)[..., None]
    color = mid * (1.0 - bright) + highlight[None, None, :] * bright
    edge_wear = 0.985 + 0.015 * np.sin(2.0 * pi * vv * 1.5)
    color *= edge_wear[..., None]
    base = np.concatenate(
        (np.clip(color, 0.0, 1.0), np.ones((size, size, 1), dtype=np.float32)),
        axis=2,
    )
    rough_value = np.clip(
        0.545 + (1.0 - grain) * 0.105 + np.abs(fine) * 0.018,
        0.52,
        0.68,
    )
    roughness = np.stack(
        (rough_value, rough_value, rough_value, np.ones_like(rough_value)),
        axis=2,
    )
    if float(base[:, :, 0].max()) > 0.22:
        raise RuntimeError("The generated AK wood red channel exceeds its art budget.")
    if float(rough_value.min()) < 0.52 or float(rough_value.max()) > 0.68:
        raise RuntimeError("The generated AK wood roughness exceeds its art budget.")
    write_texture("AK47WoodBaseColor", WOOD_BASE_COLOR, base, non_color=False)
    write_texture("AK47WoodRoughness", WOOD_ROUGHNESS, roughness, non_color=True)


def scalar_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    material["source_creator"] = SOURCE_CREATOR
    material["source_url"] = SOURCE_URL
    material["source_license"] = SOURCE_LICENSE
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Material {name} has no Principled BSDF.")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return material


def wood_material() -> bpy.types.Material:
    material = scalar_material(
        "AK47LaminatedWoodPBR",
        (0.095, 0.030, 0.010, 1.0),
        0.0,
        0.60,
    )
    color_image = bpy.data.images.load(str(WOOD_BASE_COLOR), check_existing=False)
    color_image.colorspace_settings.name = "sRGB"
    rough_image = bpy.data.images.load(str(WOOD_ROUGHNESS), check_existing=False)
    rough_image.colorspace_settings.name = "Non-Color"
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    color_node = nodes.new("ShaderNodeTexImage")
    color_node.name = "ProjectWoodBaseColor"
    color_node.image = color_image
    rough_node = nodes.new("ShaderNodeTexImage")
    rough_node.name = "ProjectWoodRoughness"
    rough_node.image = rough_image
    links.new(color_node.outputs["Color"], principled.inputs["Base Color"])
    links.new(rough_node.outputs["Color"], principled.inputs["Roughness"])
    material["texture_author"] = "Operation Steel Tide project contributors"
    material["texture_license"] = "MIT"
    material["texture_build"] = "scripts/blender/build_taradavies_ak47.py"
    return material


def build_materials() -> dict[str, bpy.types.Material]:
    return {
        "wood": wood_material(),
        "receiver": scalar_material(
            "AK47BluedReceiverSteel",
            (0.034, 0.041, 0.043, 1.0),
            0.74,
            0.46,
        ),
        "phosphate": scalar_material(
            "AK47PhosphateSteel",
            (0.050, 0.056, 0.055, 1.0),
            0.62,
            0.54,
        ),
        "bolt": scalar_material(
            "AK47WornBoltSteel",
            (0.115, 0.128, 0.128, 1.0),
            0.82,
            0.36,
        ),
        "magazine": scalar_material(
            "AK47BakeliteMagazine",
            (0.095, 0.028, 0.012, 1.0),
            0.05,
            0.56,
        ),
        "rubber": scalar_material(
            "AK47ButtpadRubber",
            (0.012, 0.014, 0.013, 1.0),
            0.0,
            0.82,
        ),
    }


def assign_material(
    obj: bpy.types.Object,
    material: bpy.types.Material,
) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def separate_front_iron() -> str:
    source = bpy.data.objects["Cylinder"]
    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in source.data.polygons:
        center = source.matrix_world @ polygon.center
        polygon.select = (
            center.x > FRONT_IRON_MIN_SOURCE_X
            and center.z > FRONT_IRON_MIN_SOURCE_Z
        )
    selected = sum(1 for polygon in source.data.polygons if polygon.select)
    if selected != FRONT_IRON_SOURCE_POLYGONS:
        raise RuntimeError(
            f"Front-sight selection drifted: {selected} != {FRONT_IRON_SOURCE_POLYGONS}"
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
        raise RuntimeError(f"Expected one front-sight mesh, found {len(separated)}")
    separated[0].name = "FrontIronSource"
    return separated[0].name


def add_authored_rail_adapter() -> str:
    source = bpy.data.objects["Cube.017"]
    adapter = source.copy()
    adapter.data = source.data.copy()
    bpy.context.collection.objects.link(adapter)
    adapter.name = "OpticRailAdapterSource"
    # Refit the authored rear-sight base into a compact dust-cover bridge.  No
    # primitive or generated rail mesh is introduced.
    adapter.location = Vector((-0.72, 0.0039, 1.37))
    adapter.scale = Vector(
        (
            source.scale.x * 1.45,
            source.scale.y * 1.65,
            source.scale.z * 0.62,
        )
    )
    adapter["source_component"] = "Cube.017 rear-sight base"
    adapter["dcc_adaptation"] = "Duplicated and transform-fitted as optic rail bridge"
    return adapter.name


def apply_lod_and_normalize(level: int) -> None:
    for obj in [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]:
        for modifier in obj.modifiers:
            if modifier.type == "SUBSURF":
                modifier.levels = level
                modifier.render_levels = level
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
        obj.data.transform(NORMALIZATION @ obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        obj.data.validate(clean_customdata=False)


def find_optic_rail_contact(adapter: bpy.types.Object) -> Vector:
    coordinates = [vertex.co.copy() for vertex in adapter.data.vertices]
    maximum_z = max(coordinate.z for coordinate in coordinates)
    peak = max(coordinates, key=lambda coordinate: coordinate.z)
    contact_y = peak.y
    ray_origin = Vector((peak.x, contact_y, maximum_z + 0.05))
    hit, location, normal, _face_index = adapter.ray_cast(
        ray_origin,
        Vector((0.0, 0.0, -1.0)),
        distance=0.10,
    )
    if not hit or normal.z < 0.90:
        raise RuntimeError(
            "Unable to resolve a horizontal contact face on the authored optic adapter."
        )
    if abs(location.z - maximum_z) > 0.003:
        raise RuntimeError(
            "Resolved optic contact is not on the adapter top surface: "
            f"contact={location.z:.6f} top={maximum_z:.6f}"
        )
    print(
        "AK47_OPTIC_CONTACT "
        f"x={location.x:.6f} y={location.y:.6f} z={location.z:.6f} "
        f"adapter_top={maximum_z:.6f} delta_mm={(location.z - maximum_z) * 1000.0:.3f}"
    )
    # The adapter has a shallow center groove, so retain the weapon centerline
    # while using the verified side-rail contact plane for optic height.
    return Vector((0.0, location.y, location.z))


def consolidate_material_slots(obj: bpy.types.Object) -> None:
    old_materials = list(obj.data.materials)
    unique: list[bpy.types.Material] = []
    remap: dict[int, int] = {}
    for old_index, material in enumerate(old_materials):
        if material not in unique:
            unique.append(material)
        remap[old_index] = unique.index(material)
    for polygon in obj.data.polygons:
        polygon.material_index = remap[polygon.material_index]
    obj.data.materials.clear()
    for material in unique:
        obj.data.materials.append(material)


def join_objects(names: list[str], result_name: str) -> bpy.types.Object:
    objects = [bpy.data.objects[name] for name in names]
    if not objects:
        raise RuntimeError(f"No objects supplied for {result_name}.")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = objects[0]
    result.name = result_name
    result.data.name = f"{result_name}Mesh"
    consolidate_material_slots(result)
    print(
        "AK47_JOIN "
        f"name={result_name} materials={[material.name for material in result.data.materials]} "
        f"polygons={len(result.data.polygons)}"
    )
    return result


def empty(name: str, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    result = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(result)
    if parent is not None:
        result.parent = parent
    return result


def parent_geometry_at_pivot(
    geometry: bpy.types.Object,
    parent: bpy.types.Object,
    pivot: Vector,
) -> None:
    geometry.data.transform(Matrix.Translation(-pivot))
    geometry.parent = parent
    geometry.location = Vector()
    parent.location = pivot


def build_runtime_hierarchy(level: int) -> bpy.types.Object:
    front_iron_source = separate_front_iron()
    adapter_source = add_authored_rail_adapter()
    materials = build_materials()
    for obj in [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]:
        if obj.name in WOOD_SOURCES:
            assign_material(obj, materials["wood"])
        elif obj.name in RUBBER_SOURCES:
            assign_material(obj, materials["rubber"])
        elif obj.name in MAGAZINE_SOURCES:
            assign_material(obj, materials["magazine"])
        elif obj.name in BOLT_SOURCES:
            assign_material(obj, materials["bolt"])
        elif obj.name == adapter_source:
            assign_material(obj, materials["phosphate"])
        else:
            assign_material(obj, materials["receiver"] if obj.name in {"Cube.002", "Plane"} else materials["phosphate"])

    apply_lod_and_normalize(level)

    magazine_geometry = bpy.data.objects[MAGAZINE_SOURCE]
    magazine_geometry.name = "MagazineGeometry"
    magazine_geometry.data.name = "MagazineMesh"
    charging_geometry = join_objects(
        list(CHARGING_HANDLE_SOURCES),
        "ChargingHandleGeometry",
    )
    rear_iron_geometry = join_objects(
        list(REAR_IRON_SOURCES),
        "RearIronGeometry",
    )
    front_iron_geometry = bpy.data.objects[front_iron_source]
    front_iron_geometry.name = "FrontIronGeometry"
    front_iron_geometry.data.name = "FrontIronMesh"
    stock_geometry = bpy.data.objects["Cube"]
    stock_geometry.name = "StockWoodGeometry"
    stock_geometry.data.name = "StockWoodMesh"
    buttpad_geometry = bpy.data.objects["Cube.001"]
    buttpad_geometry.name = "StockButtpadGeometry"
    buttpad_geometry.data.name = "StockButtpadMesh"
    rail_adapter = bpy.data.objects[adapter_source]
    rail_adapter.name = "OpticRailAdapterGeometry"
    rail_adapter.data.name = "OpticRailAdapterMesh"
    optic_rail_contact = find_optic_rail_contact(rail_adapter)

    receiver_geometry = join_objects(
        ["Cube.002", "Plane"],
        "ReceiverGeometry",
    )
    furniture_geometry = join_objects(
        ["Cube.003", "Cube.008", "Cube.009"],
        "FurnitureGeometry",
    )
    bolt_hardware_geometry = join_objects(
        ["Cube.016", "Cube.019"],
        "BoltHardwareGeometry",
    )

    reserved = {
        magazine_geometry,
        charging_geometry,
        rear_iron_geometry,
        front_iron_geometry,
        stock_geometry,
        buttpad_geometry,
        rail_adapter,
        receiver_geometry,
        furniture_geometry,
        bolt_hardware_geometry,
    }
    body_sources = [
        obj.name
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj not in reserved
    ]
    body_geometry = join_objects(body_sources, "WeaponBodyGeometry")

    root = empty("SteelTideAK47")
    root["source_creator"] = SOURCE_CREATOR
    root["source_url"] = SOURCE_URL
    root["source_license"] = SOURCE_LICENSE
    root["source_sha256"] = SOURCE_SHA256
    root["source_external_images_used"] = False
    root["dcc_lod"] = level
    root["project_owned_wood_textures"] = True

    for geometry in (
        body_geometry,
        receiver_geometry,
        furniture_geometry,
        bolt_hardware_geometry,
        rail_adapter,
    ):
        geometry.parent = root

    stock = empty("Stock", root)
    stock_geometry.parent = stock
    buttpad_geometry.parent = stock

    magazine = empty("Magazine", root)
    parent_geometry_at_pivot(magazine_geometry, magazine, MAGAZINE_PIVOT)
    magazine_grip = empty("MagazineGrip", magazine)
    magazine_grip.location = MAGAZINE_GRIP
    spare_magazine = empty("SpareMagazine", root)
    spare_magazine.location = SPARE_MAGAZINE_PIVOT
    spare_magazine.hide_render = True
    spare_magazine_grip = empty("SpareMagazineGrip", spare_magazine)
    spare_magazine_grip.location = SPARE_MAGAZINE_GRIP
    spare_geometry = magazine_geometry.copy()
    spare_geometry.data = magazine_geometry.data
    spare_geometry.name = "SpareMagazineGeometry"
    bpy.context.collection.objects.link(spare_geometry)
    spare_geometry.parent = spare_magazine
    spare_geometry.location = Vector()

    charging_handle = empty("ChargingHandle", root)
    parent_geometry_at_pivot(
        charging_geometry,
        charging_handle,
        CHARGING_HANDLE_PIVOT,
    )

    rear_iron = empty("RearIronSight", root)
    rear_iron_geometry.parent = rear_iron
    front_iron = empty("FrontIronSight", root)
    front_iron_geometry.parent = front_iron

    empty("Foregrip", root)
    muzzle_device = empty("MuzzleDevice", root)
    muzzle_tip = empty("MuzzleDeviceTip", muzzle_device)
    muzzle_tip.location = MUZZLE_TIP
    suppressor = empty("Suppressor", root)
    suppressor.hide_render = True
    suppressor_tip = empty("SuppressorTip", suppressor)
    suppressor_tip.location = SUPPRESSOR_TIP
    optic_mount = empty("OpticMount", root)
    optic_mount.hide_render = True
    optic_anchor = empty("OpticReticleAnchor", optic_mount)
    optic_anchor.location = optic_rail_contact + Vector((0.0, 0.0, 0.070))
    optic_contact = empty("OpticRailContact", root)
    optic_contact.location = optic_rail_contact
    ejection_port = empty("EjectionPort", root)
    ejection_port.location = EJECTION_PORT

    mesh_objects = [
        obj for obj in root.children_recursive if obj.type == "MESH"
    ]
    if {obj.name for obj in mesh_objects} != {
        "WeaponBodyGeometry",
        "ReceiverGeometry",
        "FurnitureGeometry",
        "BoltHardwareGeometry",
        "OpticRailAdapterGeometry",
        "StockWoodGeometry",
        "StockButtpadGeometry",
        "MagazineGeometry",
        "SpareMagazineGeometry",
        "ChargingHandleGeometry",
        "RearIronGeometry",
        "FrontIronGeometry",
    }:
        raise RuntimeError(
            f"Unexpected AK runtime mesh contract: {[obj.name for obj in mesh_objects]}"
        )
    validate_blender_runtime_hierarchy(f"built LOD {level}")
    return root


def select_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (root, *root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_asset(root: bpy.types.Object, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_extras=True,
        export_yup=True,
    )


def read_glb_json(path: Path) -> dict:
    payload = path.read_bytes()
    if payload[:4] != b"glTF" or len(payload) < 20:
        raise RuntimeError(f"Invalid GLB header: {path}")
    json_length, json_type = struct.unpack_from("<II", payload, 12)
    if json_type != 0x4E4F534A:
        raise RuntimeError(f"GLB JSON chunk is unavailable: {path}")
    return json.loads(payload[20 : 20 + json_length].decode("utf-8"))


def validate_export(path: Path, level: int) -> tuple[int, int, int, int]:
    document = read_glb_json(path)
    nodes = document.get("nodes", [])
    node_names = [node.get("name", "") for node in nodes]
    require_exact_node_names(node_names, f"exported {path.name}")
    actual_parents = {name: None for name in node_names}
    for parent_index, node in enumerate(nodes):
        parent_name = node_names[parent_index]
        for child_index in node.get("children", []):
            if child_index < 0 or child_index >= len(nodes):
                raise RuntimeError(
                    f"Exported {path.name} has invalid child index {child_index}."
                )
            child_name = node_names[child_index]
            if actual_parents[child_name] is not None:
                raise RuntimeError(
                    f"Exported {path.name} gives {child_name} multiple parents."
                )
            actual_parents[child_name] = parent_name
    require_exact_parent_contract(actual_parents, f"exported {path.name}")
    scenes = document.get("scenes", [])
    scene_index = document.get("scene", 0)
    if scene_index < 0 or scene_index >= len(scenes):
        raise RuntimeError(f"Exported {path.name} has no valid default scene.")
    scene_root_names = [
        node_names[node_index]
        for node_index in scenes[scene_index].get("nodes", [])
    ]
    if scene_root_names != ["SteelTideAK47"]:
        raise RuntimeError(
            f"Exported {path.name} has unexpected scene roots: {scene_root_names}"
        )
    accessors = document.get("accessors", [])
    triangles = 0
    meshes = document.get("meshes", [])
    for mesh in meshes:
        for primitive in mesh.get("primitives", []):
            if "indices" in primitive:
                triangles += accessors[primitive["indices"]]["count"] // 3
    if level == 1:
        expected_triangles = EXPECTED_FP_TRIANGLE_COUNT
    elif level == 0:
        expected_triangles = EXPECTED_WORLD_TRIANGLE_COUNT
    else:
        raise RuntimeError(f"Unsupported AK export LOD: {level}")
    materials = len(document.get("materials", []))
    images = len(document.get("images", []))
    textures = len(document.get("textures", []))
    actual_contract = (
        triangles,
        len(meshes),
        materials,
        images,
        textures,
    )
    expected_contract = (
        expected_triangles,
        EXPECTED_RUNTIME_MESH_COUNT,
        EXPECTED_RUNTIME_MATERIAL_COUNT,
        EXPECTED_RUNTIME_IMAGE_COUNT,
        EXPECTED_RUNTIME_TEXTURE_COUNT,
    )
    if actual_contract != expected_contract:
        raise RuntimeError(
            f"Unexpected {path.name} semantic export contract: "
            f"actual={actual_contract} expected={expected_contract} "
            "fields=(triangles,meshes,materials,images,textures)"
        )
    return triangles, len(meshes), materials, images


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    expected_textures = {
        WOOD_BASE_COLOR.resolve(),
        WOOD_ROUGHNESS.resolve(),
    }
    saved_textures: set[Path] = set()
    for image in bpy.data.images:
        resolved_path = Path(bpy.path.abspath(image.filepath)).resolve()
        if resolved_path not in expected_textures:
            raise RuntimeError(
                f"Unexpected external image in authored AK blend: {resolved_path}"
            )
        image.pack()
        if image.packed_file is None or image.packed_file.size <= 0:
            raise RuntimeError(f"Failed to pack authored AK texture: {resolved_path}")
        image.filepath = f"//textures/{resolved_path.name}"
        saved_textures.add(resolved_path)
    if saved_textures != expected_textures:
        raise RuntimeError(
            "Authored AK blend did not include the complete project texture set."
        )
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(
        filepath=str(OUTPUT_BLEND),
        relative_remap=False,
    )


def validate_saved_source() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(OUTPUT_BLEND), load_ui=False)
    expected_paths = {
        f"//textures/{WOOD_BASE_COLOR.name}",
        f"//textures/{WOOD_ROUGHNESS.name}",
    }
    actual_paths = {
        image.filepath.replace("\\", "/")
        for image in bpy.data.images
    }
    if actual_paths != expected_paths:
        raise RuntimeError(
            f"Authored AK blend has unexpected image paths: {sorted(actual_paths)}"
        )
    unpacked = [
        image.name
        for image in bpy.data.images
        if image.packed_file is None or image.packed_file.size <= 0
    ]
    if unpacked:
        raise RuntimeError(
            f"Authored AK blend has unpacked or empty images: {sorted(unpacked)}"
        )
    validate_blender_runtime_hierarchy("reopened authored source")
    source_counts = (
        len(bpy.data.meshes),
        len(bpy.data.materials),
        len(bpy.data.images),
    )
    expected_source_counts = (
        EXPECTED_RUNTIME_MESH_COUNT,
        EXPECTED_RUNTIME_MATERIAL_COUNT,
        EXPECTED_RUNTIME_IMAGE_COUNT,
    )
    if source_counts != expected_source_counts:
        raise RuntimeError(
            "Authored AK blend has unexpected resource counts: "
            f"actual={source_counts} expected={expected_source_counts} "
            "fields=(meshes,materials,images)"
        )
    print(
        "AK47_BLEND "
        f"images={len(bpy.data.images)} packed={len(bpy.data.images)} "
        f"paths={sorted(actual_paths)} nodes={len(bpy.data.objects)}"
    )


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_preview(root: bpy.types.Object) -> None:
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    preview_world = bpy.data.worlds.new("AK47PreviewWorld")
    preview_world.use_nodes = True
    background = preview_world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.010, 0.014, 0.018, 1.0)
    background.inputs["Strength"].default_value = 0.22
    scene.world = preview_world
    scene.view_settings.look = "Medium High Contrast"
    scene.view_settings.exposure = -0.7

    floor_material = scalar_material(
        "PreviewFloor",
        (0.025, 0.030, 0.034, 1.0),
        0.0,
        0.72,
    )
    bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0.0, 0.45, -0.285))
    floor = bpy.context.active_object
    floor.name = "PreviewFloor"
    floor.data.materials.append(floor_material)

    for name, energy, color, location, size in (
        ("Key", 420.0, (1.0, 0.72, 0.46), (1.55, -0.55, 1.35), 1.7),
        ("Fill", 240.0, (0.40, 0.62, 1.0), (-1.40, 0.15, 0.85), 1.5),
        ("Rim", 360.0, (0.62, 0.82, 1.0), (-0.30, 1.85, 1.10), 1.25),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.color = color
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        look_at(light, Vector((0.0, 0.48, -0.02)))

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = Vector((1.62, -1.52, 0.72))
    camera_data.lens = 58.0
    look_at(camera, Vector((0.0, 0.47, -0.03)))
    scene.camera = camera
    scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)


def build_variant(level: int, path: Path, save_blend: bool, preview: bool) -> tuple[int, int, int, int]:
    open_and_validate_source()
    if not WOOD_BASE_COLOR.is_file() or not WOOD_ROUGHNESS.is_file():
        generate_project_textures()
    root = build_runtime_hierarchy(level)
    export_asset(root, path)
    result = validate_export(path, level)
    if save_blend:
        save_source()
    if preview:
        render_preview(root)
    return result


def main() -> None:
    require_source()
    # Generate the project-owned texture payload from a source-clean Blender
    # state; a subsequent source reload cannot reintroduce the missing images.
    generate_project_textures()
    fp_stats = build_variant(1, OUTPUT_FP_GLB, save_blend=True, preview=True)
    world_stats = build_variant(0, OUTPUT_WORLD_GLB, save_blend=False, preview=False)
    validate_saved_source()
    print(
        "STEEL_TIDE_AK47_EXPORT "
        f"source_sha256={SOURCE_SHA256} "
        f"fp_triangles={fp_stats[0]} fp_meshes={fp_stats[1]} "
        f"world_triangles={world_stats[0]} world_meshes={world_stats[1]} "
        f"materials={fp_stats[2]} images={fp_stats[3]} "
        f"fp={OUTPUT_FP_GLB} world={OUTPUT_WORLD_GLB} "
        f"blend={OUTPUT_BLEND} preview={PREVIEW_PATH}"
    )


if __name__ == "__main__":
    main()
