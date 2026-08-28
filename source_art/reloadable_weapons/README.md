# Reloadable weapon DCC source

`ak74_reloadable.blend` is the authoritative editable source for the
mechanism-ready first-person AK-74N derivative. It retains the complete finished
CC0 Quaternius source mesh while giving the existing curved magazine its own
runtime node.

Rebuild with Blender 4.5 LTS or newer from the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_reloadable_ak74.py
```

The build produces:

- `assets/models/steel_tide_ak74/ak74_reloadable.glb`;
- `source_art/reloadable_weapons/ak74_reloadable.blend`; and
- an ignored review render at `build/art-previews/ak74_reloadable.png`.

## Runtime contract

The exported `SteelTideReloadableAK74` root contains exactly two mesh nodes:

| Node | Source triangles | Purpose |
| --- | ---: | --- |
| `WeaponBodyGeometry` | 1,155 | Static receiver, furniture, barrel, sights, and stock |
| `MagazineGeometry` | 227 | Detachable finished curved magazine |

Godot wraps `MagazineGeometry` in `Magazine`, duplicates the same authored mesh
under `SpareMagazine`, and transfers root-space reload deltas onto those two
nodes. The right authored arm remains at its grip anchor; the left authored arm
tracks whichever visible magazine node is active.

## Deterministic quality checks

The script fails before export unless:

- the tracked source object is exactly `AssaultRifle_4` with 2,682 vertices,
  1,382 triangles, and the expected five-material order;
- the spatial/material selection resolves to exactly 227 finished source
  magazine triangles;
- the body retains the other 1,155 triangles;
- the two runtime meshes together preserve all source triangles; and
- the exported hierarchy contains only the expected authored body and magazine
  meshes.

The in-engine `AK_RELOAD_ARM_CHECK` additionally requires visible authored
magazine geometry, distinct primary/spare mechanism nodes, a fixed right-hand
grip, left-hand tracking, sleeve continuity, frame-bottom sleeve contact, stable
weapon-root pose, deterministic samples, and a clean idle reset.
