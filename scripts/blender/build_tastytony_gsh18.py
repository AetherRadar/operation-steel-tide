"""Build a static, centered runtime GSh-18 from the licensed TastyTony source."""

from __future__ import annotations

from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE = REPO_ROOT / "assets" / "models" / "tastytony_gsh18" / "low-poly_gsh-18.glb"
OUTPUT = REPO_ROOT / "assets" / "models" / "tastytony_gsh18" / "gsh18_runtime.glb"
EXCLUDED_MESHES = {"Cube", "Icosphere"}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def evaluated_mesh_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    # The Sketchfab import puts the actual model under a 100x conversion node.
    # Bake that complete world transform into the evaluated mesh so the runtime
    # GLB has identity node transforms and a deterministic unit contract.
    mesh.transform(source.matrix_world)
    result.matrix_world = Matrix.Identity(4)
    return result


def parent_keep_world(child: bpy.types.Object, parent: bpy.types.Object) -> None:
    world = child.matrix_world.copy()
    child.parent = parent
    child.matrix_world = world


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [
        obj.matrix_world @ Vector(corner)
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


def export_root(root: bpy.types.Object) -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT),
        export_format="GLB",
        use_selection=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_apply=False,
        export_image_format="AUTO",
        export_yup=True,
    )


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()

    sources = [
        obj for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.name not in EXCLUDED_MESHES
    ]
    if not sources:
        raise RuntimeError("No GSh-18 meshes found")

    root = bpy.data.objects.new("TastyTonyGsh18Runtime", None)
    bpy.context.collection.objects.link(root)
    copies = [
        evaluated_mesh_copy(source, f"GSh18_{index:02d}")
        for index, source in enumerate(sources)
    ]
    for copy in copies:
        copy.data.transform(Matrix.Scale(0.001, 4))

    minimum, maximum = world_bounds(copies)
    center = (minimum + maximum) * 0.5
    for copy in copies:
        copy.location = -center
        parent_keep_world(copy, root)
        copy.visible_shadow = False

    export_root(root)
    print(f"Wrote {OUTPUT}")


if __name__ == "__main__":
    main()
