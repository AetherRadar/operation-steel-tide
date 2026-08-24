"""Build optimized runtime SMG-45 models from the tracked DJMaesen source GLB.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup --python scripts/blender/build_djmaesen_smg45.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "djmaesen_fps_smg45"
SOURCE_GLB = SOURCE_DIR / "fps_animated_smg.glb"
SOURCE_BLEND = SOURCE_DIR / "djmaesen_fps_smg45.blend"
RUNTIME_DIR = REPO_ROOT / "assets" / "models" / "djmaesen_smg45"
FIRST_PERSON_GLB = RUNTIME_DIR / "smg45_first_person.glb"
FIRST_PERSON_ARMS_GLB = RUNTIME_DIR / "first_person_arms.glb"
FIELD_GLB = RUNTIME_DIR / "smg45_weapon.glb"

SOURCE_IDLE_FRAME = 155
SOURCE_RELOAD_START_FRAME = 0
SOURCE_RELOAD_END_FRAME = 64
SOURCE_TO_METERS = 0.015
SLEEVE_BLEND_LENGTH = 12.0
# Negative extension pulls the cuffs back toward/behind the first-person camera
# so the tubes never end inside the visible frame, and the drop pushes them
# below the bottom edge of the camera frustum across the reload animation.
# Increased from -60/240 to -120/400 to fully hide cuffs during SMG-45 reload
# where support arm is extended and viewed at an oblique angle.
SLEEVE_EXTENSION = -120.0
SLEEVE_DROP = 400.0
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


def import_source(frame: int = SOURCE_IDLE_FRAME) -> None:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    bpy.context.scene.render.fps = 24
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def extend_authored_sleeves() -> None:
    """Move the two upper-sleeve cuffs behind the first-person camera."""
    arms = bpy.data.objects["Object_7"]
    mesh = arms.data
    adjacency: dict[int, set[int]] = {index: set() for index in range(len(mesh.vertices))}
    for edge in mesh.edges:
        left, right = edge.vertices
        adjacency[left].add(right)
        adjacency[right].add(left)

    unseen = set(adjacency)
    components: list[set[int]] = []
    while unseen:
        seed = unseen.pop()
        component = {seed}
        stack = [seed]
        while stack:
            vertex = stack.pop()
            for neighbor in adjacency[vertex]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    component.add(neighbor)
                    stack.append(neighbor)
        components.append(component)

    for component in sorted(components, key=len, reverse=True)[:2]:
        minimum_y = min(mesh.vertices[index].co.y for index in component)
        blend_end = minimum_y + SLEEVE_BLEND_LENGTH
        for index in component:
            vertex = mesh.vertices[index]
            if vertex.co.y >= blend_end:
                continue
            normalized = min(1.0, max(0.0, (blend_end - vertex.co.y) / SLEEVE_BLEND_LENGTH))
            falloff = normalized * normalized * (3.0 - 2.0 * normalized)
            vertex.co.y -= SLEEVE_EXTENSION * falloff
            vertex.co.z -= SLEEVE_DROP * falloff
    mesh.update()


def _boundary_edges(mesh: bpy.types.Mesh, component: set[int]) -> list[tuple[int, int]]:
    edge_faces: dict[tuple[int, int], int] = {}
    for polygon in mesh.polygons:
        for edge_key in polygon.edge_keys:
            key = tuple(sorted(edge_key))
            edge_faces[key] = edge_faces.get(key, 0) + 1
    return [
        edge
        for edge, face_count in edge_faces.items()
        if face_count == 1 and edge[0] in component and edge[1] in component
    ]


def _boundary_loops(edges: list[tuple[int, int]]) -> list[list[int]]:
    """Group boundary edges into ordered loops (open chains are returned too)."""
    adjacency: dict[int, list[int]] = {}
    for left, right in edges:
        adjacency.setdefault(left, []).append(right)
        adjacency.setdefault(right, []).append(left)

    loops: list[list[int]] = []
    visited_edges: set[tuple[int, int]] = set()
    for start in sorted(adjacency):
        for neighbor in adjacency[start]:
            edge_key = (min(start, neighbor), max(start, neighbor))
            if edge_key in visited_edges:
                continue
            ordered = [start, neighbor]
            visited_edges.add(edge_key)
            previous, current = start, neighbor
            while True:
                next_vertices = [
                    index
                    for index in adjacency[current]
                    if index != previous
                    and (min(current, index), max(current, index)) not in visited_edges
                ]
                if not next_vertices:
                    break
                next_vertex = next_vertices[0]
                visited_edges.add((min(current, next_vertex), max(current, next_vertex)))
                ordered.append(next_vertex)
                if next_vertex == start:
                    ordered.pop()
                    break
                previous, current = current, next_vertex
            if len(ordered) >= 3:
                loops.append(ordered)
    return loops


def _cap_boundary_loops(mesh: bpy.types.Mesh, component: set[int]) -> None:
    edges = _boundary_edges(mesh, component)
    loops = _boundary_loops(edges)
    if not loops:
        return
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        bm.verts.ensure_lookup_table()
        new_faces = []
        for order in loops:
            vertices = [bm.verts[index] for index in order]
            try:
                face = bm.faces.new(vertices)
            except ValueError:
                continue
            face.material_index = 0
            new_faces.append(face)
        if new_faces:
            component_faces = [
                face for face in bm.faces if any(vertex.index in component for vertex in face.verts)
            ]
            bmesh.ops.recalc_face_normals(bm, faces=component_faces)
            for face in new_faces:
                face.smooth = True
        bm.to_mesh(mesh)
        mesh.update()
    finally:
        bm.free()


def cap_authored_sleeves() -> None:
    """Seal every boundary loop on the authored arms so the first-person model stays watertight."""
    arms = bpy.data.objects["Object_7"]
    mesh = arms.data
    adjacency: dict[int, set[int]] = {index: set() for index in range(len(mesh.vertices))}
    for edge in mesh.edges:
        left, right = edge.vertices
        adjacency[left].add(right)
        adjacency[right].add(left)

    unseen = set(adjacency)
    components: list[set[int]] = []
    while unseen:
        seed = unseen.pop()
        component = {seed}
        stack = [seed]
        while stack:
            vertex = stack.pop()
            for neighbor in adjacency[vertex]:
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    component.add(neighbor)
                    stack.append(neighbor)
        components.append(component)

    for component in sorted(components, key=len, reverse=True):
        _cap_boundary_loops(mesh, component)


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


def arm_side_copy(
    source: bpy.types.Object,
    name: str,
    keep_positive_x: bool,
) -> bpy.types.Object:
    """Keep one disconnected authored arm while preserving materials and UVs."""
    mesh = source.data.copy()
    mesh.name = f"{name}Mesh"
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    result.matrix_world = source.matrix_world.copy()

    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        unseen = set(bm.verts)
        remove = []
        while unseen:
            seed = unseen.pop()
            component = {seed}
            stack = [seed]
            while stack:
                vertex = stack.pop()
                for edge in vertex.link_edges:
                    neighbor = edge.other_vert(vertex)
                    if neighbor in unseen:
                        unseen.remove(neighbor)
                        component.add(neighbor)
                        stack.append(neighbor)
            center_x = sum(vertex.co.x for vertex in component) / len(component)
            if (center_x >= 0.0) != keep_positive_x:
                remove.extend(component)
        bmesh.ops.delete(bm, geom=remove, context="VERTS")
        bm.to_mesh(mesh)
        mesh.update()
    finally:
        bm.free()
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


def export_root(root: bpy.types.Object, output: Path, animated: bool = False) -> None:
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
        export_animations=animated,
        export_frame_range=animated,
        export_force_sampling=animated,
        export_animation_mode="NLA_TRACKS" if animated else "ACTIONS",
        export_nla_strips_merged_animation_name="reload",
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


def prepare_first_person_hierarchy() -> bpy.types.Object:
    root = bpy.data.objects["Sketchfab_model"]
    root.name = "DJMaesenSMG45FirstPerson"
    root.scale = Vector((SOURCE_TO_METERS, SOURCE_TO_METERS, SOURCE_TO_METERS))
    bpy.data.objects["Object_7"].name = "AuthoredArms"
    for source_name, runtime_name in RUNTIME_WEAPON_NAMES.items():
        bpy.data.objects[source_name].name = runtime_name
    for child in root.children_recursive:
        if child.animation_data is None:
            continue
        for track in child.animation_data.nla_tracks:
            track.name = "reload"
    scene = bpy.context.scene
    scene.frame_start = SOURCE_RELOAD_START_FRAME
    scene.frame_end = SOURCE_RELOAD_END_FRAME
    scene.frame_set(SOURCE_RELOAD_START_FRAME)
    return root


def build_first_person() -> None:
    import_source(SOURCE_RELOAD_START_FRAME)
    extend_authored_sleeves()
    cap_authored_sleeves()
    root = prepare_first_person_hierarchy()
    base = bpy.data.objects["base"]
    muzzle = bpy.data.objects.new("Muzzle", None)
    muzzle.empty_display_type = "PLAIN_AXES"
    muzzle.empty_display_size = 0.035
    bpy.context.collection.objects.link(muzzle)
    muzzle.parent = base
    muzzle.location = base.matrix_world.inverted() @ Vector((0.0, -49.60, 4.93))
    export_root(root, FIRST_PERSON_GLB, animated=True)


def build_first_person_arms() -> None:
    """Bake the authored idle pose into independently mounted, fixed-scale arms."""
    import_source(SOURCE_IDLE_FRAME)
    cap_authored_sleeves()
    source = bpy.data.objects["Object_7"]
    baked = evaluated_mesh_copy(source, "AuthoredArmsBaked")
    right_mesh = arm_side_copy(baked, "RightArmMesh", keep_positive_x=False)
    left_mesh = arm_side_copy(baked, "LeftArmMesh", keep_positive_x=True)

    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    right_palm = (
        armature.matrix_world @ armature.pose.bones["R_palm_039"].matrix
    ).translation
    left_palm = (
        armature.matrix_world @ armature.pose.bones["L_palm_015"].matrix
    ).translation

    root = new_root("DJMaesenFirstPersonArms")
    right_arm = new_root("RightArm")
    left_arm = new_root("LeftArm")
    right_arm.location = right_palm
    left_arm.location = left_palm
    right_arm.parent = root
    left_arm.parent = root
    parent_keep_world(right_mesh, right_arm)
    parent_keep_world(left_mesh, left_arm)
    add_marker(right_arm, "RightPalm", (0.0, 0.0, 0.0))
    add_marker(left_arm, "LeftPalm", (0.0, 0.0, 0.0))
    root.scale = Vector((SOURCE_TO_METERS, SOURCE_TO_METERS, SOURCE_TO_METERS))
    export_root(root, FIRST_PERSON_ARMS_GLB)


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
    import_source(SOURCE_IDLE_FRAME)
    extend_authored_sleeves()
    cap_authored_sleeves()
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))


def main() -> None:
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(f"Missing tracked source asset: {SOURCE_GLB}")
    bpy.context.preferences.filepaths.save_version = 0
    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    save_editable_source()
    build_first_person()
    build_first_person_arms()
    build_field_weapon()
    print(f"Wrote {FIRST_PERSON_GLB}")
    print(f"Wrote {FIRST_PERSON_ARMS_GLB}")
    print(f"Wrote {FIELD_GLB}")
    print(f"Wrote {SOURCE_BLEND}")


if __name__ == "__main__":
    main()
