# Steel Tide M4A1 License and Adaptation Record

## Source and license

The Steel Tide M4A1 is adapted from **M4A1 Assault Rifle**, created and
uploaded by OpenGameArt user **nisu**.

- Official source page: https://opengameart.org/content/m4a1-assault-rifle
- Official download: https://opengameart.org/sites/default/files/m4a1_0.zip
- Original publication date: 2022-04-24
- Acquisition date: 2026-08-28
- Exact license: CC0 1.0 Universal
- License deed: https://creativecommons.org/publicdomain/zero/1.0/
- Download ZIP SHA-256:
  `ED5779EC82718861964227E2AAD2A900978EA087081154365D6D86246BE62F0D`

CC0 does not require attribution. The creator credit is retained as provenance
and courtesy credit. The original ZIP does not contain a license file, so the
official OpenGameArt page and its repository-local screenshot are the license
evidence. The unavailable historical `3dmodelscc0.com` domain is not relied on
as evidence.

The finished runtime attachment geometry also incorporates selected components
from the **Ultimate Guns Pack** by **Quaternius**:

- Official source record: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- Acquisition date: 2026-08-20
- Exact license: CC0 1.0 Universal
- Attribution required: No; creator credit is retained as provenance
- Repository license and source-file mapping:
  `../quaternius_ultimate_guns/LICENSE.md`

The final `.blend` and `.glb` are therefore a composite of two explicitly CC0
sources. Neither source asset nor the composite model output is relicensed as
MIT.

## Repository mapping

- Extracted source FBX and five source textures:
  `../../../source_art/third_party/nisu_m4a1/`
- License evidence and exact per-file hashes:
  `../../../source_art/third_party/nisu_m4a1/LICENSE_EVIDENCE.md`
- Reproducible Blender adaptation:
  `../../../scripts/blender/build_nisu_m4a1.py`
- Editable adapted source:
  `../../../source_art/combat_models/steel_tide_m4a1.blend`
- Godot runtime asset: `steel_tide_m4a1.glb`

The Blender adaptation rebinds the nisu source 2K base-color, metallic,
roughness, and normal maps into one PBR material, retains the supplied height
map in the source record, applies the project's `2.36` authored-space scale,
removes zero-area faces, and separates authored geometry into the stable
runtime hierarchy. Exact visible-component mapping is:

| Runtime node | Source file and object | Adaptation |
| --- | --- | --- |
| Main rifle, `Magazine`, `SpareMagazine`, `ChargingHandle`, `Stock` | nisu `M4A1.fbx` | PBR-textured base rifle and movable mechanisms |
| `RearIronSight` | nisu `M4A1.fbx`, objects `Sight` and `Sight_2` | Authored rear aperture grouped under a dedicated visibility node so an installed optic can keep a clear sight window without deleting the iron sight from the no-optic build |
| `FrontIronSight` | nisu `M4A1.fbx`, `Barrel` front-sight components | Authored front tower and post split from the barrel under a dedicated visibility node; optic builds can clear the sight picture while no-optic builds retain the complete original iron sights |
| `MuzzleDevice` | nisu `M4A1.fbx`, `Barrel` muzzle components | Split from the barrel so normal and suppressed builds are mutually exclusive |
| `Foregrip` | Quaternius `scarl.glb` (`Assault Rifle-Bgvuu4CUMV.glb`), object `AssaultRifle2_1` | Authored pistol-grip component fitted as a short angled foregrip |
| `Suppressor` | Quaternius `mp5a5.glb` (`Submachine Gun.glb`), object `SubmachineGun_2` | Independent authored front muzzle assembly fitted as the suppressor |
| `OpticMount` | Quaternius `axmc.glb` (`Sniper Rifle-TKaBjAEofL.glb`), object `SniperRifle_3` | Authored scope housing and mount fitted to the top rail; its independent rear/front source-glass planes locate `OpticRearApertureAnchor` and `OpticFrontApertureAnchor`, the rear plane also locates `OpticReticleAnchor`, and both panes are then excluded from the runtime mesh to leave a physically open sight aperture |

`MuzzleDeviceTip`, `SuppressorTip`, `OpticReticleAnchor`,
`OpticRearApertureAnchor`, and `OpticFrontApertureAnchor` are transform-only
gameplay markers derived from authored component bounds or real source-glass
vertices. The reproducible build asserts pre-export parentage, transforms, the two authored meshes beneath
`RearIronSight`, the authored front-tower mesh beneath `FrontIronSight`, and
non-empty triangle geometry under all four attachment nodes. The compact optic
keeps its authored housing and hardware, proves that its glass is exactly two
independent 8-vertex, 6-face/6-triangle planes, derives rear and front anchors
separately from their real plane centers, then removes all 12 triangles. The
reticle remains coincident with the rear plane. A BVH ray along the measured
rear-to-front axis asserts that the exported sight aperture is physically open
rather than relying on dark transparency. Godot's
`--validate-combat-models` diagnostic
then validates the imported GLB hierarchy, attachment geometry, configuration
truth table, and active muzzle/reticle alignment. The final GLB contains 19 mesh
instances, 12,414 imported vertices, and 10,617 triangles.

Blender `+Y` maps to Godot local `-Z`, so the front endpoint is the
more-negative Godot `Z` endpoint. Under `OpticMount`, the exact authored
coordinates are:

| Node | Blender local XYZ | Godot local XYZ |
| --- | --- | --- |
| `OpticRearApertureAnchor` | `(0.000000035, -0.088646941, 0.021902643)` | `(0.000000035, 0.021902643, 0.088646941)` |
| `OpticFrontApertureAnchor` | `(0.000000002, 0.087755263, 0.021902740)` | `(0.000000002, 0.021902740, -0.087755263)` |

Their separation is `0.176402214 m`, the Godot XY optical-axis residual is
approximately `0.000000103 m`, and reticle-to-rear distance is zero. The
builder guards those metrics before export, in raw GLB JSON, and after Blender
GLB round trip.

Reload hand contacts are likewise transform-only and are derived from the
actual nisu mechanism triangles rather than fallback bounds. In Blender
mechanism-local coordinates, `MagazineGripSocket` is
`(-0.026087064, -0.170000002, -0.059999987)` on the left wall of
`MagazineGeometry`, while `ChargingHandleSocket` is
`(-0.122946054, -0.300000012, 0.013999999)` on the rear-left wing of
`ChargingHandleGeometry`. Their exported surface gaps are respectively
`0.000000004` m and `0.000000001` m after Blender GLB round trip. The sockets
do not add triangles or alter the stable mechanism roots:

```text
SteelTideM4A1
|- Magazine                         (0.000, 0.310, -0.200)
|  |- MagazineGeometry
|  `- MagazineGripSocket
|- SpareMagazine                   (-0.300, 0.180, -0.620)
`- ChargingHandle                  (0.075, 0.050, 0.085)
   |- ChargingHandleGeometry
   `- ChargingHandleSocket
```

M4 barrel, stock, magazine, grip, and optic stat variants deliberately reuse
the corresponding finished authored base component instead of exposing the
hidden procedural variant meshes. The normal muzzle and suppressor do switch as
separate authored hierarchies. This preserves production art for every visible
M4 configuration while gameplay statistics remain controlled by C#.

The redistributed source and the adapted `.blend` and `.glb` outputs retain
their CC0 provenance and are not represented as MIT-licensed project-authored
art. The project-authored Blender adaptation script is covered by the
repository's root MIT license, subject to the disclosure in
`../../../docs/CONTENT_PROVENANCE.md`.

## Delivered output identity

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| Runtime `steel_tide_m4a1.glb` | 5,302,128 | `BC34336E1B28F3E7EB8E8E5730142BCB934284F7B136CF53BD6D06C6D3D9D609` |
| DCC source `../../../source_art/combat_models/steel_tide_m4a1.blend` | 4,872,028 | `1765E2528CE992CFB8F343A2FE641DF9FFFA582F80514ABDABE4A1C1D1FFF991` |
| Reproducible builder `../../../scripts/blender/build_nisu_m4a1.py` | 74,198 | `2F245E0FAE0079ED3CDDA1DFCF47FF1DF230EE659CB12E60E09F786AEBDCF003` |
| Ignored review `../../../build/art-previews/steel_tide_m4a1.png` | 1,023,330 | `CD5E58921A5BEA944548878C721E704C75B62C5882D846CAE5AA629DE0FA3A9F` |
| Ignored ADS review `../../../build/art-previews/steel_tide_m4a1_ads.png` | 888,600 | `30E19D7CBB7AED14305DF5F83FB97E77F04024AFC884A67ED4D7058C613286E1` |
