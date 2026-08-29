# Bazaar Crossing V2 Runtime Art

`bazaar_crossing.glb` is the standalone static visual for the dense Bazaar
Crossing V2 demolition arena. It is deterministically exported from the pinned
map-local Blender palette by `scripts/blender/build_bazaar_crossing.py`.

The runtime GLB is tracked through Git LFS because the Godot-compatible,
non-Draco export exceeds GitHub's normal 100 MiB object limit. Run
`git lfs pull` if a checkout contains the pointer instead of the binary.

The GLB uses embedded materials and textures, uncompressed mesh streams, a
meter-scale origin root, and Godot Y-up coordinates. Blender's Draco exporter
is explicitly disabled because Godot 4.6 does not support
`KHR_draco_mesh_compression`; the build parses the GLB JSON and fails if that
extension appears. Godot owns collision, navigation, spawns, bomb sites, AI
routes, and smooth traversal surfaces.

Current verified export:

- 112,618,852 bytes, SHA-256
  `CA68AC570E2FAA9FF284FBB25909888BE4AC93F9C661106525A6204801C43164`;
- 729 visible mesh nodes / 669 unique meshes / 1,032 DCC material surfaces,
  reduced from 1,492 nodes / 2,129 surfaces without changing triangles;
- 850,309 unique and 1,148,671 delivered instance triangles;
- four complete enterable interiors: A Caravanserai, B Market Warehouse, Mid
  Indoor Connector, and Defender Back Market;
- 37 coherent closed modular city blocks with complete wall returns, cornices,
  and roofs; only four legacy whole-building facades remain as outer landmarks;
- A `y=3.6`, B `y=3.4`, and Mid `y=3.2` interior decks;
- six exact-endpoint, 3.2 m authored stairs, all at or below 18 degrees, with
  guardrails/newels and three attached roofed stair vestibules;
- A/B Mid-junction doors offset in Z with two internal baffles, preventing an
  A-to-B sightline while preserving Mid-to-site splits;
- 58 DCC textures, each at most 1024 px, with a 203.473 MiB RGBA8 plus
  mip-chain estimate;
- 135 continuous storage parts, 108 continuous shopfront parts, 109 roofline
  articulation parts, and a four-part back-market service gate;
- Blender-authored bounds X `[-68,68]`, Y `[-56.2,56.2]`, Z `[-0.16,9.46]`;
- all visible meshes material-backed, UV-complete, CC0-sourced,
  provenance-complete, and exact-triangle validated after GLB round trip;
- no `KHR_draco_mesh_compression`; `EXT_texture_webp` is the only required
  extension, while `KHR_materials_ior` and `KHR_materials_specular` are also
  used.

The current hashes, palette SHA, source-module counts, interior/cover gates,
stair endpoints, and round-trip counters are written to
`source_art/world/bazaar_crossing/bazaar_crossing_build_report.json` on every
rebuild. See `LICENSE.md` and the full local source record before copying or
adapting embedded content.
