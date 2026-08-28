# Authored combat optics source

`steel_tide_optics.blend` is the authoritative DCC source for the shared
first-person micro, holo, and magnified optic housings. It is built entirely
from finished CC0 Quaternius Ultimate Guns Pack source meshes already tracked
in this repository.

Rebuild with Blender 4.5 LTS or newer from the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_authored_optics.py
```

The build produces:

- `assets/models/steel_tide_optics/steel_tide_optics.glb`;
- `source_art/combat_optics/steel_tide_optics.blend`;
- ignored review renders under `build/art-previews/steel_tide_optics*.png`.

## Runtime contract

The exported root is `SteelTideAuthoredOptics`. It owns exactly three visible
mesh variants:

| Variant | Geometry | Reticle marker | Blender dimensions (W/L/H) |
| --- | --- | --- | --- |
| `MicroOptic` | `MicroGeometry` | `MicroReticleAnchor` | `0.112 / 0.108 / 0.120 m` |
| `HoloOptic` | `HoloGeometry` | `HoloReticleAnchor` | `0.168 / 0.142 / 0.160 m` |
| `ScopeOptic` | `ScopeGeometry` | `ScopeReticleAnchor` | `0.132 / 0.420 / 0.145 m` |

All three variants are centered on the optical axis with their rail-contact
geometry below the reticle. Blender `+Y` maps to Godot `-Z`, so the reticle
markers import at local Godot positions `(0, 0, +depth)`.

## Deterministic quality checks

The script fails before export unless all of the following remain true:

- all three exact source objects and material layouts match;
- each selected glass-bearing source component has the expected 412-triangle
  topology;
- all three runtime nodes contain nonempty, source-derived mesh geometry;
- the runtime hierarchy contains no generated primitive mesh;
- exactly 12 source glass triangles are removed per optic;
- every centerline passes through the housing without a BVH hit;
- the three silhouettes remain dimensionally distinct; and
- the final total is three meshes, at least 2,200 vertices, and exactly 1,200
  triangles.

Godot additionally requires the three geometry nodes, the three reticle anchor
nodes, six material surfaces, and distinct micro/holo/scope bounds through
`CombatModelLibrary.InspectAuthoredOptics()` and `--validate-combat-models`.

Full creator, source URL, license, file mapping, and hashes are recorded in
`assets/models/steel_tide_optics/LICENSE.md`.
