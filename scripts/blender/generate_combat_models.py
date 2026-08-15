"""Generate the project-authored combat weapon and operator GLBs.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup --python scripts/blender/generate_combat_models.py

Godot owns gameplay, collision, audio, and animation state. The exported node
names are the runtime contract used by CombatModelLibrary.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
WEAPON_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_m4a1"
OPERATOR_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_operator"
SOURCE_DIR = REPO_ROOT / "source_art" / "combat_models"
PREVIEW_DIR = REPO_ROOT / "build" / "art-previews"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float = 0.0,
    roughness: float = 0.7,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.metallic = metallic
    mat.roughness = roughness
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness
        if emission is not None:
            emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
            strength_input = principled.inputs.get("Emission Strength")
            if emission_input is not None:
                emission_input.default_value = emission
            if strength_input is not None:
                strength_input.default_value = emission_strength
    return mat


def empty(name: str, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.08
    obj.parent = parent
    return obj


def finish_mesh(
    obj: bpy.types.Object,
    parent: bpy.types.Object,
    mat: bpy.types.Material,
    bevel: float = 0.0,
    smooth: bool = False,
) -> bpy.types.Object:
    obj.parent = parent
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("EdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    obj.select_set(False)
    return obj


def cube(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.008,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    return finish_mesh(obj, parent, mat, bevel)


def cylinder(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 16,
    bevel: float = 0.004,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, parent, mat, bevel, smooth=True)


def sphere(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    mat: bpy.types.Material,
    segments: int = 20,
    rings: int = 12,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_mesh(obj, parent, mat, smooth=True)


def tapered_box(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    lower: tuple[float, float],
    upper: tuple[float, float],
    height: float,
    mat: bpy.types.Material,
    bevel: float = 0.008,
) -> bpy.types.Object:
    lx, ly = lower
    ux, uy = upper
    z0 = -height * 0.5
    z1 = height * 0.5
    vertices = [
        (-lx, -ly, z0), (lx, -ly, z0), (lx, ly, z0), (-lx, ly, z0),
        (-ux, -uy, z1), (ux, -uy, z1), (ux, uy, z1), (-ux, uy, z1),
    ]
    faces = [
        (0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
        (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),
    ]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return finish_mesh(obj, parent, mat, bevel)


def cylinder_between(
    name: str,
    parent: bpy.types.Object,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    radius: float,
    mat: bpy.types.Material,
    vertices: int = 14,
) -> bpy.types.Object:
    a = Vector(start)
    b = Vector(end)
    direction = b - a
    midpoint = (a + b) * 0.5
    obj = cylinder(name, parent, tuple(midpoint), radius, direction.length, mat, vertices=vertices)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def join_direct_meshes(parent: bpy.types.Object, name: str) -> None:
    meshes = [child for child in parent.children if child.type == "MESH"]
    if not meshes:
        return
    if len(meshes) == 1:
        meshes[0].name = name
        return
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    meshes[0].name = name


def add_magazine_geometry(parent: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> None:
    cube("MagazineUpper", parent, (0.0, 0.0, -0.065), (0.092, 0.145, 0.135), mats["polymer"], bevel=0.012)
    cube("MagazineLower", parent, (0.0, 0.022, -0.185), (0.086, 0.135, 0.13), mats["polymer"], rotation=(math.radians(-8), 0, 0), bevel=0.011)
    cube("MagazineFloor", parent, (0.0, 0.038, -0.263), (0.103, 0.15, 0.025), mats["steel"], rotation=(math.radians(-8), 0, 0), bevel=0.004)
    for side in (-1.0, 1.0):
        for index in range(3):
            cube(
                f"MagazineRib_{side}_{index}", parent,
                (side * 0.047, -0.035 + index * 0.044, -0.14),
                (0.006, 0.017, 0.19), mats["steel"], bevel=0.001,
            )
    join_direct_meshes(parent, "MagazineGeometry")


def build_weapon() -> bpy.types.Object:
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    mats = {
        "phosphate": make_material("PhosphateBlack", (0.025, 0.031, 0.03, 1.0), 0.78, 0.27),
        "steel": make_material("MachinedSteel", (0.13, 0.15, 0.145, 1.0), 0.9, 0.2),
        "polymer": make_material("ReinforcedPolymer", (0.035, 0.043, 0.04, 1.0), 0.08, 0.66),
        "tan": make_material("FieldTan", (0.29, 0.255, 0.17, 1.0), 0.06, 0.72),
        "mark": make_material("SafetyMark", (0.82, 0.25, 0.055, 1.0), 0.15, 0.35),
    }
    root = empty("SteelTideM4A1")

    tapered_box("UpperReceiver", root, (0, 0.0, 0.035), (0.07, 0.23), (0.064, 0.225), 0.115, mats["phosphate"], 0.009)
    tapered_box("LowerReceiver", root, (0, -0.015, -0.045), (0.062, 0.18), (0.067, 0.2), 0.105, mats["phosphate"], 0.008)
    cube("TopRail", root, (0, 0.08, 0.112), (0.13, 0.62, 0.035), mats["steel"], bevel=0.003)
    for index in range(13):
        cube(f"RailTooth_{index}", root, (0, -0.19 + index * 0.044, 0.14), (0.145, 0.021, 0.024), mats["phosphate"], bevel=0.002)
    cube("HandguardCore", root, (0, 0.47, 0.015), (0.155, 0.47, 0.13), mats["tan"], bevel=0.016)
    for side in (-1.0, 1.0):
        for index in range(4):
            cube(f"Mlok_{side}_{index}", root, (side * 0.079, 0.31 + index * 0.095, 0.015), (0.008, 0.058, 0.024), mats["phosphate"], bevel=0.004)
    cylinder("Barrel", root, (0, 0.91, 0.015), 0.023, 0.61, mats["steel"], (math.pi / 2, 0, 0), 20)
    cube("GasBlock", root, (0, 0.755, 0.035), (0.075, 0.08, 0.09), mats["phosphate"], bevel=0.008)
    cylinder("BufferTube", root, (0, -0.42, 0.025), 0.038, 0.46, mats["steel"], (math.pi / 2, 0, 0), 18)
    cube("StockCheek", root, (0, -0.49, 0.055), (0.14, 0.39, 0.105), mats["polymer"], bevel=0.018)
    cube("StockButt", root, (0, -0.69, -0.005), (0.16, 0.075, 0.25), mats["polymer"], rotation=(math.radians(-7), 0, 0), bevel=0.015)
    tapered_box("PistolGrip", root, (0, 0.0, -0.205), (0.052, 0.07), (0.043, 0.058), 0.29, mats["polymer"], 0.012)
    cube("TriggerGuard", root, (0, 0.105, -0.13), (0.025, 0.19, 0.035), mats["steel"], bevel=0.006)
    cube("EjectionPort", root, (0.071, 0.035, 0.048), (0.011, 0.17, 0.058), mats["steel"], bevel=0.003)
    cylinder("ForwardAssist", root, (0.082, -0.115, 0.025), 0.022, 0.035, mats["steel"], (0, math.pi / 2, 0), 12)
    cylinder("Selector", root, (0.074, -0.04, -0.055), 0.019, 0.025, mats["mark"], (0, math.pi / 2, 0), 12)

    magazine = empty("Magazine", root)
    magazine.location = (0.0, 0.31, -0.2)
    add_magazine_geometry(magazine, mats)
    spare = empty("SpareMagazine", root)
    spare.location = (-0.3, 0.18, -0.62)
    add_magazine_geometry(spare, mats)

    charging = empty("ChargingHandle", root)
    charging.location = (0.075, 0.05, 0.085)
    cube("ChargingStem", charging, (0, 0, 0), (0.028, 0.11, 0.028), mats["steel"], bevel=0.004)
    cube("ChargingLatch", charging, (-0.035, -0.035, 0), (0.082, 0.034, 0.035), mats["steel"], bevel=0.005)

    stock = empty("Stock", root)
    stock.location = (0, -0.49, 0.0)
    cube("StockAdjustmentLever", stock, (0, 0.02, -0.11), (0.065, 0.14, 0.035), mats["steel"], bevel=0.005)
    foregrip = empty("Foregrip", root)
    foregrip.location = (0, 0.58, -0.17)
    tapered_box("AngledForegrip", foregrip, (0, 0, 0), (0.045, 0.075), (0.038, 0.065), 0.19, mats["polymer"], 0.01)
    optic = empty("OpticMount", root)
    optic.location = (0, 0.25, 0.145)
    cube("OpticRiser", optic, (0, 0, 0), (0.11, 0.16, 0.035), mats["phosphate"], bevel=0.005)

    muzzle = empty("MuzzleDevice", root)
    muzzle.location = (0, 1.205, 0.015)
    cylinder("ThreePortBrake", muzzle, (0, 0, 0), 0.043, 0.15, mats["phosphate"], (math.pi / 2, 0, 0), 20)
    for index in (-1, 0, 1):
        cube(f"BrakePort_{index}", muzzle, (0.035, index * 0.038, 0.012), (0.018, 0.022, 0.025), mats["steel"], bevel=0.002)
    join_direct_meshes(muzzle, "MuzzleDeviceGeometry")
    suppressor = empty("Suppressor", root)
    suppressor.location = (0, 1.26, 0.015)
    cylinder("SuppressorBody", suppressor, (0, 0, 0), 0.055, 0.28, mats["phosphate"], (math.pi / 2, 0, 0), 24)
    for index in range(4):
        cylinder("SuppressorRing_%d" % index, suppressor, (0, -0.095 + index * 0.062, 0), 0.058, 0.018, mats["steel"], (math.pi / 2, 0, 0), 24, 0.002)
    join_direct_meshes(suppressor, "SuppressorGeometry")

    join_direct_meshes(root, "WeaponBodyGeometry")
    return root


def add_operator_leg(
    name: str,
    root: bpy.types.Object,
    x: float,
    mats: dict[str, bpy.types.Material],
) -> bpy.types.Object:
    leg = empty(name, root)
    leg.location = (x, 0.0, 0.84)
    cylinder_between("CombatPantsUpper", leg, (0, 0, 0), (0, 0.012, -0.39), 0.125, mats["uniform"], 16)
    sphere("KneeJoint", leg, (0, 0.04, -0.4), (0.135, 0.12, 0.135), mats["uniform"], 16, 10)
    cube("KneePad", leg, (0, 0.145, -0.4), (0.19, 0.075, 0.18), mats["armor"], bevel=0.025)
    cylinder_between("CombatPantsLower", leg, (0, 0.02, -0.43), (0, 0.025, -0.74), 0.105, mats["uniform"], 16)
    cube("TacticalBoot", leg, (0, 0.09, -0.81), (0.215, 0.36, 0.17), mats["rubber"], bevel=0.035)
    cube("BootSole", leg, (0, 0.12, -0.9), (0.225, 0.38, 0.055), mats["steel"], bevel=0.015)
    join_direct_meshes(leg, f"{name}Geometry")
    return leg


def build_operator() -> bpy.types.Object:
    clear_scene()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    mats = {
        "uniform": make_material("RipstopUniform", (0.105, 0.15, 0.125, 1.0), 0.02, 0.92),
        "uniform_dark": make_material("ReinforcedUniform", (0.055, 0.075, 0.067, 1.0), 0.04, 0.88),
        "armor": make_material("CeramicArmor", (0.03, 0.043, 0.042, 1.0), 0.34, 0.53),
        "armor_edge": make_material("ArmorEdge", (0.012, 0.019, 0.019, 1.0), 0.62, 0.34),
        "webbing": make_material("FieldWebbing", (0.23, 0.205, 0.125, 1.0), 0.02, 0.96),
        "skin": make_material("OperatorSkin", (0.34, 0.225, 0.155, 1.0), 0.0, 0.9),
        "rubber": make_material("TacticalRubber", (0.018, 0.024, 0.023, 1.0), 0.05, 0.82),
        "steel": make_material("EquipmentSteel", (0.1, 0.12, 0.115, 1.0), 0.76, 0.27),
        "lens": make_material("GoggleLens", (0.025, 0.23, 0.24, 1.0), 0.48, 0.08),
        "patch": make_material(
            "TeamPatchMaterial", (0.08, 0.72, 0.52, 1.0), 0.16, 0.3,
            (0.025, 0.38, 0.23, 1.0), 1.6,
        ),
    }
    root = empty("SteelTideOperator")

    tapered_box("Pelvis", root, (0, -0.015, 0.88), (0.25, 0.14), (0.21, 0.13), 0.28, mats["uniform_dark"], 0.035)
    tapered_box("Torso", root, (0, 0, 1.25), (0.235, 0.15), (0.31, 0.17), 0.62, mats["uniform"], 0.05)
    cube("ShoulderYoke", root, (0, -0.015, 1.5), (0.72, 0.3, 0.16), mats["uniform_dark"], bevel=0.06)
    cylinder("Neck", root, (0, 0, 1.62), 0.105, 0.18, mats["skin"], vertices=18, bevel=0.01)
    sphere("Head", root, (0, 0.015, 1.79), (0.155, 0.145, 0.205), mats["skin"], 22, 14)
    cube("Balaclava", root, (0, 0.13, 1.76), (0.255, 0.075, 0.25), mats["rubber"], bevel=0.035)
    cube("GoggleBridge", root, (0, 0.178, 1.84), (0.31, 0.065, 0.095), mats["armor_edge"], bevel=0.025)
    for side in (-1.0, 1.0):
        cube(f"GoggleLens_{side}", root, (side * 0.078, 0.215, 1.84), (0.13, 0.018, 0.062), mats["lens"], bevel=0.018)

    left_leg = add_operator_leg("LeftLegRig", root, -0.17, mats)
    right_leg = add_operator_leg("RightLegRig", root, 0.17, mats)

    arm_points = {
        "Left": ((-0.31, 0, 1.47), (-0.43, 0.19, 1.24), (-0.17, 0.48, 1.16)),
        "Right": ((0.31, 0, 1.47), (0.43, 0.12, 1.25), (0.16, 0.31, 1.18)),
    }
    for side_name, (shoulder, elbow, hand) in arm_points.items():
        cylinder_between(f"{side_name}UpperArm", root, shoulder, elbow, 0.105, mats["uniform"], 16)
        sphere(f"{side_name}ElbowPad", root, elbow, (0.125, 0.115, 0.125), mats["armor"], 16, 10)
        cylinder_between(f"{side_name}Forearm", root, elbow, hand, 0.09, mats["uniform_dark"], 16)
        sphere(f"{side_name}Glove", root, hand, (0.105, 0.12, 0.09), mats["rubber"], 16, 10)
        cube(f"{side_name}ShoulderArmor", root, shoulder, (0.24, 0.2, 0.22), mats["armor"], bevel=0.045)

    vest = empty("Vest", root)
    cube("FrontPlate", vest, (0, 0.185, 1.29), (0.5, 0.12, 0.55), mats["armor"], bevel=0.045)
    cube("RearPlate", vest, (0, -0.18, 1.3), (0.46, 0.1, 0.52), mats["armor"], bevel=0.04)
    for side in (-1.0, 1.0):
        cube(f"ShoulderStrap_{side}", vest, (side * 0.205, 0.01, 1.53), (0.1, 0.35, 0.09), mats["webbing"], bevel=0.025)
        cube(f"SidePlate_{side}", vest, (side * 0.292, 0.02, 1.22), (0.105, 0.28, 0.3), mats["armor_edge"], bevel=0.025)
    for index in range(3):
        cube(f"MagazinePouch_{index}", vest, ((index - 1) * 0.145, 0.28, 1.13), (0.125, 0.11, 0.24), mats["webbing"], bevel=0.022)
        cube(f"PouchFlap_{index}", vest, ((index - 1) * 0.145, 0.342, 1.18), (0.115, 0.025, 0.075), mats["armor_edge"], bevel=0.008)
    cube("AdminPouch", vest, (-0.15, 0.265, 1.41), (0.2, 0.09, 0.14), mats["webbing"], bevel=0.022)
    cylinder("Radio", vest, (0.265, 0.22, 1.31), 0.065, 0.28, mats["armor_edge"], vertices=14, bevel=0.01)
    cylinder_between("RadioCable", vest, (0.265, 0.22, 1.44), (0.18, 0.12, 1.61), 0.012, mats["steel"], 10)
    join_direct_meshes(vest, "VestGeometry")

    helmet = empty("Helmet", root)
    sphere("HelmetShell", helmet, (0, -0.01, 1.91), (0.215, 0.205, 0.155), mats["armor"], 24, 14)
    cube("HelmetBrow", helmet, (0, 0.175, 1.89), (0.38, 0.09, 0.07), mats["armor_edge"], bevel=0.025)
    cube("NvgShroud", helmet, (0, 0.218, 1.96), (0.13, 0.045, 0.11), mats["steel"], bevel=0.018)
    for side in (-1.0, 1.0):
        cube(f"HelmetRail_{side}", helmet, (side * 0.205, 0.02, 1.91), (0.035, 0.22, 0.07), mats["steel"], bevel=0.012)
        cylinder(f"HeadsetCup_{side}", helmet, (side * 0.218, -0.015, 1.78), 0.07, 0.05, mats["rubber"], (0, math.pi / 2, 0), 16, 0.01)
    join_direct_meshes(helmet, "HelmetGeometry")

    backpack = empty("Backpack", root)
    cube("PackBody", backpack, (0, -0.265, 1.25), (0.45, 0.22, 0.58), mats["webbing"], bevel=0.07)
    cube("PackPocket", backpack, (0, -0.4, 1.18), (0.34, 0.12, 0.27), mats["uniform_dark"], bevel=0.04)
    for side in (-1.0, 1.0):
        cube(f"PackSidePouch_{side}", backpack, (side * 0.27, -0.27, 1.2), (0.1, 0.18, 0.31), mats["webbing"], bevel=0.025)
        cube(f"PackStrap_{side}", backpack, (side * 0.16, -0.13, 1.3), (0.055, 0.08, 0.57), mats["armor_edge"], bevel=0.015)
    cylinder("RolledPoncho", backpack, (0, -0.31, 0.92), 0.1, 0.42, mats["uniform"], (0, math.pi / 2, 0), 18, 0.018)
    cylinder("PackAntenna", backpack, (0.18, -0.26, 1.77), 0.012, 0.52, mats["steel"], vertices=10, bevel=0.002)
    join_direct_meshes(backpack, "BackpackGeometry")

    patch = empty("TeamPatch", root)
    cube("TeamPatchGeometry", patch, (0.16, 0.252, 1.43), (0.15, 0.025, 0.09), mats["patch"], bevel=0.012)
    cube("ChestCableGuide", root, (0.13, 0.255, 1.56), (0.035, 0.035, 0.18), mats["steel"], rotation=(0, math.radians(-12), 0), bevel=0.006)

    join_direct_meshes(root, "OperatorBodyGeometry")
    assert left_leg.name == "LeftLegRig" and right_leg.name == "RightLegRig"
    return root


def scene_triangle_count() -> int:
    total = 0
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def export_current_scene(glb_path: Path, blend_path: Path) -> tuple[int, int]:
    glb_path.parent.mkdir(parents=True, exist_ok=True)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    mesh_count = sum(1 for obj in bpy.context.scene.objects if obj.type == "MESH")
    return mesh_count, scene_triangle_count()


def add_preview_stage(
    target: tuple[float, float, float],
    camera_position: tuple[float, float, float],
    floor_z: float,
    output: Path,
) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    world = bpy.context.scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.014, 0.02, 0.023, 1.0)
        background.inputs["Strength"].default_value = 0.28
    floor_mat = make_material("PreviewFloor", (0.045, 0.052, 0.052, 1.0), 0.05, 0.82)
    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0, 0, floor_z))
    floor = bpy.context.object
    floor.name = "PreviewFloor"
    floor.data.materials.append(floor_mat)

    for name, location, energy, color, size in (
        ("PreviewKey", (3.5, -4.5, 5.0), 1050.0, (0.7, 0.9, 1.0), 3.5),
        ("PreviewFill", (-3.2, -1.0, 3.0), 750.0, (0.25, 1.0, 0.58), 3.0),
        ("PreviewRim", (1.5, 4.0, 4.2), 980.0, (1.0, 0.34, 0.12), 2.5),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector(target) - light.location).to_track_quat("-Z", "Y").to_euler()

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = camera_position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 58.0
    bpy.context.scene.camera = camera
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    build_weapon()
    weapon_meshes, weapon_triangles = export_current_scene(
        WEAPON_DIR / "steel_tide_m4a1.glb",
        SOURCE_DIR / "steel_tide_m4a1.blend",
    )
    bpy.data.objects["SpareMagazine"].hide_render = True
    bpy.data.objects["Suppressor"].hide_render = True
    add_preview_stage((0, 0.25, 0), (2.25, -2.8, 1.45), -0.38, PREVIEW_DIR / "steel_tide_m4a1.png")

    build_operator()
    operator_meshes, operator_triangles = export_current_scene(
        OPERATOR_DIR / "steel_tide_operator.glb",
        SOURCE_DIR / "steel_tide_operator.blend",
    )
    add_preview_stage((0, 0, 1.0), (3.1, 5.0, 2.35), -0.01, PREVIEW_DIR / "steel_tide_operator.png")
    print(
        "COMBAT_MODELS_EXPORT "
        f"weapon_meshes={weapon_meshes} weapon_triangles={weapon_triangles} "
        f"operator_meshes={operator_meshes} operator_triangles={operator_triangles}"
    )


if __name__ == "__main__":
    main()
