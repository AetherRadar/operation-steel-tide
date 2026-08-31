# Bazaar Crossing V2 DCC Source

`bazaar_crossing_source_palette.blend` is the immutable, map-local Blender 4.5
LTS input for Bazaar Crossing V2. It contains only audited CC0 source objects,
materials, and packed textures used by this map. Its SHA-256 is
`1E6C91C5AA1B7D798B5C603BB2CE40C89B5C3255A9047209EEAB109C9F4730F9`.
Daily rebuilds never open the mutable Jianghai Old City Blend.

`scripts/blender/build_bazaar_crossing.py` validates that palette, assembles
finished CC0 modules into the dense arena, renders fourteen review views, saves
the packed DCC source, exports a Godot-compatible non-Draco runtime GLB, imports
the GLB again, and writes `bazaar_crossing_build_report.json`. It rejects every
visible mesh whose source is not CC0 and parses the exported GLB JSON to reject
`KHR_draco_mesh_compression` in required, used, or per-primitive extensions.

## Rebuild

From the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background source_art/world/bazaar_crossing/bazaar_crossing_source_palette.blend `
  --python-exit-code 2 `
  --python scripts/blender/build_bazaar_crossing.py
```

Generated artifacts:

- `source_art/world/bazaar_crossing/bazaar_crossing.blend`;
- `source_art/world/bazaar_crossing/bazaar_crossing_build_report.json`;
- `source_art/world/bazaar_crossing/previews/*.png`;
- `assets/models/bazaar_crossing/bazaar_crossing.glb`.

Set the task-specific environment variable `BAZAAR_SKIP_PREVIEWS=1` only for a
fast structural iteration. A delivery build must render the review views.

## V2 architectural contract

V2 does not use the former copied boundary-building grid or three exposed
industrial plates. Its main combat spaces are complete modular buildings:

| Region | Footprint and identity | Elevated route |
|---|---|---|
| A Caravanserai | `x[-60,-34] z[-31,-4]`; two-storey perimeter, open `10 x 10 m` courtyard, covered arcades, vestibule, and divided rear warehouse | Gallery `x[-59,-53] z[-27,-9]`, top `y=3.6` |
| B Market Warehouse | `x[34,60] z[-30,-6]`; fully roofed hall, 12-column grid, loading arcade, stock room, counters, beams, and clerestory | Balcony `x[53,59] z[-27,-9]`, top `y=3.4` |
| Mid Indoor Connector | Three staggered market halls plus north junction `x[-9,9] z[-24,-7]`; offset A/B doors and two internal baffles prevent a site-to-site view | Mezzanine `x[-9,-3] z[17,31]`, top `y=3.2`, facing Mid only |
| Defender Back Market | Four roofed transfer halls bounded by seven full-height rear city blocks; 5-7 m folded retake lanes and a 14 m spawn breathing bay | Ground route |

The arena additionally contains 37 coherent closed modular blocks with complete
facades, returns, cornices, and roofs. Only four legacy Old City buildings
remain, all as distant perimeter landmarks. Props are landmarks and dressing;
the report's cover gate requires at least 70% of cover contributors to be
walls, arcades, columns, counters, partitions, portals, or kiosks.

## Authored source modules

The palette pins 16 Trey Ramm Modular Industrial Pieces sources: the original
stair, platform floor, rail, column, column cap, foundation, and angled roof,
plus wall, double arch, arch columns, arch cap, door frame, flat roof, solid
floor, window, and roof-trim modules. Six Quaternius Downtown City MegaKit
sources add red-brick wall panels, detailed door frames, two window families,
industrial first-floor windows, and double-sided interior floor/ceiling tiles.

Large visible components are arrangements or real DCC adaptations of those
finished modules. There is no exported primitive, CSG, procedural box, or
stretched whole-building shell. Poly Haven and BlenderKit CC0 props provide
limited room landmarks and the four outer facades. Exact paths and mappings are
in `LICENSE.md`.

## Coordinate and traversal contract

One Blender unit is one meter. Godot local `(x, y, z)` is authored in Blender
as `(x, -z, y)`. The root remains at the origin with unit scale.

| Stair | Visual low -> high | Width / slope |
|---|---|---|
| A South | `(-56,0,2.1)` -> `(-56,3.6,-9)` | `3.2 m` / `17.9691 deg` |
| A Rear | `(-41.9,0,-27)` -> `(-53,3.6,-27)` | `3.2 m` / `17.9691 deg` |
| B South | `(56,0,1.5)` -> `(56,3.4,-9)` | `3.2 m` / `17.9424 deg` |
| B Rear | `(42.5,0,-27)` -> `(53,3.4,-27)` | `3.2 m` / `17.9424 deg` |
| Mid South | `(-6,0,40.85)` -> `(-6,3.2,31)` | `3.2 m` / `17.9976 deg` |
| Mid North | `(-6,0,7.15)` -> `(-6,3.2,17)` | `3.2 m` / `17.9976 deg` |

The three approach-side flights use full-height 5.2-metre facade openings and
unobstructed exterior forecourts; they have no side portal, front wall, arch,
canopy post, or entry trim. Rear flights remain inside their parent buildings.
Inner rails leave landing gaps. Every stair rail and its first newel begin
`0.90 m` upslope from the visual low point, keeping the low landing clear for a
player capsule to descend along either side and turn out without catching the
rail end.
Godot owns collision, navigation, spawns, sites, and smooth route surfaces;
visual low points correspond to runtime route points at `y=0.2`.

## Current verified budget

The V2 report records:

- 1,443 -> 700 visible mesh draw nodes and 1,950 -> 935 material surfaces
  after same-region, same-material, same-responsibility static consolidation;
- 648 unique meshes in the final packed DCC scene;
- 252,707 raw whitelisted-source triangles;
- 805,155 unique and 1,101,693 delivered instance triangles, unchanged by
  draw-node consolidation and GLB round trip;
- 48 DCC materials and 58 DCC textures;
- 203.473 MiB estimated RGBA8 plus full-mip-chain texture memory;
- 107,169,064-byte non-Draco GLB, SHA-256
  `B0AADFAD6610AB37A57E7D583044483E01E7402E86557CF573D9558A72862514`;
- 49,534,466-byte packed Blend, SHA-256
  `0C5341729E1D6A4D6DFD80A74D52AD74E5787E68A236B208819710833934262B`;
- bounds X `[-68,68]`, Blender Y `[-56.2,56.2]`, Z `[-0.16,9.46]`;
- four complete enterable interiors and 9,925.573 square metres of roofed
  footprint;
- 135 continuous storage parts, 104 continuous shopfront parts, 110 skyline
  articulation parts, and two attached back-market solid-mass walls;
- exactly 28 independently preserved runtime-door portal objects, with no fake
  terminus/niche role and a floor-to-roof A-rear Segment02 connection;
- exact required-object, bounds, and triangle preservation after GLB
  round-trip import;
- no `KHR_draco_mesh_compression`; the only required GLB extension is
  `EXT_texture_webp`.

Every texture is at most 1024 px. The JSON report remains authoritative if a
rebuild changes byte counts or hashes.

## Editing policy

The build script is the reproducible source of composition and source
selection. Any hand edit to a generated artifact must be reflected in the
script. Do not append private, paid, unclear-license, CC BY, mountain, or
untracked marketplace content.
