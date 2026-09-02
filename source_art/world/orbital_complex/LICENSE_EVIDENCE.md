# FALLTIDE RECOVERY ARRAY source evidence and transformation map

The overall map layout, district selection, route graph, terrain/hardscape,
interaction metadata, material palette, source modifications, and Blender build
workflow are original project work created 2026-09-01 through 2026-09-02. They
are covered by the repository's root MIT license. Third-party mesh and texture
rights remain attached to their source content and are not relicensed as MIT.

## NASA hero geometry

| Runtime use | Retained source | Source and usage basis | Modification |
| --- | --- | --- | --- |
| 56 m telemetry landmark | `source_art/third_party/nasa_3d/nasa_70_meter_dish.glb` | NASA/Ames Research Center; [official resource](https://science.nasa.gov/3d-resources/70-meter-dish/); [NASA media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) | scale `0.62`, fictional off-white/oxide materials, weathering, static pedestal separated from reflector/feed/truss at authored component boundaries, double-axis pivots, non-target source hierarchy/metadata discarded |
| Fictional recovered return article | `source_art/third_party/nasa_3d/nasa_orion_capsule_no_fbc.stl` | NASA; [official resource](https://science.nasa.gov/3d-resources/orion-capsule/); same media guidelines | scale `0.33`, fictional ceramic/scorch materials, impact pose, no NASA mark, not identified as real Orion in scene |

Both files were acquired 2026-09-01. Exact bytes, SHA-256 values, guideline
interpretation, attribution, and non-endorsement controls are recorded in
`source_art/third_party/nasa_3d/LICENSE_EVIDENCE.md`.

## CC0 authored structures and props

- Trey Ramm / OpenGameArt user `minime453`, **Modular Industrial Pieces**,
  CC0 1.0, acquired 2026-08-27 from
  <https://opengameart.org/content/modular-industrial-kit>. FALLTIDE uses only
  the repository's converted complete building, gate, arch, and
  elevated-walkway GLBs under `assets/models/trey_modular_industrial/`. The
  second underground pass additionally instantiates the previously unused
  `compressor-house.glb`, `sawtooth-service-hall.glb`, `loading-bay.glb`,
  `inspection-office.glb`, `shift-office.glb`, `utility-office.glb`, and
  `window-hall.glb` as closed-shell modules framing the Cathode Well and Data
  Ossuary landmarks.
  Complete source-module mappings and hashes are retained in that directory's
  `README.md` and in `source_art/third_party/trey_modular_industrial/`.
- Majadroid / Maik Hoffmann, **3D House Construction Site**, CC0 1.0, acquired
  2026-08-27 from
  <https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0>.
  The current underground GLB uses only the `containers-office.glb` and
  `containers-cargo.glb` compositions for intake customs and coolant-pipe
  stores. The package's `crane-on-ground.glb` and
  `construction-materials.glb` remain general-library sources and are not
  embedded in this delivery. Exact local conversion mapping is in
  `assets/models/majadroid_construction_site/README.md`.
- The east Quarantine Archive uses the existing
  `assets/models/operations_office/operations_office_set.glb`, an authored
  composition of CC0 Trey industrial modules and CC0 Kenney Furniture Kit 2.0
  props. Its complete source mapping and rebuild are in that asset's `README.md`
  and `scripts/blender/build_operations_office_set.py`.

The FALLTIDE builder never imports the prior full Bazaar Crossing, Jianghai Old
City, or Tideglass maps. It imports only the necessary individual authored GLBs
listed above, so the output does not duplicate an unrelated map.

## CC0 PBR surfaces

All three texture sets were acquired 2026-08-06 from Poly Haven and are CC0
1.0. Original 1K files remain under `assets/textures/`; the FALLTIDE GLB embeds
only the required base-color, normal, and roughness maps.

- **Concrete Floor**, creator eye-candy.xyz:
  <https://polyhaven.com/a/concrete_floor>.
- **Asphalt 03**, photography by Charlotte Baglioni, processing by Dario
  Barresi: <https://polyhaven.com/a/asphalt_03>.
- **Rusty Painted Metal**, creator Amal Kumar:
  <https://polyhaven.com/a/rusty_painted_metal>.

The generated `build_report.json` records the exact local map-file SHA-256 for
all nine texture images as well as every mesh-source hash.

## Runtime artifact and DCC audit

The authoritative editable source is
`source_art/world/orbital_complex/orbital_complex.blend` (15,521,550 bytes;
SHA-256
`E288F6743444A27ADA414254DC06685EF223D36DE53EBFEDA959D306D3EB4EF2`). The
deterministic builder is
`scripts/blender/build_orbital_complex_underground.py`, which reuses the
provenance-aware import/material/export helpers in
`scripts/blender/build_orbital_complex.py` and must be run with Blender 4.5+.

The current self-contained runtime is
`assets/models/orbital_complex/orbital_complex.glb` (19,472,480 bytes; SHA-256
`5031189E36A803B6EEBC1A91E6F0CB7AA23A14F23586AD9585A72D4B981F2E30`). Its glTF
document contains 343 nodes, 240 mesh resources, 242 primitives, 42 materials,
and 11 embedded images, with no external buffer or image URI. The source and
round-trip audits report 340 x 320 m horizontal bounds centered at Godot
`(0,0,-60)`, vertical envelope `Y=-34..24`, three visible vertical layers,
zero duplicate node names, zero empty material slots, and all required
`DishYaw`/`DishPitch`, gate, alarm, power, POI, spawn, and extraction anchors.
Seven representative previews are rendered under
`source_art/world/orbital_complex/previews/`. Gameplay collision and navigation
are intentionally supplied by Godot runtime code rather than hidden in the
visual GLB.

## Exclusions and claims

- No paid, marketplace-standard, editorial, non-redistributable, or
  unclear-license asset is embedded.
- No NASA insignia, logotype, seal, employee likeness, or mission mark is
  exported. NASA is acknowledged only as the factual source of two meshes, and
  no NASA review, permission, approval, sponsorship, or endorsement is implied.
- No third-party source geometry is claimed as project-authored or relicensed
  under MIT.
- Project-created visible geometry is limited to terrain, water, roads, sea
  defenses, dry-dock hardscape, powered guidance strips, minor luminaires, and
  the original Cathode Well/Data Ossuary landmark dressing (pressure rings,
  curved service ribs, coolant tubes, archive spires, and memory arches). The
  original UNDERTOW SUMP / Blackwater Lift 03 dressing (teardrop intake mouth,
  paired volute spirals, double siphon arch, tidal sight glass, and surface-only
  console apron) is likewise project-authored DCC detail and is tagged
  `collision_role=minor_prop`; its four-metre console approach does not alter
  the imported architecture-shell collision contract. The imported closed-shell
  Trey modules remain the major building art; no runtime-visible building is
  assembled from `BoxMesh` or CSG primitives.
