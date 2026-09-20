# Tencent HY-3D operator outputs

The local `.glb` files in this directory are generated from Tencent HY-3D and
HY-3D-Rigging responses and converted in Blender by
`scripts/blender/build_hy3d_operator.py`. The Tencent mesh supplies the
realistic tactical appearance; the base gameplay actions come from the project's
CC0 Quaternius Universal Animation Library clips, retargeted and baked onto
the Tencent skeleton. The conversion adds the six Steel Tide weapon/gear
sockets and caps the delivered mesh at 60,000 triangles. The action set covers
locomotion, ready and aim weapon poses, shooting, reloading, melee, utility
throwing, interaction, pickup, healing, jump, slide, hit, downed, revive, and
death clips.

The existing finger chains remain in the rig. Their synthetic joints do not
match the glove surfaces, so the corrective pass bakes neutral distal channels
and preserves the authored glove shape. Grip placement is authored against
the visible glove rather than inferred from those synthetic joint positions.

The five delivered operators now carry a Blender-authored two-hand M4A1 pose:
`RifleCarrySocket` is parented to the firing hand, while `RightPalmFrame` and
`LeftPalmFrame` follow the actual hand bones. The rifle cant, stock pocket,
both arm chains, and a small aim lean are baked in Blender; Godot only follows
the authored socket and does not run corrective arm IK for these assets. The
editable source scenes are `source_art/hy3d_operators/{viper,heron,lynx,magpie,jackal}.blend`.

MAGPIE's HY-3D response was missing most of its right forearm and hand. The
original `repair_magpie_right_hand.py` copied only hand-dominant faces and
left an incomplete cuff. The current `repair_magpie_contacts.py` transfers
the same character's complete left forearm/hand surface, materials, and UVs
into the right arm's bone frame while preserving the original right sleeve.
The result is part of the authored skinned mesh.

The repair export keeps the imported armature in pose mode and marks every
imported action as a fake user. Blender's glTF exporter otherwise samples every
action from the bind pose, which turns the prone, downed, and death clips into
a static standing pose even though their action curves are present.

Each delivered operator also carries a Blender-authored `preview_stand`
action. It places the hips over the planted feet, levels the spine and
shoulders, and preserves the authored foot contact for the straight-on
homepage/loadout paper-doll. The runtime selects and pauses this action; it does
not apply a corrective skeleton transform. The original authoring pass was
`scripts/blender/author_hy3d_preview_stand.py`. Viper now uses the manual
Blender edit documented in `source_art/hy3d_operators/README.md`; do not
overwrite its saved pose with that automatic leveling pass.

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
| Viper | `viper.glb` | 23,174,264 bytes | `7477BC1D68F7ED37A4A55E2A0FABAFB3F7A49145FC0393629BEBC853526D6AA5` |
| Heron | `heron.glb` | 22,612,072 bytes | `7665A0E32E8E4AC209EF746E433F0B2314C40F56017D69318F95F8BA7082E498` |
| Lynx | `lynx.glb` | 38,574,812 bytes | `503579B3735ACCAD0BD853B153CE3B465D043016875CD7AD632327BF13974742` |
| Magpie | `magpie.glb` | 21,278,340 bytes | `9C265BCDE44ED88FEF6D6F4FA677789EF7F0E4BC5FCD2CDF9179B7FB3EF245A7` |
| Jackal | `jackal.glb` | 21,671,000 bytes | `36417C0D3C2DC15211964741425915241D493E4ECADBAAD4A9D331EA3AF2C28C` |

Rebuild one role (Windows):

```powershell
blender --background --python scripts/blender/build_hy3d_operator.py -- `
  --source assets/models/quaternius_operators/viper.glb `
  --rigged <private-tencent-viper-rigged.fbx> `
  --output assets/models/hy3d_operators/viper.glb --triangles 60000
```

The current corrective export runs `scripts/blender/repair_operator_presentation.py`
against the editable `.blend` scenes. It combines topology-aware garment weight
repair, CC0 UAL locomotion retargeting, authored floor contact, and sixteen
handgun clips. The four sidearm sockets and rifle socket follow the firing
hand. Godot selects clips and sockets and scales their playback speed to actual
travel using the exported `.locomotion.tres` measurements. The authored outer
`AuthoredOperatorPresentation` fixes the actor's size, forward direction, and
foot pivot; the gameplay wrapper uses an identity transform. Lynx's original long-hair mesh has a dedicated
head-parented chain instead of following the sleeves and shoulder straps.

See `docs/OPERATOR_PRESENTATION.md` for the reproducible command, imported-skin
regression gate, and Godot visual capture procedure.
