# Quaternius Buildings Pack Runtime Assets

These Godot-ready GLBs are reproducible adaptations of four authored FBX
models from Quaternius's Buildings Pack. The conversion preserves the source
geometry, object scale, material colors, and PBR values while correcting the
FBX importer's zero-alpha solid materials to fully opaque. It then centers each
model on the horizontal plane, grounds it at Z=0, and embeds provenance metadata.

## File mapping

| Runtime GLB | Original FBX | Source / verified GLB dimensions (m) | Mesh / triangles | Authored materials |
| --- | --- | --- | --- | --- |
| `building1-large.glb` | `Building1_Large.fbx` | 7.991 x 2.744 x 4.671 / 7.991 x 2.744 x 4.671 | 1 / 41,788 | Beige, Brown, DarkGrey, DarkRed, Grey, LightYellow, White |
| `building3-big.glb` | `Building3_Big.fbx` | 4.705 x 4.391 x 5.677 / 4.705 x 4.391 x 5.677 | 1 / 7,004 | Beige, BrickRed, Brown, DarkGrey, Grey, LightYellow |
| `building4.glb` | `Building4.fbx` | 4.644 x 3.856 x 5.487 / 4.644 x 3.856 x 5.487 | 1 / 9,740 | Brown, DarkGrey, Grey, GreyBlue, LightBlue, White, WhiteBlue |
| `house2.glb` | `House2.fbx` | 3.644 x 3.084 x 2.926 / 3.644 x 3.084 x 2.926 | 1 / 19,714 | Brown, DarkGrey, Grey, White |

## Rebuild and verification

Run with Blender 4.5 or newer:

```text
blender --background --python scripts/blender/build_quaternius_buildings_pack.py
```

The script reimports every generated GLB and fails unless its dimensions,
horizontal centering, ground contact, mesh statistics, complete material-name
set, per-material triangle assignment, major PBR values, embedded provenance,
opaque solid-material alpha, and every triangle's positions, normals, and UVs
match within strict tolerances.
Degenerate source triangles retain their positions and material assignment, but
their undefined zero-length normals are intentionally excluded from comparison.
The table above records the successful Blender 4.5.10 LTS round trip performed
on 2026-08-28. A second complete run produced identical SHA-256 hashes for all
four GLBs. Godot 4.6.3 Mono also imported all four as `PackedScene` resources.

Quantized source-geometry fingerprints used by the verified build:

- `building1-large.glb`: `f85a66cf33be2de6ae43b061cafb3b7260758871d83e71fb94c4b9ca7d166430`
- `building3-big.glb`: `093ad2c654540281eeca833e37c39cdf25c20aa5c8b1e629b71fabad61cc77cd`
- `building4.glb`: `294c2f13448272adb0904554c1eeb8eb9e848aefa9a2d244a2d30e178bed5a2c`
- `house2.glb`: `610eaa659482776ebbb580167ff610812dfe0cc40601cfdcae0c18f21a06f6e8`

Creator: Quaternius

Official source: https://quaternius.com/packs/buildings.html

License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/

Acquisition date: 2026-08-28

Local license evidence: `source_art/third_party/quaternius_buildings_pack/`
