"""Preserve the painted garrison design on the repaired Heron source rig."""
from pathlib import Path
import sys
import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
import repair_operator_presentation as pipeline
import repair_hy3d_gait as gait
import validate_operator_geometry as geometry


def main():
    design = ROOT / 'source_art/chinese_garrison/uniform_design.blend'
    helmet_design = ROOT / 'source_art/chinese_garrison/helmet_design.blend'
    bpy.ops.wm.open_mainfile(filepath=str(helmet_design))
    original_rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    original_head = original_rig.matrix_world @ original_rig.data.bones['Head'].matrix_local
    original_helmet = bpy.data.objects['GarrisonHelmet'].matrix_world.copy()
    helmet_offset = original_head.inverted() @ original_helmet
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'source_art/hy3d_operators/heron.blend'))
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    pipeline.restore_authoring_space(rig)
    with bpy.data.libraries.load(str(design), link=False) as (_, loaded):
        loaded.objects = ['OperatorBody']
    painted = loaded.objects[0]
    body = bpy.data.objects['OperatorBody']
    body.data.materials.clear()
    for material in painted.data.materials:
        body.data.materials.append(material)
        for node in material.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                node.image.name = 'GarrisonUniformPaint'
                node.image.filepath = '//GarrisonUniformPaint.png'
    bpy.data.objects.remove(painted, do_unlink=True)
    with bpy.data.libraries.load(str(helmet_design), link=False) as (_, loaded):
        loaded.objects = ['GarrisonHelmet']
    helmet = loaded.objects[0]
    for material in helmet.data.materials:
        for node in material.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                node.image.name = 'GarrisonHelmetPaint'
                node.image.filepath = '//GarrisonHelmetPaint.png'
    bpy.context.scene.collection.objects.link(helmet)
    # Bind the authored helmet rigidly in the source head's rest coordinate frame.
    helmet.modifiers.clear()
    helmet.parent = rig
    helmet.parent_type = 'BONE'
    helmet.parent_bone = 'Head'
    gait.reset_pose(rig)
    bpy.context.view_layer.update()
    helmet.matrix_world = rig.matrix_world @ rig.data.bones['Head'].matrix_local @ helmet_offset
    bpy.context.view_layer.update()
    head_group = body.vertex_groups['Head'].index
    head_points = [body.matrix_world @ vertex.co for vertex in body.data.vertices
                   if any(group.group == head_group and group.weight > .8 for group in vertex.groups)]
    helmet_points = [helmet.matrix_world @ vertex.co for vertex in helmet.data.vertices]
    matrix = helmet.matrix_world.copy()
    for axis in (0, 1):
        head_center = (min(point[axis] for point in head_points) + max(point[axis] for point in head_points)) * .5
        helmet_center = (min(point[axis] for point in helmet_points) + max(point[axis] for point in helmet_points)) * .5
        matrix.translation[axis] += head_center - helmet_center
    helmet.matrix_world = matrix
    bpy.context.view_layer.update()
    rig['steel_tide_garrison_identity'] = 'olive_uniform_wei_badge_helmet'
    pipeline.prune_duplicate_actions()
    meshes = [o for o in rig.children_recursive if o.type == 'MESH']
    pipeline.settle_prone_actions(rig, meshes, gait, geometry)
    pipeline.ground_actions(rig, meshes, gait)
    calibration = pipeline.measure_locomotion(rig, gait)
    scale = pipeline.author_presentation_space(rig, geometry)
    geometry.validate_operator_geometry(rig, meshes, gait.sample_action)
    gait.sample_action(rig, bpy.data.actions['preview_stand'], 0)
    pipeline.export(rig, ROOT/'source_art/combat_models/enemy_operator.blend',
                    ROOT/'assets/models/enemy_operator/enemy_operator.glb')
    (ROOT/'assets/models/enemy_operator/enemy_operator.locomotion.tres').write_text(
        '[gd_resource type="Resource" format=3]\n\n[resource]\n' + ''.join(
            f'metadata/{key} = {value * scale:.6f}\n' for key, value in calibration.items()), encoding='utf-8')
    print('DISTINCT_GARRISON_PASS valid=true', flush=True)


if __name__ == '__main__':
    main()
