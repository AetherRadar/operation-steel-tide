import bpy
from mathutils import Vector

bpy.ops.import_scene.gltf(filepath="assets/models/djmaesen_smg45/smg45_first_person.glb")
scene = bpy.context.scene
scene.frame_set(0)
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 1000
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.filepath = "build/art-previews/combat_models/smg45_asset_cap.png"
for obj in scene.objects:
    obj.hide_render = obj.name in {"Cube", "Icosphere"}
camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
scene.collection.objects.link(camera)
camera.location = Vector((0.0, 1.65, 0.45))
target = Vector((0.0, 0.0, 0.0))
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
camera_data.lens = 48.0
scene.camera = camera
bpy.ops.render.render(write_still=True)
