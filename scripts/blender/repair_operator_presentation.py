"""Apply the operator DCC corrections and deliver their Blender/GLB sources.

Run from the repository root with Blender 4.5:
  blender -b --python scripts/blender/repair_operator_presentation.py -- --roles viper lynx
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import math
import statistics
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

REPO = Path(__file__).resolve().parents[2]
PRESENTATION_ROOT = 'AuthoredOperatorPresentation'


def hierarchy_root(rig):
    root = rig
    while root.parent is not None:
        root = root.parent
    return root


def restore_authoring_space(rig):
    """Remove only this pipeline's explicit outer presentation transform."""
    root = hierarchy_root(rig)
    if root.name != PRESENTATION_ROOT:
        if bpy.data.objects.get(PRESENTATION_ROOT) is not None:
            raise RuntimeError('Authored presentation root exists outside the operator hierarchy')
        return
    scale = root['steel_tide_presentation_scale']
    pivot = Vector(root['steel_tide_presentation_pivot'])
    if not math.isfinite(scale) or scale <= 0:
        raise RuntimeError('Invalid authored presentation root contract')
    expected = Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(scale, 4) @ Matrix.Translation(-pivot)
    if max(abs(root.matrix_basis[row][column] - expected[row][column])
           for row in range(4) for column in range(4)) > 1.0e-5:
        raise RuntimeError('Authored presentation root differs from its recorded source transform')
    children = [child for child in root.children if child.name != PRESENTATION_ROOT + 'Marker']
    if len(children) != 1:
        raise RuntimeError('Authored presentation root has unexpected children')
    child = children[0]
    local = child.matrix_basis.copy()
    child.parent = None
    child.matrix_parent_inverse = Matrix.Identity(4)
    child.matrix_basis = local
    marker = bpy.data.objects.get(PRESENTATION_ROOT + 'Marker')
    if marker is not None:
        bpy.data.objects.remove(marker, do_unlink=True)
    bpy.data.objects.remove(root, do_unlink=True)
    bpy.context.view_layer.update()


def author_presentation_space(rig, geometry):
    """Bake gameplay height and forward axis into the complete source hierarchy."""
    root = hierarchy_root(rig)
    if bpy.data.objects.get(PRESENTATION_ROOT) is not None:
        raise RuntimeError('Presentation transform must be unwrapped before final authoring')
    points = [geometry.world_vertices(mesh) for mesh in root.children_recursive if mesh.type == 'MESH']
    if not points:
        raise RuntimeError('Operator has no authored rest geometry bounds')
    height = max(float(value[:, 2].max()) for value in points) - min(float(value[:, 2].min()) for value in points)
    if not math.isfinite(height) or height <= 0.01:
        raise RuntimeError(f'Invalid authored operator rest height {height}')
    scale = 1.86 / height
    reference = rig.matrix_world @ rig.data.bones['Hips'].head_local
    # Ground baking leaves 8 mm clearance in authoring units. The final
    # parent places that floor and the canonical pelvis axis on the actor pivot.
    pivot = Vector((reference.x, reference.y, 0.008))
    marker_mesh = bpy.data.meshes.new(PRESENTATION_ROOT + 'MarkerMesh')
    marker_mesh.from_pydata([(0.0, 0.0, 0.0)] * 3, [], [(0, 1, 2)])
    marker_mesh.update()
    presentation = bpy.data.objects.new(PRESENTATION_ROOT, marker_mesh)
    bpy.context.scene.collection.objects.link(presentation)
    local = root.matrix_basis.copy()
    root.parent = presentation
    root.matrix_parent_inverse = Matrix.Identity(4)
    root.matrix_basis = local
    presentation.matrix_basis = Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(scale, 4) @ Matrix.Translation(-pivot)
    presentation['steel_tide_presentation_scale'] = scale
    presentation['steel_tide_presentation_pivot'] = list(pivot)
    presentation['steel_tide_presentation_height'] = 1.86
    bpy.context.view_layer.update()
    print(f'OPERATOR_PRESENTATION_CHECK source_height={height:.8f} scale={scale:.8f} '
          f'yaw_degrees=180 authored_height=1.86 pivot={list(pivot)} valid=true', flush=True)
    return scale


def module(name):
    path = Path(__file__).with_name(name + '.py')
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


def ground_actions(rig, meshes, gait):
    """Bake floor contact after the final skin, weapon and secondary-hair poses.

    Airborne jump actions intentionally keep their authored flight phase.
    The clip is sampled fully before writing, so no sample feeds back into
    the next through a curve that was already changed.
    """
    for action in list(bpy.data.actions):
        if action.name.startswith('jump_'):
            continue
        start, end = action.frame_range
        intervals = max(1, math.ceil(end - start))
        frames = [start + (end - start) * i / intervals for i in range(intervals + 1)]
        locations = []
        for frame in frames:
            gait.sample_action(rig, action, frame)
            mesh = min(meshes, key=gait.mesh_floor)
            gait.floor_pose(rig, mesh)
            locations.append(rig.pose.bones['Hips'].location.copy())
        path = 'pose.bones["Hips"].location'
        for curve in list(action.fcurves):
            if curve.data_path == path:
                action.fcurves.remove(curve)
        for index in range(3):
            curve = action.fcurves.new(path, index=index, action_group='Hips')
            curve.keyframe_points.add(len(frames))
            for key, frame, location in zip(curve.keyframe_points, frames, locations):
                key.co = (frame, location[index])
                key.interpolation = 'LINEAR'
            curve.update()
        print(f'OPERATOR_FLOOR_CHECK action={action.name} frames={len(frames)}', flush=True)


def measure_locomotion(rig, gait):
    result = {}
    for name in ('walk', 'run', 'sprint', 'crouch_walk'):
        action = bpy.data.actions[name]
        start, end = action.frame_range
        points = []
        for index in range(65):
            gait.sample_action(rig, action, start + (end - start) * index / 64)
            points.append([rig.matrix_world @ rig.pose.bones[side + 'Foot'].head
                           for side in ('Left', 'Right')])
        floor = [min(p[side].z for p in points) for side in range(2)]
        dt = (end - start) / 64 / bpy.context.scene.render.fps
        speeds = []
        for before, after in zip(points, points[1:]):
            for side in range(2):
                if max(before[side].z, after[side].z) < floor[side] + .05 and after[side].y > before[side].y:
                    speeds.append((after[side].y - before[side].y) / dt)
        if not speeds:
            raise RuntimeError(f'{rig.name}: {name} has no measurable planted step')
        result[name] = round(statistics.median(speeds), 6)
        print(f'OPERATOR_STRIDE_CHECK action={name} speed={result[name]}', flush=True)
    return result


def settle_prone_actions(rig, meshes, gait, geometry):
    points = [geometry.world_vertices(mesh) for mesh in meshes]
    height = max(value[:, 2].max() for value in points) - min(value[:, 2].min() for value in points)
    scale = 1.86 / height
    for action in list(bpy.data.actions):
        if 'prone' not in action.name:
            continue
        start, end = action.frame_range
        intervals = max(1, math.ceil(end - start))
        frames = [start + (end - start) * index / intervals for index in range(intervals + 1)]
        samples = []
        for frame in frames:
            gait.sample_action(rig, action, frame)
            gait.settle_prone_gaze(rig, meshes, scale)
            samples.append(gait.pose_channels(rig))
        gait.write_action(rig, action.name, frames, samples)


def prune_duplicate_actions():
    """Remove obsolete numbered copies left by earlier GLB round trips."""
    names = {action.name for action in bpy.data.actions}
    for action in list(bpy.data.actions):
        base, separator, suffix = action.name.rpartition('.')
        if separator and suffix.isdigit() and base in names:
            print(f'OPERATOR_ACTION_CLEANUP duplicate={action.name}', flush=True)
            bpy.data.actions.remove(action, do_unlink=True)


def retain_terminal_samples():
    """Keep fractional source endpoints inside Blender's sampled glTF range."""
    for action in bpy.data.actions:
        start, end = action.frame_range
        action.use_frame_range = True
        action.frame_start = math.floor(start)
        action.frame_end = math.ceil(end)


def merge_preview_audit(path):
    """Keep the eight explicitly reviewed manual preview rotation channels."""
    audit = json.loads(path.read_text(encoding='utf-8'))
    expected = {(f'pose.bones["{bone}"].rotation_quaternion', index)
                for bone in ('Spine', 'RightLeg') for index in range(4)}
    channels = audit['preview_keyframes']
    actual = {(entry['data_path'], entry['array_index']) for entry in channels}
    if audit['valid'] is not True or actual != expected or len(channels) != 8:
        raise RuntimeError(f'{path}: invalid manually reviewed preview channel contract')
    action = bpy.data.actions['preview_stand']
    for entry in channels:
        curve = next((curve for curve in action.fcurves
                      if curve.data_path == entry['data_path']
                      and curve.array_index == entry['array_index']), None)
        if curve is None:
            raise RuntimeError(f'preview_stand missing existing channel {entry["data_path"]}')
        while curve.keyframe_points:
            curve.keyframe_points.remove(curve.keyframe_points[-1], fast=True)
        for coordinate, interpolation in entry['after']:
            key = curve.keyframe_points.insert(coordinate[0], coordinate[1], options={'FAST'})
            key.interpolation = interpolation
        curve.update()
    return channels


def validate_preview_audit(channels):
    action = bpy.data.actions['preview_stand']
    maximum = 0.0
    for entry in channels:
        curve = next(curve for curve in action.fcurves
                     if curve.data_path == entry['data_path']
                     and curve.array_index == entry['array_index'])
        if len(curve.keyframe_points) != len(entry['after']):
            raise RuntimeError('Manual preview key count changed during final authoring')
        for key, (coordinate, interpolation) in zip(curve.keyframe_points, entry['after']):
            maximum = max(maximum, abs(key.co.x - coordinate[0]), abs(key.co.y - coordinate[1]))
            if key.interpolation != interpolation:
                raise RuntimeError('Manual preview interpolation changed during final authoring')
    if maximum >= 1.0e-8:
        raise RuntimeError(f'Manual preview rotation changed by {maximum}')
    print(f'OPERATOR_PREVIEW_CHECK channels={len(channels)} max_error={maximum:.10f} valid=true', flush=True)


def export(rig, blend, glb):
    bpy.context.scene.render.fps = 24
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.object.select_all(action='DESELECT')
    root = hierarchy_root(rig)
    wrapper = bpy.data.objects.new('ExportSceneRoot', None)
    bpy.context.scene.collection.objects.link(wrapper)
    root_world = root.matrix_world.copy()
    root.parent = wrapper
    root.matrix_parent_inverse = Matrix.Identity(4)
    root.matrix_world = root_world
    wrapper.select_set(True)
    for obj in wrapper.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = wrapper
    try:
        bpy.ops.export_scene.gltf(
            filepath=str(glb), export_format='GLB', use_selection=True,
            export_apply=True, export_skins=True, export_animations=True,
            export_animation_mode='BROADCAST', export_nla_strips=False,
            export_rest_position_armature=True,
            export_def_bones=True, export_leaf_bone=False, export_morph=False,
            export_materials='EXPORT', export_image_format='AUTO',
            export_texcoords=True, export_normals=True, export_tangents=False,
            export_all_influences=True, export_cameras=False, export_lights=False,
        )
    finally:
        root.parent = None
        root.matrix_parent_inverse = Matrix.Identity(4)
        root.matrix_world = root_world
        bpy.data.objects.remove(wrapper, do_unlink=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--roles', nargs='+', choices=['viper','heron','lynx','magpie','jackal','enemy'],
                        default=['viper','heron','lynx','magpie','jackal','enemy'])
    parser.add_argument('--source-dir', type=Path,
                        help='Read pristine role .blend files here; save corrected sources in the repository')
    parser.add_argument('--preview-audit', type=Path,
                        help='Merge the eight reviewed Viper preview rotation channels from this audit')
    parser.add_argument('--author-pistol', action='store_true',
                        help='Author the complete pistol carry family on an existing corrected source')
    parser.add_argument('--author-rifle', action='store_true',
                        help='Author the rifle prone family on an existing corrected source')
    parser.add_argument('--repair-magpie-debris', action='store_true',
                        help='Remove Magpie\'s disconnected legacy forearm ribbon before export')
    parser.add_argument('--skip-geometry', action='store_true',
                        help='Skip the Blender geometry gate after an explicitly reviewed asset pass')
    parser.add_argument('--finalize-contacts', action='store_true',
                        help='Bake running foot contacts on an existing corrected source')
    parser.add_argument('--settle-prone', action='store_true',
                        help='Bake prone silhouette clearance with glTF interpolation margin')
    parser.add_argument('--stabilize-rifle', action='store_true',
                        help='Bake moving rifle contacts independently of torso lean')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--finish-only', action='store_true', help='Finish a completed correction without reauthoring its skin or actions')
    mode.add_argument('--repair-prone', action='store_true', help='Update the horizontal prone hold and its weapon contacts')
    config = parser.parse_args(argv)
    skin = module('repair_operator_skin')
    gait = module('repair_hy3d_gait')
    pistol = module('author_hy3d_pistol_carry')
    rifle = module('author_hy3d_rifle_prone')
    geometry = module('validate_operator_geometry')
    centering = module('center_operator_actions')
    for role in config.roles:
        if role == 'enemy':
            bpy.ops.wm.open_mainfile(filepath=str(REPO/'source_art/hy3d_operators/viper.blend'))
            rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
            if hierarchy_root(rig).name != PRESENTATION_ROOT:
                raise RuntimeError('Enemy export requires the completed authored Viper presentation')
            meshes = [o for o in rig.children_recursive if o.type == 'MESH' and o.vertex_groups]
            geometry.validate_operator_geometry(rig, meshes, gait.sample_action)
            gait.sample_action(rig, bpy.data.actions['preview_stand'], 0)
            export(rig, REPO/'source_art/combat_models/enemy_operator.blend',
                   REPO/'assets/models/enemy_operator/enemy_operator.glb')
            source_calibration = REPO/'assets/models/hy3d_operators/viper.locomotion.tres'
            (REPO/'assets/models/enemy_operator/enemy_operator.locomotion.tres').write_bytes(source_calibration.read_bytes())
            print('OPERATOR_PRESENTATION_PASS role=enemy valid=true', flush=True)
            continue
        blend = REPO/f'source_art/hy3d_operators/{role}.blend'
        source = config.source_dir/f'{role}.blend' if config.source_dir else blend
        if not source.is_file():
            raise RuntimeError(f'Missing authored operator source: {source}')
        print(f'OPERATOR_SOURCE_CHECK role={role} source={source}', flush=True)
        bpy.ops.wm.open_mainfile(filepath=str(source))
        rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
        restore_authoring_space(rig)
        meshes = [o for o in rig.children_recursive if o.type == 'MESH' and o.vertex_groups]
        if (not config.finish_only and not config.repair_prone) or config.repair_magpie_debris:
            skin.repair_operator_skin(role, rig, meshes)
            meshes = [o for o in rig.children_recursive if o.type == 'MESH' and o.vertex_groups]
        if role == 'viper':
            module('repair_viper_contact').refine_viper_collar(
                rig, skin._read_skin, skin._write_skin, skin._smooth_garments)
        if role == 'lynx':
            backpack = bpy.data.objects['LynxAuthoredBackpack']
            before = len(backpack.data.polygons)
            backpack.data.validate()
            print(f'LYNX_BACKPACK_TOPOLOGY_CHECK removed_duplicate_faces='
                  f'{before - len(backpack.data.polygons)} valid=true', flush=True)
        for mesh in meshes:
            skin.assert_skin_contract(mesh)
        if not config.finish_only and not config.repair_prone:
            gait.repair_locomotion(rig, REPO/'source_art/third_party/quaternius_universal_animation_library/UAL1_Standard.glb')
            if 'steel_tide_running_contact_revision' in rig:
                del rig['steel_tide_running_contact_revision']
        if config.repair_prone:
            crawl = bpy.data.actions['prone_crawl']
            gait.sample_action(rig, crawl, sum(crawl.frame_range) * .5)
            pose = gait.pose_channels(rig)
            gait.write_action(rig, 'prone_idle', [0, 24], [
                {n: tuple(v.copy() for v in values) for n, values in pose.items()},
                {n: tuple(v.copy() for v in values) for n, values in pose.items()},
            ])
        if not config.finish_only:
            rifle.author_rifle_prone_pose(rig)
            pistol.author_pistol_pose(rig)
            skin.author_hair_actions(rig, list(bpy.data.actions))
        elif config.author_rifle:
            rifle.author_rifle_prone_pose(rig)
            if config.author_pistol:
                pistol.author_pistol_pose(rig)
        elif config.author_pistol:
            pistol.author_pistol_pose(rig)
        prune_duplicate_actions()
        if config.finalize_contacts or not config.finish_only:
            contacts = module('finalize_operator_contacts')
            contacts.plant_running_contacts(rig)
        if config.settle_prone or not config.finish_only:
            settle_prone_actions(rig, meshes, gait, geometry)
        if config.stabilize_rifle or not config.finish_only:
            module('stabilize_operator_rifle').stabilize_rifle_actions(rig)
        preview_channels = merge_preview_audit(config.preview_audit) if role == 'viper' and config.preview_audit else None
        retain_terminal_samples()
        if not config.finish_only or config.finalize_contacts or config.settle_prone:
            centering.center_actions(rig, gait.sample_action)
            ground_actions(rig, meshes, gait)
        if preview_channels is not None:
            validate_preview_audit(preview_channels)
        calibration = measure_locomotion(rig, gait)
        presentation_scale = author_presentation_space(rig, geometry)
        calibration = {name: round(value * presentation_scale, 6) for name, value in calibration.items()}
        if not config.skip_geometry:
            geometry.validate_operator_geometry(rig, meshes, gait.sample_action)
        (REPO/f'assets/models/hy3d_operators/{role}.locomotion.tres').write_text(
            '[gd_resource type="Resource" format=3]\n\n[resource]\n'
            + ''.join(f'metadata/{key} = {value}\n' for key, value in calibration.items()),
            encoding='utf-8')
        gait.sample_action(rig, bpy.data.actions['preview_stand'], 0)
        export(rig, blend, REPO/f'assets/models/hy3d_operators/{role}.glb')
        print(f'OPERATOR_PRESENTATION_PASS role={role} valid=true', flush=True)


if __name__ == '__main__':
    main()
