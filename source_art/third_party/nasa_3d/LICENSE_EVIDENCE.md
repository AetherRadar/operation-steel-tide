# NASA source and usage evidence

Evidence reviewed and acquisition recorded on 2026-09-01.

## Official source records

- NASA Science, [70 meter dish](https://science.nasa.gov/3d-resources/70-meter-dish/):
  identifies the source as NASA/Ames Research Center and offers the original GLB
  download. The retained file is `nasa_70_meter_dish.glb`, 2,216,584 bytes,
  SHA-256 `36FF56A7A2BFD1C278F6F4774D32128D5931F2C22FE58241D00EE7D1815634BB`.
- NASA Science, [Orion Capsule](https://science.nasa.gov/3d-resources/orion-capsule/):
  offers the `Orion Capsule (no fbc).stl` download. The retained file is
  `nasa_orion_capsule_no_fbc.stl`, 2,586,884 bytes, SHA-256
  `ABC4C69C27AFA55C4A06BC9972B8872979F1473FB26E15224FB0F77F1CD81DC7`.

## Usage basis and restrictions observed

NASA's official [Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/)
state that NASA media and the files used to render three-dimensional models,
including polygon data and texture maps, are generally not subject to U.S.
copyright. The same guidelines require factual source acknowledgement, prohibit
implying NASA endorsement, warn that separately identified third-party content
is not covered, and impose separate restrictions on NASA identifiers and logos.

This project therefore applies the following concrete controls:

1. NASA is recorded factually as the source of the two raw meshes.
2. The finished scene contains no NASA insignia, worm logotype, seal, mission
   patch, employee likeness, or visible source branding.
3. The recovered capsule is a fictionally recolored and damaged environmental
   object; the game does not present it as a real Orion vehicle.
4. The dish is used as a fictional telemetry-array landmark and is materially
   altered. The game does not present the map as a NASA facility.
5. Repository and in-scene notices explicitly disclaim NASA review, approval,
   sponsorship, and endorsement.
6. No claim is made that NASA licensed the overall map under MIT. Only the
   project's original composition, hardscape, metadata, and build script use
   the repository's MIT license; source-derived mesh rights remain governed by
   the NASA guidance above.

The raw dish GLB contains two embedded `ANTENNA` image payloads (WEBP and JPEG)
referenced by one source texture. They are retained unchanged inside the raw
NASA download under the same NASA media-use basis, but the builder replaces the
hero materials and the runtime GLB does not embed those source images. No
separately identified third-party copyright marking was found in either raw
model. The build imports only the selected 70-metre dish mesh, discards
non-target source hierarchy/metadata, and exports no visible NASA identifier.
