# Steel Tide Reloadable AK-74N

- Original creator: Quaternius (`@Quaternius`)
- Source pack: https://poly.pizza/bundle/Ultimate-Guns-Pack-cpgUfI4t2F
- Exact source license: CC0 1.0 Universal
- License deed: https://creativecommons.org/publicdomain/zero/1.0/
- Original acquisition date: 2026-08-20
- DCC adaptation date: 2026-08-28
- Attribution required: No. Creator credit is retained as provenance.

`ak74_reloadable.glb` is a Blender-authored derivative of the finished AK-like
`Assault Rifle-fpLucho45C.glb` model already tracked as
`assets/models/quaternius_ultimate_guns/ak74.glb`. The DCC pass preserves all
1,382 source triangles and the five authored materials. It separates exactly
227 existing magazine triangles into `MagazineGeometry`; the remaining 1,155
triangles form `WeaponBodyGeometry`. No generated primitive, CSG replacement,
or marketplace-restricted content is included.

The separated magazine is attached at runtime beneath authored `Magazine` and
`SpareMagazine` mechanism nodes. This lets the actual finished mesh leave the
magazine well, follow the support hand, and return during reload instead of
animating an invisible marker over a static one-piece gun.

## Delivered files

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| Source `assets/models/quaternius_ultimate_guns/ak74.glb` | 78,616 | `34F9EA1E664444B024E29C3AD5E910912716B570FE803CE965DF19C8CE6907A1` |
| Runtime `assets/models/steel_tide_ak74/ak74_reloadable.glb` | 79,956 | `34032539D9DCD721FBD3C10B789CAC3D0229959DB7BFBE8FEF5033E241CFCFFC` |
| DCC source `source_art/reloadable_weapons/ak74_reloadable.blend` | 647,415 | `7CCAADE459FAF931F91321C970CDC3D3FEEC2892FD938800843DC4B09B8E461B` |

Reproducible build instructions and deterministic topology checks are recorded
in `source_art/reloadable_weapons/README.md` and
`scripts/blender/build_reloadable_ak74.py`.
