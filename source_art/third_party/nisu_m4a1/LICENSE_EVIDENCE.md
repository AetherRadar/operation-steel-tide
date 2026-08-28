# Nisu M4A1 Source and License Evidence

## Official record

- Asset: **M4A1 Assault Rifle**
- Creator and uploader: **nisu**
- Official source page: https://opengameart.org/content/m4a1-assault-rifle
- Original publication date: 2022-04-24
- Official download: https://opengameart.org/sites/default/files/m4a1_0.zip
- Acquisition date: 2026-08-28
- Exact license displayed by the source page: CC0 1.0 Universal
- License deed: https://creativecommons.org/publicdomain/zero/1.0/
- Official download ZIP SHA-256:
  `ED5779EC82718861964227E2AAD2A900978EA087081154365D6D86246BE62F0D`

The official source page is preserved as
`opengameart_license_evidence.png` (38,898 bytes; SHA-256
`80F422F8C955D9374D4831F3715681E15E8217B3E930B731561A70F7048FFB7C`).
The downloaded ZIP does not contain a separate license file. The historical
`3dmodelscc0.com` domain associated with the asset is unavailable and is not
used as license evidence; this record relies on the official OpenGameArt page.

## Extracted source hashes

| Local file | SHA-256 |
| --- | --- |
| `M4A1.fbx` | `D66516A3455B975027556394B9AFB92A1E9093D700CCF779AAB223CD6AC4076E` |
| `M4A1_Base_Color.png` | `E28FB02A6A1D951EC2D356FAD8F267BA04DA7D294BFA001EB3995549971E1B34` |
| `M4A1_Height.png` | `DF12E21E2C5A8553BB7B5E9BEE80477A123F3C5CDA5CF55D50D739F69A51B904` |
| `M4A1_Metallic.png` | `E9CC71FF7886C552030586C650E67C78A86C8B2A812357E40EDC19D5BC9426CA` |
| `M4A1_Normal.png` | `9DA0B6DA232CC676482EEB2B21FDFE04F03504A0E37407F3BBC35556335BD998` |
| `M4A1_Roughness.png` | `708EFF9DF647F0ECF258467E983052E164286D1DC579E763A6D8261074D3EB9A` |

## Adaptation and output mapping

`scripts/blender/build_nisu_m4a1.py` imports `M4A1.fbx`, rebinds the 2K
base-color, metallic, roughness, and normal maps as a PBR material. The supplied
height map remains among the tracked acquisition inputs. The script applies the
project's `2.36` authored-space scale, removes zero-area faces, and reorganizes
the source meshes under the project's stable weapon nodes. In particular, the
active `Magazine`, `SpareMagazine`, `ChargingHandle`, and `Stock` nodes own
movable nisu geometry. The normal `MuzzleDevice` is split from the nisu
`Barrel`; the complete `Foregrip`, `Suppressor`, and `OpticMount` geometry is
adapted from the separately tracked Quaternius CC0 `scarl.glb`, `mp5a5.glb`,
and `axmc.glb` sources. Their exact source-pack filenames, objects, acquisition
date, license, and runtime mapping are recorded in
`../../../assets/models/steel_tide_m4a1/LICENSE.md` and
`../../../assets/models/quaternius_ultimate_guns/LICENSE.md`.

The reproducible outputs are:

- Editable DCC adaptation:
  `../../combat_models/steel_tide_m4a1.blend`
- Godot runtime model:
  `../../../assets/models/steel_tide_m4a1/steel_tide_m4a1.glb`

Godot may extract temporary `steel_tide_m4a1_*.png` cache copies from the
self-contained GLB during import. Those generated cache files are ignored and
are not acquisition sources.

CC0 does not require attribution, but both nisu and Quaternius are retained for
provenance. The redistributed sources and composite model outputs retain their
CC0 provenance and are not represented as MIT-licensed project-authored art.
Only the project-authored adaptation script is covered by the repository's root
MIT license, subject to `../../../docs/CONTENT_PROVENANCE.md`.
