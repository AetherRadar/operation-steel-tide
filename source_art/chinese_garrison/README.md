# Painted garrison source

`uniform_design.blend` and `helmet_design.blend` preserve the project owner's
unfinished Blender design from the earlier character task: an olive uniform,
hand-painted guard badge, and a separately authored helmet. The uniform was
painted on the approved HY-3D Heron surface; the helmet was separated from the
approved HY-3D operator asset. These are existing project-authorized Tencent
service outputs, not newly acquired third-party models or an MIT relicensing.

`scripts/blender/build_distinct_garrison.py` transfers the painted material to
the corrected Heron UVs and binds the helmet to the authored head frame. It
retains repaired skinning, the upright gait, closed shot recoil, and authored
weapon sockets. The final editable source is
`source_art/combat_models/enemy_operator.blend`; the runtime export is
`assets/models/enemy_operator/enemy_operator.glb`.

The two unique image names, `GarrisonUniformPaint` and `GarrisonHelmetPaint`,
prevent Godot's extracted-image reuse from substituting the old Viper atlas.
Rebuilding the garrison must never copy Viper's entire source over this design.
Required rights and acquisition records remain in `assets/models/LICENSE.md`
and `docs/CONTENT_PROVENANCE.md`.
