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
| `viper.glb` | VIPER / Assault | `Soldier.blend` | Refined existing mesh; orange/charcoal assault palette |
| `heron.glb` | HERON / Medic | `Worker.blend` | Refined existing mesh; teal/white rescue-medic palette |
| `lynx.glb` | LYNX / Recon | `SciFi.blend` | Refined existing mesh; cyan/navy sensor-operator palette |
| `magpie.glb` | MAGPIE / Scavenger | `Adventurer.blend` | Refined existing mesh; ochre/olive expedition palette |
| `jackal.glb` | JACKAL / Locksmith | `Punk.blend` | Refined existing mesh; violet/black infiltration palette |

Each preset contributes its own authored head, body, legs, feet, hair or
headgear, clothing silhouette, and materials. These are refinements of the
same five Quaternius models, not replacements sourced from another character
pack. The reproducible Blender build increases surface density with selective
per-mesh subdivision, adds shape-preserving edge creases and smooth shading,
and configures role-aware Principled PBR values for skin, hair, fabric, armor,
polymer, metal, and leather. No image textures or newly authored UV artwork are
introduced by this adaptation.

After subdivision, the build keeps the strongest four deform-bone influences
per vertex and normalizes those weights before export; non-deforming selection
and control groups are removed before export so they cannot become glTF skin joints.
It retains the runtime root, rig, four
character mesh nodes, equipment sockets, and 25-action animation contract while
normalizing the shared humanoid rig and retargeting the separately recorded CC0
Quaternius Universal Animation Library actions. No code-generated primitive
geometry is used for these character visuals.

Editable source files and the supplied license evidence are retained in
`source_art/third_party/quaternius_modular_women/`. Rebuild all five GLBs with:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" `
  --background --factory-startup `
  --python scripts/blender/build_quaternius_female_operator.py
```
