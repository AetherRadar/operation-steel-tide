"""Remove importer helper meshes from the shipped HY-3D operator exports.

The helper objects are Blender source artifacts (Cube/Icosphere) and are not
part of the authored character. Cleaning them here keeps runtime code free of
visual repair logic.
"""

from __future__ import annotations

from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2] / "assets" / "models" / "hy3d_operators"
ROLES = ("heron", "lynx", "magpie", "jackal", "viper")


def helper_mesh(obj: bpy.types.Object) -> bool:
    if obj.type != "MESH":
        return False
    name = obj.name.casefold()
    return name == "icosphere" or name.startswith("icosphere.") \
        or name == "cube" or name.startswith("cube.")


def main() -> None:
    for role in ROLES:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        source = ROOT / f"{role}.glb"
        bpy.ops.import_scene.gltf(filepath=str(source))
        removed = []
        for obj in list(bpy.data.objects):
            if helper_mesh(obj):
                removed.append(obj.name)
                bpy.data.objects.remove(obj, do_unlink=True)
        root = bpy.data.objects.get("QuaterniusOperator")
        if root is None:
            raise RuntimeError(f"{role}: missing QuaterniusOperator root")
        bpy.ops.object.select_all(action="DESELECT")
        root.select_set(True)
        for child in root.children_recursive:
            child.select_set(True)
        bpy.context.view_layer.objects.active = root
        bpy.ops.export_scene.gltf(
            filepath=str(source),
            export_format="GLB",
            use_selection=True,
            export_apply=False,
            export_animations=True,
            export_skins=True,
            export_morph=False,
            export_materials="EXPORT",
        )
        print(f"HY3D_CLEAN role={role} removed={','.join(sorted(removed))}")
    print("HY3D_CLEAN_PASS valid=true")


if __name__ == "__main__":
    main()
