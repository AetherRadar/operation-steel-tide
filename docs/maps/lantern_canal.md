# Lantern Canal extraction map

`lantern_canal` is the fifth deployment map and uses the extraction loop: deploy on the west quay, search the two workshop objectives, then reach the canal extraction.

## Gameplay layout

- **Qingci Ceramics Relay** — three floors of the Qingci ceramics shop at `(22.5, 67)`. The relay terminal is on the west shopfront; rare and epic supplies are staged on the upper floors.
- **Caiyun Silk Manifest** — three floors of the Caiyun silk shop at `(22.5, -21)`. The manifest terminal is on the west shopfront; the upper floors hold a guarded route through the looms and storage rooms.
- **West quay** — deployment at `(-68, 1.32, 76)`, inside the authored paved area, with a starter pistol case nearby.
- **Canal extraction** — extraction at `(-68, 1.12, -88)`, more than 140 m from deployment.
- Six patrol enemies circulate between the quays and both workshops. Nine loot sources are placed across the three floors and quays.

The GLB supplies the visible buildings, furniture, stairs, lights, PBR materials, and embedded textures. `LanternCanalCollisionBuilder` builds invisible static collision from the imported world-space ground, bridges, and structural architecture. The street top is Y=1.12; workshop ground floors are Y=1.255, with upper floorboard tops at Y=5.105 and Y=8.905. No flat slab below the city substitutes for those surfaces. Circulation collision follows the authored return stairs, and squad traversal links follow their two flights and landing. Collision excludes decorative materials and inaccessible upper architecture, with an explicit triangle budget. The outer bounds and minimap cover the authored ground, X=-80..80 and Z=-192..86.

`LanternCanalLighting` disables the GLB's two preview directional lights so the deployment sun and fill are the only global lights. It restores architectural shadows, calibrates local lantern energy/range and distance fading, and applies restrained exposure, ambient light, and fog for each selected time of day. Quality changes reapply this map's lighting; night vision remains a subsequent gameplay override.

Run `--validate-lantern-canal` after changing the map. Alongside content and gameplay checks, it verifies deterministic authored-surface probes, settled player ground clearance at five locations, and the lighting budget. Run `--capture-lantern-canal` for street, player-camera, workshop, and time-of-day visual review. Geometry or collision changes also require `--validate-residential`, `--validate-stairs`, `--validate-skylinks`, and `--validate-vehicle-drive`.
