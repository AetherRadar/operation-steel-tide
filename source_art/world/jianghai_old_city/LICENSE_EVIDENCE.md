# Jianghai Old City License Evidence

This record identifies the external assets used in the authoritative Jianghai
Old City DCC scene, the delivered urban-life, facade, and hinged-entry
expansion, and the authored valley environment.
The existing Poly Haven models and surface sets reused by this scene were
acquired on 2026-08-06. Initial map-specific source files were acquired on
2026-08-27; further delivered BlenderKit and finished Poly Haven assets were
acquired on 2026-08-28. Coast Line 01 and three subsequently rejected Poly
Haven cliff candidates were acquired and evaluated on 2026-08-29, when their
official source pages, asset metadata, files API records, and license status
were checked. The final Hero Mountain source was published on 2021-10-21 and
acquired from Sketchfab on 2026-08-29, with its official model API response and
CC BY 4.0 legalcode retained. General cache paths below are relative to
`JIANGHAI_ACQUISITION_ROOT`; valley-source paths are relative to the separate
private `JIANGHAI_VALLEY_ACQUISITION_ROOT`. Hero Mountain original-archive,
API, and legalcode entries explicitly identified as source-evidence-cache files
are retained in a separate private evidence cache; paths beginning with
`assets/` are repository-local sources that predate this map composition. No
private acquisition or evidence cache is committed or required to export the
packed scene.

## 2026-08-29 current Chinese district mapping

The current project-authored Blender rebuild introduces no new external source.
It reuses only the already verified sources in the table below: Free poly's CC0
**Chinese Temple 2**, VVayToyek's CC0 **Chinese Four-corner Pavilion - Free**,
the existing CC0 Chinese red lamp and porcelain lion dressing, Quaternius CC0
Buildings Pack bodies, and the scene's previously registered Poly Haven
modules/materials.

The rebuild replaces 66 old visible anchors and authors 42 density placements,
including west/east `Edge04`-`Edge06`. `JianghaiChineseTempleHall_LOD` is the
Temple 2 LOD. `JianghaiChineseArcadeShop_LOD` and
`JianghaiChineseGateHouse_LOD` combine clean Quaternius building bodies with
adapted pavilion facade/eaves parts and an extracted, decimated Temple 2 roof.
Repeated placements share mesh datablocks, use a deliberately small licensed
material vocabulary, and are exported with a 512-pixel maximum runtime-texture
dimension.

The historical `Old Urban building` and `Scan Old Building Street` acquisition,
hash, and CC0 records remain intact for audit continuity, but their delivery
status is retired. The current authoritative `.blend` and runtime GLB contain
zero visible instances of either source. Representative review evidence is
`previews/12_chinese_edge_gate.png`, `previews/13_chinese_avenue.png`, and
`previews/14_chinese_old_city_overview.png`. The final packed `.blend` is
42,607,105 bytes with SHA-256
`97226E2ED4860E676F27171F7AEF76B33AFF493AD991779887BE984B5DCF9F17`;
the final GLB is 49,926,284 bytes with SHA-256
`BAD4B6C18C8FC8488419ED9EB06F18F6C34544FEAC054EF71555F0D5EB2C0433`.
Its round-trip audit reports 263 unique meshes across 569 mesh nodes, 378
unique/1,517 instanced surfaces, 943,282 unique/3,015,841 instanced triangles,
93 materials, 142 textures backed by 120 images, and maximum image dimension
512. The scene audit passes with zero density intersections and zero visible
retired-building instances.

## Source records

| Source asset | Creator / publisher | Official source | Exact license | Acquired | Acquisition input | Delivery status |
| --- | --- | --- | --- | --- | --- | --- |
| Modular Factory Facade | James Ray Cock / Poly Haven | https://polyhaven.com/a/modular_factory_facade | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-27 | `poly_haven/modular_factory_facade/modular_factory_facade_1k.gltf`, its `.bin`, and 15 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb` |
| Modular Urban Apartments Facade | James Ray Cock / Poly Haven | https://polyhaven.com/a/modular_urban_apartments_facade | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-27 | `poly_haven/modular_urban_apartments_facade/modular_urban_apartments_facade_1k.gltf`, its `.bin`, and 12 1K texture sidecars | Adapted into 36 delivered facade objects forming two asymmetrical 3-by-3 overlays in the packed `.blend` and runtime `.glb` |
| Chinese Four-corner Pavilion - Free | VVayToyek / itch.io | https://vvaytoyek.itch.io/chinese-four-corner-pavilion-free | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `itch/vvaytoyek_chinese_four_corner_pavilion/Chinese Four-corner Pavilion.zip`; extracted `fbx_only/四角亭.fbx`; page evidence `itch_license_evidence.html` | Fifteen modeled timber, tile, column, rafter, lattice, bracket, and ornament parts remain adapted into the pawnshop gate canopy and provide the facade/eaves vocabulary for the current arcade-shop and gate-house shared meshes; raw source remains external, while adapted geometry is packed into the `.blend` and runtime GLB |
| Chinese Temple 2 | Free poly / BlenderKit | https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/; `assetBaseId` `8701a79a-1635-437c-b1d2-6b14f14fc351` | Creative Commons Zero / CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-27 | `blenderkit/chinese_temple_2.glb` | Adapted as `JianghaiChineseTempleHall_LOD`; its extracted and decimated roof also tops the current arcade-shop and gate-house shared meshes in the packed `.blend` and runtime `.glb` |
| Chinese red lamp | Kin Chen / BlenderKit | https://www.blenderkit.com/asset-gallery-detail/b97e433c-2eb1-46b8-9633-5bdee21e4e7a/; `assetBaseId` `b97e433c-2eb1-46b8-9633-5bdee21e4e7a` | Creative Commons Zero / CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-27 | `blenderkit/chinese_red_lamp.glb` | Adapted into five storefront instances in the current packed `.blend` and runtime `.glb` |
| Pink city bicycle | Kin Chen / BlenderKit | https://www.blenderkit.com/asset-gallery-detail/4c1a83c1-829f-4c00-878e-9e73c6b89c3b/; `assetBaseId` `4c1a83c1-829f-4c00-878e-9e73c6b89c3b` | Creative Commons Zero / CC0 1.0 Universal; official API `license=cc_zero`, `isFree=true`; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `blenderkit/pink_city_bicycle/pink_city_bicycle_0_5k.blend`; API evidence `blenderkit/pink_city_bicycle/official_api_search.json` | Converted to a static rest pose, stripped of its rig, given adapted weathered materials, cleaned to 11,825 triangles, and delivered as three instances in the packed `.blend` and runtime `.glb` |
| Old Urban building | Abobla O.S / BlenderKit | https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/; `assetBaseId` `8177ff94-1645-4b50-95cc-cb05a336e34d` | Creative Commons Zero / CC0 1.0 Universal; API `license=cc_zero`, `isFree=true`; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `blenderkit/old_urban_building_1k.blend` | Historical source record retained. All former visible storefront, market, rear-house, factory, street-cadence, and density placements were retired by the 2026-08-29 Chinese district rebuild; current delivered visible instance count is zero |
| Scan Old Building Street | Free poly / BlenderKit | https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/; `assetBaseId` `d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030` | Creative Commons Zero / CC0 1.0 Universal; API `license=cc_zero`, `isFree=true`; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `blenderkit/scan_old_building_street.glb` | Historical source record retained. All former visible market, factory, street-cadence, and density placements were retired by the 2026-08-29 Chinese district rebuild; current delivered visible instance count is zero |
| Chinese Porcelain Lion | Free poly / BlenderKit | https://www.blenderkit.com/asset-gallery-detail/50b661cb-119d-4e80-8a9c-5c6996cbb0c8/; `assetBaseId` `50b661cb-119d-4e80-8a9c-5c6996cbb0c8` | Creative Commons Zero / CC0 1.0 Universal; API `license=cc_zero`, `isFree=true`; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `blenderkit/chinese_porcelain_lion.glb` | Adapted into the current packed `.blend` and runtime `.glb` |
| Television 02 | Benny Weimer / Poly Haven | https://polyhaven.com/a/television_02 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/television_02/television_02_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb`; used with Utility Box 01 for both objective-terminal bodies |
| Exterior Aircon Unit | Monsta3D / Poly Haven | https://polyhaven.com/a/exterior_aircon_unit | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/exterior_aircon_unit/exterior_aircon_unit_1k.gltf`, its `.bin`, and 12 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb` |
| Rollershutter Window 03 | MP / Poly Haven | https://polyhaven.com/a/rollershutter_window_03 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/rollershutter_window_03/rollershutter_window_03_1k.gltf`, its `.bin`, and four 1K texture sidecars | Adapted into the current packed `.blend` and main runtime `.glb`; `JianghaiArtPass_EastShutter00` is still reproducibly exported as `assets/models/jianghai_old_city/rollershutter_window_03.glb`, but that derivative is retained only as an alternate/legacy asset and is no longer the current interactive-door visual |
| Trashbag | Benny Weimer / Poly Haven | https://polyhaven.com/a/trashbag | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/trashbag/trashbag_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb` |
| Utility Box 01 | James Ray Cock / Poly Haven | https://polyhaven.com/a/utility_box_01 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/utility_box_01/utility_box_01_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb`; used with Television 02 for both objective-terminal bodies |
| Barrel 03 | Serhii Khromov / Poly Haven | https://polyhaven.com/a/barrel_03 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/barrel_03/barrel_03_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb` |
| Plastic Crate 02 | Fabi_G / Poly Haven | https://polyhaven.com/a/plastic_crate_02 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/plastic_crate_02/plastic_crate_02_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the current packed `.blend` and runtime `.glb` |
| Security Camera 01 | Alexander Otterbeck (modeling and texturing), Yann Kervran (rigging) / Poly Haven | https://polyhaven.com/a/security_camera_01 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/security_camera_01/security_camera_01_1k.gltf`, its `.bin`, and three 1K texture sidecars | Only static geometry, materials, and textures are adapted into the packed `.blend` and runtime `.glb`; the source rig and animations are not shipped |
| Chinese Tea Table | Kirill Sannikov / Poly Haven | https://polyhaven.com/a/chinese_tea_table | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/chinese_tea_table/chinese_tea_table_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the pawnshop frontage in the packed `.blend` and runtime `.glb` |
| Chinese Stool | Kirill Sannikov / Poly Haven | https://polyhaven.com/a/chinese_stool | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/chinese_stool/chinese_stool_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the pawnshop frontage as three delivered instances in the packed `.blend` and runtime `.glb` |
| Hand Truck | Mutanzom3D / Poly Haven | https://polyhaven.com/a/hand_truck | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `poly_haven/hand_truck/hand_truck_1k.gltf`, its `.bin`, and three 1K texture sidecars | Adapted into the factory frontage in the packed `.blend` and runtime `.glb` |
| Rocky Terrain | Amal Kumar / Poly Haven | https://polyhaven.com/a/rocky_terrain | CC0 1.0 Universal; https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | Four 2K diffuse, displacement, OpenGL-normal, and roughness maps under `rocky_terrain/textures/` in `JIANGHAI_VALLEY_ACQUISITION_ROOT` | Diffuse, normal, and roughness remain adapted to the sides of the project-authored `OldCityFoundation`. Runtime images are capped at 512 pixels and packed into the delivered `.blend`/GLB. The verified displacement map remains private, uncommitted, and unused by delivered geometry |
| Gravel Floor 03 | Charlotte Baglioni / Poly Haven | https://polyhaven.com/a/gravel_floor_03 | CC0 1.0 Universal; https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | Four 2K diffuse, displacement, OpenGL-normal, and roughness maps under `gravel_floor_03/textures/` in `JIANGHAI_VALLEY_ACQUISITION_ROOT` | Diffuse, normal, and roughness are adapted to the top of the project-authored `OldCityFoundation` and to the single Coast-derived perimeter-ground composite. The ground material uses base-color factor `(0.92, 0.78, 0.62, 1.0)` and 7-meter affine world-XY UVs. Runtime images are capped at 512 pixels and packed into the delivered `.blend`/GLB. The verified displacement map remains private, uncommitted, and unused by delivered geometry |
| Coast Line 01 | Rob Tuytel (photography and processing), Rico Cilliers (cleanup) / Poly Haven | https://polyhaven.com/a/coast_line_01 | CC0 1.0 Universal; https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-29 | `coast_line_01/coast_line_01_2k.gltf`, `coast_line_01/coast_line_01.bin`, and 2K diffuse, ARM, and OpenGL-normal sidecars under `JIANGHAI_VALLEY_ACQUISITION_ROOT` | Geometry-only delivered source. Eight modeled scan placements are assembled and shaped in Blender into the single `JianghaiPerimeterGroundComposite`: 84,960 vertices, 168,480 triangles, one connected component, two boundary loops totaling 1,440 edges, zero degenerates, and zero invalid face normals. A signed-distance transition derived from the actual projected `OldCityFoundation` top footprint connects the playable platform to outward Coast relief. This is real authored connecting geometry, not camera masking or a primitive. The delivered surface uses Charlotte Baglioni's CC0 Gravel Floor 03 PBR maps; Coast material and images are not embedded, while the raw bundle remains private and uncommitted |
| Hero Mountain | solararchitect / Sketchfab | https://sketchfab.com/3d-models/hero-mountain-83b3fd690ea44e988d086d5165a5f2ca; API https://api.sketchfab.com/v3/models/83b3fd690ea44e988d086d5165a5f2ca | Creative Commons Attribution / CC BY 4.0; http://creativecommons.org/licenses/by/4.0/ | 2026-08-29 | Original-format download obtained through an existing signed-in Edge session; private ZIP and inner RAR evidence plus selected `hero_mountain/Mesh_05K_hero_mountain01.obj`, `Color__hero_mountain01.jpg`, `Normal__hero_mountain01.png`, and `Roughness__hero_mountain01.jpg` build inputs | The complete mountain is decimated to one shared 14,000-triangle distant LOD and composed as 12 visual-only instances in staggered six-object inner and outer rings. Blender rebuilds its PBR nodes and the runtime export caps/packs selected textures at 512 pixels. Uniform scaling, rotation, and multi-instance composition are project modifications. The AO, height, and displacement sources remain private and unused. Attribution to solararchitect, source/license links, and an indication of these modifications are required; neither the source nor its adaptation is relicensed as MIT |
| Coastal Cliff 01 | Rob Tuytel (photography and processing), Rico Cilliers (cleanup) / Poly Haven | https://polyhaven.com/a/coastal_cliff_01 | CC0 1.0 Universal; https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-29 | Private 2K glTF bundle and official API snapshots retained as evaluation evidence | Evaluated but rejected for the final mountain silhouette; no geometry, material, or texture from this source is embedded in the delivered `.blend` or runtime GLB |
| Coastal Cliff 02 | Rob Tuytel / Poly Haven | https://polyhaven.com/a/coastal_cliff_02 | CC0 1.0 Universal; https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-29 | Private 2K glTF bundle and official API snapshots retained as evaluation evidence | Evaluated but rejected for the final mountain silhouette; no geometry, material, or texture from this source is embedded in the delivered `.blend` or runtime GLB |
| Namaqualand Cliff 02 | Dario Barresi (photography), Rico Cilliers (modeling) / Poly Haven | https://polyhaven.com/a/namaqualand_cliff_02 | CC0 1.0 Universal; https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-29 | Private 2K glTF bundle and official API snapshots retained as evaluation evidence | Evaluated but rejected for the final mountain silhouette; no geometry, material, or texture from this source is embedded in the delivered `.blend` or runtime GLB |
| Noto Sans SC Simplified Chinese subset variable OTF | Google Noto / `notofonts` contributors | Download mapping: https://github.com/notofonts/noto-cjk/blob/main/Sans/README.md; license: https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE | SIL Open Font License 1.1 | 2026-08-27 | `NotoSansSC-VF.otf` | DCC-authoring-only input converted to static Chinese glyph meshes; the original font is absent from the final `.blend` and GLB |
| Old Military Crate | Jack Mava / Poly Haven | https://polyhaven.com/a/old_military_crate | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-06 | `assets/models/old_military_crate/old_military_crate.gltf`, `assets/models/old_military_crate/old_military_crate.bin`, and `assets/models/old_military_crate/textures/old_military_crate_{arm,diff,nor_gl}_1k.jpg` | Repository-local source adapted into the current packed `.blend` and runtime `.glb` |
| Concrete Road Barrier | Amal Kumar / Poly Haven | https://polyhaven.com/a/concrete_road_barrier | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-06 | `assets/models/concrete_road_barrier/concrete_road_barrier.gltf`, `assets/models/concrete_road_barrier/concrete_road_barrier.bin`, and `assets/models/concrete_road_barrier/textures/concrete_road_barrier_{arm,diff,nor_gl}_1k.jpg` | Repository-local source adapted into the current packed `.blend` and runtime `.glb` |
| Coffee Cart 01 | Joe Seabuhr / Poly Haven | https://polyhaven.com/a/CoffeeCart_01 | CC0 1.0 Universal; https://polyhaven.com/license | 2026-08-28 | `assets/models/polyhaven_residential_street/CoffeeCart_01/`; exact file evidence is in `assets/models/polyhaven_residential_street/LICENSE.md` | Existing repository-local source adapted into the Jianghai market tea stall and packed into the `.blend` and runtime `.glb` |
| Wicker Basket 01 | Kuutti Siitonen / Poly Haven | https://polyhaven.com/a/wicker_basket_01 | CC0 1.0 Universal; https://polyhaven.com/license | 2026-08-28 | `assets/models/polyhaven_residential_street/wicker_basket_01/`; exact file evidence is in `assets/models/polyhaven_residential_street/LICENSE.md` | Existing repository-local source adapted into the Jianghai market tea stall and packed into the `.blend` and runtime `.glb` |
| Buildings Pack selections | Quaternius | https://quaternius.com/packs/buildings.html | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `assets/models/quaternius_buildings_pack/{building1-large,building3-big,building4,house2}.glb`; exact FBX mapping, source-page snapshot, license copy, and conversion evidence are in `source_art/third_party/quaternius_buildings_pack/` and `assets/models/quaternius_buildings_pack/README.md` | Existing repository-local authored buildings remain adapted into fourteen perimeter-density instances and three full street-cadence replacements; clean Building4 and Building3 Big bodies additionally underlie the current Chinese arcade-shop and gate-house shared meshes |
| Downtown City MegaKit selections | Quaternius | https://quaternius.com/packs/downtowncitymegakit.html | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-19 | `assets/models/quaternius_downtown_city/Brick_Plain_1.gltf` and `assets/models/quaternius_downtown_city/DoorFrame_Trim.gltf`; license copy and selection record are in `assets/models/quaternius_downtown_city/` | Existing repository-local finished modules embedded in the packed `.blend` and runtime GLB as 18 `Brick_Plain_1` instances and two `DoorFrame_Trim` instances: nine brick modules plus one doorframe at each of the pawnshop and factory hinged-entry facades |
| Factory Kit personnel door | Kenney | https://kenney.nl/assets/factory-kit | CC0 1.0 Universal; local copy `assets/models/kenney_factory_kit/KENNEY_LICENSE.txt` | 2026-08-27 | `assets/models/kenney_factory_kit/door-hinged.glb` and `door-hinged_colormap.png`; the derivative is built from the official archive's `door.glb` as recorded in `assets/models/LICENSE.md` | Current visible art for both Jianghai `InteractiveBuildingDoor` instances; each uses a human-scale 1.45-by-2.65-meter opening and a normal 96-degree side-hinged swing; this runtime door is separate from the static map GLB |
| Asphalt 03 | Charlotte Baglioni (photography), Dario Barresi (processing) / Poly Haven | https://polyhaven.com/a/asphalt_03 | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-06 | `assets/textures/asphalt_03_{diff,normal,rough}_1k.jpg` | Repository-local surface set adapted into the current packed `.blend` and runtime `.glb` |
| Concrete Floor | eye-candy.xyz / Poly Haven | https://polyhaven.com/a/concrete_floor | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-06 | `assets/textures/concrete_floor_{diff,normal,rough}_1k.jpg` | Repository-local surface set adapted into the current packed `.blend` and runtime `.glb` |
| Corrugated Iron | Dimitrios Savva (photography), Jenelle van Heerden (processing) / Poly Haven | https://polyhaven.com/a/corrugated_iron | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-06 | `assets/textures/corrugated_iron_{diff,normal,rough}_1k.jpg` | Repository-local surface set adapted into the current packed `.blend` and runtime `.glb` |
| Gravel Embedded Concrete | Charlotte Baglioni / Poly Haven | https://polyhaven.com/a/gravel_embedded_concrete | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-06 | `assets/textures/gravel_embedded_concrete_{diff,normal,rough}_1k.jpg` | Repository-local surface set adapted into the current packed `.blend` and runtime `.glb` |
| Kloppenheim 06 (Pure Sky) | Greg Zaal (Original), Jarod Guest (Sky Edits) / Poly Haven | https://polyhaven.com/a/kloppenheim_06_puresky; official 1K HDR download https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/kloppenheim_06_puresky_1k.hdr | CC0 1.0 Universal; https://creativecommons.org/publicdomain/zero/1.0/ | 2026-08-28 | `assets/textures/kloppenheim_06_puresky_1k.hdr`; 1,173,154 bytes; SHA-256 `206C67E3A1B992282821CF06662BDD69BBB4915C1C4444A66338A40D6A7D4E34`; official API MD5 `995d68b1656f26452572645c0ffe898b` | Retained repository-local CC0 evidence; not packed into the `.blend`, not embedded in the map GLB, and not loaded by the current procedural-sky `JianghaiOldCityAtmosphere` |

None of the CC0 sources requires attribution; creator names are retained as
provenance and courtesy credit. Hero Mountain is not CC0: CC BY 4.0 requires
credit to solararchitect, a source/license link, and an indication that the
delivered adaptation was modified through decimation, PBR-node reconstruction,
512-pixel texture capping/packing, uniform scaling, rotation, and
multi-instance valley composition. The raw Noto font is not redistributed. If a
future distribution includes the original or modified font software, it must
also preserve the copyright and SIL OFL 1.1 license notice and comply with the
OFL conditions. The project does not imply endorsement by any source creator.

`OldCityFoundation` is repository-original DCC geometry covered by the root MIT
license, subject to the project's AI-assistance disclosure. Its 96 source
vertices, 188 triangulated faces, applied hand chamfer, UV layout, material
split, placement, the composition and deformation of the single ground mesh
assembled from eight Coast Line 01 scan placements, and the
placement/orientation composition of 12 Hero Mountain instances are
project-authored.
The imported Coast geometry and Rocky Terrain/Gravel Floor 03 PBR data remain
CC0; Hero Mountain geometry and PBR data remain CC BY 4.0. Coast source
materials and images are not embedded. Packing any contribution into the
project-authored composition does not relicense it as MIT. No displacement map
generates delivered visible geometry.

The Poly Haven source pages identify the creators and contributor roles
recorded above and display the CC0 license. VVayToyek's official itch.io page
states CC0, identifies Blender as the authoring tool, and marks generative AI as
not used. The six delivered BlenderKit pages display their named creators and
"Creative commons zero"; for the four
assets acquired on 2026-08-28, the official API also reported
`license=cc_zero` and `isFree=true`. Attribution is not required for these CC0
sources, but creator names and identifiers are retained as provenance. The
Noto Sans CJK repository contains the SIL Open Font License 1.1 text and
directly maps the Simplified Chinese subset variable OTF filename used during
DCC authoring.

## Acquisition hashes

| Acquisition input | Bytes | SHA-256 |
| --- | ---: | --- |
| `blenderkit/chinese_red_lamp.glb` | 277,956 | `AE9E086D70F4566EC6003C7C6A2BB48140DC6A244F10B3D34B9A4823845593D9` |
| `blenderkit/chinese_temple_2.glb` | 17,248,492 | `25A79EC6C5180C11D8A1335D5229F628906EA4BCEAB71CCF1B6A0EBBCB9C5C9A` |
| `blenderkit/old_urban_building_1k.blend` | 982,731 | `002AF11B135A76DBD6EFB157DFE5A482F4D2BCF320E9A1B30C239A1199DF1538` |
| `blenderkit/scan_old_building_street.glb` | 17,198,872 | `E4686F1FC21A18828F73A81072CD9735E1DF9BD400995DB538B210589742D97E` |
| `blenderkit/chinese_porcelain_lion.glb` | 3,765,324 | `04928FF357D07EFF63CA5326D7B3988C32BCB527FFDB4408C6CED60EEEF3A41D` |
| `blenderkit/pink_city_bicycle/pink_city_bicycle_0_5k.blend` | 4,138,682 | `F7AE33ACCDE2EC56AF3BD4C0A45F46658E816C8E3000DF29BF5BAB4B6CA53AA2` |
| `blenderkit/pink_city_bicycle/official_api_search.json` | 14,880 | `83473363A4CCC0AE156B8C2F88C5059F16486740C38717FE692A8869AD262758` |
| `itch/vvaytoyek_chinese_four_corner_pavilion/Chinese Four-corner Pavilion.zip` | 2,777,866 | `008FB5C4508CE57C4203E5763FD01A177DED16CA94FD5DE34EA26C422B483EFA` |
| `itch/vvaytoyek_chinese_four_corner_pavilion/fbx_only/四角亭.fbx` | 2,040,044 | `E5A745FCEEF8EB0E4B45230E4D964F618C64AB0981F098E1E329C982C159E89C` |
| `itch/vvaytoyek_chinese_four_corner_pavilion/itch_license_evidence.html` | 24,086 | `F3AC17F9DE33C21C3780623844C868EBDAFA0FB4C4248905716E3A93688A49E0` |
| `NotoSansSC-VF.otf` | 15,054,748 | `D13ED01EC8AA45D6178999B648E96FB92150683E9F8E2A581F2ACF208DCBE44B` |
| `poly_haven/modular_factory_facade/modular_factory_facade_1k.gltf` | 372,666 | `41FF58B722F9CB3E9DB8BCACAE3163946603AE99A1D42BB6F9045D7C4EC0F179` |
| `poly_haven/modular_factory_facade/modular_factory_facade.bin` | 6,269,260 | `12702FB065A838695E0621E1AAC3947AF92D32C439FB965C8B0563AD691FED70` |
| `poly_haven/modular_urban_apartments_facade/modular_urban_apartments_facade_1k.gltf` | 270,799 | `1A5A17DFFD27FB9E1236DEA7E51C4E0393A9D88ED0885A0621E67A37F80B27EB` |
| `poly_haven/modular_urban_apartments_facade/modular_urban_apartments_facade.bin` | 8,168,772 | `F041E71B6BD31864F20DD64D3A82519454A603C300686161E0349FF44F838BF2` |
| `poly_haven/television_02/television_02_1k.gltf` | 2,767 | `8E7D7CB6BBDB5713B7D4ED21ECE7C0F9C41912359C2C0F5065B7E77F5BF4CDCF` |
| `poly_haven/television_02/television_02.bin` | 81,668 | `73F1D3519F7BDACCA24C892E2614FF756D5D27F5C33FB518CB8AAC9154FA710B` |
| `poly_haven/exterior_aircon_unit/exterior_aircon_unit_1k.gltf` | 9,634 | `F19D85C76948903047C2846068AEAA376D5E956A410675268CB6CB6AAC5D97C2` |
| `poly_haven/exterior_aircon_unit/exterior_aircon_unit.bin` | 494,784 | `B4B9AD082BDAA8F8B437BC14D9981CAEAF318334499D4BF65D616FB2EC0C5CA8` |
| `poly_haven/rollershutter_window_03/rollershutter_window_03_1k.gltf` | 4,779 | `FE535AE92674B69D333B398888A2296BD4435ADE730977A6B0C9FBE76FA47ED0` |
| `poly_haven/rollershutter_window_03/rollershutter_window_03.bin` | 46,240 | `1FC1FA40BE768A333A10172E0E95480B05064F3E71DD0CFF92FB5850C7210A00` |
| `poly_haven/trashbag/trashbag_1k.gltf` | 2,738 | `22202E1EC48D924577584DCE6751D5AB198FD7C0AEE44602E9A7317B5B60A4A7` |
| `poly_haven/trashbag/trashbag.bin` | 106,316 | `7BFAC5B9C39A058AF347944B437F712718EC129EC883C16339179862CC155FE2` |
| `poly_haven/utility_box_01/utility_box_01_1k.gltf` | 2,766 | `5B9F8C45F2640C9DD831DC3450529E45B24C987208DDCF73010FE717CE8C454E` |
| `poly_haven/utility_box_01/utility_box_01.bin` | 142,552 | `E4249EB754D7802154EED36BB12070DC24C90FC331F355D60EF7570D61FC0F5C` |
| `poly_haven/barrel_03/barrel_03_1k.gltf` | 2,638 | `80E2FAF48B7423BB522B573E459D99C5656A16763285EC8268E957FC7D41F9B8` |
| `poly_haven/barrel_03/barrel_03.bin` | 44,872 | `10F3202E9AC9ACF8DD896F1562C64DB58311FFEF89EFB402D8433E03AD486427` |
| `poly_haven/plastic_crate_02/plastic_crate_02_1k.gltf` | 2,947 | `531F7BA7D4B501759B04FB704DB1948BCAAB34D8342E71E3F5877D9F0340DBA7` |
| `poly_haven/plastic_crate_02/plastic_crate_02.bin` | 186,848 | `F51696AA04643B5C1948DA88BE8CE1D4E92596AE4908B0132CD690F8FB8E0B63` |
| `poly_haven/security_camera_01/security_camera_01_1k.gltf` | 4,743 | `D2468FE353CD9F992A5549709CF9EFCFAE67E9FD17F80BF7FF3866E2881DD2AE` |
| `poly_haven/security_camera_01/security_camera_01.bin` | 386,836 | `C43DD3576213C169D1B3A9968788C0DA2D14073E59C7D2503BC230C05042BB21` |
| `poly_haven/chinese_tea_table/chinese_tea_table_1k.gltf` | 2,793 | `4C1987AA1978C4FE1DFD0F5F10739DF399346FC4C9373315D8D4B0CC798D8B01` |
| `poly_haven/chinese_tea_table/chinese_tea_table.bin` | 78,792 | `E584DBCF8D654C662EF849EF6111B11DC9F87E9A7EA8DC98C7D32DBFE10B7E52` |
| `poly_haven/chinese_stool/chinese_stool_1k.gltf` | 2,883 | `6DB83B16076496830F956780890480D3CB1E34C80359B5EA8CB0E7AFD42572C6` |
| `poly_haven/chinese_stool/chinese_stool.bin` | 40,172 | `E35C9440FC0751AA9346E6EDBFE0BF948F0F7DD1B40BE2EF57D0B6DEBEC5B8AF` |
| `poly_haven/hand_truck/hand_truck_1k.gltf` | 2,754 | `303029BA829F2975EBED80F6F36E38D57CEC87379E7214FCFB7D8E6E921C8E58` |
| `poly_haven/hand_truck/hand_truck.bin` | 535,704 | `2A7CA50CDFAC207DEFA68F71600F37E7CDD05D35BC94139DF5269EA7D8B7A214` |
| `assets/textures/kloppenheim_06_puresky_1k.hdr` | 1,173,154 | `206C67E3A1B992282821CF06662BDD69BBB4915C1C4444A66338A40D6A7D4E34` |

### Valley acquisition and license evidence

The following paths are relative to `JIANGHAI_VALLEY_ACQUISITION_ROOT`. The
Poly Haven MD5 values match the download records in Poly Haven's official files
API; the Hero Mountain MD5 values identify the selected files extracted from
the retained original-format archive. The final valley build script verifies
the 17 selected private Coast Line 01, Hero Mountain, Rocky Terrain, and Gravel
Floor 03 authoring inputs before it modifies the authoritative scene. Coastal
Cliff 01, Coastal Cliff 02, and Namaqualand Cliff 02 rows remain below as
evaluated-only private evidence and are not build inputs or delivered content.

| Acquisition input | Bytes | SHA-256 | Official API MD5 |
| --- | ---: | --- | --- |
| `coast_line_01/coast_line_01_2k.gltf` | 2,820 | `7BCCFF2E6888782F447BF93915236C2CD5113473439427616598AA78E06DC998` | `bae8b0b77271b1d3c9cc50a710fbce02` |
| `coast_line_01/coast_line_01.bin` | 15,197,348 | `BC718C04D8FE6D9F305EA99B1DE9EC91AB254D02CF2491255DC5D2CD9F16DD8E` | `ce691184592e23391d202f37a13c9b97` |
| `coast_line_01/textures/coast_line_01_arm_2k.jpg` | 2,971,370 | `F9685DBB5D849D00CA1789EA8BFAF9177554106034B2907B590B33F0C4733A98` | `ce69b67920b01875421c8e67e28f0012` |
| `coast_line_01/textures/coast_line_01_diff_2k.jpg` | 2,788,979 | `958D85C53EA58081357C248544F65914D825ABEC09DFCD23A2178E2B6A959D98` | `cc178fb4bcca93110b98037c4c55d5c4` |
| `coast_line_01/textures/coast_line_01_nor_gl_2k.jpg` | 4,118,881 | `5B88AEE85B0D79F9A5B096614FAD794D74C600E423A64CE7C0B5BF80152578C9` | `03a6f2d453ee92b2d42b9781f8d31e80` |
| `hero_mountain/Mesh_05K_hero_mountain01.obj` | 37,306,505 | `E3DD992D95CDD3DF43D45531BACB6EBD2AE3F6AFB1AF794B1DCF1F70C7AE798D` | `af949f14c8fb8138bf75f2a70769b2be` |
| `hero_mountain/Color__hero_mountain01.jpg` | 2,030,226 | `AB9CDBA09AD505D61CDF19AC20327D6966F0BCC3F8E236090E53D2F9A544CA83` | `1480eb4cadc8c531055b0b39ea5ab50d` |
| `hero_mountain/Normal_hero_mountain01.png` | 16,446,201 | `DA1646D4042308EB1ED588AFF8B23490A3585B1E23B7B142C20C8F288B18F290` | `7f16993db123397c80fcec42e586729b` |
| `hero_mountain/Roughness__hero_mountain01.jpg` | 4,804,305 | `DB3791D28F7AC971299BE9104837F5ECFE3171CC77A184FE4946BB4EAF9F5338` | `e46afb87a2dbe6c2843eb14864245ffe` |
| `coastal_cliff_01/coastal_cliff_01_2k.gltf` | 2,825 | `8E82762AE04616A4C383CF9B91EE42D01852C07445C7B66FCC199FC635024D47` | `b740b84cdc6d7acb5274361b594f5e3a` |
| `coastal_cliff_01/coastal_cliff_01.bin` | 13,179,840 | `6833DFAF75E0D039E64BC09AD642C4BE3361739F20048AF32AD50FD8E6471A65` | `d9f1e368b18c018e158d06ab9d924646` |
| `coastal_cliff_01/textures/coastal_cliff_01_arm_2k.jpg` | 2,181,608 | `1627B66D30B7A4B557856DAA77A0D6F3D62BD722F3D79FA7A0B82FA9F03E0BD3` | `b4c23c612a9c38fa794d05a67857a645` |
| `coastal_cliff_01/textures/coastal_cliff_01_diff_2k.jpg` | 2,465,846 | `FF7B68EC580561F0C5ABEB3B269D746428F69260FC353E637A3CC69953A736B6` | `9e1adf2e9f996d85c40f7dde57a97de8` |
| `coastal_cliff_01/textures/coastal_cliff_01_nor_gl_2k.jpg` | 3,054,804 | `FEA5DB5EA6E3AE3F8DCAEB759B8A2395685C92F460E2F30C8EDAEF7216B1C027` | `54f0fed4e2ee6893c8c114c4ec1bf66a` |
| `coastal_cliff_02/coastal_cliff_02_2k.gltf` | 3,220 | `6A2A3E01AF4479A298F82CEFE9AF4FE44A44C2300B2FB1B3ADA1053A28C35FCC` | `eafa7dd7437be09a065850624871b942` |
| `coastal_cliff_02/coastal_cliff_02.bin` | 26,773,008 | `6717F1E620F71D2085A06BE3ECC46C457D99780E4E3C9518F7D4CE55982B8FA7` | `e6324e54479fd0f9aefe707dfa9e728b` |
| `coastal_cliff_02/textures/coastal_cliff_02_arm_2k.jpg` | 2,299,007 | `0AA3A5DC63DF835A60CDA51839D89FB83A7F5FBE6588D32569407C43F7C25C62` | `cc6ccd0cc6613a44a23fdb070c7395b5` |
| `coastal_cliff_02/textures/coastal_cliff_02_diff_2k.jpg` | 2,655,313 | `F2D65DFC3480EE2DF23F47D768AA2A285CF1CD1A1B8BE5173AA333F8ECE854B8` | `42b243a7d28f370b4a6ac050ea16d615` |
| `coastal_cliff_02/textures/coastal_cliff_02_nor_gl_2k.jpg` | 4,057,806 | `98F4D74F4F073C2A3B57B736FD30638381CC354A3955284BA4AA094D381086D1` | `fff741bb05b8107d829a10af63096730` |
| `namaqualand_cliff_02/namaqualand_cliff_02_2k.gltf` | 2,857 | `42B5AA0C33E4AAB50DD04F01DC0B24A57E48043EA16CEFEB8A7292E243A05630` | `599ea12c5a126d7955bcd67a82407a76` |
| `namaqualand_cliff_02/namaqualand_cliff_02.bin` | 5,697,152 | `DA05A038B3A1F3B46CD8CF074D20B8D3E2B2148D30806E1FEB0309E941200595` | `2920477744cce1a827a61c0d36a1061a` |
| `namaqualand_cliff_02/textures/namaqualand_cliff_02_arm_2k.jpg` | 2,846,951 | `3B4D733BE37186AB6B62B8DDDE90F9DC439F5B42A17293F1B65844A0F0D9E02F` | `b145f4a74a776f8e766411304ecbd2de` |
| `namaqualand_cliff_02/textures/namaqualand_cliff_02_diff_2k.jpg` | 2,845,716 | `9618E2A42155B93189E0BAC9EDEBCC46A6AF1357F4F6ED56F79CA1714690D4F0` | `45a8861aebda11935b29f1cedf6f800d` |
| `namaqualand_cliff_02/textures/namaqualand_cliff_02_nor_gl_2k.jpg` | 4,125,317 | `634BC52174130D6992A7539D1F7BB923C4A103F117AB09966782C0E89EB571F6` | `7412c300ddaff0613dade74d1b8760e7` |
| `rocky_terrain/textures/rocky_terrain_diff_2k.jpg` | 3,287,952 | `B6F927896AFEAF7F39FC54DBE08DE6A4198CCC68E7E996EAECE00E2D06F21261` | `4abb5d65394b6af07752099bd34ddd02` |
| `rocky_terrain/textures/rocky_terrain_disp_2k.png` | 6,145,255 | `97A8A6432F2129B2C9BC395B92B09C9A420299098353D3CCFCF2B23F9BFD81B1` | `8146d9555199ee5ca526d2346d97df45` |
| `rocky_terrain/textures/rocky_terrain_nor_gl_2k.jpg` | 4,149,933 | `72031698AA65FE0ED7CAABD3ECEF02AC09BB5B23E6EA262FDAD7A2F53F874342` | `05034535c6a4d24bf1886bd6331b9d39` |
| `rocky_terrain/textures/rocky_terrain_rough_2k.jpg` | 1,660,907 | `8BFC602C2933A69E84EDA2E0EC0C42FA2414956E8D54E47AB1922E264DD4FCFD` | `e773e576ac20318199c85ca84abfe2fe` |
| `gravel_floor_03/textures/gravel_floor_03_diff_2k.jpg` | 3,635,501 | `06465933A54619900230FE85BFF4C29F83FD3237A76B600E9845DEF4A206AECE` | `d86981602e03f8f1deeccc5e37a14468` |
| `gravel_floor_03/textures/gravel_floor_03_disp_2k.png` | 7,428,432 | `600BE33464CB46EAEFA9DE6105D645BEE340E00F2ECBC54C52BB5CE38DF7903E` | `d6bc2d30510434f80f725baf72b215a7` |
| `gravel_floor_03/textures/gravel_floor_03_nor_gl_2k.jpg` | 6,132,167 | `27367A648385DB57D6650C2A52C993245BE084C1CA57686BA44A52925187D491` | `864d073353dcfbbb0a507cbc07e250b7` |
| `gravel_floor_03/textures/gravel_floor_03_rough_2k.jpg` | 962,465 | `4CEE65A1DF8B7C919DF83D6A7B9A79FBF6A5A53F08E1EEDE2A3A4ECF5ECE9A17` | `698b4d00999fa3108d4abc8584dde936` |

The Hero Mountain source-evidence cache preserves the original-format download
chain separately from the four selected Hero Mountain build files above:

| Retained Hero Mountain evidence | Bytes | SHA-256 | Role |
| --- | ---: | --- | --- |
| `hero_mountain/hero-mountain-original.zip` | 89,828,796 | `9D9FE2E2C0CFF01600B6347F67AB414AF940F79EA68A49E69371BB7948310399` | Original-format Sketchfab download obtained through the existing signed-in Edge session |
| `hero_mountain/original/source/Hero_Mountain_solar_architect.rar` | 56,140,154 | `65E5E44EB7B94C2CD58114CE9F5B15959CCD528AD63F6C4AD13BB7EDF5423C10` | Inner source archive retained without modification |
| `hero_mountain/sketchfab_model_api.json` | 5,941 | `C0D48CB7218B45D7868AC661F335EA20DFEF27C868DB3AD64A602FD62603190C` | Official model API snapshot; records `faceCount=522242`, `vertexCount=262144`, `downloadable=true`, creator `solararchitect`, publication date 2021-10-21, and CC Attribution license metadata |
| `CC-BY-4.0-legalcode.txt` | 18,657 | `9BA9550AD48438D0836DDAB3DA480B3B69FFA0AAC7B7878B5A0039E7AB429411` | Retained Creative Commons Attribution 4.0 legalcode snapshot |

The private source archive also preserves Ambient Occlusion, height, and
displacement maps. They are intentionally omitted from the delivery-input table
because the final Hero Mountain material and geometry do not use them.

Private-cache evidence acquired on 2026-08-29 retains both the official asset
metadata response and official files response for every evaluated Poly Haven
scan model:

| Asset | Info API snapshot | Files API snapshot |
| --- | --- | --- |
| Coast Line 01 | `polyhaven_coast_line_01_info_api.json`; 1,102 bytes; SHA-256 `5E1C8A27BA3E73F8D6EBC019AB1DD4867361C5CE67CFCCE80BEF1261398B16BE` | `polyhaven_coast_line_01_files_api.json`; 25,434 bytes; SHA-256 `8063AE73438C683D7CA1E6681BFD616BF56C9D5F4D1FB4A5DE226BC939901DF2` |
| Coastal Cliff 01 | `polyhaven_coastal_cliff_01_info_api.json`; 1,150 bytes; SHA-256 `D74467BB6DF067191161E39A55D743331780054870734189A5E13DB268D1F722` | `polyhaven_coastal_cliff_01_files_api.json`; 26,422 bytes; SHA-256 `03FA4B1AC0AA23D6027F873EE56347CF8DE7786EB5C941ABFE572D9DACDC7D6D` |
| Coastal Cliff 02 | `polyhaven_coastal_cliff_02_info_api.json`; 1,067 bytes; SHA-256 `C3D8500D83661008413ECABCCEA82405AAA138273B47BA4BED5410A8BA5C0944` | `polyhaven_coastal_cliff_02_files_api.json`; 26,434 bytes; SHA-256 `3F444D7F730AC9E7DA960F78A11AF86E892067FCDB04B0865C36EC60C6D86EB7` |
| Namaqualand Cliff 02 | `polyhaven_namaqualand_cliff_02_info_api.json`; 1,118 bytes; SHA-256 `E5F951D545C379492B898BB970751BAEFF7BE5FEDF4D82CB57CADA88BCE53C07` | `polyhaven_namaqualand_cliff_02_files_api.json`; 29,955 bytes; SHA-256 `439C0F7A9CFBA92F5F403B9E25F58AC0369A89BA01D2248EB887111FB3DBABE0` |

The 20 Poly Haven model records in the acquisition table match the byte lengths
and MD5 values published in those retained files API responses. All five Coast
Line 01 records are verified as private authoring inputs, but only the glTF/bin
geometry contributes to the delivered ground; its three source images and
material are not embedded. The 15 cliff-source records are retained evaluation
evidence. The 2026-08-28 surface evidence also retains the
https://api.polyhaven.com/files/rocky_terrain response
(`polyhaven_rocky_terrain_api.json`, 26,200 bytes; SHA-256
`7406A157505FC2EE49E7FD09F1EF94E87D3918605B3A0899429A97E818D74140`).
The retained https://polyhaven.com/license snapshot is
`polyhaven_license.html` (71,934 bytes; SHA-256
`030F403BA3B1D303D585CB2A05D965A5A9FFD548FD9A6845F7DAA9310C7E225D`)
and states that Poly Haven assets are CC0 and may be redistributed without
attribution. The exact CC0 legal instrument is
https://creativecommons.org/publicdomain/zero/1.0/.

Gravel Floor 03 was cross-checked directly against the official
https://api.polyhaven.com/files/gravel_floor_03 response. Its four selected 2K
records map to these official downloads and match both the stated byte lengths
and MD5 values in the table above:

- https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/gravel_floor_03/gravel_floor_03_diff_2k.jpg
- https://dl.polyhaven.org/file/ph-assets/Textures/png/2k/gravel_floor_03/gravel_floor_03_disp_2k.png
- https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/gravel_floor_03/gravel_floor_03_nor_gl_2k.jpg
- https://dl.polyhaven.org/file/ph-assets/Textures/jpg/2k/gravel_floor_03/gravel_floor_03_rough_2k.jpg

For the Poly Haven bundles, a deterministic bundle digest was also calculated
from every file sorted by relative path. Each manifest row is
`relative/path|byte-length|lowercase-file-sha256`, joined with LF and terminated
by LF before hashing as UTF-8:

| Bundle | Files | Bytes | Manifest SHA-256 | Status |
| --- | ---: | ---: | --- | --- |
| `modular_factory_facade` | 17 | 17,626,179 | `E153A2BF001C517C6C0A7C5B37C8A223C734147ADF07949D722D79A592568D23` | Delivered source |
| `modular_urban_apartments_facade` | 14 | 16,488,693 | `27A46C87BE8D073F642DE3E5CA8AD7185D4DCA1377796CAF570E91105E87472C` | Delivered source adapted into 36 facade objects in the packed `.blend` and GLB |
| `television_02` | 5 | 1,239,955 | `7468FF9CBE3054DF27CEC531CD871C5DD221147144DE37BE4A9A509221F96827` | Delivered source packed into `.blend` and GLB |
| `exterior_aircon_unit` | 14 | 7,667,928 | `A2706A633554EF4960A78BF6B152A246A00A8D0ADA10E8245A8486767AD58AC0` | Delivered source packed into `.blend` and GLB |
| `rollershutter_window_03` | 6 | 2,283,373 | `5EF72962E7C4214475AA076B62604A7300C1F6FF7F0824A7E416E221D52B9D82` | Delivered source packed into `.blend` and main GLB, with a retained standalone alternate/legacy shutter derivative |
| `trashbag` | 5 | 1,699,464 | `612CB0B526A3771F83C9359439D24938478D1351E78A59CE1B4FCF1EAD90AA86` | Delivered source packed into `.blend` and GLB |
| `utility_box_01` | 5 | 1,977,061 | `D1868A98D66CF3ABC57277240E0AE82B3A2924AF46731CE017BFEFAEA426D978` | Delivered source packed into `.blend` and GLB |
| `barrel_03` | 5 | 574,136 | `B381E09529939827DB81C28367034EC67EA7B38253E66226141935E2415DF311` | Delivered source packed into `.blend` and GLB |
| `plastic_crate_02` | 5 | 1,838,779 | `435D821E078576A51AF9BA070CDB86FC7A11A50092069EA50256741E152168AF` | Delivered source packed into `.blend` and GLB |
| `security_camera_01` | 5 | 1,759,323 | `7BDF27951E3F8F5E232AFA008E33156CA1804989C6B81676982C97E555E5D5B5` | Delivered source packed into `.blend` and GLB |
| `chinese_tea_table` | 5 | 1,581,290 | `6B7C53C110421A3D27DF581768DF367505433C4B821C5F0BEBABE475A3CD1E9A` | Delivered source packed into `.blend` and GLB |
| `chinese_stool` | 5 | 1,793,116 | `2FC56EAC8BA42C1B83C08CEA07180F2B706D1F11C504F22482E2B975A1C93898` | Delivered source packed into `.blend` and GLB |
| `hand_truck` | 5 | 3,179,347 | `D4E33B9CD7B38811284E6846CAE61AC3FA4683D997AD32F1529DB6484325493D` | Delivered source packed into `.blend` and GLB |

## Historical verified hashes and current audit ownership

The earlier rows below are immutable historical snapshots. The final two rows
record the current 2026-08-29 Chinese-district delivery.

| Output | Bytes | SHA-256 | Reproducible derivation and use |
| --- | ---: | --- | --- |
| `source_art/world/jianghai_old_city/jianghai_old_city.blend` — upstream pre-valley snapshot | 61,677,884 | `C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7` | Packed DCC source before the valley rebase; includes the 15-part VVayToyek pawnshop canopy, 16 solid facade/insert wing objects around the original large opening, aligned five-object factory-gate portal, two ten-piece Quaternius hinged-entry facades, delivered urban-life/facade expansion, 36 complete six-profile perimeter buildings, four full-mesh street-cadence replacements, and five-building factory replacement |
| `assets/models/jianghai_old_city/jianghai_old_city.glb` — upstream pre-valley snapshot | 73,809,716 | `2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A` | Runtime map exported by `scripts/blender/export_jianghai_old_city.py`; contains the 20 finished Quaternius entry-facade modules but not the separately instanced Kenney interactive doors |
| `source_art/world/jianghai_old_city/jianghai_old_city.blend` — valley pre-rebase snapshot | 74,037,661 | `C9BAC433CF77791B3730E309A5E0BEEF6CF4849593D44018FD2CDFE5AC8FAA08` | Packed DCC source before integration with the upstream hinged-entry snapshot; includes the prior city art, project-authored `OldCityFoundation`, single 168,480-triangle Coast-Line-derived ground composite using Gravel Floor 03, and 12-instance staggered two-ring Hero Mountain composition built by `scripts/blender/build_jianghai_valley_environment.py`; builder and read-only audit PASS; the Hero Mountain contribution remains CC BY 4.0 and is not relicensed as MIT |
| `assets/models/jianghai_old_city/jianghai_old_city.glb` — valley pre-rebase snapshot | 76,862,308 | `0C0174672630957390A959BC3BD71DB3F4849CC7CABE0AFADFDD12273DFE02A5` | Runtime map before integration with the upstream hinged-entry snapshot; export and DCC-to-GLB round-trip PASS; 4,835,033 evaluated full-scene instance triangles, below the 5,000,000 gate |
| `source_art/world/jianghai_old_city/jianghai_old_city.blend` — historical post-valley/pre-Chinese-rebuild delivery | 81,861,168 | `7CA84CD2B17C3872323D8A5EE7B1A4BA5BCB360F4326FB2331327BED4F493461` | Historical combined packed DCC source; 500 mesh objects, 198 unique mesh datablocks, 4,807,899 raw mesh-object triangles, 1,003,869 triangles counted once per unique mesh, and 563 evaluated objects totaling 4,836,825 instance triangles; superseded by the 2026-08-29 Chinese district rebuild |
| `assets/models/jianghai_old_city/jianghai_old_city.glb` — historical post-valley/pre-Chinese-rebuild delivery | 84,723,312 | `7E2BB712BCF031692FAFB0E4E0FA59F3E75CE340B2748F5EDBDB7B105D9B2965` | Historical combined runtime map; superseded by the 2026-08-29 Chinese district rebuild |
| `source_art/world/jianghai_old_city/jianghai_old_city.blend` — current Chinese-district delivery | 42,607,105 | `97226E2ED4860E676F27171F7AEF76B33AFF493AD991779887BE984B5DCF9F17` | Authoritative packed DCC source; 66/66 visible anchors replaced, 42/42 density placements, six west/east `Edge04`-`Edge06` placements, zero density intersections, and zero visible retired-building instances; read-only audit PASS |
| `assets/models/jianghai_old_city/jianghai_old_city.glb` — current Chinese-district delivery | 49,926,284 | `BAD4B6C18C8FC8488419ED9EB06F18F6C34544FEAC054EF71555F0D5EB2C0433` | GLB round-trip PASS; 263 unique meshes/569 mesh nodes, 378 unique/1,517 instanced surfaces, 943,282 unique/3,015,841 instanced triangles, 93 materials, 142 textures/120 images, maximum image dimension 512 |
| `assets/models/jianghai_old_city/rollershutter_window_03.glb` | 187,940 | `C4884AFCD7560E4BB23320A8C311DB0011504F7C5FEE30D58C266D54F7C6B166` | `scripts/blender/export_jianghai_old_city.py` selects the packed scene's adapted `JianghaiArtPass_EastShutter00` mesh, normalizes a temporary copy, and exports its PBR geometry and materials; the derivative remains tracked but no longer supplies the current Jianghai interactive-door visuals |

This retained derivative preserves the Rollershutter Window 03 provenance and
MP / Poly Haven CC0 1.0 Universal license recorded above. It is no longer
instanced by either current Old City entrance. Both entrances now use Kenney's
CC0 `door-hinged.glb` at a 1.45-by-2.65-meter runtime opening with a normal
96-degree side swing. Door collision, animation, network state, and AI
traversal remain project gameplay behavior and are not derived from either
visible-art source.

The pre-Chinese rows remain historical evidence; the two current-delivery rows
are the authoritative 2026-08-29 artifacts.

## Runtime and editable mapping

The authoritative editable source is:

- `source_art/world/jianghai_old_city/jianghai_old_city.blend`

The valley is a saved DCC authoring pass, not runtime geometry generation.
`scripts/blender/build_jianghai_valley_environment.py` opens the authoritative
scene, requires `JIANGHAI_VALLEY_ACQUISITION_ROOT`, verifies every raw input
against the recorded MD5 values above, updates the existing authored
foundation, assembles eight modeled Coast Line 01 scan placements into the
single `JianghaiPerimeterGroundComposite`, composes the Hero Mountain instances,
rebuilds the selected PBR materials, assigns Gravel Floor 03 to the visible
ground, caps packed images to 1024 pixels, runs deterministic geometry and
rights-metadata gates, and saves the result back into the `.blend`.

The final ground topology gate requires exactly one 84,960-vertex,
168,480-triangle mesh, one connected component, two boundary loops totaling
1,440 edges, zero degenerate triangles, and zero invalid face normals. Bounds
must be X `-600.878..600.853`, Y `-540.340..660.056`, and Z
`-12.7965..5.0390` meters, for 17.835 meters of relief. The material gate
requires Gravel Floor 03 diffuse, OpenGL-normal, and roughness textures,
base-color factor `(0.92, 0.78, 0.62, 1.0)`, and 7-meter affine world-XY UVs.
Maximum DCC/GLB coordinate errors are `3.27e-6`/`4.36e-6`, within the
`1.2e-5` gate, and both Jacobian checks pass. Coast material/image counts are
0/0.

The continuity gate verifies real authored ground geometry, not camera masking
or a primitive substitute. It derives signed distance from the actual projected
top footprint of `OldCityFoundation` (25 top faces, 32 vertices, 16 boundary
edges; X `-169.998..169.998`, Blender Y `-99.998..219.998`), keeps the
footprint and margin beneath the platform, and blends Coast relief outward.
Coverage is 1.000, maximum foundation gap is 0.103 meters, safe-area top is
-0.120 meters, 0-60/60-160-meter relief is 0.969/3.955 meters, and slope
RMS/p90/p99/maximum is 0.0579/0.0869/0.2331/0.6620. The ring gate passes
7,920/7,920 probes.

The final DCC pass also tapers and buries the north-end caps of
`AuthoredStreetNetwork/CentralAvenueCurbW` and
`AuthoredStreetNetwork/CentralAvenueCurbE`. DCC and GLB ray gates pass
330/330 north and 90/90 south top hits with zero side hits. The builder,
read-only scene audit, export guard, and serialized-GLB round-trip compare the
single ground, both indexed six-object mountain rings, material/rights metadata,
mountain burial (minimum 4.942 meters), and every recorded gate above. The raw
Coast Line 01, Hero Mountain, Rocky Terrain, and Gravel Floor 03 downloads
remain only in the private cache; they are not committed or copied as standalone
runtime sources. Coastal Cliff 01, Coastal Cliff 02, and Namaqualand Cliff 02
remain private evaluation evidence and are not embedded in or required by the
delivered artifact. No displacement-generated visible geometry is created.

Composition, modeling, material, lighting, and sign changes are serialized in
that packed Blender scene. The deterministic DCC export/cleanup script
`scripts/blender/export_jianghai_old_city.py` validates the 66-anchor Chinese
replacement, the explicit 42-placement density table and six west/east
`Edge04`-`Edge06` placements, rejects both retired building sources plus legacy
pawnshop boards and zero-thickness walls, validates the authored canopy and
wings, then exports it
to:

- `assets/models/jianghai_old_city/jianghai_old_city.glb`
- `assets/models/jianghai_old_city/rollershutter_window_03.glb`

The export policy caps the longest runtime-texture dimension at 512 pixels
and recompresses eligible high-resolution runtime images as JPEG quality 90
before packing. The two visible objective-terminal bodies combine the CC0
Utility Box 01 and Television 02 sources; their small status screens and
adaptation work are project-authored in the authoritative `.blend`.

The same export step rebuilds two serialized ten-piece entry facades from the
repository-local Quaternius Downtown City MegaKit sources: 18 instances of
`Brick_Plain_1.gltf` and two instances of `DoorFrame_Trim.gltf`, divided as nine
brick modules and one doorframe per entrance. The export script does not
reconstruct the map from acquisition-cache files or generate runtime procedural
city geometry. It deterministically validates the reviewed Chinese-profile
shared meshes, explicit non-random transform table, sign cleanup, material
tuning, and export policy. The packed `.blend` remains the authoritative
serialized DCC source.

The earlier delivered Poly Haven bundles remain in the external cache as 1K glTF,
`.bin`, and texture sidecars. Their adapted geometry, materials, and textures
are packed into the authoritative `.blend` and then exported inside the runtime
GLB; the repository does not need the external raw bundles to reproduce an
export of the saved scene. The Modular Urban Apartments Facade is included in
that delivered set as 36 adapted facade objects. Those earlier delivered inputs
are CC0. The valley's separate private cache retains the Coast Line 01 2K glTF
bundle, the selected Rocky Terrain and Gravel Floor 03 maps, the Hero Mountain
original-format download/evidence chain and selected build inputs, and the three
evaluated-only cliff bundles. The delivered valley contains adapted CC0 content
plus solararchitect's adapted Hero Mountain under CC BY 4.0; it contains no raw
standalone acquisition payload. Coast Line 01 contributes geometry only, while
the delivered ground surface uses the Gravel Floor 03 material and images.
Hero Mountain's required attribution and modification notice remain applicable
to the packed scene and runtime GLB.

The repository-local Kloppenheim 06 (Pure Sky) HDRI is retained CC0 evidence.
It is not an acquisition-cache dependency, is not packed into the authoritative
`.blend`, is not embedded in the runtime map GLB, and is not loaded by the
current procedural-sky `JianghaiOldCityAtmosphere`.

The Pink city bicycle acquisition remains in the external cache with its
official BlenderKit API response. The delivered DCC source contains only the
adapted static rest-pose geometry and materials: its armature and animation
scaffolding were removed, the mesh was cleaned to 11,825 triangles, and three
instances were placed by hand.

The original Noto font is likewise absent from both final files. Only the
converted static Chinese glyph meshes remain. The export script rejects any
non-built-in Blender font datablock before packing or exporting.

### Historical pre-Chinese-rebuild expansion audit

A read-only audit before the 2026-08-29 Chinese district rebuild opened the
then-authoritative `.blend` in Blender 4.5 and confirmed that the apartment
source was intentionally delivered
as 36 adapted facade objects arranged in two asymmetrical 3-by-3 overlays. It
also found both ten-piece hinged-entry facades, all three static bicycle
instances, the finished pawnshop storefront, five market shops, two rear
houses, five Chinese red lamps, 36 complete
perimeter buildings across six CC0 profiles, four full-mesh street-cadence
replacements, and the market, pawnshop, and factory authored-prop clusters
described above. The density audit records zero intersections with the existing
city blocks. The
scene contains no font datablocks, armatures, linked-library objects, or
forbidden acquisition-source scaffolding; inspection of the exported GLB JSON
found no Noto font entry.

The same audit now records eight required runtime anchors:
`AuthoredStreetNetwork`, `JianghaiTenementDistrict`,
`RedStarElectronicsFactory`, `GuangchangPawnshop`, `OldCityMarketBridge`,
`GrandHotelSecurityTerminalVisual`, and
`MunicipalTreasuryManifestTerminalVisual`, plus `JianghaiValleyEnvironment`.
The valley anchor owns the existing project-authored, hand-chamfered
`OldCityFoundation`, one visual-only `JianghaiPerimeterGroundComposite`
assembled from eight Coast Line 01 scan placements, and 12 visual-only Hero
Mountain instances divided six/six across staggered inner and outer rings;
gameplay collision and navigation remain in Godot. Coastal Cliff 01, Coastal
Cliff 02, and Namaqualand Cliff 02 contribute no
delivered content. No procedural or displacement-generated visible terrain is
delivered. In the final saved DCC scene, the Municipal terminal root's duplicate
180-degree rotation is removed and its
screen faces opposite the Grand terminal. All 22 Rollershutter Window 03 and
Exterior Aircon Unit instances are placed flush against tenement facades;
none remains in `CentralAvenue`. Final DCC QA deletes the redundant
`JianghaiArtPass_FactoryHeroShutter` overlay because it became obsolete when
the damaged factory shell was replaced by five finished CC0 buildings. That
cleanup does not change the Rollershutter Window 03 source record or license;
the asset remains in tenement-facade placements and as a retained standalone
derivative, but is no longer used by either current interactive entrance. The
factory landmark entry is framed by five
Blender-authored visible objects: reused DCC brick piers, pier caps, and a
corrugated roof. Both this outer factory portal and the pawnshop frontage now
lead into a ten-piece authored facade: nine Quaternius `Brick_Plain_1` modules
and one `DoorFrame_Trim` module at each entrance. The portal and entry facades
are authored final art rather
than a code-built primitive or procedural visible substitute, while reused
packed materials retain their existing provenance. The recorded audit layers
are:

| Audit layer | Verified result |
| --- | --- |
| Upstream pre-valley Blender source baseline | 487 mesh objects; 196 unique mesh datablocks; 4,471,243 raw mesh-object triangles; 821,213 triangles counted once per unique mesh; 550 evaluated/runtime mesh instances and 4,500,345 instance triangles; seven then-required anchors; packed `.blend` size 61,677,884 bytes; SHA-256 `C7E9FAE468FFD9C15C8D1FCED165839F007FF6D7DBC2695FDB3041039E1510D7` |
| Factory-gate portal | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the new ten-piece personnel-door facade and its hinged runtime door |
| Hinged-entry facades | `entry_facades_ready=True`; two facades at 10/10 finished CC0 objects each; 18 `Brick_Plain_1` instances and two `DoorFrame_Trim` instances total; each leaves a human-scale 1.45-by-2.65-meter runtime door opening |
| Pawnshop hero entrance | `pawnshop_frontage_ready=True`; 15/15 modeled canopy parts; 15,492 canopy triangles; 8/8 solid wall modules and 8/8 authored inserts; 0 legacy visible gate/wall objects; the original large storefront opening is visually and physically infilled by the ten-piece hinged-entry facade around the central personnel door |
| Delivered urban-life and density expansion | 36/36 apartment-facade objects; 36/36 complete perimeter buildings across six CC0 profiles; `density_intersections=0`; four full-mesh street-cadence replacements; three static 11,825-triangle bicycles; market tea cart and basket; pawnshop tea table and three stools; factory hand truck; finished CC0 pawnshop backdrop and modeled pavilion gate; five market shops; two rear houses; five Chinese red lamps; five-building factory replacement |
| Upstream pre-valley serialized GLB baseline | 73,809,716 bytes; SHA-256 `2681C3F5F5332C1B2F8E5CA11B470C9A62EF39B8E4F76FA06365886A6FFE890A`; 550 mesh nodes; 4,500,345 instance triangles; maximum texture dimension 1024 pixels |
| Upstream pre-valley Godot authored-map baseline | `--validate-refinery-map` PASS; 550 imported authored meshes; 770 surfaces; 770 material-backed surfaces; 4,500,345 authored instance triangles; authored anchors 7/7; terminal checks 2/2/2/2; authored status screens 2/2; four interior residents |
| Upstream pre-valley Godot collision baseline | `--validate-refinery-collision` PASS; 240/240 exact concave shapes from 107 structural and 133 authored-detail meshes across 94/21/83/42 anchors; collision cache 104 shared meshes, 76 baked instances, and 77 unique shapes; 3,560,137 collision-instance triangles; 0 legacy model-placement boxes; 0 landmark proxy boxes; doorway facade and ballistic probes block the closed structures while the opened human-scale door route clears; market rail block 4/4, rail-post block 2/2, and rail-gap clear 2/2; high-value loot capsule access 12/12 |
| Runtime interactive doors and interiors | Two Kenney CC0 `door-hinged.glb` visuals; 1.45-by-2.65-meter doorway per entrance; normal 96-degree side swing; closed-door enemy capsule blocked and open-door route clear; four animated, unarmed Quaternius CC0 operator-model reuses as interior residents plus the existing interior loot placements |
| Godot route clearance | `routes=True`; `route_probes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Upstream pre-valley Godot quality baseline | Eight-view capture PASS after the 20 entry-facade objects, hinged doors, interior loot, and four authored residents; peak 1,014 draw calls, 1,271 objects, 7,113,753 primitives, 1,001.8 MB video memory, and 852.7 MB texture memory |

In that upstream baseline, the Blender source count is based on saved mesh objects, while the 821,213
unique-mesh figure counts each shared datablock once. Dependency-graph
evaluation and export resolve the scene to 550 runtime mesh instances and
4,500,345 instance triangles. The Godot diagnostic imports those same 550
meshes and sums 770 material-backed runtime surfaces. These scopes are
intentionally different rather than conflicting.

### Historical upstream capture performance evidence

The following figures were captured on 2026-08-28 after the 20-object facade
addition, hinged doors, interior loot, and four authored residents were active.
The high-tier performance policy disables shadows only for fine decorative
meshes; it does not change model geometry, materials, or visibility ranges.
Representative capture evidence is:

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
texture-memory reading. All eight views passed their configured budgets. These
figures remain historical pre-valley evidence.

The valley pre-rebase evidence and historical post-valley delivery are kept
separately. Neither binary represents the current Chinese district:

| Audit layer | Verified result |
| --- | --- |
| Valley pre-rebase Blender source | Historical snapshot: 74,037,661 bytes; SHA-256 `C9BAC433CF77791B3730E309A5E0BEEF6CF4849593D44018FD2CDFE5AC8FAA08`; Blender builder/audit/export PASS; 4,835,033 evaluated full-scene triangles, below the 5,000,000 gate; eight required anchors |
| Historical post-valley Blender source | 81,861,168 bytes; SHA-256 `7CA84CD2B17C3872323D8A5EE7B1A4BA5BCB360F4326FB2331327BED4F493461`; 500 mesh objects; 198 unique mesh datablocks; 4,807,899 raw mesh-object triangles; 1,003,869 triangles counted once per unique mesh; dependency-graph evaluation resolves 563 objects and 4,836,825 instance triangles; 8/8 anchors; builder, read-only audit, export, and GLB-round-trip gates all PASS; superseded by the Chinese district rebuild |
| Authored valley environment | `valley=True`; 188-triangle, 96-source-vertex project-authored `OldCityFoundation`; one 84,960-vertex, 168,480-triangle `JianghaiPerimeterGroundComposite` assembled from eight Coast Line 01 scan placements; one shared 14,000-triangle solararchitect Hero Mountain mesh composed as 12 instances divided six/six across staggered inner and outer rings; 336,668 total valley instance triangles. Ground bounds X `-600.878..600.853`, Y `-540.340..660.056`, Z `-12.7965..5.0390`; relief 17.835 meters; coverage 1.000; topology one component, two boundary loops/1,440 edges, zero degenerates, zero invalid normals; actual-foundation-footprint signed-distance transition; maximum foundation gap 0.103 meters; safe-area top -0.120 meters; 0-60/60-160-meter relief 0.969/3.955 meters; slope RMS/p90/p99/max 0.0579/0.0869/0.2331/0.6620; ring coverage 7,920/7,920. Gravel Floor 03 diffuse/normal/roughness, base-color factor `(0.92, 0.78, 0.62, 1.0)`, 7-meter affine world-XY UVs; DCC/GLB maximum UV errors `3.27e-6`/`4.36e-6` within `1.2e-5`; Jacobian 1/1; Coast material/image counts 0/0. North/south road ray gates 330/330 and 90/90 top hits with zero side hits; minimum mountain burial 4.942 meters; DCC and GLB round-trip gates PASS; all valley meshes visual-only; no displacement-generated visible geometry |
| Factory-gate portal contract | `factory_gate_portal=5/5`; `factory_gate_portal_aligned=True`; DCC-authored brick piers, caps, and corrugated roof frame the ten-piece personnel-door facade and hinged runtime door |
| Hinged-entry facade contract | `entry_facades_ready=True`; two facades at 10/10 finished CC0 objects each; 18 `Brick_Plain_1` instances and two `DoorFrame_Trim` instances total; each leaves a human-scale 1.45-by-2.65-meter runtime door opening |
| Pawnshop hero entrance contract | `pawnshop_frontage_ready=True`; 15/15 modeled canopy parts; 15,492 canopy triangles; 8/8 solid wall modules and 8/8 authored inserts; 0 legacy visible gate/wall objects; the original large storefront opening is visually and physically infilled by the ten-piece hinged-entry facade around the central personnel door |
| Delivered urban-life and density expansion | 36/36 apartment-facade objects; 36/36 complete perimeter buildings across six CC0 profiles; `density_intersections=0`; four full-mesh street-cadence replacements; three static 11,825-triangle bicycles; market tea cart and basket; pawnshop tea table and three stools; factory hand truck; finished CC0 pawnshop backdrop and modeled pavilion gate; five market shops; two rear houses; five Chinese red lamps; five-building factory replacement |
| Valley pre-rebase serialized GLB | Historical snapshot: 76,862,308 bytes; SHA-256 `0C0174672630957390A959BC3BD71DB3F4849CC7CABE0AFADFDD12273DFE02A5`; export and DCC-to-GLB round-trip PASS; 4,835,033 full-scene instance triangles; maximum texture dimension 1024 pixels |
| Historical post-valley serialized GLB | 84,723,312 bytes; SHA-256 `7E2BB712BCF031692FAFB0E4E0FA59F3E75CE340B2748F5EDBDB7B105D9B2965`; export and DCC-to-GLB round-trip gates PASS; 563 resolved mesh instances and 4,836,825 full-scene instance triangles; superseded by the Chinese district rebuild |
| Historical post-valley Godot authored-map import | PASS after an explicit editor reimport followed by a second no-op import; 563 authored meshes; 784 surfaces, all 784 material-backed; 4,836,825 authored instance triangles; 8/8 anchors; 419 detail meshes; 406 shadow-casting meshes; quality-tier counts 130/226/406; valley contract one ground plus 12 mountains and 336,668 triangles; exactly one named 168,480-triangle perimeter-ground composite, 12 named mountains sharing one Hero Mountain mesh, the 188-triangle foundation, both ten-piece hinged-entry facades, four interior residents, CC0/CC BY rights metadata, Gravel Floor 03 PBR identity and affine UV contract, direct hierarchy, no valley collision, modeled-ground coverage, and mountain-ring orientation |
| Historical post-valley Godot authored collision | PASS 240/240 exact concave shapes: 107 structural plus 133 authored-detail meshes across 94/21/83/42 anchors; collision cache 104 shared meshes, 76 baked instances, and 77 unique shapes; 3,560,137 collision-instance triangles; both hinged-entry facades; exact closed/open door probes; zero legacy proxy boxes; market rail/gap probes; building ballistic probes; high-value loot access 12/12 |
| Runtime interactive doors and interiors | `refinery-doors` PASS; two Kenney CC0 `door-hinged.glb` visuals; 1.45-by-2.65-meter doorway per entrance; normal 96-degree side swing; residents 4/4 using animated, unarmed Quaternius CC0 operator-model reuses alongside the existing interior loot placements |
| Historical post-valley Godot route clearance | PASS; `routes=14`; `route_blocker=none`; the Victory truck envelope `x[-2,1]` is sampled at multiple points for `y=0.45`, `y=1.4`, and `y=2.6` |
| Historical post-valley Godot atmosphere | PASS for Day and always-procedural Dusk; continuous sky/ground horizon; no panorama |
| Historical post-valley Godot quality and full runtime | All 11 representative captures PASS; 1,087.0 MB video memory of a 1,536 MB budget; 900.9 MB texture memory of a 1,152 MB budget; independent final visual review DELIVERABLE with no sky/terrain seam, radial pattern, skirt, z-fighting, trench, floating platform, or material south-line blocker |
| Historical post-valley diagnostics | `refinery-map`, `refinery-collision`, `refinery-doors`, `refinery-atmosphere`, `map-density`, `large-map`, `residential`, `stairs`, `skylinks`, and `vehicle-drive` all exit 0 |

The historical counting scopes intentionally differ: Blender's source count follows
saved mesh objects and unique datablocks, while dependency-graph, export, and
runtime counts follow resolved instances.

### Current Chinese-district runtime evidence

The current `BAD4B6C1...` GLB was explicitly reimported after the 2026-08-29
Chinese-district rebuild. Runtime and capture diagnostics report:

| Audit layer | Verified result |
| --- | --- |
| Godot authored-map import | 569 meshes; 1,517 material-backed surfaces; 3,015,841 instance triangles; 8/8 required anchors |
| Gameplay collision | 122/122 box shapes: 102 placement/profile boxes plus 20 landmark facade/traversal boxes; zero concave shapes; door, rail, rooftop, ballistic, and 12/12 high-value-loot access probes pass |
| Deployment loading | Threaded preload 502-1,069 ms across recorded verification runs; cached acquire 0 ms; reload-to-world-ready 1,535-2,913 ms across those runs |
| Render batching | 283 safe repeated source meshes grouped into 71 spatial `MultiMesh` batches; all 71 batch origins match their source centroids, reconstructed pose error is at most 0.000002 meters, visibility-range shortfall is zero in all three quality tiers, and original diagnostic nodes remain on render layer zero |
| Representative capture | All 11 views pass; overview 967 draw calls / 968 objects; mountain aerial 1,133-1,497 / 1,163-1,538 across final runs versus 1,562 / 2,867 before batching; daylight overview 1,534 / 1,558 versus 1,615 / 2,378 before batching; peak 793.3 MB video and 531.0 MB texture memory across the final verification runs |

### Historical post-valley capture performance evidence

After final export, explicit Godot editor reimport, and the second no-op import,
all 11 representative captures passed their configured budgets:

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
of the 1,152 MB budget. The historical high-tier performance policy disabled
shadows only for fine decorative meshes; it did not change model geometry,
materials, or visibility ranges. Independent visual review was DELIVERABLE: no
sky/terrain seam, radial pattern, skirt, z-fighting, trench, floating platform,
or material south-line blocker remains.

The map's complete layout, supporting geometry, district composition, material
adaptation, art direction, sign wording, sign placement, objective-terminal
status screens, terminal adaptations, urban-life composition, pawnshop
adaptation/composition, and factory-gate portal geometry/composition are project
work. The terminal bodies and other
imported third-party geometry, materials, and textures retain their source rights and
are not relicensed as MIT merely because adapted copies are packed, reused by
the portal, or exported in the delivered outputs. The Noto source remains
governed by SIL OFL 1.1 even though only converted glyph meshes, not the font
software, remain.

## Existing CC0 texture reuse

The authoritative DCC scene contains adapted copies of the already tracked
Poly Haven CC0 textures for Asphalt 03, Concrete Floor, Gravel Embedded
Concrete, and Corrugated Iron. They were acquired on 2026-08-06; exact
creators, source URLs, CC0 1.0 Universal status, attribution requirements, and
repository-local filename patterns are recorded above and in
`assets/textures/LICENSE.md`. Rusty Painted Metal remains tracked by the
project but is absent from the current Jianghai `.blend` and GLB.
Kloppenheim 06 (Pure Sky) is a separate CC0 HDRI acquired on 2026-08-28 and
retained under `assets/textures/`. It is not embedded in either final DCC/runtime
map binary, and the current `JianghaiOldCityAtmosphere` uses a procedural sky
without loading it.

## Existing CC0 model reuse

The authoritative DCC scene also contains adapted instances of the already
tracked Poly Haven Old Military Crate by Jack Mava and Concrete Road Barrier
by Amal Kumar, plus Coffee Cart 01 by Joe Seabuhr and Wicker Basket 01 by
Kuutti Siitonen. It additionally embeds the 18 brick and two doorframe instances
from Quaternius's Downtown City MegaKit recorded above. The current two runtime
doors use Kenney's separate `door-hinged.glb`. All are CC0 1.0 Universal;
attribution is not required, but creator credit is retained as provenance.
Exact evidence for the cart and basket remains in
`assets/models/polyhaven_residential_street/LICENSE.md` so this map record does
not duplicate or contradict its per-file hashes. Their packed `.blend`/GLB or
runtime mapping is recorded above and in `assets/models/LICENSE.md`.
