"""Audit the licensed authored hand geometry and connected surfaces in Blender."""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT / 'assets/models/djmaesen_smg45/smg45_rifle_arms.glb'))
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    adjacency = {vertex.index: set() for vertex in obj.data.vertices}
    for edge in obj.data.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    remaining = set(adjacency)
    components = []
    while remaining:
        vertices = {remaining.pop()}
        stack = list(vertices)
        while stack:
            for neighbor in adjacency[stack.pop()]:
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    vertices.add(neighbor)
                    stack.append(neighbor)
        components.append(vertices)
    for component in sorted(components, key=len, reverse=True):
        points = [obj.matrix_world @ obj.data.vertices[index].co for index in component]
        print('COMPONENT', obj.name, len(component), 'center', list(sum(points, Vector()) / len(points)),
              'min', [min(p[i] for p in points) for i in range(3)],
              'max', [max(p[i] for p in points) for i in range(3)])
    for material in obj.data.materials:
        print('MATERIAL', material.name)
        for node in material.node_tree.nodes:
            if node.type == 'TEX_IMAGE': print('TEXTURE', node.image.name, list(node.image.size))
