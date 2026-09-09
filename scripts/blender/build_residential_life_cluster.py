"""Build the authored residential life cluster used by the survival map.

This is an offline Blender DCC build.  The exported GLB is the visible art; the
runtime only instances it and owns gameplay collision/loot anchors.
"""
import bpy, math, os
from mathutils import Vector

OUT = os.path.abspath("assets/models/residential_life_cluster/residential_life_cluster.glb")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name, color, metallic=0.0, rough=0.7, emission=None):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=(*color,1); bs.inputs['Roughness'].default_value=rough; bs.inputs['Metallic'].default_value=metallic
    if emission:
        bs.inputs['Emission Color'].default_value=(*emission,1); bs.inputs['Emission Strength'].default_value=2.0
    return m

concrete=mat('Concrete',(0.34,0.38,0.4),rough=.9); brick=mat('Warm Brick',(0.46,0.22,0.16),rough=.85); glass=mat('Blue Glass',(0.08,0.24,0.3),metallic=.35,rough=.18); frame=mat('Window Frames',(0.05,0.07,0.08),metallic=.7,rough=.25); roof=mat('Roof Metal',(0.12,0.15,0.16),metallic=.75,rough=.35); asphalt=mat('Plaza Asphalt',(0.08,0.1,0.11),rough=.95); tile=mat('Plaza Tile',(0.32,0.38,0.35),rough=.8); accent=mat('Shop Accent',(0.8,0.42,0.12),rough=.5); green=mat('Planter Green',(0.12,0.3,0.18),rough=.9); light=mat('Interior Light',(1.0,0.55,0.2),emission=(1.0,.28,.08),rough=.35)

def box(name, loc, scale, material, bevel=.08):
    bpy.ops.mesh.primitive_cube_add(location=loc); o=bpy.context.object; o.name=name; o.dimensions=scale; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new('EdgeSoftening','BEVEL'); mod.width=bevel; mod.segments=2
    return o

def sign(name, text, loc, size, material):
    c=box(name+'Backing', (loc[0],loc[1],loc[2]), (size[0],size[1],.12), material, .03)
    bpy.ops.object.text_add(location=(loc[0],loc[1]+size[1]*.05,loc[2]-.08), rotation=(math.pi/2,0,0)); t=bpy.context.object; t.name=name; t.data.body=text; t.data.align_x='CENTER'; t.data.size=min(size[0]*.11,.75); t.data.extrude=.015; t.data.materials.append(light)

# Ground and a pedestrian forecourt.
box('DistrictPlazaGround',(0,-.12,0),(70,.24,58),asphalt,.02)
box('CentralPedestrianCourt',(0,.03,2),(31,.08,25),tile,.02)
for x in (-14,14):
    for z in (-10,10):
        box('Planter', (x,.5,z),(2.2,1.0,2.2),concrete,.15); box('Shrub',(x,.98,z),(1.45,.85,1.45),green,.35)

# Four-storey residential block on the west edge, with recessed balconies and lit lobby.
for floor in range(4):
    y=1.5+floor*3.2
    box(f'ApartmentFloor{floor}',(-22,y,2),(15,3.0,20),brick,.12)
    for x in (-27.0,-22.0,-17.0):
        box('ApartmentWindow',(x,y+.25,-8.15),(3.0,1.55,.18),glass,.03); box('WindowMullion',(x,y+.25,-8.28),(.14,1.8,.12),frame,.01)
        box('ApartmentWindow',(x,y+.25,12.15),(3.0,1.55,.18),glass,.03)
    for x in (-27,-22,-17):
        box('BalconySlab',(x,y-.65,12.6),(3.8,.18,2.4),concrete,.03); box('BalconyRail',(x,y+.15,11.5),(3.6,1.0,.08),frame,.02)
box('ApartmentRoof',(-22,14.45,2),(15,.5,20),roof,.08)
box('ApartmentLobby',(-22,1.4,12.35),(7.0,2.8,.35),glass,.02); sign('ApartmentSign','HARBOR COURT',(-22,4.0,12.0),(7.0,1.2),accent)

# Supermarket: deep hall, glass curtain wall, canopy, loading bay and visible shelf rows.
box('SupermarketShell',(12,4.0,-3),(26,8.0,18),concrete,.16); box('SupermarketRoof',(12,8.3,-3),(27,.5,19),roof,.12)
box('SupermarketCurtainWall',(12,3.0,6.15),(20,5.2,.22),glass,.03)
for x in (3,8,13,18,23): box('SupermarketMullion',(x,3.0,6.0),(.12,5.4,.3),frame,.01)
box('SupermarketCanopy',(12,6.1,7.5),(22,.25,3.3),accent,.06)
sign('SupermarketSign','NORTHSTAR MARKET',(12,7.2,7.35),(13,1.5),accent)
for x in (4,9,14,19):
    for z in (-8,-3,2): box('ShelfAisle',(x,1.0,z),(1.2,1.8,3.4),frame,.05)
box('LoadingDock',(12,.6,-13),(12,1.1,3),concrete,.08); box('RollerDoor',(12,3.1,-13.2),(7,4.5,.2),roof,.02)

# Shopping plaza east wing: three small storefronts around a covered arcade.
for i,x in enumerate((29,39,49)):
    box(f'PlazaShop{i}',(x,2.7,1),(8,5.4,12),brick if i%2==0 else concrete,.1)
    box('Shopfront',(x,2.5,7.1),(6.3,4.3,.2),glass,.03); sign(f'ShopSign{i}',('PHARMACY','BAKERY','HARDWARE')[i],(x,5.6,6.9),(5.8,1.0),accent)
box('ArcadeRoof',(39,6.0,7.8),(29,.35,3.0),roof,.06)
for x in (29,39,49): box('ArcadeColumn',(x,3.0,8.0),(.28,6.0,.28),frame,.02)

# Street furniture gives readable scale and life without blocking traversal.
for x in (-10,0,10): box('Bench',(x,.65,14),(3.0,.65,.65),concrete,.12)
for x in (-31,-5,21,55):
    box('LampPost',(x,2.0,17),(.18,4.0,.18),frame,.03); box('LampGlow',(x,4.15,17),(.45,.2,.45),light,.08)

bpy.ops.wm.save_as_mainfile(filepath=os.path.splitext(OUT)[0]+'.blend')
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB', use_selection=False, export_apply=True)
print('EXPORTED', OUT)
