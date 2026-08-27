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

The final 95,837,888-byte post-fix GLB has SHA-256
`F61D82D77311BF1C2F8A3ACE1C0FFE967EC415220DABA9BF840237EC797CD0FA`
and reports 100 serialized image resources in total. That total combines the
packed images used by all model and surface contributions in the composite; it
is not a count of standalone source assets and does not change the four
delivered reusable surface sets identified above.
Moving the 22 Rollershutter Window 03 and Exterior Aircon Unit instances from
the central avenue onto tenement facades changes only DCC placement, not the
source, license, or packed texture provenance. Final DCC QA deletes the
redundant `JianghaiArtPass_FactoryHeroShutter` instance and retains the
red-brick factory facade's existing embedded shutter. The removed duplicate
does not remove or replace any texture source because Rollershutter Window 03
remains used on the tenement facades.

The final factory-gate portal adds no untracked texture source. Its five
visible Blender objects reuse DCC-authored brick piers, caps, and a corrugated
roof with materials already packed in the authoritative scene; the corrugated
roof continues to use the tracked Corrugated Iron provenance above, and other
reused maps retain their existing records. The portal is DCC-authored final
art, not a visible primitive or procedural runtime substitute.

The Rollershutter Window 03 PBR maps are also embedded in the reproducibly
derived runtime door visual
`assets/models/jianghai_old_city/rollershutter_window_03.glb` (1,587,684 bytes;
SHA-256
`48E78DFC37FF6310151B18BEA8AC8B080BE31ABED4BD882C0FA3F46E19B0B4B1`).
That local mapping lets two Old City `InteractiveBuildingDoor` instances use
the finished shutter art without introducing a new texture source or changing
the MP / Poly Haven CC0 license. Door collision, animation, networking, and AI
traversal remain separate project gameplay behavior.

The final performance policy changes only high-tier shadow casting on fine
decorative meshes; models, materials, visibility ranges, and texture mappings
are unchanged. Capture tuples (draw calls / objects / primitives) are Overview
627/784/8,249,404, Victory street 838/1,093/9,599,741, Guangchang pawnshop
253/534/2,980,673, Red Star factory 444/628/4,705,187, and Market footbridge
514/764/4,843,093. All pass. The run reports 1,008.1 MB video memory and 862.4
MB texture memory; full capture evidence is retained in the Jianghai DCC
`README.md` and `LICENSE_EVIDENCE.md`.
