# Reloadable AK-47 DCC source

`ak47_reloadable.blend` is the authoritative editable source for the active
AK-47 first-person and world visuals. It adapts taradavies' finished CC0 model,
adds a portable mechanism/socket hierarchy, packs two project-authored wood
textures, and contains no source image with unknown provenance.

Rebuild from the repository root with Blender 4.5 LTS or newer:

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
