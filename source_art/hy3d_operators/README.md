# HY-3D operator Blender sources

## Viper manual standing-pose edit, 2026-09-19

`viper.blend` is the editable source of truth for the current Viper preview.
The change was made interactively in Blender 4.5.10 Pose Mode, then exported
to `assets/models/hy3d_operators/viper.glb`. No pose-generating script or
runtime correction was used to create this revision.

The `preview_stand` action has two identical keys, at frames 0 and 1.
Its right shin was rotated inward by 14 degrees in front view to remove
the sideways knee bend; its lower spine was rotated forward by 8 degrees
in side view to reduce the backward lean. The source mesh, materials,
skin weights, 58 rest bones, sockets, and all 38 gameplay actions are
unchanged. A read-only comparison against the previous source confirmed
that only the eight quaternion channels for `RightLeg` and `Spine` changed.
The [read-only audit](review/viper-manual-pose-audit.json) preserves the
before/after key values and source hashes for future animation merges.

Continue editing this saved action in Blender. Do not rerun
`scripts/blender/author_hy3d_preview_stand.py` on Viper: automatic leveling
would overwrite the reviewed pose. Export the operator root and its
descendants with skins, materials, and all actions enabled. Exporting is
format conversion only; no geometry or bone transforms should be rewritten.

The runtime now binds a stowed weapon to the asset's `BackWeaponSocket`.
It previously created an attachment at the raw chest-bone origin, discarding
the saved Blender socket's location and orientation. This binding change
uses the authored node directly and adds no corrective transform.

Godot 4.6.3 renders from the actual delivery GLB, with identical camera and
lighting for the front comparison:

- [Previous front](review/viper-before-front.png)
- [Revised front](review/viper-after-front.png)
- [Revised side](review/viper-after-side.png)

The review confirms the reduced shin tilt and backward lean, intact hands and
equipment surfaces, and normal embedded-material loading. The standalone
Godot review loaded all 39 delivered animations. These images show the
authored body without the separately attached gameplay weapon.

[Full in-game roster](review/viper-authored-socket-roster.png) verifies the
final preview and stowed weapons after binding the authored back socket.

Rights remain as recorded in `assets/models/LICENSE.md` and
`docs/CONTENT_PROVENANCE.md`: this is an edit to the previously authorized
Tencent service output, not a new third-party acquisition or an MIT
relicensing of the generated asset.
