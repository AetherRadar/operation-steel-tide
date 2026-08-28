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
from pathlib import Path

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


def source_aperture_center(
    source: bpy.types.Object,
    faces: set[int],
) -> tuple[Vector, set[int]]:
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
    positions = [oriented_position(source, index) for index in glass_vertices]
    rear_y = min(position.y for position in positions)
    rear_positions = [
        position for position in positions if abs(position.y - rear_y) <= 1.0e-5
    ]
    if len(rear_positions) < 3:
        raise RuntimeError(f"No stable rear aperture on {source.name}.")
    minimum = Vector(
        tuple(min(position[axis] for position in rear_positions) for axis in range(3))
    )
    maximum = Vector(
        tuple(max(position[axis] for position in rear_positions) for axis in range(3))
    )
    return (minimum + maximum) * 0.5, glass_faces


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

    built: list[tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object]] = []
    for spec in OPTICS:
        source = import_source(spec)
        faces = select_scope_component(source, spec)
        aperture_center, glass_faces = source_aperture_center(source, faces)
        deform = build_point_deformer(source, faces, spec, aperture_center)
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
        # build_point_deformer accepts source-local coordinates, whereas the
        # aperture center above is already oriented world space.  Derive the
        # stable marker from the transformed source eyepiece vertices instead.
        glass_vertices = face_vertices(source, glass_faces)
        rear_oriented_y = min(oriented_position(source, index).y for index in glass_vertices)
        rear_vertices = [
            index
            for index in glass_vertices
            if abs(oriented_position(source, index).y - rear_oriented_y) <= 1.0e-5
        ]
        reticle_positions = [deform(source.data.vertices[index].co) for index in rear_vertices]
        reticle_average = sum(reticle_positions, Vector()) / len(reticle_positions)
        # The source panes duplicate some rim vertices for split normals, so an
        # arithmetic vertex average is not the exact optical center.  The
        # aperture bounding center is intentionally normalized to X/Z zero.
        reticle = Vector((0.0, reticle_average.y, 0.0))
        anchor_name = spec.node_name.replace("Optic", "ReticleAnchor")
        anchor = empty(anchor_name, variant, reticle)
        anchor["runtime_asset"] = True
        anchor["derived_from_mesh"] = geometry.name
        anchor["derived_from_removed_source_glass"] = True
        anchor["derived_from_surface"] = "rear eyepiece center"
        validate_open_aperture(variant, geometry, anchor)
        built.append((variant, geometry, anchor))

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
    for variant, geometry, _ in built:
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
        export_cameras=False,
        export_lights=False,
    )


def save_source() -> None:
    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))


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
    available_nodes = {obj.name for obj in (root, *root.children_recursive)}
    missing = expected_nodes - available_nodes
    if missing:
        raise RuntimeError(f"Authored optic runtime contract missing: {sorted(missing)}")
    for spec in OPTICS:
        variant = bpy.data.objects[spec.node_name]
        geometry = bpy.data.objects[spec.geometry_name]
        anchor_name = spec.node_name.replace("Optic", "ReticleAnchor")
        anchors = [child for child in variant.children if child.name == anchor_name]
        if len(anchors) != 1 or geometry.parent != variant:
            raise RuntimeError(f"Incomplete runtime hierarchy for {spec.node_name}.")

    mesh_count, vertex_count, triangle_count = mesh_statistics(root)
    export_asset(root)
    save_source()
    add_preview_stage()
    print(
        "AUTHORED_OPTICS_EXPORT "
        f"meshes={mesh_count} vertices={vertex_count} triangles={triangle_count} "
        f"glb={OUTPUT_GLB} blend={OUTPUT_BLEND} preview={PREVIEW_PATH} "
        f"ads_preview={ADS_PREVIEW_PATH}"
    )


if __name__ == "__main__":
    main()
