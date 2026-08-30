"""Build one instanced, opaque CC0 liner for every enterable Jianghai shop.

The street buildings are exterior shells and their outward-facing polygons are
culled from inside.  This module composes a five-sided room from finished
Quaternius Downtown City MegaKit wall and floor modules in Blender.  Every room
instances the same 44-triangle mesh, so the fix closes sightlines without unique
geometry, textures, or expensive shadows per building.
"""

from __future__ import annotations

from dataclasses import dataclass
from math import radians
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPO_ROOT = Path(__file__).resolve().parents[2]
WALL_SOURCE = (
    REPO_ROOT
    / "assets"
    / "models"
    / "quaternius_downtown_city"
    / "Brick_Plain_1.gltf"
)
FLOOR_SOURCE = (
    REPO_ROOT
    / "assets"
    / "models"
    / "quaternius_downtown_city"
    / "Floor_4x4.gltf"
)
PACKED_WALL_MESH_NAME = "JianghaiEntryFacade_BrickPlain"
LINER_MESH_NAME = "JianghaiInteriorShopLiner_LOD"
LINER_OBJECT_PREFIX = "JianghaiInteriorShell_"
LINER_VISIBILITY_METERS = 460.0
LINER_FLOOR_LIFT_METERS = 0.015
CANONICAL_WIDTH_METERS = 6.0
CANONICAL_DEPTH_METERS = 6.0
CANONICAL_HEIGHT_METERS = 3.0
EXPECTED_TRIANGLES = 44
SOURCE_URL = "https://quaternius.com/packs/downtowncitymegakit.html"
LICENSE = "CC0 1.0 Universal"


@dataclass(frozen=True)
class InteriorLinerMetrics:
    liner_count: int
    shared_mesh_users: int
    triangle_count: int
    closure_sample_count: int
    entry_sample_count: int
    opaque_material_count: int


def _mesh_bounds(mesh: bpy.types.Mesh) -> tuple[Vector, Vector]:
    if not mesh.vertices:
        raise RuntimeError(f"Interior liner source has no vertices: {mesh.name}")
    return (
        Vector(tuple(min(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3))),
        Vector(tuple(max(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3))),
    )


def _existing_wall_module() -> tuple[bpy.types.Mesh, bpy.types.Material]:
    """Reuse the already-packed Brick_Plain_1 mesh and its interior material."""

    if not WALL_SOURCE.is_file():
        raise RuntimeError(f"Registered CC0 wall module is missing: {WALL_SOURCE}")
    mesh = bpy.data.meshes.get(PACKED_WALL_MESH_NAME)
    if mesh is None:
        raise RuntimeError(
            f"Packed entry-facade wall module is missing: {PACKED_WALL_MESH_NAME}"
        )
    minimum, maximum = _mesh_bounds(mesh)
    if (
        (maximum - minimum - Vector((2.0, 0.2, 1.0))).length > 0.001
        or len(mesh.materials) != 2
    ):
        raise RuntimeError(
            f"Packed Brick_Plain_1 contract drifted: dimensions={tuple(maximum - minimum)} "
            f"materials={len(mesh.materials)}"
        )
    interior_material = next(
        (
            material
            for material in mesh.materials
            if material is not None and "interiorwall" in material.name.lower()
        ),
        None,
    )
    if interior_material is None or not _material_is_opaque(interior_material):
        raise RuntimeError("Packed Brick_Plain_1 has no opaque interior-wall material")
    return mesh, interior_material


def _load_floor_geometry(
    source_path: Path,
    interior_material: bpy.types.Material,
) -> bpy.types.Mesh:
    """Import Floor_4x4 geometry, then discard its redundant material/images."""

    if not source_path.is_file():
        raise RuntimeError(f"CC0 interior module is missing: {source_path}")
    before_objects = set(bpy.data.objects)
    before_meshes = set(bpy.data.meshes)
    before_materials = set(bpy.data.materials)
    before_images = set(bpy.data.images)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    imported_meshes = [mesh for mesh in bpy.data.meshes if mesh not in before_meshes]
    imported_materials = [material for material in bpy.data.materials if material not in before_materials]
    imported_images = [image for image in bpy.data.images if image not in before_images]
    mesh_objects = [obj for obj in imported_objects if obj.type == "MESH"]
    if len(mesh_objects) != 1:
        raise RuntimeError(
            f"Expected one finished mesh in {source_path}, found {len(mesh_objects)}"
        )
    mesh = mesh_objects[0].data.copy()
    mesh.name = "__JianghaiLinerFloor4x4"
    mesh.materials.clear()
    mesh.materials.append(interior_material)
    for polygon in mesh.polygons:
        polygon.material_index = 0
    mesh["source_asset"] = f"Downtown City MegaKit / {source_path.stem}"
    mesh["source_creator"] = "Quaternius"
    mesh["source_url"] = SOURCE_URL
    mesh["license"] = LICENSE
    for obj in imported_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for imported_mesh in imported_meshes:
        if imported_mesh.name in bpy.data.meshes and imported_mesh.users == 0:
            bpy.data.meshes.remove(imported_mesh)
    for material in imported_materials:
        if material.name in bpy.data.materials and material.users == 0:
            bpy.data.materials.remove(material)
    for image in imported_images:
        if image.name in bpy.data.images and image.users == 0:
            bpy.data.images.remove(image)
    return mesh


def _transformed_component(
    name: str,
    source_mesh: bpy.types.Mesh,
    transform: Matrix,
) -> bpy.types.Object:
    mesh = source_mesh.copy()
    mesh.name = f"__{name}_Mesh"
    mesh.transform(transform)
    component = bpy.data.objects.new(f"__{name}", mesh)
    bpy.context.scene.collection.objects.link(component)
    return component


def _build_shared_liner_mesh() -> bpy.types.Mesh:
    old_mesh = bpy.data.meshes.get(LINER_MESH_NAME)
    if old_mesh is not None:
        if old_mesh.users != 0:
            raise RuntimeError(f"Interior liner mesh still has live users: {old_mesh.users}")
        old_materials = [material for material in old_mesh.materials if material is not None]
        old_images = {
            node.image
            for material in old_materials
            if material.use_nodes and material.node_tree is not None
            for node in material.node_tree.nodes
            if node.type == "TEX_IMAGE" and node.image is not None
        }
        bpy.data.meshes.remove(old_mesh)
        for material in old_materials:
            if material.users == 0 and material.get("jianghai_liner_opaque") is True:
                bpy.data.materials.remove(material)
        for image in old_images:
            if image.users == 0:
                bpy.data.images.remove(image)

    wall_mesh, interior_material = _existing_wall_module()
    floor_mesh = _load_floor_geometry(FLOOR_SOURCE, interior_material)
    components = (
        _transformed_component(
            "JianghaiLinerBack",
            wall_mesh,
            Matrix.Translation((0.0, CANONICAL_DEPTH_METERS, 0.0))
            @ Matrix.Rotation(radians(180.0), 4, "Z")
            @ Matrix.Diagonal((3.0, 1.0, 3.0, 1.0)),
        ),
        _transformed_component(
            "JianghaiLinerLeft",
            wall_mesh,
            Matrix.Translation((-3.0, 3.0, 0.0))
            @ Matrix.Rotation(radians(-90.0), 4, "Z")
            @ Matrix.Diagonal((3.0, 1.0, 3.0, 1.0)),
        ),
        _transformed_component(
            "JianghaiLinerRight",
            wall_mesh,
            Matrix.Translation((3.0, 3.0, 0.0))
            @ Matrix.Rotation(radians(90.0), 4, "Z")
            @ Matrix.Diagonal((3.0, 1.0, 3.0, 1.0)),
        ),
        _transformed_component(
            "JianghaiLinerFloor",
            floor_mesh,
            Matrix.Translation((0.0, 3.0, 0.0))
            @ Matrix.Diagonal((1.5, 1.5, 1.0, 1.0)),
        ),
        _transformed_component(
            "JianghaiLinerCeiling",
            floor_mesh,
            Matrix.Translation((0.0, 3.0, CANONICAL_HEIGHT_METERS))
            @ Matrix.Diagonal((1.5, 1.5, 1.0, 1.0)),
        ),
    )
    component_meshes = [component.data for component in components]
    bpy.ops.object.select_all(action="DESELECT")
    for component in components:
        component.select_set(True)
    bpy.context.view_layer.objects.active = components[0]
    bpy.ops.object.join()
    combined = components[0]
    combined.name = "__JianghaiInteriorShopLiner"
    mesh = combined.data
    mesh.name = LINER_MESH_NAME
    mesh["source_asset"] = (
        "Downtown City MegaKit / Brick_Plain_1 + Floor_4x4"
    )
    mesh["source_creator"] = "Quaternius"
    mesh["source_url"] = SOURCE_URL
    mesh["license"] = LICENSE
    mesh["authored_derivation"] = (
        "Blender-authored five-sided room joined from finished CC0 modular wall and floor meshes"
    )
    mesh["jianghai_interior_liner"] = True
    mesh["jianghai_liner_opaque"] = True
    mesh["jianghai_liner_shared_instances"] = True
    bpy.data.objects.remove(combined, do_unlink=True)
    for component_mesh in component_meshes:
        if component_mesh != mesh and component_mesh.users == 0:
            bpy.data.meshes.remove(component_mesh)
    if floor_mesh.users == 0:
        bpy.data.meshes.remove(floor_mesh)
    return mesh


def _liner_world_matrix(building: bpy.types.Object) -> Matrix:
    minimum, maximum = _mesh_bounds(building.data)
    world_scale = building.matrix_world.to_scale()
    scale = Vector((abs(world_scale.x), abs(world_scale.y), abs(world_scale.z)))
    front_inset = float(building["jianghai_door_front_inset_m"])
    room_width = float(building["jianghai_room_width_m"])
    room_depth = float(building["jianghai_room_depth_m"])
    facade_origin = building.matrix_world @ Vector(
        (
            (minimum.x + maximum.x) * 0.5,
            minimum.y + front_inset / scale.y,
            minimum.z,
        )
    )
    facade_origin.z += LINER_FLOOR_LIFT_METERS
    rotation = building.matrix_world.to_quaternion().to_matrix().to_4x4()
    return (
        Matrix.Translation(facade_origin)
        @ rotation
        @ Matrix.Diagonal(
            (
                room_width / CANONICAL_WIDTH_METERS,
                room_depth / CANONICAL_DEPTH_METERS,
                1.0,
                1.0,
            )
        )
    )


def rebuild_interior_liners(residence_names: tuple[str, ...]) -> InteriorLinerMetrics:
    for obj in list(bpy.data.objects):
        if obj.name.startswith(LINER_OBJECT_PREFIX):
            bpy.data.objects.remove(obj, do_unlink=True)
    mesh = _build_shared_liner_mesh()
    for building_name in residence_names:
        building = bpy.data.objects.get(building_name)
        if building is None or building.type != "MESH":
            raise RuntimeError(f"Interior liner building is missing: {building_name}")
        liner = bpy.data.objects.new(f"{LINER_OBJECT_PREFIX}{building_name}", mesh)
        bpy.context.scene.collection.objects.link(liner)
        liner.parent = building.parent
        liner.matrix_world = _liner_world_matrix(building)
        liner["jianghai_interior_liner"] = True
        liner["jianghai_liner_source_name"] = building_name
        liner["jianghai_liner_visibility_m"] = LINER_VISIBILITY_METERS
        liner["jianghai_liner_opaque"] = True
        liner["jianghai_liner_shadow_mode"] = "off"
        liner["jianghai_disable_shadows"] = True
        liner["jianghai_liner_floor_lift_m"] = LINER_FLOOR_LIFT_METERS
        liner["source_asset"] = mesh["source_asset"]
        liner["source_creator"] = "Quaternius"
        liner["source_url"] = SOURCE_URL
        liner["license"] = LICENSE
        liner["authored_adaptation"] = (
            "Shared low-triangle opaque modular liner fitted to an enterable Chinese shop"
        )
        liner["district_role"] = "shared_enterable_interior_liner"
    bpy.context.view_layer.update()
    return validate_interior_liners(residence_names)


def _material_is_opaque(material: bpy.types.Material) -> bool:
    if material.diffuse_color[3] < 0.9999:
        return False
    if not material.use_nodes or material.node_tree is None:
        return True
    for node in material.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        alpha = node.inputs.get("Alpha")
        if alpha is not None and (alpha.is_linked or alpha.default_value < 0.9999):
            return False
    return True


def _validate_shared_closure(mesh: bpy.types.Mesh) -> tuple[int, int]:
    tree = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in mesh.vertices],
        [tuple(polygon.vertices) for polygon in mesh.polygons],
        all_triangles=False,
    )
    centre = Vector((0.0, 3.0, 1.35))
    closure_rays = (
        (centre, Vector((0.0, 1.0, 0.0))),
        (centre, Vector((-1.0, 0.0, 0.0))),
        (centre, Vector((1.0, 0.0, 0.0))),
        (centre, Vector((0.0, 0.0, 1.0))),
        (centre, Vector((0.0, 0.0, -1.0))),
    )
    for origin, direction in closure_rays:
        if tree.ray_cast(origin, direction, 8.0)[0] is None:
            raise RuntimeError(
                f"Interior liner is open toward {tuple(direction)}"
            )
    entry_samples = 0
    for lateral in (-0.42, 0.0, 0.42):
        for height in (0.45, 1.20, 2.18):
            origin = Vector((lateral, -0.40, height))
            if tree.ray_cast(origin, Vector((0.0, 1.0, 0.0)), 1.10)[0] is not None:
                raise RuntimeError(
                    f"Interior liner blocks the doorway: x={lateral:.2f} z={height:.2f}"
                )
            entry_samples += 1
    return len(closure_rays), entry_samples


def validate_interior_liners(residence_names: tuple[str, ...]) -> InteriorLinerMetrics:
    expected_names = {f"{LINER_OBJECT_PREFIX}{name}" for name in residence_names}
    liners = [
        obj
        for obj in bpy.data.objects
        if obj.name.startswith(LINER_OBJECT_PREFIX)
    ]
    if {obj.name for obj in liners} != expected_names:
        raise RuntimeError(
            f"Interior liner set drifted: actual={sorted(obj.name for obj in liners)}"
        )
    mesh = bpy.data.meshes.get(LINER_MESH_NAME)
    if mesh is None or any(liner.data != mesh for liner in liners):
        raise RuntimeError("Enterable rooms no longer share one interior liner mesh")
    mesh.calc_loop_triangles()
    if len(mesh.loop_triangles) != EXPECTED_TRIANGLES:
        raise RuntimeError(
            f"Interior liner triangle contract drifted: {len(mesh.loop_triangles)}"
        )
    minimum, maximum = _mesh_bounds(mesh)
    expected_minimum = Vector((-3.0, 0.0, -0.1))
    expected_maximum = Vector((3.0, 6.0, 3.0))
    if (minimum - expected_minimum).length > 0.001 or (maximum - expected_maximum).length > 0.001:
        raise RuntimeError(
            f"Interior liner bounds drifted: minimum={tuple(minimum)} maximum={tuple(maximum)}"
        )
    opaque_materials = [
        material
        for material in mesh.materials
        if material is not None and _material_is_opaque(material)
    ]
    if len(mesh.materials) != 2 or len(opaque_materials) != 2:
        raise RuntimeError(
            f"Interior liner materials are not two reused opaque shared slots: "
            f"slots={len(mesh.materials)} opaque={len(opaque_materials)}"
        )
    closure_samples, entry_samples = _validate_shared_closure(mesh)
    for source_name in residence_names:
        building = bpy.data.objects[source_name]
        liner = bpy.data.objects[f"{LINER_OBJECT_PREFIX}{source_name}"]
        if (
            liner.get("jianghai_interior_liner") is not True
            or liner.get("jianghai_liner_source_name") != source_name
            or abs(float(liner.get("jianghai_liner_visibility_m", 0.0)) - LINER_VISIBILITY_METERS)
            > 0.0001
            or liner.get("jianghai_liner_opaque") is not True
            or liner.get("jianghai_liner_shadow_mode") != "off"
            or abs(float(liner.get("jianghai_liner_floor_lift_m", 0.0)) - LINER_FLOOR_LIFT_METERS)
            > 0.0001
            or liner.parent != building.parent
            or (liner.matrix_world.translation - _liner_world_matrix(building).translation).length
            > 0.001
        ):
            raise RuntimeError(f"Interior liner metadata or alignment drifted: {source_name}")
    return InteriorLinerMetrics(
        len(liners),
        mesh.users,
        len(mesh.loop_triangles),
        closure_samples * len(liners),
        entry_samples * len(liners),
        len(opaque_materials),
    )
