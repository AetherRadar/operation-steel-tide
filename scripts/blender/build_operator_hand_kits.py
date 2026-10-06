"""Build role-specific first-person hand kits from authored DJMaesen poses.

The visible first-person model keeps the production glove and finger mesh, but
only retains a short cuff behind each wrist. The cut is authored and capped in
Blender so the runtime never repairs or reshapes the visible mesh.

Run with Blender 4.5+:
    blender --background --factory-startup --python-exit-code 2 --python scripts/blender/build_operator_hand_kits.py
"""

from __future__ import annotations

from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "assets" / "models" / "djmaesen_smg45"
SOURCE_DIR = ROOT / "source_art" / "first_person_hands"
OUT_BLEND = SOURCE_DIR / "operator_hand_kits.blend"
OUT_GLB = OUTPUT_DIR / "operator_hand_kits.glb"

ROLE_SPECS = {
    "Viper": {
        "cuff_length": 0.105,
        "sleeve_inflate": 1.10,
        "glove_scale": 1.10,
        "sleeve": (0.18, 0.22, 0.25),
        "glove": (0.08, 0.10, 0.09),
        "band": (0.32, 0.39, 0.23),
    },
    "Heron": {
        "cuff_length": 0.100,
        "sleeve_inflate": 1.08,
        "glove_scale": 1.08,
        "sleeve": (0.31, 0.42, 0.43),
        "glove": (0.16, 0.22, 0.21),
        "band": (0.67, 0.82, 0.80),
    },
    "Lynx": {
        "cuff_length": 0.108,
        "sleeve_inflate": 1.09,
        "glove_scale": 1.09,
        "sleeve": (0.19, 0.24, 0.20),
        "glove": (0.11, 0.14, 0.12),
        "band": (0.36, 0.47, 0.27),
    },
    "Magpie": {
        "cuff_length": 0.098,
        "sleeve_inflate": 1.12,
        "glove_scale": 1.12,
        "sleeve": (0.34, 0.27, 0.16),
        "glove": (0.20, 0.15, 0.10),
        "band": (0.53, 0.41, 0.22),
    },
    "Jackal": {
        "cuff_length": 0.102,
        "sleeve_inflate": 1.09,
        "glove_scale": 1.09,
        "sleeve": (0.12, 0.14, 0.15),
        "glove": (0.07, 0.08, 0.08),
        "band": (0.38, 0.42, 0.42),
    },
}

FAMILY_SOURCES = {
    "Rifle": OUTPUT_DIR / "smg45_rifle_arms.glb",
    "PistolService": OUTPUT_DIR / "smg45_pistol_service_arms.glb",
    "PistolLarge": OUTPUT_DIR / "smg45_pistol_large_arms.glb",
    "Smg": OUTPUT_DIR / "smg45_first_person.glb",
    "Reload": OUTPUT_DIR / "animated_reload_arms.glb",
}

CAP_EPSILON = 0.00045
REMOVE_DOUBLES_EPSILON = 0.0001
HAND_BONES = {
    "L_palm_015",
    "L_thumb1_04", "L_thumb2_05", "L_thumb3_00",
    "L_point1_07", "L_point2_08", "L_point3_09",
    "L_middle1_011", "L_middle2_012", "L_middle3_013",
    "L_ring1_016", "L_ring2_017", "L_ring3_018",
    "L_pink1_020", "L_pink2_021", "L_pink3_022",
    "R_palm_039",
    "R_thumb1_028", "R_thumb2_029", "R_thumb3_030",
    "R_point1_031", "R_point2_032", "R_point3_033",
    "R_middle1_034", "R_middle2_035", "R_middle3_036",
    "R_ring1_037", "R_ring2_038", "R_ring3_040",
    "R_pink1_041", "R_pink2_042", "R_pink3_043",
}
ARMATURE_BONES = {
    "L_arm_01",
    "L_elbow_02",
    "L_wrist_03",
    "R_arm_024",
    "R_elbow_025",
    "R_wrist_026",
}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.armatures,
        bpy.data.materials,
        bpy.data.images,
        bpy.data.actions,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    return [root, *list(root.children_recursive)]


def find_object(root: bpy.types.Object, name: str) -> bpy.types.Object:
    for obj in descendants(root):
        if obj.name == name or obj.name.startswith(name + "."):
            return obj
    raise RuntimeError(f"Missing {name} below {root.name}")


def imported_root(family: str, imported: set[bpy.types.Object]) -> bpy.types.Object:
    expected = {
        "Smg": "DJMaesenSMG45FirstPerson",
        "Reload": "WeaponRoot",
    }.get(family, "StaticFirstPersonArms")
    for candidate in imported:
        if candidate.name == expected or candidate.name.startswith(expected + "."):
            return candidate
    raise RuntimeError(f"Imported asset is missing {expected}")


def remove_import_noise(
    root: bpy.types.Object,
    imported: set[bpy.types.Object],
) -> None:
    keep = set(descendants(root))
    for obj in list(imported):
        if obj not in keep and obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)


def copy_material(
    original: bpy.types.Material,
    name: str,
    tint: tuple[float, float, float],
    roughness: float,
) -> bpy.types.Material:
    material = original.copy()
    material.name = name
    if material.node_tree is None:
        return material
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    bsdf = next(
        (node for node in nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if bsdf is None:
        return material
    base_color = bsdf.inputs["Base Color"]
    if base_color.links:
        source_socket = base_color.links[0].from_socket
        for link in list(base_color.links):
            links.remove(link)
        mix = nodes.new("ShaderNodeMixRGB")
        mix.name = f"{name}Tint"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        mix.inputs[2].default_value = (*tint, 1.0)
        links.new(source_socket, mix.inputs[1])
        links.new(mix.outputs[0], base_color)
    else:
        base_color.default_value = (*tint, 1.0)
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = roughness
    return material


def material_variant(
    role: str,
    mesh: bpy.types.Object,
    spec: dict[str, object],
    glove_polygons: set[int] | None = None,
) -> None:
    if not hasattr(mesh.data, "materials") or not mesh.data.materials:
        return
    if glove_polygons is None:
        if len(mesh.data.materials) < 2:
            glove_polygons = set()
        else:
            glove_polygons = {
                polygon.index
                for polygon in mesh.data.polygons
                if polygon.material_index != 0
            }
    sleeve_source = mesh.data.materials[0]
    glove_source = (
        mesh.data.materials[1]
        if len(mesh.data.materials) > 1
        else sleeve_source
    )
    mesh.data.materials.clear()
    mesh.data.materials.append(
        copy_material(
            sleeve_source,
            f"{role}HandsSleeve",
            spec["sleeve"],
            0.74,
        )
    )
    mesh.data.materials.append(
        copy_material(
            glove_source,
            f"{role}HandsGlove",
            spec["glove"],
            0.58,
        )
    )
    mesh.data.materials.append(
        copy_material(
            glove_source,
            f"{role}HandsBand",
            spec["band"],
            0.46,
        )
    )
    for polygon in mesh.data.polygons:
        polygon.material_index = 1 if polygon.index in glove_polygons else 0


def smoothstep(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def soften_normals(mesh: bpy.types.Object) -> None:
    for polygon in mesh.data.polygons:
        polygon.use_smooth = True
    if any(modifier.type == "WEIGHTED_NORMAL" for modifier in mesh.modifiers):
        return
    modifier = mesh.modifiers.new("AuthoredHandWeightedNormals", "WEIGHTED_NORMAL")
    modifier.keep_sharp = True
    modifier.weight = 45


def vertex_group_weights(
    mesh: bpy.types.Object,
) -> dict[int, float]:
    group_names = {
        group.index: group.name
        for group in mesh.vertex_groups
    }
    weights: dict[int, float] = {}
    for vertex in mesh.data.vertices:
        weights[vertex.index] = sum(
            assignment.weight
            for assignment in vertex.groups
            if group_names.get(assignment.group) in HAND_BONES
        )
    return weights


def weighted_glove_polygons(
    mesh: bpy.types.Object,
    threshold: float = 0.30,
) -> set[int]:
    hand_weights = vertex_group_weights(mesh)
    return {
        polygon.index
        for polygon in mesh.data.polygons
        if sum(
            hand_weights.get(vertex_index, 0.0)
            for vertex_index in polygon.vertices
        ) / max(1, len(polygon.vertices)) >= threshold
    }


def connected_components(bm: bmesh.types.BMesh) -> list[set[bmesh.types.BMVert]]:
    bm.verts.ensure_lookup_table()
    unseen = set(bm.verts)
    components: list[set[bmesh.types.BMVert]] = []
    while unseen:
        seed = min(unseen, key=lambda vertex: vertex.index)
        unseen.remove(seed)
        component = {seed}
        stack = [seed]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                neighbor = edge.other_vert(vertex)
                if neighbor in unseen:
                    unseen.remove(neighbor)
                    component.add(neighbor)
                    stack.append(neighbor)
        components.append(component)
    return components


def ordered_boundary_loops(
    edges: list[bmesh.types.BMEdge],
) -> list[list[bmesh.types.BMVert]]:
    adjacency: dict[bmesh.types.BMVert, list[bmesh.types.BMVert]] = {}
    for edge in edges:
        left, right = edge.verts
        adjacency.setdefault(left, []).append(right)
        adjacency.setdefault(right, []).append(left)
    remaining = {
        (min(left.index, right.index), max(left.index, right.index)): (left, right)
        for left, neighbors in adjacency.items()
        for right in neighbors
    }
    loops: list[list[bmesh.types.BMVert]] = []
    while remaining:
        _, (start, current) = min(remaining.items())
        remaining.pop((min(start.index, current.index), max(start.index, current.index)))
        loop = [start, current]
        previous = start
        closed = False
        while True:
            choices = [
                neighbor
                for neighbor in adjacency[current]
                if neighbor is not previous
                and (
                    min(current.index, neighbor.index),
                    max(current.index, neighbor.index),
                )
                in remaining
            ]
            if not choices:
                break
            next_vertex = min(choices, key=lambda vertex: vertex.index)
            remaining.pop(
                (
                    min(current.index, next_vertex.index),
                    max(current.index, next_vertex.index),
                )
            )
            if next_vertex is start:
                closed = True
                break
            loop.append(next_vertex)
            previous, current = current, next_vertex
        if closed and len(loop) >= 3:
            loops.append(loop)
    return loops


def cap_cut_loops(
    bm: bmesh.types.BMesh,
    plane_co: Vector,
    plane_no: Vector,
    material_index: int = 0,
) -> int:
    boundary = [
        edge
        for edge in bm.edges
        if len(edge.link_faces) == 1
        and all(
            abs((vertex.co - plane_co).dot(plane_no)) <= CAP_EPSILON
            for vertex in edge.verts
        )
    ]
    caps: list[bmesh.types.BMFace] = []
    for loop in ordered_boundary_loops(boundary):
        try:
            face = bm.faces.new(loop)
        except ValueError:
            continue
        face.material_index = material_index
        face.smooth = True
        caps.append(face)
    if caps:
        bmesh.ops.triangulate(
            bm,
            faces=caps,
            quad_method="BEAUTY",
            ngon_method="BEAUTY",
        )
    return len(caps)


def trim_mesh_to_wrist(
    mesh: bpy.types.Object,
    wrist: Vector,
    palm: Vector,
    cuff_length: float,
) -> None:
    inverse = mesh.matrix_world.inverted()
    axis_world = (palm - wrist).normalized()
    plane_world = wrist - axis_world * cuff_length
    plane_co = inverse @ plane_world
    plane_no = (inverse.to_3x3() @ axis_world).normalized()
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh.data)
        geometry = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(
            bm,
            geom=geometry,
            plane_co=plane_co,
            plane_no=plane_no,
            dist=0.00001,
            clear_inner=True,
            clear_outer=False,
        )
        bmesh.ops.remove_doubles(
            bm,
            verts=list(bm.verts),
            dist=REMOVE_DOUBLES_EPSILON,
        )
        bm.verts.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
        caps = cap_cut_loops(bm, plane_co, plane_no)
        if caps == 0:
            raise RuntimeError(
                f"{mesh.name} wrist crop did not produce a closed cuff cap"
            )
        bm.normal_update()
        bm.to_mesh(mesh.data)
        mesh.data.update()
    finally:
        bm.free()


def shape_static_arm(
    mesh: bpy.types.Object,
    wrist: Vector,
    palm: Vector,
    spec: dict[str, object],
    role: str,
) -> None:
    sleeve_vertices, glove_vertices = mesh_vertex_material_indices(mesh.data)
    if not sleeve_vertices or not glove_vertices:
        raise RuntimeError(f"{mesh.name} lost its sleeve/glove material split")
    axis = (palm - wrist).normalized()
    inverse = mesh.matrix_world.inverted()
    glove_scale = float(spec["glove_scale"])
    sleeve_inflate = float(spec["sleeve_inflate"])
    cuff_length = float(spec["cuff_length"])
    for vertex in mesh.data.vertices:
        point = mesh.matrix_world @ vertex.co
        if vertex.index in sleeve_vertices:
            offset = point - wrist
            along = axis * offset.dot(axis)
            radial = offset - along
            distance_from_wrist = max(0.0, -offset.dot(axis))
            cuff_blend = smoothstep(
                distance_from_wrist / max(cuff_length, 0.001)
            )
            radius_scale = 1.07 + (sleeve_inflate - 1.07) * cuff_blend
            point = wrist + along + radial * radius_scale
        else:
            hand_offset = point - palm
            hand_axis = axis * hand_offset.dot(axis)
            hand_radial = hand_offset - hand_axis
            point = palm + hand_axis * 1.06 + hand_radial * glove_scale
        vertex.co = inverse @ point
    mesh.data.update()
    material_variant(role, mesh, spec)
    assign_cuff_band(mesh, wrist, axis, cuff_length)
    soften_normals(mesh)


def mesh_vertex_material_indices(
    mesh: bpy.types.Mesh,
) -> tuple[set[int], set[int]]:
    sleeve: set[int] = set()
    glove: set[int] = set()
    for polygon in mesh.polygons:
        target = sleeve if polygon.material_index == 0 else glove
        target.update(polygon.vertices)
    return sleeve, glove


def assign_cuff_band(
    mesh: bpy.types.Object,
    wrist: Vector,
    axis: Vector,
    cuff_length: float,
) -> None:
    if len(mesh.data.materials) < 3:
        return
    for polygon in mesh.data.polygons:
        center = mesh.matrix_world @ polygon.center
        along = (center - wrist).dot(axis)
        if -cuff_length <= along <= -cuff_length * 0.46:
            polygon.material_index = 2


def deform_static_arms(root: bpy.types.Object, role: str) -> None:
    spec = dict(ROLE_SPECS[role])
    for side in ("Left", "Right"):
        mesh = find_object(root, f"{side}ArmMesh")
        wrist = find_object(root, f"{side}WristFrame").matrix_world.translation
        palm = find_object(root, f"{side}PalmFrame").matrix_world.translation
        trim_mesh_to_wrist(
            mesh,
            wrist,
            palm,
            float(spec["cuff_length"]),
        )
        shape_static_arm(mesh, wrist, palm, spec, role)


def deform_animated_smg(root: bpy.types.Object, role: str) -> None:
    spec = dict(ROLE_SPECS[role])
    arms = find_object(root, "AuthoredArms")
    armature = next(
        (obj for obj in descendants(root) if obj.type == "ARMATURE"),
        None,
    )
    if armature is None:
        raise RuntimeError(f"{role} animated SMG kit is missing its armature")
    left_wrist = armature.matrix_world @ armature.data.bones["L_wrist_03"].head_local
    left_palm = armature.matrix_world @ armature.data.bones["L_palm_015"].head_local
    trim_mesh_to_wrist(arms, left_wrist, left_palm, float(spec["cuff_length"]))
    material_variant(role, arms, spec)
    soften_normals(arms)
    for obj in descendants(root):
        if obj.animation_data is None:
            continue
        for track in obj.animation_data.nla_tracks:
            track.name = "reload"
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = 240


def deform_animated_reload(root: bpy.types.Object, role: str) -> None:
    """Inflate and shorten the skinned moving crops while preserving the rig."""
    spec = dict(ROLE_SPECS[role])
    armature = next(
        (obj for obj in descendants(root) if obj.type == "ARMATURE"),
        None,
    )
    if armature is None:
        raise RuntimeError(f"{role} reload kit is missing its armature")

    def rest_bone_point(name: str, head: bool) -> Vector:
        bone = armature.data.bones[name]
        point = bone.head_local if head else bone.tail_local
        return armature.matrix_world @ point

    wrist = rest_bone_point("L_wrist_03", True)
    elbow = rest_bone_point("L_elbow_02", True)
    palm = rest_bone_point("L_palm_015", True)
    forearm_axis = (wrist - elbow).normalized()
    palm_axis = (palm - wrist).normalized()
    cuff_length = float(spec["cuff_length"])
    sleeve_inflate = float(spec["sleeve_inflate"])
    glove_scale = float(spec["glove_scale"])

    audit_mesh = find_object(root, "FullReloadArmsAuditMesh")
    shape_reload_mesh(
        audit_mesh,
        wrist,
        palm,
        forearm_axis,
        palm_axis,
        cuff_length,
        sleeve_inflate,
        glove_scale,
        role,
        spec,
    )

    # The source crop meshes were cut farther up the sleeve. Re-cut both
    # runtime crops from the shaped full audit mesh so the short cuff has one
    # clean, closed Blender-authored boundary instead of bisecting an older
    # cap and leaving a two-point seam.
    for name in (
        "LongGunReloadForearmsMesh",
        "SidearmReloadForearmsMesh",
    ):
        mesh = find_object(root, name)
        mesh.data = audit_mesh.data.copy()
        mesh.data.name = f"{name}Data"
        delete_right_arm_geometry(mesh)
        trim_mesh_to_wrist(mesh, wrist, palm, cuff_length)
        hand_weights = vertex_group_weights(mesh)
        glove_polygons = {
            polygon.index
            for polygon in mesh.data.polygons
            if sum(
                hand_weights.get(vertex_index, 0.0)
                for vertex_index in polygon.vertices
            ) / max(1, len(polygon.vertices)) >= 0.30
        }
        material_variant(role, mesh, spec, glove_polygons)
        assign_cuff_band(mesh, wrist, forearm_axis, cuff_length)
        soften_normals(mesh)


def shape_reload_mesh(
    mesh: bpy.types.Object,
    wrist: Vector,
    palm: Vector,
    forearm_axis: Vector,
    palm_axis: Vector,
    cuff_length: float,
    sleeve_inflate: float,
    glove_scale: float,
    role: str,
    spec: dict[str, object],
) -> None:
    hand_weights = vertex_group_weights(mesh)
    inverse = mesh.matrix_world.inverted()
    for vertex in mesh.data.vertices:
        point = mesh.matrix_world @ vertex.co
        hand_weight = hand_weights.get(vertex.index, 0.0)
        if hand_weight >= 0.30:
            offset = point - palm
            axial = palm_axis * offset.dot(palm_axis)
            radial = offset - axial
            point = palm + axial * 1.06 + radial * glove_scale
        else:
            offset = point - wrist
            axial = forearm_axis * offset.dot(forearm_axis)
            radial = offset - axial
            distance_from_wrist = max(0.0, -offset.dot(forearm_axis))
            cuff_blend = smoothstep(
                distance_from_wrist / max(cuff_length, 0.001)
            )
            radius_scale = 1.07 + (sleeve_inflate - 1.07) * cuff_blend
            point = wrist + axial + radial * radius_scale
        vertex.co = inverse @ point
    mesh.data.update()
    glove_polygons = {
        polygon.index
        for polygon in mesh.data.polygons
        if sum(
            hand_weights.get(vertex_index, 0.0)
            for vertex_index in polygon.vertices
        ) / max(1, len(polygon.vertices)) >= 0.30
    }
    material_variant(role, mesh, spec, glove_polygons)
    assign_cuff_band(mesh, wrist, forearm_axis, cuff_length)
    soften_normals(mesh)


def delete_right_arm_geometry(mesh: bpy.types.Object) -> None:
    left_groups = {
        group.index
        for group in mesh.vertex_groups
        if group.name.startswith("L_")
    }
    right_groups = {
        group.index
        for group in mesh.vertex_groups
        if group.name.startswith("R_")
    }
    right_vertices = []
    for vertex in mesh.data.vertices:
        left_weight = sum(
            assignment.weight
            for assignment in vertex.groups
            if assignment.group in left_groups
        )
        right_weight = sum(
            assignment.weight
            for assignment in vertex.groups
            if assignment.group in right_groups
        )
        if right_weight > left_weight:
            right_vertices.append(vertex.index)

    if not right_vertices:
        raise RuntimeError(
            f"{mesh.name} reload crop did not find right-arm geometry"
        )

    bm = bmesh.new()
    try:
        bm.from_mesh(mesh.data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(
            bm,
            geom=[bm.verts[index] for index in right_vertices],
            context="VERTS",
        )
        bm.normal_update()
        bm.to_mesh(mesh.data)
        mesh.data.update()
    finally:
        bm.free()


def prefix_tree(root: bpy.types.Object, role: str) -> None:
    prefix = role + "__"
    for obj in descendants(root):
        base_name = obj.name.split(".", 1)[0]
        obj.name = prefix + base_name


def import_family(
    family: str,
    role: str,
    role_root: bpy.types.Object,
) -> bpy.types.Object:
    path = FAMILY_SOURCES[family]
    if not path.exists():
        raise FileNotFoundError(path)
    existing = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = set(bpy.context.scene.objects) - existing
    source_root = imported_root(family, imported)
    remove_import_noise(source_root, imported)
    source_root.name = family
    source_root.parent = role_root
    if family == "Smg":
        deform_animated_smg(source_root, role)
    elif family == "Reload":
        deform_animated_reload(source_root, role)
    else:
        deform_static_arms(source_root, role)
    # Reload animation paths are authored against the exact node names in the
    # arms-only source. Keep that subtree unprefixed; it is extracted as one
    # role-owned family at runtime, so sibling role kits cannot collide.
    if family != "Reload":
        prefix_tree(source_root, role)
    return source_root


def build_ladder_variant(
    role: str,
    role_root: bpy.types.Object,
) -> bpy.types.Object:
    source_path = FAMILY_SOURCES["Rifle"]
    existing = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    imported = set(bpy.context.scene.objects) - existing
    source_root = imported_root("Rifle", imported)
    remove_import_noise(source_root, imported)
    source_root.name = "Ladder"
    source_root.parent = role_root
    deform_static_arms(source_root, role)
    for side, target in (
        ("Left", Vector((-0.24, 0.04, -0.04))),
        ("Right", Vector((0.24, 0.04, -0.25))),
    ):
        arm = find_object(source_root, f"{side}Arm")
        palm = find_object(source_root, f"{side}PalmFrame").matrix_world.translation
        arm.location += target - palm
        arm.rotation_euler.z += 0.10 if side == "Left" else -0.10
    prefix_tree(source_root, role)
    return source_root


def main() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    clear_scene()
    scene_root = bpy.data.objects.new("OperatorFirstPersonHandKits", None)
    bpy.context.scene.collection.objects.link(scene_root)

    for role in ROLE_SPECS:
        role_root = bpy.data.objects.new(role, None)
        bpy.context.scene.collection.objects.link(role_root)
        role_root.parent = scene_root
        for family in (
            "Rifle",
            "PistolService",
            "PistolLarge",
            "Smg",
            "Reload",
        ):
            import_family(family, role, role_root)
        build_ladder_variant(role, role_root)

    bpy.ops.object.select_all(action="DESELECT")
    scene_root.select_set(True)
    for child in scene_root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = scene_root
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_frame_range=True,
        export_force_sampling=True,
        export_animation_mode="NLA_TRACKS",
        export_nla_strips_merged_animation_name="reload",
        export_cameras=False,
        export_lights=False,
        export_apply=False,
        export_image_format="AUTO",
        export_yup=True,
    )
    print(f"OPERATOR_HAND_KITS_PASS blend={OUT_BLEND} glb={OUT_GLB}")


if __name__ == "__main__":
    main()
