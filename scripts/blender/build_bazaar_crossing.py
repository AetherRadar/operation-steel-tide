"""Build the authored Bazaar Crossing demolition-map art package.

Run with Blender 4.5 LTS from the repository root::

    blender --background source_art/world/bazaar_crossing/bazaar_crossing_source_palette.blend \
        --python scripts/blender/build_bazaar_crossing.py

The authoritative, map-local Bazaar source palette is deliberately opened
first.  It pins only the exact CC0 meshes, materials, and textures approved for
this map, so later edits to Jianghai Old City cannot silently change Bazaar's
source identity.  The script validates and extracts that palette, composes the
standalone arena, saves a packed DCC file, exports an embedded-material GLB,
and round-trips the GLB for validation.  Gameplay collision and navigation
remain Godot-authored.
"""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
from math import atan2, degrees, isfinite, radians, sqrt
from pathlib import Path
import json
import os
import sys
import traceback

import bmesh
import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_SOURCE_DIR = REPO_ROOT / "source_art" / "world" / "bazaar_crossing"
SOURCE_BLEND = OUTPUT_SOURCE_DIR / "bazaar_crossing_source_palette.blend"
EXPECTED_SOURCE_BLEND_SHA256 = (
    "0073ADE0E13682C47A07CCBE02B499BFF8FBD25C0C98DA908BB58A94FEE4F1F4"
)
OUTPUT_BLEND = OUTPUT_SOURCE_DIR / "bazaar_crossing.blend"
PREVIEW_DIR = OUTPUT_SOURCE_DIR / "previews"
OUTPUT_RUNTIME_DIR = REPO_ROOT / "assets" / "models" / "bazaar_crossing"
OUTPUT_GLB = OUTPUT_RUNTIME_DIR / "bazaar_crossing.glb"
OUTPUT_REPORT = OUTPUT_SOURCE_DIR / "bazaar_crossing_build_report.json"

MAP_X_MIN = -68.0
MAP_X_MAX = 68.0
MAP_Z_MIN = -56.0
MAP_Z_MAX = 56.0
MAX_GLB_BYTES = 55_000_000
MAX_BLEND_BYTES = 85_000_000
MAX_INSTANCE_TRIANGLES = 3_000_000
MAX_UNIQUE_TRIANGLES = 420_000
MAX_TEXTURE_DIMENSION = 1024
MAX_TEXTURE_MEMORY_MIB = 180.0

CC0_LICENSE = "CC0 1.0 Universal"
PROJECT_LICENSE = "Project-authored layout/build adaptation (MIT repository)"
FORBIDDEN_SOURCE_TOKENS = (
    "hero mountain",
    "hero_mountain",
    "solararchitect",
    "cc by",
    "coast line",
    "coast_line",
    "mountain",
)


@dataclass(frozen=True)
class SourceSpec:
    key: str
    object_name: str
    source_asset: str
    source_creator: str
    source_url: str
    expected_triangles: int
    expected_materials: tuple[str, ...]


@dataclass(frozen=True)
class StairSpec:
    name: str
    bottom_x: float
    bottom_z: float
    top_x: float
    top_z: float
    top_height: float
    width: float
    steps: int
    tread: float
    platform: str


SOURCE_SPECS = (
    SourceSpec(
        "old_urban",
        "JianghaiCleared_MarketShop00",
        "Old Urban building",
        "Abobla O.S",
        "https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/",
        38_280,
        ("main building material",),
    ),
    SourceSpec(
        "scan_old",
        "JianghaiCleared_MarketShop01",
        "Scan Old Building Street",
        "Free poly",
        "https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/",
        89_918,
        ("hhugu_u1_v1", "hhugu_u2_v1", "hhugu_u1_v2"),
    ),
    SourceSpec(
        "pawnshop",
        "JianghaiCleared_PawnshopStorefront",
        "Old Urban building / pawnshop doorway adaptation",
        "Abobla O.S; Operation Steel Tide adaptation",
        "https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/",
        24_106,
        ("main building material",),
    ),
    SourceSpec(
        "lantern",
        "PawnshopLantern00",
        "Chinese red lamp",
        "Kin Chen",
        "https://www.blenderkit.com/asset-gallery-detail/b97e433c-2eb1-46b8-9633-5bdee21e4e7a/",
        7_436,
        ("lantern_Baked",),
    ),
    SourceSpec(
        "bicycle",
        "JianghaiExpansion_Bicycle00",
        "Pink city bicycle",
        "Kin Chen",
        "https://www.blenderkit.com/asset-gallery-detail/4c1a83c1-829f-4c00-878e-9e73c6b89c3b/",
        11_825,
        (
            "JianghaiBicycleWeatheredTeal",
            "JianghaiBicycleLeather",
            "JianghaiBicycleRubber",
            "JianghaiBicycleSteel",
            "JianghaiBicycleDarkSteel",
            "JianghaiBicycleAmberReflector",
        ),
    ),
    SourceSpec(
        "tea_table",
        "JianghaiCleared_MarketTeaTable",
        "Chinese Tea Table",
        "Kirill Sannikov / Poly Haven",
        "https://polyhaven.com/a/chinese_tea_table",
        2_508,
        ("chinese_tea_table",),
    ),
    SourceSpec(
        "stool",
        "JianghaiCleared_MarketStool00",
        "Chinese Stool",
        "Kirill Sannikov / Poly Haven",
        "https://polyhaven.com/a/chinese_stool",
        1_090,
        ("chinese_stool",),
    ),
    SourceSpec(
        "military_crate",
        "WeatheredCargoCrate00",
        "Old Military Crate",
        "Jack Mava / Poly Haven",
        "https://polyhaven.com/a/old_military_crate",
        3_387,
        ("military_crate_m_01",),
    ),
    SourceSpec(
        "barrel",
        "JianghaiArtPass_EastBarrel00",
        "Barrel 03",
        "Serhii Khromov / Poly Haven",
        "https://polyhaven.com/a/barrel_03",
        1_473,
        ("barrel_03",),
    ),
    SourceSpec(
        "plastic_crate",
        "JianghaiArtPass_EastCrate00",
        "Plastic Crate 02",
        "Fabi_G / Poly Haven",
        "https://polyhaven.com/a/plastic_crate_02",
        5_840,
        ("plastic_crate_02",),
    ),
    SourceSpec(
        "wicker_basket",
        "JianghaiExpansion_MarketWickerBasket_Part00",
        "Wicker Basket 01",
        "Kuutti Siitonen / Poly Haven",
        "https://polyhaven.com/a/wicker_basket_01",
        22_276,
        ("wicker_basket_01",),
    ),
    SourceSpec(
        "hand_truck",
        "JianghaiExpansion_FactoryHandTruck_Part00",
        "Hand Truck",
        "Mutanzom3D / Poly Haven",
        "https://polyhaven.com/a/hand_truck",
        13_220,
        ("hand_truck",),
    ),
    SourceSpec(
        "trey_stair",
        "BazaarSource_IndStairsWideFull",
        "Modular Industrial Pieces / IndStairsWideFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        104,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_floor",
        "BazaarSource_IndFloorGreyPlatformFull",
        "Modular Industrial Pieces / IndFloorGreyPlatformFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        2,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_rail",
        "BazaarSource_IndRoofTrimBStraightFull",
        "Modular Industrial Pieces / IndRoofTrimBStraightFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        18,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_column",
        "BazaarSource_IndColumnFree",
        "Modular Industrial Pieces / IndColumnFree",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        24,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_column_cap",
        "BazaarSource_IndColumnFreeCap",
        "Modular Industrial Pieces / IndColumnFreeCap",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        40,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_foundation",
        "BazaarSource_IndFoundationAStraightFull",
        "Modular Industrial Pieces / IndFoundationAStraightFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        8,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_canopy",
        "BazaarSource_IndRoofDarkGreyAngledFull",
        "Modular Industrial Pieces / IndRoofDarkGreyAngledFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        2,
        ("BazaarTreyGradientSource",),
    ),
)

COFFEE_CART_SOURCE_SPECS = (
    SourceSpec(
        "coffee_cart_bottom",
        "JianghaiExpansion_MarketTeaCart_Part00",
        "Coffee Cart 01 / bottom cart",
        "Joe Seabuhr / Poly Haven",
        "https://polyhaven.com/a/CoffeeCart_01",
        8_970,
        ("CoffeeCart_01_cart",),
    ),
    SourceSpec(
        "coffee_cart_top",
        "JianghaiExpansion_MarketTeaCart_Part01",
        "Coffee Cart 01 / top and props",
        "Joe Seabuhr / Poly Haven",
        "https://polyhaven.com/a/CoffeeCart_01",
        16_929,
        ("CoffeeCart_01_props",),
    ),
    SourceSpec(
        "coffee_cart_mugs",
        "JianghaiExpansion_MarketTeaCart_Part02",
        "Coffee Cart 01 / mugs",
        "Joe Seabuhr / Poly Haven",
        "https://polyhaven.com/a/CoffeeCart_01",
        1_760,
        ("CoffeeCart_01_mugs",),
    ),
)

SURFACE_MATERIAL_SOURCES = {
    "BazaarWetAsphalt": "JianghaiWetAsphalt",
    "BazaarStonePaving": "JianghaiStonePaving",
    "BazaarWeatheredConcrete": "JianghaiWeatheredConcrete",
    "BazaarBlackenedSteel": "JianghaiBlackenedSteel",
}

STAIRS = (
    StairSpec("Bazaar_A_Gallery_South_Stair", -59.0, -0.28, -59.0, -10.0, 3.0, 3.2, 18, 0.54, "A_Gallery"),
    StairSpec("Bazaar_A_Gallery_East_Stair", -41.28, -27.0, -51.0, -27.0, 3.0, 3.2, 18, 0.54, "A_Gallery"),
    StairSpec("Bazaar_Mid_Bridge_West_Stair", -22.72, 0.0, -13.0, 0.0, 3.0, 3.2, 18, 0.54, "Mid_Bridge"),
    StairSpec("Bazaar_Mid_Bridge_East_Stair", 22.72, 0.0, 13.0, 0.0, 3.0, 3.2, 18, 0.54, "Mid_Bridge"),
    StairSpec("Bazaar_B_Balcony_South_Stair", 59.0, -4.68, 59.0, -13.0, 2.6, 3.2, 16, 0.52, "B_Balcony"),
    StairSpec("Bazaar_B_Balcony_West_Stair", 42.68, -27.0, 51.0, -27.0, 2.6, 3.2, 16, 0.52, "B_Balcony"),
)

# Frozen invisible runtime-collision contract supplied by the Bazaar gameplay
# builder.  The DCC scene places finished CC0 building masses or authored cover
# at every one of these footprints so players never meet an unexplained wall.
RUNTIME_ARCHITECTURE_AABBS = (
    ("Attack_West", -18.0, 31.0, 18.0, 12.0, 6.0),
    ("Attack_East", 18.0, 31.0, 18.0, 12.0, 6.0),
    ("West_Split_South", -24.0, 10.0, 20.0, 12.0, 6.1),
    ("West_Split_North", -25.0, -10.0, 18.0, 12.0, 6.1),
    ("East_Split_South", 25.0, 10.0, 18.0, 12.0, 6.1),
    ("East_Split_North", 24.0, -10.0, 20.0, 12.0, 6.1),
    ("Defender_Arcade_West", -22.0, -37.0, 18.0, 8.0, 6.0),
    ("Defender_Arcade_East", 22.0, -37.0, 18.0, 8.0, 6.0),
    ("Mid_Chicane_South", 6.5, 23.0, 7.0, 9.0, 5.8),
    ("Mid_Chicane_Center", -6.5, 10.0, 7.0, 7.0, 5.8),
    ("Mid_Chicane_North", 0.0, -16.0, 12.0, 7.0, 5.9),
)

RUNTIME_SITE_COVER_AABBS = (
    ("A_Site_Cover_West", -49.0, -18.0, 2.4, 3.2, 1.28),
    ("A_Site_Cover_East", -37.5, -27.5, 3.0, 2.4, 1.22),
    ("B_Site_Cover_East", 49.0, -18.0, 2.4, 3.2, 1.28),
    ("B_Site_Cover_West", 37.5, -27.5, 3.0, 2.4, 1.22),
)

RUNTIME_HIGH_COVER_AABBS = (
    ("A_High_Cover_West", -62.0, -20.0, 1.5, 4.0, 3.0, 4.20),
    ("A_High_Cover_North", -56.0, -29.0, 3.0, 1.4, 3.0, 4.16),
    ("B_High_Cover_East", 62.0, -21.0, 1.5, 4.0, 2.6, 3.80),
    ("B_High_Cover_North", 56.0, -30.0, 3.0, 1.4, 2.6, 3.76),
)

RUNTIME_MID_COVER_AABBS = (
    ("MidCoverWestMarketCart", -12.0, 20.0, 2.6, 2.2, 0.0, 2.0),
    ("MidCoverEastMarketCart", 12.0, 16.0, 2.6, 2.2, 0.0, 2.0),
)

RUNTIME_SITE_PAIR_SIGHT_BLOCK = (
    "SightBlockSitePair",
    0.0,
    -22.0,
    10.0,
    4.0,
    0.0,
    6.4,
)

RUNTIME_RAIL_SPECS = (
    ("Bazaar_A_Gallery_West_Parapet", (-63.0, -30.0), (-63.0, -10.0), 3.0, 4.10),
    ("Bazaar_A_Gallery_North_Parapet", (-63.0, -30.0), (-51.0, -30.0), 3.0, 4.10),
    ("Bazaar_A_Gallery_South_West_Parapet", (-63.0, -10.0), (-60.6, -10.0), 3.0, 4.10),
    ("Bazaar_A_Gallery_South_East_Parapet", (-57.4, -10.0), (-51.0, -10.0), 3.0, 4.10),
    ("Bazaar_A_Gallery_East_North_Parapet", (-51.0, -30.0), (-51.0, -28.6), 3.0, 4.10),
    ("Bazaar_A_Gallery_East_South_Parapet", (-51.0, -25.4), (-51.0, -10.0), 3.0, 4.10),
    ("Bazaar_Mid_Bridge_North_Parapet", (-13.0, -1.65), (13.0, -1.65), 3.0, 4.10),
    ("Bazaar_Mid_Bridge_South_Parapet", (-13.0, 1.65), (13.0, 1.65), 3.0, 4.10),
    ("Bazaar_B_Balcony_East_Parapet", (63.0, -31.0), (63.0, -13.0), 2.6, 3.70),
    ("Bazaar_B_Balcony_North_Parapet", (51.0, -31.0), (63.0, -31.0), 2.6, 3.70),
    ("Bazaar_B_Balcony_South_West_Parapet", (51.0, -13.0), (57.4, -13.0), 2.6, 3.70),
    ("Bazaar_B_Balcony_South_East_Parapet", (60.6, -13.0), (63.0, -13.0), 2.6, 3.70),
    ("Bazaar_B_Balcony_West_North_Parapet", (51.0, -31.0), (51.0, -28.6), 2.6, 3.70),
    ("Bazaar_B_Balcony_West_South_Parapet", (51.0, -25.4), (51.0, -13.0), 2.6, 3.70),
)


def mesh_triangles(mesh: bpy.types.Mesh) -> int:
    return sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons)


def godot_to_blender(x: float, y: float, z: float) -> Vector:
    return Vector((x, -z, y))


def set_asset_metadata(obj: bpy.types.Object, *, origin: str, role: str) -> None:
    obj["bazaar_asset_origin"] = origin
    obj["bazaar_role"] = role
    obj["coordinate_contract"] = "Godot local XYZ; Blender=(x,-z,y); meters"
    if origin == "project_authored":
        obj["license"] = PROJECT_LICENSE
        obj["visible_geometry"] = "authored connected mesh with UV/PBR; not gameplay collision"
    elif origin == "cc0":
        obj["license"] = CC0_LICENSE


def ensure_authoritative_source() -> None:
    if bpy.app.version < (4, 5, 0):
        raise RuntimeError(f"Blender 4.5+ required; found {bpy.app.version_string}")
    current = Path(bpy.data.filepath).resolve()
    if current != SOURCE_BLEND.resolve():
        raise RuntimeError(
            "Open the authoritative Bazaar source palette before running the builder: "
            f"expected {SOURCE_BLEND}, found {current}"
        )
    if not SOURCE_BLEND.is_file():
        raise FileNotFoundError(SOURCE_BLEND)
    actual_sha256 = sha256(SOURCE_BLEND.read_bytes()).hexdigest().upper()
    if actual_sha256 != EXPECTED_SOURCE_BLEND_SHA256:
        raise RuntimeError(
            "Authoritative Bazaar source palette content drifted: "
            f"expected SHA-256 {EXPECTED_SOURCE_BLEND_SHA256}, found {actual_sha256}"
        )


def validate_and_extract_sources() -> dict[str, bpy.types.Mesh]:
    templates: dict[str, bpy.types.Mesh] = {}
    for spec in SOURCE_SPECS:
        obj = bpy.data.objects.get(spec.object_name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Missing required CC0 source mesh {spec.object_name}")
        triangles = mesh_triangles(obj.data)
        if triangles != spec.expected_triangles:
            raise RuntimeError(
                f"Source topology changed for {spec.object_name}: "
                f"expected {spec.expected_triangles}, found {triangles}"
            )
        material_names = tuple(material.name for material in obj.data.materials if material)
        if material_names != spec.expected_materials:
            raise RuntimeError(
                f"Source materials changed for {spec.object_name}: "
                f"expected {spec.expected_materials}, found {material_names}"
            )
        stated_license = str(obj.get("license", ""))
        parent_license = str(obj.parent.get("license", "")) if obj.parent else ""
        if stated_license and "CC0" not in stated_license:
            raise RuntimeError(f"Non-CC0 object metadata on {spec.object_name}: {stated_license}")
        if parent_license and "CC0" not in parent_license:
            raise RuntimeError(f"Non-CC0 parent metadata on {spec.object_name}: {parent_license}")

        mesh = obj.data.copy()
        mesh.name = f"BazaarCC0_{spec.key}_Mesh"
        mesh.transform(obj.matrix_world)
        xs = [vertex.co.x for vertex in mesh.vertices]
        ys = [vertex.co.y for vertex in mesh.vertices]
        zs = [vertex.co.z for vertex in mesh.vertices]
        center_x = (min(xs) + max(xs)) * 0.5
        center_y = (min(ys) + max(ys)) * 0.5
        mesh.transform(Matrix.Translation((-center_x, -center_y, -min(zs))))
        mesh["source_asset"] = spec.source_asset
        mesh["source_creator"] = spec.source_creator
        mesh["source_url"] = spec.source_url
        mesh["license"] = CC0_LICENSE
        mesh["source_object"] = spec.object_name
        mesh["source_triangles"] = triangles
        templates[spec.key] = mesh

    # Coffee Cart 01 is a three-mesh finished asset.  Its parts must share one
    # normalization transform so their authored relative offsets survive when
    # two market-cover instances are placed around Mid.
    cart_meshes: list[tuple[SourceSpec, bpy.types.Mesh]] = []
    for spec in COFFEE_CART_SOURCE_SPECS:
        obj = bpy.data.objects.get(spec.object_name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Missing required Coffee Cart source mesh {spec.object_name}")
        triangles = mesh_triangles(obj.data)
        materials = tuple(material.name for material in obj.data.materials if material)
        parent_license = str(obj.parent.get("license", "")) if obj.parent else ""
        if triangles != spec.expected_triangles or materials != spec.expected_materials:
            raise RuntimeError(
                f"Coffee Cart source contract changed for {spec.object_name}: "
                f"triangles={triangles} materials={materials}"
            )
        if "CC0" not in parent_license:
            raise RuntimeError(f"Coffee Cart parent lacks CC0 metadata: {spec.object_name}")
        mesh = obj.data.copy()
        mesh.name = f"BazaarCC0_{spec.key}_Mesh"
        mesh.transform(obj.matrix_world)
        cart_meshes.append((spec, mesh))

    cart_vertices = [vertex.co for _spec, mesh in cart_meshes for vertex in mesh.vertices]
    cart_center_x = (min(vertex.x for vertex in cart_vertices) + max(vertex.x for vertex in cart_vertices)) * 0.5
    cart_center_y = (min(vertex.y for vertex in cart_vertices) + max(vertex.y for vertex in cart_vertices)) * 0.5
    cart_ground = min(vertex.z for vertex in cart_vertices)
    cart_normalization = Matrix.Translation((-cart_center_x, -cart_center_y, -cart_ground))
    for spec, mesh in cart_meshes:
        mesh.transform(cart_normalization)
        mesh["source_asset"] = spec.source_asset
        mesh["source_creator"] = spec.source_creator
        mesh["source_url"] = spec.source_url
        mesh["license"] = CC0_LICENSE
        mesh["source_object"] = spec.object_name
        mesh["source_triangles"] = spec.expected_triangles
        mesh["normalization_group"] = "CoffeeCart_01"
        templates[spec.key] = mesh

    for target_name, source_name in SURFACE_MATERIAL_SOURCES.items():
        material = bpy.data.materials.get(source_name)
        if material is None:
            raise RuntimeError(f"Missing approved CC0 PBR material {source_name}")
        material.use_fake_user = True
        for node in material.node_tree.nodes if material.use_nodes else ():
            if node.type == "TEX_IMAGE" and node.image is not None:
                # Keep packed pixels reachable while the mutable source scene is
                # removed.  Blender may lazily unload the buffer during orphan
                # cleanup even though the image datablock remains referenced.
                node.image.use_fake_user = True
                if max(node.image.size) > MAX_TEXTURE_DIMENSION:
                    raise RuntimeError(
                        f"Surface texture {node.image.name} exceeds {MAX_TEXTURE_DIMENSION}px"
                    )

    return templates


def clean_source_scene(templates: dict[str, bpy.types.Mesh]) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for scene in list(bpy.data.scenes):
        if scene != bpy.context.scene:
            bpy.data.scenes.remove(scene)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)

    # Template meshes have no object users yet, so protect them while the Old
    # City orphan tree is purged.  Surface materials use fake users above.
    for mesh in templates.values():
        mesh.use_fake_user = True
    bpy.data.orphans_purge(do_recursive=True)

    scene = bpy.context.scene
    scene.name = "BazaarCrossing_DCC"
    bpy.context.preferences.filepaths.save_version = 0
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world = bpy.data.worlds.new("BazaarCrossing_World")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.045, 0.07, 0.095, 1.0)
    background.inputs["Strength"].default_value = 0.32


def make_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def clone_approved_materials() -> dict[str, bpy.types.Material]:
    materials: dict[str, bpy.types.Material] = {}
    for target_name, source_name in SURFACE_MATERIAL_SOURCES.items():
        source = bpy.data.materials.get(source_name)
        if source is None:
            raise RuntimeError(f"Approved source material disappeared: {source_name}")
        material = source.copy()
        material.name = target_name
        material["source_asset"] = source_name
        if target_name == "BazaarBlackenedSteel":
            material["source_url"] = "Operation Steel Tide project-authored material"
            material["license"] = PROJECT_LICENSE
        else:
            material["source_url"] = "https://polyhaven.com/"
            material["license"] = CC0_LICENSE
        material["bazaar_adaptation"] = "Reused packed PBR nodes for authored Bazaar geometry"
        # The Old City materials were authored for generated coordinates and
        # carried 12x-22x Mapping-node scale on top of Bazaar's meter-scaled UVs.
        # In glTF that double tiling collapsed the stone and concrete into a
        # flat brown average colour.  Bazaar owns real UVs, so keep the approved
        # CC0 base-colour/normal maps but let those UVs control texture density.
        coordinate = next(
            (node for node in material.node_tree.nodes if node.type == "TEX_COORD"),
            None,
        )
        for node in material.node_tree.nodes if material.use_nodes else ():
            if node.type == "MAPPING":
                vector_input = node.inputs.get("Vector")
                scale_input = node.inputs.get("Scale")
                location_input = node.inputs.get("Location")
                rotation_input = node.inputs.get("Rotation")
                if vector_input is not None and coordinate is not None:
                    for link in list(vector_input.links):
                        material.node_tree.links.remove(link)
                    material.node_tree.links.new(coordinate.outputs["UV"], vector_input)
                if scale_input is not None:
                    scale_input.default_value = (1.0, 1.0, 1.0)
                if location_input is not None:
                    location_input.default_value = (0.0, 0.0, 0.0)
                if rotation_input is not None:
                    rotation_input.default_value = (0.0, 0.0, 0.0)

        principled = next(
            (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
            None,
        )
        if principled is None:
            raise RuntimeError(f"Approved PBR material has no Principled BSDF: {target_name}")
        if target_name != "BazaarBlackenedSteel":
            for socket_name in ("Metallic", "Roughness"):
                socket = principled.inputs.get(socket_name)
                if socket is not None:
                    for link in list(socket.links):
                        material.node_tree.links.remove(link)
            principled.inputs["Metallic"].default_value = 0.0
            principled.inputs["Roughness"].default_value = (
                0.84 if target_name == "BazaarWetAsphalt" else 0.80
            )
            coat = principled.inputs.get("Coat Weight")
            if coat is not None:
                coat.default_value = 0.0
            specular = principled.inputs.get("Specular IOR Level")
            if specular is not None:
                specular.default_value = 0.32
            material["bazaar_nonmetallic"] = True
            material["bazaar_minimum_roughness"] = 0.70
        materials[target_name] = material
        source.use_fake_user = False

    materials["BazaarSiteA_Paint"] = make_simple_material(
        "BazaarSiteA_Paint", (0.55, 0.105, 0.055, 1.0), 0.55, 0.0
    )
    materials["BazaarSiteB_Paint"] = make_simple_material(
        "BazaarSiteB_Paint", (0.045, 0.25, 0.38, 1.0), 0.5, 0.0
    )
    materials["BazaarAwningCanvas"] = make_simple_material(
        "BazaarAwningCanvas", (0.29, 0.045, 0.038, 1.0), 0.88, 0.0
    )
    materials["BazaarWarmPlaster"] = make_simple_material(
        "BazaarWarmPlaster", (0.34, 0.22, 0.13, 1.0), 0.86, 0.0
    )
    return materials


def make_simple_material(
    name: str,
    base_color: tuple[float, float, float, float],
    roughness: float,
    metallic: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = base_color
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    material["license"] = PROJECT_LICENSE
    material["bazaar_material_role"] = "authored physically based finish"
    return material


def link_object(obj: bpy.types.Object, collection: bpy.types.Collection, root: bpy.types.Object) -> None:
    collection.objects.link(obj)
    obj.parent = root


def assign_box_uv(mesh: bpy.types.Mesh, tile_meters: float = 3.0) -> None:
    if mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")
    inv = 1.0 / tile_meters
    for poly in mesh.polygons:
        normal = poly.normal
        ax, ay, az = abs(normal.x), abs(normal.y), abs(normal.z)
        for loop_index in poly.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if az >= ax and az >= ay:
                uv = (vertex.x * inv, vertex.y * inv)
            elif ax >= ay:
                uv = (vertex.y * inv, vertex.z * inv)
            else:
                uv = (vertex.x * inv, vertex.z * inv)
            uv_layer.data[loop_index].uv = uv


def place_source(
    templates: dict[str, bpy.types.Mesh],
    spec_by_key: dict[str, SourceSpec],
    key: str,
    name: str,
    position: tuple[float, float, float],
    yaw_degrees: float,
    scale: tuple[float, float, float] | float,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
) -> bpy.types.Object:
    mesh = templates[key]
    obj = bpy.data.objects.new(name, mesh)
    link_object(obj, collection, root)
    obj.location = godot_to_blender(*position)
    obj.rotation_euler.z = radians(-yaw_degrees)
    if isinstance(scale, tuple):
        # Godot X/Y/Z scale -> Blender X/-Z/Y axis order.
        obj.scale = (scale[0], scale[2], scale[1])
    else:
        obj.scale = (scale, scale, scale)
    source = spec_by_key[key]
    set_asset_metadata(obj, origin="cc0", role=role)
    obj["source_asset"] = source.source_asset
    obj["source_creator"] = source.source_creator
    obj["source_url"] = source.source_url
    obj["source_object"] = source.object_name
    return obj


def place_source_fitted(
    templates: dict[str, bpy.types.Mesh],
    spec_by_key: dict[str, SourceSpec],
    key: str,
    name: str,
    center_x: float,
    center_z: float,
    bottom: float,
    size_x: float,
    size_z: float,
    height: float,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    yaw_degrees: float = 0.0,
    role: str,
) -> bpy.types.Object:
    """Fit a normalized completed CC0 prop to one authored cover cell."""
    mesh = templates[key]
    source_x = max(vertex.co.x for vertex in mesh.vertices) - min(vertex.co.x for vertex in mesh.vertices)
    source_z = max(vertex.co.y for vertex in mesh.vertices) - min(vertex.co.y for vertex in mesh.vertices)
    source_height = max(vertex.co.z for vertex in mesh.vertices) - min(vertex.co.z for vertex in mesh.vertices)
    if min(source_x, source_z, source_height) <= 0.001:
        raise RuntimeError(f"Cannot fit degenerate CC0 source {key}")
    if abs((yaw_degrees % 180.0) - 90.0) < 0.001:
        source_x, source_z = source_z, source_x
    obj = place_source(
        templates,
        spec_by_key,
        key,
        name,
        (center_x, bottom, center_z),
        yaw_degrees,
        (size_x / source_x, height / source_height, size_z / source_z),
        collection,
        root,
        role=role,
    )
    obj["fitted_cover_size_xyz"] = f"{size_x:.3f},{height:.3f},{size_z:.3f}"
    return obj


def authored_basis_matrix(
    origin: Vector,
    x_axis: Vector,
    y_axis: Vector,
    z_axis: Vector,
    scale: tuple[float, float, float],
) -> Matrix:
    """Build a transform for arranging an existing authored source module."""
    sx, sy, sz = scale
    return Matrix(
        (
            (x_axis.x * sx, y_axis.x * sy, z_axis.x * sz, origin.x),
            (x_axis.y * sx, y_axis.y * sy, z_axis.y * sz, origin.y),
            (x_axis.z * sx, y_axis.z * sy, z_axis.z * sz, origin.z),
            (0.0, 0.0, 0.0, 1.0),
        )
    )


def create_authored_module_assembly(
    name: str,
    template: bpy.types.Mesh,
    transforms: list[Matrix],
    source: SourceSpec,
    side_material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
    top_material: bpy.types.Material | None = None,
    additional_modules: list[tuple[bpy.types.Mesh, list[Matrix], SourceSpec]] | None = None,
    weld: bool = False,
    uv_tile: float = 1.0,
) -> bpy.types.Object:
    """Arrange, edit, and consolidate finished CC0 modules without primitive substitutes."""
    if not transforms:
        raise ValueError(f"Authored module assembly {name} has no placements")
    builder = bmesh.new()
    module_groups = [(template, transforms, source)]
    if additional_modules:
        module_groups.extend(additional_modules)
    for module_template, module_transforms, _module_source in module_groups:
        for transform in module_transforms:
            part = module_template.copy()
            part.transform(transform)
            builder.from_mesh(part)
            bpy.data.meshes.remove(part)
    if weld:
        bmesh.ops.remove_doubles(builder, verts=list(builder.verts), dist=0.00002)
        builder.verts.index_update()
        duplicate_faces: list[bmesh.types.BMFace] = []
        seen_faces: set[tuple[int, ...]] = set()
        for face in builder.faces:
            key = tuple(sorted(vertex.index for vertex in face.verts))
            if key in seen_faces:
                duplicate_faces.append(face)
            else:
                seen_faces.add(key)
        if duplicate_faces:
            bmesh.ops.delete(builder, geom=duplicate_faces, context="FACES_ONLY")
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    builder.to_mesh(mesh)
    builder.free()
    mesh.materials.append(side_material)
    if top_material is not None and top_material != side_material:
        mesh.materials.append(top_material)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    if top_material is not None and top_material != side_material:
        for polygon in mesh.polygons:
            polygon.material_index = 1 if polygon.normal.z > 0.52 else 0
    obj = bpy.data.objects.new(name, mesh)
    link_object(obj, collection, root)
    set_asset_metadata(obj, origin="cc0", role=role)
    obj["source_asset"] = " | ".join(group_source.source_asset for _, _, group_source in module_groups)
    obj["source_creator"] = source.source_creator
    obj["source_url"] = source.source_url
    obj["source_object"] = " | ".join(group_source.object_name for _, _, group_source in module_groups)
    obj["authored_module_instances"] = sum(len(group_transforms) for _, group_transforms, _ in module_groups)
    obj["authored_module_types"] = len(module_groups)
    assign_box_uv(mesh, uv_tile)
    return obj


def create_authored_tiled_deck(
    name: str,
    platform: str,
    center_x: float,
    center_z: float,
    size_x: float,
    size_z: float,
    top: float,
    thickness: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    concrete: bpy.types.Material,
    paving: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    columns = max(1, int(round(size_x / 2.0)))
    rows = max(1, int(round(size_z / 2.0)))
    cell_x, cell_z = size_x / columns, size_z / rows
    transforms: list[Matrix] = []
    underside_transforms: list[Matrix] = []
    for row in range(rows):
        for column in range(columns):
            x = center_x - size_x * 0.5 + cell_x * (column + 0.5)
            z = center_z - size_z * 0.5 + cell_z * (row + 0.5)
            transforms.append(
                authored_basis_matrix(
                    godot_to_blender(x, top, z),
                    Vector((1.0, 0.0, 0.0)),
                    Vector((0.0, 1.0, 0.0)),
                    Vector((0.0, 0.0, 1.0)),
                    (cell_x * 0.5, cell_z * 0.5, 1.0),
                )
            )
            underside_transforms.append(
                authored_basis_matrix(
                    godot_to_blender(x, top - thickness, z),
                    Vector((1.0, 0.0, 0.0)),
                    Vector((0.0, -1.0, 0.0)),
                    Vector((0.0, 0.0, -1.0)),
                    (cell_x * 0.5, cell_z * 0.5, 1.0),
                )
            )
    foundation_transforms: list[Matrix] = []
    edge_depth = 0.22
    for start, end in (
        (
            (center_x - size_x * 0.5, center_z - size_z * 0.5 + edge_depth * 0.5),
            (center_x + size_x * 0.5, center_z - size_z * 0.5 + edge_depth * 0.5),
        ),
        (
            (center_x - size_x * 0.5, center_z + size_z * 0.5 - edge_depth * 0.5),
            (center_x + size_x * 0.5, center_z + size_z * 0.5 - edge_depth * 0.5),
        ),
        (
            (center_x - size_x * 0.5 + edge_depth * 0.5, center_z - size_z * 0.5),
            (center_x - size_x * 0.5 + edge_depth * 0.5, center_z + size_z * 0.5),
        ),
        (
            (center_x + size_x * 0.5 - edge_depth * 0.5, center_z - size_z * 0.5),
            (center_x + size_x * 0.5 - edge_depth * 0.5, center_z + size_z * 0.5),
        ),
    ):
        dx, dz = end[0] - start[0], end[1] - start[1]
        length = sqrt(dx * dx + dz * dz)
        ux, uz = dx / length, dz / length
        forward = Vector((ux, -uz, 0.0))
        side = Vector((-forward.y, forward.x, 0.0))
        tiles = max(1, int(round(length / 2.0)))
        cell = length / tiles
        base = godot_to_blender(start[0], top - thickness, start[1])
        for index in range(tiles):
            foundation_transforms.append(
                authored_basis_matrix(
                    base + forward * (cell * (index + 0.5)),
                    forward,
                    side,
                    Vector((0.0, 0.0, 1.0)),
                    (cell * 0.5, edge_depth / 0.20, thickness / 3.0),
                )
            )
    obj = create_authored_module_assembly(
        name,
        templates["trey_floor"],
        transforms,
        specs["trey_floor"],
        concrete,
        collection,
        root,
        role="finished_cc0_authored_elevated_deck",
        top_material=paving,
        additional_modules=[
            (templates["trey_floor"], underside_transforms, specs["trey_floor"]),
            (templates["trey_foundation"], foundation_transforms, specs["trey_foundation"]),
        ],
        weld=True,
        uv_tile=1.25,
    )
    obj["platform"] = platform
    obj["top_height_m"] = top
    obj["godot_center_xz"] = f"{center_x:.3f},{center_z:.3f}"
    obj["footprint_m"] = f"{size_x:.3f},{size_z:.3f}"
    obj["source_tile_grid"] = f"{columns}x{rows}"
    obj["source_top_modules"] = len(transforms)
    obj["source_foundation_modules"] = len(foundation_transforms)
    obj["source_underside_modules"] = len(underside_transforms)
    return obj


def create_authored_surface_patch(
    name: str,
    center_x: float,
    center_z: float,
    size_x: float,
    size_z: float,
    height: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
    uv_tile: float,
) -> bpy.types.Object:
    """Tile the finished Trey floor module for ground, paving, and painted pads."""
    columns = max(1, int(round(size_x / 2.0)))
    rows = max(1, int(round(size_z / 2.0)))
    cell_x, cell_z = size_x / columns, size_z / rows
    transforms = [
        authored_basis_matrix(
            godot_to_blender(
                center_x - size_x * 0.5 + cell_x * (column + 0.5),
                height,
                center_z - size_z * 0.5 + cell_z * (row + 0.5),
            ),
            Vector((1.0, 0.0, 0.0)),
            Vector((0.0, 1.0, 0.0)),
            Vector((0.0, 0.0, 1.0)),
            (cell_x * 0.5, cell_z * 0.5, 1.0),
        )
        for row in range(rows)
        for column in range(columns)
    ]
    obj = create_authored_module_assembly(
        name,
        templates["trey_floor"],
        transforms,
        specs["trey_floor"],
        material,
        collection,
        root,
        role=role,
        weld=True,
        uv_tile=uv_tile,
    )
    obj["source_tile_grid"] = f"{columns}x{rows}"
    obj["footprint_m"] = f"{size_x:.3f},{size_z:.3f}"
    return obj


def create_authored_stair(
    spec: StairSpec,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    concrete: bpy.types.Material,
    paving: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    source_steps = 10
    if spec.steps <= source_steps or spec.steps >= source_steps * 2:
        raise RuntimeError(f"Unsupported authored stair adaptation count: {spec.steps}")
    dx, dz = spec.top_x - spec.bottom_x, spec.top_z - spec.bottom_z
    run = sqrt(dx * dx + dz * dz)
    ux, uz = dx / run, dz / run
    forward = Vector((ux, -uz, 0.0))
    lateral = Vector((forward.y, -forward.x, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    rise = spec.top_height / spec.steps
    module_run = source_steps * spec.tread
    advance_steps = spec.steps - source_steps
    module_centers = (module_run * 0.5, module_run * 0.5 + advance_steps * spec.tread)
    module_bases = (0.0, advance_steps * rise)
    transforms: list[Matrix] = []
    bottom = godot_to_blender(spec.bottom_x, 0.0, spec.bottom_z)
    for center_u, base_height in zip(module_centers, module_bases, strict=True):
        transforms.append(
            authored_basis_matrix(
                bottom + forward * center_u + up * base_height,
                lateral,
                forward,
                up,
                (spec.width * 0.5, spec.tread / 0.2, rise / 0.2),
            )
        )
    base_tiles = max(2, int(round(run / 2.0)))
    base_cell = run / base_tiles
    base_transforms = [
        authored_basis_matrix(
            bottom + forward * (base_cell * (index + 0.5)) - up * 0.16,
            forward,
            lateral,
            up,
            (base_cell * 0.5, spec.width / 0.20, 0.16 / 3.0),
        )
        for index in range(base_tiles)
    ]
    obj = create_authored_module_assembly(
        spec.name,
        templates["trey_stair"],
        transforms,
        specs["trey_stair"],
        concrete,
        collection,
        root,
        role="finished_cc0_authored_stair_assembly",
        top_material=paving,
        additional_modules=[
            (templates["trey_foundation"], base_transforms, specs["trey_foundation"])
        ],
        weld=True,
        uv_tile=1.15,
    )
    obj["platform"] = spec.platform
    obj["path_width_m"] = spec.width
    obj["step_count"] = spec.steps
    obj["source_step_count"] = source_steps
    obj["source_stair_modules"] = len(transforms)
    obj["overlapped_source_steps"] = source_steps * 2 - spec.steps
    obj["source_foundation_modules"] = len(base_transforms)
    obj["tread_m"] = spec.tread
    obj["riser_m"] = rise
    obj["run_m"] = run
    obj["slope_degrees"] = degrees(atan2(spec.top_height, run))
    obj["godot_bottom_xyz"] = f"{spec.bottom_x:.3f},0.000,{spec.bottom_z:.3f}"
    obj["godot_top_xyz"] = f"{spec.top_x:.3f},{spec.top_height:.3f},{spec.top_z:.3f}"
    return obj


def create_authored_horizontal_strip(
    name: str,
    start: tuple[float, float],
    end: tuple[float, float],
    bottom: float,
    height: float,
    thickness: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
) -> bpy.types.Object:
    dx, dz = end[0] - start[0], end[1] - start[1]
    length = sqrt(dx * dx + dz * dz)
    ux, uz = dx / length, dz / length
    forward = Vector((ux, -uz, 0.0))
    side = Vector((-forward.y, forward.x, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    tiles = max(1, int(round(length / 2.0)))
    cell = length / tiles
    start_vector = godot_to_blender(start[0], bottom, start[1])
    transforms = [
        authored_basis_matrix(
            start_vector + forward * (cell * (index + 0.5)),
            forward,
            side,
            up,
            (cell * 0.5, thickness / 0.30, height / 1.20),
        )
        for index in range(tiles)
    ]
    return create_authored_module_assembly(
        name,
        templates["trey_rail"],
        transforms,
        specs["trey_rail"],
        material,
        collection,
        root,
        role=role,
        weld=True,
        uv_tile=0.70,
    )


def create_authored_open_guardrail(
    name: str,
    start: tuple[float, float],
    end: tuple[float, float],
    bottom: float,
    top: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Arrange finished Trey trims as two open guardrail rails, never a solid wall."""
    dx, dz = end[0] - start[0], end[1] - start[1]
    length = sqrt(dx * dx + dz * dz)
    ux, uz = dx / length, dz / length
    forward = Vector((ux, -uz, 0.0))
    side = Vector((-forward.y, forward.x, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    tiles = max(1, int(round(length / 2.0)))
    cell = length / tiles
    rail_height = 0.14
    rail_bottoms = (bottom, bottom + (top - bottom) * 0.48)
    start_vector = godot_to_blender(start[0], 0.0, start[1])
    transforms = [
        authored_basis_matrix(
            start_vector
            + forward * (cell * (index + 0.5))
            + up * rail_bottom,
            forward,
            side,
            up,
            (cell * 0.5, 0.16 / 0.30, rail_height / 1.20),
        )
        for rail_bottom in rail_bottoms
        for index in range(tiles)
    ]
    obj = create_authored_module_assembly(
        name,
        templates["trey_rail"],
        transforms,
        specs["trey_rail"],
        material,
        collection,
        root,
        role="finished_cc0_authored_sightline_parapet",
        weld=True,
        uv_tile=0.62,
    )
    obj["authored_open_rail_rows"] = len(rail_bottoms)
    obj["authored_rail_height_m"] = rail_height
    return obj


def create_authored_column_set(
    name: str,
    positions: tuple[tuple[float, float, float, float], ...],
    width: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
) -> bpy.types.Object:
    transforms = [
        authored_basis_matrix(
            godot_to_blender(x, bottom, z),
            Vector((1.0, 0.0, 0.0)),
            Vector((0.0, 1.0, 0.0)),
            Vector((0.0, 0.0, 1.0)),
            (width / 0.40, width / 0.40, (top - bottom) / 3.0),
        )
        for x, z, bottom, top in positions
    ]
    return create_authored_module_assembly(
        name,
        templates["trey_column"],
        transforms,
        specs["trey_column"],
        material,
        collection,
        root,
        role=role,
        weld=False,
        uv_tile=0.75,
    )


def create_authored_stair_rails(
    spec: StairSpec,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    steel: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> tuple[bpy.types.Object, bpy.types.Object]:
    dx, dz = spec.top_x - spec.bottom_x, spec.top_z - spec.bottom_z
    run = sqrt(dx * dx + dz * dz)
    ux, uz = dx / run, dz / run
    horizontal = Vector((ux, -uz, 0.0))
    lateral = Vector((-horizontal.y, horizontal.x, 0.0))
    slope = Vector((ux, -uz, spec.top_height / run)).normalized()
    normal = slope.cross(lateral).normalized()
    slope_length = sqrt(run * run + spec.top_height * spec.top_height)
    tiles = max(2, int(round(run / 2.0)))
    cell = slope_length / tiles
    bottom = godot_to_blender(spec.bottom_x, 0.0, spec.bottom_z)
    transforms: list[Matrix] = []
    rail_offset = spec.width * 0.5 + 0.09
    for side_sign in (-1.0, 1.0):
        base = bottom + lateral * rail_offset * side_sign
        for index in range(tiles):
            transforms.append(
                authored_basis_matrix(
                    base + slope * (cell * (index + 0.5)),
                    slope,
                    lateral,
                    normal,
                    (cell * 0.5, 0.16 / 0.30, 0.94 / 1.20),
                )
            )
    rails = create_authored_module_assembly(
        f"{spec.name}_AuthoredTreyRails",
        templates["trey_rail"],
        transforms,
        specs["trey_rail"],
        steel,
        collection,
        root,
        role="finished_cc0_authored_stair_guardrails",
        weld=True,
        uv_tile=0.62,
    )
    rails["stair_contract"] = spec.name

    newel_positions: list[tuple[float, float, float, float]] = []
    for side_sign in (-1.0, 1.0):
        side = rail_offset * side_sign
        px, pz = -uz, ux
        newel_positions.extend(
            (
                (spec.bottom_x + px * side, spec.bottom_z + pz * side, 0.0, 0.98),
                (spec.top_x + px * side, spec.top_z + pz * side, spec.top_height, spec.top_height + 0.98),
            )
        )
    newels = create_authored_column_set(
        f"{spec.name}_AuthoredTreyNewels",
        tuple(newel_positions),
        0.28,
        templates,
        specs,
        steel,
        collection,
        root,
        role="finished_cc0_authored_stair_newels",
    )
    newels["stair_contract"] = spec.name
    return rails, newels


def create_authored_canopy(
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    canvas: bpy.types.Material,
    steel: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object]:
    roof_transforms: list[Matrix] = []
    cell_x = 2.0
    for side_sign in (-1.0, 1.0):
        source_x_axis = Vector((0.0, -side_sign, 0.0))
        source_y_axis = Vector((1.0, 0.0, 0.0))
        for index in range(8):
            center_x = -8.0 + cell_x * (index + 0.5)
            center_z = -side_sign * 1.0
            roof_transforms.append(
                authored_basis_matrix(
                    godot_to_blender(center_x, 5.18, center_z),
                    source_x_axis,
                    source_y_axis,
                    Vector((0.0, 0.0, 1.0)),
                    (1.0, cell_x * 0.5, 0.42),
                )
            )
    roof = create_authored_module_assembly(
        "Bazaar_Mid_Bridge_AuthoredTreyCanopy",
        templates["trey_canopy"],
        roof_transforms,
        specs["trey_canopy"],
        canvas,
        collection,
        root,
        role="finished_cc0_authored_market_canopy",
        weld=True,
        uv_tile=1.25,
    )

    post_positions = tuple(
        (x, z, 3.0, 5.18)
        for x in (-7.4, 7.4)
        for z in (-1.55, 1.55)
    )
    posts = create_authored_column_set(
        "Bazaar_Mid_Canopy_AuthoredTreyPosts",
        post_positions,
        0.24,
        templates,
        specs,
        steel,
        collection,
        root,
        role="finished_cc0_authored_canopy_posts",
    )

    trim_transforms: list[Matrix] = []
    for start, end, bottom, height, thickness in (
        ((-8.0, -1.92), (8.0, -1.92), 5.06, 0.16, 0.18),
        ((-8.0, 1.92), (8.0, 1.92), 5.06, 0.16, 0.18),
        ((-8.0, 0.0), (8.0, 0.0), 5.50, 0.14, 0.16),
    ):
        dx, dz = end[0] - start[0], end[1] - start[1]
        length = sqrt(dx * dx + dz * dz)
        ux, uz = dx / length, dz / length
        forward = Vector((ux, -uz, 0.0))
        side = Vector((-forward.y, forward.x, 0.0))
        tiles = 8
        cell = length / tiles
        base = godot_to_blender(start[0], bottom, start[1])
        for index in range(tiles):
            trim_transforms.append(
                authored_basis_matrix(
                    base + forward * (cell * (index + 0.5)),
                    forward,
                    side,
                    Vector((0.0, 0.0, 1.0)),
                    (cell * 0.5, thickness / 0.30, height / 1.20),
                )
            )
    trim = create_authored_module_assembly(
        "Bazaar_Mid_Canopy_AuthoredTreyTrim",
        templates["trey_rail"],
        trim_transforms,
        specs["trey_rail"],
        steel,
        collection,
        root,
        role="finished_cc0_authored_canopy_trim",
        weld=True,
        uv_tile=0.62,
    )
    return roof, posts, trim


def create_authored_support_between(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
) -> bpy.types.Object:
    """Scale one authored Trey column module into a bracket or suspension member."""
    start_vector = godot_to_blender(*start)
    end_vector = godot_to_blender(*end)
    direction = end_vector - start_vector
    length = direction.length
    if length <= 0.001:
        raise ValueError(f"Authored support {name} requires distinct endpoints")
    z_axis = direction.normalized()
    reference = Vector((0.0, 0.0, 1.0))
    if abs(z_axis.dot(reference)) > 0.94:
        reference = Vector((0.0, 1.0, 0.0))
    x_axis = reference.cross(z_axis).normalized()
    y_axis = z_axis.cross(x_axis).normalized()
    transform = authored_basis_matrix(
        start_vector,
        x_axis,
        y_axis,
        z_axis,
        (width / 0.40, width / 0.40, length / 3.0),
    )
    return create_authored_module_assembly(
        name,
        templates["trey_column"],
        [transform],
        specs["trey_column"],
        material,
        collection,
        root,
        role=role,
        weld=False,
        uv_tile=0.42,
    )


def place_supported_lantern(
    templates: dict[str, bpy.types.Mesh],
    spec_by_key: dict[str, SourceSpec],
    name: str,
    anchor: tuple[float, float, float],
    hook: tuple[float, float, float],
    cable_drop: float,
    scale: float,
    steel: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Hang one finished CC0 lantern from visible authored steelwork."""
    if (Vector(anchor) - Vector(hook)).length > 0.01:
        create_authored_support_between(
            f"{name}_WallArm",
            anchor,
            hook,
            0.07,
            templates,
            spec_by_key,
            steel,
            collection,
            root,
            role="finished_cc0_authored_lantern_wall_bracket",
        )
    lantern_height = max(vertex.co.z for vertex in templates["lantern"].vertices) * scale
    lantern_top = hook[1] - cable_drop
    cable_end = (hook[0], lantern_top, hook[2])
    create_authored_support_between(
        f"{name}_Suspension",
        hook,
        cable_end,
        0.035,
        templates,
        spec_by_key,
        steel,
        collection,
        root,
        role="finished_cc0_authored_lantern_suspension",
    )
    lantern = place_source(
        templates,
        spec_by_key,
        "lantern",
        name,
        (hook[0], lantern_top - lantern_height, hook[2]),
        0.0,
        scale,
        collection,
        root,
        role="finished_cc0_supported_lantern",
    )
    lantern["support_anchor_xyz"] = f"{anchor[0]:.3f},{anchor[1]:.3f},{anchor[2]:.3f}"
    lantern["support_hook_xyz"] = f"{hook[0]:.3f},{hook[1]:.3f},{hook[2]:.3f}"
    lantern["supported_top_y"] = lantern_top
    return lantern


def add_marker(
    name: str,
    position: tuple[float, float, float],
    marker_role: str,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    marker = bpy.data.objects.new(name, None)
    link_object(marker, collection, root)
    marker.location = godot_to_blender(*position)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 1.0
    set_asset_metadata(marker, origin="project_authored", role="layout_marker")
    marker["marker_role"] = marker_role
    marker["godot_xyz"] = f"{position[0]:.3f},{position[1]:.3f},{position[2]:.3f}"
    return marker


def build_map(
    templates: dict[str, bpy.types.Mesh],
    materials: dict[str, bpy.types.Material],
) -> tuple[bpy.types.Object, dict[str, bpy.types.Collection]]:
    root = bpy.data.objects.new("BazaarCrossing", None)
    bpy.context.scene.collection.objects.link(root)
    set_asset_metadata(root, origin="project_authored", role="map_root")
    root["map_name"] = "Bazaar Crossing"
    root["map_bounds_godot_xz"] = "X[-68,68], Z[-56,56]"
    root["authoring_axis_conversion"] = "Blender=(GodotX,-GodotZ,GodotY)"
    root["scale_meters"] = 1.0

    collections = {
        key: make_collection(name)
        for key, name in (
            ("surface", "Bazaar_01_Surface"),
            ("architecture", "Bazaar_02_CC0_Architecture"),
            ("elevation", "Bazaar_03_Elevation"),
            ("dressing", "Bazaar_04_CC0_Dressing"),
            ("markers", "Bazaar_05_Layout_Markers"),
            ("review", "Bazaar_99_Review_Lighting"),
        )
    }

    asphalt = materials["BazaarWetAsphalt"]
    paving = materials["BazaarStonePaving"]
    concrete = materials["BazaarWeatheredConcrete"]
    steel = materials["BazaarBlackenedSteel"]
    specs = {spec.key: spec for spec in (*SOURCE_SPECS, *COFFEE_CART_SOURCE_SPECS)}

    create_authored_surface_patch(
        "BazaarGroundAuthoredMesh",
        0.0,
        0.0,
        136.0,
        112.0,
        0.0,
        templates,
        specs,
        asphalt,
        collections["surface"],
        root,
        role="finished_cc0_authored_ground_tiles",
        uv_tile=4.5,
    )

    # Paved readable-route ribbons are shallow connected solids with chamfered
    # corners.  They differentiate the three lanes without creating collision
    # lips in the center of a 3 m gameplay corridor.
    route_patches = (
        ("Bazaar_A_Long_Paving", -46.0, 3.0, 12.0, 96.0),
        ("Bazaar_Mid_Paving", 0.0, 2.0, 13.0, 100.0),
        ("Bazaar_B_Banana_Paving", 46.0, 3.0, 12.0, 96.0),
        ("Bazaar_Attacker_Court", 0.0, 48.0, 112.0, 12.0),
        ("Bazaar_Defender_Court", 0.0, -48.0, 112.0, 12.0),
    )
    for name, x, z, sx, sz in route_patches:
        create_authored_surface_patch(
            name,
            x,
            z,
            sx,
            sz,
            0.035,
            templates,
            specs,
            paving,
            collections["surface"],
            root,
            role="finished_cc0_authored_route_tiles",
            uv_tile=3.0,
        )

    for name, x, z, mat in (
        ("Bazaar_A_Site_Pad", -43.0, -22.0, materials["BazaarSiteA_Paint"]),
        ("Bazaar_B_Site_Pad", 43.0, -22.0, materials["BazaarSiteB_Paint"]),
    ):
        create_authored_surface_patch(
            name,
            x,
            z,
            8.4,
            8.4,
            0.055,
            templates,
            specs,
            mat,
            collections["surface"],
            root,
            role="finished_cc0_authored_bomb_site_tiles",
            uv_tile=2.0,
        )

    # Three high-ground routes are tiled from Trey Ramm's finished CC0 floor
    # module.  Collision remains an invisible Godot box below these authored
    # surfaces; no generated slab is presented as final art.
    deck_specs = (
        ("Bazaar_A_Gallery_Deck", -57.0, -20.0, 12.0, 20.0, 3.0, 0.32, "A_Gallery"),
        ("Bazaar_Mid_Bridge_Deck", 0.0, 0.0, 26.0, 3.6, 3.0, 0.30, "Mid_Bridge"),
        ("Bazaar_B_Balcony_Deck", 57.0, -22.0, 12.0, 18.0, 2.6, 0.30, "B_Balcony"),
    )
    for name, x, z, sx, sz, top, thickness, platform in deck_specs:
        create_authored_tiled_deck(
            name,
            platform,
            x,
            z,
            sx,
            sz,
            top,
            thickness,
            templates,
            specs,
            concrete,
            paving,
            collections["elevation"],
            root,
        )

    pier_layout = (
        ("A", -61.0, -27.0, 2.68), ("A", -53.0, -27.0, 2.68),
        ("A", -61.0, -13.0, 2.68), ("A", -53.0, -13.0, 2.68),
        ("M", -9.5, 0.0, 2.70), ("M", -3.2, 0.0, 2.70),
        ("M", 3.2, 0.0, 2.70), ("M", 9.5, 0.0, 2.70),
        ("B", 53.5, -28.0, 2.30), ("B", 60.5, -28.0, 2.30),
        ("B", 53.5, -16.0, 2.30), ("B", 60.5, -16.0, 2.30),
    )
    for group in ("A", "M", "B"):
        group_positions = tuple(
            (x, z, 0.0, top)
            for pier_group, x, z, top in pier_layout
            if pier_group == group
        )
        create_authored_column_set(
            f"Bazaar_{group}_Deck_AuthoredTreyColumns",
            group_positions,
            0.62 if group != "M" else 0.54,
            templates,
            specs,
            concrete,
            collections["elevation"],
            root,
            role="finished_cc0_authored_deck_supports",
        )
        cap_transforms = [
            authored_basis_matrix(
                godot_to_blender(x, top - 0.18, z),
                Vector((1.0, 0.0, 0.0)),
                Vector((0.0, 1.0, 0.0)),
                Vector((0.0, 0.0, 1.0)),
                (0.88 / 0.40, 0.88 / 0.40, 0.18 / 0.20),
            )
            for pier_group, x, z, top in pier_layout
            if pier_group == group
        ]
        create_authored_module_assembly(
            f"Bazaar_{group}_Deck_AuthoredTreyCapitals",
            templates["trey_column_cap"],
            cap_transforms,
            specs["trey_column_cap"],
            steel,
            collections["elevation"],
            root,
            role="finished_cc0_authored_deck_capitals",
            weld=False,
            uv_tile=0.62,
        )

    for stair in STAIRS:
        create_authored_stair(
            stair,
            templates,
            specs,
            concrete,
            paving,
            collections["elevation"],
            root,
        )
        create_authored_stair_rails(
            stair,
            templates,
            specs,
            steel,
            collections["elevation"],
            root,
        )

    # Authored Trey trim modules form the fascia and hide the invisible deck
    # collision edge; no generated box is visible beneath the tiled floors.
    deck_fascia_specs = (
        ("A_West", (-62.92, -29.92), (-62.92, -10.08), 2.76, 2.90),
        ("A_North", (-62.92, -29.92), (-51.08, -29.92), 2.76, 2.90),
        ("A_South", (-62.92, -10.08), (-51.08, -10.08), 2.76, 2.90),
        ("A_East", (-51.08, -29.92), (-51.08, -10.08), 2.76, 2.90),
        ("Mid_North", (-12.92, -1.57), (12.92, -1.57), 2.77, 2.91),
        ("Mid_South", (-12.92, 1.57), (12.92, 1.57), 2.77, 2.91),
        ("B_West", (51.08, -30.92), (51.08, -13.08), 2.37, 2.51),
        ("B_North", (51.08, -30.92), (62.92, -30.92), 2.37, 2.51),
        ("B_South", (51.08, -13.08), (62.92, -13.08), 2.37, 2.51),
        ("B_East", (62.92, -30.92), (62.92, -13.08), 2.37, 2.51),
    )
    for edge_name, start, end, bottom, top in deck_fascia_specs:
        create_authored_horizontal_strip(
            f"Bazaar_Deck_{edge_name}_RecessedFascia",
            start,
            end,
            bottom,
            top - bottom,
            0.16,
            templates,
            specs,
            steel,
            collections["elevation"],
            root,
            role="authored_deck_structural_edge_band",
        )

    # The exact runtime-aligned parapets are arrangements of finished Trey CC0
    # trim and column modules, retargeted to Bazaar's PBRs in the DCC scene.
    for name, start, end, bottom, top in RUNTIME_RAIL_SPECS:
        railing = create_authored_open_guardrail(
            name,
            start,
            end,
            bottom,
            top,
            templates,
            specs,
            concrete,
            collections["elevation"],
            root,
        )
        railing["runtime_railing_start_xz"] = f"{start[0]:.3f},{start[1]:.3f}"
        railing["runtime_railing_end_xz"] = f"{end[0]:.3f},{end[1]:.3f}"
        railing["runtime_railing_bottom_top"] = f"{bottom:.3f},{top:.3f}"
        create_authored_horizontal_strip(
            f"{name}_AuthoredTreyCap",
            start,
            end,
            top - 0.14,
            0.14,
            0.30,
            templates,
            specs,
            paving,
            collections["elevation"],
            root,
            role="finished_cc0_authored_parapet_cap",
        )
        segment_length = sqrt((end[0] - start[0]) ** 2 + (end[1] - start[1]) ** 2)
        post_count = max(2, int(segment_length / 3.4) + 1)
        post_positions: list[tuple[float, float, float, float]] = []
        for post_index in range(post_count):
            amount = post_index / (post_count - 1)
            post_x = start[0] + (end[0] - start[0]) * amount
            post_z = start[1] + (end[1] - start[1]) * amount
            post_positions.append((post_x, post_z, bottom, top))
        create_authored_column_set(
            f"{name}_AuthoredTreyPosts",
            tuple(post_positions),
            0.20,
            templates,
            specs,
            steel,
            collections["elevation"],
            root,
            role="finished_cc0_authored_parapet_posts",
        )

    architecture = collections["architecture"]

    # The runtime collision builder owns eleven invisible architectural AABBs.
    # Each footprint is explained by one or two completed CC0 buildings fitted
    # wholly inside it.  Their alternating rows
    # make the intended S-shaped lanes legible without any unmarked collision.
    for aabb_name, center_x, center_z, size_x, size_z, target_height in RUNTIME_ARCHITECTURE_AABBS:
        if aabb_name == "Mid_Chicane_North":
            source_key = "pawnshop"
        elif aabb_name.startswith("Mid_Chicane"):
            source_key = "scan_old"
        else:
            source_key = "old_urban"
        count = 2 if size_x >= 16.0 else 1
        cell_width = size_x / count
        target_piece_width = cell_width - 0.42
        target_piece_depth = size_z - 0.50
        yaw = 0.0 if center_z >= 0.0 else 180.0
        for index in range(count):
            # Wide collision blocks alternate a full Old Urban shophouse with
            # the completed pawnshop frontage.  The footprint remains fully
            # explained while attack/defender views gain readable near/mid
            # façade rhythm instead of cloned twin boxes.
            piece_key = (
                "pawnshop"
                if source_key == "old_urban" and count == 2 and index == 1
                else source_key
            )
            mesh = templates[piece_key]
            source_x = max(vertex.co.x for vertex in mesh.vertices) - min(vertex.co.x for vertex in mesh.vertices)
            source_z = max(vertex.co.y for vertex in mesh.vertices) - min(vertex.co.y for vertex in mesh.vertices)
            source_height = max(vertex.co.z for vertex in mesh.vertices) - min(vertex.co.z for vertex in mesh.vertices)
            scale_x = target_piece_width / source_x
            scale_z = target_piece_depth / source_z
            scale_y = target_height / source_height
            x = center_x - size_x * 0.5 + cell_width * (index + 0.5)
            building = place_source(
                templates,
                specs,
                piece_key,
                f"BazaarCollisionArt_{aabb_name}_{index:02d}",
                (x, 0.0, center_z),
                yaw if index % 2 == 0 else yaw + 180.0,
                (scale_x, scale_y, scale_z),
                architecture,
                root,
                role="finished_runtime_collision_building",
            )
            building["runtime_collision_aabb"] = aabb_name
            building["runtime_aabb_center_xz"] = f"{center_x:.3f},{center_z:.3f}"
            building["runtime_aabb_size_xz"] = f"{size_x:.3f},{size_z:.3f}"

        marker = add_marker(
            f"Marker_Architecture_AABB_{aabb_name}",
            (center_x, 0.0, center_z),
            "runtime_architecture_aabb",
            collections["markers"],
            root,
        )
        marker["runtime_collision_aabb"] = aabb_name
        marker["runtime_aabb_size_xz"] = f"{size_x:.3f},{size_z:.3f}"

    sight_name, sight_x, sight_z, sight_size_x, sight_size_z, sight_bottom, sight_top = (
        RUNTIME_SITE_PAIR_SIGHT_BLOCK
    )
    old_urban_mesh = templates["old_urban"]
    old_width = max(vertex.co.x for vertex in old_urban_mesh.vertices) - min(vertex.co.x for vertex in old_urban_mesh.vertices)
    old_depth = max(vertex.co.y for vertex in old_urban_mesh.vertices) - min(vertex.co.y for vertex in old_urban_mesh.vertices)
    old_height = max(vertex.co.z for vertex in old_urban_mesh.vertices) - min(vertex.co.z for vertex in old_urban_mesh.vertices)
    sight_art = place_source(
        templates,
        specs,
        "old_urban",
        "BazaarCollisionArt_SightBlockSitePair",
        (sight_x, sight_bottom, sight_z),
        180.0,
        ((sight_size_x - 0.42) / old_width, sight_top / old_height, (sight_size_z - 0.38) / old_depth),
        architecture,
        root,
        role="finished_site_pair_sight_block_storefront",
    )
    sight_art["runtime_collision_aabb"] = sight_name
    sight_art["runtime_aabb_center_xyz"] = f"{sight_x:.3f},3.200,{sight_z:.3f}"
    sight_art["runtime_aabb_size_xyz"] = f"{sight_size_x:.3f},6.400,{sight_size_z:.3f}"

    # Boundary shophouses.  Shared source meshes keep the GLB light while the
    # alternating finished Old Urban / Scan Old silhouettes prevent repetition.
    boundary: list[tuple[str, str, float, float, float, float]] = []
    for side, x, yaw in (("West", -64.2, 90.0), ("East", 64.2, -90.0)):
        for index, z in enumerate((-48.0, -28.0, -8.0, 12.0, 32.0, 50.0)):
            key = "scan_old" if index == (1 if side == "West" else 4) else "old_urban"
            boundary.append((key, f"BazaarBoundary_{side}_{index:02d}", x, z, yaw, 1.24))
    for side, z, yaw in (("North", -52.6, 0.0), ("South", 52.6, 180.0)):
        for index, x in enumerate((-54.0, -33.0, -11.0, 11.0, 33.0, 54.0)):
            key = "scan_old" if index == (4 if side == "North" else 1) else "old_urban"
            boundary.append((key, f"BazaarBoundary_{side}_{index:02d}", x, z, yaw, 1.22))
    # Slim completed pawnshop façades close the 10-14 m holes between the
    # perimeter houses.  They remain in the boundary collision belt, never in
    # an uncollided walkable lane.
    for side, x, yaw in (("West", -65.0, 90.0), ("East", 65.0, -90.0)):
        for index, z in enumerate((-38.0, -18.0, 2.0, 22.0, 42.0)):
            boundary.append(
                ("pawnshop", f"BazaarBoundary_{side}_Infill_{index:02d}", x, z, yaw, 0.95)
            )
    for side, z, yaw in (("North", -53.2, 0.0), ("South", 53.2, 180.0)):
        for index, x in enumerate((-43.0, 43.0)):
            boundary.append(
                ("pawnshop", f"BazaarBoundary_{side}_Infill_{index:02d}", x, z, yaw, 0.92)
            )
    for key, name, x, z, yaw, scale in boundary:
        place_source(
            templates, specs, key, name, (x, 0.0, z), yaw, scale,
            architecture, root, role="finished_boundary_shophouse"
        )

    hero_buildings = (
        ("pawnshop", "Bazaar_A_Pawnshop_Facade", -48.0, -36.0, 0.0, (1.10, 1.10, 1.10)),
        ("old_urban", "Bazaar_A_Gallery_Backshop", -58.0, -34.8, 0.0, (1.18, 1.08, 1.18)),
        ("old_urban", "Bazaar_B_Balcony_Backshop", 58.0, -35.2, 0.0, (1.18, 1.08, 1.18)),
    )
    for key, name, x, z, yaw, scale in hero_buildings:
        place_source(
            templates, specs, key, name, (x, 0.0, z), yaw, scale,
            architecture, root, role="finished_inner_shophouse"
        )

    # Site cover is a dense, two-tier stack of completed CC0 military crates;
    # no generated base mesh is serialized as visible art.
    for cover_name, x, z, size_x, size_z, top in RUNTIME_SITE_COVER_AABBS:
        base_top = 0.0
        columns, rows = ((2, 3) if size_x <= size_z else (3, 2))
        cell_x, cell_z = size_x / columns, size_z / rows
        layer_height = (top - base_top) / 2.0
        crate_index = 0
        for layer in range(2):
            for row in range(rows):
                for column in range(columns):
                    crate_x = x - size_x * 0.5 + cell_x * (column + 0.5)
                    crate_z = z - size_z * 0.5 + cell_z * (row + 0.5)
                    crate_name = (
                        f"BazaarCover_{cover_name}"
                        if crate_index == 0
                        else f"BazaarCover_{cover_name}_Crate_{crate_index:02d}"
                    )
                    crate = place_source_fitted(
                        templates,
                        specs,
                        "military_crate",
                        crate_name,
                        crate_x,
                        crate_z,
                        base_top + layer * layer_height,
                        cell_x,
                        cell_z,
                        layer_height,
                        architecture,
                        root,
                        yaw_degrees=180.0 if (layer + row + column) % 2 else 0.0,
                        role="finished_cc0_site_cover_crate_cluster",
                    )
                    crate["runtime_collision_aabb"] = cover_name
                    crate["runtime_aabb_center_xz"] = f"{x:.3f},{z:.3f}"
                    crate["runtime_aabb_size_xz"] = f"{size_x:.3f},{size_z:.3f}"
                    crate_index += 1

    # High cover uses banks of completed CC0 steel barrels whose natural
    # vertical proportion closely matches the 1.1-1.2 m parapet-height AABBs.
    for cover_name, x, z, size_x, size_z, bottom, top in RUNTIME_HIGH_COVER_AABBS:
        base_top = bottom
        columns, rows = ((2, 4) if size_x <= size_z else (4, 2))
        cell_x, cell_z = size_x / columns, size_z / rows
        barrel_index = 0
        for row in range(rows):
            for column in range(columns):
                barrel_x = x - size_x * 0.5 + cell_x * (column + 0.5)
                barrel_z = z - size_z * 0.5 + cell_z * (row + 0.5)
                barrel_name = (
                    f"BazaarCover_{cover_name}"
                    if barrel_index == 0
                    else f"BazaarCover_{cover_name}_Barrel_{barrel_index:02d}"
                )
                barrel = place_source_fitted(
                    templates,
                    specs,
                    "barrel",
                    barrel_name,
                    barrel_x,
                    barrel_z,
                    base_top,
                    cell_x,
                    cell_z,
                    top - base_top,
                    collections["elevation"],
                    root,
                    yaw_degrees=180.0 if (row + column) % 2 else 0.0,
                    role="finished_cc0_high_cover_barrel_cluster",
                )
                barrel["runtime_collision_aabb"] = cover_name
                barrel["runtime_aabb_center_xz"] = f"{x:.3f},{z:.3f}"
                barrel["runtime_aabb_size_xz"] = f"{size_x:.3f},{size_z:.3f}"
                barrel_index += 1

    for cart_name, x, z, size_x, size_z, bottom, top in RUNTIME_MID_COVER_AABBS:
        for part_index, source_key in enumerate(
            ("coffee_cart_bottom", "coffee_cart_top", "coffee_cart_mugs")
        ):
            cart_part = place_source(
                templates,
                specs,
                source_key,
                f"BazaarCover_{cart_name}_Part{part_index:02d}",
                (x, 0.0, z),
                0.0 if x < 0.0 else 180.0,
                (0.98, 1.02, 1.30),
                architecture,
                root,
                role="finished_cc0_market_cart_cover",
            )
            cart_part["runtime_collision_aabb"] = cart_name
            cart_part["runtime_aabb_center_xyz"] = f"{x:.3f},1.000,{z:.3f}"
            cart_part["runtime_aabb_size_xyz"] = f"{size_x:.3f},2.000,{size_z:.3f}"

    # Finished Trey roof, column, and trim modules form the complete canopy.
    create_authored_canopy(
        templates,
        specs,
        materials["BazaarAwningCanvas"],
        steel,
        collections["elevation"],
        root,
    )

    dressing = collections["dressing"]
    detail_layout = (
        ("bicycle", "BazaarBicycle_A_Long", (-50.5, 0.05, 12.5), 16.0, 1.0),
        ("bicycle", "BazaarBicycle_B_Entry", (49.5, 0.05, 8.5), -20.0, 1.0),
        ("hand_truck", "BazaarHandTruck_Pawnshop", (-47.2, 0.05, -32.0), 15.0, 0.88),
        ("tea_table", "BazaarTeaTable_DefenderCourt", (-10.0, 0.05, -43.5), 0.0, 0.92),
        ("stool", "BazaarTeaStool_00", (-11.1, 0.05, -42.4), 20.0, 0.92),
        ("stool", "BazaarTeaStool_01", (-8.8, 0.05, -42.5), -18.0, 0.92),
        ("wicker_basket", "BazaarBasket_MidMarket", (6.7, 0.05, 7.2), 30.0, 1.0),
        ("barrel", "BazaarBarrel_A_Site", (-38.0, 0.05, -18.0), 0.0, 1.0),
        ("barrel", "BazaarBarrel_B_Site", (38.5, 0.05, -17.5), 0.0, 1.0),
        ("military_crate", "BazaarCrate_A_Site_00", (-39.0, 0.05, -25.8), 10.0, 1.05),
        ("military_crate", "BazaarCrate_A_Site_01", (-38.0, 0.43, -25.7), -8.0, 1.05),
        ("military_crate", "BazaarCrate_B_Site_00", (39.0, 0.05, -25.5), -12.0, 1.05),
        ("plastic_crate", "BazaarProduceCrate_West", (-12.5, 0.05, 24.0), 8.0, 1.0),
        ("plastic_crate", "BazaarProduceCrate_East", (11.8, 0.05, 18.0), -10.0, 1.0),
    )
    for key, name, position, yaw, scale in detail_layout:
        place_source(
            templates, specs, key, name, position, yaw, scale,
            dressing, root, role="finished_cc0_street_dressing"
        )

    # Small finished CC0 market vignettes fill the broad gallery/balcony
    # silhouettes without narrowing any frozen 3.2 m stair opening or route.
    elevated_detail_layout = (
        ("tea_table", "BazaarTeaTable_A_Gallery", (-59.2, 3.025, -23.5), 8.0, 0.76),
        ("stool", "BazaarTeaStool_A_Gallery_00", (-60.2, 3.025, -22.6), 20.0, 0.82),
        ("stool", "BazaarTeaStool_A_Gallery_01", (-58.1, 3.025, -22.7), -18.0, 0.82),
        ("plastic_crate", "BazaarProduceCrate_A_Gallery", (-61.3, 3.025, -26.8), 12.0, 0.78),
        ("tea_table", "BazaarTeaTable_B_Balcony", (59.3, 2.625, -24.3), -10.0, 0.76),
        ("stool", "BazaarTeaStool_B_Balcony_00", (60.3, 2.625, -23.4), 18.0, 0.82),
        ("stool", "BazaarTeaStool_B_Balcony_01", (58.2, 2.625, -23.5), -22.0, 0.82),
        ("wicker_basket", "BazaarBasket_B_Balcony", (61.0, 2.625, -27.8), 28.0, 0.72),
    )
    for key, name, position, yaw, scale in elevated_detail_layout:
        place_source(
            templates,
            specs,
            key,
            name,
            position,
            yaw,
            scale,
            dressing,
            root,
            role="finished_cc0_elevated_market_dressing",
        )

    # Seven restrained lanterns replace the former seventeen floating pieces.
    # Every lamp now has an authored steel wall arm or a visible canopy cable.
    supported_lanterns = (
        ((-62.88, 4.02, -16.5), (-61.75, 4.72, -16.5), 0.16),
        ((-62.88, 4.02, -23.5), (-61.75, 4.72, -23.5), 0.16),
        ((-5.0, 5.50, 0.0), (-5.0, 5.50, 0.0), 0.24),
        ((0.0, 5.50, 0.0), (0.0, 5.50, 0.0), 0.24),
        ((5.0, 5.50, 0.0), (5.0, 5.50, 0.0), 0.24),
        ((62.88, 3.62, -18.0), (61.75, 4.32, -18.0), 0.16),
        ((62.88, 3.62, -25.0), (61.75, 4.32, -25.0), 0.16),
    )
    for index, (anchor, hook, drop) in enumerate(supported_lanterns):
        place_supported_lantern(
            templates,
            specs,
            f"BazaarLantern_{index:02d}",
            anchor,
            hook,
            drop,
            0.62,
            steel,
            dressing,
            root,
        )

    markers = collections["markers"]
    for name, position, role in (
        ("Marker_AttackerSpawn", (0.0, 0.0, 49.0), "attacker_spawn"),
        ("Marker_DefenderSpawn", (0.0, 0.0, -49.0), "defender_spawn"),
        ("Marker_BombSite_A", (-43.0, 0.055, -22.0), "bomb_site_a"),
        ("Marker_BombSite_B", (43.0, 0.055, -22.0), "bomb_site_b"),
        ("Marker_A_Gallery_Top", (-57.0, 3.0, -20.0), "high_ground"),
        ("Marker_Mid_Bridge_Top", (0.0, 3.0, 0.0), "high_ground"),
        ("Marker_B_Balcony_Top", (57.0, 2.6, -22.0), "high_ground"),
    ):
        add_marker(name, position, role, markers, root)

    for mesh in templates.values():
        mesh.use_fake_user = False

    return root, collections


def add_review_lighting(collection: bpy.types.Collection) -> None:
    sun_data = bpy.data.lights.new("BazaarReviewSun", "SUN")
    sun_data.energy = 2.0
    sun_data.angle = radians(22.0)
    sun = bpy.data.objects.new("BazaarReviewSun", sun_data)
    collection.objects.link(sun)
    sun.rotation_euler = (radians(32.0), radians(-18.0), radians(138.0))

    area_data = bpy.data.lights.new("BazaarReviewFill", "AREA")
    area_data.energy = 1800.0
    area_data.shape = "DISK"
    area_data.size = 34.0
    area = bpy.data.objects.new("BazaarReviewFill", area_data)
    collection.objects.link(area)
    area.location = (0.0, -8.0, 38.0)


def point_camera(camera: bpy.types.Object, target_godot: tuple[float, float, float]) -> None:
    target = godot_to_blender(*target_godot)
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_previews(collection: bpy.types.Collection) -> None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    add_review_lighting(collection)
    scene = bpy.context.scene
    camera_data = bpy.data.cameras.new("BazaarReviewCamera")
    camera_data.lens = 42.0
    camera_data.sensor_width = 36.0
    camera = bpy.data.objects.new("BazaarReviewCamera", camera_data)
    collection.objects.link(camera)
    scene.camera = camera

    views = (
        ("01_overview.png", (0.0, -88.0, 96.0), (0.0, 0.8, 0.0), 47.0),
        ("02_a_gallery.png", (-45.0, 3.0, 8.5), (-57.5, 1.5, -10.5), 46.0),
        ("03_mid_bridge.png", (12.0, -12.0, 6.5), (8.0, 2.0, 0.0), 40.0),
        ("04_b_balcony.png", (39.0, 19.0, 7.5), (52.0, 1.4, -25.5), 46.0),
    )
    for filename, blender_location, target, lens in views:
        camera.location = blender_location
        camera_data.lens = lens
        point_camera(camera, target)
        scene.render.filepath = str(PREVIEW_DIR / filename)
        bpy.ops.render.render(write_still=True)
        if not (PREVIEW_DIR / filename).is_file():
            raise RuntimeError(f"Preview render failed: {filename}")


def object_world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("No visible mesh bounds")
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return minimum, maximum


def triangulate_visible_meshes(root: bpy.types.Object) -> None:
    """Triangulate each shared visible mesh once for stable MikkTSpace export."""
    meshes = {obj.data for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.parent == root}
    for mesh in meshes:
        if all(len(poly.vertices) == 3 for poly in mesh.polygons):
            continue
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.triangulate(
            bm,
            faces=list(bm.faces),
            quad_method="BEAUTY",
            ngon_method="BEAUTY",
        )
        bm.to_mesh(mesh)
        bm.free()
        mesh.validate(verbose=False)
        mesh.update(calc_edges=True)


def ensure_image_pixels_loaded(image: bpy.types.Image) -> None:
    """Force Blender's lazily unloaded packed image buffer back into memory."""
    if image.packed_file is None:
        raise RuntimeError(f"Texture is not packed into the authoritative source: {image.name}")
    if image.has_data and min(image.size) > 0:
        return
    load_error: Exception | None = None
    try:
        # Reading one packed pixel is enough to make Blender reconstruct the
        # ImBuf after orphan cleanup without writing or unpacking any files.
        _ = image.pixels[0]
    except (IndexError, RuntimeError) as error:
        load_error = error
    if not image.has_data or min(image.size) <= 0:
        detail = f": {load_error}" if load_error is not None else ""
        raise RuntimeError(f"Missing texture pixels: {image.name}{detail}")


def validate_authored_scene(root: bpy.types.Object) -> dict[str, object]:
    scene_objects = list(bpy.context.scene.objects)
    mesh_objects = [obj for obj in scene_objects if obj.type == "MESH" and obj.parent == root]
    # Include nested direct children only: the map uses a deliberately flat
    # export hierarchy under one root to avoid transform surprises in Godot.
    if len(mesh_objects) < 80:
        raise RuntimeError(f"Bazaar scene unexpectedly sparse: {len(mesh_objects)} meshes")
    if any(obj.parent != root for obj in scene_objects if obj.name.startswith("Bazaar") and obj != root and obj.type in {"MESH", "EMPTY"}):
        raise RuntimeError("Bazaar export hierarchy must remain flat under the map root")

    names = {obj.name for obj in scene_objects}
    required = {
        "BazaarGroundAuthoredMesh",
        "Bazaar_A_Gallery_Deck",
        "Bazaar_Mid_Bridge_Deck",
        "Bazaar_B_Balcony_Deck",
        *(stair.name for stair in STAIRS),
        "Bazaar_A_Pawnshop_Facade",
    }
    missing = sorted(required - names)
    if missing:
        raise RuntimeError(f"Missing required Bazaar objects: {missing}")

    validated_meshes: set[bpy.types.Mesh] = set()
    for obj in mesh_objects:
        origin = str(obj.get("bazaar_asset_origin", ""))
        if origin != "cc0":
            raise RuntimeError(
                f"Visible Bazaar mesh must come from a finished CC0 module: {obj.name} origin={origin!r}"
            )
        searchable = " ".join(
            str(obj.get(key, ""))
            for key in ("source_asset", "source_creator", "source_url", "license")
        ).lower()
        if any(token in searchable for token in FORBIDDEN_SOURCE_TOKENS):
            raise RuntimeError(f"Forbidden non-CC0 source token on {obj.name}: {searchable}")
        for metadata_key in ("license", "source_asset", "source_object", "source_creator", "source_url"):
            if not str(obj.get(metadata_key, "")).strip():
                raise RuntimeError(f"CC0 instance lacks {metadata_key} provenance: {obj.name}")
        if "CC0" not in str(obj.get("license", "")):
            raise RuntimeError(f"CC0 instance lacks license metadata: {obj.name}")
        if not obj.data.uv_layers:
            raise RuntimeError(f"Visible mesh lacks UVs: {obj.name}")
        if not any(material is not None for material in obj.data.materials):
            raise RuntimeError(f"Visible mesh lacks material: {obj.name}")
        if obj.data not in validated_meshes:
            validated_meshes.add(obj.data)
            mesh = obj.data
            if not mesh.vertices or not mesh.polygons:
                raise RuntimeError(f"Visible mesh has no renderable surface: {obj.name}")
            if len(mesh.uv_layers.active.data) != len(mesh.loops):
                raise RuntimeError(f"Visible mesh UV loop coverage failed: {obj.name}")
            if any(
                not isfinite(value)
                for uv_loop in mesh.uv_layers.active.data
                for value in uv_loop.uv
            ):
                raise RuntimeError(f"Visible mesh has non-finite UVs: {obj.name}")
            for poly in mesh.polygons:
                if poly.material_index >= len(mesh.materials) or mesh.materials[poly.material_index] is None:
                    raise RuntimeError(f"Visible mesh polygon lacks material: {obj.name}")

    for material_name in (
        "BazaarWetAsphalt",
        "BazaarStonePaving",
        "BazaarWeatheredConcrete",
    ):
        material = bpy.data.materials.get(material_name)
        if material is None or not material.use_nodes:
            raise RuntimeError(f"Missing Bazaar surface PBR: {material_name}")
        coordinate = next((node for node in material.node_tree.nodes if node.type == "TEX_COORD"), None)
        mappings = [node for node in material.node_tree.nodes if node.type == "MAPPING"]
        if coordinate is None or not mappings:
            raise RuntimeError(f"Surface PBR lacks UV mapping nodes: {material_name}")
        for mapping in mappings:
            vector_links = list(mapping.inputs["Vector"].links)
            if len(vector_links) != 1 or vector_links[0].from_socket != coordinate.outputs["UV"]:
                raise RuntimeError(f"Surface PBR is not UV-driven: {material_name}")
            if (Vector(mapping.inputs["Scale"].default_value) - Vector((1.0, 1.0, 1.0))).length > 0.0001:
                raise RuntimeError(f"Surface PBR double-tiles authored UVs: {material_name}")
        principled = next(
            (node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),
            None,
        )
        if principled is None:
            raise RuntimeError(f"Surface PBR lacks Principled shader: {material_name}")
        if principled.inputs["Metallic"].is_linked or principled.inputs["Metallic"].default_value > 0.001:
            raise RuntimeError(f"Stone/asphalt material must be nonmetallic: {material_name}")
        if principled.inputs["Roughness"].is_linked or principled.inputs["Roughness"].default_value < 0.70:
            raise RuntimeError(f"Stone/asphalt roughness gate failed: {material_name}")

    for stair in STAIRS:
        obj = bpy.data.objects[stair.name]
        run = float(obj["run_m"])
        slope = float(obj["slope_degrees"])
        if abs(float(obj["path_width_m"]) - stair.width) > 0.0001 or slope > 18.0:
            raise RuntimeError(f"Stair traversal gate failed for {stair.name}")
        if abs(run - stair.steps * stair.tread) > 0.0001:
            raise RuntimeError(f"Stair run metadata failed for {stair.name}")
        expected_bottom = f"{stair.bottom_x:.3f},0.000,{stair.bottom_z:.3f}"
        expected_top = f"{stair.top_x:.3f},{stair.top_height:.3f},{stair.top_z:.3f}"
        if obj.get("godot_bottom_xyz") != expected_bottom or obj.get("godot_top_xyz") != expected_top:
            raise RuntimeError(f"Stair endpoint contract drifted for {stair.name}")
        stair_materials = {material.name for material in obj.data.materials if material}
        if not {"BazaarWeatheredConcrete", "BazaarStonePaving"}.issubset(stair_materials):
            raise RuntimeError(f"Stair lacks distinct PBR tread/riser finishes: {stair.name}")
        if obj.get("bazaar_role") != "finished_cc0_authored_stair_assembly":
            raise RuntimeError(f"Stair is not a finished CC0 module assembly: {stair.name}")
        stair_sources = str(obj.get("source_asset", ""))
        if "IndStairsWideFull" not in stair_sources or "IndFoundationAStraightFull" not in stair_sources:
            raise RuntimeError(f"Stair lacks pinned Trey source mapping: {stair.name}")
        if int(obj.get("overlapped_source_steps", -1)) != 20 - stair.steps:
            raise RuntimeError(f"Stair authored step adaptation drifted: {stair.name}")
        rails = bpy.data.objects.get(f"{stair.name}_AuthoredTreyRails")
        newels = bpy.data.objects.get(f"{stair.name}_AuthoredTreyNewels")
        if rails is None or rails.get("stair_contract") != stair.name:
            raise RuntimeError(f"Stair lacks authored Trey guardrails: {stair.name}")
        if newels is None or newels.get("stair_contract") != stair.name:
            raise RuntimeError(f"Stair lacks authored Trey landing newels: {stair.name}")
        expected_rail_modules = 2 * max(2, int(round(run / 2.0)))
        if (
            "IndRoofTrimBStraightFull" not in str(rails.get("source_asset", ""))
            or int(rails.get("authored_module_instances", 0)) != expected_rail_modules
        ):
            raise RuntimeError(f"Stair authored guardrail module count drifted: {stair.name}")
        if (
            "IndColumnFree" not in str(newels.get("source_asset", ""))
            or int(newels.get("authored_module_instances", 0)) != 4
        ):
            raise RuntimeError(f"Stair authored newel module count drifted: {stair.name}")
        if int(obj.get("source_stair_modules", 0)) != 2:
            raise RuntimeError(f"Stair must retain two finished Trey stair modules: {stair.name}")
        expected_foundation_modules = max(2, int(round(run / 2.0)))
        if int(obj.get("source_foundation_modules", 0)) != expected_foundation_modules:
            raise RuntimeError(f"Stair authored foundation module count drifted: {stair.name}")
        if int(obj.get("authored_module_instances", 0)) != 2 + expected_foundation_modules:
            raise RuntimeError(f"Stair combined authored module count drifted: {stair.name}")

    expected_platforms = {
        "Bazaar_A_Gallery_Deck": (3.0, 0.32),
        "Bazaar_Mid_Bridge_Deck": (3.0, 0.30),
        "Bazaar_B_Balcony_Deck": (2.6, 0.30),
    }
    expected_deck_modules = {
        "Bazaar_A_Gallery_Deck": (60, 32),
        "Bazaar_Mid_Bridge_Deck": (26, 30),
        "Bazaar_B_Balcony_Deck": (54, 30),
    }
    for name, (expected_top, expected_thickness) in expected_platforms.items():
        obj = bpy.data.objects[name]
        deck_points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        actual_top = max(point.z for point in deck_points)
        actual_bottom = min(point.z for point in deck_points)
        if abs(actual_top - expected_top) > 0.002:
            raise RuntimeError(f"Platform height failed for {name}: {actual_top}")
        if abs(actual_bottom - (expected_top - expected_thickness)) > 0.002:
            raise RuntimeError(f"Platform authored underside failed for {name}: {actual_bottom}")
        deck_materials = {material.name for material in obj.data.materials if material}
        if not {"BazaarWeatheredConcrete", "BazaarStonePaving"}.issubset(deck_materials):
            raise RuntimeError(f"Deck lacks distinct PBR top/edge finishes: {name}")
        if obj.get("bazaar_role") != "finished_cc0_authored_elevated_deck":
            raise RuntimeError(f"Deck is not a finished CC0 module assembly: {name}")
        deck_sources = str(obj.get("source_asset", ""))
        if "IndFloorGreyPlatformFull" not in deck_sources or "IndFoundationAStraightFull" not in deck_sources:
            raise RuntimeError(f"Deck lacks pinned Trey floor source mapping: {name}")
        expected_floor_modules, expected_foundation_modules = expected_deck_modules[name]
        if int(obj.get("source_top_modules", 0)) != expected_floor_modules:
            raise RuntimeError(f"Deck authored top module count drifted: {name}")
        if int(obj.get("source_underside_modules", 0)) != expected_floor_modules:
            raise RuntimeError(f"Deck authored underside module count drifted: {name}")
        if int(obj.get("source_foundation_modules", 0)) != expected_foundation_modules:
            raise RuntimeError(f"Deck authored foundation edge module count drifted: {name}")
        expected_total = expected_floor_modules * 2 + expected_foundation_modules
        if int(obj.get("authored_module_instances", 0)) != expected_total:
            raise RuntimeError(f"Deck combined authored module count drifted: {name}")

    canopy_roof = bpy.data.objects.get("Bazaar_Mid_Bridge_AuthoredTreyCanopy")
    canopy_posts = bpy.data.objects.get("Bazaar_Mid_Canopy_AuthoredTreyPosts")
    canopy_trim = bpy.data.objects.get("Bazaar_Mid_Canopy_AuthoredTreyTrim")
    if (
        canopy_roof is None
        or canopy_roof.get("bazaar_role") != "finished_cc0_authored_market_canopy"
        or "IndRoofDarkGreyAngledFull" not in str(canopy_roof.get("source_asset", ""))
        or int(canopy_roof.get("authored_module_instances", 0)) != 16
    ):
        raise RuntimeError("Mid canopy lacks sixteen finished Trey roof modules")
    if (
        canopy_posts is None
        or canopy_posts.get("bazaar_role") != "finished_cc0_authored_canopy_posts"
        or "IndColumnFree" not in str(canopy_posts.get("source_asset", ""))
        or int(canopy_posts.get("authored_module_instances", 0)) != 4
    ):
        raise RuntimeError("Mid canopy lacks four finished Trey column modules")
    if (
        canopy_trim is None
        or canopy_trim.get("bazaar_role") != "finished_cc0_authored_canopy_trim"
        or "IndRoofTrimBStraightFull" not in str(canopy_trim.get("source_asset", ""))
        or int(canopy_trim.get("authored_module_instances", 0)) != 24
    ):
        raise RuntimeError("Mid canopy lacks twenty-four finished Trey trim modules")

    elevated_market = [
        obj
        for obj in mesh_objects
        if obj.get("bazaar_role") == "finished_cc0_elevated_market_dressing"
    ]
    if len(elevated_market) != 8:
        raise RuntimeError(f"Expected eight elevated market-dressing pieces, found {len(elevated_market)}")

    lanterns = [
        obj for obj in mesh_objects if obj.get("bazaar_role") == "finished_cc0_supported_lantern"
    ]
    if len(lanterns) != 7:
        raise RuntimeError(f"Expected seven supported lanterns, found {len(lanterns)}")
    for lantern in lanterns:
        actual_top = max((lantern.matrix_world @ Vector(corner)).z for corner in lantern.bound_box)
        if abs(actual_top - float(lantern["supported_top_y"])) > 0.012:
            raise RuntimeError(f"Lantern top anchor drifted: {lantern.name}")
        if bpy.data.objects.get(f"{lantern.name}_Suspension") is None:
            raise RuntimeError(f"Lantern lacks visible suspension: {lantern.name}")

    for aabb_name, center_x, center_z, size_x, size_z, _target_height in RUNTIME_ARCHITECTURE_AABBS:
        art = [
            obj
            for obj in mesh_objects
            if obj.get("runtime_collision_aabb") == aabb_name
            and obj.get("bazaar_role") == "finished_runtime_collision_building"
        ]
        expected_count = 2 if size_x >= 16.0 else 1
        if len(art) != expected_count:
            raise RuntimeError(
                f"Runtime AABB {aabb_name} expected {expected_count} finished buildings, found {len(art)}"
            )
        corners = [obj.matrix_world @ Vector(corner) for obj in art for corner in obj.bound_box]
        art_min_x = min(point.x for point in corners)
        art_max_x = max(point.x for point in corners)
        art_min_z = min(-point.y for point in corners)
        art_max_z = max(-point.y for point in corners)
        art_top = max(point.z for point in corners)
        expected_min_x, expected_max_x = center_x - size_x * 0.5, center_x + size_x * 0.5
        expected_min_z, expected_max_z = center_z - size_z * 0.5, center_z + size_z * 0.5
        if art_min_x < expected_min_x - 0.06 or art_max_x > expected_max_x + 0.06:
            raise RuntimeError(f"{aabb_name} visible X mass escapes runtime AABB")
        if art_min_z < expected_min_z - 0.06 or art_max_z > expected_max_z + 0.06:
            raise RuntimeError(f"{aabb_name} visible Z mass escapes runtime AABB")
        x_coverage = (art_max_x - art_min_x) / size_x
        z_coverage = (art_max_z - art_min_z) / size_z
        if x_coverage < 0.92 or z_coverage < 0.90:
            raise RuntimeError(
                f"{aabb_name} visible mass under-covers collision: X={x_coverage:.3f} Z={z_coverage:.3f}"
            )
        if not (5.6 <= art_top <= 7.0):
            raise RuntimeError(f"{aabb_name} visible height outside 5.6..7.0m: {art_top:.3f}")
        marker = bpy.data.objects.get(f"Marker_Architecture_AABB_{aabb_name}")
        if marker is None or marker.get("runtime_aabb_size_xz") != f"{size_x:.3f},{size_z:.3f}":
            raise RuntimeError(f"{aabb_name} marker contract missing")

    for cover_name, x, z, size_x, size_z, top in RUNTIME_SITE_COVER_AABBS:
        art = [
            obj
            for obj in mesh_objects
            if obj.get("runtime_collision_aabb") == cover_name
            and obj.get("bazaar_role") == "finished_cc0_site_cover_crate_cluster"
        ]
        if len(art) != 12 or bpy.data.objects.get(f"BazaarCover_{cover_name}") is None:
            raise RuntimeError(f"Missing runtime site cover {cover_name}")
        corners = [obj.matrix_world @ Vector(corner) for obj in art for corner in obj.bound_box]
        minimum_x, maximum_x = min(p.x for p in corners), max(p.x for p in corners)
        minimum_z, maximum_z = min(-p.y for p in corners), max(-p.y for p in corners)
        maximum_y = max(p.z for p in corners)
        if (
            abs(minimum_x - (x - size_x * 0.5)) > 0.012
            or abs(maximum_x - (x + size_x * 0.5)) > 0.012
            or abs(minimum_z - (z - size_z * 0.5)) > 0.012
            or abs(maximum_z - (z + size_z * 0.5)) > 0.012
            or abs(maximum_y - top) > 0.012
        ):
            raise RuntimeError(f"Finished CC0 site-cover cluster drifted from AABB: {cover_name}")

    for cover_name, x, z, size_x, size_z, bottom, top in RUNTIME_HIGH_COVER_AABBS:
        art = [
            obj
            for obj in mesh_objects
            if obj.get("runtime_collision_aabb") == cover_name
            and obj.get("bazaar_role") == "finished_cc0_high_cover_barrel_cluster"
        ]
        if len(art) != 8 or bpy.data.objects.get(f"BazaarCover_{cover_name}") is None:
            raise RuntimeError(f"Missing runtime high cover {cover_name}")
        corners = [obj.matrix_world @ Vector(corner) for obj in art for corner in obj.bound_box]
        minimum_x, maximum_x = min(p.x for p in corners), max(p.x for p in corners)
        minimum_z, maximum_z = min(-p.y for p in corners), max(-p.y for p in corners)
        maximum_y = max(p.z for p in corners)
        if (
            abs(minimum_x - (x - size_x * 0.5)) > 0.012
            or abs(maximum_x - (x + size_x * 0.5)) > 0.012
            or abs(minimum_z - (z - size_z * 0.5)) > 0.012
            or abs(maximum_z - (z + size_z * 0.5)) > 0.012
            or abs(maximum_y - top) > 0.012
        ):
            raise RuntimeError(f"Finished CC0 high-cover cluster drifted from AABB: {cover_name}")

    for cart_name, x, z, size_x, size_z, bottom, top in RUNTIME_MID_COVER_AABBS:
        parts = [
            obj
            for obj in mesh_objects
            if obj.get("runtime_collision_aabb") == cart_name
            and obj.get("bazaar_role") == "finished_cc0_market_cart_cover"
        ]
        if len(parts) != 3:
            raise RuntimeError(f"{cart_name} expected three finished Coffee Cart parts, found {len(parts)}")
        corners = [obj.matrix_world @ Vector(corner) for obj in parts for corner in obj.bound_box]
        minimum_x, maximum_x = min(p.x for p in corners), max(p.x for p in corners)
        minimum_z, maximum_z = min(-p.y for p in corners), max(-p.y for p in corners)
        minimum_y, maximum_y = min(p.z for p in corners), max(p.z for p in corners)
        if minimum_x < x - size_x * 0.5 - 0.02 or maximum_x > x + size_x * 0.5 + 0.02:
            raise RuntimeError(f"{cart_name} visible cart escapes runtime X cover")
        if minimum_z < z - size_z * 0.5 - 0.02 or maximum_z > z + size_z * 0.5 + 0.02:
            raise RuntimeError(f"{cart_name} visible cart escapes runtime Z cover")
        if minimum_y < bottom - 0.02 or maximum_y > top + 0.02 or maximum_y < top - 0.10:
            raise RuntimeError(f"{cart_name} visible cart height does not explain runtime cover")

    for name, start, end, bottom, top in RUNTIME_RAIL_SPECS:
        railing = bpy.data.objects.get(name)
        if railing is None:
            raise RuntimeError(f"Missing runtime-aligned visible railing: {name}")
        if railing.get("runtime_railing_start_xz") != f"{start[0]:.3f},{start[1]:.3f}":
            raise RuntimeError(f"Visible railing start drifted: {name}")
        if railing.get("runtime_railing_end_xz") != f"{end[0]:.3f},{end[1]:.3f}":
            raise RuntimeError(f"Visible railing end drifted: {name}")
        if railing.get("runtime_railing_bottom_top") != f"{bottom:.3f},{top:.3f}":
            raise RuntimeError(f"Visible railing height drifted: {name}")
        if railing.get("bazaar_role") != "finished_cc0_authored_sightline_parapet":
            raise RuntimeError(f"Visible railing is not a finished CC0 module assembly: {name}")
        segment_length = sqrt((end[0] - start[0]) ** 2 + (end[1] - start[1]) ** 2)
        expected_cap_modules = max(1, int(round(segment_length / 2.0)))
        expected_rail_modules = expected_cap_modules * 2
        expected_post_modules = max(2, int(segment_length / 3.4) + 1)
        if (
            "IndRoofTrimBStraightFull" not in str(railing.get("source_asset", ""))
            or int(railing.get("authored_module_instances", 0)) != expected_rail_modules
            or int(railing.get("authored_open_rail_rows", 0)) != 2
        ):
            raise RuntimeError(f"Visible railing lacks pinned Trey source mapping: {name}")
        cap = bpy.data.objects.get(f"{name}_AuthoredTreyCap")
        if (
            cap is None
            or cap.get("bazaar_role") != "finished_cc0_authored_parapet_cap"
            or "IndRoofTrimBStraightFull" not in str(cap.get("source_asset", ""))
            or int(cap.get("authored_module_instances", 0)) != expected_cap_modules
        ):
            raise RuntimeError(f"Visible railing lacks authored Trey cap contract: {name}")
        posts = bpy.data.objects.get(f"{name}_AuthoredTreyPosts")
        if (
            posts is None
            or posts.get("bazaar_role") != "finished_cc0_authored_parapet_posts"
            or "IndColumnFree" not in str(posts.get("source_asset", ""))
            or int(posts.get("authored_module_instances", 0)) != expected_post_modules
        ):
            raise RuntimeError(f"Visible railing lacks authored Trey post contract: {name}")

    sight_name, sight_x, sight_z, sight_size_x, sight_size_z, sight_bottom, sight_top = (
        RUNTIME_SITE_PAIR_SIGHT_BLOCK
    )
    sight_art = bpy.data.objects.get("BazaarCollisionArt_SightBlockSitePair")
    if sight_art is None or sight_art.get("runtime_collision_aabb") != sight_name:
        raise RuntimeError("Site-pair sight blocker lacks finished storefront art")
    sight_corners = [sight_art.matrix_world @ Vector(corner) for corner in sight_art.bound_box]
    if max(point.z for point in sight_corners) < sight_top - 0.01:
        raise RuntimeError("Site-pair sight blocker does not cover the 6.4 m runtime height")
    if min(point.x for point in sight_corners) < sight_x - sight_size_x * 0.5 - 0.02:
        raise RuntimeError("Site-pair sight blocker escapes its runtime footprint")

    minimum, maximum = object_world_bounds(mesh_objects)
    # Blender Y corresponds to -Godot Z.  Finished facades sit on the boundary
    # but may not spill more than one meter beyond the authored playable frame.
    if minimum.x < MAP_X_MIN - 0.2 or maximum.x > MAP_X_MAX + 0.2:
        raise RuntimeError(f"Bazaar X bounds escaped frame: {minimum.x}..{maximum.x}")
    if minimum.y < -MAP_Z_MAX - 0.2 or maximum.y > -MAP_Z_MIN + 0.2:
        raise RuntimeError(f"Bazaar Z bounds escaped frame: Blender Y {minimum.y}..{maximum.y}")
    if minimum.z < -0.35 or maximum.z > 10.5:
        raise RuntimeError(f"Bazaar vertical bounds invalid: {minimum.z}..{maximum.z}")
    if maximum.x - minimum.x < 130.0 or maximum.y - minimum.y < 106.0 or maximum.z - minimum.z < 6.0:
        raise RuntimeError(
            "Bazaar authored span gate failed: "
            f"{maximum.x - minimum.x:.3f} x {maximum.y - minimum.y:.3f} x {maximum.z - minimum.z:.3f}"
        )

    unique_meshes = {obj.data for obj in mesh_objects}
    unique_triangles = sum(mesh_triangles(mesh) for mesh in unique_meshes)
    instance_triangles = sum(mesh_triangles(obj.data) for obj in mesh_objects)
    if unique_triangles > MAX_UNIQUE_TRIANGLES:
        raise RuntimeError(f"Unique triangle budget exceeded: {unique_triangles}")
    if instance_triangles > MAX_INSTANCE_TRIANGLES:
        raise RuntimeError(f"Instance triangle budget exceeded: {instance_triangles}")

    materials = {
        material for obj in mesh_objects for material in obj.data.materials if material is not None
    }
    images = {
        node.image
        for material in materials
        if material.use_nodes
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    for image in images:
        ensure_image_pixels_loaded(image)
        if max(image.size) > MAX_TEXTURE_DIMENSION:
            raise RuntimeError(f"Texture exceeds runtime cap: {image.name} {tuple(image.size)}")

    raw_source_triangles = sum(
        spec.expected_triangles for spec in (*SOURCE_SPECS, *COFFEE_CART_SOURCE_SPECS)
    )
    texture_memory_bytes_estimate = sum(
        int(image.size[0] * image.size[1] * 4 * (4.0 / 3.0)) for image in images
    )
    texture_memory_mib_estimate = texture_memory_bytes_estimate / (1024.0 * 1024.0)
    if texture_memory_mib_estimate > MAX_TEXTURE_MEMORY_MIB:
        raise RuntimeError(
            "Estimated RGBA8+mips texture memory exceeds "
            f"{MAX_TEXTURE_MEMORY_MIB:.0f} MiB: {texture_memory_mib_estimate:.2f}"
        )
    surface_draw_count = sum(
        len({poly.material_index for poly in obj.data.polygons}) for obj in mesh_objects
    )

    return {
        "object_count": len(scene_objects),
        "mesh_object_count": len(mesh_objects),
        "unique_mesh_count": len(unique_meshes),
        "material_count": len(materials),
        "texture_count": len(images),
        "draw_node_count": len(mesh_objects),
        "surface_draw_count": surface_draw_count,
        "raw_source_triangles": raw_source_triangles,
        "unique_triangles": unique_triangles,
        "instance_triangles": instance_triangles,
        "texture_memory_bytes_estimate_rgba8_mips": texture_memory_bytes_estimate,
        "texture_memory_mib_estimate_rgba8_mips": round(texture_memory_mib_estimate, 3),
        "blender_bounds_min": [round(value, 4) for value in minimum],
        "blender_bounds_max": [round(value, 4) for value in maximum],
        "stairs": {
            stair.name: {
                "width_m": float(bpy.data.objects[stair.name]["path_width_m"]),
                "run_m": float(bpy.data.objects[stair.name]["run_m"]),
                "slope_degrees": round(float(bpy.data.objects[stair.name]["slope_degrees"]), 4),
                "steps": int(bpy.data.objects[stair.name]["step_count"]),
            }
            for stair in STAIRS
        },
        "platform_top_m": {
            name: expected_top for name, (expected_top, _thickness) in expected_platforms.items()
        },
        "authored_module_contract": {
            "visible_mesh_origin_counts": {
                "cc0": len(mesh_objects),
                "project_authored": 0,
                "procedural": 0,
            },
            "stairs": {
                stair.name: {
                    "primary_source_object": "BazaarSource_IndStairsWideFull",
                    "stair_module_instances": int(
                        bpy.data.objects[stair.name]["source_stair_modules"]
                    ),
                    "foundation_module_instances": int(
                        bpy.data.objects[stair.name]["source_foundation_modules"]
                    ),
                    "foundation_source_object": "BazaarSource_IndFoundationAStraightFull",
                    "guardrail_source_object": "BazaarSource_IndRoofTrimBStraightFull",
                    "guardrail_module_instances": int(
                        bpy.data.objects[f"{stair.name}_AuthoredTreyRails"][
                            "authored_module_instances"
                        ]
                    ),
                    "newel_source_object": "BazaarSource_IndColumnFree",
                    "newel_module_instances": int(
                        bpy.data.objects[f"{stair.name}_AuthoredTreyNewels"][
                            "authored_module_instances"
                        ]
                    ),
                }
                for stair in STAIRS
            },
            "decks": {
                name: {
                    "floor_source_object": "BazaarSource_IndFloorGreyPlatformFull",
                    "foundation_source_object": "BazaarSource_IndFoundationAStraightFull",
                    "top_module_instances": int(bpy.data.objects[name]["source_top_modules"]),
                    "underside_module_instances": int(
                        bpy.data.objects[name]["source_underside_modules"]
                    ),
                    "foundation_module_instances": int(
                        bpy.data.objects[name]["source_foundation_modules"]
                    ),
                }
                for name in expected_platforms
            },
            "runtime_railings": {
                name: {
                    "rail_source_object": "BazaarSource_IndRoofTrimBStraightFull",
                    "rail_module_instances": int(
                        bpy.data.objects[name]["authored_module_instances"]
                    ),
                    "cap_module_instances": int(
                        bpy.data.objects[f"{name}_AuthoredTreyCap"][
                            "authored_module_instances"
                        ]
                    ),
                    "cap_source_object": "BazaarSource_IndRoofTrimBStraightFull",
                    "post_source_object": "BazaarSource_IndColumnFree",
                    "post_module_instances": int(
                        bpy.data.objects[f"{name}_AuthoredTreyPosts"][
                            "authored_module_instances"
                        ]
                    ),
                }
                for name, _start, _end, _bottom, _top in RUNTIME_RAIL_SPECS
            },
            "canopy": {
                "roof_source_object": "BazaarSource_IndRoofDarkGreyAngledFull",
                "roof_module_instances": int(canopy_roof["authored_module_instances"]),
                "post_source_object": "BazaarSource_IndColumnFree",
                "post_module_instances": int(canopy_posts["authored_module_instances"]),
                "trim_source_object": "BazaarSource_IndRoofTrimBStraightFull",
                "trim_module_instances": int(canopy_trim["authored_module_instances"]),
            },
        },
        "runtime_architecture_aabbs": len(RUNTIME_ARCHITECTURE_AABBS),
        "runtime_site_cover_aabbs": len(RUNTIME_SITE_COVER_AABBS),
        "runtime_high_cover_aabbs": len(RUNTIME_HIGH_COVER_AABBS),
        "runtime_mid_cover_aabbs": len(RUNTIME_MID_COVER_AABBS),
        "runtime_railing_segments": len(RUNTIME_RAIL_SPECS),
        "runtime_site_pair_sight_block": sight_name,
    }


def select_export_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in bpy.context.scene.objects:
        if obj.parent == root:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def pack_save_export(root: bpy.types.Object) -> None:
    OUTPUT_SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND), compress=True)

    select_export_hierarchy(root)
    result = bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_yup=True,
        export_skins=False,
        export_animations=False,
        export_attributes=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"glTF export failed: {result}")
    if not OUTPUT_GLB.is_file() or OUTPUT_GLB.stat().st_size == 0:
        raise RuntimeError("Bazaar GLB was not written")
    if OUTPUT_GLB.stat().st_size > MAX_GLB_BYTES:
        raise RuntimeError(f"Bazaar GLB exceeds {MAX_GLB_BYTES} bytes")
    if OUTPUT_BLEND.stat().st_size > MAX_BLEND_BYTES:
        raise RuntimeError(f"Bazaar blend exceeds {MAX_BLEND_BYTES} bytes")


def validate_round_trip(expected: dict[str, object]) -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB round-trip import failed: {result}")

    objects = list(bpy.context.scene.objects)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    names = {obj.name for obj in objects}
    required = {
        "BazaarGroundAuthoredMesh",
        "Bazaar_A_Gallery_Deck",
        "Bazaar_Mid_Bridge_Deck",
        "Bazaar_B_Balcony_Deck",
        *(stair.name for stair in STAIRS),
    }
    if not required.issubset(names):
        raise RuntimeError(f"Round-trip missing objects: {sorted(required - names)}")
    if len(meshes) != int(expected["mesh_object_count"]):
        raise RuntimeError(
            f"Round-trip mesh count changed: expected {expected['mesh_object_count']}, found {len(meshes)}"
        )

    for obj in meshes:
        if not obj.data.uv_layers:
            raise RuntimeError(f"Round-trip UV missing: {obj.name}")
        if len(obj.data.uv_layers.active.data) != len(obj.data.loops):
            raise RuntimeError(f"Round-trip UV loop coverage changed: {obj.name}")
        if any(
            not isfinite(value)
            for uv_loop in obj.data.uv_layers.active.data
            for value in uv_loop.uv
        ):
            raise RuntimeError(f"Round-trip contains non-finite UVs: {obj.name}")
        for polygon in obj.data.polygons:
            if (
                polygon.material_index >= len(obj.data.materials)
                or obj.data.materials[polygon.material_index] is None
            ):
                raise RuntimeError(f"Round-trip polygon lacks material: {obj.name}")
        origin = str(obj.get("bazaar_asset_origin", ""))
        if origin != "cc0":
            raise RuntimeError(
                f"Round-trip visible mesh is not a finished CC0 asset: {obj.name} origin={origin!r}"
            )
        for metadata_key in (
            "license",
            "source_asset",
            "source_object",
            "source_creator",
            "source_url",
        ):
            if not str(obj.get(metadata_key, "")).strip():
                raise RuntimeError(
                    f"Round-trip provenance metadata {metadata_key} missing: {obj.name}"
                )
        if "CC0" not in str(obj.get("license", "")):
            raise RuntimeError(f"Round-trip CC0 license metadata missing: {obj.name}")
        searchable = " ".join(str(obj.get(k, "")) for k in ("source_asset", "source_creator", "source_url", "license")).lower()
        if any(token in searchable for token in FORBIDDEN_SOURCE_TOKENS):
            raise RuntimeError(f"Round-trip includes forbidden source: {obj.name}")

    minimum, maximum = object_world_bounds(meshes)
    expected_min = Vector(expected["blender_bounds_min"])
    expected_max = Vector(expected["blender_bounds_max"])
    if (minimum - expected_min).length > 0.02 or (maximum - expected_max).length > 0.02:
        raise RuntimeError(
            f"Round-trip bounds changed: {tuple(minimum)}..{tuple(maximum)} vs "
            f"{tuple(expected_min)}..{tuple(expected_max)}"
        )

    materials = {m for obj in meshes for m in obj.data.materials if m}
    images = {
        node.image
        for material in materials
        if material.use_nodes
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    for image in images:
        if min(image.size) <= 0 or not image.has_data:
            raise RuntimeError(f"Round-trip embedded texture missing: {image.name}")

    imported_unique_meshes = {obj.data for obj in meshes}
    if len(imported_unique_meshes) != int(expected["unique_mesh_count"]):
        raise RuntimeError(
            "Round-trip shared-mesh count changed: "
            f"expected {expected['unique_mesh_count']}, found {len(imported_unique_meshes)}"
        )
    instance_triangles = sum(mesh_triangles(obj.data) for obj in meshes)
    if instance_triangles != int(expected["instance_triangles"]):
        raise RuntimeError(
            "Round-trip instance triangle count changed: "
            f"expected {expected['instance_triangles']}, found {instance_triangles}"
        )
    return {
        "object_count": len(objects),
        "mesh_object_count": len(meshes),
        "unique_mesh_count": len(imported_unique_meshes),
        "material_count": len(materials),
        "texture_count": len(images),
        "instance_triangles": instance_triangles,
        "bounds_min": [round(value, 4) for value in minimum],
        "bounds_max": [round(value, 4) for value in maximum],
    }


def write_report(scene_stats: dict[str, object], round_trip: dict[str, object]) -> dict[str, object]:
    report = {
        "status": "BAZAAR_DCC_PASS",
        "blender_version": bpy.app.version_string,
        "source_blend": str(SOURCE_BLEND.relative_to(REPO_ROOT)).replace("\\", "/"),
        "source_blend_sha256": sha256(SOURCE_BLEND.read_bytes()).hexdigest().upper(),
        "blend": {
            "path": str(OUTPUT_BLEND.relative_to(REPO_ROOT)).replace("\\", "/"),
            "bytes": OUTPUT_BLEND.stat().st_size,
            "sha256": sha256(OUTPUT_BLEND.read_bytes()).hexdigest().upper(),
        },
        "glb": {
            "path": str(OUTPUT_GLB.relative_to(REPO_ROOT)).replace("\\", "/"),
            "bytes": OUTPUT_GLB.stat().st_size,
            "sha256": sha256(OUTPUT_GLB.read_bytes()).hexdigest().upper(),
        },
        "scene": scene_stats,
        "round_trip": round_trip,
        "license_gate": (
            "Every exported visible mesh is an arrangement or adapted instance of a "
            "whitelisted finished CC0 source; project-authored work is limited to "
            "materials, UV retargeting, metadata, and invisible layout scaffolding"
        ),
        "forbidden_sources_absent": list(FORBIDDEN_SOURCE_TOKENS),
        "previews": [
            str(path.relative_to(REPO_ROOT)).replace("\\", "/")
            for path in sorted(PREVIEW_DIR.glob("*.png"))
        ],
    }
    serialized = json.dumps(report, indent=2, ensure_ascii=False).replace("\n", "\r\n") + "\r\n"
    # Write bytes after explicit normalization.  Path.write_text() uses the
    # platform newline translator on Windows and would turn CRLF into CRCRLF.
    OUTPUT_REPORT.write_bytes(serialized.encode("utf-8"))
    return report


def main() -> None:
    ensure_authoritative_source()
    OUTPUT_SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_RUNTIME_DIR.mkdir(parents=True, exist_ok=True)
    templates = validate_and_extract_sources()
    clean_source_scene(templates)
    materials = clone_approved_materials()
    root, collections = build_map(templates, materials)
    triangulate_visible_meshes(root)
    bpy.context.view_layer.update()
    render_previews(collections["review"])
    scene_stats = validate_authored_scene(root)
    pack_save_export(root)
    round_trip = validate_round_trip(scene_stats)
    report = write_report(scene_stats, round_trip)
    print(
        "BAZAAR_DCC_CHECK "
        f"draw_nodes={scene_stats['draw_node_count']} surfaces={scene_stats['surface_draw_count']} "
        f"raw_source_tris={scene_stats['raw_source_triangles']} "
        f"unique_tris={scene_stats['unique_triangles']} "
        f"instance_tris={scene_stats['instance_triangles']} "
        f"materials={scene_stats['material_count']} textures={scene_stats['texture_count']} "
        f"texture_mib_rgba8_mips={scene_stats['texture_memory_mib_estimate_rgba8_mips']} "
        f"glb_bytes={report['glb']['bytes']} blend_bytes={report['blend']['bytes']}"
    )
    print(
        "BAZAAR_DCC_PASS valid=True cc0_only=True stairs=6 platforms=3 "
        "runtime_aabbs=11 mid_carts=2 site_pair_block=True rail_gaps=True "
        "round_trip=True"
    )


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(2)
