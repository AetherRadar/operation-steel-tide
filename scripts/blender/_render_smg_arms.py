import bpy
from collections import defaultdict

bpy.ops.import_scene.gltf(filepath="source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb")
scene = bpy.context.scene
scene.frame_set(155)
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 900
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.filepath = "build/art-previews/combat_models/smg45_arms_components.png"
for obj in scene.objects:
    obj.hide_render = obj.name != "Object_7"
o = bpy.data.objects["Object_7"]
m = o.data
adj = defaultdict(set)
for edge in m.edges:
    a, b = edge.vertices
    adj[a].add(b)
    adj[b].add(a)
unseen = set(range(len(m.vertices)))
components = []
while unseen:
    seed = unseen.pop()
    stack = [seed]
    component = {seed}
    while stack:
        vertex = stack.pop()
        for neighbor in adj[vertex]:
            if neighbor in unseen:
                unseen.remove(neighbor)
                component.add(neighbor)
                stack.append(neighbor)
    components.append(component)
component_by_vertex = {}
for component_index, component in enumerate(sorted(components, key=len, reverse=True)):
    for vertex in component:
        component_by_vertex[vertex] = component_index
for poly in m.polygons:
    component_index = component_by_vertex[poly.vertices[0]]
    poly.material_index = component_index
for index in range(6):
    mat = bpy.data.materials.new(f"component_{index}")
    mat.diffuse_color = ((index + 1) / 6.0, 0.2, 1.0 - index / 6.0, 1.0)
    o.data.materials.append(mat)
scene.camera = bpy.data.objects.get("Camera")
bpy.ops.render.render(write_still=True)
