"""Separate Viper's authored glove from the touching thigh-pouch surface.

The original atlas distinguishes dark glove fabric and exposed fingertips from
the olive pouch. All visible faces and UVs are retained. Only the contact and
cuff boundaries are closed in Blender; no runtime geometry repair is used.
"""

from __future__ import annotations

import bmesh
import bpy
import numpy as np


def _forearm_fraction(rig, positions, side):
    wrist = np.asarray(rig.matrix_world @ rig.data.bones[side + "Hand"].head_local)
    axis = np.asarray(rig.matrix_world @ rig.data.bones[side + "ForeArm"].head_local) - wrist
    axis /= np.linalg.norm(axis)
    blend = np.clip(((positions - wrist) @ axis - 0.005) / 0.035, 0.0, 1.0)
    return blend * blend * (3.0 - 2.0 * blend)


def _glove_weights(rig, mesh, positions):
    weights = np.zeros((len(positions), len(mesh.vertex_groups)))
    blend = _forearm_fraction(rig, positions, "Right")
    weights[:, mesh.vertex_groups["RightHand"].index] = 1.0 - blend
    weights[:, mesh.vertex_groups["RightForeArm"].index] = blend
    return weights


def _atlas_colors(mesh):
    atlas = next(node.image for node in mesh.data.materials[0].node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image is not None)
    width, height = atlas.size
    pixels = np.asarray(atlas.pixels[:], dtype=np.float32).reshape(height, width, 4)
    colors = np.zeros((len(mesh.data.vertices), 3))
    for loop in mesh.data.loops:
        u, v = mesh.data.uv_layers.active.data[loop.index].uv
        colors[loop.vertex_index] = pixels[min(height - 1, max(0, int(v * height))),
                                            min(width - 1, max(0, int(u * width))), :3]
    return colors


def _paint_exposed_forearms(rig, mesh, positions, weights, colors):
    # A flesh mask restricted to the actual forearm segment excludes the belt
    # beside the relaxed arm. It prevents torso weights stretching the skin.
    for side in ("Left", "Right"):
        bone = rig.data.bones[side + "ForeArm"]
        start = np.asarray(rig.matrix_world @ bone.head_local)
        axis = np.asarray(rig.matrix_world @ bone.tail_local) - start
        fraction = np.clip((positions - start) @ axis / (axis @ axis), 0.0, 1.0)
        distance = np.linalg.norm(positions - (start + fraction[:, None] * axis), axis=1)
        skin = (colors[:, 0] > 0.30) & (colors[:, 0] > colors[:, 1] * 1.08)
        skin &= (distance < 0.075) & (fraction > 0.02) & (fraction < 0.98)
        weights[skin] = 0.0
        blend = _forearm_fraction(rig, positions, side)
        weights[skin, mesh.vertex_groups[side + "ForeArm"].index] = blend[skin]
        weights[skin, mesh.vertex_groups[side + "Hand"].index] = 1.0 - blend[skin]

    # The inspected olive belt behind the relaxed right forearm was also
    # labelled as arm skin by the original nearest-bone binder. Its atlas and
    # location distinguish it from the pale exposed forearm above/outside it.
    belt = (positions[:, 0] > -0.34) & (positions[:, 0] < -0.275)
    belt &= (positions[:, 1] > 0.045) & (positions[:, 1] < 0.13)
    belt &= (positions[:, 2] > 0.98) & (positions[:, 2] < 1.125)
    belt &= (colors[:, 1] > colors[:, 2] * 1.12) & (colors[:, 0] - colors[:, 1] < 0.08)
    weights[belt] = 0.0
    weights[belt, mesh.vertex_groups["Hips"].index] = 0.8
    weights[belt, mesh.vertex_groups["Spine"].index] = 0.2


def _extract_faces(mesh, rig, polygons):
    source = mesh.data
    indices = sorted({index for face in polygons for index in face.vertices})
    remap = {original: target for target, original in enumerate(indices)}
    data = bpy.data.meshes.new("AuthoredRightGlove")
    data.from_pydata([source.vertices[index].co.copy() for index in indices], [],
                     [[remap[index] for index in face.vertices] for face in polygons])
    for material in source.materials:
        data.materials.append(material)
    uv = data.uv_layers.new(name=source.uv_layers.active.name)
    for target, original in zip(data.polygons, polygons):
        target.material_index = original.material_index
        target.use_smooth = True
        for target_loop, original_loop in zip(target.loop_indices, original.loop_indices):
            uv.data[target_loop].uv = source.uv_layers.active.data[original_loop].uv
    glove = bpy.data.objects.new("AuthoredRightGlove", data)
    bpy.context.collection.objects.link(glove)
    glove.parent = mesh.parent
    glove.matrix_world = mesh.matrix_world.copy()
    for group in mesh.vertex_groups:
        glove.vertex_groups.new(name=group.name)
    modifier = glove.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    return glove, indices


def _close_contact(mesh):
    edit = bmesh.new()
    edit.from_mesh(mesh.data)
    boundary = [edge for edge in edit.edges if edge.is_boundary
                and all(vertex.co.x < -0.34 and 0.75 < vertex.co.z < 1.02 for vertex in edge.verts)]
    filled = bmesh.ops.holes_fill(edit, edges=boundary, sides=0)
    uv = edit.loops.layers.uv.active
    for face in filled.get("faces", []):
        face.smooth = True
        for loop in face.loops:
            adjacent = [other for other in loop.vert.link_loops if other.face != face]
            if adjacent:
                loop[uv].uv = adjacent[0][uv].uv
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh.data)
    edit.free()
    mesh.data.update()


def repair_viper_contact(rig, mesh, positions, weights, read_skin, write_skin):
    """Return the independently skinned glove; the caller smooths the torso."""
    if mesh.get("steel_tide_authored_glove_contact"):
        return bpy.data.objects.get("AuthoredRightGlove")
    if bpy.data.objects.get("AuthoredRightGlove") is not None:
        raise RuntimeError("Viper contact exists without its source repair marker")
    # Join only exact UV copies around this contact before closing its loops.
    # A broad body weld would merge the separate vest and upper-arm layers.
    write_skin(mesh, weights)
    edit = bmesh.new()
    edit.from_mesh(mesh.data)
    contact = [vertex for vertex in edit.verts if vertex.co.x < -0.34 and vertex.co.z < 1.02]
    bmesh.ops.remove_doubles(edit, verts=contact, dist=1.0e-6)
    edit.to_mesh(mesh.data)
    edit.free()
    mesh.data.update()
    positions, weights = read_skin(mesh)
    colors = _atlas_colors(mesh)
    _paint_exposed_forearms(rig, mesh, positions, weights, colors)
    hand = mesh.vertex_groups["RightHand"].index
    thigh = mesh.vertex_groups["RightUpLeg"].index
    # These are inspected authoring-space bounds, before presentation transforms.
    pouch = (positions[:, 0] > -0.422) & (positions[:, 0] < -0.36)
    pouch &= (positions[:, 2] > 0.77) & (positions[:, 2] < 0.91)
    pouch &= (colors[:, 0] > 0.16) & (colors[:, 0] - colors[:, 1] < 0.065)
    pouch &= colors[:, 2] < colors[:, 1] * 0.9
    selected = (weights[:, hand] > 0.4) & ~pouch & (positions[:, 2] < 0.99)
    polygons = [face for face in mesh.data.polygons
                if sum(selected[list(face.vertices)]) >= 2]
    if not 1000 <= len(polygons) <= 5000:
        raise RuntimeError(f"Viper glove source selection changed: {len(polygons)} faces")
    glove, indices = _extract_faces(mesh, rig, polygons)
    write_skin(glove, _glove_weights(rig, glove, positions[indices]))
    write_skin(mesh, weights)
    removed = {face.index for face in polygons}
    edit = bmesh.new()
    edit.from_mesh(mesh.data)
    edit.faces.ensure_lookup_table()
    bmesh.ops.delete(edit, geom=[face for face in edit.faces if face.index in removed], context="FACES")
    edit.to_mesh(mesh.data)
    edit.free()
    mesh.data.update()
    positions, weights = read_skin(mesh)
    retained_pouch = (positions[:, 0] < -0.34) & (positions[:, 2] < 0.95)
    weights[retained_pouch, thigh] += weights[retained_pouch, hand]
    weights[retained_pouch, hand] = 0.0
    write_skin(mesh, weights)
    for surface in (mesh, glove):
        _close_contact(surface)
    mesh["steel_tide_authored_glove_contact"] = True
    glove["steel_tide_authored_glove_contact"] = True
    print(f"VIPER_CONTACT_CHECK glove_faces={len(polygons)} pouch_vertices={int(retained_pouch.sum())}")
    return glove


def refine_viper_skin(rig, read_skin, write_skin, smooth):
    """Finish garment gradients while keeping the authored palms unchanged."""
    body = bpy.data.objects["OperatorBody"]
    if body.get("steel_tide_viper_garment_revision") == 1:
        return
    positions, weights = read_skin(body)
    _paint_exposed_forearms(rig, body, positions, weights, _atlas_colors(body))
    distal = [group.index for group in body.vertex_groups
              if group.name in ("LeftForeArm", "RightForeArm", "LeftHand", "RightHand")]
    protected = weights[:, distal].sum(axis=1) > 0.5
    region = (positions[:, 2] > 0.74) & (positions[:, 2] < 1.57)
    weights = smooth(body, positions, weights, protected, region=region, iterations=144)
    write_skin(body, weights)
    glove = bpy.data.objects["AuthoredRightGlove"]
    positions, _weights = read_skin(glove)
    write_skin(glove, _glove_weights(rig, glove, positions))
    body["steel_tide_viper_garment_revision"] = 1


def refine_viper_collar(rig, read_skin, write_skin, smooth):
    """Blend the inspected back-left vest fold without repainting its boundary."""
    body = bpy.data.objects["OperatorBody"]
    if body.get("steel_tide_viper_collar_revision") == 1:
        return
    positions, weights = read_skin(body)
    local = np.asarray([tuple(rig.matrix_world.inverted() @ body.matrix_world @ vertex.co)
                        for vertex in body.data.vertices])
    region = np.linalg.norm(local - np.asarray((.033, .053, 1.280)), axis=1) < .085
    if region.sum() < 24:
        raise RuntimeError("Viper's inspected collar surface is missing")
    weights = smooth(body, positions, weights, ~region, region=region, iterations=96)
    write_skin(body, weights)
    if "steel_tide_audited_fold_edges" in body:
        del body["steel_tide_audited_fold_edges"]
    body["steel_tide_viper_collar_revision"] = 1
    print(f"VIPER_COLLAR_CHECK vertices={int(region.sum())} valid=true", flush=True)
