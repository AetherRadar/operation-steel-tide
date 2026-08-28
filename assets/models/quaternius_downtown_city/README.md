# Downtown City MegaKit selection

This directory contains the selected standard-edition modules used by Saint
Marais Old Town, the player-facing identity of extraction Map 02, and selected
facade modules reused in Jianghai Old City.

- Creator: Quaternius (`@Quaternius`)
- Official source: https://quaternius.com/packs/downtowncitymegakit.html
- Acquired: 2026-08-19
- License: CC0 1.0 Universal; see `QUATERNIUS_LICENSE.txt`
- Local selection: 21 glTF scenes, their external binary buffers, and 26 shared 1K PNG texture maps

The files were copied from the standard free glTF package without geometry or texture modification. Original filenames remain unchanged so each scene keeps its external buffer and texture references. The downloaded source archive is not tracked.

Jianghai Old City's packed Blender source and runtime map GLB reuse
`Brick_Plain_1.gltf` as 18 visible instances and `DoorFrame_Trim.gltf` as two
visible instances. They form two 10-object personnel-entry facades: nine brick
modules and one doorframe at the pawnshop, and the same allocation at the
factory. The current interactive door inside each facade is Kenney Factory Kit
CC0 `../kenney_factory_kit/door-hinged.glb`, configured for a
1.45-by-2.65-meter opening and a normal 96-degree side swing. The retained
Jianghai Rollershutter Window 03 derivative is no longer the current door
visual. Exact scene hashes, instance counts, and runtime mapping are recorded in
`../../../source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`.

The referenced CS:GO Town Sketchfab model was not downloaded or redistributed. It informed the broad old-town/street-density direction only; every shipped third-party file in this directory comes from the CC0 Quaternius package above.
