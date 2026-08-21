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
SOURCE_RELOAD_START_FRAME = 0
SOURCE_RELOAD_END_FRAME = 64
SOURCE_TO_METERS = 0.015
SLEEVE_BLEND_LENGTH = 12.0
SLEEVE_EXTENSION = 36.0
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
        maximum_y = max(mesh.vertices[index].co.y for index in component)
        blend_start = maximum_y - SLEEVE_BLEND_LENGTH
        for index in component:
            vertex = mesh.vertices[index]
            if vertex.co.y <= blend_start:
                continue
            normalized = min(1.0, max(0.0, (vertex.co.y - blend_start) / SLEEVE_BLEND_LENGTH))
            falloff = normalized * normalized * (3.0 - 2.0 * normalized)
            vertex.co.y += SLEEVE_EXTENSION * falloff
    mesh.update()


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
    root = prepare_first_person_hierarchy()
    base = bpy.data.objects["base"]
    muzzle = bpy.data.objects.new("Muzzle", None)
    muzzle.empty_display_type = "PLAIN_AXES"
    muzzle.empty_display_size = 0.035
    bpy.context.collection.objects.link(muzzle)
    muzzle.parent = base
    muzzle.location = base.matrix_world.inverted() @ Vector((0.0, -49.60, 4.93))
    export_root(root, FIRST_PERSON_GLB, animated=True)


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
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))


def main() -> None:
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(f"Missing tracked source asset: {SOURCE_GLB}")
    bpy.context.preferences.filepaths.save_version = 0
    RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    save_editable_source()
    build_first_person()
    build_field_weapon()
    print(f"Wrote {FIRST_PERSON_GLB}")
    print(f"Wrote {FIELD_GLB}")
    print(f"Wrote {SOURCE_BLEND}")


if __name__ == "__main__":
    main()
