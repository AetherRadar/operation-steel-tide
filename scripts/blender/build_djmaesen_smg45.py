"""Build optimized runtime SMG-45 models from the tracked DJMaesen source GLB.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup --python scripts/blender/build_djmaesen_smg45.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "djmaesen_fps_smg45"
SOURCE_GLB = SOURCE_DIR / "fps_animated_smg.glb"
SOURCE_BLEND = SOURCE_DIR / "djmaesen_fps_smg45.blend"
RUNTIME_DIR = REPO_ROOT / "assets" / "models" / "djmaesen_smg45"
FIRST_PERSON_GLB = RUNTIME_DIR / "smg45_first_person.glb"
FIELD_GLB = RUNTIME_DIR / "smg45_weapon.glb"

SOURCE_IDLE_FRAME = 155
SOURCE_TO_METERS = 0.015
FIELD_ROTATION = Matrix.Rotation(math.radians(90.0), 4, "Z")
WEAPON_MESH_NAMES = (
    "base_smg45_0",
    "carrier_smg45_0",
    "bolt_smg45_0",
    "trigger_smg45_0",
    "clip_smg45_0",
    "bullet_smg45_0",
)
RUNTIME_WEAPON_NAMES = {
    "base_smg45_0": "WeaponBody",
    "carrier_smg45_0": "ChargingHandleGeometry",
    "bolt_smg45_0": "BoltGeometry",
    "trigger_smg45_0": "TriggerGeometry",
    "clip_smg45_0": "MagazineGeometry",
    "bullet_smg45_0": "ChamberedRoundGeometry",
}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def import_source() -> None:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    bpy.context.scene.render.fps = 24
    bpy.context.scene.frame_set(SOURCE_IDLE_FRAME)
    bpy.context.view_layer.update()


def evaluated_mesh_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    mesh.name = f"{name}Mesh"
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    result.matrix_world = source.matrix_world.copy()
    return result


def parent_keep_world(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_world = world


def new_root(name: str) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    bpy.context.collection.objects.link(root)
    return root


def export_root(root: bpy.types.Object, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_apply=False,
        export_image_format="AUTO",
        export_yup=True,
    )


def add_marker(root: bpy.types.Object, name: str, location: tuple[float, float, float]) -> None:
    marker = bpy.data.objects.new(name, None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 0.035
    marker.location = location
    bpy.context.collection.objects.link(marker)
    marker.parent = root


def build_first_person() -> None:
    import_source()
    root = new_root("DJMaesenSMG45FirstPerson")
    source_scale = Matrix.Scale(SOURCE_TO_METERS, 4)

    arms = evaluated_mesh_copy(bpy.data.objects["Object_7"], "AuthoredArms")
    arms.matrix_world = source_scale @ arms.matrix_world
    parent_keep_world(arms, root)
    for source_name in WEAPON_MESH_NAMES:
        runtime_name = RUNTIME_WEAPON_NAMES[source_name]
        mesh = evaluated_mesh_copy(bpy.data.objects[source_name], runtime_name)
        mesh.matrix_world = source_scale @ mesh.matrix_world
        parent_keep_world(mesh, root)

    add_marker(root, "Magazine", (0.0, -0.272, -0.08))
    add_marker(root, "ChargingHandle", (0.0, -0.098, 0.125))
    add_marker(root, "Muzzle", (0.0, -0.744, 0.074))
    export_root(root, FIRST_PERSON_GLB)


def transformed_bounds(
    objects: list[bpy.types.Object],
    transform: Matrix,
) -> tuple[Vector, Vector]:
    points = [
        transform @ obj.matrix_world @ Vector(corner)
        for obj in objects
        for corner in obj.bound_box
    ]
    minimum = Vector((
        min(point.x for point in points),
        min(point.y for point in points),
        min(point.z for point in points),
    ))
    maximum = Vector((
        max(point.x for point in points),
        max(point.y for point in points),
        max(point.z for point in points),
    ))
    return minimum, maximum


def build_field_weapon() -> None:
    import_source()
    root = new_root("DJMaesenSMG45")
    source_objects = [bpy.data.objects[name] for name in WEAPON_MESH_NAMES]
    normalized = Matrix.Scale(SOURCE_TO_METERS, 4) @ FIELD_ROTATION
    minimum, maximum = transformed_bounds(source_objects, normalized)
    center = (minimum + maximum) * 0.5
    centered = Matrix.Translation(-center) @ normalized

    for source_name in WEAPON_MESH_NAMES:
        runtime_name = RUNTIME_WEAPON_NAMES[source_name]
        mesh = evaluated_mesh_copy(bpy.data.objects[source_name], runtime_name)
        mesh.matrix_world = centered @ mesh.matrix_world
        parent_keep_world(mesh, root)
    export_root(root, FIELD_GLB)


def save_editable_source() -> None:
    import_source()
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))


def main() -> None:
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(f"Missing tracked source asset: {SOURCE_GLB}")
    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    save_editable_source()
    build_first_person()
    build_field_weapon()
    print(f"Wrote {FIRST_PERSON_GLB}")
    print(f"Wrote {FIELD_GLB}")
    print(f"Wrote {SOURCE_BLEND}")


if __name__ == "__main__":
    main()
