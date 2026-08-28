# Special Operations authored command hall

`operations_office_set.glb` is the Godot-ready, one-storey command-hall set used by the Special Operations home screen. Its open camera side frames three staggered operator workstations, a ready lounge, an asymmetric tactical console, a demolition preparation counter, a panoramic industrial window wall, and the authored bridge and helipad visible beyond it. The varied desk angles, monitor counts, chair offsets, storage cabinets, and equipment cases keep the quick-start and demolition views from reading as repeated furniture rows. Shallow Trey roof trims and column caps form a restrained overhead service-rib grid that breaks up the dark roof from all three interactive look directions without entering workstation or window sightlines.

Every visible mesh is an instance of a redistributed CC0 source already tracked by this repository. No generated primitive, CSG mesh, downloaded runtime dependency, camera, or light is exported as visible art.

## Source mapping

- Structure, windows, floor, roof, columns, bridge, helipad, and trim: Trey Ramm / minime453, Modular Industrial Pieces, CC0 1.0, acquired 2026-08-27, from `source_art/third_party/trey_modular_industrial/`.
- Desks, desk chairs, computer screens, tactical table, lounge furniture, and equipment cabinets: Kenney Furniture Kit 2.0, CC0 1.0, acquired 2026-08-26, from `assets/models/kenney_furniture_kit/`.
- Trey source and license evidence: `source_art/third_party/trey_modular_industrial/SOURCE_PAGE.html` and `ORIGINAL_README.txt`.
- Kenney license evidence: `assets/models/kenney_furniture_kit/KENNEY_LICENSE.txt`.

The GLB root stores source creator, source URL, exact license, unit, assembly, and authored-geometry policy metadata. Every visible node stores its local source mapping and CC0 license. The packed GLB has no external buffer or image URI.

## Rebuild

Authoritative DCC source: `source_art/operations_office/operations_office_set.blend`

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.5\blender.exe" --background --python scripts/blender/build_operations_office_set.py
```

The deterministic script imports and instances only the named authored source assets, saves the compressed `.blend`, exports the GLB, then round-trips it through Blender. The validation rejects missing provenance, missing authored workstation groups, missing or shifted gameplay anchors, implausible dimensions, empty geometry, and external GLB dependencies.

## Runtime coordinates

The set is modeled in meters with Blender Z-up. Blender positive Y maps to Godot negative Z during glTF export. The open camera side is at negative Blender Y; the panoramic windows face positive Blender Y.

- Command hall footprint: about 28 m wide by 16 m deep.
- Exterior bridge and helipad extend the complete set to about 39 m deep.
- Helipad deck center: Blender `(4, 21, 0.4)`, corresponding to Godot local `(4, 0.4, -21)`.
- `AircraftAnchor`: Blender `(4, 21, 1.55)`, matching the existing aircraft resting height.
- Exported anchors: `CameraAnchor`, `NeutralLookAnchor`, `QuickLookAnchor`, `DemolitionLookAnchor`, `OperatorStandAnchor`, `OperatorDeskAnchor`, `AircraftAnchor`, `QuickLightAnchor`, and `DemolitionLightAnchor`.

## Verified build

- Overall bounds: `28.400 x 38.400 x 4.304 m`.
- Authored placements: 510.
- Exported mesh nodes: 529, sharing 28 unique authored mesh datablocks.
- Render triangles: 15,894.
- Materials: 24, including one embedded Trey palette image.
- Gameplay anchors: 9/9.
- Compressed authoritative `.blend`: 850,552 bytes.
- Embedded runtime GLB: 1,020,772 bytes.
- GLB SHA-256: `3D91DF3C59651890C725A3A8301ACEC4E67EA3917DBCABAFA30EEB7812708C59`.

The values above were verified through a Blender GLB round trip, and two clean background rebuilds produced the same GLB hash. Neutral, quick-start, demolition, and helipad views were also rendered from player-scale camera distances to check scale, silhouettes, overhead depth, materials, furniture placement, window sightlines, and deck continuity.
