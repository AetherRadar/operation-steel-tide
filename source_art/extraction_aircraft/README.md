# Rescue Tilt-Rotor Source Asset

`extraction_aircraft.blend` is the editable source for the rescue aircraft loaded by Godot from `assets/models/extraction_aircraft/extraction_aircraft.glb`.

Regenerate the source and runtime asset with Blender 5.x from the repository root:

```bash
blender --background --factory-startup --python scripts/blender/build_extraction_aircraft.py
```

The GLB exposes `LeftRotorPivot`, `RightRotorPivot`, and `BoardingDoor` as a stable runtime contract. Godot owns their animation as well as flight state, seats, lights, audio, and mission behavior. If loading or binding the authored visual fails, `ExtractionAircraft` uses its previous runtime-generated exterior instead.

The model is project-authored with disclosed AI assistance and is covered by the repository's root MIT license. See `docs/CONTENT_PROVENANCE.md` for the complete content inventory.
