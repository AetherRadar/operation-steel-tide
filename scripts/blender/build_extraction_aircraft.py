"""Build the project-authored rescue tilt-rotor and export it for Godot.

Run with Blender 5.x:
    blender --background --python scripts/blender/build_extraction_aircraft.py

The generated hierarchy deliberately exposes stable pivot names used by
ExtractionAircraftVisual.cs. Gameplay, collision, audio, seats, and flight
state remain owned by Godot; this file only authors the exterior visual.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = REPO_ROOT / "assets" / "models" / "extraction_aircraft"
SOURCE_DIR = REPO_ROOT / "source_art" / "extraction_aircraft"
BLEND_PATH = SOURCE_DIR / "extraction_aircraft.blend"
GLB_PATH = ASSET_DIR / "extraction_aircraft.glb"
PREVIEW_PATH = Path("/tmp/operation-steel-tide-tools/blender/extraction_aircraft_preview.png")


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.metallic = metallic
    mat.roughness = roughness
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness
        if emission is not None:
            emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
            strength_input = principled.inputs.get("Emission Strength")
            if emission_input is not None:
                emission_input.default_value = emission
            if strength_input is not None:
                strength_input.default_value = emission_strength
    return mat


def empty(name: str, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 0.25
    obj.parent = parent
    return obj


def finish_mesh(
    obj: bpy.types.Object,
    parent: bpy.types.Object,
    mat: bpy.types.Material,
    bevel: float = 0.0,
    smooth: bool = False,
) -> bpy.types.Object:
    obj.parent = parent
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("EdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    obj.select_set(False)
    return obj


def cube(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    bevel: float = 0.06,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    return finish_mesh(obj, parent, mat, bevel)


def sphere(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    mat: bpy.types.Material,
    segments: int = 32,
    rings: int = 16,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_mesh(obj, parent, mat, smooth=True)


def cylinder(
    name: str,
    parent: bpy.types.Object,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    mat: bpy.types.Material,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    vertices: int = 24,
    bevel: float = 0.04,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, parent, mat, bevel, smooth=True)


def prism(
    name: str,
    parent: bpy.types.Object,
    footprint: list[tuple[float, float]],
    z_bottom: float,
    z_top: float,
    mat: bpy.types.Material,
    bevel: float = 0.04,
) -> bpy.types.Object:
    count = len(footprint)
    vertices = [(x, y, z_bottom) for x, y in footprint]
    vertices.extend((x, y, z_top) for x, y in footprint)
    faces: list[tuple[int, ...]] = []
    faces.append(tuple(range(count - 1, -1, -1)))
    faces.append(tuple(range(count, count * 2)))
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))
    mesh = bpy.data.meshes.new(f"{name}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish_mesh(obj, parent, mat, bevel)


def add_panel_strips(
    root: bpy.types.Object,
    armor: bpy.types.Material,
    accent: bpy.types.Material,
) -> None:
    for side in (-1.0, 1.0):
        x = side * 1.09
        cube(
            f"SideArmor_{'L' if side < 0 else 'R'}",
            root,
            (x, 0.2, 0.04),
            (0.08, 4.15, 0.82),
            armor,
            bevel=0.025,
        )
        for index, y in enumerate((-1.15, -0.15, 0.85)):
            cube(
                f"ServicePanel_{'L' if side < 0 else 'R'}_{index}",
                root,
                (side * 1.137, y, 0.02),
                (0.018, 0.78, 0.48),
                accent if index == 1 else armor,
                bevel=0.01,
            )
        cube(
            f"RescueStripe_{'L' if side < 0 else 'R'}",
            root,
            (side * 1.145, 1.68, 0.12),
            (0.022, 0.18, 0.56),
            accent,
            rotation=(math.radians(-18.0), 0.0, 0.0),
            bevel=0.008,
        )


def add_cockpit_glass(
    root: bpy.types.Object,
    glass: bpy.types.Material,
    frame: bpy.types.Material,
) -> None:
    cube("CockpitGlassCenter", root, (0.0, -3.26, 0.39), (1.45, 0.08, 0.55), glass, bevel=0.08)
    for side in (-1.0, 1.0):
        cube(
            f"CockpitGlass_{'L' if side < 0 else 'R'}",
            root,
            (side * 0.72, -2.98, 0.37),
            (0.48, 0.72, 0.5),
            glass,
            rotation=(0.0, math.radians(side * 19.0), 0.0),
            bevel=0.07,
        )
        cube(
            f"CockpitFrame_{'L' if side < 0 else 'R'}",
            root,
            (side * 0.31, -3.32, 0.4),
            (0.07, 0.09, 0.62),
            frame,
            bevel=0.015,
        )


def add_rotor(
    side_name: str,
    side: float,
    root: bpy.types.Object,
    armor: bpy.types.Material,
    dark: bpy.types.Material,
    metal: bpy.types.Material,
    warning: bpy.types.Material,
) -> None:
    nacelle = empty(f"{side_name}Nacelle", root)
    nacelle.location = (side * 3.72, -0.18, 0.82)
    sphere("NacelleShell", nacelle, (0.0, 0.0, 0.0), (0.73, 1.18, 0.67), armor, 28, 14)
    cube("NacelleLowerArmor", nacelle, (0.0, 0.05, -0.49), (1.02, 1.6, 0.28), dark, bevel=0.11)
    cylinder(
        "TurbineIntake",
        nacelle,
        (0.0, -1.04, 0.03),
        0.39,
        0.18,
        dark,
        rotation=(math.pi / 2.0, 0.0, 0.0),
        vertices=32,
        bevel=0.025,
    )
    cylinder(
        "TurbineCore",
        nacelle,
        (0.0, -1.15, 0.03),
        0.22,
        0.05,
        metal,
        rotation=(math.pi / 2.0, 0.0, 0.0),
        vertices=24,
        bevel=0.01,
    )
    for index, y in enumerate((-0.45, 0.14, 0.68)):
        cube(f"NacelleBand_{index}", nacelle, (0.0, y, 0.02), (1.12, 0.075, 0.92), dark, bevel=0.015)

    pivot = empty(f"{side_name}RotorPivot", nacelle)
    pivot.location = (0.0, -0.12, 0.66)
    cylinder("RotorHub", pivot, (0.0, 0.0, 0.09), 0.28, 0.26, metal, vertices=28, bevel=0.04)
    cylinder("RotorCap", pivot, (0.0, 0.0, 0.28), 0.13, 0.24, warning, vertices=20, bevel=0.04)
    for blade_index in range(4):
        blade = prism(
            f"RotorBlade_{blade_index}",
            pivot,
            [(0.31, -0.11), (3.42, -0.18), (3.62, 0.01), (0.42, 0.16)],
            -0.035,
            0.035,
            dark,
            bevel=0.025,
        )
        blade.rotation_euler[2] = blade_index * math.pi / 2.0 + side * math.radians(7.0)
        cube(
            f"BladeTip_{blade_index}",
            pivot,
            (3.42 * math.cos(blade.rotation_euler[2]), 3.42 * math.sin(blade.rotation_euler[2]), 0.015),
            (0.34, 0.2, 0.075),
            warning,
            rotation=(0.0, 0.0, blade.rotation_euler[2]),
            bevel=0.025,
        )


def add_landing_gear(
    root: bpy.types.Object,
    dark: bpy.types.Material,
    metal: bpy.types.Material,
) -> None:
    gear_points = [(-0.82, -1.1), (0.82, -1.1), (-0.72, 2.05), (0.72, 2.05)]
    for index, (x, y) in enumerate(gear_points):
        cylinder(
            f"GearStrut_{index}",
            root,
            (x, y, -0.92),
            0.075,
            0.82,
            metal,
            rotation=(0.0, math.radians(11.0 if x < 0 else -11.0), 0.0),
            vertices=16,
            bevel=0.018,
        )
        cylinder(
            f"GearWheel_{index}",
            root,
            (x, y + 0.05, -1.29),
            0.22,
            0.15,
            dark,
            rotation=(0.0, math.pi / 2.0, 0.0),
            vertices=20,
            bevel=0.035,
        )


def join_direct_mesh_children(parent: bpy.types.Object, name: str) -> None:
    meshes = [child for child in parent.children if child.type == "MESH"]
    if len(meshes) < 2:
        if meshes:
            meshes[0].name = name
        return
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    meshes[0].name = name


def consolidate_runtime_meshes(root: bpy.types.Object) -> None:
    left_nacelle = bpy.data.objects["LeftNacelle"]
    right_nacelle = bpy.data.objects["RightNacelle"]
    left_rotor = bpy.data.objects["LeftRotorPivot"]
    right_rotor = bpy.data.objects["RightRotorPivot"]
    boarding_door = bpy.data.objects["BoardingDoor"]
    join_direct_mesh_children(root, "AircraftExterior")
    join_direct_mesh_children(left_nacelle, "LeftNacelleShell")
    join_direct_mesh_children(right_nacelle, "RightNacelleShell")
    join_direct_mesh_children(left_rotor, "LeftRotorAssembly")
    join_direct_mesh_children(right_rotor, "RightRotorAssembly")
    join_direct_mesh_children(boarding_door, "BoardingDoorAssembly")


def add_preview_stage() -> None:
    world = bpy.context.scene.world
    world.color = (0.025, 0.032, 0.04)
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.018, 0.025, 0.034, 1.0)
        background.inputs["Strength"].default_value = 0.32

    floor_mat = material("PreviewFloor", (0.055, 0.065, 0.072, 1.0), 0.1, 0.82)
    bpy.ops.mesh.primitive_plane_add(size=32.0, location=(0.0, 0.0, -1.52))
    floor = bpy.context.object
    floor.name = "PreviewFloor"
    floor.data.materials.append(floor_mat)

    def area_light(
        name: str,
        location: tuple[float, float, float],
        energy: float,
        color: tuple[float, float, float],
        size: float,
    ) -> None:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        direction = Vector((0.0, 0.0, 0.2)) - light.location
        light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    area_light("PreviewKey", (6.0, -7.0, 9.0), 1700.0, (0.72, 0.88, 1.0), 5.0)
    area_light("PreviewFill", (-7.0, -1.0, 4.0), 1050.0, (0.2, 1.0, 0.58), 4.0)
    area_light("PreviewRim", (1.0, 7.0, 7.0), 1500.0, (1.0, 0.34, 0.12), 3.5)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (9.8, -12.8, 6.9)
    direction = Vector((0.0, 0.0, 0.15)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 56.0
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)


def build_aircraft() -> None:
    clear_scene()
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"

    armor = material("ArmorGreen", (0.12, 0.19, 0.17, 1.0), 0.72, 0.34)
    armor_light = material("ArmorEdge", (0.24, 0.31, 0.27, 1.0), 0.66, 0.31)
    dark = material("RotorGraphite", (0.025, 0.035, 0.034, 1.0), 0.58, 0.32)
    metal = material("ExposedMetal", (0.42, 0.47, 0.43, 1.0), 0.84, 0.24)
    glass = material("CockpitGlass", (0.018, 0.095, 0.12, 1.0), 0.36, 0.12)
    rescue = material(
        "RescueGreen",
        (0.05, 0.73, 0.36, 1.0),
        0.22,
        0.3,
        (0.02, 0.42, 0.13, 1.0),
        2.4,
    )
    warning = material(
        "WarningOrange",
        (1.0, 0.36, 0.035, 1.0),
        0.18,
        0.3,
        (0.65, 0.08, 0.005, 1.0),
        1.7,
    )
    interior = material("CabinInterior", (0.012, 0.018, 0.017, 1.0), 0.08, 0.82)

    root = empty("AircraftBody")
    sphere("FuselageCore", root, (0.0, -0.05, -0.02), (1.18, 3.22, 0.91), armor, 40, 20)
    sphere("ArmoredNose", root, (0.0, -2.68, -0.04), (1.03, 1.34, 0.72), armor_light, 36, 18)
    sphere("TailBoom", root, (0.0, 2.64, 0.12), (0.72, 2.12, 0.56), armor, 32, 16)
    cube("BellyKeel", root, (0.0, 0.15, -0.72), (1.28, 4.75, 0.28), dark, bevel=0.13)
    cube("DorsalSpine", root, (0.0, 0.35, 0.72), (0.65, 4.55, 0.22), armor_light, bevel=0.09)
    add_cockpit_glass(root, glass, dark)
    add_panel_strips(root, armor_light, rescue)

    prism(
        "MainWing",
        root,
        [(-4.78, -0.78), (4.78, -0.78), (4.96, 0.34), (2.0, 0.82), (-2.0, 0.82), (-4.96, 0.34)],
        0.65,
        0.89,
        dark,
        bevel=0.08,
    )
    prism(
        "WingArmorTop",
        root,
        [(-4.58, -0.58), (4.58, -0.58), (4.7, 0.16), (1.9, 0.55), (-1.9, 0.55), (-4.7, 0.16)],
        0.885,
        0.98,
        armor_light,
        bevel=0.045,
    )
    for side in (-1.0, 1.0):
        cube(
            f"WingRescueMark_{'L' if side < 0 else 'R'}",
            root,
            (side * 2.28, -0.12, 0.99),
            (1.0, 0.19, 0.035),
            rescue,
            rotation=(0.0, 0.0, math.radians(side * 9.0)),
            bevel=0.012,
        )

    prism("HorizontalTail", root, [(-2.1, 3.34), (2.1, 3.34), (1.65, 4.2), (-1.65, 4.2)], 0.65, 0.82, dark, bevel=0.055)
    prism("VerticalTail", root, [(-0.12, 2.72), (0.12, 2.72), (0.12, 4.2), (-0.12, 4.42)], 0.66, 2.5, armor_light, bevel=0.06)
    cube("TailRescueBeacon", root, (0.0, 4.26, 2.1), (0.22, 0.16, 0.28), rescue, bevel=0.06)

    add_rotor("Left", -1.0, root, armor, dark, metal, warning)
    add_rotor("Right", 1.0, root, armor, dark, metal, warning)
    add_landing_gear(root, dark, metal)

    cube("RearDoorOpening", root, (0.0, 3.26, -0.38), (1.48, 0.16, 1.03), interior, bevel=0.055)
    ramp = empty("BoardingDoor", root)
    ramp.location = (0.0, 3.31, -0.76)
    cube("RampDeck", ramp, (0.0, 0.93, 0.0), (1.42, 1.86, 0.12), dark, bevel=0.045)
    for index, y in enumerate((0.28, 0.65, 1.02, 1.39, 1.68)):
        cube(f"RampGrip_{index}", ramp, (0.0, y, 0.075), (1.25, 0.055, 0.035), rescue, bevel=0.012)
    for side in (-1.0, 1.0):
        cube(f"RampRail_{'L' if side < 0 else 'R'}", ramp, (side * 0.64, 0.93, 0.12), (0.07, 1.82, 0.18), armor_light, bevel=0.025)

    for side in (-1.0, 1.0):
        cube(f"NavigationLight_{'L' if side < 0 else 'R'}", root, (side * 4.84, 0.05, 0.9), (0.18, 0.2, 0.12), warning if side < 0 else rescue, bevel=0.05)
    cylinder("NoseSearchLight", root, (0.0, -3.48, -0.37), 0.18, 0.1, warning, rotation=(math.pi / 2.0, 0.0, 0.0), vertices=24, bevel=0.025)

    consolidate_runtime_meshes(root)
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        export_format="GLB",
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )
    add_preview_stage()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    mesh_count = sum(1 for obj in bpy.context.scene.objects if obj.type == "MESH")
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
    triangle_count = sum(len(obj.data.loop_triangles) for obj in bpy.context.scene.objects if obj.type == "MESH")
    print(
        f"EXTRACTION_AIRCRAFT_EXPORT meshes={mesh_count} triangles={triangle_count} "
        f"glb={GLB_PATH} preview={PREVIEW_PATH}"
    )


if __name__ == "__main__":
    build_aircraft()
