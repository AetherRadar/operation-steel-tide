# Authored combat optics source

`steel_tide_optics.blend` is the authoritative DCC source for the shared
first-person micro, holo, and magnified optic housings. It starts from finished
CC0 Quaternius Ultimate Guns Pack source meshes already tracked in this
repository, then adds a machined Picatinny base, clamp feet, and small bevels
so each sight seats naturally against a firearm rail.

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

| Variant | Geometry | Reticle marker | Rear/front aperture markers | Blender dimensions (W/L/H) |
| --- | --- | --- | --- | --- |
| `MicroOptic` | `MicroGeometry` | `MicroReticleAnchor` | `MicroRearApertureAnchor` / `MicroFrontApertureAnchor` | `0.112 / 0.108 / 0.120 m` |
| `HoloOptic` | `HoloGeometry` | `HoloReticleAnchor` | `HoloRearApertureAnchor` / `HoloFrontApertureAnchor` | `0.168 / 0.142 / 0.160 m` |
| `ScopeOptic` | `ScopeGeometry` | `ScopeReticleAnchor` | `ScopeRearApertureAnchor` / `ScopeFrontApertureAnchor` | `0.132 / 0.420 / 0.145 m` |

Every aperture marker is a direct child of its optic variant. The names are
globally unique because Godot 4.6 globally uniquifies imported glTF node names,
not merely sibling names. All three variants remain centered on the optical
axis with their rail-contact geometry below the reticle. Blender `+Y` maps to
Godot `-Z`; the `FrontApertureAnchor` endpoint is consequently the
more-negative local Godot `Z` endpoint.

The rear and front coordinates come independently from the bounding centers
of the original rear and front source-glass planes after deformation. The
reticle marker is deliberately coincident with the rear anchor; no aperture
endpoint is copied from a reticle marker.

## Deterministic quality checks

The script fails before export unless all of the following remain true:

- all three exact source objects and material layouts match;
- each selected glass-bearing source component has the expected 412-triangle
  topology;
- all three runtime nodes contain nonempty, source-derived mesh geometry;
- the rail base and clamp feet are authored and joined in Blender, with no
  runtime-generated visible primitive mesh;
- exactly 12 source glass triangles are removed per optic;
- those triangles resolve into exactly two independent 8-vertex,
  6-face/6-triangle planes;
- the six globally unique aperture anchors are direct variant children and
  preserve their exact raw-GLB and Blender-round-trip names;
- front/rear separation stays positive, Godot XY optical-axis residual stays
  at or below 0.5 mm, and reticle-to-rear distance remains zero;
- every centerline passes through the housing without a BVH hit;
- the three silhouettes remain dimensionally distinct; and
- the final total is three meshes, at least 2,200 vertices, and at least 1,200
  triangles after the DCC bevel and rail-mount pass.

Godot additionally requires the three geometry nodes, the three reticle anchor
nodes, the six unique aperture-anchor nodes, six material surfaces, and
distinct micro/holo/scope bounds. A clean Godot 4.6.3 import-tree audit verifies
that none of the unique anchor names receives a numeric suffix.

Full creator, source URL, license, file mapping, and hashes are recorded in
`assets/models/steel_tide_optics/LICENSE.md`.
