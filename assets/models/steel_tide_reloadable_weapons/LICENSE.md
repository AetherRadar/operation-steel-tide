# Steel Tide Reloadable Weapons

## Quaternius source and rights

- Original creator: Quaternius (`@Quaternius`).
- Official pack page: https://quaternius.com/packs/ultimategun.html
- Tracked bundle page: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- Exact source license: CC0 1.0 Universal.
- License deed: https://creativecommons.org/publicdomain/zero/1.0/
- Tracked-source acquisition date: 2026-08-20.
- DCC mechanism adaptation and review date: 2026-08-29; MP5A5 independent
  iron-sight split revised and reviewed 2026-08-30.
- Attribution required for the Quaternius source: No. Creator credit and source
  links are retained as provenance.

The finished Quaternius weapon bodies, their materials, and the MP5A5's
separated authored magazine remain CC0 and are not relicensed as MIT. The
project-authored mechanism geometry and reproducible builders are covered by
the repository's root MIT license. Each combined runtime GLB and preview
contains both categories and must retain the source mapping below. Gameplay
names describe silhouette mappings; they do not claim manufacturer-authenticated
replicas.

All visible project mechanisms are authored in Blender as custom lofted,
lathed, or swept meshes with applied bevels, weighted normals, and scalar PBR.
They are not Godot runtime primitives, CSG objects, or empty marker substitutes.

## MP5A5 authored-magazine adaptation

The immutable source is `../quaternius_ultimate_guns/mp5a5.glb`, mapped from
the pack file `Submachine Gun.glb` (`SubmachineGun_2`). The derivative preserves
all 1,374 source triangles. A welded-topology flood separates the complete
authored 60-triangle external magazine from the 1,314-triangle body. Two
additional audited welded islands expose the 94-triangle front and 14-triangle
rear mechanical sights beneath dedicated visibility nodes, leaving a
1,206-triangle main body. An independent 60-triangle visible copy supplies the
spare.

The static source does not contain a separable charging control, so Operation
Steel Tide hand-models a five-ring swept MP5 tubular handle. Its neck, angled
stem, and enlarged knob form one 120-triangle mesh with weighted normals and
blued-metal scalar PBR. The delivered scene contains 1,554 triangles.

```text
SteelTideReloadableMP5A5
|- WeaponBodyGeometry                  [1,206 source triangles]
|- FrontIronSight
|  `- FrontIronGeometry                  [94 source triangles]
|- RearIronSight
|  `- RearIronGeometry                   [14 source triangles]
|- Magazine
|  |- MagazineGeometry                    [60 source triangles]
|  `- MagazineGripSocket
|- SpareMagazine
|  `- SpareMagazineGeometry               [60 source-derived triangles]
|- ChargingHandle
|  |- ChargingHandleGeometry             [120 project triangles]
|  `- ChargingHandleSocket
|- MagazineWellSocket
|- OpticRailSocket
|- MuzzleSocket
|- PrimaryGripSocket
`- SupportGripSocket
```

The installed bounds are `(-0.102000,-0.267235,-0.850000)` to
`(0.046081,0.267235,0.320000)`, exactly 1.17 m stock-to-muzzle. The full staged
scene bounds are `(-0.321852,-0.687235,-0.850000)` to
`(0.046081,0.267235,0.320000)`. Coordinates are root-local Godot metres: X is
lateral, Y is up, positive Z points to the stock, and negative Z points to the
muzzle.

The shared `ChargingHandle` rest stays at `(-0.052,0.108,-0.648)`. Its hand
socket is no longer the mechanism origin: the builder selects the upper vertex
on the handle's real outer terminal ring. After GLB round trip the socket is
`(-0.049999997,0.016999997,-0.013000011)` in handle-local space and
`(-0.101999998,0.125000000,-0.661000013)` in root space. Its measured distance
to the 120-triangle visible action surface is exactly `0` m.

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| Source `../quaternius_ultimate_guns/mp5a5.glb` | 70,976 | `69DF22D1AA8603D66366D20C46755CCA2A19E1CABE8C1DB1B72EDB491AE48699` |
| Runtime `mp5a5_reloadable.glb` | 84,528 | `55A33356F659CC9A6EC68FE3DE68A4B3D3C63DDAFCC45FE918DB1CC9FFBF1BDF` |
| Review `mp5a5_reloadable_preview.png` | 1,428,041 | `BE4CF860018E8023E0AE6ED61F83B2030C8E4525FCB7785A03062641B038998C` |
| DCC source `../../../source_art/reloadable_weapons/mp5a5_reloadable.blend` | 699,430 | `C2974747AB626E168047066166DC0AAECBD6A05B29FA12C397AD5E247C810B31` |

`../../../scripts/blender/build_reloadable_quaternius_weapons.py` verifies the
source hash/topology/bounds, welded magazine island and material partition, the
unique 94-/14-triangle front/rear sight islands and their exact bounds,
source-triangle conservation, independent magazine data, visible action,
visibility-node hierarchy, per-node materials/topology, metre scale, sockets,
action-terminal region and triangle-surface distance, output bounds, hashes,
and Blender glTF round trip. Its reviewed GLB and preview hashes are stable
across two headless Blender
4.5.10 LTS rebuilds. Blender may rewrite `.blend` container metadata when the
editable source is saved, so the table records the exact delivered container.

## Supplemental mechanism adaptations

These six derivatives retain every source triangle and canonicalize the source
transform and target length. For P226 and M1911, the builder partitions the
finished source mesh into its fixed frame and complete authored slide; the two
parts still sum exactly to the source topology. Their source files and source
objects are:

| Gameplay mapping | Local source | Pack file | Source object | Bytes | Source triangles | Source SHA-256 |
| --- | --- | --- | --- | ---: | ---: | --- |
| M24 | `../quaternius_ultimate_guns/m24.glb` | `Sniper Rifle-ASOMZIErq3.glb` | `SniperRifle_1` | 76,652 | 1,382 | `A780E291A22BABE8C3472AE9FD0C0F4B98F22382E25C7DA3F5507A5761DFFC5B` |
| AXMC | `../quaternius_ultimate_guns/axmc.glb` | `Sniper Rifle-TKaBjAEofL.glb` | `SniperRifle_3` | 95,356 | 1,722 | `7CDCE34DEC9A9B1AAE6C9E2EF554C88ECDC19554407DECC239B159E13D295F3F` |
| AWM | `../quaternius_ultimate_guns/awm.glb` | `Sniper Rifle-i65hEldsw6.glb` | `SniperRifle_5` | 95,200 | 1,688 | `095E918BD89823B1CA726EAC0016D7C9DAEE15CC6F71010AB251FB0365819F02` |
| VSS | `../quaternius_ultimate_guns/vss.glb` | `Sniper Rifle.glb` | `SniperRifle_4` | 74,692 | 1,344 | `C69B8D4088176580819C20F44FC80D7742E6AD00BA1CA09CD064D8677B1C4BE5` |
| P226 | `../quaternius_ultimate_guns/p226.glb` | `Pistol-J3i9KDQ3kt.glb` | `Pistol_5` | 53,776 | 968 | `4622AB2909AA0F4E88B74A13F52F9E28183A6FF5FCA5896FC7E98D44008F2148` |
| M1911 | `../quaternius_ultimate_guns/m1911.glb` | `Pistol-Z7aOjJu583.glb` | `Pistol_3` | 79,648 | 1,442 | `6DC98CF2E44DC8CD052E402D72B5FE21AF70AFBFC48A70F3139E22008991FD47` |

The DCC mechanisms and actual post-bevel exported topology are:

| Platform | Target length | Loading component | Action | Body / installed / spare / action triangles | Scene triangles |
| --- | ---: | --- | --- | --- | ---: |
| M24 | 1.74 m | Internal floorplate/box plus five individually lathed ogive cartridges | Swept bolt handle | `1382 / 460 / 2740 / 216` | 4,798 |
| AXMC | 1.74 m | Independent tapered box magazines | Swept bolt handle | `1722 / 364 / 364 / 216` | 2,666 |
| AWM | 2.00 m | Independent tapered box magazines | Swept bolt handle | `1688 / 364 / 364 / 216` | 2,632 |
| VSS | 1.58 m | Independent curved rock-and-lock magazines | Swept charging control | `1344 / 364 / 364 / 216` | 2,288 |
| P226 | 0.40 m | Independent straight pistol magazines | Complete separated Quaternius slide | `532 / 364 / 364 / 436` | 1,696 |
| M1911 | 0.40 m | Independent straight pistol magazines | Complete separated Quaternius slide | `870 / 364 / 364 / 572` | 2,170 |

The four long-gun action pivots retain their shared runtime rest at
`(0.075,0.085,-0.050)`, but each `ChargingHandleSocket` is derived from the
highest-Y vertex in the minimum-X terminal region of the real 216-triangle
action mesh. M24, AXMC, and AWM resolve to handle-local
`(-0.061999999,-0.001527630,0.020006508)` and root-local
`(0.013000004,0.083472371,-0.029993493)`; VSS resolves to handle-local
`(-0.061999999,-0.001540400,0.038010638)` and root-local
`(0.013000004,0.083459601,-0.011989363)`. Post-GLB distances to the visible
action triangles are `1.16415321826935e-10` m for M24/AXMC/AWM and
`2.56113708019257e-9` m for VSS.

M24 does not receive a fictitious detachable box magazine: its installed
component stays in the receiver while the five-cartridge bundle moves from the
staged loading component into the internal port. P226 and M1911 do not receive
project-authored proxy slides. Their 436- and 572-triangle complete source
slides, including their separate sight/detail islands, are moved by an 85 mm
action pivot while the 532- and 870-triangle fixed frames remain stationary.
Those slide meshes remain Quaternius CC0 geometry; only the new magazines and
builder fall under the project MIT license.

The service-pistol mechanism roots keep the shared runtime rest contract at
`Magazine=(0,-0.20,-0.31)` and `SpareMagazine=(-0.30,-0.62,-0.18)` metres.
Only the magazine geometry and hand socket are aligned inside those pivots. The
builder derives that alignment from the canonical source mesh: P226 uses the
unique vertices of its four 28-triangle grip bands, M1911 uses the unique
vertices of its two 192-triangle grip panels, and an analytic 2D YZ covariance
PCA aims each magazine down the authored grip. The reviewed mouth plane is
Y=0.030 m. P226 resolves to top `(0,0.030,0.191781466)` and X pitch
`-0.241202590` radians; M1911 resolves to top
`(0,0.030,0.219061704)` and X pitch `-0.362301187` radians. The export audit
locks both installed/staged poses, post-bevel bounds, bilateral grip clearance,
socket contact, fixed-frame continuity, and full-slide travel.

### Delivered supplemental files

| Platform and file | Bytes | SHA-256 |
| --- | ---: | --- |
| M24 runtime `m24_reloadable.glb` | 165,308 | `F057513B5DF9B90A43D3129FEE7CBFEFBFBF3415C85C822B07ADB39979650454` |
| M24 review `m24_reloadable_preview.png` | 810,858 | `A699277406ADADDB6414DBCECE5483DDC3917220CF6D6F4E9D19353D569C06A7` |
| M24 DCC `../../../source_art/reloadable_weapons/m24_reloadable.blend` | 883,662 | `13425F3ABDBC4533BA90D46AACA7ADD6FC482E90F638613FC0CCFA70630289F4` |
| AXMC runtime `axmc_reloadable.glb` | 122,880 | `872A4848F3B2D78E5923F4C9EE97D759A42E5CC445635B4D55D99FBBEB1F989D` |
| AXMC review `axmc_reloadable_preview.png` | 815,182 | `F0C1403D052F522501837FE58F5143FFC870BF9B3D0AC2E23ED138695E9C4DEB` |
| AXMC DCC `../../../source_art/reloadable_weapons/axmc_reloadable.blend` | 792,218 | `E9CE51B3D2B0C69B1BE5E14A0A31BCA6696C8022A185C9B638B0C95D0DA3FA7A` |
| AWM runtime `awm_reloadable.glb` | 122,700 | `FFA2FE9DD07771650D55D60FAAC6715336ECD087D57373BA5C4139B3E0C73807` |
| AWM review `awm_reloadable_preview.png` | 832,830 | `72BAA67013EFD979DFC7CBD78D584929C86E0371794999293535FED2D5C2DBC9` |
| AWM DCC `../../../source_art/reloadable_weapons/awm_reloadable.blend` | 813,394 | `270C6224D9FC1CDA586EEC65AC758D75FC2896C3B82DE652C80AC68F0547BCE3` |
| VSS runtime `vss_reloadable.glb` | 100,852 | `EAD6F895A66662F127949CA1F8A556873C627D9C0E39F472D57BE42882D21FBB` |
| VSS review `vss_reloadable_preview.png` | 817,219 | `B8F3650B19711F73D3692A17AB55D472EDD40FCF4B4F5FF60BAE16686F5543F0` |
| VSS DCC `../../../source_art/reloadable_weapons/vss_reloadable.blend` | 756,896 | `3A239001B22DFAAA747675CCDEFF947B7BF3D5C47A6AAF89C3C3F94CA88B9821` |
| P226 runtime `p226_reloadable.glb` | 75,592 | `579CB38E8F861ECAC5B7C7739946C4620046FFFAF94EE5E073CB69B913DB72FC` |
| P226 review `p226_reloadable_preview.png` | 805,122 | `DB6CE77A67DAB77502E818D6F01D670AF0193AD07CBE22DC30D949B325C4AD68` |
| P226 DCC `../../../source_art/reloadable_weapons/p226_reloadable.blend` | 637,573 | `1406B0F2E797BF11EBDEA912299DCB4EAFDD99CA085C883CF66E210189239B5C` |
| M1911 runtime `m1911_reloadable.glb` | 105,280 | `08B5DC8D4ABC14B88B6728F2B4EB007284DA54ADC30D5295DBD703E5551D3C10` |
| M1911 review `m1911_reloadable_preview.png` | 813,613 | `A5371D16207493EF433A62F849601AD3A7CA76595E19499396CCE5A91DA87EEA` |
| M1911 DCC `../../../source_art/reloadable_weapons/m1911_reloadable.blend` | 703,191 | `1F511016985F186C7F9F73CC19F0A293AC60B0639A1939713DA687F994C9FEEF` |

`../../../scripts/blender/build_supplemental_reload_mechanisms.py` verifies each
source byte count/hash/object/topology, preserves the source-body triangles,
requires visible installed/spare/action geometry, enforces the named hierarchy,
checks the target length after a Blender glTF round trip, locks every long-gun
socket to the minimum-X action endpoint and a one-micrometre triangle-surface
distance, and records exact runtime/review hashes. The editable `.blend` files
and review PNGs are paired
with each runtime GLB under the paths above. Blender can rewrite internal
`.blend` metadata on a later save, so each Blend hash records the exact current
delivered container rather than a deterministic-output gate.

## GSh-18 authored-slide adaptation

- Original creator: TastyTony.
- Creator profile: https://sketchfab.com/TastyTony
- Official source: https://sketchfab.com/3d-models/low-poly-gsh-18-7ce65f794f0e42f98f61a96026e4d75e
- Exact source license: Creative Commons Attribution 4.0 International (CC BY
  4.0).
- License deed: https://creativecommons.org/licenses/by/4.0/
- Source acquisition date established by the tracked adapter: 2026-08-20.
- DCC mechanism adaptation and review date: 2026-08-29.
- Attribution required: Yes. Credit **Low-Poly GSh-18** by TastyTony, link the
  source and CC BY 4.0 license, and state that Operation Steel Tide separated
  the authored slide, normalized the presentation, tuned scalar PBR, removed
  the source rig/staging objects, and added two detachable magazines, pivots,
  and gameplay sockets.

The immutable source is `../tastytony_gsh18/low-poly_gsh-18.glb`. Its embedded
Sketchfab metadata independently records the same title, creator, official URL,
and `CC-BY-4.0` license. The Blender derivative retains all ten rendered source
mesh objects, all 6,361 source triangles, and all ten source materials. It
separates the connected outer-slide islands of `Object_21` and moves them with
the authored `Object_28` and `Object_30` slide details: 1,028 source triangles
move under `ChargingHandle`, while 5,333 source triangles remain fixed. No
proxy action block is added and no source triangle is duplicated or discarded.

The source has no detachable magazine mesh suitable for the runtime contract,
so the adaptation adds independent 900-triangle installed and spare 18-round
magazines. Each is a rounded Blender loft with separate feed lips, follower,
and floor plate. The project magazines and builder are MIT; the retained body,
complete slide, materials, and combined derivative remain subject to TastyTony's
CC BY 4.0 attribution. The 0.43 m identity-root scene contains 8,161 triangles,
13 mesh nodes, 13 scalar PBR materials, an 85 mm visible slide cycle, independent
magazine data, and seven named gameplay sockets.

The two moving-hand contacts are projected onto real triangles instead of
using mechanism origins or volume centres. In root-local Godot metres,
`MagazineGripSocket` is
`(-0.024118496, -0.059071764, 0.233635664)` on the left middle magazine wall;
its parent-local position under `Magazine` is
`(-0.024118496, 0.140928239, 0.543635666)`. `ChargingHandleSocket` is
`(-0.031275425, 0.111273542, 0.235600069)` on the authored outer slide's
left rear surface; its parent-local position under `ChargingHandle` is
`(-0.106275424, 0.026273541, 0.285600066)`. GLB round-trip gaps are
`0.000000000` m and `0.000000004` m respectively, and the action contact is at
rear fraction `0.797865` of the complete moving-slide bounds.

```text
SteelTideReloadableGSh18
|- fixed authored body parts                [5,333 CC BY source triangles]
|- Magazine
|  |- MagazineGeometry                       [900 project triangles]
|  `- MagazineGripSocket
|- SpareMagazine
|  `- SpareMagazineGeometry                  [900 project triangles]
`- ChargingHandle
   |- ChargingHandleGeometry                 [876 CC BY source triangles]
   |- SlideInsertGeometry                     [92 CC BY source triangles]
   |- SlideTopGeometry                        [60 CC BY source triangles]
   `- ChargingHandleSocket
```

| GSh-18 file | Bytes | SHA-256 |
| --- | ---: | --- |
| Source `../tastytony_gsh18/low-poly_gsh-18.glb` | 916,616 | `56E8CB31AE1CE1DEA689A3D890A95DAC7E1D30334C809CBB2D9E43038CBBC6B9` |
| Runtime `gsh18_reloadable.glb` | 499,464 | `887DD398F720393074335D31A210F4770A02AF4F4740FF5E5FD322E89FB2B405` |
| Review `gsh18_reloadable_preview.png` | 1,444,201 | `7BED8C18CE7019A8AD564A9C85F40F0AE7AF8015BE346015752DF8BEC67F1371` |
| DCC source `../../../source_art/reloadable_weapons/gsh18_reloadable.blend` | 2,423,327 | `E5ED7B0B155D85A31A11389FEC0B08E4A37196C8337721172BBBEF3972D049AA` |

`../../../scripts/blender/build_reloadable_gsh18.py` audits the immutable
source bytes/hash, ten-object topology and materials, connected slide islands,
source-triangle conservation, independent magazine data, identity metre scale,
named hierarchy, sockets, installed/staged bounds, fixed-frame behavior during
the slide cycle, both hand-socket surface distances, the action socket's
stockward longitudinal fraction of at least `0.70`, exact runtime/review
identity, and a Blender glTF round trip.
The Blend hash above likewise identifies the current editable container and is
not asserted as stable across later Blender resaves.
The source-only static adapter and its separate output remain documented in
`../tastytony_gsh18/LICENSE.md`; they are not substituted for this reloadable
mechanism asset.
