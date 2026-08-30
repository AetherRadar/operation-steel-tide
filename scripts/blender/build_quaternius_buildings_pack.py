"""Convert selected Quaternius Buildings Pack FBXs into Godot-ready GLBs.

Run with Blender 4.5 or newer:
    blender --background --python scripts/blender/build_quaternius_buildings_pack.py

Pass ``-- --only building1-large house2`` to rebuild selected assets. Source
FBXs remain untouched. Each output preserves the authored mesh and rendered materials,
is centered on Blender's horizontal plane, grounded at Z=0, and carries its
CC0 provenance as glTF extras.
"""

from __future__ import annotations

import argparse
import hashlib
import sys
from dataclasses import dataclass
from itertools import permutations
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "quaternius_buildings_pack"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "quaternius_buildings_pack"
SOURCE_URL = "https://quaternius.com/packs/buildings.html"
LICENSE_URL = "https://creativecommons.org/publicdomain/zero/1.0/"
ACQUISITION_DATE = "2026-08-28"


@dataclass(frozen=True)
class AssetSpec:
    slug: str
    source_name: str
    output_name: str
    root_name: str


@dataclass(frozen=True)
class MaterialState:
    base_color: tuple[float, float, float, float]
    metallic: float
    roughness: float
    alpha: float
    emission_color: tuple[float, float, float, float]
    emission_strength: float
    alpha_mode: str
    backface_culling: bool


@dataclass(frozen=True)
class CornerState:
    position: tuple[float, float, float]
    normal: tuple[float, float, float]
    uvs: tuple[tuple[float, float], ...]


@dataclass(frozen=True)
class TriangleState:
    material: str
    centroid: tuple[float, float, float]
    corners: tuple[CornerState, CornerState, CornerState]


ASSETS = (
    AssetSpec(
        "building1-large",
        "Building1_Large.fbx",
        "building1-large.glb",
        "QuaterniusBuilding1Large",
    ),
    AssetSpec(
        "building1-small",
        "Building1_Small.fbx",
        "building1-small.glb",
        "QuaterniusBuilding1Small",
    ),
    AssetSpec(
        "building2-large",
        "Building2_Large.fbx",
        "building2-large.glb",
        "QuaterniusBuilding2Large",
    ),
    AssetSpec(
        "building2-small",
        "Building2_Small.fbx",
        "building2-small.glb",
        "QuaterniusBuilding2Small",
    ),
    AssetSpec(
        "building3-big",
        "Building3_Big.fbx",
        "building3-big.glb",
        "QuaterniusBuilding3Big",
    ),
    AssetSpec(
        "building3-small",
        "Building3_Small.fbx",
        "building3-small.glb",
        "QuaterniusBuilding3Small",
    ),
    AssetSpec(
        "building4",
        "Building4.fbx",
        "building4.glb",
        "QuaterniusBuilding4",
    ),
    AssetSpec(
        "house1",
        "House1.fbx",
        "house1.glb",
        "QuaterniusHouse1",
    ),
    AssetSpec(
        "house2",
        "House2.fbx",
        "house2.glb",
        "QuaterniusHouse2",
    ),
)


def parse_args() -> set[str]:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--only",
        nargs="+",
        choices=[asset.slug for asset in ASSETS],
        help="Rebuild only the listed asset slugs.",
    )
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(blender_args)
    return set(args.only or [asset.slug for asset in ASSETS])


def require_sources(selected: set[str]) -> None:
    missing = [
        asset.source_name
        for asset in ASSETS
        if asset.slug in selected and not (SOURCE_DIR / asset.source_name).is_file()
    ]
    if missing:
        raise FileNotFoundError(f"Missing Quaternius source files: {', '.join(missing)}")


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.images,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def import_fbx(path: Path) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.fbx(
        filepath=str(path),
        global_scale=1.0,
        use_manual_orientation=False,
        bake_space_transform=False,
        use_image_search=True,
        use_anim=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {path.name}: {result}")

    imported = [obj for obj in bpy.data.objects if obj not in before]
    for obj in list(imported):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            imported.remove(obj)
    if not any(obj.type == "MESH" for obj in imported):
        raise RuntimeError(f"No mesh objects were imported from {path.name}")
    return imported


def mesh_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not corners:
        raise RuntimeError("Cannot calculate bounds without meshes")
    minimum = Vector(
        (
            min(point.x for point in corners),
            min(point.y for point in corners),
            min(point.z for point in corners),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in corners),
            max(point.y for point in corners),
            max(point.z for point in corners),
        )
    )
    return minimum, maximum


def socket_scalar(principled: bpy.types.Node, name: str, fallback: float) -> float:
    socket = principled.inputs.get(name)
    return float(socket.default_value) if socket is not None else fallback


def socket_color(
    principled: bpy.types.Node,
    names: tuple[str, ...],
    fallback: tuple[float, float, float, float],
) -> tuple[float, float, float, float]:
    for name in names:
        socket = principled.inputs.get(name)
        if socket is not None:
            return tuple(float(channel) for channel in socket.default_value)
    return fallback


def material_state(material: bpy.types.Material) -> MaterialState:
    diffuse = tuple(float(channel) for channel in material.diffuse_color)
    if not material.use_nodes:
        return MaterialState(
            diffuse,
            0.0,
            0.5,
            diffuse[3],
            (0.0, 0.0, 0.0, 1.0),
            0.0,
            str(getattr(material, "surface_render_method", "OPAQUE")),
            material.use_backface_culling,
        )

    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Authored material {material.name} has no Principled BSDF node")
    base_color = socket_color(principled, ("Base Color",), diffuse)
    return MaterialState(
        base_color=base_color,
        metallic=socket_scalar(principled, "Metallic", 0.0),
        roughness=socket_scalar(principled, "Roughness", 0.5),
        alpha=socket_scalar(principled, "Alpha", base_color[3]),
        emission_color=socket_color(
            principled,
            ("Emission Color", "Emission"),
            (0.0, 0.0, 0.0, 1.0),
        ),
        emission_strength=socket_scalar(principled, "Emission Strength", 0.0),
        alpha_mode=str(getattr(material, "surface_render_method", "OPAQUE")),
        backface_culling=material.use_backface_culling,
    )


def material_snapshot(
    objects: list[bpy.types.Object],
    included_names: set[str] | None = None,
) -> dict[str, MaterialState]:
    materials = {
        material
        for obj in objects
        if obj.type == "MESH"
        for material in obj.data.materials
        if material is not None
        and (included_names is None or material.name in included_names)
    }
    if not materials:
        raise RuntimeError("The source contains no authored materials")
    return {
        material.name: material_state(material)
        for material in sorted(materials, key=lambda item: item.name)
    }


def normalize_solid_materials(objects: list[bpy.types.Object]) -> None:
    materials = {
        material
        for obj in objects
        if obj.type == "MESH"
        for material in obj.data.materials
        if material is not None
    }
    for material in materials:
        diffuse = tuple(float(channel) for channel in material.diffuse_color)
        material.diffuse_color = (*diffuse[:3], 1.0)
        if hasattr(material, "surface_render_method"):
            material.surface_render_method = "DITHERED"
        if not material.use_nodes:
            continue
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is None:
            raise RuntimeError(f"Authored material {material.name} has no Principled BSDF node")
        alpha = principled.inputs.get("Alpha")
        if alpha is not None:
            alpha.default_value = 1.0


def material_triangle_counts(objects: list[bpy.types.Object]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for obj in objects:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        for triangle in obj.data.loop_triangles:
            if triangle.material_index >= len(obj.material_slots):
                raise RuntimeError(f"{obj.name} triangle references a missing material slot")
            material = obj.material_slots[triangle.material_index].material
            if material is None:
                raise RuntimeError(f"{obj.name} triangle references an empty material slot")
            counts[material.name] = counts.get(material.name, 0) + 1
    return dict(sorted(counts.items()))


def quantized(values: tuple[float, ...] | Vector, digits: int) -> tuple[float, ...]:
    return tuple(round(float(value), digits) for value in values)


def geometry_snapshot(objects: list[bpy.types.Object]) -> tuple[TriangleState, ...]:
    records: list[TriangleState] = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        uv_layers = list(mesh.uv_layers)
        corner_normals = mesh.corner_normals
        for triangle in mesh.loop_triangles:
            material = obj.material_slots[triangle.material_index].material
            if material is None:
                raise RuntimeError(f"{obj.name} triangle references an empty material slot")
            corners: list[CornerState] = []
            for loop_index, vertex_index in zip(triangle.loops, triangle.vertices):
                position = obj.matrix_world @ mesh.vertices[vertex_index].co
                normal = normal_matrix @ corner_normals[loop_index].vector
                normal.normalize()
                uvs = tuple(
                    tuple(float(value) for value in layer.data[loop_index].uv)
                    for layer in uv_layers
                )
                corners.append(
                    CornerState(
                        position=tuple(float(value) for value in position),
                        normal=tuple(float(value) for value in normal),
                        uvs=uvs,
                    )
                )
            centroid = sum((Vector(corner.position) for corner in corners), Vector()) / 3.0
            records.append(
                TriangleState(
                    material=material.name,
                    centroid=tuple(float(value) for value in centroid),
                    corners=(corners[0], corners[1], corners[2]),
                )
            )
    return tuple(records)


def geometry_digest(snapshot: tuple[TriangleState, ...]) -> str:
    records = []
    for triangle in snapshot:
        corners = tuple(
            sorted(
                (
                    quantized(corner.position, 4),
                    quantized(corner.normal, 3),
                    tuple(quantized(uv, 4) for uv in corner.uvs),
                )
                for corner in triangle.corners
            )
        )
        records.append((triangle.material, corners))

    digest = hashlib.sha256()
    for record in sorted(records):
        digest.update(repr(record).encode("utf-8"))
        digest.update(b"\n")
    return digest.hexdigest()


def vector_distance(left: tuple[float, ...], right: tuple[float, ...]) -> float:
    return sum((a - b) ** 2 for a, b in zip(left, right)) ** 0.5


def corners_match(
    expected: CornerState,
    actual: CornerState,
    compare_normal: bool,
) -> bool:
    if vector_distance(expected.position, actual.position) > 0.0005:
        return False
    if compare_normal and vector_distance(expected.normal, actual.normal) > 0.005:
        return False
    if len(expected.uvs) != len(actual.uvs):
        return False
    return all(
        vector_distance(expected_uv, actual_uv) <= 0.0005
        for expected_uv, actual_uv in zip(expected.uvs, actual.uvs)
    )


def triangles_match(expected: TriangleState, actual: TriangleState) -> bool:
    edge_a = Vector(expected.corners[1].position) - Vector(expected.corners[0].position)
    edge_b = Vector(expected.corners[2].position) - Vector(expected.corners[0].position)
    compare_normal = edge_a.cross(edge_b).length > 0.0000001
    return any(
        all(
            corners_match(
                expected.corners[index],
                actual.corners[actual_index],
                compare_normal,
            )
            for index, actual_index in enumerate(order)
        )
        for order in permutations(range(3))
    )


def validate_geometry(
    output_path: Path,
    expected: tuple[TriangleState, ...],
    actual: tuple[TriangleState, ...],
) -> None:
    expected_by_material: dict[str, list[TriangleState]] = {}
    actual_by_material: dict[str, list[TriangleState]] = {}
    for triangle in expected:
        expected_by_material.setdefault(triangle.material, []).append(triangle)
    for triangle in actual:
        actual_by_material.setdefault(triangle.material, []).append(triangle)
    if {name: len(items) for name, items in expected_by_material.items()} != {
        name: len(items) for name, items in actual_by_material.items()
    }:
        raise RuntimeError(f"{output_path.name} changed per-material triangle counts")

    for material, expected_triangles in expected_by_material.items():
        actual_triangles = actual_by_material[material]
        tree = KDTree(len(actual_triangles))
        for index, triangle in enumerate(actual_triangles):
            tree.insert(Vector(triangle.centroid), index)
        tree.balance()
        consumed: set[int] = set()
        for expected_triangle in expected_triangles:
            candidates = tree.find_range(Vector(expected_triangle.centroid), 0.0005)
            match = next(
                (
                    index
                    for _, index, _ in candidates
                    if index not in consumed
                    and triangles_match(expected_triangle, actual_triangles[index])
                ),
                None,
            )
            if match is None:
                raise RuntimeError(
                    f"{output_path.name} changed {material} triangle geometry, normals, or UVs "
                    f"near centroid={expected_triangle.centroid}"
                )
            consumed.add(match)


def mesh_statistics(objects: list[bpy.types.Object]) -> tuple[int, int]:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangle_count = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangle_count += len(obj.data.loop_triangles)
    return len(meshes), triangle_count


def normalize_asset(objects: list[bpy.types.Object], spec: AssetSpec) -> bpy.types.Object:
    minimum, maximum = mesh_bounds(objects)
    offset = Vector(
        (
            -(minimum.x + maximum.x) * 0.5,
            -(minimum.y + maximum.y) * 0.5,
            -minimum.z,
        )
    )
    translation = Matrix.Translation(offset)
    imported_set = set(objects)
    top_level = [obj for obj in objects if obj.parent not in imported_set]
    for obj in top_level:
        obj.matrix_world = translation @ obj.matrix_world

    root = bpy.data.objects.new(spec.root_name, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 1.0
    root["source_creator"] = "Quaternius"
    root["source_asset"] = "Buildings Pack"
    root["source_file"] = spec.source_name
    root["source_url"] = SOURCE_URL
    root["license"] = "CC0-1.0"
    root["license_url"] = LICENSE_URL
    root["acquisition_date"] = ACQUISITION_DATE
    root["units"] = "meters"
    for obj in top_level:
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    bpy.context.view_layer.update()
    return root


def validate_normalization(objects: list[bpy.types.Object], spec: AssetSpec) -> Vector:
    minimum, maximum = mesh_bounds(objects)
    dimensions = maximum - minimum
    if min(dimensions) < 0.05:
        raise RuntimeError(f"{spec.slug} has a collapsed dimension: {tuple(dimensions)}")
    if max(dimensions) > 100.0:
        raise RuntimeError(f"{spec.slug} is not in plausible meter scale: {tuple(dimensions)}")
    if abs(minimum.z) > 0.002:
        raise RuntimeError(f"{spec.slug} is not grounded at Z=0 (minimum Z={minimum.z:.6f})")
    if abs(minimum.x + maximum.x) > 0.002 or abs(minimum.y + maximum.y) > 0.002:
        raise RuntimeError(
            f"{spec.slug} is not horizontally centered: min={tuple(minimum)} max={tuple(maximum)}"
        )
    return dimensions


def export_glb(root: bpy.types.Object, objects: list[bpy.types.Object], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_extras=True,
    )
    if "FINISHED" not in result or not output_path.is_file():
        raise RuntimeError(f"Blender could not export {output_path.name}: {result}")


def validate_materials(
    output_path: Path,
    expected: dict[str, MaterialState],
    actual: dict[str, MaterialState],
) -> None:
    if actual.keys() != expected.keys():
        raise RuntimeError(
            f"{output_path.name} changed materials during glTF round-trip: "
            f"expected={sorted(expected)} actual={sorted(actual)}"
        )
    for name, expected_state in expected.items():
        actual_state = actual[name]
        if expected_state.alpha < 0.997 or actual_state.alpha < 0.997:
            raise RuntimeError(
                f"{output_path.name} contains a transparent solid material {name}: "
                f"expected_alpha={expected_state.alpha:.4f} actual_alpha={actual_state.alpha:.4f}"
            )
        if expected_state.base_color[3] < 0.997 or actual_state.base_color[3] < 0.997:
            raise RuntimeError(
                f"{output_path.name} contains a transparent base color for {name}: "
                f"expected_alpha={expected_state.base_color[3]:.4f} "
                f"actual_alpha={actual_state.base_color[3]:.4f}"
            )
        expected_values = (
            *expected_state.base_color,
            expected_state.metallic,
            expected_state.roughness,
            expected_state.alpha,
            expected_state.emission_strength,
        )
        actual_values = (
            *actual_state.base_color,
            actual_state.metallic,
            actual_state.roughness,
            actual_state.alpha,
            actual_state.emission_strength,
        )
        if any(abs(left - right) > 0.003 for left, right in zip(expected_values, actual_values)):
            raise RuntimeError(
                f"{output_path.name} changed {name} PBR values during glTF round-trip: "
                f"expected={expected_state} actual={actual_state}"
            )
        if max(expected_state.emission_strength, actual_state.emission_strength) > 0.003:
            if any(
                abs(left - right) > 0.003
                for left, right in zip(
                    expected_state.emission_color,
                    actual_state.emission_color,
                )
            ):
                raise RuntimeError(
                    f"{output_path.name} changed {name} active emission color during glTF round-trip"
                )
        if expected_state.backface_culling != actual_state.backface_culling:
            raise RuntimeError(
                f"{output_path.name} changed {name} backface-culling state during glTF round-trip"
            )
        if expected_state.alpha_mode != actual_state.alpha_mode:
            raise RuntimeError(
                f"{output_path.name} changed {name} alpha mode during glTF round-trip: "
                f"expected={expected_state.alpha_mode} actual={actual_state.alpha_mode}"
            )


def verify_glb(
    output_path: Path,
    spec: AssetSpec,
    expected_dimensions: Vector,
    expected_materials: dict[str, MaterialState],
    expected_material_triangles: dict[str, int],
    expected_geometry: tuple[TriangleState, ...],
    expected_geometry_digest: str,
) -> tuple[Vector, int, int, str]:
    clear_scene()
    configure_scene()
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.gltf(filepath=str(output_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not verify {output_path.name}: {result}")
    imported = [obj for obj in bpy.data.objects if obj not in before]
    minimum, maximum = mesh_bounds(imported)
    dimensions = maximum - minimum
    if any(abs(dimensions[index] - expected_dimensions[index]) > 0.005 for index in range(3)):
        raise RuntimeError(
            f"{output_path.name} changed dimensions during glTF round-trip: "
            f"expected={tuple(expected_dimensions)} actual={tuple(dimensions)}"
        )
    if abs(minimum.z) > 0.005:
        raise RuntimeError(f"{output_path.name} moved off Z=0 during glTF round-trip")
    if abs(minimum.x + maximum.x) > 0.005 or abs(minimum.y + maximum.y) > 0.005:
        raise RuntimeError(f"{output_path.name} lost horizontal centering during glTF round-trip")

    validate_materials(output_path, expected_materials, material_snapshot(imported))
    actual_material_triangles = material_triangle_counts(imported)
    if actual_material_triangles != expected_material_triangles:
        raise RuntimeError(
            f"{output_path.name} changed per-material triangle assignment: "
            f"expected={expected_material_triangles} actual={actual_material_triangles}"
        )
    validate_geometry(output_path, expected_geometry, geometry_snapshot(imported))
    expected_metadata = {
        "source_creator": "Quaternius",
        "source_asset": "Buildings Pack",
        "source_file": spec.source_name,
        "source_url": SOURCE_URL,
        "license": "CC0-1.0",
        "license_url": LICENSE_URL,
        "acquisition_date": ACQUISITION_DATE,
        "units": "meters",
    }
    metadata_owners = [
        obj
        for obj in imported
        if all(obj.get(key) == value for key, value in expected_metadata.items())
    ]
    if len(metadata_owners) != 1:
        raise RuntimeError(f"{output_path.name} lost or duplicated its provenance metadata")
    mesh_count, triangle_count = mesh_statistics(imported)
    return dimensions, mesh_count, triangle_count, expected_geometry_digest


def build_asset(spec: AssetSpec) -> None:
    clear_scene()
    configure_scene()
    objects = import_fbx(SOURCE_DIR / spec.source_name)
    raw_minimum, raw_maximum = mesh_bounds(objects)
    raw_dimensions = raw_maximum - raw_minimum
    normalize_solid_materials(objects)
    expected_material_triangles = material_triangle_counts(objects)
    expected_materials = material_snapshot(objects, set(expected_material_triangles))
    root = normalize_asset(objects, spec)
    normalized_dimensions = validate_normalization(objects, spec)
    expected_geometry = geometry_snapshot(objects)
    expected_geometry_digest = geometry_digest(expected_geometry)
    source_meshes, source_triangles = mesh_statistics(objects)
    output_path = OUTPUT_DIR / spec.output_name
    export_glb(root, objects, output_path)
    verified_dimensions, verified_meshes, verified_triangles, verified_digest = verify_glb(
        output_path,
        spec,
        normalized_dimensions,
        expected_materials,
        expected_material_triangles,
        expected_geometry,
        expected_geometry_digest,
    )
    if (verified_meshes, verified_triangles) != (source_meshes, source_triangles):
        raise RuntimeError(
            f"{output_path.name} changed mesh statistics during glTF round-trip: "
            f"source={source_meshes}/{source_triangles} "
            f"verified={verified_meshes}/{verified_triangles}"
        )

    output_sha256 = hashlib.sha256(output_path.read_bytes()).hexdigest()

    print(
        "QUATERNIUS_BUILDING_ASSET "
        f"slug={spec.slug} source={spec.source_name} output={spec.output_name} "
        f"raw_m={raw_dimensions.x:.3f}x{raw_dimensions.y:.3f}x{raw_dimensions.z:.3f} "
        f"normalized_m={normalized_dimensions.x:.3f}x{normalized_dimensions.y:.3f}x{normalized_dimensions.z:.3f} "
        f"verified_m={verified_dimensions.x:.3f}x{verified_dimensions.y:.3f}x{verified_dimensions.z:.3f} "
        f"meshes={source_meshes} triangles={source_triangles} "
        f"materials={len(expected_materials)} "
        f"material_names={','.join(expected_materials)} "
        f"geometry_sha256={verified_digest} "
        f"sha256={output_sha256} metadata=verified bytes={output_path.stat().st_size}"
    )


def main() -> None:
    selected = parse_args()
    require_sources(selected)
    for spec in ASSETS:
        if spec.slug in selected:
            build_asset(spec)
    print(f"QUATERNIUS_BUILDING_PASS built={len(selected)} output={OUTPUT_DIR}")


if __name__ == "__main__":
    main()
