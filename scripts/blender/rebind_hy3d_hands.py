"""Rebind HY-3D finger surfaces to corrected finger chains.

The Tencent HY-3D exports used by Steel Tide contain finger bones, but the
finger joints can be placed in a different frame from the authored hand
surface and the exported finger weights are often tiny, overlapping values.
This Blender/DCC step derives a proximal-to-distal chain from the existing
finger-labelled surface samples, moves the finger bones onto that chain, and
normalizes the nearest finger weights.  It is intentionally a separate step
from carry-action repair so a damaged source is rejected instead of silently
producing an incomplete hand.

Usage::

    blender -b --python rebind_hy3d_hands.py -- \
        --input operator.clean.glb --output operator.hands.glb

The output is the hand-rebound source.  Run the carry-action repair as a
second, separate export so the source GLB is never overwritten::

    blender -b --python repair_hy3d_operator_carry_actions.py -- \
        operator.hands.glb operator.carried.glb

The second pass replaces only carry finger rotations and upper-body carry
tracks; it preserves the location/scale channels that the glTF importer needs
for the child finger translations.  Keep both outputs in a private asset
directory and point the Godot test project at ``operator.carried.glb``.
"""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Iterable

import bpy
from mathutils import Vector


FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")
SIDES = ("Left", "Right")
SEGMENTS = (1, 2, 3)
MINIMUM_HAND_SAMPLES = 20
MINIMUM_HAND_WEIGHT = 100.0
MINIMUM_FINGER_SAMPLES = 8


def _matches_canonical_name(name: str, canonical: str) -> bool:
    """Match Blender/glTF names with an optional importer namespace prefix."""

    if name == canonical:
        return True
    return any(name.endswith(separator + canonical) for separator in (":", "/", "|"))


def find_vertex_group(mesh: bpy.types.Object, canonical: str) -> bpy.types.VertexGroup | None:
    """Resolve a skin group without losing namespaced glTF/FBX groups."""

    exact = mesh.vertex_groups.get(canonical)
    if exact is not None:
        return exact
    candidates = [
        group
        for group in mesh.vertex_groups
        if _matches_canonical_name(group.name, canonical)
    ]
    if not candidates:
        return None
    # Prefer the shortest suffix match when an exporter emitted duplicate
    # namespace aliases.  Exact names were handled above.
    return min(candidates, key=lambda group: (len(group.name), group.name))


def find_edit_bone(armature: bpy.types.Object, canonical: str) -> bpy.types.EditBone | None:
    """Resolve an edit bone by canonical name or an importer namespace suffix."""

    exact = armature.data.edit_bones.get(canonical)
    if exact is not None:
        return exact
    candidates = [
        bone
        for bone in armature.data.edit_bones
        if _matches_canonical_name(bone.name, canonical)
    ]
    return min(candidates, key=lambda bone: (len(bone.name), bone.name)) if candidates else None


def is_helper_mesh(obj: bpy.types.Object, keep: Iterable[bpy.types.Object] = ()) -> bool:
    """Identify importer preview primitives without touching authored meshes."""

    if obj.type != "MESH" or obj in set(keep):
        return False
    lowered = obj.name.casefold()
    if lowered == "cube" or lowered.startswith("cube."):
        return len(obj.data.polygons) <= 128
    if lowered == "icosphere" or lowered.startswith("icosphere."):
        return True
    return False


def remove_helper_meshes(keep: Iterable[bpy.types.Object] = ()) -> list[str]:
    """Remove only Cube/Icosphere preview meshes and preserve node objects."""

    keep_set = set(keep)
    removed: list[str] = []
    for obj in list(bpy.data.objects):
        if is_helper_mesh(obj, keep_set):
            removed.append(obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
    return removed


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument(
        "--radius",
        type=float,
        default=0.045,
        help="Maximum mesh-to-finger-chain distance in metres (default: 0.045).",
    )
    parser.add_argument(
        "--finger-share",
        type=float,
        default=0.85,
        help="Weight share assigned to the nearest finger segments (default: 0.85).",
    )
    values = parser.parse_args(argv)
    if not 0.0 < values.finger_share < 1.0:
        parser.error("--finger-share must be between 0 and 1")
    if values.radius <= 0.0:
        parser.error("--radius must be positive")
    return values


def find_armature() -> bpy.types.Object:
    armature = next(
        (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"), None
    )
    if armature is None:
        raise RuntimeError("input GLB has no armature")
    return armature


def find_visual_mesh() -> bpy.types.Object:
    meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and len(obj.data.polygons) > 100
    ]
    if not meshes:
        raise RuntimeError("input GLB has no visual mesh")
    return max(meshes, key=lambda obj: len(obj.data.polygons))


def group_weight(
    mesh: bpy.types.Object,
    vertex: bpy.types.MeshVertex,
    group_or_name: bpy.types.VertexGroup | str | None,
) -> float:
    group = (
        find_vertex_group(mesh, group_or_name)
        if isinstance(group_or_name, str)
        else group_or_name
    )
    if group is None:
        return 0.0
    return next(
        (assignment.weight for assignment in vertex.groups if assignment.group == group.index),
        0.0,
    )


def canonical_finger_name(name: str, side: str) -> tuple[str, int] | None:
    for finger in FINGERS:
        for segment in SEGMENTS:
            canonical = f"{side}Hand{finger}{segment}"
            if _matches_canonical_name(name, canonical):
                return finger, segment
    return None


def dominant_finger_weights(
    mesh: bpy.types.Object, vertex: bpy.types.MeshVertex, side: str
) -> list[tuple[float, str]]:
    values = []
    for assignment in vertex.groups:
        name = mesh.vertex_groups[assignment.group].name
        if canonical_finger_name(name, side) is not None:
            values.append((assignment.weight, name))
    return values


def _group_samples(
    mesh: bpy.types.Object,
    group: bpy.types.VertexGroup | None,
    threshold: float = 0.0,
) -> tuple[int, float]:
    if group is None:
        return 0, 0.0
    weights = [
        assignment.weight
        for vertex in mesh.data.vertices
        for assignment in vertex.groups
        if assignment.group == group.index and assignment.weight > threshold
    ]
    return len(weights), sum(weights)


def validate_hand_completeness(
    mesh: bpy.types.Object,
    minimum_points: int = MINIMUM_FINGER_SAMPLES,
) -> dict[tuple[str, str, int], bpy.types.VertexGroup]:
    """Reject incomplete hand skin before any bones or weights are changed.

    ``magpie.clean.glb`` and its hand-patch experiments retain a palm group,
    but the right distal finger groups contain one-to-four stray vertices.
    Rebinding those groups would manufacture a plausible-looking but unusable
    hand.  Require every labelled segment to have a real sample population;
    the hand-group total catches the one-vertex dummy groups in ``magpie.glb``.
    """

    groups: dict[tuple[str, str, int], bpy.types.VertexGroup] = {}
    issues: list[str] = []
    for side in SIDES:
        hand_group = find_vertex_group(mesh, f"{side}Hand")
        hand_samples, hand_weight = _group_samples(mesh, hand_group, 0.05)
        if hand_group is None:
            issues.append(f"{side}Hand(missing)")
        elif hand_samples < MINIMUM_HAND_SAMPLES or hand_weight < MINIMUM_HAND_WEIGHT:
            issues.append(
                f"{side}Hand(samples={hand_samples},weight={hand_weight:.3f})"
            )
        for finger in FINGERS:
            for segment in SEGMENTS:
                canonical = f"{side}Hand{finger}{segment}"
                group = find_vertex_group(mesh, canonical)
                count, total = _group_samples(mesh, group)
                if group is None:
                    issues.append(f"{canonical}(missing)")
                    continue
                groups[(side, finger, segment)] = group
                if count < minimum_points:
                    issues.append(f"{canonical}(samples={count},weight={total:.3f})")
    if issues:
        raise RuntimeError(
            "damaged/missing hand skin; refusing HY-3D rebind (Magpie requires "
            "a different source): " + ", ".join(sorted(set(issues)))
        )
    return groups


def surface_centroids(
    mesh: bpy.types.Object,
    side: str,
    finger: str,
    groups: dict[tuple[str, str, int], bpy.types.VertexGroup] | None = None,
) -> list[Vector | None]:
    """Return weighted centroids for the three labelled finger segments.

    A vertex is used for one segment only when that segment is tied for the
    largest finger-labelled weight on the vertex.  This filters the low-value
    cross-finger assignments found in HY-3D exports while retaining the
    authored topology labels.
    """

    centroids: list[Vector | None] = []
    for segment in SEGMENTS:
        name = f"{side}Hand{finger}{segment}"
        group = (
            groups.get((side, finger, segment))
            if groups is not None
            else find_vertex_group(mesh, name)
        )
        points: list[Vector] = []
        weights: list[float] = []
        fallback_points: list[Vector] = []
        fallback_weights: list[float] = []
        for vertex in mesh.data.vertices:
            weight = group_weight(mesh, vertex, group)
            if weight <= 0.0:
                continue
            fallback_points.append(mesh.matrix_world @ vertex.co)
            fallback_weights.append(weight)
            finger_weights = dominant_finger_weights(mesh, vertex, side)
            if not finger_weights or weight < max(value for value, _ in finger_weights) - 1.0e-6:
                continue
            points.append(mesh.matrix_world @ vertex.co)
            weights.append(weight)
        # Some exports put a segment's low weights under a neighbouring
        # finger label, leaving no strict dominant samples even though the
        # segment group still contains a usable surface.  Use that group as a
        # fallback; collect_chains() still rejects genuinely tiny/damaged
        # groups such as Magpie's missing right hand.
        if not points:
            points, weights = fallback_points, fallback_weights
        total = sum(weights)
        centroids.append(
            sum((point * weight for point, weight in zip(points, weights)), Vector()) / total
            if total > 1.0e-5
            else None
        )
    return centroids


def collect_chains(
    mesh: bpy.types.Object,
    minimum_points: int = MINIMUM_FINGER_SAMPLES,
) -> dict[tuple[str, str], list[Vector]]:
    """Collect reliable chains and reject partial/damaged hand sources."""

    groups = validate_hand_completeness(mesh, minimum_points)
    chains: dict[tuple[str, str], list[Vector]] = {}
    for side in SIDES:
        for finger in FINGERS:
            values = surface_centroids(mesh, side, finger, groups)
            # validate_hand_completeness() guarantees samples, but retain this
            # guard for malformed Blender datablocks with zero total weight.
            if any(value is None for value in values):
                raise RuntimeError(f"damaged/missing hand skin: {side}Hand{finger}")
            chains[(side, finger)] = [value for value in values if value is not None]
    return chains


def reposition_finger_bones(
    armature: bpy.types.Object,
    chains: dict[tuple[str, str], list[Vector]],
) -> None:
    """Always move the existing finger joints onto the mesh-derived chain.

    HY-3D imports may namespace bone names (for example ``mixamorig:...``),
    so an exact lookup can silently leave the old, misplaced joints in place.
    This pass never creates substitute finger bones: a missing chain is a
    source error and must be reported before export.
    """

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        world_to_armature = armature.matrix_world.inverted()
        for (side, finger), points in chains.items():
            bones = [
                find_edit_bone(armature, f"{side}Hand{finger}{segment}")
                for segment in SEGMENTS
            ]
            if any(bone is None for bone in bones):
                raise RuntimeError(
                    f"damaged/missing hand rig; armature is missing "
                    f"{side}Hand{finger} bones"
                )
            # glTF stores joints, not Blender bone tails.  Disconnect before
            # moving heads so Blender does not move the palm tail in edit mode.
            for bone in bones:
                bone.use_connect = False
            for index, bone in enumerate(bones):
                bone.head = world_to_armature @ points[index]
                desired_tail = world_to_armature @ (
                    points[index + 1]
                    if index < len(points) - 1
                    else points[-1] + (points[-1] - points[-2])
                )
                if (desired_tail - bone.head).length_squared < 1.0e-8:
                    fallback = bone.vector
                    if fallback.length_squared < 1.0e-8:
                        fallback = Vector((0.0, 0.01, 0.0))
                    desired_tail = bone.head + fallback.normalized() * 0.01
                bone.tail = desired_tail
                bone.use_connect = False
        armature["steel_tide_finger_bones_repositioned"] = True
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")


def distance_to_segment(point: Vector, start: Vector, end: Vector) -> float:
    direction = end - start
    length_squared = direction.length_squared
    travel = (
        max(0.0, min(1.0, (point - start).dot(direction) / length_squared))
        if length_squared > 1.0e-10
        else 0.0
    )
    return (point - (start + direction * travel)).length


def build_segments(
    armature: bpy.types.Object,
    mesh: bpy.types.Object,
    chains: dict[tuple[str, str], list[Vector]],
) -> list[tuple[str, str, int, Vector, Vector]]:
    inverse = armature.matrix_world.inverted() @ mesh.matrix_world
    segments = []
    for (side, finger), points in chains.items():
        local_points = [inverse @ point for point in points]
        for index in range(3):
            end = (
                local_points[index + 1]
                if index < 2
                else local_points[-1] + (local_points[-1] - local_points[-2])
            )
            segments.append((side, finger, index + 1, local_points[index], end))
    return segments


def rebind_mesh(
    armature: bpy.types.Object,
    mesh: bpy.types.Object,
    segments: Iterable[tuple[str, str, int, Vector, Vector]],
    radius: float,
    finger_share: float,
    stats: dict[str, int] | None = None,
) -> int:
    """Remove stray distal assignments and rebind each hand to nearest chains.

    The old pass left a vertex's original finger weights untouched whenever its
    nearest segment was just outside ``radius``.  That preserved the exact
    failure seen in the HY-3D exports: a distal phalanx retained a tiny weight
    on a remote vertex and the wrist/hand appeared to twist around it.  Clear
    all same-side finger groups first; nearby vertices then receive a smooth
    two-segment assignment and remote vertices retain only their non-finger
    influences.
    """

    inverse = armature.matrix_world.inverted() @ mesh.matrix_world
    segments = list(segments)
    resolved_groups = {
        side: {
            (finger, segment): find_vertex_group(
                mesh, f"{side}Hand{finger}{segment}"
            )
            for finger in FINGERS
            for segment in SEGMENTS
        }
        for side in SIDES
    }
    group_indices = {
        side: {
            group.index
            for group in resolved_groups[side].values()
            if group is not None
        }
        for side in SIDES
    }
    # Finger skin is thicker than the chain centreline.  A modest cleanup band
    # catches remote distal weights without deleting legitimate palm-side
    # vertices that fall just outside the authored radius.
    cleanup_radius = max(radius * 2.0, 0.075)
    changed = 0
    cleared = 0
    for vertex in mesh.data.vertices:
        local_point = inverse @ vertex.co
        for side in SIDES:
            if not any(
                assignment.group in group_indices[side] and assignment.weight > 0.0
                for assignment in vertex.groups
            ):
                continue
            candidates = sorted(
                (
                    distance_to_segment(local_point, start, end),
                    finger,
                    segment,
                    (finger, segment),
                )
                for candidate_side, finger, segment, start, end in segments
                if candidate_side == side
            )
            existing = [
                (assignment.group, assignment.weight)
                for assignment in vertex.groups
                if assignment.group not in group_indices[side]
                and assignment.weight > 0.0
            ]
            assigned_finger_indices = {
                assignment.group
                for assignment in vertex.groups
                if assignment.group in group_indices[side]
                and assignment.weight > 0.0
            }
            for group in resolved_groups[side].values():
                if group is not None and group.index in assigned_finger_indices:
                    # Removing even an out-of-range assignment is intentional:
                    # it is the distal/remote weight this pass is repairing.
                    group.remove([vertex.index])
                    cleared += 1
            if not candidates or candidates[0][0] > cleanup_radius:
                continue
            # Keep two closest segments so joints deform smoothly while the
            # exporter still has room for palm/forearm influences.
            chosen = candidates[:2]
            inverse_distance = [1.0 / (distance + 0.004) for distance, *_ in chosen]
            total = sum(inverse_distance)
            for raw, candidate in zip(inverse_distance, chosen):
                group = resolved_groups[side][candidate[3]]
                if group is None:
                    continue
                group.add(
                    [vertex.index], finger_share * raw / total, "REPLACE"
                )
            remaining_share = max(0.0, 1.0 - finger_share)
            existing_total = sum(weight for _, weight in existing)
            if existing_total > 1.0e-8:
                for name, weight in existing:
                    mesh.vertex_groups[name].add(
                        [vertex.index], remaining_share * weight / existing_total, "REPLACE"
                    )
            changed += 1
            break

    # Normalize all influences explicitly before glTF's four-influence limit.
    for vertex in mesh.data.vertices:
        assignments = [
            (assignment.weight, assignment.group)
            for assignment in vertex.groups
            if assignment.weight > 0.0
        ]
        total = sum(weight for weight, _ in assignments)
        if total <= 1.0e-8:
            continue
        for weight, group_index in assignments:
            mesh.vertex_groups[group_index].add(
                [vertex.index], weight / total, "REPLACE"
            )
    if stats is not None:
        stats["cleared_assignments"] = cleared
        stats["rebound_vertices"] = changed
    return changed


def export_scene(output_path: str, armature: bpy.types.Object) -> None:
    leftovers = [
        obj.name
        for obj in bpy.data.objects
        if is_helper_mesh(obj)
    ]
    if leftovers:
        raise RuntimeError(
            "helper meshes remain before export: " + ", ".join(sorted(leftovers))
        )
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.view_layer.objects.active = armature
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=output_path,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_skins=True,
        export_animations=True,
        export_animation_mode="BROADCAST",
        export_nla_strips=False,
        export_def_bones=True,
        export_leaf_bone=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_all_influences=False,
    )


def main() -> None:
    config = parse_args()
    input_path = os.path.abspath(config.input)
    output_path = os.path.abspath(config.output)
    if input_path == output_path:
        raise SystemExit("output must be separate from input")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=input_path)
    armature = find_armature()
    mesh = find_visual_mesh()
    removed_helpers = remove_helper_meshes(keep=(mesh,))
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    chains = collect_chains(mesh)
    reposition_finger_bones(armature, chains)
    bpy.context.view_layer.update()
    segments = build_segments(armature, mesh, chains)
    stats: dict[str, int] = {}
    changed = rebind_mesh(
        armature,
        mesh,
        segments,
        config.radius,
        config.finger_share,
        stats,
    )
    mesh["steel_tide_finger_rebind"] = "mesh_centroid_chain_v1"
    mesh["steel_tide_finger_rebind_vertices"] = changed
    mesh["steel_tide_finger_rebind_cleared_assignments"] = stats.get(
        "cleared_assignments", 0
    )
    mesh["steel_tide_removed_helper_meshes"] = ",".join(sorted(removed_helpers))
    export_scene(output_path, armature)
    print(
        "HY3D_HAND_REBIND_CHECK",
        f"chains={len(chains)}",
        f"vertices={changed}",
        f"cleared={stats.get('cleared_assignments', 0)}",
        f"helpers_removed={len(removed_helpers)}",
        f"radius={config.radius:.4f}",
        f"output={output_path}",
    )
    print("HY3D_HAND_REBIND_PASS valid=true")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"HY3D_HAND_REBIND_FAIL error={error}")
        raise
