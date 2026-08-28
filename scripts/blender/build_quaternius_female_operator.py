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
from math import exp
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = REPO_ROOT / "source_art" / "third_party" / "quaternius_modular_women"
OUTPUT_ROOT = REPO_ROOT / "assets" / "models" / "quaternius_operators"
HELPER_PATH = Path(__file__).with_name("build_animated_bamen_operator.py")


QUATERNIUS_AIM_CHEST_OFFSET = Vector((0.06, -0.27, -0.10))
QUATERNIUS_READY_CHEST_OFFSET = Vector((0.04, -0.23, -0.20))
QUATERNIUS_AIM_SUPPORT_HAND_OFFSET = Vector((-0.01562, 0.01559, 0.08699))
QUATERNIUS_READY_SUPPORT_HAND_OFFSET = Vector((-0.22, 0.0, 0.105))
QUATERNIUS_VIPER_AIM_SUPPORT_HAND_OFFSET = Vector((-0.02979, 0.01925, 0.08770))
QUATERNIUS_VIPER_READY_SUPPORT_HAND_OFFSET = Vector((-0.2536, 0.0045, 0.1079))


VARIANTS = {
    "viper": {
        "source": "Soldier.blend",
        "prefix": "Soldier",
        "colors": {
            "Swat": (0.34, 0.085, 0.018, 1.0),
            "Grey": (0.12, 0.13, 0.12, 1.0),
            "Skin": (0.54, 0.31, 0.16, 1.0),
        },
        "material_profiles": {
            "Swat": "fabric",
            "Grey": "polymer",
            "Black": "polymer",
            "Skin": "skin",
            "Hair_Brown": "hair",
            "Brown": "leather",
        },
        "mesh_material_profiles": {
            "OperatorBody": {"Grey": "fabric", "Black": "fabric"},
            "OperatorHead": {"Brown": "eye_white"},
        },
        "subdivision_levels": {"OperatorHead": 2, "default": 1},
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
        "material_profiles": {
            "Worker_Vest": "fabric",
            "Worker_Yellow": "polymer",
            "White": "fabric",
            "Brown_02": "fabric",
            "Brown2": "leather",
            "Black": "polymer",
            "Skin": "skin",
            "DarkBrown": "hair",
            "Brown": "leather",
        },
        "mesh_material_profiles": {
            "OperatorHead": {"Brown": "eye_white"},
        },
        "subdivision_levels": {"OperatorHead": 2, "default": 1},
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
        "material_profiles": {
            "LightBlue": "armor",
            "Blue": "armor",
            "Metal": "metal",
            "Black": "polymer",
            "Skin": "skin",
            "Hair_Black": "hair",
            "Brown": "leather",
        },
        "mesh_material_profiles": {
            "OperatorHead": {"Brown": "eye_white"},
        },
        "subdivision_levels": {"OperatorHead": 2, "default": 1},
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
        "material_profiles": {
            "LightGreen": "fabric",
            "Green": "fabric",
            "Gold": "metal",
            "White": "fabric",
            "Brown_02": "leather",
            "Brown2": "leather",
            "Skin": "skin",
            "Hair_Brown": "hair",
            "Brown": "leather",
        },
        "mesh_material_profiles": {
            "OperatorHead": {"Brown": "eye_white"},
        },
        # Adventurer's authored detail is concentrated in the torso equipment;
        # a second pass there keeps this lighter preset in the same runtime
        # density band as the other four operators.
        "subdivision_levels": {"OperatorHead": 2, "OperatorBody": 2, "default": 1},
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
        "material_profiles": {
            "Pink": "fabric",
            "Black": "leather",
            "Grey": "metal",
            "Skin": "skin",
            "Hair_Brown": "hair",
            "Brown": "leather",
        },
        "mesh_material_profiles": {
            "OperatorHead": {"Brown": "eye_white", "Pink": "hair"},
        },
        "subdivision_levels": {"OperatorHead": 2, "default": 1},
    },
}


FACE_REFINEMENT = {
    # Viper is the approved reference sculpt. The remaining presets preserve
    # the same restrained stylized language while deliberately changing eye
    # spacing, cheek volume, jaw taper, and lip proportions so the operators
    # do not inherit one mechanically duplicated face.
    "viper": {
        "eye_center_x": 0.0460,
        "eye_center_z": 1.6908,
        "eye_width_scale": 1.16,
        "eye_height_scale": 0.72,
        "iris_width_ratio": 0.50,
        "iris_height_ratio": 0.58,
        "iris_color": (0.095, 0.032, 0.010, 1.0),
        "eye_socket_depth": 0.0025,
        "upper_lid_depth": 0.0018,
        "lower_lid_depth": 0.0011,
        "nose_bridge_depth": 0.0020,
        "nose_tip_depth": 0.0010,
        "cheek_center_x": 0.058,
        "cheek_depth": 0.0027,
        "jaw_taper": 0.0038,
        "chin_depth": 0.0015,
        "mouth_center_z": 1.604,
        "upper_lip_z": 1.610,
        "lower_lip_z": 1.598,
        "upper_lip_width": 0.036,
        "lower_lip_width": 0.034,
        "upper_lip_depth": 0.0017,
        "lower_lip_depth": 0.0013,
        "mouth_crease_depth": 0.0036,
        "mouth_corner_depth": 0.0010,
        "lip_region_width": 0.034,
        "lip_region_height": 0.0075,
        "crease_half_width": 0.028,
        "lip_color": (0.50, 0.265, 0.155, 1.0),
        "crease_color": (0.18, 0.055, 0.030, 1.0),
    },
    "heron": {
        "eye_center_x": 0.0435,
        "eye_center_z": 1.6908,
        "eye_width_scale": 1.10,
        "eye_height_scale": 0.76,
        "iris_width_ratio": 0.50,
        "iris_height_ratio": 0.58,
        "iris_color": (0.105, 0.040, 0.014, 1.0),
        "eye_socket_depth": 0.0022,
        "upper_lid_depth": 0.0016,
        "lower_lid_depth": 0.0010,
        "nose_bridge_depth": 0.0018,
        "nose_tip_depth": 0.0008,
        "cheek_center_x": 0.052,
        "cheek_depth": 0.0032,
        "jaw_taper": 0.0048,
        "chin_depth": 0.0012,
        "mouth_center_z": 1.604,
        "upper_lip_z": 1.610,
        "lower_lip_z": 1.598,
        "upper_lip_width": 0.033,
        "lower_lip_width": 0.032,
        "upper_lip_depth": 0.0015,
        "lower_lip_depth": 0.0014,
        "mouth_crease_depth": 0.0032,
        "mouth_corner_depth": 0.0008,
        "lip_region_width": 0.032,
        "lip_region_height": 0.0075,
        "crease_half_width": 0.026,
        "lip_color": (0.62, 0.34, 0.235, 1.0),
        "crease_color": (0.22, 0.075, 0.045, 1.0),
    },
    "lynx": {
        "eye_center_x": 0.0495,
        "eye_center_z": 1.6908,
        "eye_width_scale": 1.08,
        "eye_height_scale": 0.70,
        "iris_width_ratio": 0.50,
        "iris_height_ratio": 0.58,
        "iris_color": (0.075, 0.025, 0.012, 1.0),
        "eye_socket_depth": 0.0028,
        "upper_lid_depth": 0.0020,
        "lower_lid_depth": 0.0009,
        "nose_bridge_depth": 0.0023,
        "nose_tip_depth": 0.0011,
        "cheek_center_x": 0.061,
        "cheek_depth": 0.0020,
        "jaw_taper": 0.0052,
        "chin_depth": 0.0019,
        "mouth_center_z": 1.604,
        "upper_lip_z": 1.610,
        "lower_lip_z": 1.598,
        "upper_lip_width": 0.030,
        "lower_lip_width": 0.029,
        "upper_lip_depth": 0.0013,
        "lower_lip_depth": 0.0010,
        "mouth_crease_depth": 0.0034,
        "mouth_corner_depth": 0.0011,
        "lip_region_width": 0.030,
        "lip_region_height": 0.0068,
        "crease_half_width": 0.024,
        "lip_color": (0.34, 0.155, 0.105, 1.0),
        "crease_color": (0.115, 0.038, 0.025, 1.0),
    },
    "magpie": {
        "eye_center_x": 0.0475,
        "eye_center_z": 1.6908,
        "eye_width_scale": 1.13,
        "eye_height_scale": 0.82,
        "iris_width_ratio": 0.50,
        "iris_height_ratio": 0.58,
        "iris_color": (0.085, 0.050, 0.016, 1.0),
        "eye_socket_depth": 0.0023,
        "upper_lid_depth": 0.0015,
        "lower_lid_depth": 0.0012,
        "nose_bridge_depth": 0.0018,
        "nose_tip_depth": 0.0009,
        "cheek_center_x": 0.055,
        "cheek_depth": 0.0034,
        "jaw_taper": 0.0056,
        "chin_depth": 0.0013,
        "mouth_center_z": 1.604,
        "upper_lip_z": 1.6105,
        "lower_lip_z": 1.5975,
        "upper_lip_width": 0.034,
        "lower_lip_width": 0.033,
        "upper_lip_depth": 0.0018,
        "lower_lip_depth": 0.0015,
        "mouth_crease_depth": 0.0032,
        "mouth_corner_depth": 0.0007,
        "lip_region_width": 0.0335,
        "lip_region_height": 0.0080,
        "crease_half_width": 0.027,
        "lip_color": (0.53, 0.275, 0.175, 1.0),
        "crease_color": (0.19, 0.058, 0.032, 1.0),
    },
    "jackal": {
        "eye_center_x": 0.0505,
        "eye_center_z": 1.6908,
        "eye_width_scale": 1.08,
        "eye_height_scale": 0.68,
        "iris_width_ratio": 0.50,
        "iris_height_ratio": 0.58,
        "iris_color": (0.075, 0.022, 0.018, 1.0),
        "eye_socket_depth": 0.0029,
        "upper_lid_depth": 0.0021,
        "lower_lid_depth": 0.0008,
        "nose_bridge_depth": 0.0024,
        "nose_tip_depth": 0.0012,
        "cheek_center_x": 0.062,
        "cheek_depth": 0.0021,
        "jaw_taper": 0.0028,
        "chin_depth": 0.0021,
        "mouth_center_z": 1.604,
        "upper_lip_z": 1.611,
        "lower_lip_z": 1.598,
        "upper_lip_width": 0.031,
        "lower_lip_width": 0.030,
        "upper_lip_depth": 0.0019,
        "lower_lip_depth": 0.0011,
        "mouth_crease_depth": 0.0037,
        "mouth_corner_depth": 0.0012,
        "lip_region_width": 0.031,
        "lip_region_height": 0.0072,
        "crease_half_width": 0.025,
        "lip_color": (0.41, 0.175, 0.135, 1.0),
        "crease_color": (0.14, 0.035, 0.040, 1.0),
    },
}


MATERIAL_PROFILES = {
    "skin": {
        "metallic": 0.0,
        "roughness": 0.47,
        "ior": 1.45,
        "specular": 0.26,
        "subsurface": 0.025,
        "coat": 0.0,
        "coat_roughness": 0.45,
        "sheen": 0.04,
    },
    "hair": {
        "metallic": 0.0,
        "roughness": 0.44,
        "ior": 1.55,
        "specular": 0.27,
        "subsurface": 0.0,
        "coat": 0.02,
        "coat_roughness": 0.40,
        "sheen": 0.16,
    },
    "fabric": {
        "metallic": 0.0,
        "roughness": 0.76,
        "ior": 1.45,
        "specular": 0.18,
        "subsurface": 0.0,
        "coat": 0.0,
        "coat_roughness": 0.5,
        "sheen": 0.22,
    },
    "armor": {
        "metallic": 0.0,
        "roughness": 0.48,
        "ior": 1.50,
        "specular": 0.24,
        "subsurface": 0.0,
        "coat": 0.04,
        "coat_roughness": 0.38,
        "sheen": 0.0,
    },
    "polymer": {
        "metallic": 0.0,
        "roughness": 0.58,
        "ior": 1.48,
        "specular": 0.22,
        "subsurface": 0.0,
        "coat": 0.02,
        "coat_roughness": 0.45,
        "sheen": 0.0,
    },
    "metal": {
        "metallic": 0.78,
        "roughness": 0.28,
        "ior": 1.50,
        "specular": 0.38,
        "subsurface": 0.0,
        "coat": 0.03,
        "coat_roughness": 0.32,
        "sheen": 0.0,
    },
    "leather": {
        "metallic": 0.0,
        "roughness": 0.52,
        "ior": 1.47,
        "specular": 0.22,
        "subsurface": 0.0,
        "coat": 0.02,
        "coat_roughness": 0.44,
        "sheen": 0.06,
    },
    "eye_white": {
        "metallic": 0.0,
        "roughness": 0.40,
        "ior": 1.38,
        "specular": 0.25,
        "subsurface": 0.0,
        "coat": 0.02,
        "coat_roughness": 0.36,
        "sheen": 0.0,
        "base_color": (0.60, 0.52, 0.40, 1.0),
    },
    "iris": {
        "metallic": 0.0,
        "roughness": 0.38,
        "ior": 1.40,
        "specular": 0.26,
        "subsurface": 0.0,
        "coat": 0.04,
        "coat_roughness": 0.28,
        "sheen": 0.0,
    },
    "lips": {
        "metallic": 0.0,
        "roughness": 0.52,
        "ior": 1.43,
        "specular": 0.22,
        "subsurface": 0.02,
        "coat": 0.0,
        "coat_roughness": 0.5,
        "sheen": 0.02,
    },
    "mouth_crease": {
        "metallic": 0.0,
        "roughness": 0.62,
        "ior": 1.42,
        "specular": 0.16,
        "subsurface": 0.0,
        "coat": 0.0,
        "coat_roughness": 0.5,
        "sheen": 0.0,
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


def configure_quaternius_rifle_origins(target: bpy.types.Object, helpers) -> None:
    """Place both rifle grips ahead of this rig's upper-chest anatomy."""

    upper_chest = (
        target.matrix_world
        @ target.data.bones["mixamorig:Spine2"].head_local
    )
    helpers.RIFLE_AIM_ORIGIN = upper_chest + QUATERNIUS_AIM_CHEST_OFFSET
    helpers.RIFLE_READY_ORIGIN = upper_chest + QUATERNIUS_READY_CHEST_OFFSET
    print(
        "QUATERNIUS_RIFLE_ORIGINS "
        f"aim={tuple(round(value, 4) for value in helpers.RIFLE_AIM_ORIGIN)} "
        f"ready={tuple(round(value, 4) for value in helpers.RIFLE_READY_ORIGIN)}"
    )


def capture_aim_hand_rotation(
    target: bpy.types.Object,
    source: bpy.types.Action,
    helpers,
    slug: str,
    rifle_origin: Vector,
    label: str,
) -> Quaternion:
    """Capture the rig-specific wrist roll from a temporary rifle pose."""

    reference = helpers.author_rifle_hold(
        target,
        source,
        f"aim_rotation_{label}",
        rifle_origin,
        helpers.RIFLE_AIM_FORWARD,
        helpers.RIFLE_AIM_UP,
        Vector((0.0, -0.50, 0.866)),
        Vector((0.0, -0.62, 0.785)),
    )
    target.animation_data.action = reference
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    rotation = helpers.rotation_only(
        target.matrix_world @ target.pose.bones["mixamorig:RightHand"].matrix
    ).normalized()
    target.animation_data.action = None
    bpy.data.actions.remove(reference)
    print(
        "QUATERNIUS_AIM_HAND_ROTATION "
        f"variant={slug} pose={label} "
        f"wxyz=({rotation.w:.9f},{rotation.x:.9f},{rotation.y:.9f},{rotation.z:.9f})"
    )
    return rotation


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
    profiles = VARIANTS[slug]["material_profiles"]
    materials = {
        slot.material
        for mesh in meshes
        for slot in mesh.material_slots
        if slot.material is not None
    }
    for material in materials:
        original_name = material.name
        material["steel_tide_source_material"] = original_name
        material["steel_tide_profile"] = profiles.get(original_name, "fabric")
        if original_name in overrides:
            material.diffuse_color = overrides[original_name]
            if material.use_nodes and material.node_tree is not None:
                principled = material.node_tree.nodes.get("Principled BSDF")
                if principled is not None:
                    principled.inputs["Base Color"].default_value = overrides[original_name]
        material.name = f"{slug.title()}_{original_name}"


def apply_mesh_material_profiles(meshes: list[bpy.types.Object], slug: str) -> None:
    """Split a shared source material only when two garment parts need different PBR."""

    overrides = VARIANTS[slug].get("mesh_material_profiles", {})
    for mesh in meshes:
        mesh_overrides = overrides.get(mesh.name, {})
        for slot in mesh.material_slots:
            material = slot.material
            if material is None:
                continue
            source_name = material.get("steel_tide_source_material", material.name)
            profile = mesh_overrides.get(source_name)
            if profile is None or profile == material.get("steel_tide_profile"):
                continue
            tailored = material.copy()
            tailored.name = f"{slug.title()}_{source_name}_{mesh.name.removeprefix('Operator')}"
            tailored["steel_tide_profile"] = profile
            slot.material = tailored
            print(
                "QUATERNIUS_OPERATOR_MATERIAL_SPLIT "
                f"variant={slug} mesh={mesh.name} source={source_name} profile={profile}"
            )


def author_eye_detail(meshes: list[bpy.types.Object], slug: str) -> None:
    """Refine the source eye blocks into rounded sclera and inset iris surfaces."""

    face = FACE_REFINEMENT[slug]
    head = next(mesh for mesh in meshes if mesh.name == "OperatorHead")
    eye_white_index = next(
        (
            index
            for index, material in enumerate(head.data.materials)
            if material is not None and material.get("steel_tide_profile") == "eye_white"
        ),
        None,
    )
    if eye_white_index is None:
        raise RuntimeError(f"{slug.title()} head is missing its authored eye material")
    eye_white = head.data.materials[eye_white_index]
    iris = eye_white.copy()
    iris.name = f"{slug.title()}_Iris"
    iris["steel_tide_source_material"] = "Iris"
    iris["steel_tide_profile"] = "iris"
    iris.diffuse_color = face["iris_color"]
    head.data.materials.append(iris)
    iris_index = len(head.data.materials) - 1

    eye_vertices = {
        vertex_index
        for polygon in head.data.polygons
        if polygon.material_index == eye_white_index
        for vertex_index in polygon.vertices
    }
    source_eye_centers = {}
    for side in (-1.0, 1.0):
        side_vertices = [
            head.data.vertices[index].co
            for index in eye_vertices
            if head.data.vertices[index].co.x * side > 0.0
        ]
        if not side_vertices:
            raise RuntimeError(f"{slug.title()} is missing one source eye block")
        source_eye_centers[side] = Vector(
            (
                sum(coordinate.x for coordinate in side_vertices) / len(side_vertices),
                sum(coordinate.y for coordinate in side_vertices) / len(side_vertices),
                sum(coordinate.z for coordinate in side_vertices) / len(side_vertices),
            )
        )
    # The source eyes are mirrored islands. Average the two source centroids
    # before moving them so tiny exporter/order differences cannot introduce
    # visible left/right drift in the authored sclera silhouette.
    source_center_x = sum(abs(center.x) for center in source_eye_centers.values()) * 0.5
    source_center_z = sum(center.z for center in source_eye_centers.values()) * 0.5
    for vertex_index in eye_vertices:
        coordinate = head.data.vertices[vertex_index].co
        side = 1.0 if coordinate.x >= 0.0 else -1.0
        eye_center_x = side * face["eye_center_x"]
        coordinate.x = eye_center_x + (
            coordinate.x - side * source_center_x
        ) * face["eye_width_scale"]
        coordinate.z = (
            face["eye_center_z"]
            + (coordinate.z - source_center_z) * face["eye_height_scale"]
        )
    head.data.update()
    sclera_bounds = {}
    for side in (-1.0, 1.0):
        side_coordinates = [
            head.data.vertices[index].co
            for index in eye_vertices
            if head.data.vertices[index].co.x * side > 0.0
        ]
        sclera_bounds[side] = (
            min(coordinate.x for coordinate in side_coordinates),
            max(coordinate.x for coordinate in side_coordinates),
            min(coordinate.z for coordinate in side_coordinates),
            max(coordinate.z for coordinate in side_coordinates),
        )

    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head
    bpy.context.tool_settings.mesh_select_mode = (False, False, True)
    iris_vertices = set()
    iris_face_count = 0
    for side in (-1.0, 1.0):
        source_fronts = [
            polygon
            for polygon in head.data.polygons
            if polygon.material_index == eye_white_index
            and polygon.normal.y < -0.70
            and polygon.center.x * side > 0.0
        ]
        if len(source_fronts) != 1:
            raise RuntimeError(
                f"Expected one source {slug.title()} eye front for side={side:+.0f}, "
                f"found {len(source_fronts)}"
            )
        source_face_index = source_fronts[0].index
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="DESELECT")
        bpy.ops.object.mode_set(mode="OBJECT")
        head.data.polygons[source_face_index].select = True
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.inset(
            thickness=0.0022,
            depth=-0.0012,
            use_even_offset=True,
            use_individual=False,
        )
        bpy.ops.object.mode_set(mode="OBJECT")
        head.data.update()
        # Blender leaves the newly inset center selected and the surrounding
        # ring unselected. Because each connected eye island is processed in
        # a separate operation, this gives an unambiguous center face without
        # a global nearest-polygon heuristic.
        new_inner_faces = [
            polygon
            for polygon in head.data.polygons
            if polygon.material_index == eye_white_index
            and polygon.normal.y < -0.70
            and polygon.center.x * side > 0.0
            and polygon.select
        ]
        if len(new_inner_faces) != 1:
            raise RuntimeError(
                f"Expected one inset {slug.title()} iris face for side={side:+.0f}, "
                f"found {len(new_inner_faces)}"
            )
        # Apply and size this island immediately. Holding a MeshPolygon RNA
        # reference across the topology edit for the other eye is unsafe.
        polygon = new_inner_faces[0]
        polygon.material_index = iris_index
        iris_face_count += 1
        polygon_vertices = set(polygon.vertices)
        iris_vertices.update(polygon_vertices)
        coordinates = [head.data.vertices[index].co for index in polygon_vertices]
        current_min_x = min(coordinate.x for coordinate in coordinates)
        current_max_x = max(coordinate.x for coordinate in coordinates)
        current_min_z = min(coordinate.z for coordinate in coordinates)
        current_max_z = max(coordinate.z for coordinate in coordinates)
        current_center_x = (current_min_x + current_max_x) * 0.5
        current_center_z = (current_min_z + current_max_z) * 0.5
        current_width = current_max_x - current_min_x
        current_height = current_max_z - current_min_z
        sclera_min_x, sclera_max_x, sclera_min_z, sclera_max_z = sclera_bounds[side]
        target_width = (sclera_max_x - sclera_min_x) * face["iris_width_ratio"]
        target_height = (sclera_max_z - sclera_min_z) * face["iris_height_ratio"]
        if current_width <= 1.0e-8 or current_height <= 1.0e-8:
            raise RuntimeError(f"{slug.title()} produced a degenerate inset iris")
        for vertex_index in polygon_vertices:
            coordinate = head.data.vertices[vertex_index].co
            coordinate.x = side * face["eye_center_x"] + (
                coordinate.x - current_center_x
            ) * target_width / current_width
            coordinate.z = face["eye_center_z"] + (
                coordinate.z - current_center_z
            ) * target_height / current_height
    if iris_face_count != 2:
        raise RuntimeError(f"Expected two inset {slug.title()} iris faces, found {iris_face_count}")
    head.data.update()
    print(
        "QUATERNIUS_OPERATOR_EYES "
        f"variant={slug} sclera_material={eye_white.name} iris_material={iris.name} "
        f"eye_vertices={len(eye_vertices)} iris_faces={iris_face_count} "
        f"iris_vertices={len(iris_vertices)} "
        f"iris_ratio={face['iris_width_ratio']:.2f}x{face['iris_height_ratio']:.2f}"
    )


def set_principled_input(
    principled: bpy.types.ShaderNodeBsdfPrincipled,
    name: str,
    value: float,
) -> None:
    socket = principled.inputs.get(name)
    if socket is not None:
        socket.default_value = value


def refine_role_materials(meshes: list[bpy.types.Object], slug: str) -> None:
    """Give each authored surface a restrained production PBR response.

    The source pack intentionally uses flat viewport materials. Runtime GLB
    assets need actual Principled values so skin, cloth, coated armor, metal,
    hair, and leather do not all react to light as the same plastic surface.
    """

    materials = {
        slot.material
        for mesh in meshes
        for slot in mesh.material_slots
        if slot.material is not None
    }
    for material in materials:
        profile_name = material.get("steel_tide_profile", "fabric")
        profile = MATERIAL_PROFILES[profile_name]
        material.use_nodes = True
        if material.node_tree is None:
            raise RuntimeError(f"Unable to create nodes for {material.name}")
        principled = next(
            (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
            None,
        )
        if principled is None:
            principled = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
            output = next(
                (node for node in material.node_tree.nodes if node.type == "OUTPUT_MATERIAL"),
                None,
            )
            if output is None:
                output = material.node_tree.nodes.new("ShaderNodeOutputMaterial")
            material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])

        if "base_color" in profile:
            material.diffuse_color = profile["base_color"]
        principled.inputs["Base Color"].default_value = material.diffuse_color
        set_principled_input(principled, "Metallic", profile["metallic"])
        set_principled_input(principled, "Roughness", profile["roughness"])
        set_principled_input(principled, "IOR", profile["ior"])
        set_principled_input(principled, "Specular IOR Level", profile["specular"])
        set_principled_input(principled, "Subsurface Weight", profile["subsurface"])
        set_principled_input(principled, "Coat Weight", profile["coat"])
        set_principled_input(principled, "Coat Roughness", profile["coat_roughness"])
        set_principled_input(principled, "Sheen Weight", profile["sheen"])
        material.metallic = profile["metallic"]
        material.roughness = profile["roughness"]
        print(
            "QUATERNIUS_OPERATOR_MATERIAL "
            f"variant={slug} material={material.name} profile={profile_name} "
            f"metallic={profile['metallic']:.2f} roughness={profile['roughness']:.2f}"
        )


def triangle_count(mesh: bpy.types.Object) -> int:
    mesh.data.calc_loop_triangles()
    return len(mesh.data.loop_triangles)


def report_geometry(meshes: list[bpy.types.Object], slug: str, stage: str) -> tuple[int, int]:
    vertices = sum(len(mesh.data.vertices) for mesh in meshes)
    triangles = sum(triangle_count(mesh) for mesh in meshes)
    print(
        "QUATERNIUS_OPERATOR_GEOMETRY "
        f"variant={slug} stage={stage} meshes={len(meshes)} "
        f"vertices={vertices} triangles={triangles}"
    )
    for mesh in meshes:
        print(
            "QUATERNIUS_OPERATOR_GEOMETRY_MESH "
            f"variant={slug} stage={stage} mesh={mesh.name} "
            f"vertices={len(mesh.data.vertices)} triangles={triangle_count(mesh)}"
        )
    return vertices, triangles


def material_profile(material: bpy.types.Material | None) -> str:
    if material is None:
        return "fabric"
    return material.get("steel_tide_profile", "fabric")


def author_shape_preserving_creases(mesh: bpy.types.Object) -> None:
    """Protect authored seams while allowing organic forms to become rounder."""

    data = mesh.data
    data.update()
    crease = data.attributes.get("crease_edge")
    if crease is None:
        crease = data.attributes.new("crease_edge", "FLOAT", "EDGE")
    edge_faces: list[list[bpy.types.MeshPolygon]] = [[] for _ in data.edges]
    for polygon in data.polygons:
        for loop_index in polygon.loop_indices:
            edge_faces[data.loops[loop_index].edge_index].append(polygon)

    hard_profiles = {"armor", "polymer", "metal", "leather"}
    for edge in data.edges:
        linked = edge_faces[edge.index]
        value = 0.0
        if len(linked) != 2:
            value = 1.0
        else:
            first, second = linked
            first_material = data.materials[first.material_index] if first.material_index < len(data.materials) else None
            second_material = data.materials[second.material_index] if second.material_index < len(data.materials) else None
            first_profile = material_profile(first_material)
            second_profile = material_profile(second_material)
            if first.material_index != second.material_index:
                eye_profiles = {"eye_white", "iris"}
                value = 0.34 if first_profile in eye_profiles and second_profile in eye_profiles else 1.0
            else:
                dot = max(-1.0, min(1.0, first.normal.dot(second.normal)))
                if first_profile in hard_profiles or second_profile in hard_profiles:
                    # Retain plates, belts, knee pads, and boot shafts instead
                    # of letting Catmull-Clark inflate them into soft balloons.
                    value = 0.98 if dot < 0.82 else 0.76
                elif first_profile == "fabric" or second_profile == "fabric":
                    # Cloth keeps authored seams/folds but its broad panels are
                    # allowed a small amount of silhouette softening.
                    value = 0.92 if dot < 0.76 else 0.46
                elif first_profile == "hair" or second_profile == "hair":
                    value = 0.70 if dot < 0.82 else 0.34
                elif first_profile == "skin" or second_profile == "skin":
                    # A gentle feature crease retains nose/lips/chin while the
                    # second head subdivision removes the faceted cheek planes.
                    value = 0.08 if mesh.name == "OperatorHead" and dot < 0.72 else (
                        0.28 if dot < 0.72 else 0.0
                    )
                if mesh.name == "OperatorBody":
                    edge_points = [data.vertices[index].co for index in edge.vertices]
                    if max(abs(point.x) for point in edge_points) > 0.66:
                        # The source has distinct authored finger wedges. Keep
                        # their webbing and fingertip silhouette through the
                        # body subdivision instead of melting them together.
                        value = max(value, 0.76 if dot < 0.86 else 0.30)
        crease.data[edge.index].value = value


def mark_material_boundaries_sharp(mesh: bpy.types.Object) -> None:
    data = mesh.data
    data.update()
    edge_faces: list[list[bpy.types.MeshPolygon]] = [[] for _ in data.edges]
    for polygon in data.polygons:
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            edge_faces[data.loops[loop_index].edge_index].append(polygon)
    for edge in data.edges:
        linked = edge_faces[edge.index]
        edge.use_edge_sharp = len(linked) != 2 or linked[0].material_index != linked[1].material_index


def apply_high_detail_modeling(meshes: list[bpy.types.Object], slug: str) -> None:
    """Apply real Catmull-Clark topology while preserving authored hard seams."""

    levels = VARIANTS[slug]["subdivision_levels"]
    for mesh in meshes:
        author_shape_preserving_creases(mesh)
        subdivision_level = levels.get(mesh.name, levels["default"])
        bpy.ops.object.select_all(action="DESELECT")
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        modifier = mesh.modifiers.new("SteelTide_HighDetail", "SUBSURF")
        modifier.subdivision_type = "CATMULL_CLARK"
        modifier.levels = subdivision_level
        modifier.render_levels = subdivision_level
        modifier.boundary_smooth = "PRESERVE_CORNERS"
        modifier.uv_smooth = "PRESERVE_BOUNDARIES"
        bpy.ops.object.modifier_move_to_index(modifier=modifier.name, index=0)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        mark_material_boundaries_sharp(mesh)
        if mesh.data.validate(verbose=True, clean_customdata=True):
            print(f"QUATERNIUS_OPERATOR_DETAIL_REPAIRED variant={slug} mesh={mesh.name}")
        mesh.data.update()
        print(
            "QUATERNIUS_OPERATOR_DETAIL "
            f"variant={slug} mesh={mesh.name} subdivision={subdivision_level} "
            f"vertices={len(mesh.data.vertices)} triangles={triangle_count(mesh)}"
        )


def gaussian(value: float, center: float, radius: float) -> float:
    return exp(-((value - center) / radius) ** 2)


def add_mouth_topology(meshes: list[bpy.types.Object], slug: str) -> None:
    """Add local topology to the existing skin before cutting the mouth sculpt."""

    face = FACE_REFINEMENT[slug]
    head = next(mesh for mesh in meshes if mesh.name == "OperatorHead")
    skin_indices = {
        index
        for index, material in enumerate(head.data.materials)
        if material is not None and material.get("steel_tide_source_material") == "Skin"
    }
    mouth_faces = [
        polygon
        for polygon in head.data.polygons
        if polygon.material_index in skin_indices
        and abs(polygon.center.x) <= max(0.050, face["lip_region_width"] + 0.016)
        and face["mouth_center_z"] - 0.026 <= polygon.center.z <= face["mouth_center_z"] + 0.022
        and polygon.center.y < -0.125
    ]
    if not mouth_faces:
        raise RuntimeError(f"{slug.title()} head has no skin faces available for mouth topology")
    before_vertices = len(head.data.vertices)
    before_triangles = triangle_count(head)
    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head
    for polygon in head.data.polygons:
        polygon.select = polygon in mouth_faces
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.subdivide(number_cuts=2, smoothness=0.0)
    bpy.ops.object.mode_set(mode="OBJECT")
    head.data.update()
    print(
        "QUATERNIUS_OPERATOR_MOUTH_TOPOLOGY "
        f"variant={slug} source_faces={len(mouth_faces)} "
        f"vertices={before_vertices}->{len(head.data.vertices)} "
        f"triangles={before_triangles}->{triangle_count(head)}"
    )


def sculpt_operator_face(meshes: list[bpy.types.Object], slug: str) -> None:
    """Sculpt restrained facial planes on the subdivided source identity."""

    face = FACE_REFINEMENT[slug]
    head = next(mesh for mesh in meshes if mesh.name == "OperatorHead")
    skin_indices = {
        index
        for index, material in enumerate(head.data.materials)
        if material is not None and material.get("steel_tide_source_material") == "Skin"
    }
    skin_vertices = {
        vertex_index
        for polygon in head.data.polygons
        if polygon.material_index in skin_indices
        for vertex_index in polygon.vertices
    }
    sculpted = 0
    maximum_displacement = 0.0
    for vertex_index in skin_vertices:
        vertex = head.data.vertices[vertex_index]
        original = vertex.co.copy()
        x = original.x
        y = original.y
        z = original.z
        front = max(0.0, min(1.0, (-y - 0.095) / 0.065))
        if front <= 0.0 or z < 1.555 or z > 1.770:
            continue
        absolute_x = abs(x)
        side = 1.0 if x >= 0.0 else -1.0

        # Eye socket depth plus separate upper/lower lid ridges.
        eye_center_x = face["eye_center_x"]
        eye_center_z = face["eye_center_z"]
        eye_field = gaussian(absolute_x, eye_center_x, 0.031) * gaussian(z, eye_center_z, 0.024) * front
        upper_lid = gaussian(absolute_x, eye_center_x, 0.029) * gaussian(z, eye_center_z + 0.013, 0.008) * front
        lower_lid = gaussian(absolute_x, eye_center_x, 0.028) * gaussian(z, eye_center_z - 0.014, 0.008) * front
        vertex.co.y += face["eye_socket_depth"] * eye_field
        vertex.co.y -= face["upper_lid_depth"] * upper_lid + face["lower_lid_depth"] * lower_lid

        # Bridge, tip, and alar wings retain a readable nose silhouette in 3/4.
        nose_bridge = gaussian(x, 0.0, 0.016) * gaussian(z, 1.676, 0.047) * front
        nose_tip = gaussian(x, 0.0, 0.022) * gaussian(z, 1.640, 0.020) * front
        nose_wing = gaussian(absolute_x, 0.018, 0.010) * gaussian(z, 1.638, 0.014) * front
        vertex.co.y += face["nose_bridge_depth"] * nose_bridge
        vertex.co.y -= face["nose_tip_depth"] * nose_tip + 0.0005 * nose_wing
        vertex.co.x += side * 0.0008 * nose_wing

        # Add soft cheek volume while tapering the lower jaw and defining chin.
        cheek = gaussian(absolute_x, face["cheek_center_x"], 0.029) * gaussian(z, 1.647, 0.033) * front
        jaw = gaussian(z, 1.596, 0.038) * max(0.0, min(1.0, (absolute_x - 0.042) / 0.050)) * front
        chin = gaussian(x, 0.0, 0.032) * gaussian(z, 1.574, 0.022) * front
        vertex.co.y -= face["cheek_depth"] * cheek + face["chin_depth"] * chin
        vertex.co.x -= side * face["jaw_taper"] * jaw

        # Two lip rolls separated by a shallow mouth crease.
        upper_lip = gaussian(x, 0.0, face["upper_lip_width"]) * gaussian(z, face["upper_lip_z"], 0.007) * front
        lower_lip = gaussian(x, 0.0, face["lower_lip_width"]) * gaussian(z, face["lower_lip_z"], 0.008) * front
        mouth_crease = gaussian(x, 0.0, face["lip_region_width"] + 0.005) * gaussian(z, face["mouth_center_z"], 0.004) * front
        vertex.co.y -= face["upper_lip_depth"] * upper_lip + face["lower_lip_depth"] * lower_lip
        vertex.co.y += face["mouth_crease_depth"] * mouth_crease
        mouth_corner = gaussian(absolute_x, face["lip_region_width"] + 0.002, 0.008) * gaussian(z, face["mouth_center_z"], 0.008) * front
        vertex.co.y += face["mouth_corner_depth"] * mouth_corner

        displacement = (vertex.co - original).length
        if displacement > 1.0e-6:
            sculpted += 1
            maximum_displacement = max(maximum_displacement, displacement)
    head.data.update()
    print(
        "QUATERNIUS_OPERATOR_FACE_SCULPT "
        f"variant={slug} vertices={sculpted} max_displacement={maximum_displacement:.6f}"
    )


def author_operator_lips(meshes: list[bpy.types.Object], slug: str) -> None:
    """Cut a restrained mouth region from the existing subdivided skin surface."""

    face = FACE_REFINEMENT[slug]
    head = next(mesh for mesh in meshes if mesh.name == "OperatorHead")
    skin_indices = {
        index
        for index, material in enumerate(head.data.materials)
        if material is not None and material.get("steel_tide_source_material") == "Skin"
    }
    skin_material = head.data.materials[next(iter(skin_indices))]
    lip_material = skin_material.copy()
    lip_material.name = f"{slug.title()}_Lips"
    lip_material["steel_tide_source_material"] = "Lips"
    lip_material["steel_tide_profile"] = "lips"
    lip_material.diffuse_color = face["lip_color"]
    head.data.materials.append(lip_material)
    lip_index = len(head.data.materials) - 1
    mouth_material = skin_material.copy()
    mouth_material.name = f"{slug.title()}_MouthCrease"
    mouth_material["steel_tide_source_material"] = "MouthCrease"
    mouth_material["steel_tide_profile"] = "mouth_crease"
    mouth_material.diffuse_color = face["crease_color"]
    head.data.materials.append(mouth_material)
    mouth_index = len(head.data.materials) - 1
    lip_faces = [
        polygon
        for polygon in head.data.polygons
        if polygon.material_index in skin_indices
        and (
            (polygon.center.x / face["lip_region_width"]) ** 2
            + ((polygon.center.z - face["mouth_center_z"]) / face["lip_region_height"]) ** 2
        ) <= 1.0
        and polygon.center.y < -0.145
    ]
    if not lip_faces:
        raise RuntimeError(f"{slug.title()} face sculpt produced no lip surface")
    for polygon in lip_faces:
        polygon.material_index = lip_index
    mouth_faces = [
        polygon
        for polygon in lip_faces
        if abs(polygon.center.x) <= face["crease_half_width"]
        and abs(polygon.center.z - face["mouth_center_z"]) <= 0.0014
    ]
    if not mouth_faces:
        raise RuntimeError(f"{slug.title()} face sculpt produced no mouth crease surface")
    for polygon in mouth_faces:
        polygon.material_index = mouth_index
    head.data.update()
    print(
        "QUATERNIUS_OPERATOR_LIPS "
        f"variant={slug} material={lip_material.name} faces={len(lip_faces)} "
        f"crease_material={mouth_material.name} crease_faces={len(mouth_faces)}"
    )


def deform_group_indices(mesh: bpy.types.Object) -> set[int]:
    armature_modifier = next(
        (modifier for modifier in mesh.modifiers if modifier.type == "ARMATURE" and modifier.object is not None),
        None,
    )
    if armature_modifier is None:
        raise RuntimeError(f"{mesh.name} has no armature modifier")
    deform_bones = {bone.name for bone in armature_modifier.object.data.bones if bone.use_deform}
    return {group.index for group in mesh.vertex_groups if group.name in deform_bones}


def skinning_weight_stats(
    mesh: bpy.types.Object,
    deform_groups: set[int],
) -> tuple[int, int, float, float]:
    max_influences = 0
    over_limit = 0
    minimum_sum = float("inf")
    maximum_sum = 0.0
    for vertex in mesh.data.vertices:
        weights = [
            group.weight
            for group in vertex.groups
            if group.group in deform_groups and group.weight > 1.0e-8
        ]
        max_influences = max(max_influences, len(weights))
        if len(weights) > 4:
            over_limit += 1
        total = sum(weights)
        minimum_sum = min(minimum_sum, total)
        maximum_sum = max(maximum_sum, total)
    if minimum_sum == float("inf"):
        minimum_sum = 0.0
    return max_influences, over_limit, minimum_sum, maximum_sum


def normalize_skinning_weights(meshes: list[bpy.types.Object], slug: str) -> None:
    """Keep runtime skinning deterministic and within glTF's four-joint limit."""

    for mesh in meshes:
        deform_groups = deform_group_indices(mesh)
        before = skinning_weight_stats(mesh, deform_groups)
        # Work directly on deform-bone groups.  The Quaternius sources also
        # contain selection/control groups such as Body and Wrist; including
        # those in Blender's Limit Total operator can evict a real joint even
        # though the final numerical weight check still appears valid.
        removals: dict[int, list[int]] = {}
        replacements: list[tuple[int, int, float]] = []
        for vertex in mesh.data.vertices:
            weights_by_group: dict[int, float] = {}
            for membership in vertex.groups:
                if membership.group not in deform_groups or membership.weight <= 1.0e-8:
                    continue
                weights_by_group[membership.group] = (
                    weights_by_group.get(membership.group, 0.0) + membership.weight
                )
                removals.setdefault(membership.group, []).append(vertex.index)
            strongest = sorted(
                weights_by_group.items(),
                key=lambda item: item[1],
                reverse=True,
            )[:4]
            total = sum(weight for _, weight in strongest)
            if total > 1.0e-8:
                for group_index, weight in strongest:
                    replacements.append((group_index, vertex.index, weight / total))
        for group_index, vertex_indices in removals.items():
            mesh.vertex_groups[group_index].remove(sorted(set(vertex_indices)))
        for group_index, vertex_index, weight in replacements:
            mesh.vertex_groups[group_index].add([vertex_index], weight, "REPLACE")
        mesh.data.update()
        after = skinning_weight_stats(mesh, deform_groups)
        if after[0] > 4 or after[2] < 0.999 or after[3] > 1.001:
            for vertex in mesh.data.vertices:
                weights = [
                    (group.group, mesh.vertex_groups[group.group].name, group.weight)
                    for group in vertex.groups
                    if group.group in deform_groups and group.weight > 1.0e-8
                ]
                if weights and abs(sum(weight for _, _, weight in weights) - 1.0) > 0.001:
                    print(
                        "QUATERNIUS_OPERATOR_WEIGHT_INVALID "
                        f"variant={slug} mesh={mesh.name} vertex={vertex.index} weights={weights}"
                    )
                    break
            raise RuntimeError(
                f"Invalid normalized weights for {mesh.name}: "
                f"max_influences={after[0]} sum={after[2]:.6f}..{after[3]:.6f}"
            )
        print(
            "QUATERNIUS_OPERATOR_WEIGHTS "
            f"variant={slug} mesh={mesh.name} "
            f"before_max_influences={before[0]} before_over_limit={before[1]} "
            f"before_sum={before[2]:.6f}..{before[3]:.6f} "
            f"after_max_influences={after[0]} after_over_limit={after[1]} "
            f"after_sum={after[2]:.6f}..{after[3]:.6f}"
        )


def operator_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [mesh.matrix_world @ Vector(corner) for mesh in meshes for corner in mesh.bound_box]
    minimum = Vector(tuple(min(point[index] for point in points) for index in range(3)))
    maximum = Vector(tuple(max(point[index] for point in points) for index in range(3)))
    return minimum, maximum


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area_light(name: str, location: tuple[float, float, float], energy: float, size: float) -> bpy.types.Object:
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    bpy.context.collection.objects.link(light)
    light.location = location
    return light


def render_operator_qa(
    meshes: list[bpy.types.Object],
    slug: str,
    stage: str,
    output_dir: Path,
    view: str = "front",
) -> Path:
    """Render the same authored model before/after without affecting export."""

    scene = bpy.context.scene
    model_minimum, model_maximum = operator_bounds(meshes)
    focus_meshes = (
        [next(mesh for mesh in meshes if mesh.name == "OperatorHead")]
        if view == "face_34"
        else meshes
    )
    minimum, maximum = operator_bounds(focus_meshes)
    center = (minimum + maximum) * 0.5
    width = maximum.x - minimum.x
    depth = maximum.y - minimum.y
    height = maximum.z - minimum.z
    if view == "face_34":
        distance = max(0.52, height * 2.55, width * 3.15, depth * 2.8)
        camera_offset = Vector((width * 1.55, -distance, height * 0.03))
        camera_lens = 72.0
    else:
        distance = max(height * 1.85, width * 2.4, depth * 2.4)
        camera_x = width * 0.62 if view == "aim" else 0.0
        camera_offset = Vector((camera_x, -distance, height * 0.06))
        camera_lens = 62.0
    created: list[bpy.types.Object] = []

    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.lens = camera_lens
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + camera_offset
    point_at(camera, center + Vector((0.0, 0.0, height * 0.03)))
    scene.camera = camera
    created.append(camera)

    light_specs = (
        ("QA_Key", center + Vector((2.4, -3.2, 3.4)), 680.0, 4.0),
        ("QA_Fill", center + Vector((-3.0, -1.8, 2.0)), 340.0, 4.5),
        ("QA_Rim", center + Vector((0.8, 2.8, 3.8)), 860.0, 3.0),
    )
    for name, location, energy, size in light_specs:
        light = add_area_light(name, tuple(location), energy, size)
        point_at(light, center)
        created.append(light)

    bpy.ops.mesh.primitive_plane_add(
        size=max(8.0, distance * 3.0),
        location=(center.x, center.y, model_minimum.z - 0.012),
    )
    ground = bpy.context.object
    ground.name = "QA_Ground"
    ground_material = bpy.data.materials.new("QA_GroundMaterial")
    ground_material.diffuse_color = (0.012, 0.016, 0.020, 1.0)
    ground_material.use_nodes = True
    ground_principled = next(node for node in ground_material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    ground_principled.inputs["Base Color"].default_value = ground_material.diffuse_color
    ground_principled.inputs["Roughness"].default_value = 0.56
    ground.data.materials.append(ground_material)
    created.append(ground)

    if scene.world is None:
        scene.world = bpy.data.worlds.new("QA_World")
    scene.world.use_nodes = True
    background = next(node for node in scene.world.node_tree.nodes if node.type == "BACKGROUND")
    background.inputs["Color"].default_value = (0.006, 0.010, 0.014, 1.0)
    background.inputs["Strength"].default_value = 0.16
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 900 if view == "face_34" else 720
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.render.filepath = str((output_dir / f"{slug}_{stage}.png").resolve())
    scene.render.use_file_extension = True
    scene.view_settings.look = "Medium High Contrast"
    output_dir.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    rendered = Path(scene.render.filepath)
    print(f"QUATERNIUS_OPERATOR_QA_RENDER variant={slug} stage={stage} path={rendered}")

    for obj in created:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    if ground_material.name in bpy.data.materials:
        bpy.data.materials.remove(ground_material)
    return rendered


def render_weapon_pose_qa(
    target: bpy.types.Object,
    meshes: list[bpy.types.Object],
    helpers,
    action: bpy.types.Action,
    slug: str,
    output_dir: Path,
) -> Path:
    """Render the actual retargeted ready pose with its authored M4 transform."""

    target.animation_data.action = action
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    before_objects = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(helpers.M4_GLB))
    imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    rifle = next(obj for obj in imported_objects if obj.name == "SteelTideM4A1")
    rifle_matrix = helpers.rifle_world_rotation(
        helpers.RIFLE_READY_FORWARD,
        helpers.RIFLE_READY_UP,
    ).to_matrix().to_4x4()
    rifle_matrix.translation = helpers.RIFLE_READY_ORIGIN
    rifle.matrix_world = rifle_matrix @ Matrix.Diagonal(
        (helpers.RIFLE_SCALE, helpers.RIFLE_SCALE, helpers.RIFLE_SCALE, 1.0)
    )
    bpy.context.view_layer.update()
    rendered = render_operator_qa(
        meshes,
        slug,
        "after_ready_weapon",
        output_dir,
        view="aim",
    )
    for obj in reversed(imported_objects):
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    target.animation_data.action = None
    helpers.reset_pose(target)
    return rendered


def prepare_scene(
    prefix: str,
    slug: str,
) -> tuple[bpy.types.Object, bpy.types.Object, list[bpy.types.Object]]:
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
    apply_mesh_material_profiles(meshes, slug)
    rename_target_rig(target)
    return target, feet, meshes


def build_variant(
    slug: str,
    save_working_blend: bool,
    qa_render: bool,
    qa_output_dir: Path,
) -> None:
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
    target, target_feet, meshes = prepare_scene(variant["prefix"], slug)
    report_geometry(meshes, slug, "before")
    if qa_render:
        render_operator_qa(meshes, slug, "before", qa_output_dir)
    author_eye_detail(meshes, slug)
    apply_high_detail_modeling(meshes, slug)
    add_mouth_topology(meshes, slug)
    sculpt_operator_face(meshes, slug)
    author_operator_lips(meshes, slug)
    normalize_skinning_weights(meshes, slug)
    refine_role_materials(meshes, slug)
    report_geometry(meshes, slug, "after")
    if qa_render:
        render_operator_qa(meshes, slug, "after_front", qa_output_dir, view="front")
        render_operator_qa(meshes, slug, "after_face_34", qa_output_dir, view="face_34")
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

    reference_aim_hand_rotation = capture_aim_hand_rotation(
        target,
        generated["aim_idle"],
        helpers,
        slug,
        helpers.RIFLE_AIM_ORIGIN,
        "reference",
    )
    configure_quaternius_rifle_origins(target, helpers)
    lowered_aim_hand_rotation = capture_aim_hand_rotation(
        target,
        generated["aim_idle"],
        helpers,
        slug,
        helpers.RIFLE_AIM_ORIGIN,
        "lowered",
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
        support_hand_offset=(
            QUATERNIUS_VIPER_AIM_SUPPORT_HAND_OFFSET
            if slug == "viper"
            else QUATERNIUS_AIM_SUPPORT_HAND_OFFSET
        ),
        right_hand_world_rotation=reference_aim_hand_rotation,
        remove_source=True,
    )
    ready_hand_rotation = (
        helpers.rifle_world_rotation(helpers.RIFLE_READY_FORWARD, helpers.RIFLE_READY_UP)
        @ helpers.rifle_world_rotation(helpers.RIFLE_AIM_FORWARD, helpers.RIFLE_AIM_UP).inverted()
        @ lowered_aim_hand_rotation
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
        support_hand_offset=(
            QUATERNIUS_VIPER_READY_SUPPORT_HAND_OFFSET
            if slug == "viper"
            else QUATERNIUS_READY_SUPPORT_HAND_OFFSET
        ),
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
    if qa_render:
        render_weapon_pose_qa(
            target,
            meshes,
            helpers,
            generated["ready_idle"],
            slug,
            qa_output_dir,
        )

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

    # Animation libraries can contain detached staging meshes that are not
    # descendants of their armature (the UAL files currently carry an
    # Icosphere helper).  Keep the final GLB strictly limited to the authored
    # operator hierarchy and its gameplay sockets.
    export_objects = {target.parent, target, *target.children_recursive}
    for obj in list(bpy.context.scene.objects):
        if obj not in export_objects:
            bpy.data.objects.remove(obj, do_unlink=True)
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
    parser.add_argument(
        "--qa-render",
        action="store_true",
        help="Render matching before/after DCC review frames for the selected variant.",
    )
    parser.add_argument(
        "--qa-output-dir",
        type=Path,
        default=REPO_ROOT / "artifacts" / "operator_refinement",
        help="Directory for optional before/after PNG review frames.",
    )
    return parser.parse_args(script_args)


def main() -> None:
    args = parse_args()
    variants = VARIANTS if args.variant == "all" else (args.variant,)
    for slug in variants:
        build_variant(slug, args.save_working_blends, args.qa_render, args.qa_output_dir)


if __name__ == "__main__":
    main()
