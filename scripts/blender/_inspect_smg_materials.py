import bpy
from collections import Counter, defaultdict

bpy.ops.import_scene.gltf(filepath="source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb")
o = bpy.data.objects["Object_7"]
m = o.data
print("MATERIALS", [(i, slot.name, slot.material.name if slot.material else None) for i, slot in enumerate(o.material_slots)])
for i, p in enumerate(m.polygons):
    if i < 3:
        print("POLY", p.index, p.material_index, p.vertices[:])
print("MATERIAL_COUNTS", Counter(p.material_index for p in m.polygons))
for i, mat in enumerate(o.material_slots):
    if mat.material:
        principled = mat.material.node_tree.nodes.get("Principled BSDF") if mat.material.use_nodes else None
        print("MAT", i, mat.material.name, "base", principled.inputs["Base Color"].default_value[:] if principled else None, "rough", principled.inputs["Roughness"].default_value if principled else None)

edge_faces = defaultdict(list)
for poly in m.polygons:
    for edge_key in poly.edge_keys:
        edge_faces[tuple(sorted(edge_key))].append(poly.index)
boundary_edges = [edge for edge, faces in edge_faces.items() if len(faces) == 1]
boundary_vertices = {index for edge in boundary_edges for index in edge}
print("BOUNDARY", len(boundary_edges), len(boundary_vertices))
for index in sorted(boundary_vertices, key=lambda idx: m.vertices[idx].co.y)[:25]:
    v = m.vertices[index]
    print("BV", index, tuple(round(x, 3) for x in v.co), "groups", [(g.group, round(g.weight, 3)) for g in v.groups])
