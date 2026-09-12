# Lantern Canal World runtime asset

This directory contains the authored extraction-map assets imported from the
repository owner's `AetherRadar/lantern-canal-world` release **v2.5**.

- Runtime asset: `lantern_canal_world_v2_5.glb` (589,056,208 bytes; 11,611,673 triangles)
- Inspection slice: `lantern_canal_workshops_v2_5.glb` (45,575,920 bytes)
- Source release: https://github.com/AetherRadar/lantern-canal-world/releases/tag/v2.5
- Upstream scene: `craft_workshops_v2_5_detail.glb`
- Import date: 2026-09-12
- SHA-256: `c80512850cce17714b3e8ee4ec75e3c2bfda8631776295b26041483bad769d23`
- Blender source: `source_art/lantern_canal_world/lantern_canal_workshops_v2_5.blend`
- Blender inspection: 4.5.10, 29 objects, 61 materials, 75 embedded images;
  source SHA-256 `6d4f531169eb31a58cf6e293c80c72a6b61205e2b48d87014f4f5756fdf5f52b`

The complete city contains the Qingci ceramics workshop and Caiyun silk shop,
their three-floor interiors, authored stairs, workshop furniture, embedded PBR
textures, named inspection cameras, and authored interior light nodes. Map 05
loads this single complete city GLB. The former Jianghai Old City backdrop is
reduced to its authored mountain ring only, so no duplicate city shell or
floating storefront geometry is rendered.

The Godot map adapter owns only invisible collision/navigation scaffolding and
gameplay placement. It does not reshape, rescale, or visually repair the GLB.
