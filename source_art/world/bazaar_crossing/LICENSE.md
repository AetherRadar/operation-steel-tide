# Bazaar Crossing V2 DCC Rights and Source Record

Every visible mesh exported by the Bazaar Crossing pipeline remains CC0 1.0
Universal and comes from a source object pinned in
`bazaar_crossing_source_palette.blend`. Arrangement, module fitting,
consolidation, UV retargeting, and material adaptation do not relicense those
meshes as MIT.

Operation Steel Tide's MIT-covered work is limited to layout, transforms,
composition, material/UV adaptations, metadata, deterministic build and
validation code, review cameras/lights, markers, and invisible gameplay
scaffolding. No generated primitive, box, prism, CSG, or procedural mesh is
presented as final visible art. Attribution is not required by CC0; credits are
retained as provenance.

## Pinned artifact identity

- map-local source palette SHA-256:
  `1E6C91C5AA1B7D798B5C603BB2CE40C89B5C3255A9047209EEAB109C9F4730F9`;
- generated packed Blend SHA-256:
  `7025690DA87D10E7CCCE4381A4EB05E0BEB6F7ABF7D989F63EF5301272B05615`;
- generated runtime GLB SHA-256:
  `93E7A925061FFF93DCC25F72E5353C584ED9B062831E9C0BD6439F77B6009D96`.

The packed artifacts were regenerated on 2026-08-30 after the authored stair
vestibule, B service passage, and material-balance refinement.

The GLB is exported without `KHR_draco_mesh_compression`; disabling a binary
compression extension does not alter the source licenses or provenance.

## Trey Ramm Modular Industrial Pieces

Creator: Trey Ramm (`minime453`). Official source:
https://opengameart.org/content/modular-industrial-kit. License: CC0 1.0
Universal. Acquired: 2026-08-27.

| Source module | Local source -> pinned object | Bazaar V2 use |
|---|---|---|
| `IndStairsWideFull` | `source_art/third_party/trey_modular_industrial/Meshes/Details/IndStairsWideFull.fbx` -> `BazaarSource_IndStairsWideFull` | Six exact-endpoint stair assemblies |
| `IndFloorGreyPlatformFull` | `.../Meshes/Floors/IndFloorGreyPlatformFull.fbx` -> `BazaarSource_IndFloorGreyPlatformFull` | Ground, paving, and painted site surfaces |
| `IndRoofTrimBStraightFull` | `.../Meshes/Trims/IndRoofTrimBStraightFull.fbx` -> `BazaarSource_IndRoofTrimBStraightFull` | Rails and stair guardrails |
| `IndColumnFree` | `.../Meshes/Details/IndColumnFree.fbx` -> `BazaarSource_IndColumnFree` | Warehouse/back-market columns, newels, and lamp supports |
| `IndColumnFreeCap` | `.../Meshes/Details/IndColumnFreeCap.fbx` -> `BazaarSource_IndColumnFreeCap` | Structural capitals |
| `IndFoundationAStraightFull` | `.../Meshes/Foundation/IndFoundationAStraightFull.fbx` -> `BazaarSource_IndFoundationAStraightFull` | Thick walls, counters, partitions, rack posts, and stair foundations |
| `IndRoofDarkGreyAngledFull` | `.../Meshes/Roofs/IndRoofDarkGreyAngledFull.fbx` -> `BazaarSource_IndRoofDarkGreyAngledFull` | Retained roof vocabulary |
| `IndWallFull` | `.../Meshes/Walls/IndWallFull.fbx` -> `BazaarSource_IndWallFull` | Interior wall vocabulary |
| `IndWallArchDouble` | `.../Meshes/Walls/IndWallArchDouble.fbx` -> `BazaarSource_IndWallArchDouble` | A courtyard, B loading, and back-market arcades; open portals |
| `IndWallArchDoubleColumns` | `.../Meshes/Walls/IndWallArchDoubleColumns.fbx` -> `BazaarSource_IndWallArchDoubleColumns` | Arcade structural vocabulary |
| `IndWallArchDoubleCapGrey` | `.../Meshes/Walls/IndWallArchDoubleCapGrey.fbx` -> `BazaarSource_IndWallArchDoubleCapGrey` | Arcade caps |
| `IndDoorFrameSingle` | `.../Meshes/Doors/IndDoorFrameSingle.fbx` -> `BazaarSource_IndDoorFrameSingle` | Door and partition frames |
| `IndRoofDarkGreyFull` | `.../Meshes/Roofs/IndRoofDarkGreyFull.fbx` -> `BazaarSource_IndRoofDarkGreyFull` | Closed-block, warehouse, stair-hall, and market roofs |
| `IndFloorGreyFull` | `.../Meshes/Floors/IndFloorGreyFull.fbx` -> `BazaarSource_IndFloorGreyFull` | Solid floor/ceiling and continuous storage-shelf vocabulary |
| `IndWindowBFull` | `.../Meshes/Windows/IndWindowBFull.fbx` -> `BazaarSource_IndWindowBFull` | Industrial facade and clerestory windows |
| `IndRoofTrimAStraight` | `.../Meshes/Trims/IndRoofTrimAStraight.fbx` -> `BazaarSource_IndRoofTrimAStraight` | Cornices, roof ridges, shop fascias, awnings, and interior beams |

`...` in the table expands to
`source_art/third_party/trey_modular_industrial`. The checked-in source folder
contains the creator's original README, source-page evidence, and CC0 record.

## Quaternius Downtown City MegaKit

Creator: Quaternius (`@Quaternius`). Official source:
https://quaternius.com/packs/downtowncitymegakit.html. License: CC0 1.0
Universal. Acquired: 2026-08-19. Local license evidence:
`assets/models/quaternius_downtown_city/QUATERNIUS_LICENSE.txt`.

| Source module | Local source -> pinned object | Bazaar V2 use |
|---|---|---|
| `Brick_Plain_1` | `assets/models/quaternius_downtown_city/Brick_Plain_1.gltf` -> `BazaarSource_QuatBrickPlain` | Red-brick wall vocabulary |
| `DoorFrame_Trim` | `assets/models/quaternius_downtown_city/DoorFrame_Trim.gltf` -> `BazaarSource_QuatDoorFrameTrim` | Detailed personnel doors and partition rhythm |
| `Brick_Window_CurvedDouble` | `assets/models/quaternius_downtown_city/Brick_Window_CurvedDouble.gltf` -> `BazaarSource_QuatBrickWindowCurvedDouble` | Curved brick windows for Mid and varied closed blocks |
| `Brick_Window_Trim` | `assets/models/quaternius_downtown_city/Brick_Window_Trim.gltf` -> `BazaarSource_QuatBrickWindowTrim` | A, Mid, back-market, boundary, shopfront-band, and closed-block facades |
| `Floor_4x4` | `assets/models/quaternius_downtown_city/Floor_4x4.gltf` -> `BazaarSource_QuatFloor4x4` | Double-sided interior floors, ceilings, decks, roofs, and rooftop monitor caps |
| `Metal_FirstFloor_Window` | `assets/models/quaternius_downtown_city/Metal_FirstFloor_Window.gltf` -> `BazaarSource_QuatMetalFirstFloorWindow` | B warehouse, rooftop monitors, and east industrial facade vocabulary |

## Other pinned CC0 content

| Source | Creator / official source | Acquired | Bazaar V2 mapping |
|---|---|---|---|
| Old Urban building | Abobla O.S / https://www.blenderkit.com/asset-gallery-detail/8177ff94-1645-4b50-95cc-cb05a336e34d/ | 2026-08-28 | Two of four outer landmark facades only |
| Scan Old Building Street | Free poly / https://www.blenderkit.com/asset-gallery-detail/d8c0ffa6-7b7d-47e9-8554-2d3bbcc82030/ | 2026-08-28 | One outer landmark facade only |
| Chinese red lamp | Kin Chen / https://www.blenderkit.com/asset-gallery-detail/b97e433c-2eb1-46b8-9633-5bdee21e4e7a/ | 2026-08-27 | Eighteen visibly supported interior lamps |
| Pink city bicycle | Kin Chen / https://www.blenderkit.com/asset-gallery-detail/4c1a83c1-829f-4c00-878e-9e73c6b89c3b/ | 2026-08-28 | Back-market landmark |
| Coffee Cart 01 | Joe Seabuhr / Poly Haven / https://polyhaven.com/a/CoffeeCart_01 | 2026-08-28 | B hall landmark; all three original parts |
| Chinese Tea Table | Kirill Sannikov / Poly Haven / https://polyhaven.com/a/chinese_tea_table | 2026-08-28 | A courtyard landmark |
| Chinese Stool | Kirill Sannikov / Poly Haven / https://polyhaven.com/a/chinese_stool | 2026-08-28 | A courtyard landmarks |
| Wicker Basket 01 | Kuutti Siitonen / Poly Haven / https://polyhaven.com/a/wicker_basket_01 | 2026-08-28 | B and back-market dressing |
| Hand Truck | Mutanzom3D / Poly Haven / https://polyhaven.com/a/hand_truck | 2026-08-28 | A warehouse landmark |
| Old Military Crate | Jack Mava / Poly Haven / https://polyhaven.com/a/old_military_crate | 2026-08-06 | Limited A warehouse dressing |
| Barrel 03 | Serhii Khromov / Poly Haven / https://polyhaven.com/a/barrel_03 | 2026-08-28 | Retained palette source; not primary V2 cover |
| Plastic Crate 02 | Fabi_G / Poly Haven / https://polyhaven.com/a/plastic_crate_02 | 2026-08-28 | Mid produce landmark |
| Asphalt 03 | Charlotte Baglioni and Dario Barresi / https://polyhaven.com/a/asphalt_03 | 2026-08-06 | `BazaarWetAsphalt` ground PBR |
| Gravel Embedded Concrete | Charlotte Baglioni / https://polyhaven.com/a/gravel_embedded_concrete | 2026-08-06 | `BazaarStonePaving` route/stair PBR |
| Concrete Floor | eye-candy.xyz / Poly Haven / https://polyhaven.com/a/concrete_floor | 2026-08-06 | `BazaarWeatheredConcrete` structural PBR |

The map-local palette is the authoritative daily-build input. Existing exact
hashes and evidence for reused non-Trey sources remain in their tracked source
records. The Bazaar builder does not open the Jianghai Blend.

Explicitly excluded: Hero Mountain, solararchitect assets, all CC BY content,
Coast Line 01, the Jianghai valley environment, the full Old City runtime GLB,
and paid, private, marketplace-standard, editorial, or unclear-license raw
assets.
