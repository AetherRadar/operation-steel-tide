# Bazaar Crossing DCC Source

`bazaar_crossing_source_palette.blend` is the authoritative, map-local Blender
4.5 LTS input for Bazaar Crossing. It contains only the exact audited CC0
source objects, materials, and packed textures used by this map. Its SHA-256
is `0073ADE0E13682C47A07CCBE02B499BFF8FBD25C0C98DA908BB58A94FEE4F1F4`.
Daily rebuilds do not open or depend on the mutable Jianghai Old City Blend.

`scripts/blender/build_bazaar_crossing.py` validates that pinned palette,
arranges and adapts finished CC0 modules, renders four review views, saves the
packed `bazaar_crossing.blend`, exports the runtime GLB, imports the GLB again,
and writes a deterministic validation report. It rejects every exported visible
mesh whose origin is not CC0.

## Rebuild

From the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background source_art/world/bazaar_crossing/bazaar_crossing_source_palette.blend `
  --python-exit-code 2 `
  --python scripts/blender/build_bazaar_crossing.py
```

Generated artifacts:

- `source_art/world/bazaar_crossing/bazaar_crossing.blend`
- `source_art/world/bazaar_crossing/bazaar_crossing_build_report.json`
- `source_art/world/bazaar_crossing/previews/*.png`
- `assets/models/bazaar_crossing/bazaar_crossing.glb`

The build prints `BAZAAR_DCC_PASS` only after the authored-source, UV/material,
budget, bounds, exact GLB round-trip triangle, and provenance gates all pass.

## Authored module contract

The three decks, bridge, six stairs, fourteen runtime-aligned guardrail runs,
supports, fascia, and canopy are arrangements or DCC adaptations of finished
Trey Ramm/minime453 Modular Industrial Pieces modules under CC0. The pinned
palette includes exactly these seven structural sources:

- `IndStairsWideFull` for the six stair assemblies;
- `IndFloorGreyPlatformFull` for ground, route, site-pad, deck-top, and deck-
  underside surfaces;
- `IndRoofTrimBStraightFull` for open rails, stair rails, fascia, and canopy
  trim;
- `IndColumnFree` for deck, canopy, guardrail, stair, and lantern supports;
- `IndColumnFreeCap` for deck capitals;
- `IndFoundationAStraightFull` for deck edges and stair stringer/foundation
  mass;
- `IndRoofDarkGreyAngledFull` for the Mid market canopy.

The report records the exact source-object and module-instance mapping for all
six stairs, three decks, fourteen guardrails, and the canopy. No programmatic
box, prism, CSG, or generated mesh is exported as visible art. Project-authored
MIT work is limited to layout, transforms, UV/material retargeting, metadata,
review lighting/cameras, validation, and invisible gameplay scaffolding.

## Coordinate and traversal contract

One Blender unit is one meter. Godot local `(x, y, z)` is authored in Blender
as `(x, -z, y)`. The exported root remains at the origin with unit scale.

| Structure | Godot top center / footprint | Visual stairs |
|---|---|---|
| A Gallery | `(-57, 3, -20)`, `12 x 20 m` | South and east; 18 steps, 3.2 m wide, 9.72 m run |
| Mid Bridge | `(0, 3, 0)`, `26 x 3.6 m` | West and east; 18 steps, 3.2 m wide, 9.72 m run |
| B Balcony | `(57, 2.6, -22)`, `12 x 18 m` | South and west; 16 steps, 3.2 m wide, 8.32 m run |

The six named visual stairs retain the frozen runtime endpoints. Deck top and
underside modules explain the full invisible collision thickness. Open authored
rails preserve every exact 3.2 m stair gap. Godot continues to own collision,
navigation, spawns, bomb sites, and smooth traversal ramps.

The map also provides finished CC0 visible explanations for all eleven runtime
architecture AABBs, four bomb-site cover clusters, four elevated barrel banks,
two Coffee Cart covers, and `SightBlockSitePair`. Eight elevated market props
and seven visibly supported lanterns reduce empty deck silhouettes.

## Current verified budget

The current report records `BAZAAR_DCC_PASS` with:

- 277 visible mesh nodes, 116 unique meshes, and 298 material surfaces;
- 249,216 raw whitelisted-source triangles;
- 272,916 unique delivered triangles and 2,771,825 instanced triangles;
- 28 DCC materials and 46 DCC textures;
- 127.473 MiB estimated RGBA8 plus full-mip-chain texture memory;
- 31,089,036-byte GLB and 22,580,020-byte packed output Blend;
- Blender bounds X `[-68, 68]`, Y `[-56, 56]`, Z `[-0.16, 7.5021]`.

Every texture is at most 1024 px. Every visible mesh is CC0-sourced,
triangulated, material-backed, UV-complete, and provenance-complete after GLB
round-trip import. The JSON report is the authority if these numbers change.

## Editing policy

The build script is the reproducible source of composition and source
selection. Any hand edit to a generated artifact must be reflected in the
script. Do not append unrelated source collections, mountains, private assets,
or unclear-license content. See `LICENSE.md` for the complete local source
record.
