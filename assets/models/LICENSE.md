# Model Asset Licenses

## Steel Tide combat models

The Steel Tide M4A1 is a composite adaptation of **M4A1 Assault Rifle** by
OpenGameArt creator/uploader **nisu** and finished attachment components from
the **Quaternius Ultimate Guns Pack**. Both sources are published under CC0 1.0
Universal:

- Source: https://opengameart.org/content/m4a1-assault-rifle
- Official download: https://opengameart.org/sites/default/files/m4a1_0.zip
- Original publication date: 2022-04-24
- Acquisition date: 2026-08-28
- Exact license: CC0 1.0 Universal,
  https://creativecommons.org/publicdomain/zero/1.0/
- Attachment creator: Quaternius (`@Quaternius`)
- Attachment source:
  https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- Attachment acquisition date: 2026-08-20
- Attachment source mapping: `quaternius_ultimate_guns/LICENSE.md`
- Runtime output: `steel_tide_m4a1/steel_tide_m4a1.glb`
- Editable adaptation: `../../source_art/combat_models/steel_tide_m4a1.blend`
- Raw source and evidence: `../../source_art/third_party/nisu_m4a1/`
- Reproducible adaptation: `../../scripts/blender/build_nisu_m4a1.py`
- Detailed license and mapping record: `steel_tide_m4a1/LICENSE.md`

The main rifle uses nisu's textured source. The short foregrip comes from
`scarl.glb`, the rounder suppressor from `mp5a5.glb`, and the open-aperture
optic housing from `axmc.glb`; the normal muzzle remains selected nisu
geometry. Exact source objects, runtime nodes, removed source-glass panes, and
transform-only muzzle/reticle markers are recorded in
`steel_tide_m4a1/LICENSE.md`. Both source collections, the editable composite,
and the runtime model remain CC0 and are not relicensed as MIT. The
project-authored adaptation script remains covered by the repository's root MIT
license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

The shared first-person micro, holographic, and magnified optic set is likewise
adapted in Blender from finished Quaternius Ultimate Guns Pack components. Its
runtime output is `steel_tide_optics/steel_tide_optics.glb`, its authoritative
editable source is `../../source_art/combat_optics/steel_tide_optics.blend`, and
its reproducible adaptation is `../../scripts/blender/build_authored_optics.py`.
All three source components are CC0 1.0 Universal. Their exact source-object
mapping, acquisition date, license, output hashes, removed source-glass panes,
and physically open aperture checks are recorded in
`steel_tide_optics/LICENSE.md` and `../../source_art/combat_optics/README.md`.

The active Steel Tide AK-47 is a Blender adaptation of taradavies' finished
OpenGameArt AK-47, published under CC0 1.0 Universal. The raw source was
published on 2023-04-15 and acquired on 2026-08-29 from
https://opengameart.org/content/ak-47-1. The adaptation removes the source
file's unpacked, unlicensed image references, rebuilds six PBR materials with
two deterministic project-authored wood textures, separates real magazine,
charging-handle, and iron-sight geometry, and refits an authored source
component as the optic rail bridge. First-person and world exports contain
97,372 and 24,488 unique triangles respectively. Their runtime outputs are
`steel_tide_ak74/ak47_reloadable_{fp,world}.glb`, their authoritative editable
source is `../../source_art/reloadable_weapons/ak47_reloadable.blend`, and the
reproducible adaptation is
`../../scripts/blender/build_taradavies_ak47.py`. Exact hashes, the source and
license evidence, the excluded dependency record, hierarchy mapping, packed
texture record, and rail-contact checks are in `steel_tide_ak74/LICENSE.md`,
`../../source_art/third_party/taradavies_ak47/LICENSE_EVIDENCE.md`, and
`../../source_art/reloadable_weapons/README.md`.

## Project-authored first-person field-use props

The trauma kit, dressing packet, injector, curved armor plate, plate carrier,
materials, moving lids, and six contact markers were created for Operation
Steel Tide in Blender 4.5 on 2026-08-28. They contain no third-party geometry,
textures, logos, or marketplace content and are covered by the repository's
root MIT license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

- Creator: Operation Steel Tide project contributors, with the AI-assisted DCC
  workflow disclosed in `docs/CONTENT_PROVENANCE.md`.
- Official source URL: not applicable; this is repository-original content.
- Exact license: the repository root MIT license.
- Required attribution: preserve the root MIT copyright and permission notice;
  no additional third-party attribution is required.
- Creation date: 2026-08-28.
- Editable source: `../../source_art/field_use/field_use_props.blend`.
- Runtime output: `steel_tide_field_use/field_use_props.glb`.
- Studio preview: `steel_tide_field_use/field_use_props_preview.png`.
- Reproducible build: `../../scripts/blender/build_field_use_props.py`.
- Detailed local record: `steel_tide_field_use/LICENSE.md` and
  `../../source_art/field_use/README.md`.

The verified runtime GLB contains 138 authored meshes, 22,276 triangles, and 17
PBR materials within a 0.380 by 0.241 by 0.487 metre set. The Blender build
saves the authoritative source and embedded GLB, renders the preview, reimports
the GLB, and rejects missing contract nodes, external payloads, material drift,
changed bounds, or an out-of-budget mesh.

## Project-authored demolition device

The compact 5v5 objective device was created for Operation Steel Tide in
Blender 4.5 on 2026-08-29. It contains no third-party geometry, textures, fonts,
logos, or marketplace content and is covered by the repository's root MIT
license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

- Creator: Operation Steel Tide project contributors, with the AI-assisted DCC
  workflow disclosed in `docs/CONTENT_PROVENANCE.md`.
- Official source URL: not applicable; this is repository-original content.
- Exact license: the repository root MIT license.
- Required attribution: preserve the root MIT copyright and permission notice;
  no additional third-party attribution is required.
- Creation date: 2026-08-29.
- Editable source: `../../source_art/demolition_device/demolition_device.blend`.
- Runtime output: `steel_tide_demolition_device/demolition_device.glb`.
- Studio preview: `steel_tide_demolition_device/demolition_device_preview.png`.
- Reproducible build: `../../scripts/blender/build_demolition_device.py`.
- Detailed local record: `steel_tide_demolition_device/LICENSE.md` and
  `../../source_art/demolition_device/README.md`.

The verified embedded GLB contains 48 authored meshes, 9,216 triangles, and
nine scalar PBR materials within 0.344 by 0.201 by 0.164 metres. Its SHA-256 is
`580F71F6ACED03888734BCD73C863A5CFB2DD35E33F415927EE899A7A8897A7F`.
The Blender build saves the packed source, exports and reimports the runtime
GLB, verifies the case/screen/status-light/carry-socket contract, enforces the
asset budget and bounds, and renders the checked-in preview.

The legacy Steel Tide operator model remains project-authored by
`scripts/blender/generate_combat_models.py`. Its editable source is
`../../source_art/combat_models/steel_tide_operator.blend`, and its generated
output is `steel_tide_operator/steel_tide_operator.glb`. Those operator files
contain no copied third-party geometry or textures and are covered by the root
MIT license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

## Project-authored melee weapons

The tactical knife, Zhanma Dao, and Tianxuan Dao were created for Operation
Steel Tide in Blender 4.5 on 2026-08-28. They contain no third-party geometry,
textures, or marketplace content and are covered by the repository's root MIT
license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

- Creator: Operation Steel Tide project contributors, with the AI-assisted DCC
  workflow disclosed in `docs/CONTENT_PROVENANCE.md`.
- Official source URL: not applicable; these are repository-original assets.
  The canonical editable sources are the local `source_art/melee_weapons/*.blend`
  files mapped below.
- Exact license: the repository root MIT license.
- Required attribution: redistributions must preserve the root MIT copyright
  and permission notice; no additional third-party attribution is required.
- Creation/acquisition date: 2026-08-28.

| Asset | Editable Blender source | Runtime GLB | Meshes | Triangles | Materials |
| --- | --- | --- | ---: | ---: | ---: |
| Tactical knife | `../../source_art/melee_weapons/tactical_knife.blend` | `steel_tide_melee/tactical_knife.glb` | 15 | 13,216 | 4 |
| Zhanma Dao | `../../source_art/melee_weapons/zhanma_dao.blend` | `steel_tide_melee/zhanma_dao.glb` | 14 | 17,548 | 4 |
| Tianxuan Dao | `../../source_art/melee_weapons/tianxuan_dao.blend` | `steel_tide_melee/tianxuan_dao.glb` | 19 | 18,212 | 4 |

The reproducible Blender DCC workflow is implemented by
`../../scripts/blender/build_melee_weapons.py`. It authors the blade profiles,
edge geometry, guards, collars, grips, wraps, pommels, decorative fittings, and
four scalar PBR materials as native Blender meshes and materials; applies
beveling and smooth shading; places named gameplay markers at both grips and
the blade endpoints; saves the editable `.blend` scenes; exports the runtime
GLBs; and renders the studio previews beside them. It then re-imports every GLB
and rejects missing roots, markers, materials, triangle-count drift, or an
out-of-budget mesh. The triangle counts above are the exact triangulated GLB
counts verified by that round-trip, not estimates.

## Bazaar Crossing V2 authored demolition-map composite

Bazaar Crossing V2 is a Blender 4.5 composition created on 2026-08-28 for the
sixth demolition-map slot. The project-authored layout, transforms, material
and UV adaptations, metadata, invisible gameplay scaffolding, review setup,
and deterministic DCC workflow are covered by the repository's root MIT
license, subject to the disclosure in `../../docs/CONTENT_PROVENANCE.md`.
Every exported visible mesh remains an arrangement or real DCC adaptation of
a finished source dedicated to **CC0 1.0 Universal**
(`https://creativecommons.org/publicdomain/zero/1.0/`) and is not relicensed
as MIT. CC0 requires no attribution; the creator credits below are retained
for provenance.

The immutable palette pins these structural sources and maps them to named
source objects before composition:

| Source / creator | Official source / acquired | Repository-local source -> pinned Bazaar object | Bazaar V2 use |
| --- | --- | --- | --- |
| **Modular Industrial Pieces**, Trey Ramm (`minime453`) | `https://opengameart.org/content/modular-industrial-kit`; 2026-08-27 | `../../source_art/third_party/trey_modular_industrial/Meshes/Details/IndStairsWideFull.fbx` -> `BazaarSource_IndStairsWideFull` | Six exact-endpoint stair assemblies |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Floors/IndFloorGreyPlatformFull.fbx` -> `BazaarSource_IndFloorGreyPlatformFull` | Ground, paving, and painted site surfaces |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Trims/IndRoofTrimBStraightFull.fbx` -> `BazaarSource_IndRoofTrimBStraightFull` | Rails and stair guardrails |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Details/IndColumnFree.fbx` -> `BazaarSource_IndColumnFree` | Warehouse/back-market columns, newels, and lamp supports |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Details/IndColumnFreeCap.fbx` -> `BazaarSource_IndColumnFreeCap` | Structural capitals |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Foundation/IndFoundationAStraightFull.fbx` -> `BazaarSource_IndFoundationAStraightFull` | Thick walls, counters, partitions, rack posts, and stair foundations |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx` -> `BazaarSource_IndRoofDarkGreyAngledFull` | Retained roof vocabulary |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Walls/IndWallFull.fbx` -> `BazaarSource_IndWallFull` | Interior wall vocabulary |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Walls/IndWallArchDouble.fbx` -> `BazaarSource_IndWallArchDouble` | A courtyard, B loading, and back-market arcades; open portals |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Walls/IndWallArchDoubleColumns.fbx` -> `BazaarSource_IndWallArchDoubleColumns` | Arcade structural vocabulary |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Walls/IndWallArchDoubleCapGrey.fbx` -> `BazaarSource_IndWallArchDoubleCapGrey` | Arcade caps |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Doors/IndDoorFrameSingle.fbx` -> `BazaarSource_IndDoorFrameSingle` | Door and partition frames |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Roofs/IndRoofDarkGreyFull.fbx` -> `BazaarSource_IndRoofDarkGreyFull` | Closed-block, warehouse, stair-hall, and market roofs |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Floors/IndFloorGreyFull.fbx` -> `BazaarSource_IndFloorGreyFull` | Solid floor/ceiling and continuous storage-shelf vocabulary |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Windows/IndWindowBFull.fbx` -> `BazaarSource_IndWindowBFull` | Industrial facade and clerestory windows |
| Same | Same | `../../source_art/third_party/trey_modular_industrial/Meshes/Trims/IndRoofTrimAStraight.fbx` -> `BazaarSource_IndRoofTrimAStraight` | Cornices, roof ridges, shop fascias, awnings, and interior beams |
| **Downtown City MegaKit**, Quaternius (`@Quaternius`) | `https://quaternius.com/packs/downtowncitymegakit.html`; 2026-08-19 | `quaternius_downtown_city/Brick_Plain_1.gltf` -> `BazaarSource_QuatBrickPlain` | Red-brick wall vocabulary |
| Same | Same | `quaternius_downtown_city/DoorFrame_Trim.gltf` -> `BazaarSource_QuatDoorFrameTrim` | Detailed personnel doors and partition rhythm |
| Same | Same | `quaternius_downtown_city/Brick_Window_CurvedDouble.gltf` -> `BazaarSource_QuatBrickWindowCurvedDouble` | Curved brick windows for Mid and varied closed blocks |
| Same | Same | `quaternius_downtown_city/Brick_Window_Trim.gltf` -> `BazaarSource_QuatBrickWindowTrim` | A, Mid, back-market, boundary, shopfront-band, and closed-block facades |
| Same | Same | `quaternius_downtown_city/Floor_4x4.gltf` -> `BazaarSource_QuatFloor4x4` | Double-sided interior floors, ceilings, decks, roofs, and rooftop monitor caps |
| Same | Same | `quaternius_downtown_city/Metal_FirstFloor_Window.gltf` -> `BazaarSource_QuatMetalFirstFloorWindow` | B warehouse, rooftop monitors, and east industrial facade vocabulary |

Quaternius's local license evidence is
`quaternius_downtown_city/QUATERNIUS_LICENSE.txt`; the Trey source folder
retains the creator's original README, source-page evidence, and CC0 record.

The palette also pins the following CC0 facade, prop, and PBR sources. The
last column records their repository-local source mapping and their exact V2
role; all are copied into
`../../source_art/world/bazaar_crossing/bazaar_crossing_source_palette.blend`
before the deterministic build runs.

| Source | Creator / official source | Acquired | Repository-local mapping -> Bazaar V2 use |
| --- | --- | --- | --- |
| Old Urban building | Abobla O.S / `https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/` | 2026-08-28 | Source evidence in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> two of four outer landmark facades |
| Scan Old Building Street | Free poly / `https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/` | 2026-08-28 | Source evidence in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> one outer landmark facade |
| Chinese red lamp | Kin Chen / `https://www.blenderkit.com/asset-gallery-detail/b97e433c-2eb1-46b8-9633-5bdee21e4e7a/` | 2026-08-27 | Source evidence in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> 18 visibly supported interior lamps |
| Pink city bicycle | Kin Chen / `https://www.blenderkit.com/asset-gallery-detail/4c1a83c1-829f-4c00-878e-9e73c6b89c3b/` | 2026-08-28 | Source evidence in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> back-market landmark |
| Coffee Cart 01 | Joe Seabuhr / Poly Haven / `https://polyhaven.com/a/CoffeeCart_01` | 2026-08-28 | `polyhaven_residential_street/CoffeeCart_01/` -> all three original parts as a B-hall landmark |
| Chinese Tea Table | Kirill Sannikov / Poly Haven / `https://polyhaven.com/a/chinese_tea_table` | 2026-08-28 | Source bundle/hash record in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> A-courtyard landmark |
| Chinese Stool | Kirill Sannikov / Poly Haven / `https://polyhaven.com/a/chinese_stool` | 2026-08-28 | Source bundle/hash record in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> A-courtyard landmarks |
| Wicker Basket 01 | Kuutti Siitonen / Poly Haven / `https://polyhaven.com/a/wicker_basket_01` | 2026-08-28 | `polyhaven_residential_street/wicker_basket_01/` -> B and back-market dressing |
| Hand Truck | Mutanzom3D / Poly Haven / `https://polyhaven.com/a/hand_truck` | 2026-08-28 | Source bundle/hash record in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> A-warehouse landmark |
| Old Military Crate | Jack Mava / Poly Haven / `https://polyhaven.com/a/old_military_crate` | 2026-08-06 | `old_military_crate/` -> limited A-warehouse dressing |
| Barrel 03 | Serhii Khromov / Poly Haven / `https://polyhaven.com/a/barrel_03` | 2026-08-28 | Source bundle/hash record in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> retained source, not primary V2 cover |
| Plastic Crate 02 | Fabi_G / Poly Haven / `https://polyhaven.com/a/plastic_crate_02` | 2026-08-28 | Source bundle/hash record in `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` -> packed Bazaar palette -> Mid produce landmark |
| Asphalt 03 | Charlotte Baglioni and Dario Barresi / Poly Haven / `https://polyhaven.com/a/asphalt_03` | 2026-08-06 | `../textures/asphalt_03_{diff,normal,rough}_1k.jpg` -> `BazaarWetAsphalt` ground PBR |
| Gravel Embedded Concrete | Charlotte Baglioni / Poly Haven / `https://polyhaven.com/a/gravel_embedded_concrete` | 2026-08-06 | `../textures/gravel_embedded_concrete_{diff,normal,rough}_1k.jpg` -> `BazaarStonePaving` route/stair PBR |
| Concrete Floor | eye-candy.xyz / Poly Haven / `https://polyhaven.com/a/concrete_floor` | 2026-08-06 | `../textures/concrete_floor_{diff,normal,rough}_1k.jpg` -> `BazaarWeatheredConcrete` structural PBR |

Artifact and audit mapping:

- Runtime output: `bazaar_crossing/bazaar_crossing.glb`, 115,853,196 bytes,
  SHA-256
  `93E7A925061FFF93DCC25F72E5353C584ED9B062831E9C0BD6439F77B6009D96`.
- Immutable map-local source palette:
  `../../source_art/world/bazaar_crossing/bazaar_crossing_source_palette.blend`,
  SHA-256
  `1E6C91C5AA1B7D798B5C603BB2CE40C89B5C3255A9047209EEAB109C9F4730F9`.
- Editable packed source:
  `../../source_art/world/bazaar_crossing/bazaar_crossing.blend`, 50,974,276
  bytes, SHA-256
  `7025690DA87D10E7CCCE4381A4EB05E0BEB6F7ABF7D989F63EF5301272B05615`.
- Reproducible build: `../../scripts/blender/build_bazaar_crossing.py`.
- Rights and exact source-object mapping: `bazaar_crossing/LICENSE.md` and
  `../../source_art/world/bazaar_crossing/LICENSE.md`.
- Deterministic report:
  `../../source_art/world/bazaar_crossing/bazaar_crossing_build_report.json`.

The final V2 scene contains 770 exported visible mesh nodes, 709 unique meshes,
1,061 material surfaces, 49 DCC materials, 58 DCC textures, 873,789 unique
triangles, and 1,172,379 delivered instance triangles. The textures are capped
at 1024 pixels and have an estimated 203.473 MiB RGBA8 plus full-mip-chain
cost. Static consolidation reduced 1,547 draw nodes to 770 and 2,160 surfaces
to 1,061 without changing either triangle count. The GLB is exported without
`KHR_draco_mesh_compression`; `EXT_texture_webp` is its only required
extension. Its round trip retains all 770 visible meshes and the exact
instance-triangle count while checking scene bounds, four complete enterable
interiors, three deck heights, all six 3.2-meter stairs, UV/material coverage,
explicit CC0 provenance, and absence of Hero Mountain, Coast Line, all CC BY,
paid, editorial, private-store, or unclear-license content. The complete
Jianghai Old City runtime GLB is neither embedded nor referenced.

## Poly Haven CC0 models

The following two [Poly Haven](https://polyhaven.com/) models adapted into
Jianghai Old City were acquired on 2026-08-06:

| Model | Creator | Official source | Repository-local source mapping |
| --- | --- | --- | --- |
| Old Military Crate | Jack Mava | https://polyhaven.com/a/old_military_crate | `old_military_crate/old_military_crate.gltf`, `old_military_crate/old_military_crate.bin`, and `old_military_crate/textures/old_military_crate_{arm,diff,nor_gl}_1k.jpg` |
| Concrete Road Barrier | Amal Kumar | https://polyhaven.com/a/concrete_road_barrier | `concrete_road_barrier/concrete_road_barrier.gltf`, `concrete_road_barrier/concrete_road_barrier.bin`, and `concrete_road_barrier/textures/concrete_road_barrier_{arm,diff,nor_gl}_1k.jpg` |

Exact license: CC0 1.0 Universal,
https://creativecommons.org/publicdomain/zero/1.0/. Attribution is not
required; creator names are retained as provenance and courtesy credit. The
Jianghai Old City adaptation of both models is packed into
`../../source_art/world/jianghai_old_city/jianghai_old_city.blend` and exported
to `jianghai_old_city/jianghai_old_city.glb`.

### Poly Haven residential-street additions

The following six models were acquired on 2026-08-28:

- Street Lamp 01: https://polyhaven.com/a/street_lamp_01
- Metal Trash Can: https://polyhaven.com/a/metal_trash_can
- Coffee Cart 01: https://polyhaven.com/a/CoffeeCart_01
- Wooden Crate 01: https://polyhaven.com/a/wooden_crate_01
- Plastic Crate 01: https://polyhaven.com/a/plastic_crate_01
- Wicker Basket 01: https://polyhaven.com/a/wicker_basket_01

Each is dedicated to the public domain under CC0 1.0 Universal; Poly Haven's
official license is https://polyhaven.com/license. Creator credits, exact CC0
terms, official API revision identifiers, source-to-runtime mapping, per-file
MD5 and SHA-256 evidence, and Blender processing details are recorded in
`polyhaven_residential_street/LICENSE.md`.

### Jianghai Old City authored-asset pass

The following finished Poly Haven assets were acquired as 1K glTF bundles for
the Jianghai Old City authored-asset and urban-life passes:

| Model | Creator | Acquired | Official source | Repository-external acquisition input |
| --- | --- | --- | --- | --- |
| Modular Urban Apartments Facade | James Ray Cock | 2026-08-27 | https://polyhaven.com/a/modular_urban_apartments_facade | `poly_haven/modular_urban_apartments_facade/modular_urban_apartments_facade_1k.gltf`, its `.bin`, and `textures/*` |
| Television 02 | Benny Weimer | 2026-08-28 | https://polyhaven.com/a/television_02 | `poly_haven/television_02/television_02_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Exterior Aircon Unit | Monsta3D | 2026-08-28 | https://polyhaven.com/a/exterior_aircon_unit | `poly_haven/exterior_aircon_unit/exterior_aircon_unit_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Rollershutter Window 03 | MP | 2026-08-28 | https://polyhaven.com/a/rollershutter_window_03 | `poly_haven/rollershutter_window_03/rollershutter_window_03_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Trashbag | Benny Weimer | 2026-08-28 | https://polyhaven.com/a/trashbag | `poly_haven/trashbag/trashbag_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Utility Box 01 | James Ray Cock | 2026-08-28 | https://polyhaven.com/a/utility_box_01 | `poly_haven/utility_box_01/utility_box_01_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Barrel 03 | Serhii Khromov | 2026-08-28 | https://polyhaven.com/a/barrel_03 | `poly_haven/barrel_03/barrel_03_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Plastic Crate 02 | Fabi_G | 2026-08-28 | https://polyhaven.com/a/plastic_crate_02 | `poly_haven/plastic_crate_02/plastic_crate_02_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Security Camera 01 | Alexander Otterbeck (modeling and texturing); Yann Kervran (rigging) | 2026-08-28 | https://polyhaven.com/a/security_camera_01 | `poly_haven/security_camera_01/security_camera_01_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Chinese Tea Table | Kirill Sannikov | 2026-08-28 | https://polyhaven.com/a/chinese_tea_table | `poly_haven/chinese_tea_table/chinese_tea_table_1k.gltf`, its `.bin`, and three 1K texture sidecars |
| Chinese Stool | Kirill Sannikov | 2026-08-28 | https://polyhaven.com/a/chinese_stool | `poly_haven/chinese_stool/chinese_stool_1k.gltf`, its `.bin`, and three 1K texture sidecars |
| Hand Truck | Mutanzom3D | 2026-08-28 | https://polyhaven.com/a/hand_truck | `poly_haven/hand_truck/hand_truck_1k.gltf`, its `.bin`, and three 1K texture sidecars |

Each bundle is CC0 1.0 Universal under
https://creativecommons.org/publicdomain/zero/1.0/. Attribution is not
required; the contributor names are retained as provenance and courtesy
credit. The acquisition bundles remain in the external cache and are not
committed as separate raw files. Adapted geometry and materials are packed
into `../../source_art/world/jianghai_old_city/jianghai_old_city.blend` and
exported to `jianghai_old_city/jianghai_old_city.glb`; exact cache hashes and
the packed-output mapping are recorded in the scene's `LICENSE_EVIDENCE.md`.
For Security Camera 01, the delivered composite contains only static geometry,
materials, and textures; the source rig and animations are not shipped in the
packed scene or runtime export.

The pawnshop gate canopy additionally adapts **Chinese Four-corner Pavilion -
Free** by **VVayToyek**, acquired from
https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free on 2026-08-28. The
official page dedicates the asset to the public domain under CC0 1.0 Universal.
Fifteen modeled timber, tile, column, lattice, rafter, and ornament parts are
retained and reshaped in Blender; the raw ZIP and FBX stay in the external
acquisition cache. Exact hashes, license-page evidence, and the packed-output
mapping are recorded in the scene's `LICENSE_EVIDENCE.md`. The reproducible DCC
adaptation is `../../scripts/blender/rebuild_pawnshop_frontage.py`.

The urban-life pass additionally embeds the already tracked Coffee Cart 01 by
Joe Seabuhr and Wicker Basket 01 by Kuutti Siitonen from
`polyhaven_residential_street/`. Their existing per-file evidence remains in
`polyhaven_residential_street/LICENSE.md` and is not duplicated here.

Rollershutter Window 03 also has a repository-local derived runtime mapping:
`jianghai_old_city/rollershutter_window_03.glb` (187,940 bytes; SHA-256
`C4884AFCD7560E4BB23320A8C311DB0011504F7C5FEE30D58C266D54F7C6B166`).
The export script reproducibly selects the adapted
`JianghaiArtPass_EastShutter00` mesh from the authoritative packed `.blend`,
normalizes a temporary copy, and exports its PBR geometry and materials. This
standalone GLB is retained as an alternate/legacy asset but no longer supplies
either of the two landmark Old City `InteractiveBuildingDoor` visuals. Those
landmark doors use Kenney's CC0 `kenney_factory_kit/door-hinged.glb` at two
1.45-by-2.65-meter personnel openings and swing sideways through 96 degrees.
Their collision, animation, network state, and AI traversal remain project
gameplay behavior. The retained shutter derivative keeps MP's Poly Haven CC0
provenance and is not relicensed as project-authored MIT art.

The two current static entry facades reuse finished CC0 Downtown City MegaKit
modules by Quaternius: 18 packed instances of
`quaternius_downtown_city/Brick_Plain_1.gltf` and two packed instances of
`quaternius_downtown_city/DoorFrame_Trim.gltf`. Each pawnshop/factory facade is
a 10-object DCC composition containing nine brick modules and one doorframe.
The source modules retain Quaternius's CC0 license and are not relicensed as
project-authored MIT art.

### Jianghai Old City valley environment inputs

The shipped valley pass adapts two Poly Haven surface sets acquired on
2026-08-28, one finished Poly Haven photogrammetry model acquired on
2026-08-29, and one complete mountain model acquired from Sketchfab on
2026-08-29. The raw downloads are retained under the separate private
`JIANGHAI_VALLEY_ACQUISITION_ROOT`, not as repository-local source bundles:

| Source | Creator | Official source and license | Private-cache input | Delivered mapping |
| --- | --- | --- | --- | --- |
| Rocky Terrain | Amal Kumar | https://polyhaven.com/a/rocky_terrain | 2K diffuse, displacement, OpenGL-normal, and roughness maps under `rocky_terrain/textures/` | Diffuse, normal, and roughness remain adapted to the sides of the project-authored `OldCityFoundation`. Capped 512-pixel DCC images are packed into the `.blend` and main GLB. The verified displacement input remains private and is not embedded or used to generate visible geometry |
| Gravel Floor 03 | Charlotte Baglioni | https://polyhaven.com/a/gravel_floor_03 | 2K diffuse, displacement, OpenGL-normal, and roughness maps under `gravel_floor_03/textures/` | Diffuse, normal, and roughness dress both the top of the project-authored `OldCityFoundation` and the single Coast-Line-derived perimeter-ground composite. The ground material uses base-color factor `(0.92, 0.78, 0.62, 1.0)` and a 7-meter affine world-XY UV layout. Capped 512-pixel DCC images are packed into the `.blend` and main GLB. The verified displacement input remains private and is not embedded or used to generate visible geometry |
| Coast Line 01 | Rob Tuytel (photography and processing); Rico Cilliers (cleanup) | https://polyhaven.com/a/coast_line_01; CC0 1.0 Universal under https://polyhaven.com/license | One 2K glTF, binary buffer, and diffuse/ARM/OpenGL-normal sidecars under `coast_line_01/` | Geometry-only source for `JianghaiPerimeterGroundComposite`, a single 84,960-vertex, 168,480-triangle modeled ground mesh assembled in Blender from eight Coast Line 01 scan placements. Its source material and images are not embedded; the delivered visible surface uses Charlotte Baglioni's CC0 Gravel Floor 03 PBR maps |
| Hero Mountain | solararchitect | https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca; CC BY 4.0 under http://creativecommons.org/licenses/by/4.0/ | Original-format Sketchfab download retained privately under `hero_mountain/`; the selected OBJ plus Color, Normal, and Roughness maps are the delivery inputs | The complete mountain is decimated to one shared 14,000-triangle distant LOD, its PBR graph is rebuilt from the selected maps with a 512-pixel pack cap, and 12 visual-only instances form staggered inner and outer six-mountain rings. The source AO, height, and displacement maps remain private and are not embedded or used to generate geometry |

The final Coast-derived ground is real authored DCC geometry, not camera
masking and not a primitive or runtime-procedural visible substitute. Eight
modeled Coast Line 01 scan placements are joined and shaped into the single
`JianghaiPerimeterGroundComposite`. Its saved topology is one connected
component with 84,960 vertices, 168,480 triangles, two boundary loops totaling
1,440 edges, zero degenerate triangles, and zero invalid face normals. Bounds
are X `-600.878..600.853`, Y `-540.340..660.056`, and Z
`-12.7965..5.0390` meters, for 17.835 meters of modeled vertical relief.

The continuity deformation is driven by signed distance from the actual
projected top footprint of `OldCityFoundation`, rather than a generic radial
mask. It keeps the footprint and safety margin below the platform and blends
the authored Coast relief outward. Coverage is 1.000, maximum audited
foundation gap is 0.103 meters, and the safe-area highest ground is -0.120
meters. Relief is 0.969 meters within 0-60 meters of the foundation and 3.955
meters from 60-160 meters. The ground slope RMS/p90/p99/maximum is
0.0579/0.0869/0.2331/0.6620, and the full ring-coverage gate passes
7,920/7,920 probes.

The visible surface uses the Gravel Floor 03 diffuse, OpenGL-normal, and
roughness maps, with base-color factor `(0.92, 0.78, 0.62, 1.0)`. Its affine
world-XY UV scale is 7 meters; the DCC and serialized-GLB coordinate errors are
`3.27e-6` and `4.36e-6`, within the `1.2e-5` gate, and both Jacobian checks pass.
No Coast source material or image datablock is embedded in the packed `.blend`
or runtime GLB. The final DCC pass also tapers and buries the north-end vertical
caps of `AuthoredStreetNetwork/CentralAvenueCurbW` and
`AuthoredStreetNetwork/CentralAvenueCurbE`; DCC and GLB ray gates report zero
exposed curb-side hits at that road end.

The three Poly Haven cliff scans below were acquired and evaluated during the
2026-08-29 art search, but they are not embedded in the final packed `.blend`
or runtime GLB and are not inputs to the shipped valley composition:

| Evaluated source | Creator | Official source | Final status |
| --- | --- | --- | --- |
| Coastal Cliff 01 | Rob Tuytel (photography and processing); Rico Cilliers (cleanup) | https://polyhaven.com/a/coastal_cliff_01 | Private evaluation source only; no geometry, material, or texture from this asset is present in the delivered artifact |
| Coastal Cliff 02 | Rob Tuytel | https://polyhaven.com/a/coastal_cliff_02 | Private evaluation source only; no geometry, material, or texture from this asset is present in the delivered artifact |
| Namaqualand Cliff 02 | Dario Barresi (photography); Rico Cilliers (modeling) | https://polyhaven.com/a/namaqualand_cliff_02 | Private evaluation source only; no geometry, material, or texture from this asset is present in the delivered artifact |

Rocky Terrain, Gravel Floor 03, Coast Line 01, and the three evaluated cliff
scans are CC0 1.0 Universal under https://polyhaven.com/license and
https://creativecommons.org/publicdomain/zero/1.0/. Attribution is not
required for those Poly Haven sources; all creator credits are retained as
provenance. Hero Mountain is licensed separately under CC BY 4.0: distributions
of the adapted mountain must credit **solararchitect**, retain the Hero
Mountain source and CC BY 4.0 links above, and indicate that Operation Steel
Tide modified the source by decimation, PBR-node reconstruction, 512-pixel
texture capping/packing, uniform scaling, rotation, and multi-instance valley
composition. Hero Mountain and its adapted geometry and textures are not
relicensed as MIT.

The raw source files remain private and uncommitted. The `OldCityFoundation`
geometry, its applied hand chamfer, UV layout, material split, placement, scale,
the composition and deformation of the single ground mesh assembled from eight
Coast Line 01 scan placements, and the placement/orientation composition of 12
Hero Mountain instances are project-authored DCC work covered
by the root MIT license, subject to the AI-assistance disclosure in
`../../docs/CONTENT_PROVENANCE.md`; packing does not relicense any underlying
third-party contribution. No displacement map is used to generate delivered
visible geometry.

`../../scripts/blender/build_jianghai_valley_environment.py` performs the
offline Blender authoring pass against the authoritative `.blend`. It requires
`JIANGHAI_VALLEY_ACQUISITION_ROOT`, verifies the 17 selected private authoring
inputs against the recorded source digests, updates the valley hierarchy and
existing authored foundation, validates ground coverage, the staggered two-ring
mountain composition, visual bounds, material/rights metadata, and triangle
budgets, and saves the packed DCC result. Exact private-file hashes, official API and
license-evidence hashes, and the private-cache-to-delivery mapping are in
`../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`.

## Jianghai Old City authored composite

The Jianghai Old City extraction-map visual is a project-authored Blender
composition that adapts redistributable third-party source assets into one
static runtime scene:

- Runtime output: `jianghai_old_city/jianghai_old_city.glb`
- Landmark interactive-door visual: `kenney_factory_kit/door-hinged.glb`
- Enterable-residence interactive-door visual:
  `jianghai_old_city/jianghai_lattice_door.glb`
- Enterable-residence door DCC source and evidence:
  `../../source_art/props/jianghai_lattice_door/`
- Retained alternate/legacy shutter derivative: `jianghai_old_city/rollershutter_window_03.glb`
- Retained CC0 sky evidence: `../textures/kloppenheim_06_puresky_1k.hdr`; Poly
  Haven evidence is in `../textures/LICENSE.md`. It is not embedded in the map
  GLB, and the current `JianghaiOldCityAtmosphere` uses a procedural sky rather
  than loading this panorama.
- Authoritative editable DCC source: `../../source_art/world/jianghai_old_city/jianghai_old_city.blend`
- Valley DCC build script: `../../scripts/blender/build_jianghai_valley_environment.py`
- Valley private input contract: `JIANGHAI_VALLEY_ACQUISITION_ROOT`; raw
  Coast Line 01, Hero Mountain, Rocky Terrain, and Gravel Floor 03 delivery
  inputs are not committed. Retained Coastal Cliff 01, Coastal Cliff 02, and
  Namaqualand Cliff 02 evaluation downloads are likewise private but are not
  embedded in or required by the final delivered artifact.
- Runtime export script: `../../scripts/blender/export_jianghai_old_city.py`
- Chinese district rebuild: `../../scripts/blender/rebuild_jianghai_chinese_district.py`
- Source and license evidence: `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`
- Initial external inputs were acquired on 2026-08-27; the additional
  BlenderKit and Poly Haven assets identified below were acquired on
  2026-08-28. Coast Line 01, the three evaluated-only cliff scans, and
  solararchitect's Hero Mountain were acquired on 2026-08-29.

The current visible-building delivery is the project-authored Blender rebuild
completed on 2026-08-29. It replaces 66 old visible anchors with three shared
Chinese-profile meshes and contains 42 authored density placements, including
the west/east `Edge04`-`Edge06` placements. The source mapping is:

- the hall profile is an adapted LOD of Free poly's CC0 **Chinese Temple 2**;
- the arcade-shop and gate-house profiles use clean Quaternius Buildings Pack
  bodies, adapted facade/eaves parts from VVayToyek's CC0 **Chinese
  Four-corner Pavilion - Free**, and an extracted/decimated Temple 2 roof; and
- existing Chinese red lamp, Chinese Porcelain Lion, Quaternius, and Poly Haven
  assets remain available as the already licensed district dressing and
  supporting modules recorded below.

Six reviewed main-street arcade shops have unique Blender-authored doorway
apertures and retained provenance metadata. Their surrounding facades still
derive from the same registered Chinese-profile sources above; no new external
building asset was acquired. The apertures are created offline by mesh-plane
splitting and face removal so the non-manifold joined source meshes do not leave
runtime or invisible boolean plugs. The final cleanup removes only the obsolete
`JianghaiExpansion_Facade_EastPhoto_F0_C1_Insert` that covered the East Photo
House opening; the adjacent `F0_C1_Wall` and `WestClock` facade art remain. The
read-only audit passes 18/18 doorway probes on the six building bodies, 18/18
structural wall/lintel probes, and 18/18 full-scene doorway probes. Its negative
regression fixture is also proven to catch a 0.404-meter facade obstruction.

The matching Chinese lattice door is a Blender DCC adaptation of two finished
CC0 sources: Kenney's Factory Kit `door-hinged.glb`, acquired on 2026-08-27,
supplies the retained 0.8-by-1.6-meter hinged leaf; material 2 of Free poly's
**Chinese Temple 2** `GuangchangClanHall` / `网格.002` supplies the retained,
DCC-reduced arched grille. Project-authored work is limited to the packed red
wood lacquer texture and material adaptation; no lattice, panel, hinge, stud,
boss, pull ring, or other door part is generated from primitives. Both source
contributions retain CC0 1.0 Universal provenance rather than being relicensed
as MIT. Editable source, studio preview, exact input/output SHA-256 values,
PBR/animation/pivot checks, and deterministic rebuild instructions are in
`../../source_art/props/jianghai_lattice_door/README.md`; the rebuild script is
`../../scripts/blender/build_jianghai_lattice_door.py`.

The editable 1,162,441-byte door `.blend` has SHA-256
`72D41DB8125BB5DDDEE04DE14E6AA5C9D8B1D4D5058823B74CC52968D78C9445`.
The 412,548-byte runtime GLB has SHA-256
`FBE9FC3EBB1F8BB49842442F1A4AEF451E0F67E5B3FF95BBB16A6F01B84D5528`
and contains three mesh nodes, two unique meshes/two surfaces, 5,745 unique and
11,334 instanced triangles, two Principled PBR materials, one packed 256-square
texture/image, and two 18-frame, 0.6-second clips that swing through 96 degrees.
The red-wood lacquer and dark-gold base colors are raised for readability under
the eaves without emission, additional lights, or runtime cost.
Godot's byte-identical extracted runtime albedo is
`jianghai_old_city/jianghai_lattice_door_JianghaiRedWoodAlbedo.png`, 63,926
bytes with SHA-256
`C75ED94A13A4F21CE518F455916802117D193FCE7A5731A0A4A602F82FD43834`.

No new external asset was acquired for this rebuild. Repeated placements share
mesh datablocks, reuse a small licensed material set, and export with a maximum
runtime-texture dimension of 512 pixels. The current authoritative `.blend` and
runtime GLB contain zero visible `Old Urban building` and zero visible `Scan Old
Building Street` instances. Their source URLs, creators, licenses, and hashes
remain documented as historical evidence only. Representative DCC review
renders are `previews/12_chinese_edge_gate.png`,
`previews/13_chinese_avenue.png`, and
`previews/14_chinese_old_city_overview.png` under the Jianghai source directory.
The final packed `.blend` is 49,398,104 bytes with SHA-256
`AD6EEED449F47564131F961394F572A5327EAAB018CA5670F49A8F34173C3B6A`.
It contains 505 objects, 206 unique mesh datablocks, 3,032,228 mesh-object
triangles, and resolves to 568 evaluated mesh objects / 3,061,154 evaluated
instance triangles. The final GLB is 58,571,556 bytes with SHA-256
`B0D21C78C3996BF2AA2D0F78FA32199B0B4BF396B164E547CAE9498033F23139`;
its Blender round-trip audit reports 585 total nodes, 568 mesh nodes, 269 unique
meshes, 449 primitives, 1,070,770 unique / 3,059,538 instanced triangles, 93
materials, 142 textures, 120 images, and a 512-pixel maximum image dimension.
The scene audit passes with 42 density placements, six enterable residences,
18/18 building-body doorway samples, 18/18 structural wall/lintel samples,
18/18 full-scene doorway samples, zero density intersections, and zero visible
retired-building instances.

The packed `.blend` is the authoritative DCC scene. The valley build is an
offline DCC authoring step that adapts the existing project-authored foundation
and serializes one modeled ground composite assembled from eight Coast Line 01
scan placements plus a 12-instance, staggered two-ring Hero Mountain
composition into that scene. The
Blender export script
does not generate runtime procedural geometry: it reapplies the documented
explicit, non-random building-transform table, cleared-asset and street-cadence
substitutions, sign cleanup, material tuning, and export policy, then serializes
the result in the packed source and runtime GLB.

The runtime export policy caps the longest texture dimension at 512 pixels
and recompresses eligible high-resolution runtime images as JPEG quality 90.
The upstream pre-valley 2026-08-28 integration audit recorded all seven
then-required runtime anchors, 487 mesh objects, 196 unique mesh datablocks,
4,471,243 raw mesh-object triangles,
and 821,213 triangles counted once per unique mesh. Dependency-graph evaluation
and the runtime export produced 550 mesh nodes and 4,500,345 instance triangles.
That upstream packed `.blend` is 61,677,884 bytes with SHA-256
`C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7`.
That upstream 73,809,716-byte GLB has SHA-256
`2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A`.
The matching upstream Godot refinery-map validation passed with 550 imported
authored meshes, 770 surfaces, all 770 surfaces material-backed, and the same 4,500,345
authored instance triangles. Route validation reports
`routes=True`, `route_probes=14`, and `route_blocker=none`; the Victory truck
envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and
`y=2.6`. High tier disables shadows only for fine decorative meshes; model
geometry, materials, and visibility ranges are unchanged. The upstream
2026-08-28 capture tuples (draw calls / objects / primitives) are Overview
582/808/4,286,647, Victory street 750/890/4,207,438, Street-life bicycle
close-up 421/466/3,322,262, Guangchang pawnshop 511/740/2,230,125, Red Star
factory 561/623/4,265,595, Market footbridge 739/1,030/4,707,642, north-ward
density 352/478/2,512,297, and daylight overview
1,014/1,271/7,113,753. All eight passed after the 20 entry-facade objects,
hinged doors, interior loot, and four authored residents were active; peak video
memory was 1,001.8 MB and peak texture memory was 852.7 MB.

The subsequent 2026-08-29 valley pre-rebase evidence is recorded separately
because neither historical binary hash represents the regenerated post-rebase
artifact.

The current 2026-08-29 Chinese-district delivery is the 49,398,104-byte packed
`.blend` and 58,571,556-byte runtime GLB identified above. After explicit Godot
reimport, runtime validation reports 568 authored meshes and 1,493
material-backed surfaces / 3,059,538 authored instance triangles. The runtime
represents 277 safe repeated sources with 71 spatial `MultiMesh` batches and
71/71 non-origin batch centroids. Gameplay physics represents 107 reviewed collision
sources with 315 gameplay box shapes plus 20 landmark boxes, 335 boxes total and
zero concave shapes; door, rail, ballistic, rooftop, 14 route, and 12/12
high-value-loot access probes pass. Six furnished rooms contain six animated
Chinese doors, 24 finished Kenney furniture props represented by 49 mesh nodes,
eight searchable loot placements, and four added residents for eight total.
Six bidirectional door links let squad AI open and traverse the required room
route. Furniture uses a 42-meter visibility range without shadows, and sky
radiance updates remain incremental.

All 11 representative captures pass the 2,400 draw-call, 2,200-object,
10.5-million-primitive, 1,536 MB video-memory, and 1,152 MB texture-memory
budgets. Overview records 1,001 draw calls / 1,005 objects / 2,774,257
primitives; the peak daylight view records 1,904 / 1,939 / 3,069,532. Peak
memory is 1,057.0 MB video and 663.3 MB texture. Detailed counts and historical
comparisons are kept in `../../source_art/world/jianghai_old_city/README.md`.

The following valley-era results are retained as historical evidence and do not
describe the current Chinese-district binaries. The 2026-08-29 final pre-rebase
valley DCC audit records a 96-source-vertex,
188-triangle project-authored foundation; one 84,960-vertex,
168,480-triangle Coast-Line-derived ground composite; and 12 instances of one
shared 14,000-triangle Hero Mountain mesh arranged as staggered six-object inner
and outer rings. The valley totals 336,668 instance triangles and the full
scene totals 4,835,033, below the 5,000,000-triangle gate. The ground bounds are
X `-600.878..600.853`, Y `-540.340..660.056`, and Z
`-12.7965..5.0390` meters, with 17.835 meters of relief. Signed-distance,
coverage, slope, topology, material, UV, north-road-endcap, mountain burial,
and DCC-to-GLB round-trip gates all pass. The final pre-rebase main GLB is
76,862,308 bytes with SHA-256
`0C0174672630957390A959BC3BD71DB3F4849CC7CABE0AFADFDD12273DFE02A5`;
the final pre-rebase packed `.blend` is 74,037,661 bytes with SHA-256
`C9BAC433CF77791B3730E309A5E0BEEF6CF4849593D44018FD2CDFE5AC8FAA08`.
The historical post-rebase packed `.blend` is 81,861,168 bytes with SHA-256
`7CA84CD2B17C3872323D8A5EE7B1A4BA5BCB360F4326FB2331327BED4F493461`.
Its DCC audit records 500 mesh objects, 198 unique mesh datablocks, 4,807,899
mesh-object triangles, 1,003,869 triangles counted once per unique mesh, and
563 evaluated objects totaling 4,836,825 instance triangles. The historical
84,723,312-byte GLB has SHA-256
`7E2BB712BCF031692FAFB0E4E0FA59F3E75CE340B2748F5EDBDB7B105D9B2965`.
All eight anchors and every builder, read-only audit, export, and serialized-GLB
round-trip gate pass.

For that historical artifact, an explicit Godot editor reimport and a second no-op import
validation reports 563 authored meshes, 784 surfaces, 784 material-backed
surfaces, 4,836,825 authored triangles, 8/8 anchors, 419 detail meshes, 406
shadow casters, quality tiers 130/226/406, the one-ground-plus-12-mountain
valley contract at 336,668 triangles, four of four residents, hinged doors at
96 degrees, collision 240/240, and route probes 14 with no blocker. Day and the
always-procedural Dusk atmosphere both pass with a continuous sky/ground horizon
and no panorama. The `refinery-map`, `refinery-collision`, `refinery-doors`,
`refinery-atmosphere`, `map-density`, `large-map`, `residential`, `stairs`,
`skylinks`, and `vehicle-drive` diagnostics all exit 0.

Its capture budgets passed at a peak 1,087.0 MB video memory of 1,536 MB and
900.9 MB texture memory of 1,152 MB. Independent visual review is DELIVERABLE:
no sky/terrain seam, radial pattern, skirt, z-fighting, trench, floating
platform, or material south-line blocker remains. Detailed DCC, GLB, runtime,
and per-view capture counting scopes are kept in
`../../source_art/world/jianghai_old_city/README.md`.

That historical runtime collision was generated from 107 structural meshes plus
133 explicitly selected detail meshes. Its 240 concave shapes, four-anchor
94/21/83/42 split, 104 shared collision meshes, 76 baked instances, 77 unique
shapes, and 3,560,137 collision-instance triangles are comparison records only;
the current lightweight 122-box collision contract is described above.

In the final DCC placement, the Municipal terminal root has no duplicate
180-degree rotation and its screen faces opposite the Grand terminal. The 22
Rollershutter Window 03 and Exterior Aircon Unit instances are rotated flush
against actual tenement facades; none occupies `CentralAvenue`.
Final DCC QA removes the redundant `JianghaiArtPass_FactoryHeroShutter`
instance because it became obsolete when the damaged factory shell was
replaced by the five finished CC0 buildings recorded below. Rollershutter
Window 03 remains used on the tenement facades and as the retained standalone
derivative recorded above, but no longer supplies either current interactive
door. The factory landmark entry is framed
by a five-object portal composed in the authoritative Blender scene from reused
DCC-authored brick piers, pier caps, and a corrugated roof. The Blender audit reports
`factory_gate_portal=5/5` and `factory_gate_portal_aligned=True`. This portal is
authored final visible art, not a code-built primitive or procedural runtime
model; reused packed materials retain their recorded source licenses. Behind
the portal is one of the two 10-object Quaternius personnel-door facades; the
other is at the pawnshop. The current Kenney personnel doors use normal
96-degree side-hinged motion rather than the former shutter motion.

The delivered urban-life expansion is authored in the authoritative `.blend`:
36 apartment-facade objects create two asymmetrical 3-by-3 tenement overlays;
three adapted Pink city bicycles line the sidewalks; a Coffee Cart 01 and
Wicker Basket 01 form the market tea cart; a Chinese Tea Table and three Chinese
Stools dress the pawnshop; and a Hand Truck dresses the factory. Five Chinese
red lamps and the licensed porcelain-lion dressing remain part of the Chinese
street vocabulary. The pawnshop hero entrance replaces six flat gate boards and
twelve zero-thickness wall pieces with 15 modeled pavilion parts, eight solid
facade modules, eight authored window/door inserts, and a ten-piece Quaternius
entry facade. The paired factory facade brings the entry total to 18
`Brick_Plain_1` and two `DoorFrame_Trim` instances around two
1.45-by-2.65-meter human-scale openings.

The 2026-08-29 building pass supersedes the former Old Urban/Scan Old
storefronts, rear houses, factory buildings, street-cadence object, and density
placements. All 66 affected visible anchors now use the Temple hall, Chinese
arcade-shop, or Chinese gate-house shared mesh. The density pass contains 42
complete buildings: eight Temple halls, sixteen arcade shops, four gate houses,
and fourteen retained Quaternius placements (four Building1 Large, three
Building3 Big, three Building4, and four House2). The `.blend` remains the
authoritative DCC source. The export script validates the explicit reviewed
transforms and material/mesh adaptations without randomness; it does not create
runtime procedural city geometry.

The project-authored portions include the complete map layout, street and
supporting geometry, district composition, material adaptations, art
direction, sign wording and placement, the objective terminals' small status
screens and adaptation work, the urban-life composition, the factory-gate
portal geometry/composition, and DCC integration work. The two terminal bodies
combine the CC0 Utility Box 01 and Television 02 sources recorded above. The
project-authored portions are covered by the repository's root MIT license,
subject to the disclosure in
`docs/CONTENT_PROVENANCE.md`; reused portal materials retain their recorded
source licenses.

The delivered composite incorporates adapted geometry and materials from these
CC0 sources acquired on 2026-08-27:

- **Modular Factory Facade**, by **James Ray Cock**, from Poly Haven:
  https://polyhaven.com/a/modular_factory_facade
- **Modular Urban Apartments Facade**, by **James Ray Cock**, from Poly Haven:
  https://polyhaven.com/a/modular_urban_apartments_facade
- **Chinese Temple 2**, by **Free poly**, from BlenderKit; `assetBaseId`
  `8701a79a-1635-437c-b1d2-6b14f14fc351`:
  https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/
- **Chinese red lamp**, by **Kin Chen**, from BlenderKit; `assetBaseId`
  `b97e433c-2eb1-46b8-9633-5bdee21e4e7a`:
  https://www.blenderkit.com/asset-gallery-detail/b97e433c-2eb1-46b8-9633-5bdee21e4e7a/
- **Chinese Four-corner Pavilion - Free**, by **VVayToyek**, from itch.io:
  https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free

Additional CC0 BlenderKit inputs acquired on 2026-08-28 are retained in this
license inventory. Chinese Porcelain Lion and Pink city bicycle remain delivered;
Old Urban building and Scan Old Building Street are historical-only sources with
zero current visible instances:

- **Old Urban building**, by **Abobla O.S**; `assetBaseId`
  `8177ff94-1645-4b50-95cc-cb05a336e34d`:
  https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/
- **Scan Old Building Street**, by **Free poly**; `assetBaseId`
  `d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030`:
  https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/
- **Chinese Porcelain Lion**, by **Free poly**; `assetBaseId`
  `50b661cb-119d-4e80-8a9c-5c6996cbb0c8`:
  https://www.blenderkit.com/asset-gallery-detail/50b661cb-119d-4e80-8a9c-5c6996cbb0c8/
- **Pink city bicycle**, by **Kin Chen**; `assetBaseId`
  `4c1a83c1-829f-4c00-878e-9e73c6b89c3b`:
  https://www.blenderkit.com/asset-gallery-detail/4c1a83c1-829f-4c00-878e-9e73c6b89c3b/

The official pages identify these BlenderKit assets as Creative
Commons Zero and free. For the four assets acquired on 2026-08-28, the
official API also reported `license=cc_zero` and `isFree=true`. Attribution is
not required by CC0, but creator credits and exact source identifiers are
retained as provenance. Retaining the Old Urban and Scan Old records does not
mean those sources remain visibly delivered. The final DCC scene also contains adapted instances of
the Poly Haven CC0 authored-pass assets listed above and the already tracked
Poly Haven CC0 Old Military Crate, Concrete Road Barrier, Coffee Cart 01, and
Wicker Basket 01. Modular Urban Apartments Facade is delivered as 36 adapted
facade objects in two asymmetrical 3-by-3 overlays. Chinese Tea Table and three
Chinese Stool instances are placed at the pawnshop, and Hand Truck is placed at
the factory. The VVayToyek pavilion contributes 15 adapted modeled parts to the
pawnshop canopy. Exact acquisition and bundle hashes are retained in
`../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`.

License text: https://creativecommons.org/publicdomain/zero/1.0/

During DCC authoring, the 2026-08-27 acquisition used the Simplified Chinese
variable subset of
**Noto Sans SC** from the Google Noto / `notofonts` project, licensed under the
SIL Open Font License 1.1. The source font is used only during the Blender
editing process to convert Chinese sign text to static glyph meshes. The
original `.otf` is not included in the authoritative `.blend`, runtime `.glb`,
or repository, and the export script rejects non-built-in font datablocks.
The sign wording and layout are project-authored. Source and license:

- https://github.com/notofonts/noto-cjk/blob/main/Sans/README.md
- https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE

The map also reuses the already tracked Poly Haven CC0 surface textures listed
in `../textures/LICENSE.md`: Asphalt 03, Concrete Floor, Gravel Embedded
Concrete, and Corrugated Iron.

The source models, their materials, and the Noto font remain governed by their
respective source rights. Inclusion in the packed `.blend` or exported `.glb`
does not relicense any third-party contribution under the repository's MIT
license, and the root MIT license is not asserted to replace those source
licenses.

## Kenney CC0 models

The City Kit (Industrial) 1.0 model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/city-kit-industrial
- Download: https://kenney.nl/media/pages/assets/city-kit-industrial/5fcb837741-1750838303/kenney_city-kit-industrial_1.0.zip
- Local assets: `kenney_city_kit_industrial/*.glb`, the edited
  `kenney_city_kit_industrial/enterable/*.glb` variants, and their extracted
  colormap textures
- License copy: `kenney_city_kit_industrial/KENNEY_LICENSE.txt`

The complete GLB set is retained so Tideforge, Harbor Locks, the freight terminal, and residential rooftop dressing can combine the pack's buildings, chimneys, and tank detail. On 2026-08-27, Blender 4.5 was used to cut 35 real door apertures across 13 building variants for the freight terminal. The reproducible edit and aperture checks are in `tools/build_enterable_industrial_buildings.py`; `enterable_layouts.json` is shared by Blender and the Godot runtime. The edited buildings retain Kenney's CC0 license.

The Factory Kit 3.0 model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/factory-kit
- Download: https://kenney.nl/media/pages/assets/factory-kit/edaac9d4f6-1777639602/kenney_factory-kit_3.0.zip
- Acquisition date: 2026-08-19
- Local assets: selected `kenney_factory_kit/*.glb` models and the original `kenney_factory_kit/Textures/colormap.png` material atlas
- License copy: `kenney_factory_kit/KENNEY_LICENSE.txt`

The authored overhead door is used for interactive industrial entrances. The Factory Kit personnel door was additionally acquired from the same official archive on 2026-08-27 as `door.glb`; Blender removes the archive's sample-scene objects and exports the side-pivoted runtime derivative `door-hinged.glb` plus its extracted colormap. Jianghai Old City currently instances that finished CC0 personnel door at both the pawnshop and factory entrances, each configured for a 1.45-by-2.65-meter opening and a normal 96-degree side swing. Tideglass Reactor additionally uses the selected `machine.glb`, `hopper-high-round.glb`, and `machine-window.glb` models as three distinct collision-backed midfield covers. No attribution is required for these CC0 assets, and Kenney is credited as a provenance courtesy.

The Furniture Kit 2.0 model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/furniture-kit
- Acquisition date: 2026-08-26
- Local assets: selected `kenney_furniture_kit/*.glb` interior props used by searchable furniture and apartment room dressing
- License copy: `kenney_furniture_kit/KENNEY_LICENSE.txt`

Selected cabinets, desks, beds, tables, fridges, and crates replace programmer-art boxes inside enterable residential rooms.

The City Kit Roads model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/city-kit-roads
- Download: https://kenney.nl/media/pages/assets/city-kit-roads/74288c9459-1787042796/kenney_city-kit-roads.zip
- Acquisition date: 2026-08-27
- Local assets: twenty selected `kenney_city_kit_roads/*.glb` road and street-furniture models plus the original `kenney_city_kit_roads/Textures/colormap.png` material atlas
- License and package evidence: `kenney_city_kit_roads/KENNEY_LICENSE.txt` and `kenney_city_kit_roads/PACK_PREVIEW.png`
- Local mapping: `kenney_city_kit_roads/README.md`

The selected authored barriers, lights, utility poles, signs, and traffic lights dress the Tideglass Reactor streets.

The third-party raw asset files listed in this document retain their stated source licenses or public-domain dedications. They are not relicensed under the repository's root MIT license.

## Majadroid CC0 construction-site models

The 3D House Construction Site - LowPoly CC0 art package is published by Majadroid / Maik Hoffmann under CC0 1.0:

- Source: https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0
- Download: https://opengameart.org/sites/default/files/lowpoly-house-construction-site-by-majadroid_2.zip
- Acquisition date: 2026-08-27
- Local source selection: `source_art/third_party/majadroid_construction_site/`
- Godot-ready assets: nine converted GLBs and their palette textures in `majadroid_construction_site/`
- License and creator evidence: `source_art/third_party/majadroid_construction_site/INFO.txt` and `Overview.png`
- Reproducible conversion: `scripts/blender/build_tideglass_map_assets.py`

The source package credits Imphenzia for its color palette. The runtime conversion selects one office-container stack, one cargo-container stack, one concrete truck, and three distinct material props instead of bundling overlapping source variants. Attribution is not required under the package's CC0 dedication, but both creator credits are retained for provenance.

## Trey Ramm CC0 modular industrial models

The Modular Industrial Kit is published by Trey Ramm, OpenGameArt user `minime453`, under CC0 1.0:

- Source: https://opengameart.org/content/modular-industrial-kit
- Download: https://opengameart.org/sites/default/files/modular_industrial_pieces.zip
- Acquisition date: 2026-08-27
- Local source selection: `source_art/third_party/trey_modular_industrial/`
- Godot-ready compositions: `trey_modular_industrial/*.glb`
- License and creator evidence: `source_art/third_party/trey_modular_industrial/SOURCE_PAGE.html` and `ORIGINAL_README.txt`
- Source atlas and preview: `source_art/third_party/trey_modular_industrial/PacificNorthwestGradientAtlas.png` and `ASSET_OVERVIEW.png`
- Reproducible conversion: `scripts/blender/build_trey_modular_industrial.py`

The runtime scenes combine selected authored modules from the source kit, including two distinct closed perimeter-gate compositions and four closed one-storey industrial buildings for Tideglass Reactor. Attribution is not required under CC0, but Trey Ramm's requested courtesy credit is retained.

### Special Operations command hall composition

The authored Special Operations home-screen set combines selected Trey Ramm
Modular Industrial Kit modules with selected Kenney Furniture Kit 2.0 props.
Every visible mesh in the composite comes from those finished CC0 sources; the
Blender composition adds transforms, material tuning, named runtime anchors,
and an embedded export without replacing the source art with generated
primitive geometry.

- Runtime output: `operations_office/operations_office_set.glb`
- Local source mapping and verification record:
  `operations_office/README.md`
- Authoritative editable source:
  `../../source_art/operations_office/operations_office_set.blend`
- Reproducible Blender build:
  `../../scripts/blender/build_operations_office_set.py`
- Trey creator and source: Trey Ramm / OpenGameArt user `minime453`,
  https://opengameart.org/content/modular-industrial-kit
- Trey acquisition date and evidence: 2026-08-27;
  `../../source_art/third_party/trey_modular_industrial/SOURCE_PAGE.html` and
  `ORIGINAL_README.txt`
- Kenney creator and source: Kenney,
  https://kenney.nl/assets/furniture-kit
- Kenney acquisition date and evidence: 2026-08-26;
  `kenney_furniture_kit/KENNEY_LICENSE.txt`
- Exact license for both source collections: CC0 1.0 Universal,
  https://creativecommons.org/publicdomain/zero/1.0/
- Required attribution: none; both creator credits are retained as provenance
  courtesy.

The underlying source geometry and materials retain their CC0 dedications.
The project-authored scene composition and rebuild script are covered by the
repository's root MIT license, subject to the AI-assistance disclosure in
`docs/CONTENT_PROVENANCE.md`.

## Quaternius CC0 Buildings Pack models

The Buildings Pack is published by Quaternius under CC0 1.0 Universal:

- Creator: Quaternius (`@Quaternius`)
- Official source: https://quaternius.com/packs/buildings.html
- Exact license: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-28
- Local source selection and official evidence: `source_art/third_party/quaternius_buildings_pack/`
- Godot-ready assets: `quaternius_buildings_pack/building1-large.glb`, `building3-big.glb`, `building4.glb`, and `house2.glb`
- Local mapping and verification record: `quaternius_buildings_pack/README.md`
- Reproducible conversion: `scripts/blender/build_quaternius_buildings_pack.py`

The conversion preserves Quaternius's authored geometry, scale, material colors, and PBR values, while correcting the FBX importer's zero-alpha solid materials to fully opaque. It centers and grounds each scene, embeds the creator, official source URL, exact license, original filename, and acquisition date, then verifies those properties through a Blender glTF round trip. Jianghai Old City further adapts these four selections into fourteen perimeter-density buildings (four Building1 Large, three Building3 Big, three Building4, and four House2) and three full street-cadence replacements in its packed DCC source and runtime GLB. Clean Building4 and Building3 Big bodies also underpin the current Chinese arcade-shop and gate-house shared meshes, combined in Blender with the separately licensed pavilion details and Temple roof recorded above. Attribution is not required under CC0, but the creator credit is retained for provenance.

## Quaternius CC0 models

The Ultimate Modular Women character pack is distributed by Quaternius under
CC0 1.0 Universal:

- Creator: Quaternius (`@Quaternius`)
- Source: https://quaternius.com/packs/ultimatemodularwomen.html
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-27
- Runtime assets: `quaternius_operators/viper.glb`, `heron.glb`,
  `lynx.glb`, `magpie.glb`, and `jackal.glb`
- License, per-role source mapping, and rebuild record:
  `quaternius_operators/LICENSE.md`
- Editable source: `source_art/third_party/quaternius_modular_women/`

Five different authored presets supply distinct heads, clothing, equipment
silhouettes, and materials for the complete extraction roster. The runtime
files remain derived from those same five Quaternius presets; they are refined
in Blender through selective subdivision, shape-preserving creases, smooth
shading, role-aware scalar PBR material parameters, and four-influence
normalized skinning before re-export. This adaptation adds no image textures
or new UV artwork and preserves the shared node, equipment-socket, and
25-action animation contract. The actions are retargeted from the CC0
Quaternius Universal Animation Library. Attribution is not required; creator
credit is retained as a courtesy. Jianghai Old City also reuses the unarmed
MAGPIE, HERON, JACKAL, and VIPER variants as its four animated indoor
residents; no additional third-party character files are introduced.

The Ultimate Guns Pack is distributed by Quaternius under CC0 1.0 Universal:

- Source: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-20
- Local assets: `quaternius_ultimate_guns/*.glb`
- License and source-file mapping: `quaternius_ultimate_guns/LICENSE.md`

Nine selected authored models remain active for SCAR-L, M24, AXMC, AWM, VSS,
MP5A5, M3A1, P226, and M1911. The tracked Quaternius AK-74N is retained as a
licensed legacy source, while the active AK-47 replacement is recorded in the
Steel Tide combat-model section above.

## DJMaesen CC BY 4.0 first-person arms and SMG

The realistic first-person arms and SMG-45 visuals are adapted from **fps
animated smg** by **DJMaesen**, licensed under **CC BY 4.0**. Full attribution,
the original source mapping, and the reproducible Blender build are recorded in
`djmaesen_smg45/LICENSE.md`.

The standard free version of the Downtown City MegaKit is distributed by Quaternius under CC0 1.0 Universal:

- Creator: Quaternius (`@Quaternius`)
- Source: https://quaternius.com/packs/downtowncitymegakit.html
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-19
- Local assets: `quaternius_downtown_city/*.gltf`, matching `.bin` buffers, and shared 1K texture maps
- License copy and processing record: `quaternius_downtown_city/QUATERNIUS_LICENSE.txt` and `quaternius_downtown_city/README.md`

The repository contains 21 selected modular scenes and 26 shared textures for composing Saint Marais Old Town. The metal wall modules are also reused as visual facades on the Tideforge and Harbor Locks collision shells. Jianghai Old City reuses `Brick_Plain_1.gltf` 18 times and `DoorFrame_Trim.gltf` twice in its packed DCC source/runtime GLB, divided into two 10-object personnel-entry facades (nine brick modules plus one doorframe each). The user-provided CS:GO Town Sketchfab page was treated as layout reference only; no geometry, textures, or other files from that page are included.

## TastyTony CC BY 4.0 model

The GSh-18 sidearm model is **Low-Poly GSh-18** by TastyTony and is used under Creative Commons Attribution 4.0 International:

- Source: https://sketchfab.com/3d-models/low-poly-gsh-18-7ce65f794f0e42f98f61a96026e4d75e
- License: https://creativecommons.org/licenses/by/4.0/
- Local asset and attribution: `tastytony_gsh18/low-poly_gsh-18.glb` and `tastytony_gsh18/LICENSE.md`

The model remains credited to TastyTony and is not covered by the repository's MIT license.

## ELIZION CC BY 4.0 model

The Desert Eagle sidearm model is **Desert Eagle** by ELIZION and is used under Creative Commons Attribution 4.0 International:

- Source: https://sketchfab.com/3d-models/desert-eagle-cabde59f5cf24effaf80536e35d04e95
- License: https://creativecommons.org/licenses/by/4.0/
- Local asset and attribution: `elizion_desert_eagle/desert_eagle.glb`, its extracted `desert_eagle_*.png` PBR maps, and `elizion_desert_eagle/LICENSE.md`

The model remains credited to ELIZION and is not covered by the repository's MIT license.

## BAMEN CC BY 4.0 character

The deployment-preview character is **FREE [Military Soldier] RIGGED** by BAMEN and is used under Creative Commons Attribution 4.0 International:

- Source: https://sketchfab.com/3d-models/free-military-soldier-rigged-e9c56308a67d4a3db62e914fafa4d198
- License: https://creativecommons.org/licenses/by/4.0/
- Local asset and attribution: `bamen_military_soldier/bamen_military_soldier.glb` and `bamen_military_soldier/LICENSE.md`
- Editable and original source: `source_art/third_party/bamen_military_soldier/`

The model remains credited to BAMEN and is not covered by the repository's MIT license.

## Tide Hunter CC0 monster

The roaming Boss uses **3D Horror Game Monster** by HorrorGameMaker.com:

- Source: https://opengameart.org/content/3d-horror-game-monster
- License: CC0 / Public Domain, as marked on the source page
- Acquisition date: 2026-08-20
- Local asset: `tide_hunter_monster/tide_hunter_monster.glb` with its three tracked `tide_hunter_monster_test_StingrayPBS1SG_*.png` PBR maps
- Editable packed source and reproducible cleanup script: `source_art/third_party/tide_hunter_monster/tide_hunter_monster.blend` and `scripts/blender/build_tide_hunter_monster.py`

The Boss is a single skinned mesh with embedded PBR maps and `idle`, `walk`, and `run` actions. Credit is retained as courtesy.

## Quaternius CC0 animation libraries

The field-operator animation set combines the standard free exports from
Quaternius Universal Animation Library and Universal Animation Library 2:

- Creator: Quaternius (`@Quaternius`)
- Sources: https://quaternius.itch.io/universal-animation-library and https://quaternius.itch.io/universal-animation-library-2
- Exact license: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-20
- Local source exports: `source_art/third_party/quaternius_universal_animation_library/UAL1_Standard.glb` and `UAL2_Standard.glb`
- License evidence: `source_art/third_party/quaternius_universal_animation_library/LICENSE.txt` and `UAL2_LICENSE.txt`
- Blender output: `source_art/third_party/bamen_military_soldier/bamen_military_soldier_animated.blend` and `assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb`

The checked-in standard exports are the root-motion-disabled versions. The
retargeting script keeps navigation in Godot authoritative and uses Blender
to add prone/downed integration, recovery poses, and attachment sockets.
