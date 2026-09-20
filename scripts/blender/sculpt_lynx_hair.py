"""Separate Lynx's existing hair surface from its fused backpack in Blender.

The approved HY-3D mesh supplies every hair face and UV. The lower original
surface becomes a cloth backpack shell, while the extracted locks are gathered,
shortened and given staggered ends. No primitive substitute is introduced.
"""

from __future__ import annotations

import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


def weld_surface(mesh):
    edit = bmesh.new()
    edit.from_mesh(mesh.data)
    before = len(edit.verts)
    deform = edit.verts.layers.deform.active
    if deform is not None:
        targets = bmesh.ops.find_doubles(edit, verts=list(edit.verts), dist=0.00045)["targetmap"]
        clusters = {}
        for vertex, target in targets.items():
            while target in targets:
                target = targets[target]
            clusters.setdefault(target, set()).update((vertex, target))
        for vertices in clusters.values():
            weights = {}
            for vertex in vertices:
                for group, weight in vertex[deform].items():
                    weights[group] = weights.get(group, 0.0) + weight
            kept = sorted(weights.items(), key=lambda pair: pair[1], reverse=True)[:8]
            total = sum(weight for _, weight in kept)
            if total <= 1.0e-8:
                continue
            for vertex in vertices:
                vertex[deform].clear()
                for group, weight in kept:
                    vertex[deform][group] = weight / total
    bmesh.ops.remove_doubles(edit, verts=list(edit.verts), dist=0.00045)
    edit.to_mesh(mesh.data)
    edit.free()
    mesh.data.update()
    mesh["steel_tide_welded_surface_vertices"] = before - len(mesh.data.vertices)


def restore_authored_backpack(mesh):
    """Fit the approved Heron backpack surface beneath Lynx's loose locks."""
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    path = Path(__file__).resolve().parents[2] / "source_art/hy3d_operators/heron.blend"
    with bpy.data.libraries.load(str(path), link=False) as (_source, target):
        target.objects = ["OperatorBody"]
    source_object = target.objects[0]
    source = source_object.data
    data = source.copy()
    data.name = "LynxAuthoredBackpack"
    cap = bpy.data.materials.get("LynxBackpackSeamCloth") or bpy.data.materials.new("LynxBackpackSeamCloth")
    cap.use_nodes = True
    shader = cap.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (0.055, 0.062, 0.034, 1.0)
    shader.inputs["Roughness"].default_value = 0.90
    data.materials.append(cap)
    cap_index = len(data.materials) - 1
    edit = bmesh.new()
    edit.from_mesh(data)
    bmesh.ops.remove_doubles(edit, verts=list(edit.verts), dist=0.00045)
    planes = [((-0.170, 0, 0), (1, 0, 0)), ((0.205, 0, 0), (-1, 0, 0)),
              ((0, 0.055, 0), (0, 1, 0)), ((0, 0, 1.07), (0, 0, 1)),
              ((0, 0, 1.470), (0, 0, -1))]
    for point, normal in planes:
        result = bmesh.ops.bisect_plane(edit, geom=list(edit.verts) + list(edit.edges) + list(edit.faces),
                                       dist=1.0e-6, plane_co=point, plane_no=normal,
                                       clear_inner=True, clear_outer=False)
    pending = set(edit.verts)
    components = []
    while pending:
        component = {pending.pop()}
        frontier = list(component)
        while frontier:
            vertex = frontier.pop()
            for edge in vertex.link_edges:
                neighbor = edge.other_vert(vertex)
                if neighbor in pending:
                    pending.remove(neighbor)
                    component.add(neighbor)
                    frontier.append(neighbor)
        components.append(component)
    keep = max(components, key=len)
    discarded = [vertex for vertex in edit.verts if vertex not in keep]
    if discarded:
        bmesh.ops.delete(edit, geom=discarded, context="VERTS")
    filled = bmesh.ops.holes_fill(edit, edges=[edge for edge in edit.edges if edge.is_boundary], sides=0)
    for face in filled.get("faces", []):
        face.material_index = cap_index
        face.smooth = True
    bmesh.ops.triangulate(edit, faces=list(edit.faces))
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    deform = edit.verts.layers.deform.active
    if deform is not None:
        for vertex in edit.verts:
            vertex[deform].clear()
    minimum = Vector(min(vertex.co[axis] for vertex in edit.verts) for axis in range(3))
    maximum = Vector(max(vertex.co[axis] for vertex in edit.verts) for axis in range(3))
    for vertex in edit.verts:
        normalized = vertex.co - minimum
        normalized.x /= maximum.x - minimum.x
        normalized.y /= maximum.y - minimum.y
        normalized.z /= maximum.z - minimum.z
        vertex.co = Vector((-0.175 + normalized.x * 0.37,
                            0.014 + normalized.y * 0.155,
                            1.015 + normalized.z * 0.385))
    edit.to_mesh(data)
    edit.free()
    data.update()
    if len(data.polygons) < 500:
        raise RuntimeError(f"Heron backpack extraction changed: {len(data.polygons)} faces")
    result = bpy.data.objects.new("LynxAuthoredBackpack", data)
    bpy.context.collection.objects.link(result)
    result.parent = mesh.parent
    result.matrix_world = mesh.matrix_world.copy()
    result.vertex_groups.clear()
    group = result.vertex_groups.new(name="Spine2")
    group.add(list(range(len(data.vertices))), 1.0, "REPLACE")
    modifier = result.modifiers.new("Armature", "ARMATURE")
    modifier.object = next(modifier.object for modifier in mesh.modifiers if modifier.type == "ARMATURE")
    for obj in set(bpy.data.objects) - before_objects - {result}:
        bpy.data.objects.remove(obj, do_unlink=True)
    for action in set(bpy.data.actions) - before_actions:
        bpy.data.actions.remove(action, do_unlink=True)
    if source.users == 0:
        bpy.data.meshes.remove(source)
    weld_surface(result)
    result["steel_tide_source"] = "Approved Heron backpack surface and UV atlas"
    print(f"LYNX_BACKPACK_CHECK faces={len(data.polygons)} vertices={len(data.vertices)}")
    return result


def select_fused_backpack_faces(mesh, hair_faces=()):
    """Select the original rear pack without cutting white sleeves beside it.

    The pack shares the largest body component, so a bounding box or whole
    connected-component deletion removes part of the arms. Use its original
    atlas and arm labels to protect sleeves, then follow the rear pack surface
    across UV splits. Small detached rear islands are included in full.
    """
    import numpy as np

    source = mesh.data
    excluded = set(hair_faces)
    material = source.materials[0]
    atlas = next(node.image for node in material.node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image is not None)
    width, height = atlas.size
    pixels = np.asarray(atlas.pixels[:], dtype=np.float32).reshape(height, width, 4)
    uv = source.uv_layers.active.data
    arm_groups = {group.index for group in mesh.vertex_groups
                  if any(group.name.split(":")[-1] == side + segment
                         for side in ("Left", "Right") for segment in ("Arm", "ForeArm", "Hand"))}
    positions = [mesh.matrix_world @ vertex.co for vertex in source.vertices]
    arm_weights = [sum(item.weight for item in vertex.groups if item.group in arm_groups)
                   for vertex in source.vertices]
    # Original UV seams are not physical barriers in the authored surface.
    cells = {}
    vertex_cells = []
    for point in positions:
        key = tuple(round(value, 4) for value in point)
        vertex_cells.append(cells.setdefault(key, len(cells)))
    touching = {}
    candidates = set()
    seeds = set()
    protected_sleeves = 0
    for face in source.polygons:
        points = [positions[index] for index in face.vertices]
        center = sum(points, Vector()) / len(points)
        if face.index in excluded or not (
                1.005 < center.z < 1.515 and center.y > 0.035 and -0.245 < center.x < 0.245):
            continue
        samples = []
        for loop_index in face.loop_indices:
            u, v = uv[loop_index].uv
            samples.append(pixels[min(height - 1, max(0, int(v * height))),
                                  min(width - 1, max(0, int(u * width))), :3])
        color = np.median(samples, axis=0)
        near_arm = abs(center.x) > 0.12 and center.y < 0.135
        pale_cloth = near_arm and color.max() > 0.20 and color[2] >= color[1] * 0.96
        arm = [arm_weights[index] for index in face.vertices]
        if pale_cloth or near_arm and center.y < 0.065 and (np.mean(arm) > 0.30 or max(arm) > 0.65):
            protected_sleeves += 1
            continue
        candidates.add(face.index)
        for index in face.vertices:
            touching.setdefault(vertex_cells[index], set()).add(face.index)
        if center.y > 0.065 and -0.21 < center.x < 0.21 and center.z < 1.445:
            seeds.add(face.index)
    pending = candidates.copy()
    selected = set()
    while pending:
        component = {pending.pop()}
        frontier = list(component)
        while frontier:
            face_index = frontier.pop()
            for index in source.polygons[face_index].vertices:
                for neighbor in touching[vertex_cells[index]]:
                    if neighbor in pending:
                        pending.remove(neighbor)
                        component.add(neighbor)
                        frontier.append(neighbor)
        if component & seeds or len(component) <= 300:
            selected.update(component)
    if not selected:
        raise RuntimeError("Lynx original backpack has no connected rear surface")
    print(f"LYNX_PACK_SELECTION_CHECK faces={len(selected)} candidates={len(candidates)} protected_sleeves={protected_sleeves}")
    return selected


def separate_hair(mesh, mask):
    source = mesh.data
    polygons = [polygon for polygon in source.polygons
                if sum(bool(mask[index]) for index in polygon.vertices) >= 2]
    if len(polygons) < 6000:
        raise RuntimeError(f"Lynx hair extraction is incomplete: {len(polygons)} faces")
    indices = sorted({index for polygon in polygons for index in polygon.vertices})
    remap = {index: target for target, index in enumerate(indices)}
    data = bpy.data.meshes.new("LynxAuthoredHairLocks")
    data.from_pydata([source.vertices[index].co.copy() for index in indices], [],
                     [[remap[index] for index in polygon.vertices] for polygon in polygons])
    for material in source.materials:
        data.materials.append(material)
    for target, original in zip(data.polygons, polygons):
        target.material_index = original.material_index
        target.use_smooth = True
    uv = data.uv_layers.new(name=source.uv_layers.active.name)
    for target, original in zip(data.polygons, polygons):
        for target_loop, original_loop in zip(target.loop_indices, original.loop_indices):
            uv.data[target_loop].uv = source.uv_layers.active.data[original_loop].uv
    hair = bpy.data.objects.new("LynxHairLocks", data)
    bpy.context.collection.objects.link(hair)
    hair.parent = mesh.parent
    hair.matrix_world = mesh.matrix_world.copy()
    for group in mesh.vertex_groups:
        hair.vertex_groups.new(name=group.name)
    for old_index, new_index in remap.items():
        for assignment in source.vertices[old_index].groups:
            hair.vertex_groups[assignment.group].add([new_index], assignment.weight, "REPLACE")
    modifier = hair.modifiers.new("Armature", "ARMATURE")
    modifier.object = next(modifier.object for modifier in mesh.modifiers if modifier.type == "ARMATURE")
    modifier.use_deform_preserve_volume = False

    # Remove the fused hair substrate completely. Its sculpted grooves cannot
    # become fabric through a material swap; a finished approved backpack
    # surface is fitted beneath it instead.
    removed = {polygon.index for polygon in polygons}
    removed.update(select_fused_backpack_faces(mesh, removed))
    edit = bmesh.new()
    edit.from_mesh(source)
    edit.faces.ensure_lookup_table()
    cut = [face for face in edit.faces if face.index in removed]
    bmesh.ops.delete(edit, geom=cut, context="FACES")
    # A few source triangles share the sleeve's pale atlas value despite lying
    # on the lower pack. The cut exposes them as tiny detached rear islands.
    pending = set(edit.verts)
    discarded = []
    while pending:
        component = {pending.pop()}
        frontier = list(component)
        while frontier:
            vertex = frontier.pop()
            for edge in vertex.link_edges:
                neighbor = edge.other_vert(vertex)
                if neighbor in pending:
                    pending.remove(neighbor)
                    component.add(neighbor)
                    frontier.append(neighbor)
        center = sum((mesh.matrix_world @ vertex.co for vertex in component), Vector()) / len(component)
        if len(component) < 300 and 1.005 < center.z < 1.32 and center.y > 0.04 and abs(center.x) < 0.22:
            discarded.extend(component)
    if discarded:
        bmesh.ops.delete(edit, geom=discarded, context="VERTS")
    print(f"LYNX_PACK_REMNANTS_CHECK vertices={len(discarded)}", flush=True)
    edit.to_mesh(source)
    edit.free()
    source.update()
    hair["steel_tide_source_hair_faces"] = len(polygons)
    sculpt_locks(hair)
    restore_authored_backpack(mesh)
    return hair


def sculpt_locks(hair):
    """Use proportional edits and face cuts to author distinct hanging ends."""
    world = hair.matrix_world
    inverse = world.inverted()
    for vertex in hair.data.vertices:
        point = world @ vertex.co
        drop = max(0.0, 1.55 - point.z)
        fraction = min(1.0, max(0.0, 1.44 - point.z) / 0.16)
        smooth = fraction * fraction * (3.0 - 2.0 * fraction)
        original_x = point.x - 0.008
        point.x = 0.008 + original_x * (1.0 - 0.45 * smooth)
        # Flatten the source's backpack-shaped bulge into loose, hanging locks.
        # Keep their lower tips behind the pack while the middle lies closer
        # to the neck; merely pushing every strand rearward preserves the lump.
        hanging_y = 0.180 + 0.090 * min(1.0, drop / 0.50) + (point.y - 0.210) * 0.30
        point.y += (hanging_y - point.y) * smooth
        point.z += max(0.0, 1.40 - point.z) * 0.45
        point.z += (0.008 * math.cos(original_x * 47.0)
                    + 0.006 * math.sin(original_x * 83.0)) * fraction ** 4
        vertex.co = inverse @ point
    hair.data.update()
    edit = bmesh.new()
    edit.from_mesh(hair.data)
    import numpy as np
    atlas = next(node.image for node in hair.data.materials[0].node_tree.nodes
                 if node.type == "TEX_IMAGE" and node.image is not None)
    width, height = atlas.size
    pixels = np.asarray(atlas.pixels[:], dtype=np.float32).reshape(height, width, 4)
    uv = edit.loops.layers.uv.active
    # Cut restrained notches between the existing broad locks. All retained
    # faces keep their original texture coordinates and strand detail.
    cut = []
    for face in edit.faces:
        point = world @ face.calc_center_median()
        trough = max(0.0, math.cos((point.x - 0.008) * 83.0)) ** 6
        cutoff = 1.16 + 0.042 * trough
        color = np.median([pixels[min(height - 1, max(0, int(loop[uv].uv.y * height))),
                                  min(width - 1, max(0, int(loop[uv].uv.x * width))), :3]
                           for loop in face.loops], axis=0)
        fused_cloth = point.z < 1.42 and color[0] < color[1] * 1.10
        if point.z < cutoff or fused_cloth:
            cut.append(face)
    if cut:
        bmesh.ops.delete(edit, geom=cut, context="FACES")
    bmesh.ops.remove_doubles(edit, verts=list(edit.verts), dist=0.00045)
    loose = [vertex for vertex in edit.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(edit, geom=loose, context="VERTS")
    pending = set(edit.verts)
    clipped_islands = []
    while pending:
        component = {pending.pop()}
        frontier = list(component)
        while frontier:
            vertex = frontier.pop()
            for edge in vertex.link_edges:
                neighbor = edge.other_vert(vertex)
                if neighbor in pending:
                    pending.remove(neighbor)
                    component.add(neighbor)
                    frontier.append(neighbor)
        if len(component) < 200 and max((world @ vertex.co).z for vertex in component) < 1.40:
            clipped_islands.extend(component)
    if clipped_islands:
        bmesh.ops.delete(edit, geom=clipped_islands, context="VERTS")
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(hair.data)
    edit.free()
    hair.data.update()
    hair["steel_tide_authored_separated_locks"] = True
