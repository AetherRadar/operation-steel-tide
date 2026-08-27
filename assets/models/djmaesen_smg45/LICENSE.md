# FPS Animated SMG License

The runtime models in this directory are adapted from **fps animated smg** by
**DJMaesen**.

- Creator: DJMaesen (`https://sketchfab.com/bumstrum`)
- Source: `https://sketchfab.com/3d-models/fps-animated-smg-ea3dad7478624495a5a46f40127b0579`
- License: Creative Commons Attribution 4.0 International (CC BY 4.0)
- License text: `https://creativecommons.org/licenses/by/4.0/`
- Acquired: 2026-08-21
- Original tracked file:
  `source_art/third_party/djmaesen_fps_smg45/fps_animated_smg.glb`
- Original SHA-256:
  `61F30D8980CE292869F97D98587A2736BAF719A19C0A32756838BD9EF2ADA83A`
- Runtime adaptations: `smg45_first_person.glb`, `first_person_arms.glb`,
  `smg45_weapon.glb`, `smg45_rifle_arms.glb`,
  `smg45_pistol_service_arms.glb`, and `smg45_pistol_large_arms.glb` (with
  their generated `*_Image_*.png` texture sidecars)
- Build scripts: `scripts/blender/build_djmaesen_smg45.py` and
  `scripts/blender/build_first_person_arms.py`
- Static pose variants generated: 2026-08-24
- Animated first-person sleeve fit and upper-arm volume revised: 2026-08-28
- Service-pistol support-arm pose revised in Blender: 2026-08-28
- First-person weapon uniformly enlarged around the authored two-hand grip center: 2026-08-28

Attribution: **"fps animated smg" by DJMaesen, licensed under CC BY 4.0.**

The original GLB embeds the creator, source URL, title, and `CC-BY-4.0`
license in `asset.extras`. `smg45_first_person.glb` retains the authored
materials, skin weights, and two-handed reload action. Its cloth sleeves keep
the fitted wrist profile, then widen through a smooth forearm blend and receive
additional shoulder-side radial expansion so the full circumference continues
below the first-person camera at idle, ADS, and throughout reload.
The source mesh's existing open boundary loops are sealed with Blender fill
faces so an oblique camera cannot reveal hollow sleeve cuts. This preserves the
authored vertex set, UV layers, skin weights, materials, and animation while
adding only the closure faces needed by the first-person adaptation.
The first-person-only weapon geometry is enlarged uniformly by 8% around the
midpoint of the two authored palm anchors; the muzzle marker follows the same
transform. No weapon axis is stretched independently, and the field weapon
export remains at its established world scale.
`first_person_arms.glb` is the older split-arm adaptation, and
`smg45_weapon.glb` separates the field weapon from the first-person rig. The
three static arm variants are separate Blender-generated evaluations of the
same authored mesh: a rifle pose, a compact service-pistol two-handed stance,
and a large-pistol two-handed stance. They do not apply the animated SMG sleeve
extension; each exports explicit palm and wrist frame markers so runtime code
can translate the pose without accumulating rotations on the arm mesh. The
service-pistol variant offsets the authored shoulder and bakes a new wrist IK
target and elbow pole so P226, M1911, and GSh-18 use a bent support arm without
pulling the complete limb toward the camera. This revision changes only the
evaluated skeletal pose and marker placement; it does not add source geometry.
The original model and all derived geometry remain copyright DJMaesen and are not
covered by the repository's MIT license.
