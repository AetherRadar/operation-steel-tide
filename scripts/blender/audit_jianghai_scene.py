"""Print deterministic geometry, image, and instancing statistics for Jianghai Old City."""

from __future__ import annotations

from collections import Counter
from math import isclose, pi
import sys

import bpy
from mathutils import Vector


def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


mesh_users = Counter(obj.data.name for obj in bpy.context.scene.objects if obj.type == "MESH")
rows = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH" or obj.name.startswith("__SOURCE_"):
        continue
    rows.append((triangle_count(obj.data), len(obj.data.vertices), mesh_users[obj.data.name], obj.name, obj.data.name))

print("JIANGHAI_AUDIT_BEGIN")
for triangles, vertices, users, object_name, mesh_name in sorted(rows, reverse=True)[:80]:
    print(
        f"JIANGHAI_MESH triangles={triangles} vertices={vertices} users={users} "
        f"object={object_name!r} data={mesh_name!r}"
    )

images = []
for image in bpy.data.images:
    if image.type != "IMAGE":
        continue
    width, height = image.size
    images.append((width * height, width, height, image.source, image.packed_file is not None, image.name, image.filepath))
for _, width, height, source, packed, name, filepath in sorted(images, reverse=True):
    print(
        f"JIANGHAI_IMAGE size={width}x{height} source={source} packed={packed} "
        f"name={name!r} path={filepath!r}"
    )

print(
    f"JIANGHAI_AUDIT_END objects={len(rows)} unique_meshes={len(mesh_users)} "
    f"object_triangles={sum(row[0] for row in rows)} "
    f"unique_triangles={sum(triangle_count(mesh) for mesh in bpy.data.meshes if mesh.users > 0)}"
)

required_anchors = {
    "AuthoredStreetNetwork",
    "JianghaiTenementDistrict",
    "RedStarElectronicsFactory",
    "GuangchangPawnshop",
    "OldCityMarketBridge",
    "GrandHotelSecurityTerminalVisual",
    "MunicipalTreasuryManifestTerminalVisual",
}
missing_anchors = sorted(required_anchors.difference(bpy.data.objects.keys()))
terminal_checks = []
for terminal_name in (
    "GrandHotelSecurityTerminalVisual",
    "MunicipalTreasuryManifestTerminalVisual",
):
    terminal = bpy.data.objects.get(terminal_name)
    meshes = [] if terminal is None else [child for child in terminal.children_recursive if child.type == "MESH"]
    finished_parts = [
        child for child in meshes
        if child.name.startswith("JianghaiArtPass_")
        and ("_CRT" in child.name or "Weather" in child.name)
        and triangle_count(child.data) >= 2_000
    ]
    terminal_checks.append(
        terminal is not None
        and len(meshes) == 7
        and len(finished_parts) == 2
        and any("AuthoredStatusScreen" in child.name for child in meshes)
    )

grand_root = bpy.data.objects.get("GrandHotelSecurityTerminalVisual")
municipal_root = bpy.data.objects.get("MunicipalTreasuryManifestTerminalVisual")
grand_screen = bpy.data.objects.get("GrandHotelSecurityTerminalVisual_AuthoredStatusScreen")
municipal_screen = bpy.data.objects.get("MunicipalTreasuryManifestTerminalVisual_AuthoredStatusScreen")
terminal_orientation_ready = (
    grand_root is not None
    and municipal_root is not None
    and grand_screen is not None
    and municipal_screen is not None
    and isclose(grand_root.rotation_euler.z, 0.0, abs_tol=0.001)
    and isclose(municipal_root.rotation_euler.z, 0.0, abs_tol=0.001)
    and grand_screen.matrix_world.translation.y < grand_root.matrix_world.translation.y - 0.15
    and municipal_screen.matrix_world.translation.y > municipal_root.matrix_world.translation.y + 0.15
)

facade_props = [
    obj
    for obj in bpy.context.scene.objects
    if obj.name.startswith(("JianghaiArtPass_EastAircon", "JianghaiArtPass_WestAircon"))
    or obj.name.startswith(("JianghaiArtPass_EastShutter", "JianghaiArtPass_WestShutter"))
]
facade_props_ready = len(facade_props) == 22
for prop in facade_props:
    bounds_x = [(prop.matrix_world @ Vector(corner)).x for corner in prop.bound_box]
    east_side = "_East" in prop.name
    expected_yaw = -pi * 0.5 if east_side else pi * 0.5
    facade_props_ready &= (
        isclose(prop.rotation_euler.z, expected_yaw, abs_tol=0.001)
        and (min(bounds_x) >= 9.5 if east_side else max(bounds_x) <= -9.5)
    )

factory_duplicate_shutter_removed = (
    bpy.data.objects.get("JianghaiArtPass_FactoryHeroShutter") is None
)

factory_gate_names = (
    "FactoryGatePortal_PierL",
    "FactoryGatePortal_PierR",
    "FactoryGatePortal_PierCapL",
    "FactoryGatePortal_PierCapR",
    "FactoryGatePortal_Roof",
)
factory_gate_objects = [bpy.data.objects.get(name) for name in factory_gate_names]
factory_gate_root = bpy.data.objects.get("RedStarElectronicsFactory")
factory_gate_portal_ready = all(obj is not None for obj in factory_gate_objects)
if factory_gate_portal_ready:
    left_pier, right_pier, left_cap, right_cap, roof = factory_gate_objects

    def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
        corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        return (
            Vector(tuple(min(point[axis] for point in corners) for axis in range(3))),
            Vector(tuple(max(point[axis] for point in corners) for axis in range(3))),
        )

    left_min, left_max = world_bounds(left_pier)
    right_min, right_max = world_bounds(right_pier)
    roof_min, roof_max = world_bounds(roof)
    door_half_width = 7.2956 * 0.5
    factory_gate_portal_ready &= (
        all(obj.parent == factory_gate_root for obj in factory_gate_objects)
        and all(isclose(obj.matrix_world.translation.y, 7.9245, abs_tol=0.001) for obj in factory_gate_objects[:4])
        and isclose(left_max.x, 86.0 - door_half_width, abs_tol=0.002)
        and isclose(right_min.x, 86.0 + door_half_width, abs_tol=0.002)
        and left_min.z <= 0.001
        and right_min.z <= 0.001
        and left_max.z >= 4.39
        and right_max.z >= 4.39
        and roof_min.x <= left_min.x - 0.5
        and roof_max.x >= right_max.x + 0.5
        and roof_min.y <= 7.9245 - 1.0
        and roof_max.y >= 7.9245 + 1.0
        and roof_min.z <= 4.22
        and roof_max.z >= 5.9
        and isclose(left_cap.matrix_world.translation.x, left_pier.matrix_world.translation.x, abs_tol=0.001)
        and isclose(right_cap.matrix_world.translation.x, right_pier.matrix_world.translation.x, abs_tol=0.001)
    )

object_triangles = sum(row[0] for row in rows)
images_ready = all(
    packed and width <= 1_024 and height <= 1_024
    for _, width, height, _, packed, _, _ in images
)
valid = (
    not missing_anchors
    and all(terminal_checks)
    and terminal_orientation_ready
    and facade_props_ready
    and factory_duplicate_shutter_removed
    and factory_gate_portal_ready
    and bpy.data.objects.get("LingnanTimberShop04") is None
    and object_triangles <= 5_000_000
    and images_ready
)
print(
    f"JIANGHAI_PASS valid={valid} anchors={len(required_anchors) - len(missing_anchors)}/{len(required_anchors)} "
    f"terminals={sum(terminal_checks)}/{len(terminal_checks)} terminal_orientation="
    f"{terminal_orientation_ready} facade_props={len(facade_props)}/22 facade_props_aligned="
    f"{facade_props_ready} factory_duplicate_shutter_removed={factory_duplicate_shutter_removed} "
    f"factory_gate_portal={sum(obj is not None for obj in factory_gate_objects)}/5 "
    f"factory_gate_portal_aligned={factory_gate_portal_ready} "
    f"lane_blocker_removed={bpy.data.objects.get('LingnanTimberShop04') is None} "
    f"images_1k_packed={images_ready}"
)
if not valid:
    sys.exit(2)
