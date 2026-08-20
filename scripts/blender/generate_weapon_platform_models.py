"""Generate authored firearm platform models for Operation Steel Tide.

Run with Blender 4.5 LTS or newer from the repository root.  The meshes are
assembled and bevelled in Blender, exported as independent GLBs, and kept with
their editable .blend sources so Godot never presents runtime primitives as
the final weapon art.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
ASSET_ROOT = ROOT / "assets" / "models" / "weapon_platforms"
SOURCE_ROOT = ROOT / "source_art" / "weapon_platforms"
PREVIEW_ROOT = ROOT / "build" / "art-previews" / "weapon_platforms"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def material(name: str, color: tuple[float, float, float, float], metallic=0.0, roughness=0.7):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.metallic = metallic
    mat.roughness = roughness
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat


def empty(name: str, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.06
    if parent:
        obj.parent = parent
    return obj


def finish(obj, parent, mat, bevel=0.0, smooth=False):
    obj.parent = parent
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
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


def cube(name, parent, location, dimensions, mat, rotation=(0.0, 0.0, 0.0), bevel=0.008):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    return finish(obj, parent, mat, bevel)


def cylinder(name, parent, location, radius, depth, mat, rotation=(math.pi / 2, 0.0, 0.0), vertices=20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, parent, mat, 0.004, True)


def tapered(name, parent, location, lower, upper, height, mat, bevel=0.008):
    lx, ly = lower
    ux, uy = upper
    z0, z1 = -height * 0.5, height * 0.5
    vertices = [(-lx, -ly, z0), (lx, -ly, z0), (lx, ly, z0), (-lx, ly, z0),
                (-ux, -uy, z1), (ux, -uy, z1), (ux, uy, z1), (-ux, uy, z1)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    return finish(obj, parent, mat, bevel)


def cylinder_between(name, parent, start, end, radius, mat):
    a, b = Vector(start), Vector(end)
    direction = b - a
    obj = cylinder(name, parent, tuple((a + b) * 0.5), radius, direction.length, mat, rotation=(0.0, 0.0, 0.0), vertices=16)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def add_contract_nodes(root):
    nodes = {}
    for name in ("Magazine", "SpareMagazine", "ChargingHandle", "Stock", "Foregrip", "MuzzleDevice", "Suppressor", "OpticMount"):
        nodes[name] = empty(name, root)
    return nodes


def common_platform(root, mats, *, receiver, handguard, barrel, stock, magazine, wood=False, compact=False, integrated=False):
    nodes = add_contract_nodes(root)
    phosphate, steel, polymer, accent, glass = mats["phosphate"], mats["steel"], mats["polymer"], mats["accent"], mats["glass"]
    grip_mat = mats["wood"] if wood else polymer
    receiver_width = 0.15 if compact else 0.18
    tapered("UpperReceiver", root, (0, 0.0, 0.035), (receiver_width * 0.48, receiver * 0.46), (receiver_width * 0.44, receiver * 0.43), 0.13, phosphate, 0.01)
    cube("LowerReceiver", root, (0, -0.015, -0.055), (receiver_width, receiver * 0.76, 0.12), phosphate, bevel=0.012)
    cube("TopRail", root, (0, 0.08, 0.12), (receiver_width * 0.88, receiver + 0.2, 0.035), steel, bevel=0.003)
    cube("Handguard", root, (0, handguard * 0.5, 0.02), (receiver_width * 0.95, handguard, 0.145), grip_mat, bevel=0.018)
    for side in (-1.0, 1.0):
        for index in range(3 if compact else 5):
            cube(f"RailSlot_{side}_{index}", root, (side * receiver_width * 0.52, 0.27 + index * 0.09, 0.02), (0.01, 0.05, 0.032), phosphate, bevel=0.003)
    barrel_start = handguard + 0.1
    cylinder("Barrel", root, (0, barrel_start + barrel * 0.5, 0.025), 0.026 if not integrated else 0.075, barrel, steel if not integrated else phosphate)
    if integrated:
        for index in range(5):
            cylinder(f"SuppressorBaffle_{index}", nodes["Suppressor"], (0, barrel_start + 0.08 + index * 0.1, 0.025), 0.08, 0.018, steel)
    cylinder("GasBlock", root, (0, barrel_start - 0.02, 0.03), 0.045, 0.08, phosphate, rotation=(0.0, math.pi / 2, 0.0), vertices=16)
    stock_start = -receiver * 0.72
    if stock == "skeleton":
        cube("StockTube", nodes["Stock"], (0, stock_start - 0.22, 0.03), (0.045, 0.46, 0.045), steel, bevel=0.006)
        cube("StockButt", nodes["Stock"], (0, stock_start - 0.48, 0.03), (0.17, 0.07, 0.2), polymer, rotation=(math.radians(-7), 0, 0), bevel=0.015)
    else:
        cube("StockBody", nodes["Stock"], (0, stock_start - 0.22, 0.03), (0.18, 0.48, 0.13), grip_mat, rotation=(math.radians(-4), 0, 0), bevel=0.02)
        cube("StockButt", nodes["Stock"], (0, stock_start - 0.46, 0.03), (0.2, 0.09, 0.23), grip_mat, rotation=(math.radians(-7), 0, 0), bevel=0.015)
    mag = nodes["Magazine"]
    cube("MagazineBody", mag, (0, 0, -0.1), (0.15, magazine, 0.16), polymer if not wood else grip_mat, rotation=(math.radians(-8 if wood else 0), 0, 0), bevel=0.014)
    for index in range(3):
        cube(f"MagazineRib_{index}", mag, (0, -0.03 + index * 0.045, -0.185), (0.008, 0.018, 0.17), steel, bevel=0.002)
    nodes["SpareMagazine"].location = (-0.3, -0.05, -0.57)
    cube("SpareMagazineBody", nodes["SpareMagazine"], (0, 0, 0), (0.15, magazine, 0.16), polymer, rotation=(math.radians(-8), 0, 0), bevel=0.014)
    nodes["SpareMagazine"].hide_render = True
    nodes["Foregrip"].location = (0, handguard * 0.78, -0.13)
    tapered("ForegripBody", nodes["Foregrip"], (0, 0, 0), (0.045, 0.075), (0.038, 0.065), 0.2, polymer, 0.01)
    nodes["ChargingHandle"].location = (receiver_width * 0.52, 0.06, 0.1)
    cube("ChargingStem", nodes["ChargingHandle"], (0, 0, 0), (0.028, 0.13, 0.028), steel, bevel=0.004)
    cube("ChargingLatch", nodes["ChargingHandle"], (-0.035, -0.035, 0), (0.08, 0.035, 0.038), steel, bevel=0.005)
    nodes["MuzzleDevice"].location = (0, barrel_start + barrel + 0.04, 0.025)
    cylinder("MuzzleBrake", nodes["MuzzleDevice"], (0, 0, 0), 0.045, 0.12, phosphate)
    nodes["Suppressor"].location = (0, barrel_start + barrel + 0.08, 0.025)
    cylinder("SuppressorBody", nodes["Suppressor"], (0, 0, 0), 0.06, 0.28, phosphate)
    for index in range(4):
        cylinder(f"SuppressorRing_{index}", nodes["Suppressor"], (0, -0.1 + index * 0.065, 0), 0.063, 0.018, steel)
    nodes["OpticMount"].location = (0, 0.23, 0.15)
    cube("OpticRiser", nodes["OpticMount"], (0, 0, 0), (0.12, 0.16, 0.04), phosphate, bevel=0.005)
    cube("OpticGlass", nodes["OpticMount"], (0, 0.04, 0.08), (0.1, 0.1, 0.08), glass, bevel=0.012)
    return nodes


def build_platform(platform):
    clear_scene()
    mats = {
        "phosphate": material("Phosphate", (0.025, 0.032, 0.03, 1), 0.78, 0.28),
        "steel": material("MachinedSteel", (0.13, 0.15, 0.145, 1), 0.9, 0.2),
        "polymer": material("ReinforcedPolymer", (0.035, 0.043, 0.04, 1), 0.08, 0.66),
        "wood": material("OiledWood", (0.24, 0.105, 0.035, 1), 0.02, 0.68),
        "accent": material("PlatformAccent", (0.12, 0.26, 0.22, 1), 0.2, 0.4),
        "glass": material("OpticGlass", (0.03, 0.22, 0.25, 1), 0.45, 0.08),
    }
    root = empty(f"SteelTide{platform}")
    if platform == "AK74":
        common_platform(root, mats, receiver=0.52, handguard=0.43, barrel=0.78, stock="solid", magazine=0.48, wood=True)
        cube("GasTube", root, (0, 0.68, 0.11), (0.09, 0.36, 0.06), mats["wood"], bevel=0.012)
    elif platform == "ScarL":
        common_platform(root, mats, receiver=0.58, handguard=0.5, barrel=0.82, stock="skeleton", magazine=0.45)
        cube("UpperMonorail", root, (0, 0.12, 0.17), (0.2, 0.72, 0.04), mats["accent"], bevel=0.008)
    elif platform == "M24":
        common_platform(root, mats, receiver=0.66, handguard=0.64, barrel=1.18, stock="solid", magazine=0.22, wood=True)
        cube("BoltHandle", root, (0.1, -0.05, -0.06), (0.08, 0.23, 0.05), mats["steel"], bevel=0.008)
        cylinder("ScopeTube", root, (0, 0.16, 0.27), 0.045, 0.55, mats["steel"], rotation=(math.pi / 2, 0, 0))
    elif platform == "MP5A5":
        common_platform(root, mats, receiver=0.4, handguard=0.28, barrel=0.42, stock="skeleton", magazine=0.5, compact=True)
        cylinder("ThreeLug", root, (0, 0.82, 0.025), 0.055, 0.09, mats["steel"])
    elif platform == "M3A1":
        common_platform(root, mats, receiver=0.55, handguard=0.2, barrel=0.58, stock="solid", magazine=0.56, compact=True)
        cube("TopChargingSlot", root, (0, 0.03, 0.12), (0.11, 0.28, 0.02), mats["steel"], bevel=0.003)
    elif platform == "AXMC":
        common_platform(root, mats, receiver=0.72, handguard=0.78, barrel=1.32, stock="skeleton", magazine=0.25)
        cube("ChassisSpine", root, (0, -0.25, -0.12), (0.22, 0.72, 0.08), mats["accent"], bevel=0.012)
        for side in (-1, 1):
            cylinder("BipodLeg", root, (side * 0.14, 0.48, -0.25), 0.018, 0.34, mats["steel"], rotation=(0.3, 0, side * 0.22), vertices=12)
    elif platform == "AWM":
        common_platform(root, mats, receiver=0.68, handguard=0.72, barrel=1.28, stock="solid", magazine=0.22)
        cube("ThumbholeStock", root, (0, -0.56, -0.1), (0.24, 0.38, 0.18), mats["accent"], bevel=0.025)
        cylinder("ScopeTube", root, (0, 0.15, 0.28), 0.05, 0.68, mats["steel"], rotation=(math.pi / 2, 0, 0))
    elif platform == "VSS":
        common_platform(root, mats, receiver=0.5, handguard=0.24, barrel=0.76, stock="solid", magazine=0.38, wood=True, integrated=True)
        cube("VSSUpperRail", root, (0, 0.1, 0.19), (0.13, 0.5, 0.04), mats["steel"], bevel=0.005)
    elif platform in ("P226", "M1911"):
        nodes = add_contract_nodes(root)
        slide = 0.78 if platform == "M1911" else 0.74
        cube("Slide", root, (0, 0.08, 0.1), (0.2, slide, 0.18), mats["phosphate"], bevel=0.018)
        cube("Frame", root, (0, -0.14, 0.02), (0.22, 0.46, 0.16), mats["steel"], bevel=0.02)
        cube("Grip", root, (0, -0.39, -0.13), (0.21, 0.34, 0.28), mats["wood"] if platform == "M1911" else mats["polymer"], rotation=(math.radians(-15), 0, 0), bevel=0.025)
        cylinder("Barrel", root, (0, 0.52, 0.1), 0.04, 0.22, mats["steel"])
        nodes["Magazine"].location = (0, -0.37, -0.16)
        cube("MagazineBody", nodes["Magazine"], (0, 0, 0), (0.13, 0.36, 0.14), mats["steel"], bevel=0.01)
        nodes["MuzzleDevice"].location = (0, 0.67, 0.1)
        cylinder("Muzzle", nodes["MuzzleDevice"], (0, 0, 0), 0.045, 0.08, mats["steel"])
        nodes["OpticMount"].location = (0, 0.08, 0.2)
        cube("OpticRiser", nodes["OpticMount"], (0, 0, 0), (0.12, 0.14, 0.035), mats["steel"], bevel=0.004)
    else:
        raise ValueError(platform)
    return root


def export(root, platform):
    ASSET_ROOT.mkdir(parents=True, exist_ok=True)
    SOURCE_ROOT.mkdir(parents=True, exist_ok=True)
    path = ASSET_ROOT / f"{platform.lower()}.glb"
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB", export_apply=True, export_materials="EXPORT", export_cameras=False, export_lights=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_ROOT / f"{platform.lower()}.blend"))
    return path


def main():
    for platform in ("AK74", "ScarL", "M24", "MP5A5", "M3A1", "AXMC", "AWM", "VSS", "P226", "M1911"):
        root = build_platform(platform)
        export(root, platform)
        print(f"WEAPON_PLATFORM_EXPORT platform={platform} path=assets/models/weapon_platforms/{platform.lower()}.glb")


if __name__ == "__main__":
    main()
