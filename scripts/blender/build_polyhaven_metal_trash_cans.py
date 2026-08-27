"""Split Poly Haven's CC0 trash-can source into grounded single-can GLBs.

The official source scene contains clean and rusted cans side by side. Each
composition also has its authored handles and a lid leaning against the can.
This Blender build preserves those meshes and materials, centres each complete
composition at the origin, grounds it at Z=0, and exports one runtime GLB.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup \
        --python scripts/blender/build_polyhaven_metal_trash_cans.py
"""

from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
ASSET_ROOT = (
    REPO_ROOT
    / "assets"
    / "models"
    / "polyhaven_residential_street"
    / "metal_trash_can"
)
SOURCE_PATH = ASSET_ROOT / "metal_trash_can_1k.gltf"

VARIANTS = {
    "clean": {
        "output": "metal_trash_can_clean.glb",
        "root": "MetalTrashCanClean",
        "triangles": 6428,
    },
    "rust": {
        "output": "metal_trash_can_rust.glb",
        "root": "MetalTrashCanRust",
        "triangles": 7532,
    },
}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        for corner in obj.bound_box
    ]
    minimum = Vector(tuple(min(corner[i] for corner in corners) for i in range(3)))
    maximum = Vector(tuple(max(corner[i] for corner in corners) for i in range(3)))
    return minimum, maximum


def triangle_count(objects: list[bpy.types.Object]) -> int:
    return sum(
        len(polygon.vertices) - 2
        for obj in objects
        for polygon in obj.data.polygons
    )


def select_only(objects: list[bpy.types.Object]) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def import_variant(variant: str) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    clear_scene()
    if not SOURCE_PATH.is_file():
        raise FileNotFoundError(f"Missing official Poly Haven source: {SOURCE_PATH}")

    result = bpy.ops.import_scene.gltf(filepath=str(SOURCE_PATH))
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to import source glTF: {result}")

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 8:
        raise RuntimeError(f"Expected 8 source meshes; found {len(meshes)}")

    selected = [
        obj
        for obj in meshes
        if (obj.name.startswith("metal_trash_can_rust")) == (variant == "rust")
    ]
    if len(selected) != 4:
        raise RuntimeError(f"Expected 4 meshes for {variant}; found {len(selected)}")

    minimum, maximum = world_bounds(selected)
    offset = Vector(
        (
            -(minimum.x + maximum.x) * 0.5,
            -(minimum.y + maximum.y) * 0.5,
            -minimum.z,
        )
    )
    for obj in selected:
        obj.location += offset
    bpy.context.view_layer.update()

    root_name = str(VARIANTS[variant]["root"])
    root = bpy.data.objects.new(root_name, None)
    bpy.context.scene.collection.objects.link(root)
    root["author"] = "GurJas Studios"
    root["license"] = "CC0-1.0"
    root["source"] = "https://polyhaven.com/a/metal_trash_can"
    root["source_variant"] = variant
    for obj in selected:
        world_matrix = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world_matrix

    return root, selected


def export_variant(
    root: bpy.types.Object,
    meshes: list[bpy.types.Object],
    output_path: Path,
) -> None:
    select_only([root, *meshes])
    result = bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_extras=True,
        export_materials="EXPORT",
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to export {output_path.name}: {result}")


def validate_export(
    output_path: Path,
    expected_triangles: int,
) -> dict[str, object]:
    clear_scene()
    result = bpy.ops.import_scene.gltf(filepath=str(output_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to re-import {output_path.name}: {result}")

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 4:
        raise RuntimeError(f"{output_path.name} has {len(meshes)} meshes, expected 4")

    minimum, maximum = world_bounds(meshes)
    dimensions = maximum - minimum
    triangles = triangle_count(meshes)
    if abs(minimum.z) > 0.0001:
        raise RuntimeError(f"{output_path.name} is not grounded: min Z {minimum.z}")
    if abs((minimum.x + maximum.x) * 0.5) > 0.0001:
        raise RuntimeError(f"{output_path.name} is not centred on X")
    if abs((minimum.y + maximum.y) * 0.5) > 0.0001:
        raise RuntimeError(f"{output_path.name} is not centred on Y")
    if triangles != expected_triangles:
        raise RuntimeError(
            f"{output_path.name} has {triangles} triangles, expected {expected_triangles}"
        )

    return {
        "path": str(output_path.relative_to(REPO_ROOT)).replace("\\", "/"),
        "bounds_min_m": [round(value, 6) for value in minimum],
        "bounds_max_m": [round(value, 6) for value in maximum],
        "dimensions_m": [round(value, 6) for value in dimensions],
        "mesh_count": len(meshes),
        "triangles": triangles,
        "materials": sorted(
            {
                material.name
                for obj in meshes
                for material in obj.data.materials
                if material is not None
            }
        ),
    }


def main() -> None:
    reports: list[dict[str, object]] = []
    for variant, settings in VARIANTS.items():
        root, meshes = import_variant(variant)
        expected_triangles = int(settings["triangles"])
        if triangle_count(meshes) != expected_triangles:
            raise RuntimeError(
                f"{variant} source has {triangle_count(meshes)} triangles, "
                f"expected {expected_triangles}"
            )

        output_path = ASSET_ROOT / str(settings["output"])
        export_variant(root, meshes, output_path)
        reports.append(validate_export(output_path, expected_triangles))

    print("TRASH_CAN_BUILD " + json.dumps(reports, sort_keys=True))


if __name__ == "__main__":
    main()
