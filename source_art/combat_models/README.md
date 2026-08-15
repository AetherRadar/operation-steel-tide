# Combat Model Source Assets

`steel_tide_m4a1.blend` and `steel_tide_operator.blend` are the editable sources for the first-person rifle and operator body loaded from `assets/models/steel_tide_m4a1/steel_tide_m4a1.glb` and `assets/models/steel_tide_operator/steel_tide_operator.glb`.

Regenerate both source files, runtime GLBs, and local preview renders with Blender 4.5 LTS or newer from the repository root:

```bash
blender --background --factory-startup --python scripts/blender/generate_combat_models.py
```

The weapon GLB exposes `Magazine`, `SpareMagazine`, `ChargingHandle`, `Stock`, `Foregrip`, `MuzzleDevice`, `Suppressor`, and `OpticMount`. The operator GLB exposes `LeftLegRig`, `RightLegRig`, `Helmet`, `Vest`, `Backpack`, and `TeamPatch`. These names are a stable runtime contract used by `CombatModelLibrary`; Godot continues to own gameplay, collision, weapon state, team colors, and animation.

Both models are project-authored by the checked-in Blender script with disclosed AI assistance. They contain no copied geometry, textures, or source files from the reference pages discussed during development. They are covered by the repository's root MIT license; see `docs/CONTENT_PROVENANCE.md` for the complete inventory.
