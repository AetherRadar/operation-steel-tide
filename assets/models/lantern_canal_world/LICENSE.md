# Lantern Canal World source permission

The GLB in this directory was supplied by the Operation Steel Tide repository
owner from their own public repository:

- Creator: AetherRadar / Operation Steel Tide repository owner
- Source URL: https://github.com/AetherRadar/lantern-canal-world
- Exact source release: `v2.5`, published 2026-09-11
- Source license: repository-owner permission; the upstream repository does
  not publish a separate SPDX license file. The owner explicitly authorizes
  this copy for use and redistribution in Operation Steel Tide.
- Acquisition date: 2026-09-12
- Local mapping: `lantern_canal_world_v2_5.glb` -> complete Map 05 city;
  `craft_workshops_v2_5_detail.glb` -> `lantern_canal_workshops_v2_5.glb`
- Blender inspection source: `source_art/lantern_canal_world/lantern_canal_workshops_v2_5.blend`
- Blender source SHA-256: `6d4f531169eb31a58cf6e293c80c72a6b61205e2b48d87014f4f5756fdf5f52b`
- Complete city SHA-256: `ab651fc398e9c77432d9ece3e434220fbff02d87f6dffe24edc5d7d883037cdb`
- Workshop slice SHA-256: `c80512850cce17714b3e8ee4ec75e3c2bfda8631776295b26041483bad769d23`

The upstream validation report records 11,611,673 triangles in the complete
city, 215 embedded images, no external textures, six named cameras, and 357/357
sampled stairs/platform/doorway clearance probes. The original complete
589 MB city and the separate 45.6 MB workshop extraction remain for
inspection and Blender round-trip work. Neither file is relicensed as MIT by
the project code.

## Runtime derivatives, 2026-09-20

The same owner-authorized city is optimized in Blender, retaining its UVs,
materials and named traversal geometry. Mapping:
`source_art/lantern_canal_world/lantern_canal_runtime.blend` to
`lantern_canal_runtime.glb`, SHA-256
`85B4D3CB9AC0E2969D49C6FC6CC945B530B5093B08755169870244966AC9FB54`.
Acquisition/source release and redistribution permission remain as above.
No additional attribution condition or new third-party asset is introduced.

The mountain-only source/export pair `lantern_mountains.blend` / `.glb` retains
the twelve project-authored Jianghai mountain meshes and their existing
project-authored materials. Creator: Operation Steel Tide contributors;
source: this repository's `source_art/world/jianghai_old_city/jianghai_old_city.blend`;
license: root MIT; extraction date: 2026-09-20; no external attribution required.
Export SHA-256:
`747A6B747BB088A37D373DFB305658FDBA7EA329A9B7A3111DC8BFA877E41E56`.
