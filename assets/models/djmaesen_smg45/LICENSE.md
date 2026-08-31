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
  `smg45_pistol_service_arms.glb`, `smg45_pistol_large_arms.glb`, and
  `animated_reload_arms.glb` (with their generated `*_Image_*.png` texture
  sidecars)
- Build scripts: `scripts/blender/build_djmaesen_smg45.py`,
  `scripts/blender/build_first_person_arms.py`, and
  `scripts/blender/build_animated_reload_arms.py` (63,519 bytes, SHA-256
  `DDEE2191373ED8E4395166CEA921A05EFA64C5753B618BAF5A0785E5138FF682`)
- Animated arms reproducible DCC source:
  `source_art/third_party/djmaesen_fps_smg45/animated_reload_arms.blend`
- Static pose variants generated: 2026-08-24
- Animated reload-arm derivative generated: 2026-08-29
- Animated left-arm IK continuity and elbow-pole revision: 2026-08-30
- Static-matched compact sidearm reload endpoints generated: 2026-08-31
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
`animated_reload_arms.glb` is an arms-only skinned derivative that removes the
visible SMG while retaining the authored glove/sleeve materials, finger bones,
skin weights, and 13,700-triangle full-arm topology. It also contains a
9,306-triangle `SidearmReloadForearmsMesh` made by cropping both authored
sleeves to glove-length cuffs, so pistol reloads do not expose distorted full
arms; both meshes share the original skin and armature. The authored frame-155
two-hand surface is baked as the new bind pose, and 24 named tactical/empty
reload clips cover M4A1, AK74, SCAR-L, MP5A5, M24, AXMC, AWM, VSS, P226,
M1911, GSh-18, and Desert Eagle. Its palm markers use evaluated glove contact
centers, while
`RightGripFrame` preserves the source SMG's real primary-grip transform. No
procedural replacement geometry is introduced. The 2026-08-30 DCC revision
solves left-hand position separately from wrist rotation, makes every baked
quaternion track hemisphere-continuous before glTF export, and exports one
platform-specific elbow-pole marker per profile. The 2026-08-31 follow-up then
hard-matches every pistol clip boundary back to the exact `pistol_service` or
`pistol_large` static pose so the exchange stays compact and the support arm
does not jitter back into a full-arm silhouette. Contract revision 5 places
`LeftSidearmMagazineAnchorFrame` on an actual glove-side triangle selected by
BVH at the camera-calibrated signed offsets of -39.7 mm lateral, -11.8 mm
forward, and 61.3 mm below the evaluated palm center. Aligning this point with
the magazine uses a compact platform-calibrated wrist pose: -10/25/42 degrees
for P226 and M1911, 0/5/42 degrees for GSh-18, and -9/28/30 degrees for Desert
Eagle. Tactical and empty pistol clips reuse the exact `pistol_service` or
`pistol_large` static DCC pose at normalized time 0.00 and 1.00. Because runtime
already translates the cropped chain onto the real magazine, the DCC clip keeps
position IK disabled and eases only the compact wrist grip in by 0.15, holds it
through 0.82, then eases it out. This avoids a duplicate shoulder/elbow sweep
and removes the static-to-skinned visibility-boundary flip. The builder verifies
that every LEFT_CHAIN endpoint stays within 0.01 radians and 3 mm of its matching
static pistol pose, and that the magazine marker remains on the skinned glove at
the extract and insert grip frames of every P226, M1911, GSh-18, and Desert Eagle
clip. It also rejects any clip whose left shoulder, elbow, or wrist rotation,
joint translation, palm translation, or total hand travel exceeds the compact
sidearm continuity limits; these changes preserve the same mesh, materials,
skin, and arm hierarchy.
The original model and all derived geometry remain copyright DJMaesen and are not
covered by the repository's MIT license.
