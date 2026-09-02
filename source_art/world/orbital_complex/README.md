# FALLTIDE RECOVERY ARRAY authored map

`FALLTIDE RECOVERY ARRAY` is an original 340 by 320 metre extraction-map
composition built on a decommissioned storm barrier. It is not a replica of a
real facility or another game's map. Its own visual and gameplay identity is a
return-capsule recovery, quarantine, and telemetry complex being restarted in
the middle of a storm-grid failure.

## Spatial composition

- **Intake Causeway** is the low-risk south approach, with customs offices,
  maintenance sheds, and two early flanking routes.
- **UNDERTOW SUMP / Blackwater Lift 03** sits just south of the west pressure
  bulkhead at the requested maintenance-console coordinate. A teardrop sump
  mouth, paired expanding volute pumps, double siphon arch, and wall-mounted
  tidal sight glass create a wet industrial signature while the marked
  console apron and west-service lane remain open for traversal.
- **Capsule Dry Dock** is a true 4-metre-deep central cut in the service deck.
  It contains a fictionally scorched recovered return article, authored access
  bridges, two bypass roads, and the first strong cross-map sightline. The
  general Majadroid source library retains a recovery crane and rigging set,
  but those variants are intentionally not embedded in this enclosed delivery.
- **Breaker Yard** uses oxidized machinery halls, dense horizontal cover, and a
  west-side upper catwalk chain.
- **Quarantine Archive** contrasts that with faded aerospace-white structures,
  cyan cold-storage accents, an enterable authored command-hall reuse, and an
  east-side upper route.
- **Telemetry Spine** is dominated by the suspended adapted dish centered near
  Godot `(0, -14, -34)` and its surrounding power/vault structures.
- **Cathode Well / Coolant Cathedral** occupies the north transition between
  the reactor and launch silo. A recessed black mouth, four pressure rings,
  sixteen tapered service ribs, a suspended cyan coolant bundle, and a
  north-south maintenance bridge make the vertical maintenance route readable
  before the player reaches the tide gate.
- **Data Ossuary / Quarantine Memory Aisle** is the east-ring detour behind
  the archive halls. Twelve faceted archive spires carry individual cyan
  memory seams beneath three pressure arches and a suspended halo; the aisle
  reads as a sealed records vault rather than another generic office block.
- **Tide Gate** terminates the north route with flanking ballast halls, control
  towers, a two-leaf animated gate, a secondary skiff anchor, public alarm
  lighting, and the primary extraction anchor.

The three authored height bands are the low dry dock, the main service deck,
and the repeated upper catwalk network. The visible terrain is not a runtime
`BoxMesh`/CSG graybox: it is a Blender-authored water grid, irregular reclaimed
deck, stormward sea wall, dry-dock ring/floor, causeway, and UV-mapped service
road system. Major buildings and hero objects remain authored third-party
geometry with exact provenance.

## Art direction

- oxidized red steel;
- faded aerospace white;
- wet black metal and concrete;
- cyan insulation/ceramic accents;
- sodium-orange emergency lighting;
- cyan powered-route guidance against a dark storm sky.

Concrete, asphalt, and rusty painted metal use embedded 1K CC0 Poly Haven PBR
maps. The remaining palette is intentionally restrained so the dish, capsule,
powered route, and district colors work as long-distance navigation landmarks.

## Runtime hierarchy

The GLB preserves stable unique interaction nodes and pivots:

- static authored dish pedestal outside the motion axes, then `DishYaw` ->
  `DishPitch` -> authored reflector/feed/truss assembly;
- `District_CoolantCathedral` with original pressure-ring, service-rib, coolant
  bundle, bridge, and imported compressor/sawtooth/loading-bay modules;
- `District_DataOssuary` with original archive-spire/halo detail and imported
  inspection, shift, and window-hall modules;
- `District_UndertowSump` with the teardrop mouth, paired volute spirals,
  siphon arch, tidal sight glass, and a surface-only interaction-console apron;
- `UndertowSumpPowered` nested under `PowerZone_Powered`, so water-level marks
  and the service trace wake with emergency power without changing the
  landmark's minor-prop collision role;
- `CathodeWellPowered` and `DataOssuaryPowered` nested under
  `PowerZone_Powered`, so the coolant core, memory seams, floor guidance, and
  inner halo wake with emergency power while the structural silhouettes stay
  readable in blackout;
- `TideGateLeft` and `TideGateRight` with outer hinge pivots;
- `VaultDoorLeft` and `VaultDoorRight` with local slide origins;
- `UpperBypassBarrier`, an authored west-catwalk gate that slides upward when
  stage 1 restores the bypass, eliminating an invisible gameplay blocker;
- `AlarmLight_Central`, `AlarmLight_Breaker`, `AlarmLight_Archive`, and
  `AlarmLight_TideGate`;
- `PowerZone_Blackout` and `PowerZone_Powered` visibility groups.

Each movable root stores its Godot Y-up runtime animation axis/range or travel
string in GLB extras, with the Blender source-up axis recorded separately where
it differs. Twelve stable `POI_*`, `Spawn_*`, and `Extraction_*` anchors provide
the runtime assembler with map-authored positions.

## Coordinates

The source is modeled in metres with Blender Z-up. During glTF export, Blender
positive Y maps to Godot negative Z.

- Blender horizontal bounds: X `-170..170`, Y `-100..220`.
- Godot horizontal bounds: X `-170..170`, Z `-220..100`.
- Godot horizontal centre: `(0, -60)`.
- Cathode Well centre: Blender `(0,126)`, approximately Godot `(0,-15.6,-126)`;
  Data Ossuary centre: Blender `(133,126)`, approximately Godot
  `(133,-15.6,-126)`.
- Undertow Sump console: Blender `(-112,-42,-15.6)`, Godot
  `(-112,-15.6,42)`; the visual apron reserves a four-metre interaction bay.
- Verified footprint: exactly `340 x 320 m` after Blender GLB round trip.

## Rebuild and verification

Authoritative source: `source_art/world/orbital_complex/orbital_complex.blend`

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" --background --python scripts/blender/build_orbital_complex_underground.py
```

The reproducible underground builder imports only the listed source assets,
applies the fictional map-specific materials and transforms, builds the
enclosed terrain/hardscape, creates stable interaction pivots, renders review
images, saves the compressed `.blend`, exports one embedded GLB, clears Blender,
reimports that GLB, and repeats the extent/node/material/provenance audit.

The generated `build_report.json` is the authoritative machine-readable audit.
It emits the exact mesh/material/image counts for each rebuild, including the
second-pass Cathode Well, Data Ossuary, and Undertow Sump detail. Static source compositions
are DCC-consolidated per placement, while every animated root remains separate.
All surviving mesh nodes have a material, node names are unique, and every
required interaction node survives the round trip.

## Review renders

- `previews/overview_top.png` — footprint, district balance, road graph, and
  landmark silhouette from above.
- `previews/south_player_height.png` — player-scale read from Intake Causeway.
- `previews/central_landmark.png` — real lower dry dock and recovered capsule.
- `previews/north_tide_gate_powered.png` — powered cyan guidance, sodium alarm
  wash, animated-gate framing, and the extraction approach.
- `previews/cathode_well.png` — player-scale view into the pressure-ring coolant
  cathedral and its maintenance bridge.
- `previews/data_ossuary.png` — east-ring view of the cyan archive spires,
  memory seams, and suspended halo.
- `previews/undertow_sump.png` — player-scale blackwater lift station, paired
  pump volutes, siphon arch, and clear maintenance-console approach.

Full source/license mapping is in `LICENSE_EVIDENCE.md` and
`assets/models/orbital_complex/LICENSE.md`.
