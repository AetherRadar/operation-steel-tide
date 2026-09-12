# Tencent HY-3D operator outputs

The local `.glb` files in this directory are generated from Tencent HY-3D and
HY-3D-Rigging responses and converted in Blender by
`scripts/blender/build_hy3d_operator.py`. The Tencent mesh supplies the
realistic tactical appearance; the 38 gameplay actions come from the project's
CC0 Quaternius Universal Animation Library clips, retargeted and baked onto
the Tencent skeleton. The conversion adds the six Steel Tide weapon/gear
sockets and caps the delivered mesh at 60,000 triangles. The action set covers
locomotion, ready and aim weapon poses, shooting, reloading, melee, utility
throwing, interaction, pickup, healing, jump, slide, hit, downed, revive, and
death clips.

The conversion also guarantees a 30-bone finger rig (three phalanges for each
of five fingers on both hands). Finger chains are weighted to the authored palm
mesh and receive per-action curl keys, so the right hand can close around the
primary grip while the left hand supports the foregrip without the mitten mesh
being swallowed by the rifle. Existing finger chains are preserved when the
private source already contains them; wrist-only HY-3D responses receive the
fallback chains during this Blender build.

The five delivered operators now carry a Blender-authored two-hand M4A1 pose:
`RifleCarrySocket` is parented to `Spine2`, while `RightPalmFrame` and
`LeftPalmFrame` follow the actual hand bones. The rifle cant, stock pocket,
both arm chains, and a small aim lean are baked in Blender; Godot only follows
the authored socket and does not run corrective arm IK for these assets. The
editable source scenes are `source_art/hy3d_operators/{viper,heron,lynx,magpie,jackal}.blend`.

MAGPIE's HY-3D response was missing the disconnected right-hand surface. The
delivery rebuilds it with `scripts/blender/repair_magpie_right_hand.py`, which
transfers the authored left-hand surface, materials, UVs, and finger weights
into the right-hand bone frame before the locomotion and carry passes. The
exported `MagpieRightHandPatch` is part of the final skinned character; it is
not a runtime primitive or pose workaround.

The repair export keeps the imported armature in pose mode and marks every
imported action as a fake user. Blender's glTF exporter otherwise samples every
action from the bind pose, which turns the prone, downed, and death clips into
a static standing pose even though their action curves are present.

Each delivered operator also carries a 39th Blender-authored `preview_stand`
action. It places the hips over the planted feet, levels the spine and
shoulders, and preserves the authored foot contact for the straight-on
homepage/loadout paper-doll. The runtime selects and pauses this action; it does
not apply a corrective skeleton transform. The reproducible authoring pass is
`scripts/blender/author_hy3d_preview_stand.py`.

The delivered Viper also carries bone-parented `LeftPalmFrame`,
`RightPalmFrame`, `LeftWristFrame`, `RightWristFrame`, shoulder frames,
`ChestClearanceFrame`, and `HeadBaseFrame` landmarks. These are authored in the
carry reference pose so Blender retargets and runtime diagnostics can measure
human/weapon contacts directly.

The project owner confirmed on 2026-09-08 that these five converted outputs
may be redistributed with this repository. They remain generated service
outputs and are not relicensed as MIT; the permission covers the delivered
GLBs listed below. The original Tencent responses and rigged FBX files stay in
the private asset store. Credentials must never be committed.

Delivered files (all self-contained GLBs with embedded textures):

| Role | File | Size | SHA-256 |
| --- | --- | ---: | --- |
| Viper | `viper.glb` | 21,736,428 bytes | `B1CDA6190C36710D2B6BD153E42C2BC550E3A4810486639C054C857C01B2F3BE` |
| Heron | `heron.glb` | 21,476,084 bytes | `62A7BEEE92A969E4DEB4BF2B0A2EC6954D5A55C3C7C90D95F3316899D05AF351` |
| Lynx | `lynx.glb` | 22,770,236 bytes | `8E1D71FC4DE2CC63D6B436F0D4228296046349310E952FAE86CB3B370BB03089` |
| Magpie | `magpie.glb` | 21,134,168 bytes | `02DFF23FF8BB0C900E8680FCA9793B38D236893420BA64B471211443C8160677` |
| Jackal | `jackal.glb` | 21,976,500 bytes | `1842272C00E794F7ACF3832965CFC85B1056808274283702ED4C9E03FDADC7DA` |

Rebuild one role (Windows):

```powershell
blender --background --python scripts/blender/build_hy3d_operator.py -- `
  --source assets/models/quaternius_operators/viper.glb `
  --rigged <private-tencent-viper-rigged.fbx> `
  --output assets/models/hy3d_operators/viper.glb --triangles 60000
```

The checked-in delivery runs `scripts/blender/retarget_hy3d_locomotion.py` and
`scripts/blender/clean_hy3d_operator_exports.py` after conversion. The first
bakes the upright CC0 Quaternius walk cycle onto the
HY-3D leg chains while keeping the torso neutral for runtime two-hand rifle IK;
the script accepts an existing HY-3D GLB, a matching Quaternius source GLB, and
an output GLB via `--input`, `--source`, and `--output`.
