"""Build the Steel Tide authored first-person optic set.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup --python scripts/blender/build_authored_optics.py

The three runtime silhouettes are DCC adaptations of scope components from
Quaternius' CC0 Ultimate Guns Pack.  No generated primitive is part of the
exported runtime hierarchy.  The source glass panes are deliberately removed;
the open apertures let the real scene remain visible behind the gameplay dot.
"""

from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import struct
import zlib

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / "assets" / "models" / "quaternius_ultimate_guns"
OUTPUT_GLB = (
    REPO_ROOT / "assets" / "models" / "steel_tide_optics" / "steel_tide_optics.glb"
)
OUTPUT_BLEND = (
    REPO_ROOT / "source_art" / "combat_optics" / "steel_tide_optics.blend"
)
PREVIEW_PATH = REPO_ROOT / "build" / "art-previews" / "steel_tide_optics.png"
ADS_PREVIEW_PATH = (
    REPO_ROOT / "build" / "art-previews" / "steel_tide_optics_ads.png"
)

SOURCE_URL = "https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F"
SOURCE_CREATOR = "Quaternius"
SOURCE_LICENSE = "CC0-1.0"

APERTURE_PLANE_TOLERANCE = 1.0e-5
APERTURE_AXIS_RESIDUAL_TOLERANCE = 0.0005
ANCHOR_MATCH_TOLERANCE = 1.0e-7
ROUND_TRIP_TOLERANCE = 1.0e-6
MINIMUM_APERTURE_SEPARATION = 1.0e-4

# Filled with reviewed deterministic outputs after the first complete build.
OUTPUT_GLB_SHA256 = "F10CDBBA8ED896807EE5111EC4D5FF1256D94B6FA8EF3899783641D49472D010"
OUTPUT_GLB_BYTES = 80_356
OUTPUT_PREVIEW_SHA256 = "69CF3EE68D05F1F46A150857B120F32C5C22F8258F5D1562944D93CCFC490D6B"
OUTPUT_PREVIEW_BYTES = 1_615_951
OUTPUT_ADS_PREVIEW_SHA256 = "09801D4FE74A6DFB1444850D5995FAE12D9455DCFFD2DBC99D5D715EFD805287"
OUTPUT_ADS_PREVIEW_BYTES = 1_419_648


@dataclass(frozen=True)
class OpticSpec:
    node_name: str
    geometry_name: str
    source_filename: str
    source_object: str
    source_materials: tuple[str, ...]
    expected_vertices: int
    expected_triangles: int
    target_width: float
    target_length: float
    target_bottom: float
    target_top: float
    cross_section_power: float
    housing_color: tuple[float, float, float, float]
    hardware_color: tuple[float, float, float, float]


@dataclass(frozen=True)
class AperturePlane:
    source_axis_y: float
    source_center: Vector
    vertex_indices: tuple[int, ...]


OPTICS = (
    OpticSpec(
        node_name="MicroOptic",
        geometry_name="MicroGeometry",
        source_filename="axmc.glb",
        source_object="SniperRifle_3",
        source_materials=("Black", "DarkMetal", "Glass", "Green", "Grey"),
        expected_vertices=796,
        expected_triangles=412,
        target_width=0.112,
        target_length=0.108,
        target_bottom=-0.070,
        target_top=0.050,
        cross_section_power=0.84,
        housing_color=(0.010, 0.014, 0.016, 1.0),
        hardware_color=(0.055, 0.064, 0.067, 1.0),
    ),
    OpticSpec(
        node_name="HoloOptic",
        geometry_name="HoloGeometry",
        source_filename="awm.glb",
        source_object="SniperRifle_5",
        source_materials=(
            "LightMetal",
            "Metal",
            "DarkMetal",
            "Black",
            "Grey",
            "Glass",
        ),
        expected_vertices=800,
        expected_triangles=412,
        target_width=0.168,
        target_length=0.142,
        target_bottom=-0.092,
        target_top=0.068,
        cross_section_power=0.60,
        housing_color=(0.075, 0.070, 0.052, 1.0),
        hardware_color=(0.040, 0.044, 0.041, 1.0),
    ),
    OpticSpec(
        node_name="ScopeOptic",
        geometry_name="ScopeGeometry",
        source_filename="vss.glb",
        source_object="SniperRifle_4",
        source_materials=("Metal", "Black", "DarkMetal", "Glass", "Grey"),
        expected_vertices=774,
        expected_triangles=412,
        target_width=0.132,
        target_length=0.420,
        target_bottom=-0.084,
        target_top=0.061,
        cross_section_power=1.0,
        housing_color=(0.018, 0.025, 0.028, 1.0),
        hardware_color=(0.080, 0.090, 0.092, 1.0),
    ),
)


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def empty(
    name: str,
    parent: bpy.types.Object | None = None,
    location: Vector | None = None,
) -> bpy.types.Object:
    result = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(result)
    result.empty_display_type = "PLAIN_AXES"
    result.empty_display_size = 0.025
    result.parent = parent
    result.matrix_parent_inverse = Matrix.Identity(4)
    if location is not None:
        result.location = location
    return result


def material_base_name(material: bpy.types.Material) -> str:
    stem, separator, suffix = material.name.rpartition(".")
    return stem if separator and suffix.isdigit() else material.name


def build_scalar_pbr_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    source_file: str,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    material["source_creator"] = SOURCE_CREATOR
    material["source_url"] = SOURCE_URL
    material["source_license"] = SOURCE_LICENSE
    material["source_file"] = (
        Path("assets") / "models" / "quaternius_ultimate_guns" / source_file
    ).as_posix()
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Material {name} has no Principled BSDF node.")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Specular IOR Level"].default_value = 0.28
    return material


def import_source(spec: OpticSpec) -> bpy.types.Object:
    path = SOURCE_ROOT / spec.source_filename
    if not path.is_file():
        raise FileNotFoundError(path)
    existing_objects = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()
    imported_meshes = [
        obj
        for obj in bpy.data.objects
        if obj not in existing_objects and obj.type == "MESH"
    ]
    if len(imported_meshes) != 1 or imported_meshes[0].name != spec.source_object:
        raise RuntimeError(
            f"Unexpected Quaternius source layout in {path}: "
            f"meshes={[obj.name for obj in imported_meshes]}"
        )
    source = imported_meshes[0]
    material_names = tuple(material_base_name(material) for material in source.data.materials)
    if material_names != spec.source_materials:
        raise RuntimeError(
            f"Unexpected materials in {path}: "
            f"expected={spec.source_materials} actual={material_names}"
        )
    return source


def source_position(
    source: bpy.types.Object,
    vertex_index: int,
) -> Vector:
    return source.matrix_world @ source.data.vertices[vertex_index].co


def face_vertices(source: bpy.types.Object, faces: set[int]) -> set[int]:
    return {
        vertex_index
        for face_index in faces
        for vertex_index in source.data.polygons[face_index].vertices
    }


def face_bounds(
    source: bpy.types.Object,
    faces: set[int],
) -> tuple[Vector, Vector]:
    positions = [source_position(source, index) for index in face_vertices(source, faces)]
    if not positions:
        raise RuntimeError(f"No positions selected from {source.name}.")
    minimum = Vector(tuple(min(position[axis] for position in positions) for axis in range(3)))
    maximum = Vector(tuple(max(position[axis] for position in positions) for axis in range(3)))
    return minimum, maximum


def position_connected_face_components(source: bpy.types.Object) -> list[set[int]]:
    position_faces: dict[tuple[float, float, float], set[int]] = defaultdict(set)
    face_positions: list[set[tuple[float, float, float]]] = []
    for polygon in source.data.polygons:
        keys = {
            tuple(round(value, 5) for value in source_position(source, vertex_index))
            for vertex_index in polygon.vertices
        }
        face_positions.append(keys)
        for key in keys:
            position_faces[key].add(polygon.index)

    unseen = set(range(len(source.data.polygons)))
    components: list[set[int]] = []
    while unseen:
        pending = [min(unseen)]
        component: set[int] = set()
        while pending:
            face_index = pending.pop()
            if face_index in component:
                continue
            component.add(face_index)
            unseen.discard(face_index)
            for key in face_positions[face_index]:
                pending.extend(position_faces[key] - component)
        components.append(component)
    return components


def select_scope_component(source: bpy.types.Object, spec: OpticSpec) -> set[int]:
    candidates: list[set[int]] = []
    for component in position_connected_face_components(source):
        material_names = {
            material_base_name(source.data.materials[source.data.polygons[index].material_index])
            for index in component
        }
        minimum, maximum = face_bounds(source, component)
        dimensions = maximum - minimum
        if (
            "Glass" in material_names
            and len(component) == spec.expected_triangles
            and dimensions.x > 1.9
            and dimensions.y > 0.25
            and dimensions.z > 0.34
        ):
            candidates.append(component)
    if len(candidates) != 1:
        raise RuntimeError(
            f"Expected one glass-bearing scope component in {spec.source_filename}; "
            f"found={len(candidates)}"
        )
    faces = candidates[0]
    vertices = face_vertices(source, faces)
    triangles = sum(len(source.data.polygons[index].vertices) - 2 for index in faces)
    if (len(vertices), triangles) != (spec.expected_vertices, spec.expected_triangles):
        raise RuntimeError(
            f"Unexpected {spec.node_name} source topology: "
            f"vertices={len(vertices)}/{spec.expected_vertices} "
            f"triangles={triangles}/{spec.expected_triangles}"
        )
    return faces


def oriented_position(source: bpy.types.Object, vertex_index: int) -> Vector:
    # Quaternius guns use +X forward, +Z up.  The project weapon convention is
    # +Y forward in Blender; glTF then imports into Godot as -Z forward, +Y up.
    return Matrix.Rotation(1.5707963267948966, 4, "Z") @ source_position(
        source,
        vertex_index,
    )


def bounding_center(positions: list[Vector]) -> Vector:
    if not positions:
        raise RuntimeError("Cannot derive a center from an empty position set.")
    minimum = Vector(
        tuple(min(position[axis] for position in positions) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(position[axis] for position in positions) for axis in range(3))
    )
    return (minimum + maximum) * 0.5


def source_aperture_planes(
    source: bpy.types.Object,
    faces: set[int],
) -> tuple[AperturePlane, AperturePlane, set[int]]:
    glass_faces = {
        face_index
        for face_index in faces
        if material_base_name(
            source.data.materials[source.data.polygons[face_index].material_index]
        )
        == "Glass"
    }
    glass_vertices = face_vertices(source, glass_faces)
    glass_triangles = sum(
        len(source.data.polygons[index].vertices) - 2 for index in glass_faces
    )
    if (len(glass_faces), len(glass_vertices), glass_triangles) != (12, 16, 12):
        raise RuntimeError(
            f"Unexpected source glass topology on {source.name}: "
            f"faces={len(glass_faces)}/12 vertices={len(glass_vertices)}/16 "
            f"triangles={glass_triangles}/12"
        )
    oriented = {
        index: oriented_position(source, index)
        for index in glass_vertices
    }
    rear_y = min(position.y for position in oriented.values())
    front_y = max(position.y for position in oriented.values())
    if front_y - rear_y <= MINIMUM_APERTURE_SEPARATION:
        raise RuntimeError(
            f"Source aperture planes collapse on {source.name}: "
            f"rear={rear_y:.9f} front={front_y:.9f}"
        )
    rear_vertices = tuple(
        sorted(
            index
            for index, position in oriented.items()
            if abs(position.y - rear_y) <= APERTURE_PLANE_TOLERANCE
        )
    )
    front_vertices = tuple(
        sorted(
            index
            for index, position in oriented.items()
            if abs(position.y - front_y) <= APERTURE_PLANE_TOLERANCE
        )
    )
    if (
        len(rear_vertices) != 8
        or len(front_vertices) != 8
        or set(rear_vertices) & set(front_vertices)
        or set(rear_vertices) | set(front_vertices) != glass_vertices
    ):
        raise RuntimeError(
            f"Source glass does not form two independent aperture planes on {source.name}: "
            f"rear_vertices={len(rear_vertices)} "
            f"front_vertices={len(front_vertices)} total={len(glass_vertices)}"
        )
    for plane_name, vertex_indices in (
        ("rear", rear_vertices),
        ("front", front_vertices),
    ):
        plane_faces = [
            source.data.polygons[index]
            for index in glass_faces
            if set(source.data.polygons[index].vertices).issubset(vertex_indices)
        ]
        plane_triangles = sum(len(polygon.vertices) - 2 for polygon in plane_faces)
        plane_y = [oriented[index].y for index in vertex_indices]
        if (
            len(plane_faces) != 6
            or plane_triangles != 6
            or max(plane_y) - min(plane_y) > APERTURE_PLANE_TOLERANCE
        ):
            raise RuntimeError(
                f"Unexpected {plane_name} glass plane topology on {source.name}: "
                f"faces={len(plane_faces)}/6 triangles={plane_triangles}/6 "
                f"axis_span={max(plane_y) - min(plane_y):.9f}"
            )
    rear = AperturePlane(
        source_axis_y=rear_y,
        source_center=bounding_center([oriented[index] for index in rear_vertices]),
        vertex_indices=rear_vertices,
    )
    front = AperturePlane(
        source_axis_y=front_y,
        source_center=bounding_center([oriented[index] for index in front_vertices]),
        vertex_indices=front_vertices,
    )
    return rear, front, glass_faces


def deformed_aperture_center(
    source: bpy.types.Object,
    plane: AperturePlane,
    deform,
) -> Vector:
    return bounding_center(
        [deform(source.data.vertices[index].co) for index in plane.vertex_indices]
    )


def signed_power(value: float, exponent: float) -> float:
    if abs(value) <= 1.0e-12:
        return 0.0
    return (1.0 if value > 0.0 else -1.0) * abs(value) ** exponent


def build_point_deformer(
    source: bpy.types.Object,
    faces: set[int],
    spec: OpticSpec,
    aperture_center: Vector,
):
    positions = [oriented_position(source, index) for index in face_vertices(source, faces)]
    minimum = Vector(tuple(min(position[axis] for position in positions) for axis in range(3)))
    maximum = Vector(tuple(max(position[axis] for position in positions) for axis in range(3)))
    source_width = max(
        abs(minimum.x - aperture_center.x),
        abs(maximum.x - aperture_center.x),
    )
    source_bottom = aperture_center.z - minimum.z
    source_top = maximum.z - aperture_center.z
    source_length = maximum.y - minimum.y
    source_mid_y = (minimum.y + maximum.y) * 0.5
    if min(source_width, source_bottom, source_top, source_length) <= 1.0e-6:
        raise RuntimeError(f"Degenerate optic source bounds for {spec.node_name}.")

    def deform(position: Vector) -> Vector:
        oriented = Matrix.Rotation(1.5707963267948966, 4, "Z") @ (
            source.matrix_world @ position
        )
        normalized_x = (oriented.x - aperture_center.x) / source_width
        x = signed_power(normalized_x, spec.cross_section_power) * (
            spec.target_width * 0.5
        )
        y = (oriented.y - source_mid_y) / source_length * spec.target_length
        delta_z = oriented.z - aperture_center.z
        if delta_z < 0.0:
            normalized_z = delta_z / source_bottom
            z = -signed_power(-normalized_z, spec.cross_section_power) * abs(
                spec.target_bottom
            )
        else:
            normalized_z = delta_z / source_top
            z = signed_power(normalized_z, spec.cross_section_power) * spec.target_top
        return Vector((x, y, z))

    return deform


def extract_authored_geometry(
    source: bpy.types.Object,
    parent: bpy.types.Object,
    spec: OpticSpec,
    faces: set[int],
    glass_faces: set[int],
    deform,
    housing: bpy.types.Material,
    hardware: bpy.types.Material,
) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    if len(mesh.polygons) != len(source.data.polygons):
        raise RuntimeError(f"Evaluated topology changed for {source.name}.")

    topology = bmesh.new()
    topology.from_mesh(mesh)
    topology.faces.ensure_lookup_table()
    rejected = [
        face
        for face in topology.faces
        if face.index not in faces or face.index in glass_faces
    ]
    bmesh.ops.delete(topology, geom=rejected, context="FACES")
    loose = [vertex for vertex in topology.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(topology, geom=loose, context="VERTS")
    topology.to_mesh(mesh)
    topology.free()

    for vertex in mesh.vertices:
        vertex.co = deform(vertex.co)

    source_material_names = tuple(
        material_base_name(material) for material in source.data.materials
    )
    material_mapping = {
        index: 1 if name == "DarkMetal" else 0
        for index, name in enumerate(source_material_names)
    }
    original_indices = [polygon.material_index for polygon in mesh.polygons]
    mesh.materials.clear()
    mesh.materials.append(housing)
    mesh.materials.append(hardware)
    for polygon, original_index in zip(mesh.polygons, original_indices):
        polygon.material_index = material_mapping[original_index]

    mesh.name = f"{spec.geometry_name}Mesh"
    mesh.validate(clean_customdata=False)
    mesh.update()
    mesh.calc_loop_triangles()
    if len(mesh.polygons) != spec.expected_triangles - 12:
        raise RuntimeError(
            f"{spec.geometry_name} did not remove exactly 12 glass faces: "
            f"faces={len(mesh.polygons)}"
        )
    if not mesh.vertices or not mesh.loop_triangles:
        raise RuntimeError(f"{spec.geometry_name} has no renderable geometry.")

    result = bpy.data.objects.new(spec.geometry_name, mesh)
    bpy.context.collection.objects.link(result)
    result.parent = parent
    result.matrix_parent_inverse = Matrix.Identity(4)
    result["source_creator"] = SOURCE_CREATOR
    result["source_url"] = SOURCE_URL
    result["source_license"] = SOURCE_LICENSE
    result["source_file"] = (
        Path("assets")
        / "models"
        / "quaternius_ultimate_guns"
        / spec.source_filename
    ).as_posix()
    result["source_object"] = spec.source_object
    result["source_component"] = "glass-bearing authored scope component"
    result["removed_source_glass_faces"] = 12
    result["runtime_generated_primitive"] = False
    result["dcc_cross_section_power"] = spec.cross_section_power
    return result


def mesh_statistics(root: bpy.types.Object) -> tuple[int, int, int]:
    meshes = [
        obj
        for obj in (root, *root.children_recursive)
        if obj.type == "MESH"
    ]
    for obj in meshes:
        obj.data.calc_loop_triangles()
    return (
        len(meshes),
        sum(len(obj.data.vertices) for obj in meshes),
        sum(len(obj.data.loop_triangles) for obj in meshes),
    )


def add_aperture_anchor(
    dcc_name: str,
    runtime_name: str,
    variant: bpy.types.Object,
    geometry: bpy.types.Object,
    spec: OpticSpec,
    plane_name: str,
    plane: AperturePlane,
    location: Vector,
) -> bpy.types.Object:
    anchor = empty(dcc_name, variant, location)
    anchor["runtime_asset"] = True
    anchor["runtime_contract_name"] = runtime_name
    anchor["derived_from_mesh"] = geometry.name
    anchor["derived_from_removed_source_glass"] = True
    anchor["derived_from_source_file"] = spec.source_filename
    anchor["derived_from_source_object"] = spec.source_object
    anchor["derived_from_source_material"] = "Glass"
    anchor["derived_from_source_plane"] = plane_name
    anchor["source_plane_axis_y"] = plane.source_axis_y
    anchor["source_plane_center"] = list(plane.source_center)
    anchor["source_plane_vertex_count"] = len(plane.vertex_indices)
    anchor["authored_aperture_center"] = list(location)
    return anchor


def aperture_anchor_name(optic_name: str, plane_name: str) -> str:
    if not optic_name.endswith("Optic") or plane_name not in {"Rear", "Front"}:
        raise RuntimeError(
            f"Cannot derive a stable aperture node from {optic_name}/{plane_name}."
        )
    return optic_name.removesuffix("Optic") + plane_name + "ApertureAnchor"


def validate_aperture_anchor_pair(
    variant: bpy.types.Object,
    reticle_anchor: bpy.types.Object,
    rear_anchor: bpy.types.Object,
    front_anchor: bpy.types.Object,
    phase: str,
) -> None:
    if any(
        anchor.parent != variant
        for anchor in (reticle_anchor, rear_anchor, front_anchor)
    ):
        raise RuntimeError(
            f"{variant.name} aperture anchors are not direct children after {phase}."
        )
    expected_rear_name = aperture_anchor_name(variant.name, "Rear")
    expected_front_name = aperture_anchor_name(variant.name, "Front")
    if (
        rear_anchor.name != expected_rear_name
        or rear_anchor.get("runtime_contract_name") != expected_rear_name
    ):
        raise RuntimeError(f"{variant.name} rear aperture contract drifted after {phase}.")
    if (
        front_anchor.name != expected_front_name
        or front_anchor.get("runtime_contract_name") != expected_front_name
    ):
        raise RuntimeError(f"{variant.name} front aperture contract drifted after {phase}.")
    if (
        rear_anchor.get("derived_from_source_plane") != "rear"
        or front_anchor.get("derived_from_source_plane") != "front"
        or not rear_anchor.get("derived_from_removed_source_glass")
        or not front_anchor.get("derived_from_removed_source_glass")
    ):
        raise RuntimeError(
            f"{variant.name} aperture provenance drifted after {phase}."
        )

    separation = front_anchor.location.y - rear_anchor.location.y
    optical_axis_residual = Vector(
        (
            front_anchor.location.x - rear_anchor.location.x,
            front_anchor.location.z - rear_anchor.location.z,
        )
    ).length
    reticle_to_rear = (reticle_anchor.location - rear_anchor.location).length
    if separation <= MINIMUM_APERTURE_SEPARATION:
        raise RuntimeError(
            f"{variant.name} aperture depth collapsed after {phase}: "
            f"separation={separation:.9f}"
        )
    if optical_axis_residual > APERTURE_AXIS_RESIDUAL_TOLERANCE:
        raise RuntimeError(
            f"{variant.name} aperture optical axis drifted after {phase}: "
            f"residual={optical_axis_residual:.9f}"
        )
    if reticle_to_rear > ANCHOR_MATCH_TOLERANCE:
        raise RuntimeError(
            f"{variant.name} reticle left the real rear aperture after {phase}: "
            f"distance={reticle_to_rear:.9f}"
        )
    print(
        "AUTHORED_OPTIC_ANCHORS "
        f"phase={phase} variant={variant.name} "
        f"rear={tuple(round(value, 9) for value in rear_anchor.location)} "
        f"front={tuple(round(value, 9) for value in front_anchor.location)} "
        f"separation={separation:.9f} "
        f"godot_xy_residual={optical_axis_residual:.9f} "
        f"reticle_to_rear={reticle_to_rear:.9f}"
    )


def validate_open_aperture(
    variant: bpy.types.Object,
    geometry: bpy.types.Object,
    anchor: bpy.types.Object,
) -> None:
    if geometry.parent != variant or anchor.parent != variant:
        raise RuntimeError(f"Broken authored optic hierarchy under {variant.name}.")
    if geometry.get("runtime_generated_primitive") is not False:
        raise RuntimeError(f"{geometry.name} is not recorded as source-derived geometry.")
    if geometry.get("removed_source_glass_faces") != 12:
        raise RuntimeError(f"{geometry.name} did not remove its source panes.")
    if len(geometry.data.materials) != 2:
        raise RuntimeError(f"{geometry.name} has an unexpected material contract.")
    if any("glass" in material.name.lower() for material in geometry.data.materials):
        raise RuntimeError(f"{geometry.name} retains a glass material slot.")
    if any(material.diffuse_color[3] < 0.999 for material in geometry.data.materials):
        raise RuntimeError(f"{geometry.name} contains an opaque-cover substitute.")

    vertices = [geometry.matrix_local @ vertex.co for vertex in geometry.data.vertices]
    polygons = [tuple(polygon.vertices) for polygon in geometry.data.polygons]
    aperture = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)
    minimum_y = min(position.y for position in vertices)
    maximum_y = max(position.y for position in vertices)
    center = anchor.location
    hit, _, _, _ = aperture.ray_cast(
        Vector((center.x, minimum_y - 0.01, center.z)),
        Vector((0.0, 1.0, 0.0)),
        maximum_y - minimum_y + 0.02,
    )
    if hit is not None:
        raise RuntimeError(
            f"{variant.name} centerline is blocked after pane removal: {tuple(hit)}"
        )
    print(
        "AUTHORED_OPTIC_APERTURE "
        f"variant={variant.name} glass_faces=0 removed_faces=12 "
        "centerline_clear=True source_derived=True"
    )


def remove_sources(runtime_root: bpy.types.Object) -> None:
    runtime_nodes = {runtime_root, *runtime_root.children_recursive}
    for obj in list(bpy.context.scene.objects):
        if obj not in runtime_nodes:
            bpy.data.objects.remove(obj, do_unlink=True)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def build_runtime_asset() -> bpy.types.Object:
    root = empty("SteelTideAuthoredOptics")
    root["runtime_asset"] = True
    root["source_creator"] = SOURCE_CREATOR
    root["source_url"] = SOURCE_URL
    root["source_license"] = SOURCE_LICENSE
    root["acquisition_date"] = "2026-08-20"
    root["adaptation_date"] = "2026-08-28"
    root["runtime_generated_primitives"] = 0

    built: list[
        tuple[
            bpy.types.Object,
            bpy.types.Object,
            bpy.types.Object,
            bpy.types.Object,
            bpy.types.Object,
        ]
    ] = []
    for spec in OPTICS:
        source = import_source(spec)
        faces = select_scope_component(source, spec)
        rear_plane, front_plane, glass_faces = source_aperture_planes(source, faces)
        deform = build_point_deformer(
            source,
            faces,
            spec,
            rear_plane.source_center,
        )
        variant = empty(spec.node_name, root)
        variant["runtime_asset"] = True
        variant["optic_profile"] = spec.node_name.removesuffix("Optic").lower()
        housing = build_scalar_pbr_material(
            f"{spec.node_name}Housing",
            spec.housing_color,
            0.34,
            0.46,
            spec.source_filename,
        )
        hardware = build_scalar_pbr_material(
            f"{spec.node_name}Hardware",
            spec.hardware_color,
            0.78,
            0.31,
            spec.source_filename,
        )
        geometry = extract_authored_geometry(
            source,
            variant,
            spec,
            faces,
            glass_faces,
            deform,
            housing,
            hardware,
        )
        rear_aperture = deformed_aperture_center(source, rear_plane, deform)
        front_aperture = deformed_aperture_center(source, front_plane, deform)
        anchor_name = spec.node_name.replace("Optic", "ReticleAnchor")
        anchor = empty(anchor_name, variant, rear_aperture)
        anchor["runtime_asset"] = True
        anchor["derived_from_mesh"] = geometry.name
        anchor["derived_from_removed_source_glass"] = True
        anchor["derived_from_surface"] = "rear eyepiece center"
        anchor["authored_aperture_center"] = list(rear_aperture)
        rear_anchor_name = aperture_anchor_name(spec.node_name, "Rear")
        front_anchor_name = aperture_anchor_name(spec.node_name, "Front")
        rear_anchor = add_aperture_anchor(
            rear_anchor_name,
            rear_anchor_name,
            variant,
            geometry,
            spec,
            "rear",
            rear_plane,
            rear_aperture,
        )
        front_anchor = add_aperture_anchor(
            front_anchor_name,
            front_anchor_name,
            variant,
            geometry,
            spec,
            "front",
            front_plane,
            front_aperture,
        )
        variant["aperture_anchor_contract"] = (
            f"{rear_anchor_name},{front_anchor_name}"
        )
        validate_aperture_anchor_pair(
            variant,
            anchor,
            rear_anchor,
            front_anchor,
            "dcc_source",
        )
        validate_open_aperture(variant, geometry, anchor)
        built.append((variant, geometry, anchor, rear_anchor, front_anchor))

    remove_sources(root)
    if len(built) != 3:
        raise RuntimeError(f"Authored optic set is incomplete: {len(built)}/3.")
    mesh_count, vertex_count, triangle_count = mesh_statistics(root)
    if mesh_count != 3 or triangle_count != 1200 or vertex_count < 2200:
        raise RuntimeError(
            "Authored optic geometry regression: "
            f"meshes={mesh_count}/3 vertices={vertex_count} triangles={triangle_count}/1200"
        )
    dimensions = []
    for variant, geometry, _, _, _ in built:
        positions = [geometry.matrix_local @ vertex.co for vertex in geometry.data.vertices]
        minimum = Vector(
            tuple(min(position[axis] for position in positions) for axis in range(3))
        )
        maximum = Vector(
            tuple(max(position[axis] for position in positions) for axis in range(3))
        )
        dimensions.append(maximum - minimum)
    if not (
        dimensions[0].x < dimensions[1].x
        and dimensions[0].y < dimensions[1].y < dimensions[2].y
        and dimensions[2].y > dimensions[0].y * 3.0
    ):
        raise RuntimeError(f"Optic silhouettes are not distinct: {dimensions}")
    return root


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require_output_identity(
    label: str,
    path: Path,
    expected_bytes: int,
    expected_hash: str,
) -> None:
    actual_bytes = path.stat().st_size
    actual_hash = sha256(path)
    if expected_hash and (
        actual_bytes != expected_bytes
        or actual_hash != expected_hash
    ):
        raise RuntimeError(
            f"{label} identity drifted: bytes={actual_bytes}/{expected_bytes} "
            f"sha256={actual_hash}/{expected_hash}"
        )


def canonicalize_png_metadata(path: Path) -> None:
    payload = path.read_bytes()
    signature = b"\x89PNG\r\n\x1a\n"
    if not payload.startswith(signature):
        raise RuntimeError(f"Review render is not a PNG: {path}")
    output = bytearray(signature)
    offset = len(signature)
    while offset + 12 <= len(payload):
        chunk_length = struct.unpack_from(">I", payload, offset)[0]
        chunk_end = offset + 12 + chunk_length
        if chunk_end > len(payload):
            raise RuntimeError(f"Truncated PNG chunk in {path}")
        chunk_type = payload[offset + 4:offset + 8]
        chunk_data = payload[offset + 8:offset + 8 + chunk_length]
        expected_crc = struct.unpack_from(">I", payload, offset + 8 + chunk_length)[0]
        actual_crc = zlib.crc32(chunk_type + chunk_data) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            raise RuntimeError(f"Invalid PNG CRC in {path}: {chunk_type!r}")
        if chunk_type not in {b"tEXt", b"tIME"}:
            output.extend(payload[offset:chunk_end])
        offset = chunk_end
        if chunk_type == b"IEND":
            break
    if offset != len(payload):
        raise RuntimeError(f"Invalid PNG chunk layout: {path}")
    path.write_bytes(output)


def read_glb_document(path: Path) -> tuple[dict, list[tuple[int, bytes]]]:
    payload = path.read_bytes()
    if len(payload) < 20:
        raise RuntimeError(f"GLB is truncated: {path}")
    magic, version, declared_length = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(payload):
        raise RuntimeError(
            f"Invalid GLB header: magic={magic!r} version={version} "
            f"length={declared_length}/{len(payload)}"
        )
    chunks: list[tuple[int, bytes]] = []
    document = None
    offset = 12
    while offset + 8 <= len(payload):
        chunk_length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk = payload[offset:offset + chunk_length]
        offset += chunk_length
        chunks.append((chunk_type, chunk))
        if chunk_type == 0x4E4F534A:
            document = json.loads(chunk.rstrip(b"\x00\x20\t\r\n").decode("utf-8"))
    if offset != len(payload) or document is None:
        raise RuntimeError(f"Invalid GLB chunk layout: {path}")
    return document, chunks


def gltf_translation(node: dict) -> Vector:
    values = node.get("translation", (0.0, 0.0, 0.0))
    if len(values) != 3:
        raise RuntimeError(f"Invalid glTF node translation: {values}")
    return Vector(values)


def blender_to_gltf_translation(position: Vector) -> Vector:
    return Vector((position.x, position.z, -position.y))


def validate_exported_aperture_contract(
    expected_locations: dict[str, tuple[Vector, Vector]],
) -> None:
    document, _ = read_glb_document(OUTPUT_GLB)
    nodes = document.get("nodes", [])
    root_matches = [
        index
        for index, node in enumerate(nodes)
        if node.get("name") == "SteelTideAuthoredOptics"
    ]
    if len(root_matches) != 1:
        raise RuntimeError(
            f"Exported authored optic root count drifted: {len(root_matches)}"
        )
    root_children = set(nodes[root_matches[0]].get("children", []))
    for spec in OPTICS:
        variant_matches = [
            index
            for index, node in enumerate(nodes)
            if node.get("name") == spec.node_name
        ]
        if len(variant_matches) != 1 or variant_matches[0] not in root_children:
            raise RuntimeError(
                f"Exported {spec.node_name} is not a direct child of the runtime root."
            )
        variant = nodes[variant_matches[0]]
        children = [nodes[index] for index in variant.get("children", [])]

        def require_child(name: str) -> dict:
            matches = [child for child in children if child.get("name") == name]
            if len(matches) != 1:
                raise RuntimeError(
                    f"Exported {spec.node_name}/{name} count drifted: {len(matches)}"
                )
            return matches[0]

        require_child(spec.geometry_name)
        reticle = require_child(spec.node_name.replace("Optic", "ReticleAnchor"))
        rear_name = aperture_anchor_name(spec.node_name, "Rear")
        front_name = aperture_anchor_name(spec.node_name, "Front")
        rear = require_child(rear_name)
        front = require_child(front_name)
        if (
            rear.get("extras", {}).get("runtime_contract_name") != rear_name
            or front.get("extras", {}).get("runtime_contract_name") != front_name
        ):
            raise RuntimeError(
                f"Exported {spec.node_name} aperture names are not stable contracts."
            )
        if (
            rear.get("extras", {}).get("derived_from_source_plane") != "rear"
            or front.get("extras", {}).get("derived_from_source_plane") != "front"
        ):
            raise RuntimeError(
                f"Exported {spec.node_name} aperture provenance drifted."
            )
        rear_translation = gltf_translation(rear)
        front_translation = gltf_translation(front)
        reticle_translation = gltf_translation(reticle)
        expected_rear, expected_front = expected_locations[spec.node_name]
        if (
            rear_translation - blender_to_gltf_translation(expected_rear)
        ).length > ANCHOR_MATCH_TOLERANCE or (
            front_translation - blender_to_gltf_translation(expected_front)
        ).length > ANCHOR_MATCH_TOLERANCE:
            raise RuntimeError(
                f"Exported {spec.node_name} aperture transforms drifted."
            )
        godot_xy_residual = Vector(
            (
                front_translation.x - rear_translation.x,
                front_translation.y - rear_translation.y,
            )
        ).length
        separation = rear_translation.z - front_translation.z
        reticle_to_rear = (reticle_translation - rear_translation).length
        if (
            separation <= MINIMUM_APERTURE_SEPARATION
            or godot_xy_residual > APERTURE_AXIS_RESIDUAL_TOLERANCE
            or reticle_to_rear > ANCHOR_MATCH_TOLERANCE
        ):
            raise RuntimeError(
                f"Exported {spec.node_name} optical contract drifted: "
                f"separation={separation:.9f} "
                f"godot_xy_residual={godot_xy_residual:.9f} "
                f"reticle_to_rear={reticle_to_rear:.9f}"
            )
        print(
            "AUTHORED_OPTIC_EXPORTED_ANCHORS "
            f"variant={spec.node_name} separation={separation:.9f} "
            f"godot_xy_residual={godot_xy_residual:.9f} "
            f"reticle_to_rear={reticle_to_rear:.9f}"
        )


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (root, *root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_yup=True,
        export_attributes=True,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
    )


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


def validate_glb_roundtrip(
    expected_locations: dict[str, tuple[Vector, Vector]],
) -> bpy.types.Object:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    root = bpy.data.objects.get("SteelTideAuthoredOptics")
    if root is None:
        raise RuntimeError("Authored optic GLB round trip lost its runtime root.")
    mesh_count, vertex_count, triangle_count = mesh_statistics(root)
    if mesh_count != 3 or triangle_count != 1200 or vertex_count < 2200:
        raise RuntimeError(
            "Authored optic GLB round-trip topology drifted: "
            f"meshes={mesh_count}/3 vertices={vertex_count} triangles={triangle_count}/1200"
        )
    for spec in OPTICS:
        variant = bpy.data.objects.get(spec.node_name)
        geometry = bpy.data.objects.get(spec.geometry_name)
        reticle = bpy.data.objects.get(spec.node_name.replace("Optic", "ReticleAnchor"))
        if variant is None or geometry is None or reticle is None:
            raise RuntimeError(
                f"Authored optic GLB round trip lost {spec.node_name} nodes."
            )

        def require_contract_child(contract_name: str) -> bpy.types.Object:
            matches = [
                child
                for child in variant.children
                if child.name == contract_name
                and child.get("runtime_contract_name") == contract_name
            ]
            if len(matches) != 1:
                raise RuntimeError(
                    f"{spec.node_name}/{contract_name} round-trip count drifted: "
                    f"{len(matches)}"
                )
            return matches[0]

        rear = require_contract_child(aperture_anchor_name(spec.node_name, "Rear"))
        front = require_contract_child(aperture_anchor_name(spec.node_name, "Front"))
        expected_rear, expected_front = expected_locations[spec.node_name]
        if (
            rear.location - expected_rear
        ).length > ROUND_TRIP_TOLERANCE or (
            front.location - expected_front
        ).length > ROUND_TRIP_TOLERANCE:
            raise RuntimeError(
                f"{spec.node_name} aperture anchors moved during GLB round trip: "
                f"rear={tuple(rear.location)} front={tuple(front.location)}"
            )
        validate_aperture_anchor_pair(
            variant,
            reticle,
            rear,
            front,
            "glb_roundtrip",
        )
        validate_open_aperture(variant, geometry, reticle)
    return root


def preview_material(
    name: str,
    color: tuple[float, float, float, float],
    roughness: float,
    emission: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = roughness
        if emission > 0.0:
            principled.inputs["Emission Color"].default_value = color
            principled.inputs["Emission Strength"].default_value = emission
    return material


def add_preview_stage() -> None:
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    variants = [bpy.data.objects[spec.node_name] for spec in OPTICS]
    for variant, x in zip(variants, (-0.30, 0.0, 0.30)):
        variant.location.x = x

    world = bpy.context.scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.008, 0.012, 0.018, 1.0)
        background.inputs["Strength"].default_value = 0.28

    floor_material = preview_material(
        "OpticsPreviewFloor",
        (0.025, 0.032, 0.037, 1.0),
        0.76,
    )
    bpy.ops.mesh.primitive_plane_add(size=4.0, location=(0.0, 0.0, -0.095))
    floor = bpy.context.object
    floor.name = "PreviewOnlyFloor"
    floor.data.materials.append(floor_material)

    backdrop_material = preview_material(
        "OpticsPreviewBackdrop",
        (0.10, 0.58, 0.48, 1.0),
        0.38,
        emission=0.18,
    )
    bpy.ops.mesh.primitive_plane_add(
        size=2.2,
        location=(0.0, 0.52, 0.20),
        rotation=(1.5707963267948966, 0.0, 0.0),
    )
    backdrop = bpy.context.object
    backdrop.name = "PreviewOnlyApertureBackdrop"
    backdrop.data.materials.append(backdrop_material)

    target = Vector((0.0, 0.0, -0.01))
    for name, location, energy, color, size in (
        ("PreviewKey", (1.2, -1.2, 1.25), 650.0, (0.72, 0.88, 1.0), 1.2),
        ("PreviewFill", (-1.2, -0.4, 0.65), 520.0, (0.30, 1.0, 0.66), 1.0),
        ("PreviewRim", (0.0, 1.0, 0.9), 680.0, (1.0, 0.42, 0.18), 0.9),
    ):
        light_data = bpy.data.lights.new(name, "AREA")
        light_data.energy = energy
        light_data.color = color
        light_data.shape = "DISK"
        light_data.size = size
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = Vector((0.66, -1.18, 0.42))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 62.0
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)

    camera.location = Vector((0.0, -1.55, 0.0))
    camera.rotation_euler = (Vector((0.0, 0.32, 0.0)) - camera.location).to_track_quat(
        "-Z",
        "Y",
    ).to_euler()
    camera_data.lens = 60.0
    scene.render.filepath = str(ADS_PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)
    canonicalize_png_metadata(PREVIEW_PATH)
    canonicalize_png_metadata(ADS_PREVIEW_PATH)


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    root = build_runtime_asset()
    expected_nodes = {
        "SteelTideAuthoredOptics",
        "MicroOptic",
        "MicroGeometry",
        "HoloOptic",
        "HoloGeometry",
        "ScopeOptic",
        "ScopeGeometry",
        "MicroReticleAnchor",
        "HoloReticleAnchor",
        "ScopeReticleAnchor",
    }
    expected_nodes.update(
        spec.node_name.replace("Optic", suffix)
        for spec in OPTICS
        for suffix in ("RearApertureAnchor", "FrontApertureAnchor")
    )
    available_nodes = {obj.name for obj in (root, *root.children_recursive)}
    missing = expected_nodes - available_nodes
    if missing:
        raise RuntimeError(f"Authored optic runtime contract missing: {sorted(missing)}")
    for spec in OPTICS:
        variant = bpy.data.objects[spec.node_name]
        geometry = bpy.data.objects[spec.geometry_name]
        anchor_name = spec.node_name.replace("Optic", "ReticleAnchor")
        anchors = [child for child in variant.children if child.name == anchor_name]
        rear = bpy.data.objects[
            spec.node_name.replace("Optic", "RearApertureAnchor")
        ]
        front = bpy.data.objects[
            spec.node_name.replace("Optic", "FrontApertureAnchor")
        ]
        if (
            len(anchors) != 1
            or geometry.parent != variant
            or rear.parent != variant
            or front.parent != variant
        ):
            raise RuntimeError(f"Incomplete runtime hierarchy for {spec.node_name}.")

    mesh_count, vertex_count, triangle_count = mesh_statistics(root)
    expected_locations = {
        spec.node_name: (
            bpy.data.objects[
                spec.node_name.replace("Optic", "RearApertureAnchor")
            ].location.copy(),
            bpy.data.objects[
                spec.node_name.replace("Optic", "FrontApertureAnchor")
            ].location.copy(),
        )
        for spec in OPTICS
    }
    export_asset(root)
    validate_exported_aperture_contract(expected_locations)
    require_output_identity(
        "Authored optics GLB",
        OUTPUT_GLB,
        OUTPUT_GLB_BYTES,
        OUTPUT_GLB_SHA256,
    )
    save_source()
    validate_glb_roundtrip(expected_locations)
    add_preview_stage()
    require_output_identity(
        "Authored optics review preview",
        PREVIEW_PATH,
        OUTPUT_PREVIEW_BYTES,
        OUTPUT_PREVIEW_SHA256,
    )
    require_output_identity(
        "Authored optics ADS preview",
        ADS_PREVIEW_PATH,
        OUTPUT_ADS_PREVIEW_BYTES,
        OUTPUT_ADS_PREVIEW_SHA256,
    )
    print(
        "AUTHORED_OPTICS_EXPORT "
        f"meshes={mesh_count} vertices={vertex_count} triangles={triangle_count} "
        f"glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"glb_sha256={sha256(OUTPUT_GLB)} "
        f"blend_bytes={OUTPUT_BLEND.stat().st_size} "
        f"blend_sha256={sha256(OUTPUT_BLEND)} "
        f"preview_bytes={PREVIEW_PATH.stat().st_size} "
        f"preview_sha256={sha256(PREVIEW_PATH)} "
        f"ads_preview_bytes={ADS_PREVIEW_PATH.stat().st_size} "
        f"ads_preview_sha256={sha256(ADS_PREVIEW_PATH)} "
        f"glb={OUTPUT_GLB} blend={OUTPUT_BLEND} preview={PREVIEW_PATH} "
        f"ads_preview={ADS_PREVIEW_PATH}"
    )


if __name__ == "__main__":
    main()
