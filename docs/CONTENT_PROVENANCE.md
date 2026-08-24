# Content Provenance

This document records the known origin and licensing status of content shipped in or displayed by Operation Steel Tide. It is an audit aid, not a representation that the project satisfies every community's AI-content policy.

## Development disclosure

Operation Steel Tide is an AI-assisted solo prototype. AI tools contributed to portions of implementation, documentation, and project-authored presentation work. The repository owner reviewed, integrated, debugged, and validated the resulting project, but cannot verify that every model involved was trained exclusively on material submitted with each original creator's consent.

Consequently, this repository must not be described as satisfying policies that require that specific training-data proof. In particular, this document does not establish compliance with r/godot Rule #10.

## Content inventory

| Content | Origin | Rights or license | Evidence |
| --- | --- | --- | --- |
| C#, Go, scene, configuration, and documentation files | Project-authored, with disclosed AI assistance | Released by the repository owner under the root MIT license, subject to the limitation above | Git history and root `LICENSE` |
| `docs/media/*` gameplay images | Direct captures of the running Godot project | Project screenshots; depicted third-party assets retain their source licenses | Capture commands documented in `README.md` and asset records below |
| `assets/branding/operation-steel-tide-icon.svg` | Created for this project; AI assistance may have contributed | Included under the root MIT license, subject to the limitation above | Git history |
| Rescue tilt-rotor `.blend` and `.glb` model | Project-authored in Blender from the checked-in procedural modeling script, with disclosed AI assistance | Included under the root MIT license, subject to the limitation above | `scripts/blender/build_extraction_aircraft.py`, `source_art/extraction_aircraft/extraction_aircraft.blend`, and Git history |
| Steel Tide M4A1 and operator `.blend` and `.glb` models | Project-authored in Blender from the checked-in procedural modeling script, with disclosed AI assistance; no third-party geometry or textures copied | Included under the root MIT license, subject to the limitation above | `scripts/blender/generate_combat_models.py`, `source_art/combat_models/`, and Git history |
| Low-Poly GSh-18 sidearm model and centered runtime adaptation | TastyTony on Sketchfab | CC BY 4.0; attribution required | `assets/models/tastytony_gsh18/LICENSE.md`, `scripts/blender/build_tastytony_gsh18.py`, and the creator/source metadata embedded in the source GLB |
| Desert Eagle sidearm model | ELIZION on Sketchfab | CC BY 4.0; attribution required | `assets/models/elizion_desert_eagle/LICENSE.md` and the creator/source metadata embedded in the GLB |
| Deployment-preview military soldier | BAMEN (`bamenwo05`) on Sketchfab | CC BY 4.0; attribution required | `assets/models/bamen_military_soldier/LICENSE.md`, retained original FBX, cleaned Blender source, and reproducible import script |
| Tide Hunter roaming Boss monster | HorrorGameMaker.com on OpenGameArt | CC0 / Public Domain as marked on the source page | `assets/models/tide_hunter_monster/LICENSE.md`, `source_art/third_party/tide_hunter_monster/tide_hunter_monster.blend`, and `scripts/blender/build_tide_hunter_monster.py` |
| Field operator animation clips | Quaternius Universal Animation Library and Universal Animation Library 2 | CC0 1.0 Universal; no attribution required, creator credit retained as courtesy | Acquired 2026-08-20 from the two official itch.io pages; standard GLB exports, license copies, and source mapping are in `source_art/third_party/quaternius_universal_animation_library/`; retargeted output is `assets/models/bamen_military_soldier/bamen_military_soldier_animated.glb` |
| AK-74N, SCAR-L, M24, AXMC, AWM, VSS, MP5A5, M3A1, P226, and M1911 weapon visuals | Selected authored GLB models from Quaternius Ultimate Guns Pack | CC0 1.0 Universal; no attribution required, creator credit retained as courtesy | Acquired 2026-08-20 from `https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F`; exact source-file mapping and license record are in `assets/models/quaternius_ultimate_guns/LICENSE.md` |
| First-person tactical arms and SMG-45 weapon visual, including the original static arm export and static rifle/two-handed service- and large-pistol arm poses | DJMaesen on Sketchfab; static poses evaluated and exported in Blender from the tracked source GLB | CC BY 4.0; attribution required | Source acquired 2026-08-21; new static pose variants generated 2026-08-24. Original GLB metadata, all runtime GLB/texture adaptations, and reproducible Blender sources are recorded in `assets/models/djmaesen_smg45/LICENSE.md`, `source_art/third_party/djmaesen_fps_smg45/`, `scripts/blender/build_djmaesen_smg45.py`, and `scripts/blender/build_first_person_arms.py` |
| Old Military Crate and Concrete Road Barrier | Poly Haven | CC0 | `assets/models/LICENSE.md` |
| City Kit (Industrial) 1.0 GLB model set | Kenney | CC0 1.0 | `assets/models/LICENSE.md` and `assets/models/kenney_city_kit_industrial/KENNEY_LICENSE.txt` |
| Factory Kit 3.0 authored overhead door | Kenney | CC0 1.0 | Acquired 2026-08-19 from `https://kenney.nl/assets/factory-kit`; local asset and license copy in `assets/models/kenney_factory_kit/` |
| Downtown City MegaKit standard modular scene set | Quaternius | CC0 1.0 Universal | Acquired 2026-08-19 from `https://quaternius.com/packs/downtowncitymegakit.html`; selected-file mapping and license copy in `assets/models/quaternius_downtown_city/` |
| Asphalt 03, Concrete Floor, Rusty Painted Metal, Corrugated Iron, and Gravel Embedded Concrete | Poly Haven | CC0 | `assets/textures/LICENSE.md` |
| Runtime primitive meshes, materials, UI, effects, and synthesized sounds | Generated by project code at runtime | Project-authored implementation, with disclosed AI assistance | Source files and Git history |

No separate third-party music, font, or stock-image collection is currently tracked in the repository. The Quaternius CC0 animation libraries listed above are the tracked animation pack.

## Posting guidance

- Credit TastyTony for the Low-Poly GSh-18 model and retain its source and CC BY 4.0 license record in distributions.
- Credit ELIZION for the Desert Eagle model and retain its source and CC BY 4.0 license record in distributions.
- Credit BAMEN for the deployment-preview military soldier and retain its source and CC BY 4.0 license record in distributions.
- Credit HorrorGameMaker.com for the Tide Hunter monster as a courtesy and retain the CC0 source record above.
- Quaternius animations are CC0; retaining the creator and source links above is recommended for provenance even though attribution is not required.
- Quaternius weapon models are CC0; retain the pack link and platform mapping so generic silhouettes are not mistaken for manufacturer-authenticated replicas.
- Credit DJMaesen for the FPS animated SMG source and retain the CC BY 4.0 license record in distributions.
- Credit Poly Haven and link the two asset license records whenever a platform requires attribution or source disclosure.
- Describe screenshots as direct in-engine captures, not as independently generated promotional images.
- Disclose AI assistance plainly. Do not claim that human review, ownership of output, or the MIT license proves anything about model training data.
- Do not repost this project to a community that requires consent-only model-training proof unless its moderators explicitly confirm that the available evidence is sufficient.
- A compliant alternative would require a genuinely traceable content pipeline accepted by that community, not merely removal of the disclosure or rewording of the post.

## Maintainer checklist

When adding a binary asset, record its creator, source URL, exact license, and any attribution requirement before commit. When replacing an asset, remove stale attribution entries. Keep promotional media traceable to an in-engine capture command or another documented source.
