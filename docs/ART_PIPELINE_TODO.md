# Art Pipeline TODO

This list tracks the remaining replacement of programmer art with redistributable authored assets. Keep source URLs, exact licenses, acquisition dates, and local mappings current in `assets/models/LICENSE.md` and `docs/CONTENT_PROVENANCE.md`.

## Character pipeline

- [x] Add a CC BY 4.0 tactical soldier as the deployment and backpack operator preview.
- [x] Retain the original FBX, a cleaned Blender source, and a reproducible GLB import script.
- [x] Download the Quaternius CC0 animation libraries and archive their license evidence.
- [x] Retarget idle, walk, run, crouch, aim, hit, revive, and death clips to the Mixamo-compatible rig in Blender.
- [x] Add authored prone crawl, downed hold, revive recovery, and per-frame map grounding in Blender.
- [x] Replace the neutral preview stance with a relaxed authored idle after retargeting is stable.
- [x] Add named right-hand weapon, back weapon, head, vest, backpack, and team-patch sockets to the Blender rig.
- [ ] Split or mask authored equipment so helmet, armor, and backpack selections remain visually distinct.
- [x] Build a vendor-neutral Godot animation adapter and retain procedural fallback behavior for asset-load failures.
- [x] Replace friendly and hostile field operators; validate animation, sockets, hit regions, collision alignment, squad behavior, and map traversal.
- [ ] Produce at least two additional redistributable operator silhouettes to reduce clone repetition.

## First-person and vehicles

- [x] Replace the procedural first-person arms with a licensed authored arm set, fixed-scale pose classes, and named runtime palm mounts.
- [x] Replace the active AK with separate high-detail FP/world CC0 models, authored mechanisms, PBR materials, and a mesh-derived optic contact marker.
- [ ] Replace remaining procedural primary-weapon previews with licensed authored models, starting with SCAR-L, MP5A5, M24, and AXMC.
- [ ] Rework the extraction tilt-rotor silhouette, landing gear, fuselage materials, rotor blur, and boarding interior in Blender.
- [ ] Audit drivable vehicles and major visible props for remaining primitive presentation meshes.

## World art and presentation

- [ ] Continue replacing visible residential and industrial graybox modules with CC0 or CC BY authored kits while preserving current collision and navigation proxies.
- [ ] Reduce green emissive intensity and rebalance exposure at deployment, indoor, dusk, and extraction camera positions.
- [ ] Add authored decals, material variation, and damage dressing without increasing collision-body counts.
- [ ] Establish per-category budgets for triangles, materials, draw calls, texture memory, and LOD distances.

## Review gates

- [ ] Capture deployment, backpack, squad, enemy, and extraction frames at representative camera distances after each asset batch.
- [x] Inspect Blender deformation, clipping, socket presence, collision dimensions, material response, and animation action coverage.
- [x] Capture and inspect the AK at hip, ADS, optic-contact, fire, reload, and world/operator camera distances.
- [x] Verify Godot crowd, squad, stairs, residential, skybridge, skylink, vehicle, and general performance diagnostics with the animated operator.
- [ ] Confirm all imported files remain redistributable in this public repository before every push.
