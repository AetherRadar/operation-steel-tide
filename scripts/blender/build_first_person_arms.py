"""Build clean static first-person arm poses from the original DJMaesen GLB.

The tracked runtime SMG asset deliberately contains an animated rig.  This
builder takes the unmodified source GLB, evaluates a single authored pose, and
exports a small static GLB with separate production arm meshes plus explicit
palm, wrist, and source-weapon grip frames. Runtime code can rigidly mount the
pose at the primary grip and translate only the support arm for each gun family.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup --python scripts/blender/build_first_person_arms.py
"""

from __future__ import annotations

import math
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_djmaesen_smg45 import cap_authored_sleeves


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_GLB = (
    REPO_ROOT
    / "source_art"
    / "third_party"
    / "djmaesen_fps_smg45"
    / "fps_animated_smg.glb"
)
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "djmaesen_smg45"
PREVIEW_DIR = REPO_ROOT / "build" / "art-previews" / "combat_models"
SOURCE_TO_METERS = 0.015


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


def import_source(frame: int = 0) -> None:
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def bone_world_matrix(armature: bpy.types.Object, bone_name: str) -> Matrix:
    return armature.matrix_world @ armature.pose.bones[bone_name].matrix


def add_pistol_ik(
    armature: bpy.types.Object,
    wrist_target: Vector,
    pole_location: Vector,
    roll_degrees: float,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    wrist = armature.pose.bones["L_wrist_03"]
    target = bpy.data.objects.new("PistolLeftWristTarget", None)
    target.empty_display_type = "SPHERE"
    target.empty_display_size = 2.0
    right_palm_basis = bone_world_matrix(armature, "R_palm_039").to_3x3().to_4x4()
    target.matrix_world = (
        Matrix.Translation(wrist_target)
        @ Matrix.Rotation(math.radians(roll_degrees), 4, "Z")
        @ right_palm_basis
    )
    bpy.context.collection.objects.link(target)

    pole = bpy.data.objects.new("PistolLeftElbowPole", None)
    pole.empty_display_type = "CUBE"
    pole.empty_display_size = 2.0
    pole.location = pole_location
    bpy.context.collection.objects.link(pole)

    ik = wrist.constraints.new("IK")
    ik.name = "PistolTwoHandIK"
    ik.target = target
    ik.pole_target = pole
    ik.pole_angle = math.radians(90.0)
    ik.chain_count = 3
    ik.iterations = 256
    ik.use_rotation = True
    return target, pole


def evaluate_pose(kind: str) -> None:
    armature = bpy.data.objects["Object_4"]
    # Frame 155 is the source asset's stable two-hand firing pose. Frame 0 is
    # the reload-start pose with the support arm lifted away from the weapon.
    bpy.context.scene.frame_set(155)
    if kind == "pistol_service":
        # Keep the source firing pose and pull the support wrist toward the
        # pistol's short frame. This keeps both elbows below the sight line
        # instead of folding the support arm across the primary hand.
        add_pistol_ik(
            armature,
            Vector((-7.0, -10.0, 1.0)),
            Vector((12.0, -2.0, -12.0)),
            180.0,
        )
    elif kind == "pistol_large":
        add_pistol_ik(
            armature,
            Vector((-7.0, -10.5, 1.0)),
            Vector((14.0, -2.0, -12.0)),
            180.0,
        )
    bpy.context.view_layer.update()


def mesh_copy_evaluated(
    source: bpy.types.Object,
    object_name: str,
    retained_vertices: set[int],
) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    mesh.name = f"{object_name}Mesh"
    edit_mesh = bmesh.new()
    edit_mesh.from_mesh(mesh)
    edit_mesh.verts.ensure_lookup_table()
    discarded = [
        vertex
        for vertex in edit_mesh.verts
        if vertex.index not in retained_vertices
    ]
    bmesh.ops.delete(edit_mesh, geom=discarded, context="VERTS")
    edit_mesh.to_mesh(mesh)
    edit_mesh.free()
    mesh.update()
    result = bpy.data.objects.new(object_name, mesh)
    bpy.context.collection.objects.link(result)
    result.matrix_world = source.matrix_world.copy()
    return result


def evaluated_component_centers(source: bpy.types.Object) -> list[tuple[int, Vector, list[int]]]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        adjacency = {index: set() for index in range(len(mesh.vertices))}
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
            if len(component) >= 100:
                components.append(component)

        result = []
        for component in components:
            center = sum((mesh.vertices[index].co for index in component), Vector()) / len(component)
            result.append((len(component), center, sorted(component)))
        return sorted(result, key=lambda item: item[0], reverse=True)
    finally:
        evaluated.to_mesh_clear()


def hand_contact_center(
    components: list[tuple[int, Vector, list[int]]],
    palm_origin: Vector,
) -> Vector:
    # Each glove is split into a palm and finger component in the source GLB.
    # Pick the two components nearest the palm bone and average their actual
    # evaluated vertices instead of using the wrist/palm bone origin.
    nearest = sorted(
        components,
        key=lambda item: (item[1] - palm_origin).length,
    )[:2]
    total = sum(item[0] for item in nearest)
    return sum((item[1] * item[0] for item in nearest), Vector()) / total


def frame_at(position: Vector, basis_source: Matrix) -> Matrix:
    result = basis_source.to_3x3().to_4x4()
    result.translation = position
    return result


def weapon_cross_section_frame(
    weapon_frame: Matrix,
    contact_position: Vector,
) -> Matrix:
    weapon_inverse = weapon_frame.inverted()
    contact_local = weapon_inverse @ contact_position
    weapon_mesh = bpy.data.objects["base_smg45_0"]
    vertices_local = [
        weapon_inverse @ (weapon_mesh.matrix_world @ vertex.co)
        for vertex in weapon_mesh.data.vertices
    ]
    nearby = [
        vertex
        for vertex in vertices_local
        if abs(vertex.y - contact_local.y) <= 3.0
    ]
    if not nearby:
        raise RuntimeError("Unable to resolve authored support-hand weapon section")
    section_local = Vector(
        (
            (min(vertex.x for vertex in nearby) + max(vertex.x for vertex in nearby)) * 0.5,
            contact_local.y,
            (min(vertex.z for vertex in nearby) + max(vertex.z for vertex in nearby)) * 0.5,
        )
    )
    result = weapon_frame.copy()
    result.translation = weapon_frame @ section_local
    return result


def arm_component_vertices(
    components: list[tuple[int, Vector, list[int]]],
    armature: bpy.types.Object,
) -> tuple[set[int], set[int]]:
    right_palm = bone_world_matrix(armature, "R_palm_039").translation
    left_palm = bone_world_matrix(armature, "L_palm_015").translation
    right_hand = sorted(
        range(len(components)),
        key=lambda index: (components[index][1] - right_palm).length,
    )[:2]
    left_hand = sorted(
        range(len(components)),
        key=lambda index: (components[index][1] - left_palm).length,
    )[:2]
    if set(right_hand) & set(left_hand):
        raise RuntimeError("Unable to separate authored left and right glove components")

    right_chain = sum(
        (
            bone_world_matrix(armature, bone).translation
            for bone in ("R_arm_024", "R_elbow_025", "R_wrist_026")
        ),
        Vector(),
    ) / 3.0
    left_chain = sum(
        (
            bone_world_matrix(armature, bone).translation
            for bone in ("L_arm_01", "L_elbow_02", "L_wrist_03")
        ),
        Vector(),
    ) / 3.0
    assigned = set(right_hand) | set(left_hand)
    right_components = list(right_hand)
    left_components = list(left_hand)
    for index, (_, center, _) in enumerate(components):
        if index in assigned:
            continue
        if (center - right_chain).length <= (center - left_chain).length:
            right_components.append(index)
        else:
            left_components.append(index)

    right_vertices = {
        vertex
        for index in right_components
        for vertex in components[index][2]
    }
    left_vertices = {
        vertex
        for index in left_components
        for vertex in components[index][2]
    }
    if not right_vertices or not left_vertices or right_vertices & left_vertices:
        raise RuntimeError("Authored arm component partition is invalid")
    return right_vertices, left_vertices


def add_marker(
    root: bpy.types.Object,
    name: str,
    position_source: Vector,
    transform_source: Matrix,
) -> bpy.types.Object:
    marker = bpy.data.objects.new(name, None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 0.04
    bpy.context.collection.objects.link(marker)
    marker.parent = root
    # The glTF exporter applies a 180-degree Z axis conversion to this baked
    # mesh. Apply the same conversion to markers before export; otherwise the
    # marker nodes keep their source Y sign and land outside the visible hand.
    export_basis = Matrix.Rotation(math.pi, 4, "Z")
    converted = export_basis @ transform_source
    marker.location = (export_basis @ position_source) * SOURCE_TO_METERS
    marker.rotation_mode = "QUATERNION"
    marker.rotation_quaternion = converted.to_quaternion()
    return marker


def export_static_arms(kind: str, output_name: str) -> None:
    import_source(0)
    cap_authored_sleeves()
    armature = bpy.data.objects["Object_4"]
    source_mesh = bpy.data.objects["Object_7"]
    bpy.context.scene.frame_set(155)
    bpy.context.view_layer.update()
    reference_components = evaluated_component_centers(source_mesh)
    reference_left_palm = bone_world_matrix(armature, "L_palm_015")
    reference_left_contact = hand_contact_center(
        reference_components,
        reference_left_palm.translation,
    )
    reference_contact_frame = frame_at(reference_left_contact, reference_left_palm)
    reference_weapon_frame = bpy.data.objects["smg45"].matrix_world.copy()
    reference_support_frame = weapon_cross_section_frame(
        reference_weapon_frame,
        reference_left_contact,
    )
    contact_to_support = reference_contact_frame.inverted() @ reference_support_frame

    evaluate_pose(kind)
    components = evaluated_component_centers(source_mesh)
    right_vertices, left_vertices = arm_component_vertices(components, armature)
    right_palm = bone_world_matrix(armature, "R_palm_039")
    left_palm = bone_world_matrix(armature, "L_palm_015")
    weapon_grip = bpy.data.objects["smg45"].matrix_world.copy()
    right_palm_contact = hand_contact_center(components, right_palm.translation)
    left_palm_contact = hand_contact_center(components, left_palm.translation)
    left_contact_frame = frame_at(left_palm_contact, left_palm)
    left_grip = left_contact_frame @ contact_to_support
    right_wrist = bone_world_matrix(armature, "R_wrist_026")
    left_wrist = bone_world_matrix(armature, "L_wrist_03")

    root = bpy.data.objects.new("StaticFirstPersonArms", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    bpy.context.collection.objects.link(root)

    right_arm = bpy.data.objects.new("RightArm", None)
    left_arm = bpy.data.objects.new("LeftArm", None)
    for arm in (right_arm, left_arm):
        arm.empty_display_type = "PLAIN_AXES"
        arm.empty_display_size = 0.06
        bpy.context.collection.objects.link(arm)
        arm.parent = root

    for arm, object_name, retained_vertices in (
        (right_arm, "RightArmMesh", right_vertices),
        (left_arm, "LeftArmMesh", left_vertices),
    ):
        mesh = mesh_copy_evaluated(source_mesh, object_name, retained_vertices)
        mesh.parent = arm
        mesh.location = Vector((0.0, 0.0, 0.0))
        mesh.rotation_mode = "QUATERNION"
        mesh.rotation_quaternion = (0.0, 0.0, 0.0, 1.0)
        mesh.scale = Vector((SOURCE_TO_METERS, SOURCE_TO_METERS, SOURCE_TO_METERS))
        for polygon in mesh.data.polygons:
            polygon.use_smooth = True

    add_marker(right_arm, "RightPalmFrame", right_palm_contact, right_palm)
    add_marker(
        left_arm,
        "LeftPalmFrame",
        left_palm_contact,
        left_palm,
    )
    add_marker(right_arm, "RightWristFrame", right_wrist.translation, right_wrist)
    add_marker(left_arm, "LeftWristFrame", left_wrist.translation, left_wrist)
    add_marker(right_arm, "RightGripFrame", weapon_grip.translation, weapon_grip)
    add_marker(left_arm, "LeftGripFrame", left_grip.translation, left_grip)

    for obj in list(bpy.context.scene.objects):
        if obj is root or obj in root.children_recursive:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)

    output_path = OUTPUT_DIR / output_name
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_apply=False,
        export_image_format="AUTO",
        export_yup=True,
    )
    print(f"Wrote {output_path}")


def main() -> None:
    if not SOURCE_GLB.exists():
        raise FileNotFoundError(SOURCE_GLB)
    # The runtime source is CC BY 4.0 and may be adapted. The existing DCC cap
    # pass closes the authored sleeve openings before the static export.
    export_static_arms("rifle", "smg45_rifle_arms.glb")
    export_static_arms("pistol_service", "smg45_pistol_service_arms.glb")
    export_static_arms("pistol_large", "smg45_pistol_large_arms.glb")


if __name__ == "__main__":
    main()
