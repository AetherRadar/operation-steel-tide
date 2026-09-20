"""Repair the approved HY-3D garment and Lynx hair skin in Blender.

The HY-3D body is split at thousands of UV seams. Its original nearest-bone
weights jump between adjacent sleeves/vest vertices and include finger
influences on thigh pouches. This pass edits weights in the source .blend;
it does not change runtime geometry or compensate for an animation pose.

Call ``repair_operator_skin(role, rig, meshes)`` before authoring actions,
then ``author_hair_actions(rig, actions)`` once the final actions exist.
The caller owns the .blend save and the single final GLB export.
"""

from __future__ import annotations

import math
import importlib.util
from pathlib import Path
import sys
from collections.abc import Iterable

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree


REVISION = 4
MAX_INFLUENCES = 8
HAIR_BONES = ("HairRoot", "HairMid", "HairTip")


def _read_skin(mesh):
    positions = np.empty((len(mesh.data.vertices), 3), dtype=np.float64)
    weights = np.zeros((len(mesh.data.vertices), len(mesh.vertex_groups)))
    for vertex in mesh.data.vertices:
        positions[vertex.index] = mesh.matrix_world @ vertex.co
        for assignment in vertex.groups:
            weights[vertex.index, assignment.group] = assignment.weight
    totals = weights.sum(axis=1)
    if np.any(totals <= 1.0e-8):
        raise RuntimeError(f"{mesh.name}: unbound source vertices")
    weights /= totals[:, None]
    return positions, weights


def _write_skin(mesh, weights):
    if not np.isfinite(weights).all() or np.any(weights.sum(axis=1) <= 1.0e-8):
        raise RuntimeError(f"{mesh.name}: non-finite or empty authored skin weights")
    for vertex in mesh.data.vertices:
        indices = np.argsort(weights[vertex.index])[-MAX_INFLUENCES:]
        values = weights[vertex.index, indices]
        values /= values.sum()
        # VertexGroupElement objects are live RNA views. Snapshot integers
        # before removal; mutating the collection invalidates element views.
        for group_index in [assignment.group for assignment in vertex.groups]:
            mesh.vertex_groups[group_index].remove([vertex.index])
        for index, value in zip(indices, values):
            if value > 1.0e-7:
                mesh.vertex_groups[int(index)].add([vertex.index], float(value), "REPLACE")
    assert_skin_contract(mesh)


def _collapse_finger_weights(mesh, weights):
    """Preserve authored glove shape with a stable wrist influence family.

    These source rigs have synthetic distal joints outside the actual finger
    surfaces. Their final action channels are identities. Combining their
    weights onto Hand preserves the same shape and prevents four almost
    identical finger influences displacing a necessary forearm/garment weight.
    """
    for side in ("Left", "Right"):
        target = mesh.vertex_groups.get(side + "Hand")
        groups = [group.index for group in mesh.vertex_groups if group.name.startswith(side + "Hand")]
        if target is None or not groups:
            continue
        total = weights[:, groups].sum(axis=1)
        weights[:, groups] = 0.0
        weights[:, target.index] = total
    return weights


def _clear_remote_hand_weights(mesh, positions, weights, allow_hand_only=False):
    """Remove stray hand weights from the lower-body surface after hair split."""
    hand_groups = [group.index for group in mesh.vertex_groups
                   if group.name in ("LeftHand", "RightHand")]
    if not hand_groups:
        return 0
    lower_body = positions[:, 2] < 0.90
    lower_body_bones = [group.index for group in mesh.vertex_groups
                        if group.name in ("Hips", "LeftUpLeg", "RightUpLeg",
                                          "LeftLeg", "RightLeg", "LeftFoot", "RightFoot")]
    if not lower_body_bones:
        return 0
    remote = lower_body & (weights[:, hand_groups].sum(axis=1) > 1.0e-5)
    remote &= ((weights[:, lower_body_bones].sum(axis=1) > 1.0e-5)
               | (allow_hand_only & (np.abs(positions[:, 0]) > 0.34)))
    if not remote.any():
        return 0
    weights[np.ix_(remote, hand_groups)] = 0.0
    for vertex in np.flatnonzero(remote):
        total = weights[vertex].sum()
        if total <= 1.0e-8:
            if not allow_hand_only:
                raise RuntimeError(f"{mesh.name}: clearing remote hand weights unbound vertex {vertex}")
            fallback = mesh.vertex_groups.get(
                "LeftUpLeg" if positions[vertex, 0] >= 0.0 else "RightUpLeg")
            if fallback is None:
                raise RuntimeError(f"{mesh.name}: no lower-body fallback for vertex {vertex}")
            weights[vertex, fallback.index] = 1.0
            total = 1.0
        weights[vertex] /= total
    return int(remote.sum())


def assert_skin_contract(mesh):
    for vertex in mesh.data.vertices:
        values = [assignment.weight for assignment in vertex.groups if assignment.weight > 1.0e-7]
        if len(values) > MAX_INFLUENCES or abs(sum(values) - 1.0) > 1.0e-5:
            raise RuntimeError(f"{mesh.name}: invalid exported skin at vertex {vertex.index}: "
                               f"influences={len(values)} total={sum(values):.8f}")


def _lynx_hair_mask(mesh, positions):
    """Select the authored brown rear locks, preserving headset and backpack.

    The mask combines their inspected rear-volume bounds and original atlas
    color. The small surface-neighbor closing step includes texture highlights
    without leaking through the gap to green backpack or white sleeve skin.
    """
    material = mesh.data.materials[0]
    image = next(node.image for node in material.node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image is not None)
    width, height = image.size
    pixels = np.array(image.pixels[:], dtype=np.float32).reshape((height, width, 4))
    colors = np.zeros((len(positions), 3))
    uv = mesh.data.uv_layers.active.data
    for loop in mesh.data.loops:
        u, v = uv[loop.index].uv
        colors[loop.vertex_index] = pixels[
            min(height - 1, max(0, int(v * height))),
            min(width - 1, max(0, int(u * width))), :3]
    rear_limit = 0.012 + 0.065 * np.clip((1.38 - positions[:, 2]) / 0.35, 0.0, 1.0)
    volume = (positions[:, 2] > 1.025) & (positions[:, 2] < 1.70) & (positions[:, 1] > rear_limit)
    brown = (colors[:, 0] > colors[:, 1] + 0.005) & (colors[:, 0] > colors[:, 2] + 0.002)
    brown &= (colors.max(axis=1) < 0.30) & (colors.min(axis=1) > 0.022)
    seeds = volume & brown
    tree = KDTree(len(positions))
    for index, point in enumerate(positions):
        tree.insert(point, index)
    tree.balance()
    mask = seeds.copy()
    for index in np.flatnonzero(volume):
        neighbors = [neighbor for _, neighbor, _ in tree.find_range(positions[index], 0.009)]
        if neighbors:
            mask[index] = np.mean(seeds[neighbors]) >= 0.45
    count = int(mask.sum())
    if not 10000 <= count <= 23000:
        raise RuntimeError(f"Lynx hair surface selection changed: {count} vertices")
    return mask


def _smooth_garments(mesh, positions, weights, protected, region=None, iterations=24):
    """Diffuse weights along authored surfaces, never across nearby garments.

    Vertices split only by UV coordinates share one paint sample. All other
    diffusion follows actual mesh edges: a sleeve beside the vest cannot pick
    up torso weights just because the two surfaces are close in the bind pose.
    """
    cells, inverse = np.unique(np.round(positions, 5), axis=0, return_inverse=True)
    counts = np.bincount(inverse)
    sampled = np.zeros((len(cells), weights.shape[1]))
    np.add.at(sampled, inverse, weights)
    sampled /= counts[:, None]
    locked = np.zeros(len(cells), dtype=bool)
    np.maximum.at(locked, inverse, protected)
    edges = np.array([edge.vertices[:] for edge in mesh.data.edges])
    edges = np.unique(np.sort(inverse[edges], axis=1), axis=0)
    edges = edges[edges[:, 0] != edges[:, 1]]
    left, right = edges.T
    factors = 1.0 / np.maximum(np.linalg.norm(cells[left] - cells[right], axis=1), 0.004)
    degree = np.bincount(left, weights=factors, minlength=len(cells))
    degree += np.bincount(right, weights=factors, minlength=len(cells))
    # Fixed palm/garment values remain boundary samples for their neighbors.
    # Removing incident edges instead creates an artificial weight jump.
    active = (degree > 0.0) & ~locked
    if region is not None:
        selected = np.zeros(len(cells), dtype=bool)
        np.maximum.at(selected, inverse, region)
        active &= selected
    for _ in range(iterations):
        accumulated = np.zeros_like(sampled)
        np.add.at(accumulated, left, sampled[right] * factors[:, None])
        np.add.at(accumulated, right, sampled[left] * factors[:, None])
        sampled[active] = 0.28 * sampled[active] + 0.72 * accumulated[active] / degree[active, None]
    output = sampled[inverse]
    output[protected] = weights[protected]
    return output


def _bind_lynx_hair(rig, mesh, positions, mask):
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        head = rig.data.edit_bones["Head"]
        inverse = rig.matrix_world.inverted()
        points = [(0.008, 0.066, 1.555), (0.008, 0.151, 1.425),
                  (0.008, 0.223, 1.310), (0.008, 0.263, 1.190)]
        for index, name in enumerate(HAIR_BONES):
            bone = rig.data.edit_bones.get(name) or rig.data.edit_bones.new(name)
            bone.parent = head if index == 0 else rig.data.edit_bones[HAIR_BONES[index - 1]]
            bone.head = inverse @ Vector(points[index])
            bone.tail = inverse @ Vector(points[index + 1])
            bone.use_connect = index > 0
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")
    groups = {name: mesh.vertex_groups.get(name) or mesh.vertex_groups.new(name=name)
              for name in ("Head",) + HAIR_BONES}
    # Blend the original locks through anatomical arc stations. The scalp
    # remains on Head and the ends follow the hair chain instead of shoulders.
    stations = ((1.555, "Head"), (1.465, "HairRoot"), (1.340, "HairMid"), (1.205, "HairTip"))
    for index in np.flatnonzero(mask):
        vertex = mesh.data.vertices[int(index)]
        for group_index in [assignment.group for assignment in vertex.groups]:
            mesh.vertex_groups[group_index].remove([int(index)])
        height = positions[index, 2]
        if height >= stations[0][0]:
            groups["Head"].add([int(index)], 1.0, "REPLACE")
        elif height <= stations[-1][0]:
            groups["HairTip"].add([int(index)], 1.0, "REPLACE")
        else:
            for (upper, upper_name), (lower, lower_name) in zip(stations, stations[1:]):
                if lower <= height <= upper:
                    fraction = (height - lower) / (upper - lower)
                    groups[upper_name].add([int(index)], float(fraction), "REPLACE")
                    groups[lower_name].add([int(index)], float(1.0 - fraction), "REPLACE")
                    break
    mesh["steel_tide_hair_vertices"] = int(mask.sum())
    rig["steel_tide_authored_hair_chain"] = True
    assert_skin_contract(mesh)


def _load_repair(name):
    path = Path(__file__).with_name(name + ".py")
    specification = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(specification)
    sys.modules[name] = module
    specification.loader.exec_module(module)
    return module


def _operator_meshes(rig):
    return [mesh for mesh in bpy.context.scene.objects if mesh.type == "MESH" and mesh.vertex_groups
            and any(modifier.type == "ARMATURE" and modifier.object == rig for modifier in mesh.modifiers)]


def _repair_lynx_hair(rig, mesh, positions, weights):
    hair = bpy.data.objects.get("LynxHairLocks")
    if hair is None:
        mask = _lynx_hair_mask(mesh, positions)
        _write_skin(mesh, weights)
        hair = _load_repair("sculpt_lynx_hair").separate_hair(mesh, mask)
    hair_positions, _weights = _read_skin(hair)
    _bind_lynx_hair(rig, hair, hair_positions, np.ones(len(hair_positions), dtype=bool))
    hair["steel_tide_skin_repair_revision"] = REVISION
    backpack = bpy.data.objects.get("LynxAuthoredBackpack")
    if backpack is not None:
        assert_skin_contract(backpack)
        backpack["steel_tide_skin_repair_revision"] = REVISION
    return len(hair_positions)


def _repair_surface(role, rig, mesh):
    positions, weights = _read_skin(mesh)
    weights = _collapse_finger_weights(mesh, weights)
    hair_vertices = 0
    if role == "lynx" and mesh.name == "OperatorBody":
        hair_vertices = _repair_lynx_hair(rig, mesh, positions, weights)
        positions, weights = _read_skin(mesh)
        remote_hand_vertices = _clear_remote_hand_weights(mesh, positions, weights)
    else:
        remote_hand_vertices = 0
    if role == "viper" and mesh.name == "OperatorBody":
        glove = _load_repair("repair_viper_contact").repair_viper_contact(
            rig, mesh, positions, weights, _read_skin, _write_skin)
        glove["steel_tide_skin_repair_revision"] = REVISION
        positions, weights = _read_skin(mesh)
    protected = np.zeros(len(positions), dtype=bool)
    if role == "magpie":
        groups = [group.index for group in mesh.vertex_groups
                  if group.name in ("LeftHand", "RightHand", "LeftForeArm", "RightForeArm")]
        protected = weights[:, groups].sum(axis=1) > 0.8
    if mesh.name != "MagpieRightHandPatch":
        for _ in range(2 if role == "viper" else 1):
            weights = _smooth_garments(mesh, positions, weights, protected)
    if role == "lynx" and mesh.name == "OperatorBody":
        remote_hand_vertices += _clear_remote_hand_weights(mesh, positions, weights)
    _write_skin(mesh, weights)
    if role == "lynx" and mesh.name == "OperatorBody":
        mesh["steel_tide_removed_remote_hand_vertices"] = remote_hand_vertices
    if role == "viper" and mesh.name == "OperatorBody":
        mesh["steel_tide_audited_fold_edges"] = [
            [20666, 20667], [20680, 20851], [39734, 41160], [47974, 47976]
        ]
    mesh["steel_tide_skin_repair_revision"] = REVISION
    return hair_vertices


def repair_operator_skin(role: str, rig, meshes: Iterable) -> dict:
    """Correct pristine authored surfaces once, preserving separate garments."""
    meshes = _operator_meshes(rig)
    heron_upgrade = role == "heron" and all(
        mesh.get("steel_tide_skin_repair_revision") in (2, 3, REVISION)
        for mesh in meshes)
    magpie_upgrade = role == "magpie" and all(
        mesh.get("steel_tide_skin_repair_revision") in (2, 3, REVISION)
        for mesh in meshes)
    for mesh in meshes:
        previous = mesh.get("steel_tide_skin_repair_revision")
        if previous is not None and previous != REVISION and not heron_upgrade and not magpie_upgrade:
            raise RuntimeError(f"{mesh.name}: rebuild revision {previous} from the pristine source")
        for modifier in mesh.modifiers:
            if modifier.type == "ARMATURE":
                modifier.use_deform_preserve_volume = False
    stats = {"role": role, "meshes": 0, "hair_vertices": 0}
    current = all(mesh.get("steel_tide_skin_repair_revision") == REVISION for mesh in meshes)
    complete_seams = role != "jackal" or all(
        mesh.get("steel_tide_jackal_forearm_seams_revision") == 1 for mesh in meshes)
    heron_refined = role != "heron" or all(
        mesh.get("steel_tide_heron_garment_transition_revision") == 1
        for mesh in meshes if mesh.name == "OperatorBody")
    if current and complete_seams and heron_refined:
        return stats
    if heron_upgrade or magpie_upgrade:
        stats["meshes"] = 0
    elif role == "jackal":
        helper = _load_repair("repair_jackal_skin")
        for mesh in meshes:
            helper.repair_jackal_skin(rig, mesh, _read_skin, _write_skin)
            mesh["steel_tide_skin_repair_revision"] = REVISION
            stats["meshes"] += 1
    else:
        if role == "magpie":
            _load_repair("repair_magpie_contacts").repair_magpie_contacts(rig)
            meshes = _operator_meshes(rig)
        for mesh in meshes:
            if mesh.get("steel_tide_skin_repair_revision") == REVISION:
                continue
            stats["hair_vertices"] += _repair_surface(role, rig, mesh)
            stats["meshes"] += 1
    if role == "heron" and not heron_refined:
        helper = _load_repair("repair_heron_skin")
        helper.refine_heron_skin(rig, meshes)
    if magpie_upgrade:
        for mesh in meshes:
            if mesh.name != "OperatorBody":
                continue
            positions, weights = _read_skin(mesh)
            weights = _collapse_finger_weights(mesh, weights)
            protected = np.zeros(len(positions), dtype=bool)
            hand_groups = [group.index for group in mesh.vertex_groups
                           if group.name in ("LeftHand", "RightHand", "LeftForeArm", "RightForeArm")]
            protected = weights[:, hand_groups].sum(axis=1) > 0.8
            weights = _smooth_garments(mesh, positions, weights, protected, iterations=168)
            removed = _clear_remote_hand_weights(mesh, positions, weights, allow_hand_only=True)
            _write_skin(mesh, weights)
            mesh["steel_tide_removed_remote_hand_vertices"] = removed
            mesh["steel_tide_audited_fold_edges"] = [[42242, 42247], [35917, 35918]]
    for mesh in _operator_meshes(rig):
        assert_skin_contract(mesh)
    print("OPERATOR_SKIN_REPAIR_CHECK", " ".join(f"{key}={value}" for key, value in stats.items()))
    return stats


def author_hair_actions(rig, actions: Iterable) -> None:
    if not rig.get("steel_tide_authored_hair_chain"):
        return
    locomotion = {"walk", "run", "sprint", "ready_walk", "ready_run", "ready_sprint",
                  "aim_walk", "aim_run", "aim_sprint", "crouch_walk", "ready_crouch_walk", "aim_crouch_walk"}
    for action in actions:
        for curve in list(action.fcurves):
            if any(f'pose.bones["{name}"]' in curve.data_path for name in HAIR_BONES):
                action.fcurves.remove(curve)
        start, end = action.frame_range
        family = action.name
        for prefix in ("pistol_", "rifle_"):
            if family.startswith(prefix):
                family = family[len(prefix):]
        animated = family in locomotion
        for bone_index, name in enumerate(HAIR_BONES):
            bone = rig.pose.bones[name]
            bone.rotation_mode = "QUATERNION"
            amplitude = math.radians((0.7, 1.5, 2.4)[bone_index]) if animated else 0.0
            values = []
            frames = sorted({float(start), float(end), *[float(frame) for frame in range(math.ceil(start), math.floor(end) + 1)]})
            for frame in frames:
                phase = (frame - start) / max(1.0, end - start) * math.tau
                rotation = Quaternion((1.0, 0.0, 0.0), amplitude * math.sin(phase - bone_index * 0.45))
                values.append((frame, rotation))
            if animated:
                values[-1] = (float(end), values[0][1].copy())
            for component in range(4):
                curve = action.fcurves.new(f'pose.bones["{name}"].rotation_quaternion', index=component, action_group=name)
                for frame, rotation in values:
                    key = curve.keyframe_points.insert(frame, rotation[component], options={"FAST"})
                    key.interpolation = "LINEAR"
                curve.update()
        action["steel_tide_authored_hair_motion"] = True
