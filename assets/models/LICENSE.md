# Model Asset Licenses

## Project-authored combat models

The Steel Tide M4A1 and operator models are generated from `scripts/blender/generate_combat_models.py`. Their editable `.blend` sources are tracked under `source_art/combat_models/`. They contain no copied third-party geometry or textures and are covered by the repository's root MIT license, subject to the disclosure in `docs/CONTENT_PROVENANCE.md`.

## Poly Haven CC0 models

The following models are from [Poly Haven](https://polyhaven.com/) and are dedicated to the public domain under CC0:

- Old Military Crate: https://polyhaven.com/a/old_military_crate
- Concrete Road Barrier: https://polyhaven.com/a/concrete_road_barrier
- Street Lamp 01: https://polyhaven.com/a/street_lamp_01
- Metal Trash Can: https://polyhaven.com/a/metal_trash_can
- Coffee Cart 01: https://polyhaven.com/a/CoffeeCart_01
- Wooden Crate 01: https://polyhaven.com/a/wooden_crate_01
- Plastic Crate 01: https://polyhaven.com/a/plastic_crate_01
- Wicker Basket 01: https://polyhaven.com/a/wicker_basket_01

License: https://polyhaven.com/license

The six residential-street additions were acquired on 2026-08-28. Creator
credits, exact CC0 terms, official API revision identifiers, source-to-runtime
mapping, per-file MD5 and SHA-256 evidence, and Blender processing details are
recorded in `polyhaven_residential_street/LICENSE.md`.

## Kenney CC0 models

The City Kit (Industrial) 1.0 model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/city-kit-industrial
- Download: https://kenney.nl/media/pages/assets/city-kit-industrial/5fcb837741-1750838303/kenney_city-kit-industrial_1.0.zip
- Local assets: `kenney_city_kit_industrial/*.glb`
- License copy: `kenney_city_kit_industrial/KENNEY_LICENSE.txt`

The complete GLB set is retained so Tideforge, Harbor Locks, the freight terminal, and residential rooftop dressing can combine the pack's buildings, chimneys, and tank detail without modifying third-party geometry. Runtime placement is visual-only where an existing collision shell already owns gameplay geometry.

The Factory Kit 3.0 model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/factory-kit
- Download: https://kenney.nl/media/pages/assets/factory-kit/edaac9d4f6-1777639602/kenney_factory-kit_3.0.zip
- Acquisition date: 2026-08-19
- Local assets: selected `kenney_factory_kit/*.glb` models and the original `kenney_factory_kit/Textures/colormap.png` material atlas
- License copy: `kenney_factory_kit/KENNEY_LICENSE.txt`

The authored overhead door is used for the interactive entrances to the Grand Hotel and Municipal Treasury in Saint Marais Old Town.

The Furniture Kit 1.0 model set is distributed by Kenney under CC0 1.0:

- Source: https://kenney.nl/assets/furniture-kit
- Acquisition date: 2026-08-26
- Local assets: selected `kenney_furniture_kit/*.glb` interior props used by searchable furniture and apartment room dressing
- License copy: `kenney_furniture_kit/KENNEY_LICENSE.txt`

Selected cabinets, desks, beds, tables, fridges, and crates replace programmer-art boxes inside enterable residential rooms.

## Quaternius CC0 models

The Ultimate Modular Women character pack is distributed by Quaternius under
CC0 1.0 Universal:

- Creator: Quaternius (`@Quaternius`)
- Source: https://quaternius.com/packs/ultimatemodularwomen.html
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-27
- Runtime assets: `quaternius_operators/viper.glb`, `heron.glb`,
  `lynx.glb`, `magpie.glb`, and `jackal.glb`
- License, per-role source mapping, and rebuild record:
  `quaternius_operators/LICENSE.md`
- Editable source: `source_art/third_party/quaternius_modular_women/`

Five different authored presets supply distinct heads, clothing, equipment
silhouettes, and materials for the complete extraction roster. The runtime
files remain derived from those same five Quaternius presets; they are refined
in Blender through selective subdivision, shape-preserving creases, smooth
shading, role-aware scalar PBR material parameters, and four-influence
normalized skinning before re-export. This adaptation adds no image textures
or new UV artwork and preserves the shared node, equipment-socket, and
25-action animation contract. The actions are retargeted from the CC0
Quaternius Universal Animation Library. Attribution is not required; creator
credit is retained as a courtesy.

The Ultimate Guns Pack is distributed by Quaternius under CC0 1.0 Universal:

- Source: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-20
- Local assets: `quaternius_ultimate_guns/*.glb`
- License and source-file mapping: `quaternius_ultimate_guns/LICENSE.md`

Ten selected authored models replace the runtime primitive visuals for AK-74N,
SCAR-L, M24, AXMC, AWM, VSS, MP5A5, M3A1, P226, and M1911.

## DJMaesen CC BY 4.0 first-person arms and SMG

The realistic first-person arms and SMG-45 visuals are adapted from **fps
animated smg** by **DJMaesen**, licensed under **CC BY 4.0**. Full attribution,
the original source mapping, and the reproducible Blender build are recorded in
`djmaesen_smg45/LICENSE.md`.

The standard free version of the Downtown City MegaKit is distributed by Quaternius under CC0 1.0 Universal:

- Creator: Quaternius (`@Quaternius`)
- Source: https://quaternius.com/packs/downtowncitymegakit.html
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-19
- Local assets: `quaternius_downtown_city/*.gltf`, matching `.bin` buffers, and shared 1K texture maps
- License copy and processing record: `quaternius_downtown_city/QUATERNIUS_LICENSE.txt` and `quaternius_downtown_city/README.md`

The repository contains 21 selected modular scenes and 26 shared textures for composing Saint Marais Old Town. The metal wall modules are also reused as visual facades on the Tideforge and Harbor Locks collision shells. The user-provided CS:GO Town Sketchfab page was treated as layout reference only; no geometry, textures, or other files from that page are included.

## TastyTony CC BY 4.0 model

The GSh-18 sidearm model is **Low-Poly GSh-18** by TastyTony and is used under Creative Commons Attribution 4.0 International:

- Source: https://sketchfab.com/3d-models/low-poly-gsh-18-7ce65f794f0e42f98f61a96026e4d75e
- License: https://creativecommons.org/licenses/by/4.0/
- Local asset and attribution: `tastytony_gsh18/low-poly_gsh-18.glb` and `tastytony_gsh18/LICENSE.md`

The model remains credited to TastyTony and is not covered by the repository's MIT license.

## ELIZION CC BY 4.0 model

The Desert Eagle sidearm model is **Desert Eagle** by ELIZION and is used under Creative Commons Attribution 4.0 International:

- Source: https://sketchfab.com/3d-models/desert-eagle-cabde59f5cf24effaf80536e35d04e95
- License: https://creativecommons.org/licenses/by/4.0/
- Local asset and attribution: `elizion_desert_eagle/desert_eagle.glb`, its extracted `desert_eagle_*.png` PBR maps, and `elizion_desert_eagle/LICENSE.md`

The model remains credited to ELIZION and is not covered by the repository's MIT license.

## BAMEN CC BY 4.0 character

The deployment-preview character is **FREE [Military Soldier] RIGGED** by BAMEN and is used under Creative Commons Attribution 4.0 International:

- Source: https://sketchfab.com/3d-models/free-military-soldier-rigged-e9c56308a67d4a3db62e914fafa4d198
- License: https://creativecommons.org/licenses/by/4.0/
- Local asset and attribution: `bamen_military_soldier/bamen_military_soldier.glb` and `bamen_military_soldier/LICENSE.md`
- Editable and original source: `source_art/third_party/bamen_military_soldier/`

The model remains credited to BAMEN and is not covered by the repository's MIT license.

## Tide Hunter CC0 monster

The roaming Boss uses **3D Horror Game Monster** by HorrorGameMaker.com:

- Source: https://opengameart.org/content/3d-horror-game-monster
- License: CC0 / Public Domain, as marked on the source page
- Acquisition date: 2026-08-20
- Local asset: `tide_hunter_monster/tide_hunter_monster.glb` with its three tracked `tide_hunter_monster_test_StingrayPBS1SG_*.png` PBR maps
- Editable packed source and reproducible cleanup script: `source_art/third_party/tide_hunter_monster/tide_hunter_monster.blend` and `scripts/blender/build_tide_hunter_monster.py`

The Boss is a single skinned mesh with embedded PBR maps and `idle`, `walk`, and `run` actions. Credit is retained as courtesy.

## Quaternius CC0 animation libraries

The field-operator animation set combines the standard free exports from
Quaternius Universal Animation Library and Universal Animation Library 2:

- Creator: Quaternius (`@Quaternius`)
- Sources: https://quaternius.itch.io/universal-animation-library and https://quaternius.itch.io/universal-animation-library-2
- Exact license: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/
- Acquisition date: 2026-08-20
- Local source exports: `source_art/third_party/quaternius_universal_animation_library/UAL1_Standard.glb` and `UAL2_Standard.glb`
- License evidence: `source_art/third_party/quaternius_universal_animation_library/LICENSE.txt` and `UAL2_LICENSE.txt`
- Blender output: `source_art/third_party/bamen_military_soldier/bamen_military_soldier_animated.blend` and `assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb`

The checked-in standard exports are the root-motion-disabled versions. The
retargeting script keeps navigation in Godot authoritative and uses Blender
to add prone/downed integration, recovery poses, and attachment sockets.
