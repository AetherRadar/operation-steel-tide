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
  `scripts/blender/build_animated_reload_arms.py` (113,403 bytes, SHA-256
  `7951A3967F8CFDFC306F9898CF4F298210041CB3414852408403B73C2B52913E`)
- Animated arms reproducible DCC source:
  `source_art/third_party/djmaesen_fps_smg45/animated_reload_arms.blend`
- Static pose variants generated: 2026-08-24
- Animated reload-arm derivative generated: 2026-08-29
- Animated left-arm IK continuity and elbow-pole revision: 2026-08-30
- Static-matched compact sidearm reload endpoints generated: 2026-08-31
- Camera-safe six-family pose-to-pose reload clips rebuilt: 2026-08-31
- Dedicated long-gun forearm crop and runtime layer rebuilt: 2026-08-31
- Articulated magazine grasp, real-prop contact following, and professional
  platform-specific slide manipulation rebuilt/reviewed: 2026-09-01
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
skin weights, and 13,700-triangle full-arm topology. Contract revision 8 keeps
that complete topology as the non-runtime `FullReloadArmsAuditMesh`, adds a
12,686-triangle `LongGunReloadForearmsMesh` with both authored hands and 28
source-unit (0.42 m) elbow-length cuffs, and retains the 9,334-triangle
`SidearmReloadForearmsMesh` with 16 source-unit (0.24 m) compact cuffs (9,306
triangles after Godot removes 28 collinear cut-ring faces). All
three meshes share the original skin and armature, materials, UVs, and normalized
weights. Runtime selects the long-gun crop for rifles/SMGs and the shorter crop
for pistols; the full audit mesh is always hidden. A geometry-free
`ReloadArmsMesh` compatibility layer preserves the existing runtime diagnostic
name without rendering the formerly intrusive upper-arm cloth. The authored frame-155
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
does not jitter back into a full-arm silhouette. Contract revision 8 places
`LeftSidearmMagazineAnchorFrame` on an actual glove-side triangle selected by
BVH at the camera-calibrated signed offsets of -39.7 mm lateral, -11.8 mm
forward, and 61.3 mm below the evaluated palm center. Aligning this point with
the magazine uses a compact platform-calibrated wrist pose: -10/25/42 degrees
for P226 and M1911, 0/5/42 degrees for GSh-18, and -9/28/30 degrees for Desert
Eagle. Tactical and empty pistol clips reuse the exact `pistol_service` or
`pistol_large` static DCC pose at normalized time 0.00 and 1.00. Their cropped
forearms now use a deterministic analytical shoulder/elbow solve in Blender,
with a compact position-and-wrist path for the magazine and an overhand slide
beat on empty reloads; runtime selects and samples the baked clip without adding
a second shoulder translation. This removes the former generic-IK branch flip
and the static-to-skinned visibility-boundary flip.

The 2026-09-01 revision replaces the former open-palm approximation with a
purpose-authored `magazine_grasp` pose. The thumb opposes four independently
curled finger chains around the real magazine, and runtime moves the installed
or staged magazine to the DCC glove contact instead of moving the arm toward a
detached prop. Every extraction/insertion key is rejected unless all five digit
chains exceed 0.65 radians and the glove-anchor clearance stays between 45 and
60 mm. The delivered clips measure 1.291766 radians at the thumb,
2.225294-2.280825 radians across the fingers, and 51.759 mm clearance. Service
pistols and Desert Eagle retain distinct magazine paths and now use distinct
slide contact/pull/hold/release timing; the support hand releases while the
slide returns under spring force. Runtime sidearm wrist correction and hidden
shoulder translation were removed, leaving the exported Blender performance as
the only arm motion.

The revision groups the twelve profiles into six readable choreography sets:
straight rifle (M4A1/SCAR-L), rock-and-lock (AK74/VSS), MP5, precision/internal
(M24/AXMC/AWM), service pistol (P226/M1911/GSh-18), and Desert Eagle. Long guns
use direct exchange poses instead of the retired waist-pouch arc, with explicit
old-magazine-out and new-magazine-seat holds. Empty clips add a mechanical
contact/pull/hold/release beat, while M24 retains its bolt beat in both tactical
and empty clips. The builder verifies zero hold drift, a 0.65 m control envelope
for long guns and 0.32 m for pistol crops, fixed shoulder roots and right grip,
hemisphere-continuous quaternions, 45 mm maximum pistol joint/palm step, exact
pistol endpoints within 0.01 radians and 3 mm, glove-surface marker contact, and
all 24 exported clip durations. Sidearm validation also verifies grasp
clearance and per-digit curl at extraction and insertion. It additionally samples every other frame of
all sixteen long-gun clips in the right-grip camera proxy: delivered spans peak
at 0.719082 m horizontal, 1.183342 m depth, 0.643858 m vertical, and 0.552705 m
rear extent. The retained full-arm layer is deliberately rejected by the same
gate at 1.843685/2.900767/0.979012 m with 2.357507 m rear extent, so the prior
near-plane giant-sleeve failure cannot satisfy the build contract. These changes
preserve the authored materials, skin, and arm hierarchy.
The original model and all derived geometry remain copyright DJMaesen and are not
covered by the repository's MIT license.
