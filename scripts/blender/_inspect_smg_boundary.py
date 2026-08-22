import bpy
from collections import defaultdict

bpy.ops.import_scene.gltf(filepath="source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb")
o = bpy.data.objects["Object_7"]
m = o.data
edge_faces = defaultdict(list)
for poly in m.polygons:
    for edge_key in poly.edge_keys:
        edge_faces[tuple(sorted(edge_key))].append(poly.index)
boundary = [edge for edge, faces in edge_faces.items() if len(faces) == 1]
by_vertex = defaultdict(list)
for a, b in boundary:
    by_vertex[a].append(b)
    by_vertex[b].append(a)
for index, neighbors in sorted(by_vertex.items(), key=lambda item: len(item[1])):
    if len(neighbors) != 2:
        v = m.vertices[index]
        print("ENDPOINT", index, "degree", len(neighbors), "co", tuple(round(x, 3) for x in v.co), "neighbors", neighbors[:8])
print("boundary vertices", len(by_vertex))
for y_cut in (-45, -43, -30, -27, 29, 32):
    selected = [index for index in by_vertex if m.vertices[index].co.y < y_cut]
    print("Y<", y_cut, "count", len(selected), "x", (round(min(m.vertices[i].co.x for i in selected), 3), round(max(m.vertices[i].co.x for i in selected), 3)) if selected else None, "z", (round(min(m.vertices[i].co.z for i in selected), 3), round(max(m.vertices[i].co.z for i in selected), 3)) if selected else None)
for threshold in (0.05, 0.15, 0.3, 0.6, 1.0):
    minimum = min(m.vertices[index].co.y for index in by_vertex)
    selected = [index for index in by_vertex if m.vertices[index].co.y <= minimum + threshold]
    print("MIN+", threshold, "count", len(selected), "x", (round(min(m.vertices[i].co.x for i in selected), 3), round(max(m.vertices[i].co.x for i in selected), 3)), "z", (round(min(m.vertices[i].co.z for i in selected), 3), round(max(m.vertices[i].co.z for i in selected), 3)))
