# Steel Tide Authored Optics

- Original creator: Quaternius (`@Quaternius`)
- Source pack: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- Exact source license: CC0 1.0 Universal
- License deed: https://creativecommons.org/publicdomain/zero/1.0/
- Original acquisition date: 2026-08-20
- DCC adaptation date: 2026-08-28
- Attribution required: No. Creator credit is retained as provenance.

The runtime optic set is a Blender-authored derivative of three finished scope
components already tracked from the Quaternius Ultimate Guns Pack. The
components are extracted from repository-local CC0 GLBs, reshaped in Blender,
then finished with a DCC-authored machined Picatinny base and clamp feet before
export as one Godot-ready GLB. No marketplace-restricted content, code
primitive, CSG, or runtime-generated visible geometry is included in the
runtime hierarchy.

## Exact source mapping

| Runtime node | Repository source | Original pack file | Source object/component | Source SHA-256 |
| --- | --- | --- | --- | --- |
| `MicroOptic` / `MicroGeometry` | `assets/models/quaternius_ultimate_guns/axmc.glb` | `Sniper Rifle-TKaBjAEofL.glb` | `SniperRifle_3`, glass-bearing authored scope component | `7CDCE34DEC9A9B1AAE6C9E2EF554C88ECDC19554407DECC239B159E13D295F3F` |
| `HoloOptic` / `HoloGeometry` | `assets/models/quaternius_ultimate_guns/awm.glb` | `Sniper Rifle-i65hEldsw6.glb` | `SniperRifle_5`, glass-bearing authored scope component | `095E918BD89823B1CA726EAC0016D7C9DAEE15CC6F71010AB251FB0365819F02` |
| `ScopeOptic` / `ScopeGeometry` | `assets/models/quaternius_ultimate_guns/vss.glb` | `Sniper Rifle.glb` | `SniperRifle_4`, glass-bearing authored scope component | `C69B8D4088176580819C20F44FC80D7742E6AD00BA1CA09CD064D8677B1C4BE5` |

The upstream pack record and selected-file mapping remain in
`assets/models/quaternius_ultimate_guns/LICENSE.md`.

## DCC adaptation and aperture policy

Each selected source component contains 412 authored triangles, including two
source glass panes totaling 12 triangles. The reproducible Blender build:

- maps the source `+X` barrel axis into the project's Blender `+Y` weapon axis;
- applies DCC vertex deformation to produce distinct micro, wide holo, and
  long-tube silhouettes while retaining the authored source topology;
- adds a low-profile rail base, two clamp feet, and small edge bevels in
  Blender so the optic seats against the authored weapon rail contact on every
  compatible firearm;
- replaces source scalar materials with six named low-poly PBR housing/hardware
  materials;
- removes both source glass panes instead of shipping an opaque or incorrectly
  sorted cover; and
- proves that the removed glass is exactly two independent 8-vertex,
  6-face/6-triangle planes, then derives each `*RearApertureAnchor` and
  `*FrontApertureAnchor` independently from the respective plane's deformed
  bounding center rather than copying the gameplay reticle marker;
- keeps each `*ReticleAnchor` coincident with its real rear plane and validates
  the complete rear-to-front optical axis before checking the open centerline
  with a mesh BVH ray test; and
- exports six globally unique aperture node names. This is required because
  Godot 4.6 makes imported glTF node names globally unique even when duplicate
  names have different parents.

The final runtime asset contains three mesh nodes, 2,858 Blender vertices, and
2,204 triangles. Every optic has a machined rail interface and a genuinely open
aperture.
The red gameplay dot is an effect owned by the player view model, not a glass
or housing primitive in this asset.

## Aperture-anchor runtime contract

Blender `+Y` is the muzzle-facing end and imports as Godot local `-Z`. The
front anchor is therefore the more-negative Godot `Z` endpoint. All six
markers are direct children of their named optic variant.

| Variant | Rear node / Blender local XYZ | Front node / Blender local XYZ | Front-rear separation | Godot XY residual |
| --- | --- | --- | ---: | ---: |
| `MicroOptic` | `MicroRearApertureAnchor` / `(0.000000000, -0.053188160, 0.001201294)` | `MicroFrontApertureAnchor` / `(-0.000000050, 0.052653145, 0.001416905)` | `0.105841305 m` | `0.000215610 m` |
| `HoloOptic` | `HoloRearApertureAnchor` / `(0.000000000, -0.069955960, -0.001741253)` | `HoloFrontApertureAnchor` / `(-0.000000015, 0.069101647, -0.002069937)` | `0.139057606 m` | `0.000328684 m` |
| `ScopeOptic` | `ScopeRearApertureAnchor` / `(0.000000000, -0.206911996, 0.001292342)` | `ScopeFrontApertureAnchor` / `(-0.000000052, 0.204385146, 0.001723982)` | `0.411297143 m` | `0.000431640 m` |

The builder audits parentage, exact names, source-plane provenance, nonzero
front/rear separation, the sub-0.5 mm Godot XY residual, zero
reticle-to-rear distance, raw GLB JSON, and a Blender GLB round trip. A clean
Godot 4.6.3 reimport was also inspected to confirm that all six unique names
survive without numeric suffixes.

## Delivered files

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `assets/models/steel_tide_optics/steel_tide_optics.glb` | 171,616 | `680A0063BB0D6DBDC900629D05CC342C0CBD614591FD119CE72567DD9893D413` |
| `source_art/combat_optics/steel_tide_optics.blend` | 801,081 | `61F6F703B35AEE2A677E8E31C1D0E39AF6F2E80C81786AFF03AA640D872BF6A2` |
| `scripts/blender/build_authored_optics.py` | 54,839 | `0592E842E2F14C43AE97B3DA7936029E43088C1ACC3BFF97951D063B11AE75C8` |
| Ignored review `build/art-previews/steel_tide_optics.png` | 1,628,521 | `33ACF897D05E61AF6FF562FD74B60446106912AF7F70CDE2A44350BE6833E5E4` |
| Ignored ADS review `build/art-previews/steel_tide_optics_ads.png` | 1,425,809 | `94BA85398F0E8C8BBE427A7B0D41E99BDAD4CAF46CE5EFE26DDB70D266025270` |

The source geometry remains CC0 1.0 Universal. Project-authored runtime code is
covered separately by the repository's MIT license; that license does not
change or obscure the source provenance recorded above.
