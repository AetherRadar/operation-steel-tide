# Weapon Platform Source Art

These Blender files are the editable source for the authored weapon platform
exports in `assets/models/weapon_platforms/`. Regenerate them from the repository
root with Blender 4.5 LTS or newer:

```powershell
blender --background --python scripts/blender/generate_weapon_platform_models.py
```

Each export keeps the same node contract used by Godot:
`Magazine`, `SpareMagazine`, `ChargingHandle`, `Stock`, `Foregrip`,
`MuzzleDevice`, `Suppressor`, and `OpticMount`. Geometry is authored in Blender
along local +Y so world presentation can rotate it toward -Z; materials are
project-authored Principled BSDF materials with no third-party textures.
