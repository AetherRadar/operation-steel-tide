"""Fit the CC0 Poly Haven ladder to the authored canal banks and export access markers."""
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'source_art/lantern_canal_world/lantern_canal_runtime.blend'))
    ground = bpy.data.objects['00_Ground_and_quays']
    inverse = ground.matrix_world.inverted()

    def height(x, y):
        hit, point, _, _ = ground.ray_cast(inverse @ Vector((x, y, 10)),
                                         (inverse.to_3x3() @ Vector((0, 0, -1))).normalized())
        return (ground.matrix_world @ point).z if hit else -100

    cross_banks = []
    previous = height(68, -80)
    for index in range(-319, 721):
        y = index / 4
        current = height(68, y)
        if (previous < 0) != (current < 0):
            cross_banks.append((y - .125, 1 if current < 0 else -1))
        previous = current
    if len(cross_banks) != 4:
        raise RuntimeError(f'Expected two cross canals, found banks: {cross_banks}')

    placements = []
    for y in (-62, -22, 20, 78, 132, 145):
        for side in (-1, 1):
            placements.append((Vector((side * 6.70, y, 0)), Vector((-side, 0, 0))))
    for y, direction in cross_banks:
        for x in (-68, -50, -20, 20, 50, 68):
            if y > 114 and x == 68:
                x = 54
            placements.append((Vector((x, y + direction * .20, 0)), Vector((0, direction, 0))))
    bed = bpy.data.objects['60_V23_Submerged_riverbed']
    bed_inverse = bed.matrix_world.inverted()
    bottoms = []
    for position, outward in placements:
        top = position - outward * 1.10
        print(f'CANAL_ACCESS_BANK top={list(top)} floor={height(top.x, top.y)}', flush=True)
        if height(top.x, top.y) < 1:
            raise RuntimeError(f'Ladder landing is not on a dry bank: {top}')
        floors = []
        for dx in (-.30, 0, .30):
            for dy in (-.30, 0, .30):
                probe = position + outward * .55 + Vector((dx, dy, 0))
                probe.z = 0
                floors.append(height(probe.x, probe.y))
                hit, point, _, _ = bed.ray_cast(bed_inverse @ probe,
                    (bed_inverse.to_3x3() @ Vector((0, 0, -1))).normalized())
                if hit:
                    floors.append((bed.matrix_world @ point).z)
        floor = max(floors)
        if not -2 < floor < 0:
            raise RuntimeError(f'Invalid riverbed below ladder {position}: {floor}')
        bottoms.append(floor + .10)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT / 'source_art/third_party/polyhaven_canal_ladder/ladder_sectioned_01.gltf'))
    template = bpy.data.objects['ladder_section_01']
    template.data = template.data.copy()
    points = [template.matrix_world @ vertex.co for vertex in template.data.vertices]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center = (minimum + maximum) * .5
    for vertex, point in zip(template.data.vertices, points):
        vertex.co = Vector((point.x - center.x, point.y - center.y,
                            (point.z - minimum.z) * 3.30 / (maximum.z - minimum.z) - 1.48))
    template.matrix_world = Matrix.Identity(4)
    mesh = template.data
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    root = bpy.data.objects.new('LanternCanalAccess', None)
    bpy.context.collection.objects.link(root)

    def empty(name, parent, position):
        obj = bpy.data.objects.new(name, None)
        bpy.context.collection.objects.link(obj)
        obj.parent = parent
        obj.location = position
        return obj

    for index, (position, outward) in enumerate(placements):
        route = empty(f'CanalAccess_{index:02}', root, position)
        right = outward.cross(Vector((0, 0, 1)))
        route.rotation_mode = 'QUATERNION'
        route.rotation_quaternion = Matrix((right, outward, Vector((0, 0, 1)))).transposed().to_quaternion()
        ladder = bpy.data.objects.new(f'CanalLadder_{index:02}', mesh)
        bpy.context.collection.objects.link(ladder)
        ladder.parent = route
        empty(f'BottomFeet_{index:02}', route, Vector((0, .55, bottoms[index])))
        empty(f'TopFeet_{index:02}', route, Vector((0, -1.10, 1.43)))
        empty(f'Outward_{index:02}', route, Vector((0, 1, 0)))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'source_art/lantern_canal_world/canal_access.blend'), compress=True)
    bpy.ops.export_scene.gltf(filepath=str(ROOT / 'assets/models/lantern_canal_world/canal_access.glb'),
                              export_format='GLB', export_animations=False, export_cameras=False,
                              export_lights=False)
    print(f'CANAL_ACCESS_ART_PASS routes={len(placements)} banks={cross_banks}', flush=True)


if __name__ == '__main__':
    main()
