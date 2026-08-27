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
- Animated first-person sleeve fit revised: 2026-08-27

Attribution: **"fps animated smg" by DJMaesen, licensed under CC BY 4.0.**

The original GLB embeds the creator, source URL, title, and `CC-BY-4.0`
license in `asset.extras`. `smg45_first_person.glb` retains the authored
materials, skin weights, and two-handed reload action. Its cloth sleeves are
slimmed at the wrist and their shoulder-side vertices are extended through a
smooth Blender deformation so the open source-mesh cuts remain below the
first-person camera at idle, ADS, and throughout reload. The adaptation does
not add boundary caps or replace the authored topology, UVs, or animation.
`first_person_arms.glb` is the older split-arm adaptation, and
`smg45_weapon.glb` separates the field weapon from the first-person rig. The
three static arm variants are separate Blender-generated evaluations of the
same authored mesh: a rifle pose, a compact service-pistol two-handed stance,
and a large-pistol two-handed stance. They do not apply the animated SMG sleeve
extension; each exports explicit palm and wrist frame markers so runtime code
can translate the pose without accumulating rotations on the arm mesh. The
original model and all derived geometry remain copyright DJMaesen and are not
covered by the repository's MIT license.
