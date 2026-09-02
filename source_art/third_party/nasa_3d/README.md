# NASA 3D source selection for FALLTIDE RECOVERY ARRAY

This directory preserves the two unmodified NASA source downloads used as hero
geometry in the fictional `FALLTIDE RECOVERY ARRAY` extraction map.

| Local source | NASA source | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `nasa_70_meter_dish.glb` | [70 meter dish](https://science.nasa.gov/3d-resources/70-meter-dish/) — source credited by NASA to NASA/Ames Research Center | 2,216,584 | `36FF56A7A2BFD1C278F6F4774D32128D5931F2C22FE58241D00EE7D1815634BB` |
| `nasa_orion_capsule_no_fbc.stl` | [Orion Capsule](https://science.nasa.gov/3d-resources/orion-capsule/), download labelled `Orion Capsule (no fbc).stl` | 2,586,884 | `ABC4C69C27AFA55C4A06BC9972B8872979F1473FB26E15224FB0F77F1CD81DC7` |

- Acquired: 2026-09-01.
- The raw files are retained byte-for-byte under the normalized local names
  listed above; only the filenames were normalized for repository use.
- Their use follows the [NASA Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/).
- NASA is acknowledged as the source. Nothing in this project implies NASA's
  review, approval, sponsorship, or endorsement.
- No NASA insignia, logotype, seal, employee likeness, or mission mark is used
  in the delivered map. The final material treatments contain no source logo.

The map builder scales the dish to `0.62`, gives it a fictional weathered white
and oxidized-red material treatment, separates the connected low pedestal from
the reflector/feed/truss components, and places only that moving assembly below
stable `DishYaw` and `DishPitch` pivots. The capsule is scaled to `0.33`,
recolored, selectively scorched, impact-posed, and named only as a fictional
recovered return article inside the runtime scene. These changes are recorded
in the generated build report and do not alter the immutable files here.

See `LICENSE_EVIDENCE.md` for the media-guideline interpretation and
`source_art/world/orbital_complex/README.md` for the complete composite mapping.
