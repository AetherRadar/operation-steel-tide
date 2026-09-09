"""Build the field enemy operator from the approved HY-3D Viper asset.

The enemy uses the same authored skeleton and animation contract as the Viper
operator, but is exported as its own asset so enemy presentation can evolve in
Blender without changing the squad roster assets or adding runtime shape fixes.
"""

from __future__ import annotations

from pathlib import Path

import bpy


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_GLB = REPO_ROOT / "assets" / "models" / "hy3d_operators" / "viper.glb"
OUTPUT_BLEND = REPO_ROOT / "source_art" / "combat_models" / "enemy_operator.blend"
OUTPUT_GLB = REPO_ROOT / "assets" / "models" / "enemy_operator" / "enemy_operator.glb"


REQUIRED_NODES = {
    "QuaterniusOperator",
    "QuaterniusOperatorRig",
    "OperatorBody",
    "WeaponSocket",
    "BackWeaponSocket",
    "HeadSocket",
    "VestSocket",
    "BackpackSocket",
    "TeamPatchSocket",
}


def main() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))

    root = next(
        obj for obj in bpy.context.scene.objects
        if obj.name == "QuaterniusOperator"
    )
    armature = next(obj for obj in root.children_recursive if obj.type == "ARMATURE")
    mesh = next(obj for obj in root.children_recursive if obj.type == "MESH")

    names = {obj.name for obj in root.children_recursive} | {root.name}
    missing = sorted(REQUIRED_NODES - names)
    if missing:
        raise RuntimeError(f"Enemy operator is missing authored nodes: {missing}")

    for obj in list(bpy.data.objects):
        lowered = obj.name.casefold()
        if obj.type == "MESH" and (
            lowered == "icosphere" or lowered.startswith("icosphere.")
            or lowered == "cube" or lowered.startswith("cube.")
        ):
            bpy.data.objects.remove(obj, do_unlink=True)

    OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_animations=True,
        export_skins=True,
        export_morph=False,
        export_materials="EXPORT",
    )
    print(
        "ENEMY_OPERATOR_CHECK "
        f"mesh={mesh.name} armature={armature.name} "
        f"nodes={len(REQUIRED_NODES)} output={OUTPUT_GLB}"
    )
    print("ENEMY_OPERATOR_PASS valid=true")


if __name__ == "__main__":
    main()
