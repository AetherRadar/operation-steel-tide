"""Author the compact Steel Tide demolition device.

Run from the repository root with Blender 4.5 LTS:
    blender --background --factory-startup --python scripts/blender/build_demolition_device.py

The script saves the editable source, exports an embedded GLB, renders a studio
preview, and round-trips the GLB to verify the runtime node contract.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "demolition_device"
ASSET_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_demolition_device"
SOURCE_BLEND = SOURCE_DIR / "demolition_device.blend"
OUTPUT_GLB = ASSET_DIR / "demolition_device.glb"
PREVIEW_PNG = ASSET_DIR / "demolition_device_preview.png"
ROOT_NAME = "SteelTideDemolitionDevice"
REQUIRED_NODES = (
    ROOT_NAME,
    "DeviceCase",
    "DeviceScreen",
    "DeviceStatusLight",
    "DeviceCarrySocket",
)


def configure_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 960
    scene.render.resolution_percentage = 100
    scene.view_settings.look = "AgX - Medium High Contrast"


def material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    *,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    result.diffuse_color = color
    result.metallic = metallic
    result.roughness = roughness
    principled = result.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Missing Principled BSDF for {name}")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    coat = principled.inputs.get("Coat Weight")
    if coat is not None:
        coat.default_value = 0.18 if metallic > 0.3 else 0.06
    if emission is not None:
        emission_color = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        emission_power = principled.inputs.get("Emission Strength")
        if emission_color is not None:
            emission_color.default_value = emission
        if emission_power is not None:
            emission_power.default_value = emission_strength
    return result


def palette() -> dict[str, bpy.types.Material]:
    return {
        "shell": material("DeviceShellGraphite", (0.018, 0.025, 0.027, 1.0), 0.58, 0.31),
        "shell_top": material("DeviceShellTop", (0.055, 0.075, 0.078, 1.0), 0.42, 0.36),
        "rubber": material("DeviceImpactRubber", (0.008, 0.011, 0.012, 1.0), 0.04, 0.82),
        "metal": material("DeviceGunmetal", (0.16, 0.19, 0.19, 1.0), 0.82, 0.24),
        "orange": material(
            "DeviceSignalOrange",
            (1.0, 0.19, 0.012, 1.0),
            0.18,
            0.28,
            emission=(1.0, 0.035, 0.002, 1.0),
            emission_strength=1.8,
        ),
        "amber": material(
            "DeviceSignalAmber",
            (1.0, 0.58, 0.025, 1.0),
            0.12,
            0.22,
            emission=(1.0, 0.22, 0.002, 1.0),
            emission_strength=4.5,
        ),
        "screen": material("DeviceScreenGlass", (0.004, 0.03, 0.035, 1.0), 0.25, 0.12),
        "cyan": material(
            "DeviceScreenCyan",
            (0.015, 0.82, 0.9, 1.0),
            0.05,
            0.16,
            emission=(0.005, 0.62, 0.8, 1.0),
            emission_strength=6.0,
        ),
        "ivory": material("DeviceStencilIvory", (0.72, 0.75, 0.67, 1.0), 0.02, 0.66),
    }


def empty(name: str, parent: bpy.types.Object | None, size: float = 0.02) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = size
    return obj


def tag(obj: bpy.types.Object) -> bpy.types.Object:
    obj["asset_author"] = "Operation Steel Tide project"
    obj["asset_license"] = "MIT"
    return obj


def rounded_box(
    name: str,
    parent: bpy.types.Object,
    size: tuple[float, float, float],
    location: tuple[float, float, float],
    mat: bpy.types.Material,
    *,
    bevel: float = 0.004,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = tag(bpy.context.active_object)
    obj.name = name
    obj.parent = parent
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler = rotation
    obj.data.materials.append(mat)
    if bevel > 0.0:
        modifier = obj.modifiers.new("AuthoredEdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        modifier.limit_method = "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return obj


def cylinder(
    name: str,
    parent: bpy.types.Object,
    radius: float,
    depth: float,
    location: tuple[float, float, float],
    mat: bpy.types.Material,
    *,
    vertices: int = 20,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = tag(bpy.context.active_object)
    obj.name = name
    obj.parent = parent
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("AuthoredEdgeSoftening", "BEVEL")
    bevel.width = min(radius * 0.16, 0.0018)
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def create_device(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> None:
    case = empty("DeviceCase", root, 0.04)
    rounded_box("LowerArmorShell", case, (0.338, 0.194, 0.060), (0.0, 0.0, 0.044), mats["shell"], bevel=0.009)
    rounded_box("UpperControlShell", case, (0.306, 0.166, 0.042), (0.0, -0.002, 0.091), mats["shell_top"], bevel=0.007)
    rounded_box("SignalSpineLeft", case, (0.018, 0.142, 0.052), (-0.154, 0.0, 0.073), mats["orange"], bevel=0.005)
    rounded_box("SignalSpineRight", case, (0.018, 0.142, 0.052), (0.154, 0.0, 0.073), mats["orange"], bevel=0.005)

    for x_index, x in enumerate((-0.156, 0.156)):
        for y_index, y in enumerate((-0.078, 0.078)):
            rounded_box(
                f"CornerBumper_{x_index}_{y_index}",
                case,
                (0.032, 0.044, 0.076),
                (x, y, 0.050),
                mats["rubber"],
                bevel=0.008,
            )
            cylinder(
                f"CornerFastener_{x_index}_{y_index}",
                case,
                0.006,
                0.004,
                (x, y, 0.091),
                mats["metal"],
                vertices=16,
            )

    screen = rounded_box(
        "DeviceScreen",
        case,
        (0.144, 0.076, 0.009),
        (-0.038, -0.015, 0.116),
        mats["screen"],
        bevel=0.004,
    )
    screen["screen_contract"] = "emissive-status-display"
    rounded_box("ScreenMainReadout", screen, (0.094, 0.008, 0.0025), (-0.015, -0.012, 0.006), mats["cyan"], bevel=0.001)
    for index, width in enumerate((0.078, 0.060, 0.042)):
        rounded_box(
            f"ScreenTelemetry_{index}",
            screen,
            (width, 0.005, 0.0023),
            (-0.022, 0.004 + index * 0.012, 0.006),
            mats["cyan"],
            bevel=0.0008,
        )
    for index, x in enumerate((0.036, 0.051, 0.066)):
        cylinder(
            f"ScreenIndicator_{index}",
            screen,
            0.004,
            0.0028,
            (x, 0.019, 0.006),
            mats["amber" if index == 2 else "cyan"],
            vertices=16,
        )

    keypad_origin = (0.078, -0.025)
    for row in range(3):
        for column in range(3):
            x = keypad_origin[0] + (column - 1) * 0.020
            y = keypad_origin[1] + (row - 1) * 0.020
            rounded_box(
                f"Keypad_{row}_{column}",
                case,
                (0.014, 0.014, 0.008),
                (x, y, 0.116),
                mats["ivory" if row == 2 and column == 1 else "metal"],
                bevel=0.003,
            )

    status = cylinder(
        "DeviceStatusLight",
        case,
        0.010,
        0.010,
        (0.118, 0.045, 0.116),
        mats["amber"],
        vertices=24,
    )
    status["status_light"] = "high-visibility-objective-beacon"

    for index, y in enumerate((-0.054, -0.027, 0.0, 0.027, 0.054)):
        rounded_box(
            f"SideVent_{index}",
            case,
            (0.004, 0.015, 0.014),
            (-0.170, y, 0.047),
            mats["metal"],
            bevel=0.001,
        )

    rounded_box("CarryHandleLeft", case, (0.018, 0.018, 0.052), (-0.092, 0.084, 0.115), mats["rubber"], bevel=0.006)
    rounded_box("CarryHandleRight", case, (0.018, 0.018, 0.052), (0.092, 0.084, 0.115), mats["rubber"], bevel=0.006)
    rounded_box("CarryHandleGrip", case, (0.202, 0.020, 0.020), (0.0, 0.084, 0.139), mats["rubber"], bevel=0.008)
    rounded_box("CarryHandleSignal", case, (0.126, 0.022, 0.008), (0.0, 0.082, 0.144), mats["orange"], bevel=0.003)

    for index, x in enumerate((-0.122, 0.122)):
        cylinder(
            f"AntennaBase_{index}",
            case,
            0.010,
            0.018,
            (x, -0.069, 0.119),
            mats["metal"],
            vertices=20,
        )
        cylinder(
            f"Antenna_{index}",
            case,
            0.004,
            0.050,
            (x, -0.069, 0.151),
            mats["orange"],
            vertices=16,
        )

    for index, x in enumerate((-0.080, -0.040, 0.0, 0.040, 0.080)):
        rounded_box(
            f"FrontWarningBar_{index}",
            case,
            (0.024, 0.006, 0.020),
            (x, -0.098, 0.055),
            mats["orange" if index % 2 == 0 else "ivory"],
            bevel=0.002,
            rotation=(0.0, math.radians(-22.0), 0.0),
        )

    carry_socket = empty("DeviceCarrySocket", root, 0.018)
    carry_socket.location = (0.0, 0.0, 0.072)
    carry_socket["socket_space"] = "device-local"
    carry_socket["mount"] = "operator-right-hip"


def hierarchy(root: bpy.types.Object) -> list[bpy.types.Object]:
    result: list[bpy.types.Object] = []

    def visit(obj: bpy.types.Object) -> None:
        result.append(obj)
        for child in obj.children:
            visit(child)

    visit(root)
    return result


def mesh_statistics(root: bpy.types.Object) -> tuple[int, int, int, tuple[float, float, float]]:
    meshes = [obj for obj in hierarchy(root) if obj.type == "MESH"]
    triangles = 0
    corners: list[Vector] = []
    material_names: set[str] = set()
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        material_names.update(slot.material.name for slot in obj.material_slots if slot.material)
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    minimum = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    maximum = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    dimensions = maximum - minimum
    return len(meshes), triangles, len(material_names), tuple(float(value) for value in dimensions)


def validate_scene(root: bpy.types.Object) -> tuple[int, int, int, tuple[float, float, float]]:
    names = [obj.name for obj in hierarchy(root)]
    missing = [name for name in REQUIRED_NODES if names.count(name) != 1]
    if missing:
        raise RuntimeError(f"Missing demolition-device contract nodes: {missing}")
    stats = mesh_statistics(root)
    meshes, triangles, materials, dimensions = stats
    if meshes < 36:
        raise RuntimeError(f"Demolition device has only {meshes} authored mesh pieces")
    if not 2500 <= triangles <= 18000:
        raise RuntimeError(f"Demolition device triangle budget violation: {triangles}")
    if not 8 <= materials <= 11:
        raise RuntimeError(f"Unexpected demolition-device material count: {materials}")
    if not (0.32 <= dimensions[0] <= 0.36 and 0.18 <= dimensions[1] <= 0.22 and 0.14 <= dimensions[2] <= 0.19):
        raise RuntimeError(f"Unexpected demolition-device bounds: {dimensions}")
    return stats


def select_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in hierarchy(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def save_source(root: bpy.types.Object) -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    root["asset_author"] = "Operation Steel Tide project"
    root["asset_license"] = "MIT"
    root["asset_kind"] = "compact-demolition-objective-device"
    root["units"] = "metres"
    root["forward_axis"] = "Blender +Y / Godot -Z"
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND), compress=True, check_existing=False)
    if not SOURCE_BLEND.is_file() or SOURCE_BLEND.stat().st_size < 8192:
        raise RuntimeError("Blender did not save the demolition-device source")


def export_glb(root: bpy.types.Object) -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    result = bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_animations=False,
    )
    if result != {"FINISHED"} or not OUTPUT_GLB.is_file() or OUTPUT_GLB.stat().st_size < 16384:
        raise RuntimeError(f"Blender could not export {OUTPUT_GLB}: {result}")


def glb_document(path: Path) -> dict[str, object]:
    payload = path.read_bytes()
    if len(payload) < 20 or payload[:4] != b"glTF":
        raise RuntimeError("Generated demolition device is not a binary glTF")
    _, version, length = struct.unpack_from("<III", payload, 0)
    chunk_length, chunk_type = struct.unpack_from("<II", payload, 12)
    if version != 2 or length != len(payload) or chunk_type != 0x4E4F534A:
        raise RuntimeError("Generated demolition-device GLB header is invalid")
    return json.loads(payload[20 : 20 + chunk_length].decode("utf-8"))


def verify_glb(expected: tuple[int, int, int, tuple[float, float, float]]) -> tuple[int, int, int, tuple[float, float, float]]:
    document = glb_document(OUTPUT_GLB)
    if any("uri" in item for item in document.get("buffers", [])) or any("uri" in item for item in document.get("images", [])):
        raise RuntimeError("Demolition-device GLB depends on external data")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB), import_pack_images=True)
    if result != {"FINISHED"}:
        raise RuntimeError("Blender could not round-trip the demolition-device GLB")
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None:
        raise RuntimeError("Round-tripped demolition-device GLB lost its root")
    names = [obj.name for obj in hierarchy(root)]
    missing = [name for name in REQUIRED_NODES if names.count(name) != 1]
    if missing:
        raise RuntimeError(f"Round-tripped GLB lost contract nodes: {missing}")
    if root.get("asset_license") != "MIT" or root.get("asset_author") != "Operation Steel Tide project":
        raise RuntimeError("Round-tripped GLB lost original-asset metadata")
    actual = mesh_statistics(root)
    if actual[:3] != expected[:3]:
        raise RuntimeError(f"GLB statistics drifted expected={expected[:3]} actual={actual[:3]}")
    for wanted, got in zip(expected[3], actual[3]):
        if abs(wanted - got) > 0.003:
            raise RuntimeError(f"GLB dimensions drifted expected={expected[3]} actual={actual[3]}")
    return actual


def render_preview() -> None:
    root = bpy.data.objects[ROOT_NAME]
    root.rotation_euler = (math.radians(5.0), 0.0, math.radians(-18.0))
    root.location = (0.0, 0.0, 0.02)
    stage_mat = material("PreviewStageOnly", (0.006, 0.012, 0.014, 1.0), 0.28, 0.38)
    rounded_box("PreviewStage", root, (0.68, 0.52, 0.025), (0.0, 0.0, -0.025), stage_mat, bevel=0.025)
    world = bpy.data.worlds.new("DemolitionDevicePreviewWorld")
    world.use_nodes = True
    bpy.context.scene.world = world
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.002, 0.005, 0.007, 1.0)
        background.inputs["Strength"].default_value = 0.035
    target = Vector((0.0, 0.0, 0.075))
    lights = (
        ("PreviewKey", (-0.48, -0.62, 0.62), 72.0, (0.63, 0.84, 1.0), 0.42),
        ("PreviewSignal", (0.52, -0.16, 0.36), 54.0, (1.0, 0.20, 0.035), 0.32),
        ("PreviewRim", (0.18, 0.52, 0.48), 64.0, (0.12, 0.82, 0.88), 0.34),
    )
    for name, location, energy, color, size in lights:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.44, -0.72, 0.43)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 58.0
    bpy.context.scene.camera = camera
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.context.scene.render.image_settings.color_mode = "RGBA"
    bpy.context.scene.render.filepath = str(PREVIEW_PNG)
    bpy.context.scene.render.film_transparent = False
    bpy.context.scene.view_settings.exposure = -0.4
    bpy.ops.render.render(write_still=True)
    if not PREVIEW_PNG.is_file() or PREVIEW_PNG.stat().st_size < 16384:
        raise RuntimeError("Blender did not render the demolition-device preview")


def build() -> None:
    configure_scene()
    mats = palette()
    root = empty(ROOT_NAME, None, 0.05)
    create_device(root, mats)
    bpy.context.view_layer.update()
    stats = validate_scene(root)
    save_source(root)
    export_glb(root)
    verified = verify_glb(stats)
    render_preview()
    digest = hashlib.sha256(OUTPUT_GLB.read_bytes()).hexdigest().upper()
    print(
        "DEMOLITION_DEVICE_ASSET "
        f"meshes={stats[0]} triangles={stats[1]} materials={stats[2]} "
        f"dimensions_m={stats[3][0]:.3f}x{stats[3][1]:.3f}x{stats[3][2]:.3f} "
        f"roundtrip_meshes={verified[0]} roundtrip_triangles={verified[1]} "
        f"blend_bytes={SOURCE_BLEND.stat().st_size} glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"preview_bytes={PREVIEW_PNG.stat().st_size} sha256={digest}"
    )
    print(
        "DEMOLITION_DEVICE_PASS valid=True authored_dcc=True embedded=True "
        f"nodes={','.join(REQUIRED_NODES)}"
    )


if __name__ == "__main__":
    build()
