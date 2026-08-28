# Jianghai Old City DCC Source

`jianghai_old_city.blend` is the editable, packed Blender source for the static
Jianghai Old City extraction-map visual exported to
`assets/models/jianghai_old_city/jianghai_old_city.glb`.

The scene is a project-authored 1990s Lingnan river-industrial city
composition. The complete layout, streets, supporting geometry, district
assembly, material adaptations, art direction, Chinese sign wording and
placement, lighting used for review renders, objective-terminal status screens
and adaptations, factory-gate portal and hinged-entry facade composition,
urban-life dressing, facade expansion, and integration of licensed source
modules are produced for
Operation Steel Tide. Each terminal body
combines the CC0 Utility Box 01 and Television 02 sources recorded below; only
its small status screen and adaptation work are project-authored. Godot
continues to own gameplay collision, navigation, loot, spawning, doors, and
mission state.

## Authoritative source and provenance inputs

`jianghai_old_city.blend` is the authoritative hand-edited DCC source. The
repository-external acquisition paths below record the files used during
authoring and remain useful for provenance or deliberate re-editing; they are
not required to export the packed final scene. The listed paths are relative
to the original `JIANGHAI_ACQUISITION_ROOT` cache.

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

The dusk sky uses the repository-local Poly Haven CC0 HDRI **Kloppenheim 06
(Pure Sky)** by Greg Zaal (Original), with sky edits by Jarod Guest. It was
acquired on 2026-08-28 from
https://polyhaven.com/a/kloppenheim_06_puresky. The official 1K HDR download is
https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/kloppenheim_06_puresky_1k.hdr;
the local `assets/textures/kloppenheim_06_puresky_1k.hdr` is 1,173,154 bytes,
has SHA-256
`206C67E3A1B992282821CF06662BDD69BBB4915C1C4444A66338A40D6A7D4E34`,
and matches official API MD5 `995d68b1656f26452572645c0ffe898b`.
`JianghaiOldCityAtmosphere` loads it at runtime. It is not packed into the
authoritative `.blend` or embedded in the map GLB.

The Poly Haven **Modular Urban Apartments Facade** is a delivered source.
Thirty-six adapted facade objects form two asymmetrical 3-by-3 overlays on the
west and east tenements in the authoritative `.blend` and runtime GLB.

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

The repository-external cache is neither a runtime lookup nor an export
dependency. The authoritative `.blend` already contains the adapted source
geometry, materials, and textures. Do not replace an embedded source with a
similarly named marketplace asset unless its exact creator, source URL,
license, acquisition date, and evidence record have first been verified.

## Edit and export

Make composition, modeling, material, lighting, and sign changes directly in
`source_art/world/jianghai_old_city/jianghai_old_city.blend`. From the
repository root, export that saved scene with:

```powershell
blender --background source_art/world/jianghai_old_city/jianghai_old_city.blend --python scripts/blender/export_jianghai_old_city.py
```

The export script verifies that the authoritative scene is open, removes the
documented obsolete signs and retired asset metadata, reapplies the cleared
five-building factory frontage, validates the authored pawnshop canopy and
wings, and applies runtime material tuning, then flattens any
remaining tiled images, rebuilds the two ten-piece Quaternius entry facades,
caps the longest runtime-texture dimension at 1024
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
The current doors use Kenney's CC0 `door-hinged.glb` at the two 1.45-by-2.65-meter
personnel openings and swing sideways through 96 degrees. Collision, animation,
network state, and AI traversal remain project gameplay behavior.

It is a deterministic Blender DCC export/cleanup/validation step, not a runtime
procedural city-generation system. It reapplies the documented explicit
building-transform table, factory-frontage substitution, sign cleanup, material
tuning, and export policy without randomness. The resulting placements and
mesh edits are serialized in the packed `.blend`. Review PNGs under
`previews/` are maintained separately.

The main GLB contains the final static map geometry, including the 18
Quaternius brick and two Quaternius doorframe instances, materials, textures, and
custom provenance metadata. It excludes preview cameras and lights, the
original Noto font, and all acquisition-only rig/source scaffolding. Chinese
sign outlines are ordinary static mesh geometry. The
standalone shutter GLB retains MP / Poly Haven CC0 provenance through its source
mesh in the authoritative packed scene even though it is not the current door.
Security Camera 01 is delivered only as static geometry, materials, and
textures; its source rig and animations are not shipped.

### Urban-life and facade expansion

The 2026-08-28 DCC expansion makes the central city blocks read as inhabited
and locally specific while keeping gameplay scaffolding in Godot. It adds:

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

These are saved DCC placements and adaptations. The `.blend` remains the
authoritative source; the export script enforces the explicit reviewed
transform table, targeted cleanup, and cleared-asset substitutions rather than
generating runtime city geometry.

## Runtime contract and geometry audit

The authored runtime contract requires all seven named anchors below. The last
two anchors own visible objective-terminal composites built in Blender from
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
current entrance adds a ten-piece Quaternius facade around a Kenney hinged door.

Godot derives exact static collision from 107 named authored structural meshes
and 133 explicitly selected authored details. The 240 concave shapes are split
94/21/83/42 across `JianghaiTenementDistrict`,
`RedStarElectronicsFactory`, `GuangchangPawnshop`, and
`OldCityMarketBridge`. The detail meshes comprise the five-piece factory gate,
the two ten-piece hinged-entry facades, 71 pawnshop canopy/wing/low-wall pieces,
and 37 market deck/ramp/rail pieces.
The same layer-1 geometry blocks traversal and ballistic world queries while
preserving the real doorway and rail gaps. Successful authored collision
suppresses all former broad model-placement and landmark proxy boxes. The
central rooftop path is verified with a 0.5-meter-radius capsule sweep, and all
12 Epic/Legendary high-value placements have player-capsule routes in open
space. Runtime validation additionally confirms that the two closed doors block
an enemy capsule, the opened 1.45-by-2.65-meter routes clear, and the two
interiors contain four residents in total alongside their loot placements.

Read-only audits on 2026-08-28 recorded these matching source, serialized, and
runtime results:

| Audit layer | Verified result |
| --- | --- |
| Authoritative Blender source | 487 mesh objects; 196 unique mesh datablocks; 4,471,243 raw mesh-object triangles; 821,213 triangles counted once per unique mesh; 550 evaluated/runtime mesh instances and 4,500,345 instance triangles; all seven required anchors; 61,677,884 bytes; SHA-256 `C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7` |
| Factory-gate portal | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the ten-piece personnel-door facade and hinged runtime door |
| Hinged-entry facades | `entry_facades_ready=True`; two facades at 10/10 finished CC0 objects each; 18 Quaternius `Brick_Plain_1` instances and two `DoorFrame_Trim` instances total; nine bricks plus one doorframe at each entry |
| Pawnshop hero entrance | `pawnshop_frontage_ready=True`; 15/15 modeled canopy parts; 15,492 canopy triangles; 8/8 solid wall modules; 8/8 authored inserts; 0 legacy visible gate/wall objects; the original large storefront opening is infilled by the ten-piece facade around the central 1.45-by-2.65-meter door |
| Delivered urban-life and density expansion | 36/36 apartment-facade objects; 36/36 complete perimeter buildings across six licensed profiles with `density_intersections=0`; four full-mesh street-cadence replacements; three static 11,825-triangle bicycles; market and frontage props; finished CC0 pawnshop backdrop and modeled pavilion gate; five market shops; two rear houses; five Chinese red lamps; five-building factory replacement |
| Serialized GLB | 73,809,716 bytes; SHA-256 `2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A`; 550 mesh nodes; 4,500,345 mesh-node instance triangles; maximum texture dimension 1024 pixels |
| Godot authored-map import | `--validate-refinery-map` PASS; 550 imported authored meshes; 770 surfaces; all 770 surfaces are material-backed; 4,500,345 authored instance triangles; 7/7 authored anchors; terminal checks 2/2/2/2; authored status screens 2/2; four interior residents |
| Godot authored collision | `--validate-refinery-collision` PASS; 240/240 exact concave shapes from 107 structural and 133 detail meshes across 94/21/83/42 anchors; collision cache 104 shared meshes, 76 baked instances, and 77 unique shapes; 3,560,137 collision-instance triangles; 0 legacy model-placement boxes; 0 landmark proxy boxes; closed-door enemy capsule blocks and opened route clears; market rails 4/4 and posts 2/2 block while rail gaps 2/2 stay clear; building ballistic probes 5/5; high-value loot access 12/12 |
| Runtime doors and interiors | Two Kenney CC0 `door-hinged.glb` visuals; 1.45-by-2.65-meter personnel openings; normal 96-degree side swing; four animated, unarmed Quaternius CC0 operator-model reuses as interior residents plus existing interior loot placements |
| Godot route clearance | `routes=True`; `route_probes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Godot quality and full runtime | Current eight-view capture PASS after the 20 entry-facade objects, hinged doors, interior loot, and four authored residents; peak 1,014 draw calls, 1,271 objects, 7,113,753 primitives, 1,001.8 MB video memory, and 852.7 MB texture memory |

The Blender source count is based on saved mesh objects, while the 821,213
unique-mesh figure counts each shared datablock once. Dependency-graph
evaluation and export resolve the scene to 550 runtime mesh instances and
4,500,345 instance triangles. The Godot diagnostic imports those same 550
meshes and sums 770 material-backed runtime surfaces. These scopes are
intentionally different rather than conflicting.

### Current capture performance evidence

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

## Rights boundary

All imported marketplace/model sources contained in the current DCC scene are
CC0.
Noto Sans SC is licensed under SIL OFL 1.1 and was used only during DCC
authoring to convert the required Chinese sign text to static glyph meshes.
The original font file is not committed or present in the final `.blend` or
GLB, and the export script enforces that boundary. The existing Poly Haven
surface textures and the separately loaded Kloppenheim 06 (Pure Sky) HDRI are
also CC0. The HDRI remains outside the `.blend` and map GLB.

The project-authored layout, adaptation work, objective-terminal status
screens, pawnshop adaptation/composition, and factory-gate/hinged-entry
composition are covered by the
repository's root MIT license, subject to `docs/CONTENT_PROVENANCE.md`. The CC0
Utility Box 01 and Television 02 bodies, along with all other third-party source
geometry, materials, textures, and font software, retain their recorded source
rights; packing, reusing a material on the portal, or exporting does not
relicense them as MIT content. See `LICENSE_EVIDENCE.md` for exact creators,
URLs, asset IDs, hashes, and local mapping. In particular, the 18 brick and two
doorframe facade instances remain Quaternius Downtown City MegaKit CC0 content,
and the two current hinged runtime doors remain Kenney Factory Kit CC0 content.
