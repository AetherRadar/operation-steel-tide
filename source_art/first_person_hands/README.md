# First-Person Hand Kits

`operator_hand_kits.blend` is the editable Blender source for the role-specific
first-person hand variants used by Operation Steel Tide.

The source mesh is derived from **fps animated smg** by DJMaesen under CC BY
4.0. The Blender pass creates five authored role subtrees:

- `Viper`
- `Heron`
- `Lynx`
- `Magpie`
- `Jackal`

Each subtree contains `Rifle`, `PistolService`, `PistolLarge`, `Smg`, and
`Ladder` families. The static families use role-specific sleeve length,
cuff width, glove proportions, and materials. The SMG family keeps the source
reload rig and animation player. The ladder family is an authored hand pose
with only the short visible forearm needed by the first-person camera.

The deterministic builder is
`../../scripts/blender/build_operator_hand_kits.py`. It imports the existing
licensed Blender/GLB derivatives, applies the role-specific DCC edits, packs
the source, and exports
`../../assets/models/djmaesen_smg45/operator_hand_kits.glb`. Runtime mapping,
attribution, exact license, hashes, and the mixed rights boundary are recorded
in `../../assets/models/djmaesen_smg45/LICENSE.md`.
