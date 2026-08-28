"""Author and export Operation Steel Tide's original melee weapon set.

Run from the repository root with Blender 4.5 LTS:
    blender --background --factory-startup --python scripts/blender/build_melee_weapons.py

The generated meshes are authored in metres around the primary grip.  Blade
length follows Blender +Y; Blender's glTF export maps that forward direction to
Godot -Z.  Godot owns collision, gameplay, attachment, and animation.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = REPO_ROOT / "assets" / "models" / "steel_tide_melee"
SOURCE_DIR = REPO_ROOT / "source_art" / "melee_weapons"
MARKER_NAMES = ("GripPrimary", "GripSupport", "BladeBase", "BladeTip")
MATERIAL_NAMES = ("TintBlade", "TintEdge", "TintGrip", "TintAccent")


@dataclass(frozen=True)
class WeaponBuild:
    slug: str
    root_name: str
    blade_base: float
    blade_tip: float
    grip_primary: float
    grip_support: float
    preview_target_y: float
    preview_scale: float
    builder: Callable[[bpy.types.Object, dict[str, bpy.types.Material]], None]
    palette: dict[str, tuple[tuple[float, float, float, float], float, float, tuple[float, float, float, float] | None, float]]


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"


def make_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness
        coat = principled.inputs.get("Coat Weight")
        if coat is not None:
            coat.default_value = 0.18 if metallic > 0.5 else 0.06
        if emission is not None:
            emission_color = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
            emission_power = principled.inputs.get("Emission Strength")
            if emission_color is not None:
                emission_color.default_value = emission
            if emission_power is not None:
                emission_power.default_value = emission_strength
    return material


def create_palette(spec: WeaponBuild) -> dict[str, bpy.types.Material]:
    return {
        name: make_material(name, *spec.palette[name])
        for name in MATERIAL_NAMES
    }


def empty(name: str, parent: bpy.types.Object | None = None, size: float = 0.035) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = size
    obj.parent = parent
    return obj


def marker(name: str, parent: bpy.types.Object, y: float) -> bpy.types.Object:
    obj = empty(name, parent, 0.025)
    obj.location = (0.0, y, 0.0)
    obj["steel_tide_marker"] = name
    return obj


def create_mesh(
    name: str,
    parent: bpy.types.Object,
    vertices: list[tuple[float, float, float]],
    faces: list[tuple[int, ...]],
    materials: Iterable[bpy.types.Material],
    face_materials: list[int] | None = None,
    bevel: float = 0.0,
    bevel_segments: int = 3,
    smooth: bool = False,
) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    for material in materials:
        mesh.materials.append(material)
    if face_materials is not None:
        if len(face_materials) != len(mesh.polygons):
            raise RuntimeError(f"{name}: material assignment does not match polygon count")
        for polygon, material_index in zip(mesh.polygons, face_materials):
            polygon.material_index = material_index
    if smooth:
        for polygon in mesh.polygons:
            polygon.use_smooth = True
    if bevel > 0.0:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        modifier = obj.modifiers.new("AuthoredEdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = bevel_segments
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)
    return obj


def lathe_y(
    name: str,
    parent: bpy.types.Object,
    profile: list[tuple[float, float, float]],
    material: bpy.types.Material,
    segments: int = 32,
    bevel: float = 0.001,
) -> bpy.types.Object:
    """Create a custom elliptical lathe whose axis is Blender +Y."""
    vertices: list[tuple[float, float, float]] = []
    for y, radius_x, radius_z in profile:
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append((math.cos(angle) * radius_x, y, math.sin(angle) * radius_z))
    faces: list[tuple[int, ...]] = []
    rings = len(profile)
    for ring_index in range(rings - 1):
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            a = ring_index * segments + segment
            b = ring_index * segments + next_segment
            c = (ring_index + 1) * segments + next_segment
            d = (ring_index + 1) * segments + segment
            faces.append((a, b, c, d))
    faces.append(tuple(range(segments - 1, -1, -1)))
    last = (rings - 1) * segments
    faces.append(tuple(last + segment for segment in range(segments)))
    return create_mesh(name, parent, vertices, faces, [material], bevel=bevel, smooth=True)


def extruded_polygon_xy(
    name: str,
    parent: bpy.types.Object,
    outline: list[tuple[float, float]],
    thickness: float,
    material: bpy.types.Material,
    bevel: float = 0.0015,
) -> bpy.types.Object:
    count = len(outline)
    half = thickness * 0.5
    vertices = [(x, y, -half) for x, y in outline]
    vertices.extend((x, y, half) for x, y in outline)
    faces: list[tuple[int, ...]] = [
        tuple(range(count - 1, -1, -1)),
        tuple(range(count, count * 2)),
    ]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))
    return create_mesh(name, parent, vertices, faces, [material], bevel=bevel)


def custom_blade(
    name: str,
    parent: bpy.types.Object,
    controls: list[tuple[float, float, float]],
    thickness: float,
    blade_material: bpy.types.Material,
    edge_material: bpy.types.Material,
    sections_per_metre: int = 42,
) -> bpy.types.Object:
    """Loft a single-edged blade from authored centre/half-width controls.

    The -X side is the full-thickness spine and +X is the tapered cutting
    edge.  Six cross-blade rails form a crowned ridge and a distinct edge
    bevel instead of a flat extruded silhouette.
    """
    stations: list[tuple[float, float, float]] = []
    for control_index in range(len(controls) - 1):
        y0, centre0, width0 = controls[control_index]
        y1, centre1, width1 = controls[control_index + 1]
        section_count = max(2, int(math.ceil((y1 - y0) * sections_per_metre)))
        for index in range(section_count):
            if control_index > 0 and index == 0:
                continue
            t = index / section_count
            smooth_t = t * t * (3.0 - 2.0 * t)
            stations.append((
                y0 + (y1 - y0) * t,
                centre0 + (centre1 - centre0) * smooth_t,
                width0 + (width1 - width0) * smooth_t,
            ))
    stations.append(controls[-1])

    rail_u = (-1.0, -0.64, -0.18, 0.34, 0.76, 1.0)
    crown = (0.58, 0.94, 1.0, 0.91, 0.48, 0.055)
    rail_count = len(rail_u)
    vertices: list[tuple[float, float, float]] = []
    for y, centre, width in stations:
        taper = min(1.0, max(0.16, width / max(controls[0][2], 0.001)))
        for side in (1.0, -1.0):
            for u, height_factor in zip(rail_u, crown):
                x = centre + width * u
                z = side * thickness * height_factor * (0.72 + 0.28 * taper)
                vertices.append((x, y, z))

    faces: list[tuple[int, ...]] = []
    face_materials: list[int] = []
    station_stride = rail_count * 2
    for station_index in range(len(stations) - 1):
        current = station_index * station_stride
        following = (station_index + 1) * station_stride
        for rail in range(rail_count - 1):
            faces.append((current + rail, following + rail, following + rail + 1, current + rail + 1))
            face_materials.append(1 if rail >= rail_count - 2 else 0)
            bottom = current + rail_count
            next_bottom = following + rail_count
            faces.append((bottom + rail + 1, next_bottom + rail + 1, next_bottom + rail, bottom + rail))
            face_materials.append(1 if rail >= rail_count - 2 else 0)
        faces.append((current, current + rail_count, following + rail_count, following))
        face_materials.append(0)
        edge_top = current + rail_count - 1
        edge_bottom = current + station_stride - 1
        next_edge_top = following + rail_count - 1
        next_edge_bottom = following + station_stride - 1
        faces.append((edge_top, next_edge_top, next_edge_bottom, edge_bottom))
        face_materials.append(1)

    first = 0
    last = (len(stations) - 1) * station_stride
    faces.append(tuple(first + rail for rail in range(rail_count - 1, -1, -1)) + tuple(first + rail_count + rail for rail in range(rail_count)))
    face_materials.append(0)
    faces.append(tuple(last + rail for rail in range(rail_count)) + tuple(last + rail_count + rail for rail in range(rail_count - 1, -1, -1)))
    face_materials.append(1)
    return create_mesh(
        name,
        parent,
        vertices,
        faces,
        [blade_material, edge_material],
        face_materials,
        bevel=0.00065,
        bevel_segments=2,
    )


def add_torus(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    major_radius: float,
    minor_radius: float,
    material: bpy.types.Material,
    major_segments: int = 40,
    minor_segments: int = 10,
    rotation: tuple[float, float, float] = (math.pi * 0.5, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=location,
        rotation=rotation,
        major_radius=major_radius,
        minor_radius=minor_radius,
    )
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def add_helix(
    name: str,
    parent: bpy.types.Object,
    y_start: float,
    y_end: float,
    radius_x: float,
    radius_z: float,
    turns: float,
    strand_radius: float,
    material: bpy.types.Material,
    direction: float = 1.0,
    phase: float = 0.0,
    steps: int = 112,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(f"{name}Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = strand_radius
    curve.bevel_resolution = 3
    curve.resolution_u = 2
    spline = curve.splines.new("NURBS")
    spline.points.add(steps - 1)
    for index, point in enumerate(spline.points):
        t = index / (steps - 1)
        angle = phase + direction * turns * math.tau * t
        point.co = (
            math.cos(angle) * radius_x,
            y_start + (y_end - y_start) * t,
            math.sin(angle) * radius_z,
            1.0,
        )
    spline.order_u = 3
    spline.use_endpoint_u = True
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    curve.materials.append(material)
    return obj


def add_curve_line(
    name: str,
    parent: bpy.types.Object,
    points: list[tuple[float, float, float]],
    radius: float,
    material: bpy.types.Material,
    cyclic: bool = False,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(f"{name}Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bezier, point in zip(spline.bezier_points, points):
        bezier.co = point
        bezier.handle_left_type = "AUTO"
        bezier.handle_right_type = "AUTO"
    spline.use_cyclic_u = cyclic
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    curve.materials.append(material)
    return obj


def add_ribbon(
    name: str,
    parent: bpy.types.Object,
    points: list[tuple[float, float, float]],
    width: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    for index, point in enumerate(points):
        current = Vector(point)
        previous = Vector(points[max(0, index - 1)])
        following = Vector(points[min(len(points) - 1, index + 1)])
        tangent = following - previous
        side = Vector((-tangent.y, tangent.x, 0.0)).normalized() * width * 0.5
        vertices.append(tuple(current - side))
        vertices.append(tuple(current + side))
    faces = [
        (index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2)
        for index in range(len(points) - 1)
    ]
    obj = create_mesh(name, parent, vertices, faces, [material])
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    solidify = obj.modifiers.new("InlayThickness", "SOLIDIFY")
    solidify.thickness = 0.0008
    solidify.offset = 0.0
    bpy.ops.object.modifier_apply(modifier=solidify.name)
    bevel = obj.modifiers.new("InlaySoftening", "BEVEL")
    bevel.width = 0.00035
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    obj.select_set(False)
    return obj


def add_faceted_gem(
    name: str,
    parent: bpy.types.Object,
    y: float,
    radius: float,
    length: float,
    material: bpy.types.Material,
    sides: int = 10,
) -> bpy.types.Object:
    vertices = [(0.0, y - length * 0.5, 0.0), (0.0, y + length * 0.5, 0.0)]
    for index in range(sides):
        angle = math.tau * index / sides
        vertices.append((math.cos(angle) * radius, y, math.sin(angle) * radius))
    faces: list[tuple[int, ...]] = []
    for index in range(sides):
        next_index = (index + 1) % sides
        faces.append((0, 2 + next_index, 2 + index))
        faces.append((1, 2 + index, 2 + next_index))
    return create_mesh(name, parent, vertices, faces, [material], bevel=0.0008)


def add_grip_wrap(
    parent: bpy.types.Object,
    y_start: float,
    y_end: float,
    radius_x: float,
    radius_z: float,
    turns: float,
    radius: float,
    material: bpy.types.Material,
    prefix: str,
) -> None:
    add_helix(f"{prefix}WrapA", parent, y_start, y_end, radius_x, radius_z, turns, radius, material, 1.0, 0.0)
    add_helix(f"{prefix}WrapB", parent, y_start, y_end, radius_x, radius_z, turns, radius, material, -1.0, math.pi * 0.5)


def build_tactical_knife(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> None:
    custom_blade(
        "TacticalKnifeBlade",
        root,
        [
            (0.035, -0.002, 0.030),
            (0.085, -0.001, 0.033),
            (0.172, 0.002, 0.035),
            (0.236, 0.006, 0.029),
            (0.280, 0.012, 0.016),
            (0.292, 0.018, 0.0012),
        ],
        0.0042,
        mats["TintBlade"],
        mats["TintEdge"],
        sections_per_metre=64,
    )
    extruded_polygon_xy(
        "KnifeGuard",
        root,
        [(-0.052, 0.018), (-0.030, 0.034), (-0.010, 0.038), (0.010, 0.038), (0.033, 0.032), (0.050, 0.014), (0.042, 0.002), (-0.040, 0.002)],
        0.014,
        mats["TintAccent"],
        bevel=0.0025,
    )
    lathe_y(
        "KnifeGripCore",
        root,
        [(-0.132, 0.015, 0.012), (-0.123, 0.020, 0.016), (-0.065, 0.022, 0.017), (-0.005, 0.020, 0.016), (0.012, 0.017, 0.014)],
        mats["TintGrip"],
        36,
        0.0015,
    )
    add_grip_wrap(root, -0.119, -0.002, 0.0222, 0.0172, 4.4, 0.0024, mats["TintAccent"], "Knife")
    lathe_y(
        "KnifeFingerCollar",
        root,
        [(0.005, 0.022, 0.018), (0.016, 0.025, 0.020), (0.026, 0.021, 0.016), (0.031, 0.019, 0.014)],
        mats["TintAccent"],
        32,
        0.001,
    )
    add_faceted_gem("KnifePommel", root, -0.142, 0.020, 0.026, mats["TintAccent"], 12)
    add_torus(
        "KnifeLanyardRing",
        root,
        (0.0, -0.169, 0.0),
        0.013,
        0.0028,
        mats["TintAccent"],
        32,
        8,
        rotation=(0.0, 0.0, 0.0),
    )
    add_ribbon(
        "KnifeFullerInlay",
        root,
        [(-0.014, 0.055, 0.00435), (-0.016, 0.118, 0.00435), (-0.012, 0.186, 0.0042), (-0.003, 0.242, 0.0036)],
        0.004,
        mats["TintAccent"],
    )
    for index in range(6):
        y = 0.047 + index * 0.012
        extruded_polygon_xy(
            f"KnifeSpineSerration{index:02d}",
            root,
            [(-0.0315, y - 0.0045), (-0.037, y + 0.001), (-0.0315, y + 0.006)],
            0.0065,
            mats["TintBlade"],
            bevel=0.0005,
        )


def build_zhanma_dao(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> None:
    blade_base = 0.184
    blade_tip = blade_base + 0.936
    custom_blade(
        "ZhanmaDaoBlade",
        root,
        [
            (blade_base, -0.006, 0.057),
            (0.310, -0.005, 0.065),
            (0.570, 0.000, 0.071),
            (0.815, 0.010, 0.075),
            (0.990, 0.025, 0.066),
            (1.075, 0.041, 0.040),
            (blade_tip, 0.060, 0.0015),
        ],
        0.0068,
        mats["TintBlade"],
        mats["TintEdge"],
        sections_per_metre=46,
    )
    extruded_polygon_xy(
        "ZhanmaWingGuard",
        root,
        [(-0.105, 0.151), (-0.081, 0.168), (-0.044, 0.178), (-0.019, 0.177), (0.0, 0.166), (0.019, 0.177), (0.044, 0.178), (0.081, 0.168), (0.105, 0.151), (0.082, 0.139), (0.036, 0.145), (0.0, 0.151), (-0.036, 0.145), (-0.082, 0.139)],
        0.021,
        mats["TintAccent"],
        bevel=0.003,
    )
    lathe_y(
        "ZhanmaGripCore",
        root,
        [(-0.156, 0.022, 0.019), (-0.146, 0.028, 0.023), (-0.090, 0.030, 0.024), (0.000, 0.029, 0.023), (0.090, 0.030, 0.024), (0.146, 0.028, 0.023), (0.156, 0.022, 0.019)],
        mats["TintGrip"],
        40,
        0.0018,
    )
    add_grip_wrap(root, -0.142, 0.142, 0.0302, 0.0242, 8.2, 0.0027, mats["TintAccent"], "Zhanma")
    for y, radius in ((-0.150, 0.032), (-0.052, 0.031), (0.050, 0.031), (0.150, 0.032)):
        lathe_y(
            f"ZhanmaGripBand{int((y + 0.2) * 1000):03d}",
            root,
            [(y - 0.004, radius, radius * 0.80), (y + 0.004, radius, radius * 0.80)],
            mats["TintAccent"],
            36,
            0.001,
        )
    lathe_y(
        "ZhanmaBladeCollar",
        root,
        [(0.166, 0.034, 0.025), (0.177, 0.040, 0.029), (0.190, 0.036, 0.024)],
        mats["TintAccent"],
        36,
        0.0015,
    )
    add_torus(
        "ZhanmaRingPommel",
        root,
        (0.0, -0.198, 0.0),
        0.044,
        0.0075,
        mats["TintAccent"],
        48,
        12,
        rotation=(0.0, 0.0, 0.0),
    )
    lathe_y(
        "ZhanmaPommelNeck",
        root,
        [(-0.172, 0.025, 0.022), (-0.160, 0.032, 0.026), (-0.150, 0.029, 0.024)],
        mats["TintAccent"],
        36,
        0.0015,
    )
    add_ribbon(
        "ZhanmaFullerLine",
        root,
        [(-0.030, 0.215, 0.0069), (-0.034, 0.410, 0.0069), (-0.034, 0.670, 0.0068), (-0.026, 0.900, 0.0064), (0.010, 1.055, 0.0048)],
        0.006,
        mats["TintAccent"],
    )
    add_curve_line(
        "ZhanmaRingCord",
        root,
        [(-0.020, -0.238, -0.006), (-0.030, -0.270, -0.020), (-0.014, -0.300, -0.028)],
        0.0027,
        mats["TintGrip"],
    )


def build_tianxuan_dao(root: bpy.types.Object, mats: dict[str, bpy.types.Material]) -> None:
    blade_base = 0.194
    blade_tip = blade_base + 1.0
    custom_blade(
        "TianxuanBlacksteelBlade",
        root,
        [
            (blade_base, -0.003, 0.062),
            (0.315, -0.010, 0.074),
            (0.520, -0.020, 0.083),
            (0.735, -0.011, 0.070),
            (0.900, 0.015, 0.087),
            (1.040, 0.045, 0.102),
            (1.135, 0.076, 0.058),
            (blade_tip, 0.104, 0.0015),
        ],
        0.0078,
        mats["TintBlade"],
        mats["TintEdge"],
        sections_per_metre=50,
    )
    extruded_polygon_xy(
        "TianxuanCrescentGuard",
        root,
        [(-0.145, 0.127), (-0.112, 0.167), (-0.066, 0.189), (-0.028, 0.183), (0.0, 0.157), (0.031, 0.184), (0.078, 0.190), (0.126, 0.163), (0.151, 0.119), (0.111, 0.137), (0.060, 0.148), (0.0, 0.137), (-0.058, 0.148), (-0.108, 0.143)],
        0.026,
        mats["TintAccent"],
        bevel=0.0035,
    )
    extruded_polygon_xy(
        "TianxuanSpineHornLeft",
        root,
        [(-0.060, 0.212), (-0.111, 0.252), (-0.072, 0.264), (-0.041, 0.235)],
        0.012,
        mats["TintBlade"],
        bevel=0.0015,
    )
    lathe_y(
        "TianxuanGripCore",
        root,
        [(-0.205, 0.021, 0.020), (-0.190, 0.030, 0.027), (-0.120, 0.032, 0.028), (-0.025, 0.031, 0.027), (0.070, 0.032, 0.028), (0.135, 0.030, 0.026), (0.150, 0.024, 0.021)],
        mats["TintGrip"],
        44,
        0.0018,
    )
    add_grip_wrap(root, -0.187, 0.132, 0.0323, 0.0283, 9.0, 0.0026, mats["TintAccent"], "Tianxuan")
    lathe_y(
        "TianxuanBladeCollar",
        root,
        [(0.154, 0.034, 0.029), (0.173, 0.043, 0.034), (0.193, 0.038, 0.027), (0.202, 0.034, 0.024)],
        mats["TintAccent"],
        40,
        0.0014,
    )
    add_faceted_gem("TianxuanPommelCrystal", root, -0.233, 0.035, 0.062, mats["TintAccent"], 12)
    add_torus(
        "TianxuanPommelHalo",
        root,
        (0.0, -0.243, 0.0),
        0.049,
        0.0045,
        mats["TintBlade"],
        48,
        10,
        rotation=(0.0, 0.0, 0.0),
    )
    for index, y in enumerate((-0.130, -0.035, 0.060)):
        lathe_y(
            f"TianxuanGripSeal{index}",
            root,
            [(y - 0.0035, 0.034, 0.030), (y + 0.0035, 0.034, 0.030)],
            mats["TintAccent"],
            40,
            0.0008,
        )

    rune_z = 0.0081
    add_curve_line(
        "TianxuanMainRune",
        root,
        [(-0.030, 0.235, rune_z), (-0.041, 0.370, rune_z), (-0.033, 0.525, rune_z), (-0.052, 0.680, rune_z), (-0.020, 0.825, rune_z), (0.010, 0.965, rune_z), (0.052, 1.085, 0.0068)],
        0.0032,
        mats["TintAccent"],
    )
    for index, (x, y) in enumerate(((-0.038, 0.355), (-0.036, 0.520), (-0.048, 0.685), (-0.016, 0.830), (0.013, 0.965))):
        add_curve_line(
            f"TianxuanRuneBranch{index}",
            root,
            [(x - 0.022, y - 0.024, rune_z), (x, y, rune_z), (x + 0.025, y + 0.018, rune_z)],
            0.0024,
            mats["TintAccent"],
        )
    add_torus("TianxuanGuardCore", root, (0.0, 0.154, 0.0), 0.033, 0.005, mats["TintAccent"], 40, 10)


def convert_curves(root: bpy.types.Object) -> None:
    curves = [obj for obj in root.children_recursive if obj.type == "CURVE"]
    for obj in curves:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
        for polygon in obj.data.polygons:
            polygon.use_smooth = True


def mesh_statistics(root: bpy.types.Object) -> tuple[int, int, int, tuple[float, float, float]]:
    mesh_objects = [obj for obj in root.children_recursive if obj.type == "MESH"]
    triangle_count = 0
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for obj in mesh_objects:
        obj.data.calc_loop_triangles()
        triangle_count += len(obj.data.loop_triangles)
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, world.x)
            minimum.y = min(minimum.y, world.y)
            minimum.z = min(minimum.z, world.z)
            maximum.x = max(maximum.x, world.x)
            maximum.y = max(maximum.y, world.y)
            maximum.z = max(maximum.z, world.z)
    dimensions = maximum - minimum
    used_materials = {
        slot.material.name
        for obj in mesh_objects
        for slot in obj.material_slots
        if slot.material is not None
    }
    return len(mesh_objects), triangle_count, len(used_materials), tuple(dimensions)


def select_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in root.children_recursive:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_glb(root: bpy.types.Object, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    select_hierarchy(root)
    result = bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )
    if result != {"FINISHED"} or not output.is_file() or output.stat().st_size < 4096:
        raise RuntimeError(f"Failed to create production GLB: {output}")


def add_preview_stage(spec: WeaponBuild, root: bpy.types.Object, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("PreviewWorld")
        scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.004, 0.008, 0.012, 1.0)
        background.inputs["Strength"].default_value = 0.035

    floor_material = make_material("PreviewStageOnly", (0.007, 0.011, 0.016, 1.0), 0.12, 0.46)
    bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0.0, spec.preview_target_y, -0.042))
    floor = bpy.context.object
    floor.name = "PreviewStageFloor"
    floor.data.materials.append(floor_material)
    floor["preview_only"] = True

    target = Vector((0.0, spec.preview_target_y, 0.005))
    energy_scale = min(1.0, max(0.38, spec.preview_scale * spec.preview_scale))
    lights = (
        ("PreviewKey", (-0.75, spec.preview_target_y - 0.55, 1.45), 54.0 * energy_scale, (0.60, 0.84, 1.0), 1.2),
        ("PreviewFill", (0.90, spec.preview_target_y + 0.20, 0.70), 24.0 * energy_scale, (0.10, 0.95, 0.82), 1.0),
        ("PreviewRim", (-0.30, spec.preview_target_y + 0.80, 0.95), 76.0 * energy_scale, (0.22, 0.55, 1.0), 0.75),
        ("PreviewWarm", (0.65, spec.preview_target_y - 0.75, 0.38), 14.0 * energy_scale, (1.0, 0.34, 0.12), 0.7),
    )
    for name, location, energy, color, size in lights:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
        light["preview_only"] = True

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.62 * spec.preview_scale, spec.preview_target_y - 0.12, 1.40 * spec.preview_scale)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.rotation_euler.rotate_axis("Z", math.radians(-27.0))
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = spec.preview_scale
    camera_data.lens = 64.0
    camera["preview_only"] = True
    scene.camera = camera

    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.filepath = str(output)
    scene.render.image_settings.color_depth = "8"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.65
    bpy.ops.render.render(write_still=True)


def validate_authored_scene(spec: WeaponBuild, root: bpy.types.Object) -> None:
    missing_markers = [name for name in MARKER_NAMES if bpy.data.objects.get(name) is None]
    if missing_markers:
        raise RuntimeError(f"{spec.slug}: missing markers {missing_markers}")
    material_names = {material.name for material in bpy.data.materials}
    missing_materials = [name for name in MATERIAL_NAMES if name not in material_names]
    if missing_materials:
        raise RuntimeError(f"{spec.slug}: missing materials {missing_materials}")
    if root.location.length > 0.00001:
        raise RuntimeError(f"{spec.slug}: root origin moved away from primary grip")
    if abs((spec.blade_tip - spec.blade_base) - (0.936 if spec.slug == "zhanma_dao" else 1.0 if spec.slug == "tianxuan_dao" else 0.257)) > 0.002:
        raise RuntimeError(f"{spec.slug}: authored blade contract has an unexpected length")


def build_weapon(spec: WeaponBuild) -> dict[str, object]:
    clear_scene()
    materials = create_palette(spec)
    root = empty(spec.root_name, None, 0.055)
    root["asset_author"] = "Operation Steel Tide project"
    root["asset_license"] = "MIT"
    root["forward_axis"] = "Blender +Y / Godot -Z"
    root["primary_grip_origin"] = True
    spec.builder(root, materials)
    marker("GripPrimary", root, spec.grip_primary)
    marker("GripSupport", root, spec.grip_support)
    marker("BladeBase", root, spec.blade_base)
    marker("BladeTip", root, spec.blade_tip)
    convert_curves(root)
    validate_authored_scene(spec, root)
    mesh_count, triangle_count, material_count, dimensions = mesh_statistics(root)
    if triangle_count < 3000:
        raise RuntimeError(f"{spec.slug}: {triangle_count} triangles is below the authored detail floor")
    if triangle_count > 35000:
        raise RuntimeError(f"{spec.slug}: {triangle_count} triangles exceeds the realtime budget")
    if material_count != 4:
        raise RuntimeError(f"{spec.slug}: expected exactly four runtime materials, found {material_count}")

    glb_path = ASSET_DIR / f"{spec.slug}.glb"
    blend_path = SOURCE_DIR / f"{spec.slug}.blend"
    preview_path = ASSET_DIR / f"{spec.slug}_preview.png"
    export_glb(root, glb_path)
    add_preview_stage(spec, root, preview_path)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    print(
        f"MELEE_EXPORT weapon={spec.slug} meshes={mesh_count} triangles={triangle_count} "
        f"materials={material_count} dimensions_m={dimensions[0]:.3f}x{dimensions[1]:.3f}x{dimensions[2]:.3f} "
        f"glb={glb_path} blend={blend_path} preview={preview_path}"
    )
    return {
        "spec": spec,
        "meshes": mesh_count,
        "triangles": triangle_count,
        "materials": material_count,
        "dimensions": dimensions,
        "glb": glb_path,
    }


def verify_glb(result: dict[str, object]) -> None:
    spec = result["spec"]
    assert isinstance(spec, WeaponBuild)
    glb_path = result["glb"]
    assert isinstance(glb_path, Path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    imported = bpy.ops.import_scene.gltf(filepath=str(glb_path))
    if imported != {"FINISHED"}:
        raise RuntimeError(f"{spec.slug}: Blender could not re-import generated GLB")
    missing_markers = [name for name in MARKER_NAMES if bpy.data.objects.get(name) is None]
    imported_materials = {material.name for material in bpy.data.materials}
    missing_materials = [name for name in MATERIAL_NAMES if name not in imported_materials]
    root = bpy.data.objects.get(spec.root_name)
    if root is None or missing_markers or missing_materials:
        raise RuntimeError(
            f"{spec.slug}: GLB contract failure root={root is not None} "
            f"markers={missing_markers} materials={missing_materials}"
        )
    triangles = 0
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            triangles += len(obj.data.loop_triangles)
    if triangles != result["triangles"]:
        raise RuntimeError(f"{spec.slug}: GLB triangle count changed {result['triangles']} -> {triangles}")
    print(
        f"MELEE_VERIFY weapon={spec.slug} root={spec.root_name} markers={','.join(MARKER_NAMES)} "
        f"materials={','.join(MATERIAL_NAMES)} triangles={triangles} valid=True"
    )


def weapon_specs() -> tuple[WeaponBuild, ...]:
    return (
        WeaponBuild(
            "tactical_knife",
            "TacticalKnife",
            0.035,
            0.292,
            -0.040,
            -0.096,
            0.066,
            0.62,
            build_tactical_knife,
            {
                "TintBlade": ((0.055, 0.070, 0.078, 1.0), 0.92, 0.24, None, 0.0),
                "TintEdge": ((0.52, 0.60, 0.64, 1.0), 0.96, 0.12, None, 0.0),
                "TintGrip": ((0.018, 0.024, 0.025, 1.0), 0.08, 0.68, None, 0.0),
                "TintAccent": ((0.08, 0.48, 0.42, 1.0), 0.72, 0.24, (0.01, 0.11, 0.075, 1.0), 0.7),
            },
        ),
        WeaponBuild(
            "zhanma_dao",
            "ZhanmaDao",
            0.184,
            1.120,
            0.035,
            -0.085,
            0.435,
            1.70,
            build_zhanma_dao,
            {
                "TintBlade": ((0.10, 0.115, 0.12, 1.0), 0.94, 0.20, None, 0.0),
                "TintEdge": ((0.68, 0.70, 0.67, 1.0), 0.98, 0.10, None, 0.0),
                "TintGrip": ((0.075, 0.025, 0.018, 1.0), 0.03, 0.70, None, 0.0),
                "TintAccent": ((0.34, 0.17, 0.045, 1.0), 0.82, 0.27, None, 0.0),
            },
        ),
        WeaponBuild(
            "tianxuan_dao",
            "TianxuanDao",
            0.194,
            1.194,
            0.030,
            -0.092,
            0.455,
            1.78,
            build_tianxuan_dao,
            {
                "TintBlade": ((0.012, 0.020, 0.030, 1.0), 0.93, 0.17, None, 0.0),
                "TintEdge": ((0.30, 0.49, 0.58, 1.0), 0.95, 0.10, (0.005, 0.055, 0.080, 1.0), 0.5),
                "TintGrip": ((0.012, 0.015, 0.023, 1.0), 0.10, 0.62, None, 0.0),
                "TintAccent": ((0.015, 0.58, 0.76, 1.0), 0.46, 0.19, (0.005, 0.35, 0.62, 1.0), 5.5),
            },
        ),
    )


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    results = [build_weapon(spec) for spec in weapon_specs()]
    for result in results:
        verify_glb(result)
    print(f"MELEE_SET_PASS weapons={len(results)} valid=True")


if __name__ == "__main__":
    main()
