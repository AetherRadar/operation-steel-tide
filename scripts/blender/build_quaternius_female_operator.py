"""Build the five animated Quaternius extraction operators.

The CC0 Ultimate Modular Women sources use one compact humanoid rig. This
script gives five authored characters the same runtime skeleton, socket, and
Quaternius Universal Animation Library action contract as the garrison model.

Run from the repository root with Blender 4.5 LTS or newer:
    blender --background --factory-startup \
        --python scripts/blender/build_quaternius_female_operator.py
"""

from __future__ import annotations

import argparse
import importlib.util
from pathlib import Path
import sys

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / "source_art" / "third_party" / "quaternius_modular_women"
OUTPUT_ROOT = REPO_ROOT / "assets" / "models" / "quaternius_operators"
HELPER_PATH = Path(__file__).with_name("build_animated_bamen_operator.py")


VARIANTS = {
    "viper": {
        "source": "Soldier.blend",
        "prefix": "Soldier",
        "colors": {
            "Swat": (0.34, 0.085, 0.018, 1.0),
            "Grey": (0.12, 0.13, 0.12, 1.0),
            "Skin": (0.54, 0.31, 0.16, 1.0),
        },
    },
    "heron": {
        "source": "Worker.blend",
        "prefix": "Worker",
        "colors": {
            "Worker_Vest": (0.018, 0.30, 0.17, 1.0),
            "Worker_Yellow": (0.22, 0.82, 0.48, 1.0),
            "White": (0.72, 0.78, 0.72, 1.0),
            "Brown_02": (0.08, 0.11, 0.10, 1.0),
            "Brown2": (0.035, 0.055, 0.05, 1.0),
            "Skin": (0.72, 0.49, 0.31, 1.0),
        },
    },
    "lynx": {
        "source": "SciFi.blend",
        "prefix": "SciFi",
        "colors": {
            "LightBlue": (0.025, 0.40, 0.72, 1.0),
            "Blue": (0.008, 0.07, 0.18, 1.0),
            "Metal": (0.24, 0.30, 0.34, 1.0),
            "Skin": (0.34, 0.17, 0.095, 1.0),
        },
    },
    "magpie": {
        "source": "Adventurer.blend",
        "prefix": "Adventurer",
        "colors": {
            "LightGreen": (0.46, 0.25, 0.035, 1.0),
            "Green": (0.12, 0.15, 0.035, 1.0),
            "Gold": (0.83, 0.50, 0.07, 1.0),
            "White": (0.26, 0.22, 0.12, 1.0),
            "Skin": (0.58, 0.34, 0.19, 1.0),
        },
    },
    "jackal": {
        "source": "Punk.blend",
        "prefix": "Punk",
        "colors": {
            "Pink": (0.38, 0.10, 0.62, 1.0),
            "Black": (0.025, 0.018, 0.035, 1.0),
            "Grey": (0.16, 0.13, 0.19, 1.0),
            "Skin": (0.46, 0.24, 0.13, 1.0),
        },
    },
}


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


def load_animation_helpers(output_glb: Path, output_blend: Path):
    spec = importlib.util.spec_from_file_location(
        "steel_tide_operator_animation_helpers",
        HELPER_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load animation helpers from {HELPER_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.BONE_MAP = RETARGET_BONE_MAP
    module.OUTPUT_BLEND = output_blend
    module.OUTPUT_GLB = output_glb
    return module


def rename_target_rig(target: bpy.types.Object) -> None:
    for old_name, new_name in TARGET_BONE_RENAMES.items():
        aliases = (old_name, old_name.replace("Hand.", "Wrist."))
        bone = next((target.data.bones.get(alias) for alias in aliases if target.data.bones.get(alias)), None)
        if bone is None:
            raise RuntimeError(f"Quaternius operator source is missing bone {old_name}")
        bone.name = new_name

    finger_names = ("Index", "Middle", "Ring", "Pinky", "Thumb")
    for source_side, target_side in (("L", "Left"), ("R", "Right")):
        for finger in finger_names:
            for segment in range(1, 4):
                old_name = f"{finger}{segment}.{source_side}"
                bone = target.data.bones.get(old_name)
                if bone is not None:
                    bone.name = f"mixamorig:{target_side}Hand{finger}{segment}"


def apply_role_materials(meshes: list[bpy.types.Object], slug: str) -> None:
    overrides = VARIANTS[slug]["colors"]
    materials = {
        slot.material
        for mesh in meshes
        for slot in mesh.material_slots
        if slot.material is not None
    }
    for material in materials:
        original_name = material.name
        if original_name in overrides:
            material.diffuse_color = overrides[original_name]
            if material.use_nodes and material.node_tree is not None:
                principled = material.node_tree.nodes.get("Principled BSDF")
                if principled is not None:
                    principled.inputs["Base Color"].default_value = overrides[original_name]
        material.name = f"{slug.title()}_{original_name}"


def prepare_scene(prefix: str, slug: str) -> tuple[bpy.types.Object, bpy.types.Object]:
    target = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    feet = bpy.data.objects.get(f"{prefix}_Feet")
    if feet is None or feet.type != "MESH":
        raise RuntimeError(f"{slug} source is missing {prefix}_Feet")

    root = bpy.data.objects.new("QuaterniusOperator", None)
    bpy.context.collection.objects.link(root)
    target.parent = root
    target.name = "QuaterniusOperatorRig"
    target.data.name = "QuaterniusOperatorSkeleton"
    mesh_names = {
        f"{prefix}_Body": "OperatorBody",
        f"{prefix}_Feet": "OperatorFeet",
        f"{prefix}_Head": "OperatorHead",
        f"{prefix}_Legs": "OperatorLegs",
    }
    meshes: list[bpy.types.Object] = []
    for old_name, new_name in mesh_names.items():
        mesh = bpy.data.objects.get(old_name)
        if mesh is None or mesh.type != "MESH":
            raise RuntimeError(f"{slug} source is missing {old_name}")
        meshes.append(mesh)
        mesh.name = new_name

    # Some presets include a staged prop (for example SciFi's pistol). Runtime
    # weapons are attached through the shared socket contract, so only the
    # authored four-piece character is allowed under the exported rig.
    allowed = {target, root, *meshes}
    for obj in list(bpy.context.scene.objects):
        if obj not in allowed:
            bpy.data.objects.remove(obj, do_unlink=True)

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
            print(f"QUATERNIUS_OPERATOR_REPAIRED variant={slug} mesh={mesh.name}")

    apply_role_materials(meshes, slug)
    rename_target_rig(target)
    return target, feet


def build_variant(slug: str, save_working_blend: bool) -> None:
    variant = VARIANTS[slug]
    source_blend = SOURCE_ROOT / variant["source"]
    output_glb = OUTPUT_ROOT / f"{slug}.glb"
    output_blend = SOURCE_ROOT / f"quaternius_{slug}_animated.blend"
    helpers = load_animation_helpers(output_glb, output_blend)
    for path in (source_blend, helpers.UAL1_GLB, helpers.UAL2_GLB, helpers.M4_GLB):
        helpers.require_file(path)

    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    target, target_feet = prepare_scene(variant["prefix"], slug)
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
    if save_working_blend:
        helpers.save_source()
    helpers.export_asset(target)
    print(
        "QUATERNIUS_OPERATOR_EXPORT "
        f"variant={slug} glb={output_glb} "
        f"actions={sorted(action.name for action in bpy.data.actions)}"
    )


def parse_args() -> argparse.Namespace:
    script_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--variant",
        choices=("all", *VARIANTS.keys()),
        default="all",
        help="Build one operator or the complete five-role roster.",
    )
    parser.add_argument(
        "--save-working-blends",
        action="store_true",
        help="Also save the large retargeted Blender working files.",
    )
    return parser.parse_args(script_args)


def main() -> None:
    args = parse_args()
    variants = VARIANTS if args.variant == "all" else (args.variant,)
    for slug in variants:
        build_variant(slug, args.save_working_blends)


if __name__ == "__main__":
    main()
