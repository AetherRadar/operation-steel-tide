"""Build the Jianghai Old City Chinese lattice hinge door.

Run from the repository root with Blender 4.5 LTS:
    blender --background --factory-startup --python scripts/blender/build_jianghai_lattice_door.py

The retained door leaf is Kenney's finished CC0 Factory Kit mesh.  Its Chinese
identity comes from a retained, finished CC0 arched double-door lattice region
in Free poly's Chinese Temple 2, extracted from the repository's authoritative
Jianghai Blender source and fitted to the leaf in DCC.  No generated primitive
is part of the exported door.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
import zlib
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE_GLB = REPO_ROOT / "assets" / "models" / "kenney_factory_kit" / "door-hinged.glb"
CHINESE_SOURCE_BLEND = REPO_ROOT / "source_art" / "world" / "jianghai_old_city" / "jianghai_old_city.blend"
SOURCE_DIR = REPO_ROOT / "source_art" / "props" / "jianghai_lattice_door"
OUTPUT_DIR = REPO_ROOT / "assets" / "models" / "jianghai_old_city"
SOURCE_BLEND = SOURCE_DIR / "jianghai_lattice_door.blend"
PREVIEW_PNG = SOURCE_DIR / "jianghai_lattice_door_preview.png"
WOOD_TEXTURE = SOURCE_DIR / "jianghai_red_wood_albedo.png"
OUTPUT_GLB = OUTPUT_DIR / "jianghai_lattice_door.glb"
OUTPUT_TEXTURE = OUTPUT_DIR / "jianghai_lattice_door_JianghaiRedWoodAlbedo.png"
ROOT_NAME = "JianghaiLatticeDoor"
HINGE_NAME = "DoorLeafHinge"
SOURCE_RELATIVE = "assets/models/kenney_factory_kit/door-hinged.glb"
CHINESE_SOURCE_RELATIVE = "source_art/world/jianghai_old_city/jianghai_old_city.blend"
SOURCE_SHA256 = "3857B4953CA264DD37B42B8D8391CD2348CACBD2671BA87113434A311B956C1B"
EXPECTED_MATERIALS = {
    "JianghaiRedWoodLacquer",
    "JianghaiTempleAgedGoldLattice",
}
CHINESE_SOURCE_OBJECT = "GuangchangClanHall"
CHINESE_SOURCE_MESH = "网格.002"
CHINESE_SOURCE_MATERIAL_INDEX = 2
CHINESE_SOURCE_MIN = (-0.61685, -2.24555, 6.14627)
CHINESE_SOURCE_MAX = (0.61647, -2.10685, 7.64258)
CHINESE_SOURCE_VERTICES = 11192
CHINESE_SOURCE_POLYGONS = 9316
CHINESE_SOURCE_TRIANGLES = 9316
CHINESE_DECIMATE_RATIO = 0.60
CHINESE_LOD_VERTICES = 9276
CHINESE_LOD_POLYGONS = 5589
CHINESE_LOD_TRIANGLES = 5589
CHINESE_SOURCE_CREATOR = "Free poly"
CHINESE_SOURCE_ASSET = "Chinese Temple 2"
CHINESE_SOURCE_URL = (
    "https://www.blenderkit.com/asset-gallery-detail/"
    "8701a79a-1635-437c-b1d2-6b14f14fc351/"
)


def configure_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    scene.view_settings.look = "AgX - Medium High Contrast"


def create_wood_texture() -> bpy.types.Image:
    width = 256
    height = 256
    rows: list[bytes] = []
    for row in range(height):
        vertical = row / (height - 1)
        pixels = bytearray((0,))
        for column in range(width):
            horizontal = column / (width - 1)
            grain = (
                0.54
                + 0.22 * math.sin(horizontal * 92.0 + math.sin(vertical * 15.0) * 2.6)
                + 0.11 * math.sin(horizontal * 211.0 + vertical * 7.0)
            )
            pore = ((column * 37 + row * 17) % 29) / 28.0
            grain += (pore - 0.5) * 0.035
            highlight = max(0.0, math.sin(vertical * math.pi)) * 0.028
            red = min(0.70, 0.34 + grain * 0.28 + highlight)
            green = min(0.18, 0.045 + grain * 0.075)
            blue = min(0.11, 0.022 + grain * 0.040)
            pixels.extend((round(red * 255), round(green * 255), round(blue * 255), 255))
        rows.append(bytes(pixels))

    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)
        )

    encoded = b"\x89PNG\r\n\x1a\n"
    encoded += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    encoded += chunk(b"IDAT", zlib.compress(b"".join(rows), level=9))
    encoded += chunk(b"IEND", b"")
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    WOOD_TEXTURE.write_bytes(encoded)
    # Godot extracts embedded glTF images beside the source asset.  Writing the
    # exact deterministic payload here keeps a clean checkout clean after its
    # first import while the GLB itself remains independently self-contained.
    OUTPUT_TEXTURE.write_bytes(encoded)
    image = bpy.data.images.load(str(WOOD_TEXTURE), check_existing=False)
    image.name = "JianghaiRedWoodAlbedo"
    image.pack()
    return image


def pbr_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
    *,
    texture: bpy.types.Image | None = None,
    coat: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    material.metallic = metallic
    material.roughness = roughness
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError(f"Blender did not create Principled BSDF for {name}")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    coat_weight = principled.inputs.get("Coat Weight")
    coat_roughness = principled.inputs.get("Coat Roughness")
    if coat_weight is not None:
        coat_weight.default_value = coat
    if coat_roughness is not None:
        coat_roughness.default_value = 0.22
    if texture is not None:
        texture_node = nodes.new("ShaderNodeTexImage")
        texture_node.name = "PackedRedWoodAlbedo"
        texture_node.label = "Project-authored packed lacquered wood albedo"
        texture_node.image = texture
        texture_node.interpolation = "Linear"
        links.new(texture_node.outputs["Color"], principled.inputs["Base Color"])
    return material


def create_palette() -> dict[str, bpy.types.Material]:
    wood_texture = create_wood_texture()
    return {
        "base": pbr_material(
            "JianghaiRedWoodLacquer", (0.46, 0.060, 0.030, 1.0), 0.04, 0.37,
            texture=wood_texture, coat=0.26,
        ),
        "lattice": pbr_material(
            "JianghaiTempleAgedGoldLattice", (0.56, 0.240, 0.045, 1.0), 0.48, 0.38,
            coat=0.20,
        ),
    }


def empty(name: str, parent: bpy.types.Object | None = None) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    obj.empty_display_type = "ARROWS"
    obj.empty_display_size = 0.08
    return obj


def import_authored_leaf(hinge: bpy.types.Object, material: bpy.types.Material) -> bpy.types.Object:
    if not SOURCE_GLB.is_file():
        raise FileNotFoundError(f"Missing retained Kenney source: {SOURCE_GLB}")
    digest = hashlib.sha256(SOURCE_GLB.read_bytes()).hexdigest().upper()
    if digest != SOURCE_SHA256:
        raise RuntimeError(f"Kenney door digest changed: expected={SOURCE_SHA256} actual={digest}")
    before = set(bpy.data.objects)
    result = bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not import {SOURCE_GLB}: {result}")
    imported = [obj for obj in bpy.data.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one retained Kenney mesh, found {len(meshes)}")
    base = meshes[0]
    for obj in imported:
        if obj != base:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    base.animation_data_clear()
    base.name = "DoorLeafAuthoredKenneyBase"
    base.data.name = "DoorLeafAuthoredKenneyMesh"
    base.parent = hinge
    base.location = (0.0, 0.0, 0.0)
    base.rotation_mode = "XYZ"
    base.rotation_euler = (0.0, 0.0, math.pi)
    base.scale = (1.0, 0.36, 1.0)
    bpy.ops.object.select_all(action="DESELECT")
    base.select_set(True)
    bpy.context.view_layer.objects.active = base
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    base.data.materials.clear()
    base.data.materials.append(material)
    base["authored_source"] = SOURCE_RELATIVE
    base["source_creator"] = "Kenney"
    base["source_asset"] = "Factory Kit / door.glb derivative"
    base["source_license"] = "CC0-1.0"
    base["source_sha256"] = SOURCE_SHA256
    base["retained_finished_mesh"] = True
    return base


def data_bounds(mesh: bpy.types.Mesh) -> tuple[Vector, Vector]:
    points = [vertex.co for vertex in mesh.vertices]
    if not points:
        raise RuntimeError(f"Authored source mesh {mesh.name} has no vertices")
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def fit_authored_mesh(
    mesh: bpy.types.Mesh,
    source_matrix: Matrix,
    axis_remap: Matrix,
    target_dimensions: tuple[float, float, float],
) -> None:
    """Bake and fit retained topology; no vertices or faces are generated."""
    mesh.transform(source_matrix)
    minimum, maximum = data_bounds(mesh)
    mesh.transform(Matrix.Translation(-(minimum + maximum) * 0.5))
    mesh.transform(axis_remap)
    minimum, maximum = data_bounds(mesh)
    dimensions = maximum - minimum
    factors = [target_dimensions[axis] / dimensions[axis] for axis in range(3)]
    mesh.transform(Matrix.Diagonal((*factors, 1.0)))
    mesh.update()


def extract_temple_lattice(source: bpy.types.Object) -> bpy.types.Mesh:
    if source.type != "MESH" or source.data.name != CHINESE_SOURCE_MESH:
        raise RuntimeError(
            f"Chinese Temple 2 source contract changed: object={source.name} mesh={source.data.name}"
        )
    if source.get("asset_origin") != "BlenderKit CC0 Chinese Temple 2":
        raise RuntimeError("Chinese Temple 2 lost its packed CC0 asset-origin metadata")

    tolerance = 0.00001
    selected_faces = [
        polygon
        for polygon in source.data.polygons
        if polygon.material_index == CHINESE_SOURCE_MATERIAL_INDEX
        and all(
            all(
                CHINESE_SOURCE_MIN[axis] - tolerance
                <= source.data.vertices[vertex_index].co[axis]
                <= CHINESE_SOURCE_MAX[axis] + tolerance
                for axis in range(3)
            )
            for vertex_index in polygon.vertices
        )
    ]
    selected_vertices = {vertex_index for polygon in selected_faces for vertex_index in polygon.vertices}
    selected_triangles = sum(len(polygon.vertices) - 2 for polygon in selected_faces)
    actual = (len(selected_vertices), len(selected_faces), selected_triangles)
    expected = (CHINESE_SOURCE_VERTICES, CHINESE_SOURCE_POLYGONS, CHINESE_SOURCE_TRIANGLES)
    if actual != expected:
        raise RuntimeError(f"Chinese Temple 2 lattice region changed: actual={actual} expected={expected}")

    retained = source.data.copy()
    retained.name = "FreePolyTempleArchedDoubleDoorLatticeSourceMesh"
    editable = bmesh.new()
    editable.from_mesh(retained)
    editable.faces.ensure_lookup_table()
    selected_indices = {polygon.index for polygon in selected_faces}
    bmesh.ops.delete(
        editable,
        geom=[face for face in editable.faces if face.index not in selected_indices],
        context="FACES",
    )
    bmesh.ops.delete(
        editable,
        geom=[vertex for vertex in editable.verts if not vertex.link_faces],
        context="VERTS",
    )
    editable.to_mesh(retained)
    editable.free()
    retained.calc_loop_triangles()
    extracted = (len(retained.vertices), len(retained.polygons), len(retained.loop_triangles))
    if extracted != expected:
        raise RuntimeError(f"Temple lattice extraction changed topology: {extracted} expected={expected}")
    return retained


def decimate_temple_lattice(mesh: bpy.types.Mesh) -> bpy.types.Mesh:
    temporary = bpy.data.objects.new("__TempleLatticeDccDecimation", mesh)
    bpy.context.scene.collection.objects.link(temporary)
    bpy.context.view_layer.objects.active = temporary
    temporary.select_set(True)
    modifier = temporary.modifiers.new("AuthoredTempleLatticeDistanceLOD", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = CHINESE_DECIMATE_RATIO
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    result = temporary.data.copy()
    result.name = "FreePolyTempleArchedDoubleDoorLatticeMesh"
    bpy.data.objects.remove(temporary, do_unlink=True)
    result.calc_loop_triangles()
    actual = (len(result.vertices), len(result.polygons), len(result.loop_triangles))
    expected = (CHINESE_LOD_VERTICES, CHINESE_LOD_POLYGONS, CHINESE_LOD_TRIANGLES)
    if actual != expected:
        raise RuntimeError(f"Temple lattice DCC LOD changed: actual={actual} expected={expected}")
    return result


def source_metadata(obj: bpy.types.Object, role: str, component_triangles: int) -> None:
    obj["authored_source"] = CHINESE_SOURCE_RELATIVE
    obj["source_object"] = CHINESE_SOURCE_OBJECT
    obj["source_mesh"] = CHINESE_SOURCE_MESH
    obj["source_material_index"] = CHINESE_SOURCE_MATERIAL_INDEX
    obj["source_region_min"] = ",".join(f"{value:.5f}" for value in CHINESE_SOURCE_MIN)
    obj["source_region_max"] = ",".join(f"{value:.5f}" for value in CHINESE_SOURCE_MAX)
    obj["source_topology"] = (
        f"vertices={CHINESE_SOURCE_VERTICES};polygons={CHINESE_SOURCE_POLYGONS};"
        f"triangles={CHINESE_SOURCE_TRIANGLES}"
    )
    obj["source_asset"] = CHINESE_SOURCE_ASSET
    obj["source_creator"] = CHINESE_SOURCE_CREATOR
    obj["source_url"] = CHINESE_SOURCE_URL
    obj["source_license"] = "CC0-1.0"
    obj["retained_finished_mesh"] = True
    obj["dcc_adaptation"] = (
        f"polygon-region extraction; Blender Decimate COLLAPSE ratio={CHINESE_DECIMATE_RATIO:.2f}; "
        "centered and non-uniformly fitted"
    )
    obj["component_triangles"] = component_triangles
    obj["dcc_lod_topology"] = (
        f"vertices={CHINESE_LOD_VERTICES};polygons={CHINESE_LOD_POLYGONS};"
        f"triangles={CHINESE_LOD_TRIANGLES}"
    )
    obj["door_role"] = role


def append_authored_components(
    hinge: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> list[bpy.types.Object]:
    if not CHINESE_SOURCE_BLEND.is_file():
        raise FileNotFoundError(f"Missing authoritative Chinese source: {CHINESE_SOURCE_BLEND}")
    with bpy.data.libraries.load(str(CHINESE_SOURCE_BLEND), link=False) as (available, loaded):
        if CHINESE_SOURCE_OBJECT not in available.objects:
            raise RuntimeError(f"Jianghai source lost {CHINESE_SOURCE_OBJECT}")
        loaded.objects = [CHINESE_SOURCE_OBJECT]

    source = loaded.objects[0]
    if source is None:
        raise RuntimeError("Blender returned an empty Chinese Temple 2 append")
    bpy.context.scene.collection.objects.link(source)
    bpy.context.view_layer.update()

    extracted = extract_temple_lattice(source)
    lattice_mesh = decimate_temple_lattice(extracted)
    fit_authored_mesh(
        lattice_mesh,
        Matrix.Identity(4),
        Matrix.Identity(4),
        (0.60, 0.038, 0.70),
    )
    lattice_mesh.materials.clear()
    lattice_mesh.materials.append(materials["lattice"])
    lattice_mesh.calc_loop_triangles()
    component_triangles = len(lattice_mesh.loop_triangles)

    front = bpy.data.objects.new("AuthoredTempleLatticeFront", lattice_mesh)
    bpy.context.scene.collection.objects.link(front)
    front.parent = hinge
    front.location = (0.40, -0.076, 1.075)
    source_metadata(front, "front arched double-door fine-lattice panel", component_triangles)

    back = bpy.data.objects.new("AuthoredTempleLatticeBack", lattice_mesh)
    bpy.context.scene.collection.objects.link(back)
    back.parent = hinge
    back.location = (0.40, 0.076, 1.075)
    back.rotation_euler.z = math.pi
    source_metadata(back, "back arched double-door fine-lattice panel", component_triangles)

    bpy.data.objects.remove(source, do_unlink=True)
    return [front, back]


def create_action(hinge: bpy.types.Object, name: str, start_angle: float, end_angle: float) -> bpy.types.Action:
    hinge.animation_data_create()
    action = bpy.data.actions.new(name)
    hinge.animation_data.action = action
    hinge.rotation_mode = "XYZ"
    hinge.rotation_euler = (0.0, 0.0, math.radians(start_angle))
    hinge.keyframe_insert(data_path="rotation_euler", frame=0, group="DoorHinge")
    hinge.rotation_euler = (0.0, 0.0, math.radians(end_angle))
    hinge.keyframe_insert(data_path="rotation_euler", frame=18, group="DoorHinge")
    for curve in action.fcurves:
        for point in curve.keyframe_points:
            point.interpolation = "BEZIER"
            point.easing = "AUTO"
    action.use_fake_user = True
    action.asset_mark()
    action["motion"] = "side-hinged"
    action["duration_seconds"] = 0.6
    return action


def author_actions(hinge: bpy.types.Object) -> dict[str, bpy.types.Action]:
    actions = {
        "open": create_action(hinge, "open", 0.0, -96.0),
        "close": create_action(hinge, "close", -96.0, 0.0),
    }
    hinge.animation_data.action = None
    for name, action in actions.items():
        track = hinge.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 0, action)
        # Keep the editable source in its canonical closed pose. The glTF NLA
        # exporter still emits each muted track as a separately named clip.
        track.mute = True
    hinge.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.scene.frame_set(0)
    return actions


def mesh_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects if obj.type == "MESH" for corner in obj.bound_box]
    if not points:
        raise RuntimeError("Door contains no visible mesh geometry")
    return (
        Vector(tuple(min(point[index] for point in points) for index in range(3))),
        Vector(tuple(max(point[index] for point in points) for index in range(3))),
    )


def mesh_statistics(objects: list[bpy.types.Object]) -> tuple[int, int, int]:
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    materials = {material.name for obj in meshes for material in obj.data.materials if material is not None}
    return len(meshes), triangles, len(materials)


def validate_asset(
    root: bpy.types.Object,
    hinge: bpy.types.Object,
    objects: list[bpy.types.Object],
    actions: dict[str, bpy.types.Action],
) -> tuple[Vector, int, int, int]:
    bpy.context.view_layer.update()
    minimum, maximum = mesh_bounds(objects)
    dimensions = maximum - minimum
    if not (-0.002 <= minimum.x <= 0.002 and 0.798 <= maximum.x <= 0.802):
        raise RuntimeError(f"Door lost its hinge-edge width bounds: {tuple(minimum)}..{tuple(maximum)}")
    if not (-0.002 <= minimum.z <= 0.002 and 1.598 <= maximum.z <= 1.602):
        raise RuntimeError(f"Door lost its source height bounds: {tuple(minimum)}..{tuple(maximum)}")
    if dimensions.y > 0.31:
        raise RuntimeError(f"Door details exceed the low-profile depth budget: {dimensions.y:.3f}m")
    if root.matrix_world.translation.length > 0.0001 or hinge.matrix_world.translation.length > 0.0001:
        raise RuntimeError("Door root and leaf hinge must share the zero hinge pivot")
    if objects[0].get("authored_source") != SOURCE_RELATIVE or not objects[0].get("retained_finished_mesh"):
        raise RuntimeError("The finished Kenney door body was not retained as the asset base")
    if any(not obj.get("authored_source") or not obj.get("retained_finished_mesh") for obj in objects):
        raise RuntimeError("Every exported mesh must retain finished authored-source geometry")
    if any(obj.get("authored_detail") for obj in objects):
        raise RuntimeError("Generated project-detail geometry is forbidden in the runtime door")
    source_objects = sorted(str(obj.get("source_object", "")) for obj in objects[1:])
    if source_objects != [
        CHINESE_SOURCE_OBJECT,
        CHINESE_SOURCE_OBJECT,
    ]:
        raise RuntimeError(f"Authored Chinese component mapping changed: {source_objects}")
    if any(obj.get("source_mesh") != CHINESE_SOURCE_MESH for obj in objects[1:]):
        raise RuntimeError("Authored Chinese components lost the Temple 2 source-mesh mapping")
    material_names = {material.name for obj in objects for material in obj.data.materials if material is not None}
    if material_names != EXPECTED_MATERIALS:
        raise RuntimeError(f"Unexpected PBR material set: {sorted(material_names)}")
    for material in (bpy.data.materials[name] for name in EXPECTED_MATERIALS):
        if not material.use_nodes or material.node_tree.nodes.get("Principled BSDF") is None:
            raise RuntimeError(f"{material.name} is not a Principled PBR material")
    mesh_count, triangles, material_count = mesh_statistics(objects)
    if mesh_count != 3 or triangles != 11334:
        raise RuntimeError(f"Door mesh/triangle budget failed: meshes={mesh_count} triangles={triangles}")
    expected_angles = {"open": (0.0, -96.0), "close": (-96.0, 0.0)}
    for name, (start, end) in expected_angles.items():
        hinge.animation_data.action = actions[name]
        for frame, expected in ((0, start), (18, end)):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            actual = math.degrees(hinge.rotation_euler.z)
            if abs(actual - expected) > 0.1:
                raise RuntimeError(f"{name} animation angle failed at frame {frame}: {actual:.3f}")
    hinge.animation_data.action = None
    hinge.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.scene.frame_set(0)
    return dimensions, mesh_count, triangles, material_count


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_preview(hinge: bpy.types.Object) -> None:
    scene = bpy.context.scene
    preview_material = pbr_material("PreviewStudioClay", (0.052, 0.060, 0.066, 1.0), 0.0, 0.78)
    bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0.4, 0.15, -0.012))
    floor = bpy.context.object
    floor.name = "PreviewFloor"
    floor.data.materials.append(preview_material)
    bpy.ops.mesh.primitive_plane_add(size=6.0, location=(0.4, 0.42, 1.75), rotation=(math.pi * 0.5, 0.0, 0.0))
    wall = bpy.context.object
    wall.name = "PreviewWall"
    wall.data.materials.append(preview_material)
    bpy.ops.object.camera_add(location=(2.22, -3.05, 1.74))
    camera = bpy.context.object
    camera.name = "JianghaiDoorPreviewCamera"
    camera.data.lens = 58
    look_at(camera, Vector((0.40, 0.0, 0.82)))
    scene.camera = camera
    for name, location, energy, size, color in (
        ("PreviewKey", (-1.7, -2.2, 3.0), 760.0, 2.3, (1.0, 0.70, 0.48)),
        ("PreviewFill", (2.2, -1.2, 2.0), 460.0, 2.0, (0.48, 0.68, 1.0)),
        ("PreviewRim", (0.2, 1.7, 2.8), 640.0, 1.8, (1.0, 0.45, 0.22)),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        look_at(light, Vector((0.40, 0.0, 0.84)))
    world = bpy.data.worlds.new("JianghaiDoorPreviewWorld")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs["Color"].default_value = (0.006, 0.009, 0.014, 1.0)
        background.inputs["Strength"].default_value = 0.24
    scene.world = world
    hinge.rotation_euler = (0.0, 0.0, math.radians(-17.0))
    bpy.context.view_layer.update()
    PREVIEW_PNG.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(PREVIEW_PNG)
    bpy.ops.render.render(write_still=True)
    hinge.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    if not PREVIEW_PNG.is_file():
        raise RuntimeError("Blender did not render the Jianghai door preview")


def save_source() -> None:
    SOURCE_BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND), compress=True, check_existing=False)
    if not SOURCE_BLEND.is_file():
        raise RuntimeError("Blender did not save the editable Jianghai lattice door source")


def export_glb(root: bpy.types.Object) -> None:
    OUTPUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_GLB), export_format="GLB", use_selection=True,
        export_apply=True, export_yup=True, export_cameras=False, export_lights=False,
        export_animations=True, export_animation_mode="NLA_TRACKS", export_nla_strips=False,
        export_optimize_animation_size=True, export_force_sampling=True,
        export_frame_range=False, export_materials="EXPORT", export_image_format="AUTO",
        export_extras=True,
    )
    if "FINISHED" not in result or not OUTPUT_GLB.is_file():
        raise RuntimeError(f"Blender could not export {OUTPUT_GLB}: {result}")


def glb_json(path: Path) -> dict[str, object]:
    payload = path.read_bytes()
    if len(payload) < 20 or payload[:4] != b"glTF":
        raise RuntimeError(f"{path.name} is not a binary glTF")
    _, version, total_length = struct.unpack_from("<III", payload, 0)
    chunk_length, chunk_type = struct.unpack_from("<II", payload, 12)
    if version != 2 or total_length != len(payload) or chunk_type != 0x4E4F534A:
        raise RuntimeError(f"{path.name} has an invalid GLB header")
    return json.loads(payload[20 : 20 + chunk_length].decode("utf-8"))


def canonicalize_float_accessors(path: Path) -> None:
    """Remove insignificant DCC-evaluation jitter from serialized GLB floats."""
    payload = bytearray(path.read_bytes())
    json_length = struct.unpack_from("<I", payload, 12)[0]
    document = json.loads(payload[20 : 20 + json_length].decode("utf-8"))
    binary_header = 20 + json_length
    binary_length, binary_type = struct.unpack_from("<II", payload, binary_header)
    if binary_type != 0x004E4942 or binary_header + 8 + binary_length > len(payload):
        raise RuntimeError("Runtime door has an invalid GLB binary chunk")
    binary_start = binary_header + 8
    component_counts = {
        "SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
        "MAT2": 4, "MAT3": 9, "MAT4": 16,
    }
    views = document.get("bufferViews", [])
    for accessor in document.get("accessors", []):
        if accessor.get("componentType") != 5126 or "bufferView" not in accessor:
            continue
        view = views[accessor["bufferView"]]
        components = component_counts[accessor["type"]]
        stride = view.get("byteStride", components * 4)
        start = binary_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        for row in range(accessor["count"]):
            for component in range(components):
                offset = start + row * stride + component * 4
                value = struct.unpack_from("<f", payload, offset)[0]
                rounded = round(value, 6)
                if abs(rounded) < 0.0000005:
                    rounded = 0.0
                struct.pack_into("<f", payload, offset, rounded)
    path.write_bytes(payload)


def verify_glb(expected_dimensions: Vector) -> tuple[Vector, int, int, int, list[str]]:
    document = glb_json(OUTPUT_GLB)
    if any("uri" in entry for entry in document.get("buffers", [])):
        raise RuntimeError("Runtime door depends on an external buffer")
    if any("uri" in entry for entry in document.get("images", [])):
        raise RuntimeError("Runtime door depends on an external image")
    animations = sorted(str(entry.get("name", "")) for entry in document.get("animations", []))
    if animations != ["close", "open"]:
        raise RuntimeError(f"Runtime door animation set changed: {animations}")
    nodes = {str(entry.get("name", "")): entry for entry in document.get("nodes", [])}
    for name in (ROOT_NAME, HINGE_NAME):
        if name not in nodes:
            raise RuntimeError(f"Runtime door lost required node {name}")
        translation = nodes[name].get("translation", [0.0, 0.0, 0.0])
        if Vector(translation).length > 0.0001:
            raise RuntimeError(f"Runtime door moved {name} away from the hinge pivot: {translation}")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    configure_scene()
    result = bpy.ops.import_scene.gltf(filepath=str(OUTPUT_GLB), import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"Blender could not round-trip {OUTPUT_GLB.name}: {result}")
    root = bpy.data.objects.get(ROOT_NAME)
    hinge = bpy.data.objects.get(HINGE_NAME)
    if root is None or hinge is None:
        raise RuntimeError("Round-trip lost the door root or hinge")
    imported_actions = {action.name: action for action in bpy.data.actions}
    for name, (start, end) in {"open": (0.0, -96.0), "close": (-96.0, 0.0)}.items():
        action = imported_actions.get(name)
        if action is None:
            raise RuntimeError(f"Round-trip lost the {name} action datablock")
        hinge.animation_data.action = action
        for frame, expected in ((0, start), (18, end)):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            actual = math.degrees(hinge.rotation_quaternion.to_euler("XYZ").z)
            if abs(actual - expected) > 0.1:
                raise RuntimeError(f"Round-trip {name} angle failed at frame {frame}: {actual:.3f}")
    hinge.animation_data_clear()
    hinge.rotation_mode = "XYZ"
    hinge.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.scene.frame_set(0)
    bpy.context.view_layer.update()
    imported = [obj for obj in root.children_recursive if obj.type == "MESH"]
    minimum, maximum = mesh_bounds(imported)
    dimensions = maximum - minimum
    if any(abs(dimensions[index] - expected_dimensions[index]) > 0.008 for index in range(3)):
        raise RuntimeError(f"GLB bounds changed: expected={tuple(expected_dimensions)} actual={tuple(dimensions)}")
    if not (-0.003 <= minimum.x <= 0.003 and 0.797 <= maximum.x <= 0.803):
        raise RuntimeError(f"GLB hinge-edge bounds changed: {tuple(minimum)}..{tuple(maximum)}")
    if (
        root.get("source_license") != "CC0-1.0"
        or root.get("hinge_axis") != "+Y in Godot / +Z in Blender"
        or root.get("chinese_source") != CHINESE_SOURCE_RELATIVE
    ):
        raise RuntimeError("GLB lost its license or hinge metadata")
    source_objects = sorted(str(obj.get("source_object")) for obj in imported if obj.get("source_object"))
    if source_objects != [
        CHINESE_SOURCE_OBJECT,
        CHINESE_SOURCE_OBJECT,
    ]:
        raise RuntimeError(f"GLB lost the authored Chinese component mapping: {source_objects}")
    mesh_count, triangles, material_count = mesh_statistics(imported)
    if (
        mesh_count != 3
        or triangles != 11334
        or material_count != len(EXPECTED_MATERIALS)
    ):
        raise RuntimeError(f"GLB budget changed: meshes={mesh_count} triangles={triangles} materials={material_count}")
    return dimensions, mesh_count, triangles, material_count, animations


def build() -> None:
    configure_scene()
    materials = create_palette()
    root = empty(ROOT_NAME)
    root["source_creator"] = "Kenney; Free poly"
    root["source_url"] = f"https://kenney.nl/assets/factory-kit; {CHINESE_SOURCE_URL}"
    root["source_license"] = "CC0-1.0"
    root["source_asset"] = f"{SOURCE_RELATIVE}; {CHINESE_SOURCE_ASSET}"
    root["chinese_source"] = CHINESE_SOURCE_RELATIVE
    root["chinese_source_objects"] = (
        f"{CHINESE_SOURCE_OBJECT}/{CHINESE_SOURCE_MESH}/material_index={CHINESE_SOURCE_MATERIAL_INDEX}"
    )
    root["adaptation_author"] = "Operation Steel Tide project"
    root["adaptation_license"] = "MIT"
    root["design"] = "Jianghai Old City Chinese lattice hinge door"
    root["units"] = "meters"
    root["hinge_axis"] = "+Y in Godot / +Z in Blender"
    root["hinge_origin_m"] = "0,0,0"
    hinge = empty(HINGE_NAME, root)
    hinge["gameplay_pivot"] = "left-edge side hinge"
    hinge["open_angle_degrees"] = -96.0
    base = import_authored_leaf(hinge, materials["base"])
    details = append_authored_components(hinge, materials)
    objects = [base, *details]
    actions = author_actions(hinge)
    dimensions, mesh_count, triangles, material_count = validate_asset(root, hinge, objects, actions)
    render_preview(hinge)
    save_source()
    export_glb(root)
    canonicalize_float_accessors(OUTPUT_GLB)
    verified_dimensions, verified_meshes, verified_triangles, verified_materials, animations = verify_glb(dimensions)
    digest = hashlib.sha256(OUTPUT_GLB.read_bytes()).hexdigest().upper()
    chinese_source_digest = hashlib.sha256(CHINESE_SOURCE_BLEND.read_bytes()).hexdigest().upper()
    print(
        "JIANGHAI_LATTICE_DOOR_ASSET "
        f"bounds_m={dimensions.x:.3f}x{dimensions.y:.3f}x{dimensions.z:.3f} "
        f"verified_m={verified_dimensions.x:.3f}x{verified_dimensions.y:.3f}x{verified_dimensions.z:.3f} "
        f"meshes={mesh_count} triangles={triangles} materials={material_count} "
        f"roundtrip_meshes={verified_meshes} roundtrip_triangles={verified_triangles} "
        f"roundtrip_materials={verified_materials} animations={','.join(animations)} "
        f"pivot=0,0,0 kenney_sha256={SOURCE_SHA256} "
        f"chinese_source_sha256={chinese_source_digest} glb_sha256={digest} "
        f"blend_bytes={SOURCE_BLEND.stat().st_size} glb_bytes={OUTPUT_GLB.stat().st_size} "
        f"preview_bytes={PREVIEW_PNG.stat().st_size} texture_bytes={OUTPUT_TEXTURE.stat().st_size}"
    )
    print(
        "JIANGHAI_LATTICE_DOOR_PASS valid=True authored_base=True "
        "authored_chinese_components=2 generated_runtime_geometry=0 pbr=True embedded=True"
    )


if __name__ == "__main__":
    build()
