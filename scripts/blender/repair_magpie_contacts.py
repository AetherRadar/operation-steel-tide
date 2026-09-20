"""Restore Magpie's missing right forearm/glove from its authored left surface.

Selection and reflection use the rest skeleton, not presentation-world axes.
The source UVs, materials and upper-arm weights remain intact.
"""

import bpy
import numpy as np
from mathutils import Matrix, Vector

from repair_operator_skin import _read_skin, _write_skin


MAGPIE_COMPLETE_HAND_REVISION = 6


def _segment_distance(points, start, end):
    axis = end - start
    factors = np.clip((points - start) @ axis / (axis @ axis), 0, 1)
    return np.linalg.norm(points - start - factors[:, None] * axis, axis=1)


def _bone_segment(rig, name):
    bone = rig.data.bones[name]
    return (np.asarray(rig.matrix_world @ bone.head_local),
            np.asarray(rig.matrix_world @ bone.tail_local))


def _hand_surface_mask(mesh, rig, positions, side):
    """Separate the adjacent atlas-textured pouch from the authored glove."""
    wrist, _ = _bone_segment(rig, side + "Hand")
    elbow, _ = _bone_segment(rig, side + "ForeArm")
    scale = np.linalg.norm(wrist - elbow) / (.203856 if side == "Left" else .21898)
    lateral = np.asarray(rig.matrix_world.to_3x3() @ Vector((1 if side == "Left" else -1, 0, 0)))
    lateral /= np.linalg.norm(lateral)
    up = np.asarray(rig.matrix_world.to_3x3() @ Vector((0, 0, 1)))
    up /= np.linalg.norm(up)
    offset = (positions - wrist) @ lateral
    if side == "Right":
        return offset > -.052 * scale
    image = next(node.image for node in mesh.data.materials[0].node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image is not None)
    width, height = image.size
    pixels = np.asarray(image.pixels[:]).reshape(height, width, 4)
    colors = np.zeros((len(positions), 3))
    for loop in mesh.data.loops:
        u, v = mesh.data.uv_layers.active.data[loop.index].uv
        colors[loop.vertex_index] = pixels[min(height - 1, max(0, int(v * height))),
                                          min(width - 1, max(0, int(u * width))), :3]
    flesh = colors[:, 0] - colors[:, 1] > .065
    mask = ((offset > -.050 * scale) | (flesh & (offset > -.055 * scale))
            | ((positions - wrist) @ up > .015 * scale))
    # Atlas shadows cross the thumb, but each authored hand component must
    # keep one assignment. Stray pouch samples are tiny disconnected islands.
    groups = {group.index for group in mesh.vertex_groups if group.name.startswith(side + "Hand")}
    hand = np.asarray([sum(g.weight for g in vertex.groups if g.group in groups) > .5
                       for vertex in mesh.data.vertices])
    adjacency = [[] for _ in mesh.data.vertices]
    for edge in mesh.data.edges:
        first, second = edge.vertices
        if hand[first] and hand[second]:
            adjacency[first].append(second)
            adjacency[second].append(first)
    seen = set()
    for start in np.flatnonzero(hand):
        if int(start) in seen:
            continue
        pending = [int(start)]
        seen.add(int(start))
        component = []
        while pending:
            index = pending.pop()
            component.append(index)
            for neighbor in adjacency[index]:
                if neighbor not in seen:
                    seen.add(neighbor)
                    pending.append(neighbor)
        mask[component] = float(mask[component].mean()) > .5
    return mask


def _source_forearm_mask(mesh, rig, positions, weights):
    elbow, _ = _bone_segment(rig, "LeftForeArm")
    wrist, fingertips = _bone_segment(rig, "LeftHand")
    axis = wrist - elbow
    axis /= np.linalg.norm(axis)
    groups = [group.index for group in mesh.vertex_groups
              if group.name in ("LeftArm", "LeftForeArm")
              or group.name.startswith("LeftHand")]
    influence = weights[:, groups].sum(axis=1)
    forearm_distance = _segment_distance(positions, elbow, wrist)
    hand_distance = _segment_distance(positions, wrist, fingertips)
    return ((influence > .5) & ((positions - elbow) @ axis > -.015)
            & (np.minimum(forearm_distance, hand_distance) < .10)
            & _hand_surface_mask(mesh, rig, positions, "Left"))


def _mirror_arm_weights(mesh, patch, source_indices, weights):
    target = np.zeros((len(source_indices), len(patch.vertex_groups)))
    for group in mesh.vertex_groups:
        name = "Right" + group.name[4:] if group.name.startswith("Left") else group.name
        target[:, patch.vertex_groups[name].index] += weights[source_indices, group.index]
    _write_skin(patch, target)


def _repair_body_weights(mesh, rig, positions, weights):
    if mesh.get("steel_tide_magpie_anatomical_skin_revision") == 1:
        return weights
    for side in ("Left", "Right"):
        groups = [group.index for group in mesh.vertex_groups if group.name.startswith(side + "Hand")]
        wrist, tip = _bone_segment(rig, side + "Hand")
        remote = ((weights[:, groups].sum(axis=1) > 0)
                  & ~_hand_surface_mask(mesh, rig, positions, side))
        # Only the lower garment is repainted; sleeve wrist blends stay intact.
        remote &= ((positions - wrist) @ (tip - wrist) > 0)
        for index in np.flatnonzero(remote):
            weights[index, groups] = 0
            if weights[index].sum() < 1.e-6:
                weights[index, mesh.vertex_groups[side + "UpLeg"].index] = 1
            weights[index] /= weights[index].sum()
        print(f"MAGPIE_REMOTE_HAND_CHECK side={side} vertices={int(remote.sum())}", flush=True)
    torso = [mesh.vertex_groups[name].index for name in ("Spine", "Spine1", "Spine2")]
    arms = [mesh.vertex_groups[name].index for name in ("LeftArm", "RightArm", "LeftShoulder", "RightShoulder")]
    region = (weights[:, torso].sum(axis=1) > .01) & (weights[:, arms].sum(axis=1) > .01)
    # The UV-split shoulder folds have very short triangles beside long ones.
    # Limit adjacent paint jumps directly so tiny edges cannot dominate diffusion.
    edges = np.asarray([edge.vertices[:] for edge in mesh.data.edges])
    edges = edges[region[edges].any(axis=1)]
    for _ in range(96):
        first, second = edges.T
        delta = weights[second] - weights[first]
        distance = np.abs(delta).sum(axis=1)
        active = distance > .10
        if not active.any():
            break
        first, second = first[active], second[active]
        correction = delta[active] * ((distance[active] - .10) / distance[active] * .5)[:, None]
        change = np.zeros_like(weights)
        np.add.at(change, first, correction)
        np.add.at(change, second, -correction)
        degree = np.bincount(np.concatenate((first, second)), minlength=len(weights))
        affected = degree > 0
        weights[affected] += change[affected] / degree[affected, None]
    # Keep Magpie's inspected shoulder palette compact before evaluating poses.
    discard = np.argsort(weights, axis=1)[:, :-4]
    np.put_along_axis(weights, discard, 0, axis=1)
    weights /= weights.sum(axis=1)[:, None]
    _write_skin(mesh, weights)
    if "steel_tide_audited_fold_edges" in mesh:
        del mesh["steel_tide_audited_fold_edges"]
    mesh["steel_tide_magpie_anatomical_skin_revision"] = 1
    return weights


def repair_magpie_contacts(rig):
    mesh = bpy.data.objects["OperatorBody"]
    patch = bpy.data.objects.get("MagpieRightHandPatch")
    if (mesh.get("steel_tide_magpie_complete_hand_revision") == MAGPIE_COMPLETE_HAND_REVISION
            and patch is not None
            and patch.get("steel_tide_magpie_complete_hand_revision") == MAGPIE_COMPLETE_HAND_REVISION):
        return {"already_authored": True}
    positions, weights = _read_skin(mesh)
    weights = _repair_body_weights(mesh, rig, positions, weights)
    selected = _source_forearm_mask(mesh, rig, positions, weights)
    polygons = [face for face in mesh.data.polygons
                if all(selected[index] for index in face.vertices)]
    if not 800 <= len(polygons) <= 5000:
        raise RuntimeError(f"Unexpected authored Magpie forearm/glove selection: {len(polygons)} faces")

    # Reflect across the skeleton's sagittal plane, then align its unequal arms.
    left_wrist = rig.matrix_world @ rig.data.bones["LeftHand"].head_local
    right_wrist = rig.matrix_world @ rig.data.bones["RightHand"].head_local
    left_elbow = rig.matrix_world @ rig.data.bones["LeftForeArm"].head_local
    right_elbow = rig.matrix_world @ rig.data.bones["RightForeArm"].head_local
    lateral = (rig.matrix_world @ rig.data.bones["LeftArm"].head_local
               - rig.matrix_world @ rig.data.bones["RightArm"].head_local).normalized()
    reflection = Matrix.Identity(3)
    for row in range(3):
        for column in range(3):
            reflection[row][column] -= 2 * lateral[row] * lateral[column]
    rotation = (reflection @ (left_elbow - left_wrist)).rotation_difference(right_elbow - right_wrist)
    transform = (Matrix.Translation(right_wrist) @ rotation.to_matrix().to_4x4()
                 @ reflection.to_4x4() @ Matrix.Translation(-left_wrist))
    source_indices = sorted({index for face in polygons for index in face.vertices})
    remap = {index: target for target, index in enumerate(source_indices)}
    coordinates = [transform @ Vector(positions[index]) for index in source_indices]
    axis = (right_elbow - right_wrist).normalized()
    ratio = (right_elbow - right_wrist).length / (left_elbow - left_wrist).length
    coordinates = [point + axis * max(0, (point - right_wrist).dot(axis)) * (ratio - 1)
                   for point in coordinates]
    faces = [[remap[index] for index in reversed(face.vertices)] for face in polygons]
    if patch is not None:
        bpy.data.objects.remove(patch, do_unlink=True)
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
    _mirror_arm_weights(mesh, patch, source_indices, weights)
    modifier = patch.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    modifier.use_deform_preserve_volume = False
    patch["steel_tide_magpie_complete_hand_revision"] = MAGPIE_COMPLETE_HAND_REVISION
    patch["steel_tide_skin_repair_revision"] = 4
    mesh["steel_tide_magpie_complete_hand_revision"] = MAGPIE_COMPLETE_HAND_REVISION
    result = {"faces": len(faces), "vertices": len(coordinates)}
    print("MAGPIE_COMPLETE_HAND_CHECK", result, flush=True)
    return result
