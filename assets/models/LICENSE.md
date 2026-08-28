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
either current Old City `InteractiveBuildingDoor` visual. The current doors use
Kenney's CC0 `kenney_factory_kit/door-hinged.glb` at two
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

## Jianghai Old City authored composite

The Jianghai Old City extraction-map visual is a project-authored Blender
composition that adapts redistributable third-party source assets into one
static runtime scene:

- Runtime output: `jianghai_old_city/jianghai_old_city.glb`
- Current interactive-door visual: `kenney_factory_kit/door-hinged.glb`
- Retained alternate/legacy shutter derivative: `jianghai_old_city/rollershutter_window_03.glb`
- Runtime dusk panorama: `../textures/kloppenheim_06_puresky_1k.hdr`; Poly
  Haven CC0 evidence is in `../textures/LICENSE.md`, and the file is loaded by
  `JianghaiOldCityAtmosphere` rather than embedded in the map GLB.
- Authoritative editable DCC source: `../../source_art/world/jianghai_old_city/jianghai_old_city.blend`
- Runtime export script: `../../scripts/blender/export_jianghai_old_city.py`
- Source and license evidence: `../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`
- Initial external inputs were acquired on 2026-08-27; the additional
  BlenderKit and Poly Haven assets identified below were acquired on
  2026-08-28.

The packed `.blend` is the authoritative DCC scene. The Blender export script
does not generate runtime procedural geometry: it reapplies the documented
explicit, non-random building-transform table, cleared-asset and street-cadence
substitutions, sign cleanup, material tuning, and export policy, then serializes
the result in the packed source and runtime GLB.

The runtime export policy caps the longest texture dimension at 1024 pixels
and recompresses eligible high-resolution runtime images as JPEG quality 90.
A 2026-08-28 Blender audit recorded all seven required runtime anchors, 487
mesh objects, 196 unique mesh datablocks, 4,471,243 raw mesh-object triangles,
and 821,213 triangles counted once per unique mesh. Dependency-graph evaluation
and the runtime export produce 550 mesh nodes and 4,500,345 instance triangles.
The final packed `.blend` is 61,677,884 bytes with SHA-256
`C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7`.
The final 73,809,716-byte GLB has SHA-256
`2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A`.
The matching Godot refinery-map validation passes with 550 imported authored
meshes, 770 surfaces, all 770 surfaces material-backed, and the same 4,500,345
authored instance triangles. Route validation reports
`routes=True`, `route_probes=14`, and `route_blocker=none`; the Victory truck
envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and
`y=2.6`. High tier disables shadows only for fine decorative meshes; model
geometry, materials, and visibility ranges are unchanged. The current
2026-08-28 capture tuples (draw calls / objects / primitives) are Overview
582/808/4,286,647, Victory street 750/890/4,207,438, Street-life bicycle
close-up 421/466/3,322,262, Guangchang pawnshop 511/740/2,230,125, Red Star
factory 561/623/4,265,595, Market footbridge 739/1,030/4,707,642, north-ward
density 352/478/2,512,297, and daylight overview
1,014/1,271/7,113,753. All eight passed after the 20 entry-facade objects,
hinged doors, interior loot, and four authored residents were active; peak video
memory was 1,001.8 MB and peak texture memory was 852.7 MB. Detailed DCC, GLB,
and runtime counting scopes are kept in
`../../source_art/world/jianghai_old_city/README.md`.

Runtime collision is generated from the actual exported geometry: 107
structural meshes plus 133 explicitly selected factory-gate, hinged-entry,
pawnshop-canopy/wing/low-wall, and market deck/ramp/rail detail meshes. The 240
concave shapes replace all former broad model-placement and landmark proxy
boxes. Deterministic probes verify visible surfaces block movement and bullets,
opened door/market air remains clear, rail gaps remain penetrable, and all 12
Epic/Legendary high-value placements have player-capsule access. The four anchor
shape counts are 94/21/83/42. Runtime instrumentation records 104 shared
collision meshes, 76 baked instances, 77 unique shapes, and 3,560,137
collision-instance triangles. Closed-door enemy capsule probes block and opened
door routes clear. The two interiors contain four residents in total alongside
their existing loot placements.

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

The delivered urban-life expansion is authored in the authoritative
`.blend`: 36 apartment-facade objects create two asymmetrical 3-by-3 tenement
overlays; three adapted Pink city bicycles line the sidewalks; a Coffee Cart
01 and Wicker Basket 01 form the market tea cart; a Chinese Tea Table and three
Chinese Stools dress the pawnshop; and a Hand Truck dresses the factory. The
cleared storefront composition adds a finished CC0 pawnshop backdrop, five
market shops (three adapted Old Urban building instances and two Scan Old
Building Street instances), two Old Urban building rear houses, and five
Chinese red lamps. The pawnshop hero entrance replaces six flat gate boards and
twelve zero-thickness wall pieces with 15 modeled pavilion parts, eight solid
facade modules, eight authored window/door inserts, and a ten-piece Quaternius
entry facade. The paired factory facade brings the entry total to 18
`Brick_Plain_1` and two `DoorFrame_Trim` instances around two
1.45-by-2.65-meter human-scale openings. The former damaged
factory shell is replaced with three Old
Urban building office/admin instances and two Scan Old Building Street workshops.
The density pass adds 36 complete perimeter buildings from six CC0 profiles:
eight Old Urban, fourteen Scan Old, four Quaternius Building1 Large, three
Building3 Big, three Building4, and four House2 instances. Four repeated
near-street buildings are replaced by full Scan Old or Quaternius meshes. The
`.blend` remains the authoritative DCC source. The export script reapplies the
explicit reviewed transforms and material/mesh adaptations without randomness;
it does not create runtime procedural city geometry.

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

Additional CC0 BlenderKit inputs acquired on 2026-08-28 and incorporated into
the delivered composite are:

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

The official pages identify the delivered BlenderKit assets as Creative
Commons Zero and free. For the four assets acquired on 2026-08-28, the
official API also reported `license=cc_zero` and `isFree=true`. Attribution is
not required by CC0, but creator credits and exact source identifiers are
retained as provenance. The final DCC scene also contains adapted instances of
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

The conversion preserves Quaternius's authored geometry, scale, material colors, and PBR values, while correcting the FBX importer's zero-alpha solid materials to fully opaque. It centers and grounds each scene, embeds the creator, official source URL, exact license, original filename, and acquisition date, then verifies those properties through a Blender glTF round trip. Jianghai Old City further adapts these four selections into fourteen perimeter-density buildings (four Building1 Large, three Building3 Big, three Building4, and four House2) and three full street-cadence replacements in its packed DCC source and runtime GLB. Attribution is not required under CC0, but the creator credit is retained for provenance.

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
