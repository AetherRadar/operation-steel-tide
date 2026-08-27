# Poly Haven Residential Street Models

These assets were acquired from Poly Haven on 2026-08-28. Poly Haven releases
its assets under the **CC0 1.0 Universal** public-domain dedication:

- Poly Haven license: https://polyhaven.com/license
- Exact license: https://creativecommons.org/publicdomain/zero/1.0/
- Attribution requirement: none; creator credit is retained for provenance.

The repository preserves each official 1K glTF, its referenced binary buffer,
and every referenced 1K texture without modification. The `files_hash` values
below are the revision fingerprints returned by Poly Haven's official info API.
The per-file MD5 values are from the official files API and were verified after
download with the user agent
`OperationSteelTide-AssetAcquisition/2026-08-28 (public MIT game; contact via repository)`.

| Asset | Creator | Official source | API `files_hash` | Local source |
| --- | --- | --- | --- | --- |
| Street Lamp 01 | Josh Dean | https://polyhaven.com/a/street_lamp_01 | `002d697fa195d73f627e82804612208853212519` | `street_lamp_01/street_lamp_01_1k.gltf` |
| Metal Trash Can | GurJas Studios | https://polyhaven.com/a/metal_trash_can | `3ecbee3adbcc53b4af3c8f010e76e10c5b7558a2` | `metal_trash_can/metal_trash_can_1k.gltf` |
| Coffee Cart 01 | Joe Seabuhr | https://polyhaven.com/a/CoffeeCart_01 | `9a4acb5ef57be1f96cd1af66af66d2a798139a24` | `CoffeeCart_01/CoffeeCart_01_1k.gltf` |
| Wooden Crate 01 | James Ray Cock | https://polyhaven.com/a/wooden_crate_01 | `1ac5d053ce4c72e3fbb4c3065e7979522bf1908e` | `wooden_crate_01/wooden_crate_01_1k.gltf` |
| Plastic Crate 01 | PierreB3D | https://polyhaven.com/a/plastic_crate_01 | `74d27ffa75c1d477708f4dd344d500b65d376a84` | `plastic_crate_01/plastic_crate_01_1k.gltf` |
| Wicker Basket 01 | Kuutti Siitonen | https://polyhaven.com/a/wicker_basket_01 | `49f624a285b5e5dc8fe4b75ad18cf5d18d8cdca1` | `wicker_basket_01/wicker_basket_01_1k.gltf` |

Official metadata and file manifests can be independently checked at
`https://api.polyhaven.com/info/<asset-id>` and
`https://api.polyhaven.com/files/<asset-id>`.

## Runtime mapping and Blender processing

The lamp, coffee cart, crates, and basket use their official 1K glTF paths
directly. Blender 4.5.10 LTS inspection confirmed that glTF Y-up is converted
to Blender Z-up as expected, the models retain real-world metre scale, and no
geometry correction is needed. Godot imports the original glTFs in Y-up space.

The official Metal Trash Can scene contains two complete authored variants
side by side: a clean can and a rusted can, each with two handles and a lid
leaning against it. The source scene remains intact. The reproducible Blender
script `scripts/blender/build_polyhaven_metal_trash_cans.py` selects the four
authored meshes for each variant, preserves their size and materials, centres
the composition on X/Y, grounds it at Z=0, and exports:

| Runtime GLB | Dimensions (m) | Triangles | SHA-256 |
| --- | --- | ---: | --- |
| `metal_trash_can/metal_trash_can_clean.glb` | 0.846935 x 0.556290 x 0.906160 | 6,428 | `12ac8fc373317b547f977b3a45f69ad100b3551563935dba7a6653e06186e69a` |
| `metal_trash_can/metal_trash_can_rust.glb` | 0.849762 x 0.549013 x 0.906157 | 7,532 | `8379f4732bd93b02bd6739564dd8efdcbae3aa7a8cba3f7a7dbb6bab97f18165` |

Blender reports a glTF-export warning because the imported Poly Haven packed
ARM material exposes more than one image texture node to one material input.
The official source glTF is unaffected. The two derived GLBs re-import with the
expected mesh, triangle, material, scale, centring, and ground bounds.

Both runtime GLBs embed their texture images. Godot 4.6 may extract six renamed
cache JPGs beside the GLBs when `gltf/embedded_image_handling=1`; the repository
root `.gitignore` lists those six paths exactly. They are generated import
caches, not provenance evidence or part of the tracked asset payload.

## Verified official file hashes

Paths are relative to this directory. Both columns were computed from the
tracked bytes; every MD5 matched the value returned by Poly Haven's files API.

| File | Official API MD5 | Repository SHA-256 |
| --- | --- | --- |
| `street_lamp_01/street_lamp_01_1k.gltf` | `d29d1fc30bbdad99a58371754f144e65` | `5d0358ede168b5e04547780b99d8e6d651cbe644e468e67cb505019047cbd5c8` |
| `street_lamp_01/street_lamp_01.bin` | `324bc6295e3962322514ddcfc7037b23` | `c283db9b6d95835d9e8fc1692829acadcb21a0623b2daaa7231cec1440488401` |
| `street_lamp_01/textures/street_lamp_01_arm_1k.jpg` | `63d980174db10cf22a1b175743c465c9` | `4b5487a6c7ee300c3cf5449629e01c35409e5c7ac1695edb9fe69d4821853c7c` |
| `street_lamp_01/textures/street_lamp_01_diff_1k.jpg` | `82efb7531a25cda3ef8af2aff8ecccf7` | `3e9d3471b33eb1f8075ed741c2b1932103598626d057ca12ef38005ea5fcb165` |
| `street_lamp_01/textures/street_lamp_01_nor_gl_1k.jpg` | `c27d8102c7e851a8f0f654bb959b9d67` | `172af0fd7ed4999e9f520b9f56e1f357a6dba87adbd7b8aa87e0a0b95840c63d` |
| `metal_trash_can/metal_trash_can_1k.gltf` | `c322802832a61ad7b17711dfbbc52e6b` | `ccdc55ecfe43db2174c9257891e92551882a874564bb9a7908cafc5268b46c0c` |
| `metal_trash_can/metal_trash_can.bin` | `fbde1679a66ea644223b97ebc8336a77` | `a9b9f526b655bef865933335f2cfc8c8f0bd1a35fda05b290a93e3061f2b6c8a` |
| `metal_trash_can/textures/metal_trash_can_arm_1k.jpg` | `af676572c13fae2bb584538f03175efc` | `b23dcdb2ed40c1c6cfbd2d2779c6f075025b7269bfbbed2a70c6bdb7167e47fb` |
| `metal_trash_can/textures/metal_trash_can_diff_1k.jpg` | `55d375d8d51b268b6ed8a4308155959c` | `920c895a31b1cd2c000123c0592c70c33d46562752987da98070e7e0eebb662f` |
| `metal_trash_can/textures/metal_trash_can_nor_gl_1k.jpg` | `fa6db04e021f6335ae2d9ad501120cae` | `b977f17aa9d4718ffc91a62868b7171b4479578b8cffd1d32001d92552af81a4` |
| `metal_trash_can/textures/metal_trash_can_rust_arm_1k.jpg` | `ed9f635917cde411f235d4585c90b310` | `31a6269e98a3a8b2a2d6db8f58724e0151d5312b5137e6d59b5a2ba873989159` |
| `metal_trash_can/textures/metal_trash_can_rust_diff_1k.jpg` | `8b90447d250cb790ca537748a2b16691` | `51c44248305b111d978f7e0cbfe4a71ab7f49549968ce37c54b635d74c2cfba1` |
| `metal_trash_can/textures/metal_trash_can_rust_nor_gl_1k.jpg` | `25187818d1236815d4a34fd743cadc29` | `a5497588e00e5e56db72e701457fc8a46d3a1fa7d9dd7bf9d79e02cada09e84b` |
| `CoffeeCart_01/CoffeeCart_01_1k.gltf` | `29a775b98a898b38dc8a34b53e2a65e9` | `3f4e7e79e751662a767c22b21c34dfb6f7d414d56dda60a236ad7d621d211934` |
| `CoffeeCart_01/CoffeeCart_01.bin` | `ef3a282d8fbf3ab6c5d91219be61e69a` | `0c0f9ed417ba85208c7f42b674ee3ce99a7e6fb63e67c7ea8d312304c470db50` |
| `CoffeeCart_01/textures/CoffeeCart_01_cart_arm_1k.jpg` | `83254d27de5af53360d06f774d94baa9` | `7481660c7b4b0c676c9143a9ae497dfa6da2c509a0e1db8080c0dfb498279175` |
| `CoffeeCart_01/textures/CoffeeCart_01_cart_diff_1k.jpg` | `d8fbad3535234e02aa278ec6dc74d15e` | `654ead6c2e651e6aa3a9166e9b94333f41b33e31fcd9c75b4058e40744108bcb` |
| `CoffeeCart_01/textures/CoffeeCart_01_cart_nor_gl_1k.jpg` | `52e3125e23024b146ffe195488283641` | `160121cfeb3e9a6a029fd6b7be0e282c1f99abeb1b34d197dbc99d9fcaeea1f7` |
| `CoffeeCart_01/textures/CoffeeCart_01_props_arm_1k.jpg` | `b4b2e1aa668e757e67666cdf092ddd54` | `8bc6a844a23d1ccb441d40e912adf8c38c6a3f7daf468598ae990a90d73733e8` |
| `CoffeeCart_01/textures/CoffeeCart_01_props_diff_1k.jpg` | `022cf4bfd25543f932efd1786d2961e7` | `2583de82b450923b75eb62b9e24fca9c252969fd249e91e7bbbbf3cfbad5e703` |
| `CoffeeCart_01/textures/CoffeeCart_01_props_nor_gl_1k.jpg` | `119787e463389d7877b4bfa96cc569ca` | `8546f2ba3cfb14e9aef8e24e695a30117b03076071f11d877c15007793b3ca9e` |
| `wooden_crate_01/wooden_crate_01_1k.gltf` | `33df4d2ae00186d4b64e878466c9e22e` | `fd9c6073acfc671e12b64666053f2805ae33aff5d3dbbcfe5c97c08c06644927` |
| `wooden_crate_01/wooden_crate_01.bin` | `8318f76b0c24b5538ce23051dbcb79a0` | `85f381f8035f7a6f2f3b848640894c6d3333b261946dd8a338c0fe19e0bea21e` |
| `wooden_crate_01/textures/wooden_crate_01_arm_1k.jpg` | `67c4aeb1154af98f95516b21ef62291c` | `cd0b031c3924905e16ffd78ead062c2cc274148c4104d09635310097738b4679` |
| `wooden_crate_01/textures/wooden_crate_01_diff_1k.jpg` | `266e0462d06e77c5745f66e89820544f` | `16653f4abaf4c1d9bca94bde3bd8c54d0ea9a5298d1648abcf57de1f7ecc34cc` |
| `wooden_crate_01/textures/wooden_crate_01_nor_gl_1k.jpg` | `e31626a1fcf6f7062c2ffd9fc982b942` | `f4ea44c7dc1686123d337039cb713d74e8a2dbc6d1bd2beeac276d06f5df3fab` |
| `plastic_crate_01/plastic_crate_01_1k.gltf` | `af44f97630da80a640c5e6d70e72c56b` | `42556fc8e39e5834ebf474dff79d4c0524fd3074be6c86c0c6704a10be8b3756` |
| `plastic_crate_01/plastic_crate_01.bin` | `68c6e126a29ad4b72c21aa85f89a48c1` | `59b307238a27f304739aeef0b1c9622d1e4efb31a96d805d2cfc0c461f9bb06d` |
| `plastic_crate_01/textures/plastic_crate_01_arm_1k.jpg` | `a0c021fba58dc9fb16df94b1fdda2308` | `9a4af28d6fe8d5459a3c1a8706c0f8877221873ac9b83de580e6cfa63c3ecaa0` |
| `plastic_crate_01/textures/plastic_crate_01_diff_1k.jpg` | `0250b2750da634e95ef44b6ffb11681e` | `d9109a42e397e39bbd7f42976d93122dbd5ccdef8ff64bce353571e1c1b994f6` |
| `plastic_crate_01/textures/plastic_crate_01_nor_gl_1k.jpg` | `95f73143467ebbf4c55fb599269edc7b` | `6f0c44778216d50672d6a23660b22dec8b96bb6e71518d699c24630c34cf9217` |
| `wicker_basket_01/wicker_basket_01_1k.gltf` | `6910bb33f9b29c30e41c06342cba4f9b` | `9ad86c33f00f46a18c9bc8bfc28c0036090ee819f7270d0028ec408512c04094` |
| `wicker_basket_01/wicker_basket_01.bin` | `154e2cd9157175591e3585e706925fa9` | `8e96025ffcdc64a7e75d1aa980f2491b1219912a4554272cd1e192a966c399f8` |
| `wicker_basket_01/textures/wicker_basket_01_arm_1k.jpg` | `e456e1e051da9b8433162de9cb96cb61` | `5150638cbcd8f1bc07b76af3b60b31a4c5e7d38ce66ac8906b9c3192f3283b9a` |
| `wicker_basket_01/textures/wicker_basket_01_diff_1k.jpg` | `54c05d9c5e4f92930cbbf67e4770fc61` | `15156c7855d5642f6bb594edfad5bcdcfe11f87f3e109956cb9e8fdb15fef921` |
| `wicker_basket_01/textures/wicker_basket_01_nor_gl_1k.jpg` | `4cca87156e77045fd29562b09609b41d` | `d06024a37ef330d05bf128b8488f72769af05e72d266ffbc810d7f09ea2b32f9` |
