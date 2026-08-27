# Quaternius Ultimate Modular Women operator sources

This directory retains the editable source presets and license evidence for
the five visually distinct extraction operators used by Operation Steel Tide.

- `Soldier.blend` is the unmodified VIPER source.
- `Worker.blend` is the unmodified HERON source.
- `SciFi.blend` is the unmodified LYNX source.
- `Adventurer.blend` is the unmodified MAGPIE source.
- `Punk.blend` is the unmodified JACKAL source.
- `LICENSE.txt` is the license file supplied in the official download folder.
- `quaternius_female_soldier_animated.blend` is the generated, retargeted
  working file retained from the original single-operator integration.

Run `scripts/blender/build_quaternius_female_operator.py` with Blender 4.5 or a
compatible newer release to regenerate all five runtime GLBs under
`assets/models/quaternius_operators/`. Pass `-- --variant <callsign>` to build
one character, or `-- --save-working-blends` when editable retargeted working
files are also needed.

- Source: https://quaternius.com/packs/ultimatemodularwomen.html
- Official download: https://drive.google.com/drive/folders/1720N9IGyQHXYvtvZJzazhxtTTlz-y2Vf
- License: CC0 1.0 Universal / Public Domain Dedication
- Acquired: 2026-08-27
