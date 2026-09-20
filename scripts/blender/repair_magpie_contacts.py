"""Restore Magpie's authored right forearm and glove from its intact left side.

The old prop cleanup removed most of the right forearm and the old patch
copied only palm-dominant faces. Copy the complete inspected source surface,
including exposed fingers, with its original UVs. The caller saves/exports.
"""

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

from repair_operator_skin import _read_skin, _write_skin


def _vertex_colors(mesh):
    image = next(node.image for node in mesh.data.materials[0].node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image is not None)
    width, height = image.size
    pixels = np.asarray(image.pixels[:]).reshape(height, width, 4)
    colors = np.zeros((len(mesh.data.vertices), 3))
    for loop in mesh.data.loops:
        u, v = mesh.data.uv_layers.active.data[loop.index].uv
        colors[loop.vertex_index] = pixels[
            min(height - 1, max(0, int(v * height))),
            min(width - 1, max(0, int(u * width))), :3]
    return colors


def _source_forearm_mask(positions, colors):
    x, y, z = positions.T
    # Inspected Magpie source: the thigh pouch ends at x=.054, while its
    # exposed thumb crosses x=.053. Preserve that thumb using the skin atlas.
    flesh = (colors[:, 0] - colors[:, 1] > .065)
    distal = (x > .055) | (flesh & (x > .050))
    return ((z > .745) & (z < 1.165) & (y > -.095) & (x > .045)
            & ((z > .94) | distal) & ((z < .98) | flesh))


def _paint_forearm(mesh, rig, side, positions, selected, weights):
    wrist = np.asarray(rig.matrix_world @ rig.data.bones[side + "Hand"].head_local)
    elbow = np.asarray(rig.matrix_world @ rig.data.bones[side + "ForeArm"].head_local)
    axis = elbow - wrist
    axis /= np.linalg.norm(axis)
    distance = (positions - wrist) @ axis
    forearm = np.clip((distance - .016) / .027, 0, 1)
    weights[selected] = 0
    weights[selected, mesh.vertex_groups[side + "ForeArm"].index] = forearm[selected]
    weights[selected, mesh.vertex_groups[side + "Hand"].index] = 1 - forearm[selected]


def repair_magpie_contacts(rig):
    """Run once on Magpie's source, before generic garment consolidation."""
    mesh = bpy.data.objects["OperatorBody"]
    if mesh.get("steel_tide_magpie_complete_hand_revision") == 1:
        return {"already_authored": True}
    positions, weights = _read_skin(mesh)
    colors = _vertex_colors(mesh)
    selected = _source_forearm_mask(positions, colors)
    polygons = [face for face in mesh.data.polygons
                if all(selected[index] for index in face.vertices)]
    if len(polygons) < 800:
        raise RuntimeError("Magpie's complete authored left forearm/glove was not found")

    # A spatial reflection preserves handedness; the small rotation aligns
    # the unequal source elbow/wrist directions without stretching the glove.
    reflection = Matrix.Diagonal(Vector((-1, 1, 1, 1)))
    left_wrist = rig.matrix_world @ rig.data.bones["LeftHand"].head_local
    right_wrist = rig.matrix_world @ rig.data.bones["RightHand"].head_local
    left_elbow = rig.matrix_world @ rig.data.bones["LeftForeArm"].head_local
    right_elbow = rig.matrix_world @ rig.data.bones["RightForeArm"].head_local
    rotation = (reflection.to_3x3() @ (left_elbow - left_wrist)).rotation_difference(right_elbow - right_wrist)
    transform = Matrix.Translation(right_wrist) @ rotation.to_matrix().to_4x4() @ reflection @ Matrix.Translation(-left_wrist)
    source_indices = sorted({index for face in polygons for index in face.vertices})
    remap = {index: new_index for new_index, index in enumerate(source_indices)}
    coordinates = [transform @ Vector(positions[index]) for index in source_indices]
    forearm_axis = (right_elbow - right_wrist).normalized()
    forearm_ratio = (right_elbow - right_wrist).length / (left_elbow - left_wrist).length
    # Magpie's right forearm is 7% longer. Fit only the copied exposed arm
    # along that anatomical axis; the complete glove keeps its source shape.
    coordinates = [point + forearm_axis * max(0, (point - right_wrist).dot(forearm_axis))
                   * (forearm_ratio - 1) for point in coordinates]
    faces = [[remap[index] for index in reversed(face.vertices)] for face in polygons]
    old_patch = bpy.data.objects.get("MagpieRightHandPatch")
    if old_patch is not None:
        bpy.data.objects.remove(old_patch, do_unlink=True)
    data = bpy.data.meshes.new("MagpieRightHandSurface")
    data.from_pydata(coordinates, [], faces)
    data.update()
    for material in mesh.data.materials:
        data.materials.append(material)
    uv = data.uv_layers.new(name=mesh.data.uv_layers.active.name)
    for new_face, old_face in zip(data.polygons, polygons):
        new_face.material_index = old_face.material_index
        new_face.use_smooth = True
        for new_loop, old_loop in zip(new_face.loop_indices, reversed(old_face.loop_indices)):
            uv.data[new_loop].uv = mesh.data.uv_layers.active.data[old_loop].uv.copy()
    patch = bpy.data.objects.new("MagpieRightHandPatch", data)
    bpy.context.collection.objects.link(patch)
    patch.parent = rig
    patch.matrix_world = Matrix.Identity(4)
    for group in mesh.vertex_groups:
        patch.vertex_groups.new(name=group.name)
    patch_positions = np.asarray([list(point) for point in coordinates])
    patch_weights = np.zeros((len(coordinates), len(patch.vertex_groups)))
    _paint_forearm(patch, rig, "Right", patch_positions, np.ones(len(coordinates), dtype=bool), patch_weights)
    _write_skin(patch, patch_weights)
    modifier = patch.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    modifier.use_deform_preserve_volume = True
    patch["steel_tide_magpie_complete_hand_revision"] = 1

    # Restore continuous anatomical weights on the intact left surface, and
    # remove synthetic finger labels from the separately textured thigh pouch.
    _paint_forearm(mesh, rig, "Left", positions, selected, weights)
    for side, region in (("Left", (positions[:, 0] < .055) & (positions[:, 2] < .92)),
                         ("Right", (positions[:, 0] > -.408) & (positions[:, 2] < .94))):
        hand_groups = [group.index for group in mesh.vertex_groups if group.name.startswith(side + "Hand")]
        remote = region & ~selected & (weights[:, hand_groups].sum(axis=1) > 0)
        weights[remote] = 0
        weights[remote, mesh.vertex_groups[side + "UpLeg"].index] = 1
    _write_skin(mesh, weights)

    # The remaining old right forearm consists of a narrow disconnected
    # ribbon outside the sleeve. Remove it after the complete surface exists.
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bm.verts.ensure_lookup_table()
    debris = [vertex for vertex in bm.verts
              if (positions[vertex.index, 0] < -.410
                  and .94 < positions[vertex.index, 2] < 1.125
                  and colors[vertex.index, 0] - colors[vertex.index, 1] > .065)]
    bmesh.ops.delete(bm, geom=debris, context="VERTS")
    bm.to_mesh(mesh.data)
    bm.free()
    mesh.data.update()
    mesh["steel_tide_magpie_complete_hand_revision"] = 1
    result = {"faces": len(faces), "vertices": len(coordinates), "old_ribbon_vertices": len(debris)}
    print("MAGPIE_COMPLETE_HAND_CHECK", result, flush=True)
    return result
