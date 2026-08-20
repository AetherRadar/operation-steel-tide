"""Render and measure representative animated operator poses."""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = REPO_ROOT / "source_art" / "third_party" / "bamen_military_soldier" / "bamen_military_soldier_animated.blend"
OUTPUT_DIR = REPO_ROOT / "build" / "art-previews" / "animated-operator"
POSES = (
    ("idle", 0),
    ("aim_idle", 0),
    ("walk", 10),
    ("run", 6),
    ("sprint", 5),
    ("aim_walk", 10),
    ("aim_run", 6),
    ("aim_sprint", 5),
    ("crouch_idle", 18),
    ("crouch_walk", 15),
    ("aim_crouch_walk", 15),
    ("prone_idle", 20),
    ("prone_crawl", 12),
    ("hit", 5),
    ("death", 55),
    ("downed", 0),
    ("revive_kneel", 40),
    ("revived", 28),
)


def world_bounds(mesh: bpy.types.Object) -> tuple[Vector, Vector]:
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for corner in evaluated.bound_box:
        point = evaluated.matrix_world @ Vector(corner)
        minimum.x = min(minimum.x, point.x)
        minimum.y = min(minimum.y, point.y)
        minimum.z = min(minimum.z, point.z)
        maximum.x = max(maximum.x, point.x)
        maximum.y = max(maximum.y, point.y)
        maximum.z = max(maximum.z, point.z)
    return minimum, maximum


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 480
    scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world.color = (0.015, 0.02, 0.018)

    floor_material = bpy.data.materials.new("InspectionFloor")
    floor_material.diffuse_color = (0.07, 0.08, 0.075, 1.0)
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, 0.0))
    floor = bpy.context.object
    floor.data.materials.append(floor_material)

    camera_data = bpy.data.cameras.new("InspectionCamera")
    camera = bpy.data.objects.new("InspectionCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.8

    key_data = bpy.data.lights.new("InspectionKey", "AREA")
    key_data.energy = 950
    key_data.size = 4.0
    key = bpy.data.objects.new("InspectionKey", key_data)
    bpy.context.collection.objects.link(key)
    key.location = (-2.5, -3.2, 4.0)
    look_at(key, Vector((0.0, 0.0, 1.0)))

    fill_data = bpy.data.lights.new("InspectionFill", "AREA")
    fill_data.energy = 600
    fill_data.color = (0.24, 0.56, 0.72)
    fill_data.size = 3.0
    fill = bpy.data.objects.new("InspectionFill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (2.0, 2.0, 2.5)
    look_at(fill, Vector((0.0, 0.0, 1.0)))
    return armature, mesh


def main() -> None:
    armature, mesh = setup_scene()
    scene = bpy.context.scene
    camera = scene.camera
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    for action_name, frame in POSES:
        action = bpy.data.actions.get(action_name)
        if action is None:
            raise RuntimeError(f"Missing action {action_name}")
        armature.animation_data.action = action
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        minimum, maximum = world_bounds(mesh)
        size = maximum - minimum
        center = (minimum + maximum) * 0.5
        prone = action_name.startswith("prone") or action_name in {"death", "downed"}
        camera.location = center + Vector((2.5 if prone else 2.1, -4.2, 1.7 if prone else 1.1))
        camera.data.ortho_scale = max(2.5, max(size.x, size.z) * 1.45)
        look_at(camera, center)
        scene.render.filepath = str(OUTPUT_DIR / f"{action_name}.png")
        bpy.ops.render.render(write_still=True)
        print(
            "ANIM_POSE "
            f"name={action_name} frame={frame} "
            f"min={tuple(round(value, 3) for value in minimum)} "
            f"max={tuple(round(value, 3) for value in maximum)} "
            f"size={tuple(round(value, 3) for value in size)}"
        )


if __name__ == "__main__":
    main()
