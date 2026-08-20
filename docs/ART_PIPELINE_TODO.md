# Art Pipeline TODO

This list tracks the remaining replacement of programmer art with redistributable authored assets. Keep source URLs, exact licenses, acquisition dates, and local mappings current in `assets/models/LICENSE.md` and `docs/CONTENT_PROVENANCE.md`.

## Character pipeline

- [x] Add a CC BY 4.0 tactical soldier as the deployment and backpack operator preview.
- [x] Retain the original FBX, a cleaned Blender source, and a reproducible GLB import script.
- [ ] Download a CC0 animation library and archive its license evidence.
- [ ] Retarget idle, walk, run, crouch, aim, hit, revive, and death clips to the Mixamo-compatible rig in Blender.
- [ ] Replace the neutral preview stance with a relaxed weapon-ready idle after retargeting is stable.
- [ ] Add named right-hand weapon, back weapon, head, vest, backpack, and team-patch sockets to the Blender rig.
- [ ] Split or mask authored equipment so helmet, armor, and backpack selections remain visually distinct.
- [ ] Build a Godot adapter scene that exposes the existing operator node contract without vendor-specific names in gameplay code.
- [ ] Replace friendly and hostile field operators only after animation, sockets, hit regions, and collision alignment pass `--validate-squad`, `--validate-stance-armor`, and `--validate-combat-models`.
- [ ] Produce at least two additional redistributable operator silhouettes to reduce clone repetition.

## First-person and vehicles

- [ ] Replace the procedural first-person arms with a rigged authored arm set that matches weapon sockets and reload clips.
- [ ] Replace remaining procedural primary-weapon previews with licensed authored models, starting with AK-74N, SCAR-L, MP5A5, M24, and AXMC.
- [ ] Rework the extraction tilt-rotor silhouette, landing gear, fuselage materials, rotor blur, and boarding interior in Blender.
- [ ] Audit drivable vehicles and major visible props for remaining primitive presentation meshes.

## World art and presentation

- [ ] Continue replacing visible residential and industrial graybox modules with CC0 or CC BY authored kits while preserving current collision and navigation proxies.
- [ ] Reduce green emissive intensity and rebalance exposure at deployment, indoor, dusk, and extraction camera positions.
- [ ] Add authored decals, material variation, and damage dressing without increasing collision-body counts.
- [ ] Establish per-category budgets for triangles, materials, draw calls, texture memory, and LOD distances.

## Review gates

- [ ] Capture deployment, backpack, squad, enemy, and extraction frames at representative camera distances after each asset batch.
- [ ] Inspect deformation, clipping, sockets, collision alignment, material response, shadows, draw calls, and texture memory in Godot.
- [ ] Confirm all imported files remain redistributable in this public repository before every push.
