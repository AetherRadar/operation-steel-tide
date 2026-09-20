# Lantern Canal runtime assets

The runtime city is a Blender-authored derivative of the owner's approved
v2.5 source. `scripts/blender/optimize_lantern_runtime.py` preserves named
ground, bridge and circulation meshes, dissolves redundant coplanar faces,
and simplifies dense decorative surfaces. The imported triangle count drops
from 11,461,024 to 3,900,204. The original source remains available unchanged.
Texture dimensions are capped at 1024; embedded material assignments and UVs
are preserved. Inspect storefronts, the canal, both workshop interiors and
their stairs after any rebuild.

`lantern_canal_runtime.glb` is 292,030,388 bytes instead of the 589,056,208-byte
upstream scene. `lantern_mountains.glb` contains only the twelve authored
Jianghai mountain meshes and is 706,708 bytes. Loading the mountain ring no
longer instantiates and retains another entire city behind invisible nodes.
Both derivatives have editable `.blend` sources in
`source_art/lantern_canal_world/`. The large city source and export use Git LFS.

`LanternCanalPreloadCache` starts background resource loading when the canal
is selected. Deployment waits asynchronously before reloading the world.
Direct launches use the same preload before initializing mission callbacks,
then build the scene and collision. `STEEL_TIDE_RUNTIME_READY` is emitted
only after world initialization completes. The loaded resource cache is
released when the canal world exits.

Eight unarmed residents reuse the approved Heron and Magpie assets, with
bounded roaming on dry quays and the existing distant simulation cadence.
Six original garrison patrols remain. Garrison presentation uses its own
painted uniform and helmet, independently of the roster's Viper asset.

Validation commands (Godot 4.6.3 Mono console executable):

```powershell
dotnet build OperationSteelTide.csproj
& '<Godot>' --headless --path . -- --validate-lantern-canal
& '<Godot>' --path . --resolution 1280x720 -- --validate-lantern-performance
& '<Godot>' --path . --resolution 1280x720 -- --capture-lantern-canal
```

The map diagnostic independently checks 16 authored floor samples and five
settled player positions, workshop content, mountain visibility, lighting,
objectives, and loot. The performance diagnostic runs live residents and
guards, verifies their survival and floor height, and records median/p95/max
frame time, resource wait, build time, draw calls, and video memory. These
measurements depend on hardware, viewport, and concurrent processes; they
are not a guaranteed frame rate on every machine.

Verified on 2026-09-20 with Godot 4.6.3 Mono, RTX 4060 Laptop GPU and a
1280x720 viewport: background resource wait 2,180 ms (362 yielded frames),
scene/collision build 4,194 ms, median frame 10.98 ms, p95 13.74 ms and maximum
15.80 ms across 180 measured frames after 30 warm-up frames. Video memory was
2,114.7 MiB; all eight residents and six garrison patrols remained alive and
grounded. This is a short stationary sample, not a full-map sustained benchmark.
Initial scene construction still takes several seconds; background preloading
does not make Godot scene instantiation asynchronous.
