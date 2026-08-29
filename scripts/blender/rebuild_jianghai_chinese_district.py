"""Rebuild Jianghai's visible district from its licensed authored Chinese sources.

Run with Blender 4.5+::

    blender -b source_art/world/jianghai_old_city/jianghai_old_city.blend \
        --python scripts/blender/rebuild_jianghai_chinese_district.py \
        -- --render-previews

The pass is deterministic and idempotent.  It replaces every delivered instance
of the two retired ruin meshes, adds six set-back edge buildings, preserves the
named gameplay anchors, and saves the authoritative blend.  No acquisition-cache
asset is read: all geometry comes from CC0 sources already packed in the scene.
"""

from __future__ import annotations

from math import radians
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from jianghai_chinese_district_layout import (
    DENSITY_BUILDING_LAYOUT, JIANGHAI_DEPLOYMENT_POINTS, OLD_URBAN_TARGETS, PROFILE_BASE_SCALE,
    QUATERNIUS_DENSITY_MESHES, SHOP_TARGETS,
)
from jianghai_enterable_residences import apply_enterable_residences
REPO_ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = REPO_ROOT / "source_art" / "world" / "jianghai_old_city" / "jianghai_old_city.blend"
PREVIEW_DIR = BLEND_PATH.parent / "previews"
TEMPLE_SOURCE_OBJECT = "GuangchangClanHall"
TEMPLE_SOURCE_URL = (
    "https://www.blenderkit.com/asset-gallery-detail/"
    "8701a79a-1635-437c-b1d2-6b14f14fc351/"
)
TEMPLE_CREATOR = "Free poly"
LICENSE = "CC0 1.0 Universal"
REBUILD_VERSION = 1
MAX_INSTANCE_TRIANGLES = 3_200_000

HALL_MESH_NAME = "JianghaiChineseTempleHall_LOD"
SHOP_MESH_NAME = "JianghaiChineseArcadeShop_LOD"
GATE_MESH_NAME = "JianghaiChineseGateHouse_LOD"
ROOF_KIT_MESH_NAME = "JianghaiChineseTempleRoofKit_LOD"
DENSITY_HALL_MESH_NAME = "JianghaiDensity_ChineseTempleHall_LOD"
DENSITY_SHOP_MESH_NAME = "JianghaiDensity_ChineseArcadeShop_LOD"
DENSITY_GATE_MESH_NAME = "JianghaiDensity_ChineseGateHouse_LOD"
RETIRED_MESH_NAMES = {
    "Cube.286",
    "hhugu.001",
    "JianghaiDensity_OldUrban_LOD",
    "JianghaiDensity_ScanStreet_LOD",
}

def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def build_temple_lod(mesh_name: str, ratio: float, role: str) -> bpy.types.Mesh:
    source = bpy.data.objects.get(TEMPLE_SOURCE_OBJECT)
    if source is None or source.type != "MESH":
        raise RuntimeError(f"Packed Chinese Temple 2 source is missing: {TEMPLE_SOURCE_OBJECT}")
    previous = bpy.data.meshes.get(mesh_name)
    if previous is not None:
        previous.name = f"__RETIRED_{mesh_name}"

    template = source.copy()
    template.data = source.data.copy()
    template.name = f"__JIANGHAI_REBUILD_{role}"
    template.parent = None
    template.matrix_world = Matrix.Identity(4)
    bpy.context.scene.collection.objects.link(template)
    bpy.ops.object.select_all(action="DESELECT")
    template.select_set(True)
    bpy.context.view_layer.objects.active = template
    modifier = template.modifiers.new(name="JianghaiAuthoredDistanceLOD", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    mesh = template.data
    coordinates = [vertex.co for vertex in mesh.vertices]
    center_x = (min(v.x for v in coordinates) + max(v.x for v in coordinates)) * 0.5
    center_y = (min(v.y for v in coordinates) + max(v.y for v in coordinates)) * 0.5
    minimum_z = min(v.z for v in coordinates)
    mesh.transform(Matrix.Translation((-center_x, -center_y, -minimum_z)))
    mesh.name = mesh_name
    mesh["source_asset"] = "Chinese Temple 2"
    mesh["source_creator"] = TEMPLE_CREATOR
    mesh["source_url"] = TEMPLE_SOURCE_URL
    mesh["license"] = LICENSE
    mesh["authored_derivation"] = (
        f"Project-authored Blender 4.5 Chinese district {role}; decimated ratio {ratio:.3f}"
    )
    mesh["jianghai_chinese_rebuild_version"] = REBUILD_VERSION
    bpy.data.objects.remove(template, do_unlink=True)
    return mesh


def normalize_mesh(mesh: bpy.types.Mesh) -> None:
    coordinates = [vertex.co for vertex in mesh.vertices]
    center_x = (min(v.x for v in coordinates) + max(v.x for v in coordinates)) * 0.5
    center_y = (min(v.y for v in coordinates) + max(v.y for v in coordinates)) * 0.5
    mesh.transform(Matrix.Translation((-center_x, -center_y, -min(v.z for v in coordinates))))


def decimate_mesh(source: bpy.types.Mesh, mesh_name: str, ratio: float, role: str) -> bpy.types.Mesh:
    previous = bpy.data.meshes.get(mesh_name)
    if previous is not None:
        previous.name = f"__RETIRED_{mesh_name}"
    template = bpy.data.objects.new(f"__JIANGHAI_REBUILD_{role}", source.copy())
    bpy.context.scene.collection.objects.link(template)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = template
    template.select_set(True)
    modifier = template.modifiers.new(name="JianghaiAuthoredDistanceLOD", type="DECIMATE")
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh = template.data
    normalize_mesh(mesh)
    mesh.name = mesh_name
    for key in source.keys():
        mesh[key] = source[key]
    mesh["authored_derivation"] = f"Project-authored Blender 4.5 {role}; decimated ratio {ratio:.3f}"
    mesh["jianghai_chinese_rebuild_version"] = REBUILD_VERSION
    bpy.data.objects.remove(template, do_unlink=True)
    return mesh


def build_temple_roof_kit() -> bpy.types.Mesh:
    source = bpy.data.objects[TEMPLE_SOURCE_OBJECT]
    previous = bpy.data.meshes.get(ROOF_KIT_MESH_NAME)
    if previous is not None:
        previous.name = f"__RETIRED_{ROOF_KIT_MESH_NAME}"
    mesh = source.data.copy()
    editable = bmesh.new()
    editable.from_mesh(mesh)
    bmesh.ops.delete(
        editable,
        geom=[face for face in editable.faces if face.calc_center_median().z < 7.5],
        context="FACES",
    )
    bmesh.ops.delete(editable, geom=[vertex for vertex in editable.verts if not vertex.link_faces], context="VERTS")
    editable.to_mesh(mesh)
    editable.free()
    normalize_mesh(mesh)
    mesh["source_asset"] = "Chinese Temple 2"
    mesh["source_creator"] = TEMPLE_CREATOR
    mesh["source_url"] = TEMPLE_SOURCE_URL
    mesh["license"] = LICENSE
    return decimate_mesh(mesh, ROOF_KIT_MESH_NAME, 0.075, "Chinese Temple roof kit LOD")


def build_pavilion_composite(
    mesh_name: str,
    body_mesh_name: str,
    body_dimensions: tuple[float, float, float],
    roof_mesh: bpy.types.Mesh,
    roof_dimensions: tuple[float, float, float],
    roof_z: float,
    canopy_scale: tuple[float, float, float],
    ratio: float,
    role: str,
) -> bpy.types.Mesh:
    previous = bpy.data.meshes.get(mesh_name)
    if previous is not None:
        previous.name = f"__RETIRED_{mesh_name}"
    body_mesh = bpy.data.meshes.get(body_mesh_name)
    if body_mesh is None:
        raise RuntimeError(f"Packed Quaternius body is missing: {body_mesh_name}")
    body = bpy.data.objects.new(f"__JIANGHAI_REBUILD_{role}_BODY", body_mesh.copy())
    bpy.context.scene.collection.objects.link(body)
    normalize_mesh(body.data)
    body.dimensions = body_dimensions
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    roof = bpy.data.objects.new(f"__JIANGHAI_REBUILD_{role}_ROOF", roof_mesh)
    bpy.context.scene.collection.objects.link(roof)
    roof.dimensions = roof_dimensions
    roof.location.z = roof_z
    bpy.context.view_layer.update()
    roof.select_set(True)
    transform = (
        Matrix.Diagonal((*canopy_scale, 1.0))
        @ Matrix.Translation((86.0, -112.0, 0.0))
    )
    for index in range(15):
        source = bpy.data.objects.get(f"PawnshopAuthoredCanopy_{index:02d}")
        if source is None or source.type != "MESH":
            raise RuntimeError(f"Packed pavilion component is missing: {index:02d}")
        part = source.copy()
        part.data = source.data
        part.name = f"__JIANGHAI_REBUILD_{role}_CANOPY_{index:02d}"
        part.parent = None
        bpy.context.scene.collection.objects.link(part)
        part.matrix_world = transform @ source.matrix_world
        part.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    modifier = body.modifiers.new(name="JianghaiAuthoredCompositeLOD", type="DECIMATE")
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh = body.data
    normalize_mesh(mesh)
    mesh.name = mesh_name
    mesh["source_asset"] = "Chinese Four-corner Pavilion - Free; Quaternius Buildings Pack; Chinese Temple 2"
    mesh["source_creator"] = "VVayToyek; Quaternius; Free poly"
    mesh["source_url"] = (
        "https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free; "
        f"https://quaternius.com/packs/buildings.html; {TEMPLE_SOURCE_URL}"
    )
    mesh["license"] = LICENSE
    mesh["authored_derivation"] = f"Project-authored joined pavilion-fronted {role} composition"
    mesh["jianghai_chinese_rebuild_version"] = REBUILD_VERSION
    bpy.data.objects.remove(body, do_unlink=True)
    return mesh


def fit_object_to_mesh(obj: bpy.types.Object, mesh: bpy.types.Mesh, role: str) -> None:
    desired_dimensions = obj.dimensions.copy()
    obj.data = mesh
    obj.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    obj.dimensions = desired_dimensions
    bpy.context.view_layer.update()
    obj["source_asset"] = mesh.get("source_asset", "Chinese Temple 2")
    obj["source_creator"] = mesh.get("source_creator", TEMPLE_CREATOR)
    obj["source_url"] = mesh.get("source_url", TEMPLE_SOURCE_URL)
    obj["license"] = LICENSE
    obj["authored_adaptation"] = (
        "Project-authored clean Chinese roof-hall composition fitted to the retained gameplay anchor"
    )
    obj["district_role"] = role
    obj["collision_role"] = "building_shell"
    obj["building_id"] = obj.name
    if obj.name == "JianghaiCleared_PawnshopStorefront":
        for key in ("doorway_cut_version", "doorway_width_m", "doorway_height_m"):
            if key in obj:
                del obj[key]


def replace_retired_buildings(
    hall_mesh: bpy.types.Mesh,
    shop_mesh: bpy.types.Mesh,
    gate_mesh: bpy.types.Mesh,
) -> tuple[int, dict[str, int]]:
    missing = [name for name in OLD_URBAN_TARGETS if bpy.data.objects.get(name) is None]
    if missing:
        raise RuntimeError(f"Named Jianghai gameplay anchors are missing: {missing}")
    counts = {"hall": 0, "shop": 0, "gate": 0}
    for name in OLD_URBAN_TARGETS:
        obj = bpy.data.objects[name]
        if obj.type != "MESH":
            raise RuntimeError(f"Jianghai building anchor is not a mesh: {name}")
        if name in SHOP_TARGETS or sum(map(ord, name)) % 5 == 0:
            profile, mesh = "shop", shop_mesh
        elif "Gate" in name or sum(map(ord, name)) % 4 == 0:
            profile, mesh = "gate", gate_mesh
        else:
            profile, mesh = "hall", hall_mesh
        fit_object_to_mesh(obj, mesh, f"authored_chinese_{profile}")
        counts[profile] += 1
    return len(OLD_URBAN_TARGETS), counts


def rebuild_density(
    hall_mesh: bpy.types.Mesh,
    shop_mesh: bpy.types.Mesh,
    gate_mesh: bpy.types.Mesh,
) -> tuple[int, dict[str, int]]:
    district = bpy.data.objects.get("JianghaiTenementDistrict")
    if district is None:
        raise RuntimeError("Jianghai tenement district root is missing")
    quaternius = {}
    for profile, mesh_name in QUATERNIUS_DENSITY_MESHES.items():
        mesh = bpy.data.meshes.get(mesh_name)
        if mesh is None:
            raise RuntimeError(f"Packed Quaternius density mesh is missing: {mesh_name}")
        quaternius[profile] = mesh
    for obj in list(bpy.data.objects):
        if obj.name.startswith("JianghaiDensity_"):
            bpy.data.objects.remove(obj, do_unlink=True)

    meshes = {
        "chinese_hall": hall_mesh,
        "chinese_shop": shop_mesh,
        "chinese_gate": gate_mesh,
        **quaternius,
    }
    counts = {name: 0 for name in meshes}
    for suffix, profile, location, yaw_degrees, scale in DENSITY_BUILDING_LAYOUT:
        obj = bpy.data.objects.new(f"JianghaiDensity_{suffix}", meshes[profile])
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = district
        obj.location = location
        obj.rotation_euler = (0.0, 0.0, radians(yaw_degrees))
        authored_scale = PROFILE_BASE_SCALE[profile] * scale
        obj.scale = (authored_scale, authored_scale, authored_scale)
        if profile.startswith("chinese_"):
            obj["source_asset"] = meshes[profile].get("source_asset", "Chinese Temple 2")
            obj["source_creator"] = meshes[profile].get("source_creator", TEMPLE_CREATOR)
            obj["source_url"] = meshes[profile].get("source_url", TEMPLE_SOURCE_URL)
        else:
            obj["source_asset"] = f"Quaternius Buildings Pack / {profile}"
            obj["source_creator"] = "Quaternius"
            obj["source_url"] = "https://quaternius.com/packs/buildings.html"
        obj["license"] = LICENSE
        obj["authored_adaptation"] = "DCC-authored clean perimeter building with shared distance mesh"
        obj["district_role"] = "authored_density_building"
        obj["collision_role"] = "building_shell"
        obj["building_id"] = obj.name
        obj["jianghai_gameplay_proxy"] = True
        obj["jianghai_proxy_role"] = "density_building_shell"
        counts[profile] += 1
    return len(DENSITY_BUILDING_LAYOUT), counts


def purge_retired_data() -> int:
    removed = 0
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0 and (mesh.name in RETIRED_MESH_NAMES or mesh.name.startswith("__RETIRED_")):
            bpy.data.meshes.remove(mesh)
            removed += 1
    for material in list(bpy.data.materials):
        if material.users == 0 and (
            material.name == "main building material" or material.name.startswith("hhugu_")
        ):
            bpy.data.materials.remove(material)
            removed += 1
    for _ in range(3):
        result = bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=False, do_recursive=True)
        if result != {"FINISHED"}:
            break
    return removed


def evaluated_triangle_count() -> int:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    total = 0
    for instance in depsgraph.object_instances:
        obj = instance.object
        if obj.type != "MESH":
            continue
        mesh = obj.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        if mesh is None:
            continue
        mesh.calc_loop_triangles()
        total += len(mesh.loop_triangles)
        obj.to_mesh_clear()
    return total


def validate_delivery() -> dict[str, int | float]:
    retired_instances = [
        obj.name for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.data.name in RETIRED_MESH_NAMES
    ]
    density = [obj for obj in bpy.context.scene.objects if obj.name.startswith("JianghaiDensity_")]
    edge_names = {
        f"JianghaiDensity_{side}Edge{index:02d}"
        for side in ("West", "East") for index in (4, 5, 6)
    }
    spawn_points = tuple(Vector(point) for point in JIANGHAI_DEPLOYMENT_POINTS)
    edge_clearance = min(
        (bpy.data.objects[name].matrix_world.translation - spawn).xy.length
        for name in edge_names for spawn in spawn_points
    )
    triangles = evaluated_triangle_count()
    targets_ready = all(
        bpy.data.objects[name].data.get("jianghai_chinese_rebuild_version") == REBUILD_VERSION
        for name in OLD_URBAN_TARGETS
    )
    valid = (
        not retired_instances
        and targets_ready
        and len(density) == 42
        and edge_names.issubset(bpy.data.objects.keys())
        and edge_clearance >= 24.0
        and triangles < MAX_INSTANCE_TRIANGLES
    )
    print(
        "JIANGHAI_CHINESE_DISTRICT_CHECK "
        f"valid={valid} replaced={len(OLD_URBAN_TARGETS)} retired_visible={len(retired_instances)} "
        f"density={len(density)}/42 edge={len(edge_names)}/6 "
        f"deployment_pad_clearance={edge_clearance:.2f} triangles={triangles}/{MAX_INSTANCE_TRIANGLES}"
    )
    if not valid:
        raise RuntimeError("Jianghai Chinese district rebuild contract failed")
    return {"triangles": triangles, "density": len(density), "clearance": edge_clearance}


def render_previews() -> list[Path]:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("JianghaiChineseDistrictPreviewCamera")
    camera = bpy.data.objects.new("JianghaiChineseDistrictPreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.lens = 38.0
    camera_data.clip_end = 2_000.0
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    views = (
        ("12_chinese_edge_gate.png", (-174.0, 58.0, 8.5), (-127.0, 60.0, 5.5)),
        ("13_chinese_avenue.png", (0.0, -112.0, 14.0), (0.0, 72.0, 6.0)),
        ("14_chinese_old_city_overview.png", (215.0, -145.0, 145.0), (0.0, 58.0, 2.0)),
    )
    outputs = []
    for filename, origin, target in views:
        camera.location = origin
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = PREVIEW_DIR / filename
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(path)
        print(f"JIANGHAI_CHINESE_PREVIEW path={path}")
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data)
    return outputs


def main() -> None:
    if Path(bpy.data.filepath).resolve() != BLEND_PATH.resolve():
        raise RuntimeError(f"Open the authoritative Jianghai blend before rebuilding: {BLEND_PATH}")
    render_requested = "--render-previews" in sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else False
    hall_mesh = build_temple_lod(HALL_MESH_NAME, 0.080, "street hall LOD")
    density_hall = decimate_mesh(hall_mesh, DENSITY_HALL_MESH_NAME, 0.38, "perimeter hall LOD")
    roof_mesh = build_temple_roof_kit()
    shop_mesh = build_pavilion_composite(
        SHOP_MESH_NAME,
        QUATERNIUS_DENSITY_MESHES["quaternius_building4"],
        (9.8, 7.0, 6.4),
        roof_mesh,
        (11.6, 8.8, 3.8),
        5.6,
        (1.00, 1.00, 1.00),
        0.64,
        "arcade shop",
    )
    gate_mesh = build_pavilion_composite(
        GATE_MESH_NAME,
        QUATERNIUS_DENSITY_MESHES["quaternius_big"],
        (11.2, 8.6, 7.2),
        roof_mesh,
        (13.2, 10.4, 4.4),
        6.3,
        (1.18, 1.15, 1.10),
        0.66,
        "gate house",
    )
    replaced, object_profiles = replace_retired_buildings(hall_mesh, shop_mesh, gate_mesh)
    enterable = apply_enterable_residences()
    density_shop = decimate_mesh(shop_mesh, DENSITY_SHOP_MESH_NAME, 0.42, "perimeter arcade shop LOD")
    density_gate = decimate_mesh(gate_mesh, DENSITY_GATE_MESH_NAME, 0.48, "perimeter gate house LOD")
    density_count, profile_counts = rebuild_density(density_hall, density_shop, density_gate)
    retired_blocks = purge_retired_data()
    root = bpy.data.objects.get("JianghaiOldCityAuthoredScene")
    if root is None:
        raise RuntimeError("Jianghai authored scene root is missing")
    root["chinese_district_rebuild_version"] = REBUILD_VERSION
    root["chinese_district_authored_on"] = "2026-08-29"
    root["chinese_district_source"] = "Chinese Temple 2 plus retained Quaternius CC0 modules"
    root["retired_visible_assets"] = "Old Urban building; Scan Old Building Street"
    root["retired_visible_asset_instances"] = 0
    metrics = validate_delivery()
    previews = render_previews() if render_requested else []
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    print(
        "JIANGHAI_CHINESE_REBUILD_PASS "
        f"valid=True replaced={replaced} density={density_count} "
        f"object_profiles={','.join(f'{name}:{count}' for name, count in object_profiles.items())} "
        f"profiles={','.join(f'{name}:{count}' for name, count in profile_counts.items())} "
        f"retired_blocks={retired_blocks} triangles={metrics['triangles']} "
        f"enterable={enterable.residence_count} cuts={enterable.cut_count} "
        f"door_samples={enterable.aperture_sample_count}/{enterable.wall_sample_count} "
        f"scene_door_samples={enterable.scene_aperture_sample_count} "
        f"removed_door_inserts={enterable.removed_insert_count} "
        f"previews={len(previews)} blend={BLEND_PATH}"
    )


if __name__ == "__main__":
    main()
