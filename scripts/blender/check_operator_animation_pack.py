import bpy
import glob
import os

paths = [
    "assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb",
    *glob.glob("assets/models/quaternius_operators/*.glb"),
]
expected = {
    "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk",
    "ready_idle", "ready_walk", "ready_run", "ready_sprint",
    "ready_crouch_idle", "ready_crouch_walk", "aim_idle", "aim_walk",
    "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk",
    "prone_idle", "prone_crawl", "hit", "death", "downed", "revive_kneel",
    "revived", "shoot", "reload", "melee", "throw", "interact", "pickup",
    "heal", "jump_start", "jump_loop", "jump_land", "slide_start",
    "slide_loop", "slide_exit",
}
valid = True
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    names = {action.name for action in bpy.data.actions}
    missing = sorted(expected - names)
    valid = valid and not missing
    print(
        "ANIMATION_PACK_CHECK",
        os.path.basename(path),
        len(names),
        "shoot" in names,
        "reload" in names,
        "jump_loop" in names,
        "missing=" + ",".join(missing),
    )
print("ANIMATION_PACK_PASS valid=" + str(valid))
