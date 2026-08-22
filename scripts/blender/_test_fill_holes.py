import bpy
import bmesh
from mathutils import Vector

bpy.ops.import_scene.gltf(filepath="source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb")
arms = bpy.data.objects["Object_7"]
mesh = arms.data
bm = bmesh.new()
bm.from_mesh(mesh)
boundary_edges = [edge for edge in bm.edges if edge.is_boundary]
print("BOUNDARY", len(boundary_edges))
bmesh.ops.holes_fill(bm, edges=boundary_edges, sides=0)
bm.to_mesh(mesh)
bm.free()
mesh.update()
root = bpy.data.objects["Sketchfab_model"]
root.name = "Preview"
for obj in bpy.context.scene.objects:
    obj.hide_render = obj.name not in {"Object_7", "Camera"}
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 1000
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.filepath = "build/art-previews/combat_models/smg45_fill_holes.png"
scene.camera = bpy.data.objects.get("Camera")
bpy.ops.render.render(write_still=True)
