# Jianghai Old City DCC Source

`jianghai_old_city.blend` is the editable, packed Blender source for the static
Jianghai Old City extraction-map visual exported to
`assets/models/jianghai_old_city/jianghai_old_city.glb`.

The scene is a project-authored 1990s Lingnan river-industrial city
composition. The complete layout, streets, supporting geometry, district
assembly, material adaptations, art direction, Chinese sign wording and
placement, lighting used for review renders, objective-terminal status screens
and adaptations, factory-gate portal composition, and integration of licensed
source modules are produced for Operation Steel Tide. Each terminal body
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
blenderkit/scan_old_brick_building_red_small.blend
blenderkit/scan_old_building_street.glb
blenderkit/chinese_wood_house_wall_1k.blend
blenderkit/chinese_porcelain_lion.glb
NotoSansSC-VF.otf
```

The final DCC scene also incorporates four existing Poly Haven PBR surface
sets under `assets/textures/`: Asphalt 03, Concrete Floor, Gravel Embedded
Concrete, and Corrugated Iron. Rusty Painted Metal remains a tracked project
texture but is not part of the current Jianghai scene. The scene also contains
adapted instances of the already tracked Poly Haven CC0 Old Military Crate and
Concrete Road Barrier models under `assets/models/`, plus the eight external
Poly Haven CC0 1K glTF bundles listed above. All eight were acquired on
2026-08-28; exact creators, official URLs, bundle hashes, and mappings are in
`LICENSE_EVIDENCE.md`.

The Poly Haven **Modular Urban Apartments Facade** bundle was acquired and
evaluated on 2026-08-27, but it is not part of the authoritative `.blend` or
runtime GLB. The GLB contains no apartment nodes, meshes, materials, images,
or other asset payload. An `evaluated_not_used` provenance field may name the
evaluation; its source record and cache hashes remain in
`LICENSE_EVIDENCE.md` solely as an audit trail.

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

The export script verifies that the authoritative scene is open, flattens any
remaining tiled images, caps the longest runtime-texture dimension at 1024
pixels, recompresses eligible high-resolution runtime images as JPEG quality
90, rejects non-built-in font datablocks, packs external data, saves the
`.blend`, and exports:

- `assets/models/jianghai_old_city/jianghai_old_city.glb`
- `assets/models/jianghai_old_city/rollershutter_window_03.glb`

The second output is a reproducible standalone PBR door visual. The script
selects `JianghaiArtPass_EastShutter00`, makes a temporary normalized copy, and
exports only that adapted Rollershutter Window 03 mesh and its materials. The
result is 1,587,684 bytes with SHA-256
`48E78DFC37FF6310151B18BEA8AC8B080BE31ABED4BD882C0FA3F46E19B0B4B1`.
Two Old City `InteractiveBuildingDoor` instances use this GLB in place of the
enlarged Kenney `door-wide-closed` visible placeholder that appeared as an
olive-colored panel. Only the visible art changes: the existing gameplay door
keeps collision, animation, network state, and AI traversal.

It is an export/validation step, not an authoring or scene-generation system.
No script reconstructs the complete map. All further composition and modeling
work starts from the hand-edited `.blend`. Review PNGs under `previews/` are
maintained separately.

The main GLB contains the final static map geometry, materials, textures, and
custom provenance metadata. It excludes preview cameras and lights, the
unselected Modular Urban Apartments Facade asset payload, and the original
Noto font. Chinese sign outlines are ordinary static mesh geometry. The
standalone door GLB retains MP / Poly Haven CC0 provenance through its source
mesh in the authoritative packed scene.

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
overlay. The red-brick factory facade already contains an embedded industrial
roller shutter, so retaining that existing authored opening avoids a second
overlapping dark door in player-height and acceptance-review views.
The factory interactive shutter is framed by five visible objects authored and
aligned in Blender: two reused DCC brick piers, two pier caps, and a corrugated
roof. Their portal composition is final DCC art, not code-built primitive or
procedural visible geometry. Reused packed materials keep their recorded
third-party provenance.

Read-only audits on 2026-08-28 recorded these matching source, serialized, and
runtime results:

| Audit layer | Verified result |
| --- | --- |
| Authoritative Blender source | 429 mesh objects; 176 unique mesh datablocks; 4,664,722 object-instance triangles; 863,620 unique DCC-mesh triangles; all seven required anchors; 82,347,471 bytes; SHA-256 `3881F3653188A00328C85829FE06C7C61AD07510495791DD8537A38EB7816EF6` |
| Factory-gate portal | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the interactive PBR shutter |
| Serialized GLB | 95,837,888 bytes; SHA-256 `F61D82D77311BF1C2F8A3ACE1C0FFE967EC415220DABA9BF840237EC797CD0FA`; 525 nodes; 264 unique mesh resources; 515 mesh nodes; 275 glTF primitives; 50 materials; 100 images; 898,994 triangles counted once per unique mesh resource; 4,700,072 triangles across all mesh-node instances; maximum texture dimension 1024 pixels |
| Godot authored-map import | `--validate-refinery-map` PASS; 515 authored meshes; 575 surfaces; 575 material-backed surfaces; 4,700,072 authored instance triangles; 413 detail meshes; 7/7 authored anchors; terminal checks 2/2/2/2; authored status screens 2/2 |
| Godot route clearance | `routes=True`; `route_probes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Godot quality and full runtime | Quality tier 1 with a shadow count of 207; tier probe counts 102/207/373 and restores tier 1; 934 total runtime nodes; 625 total runtime mesh instances |

The 275 GLB primitives are serialized surface-equivalents counted once across
the 264 unique GLB mesh resources. The Godot diagnostic instead sums surfaces
across the 515 imported authored meshes, producing 575 runtime surfaces; all
575 have materials. These are different scopes rather than conflicting counts.
The full runtime total of 625 mesh instances is broader again because it also
includes scene meshes outside the authored-map import.

### Final capture performance

The final high-tier policy disables shadows only on fine decorative meshes.
Model geometry, materials, and visibility ranges are unchanged. All five
representative captures pass their budgets:

| View | Draw calls | Objects | Primitives | Result |
| --- | ---: | ---: | ---: | --- |
| Overview | 627 | 784 | 8,249,404 | PASS |
| Victory street | 832 | 1,086 | 9,596,938 | PASS |
| Guangchang pawnshop | 253 | 534 | 2,980,673 | PASS |
| Red Star factory | 443 | 632 | 4,743,175 | PASS |
| Market footbridge | 503 | 747 | 4,684,143 | PASS |

The capture reports 1,061.0 MB video memory and 919.5 MB texture memory.

## Rights boundary

All imported marketplace/model sources contained in the current DCC scene are
CC0.
Noto Sans SC is licensed under SIL OFL 1.1 and was used only during DCC
authoring to convert the required Chinese sign text to static glyph meshes.
The original font file is not committed or present in the final `.blend` or
GLB, and the export script enforces that boundary. The existing Poly Haven
surface textures are also CC0.

The project-authored layout, adaptation work, objective-terminal status
screens, and factory-gate portal geometry/composition are covered by the
repository's root MIT license, subject to `docs/CONTENT_PROVENANCE.md`. The CC0
Utility Box 01 and Television 02 bodies, along with all other third-party source
geometry, materials, textures, and font software, retain their recorded source
rights; packing, reusing a material on the portal, or exporting does not
relicense them as MIT content. See `LICENSE_EVIDENCE.md` for exact creators,
URLs, asset IDs, hashes, and local mapping.
