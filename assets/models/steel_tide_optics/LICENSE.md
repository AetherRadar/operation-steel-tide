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
and exported as one Godot-ready GLB. No marketplace-restricted content, code
primitive, CSG, generated box, or generated cylinder is included in the runtime
hierarchy.

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
- replaces source scalar materials with six named low-poly PBR housing/hardware
  materials;
- removes both source glass panes instead of shipping an opaque or incorrectly
  sorted cover; and
- derives each `*ReticleAnchor` from the removed rear-eyepiece pane before
  validating the open centerline with a mesh BVH ray test.

The final runtime asset contains three mesh nodes, 2,322 Blender vertices, and
1,200 triangles. Every optic has 400 triangles and a genuinely open aperture.
The red gameplay dot is an effect owned by the player view model, not a glass
or housing primitive in this asset.

## Delivered files

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `assets/models/steel_tide_optics/steel_tide_optics.glb` | 72,616 | `945D5C701F0EBB329BF7338950E79C02EA7521FC6C27E79093E5946952047E61` |
| `source_art/combat_optics/steel_tide_optics.blend` | 685,271 | `FC513563E32CCC64E57858501759A3376EAB3BF23C5AEDB635345D0678D1D72A` |
| `scripts/blender/build_authored_optics.py` | reproducible build script | `C36126B3B611608F065F84F1F6CE848BB9E8E5408C8124D8266FC72309EE7C07` |

The source geometry remains CC0 1.0 Universal. Project-authored runtime code is
covered separately by the repository's MIT license; that license does not
change or obscure the source provenance recorded above.
