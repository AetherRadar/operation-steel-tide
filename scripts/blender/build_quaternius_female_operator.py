"""Build the animated Quaternius female field operator.

The CC0 Ultimate Modular Women Soldier source uses a compact humanoid rig.
This script gives that authored mesh the same runtime skeleton contract and
Quaternius Universal Animation Library actions as the existing BAMEN operator.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup \
        --python scripts/blender/build_quaternius_female_operator.py
"""

from __future__ import annotations

import importlib.util
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = (
    REPO_ROOT
    / "source_art"
    / "third_party"
    / "quaternius_modular_women"
    / "Soldier.blend"
)
OUTPUT_BLEND = (
    REPO_ROOT
    / "source_art"
    / "third_party"
    / "quaternius_modular_women"
    / "quaternius_female_soldier_animated.blend"
)
OUTPUT_GLB = (
    REPO_ROOT
    / "assets"
    / "models"
    / "quaternius_female_operator"
    / "quaternius_female_operator.glb"
)
HELPER_PATH = Path(__file__).with_name("build_animated_bamen_operator.py")


TARGET_BONE_RENAMES = {
    "Hips": "mixamorig:Hips",
    "Abdomen": "mixamorig:Spine",
    "Torso": "mixamorig:Spine1",
    "Chest": "mixamorig:Spine2",
    "Neck": "mixamorig:Neck",
    "Head": "mixamorig:Head",
    "Shoulder.L": "mixamorig:LeftShoulder",
    "UpperArm.L": "mixamorig:LeftArm",
    "LowerArm.L": "mixamorig:LeftForeArm",
    "Hand.L": "mixamorig:LeftHand",
    "Shoulder.R": "mixamorig:RightShoulder",
    "UpperArm.R": "mixamorig:RightArm",
    "LowerArm.R": "mixamorig:RightForeArm",
    "Hand.R": "mixamorig:RightHand",
    "UpperLeg.L": "mixamorig:LeftUpLeg",
    "LowerLeg.L": "mixamorig:LeftLeg",
    "Foot.L": "mixamorig:LeftFoot",
    "UpperLeg.R": "mixamorig:RightUpLeg",
    "LowerLeg.R": "mixamorig:RightLeg",
    "Foot.R": "mixamorig:RightFoot",
}


RETARGET_BONE_MAP = {
    "pelvis": "mixamorig:Hips",
    "spine_01": "mixamorig:Spine",
    "spine_02": "mixamorig:Spine1",
    "spine_03": "mixamorig:Spine2",
    "neck_01": "mixamorig:Neck",
    "Head": "mixamorig:Head",
    "clavicle_l": "mixamorig:LeftShoulder",
    "upperarm_l": "mixamorig:LeftArm",
    "lowerarm_l": "mixamorig:LeftForeArm",
    "hand_l": "mixamorig:LeftHand",
    "clavicle_r": "mixamorig:RightShoulder",
    "upperarm_r": "mixamorig:RightArm",
    "lowerarm_r": "mixamorig:RightForeArm",
    "hand_r": "mixamorig:RightHand",
    "thigh_l": "mixamorig:LeftUpLeg",
    "calf_l": "mixamorig:LeftLeg",
    "foot_l": "mixamorig:LeftFoot",
    "thigh_r": "mixamorig:RightUpLeg",
    "calf_r": "mixamorig:RightLeg",
    "foot_r": "mixamorig:RightFoot",
}


def load_animation_helpers():
    spec = importlib.util.spec_from_file_location(
        "steel_tide_operator_animation_helpers",
        HELPER_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load animation helpers from {HELPER_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.BONE_MAP = RETARGET_BONE_MAP
    module.OUTPUT_BLEND = OUTPUT_BLEND
    module.OUTPUT_GLB = OUTPUT_GLB
    return module


def rename_target_rig(target: bpy.types.Object) -> None:
    for old_name, new_name in TARGET_BONE_RENAMES.items():
        bone = target.data.bones.get(old_name)
        if bone is None:
            raise RuntimeError(f"Female operator source is missing bone {old_name}")
        bone.name = new_name

    finger_names = ("Index", "Middle", "Ring", "Pinky", "Thumb")
    for source_side, target_side in (("L", "Left"), ("R", "Right")):
        for finger in finger_names:
            for segment in range(1, 4):
                old_name = f"{finger}{segment}.{source_side}"
                bone = target.data.bones.get(old_name)
                if bone is not None:
                    bone.name = f"mixamorig:{target_side}Hand{finger}{segment}"


def prepare_scene() -> tuple[bpy.types.Object, bpy.types.Object]:
    target = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    feet = bpy.data.objects.get("Soldier_Feet")
    if feet is None or feet.type != "MESH":
        raise RuntimeError("Female operator source is missing Soldier_Feet")

    root = bpy.data.objects.new("QuaterniusFemaleOperator", None)
    bpy.context.collection.objects.link(root)
    target.parent = root
    target.name = "QuaterniusFemaleOperatorRig"
    target.data.name = "QuaterniusFemaleOperatorSkeleton"
    mesh_names = {
        "Soldier_Body": "FemaleOperatorBody",
        "Soldier_Feet": "FemaleOperatorFeet",
        "Soldier_Head": "FemaleOperatorHead",
        "Soldier_Legs": "FemaleOperatorLegs",
    }
    meshes: list[bpy.types.Object] = []
    for old_name, new_name in mesh_names.items():
        mesh = bpy.data.objects.get(old_name)
        if mesh is None or mesh.type != "MESH":
            raise RuntimeError(f"Female operator source is missing {old_name}")
        meshes.append(mesh)
        mesh.name = new_name

    # The source stores symmetrical geometry behind Mirror modifiers whose
    # vertex-group flip depends on the original `.L` / `.R` bone names. Apply
    # those modifiers before renaming the rig to the runtime Mixamo contract;
    # otherwise the mirrored half keeps left-side weights and stretches across
    # the character whenever a combat upper-body pose is played.
    bpy.ops.object.select_all(action="DESELECT")
    for mesh in meshes:
        for modifier in list(mesh.modifiers):
            if modifier.type != "MIRROR":
                continue
            modifier.use_mirror_vertex_groups = True
            mesh.select_set(True)
            bpy.context.view_layer.objects.active = mesh
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            mesh.select_set(False)
        if mesh.data.validate(verbose=True, clean_customdata=True):
            print(f"QUATERNIUS_FEMALE_OPERATOR_REPAIRED mesh={mesh.name}")

    rename_target_rig(target)
    return target, feet


def main() -> None:
    helpers = load_animation_helpers()
    for path in (SOURCE_BLEND, helpers.UAL1_GLB, helpers.UAL2_GLB, helpers.M4_GLB):
        helpers.require_file(path)

    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    target, target_feet = prepare_scene()
    target.data.pose_position = "POSE"
    target.animation_data_create()
    target.animation_data.action = None
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    helpers.reset_pose(target)

    ual1 = helpers.import_animation_library(helpers.UAL1_GLB, "ual1")
    ual2 = helpers.import_animation_library(helpers.UAL2_GLB, "ual2")
    sources = {"ual1": ual1, "ual2": ual2}
    generated: dict[str, bpy.types.Action] = {}
    for output_name, (library, source_name, _) in helpers.ACTION_SOURCES.items():
        generated[output_name] = helpers.retarget_action(
            target,
            sources[library],
            helpers.find_source_action(library, source_name),
            output_name,
        )

    generated["aim_idle"] = helpers.author_rifle_hold(
        target,
        generated["aim_idle"],
        "aim_idle",
        helpers.RIFLE_AIM_ORIGIN,
        helpers.RIFLE_AIM_FORWARD,
        helpers.RIFLE_AIM_UP,
        Vector((0.0, -0.50, 0.866)),
        Vector((0.0, -0.62, 0.785)),
        remove_source=True,
    )
    target.animation_data.action = generated["aim_idle"]
    scene.frame_set(0)
    bpy.context.view_layer.update()
    aim_hand_rotation = helpers.rotation_only(
        target.matrix_world @ target.pose.bones["mixamorig:RightHand"].matrix
    )
    ready_hand_rotation = (
        helpers.rifle_world_rotation(helpers.RIFLE_READY_FORWARD, helpers.RIFLE_READY_UP)
        @ helpers.rifle_world_rotation(helpers.RIFLE_AIM_FORWARD, helpers.RIFLE_AIM_UP).inverted()
        @ aim_hand_rotation
    ).normalized()
    generated["ready_idle"] = helpers.author_rifle_hold(
        target,
        generated["idle"],
        "ready_idle",
        helpers.RIFLE_READY_ORIGIN,
        helpers.RIFLE_READY_FORWARD,
        helpers.RIFLE_READY_UP,
        Vector((0.0, -0.12, 0.993)).normalized(),
        Vector((0.0, -0.18, 0.984)).normalized(),
        right_hand_world_rotation=ready_hand_rotation,
    )
    for output_name, source_name in helpers.READY_LOCOMOTION_SOURCES.items():
        generated[output_name] = helpers.author_upper_body_locomotion(
            target,
            generated[source_name],
            generated["ready_idle"],
            output_name,
        )
    for output_name, source_name in helpers.AIM_LOCOMOTION_SOURCES.items():
        generated[output_name] = helpers.author_upper_body_locomotion(
            target,
            generated[source_name],
            generated["aim_idle"],
            output_name,
        )

    helpers.ground_action(target, target_feet, generated["prone_idle"])
    helpers.ground_action(target, target_feet, generated["prone_crawl"])
    stale_prone_idle = generated["prone_idle"]
    target.animation_data.action = None
    bpy.data.actions.remove(stale_prone_idle)
    generated["prone_idle"] = helpers.author_pose_hold(
        target,
        generated["prone_crawl"],
        12,
        "prone_idle",
    )
    helpers.author_downed_hold(target, generated["death"])
    helpers.reset_pose(target)
    helpers.cleanup_sources([ual1, ual2])

    right_hand = tuple(
        target.matrix_world
        @ target.data.bones["mixamorig:RightHand"].head_local
    )
    helpers.add_socket(
        target,
        "WeaponSocket",
        "mixamorig:RightHand",
        world_location=right_hand,
        world_rotation_degrees=(0.0, 0.0, 180.0),
    )
    helpers.add_socket(
        target,
        "BackWeaponSocket",
        "mixamorig:Spine2",
        world_location=(0.22, 0.14, 1.28),
        world_rotation_degrees=(90.0, 0.0, -8.0),
    )
    helpers.add_socket(target, "HeadSocket", "mixamorig:Head")
    helpers.add_socket(target, "VestSocket", "mixamorig:Spine2")
    helpers.add_socket(target, "BackpackSocket", "mixamorig:Spine2")
    helpers.add_socket(target, "TeamPatchSocket", "mixamorig:LeftShoulder")
    helpers.set_action_export_metadata()
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    helpers.save_source()
    helpers.export_asset(target)
    print(
        "QUATERNIUS_FEMALE_OPERATOR_EXPORT "
        f"glb={OUTPUT_GLB} actions={sorted(action.name for action in bpy.data.actions)}"
    )


if __name__ == "__main__":
    main()
