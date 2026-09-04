"""Convert a Tencent HY-3D rigged character into this project's GLB contract.

The mesh and skin weights come from the Tencent HY-3D-Rigging FBX.  The 25
locomotion/combat actions come from the existing Quaternius Universal
Animation Library export and are baked on the Tencent skeleton in local rest
frames.  This is a Blender/DCC conversion step, not runtime primitive art.
"""
from __future__ import annotations

import argparse
import os
import sys
from typing import Iterable

import bpy
from mathutils import Matrix, Vector

MAP = {
    "Root": "root", "Hips": "Hips", "Spine": "Spine", "Spine1": "Spine1", "Spine2": "Spine2",
    "Neck": "Neck", "Head": "Head", "LeftShoulder": "LeftShoulder", "LeftArm": "LeftArm",
    "LeftForeArm": "LeftForeArm", "LeftHand": "LeftHand", "RightShoulder": "RightShoulder",
    "RightArm": "RightArm", "RightForeArm": "RightForeArm", "RightHand": "RightHand",
    "LeftUpLeg": "LeftUpLeg", "LeftLeg": "LeftLeg", "LeftFoot": "LeftFoot",
    "RightUpLeg": "RightUpLeg", "RightLeg": "RightLeg", "RightFoot": "RightFoot",
}
EXPECTED = {
    "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk", "ready_idle", "ready_walk",
    "ready_run", "ready_sprint", "ready_crouch_idle", "ready_crouch_walk", "aim_walk", "aim_run",
    "aim_sprint", "aim_crouch_idle", "aim_crouch_walk", "prone_idle", "prone_crawl", "aim_idle",
    "hit", "death", "downed", "revive_kneel", "revived",
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True)
    parser.add_argument("--rigged", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--triangles", type=int, default=60000)
    return parser.parse_args(argv)


def import_asset(path: str) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_custom_normals=True, ignore_leaf_bones=False)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    return [obj for obj in bpy.context.scene.objects if obj not in before]


def armature(objects: Iterable[bpy.types.Object]) -> bpy.types.Object:
    value = next((obj for obj in objects if obj.type == "ARMATURE"), None)
    if value is None:
        raise RuntimeError("missing armature")
    return value


def visual_mesh(objects: Iterable[bpy.types.Object]) -> bpy.types.Object:
    meshes = [obj for obj in objects if obj.type == "MESH" and len(obj.data.polygons) > 100]
    if not meshes:
        raise RuntimeError("missing visual mesh")
    return max(meshes, key=lambda obj: len(obj.data.polygons))


def source_bone(source: bpy.types.Object, canonical: str) -> bpy.types.PoseBone | None:
    return source.pose.bones.get(canonical) or source.pose.bones.get("mixamorig:" + canonical)


def source_actions(source: bpy.types.Object) -> list[bpy.types.Action]:
    names = {bone.name for bone in source.data.bones}
    candidates = [action for action in bpy.data.actions if any(any(f'pose.bones["{name}"]' in curve.data_path for name in names) for curve in action.fcurves)]
    unique: dict[str, bpy.types.Action] = {}
    for action in sorted(candidates, key=lambda item: item.name):
        unique.setdefault(action.name.split(".")[0], action)
    return list(unique.values())


def rest_corrections(source: bpy.types.Object, target: bpy.types.Object) -> dict[str, Matrix]:
    result: dict[str, Matrix] = {}
    for canonical, target_name in MAP.items():
        source_pose = source_bone(source, canonical)
        target_data = target.data.bones.get(target_name)
        if source_pose is None or target_data is None:
            continue
        source_rest = source_pose.bone.matrix_local.to_quaternion().to_matrix()
        target_rest = target_data.matrix_local.to_quaternion().to_matrix()
        result[canonical] = target_rest.inverted() @ source_rest
    return result


def bake_action(source: bpy.types.Object, target: bpy.types.Object, action: bpy.types.Action, corrections: dict[str, Matrix]) -> bpy.types.Action:
    source.animation_data_create(); target.animation_data_create()
    source.animation_data.action = action
    output = bpy.data.actions.new("HY3D_" + action.name.split(".")[0])
    target.animation_data.action = output
    start, end = [int(round(value)) for value in action.frame_range]
    for frame in range(start, end + 1):
        bpy.context.scene.frame_set(frame)
        for canonical, target_name in MAP.items():
            src = source_bone(source, canonical)
            dst = target.pose.bones.get(target_name)
            correction = corrections.get(canonical)
            if src is None or dst is None or correction is None:
                continue
            basis = src.matrix_basis
            dst.rotation_mode = "QUATERNION"
            dst.rotation_quaternion = (correction @ basis.to_3x3() @ correction.inverted()).to_quaternion()
            dst.location = correction @ basis.to_translation() if canonical in {"Root", "Hips"} else Vector((0.0, 0.0, 0.0))
            dst.scale = Vector((1.0, 1.0, 1.0))
            dst.keyframe_insert("rotation_quaternion", frame=frame, group=target_name)
            dst.keyframe_insert("location", frame=frame, group=target_name)
    source.animation_data.action = None; target.animation_data.action = None
    return output


def triangle_count(mesh: bpy.types.Object) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in mesh.data.polygons)


def reduce_mesh(mesh: bpy.types.Object, budget: int) -> int:
    count = triangle_count(mesh)
    if count > budget:
        modifier = mesh.modifiers.new("SteelTideGameplayDecimate", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.001, min(1.0, budget / float(count)))
        bpy.ops.object.select_all(action="DESELECT"); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return triangle_count(mesh)


def add_contract_nodes(target: bpy.types.Object, mesh: bpy.types.Object) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    for obj in list(bpy.context.scene.objects):
        if obj.type == "MESH" and obj != mesh:
            bpy.data.objects.remove(obj, do_unlink=True)
    target.name = "QuaterniusOperatorRig"; target.data.name = "QuaterniusOperatorRig"; mesh.name = "OperatorBody"
    if mesh.parent != target:
        mesh.parent = target; mesh.parent_type = "OBJECT"
    root = bpy.data.objects.new("QuaterniusOperator", None); bpy.context.collection.objects.link(root); target.parent = root
    specs = [("WeaponSocket", "RightHand", (0.0, 0.0, 0.0)), ("BackWeaponSocket", "Spine2", (0.0, -0.18, 0.05)), ("HeadSocket", "Head", (0.0, 0.0, 0.05)), ("VestSocket", "Spine2", (0.0, 0.0, 0.0)), ("BackpackSocket", "Spine", (0.0, -0.16, 0.0)), ("TeamPatchSocket", "Spine2", (0.0, 0.16, 0.0))]
    sockets=[]
    for name, bone, offset in specs:
        marker=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(marker); marker.parent=target; marker.parent_type="BONE"; marker.parent_bone=bone; marker.location=offset; sockets.append(marker)
    return root, sockets


def main() -> None:
    cfg = parse_args(); source_path=os.path.abspath(cfg.source); rigged_path=os.path.abspath(cfg.rigged); output_path=os.path.abspath(cfg.output)
    if output_path in {source_path, rigged_path}: raise SystemExit("output must be separate from inputs")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source_objects=import_asset(source_path); source_names=[obj.name for obj in source_objects]; source=armature(source_objects); actions=source_actions(source)
    target_objects=import_asset(rigged_path); target=armature(target_objects); mesh=visual_mesh(target_objects)
    bpy.ops.object.select_all(action="DESELECT"); target.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active=target; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    corrections=rest_corrections(source,target); generated=[bake_action(source,target,action,corrections) for action in actions]
    source_ids={id(action) for action in actions}
    for action in list(bpy.data.actions):
        if id(action) in source_ids: bpy.data.actions.remove(action)
    for action in generated: action.name=action.name.removeprefix("HY3D_")
    missing=EXPECTED-{action.name for action in generated}
    if missing: raise RuntimeError("missing canonical actions: "+",".join(sorted(missing)))
    # Remove source objects before renaming the target to the same contract
    # names (Quaternius and Tencent both commonly use OperatorBody/Rig names).
    for source_name in source_names:
        live = bpy.data.objects.get(source_name)
        if live is not None:
            bpy.data.objects.remove(live, do_unlink=True)
    triangles=reduce_mesh(mesh,cfg.triangles); root,sockets=add_contract_nodes(target,mesh)
    root["steel_tide_asset_role"]="realistic_hy3d_operator"; root["mesh_source"]="Tencent HY-3D-3.1 + HY-3D-Rigging"; root["animation_source"]="Quaternius Universal Animation Library (CC0), rest-frame retarget"; root["triangle_count"]=triangles; root["animation_count"]=len(generated)
    bpy.ops.object.select_all(action="DESELECT"); root.select_set(True); target.select_set(True); mesh.select_set(True); [obj.select_set(True) for obj in sockets]; bpy.context.view_layer.objects.active=root
    os.makedirs(os.path.dirname(output_path),exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=output_path,export_format="GLB",use_selection=True,export_yup=True,export_apply=False,export_skins=True,export_animations=True,export_animation_mode="ACTIONS",export_nla_strips=False,export_def_bones=True,export_leaf_bone=False,export_materials="EXPORT",export_image_format="AUTO",export_texcoords=True,export_normals=True,export_tangents=False,export_all_influences=False)
    print("HY3D_OPERATOR_CHECK",f"actions={len(generated)}",f"bones={len(target.data.bones)}",f"triangles={triangles}",f"sockets={len(sockets)}",f"output={output_path}"); print("HY3D_OPERATOR_PASS valid=true")


if __name__ == "__main__": main()
