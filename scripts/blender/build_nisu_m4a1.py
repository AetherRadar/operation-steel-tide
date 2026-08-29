"""Build the runtime M4A1 from nisu's CC0 authored source asset.

Run from the repository root with Blender 4.5 LTS or newer:

    blender --background --factory-startup --python scripts/blender/build_nisu_m4a1.py

The source FBX uses +Y for the muzzle direction and +Z for up, matching the
project's Blender weapon convention. The glTF exporter converts that to the
Godot -Z-forward, +Y-up convention used by the first-person weapon rig.
"""

from __future__ import annotations

from collections import defaultdict
import hashlib
import json
from math import pi
from pathlib import Path
import struct

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "nisu_m4a1"
SOURCE_FBX = SOURCE_DIR / "M4A1.fbx"
QUATERNIUS_GUN_DIR = REPO_ROOT / "assets" / "models" / "quaternius_ultimate_guns"
FOREGRIP_SOURCE_GLB = QUATERNIUS_GUN_DIR / "scarl.glb"
SUPPRESSOR_SOURCE_GLB = QUATERNIUS_GUN_DIR / "mp5a5.glb"
OPTIC_SOURCE_GLB = QUATERNIUS_GUN_DIR / "axmc.glb"
OUTPUT_GLB = REPO_ROOT / "assets" / "models" / "steel_tide_m4a1" / "steel_tide_m4a1.glb"
OUTPUT_BLEND = REPO_ROOT / "source_art" / "combat_models" / "steel_tide_m4a1.blend"
PREVIEW_PATH = REPO_ROOT / "build" / "art-previews" / "steel_tide_m4a1.png"
ADS_PREVIEW_PATH = (
    REPO_ROOT / "build" / "art-previews" / "steel_tide_m4a1_ads.png"
)

# The source model is authored at a real 0.859 m length. Existing first-person
# weapon transforms expect an authored-space length of about 2.03 m before the
# shared 0.68 presentation scale, yielding a 1.38 m first-person silhouette.
SOURCE_SCALE = 2.36

EXPECTED_SOURCE_MESHES = {
    "Magazine",
    "Base",
    "Charging_Handle",
    "Sight_2",
    "Switch1",
    "Switch2",
    "Sight",
    "Firemode_Selector",
    "Trigger",
    "Ejector_Lid",
    "Ejector_2",
    "Barrel",
    "Stock",
}

MAGAZINE_ORIGIN = Vector((0.0, 0.31, -0.2))
SPARE_MAGAZINE_ORIGIN = Vector((-0.3, 0.18, -0.62))
CHARGING_HANDLE_ORIGIN = Vector((0.075, 0.05, 0.085))
STOCK_ORIGIN = Vector((0.0, -0.49, 0.0))
REAR_IRON_ORIGIN = Vector((0.0, -0.16, 0.15))
FRONT_IRON_ORIGIN = Vector((0.0, 0.78, 0.14))
FOREGRIP_ORIGIN = Vector((0.0, 0.58, -0.17))
MUZZLE_ORIGIN = Vector((0.0, 1.205, 0.015))
SUPPRESSOR_ORIGIN = Vector((0.0, 1.26, 0.015))
OPTIC_ORIGIN = Vector((0.0, 0.25, 0.145))

# Surface queries are expressed in each mechanism parent's Blender-local frame.
# The far-left X seed selects the support-hand side of the real mesh, while Y/Z
# select the lower-middle magazine wall and the rear T-handle wing respectively.
MAGAZINE_GRIP_TARGET = Vector((-1.0, -0.170, -0.060))
CHARGING_HANDLE_GRIP_TARGET = Vector((-1.0, -0.300, 0.014))
EXPECTED_DCC_MESH_COUNT = 19
EXPECTED_DCC_VERTEX_COUNT = 6_797
EXPECTED_GLB_VERTEX_COUNT = 12_414
EXPECTED_TRIANGLE_COUNT = 10_617

FOREGRIP_SOURCE_OBJECT = "AssaultRifle2_1"
SUPPRESSOR_SOURCE_OBJECT = "SubmachineGun_2"
OPTIC_SOURCE_OBJECT = "SniperRifle_3"
FOREGRIP_TARGET_DIMENSIONS = Vector((0.072, 0.13, 0.20))
SUPPRESSOR_TARGET_DIMENSIONS = Vector((0.07, 0.32, 0.07))
OPTIC_TARGET_DIMENSIONS = Vector((0.065, 0.18, 0.075))
FOREGRIP_LOCAL_OFFSET = Vector((0.0, 0.0, 0.0435))
SUPPRESSOR_LOCAL_OFFSET = Vector((0.0, 0.11, 0.0))
OPTIC_LOCAL_OFFSET = Vector((0.0, 0.0, 0.013))

ATTACHMENT_SOURCE_INFO = {
    "Foregrip": (
        "assets/models/quaternius_ultimate_guns/scarl.glb",
        f"{FOREGRIP_SOURCE_OBJECT}:pistol-grip component",
    ),
    "MuzzleDevice": (
        "source_art/third_party/nisu_m4a1/M4A1.fbx",
        "Barrel:muzzle-device components",
    ),
    "Suppressor": (
        "assets/models/quaternius_ultimate_guns/mp5a5.glb",
        f"{SUPPRESSOR_SOURCE_OBJECT}:authored front muzzle assembly",
    ),
    "OpticMount": (
        "assets/models/quaternius_ultimate_guns/axmc.glb",
        f"{OPTIC_SOURCE_OBJECT}:authored scope housing with source glass removed at runtime",
    ),
}


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


def require_file(path: Path) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)


def empty(
    name: str,
    parent: bpy.types.Object | None = None,
    location: Vector | None = None,
) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.08
    obj.parent = parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    if location is not None:
        obj.location = location
    return obj


def load_texture(filename: str, colorspace: str) -> bpy.types.Image:
    path = SOURCE_DIR / filename
    require_file(path)
    image = bpy.data.images.load(str(path), check_existing=False)
    image.name = path.stem
    image.colorspace_settings.name = colorspace
    image["source_creator"] = "nisu"
    image["source_license"] = "CC0-1.0"
    return image


def build_material() -> bpy.types.Material:
    base_color = load_texture("M4A1_Base_Color.png", "sRGB")
    metallic = load_texture("M4A1_Metallic.png", "Non-Color")
    roughness = load_texture("M4A1_Roughness.png", "Non-Color")
    normal = load_texture("M4A1_Normal.png", "Non-Color")
    load_texture("M4A1_Height.png", "Non-Color")

    material = bpy.data.materials.new("NisuM4A1PBR")
    material.use_nodes = True
    material.diffuse_color = (0.115, 0.12, 0.11, 1.0)
    material.metallic = 0.42
    material.roughness = 0.52
    material["source_creator"] = "nisu"
    material["source_url"] = "https://opengameart.org/content/m4a1-assault-rifle"
    material["source_license"] = "CC0-1.0"

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (680, 0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (380, 0)
    principled.inputs["Metallic"].default_value = 0.42
    principled.inputs["Roughness"].default_value = 0.52
    principled.inputs["Specular IOR Level"].default_value = 0.34
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    base_node = nodes.new("ShaderNodeTexImage")
    base_node.name = "M4A1BaseColor"
    base_node.image = base_color
    base_node.location = (-520, 250)
    links.new(base_node.outputs["Color"], principled.inputs["Base Color"])

    metallic_node = nodes.new("ShaderNodeTexImage")
    metallic_node.name = "M4A1Metallic"
    metallic_node.image = metallic
    metallic_node.location = (-520, 40)
    links.new(metallic_node.outputs["Color"], principled.inputs["Metallic"])

    roughness_node = nodes.new("ShaderNodeTexImage")
    roughness_node.name = "M4A1Roughness"
    roughness_node.image = roughness
    roughness_node.location = (-520, -150)
    links.new(roughness_node.outputs["Color"], principled.inputs["Roughness"])

    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.name = "M4A1Normal"
    normal_node.image = normal
    normal_node.location = (-520, -350)
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (-120, -350)
    normal_map.inputs["Strength"].default_value = 0.88
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    return material


def build_scalar_pbr_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    source_file: Path,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    material["source_creator"] = "Quaternius"
    material["source_url"] = "https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F"
    material["source_license"] = "CC0-1.0"
    material["source_file"] = source_file.relative_to(REPO_ROOT).as_posix()

    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Material {name} is missing its Principled BSDF node.")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Specular IOR Level"].default_value = 0.3
    return material


def source_vertex_position(
    source: bpy.types.Object,
    mesh: bpy.types.Mesh,
    vertex_index: int,
) -> Vector:
    return source.matrix_world @ mesh.vertices[vertex_index].co


def face_vertex_indices(mesh: bpy.types.Mesh, face_indices: set[int]) -> set[int]:
    return {
        vertex_index
        for face_index in face_indices
        for vertex_index in mesh.polygons[face_index].vertices
    }


def face_bounds(
    source: bpy.types.Object,
    mesh: bpy.types.Mesh,
    face_indices: set[int],
) -> tuple[Vector, Vector]:
    if not face_indices:
        raise RuntimeError(f"No source faces selected from {source.name}.")
    positions = [
        source_vertex_position(source, mesh, vertex_index)
        for vertex_index in face_vertex_indices(mesh, face_indices)
    ]
    minimum = Vector(tuple(min(position[axis] for position in positions) for axis in range(3)))
    maximum = Vector(tuple(max(position[axis] for position in positions) for axis in range(3)))
    return minimum, maximum


def source_face_statistics(
    source: bpy.types.Object,
    face_indices: set[int],
) -> tuple[int, int]:
    mesh = source.data
    vertex_count = len(face_vertex_indices(mesh, face_indices))
    triangle_count = sum(
        len(mesh.polygons[face_index].vertices) - 2
        for face_index in face_indices
    )
    return vertex_count, triangle_count


def require_source_face_statistics(
    label: str,
    source: bpy.types.Object,
    face_indices: set[int],
    expected_vertices: int,
    expected_triangles: int,
) -> None:
    vertex_count, triangle_count = source_face_statistics(source, face_indices)
    if (vertex_count, triangle_count) != (expected_vertices, expected_triangles):
        raise RuntimeError(
            f"Unexpected {label} source geometry: "
            f"vertices={vertex_count}/{expected_vertices} "
            f"triangles={triangle_count}/{expected_triangles}"
        )


def position_connected_face_components(source: bpy.types.Object) -> list[set[int]]:
    mesh = source.data
    position_faces: dict[tuple[float, float, float], set[int]] = defaultdict(set)
    face_positions: list[set[tuple[float, float, float]]] = []
    for polygon in mesh.polygons:
        keys = {
            tuple(
                round(value, 5)
                for value in source_vertex_position(source, mesh, vertex_index)
            )
            for vertex_index in polygon.vertices
        }
        face_positions.append(keys)
        for key in keys:
            position_faces[key].add(polygon.index)

    unseen = set(range(len(mesh.polygons)))
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


def select_face_components(
    source: bpy.types.Object,
    predicate,
    expected_component_count: int,
) -> set[int]:
    matches: list[set[int]] = []
    for component in position_connected_face_components(source):
        minimum, maximum = face_bounds(source, source.data, component)
        if predicate(minimum, maximum):
            matches.append(component)
    if len(matches) != expected_component_count:
        raise RuntimeError(
            f"Unexpected authored component selection for {source.name}: "
            f"expected={expected_component_count} actual={len(matches)}"
        )
    return set().union(*matches)


def fitted_component_transform(
    source: bpy.types.Object,
    face_indices: set[int],
    orientation: Matrix,
    target_dimensions: Vector,
    local_offset: Vector = Vector((0.0, 0.0, 0.0)),
) -> Matrix:
    positions = [
        orientation @ source_vertex_position(source, source.data, vertex_index)
        for vertex_index in face_vertex_indices(source.data, face_indices)
    ]
    minimum = Vector(tuple(min(position[axis] for position in positions) for axis in range(3)))
    maximum = Vector(tuple(max(position[axis] for position in positions) for axis in range(3)))
    dimensions = maximum - minimum
    if min(dimensions) <= 1.0e-8:
        raise RuntimeError(f"Cannot fit degenerate authored component from {source.name}.")
    scale = Vector(
        tuple(target_dimensions[axis] / dimensions[axis] for axis in range(3))
    )
    fit = Matrix.Diagonal((scale.x, scale.y, scale.z, 1.0))
    center = (minimum + maximum) * 0.5
    return (
        Matrix.Translation(local_offset)
        @ fit
        @ Matrix.Translation(-center)
        @ orientation
        @ source.matrix_world
    )


def mesh_copy_from_faces(
    source: bpy.types.Object,
    name: str,
    parent: bpy.types.Object,
    face_indices: set[int],
    transform: Matrix,
    materials: tuple[bpy.types.Material, ...],
    material_remap: dict[int, int],
    require_uv: bool = True,
) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    if len(mesh.polygons) != len(source.data.polygons):
        raise RuntimeError(
            f"Evaluated topology changed for {source.name}: "
            f"source={len(source.data.polygons)} evaluated={len(mesh.polygons)}"
        )
    if require_uv and not mesh.uv_layers:
        raise RuntimeError(f"Source mesh {source.name} has no UV map.")

    topology = bmesh.new()
    topology.from_mesh(mesh)
    topology.faces.ensure_lookup_table()
    rejected_faces = [
        face for face in topology.faces if face.index not in face_indices
    ]
    if rejected_faces:
        bmesh.ops.delete(topology, geom=rejected_faces, context="FACES")
    loose_vertices = [vertex for vertex in topology.verts if not vertex.link_faces]
    if loose_vertices:
        bmesh.ops.delete(topology, geom=loose_vertices, context="VERTS")
    topology.to_mesh(mesh)
    topology.free()

    mesh.transform(transform)
    topology = bmesh.new()
    topology.from_mesh(mesh)
    degenerate_faces = [face for face in topology.faces if face.calc_area() <= 1.0e-12]
    if degenerate_faces:
        bmesh.ops.delete(topology, geom=degenerate_faces, context="FACES")
        loose_vertices = [vertex for vertex in topology.verts if not vertex.link_faces]
        if loose_vertices:
            bmesh.ops.delete(topology, geom=loose_vertices, context="VERTS")
        topology.to_mesh(mesh)
    topology.free()

    remapped_material_indices = [
        material_remap.get(polygon.material_index, 0)
        for polygon in mesh.polygons
    ]
    mesh.materials.clear()
    for material in materials:
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, remapped_material_indices):
        polygon.material_index = material_index
    mesh.name = f"{name}Mesh"
    mesh.validate(clean_customdata=False)
    mesh.update()
    mesh.calc_loop_triangles()
    if not mesh.vertices or not mesh.loop_triangles:
        raise RuntimeError(f"Authored component {name} has no renderable triangles.")

    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    result.parent = parent
    result.matrix_parent_inverse = Matrix.Identity(4)
    result.location = Vector((0.0, 0.0, 0.0))
    result.rotation_euler = Vector((0.0, 0.0, 0.0))
    result.scale = Vector((1.0, 1.0, 1.0))
    return result


def authored_tip_location(geometry: bpy.types.Object) -> Vector:
    if geometry.type != "MESH":
        raise RuntimeError(f"Cannot derive authored tip from non-mesh {geometry.name}.")
    bpy.context.view_layer.update()
    positions = [geometry.matrix_local @ vertex.co for vertex in geometry.data.vertices]
    minimum = Vector(tuple(min(position[axis] for position in positions) for axis in range(3)))
    maximum = Vector(tuple(max(position[axis] for position in positions) for axis in range(3)))
    return Vector(
        (
            (minimum.x + maximum.x) * 0.5,
            maximum.y,
            (minimum.z + maximum.z) * 0.5,
        )
    )


def add_authored_tip_marker(
    name: str,
    parent: bpy.types.Object,
    geometry: bpy.types.Object,
) -> bpy.types.Object:
    if geometry.parent != parent:
        raise RuntimeError(
            f"Cannot derive {name} from geometry outside {parent.name}."
        )
    marker = empty(
        name,
        parent,
        authored_tip_location(geometry),
    )
    marker["runtime_asset"] = True
    marker["derived_from_mesh"] = geometry.name
    marker["derived_from_bound"] = "+Y"
    return marker


def authored_optic_reticle_location(
    geometry: bpy.types.Object,
    glass_material_index: int,
) -> Vector:
    if geometry.type != "MESH":
        raise RuntimeError("Cannot derive optic reticle anchor from non-mesh geometry.")
    bpy.context.view_layer.update()
    glass_vertices = {
        vertex_index
        for polygon in geometry.data.polygons
        if polygon.material_index == glass_material_index
        for vertex_index in polygon.vertices
    }
    if len(glass_vertices) < 3:
        raise RuntimeError("Compact optic has no authored glass surface.")
    glass_positions = [
        geometry.matrix_local @ geometry.data.vertices[vertex_index].co
        for vertex_index in glass_vertices
    ]
    eyepiece_y = min(position.y for position in glass_positions)
    eyepiece_positions = [
        position
        for position in glass_positions
        if abs(position.y - eyepiece_y) <= 1.0e-5
    ]
    if len(eyepiece_positions) < 3:
        raise RuntimeError("Compact optic eyepiece glass is not a stable planar surface.")
    minimum = Vector(
        tuple(min(position[axis] for position in eyepiece_positions) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(position[axis] for position in eyepiece_positions) for axis in range(3))
    )
    return Vector(
        (
            (minimum.x + maximum.x) * 0.5,
            eyepiece_y,
            (minimum.z + maximum.z) * 0.5,
        )
    )


def add_optic_reticle_anchor(
    parent: bpy.types.Object,
    geometry: bpy.types.Object,
    glass_material_index: int,
) -> bpy.types.Object:
    if geometry.parent != parent:
        raise RuntimeError(
            f"Cannot derive optic reticle anchor outside {parent.name}."
        )
    authored_glass_center = authored_optic_reticle_location(
        geometry,
        glass_material_index,
    )
    marker = empty("OpticReticleAnchor", parent, authored_glass_center)
    marker["runtime_asset"] = True
    marker["derived_from_mesh"] = geometry.name
    marker["derived_from_material"] = geometry.data.materials[glass_material_index].name
    marker["derived_from_surface"] = "-Y source eyepiece glass before pane removal"
    marker["authored_glass_center"] = list(authored_glass_center)
    return marker


def remove_authored_optic_glass(
    geometry: bpy.types.Object,
    glass_material_index: int,
) -> None:
    if geometry.type != "MESH":
        raise RuntimeError("Cannot open an optic aperture on non-mesh geometry.")
    mesh = geometry.data
    if glass_material_index >= len(mesh.materials):
        raise RuntimeError("Compact optic glass material slot is missing.")
    glass_material_name = mesh.materials[glass_material_index].name
    glass_polygons = [
        polygon
        for polygon in mesh.polygons
        if polygon.material_index == glass_material_index
    ]
    glass_vertices = {
        vertex_index
        for polygon in glass_polygons
        for vertex_index in polygon.vertices
    }
    glass_triangles = sum(len(polygon.vertices) - 2 for polygon in glass_polygons)
    if (len(glass_polygons), len(glass_vertices), glass_triangles) != (12, 16, 12):
        raise RuntimeError(
            "Unexpected compact optic source-glass topology: "
            f"faces={len(glass_polygons)}/12 "
            f"vertices={len(glass_vertices)}/16 triangles={glass_triangles}/12"
        )

    topology = bmesh.new()
    topology.from_mesh(mesh)
    glass_faces = [
        face
        for face in topology.faces
        if face.material_index == glass_material_index
    ]
    bmesh.ops.delete(topology, geom=glass_faces, context="FACES")
    loose_vertices = [vertex for vertex in topology.verts if not vertex.link_faces]
    if loose_vertices:
        bmesh.ops.delete(topology, geom=loose_vertices, context="VERTS")
    topology.to_mesh(mesh)
    topology.free()
    mesh.materials.pop(index=glass_material_index)
    mesh.validate(clean_customdata=False)
    mesh.update()
    mesh.calc_loop_triangles()

    geometry["removed_source_glass_material"] = glass_material_name
    geometry["removed_source_glass_faces"] = len(glass_polygons)
    geometry["removed_source_glass_vertices"] = len(glass_vertices)
    geometry["removed_source_glass_triangles"] = glass_triangles


def validate_open_optic_aperture() -> None:
    geometry = bpy.data.objects["OpticMountGeometry"]
    reticle_anchor = bpy.data.objects["OpticReticleAnchor"]
    if geometry.parent != reticle_anchor.parent:
        raise RuntimeError("Optic geometry and reticle anchor no longer share a parent.")
    if any("Glass" in material.name for material in geometry.data.materials):
        raise RuntimeError("Runtime optic still contains a glass material slot.")
    if geometry.get("removed_source_glass_faces") != 12:
        raise RuntimeError("Runtime optic did not remove both authored glass panes.")

    vertices = [
        geometry.matrix_local @ vertex.co
        for vertex in geometry.data.vertices
    ]
    polygons = [tuple(polygon.vertices) for polygon in geometry.data.polygons]
    aperture = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)
    center = reticle_anchor.location
    bounds_min_y = min(position.y for position in vertices)
    bounds_max_y = max(position.y for position in vertices)
    ray_origin = Vector((center.x, bounds_min_y - 0.01, center.z))
    hit_location, _, _, _ = aperture.ray_cast(
        ray_origin,
        Vector((0.0, 1.0, 0.0)),
        bounds_max_y - bounds_min_y + 0.02,
    )
    if hit_location is not None:
        raise RuntimeError(
            "Runtime optic centerline remains blocked after glass removal: "
            f"hit={tuple(hit_location)}"
        )
    print(
        "M4A1_OPTIC_APERTURE "
        "glass_faces=0 removed_faces=12 removed_triangles=12 "
        "centerline_clear=True"
    )


def evaluated_mesh_copy(
    source: bpy.types.Object,
    name: str,
    parent: bpy.types.Object,
    material: bpy.types.Material,
    local_origin: Vector = Vector((0.0, 0.0, 0.0)),
) -> bpy.types.Object:
    transform = (
        Matrix.Translation(-local_origin)
        @ Matrix.Scale(SOURCE_SCALE, 4)
        @ source.matrix_world
    )
    return mesh_copy_from_faces(
        source,
        name,
        parent,
        set(range(len(source.data.polygons))),
        transform,
        (material,),
        {index: 0 for index in range(len(source.data.materials))},
    )


def mesh_surface_contact_in_parent(
    geometry: bpy.types.Object,
    target_in_parent: Vector,
) -> tuple[Vector, Vector, int]:
    if geometry.type != "MESH" or geometry.parent is None:
        raise RuntimeError(f"Cannot derive mechanism contact from {geometry.name}.")
    bpy.context.view_layer.update()
    vertices = [
        geometry.matrix_local @ vertex.co
        for vertex in geometry.data.vertices
    ]
    surface = BVHTree.FromPolygons(
        vertices,
        [tuple(polygon.vertices) for polygon in geometry.data.polygons],
        all_triangles=False,
    )
    location, normal, face_index, _ = surface.find_nearest(target_in_parent)
    if location is None or normal is None or face_index is None:
        raise RuntimeError(f"No surface contact found on {geometry.name}.")
    return location, normal, face_index


def mesh_surface_distance_in_parent(
    geometry: bpy.types.Object,
    point_in_parent: Vector,
) -> float:
    location, _, _ = mesh_surface_contact_in_parent(geometry, point_in_parent)
    return (point_in_parent - location).length


def add_surface_socket(
    name: str,
    parent: bpy.types.Object,
    geometry: bpy.types.Object,
    target_in_parent: Vector,
    role: str,
) -> bpy.types.Object:
    if geometry.parent != parent:
        raise RuntimeError(f"{geometry.name} is outside mechanism node {parent.name}.")
    contact, normal, face_index = mesh_surface_contact_in_parent(
        geometry,
        target_in_parent,
    )
    if normal.x > -0.95:
        raise RuntimeError(
            f"{name} left the support-hand side of {geometry.name}: "
            f"contact={tuple(contact)} normal={tuple(normal)} face={face_index}"
        )
    socket = empty(name, parent, contact)
    socket["runtime_asset"] = True
    socket["socket_role"] = role
    socket["derived_from_mesh"] = geometry.name
    socket["derived_from_face"] = face_index
    socket["surface_target_in_parent"] = tuple(target_in_parent)
    return socket


def import_source() -> dict[str, bpy.types.Object]:
    require_file(SOURCE_FBX)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX), use_anim=False)
    bpy.context.view_layer.update()
    meshes = {
        obj.name: obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH"
    }
    missing = EXPECTED_SOURCE_MESHES - set(meshes)
    unexpected = set(meshes) - EXPECTED_SOURCE_MESHES
    if missing or unexpected:
        raise RuntimeError(
            f"Unexpected nisu M4A1 source layout: missing={sorted(missing)} "
            f"unexpected={sorted(unexpected)}"
        )
    return meshes


def import_quaternius_mesh(
    path: Path,
    expected_object_name: str,
    expected_material_names: tuple[str, ...],
) -> bpy.types.Object:
    require_file(path)
    existing_objects = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()
    imported_meshes = [
        obj
        for obj in bpy.data.objects
        if obj not in existing_objects and obj.type == "MESH"
    ]
    if len(imported_meshes) != 1 or imported_meshes[0].name != expected_object_name:
        raise RuntimeError(
            f"Unexpected Quaternius source layout in {path}: "
            f"meshes={[obj.name for obj in imported_meshes]}"
        )
    source = imported_meshes[0]
    material_names = tuple(
        material.name.rsplit(".", 1)[0]
        if material.name.rsplit(".", 1)[-1].isdigit()
        else material.name
        for material in source.data.materials
    )
    if material_names != expected_material_names:
        raise RuntimeError(
            f"Unexpected Quaternius materials in {path}: "
            f"expected={expected_material_names} actual={material_names}"
        )
    return source


def remove_imported_objects() -> None:
    for obj in list(bpy.context.scene.objects):
        if obj.get("runtime_asset"):
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def build_runtime_asset() -> bpy.types.Object:
    source_meshes = import_source()
    material = build_material()
    foregrip_source = import_quaternius_mesh(
        FOREGRIP_SOURCE_GLB,
        FOREGRIP_SOURCE_OBJECT,
        ("Main", "MainDark", "MainLight"),
    )
    suppressor_source = import_quaternius_mesh(
        SUPPRESSOR_SOURCE_GLB,
        SUPPRESSOR_SOURCE_OBJECT,
        ("DarkMetal", "Metal", "Black", "Grey"),
    )
    optic_source = import_quaternius_mesh(
        OPTIC_SOURCE_GLB,
        OPTIC_SOURCE_OBJECT,
        ("Black", "DarkMetal", "Glass", "Green", "Grey"),
    )
    grip_material = build_scalar_pbr_material(
        "QuaterniusGripPolymer",
        (0.008, 0.011, 0.012, 1.0),
        0.0,
        0.72,
        FOREGRIP_SOURCE_GLB,
    )
    grip_inset_material = build_scalar_pbr_material(
        "QuaterniusGripInset",
        (0.004, 0.006, 0.007, 1.0),
        0.0,
        0.78,
        FOREGRIP_SOURCE_GLB,
    )
    suppressor_material = build_scalar_pbr_material(
        "QuaterniusSuppressorMetal",
        (0.035, 0.042, 0.045, 1.0),
        0.74,
        0.34,
        SUPPRESSOR_SOURCE_GLB,
    )
    optic_housing_material = build_scalar_pbr_material(
        "QuaterniusCompactOpticHousing",
        (0.008, 0.011, 0.013, 1.0),
        0.52,
        0.4,
        OPTIC_SOURCE_GLB,
    )
    optic_hardware_material = build_scalar_pbr_material(
        "QuaterniusCompactOpticHardware",
        (0.028, 0.032, 0.034, 1.0),
        0.78,
        0.3,
        OPTIC_SOURCE_GLB,
    )
    optic_source_glass_material = build_scalar_pbr_material(
        "QuaterniusSourceGlassSelection",
        (0.0, 0.0, 0.0, 0.0),
        0.0,
        0.0,
        OPTIC_SOURCE_GLB,
    )

    muzzle_faces = select_face_components(
        source_meshes["Barrel"],
        lambda minimum, maximum: minimum.y >= 0.501,
        expected_component_count=2,
    )
    require_source_face_statistics(
        "MuzzleDevice",
        source_meshes["Barrel"],
        muzzle_faces,
        expected_vertices=123,
        expected_triangles=250,
    )
    front_iron_faces = select_face_components(
        source_meshes["Barrel"],
        lambda minimum, maximum: (
            minimum.y >= 0.31
            and maximum.y <= 0.356
            and maximum.z >= 0.072
        ),
        expected_component_count=3,
    )
    require_source_face_statistics(
        "FrontIronSight",
        source_meshes["Barrel"],
        front_iron_faces,
        expected_vertices=236,
        expected_triangles=454,
    )
    barrel_body_faces = (
        set(range(len(source_meshes["Barrel"].data.polygons)))
        - muzzle_faces
        - front_iron_faces
    )

    foregrip_faces = select_face_components(
        foregrip_source,
        lambda minimum, maximum: (
            minimum.x > -0.33
            and maximum.x < 0.25
            and minimum.z < -0.29
            and maximum.z > 0.32
        ),
        expected_component_count=1,
    )
    require_source_face_statistics(
        "Foregrip",
        foregrip_source,
        foregrip_faces,
        expected_vertices=232,
        expected_triangles=124,
    )

    suppressor_faces = select_face_components(
        suppressor_source,
        lambda minimum, maximum: (
            minimum.x > 1.92
            and maximum.x > 2.24
            and (maximum - minimum).x < 0.33
        ),
        expected_component_count=1,
    )
    require_source_face_statistics(
        "Suppressor",
        suppressor_source,
        suppressor_faces,
        expected_vertices=278,
        expected_triangles=148,
    )

    optic_faces = select_face_components(
        optic_source,
        lambda minimum, maximum: (
            minimum.x < 0.07
            and maximum.x > 2.12
            and minimum.z > 0.44
            and maximum.z > 0.81
        ),
        expected_component_count=1,
    )
    require_source_face_statistics(
        "OpticMount",
        optic_source,
        optic_faces,
        expected_vertices=796,
        expected_triangles=412,
    )

    root = empty("SteelTideM4A1")
    root["runtime_asset"] = True
    root["source_creator"] = "nisu"
    root["source_url"] = "https://opengameart.org/content/m4a1-assault-rifle"
    root["source_license"] = "CC0-1.0"
    root["attachment_source_creator"] = "Quaternius"
    root["attachment_source_url"] = (
        "https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F"
    )
    root["attachment_source_license"] = "CC0-1.0"

    mechanism_sources = {"Magazine", "Charging_Handle", "Stock"}
    rear_iron_sources = {"Sight", "Sight_2"}
    for index, source_name in enumerate(
        sorted(EXPECTED_SOURCE_MESHES - mechanism_sources - rear_iron_sources)
    ):
        if source_name == "Barrel":
            body = mesh_copy_from_faces(
                source_meshes[source_name],
                f"M4A1Body_{index:02d}_{source_name}",
                root,
                barrel_body_faces,
                Matrix.Scale(SOURCE_SCALE, 4) @ source_meshes[source_name].matrix_world,
                (material,),
                {
                    material_index: 0
                    for material_index in range(
                        len(source_meshes[source_name].data.materials)
                    )
                },
            )
        else:
            body = evaluated_mesh_copy(
                source_meshes[source_name],
                f"M4A1Body_{index:02d}_{source_name}",
                root,
                material,
            )
        body["runtime_asset"] = True

    magazine = empty("Magazine", root, MAGAZINE_ORIGIN)
    magazine["runtime_asset"] = True
    magazine_geometry = evaluated_mesh_copy(
        source_meshes["Magazine"],
        "MagazineGeometry",
        magazine,
        material,
        MAGAZINE_ORIGIN,
    )
    magazine_geometry["runtime_asset"] = True
    add_surface_socket(
        "MagazineGripSocket",
        magazine,
        magazine_geometry,
        MAGAZINE_GRIP_TARGET,
        "magazine_hand_contact",
    )

    spare_magazine = empty("SpareMagazine", root, SPARE_MAGAZINE_ORIGIN)
    spare_magazine["runtime_asset"] = True
    spare_geometry = evaluated_mesh_copy(
        source_meshes["Magazine"],
        "SpareMagazineGeometry",
        spare_magazine,
        material,
        MAGAZINE_ORIGIN,
    )
    spare_geometry["runtime_asset"] = True

    charging_handle = empty("ChargingHandle", root, CHARGING_HANDLE_ORIGIN)
    charging_handle["runtime_asset"] = True
    charging_geometry = evaluated_mesh_copy(
        source_meshes["Charging_Handle"],
        "ChargingHandleGeometry",
        charging_handle,
        material,
        CHARGING_HANDLE_ORIGIN,
    )
    charging_geometry["runtime_asset"] = True
    add_surface_socket(
        "ChargingHandleSocket",
        charging_handle,
        charging_geometry,
        CHARGING_HANDLE_GRIP_TARGET,
        "action_hand_contact",
    )

    stock = empty("Stock", root, STOCK_ORIGIN)
    stock["runtime_asset"] = True
    stock_geometry = evaluated_mesh_copy(
        source_meshes["Stock"],
        "StockGeometry",
        stock,
        material,
        STOCK_ORIGIN,
    )
    stock_geometry["runtime_asset"] = True

    rear_iron_sight = empty("RearIronSight", root, REAR_IRON_ORIGIN)
    rear_iron_sight["runtime_asset"] = True
    rear_iron_sight["source_creator"] = "nisu"
    rear_iron_sight["source_file"] = SOURCE_FBX.relative_to(REPO_ROOT).as_posix()
    rear_iron_sight["source_objects"] = "Sight,Sight_2"
    rear_iron_sight["source_license"] = "CC0-1.0"
    for source_name in sorted(rear_iron_sources):
        rear_geometry = evaluated_mesh_copy(
            source_meshes[source_name],
            f"RearIronSightGeometry_{source_name}",
            rear_iron_sight,
            material,
            REAR_IRON_ORIGIN,
        )
        rear_geometry["runtime_asset"] = True

    front_iron_sight = empty("FrontIronSight", root, FRONT_IRON_ORIGIN)
    front_iron_sight["runtime_asset"] = True
    front_iron_sight["source_creator"] = "nisu"
    front_iron_sight["source_file"] = SOURCE_FBX.relative_to(REPO_ROOT).as_posix()
    front_iron_sight["source_object"] = "Barrel"
    front_iron_sight["source_selection"] = "authored front-sight components"
    front_iron_sight["source_license"] = "CC0-1.0"
    front_iron_geometry = mesh_copy_from_faces(
        source_meshes["Barrel"],
        "FrontIronSightGeometry",
        front_iron_sight,
        front_iron_faces,
        Matrix.Translation(-FRONT_IRON_ORIGIN)
        @ Matrix.Scale(SOURCE_SCALE, 4)
        @ source_meshes["Barrel"].matrix_world,
        (material,),
        {
            material_index: 0
            for material_index in range(len(source_meshes["Barrel"].data.materials))
        },
    )
    front_iron_geometry["runtime_asset"] = True

    foregrip = empty("Foregrip", root, FOREGRIP_ORIGIN)
    muzzle_device = empty("MuzzleDevice", root, MUZZLE_ORIGIN)
    suppressor = empty("Suppressor", root, SUPPRESSOR_ORIGIN)
    optic_mount = empty("OpticMount", root, OPTIC_ORIGIN)
    for marker in (foregrip, muzzle_device, suppressor, optic_mount):
        marker["runtime_asset"] = True

    foregrip_geometry = mesh_copy_from_faces(
        foregrip_source,
        "ForegripGeometry",
        foregrip,
        foregrip_faces,
        fitted_component_transform(
            foregrip_source,
            foregrip_faces,
            Matrix.Rotation(pi * 0.5, 4, "Z"),
            FOREGRIP_TARGET_DIMENSIONS,
            FOREGRIP_LOCAL_OFFSET,
        ),
        (grip_material, grip_inset_material),
        {0: 0, 1: 1, 2: 0},
        require_uv=False,
    )
    foregrip_geometry["runtime_asset"] = True
    foregrip_geometry["source_creator"] = "Quaternius"
    foregrip_geometry["source_file"] = FOREGRIP_SOURCE_GLB.relative_to(REPO_ROOT).as_posix()
    foregrip_geometry["source_object"] = FOREGRIP_SOURCE_OBJECT
    foregrip_geometry["source_selection"] = "authored pistol-grip component adapted as foregrip"
    foregrip_geometry["source_license"] = "CC0-1.0"

    muzzle_geometry = mesh_copy_from_faces(
        source_meshes["Barrel"],
        "MuzzleDeviceGeometry",
        muzzle_device,
        muzzle_faces,
        Matrix.Translation(-MUZZLE_ORIGIN)
        @ Matrix.Scale(SOURCE_SCALE, 4)
        @ source_meshes["Barrel"].matrix_world,
        (material,),
        {
            material_index: 0
            for material_index in range(len(source_meshes["Barrel"].data.materials))
        },
    )
    muzzle_geometry["runtime_asset"] = True
    muzzle_geometry["source_creator"] = "nisu"
    muzzle_geometry["source_file"] = SOURCE_FBX.relative_to(REPO_ROOT).as_posix()
    muzzle_geometry["source_object"] = "Barrel"
    muzzle_geometry["source_selection"] = "authored muzzle-device components"
    muzzle_geometry["source_license"] = "CC0-1.0"
    muzzle_tip = add_authored_tip_marker(
        "MuzzleDeviceTip",
        muzzle_device,
        muzzle_geometry,
    )

    suppressor_geometry = mesh_copy_from_faces(
        suppressor_source,
        "SuppressorGeometry",
        suppressor,
        suppressor_faces,
        fitted_component_transform(
            suppressor_source,
            suppressor_faces,
            Matrix.Rotation(pi * 0.5, 4, "Z"),
            SUPPRESSOR_TARGET_DIMENSIONS,
            SUPPRESSOR_LOCAL_OFFSET,
        ),
        (suppressor_material,),
        {
            material_index: 0
            for material_index in range(len(suppressor_source.data.materials))
        },
        require_uv=False,
    )
    suppressor_geometry["runtime_asset"] = True
    suppressor_geometry["source_creator"] = "Quaternius"
    suppressor_geometry["source_file"] = SUPPRESSOR_SOURCE_GLB.relative_to(REPO_ROOT).as_posix()
    suppressor_geometry["source_object"] = SUPPRESSOR_SOURCE_OBJECT
    suppressor_geometry["source_selection"] = (
        "authored MP5 front muzzle assembly adapted as suppressor"
    )
    suppressor_geometry["source_license"] = "CC0-1.0"
    suppressor_tip = add_authored_tip_marker(
        "SuppressorTip",
        suppressor,
        suppressor_geometry,
    )

    optic_geometry = mesh_copy_from_faces(
        optic_source,
        "OpticMountGeometry",
        optic_mount,
        optic_faces,
        fitted_component_transform(
            optic_source,
            optic_faces,
            Matrix.Rotation(pi * 0.5, 4, "Z"),
            OPTIC_TARGET_DIMENSIONS,
            OPTIC_LOCAL_OFFSET,
        ),
        (
            optic_housing_material,
            optic_hardware_material,
            optic_source_glass_material,
        ),
        {0: 0, 1: 1, 2: 2, 3: 0, 4: 1},
        require_uv=False,
    )
    optic_geometry["runtime_asset"] = True
    optic_geometry["source_creator"] = "Quaternius"
    optic_geometry["source_file"] = OPTIC_SOURCE_GLB.relative_to(REPO_ROOT).as_posix()
    optic_geometry["source_object"] = OPTIC_SOURCE_OBJECT
    optic_geometry["source_selection"] = (
        "authored scope housing; source glass used for anchor then removed"
    )
    optic_geometry["source_license"] = "CC0-1.0"
    optic_reticle_anchor = add_optic_reticle_anchor(
        optic_mount,
        optic_geometry,
        glass_material_index=2,
    )
    remove_authored_optic_glass(optic_geometry, glass_material_index=2)

    remove_imported_objects()
    magazine.name = "Magazine"
    spare_magazine.name = "SpareMagazine"
    charging_handle.name = "ChargingHandle"
    stock.name = "Stock"
    rear_iron_sight.name = "RearIronSight"
    front_iron_sight.name = "FrontIronSight"
    foregrip.name = "Foregrip"
    muzzle_device.name = "MuzzleDevice"
    suppressor.name = "Suppressor"
    optic_mount.name = "OpticMount"
    muzzle_tip.name = "MuzzleDeviceTip"
    suppressor_tip.name = "SuppressorTip"
    optic_reticle_anchor.name = "OpticReticleAnchor"
    bpy.context.view_layer.update()
    return root


def mesh_statistics(root: bpy.types.Object) -> tuple[int, int, int]:
    meshes = [obj for obj in root.children_recursive if obj.type == "MESH"]
    vertex_count = sum(len(obj.data.vertices) for obj in meshes)
    triangle_count = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangle_count += len(obj.data.loop_triangles)
    return len(meshes), vertex_count, triangle_count


def validate_attachment_geometry() -> dict[str, tuple[int, int, int]]:
    statistics: dict[str, tuple[int, int, int]] = {}
    for name in ("Foregrip", "MuzzleDevice", "Suppressor", "OpticMount"):
        node = bpy.data.objects[name]
        mesh_count, vertex_count, triangle_count = mesh_statistics(node)
        if mesh_count < 1 or vertex_count < 3 or triangle_count < 1:
            raise RuntimeError(
                f"Runtime attachment node {name} has no non-empty triangle mesh descendant: "
                f"meshes={mesh_count} vertices={vertex_count} triangles={triangle_count}"
            )
        source_file, source_object = ATTACHMENT_SOURCE_INFO[name]
        print(
            "M4A1_ATTACHMENT_GEOMETRY "
            f"node={name} meshes={mesh_count} vertices={vertex_count} "
            f"triangles={triangle_count} source={source_file} object={source_object}"
        )
        statistics[name] = (mesh_count, vertex_count, triangle_count)
    return statistics


def validate_authored_markers() -> None:
    bpy.context.view_layer.update()
    for name, parent_name in (
        ("MuzzleDeviceTip", "MuzzleDevice"),
        ("SuppressorTip", "Suppressor"),
    ):
        marker = bpy.data.objects[name]
        parent = bpy.data.objects[parent_name]
        if marker.parent != parent:
            raise RuntimeError(f"{name} is not a direct child of {parent_name}.")
        geometry = bpy.data.objects[marker["derived_from_mesh"]]
        expected_location = authored_tip_location(geometry)
        if (marker.location - expected_location).length > 1.0e-8:
            raise RuntimeError(
                f"{name} no longer matches the authored +Y mesh bound: "
                f"actual={tuple(marker.location)} expected={tuple(expected_location)}"
            )
        global_location = marker.matrix_world.translation
        parent_global = parent.matrix_world.translation
        if marker.location.y <= 0.0 or global_location.y <= parent_global.y:
            raise RuntimeError(f"{name} is not forward of its attachment origin.")
        print(
            "M4A1_AUTHORED_MARKER "
            f"node={name} parent={parent_name} "
            f"local={tuple(round(value, 6) for value in marker.location)} "
            f"global={tuple(round(value, 6) for value in global_location)}"
        )

    reticle_anchor = bpy.data.objects["OpticReticleAnchor"]
    optic_mount = bpy.data.objects["OpticMount"]
    if reticle_anchor.parent != optic_mount:
        raise RuntimeError("OpticReticleAnchor is not a direct child of OpticMount.")
    expected_reticle = Vector(reticle_anchor["authored_glass_center"])
    if (reticle_anchor.location - expected_reticle).length > 1.0e-8:
        raise RuntimeError(
            "OpticReticleAnchor no longer matches the authored eyepiece glass center."
        )
    reticle_global = reticle_anchor.matrix_world.translation
    print(
        "M4A1_AUTHORED_MARKER "
        "node=OpticReticleAnchor parent=OpticMount "
        f"local={tuple(round(value, 6) for value in reticle_anchor.location)} "
        f"global={tuple(round(value, 6) for value in reticle_global)}"
    )


def require_unique_node(name: str) -> bpy.types.Object:
    matches = [obj for obj in bpy.context.scene.objects if obj.name == name]
    if len(matches) != 1:
        raise RuntimeError(f"Expected one {name!r} node, found {len(matches)}.")
    return matches[0]


def validate_reload_sockets(
    root: bpy.types.Object,
    phase: str,
) -> dict[str, Vector]:
    bpy.context.view_layer.update()
    mechanism_locations = {
        "Magazine": MAGAZINE_ORIGIN,
        "SpareMagazine": SPARE_MAGAZINE_ORIGIN,
        "ChargingHandle": CHARGING_HANDLE_ORIGIN,
    }
    for name, expected_location in mechanism_locations.items():
        mechanism = require_unique_node(name)
        if (
            mechanism.parent != root
            or (mechanism.location - expected_location).length > 1.0e-8
        ):
            raise RuntimeError(
                f"M4A1 {name} mechanism contract changed after {phase}: "
                f"parent={mechanism.parent.name if mechanism.parent else None} "
                f"location={tuple(mechanism.location)} "
                f"expected={tuple(expected_location)}"
            )

    contacts: dict[str, Vector] = {}
    socket_contracts = (
        (
            "MagazineGripSocket",
            "Magazine",
            "MagazineGeometry",
            MAGAZINE_GRIP_TARGET,
        ),
        (
            "ChargingHandleSocket",
            "ChargingHandle",
            "ChargingHandleGeometry",
            CHARGING_HANDLE_GRIP_TARGET,
        ),
    )
    for socket_name, parent_name, geometry_name, target in socket_contracts:
        socket = require_unique_node(socket_name)
        parent = require_unique_node(parent_name)
        geometry = require_unique_node(geometry_name)
        if socket.parent != parent or geometry.parent != parent:
            raise RuntimeError(
                f"M4A1 {socket_name} hierarchy changed after {phase}: "
                f"socket_parent={socket.parent.name if socket.parent else None} "
                f"geometry_parent={geometry.parent.name if geometry.parent else None}"
            )
        expected_contact, normal, face_index = mesh_surface_contact_in_parent(
            geometry,
            target,
        )
        drift = (socket.location - expected_contact).length
        surface_distance = mesh_surface_distance_in_parent(
            geometry,
            socket.location,
        )
        if drift > 0.000001 or surface_distance > 0.000001 or normal.x > -0.95:
            raise RuntimeError(
                f"M4A1 {socket_name} left its authored support-hand surface "
                f"after {phase}: drift={drift:.9f} "
                f"distance={surface_distance:.9f} "
                f"normal={tuple(normal)} face={face_index}"
            )
        contacts[socket_name] = socket.location.copy()
        print(
            "M4A1_RUNTIME_SOCKET "
            f"phase={phase} node={socket_name} parent={parent_name} "
            f"mesh={geometry_name} "
            f"local={tuple(round(value, 9) for value in socket.location)} "
            f"face={face_index} surface_distance={surface_distance:.9f}"
        )
    return contacts


def select_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_asset(root: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_extras=True,
        export_yup=True,
    )


def validate_glb_roundtrip() -> bpy.types.Object:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB))
    root = require_unique_node("SteelTideM4A1")
    mesh_count, vertex_count, triangle_count = mesh_statistics(root)
    if (
        mesh_count,
        vertex_count,
        triangle_count,
    ) != (
        EXPECTED_DCC_MESH_COUNT,
        EXPECTED_GLB_VERTEX_COUNT,
        EXPECTED_TRIANGLE_COUNT,
    ):
        raise RuntimeError(
            "M4A1 GLB round-trip topology changed: "
            f"meshes={mesh_count}/{EXPECTED_DCC_MESH_COUNT} "
            f"vertices={vertex_count}/{EXPECTED_GLB_VERTEX_COUNT} "
            f"triangles={triangle_count}/{EXPECTED_TRIANGLE_COUNT}"
        )
    validate_reload_sockets(root, "glb_roundtrip")
    root["reload_socket_roundtrip_verified"] = True
    return root


def validate_exported_optic_aperture() -> None:
    payload = OUTPUT_GLB.read_bytes()
    if len(payload) < 20:
        raise RuntimeError("Exported M4A1 GLB is truncated.")
    magic, version, declared_length = struct.unpack_from("<4sII", payload, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(payload):
        raise RuntimeError(
            "Exported M4A1 GLB header is invalid: "
            f"magic={magic!r} version={version} "
            f"length={declared_length}/{len(payload)}"
        )

    document = None
    offset = 12
    while offset + 8 <= len(payload):
        chunk_length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk = payload[offset:offset + chunk_length]
        offset += chunk_length
        if chunk_type == 0x4E4F534A:
            document = json.loads(chunk.rstrip(b"\x00\x20\t\r\n").decode("utf-8"))
            break
    if document is None:
        raise RuntimeError("Exported M4A1 GLB has no JSON chunk.")

    materials = document.get("materials", [])
    material_names = [material.get("name", "") for material in materials]
    if any("Glass" in name for name in material_names):
        raise RuntimeError(
            "Exported M4A1 GLB still contains a glass material: "
            f"materials={material_names}"
        )
    optic_meshes = [
        mesh
        for mesh in document.get("meshes", [])
        if mesh.get("name") == "OpticMountGeometryMesh"
    ]
    if len(optic_meshes) != 1:
        raise RuntimeError(
            "Exported M4A1 GLB optic mesh contract changed: "
            f"count={len(optic_meshes)}"
        )
    primitive_material_names = [
        material_names[primitive["material"]]
        for primitive in optic_meshes[0].get("primitives", [])
        if "material" in primitive
    ]
    if primitive_material_names != [
        "QuaterniusCompactOpticHousing",
        "QuaterniusCompactOpticHardware",
    ]:
        raise RuntimeError(
            "Exported M4A1 optic has unexpected runtime surfaces: "
            f"materials={primitive_material_names}"
        )
    print(
        "M4A1_EXPORTED_OPTIC_APERTURE "
        "glass_materials=0 glass_primitives=0 "
        f"runtime_materials={primitive_material_names}"
    )


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


def make_preview_material(
    name: str,
    color: tuple[float, float, float, float],
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = roughness
    return material


def set_hierarchy_render_visibility(root: bpy.types.Object, visible: bool) -> None:
    root.hide_render = not visible
    for child in root.children_recursive:
        child.hide_render = not visible


def add_preview_stage(root: bpy.types.Object) -> None:
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    set_hierarchy_render_visibility(bpy.data.objects["SpareMagazine"], False)
    set_hierarchy_render_visibility(bpy.data.objects["MuzzleDevice"], False)
    set_hierarchy_render_visibility(bpy.data.objects["Suppressor"], True)
    rear_iron_sight = bpy.data.objects["RearIronSight"]
    front_iron_sight = bpy.data.objects["FrontIronSight"]
    set_hierarchy_render_visibility(rear_iron_sight, False)
    set_hierarchy_render_visibility(front_iron_sight, False)

    world = bpy.context.scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.012, 0.016, 0.019, 1.0)
        background.inputs["Strength"].default_value = 0.32

    floor_material = make_preview_material(
        "PreviewFloorMaterial",
        (0.035, 0.041, 0.043, 1.0),
        0.76,
    )
    bpy.ops.mesh.primitive_plane_add(size=10.0, location=(0.0, 0.22, -0.445))
    floor = bpy.context.object
    floor.name = "PreviewFloor"
    floor.data.materials.append(floor_material)

    target = Vector((0.0, 0.22, -0.05))
    for name, location, energy, color, size in (
        ("PreviewKey", (2.4, -1.6, 2.7), 1150.0, (0.72, 0.88, 1.0), 2.8),
        ("PreviewFill", (-2.4, -0.2, 1.3), 820.0, (0.28, 1.0, 0.62), 2.4),
        ("PreviewRim", (-0.6, 2.8, 2.2), 1050.0, (1.0, 0.4, 0.18), 2.0),
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
    camera.location = Vector((2.75, -2.35, 1.25))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 56.0
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)

    reticle = bpy.data.objects["OpticReticleAnchor"].matrix_world.translation
    camera.location = Vector((reticle.x, -0.72, reticle.z))
    ads_target = Vector((reticle.x, 2.6, reticle.z))
    camera.rotation_euler = (ads_target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 58.0
    scene.render.filepath = str(ADS_PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)
    set_hierarchy_render_visibility(rear_iron_sight, True)
    set_hierarchy_render_visibility(front_iron_sight, True)
    root.hide_render = False


def main() -> None:
    bpy.context.preferences.filepaths.save_version = 0
    clear_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    root = build_runtime_asset()
    required_nodes = {
        "SteelTideM4A1",
        "Magazine",
        "SpareMagazine",
        "ChargingHandle",
        "MagazineGripSocket",
        "ChargingHandleSocket",
        "Stock",
        "RearIronSight",
        "FrontIronSight",
        "Foregrip",
        "MuzzleDevice",
        "Suppressor",
        "OpticMount",
        "MuzzleDeviceTip",
        "SuppressorTip",
        "OpticReticleAnchor",
    }
    missing_nodes = required_nodes - {obj.name for obj in bpy.data.objects}
    if missing_nodes:
        raise RuntimeError(f"Runtime M4A1 contract nodes missing: {sorted(missing_nodes)}")
    expected_attachment_locations = {
        "Foregrip": FOREGRIP_ORIGIN,
        "MuzzleDevice": MUZZLE_ORIGIN,
        "Suppressor": SUPPRESSOR_ORIGIN,
        "OpticMount": OPTIC_ORIGIN,
    }
    for name, expected_location in expected_attachment_locations.items():
        node = bpy.data.objects[name]
        if node.parent != root or (node.location - expected_location).length > 1.0e-8:
            raise RuntimeError(
                f"Runtime attachment transform changed for {name}: "
                f"parent={node.parent.name if node.parent else None} "
                f"location={tuple(node.location)} expected={tuple(expected_location)}"
            )
    rear_iron_sight = bpy.data.objects["RearIronSight"]
    if (
        rear_iron_sight.parent != root
        or (rear_iron_sight.location - REAR_IRON_ORIGIN).length > 1.0e-8
        or mesh_statistics(rear_iron_sight)[0] != 2
    ):
        raise RuntimeError(
            "Runtime rear iron sight contract changed: "
            f"parent={rear_iron_sight.parent.name if rear_iron_sight.parent else None} "
            f"location={tuple(rear_iron_sight.location)} "
            f"meshes={mesh_statistics(rear_iron_sight)[0]}"
        )
    front_iron_sight = bpy.data.objects["FrontIronSight"]
    if (
        front_iron_sight.parent != root
        or (front_iron_sight.location - FRONT_IRON_ORIGIN).length > 1.0e-8
        or mesh_statistics(front_iron_sight)[0] != 1
    ):
        raise RuntimeError(
            "Runtime front iron sight contract changed: "
            f"parent={front_iron_sight.parent.name if front_iron_sight.parent else None} "
            f"location={tuple(front_iron_sight.location)} "
            f"meshes={mesh_statistics(front_iron_sight)[0]}"
        )
    validate_attachment_geometry()
    validate_authored_markers()
    validate_reload_sockets(root, "dcc_source")
    validate_open_optic_aperture()
    mesh_count, vertex_count, triangle_count = mesh_statistics(root)
    if (
        mesh_count,
        vertex_count,
        triangle_count,
    ) != (
        EXPECTED_DCC_MESH_COUNT,
        EXPECTED_DCC_VERTEX_COUNT,
        EXPECTED_TRIANGLE_COUNT,
    ):
        raise RuntimeError(
            "Authored M4A1 visible topology changed: "
            f"meshes={mesh_count}/{EXPECTED_DCC_MESH_COUNT} "
            f"vertices={vertex_count}/{EXPECTED_DCC_VERTEX_COUNT} "
            f"triangles={triangle_count}/{EXPECTED_TRIANGLE_COUNT}"
        )
    export_asset(root)
    validate_exported_optic_aperture()
    save_source()
    roundtrip_root = validate_glb_roundtrip()
    add_preview_stage(roundtrip_root)
    print(
        "NISU_M4A1_EXPORT "
        f"meshes={mesh_count} vertices={vertex_count} triangles={triangle_count} "
        f"glb_sha256={hashlib.sha256(OUTPUT_GLB.read_bytes()).hexdigest()} "
        f"blend_sha256={hashlib.sha256(OUTPUT_BLEND.read_bytes()).hexdigest()} "
        f"preview_sha256={hashlib.sha256(PREVIEW_PATH.read_bytes()).hexdigest()} "
        f"ads_preview_sha256={hashlib.sha256(ADS_PREVIEW_PATH.read_bytes()).hexdigest()} "
        f"glb={OUTPUT_GLB} blend={OUTPUT_BLEND} preview={PREVIEW_PATH} "
        f"ads_preview={ADS_PREVIEW_PATH}"
    )


if __name__ == "__main__":
    main()
