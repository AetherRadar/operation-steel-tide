"""Assemble Trey Ramm's CC0 industrial modules into Godot-ready GLBs.

Run with Blender 4.5 or newer:
    blender --background --python scripts/blender/build_trey_modular_industrial.py

Pass ``-- --only arch-gateway loading-bay`` to rebuild selected assemblies.
Every visible mesh comes from the original Modular Industrial Pieces pack;
the script only places, names, and exports those authored modules.
"""

from __future__ import annotations

import argparse
import hashlib
import math
import sys
from dataclasses import dataclass
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = REPO_ROOT / "source_art" / "third_party" / "trey_modular_industrial"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "trey_modular_industrial"
PALETTE_PATH = SOURCE_DIR / "PacificNorthwestGradientAtlas.png"


@dataclass(frozen=True)
class Module:
    source: str
    location: tuple[float, float, float] = (0.0, 0.0, 0.0)
    yaw: float = 0.0
    scale: tuple[float, float, float] = (1.0, 1.0, 1.0)
    role: str = ""


@dataclass(frozen=True)
class Assembly:
    slug: str
    output_name: str
    root_name: str
    modules: tuple[Module, ...]


ASSEMBLIES = (
    Assembly(
        "east-security-gate",
        "east-security-gate.glb",
        "TreyIndustrialEastSecurityGate",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx"),
            Module("Meshes/Details/IndColumnFreeCap.fbx"),
            Module("Meshes/Details/IndColumnFree.fbx", (4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (4.2, 0.0, 0.0)),
        ),
    ),
    Assembly(
        "west-service-gate",
        "west-service-gate.glb",
        "TreyIndustrialWestServiceGate",
        (
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx"),
            Module("Meshes/Doors/IndGarageWhite.fbx"),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (4.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (4.2, 0.0, 0.0)),
        ),
    ),
    Assembly(
        "arch-gateway",
        "arch-gateway.glb",
        "TreyIndustrialArchGateway",
        (
            Module("Meshes/Walls/IndWallArchDouble.fbx"),
            Module("Meshes/Walls/IndWallArchDoubleColumns.fbx"),
            Module("Meshes/Walls/IndWallArchDoubleCapGrey.fbx", (0.0, 0.0, 3.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-2.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (-2.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (2.2, 0.0, 0.0)),
            Module("Meshes/Details/IndColumnFreeCap.fbx", (2.2, 0.0, 0.0)),
        ),
    ),
    Assembly(
        "loading-bay",
        "loading-bay.glb",
        "TreyIndustrialLoadingBay",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx"),
            Module("Meshes/Doors/IndGarageWhite.fbx"),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 3.0, 3.0)),
        ),
    ),
    Assembly(
        "elevated-walkway",
        "elevated-walkway.glb",
        "TreyIndustrialElevatedWalkway",
        (
            Module("Meshes/Details/IndColumnFree.fbx", (-3.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-3.0, 0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-1.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (-1.0, 0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (1.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (1.0, 0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (3.0, -0.75, 0.0)),
            Module("Meshes/Details/IndColumnFree.fbx", (3.0, 0.75, 0.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (-3.0, 0.0, 2.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (-1.0, 0.0, 2.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (1.0, 0.0, 2.0)),
            Module("Meshes/Floors/IndFloorGreyPlatformFull.fbx", (3.0, 0.0, 2.0)),
            Module("Meshes/Details/IndStairsWideFull.fbx", (-4.0, 0.0, 0.0), -90.0),
            Module("Meshes/Details/IndStairsWideFull.fbx", (4.0, 0.0, 0.0), 90.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-3.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-1.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (1.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (3.0, -1.0, 2.0)),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-3.0, 1.0, 2.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (-1.0, 1.0, 2.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (1.0, 1.0, 2.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimBStraightFull.fbx", (3.0, 1.0, 2.0), 180.0),
        ),
    ),
    Assembly(
        "window-hall",
        "window-hall.glb",
        "TreyIndustrialWindowHall",
        (
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 3.0, 3.0)),
        ),
    ),
    Assembly(
        "sawtooth-service-hall",
        "sawtooth-service-hall.glb",
        "TreyIndustrialServiceHall",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-3.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 3.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (3.0, 3.0, 3.0)),
        ),
    ),
    Assembly(
        "utility-office",
        "utility-office.glb",
        "TreyIndustrialUtilityOffice",
        (
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 2.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 2.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-2.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (2.0, 1.0, 0.0), -90.0),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (-1.0, 1.0, 3.0)),
            Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (1.0, 1.0, 3.0)),
            Module("Meshes/Trims/IndCornerTrimBFull.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Trims/IndCornerTrimBFull.fbx", (2.0, 0.0, 0.0), 90.0),
        ),
    ),
    Assembly(
        "reactor-annex",
        "reactor-annex.glb",
        "TreyIndustrialReactorAnnex",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (4.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (4.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-4.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-2.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (0.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (2.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 5.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (5.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (5.0, 3.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (5.0, 5.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
        ),
    ),
    Assembly(
        "shift-office",
        "shift-office.glb",
        "TreyIndustrialShiftOffice",
        (
            Module("Meshes/Windows/IndWindowBFull.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-2.0, 4.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 4.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 4.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 1.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 3.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 3.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0)
                for x in (-2.0, 0.0, 2.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0)
                for x in (-2.0, 0.0, 2.0)
            ),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (-2.0, 0.0, 3.0)),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (0.0, 0.0, 3.0)),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (2.0, 0.0, 3.0)),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (-2.0, 4.0, 3.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (0.0, 4.0, 3.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (2.0, 4.0, 3.0), 180.0),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (-3.0, 1.0, 3.0), -90.0),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (-3.0, 3.0, 3.0), -90.0),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (3.0, 1.0, 3.0), 90.0),
            Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (3.0, 3.0, 3.0), 90.0),
        ),
    ),
    Assembly(
        "turbine-workshop",
        "turbine-workshop.glb",
        "TreyIndustrialTurbineWorkshop",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 5.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 5.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module(
                    "Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx",
                    (x, y, 3.0),
                    0.0 if x in (-3.0, 1.0) else 180.0,
                )
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module(source, (ridge, y, 3.0), yaw)
                for y, yaw in ((0.0, 0.0), (6.0, 180.0))
                for ridge in (-2.0, 2.0)
                for source in (
                    "Meshes/Trims/IndRoofTrimAAngledL.fbx",
                    "Meshes/Trims/IndRoofTrimAAngledR.fbx",
                )
            ),
        ),
    ),
    Assembly(
        "compressor-house",
        "compressor-house.glb",
        "TreyIndustrialCompressorHouse",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 5.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 5.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module(source, (x, y, 0.0))
                for x, y in ((-4.0, 0.0), (4.0, 0.0), (-4.0, 6.0), (4.0, 6.0))
                for source in (
                    "Meshes/Details/IndColumnFree.fbx",
                    "Meshes/Details/IndColumnFreeCap.fbx",
                )
            ),
        ),
    ),
    Assembly(
        "inspection-office",
        "inspection-office.glb",
        "TreyIndustrialInspectionOffice",
        (
            Module("Meshes/Windows/IndWindowBFull.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-2.0, 4.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 4.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 4.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 1.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 1.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 3.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0)
                for x in (-2.0, 0.0, 2.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0)
                for x in (-2.0, 0.0, 2.0)
            ),
            Module(
                "Meshes/Roofs/IndRoofDarkGreyFull.fbx",
                (0.0, -0.6, 2.8),
                0.0,
                (1.0, 1.0, 1.0),
                "canopy",
            ),
            Module(
                "Meshes/Details/IndColumnFree.fbx",
                (-0.8, -1.3, 0.0),
                0.0,
                (1.0, 1.0, 0.9),
                "canopy",
            ),
            Module(
                "Meshes/Details/IndColumnFree.fbx",
                (0.8, -1.3, 0.0),
                0.0,
                (1.0, 1.0, 0.9),
                "canopy",
            ),
        ),
    ),
    Assembly(
        "boiler-workshop",
        "boiler-workshop.glb",
        "TreyIndustrialBoilerWorkshop",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (4.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (4.0, 0.0, 0.0)),
            *(
                Module("Meshes/Windows/IndWindowETopFull.fbx", (x, 0.0, 3.0))
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-4.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-2.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (2.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 6.0, 0.0), 180.0),
            *(
                Module("Meshes/Windows/IndWindowETopFull.fbx", (x, 6.0, 3.0), 180.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 1.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-5.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 5.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 1.0, 3.0), 90.0),
            Module("Meshes/Windows/IndWindowETopFull.fbx", (-5.0, 3.0, 3.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-5.0, 5.0, 3.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (5.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (5.0, 3.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (5.0, 5.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowETopFull.fbx", (5.0, 1.0, 3.0), -90.0),
            Module("Meshes/Windows/IndWindowETopFull.fbx", (5.0, 3.0, 3.0), -90.0),
            Module("Meshes/Windows/IndWindowETopFull.fbx", (5.0, 5.0, 3.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 6.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Trims/IndCornerTrimBFull.fbx", (x, y, z), yaw)
                for z in (0.0, 3.0)
                for x, y, yaw in (
                    (-5.0, 0.0, 0.0),
                    (5.0, 0.0, 90.0),
                    (-5.0, 6.0, -90.0),
                    (5.0, 6.0, 180.0),
                )
            ),
        ),
    ),
    Assembly(
        "switchgear-hall",
        "switchgear-hall.glb",
        "TreyIndustrialSwitchgearHall",
        (
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (1.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 1.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-4.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-4.0, 5.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (4.0, 3.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 5.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module(
                    "Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx",
                    (x, y, 3.0),
                    yaw,
                    (1.5, 1.0, 1.0),
                )
                for x in (-3.0, -1.0, 1.0, 3.0)
                for y, yaw in ((1.5, 90.0), (4.5, -90.0))
            ),
            *(
                Module(source, (x, 3.0, 3.0), yaw, (1.5, 1.0, 1.0))
                for x, yaw in ((-4.0, -90.0), (4.0, 90.0))
                for source in (
                    "Meshes/Trims/IndRoofTrimAAngledL.fbx",
                    "Meshes/Trims/IndRoofTrimAAngledR.fbx",
                )
            ),
        ),
    ),
    Assembly(
        "crew-canteen",
        "crew-canteen.glb",
        "TreyIndustrialCrewCanteen",
        (
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            *(
                Module("Meshes/Windows/IndWindowBFull.fbx", (x, 6.0, 0.0), 180.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowBFull.fbx", (-4.0, y, 0.0), 90.0)
                for y in (1.0, 3.0, 5.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, y, 0.0), -90.0)
                for y in (1.0, 3.0, 5.0)
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (x, y, 3.0), yaw)
                for y, yaw in ((0.0, 0.0), (6.0, 180.0))
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (x, y, 3.0), yaw)
                for x, yaw in ((-4.0, -90.0), (4.0, 90.0))
                for y in (1.0, 3.0, 5.0)
            ),
        ),
    ),
    Assembly(
        "pump-house",
        "pump-house.glb",
        "TreyIndustrialPumpHouse",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-2.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 6.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (2.0, 6.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 1.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 3.0, 0.0), 90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-3.0, 5.0, 0.0), 90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 1.0, 0.0), -90.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 3.0, 0.0), -90.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 5.0, 0.0), -90.0),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-2.0, 0.0, 2.0)
            ),
            *(
                Module(
                    "Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx",
                    (x, y, 3.0),
                    yaw,
                    (1.5, 1.0, 1.0),
                )
                for y in (1.0, 3.0, 5.0)
                for x, yaw in ((-1.5, 0.0), (1.5, 180.0))
            ),
            *(
                Module(source, (0.0, y, 3.0), yaw, (1.5, 1.0, 1.0))
                for y, yaw in ((0.0, 0.0), (6.0, 180.0))
                for source in (
                    "Meshes/Trims/IndRoofTrimAAngledL.fbx",
                    "Meshes/Trims/IndRoofTrimAAngledR.fbx",
                )
            ),
        ),
    ),
    Assembly(
        "transformer-works",
        "transformer-works.glb",
        "TreyIndustrialTransformerWorks",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-4.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-4.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (5.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (5.0, 0.0, 0.0)),
            *(
                Module(source, (x, 8.0, 0.0), 180.0)
                for x, source in zip(
                    (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (-6.0, y, 0.0), 90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (6.0, y, 0.0), -90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
            ),
            *(
                Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (x, y, 3.0), yaw)
                for y, yaw in ((0.0, 0.0), (8.0, 180.0))
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
            ),
            *(
                Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (x, y, 3.0), yaw)
                for x, yaw in ((-6.0, -90.0), (6.0, 90.0))
                for y in (1.0, 3.0, 5.0, 7.0)
            ),
            *(
                Module(source, (x, y, 0.0))
                for x, y in ((-6.0, 0.0), (6.0, 0.0), (-6.0, 8.0), (6.0, 8.0))
                for source in (
                    "Meshes/Details/IndColumnFree.fbx",
                    "Meshes/Details/IndColumnFreeCap.fbx",
                )
            ),
        ),
    ),
    Assembly(
        "glassworks-office",
        "glassworks-office.glb",
        "TreyIndustrialGlassworksOffice",
        (
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (-4.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (-2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (4.0, 0.0, 0.0)),
            *(
                Module("Meshes/Windows/IndWindowBFull.fbx", (x, 8.0, 0.0), 180.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowEBottomFull.fbx", (-5.0, y, 0.0), 90.0)
                for y in (1.0, 3.0, 5.0, 7.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowBFull.fbx", (5.0, y, 0.0), -90.0)
                for y in (1.0, 3.0, 5.0, 7.0)
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 3.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (x, y, 3.0), yaw)
                for y, yaw in ((0.0, 0.0), (8.0, 180.0))
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module("Meshes/Trims/IndRoofTrimAStraight.fbx", (x, y, 3.0), yaw)
                for x, yaw in ((-5.0, -90.0), (5.0, 90.0))
                for y in (1.0, 3.0, 5.0, 7.0)
            ),
            Module(
                "Meshes/Roofs/IndRoofDarkGreyFull.fbx",
                (0.0, -0.6, 2.8),
                0.0,
                (1.5, 1.0, 1.0),
                "canopy",
            ),
            Module(
                "Meshes/Details/IndColumnFree.fbx",
                (-0.9, -1.3, 0.0),
                0.0,
                (1.0, 1.0, 0.9),
                "canopy",
            ),
            Module(
                "Meshes/Details/IndColumnFree.fbx",
                (0.9, -1.3, 0.0),
                0.0,
                (1.0, 1.0, 0.9),
                "canopy",
            ),
        ),
    ),
    Assembly(
        "cooling-service-hall",
        "cooling-service-hall.glb",
        "TreyIndustrialCoolingServiceHall",
        (
            *(
                Module(source, (x, 0.0, 0.0))
                for x in (-4.0, 0.0, 4.0)
                for source in (
                    "Meshes/Doors/IndGarageArchWhite.fbx",
                    "Meshes/Doors/IndGarageWhite.fbx",
                )
            ),
            *(
                Module(source, (x, 8.0, 0.0), 180.0)
                for x, source in zip(
                    (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (-6.0, y, 0.0), 90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (6.0, y, 0.0), -90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
            ),
            *(
                Module(
                    "Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx",
                    (x, y, 3.0),
                    yaw,
                    (2.0, 1.0, 1.0),
                )
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
                for y, yaw in ((2.0, 90.0), (6.0, -90.0))
            ),
            *(
                Module(source, (x, 4.0, 3.0), yaw, (2.0, 1.0, 1.0))
                for x, yaw in ((-6.0, -90.0), (6.0, 90.0))
                for source in (
                    "Meshes/Trims/IndRoofTrimAAngledL.fbx",
                    "Meshes/Trims/IndRoofTrimAAngledR.fbx",
                )
            ),
            Module(
                "Meshes/Walls/IndWallFull.fbx",
                (-6.0, 4.0, 3.0),
                90.0,
                (4.0, 1.0, 0.4666666667),
                "gable_infill",
            ),
            Module(
                "Meshes/Walls/IndWallFull.fbx",
                (6.0, 4.0, 3.0),
                -90.0,
                (4.0, 1.0, 0.4666666667),
                "gable_infill",
            ),
        ),
    ),
    Assembly(
        "control-room",
        "control-room.glb",
        "TreyIndustrialControlRoom",
        (
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (-1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (1.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (-3.0, 8.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (-1.0, 8.0, 0.0), 180.0),
            Module("Meshes/Windows/IndWindowBFull.fbx", (1.0, 8.0, 0.0), 180.0),
            Module("Meshes/Walls/IndWallFull.fbx", (3.0, 8.0, 0.0), 180.0),
            *(
                Module(source, (-4.0, y, 0.0), 90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (4.0, y, 0.0), -90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                    ),
                )
            ),
            *(
                Module("Meshes/Windows/IndWindowETopFull.fbx", (x, 0.0, 3.0))
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowETopFull.fbx", (x, 8.0, 3.0), 180.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowETopFull.fbx", (-4.0, y, 3.0), 90.0)
                for y in (1.0, 3.0, 5.0, 7.0)
            ),
            *(
                Module("Meshes/Windows/IndWindowETopFull.fbx", (4.0, y, 3.0), -90.0)
                for y in (1.0, 3.0, 5.0, 7.0)
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Roofs/IndRoofDarkGreyFull.fbx", (x, y, 6.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-3.0, -1.0, 1.0, 3.0)
            ),
            *(
                Module("Meshes/Trims/IndCornerTrimBFull.fbx", (x, y, z), yaw)
                for z in (0.0, 3.0)
                for x, y, yaw in (
                    (-4.0, 0.0, 0.0),
                    (4.0, 0.0, 90.0),
                    (-4.0, 8.0, -90.0),
                    (4.0, 8.0, 180.0),
                )
            ),
        ),
    ),
    Assembly(
        "maintenance-depot",
        "maintenance-depot.glb",
        "TreyIndustrialMaintenanceDepot",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-3.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (2.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowBFull.fbx", (4.0, 0.0, 0.0)),
            *(
                Module(source, (x, 6.0, 0.0), 180.0)
                for x, source in zip(
                    (-4.0, -2.0, 0.0, 2.0, 4.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (-5.0, y, 0.0), 90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (5.0, y, 0.0), -90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0),
                    (
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0)
                for x in (-4.0, -2.0, 0.0, 2.0, 4.0)
            ),
            *(
                Module(
                    "Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx",
                    (x, y, 3.0),
                    yaw,
                    (2.5, 1.0, 1.0),
                )
                for y in (1.0, 3.0, 5.0)
                for x, yaw in ((-2.5, 0.0), (2.5, 180.0))
            ),
            *(
                Module(source, (0.0, y, 3.0), yaw, (2.5, 1.0, 1.0))
                for y, yaw in ((0.0, 0.0), (6.0, 180.0))
                for source in (
                    "Meshes/Trims/IndRoofTrimAAngledL.fbx",
                    "Meshes/Trims/IndRoofTrimAAngledR.fbx",
                )
            ),
            Module(
                "Meshes/Walls/IndWallFull.fbx",
                (0.0, 0.0, 3.0),
                0.0,
                (5.0, 1.0, 0.4666666667),
                "gable_infill",
            ),
            Module(
                "Meshes/Walls/IndWallFull.fbx",
                (0.0, 6.0, 3.0),
                180.0,
                (5.0, 1.0, 0.4666666667),
                "gable_infill",
            ),
        ),
    ),
    Assembly(
        "foundry-warehouse",
        "foundry-warehouse.glb",
        "TreyIndustrialFoundryWarehouse",
        (
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (-4.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (-4.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageArchWhite.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndGarageWhite.fbx", (0.0, 0.0, 0.0)),
            Module("Meshes/Windows/IndWindowEBottomFull.fbx", (3.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorFrameSingle.fbx", (5.0, 0.0, 0.0)),
            Module("Meshes/Doors/IndDoorSingleRed.fbx", (5.0, 0.0, 0.0)),
            *(
                Module(source, (x, 8.0, 0.0), 180.0)
                for x, source in zip(
                    (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0),
                    (
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowEBottomFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (-6.0, y, 0.0), 90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                    ),
                )
            ),
            *(
                Module(source, (6.0, y, 0.0), -90.0)
                for y, source in zip(
                    (1.0, 3.0, 5.0, 7.0),
                    (
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                        "Meshes/Windows/IndWindowBFull.fbx",
                        "Meshes/Walls/IndWallFull.fbx",
                    ),
                )
            ),
            *(
                Module("Meshes/Floors/IndFloorGreyFull.fbx", (x, y, 0.0))
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
            ),
            *(
                Module(
                    "Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx",
                    (x, y, 3.0),
                    0.0 if x in (-5.0, -1.0, 3.0) else 180.0,
                )
                for y in (1.0, 3.0, 5.0, 7.0)
                for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0)
            ),
            *(
                Module(source, (ridge, y, 3.0), yaw)
                for y, yaw in ((0.0, 0.0), (8.0, 180.0))
                for ridge in (-4.0, 0.0, 4.0)
                for source in (
                    "Meshes/Trims/IndRoofTrimAAngledL.fbx",
                    "Meshes/Trims/IndRoofTrimAAngledR.fbx",
                )
            ),
            *(
                Module(
                    "Meshes/Walls/IndWallFull.fbx",
                    (ridge, y, 3.0),
                    yaw,
                    (2.0, 1.0, 0.4666666667),
                    "gable_infill",
                )
                for y, yaw in ((0.0, 0.0), (8.0, 180.0))
                for ridge in (-4.0, 0.0, 4.0)
            ),
        ),
    ),
)


NEW_CLOSED_BUILDING_REQUIREMENTS = {
    "reactor-annex": {
        "footprint": (10.0, 6.0),
        "floor_modules": 15,
        "roof_modules": 15,
        "window_modules": 6,
        "door_modules": 4,
        "roof_style": "flat",
    },
    "shift-office": {
        "footprint": (6.0, 4.0),
        "floor_modules": 6,
        "roof_modules": 6,
        "window_modules": 7,
        "door_modules": 2,
        "roof_style": "corniced-flat",
    },
    "turbine-workshop": {
        "footprint": (8.0, 6.0),
        "floor_modules": 12,
        "roof_modules": 12,
        "window_modules": 6,
        "door_modules": 4,
        "roof_style": "twin-gable",
    },
    "compressor-house": {
        "footprint": (8.0, 6.0),
        "floor_modules": 12,
        "roof_modules": 12,
        "window_modules": 4,
        "door_modules": 4,
        "roof_style": "reinforced-flat",
    },
    "inspection-office": {
        "footprint": (6.0, 4.0),
        "floor_modules": 6,
        "roof_modules": 6,
        "window_modules": 7,
        "door_modules": 2,
        "roof_style": "canopied-flat",
    },
    "boiler-workshop": {
        "footprint": (10.0, 6.0),
        "floor_modules": 15,
        "roof_modules": 15,
        "window_modules": 22,
        "door_modules": 4,
        "roof_style": "tall-flat",
        "storeys": 2,
    },
    "switchgear-hall": {
        "footprint": (8.0, 6.0),
        "floor_modules": 12,
        "roof_modules": 8,
        "window_modules": 6,
        "door_modules": 2,
        "roof_style": "transverse-gable",
    },
    "crew-canteen": {
        "footprint": (8.0, 6.0),
        "floor_modules": 12,
        "roof_modules": 12,
        "window_modules": 13,
        "door_modules": 2,
        "roof_style": "canteen-cornice",
    },
    "pump-house": {
        "footprint": (6.0, 6.0),
        "floor_modules": 9,
        "roof_modules": 6,
        "window_modules": 6,
        "door_modules": 4,
        "roof_style": "broad-gable",
    },
    "transformer-works": {
        "footprint": (12.0, 8.0),
        "floor_modules": 24,
        "roof_modules": 24,
        "window_modules": 8,
        "door_modules": 6,
        "roof_style": "reinforced-cornice",
    },
    "glassworks-office": {
        "footprint": (10.0, 8.0),
        "floor_modules": 20,
        "roof_modules": 20,
        "window_modules": 17,
        "door_modules": 2,
        "roof_style": "glazed-canopy-cornice",
    },
    "cooling-service-hall": {
        "footprint": (12.0, 8.0),
        "floor_modules": 24,
        "roof_modules": 12,
        "window_modules": 7,
        "door_modules": 6,
        "roof_style": "wide-transverse-gable",
        "gable_infills": 2,
    },
    "control-room": {
        "footprint": (8.0, 8.0),
        "floor_modules": 16,
        "roof_modules": 16,
        "window_modules": 25,
        "door_modules": 2,
        "roof_style": "two-storey-control",
        "storeys": 2,
    },
    "maintenance-depot": {
        "footprint": (10.0, 6.0),
        "floor_modules": 15,
        "roof_modules": 6,
        "window_modules": 8,
        "door_modules": 4,
        "roof_style": "depot-gable",
        "gable_infills": 2,
    },
    "foundry-warehouse": {
        "footprint": (12.0, 8.0),
        "floor_modules": 24,
        "roof_modules": 24,
        "window_modules": 8,
        "door_modules": 6,
        "roof_style": "triple-gable",
        "gable_infills": 6,
    },
}


def validate_new_assembly_definitions() -> None:
    new_assemblies = [
        assembly for assembly in ASSEMBLIES if assembly.slug in NEW_CLOSED_BUILDING_REQUIREMENTS
    ]
    if {assembly.slug for assembly in new_assemblies} != set(NEW_CLOSED_BUILDING_REQUIREMENTS):
        raise RuntimeError("The Tideglass closed-building definitions are incomplete")
    if len({assembly.output_name for assembly in ASSEMBLIES}) != len(ASSEMBLIES):
        raise RuntimeError("Trey industrial output filenames must be unique")

    signatures: set[tuple[Module, ...]] = set()
    for assembly in new_assemblies:
        requirement = NEW_CLOSED_BUILDING_REQUIREMENTS[assembly.slug]
        signature = tuple(
            sorted(
                assembly.modules,
                key=lambda module: (
                    module.source,
                    module.location,
                    module.yaw,
                    module.scale,
                    module.role,
                ),
            )
        )
        if signature in signatures:
            raise RuntimeError(f"{assembly.slug} duplicates another annex composition")
        signatures.add(signature)

        floor_modules = [
            module for module in assembly.modules if "IndFloorGreyFull" in module.source
        ]
        roof_modules = [
            module
            for module in assembly.modules
            if "/Roofs/IndRoofDarkGrey" in module.source.replace("\\", "/")
            and module.role != "canopy"
        ]
        window_modules = [module for module in assembly.modules if "/Windows/" in module.source]
        door_modules = [module for module in assembly.modules if "/Doors/" in module.source]
        actual_counts = {
            "floor_modules": len(floor_modules),
            "roof_modules": len(roof_modules),
            "window_modules": len(window_modules),
            "door_modules": len(door_modules),
        }
        for field, actual in actual_counts.items():
            if actual != requirement[field]:
                raise RuntimeError(
                    f"{assembly.slug} has {actual} {field}, expected {requirement[field]}"
                )

        floor_minimum = Vector(
            (
                min(module.location[0] - 1.0 for module in floor_modules),
                min(module.location[1] - 1.0 for module in floor_modules),
            )
        )
        floor_maximum = Vector(
            (
                max(module.location[0] + 1.0 for module in floor_modules),
                max(module.location[1] + 1.0 for module in floor_modules),
            )
        )
        footprint = floor_maximum - floor_minimum
        expected_footprint = requirement["footprint"]
        if (
            abs(footprint.x - expected_footprint[0]) > 0.001
            or abs(footprint.y - expected_footprint[1]) > 0.001
        ):
            raise RuntimeError(
                f"{assembly.slug} footprint is {tuple(footprint)}, expected {expected_footprint}"
            )

        roof_style = requirement["roof_style"]
        angled_roofs = [module for module in roof_modules if "Angled" in module.source]
        straight_trims = [
            module for module in assembly.modules if "IndRoofTrimAStraight" in module.source
        ]
        angled_trims = [
            module for module in assembly.modules if "IndRoofTrimAAngled" in module.source
        ]
        columns = [
            module for module in assembly.modules if module.source.endswith("IndColumnFree.fbx")
        ]
        column_caps = [
            module for module in assembly.modules if module.source.endswith("IndColumnFreeCap.fbx")
        ]
        corner_trims = [
            module for module in assembly.modules if module.source.endswith("IndCornerTrimBFull.fbx")
        ]
        canopy_modules = [module for module in assembly.modules if module.role == "canopy"]
        gable_infills = [
            module for module in assembly.modules if module.role == "gable_infill"
        ]
        bottom_windows = [
            module for module in window_modules if "IndWindowEBottomFull" in module.source
        ]
        top_windows = [
            module for module in window_modules if "IndWindowETopFull" in module.source
        ]
        style_valid = (
            (roof_style == "flat" and not angled_roofs and not straight_trims and not angled_trims)
            or (
                roof_style == "corniced-flat"
                and not angled_roofs
                and len(straight_trims) == 10
                and not angled_trims
            )
            or (
                roof_style == "twin-gable"
                and len(angled_roofs) == len(roof_modules)
                and not straight_trims
                and len(angled_trims) == 8
            )
            or (
                roof_style == "reinforced-flat"
                and not angled_roofs
                and not straight_trims
                and not angled_trims
                and len(columns) == 4
                and len(column_caps) == 4
            )
            or (
                roof_style == "canopied-flat"
                and not angled_roofs
                and not straight_trims
                and not angled_trims
                and len(canopy_modules) == 3
                and len([module for module in canopy_modules if "/Roofs/" in module.source]) == 1
                and len([module for module in canopy_modules if "IndColumnFree" in module.source])
                == 2
            )
            or (
                roof_style == "tall-flat"
                and not angled_roofs
                and not straight_trims
                and not angled_trims
                and len(corner_trims) == 8
                and requirement.get("storeys") == 2
            )
            or (
                roof_style == "transverse-gable"
                and len(angled_roofs) == len(roof_modules)
                and not straight_trims
                and len(angled_trims) == 4
                and all(module.scale == (1.5, 1.0, 1.0) for module in roof_modules)
                and all(module.yaw in (-90.0, 90.0) for module in roof_modules)
                and all(module.scale == (1.5, 1.0, 1.0) for module in angled_trims)
            )
            or (
                roof_style == "canteen-cornice"
                and not angled_roofs
                and len(straight_trims) == 14
                and not angled_trims
            )
            or (
                roof_style == "broad-gable"
                and len(angled_roofs) == len(roof_modules)
                and not straight_trims
                and len(angled_trims) == 4
                and all(module.scale == (1.5, 1.0, 1.0) for module in roof_modules)
                and all(module.scale == (1.5, 1.0, 1.0) for module in angled_trims)
            )
            or (
                roof_style == "reinforced-cornice"
                and not angled_roofs
                and len(straight_trims) == 20
                and not angled_trims
                and len(columns) == 4
                and len(column_caps) == 4
            )
            or (
                roof_style == "glazed-canopy-cornice"
                and not angled_roofs
                and len(straight_trims) == 18
                and not angled_trims
                and len(canopy_modules) == 3
                and len([module for module in canopy_modules if "/Roofs/" in module.source]) == 1
                and len([module for module in canopy_modules if "IndColumnFree" in module.source])
                == 2
                and len(bottom_windows) == 8
            )
            or (
                roof_style == "wide-transverse-gable"
                and len(angled_roofs) == len(roof_modules)
                and not straight_trims
                and len(angled_trims) == 4
                and all(module.scale == (2.0, 1.0, 1.0) for module in roof_modules)
                and all(module.yaw in (-90.0, 90.0) for module in roof_modules)
                and all(module.scale == (2.0, 1.0, 1.0) for module in angled_trims)
                and len(gable_infills) == 2
            )
            or (
                roof_style == "two-storey-control"
                and not angled_roofs
                and not straight_trims
                and not angled_trims
                and len(corner_trims) == 8
                and requirement.get("storeys") == 2
                and len(bottom_windows) == 7
                and len(top_windows) == 16
            )
            or (
                roof_style == "depot-gable"
                and len(angled_roofs) == len(roof_modules)
                and not straight_trims
                and len(angled_trims) == 4
                and all(module.scale == (2.5, 1.0, 1.0) for module in roof_modules)
                and all(module.scale == (2.5, 1.0, 1.0) for module in angled_trims)
                and len(gable_infills) == 2
            )
            or (
                roof_style == "triple-gable"
                and len(angled_roofs) == len(roof_modules)
                and not straight_trims
                and len(angled_trims) == 12
                and all(module.scale == (1.0, 1.0, 1.0) for module in roof_modules)
                and all(module.scale == (1.0, 1.0, 1.0) for module in angled_trims)
                and len(gable_infills) == 6
            )
        )
        if not style_valid:
            raise RuntimeError(f"{assembly.slug} does not satisfy its {roof_style} roof contract")


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def parse_args() -> set[str]:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--only",
        nargs="+",
        choices=[assembly.slug for assembly in ASSEMBLIES],
        help="Rebuild only the listed assembly slugs.",
    )
    blender_args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(blender_args)
    return set(args.only or [assembly.slug for assembly in ASSEMBLIES])


def require_sources() -> None:
    missing = sorted(
        {
            module.source
            for assembly in ASSEMBLIES
            for module in assembly.modules
            if not (SOURCE_DIR / module.source).is_file()
        }
    )
    if not PALETTE_PATH.is_file():
        missing.append(PALETTE_PATH.name)
    if missing:
        raise FileNotFoundError(f"Missing Trey source files: {', '.join(missing)}")


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.materials,
        bpy.data.images,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def shape_authored_gable_infill(obj: bpy.types.Object) -> None:
    if obj.type != "MESH":
        return

    world_vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    if len(world_vertices) != 4:
        raise RuntimeError(
            f"Gable infill source {obj.name} must be the authored four-corner wall panel"
        )
    minimum_z = min(vertex.z for vertex in world_vertices)
    maximum_z = max(vertex.z for vertex in world_vertices)
    top_indices = [
        index
        for index, vertex in enumerate(world_vertices)
        if abs(vertex.z - maximum_z) <= 0.001
    ]
    bottom_indices = [
        index
        for index, vertex in enumerate(world_vertices)
        if abs(vertex.z - minimum_z) <= 0.001
    ]
    if len(top_indices) != 2 or len(bottom_indices) != 2 or maximum_z - minimum_z < 1.0:
        raise RuntimeError(f"Gable infill source {obj.name} has an unexpected wall profile")

    span_x = max(vertex.x for vertex in world_vertices) - min(
        vertex.x for vertex in world_vertices
    )
    span_y = max(vertex.y for vertex in world_vertices) - min(
        vertex.y for vertex in world_vertices
    )
    ridge_axis = 0 if span_x >= span_y else 1
    ridge_coordinate = sum(world_vertices[index][ridge_axis] for index in top_indices) * 0.5
    inverse = obj.matrix_world.inverted()
    for index in top_indices:
        vertex = world_vertices[index].copy()
        vertex[ridge_axis] = ridge_coordinate
        obj.data.vertices[index].co = inverse @ vertex

    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=0.0001)
    mesh.normal_update()
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.validate(verbose=True)
    obj.data.update()
    obj["dcc_adaptation"] = "authored-wall-triangular-gable-infill"

    obj.data.calc_loop_triangles()
    if len(obj.data.vertices) != 3 or len(obj.data.loop_triangles) != 1:
        raise RuntimeError(
            f"Gable infill {obj.name} did not become one authored triangular facade"
        )


def import_module(module: Module, index: int) -> list[bpy.types.Object]:
    path = SOURCE_DIR / module.source
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.fbx(
        filepath=str(path),
        global_scale=1.0,
        use_manual_orientation=False,
        bake_space_transform=False,
        use_image_search=True,
        use_anim=False,
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {module.source}: {result}")

    imported = [obj for obj in bpy.data.objects if obj not in before]
    for obj in list(imported):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)
            imported.remove(obj)
    if not any(obj.type == "MESH" for obj in imported):
        raise RuntimeError(f"No mesh objects were imported from {module.source}")

    transform = Matrix.Translation(Vector(module.location)) @ Matrix.Rotation(
        math.radians(module.yaw), 4, "Z"
    )
    if module.scale != (1.0, 1.0, 1.0):
        transform @= Matrix.Diagonal((*module.scale, 1.0))
    imported_set = set(imported)
    for obj in imported:
        obj.name = f"Part_{index:02d}_{path.stem}_{obj.name}"
        if module.role:
            obj["assembly_role"] = module.role
    for obj in [candidate for candidate in imported if candidate.parent not in imported_set]:
        obj.matrix_world = transform @ obj.matrix_world
    if module.role == "gable_infill":
        bpy.context.view_layer.update()
        for obj in imported:
            shape_authored_gable_infill(obj)
    return imported


def load_palette() -> bpy.types.Image:
    image = bpy.data.images.get(PALETTE_PATH.name)
    if image is None:
        image = bpy.data.images.load(str(PALETTE_PATH), check_existing=True)
    image.name = PALETTE_PATH.name
    image.filepath = str(PALETTE_PATH)
    image.colorspace_settings.name = "sRGB"
    return image


def ensure_palette_materials(objects: list[bpy.types.Object], palette: bpy.types.Image) -> None:
    materials = {
        material
        for obj in objects
        if obj.type == "MESH"
        for material in obj.data.materials
        if material is not None
    }
    if not materials:
        fallback = bpy.data.materials.new("TreyIndustrialPalette")
        materials.add(fallback)
        for obj in objects:
            if obj.type == "MESH":
                obj.data.materials.append(fallback)

    for index, material in enumerate(sorted(materials, key=lambda item: item.name)):
        material.name = f"TreyIndustrialPalette_{index + 1:02d}"
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        principled = nodes.get("Principled BSDF")
        if principled is None:
            principled = nodes.new("ShaderNodeBsdfPrincipled")
            output = nodes.get("Material Output") or nodes.new("ShaderNodeOutputMaterial")
            links.new(principled.outputs["BSDF"], output.inputs["Surface"])

        texture_nodes = [node for node in nodes if node.type == "TEX_IMAGE"]
        texture = texture_nodes[0] if texture_nodes else nodes.new("ShaderNodeTexImage")
        texture.name = "TreyIndustrialPaletteTexture"
        texture.label = "CC0 Pacific Northwest Gradient Atlas"
        texture.image = palette
        texture.interpolation = "Closest"
        if not any(link.to_node == principled and link.to_socket.name == "Base Color" for link in links):
            links.new(texture.outputs["Color"], principled.inputs["Base Color"])
        principled.inputs["Roughness"].default_value = 0.68
        emission = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        if emission is not None:
            emission.default_value = (0.0, 0.0, 0.0, 1.0)
        emission_strength = principled.inputs.get("Emission Strength")
        if emission_strength is not None:
            emission_strength.default_value = 0.0


def mesh_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type == "MESH"
        for corner in obj.bound_box
    ]
    if not corners:
        raise RuntimeError("Cannot calculate bounds without meshes")
    minimum = Vector(
        (min(point.x for point in corners), min(point.y for point in corners), min(point.z for point in corners))
    )
    maximum = Vector(
        (max(point.x for point in corners), max(point.y for point in corners), max(point.z for point in corners))
    )
    return minimum, maximum


def object_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(
            (
                min(point.x for point in corners),
                min(point.y for point in corners),
                min(point.z for point in corners),
            )
        ),
        Vector(
            (
                max(point.x for point in corners),
                max(point.y for point in corners),
                max(point.z for point in corners),
            )
        ),
    )


def world_mesh_bvh(obj: bpy.types.Object) -> tuple[BVHTree, list[Vector]]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    triangles = [tuple(triangle.vertices) for triangle in mesh.loop_triangles]
    if not vertices or not triangles:
        raise RuntimeError(f"Cannot calculate surface distance for {obj.name}")
    return BVHTree.FromPolygons(vertices, triangles, all_triangles=True), vertices


def mesh_surface_distance(first: bpy.types.Object, second: bpy.types.Object) -> float:
    first_bvh, first_vertices = world_mesh_bvh(first)
    second_bvh, second_vertices = world_mesh_bvh(second)
    if first_bvh.overlap(second_bvh):
        return 0.0

    minimum = math.inf
    for point in first_vertices:
        nearest = second_bvh.find_nearest(point)
        if nearest is not None:
            minimum = min(minimum, nearest[3])
    for point in second_vertices:
        nearest = first_bvh.find_nearest(point)
        if nearest is not None:
            minimum = min(minimum, nearest[3])
    return minimum


def intervals_cover(
    intervals: list[tuple[float, float]],
    expected_start: float,
    expected_end: float,
    tolerance: float = 0.18,
) -> bool:
    if not intervals:
        return False

    merged_end = expected_start
    for start, end in sorted(intervals):
        if end < merged_end - tolerance:
            continue
        if start > merged_end + tolerance:
            return False
        merged_end = max(merged_end, end)
        if merged_end >= expected_end - tolerance:
            return True
    return merged_end >= expected_end - tolerance


def validate_closed_building_perimeter(
    objects: list[bpy.types.Object], assembly: Assembly
) -> None:
    if assembly.slug not in {
        "loading-bay",
        "window-hall",
        "sawtooth-service-hall",
        "utility-office",
    } and assembly.slug not in NEW_CLOSED_BUILDING_REQUIREMENTS:
        return

    roofs = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and "IndRoofDarkGrey" in obj.name
        and obj.get("assembly_role") != "canopy"
    ]
    perimeter = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and any(
            marker in obj.name
            for marker in ("IndWall", "IndWindow", "IndDoor", "IndGarage")
        )
    ]
    if not roofs or not perimeter:
        raise RuntimeError(f"{assembly.slug} is missing roof or perimeter meshes")

    roof_minimum, roof_maximum = mesh_bounds(roofs)
    wall_bounds = [(obj.name, *object_bounds(obj)) for obj in perimeter]
    plane_tolerance = 0.35
    sides = {
        "front": (
            [
                (minimum.x, maximum.x)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.y + maximum.y) * 0.5 - roof_minimum.y) <= plane_tolerance
            ],
            roof_minimum.x,
            roof_maximum.x,
        ),
        "back": (
            [
                (minimum.x, maximum.x)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.y + maximum.y) * 0.5 - roof_maximum.y) <= plane_tolerance
            ],
            roof_minimum.x,
            roof_maximum.x,
        ),
        "left": (
            [
                (minimum.y, maximum.y)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.x + maximum.x) * 0.5 - roof_minimum.x) <= plane_tolerance
            ],
            roof_minimum.y,
            roof_maximum.y,
        ),
        "right": (
            [
                (minimum.y, maximum.y)
                for _, minimum, maximum in wall_bounds
                if abs((minimum.x + maximum.x) * 0.5 - roof_maximum.x) <= plane_tolerance
            ],
            roof_minimum.y,
            roof_maximum.y,
        ),
    }
    for side, (intervals, expected_start, expected_end) in sides.items():
        if not intervals_cover(intervals, expected_start, expected_end):
            raise RuntimeError(
                f"{assembly.slug} has an open {side} perimeter: "
                f"expected={expected_start:.3f}..{expected_end:.3f} intervals={intervals}"
            )


def xy_bounds_cover(
    objects: list[bpy.types.Object],
    minimum: Vector,
    maximum: Vector,
    sample_step: float = 0.5,
) -> bool:
    bounds = [object_bounds(obj) for obj in objects]
    x = minimum.x + sample_step * 0.5
    while x < maximum.x:
        y = minimum.y + sample_step * 0.5
        while y < maximum.y:
            if not any(
                obj_minimum.x - 0.005 <= x <= obj_maximum.x + 0.005
                and obj_minimum.y - 0.005 <= y <= obj_maximum.y + 0.005
                for obj_minimum, obj_maximum in bounds
            ):
                return False
            y += sample_step
        x += sample_step
    return True


def validate_new_building_shell(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    requirement = NEW_CLOSED_BUILDING_REQUIREMENTS.get(assembly.slug)
    if requirement is None:
        return

    floors = [
        obj for obj in objects if obj.type == "MESH" and "IndFloorGreyFull" in obj.name
    ]
    roofs = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and "IndRoofDarkGrey" in obj.name
        and obj.get("assembly_role") != "canopy"
    ]
    perimeter = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and obj.get("assembly_role") != "gable_infill"
        and any(marker in obj.name for marker in ("IndWall", "IndWindow", "IndDoor", "IndGarage"))
    ]
    windows = [obj for obj in perimeter if "IndWindow" in obj.name]
    entrances = [
        obj
        for obj in perimeter
        if any(marker in obj.name for marker in ("IndDoorSingle", "IndGarageWhite"))
    ]
    if not floors or not roofs or not perimeter or not windows or not entrances:
        raise RuntimeError(f"{assembly.slug} lacks a visible floor, facade, entrance, window, or roof")

    floor_minimum, floor_maximum = mesh_bounds(floors)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    wall_minimum, wall_maximum = mesh_bounds(perimeter)
    footprint = requirement["footprint"]
    storeys = int(requirement.get("storeys", 1))
    horizontal_ready = (
        abs((floor_maximum.x - floor_minimum.x) - footprint[0]) <= 0.01
        and abs((floor_maximum.y - floor_minimum.y) - footprint[1]) <= 0.01
        and abs(roof_minimum.x - floor_minimum.x) <= 0.01
        and abs(roof_minimum.y - floor_minimum.y) <= 0.01
        and abs(roof_maximum.x - floor_maximum.x) <= 0.01
        and abs(roof_maximum.y - floor_maximum.y) <= 0.01
        and xy_bounds_cover(floors, floor_minimum, floor_maximum)
        and xy_bounds_cover(roofs, roof_minimum, roof_maximum)
    )
    vertical_ready = (
        abs(floor_minimum.z) <= 0.005
        and abs(floor_maximum.z - wall_minimum.z) <= 0.01
        and abs(wall_maximum.z - (floor_maximum.z + storeys * 3.0)) <= 0.01
        and roof_minimum.z >= wall_maximum.z - 0.12
        and roof_minimum.z <= wall_maximum.z + 0.01
    )
    if not horizontal_ready or not vertical_ready:
        raise RuntimeError(
            f"{assembly.slug} does not form a closed floor-wall-roof shell: "
            f"floor={tuple(floor_minimum)}..{tuple(floor_maximum)} "
            f"walls={tuple(wall_minimum)}..{tuple(wall_maximum)} "
            f"roof={tuple(roof_minimum)}..{tuple(roof_maximum)}"
        )

    plane_tolerance = 0.35
    detached_panels: list[str] = []
    for obj in perimeter:
        obj_minimum, obj_maximum = object_bounds(obj)
        center_x = (obj_minimum.x + obj_maximum.x) * 0.5
        center_y = (obj_minimum.y + obj_maximum.y) * 0.5
        if not any(
            (
                abs(center_x - floor_minimum.x) <= plane_tolerance,
                abs(center_x - floor_maximum.x) <= plane_tolerance,
                abs(center_y - floor_minimum.y) <= plane_tolerance,
                abs(center_y - floor_maximum.y) <= plane_tolerance,
            )
        ):
            detached_panels.append(obj.name)
    if detached_panels:
        raise RuntimeError(
            f"{assembly.slug} contains facade panels detached from the perimeter: {detached_panels}"
        )

    wall_bounds = [(obj.name, *object_bounds(obj)) for obj in perimeter]
    for level in range(storeys):
        level_bottom = floor_maximum.z + level * 3.0
        level_top = level_bottom + 3.0
        level_bounds = [
            (name, minimum, maximum)
            for name, minimum, maximum in wall_bounds
            if minimum.z <= level_bottom + 0.01 and maximum.z >= level_top - 0.01
        ]
        level_sides = {
            "front": (
                [
                    (minimum.x, maximum.x)
                    for _, minimum, maximum in level_bounds
                    if abs((minimum.y + maximum.y) * 0.5 - floor_minimum.y)
                    <= plane_tolerance
                ],
                floor_minimum.x,
                floor_maximum.x,
            ),
            "back": (
                [
                    (minimum.x, maximum.x)
                    for _, minimum, maximum in level_bounds
                    if abs((minimum.y + maximum.y) * 0.5 - floor_maximum.y)
                    <= plane_tolerance
                ],
                floor_minimum.x,
                floor_maximum.x,
            ),
            "left": (
                [
                    (minimum.y, maximum.y)
                    for _, minimum, maximum in level_bounds
                    if abs((minimum.x + maximum.x) * 0.5 - floor_minimum.x)
                    <= plane_tolerance
                ],
                floor_minimum.y,
                floor_maximum.y,
            ),
            "right": (
                [
                    (minimum.y, maximum.y)
                    for _, minimum, maximum in level_bounds
                    if abs((minimum.x + maximum.x) * 0.5 - floor_maximum.x)
                    <= plane_tolerance
                ],
                floor_minimum.y,
                floor_maximum.y,
            ),
        }
        for side, (intervals, expected_start, expected_end) in level_sides.items():
            if not intervals_cover(intervals, expected_start, expected_end):
                raise RuntimeError(
                    f"{assembly.slug} has an open level-{level + 1} {side} facade: "
                    f"expected={expected_start:.3f}..{expected_end:.3f} "
                    f"intervals={intervals}"
                )

    expected_gable_infills = int(requirement.get("gable_infills", 0))
    gable_infills = [
        obj
        for obj in objects
        if obj.type == "MESH" and obj.get("assembly_role") == "gable_infill"
    ]
    if len(gable_infills) != expected_gable_infills:
        raise RuntimeError(
            f"{assembly.slug} has {len(gable_infills)} gable infills, "
            f"expected {expected_gable_infills}"
        )
    wall_top = floor_maximum.z + storeys * 3.0
    roof_profile_objects = roofs + [
        obj
        for obj in objects
        if obj.type == "MESH" and "IndRoofTrimAAngled" in obj.name
    ]
    _, roof_profile_maximum = mesh_bounds(roof_profile_objects)
    for infill in gable_infills:
        infill_minimum, infill_maximum = object_bounds(infill)
        infill.data.calc_loop_triangles()
        center_x = (infill_minimum.x + infill_maximum.x) * 0.5
        center_y = (infill_minimum.y + infill_maximum.y) * 0.5
        on_shell_plane = any(
            (
                abs(center_x - floor_minimum.x) <= plane_tolerance,
                abs(center_x - floor_maximum.x) <= plane_tolerance,
                abs(center_y - floor_minimum.y) <= plane_tolerance,
                abs(center_y - floor_maximum.y) <= plane_tolerance,
            )
        )
        horizontal_span = max(
            infill_maximum.x - infill_minimum.x,
            infill_maximum.y - infill_minimum.y,
        )
        ready = (
            infill.get("dcc_adaptation") == "authored-wall-triangular-gable-infill"
            and len(infill.data.vertices) == 3
            and len(infill.data.loop_triangles) == 1
            and on_shell_plane
            and horizontal_span >= 3.9
            and abs(infill_minimum.z - wall_top) <= 0.01
            and abs(infill_maximum.z - roof_profile_maximum.z) <= 0.01
        )
        if not ready:
            raise RuntimeError(
                f"{assembly.slug} has an invalid authored gable infill {infill.name}: "
                f"bounds={tuple(infill_minimum)}..{tuple(infill_maximum)} "
                f"vertices={len(infill.data.vertices)} triangles={len(infill.data.loop_triangles)} "
                f"adaptation={infill.get('dcc_adaptation')} wall_top={wall_top:.3f} "
                f"roof_profile_top={roof_profile_maximum.z:.3f} on_shell={on_shell_plane}"
            )

    print(
        "TREY_CLOSED_SHELL_CHECK "
        f"slug={assembly.slug} footprint_m={footprint[0]:.1f}x{footprint[1]:.1f} "
        f"windows={requirement['window_modules']} entrances={len(entrances)} "
        f"storeys={storeys} roof_style={requirement['roof_style']} "
        f"gable_infills={len(gable_infills)}"
    )


def normalize_assembly(objects: list[bpy.types.Object], assembly: Assembly) -> bpy.types.Object:
    minimum, maximum = mesh_bounds(objects)
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
    translation = Matrix.Translation(offset)
    imported_set = set(objects)
    top_level = [obj for obj in objects if obj.parent not in imported_set]
    for obj in top_level:
        obj.matrix_world = translation @ obj.matrix_world

    root = bpy.data.objects.new(assembly.root_name, None)
    bpy.context.collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 1.0
    root["source_creator"] = "Trey Ramm / minime453"
    root["source_asset"] = "Modular Industrial Pieces"
    root["source_url"] = "https://opengameart.org/content/modular-industrial-kit"
    root["license"] = "CC0-1.0"
    root["assembly"] = assembly.slug
    root["units"] = "meters"
    for obj in top_level:
        world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = world
    bpy.context.view_layer.update()
    return root


def validate_dimensions(objects: list[bpy.types.Object], assembly: Assembly) -> Vector:
    minimum, maximum = mesh_bounds(objects)
    dimensions = maximum - minimum
    if min(dimensions) < 0.05:
        raise RuntimeError(f"{assembly.slug} has a collapsed dimension: {tuple(dimensions)}")
    if max(dimensions) > 80.0:
        raise RuntimeError(f"{assembly.slug} is not in plausible meter scale: {tuple(dimensions)}")
    if abs(minimum.z) > 0.002:
        raise RuntimeError(f"{assembly.slug} is not grounded at Z=0 (minimum Z={minimum.z:.6f})")
    return dimensions


def validate_arch_gateway_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "arch-gateway":
        return

    column_bodies = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and "IndColumnFree" in obj.name
        and "IndColumnFreeCap" not in obj.name
    ]
    column_caps = [
        obj for obj in objects if obj.type == "MESH" and "IndColumnFreeCap" in obj.name
    ]
    wall_caps = [
        obj for obj in objects if obj.type == "MESH" and "IndWallArchDoubleCapGrey" in obj.name
    ]
    if len(column_bodies) != 2 or len(column_caps) != 2 or len(wall_caps) != 1:
        raise RuntimeError(
            "arch-gateway must contain two free columns, two column caps, and one wall cap"
        )

    body_minimum, body_maximum = mesh_bounds(column_bodies)
    cap_minimum, cap_maximum = mesh_bounds(column_caps + wall_caps)
    if body_minimum.z < -0.005 or body_minimum.z > 0.15:
        raise RuntimeError(f"arch-gateway columns are not grounded: minimum={body_minimum.z:.3f}")
    if cap_minimum.z < body_maximum.z - 0.15 or cap_minimum.z > body_maximum.z + 0.15:
        raise RuntimeError(
            "arch-gateway caps do not meet the column tops: "
            f"column_top={body_maximum.z:.3f} cap_bottom={cap_minimum.z:.3f}"
        )
    if cap_maximum.z > body_maximum.z + 0.35:
        raise RuntimeError(
            "arch-gateway caps extend implausibly above the columns: "
            f"column_top={body_maximum.z:.3f} cap_top={cap_maximum.z:.3f}"
        )


def validate_elevated_walkway_parts(
    objects: list[bpy.types.Object], assembly: Assembly
) -> None:
    if assembly.slug != "elevated-walkway":
        return

    floors = [obj for obj in objects if obj.type == "MESH" and "IndFloor" in obj.name]
    stairs = [obj for obj in objects if obj.type == "MESH" and "IndStairs" in obj.name]
    pillars = [
        obj
        for obj in objects
        if obj.type == "MESH"
        and "IndColumnFree" in obj.name
        and "IndColumnFreeCap" not in obj.name
    ]
    rails = [obj for obj in objects if obj.type == "MESH" and "IndRoofTrim" in obj.name]
    if len(floors) != 4 or any("IndFloorGreyPlatformFull" not in floor.name for floor in floors):
        raise RuntimeError("elevated-walkway must use four complete platform modules")
    if len(stairs) != 2 or any("IndStairsWideFull" not in stair.name for stair in stairs):
        raise RuntimeError("elevated-walkway must use two straight wide stair modules")
    if len(pillars) != 8 or len(rails) != 8:
        raise RuntimeError(
            "elevated-walkway must contain eight pillars and eight authored rail modules"
        )

    floor_minimum, floor_maximum = mesh_bounds(floors)
    stair_bounds = sorted(
        (object_bounds(stair) for stair in stairs), key=lambda bounds: bounds[0].x
    )
    left_minimum, left_maximum = stair_bounds[0]
    right_minimum, right_maximum = stair_bounds[1]
    rail_minimum, rail_maximum = mesh_bounds(rails)
    tolerance = 0.01
    ready = (
        abs(left_maximum.x - floor_minimum.x) <= tolerance
        and abs(right_minimum.x - floor_maximum.x) <= tolerance
        and abs(left_minimum.x + right_maximum.x) <= tolerance
        and abs(left_maximum.x + right_minimum.x) <= tolerance
        and abs(left_minimum.y - floor_minimum.y) <= tolerance
        and abs(left_maximum.y - floor_maximum.y) <= tolerance
        and abs(right_minimum.y - floor_minimum.y) <= tolerance
        and abs(right_maximum.y - floor_maximum.y) <= tolerance
        and abs(left_maximum.z - floor_maximum.z) <= tolerance
        and abs(right_maximum.z - floor_maximum.z) <= tolerance
        and abs(rail_minimum.y + rail_maximum.y) <= tolerance
    )
    if not ready:
        raise RuntimeError(
            "elevated-walkway stairs do not meet the platform symmetrically: "
            f"floor={tuple(floor_minimum)}..{tuple(floor_maximum)} "
            f"left={tuple(left_minimum)}..{tuple(left_maximum)} "
            f"right={tuple(right_minimum)}..{tuple(right_maximum)}"
        )

    rail_gaps = [
        (rail.name, min(mesh_surface_distance(rail, support) for support in floors + pillars))
        for rail in rails
    ]
    maximum_rail_gap = max(gap for _, gap in rail_gaps)
    if maximum_rail_gap > tolerance:
        raise RuntimeError(
            "elevated-walkway rails are detached from the deck structure: "
            f"gaps={rail_gaps}"
        )
    print(
        "TREY_WALKWAY_CHECK "
        f"rails={len(rail_gaps)} maximum_attachment_gap_m={maximum_rail_gap:.6f}"
    )


def validate_utility_office_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "utility-office":
        return

    windows = [obj for obj in objects if obj.type == "MESH" and "IndWindow" in obj.name]
    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    if len(windows) != 1 or "IndWindowBFull" not in windows[0].name or len(roofs) != 2:
        raise RuntimeError("utility-office must use one single-storey window and two roof modules")

    _, office_maximum = mesh_bounds(objects)
    window_minimum, window_maximum = mesh_bounds(windows)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    if office_maximum.z > 3.25:
        raise RuntimeError(f"utility-office exceeds one storey: maximum={office_maximum.z:.3f}")
    if window_minimum.z < -0.005 or window_maximum.z > 3.15:
        raise RuntimeError(
            "utility-office window leaves the wall height: "
            f"minimum={window_minimum.z:.3f} maximum={window_maximum.z:.3f}"
        )
    if roof_minimum.z < 2.85 or roof_maximum.z > 3.25:
        raise RuntimeError(
            "utility-office roof is not seated at the storey top: "
            f"minimum={roof_minimum.z:.3f} maximum={roof_maximum.z:.3f}"
        )


def validate_window_hall_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "window-hall":
        return

    windows = [obj for obj in objects if obj.type == "MESH" and "IndWindow" in obj.name]
    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    if len(windows) != 3 or any("IndWindowBFull" not in window.name for window in windows):
        raise RuntimeError("window-hall must use three single-storey windows")
    if len(roofs) != 8:
        raise RuntimeError("window-hall must contain eight roof modules")

    _, hall_maximum = mesh_bounds(objects)
    window_minimum, window_maximum = mesh_bounds(windows)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    if hall_maximum.z > 3.25:
        raise RuntimeError(f"window-hall exceeds one storey: maximum={hall_maximum.z:.3f}")
    if window_minimum.z < -0.005 or window_maximum.z > 3.15:
        raise RuntimeError(
            "window-hall windows leave the wall height: "
            f"minimum={window_minimum.z:.3f} maximum={window_maximum.z:.3f}"
        )
    if roof_minimum.z < 2.85 or roof_maximum.z > 3.25:
        raise RuntimeError(
            "window-hall roof is not seated at the storey top: "
            f"minimum={roof_minimum.z:.3f} maximum={roof_maximum.z:.3f}"
        )


def validate_service_hall_parts(objects: list[bpy.types.Object], assembly: Assembly) -> None:
    if assembly.slug != "sawtooth-service-hall":
        return

    windows = [obj for obj in objects if obj.type == "MESH" and "IndWindow" in obj.name]
    roofs = [obj for obj in objects if obj.type == "MESH" and "IndRoof" in obj.name]
    if len(windows) != 2 or any("IndWindowBFull" not in window.name for window in windows):
        raise RuntimeError("service hall must use two single-storey front windows")
    if len(roofs) != 8 or any("IndRoofDarkGreyFull" not in roof.name for roof in roofs):
        raise RuntimeError("service hall must use eight closed flat-roof modules")
    _, hall_maximum = mesh_bounds(objects)
    roof_minimum, roof_maximum = mesh_bounds(roofs)
    if hall_maximum.z > 3.25 or roof_minimum.z < 2.85 or roof_maximum.z > 3.25:
        raise RuntimeError(
            "service hall roof is not seated at the wall top: "
            f"hall_top={hall_maximum.z:.3f} roof={roof_minimum.z:.3f}..{roof_maximum.z:.3f}"
        )


def mesh_statistics(objects: list[bpy.types.Object]) -> tuple[int, int, int]:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    materials = {material for obj in meshes for material in obj.data.materials if material is not None}
    return len(meshes), triangles, len(materials)


def optimize_new_building_for_export(
    objects: list[bpy.types.Object], assembly: Assembly
) -> tuple[list[bpy.types.Object], tuple[int, int, int]]:
    source_statistics = mesh_statistics(objects)
    if assembly.slug not in NEW_CLOSED_BUILDING_REQUIREMENTS:
        return objects, source_statistics

    source_minimum, source_maximum = mesh_bounds(objects)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    non_meshes = [obj for obj in objects if obj.type != "MESH"]
    materials = sorted(
        {
            material
            for obj in meshes
            for material in obj.data.materials
            if material is not None
        },
        key=lambda material: material.name,
    )
    if not meshes or not materials:
        raise RuntimeError(f"{assembly.slug} has no source meshes or palette material to optimize")

    canonical_material = materials[0]
    for obj in meshes:
        for slot in obj.material_slots:
            slot.material = canonical_material

    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    joined = meshes[0]
    bpy.context.view_layer.objects.active = joined
    result = bpy.ops.object.join()
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not join {assembly.slug} source modules: {result}")
    joined.name = f"{assembly.root_name}Visual"
    joined.data.name = f"{assembly.root_name}Mesh"
    joined.data.materials.clear()
    joined.data.materials.append(canonical_material)
    for polygon in joined.data.polygons:
        polygon.material_index = 0
    optimized_objects = [*non_meshes, joined]
    optimized_minimum, optimized_maximum = mesh_bounds(optimized_objects)
    optimized_statistics = mesh_statistics(optimized_objects)
    if any(
        abs(source_minimum[index] - optimized_minimum[index]) > 0.001
        or abs(source_maximum[index] - optimized_maximum[index]) > 0.001
        for index in range(3)
    ):
        raise RuntimeError(
            f"{assembly.slug} changed bounds during DCC mesh consolidation: "
            f"source={tuple(source_minimum)}..{tuple(source_maximum)} "
            f"optimized={tuple(optimized_minimum)}..{tuple(optimized_maximum)}"
        )
    if (
        optimized_statistics[0] != 1
        or optimized_statistics[1] != source_statistics[1]
        or optimized_statistics[2] != 1
    ):
        raise RuntimeError(
            f"{assembly.slug} failed DCC mesh/material consolidation: "
            f"source={source_statistics} optimized={optimized_statistics}"
        )
    print(
        "TREY_EXPORT_OPTIMIZATION "
        f"slug={assembly.slug} source={source_statistics[0]}/{source_statistics[1]}/"
        f"{source_statistics[2]} runtime={optimized_statistics[0]}/"
        f"{optimized_statistics[1]}/{optimized_statistics[2]}"
    )
    return optimized_objects, source_statistics


def export_glb(root: bpy.types.Object, objects: list[bpy.types.Object], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_cameras=False,
        export_lights=False,
        export_animations=False,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_extras=True,
    )
    if "FINISHED" not in result or not output_path.is_file():
        raise RuntimeError(f"Blender could not export {output_path.name}: {result}")


def verify_glb(
    output_path: Path,
    assembly: Assembly,
    expected_dimensions: Vector,
    expected_statistics: tuple[int, int, int],
    expected_source_statistics: tuple[int, int, int],
) -> tuple[Vector, int, tuple[int, int, int]]:
    clear_scene()
    configure_scene()
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.gltf(filepath=str(output_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not verify {output_path.name}: {result}")
    imported = [obj for obj in bpy.data.objects if obj not in before]
    minimum, maximum = mesh_bounds(imported)
    dimensions = maximum - minimum
    if any(abs(dimensions[index] - expected_dimensions[index]) > 0.005 for index in range(3)):
        raise RuntimeError(
            f"{output_path.name} changed dimensions during glTF round-trip: "
            f"expected={tuple(expected_dimensions)} actual={tuple(dimensions)}"
        )
    if abs(minimum.z) > 0.005:
        raise RuntimeError(f"{output_path.name} moved off Z=0 during glTF round-trip")

    images = {
        node.image
        for material in bpy.data.materials
        if material.use_nodes
        for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    }
    if not images or any(image.size[0] == 0 or image.size[1] == 0 for image in images):
        raise RuntimeError(f"{output_path.name} has no readable embedded palette texture")
    if not any(obj.get("license") == "CC0-1.0" for obj in imported):
        raise RuntimeError(f"{output_path.name} lost its CC0 source metadata")
    verified_statistics = mesh_statistics(imported)
    if verified_statistics != expected_statistics:
        raise RuntimeError(
            f"{output_path.name} changed mesh statistics during glTF round-trip: "
            f"expected={expected_statistics} actual={verified_statistics}"
        )
    if assembly.slug in NEW_CLOSED_BUILDING_REQUIREMENTS:
        roots = [obj for obj in imported if obj.get("assembly") == assembly.slug]
        if len(roots) != 1 or not roots[0].get("closed_shell"):
            raise RuntimeError(f"{output_path.name} lost its closed-shell metadata")
        requirement = NEW_CLOSED_BUILDING_REQUIREMENTS[assembly.slug]
        if "storeys" in requirement and roots[0].get("storeys") != int(requirement["storeys"]):
            raise RuntimeError(f"{output_path.name} lost its storey-count metadata")
        if "gable_infills" in requirement and roots[0].get("gable_infill_modules") != int(
            requirement["gable_infills"]
        ):
            raise RuntimeError(f"{output_path.name} lost its authored gable-infill metadata")
        source_contract = (
            roots[0].get("source_meshes"),
            roots[0].get("source_triangles"),
            roots[0].get("source_materials"),
        )
        if roots[0].get("source_modules") != len(assembly.modules) or source_contract != (
            expected_source_statistics
        ):
            raise RuntimeError(
                f"{output_path.name} lost its source composition metadata: "
                f"modules={roots[0].get('source_modules')} source={source_contract}"
            )
        size = list(roots[0].get("collision_aabb_size_godot", []))
        offset = list(roots[0].get("collision_aabb_offset_godot", []))
        expected_size = [expected_dimensions.x, expected_dimensions.z, expected_dimensions.y]
        expected_offset = [0.0, expected_dimensions.z * 0.5, 0.0]
        if len(size) != 3 or len(offset) != 3 or any(
            abs(size[index] - expected_size[index]) > 0.001
            or abs(offset[index] - expected_offset[index]) > 0.001
            for index in range(3)
        ):
            raise RuntimeError(
                f"{output_path.name} lost its Godot AABB metadata: size={size} offset={offset}"
            )
    return dimensions, len(images), verified_statistics


def build_assembly(assembly: Assembly) -> None:
    clear_scene()
    configure_scene()
    objects: list[bpy.types.Object] = []
    for index, module in enumerate(assembly.modules, start=1):
        objects.extend(import_module(module, index))
    palette = load_palette()
    ensure_palette_materials(objects, palette)
    root = normalize_assembly(objects, assembly)
    dimensions = validate_dimensions(objects, assembly)
    validate_arch_gateway_parts(objects, assembly)
    validate_elevated_walkway_parts(objects, assembly)
    validate_utility_office_parts(objects, assembly)
    validate_window_hall_parts(objects, assembly)
    validate_service_hall_parts(objects, assembly)
    validate_closed_building_perimeter(objects, assembly)
    validate_new_building_shell(objects, assembly)
    objects, source_statistics = optimize_new_building_for_export(objects, assembly)
    mesh_count, triangle_count, material_count = mesh_statistics(objects)
    statistics = (mesh_count, triangle_count, material_count)
    if assembly.slug in NEW_CLOSED_BUILDING_REQUIREMENTS:
        root["closed_shell"] = True
        requirement = NEW_CLOSED_BUILDING_REQUIREMENTS[assembly.slug]
        root["roof_style"] = requirement["roof_style"]
        if "storeys" in requirement:
            root["storeys"] = int(requirement["storeys"])
        if "gable_infills" in requirement:
            root["gable_infill_modules"] = int(requirement["gable_infills"])
            root["gable_infill_adaptation"] = "authored-wall-triangular-dcc-cut"
        root["source_modules"] = len(assembly.modules)
        root["source_meshes"] = source_statistics[0]
        root["source_triangles"] = source_statistics[1]
        root["source_materials"] = source_statistics[2]
        root["collision_aabb_size_godot"] = [dimensions.x, dimensions.z, dimensions.y]
        root["collision_aabb_offset_godot"] = [0.0, dimensions.z * 0.5, 0.0]
    output_path = OUTPUT_DIR / assembly.output_name
    export_glb(root, objects, output_path)
    verified_dimensions, embedded_image_count, verified_statistics = verify_glb(
        output_path, assembly, dimensions, statistics, source_statistics
    )
    print(
        "TREY_INDUSTRIAL_ASSET "
        f"slug={assembly.slug} "
        f"dimensions_m={dimensions.x:.3f}x{dimensions.y:.3f}x{dimensions.z:.3f} "
        f"verified_m={verified_dimensions.x:.3f}x{verified_dimensions.y:.3f}x{verified_dimensions.z:.3f} "
        f"modules={len(assembly.modules)} meshes={mesh_count} triangles={triangle_count} "
        f"materials={material_count} embedded_images={embedded_image_count} "
        f"roundtrip={verified_statistics[0]}/{verified_statistics[1]}/{verified_statistics[2]} "
        f"bytes={output_path.stat().st_size} sha256={file_sha256(output_path)}"
    )


def main() -> None:
    selected = parse_args()
    validate_new_assembly_definitions()
    require_sources()
    for assembly in ASSEMBLIES:
        if assembly.slug in selected:
            build_assembly(assembly)
    print(f"TREY_INDUSTRIAL_PASS built={len(selected)} output={OUTPUT_DIR}")


if __name__ == "__main__":
    main()
