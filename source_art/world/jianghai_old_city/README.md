# Jianghai Old City DCC Source

`jianghai_old_city.blend` is the editable, packed Blender source for the static
Jianghai Old City extraction-map visual exported to
`assets/models/jianghai_old_city/jianghai_old_city.glb`.

The scene is a project-authored 1990s Lingnan river-industrial city
composition. The complete layout, streets, supporting geometry, district
assembly, material adaptations, art direction, Chinese sign wording and
placement, lighting used for review renders, objective-terminal status screens
and adaptations, factory-gate portal and hinged-entry facade composition,
urban-life dressing, facade expansion, authored valley foundation, ground and
mountain instance placement/orientation composition, and integration of
licensed source modules are produced for
Operation Steel Tide. Each terminal body
combines the CC0 Utility Box 01 and Television 02 sources recorded below; only
its small status screen and adaptation work are project-authored. Godot
continues to own gameplay collision, navigation, loot, spawning, doors, and
mission state.

## 2026-08-29 rebuild and 2026-08-30 infill/interior expansion

The current delivered district is the project-authored Blender rebuild made on
2026-08-29 and expanded on 2026-08-30. It replaces 66 previously visible
building anchors with three reviewed Chinese-profile shared meshes and expands
the authored perimeter table to 50 density placements. Eight west/east infill
buildings close formerly sparse blocks without adding a new asset or texture.
Twelve reviewed shops and residences receive unique human-scale doorway
apertures, authored offline by deterministic mesh-plane splitting and face
removal so the non-manifold joined facades cannot retain an invisible boolean
plug. Each opening has a five-sided opaque DCC-authored liner sharing one small
mesh and the existing brick material vocabulary, so a room cannot reveal the
outside world through its walls. The rebuild is serialized in the authoritative
`.blend` and exported to the runtime GLB; it is not runtime procedural geometry.
The final correction retires all 35 remaining photographic facade planes under
the exact `JianghaiExpansion_Facade_EastPhoto_` (17 objects) and
`JianghaiExpansion_Facade_WestClock_` (18 objects) prefixes. It does not touch
the real `EastPhotoHouse` mesh doorway, the 16 `PawnshopAuthoredWing_` facade
modules, road decals, or other legitimate thin authored details. The
`GuangchangClanHall` source mesh also loses only its measured baked static gate
assembly: 56 disconnected leaf/seam/hardware islands, 425 vertices, and 323
triangles. Its authored jambs, arched lintel, and threshold remain around a
3.687810-by-4.028527-meter opening.

The source mapping is:

- `JianghaiChineseTempleHall_LOD`: an adapted LOD of Free poly's CC0
  **Chinese Temple 2**;
- `JianghaiChineseArcadeShop_LOD`: a clean Quaternius Buildings Pack body
  composed in Blender with adapted VVayToyek pavilion facade/eaves parts and an
  extracted, decimated **Chinese Temple 2** roof; and
- `JianghaiChineseGateHouse_LOD`: a second clean Quaternius Buildings Pack body
  composed with the same licensed pavilion-detail and Temple-roof vocabulary.

The existing CC0 Chinese red lamps and porcelain lions remain available as
locally specific dressing, while the existing Poly Haven facade, prop, and PBR
sources continue to serve the surrounding district. No new external asset was
acquired for this rebuild. Repeated buildings share mesh datablocks, the runtime
texture cap is 512 pixels, and the three profiles deliberately reuse a small
licensed material vocabulary to reduce loading and rendering pressure.
The four repeated Quaternius density profiles preserve their exact scalar face
colors in glTF `COLOR_0` while using one opaque material each. Across their 22
instances this reduces material-backed instance surfaces from 131 to 22 without
changing geometry, roughness, placement, collision, images, or textures.

`Scan Old Building Street` and `Old Urban building` are retained below only as
historical acquisition and license records. Their former placements were
superseded by the rebuild: the current authoritative scene and delivered GLB
contain zero visible instances of either retired building source.

The authoritative outputs and representative player-scale review renders are:

- `source_art/world/jianghai_old_city/jianghai_old_city.blend`
- `assets/models/jianghai_old_city/jianghai_old_city.glb`
- `source_art/props/jianghai_lattice_door/jianghai_lattice_door.blend`
- `assets/models/jianghai_old_city/jianghai_lattice_door.glb`
- `previews/12_chinese_edge_gate.png`
- `previews/13_chinese_avenue.png`
- `previews/14_chinese_old_city_overview.png`

The final packed `.blend` is 52,107,580 bytes with SHA-256
`DA1907CE44D694960BB959460B6339C3796D31D76CB5111048DEDDD199D918CE`.
It contains 587 objects / 490 mesh objects, 200 unique mesh datablocks,
3,028,604 mesh-object triangles, 1,086,828 triangles counted once per unique
mesh, and resolves to 553 evaluated mesh objects / 3,057,530 evaluated instance
triangles. The final GLB is 63,204,100 bytes with SHA-256
`33E2176E52D538123D45ABEEA61464EA8E7002854A2B1D47E5C5D560AD32D17E`.
Its JSON audit reports 571 total nodes, 553 mesh nodes, 263 unique meshes, 450
primitives, and 1,115,644 unique / 3,056,190 instanced triangles, with 96
materials, 139 textures, 120 images, and a 512-pixel maximum image
dimension. The authoritative scene audit passes with all 50 density placements
marked for lightweight gameplay-proxy coverage, 12 enterable residences,
108/108 building-body doorway samples, 36/36 structural wall/lintel samples,
108/108 full-scene doorway samples, 12/12 opaque interior liners, zero density
intersections, zero retired photographed facade objects, and zero visible
retired-building instances. The clan-hall audit additionally proves 0 residual
static gate islands, 9/9 open passage rays, 6/6 jamb rays, 3/3 lintel rays, and
3/3 retained-threshold rays. A negative regression fixture proves the
full-scene residence gate catches a 0.404-meter facade obstruction.

Against the immediately preceding `F4E59781...` / `3A854DD9...` artifact, the
correction changes the saved scene by -34 total objects (35 mesh overlays
retired, one Empty anchor added), -35 mesh objects, -94,890 mesh-object and
evaluated instance triangles, and -26,864 unique triangles. The GLB changes by
-34 nodes, -35 mesh nodes, -10 unique meshes, -19 primitives, -94,890 instanced
triangles, and -26,864 unique triangles. These deltas include both the 35
overlay objects and the 323-triangle gate-island edit.

`JianghaiClanHallDoubleGateAnchor` is the sole authored Empty for the new
double-hinged gate. Its Blender floor-centre world position is
`(-86.001892, 122.576271, 1.278776)`; Y-up glTF/Godot imports it at
`(-86.001892, 1.278776, -122.576271)` with identity basis. In Godot, local
`+X` spans the opening, `+Y` is up, and `+Z` points outward toward the south
street (Blender `-Y`). Exported extras lock `gate_width_m=3.6878103066`,
`gate_height_m=4.0285267088`, `gate_floor_y_m=1.2787764072`, and
`gate_outward_axis=+Z`. Re-running the exporter records zero additional
removals and repeats the complete source/GLB anchor and passage audit.

Runtime uses 14 instances of the finished Chinese-lattice composite visual:
12 single-leaf residence doors plus two independently hinged leaves forming one
logical clan-hall double gate. The two existing Kenney-only pawnshop/factory
personnel doors are separate and are not included in that 14-instance lattice
count.

## Authoritative source and provenance inputs

`jianghai_old_city.blend` is the authoritative hand-edited DCC source. The
repository-external acquisition paths below record the files used during
authoring and remain useful for provenance or deliberate re-editing; they are
not required to export the packed final scene. The listed paths are relative
to the original `JIANGHAI_ACQUISITION_ROOT` cache. Valley inputs use the
separate private `JIANGHAI_VALLEY_ACQUISITION_ROOT` described below.

```text
poly_haven/modular_factory_facade/modular_factory_facade_1k.gltf
poly_haven/modular_factory_facade/modular_factory_facade.bin
poly_haven/modular_factory_facade/textures/*
poly_haven/modular_urban_apartments_facade/modular_urban_apartments_facade_1k.gltf
poly_haven/modular_urban_apartments_facade/modular_urban_apartments_facade.bin
poly_haven/modular_urban_apartments_facade/textures/*
poly_haven/chinese_tea_table/chinese_tea_table_1k.gltf
poly_haven/chinese_tea_table/chinese_tea_table.bin
poly_haven/chinese_tea_table/textures/*_1k.jpg
poly_haven/chinese_stool/chinese_stool_1k.gltf
poly_haven/chinese_stool/chinese_stool.bin
poly_haven/chinese_stool/textures/*_1k.jpg
poly_haven/hand_truck/hand_truck_1k.gltf
poly_haven/hand_truck/hand_truck.bin
poly_haven/hand_truck/textures/*_1k.jpg
poly_haven/television_02/television_02_1k.gltf
poly_haven/television_02/television_02.bin
poly_haven/television_02/textures/*_1k.jpg
poly_haven/exterior_aircon_unit/exterior_aircon_unit_1k.gltf
poly_haven/exterior_aircon_unit/exterior_aircon_unit.bin
poly_haven/exterior_aircon_unit/textures/*_1k.jpg
poly_haven/rollershutter_window_03/rollershutter_window_03_1k.gltf
poly_haven/rollershutter_window_03/rollershutter_window_03.bin
poly_haven/rollershutter_window_03/textures/*_1k.jpg
poly_haven/trashbag/trashbag_1k.gltf
poly_haven/trashbag/trashbag.bin
poly_haven/trashbag/textures/*_1k.jpg
poly_haven/utility_box_01/utility_box_01_1k.gltf
poly_haven/utility_box_01/utility_box_01.bin
poly_haven/utility_box_01/textures/*_1k.jpg
poly_haven/barrel_03/barrel_03_1k.gltf
poly_haven/barrel_03/barrel_03.bin
poly_haven/barrel_03/textures/*_1k.jpg
poly_haven/plastic_crate_02/plastic_crate_02_1k.gltf
poly_haven/plastic_crate_02/plastic_crate_02.bin
poly_haven/plastic_crate_02/textures/*_1k.jpg
poly_haven/security_camera_01/security_camera_01_1k.gltf
poly_haven/security_camera_01/security_camera_01.bin
poly_haven/security_camera_01/textures/*_1k.jpg
blenderkit/chinese_temple_2.glb
blenderkit/chinese_red_lamp.glb
blenderkit/old_urban_building_1k.blend
blenderkit/scan_old_building_street.glb
blenderkit/chinese_porcelain_lion.glb
blenderkit/pink_city_bicycle/pink_city_bicycle_0_5k.blend
blenderkit/pink_city_bicycle/official_api_search.json
itch/vvaytoyek_chinese_four_corner_pavilion/Chinese Four-corner Pavilion.zip
itch/vvaytoyek_chinese_four_corner_pavilion/fbx_only/四角亭.fbx
itch/vvaytoyek_chinese_four_corner_pavilion/itch_license_evidence.html
NotoSansSC-VF.otf
```

The `old_urban_building_1k.blend` and `scan_old_building_street.glb` paths
remain in this list as historical evidence for the superseded 2026-08-28
layout. They are not required to rebuild or export the current Chinese district
and contribute zero visible delivered instances.

The valley authoring inputs remain in a separate private cache and use these
paths relative to `JIANGHAI_VALLEY_ACQUISITION_ROOT`:

```text
coast_line_01/coast_line_01_2k.gltf
coast_line_01/coast_line_01.bin
coast_line_01/textures/coast_line_01_{diff,arm,nor_gl}_2k.jpg
hero_mountain/Mesh_05K_hero_mountain01.obj
hero_mountain/Color__hero_mountain01.jpg
hero_mountain/Normal_hero_mountain01.png
hero_mountain/Roughness__hero_mountain01.jpg
rocky_terrain/textures/rocky_terrain_{diff,nor_gl,rough}_2k.jpg
rocky_terrain/textures/rocky_terrain_disp_2k.png
gravel_floor_03/textures/gravel_floor_03_{diff,nor_gl,rough}_2k.jpg
gravel_floor_03/textures/gravel_floor_03_disp_2k.png
```

The private evidence cache also retains the original Sketchfab ZIP, its inner
RAR, the selected OBJ and texture originals, the official model API snapshot,
and the CC BY 4.0 legalcode snapshot. Source AO, height, and displacement maps
are preserved there for evidence but are not valley build inputs and are not
embedded. Coastal Cliff 01, Coastal Cliff 02, and Namaqualand Cliff 02 downloads
and API snapshots are likewise retained only as evaluated art-search evidence;
none of those three sources is embedded in or required by the delivered
`.blend` or GLB.

Coast Line 01 contributes terrain geometry only. Its source material and images
remain private evidence and are not embedded. The delivered Coast ground uses
Charlotte Baglioni's CC0 Gravel Floor 03 surface through the packed material
and images described below.

The final DCC scene also incorporates four existing Poly Haven PBR surface
sets under `assets/textures/`: Asphalt 03, Concrete Floor, Gravel Embedded
Concrete, and Corrugated Iron. Rusty Painted Metal remains a tracked project
texture but is not part of the current Jianghai scene. The scene also contains
adapted instances of the already tracked Poly Haven CC0 Old Military Crate and
Concrete Road Barrier models under `assets/models/`. The urban-life pass also
embeds the repository's existing Poly Haven CC0 Coffee Cart 01 and Wicker
Basket 01 sources from `assets/models/polyhaven_residential_street/`. The
external Poly Haven, BlenderKit, and VVayToyek itch.io acquisition files listed
above were packed only after their official CC0 records were checked. Exact
creators, official URLs, acquisition dates, hashes, and mappings are in
`LICENSE_EVIDENCE.md`.

The authored valley environment uses two Poly Haven surface sets acquired on
2026-08-28: **Rocky Terrain** by Amal Kumar,
https://polyhaven.com/a/rocky_terrain, and **Gravel Floor 03** by Charlotte
Baglioni, https://polyhaven.com/a/gravel_floor_03. The shipped ground source is
the finished Poly Haven photogrammetry model **Coast Line 01** by Rob Tuytel
(photography and processing), with cleanup by Rico Cilliers, acquired on
2026-08-29 from https://polyhaven.com/a/coast_line_01. These three Poly Haven
sources are CC0 1.0 Universal under https://polyhaven.com/license and
https://creativecommons.org/publicdomain/zero/1.0/.

The complete mountain source is **Hero Mountain** by **solararchitect**,
published 2021-10-21 and acquired on 2026-08-29 from
https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca
through an original-format download in an existing signed-in Edge session. Its
official model API record is
https://api.sketchfab.com/v3/models/83b3fd690ea44e988d086d5165a5f2ca.
It is licensed under Creative Commons Attribution 4.0,
http://creativecommons.org/licenses/by/4.0/. Distribution of the adapted
mountain must credit solararchitect, retain the source and license links, and
indicate the modifications described below.

Coast Line 01 is adapted into `JianghaiPerimeterGroundComposite`, one saved DCC
mesh assembled from eight modeled scan placements. The final mesh has 84,960
vertices, 168,480 triangles, one connected component, two boundary loops
totaling 1,440 edges, zero degenerate triangles, and zero invalid face normals.
Its bounds are X `-600.878..600.853`, Y `-540.340..660.056`, and Z
`-12.7965..5.0390` meters, for 17.835 meters of modeled vertical relief.

The final ground connection is authored mesh deformation, not camera masking
and not a primitive or runtime-procedural visible substitute. The transition is
driven by signed distance from the actual projected top footprint of
`OldCityFoundation`: 25 top faces, 32 vertices, and 16 projected boundary edges,
with X bounds `-169.998..169.998` and Blender-Y bounds
`-99.998..219.998`. It keeps the footprint and safety margin below the platform
and blends the modeled Coast relief outward. Coverage is 1.000, maximum
foundation gap is 0.103 meters, and the safe-area highest ground is -0.120
meters. Relief is 0.969 meters at 0-60 meters from the foundation and 3.955
meters at 60-160 meters; slope RMS/p90/p99/maximum is
0.0579/0.0869/0.2331/0.6620. The full ring gate passes 7,920/7,920 probes.

The ground uses Gravel Floor 03 diffuse, OpenGL-normal, and roughness maps with
base-color factor `(0.92, 0.78, 0.62, 1.0)` and a 7-meter affine world-XY UV
layout. Maximum DCC and serialized-GLB UV coordinate errors are `3.27e-6` and
`4.36e-6`, within the `1.2e-5` gate, and both Jacobian checks pass. No source
Coast material or image is embedded; Coast material/image counts are 0/0.

Hero Mountain is decimated to one shared 14,000-triangle distant LOD and
composed as 12 visual-only instances in staggered six-object
inner and outer rings. Blender rebuilds the Hero Mountain PBR nodes from its
Color, Normal, and Roughness maps, caps/packs the selected images at 1024
pixels, and applies uniform scale, rotation, and the reviewed two-ring
composition. Its AO, height, and
displacement maps remain private and unused by the delivered material or
geometry. Gravel Floor 03 dresses the top of the existing project-authored
`OldCityFoundation` and the visible Coast-derived ground; Rocky Terrain dresses
the foundation sides. Their verified
displacement maps likewise remain private and do not generate delivered visible
geometry. The foundation mesh, applied hand chamfer, placement, UV layout,
material split, and placement/orientation composition of the finished ground
and mountains are project-authored DCC work. All raw source bundles remain
private and uncommitted. Packing does not relicense the embedded CC0 or CC BY
4.0 content as MIT.

The repository retains the Poly Haven CC0 HDRI **Kloppenheim 06 (Pure Sky)** by
Greg Zaal (Original), with sky edits by Jarod Guest. It was
acquired on 2026-08-28 from
https://polyhaven.com/a/kloppenheim_06_puresky. The official 1K HDR download is
https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/kloppenheim_06_puresky_1k.hdr;
the local `assets/textures/kloppenheim_06_puresky_1k.hdr` is 1,173,154 bytes,
has SHA-256
`206C67E3A1B992282821CF06662BDD69BBB4915C1C4444A66338A40D6A7D4E34`,
and matches official API MD5 `995d68b1656f26452572645c0ffe898b`.
It is not packed into the authoritative `.blend` or embedded in the map GLB.
The current `JianghaiOldCityAtmosphere` uses a procedural sky and does not load
this panorama.

The Poly Haven **Modular Urban Apartments Facade** remains a delivered source
through the 16 solid/insert `PawnshopAuthoredWing_` modules and their packed
materials. Its former 36-object east/west photographic overlay composition is
historical: one East Photo insert was retired with the doorway pass, and the
final DCC cleanup retires the remaining 35 overlay objects. None of those two
exact overlay prefixes occurs in the authoritative `.blend` or runtime GLB.

The Guangchang pawnshop gate uses 15 retained modeled components from
VVayToyek's CC0 **Chinese Four-corner Pavilion - Free**. Blender reshapes its
timber columns, tiled eaves, rafters, hanging lattice, brackets, and ornaments
into a shallow street gate. Eight solid Poly Haven apartment-facade wall
modules and eight authored door/window inserts form the two side wings. The six
former flat gate boards and twelve zero-thickness south-wall objects are absent
from the saved scene and rejected by both export and audit scripts.

The pawnshop and factory personnel entrances additionally reuse two finished
CC0 modules from Quaternius's **Downtown City MegaKit**: 18 packed instances of
`assets/models/quaternius_downtown_city/Brick_Plain_1.gltf` and two packed
instances of `DoorFrame_Trim.gltf`. Each entrance facade contains nine brick
modules and one doorframe, for 10 DCC-authored visible objects per facade. These
modules infill the former oversized route around a human-scale 1.45-by-2.65-meter
opening. The two current runtime doors are separate Kenney Factory Kit CC0
`assets/models/kenney_factory_kit/door-hinged.glb` instances that open with a
normal 96-degree side-hinged swing.

The 12 enterable rooms use a separate red-and-gold composite
door. Its retained finished geometry combines the same Kenney CC0 hinged leaf
with material 2 of Free poly's CC0 **Chinese Temple 2**
`GuangchangClanHall` / `网格.002` arched grille, reduced and fitted in Blender.
Project-authored work is limited to the packed red-wood lacquer texture and
material adaptation; no door component is generated from primitives. The
brighter red-wood lacquer and dark-gold base colors preserve readability under
the eaves without emission, additional lights, or runtime cost. The exact
extraction, license mapping, and rebuild contract are in
`source_art/props/jianghai_lattice_door/README.md`.

The final door source is a 1,162,441-byte `.blend` with SHA-256
`72D41DB8125BB5DDDEE04DE14E6AA5C9D8B1D4D5058823B74CC52968D78C9445`.
Its 412,548-byte runtime GLB has SHA-256
`FBE9FC3EBB1F8BB49842442F1A4AEF451E0F67E5B3FF95BBB16A6F01B84D5528`
and contains three mesh nodes, two unique meshes/two surfaces, 5,745 unique /
11,334 instanced triangles, two PBR materials, one 256-square texture/image, and
two 18-frame, 0.6-second, 96-degree clips. The 63,926-byte source and Godot
extraction-target red-wood textures are byte-identical with SHA-256
`C75ED94A13A4F21CE518F455916802117D193FCE7A5731A0A4A602F82FD43834`.

The repository-external cache is neither a runtime lookup nor an export
dependency. The authoritative `.blend` already contains the adapted source
geometry, materials, and textures. Do not replace an embedded source with a
similarly named marketplace asset unless its exact creator, source URL,
license, acquisition date, and evidence record have first been verified.

### Build or refresh the valley environment

The valley build is an offline Blender authoring step against the authoritative
`.blend`; it does not run in Godot. Point the environment variable at the
verified private cache, then run:

```powershell
$env:JIANGHAI_VALLEY_ACQUISITION_ROOT = '<private-cache-root>'
blender --background source_art/world/jianghai_old_city/jianghai_old_city.blend --python scripts/blender/build_jianghai_valley_environment.py
```

The script verifies all 17 selected private authoring inputs against the
recorded source digests before editing, removes only an earlier
`JianghaiValleyEnvironment` hierarchy, authors and validates the replacement,
caps packed DCC images at 1024 pixels, and saves the result into the
authoritative scene. It does not copy raw downloads into the repository. Coast
Line 01 contributes geometry only: its verified source material and images are
not embedded, and the finished ground instead uses the Gravel Floor 03 material
and three image datablocks. Run the normal export step below afterward
to refresh the runtime GLB. Exact source SHA-256/MD5 values, retained API and
license snapshots, and the
private-cache-to-delivery mapping are in `LICENSE_EVIDENCE.md`.

The builder, read-only scene audit, export guard, and serialized-GLB round-trip
audit verify the single ground identity, exact topology and bounds, signed-
distance foundation transition, coverage and slope bands, Gravel Floor 03
material/UV contract, road-end ray probes, and every mountain transform. The
ground ring gate passes 7,920/7,920 probes; both six-object mountain rings retain
their indexed staggered layout.

## Edit and export

Make composition, modeling, material, lighting, and sign changes directly in
`source_art/world/jianghai_old_city/jianghai_old_city.blend`. From the
repository root, export that saved scene with:

```powershell
blender --background source_art/world/jianghai_old_city/jianghai_old_city.blend --python scripts/blender/export_jianghai_old_city.py
```

The export script verifies that the authoritative scene is open, removes the
documented obsolete signs, retired asset metadata, and the exact 35-object
photographic-facade retirement set. The focused
`scripts/blender/jianghai_clan_hall_portal.py` helper removes or validates the
56-island baked gate contract and authors the sole Empty anchor; the focused
`scripts/blender/jianghai_retired_facades.py` helper owns the two exact overlay
prefixes. The export then validates the 66-anchor Chinese-profile replacement,
the 50-placement density table, authored pawnshop canopy/wings, 9/9 clan-hall
passage probes, retained jamb/lintel/threshold probes, and raw GLB/Godot anchor
transform. It applies runtime material tuning, flattens tiled images, rebuilds
the two ten-piece Quaternius entry facades, caps runtime textures at 512
pixels, recompresses eligible high-resolution runtime images as JPEG quality
90, rejects non-built-in font datablocks, packs external data, saves the
`.blend`, and exports:

- `assets/models/jianghai_old_city/jianghai_old_city.glb`
- `assets/models/jianghai_old_city/rollershutter_window_03.glb`

The second output is a reproducible standalone PBR shutter visual. The script
selects `JianghaiArtPass_EastShutter00`, makes a temporary normalized copy, and
exports only that adapted Rollershutter Window 03 mesh and its materials. The
result is 187,940 bytes with SHA-256
`C4884AFCD7560E4BB23320A8C311DB0011504F7C5FEE30D58C266D54F7C6B166`.
This derivative remains tracked for provenance and possible alternate use, but
it no longer supplies either current Old City `InteractiveBuildingDoor` visual.
The two existing pawnshop/factory doors use Kenney's CC0 `door-hinged.glb` at
the 1.45-by-2.65-meter personnel openings; the 12 enterable-room doors use
the Kenney / Chinese Temple 2 composite documented above. All swing sideways
through 96 degrees. Collision, animation, network state, and AI traversal remain
project gameplay behavior.

It is a deterministic Blender DCC export/cleanup/validation step, not a runtime
procedural city-generation system. It reapplies the documented explicit
building-transform table, factory-frontage substitution, sign cleanup, material
tuning, and export policy without randomness. The resulting placements and
mesh edits are serialized in the packed `.blend`. Review PNGs under
`previews/` are maintained separately.

The main GLB contains the final static map geometry, including the three shared
Chinese district profiles, 50 authored density placements, the 18 Quaternius
brick and two Quaternius doorframe instances, materials, textures, and custom
provenance metadata. It contains zero visible `Old Urban building` or `Scan Old
Building Street` instances. It excludes preview cameras and lights, the
original Noto font, and all acquisition-only rig/source scaffolding. Chinese
sign outlines are ordinary static mesh geometry. The
standalone shutter GLB retains MP / Poly Haven CC0 provenance through its source
mesh in the authoritative packed scene even though it is not the current door.
Security Camera 01 is delivered only as static geometry, materials, and
textures; its source rig and animations are not shipped.

### Historical 2026-08-28 urban-life and facade expansion

The 2026-08-28 DCC expansion was the source baseline for the current work. It
added the following elements before the Chinese-profile building replacement:

- 36 adapted Modular Urban Apartments Facade objects arranged as two
  asymmetrical 3-by-3 tenement overlays;
- three instances of Kin Chen's Pink city bicycle, converted to a static rest
  pose, stripped of its rig, given weathered teal material adaptations, and
  cleaned to 11,825 triangles per instance;
- a Coffee Cart 01 and Wicker Basket 01 market tea stall, a Chinese Tea Table
  with three Chinese Stools at the pawnshop, and a Hand Truck at the factory;
- a finished CC0 pawnshop storefront, five market shops made from three Old
  Urban building and two Scan Old Building Street instances, and two Old Urban
  building rear houses;
- a modeled pawnshop hero gate made from 15 adapted VVayToyek pavilion parts,
  eight solid apartment-facade wall modules, eight authored inserts, and a
  ten-piece Quaternius personnel-door facade;
- a matching ten-piece factory personnel-door facade; together the two entries
  use 18 `Brick_Plain_1` and two `DoorFrame_Trim` instances around
  1.45-by-2.65-meter openings, with separately instanced Kenney CC0 hinged doors
  that swing sideways through 96 degrees;
- replacement of the former damaged factory shell with three Old Urban
  building office/admin instances and two Scan Old Building Street workshops;
- 36 complete perimeter-density buildings from six CC0 profiles: eight Old
  Urban, fourteen Scan Old, four Quaternius Building1 Large, three Building3
  Big, three Building4, and four House2 instances, positioned by an explicit
  reviewed transform table with zero density intersections;
- four full-mesh replacements in the repeated near-street row, giving the six
  reviewed buildings five distinct modeled silhouettes; and
- a real 7.6-by-4.2-meter base opening through the pawnshop storefront mesh,
  visibly and physically infilled by the authored facade around its central
  human-scale hinged door; and
- five Chinese red lamps distributed across the cleared storefront
  composition.

The Old Urban and Scan Old building bullets above are retained solely to explain
the historical source state. The 2026-08-29 rebuild supersedes every one of
those visible placements with the three Chinese-profile shared meshes described
above. The other urban-life, entrance, and prop adaptations remain saved DCC
work. The `.blend` remains the authoritative source; the export script enforces
the explicit reviewed transform table and targeted cleanup rather than
generating runtime city geometry.

### Current Chinese-profile density and replacement contract

The current contract requires 66/66 replaced visible anchors, 50/50 authored
density placements, all six west/east `Edge04`-`Edge06` placements, all eight
west/east `Infill05`-`Infill08` placements, and zero visible instances of the
two retired building sources. Hall instances use the
Temple-derived LOD; arcade and gate-house instances use the Quaternius clean
bodies plus pavilion facade/eaves parts and an extracted/decimated Temple roof.
This retains finished authored silhouettes while sharing meshes and limiting
runtime textures to 512 pixels.

## Runtime contract and geometry audit

The authored runtime contract requires all eight named anchors below. The
terminal anchors own visible objective-terminal composites built in Blender from
CC0 Utility Box 01 and Television 02 bodies plus project-authored small status
screens and adaptations; the legacy code-built terminals remain invisible
gameplay scaffolds:

- `AuthoredStreetNetwork`
- `JianghaiTenementDistrict`
- `RedStarElectronicsFactory`
- `GuangchangPawnshop`
- `OldCityMarketBridge`
- `GrandHotelSecurityTerminalVisual`
- `MunicipalTreasuryManifestTerminalVisual`
- `JianghaiValleyEnvironment`

`JianghaiValleyEnvironment` owns the existing project-authored,
hand-chamfered `OldCityFoundation`, one visual-only
`JianghaiPerimeterGroundComposite` assembled from eight Coast Line 01 scan
placements, and 12 visual-only Hero Mountain instances arranged as staggered
six-object inner and outer rings. The foundation top and visible Coast-derived
ground use Gravel Floor 03; the foundation sides use Rocky Terrain. No
procedural visible terrain or displacement-generated geometry is delivered.
The ground-to-platform and ground-to-mountain continuity comes from the
signed-distance-shaped Coast composite itself, not camera masking or primitive
art. The
valley contributes no collision proxy; Godot continues to own gameplay
collision and navigation.

The same DCC correction tapers and buries the north-end vertical caps of
`AuthoredStreetNetwork/CentralAvenueCurbW` and
`AuthoredStreetNetwork/CentralAvenueCurbE`. DCC and serialized-GLB ray gates
report 330/330 north-approach top hits and 90/90 south-approach top hits, with
zero side hits on either approach.

The final DCC correction removes a duplicated 180-degree rotation from the
`MunicipalTreasuryManifestTerminalVisual` root. Its screen now faces opposite
the Grand terminal as intended. The same correction relocates all 22
Rollershutter Window 03 and Exterior Aircon Unit instances from the central
lane to actual tenement facades and rotates them flush to the walls; none of
these props occupies `CentralAvenue` in the saved scene.
Final DCC QA deletes the redundant `JianghaiArtPass_FactoryHeroShutter`
overlay because it became obsolete when the damaged factory shell was replaced
by five finished CC0 buildings. Rollershutter Window 03 remains used on the
tenement facades and as a retained standalone derivative, but it is no longer
the current visual for either Old City interactive door.
The factory landmark entry is framed by five visible objects authored and
aligned in Blender: two reused DCC brick piers, two pier caps, and a corrugated
roof. Their portal composition is final DCC art, not code-built primitive or
procedural visible geometry. Reused packed materials keep their recorded
third-party provenance. Behind that outer portal, and at the pawnshop, each
existing entrance adds a ten-piece Quaternius facade around a Kenney hinged
door. Twelve reviewed street buildings use the red-and-gold Kenney /
Chinese Temple 2 composite doors described above.

Godot now builds a deliberately lightweight gameplay-collision layer instead of
turning millions of authored render triangles into physics geometry. The current
contract maps 119 authored source meshes to 383 authored box shapes. The main
gameplay-collision body contains 490 boxes; 29 landmark facade/traversal boxes
bring the world-layer-1 total to 519, with zero concave shapes. Twelve enterable
furnished rooms retain 12 single-leaf Chinese-lattice doors, 48 finished Kenney
furniture props / 95 authored mesh nodes, 12 searchable loot placements, four
added residents, and 12 bidirectional squad door links. The clan hall adds one
logical double-hinged door using two further lattice visual instances.

The explicit post-import runtime diagnostics for the current `33E2176E...` GLB
report:

| Current post-portal runtime layer | Verified result |
| --- | --- |
| Authored-map import | 553 authored meshes; 1,382 surfaces; 3,056,190 authored instance triangles; 8/8 loader-required anchors. The ninth DCC anchor is the separately validated and consumed clan-hall gate contract |
| Render batching | 272 safe source meshes represented by 69 spatial `MultiMesh` batches; zero enterable source meshes are batched |
| Gameplay collision | 119 authored source meshes / 383 authored box shapes; 490 gameplay boxes plus 29 landmark boxes, 519 total; zero concave shapes |
| Door visuals | 12 single-leaf residence doors plus one logical clan-hall double gate with two leaves; 14 Chinese-lattice visual instances total |

The table below is the superseded pre-portal runtime baseline for the earlier
`3A854DD9...` GLB. It is retained only for comparison and must not be read as
the current import/batching count after the 35-node facade retirement and ninth
authored anchor. Current DCC and serialized-GLB counts are recorded above;
current runtime counts are recorded in the immediately preceding table from the
post-import diagnostics.

| Historical pre-portal runtime layer | Verified result |
| --- | --- |
| Authored-map import | 588 authored meshes; 1,450 material-backed surfaces; 3,151,080 authored instance triangles; all eight required anchors |
| Runtime scene budget | Detailed structural diagnostics: 1,895 nodes, 76 static bodies, and 799 mesh instances against an 820 budget; production source release: 1,605 nodes and 509 mesh instances; 26 lights, 48 loot placements, and 33 garrison actors |
| Gameplay collision | 115 authored sources plus 96 placement fragments; 486 gameplay plus 20 landmark boxes, 506 total; zero concave shapes; 14 route probes and 12/12 high-value-loot routes pass |
| Enterable interiors | 12 rooms; 12 red-and-gold Chinese doors; 12 shared opaque liners; 48 finished Kenney furniture props / 95 authored mesh nodes; 12 searchable loot placements; four added / eight total residents; 12 bidirectional AI door links |
| Render batching and quality | 292 repeated source meshes represented by 78 spatial batches; production releases 290 sources and retains two room references; 36 static furniture props resolve to 50 short-range batches / 62 instances with a 32.08-meter maximum batch radius; no furniture or liner shadows; incremental sky-radiance updates |
| Representative capture | All 19 measured views pass the 2,400 draw-call / 2,200-object / 10.5-million-primitive / 1,250 MB video / 750 MB texture budgets; overview 982 / 1,007 / 2,867,417; daylight 1,797 / 1,844 / 3,054,463; peak primitives 3,121,853; peak 1,097.4 MB video and 719.2 MB texture memory; same-machine texture memory is unchanged from the pre-expansion baseline |

Read-only audits through the earlier 2026-08-29 valley integration recorded the
historical pre-rebase and post-valley values below. Both tables predate the
Chinese district rebuild and remain only as comparison/evidence; current
artifact hashes and counts come from the final rebuild audit. The first table is
the upstream pre-valley baseline:

| Audit layer | Verified result |
| --- | --- |
| Upstream pre-valley Blender source baseline | 487 mesh objects; 196 unique mesh datablocks; 4,471,243 raw mesh-object triangles; 821,213 triangles counted once per unique mesh; 550 evaluated/runtime mesh instances and 4,500,345 instance triangles; all seven then-required anchors; 61,677,884 bytes; SHA-256 `C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7` |
| Factory-gate portal | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the ten-piece personnel-door facade and hinged runtime door |
| Hinged-entry facades | `entry_facades_ready=True`; two facades at 10/10 finished CC0 objects each; 18 Quaternius `Brick_Plain_1` instances and two `DoorFrame_Trim` instances total; nine bricks plus one doorframe at each entry |
| Pawnshop hero entrance | `pawnshop_frontage_ready=True`; 15/15 modeled canopy parts; 15,492 canopy triangles; 8/8 solid wall modules; 8/8 authored inserts; 0 legacy visible gate/wall objects; the original large storefront opening is infilled by the ten-piece facade around the central 1.45-by-2.65-meter door |
| Delivered urban-life and density expansion | 36/36 apartment-facade objects; 36/36 complete perimeter buildings across six licensed profiles with `density_intersections=0`; four full-mesh street-cadence replacements; three static 11,825-triangle bicycles; market and frontage props; finished CC0 pawnshop backdrop and modeled pavilion gate; five market shops; two rear houses; five Chinese red lamps; five-building factory replacement |
| Upstream pre-valley serialized GLB baseline | 73,809,716 bytes; SHA-256 `2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A`; 550 mesh nodes; 4,500,345 mesh-node instance triangles; maximum texture dimension 1024 pixels |
| Upstream pre-valley Godot authored-map baseline | `--validate-refinery-map` PASS; 550 imported authored meshes; 770 surfaces; all 770 surfaces are material-backed; 4,500,345 authored instance triangles; 7/7 then-required authored anchors; terminal checks 2/2/2/2; authored status screens 2/2; four interior residents |
| Upstream pre-valley Godot collision baseline | `--validate-refinery-collision` PASS; 240/240 exact concave shapes from 107 structural and 133 detail meshes across 94/21/83/42 anchors; collision cache 104 shared meshes, 76 baked instances, and 77 unique shapes; 3,560,137 collision-instance triangles; 0 legacy model-placement boxes; 0 landmark proxy boxes; closed-door enemy capsule blocks and opened route clears; market rails 4/4 and posts 2/2 block while rail gaps 2/2 stay clear; building ballistic probes 5/5; high-value loot access 12/12 |
| Runtime doors and interiors | Two Kenney CC0 `door-hinged.glb` visuals; 1.45-by-2.65-meter personnel openings; normal 96-degree side swing; four animated, unarmed Quaternius CC0 operator-model reuses as interior residents plus existing interior loot placements |
| Godot route clearance | `routes=True`; `route_probes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Upstream pre-valley Godot quality baseline | Eight-view capture PASS after the 20 entry-facade objects, hinged doors, interior loot, and four authored residents; peak 1,014 draw calls, 1,271 objects, 7,113,753 primitives, 1,001.8 MB video memory, and 852.7 MB texture memory |

In that upstream baseline, the Blender source count is based on saved mesh objects, while the 821,213
unique-mesh figure counts each shared datablock once. Dependency-graph
evaluation and export resolve the scene to 550 runtime mesh instances and
4,500,345 instance triangles. The Godot diagnostic imports those same 550
meshes and sums 770 material-backed runtime surfaces. These scopes are
intentionally different rather than conflicting.

The later valley pre-rebase evidence and historical post-valley delivery are
kept separately because neither binary represents the current Chinese district:

| Audit layer | Verified result |
| --- | --- |
| Valley pre-rebase Blender source | Historical snapshot: 74,037,661 bytes; SHA-256 `C9BAC433CF77791B3730E309A5E0BEEF6CF4849593D44018FD2CDFE5AC8FAA08`; Blender builder/audit/export PASS; 4,835,033 evaluated full-scene triangles, below the 5,000,000 gate; eight required anchors |
| Historical post-valley Blender source | 81,861,168 bytes; SHA-256 `7CA84CD2B17C3872323D8A5EE7B1A4BA5BCB360F4326FB2331327BED4F493461`; 500 mesh objects; 198 unique mesh datablocks; 4,807,899 raw mesh-object triangles; 1,003,869 triangles counted once per unique mesh; dependency-graph evaluation resolves 563 objects and 4,836,825 instance triangles; 8/8 anchors; builder, read-only audit, export, and GLB-round-trip gates all PASS; superseded by the 2026-08-29 Chinese district rebuild |
| Authored valley environment | `valley=True`; 188-triangle, 96-source-vertex project-authored `OldCityFoundation`; one 84,960-vertex, 168,480-triangle `JianghaiPerimeterGroundComposite` assembled from eight Coast Line 01 scan placements; one shared 14,000-triangle solararchitect Hero Mountain mesh composed as 12 instances divided six/six across staggered inner and outer rings; 336,668 total valley instance triangles. Ground bounds X `-600.878..600.853`, Y `-540.340..660.056`, Z `-12.7965..5.0390`; relief 17.835 meters; coverage 1.000; topology one component, two boundary loops/1,440 edges, zero degenerates, zero invalid normals; actual-foundation-footprint signed-distance transition; maximum foundation gap 0.103 meters; safe-area top -0.120 meters; 0-60/60-160-meter relief 0.969/3.955 meters; slope RMS/p90/p99/max 0.0579/0.0869/0.2331/0.6620; ring coverage 7,920/7,920. Gravel Floor 03 diffuse/normal/roughness, base-color factor `(0.92, 0.78, 0.62, 1.0)`, 7-meter affine world-XY UVs; DCC/GLB maximum UV errors `3.27e-6`/`4.36e-6` within `1.2e-5`; Jacobian 1/1; Coast material/image counts 0/0. North/south road ray gates 330/330 and 90/90 top hits with zero side hits; minimum mountain burial 4.942 meters; DCC and GLB round-trip gates PASS; all valley meshes visual-only; no displacement-generated visible geometry |
| Factory-gate portal contract | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the ten-piece personnel-door facade and hinged runtime door |
| Hinged-entry facade contract | `entry_facades_ready=True`; two facades at 10/10 finished CC0 objects each; 18 Quaternius `Brick_Plain_1` instances and two `DoorFrame_Trim` instances total; nine bricks plus one doorframe at each entry |
| Pawnshop hero entrance contract | `pawnshop_frontage_ready=True`; 15/15 modeled canopy parts; 15,492 canopy triangles; 8/8 solid wall modules; 8/8 authored inserts; 0 legacy visible gate/wall objects; the original large storefront opening is infilled by the ten-piece facade around the central 1.45-by-2.65-meter door |
| Delivered urban-life and density expansion | 36/36 apartment-facade objects; 36/36 complete perimeter buildings across six licensed profiles with `density_intersections=0`; four full-mesh street-cadence replacements; three static 11,825-triangle bicycles; market and frontage props; finished CC0 pawnshop backdrop and modeled pavilion gate; five market shops; two rear houses; five Chinese red lamps; five-building factory replacement |
| Valley pre-rebase serialized GLB | Historical snapshot: 76,862,308 bytes; SHA-256 `0C0174672630957390A959BC3BD71DB3F4849CC7CABE0AFADFDD12273DFE02A5`; export and DCC-to-GLB round-trip PASS; 4,835,033 full-scene instance triangles; maximum texture dimension 1024 pixels |
| Historical post-valley serialized GLB | 84,723,312 bytes; SHA-256 `7E2BB712BCF031692FAFB0E4E0FA59F3E75CE340B2748F5EDBDB7B105D9B2965`; export and DCC-to-GLB round-trip gates PASS; 563 resolved mesh instances and 4,836,825 full-scene instance triangles; superseded by the 2026-08-29 Chinese district rebuild |
| Historical post-valley Godot authored-map import | PASS after an explicit editor reimport followed by a second no-op import; 563 authored meshes; 784 surfaces, all 784 material-backed; 4,836,825 authored instance triangles; 8/8 anchors; 419 detail meshes; 406 shadow-casting meshes; quality-tier counts 130/226/406; valley contract one ground plus 12 mountains and 336,668 triangles; exactly one named 168,480-triangle perimeter-ground composite, 12 named mountains sharing one Hero Mountain mesh, the 188-triangle foundation, both ten-piece hinged-entry facades, CC0/CC BY rights metadata, Gravel Floor 03 PBR identity and affine UV contract, direct hierarchy, no valley collision, modeled-ground coverage, and mountain-ring orientation |
| Historical post-valley Godot authored collision | PASS 240/240 exact concave shapes: 107 structural plus 133 detail meshes across 94/21/83/42 anchors; collision cache 104 shared meshes, 76 baked instances, and 77 unique shapes; 3,560,137 collision-instance triangles; both hinged-entry facades; exact closed/open door probes; zero legacy proxy boxes; market rail/gap probes; building ballistic probes; high-value loot access 12/12 |
| Runtime doors and interiors | `refinery-doors` PASS; two Kenney CC0 `door-hinged.glb` visuals; 1.45-by-2.65-meter personnel openings; normal 96-degree side swing; residents 4/4 using animated, unarmed Quaternius CC0 operator-model reuses alongside the existing interior loot placements |
| Historical post-valley Godot route clearance | PASS; `routes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Historical post-valley Godot atmosphere | PASS for Day and always-procedural Dusk; continuous sky/ground horizon; no panorama |
| Historical post-valley Godot quality and full runtime | All 11 representative captures PASS; 1,087.0 MB video memory of a 1,536 MB budget; 900.9 MB texture memory of a 1,152 MB budget; independent visual review DELIVERABLE with no sky/terrain seam, radial pattern, skirt, z-fighting, trench, floating platform, or material south-line blocker |
| Historical post-valley diagnostics | `refinery-map`, `refinery-collision`, `refinery-doors`, `refinery-atmosphere`, `map-density`, `large-map`, `residential`, `stairs`, `skylinks`, and `vehicle-drive` all exit 0 |

The historical counting scopes intentionally differ: Blender's source count follows
saved mesh objects and unique datablocks, while dependency-graph, export, and
runtime counts follow resolved instances.

### Historical upstream capture performance evidence

These figures were captured on 2026-08-28 after the two ten-piece hinged-entry
facades, hinged doors, interior loot, and four authored residents were active.
The high-tier policy disables shadows only on fine decorative meshes, and all
eight representative captures passed their configured budgets:

| View | Draw calls | Objects | Primitives | Result |
| --- | ---: | ---: | ---: | --- |
| Overview | 582 | 808 | 4,286,647 | PASS |
| Victory street | 750 | 890 | 4,207,438 | PASS |
| Street-life bicycle close-up | 421 | 466 | 3,322,262 | PASS |
| Guangchang pawnshop | 511 | 740 | 2,230,125 | PASS |
| Red Star factory | 561 | 623 | 4,265,595 | PASS |
| Market footbridge | 739 | 1,030 | 4,707,642 | PASS |
| North-ward density | 352 | 478 | 2,512,297 | PASS |
| Daylight overview | 1,014 | 1,271 | 7,113,753 | PASS |

The capture reports a 1,001.8 MB peak video-memory reading and an 852.7 MB peak
texture-memory reading.
This table is the upstream pre-valley baseline, not final delivery evidence.

### Historical post-valley capture performance evidence

Before the Chinese district rebuild, the post-valley export, explicit Godot
editor reimport, and second no-op import produced 11 passing representative
captures. The tuple
columns below are draw calls, visible objects, and primitives:

| View | Draw calls | Objects | Primitives | Result |
| --- | ---: | ---: | ---: | --- |
| `overview` | 571 | 810 | 4,509,279 | PASS |
| `mountain_valley_aerial` | 643 | 827 | 5,258,780 | PASS |
| `south` | 8 | 8 | 226,868 | PASS |
| `north` | 107 | 114 | 756,216 | PASS |
| `perimeter_ground_scan` | 570 | 749 | 4,576,881 | PASS |
| `street_life` | 615 | 724 | 3,833,658 | PASS |
| `guangchang_pawnshop` | 509 | 742 | 2,453,853 | PASS |
| `red_star_factory` | 551 | 621 | 4,491,929 | PASS |
| `market_footbridge` | 727 | 1,045 | 5,081,051 | PASS |
| `north_ward_density` | 382 | 514 | 2,795,341 | PASS |
| `daylight_overview` | 1,005 | 1,274 | 7,351,237 | PASS |

Video memory was 1,087.0 MB of the 1,536 MB budget; texture memory was 900.9 MB
of the 1,152 MB budget. The historical high-tier policy disabled shadows only on
fine decorative meshes; model geometry, materials, and visibility ranges were
unchanged. Independent visual review was DELIVERABLE: no sky/terrain seam,
radial pattern, skirt, z-fighting, trench, floating platform, or material
south-line blocker remains.

## Rights boundary

Most imported marketplace/model sources contained in the current DCC scene are
CC0. The exception is solararchitect's Hero Mountain, which is embedded under
CC BY 4.0 and requires attribution, a link to
http://creativecommons.org/licenses/by/4.0/, and an indication that Operation
Steel Tide modified the source by decimation, PBR-node reconstruction,
512-pixel texture capping/packing, uniform scaling, rotation, and multi-instance
valley composition. Hero Mountain and its adapted geometry and textures are not
relicensed as MIT.
Noto Sans SC is licensed under SIL OFL 1.1 and was used only during DCC
authoring to convert the required Chinese sign text to static glyph meshes.
The original font file is not committed or present in the final `.blend` or
GLB, and the export script enforces that boundary. The existing Poly Haven
surface textures and the retained but currently unused Kloppenheim 06 (Pure
Sky) HDRI are also CC0. Coast Line 01, Rocky Terrain, and Gravel Floor 03 are
likewise CC0; their raw 2K files stay in the private acquisition cache. Coast
Line 01 contributes geometry only; its source material and images are not
embedded, and the visible ground instead uses Charlotte Baglioni's Gravel Floor
03 material and three image datablocks. Only the adapted DCC content is packed
into the `.blend` and map GLB. Coastal Cliff 01, Coastal Cliff 02, and
Namaqualand Cliff 02 remain
evaluation-only private records and
contribute no delivered geometry, materials, or textures. The HDRI remains
outside the `.blend` and map GLB.

The project-authored layout, 2026-08-29 Chinese district rebuild, valley
foundation and source-instance
placement/orientation composition, adaptation work, objective-terminal status
screens, pawnshop adaptation/composition, and factory-gate/hinged-entry
geometry and composition are covered by the
repository's root MIT license, subject to `docs/CONTENT_PROVENANCE.md`. The CC0
Utility Box 01 and Television 02 bodies, Hero Mountain, and all other
third-party source geometry, materials, textures, and font software retain
their recorded source rights; packing, reusing a material on the portal, or exporting does not
relicense them as MIT content. See `LICENSE_EVIDENCE.md` for exact creators,
URLs, asset IDs, hashes, and local mapping. In particular, the 18 brick and two
doorframe facade instances remain Quaternius Downtown City MegaKit CC0 content,
the two existing pawnshop/factory hinged doors remain Kenney Factory Kit CC0
content, and each of the 12 composite doors retains both Kenney Factory Kit
and Free poly Chinese Temple 2 CC0 provenance. The packed red-wood lacquer and
material adaptation are project-authored under the root MIT boundary described
above.
