# Trey Ramm Modular Industrial Kit selection

This directory contains Godot-ready compositions of authored modules used by the Tideglass Reactor demolition arena.

- Creator: Trey Ramm (OpenGameArt username `minime453`)
- Official source: https://opengameart.org/content/modular-industrial-kit
- Official download: https://opengameart.org/sites/default/files/modular_industrial_pieces.zip
- Acquired: 2026-08-27
- License: Creative Commons Zero 1.0 Universal (CC0 1.0)
- Attribution: Not required; the creator's requested courtesy credit is retained
- Source and license evidence: `source_art/third_party/trey_modular_industrial/SOURCE_PAGE.html` and `ORIGINAL_README.txt`
- Selected source files: `source_art/third_party/trey_modular_industrial/Meshes/` and `PacificNorthwestGradientAtlas.png`
- Reproducible conversion: `scripts/blender/build_trey_modular_industrial.py`

The runtime GLBs in this directory combine selected walls, arches, windows, doors, stairs, floors, foundations, roofs, trims, and columns from the original modular kit. `ASSET_OVERVIEW.png` is retained with the selected sources as package evidence.

Runtime compositions:

- `east-security-gate.glb`
- `west-service-gate.glb`
- `arch-gateway.glb`
- `loading-bay.glb`
- `elevated-walkway.glb`
- `window-hall.glb`
- `sawtooth-service-hall.glb`
- `utility-office.glb`

The two gate compositions use different authored door layouts. At runtime they
close the east and west openings in the Majadroid perimeter fence; the
Tideglass diagnostic checks their exact AABBs, opaque solid materials, multi-height
visible triangle coverage, and alignment with the matching boundary collision.

The loading bay, utility office, central service hall, and window hall are
closed one-storey compositions. The Blender build validates continuous authored
front, rear, left, and right perimeter coverage against the roof footprint,
rejects the source kit's two-storey window and angled-roof pieces for these
assemblies, and verifies the exported dimensions through a GLB round trip.
Runtime diagnostics additionally check module counts, single-storey height,
solid-material opacity, visible render layers, and tight collision padding.

The elevated walkway uses four complete authored platform modules, two
symmetrical ten-step wide stairs that meet the deck at both ends, and authored
guard rails along both long edges. Its Blender
build rejects corner stairs, partial platform panels, asymmetric landings, and
disconnected stair tops, and measures every rail against the deck structure with
world-space mesh BVHs. Runtime uses the visible 22-mesh assembly itself for
scale-baked, double-sided concave collision, sweeps a player capsule upward from
below the deck, and physically walks a player up both stairways.

The composed scenes are adaptations of Trey Ramm's authored modules. They retain the source kit's CC0 dedication and are not relicensed under the repository's root MIT license.
