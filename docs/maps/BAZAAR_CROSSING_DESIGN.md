# Bazaar Crossing — Demolition Map Design

`Bazaar Crossing` is a 5v5 demolition map set in a dense old-city market district. It borrows the readable lane hierarchy of classic tactical shooters without copying an existing map silhouette: a committed west long route, an information-rich middle, and a bent east route feed two ground-level sites while three optional upper routes create short-lived positional advantages.

The persistent plan is shown in [bazaar_crossing_topdown.svg](bazaar_crossing_topdown.svg).

## Competitive envelope

| Property | Target |
|---|---:|
| Playable bounds | 136 × 112 m |
| Attack spawn | `(0, 0.22, 49)` |
| Defender spawn | `(0, 0.22, -49)` |
| Site A | `(-43, 0.18, -22)` |
| Site B | `(43, 0.18, -22)` |
| Main attack travel | 100–110 m per site |
| Defender setup advantage | 1–3 s to first hold position |
| Site-to-site rotation | 80–100 m through safe back routes |
| Minimum clear lane width | 2.8 m |
| Maximum stair/ramp grade | 18 degrees |

Both sites remain at street level. This avoids stacked objective volumes and keeps planting, defusing, network validation, and minimap communication unambiguous.

The frozen runtime paths measure 107.02 m to A and 106.76 m to B, a 0.24 percent difference before player movement and utility delays.

## Ground plan

### West: A Long

The west route is the deliberate long-range commitment. Two lateral kinks prevent an opening spawn-to-site sightline and divide the lane into three readable fights: market gate, fountain bend, and gallery court. Attackers can finish through the long gate or leave the lane through Mid/A Gallery; defenders can concede one segment without immediately losing the site.

### Middle: Crossroads

Middle gives information and rotation speed, not a free view into either site. A staggered pair of building corners breaks the south-to-north axis. From the crossroads, attackers can pressure either site, contest the elevated bridge, or fall back into their side routes. Defenders retain a north back-market rotation that does not require crossing an exposed site.

### East: B Banana

The east route uses an S bend instead of a straight corridor. Its three engagement pockets support utility play at different depths: south produce stalls, the drain bend, and the B market gate. B Balcony offers a second final approach, but it cannot see the attacker spawn or both sites.

### Rotation network

There are two defender-side rotations: a fast exposed crossroad route and a longer protected north back-market route. Attackers can rotate through their south market apron or earn the faster middle route by taking Crossroads. No single doorway is shared by every valid route to a site.

## Elevation plan

| Upper route | Deck height | Access | Tactical purpose | Visibility limit |
|---|---:|---|---|---|
| A Gallery | +3.0 m | South stair + east stair | Alternate A entry and retake crossfire | A court and one Mid junction only |
| Mid Bridge | +3.0 m | West stair + east stair | Short information duel and rotation connector | Crossroads only; no site or spawn view |
| B Balcony | +2.6 m | South stair + west stair | Alternate B entry and close retake | B gate and inner market only |

Each upper route has two independent exits, a 1.1 m guardrail, at least 2.8 m of clear deck width, and smooth invisible ramp collision beneath authored stair treads. The route graph preserves Y coordinates and links elevation changes only through the six authored stair paths. Upper routes are optional tactical branches; neither bomb site depends on them.

## Sightline budget

- Attack and defender spawns cannot see each other, either site, or an upper combat deck.
- Each main lane has at least two hard sightline breaks before its site.
- A sniper sightline may cover one engagement pocket, never a complete spawn-to-site lane.
- An upper deck may influence one lane junction but cannot observe both sites.
- A and B cannot see each other; rotations always pass through a deliberate threshold.
- Cover silhouettes stay readable at standing, crouched, and prone eye heights.

## Art direction

The street-level identity is an authored south-China old-city market: weathered concrete and brick shopfronts, stone paving, wet asphalt, corrugated shutters, fabric awnings, lanterns, air-conditioning units, bicycles, crates, drainage channels, cables, and restrained market signage. Major architecture, platforms, stairs, and hero props are composed in Blender from finished, provenance-tracked CC0 meshes and exported as one deterministic CC0-only GLB; no generated primitive or project-authored mesh is visible. Runtime boxes are collision and navigation scaffolding only and are never visible final art.

Material repetition is broken with vertex-color dirt masks, edge wear, puddle variation, shutter color families, facade depth changes, and decals at navigation landmarks. Route colors are semantic rather than decorative: warm A accents, cool B accents, and neutral yellow/white wayfinding at Mid.

## Acceptance checks

- Five separated spawns per side, two ground-level sites, all inside bounds.
- A/B attack routes differ by no more than 12 percent.
- Three genuinely independent attack bottlenecks and two defender rotations.
- Physical raycasts block all spawn-to-spawn and spawn-to-site sightlines.
- All six stairs are walked both directions by player, squad AI, and enemy AI.
- Every upper deck has two reachable entrances, continuous floor collision, guardrails, and capsule clearance.
- The three-dimensional route planner never merges vertically stacked nodes or accepts a floor-to-deck shortcut.
- The authored GLB contains UV-mapped PBR surfaces and no visible primitive placeholder art.
- Overview, site, lane, stair, and upper-deck captures are visually reviewed in Godot.

## Reference structure

- Dust II informed the idea of a legible long-route commitment plus a Mid split, not the geometry.
- Inferno informed the bent B approach and layered market thresholds, not the landmark placement.
- VALORANT maps informed the rule that every elevation should have a specific tactical job and a clear counter-route, rather than height added as decoration.

References: [VALORANT maps](https://playvalorant.com/en-us/maps/), [Counter-Strike level-design guidance](https://steamcommunity.com/sharedfiles/filedetails/?id=1110438811), and [World of Level Design gameplay-layout guide](https://www.worldofleveldesign.com/categories/csgo-tutorials/csgo-how-to-design-gameplay-map-layouts.php).
