import bpy
from collections import defaultdict

bpy.ops.import_scene.gltf(filepath="source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb")
o = bpy.data.objects["Object_7"]
m = o.data
adj = defaultdict(set)
edge_faces = defaultdict(list)
for poly in m.polygons:
    for edge_key in poly.edge_keys:
        edge_faces[tuple(sorted(edge_key))].append(poly.index)
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
for component in sorted(components, key=len, reverse=True):
    boundaries = [edge for edge, faces in edge_faces.items() if len(faces) == 1 and edge[0] in component and edge[1] in component]
    xs = [m.vertices[index].co.x for index in component]
    ys = [m.vertices[index].co.y for index in component]
    zs = [m.vertices[index].co.z for index in component]
    print("COMP", len(component), "bounds", tuple(round(value, 3) for value in (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))), "boundary_edges", len(boundaries))
    if boundaries:
        bverts = {index for edge in boundaries for index in edge}
        by = sorted((m.vertices[index].co.y, index) for index in bverts)
        print("  boundary_y", tuple(round(value, 3) for value, _ in by[:8]), "...", tuple(round(value, 3) for value, _ in by[-8:]))
        badj = defaultdict(set)
        for a, b in boundaries:
            badj[a].add(b)
            badj[b].add(a)
        unseen_b = set(bverts)
        loops = []
        while unseen_b:
            seed_b = unseen_b.pop()
            stack_b = [seed_b]
            loop = {seed_b}
            while stack_b:
                vertex_b = stack_b.pop()
                for neighbor_b in badj[vertex_b]:
                    if neighbor_b in unseen_b:
                        unseen_b.remove(neighbor_b)
                        loop.add(neighbor_b)
                        stack_b.append(neighbor_b)
            loop_y = [m.vertices[index].co.y for index in loop]
            loops.append((len(loop), min(loop_y), max(loop_y)))
        print("  boundary_loops", tuple((size, round(low, 3), round(high, 3)) for size, low, high in sorted(loops, reverse=True)))
