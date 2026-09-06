"""Convert a Tencent HY-3D rigged character into this project's GLB contract.

The mesh and skin weights come from the Tencent HY-3D-Rigging FBX.  The 25
locomotion/combat actions come from the existing Quaternius Universal
Animation Library export and are baked on the Tencent skeleton in local rest
frames.  This is a Blender/DCC conversion step, not runtime primitive art.
"""
from __future__ import annotations

import argparse
import os
import sys
from typing import Iterable

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector

MAP = {
    "Root": "root", "Hips": "Hips", "Spine": "Spine", "Spine1": "Spine1", "Spine2": "Spine2",
    "Neck": "Neck", "Head": "Head", "LeftShoulder": "LeftShoulder", "LeftArm": "LeftArm",
    "LeftForeArm": "LeftForeArm", "LeftHand": "LeftHand", "RightShoulder": "RightShoulder",
    "RightArm": "RightArm", "RightForeArm": "RightForeArm", "RightHand": "RightHand",
    "LeftUpLeg": "LeftUpLeg", "LeftLeg": "LeftLeg", "LeftFoot": "LeftFoot",
    "RightUpLeg": "RightUpLeg", "RightLeg": "RightLeg", "RightFoot": "RightFoot",
}
EXPECTED = {
    "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk", "ready_idle", "ready_walk",
    "ready_run", "ready_sprint", "ready_crouch_idle", "ready_crouch_walk", "aim_walk", "aim_run",
    "aim_sprint", "aim_crouch_idle", "aim_crouch_walk", "prone_idle", "prone_crawl", "aim_idle",
    "hit", "death", "downed", "revive_kneel", "revived",
    "shoot", "reload", "melee", "throw", "interact", "pickup", "heal",
    "jump_start", "jump_loop", "jump_land", "slide_start", "slide_loop", "slide_exit",
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True)
    parser.add_argument("--rigged", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--triangles", type=int, default=60000)
    parser.add_argument("--retarget-mode", choices=("direction", "local"), default="direction")
    # Kept as a backwards-compatible no-op switch for older build commands.
    # The Tencent FBX armature is authored at centimetre scale; applying its
    # object scale is required for Godot's Skeleton3D importer to evaluate
    # bone translations in metres instead of raw FBX units.
    parser.add_argument("--experimental-apply-scale", action="store_true")
    return parser.parse_args(argv)


def import_asset(path: str) -> list[bpy.types.Object]:
    before = set(bpy.context.scene.objects)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, use_custom_normals=True, ignore_leaf_bones=False)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    return [obj for obj in bpy.context.scene.objects if obj not in before]


def armature(objects: Iterable[bpy.types.Object]) -> bpy.types.Object:
    value = next((obj for obj in objects if obj.type == "ARMATURE"), None)
    if value is None:
        raise RuntimeError("missing armature")
    return value


def visual_mesh(objects: Iterable[bpy.types.Object]) -> bpy.types.Object:
    meshes = [obj for obj in objects if obj.type == "MESH" and len(obj.data.polygons) > 100]
    if not meshes:
        raise RuntimeError("missing visual mesh")
    return max(meshes, key=lambda obj: len(obj.data.polygons))


def remove_embedded_weapon(mesh: bpy.types.Object, asset_name: str) -> int:
    """Remove the disconnected HY-3D carry rifle from the Magpie source.

    HY-3D occasionally bakes a prop into the character texture/mesh.  The
    Magpie FBX contains a second, disconnected rifle at the outer right thigh;
    deleting those faces before skin decimation prevents it from stretching
    with the leg during retargeted locomotion.  Other operators are left
    untouched because their dark backpack hardware is part of the authored
    silhouette.
    """
    if asset_name.lower() != "magpie":
        return 0
    data = mesh.data
    parent = list(range(len(data.vertices)))
    sizes = [1] * len(parent)

    def find(index: int) -> int:
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    def union(first: int, second: int) -> None:
        first = find(first)
        second = find(second)
        if first == second:
            return
        if sizes[first] < sizes[second]:
            first, second = second, first
        parent[second] = first
        sizes[first] += sizes[second]

    for edge in data.edges:
        union(edge.vertices[0], edge.vertices[1])

    component_faces: dict[int, list[bpy.types.MeshPolygon]] = {}
    for polygon in data.polygons:
        root = find(polygon.vertices[0])
        component_faces.setdefault(root, []).append(polygon)

    selected: set[int] = set()
    for root, polygons in component_faces.items():
        coordinates = [mesh.matrix_world @ data.vertices[index].co for polygon in polygons for index in polygon.vertices]
        minimum = Vector((min(point.x for point in coordinates), min(point.y for point in coordinates), min(point.z for point in coordinates)))
        maximum = Vector((max(point.x for point in coordinates), max(point.y for point in coordinates), max(point.z for point in coordinates)))
        dominant_groups: set[str] = set()
        for polygon in polygons:
            for index in polygon.vertices:
                for group in data.vertices[index].groups:
                    if group.weight >= 0.25 and group.group < len(mesh.vertex_groups):
                        dominant_groups.add(mesh.vertex_groups[group.group].name)
        spatial_weapon = (minimum.x < -0.40 and maximum.x < -0.34
                and minimum.z > 0.42 and maximum.z < 0.98
                and ("RightUpLeg" in dominant_groups or "RightHand" in dominant_groups))
        # Do not use the RightHand group as a prop heuristic.  Magpie's real
        # hand is a disconnected component in the HY-3D mesh too, so the old
        # broad check deleted the actual hand along with the embedded rifle.
        if spatial_weapon:
            selected.add(root)

    if not selected:
        return 0
    edit_mesh = bmesh.new()
    edit_mesh.from_mesh(data)
    faces_to_delete = [face for face in edit_mesh.faces if find(face.verts[0].index) in selected]
    bmesh.ops.delete(edit_mesh, geom=faces_to_delete, context="FACES")
    loose_vertices = [vertex for vertex in edit_mesh.verts if not vertex.link_faces]
    if loose_vertices:
        bmesh.ops.delete(edit_mesh, geom=loose_vertices, context="VERTS")
    edit_mesh.to_mesh(data)
    edit_mesh.free()
    data.update()
    return len(faces_to_delete)


def strong_weighted_vertices(mesh: bpy.types.Object, group_name: str, threshold: float = 0.25) -> int:
    """Count vertices that retain a meaningful weight for a contract bone."""
    group = mesh.vertex_groups.get(group_name)
    if group is None:
        return 0
    return sum(
        1
        for vertex in mesh.data.vertices
        if any(assignment.group == group.index and assignment.weight >= threshold for assignment in vertex.groups)
    )


def source_bone(source: bpy.types.Object, canonical: str) -> bpy.types.PoseBone | None:
    return source.pose.bones.get(canonical) or source.pose.bones.get("mixamorig:" + canonical)


def source_actions(source: bpy.types.Object) -> list[bpy.types.Action]:
    names = {bone.name for bone in source.data.bones}
    candidates = [action for action in bpy.data.actions if any(any(f'pose.bones["{name}"]' in curve.data_path for name in names) for curve in action.fcurves)]
    unique: dict[str, bpy.types.Action] = {}
    for action in sorted(candidates, key=lambda item: item.name):
        unique.setdefault(action.name.split(".")[0], action)
    return list(unique.values())


def _rest_local(armature: bpy.types.Object, bone: bpy.types.Bone) -> Matrix:
    """Return a bone's rest matrix in its parent-local frame."""
    if bone.parent is None:
        return bone.matrix_local.copy()
    return bone.parent.matrix_local.inverted() @ bone.matrix_local


def _pose_local(pose_bone: bpy.types.PoseBone) -> Matrix:
    """Return a pose bone matrix in its parent's current pose frame."""
    if pose_bone.parent is None:
        return pose_bone.matrix.copy()
    return pose_bone.parent.matrix.inverted() @ pose_bone.matrix


def _mapped_parent(
    canonical: str,
    target: bpy.types.Object,
) -> tuple[str | None, bpy.types.PoseBone | None, bpy.types.Bone | None]:
    target_name = MAP[canonical]
    target_data = target.data.bones.get(target_name)
    target_pose = target.pose.bones.get(target_name)
    if target_data is None or target_pose is None or target_data.parent is None:
        return None, None, target_data
    parent_name = target_data.parent.name
    parent_pose = target.pose.bones.get(parent_name)
    return parent_name, parent_pose, target_data


def capture_local_pose(armature: bpy.types.Object) -> dict[str, Matrix]:
    """Capture the imported bind/default pose in parent-local coordinates."""
    bpy.context.view_layer.update()
    result: dict[str, Matrix] = {}
    for canonical, target_name in MAP.items():
        pose = armature.pose.bones.get(target_name) or source_bone(armature, canonical)
        if pose is not None:
            result[canonical] = _pose_local(pose)
    return result


def capture_basis_pose(armature: bpy.types.Object) -> dict[str, Matrix]:
    """Capture imported channel-space bind offsets for each mapped bone."""
    bpy.context.view_layer.update()
    result: dict[str, Matrix] = {}
    for canonical, target_name in MAP.items():
        pose = armature.pose.bones.get(target_name) or source_bone(armature, canonical)
        if pose is not None:
            result[canonical] = pose.matrix_basis.copy()
    return result


def capture_display_pose(armature: bpy.types.Object) -> dict[str, Matrix]:
    """Capture Tencent's display-pose channel bases.

    HY-3D-Rigging stores useful pre-rotation channels in the FBX.  The mesh is
    skinned against that evaluated pose rather than the raw armature-data rest
    matrices, so a retarget must start from this pose and rotate its visible
    bone directions instead of replacing the pre-rotations with identity.
    """
    bpy.context.view_layer.update()
    return {
        target_name: armature.pose.bones[target_name].matrix_basis.copy()
        for _canonical, target_name in MAP.items()
        if armature.pose.bones.get(target_name) is not None
    }


def restore_display_pose(armature: bpy.types.Object, pose: dict[str, Matrix]) -> None:
    """Restore captured FBX channel bases parent-first without drift.

    Reassigning ``PoseBone.matrix`` after a parent has rotated decomposes the
    child into a large local translation on FBX rigs.  Restoring the original
    ``matrix_basis`` channels avoids that decomposition and keeps every sample
    independent of the previous frame.
    """
    ordered = sorted(
        (name for name in pose if armature.pose.bones.get(name) is not None),
        key=lambda name: len(armature.data.bones[name].parent_recursive),
    )
    for name in ordered:
        bone = armature.pose.bones[name]
        bone.matrix_basis = pose[name]
    bpy.context.view_layer.update()


def _frame_transform(source: bpy.types.Object, target: bpy.types.Object) -> Matrix:
    """Build a uniform torso-frame transform from source to target space."""
    def points(armature: bpy.types.Object) -> dict[str, Vector]:
        result = {}
        for canonical in ("Hips", "Head", "LeftShoulder", "RightShoulder"):
            pose = source_bone(armature, canonical)
            if pose is None:
                raise RuntimeError(f"missing frame bone {canonical}")
            result[canonical] = armature.matrix_world @ pose.bone.head_local
        return result

    s = points(source)
    t = points(target)
    def basis(p: dict[str, Vector]) -> Matrix:
        up = (p["Head"] - p["Hips"]).normalized()
        right = (p["LeftShoulder"] - p["RightShoulder"]).normalized()
        forward = right.cross(up).normalized()
        # Re-orthogonalize right to avoid small FBX rest-pose skew.
        right = up.cross(forward).normalized()
        return Matrix((right, forward, up)).transposed()

    source_basis = basis(s)
    target_basis = basis(t)
    rotation = target_basis @ source_basis.transposed()
    scale = (t["Head"] - t["Hips"]).length / max(0.0001, (s["Head"] - s["Hips"]).length)
    translation = t["Hips"] - rotation @ (s["Hips"] * scale)
    # ``Matrix *=`` is element-wise in Blender's mathutils; using it here
    # silently erased the torso-frame rotation.  Compose the uniform scale
    # with matrix multiplication and assign translation afterwards.
    transform = rotation.to_4x4() @ Matrix.Diagonal((scale, scale, scale, 1.0))
    transform.translation = translation
    return transform


def bake_action_direction(
    source: bpy.types.Object,
    target: bpy.types.Object,
    action: bpy.types.Action,
    frame_transform: Matrix,
) -> bpy.types.Action:
    """Bake bone directions/positions into the Tencent hierarchy."""
    source.animation_data_create(); source.animation_data.action = action
    output = bpy.data.actions.new("HY3D_" + action.name.split(".")[0])
    target.animation_data_create(); target.animation_data.action = output
    # Keying an action while it is already assigned can feed interpolated
    # values back into the next frame.  Mute the freshly-created curves until
    # all poses have been authored; each frame is restored from the immutable
    # Tencent display pose below.
    for curve in output.fcurves:
        curve.mute = True
    target_inverse = target.matrix_world.inverted()
    start, end = [int(round(value)) for value in action.frame_range]
    for frame in range(start, end + 1):
        bpy.context.scene.frame_set(frame)
        for canonical, target_name in MAP.items():
            src = source_bone(source, canonical)
            dst = target.pose.bones.get(target_name)
            target_data = target.data.bones.get(target_name)
            if src is None or dst is None or target_data is None:
                continue
            source_head = source.matrix_world @ src.head
            source_tail = source.matrix_world @ src.tail
            desired_head = target_inverse @ (frame_transform @ source_head)
            desired_tail = target_inverse @ (frame_transform @ source_tail)
            desired_direction = desired_tail - desired_head
            if desired_direction.length_squared <= 1e-8:
                continue
            rest_direction = target_data.tail_local - target_data.head_local
            align = rest_direction.normalized().rotation_difference(desired_direction.normalized())
            desired = align.to_matrix().to_4x4() @ target_data.matrix_local
            desired.translation = desired_head
            dst.rotation_mode = "QUATERNION"
            dst.matrix = desired
            # Non-root translation channels are not part of the Tencent bind
            # contract.  Keeping the absolute head position here makes every
            # child bone translate twice and stretches the skinned gear when
            # the source action bends (most obvious in prone/death clips).
            # Navigation owns actor displacement in Godot.  Zero every bone
            # translation channel, including the source Root motion, so a
            # glTF importer cannot reinterpret centimetre FBX offsets as
            # multi-metre world movement during action seeks.
            dst.location = Vector((0.0, 0.0, 0.0))
            dst.scale = Vector((1.0, 1.0, 1.0))
            dst.keyframe_insert("rotation_quaternion", frame=frame, group=target_name)
            dst.keyframe_insert("location", frame=frame, group=target_name)
    for curve in output.fcurves:
        curve.mute = False
    source.animation_data.action = None; target.animation_data.action = None
    return output


def bake_action_display_direction(
    source: bpy.types.Object,
    target: bpy.types.Object,
    action: bpy.types.Action,
    display_pose: dict[str, Matrix],
    frame_transform: Matrix,
) -> bpy.types.Action:
    """Retarget source motion while preserving the Tencent visible bind pose.

    The two imported rigs use different bone-roll conventions.  Copying local
    quaternion channels therefore twists the skinned mesh.  Instead, each
    source joint contributes its evaluated world-space direction; the target
    starts from Tencent's display pose and applies only the shortest direction
    alignment, parent-first.  This preserves the authored limb roll, clothing
    deformation, and equipment silhouette while still carrying the mature
    Quaternius gait/combat timing.
    """
    source.animation_data_create(); source.animation_data.action = action
    target.animation_data_create(); target.animation_data.action = None
    # Do not assign a freshly-created Action while sampling.  Blender creates
    # F-curves lazily on the first ``keyframe_insert`` call; muting an empty
    # action therefore leaves the newly-created curves live and they feed the
    # previous sample back into the next frame.  Collect local channels in
    # memory first, then author the Action once the complete clip is stable.
    start, end = [int(round(value)) for value in action.frame_range]
    ordered = sorted(MAP.items(), key=lambda item: len(target.data.bones[item[1]].parent_recursive))
    looped = action.name.split(".")[0] in {
        "idle", "walk", "run", "sprint", "crouch_idle", "crouch_walk",
        "ready_idle", "ready_walk", "ready_run", "ready_sprint",
        "ready_crouch_idle", "ready_crouch_walk", "aim_idle", "aim_walk",
        "aim_run", "aim_sprint", "aim_crouch_idle", "aim_crouch_walk",
        "prone_idle", "prone_crawl", "revive_kneel", "downed",
    }
    # ``idle`` settles into the authored relaxed stance around frame 15;
    # starting there avoids the library's opening gesture while preserving a
    # subtle breathing loop.  Other clips only need the first three frames
    # skipped because their glTF import starts with a stale sample.
    sample_offset = 15 if action.name.split(".")[0] == "idle" else 3
    target_inverse = target.matrix_world.inverted()
    frames: list[int] = []
    samples: dict[str, list[tuple[Quaternion, Vector, Vector]]] = {
        canonical: [] for canonical, _target_name in MAP.items()
    }
    for frame in range(start, end + 1):
        # The Quaternius glTF has an import-time stale sample at frame 0.  A
        # small offset avoids a one-frame snap; looped clips wrap it cleanly.
        if looped:
            valid_start = min(end, start + sample_offset)
            valid_span = max(1, end - valid_start + 1)
            sample = valid_start + ((frame - start) % valid_span)
        else:
            sample = min(end, frame + sample_offset)
        bpy.context.scene.frame_set(sample)
        bpy.context.view_layer.update()
        restore_display_pose(target, display_pose)
        for canonical, target_name in ordered:
            src = source_bone(source, canonical)
            dst = target.pose.bones.get(target_name)
            if src is None or dst is None:
                continue
            bpy.context.view_layer.update()
            current_world = target.matrix_world @ dst.matrix
            current_direction = (
                target.matrix_world @ dst.tail - target.matrix_world @ dst.head
            ).normalized()
            # The source GLB and Tencent FBX use different world-facing axes.
            # Convert both endpoints through the torso frame before aligning
            # directions; using the raw source vector mirrors/rolls the limbs
            # and is especially visible in crouch, prone, and sprint clips.
            source_head = frame_transform @ (source.matrix_world @ src.head)
            source_tail = frame_transform @ (source.matrix_world @ src.tail)
            source_direction = (source_tail - source_head).normalized()
            if current_direction.length_squared <= 1.0e-8 or source_direction.length_squared <= 1.0e-8:
                continue
            alignment = current_direction.rotation_difference(source_direction)
            desired_world = alignment.to_matrix().to_4x4() @ current_world
            desired_world.translation = target.matrix_world @ dst.head
            dst.rotation_mode = "QUATERNION"
            dst.matrix = target_inverse @ desired_world
            if canonical not in samples:
                continue
            # Store local quaternion channels, not pose matrices.  The FBX
            # contains non-identity pre-rotations; exporting these channels
            # against the untouched Tencent rest pose preserves its skin bind.
            samples[canonical].append((dst.rotation_quaternion.copy(), dst.location.copy(), dst.scale.copy()))
        frames.append(frame)
        bpy.context.view_layer.update()
    output = bpy.data.actions.new("HY3D_" + action.name.split(".")[0])
    # Blender 4.4+ stores armature F-curves in an ActionSlot/channelbag.  A
    # legacy ``output.fcurves.new`` creates an UNSPECIFIED slot which the
    # glTF ACTIONS exporter ignores, so build the keyframe channelbag first.
    output_slot = output.slots.new(target.id_type, target.name)
    output_layer = output.layers.new("Layer")
    output_strip = output_layer.strips.new(type="KEYFRAME")
    output_channelbag = output_strip.channelbag(output_slot, ensure=True)
    # Author f-curves directly.  Besides avoiding feedback, this lets us make
    # quaternion signs continuous so short tactical clips do not interpolate
    # through a 360-degree spin at a key boundary.
    for canonical, target_name in MAP.items():
        values = samples.get(canonical, [])
        if not values or len(values) != len(frames):
            continue
        previous: Quaternion | None = None
        quaternions: list[Quaternion] = []
        locations: list[Vector] = []
        scales: list[Vector] = []
        for quaternion, location, scale in values:
            q = quaternion.copy()
            if previous is not None and previous.dot(q) < 0.0:
                q.negate()
            previous = q.copy(); quaternions.append(q); locations.append(location.copy()); scales.append(scale.copy())
        for index in range(4):
            curve = output_channelbag.fcurves.new(
                data_path=f'pose.bones["{target_name}"].rotation_quaternion',
                index=index,
            )
            group = output_channelbag.groups.get(target_name) or output_channelbag.groups.new(target_name)
            curve.group = group
            curve.keyframe_points.add(len(frames))
            for point, frame, quaternion in zip(curve.keyframe_points, frames, quaternions):
                point.co = (frame, float(quaternion[index])); point.interpolation = "BEZIER"
            curve.update()
        # Preserve the FBX local location/scale channels as well.  They are
        # tiny (the armature is authored in centimetres), but omitting them
        # makes Blender reset a child's bind offset when a rotation action is
        # evaluated, shifting the hips/feet laterally on re-import.
        for property_name, vectors in (("location", locations), ("scale", scales)):
            for index in range(3):
                curve = output_channelbag.fcurves.new(
                    data_path=f'pose.bones["{target_name}"].{property_name}',
                    index=index,
                )
                group = output_channelbag.groups.get(target_name) or output_channelbag.groups.new(target_name)
                curve.group = group
                curve.keyframe_points.add(len(frames))
                for point, frame, vector in zip(curve.keyframe_points, frames, vectors):
                    point.co = (frame, float(vector[index])); point.interpolation = "BEZIER"
                curve.update()
    source.animation_data.action = None; target.animation_data.action = None
    return output


def bake_action(
    source: bpy.types.Object,
    target: bpy.types.Object,
    action: bpy.types.Action,
    source_reference: dict[str, Matrix],
    target_reference: dict[str, Matrix],
) -> bpy.types.Action:
    """Bake source local joint deltas onto the target's rest hierarchy.

    Copying a world-space rest correction looks attractive, but it applies the
    source armature's parent basis twice on FBX rigs whose bone rolls differ.
    Computing each joint's local rest-to-pose delta and rebuilding it under the
    target parent preserves the animation semantics while retaining Tencent's
    authored proportions.
    """
    source.animation_data_create()
    source.animation_data.action = action
    output = bpy.data.actions.new("HY3D_" + action.name.split(".")[0])
    target.animation_data_create()
    target.animation_data.action = output
    start, end = [int(round(value)) for value in action.frame_range]
    # MAP is ordered parent-first; keep this explicit so matrix assignment is
    # deterministic even if the source file's bone collection order changes.
    ordered = list(MAP.items())
    for frame in range(start, end + 1):
        bpy.context.scene.frame_set(frame)
        for canonical, target_name in ordered:
            src = source_bone(source, canonical)
            dst = target.pose.bones.get(target_name)
            target_data = target.data.bones.get(target_name)
            if src is None or dst is None or target_data is None:
                continue
            source_pose_local = src.matrix_basis.copy()
            source_rest_local = source_reference.get(canonical, Matrix.Identity(4))
            delta = source_rest_local.inverted() @ source_pose_local
            target_rest_local = target_reference.get(target_name, Matrix.Identity(4))
            desired = target_rest_local @ delta
            if canonical not in {"Root", "Hips"}:
                desired.translation = Vector((0.0, 0.0, 0.0))
            dst.rotation_mode = "QUATERNION"
            dst.matrix_basis = desired
            dst.scale = Vector((1.0, 1.0, 1.0))
            dst.keyframe_insert("rotation_quaternion", frame=frame, group=target_name)
            dst.keyframe_insert("location", frame=frame, group=target_name)
    source.animation_data.action = None
    target.animation_data.action = None
    return output


def triangle_count(mesh: bpy.types.Object) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in mesh.data.polygons)


def reduce_mesh(mesh: bpy.types.Object, budget: int) -> int:
    count = triangle_count(mesh)
    if count > budget:
        modifier = mesh.modifiers.new("SteelTideGameplayDecimate", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.001, min(1.0, budget / float(count)))
        bpy.ops.object.select_all(action="DESELECT"); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
        # Apply decimation in the mesh's bind space, before the imported
        # Armature modifier.  Leaving it after Armature produces Blender's
        # "modifier was not first" warning and can decimate already-deformed
        # vertices, which subtly changes skin weights at runtime.
        while mesh.modifiers.find(modifier.name) > 0:
            bpy.ops.object.modifier_move_up(modifier=modifier.name)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return triangle_count(mesh)


def limit_vertex_influences(mesh: bpy.types.Object, limit: int = 4) -> int:
    """Keep the strongest deform weights and renormalize each vertex.

    glTF skinning guarantees at least four influences, while the HY-3D FBX
    can contain a longer tail of tiny weights.  Letting the exporter truncate
    those weights implicitly makes each export depend on exporter details and
    emits a warning.  Do the reduction explicitly after decimation so the
    delivered bind and runtime deformation are deterministic.
    """
    kept_vertices = 0
    groups = {group.index: group for group in mesh.vertex_groups}
    for vertex in mesh.data.vertices:
        weighted = sorted(
            ((element.weight, element.group) for element in vertex.groups if element.group in groups and element.weight > 0.0),
            reverse=True,
        )
        if len(weighted) <= limit:
            continue
        kept = weighted[:limit]
        total = sum(weight for weight, _group_index in kept)
        if total <= 1.0e-8:
            continue
        keep_indices = {group_index for _weight, group_index in kept}
        for _weight, group_index in weighted[limit:]:
            groups[group_index].remove([vertex.index])
        for weight, group_index in kept:
            groups[group_index].add([vertex.index], weight / total, "REPLACE")
        kept_vertices += 1
    return kept_vertices


def add_contract_nodes(target: bpy.types.Object, mesh: bpy.types.Object) -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    # Iterate the datablock, not only the active scene collection: imported
    # glTF markers such as Quaternius' Icosphere can remain linked through a
    # nested collection and must not ship as visible gameplay geometry.
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj != mesh:
            bpy.data.objects.remove(obj, do_unlink=True)
    target.name = "QuaterniusOperatorRig"; target.data.name = "QuaterniusOperatorRig"; mesh.name = "OperatorBody"
    if mesh.parent != target:
        mesh.parent = target; mesh.parent_type = "OBJECT"
    root = bpy.data.objects.new("QuaterniusOperator", None); bpy.context.collection.objects.link(root); target.parent = root
    specs = [("WeaponSocket", "RightHand", (0.0, 0.0, 0.0)), ("BackWeaponSocket", "Spine2", (0.0, -0.18, 0.05)), ("HeadSocket", "Head", (0.0, 0.0, 0.05)), ("VestSocket", "Spine2", (0.0, 0.0, 0.0)), ("BackpackSocket", "Spine", (0.0, -0.16, 0.0)), ("TeamPatchSocket", "Spine2", (0.0, 0.16, 0.0))]
    sockets=[]
    for name, bone, offset in specs:
        marker=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(marker); marker.parent=target; marker.parent_type="BONE"; marker.parent_bone=bone; marker.location=offset; sockets.append(marker)
    return root, sockets


def main() -> None:
    cfg = parse_args(); source_path=os.path.abspath(cfg.source); rigged_path=os.path.abspath(cfg.rigged); output_path=os.path.abspath(cfg.output)
    if output_path in {source_path, rigged_path}: raise SystemExit("output must be separate from inputs")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source_objects=import_asset(source_path); source_names=[obj.name for obj in source_objects]; source=armature(source_objects); actions=source_actions(source)
    source.animation_data_create(); source.animation_data.action = None; bpy.context.scene.frame_set(0); bpy.context.view_layer.update()
    source_reference = capture_basis_pose(source)
    target_objects=import_asset(rigged_path); target=armature(target_objects); mesh=visual_mesh(target_objects)
    removed_weapon_faces = remove_embedded_weapon(mesh, os.path.splitext(os.path.basename(source_path))[0])
    bpy.ops.object.select_all(action="DESELECT"); target.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active=target
    # Canonicalize the FBX armature to metre-space before baking. Godot's
    # Skeleton3D importer does not propagate the nested FBX object scale to
    # animation translation tracks, so leaving the authored 0.01 scale makes
    # animated limbs tens of metres long at runtime.
    target.animation_data_create(); target.animation_data.action = None; bpy.context.scene.frame_set(0); bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True); bpy.context.view_layer.objects.active=target
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.context.view_layer.update()
    target_reference = capture_basis_pose(target)
    display_pose = capture_display_pose(target)
    frame_transform = _frame_transform(source, target)
    if cfg.retarget_mode == "direction":
        generated=[bake_action_display_direction(source, target, action, display_pose, frame_transform) for action in actions]
    else:
        generated=[bake_action(source,target,action,source_reference,target_reference) for action in actions]
    source_ids={id(action) for action in actions}
    for action in list(bpy.data.actions):
        if id(action) in source_ids: bpy.data.actions.remove(action)
    for action in generated: action.name=action.name.removeprefix("HY3D_")
    # Keep every clip discoverable by the glTF ACTIONS exporter.  Actions
    # authored directly through ``Action.fcurves`` otherwise have zero users
    # and Blender silently drops them from the exported GLB.
    for action in generated:
        action.use_fake_user = True
    target.animation_data_create(); target.animation_data.action = generated[0]
    missing=EXPECTED-{action.name for action in generated}
    if missing: raise RuntimeError("missing canonical actions: "+",".join(sorted(missing)))
    # Remove source objects before renaming the target to the same contract
    # names (Quaternius and Tencent both commonly use OperatorBody/Rig names).
    for source_name in source_names:
        live = bpy.data.objects.get(source_name)
        if live is not None:
            bpy.data.objects.remove(live, do_unlink=True)
    triangles=reduce_mesh(mesh,cfg.triangles); limited_vertices=limit_vertex_influences(mesh)
    right_hand_vertices = strong_weighted_vertices(mesh, "RightHand")
    if right_hand_vertices == 0:
        raise RuntimeError("RightHand skin weights missing after embedded-prop cleanup")
    root,sockets=add_contract_nodes(target,mesh)
    root["steel_tide_asset_role"]="realistic_hy3d_operator"; root["mesh_source"]="Tencent HY-3D-3.1 + HY-3D-Rigging"; root["animation_source"]="Quaternius Universal Animation Library (CC0), rest-frame retarget"; root["triangle_count"]=triangles; root["animation_count"]=len(generated); root["removed_embedded_weapon_faces"]=removed_weapon_faces
    bpy.ops.object.select_all(action="DESELECT"); root.select_set(True); target.select_set(True); mesh.select_set(True); [obj.select_set(True) for obj in sockets]; bpy.context.view_layer.objects.active=root
    os.makedirs(os.path.dirname(output_path),exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=output_path,export_format="GLB",use_selection=True,export_yup=True,export_apply=False,export_skins=True,export_animations=True,export_animation_mode="BROADCAST",export_nla_strips=False,export_def_bones=True,export_leaf_bone=False,export_materials="EXPORT",export_image_format="AUTO",export_texcoords=True,export_normals=True,export_tangents=False,export_all_influences=False)
    print("HY3D_OPERATOR_CHECK",f"actions={len(generated)}",f"bones={len(target.data.bones)}",f"triangles={triangles}",f"sockets={len(sockets)}",f"limited_vertices={limited_vertices}",f"right_hand_vertices={right_hand_vertices}",f"removed_embedded_weapon_faces={removed_weapon_faces}",f"output={output_path}"); print("HY3D_OPERATOR_PASS valid=true")


if __name__ == "__main__": main()
