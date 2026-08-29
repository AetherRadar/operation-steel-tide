# Combat Model Source Assets

`steel_tide_m4a1.blend` is the editable adaptation source for the first-person
rifle loaded from `assets/models/steel_tide_m4a1/steel_tide_m4a1.glb`. It is
built from nisu's CC0 M4A1 source retained in
`source_art/third_party/nisu_m4a1/` plus selected CC0 Quaternius Ultimate Guns
Pack components already tracked under `assets/models/quaternius_ultimate_guns/`.
See the nisu `LICENSE_EVIDENCE.md`, the Quaternius pack `LICENSE.md`, and
`assets/models/steel_tide_m4a1/LICENSE.md` for exact component mappings.

Regenerate the adapted M4A1 source, runtime GLB, and local preview render with
Blender 4.5 LTS or newer from the repository root:

```bash
blender --background --factory-startup --python scripts/blender/build_nisu_m4a1.py
```

The M4A1 GLB exposes `Magazine`, `SpareMagazine`, `ChargingHandle`, `Stock`,
`RearIronSight`, `FrontIronSight`, `Foregrip`, `MuzzleDevice`, `Suppressor`, and
`OpticMount`. `MagazineGripSocket` is a direct child of `Magazine`, on the
actual left magazine wall, and `ChargingHandleSocket` is a direct child of
`ChargingHandle`, on the actual rear-left T-handle wing. `RearIronSight` owns
both original nisu rear-aperture meshes and
`FrontIronSight` owns the original front tower and post. The fitted-optic
configuration can clear both irons from the optic window without deleting them
from the no-optic build. These names are a stable runtime contract used by
`CombatModelLibrary`; Godot continues to own
gameplay, collision, weapon state, and animation. The adaptation rebinds the
source 2K PBR textures, applies the project's `2.36` authored-space scale,
removes zero-area faces, and places authored geometry under the movable runtime
nodes. The original model and textures, adapted `.blend`, and runtime GLB retain
their CC0 provenance and are not project-authored MIT art.

The final visible attachment nodes contain authored geometry: a Quaternius
SCAR-L component fitted as `Foregrip`, the nisu barrel muzzle split into
`MuzzleDevice`, a Quaternius MP5A5 front assembly fitted as `Suppressor`, and a
complete Quaternius AXMC scope housing and mount fitted as `OpticMount`.
`MuzzleDeviceTip`, `SuppressorTip`, and `OpticReticleAnchor` are transform-only
runtime markers derived from those component bounds. The build asserts every
attachment mesh, the two meshes beneath `RearIronSight`, the front-tower mesh
beneath `FrontIronSight`, marker parent, pre-export transform, and fixed source
selection. The optic reticle anchor is derived from the original authored
eyepiece pane before both glass panes are removed from the runtime mesh. The
build asserts zero remaining glass faces and a collision-free centerline through
the resulting open aperture. Godot's `--validate-combat-models` diagnostic
validates the imported
GLB hierarchy and runtime alignment. Gameplay stat variants deliberately share
these finished authored slot visuals rather than enabling the hidden primitive
variant meshes.

The builder also locks all three mechanism-root transforms, projects each hand
socket from a semantic grip seed onto the authored triangles with a BVH, checks
sub-micrometre contact error, preserves the exact 19-mesh/10,617-triangle
visible topology, and repeats the socket/hierarchy/topology audit after GLB
export and Blender re-import.

`steel_tide_operator.blend` is the editable source for the legacy
project-authored operator output at
`assets/models/steel_tide_operator/steel_tide_operator.glb`. Regenerate only
that operator source, runtime GLB, and preview with:

```bash
blender --background --factory-startup --python scripts/blender/generate_combat_models.py
```

The legacy operator exposes `LeftLegRig`, `RightLegRig`, `Helmet`, `Vest`,
`Backpack`, and `TeamPatch`. It is project-authored by the checked-in Blender
script with disclosed AI assistance, contains no copied third-party geometry or
textures, and is covered by the repository's root MIT license. See
`docs/CONTENT_PROVENANCE.md` for the complete inventory.
