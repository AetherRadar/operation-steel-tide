# Tencent HY-3D operator outputs (private)

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

The generated files are intentionally ignored by Git and remain in the local
private asset store. On 2026-09-05 the project owner reported confirmation
from Tencent customer support permitting use of these outputs in this game.
That confirmation is recorded as a local-use permission only; it does not
authorize publishing or redistributing the raw Tencent meshes from the public
MIT repository. The source responses and rigged FBX files stay private.
Credentials must never be committed.

Rebuild one role (Windows):

```powershell
blender --background --python scripts/blender/build_hy3d_operator.py -- `
  --source assets/models/quaternius_operators/viper.glb `
  --rigged <private-tencent-viper-rigged.fbx> `
  --output assets/models/hy3d_operators/viper.glb --triangles 60000
```
