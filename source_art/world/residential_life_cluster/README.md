# Residential Life Cluster

Standalone Blender authored scene for the residential survival map. The scene is built from scratch by `scripts/blender/build_residential_life_cluster.py` and exported to `assets/models/residential_life_cluster/residential_life_cluster.glb`.

Contents:

- four-floor apartment block with balconies and lobby
- Northstar Market supermarket with curtain wall, aisles and loading dock
- pharmacy, bakery and hardware storefronts
- pedestrian court with planters, benches, lamps and readable signage

The runtime instances the GLB from `FreightTerminalWorld.Residential.LifeCluster.cs`. Gameplay collision is maintained as separate invisible boxes so the authored mesh can be iterated without changing navigation contracts.
