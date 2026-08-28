"""Rebuild the Guangchang pawnshop frontage from authored CC0 meshes.

This is a targeted Blender DCC adaptation pass.  It imports VVayToyek's
CC0 four-corner pavilion, reshapes its modeled timber-and-tile assembly into
the pawnshop canopy, and reuses the packed Poly Haven apartment facade meshes
for the compound wings.  It does not generate visible primitive geometry.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


PAWNSHOP_ROOT_NAME = "GuangchangPawnshop"
AUTHORED_PREFIX = "PawnshopAuthored"
LEGACY_GATE_OBJECTS = (
    "GuangchangPawnshopSignBacking",
    "GuangchangPawnshopDangPlaqueBacking",
    "PawnshopGatePierL",
    "PawnshopGatePierR",
    "PawnshopGatePierCapL",
    "PawnshopGatePierCapR",
)
LEGACY_SOUTH_WALL_PREFIXES = (
    "PawnshopSouthEast_",
    "PawnshopSouthEastCap_",
    "PawnshopSouthWest_",
    "PawnshopSouthWestCap_",
)
PAVILION_RETAINED_PARTS = frozenset(
    (
        "套兽1",
        "套兽2",
        "檐柱1",
        "檐柱2",
        "挂落1",
        "檐檩垫板1",
        "角云1",
        "角云4",
        "金檩金枋1",
        "额枋1",
        "檐椽1",
        "脑椽1",
        "飞椽1",
        "衬头木1",
        "瓦面1",
    )
)
PAVILION_SOURCE_URL = (
    "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free"
)
PAVILION_CREATOR = "VVayToyek"
PAVILION_LICENSE = "CC0 1.0 Universal"
PAVILION_ACQUISITION_DATE = "2026-08-28"

CANOPY_CENTER = Vector((-86.0, 112.0, 0.0))
CANOPY_AXIS_SCALE = Vector((0.257, 0.116, 0.220))
COLUMN_X = {
    "檐柱1": -81.95,
    "檐柱2": -90.05,
    "檐柱3": -81.95,
    "檐柱4": -90.05,
}

FACADE_LAYOUT = (
    # suffix, wall mesh, insert mesh, x, y, z, yaw, x scale.
    # The Poly Haven modules use their right edge as the object origin.
    ("East_F0_C0", "Mesh.018", "Cube.115", -79.00, 111.86, 0.03, 0.0, 0.9667),
    ("East_F0_C1", "Mesh.012", "Cube.004", -76.10, 111.86, 0.03, 0.0, 0.9667),
    ("East_F0_C2", "Mesh.018", "Cube.115", -73.20, 111.86, 0.03, 0.0, 0.9667),
    ("West_F0_C0", "Mesh.018", "Cube.115", -93.00, 111.86, 0.03, 3.141592654, 0.9667),
    ("West_F0_C1", "Mesh.012", "Cube.004", -95.90, 111.86, 0.03, 3.141592654, 0.9667),
    ("West_F0_C2", "Mesh.018", "Cube.115", -98.80, 111.86, 0.03, 3.141592654, 0.9667),
    ("EastReturn_F0", "Mesh.018", "Cube.115", -73.20, 111.86, 0.03, -1.570796327, 0.9667),
    ("WestReturn_F0", "Mesh.018", "Cube.115", -98.80, 111.86, 0.03, -1.570796327, 0.9667),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pavilion-fbx", required=True, type=Path)
    parser.add_argument("--save", action="store_true")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])


def remove_legacy_frontage() -> int:
    removed = 0
    for obj in list(bpy.data.objects):
        if (
            obj.name in LEGACY_GATE_OBJECTS
            or obj.name.startswith(LEGACY_SOUTH_WALL_PREFIXES)
            or obj.name.startswith(AUTHORED_PREFIX)
        ):
            bpy.data.objects.remove(obj, do_unlink=True)
            removed += 1
    return removed


def principled_material(
    name: str,
    base_color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    coat_weight: float,
) -> bpy.types.Material:
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    if material.node_tree is None:
        raise RuntimeError(f"Material node tree is unavailable: {name}")
    principled = next(
        (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        raise RuntimeError(f"Principled shader is unavailable: {name}")
    principled.inputs["Base Color"].default_value = base_color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    coat = principled.inputs.get("Coat Weight")
    if coat is not None:
        coat.default_value = coat_weight
    coat_roughness = principled.inputs.get("Coat Roughness")
    if coat_roughness is not None:
        coat_roughness.default_value = 0.24
    return material


def pavilion_materials() -> dict[str, bpy.types.Material]:
    return {
        "tile": principled_material(
            "JianghaiPavilionGlazedTile",
            (0.025, 0.068, 0.078, 1.0),
            0.18,
            0.34,
            0.30,
        ),
        "lacquer": principled_material(
            "JianghaiPavilionVermilionLacquer",
            (0.330, 0.025, 0.012, 1.0),
            0.08,
            0.38,
            0.42,
        ),
        "ornament": principled_material(
            "JianghaiPavilionPatinatedOrnament",
            (0.044, 0.160, 0.122, 1.0),
            0.28,
            0.43,
            0.22,
        ),
    }


def material_role(source_part_name: str) -> str:
    if source_part_name.startswith(("瓦面", "垂脊", "套兽", "雷公柱")):
        return "tile"
    if source_part_name.startswith(("挂落", "角云")):
        return "ornament"
    return "lacquer"


def apply_world_axis_scale(obj: bpy.types.Object, axis: int, factor: float) -> None:
    center = obj.matrix_world.translation.copy()
    values = [1.0, 1.0, 1.0, 1.0]
    values[axis] = factor
    transform = (
        Matrix.Translation(center)
        @ Matrix.Diagonal(values)
        @ Matrix.Translation(-center)
    )
    obj.matrix_world = transform @ obj.matrix_world


def import_authored_canopy(
    pavilion_fbx: Path,
    pawnshop_root: bpy.types.Object,
) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    if not pavilion_fbx.is_file():
        raise FileNotFoundError(pavilion_fbx)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(pavilion_fbx))
    imported = [obj for obj in bpy.data.objects if obj not in before]
    materials = pavilion_materials()

    canopy_root = bpy.data.objects.new(f"{AUTHORED_PREFIX}PavilionGate", None)
    bpy.context.scene.collection.objects.link(canopy_root)
    canopy_root.parent = pawnshop_root
    canopy_root["district_role"] = "authored_cc0_pawnshop_canopy"
    canopy_root["source_asset"] = "Chinese Four-corner Pavilion - Free"
    canopy_root["source_creator"] = PAVILION_CREATOR
    canopy_root["source_url"] = PAVILION_SOURCE_URL
    canopy_root["source_license"] = PAVILION_LICENSE
    canopy_root["acquired_on"] = PAVILION_ACQUISITION_DATE

    retained: list[bpy.types.Object] = []
    world_scale = Matrix.Diagonal((*CANOPY_AXIS_SCALE, 1.0))
    for obj in imported:
        source_name = obj.name
        if obj.type != "MESH" or source_name not in PAVILION_RETAINED_PARTS:
            bpy.data.objects.remove(obj, do_unlink=True)
            continue

        obj.matrix_world = Matrix.Translation(CANOPY_CENTER) @ world_scale @ obj.matrix_world
        if source_name in COLUMN_X:
            location = obj.matrix_world.translation.copy()
            location.x = COLUMN_X[source_name]
            obj.matrix_world.translation = location
        if source_name.startswith(("额枋1", "额枋2", "檐檩垫板1", "檐檩垫板2")):
            apply_world_axis_scale(obj, 0, 1.24)

        role = material_role(source_name)
        obj.data.materials.clear()
        obj.data.materials.append(materials[role])
        obj.name = f"{AUTHORED_PREFIX}Canopy_{len(retained):02d}"
        obj.parent = canopy_root
        obj["source_part_name"] = source_name
        obj["source_asset"] = "Chinese Four-corner Pavilion - Free"
        obj["source_creator"] = PAVILION_CREATOR
        obj["source_url"] = PAVILION_SOURCE_URL
        obj["source_license"] = PAVILION_LICENSE
        obj["acquired_on"] = PAVILION_ACQUISITION_DATE
        retained.append(obj)

    if len(retained) != len(PAVILION_RETAINED_PARTS):
        raise RuntimeError(f"Pavilion import retained too few authored parts: {len(retained)}")
    return canopy_root, retained


def source_for_mesh(mesh_name: str) -> bpy.types.Object:
    source = next(
        (
            obj
            for obj in bpy.context.scene.objects
            if obj.type == "MESH" and obj.data.name == mesh_name
        ),
        None,
    )
    if source is None:
        raise RuntimeError(f"Packed facade source mesh is missing: {mesh_name}")
    return source


def tuned_facade_material(source: bpy.types.Material) -> bpy.types.Material:
    name = f"JianghaiPawnshop_{source.name}"
    existing = bpy.data.materials.get(name)
    if existing is not None:
        return existing
    material = source.copy()
    material.name = name
    if not material.use_nodes or material.node_tree is None:
        return material
    principled = next(
        (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        return material
    base_color = principled.inputs.get("Base Color")
    if base_color is None or not base_color.is_linked:
        return material
    source_link = base_color.links[0]
    source_socket = source_link.from_socket
    grade = material.node_tree.nodes.new("ShaderNodeHueSaturation")
    grade.name = "PawnshopFacadeGrade"
    grade.label = "Pawnshop warm plaster lift"
    grade.inputs["Saturation"].default_value = 0.88
    grade.inputs["Value"].default_value = 1.55
    grade.location = (
        (source_link.from_node.location.x + principled.location.x) * 0.5,
        principled.location.y,
    )
    material.node_tree.links.remove(source_link)
    material.node_tree.links.new(source_socket, grade.inputs["Color"])
    material.node_tree.links.new(grade.outputs["Color"], base_color)
    return material


def adapted_facade_mesh(source: bpy.types.Object) -> bpy.types.Mesh:
    mesh_name = f"{AUTHORED_PREFIX}_{source.data.name}"
    existing = bpy.data.meshes.get(mesh_name)
    if existing is not None:
        return existing
    mesh = source.data.copy()
    mesh.name = mesh_name
    for index, material in enumerate(tuple(mesh.materials)):
        if material is not None:
            mesh.materials[index] = tuned_facade_material(material)
    return mesh


def adapted_insert_mesh(source: bpy.types.Object) -> bpy.types.Mesh:
    mesh_name = f"{AUTHORED_PREFIX}Insert_{source.data.name}"
    existing = bpy.data.meshes.get(mesh_name)
    if existing is not None:
        return existing
    mesh = source.data.copy()
    mesh.name = mesh_name
    return mesh


def build_authored_wings(pawnshop_root: bpy.types.Object) -> list[bpy.types.Object]:
    wall_sources = {
        mesh_name: source_for_mesh(mesh_name) for mesh_name in {row[1] for row in FACADE_LAYOUT}
    }
    insert_sources = {
        mesh_name: source_for_mesh(mesh_name) for mesh_name in {row[2] for row in FACADE_LAYOUT}
    }
    wall_meshes = {
        mesh_name: adapted_facade_mesh(source) for mesh_name, source in wall_sources.items()
    }
    insert_meshes = {
        mesh_name: adapted_insert_mesh(source) for mesh_name, source in insert_sources.items()
    }
    wings: list[bpy.types.Object] = []
    for suffix, wall_name, insert_name, x, y, z, yaw, x_scale in FACADE_LAYOUT:
        for role, source, mesh in (
            ("Wall", wall_sources[wall_name], wall_meshes[wall_name]),
            ("Insert", insert_sources[insert_name], insert_meshes[insert_name]),
        ):
            wing = source.copy()
            wing.data = mesh
            wing.name = f"{AUTHORED_PREFIX}Wing_{suffix}_{role}"
            bpy.context.scene.collection.objects.link(wing)
            wing.parent = pawnshop_root
            wing.location = (x, y, z)
            wing.rotation_euler = (0.0, 0.0, yaw)
            wing.scale = (x_scale, 1.0, 1.0)
            wing["district_role"] = "authored_cc0_pawnshop_wing"
            wing["source_asset"] = "Modular Urban Apartments Facade"
            wing["source_creator"] = "James Ray Cock"
            wing["source_url"] = "https://polyhaven.com/a/modular_urban_apartments_facade"
            wing["source_license"] = "CC0 1.0 Universal"
            wings.append(wing)
    return wings


def place_existing_details() -> None:
    placements = {
        "GuangchangPawnshopSignText": ((-86.0, 109.56, 4.92), (2.20, 2.20, 2.20)),
        "GuangchangPawnshopDangPlaqueText": ((-90.72, 111.70, 2.20), (1.45, 1.45, 1.45)),
        "PawnshopGuardianLionWest": ((-90.55, 109.70, 0.06), (4.65, 4.65, 4.65)),
        "PawnshopGuardianLionEast": ((-81.45, 109.70, 0.06), (4.65, 4.65, 4.65)),
        "PawnshopLantern00": ((-90.12, 110.48, 4.58), (2.65, 2.65, 2.65)),
        "PawnshopLantern01": ((-81.88, 110.48, 4.58), (2.65, 2.65, 2.65)),
    }
    for object_name, (location, scale) in placements.items():
        obj = bpy.data.objects.get(object_name)
        if obj is None:
            raise RuntimeError(f"Pawnshop detail is missing: {object_name}")
        obj.location = location
        obj.scale = scale
    bpy.data.objects["PawnshopGuardianLionWest"].rotation_euler.z = -0.14
    bpy.data.objects["PawnshopGuardianLionEast"].rotation_euler.z = 0.14
    for object_name in ("GuangchangPawnshopSignText", "GuangchangPawnshopDangPlaqueText"):
        obj = bpy.data.objects[object_name]
        if not obj.data.materials or obj.data.materials[0] is None:
            continue
        material = obj.data.materials[0].copy()
        material.name = f"JianghaiPawnshopGold_{object_name}"
        if material.use_nodes and material.node_tree is not None:
            for node in material.node_tree.nodes:
                if node.type != "BSDF_PRINCIPLED":
                    continue
                node.inputs["Base Color"].default_value = (0.45, 0.16, 0.01, 1.0)
                node.inputs["Metallic"].default_value = 0.45
                node.inputs["Roughness"].default_value = 0.50
                emission_color = node.inputs.get("Emission Color")
                if emission_color is not None:
                    emission_color.default_value = (0.55, 0.12, 0.005, 1.0)
                emission = node.inputs.get("Emission Strength")
                if emission is not None:
                    emission.default_value = 0.06
        obj.data.materials[0] = material


def validate_frontage(
    canopy_root: bpy.types.Object,
    canopy_parts: list[bpy.types.Object],
    wings: list[bpy.types.Object],
) -> None:
    forbidden = [name for name in LEGACY_GATE_OBJECTS if bpy.data.objects.get(name) is not None]
    if forbidden:
        raise RuntimeError(f"Legacy primitive gate art remains: {forbidden}")
    legacy_walls = [
        obj.name
        for obj in bpy.data.objects
        if obj.name.startswith(LEGACY_SOUTH_WALL_PREFIXES)
    ]
    if legacy_walls:
        raise RuntimeError(f"Legacy zero-thickness south walls remain: {legacy_walls}")
    if (
        len(canopy_parts) != len(PAVILION_RETAINED_PARTS)
        or len(wings) != len(FACADE_LAYOUT) * 2
    ):
        raise RuntimeError(
            f"Incomplete authored frontage: canopy={len(canopy_parts)} wings={len(wings)}"
        )
    if canopy_root.get("source_license") != PAVILION_LICENSE:
        raise RuntimeError("Pavilion license metadata is missing")
    doorway_blockers = []
    for obj in canopy_parts:
        if not obj.name.startswith(f"{AUTHORED_PREFIX}Canopy_"):
            continue
        if obj.get("source_part_name", "").startswith("檐柱"):
            x = obj.matrix_world.translation.x
            if -90.0 < x < -82.0:
                doorway_blockers.append(obj.name)
    if doorway_blockers:
        raise RuntimeError(f"Authored columns intrude into the 7.6m doorway: {doorway_blockers}")


def main() -> None:
    args = parse_args()
    pawnshop_root = bpy.data.objects.get(PAWNSHOP_ROOT_NAME)
    if pawnshop_root is None:
        raise RuntimeError(f"Pawnshop anchor is missing: {PAWNSHOP_ROOT_NAME}")
    removed = remove_legacy_frontage()
    canopy_root, canopy_parts = import_authored_canopy(args.pavilion_fbx, pawnshop_root)
    wings = build_authored_wings(pawnshop_root)
    place_existing_details()
    validate_frontage(canopy_root, canopy_parts, wings)
    bpy.context.view_layer.update()
    if args.save:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath, compress=True)
    print(
        "PAWNSHOP_DCC_PASS "
        f"valid=True removed_legacy={removed} authored_canopy_parts={len(canopy_parts)} "
        f"authored_wings={len(wings)} source={PAVILION_SOURCE_URL!r}"
    )


if __name__ == "__main__":
    main()
