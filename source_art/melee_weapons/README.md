# Steel Tide Original Melee Weapons

This directory contains the editable Blender 4.5 source scenes for three
project-authored melee weapons.  Their silhouettes, topology, fittings,
wrapping, and materials were created specifically for Operation Steel Tide;
they do not contain third-party marketplace geometry or textures.

Regenerate all three `.blend` sources, runtime GLBs, and 16:9 studio previews
from the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_melee_weapons.py
```

The generator creates:

- `tactical_knife.blend` and `assets/models/steel_tide_melee/tactical_knife.glb`
- `zhanma_dao.blend` and `assets/models/steel_tide_melee/zhanma_dao.glb`
- `tianxuan_dao.blend` and `assets/models/steel_tide_melee/tianxuan_dao.glb`
- one `*_preview.png` dark studio render beside each GLB

## Runtime contract

All dimensions are authored in metres with the object origin at the primary
grip.  The blade points along Blender `+Y`, which the glTF importer presents as
Godot `-Z`.  Every GLB contains direct marker nodes named `GripPrimary`,
`GripSupport`, `BladeBase`, and `BladeTip`.

The four runtime PBR materials are deliberately named `TintBlade`, `TintEdge`,
`TintGrip`, and `TintAccent`.  Gameplay code may duplicate and tint those
materials without depending on individual mesh-piece names.

The script re-imports every generated GLB and rejects missing roots, markers,
materials, triangle-count drift, or an out-of-budget mesh.  It prints the mesh
count, triangulated face count, material count, and final XYZ dimensions for
each weapon.

## Design notes

- `tactical_knife` replaces the old low-detail knife with a crowned custom
  blade loft, separate edge bevel, spine serrations, fuller inlay, double-helix
  grip wrap, faceted pommel, and lanyard ring.
- `zhanma_dao` uses a 0.936 m single-edged wide blade, a 0.312 m two-hand grip,
  wing guard, metal collars, eight-turn crossed wrap, and historical ring
  pommel.  It is an original interpretation rather than a reconstruction of a
  museum object.
- `tianxuan_dao` uses a distinct 1.0 m upswept black-steel fantasy blade,
  crescent guard, spine horn, faceted pommel crystal, crossed grip wrap, and
  emissive cyan `TintAccent` rune geometry.

Godot remains responsible for attachment, hit detection, animation, effects,
and gameplay statistics.  The authored source and generated assets are covered
by the repository's root MIT license.
