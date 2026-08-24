"""Build clean static first-person arm poses from the original DJMaesen GLB.

The tracked runtime SMG asset deliberately contains an animated rig.  This
builder takes the unmodified source GLB, evaluates a single authored pose, and
exports a small static GLB with the production arm mesh plus explicit palm and
wrist frames.  Runtime code can then translate the pose to a weapon grip without
ever scaling or accumulating rotations on the arm mesh.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup --python scripts/blender/build_first_person_arms.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


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
    bpy.context.scene.frame_set(0)
    if kind == "pistol_service":
        # The right hand stays in a compact firing grip.  The left wrist is
        # pulled back beside it, with the elbow kept low and outside the sight
        # line so the two hands form a stable isosceles stance.
        add_pistol_ik(
            armature,
            Vector((-13.0, -1.5, 2.8)),
            Vector((12.0, 8.0, -12.0)),
            180.0,
        )
    elif kind == "pistol_large":
        add_pistol_ik(
            armature,
            Vector((-12.0, -1.5, 2.8)),
            Vector((14.0, 8.0, -12.0)),
            180.0,
        )
    bpy.context.view_layer.update()


def mesh_copy_evaluated(source: bpy.types.Object) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    mesh.name = "AuthoredArmsMesh"
    result = bpy.data.objects.new("AuthoredArms", mesh)
    bpy.context.collection.objects.link(result)
    result.matrix_world = source.matrix_world.copy()
    return result


def add_marker(
    root: bpy.types.Object,
    name: str,
    transform_source: Matrix,
) -> bpy.types.Object:
    marker = bpy.data.objects.new(name, None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 0.04
    bpy.context.collection.objects.link(marker)
    marker.parent = root
    source_position = transform_source.translation * SOURCE_TO_METERS
    source_basis = transform_source.to_3x3()
    marker.location = source_position
    marker.rotation_mode = "QUATERNION"
    marker.rotation_quaternion = source_basis.to_quaternion()
    return marker


def export_static_arms(kind: str, output_name: str) -> None:
    import_source(0)
    evaluate_pose(kind)
    armature = bpy.data.objects["Object_4"]
    source_mesh = bpy.data.objects["Object_7"]

    root = bpy.data.objects.new("StaticFirstPersonArms", None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.08
    bpy.context.collection.objects.link(root)

    mesh = mesh_copy_evaluated(source_mesh)
    mesh.parent = root
    mesh.location = Vector((0.0, 0.0, 0.0))
    mesh.rotation_mode = "QUATERNION"
    mesh.rotation_quaternion = (0.0, 0.0, 0.0, 1.0)
    mesh.scale = Vector((SOURCE_TO_METERS, SOURCE_TO_METERS, SOURCE_TO_METERS))
    for polygon in mesh.data.polygons:
        polygon.use_smooth = True

    add_marker(root, "RightPalmFrame", bone_world_matrix(armature, "R_palm_039"))
    add_marker(root, "LeftPalmFrame", bone_world_matrix(armature, "L_palm_015"))
    add_marker(root, "RightWristFrame", bone_world_matrix(armature, "R_wrist_026"))
    add_marker(root, "LeftWristFrame", bone_world_matrix(armature, "L_wrist_03"))

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
    # The runtime source is CC BY 4.0 and may be adapted, but never run the
    # historical sleeve-extension/capping pass for these static poses.
    export_static_arms("rifle", "smg45_rifle_arms.glb")
    export_static_arms("pistol_service", "smg45_pistol_service_arms.glb")
    export_static_arms("pistol_large", "smg45_pistol_large_arms.glb")


if __name__ == "__main__":
    main()
