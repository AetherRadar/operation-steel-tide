# Trey Ramm Modular Industrial Kit selection

This directory contains Godot-ready compositions of authored modules used by the Tideglass Reactor demolition arena.

- Creator: Trey Ramm (OpenGameArt username `minime453`)
- Official source: https://opengameart.org/content/modular-industrial-kit
- Official download: https://opengameart.org/sites/default/files/modular_industrial_pieces.zip
- Acquired: 2026-08-27
- License: Creative Commons Zero 1.0 Universal (CC0 1.0)
- Attribution: Not required; the creator's requested courtesy credit is retained
- Source and license evidence: `source_art/third_party/trey_modular_industrial/SOURCE_PAGE.html` and `ORIGINAL_README.txt`
- Selected source files: `source_art/third_party/trey_modular_industrial/Meshes/` and `PacificNorthwestGradientAtlas.png`
- Reproducible conversion: `scripts/blender/build_trey_modular_industrial.py`

The runtime GLBs in this directory combine selected walls, arches, windows, doors, stairs, floors, foundations, roofs, trims, and columns from the original modular kit. `ASSET_OVERVIEW.png` is retained with the selected sources as package evidence.

Runtime compositions:

- `east-security-gate.glb`
- `west-service-gate.glb`
- `arch-gateway.glb`
- `loading-bay.glb`
- `elevated-walkway.glb`
- `window-hall.glb`
- `sawtooth-service-hall.glb`
- `utility-office.glb`
- `reactor-annex.glb`
- `shift-office.glb`
- `turbine-workshop.glb`
- `compressor-house.glb`
- `inspection-office.glb`
- `boiler-workshop.glb`
- `switchgear-hall.glb`
- `crew-canteen.glb`
- `pump-house.glb`
- `transformer-works.glb`
- `glassworks-office.glb`
- `cooling-service-hall.glb`
- `control-room.glb`
- `maintenance-depot.glb`
- `foundry-warehouse.glb`

The two gate compositions use different authored door layouts. At runtime they
close the east and west openings in the Majadroid perimeter fence; the
Tideglass diagnostic checks their exact AABBs, opaque solid materials, multi-height
visible triangle coverage, and alignment with the matching boundary collision.

The loading bay, utility office, central service hall, and window hall are
closed one-storey compositions. The Blender build validates continuous authored
front, rear, left, and right perimeter coverage against the roof footprint,
rejects the source kit's two-storey window and angled-roof pieces for these
assemblies, and verifies the exported dimensions through a GLB round trip.
Runtime diagnostics additionally check module counts, single-storey height,
solid-material opacity, visible render layers, and tight collision padding.

The first three expansion buildings are separate finished DCC compositions rather
than isolated facade panels. `reactor-annex.glb` is a wide flat-roof hall with
an overhead bay, two window bands, and a personnel entrance;
`shift-office.glb` is a compact windowed office with a continuous raised
cornice; and `turbine-workshop.glb` uses two complete gabled roof runs above
separate overhead and personnel entrances. Each includes authored floor tiles,
continuous four-sided wall/window/door coverage, and a complete authored roof.
The second expansion set adds a reinforced double-bay `compressor-house.glb`,
an `inspection-office.glb` with a two-post authored entrance canopy, and a
two-storey-height `boiler-workshop.glb` with independently validated lower and
upper facade bands. These three are sized as practical sightline blockers, not
thin decorative facades.
The final grid-filler set adds a transverse-gabled `switchgear-hall.glb`, a
window-rich flat-roof `crew-canteen.glb` with a full raised cornice, and a
broad-gabled `pump-house.glb` with separate overhead and personnel entrances.
The switchgear hall's first roof arrangement was rejected during rendered DCC
review because backface culling exposed an incomplete silhouette; its delivered
roof is a complete authored single-ridge gable with matching end trims.
The six-building street-blocker set adds a reinforced and corniced transformer
works, a window-rich glassworks office with an entrance canopy, a triple-bay
transverse-gabled cooling hall, a two-storey control room, a broad-gabled
maintenance depot, and a three-ridge foundry warehouse. Backface-culling review
of the first slope-roof exports exposed dark attic openings. The delivered
cooling hall, depot, and foundry therefore include respectively two, two, and
six triangular gable end walls DCC-shaped from Trey Ramm's authored
`IndWallFull` source panel. The build rejects any end wall that is not a single
triangle, leaves the shell plane, misses the storey top, or misses the matching
authored roof-trim peak.
The build samples the horizontal floor and roof coverage, rejects facade pieces
away from the four shell planes, checks exact module/style contracts, and then
requires dimensions, mesh count, triangle count, material count, embedded
palette, license metadata, and collision metadata to survive the GLB round trip.

Exact selected-source module mapping for all fifteen additions:

| Output | Source FBX modules used |
| --- | --- |
| `reactor-annex.glb` | `IndGarageArchWhite` x1, `IndGarageWhite` x1, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x6, `IndWallFull` x7, `IndFloorGreyFull` x15, `IndRoofDarkGreyFull` x15 |
| `shift-office.glb` | `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x7, `IndWallFull` x2, `IndFloorGreyFull` x6, `IndRoofDarkGreyFull` x6, `IndRoofTrimAStraight` x10 |
| `turbine-workshop.glb` | `IndGarageArchWhite` x1, `IndGarageWhite` x1, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x6, `IndWallFull` x5, `IndFloorGreyFull` x12, `IndRoofDarkGreyAngledFull` x12, `IndRoofTrimAAngledL` x4, `IndRoofTrimAAngledR` x4 |
| `compressor-house.glb` | `IndGarageArchWhite` x2, `IndGarageWhite` x2, `IndWindowBFull` x4, `IndWallFull` x6, `IndFloorGreyFull` x12, `IndRoofDarkGreyFull` x12, `IndColumnFree` x4, `IndColumnFreeCap` x4 |
| `inspection-office.glb` | `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x7, `IndWallFull` x2, `IndFloorGreyFull` x6, `IndRoofDarkGreyFull` x7 (six shell tiles plus one canopy), `IndColumnFree` x2 |
| `boiler-workshop.glb` | `IndGarageArchWhite` x1, `IndGarageWhite` x1, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x8, `IndWindowETopFull` x14, `IndWallFull` x7, `IndFloorGreyFull` x15, `IndRoofDarkGreyFull` x15, `IndCornerTrimBFull` x8 |
| `switchgear-hall.glb` | `IndWindowBFull` x6, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWallFull` x7, `IndFloorGreyFull` x12, `IndRoofDarkGreyAngledFull` x8, `IndRoofTrimAAngledL` x2, `IndRoofTrimAAngledR` x2 |
| `crew-canteen.glb` | `IndWindowBFull` x13, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndFloorGreyFull` x12, `IndRoofDarkGreyFull` x12, `IndRoofTrimAStraight` x14 |
| `pump-house.glb` | `IndGarageArchWhite` x1, `IndGarageWhite` x1, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x6, `IndWallFull` x3, `IndFloorGreyFull` x9, `IndRoofDarkGreyAngledFull` x6, `IndRoofTrimAAngledL` x2, `IndRoofTrimAAngledR` x2 |
| `transformer-works.glb` | `IndGarageArchWhite` x2, `IndGarageWhite` x2, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x8, `IndWallFull` x7, `IndFloorGreyFull` x24, `IndRoofDarkGreyFull` x24, `IndRoofTrimAStraight` x20, `IndColumnFree` x4, `IndColumnFreeCap` x4 |
| `glassworks-office.glb` | `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowEBottomFull` x8, `IndWindowBFull` x9, `IndFloorGreyFull` x20, `IndRoofDarkGreyFull` x21 (twenty shell tiles plus one canopy), `IndRoofTrimAStraight` x18, `IndColumnFree` x2 |
| `cooling-service-hall.glb` | `IndGarageArchWhite` x3, `IndGarageWhite` x3, `IndWindowBFull` x7, `IndWallFull` x9 (seven storey panels plus two DCC-shaped gable infills), `IndFloorGreyFull` x24, `IndRoofDarkGreyAngledFull` x12, `IndRoofTrimAAngledL` x2, `IndRoofTrimAAngledR` x2 |
| `control-room.glb` | `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowEBottomFull` x7, `IndWindowBFull` x2, `IndWindowETopFull` x16, `IndWallFull` x6, `IndFloorGreyFull` x16, `IndRoofDarkGreyFull` x16, `IndCornerTrimBFull` x8 |
| `maintenance-depot.glb` | `IndGarageArchWhite` x1, `IndGarageWhite` x1, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowBFull` x8, `IndWallFull` x7 (five storey panels plus two DCC-shaped gable infills), `IndFloorGreyFull` x15, `IndRoofDarkGreyAngledFull` x6, `IndRoofTrimAAngledL` x2, `IndRoofTrimAAngledR` x2 |
| `foundry-warehouse.glb` | `IndGarageArchWhite` x2, `IndGarageWhite` x2, `IndDoorFrameSingle` x1, `IndDoorSingleRed` x1, `IndWindowEBottomFull` x4, `IndWindowBFull` x4, `IndWallFull` x13 (seven storey panels plus six DCC-shaped gable infills), `IndFloorGreyFull` x24, `IndRoofDarkGreyAngledFull` x24, `IndRoofTrimAAngledL` x6, `IndRoofTrimAAngledR` x6 |

Every module path resolves below
`source_art/third_party/trey_modular_industrial/Meshes/`; all materials use the
tracked `PacificNorthwestGradientAtlas.png` from the same CC0 source selection.

Blender 4.5.10 LTS produced the following deterministic expansion outputs. The
Godot AABB values use `Vector3(X, Y, Z)` order after glTF Y-up conversion. Each
scene is centered on X/Z and grounded at Y=0, so the listed box can be used at
the recommended authored scale `(1, 1, 1)` without an additional offset shim.

| Runtime GLB | Blender bounds X x Y x Z (m) | Meshes / triangles / materials | Godot collision AABB size / offset (m) | Bytes | SHA-256 |
| --- | --- | --- | --- | ---: | --- |
| `reactor-annex.glb` | 10.000 x 6.432 x 3.100 | 1 / 2,384 / 1 | `(10.000, 3.100, 6.432)` / `(0.000, 1.550, 0.000)` | 731,484 | `51687DA6FADDB6A1E9E66DED920745FE25829B5FC542B33BAF60C465FCBDBE99` |
| `shift-office.glb` | 6.600 x 4.600 x 3.500 | 1 / 2,566 / 1 | `(6.600, 3.500, 4.600)` / `(0.000, 1.750, 0.000)` | 747,188 | `86B4FC5FBC1564EF9E677494D6BF0417529E7D3102083C80C516B7867D1D46B7` |
| `turbine-workshop.glb` | 8.000 x 6.600 x 4.500 | 1 / 2,460 / 1 | `(8.000, 4.500, 6.600)` / `(0.000, 2.250, 0.000)` | 735,840 | `6B74C277967F376E9833F01EBACE2E940147DD203F1C4BEFF35AA6AB6A1F4FFB` |
| `compressor-house.glb` | 8.400 x 6.416 x 3.300 | 1 / 2,088 / 1 | `(8.400, 3.300, 6.416)` / `(0.000, 1.650, 0.000)` | 704,756 | `3BA6206AAE522FEDA537DC6C7410BA4D6680F4BE2E557838DDAF75EA871DF121` |
| `inspection-office.glb` | 6.000 x 5.816 x 3.100 | 1 / 2,458 / 1 | `(6.000, 3.100, 5.816)` / `(0.000, 1.550, 0.000)` | 740,384 | `8AA2C47F558DAA0CE20C7407EA40D9A353537BC9FEB1A5C3ED26B4C58E6889D2` |
| `boiler-workshop.glb` | 10.400 x 6.432 x 6.100 | 1 / 8,212 / 1 | `(10.400, 6.100, 6.432)` / `(0.000, 3.050, 0.000)` | 1,085,380 | `5D9CB882ED3E0CD590BFF06255869F5A744467BA3C870FE10293B37A8C85183B` |
| `switchgear-hall.glb` | 8.600 x 6.316 x 4.500 | 1 / 2,174 / 1 | `(8.600, 4.500, 6.316)` / `(0.000, 2.250, 0.000)` | 723,360 | `673CE57EEE18D822666B0BF0FF378BFE230BECFA8D07E66851DB324EE08BD09F` |
| `crew-canteen.glb` | 8.600 x 6.600 x 3.500 | 1 / 4,606 / 1 | `(8.600, 3.500, 6.600)` / `(0.000, 1.750, 0.000)` | 872,584 | `F7079FC40C60824B0206C2BE97ADCB5CB883B2AD4ADDFE8F67BFC1D1AA1B1C49` |
| `pump-house.glb` | 6.000 x 6.600 x 4.500 | 1 / 2,368 / 1 | `(6.000, 4.500, 6.600)` / `(0.000, 2.250, 0.000)` | 729,892 | `2E2E109221315CB2E9ABC240D840EDA79C7C6260B430EFAA936F3B1FBDFCEAA8` |
| `transformer-works.glb` | 12.600 x 8.600 x 3.500 | 1 / 3,894 / 1 | `(12.600, 3.500, 8.600)` / `(0.000, 1.750, 0.000)` | 816,816 | `A27D02F90CA903A4C64F2445DA72DCDFC0884DBBB6273FEF8A66FC59E022EE93` |
| `glassworks-office.glb` | 10.600 x 9.900 x 3.500 | 1 / 6,058 / 1 | `(10.600, 3.500, 9.900)` / `(0.000, 1.750, 0.000)` | 961,472 | `484729E304094E7D6B7D7AB6C620A3A8C9AC08333555697828EC6B1E80BA39E3` |
| `cooling-service-hall.glb` | 12.600 x 8.416 x 4.500 | 1 / 3,108 / 1 | `(12.600, 4.500, 8.416)` / `(0.000, 2.250, 0.000)` | 765,584 | `BCC3E72A8BF088AED9FF026C82AE5719095E1A167647DA2F7A3A33359BC4EDE6` |
| `control-room.glb` | 8.400 x 8.432 x 6.100 | 1 / 8,948 / 1 | `(8.400, 6.100, 8.432)` / `(0.000, 3.050, 0.000)` | 1,134,884 | `5106C8590330ECC18204F5E5261F8CF67443568644EAC116D2616FAF71C0C0FA` |
| `maintenance-depot.glb` | 10.000 x 6.600 x 4.500 | 1 / 3,042 / 1 | `(10.000, 4.500, 6.600)` / `(0.000, 2.250, 0.000)` | 771,636 | `76302E049BA585DEBA43086A68FAE871FAB5AB231FBBAFBCB551F1266535DC63` |
| `foundry-warehouse.glb` | 12.000 x 8.600 x 4.500 | 1 / 3,460 / 1 | `(12.000, 4.500, 8.600)` / `(0.000, 2.250, 0.000)` | 792,724 | `EC2196B2B95D1F643DC25ACD66CFDF530F387E87A9D2DD4AF908001BBDEB2916` |

After the module-level shell checks, Blender consolidates the respectively
47/33/47 source meshes and material instances into one runtime mesh and one
shared atlas material per building. It preserves all 2,384/2,566/2,460 authored
triangles, UVs, bounds, and source-composition metadata; the GLB round trip
rechecks those optimized statistics rather than relying on exporter estimates.

The blocker set applies the same optimization to 46/26/71 validated source
meshes, producing one runtime mesh and one material per building while
preserving 2,088/2,458/8,212 triangles respectively. The boiler workshop also
requires continuous coverage independently at both three-meter facade levels.

The final grid-filler set consolidates 39/53/32 validated source meshes to one
runtime mesh and one atlas material per building while preserving
2,174/4,606/2,368 triangles respectively. Its shell contracts independently
require the transverse gable, complete cornice, and broad gable profiles.

The six-building street-blocker set consolidates 97/80/62/73/44/87 validated
source meshes to one runtime mesh and one atlas material per building while
preserving 3,894/6,058/3,108/8,948/3,042/3,460 triangles respectively. The
source and round-trip contracts record the 2/2/6 authored-wall gable adaptations
separately from the continuous one- or two-storey facade bands.

Two consecutive full twenty-three-asset Blender builds produced byte-identical hashes
for every output; the eight pre-existing GLBs also remained byte-identical to
their pre-expansion versions.

The elevated walkway uses four complete authored platform modules, two
symmetrical ten-step wide stairs that meet the deck at both ends, and authored
guard rails along both long edges. Its Blender
build rejects corner stairs, partial platform panels, asymmetric landings, and
disconnected stair tops, and measures every rail against the deck structure with
world-space mesh BVHs. Runtime uses the visible 22-mesh assembly itself for
scale-baked, double-sided concave collision, sweeps a player capsule upward from
below the deck, and physically walks a player up both stairways.

The composed scenes are adaptations of Trey Ramm's authored modules. They retain the source kit's CC0 dedication and are not relicensed under the repository's root MIT license.
