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
| Viper | `viper.glb` | 21,687,648 bytes | `CD454A45AAD26D359964A14DC9D513CDF7B030DAC550A4F71B7E6F3D63D728B5` |
| Heron | `heron.glb` | 21,433,124 bytes | `81A190D7C4F54E4D8381E7E647A674799F4BEB06F3ED3A2C4B1283B522C8539F` |
| Lynx | `lynx.glb` | 22,709,020 bytes | `2F8256A0E424FF7C1BA9C12B121FAF77C7A7E49E939EC55044EE13B4C96A0091` |
| Magpie | `magpie.glb` | 20,084,204 bytes | `753EB9E41B03269FAF89CA4E2D2B78B973DAF271072A0437ABB0B667F6EC8654` |
| Jackal | `jackal.glb` | 21,915,992 bytes | `088ACE1CCA20E4CF708670B8E551DA213055D93B23935FD012A7E8BC5E7F4173` |

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
