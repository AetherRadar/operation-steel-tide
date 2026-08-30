# Quaternius Buildings Pack Runtime Assets

These Godot-ready GLBs are reproducible adaptations of all nine authored FBX
models from Quaternius's Buildings Pack. The conversion preserves the source
geometry, object scale, material colors, and PBR values while correcting the
FBX importer's zero-alpha solid materials to fully opaque. It then centers each
model on the horizontal plane, grounds it at Z=0, and embeds provenance metadata.

## File mapping

| Runtime GLB | Original FBX | Source / verified GLB dimensions (m) | Mesh / triangles | Authored materials | GLB bytes / SHA-256 |
| --- | --- | --- | --- | --- | --- |
| `building1-large.glb` | `Building1_Large.fbx` | 7.991 x 2.744 x 4.671 / 7.991 x 2.744 x 4.671 | 1 / 41,788 | Beige, Brown, DarkGrey, DarkRed, Grey, LightYellow, White | 1,682,092 / `84639fe0064d1c8201759059d4c42380778ce3bb3cbc43067755454977d9b64d` |
| `building1-small.glb` | `Building1_Small.fbx` | 3.747 x 2.744 x 4.663 / 3.747 x 2.744 x 4.663 | 1 / 16,620 | Beige, Brown, DarkGrey, DarkRed, Grey, LightYellow, White | 673,204 / `1e2cdf8850b66243f0f24e1073231cadab3e56a5f87fec1b6061ad554804f525` |
| `building2-large.glb` | `Building2_Large.fbx` | 5.716 x 2.219 x 5.918 / 5.716 x 2.219 x 5.918 | 1 / 15,072 | Beige, Brown, DarkGrey, Grey, GreyBlue, LightBlue, White | 555,380 / `33befe0a6d8fd12f9020d27052f2a071518ec5a5a0cfa006ea40d0de674bbe5f` |
| `building2-small.glb` | `Building2_Small.fbx` | 3.584 x 2.486 x 4.968 / 3.584 x 2.486 x 4.968 | 1 / 5,160 | Beige, Brown, DarkGrey, Grey, GreyBlue, LightBlue, White | 194,348 / `01b63356b10c1b0b1478709060757cf2158421f53dede865b8b48b3e3272b979` |
| `building3-big.glb` | `Building3_Big.fbx` | 4.705 x 4.391 x 5.677 / 4.705 x 4.391 x 5.677 | 1 / 7,004 | Beige, BrickRed, Brown, DarkGrey, Grey, LightYellow | 338,768 / `a3c8786b25804600207239a9c4728860a15420e0b15010b71176c1f0bccb9cfc` |
| `building3-small.glb` | `Building3_Small.fbx` | 3.052 x 4.396 x 5.677 / 3.052 x 4.396 x 5.677 | 1 / 5,242 | BrickRed, Brown, DarkGrey, Grey, LightGrey | 269,984 / `260c5cf67b83b6d77f262458ca5bffd41e5a8c59efd01e80e09f1e9949856550` |
| `building4.glb` | `Building4.fbx` | 4.644 x 3.856 x 5.487 / 4.644 x 3.856 x 5.487 | 1 / 9,740 | Brown, DarkGrey, Grey, GreyBlue, LightBlue, White, WhiteBlue | 362,100 / `f623bac2837329e5c948c064f964dacbd4188505223b238778c5cc2f27d6f672` |
| `house1.glb` | `House1.fbx` | 2.557 x 3.870 x 3.179 / 2.557 x 3.870 x 3.179 | 1 / 28,354 | Brown, DarkGrey, Grey, White | 1,202,880 / `42496d4d0840110159499fff2e1b5aacac328b79ddeb7225024b3b879c8285f9` |
| `house2.glb` | `House2.fbx` | 3.644 x 3.084 x 2.926 / 3.644 x 3.084 x 2.926 | 1 / 19,714 | Brown, DarkGrey, Grey, White | 838,012 / `70672a5095c46a5f04dda5b82f341b83401bf8711d795426f2c08970f910cdd8` |

## Rebuild and verification

Run with Blender 4.5 or newer:

```text
blender --background --python scripts/blender/build_quaternius_buildings_pack.py
```

The script reimports every generated GLB and fails unless its dimensions,
horizontal centering, ground contact, mesh statistics, complete triangle-used material-name
set, per-material triangle assignment, major PBR values, embedded provenance,
opaque solid-material alpha, and every triangle's positions, normals, and UVs
match within strict tolerances.
Degenerate source triangles retain their positions and material assignment, but
their undefined zero-length normals are intentionally excluded from comparison.
The table above records the successful Blender 4.5.10 LTS round trip performed
on 2026-08-30. A second complete run produced identical SHA-256 hashes for all
nine GLBs.

Quantized source-geometry fingerprints used by the verified build:

- `building1-large.glb`: `f85a66cf33be2de6ae43b061cafb3b7260758871d83e71fb94c4b9ca7d166430`
- `building1-small.glb`: `012ea1d7ea16675e0552d563ae119b3d37df53e48adb990f2419018c0477c865`
- `building2-large.glb`: `1512c61587e29976c61d670adf9ab2937f214ef492ed7248ae003886134bdcd0`
- `building2-small.glb`: `e8bf3959288f0fc7c1f6ed92d47e0405c7320571d797576f5fc3486b6a080955`
- `building3-big.glb`: `093ad2c654540281eeca833e37c39cdf25c20aa5c8b1e629b71fabad61cc77cd`
- `building3-small.glb`: `60a96d21e91c0721f24529cd770116a49f5ebd03ed59066889cfe36eb0064eb9`
- `building4.glb`: `294c2f13448272adb0904554c1eeb8eb9e848aefa9a2d244a2d30e178bed5a2c`
- `house1.glb`: `f638f2b8037bd446640a980cc532b7bc0fe2c743465f25ef4199ee9dc063de80`
- `house2.glb`: `610eaa659482776ebbb580167ff610812dfe0cc40601cfdcae0c18f21a06f6e8`

Creator: Quaternius

Official source: https://quaternius.com/packs/buildings.html

License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/

Acquisition date: 2026-08-28

Local license evidence: `source_art/third_party/quaternius_buildings_pack/`
