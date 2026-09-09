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
| Viper | `viper.glb` | 21,687,844 bytes | `985C33144A7FD8A69506F81886BCA84CE880D2170AEEB572AE7455835B12C994D` |
| Heron | `heron.glb` | 21,420,252 bytes | `C4CF1FF4310A7340FD13DEA92915F2B020DFB7B9619B6153AED5AA2AEFAD3016` |
| Lynx | `lynx.glb` | 22,984,376 bytes | `03AB5A06ADD4E38D9EC4CBD828A2CE72E1AC110946139E2DC0790B7AB115E071` |
| Magpie | `magpie.glb` | 21,225,816 bytes | `1AB991FB165B49FB51AE146A7427E1443EAD9B8D868F2D2F23A7B70AF43B4784` |
| Jackal | `jackal.glb` | 22,192,312 bytes | `BFD045911A92EE1B1AC34FFCFA2D6C363E492940E58C4AEFD49E4BBA768694D0` |

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
