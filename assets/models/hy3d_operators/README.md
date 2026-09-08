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

The project owner confirmed on 2026-09-08 that these five converted outputs
may be redistributed with this repository. They remain generated service
outputs and are not relicensed as MIT; the permission covers the delivered
GLBs listed below. The original Tencent responses and rigged FBX files stay in
the private asset store. Credentials must never be committed.

Delivered files (all self-contained GLBs with embedded textures):

| Role | File | Size | SHA-256 |
| --- | --- | ---: | --- |
| Viper | `viper.glb` | 21,908,112 bytes | `6A7EB9DD13C7F79E252DADEF1D1B29FD68A5BE19B49C34204A0FA0ECE369D2CE` |
| Heron | `heron.glb` | 22,652,672 bytes | `AD86823F01A50AFBE1CE23B45A89DF74BDA25A1DD22B69D146D0B465E5935427` |
| Lynx | `lynx.glb` | 24,804,160 bytes | `A6927B982B8B8073A5D41266F53795A641CD18F80541630906B16BFFB9C16711` |
| Magpie | `magpie.glb` | 21,659,412 bytes | `C9A7A2B9EC5EA2019C1F1015181BCA492018B9EE732684F9FEFB92248F3DE0D4` |
| Jackal | `jackal.glb` | 23,986,088 bytes | `D527DD2005776FFA7C64016972E753AF2510C086957BA9984EB241A7965288F0` |

Rebuild one role (Windows):

```powershell
blender --background --python scripts/blender/build_hy3d_operator.py -- `
  --source assets/models/quaternius_operators/viper.glb `
  --rigged <private-tencent-viper-rigged.fbx> `
  --output assets/models/hy3d_operators/viper.glb --triangles 60000
```
