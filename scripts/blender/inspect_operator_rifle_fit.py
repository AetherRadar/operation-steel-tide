"""Render close inspection views of the animated operator holding the M4A1."""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = REPO_ROOT / "source_art" / "third_party" / "bamen_military_soldier" / "bamen_military_soldier_animated.blend"
WEAPON_GLB = REPO_ROOT / "assets" / "models" / "steel_tide_m4a1" / "steel_tide_m4a1.glb"
OUTPUT_DIR = REPO_ROOT / "build" / "art-previews" / "operator-rifle-fit"
RIGHT_HAND = "mixamorig:RightHand"
WEAPON_SCALE = 0.476
RIFLE_ORIGIN = Vector((-0.16, -0.18, 1.61))
RIFLE_FORWARD = Vector((0.12, -0.993, 0.0)).normalized()
RIFLE_UP = Vector((0.0, 0.0, 1.0))


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def import_weapon() -> bpy.types.Object:
    bpy.ops.import_scene.gltf(filepath=str(WEAPON_GLB))
    weapon = bpy.data.objects["SteelTideM4A1"]
    spare_magazine = bpy.data.objects["SpareMagazine"]
    spare_magazine.hide_render = True
    for child in spare_magazine.children_recursive:
        child.hide_render = True
    suppressor = bpy.data.objects["Suppressor"]
    suppressor.hide_render = True
    for child in suppressor.children_recursive:
        child.hide_render = True
    rifle_right = RIFLE_FORWARD.cross(RIFLE_UP).normalized()
    rifle_up = rifle_right.cross(RIFLE_FORWARD).normalized()
    rifle_matrix = Matrix((rifle_right, RIFLE_FORWARD, rifle_up)).transposed().to_4x4()
    rifle_matrix.translation = RIFLE_ORIGIN
    weapon.matrix_world = rifle_matrix @ Matrix.Diagonal(
        (WEAPON_SCALE, WEAPON_SCALE, WEAPON_SCALE, 1.0)
    )
    return weapon


def setup_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    armature.animation_data.action = bpy.data.actions["aim_idle"]
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    weapon = import_weapon()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.012, 0.016, 0.018)

    floor_material = bpy.data.materials.new("RifleFitFloor")
    floor_material.diffuse_color = (0.065, 0.075, 0.072, 1.0)
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, 0.0))
    bpy.context.object.data.materials.append(floor_material)

    camera_data = bpy.data.cameras.new("RifleFitCamera")
    camera = bpy.data.objects.new("RifleFitCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.65

    for name, location, energy, color, size in (
        ("RifleFitKey", (-2.5, -3.0, 4.0), 1050, (1.0, 0.92, 0.82), 3.0),
        ("RifleFitFill", (2.2, -1.0, 2.6), 750, (0.30, 0.62, 0.82), 2.5),
        ("RifleFitRim", (0.5, 2.5, 3.0), 900, (0.72, 0.86, 1.0), 2.0),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.color = color
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        look_at(light, Vector((0.0, -0.25, 1.45)))

    bpy.context.view_layer.update()
    return armature, weapon


def main() -> None:
    armature, weapon = setup_scene()
    scene = bpy.context.scene
    camera = scene.camera
    target = Vector((0.0, -0.28, 1.46))
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    views = {
        "front": Vector((0.0, -5.0, 1.55)),
        "front_three_quarter": Vector((2.8, -4.2, 2.0)),
        "support_side": Vector((4.8, -0.35, 1.62)),
        "stock_side": Vector((-4.8, -0.35, 1.62)),
    }
    for name, location in views.items():
        camera.location = location
        look_at(camera, target)
        scene.render.filepath = str(OUTPUT_DIR / f"aim_idle_{name}.png")
        bpy.ops.render.render(write_still=True)

    for bone_name in ("mixamorig:RightHand", "mixamorig:LeftHand"):
        bone = armature.pose.bones[bone_name]
        print(
            "RIFLE_HAND "
            f"bone={bone_name} "
            f"head={tuple(round(value, 4) for value in (armature.matrix_world @ bone.head))} "
            f"tail={tuple(round(value, 4) for value in (armature.matrix_world @ bone.tail))}"
        )
    print(
        "RIFLE_WEAPON "
        f"origin={tuple(round(value, 4) for value in weapon.matrix_world.translation)} "
        f"rotation={tuple(round(math.degrees(value), 2) for value in weapon.matrix_world.to_euler())}"
    )


if __name__ == "__main__":
    main()
