# Quaternius five-operator roster

The five runtime characters in this directory are derived from authored
character presets in Quaternius' **Ultimate Modular Women Pack**.

- Creator: Quaternius (`@Quaternius`)
- Official source: https://quaternius.com/packs/ultimatemodularwomen.html
- Official download folder: https://drive.google.com/drive/folders/1720N9IGyQHXYvtvZJzazhxtTTlz-y2Vf
- License: CC0 1.0 Universal / Public Domain Dedication
- License text: https://creativecommons.org/publicdomain/zero/1.0/
- Acquired: 2026-08-27
- Attribution required: no; creator credit is retained as a courtesy

The upstream `LICENSE.txt` retained with the editable sources has an apparent
title typo referring to “Ultimate Modular Males.” Its body is the supplied CC0
1.0 dedication, and the official Ultimate Modular Women page independently
identifies this character pack as CC0.

## Runtime mapping

| Runtime file | Operator | Authored source preset | DCC adaptation |
| --- | --- | --- | --- |
| `viper.glb` | VIPER / Assault | `Soldier.blend` | Orange/charcoal assault palette |
| `heron.glb` | HERON / Medic | `Worker.blend` | Teal/white rescue-medic palette |
| `lynx.glb` | LYNX / Recon | `SciFi.blend` | Cyan/navy sensor-operator palette |
| `magpie.glb` | MAGPIE / Scavenger | `Adventurer.blend` | Ochre/olive expedition palette |
| `jackal.glb` | JACKAL / Locksmith | `Punk.blend` | Violet/black infiltration palette |

Each preset contributes its own authored head, body, legs, feet, hair or
headgear, clothing silhouette, and materials. The Blender build applies the
role palette, normalizes the shared humanoid rig, adds runtime equipment
sockets, and retargets 25 actions from the separately recorded CC0 Quaternius
Universal Animation Library. No code-generated primitive geometry is used for
these character visuals.

Editable source files and the supplied license evidence are retained in
`source_art/third_party/quaternius_modular_women/`. Rebuild all five GLBs with:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_quaternius_female_operator.py
```
