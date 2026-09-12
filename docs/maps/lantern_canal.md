# Lantern Canal extraction map

`lantern_canal` is the fifth deployment map and uses the extraction loop: deploy on the west quay, search the two workshop objectives, then cross the canal district to the south quay extraction.

## Gameplay layout

- **Qingci Ceramics Relay** — three floors of the Qingci ceramics shop at `(22.5, 67)`. The relay terminal is on the west shopfront; rare and epic supplies are staged on the upper floors.
- **Caiyun Silk Manifest** — three floors of the Caiyun silk shop at `(22.5, -21)`. The manifest terminal is on the west shopfront; the upper floors hold a guarded route through the looms and storage rooms.
- **West quay** — deployment at `(-68, 92)` with a starter pistol case and a long sightline into the workshop lanes.
- **South canal extraction** — extraction at `(-68, -88)`. The route is intentionally longer than 140 m and includes fixed squad traversal links.
- Six patrol enemies circulate between the quays and both workshops. Nine authored-map loot sources are placed across the three floors and quays.

The GLB supplies the visible buildings, furniture, stairs, lights, PBR materials, and embedded textures. The Godot adapter adds invisible collision, stair ramps, squad traversal links, objective terminals, loot, patrols, extraction, and minimap landmarks. This keeps collision and navigation deterministic without modifying the authored meshes.

Run `--validate-lantern-canal` after changing the map. The diagnostic checks both named workshop roots, imported mesh/light counts, collision budget, objective/loot counts, and deterministic deployment/extraction spacing.
