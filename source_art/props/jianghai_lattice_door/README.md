# Jianghai lattice hinge door

This directory contains the editable Blender source and representative studio
preview for the Jianghai Old City Chinese lattice door. The runtime asset is
`assets/models/jianghai_old_city/jianghai_lattice_door.glb`.

## Retained authored sources

The structural leaf is retained from Kenney's finished CC0 **Factory Kit**
`assets/models/kenney_factory_kit/door-hinged.glb`, SHA-256
`3857B4953CA264DD37B42B8D8391CD2348CACBD2671BA87113434A311B956C1B`.

The Chinese grille is retained from Free poly's CC0 **Chinese Temple 2**,
acquired from BlenderKit on 2026-08-27. Its official asset page is
`https://www.blenderkit.com/asset-gallery-detail/8701a79a-1635-437c-b1d2-6b14f14fc351/`.
The repository's authoritative Jianghai scene already contains the licensed
source as object `GuangchangClanHall`, mesh `网格.002`. License and acquisition
evidence are recorded in
`source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md` and the repository's
top-level model/provenance records.

## Exact DCC extraction

The builder extracts the complete closed, double-sided arched double-door
fine-grille component from material index 2. A source polygon is retained only
when every vertex lies inside the mesh-local AABB:

- minimum `(-0.61685, -2.24555, 6.14627)`
- maximum `(0.61647, -2.10685, 7.64258)`

That deterministic selection must contain exactly 11,192 vertices, 9,316
polygons and 9,316 triangles. Blender's authored-mesh `DECIMATE/COLLAPSE` pass
at ratio `0.60` produces the reviewed runtime LOD: 9,276 vertices, 5,589
polygons and 5,589 triangles. The pass removes only redundant authored surface
detail; it does not rebuild the grille from primitives.

The LOD is centered and fitted to 0.600 by 0.038 by 0.700 meters, then shared by
front and back grille nodes at hinge-local centers `(0.40, -0.076, 1.075)` and
`(0.40, 0.076, 1.075)`. The leaf uses packed deep-red lacquer and the authored
grille uses an aged-gold PBR finish. Their physically based albedos are tuned
for legibility beneath the Old City shop awnings without emissive shading or
extra runtime lights. This replaces the earlier turquoise hanging-lattice
overlay with a true finished Chinese arched grille.

The exported door contains only retained authored geometry: one finished
Kenney leaf and two nodes sharing the finished, DCC-reduced Temple 2 grille.
No lattice bars, frame members, panels, hinges or hardware are generated from
cubes, cylinders, tori, CSG or assembled boxes. The studio-only floor and wall
used by `render_preview` are excluded from the selected runtime export.

## Rebuild and verification

From the repository root with Blender 4.5 LTS:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_jianghai_lattice_door.py
```

The DCC recipe rewrites:

- `source_art/props/jianghai_lattice_door/jianghai_lattice_door.blend`
- `source_art/props/jianghai_lattice_door/jianghai_lattice_door_preview.png`
- `source_art/props/jianghai_lattice_door/jianghai_red_wood_albedo.png`
- `assets/models/jianghai_old_city/jianghai_lattice_door.glb`
- `assets/models/jianghai_old_city/jianghai_lattice_door_JianghaiRedWoodAlbedo.png`

The Blender 4.5.10 GLB round trip passes with closed bounds 0.800 by 0.190 by
1.600 meters, three visible mesh nodes, two unique authored mesh resources, two
Principled PBR materials, 11,334 instanced triangles, one packed 256-square
lacquered-wood albedo, and no external buffer or image dependency. The final
412,548-byte runtime GLB has SHA-256
`FBE9FC3EBB1F8BB49842442F1A4AEF451E0F67E5B3FF95BBB16A6F01B84D5528`.
The 63,926-byte source and Godot extraction-target texture copies are
byte-identical and have SHA-256
`C75ED94A13A4F21CE518F455916802117D193FCE7A5731A0A4A602F82FD43834`.

The 1,162,441-byte editable `.blend` has SHA-256
`72D41DB8125BB5DDDEE04DE14E6AA5C9D8B1D4D5058823B74CC52968D78C9445`;
it and the representative preview are retained for visual and DCC review.
Blender save/render bookkeeping can vary across rebuilds, so the canonicalized
runtime GLB and deterministic packed texture hashes above are the reproducible
delivery identities.

## Runtime contract

`JianghaiLatticeDoor` and `DoorLeafHinge` both use `(0, 0, 0)` as the left-edge
hinge origin. Closed geometry spans local X `0.0..0.8` and local Z `0.0..1.6`.
The embedded `open` and `close` clips each run for 18 frames at 30 fps and
rotate `DoorLeafHinge` through 96 degrees. Godot converts Blender +Z-up to its
+Y-up coordinate system. A runtime component may either play the clips or
rotate its collision-backed parent about the same zero pivot.

The builder verifies both upstream source contracts, rejects altered source
selection or LOD topology, validates every exported mesh as retained authored
geometry, round-trips the GLB, and rejects shifted pivots or bounds, missing PBR
materials, external payloads, lost animation clips, changed mesh/triangle
counts, or missing component-level provenance metadata.
