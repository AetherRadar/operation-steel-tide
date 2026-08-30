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
    "1E6C91C5AA1B7D798B5C603BB2CE40C89B5C3255A9047209EEAB109C9F4730F9"
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
# Godot 4.6 cannot import Blender's KHR_draco_mesh_compression output.  The
# runtime GLB therefore keeps its vertex streams uncompressed; the higher cap
# reflects that compatible representation without relaxing geometry budgets.
MAX_GLB_BYTES = 120_000_000
MAX_BLEND_BYTES = 85_000_000
MAX_INSTANCE_TRIANGLES = 3_000_000
MAX_UNIQUE_TRIANGLES = 900_000
MAX_EXPORT_DRAW_NODES = 800
TARGET_EXPORT_DRAW_NODES = 780
MAX_TEXTURE_DIMENSION = 1024
MAX_TEXTURE_MEMORY_MIB = 240.0

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
    SourceSpec(
        "trey_wall",
        "BazaarSource_IndWallFull",
        "Modular Industrial Pieces / IndWallFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        2,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_arch",
        "BazaarSource_IndWallArchDouble",
        "Modular Industrial Pieces / IndWallArchDouble",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        228,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_arch_columns",
        "BazaarSource_IndWallArchDoubleColumns",
        "Modular Industrial Pieces / IndWallArchDoubleColumns",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        28,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_arch_cap",
        "BazaarSource_IndWallArchDoubleCapGrey",
        "Modular Industrial Pieces / IndWallArchDoubleCapGrey",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        20,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_door_frame",
        "BazaarSource_IndDoorFrameSingle",
        "Modular Industrial Pieces / IndDoorFrameSingle",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        42,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_roof",
        "BazaarSource_IndRoofDarkGreyFull",
        "Modular Industrial Pieces / IndRoofDarkGreyFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        4,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_floor_solid",
        "BazaarSource_IndFloorGreyFull",
        "Modular Industrial Pieces / IndFloorGreyFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        4,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_window",
        "BazaarSource_IndWindowBFull",
        "Modular Industrial Pieces / IndWindowBFull",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        322,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "trey_roof_trim",
        "BazaarSource_IndRoofTrimAStraight",
        "Modular Industrial Pieces / IndRoofTrimAStraight",
        "Trey Ramm (minime453)",
        "https://opengameart.org/content/modular-industrial-kit",
        16,
        ("BazaarTreyGradientSource",),
    ),
    SourceSpec(
        "quat_brick_plain",
        "BazaarSource_QuatBrickPlain",
        "Downtown City MegaKit / Brick_Plain_1",
        "Quaternius (@Quaternius)",
        "https://quaternius.com/packs/downtowncitymegakit.html",
        4,
        ("MI_RedBrick", "MI_InteriorWall"),
    ),
    SourceSpec(
        "quat_door_frame",
        "BazaarSource_QuatDoorFrameTrim",
        "Downtown City MegaKit / DoorFrame_Trim",
        "Quaternius (@Quaternius)",
        "https://quaternius.com/packs/downtowncitymegakit.html",
        860,
        ("MI_Trim_Green", "MI_Glass", "MI_Trim", "MI_InteriorWall"),
    ),
    SourceSpec(
        "quat_curved_window",
        "BazaarSource_QuatBrickWindowCurvedDouble",
        "Downtown City MegaKit / Brick_Window_CurvedDouble",
        "Quaternius (@Quaternius)",
        "https://quaternius.com/packs/downtowncitymegakit.html",
        1137,
        (
            "MI_RedBrick_Pale",
            "MI_InteriorWall",
            "MI_Trim_MetalConcrete",
            "MI_Glass",
            "MI_FakeInterior",
        ),
    ),
    SourceSpec(
        "quat_window_trim",
        "BazaarSource_QuatBrickWindowTrim",
        "Downtown City MegaKit / Brick_Window_Trim",
        "Quaternius (@Quaternius)",
        "https://quaternius.com/packs/downtowncitymegakit.html",
        652,
        ("MI_RedBrick", "MI_Trim", "MI_Glass", "MI_FakeInterior", "MI_InteriorWall"),
    ),
    SourceSpec(
        "quat_floor",
        "BazaarSource_QuatFloor4x4",
        "Downtown City MegaKit / Floor_4x4",
        "Quaternius (@Quaternius)",
        "https://quaternius.com/packs/downtowncitymegakit.html",
        16,
        ("MI_InteriorFloor", "MI_InteriorRoof"),
    ),
    SourceSpec(
        "quat_metal_window",
        "BazaarSource_QuatMetalFirstFloorWindow",
        "Downtown City MegaKit / Metal_FirstFloor_Window",
        "Quaternius (@Quaternius)",
        "https://quaternius.com/packs/downtowncitymegakit.html",
        156,
        ("MI_Trim_Dark", "MI_Trim_MetalConcrete", "MI_Glass", "MI_FakeInterior", "MI_InteriorWall"),
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
    StairSpec("Bazaar_A_Gallery_South_Stair", -56.0, 2.1, -56.0, -9.0, 3.6, 3.2, 20, 0.555, "A_Gallery"),
    StairSpec("Bazaar_A_Gallery_Rear_Stair", -41.9, -27.0, -53.0, -27.0, 3.6, 3.2, 20, 0.555, "A_Gallery"),
    StairSpec("Bazaar_B_Balcony_South_Stair", 56.0, 1.5, 56.0, -9.0, 3.4, 3.2, 20, 0.525, "B_Balcony"),
    StairSpec("Bazaar_B_Balcony_Rear_Stair", 42.5, -27.0, 53.0, -27.0, 3.4, 3.2, 20, 0.525, "B_Balcony"),
    StairSpec("Bazaar_Mid_Mezzanine_South_Stair", -6.0, 40.85, -6.0, 31.0, 3.2, 3.2, 18, 9.85 / 18.0, "Mid_Mezzanine"),
    StairSpec("Bazaar_Mid_Mezzanine_North_Stair", -6.0, 7.15, -6.0, 17.0, 3.2, 3.2, 18, 9.85 / 18.0, "Mid_Mezzanine"),
)

# Frozen invisible runtime-collision contract supplied by the Bazaar gameplay
# builder.  The DCC scene places finished CC0 building masses or authored cover
# at every one of these footprints so players never meet an unexplained wall.
RUNTIME_ARCHITECTURE_AABBS = (
    ("AttackWest", -36.5, 48.5, 43.0, 13.0, 7.4),
    ("AttackEast", 36.5, 48.5, 43.0, 13.0, 7.6),
    ("AttackWestEntryWing", -12.0, 48.25, 6.0, 13.5, 8.0),
    ("AttackEastEntryWing", 12.0, 48.25, 6.0, 13.5, 8.0),
    ("WestLaneLink", -45.5, 39.25, 7.0, 5.5, 8.4),
    ("EastLaneLink", 45.5, 39.25, 7.0, 5.5, 8.4),
    ("WestApproachFacadeReturn", -49.0, 4.0, 0.42, 16.0, 8.0),
    ("EastApproachFacadeReturn", 52.0, 3.0, 0.42, 18.0, 8.0),
    ("WestConnectorReturn", -20.0, -19.4, 0.42, 3.2, 8.0),
    ("EastConnectorReturn", 20.0, -13.4, 0.42, 2.8, 8.0),
    ("MidCarpetSouthFacadeReturn", 5.5, 34.0, 5.0, 0.42, 6.2),
    ("DefenderWestFoyerPier", -20.0, -51.1, 0.42, 9.8, 7.6),
    ("DefenderEastFoyerPier", 20.0, -51.1, 0.42, 9.8, 7.6),
    ("DefenderWestFoyerReturn", -13.75, -46.2, 12.5, 0.42, 7.6),
    ("DefenderEastFoyerReturn", 13.75, -46.2, 12.5, 0.42, 7.6),
    ("SouthWest", -37.0, 24.25, 42.0, 24.5, 8.0),
    ("SouthEast", 37.0, 24.25, 42.0, 24.5, 8.2),
    ("SeparationWestNorth", -19.25, -26.0, 19.5, 10.0, 7.4),
    ("SeparationWestSouth", -19.25, -4.5, 19.5, 21.0, 6.4),
    ("SeparationEastNorth", 19.25, -24.5, 19.5, 13.0, 7.8),
    ("SeparationEastSouth", 19.25, -3.0, 19.5, 18.0, 6.6),
    ("BoundaryWest", -65.5, 0.0, 3.0, 112.0, 8.3),
    ("BoundaryEast", 65.5, 0.0, 3.0, 112.0, 8.3),
    ("WestServiceClosure", -62.0, 4.0, 4.0, 16.0, 8.0),
    ("EastServiceClosure", 62.0, 4.0, 4.0, 16.0, 8.0),
    ("RearFarWest", -58.5, -43.5, 11.0, 25.0, 8.1),
    ("RearWest", -44.0, -49.0, 18.0, 14.0, 7.4),
    ("RearWestSpawn", -12.0, -37.0, 10.0, 12.0, 6.6),
    ("RearDefenderGuild", 0.0, -37.0, 14.0, 12.0, 7.2),
    ("RearEastSpawn", 13.5, -37.0, 13.0, 12.0, 6.9),
    ("RearEast", 44.0, -49.0, 18.0, 14.0, 7.7),
    ("RearFarEast", 58.5, -45.0, 11.0, 22.0, 8.3),
)

RUNTIME_SITE_COVER_AABBS = ()

RUNTIME_HIGH_COVER_AABBS = ()

RUNTIME_MID_COVER_AABBS = ()

RUNTIME_SITE_PAIR_SIGHT_BLOCK = (
    "SightBlockSitePair",
    0.0,
    -15.5,
    18.0,
    17.0,
    0.0,
    6.2,
)

RUNTIME_RAIL_SPECS = (
    ("Bazaar_A_Gallery_Inner_Rail", (-53.0, -23.8), (-53.0, -9.0), 3.6, 4.7),
    ("Bazaar_B_Balcony_Inner_Rail", (53.0, -9.0), (53.0, -23.8), 3.4, 4.5),
    ("Bazaar_Mid_Mezzanine_Inner_Rail", (-3.0, 17.0), (-3.0, 31.0), 3.2, 4.3),
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
    scene.render.resolution_x = 800
    scene.render.resolution_y = 500
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.eevee.taa_render_samples = 16
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.60
    scene.world = bpy.data.worlds.new("BazaarCrossing_World")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.045, 0.07, 0.095, 1.0)
    background.inputs["Strength"].default_value = 0.44


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

    materials["BazaarSiteA_Paint"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarSiteA_Paint",
        (0.48, 0.13, 0.07, 1.0),
        0.82,
        tint_strength=0.54,
    )
    materials["BazaarSiteB_Paint"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarSiteB_Paint",
        (0.08, 0.28, 0.34, 1.0),
        0.80,
        tint_strength=0.52,
    )
    materials["BazaarAwningCanvas"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarAwningCanvas",
        (0.32, 0.08, 0.05, 1.0),
        0.90,
        tint_strength=0.58,
    )
    materials["BazaarWarmPlaster"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarWarmPlaster",
        (0.46, 0.30, 0.18, 1.0),
        0.88,
        tint_strength=0.34,
    )
    # V2 roof, stair, and shopfront accents remain adaptations of the packed
    # Poly Haven surface set.  Keeping the source colour/normal texture graph
    # makes the overview read as a material palette instead of alternating
    # featureless white and black slabs.
    materials["BazaarRoofClay"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarRoofClay",
        (0.72, 0.30, 0.18, 1.0),
        0.82,
        tint_strength=0.62,
    )
    materials["BazaarRoofSlate"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarRoofSlate",
        (0.28, 0.43, 0.50, 1.0),
        0.78,
        tint_strength=0.42,
    )
    materials["BazaarRoofSandstone"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarRoofSandstone",
        (0.78, 0.62, 0.42, 1.0),
        0.84,
        tint_strength=0.54,
    )
    materials["BazaarPaintedSteel"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarPaintedSteel",
        (0.18, 0.31, 0.36, 1.0),
        0.64,
        metallic=0.18,
        tint_strength=0.38,
    )
    materials["BazaarSignOchre"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarSignOchre",
        (0.92, 0.48, 0.12, 1.0),
        0.72,
        tint_strength=0.62,
    )
    materials["BazaarSignTeal"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarSignTeal",
        (0.10, 0.52, 0.54, 1.0),
        0.70,
        tint_strength=0.60,
    )
    materials["BazaarInteriorTerracotta"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarInteriorTerracotta",
        (0.45, 0.24, 0.15, 1.0),
        0.88,
        tint_strength=0.42,
    )
    materials["BazaarInteriorSlate"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarInteriorSlate",
        (0.20, 0.29, 0.31, 1.0),
        0.84,
        tint_strength=0.28,
    )
    materials["BazaarInteriorSand"] = make_tinted_pbr_material(
        materials["BazaarStonePaving"],
        "BazaarInteriorSand",
        (0.54, 0.40, 0.25, 1.0),
        0.90,
        tint_strength=0.34,
    )
    materials["BazaarDarkTimber"] = make_tinted_pbr_material(
        materials["BazaarWeatheredConcrete"],
        "BazaarDarkTimber",
        (0.20, 0.105, 0.055, 1.0),
        0.86,
        tint_strength=0.56,
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


def make_tinted_pbr_material(
    source: bpy.types.Material,
    name: str,
    tint: tuple[float, float, float, float],
    roughness: float,
    *,
    metallic: float = 0.0,
    tint_strength: float = 0.62,
) -> bpy.types.Material:
    """Create a texture-preserving colourway from one approved packed PBR."""
    material = source.copy()
    material.name = name
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = next(
        (node for node in nodes if node.type == "BSDF_PRINCIPLED"),
        None,
    )
    if principled is None:
        raise RuntimeError(f"Cannot tint PBR without Principled shader: {source.name}")
    base = principled.inputs.get("Base Color")
    if base is None:
        raise RuntimeError(f"Cannot tint PBR without Base Color socket: {source.name}")
    incoming = base.links[0] if base.links else None
    colour = nodes.new("ShaderNodeMixRGB")
    colour.name = f"{name}_TextureTint"
    colour.label = "Bazaar texture-preserving tint"
    colour.blend_type = "MIX"
    # Preserve enough albedo variation to keep the packed texture readable,
    # while letting the architectural colourway lift originally near-black
    # concrete into a legible roof/stair/shopfront palette.
    colour.inputs[0].default_value = tint_strength
    colour.inputs[2].default_value = tint
    if incoming is not None:
        source_socket = incoming.from_socket
        links.remove(incoming)
        links.new(source_socket, colour.inputs[1])
    else:
        colour.inputs[1].default_value = base.default_value
    links.new(colour.outputs[0], base)
    for socket_name, value in (("Roughness", roughness), ("Metallic", metallic)):
        socket = principled.inputs.get(socket_name)
        if socket is None:
            continue
        for link in list(socket.links):
            links.remove(link)
        socket.default_value = value
    material["license"] = CC0_LICENSE
    material["source_material"] = source.name
    material["bazaar_material_role"] = "texture-preserving CC0 PBR colourway"
    material["bazaar_tint_strength"] = tint_strength
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
    side_material: bpy.types.Material | None,
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
    if side_material is None and additional_modules:
        raise ValueError(
            f"Source-material assembly {name} must use exactly one module type"
        )
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
    if side_material is None:
        for material in template.materials:
            if material is not None:
                mesh.materials.append(material)
        if not mesh.materials:
            raise RuntimeError(f"Source-material assembly {name} has no authored materials")
    else:
        mesh.materials.append(side_material)
        if top_material is not None and top_material != side_material:
            mesh.materials.append(top_material)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    if side_material is not None:
        if top_material is not None and top_material != side_material:
            for polygon in mesh.polygons:
                polygon.material_index = 1 if polygon.normal.z > 0.52 else 0
        else:
            # Imported modules may carry two or more source slots.  A Bazaar
            # colourway intentionally replaces the full finish, so normalize
            # every inherited face index to the single adapted PBR slot.
            for polygon in mesh.polygons:
                polygon.material_index = 0
    obj = bpy.data.objects.new(name, mesh)
    link_object(obj, collection, root)
    set_asset_metadata(obj, origin="cc0", role=role)
    obj["source_asset"] = " | ".join(group_source.source_asset for _, _, group_source in module_groups)
    obj["source_creator"] = source.source_creator
    obj["source_url"] = source.source_url
    obj["source_object"] = " | ".join(group_source.object_name for _, _, group_source in module_groups)
    obj["authored_module_instances"] = sum(len(group_transforms) for _, group_transforms, _ in module_groups)
    obj["authored_module_types"] = len(module_groups)
    if side_material is not None:
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
    if spec.steps <= source_steps or spec.steps > source_steps * 2:
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
    up = Vector((0.0, 0.0, 1.0))
    slope_length = sqrt(run * run + spec.top_height * spec.top_height)
    tiles = max(2, int(round(run / 2.0)))
    cell = slope_length / tiles
    bottom = godot_to_blender(spec.bottom_x, 0.0, spec.bottom_z)
    transforms: list[Matrix] = []
    rail_offset = spec.width * 0.5 + 0.06
    rail_levels = (0.48, 0.92)
    for side_sign in (-1.0, 1.0):
        base = bottom + lateral * rail_offset * side_sign
        for rail_level in rail_levels:
            for index in range(tiles):
                transforms.append(
                    authored_basis_matrix(
                        base + slope * (cell * (index + 0.5)) + up * rail_level,
                        slope,
                        lateral,
                        normal,
                        (cell * 0.5, 0.13 / 0.30, 0.13 / 1.20),
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
    rails["open_rail_rows"] = len(rail_levels)

    newel_positions: list[tuple[float, float, float, float]] = []
    post_segments = max(3, int(round(run / 2.7)))
    for side_sign in (-1.0, 1.0):
        side = rail_offset * side_sign
        px, pz = -uz, ux
        for post_index in range(post_segments + 1):
            fraction = post_index / post_segments
            step_height = spec.top_height * fraction
            newel_positions.append(
                (
                    spec.bottom_x + dx * fraction + px * side,
                    spec.bottom_z + dz * fraction + pz * side,
                    step_height,
                    step_height + 1.02,
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
    newels["post_segments"] = post_segments
    return rails, newels


def create_authored_stair_supports(
    spec: StairSpec,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Give each long stair visible Trey stringers and grounded support posts."""
    dx, dz = spec.top_x - spec.bottom_x, spec.top_z - spec.bottom_z
    run = sqrt(dx * dx + dz * dz)
    ux, uz = dx / run, dz / run
    horizontal = Vector((ux, -uz, 0.0))
    lateral = Vector((-horizontal.y, horizontal.x, 0.0))
    slope = Vector((ux, -uz, spec.top_height / run)).normalized()
    normal = slope.cross(lateral).normalized()
    up = Vector((0.0, 0.0, 1.0))
    bottom = godot_to_blender(spec.bottom_x, 0.0, spec.bottom_z)
    slope_length = sqrt(run * run + spec.top_height * spec.top_height)
    tiles = max(2, int(round(run / 2.0)))
    cell = slope_length / tiles
    stringer_offset = spec.width * 0.5 - 0.16
    stringer_transforms = [
        authored_basis_matrix(
            bottom
            + lateral * stringer_offset * side_sign
            + slope * (cell * (index + 0.5))
            + up * 0.03,
            slope,
            lateral,
            normal,
            (cell * 0.5, 0.20 / 0.30, 0.24 / 1.20),
        )
        for side_sign in (-1.0, 1.0)
        for index in range(tiles)
    ]
    support_transforms: list[Matrix] = []
    support_count = 0
    for fraction in (0.34, 0.67, 0.94):
        support_top = max(0.34, spec.top_height * fraction - 0.12)
        for side_sign in (-1.0, 1.0):
            side = stringer_offset * side_sign
            px, pz = -uz, ux
            x = spec.bottom_x + dx * fraction + px * side
            z = spec.bottom_z + dz * fraction + pz * side
            support_transforms.append(
                authored_basis_matrix(
                    godot_to_blender(x, 0.0, z),
                    Vector((1.0, 0.0, 0.0)),
                    Vector((0.0, 1.0, 0.0)),
                    up,
                    (0.24 / 0.40, 0.24 / 0.40, support_top / 3.0),
                )
            )
            support_count += 1
    supports = create_authored_module_assembly(
        f"{spec.name}_AuthoredTreySupports",
        templates["trey_rail"],
        stringer_transforms,
        specs["trey_rail"],
        material,
        collection,
        root,
        role="finished_cc0_authored_stair_stringers_and_supports",
        additional_modules=[
            (templates["trey_column"], support_transforms, specs["trey_column"])
        ],
        weld=False,
        uv_tile=0.72,
    )
    supports["stair_contract"] = spec.name
    supports["stringer_module_instances"] = len(stringer_transforms)
    supports["ground_support_instances"] = support_count
    return supports


def create_authored_stair_tread_nosings(
    spec: StairSpec,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Expose every tread edge with a thin finished Trey trim module."""
    dx, dz = spec.top_x - spec.bottom_x, spec.top_z - spec.bottom_z
    run = sqrt(dx * dx + dz * dz)
    ux, uz = dx / run, dz / run
    horizontal = Vector((ux, -uz, 0.0))
    lateral = Vector((-horizontal.y, horizontal.x, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    bottom = godot_to_blender(spec.bottom_x, 0.0, spec.bottom_z)
    rise = spec.top_height / spec.steps
    transforms = [
        authored_basis_matrix(
            bottom
            + horizontal * (spec.tread * (step_index + 0.96))
            + up * (rise * (step_index + 1) - 0.025),
            lateral,
            horizontal,
            up,
            (spec.width * 0.5, 0.10 / 0.30, 0.055 / 1.20),
        )
        for step_index in range(spec.steps)
    ]
    nosings = create_authored_module_assembly(
        f"{spec.name}_AuthoredTreyTreadNosings",
        templates["trey_rail"],
        transforms,
        specs["trey_rail"],
        material,
        collection,
        root,
        role="finished_cc0_authored_stair_tread_nosings",
        weld=False,
        uv_tile=0.52,
    )
    nosings["stair_contract"] = spec.name
    nosings["tread_nosing_instances"] = len(transforms)
    return nosings


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


def source_dimensions(mesh: bpy.types.Mesh) -> Vector:
    minimum = Vector(
        (
            min(vertex.co.x for vertex in mesh.vertices),
            min(vertex.co.y for vertex in mesh.vertices),
            min(vertex.co.z for vertex in mesh.vertices),
        )
    )
    maximum = Vector(
        (
            max(vertex.co.x for vertex in mesh.vertices),
            max(vertex.co.y for vertex in mesh.vertices),
            max(vertex.co.z for vertex in mesh.vertices),
        )
    )
    return maximum - minimum


def make_module_run(
    name: str,
    start: tuple[float, float],
    end: tuple[float, float],
    bottom: float,
    height: float,
    pattern: tuple[str, ...],
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
    material: bpy.types.Material | None = None,
    nominal_cell: float = 2.0,
    depth_scale: float = 1.0,
) -> list[bpy.types.Object]:
    """Compose one thick, authored wall/beam run from finished CC0 modules."""
    dx, dz = end[0] - start[0], end[1] - start[1]
    length = sqrt(dx * dx + dz * dz)
    if length < 0.12:
        return []
    ux, uz = dx / length, dz / length
    forward = Vector((ux, -uz, 0.0))
    depth_axis = Vector((uz, ux, 0.0))
    up = Vector((0.0, 0.0, 1.0))
    cell_count = max(1, int(round(length / nominal_cell)))
    cell = length / cell_count
    grouped: dict[str, list[Matrix]] = {key: [] for key in pattern}
    for index in range(cell_count):
        key = pattern[index % len(pattern)]
        dimensions = source_dimensions(templates[key])
        if dimensions.x <= 0.001 or dimensions.z <= 0.001:
            raise RuntimeError(f"Degenerate authored run source {key}")
        center_distance = cell * (index + 0.5)
        x = start[0] + ux * center_distance
        z = start[1] + uz * center_distance
        grouped[key].append(
            authored_basis_matrix(
                godot_to_blender(x, bottom, z),
                forward,
                depth_axis,
                up,
                (
                    cell / dimensions.x,
                    depth_scale,
                    height / dimensions.z,
                ),
            )
        )

    objects: list[bpy.types.Object] = []
    for key, transforms in grouped.items():
        if not transforms:
            continue
        suffix = key.removeprefix("quat_").removeprefix("trey_")
        obj = create_authored_module_assembly(
            f"{name}_{suffix}",
            templates[key],
            transforms,
            specs[key],
            material,
            collection,
            root,
            role=role,
            weld=False,
            uv_tile=1.5,
        )
        obj["godot_run_start_xz"] = f"{start[0]:.3f},{start[1]:.3f}"
        obj["godot_run_end_xz"] = f"{end[0]:.3f},{end[1]:.3f}"
        obj["run_bottom_top_y"] = f"{bottom:.3f},{bottom + height:.3f}"
        obj["module_cell_m"] = round(cell, 5)
        objects.append(obj)
    return objects


def make_segmented_wall(
    name: str,
    start: tuple[float, float],
    end: tuple[float, float],
    openings: tuple[tuple[float, float], ...],
    bottom: float,
    height: float,
    pattern: tuple[str, ...],
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
    material: bpy.types.Material | None = None,
    nominal_cell: float = 2.0,
    depth_scale: float = 1.0,
) -> list[bpy.types.Object]:
    """Build a wall while leaving exact distance-along-run gameplay openings."""
    dx, dz = end[0] - start[0], end[1] - start[1]
    length = sqrt(dx * dx + dz * dz)
    ux, uz = dx / length, dz / length
    clipped = sorted(
        (max(0.0, low), min(length, high))
        for low, high in openings
        if high > 0.0 and low < length
    )
    cursor = 0.0
    objects: list[bpy.types.Object] = []
    section = 0
    for low, high in (*clipped, (length, length)):
        if low - cursor > 0.12:
            section_start = (start[0] + ux * cursor, start[1] + uz * cursor)
            section_end = (start[0] + ux * low, start[1] + uz * low)
            objects.extend(
                make_module_run(
                    f"{name}_Section{section:02d}",
                    section_start,
                    section_end,
                    bottom,
                    height,
                    pattern,
                    templates,
                    specs,
                    collection,
                    root,
                    role=role,
                    material=material,
                    nominal_cell=nominal_cell,
                    depth_scale=depth_scale,
                )
            )
            section += 1
        cursor = max(cursor, high)
    return objects


def make_tiled_patch(
    name: str,
    key: str,
    center_x: float,
    center_z: float,
    size_x: float,
    size_z: float,
    bottom: float,
    thickness: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    role: str,
    material: bpy.types.Material | None = None,
) -> bpy.types.Object:
    dimensions = source_dimensions(templates[key])
    columns = max(1, int(round(size_x / dimensions.x)))
    rows = max(1, int(round(size_z / dimensions.y)))
    cell_x, cell_z = size_x / columns, size_z / rows
    transforms: list[Matrix] = []
    for row in range(rows):
        for column in range(columns):
            x = center_x - size_x * 0.5 + cell_x * (column + 0.5)
            z = center_z - size_z * 0.5 + cell_z * (row + 0.5)
            transforms.append(
                authored_basis_matrix(
                    godot_to_blender(x, bottom, z),
                    Vector((1.0, 0.0, 0.0)),
                    Vector((0.0, 1.0, 0.0)),
                    Vector((0.0, 0.0, 1.0)),
                    (
                        cell_x / dimensions.x,
                        cell_z / dimensions.y,
                        thickness / dimensions.z,
                    ),
                )
            )
    obj = create_authored_module_assembly(
        name,
        templates[key],
        transforms,
        specs[key],
        material,
        collection,
        root,
        role=role,
        weld=True,
        uv_tile=2.0,
    )
    obj["godot_center_xz"] = f"{center_x:.3f},{center_z:.3f}"
    obj["footprint_m"] = f"{size_x:.3f},{size_z:.3f}"
    obj["bottom_top_y"] = f"{bottom:.3f},{bottom + thickness:.3f}"
    obj["source_tile_grid"] = f"{columns}x{rows}"
    return obj


def make_portal(
    name: str,
    position: tuple[float, float, float],
    yaw_degrees: float,
    opening_width: float,
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    region: str,
) -> bpy.types.Object:
    dimensions = source_dimensions(templates["trey_arch"])
    portal = place_source(
        templates,
        specs,
        "trey_arch",
        name,
        position,
        yaw_degrees,
        (opening_width / dimensions.x, 1.0, 0.42 / dimensions.y),
        collection,
        root,
        role="finished_cc0_runtime_door_portal",
    )
    portal["interior_region"] = region
    portal["clear_opening_width_m"] = opening_width
    return portal


def make_closed_block(
    name: str,
    bounds: tuple[float, float, float, float],
    height: float,
    facade_pattern: tuple[str, ...],
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    roof_material: bpy.types.Material,
    wall_material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    xmin, xmax, zmin, zmax = bounds
    sides = (
        ((xmin, zmin), (xmax, zmin)),
        ((xmax, zmin), (xmax, zmax)),
        ((xmax, zmax), (xmin, zmax)),
        ((xmin, zmax), (xmin, zmin)),
    )
    for index, (start, end) in enumerate(sides):
        pattern = facade_pattern if index in (0, 2) else ("trey_foundation",)
        material = None if any(key.startswith("quat_") for key in pattern) else wall_material
        make_module_run(
            f"{name}_Wall{index:02d}",
            start,
            end,
            0.0,
            height,
            pattern,
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_closed_urban_block_wall",
            material=material,
        )
    make_tiled_patch(
        f"{name}_Roof",
        "trey_roof",
        (xmin + xmax) * 0.5,
        (zmin + zmax) * 0.5,
        xmax - xmin,
        zmax - zmin,
        height,
        0.18,
        templates,
        specs,
        collection,
        root,
        role="finished_cc0_closed_urban_block_roof",
        material=roof_material,
    )
    for index, (start, end) in enumerate(sides):
        make_module_run(
            f"{name}_Cornice{index:02d}",
            start,
            end,
            height - 0.06,
            0.32,
            ("trey_roof_trim",),
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_urban_block_cornice",
            material=roof_material,
        )
    # A second, narrower eave/parapet band breaks the former one-piece slab
    # silhouette without duplicating an entire building kit.  Opposing sides
    # keep roof drainage/readability and give the overview a consistent scale.
    for index in (0, 2):
        start, end = sides[index]
        make_module_run(
            f"{name}_Parapet{index:02d}",
            start,
            end,
            height + 0.18,
            0.34,
            ("trey_roof_trim",),
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_urban_block_roof_parapet",
            material=roof_material,
            depth_scale=0.72,
        )

    # Attached roof-trim ridges subdivide broad roof planes at overview scale.
    # They sit directly on the authored roof skin and follow the long axis, so
    # they read as drainage/vent spines rather than floating decorative bars.
    span_x, span_z = xmax - xmin, zmax - zmin
    ridge_offsets = (0.0,)
    if max(span_x, span_z) > 18.0:
        ridge_offsets = (-0.16, 0.16)
    for ridge_index, offset_ratio in enumerate(ridge_offsets):
        if span_x >= span_z:
            ridge_z = (zmin + zmax) * 0.5 + span_z * offset_ratio
            ridge_start = (xmin + 0.8, ridge_z)
            ridge_end = (xmax - 0.8, ridge_z)
        else:
            ridge_x = (xmin + xmax) * 0.5 + span_x * offset_ratio
            ridge_start = (ridge_x, zmin + 0.8)
            ridge_end = (ridge_x, zmax - 0.8)
        make_module_run(
            f"{name}_SkylineRidge{ridge_index:02d}",
            ridge_start,
            ridge_end,
            height + 0.20,
            0.24,
            ("trey_roof_trim",),
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_attached_skyline_ridge",
            material=roof_material,
            nominal_cell=3.0,
            depth_scale=0.76,
        )

    # Roughly one building in three receives a low rooftop lantern/vent house.
    # The deterministic name signature creates skyline variety while retaining
    # finished Trey walls, roof, UVs, and the frozen gameplay footprint below.
    signature = sum(ord(character) for character in name)
    if signature % 3 == 0:
        cap_x = min(4.2, max(2.3, span_x * 0.24))
        cap_z = min(4.2, max(2.3, span_z * 0.24))
        offset_x = (0.12 if signature % 2 else -0.12) * max(0.0, span_x - cap_x)
        offset_z = (-0.10 if signature % 5 else 0.10) * max(0.0, span_z - cap_z)
        center_x = (xmin + xmax) * 0.5 + offset_x
        center_z = (zmin + zmax) * 0.5 + offset_z
        cap_bounds = (
            center_x - cap_x * 0.5,
            center_x + cap_x * 0.5,
            center_z - cap_z * 0.5,
            center_z + cap_z * 0.5,
        )
        cap_sides = (
            ((cap_bounds[0], cap_bounds[2]), (cap_bounds[1], cap_bounds[2])),
            ((cap_bounds[1], cap_bounds[2]), (cap_bounds[1], cap_bounds[3])),
            ((cap_bounds[1], cap_bounds[3]), (cap_bounds[0], cap_bounds[3])),
            ((cap_bounds[0], cap_bounds[3]), (cap_bounds[0], cap_bounds[2])),
        )
        for index, (start, end) in enumerate(cap_sides):
            make_module_run(
                f"{name}_RoofLanternWall{index:02d}",
                start,
                end,
                height + 0.20,
                0.72,
                ("trey_foundation",),
                templates,
                specs,
                collection,
                root,
                role="finished_cc0_rooftop_lantern_wall",
                material=wall_material,
                nominal_cell=2.2,
                depth_scale=0.62,
            )
        make_tiled_patch(
            f"{name}_RoofLanternCap",
            "trey_roof",
            center_x,
            center_z,
            cap_x,
            cap_z,
            height + 0.92,
            0.14,
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_rooftop_lantern_roof",
            material=roof_material,
        )


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


def build_rect_perimeter(
    name: str,
    bounds: tuple[float, float, float, float],
    doors: dict[str, tuple[tuple[float, float], ...]],
    height: float,
    lower_pattern: tuple[str, ...],
    upper_pattern: tuple[str, ...],
    templates: dict[str, bpy.types.Mesh],
    specs: dict[str, SourceSpec],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    *,
    region: str,
) -> None:
    xmin, xmax, zmin, zmax = bounds
    side_specs = (
        ("South", (xmin, zmax), (xmax, zmax), "south", xmin, 0.0),
        ("North", (xmin, zmin), (xmax, zmin), "north", xmin, 0.0),
        ("West", (xmin, zmin), (xmin, zmax), "west", zmin, 90.0),
        ("East", (xmax, zmin), (xmax, zmax), "east", zmin, 90.0),
    )
    for side_name, start, end, key, axis_minimum, portal_yaw in side_specs:
        door_specs = doors.get(key, ())
        openings = tuple(
            (
                center - axis_minimum - width * 0.5,
                center - axis_minimum + width * 0.5,
            )
            for center, width in door_specs
        )
        make_segmented_wall(
            f"{name}_{side_name}_Lower",
            start,
            end,
            openings,
            0.0,
            3.0,
            lower_pattern,
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_enterable_building_lower_wall",
        )
        make_module_run(
            f"{name}_{side_name}_Upper",
            start,
            end,
            3.0,
            height - 3.0,
            upper_pattern,
            templates,
            specs,
            collection,
            root,
            role="finished_cc0_enterable_building_upper_wall",
        )
        for door_index, (center, width) in enumerate(door_specs):
            position = (
                (center, 0.0, start[1])
                if key in ("south", "north")
                else (start[0], 0.0, center)
            )
            make_portal(
                f"{name}_{side_name}_Portal{door_index:02d}",
                position,
                portal_yaw,
                width,
                templates,
                specs,
                collection,
                root,
                region=region,
            )


def build_map_v2(
    templates: dict[str, bpy.types.Mesh],
    materials: dict[str, bpy.types.Material],
) -> tuple[bpy.types.Object, dict[str, bpy.types.Collection]]:
    """Compose Bazaar Crossing V2 as a dense, enterable urban interior map."""
    root = bpy.data.objects.new("BazaarCrossing", None)
    bpy.context.scene.collection.objects.link(root)
    set_asset_metadata(root, origin="project_authored", role="map_root")
    root["map_name"] = "Bazaar Crossing V2"
    root["map_bounds_godot_xz"] = "X[-68,68], Z[-56,56]"
    root["authoring_axis_conversion"] = "Blender=(GodotX,-GodotZ,GodotY)"
    root["scale_meters"] = 1.0
    root["design_revision"] = "V2 dense enterable interiors"

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
    specs = {spec.key: spec for spec in (*SOURCE_SPECS, *COFFEE_CART_SOURCE_SPECS)}
    surface = collections["surface"]
    architecture = collections["architecture"]
    elevation = collections["elevation"]
    dressing = collections["dressing"]
    markers = collections["markers"]
    asphalt = materials["BazaarWetAsphalt"]
    paving = materials["BazaarStonePaving"]
    concrete = materials["BazaarWeatheredConcrete"]
    steel = materials["BazaarBlackenedSteel"]
    warm = materials["BazaarWarmPlaster"]
    canvas = materials["BazaarAwningCanvas"]
    roof_clay = materials["BazaarRoofClay"]
    roof_slate = materials["BazaarRoofSlate"]
    roof_sand = materials["BazaarRoofSandstone"]
    painted_steel = materials["BazaarPaintedSteel"]
    sign_ochre = materials["BazaarSignOchre"]
    sign_teal = materials["BazaarSignTeal"]
    floor_terracotta = materials["BazaarInteriorTerracotta"]
    floor_slate = materials["BazaarInteriorSlate"]
    floor_sand = materials["BazaarInteriorSand"]
    dark_timber = materials["BazaarDarkTimber"]

    def add_wall_storage_rack(
        name: str,
        start: tuple[float, float],
        end: tuple[float, float],
        shelf_material: bpy.types.Material,
        *,
        shelf_levels: tuple[float, ...] = (0.70, 1.42, 2.14),
        post_top: float = 2.72,
    ) -> None:
        """Attach one continuous finished-module storage rhythm to a wall."""
        dx, dz = end[0] - start[0], end[1] - start[1]
        length = sqrt(dx * dx + dz * dz)
        if length < 1.0:
            raise RuntimeError(f"Wall rack is too short: {name}")
        for shelf_index, shelf_bottom in enumerate(shelf_levels):
            create_authored_horizontal_strip(
                f"{name}_Shelf{shelf_index:02d}",
                start,
                end,
                shelf_bottom,
                0.14,
                0.34,
                templates,
                specs,
                shelf_material,
                dressing,
                root,
                role="finished_cc0_continuous_wall_storage_shelf",
            )
        post_count = max(2, int(length / 2.35) + 1)
        post_positions = tuple(
            (
                start[0] + dx * index / (post_count - 1),
                start[1] + dz * index / (post_count - 1),
                0.24,
                post_top,
            )
            for index in range(post_count)
        )
        create_authored_column_set(
            f"{name}_Posts",
            post_positions,
            0.15,
            templates,
            specs,
            painted_steel,
            dressing,
            root,
            role="finished_cc0_continuous_wall_storage_posts",
        )
        make_module_run(
            f"{name}_TopFascia",
            start,
            end,
            post_top,
            0.20,
            ("trey_roof_trim",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_continuous_wall_storage_fascia",
            material=dark_timber,
            nominal_cell=2.5,
            depth_scale=0.62,
        )

    def add_upper_shopfront_band(
        name: str,
        start: tuple[float, float],
        end: tuple[float, float],
        band_material: bpy.types.Material,
        *,
        bottom: float = 3.25,
        height: float = 2.10,
    ) -> None:
        """Overlay a shallow Quaternius window band with Trey framing."""
        module_depth = source_dimensions(templates["quat_window_trim"]).y
        make_module_run(
            f"{name}_WindowBand",
            start,
            end,
            bottom,
            height,
            ("quat_window_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_continuous_upper_shopfront_band",
            material=band_material,
            nominal_cell=2.8,
            depth_scale=0.20 / module_depth,
        )
        dx, dz = end[0] - start[0], end[1] - start[1]
        length = sqrt(dx * dx + dz * dz)
        frame_count = max(2, int(length / 3.0) + 1)
        create_authored_column_set(
            f"{name}_BandPiers",
            tuple(
                (
                    start[0] + dx * index / (frame_count - 1),
                    start[1] + dz * index / (frame_count - 1),
                    bottom - 0.08,
                    bottom + height + 0.18,
                )
                for index in range(frame_count)
            ),
            0.16,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_continuous_upper_shopfront_piers",
        )
        for trim_index, trim_bottom in enumerate((bottom - 0.10, bottom + height)):
            make_module_run(
                f"{name}_BandTrim{trim_index:02d}",
                start,
                end,
                trim_bottom,
                0.20,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_continuous_upper_shopfront_cornice",
                material=dark_timber,
                nominal_cell=2.8,
                depth_scale=0.58,
            )

    def add_rooftop_monitor(
        name: str,
        bounds: tuple[float, float, float, float],
        base_y: float,
        wall_material: bpy.types.Material,
        cap_material: bpy.types.Material,
    ) -> None:
        """Build an attached low clerestory/vent monitor from finished modules."""
        xmin, xmax, zmin, zmax = bounds
        monitor_height = 0.82
        module_depth = source_dimensions(templates["quat_metal_window"]).y
        for side_index, (start, end, key) in enumerate(
            (
                ((xmin, zmin), (xmax, zmin), "quat_metal_window"),
                ((xmax, zmin), (xmax, zmax), "trey_foundation"),
                ((xmax, zmax), (xmin, zmax), "quat_metal_window"),
                ((xmin, zmax), (xmin, zmin), "trey_foundation"),
            )
        ):
            depth = module_depth if key == "quat_metal_window" else source_dimensions(templates[key]).y
            make_module_run(
                f"{name}_MonitorWall{side_index:02d}",
                start,
                end,
                base_y,
                monitor_height,
                (key,),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_skyline_monitor_wall",
                material=wall_material,
                nominal_cell=2.4,
                depth_scale=0.22 / depth,
            )
        make_tiled_patch(
            f"{name}_MonitorCap",
            "quat_floor",
            (xmin + xmax) * 0.5,
            (zmin + zmax) * 0.5,
            xmax - xmin + 0.18,
            zmax - zmin + 0.18,
            base_y + monitor_height,
            0.14,
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_skyline_monitor_cap",
            material=cap_material,
        )
        make_module_run(
            f"{name}_MonitorRidge",
            ((xmin + xmax) * 0.5, zmin + 0.18),
            ((xmin + xmax) * 0.5, zmax - 0.18),
            base_y + monitor_height + 0.12,
            0.22,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_attached_skyline_ridge",
            material=cap_material,
            nominal_cell=2.4,
            depth_scale=0.68,
        )

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
        surface,
        root,
        role="finished_cc0_authored_ground_tiles",
        uv_tile=4.5,
    )

    # Narrow paving bands make the 4.5-6 m tactical lanes readable without
    # visually reopening the whole arena into a parking lot.
    for name, x, z, sx, sz in (
        ("Bazaar_Attacker_Foyer_Paving", 0.0, 48.5, 28.0, 9.0),
        ("Bazaar_A_Approach_Paving", -46.0, 24.0, 7.0, 25.0),
        ("Bazaar_Mid_Approach_Paving", 0.0, 25.0, 7.0, 25.0),
        ("Bazaar_B_Approach_Paving", 46.0, 24.0, 7.0, 25.0),
        ("Bazaar_Defender_Spawn_Paving", 0.0, -49.0, 15.0, 9.0),
    ):
        create_authored_surface_patch(
            name,
            x,
            z,
            sx,
            sz,
            0.025,
            templates,
            specs,
            paving,
            surface,
            root,
            role="finished_cc0_authored_confined_route_tiles",
            uv_tile=2.5,
        )

    # The large gameplay blockers are expressed as coherent modular city
    # blocks with roofs, returns, cornices, and varied elevations. They are not
    # scaled copies of a single closed building.
    closed_blocks = (
        ("AttackWestInn", (-58.0, -43.0, 42.0, 55.0), 6.2, ("quat_window_trim", "trey_foundation"), warm),
        ("AttackWestBaths", (-43.0, -29.0, 42.0, 55.0), 7.4, ("quat_curved_window",), concrete),
        ("AttackWestShops", (-29.0, -15.0, 42.0, 55.0), 6.7, ("quat_window_trim", "trey_foundation"), warm),
        ("AttackEastHotel", (15.0, 31.0, 42.0, 55.0), 7.0, ("quat_metal_window", "trey_foundation"), steel),
        ("AttackEastGuild", (31.0, 45.0, 42.0, 55.0), 6.1, ("quat_window_trim",), warm),
        ("AttackEastFoundry", (45.0, 58.0, 42.0, 55.0), 7.6, ("quat_metal_window", "trey_window"), steel),
        ("AttackWestEntryWing", (-15.0, -9.0, 41.5, 55.0), 8.0, ("quat_curved_window", "trey_foundation"), concrete),
        ("AttackEastEntryWing", (9.0, 15.0, 41.5, 55.0), 8.0, ("quat_metal_window", "trey_foundation"), steel),
        ("WestLaneLink", (-49.0, -42.0, 36.5, 42.0), 8.4, ("quat_window_trim", "trey_foundation"), warm),
        ("EastLaneLink", (42.0, 49.0, 36.5, 42.0), 8.4, ("quat_metal_window", "trey_foundation"), steel),
        ("SouthWestCaravan", (-58.0, -37.0, 12.0, 36.5), 6.8, ("quat_window_trim", "trey_foundation"), warm),
        ("SouthWestArchive", (-37.0, -16.0, 12.0, 36.5), 8.0, ("quat_curved_window",), concrete),
        ("SouthEastTextile", (16.0, 38.0, 12.0, 36.5), 7.2, ("quat_window_trim", "trey_foundation"), warm),
        ("SouthEastMetalworks", (38.0, 58.0, 12.0, 36.5), 8.2, ("quat_metal_window", "trey_window"), steel),
        ("SeparationWestNorth", (-29.0, -9.5, -31.0, -21.0), 7.4, ("quat_window_trim", "trey_foundation"), warm),
        ("SeparationWestSouth", (-29.0, -9.5, -15.0, 6.0), 6.4, ("quat_curved_window",), concrete),
        ("SeparationEastNorth", (9.5, 29.0, -31.0, -18.0), 7.8, ("quat_metal_window", "trey_foundation"), steel),
        ("SeparationEastSouth", (9.5, 29.0, -12.0, 6.0), 6.6, ("quat_window_trim",), warm),
        ("WestServiceClosure", (-64.0, -60.0, -4.0, 12.0), 8.0, ("quat_window_trim", "trey_foundation"), warm),
        ("EastServiceClosure", (60.0, 64.0, -4.0, 12.0), 8.0, ("quat_metal_window", "trey_foundation"), steel),
    )
    roof_cycle = (roof_clay, roof_slate, roof_sand, roof_slate)
    for block_index, (name, bounds, height, pattern, wall_material) in enumerate(closed_blocks):
        make_closed_block(
            f"BazaarBlock_{name}",
            bounds,
            height,
            pattern,
            templates,
            specs,
            roof_cycle[block_index % len(roof_cycle)],
            wall_material,
            architecture,
            root,
        )

    # Side boundary rows remain a continuous urban edge, but use five distinct
    # rooflines per side rather than dozens of duplicated whole-building props.
    for side_name, xmin, xmax, pattern in (
        ("West", -67.0, -64.0, ("quat_window_trim", "trey_foundation")),
        ("East", 64.0, 67.0, ("quat_metal_window", "trey_foundation")),
    ):
        for index, (zmin, zmax, height) in enumerate(
            ((-56.0, -34.0, 7.0), (-34.0, -12.0, 8.3), (-12.0, 10.0, 6.5), (10.0, 34.0, 7.6), (34.0, 56.0, 6.9))
        ):
            make_closed_block(
                f"BazaarBoundary_{side_name}_{index:02d}",
                (xmin, xmax, zmin, zmax),
                height,
                pattern,
                templates,
                specs,
                (roof_clay, roof_slate, roof_sand)[(index + (0 if side_name == "West" else 1)) % 3],
                warm if side_name == "West" else concrete,
                architecture,
                root,
            )

    # A: a two-storey caravanserai. The bomb site is a small open courtyard;
    # all four surrounding wings are enterable and roofed.
    build_rect_perimeter(
        "Bazaar_A_Caravanserai",
        (-60.0, -34.0, -31.0, -4.0),
        {
            "south": ((-56.0, 3.2), (-47.0, 3.4)),
            "west": ((-12.0, 3.2),),
            "east": ((-10.0, 3.2),),
            "north": ((-52.0, 3.2), (-37.0, 3.2)),
        },
        6.4,
        ("trey_foundation", "quat_door_frame"),
        ("quat_window_trim", "trey_foundation"),
        templates,
        specs,
        architecture,
        root,
        region="A_Caravanserai",
    )
    make_tiled_patch(
        "Bazaar_A_InteriorFloor",
        "quat_floor",
        -47.0,
        -17.5,
        25.2,
        26.2,
        0.035,
        0.12,
        templates,
        specs,
        surface,
        root,
        role="finished_cc0_enterable_A_floor",
        material=floor_terracotta,
    )
    for roof_name, x, z, sx, sz, roof_material in (
        ("WestArcade", -55.5, -17.5, 9.0, 27.0, roof_clay),
        ("EastRooms", -37.5, -17.5, 7.0, 27.0, roof_slate),
        ("RearWarehouse", -46.0, -27.0, 10.0, 8.0, roof_sand),
        ("SouthVestibule", -46.0, -8.5, 10.0, 9.0, roof_clay),
    ):
        make_tiled_patch(
            f"Bazaar_A_Roof_{roof_name}",
            "quat_floor",
            x,
            z,
            sx,
            sz,
            6.3,
            0.14,
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_enterable_A_roof",
            material=roof_material,
        )
        roof_edges = (
            ((x - sx * 0.5, z - sz * 0.5), (x + sx * 0.5, z - sz * 0.5)),
            ((x + sx * 0.5, z - sz * 0.5), (x + sx * 0.5, z + sz * 0.5)),
            ((x + sx * 0.5, z + sz * 0.5), (x - sx * 0.5, z + sz * 0.5)),
            ((x - sx * 0.5, z + sz * 0.5), (x - sx * 0.5, z - sz * 0.5)),
        )
        for edge_index, (edge_start, edge_end) in enumerate(roof_edges):
            make_module_run(
                f"Bazaar_A_Roof_{roof_name}_Parapet{edge_index:02d}",
                edge_start,
                edge_end,
                6.48,
                0.32,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_enterable_A_roof_parapet",
                material=roof_material,
                depth_scale=0.70,
            )
    add_rooftop_monitor(
        "Bazaar_A_WestArcade",
        (-57.1, -53.9, -23.0, -12.0),
        6.54,
        floor_slate,
        roof_clay,
    )
    add_rooftop_monitor(
        "Bazaar_A_EastRooms",
        (-39.4, -35.8, -22.5, -13.0),
        6.54,
        floor_sand,
        roof_slate,
    )
    # Courtyard arcades and warehouse rooms create local cover without crates.
    make_module_run(
        "Bazaar_A_Courtyard_West_Arcade",
        (-51.0, -23.0),
        (-51.0, -13.0),
        0.0,
        3.0,
        ("trey_arch",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_A_courtyard_arcade",
        material=roof_sand,
        nominal_cell=4.0,
    )
    make_module_run(
        "Bazaar_A_Courtyard_East_Arcade",
        (-41.0, -13.0),
        (-41.0, -23.0),
        0.0,
        3.0,
        ("trey_arch",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_A_courtyard_arcade",
        material=roof_sand,
        nominal_cell=4.0,
    )
    make_segmented_wall(
        "Bazaar_A_RearWarehouse_Partition",
        (-60.0, -23.0),
        (-34.0, -23.0),
        ((2.4, 5.6), (12.4, 15.6), (20.4, 23.6)),
        0.0,
        3.0,
        ("trey_foundation",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_A_warehouse_partition",
        material=warm,
    )
    make_portal(
        "Bazaar_A_RearWarehouse_CenterPortal",
        (-46.0, 0.0, -23.0),
        0.0,
        3.2,
        templates,
        specs,
        architecture,
        root,
        region="A_Caravanserai",
    )
    make_segmented_wall(
        "Bazaar_A_Warehouse_Bays",
        (-47.0, -31.0),
        (-47.0, -23.0),
        ((2.4, 5.6),),
        0.0,
        3.0,
        ("trey_foundation",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_A_warehouse_bay_wall",
        material=warm,
    )
    for counter_name, start, end in (
        ("A_SpiceCounter", (-49.8, -15.0), (-49.8, -20.8)),
        ("A_WarehouseDesk", (-35.5, -29.5), (-35.5, -24.5)),
        ("A_EntryDesk", (-48.7, -9.5), (-44.0, -9.5)),
    ):
        make_module_run(
            f"Bazaar_{counter_name}",
            start,
            end,
            0.0,
            1.18,
            ("trey_foundation",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_architectural_counter_cover",
            material=concrete,
        )

    # The warehouse partition is gameplay-critical, so all three runtime bays
    # receive a visible arch frame, upper signboard, pilasters, and cornice.
    for portal_x in (-56.0, -38.0):
        make_portal(
            f"Bazaar_A_RearWarehouse_Portal_{portal_x:+05.1f}",
            (portal_x, 0.0, -23.0),
            0.0,
            3.2,
            templates,
            specs,
            architecture,
            root,
            region="A_Caravanserai",
        )
    for sign_index, (sign_x, sign_material) in enumerate(
        ((-56.0, sign_ochre), (-46.0, sign_teal), (-38.0, roof_clay))
    ):
        make_module_run(
            f"Bazaar_A_RearWarehouse_Sign{sign_index:02d}",
            (sign_x - 1.8, -22.76),
            (sign_x + 1.8, -22.76),
            3.28,
            0.70,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=1.8,
            depth_scale=0.26,
        )
    create_authored_column_set(
        "Bazaar_A_RearWarehouse_Pilasters",
        tuple((x, -22.78, 0.0, 3.14) for x in (-59.0, -52.0, -49.5, -42.5, -40.0, -35.0)),
        0.28,
        templates,
        specs,
        roof_sand,
        architecture,
        root,
        role="finished_cc0_shopfront_pilasters",
    )
    make_module_run(
        "Bazaar_A_RearWarehouse_Cornice",
        (-59.5, -22.80),
        (-34.5, -22.80),
        3.02,
        0.28,
        ("trey_roof_trim",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_interior_shopfront_cornice",
        material=painted_steel,
        depth_scale=0.74,
    )
    for band_name, start, end, band_material in (
        ("RearShopWest", (-59.2, -22.70), (-52.1, -22.70), floor_sand),
        ("RearShopEast", (-41.9, -22.70), (-34.8, -22.70), floor_slate),
    ):
        add_upper_shopfront_band(
            f"Bazaar_A_{band_name}",
            start,
            end,
            band_material,
            bottom=3.42,
            height=1.78,
        )
    add_wall_storage_rack(
        "Bazaar_A_RearDisplayWest",
        (-59.0, -22.48),
        (-52.2, -22.48),
        dark_timber,
    )
    add_wall_storage_rack(
        "Bazaar_A_RearDisplayEast",
        (-41.8, -22.48),
        (-35.0, -22.48),
        roof_sand,
    )
    add_wall_storage_rack(
        "Bazaar_A_CourtyardSpiceRack",
        (-50.62, -20.5),
        (-50.62, -14.0),
        dark_timber,
        shelf_levels=(0.78, 1.48, 2.18),
        post_top=2.78,
    )
    for banner_index, (zmin, zmax, banner_material) in enumerate(
        ((-21.2, -18.2, sign_ochre), (-16.8, -13.8, sign_teal))
    ):
        make_module_run(
            f"Bazaar_A_Gallery_HangingBanner{banner_index:02d}",
            (-52.76, zmin),
            (-52.76, zmax),
            2.18,
            0.66,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_gallery_hanging_shop_sign",
            material=banner_material,
            nominal_cell=1.5,
            depth_scale=0.24,
        )
    make_module_run(
        "Bazaar_A_SpiceCounter_Awning",
        (-50.72, -20.5),
        (-50.72, -15.3),
        2.86,
        0.34,
        ("trey_roof_trim",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_wall_attached_market_awning",
        material=roof_clay,
        nominal_cell=2.6,
        depth_scale=0.86,
    )
    for beam_index, beam_z in enumerate((-14.0, -21.5)):
        make_module_run(
            f"Bazaar_A_Courtyard_UpperBeam{beam_index:02d}",
            (-50.7, beam_z),
            (-41.3, beam_z),
            5.24,
            0.28,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_A_visible_courtyard_beam",
            material=painted_steel,
            depth_scale=1.05,
        )

    # B: a fully roofed market warehouse with a readable column grid and
    # clerestory, loading vestibule, main hall, stock room, and east mezzanine.
    build_rect_perimeter(
        "Bazaar_B_MarketWarehouse",
        (34.0, 60.0, -30.0, -6.0),
        {
            "south": ((46.0, 3.4), (56.0, 3.2)),
            "west": ((-14.0, 3.2),),
            "east": ((-12.0, 3.2),),
            "north": ((40.0, 3.2), (55.0, 3.2)),
        },
        6.5,
        ("trey_foundation", "quat_metal_window"),
        ("quat_metal_window", "trey_window"),
        templates,
        specs,
        architecture,
        root,
        region="B_MarketWarehouse",
    )
    make_tiled_patch(
        "Bazaar_B_InteriorFloor",
        "quat_floor",
        47.0,
        -18.0,
        25.2,
        23.2,
        0.035,
        0.12,
        templates,
        specs,
        surface,
        root,
        role="finished_cc0_enterable_B_floor",
        material=floor_slate,
    )
    for site_name, site_x, site_material in (
        ("A", -46.0, materials["BazaarSiteA_Paint"]),
        ("B", 46.0, materials["BazaarSiteB_Paint"]),
    ):
        create_authored_surface_patch(
            f"Bazaar_{site_name}_Site_Pad",
            site_x,
            -18.0,
            8.0,
            8.0,
            0.17,
            templates,
            specs,
            site_material,
            surface,
            root,
            role="finished_cc0_authored_interior_bomb_site_tiles",
            uv_tile=2.0,
        )
    make_tiled_patch(
        "Bazaar_B_WarehouseRoof",
        "trey_roof",
        47.0,
        -18.0,
        26.0,
        24.0,
        6.4,
        0.18,
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_enterable_B_roof",
        material=roof_slate,
    )
    create_authored_column_set(
        "Bazaar_B_Warehouse_ColumnGrid",
        tuple(
            (x, z, 0.0, 6.25)
            for x in (39.0, 45.0, 51.0, 57.0)
            for z in (-25.5, -17.5, -9.5)
        ),
        0.52,
        templates,
        specs,
        roof_sand,
        architecture,
        root,
        role="finished_cc0_B_structural_column_grid",
    )
    make_segmented_wall(
        "Bazaar_B_LoadingBay_Partition",
        (40.0, -28.0),
        (40.0, -6.0),
        ((1.0, 4.4), (12.0, 15.2)),
        0.0,
        3.0,
        ("trey_arch",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_B_loading_arcade",
        material=concrete,
    )
    make_segmented_wall(
        "Bazaar_B_Stockroom_Partition",
        (52.0, -30.0),
        (52.0, -6.0),
        ((1.4, 4.6), (5.0, 8.2), (16.0, 19.2)),
        0.0,
        3.0,
        ("trey_foundation",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_B_stockroom_partition",
        material=concrete,
    )
    for counter_name, start, end in (
        ("B_FishCounter", (42.0, -14.0), (42.0, -21.0)),
        ("B_TextileCounter", (47.0, -11.0), (51.0, -11.0)),
        ("B_LoadingDesk", (35.8, -19.0), (39.0, -19.0)),
    ):
        make_module_run(
            f"Bazaar_{counter_name}",
            start,
            end,
            0.0,
            1.16,
            ("trey_foundation",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_architectural_counter_cover",
            material=painted_steel,
        )
    for beam_index, z in enumerate((-27.0, -21.0, -15.0, -9.0)):
        make_module_run(
            f"Bazaar_B_RoofBeam_{beam_index:02d}",
            (34.4, z),
            (59.6, z),
            5.75,
            0.38,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_B_visible_roof_beam",
            material=painted_steel,
        )
    for truss_index, z in enumerate((-24.0, -18.0, -12.0)):
        make_module_run(
            f"Bazaar_B_LowerTruss_{truss_index:02d}",
            (34.8, z),
            (59.2, z),
            4.48,
            0.26,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_B_visible_loading_truss",
            material=dark_timber,
            nominal_cell=3.0,
            depth_scale=0.92,
        )

    # Make both runtime-aligned partitions explicit at player height.  Arched
    # frames mark every loading/stockroom opening; signs and wall shelves occupy
    # the high wall band instead of consuming the clean navigation floor.
    for partition_name, portal_x, portal_centers in (
        ("Loading", 40.0, (-25.3, -14.4)),
        ("Stockroom", 52.0, (-27.0, -23.4, -12.4)),
    ):
        for portal_index, portal_z in enumerate(portal_centers):
            make_portal(
                f"Bazaar_B_{partition_name}_Portal{portal_index:02d}",
                (portal_x, 0.0, portal_z),
                90.0,
                3.2,
                templates,
                specs,
                architecture,
                root,
                region="B_MarketWarehouse",
            )
            sign_material = sign_teal if (portal_index + (0 if partition_name == "Loading" else 1)) % 2 == 0 else sign_ochre
            sign_x = portal_x + (0.24 if partition_name == "Loading" else -0.24)
            make_module_run(
                f"Bazaar_B_{partition_name}_Sign{portal_index:02d}",
                (sign_x, portal_z - 1.65),
                (sign_x, portal_z + 1.65),
                3.22,
                0.68,
                ("trey_foundation",),
                templates,
                specs,
                dressing,
                root,
                role="finished_cc0_wall_mounted_shop_sign",
                material=sign_material,
                nominal_cell=1.65,
                depth_scale=0.26,
            )

    for shelf_name, start, end in (
        ("NorthMain", (42.0, -29.54), (50.0, -29.54)),
        ("Loading", (40.24, -21.8), (40.24, -17.2)),
        ("Stockroom", (51.76, -20.6), (51.76, -16.0)),
        ("EastWall", (59.55, -20.5), (59.55, -15.0)),
    ):
        for shelf_index, shelf_bottom in enumerate((1.18, 2.02)):
            create_authored_horizontal_strip(
                f"Bazaar_B_{shelf_name}_HighShelf{shelf_index:02d}",
                start,
                end,
                shelf_bottom,
                0.17,
                0.38,
                templates,
                specs,
                roof_sand,
                dressing,
                root,
                role="finished_cc0_high_wall_market_shelf",
            )

    for rack_name, start, end, rack_material in (
        ("LoadingRack", (40.30, -21.6), (40.30, -16.4), dark_timber),
        ("StockroomRack", (51.70, -21.4), (51.70, -16.0), roof_sand),
        ("EastWallRack", (59.48, -26.0), (59.48, -18.2), dark_timber),
        ("NorthWallRack", (42.0, -29.48), (49.3, -29.48), roof_sand),
    ):
        add_wall_storage_rack(
            f"Bazaar_B_{rack_name}",
            start,
            end,
            rack_material,
            shelf_levels=(0.66, 1.36, 2.06),
            post_top=2.70,
        )

    for band_name, start, end, band_material in (
        ("NorthClerestoryWest", (34.8, -29.68), (39.0, -29.68), floor_sand),
        ("NorthClerestoryCenter", (41.8, -29.68), (50.2, -29.68), floor_slate),
        ("NorthClerestoryEast", (56.8, -29.68), (59.2, -29.68), floor_sand),
    ):
        add_upper_shopfront_band(
            f"Bazaar_B_{band_name}",
            start,
            end,
            band_material,
            bottom=3.34,
            height=1.86,
        )

    for awning_name, start, end, awning_material in (
        ("FishCounter", (40.24, -20.5), (40.24, -14.5), roof_slate),
        ("TextileCounter", (47.0, -11.24), (51.0, -11.24), roof_clay),
    ):
        make_module_run(
            f"Bazaar_B_{awning_name}_Awning",
            start,
            end,
            2.86,
            0.34,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_wall_attached_market_awning",
            material=awning_material,
            nominal_cell=2.0,
            depth_scale=0.86,
        )

    for sign_index, (start, end, sign_material, sign_bottom) in enumerate(
        (
            ((42.5, -14.0), (46.5, -14.0), sign_teal, 3.58),
            ((45.0, -24.0), (49.0, -24.0), sign_ochre, 3.72),
        )
    ):
        make_module_run(
            f"Bazaar_B_HangingAisleSign{sign_index:02d}",
            start,
            end,
            sign_bottom,
            0.66,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_hanging_market_sign",
            material=sign_material,
            nominal_cell=2.0,
            depth_scale=0.24,
        )
        create_authored_column_set(
            f"Bazaar_B_HangingAisleSign{sign_index:02d}_Rods",
            (
                (start[0], start[1], sign_bottom + 0.66, 5.52),
                (end[0], end[1], sign_bottom + 0.66, 5.52),
            ),
            0.13,
            templates,
            specs,
            painted_steel,
            dressing,
            root,
            role="finished_cc0_hanging_market_sign_rods",
        )

    b_roof_edges = (
        ((34.2, -29.8), (59.8, -29.8)),
        ((59.8, -29.8), (59.8, -6.2)),
        ((59.8, -6.2), (34.2, -6.2)),
        ((34.2, -6.2), (34.2, -29.8)),
    )
    for edge_index, (start, end) in enumerate(b_roof_edges):
        make_module_run(
            f"Bazaar_B_WarehouseRoof_Parapet{edge_index:02d}",
            start,
            end,
            6.58,
            0.36,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_enterable_B_roof_parapet",
            material=roof_slate,
            depth_scale=0.74,
        )
    for ridge_index, (ridge_x, ridge_material) in enumerate(((43.0, roof_sand), (51.0, roof_clay))):
        make_tiled_patch(
            f"Bazaar_B_WarehouseRoof_RaisedRidge{ridge_index:02d}",
            "trey_canopy",
            ridge_x,
            -18.0,
            3.2,
            17.5,
            6.72,
            0.28,
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_enterable_B_roof_clerestory_ridge",
            material=ridge_material,
        )
        ridge_edges = (
            ((ridge_x - 1.6, -26.75), (ridge_x + 1.6, -26.75)),
            ((ridge_x + 1.6, -26.75), (ridge_x + 1.6, -9.25)),
            ((ridge_x + 1.6, -9.25), (ridge_x - 1.6, -9.25)),
            ((ridge_x - 1.6, -9.25), (ridge_x - 1.6, -26.75)),
        )
        for edge_index, (start, end) in enumerate(ridge_edges):
            make_module_run(
                f"Bazaar_B_WarehouseRoof_RaisedRidge{ridge_index:02d}_Support{edge_index:02d}",
                start,
                end,
                6.44,
                0.30,
                ("trey_foundation",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_enterable_B_roof_clerestory_support",
                material=ridge_material,
                nominal_cell=2.5,
                depth_scale=0.54,
            )
        ridge_window_depth = source_dimensions(templates["quat_metal_window"]).y
        for window_index, window_x in enumerate((ridge_x - 1.58, ridge_x + 1.58)):
            make_module_run(
                f"Bazaar_B_WarehouseRoof_RaisedRidge{ridge_index:02d}_Clerestory{window_index:02d}",
                (window_x, -26.5),
                (window_x, -9.5),
                6.54,
                0.44,
                ("quat_metal_window",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_B_roof_clerestory_window_band",
                material=floor_slate if ridge_index == 0 else floor_terracotta,
                nominal_cell=2.8,
                depth_scale=0.20 / ridge_window_depth,
            )

    # Winning Mid opens an offset indoor junction to A and B. West/east doors
    # sit four metres apart in Z and two half-width partitions force another
    # S-turn, so the junction never exposes a direct site-to-site sightline.
    build_rect_perimeter(
        "Bazaar_Mid_NorthConnector",
        (-9.0, 9.0, -24.0, -7.0),
        {
            "west": ((-18.0, 3.2),),
            "east": ((-14.0, 3.2),),
            "south": ((-5.0, 3.2),),
            "north": ((4.0, 3.2),),
        },
        6.2,
        ("trey_foundation",),
        ("quat_curved_window", "trey_foundation"),
        templates,
        specs,
        architecture,
        root,
        region="Mid_NorthConnector",
    )
    make_tiled_patch(
        "Bazaar_Mid_NorthConnector_Floor",
        "quat_floor",
        0.0,
        -15.5,
        17.2,
        16.2,
        0.035,
        0.12,
        templates,
        specs,
        surface,
        root,
        role="finished_cc0_enterable_Mid_floor",
        material=floor_sand,
    )
    make_tiled_patch(
        "Bazaar_Mid_NorthConnector_Roof",
        "quat_floor",
        0.0,
        -15.5,
        18.0,
        17.0,
        6.1,
        0.14,
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_enterable_Mid_roof",
        material=roof_slate,
    )
    for edge_index, (start, end) in enumerate(
        (
            ((-8.8, -23.8), (8.8, -23.8)),
            ((8.8, -23.8), (8.8, -7.2)),
            ((8.8, -7.2), (-8.8, -7.2)),
            ((-8.8, -7.2), (-8.8, -23.8)),
        )
    ):
        make_module_run(
            f"Bazaar_Mid_NorthConnector_RoofParapet{edge_index:02d}",
            start,
            end,
            6.24,
            0.34,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_enterable_Mid_roof_parapet",
            material=roof_slate,
            depth_scale=0.72,
        )
    make_module_run(
        "Bazaar_Mid_NorthConnector_WestBaffle",
        (-8.8, -16.7),
        (1.5, -16.7),
        0.0,
        3.0,
        ("trey_foundation", "quat_door_frame"),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_Mid_offset_partition_wall",
    )
    make_module_run(
        "Bazaar_Mid_NorthConnector_EastBaffle",
        (-1.5, -12.7),
        (8.8, -12.7),
        0.0,
        3.0,
        ("trey_foundation", "quat_door_frame"),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_Mid_offset_partition_wall",
    )
    add_upper_shopfront_band(
        "Bazaar_Mid_ConnectorWestBaffle",
        (-8.2, -16.48),
        (0.9, -16.48),
        floor_terracotta,
        bottom=3.20,
        height=1.82,
    )
    add_upper_shopfront_band(
        "Bazaar_Mid_ConnectorEastBaffle",
        (-0.9, -12.48),
        (8.2, -12.48),
        floor_slate,
        bottom=3.20,
        height=1.82,
    )
    add_wall_storage_rack(
        "Bazaar_Mid_ConnectorWestRack",
        (-7.8, -16.28),
        (-2.0, -16.28),
        dark_timber,
        shelf_levels=(0.68, 1.36, 2.04),
    )
    add_wall_storage_rack(
        "Bazaar_Mid_ConnectorEastRack",
        (2.0, -12.48),
        (7.8, -12.48),
        roof_sand,
        shelf_levels=(0.68, 1.36, 2.04),
    )
    for beam_index, beam_z in enumerate((-21.3, -17.8, -10.0)):
        make_module_run(
            f"Bazaar_Mid_ConnectorCrossBeam{beam_index:02d}",
            (-8.4, beam_z),
            (8.4, beam_z),
            4.48,
            0.26,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_mid_shop_ceiling_beam",
            material=dark_timber,
            nominal_cell=2.8,
            depth_scale=0.92,
        )
    add_rooftop_monitor(
        "Bazaar_Mid_NorthConnector",
        (-4.2, 4.2, -21.4, -17.4),
        6.34,
        floor_slate,
        roof_slate,
    )

    # Mid is an indoor S-connector. Its three overlapping halls alternate
    # laterally, so neither site nor either spawn can see through it.
    mid_halls = (
        ("NorthTeaHall", (-9.0, 3.0, -8.0, 6.0), ((-1.0, 3.2),), ((-5.0, 3.2),)),
        ("CenterProduceHall", (-3.0, 9.0, 5.0, 20.0), ((1.0, 3.2),), ((0.0, 3.2),)),
        ("SouthCarpetHall", (-9.0, 3.0, 19.0, 34.0), ((-6.0, 3.2), (0.0, 3.2)), ((0.0, 3.2),)),
    )
    mid_roof_materials = {
        "NorthTeaHall": roof_sand,
        "CenterProduceHall": roof_clay,
        "SouthCarpetHall": roof_slate,
    }
    mid_floor_materials = {
        "NorthTeaHall": floor_sand,
        "CenterProduceHall": floor_terracotta,
        "SouthCarpetHall": floor_slate,
    }
    for hall_name, bounds, south_doors, north_doors in mid_halls:
        build_rect_perimeter(
            f"Bazaar_Mid_{hall_name}",
            bounds,
            {"south": south_doors, "north": north_doors},
            6.2,
            ("trey_foundation",),
            ("quat_curved_window", "trey_foundation"),
            templates,
            specs,
            architecture,
            root,
            region=f"Mid_{hall_name}",
        )
        xmin, xmax, zmin, zmax = bounds
        make_tiled_patch(
            f"Bazaar_Mid_{hall_name}_Floor",
            "quat_floor",
            (xmin + xmax) * 0.5,
            (zmin + zmax) * 0.5,
            xmax - xmin - 0.4,
            zmax - zmin - 0.4,
            0.035,
            0.12,
            templates,
            specs,
            surface,
            root,
            role="finished_cc0_enterable_Mid_floor",
            material=mid_floor_materials[hall_name],
        )
        make_tiled_patch(
            f"Bazaar_Mid_{hall_name}_Roof",
            "quat_floor",
            (xmin + xmax) * 0.5,
            (zmin + zmax) * 0.5,
            xmax - xmin,
            zmax - zmin,
            6.1,
            0.14,
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_enterable_Mid_roof",
            material=mid_roof_materials[hall_name],
        )
        roof_edges = (
            ((xmin + 0.2, zmin + 0.2), (xmax - 0.2, zmin + 0.2)),
            ((xmax - 0.2, zmin + 0.2), (xmax - 0.2, zmax - 0.2)),
            ((xmax - 0.2, zmax - 0.2), (xmin + 0.2, zmax - 0.2)),
            ((xmin + 0.2, zmax - 0.2), (xmin + 0.2, zmin + 0.2)),
        )
        for edge_index, (start, end) in enumerate(roof_edges):
            make_module_run(
                f"Bazaar_Mid_{hall_name}_RoofParapet{edge_index:02d}",
                start,
                end,
                6.24,
                0.34,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_enterable_Mid_roof_parapet",
                material=mid_roof_materials[hall_name],
                depth_scale=0.72,
            )
        for side_index, (wall_x, band_material, rack_material) in enumerate(
            (
                (xmin + 0.34, floor_sand, dark_timber),
                (xmax - 0.34, floor_slate, roof_sand),
            )
        ):
            side_start = (wall_x, zmin + 1.05)
            side_end = (wall_x, zmax - 1.05)
            add_upper_shopfront_band(
                f"Bazaar_Mid_{hall_name}_Side{side_index:02d}",
                side_start,
                side_end,
                band_material,
                bottom=3.24,
                height=1.78,
            )
            add_wall_storage_rack(
                f"Bazaar_Mid_{hall_name}_SideRack{side_index:02d}",
                (wall_x, zmin + 1.35),
                (wall_x, zmax - 1.35),
                rack_material,
                shelf_levels=(0.66, 1.34, 2.02),
                post_top=2.66,
            )
        for beam_index, beam_z in enumerate(
            (
                zmin + (zmax - zmin) * 0.32,
                zmin + (zmax - zmin) * 0.68,
            )
        ):
            make_module_run(
                f"Bazaar_Mid_{hall_name}_CrossBeam{beam_index:02d}",
                (xmin + 0.35, beam_z),
                (xmax - 0.35, beam_z),
                4.46,
                0.26,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_mid_shop_ceiling_beam",
                material=dark_timber,
                nominal_cell=2.8,
                depth_scale=0.92,
            )
    add_rooftop_monitor(
        "Bazaar_Mid_CenterProduceHall",
        (1.0, 5.0, 9.0, 16.0),
        6.34,
        floor_terracotta,
        roof_clay,
    )
    add_rooftop_monitor(
        "Bazaar_Mid_SouthCarpetHall",
        (-6.0, -2.0, 22.0, 30.0),
        6.34,
        floor_slate,
        roof_slate,
    )
    make_module_run(
        "Bazaar_Mid_ProduceCounter",
        (1.0, 9.0),
        (6.5, 9.0),
        0.0,
        1.15,
        ("trey_foundation",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_architectural_counter_cover",
        material=warm,
    )
    make_module_run(
        "Bazaar_Mid_CarpetDivider",
        (-7.0, 23.0),
        (-7.0, 28.5),
        0.0,
        1.18,
        ("trey_foundation",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_architectural_counter_cover",
        material=warm,
    )

    # Player-eye landmarks turn the Mid route into three distinct shops rather
    # than a sequence of blank white rooms.  Every sign, beam, shelf, and wall
    # base is a DCC assembly of finished Trey modules, kept tight to the walls
    # so the 5.5-7 m clean combat lane remains unobstructed.
    for beam_name, start, end in (
        ("NorthConnector", (-8.4, -20.5), (8.4, -20.5)),
        ("NorthTea", (-8.4, -1.0), (2.4, -1.0)),
        ("CenterProduce", (-2.4, 13.0), (8.4, 13.0)),
        ("SouthCarpet", (-8.4, 26.0), (2.4, 26.0)),
    ):
        make_module_run(
            f"Bazaar_Mid_{beam_name}_CeilingBeam",
            start,
            end,
            4.58,
            0.30,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_mid_shop_ceiling_beam",
            material=painted_steel,
            depth_scale=1.15,
        )

    mid_signs = (
        ("ConnectorSpice", (-7.2, -16.42), (-3.0, -16.42), sign_ochre),
        ("ConnectorWayfinding", (-1.2, -16.42), (1.15, -16.42), roof_clay),
        ("ConnectorTextile", (2.6, -12.42), (6.8, -12.42), sign_teal),
        ("TeaHouse", (-8.56, -4.8), (-8.56, -0.4), sign_ochre),
        ("Produce", (-2.56, 10.0), (-2.56, 14.5), sign_teal),
        ("Carpet", (-8.56, 23.0), (-8.56, 27.4), roof_clay),
    )
    for sign_name, start, end, sign_material in mid_signs:
        make_module_run(
            f"Bazaar_Mid_Sign_{sign_name}",
            start,
            end,
            2.22,
            0.78,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=2.2,
            depth_scale=0.28,
        )
        create_authored_column_set(
            f"Bazaar_Mid_Sign_{sign_name}_SidePosts",
            (
                (start[0], start[1], 2.14, 3.08),
                (end[0], end[1], 2.14, 3.08),
            ),
            0.18,
            templates,
            specs,
            painted_steel,
            dressing,
            root,
            role="finished_cc0_shop_sign_frame_posts",
        )
        make_module_run(
            f"Bazaar_Mid_Sign_{sign_name}_Cornice",
            start,
            end,
            3.02,
            0.18,
            ("trey_roof_trim",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_shop_sign_frame_cornice",
            material=roof_sand,
            nominal_cell=2.2,
            depth_scale=0.46,
        )

    for awning_name, start, end, awning_material in (
        ("TeaHouse", (-8.56, -5.2), (-8.56, 0.0), roof_clay),
        ("Produce", (-2.56, 10.4), (-2.56, 15.6), roof_slate),
        ("Carpet", (2.56, 23.6), (2.56, 29.0), roof_clay),
    ):
        make_module_run(
            f"Bazaar_Mid_{awning_name}_WallAwning",
            start,
            end,
            3.16,
            0.34,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_wall_attached_market_awning",
            material=awning_material,
            nominal_cell=2.6,
            depth_scale=0.82,
        )

    for shelf_name, start, end in (
        ("Connector", (-7.2, -16.24), (-2.2, -16.24)),
        ("TeaHouse", (-8.32, -5.0), (-8.32, 2.0)),
        ("Produce", (-2.32, 9.5), (-2.32, 17.0)),
        ("Carpet", (2.32, 22.5), (2.32, 30.0)),
    ):
        for shelf_index, shelf_bottom in enumerate((0.68, 1.48)):
            create_authored_horizontal_strip(
                f"Bazaar_Mid_{shelf_name}_WallShelf{shelf_index:02d}",
                start,
                end,
                shelf_bottom,
                0.16,
                0.34,
                templates,
                specs,
                roof_sand,
                dressing,
                root,
                role="finished_cc0_wall_mounted_market_shelf",
            )

    for base_name, start, end in (
        ("ConnectorWest", (-8.4, -16.5), (1.2, -16.5)),
        ("ConnectorEast", (-1.2, -12.5), (8.4, -12.5)),
        ("TeaWest", (-8.5, -6.8), (-8.5, 4.8)),
        ("ProduceWest", (-2.5, 7.0), (-2.5, 18.0)),
        ("CarpetWest", (-8.5, 21.0), (-8.5, 32.0)),
    ):
        make_module_run(
            f"Bazaar_Mid_{base_name}_WallBase",
            start,
            end,
            0.10,
            0.34,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_interior_wall_base",
            material=roof_sand,
            depth_scale=0.76,
        )

    # Keep the attack foyer open between the three real building masses.  The
    # previous west/east frontage baffles were detached at both ends, read as
    # freestanding façade slices, and created four low-value blind corners.
    trim_depth = source_dimensions(templates["trey_roof_trim"]).y

    def make_articulated_facade_return(
        prefix: str,
        start: tuple[float, float],
        end: tuple[float, float],
        height: float,
        sign_material: bpy.types.Material,
        wall_role: str,
        sign_face: float = 1.0,
    ) -> None:
        """Build one exact 420 mm runtime return from finished CC0 modules."""
        dx = end[0] - start[0]
        dz = end[1] - start[1]
        span = sqrt(dx * dx + dz * dz)
        if span <= 0.01:
            raise RuntimeError(f"Degenerate articulated facade return: {prefix}")
        tangent_x, tangent_z = dx / span, dz / span
        normal_x, normal_z = -tangent_z * sign_face, tangent_x * sign_face
        center_x = (start[0] + end[0]) * 0.5
        center_z = (start[1] + end[1]) * 0.5
        nominal_cell = max(1.25, span * 0.5)
        tier_height = height * 0.5
        bounds_metadata = (
            f"{start[0]:.3f},{start[1]:.3f},{end[0]:.3f},{end[1]:.3f}"
        )
        for tier_index, key in enumerate(("quat_window_trim", "quat_metal_window")):
            module_depth = source_dimensions(templates[key]).y
            wall_objects = make_module_run(
                f"{prefix}_Tier{tier_index:02d}",
                start,
                end,
                tier_height * tier_index,
                tier_height,
                (key,),
                templates,
                specs,
                architecture,
                root,
                role=wall_role,
                nominal_cell=nominal_cell,
                depth_scale=0.42 / module_depth,
            )
            for wall_object in wall_objects:
                wall_object["runtime_wall_thickness_m"] = 0.42
                wall_object["runtime_wall_bounds_xz"] = bounds_metadata
        for trim_index, trim_bottom in enumerate(
            (0.10, tier_height - 0.14, height - 0.30)
        ):
            make_module_run(
                f"{prefix}_Trim{trim_index:02d}",
                start,
                end,
                trim_bottom,
                0.24,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_articulated_facade_return_cornice",
                material=painted_steel,
                nominal_cell=nominal_cell,
                depth_scale=0.42 / trim_depth,
            )
        create_authored_column_set(
            f"{prefix}_Piers",
            (
                (start[0], start[1], 0.0, height),
                (center_x, center_z, 0.0, height),
                (end[0], end[1], 0.0, height),
            ),
            0.28,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_articulated_facade_return_piers",
        )
        sign_half_span = min(1.15, span * 0.28)
        foundation_depth = source_dimensions(templates["trey_foundation"]).y
        sign_start = (
            center_x - tangent_x * sign_half_span + normal_x * 0.02,
            center_z - tangent_z * sign_half_span + normal_z * 0.02,
        )
        sign_end = (
            center_x + tangent_x * sign_half_span + normal_x * 0.02,
            center_z + tangent_z * sign_half_span + normal_z * 0.02,
        )
        make_module_run(
            f"{prefix}_Sign",
            sign_start,
            sign_end,
            min(2.62, height - 1.10),
            min(0.80, height * 0.12),
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=max(0.70, sign_half_span),
            depth_scale=0.42 / foundation_depth,
        )

    # Approach returns close long side-court sightlines without narrowing the
    # intended route around their north ends.
    make_articulated_facade_return(
        "Bazaar_WestApproachFacadeReturn",
        (-49.0, -4.0),
        (-49.0, 12.0),
        8.0,
        sign_ochre,
        "finished_cc0_full_height_approach_facade_return",
        sign_face=-1.0,
    )

    # The east return now contains a deliberate service door. It turns the
    # former blind pocket into a short L-shaped route to the B stair vestibule
    # while preserving the full-height sightline break on either side.
    east_return_start = (52.0, -6.0)
    east_return_end = (52.0, 12.0)
    east_return_height = 8.0
    east_service_center_z = 6.2
    east_service_width = 3.2
    east_opening = (
        (
            east_service_center_z - east_return_start[1] - east_service_width * 0.5,
            east_service_center_z - east_return_start[1] + east_service_width * 0.5,
        ),
    )
    east_return_bounds = (
        f"{east_return_start[0]:.3f},{east_return_start[1]:.3f},"
        f"{east_return_end[0]:.3f},{east_return_end[1]:.3f}"
    )
    for tier_index, (bottom, height, key, tier_material) in enumerate(
        (
            (0.0, 4.0, "quat_window_trim", warm),
            (4.0, 4.0, "quat_metal_window", floor_slate),
        )
    ):
        module_depth = source_dimensions(templates[key]).y
        east_return_objects = make_segmented_wall(
            f"Bazaar_EastApproachServiceReturn_{'Lower' if tier_index == 0 else 'Upper'}",
            east_return_start,
            east_return_end,
            east_opening,
            bottom,
            height,
            (key,),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_full_height_approach_service_return",
            material=tier_material,
            nominal_cell=2.35,
            depth_scale=0.42 / module_depth,
        )
        for wall_object in east_return_objects:
            wall_object["runtime_wall_thickness_m"] = 0.42
            wall_object["runtime_wall_bounds_xz"] = east_return_bounds

    east_service_edges = (-6.0, 4.6, 7.8, 12.0)
    create_authored_column_set(
        "Bazaar_EastApproachServiceReturn_Piers",
        tuple((52.0, z, 0.0, east_return_height) for z in east_service_edges),
        0.28,
        templates,
        specs,
        roof_sand,
        architecture,
        root,
        role="finished_cc0_approach_service_return_piers",
    )
    trim_depth = source_dimensions(templates["trey_roof_trim"]).y
    for segment_index, (zmin, zmax) in enumerate(((-6.0, 4.6), (7.8, 12.0))):
        for trim_index, trim_bottom in enumerate((0.10, 3.86, 7.70)):
            make_module_run(
                f"Bazaar_EastApproachServiceReturn_Segment{segment_index:02d}Trim{trim_index:02d}",
                (52.0, zmin),
                (52.0, zmax),
                trim_bottom,
                0.22,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_approach_service_return_cornice",
                material=painted_steel,
                nominal_cell=2.4,
                depth_scale=0.42 / trim_depth,
            )
    make_portal(
        "Bazaar_EastApproachServiceReturn_Portal",
        (52.0, 0.0, east_service_center_z),
        90.0,
        east_service_width,
        templates,
        specs,
        architecture,
        root,
        region="B_ServicePassage",
    )

    # A complete shopfront at the north end removes the leftover no-purpose
    # cavity. Shelving and a runtime-aligned half-height counter make the new
    # link read as a usable service stall rather than a collision correction.
    make_articulated_facade_return(
        "Bazaar_EastServicePocketClosure",
        (52.0, 9.4),
        (60.0, 9.4),
        8.0,
        sign_ochre,
        "finished_cc0_full_height_service_shopfront_closure",
        sign_face=-1.0,
    )
    add_upper_shopfront_band(
        "Bazaar_EastServicePocketClosure_InnerShopfront",
        (52.4, 9.16),
        (59.6, 9.16),
        floor_sand,
        bottom=4.16,
        height=1.66,
    )
    make_tiled_patch(
        "Bazaar_B_ServicePassage_Floor",
        "quat_floor",
        56.0,
        4.0,
        7.6,
        10.4,
        0.035,
        0.12,
        templates,
        specs,
        surface,
        root,
        role="finished_cc0_enterable_service_passage_floor",
        material=floor_slate,
    )
    foundation_depth = source_dimensions(templates["trey_foundation"]).y
    make_module_run(
        "Bazaar_B_ServiceCounter",
        (58.5, 1.4),
        (58.5, 6.0),
        0.0,
        1.16,
        ("trey_foundation",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_architectural_counter_cover",
        material=painted_steel,
        nominal_cell=2.3,
        depth_scale=0.62 / foundation_depth,
    )
    add_wall_storage_rack(
        "Bazaar_B_ServiceStallRack",
        (59.48, 1.55),
        (59.48, 5.85),
        dark_timber,
        shelf_levels=(0.70, 1.40, 2.10),
        post_top=2.76,
    )
    make_module_run(
        "Bazaar_B_ServiceStallAwning",
        (59.38, 1.45),
        (59.38, 5.95),
        2.86,
        0.34,
        ("trey_roof_trim",),
        templates,
        specs,
        architecture,
        root,
        role="finished_cc0_wall_attached_market_awning",
        material=roof_slate,
        nominal_cell=2.25,
        depth_scale=0.86,
    )
    make_module_run(
        "Bazaar_B_ServiceStallSign",
        (59.62, 1.70),
        (59.62, 5.70),
        3.38,
        0.72,
        ("trey_foundation",),
        templates,
        specs,
        dressing,
        root,
        role="finished_cc0_wall_mounted_shop_sign",
        material=sign_teal,
        nominal_cell=2.0,
        depth_scale=0.26,
    )

    # The Carpet Hall south return is an authored continuation of its façade,
    # not a freestanding collision plate in the attack foyer.
    make_articulated_facade_return(
        "Bazaar_MidCarpetSouthFacadeReturn",
        (3.0, 34.0),
        (8.0, 34.0),
        6.2,
        sign_ochre,
        "finished_cc0_mid_carpet_south_facade_return",
    )

    # Short articulated returns project from the separation blocks into the
    # staggered connector vestibules.  They preserve both 6 m door gaps while
    # breaking the direct A-to-B rotation sightline with finished shopfronts.
    for side_name, wall_x, zmin, zmax, sign_material in (
        ("West", -20.0, -21.0, -17.8, sign_ochre),
        ("East", 20.0, -14.8, -12.0, sign_teal),
    ):
        span = zmax - zmin
        center_z = (zmin + zmax) * 0.5
        nominal_cell = span * 0.5
        for tier_index, (bottom, height, key) in enumerate(
            ((0.0, 4.0, "quat_window_trim"), (4.0, 4.0, "quat_metal_window"))
        ):
            module_depth = source_dimensions(templates[key]).y
            return_objects = make_module_run(
                f"Bazaar_Mid_{side_name}ConnectorReturn_Tier{tier_index:02d}",
                (wall_x, zmin),
                (wall_x, zmax),
                bottom,
                height,
                (key,),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_full_height_connector_return_shop_wall",
                nominal_cell=nominal_cell,
                depth_scale=0.42 / module_depth,
            )
            for return_object in return_objects:
                return_object["runtime_wall_thickness_m"] = 0.42
                return_object["runtime_wall_bounds_xz"] = (
                    f"{wall_x:.3f},{zmin:.3f},{wall_x:.3f},{zmax:.3f}"
                )
        for trim_index, trim_bottom in enumerate((0.10, 3.86, 7.70)):
            make_module_run(
                f"Bazaar_Mid_{side_name}ConnectorReturn_Trim{trim_index:02d}",
                (wall_x, zmin),
                (wall_x, zmax),
                trim_bottom,
                0.24,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_connector_return_cornice",
                material=painted_steel,
                nominal_cell=nominal_cell,
                depth_scale=0.42 / trim_depth,
            )
        create_authored_column_set(
            f"Bazaar_Mid_{side_name}ConnectorReturn_Piers",
            tuple((wall_x, z, 0.0, 8.0) for z in (zmin, center_z, zmax)),
            0.28,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_connector_return_sign_piers",
        )
        foundation_depth = source_dimensions(templates["trey_foundation"]).y
        sign_half_span = min(0.70, span * 0.30)
        make_module_run(
            f"Bazaar_Mid_{side_name}ConnectorReturn_Sign",
            (wall_x + (0.02 if wall_x < 0.0 else -0.02), center_z - sign_half_span),
            (wall_x + (0.02 if wall_x < 0.0 else -0.02), center_z + sign_half_span),
            2.62,
            0.78,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=sign_half_span,
            depth_scale=0.42 / foundation_depth,
        )

    # Symmetric defender-foyer piers split the rear court into three authored
    # vestibules.  Like the Mid baffles, they are stacked shopfront modules with
    # cornice bands and sign frames, never featureless full-height boards.
    for side_name, wall_x, sign_material in (
        ("West", -20.0, sign_ochre),
        ("East", 20.0, sign_teal),
    ):
        for tier_index, (bottom, height, key) in enumerate(
            ((0.0, 3.8, "quat_window_trim"), (3.8, 3.8, "quat_metal_window"))
        ):
            module_depth = source_dimensions(templates[key]).y
            pier_objects = make_module_run(
                f"Bazaar_Defender_{side_name}FoyerPier_Tier{tier_index:02d}",
                (wall_x, -56.0),
                (wall_x, -46.2),
                bottom,
                height,
                (key,),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_full_height_defender_foyer_shop_wall",
                nominal_cell=2.75,
                depth_scale=0.42 / module_depth,
            )
            for pier_object in pier_objects:
                pier_object["runtime_wall_thickness_m"] = 0.42
                pier_object["runtime_wall_bounds_xz"] = f"{wall_x:.3f},-56.000,{wall_x:.3f},-46.200"
        for trim_index, trim_bottom in enumerate((0.10, 3.66, 7.30)):
            make_module_run(
                f"Bazaar_Defender_{side_name}FoyerPier_Trim{trim_index:02d}",
                (wall_x, -56.0),
                (wall_x, -46.2),
                trim_bottom,
                0.22,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_defender_foyer_pier_cornice",
                material=painted_steel,
                nominal_cell=2.75,
                depth_scale=0.42 / trim_depth,
            )
        create_authored_column_set(
            f"Bazaar_Defender_{side_name}FoyerPier_Frames",
            tuple((wall_x, z, 0.0, 7.6) for z in (-56.0, -51.1, -46.2)),
            0.28,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_defender_foyer_sign_piers",
        )
        make_module_run(
            f"Bazaar_Defender_{side_name}FoyerPier_Sign",
            (wall_x + (0.02 if wall_x < 0.0 else -0.02), -52.30),
            (wall_x + (0.02 if wall_x < 0.0 else -0.02), -49.90),
            2.54,
            0.80,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=1.20,
            depth_scale=0.42 / foundation_depth,
        )

    # Horizontal L returns complete the rear defender vestibules at z=-46.2.
    # Their inner tips stop at x=+-7.5, leaving the central spawn bay intact.
    make_articulated_facade_return(
        "Bazaar_Defender_WestFoyerReturn",
        (-20.0, -46.2),
        (-7.5, -46.2),
        7.6,
        sign_ochre,
        "finished_cc0_full_height_defender_foyer_return",
    )
    make_articulated_facade_return(
        "Bazaar_Defender_EastFoyerReturn",
        (7.5, -46.2),
        (20.0, -46.2),
        7.6,
        sign_teal,
        "finished_cc0_full_height_defender_foyer_return",
    )

    # Elevated combat spaces are embedded inside their parent buildings.
    platform_specs = (
        ("Bazaar_A_Gallery_Deck", "A_Gallery", -56.0, -18.0, 6.0, 18.0, 3.6),
        ("Bazaar_B_Balcony_Deck", "B_Balcony", 56.0, -18.0, 6.0, 18.0, 3.4),
        ("Bazaar_Mid_Mezzanine_Deck", "Mid_Mezzanine", -6.0, 24.0, 6.0, 14.0, 3.2),
    )
    for deck_name, platform, x, z, sx, sz, top in platform_specs:
        deck = make_tiled_patch(
            deck_name,
            "quat_floor",
            x,
            z,
            sx,
            sz,
            top - 0.12,
            0.12,
            templates,
            specs,
            elevation,
            root,
            role="finished_cc0_authored_interior_deck",
        )
        deck["platform"] = platform
        deck["top_height_m"] = top

    for name, start, end, bottom, top in RUNTIME_RAIL_SPECS:
        rail = create_authored_open_guardrail(
            name,
            start,
            end,
            bottom,
            top,
            templates,
            specs,
            painted_steel,
            elevation,
            root,
        )
        rail["interior_sightline_only"] = True

    # Upper-only privacy screens close the diagnostic high sightlines while
    # retaining both ground-floor routes.  Finished Quaternius shop windows
    # form the lattice field; Trey trims and posts make the 420 mm assembly
    # read as an authored gallery/shopfront screen rather than a blank blocker.
    window_depth = source_dimensions(templates["quat_metal_window"]).y
    trim_depth = source_dimensions(templates["trey_roof_trim"]).y
    for screen_name, screen_x, zmin, zmax, bottom, top, frame_material in (
        ("A_Gallery", -51.0, -26.0, -8.8, 3.6, 6.4, sign_ochre),
        ("B_Balcony", 53.0, -24.5, -9.0, 3.4, 6.5, sign_teal),
    ):
        screen_objects = make_module_run(
            f"Bazaar_{screen_name}_UpperPrivacyScreen",
            (screen_x, zmin),
            (screen_x, zmax),
            bottom,
            top - bottom,
            ("quat_metal_window",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_upper_privacy_lattice_shopfront",
            nominal_cell=2.2,
            depth_scale=0.42 / window_depth,
        )
        for screen_object in screen_objects:
            screen_object["runtime_screen_thickness_m"] = 0.42
            screen_object["upper_only_bottom_top_y"] = f"{bottom:.3f},{top:.3f}"
        for trim_index, trim_bottom in enumerate((bottom, top - 0.20)):
            make_module_run(
                f"Bazaar_{screen_name}_UpperPrivacyTrim{trim_index:02d}",
                (screen_x, zmin),
                (screen_x, zmax),
                trim_bottom,
                0.20,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_upper_privacy_screen_trim",
                material=frame_material,
                nominal_cell=2.2,
                depth_scale=0.42 / trim_depth,
            )
        post_count = max(3, int(round((zmax - zmin) / 4.0)))
        create_authored_column_set(
            f"Bazaar_{screen_name}_UpperPrivacyPosts",
            tuple(
                (
                    screen_x,
                    zmin + (zmax - zmin) * post_index / post_count,
                    bottom,
                    top,
                )
                for post_index in range(post_count + 1)
            ),
            0.24,
            templates,
            specs,
            painted_steel,
            architecture,
            root,
            role="finished_cc0_upper_privacy_screen_posts",
        )

    for stair in STAIRS:
        create_authored_stair(
            stair,
            templates,
            specs,
            roof_sand,
            paving,
            elevation,
            root,
        )
        create_authored_stair_rails(
            stair,
            templates,
            specs,
            painted_steel,
            elevation,
            root,
        )
        create_authored_stair_supports(
            stair,
            templates,
            specs,
            painted_steel,
            elevation,
            root,
        )
        create_authored_stair_tread_nosings(
            stair,
            templates,
            specs,
            painted_steel,
            elevation,
            root,
        )

    # The three approach-side flights now use a real landing vestibule with a
    # side entry. Players step into a small room and turn onto the stairs, so
    # the doorway frame can no longer overlap the first treads.
    def build_approach_stair_vestibule(
        vestibule_name: str,
        center_x: float,
        building_z: float,
        stair_bottom_z: float,
        roof_y: float,
        entry_on_east: bool,
        region: str,
        wall_material: bpy.types.Material,
        roof_material: bpy.types.Material,
        floor_material: bpy.types.Material,
        sign_material: bpy.types.Material,
    ) -> None:
        half_width = 1.8
        entry_width = 3.2
        outer_z = stair_bottom_z + 4.3
        entry_center_z = stair_bottom_z + 2.2
        west_x = center_x - half_width
        east_x = center_x + half_width
        opening = (
            (
                entry_center_z - entry_width * 0.5 - building_z,
                entry_center_z + entry_width * 0.5 - building_z,
            ),
        )
        wall_bounds = f"{building_z:.3f},{outer_z:.3f}"
        for side_name, side_x, has_entry in (
            ("West", west_x, not entry_on_east),
            ("East", east_x, entry_on_east),
        ):
            side_openings = opening if has_entry else ()
            for tier_name, bottom, height, key, tier_material in (
                ("Lower", 0.0, 3.0, "trey_foundation", wall_material),
                ("Upper", 3.0, roof_y - 3.0, "quat_window_trim", floor_material),
            ):
                module_depth = source_dimensions(templates[key]).y
                wall_objects = make_segmented_wall(
                    f"Bazaar_{vestibule_name}_{side_name}{tier_name}",
                    (side_x, building_z),
                    (side_x, outer_z),
                    side_openings,
                    bottom,
                    height,
                    (key,),
                    templates,
                    specs,
                    architecture,
                    root,
                    role="finished_cc0_attached_stair_vestibule_wall",
                    material=tier_material,
                    nominal_cell=2.2,
                    depth_scale=0.42 / module_depth,
                )
                for wall_object in wall_objects:
                    wall_object["runtime_wall_thickness_m"] = 0.42
                    wall_object["runtime_wall_bounds_z"] = wall_bounds

        for tier_name, bottom, height, key, tier_material in (
            ("Lower", 0.0, 3.0, "trey_foundation", wall_material),
            ("Upper", 3.0, roof_y - 3.0, "quat_metal_window", floor_material),
        ):
            module_depth = source_dimensions(templates[key]).y
            outer_objects = make_module_run(
                f"Bazaar_{vestibule_name}_Outer{tier_name}",
                (west_x, outer_z),
                (east_x, outer_z),
                bottom,
                height,
                (key,),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_attached_stair_vestibule_outer_wall",
                material=tier_material,
                nominal_cell=1.8,
                depth_scale=0.42 / module_depth,
            )
            for wall_object in outer_objects:
                wall_object["runtime_wall_thickness_m"] = 0.42

        make_tiled_patch(
            f"Bazaar_{vestibule_name}_Floor",
            "quat_floor",
            center_x,
            (building_z + outer_z) * 0.5,
            half_width * 2.0,
            outer_z - building_z,
            0.035,
            0.12,
            templates,
            specs,
            surface,
            root,
            role="finished_cc0_attached_stair_vestibule_floor",
            material=floor_material,
        )
        make_tiled_patch(
            f"Bazaar_{vestibule_name}_Roof",
            "quat_floor",
            center_x,
            (building_z + outer_z) * 0.5,
            half_width * 2.0 + 0.2,
            outer_z - building_z,
            roof_y,
            0.14,
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_attached_stair_vestibule_roof",
            material=roof_material,
        )
        entry_x = east_x if entry_on_east else west_x
        make_portal(
            f"Bazaar_{vestibule_name}_SideEntryPortal",
            (entry_x, 0.0, entry_center_z),
            90.0,
            entry_width,
            templates,
            specs,
            architecture,
            root,
            region=region,
        )
        create_authored_column_set(
            f"Bazaar_{vestibule_name}_CornerPiers",
            tuple(
                (x, z, 0.0, roof_y)
                for x in (west_x, east_x)
                for z in (building_z, outer_z)
            ),
            0.24,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_attached_stair_vestibule_corner_piers",
        )
        make_module_run(
            f"Bazaar_{vestibule_name}_OuterSign",
            (center_x - 1.32, outer_z - 0.02),
            (center_x + 1.32, outer_z - 0.02),
            3.26,
            0.68,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=1.32,
            depth_scale=0.26,
        )
        for trim_index, trim_bottom in enumerate((0.10, 2.88, roof_y - 0.28)):
            make_module_run(
                f"Bazaar_{vestibule_name}_OuterTrim{trim_index:02d}",
                (west_x, outer_z - 0.02),
                (east_x, outer_z - 0.02),
                trim_bottom,
                0.22,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_attached_stair_vestibule_cornice",
                material=painted_steel,
                nominal_cell=1.8,
                depth_scale=0.72,
            )

    for vestibule_args in (
        ("A_SouthStair", -56.0, -4.0, 2.1, 6.3, True, "A_Caravanserai", warm, roof_clay, floor_terracotta, sign_ochre),
        ("B_SouthStair", 56.0, -6.0, 1.5, 6.4, False, "B_MarketWarehouse", concrete, roof_slate, floor_slate, sign_teal),
        ("Mid_SouthStair", -6.0, 34.0, 40.85, 6.1, True, "Mid_IndoorConnector", warm, roof_sand, floor_sand, sign_ochre),
    ):
        build_approach_stair_vestibule(*vestibule_args)

    # Defender back market: three roofed transfer halls replace the former
    # exposed cross-map court. The spawn remains a small central breathing bay.
    back_halls = (
        ("WestRearMarket", -40.0, -38.0, 26.0, 8.0),
        ("WestSpawnArcade", -17.0, -47.0, 20.0, 8.0),
        ("EastSpawnArcade", 17.0, -47.0, 20.0, 8.0),
        ("EastRearMarket", 40.0, -38.0, 26.0, 8.0),
    )
    back_roof_materials = (roof_clay, roof_sand, roof_slate, roof_clay)
    back_floor_materials = (floor_terracotta, floor_sand, floor_slate, floor_terracotta)
    for hall_index, (hall_name, x, z, sx, sz) in enumerate(back_halls):
        make_tiled_patch(
            f"Bazaar_Back_{hall_name}_Floor",
            "quat_floor",
            x,
            z,
            sx,
            sz,
            0.035,
            0.12,
            templates,
            specs,
            surface,
            root,
            role="finished_cc0_enterable_back_market_floor",
            material=back_floor_materials[hall_index],
        )
        make_tiled_patch(
            f"Bazaar_Back_{hall_name}_Roof",
            "trey_roof",
            x,
            z,
            sx,
            sz,
            4.15,
            0.16,
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_enterable_back_market_roof",
            material=back_roof_materials[hall_index],
        )
        xmin, xmax = x - sx * 0.5, x + sx * 0.5
        zmin, zmax = z - sz * 0.5, z + sz * 0.5
        # Arcaded north wall and staggered south kiosks create a protected,
        # non-linear retake route while retaining multiple 3.2 m doorways.
        make_module_run(
            f"Bazaar_Back_{hall_name}_NorthArcade",
            (xmin, zmin),
            (xmax, zmin),
            0.0,
            3.0,
            ("trey_arch",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_back_market_arcade",
            material=warm,
            nominal_cell=4.0,
        )
        make_segmented_wall(
            f"Bazaar_Back_{hall_name}_SouthKiosks",
            (xmin, zmax),
            (xmax, zmax),
            ((sx * 0.33 - 1.6, sx * 0.33 + 1.6), (sx * 0.76 - 1.6, sx * 0.76 + 1.6)),
            0.0,
            3.0,
            ("trey_foundation", "quat_window_trim"),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_back_market_kiosk_wall",
        )
        create_authored_column_set(
            f"Bazaar_Back_{hall_name}_Columns",
            tuple((column_x, zmin + 0.25, 0.0, 4.05) for column_x in (xmin + 1.0, x, xmax - 1.0)),
            0.44,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_back_market_columns",
        )
        make_module_run(
            f"Bazaar_Back_{hall_name}_CrossBeam",
            (xmin + 0.5, z),
            (xmax - 0.5, z),
            3.48,
            0.30,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_back_market_visible_beam",
            material=painted_steel,
            depth_scale=1.10,
        )
        sign_material = sign_ochre if hall_index % 2 == 0 else sign_teal
        make_module_run(
            f"Bazaar_Back_{hall_name}_ShopSign",
            (x - 2.5, zmax - 0.20),
            (x + 2.5, zmax - 0.20),
            2.20,
            0.74,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=2.5,
            depth_scale=0.28,
        )
        shelf_end = min(xmax - 2.2, xmin + 9.0)
        for shelf_index, shelf_bottom in enumerate((0.66, 1.42)):
            create_authored_horizontal_strip(
                f"Bazaar_Back_{hall_name}_WallShelf{shelf_index:02d}",
                (xmin + 2.2, zmax - 0.36),
                (shelf_end, zmax - 0.36),
                shelf_bottom,
                0.15,
                0.36,
                templates,
                specs,
                roof_sand,
                dressing,
                root,
                role="finished_cc0_wall_mounted_market_shelf",
            )
        make_module_run(
            f"Bazaar_Back_{hall_name}_WallBase",
            (xmin + 0.5, zmax - 0.24),
            (xmax - 0.5, zmax - 0.24),
            0.10,
            0.32,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_interior_wall_base",
            material=roof_sand,
            depth_scale=0.72,
        )
        make_module_run(
            f"Bazaar_Back_{hall_name}_WallAwning",
            (x - 2.8, zmax - 0.22),
            (x + 2.8, zmax - 0.22),
            3.04,
            0.34,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_wall_attached_market_awning",
            material=back_roof_materials[hall_index],
            nominal_cell=2.8,
            depth_scale=0.82,
        )
        for beam_index, beam_x in enumerate((x - sx * 0.24, x + sx * 0.24)):
            make_module_run(
                f"Bazaar_Back_{hall_name}_LongBeam{beam_index:02d}",
                (beam_x, zmin + 0.35),
                (beam_x, zmax - 0.35),
                3.52,
                0.24,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_back_market_visible_beam",
                material=painted_steel,
                depth_scale=0.92,
            )
        solid_segments = (
            (xmin + 0.8, xmin + sx * 0.33 - 1.95),
            (xmin + sx * 0.33 + 1.95, xmin + sx * 0.76 - 1.95),
            (xmin + sx * 0.76 + 1.95, xmax - 0.8),
        )
        for segment_index, (segment_min, segment_max) in enumerate(solid_segments):
            if segment_max - segment_min < 1.5:
                continue
            add_wall_storage_rack(
                f"Bazaar_Back_{hall_name}_KioskRack{segment_index:02d}",
                (segment_min, zmax - 0.34),
                (segment_max, zmax - 0.34),
                dark_timber if segment_index % 2 == 0 else roof_sand,
                shelf_levels=(0.62, 1.26, 1.90),
                post_top=2.54,
            )
            add_upper_shopfront_band(
                f"Bazaar_Back_{hall_name}_KioskUpper{segment_index:02d}",
                (segment_min, zmax - 0.25),
                (segment_max, zmax - 0.25),
                floor_sand if segment_index % 2 == 0 else floor_slate,
                bottom=2.72,
                height=0.88,
            )
        for extra_beam_index, beam_x in enumerate((x - sx * 0.12, x + sx * 0.12)):
            make_module_run(
                f"Bazaar_Back_{hall_name}_MarketAisleBeam{extra_beam_index:02d}",
                (beam_x, zmin + 0.4),
                (beam_x, zmax - 0.4),
                2.92,
                0.20,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_back_market_visible_beam",
                material=dark_timber,
                nominal_cell=2.4,
                depth_scale=0.72,
            )
        for ridge_index, ridge_z in enumerate((z - 1.15, z + 1.15)):
            make_module_run(
                f"Bazaar_Back_{hall_name}_SkylineRidge{ridge_index:02d}",
                (xmin + 0.7, ridge_z),
                (xmax - 0.7, ridge_z),
                4.32,
                0.22,
                ("trey_roof_trim",),
                templates,
                specs,
                architecture,
                root,
                role="finished_cc0_attached_skyline_ridge",
                material=back_roof_materials[hall_index],
                nominal_cell=3.0,
                depth_scale=0.68,
            )

    # False arched shop niches and framed signs resolve the long-hall end walls
    # without claiming a new runtime doorway through the closed link masses.
    for niche_name, niche_x, niche_z, sign_material in (
        ("WestTerminus", -27.18, -38.0, sign_ochre),
        ("EastTerminus", 27.18, -38.0, sign_teal),
    ):
        make_portal(
            f"Bazaar_Back_{niche_name}_ArchedNiche",
            (niche_x, 0.0, niche_z),
            90.0,
            3.2,
            templates,
            specs,
            architecture,
            root,
            region="Defender_BackMarket",
        )
        sign_x = niche_x + (0.22 if niche_x < 0.0 else -0.22)
        make_module_run(
            f"Bazaar_Back_{niche_name}_UpperSign",
            (sign_x, niche_z - 2.2),
            (sign_x, niche_z + 2.2),
            2.58,
            0.72,
            ("trey_foundation",),
            templates,
            specs,
            dressing,
            root,
            role="finished_cc0_wall_mounted_shop_sign",
            material=sign_material,
            nominal_cell=2.2,
            depth_scale=0.26,
        )
        gate_x = niche_x + (0.18 if niche_x < 0.0 else -0.18)
        add_upper_shopfront_band(
            f"Bazaar_Back_{niche_name}_ServiceGate",
            (gate_x, niche_z - 3.55),
            (gate_x, niche_z + 3.55),
            floor_slate if niche_x < 0.0 else floor_terracotta,
            bottom=3.02,
            height=0.82,
        )
        create_authored_column_set(
            f"Bazaar_Back_{niche_name}_ServiceGatePiers",
            tuple((gate_x, z, 0.0, 4.02) for z in (niche_z - 3.5, niche_z, niche_z + 3.5)),
            0.24,
            templates,
            specs,
            roof_sand,
            architecture,
            root,
            role="finished_cc0_back_market_service_gate_piers",
        )
        make_module_run(
            f"Bazaar_Back_{niche_name}_ServiceGateCornice",
            (gate_x, niche_z - 3.6),
            (gate_x, niche_z + 3.6),
            3.86,
            0.22,
            ("trey_roof_trim",),
            templates,
            specs,
            architecture,
            root,
            role="finished_cc0_back_market_service_gate_cornice",
            material=dark_timber,
            nominal_cell=2.4,
            depth_scale=0.70,
        )

    # Full-height rear city blocks bound the back-market folds. Their southern
    # faces leave a deliberate 5-7 m transfer corridor; only the 14 m defender
    # breathing bay remains open at the centre.
    rear_blocks = (
        ("FarWestGate", (-64.0, -53.0, -56.0, -31.0), 8.1, ("quat_window_trim", "trey_foundation"), warm),
        ("FarWestInn", (-53.0, -35.0, -56.0, -42.0), 7.4, ("quat_curved_window",), concrete),
        ("WestSpawnStore", (-17.0, -7.0, -43.0, -31.0), 6.6, ("quat_window_trim", "trey_foundation"), warm),
        ("DefenderGuild", (-7.0, 7.0, -43.0, -31.0), 7.2, ("quat_curved_window",), concrete),
        ("EastSpawnStore", (7.0, 20.0, -43.0, -31.0), 6.9, ("quat_metal_window", "trey_foundation"), steel),
        ("FarEastGuild", (35.0, 53.0, -56.0, -42.0), 7.7, ("quat_window_trim",), warm),
        ("FarEastGate", (53.0, 64.0, -56.0, -34.0), 8.3, ("quat_metal_window", "trey_window"), steel),
    )
    rear_roof_materials = (roof_slate, roof_clay, roof_sand, roof_slate, roof_clay, roof_sand, roof_slate)
    for block_index, (name, bounds, height, pattern, wall_material) in enumerate(rear_blocks):
        make_closed_block(
            f"BazaarRearBlock_{name}",
            bounds,
            height,
            pattern,
            templates,
            specs,
            rear_roof_materials[block_index],
            wall_material,
            architecture,
            root,
        )

    # Four legacy Old City facades survive only as distant perimeter anchors.
    for key, name, position, yaw, scale in (
        ("old_urban", "BazaarLandmark_WestGateHouse", (-62.0, 0.0, -49.0), 90.0, 0.62),
        ("scan_old", "BazaarLandmark_WestClockHouse", (-62.0, 0.0, 4.0), 90.0, 0.58),
        ("old_urban", "BazaarLandmark_EastGateHouse", (62.0, 0.0, -49.0), -90.0, 0.62),
        ("pawnshop", "BazaarLandmark_EastPawnSign", (62.0, 0.0, 3.0), -90.0, 0.58),
    ):
        place_source(
            templates,
            specs,
            key,
            name,
            position,
            yaw,
            scale,
            architecture,
            root,
            role="finished_cc0_outer_landmark_facade",
        )

    # Props are landmarks and readable market clutter, never the primary cover.
    prop_layout = (
        ("tea_table", "Bazaar_A_Courtyard_TeaTable", (-46.0, 0.16, -18.0), 12.0, 0.92),
        ("stool", "Bazaar_A_Courtyard_Stool00", (-47.2, 0.16, -17.0), 28.0, 0.92),
        ("stool", "Bazaar_A_Courtyard_Stool01", (-44.8, 0.16, -17.2), -20.0, 0.92),
        ("wicker_basket", "Bazaar_A_SpiceRack_Basket00", (-49.75, 0.16, -19.7), 18.0, 0.78),
        ("wicker_basket", "Bazaar_A_SpiceRack_Basket01", (-49.72, 0.16, -18.7), -12.0, 0.72),
        ("hand_truck", "Bazaar_A_Warehouse_HandTruck", (-56.5, 0.16, -27.0), 15.0, 0.88),
        ("military_crate", "Bazaar_A_Warehouse_Crate", (-53.7, 0.16, -27.5), -8.0, 1.0),
        ("coffee_cart_bottom", "Bazaar_B_CoffeeCart_Base", (56.2, 0.16, -18.5), 0.0, 0.92),
        ("coffee_cart_top", "Bazaar_B_CoffeeCart_Top", (56.2, 0.16, -18.5), 0.0, 0.92),
        ("coffee_cart_mugs", "Bazaar_B_CoffeeCart_Mugs", (56.2, 0.16, -18.5), 0.0, 0.92),
        ("wicker_basket", "Bazaar_B_ProduceBasket00", (55.2, 0.16, -16.9), 28.0, 0.82),
        ("wicker_basket", "Bazaar_B_ProduceBasket01", (56.4, 0.16, -16.7), -18.0, 0.76),
        ("hand_truck", "Bazaar_B_Loading_HandTruck", (36.6, 0.16, -24.2), 8.0, 0.82),
        ("plastic_crate", "Bazaar_Mid_ProduceCrate", (7.6, 0.16, 13.2), -10.0, 0.82),
        ("bicycle", "Bazaar_Back_West_Bicycle", (-29.1, 0.16, -40.1), 82.0, 0.88),
        ("wicker_basket", "Bazaar_Back_West_Basket", (-30.2, 0.16, -41.0), 16.0, 0.78),
        ("wicker_basket", "Bazaar_Back_East_Basket", (30.2, 0.16, -41.0), -16.0, 0.78),
    )
    for key, name, position, yaw, scale in prop_layout:
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
            role="finished_cc0_interior_landmark_prop",
        )

    # Suspended lamps give each main interior a distinct warm landmark and
    # visibly attach to authored beams rather than floating in space.
    supported_lanterns = (
        ((-50.5, 5.6, -18.0), (-48.5, 5.6, -18.0), 0.45),
        ((-39.5, 5.5, -26.0), (-41.0, 5.5, -26.0), 0.42),
        ((-55.5, 5.45, -12.5), (-53.8, 5.45, -12.5), 0.40),
        ((-45.5, 5.45, -22.0), (-44.0, 5.45, -22.0), 0.40),
        ((43.0, 5.7, -18.0), (45.0, 5.7, -18.0), 0.42),
        ((54.5, 5.7, -13.0), (52.5, 5.7, -13.0), 0.42),
        ((39.0, 5.48, -10.0), (40.6, 5.48, -10.0), 0.40),
        ((47.0, 5.48, -25.0), (48.6, 5.48, -25.0), 0.40),
        ((57.0, 5.48, -20.0), (55.4, 5.48, -20.0), 0.40),
        ((-1.5, 5.4, 12.0), (0.0, 5.4, 12.0), 0.40),
        ((-5.0, 5.4, 26.0), (-3.5, 5.4, 26.0), 0.40),
        ((-1.5, 5.3, -18.0), (0.0, 5.3, -18.0), 0.40),
        ((-6.5, 5.18, -2.0), (-5.0, 5.18, -2.0), 0.38),
        ((4.5, 5.18, 14.0), (3.0, 5.18, 14.0), 0.38),
        ((-17.8, 3.65, -47.0), (-16.2, 3.65, -47.0), 0.36),
        ((16.2, 3.65, -47.0), (17.8, 3.65, -47.0), 0.36),
        ((-39.0, 3.65, -38.0), (-37.5, 3.65, -38.0), 0.36),
        ((39.0, 3.65, -38.0), (37.5, 3.65, -38.0), 0.36),
    )
    for index, (anchor, hook, drop) in enumerate(supported_lanterns):
        place_supported_lantern(
            templates,
            specs,
            f"BazaarInteriorLantern_{index:02d}",
            anchor,
            hook,
            drop,
            0.58,
            steel,
            dressing,
            root,
        )

    for name, position, role in (
        ("Marker_AttackerSpawn", (0.0, 0.22, 49.0), "attacker_spawn"),
        ("Marker_DefenderSpawn", (0.0, 0.22, -49.0), "defender_spawn"),
        ("Marker_BombSite_A", (-46.0, 0.18, -18.0), "bomb_site_a"),
        ("Marker_BombSite_B", (46.0, 0.18, -18.0), "bomb_site_b"),
        ("Marker_A_Gallery_Top", (-56.0, 3.6, -18.0), "high_ground"),
        ("Marker_B_Balcony_Top", (56.0, 3.4, -18.0), "high_ground"),
        ("Marker_Mid_Mezzanine_Top", (-6.0, 3.2, 24.0), "high_ground"),
    ):
        add_marker(name, position, role, markers, root)

    for region_name, bounds, region_kind in (
        ("A_Caravanserai", (-60.0, -34.0, -31.0, -4.0), "two_storey_courtyard_arcade_warehouse"),
        ("B_MarketWarehouse", (34.0, 60.0, -30.0, -6.0), "roofed_column_hall_mezzanine"),
        ("Mid_IndoorConnector", (-9.0, 9.0, -8.0, 34.0), "three_hall_s_connector"),
        ("Defender_BackMarket", (-53.0, 53.0, -51.0, -34.0), "roofed_transfer_corridors"),
    ):
        marker = add_marker(
            f"Marker_Interior_{region_name}",
            ((bounds[0] + bounds[1]) * 0.5, 0.18, (bounds[2] + bounds[3]) * 0.5),
            "complete_enterable_interior",
            markers,
            root,
        )
        marker["interior_region"] = region_name
        marker["interior_kind"] = region_kind
        marker["godot_bounds_xz"] = ",".join(f"{value:.3f}" for value in bounds)

    for mesh in templates.values():
        mesh.use_fake_user = False
    return root, collections


def add_review_lighting(collection: bpy.types.Collection) -> None:
    sun_data = bpy.data.lights.new("BazaarReviewSun", "SUN")
    sun_data.energy = 2.65
    sun_data.angle = radians(22.0)
    sun = bpy.data.objects.new("BazaarReviewSun", sun_data)
    collection.objects.link(sun)
    sun.rotation_euler = (radians(32.0), radians(-18.0), radians(138.0))

    area_data = bpy.data.lights.new("BazaarReviewFill", "AREA")
    area_data.energy = 2100.0
    area_data.shape = "DISK"
    area_data.size = 42.0
    area = bpy.data.objects.new("BazaarReviewFill", area_data)
    collection.objects.link(area)
    area.location = (0.0, -8.0, 38.0)

    # Review-only practicals reveal the authored interior combat band. Runtime
    # lighting stays Godot-owned and these lights are excluded from the GLB.
    for index, position in enumerate(
        (
            (-55.0, 3.0, -18.0),
            (-46.0, 3.0, -27.0),
            (-37.5, 3.0, -16.0),
            (38.0, 3.2, -18.0),
            (46.0, 3.2, -18.0),
            (55.0, 3.2, -18.0),
            (53.6, 2.8, 6.2),
            (56.0, 3.0, 2.5),
            (58.4, 2.8, 5.4),
            (0.0, 3.0, -15.5),
            (-3.0, 3.0, -1.0),
            (3.0, 3.0, 12.0),
            (-3.0, 3.0, 27.0),
            (0.0, 3.0, -18.0),
            (-17.0, 2.8, -47.0),
            (17.0, 2.8, -47.0),
            (-40.0, 2.8, -38.0),
            (40.0, 2.8, -38.0),
        )
    ):
        light_data = bpy.data.lights.new(f"BazaarReviewInterior_{index:02d}", "POINT")
        light_data.energy = 720.0
        light_data.color = (1.0, 0.74, 0.52)
        light_data.shadow_soft_size = 2.2
        light_data.use_shadow = True
        light = bpy.data.objects.new(f"BazaarReviewInterior_{index:02d}", light_data)
        collection.objects.link(light)
        light.location = godot_to_blender(*position)


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
        ("01_overview.png", (98.0, -122.0, 112.0), (0.0, 1.0, -4.0), 45.0),
        ("02_a_interior.png", (-46.5, 11.0, 1.72), (-47.0, 1.45, -23.0), 32.0),
        ("03_b_interior.png", (46.0, 8.0, 1.68), (46.0, 1.45, -22.0), 31.0),
        ("04_mid_s_bend.png", (6.0, -18.5, 1.68), (-3.5, 1.45, 4.0), 31.0),
        ("05_mid_north_connector.png", (-6.0, 14.0, 1.68), (5.0, 1.45, -18.5), 28.0),
        ("06_back_market.png", (-50.0, 38.0, 1.68), (-29.0, 1.4, -38.0), 32.0),
        ("07_b_service_link.png", (49.0, -8.0, 1.72), (54.2, 1.35, 5.3), 34.0),
        ("08_b_stair_vestibule.png", (52.7, -4.8, 1.62), (55.8, 1.35, 1.0), 31.0),
    )
    preview_filter = {
        filename.strip()
        for filename in os.environ.get("BAZAAR_PREVIEW_FILTER", "").split(",")
        if filename.strip()
    }
    for filename, blender_location, target, lens in views:
        if preview_filter and filename not in preview_filter:
            continue
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


def validate_authored_scene_v2(root: bpy.types.Object) -> dict[str, object]:
    scene_objects = list(bpy.context.scene.objects)
    mesh_objects = [obj for obj in scene_objects if obj.type == "MESH" and obj.parent == root]
    if len(mesh_objects) < 160:
        raise RuntimeError(f"Bazaar V2 scene unexpectedly sparse: {len(mesh_objects)} meshes")
    if any(
        obj.parent != root
        for obj in scene_objects
        if obj.name.startswith("Bazaar")
        and obj != root
        and obj.type in {"MESH", "EMPTY"}
    ):
        raise RuntimeError("Bazaar V2 export hierarchy must remain flat under the map root")

    names = {obj.name for obj in scene_objects}
    required = {
        "BazaarGroundAuthoredMesh",
        "Bazaar_A_InteriorFloor",
        "Bazaar_A_Gallery_Deck",
        "Bazaar_B_InteriorFloor",
        "Bazaar_B_WarehouseRoof",
        "Bazaar_B_Balcony_Deck",
        "Bazaar_Mid_Mezzanine_Deck",
        "Bazaar_Mid_NorthConnector_Roof",
        "Bazaar_Mid_NorthTeaHall_Roof",
        "Bazaar_Mid_CenterProduceHall_Roof",
        "Bazaar_Mid_SouthCarpetHall_Roof",
        "BazaarBlock_AttackWestEntryWing_Roof",
        "BazaarBlock_AttackEastEntryWing_Roof",
        "BazaarBlock_WestLaneLink_Roof",
        "BazaarBlock_EastLaneLink_Roof",
        "BazaarBlock_WestServiceClosure_Roof",
        "BazaarBlock_EastServiceClosure_Roof",
        "Bazaar_A_Gallery_UpperPrivacyScreen_metal_window",
        "Bazaar_B_Balcony_UpperPrivacyScreen_metal_window",
        "Bazaar_Mid_WestConnectorReturn_Tier00_window_trim",
        "Bazaar_Mid_WestConnectorReturn_Tier01_metal_window",
        "Bazaar_Mid_EastConnectorReturn_Tier00_window_trim",
        "Bazaar_Mid_EastConnectorReturn_Tier01_metal_window",
        "Bazaar_WestApproachFacadeReturn_Tier00_window_trim",
        "Bazaar_WestApproachFacadeReturn_Tier01_metal_window",
        "Bazaar_EastApproachServiceReturn_Lower_Section00_window_trim",
        "Bazaar_EastApproachServiceReturn_Lower_Section01_window_trim",
        "Bazaar_EastApproachServiceReturn_Upper_Section00_metal_window",
        "Bazaar_EastApproachServiceReturn_Upper_Section01_metal_window",
        "Bazaar_EastApproachServiceReturn_Portal",
        "Bazaar_EastServicePocketClosure_Tier00_window_trim",
        "Bazaar_EastServicePocketClosure_Tier01_metal_window",
        "Bazaar_A_SouthStair_Roof",
        "Bazaar_A_SouthStair_SideEntryPortal",
        "Bazaar_B_SouthStair_Roof",
        "Bazaar_B_SouthStair_SideEntryPortal",
        "Bazaar_Mid_SouthStair_Roof",
        "Bazaar_Mid_SouthStair_SideEntryPortal",
        "Bazaar_MidCarpetSouthFacadeReturn_Tier00_window_trim",
        "Bazaar_MidCarpetSouthFacadeReturn_Tier01_metal_window",
        "Bazaar_Defender_WestFoyerPier_Tier00_window_trim",
        "Bazaar_Defender_WestFoyerPier_Tier01_metal_window",
        "Bazaar_Defender_EastFoyerPier_Tier00_window_trim",
        "Bazaar_Defender_EastFoyerPier_Tier01_metal_window",
        "Bazaar_Defender_WestFoyerReturn_Tier00_window_trim",
        "Bazaar_Defender_WestFoyerReturn_Tier01_metal_window",
        "Bazaar_Defender_EastFoyerReturn_Tier00_window_trim",
        "Bazaar_Defender_EastFoyerReturn_Tier01_metal_window",
        "Marker_Interior_A_Caravanserai",
        "Marker_Interior_B_MarketWarehouse",
        "Marker_Interior_Mid_IndoorConnector",
        "Marker_Interior_Defender_BackMarket",
        *(stair.name for stair in STAIRS),
        *(f"{stair.name}_AuthoredTreyTreadNosings" for stair in STAIRS),
    }
    missing = sorted(required - names)
    if missing:
        raise RuntimeError(f"Missing required Bazaar V2 objects: {missing}")

    detached_foyer_prefixes = (
        "Bazaar_Mid_WestSouthFrontageBaffle",
        "Bazaar_Mid_EastSouthFrontageBaffle",
    )
    detached_foyer_parts = sorted(
        name for name in names if name.startswith(detached_foyer_prefixes)
    )
    if detached_foyer_parts:
        raise RuntimeError(
            "Detached attack-foyer baffles survived the rebuild: "
            f"{detached_foyer_parts}"
        )

    validated_meshes: set[bpy.types.Mesh] = set()
    for obj in mesh_objects:
        origin = str(obj.get("bazaar_asset_origin", ""))
        if origin != "cc0":
            raise RuntimeError(
                f"Visible Bazaar V2 mesh must come from a finished CC0 module: "
                f"{obj.name} origin={origin!r}"
            )
        searchable = " ".join(
            str(obj.get(key, ""))
            for key in ("source_asset", "source_creator", "source_url", "license")
        ).lower()
        if any(token in searchable for token in FORBIDDEN_SOURCE_TOKENS):
            raise RuntimeError(f"Forbidden non-CC0 source token on {obj.name}: {searchable}")
        for metadata_key in (
            "license",
            "source_asset",
            "source_object",
            "source_creator",
            "source_url",
        ):
            if not str(obj.get(metadata_key, "")).strip():
                raise RuntimeError(f"CC0 instance lacks {metadata_key} provenance: {obj.name}")
        if "CC0" not in str(obj.get("license", "")):
            raise RuntimeError(f"CC0 instance lacks license metadata: {obj.name}")
        if not obj.data.uv_layers:
            raise RuntimeError(f"Visible mesh lacks UVs: {obj.name}")
        if not any(material is not None for material in obj.data.materials):
            raise RuntimeError(f"Visible mesh lacks material: {obj.name}")
        if obj.data in validated_meshes:
            continue
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
        for polygon in mesh.polygons:
            if (
                polygon.material_index >= len(mesh.materials)
                or mesh.materials[polygon.material_index] is None
            ):
                raise RuntimeError(f"Visible mesh polygon lacks material: {obj.name}")

    interior_markers = [
        obj
        for obj in scene_objects
        if obj.type == "EMPTY" and obj.get("marker_role") == "complete_enterable_interior"
    ]
    if len(interior_markers) != 4:
        raise RuntimeError(
            f"Bazaar V2 must retain four complete enterable interiors, found {len(interior_markers)}"
        )
    expected_regions = {
        "A_Caravanserai",
        "B_MarketWarehouse",
        "Mid_IndoorConnector",
        "Defender_BackMarket",
    }
    actual_regions = {str(marker.get("interior_region", "")) for marker in interior_markers}
    if actual_regions != expected_regions:
        raise RuntimeError(f"Interior-region contract drifted: {sorted(actual_regions)}")

    legacy_facades = [
        obj for obj in mesh_objects if obj.get("bazaar_role") == "finished_cc0_outer_landmark_facade"
    ]
    if len(legacy_facades) != 4:
        raise RuntimeError(
            f"Legacy whole-building facades must remain four outer landmarks, found {len(legacy_facades)}"
        )
    if any("finished_boundary_shophouse" == obj.get("bazaar_role") for obj in mesh_objects):
        raise RuntimeError("V1 repeated boundary shophouses survived the V2 rebuild")

    architectural_cover_tokens = (
        "wall",
        "arcade",
        "column",
        "counter",
        "partition",
        "portal",
        "kiosk",
    )
    architectural_cover = [
        obj
        for obj in mesh_objects
        if any(token in str(obj.get("bazaar_role", "")) for token in architectural_cover_tokens)
    ]
    prop_cover = [
        obj
        for obj in mesh_objects
        if "cover" in str(obj.get("bazaar_role", ""))
        and obj not in architectural_cover
    ]
    cover_ratio = len(architectural_cover) / max(1, len(architectural_cover) + len(prop_cover))
    if cover_ratio < 0.70:
        raise RuntimeError(f"Architectural cover ratio below 70%: {cover_ratio:.3f}")

    art_polish_counts = {
        "shop_signs": sum(
            obj.get("bazaar_role") == "finished_cc0_wall_mounted_shop_sign"
            for obj in mesh_objects
        ),
        "wall_shelves": sum(
            "shelf" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
        "wall_awnings": sum(
            obj.get("bazaar_role") == "finished_cc0_wall_attached_market_awning"
            for obj in mesh_objects
        ),
        "roof_parapets": sum(
            "roof_parapet" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
        "rooftop_lantern_parts": sum(
            "rooftop_lantern" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
        "upper_privacy_screen_parts": sum(
            "upper_privacy" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
        "stair_tread_nosing_assemblies": sum(
            obj.get("bazaar_role") == "finished_cc0_authored_stair_tread_nosings"
            for obj in mesh_objects
        ),
        "continuous_storage_parts": sum(
            "continuous_wall_storage" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
        "continuous_shopfront_parts": sum(
            "continuous_upper_shopfront" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
        "skyline_articulation_parts": sum(
            any(
                token in str(obj.get("bazaar_role", ""))
                for token in ("skyline", "clerestory")
            )
            for obj in mesh_objects
        ),
        "back_service_gate_parts": sum(
            "back_market_service_gate" in str(obj.get("bazaar_role", ""))
            for obj in mesh_objects
        ),
    }
    polish_minimums = {
        "shop_signs": 18,
        "wall_shelves": 20,
        "wall_awnings": 10,
        "roof_parapets": 40,
        "rooftop_lantern_parts": 8,
        "upper_privacy_screen_parts": 8,
        "stair_tread_nosing_assemblies": len(STAIRS),
        "continuous_storage_parts": 80,
        "continuous_shopfront_parts": 60,
        "skyline_articulation_parts": 45,
        "back_service_gate_parts": 4,
    }
    for polish_key, minimum in polish_minimums.items():
        if art_polish_counts[polish_key] < minimum:
            raise RuntimeError(
                f"Bazaar V2 art polish gate failed for {polish_key}: "
                f"{art_polish_counts[polish_key]} < {minimum}"
            )

    mid_wall_groups = (
        "Bazaar_Mid_NorthConnector",
        "Bazaar_Mid_NorthTeaHall",
        "Bazaar_Mid_CenterProduceHall",
        "Bazaar_Mid_SouthCarpetHall",
    )
    for prefix in mid_wall_groups:
        walls = [
            obj
            for obj in mesh_objects
            if obj.name.startswith(prefix)
            and "wall" in str(obj.get("bazaar_role", "")).lower()
        ]
        if len(walls) < 4:
            raise RuntimeError(f"Indoor Mid hall lacks four-sided sight block: {prefix}")

    for stair in STAIRS:
        obj = bpy.data.objects[stair.name]
        if obj.get("bazaar_role") != "finished_cc0_authored_stair_assembly":
            raise RuntimeError(f"Stair is not a finished CC0 assembly: {stair.name}")
        slope = float(obj["slope_degrees"])
        if slope > 18.1 or abs(float(obj["path_width_m"]) - stair.width) > 0.0001:
            raise RuntimeError(f"Stair traversal gate failed for {stair.name}: slope={slope:.3f}")
        if bpy.data.objects.get(f"{stair.name}_AuthoredTreyRails") is None:
            raise RuntimeError(f"Stair lacks authored guardrails: {stair.name}")
        if bpy.data.objects.get(f"{stair.name}_AuthoredTreyNewels") is None:
            raise RuntimeError(f"Stair lacks authored newels: {stair.name}")
        nosings = bpy.data.objects.get(f"{stair.name}_AuthoredTreyTreadNosings")
        if nosings is None or int(nosings.get("tread_nosing_instances", 0)) != stair.steps:
            raise RuntimeError(f"Stair lacks one authored tread nosing per step: {stair.name}")

    for screen_name, expected_thickness, expected_band in (
        ("Bazaar_A_Gallery_UpperPrivacyScreen_metal_window", 0.42, "3.600,6.400"),
        ("Bazaar_B_Balcony_UpperPrivacyScreen_metal_window", 0.42, "3.400,6.500"),
    ):
        screen = bpy.data.objects[screen_name]
        if (
            abs(float(screen.get("runtime_screen_thickness_m", 0.0)) - expected_thickness) > 0.001
            or screen.get("upper_only_bottom_top_y") != expected_band
        ):
            raise RuntimeError(f"Upper privacy-screen contract drifted: {screen_name}")

    for wall_name, expected_bounds in (
        ("Bazaar_WestApproachFacadeReturn_Tier00_window_trim", "-49.000,-4.000,-49.000,12.000"),
        ("Bazaar_EastApproachServiceReturn_Lower_Section00_window_trim", "52.000,-6.000,52.000,12.000"),
        ("Bazaar_EastApproachServiceReturn_Lower_Section01_window_trim", "52.000,-6.000,52.000,12.000"),
        ("Bazaar_EastServicePocketClosure_Tier00_window_trim", "52.000,9.400,60.000,9.400"),
        ("Bazaar_Mid_WestConnectorReturn_Tier00_window_trim", "-20.000,-21.000,-20.000,-17.800"),
        ("Bazaar_Mid_EastConnectorReturn_Tier00_window_trim", "20.000,-14.800,20.000,-12.000"),
        ("Bazaar_MidCarpetSouthFacadeReturn_Tier00_window_trim", "3.000,34.000,8.000,34.000"),
        ("Bazaar_Defender_WestFoyerPier_Tier00_window_trim", "-20.000,-56.000,-20.000,-46.200"),
        ("Bazaar_Defender_EastFoyerPier_Tier00_window_trim", "20.000,-56.000,20.000,-46.200"),
        ("Bazaar_Defender_WestFoyerReturn_Tier00_window_trim", "-20.000,-46.200,-7.500,-46.200"),
        ("Bazaar_Defender_EastFoyerReturn_Tier00_window_trim", "7.500,-46.200,20.000,-46.200"),
    ):
        wall = bpy.data.objects[wall_name]
        if (
            abs(float(wall.get("runtime_wall_thickness_m", 0.0)) - 0.42) > 0.001
            or wall.get("runtime_wall_bounds_xz") != expected_bounds
        ):
            raise RuntimeError(f"Runtime facade-return contract drifted: {wall_name}")

    for vestibule_name, expected_entry_x, expected_entry_z, expected_roof_y in (
        ("A_SouthStair", -54.2, 4.3, 6.3),
        ("B_SouthStair", 54.2, 3.7, 6.4),
        ("Mid_SouthStair", -4.2, 43.05, 6.1),
    ):
        portal = bpy.data.objects[f"Bazaar_{vestibule_name}_SideEntryPortal"]
        roof = bpy.data.objects[f"Bazaar_{vestibule_name}_Roof"]
        expected_portal_location = godot_to_blender(expected_entry_x, 0.0, expected_entry_z)
        if (
            (portal.location - expected_portal_location).length > 0.002
            or abs(float(portal.get("clear_opening_width_m", 0.0)) - 3.2) > 0.001
        ):
            raise RuntimeError(f"Stair vestibule side-entry contract drifted: {vestibule_name}")
        roof_top = max((roof.matrix_world @ Vector(corner)).z for corner in roof.bound_box)
        if abs(roof_top - (expected_roof_y + 0.14)) > 0.012:
            raise RuntimeError(f"Stair vestibule roof contract drifted: {vestibule_name}")

    service_counter = bpy.data.objects["Bazaar_B_ServiceCounter_foundation"]
    service_counter_points = [
        service_counter.matrix_world @ Vector(corner) for corner in service_counter.bound_box
    ]
    service_counter_bounds = (
        min(point.x for point in service_counter_points),
        max(point.x for point in service_counter_points),
        min(-point.y for point in service_counter_points),
        max(-point.y for point in service_counter_points),
        max(point.z for point in service_counter_points),
    )
    expected_service_counter_bounds = (58.19, 58.81, 1.4, 6.0, 1.16)
    if any(
        abs(actual - expected) > 0.012
        for actual, expected in zip(service_counter_bounds, expected_service_counter_bounds)
    ):
        raise RuntimeError(
            "B service counter drifted from runtime cover: "
            f"actual={service_counter_bounds} expected={expected_service_counter_bounds}"
        )

    platform_contract = {
        "Bazaar_A_Gallery_Deck": 3.6,
        "Bazaar_B_Balcony_Deck": 3.4,
        "Bazaar_Mid_Mezzanine_Deck": 3.2,
    }
    for name, expected_top in platform_contract.items():
        obj = bpy.data.objects[name]
        actual_top = max((obj.matrix_world @ Vector(corner)).z for corner in obj.bound_box)
        if abs(actual_top - expected_top) > 0.012:
            raise RuntimeError(f"Interior platform height drifted for {name}: {actual_top:.4f}")

    minimum, maximum = object_world_bounds(mesh_objects)
    if minimum.x < MAP_X_MIN - 0.25 or maximum.x > MAP_X_MAX + 0.25:
        raise RuntimeError(f"Bazaar V2 X bounds escaped frame: {minimum.x}..{maximum.x}")
    if minimum.y < -MAP_Z_MAX - 0.25 or maximum.y > -MAP_Z_MIN + 0.25:
        raise RuntimeError(
            f"Bazaar V2 Z bounds escaped frame: Blender Y {minimum.y}..{maximum.y}"
        )
    if minimum.z < -0.35 or maximum.z > 10.5:
        raise RuntimeError(f"Bazaar V2 vertical bounds invalid: {minimum.z}..{maximum.z}")

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
        len({polygon.material_index for polygon in obj.data.polygons}) for obj in mesh_objects
    )
    raw_source_triangles = sum(
        spec.expected_triangles for spec in (*SOURCE_SPECS, *COFFEE_CART_SOURCE_SPECS)
    )
    roof_footprint_m2 = 0.0
    for obj in mesh_objects:
        role = str(obj.get("bazaar_role", ""))
        footprint = str(obj.get("footprint_m", ""))
        if "roof" not in role or "," not in footprint:
            continue
        size_x, size_z = (float(value) for value in footprint.split(",", 1))
        roof_footprint_m2 += size_x * size_z

    return {
        "revision": "V2 dense enterable interiors",
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
        "complete_enterable_interior_count": len(interior_markers),
        "interior_regions": sorted(actual_regions),
        "architectural_cover_object_count": len(architectural_cover),
        "prop_cover_object_count": len(prop_cover),
        "architectural_cover_ratio": round(cover_ratio, 4),
        "art_polish_object_counts": art_polish_counts,
        "roofed_footprint_m2": round(roof_footprint_m2, 3),
        "legacy_outer_facade_count": len(legacy_facades),
        "closed_modular_block_count": len(
            [obj for obj in mesh_objects if obj.get("bazaar_role") == "finished_cc0_closed_urban_block_roof"]
        ),
        "stairs": {
            stair.name: {
                "width_m": float(bpy.data.objects[stair.name]["path_width_m"]),
                "run_m": float(bpy.data.objects[stair.name]["run_m"]),
                "slope_degrees": round(
                    float(bpy.data.objects[stair.name]["slope_degrees"]), 4
                ),
                "steps": int(bpy.data.objects[stair.name]["step_count"]),
            }
            for stair in STAIRS
        },
        "platform_top_m": platform_contract,
        "authored_module_contract": {
            "visible_mesh_origin_counts": {
                "cc0": len(mesh_objects),
                "project_authored": 0,
                "procedural": 0,
            },
            "quaternius_downtown_sources": 6,
            "trey_structural_sources": 16,
            "complete_interior_regions": sorted(actual_regions),
        },
    }


def optimize_static_draw_nodes_v2(
    root: bpy.types.Object,
    scene_stats: dict[str, object],
) -> dict[str, object]:
    """Batch compatible static art without changing triangles or materials.

    Batches never cross a DCC collection, spatial region, authored
    responsibility, source/provenance identity, or material signature.
    Runtime-contract walls, roofs, decks, and stair assemblies retain stable
    object names for inspection and round-trip checks. Shared mesh instances
    are deliberately left instanced so batching cannot inflate the
    unique-triangle budget.
    """
    protected_names = {
        "BazaarGroundAuthoredMesh",
        "Bazaar_A_Gallery_Deck",
        "Bazaar_B_Balcony_Deck",
        "Bazaar_Mid_Mezzanine_Deck",
        "Bazaar_B_WarehouseRoof",
        "Bazaar_Mid_NorthConnector_Roof",
        "Bazaar_Mid_NorthTeaHall_Roof",
        "Bazaar_Mid_CenterProduceHall_Roof",
        "Bazaar_Mid_SouthCarpetHall_Roof",
        "BazaarBlock_AttackWestEntryWing_Roof",
        "BazaarBlock_AttackEastEntryWing_Roof",
        "BazaarBlock_WestLaneLink_Roof",
        "BazaarBlock_EastLaneLink_Roof",
        "BazaarBlock_WestServiceClosure_Roof",
        "BazaarBlock_EastServiceClosure_Roof",
        *(stair.name for stair in STAIRS),
    }
    pre_meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.parent == root
    ]
    pre_bounds_min, pre_bounds_max = object_world_bounds(pre_meshes)
    pre_instance_triangles = sum(mesh_triangles(obj.data) for obj in pre_meshes)
    pre_unique_triangles = sum(
        mesh_triangles(mesh) for mesh in {obj.data for obj in pre_meshes}
    )
    pre_surface_count = sum(
        len({polygon.material_index for polygon in obj.data.polygons})
        for obj in pre_meshes
    )

    def is_protected(obj: bpy.types.Object) -> bool:
        role = str(obj.get("bazaar_role", ""))
        return (
            obj.name in protected_names
            or any(obj.name.startswith(stair.name) for stair in STAIRS)
            or "deck" in role
            or "upper_privacy" in role
            or obj.get("runtime_wall_thickness_m") is not None
            or obj.get("runtime_screen_thickness_m") is not None
            or obj.data.users != 1
            or obj.data.shape_keys is not None
            or bool(obj.modifiers)
        )

    candidates = [obj for obj in pre_meshes if not is_protected(obj)]
    protected_mesh_names = {obj.name for obj in pre_meshes if is_protected(obj)}
    pre_runtime_wall_contract = sorted(
        (
            obj.name,
            float(obj.get("runtime_wall_thickness_m", 0.0)),
            str(obj.get("runtime_wall_bounds_xz", "")),
        )
        for obj in pre_meshes
        if obj.get("runtime_wall_thickness_m") is not None
    )
    pre_runtime_screen_contract = sorted(
        (
            obj.name,
            float(obj.get("runtime_screen_thickness_m", 0.0)),
            str(obj.get("upper_only_bottom_top_y", "")),
        )
        for obj in pre_meshes
        if obj.get("runtime_screen_thickness_m") is not None
    )

    def spatial_center(obj: bpy.types.Object) -> Vector:
        corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        return sum(corners, Vector((0.0, 0.0, 0.0))) / len(corners)

    def group_candidates(bucket_m: float) -> dict[tuple[object, ...], list[bpy.types.Object]]:
        groups: dict[tuple[object, ...], list[bpy.types.Object]] = {}
        for obj in candidates:
            center = spatial_center(obj)
            region_x = int((center.x - MAP_X_MIN) / bucket_m)
            godot_z = -center.y
            region_z = int((godot_z - MAP_Z_MIN) / bucket_m)
            collection_name = sorted(collection.name for collection in obj.users_collection)[0]
            material_signature = tuple(
                material.name_full if material is not None else ""
                for material in obj.data.materials
            )
            provenance_signature = tuple(
                str(obj.get(key, ""))
                for key in (
                    "bazaar_asset_origin",
                    "license",
                    "source_asset",
                    "source_object",
                    "source_creator",
                    "source_url",
                )
            )
            key = (
                collection_name,
                region_x,
                region_z,
                str(obj.get("bazaar_role", "")),
                material_signature,
                provenance_signature,
            )
            groups.setdefault(key, []).append(obj)
        return groups

    chosen_bucket = 0.0
    chosen_groups: dict[tuple[object, ...], list[bpy.types.Object]] = {}
    predicted_nodes = len(pre_meshes)
    for bucket_m in (18.0, 24.0, 32.0, 44.0, 64.0, 140.0):
        groups = group_candidates(bucket_m)
        predicted = len(pre_meshes) - sum(
            len(group) - 1 for group in groups.values() if len(group) > 1
        )
        chosen_bucket = bucket_m
        chosen_groups = groups
        predicted_nodes = predicted
        if predicted <= TARGET_EXPORT_DRAW_NODES:
            break
    if predicted_nodes > MAX_EXPORT_DRAW_NODES:
        raise RuntimeError(
            "Region/material batching cannot meet draw-node gate: "
            f"predicted {predicted_nodes} > {MAX_EXPORT_DRAW_NODES}"
        )

    def normalize_material_slots(obj: bpy.types.Object) -> None:
        mesh = obj.data
        old_slots = list(mesh.materials)
        polygon_materials = [old_slots[polygon.material_index] for polygon in mesh.polygons]
        unique_materials: list[bpy.types.Material] = []
        material_indices: dict[int, int] = {}
        for material in polygon_materials:
            if material is None:
                raise RuntimeError(f"Batched mesh polygon lost material: {obj.name}")
            pointer = material.as_pointer()
            if pointer not in material_indices:
                material_indices[pointer] = len(unique_materials)
                unique_materials.append(material)
        mesh.materials.clear()
        for material in unique_materials:
            mesh.materials.append(material)
        for polygon, material in zip(mesh.polygons, polygon_materials, strict=True):
            polygon.material_index = material_indices[material.as_pointer()]
        mesh.update()

    merged_groups = 0
    merged_source_nodes = 0
    for group in chosen_groups.values():
        if len(group) < 2:
            continue
        live_group = [
            obj
            for obj in group
            if bpy.context.scene.objects.get(obj.name) is obj
        ]
        if len(live_group) < 2:
            continue
        live_group.sort(key=lambda obj: obj.name)
        leader = live_group[0]
        role_set = sorted({str(obj.get("bazaar_role", "")) for obj in live_group})
        source_names = "\n".join(obj.name for obj in live_group)
        bpy.ops.object.select_all(action="DESELECT")
        for obj in live_group:
            obj.hide_set(False)
            obj.hide_viewport = False
            obj.select_set(True)
        bpy.context.view_layer.objects.active = leader
        result = bpy.ops.object.join()
        if "FINISHED" not in result:
            raise RuntimeError(f"Static art batching failed for {leader.name}: {result}")
        normalize_material_slots(leader)
        leader["dcc_batch_source_node_count"] = len(live_group)
        leader["dcc_batch_source_names_sha256"] = sha256(
            source_names.encode("utf-8")
        ).hexdigest().upper()
        leader["dcc_batch_roles"] = json.dumps(role_set, ensure_ascii=False)
        leader["dcc_batch_spatial_bucket_m"] = chosen_bucket
        merged_groups += 1
        merged_source_nodes += len(live_group)

    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = None
    bpy.context.view_layer.update()
    post_meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.parent == root
    ]
    post_unique_meshes = {obj.data for obj in post_meshes}
    post_instance_triangles = sum(mesh_triangles(obj.data) for obj in post_meshes)
    post_unique_triangles = sum(mesh_triangles(mesh) for mesh in post_unique_meshes)
    post_surface_count = sum(
        len({polygon.material_index for polygon in obj.data.polygons})
        for obj in post_meshes
    )
    post_bounds_min, post_bounds_max = object_world_bounds(post_meshes)
    post_names = {obj.name for obj in post_meshes}
    missing_protected = sorted(protected_mesh_names - post_names)
    if missing_protected:
        raise RuntimeError(
            "Static batching removed protected required objects: "
            f"{missing_protected}"
        )
    post_runtime_wall_contract = sorted(
        (
            obj.name,
            float(obj.get("runtime_wall_thickness_m", 0.0)),
            str(obj.get("runtime_wall_bounds_xz", "")),
        )
        for obj in post_meshes
        if obj.get("runtime_wall_thickness_m") is not None
    )
    post_runtime_screen_contract = sorted(
        (
            obj.name,
            float(obj.get("runtime_screen_thickness_m", 0.0)),
            str(obj.get("upper_only_bottom_top_y", "")),
        )
        for obj in post_meshes
        if obj.get("runtime_screen_thickness_m") is not None
    )
    if post_runtime_wall_contract != pre_runtime_wall_contract:
        raise RuntimeError("Static batching changed exact runtime wall metadata")
    if post_runtime_screen_contract != pre_runtime_screen_contract:
        raise RuntimeError("Static batching changed exact privacy-screen metadata")
    if len(post_meshes) > MAX_EXPORT_DRAW_NODES:
        raise RuntimeError(
            f"Export draw-node gate failed: {len(post_meshes)} > {MAX_EXPORT_DRAW_NODES}"
        )
    if post_instance_triangles != pre_instance_triangles:
        raise RuntimeError(
            "Static art batching changed delivered triangles: "
            f"{pre_instance_triangles} -> {post_instance_triangles}"
        )
    if post_unique_triangles != pre_unique_triangles:
        raise RuntimeError(
            "Static art batching changed unique triangles: "
            f"{pre_unique_triangles} -> {post_unique_triangles}"
        )
    if (
        (post_bounds_min - pre_bounds_min).length > 0.002
        or (post_bounds_max - pre_bounds_max).length > 0.002
    ):
        raise RuntimeError("Static art batching changed authored map bounds")
    for obj in post_meshes:
        if str(obj.get("bazaar_asset_origin", "")) != "cc0":
            raise RuntimeError(f"Batched export lost CC0 origin: {obj.name}")
        if "CC0" not in str(obj.get("license", "")):
            raise RuntimeError(f"Batched export lost CC0 license: {obj.name}")
        if not obj.data.uv_layers:
            raise RuntimeError(f"Batched export lost UVs: {obj.name}")

    optimization = {
        "strategy": (
            "same collection, spatial region, authored responsibility, source "
            "provenance, and material signature"
        ),
        "spatial_bucket_m": chosen_bucket,
        "merged_group_count": merged_groups,
        "merged_source_node_count": merged_source_nodes,
        "pre_draw_nodes": len(pre_meshes),
        "post_draw_nodes": len(post_meshes),
        "pre_surface_draw_count": pre_surface_count,
        "post_surface_draw_count": post_surface_count,
        "pre_unique_triangles": pre_unique_triangles,
        "post_unique_triangles": post_unique_triangles,
        "pre_instance_triangles": pre_instance_triangles,
        "post_instance_triangles": post_instance_triangles,
        "triangle_and_bounds_preserved": True,
    }
    scene_stats["object_count"] = len(bpy.context.scene.objects)
    scene_stats["mesh_object_count"] = len(post_meshes)
    scene_stats["unique_mesh_count"] = len(post_unique_meshes)
    scene_stats["draw_node_count"] = len(post_meshes)
    scene_stats["surface_draw_count"] = post_surface_count
    scene_stats["unique_triangles"] = post_unique_triangles
    scene_stats["instance_triangles"] = post_instance_triangles
    scene_stats["draw_node_optimization"] = optimization
    scene_stats["authored_module_contract"]["visible_mesh_origin_counts"]["cc0"] = len(
        post_meshes
    )
    return optimization


def select_export_hierarchy(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in bpy.context.scene.objects:
        if obj.parent == root:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def read_glb_json_document(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    if len(data) < 20 or data[:4] != b"glTF":
        raise RuntimeError(f"Exported file is not a valid GLB container: {path}")
    version = int.from_bytes(data[4:8], "little")
    declared_length = int.from_bytes(data[8:12], "little")
    json_chunk_length = int.from_bytes(data[12:16], "little")
    json_chunk_type = int.from_bytes(data[16:20], "little")
    if version != 2:
        raise RuntimeError(f"Exported GLB version must be 2, found {version}")
    if declared_length != len(data):
        raise RuntimeError(
            f"Exported GLB length mismatch: header={declared_length}, file={len(data)}"
        )
    if json_chunk_type != 0x4E4F534A:
        raise RuntimeError("Exported GLB first chunk is not JSON")
    json_end = 20 + json_chunk_length
    if json_end > len(data):
        raise RuntimeError("Exported GLB JSON chunk exceeds file length")
    document = json.loads(data[20:json_end].decode("utf-8").rstrip(" \t\r\n\0"))
    if not isinstance(document, dict):
        raise RuntimeError("Exported GLB JSON document is not an object")
    return document


def validate_glb_extension_contract() -> dict[str, object]:
    document = read_glb_json_document(OUTPUT_GLB)
    extensions_required = sorted(str(value) for value in document.get("extensionsRequired", []))
    extensions_used = sorted(str(value) for value in document.get("extensionsUsed", []))
    forbidden_extension = "KHR_draco_mesh_compression"
    encoded_document = json.dumps(document, separators=(",", ":"), ensure_ascii=True)
    if (
        forbidden_extension in extensions_required
        or forbidden_extension in extensions_used
        or forbidden_extension in encoded_document
    ):
        raise RuntimeError(
            f"Exported GLB contains unsupported extension {forbidden_extension}"
        )
    return {
        "draco_mesh_compression_absent": True,
        "extensions_required": extensions_required,
        "extensions_used": extensions_used,
    }


def pack_save_export(root: bpy.types.Object) -> dict[str, object]:
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
        export_draco_mesh_compression_enable=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"glTF export failed: {result}")
    if not OUTPUT_GLB.is_file() or OUTPUT_GLB.stat().st_size == 0:
        raise RuntimeError("Bazaar GLB was not written")
    if OUTPUT_GLB.stat().st_size > MAX_GLB_BYTES:
        raise RuntimeError(f"Bazaar GLB exceeds {MAX_GLB_BYTES} bytes")
    if OUTPUT_BLEND.stat().st_size > MAX_BLEND_BYTES:
        raise RuntimeError(f"Bazaar blend exceeds {MAX_BLEND_BYTES} bytes")
    return validate_glb_extension_contract()


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


def validate_round_trip_v2(expected: dict[str, object]) -> dict[str, object]:
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
        "Bazaar_B_Balcony_Deck",
        "Bazaar_Mid_Mezzanine_Deck",
        "Bazaar_B_WarehouseRoof",
        *(stair.name for stair in STAIRS),
    }
    missing = sorted(required - names)
    if missing:
        raise RuntimeError(f"Round-trip missing Bazaar V2 objects: {missing}")
    if len(meshes) != int(expected["mesh_object_count"]):
        raise RuntimeError(
            f"Round-trip mesh count changed: expected {expected['mesh_object_count']}, "
            f"found {len(meshes)}"
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
        if str(obj.get("bazaar_asset_origin", "")) != "cc0":
            raise RuntimeError(f"Round-trip visible mesh lost CC0 origin: {obj.name}")
        if "CC0" not in str(obj.get("license", "")):
            raise RuntimeError(f"Round-trip visible mesh lost CC0 license: {obj.name}")
        for metadata_key in (
            "source_asset",
            "source_object",
            "source_creator",
            "source_url",
        ):
            if not str(obj.get(metadata_key, "")).strip():
                raise RuntimeError(
                    f"Round-trip provenance metadata {metadata_key} missing: {obj.name}"
                )

    minimum, maximum = object_world_bounds(meshes)
    expected_min = Vector(expected["blender_bounds_min"])
    expected_max = Vector(expected["blender_bounds_max"])
    if (minimum - expected_min).length > 0.02 or (maximum - expected_max).length > 0.02:
        raise RuntimeError(
            f"Round-trip bounds changed: {tuple(minimum)}..{tuple(maximum)} vs "
            f"{tuple(expected_min)}..{tuple(expected_max)}"
        )
    materials = {material for obj in meshes for material in obj.data.materials if material}
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
    unique_meshes = {obj.data for obj in meshes}
    instance_triangles = sum(mesh_triangles(obj.data) for obj in meshes)
    if instance_triangles != int(expected["instance_triangles"]):
        raise RuntimeError(
            "Round-trip triangle count changed: "
            f"expected {expected['instance_triangles']}, found {instance_triangles}"
        )
    return {
        "object_count": len(objects),
        "mesh_object_count": len(meshes),
        "unique_mesh_count": len(unique_meshes),
        "material_count": len(materials),
        "texture_count": len(images),
        "instance_triangles": instance_triangles,
        "bounds_min": [round(value, 4) for value in minimum],
        "bounds_max": [round(value, 4) for value in maximum],
        "v2_required_objects_preserved": True,
    }


def write_report(
    scene_stats: dict[str, object],
    round_trip: dict[str, object],
    glb_extension_gate: dict[str, object],
) -> dict[str, object]:
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
        "glb_extension_gate": glb_extension_gate,
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
    root, collections = build_map_v2(templates, materials)
    triangulate_visible_meshes(root)
    bpy.context.view_layer.update()
    if os.environ.get("BAZAAR_SKIP_PREVIEWS") != "1":
        render_previews(collections["review"])
    scene_stats = validate_authored_scene_v2(root)
    optimization = optimize_static_draw_nodes_v2(root, scene_stats)
    glb_extension_gate = pack_save_export(root)
    round_trip = validate_round_trip_v2(scene_stats)
    report = write_report(scene_stats, round_trip, glb_extension_gate)
    print(
        "BAZAAR_DCC_CHECK "
        f"draw_nodes_pre={optimization['pre_draw_nodes']} "
        f"draw_nodes={scene_stats['draw_node_count']} "
        f"surfaces_pre={optimization['pre_surface_draw_count']} "
        f"surfaces={scene_stats['surface_draw_count']} "
        f"raw_source_tris={scene_stats['raw_source_triangles']} "
        f"unique_tris_pre={optimization['pre_unique_triangles']} "
        f"unique_tris={scene_stats['unique_triangles']} "
        f"instance_tris_pre={optimization['pre_instance_triangles']} "
        f"instance_tris={scene_stats['instance_triangles']} "
        f"materials={scene_stats['material_count']} textures={scene_stats['texture_count']} "
        f"texture_mib_rgba8_mips={scene_stats['texture_memory_mib_estimate_rgba8_mips']} "
        f"glb_bytes={report['glb']['bytes']} blend_bytes={report['blend']['bytes']}"
    )
    print(
        "BAZAAR_DCC_PASS valid=True cc0_only=True interiors=4 platforms=3 "
        "dense_v2=True architectural_cover=True site_pair_block=True "
        "round_trip=True draco_absent=True"
    )


if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.stdout.flush()
        sys.stderr.flush()
        os._exit(2)
