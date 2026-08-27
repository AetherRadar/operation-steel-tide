# Model Asset Licenses

## Project-authored combat models

The Steel Tide M4A1 and operator models are generated from `scripts/blender/generate_combat_models.py`. Their editable `.blend` sources are tracked under `source_art/combat_models/`. They contain no copied third-party geometry or textures and are covered by the repository's root MIT license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

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

The following finished Poly Haven assets were acquired as 1K glTF bundles on
2026-08-28 for the Jianghai Old City authored-asset pass:

| Model | Creator | Official source | Repository-external acquisition input |
| --- | --- | --- | --- |
| Television 02 | Benny Weimer | https://polyhaven.com/a/television_02 | `poly_haven/television_02/television_02_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Exterior Aircon Unit | Monsta3D | https://polyhaven.com/a/exterior_aircon_unit | `poly_haven/exterior_aircon_unit/exterior_aircon_unit_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Rollershutter Window 03 | MP | https://polyhaven.com/a/rollershutter_window_03 | `poly_haven/rollershutter_window_03/rollershutter_window_03_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Trashbag | Benny Weimer | https://polyhaven.com/a/trashbag | `poly_haven/trashbag/trashbag_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Utility Box 01 | James Ray Cock | https://polyhaven.com/a/utility_box_01 | `poly_haven/utility_box_01/utility_box_01_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Barrel 03 | Serhii Khromov | https://polyhaven.com/a/barrel_03 | `poly_haven/barrel_03/barrel_03_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Plastic Crate 02 | Fabi_G | https://polyhaven.com/a/plastic_crate_02 | `poly_haven/plastic_crate_02/plastic_crate_02_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |
| Security Camera 01 | Alexander Otterbeck (modeling and texturing); Yann Kervran (rigging) | https://polyhaven.com/a/security_camera_01 | `poly_haven/security_camera_01/security_camera_01_1k.gltf`, its `.bin`, and `textures/*_1k.jpg` |

Each bundle is CC0 1.0 Universal under
https://creativecommons.org/publicdomain/zero/1.0/. Attribution is not
required; the contributor names are retained as provenance and courtesy
credit. The acquisition bundles remain in the external cache and are not
committed as separate raw files. Adapted geometry and materials are packed
into `../../source_art/world/jianghai_old_city/jianghai_old_city.blend` and
exported to `jianghai_old_city/jianghai_old_city.glb`; exact cache hashes and
the packed-output mapping are recorded in the scene's `LICENSE_EVIDENCE.md`.

Rollershutter Window 03 also has a repository-local derived runtime mapping:
`jianghai_old_city/rollershutter_window_03.glb` (1,587,684 bytes; SHA-256
`48E78DFC37FF6310151B18BEA8AC8B080BE31ABED4BD882C0FA3F46E19B0B4B1`).
The export script reproducibly selects the adapted
`JianghaiArtPass_EastShutter00` mesh from the authoritative packed `.blend`,
normalizes a temporary copy, and exports its PBR geometry and materials. This
standalone GLB supplies only the visible art for the two Old City
`InteractiveBuildingDoor` instances, replacing the enlarged Kenney
`door-wide-closed` visual. Their collision, animation, network state, and AI
traversal remain project gameplay behavior. The derived file retains MP's
Poly Haven CC0 provenance and is not relicensed as project-authored MIT art.

## Jianghai Old City authored composite

The Jianghai Old City extraction-map visual is a project-authored Blender
composition that adapts redistributable third-party source assets into one
static runtime scene:

- Runtime output: `jianghai_old_city/jianghai_old_city.glb`
- Interactive-door visual output: `jianghai_old_city/rollershutter_window_03.glb`
- Authoritative editable DCC source: `../../source_art/world/jianghai_old_city/jianghai_old_city.blend`
- Runtime export script: `../../scripts/blender/export_jianghai_old_city.py`
- Source and license evidence: `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`
- Initial external inputs were acquired on 2026-08-27; the additional
  BlenderKit and Poly Haven assets identified below were acquired on
  2026-08-28.

The packed `.blend` is the authoritative hand-edited scene. The export script
validates and exports that scene; it does not generate or reconstruct the
map. No script reconstructs the complete map; further composition and modeling
work starts from the saved hand-edited DCC source.

The runtime export policy caps the longest texture dimension at 1024 pixels
and recompresses eligible high-resolution runtime images as JPEG quality 90.
A 2026-08-28 Blender audit recorded all seven required runtime anchors, 429
mesh objects, 176 unique mesh datablocks, 4,664,722 object-instance triangles,
and 863,620 unique DCC-mesh triangles. The final packed `.blend` is 82,347,471
bytes with SHA-256
`3881F3653188A00328C85829FE06C7C61AD07510495791DD8537A38EB7816EF6`.
The final 95,837,888-byte GLB has SHA-256
`F61D82D77311BF1C2F8A3ACE1C0FFE967EC415220DABA9BF840237EC797CD0FA`
and contains 525 nodes, 264 unique mesh resources, 515 mesh nodes, 275
serialized glTF primitives, 50 materials, 100 images, 898,994 triangles counted
once per unique mesh resource, and 4,700,072 triangles across all mesh-node
instances. The matching Godot refinery-map validation passes with 515 authored
meshes, 575 material-backed surfaces, the same 4,700,072 authored instance
triangles, and 413 detail meshes. The full runtime scene contains 934 nodes and
625 mesh instances; that broader count
includes meshes outside the authored-map import. Route validation reports
`routes=True`, `route_probes=14`, and `route_blocker=none`; the Victory truck
envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and
`y=2.6`. The final shadow tiers are 102/207/373, with quality tier 1 still at
207. High tier disables shadows only for fine decorative meshes; model
geometry, materials, and visibility ranges are unchanged. Final capture tuples
(draw calls / objects / primitives) are Overview 627/784/8,249,404, Victory
street 832/1,086/9,596,938, Guangchang pawnshop 253/534/2,980,673, Red Star
factory 443/632/4,743,175, and Market footbridge 503/747/4,684,143. All pass;
video memory is 1,061.0 MB and texture memory is 919.5 MB. Detailed DCC, GLB,
and runtime counting scopes are kept in
`../../source_art/world/jianghai_old_city/README.md`.

In the final DCC placement, the Municipal terminal root has no duplicate
180-degree rotation and its screen faces opposite the Grand terminal. The 22
Rollershutter Window 03 and Exterior Aircon Unit instances are rotated flush
against actual tenement facades; none occupies `CentralAvenue`.
Final DCC QA removes the redundant `JianghaiArtPass_FactoryHeroShutter`
instance because the red-brick factory facade already contains an embedded
industrial roller shutter. Keeping the existing facade shutter avoids a
second overlapping dark door while the Rollershutter Window 03 source remains
used on the tenement facades recorded above.
The factory interactive shutter is framed by a five-object portal composed in
the authoritative Blender scene from reused DCC-authored brick piers, pier
caps, and a corrugated roof. The Blender audit reports
`factory_gate_portal=5/5` and `factory_gate_portal_aligned=True`. This portal is
authored final visible art, not a code-built primitive or procedural runtime
model; reused packed materials retain their recorded source licenses.

The project-authored portions include the complete map layout, street and
supporting geometry, district composition, material adaptations, art
direction, sign wording and placement, the objective terminals' small status
screens and adaptation work, the factory-gate portal geometry/composition, and
DCC integration work. The two terminal bodies combine the CC0 Utility Box 01
and Television 02 sources recorded above. The project-authored portions are
covered by the repository's root MIT license, subject to the disclosure in
`docs/CONTENT_PROVENANCE.md`; reused portal materials retain their recorded
source licenses.

The delivered composite incorporates adapted geometry and materials from these
CC0 sources acquired on 2026-08-27:

- **Modular Factory Facade**, by **James Ray Cock**, from Poly Haven:
  https://polyhaven.com/a/modular_factory_facade
- **Chinese Temple 2**, by **Free poly**, from BlenderKit; `assetBaseId`
  `8701a79a-1635-437c-b1d2-6b14f14fc351`:
  https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/
- **Chinese red lamp**, by **Kin Chen**, from BlenderKit; `assetBaseId`
  `b97e433c-2eb1-46b8-9633-5bdee21e4e7a`:
  https://www.blenderkit.com/asset-gallery-detail/b97e433c-2eb1-46b8-9633-5bdee21e4e7a/

Additional CC0 BlenderKit inputs acquired on 2026-08-28 and incorporated into
the delivered composite are:

- **Old Urban building**, by **Abobla O.S**; `assetBaseId`
  `8177ff94-1645-4b50-95cc-cb05a336e34d`:
  https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/
- **Scan Old brick building red small**, by **Free poly**; `assetBaseId`
  `fc8376f8-7c79-48b3-8a3c-bf061ace53e0`:
  https://www.blenderkit.com/asset-gallery-detail/fc8376f8-7c79-48b3-8a3c-bf061ace53e0/
- **Scan Old Building Street**, by **Free poly**; `assetBaseId`
  `d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030`:
  https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/
- **Chinese Wood House Wall**, by **Free poly**; `assetBaseId`
  `7c4def52-e40b-4b77-bd89-44985e00375b`:
  https://www.blenderkit.com/asset-gallery-detail/7c4def52-e40b-4b77-bd89-44985e00375b/
- **Chinese Porcelain Lion**, by **Free poly**; `assetBaseId`
  `50b661cb-119d-4e80-8a9c-5c6996cbb0c8`:
  https://www.blenderkit.com/asset-gallery-detail/50b661cb-119d-4e80-8a9c-5c6996cbb0c8/

The official pages identify the delivered BlenderKit assets as Creative
Commons Zero and free. For the five assets acquired on 2026-08-28, the
official API also reported `license=cc_zero` and `isFree=true`. Attribution is
not required by CC0, but creator credits and exact source identifiers are
retained as provenance. The final DCC scene also contains adapted instances of
the eight Poly Haven CC0 authored-pass assets acquired on 2026-08-28 and the
already tracked Poly Haven CC0 Old Military Crate and Concrete Road Barrier
listed above.

**Modular Urban Apartments Facade**, by **James Ray Cock**, was acquired and
evaluated on 2026-08-27 from
https://polyhaven.com/a/modular_urban_apartments_facade. It is CC0, but it is
not imported or embedded in the current delivered `jianghai_old_city.blend`
or `jianghai_old_city.glb`. The GLB may retain an
`evaluated_not_used` provenance field naming the evaluation, but contains no
apartment geometry, materials, or textures. Acquisition hashes are retained
solely to preserve the evaluation trail.

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

The authored overhead door is used for interactive industrial and Old Town entrances. The Factory Kit personnel door was additionally acquired from the same official archive on 2026-08-27 as `door.glb`; Blender removes the archive's sample-scene objects and exports the side-pivoted runtime derivative `door-hinged.glb` plus its extracted colormap. Tideglass Reactor additionally uses the selected `machine.glb`, `hopper-high-round.glb`, and `machine-window.glb` models as three distinct collision-backed midfield covers. No attribution is required for these CC0 assets, and Kenney is credited as a provenance courtesy.

The Furniture Kit 1.0 model set is distributed by Kenney under CC0 1.0:

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

The conversion preserves Quaternius's authored geometry, scale, material colors, and PBR values, while correcting the FBX importer's zero-alpha solid materials to fully opaque. It centers and grounds each scene, embeds the creator, official source URL, exact license, original filename, and acquisition date, then verifies those properties through a Blender glTF round trip. Attribution is not required under CC0, but the creator credit is retained for provenance.

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
credit is retained as a courtesy.

The Ultimate Guns Pack is distributed by Quaternius under CC0 1.0 Universal:

- Source: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-20
- Local assets: `quaternius_ultimate_guns/*.glb`
- License and source-file mapping: `quaternius_ultimate_guns/LICENSE.md`

Ten selected authored models replace the runtime primitive visuals for AK-74N,
SCAR-L, M24, AXMC, AWM, VSS, MP5A5, M3A1, P226, and M1911.

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

The repository contains 21 selected modular scenes and 26 shared textures for composing Saint Marais Old Town. The metal wall modules are also reused as visual facades on the Tideforge and Harbor Locks collision shells. The user-provided CS:GO Town Sketchfab page was treated as layout reference only; no geometry, textures, or other files from that page are included.

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
