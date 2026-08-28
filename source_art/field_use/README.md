# Steel Tide First-Person Field-Use Props

This directory contains the editable Blender 4.5 source for Operation Steel
Tide's first-person trauma-care and armor-repair prop set. The silhouettes,
topology, hinge layout, details, and PBR material palette are original project
work. No marketplace model, texture, trademarked artwork, or third-party mesh
is included.

Regenerate the authoritative `.blend`, embedded runtime GLB, and studio preview
from the repository root:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_field_use_props.py
```

The generator saves `field_use_props.blend` here and writes
`field_use_props.glb` plus `field_use_props_preview.png` to
`assets/models/steel_tide_field_use/`. It then re-imports the GLB and rejects
missing nodes, external payloads, material drift, changed bounds, or a mesh
outside the real-time triangle budget.

## Runtime hierarchy

All dimensions are metres. Blender `+Y` is exported as Godot `-Z`. The root is
`SteelTideFieldUseProps`; every independently visible prop is below that root
and begins at the root origin so gameplay code can present one item at a time.

- `TraumaKit` owns the soft case and hinged `TraumaKitLid`.
- `TraumaGauzePack` and `TraumaInjector` are independent treatment items.
- `ArmorPlate` is the curved replacement plate.
- `ArmorCarrier` owns the insertion target and hinged `ArmorCarrierFlap`.

`TraumaKitLid` has its origin on the rear zipper hinge. Its local X rotation is
`0°` when closed and `-104°` for the authored open pose. `ArmorCarrierFlap` has
its origin on its upper seam. Its local X rotation is `0°` when secured and
`78°` when peeled open. These are data-bearing nodes, not baked animations, so
the first-person controller can time them against healing or repair progress.

## Grip-marker convention

The GLB contains the unique marker nodes `TraumaPrimaryGrip`,
`TraumaLidGrip`, `TraumaGauzeGrip`, `InjectorPrimaryGrip`,
`ArmorPrimaryGrip`, and `ArmorSupportGrip`. Each is parented to the prop or
moving part it follows. For every marker, local `+Z` points away from the
contacted surface (the palm normal) and local `+Y` points toward the intended
finger direction; local `+X` completes a right-handed frame. Runtime hand
alignment should use the complete transform rather than position alone.

## Art direction

The trauma kit uses a rounded sewn shell, inset panels, compression cords,
individual zipper teeth, stitched pockets, a hinged lid, medical patch, and
visible interior dressing compartments. The dressing packet has crimped foil
seals; the injector is a custom lathed assembly with dose marks and fluid core.
The armor plate has a clipped SAPI-like silhouette, compound curvature,
separate edge seal, strike face, orientation mark, and impact-inspection lines.
The carrier includes a padded shaped shell, plate pocket, tapered shoulder
straps, MOLLE rows, stitch geometry, hook-and-loop closure, and a hinged flap.

The source and generated assets are covered by the repository's root MIT
license.
