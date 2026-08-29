# Bazaar Crossing V2 — Demolition Map Design

`Bazaar Crossing V2` is a replacement layout, not a polish pass on the previous map. The first version was an open, mirrored plane with isolated props: its two sites read across the same exterior space, most routes lacked full-height thresholds, the elevated pieces behaved like freestanding scaffolding, and the visible mesh count overstated the amount of real architecture. V2 discards that topology while keeping the `136 × 112 m` outer envelope.

The frozen runtime plan is shown in [bazaar_crossing_topdown.svg](bazaar_crossing_topdown.svg). Dimensions and coordinates in this document are gameplay contracts; authored art must reinforce them without reopening a blocked sightline.

## Design lineage, not replication

V2 studies three proven demolition-map ideas without copying any map silhouette, encounter, landmark, or route sequence:

- VALORANT Ascent (`亚海悬城`) informs the use of complete city blocks to separate A and B, a legible three-lane hierarchy, and doors or thresholds that make route ownership readable. Its lesson is that an open court can still feel like a room when continuous architecture defines its edges.
- VALORANT Split informs point-specific elevation and a Mid divided into contestable halves. A high position may solve one local fight; it must never become a tower that observes both objectives.
- Counter-Strike Inferno informs folded alleys, apartments, and repeated indoor/outdoor thresholds. Long movement is broken into short tactical rooms instead of exposed distance.

These are design principles only. Bazaar Crossing retains its own south-China market identity, dimensions, circulation, asymmetry, and authored geometry.

Official design references: [The Birth of Ascent](https://playvalorant.com/en-gb/news/dev/the-birth-of-ascent/), [The Creation of Split](https://playvalorant.com/en-us/news/dev/the-creation-of-split/), and [The Art of VALORANT Map Environments](https://playvalorant.com/en-us/news/dev/the-art-of-valorant-map-environments/).

## Frozen competitive envelope

| Property | V2 contract |
|---|---:|
| Playable bounds | `136 × 112 m` (`x = -68..68`, `z = -56..56`) |
| Attack spawn | `(0, 0.22, 49)` |
| Defender spawn | `(0, 0.22, -49)` |
| Site A centre | `(-46, 0.18, -18)` |
| Site A program | `26 × 27 m` enterable two-storey merchant court |
| Site B centre | `(46, 0.18, -18)` |
| Site B program | `26 × 24 m` roofed warehouse and market hall |
| Mid control band | `x = -9..9`, three staggered masses |
| Main route clear width | `4.5–7.0 m` |
| Door clear width | `2.8–3.6 m` |
| Hard turn cadence | every `10–16 m` |
| Maximum open-space diameter | `18 m` |
| Maximum continuous combat sightline | `45 m` |

Both objectives stay at street level, but neither is an exposed pad. At least 75 percent of the gameplay sample points on each plant zone must be under a roof or inside a space bounded by continuous courtyard walls.

## Spatial structure

### South approach: three immediate commitments

Attackers leave `(0, 49)` through three separated gates rather than entering one common apron. The two full-height Entry Wings occupy `x = -15..-9` and `x = 9..15` through `z = 41.5..55`; lane-closure masses and foyer baffles lock the commitment after the first bend. West enters the merchant streets toward A, centre enters the Mid vestibule, and east enters the loading streets toward B. The west sight return at `x = -49, z = -4..12` and east return at `x = 52, z = -6..12`, plus the edge service closures, prevent either approach from reading straight into its site.

### A: two-storey merchant court

Site A is centred at `(-46, -18)` inside a `26 × 27 m` caravansary-like building. Its plant court is open to the sky but reads as a large interior room: continuous two-storey walls, arcades, shop bays, and a rear store define every side. The structure has exactly five ground thresholds: south `(-47, -4)`, west `(-60, -12)`, east/Mid `(-34, -10)`, north `(-52, -31)`, and northeast `(-37, -31)`. These thresholds are separated by hard corners; no door aligns through the courtyard to another exterior door.

`A Gallery` is integrated into the merchant building rather than standing on independent columns. Two separated stairs connect it to the court and adjacent shop route. The stairs are vertical transitions and do not count toward the five ground thresholds. The gallery influences A only: it may see the plant court and one A threshold, but not B, B's main entry, or the complete Mid route.

### B: roofed warehouse and market hall

Site B is centred at `(46, -18)` inside a `26 × 24 m` roofed warehouse/market hall. Loading bays, storage rooms, market counters, structural piers, and a rear service room turn the footprint into several close-range decisions while preserving a readable central plant area. Its exactly five ground thresholds are south `(46, -6)`, east `(60, -12)`, west/Mid `(34, -14)`, north `(40, -30)`, and northeast `(55, -30)`. Offset vestibules prevent any straight window-door-through-building shot.

`B Balcony` is part of the hall's wall and roof system. Its two stairs arrive from different floor areas so a single grenade or held angle cannot deny both. The stairs are vertical transitions and do not count toward the five ground thresholds. The balcony influences B only and cannot observe A or both B ground entries simultaneously.

### A/B separator: two full-height city blocks

Two full-height city blocks occupy the space between the objectives. Each is split north/south to leave one offset connector: the west gap lies at `z = -21..-15`, the east at `z = -18..-12`, and each contains an L-shaped sight baffle. These blocks are the primary A/B sightline barrier, not decorative cover. Their depth blocks plant-zone-to-plant-zone vision at standing, crouched, and elevated eye heights.

### Mid: a narrow S-shaped interior street

Mid stays inside `x = -9..9`. Its exact enterable shells alternate across that band: Carpet Hall `x = -9..3, z = 19..34`, Produce Hall `x = -3..9, z = 5..20`, Tea Hall `x = -9..3, z = -8..6`, and North Connector `x = -9..9, z = -24..-7`. Their offset doors and two connector baffles force a `5.5–7.0 m` S bend. Mid is a sequence of rooms and thresholds—not a south-to-north shooting tube.

`Mid Mezzanine` spans one local market room and has two stairs. It can contest that room and one adjacent bend only. It cannot see either plant zone or both site connectors.

### North retake: protected back market

The defender-side rotation is a four-part roofed back-market chain rather than an exposed line behind both objectives. Moving between A and B requires at least four full-height, approximately right-angle turns through separate rooms. Vertical baffles at `x = ±20, z = -56..-46.2` and horizontal returns at `z = -46.2` create L-shaped spawn exits before the route folds toward either site. The safe route may be longer than a contested Mid rotation, but it never presents both site doors in one view.

## Route and encounter rules

- The playable network is three lanes with earned cross-connections, not one open rectangle decorated as lanes.
- Main routes remain `4.5–7.0 m` clear; individual doors remain `2.8–3.6 m` clear.
- A wall, offset doorway, stair landing, or building corner creates a hard sightline turn every `10–16 m`.
- The only deliberate long-range fights stop at `45 m`; most fights should fall between `8–25 m`, with a smaller group between `25–40 m`.
- A and B each have exactly five tactically distinct ground thresholds. Gallery, Balcony, and Mezzanine stairs are counted separately as vertical transitions; they never inflate the door total. A threshold is a navigable transition between rooms or lanes, not a second hole cut beside an existing door.
- Loose crates, carts, and stalls support room-level cover but do not substitute for full-height route separation. At least 70 percent of meaningful occlusion comes from architecture.
- The north route, Mid, and each site contain enough depth for retreat and retake without exposing the next major room automatically.

## Vertical plan

| Upper position | Integration | Access | Allowed influence | Forbidden visibility |
|---|---|---|---|---|
| A Gallery | Merchant-court upper arcade | Two separated stairs | A court + one A threshold | B, B entries, full Mid |
| B Balcony | Warehouse wall/roof structure | Two separated stairs | B hall + one B threshold | A, both B entries together |
| Mid Mezzanine | One enclosed Mid market room | Two separated stairs | Local room + one bend | Either plant zone, both connectors |

No reachable high point may see both sites, both main site entries, or both spawn gates. Stair collision may use invisible navigation scaffolding, but the visible tread, landing, railing, supporting wall, and floor edge must all be authored, readable art. Three inner platform guardrails and both sides of all six stairs also carry dedicated world-layer collision, with deterministic transverse probes proving that a player cannot walk through the rail or fall sideways from the route.

## Interior and art program

The target playable mix is approximately `35–45%` indoor or semi-indoor space. The minimum architectural program is three genuinely enterable masses: the A merchant court, B warehouse/market hall, and enclosed Mid market/connector. The north back market adds further roofed rooms and transition depth.

Dark outer building belts terminate the playable world and prevent the old empty-rectangle silhouette. Streets are narrow gaps between finished facades; courts are rooms within building shells. The art direction remains a weathered south-China market—masonry shop houses, timber and metal doors, tiled or corrugated roofs, arcades, storage racks, loading equipment, drainage, lanterns, signs, bicycles, and restrained market clutter—assembled in Blender from provenance-tracked, redistribution-compatible authored assets.

Procedural boxes may exist only as invisible collision, navigation, occlusion, or diagnostic scaffolding. Every wall, doorway, stair, balcony, roof edge, pillar, counter, and other tactically important collision surface must have matching, legible authored art at player-camera distance.

## Measured runtime acceptance

The deterministic V2 diagnostics currently report:

| Hard gate | Limit | Measured result |
|---|---:|---:|
| A/B multi-sample visible pairs | `0` | `0 / 6,399` |
| Reachable high-point violations | `0` | `0` |
| Longest continuous sightline | `≤ 45 m` | `44.30 m` |
| Largest reachable open-space diameter | `≤ 18 m` | `17.0 m` |
| Indoor or semi-indoor playable share | `35–45%` | `35.5%` |
| Plant samples roofed or courtyard-bounded | `≥ 75%` per site | `1.000 / 1.000` (`9 × 9` per site) |
| Intentional ground doors | `10` | `10 / 10` |
| Authored route traversal checks | `7` | `7 / 7` |
| Strategy target checks | `24` | `24 / 24` |
| Worst defender route stretch | `≤ 1.40×` direct distance | `1.151×` |

The `35.5%` share is a multi-level playable-surface measurement: the exact union of ground-level roof footprints contributes `2,772 m²`, the ground playable floor plate contributes `8,354 m²`, and the three genuinely additional horizontal decks contribute `300 m²` to both numerator and denominator. Stair ramps connect those surfaces and are not double-counted as another floor plate.

The presentation gate is qualitative but mandatory: gameplay-critical collision must have `100%` matching readable art, and representative Godot captures must show coherent walls, roofs, doors, stairs, interiors, and cover at player-camera distance. Fifteen shadow-free warm practical lights illuminate the authored interior combat band at runtime. The 21-frame Godot capture set covers both sites, both interior rear rooms, the S-bend and north connector, both back-market legs, all three high grounds, all six stairs, and both spawns. Every player-distance interior frame must pass centre-80% image gates of mean luminance `≥ 0.16`, lower-quartile luminance `≥ 0.09`, and pixels below `0.075` luminance `≤ 15%`; non-overview frames additionally require an unobstructed physics ray to their projected target. These measurements and captures replace the previous weak criteria; mesh count, a centre-to-centre A/B ray, or route-length symmetry cannot by itself demonstrate a tactically dense map.
