# Reloadable weapon DCC sources

This directory contains the authoritative editable Blender sources for the
active AK-47 plus the mechanism-ready SCAR-L, MP5A5, M24, AXMC, AWM, VSS,
P226, M1911, and GSh-18 runtime derivatives. Their 2026-08-29 DCC pass replaces
marker-only reload motion with visible magazine/loading/action geometry while
retaining finished third-party weapon art. `ak47_reloadable.blend` adapts
taradavies' finished CC0 AK-47, adds a portable mechanism/socket hierarchy,
packs two project-authored wood textures, and contains no source image with
unknown provenance.

Source and output rights are recorded in:

- `../../assets/models/steel_tide_ak74/LICENSE.md`;
- `../../assets/models/steel_tide_scarl/LICENSE.md`;
- `../../assets/models/steel_tide_reloadable_weapons/LICENSE.md`;
- `../../assets/models/quaternius_ultimate_guns/LICENSE.md`; and
- `../third_party/taradavies_ak47/LICENSE_EVIDENCE.md`;
- `../third_party/adamkokrito_scarl/LICENSE.md`; and
- `../../assets/models/tastytony_gsh18/LICENSE.md`.

taradavies' **AK-47** is CC0 and was acquired 2026-08-29. The Quaternius
Ultimate Guns Pack sources were acquired 2026-08-20 under CC0 1.0 Universal.
AdamKokrito's **ScarL** source was acquired 2026-08-29 under CC BY 3.0.
TastyTony's **Low-Poly GSh-18** source was acquired 2026-08-20 under CC BY 4.0.
The project-authored textures, mechanism meshes, and builders are covered by
the root MIT license; retained third-party art and combined outputs keep the
source-license mapping above.

The AK adaptation keeps the original fixed-stock/curved-magazine proportions,
then applies a uniform `0.89` presentation pass around the firing-hand grip.
The resulting FP and world exports measure about `1.40 m` overall; this avoids
the former oversized `1.58 m` viewmodel while keeping the trigger hand, support
hand, and reload sockets on the same authored surfaces.
The runtime leaves the AK's authored iron sights exposed at tier 0; the shared
external holo and magnified optics begin at higher tiers so the base silhouette
is not obscured by a generic circular sight. Bare-iron ADS uses the authored
rear-aperture line at `0.075 m` in the weapon-root frame rather than the
shared M4 fallback height.

## Rebuild

Run Blender 4.5 LTS or newer from the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_taradavies_ak47.py
```

The deterministic build verifies the exact 2,044,772-byte raw source and its
SHA-256 before opening it. It then produces:

- `assets/models/steel_tide_ak74/ak47_reloadable_fp.glb`;
- `assets/models/steel_tide_ak74/ak47_reloadable_world.glb`;
- `assets/models/steel_tide_ak74/ak47_studio_preview.png`;
- `source_art/reloadable_weapons/ak47_reloadable.blend`; and
- `source_art/reloadable_weapons/textures/ak47_laminated_wood_{base_color,roughness}.png`.

## Source sanitation and materials

The official `.blend` contains unpacked references to two unlicensed wood
images and an external HDR. The build deletes every source material and image
datablock before constructing the runtime asset. It never downloads or reads
those missing dependencies. The replacement wood maps are generated from a
fixed mathematical grain recipe; their pixels are project-authored. Five
scalar metal/bakelite materials and one textured wood material provide the six
runtime PBR materials.

The final source `.blend` packs both generated PNGs and stores their portable
relative paths beneath `//textures/`. A post-save reopen rejects an absolute,
unpacked, missing, empty, or unexpected image.

## Runtime hierarchy

The FP and world GLBs share this authored contract:

- `SteelTideAK47`
- `WeaponBodyGeometry`, `ReceiverGeometry`, `FurnitureGeometry`, and
  `BoltHardwareGeometry`
- `Magazine/MagazineGeometry`
- `SpareMagazine/SpareMagazineGeometry`
- `ChargingHandle/ChargingHandleGeometry`
- `Stock`
- `RearIronSight/RearIronGeometry`
- `FrontIronSight/FrontIronGeometry`
- `Foregrip`
- `MuzzleDevice/MuzzleDeviceTip`
- `Suppressor/SuppressorTip`
- `OpticMount/OpticReticleAnchor`
- `OpticRailAdapterGeometry` and `OpticRailContact`
- `EjectionPort`

The real magazine and charging-handle meshes follow the existing deterministic
reload timeline. The muzzle flash, tracer start, and casing ejection now follow
the DCC markers. External optics attach to `OpticRailContact`; adding an optic
hides the independently modeled mechanical sights, and removing it restores
them.

## Deterministic quality checks

The script fails unless:

- the raw input hash, byte count, 32 firearm mesh names, source bounds, and
  source topology match the audited download;
- no unlicensed source material or image survives;
- the FP/world exports contain 97,372/24,488 unique triangles respectively,
  11 mesh resources, six materials, and two embedded PNG textures;
- the magazine, charging handle, front/rear sights, optic adapter, muzzle, and
  ejection markers are present as non-empty, independent runtime nodes;
- the optic marker resolves against the actual horizontal rail top, with a
  verified 0.000 mm marker-to-mesh gap in both detail levels; and
- the saved `.blend` reopens with exactly two packed project textures and no
  absolute path.

Godot's `--validate-combat-models` additionally checks both separate runtime
paths, instanced triangle counts, materials, textures, bounds, mechanism mesh
presence, independent sights, and marker-to-rail contact. Its
`--validate-ads-alignment` matrix applies a dedicated +/-3 mm AK optic contact
gate to micro, holographic, and magnified sights.

## Rebuild the remaining weapon derivatives

```powershell

& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_reloadable_scarl.py

& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_reloadable_quaternius_weapons.py

& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_supplemental_reload_mechanisms.py

& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_reloadable_gsh18.py
```

The builders write the runtime GLB and review PNG beside its local license
record, and save the editable `.blend` in this directory. Exact delivered
bytes and SHA-256 values are intentionally kept in those license records rather
than duplicated here.

## Source/output inventory

| Platform | Immutable source | Editable DCC source | Runtime / review | Reproducible builder |
| --- | --- | --- | --- | --- |
| AK-47 | `../third_party/taradavies_ak47/ak47_taradavies.blend` | `ak47_reloadable.blend` | `../../assets/models/steel_tide_ak74/ak47_reloadable_fp.glb`; `../../assets/models/steel_tide_ak74/ak47_reloadable_world.glb`; `../../assets/models/steel_tide_ak74/ak47_studio_preview.png` | `../../scripts/blender/build_taradavies_ak47.py` |
| SCAR-L | `../third_party/adamkokrito_scarl/adamkokrito_scarl.glb` | `scarl_reloadable.blend` | `../../assets/models/steel_tide_scarl/scarl_reloadable.glb`; `../../assets/models/steel_tide_scarl/scarl_reloadable_preview.png` | `../../scripts/blender/build_reloadable_scarl.py` |
| MP5A5 | `../../assets/models/quaternius_ultimate_guns/mp5a5.glb` | `mp5a5_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/mp5a5_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/mp5a5_reloadable_preview.png` | `../../scripts/blender/build_reloadable_quaternius_weapons.py` |
| M24 | `../../assets/models/quaternius_ultimate_guns/m24.glb` | `m24_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/m24_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/m24_reloadable_preview.png` | `../../scripts/blender/build_supplemental_reload_mechanisms.py` |
| AXMC | `../../assets/models/quaternius_ultimate_guns/axmc.glb` | `axmc_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/axmc_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/axmc_reloadable_preview.png` | same supplemental builder |
| AWM | `../../assets/models/quaternius_ultimate_guns/awm.glb` | `awm_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/awm_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/awm_reloadable_preview.png` | same supplemental builder |
| VSS | `../../assets/models/quaternius_ultimate_guns/vss.glb` | `vss_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/vss_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/vss_reloadable_preview.png` | same supplemental builder |
| P226 | `../../assets/models/quaternius_ultimate_guns/p226.glb` | `p226_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/p226_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/p226_reloadable_preview.png` | same supplemental builder |
| M1911 | `../../assets/models/quaternius_ultimate_guns/m1911.glb` | `m1911_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/m1911_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/m1911_reloadable_preview.png` | same supplemental builder |
| GSh-18 | `../../assets/models/tastytony_gsh18/low-poly_gsh-18.glb` | `gsh18_reloadable.blend` | `../../assets/models/steel_tide_reloadable_weapons/gsh18_reloadable.glb`; `../../assets/models/steel_tide_reloadable_weapons/gsh18_reloadable_preview.png` | `../../scripts/blender/build_reloadable_gsh18.py` |

The SCAR-L builder conserves the source topology while separating the 112
original `Base` faces that form its welded flip-up front sight. The resulting
`FrontIronSight` node and the source's independent `IronSightGeometry` node can
be hidden together for optical sights and restored together for the bare rifle.

## Runtime mechanism contract

The canonical supplemental and GSh-18 assets use identity roots in Godot
metres: X is lateral, Y is up, positive Z points toward the stock, and negative
Z points toward the muzzle. Their visible mechanism hierarchy is:

```text
SteelTideReloadable<Platform>
|- WeaponBodyGeometry
|- Magazine
|  |- MagazineGeometry
|  `- MagazineGripSocket
|- SpareMagazine
|  `- SpareMagazineGeometry
`- ChargingHandle
   |- ChargingHandleGeometry
   `- ChargingHandleSocket
```

SCAR-L additionally moves its authored `BoltGeometry` with the authored
charging handle and supplies primary/support/magwell/rail/muzzle surface
sockets. MP5A5 supplies the same broader socket set and exposes its audited
source front/rear sight islands below `FrontIronSight/FrontIronGeometry` and
`RearIronSight/RearIronGeometry`, so an attached optic can hide the mechanical
sights without hiding the weapon body. The AK-47 uses the direct authored
contract documented above rather than this supplemental adapter path.
Its installed and spare magazine nodes share identical local geometry and an
identical DCC-authored palm contact, so the support hand stays on the real
magazine surface through extraction, pouch handoff, insertion, and seating.

| Platform | Source/body triangles | Installed loading geometry | Spare/loading geometry | Action geometry | Delivered scene triangles |
| --- | ---: | ---: | ---: | ---: | ---: |
| SCAR-L | 8,585 retained/refined source-derived geometry before spare | 132 refined source-derived | 132 refined source-derived | 937 refined source-derived = 85 bolt + 852 handle | 8,717 |
| MP5A5 | 1,374 source = 1,206 body + 94 front sight + 14 rear sight + 60 magazine | 60 source | 60 source-derived | 120 project | 1,554 |
| M24 | 1,382 source body | 460 project internal floorplate/box | 2,740 project, five cartridges | 216 project | 4,798 |
| AXMC | 1,722 source body | 364 project | 364 project | 216 project | 2,666 |
| AWM | 1,688 source body | 364 project | 364 project | 216 project | 2,632 |
| VSS | 1,344 source body | 364 project | 364 project | 216 project | 2,288 |
| P226 | 968 source = 532 fixed frame + 436 complete slide | 364 project | 364 project | 436 source slide | 1,696 |
| M1911 | 1,442 source = 870 fixed frame + 572 complete slide | 364 project | 364 project | 572 source slide | 2,170 |
| GSh-18 | 6,361 CC BY source = 5,333 fixed + 1,028 complete slide | 900 project | 900 project | 1,028 source slide | 8,161 |

M24 deliberately keeps an internal receiver loading component instead of
inventing a detachable box magazine. Five individually lathed cartridges form
its staged loading bundle. AXMC/AWM use tapered box magazines, VSS uses curved
rock-and-lock magazines, and P226/M1911 use straight magazines while cycling
their complete Quaternius-authored slides. GSh-18 likewise moves TastyTony's
complete authored slide rather than a proxy block and adds a pair of rounded
18-round magazines with separate feed lips, follower, and floor plate.

## Deterministic quality checks

The remaining builders fail before completion unless the immutable input byte count,
SHA-256, named source object, source topology, expected node hierarchy, visible
mechanism topology, independent spare data where applicable, target scale, and
Blender glTF round trip all match. SCAR checks every retained source part,
per-material topology, installed/staged bounds, and seven surface-derived
sockets. MP5 checks its welded magazine island, unique 94-/14-triangle
front/rear sight islands, exact sight bounds, visibility hierarchy, per-node
material partitions, output bounds, stable runtime/preview identity, and that
its action-hand socket remains on the outer terminal ring rather than at the
mechanism pivot. The supplemental
builder likewise derives the M24/AXMC/AWM/VSS action sockets from the real
minimum-X terminal surface. Both builders lock the endpoint region and measure
socket-to-triangle distance after GLB round trip; the reviewed maximum is
`2.56113708019257e-9` m. The supplemental builder audits
the P226/M1911 welded connected components, complete source-slide bounds,
source-triangle conservation, 85 mm cycle, fixed frame, and surface action
contact. Their magazine geometry and contact socket retain the shared runtime
pivots while an analytic PCA of the source grip bands/panels supplies the
per-platform mouth position and pitch; the audit also locks installed/staged
bounds, bilateral clearance, and the source-derived insertion axis. GSh-18
audits all ten rendered source meshes/materials, its connected
slide islands, exact source-triangle conservation, independent 900-triangle
magazines, seven sockets, installed/staged bounds, and the fixed frame during
the same 85 mm slide cycle. Its magazine grip is projected onto the real left
magazine wall, while the slide-hand socket must remain on the complete authored
slide surface and beyond 70 percent of its stockward length.

The in-engine `--validate-all-weapon-reloads` diagnostic then samples tactical
and empty reload stages, visible mechanism travel, action travel, fixed primary
grip, support-hand target tracking, shoulder/sleeve continuity, idempotence,
cancel, and idle reset.

## Animated reload-arm driver

The shared arms-only driver is outside this directory at
`../third_party/djmaesen_fps_smg45/animated_reload_arms.blend`, with runtime
output `../../assets/models/djmaesen_smg45/animated_reload_arms.glb` and build
script `../../scripts/blender/build_animated_reload_arms.py`. It is derived from
DJMaesen's **fps animated smg**, acquired 2026-08-21 from
`https://sketchfab.com/3d-models/fps-animated-smg-ea3dad7478624495a5a46f40127b0579`
under CC BY 4.0 and generated for this reload set on 2026-08-29.

Contract revision 7 retains the 13,700-triangle full-arm mesh as a hidden audit
layer, adds 9,914-triangle cropped long-gun forearms, and retains the 9,306-
triangle cropped pistol forearms. All three share one skin, three embedded
source textures, and 24 named tactical/empty clips. The 2026-08-31 Blender pass uses
six choreography groups: straight rifle (M4A1/SCAR-L), rock-and-lock
(AK74/VSS), MP5, precision/internal (M24/AXMC/AWM), service pistol
(P226/M1911/GSh-18), and Desert Eagle. Long-gun clips replace the old waist-
pouch sweep with direct exchange poses and explicit old-magazine-out and new-
magazine-seat holds. Empty clips add a contact/pull/hold/release mechanical
beat; M24 retains that bolt beat for both variants. Pistol crops use a stable
analytical shoulder/elbow solve with exact static endpoints rather than a
runtime chain translation. Both shoulder roots and the right palm/grip
relation remain fixed throughout. Runtime renders the long-gun or pistol crop,
never the complete upper-arm audit layer. A grip-space camera envelope samples
all sixteen long-gun clips and explicitly rejects the former full-sleeve
presentation that crossed the Godot near plane. Exact attribution and source mapping are in
`../../assets/models/djmaesen_smg45/LICENSE.md`; exact delivered hashes are
summarized in `../../assets/models/LICENSE.md`. This builder does not emit a
dedicated studio preview PNG; visual review is performed through the in-engine
reload captures and deterministic diagnostic.
