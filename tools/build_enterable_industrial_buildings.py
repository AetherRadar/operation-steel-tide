"""Cut real ground-floor door apertures into the licensed industrial buildings.

Run with Blender 4.5+ from the repository root:

    blender --background --python tools/build_enterable_industrial_buildings.py

The portal catalog is shared with the Godot runtime. Coordinates in the catalog
use Godot's X/Z ground plane; this tool converts them back to Blender X/Y.
"""

from __future__ import annotations

import json
import math
import pathlib

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[1]
SOURCE_DIRECTORY = REPOSITORY_ROOT / "assets" / "models" / "kenney_city_kit_industrial"
LAYOUT_PATH = SOURCE_DIRECTORY / "enterable_layouts.json"
OUTPUT_DIRECTORY = SOURCE_DIRECTORY / "enterable"
FACTORY_DIRECTORY = REPOSITORY_ROOT / "assets" / "models" / "kenney_factory_kit"
CUTTER_DEPTH = 0.72
CUTTER_FLOOR_OVERLAP = 0.025


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def import_building(model_id: str) -> bpy.types.Object:
    source_path = SOURCE_DIRECTORY / f"{model_id}.glb"
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"{source_path.name}: expected one mesh, found {len(meshes)}")
    model = meshes[0]
    model.name = f"{model_id}-enterable"
    return model


def blender_ground_vector(godot_x: float, godot_z: float) -> Vector:
    return Vector((godot_x, -godot_z, 0.0))


def cut_portal(
    model: bpy.types.Object,
    model_id: str,
    portal_index: int,
    portal: dict[str, object],
) -> None:
    center_x, center_z = portal["center"]
    normal_x, normal_z = portal["normal"]
    width = float(portal["width"])
    height = float(portal["height"])
    center = blender_ground_vector(float(center_x), float(center_z))
    normal = blender_ground_vector(float(normal_x), float(normal_z)).normalized()

    bpy.ops.mesh.primitive_cube_add(
        location=(center.x, center.y, height * 0.5 - CUTTER_FLOOR_OVERLAP)
    )
    cutter = bpy.context.active_object
    cutter.name = f"{model_id}-portal-cutter-{portal_index:02d}"
    if abs(normal.x) > 0.5:
        cutter.dimensions = (CUTTER_DEPTH, width, height + CUTTER_FLOOR_OVERLAP * 2.0)
    else:
        cutter.dimensions = (width, CUTTER_DEPTH, height + CUTTER_FLOOR_OVERLAP * 2.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    modifier = model.modifiers.new(name=cutter.name, type="BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    bpy.context.view_layer.objects.active = model
    model.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def aperture_is_clear(model: bpy.types.Object, portal: dict[str, object]) -> bool:
    center_x, center_z = portal["center"]
    normal_x, normal_z = portal["normal"]
    height = float(portal["height"])
    center = blender_ground_vector(float(center_x), float(center_z))
    normal = blender_ground_vector(float(normal_x), float(normal_z)).normalized()
    sample_height = min(height * 0.52, height - 0.025)
    origin = center + normal * 0.24 + Vector((0.0, 0.0, sample_height))
    direction = -normal
    tree = BVHTree.FromObject(model, bpy.context.evaluated_depsgraph_get())
    hit = tree.ray_cast(origin, direction, 0.48)
    return hit[0] is None


def find_interior_anchor(model: bpy.types.Object, layout: dict[str, object]) -> Vector:
    """Find a ground-floor point enclosed by floor, roof, and walls in all directions."""
    minimum_x, minimum_z, maximum_x, maximum_z = [float(value) for value in layout["bounds"]]
    tree = BVHTree.FromObject(model, bpy.context.evaluated_depsgraph_get())
    maximum_distance = max(maximum_x - minimum_x, maximum_z - minimum_z) * 2.4
    best: tuple[float, Vector] | None = None
    for z_step in range(1, 16):
        godot_z = minimum_z + (maximum_z - minimum_z) * z_step / 16.0
        for x_step in range(1, 16):
            godot_x = minimum_x + (maximum_x - minimum_x) * x_step / 16.0
            point = blender_ground_vector(godot_x, godot_z) + Vector((0.0, 0.0, 0.20))
            floor_hit = tree.ray_cast(point, Vector((0.0, 0.0, -1.0)), 0.28)
            roof_hit = tree.ray_cast(point, Vector((0.0, 0.0, 1.0)), 5.0)
            if floor_hit[0] is None or roof_hit[0] is None:
                continue
            wall_distances: list[float] = []
            for direction_index in range(16):
                angle = math.tau * direction_index / 16.0
                direction = Vector((math.cos(angle), math.sin(angle), 0.0))
                hit = tree.ray_cast(point, direction, maximum_distance)
                if hit[0] is None:
                    break
                wall_distances.append((hit[0] - point).length)
            if len(wall_distances) != 16:
                continue
            minimum_clearance = min(wall_distances)
            roof_clearance = (roof_hit[0] - point).length
            if minimum_clearance < 0.12 or roof_clearance < 0.12:
                continue
            score = minimum_clearance + min(roof_clearance, 1.0) * 0.18
            if best is None or score > best[0]:
                best = (score, point)
    if best is None:
        raise RuntimeError(f"{layout['id']}: no enclosed ground-floor interior anchor was found")
    return best[1]


def export_building(model: bpy.types.Object, model_id: str) -> pathlib.Path:
    OUTPUT_DIRECTORY.mkdir(parents=True, exist_ok=True)
    output_path = OUTPUT_DIRECTORY / f"{model_id}-enterable.glb"
    bpy.ops.object.select_all(action="DESELECT")
    model.select_set(True)
    bpy.context.view_layer.objects.active = model
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
    )
    return output_path


def export_hinged_door() -> pathlib.Path:
    """Remove the sample scene objects from Kenney's door GLB and keep its hinge pivot."""
    clear_scene()
    source_path = FACTORY_DIRECTORY / "door.glb"
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"{source_path.name}: no door mesh was imported")
    door = max(meshes, key=lambda obj: len(obj.data.polygons))
    if len(door.data.polygons) < 100:
        raise RuntimeError(f"{source_path.name}: authored door panel was not found")
    door.parent = None
    door.matrix_parent_inverse.identity()
    door.matrix_world = Matrix.Translation((0.8, 0.0, 0.0))
    for imported in list(bpy.context.scene.objects):
        if imported != door:
            bpy.data.objects.remove(imported, do_unlink=True)
    door.name = "door-hinged"
    bpy.context.view_layer.update()

    corners = [door.matrix_world @ Vector(corner) for corner in door.bound_box]
    minimum = Vector(tuple(min(point[axis] for point in corners) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in corners) for axis in range(3)))
    dimensions = maximum - minimum
    if abs(minimum.x) > 0.01 or abs(dimensions.x - 0.8) > 0.02 or abs(dimensions.z - 1.6) > 0.02:
        raise RuntimeError(
            f"{source_path.name}: unexpected hinge bounds min={tuple(minimum)} size={tuple(dimensions)}"
        )

    output_path = FACTORY_DIRECTORY / "door-hinged.glb"
    bpy.ops.object.select_all(action="DESELECT")
    door.select_set(True)
    bpy.context.view_layer.objects.active = door
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
    )
    print(
        "HINGED_DOOR_EXPORT",
        f"polygons={len(door.data.polygons)}",
        f"path={output_path.relative_to(REPOSITORY_ROOT).as_posix()}",
    )
    return output_path


def main() -> None:
    catalog = json.loads(LAYOUT_PATH.read_text(encoding="utf-8"))
    failures: list[str] = []
    for layout in catalog["models"]:
        model_id = str(layout["id"])
        clear_scene()
        model = import_building(model_id)
        portals = layout["portals"]
        for index, portal in enumerate(portals):
            cut_portal(model, model_id, index, portal)
        bpy.context.view_layer.update()

        blocked = [
            index
            for index, portal in enumerate(portals)
            if not aperture_is_clear(model, portal)
        ]
        if blocked:
            failures.append(f"{model_id}:{','.join(str(index) for index in blocked)}")
        interior_anchor = find_interior_anchor(model, layout)
        expected_interior = blender_ground_vector(
            float(layout["interior"][0]),
            float(layout["interior"][1]),
        ) + Vector((0.0, 0.0, 0.20))
        if (interior_anchor - expected_interior).length > 0.025:
            failures.append(f"{model_id}:interior-anchor-drift")
        output_path = export_building(model, model_id)
        print(
            "ENTERABLE_BUILDING_EXPORT",
            f"model={model_id}",
            f"portals={len(portals)}",
            f"blocked={','.join(str(index) for index in blocked) or 'none'}",
            f"vertices={len(model.data.vertices)}",
            f"interior=[{interior_anchor.x:.5f},{-interior_anchor.y:.5f}]",
            f"path={output_path.relative_to(REPOSITORY_ROOT).as_posix()}",
        )

    if failures:
        raise RuntimeError(f"Portal cuts remained blocked: {';'.join(failures)}")
    export_hinged_door()
    print(
        "ENTERABLE_BUILDING_PASS",
        f"valid=True models={len(catalog['models'])}",
        f"portals={sum(len(layout['portals']) for layout in catalog['models'])}",
    )


if __name__ == "__main__":
    main()
