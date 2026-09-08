# FALLTIDE RECOVERY ARRAY runtime asset rights

Runtime asset: `orbital_complex.glb`

This file is a mixed-rights Blender composition. The original map layout,
terrain/hardscape, interaction hierarchy, metadata, material tuning, and build
script are available under the repository's root MIT license. Embedded
third-party geometry and textures retain the rights described below and are not
relicensed as MIT.

| Embedded content | Creator/source | Rights and required notice | FALLTIDE modification |
| --- | --- | --- | --- |
| 70 Meter Dish mesh | NASA/Ames Research Center, [NASA Science resource](https://science.nasa.gov/3d-resources/70-meter-dish/) | Redistributable under NASA [Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) for this factual-source-acknowledged, non-endorsement use; NASA marks are not included | scaled, fictionally recolored/weathered; static pedestal separated at authored component boundaries and reflector/feed/truss placed beneath `DishYaw`/`DishPitch` |
| Orion Capsule (no fbc) source mesh | NASA, [NASA Science resource](https://science.nasa.gov/3d-resources/orion-capsule/) | Redistributable under the same NASA media guidelines and non-endorsement conditions; NASA source is acknowledged and no NASA marks are included | scaled, fictionally recolored/scorched and impact posed as an unnamed recovered return article |
| Complete industrial buildings, gates, arches, catwalks | Trey Ramm / `minime453`, [Modular Industrial Pieces](https://opengameart.org/content/modular-industrial-kit) | CC0 1.0 Universal; credit retained as provenance | map-specific placement, scale, district material treatment, interaction pivots |
| Office/cargo containers | Majadroid / Maik Hoffmann, [3D House Construction Site](https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0) | CC0 1.0 Universal; credit retained as provenance | `containers-office.glb` and `containers-cargo.glb` placed as intake and coolant-store dressing; map-specific material treatment. The source package's crane and construction-material variants are not embedded in this underground GLB |
| Quarantine Archive command hall geometry/furniture | existing `operations_office_set.glb`, composed from Trey Ramm and Kenney CC0 sources | source geometry CC0 1.0; prior project composition and this placement remain project MIT | reused as one enterable east-district facility; no unrelated old map embedded |
| Concrete Floor PBR maps | eye-candy.xyz, [Poly Haven](https://polyhaven.com/a/concrete_floor) | CC0 1.0 Universal | planar UV scale and wet service-deck use |
| Asphalt 03 PBR maps | Charlotte Baglioni and Dario Barresi, [Poly Haven](https://polyhaven.com/a/asphalt_03) | CC0 1.0 Universal | service-route UV scale |
| Rusty Painted Metal PBR maps | Amal Kumar, [Poly Haven](https://polyhaven.com/a/rusty_painted_metal) | CC0 1.0 Universal | oxidized-red district material use |

NASA is acknowledged solely as the source of two mesh files. NASA has not
reviewed, approved, sponsored, or endorsed Operation Steel Tide or FALLTIDE
RECOVERY ARRAY. The scene includes no NASA insignia, logotype, seal, employee
likeness, or mission branding, and it does not claim the fictional capsule is a
real Orion vehicle or that the map depicts a NASA facility.

Acquisition dates and immutable NASA hashes:

- 2026-09-01, `nasa_70_meter_dish.glb`, SHA-256
  `36FF56A7A2BFD1C278F6F4774D32128D5931F2C22FE58241D00EE7D1815634BB`.
- 2026-09-01, `nasa_orion_capsule_no_fbc.stl`, SHA-256
  `ABC4C69C27AFA55C4A06BC9972B8872979F1473FB26E15224FB0F77F1CD81DC7`.

Current delivery hashes (generated 2026-09-05) are:

- `source_art/world/orbital_complex/orbital_complex.blend`, 15,689,980 bytes,
  SHA-256 `A2C9C20B5978A54C3A70D9DBF3A177AF3223E10EF637A028FD4259490AE3459C`;
- `assets/models/orbital_complex/orbital_complex.glb`, 20,284,932 bytes,
  SHA-256 `FDFA6F70F5BCF1AB7996A69C39BAFE6C38F52374C9AD9A573AB82E3D22DB49D4`.

The runtime GLB is self-contained (597 glTF nodes, 441 mesh resources, 443
primitives, 43 materials, and 11 embedded images; no external buffer or image
URI). The authoritative `build_report.json` records the nine packed texture
hashes and every imported source hash; the underground builder's source-date
metadata remains 2026-09-01 for the NASA downloads even when rebuilt later.

Complete source mappings, local file hashes, modifications, previews, build
statistics, embedded-image checks, and the Blender round-trip audit are in:

- `source_art/third_party/nasa_3d/LICENSE_EVIDENCE.md`;
- `source_art/world/orbital_complex/LICENSE_EVIDENCE.md`;
- `source_art/world/orbital_complex/build_report.json`;
- `scripts/blender/build_orbital_complex.py`.
