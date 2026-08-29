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
| `OpticMount` | Quaternius `axmc.glb` (`Sniper Rifle-TKaBjAEofL.glb`), object `SniperRifle_3` | Authored scope housing and mount fitted to the top rail; the two source glass panes locate `OpticReticleAnchor` and are then excluded from the runtime mesh to leave a physically open sight aperture |

`MuzzleDeviceTip`, `SuppressorTip`, and `OpticReticleAnchor` are transform-only
gameplay markers derived from those authored component bounds. The reproducible
build asserts pre-export parentage, transforms, the two authored meshes beneath
`RearIronSight`, the authored front-tower mesh beneath `FrontIronSight`, and
non-empty triangle geometry under all four attachment nodes. The compact optic
keeps its authored housing and hardware, derives the reticle anchor from the
source eyepiece glass center, then removes all 12 triangles forming the two
glass end panes. A BVH centerline check asserts that the exported sight aperture
is physically open rather than relying on dark transparency. Godot's
`--validate-combat-models` diagnostic
then validates the imported GLB hierarchy, attachment geometry, configuration
truth table, and active muzzle/reticle alignment. The final GLB contains 19 mesh
instances, 12,414 imported vertices, and 10,617 triangles.

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
| Runtime `steel_tide_m4a1.glb` | 5,300,992 | `674C32712FFEB684C78E33411E5027852BE7F954098AD53BEDB72897F493DE06` |
| DCC source `../../../source_art/combat_models/steel_tide_m4a1.blend` | 4,865,006 | `ED007B14A3B2C3B932624F75179A62E9575424B05B22ABF6FE28482CDFE71C0B` |
