"""Author and validate the Guangchang clan-hall double-gate portal.

The packed Chinese Temple mesh contains a baked static arched gate as 56
disconnected islands: two leaf surfaces, six centre-seam surfaces, and 48
small pull/handle islands.  This module removes only those measured islands,
retains the authored jamb/lintel/threshold geometry, and exports a stable
Empty whose glTF extras drive the runtime double-hinged door.
"""

from __future__ import annotations

import json
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


CLAN_HALL_OBJECT_NAME = "GuangchangClanHall"
CLAN_HALL_GATE_ANCHOR_NAME = "JianghaiClanHallDoubleGateAnchor"
CLAN_HALL_GATE_REMOVAL_VERSION = 1
CLAN_HALL_GATE_REMOVED_COMPONENTS = 56
CLAN_HALL_GATE_REMOVED_VERTICES = 425
CLAN_HALL_GATE_REMOVED_TRIANGLES = 323
CLAN_HALL_GATE_WIDTH_METERS = 3.6878103065801326
CLAN_HALL_GATE_HEIGHT_METERS = 4.0285267088284655
CLAN_HALL_GATE_FLOOR_CENTER_BLENDER = Vector(
    (-86.00189208984375, 122.5762710571289, 1.2787764072418213)
)
CLAN_HALL_GATE_FLOOR_CENTER_GODOT = Vector(
    (-86.00189208984375, 1.2787764072418213, -122.5762710571289)
)
CLAN_HALL_GATE_OUTER_LOCAL_MINIMUM = Vector(
    (-1.1682264804840088, -3.4327406883239746, 0.6954280138015747)
)
CLAN_HALL_GATE_OUTER_LOCAL_MAXIMUM = Vector(
    (1.1658306121826172, -3.4327406883239746, 3.2451283931732178)
)
CLAN_HALL_GATE_INNER_LOCAL_MINIMUM = Vector(
    (-0.834998607635498, -3.316740036010742, 0.8456979393959045)
)
CLAN_HALL_GATE_INNER_LOCAL_MAXIMUM = Vector(
    (0.8320126533508301, -3.316740036010742, 2.9118804931640625)
)
CLAN_HALL_GATE_FINAL_VERTICES = 386_138
CLAN_HALL_GATE_FINAL_POLYGONS = 197_003


def _bounds_match(
    minimum: Vector,
    maximum: Vector,
    expected_minimum: Vector,
    expected_maximum: Vector,
    tolerance: float = 0.00005,
) -> bool:
    return (
        (minimum - expected_minimum).length <= tolerance
        and (maximum - expected_maximum).length <= tolerance
    )


def _component_sets(
    editable: bmesh.types.BMesh,
) -> dict[str, list[tuple[list[bmesh.types.BMVert], set[bmesh.types.BMFace]]]]:
    """Identify only the measured static leaf islands and their loose hardware."""

    editable.verts.ensure_lookup_table()
    visited: set[int] = set()
    components: list[tuple[list[bmesh.types.BMVert], set[bmesh.types.BMFace]]] = []
    seeds = [
        vertex
        for vertex in editable.verts
        if -1.25 <= vertex.co.x <= 1.25
        and -3.45 <= vertex.co.y <= -3.29
        and 0.60 <= vertex.co.z <= 3.40
    ]
    for seed in seeds:
        if seed.index in visited:
            continue
        pending = [seed]
        visited.add(seed.index)
        vertices: list[bmesh.types.BMVert] = []
        while pending:
            vertex = pending.pop()
            vertices.append(vertex)
            for edge in vertex.link_edges:
                adjacent = edge.other_vert(vertex)
                if adjacent.index in visited:
                    continue
                visited.add(adjacent.index)
                pending.append(adjacent)
        faces = {face for vertex in vertices for face in vertex.link_faces}
        components.append((vertices, faces))

    classified = {"outer": [], "inner": [], "seam": [], "hardware": []}
    for vertices, faces in components:
        minimum = Vector(
            tuple(min(vertex.co[axis] for vertex in vertices) for axis in range(3))
        )
        maximum = Vector(
            tuple(max(vertex.co[axis] for vertex in vertices) for axis in range(3))
        )
        materials = {face.material_index for face in faces}
        triangles = sum(max(len(face.verts) - 2, 0) for face in faces)
        if (
            len(vertices) == 34
            and len(faces) == 32
            and triangles == 32
            and materials == {1}
            and _bounds_match(
                minimum,
                maximum,
                CLAN_HALL_GATE_OUTER_LOCAL_MINIMUM,
                CLAN_HALL_GATE_OUTER_LOCAL_MAXIMUM,
            )
        ):
            classified["outer"].append((vertices, faces))
        elif (
            len(vertices) == 17
            and len(faces) == 15
            and triangles == 15
            and materials == {2}
            and _bounds_match(
                minimum,
                maximum,
                CLAN_HALL_GATE_INNER_LOCAL_MINIMUM,
                CLAN_HALL_GATE_INNER_LOCAL_MAXIMUM,
            )
        ):
            classified["inner"].append((vertices, faces))
        elif (
            len(vertices) == 4
            and len(faces) == 2
            and triangles == 2
            and materials == {2}
            and minimum.x >= -0.006
            and maximum.x <= 0.006
            and minimum.y >= -3.326
            and maximum.y <= -3.316
            and minimum.z >= 0.845
            and maximum.z <= 2.913
        ):
            classified["seam"].append((vertices, faces))
        elif (
            materials == {1}
            and minimum.x >= -0.36
            and maximum.x <= 0.36
            and minimum.y >= -3.37
            and maximum.y <= -3.30
            and minimum.z >= 1.70
            and maximum.z <= 2.00
        ):
            classified["hardware"].append((vertices, faces))
    return classified


def _component_totals(
    classified: dict[
        str,
        list[tuple[list[bmesh.types.BMVert], set[bmesh.types.BMFace]]],
    ],
) -> tuple[int, int, int]:
    components = [component for values in classified.values() for component in values]
    vertices = {vertex for component, _ in components for vertex in component}
    faces = {face for _, component_faces in components for face in component_faces}
    triangles = sum(max(len(face.verts) - 2, 0) for face in faces)
    return len(components), len(vertices), triangles


def author_clan_hall_gate_portal() -> dict[str, int]:
    hall = bpy.data.objects.get(CLAN_HALL_OBJECT_NAME)
    if hall is None or hall.type != "MESH":
        raise RuntimeError(f"Clan hall source is missing: {CLAN_HALL_OBJECT_NAME}")
    if hall.data.users != 1:
        raise RuntimeError(
            f"Clan hall source mesh must be unique before its door edit: {hall.data.users}"
        )

    editable = bmesh.new()
    editable.from_mesh(hall.data)
    classified = _component_sets(editable)
    component_count, vertex_count, triangle_count = _component_totals(classified)
    marker = hall.data.get("jianghai_clan_hall_gate_removal_version")
    if marker == CLAN_HALL_GATE_REMOVAL_VERSION:
        if component_count or vertex_count or triangle_count:
            editable.free()
            raise RuntimeError("Static clan-hall gate geometry returned after retirement")
        removed = (0, 0, 0)
    else:
        category_counts = {name: len(values) for name, values in classified.items()}
        hardware_vertices = sum(
            len(vertices) for vertices, _ in classified["hardware"]
        )
        hardware_triangles = sum(
            max(len(face.verts) - 2, 0)
            for _, faces in classified["hardware"]
            for face in faces
        )
        if (
            category_counts != {"outer": 1, "inner": 1, "seam": 6, "hardware": 48}
            or hardware_vertices != 350
            or hardware_triangles != 264
            or component_count != CLAN_HALL_GATE_REMOVED_COMPONENTS
            or vertex_count != CLAN_HALL_GATE_REMOVED_VERTICES
            or triangle_count != CLAN_HALL_GATE_REMOVED_TRIANGLES
        ):
            editable.free()
            raise RuntimeError(
                "Clan-hall static gate island contract drifted: "
                f"categories={category_counts} components={component_count} "
                f"vertices={vertex_count} triangles={triangle_count} "
                f"hardware={hardware_vertices}:{hardware_triangles}"
            )
        removal_vertices = {
            vertex
            for values in classified.values()
            for vertices, _ in values
            for vertex in vertices
        }
        bmesh.ops.delete(editable, geom=list(removal_vertices), context="VERTS")
        editable.to_mesh(hall.data)
        hall.data.validate(verbose=False)
        hall.data.update()
        removed = (component_count, vertex_count, triangle_count)
    editable.free()

    hall.data["jianghai_clan_hall_gate_removal_version"] = (
        CLAN_HALL_GATE_REMOVAL_VERSION
    )
    hall.data["jianghai_clan_hall_removed_gate_components"] = (
        CLAN_HALL_GATE_REMOVED_COMPONENTS
    )
    hall.data["jianghai_clan_hall_removed_gate_vertices"] = (
        CLAN_HALL_GATE_REMOVED_VERTICES
    )
    hall.data["jianghai_clan_hall_removed_gate_triangles"] = (
        CLAN_HALL_GATE_REMOVED_TRIANGLES
    )
    hall.data["jianghai_clan_hall_retained_portal_structure"] = (
        "arched lintel, side jambs, and threshold"
    )
    if (
        len(hall.data.vertices) != CLAN_HALL_GATE_FINAL_VERTICES
        or len(hall.data.polygons) != CLAN_HALL_GATE_FINAL_POLYGONS
    ):
        raise RuntimeError(
            "Clan-hall mesh count drifted after exact static-gate removal: "
            f"vertices={len(hall.data.vertices)} polygons={len(hall.data.polygons)}"
        )

    anchor_candidates = sorted(
        (
            obj
            for obj in bpy.data.objects
            if obj.name.startswith(CLAN_HALL_GATE_ANCHOR_NAME)
        ),
        key=lambda obj: obj.name,
    )
    if anchor_candidates and (
        len(anchor_candidates) != 1
        or anchor_candidates[0].name != CLAN_HALL_GATE_ANCHOR_NAME
    ):
        raise RuntimeError(
            "Clan-hall gate anchor prefix is occupied by a non-canonical set: "
            f"{[obj.name for obj in anchor_candidates]}"
        )
    anchor = bpy.data.objects.get(CLAN_HALL_GATE_ANCHOR_NAME)
    if anchor is None:
        anchor = bpy.data.objects.new(CLAN_HALL_GATE_ANCHOR_NAME, None)
        bpy.context.scene.collection.objects.link(anchor)
    if anchor.type != "EMPTY":
        raise RuntimeError("Clan-hall double-gate anchor must be an authored Empty")
    anchor.parent = hall.parent
    anchor.matrix_world = Matrix.Translation(CLAN_HALL_GATE_FLOOR_CENTER_BLENDER)
    anchor.empty_display_type = "ARROWS"
    anchor.empty_display_size = 1.0
    anchor["gate_contract_version"] = CLAN_HALL_GATE_REMOVAL_VERSION
    anchor["gate_width_m"] = CLAN_HALL_GATE_WIDTH_METERS
    anchor["gate_height_m"] = CLAN_HALL_GATE_HEIGHT_METERS
    anchor["gate_floor_y_m"] = CLAN_HALL_GATE_FLOOR_CENTER_GODOT.y
    anchor["gate_outward_axis"] = "+Z"
    anchor["gate_blender_outward_axis"] = "-Y"
    anchor["gate_tangent_axis"] = "+X"
    anchor["gate_up_axis"] = "+Y"
    anchor["gate_source_object"] = CLAN_HALL_OBJECT_NAME
    anchor["gate_source_mesh"] = hall.data.name
    anchor["gate_removed_component_count"] = CLAN_HALL_GATE_REMOVED_COMPONENTS
    anchor["gate_removed_vertex_count"] = CLAN_HALL_GATE_REMOVED_VERTICES
    anchor["gate_removed_triangle_count"] = CLAN_HALL_GATE_REMOVED_TRIANGLES
    anchor["gate_godot_position"] = tuple(CLAN_HALL_GATE_FLOOR_CENTER_GODOT)
    anchor["gate_godot_basis_contract"] = (
        "+X tangent, +Y up, +Z outward toward the south street"
    )
    bpy.context.view_layer.update()
    return {
        "removed_components": removed[0],
        "removed_vertices": removed[1],
        "removed_triangles": removed[2],
    }


def validate_clan_hall_gate_portal() -> dict[str, int | bool]:
    hall = bpy.data.objects.get(CLAN_HALL_OBJECT_NAME)
    anchor = bpy.data.objects.get(CLAN_HALL_GATE_ANCHOR_NAME)
    anchor_candidates = sorted(
        (
            obj
            for obj in bpy.data.objects
            if obj.name.startswith(CLAN_HALL_GATE_ANCHOR_NAME)
        ),
        key=lambda obj: obj.name,
    )
    if hall is None or hall.type != "MESH":
        raise RuntimeError("Clan-hall authored portal source is missing")
    if (
        len(anchor_candidates) != 1
        or anchor is None
        or anchor_candidates[0] is not anchor
        or anchor.name != CLAN_HALL_GATE_ANCHOR_NAME
    ):
        raise RuntimeError(
            "Clan-hall gate anchor prefix must resolve to the sole exact anchor: "
            f"{[obj.name for obj in anchor_candidates]}"
        )
    if anchor.type != "EMPTY":
        raise RuntimeError("Clan-hall double-gate anchor must be an authored Empty")
    editable = bmesh.new()
    editable.from_mesh(hall.data)
    classified = _component_sets(editable)
    component_count, vertex_count, triangle_count = _component_totals(classified)
    editable.free()

    matrix = anchor.matrix_world
    basis = matrix.to_3x3()
    anchor_ready = (
        len(anchor_candidates) == 1
        and anchor_candidates[0] is anchor
        and (matrix.translation - CLAN_HALL_GATE_FLOOR_CENTER_BLENDER).length
        <= 0.00005
        and all(
            abs(basis[row][column] - (1.0 if row == column else 0.0)) <= 0.00001
            for row in range(3)
            for column in range(3)
        )
        and abs(float(anchor.get("gate_width_m", 0.0)) - CLAN_HALL_GATE_WIDTH_METERS)
        <= 0.00001
        and abs(float(anchor.get("gate_height_m", 0.0)) - CLAN_HALL_GATE_HEIGHT_METERS)
        <= 0.00001
        and anchor.get("gate_outward_axis") == "+Z"
        and anchor.get("gate_blender_outward_axis") == "-Y"
    )

    inverse = hall.matrix_world.inverted()
    world_direction = Vector((0.0, 1.0, 0.0))

    def ray_hits(x: float, z: float) -> bool:
        world_origin = Vector(
            (x, CLAN_HALL_GATE_FLOOR_CENTER_BLENDER.y - 0.75, z)
        )
        world_destination = world_origin + world_direction * 2.5
        local_origin = inverse @ world_origin
        local_destination = inverse @ world_destination
        local_delta = local_destination - local_origin
        hit, _, _, _ = hall.ray_cast(
            local_origin,
            local_delta.normalized(),
            distance=local_delta.length,
        )
        return hit

    center = CLAN_HALL_GATE_FLOOR_CENTER_BLENDER
    opening_hits = [
        ray_hits(
            center.x + CLAN_HALL_GATE_WIDTH_METERS * x_fraction,
            center.z + CLAN_HALL_GATE_HEIGHT_METERS * height_fraction,
        )
        for x_fraction in (-0.32, 0.0, 0.32)
        for height_fraction in (0.15, 0.45, 0.75)
    ]
    jamb_hits = [
        ray_hits(
            center.x + side * (CLAN_HALL_GATE_WIDTH_METERS * 0.5 + 0.32),
            center.z + CLAN_HALL_GATE_HEIGHT_METERS * height_fraction,
        )
        for side in (-1.0, 1.0)
        for height_fraction in (0.20, 0.50, 0.80)
    ]
    lintel_hits = [
        ray_hits(
            center.x + CLAN_HALL_GATE_WIDTH_METERS * x_fraction,
            center.z + CLAN_HALL_GATE_HEIGHT_METERS + 0.30,
        )
        for x_fraction in (-0.25, 0.0, 0.25)
    ]
    threshold_hits = [
        ray_hits(
            center.x + CLAN_HALL_GATE_WIDTH_METERS * x_fraction,
            center.z + 0.12,
        )
        for x_fraction in (-0.30, 0.0, 0.30)
    ]
    valid = (
        component_count == 0
        and vertex_count == 0
        and triangle_count == 0
        and anchor_ready
        and not any(opening_hits)
        and all(jamb_hits)
        and all(lintel_hits)
        and all(threshold_hits)
        and hall.data.get("jianghai_clan_hall_gate_removal_version")
        == CLAN_HALL_GATE_REMOVAL_VERSION
        and hall.data.get("jianghai_clan_hall_removed_gate_components")
        == CLAN_HALL_GATE_REMOVED_COMPONENTS
        and hall.data.get("jianghai_clan_hall_removed_gate_vertices")
        == CLAN_HALL_GATE_REMOVED_VERTICES
        and hall.data.get("jianghai_clan_hall_removed_gate_triangles")
        == CLAN_HALL_GATE_REMOVED_TRIANGLES
    )
    print(
        "JIANGHAI_CLAN_HALL_PORTAL_CHECK "
        f"valid={valid} static_gate={component_count}:{vertex_count}:{triangle_count} "
        f"retired={CLAN_HALL_GATE_REMOVED_COMPONENTS}:"
        f"{CLAN_HALL_GATE_REMOVED_VERTICES}:"
        f"{CLAN_HALL_GATE_REMOVED_TRIANGLES} "
        f"aperture_clear={sum(not hit for hit in opening_hits)}/9 "
        f"jambs={sum(jamb_hits)}/6 lintel={sum(lintel_hits)}/3 "
        f"threshold={sum(threshold_hits)}/3 anchor={anchor_ready}:"
        f"prefix={len(anchor_candidates)}/1 "
        f"blender_position={tuple(round(value, 6) for value in center)} "
        f"godot_position={tuple(round(value, 6) for value in CLAN_HALL_GATE_FLOOR_CENTER_GODOT)} "
        f"size={CLAN_HALL_GATE_WIDTH_METERS:.6f}x"
        f"{CLAN_HALL_GATE_HEIGHT_METERS:.6f} outward=+Z"
    )
    if not valid:
        raise RuntimeError("Clan-hall authored double-gate portal validation failed")
    return {
        "static_components": component_count,
        "static_vertices": vertex_count,
        "static_triangles": triangle_count,
        "aperture_clear": sum(not hit for hit in opening_hits),
        "jamb_hits": sum(jamb_hits),
        "lintel_hits": sum(lintel_hits),
        "threshold_hits": sum(threshold_hits),
        "anchor_ready": anchor_ready,
        "anchor_prefix_count": len(anchor_candidates),
    }


def validate_clan_hall_gate_glb(glb_path: Path) -> dict[str, int | bool]:
    """Lock the exact glTF/Godot transform and exported extras contract."""

    data = glb_path.read_bytes()
    if data[:4] != b"glTF" or int.from_bytes(data[4:8], "little") != 2:
        raise RuntimeError(f"Clan-hall anchor target is not GLB 2.0: {glb_path}")
    json_length = int.from_bytes(data[12:16], "little")
    if data[16:20] != b"JSON":
        raise RuntimeError("Clan-hall runtime GLB has no JSON chunk")
    document = json.loads(data[20 : 20 + json_length].decode("utf-8"))
    nodes = document.get("nodes", [])
    prefix_matches = [
        (index, node)
        for index, node in enumerate(nodes)
        if node.get("name", "").startswith(CLAN_HALL_GATE_ANCHOR_NAME)
    ]
    matches = [
        (index, node)
        for index, node in enumerate(nodes)
        if node.get("name") == CLAN_HALL_GATE_ANCHOR_NAME
    ]
    if (
        len(prefix_matches) != 1
        or len(matches) != 1
        or prefix_matches[0] != matches[0]
    ):
        raise RuntimeError(
            "Runtime GLB clan-hall anchor prefix drifted: "
            f"prefix={len(prefix_matches)} exact={len(matches)} "
            f"names={[node.get('name') for _, node in prefix_matches]}"
        )
    anchor_index, node = matches[0]
    parents = [
        parent
        for parent in nodes
        if anchor_index in parent.get("children", [])
    ]
    extras = node.get("extras", {})
    expected_translation = tuple(CLAN_HALL_GATE_FLOOR_CENTER_GODOT)
    translation = tuple(float(value) for value in node.get("translation", ()))
    rotation = tuple(float(value) for value in node.get("rotation", (0.0, 0.0, 0.0, 1.0)))
    scale = tuple(float(value) for value in node.get("scale", (1.0, 1.0, 1.0)))
    valid = (
        len(parents) == 1
        and parents[0].get("name") == "GuangchangPawnshop"
        and len(translation) == 3
        and all(
            abs(actual - expected) <= 0.00001
            for actual, expected in zip(translation, expected_translation, strict=True)
        )
        and len(rotation) == 4
        and all(
            abs(actual - expected) <= 0.00001
            for actual, expected in zip(
                rotation,
                (0.0, 0.0, 0.0, 1.0),
                strict=True,
            )
        )
        and len(scale) == 3
        and all(abs(value - 1.0) <= 0.00001 for value in scale)
        and abs(float(extras.get("gate_width_m", 0.0)) - CLAN_HALL_GATE_WIDTH_METERS)
        <= 0.00001
        and abs(float(extras.get("gate_height_m", 0.0)) - CLAN_HALL_GATE_HEIGHT_METERS)
        <= 0.00001
        and abs(
            float(extras.get("gate_floor_y_m", 0.0))
            - CLAN_HALL_GATE_FLOOR_CENTER_GODOT.y
        )
        <= 0.00001
        and extras.get("gate_outward_axis") == "+Z"
        and extras.get("gate_removed_component_count")
        == CLAN_HALL_GATE_REMOVED_COMPONENTS
        and extras.get("gate_removed_vertex_count") == CLAN_HALL_GATE_REMOVED_VERTICES
        and extras.get("gate_removed_triangle_count")
        == CLAN_HALL_GATE_REMOVED_TRIANGLES
    )
    print(
        "JIANGHAI_CLAN_HALL_GLB_ANCHOR_CHECK "
        f"valid={valid} count={len(matches)} prefix={len(prefix_matches)}/1 parent="
        f"{parents[0].get('name') if len(parents) == 1 else None} "
        f"translation={translation} rotation={rotation} scale={scale} "
        f"size={extras.get('gate_width_m')}x{extras.get('gate_height_m')} "
        f"outward={extras.get('gate_outward_axis')}"
    )
    if not valid:
        raise RuntimeError("Clan-hall GLB/Godot anchor contract failed")
    return {
        "anchor_count": len(matches),
        "anchor_prefix_count": len(prefix_matches),
        "parent_ready": len(parents) == 1,
        "transform_ready": valid,
    }
