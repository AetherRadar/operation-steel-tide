"""Export reviewed Blender runtime assets without loading an unrelated city."""
import argparse
import math
from pathlib import Path
import sys
import bpy

ROOT = Path(__file__).resolve().parents[2]


def export(name):
    source = ROOT / 'source_art/lantern_canal_world' / (name + '.blend')
    output = ROOT / 'assets/models/lantern_canal_world' / (name + '.glb')
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB',
                              export_animations=False, export_cameras=False,
                              export_lights=True, export_image_format='AUTO')
    print(f'LANTERN_RUNTIME_EXPORT name={name} bytes={output.stat().st_size}', flush=True)


def mountains():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'source_art/world/jianghai_old_city/jianghai_old_city.blend'))
    keep = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('JianghaiMountainMassif')]
    if len(keep) != 12:
        raise RuntimeError(f'Expected twelve authored mountains, found {len(keep)}')
    for obj in keep:
        matrix = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = matrix
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    export('lantern_mountains')


def city():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'assets/models/lantern_canal_world/lantern_canal_world_v2_5.glb'))
    total_before = total_after = 0
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            continue
        bpy.context.view_layer.objects.active = obj
        obj.data.calc_loop_triangles()
        before = len(obj.data.loop_triangles)
        total_before += before
        # Preserve access geometry exactly: floors, bridges, stairs and workshops.
        protected = obj.name.startswith(('00_', '31_', '32_', '33_', '80_', '60_'))
        if not protected and before > 3000:
            dissolve = obj.modifiers.new('Retain structural planes', 'DECIMATE')
            dissolve.decimate_type = 'DISSOLVE'
            dissolve.angle_limit = math.radians(.5)
            dissolve.delimit = {'MATERIAL', 'UV', 'SHARP'}
            bpy.ops.object.modifier_apply(modifier=dissolve.name)
            obj.data.calc_loop_triangles()
            if len(obj.data.loop_triangles) > 18000:
                simplify = obj.modifiers.new('Authored runtime surface budget', 'DECIMATE')
                simplify.ratio = max(.12, min(.5, 65000 / len(obj.data.loop_triangles)))
                simplify.use_collapse_triangulate = True
                bpy.ops.object.modifier_apply(modifier=simplify.name)
        obj.data.calc_loop_triangles()
        after = len(obj.data.loop_triangles)
        total_after += after
        print(f'LANTERN_MESH_CHECK name={obj.name} before={before} after={after} protected={protected}', flush=True)
    for image in bpy.data.images:
        if image.size[0] > 1024 or image.size[1] > 1024:
            ratio = 1024 / max(image.size)
            image.scale(max(1, round(image.size[0] * ratio)), max(1, round(image.size[1] * ratio)))
            image.pack()
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    print(f'LANTERN_GEOMETRY_CHECK before={total_before} after={total_after}', flush=True)
    export('lantern_canal_runtime')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--mountains', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    mountains() if args.mountains else city()
