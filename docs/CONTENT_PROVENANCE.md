# Content Provenance

This document records the known origin and licensing status of content shipped in or displayed by Operation Steel Tide. It is an audit aid, not a representation that the project satisfies every community's AI-content policy.

## Development disclosure

Operation Steel Tide is an AI-assisted solo prototype. AI tools contributed to portions of implementation, documentation, and project-authored presentation work. The repository owner reviewed, integrated, debugged, and validated the resulting project, but cannot verify that every model involved was trained exclusively on material submitted with each original creator's consent.

Consequently, this repository must not be described as satisfying policies that require that specific training-data proof. In particular, this document does not establish compliance with r/godot Rule #10.

## Content inventory

| Content | Origin | Rights or license | Evidence |
| --- | --- | --- | --- |
| C#, Go, scene, configuration, and documentation files | Project-authored, with disclosed AI assistance | Released by the repository owner under the root MIT license, subject to the limitation above | Git history and root `LICENSE` |
| `docs/media/hero.webp`, `squad.webp`, `city.webp`, and `social-preview.png` | Direct captures of the running Godot project | Project screenshots; depicted third-party assets retain their source licenses | Deterministic `--capture-promotion` command documented in `README.md` and asset records below |
| `docs/media/gameplay-combat-zh.webp`, `gameplay-squad-zh.webp`, `gameplay-tactical-zh.webp`, `gameplay-loot-zh.webp`, and `gameplay-demolition-zh.webp` | Direct captures of the running Godot project in its Chinese interface mode | Project screenshots; depicted third-party assets retain their source licenses | Deterministic `--capture-readme-zh` command documented in `README.md`; capture implementation in `csharp/FreightTerminalWorld.ReadmeCapture.cs` |
| `docs/media/cover.png` and `squad-key-art.png` project key art | Generated for this project with OpenAI image generation on 2026-08-28. `cover.png` uses the direct `hero.webp` capture as a composition and location reference; `squad-key-art.png` uses `squad.webp`, the cover, and the DCC avenue preview as references. Both prompts requested three believable tactical operators in Jianghai Old City with no text, logos, HUD, or watermarks | Included by the repository owner under the root MIT license, subject to the AI-assistance limitation above; depicted source-asset identities remain covered by their records below | Source references `docs/media/hero.webp`, `docs/media/squad.webp`, and `source_art/world/jianghai_old_city/previews/01_avenue_dusk.png`; generated outputs committed under `docs/media/`; Git history |
| `assets/branding/operation-steel-tide-icon.svg` | Created for this project; AI assistance may have contributed | Included under the root MIT license, subject to the limitation above | Git history |
| Rescue tilt-rotor `.blend` and `.glb` model | Project-authored in Blender from the checked-in procedural modeling script, with disclosed AI assistance | Included under the root MIT license, subject to the limitation above | `scripts/blender/build_extraction_aircraft.py`, `source_art/extraction_aircraft/extraction_aircraft.blend`, and Git history |
| Steel Tide M4A1 `.blend` and `.glb` model | Textured `M4A1 Assault Rifle` by OpenGameArt creator/uploader `nisu`, plus a SCAR-L grip component, MP5A5 front muzzle assembly, and AXMC scope housing/mount component by Quaternius, composed in Blender for the runtime hierarchy and first-person scale | Both sources are CC0 1.0 Universal; no attribution required, both creator credits retained as provenance; sources and composite outputs are not relicensed as MIT | nisu source published 2022-04-24 and acquired 2026-08-28 from `https://opengameart.org/content/m4a1-assault-rifle`; Quaternius Ultimate Guns Pack components acquired 2026-08-20 from `https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F`; exact source objects, license records, hashes, component mappings, acquisition evidence, dedicated authored front/rear-iron visibility hierarchy, source-glass-derived reticle anchor, physically open runtime optic aperture, muzzle markers, and the deliberate shared-geometry fallback for stat variants are recorded in `assets/models/steel_tide_m4a1/LICENSE.md`, `assets/models/quaternius_ultimate_guns/LICENSE.md`, and `source_art/third_party/nisu_m4a1/LICENSE_EVIDENCE.md`; reproducible build is `scripts/blender/build_nisu_m4a1.py` |
| Steel Tide shared micro, holo, and magnified optic `.blend` and `.glb` models | Three finished scope components from the Quaternius Ultimate Guns Pack, extracted from the tracked AXMC, AWM, and VSS source GLBs and reshaped in Blender into distinct open-aperture silhouettes | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; source and derivative outputs are not relicensed as MIT | Sources acquired 2026-08-20 from `https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F` and adapted 2026-08-28; exact `SniperRifle_3`/`SniperRifle_5`/`SniperRifle_4` mappings, source and output SHA-256 hashes, removed source-glass panes, physically open aperture checks, node contract, and deterministic build checks are recorded in `assets/models/steel_tide_optics/LICENSE.md` and `source_art/combat_optics/README.md`; reproducible build is `scripts/blender/build_authored_optics.py` |
| Steel Tide reloadable AK-74N `.blend` and `.glb` model | Blender mechanism adaptation of the tracked Quaternius Ultimate Guns Pack AK source; all 1,382 authored source triangles and five materials are retained, with the existing 227-triangle curved magazine separated for visible reload motion | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; source and derivative outputs are not relicensed as MIT | Quaternius source acquired 2026-08-20 from `https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F` and adapted 2026-08-28; exact input/output hashes, source-object mapping, magazine topology partition, runtime hierarchy, and deterministic rebuild checks are recorded in `assets/models/steel_tide_ak74/LICENSE.md`, `source_art/reloadable_weapons/README.md`, and `scripts/blender/build_reloadable_ak74.py` |
| First-person trauma-care and armor-repair prop set | Creator: Operation Steel Tide project contributors. Project-authored in Blender 4.5 on 2026-08-28 from the checked-in DCC generator, with disclosed AI assistance; no third-party geometry, textures, logos, or marketplace content are present | Root MIT license; required attribution is preservation of the root MIT copyright and permission notice, with no additional third-party credit required | Canonical source `source_art/field_use/field_use_props.blend`; runtime output and studio preview `assets/models/steel_tide_field_use/{field_use_props.glb,field_use_props_preview.png}`; reproducible build and GLB round-trip checks `scripts/blender/build_field_use_props.py`; verified 138 meshes, 22,276 triangles, 17 PBR materials, six grip markers, two moving closures, and embedded payload; detailed record in `assets/models/steel_tide_field_use/LICENSE.md` and `source_art/field_use/README.md` |
| Compact 5v5 demolition objective device | Creator: Operation Steel Tide project contributors. Project-authored in Blender 4.5 on 2026-08-29 from the checked-in DCC generator, with disclosed AI assistance; no third-party geometry, textures, fonts, logos, or marketplace content are present | Root MIT license; required attribution is preservation of the root MIT copyright and permission notice, with no additional third-party credit required | Canonical source `source_art/demolition_device/demolition_device.blend`; runtime output and studio preview `assets/models/steel_tide_demolition_device/{demolition_device.glb,demolition_device_preview.png}`; reproducible build and GLB round-trip checks `scripts/blender/build_demolition_device.py`; verified 48 meshes, 9,216 triangles, nine PBR/emissive materials, named case/screen/status-light/carry-socket contract, 0.344 by 0.201 by 0.164 metre bounds, and SHA-256 `580F71F6ACED03888734BCD73C863A5CFB2DD35E33F415927EE899A7A8897A7F`; detailed record in `assets/models/steel_tide_demolition_device/LICENSE.md` and `source_art/demolition_device/README.md` |
| Legacy Steel Tide operator `.blend` and `.glb` model | Project-authored in Blender from the checked-in procedural modeling script, with disclosed AI assistance; no third-party geometry or textures copied | Included under the root MIT license, subject to the limitation above | `scripts/blender/generate_combat_models.py`, `source_art/combat_models/steel_tide_operator.blend`, and Git history |
| Tactical knife, Zhanma Dao, and Tianxuan Dao `.blend` and `.glb` models | Creator: Operation Steel Tide project contributors. Project-authored in Blender 4.5 on 2026-08-28 from the checked-in DCC generator, with disclosed AI assistance; no external source URL applies and no third-party geometry or textures are present | Root MIT license; required attribution is preservation of the root MIT copyright and permission notice, with no additional third-party credit required | Canonical local sources `source_art/melee_weapons/{tactical_knife,zhanma_dao,tianxuan_dao}.blend`; reproducible build and GLB round-trip validation in `scripts/blender/build_melee_weapons.py`; runtime outputs `assets/models/steel_tide_melee/{tactical_knife,zhanma_dao,tianxuan_dao}.glb`; exact triangulated counts are 13,216, 17,548, and 18,212 respectively; additional workflow notes are in `source_art/melee_weapons/README.md` and `assets/models/LICENSE.md` |
| Jianghai Old City map layout, street and supporting geometry, district composition, 2026-08-29 Chinese district rebuild, material adaptations, art direction, sign wording and placement, objective-terminal status screens and adaptations, urban-life composition, facade expansion, pawnshop entrance adaptation/composition, factory-gate portal and hinged-entry composition, authored perimeter density, street-cadence replacements, and valley foundation/terrain composition | Project-authored DCC composition in Blender, with disclosed AI assistance. The current rebuild replaces 66 old visible anchors with shared Chinese Temple hall, arcade-shop, and gate-house meshes and authors 42 density placements including west/east `Edge04`-`Edge06`. The hall is a Temple 2 LOD; arcade/gate profiles combine clean Quaternius bodies, VVayToyek pavilion facade/eaves parts, and an extracted/decimated Temple 2 roof. Existing registered Chinese lamps/lions and Poly Haven modules provide surrounding dressing; no new external asset was acquired. The pawnshop and two ten-piece entry facades, authored valley foundation, Coast-derived ground, and 12 Hero Mountain instances remain as recorded below. Shared meshes, a small licensed material vocabulary, and 512-pixel runtime images reduce loading pressure without procedural runtime art | Project-authored portions are included under the root MIT license, subject to the limitation above; embedded third-party geometry, materials, and textures retain the licenses recorded in the rows below, including Quaternius/VVayToyek/BlenderKit/Poly Haven CC0 and Hero Mountain CC BY 4.0. `Old Urban building` and `Scan Old Building Street` retain historical CC0 records but have zero visible current instances | Authoritative DCC source `source_art/world/jianghai_old_city/jianghai_old_city.blend`; Chinese rebuild `scripts/blender/rebuild_jianghai_chinese_district.py`; deterministic export `scripts/blender/export_jianghai_old_city.py`; runtime `assets/models/jianghai_old_city/jianghai_old_city.glb`; previews `source_art/world/jianghai_old_city/previews/{12_chinese_edge_gate,13_chinese_avenue,14_chinese_old_city_overview}.png`; final `.blend` 42,607,105 bytes / SHA-256 `97226E2ED4860E676F27171F7AEF76B33AFF493AD991779887BE984B5DCF9F17`; final GLB 49,926,284 bytes / SHA-256 `BAD4B6C18C8FC8488419ED9EB06F18F6C34544FEAC054EF71555F0D5EB2C0433`; GLB audit 263 unique meshes/569 mesh nodes, 378 unique/1,517 instanced surfaces, 943,282 unique/3,015,841 instanced triangles, 93 materials, 142 textures/120 images at maximum 512 pixels; audit PASS with 42 density placements, zero intersections, and zero retired visible instances; complete mapping in `source_art/world/jianghai_old_city/{README.md,LICENSE_EVIDENCE.md}` |
| Modular Factory Facade geometry and materials embedded in Jianghai Old City | James Ray Cock on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-27 from `https://polyhaven.com/a/modular_factory_facade`; exact hashes and local mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Modular Urban Apartments Facade geometry, materials, and textures embedded in Jianghai Old City as 36 adapted facade objects in two asymmetrical 3-by-3 overlays | James Ray Cock on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-27 from `https://polyhaven.com/a/modular_urban_apartments_facade`; source bundle hashes, DCC mapping, and delivered expansion audit are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Chinese Four-corner Pavilion - Free modeled geometry embedded in Jianghai Old City as the 15-part pawnshop gate canopy and the facade/eaves vocabulary of the current arcade-shop and gate-house shared meshes | VVayToyek on itch.io | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free`; official-page evidence, ZIP/FBX hashes, retained-part mapping, and packed-output mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`; raw source remains in the external acquisition cache |
| Chinese Temple 2 geometry and materials embedded as the current Jianghai Temple hall LOD and as the extracted/decimated roof on the arcade-shop and gate-house shared meshes | Free poly on BlenderKit, `assetBaseId` `8701a79a-1635-437c-b1d2-6b14f14fc351` | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-27 from the BlenderKit asset page recorded in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`; current DCC mapping created 2026-08-29 |
| Chinese red lamp geometry and materials embedded as five Jianghai storefront instances | Kin Chen on BlenderKit, `assetBaseId` `b97e433c-2eb1-46b8-9633-5bdee21e4e7a` | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-27 from the BlenderKit asset page recorded in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Pink city bicycle geometry embedded in Jianghai Old City as three static sidewalk instances | Kin Chen on BlenderKit, `assetBaseId` `4c1a83c1-829f-4c00-878e-9e73c6b89c3b` | CC0 1.0 Universal; official API `license=cc_zero`, `isFree=true`; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://www.blenderkit.com/asset-gallery-detail/4c1a83c1-829f-4c00-878e-9e73c6b89c3b/`; converted to a static rest pose, stripped of its rig, given weathered material adaptations, and cleaned to 11,825 triangles before three-instance placement; input/API hashes and output mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Old Urban building historical Jianghai source; all former visible placements retired from the current delivery | Abobla O.S on BlenderKit, `assetBaseId` `8177ff94-1645-4b50-95cc-cb05a336e34d` | CC0 1.0 Universal; official API `license=cc_zero`, `isFree=true`; not relicensed as MIT | Acquired 2026-08-28 from the official BlenderKit asset page; historical cache hash and mapping are retained in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`; current `.blend`/GLB visible instance count is zero |
| Scan Old Building Street historical Jianghai source; all former visible placements retired from the current delivery | Free poly on BlenderKit, `assetBaseId` `d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030` | CC0 1.0 Universal; official API `license=cc_zero`, `isFree=true`; not relicensed as MIT | Acquired 2026-08-28 from the official BlenderKit asset page; historical cache hash and mapping are retained in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`; current `.blend`/GLB visible instance count is zero |
| Chinese Porcelain Lion geometry and materials embedded in Jianghai Old City | Free poly on BlenderKit, `assetBaseId` `50b661cb-119d-4e80-8a9c-5c6996cbb0c8` | CC0 1.0 Universal; official API `license=cc_zero`, `isFree=true`; not relicensed as MIT | Acquired 2026-08-28 from the official BlenderKit asset page; local cache hash and output mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Television 02 geometry, materials, and textures embedded in Jianghai Old City | Benny Weimer on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/television_02`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Exterior Aircon Unit geometry, materials, and textures embedded in Jianghai Old City | Monsta3D on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/exterior_aircon_unit`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Rollershutter Window 03 geometry, materials, and textures embedded in Jianghai Old City and exported as a retained standalone alternate/legacy shutter GLB | MP on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/rollershutter_window_03`; external 1K glTF cache, packed `.blend`, main runtime GLB, and derived `assets/models/jianghai_old_city/rollershutter_window_03.glb` mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`; the derivative is retained but no longer supplies either current interactive-door visual |
| Trashbag geometry, materials, and textures embedded in Jianghai Old City | Benny Weimer on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/trashbag`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Utility Box 01 geometry, materials, and textures embedded in Jianghai Old City | James Ray Cock on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/utility_box_01`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Barrel 03 geometry, materials, and textures embedded in Jianghai Old City | Serhii Khromov on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/barrel_03`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Plastic Crate 02 geometry, materials, and textures embedded in Jianghai Old City | Fabi_G on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/plastic_crate_02`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Security Camera 01 static geometry, materials, and textures embedded in Jianghai Old City; source rig and animations are not shipped | Alexander Otterbeck (modeling and texturing) and Yann Kervran (rigging) on Poly Haven | CC0 1.0 Universal; no attribution required, contributor credits retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/security_camera_01`; external 1K glTF cache and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Chinese Tea Table geometry, materials, and textures embedded at the Jianghai pawnshop | Kirill Sannikov on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/chinese_tea_table`; source bundle hashes and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Chinese Stool geometry, materials, and textures embedded as three Jianghai pawnshop instances | Kirill Sannikov on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/chinese_stool`; source bundle hashes and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Hand Truck geometry, materials, and textures embedded at the Jianghai factory | Mutanzom3D on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/hand_truck`; source bundle hashes and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Rocky Terrain PBR textures embedded on the sides of the project-authored Jianghai `OldCityFoundation` | Amal Kumar on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; source textures are not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/rocky_terrain`; diffuse, normal, and roughness are packed at a 512-pixel cap in the current delivery. The verified displacement input remains private and is not used to generate delivered geometry; exact evidence and mappings are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Gravel Floor 03 PBR textures embedded on top of the project-authored Jianghai `OldCityFoundation` and on the Coast-derived ground composite | Charlotte Baglioni on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; source textures are not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/gravel_floor_03`; diffuse, OpenGL-normal, and roughness are packed at a 512-pixel cap in the current delivery. The ground material uses base-color factor `(0.92, 0.78, 0.62, 1.0)` and a 7-meter affine world-XY UV layout; the verified displacement input remains private and is not used to generate delivered geometry. All four local SHA-256 values, official API byte/MD5 matches, and output mappings are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Coast Line 01 geometry embedded in Jianghai Old City as one 84,960-vertex, 168,480-triangle perimeter-ground composite assembled from eight modeled scan placements | Rob Tuytel (photography and processing) and Rico Cilliers (cleanup) on Poly Haven | CC0 1.0 Universal; no attribution required, contributor credits retained as provenance; source geometry is not relicensed as MIT | Acquired 2026-08-29 from `https://polyhaven.com/a/coast_line_01`; raw 2K glTF bundle remains under private `JIANGHAI_VALLEY_ACQUISITION_ROOT` and is not committed. The final Blender-authored `JianghaiPerimeterGroundComposite` is one connected mesh with two boundary loops totaling 1,440 edges, zero degenerates, and zero invalid face normals. A signed-distance transition derived from the actual projected `OldCityFoundation` top footprint keeps the playable platform safe while blending the modeled Coast relief outward. This is authored connecting geometry, not camera masking or a primitive. Coast Line 01 is used only as the terrain geometry source: its material and images are absent from the delivered files, whose ground surface uses Charlotte Baglioni's CC0 Gravel Floor 03 textures. Exact source-file SHA-256/API MD5 evidence, API snapshots, DCC adaptation, and packed `.blend`/runtime GLB mapping are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Hero Mountain geometry and Color/Normal/Roughness textures embedded as one decimated 14,000-triangle mesh shared by 12 visual-only Jianghai mountain instances in staggered inner and outer rings | solararchitect on Sketchfab | Creative Commons Attribution / CC BY 4.0, http://creativecommons.org/licenses/by/4.0/; attribution and indication of modifications required; the source and adapted content are not relicensed as MIT | Published 2021-10-21 and acquired 2026-08-29 from `https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca` by an original-format download through an existing signed-in Edge session; the retained API record is `https://api.sketchfab.com/v3/models/83b3fd690ea44e988d086d5165a5f2ca` (`faceCount=522242`, `vertexCount=262144`, `downloadable=true`); Operation Steel Tide modifies the source through decimation, PBR-node reconstruction, a 512-pixel texture cap/pack in the current delivery, uniform scaling, rotation, and multi-instance composition; exact private-source, API, legalcode, and output evidence is in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Coastal Cliff 01, Coastal Cliff 02, and Namaqualand Cliff 02 evaluation downloads | Rob Tuytel, Rico Cilliers, and Dario Barresi on Poly Haven | CC0 1.0 Universal; no attribution required; retained only as private art-search evidence | Acquired and evaluated 2026-08-29 from their official Poly Haven pages; no geometry, material, or texture from these three assets is embedded in the final packed `.blend` or runtime GLB; retained private hashes and API snapshots are identified in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Noto Sans SC glyph outlines used by Jianghai Old City signs | Google Noto / `notofonts` contributors; the Simplified Chinese variable subset from Noto Sans CJK | SIL Open Font License 1.1; DCC authoring converted only required sign text to static glyph meshes; the original font is not shipped in the final `.blend` or GLB | Acquired 2026-08-27 from `https://github.com/notofonts/noto-cjk`; source hash and license are in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`, and `scripts/blender/export_jianghai_old_city.py` rejects non-built-in font datablocks before export |
| Low-Poly GSh-18 sidearm model and centered runtime adaptation | TastyTony on Sketchfab | CC BY 4.0; attribution required | `assets/models/tastytony_gsh18/LICENSE.md`, `scripts/blender/build_tastytony_gsh18.py`, and the creator/source metadata embedded in the source GLB |
| Desert Eagle sidearm model | ELIZION on Sketchfab | CC BY 4.0; attribution required | `assets/models/elizion_desert_eagle/LICENSE.md` and the creator/source metadata embedded in the GLB |
| Deployment-preview military soldier | BAMEN (`bamenwo05`) on Sketchfab | CC BY 4.0; attribution required | `assets/models/bamen_military_soldier/LICENSE.md`, retained original FBX, cleaned Blender source, and reproducible import script |
| Tide Hunter roaming Boss monster | HorrorGameMaker.com on OpenGameArt | CC0 / Public Domain as marked on the source page | `assets/models/tide_hunter_monster/LICENSE.md`, `source_art/third_party/tide_hunter_monster/tide_hunter_monster.blend`, and `scripts/blender/build_tide_hunter_monster.py` |
| Field operator animation clips | Quaternius Universal Animation Library and Universal Animation Library 2 | CC0 1.0 Universal; no attribution required, creator credit retained as courtesy | Acquired 2026-08-20 from the two official itch.io pages; standard GLB exports, license copies, and source mapping are in `source_art/third_party/quaternius_universal_animation_library/`; retargeted output is `assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb` |
| Five-role extraction operator roster, four unarmed Jianghai indoor-resident reuses, and the shared retargeted action set | Quaternius Ultimate Modular Women `Soldier`, `Worker`, `SciFi`, `Adventurer`, and `Punk` presets plus Quaternius Universal Animation Library actions; the same five character meshes are refined in Blender with selective subdivision, shape-preserving creases, smooth shading, scalar PBR parameters, and four-influence normalized skinning | CC0 1.0 Universal; no attribution required, creator credit retained as courtesy | Models acquired 2026-08-27 from `https://quaternius.com/packs/ultimatemodularwomen.html`; VIPER/HERON/LYNX/MAGPIE/JACKAL source mapping, supplied license evidence, unmodified source presets, reproducible high-detail DCC adaptation, and five runtime GLBs are recorded in `assets/models/quaternius_operators/LICENSE.md` and `source_art/third_party/quaternius_modular_women/`. Jianghai reuses unarmed MAGPIE, HERON, JACKAL, and VIPER instances for its four animated residents. The adaptation introduces no image textures or new UV artwork and preserves the runtime node, socket, and 25-action contract. |
| AK-74N, SCAR-L, M24, AXMC, AWM, VSS, MP5A5, M3A1, P226, and M1911 weapon visuals | Selected authored GLB models from Quaternius Ultimate Guns Pack | CC0 1.0 Universal; no attribution required, creator credit retained as courtesy | Acquired 2026-08-20 from `https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F`; exact source-file mapping and license record are in `assets/models/quaternius_ultimate_guns/LICENSE.md` |
| First-person tactical arms and SMG-45 weapon visual, including the original static arm export and static rifle/two-handed service- and large-pistol arm poses | DJMaesen on Sketchfab; poses and the animated sleeve fit evaluated and exported in Blender from the tracked source GLB | CC BY 4.0; attribution required | Source acquired 2026-08-21; new static pose variants generated 2026-08-24; animated shoulder continuation and boundary closure revised 2026-08-27; service-pistol shoulder, wrist IK target, and elbow pole revised 2026-08-28 to preserve a bent support arm without a large runtime limb translation; the fitted-wrist-to-fuller-forearm volume blend and uniform 8% first-person weapon enlargement around the authored two-hand grip center were also revised 2026-08-28. The adaptation preserves the authored vertex set, UV layers, skin weights, materials, reload animation, and uniform weapon proportions while adding only the sleeve closure faces. Original GLB metadata, all runtime GLB/texture adaptations, and reproducible Blender sources are recorded in `assets/models/djmaesen_smg45/LICENSE.md`, `source_art/third_party/djmaesen_fps_smg45/`, `scripts/blender/build_djmaesen_smg45.py`, and `scripts/blender/build_first_person_arms.py` |
| Old Military Crate geometry and materials, including packed Jianghai reuse | Jack Mava on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/old_military_crate`; repository-local source is `assets/models/old_military_crate/`, with the Jianghai adaptation packed into its authoritative `.blend` and runtime `.glb`; full mapping in `assets/models/LICENSE.md` |
| Concrete Road Barrier geometry and materials, including packed Jianghai reuse | Amal Kumar on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/concrete_road_barrier`; repository-local source is `assets/models/concrete_road_barrier/`, with the Jianghai adaptation packed into its authoritative `.blend` and runtime `.glb`; full mapping in `assets/models/LICENSE.md` |
| Street Lamp 01, Metal Trash Can, Coffee Cart 01, Wooden Crate 01, Plastic Crate 01, and Wicker Basket 01, including the Coffee Cart 01 and Wicker Basket 01 copies embedded in Jianghai Old City | Josh Dean, GurJas Studios, Joe Seabuhr, James Ray Cock, PierreB3D, and Kuutti Siitonen via Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained for provenance; not relicensed as MIT inside the Jianghai composite | Acquired 2026-08-28 from the six official Poly Haven asset pages. Original 1K glTF, binary buffers, textures, official API MD5 values, repository SHA-256 values, dimensions, and the two Blender-derived single-trash-can runtime mappings are recorded in `assets/models/polyhaven_residential_street/LICENSE.md`; Jianghai embedding is mapped in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` without duplicating per-file hashes |
| City Kit (Industrial) 1.0 GLB model set and 13 Blender-edited enterable building variants used by demolition arenas, the freight terminal, and residential rooftop dressing | Kenney; door apertures edited in Blender for this project | CC0 1.0 | Original pack from `https://kenney.nl/assets/city-kit-industrial`; edited variants generated 2026-08-27 by `tools/build_enterable_industrial_buildings.py`; layout, output mapping, and license copy in `assets/models/kenney_city_kit_industrial/` and `assets/models/LICENSE.md` |
| Factory Kit 3.0 authored overhead and personnel doors plus selected Tideglass machine covers | Kenney; personnel-door sample scene cleaned and hinge pivot prepared in Blender for this project | CC0 1.0 | Official pack from `https://kenney.nl/assets/factory-kit`; overhead door and machine covers acquired 2026-08-19, personnel door acquired and adapted 2026-08-27; `door-hinged.glb` is the current visible art for both Jianghai interactive doors at 1.45-by-2.65-meter openings with normal 96-degree side swings; `machine.glb`, `hopper-high-round.glb`, and `machine-window.glb` provide three distinct Tideglass Reactor covers; local source, runtime derivatives, extracted colormap, and license copy in `assets/models/kenney_factory_kit/` |
| Furniture Kit 2.0 selected interior props | Kenney | CC0 1.0 | Acquired 2026-08-26 from `https://kenney.nl/assets/furniture-kit`; selected GLBs and license copy in `assets/models/kenney_furniture_kit/` |
| City Kit Roads selected road and street-furniture models and material atlas | Kenney | CC0 1.0; no attribution required, creator credit retained as courtesy | Acquired 2026-08-27 from `https://kenney.nl/assets/city-kit-roads`; twenty selected GLBs, original `Textures/colormap.png`, official license copy, pack preview, and local mapping in `assets/models/kenney_city_kit_roads/` |
| 3D House Construction Site buildings, crane, selected container stacks, distinct materials, fencing, ground, road, and concrete truck | Majadroid / Maik Hoffmann; source package credits Imphenzia for the color palette | CC0 1.0; no attribution required, creator credits retained as courtesy | Acquired 2026-08-27 from `https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0`; selected FBX sources and evidence in `source_art/third_party/majadroid_construction_site/`; nine normalized GLBs and local mapping in `assets/models/majadroid_construction_site/`; conversion explicitly selects the office, cargo, concrete-truck, plank, box, and barrel meshes and rejects high-overlap variants in `scripts/blender/build_tideglass_map_assets.py` |
| Modular Industrial Kit selected modules, texture atlas, and eight Godot-ready industrial compositions | Trey Ramm (`minime453` on OpenGameArt) | CC0 1.0; no attribution required, creator credit retained as courtesy | Acquired 2026-08-27 from `https://opengameart.org/content/modular-industrial-kit`; selected FBXs, original README, source-page snapshot, atlas, and preview in `source_art/third_party/trey_modular_industrial/`; runtime assets and local mapping in `assets/models/trey_modular_industrial/`; reproducible conversion in `scripts/blender/build_trey_modular_industrial.py` |
| Special Operations home-screen command hall composite | Project-authored Blender composition of Trey Ramm Modular Industrial Kit modules and Kenney Furniture Kit 2.0 props, with disclosed AI assistance | Underlying visible geometry and materials remain CC0 1.0; project-authored composition and rebuild code are MIT, subject to the development disclosure above | Runtime GLB and exact source mapping in `assets/models/operations_office/`; authoritative editable source in `source_art/operations_office/operations_office_set.blend`; deterministic composition and round-trip validation in `scripts/blender/build_operations_office_set.py`; Trey and Kenney acquisition dates, source URLs, license evidence, required attribution, and local file mapping in `assets/models/LICENSE.md` |
| Buildings Pack selected authored buildings, including fourteen adapted Jianghai perimeter buildings, three full street-cadence replacements, and clean bodies beneath the current Chinese arcade-shop/gate-house profiles | Quaternius | CC0 1.0 Universal; no attribution required, creator credit retained as courtesy | Acquired 2026-08-28 from `https://quaternius.com/packs/buildings.html`; nine retained FBXs, four converted selections, official source-page snapshot, official preview, and license record in `source_art/third_party/quaternius_buildings_pack/`; centered and grounded runtime GLBs, exact file mapping, and verification record in `assets/models/quaternius_buildings_pack/`; four/three/three/four Jianghai density instances respectively use Building1 Large, Building3 Big, Building4, and House2; clean Building4/Building3 Big bodies are combined with separately licensed pavilion and Temple parts in the 2026-08-29 DCC rebuild; reproducible conversion in `scripts/blender/build_quaternius_buildings_pack.py` and deterministic Jianghai adaptation in `scripts/blender/{rebuild_jianghai_chinese_district,export_jianghai_old_city}.py` |
| Downtown City MegaKit standard modular scene set used by Saint Marais Old Town, demolition-arena facades, and Jianghai Old City hinged-entry facades | Quaternius | CC0 1.0 Universal | Acquired 2026-08-19 from `https://quaternius.com/packs/downtowncitymegakit.html`; Jianghai embeds 18 instances of `Brick_Plain_1.gltf` and two of `DoorFrame_Trim.gltf`, divided into two 10-object entry facades; selected-file mapping and license copy in `assets/models/quaternius_downtown_city/`, with exact Jianghai mapping in `source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` |
| Asphalt 03 surface textures, including packed Jianghai reuse | Charlotte Baglioni (photography) and Dario Barresi (processing) on Poly Haven | CC0 1.0 Universal; no attribution required, creator credits retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/asphalt_03`; local maps are `assets/textures/asphalt_03_{diff,normal,rough}_1k.jpg`, with adapted copies in the Jianghai `.blend` and GLB |
| Concrete Floor surface textures, including packed Jianghai reuse | eye-candy.xyz on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/concrete_floor`; local maps are `assets/textures/concrete_floor_{diff,normal,rough}_1k.jpg`, with adapted copies in the Jianghai `.blend` and GLB |
| Rusty Painted Metal surface textures; not used by the current Jianghai scene | Amal Kumar on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/rusty_painted_metal`; local maps are `assets/textures/rusty_painted_metal_{diff,normal,rough}_1k.jpg` |
| Corrugated Iron surface textures, including packed Jianghai reuse | Dimitrios Savva (photography) and Jenelle van Heerden (processing) on Poly Haven | CC0 1.0 Universal; no attribution required, creator credits retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/corrugated_iron`; local maps are `assets/textures/corrugated_iron_{diff,normal,rough}_1k.jpg`, with adapted copies in the Jianghai `.blend` and GLB |
| Gravel Embedded Concrete surface textures, including packed Jianghai reuse | Charlotte Baglioni on Poly Haven | CC0 1.0 Universal; no attribution required, creator credit retained as provenance; not relicensed as MIT | Acquired 2026-08-06 from `https://polyhaven.com/a/gravel_embedded_concrete`; local maps are `assets/textures/gravel_embedded_concrete_{diff,normal,rough}_1k.jpg`, with adapted copies in the Jianghai `.blend` and GLB |
| Kloppenheim 06 (Pure Sky) 1K HDRI retained as repository-local CC0 evidence; not embedded in the Jianghai `.blend` or map GLB and not loaded by the current procedural-sky atmosphere | Greg Zaal (Original) and Jarod Guest (Sky Edits) on Poly Haven | CC0 1.0 Universal; no attribution required, contributor credits retained as provenance; not relicensed as MIT | Acquired 2026-08-28 from `https://polyhaven.com/a/kloppenheim_06_puresky`; local `assets/textures/kloppenheim_06_puresky_1k.hdr` is 1,173,154 bytes with SHA-256 `206C67E3A1B992282821CF06662BDD69BBB4915C1C4444A66338A40D6A7D4E34`; official API MD5 `995d68b1656f26452572645c0ffe898b`; official download `https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/kloppenheim_06_puresky_1k.hdr` |
| Runtime primitive meshes, materials, UI, effects, and synthesized sounds | Generated by project code at runtime | Project-authored implementation, with disclosed AI assistance | Source files and Git history |

The upstream pre-valley integration artifact is a 61,677,884-byte packed
`.blend` with SHA-256
`C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7`
and a 73,809,716-byte GLB with SHA-256
`2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A`.
The source audit records 487 mesh objects, 196 unique mesh datablocks,
4,471,243 raw mesh-object triangles, and 821,213 triangles counted once per
unique mesh. Dependency-graph evaluation and the runtime export produced 550
mesh nodes and 4,500,345 instance triangles. The matching Godot
`--validate-refinery-map` diagnostic passed with 550 imported authored meshes,
770 surfaces, all 770 surfaces material-backed, 4,500,345 authored instance
triangles, 7/7 authored anchors, terminal checks of 2/2/2/2, 2/2 authored status
screens, and four interior residents. Route
validation reports `routes=True`,
`route_probes=14`, and
`route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at
multiple points for `y=0.45`, `y=1.4`, and `y=2.6`.

Those hashes and counts remain historical evidence for the two ten-piece
hinged-entry facades, hinged doors, interior loot, and four residents; they are
not the current post-valley output.

The following artifacts are historical valley-era evidence and are not the
current Chinese-district delivery. The verified final pre-rebase Jianghai GLB is
76,862,308 bytes with SHA-256
`0C0174672630957390A959BC3BD71DB3F4849CC7CABE0AFADFDD12273DFE02A5`;
the Blender audit and export pass with 4,835,033 evaluated full-scene triangles,
below the 5,000,000-triangle gate. The verified final pre-rebase packed `.blend`
is 74,037,661 bytes with SHA-256
`C9BAC433CF77791B3730E309A5E0BEEF6CF4849593D44018FD2CDFE5AC8FAA08`.
Those are historical valley pre-rebase artifacts. The historical combined packed
`.blend` is 81,861,168 bytes with SHA-256
`7CA84CD2B17C3872323D8A5EE7B1A4BA5BCB360F4326FB2331327BED4F493461`;
it contains 500 mesh objects, 198 unique mesh datablocks, 4,807,899 raw
mesh-object triangles, and 1,003,869 triangles counted once per unique mesh.
Dependency-graph evaluation resolves 563 objects and 4,836,825 instance
triangles. The historical 84,723,312-byte GLB has SHA-256
`7E2BB712BCF031692FAFB0E4E0FA59F3E75CE340B2748F5EDBDB7B105D9B2965`.
All builder, audit, export, and GLB-round-trip gates pass with 8/8 anchors.

For that historical artifact, an explicit Godot editor reimport followed by a
second no-op import passed with 563 authored meshes, 784 surfaces, all 784
surfaces material-backed, 4,836,825 authored triangles, 8/8 anchors, 419 detail
meshes, 406 shadow casters, and quality tiers 130/226/406. The valley contract
is one ground plus 12 mountains totaling 336,668 triangles. Collision passes
240/240, routes pass 14 probes with no blocker, residents pass 4/4, hinged doors
pass at 96 degrees, and Day plus always-procedural Dusk atmosphere checks pass
with a continuous sky/ground horizon and no panorama.

The current Chinese-district binaries and exact hashes are recorded in the
inventory row above. Their explicit Godot reimport resolves 569 authored meshes,
1,517 material-backed surfaces, 3,015,841 authored instance triangles, and all
eight required anchors. At runtime, 283 safe repeated render sources are grouped
into 71 spatial `MultiMesh` batches. Physics uses 102 reviewed
placement/profile boxes plus 20 landmark boxes and zero concave collision
shapes. Threaded map preload completes in 502-1,069 ms across the recorded
verification runs, cached acquisition is 0 ms, and reload-to-world-ready
completes in 1,535-2,913 ms across those runs. All 11 representative captures
pass below 794 MB video memory and 532
MB texture memory.

The final pre-rebase valley DCC and serialized-GLB audits pass with a
188-triangle, 96-source-vertex project-authored `OldCityFoundation`; one
84,960-vertex, 168,480-triangle `JianghaiPerimeterGroundComposite`; and 12
instances of one shared 14,000-triangle Hero Mountain mesh arranged as staggered
six-object inner and outer rings. The valley totals 336,668 instance triangles.
The modeled ground bounds are X `-600.878..600.853`, Y
`-540.340..660.056`, and Z `-12.7965..5.0390` meters, for 17.835 meters of
vertical relief. It has one connected component, two boundary loops totaling
1,440 edges, zero degenerate triangles, and zero invalid face normals.

The safety transition uses signed distance from the actual projected top
footprint of `OldCityFoundation`: 25 top faces, 32 vertices, and 16 projected
boundary edges, with X bounds `-169.998..169.998` and Blender-Y bounds
`-99.998..219.998`. Coverage is 1.000, maximum foundation gap is 0.103 meters,
and the safe-area highest ground is -0.120 meters. Relief is 0.969 meters at
0-60 meters from the foundation and 3.955 meters at 60-160 meters. Ground slope
RMS/p90/p99/maximum is 0.0579/0.0869/0.2331/0.6620; the complete ring gate
passes 7,920/7,920 probes. No displacement map generates delivered visible
geometry.

The single ground material uses Gravel Floor 03 diffuse, OpenGL-normal, and
roughness maps with base-color factor `(0.92, 0.78, 0.62, 1.0)` and 7-meter
affine world-XY UVs. Official API MD5 values are
`d86981602e03f8f1deeccc5e37a14468`,
`864d073353dcfbbb0a507cbc07e250b7`, and
`698b4d00999fa3108d4abc8584dde936`; complete SHA-256 records remain in
`source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`. Maximum DCC and GLB UV
errors are `3.27e-6` and `4.36e-6`, within the `1.2e-5` gate, and both Jacobian
checks pass. Coast source material/image counts are 0/0. The north-end curb-cap
repair passes 330/330 top-ray probes with zero side hits, and the south approach
passes 90/90 with zero side hits. All valley meshes are visual-only; Godot
retains gameplay collision and navigation.

In the historical post-valley artifact, the final DCC correction removed a duplicate 180-degree rotation from the
Municipal terminal root so its screen faces opposite the Grand terminal, and
mounts all 22 shutter/air-conditioner props flush to tenement facades rather
than occupying `CentralAvenue`. Final DCC QA also deletes the redundant
`JianghaiArtPass_FactoryHeroShutter` overlay because it became obsolete when
the damaged factory shell was replaced by five finished CC0 buildings. The
factory landmark entry retains a five-object Blender portal reusing DCC-authored
brick piers, caps, and a corrugated roof. Its audit is
`factory_gate_portal=5/5` with `factory_gate_portal_aligned=True`. This was
authored visible art rather than a primitive or procedural runtime
substitute; reused packed materials retain their existing license records. The
delivered urban-life pass adds 36 adapted apartment-facade objects in two
asymmetrical 3-by-3 overlays, three static Pink city bicycles, a market tea cart
and basket, a pawnshop tea table and three stools, a factory hand truck, a
finished CC0 pawnshop backdrop, and a hero entrance built in Blender from 15
modeled VVayToyek pavilion parts plus eight solid facade modules and eight
authored inserts. Six flat gate boards and twelve zero-thickness south-wall
objects were removed and rejected by the export/audit guard. The expansion also
adds two 10-object personnel-entry facades from the tracked Quaternius Downtown
City MegaKit selection: 18 `Brick_Plain_1` and two `DoorFrame_Trim` instances in
total, divided as nine bricks and one doorframe at each entry. The central
openings are 1.45 by 2.65 meters, and the two interactive visuals were
Kenney CC0 `door-hinged.glb` instances with normal 96-degree side swings. It
also includes five market shops (three
Old Urban building and two Scan Old Building Street instances), two Old Urban
building rear houses, and five Chinese red lamps. The former damaged factory
shell is replaced by three Old Urban building office/admin instances and two
Scan Old Building Street workshops. A further authored-density pass added 36
complete perimeter buildings from six CC0 profiles: eight Old Urban, fourteen
Scan Old, four Quaternius Building1 Large, three Building3 Big, three Building4,
and four House2 instances. Four formerly repeated near-street buildings are
replaced with full Scan Old or Quaternius meshes, producing five distinct
silhouettes across the six-building review row. All 36 density placements use
an explicit transform table, pass a zero-intersection audit against the existing
blocks, and are serialized in the authoritative `.blend`. The export script
deterministically reapplies these documented DCC adaptations, factory-frontage
substitution, sign cleanup, and material tuning; it does not randomly or at
runtime procedurally generate the city. The current rebuild supersedes every
visible Old Urban and Scan Old building placement with the three Chinese-profile
shared meshes and expands the reviewed density table to 42 placements.

The historical runtime derived building physics from 107 structural and 133
detail mesh instances, producing 240 concave shapes and 3,560,137
collision-instance triangles. The current delivery removes that expensive
builder and uses 122 reviewed box shapes instead: 102 placement/profile shapes
and 20 landmark facade/traversal shapes. Dedicated diagnostics verify closed-door
enemy-capsule blocking, opened-door clearance, visible wall and rail hits,
rail-gap penetration, rooftop traversal, 12/12 reachable high-value loot
placements, and ballistic building probes with zero concave shapes.
The same export script
reproducibly derives `assets/models/jianghai_old_city/rollershutter_window_03.glb`
(187,940 bytes; SHA-256
`C4884AFCD7560E4BB23320A8C311DB0011504F7C5FEE30D58C266D54F7C6B166`)
from the packed scene's `JianghaiArtPass_EastShutter00` PBR mesh. Neither Old
City entrance now instances this derivative; it is retained as an
alternate/legacy asset. The current `InteractiveBuildingDoor` visuals are the
Kenney personnel doors recorded above. Their collision, animation, network
state, and AI traversal remain project gameplay behavior and are checked by
`--validate-refinery-doors`.
Full evidence is in
`source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`.

The upstream pre-valley figures were captured on 2026-08-28 after the 20 entry-facade
objects, hinged doors, interior loot, and four authored residents were active.
High tier disables shadows only on fine decorative meshes; model geometry,
materials, and visibility ranges are unchanged. Capture tuples (draw calls /
objects / primitives) are Overview 582/808/4,286,647, Victory street
750/890/4,207,438, Street-life bicycle close-up 421/466/3,322,262,
Guangchang pawnshop 511/740/2,230,125, Red Star factory 561/623/4,265,595,
Market footbridge 739/1,030/4,707,642, north-ward density 352/478/2,512,297,
and daylight overview 1,014/1,271/7,113,753. All eight passed; peak video
memory was 1,001.8 MB and peak texture memory was 852.7 MB.
These historical results do not replace the current representative-view draw
calls, visible-object and primitive counts. The historical post-valley capture tuples
(draw calls / objects / primitives) are Overview 571/810/4,509,279, Mountain
valley aerial 643/827/5,258,780, South 8/8/226,868, North
107/114/756,216, Perimeter ground scan 570/749/4,576,881, Street life
615/724/3,833,658, Guangchang pawnshop 509/742/2,453,853, Red Star factory
551/621/4,491,929, Market footbridge 727/1,045/5,081,051, North-ward density
382/514/2,795,341, and Daylight overview 1,005/1,274/7,351,237. Every capture
passed; video memory was 1,087.0 MB of a 1,536 MB budget and texture memory was
900.9 MB of a 1,152 MB budget. Independent visual review was DELIVERABLE,
with no sky/terrain seam, radial pattern, skirt, z-fighting, trench, floating
platform, or material south-line blocker. The `refinery-map`,
`refinery-collision`, `refinery-doors`, `refinery-atmosphere`, `map-density`,
`large-map`, `residential`, `stairs`, `skylinks`, and `vehicle-drive`
diagnostics all exit 0.

For the current Chinese-district delivery, render batching converts 283 safe
repeated source meshes into 71 spatial batches. A final capture run records
overview at 967 draw calls / 968 objects, mountain aerial at 1,133-1,497 /
1,163-1,538 across final runs
versus 1,562 / 2,867 before batching, and daylight overview at 1,534 / 1,558
versus 1,615 / 2,378 before batching. All 11 views pass; all 71 batch origins
match their source centroids, reconstructed pose error is at most 0.000002
meters, visibility-range shortfall is zero, and the recorded peak is 793.3 MB
video memory and 531.0 MB texture memory.

No separate third-party music, font, or stock-image collection is currently tracked in the repository. Noto Sans SC was a repository-external DCC-authoring input used only to create the static Jianghai sign glyph meshes documented above; the original font is not tracked and is absent from both the final `.blend` and GLB. The Quaternius CC0 animation libraries listed above are the tracked animation pack.

The root MIT license covers project-authored material only. It does not relicense the third-party raw asset files inventoried above; those files retain their stated source licenses or public-domain dedications.

## Posting guidance

- Credit TastyTony for the Low-Poly GSh-18 model and retain its source and CC BY 4.0 license record in distributions.
- Credit ELIZION for the Desert Eagle model and retain its source and CC BY 4.0 license record in distributions.
- Credit BAMEN for the deployment-preview military soldier and retain its source and CC BY 4.0 license record in distributions.
- Credit HorrorGameMaker.com for the Tide Hunter monster as a courtesy and retain the CC0 source record above.
- Quaternius animations are CC0; retaining the creator and source links above is recommended for provenance even though attribution is not required.
- The Quaternius female operator is CC0; retain the Ultimate Modular Women source link and local source mapping as provenance even though attribution is not required.
- Quaternius weapon models are CC0; retain the pack link and platform mapping so generic silhouettes are not mistaken for manufacturer-authenticated replicas.
- Credit DJMaesen for the FPS animated SMG source and retain the CC BY 4.0 license record in distributions.
- Poly Haven assets are CC0; retain the linked source and hash records whenever a platform requires source disclosure.
- Credit Poly Haven as a courtesy and retain the delivered Modular Factory Facade and Modular Urban Apartments Facade records whenever a platform requires source disclosure; the apartments facade now ships as 36 adapted objects in the Jianghai composite.
- Retain the Coast Line 01, Rocky Terrain, and Gravel Floor 03 source pages, Poly Haven CC0 license link, private-cache hash/API evidence, and packed-output mapping with the Jianghai valley environment. Courtesy credit to Rob Tuytel, Rico Cilliers, Amal Kumar, and Charlotte Baglioni is recommended even though attribution is not required. Coastal Cliff 01, Coastal Cliff 02, and Namaqualand Cliff 02 are evaluation-only records and must not be described as embedded in the delivered artifact.
- Credit **solararchitect** for **Hero Mountain**, link `https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca`, retain the CC BY 4.0 link `http://creativecommons.org/licenses/by/4.0/`, and indicate that Operation Steel Tide modified the work by decimation, PBR-node reconstruction, 512-pixel runtime-texture capping/packing, uniform scaling, rotation, and multi-instance valley composition. Hero Mountain and its adaptations must not be described as MIT-licensed project art.
- Retain the Jianghai Old City evidence record with distributions so its Poly Haven, BlenderKit, and VVayToyek itch.io CC0 inputs, Noto Sans SC DCC-authoring use, and project-authored portions remain distinguishable. Courtesy credit to VVayToyek, James Ray Cock, Abobla O.S, Free poly, Kin Chen, Kirill Sannikov, Mutanzom3D, Joe Seabuhr, Kuutti Siitonen, Benny Weimer, Monsta3D, MP, Serhii Khromov, Fabi_G, Alexander Otterbeck, Yann Kervran, Greg Zaal, Jarod Guest, Poly Haven, BlenderKit, and the Google Noto / `notofonts` contributors is recommended even where the source license does not require attribution.
- Kenney's City Kit Roads assets are CC0; retaining the pack link and bundled license evidence is recommended for provenance even though attribution is not required.
- Majadroid's construction-site package and Trey Ramm's Modular Industrial Kit are CC0; retaining both creator credits and the original package evidence is recommended for provenance even though attribution is not required.
- Quaternius's Buildings Pack is CC0; retain the official pack page, original-file mapping, and creator credit for provenance even though attribution is not required.
- Describe `hero.webp`, `squad.webp`, `city.webp`, `social-preview.png`, and all five `gameplay-*-zh.webp` files as direct in-engine captures. Describe `cover.png` and `squad-key-art.png` as AI-assisted project key art, never as gameplay screenshots.
- Disclose AI assistance plainly. Do not claim that human review, ownership of output, or the MIT license proves anything about model training data.
- Do not repost this project to a community that requires consent-only model-training proof unless its moderators explicitly confirm that the available evidence is sufficient.
- A compliant alternative would require a genuinely traceable content pipeline accepted by that community, not merely removal of the disclosure or rewording of the post.

## Maintainer checklist

When adding a binary asset, record its creator, source URL, exact license, and any attribution requirement before commit. When replacing an asset, remove stale attribution entries. Keep promotional media traceable to an in-engine capture command or another documented source.
