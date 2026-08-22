import bpy
from collections import defaultdict, Counter

bpy.ops.import_scene.gltf(filepath="source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb")
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
for number, component in enumerate(sorted(components, key=len, reverse=True)):
    groups = Counter()
    for index in component:
        for group in m.vertices[index].groups:
            if group.weight >= 0.1:
                groups[group.group] += 1
    named = [(o.vertex_groups[group].name, count) for group, count in groups.most_common()]
    print(number, len(component), named[:12])
    boundary = []
    edge_faces = defaultdict(list)
    for poly in m.polygons:
        for edge_key in poly.edge_keys:
            edge_faces[tuple(sorted(edge_key))].append(poly.index)
    for edge, faces in edge_faces.items():
        if len(faces) == 1 and edge[0] in component and edge[1] in component:
            boundary.extend(edge)
    unique_boundary = set(boundary)
    if unique_boundary:
        low = min(m.vertices[index].co.y for index in unique_boundary)
        high = max(m.vertices[index].co.y for index in unique_boundary)
        for label, cut in (("LOW", low + 0.5), ("HIGH", high - 0.5)):
            ring = [index for index in unique_boundary if (m.vertices[index].co.y <= cut if label == "LOW" else m.vertices[index].co.y >= cut)]
            print("  ", label, len(ring), "bounds", tuple(round(value, 3) for value in (min(m.vertices[index].co.x for index in ring), max(m.vertices[index].co.x for index in ring), min(m.vertices[index].co.y for index in ring), max(m.vertices[index].co.y for index in ring), min(m.vertices[index].co.z for index in ring), max(m.vertices[index].co.z for index in ring))))
            if number < 2 and label == "LOW":
                print("   RING", [(index, tuple(round(value, 3) for value in m.vertices[index].co)) for index in sorted(ring, key=lambda idx: (m.vertices[idx].co.z, m.vertices[idx].co.x))])
                ring_set = set(ring)
                print("   RING_EDGES", [(edge.vertices[0], edge.vertices[1]) for edge in m.edges if edge.vertices[0] in ring_set and edge.vertices[1] in ring_set])
