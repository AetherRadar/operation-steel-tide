# Jianghai Old City DCC Source

`jianghai_old_city.blend` is the editable, packed Blender source for the static
Jianghai Old City extraction-map visual exported to
`assets/models/jianghai_old_city/jianghai_old_city.glb`.

The scene is a project-authored 1990s Lingnan river-industrial city
composition. The complete layout, streets, supporting geometry, district
assembly, material adaptations, art direction, Chinese sign wording and
placement, lighting used for review renders, objective-terminal status screens
and adaptations, factory-gate portal composition, urban-life dressing, facade
expansion, and integration of licensed source modules are produced for
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
remaining tiled images, caps the longest runtime-texture dimension at 1024
pixels, recompresses eligible high-resolution runtime images as JPEG quality
90, rejects non-built-in font datablocks, packs external data, saves the
`.blend`, and exports:

- `assets/models/jianghai_old_city/jianghai_old_city.glb`
- `assets/models/jianghai_old_city/rollershutter_window_03.glb`

The second output is a reproducible standalone PBR door visual. The script
selects `JianghaiArtPass_EastShutter00`, makes a temporary normalized copy, and
exports only that adapted Rollershutter Window 03 mesh and its materials. The
result is 187,940 bytes with SHA-256
`C4884AFCD7560E4BB23320A8C311DB0011504F7C5FEE30D58C266D54F7C6B166`.
Two Old City `InteractiveBuildingDoor` instances each tile this GLB into three
separate authored shutter panels across a 7.6-meter opening. The maximum
horizontal/vertical scale distortion is 1.094, avoiding the former single
stretched slab. Only the visible art changes: the existing gameplay door keeps
collision, animation, network state, and AI traversal.

It is an export/cleanup/validation step, not a procedural city-generation system. It
deterministically reapplies the documented factory-frontage substitution, sign
cleanup, material tuning, and export policy. All further layout composition and
modeling work starts from the hand-edited `.blend`. Review PNGs under
`previews/` are maintained separately.

The main GLB contains the final static map geometry, materials, textures, and
custom provenance metadata. It excludes preview cameras and lights, the
original Noto font, and all acquisition-only rig/source scaffolding. Chinese
sign outlines are ordinary static mesh geometry. The
standalone door GLB retains MP / Poly Haven CC0 provenance through its source
mesh in the authoritative packed scene.
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
  three-panel interactive Rollershutter Window 03 door;
- replacement of the former damaged factory shell with three Old Urban
  building office/admin instances and two Scan Old Building Street workshops;
  and
- five Chinese red lamps distributed across the cleared storefront
  composition.

These are saved DCC placements and adaptations. The `.blend` remains the
authoritative hand-edited source; the export script only enforces the targeted
cleanup and cleared-asset substitution described above rather than generating
the city layout.

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
tenement facades and the two standalone Old City interactive-door visuals.
The factory landmark entry is framed by five visible objects authored and
aligned in Blender: two reused DCC brick piers, two pier caps, and a corrugated
roof. Their portal composition is final DCC art, not code-built primitive or
procedural visible geometry. Reused packed materials keep their recorded
third-party provenance.

Read-only audits on 2026-08-28 recorded these matching source, serialized, and
runtime results:

| Audit layer | Verified result |
| --- | --- |
| Authoritative Blender source | 438 mesh objects; 184 unique mesh datablocks; 4,526,960 raw mesh-object triangles; 688,637 triangles counted once per unique mesh; 501 evaluated/runtime mesh instances and 4,556,062 instance triangles; all seven required anchors; 49,787,440 bytes; SHA-256 `9E49EF372F3609EBDF0A68CA8A5F4EF5A0D38E08B5B28F9A463707E04F550CD5` |
| Factory-gate portal | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the interactive PBR shutter |
| Pawnshop hero entrance | `pawnshop_frontage_ready=True`; 15/15 modeled canopy parts; 15,492 canopy triangles; 8/8 solid wall modules; 8/8 authored inserts; 0 legacy visible gate/wall objects; columns clear the 7.6-meter doorway |
| Delivered urban-life expansion | 36/36 apartment-facade objects; two asymmetrical 3-by-3 overlays; three static 11,825-triangle bicycles; market tea cart and basket; pawnshop tea table and three stools; factory hand truck; finished CC0 pawnshop backdrop and modeled pavilion gate; five market shops (three Old Urban building and two Scan Old Building Street instances); two Old Urban building rear houses; five Chinese red lamps; factory replacement with three Old Urban building office/admin instances and two Scan Old Building Street workshops |
| Serialized GLB | 59,205,576 bytes; SHA-256 `E9CF20F1A1FFFAFBB12F233B21852B458ED361DDA9D10D2A77A24791BFD75307`; 501 mesh nodes; 4,556,062 mesh-node instance triangles; maximum texture dimension 1024 pixels |
| Godot authored-map import | `--validate-refinery-map` PASS; 501 authored meshes; 583 surfaces; every audited surface is material-backed; 4,556,062 authored instance triangles; 7/7 authored anchors; terminal checks 2/2/2/2; authored status screens 2/2 |
| Godot route clearance | `routes=True`; `route_probes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Godot quality and full runtime | Quality tier 1 is restored after the capture probes; all six representative views pass their configured budgets |

The Blender source count is based on saved mesh objects, while the 688,637
unique-mesh figure counts each shared datablock once. Dependency-graph
evaluation and export resolve the scene to 501 runtime mesh instances and
4,556,062 instance triangles. The Godot diagnostic imports those same 501
meshes and sums 583 material-backed runtime surfaces. These scopes are
intentionally different rather than conflicting.

### Final capture performance

The final high-tier policy disables shadows only on fine decorative meshes.
Model geometry, materials, and visibility ranges are unchanged. All six
representative captures pass their budgets:

| View | Draw calls | Objects | Primitives | Result |
| --- | ---: | ---: | ---: | --- |
| Overview | 416 | 612 | 4,267,626 | PASS |
| Victory street | 500 | 630 | 4,288,357 | PASS |
| Street-life bicycle close-up | 279 | 331 | 3,256,046 | PASS |
| Guangchang pawnshop | 308 | 478 | 2,139,132 | PASS |
| Red Star factory | 340 | 401 | 4,021,716 | PASS |
| Market footbridge | 512 | 756 | 4,612,312 | PASS |

The capture reports 955.7 MB video memory and 813.2 MB texture memory.

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
screens, pawnshop adaptation/composition, and factory-gate portal
geometry/composition are covered by the
repository's root MIT license, subject to `docs/CONTENT_PROVENANCE.md`. The CC0
Utility Box 01 and Television 02 bodies, along with all other third-party source
geometry, materials, textures, and font software, retain their recorded source
rights; packing, reusing a material on the portal, or exporting does not
relicense them as MIT content. See `LICENSE_EVIDENCE.md` for exact creators,
URLs, asset IDs, hashes, and local mapping.
