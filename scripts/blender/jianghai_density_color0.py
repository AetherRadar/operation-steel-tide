"""Collapse Jianghai's scalar Quaternius density palettes into COLOR_0.

Each relevant source LOD carries four to seven texture-free materials that vary
only by base color while sharing one profile roughness.  Keeping those slots on
every perimeter instance multiplies both color and directional-shadow surface
submissions.  This deterministic DCC pass stores the exact former base color on
every face corner and leaves one opaque material/primitive on each shared mesh.
"""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
from math import isfinite
from struct import pack

import bpy

from jianghai_chinese_district_layout import (
    DENSITY_BUILDING_LAYOUT,
    DENSITY_COLOR0_ATTRIBUTE,
    DENSITY_COLOR0_INFILL_SUFFIXES,
    DENSITY_COLOR0_PROFILE_MATERIALS,
    DENSITY_COLOR0_PROFILE_ROUGHNESS,
    DENSITY_COLOR0_PROFILE_SOURCE_SURFACES,
    DENSITY_COLOR0_VERSION,
    QUATERNIUS_DENSITY_MESHES,
)


COLOR_TOLERANCE = 1.0e-6
SCALAR_TOLERANCE = 1.0e-5


@dataclass(frozen=True)
class DensityColor0MeshMetrics:
    profile_name: str
    triangle_count: int
    source_surface_count: int
    output_surface_count: int
    distinct_color_count: int
    palette_sha256: str
    maximum_color_error: float


@dataclass(frozen=True)
class DensityColor0SceneMetrics:
    profile_count: int
    profile_surface_count: int
    instance_count: int
    instance_surface_count: int
    infill_instance_count: int
    infill_surface_count: int


def _principled(material: bpy.types.Material) -> bpy.types.ShaderNodeBsdfPrincipled:
    if not material.use_nodes or material.node_tree is None:
        raise RuntimeError(f"Density material is not node-based: {material.name}")
    nodes = [node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"]
    if len(nodes) != 1:
        raise RuntimeError(
            f"Density material must contain one Principled BSDF: {material.name}"
        )
    return nodes[0]


def _unlinked_scalar(material: bpy.types.Material, socket_name: str) -> float:
    socket = _principled(material).inputs.get(socket_name)
    if socket is None or socket.is_linked:
        raise RuntimeError(
            f"Density material has a non-scalar {socket_name}: {material.name}"
        )
    return float(socket.default_value)


def _source_base_color(material: bpy.types.Material) -> tuple[float, float, float, float]:
    if material.node_tree is None or any(
        node.type == "TEX_IMAGE" for node in material.node_tree.nodes
    ):
        raise RuntimeError(
            f"COLOR_0 consolidation only accepts texture-free materials: {material.name}"
        )
    socket = _principled(material).inputs.get("Base Color")
    if socket is None or socket.is_linked:
        raise RuntimeError(
            f"Density material has a non-scalar Base Color: {material.name}"
        )
    color = tuple(float(component) for component in socket.default_value)
    if len(color) != 4 or not all(isfinite(component) for component in color):
        raise RuntimeError(f"Density material base color is invalid: {material.name}")
    if any(component < 0.0 or component > 1.0 for component in color):
        raise RuntimeError(f"Density material base color is out of range: {material.name}")
    return color


def _palette_digest(colors: set[tuple[float, float, float, float]]) -> str:
    payload = b"".join(
        pack("<4f", *color)
        for color in sorted(colors)
    )
    return sha256(payload).hexdigest().upper()


def _ensure_color_material(
    profile_name: str,
    source_asset: str,
) -> bpy.types.Material:
    material_name = DENSITY_COLOR0_PROFILE_MATERIALS[profile_name]
    material = bpy.data.materials.get(material_name)
    if material is None:
        material = bpy.data.materials.new(material_name)
    material.use_nodes = True
    material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    material.use_backface_culling = False
    if hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"

    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.name = "JianghaiDensityOutput"
    output.location = (360.0, 0.0)
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    principled.name = "JianghaiDensitySurface"
    principled.location = (80.0, 0.0)
    principled.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    principled.inputs["Roughness"].default_value = DENSITY_COLOR0_PROFILE_ROUGHNESS[
        profile_name
    ]
    principled.inputs["Metallic"].default_value = 0.0
    principled.inputs["Alpha"].default_value = 1.0
    vertex_color = nodes.new("ShaderNodeVertexColor")
    vertex_color.name = "JianghaiDensityCOLOR0"
    vertex_color.layer_name = DENSITY_COLOR0_ATTRIBUTE
    vertex_color.location = (-220.0, 40.0)
    material.node_tree.links.new(vertex_color.outputs["Color"], principled.inputs["Base Color"])
    material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])

    material["source_asset"] = source_asset
    material["source_creator"] = "Quaternius"
    material["source_url"] = "https://quaternius.com/packs/buildings.html"
    material["license"] = "CC0 1.0 Universal"
    material["authored_adaptation"] = (
        "Texture-free scalar palette preserved in face-corner COLOR_0"
    )
    material["jianghai_density_color0_profile"] = profile_name
    material["jianghai_density_color0_version"] = DENSITY_COLOR0_VERSION
    material["jianghai_density_color0_opaque"] = True
    return material


def _set_active_color(mesh: bpy.types.Mesh, attribute: bpy.types.Attribute) -> None:
    mesh.color_attributes.active_color = attribute
    mesh.color_attributes.render_color_index = mesh.color_attributes.find(attribute.name)


def consolidate_density_profile_mesh(
    mesh: bpy.types.Mesh,
    profile_name: str,
) -> DensityColor0MeshMetrics:
    """Idempotently collapse one scalar-material density mesh to COLOR_0."""

    if profile_name not in DENSITY_COLOR0_PROFILE_MATERIALS:
        raise RuntimeError(f"Unsupported COLOR_0 density profile: {profile_name}")
    if (
        mesh.get("jianghai_density_color0_version") == DENSITY_COLOR0_VERSION
        and mesh.get("jianghai_density_color0_profile") == profile_name
    ):
        return validate_density_profile_mesh(mesh, profile_name)

    expected_surfaces = DENSITY_COLOR0_PROFILE_SOURCE_SURFACES[profile_name]
    source_materials = list(mesh.materials)
    if len(source_materials) != expected_surfaces or any(
        material is None for material in source_materials
    ):
        raise RuntimeError(
            f"Density source surface count drifted: {profile_name} "
            f"actual={len(source_materials)} expected={expected_surfaces}"
        )
    used_indices = {polygon.material_index for polygon in mesh.polygons}
    if used_indices != set(range(expected_surfaces)):
        raise RuntimeError(
            f"Density source has unused or invalid material slots: {profile_name} "
            f"actual={sorted(used_indices)}"
        )

    expected_roughness = DENSITY_COLOR0_PROFILE_ROUGHNESS[profile_name]
    source_colors = []
    for material in source_materials:
        source_colors.append(_source_base_color(material))
        roughness = _unlinked_scalar(material, "Roughness")
        metallic = _unlinked_scalar(material, "Metallic")
        alpha = _unlinked_scalar(material, "Alpha")
        if (
            abs(roughness - expected_roughness) > SCALAR_TOLERANCE
            or abs(metallic) > SCALAR_TOLERANCE
            or abs(alpha - 1.0) > SCALAR_TOLERANCE
        ):
            raise RuntimeError(
                f"Density scalar material contract drifted: {material.name} "
                f"roughness={roughness:.6f} metallic={metallic:.6f} alpha={alpha:.6f}"
            )

    triangle_count_before = _triangle_count(mesh)
    previous_attribute = mesh.color_attributes.get(DENSITY_COLOR0_ATTRIBUTE)
    if previous_attribute is not None:
        mesh.color_attributes.remove(previous_attribute)
    attribute = mesh.color_attributes.new(
        name=DENSITY_COLOR0_ATTRIBUTE,
        type="FLOAT_COLOR",
        domain="CORNER",
    )
    maximum_error = 0.0
    for polygon in mesh.polygons:
        expected_color = source_colors[polygon.material_index]
        for loop_index in polygon.loop_indices:
            attribute.data[loop_index].color = expected_color
            maximum_error = max(
                maximum_error,
                max(
                    abs(float(actual) - expected)
                    for actual, expected in zip(
                        attribute.data[loop_index].color,
                        expected_color,
                    )
                ),
            )

    source_asset = str(mesh.get("source_asset", f"Quaternius Buildings Pack / {profile_name}"))
    material = _ensure_color_material(profile_name, source_asset)
    mesh.materials.clear()
    mesh.materials.append(material)
    for polygon in mesh.polygons:
        polygon.material_index = 0
    _set_active_color(mesh, attribute)
    mesh["jianghai_density_color0_version"] = DENSITY_COLOR0_VERSION
    mesh["jianghai_density_color0_profile"] = profile_name
    mesh["jianghai_density_color0_attribute"] = DENSITY_COLOR0_ATTRIBUTE
    mesh["jianghai_density_color0_source_surfaces"] = expected_surfaces
    mesh["jianghai_density_color0_output_surfaces"] = 1
    mesh["jianghai_density_color0_max_error"] = maximum_error
    mesh["jianghai_density_color0_opaque"] = True
    mesh.update()
    if _triangle_count(mesh) != triangle_count_before:
        raise RuntimeError(f"COLOR_0 consolidation changed topology: {profile_name}")
    metrics = validate_density_profile_mesh(mesh, profile_name)
    mesh["jianghai_density_color0_palette_sha256"] = metrics.palette_sha256
    return validate_density_profile_mesh(mesh, profile_name)


def _triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def validate_density_profile_mesh(
    mesh: bpy.types.Mesh,
    profile_name: str,
) -> DensityColor0MeshMetrics:
    expected_material_name = DENSITY_COLOR0_PROFILE_MATERIALS[profile_name]
    expected_source_surfaces = DENSITY_COLOR0_PROFILE_SOURCE_SURFACES[profile_name]
    if (
        mesh.get("jianghai_density_color0_version") != DENSITY_COLOR0_VERSION
        or mesh.get("jianghai_density_color0_profile") != profile_name
        or mesh.get("jianghai_density_color0_attribute") != DENSITY_COLOR0_ATTRIBUTE
        or mesh.get("jianghai_density_color0_source_surfaces") != expected_source_surfaces
        or mesh.get("jianghai_density_color0_output_surfaces") != 1
        or mesh.get("jianghai_density_color0_opaque") is not True
    ):
        raise RuntimeError(f"Density COLOR_0 metadata drifted: {profile_name}")
    if len(mesh.materials) != 1 or mesh.materials[0].name != expected_material_name:
        raise RuntimeError(
            f"Density COLOR_0 material surface drifted: {profile_name} "
            f"materials={[material.name if material else None for material in mesh.materials]}"
        )
    if any(polygon.material_index != 0 for polygon in mesh.polygons):
        raise RuntimeError(f"Density COLOR_0 mesh retained split surfaces: {profile_name}")

    attribute = mesh.color_attributes.get(DENSITY_COLOR0_ATTRIBUTE)
    if (
        attribute is None
        or attribute.domain != "CORNER"
        or attribute.data_type != "FLOAT_COLOR"
        or len(attribute.data) != len(mesh.loops)
    ):
        raise RuntimeError(f"Density COLOR_0 corner layer drifted: {profile_name}")
    colors = {
        tuple(float(component) for component in element.color)
        for element in attribute.data
    }
    if len(colors) != expected_source_surfaces:
        raise RuntimeError(
            f"Density COLOR_0 palette drifted: {profile_name} "
            f"actual={len(colors)} expected={expected_source_surfaces}"
        )
    if any(
        not isfinite(component) or component < 0.0 or component > 1.0
        for color in colors
        for component in color
    ):
        raise RuntimeError(f"Density COLOR_0 values are invalid: {profile_name}")
    palette_sha256 = _palette_digest(colors)
    stored_digest = mesh.get("jianghai_density_color0_palette_sha256")
    if stored_digest is not None and stored_digest != palette_sha256:
        raise RuntimeError(
            f"Density COLOR_0 palette hash drifted: {profile_name} "
            f"actual={palette_sha256} expected={stored_digest}"
        )

    material = mesh.materials[0]
    principled = _principled(material)
    vertex_colors = [
        node for node in material.node_tree.nodes if node.type == "VERTEX_COLOR"
    ]
    base_color = principled.inputs["Base Color"]
    if (
        len(vertex_colors) != 1
        or vertex_colors[0].layer_name != DENSITY_COLOR0_ATTRIBUTE
        or not base_color.is_linked
        or base_color.links[0].from_node != vertex_colors[0]
        or abs(float(principled.inputs["Roughness"].default_value)
               - DENSITY_COLOR0_PROFILE_ROUGHNESS[profile_name]) > SCALAR_TOLERANCE
        or abs(float(principled.inputs["Metallic"].default_value)) > SCALAR_TOLERANCE
        or abs(float(principled.inputs["Alpha"].default_value) - 1.0) > SCALAR_TOLERANCE
        or any(node.type == "TEX_IMAGE" for node in material.node_tree.nodes)
        or material.get("jianghai_density_color0_opaque") is not True
    ):
        raise RuntimeError(f"Density COLOR_0 shader contract drifted: {profile_name}")
    maximum_error = float(mesh.get("jianghai_density_color0_max_error", float("inf")))
    if maximum_error > COLOR_TOLERANCE:
        raise RuntimeError(
            f"Density COLOR_0 color error exceeded tolerance: {profile_name} "
            f"error={maximum_error:.9f}"
        )
    return DensityColor0MeshMetrics(
        profile_name=profile_name,
        triangle_count=_triangle_count(mesh),
        source_surface_count=expected_source_surfaces,
        output_surface_count=1,
        distinct_color_count=len(colors),
        palette_sha256=palette_sha256,
        maximum_color_error=maximum_error,
    )


def validate_density_color0_scene() -> DensityColor0SceneMetrics:
    metrics = []
    meshes = {}
    for profile_name, mesh_name in QUATERNIUS_DENSITY_MESHES.items():
        mesh = bpy.data.meshes.get(mesh_name)
        if mesh is None:
            raise RuntimeError(f"Density COLOR_0 mesh is missing: {mesh_name}")
        meshes[profile_name] = mesh
        metrics.append(validate_density_profile_mesh(mesh, profile_name))

    expected_instances = {
        f"JianghaiDensity_{suffix}": profile_name
        for suffix, profile_name, _, _, _ in DENSITY_BUILDING_LAYOUT
        if profile_name in QUATERNIUS_DENSITY_MESHES
    }
    for object_name, profile_name in expected_instances.items():
        obj = bpy.data.objects.get(object_name)
        if obj is None or obj.type != "MESH" or obj.data != meshes[profile_name]:
            raise RuntimeError(
                f"Density COLOR_0 shared instance drifted: {object_name} profile={profile_name}"
            )
    infill_names = {
        f"JianghaiDensity_{suffix}" for suffix in DENSITY_COLOR0_INFILL_SUFFIXES
    }
    if not infill_names.issubset(expected_instances):
        raise RuntimeError("Density COLOR_0 infill contract drifted from the layout")
    return DensityColor0SceneMetrics(
        profile_count=len(metrics),
        profile_surface_count=sum(metric.output_surface_count for metric in metrics),
        instance_count=len(expected_instances),
        instance_surface_count=len(expected_instances),
        infill_instance_count=len(infill_names),
        infill_surface_count=len(infill_names),
    )
