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


def group_weight(mesh: bpy.types.Object, vertex: bpy.types.MeshVertex, name: str) -> float:
    group = mesh.vertex_groups.get(name)
    if group is None:
        return 0.0
    return next(
        (assignment.weight for assignment in vertex.groups if assignment.group == group.index),
        0.0,
    )


def is_finger_name(name: str, side: str) -> bool:
    return name.startswith(f"{side}Hand") and any(
        name.startswith(f"{side}Hand{finger}") for finger in FINGERS
    )


def dominant_finger_weights(
    mesh: bpy.types.Object, vertex: bpy.types.MeshVertex, side: str
) -> list[tuple[float, str]]:
    values = []
    for assignment in vertex.groups:
        name = mesh.vertex_groups[assignment.group].name
        if is_finger_name(name, side):
            values.append((assignment.weight, name))
    return values


def surface_centroids(
    mesh: bpy.types.Object,
    side: str,
    finger: str,
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
        points: list[Vector] = []
        weights: list[float] = []
        fallback_points: list[Vector] = []
        fallback_weights: list[float] = []
        for vertex in mesh.data.vertices:
            weight = group_weight(mesh, vertex, name)
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
    minimum_points: int = 8,
) -> dict[tuple[str, str], list[Vector]]:
    """Collect reliable chains and reject partial/damaged hand sources."""

    chains: dict[tuple[str, str], list[Vector]] = {}
    missing: list[str] = []
    for side in SIDES:
        for finger in FINGERS:
            values = surface_centroids(mesh, side, finger)
            if any(value is None for value in values):
                missing.append(f"{side}Hand{finger}")
                continue
            # Ensure each segment has a meaningful sample population.  A
            # single stray weight is the signature of the damaged Magpie GLB.
            for segment, value in zip(SEGMENTS, values):
                name = f"{side}Hand{finger}{segment}"
                count = 0
                for vertex in mesh.data.vertices:
                    if group_weight(mesh, vertex, name) > 0.0:
                        count += 1
                if count < minimum_points:
                    missing.append(f"{name}(samples={count})")
            if all(value is not None for value in values):
                chains[(side, finger)] = [value for value in values if value is not None]
    if missing:
        raise RuntimeError(
            "finger surface samples are incomplete: " + ", ".join(sorted(set(missing)))
        )
    return chains


def reposition_finger_bones(
    armature: bpy.types.Object,
    chains: dict[tuple[str, str], list[Vector]],
) -> None:
    """Move existing finger joints onto the mesh-derived chain in edit mode."""

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        world_to_armature = armature.matrix_world.inverted()
        for (side, finger), points in chains.items():
            bones = [
                armature.data.edit_bones.get(f"{side}Hand{finger}{segment}")
                for segment in SEGMENTS
            ]
            if any(bone is None for bone in bones):
                raise RuntimeError(f"armature is missing {side}Hand{finger} bones")
            # glTF stores joints, not Blender bone tails.  Disconnect before
            # moving heads so Blender does not move the palm tail in edit mode.
            for bone in bones:
                bone.use_connect = False
            for index, bone in enumerate(bones):
                bone.head = world_to_armature @ points[index]
                bone.tail = world_to_armature @ (
                    points[index + 1]
                    if index < len(points) - 1
                    else points[-1] + (points[-1] - points[-2])
                )
                bone.use_connect = False
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
) -> int:
    inverse = armature.matrix_world.inverted() @ mesh.matrix_world
    segments = list(segments)
    finger_groups = {
        side: {
            f"{side}Hand{finger}{segment}"
            for finger in FINGERS
            for segment in SEGMENTS
        }
        for side in SIDES
    }
    changed = 0
    for vertex in mesh.data.vertices:
        local_point = inverse @ vertex.co
        for side in SIDES:
            if not any(
                mesh.vertex_groups[assignment.group].name in finger_groups[side]
                and assignment.weight > 0.0
                for assignment in vertex.groups
            ):
                continue
            candidates = sorted(
                (
                    distance_to_segment(local_point, start, end),
                    finger,
                    segment,
                    f"{side}Hand{finger}{segment}",
                )
                for candidate_side, finger, segment, start, end in segments
                if candidate_side == side
            )
            if not candidates or candidates[0][0] > radius:
                continue
            # Keep two closest segments so joints deform smoothly while the
            # exporter still has room for palm/forearm influences.
            chosen = candidates[:2]
            existing = []
            for assignment in vertex.groups:
                name = mesh.vertex_groups[assignment.group].name
                if name not in finger_groups[side] and assignment.weight > 0.0:
                    existing.append((name, assignment.weight))
            for name in finger_groups[side]:
                group = mesh.vertex_groups.get(name)
                if group is not None:
                    group.remove([vertex.index])
            inverse_distance = [1.0 / (distance + 0.004) for distance, *_ in chosen]
            total = sum(inverse_distance)
            for raw, candidate in zip(inverse_distance, chosen):
                mesh.vertex_groups[candidate[3]].add(
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
    return changed


def export_scene(output_path: str, armature: bpy.types.Object) -> None:
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
    armature.data.pose_position = "REST"
    bpy.context.view_layer.update()
    chains = collect_chains(mesh)
    reposition_finger_bones(armature, chains)
    bpy.context.view_layer.update()
    segments = build_segments(armature, mesh, chains)
    changed = rebind_mesh(armature, mesh, segments, config.radius, config.finger_share)
    mesh["steel_tide_finger_rebind"] = "mesh_centroid_chain_v1"
    mesh["steel_tide_finger_rebind_vertices"] = changed
    export_scene(output_path, armature)
    print(
        "HY3D_HAND_REBIND_CHECK",
        f"chains={len(chains)}",
        f"vertices={changed}",
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
