"""Print deterministic geometry, image, and instancing statistics for Jianghai Old City."""

from __future__ import annotations

from collections import Counter
from collections.abc import Mapping
from math import isclose, isfinite, pi
import sys

import bpy
from mathutils import Vector


def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def evaluated_geometry_statistics() -> tuple[int, int, Counter[str]]:
    """Count the geometry Blender actually evaluates for the scene export."""

    depsgraph = bpy.context.evaluated_depsgraph_get()
    geometry_objects = 0
    triangles = 0
    triangles_by_type: Counter[str] = Counter()
    for instance in depsgraph.object_instances:
        obj = instance.object
        # Blender exposes each bevelled Curve twice here: once as the source
        # CURVE and once as its evaluated MESH. Counting only evaluated MESH
        # instances matches glTF/Godot and avoids double-counting every curve.
        if obj.type != "MESH":
            continue
        mesh = obj.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        if mesh is None:
            continue
        try:
            mesh.calc_loop_triangles()
            object_triangles = len(mesh.loop_triangles)
            triangles += object_triangles
            triangles_by_type[obj.type] += object_triangles
            geometry_objects += 1
        finally:
            obj.to_mesh_clear()
    return geometry_objects, triangles, triangles_by_type


mesh_users = Counter(obj.data.name for obj in bpy.context.scene.objects if obj.type == "MESH")
rows = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
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

evaluated_objects, evaluated_triangles, evaluated_by_type = evaluated_geometry_statistics()
print(
    "JIANGHAI_EVALUATED_GEOMETRY "
    f"objects={evaluated_objects} triangles={evaluated_triangles} "
    f"by_type={','.join(f'{key}:{value}' for key, value in sorted(evaluated_by_type.items()))}"
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
forbidden_export_objects = sorted(
    obj.name
    for obj in bpy.context.scene.objects
    if obj.name.startswith("__SOURCE_") or obj.type in {"ARMATURE", "FONT"} or obj.library is not None
)
finite_transforms = all(
    all(isfinite(value) for row in obj.matrix_world for value in row)
    for obj in bpy.context.scene.objects
)


def iter_property_text(value, depth=0):
    """Yield nested custom-property text without trusting marketplace metadata."""

    if depth > 8:
        return
    if isinstance(value, Mapping) or hasattr(value, "items"):
        for key, nested in value.items():
            yield str(key)
            yield from iter_property_text(nested, depth + 1)
        return
    if hasattr(value, "to_list"):
        yield from iter_property_text(value.to_list(), depth + 1)
        return
    if isinstance(value, (list, tuple)):
        for nested in value:
            yield from iter_property_text(nested, depth + 1)
        return
    yield str(value)


marketplace_marker_tokens = (
    "3d66",
    "www.3d66.com",
    "chinese wood house wall",
    "blenderkit_wood_house",
    "scan old brick building red small",
    "blenderkit_old_brick_factory",
    "fc8376f8-7c79-48b3-8a3c-bf061ace53e0",
)
marketplace_marker_hits = []
custom_property_sources = [
    *(f"object:{item.name}" for item in bpy.data.objects),
    *(f"mesh:{item.name}" for item in bpy.data.meshes),
    *(f"material:{item.name}" for item in bpy.data.materials),
    *(f"image:{item.name}" for item in bpy.data.images),
    *(f"collection:{item.name}" for item in bpy.data.collections),
    *(f"scene:{item.name}" for item in bpy.data.scenes),
    *(f"world:{item.name}" for item in bpy.data.worlds),
    *(f"node_group:{item.name}" for item in bpy.data.node_groups),
]
custom_property_blocks = [
    *bpy.data.objects,
    *bpy.data.meshes,
    *bpy.data.materials,
    *bpy.data.images,
    *bpy.data.collections,
    *bpy.data.scenes,
    *bpy.data.worlds,
    *bpy.data.node_groups,
]
for source_name, block in zip(custom_property_sources, custom_property_blocks, strict=True):
    custom_text = " ".join(iter_property_text({key: block[key] for key in block.keys()})).lower()
    matching_tokens = sorted(token for token in marketplace_marker_tokens if token in custom_text)
    if matching_tokens:
        marketplace_marker_hits.append((source_name, matching_tokens))

urban_life_names = {
    "JianghaiExpansion_UrbanFacades",
    "JianghaiExpansion_StreetLife",
    "JianghaiExpansion_Bicycle00",
    "JianghaiExpansion_Bicycle01",
    "JianghaiExpansion_Bicycle02",
    "JianghaiExpansion_MarketTeaCart",
    "JianghaiExpansion_MarketWickerBasket",
    "JianghaiExpansion_PawnshopTeaTable",
    "JianghaiExpansion_PawnshopStool00",
    "JianghaiExpansion_PawnshopStool01",
    "JianghaiExpansion_PawnshopStool02",
    "JianghaiExpansion_PawnshopBackdrop",
    "JianghaiExpansion_FactoryHandTruck",
    "JianghaiExpansion_WestClockLantern",
    "JianghaiCleared_MarketTeaTable",
    "JianghaiCleared_MarketStool00",
    "JianghaiCleared_MarketStool01",
    "JianghaiCleared_MarketStool02",
}
urban_life_ready = urban_life_names.issubset(bpy.data.objects.keys())
facade_expansion = [
    obj
    for obj in bpy.context.scene.objects
    if obj.name.startswith("JianghaiExpansion_Facade_")
]
facade_expansion_count = len(facade_expansion)
facade_expansion_aligned = all(
    (
        "_EastPhoto_" in obj.name
        and isclose(obj.location.x, 13.38, abs_tol=0.001)
        and isclose(obj.rotation_euler.z, pi * 0.5, abs_tol=0.001)
        and min((obj.matrix_world @ Vector(corner)).x for corner in obj.bound_box) >= 13.0
    )
    or (
        "_WestClock_" in obj.name
        and isclose(obj.location.x, -13.48, abs_tol=0.001)
        and isclose(obj.rotation_euler.z, -pi * 0.5, abs_tol=0.001)
        and max((obj.matrix_world @ Vector(corner)).x for corner in obj.bound_box) <= -13.0
    )
    for obj in facade_expansion
)
legacy_wood_house_nodes = sorted(
    obj.name
    for obj in bpy.data.objects
    if obj.name.startswith(("LingnanTimberShop", "ElevatedMarketShop"))
)
floating_market_signs_removed = not any(
    object_name in bpy.data.objects
    for object_name in (
        "OldCityMarketSignBacking",
        "OldCityMarketSignText",
        "OldCityMarketBuySignBacking",
        "OldCityMarketBuySignText",
        "OldCityMarketPawnSignBacking",
        "OldCityMarketPawnSignText",
    )
)
replacement_pawnshop = bpy.data.objects.get("JianghaiCleared_PawnshopStorefront")
pawnshop_root = bpy.data.objects.get("GuangchangPawnshop")
pawnshop_legacy_gate_names = {
    "GuangchangPawnshopSignBacking",
    "GuangchangPawnshopDangPlaqueBacking",
    "PawnshopGatePierL",
    "PawnshopGatePierR",
    "PawnshopGatePierCapL",
    "PawnshopGatePierCapR",
}
pawnshop_legacy_visible_names = pawnshop_legacy_gate_names.intersection(bpy.data.objects.keys())
pawnshop_legacy_visible_names.update(
    obj.name
    for obj in bpy.data.objects
    if obj.name.startswith(
        (
            "PawnshopSouthEast_",
            "PawnshopSouthEastCap_",
            "PawnshopSouthWest_",
            "PawnshopSouthWestCap_",
        )
    )
)
pawnshop_canopy_root = bpy.data.objects.get("PawnshopAuthoredPavilionGate")
pawnshop_canopy_parts = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("PawnshopAuthoredCanopy_")
    ),
    key=lambda obj: obj.name,
)
pawnshop_wings = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("PawnshopAuthoredWing_")
    ),
    key=lambda obj: obj.name,
)
pawnshop_canopy_triangles = sum(triangle_count(obj.data) for obj in pawnshop_canopy_parts)
pawnshop_wall_wings = [obj for obj in pawnshop_wings if obj.name.endswith("_Wall")]
pawnshop_insert_wings = [obj for obj in pawnshop_wings if obj.name.endswith("_Insert")]
pawnshop_columns_clear = all(
    not (-90.0 < obj.matrix_world.translation.x < -82.0)
    for obj in pawnshop_canopy_parts
    if str(obj.get("source_part_name", "")).startswith("檐柱")
)
pawnshop_frontage_ready = (
    not pawnshop_legacy_visible_names
    and pawnshop_root is not None
    and pawnshop_canopy_root is not None
    and pawnshop_canopy_root.parent == pawnshop_root
    and pawnshop_canopy_root.get("source_license") == "CC0 1.0 Universal"
    and pawnshop_canopy_root.get("source_creator") == "VVayToyek"
    and pawnshop_canopy_root.get("source_url")
    == "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free"
    and len(pawnshop_canopy_parts) == 15
    and pawnshop_canopy_triangles >= 15_000
    and all(obj.parent == pawnshop_canopy_root for obj in pawnshop_canopy_parts)
    and all(obj.type == "MESH" and len(obj.data.vertices) > 8 for obj in pawnshop_canopy_parts)
    and len(pawnshop_wall_wings) == 8
    and len(pawnshop_insert_wings) == 8
    and all(obj.parent == pawnshop_root for obj in pawnshop_wings)
    and all(
        obj.get("source_creator") == "James Ray Cock"
        and obj.get("source_url")
        == "https://polyhaven.com/a/modular_urban_apartments_facade"
        and obj.get("source_license") == "CC0 1.0 Universal"
        for obj in pawnshop_wings
    )
    and all(min(obj.dimensions.x, obj.dimensions.y) >= 0.17 for obj in pawnshop_wall_wings)
    and pawnshop_columns_clear
)
replacement_market_shops = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("JianghaiCleared_MarketShop")
    ),
    key=lambda obj: obj.name,
)
replacement_factory_buildings = sorted(
    (
        obj
        for obj in bpy.data.objects
        if obj.name.startswith("JianghaiCleared_Factory")
    ),
    key=lambda obj: obj.name,
)


def is_cleared_authored_storefront(obj, expected_parent):
    source_url = str(obj.get("source_url", "")).lower() if obj is not None else ""
    return (
        obj is not None
        and obj.type == "MESH"
        and obj.data is not None
        and obj.data.name in {"Cube.286", "hhugu.001"}
        and obj.parent == expected_parent
        and len(obj.material_slots) > 0
        and "cc0" in str(obj.get("license", "")).lower()
        and (
            "blenderkit.com/asset-gallery-detail/8177ff94" in source_url
            or "blenderkit.com/asset-gallery-detail/d8c0ffa6" in source_url
        )
        and all(isfinite(value) for row in obj.matrix_world for value in row)
    )


replacement_storefronts_ready = (
    is_cleared_authored_storefront(replacement_pawnshop, bpy.data.objects.get("GuangchangPawnshop"))
    and len(replacement_market_shops) == 5
    and sum(shop.data.name == "Cube.286" for shop in replacement_market_shops) == 3
    and sum(shop.data.name == "hhugu.001" for shop in replacement_market_shops) == 2
    and all(
        is_cleared_authored_storefront(shop, bpy.data.objects.get("OldCityMarketBridge"))
        for shop in replacement_market_shops
    )
    and not legacy_wood_house_nodes
    and not marketplace_marker_hits
    and floating_market_signs_removed
    and not any(obj.name.startswith(("MarketCanopy", "MarketAwning", "MarketLantern")) for obj in bpy.data.objects)
)
replacement_factory_ready = (
    len(replacement_factory_buildings) == 5
    and sum(building.data.name == "Cube.286" for building in replacement_factory_buildings) == 3
    and sum(building.data.name == "hhugu.001" for building in replacement_factory_buildings) == 2
    and all(
        is_cleared_authored_storefront(building, bpy.data.objects.get("RedStarElectronicsFactory"))
        for building in replacement_factory_buildings
    )
    and not any(
        object_name in bpy.data.objects
        for object_name in ("RedStarFactoryMainBuilding", "RedStarLoadingBayWest", "RedStarLoadingBayEast")
    )
    and not any(obj.name.startswith("RedStarMainFacade_") for obj in bpy.data.objects)
)
root = bpy.data.objects.get("JianghaiOldCityAuthoredScene")
root_provenance_ready = (
    root is not None
    and "blenderkit_wood_house" not in root
    and "blenderkit_old_brick_factory" not in root
    and "poly_haven_apartments_evaluated_not_used" not in root
    and "poly_haven_apartments" in root
    and "cleared_storefront_pass" in root
)
valid = (
    not missing_anchors
    and all(terminal_checks)
    and terminal_orientation_ready
    and facade_props_ready
    and factory_duplicate_shutter_removed
    and factory_gate_portal_ready
    and not legacy_wood_house_nodes
    and not marketplace_marker_hits
    and evaluated_triangles <= 5_000_000
    and images_ready
    and not forbidden_export_objects
    and finite_transforms
    and urban_life_ready
    and facade_expansion_count == 36
    and facade_expansion_aligned
    and replacement_storefronts_ready
    and pawnshop_frontage_ready
    and replacement_factory_ready
    and root_provenance_ready
)
print(
    f"JIANGHAI_PASS valid={valid} anchors={len(required_anchors) - len(missing_anchors)}/{len(required_anchors)} "
    f"terminals={sum(terminal_checks)}/{len(terminal_checks)} terminal_orientation="
    f"{terminal_orientation_ready} facade_props={len(facade_props)}/22 facade_props_aligned="
    f"{facade_props_ready} factory_duplicate_shutter_removed={factory_duplicate_shutter_removed} "
    f"factory_gate_portal={sum(obj is not None for obj in factory_gate_objects)}/5 "
    f"factory_gate_portal_aligned={factory_gate_portal_ready} "
    f"legacy_wood_house_nodes={len(legacy_wood_house_nodes)} "
    f"marketplace_marker_hits={len(marketplace_marker_hits)} "
    f"floating_market_signs_removed={floating_market_signs_removed} "
    f"images_1k_packed={images_ready} evaluated_triangles={evaluated_triangles}/5000000 "
    f"forbidden_export_objects={len(forbidden_export_objects)} finite_transforms={finite_transforms} "
    f"urban_life={urban_life_ready} facade_expansion={facade_expansion_count}/36 "
    f"facade_expansion_aligned={facade_expansion_aligned} "
    f"replacement_storefronts_ready={replacement_storefronts_ready} "
    f"pawnshop_frontage_ready={pawnshop_frontage_ready} "
    f"pawnshop_canopy={len(pawnshop_canopy_parts)}/15 "
    f"pawnshop_canopy_triangles={pawnshop_canopy_triangles}/15000 "
    f"pawnshop_wings={len(pawnshop_wings)}/16 "
    f"pawnshop_legacy_visible={len(pawnshop_legacy_visible_names)} "
    f"pawnshop_columns_clear={pawnshop_columns_clear} "
    f"replacement_factory_ready={replacement_factory_ready} "
    f"root_provenance_ready={root_provenance_ready}"
)
for source_name, tokens in marketplace_marker_hits:
    print(f"JIANGHAI_MARKETPLACE_MARKER source={source_name!r} tokens={','.join(tokens)}")
if not valid:
    sys.exit(2)
