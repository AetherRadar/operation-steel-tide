# Steel Tide Reloadable SCAR-L

## Required attribution and source

- Original work: **ScarL**
- Creator: **AdamKokrito**
- Source page: https://poly.pizza/m/ab1V8RlPDc
- Exact license: **Creative Commons Attribution 3.0 Unported (CC BY 3.0)**
- License deed: https://creativecommons.org/licenses/by/3.0/
- Acquired: 2026-08-29
- Adapted: 2026-08-29

Distributions of this derivative must credit AdamKokrito, link the source page,
name and link CC BY 3.0, and indicate that Operation Steel Tide modified the
work. The original and derivative art are not relicensed as MIT by this
repository.

The Blender adaptation normalizes the authored model to a 1.58 m Godot-space
contract; removes only the source presentation camera, light, and unused UV
channels; retains the real `Base`, `Stock`, `Mag`, `Trigger`, `Bolt`,
`CharginHandle`, `Safety`, and `IronSight` meshes; welds coincident hard-surface
vertices; applies a 1.4 mm two-segment bevel and weighted normals; tunes scalar
metallic/roughness values; adds gameplay pivots/sockets; and creates an
independent spare by duplicating the complete authored magazine mesh. No
programmatic primitive or CSG geometry is presented as finished art.

## File identity

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `../../../source_art/third_party/adamkokrito_scarl/adamkokrito_scarl.glb` | 114,000 | `B67738744962E20E1829008064153D7817D3B4532391D759582028DB225E3308` |
| `scarl_reloadable.glb` | 176,684 | `C8FDC9F7C780E27D4EA87A3D4B7C00FDF7CACE2902F6A5A765DC5B8AB992FEC0` |
| `scarl_reloadable_preview.png` | 1,492,750 | `0F24C3CB0A7BF0B6A233308AC3033AE56A1A0419BCC04BD5F3E291CED0CD9E8E` |
| `../../../source_art/reloadable_weapons/scarl_reloadable.blend` | 1,091,832 | `C94017345E323A2D2613DCFAE6E9AE9BE55AAA9C3BB84E4EC3457A7EB527B7CA` |

The reproducible builder is
`../../../scripts/blender/build_reloadable_scarl.py`. It verifies the exact
source bytes/hash, source nodes and per-node topology/materials, source bounds,
refined per-node topology, independent magazine mesh data, visible action
geometry, root and fixed-node identity, metre scale, root-space socket
positions and surface distances, installed/staged bounds, stable GLB and
preview hashes, and a Blender glTF round trip. Unused UVs are deliberately
removed after beveling because the source has no images or textures; this also
makes the exported buffer bit-for-bit reproducible across headless runs.

## Runtime contract

The source's independent `Mag`, `Bolt`, and `CharginHandle` meshes remain real
visible mechanisms. `ChargingHandle` moves the authored `BoltGeometry` and
`ChargingHandleGeometry` together, while tactical reload can leave the pivot at
its authored rest frame.

```text
SteelTideReloadableScarL                 [identity root; Godot metres]
|- WeaponBodyGeometry                   [5,248 triangles]
|- StockGeometry                          [668 triangles]
|- TriggerGeometry                        [228 triangles]
|- SafetyGeometry                         [540 triangles]
|- IronSightGeometry                      [832 triangles]
|- Magazine               (0.000000, 0.008324,-0.502861)
|  |- MagazineGeometry                    [132 triangles]
|  `- MagazineGripSocket
|- SpareMagazine         (-0.300000,-0.411676,-0.372861)
|  `- SpareMagazineGeometry               [132 triangles]
|- ChargingHandle        (-0.031344, 0.164003,-0.728878)
|  |- BoltGeometry                         [85 triangles]
|  |- ChargingHandleGeometry              [852 triangles]
|  `- ChargingHandleSocket
|- PrimaryGripSocket     (-0.008427,-0.162496,-0.155166)
|- SupportGripSocket     (-0.009330, 0.055785,-0.633668)
|- MagazineWellSocket     (0.000000, 0.008324,-0.502861)
|- OpticRailSocket        (0.000000, 0.197845,-0.250000)
`- MuzzleSocket          (-0.008428, 0.100633,-1.260000)
```

All coordinates are root-local Godot metres: X lateral, Y up, positive Z to
the stock, and negative Z to the muzzle. The installed asset bounds are
`(-0.063999,-0.299936,-1.260000)` to
`(0.063999,0.300235,0.320000)`, exactly 1.58 m stock-to-muzzle. The staged-spare
scene bounds are `(-0.330012,-0.719936,-1.260000)` to
`(0.063999,0.300235,0.320000)`. The scene contains 8,717 triangles, including
the independent staged spare.

The grip frames are derived from the authored pistol-grip and handguard
contact volumes instead of the retired Quaternius proportions. The charging
frame is the author's original `CharginHandle` object origin, not the handle's
whole-mesh bounding-box centre. Magazine grip/well, optic rail, and muzzle
markers are derived from their corresponding visible surfaces. Final nearest
surface distances are 18.970 mm primary, 7.944 mm support, 0 mm magazine grip,
0 mm magazine well, 3.389 mm charging handle, 0 mm optic rail, and 11.282 mm
from the centre of the open bore to its visible muzzle ring.

The material refinement is scalar-only and does not claim texture-based PBR:
`Primary` is tan polymer (`metallic=0.02`, `roughness=0.48`), `Secondary` is
phosphated receiver/control metal (`0.75`, `0.27`), and `Highlight` is finished
magazine/hardware metal (`0.86`, `0.23`).
