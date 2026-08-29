# Bazaar Crossing Runtime Art

`bazaar_crossing.glb` is the standalone static visual for the Bazaar Crossing
demolition map. It is deterministically exported from the map-local pinned
palette and packed DCC output under `source_art/world/bazaar_crossing/` by
`scripts/blender/build_bazaar_crossing.py`.

The GLB uses embedded materials and textures, a meter-scale root at the origin,
and Godot Y-up coordinates. Godot owns collision, navigation, spawns, bomb
sites, AI directives, and smooth traversal ramps.

Current verified export:

- 31,089,036 bytes;
- 277 mesh nodes / 116 unique meshes / 298 DCC material surfaces;
- 272,916 unique and 2,771,825 instanced triangles;
- Blender-authored bounds X `[-68, 68]`, Y `[-56, 56]`, Z `[-0.16, 7.5021]`
  (Blender Y corresponds to negative Godot Z before glTF axis conversion);
- six exact-endpoint, 3.2 m finished CC0 Trey stair assemblies;
- three authored tiled decks with visible CC0 undersides, foundations, edge
  trim, supports, and capitals;
- fourteen open runtime-aligned CC0 Trey guardrails, stair rails/newels, and a
  complete authored-module Mid canopy;
- finished CC0 crate/barrel/cart cover clusters, eight elevated market props,
  and seven visibly supported lanterns;
- 46 DCC textures with a 127.473 MiB RGBA8 plus mip-chain estimate;
- all 277 serialized visible meshes material-backed, UV-complete, CC0-sourced,
  provenance-complete, and exact-triangle validated after GLB round trip.

The current hashes, exact per-stair/deck/guardrail Trey module mappings, and
complete validation counters are written to
`source_art/world/bazaar_crossing/bazaar_crossing_build_report.json` on every
rebuild. See `LICENSE.md` before copying or adapting embedded source content.
