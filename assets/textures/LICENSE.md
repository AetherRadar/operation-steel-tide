# Poly Haven Texture License

The following textures were acquired from [Poly Haven](https://polyhaven.com/)
on 2026-08-06 and are dedicated to the public domain under CC0 1.0
Universal:

| Texture | Creator | Official source | Repository-local mapping |
| --- | --- | --- | --- |
| Asphalt 03 | Charlotte Baglioni (photography); Dario Barresi (processing) | https://polyhaven.com/a/asphalt_03 | `asphalt_03_{diff,normal,rough}_1k.jpg` |
| Concrete Floor | eye-candy.xyz | https://polyhaven.com/a/concrete_floor | `concrete_floor_{diff,normal,rough}_1k.jpg` |
| Rusty Painted Metal | Amal Kumar | https://polyhaven.com/a/rusty_painted_metal | `rusty_painted_metal_{diff,normal,rough}_1k.jpg` |
| Corrugated Iron | Dimitrios Savva (photography); Jenelle van Heerden (processing) | https://polyhaven.com/a/corrugated_iron | `corrugated_iron_{diff,normal,rough}_1k.jpg` |
| Gravel Embedded Concrete | Charlotte Baglioni | https://polyhaven.com/a/gravel_embedded_concrete | `gravel_embedded_concrete_{diff,normal,rough}_1k.jpg` |

Exact license: CC0 1.0 Universal,
https://creativecommons.org/publicdomain/zero/1.0/. Attribution is not
required; creator names are retained as provenance and courtesy credit.

## Jianghai dusk HDRI

**Kloppenheim 06 (Pure Sky)** was created by Greg Zaal (Original), with sky
edits by Jarod Guest, and acquired from Poly Haven on 2026-08-28:

- Official asset page: https://polyhaven.com/a/kloppenheim_06_puresky
- Exact license: CC0 1.0 Universal
- Official 1K HDR download:
  https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/kloppenheim_06_puresky_1k.hdr
- Repository-local mapping: `kloppenheim_06_puresky_1k.hdr`
- Bytes: 1,173,154
- SHA-256:
  `206C67E3A1B992282821CF06662BDD69BBB4915C1C4444A66338A40D6A7D4E34`
- Official API MD5: `995d68b1656f26452572645c0ffe898b`

`JianghaiOldCityAtmosphere` loads this HDRI at runtime for the dusk sky. The
file is not packed into `jianghai_old_city.blend` and is not embedded in the
Jianghai map GLB. Attribution is not required by CC0; the contributor roles are
retained as provenance and courtesy credit.

## Jianghai Old City reuse

The authoritative Jianghai Old City Blender scene reuses four tracked CC0
surface sets above. Asphalt 03, Concrete Floor, and Gravel Embedded Concrete
provide the street, pavement, and stone-paving bases; Corrugated Iron provides
roof and industrial-metal surfaces. Rusty Painted Metal remains a tracked
project texture but is not packed into the current Jianghai `.blend` or GLB.
The four delivered sets were acquired on 2026-08-06 and integrated into the
Jianghai DCC scene beginning on 2026-08-27.

Adapted copies are packed in the authoritative DCC source
`source_art/world/jianghai_old_city/jianghai_old_city.blend` and exported with
the scene to `assets/models/jianghai_old_city/jianghai_old_city.glb`. They
retain their Poly Haven CC0 provenance and are not relicensed as
project-authored MIT content.

The eight finished Poly Haven models added to Jianghai Old City on 2026-08-28
also include their own CC0 1K texture sidecars. Those maps are components of
the Television 02, Exterior Aircon Unit, Rollershutter Window 03, Trashbag,
Utility Box 01, Barrel 03, Plastic Crate 02, and Security Camera 01 model
bundles, rather than standalone repository texture sets. Their creators,
official URLs, exact CC0 1.0 Universal license, acquisition hashes, and mapping
from the external cache into the packed `.blend` and runtime GLB are recorded
in `assets/models/LICENSE.md` and
`source_art/world/jianghai_old_city/LICENSE_EVIDENCE.md`.
For Security Camera 01, only the static geometry, materials, and textures are
delivered; the source rig and animations are not shipped.

The final 58,070,456-byte GLB has SHA-256
`6B8C5D35F0224D81125B44B304B5FE03E6F2523062F3BFB0861A00258CF66663`.
Its packed images combine the model and surface contributions in the
composite; the separately loaded HDRI remains outside the GLB and does not
change the four delivered reusable surface sets identified above.
Moving the 22 Rollershutter Window 03 and Exterior Aircon Unit instances from
the central avenue onto tenement facades changes only DCC placement, not the
source, license, or packed texture provenance. Final DCC QA deletes the
redundant `JianghaiArtPass_FactoryHeroShutter` instance after the damaged
factory shell is replaced by five finished CC0 buildings. That cleanup does not
remove or replace any texture source because Rollershutter Window 03 remains
used on the tenement facades and the two standalone Old City interactive-door
visuals.

The final factory-gate portal adds no untracked texture source. Its five
visible Blender objects reuse DCC-authored brick piers, caps, and a corrugated
roof with materials already packed in the authoritative scene; the corrugated
roof continues to use the tracked Corrugated Iron provenance above, and other
reused maps retain their existing records. The portal is DCC-authored final
art, not a visible primitive or procedural runtime substitute.

The Rollershutter Window 03 PBR maps are also embedded in the reproducibly
derived runtime door visual
`assets/models/jianghai_old_city/rollershutter_window_03.glb` (187,940 bytes;
SHA-256
`C4884AFCD7560E4BB23320A8C311DB0011504F7C5FEE30D58C266D54F7C6B166`).
That local mapping lets two Old City `InteractiveBuildingDoor` instances use
the finished shutter art without introducing a new texture source or changing
the MP / Poly Haven CC0 license. Door collision, animation, networking, and AI
traversal remain separate project gameplay behavior.

The final performance policy changes only high-tier shadow casting on fine
decorative meshes; models, materials, visibility ranges, and texture mappings
are unchanged. Capture tuples (draw calls / objects / primitives) are Overview
349/539/4,209,048, Victory street 427/547/3,947,301, Street-life bicycle
close-up 255/304/3,265,850, Guangchang pawnshop 218/390/2,046,993, Red Star
factory 343/403/4,032,380, and Market footbridge 424/663/4,680,828. All pass.
The run reports 825.2 MB video memory and 701.2 MB texture memory; full capture
evidence is retained in the Jianghai DCC
`README.md` and `LICENSE_EVIDENCE.md`.
