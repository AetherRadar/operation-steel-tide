"""Author the first-person medical and armor-use prop set.

Run from the repository root with Blender 4.5 LTS:
    blender --background --factory-startup --python scripts/blender/build_field_use_props.py

The script is the repeatable DCC recipe for an original low-poly/PBR asset.
It saves the editable Blender source, exports one embedded GLB, creates a
studio preview, and round-trips the GLB to verify its runtime node contract.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
from pathlib import Path
from typing import Iterable

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "field_use"
ASSET_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_field_use"
SOURCE_BLEND = SOURCE_DIR / "field_use_props.blend"
OUTPUT_GLB = ASSET_DIR / "field_use_props.glb"
PREVIEW_PNG = ASSET_DIR / "field_use_props_preview.png"
ROOT_NAME = "SteelTideFieldUseProps"
REQUIRED_NODES = (
    ROOT_NAME,
    "TraumaKit",
    "TraumaKitLid",
    "TraumaGauzePack",
    "TraumaInjector",
    "ArmorPlate",
    "ArmorCarrier",
    "ArmorCarrierFlap",
    "TraumaPrimaryGrip",
    "TraumaLidGrip",
    "TraumaGauzeGrip",
    "InjectorPrimaryGrip",
    "ArmorPrimaryGrip",
    "ArmorSupportGrip",
)
MARKER_NAMES = REQUIRED_NODES[8:]


def configure_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1440
    scene.render.resolution_y = 900
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
        raise RuntimeError(f"Blender did not create Principled BSDF for {name}")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    coat = principled.inputs.get("Coat Weight")
    if coat is not None:
        coat.default_value = 0.16 if metallic > 0.3 else 0.055
    if emission is not None:
        emission_color = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        emission_power = principled.inputs.get("Emission Strength")
        if emission_color is not None:
            emission_color.default_value = emission
        if emission_power is not None:
            emission_power.default_value = emission_strength
    return result


def create_palette() -> dict[str, bpy.types.Material]:
    return {
        "fabric": material("FieldFabricOlive", (0.105, 0.135, 0.102, 1.0), 0.02, 0.78),
        "fabric_light": material("FieldFabricHighlight", (0.185, 0.225, 0.150, 1.0), 0.02, 0.72),
        "fabric_dark": material("FieldFabricShadow", (0.035, 0.048, 0.041, 1.0), 0.04, 0.83),
        "medic_signal": material("TraumaSignalTeal", (0.025, 0.48, 0.34, 1.0), 0.05, 0.52),
        "medic_ivory": material("MedicPatchIvory", (0.72, 0.72, 0.61, 1.0), 0.0, 0.72),
        "zipper": material("ZipperGunmetal", (0.11, 0.13, 0.14, 1.0), 0.72, 0.28),
        "rubber": material("TacticalRubber", (0.016, 0.020, 0.021, 1.0), 0.03, 0.82),
        "gauze": material("SterileGauze", (0.74, 0.76, 0.68, 1.0), 0.0, 0.88),
        "foil": material("SterileFoil", (0.46, 0.51, 0.50, 1.0), 0.55, 0.32),
        "glass": material("InjectorPolymer", (0.26, 0.36, 0.35, 0.62), 0.02, 0.20),
        "fluid": material("InjectorFluid", (0.03, 0.50, 0.36, 1.0), 0.05, 0.23, emission=(0.005, 0.12, 0.065, 1.0), emission_strength=0.32),
        "plate": material("ArmorComposite", (0.145, 0.175, 0.160, 1.0), 0.14, 0.58),
        "plate_face": material("ArmorStrikeFace", (0.235, 0.255, 0.220, 1.0), 0.08, 0.52),
        "plate_edge": material("ArmorEdgeSeal", (0.025, 0.032, 0.034, 1.0), 0.12, 0.70),
        "carrier": material("CarrierNylon", (0.065, 0.078, 0.064, 1.0), 0.02, 0.82),
        "webbing": material("CarrierWebbing", (0.105, 0.123, 0.092, 1.0), 0.03, 0.75),
        "stitch": material("ReinforcedStitch", (0.32, 0.31, 0.22, 1.0), 0.0, 0.86),
    }


def empty(name: str, parent: bpy.types.Object | None, size: float = 0.025) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = size
    return obj


def marker(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    obj = empty(name, parent, 0.018)
    obj.location = location
    obj.rotation_euler = rotation
    obj["steel_tide_marker"] = name
    obj["marker_space"] = "prop-local"
    return obj


def grip_marker(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    outward: tuple[float, float, float],
    finger_direction: tuple[float, float, float],
) -> bpy.types.Object:
    """Create a marker with a stable hand-alignment coordinate convention.

    Local +Z points away from the contacted prop surface and local +Y follows
    the intended finger direction.  Local +X completes a right-handed frame.
    """
    normal = Vector(outward).normalized()
    finger = Vector(finger_direction)
    finger -= normal * finger.dot(normal)
    if finger.length < 0.0001:
        raise RuntimeError(f"{name}: finger direction is parallel to the contact normal")
    finger.normalize()
    side = finger.cross(normal).normalized()
    obj = marker(name, parent, location)
    obj.rotation_euler = Matrix((side, finger, normal)).transposed().to_euler()
    obj["local_axis_z"] = "outward-contact-normal"
    obj["local_axis_y"] = "toward-fingers"
    return obj


def authored_mesh(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    materials: Iterable[bpy.types.Material],
    *,
    bevel: float = 0.0,
    bevel_segments: int = 2,
    smooth: bool = False,
    face_materials: list[int] | None = None,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj["asset_author"] = "Operation Steel Tide project"
    obj["asset_license"] = "MIT"
    for entry in materials:
        mesh.materials.append(entry)
    if face_materials is not None:
        if len(face_materials) != len(mesh.polygons):
            raise RuntimeError(f"{name}: material indices do not match polygons")
        for polygon, index in zip(mesh.polygons, face_materials):
            polygon.material_index = index
    if smooth:
        for polygon in mesh.polygons:
            polygon.use_smooth = True
    if bevel > 0.0:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        modifier = obj.modifiers.new("AuthoredEdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = bevel_segments
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    return obj


def rounded_outline(width: float, depth: float, radius: float, segments: int = 4) -> list[tuple[float, float]]:
    points: list[tuple[float, float]] = []
    centres = (
        (width * 0.5 - radius, depth * 0.5 - radius, 0.0),
        (-width * 0.5 + radius, depth * 0.5 - radius, math.pi * 0.5),
        (-width * 0.5 + radius, -depth * 0.5 + radius, math.pi),
        (width * 0.5 - radius, -depth * 0.5 + radius, math.pi * 1.5),
    )
    for cx, cy, start in centres:
        for segment in range(segments + 1):
            angle = start + (math.pi * 0.5) * segment / segments
            points.append((cx + radius * math.cos(angle), cy + radius * math.sin(angle)))
    return points


def extrude_xy(
    name: str,
    parent: bpy.types.Object,
    outline: list[tuple[float, float]],
    thickness: float,
    mat: bpy.types.Material,
    *,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.0015,
) -> bpy.types.Object:
    count = len(outline)
    half = thickness * 0.5
    vertices = [(x, y, -half) for x, y in outline]
    vertices.extend((x, y, half) for x, y in outline)
    faces: list[tuple[int, ...]] = [tuple(range(count - 1, -1, -1)), tuple(range(count, count * 2))]
    faces.extend(
        (index, (index + 1) % count, count + (index + 1) % count, count + index)
        for index in range(count)
    )
    obj = authored_mesh(name, parent, vertices, faces, [mat], bevel=bevel, bevel_segments=3)
    obj.location = location
    return obj


def extrude_xz(
    name: str,
    parent: bpy.types.Object,
    outline: list[tuple[float, float]],
    depth: float,
    mat: bpy.types.Material,
    *,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.0015,
) -> bpy.types.Object:
    count = len(outline)
    half = depth * 0.5
    vertices = [(x, -half, z) for x, z in outline]
    vertices.extend((x, half, z) for x, z in outline)
    faces: list[tuple[int, ...]] = [tuple(range(count)), tuple(range(count * 2 - 1, count - 1, -1))]
    faces.extend(
        (index, count + index, count + (index + 1) % count, (index + 1) % count)
        for index in range(count)
    )
    obj = authored_mesh(name, parent, vertices, faces, [mat], bevel=bevel, bevel_segments=3)
    obj.location = location
    return obj


def line_curve(
    name: str,
    parent: bpy.types.Object,
    points: list[tuple[float, float, float]],
    radius: float,
    mat: bpy.types.Material,
    *,
    cyclic: bool = False,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(f"{name}Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, value in zip(spline.points, points):
        point.co = (*value, 1.0)
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    curve.materials.append(mat)
    obj["asset_author"] = "Operation Steel Tide project"
    obj["asset_license"] = "MIT"
    return obj


def lathe_z(
    name: str,
    parent: bpy.types.Object,
    profile: list[tuple[float, float]],
    mat: bpy.types.Material,
    *,
    segments: int = 20,
    location: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    for z, radius in profile:
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append((math.cos(angle) * radius, math.sin(angle) * radius, z))
    faces: list[tuple[int, ...]] = []
    for ring in range(len(profile) - 1):
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            a = ring * segments + segment
            b = ring * segments + next_segment
            c = (ring + 1) * segments + next_segment
            d = (ring + 1) * segments + segment
            faces.append((a, b, c, d))
    faces.append(tuple(range(segments - 1, -1, -1)))
    last = (len(profile) - 1) * segments
    faces.append(tuple(last + index for index in range(segments)))
    obj = authored_mesh(name, parent, vertices, faces, [mat], bevel=0.00045, smooth=True)
    obj.location = location
    obj.rotation_euler = rotation
    return obj


def add_stitches(
    prefix: str,
    parent: bpy.types.Object,
    centre: tuple[float, float, float],
    width: float,
    count: int,
    mat: bpy.types.Material,
    *,
    vertical: bool = False,
) -> None:
    for index in range(count):
        offset = (index - (count - 1) * 0.5) * width / max(1, count - 1)
        if vertical:
            points = [
                (centre[0], centre[1], centre[2] + offset - 0.003),
                (centre[0], centre[1], centre[2] + offset + 0.003),
            ]
        else:
            points = [
                (centre[0] + offset - 0.003, centre[1], centre[2]),
                (centre[0] + offset + 0.003, centre[1], centre[2]),
            ]
        line_curve(f"{prefix}{index:02d}", parent, points, 0.00065, mat)


def create_medical_kit(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> dict[str, bpy.types.Object]:
    kit = empty("TraumaKit", root, 0.04)
    kit["runtime_role"] = "field-medkit"
    body_outline = rounded_outline(0.34, 0.205, 0.032, 5)
    extrude_xy("TraumaKitBody", kit, body_outline, 0.082, mats["fabric"], location=(0.0, 0.0, 0.0), bevel=0.0035)
    extrude_xy("TraumaKitLowerGuard", kit, rounded_outline(0.326, 0.192, 0.026, 4), 0.012, mats["fabric_dark"], location=(0.0, 0.0, -0.045), bevel=0.0015)
    extrude_xy("TraumaKitFrontPocket", kit, rounded_outline(0.245, 0.055, 0.014, 4), 0.050, mats["fabric_light"], location=(0.0, -0.102, -0.002), bevel=0.0025)
    extrude_xy("TraumaKitPocketTrim", kit, rounded_outline(0.218, 0.010, 0.004, 3), 0.010, mats["fabric_dark"], location=(0.0, -0.134, 0.017), bevel=0.0009)
    for side in (-1.0, 1.0):
        line_curve(
            f"TraumaKitCompressionCord{'L' if side < 0 else 'R'}",
            kit,
            [(0.125 * side, -0.096, -0.025), (0.145 * side, 0.0, -0.036), (0.126 * side, 0.095, -0.021)],
            0.0022,
            mats["fabric_dark"],
        )

    zipper_points = [
        (x, y, 0.043)
        for x, y in rounded_outline(0.326, 0.192, 0.026, 4)
    ]
    line_curve("TraumaKitZipperRail", kit, zipper_points, 0.0023, mats["zipper"], cyclic=True)
    for index in range(28):
        angle = math.tau * index / 28.0
        x = 0.147 * math.cos(angle)
        y = 0.082 * math.sin(angle)
        tooth = extrude_xy(
            f"TraumaKitZipperTooth{index:02d}",
            kit,
            rounded_outline(0.008, 0.004, 0.0012, 2),
            0.005,
            mats["zipper"],
            location=(x, y, 0.046),
            bevel=0.00045,
        )
        tooth.rotation_euler.z = angle + math.pi * 0.5

    lid = empty("TraumaKitLid", kit, 0.03)
    lid.location = (0.0, 0.101, 0.045)
    lid["hinge_axis"] = "local-x"
    lid["closed_rotation_degrees"] = 0.0
    lid["open_rotation_degrees"] = -104.0
    lid_outline = rounded_outline(0.338, 0.202, 0.032, 5)
    extrude_xy("TraumaKitLidShell", lid, lid_outline, 0.046, mats["fabric_light"], location=(0.0, -0.101, 0.023), bevel=0.003)
    extrude_xy("TraumaKitLidInset", lid, rounded_outline(0.302, 0.166, 0.022, 4), 0.012, mats["fabric"], location=(0.0, -0.101, 0.052), bevel=0.0017)
    patch = extrude_xy("TraumaKitMedicalPatch", lid, rounded_outline(0.092, 0.074, 0.009, 4), 0.005, mats["medic_ivory"], location=(0.0, -0.101, 0.061), bevel=0.0012)
    patch["readable_side"] = "lid-top"
    extrude_xy("TraumaKitCrossVertical", lid, rounded_outline(0.019, 0.056, 0.004, 3), 0.0035, mats["medic_signal"], location=(0.0, -0.101, 0.066), bevel=0.0008)
    extrude_xy("TraumaKitCrossHorizontal", lid, rounded_outline(0.056, 0.019, 0.004, 3), 0.0035, mats["medic_signal"], location=(0.0, -0.101, 0.066), bevel=0.0008)
    line_curve(
        "TraumaKitLidSeam",
        lid,
        [(x, y - 0.101, 0.055) for x, y in rounded_outline(0.314, 0.176, 0.023, 3)],
        0.0008,
        mats["stitch"],
        cyclic=True,
    )
    extrude_xy(
        "TraumaKitLidInnerPanel",
        lid,
        rounded_outline(0.292, 0.154, 0.020, 4),
        0.009,
        mats["fabric_dark"],
        location=(0.0, -0.101, -0.0045),
        bevel=0.0015,
    )
    for index, x in enumerate((-0.072, 0.072)):
        extrude_xy(
            f"TraumaKitLidElastic{index}",
            lid,
            rounded_outline(0.034, 0.126, 0.006, 3),
            0.006,
            mats["fabric_light"],
            location=(x, -0.101, -0.012),
            bevel=0.001,
        )
    extrude_xy(
        "TraumaKitLidQuickDressing",
        lid,
        rounded_outline(0.106, 0.056, 0.010, 3),
        0.017,
        mats["gauze"],
        location=(0.0, -0.111, -0.016),
        bevel=0.0015,
    )
    extrude_xy(
        "TraumaKitLidQuickDressingBand",
        lid,
        rounded_outline(0.025, 0.050, 0.004, 2),
        0.019,
        mats["medic_signal"],
        location=(0.0, -0.111, -0.017),
        bevel=0.0007,
    )

    # Interior divisions remain visible during the open/application phases.
    extrude_xy("TraumaKitInteriorTray", kit, rounded_outline(0.300, 0.160, 0.018, 4), 0.015, mats["fabric_dark"], location=(0.0, 0.0, 0.043), bevel=0.0014)
    extrude_xy("TraumaKitInteriorDressing", kit, rounded_outline(0.112, 0.062, 0.014, 4), 0.025, mats["gauze"], location=(-0.075, -0.025, 0.061), bevel=0.002)
    extrude_xy("TraumaKitInteriorPouch", kit, rounded_outline(0.098, 0.070, 0.013, 4), 0.026, mats["medic_signal"], location=(0.074, 0.022, 0.061), bevel=0.002)
    for row in (-0.026, 0.022):
        add_stitches("TraumaKitPocketStitch", kit, (0.0, -0.1335, row), 0.18, 13, mats["stitch"])

    grip_marker("TraumaPrimaryGrip", kit, (0.137, -0.040, -0.015), (1.0, 0.0, 0.0), (0.0, 0.0, 1.0))
    grip_marker("TraumaLidGrip", lid, (-0.105, -0.176, 0.070), (0.0, 0.0, 1.0), (1.0, 0.0, 0.0))
    return {"kit": kit, "lid": lid}


def create_gauze(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> bpy.types.Object:
    gauze = empty("TraumaGauzePack", root, 0.025)
    gauze["runtime_role"] = "wound-dressing"
    wrapper = extrude_xy("GauzeFoilWrapper", gauze, rounded_outline(0.112, 0.065, 0.011, 4), 0.024, mats["foil"], bevel=0.0017)
    wrapper.rotation_euler.z = math.radians(-3.0)
    extrude_xy("GauzePaperFace", gauze, rounded_outline(0.094, 0.050, 0.007, 3), 0.005, mats["gauze"], location=(0.0, 0.0, 0.0155), bevel=0.0008)
    extrude_xy("GauzeIdentifierBand", gauze, rounded_outline(0.025, 0.052, 0.004, 3), 0.006, mats["medic_signal"], location=(0.025, 0.0, 0.018), bevel=0.0007)
    for index, x in enumerate((-0.041, -0.032, -0.023)):
        line_curve(f"GauzeSealRib{index}", gauze, [(x, -0.027, 0.0185), (x, 0.027, 0.0185)], 0.0007, mats["fabric_dark"])
    grip_marker("TraumaGauzeGrip", gauze, (-0.038, -0.018, -0.004), (0.0, 0.0, 1.0), (1.0, 0.0, 0.0))
    return gauze


def create_injector(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> bpy.types.Object:
    injector = empty("TraumaInjector", root, 0.025)
    injector["runtime_role"] = "adrenaline-injector"
    # The lathe axis is local Z; rotate the authored assembly so the injector points +X.
    barrel = lathe_z(
        "InjectorBarrel",
        injector,
        [(-0.055, 0.009), (-0.048, 0.012), (0.036, 0.012), (0.047, 0.009)],
        mats["glass"],
        segments=24,
        rotation=(0.0, math.pi * 0.5, 0.0),
    )
    barrel["transparent_part"] = True
    lathe_z("InjectorFluidCore", injector, [(-0.041, 0.006), (0.025, 0.006)], mats["fluid"], segments=18, rotation=(0.0, math.pi * 0.5, 0.0))
    lathe_z("InjectorPlunger", injector, [(-0.075, 0.005), (-0.052, 0.005)], mats["rubber"], segments=18, rotation=(0.0, math.pi * 0.5, 0.0))
    lathe_z("InjectorThumbPad", injector, [(-0.081, 0.017), (-0.075, 0.017)], mats["medic_signal"], segments=24, rotation=(0.0, math.pi * 0.5, 0.0))
    lathe_z("InjectorSafetyCap", injector, [(0.045, 0.010), (0.075, 0.007), (0.082, 0.004)], mats["medic_ivory"], segments=20, rotation=(0.0, math.pi * 0.5, 0.0))
    for index, x in enumerate((-0.025, -0.005, 0.015, 0.035)):
        line_curve(f"InjectorDoseMark{index}", injector, [(x, -0.0122, -0.004), (x, -0.0122, 0.004)], 0.0006, mats["medic_ivory"])
    grip_marker("InjectorPrimaryGrip", injector, (-0.020, 0.0, 0.0), (0.0, -1.0, 0.0), (1.0, 0.0, 0.0))
    return injector


def curved_plate_mesh(
    name: str,
    parent: bpy.types.Object,
    outline: list[tuple[float, float]],
    depth: float,
    mat: bpy.types.Material,
    *,
    curve: float,
    bevel: float,
    scale: float = 1.0,
    y_offset: float = 0.0,
) -> bpy.types.Object:
    count = len(outline)
    vertices: list[tuple[float, float, float]] = []
    max_x = max(abs(x) for x, _ in outline)
    for side in (-1.0, 1.0):
        for x, z in outline:
            sx = x * scale
            sz = z * scale
            bow = curve * (sx / max(max_x * scale, 0.001)) ** 2
            vertices.append((sx, y_offset + bow + side * depth * 0.5, sz))
    faces: list[tuple[int, ...]] = [tuple(range(count - 1, -1, -1)), tuple(range(count, count * 2))]
    faces.extend((index, (index + 1) % count, count + (index + 1) % count, count + index) for index in range(count))
    return authored_mesh(name, parent, vertices, faces, [mat], bevel=bevel, bevel_segments=3)


def create_armor_plate(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> bpy.types.Object:
    plate = empty("ArmorPlate", root, 0.04)
    plate["runtime_role"] = "replacement-armor-plate"
    plate["dimensions_m"] = "0.260x0.330x0.032"
    outline = [
        (-0.105, -0.165),
        (0.105, -0.165),
        (0.126, 0.092),
        (0.086, 0.165),
        (-0.086, 0.165),
        (-0.126, 0.092),
    ]
    curved_plate_mesh("ArmorPlateEdgeSeal", plate, outline, 0.034, mats["plate_edge"], curve=0.023, bevel=0.004)
    curved_plate_mesh("ArmorPlateCompositeCore", plate, outline, 0.027, mats["plate"], curve=0.022, bevel=0.003, scale=0.962, y_offset=-0.001)
    curved_plate_mesh("ArmorPlateStrikeFace", plate, outline, 0.006, mats["plate_face"], curve=0.020, bevel=0.0014, scale=0.875, y_offset=-0.018)
    chevron = [(-0.064, 0.014), (0.0, -0.022), (0.064, 0.014), (0.064, 0.030), (0.0, -0.006), (-0.064, 0.030)]
    extrude_xz("ArmorPlateOrientationChevron", plate, chevron, 0.003, mats["medic_ivory"], location=(0.0, -0.044, 0.052), bevel=0.0006)
    for index, z in enumerate((-0.086, -0.066, 0.105)):
        line_curve(f"ArmorPlateInspectionLine{index}", plate, [(-0.055, -0.0445, z), (0.055, -0.0445, z)], 0.0011, mats["fabric_dark"])
    for side in (-1.0, 1.0):
        extrude_xz(
            f"ArmorPlateCornerPad{'L' if side < 0 else 'R'}",
            plate,
            [(-0.018, -0.030), (0.018, -0.030), (0.014, 0.030), (-0.014, 0.030)],
            0.005,
            mats["rubber"],
            location=(0.096 * side, -0.041, -0.112),
            bevel=0.0014,
        )
    grip_marker("ArmorPrimaryGrip", plate, (0.105, -0.020, -0.055), (0.0, -1.0, 0.0), (0.0, 0.0, 1.0))
    grip_marker("ArmorSupportGrip", plate, (-0.100, -0.018, 0.018), (0.0, -1.0, 0.0), (0.0, 0.0, -1.0))
    return plate


def create_armor_carrier(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> dict[str, bpy.types.Object]:
    carrier = empty("ArmorCarrier", root, 0.05)
    carrier["runtime_role"] = "plate-carrier-insertion-target"
    body_outline = [
        (-0.190, -0.185),
        (0.190, -0.185),
        (0.178, 0.105),
        (0.112, 0.188),
        (0.052, 0.158),
        (-0.052, 0.158),
        (-0.112, 0.188),
        (-0.178, 0.105),
    ]
    extrude_xz("ArmorCarrierPaddedBody", carrier, body_outline, 0.052, mats["carrier"], location=(0.0, 0.0, 0.0), bevel=0.006)
    pocket_outline = [(-0.154, -0.146), (0.154, -0.146), (0.146, 0.108), (-0.146, 0.108)]
    extrude_xz("ArmorCarrierPlatePocket", carrier, pocket_outline, 0.032, mats["fabric_dark"], location=(0.0, -0.040, -0.010), bevel=0.004)
    # Shoulder straps use custom tapered silhouettes instead of block primitives.
    for side in (-1.0, 1.0):
        strap_outline = [(-0.030, -0.10), (0.030, -0.10), (0.040, 0.10), (-0.026, 0.10)]
        strap = extrude_xz(
            f"ArmorCarrierShoulder{'L' if side < 0 else 'R'}",
            carrier,
            strap_outline,
            0.024,
            mats["webbing"],
            location=(0.115 * side, 0.012, 0.197),
            bevel=0.003,
        )
        strap.rotation_euler.y = math.radians(-10.0 * side)

    for row, z in enumerate((-0.098, -0.045, 0.008, 0.061)):
        extrude_xz(
            f"ArmorCarrierMolleRow{row}",
            carrier,
            [(-0.148, -0.010), (0.148, -0.010), (0.148, 0.010), (-0.148, 0.010)],
            0.010,
            mats["webbing"],
            location=(0.0, -0.064, z),
            bevel=0.0012,
        )
        for column in range(7):
            x = -0.126 + column * 0.042
            line_curve(f"ArmorMolleStitch{row}_{column}", carrier, [(x, -0.070, z - 0.008), (x, -0.070, z + 0.008)], 0.00075, mats["stitch"])

    line_curve(
        "ArmorCarrierPocketSeam",
        carrier,
        [(-0.145, -0.070, -0.135), (-0.145, -0.070, 0.097), (0.145, -0.070, 0.097), (0.145, -0.070, -0.135)],
        0.0010,
        mats["stitch"],
    )
    flap = empty("ArmorCarrierFlap", carrier, 0.025)
    # The origin sits on the flap's upper seam so runtime rotation reads as a
    # physical hook-and-loop closure being peeled down from the carrier.
    flap.location = (0.0, -0.075, -0.073)
    flap["hinge_axis"] = "local-x"
    flap["closed_rotation_degrees"] = 0.0
    flap["open_rotation_degrees"] = 78.0
    extrude_xz(
        "ArmorCarrierFlapPanel",
        flap,
        [(-0.133, -0.082), (0.133, -0.082), (0.146, 0.0), (-0.146, 0.0)],
        0.024,
        mats["webbing"],
        location=(0.0, 0.0, 0.0),
        bevel=0.003,
    )
    extrude_xz(
        "ArmorCarrierHookLoop",
        flap,
        [(-0.066, -0.014), (0.066, -0.014), (0.072, 0.014), (-0.072, 0.014)],
        0.006,
        mats["rubber"],
        location=(0.0, -0.016, -0.042),
        bevel=0.001,
    )
    return {"carrier": carrier, "flap": flap}


def convert_curves(root: bpy.types.Object) -> None:
    for obj in [child for child in root.children_recursive if child.type == "CURVE"]:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
        obj["asset_author"] = "Operation Steel Tide project"
        obj["asset_license"] = "MIT"


def hierarchy(root: bpy.types.Object) -> list[bpy.types.Object]:
    return [root, *root.children_recursive]


def mesh_statistics(root: bpy.types.Object) -> tuple[int, int, int, tuple[float, float, float]]:
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    triangles = 0
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, point.x)
            minimum.y = min(minimum.y, point.y)
            minimum.z = min(minimum.z, point.z)
            maximum.x = max(maximum.x, point.x)
            maximum.y = max(maximum.y, point.y)
            maximum.z = max(maximum.z, point.z)
    materials = {
        slot.material.name
        for obj in meshes
        for slot in obj.material_slots
        if slot.material is not None
    }
    dimensions = maximum - minimum
    return len(meshes), triangles, len(materials), tuple(dimensions)


def validate_scene(root: bpy.types.Object) -> tuple[int, int, int, tuple[float, float, float]]:
    names = [obj.name for obj in hierarchy(root)]
    missing = [name for name in REQUIRED_NODES if names.count(name) != 1]
    if missing:
        raise RuntimeError(f"Field-use node contract is incomplete or duplicated: {missing}")
    unlicensed = [
        obj.name
        for obj in root.children_recursive
        if obj.type == "MESH" and (obj.get("asset_author") != "Operation Steel Tide project" or obj.get("asset_license") != "MIT")
    ]
    if unlicensed:
        raise RuntimeError(f"Visible meshes lack original-asset metadata: {unlicensed[:8]}")
    for child_name in ("TraumaKit", "TraumaGauzePack", "TraumaInjector", "ArmorPlate", "ArmorCarrier"):
        child = bpy.data.objects.get(child_name)
        if child is None or child.parent != root or child.location.length > 0.00001:
            raise RuntimeError(f"{child_name} must be a direct, origin-centred runtime prop")
    stats = mesh_statistics(root)
    meshes, triangles, materials, dimensions = stats
    if meshes < 80:
        raise RuntimeError(f"Field-use set has only {meshes} mesh pieces; authored detail is missing")
    if triangles < 4500 or triangles > 28000:
        raise RuntimeError(f"Field-use triangle budget violation: {triangles}")
    if materials < 12 or materials > 20:
        raise RuntimeError(f"Unexpected material count: {materials}")
    if not (0.36 <= dimensions[0] <= 0.44 and 0.21 <= dimensions[1] <= 0.30 and 0.45 <= dimensions[2] <= 0.51):
        raise RuntimeError(f"Unexpected authored bounds: {dimensions}")
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
    root["asset_kind"] = "first-person-field-use-props"
    root["units"] = "metres"
    root["forward_axis"] = "Blender +Y / Godot -Z"
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND), compress=True, check_existing=False)
    if not SOURCE_BLEND.is_file() or SOURCE_BLEND.stat().st_size < 8192:
        raise RuntimeError("Blender did not save the authoritative field-use source")


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
        raise RuntimeError("Generated asset is not a binary glTF")
    _, version, length = struct.unpack_from("<III", payload, 0)
    chunk_length, chunk_type = struct.unpack_from("<II", payload, 12)
    if version != 2 or length != len(payload) or chunk_type != 0x4E4F534A:
        raise RuntimeError("Generated GLB header is invalid")
    return json.loads(payload[20 : 20 + chunk_length].decode("utf-8"))


def verify_glb(expected: tuple[int, int, int, tuple[float, float, float]]) -> tuple[int, int, int, tuple[float, float, float]]:
    document = glb_document(OUTPUT_GLB)
    if any("uri" in entry for entry in document.get("buffers", [])) or any("uri" in entry for entry in document.get("images", [])):
        raise RuntimeError("Field-use GLB depends on external buffers or images")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB), import_pack_images=True)
    if result != {"FINISHED"}:
        raise RuntimeError("Blender could not round-trip the field-use GLB")
    root = bpy.data.objects.get(ROOT_NAME)
    if root is None:
        raise RuntimeError("Round-tripped GLB lost its root")
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
    # Re-import the verified runtime asset, then make a non-exported exploded studio arrangement.
    root = bpy.data.objects[ROOT_NAME]
    trauma = bpy.data.objects["TraumaKit"]
    lid = bpy.data.objects["TraumaKitLid"]
    gauze = bpy.data.objects["TraumaGauzePack"]
    injector = bpy.data.objects["TraumaInjector"]
    plate = bpy.data.objects["ArmorPlate"]
    carrier = bpy.data.objects["ArmorCarrier"]
    flap = bpy.data.objects["ArmorCarrierFlap"]
    for obj in (trauma, lid, gauze, injector, plate, carrier, flap):
        obj.rotation_mode = "XYZ"
    trauma.location = (-0.36, 0.04, 0.11)
    trauma.rotation_euler.z = math.radians(-7.0)
    lid.rotation_euler.x = math.radians(-76.0)
    gauze.location = (-0.46, -0.20, 0.27)
    gauze.rotation_euler = (math.radians(18.0), math.radians(-8.0), math.radians(14.0))
    injector.location = (-0.22, -0.17, 0.31)
    injector.rotation_euler = (math.radians(-12.0), math.radians(10.0), math.radians(-18.0))
    carrier.location = (0.40, 0.08, 0.22)
    carrier.rotation_euler = (0.0, math.radians(-3.0), math.radians(7.0))
    flap.rotation_euler.x = math.radians(58.0)
    plate.location = (0.37, -0.17, 0.21)
    plate.rotation_euler = (0.0, math.radians(-4.0), math.radians(-10.0))
    bpy.context.view_layer.update()

    floor_mat = material("PreviewStageOnly", (0.012, 0.018, 0.020, 1.0), 0.16, 0.52)
    floor = extrude_xy("PreviewStageFloor", root, rounded_outline(1.55, 0.95, 0.08, 5), 0.035, floor_mat, location=(0.0, 0.02, -0.045), bevel=0.01)
    floor["preview_only"] = True
    world = bpy.data.worlds.new("FieldUsePreviewWorld")
    world.use_nodes = True
    bpy.context.scene.world = world
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.005, 0.009, 0.012, 1.0)
        background.inputs["Strength"].default_value = 0.045
    target = Vector((0.0, 0.0, 0.18))
    lights = (
        ("PreviewKey", (-0.78, -0.72, 1.05), 68.0, (0.72, 0.88, 1.0), 0.85),
        ("PreviewFill", (0.85, -0.20, 0.58), 38.0, (0.12, 0.76, 0.60), 0.70),
        ("PreviewRim", (0.28, 0.72, 0.86), 76.0, (1.0, 0.34, 0.15), 0.55),
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
        light["preview_only"] = True
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.78, -1.82, 1.04)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 55.0
    camera["preview_only"] = True
    bpy.context.scene.camera = camera
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.context.scene.render.image_settings.color_mode = "RGBA"
    bpy.context.scene.render.image_settings.color_depth = "8"
    bpy.context.scene.render.filepath = str(PREVIEW_PNG)
    bpy.context.scene.render.film_transparent = False
    bpy.context.scene.view_settings.exposure = -0.55
    bpy.ops.render.render(write_still=True)
    if not PREVIEW_PNG.is_file() or PREVIEW_PNG.stat().st_size < 16384:
        raise RuntimeError("Blender did not create the field-use preview")


def build() -> None:
    configure_scene()
    mats = create_palette()
    root = empty(ROOT_NAME, None, 0.06)
    root["asset_author"] = "Operation Steel Tide project"
    root["asset_license"] = "MIT"
    root["asset_kind"] = "first-person-field-use-props"
    root["units"] = "metres"
    root["forward_axis"] = "Blender +Y / Godot -Z"
    create_medical_kit(root, mats)
    create_gauze(root, mats)
    create_injector(root, mats)
    create_armor_plate(root, mats)
    create_armor_carrier(root, mats)
    convert_curves(root)
    bpy.context.view_layer.update()
    stats = validate_scene(root)
    save_source(root)
    export_glb(root)
    verified = verify_glb(stats)
    render_preview()
    digest = hashlib.sha256(OUTPUT_GLB.read_bytes()).hexdigest().upper()
    print(
        "FIELD_USE_ASSET "
        f"meshes={stats[0]} triangles={stats[1]} materials={stats[2]} "
        f"dimensions_m={stats[3][0]:.3f}x{stats[3][1]:.3f}x{stats[3][2]:.3f} "
        f"roundtrip_meshes={verified[0]} roundtrip_triangles={verified[1]} "
        f"blend_bytes={SOURCE_BLEND.stat().st_size} glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"preview_bytes={PREVIEW_PNG.stat().st_size} sha256={digest}"
    )
    print(
        "FIELD_USE_PASS valid=True authored_dcc=True embedded=True "
        f"nodes={','.join(REQUIRED_NODES)}"
    )


if __name__ == "__main__":
    build()
