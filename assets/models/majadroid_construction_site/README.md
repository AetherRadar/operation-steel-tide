# Majadroid 3D House Construction Site selection

This directory contains Godot-ready conversions of authored construction-site models used by the Tideglass Reactor demolition arena.

- Creator: Majadroid / Maik Hoffmann
- Palette credit recorded by the source package: Imphenzia
- Official source: https://opengameart.org/content/3d-house-construction-site-lowpoly-cc0
- Official download: https://opengameart.org/sites/default/files/lowpoly-house-construction-site-by-majadroid_2.zip
- Acquired: 2026-08-27
- License: Creative Commons Zero 1.0 Universal (CC0 1.0)
- Attribution: Not required; creator credits are retained as a courtesy
- Source and license evidence: `source_art/third_party/majadroid_construction_site/INFO.txt` and `Overview.png`
- Reproducible conversion: `scripts/blender/build_tideglass_map_assets.py`

The local runtime selection consists of `building.glb`, `concrete-truck-red.glb`, `construction-materials.glb`, `containers-cargo.glb`, `containers-office.glb`, `crane-on-ground.glb`, `fence.glb`, `ground.glb`, and `road.glb`, with the palette textures exported beside the scenes. The conversion deliberately selects only `Concrete Truck Red`, `Office Container Stack`, and `Cargo Container Blue Boxes` for their respective outputs. `construction-materials.glb` keeps three distinct authored pieces (`Planks Wood V3`, `Box Stack Brown`, and `Barrel`) and spaces them into a readable cover line. A build-time AABB-overlap check rejects accidentally stacked variants whose shared volume reaches 80 percent of the smaller mesh. The selected original FBX files and `ImphenziaPalette01-256-Gradient.png` are retained under `source_art/third_party/majadroid_construction_site/`.

The Blender conversion preserves the authored geometry, restores the source palette material, centers and grounds each scene, converts it to meter-scale glTF coordinates, and embeds creator, source, and CC0 metadata. The construction tower is compressed heavily on its horizontal axes and lightly on its vertical axis so it fits the arena with a playable 2.56 m floor spacing. These derived third-party assets retain the source package's CC0 dedication and are not relicensed under the repository's root MIT license.
