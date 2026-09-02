# Falltide Recovery Array — Extraction Map Design

`FALLTIDE RECOVERY ARRAY` is the third extraction map. The stable runtime ID remains
`orbital_complex`, but the old locked "Orbital Complex" placeholder is replaced by an original
offshore recovery facility built across a storm-surge barrier. A deep-space return capsule has
struck the drained recovery dock. The site's telemetry archive, flood-control grid, and quarantine
vault are isolated from one another while rival teams converge on the same evidence.

The frozen spatial plan is shown in [orbital_complex_topdown.svg](orbital_complex_topdown.svg).
Coordinates and state transitions in this document are gameplay contracts. Authored art can add
detail and silhouette, but it must not close a declared route, expose a protected spawn, or turn an
invisible collision volume into an unreadable trap.

## Design lineage, not replication

The map borrows proven extraction-map principles without reusing another game's theme, names,
silhouette, point-of-interest arrangement, objective sequence, or extraction conditions:

- Delta Force's Tide Prison demonstrates that the whole round should express one map-specific
  verb, and that an extraction can be a public, contestable process rather than a passive green
  circle. Falltide's verb is **reroute the storm grid**: power changes routes, risk, lighting, enemy
  response, vault access, and extraction readiness. Reference: [Season 5 official overview](https://www.playdeltaforce.com/season05/index.html).
- Space City's evolution shows the value of an outer decision ring, a high-value inner facility,
  and several entrance costs. Its later dock, water, and bridge additions also show why a core
  cannot depend on one permanently dominant choke. Falltide therefore has west, east, low-dock,
  and earned upper approaches. Reference: [official map tool](https://www.playdeltaforce.com/act/mapswiki/?lang=en)
  and [official update notes](https://deltaforce.garena.com/en/news/all/MW4ZYF).
- Brakkesh demonstrates that two equal-value districts can support different weapon scales. The
  Breaker Yard is low, dense, and industrial; the Quarantine Archive is clean, vertical, and
  information-rich. Neither is a copy of an old city or a high-rise tower. Reference:
  [official map tool](https://www.playdeltaforce.com/act/mapswiki/).
- Hunt: Showdown's environment process informs terrain-first placement, strong silhouettes,
  plausible facility growth, and multiple readable entrances. Reference:
  [The Making of Lawson Delta](https://www.huntshowdown.com/news/the-making-of-the-lawson-delta-map).

## Identity and visual language

Falltide is not a spaceport. It is a terrestrial recovery and quarantine campus built into a
coastal storm barrier: part drydock, part telemetry station, part containment site. The contrast
between advanced recovery hardware and salt-damaged civil infrastructure gives the map its own
identity.

| Layer | Function | Material and light language |
|---|---|---|
| Barrier crown / upper catwalk | Exposed rotation, overwatch, earned bypass | Faded aerospace white, wet galvanized steel, cold cyan task lights |
| Service deck | Main looting and district combat | Oxidized red steel, dark concrete, sodium orange practical lights |
| Drained dock / maintenance cut | Fast but exposed low route | Black wet rock, salt bloom, standing water, cyan heat-shield debris |

The central `Stormglass Array` is a moving 56-metre-class telemetry dish visible from every spawn
sector without giving line of sight into those sectors. The crashed capsule in the `Impact Drydock`
is a second, lower landmark. The north tide gate and south intake causeway make north/south
orientation readable even during blackout.

## Frozen gameplay envelope

| Property | Contract |
|---|---:|
| Playable bounds | `340 × 320 m`, `x = -170..170`, `z = -220..100` |
| World centre | `(0, 0, -60)` |
| South intake landmark | `(0, 0, 82)` |
| Breaker Yard objective district | approximately `(-96, 0, -60)` |
| Quarantine Archive objective district | approximately `(96, 0, -70)` |
| Stormglass Array | approximately `(0, 0, -83)` |
| Impact Drydock / capsule | approximately `(0, -1.6, -30)` |
| Tide Gate extraction | `(0, 0.1, -202)`, 7 m activation radius |
| Rival teams | four three-operator squads on separated edge pads |
| Garrison target | 18–24 authored positions across district and core patrols |
| Meaningful route width | 4.0–8.0 m; exposed dock can widen to 14 m |
| Hard sightline break cadence | every 18–32 m outside the open drydock |
| Intended engagement bands | 8–24 m district interiors; 25–55 m deck; 55–95 m exposed crown |

## Spatial structure

### South: Intake Causeway

The south sector teaches the map in under 90 seconds. Each edge spawn has a low-value maintenance
cache or weapon case within roughly 12 metres, so the cold-start rule remains fair without granting
high-tier equipment. Full-height intake machinery divides west, centre, and east commitments before
teams can see one another. Players can follow the exposed crown road, descend into the drained dock,
or enter one of the two objective districts.

### South-west service ring: Undertow Sump

The west service ring hides the **Undertow Sump**, a manual pump station built below the
storm-barrier maintenance route.  It is a map-specific optional verb rather than a third
objective: once emergency power is online, a player can hold the pump control for several seconds
to purge the lower dock.  The action clears the coolant film and steam that obscure the Impact
Drydock, stabilizes the pressure lighting, and slightly lowers the facility response level.  It
does not open the extraction gate or skip either objective, so the reward is information and a
safer rotation in exchange for spending time in a predictable service lane.

Before the purge, the lower dock is readable as a dangerous shortcut: cyan pressure pulses and
periodic warnings make the flooded floor a deliberate choice.  After the purge, the same space
becomes a clean sightline break with a rare pump key and a ceramic core cache.  This reversible
visual state gives the map a memorable risk decision without adding a hard timer or a hidden
soft-lock.

### West: Breaker Yard

The Breaker Yard is a dense industrial maze of transformer works, pump halls, gantries, and short
service alleys. Its objective terminal stabilizes the storm-grid breakers. Local loot is mostly
uncommon and rare, with one epic maintenance cache placed deep enough to demand room clearing. The
district has two ground exits and one upper exit; no single post can watch all three.

### East: Quarantine Archive

The Quarantine Archive uses cleaner materials, taller rooms, glazed control spaces, and a folded
two-level circulation loop. Its objective authorizes release of the capsule quarantine. The archive
provides a comparable expected value to the Breaker Yard but favours vertical room clearing rather
than horizontal close quarters. A player who holds its upper control room cannot see the Breaker
Yard objective or every core entrance.

### Centre: Stormglass Array and Impact Drydock

The dish ring is a public information space, not a single mandatory bridge. Its outer service ring
connects west and east; the drained dock passes beneath it; the upper telemetry walk becomes usable
after emergency power returns. The sealed capsule vault contains the map's legendary reward but is
not required for a successful extraction. It opens only after both objectives, simultaneously
raising the response level and announcing the gamble to the map.

### North transition: Cathode Well / Coolant Cathedral

The north transition is no longer a blank connector between the reactor and the launch silo. The
**Cathode Well** is a vertical maintenance shaft where the storm barrier's coolant headers descend
through all three height bands. A recessed black mouth and four contracting pressure rings establish
the depth cue; sixteen tapered red/white service ribs and a suspended cyan coolant bundle turn the
void into a landmark visible from the reactor ring. A narrow north--south maintenance bridge crosses
the mouth, so the route has a readable commitment and a risky side peek rather than an arbitrary
dead-end. Closed-shell compressor, sawtooth-hall, loading-bay, and utility-office modules frame the
well as an actual service cathedral while preserving a four-metre central aisle.

The well's identity is **vertical maintenance under pressure**: the same coolant that keeps the
recovered capsule safe is also the facility's most fragile emergency system. In blackout the ribs
read as silhouettes around a dark core; after emergency power, the cyan bundle and ring seams make
the bridge a high-information rotation landmark without granting a free objective sightline.

### East transition: Data Ossuary / Quarantine Memory Aisle

Behind the Quarantine Archive, the east service ring folds into the **Data Ossuary**, a narrow records
vault for failed recovery missions. Twelve faceted archive spires stand in two rows with a four-metre
aisle. Each marker has a single cyan memory seam, and three pressure arches plus a suspended halo make
the detour legible from the archive approach. Inspection, shift, and window-hall modules close the
outer edge so the space reads as a sealed facility rather than a repeated office façade.

The ossuary's identity is **evidence that outlived its owners**. Its red quarantine chevrons and
alternating white/cyan spires create a deliberate visual counterpoint to the orange Breaker Yard:
players can read the district from colour and silhouette before they know which objective is first.
The aisle is an optional high-risk detour around the east loop; it should be useful for ambushes,
information, and graded valuables without becoming a mandatory central loot pile.

### North: Tide Gate

The Tide Gate is a fixed, highly readable extraction destination with three final approaches: west
service deck, east archive apron, and the central gate trench. It is intentionally unavailable in
blackout. After one objective it becomes a long, public extraction; after both objectives it becomes
faster, offsetting the additional central-vault risk. The extraction hardware is authored into the
map scene; runtime code supplies only the trigger, state, and invisible collision.

## The storm-grid state machine

All animated and gameplay state is derived from `objectiveStage` plus the shared world seed. There
is no unsynchronized local random switch, which keeps host and clients deterministic.

| Stage | Facility state | Routes | Rewards and enemies | Extraction |
|---|---|---|---|---|
| `0` | Blackout; dish coasts slowly; structural Cathode ribs/Data Ossuary silhouettes remain, powered seams are dark | Low dock and two ground district routes; upper bypass and vault sealed | Outer economy only; garrison dispersed | Tide Gate offline |
| `1` | Emergency power; sodium circuits and alarm beacons wake | Upper telemetry bypass opens; Undertow Sump can be purged | Objective district yields rare/epic cache; QRF becomes more alert; lower dock pressure is an optional risk | Tide Gate online, long public hold |
| `2` | Full reroute; dish tracks; quarantine alarm red | Vault opens; all three core routes remain available | Legendary capsule sample available; core boss/QRF pressure activated | Faster public hold |

The world seed decides which objective is first. The order is visible in the briefing and remains
fixed for the match. Completion effects depend on stage, not on an unsynchronized timer: the first
completed system always establishes emergency power, and the second completes the reroute.

## Risk and economy

Falltide uses three readable reward tiers instead of placing every valuable at the centre:

1. **Outer survival ring** — common/uncommon gear, guaranteed early weapon access, civilian and
   maintenance valuables, low garrison density.
2. **Objective districts** — rare/epic equipment, short-key-room style detours, denser patrols, and
   public objective audio/lighting.
3. **Capsule quarantine core** — a small legendary pool that opens after both objectives and never
   blocks ordinary extraction progress.

The Cathode Well and Data Ossuary extend the economy's middle band without moving the legendary
pool: the well is a noisy, exposed rotation landmark between the objective districts, while the
ossuary is a quiet information detour with short sightlines. Both spaces should reward route
knowledge and timing, not simply add another stack of crates.

The Undertow Sump is the map's third economy lever.  Its interaction is deliberately one-shot and
non-objective: squads can leave the lower dock pressurized for concealment, or spend a vulnerable
hold action to make the drydock legible before carrying high-tier loot through it.  The resulting
choice is visible to every nearby player through the pressure light and cleared coolant surface.

The intended decision is explicit: after one objective a squad may take a slower but lower-risk
extraction, rotate to the second district for progression, or hunt the public core gamble. A single
central loot pile must never make the outer map economically meaningless.

## AI, boss, and public response

- Garrison patrols stay local during blackout, then gain cross-district routes after emergency
  power. Their traversal points are authored and ordered deterministically.
- Four rival squads spawn on the perimeter with minimum separation and no direct opening sightline.
- Reinforcements enter from distant north-west, north-east, south-west, or south-east service locks;
  the closest point to the player is rejected.
- The existing authored world-boss character may be reused as a map-specific quarantine hunter,
  but its route and activation belong to Falltide. It patrols the dish ring only after full reroute,
  so it is a consequence of the optional high-value choice rather than unavoidable spawn pressure.
- Dish motion, gate travel, alarm lighting, and vault-door travel use stable authored pivots. Their
  presentation may tween, but their authoritative open/closed result comes from objective stage.
- The Undertow Sump's pump wheel, console screen, and lower-dock film are reactive presentation
  helpers.  The one-shot drain state is held by the map runtime, replicated through the host's
  objective snapshot seam where applicable, and never changes the objective order.
- The Cathode Well's ring lights and coolant seams provide a stage-readable silhouette; the Data
  Ossuary's halo and archive seams provide a contrasting quarantine signal. Their visible state may
  animate with power, but the geometry remains authored and deterministic.

## Art and provenance contract

Major visible geometry is assembled and edited in Blender from redistribution-compatible authored
assets. The runtime map loader never constructs visible buildings from `BoxMesh`, CSG, or assembled
primitive blocks. Primitive collision is permitted only when invisible and aligned with authored
surfaces.

The two hero references are NASA's authored [70 meter dish](https://science.nasa.gov/3d-resources/70-meter-dish/)
and [Orion capsule printable model](https://science.nasa.gov/3d-resources/orion-capsule/), modified,
re-scaled, re-materialed, stripped of NASA identifiers, and presented as fictional hardware. NASA is
acknowledged as the source without implying endorsement, following the
[NASA media usage guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/). Secondary
industrial buildings, stairs, and props come from existing provenance-tracked CC0 packs in the
repository. The scene README, build report, asset license file, and global provenance register must
record exact source-to-runtime mappings and acquisition dates.

## Acceptance gates

The map is complete only when deterministic diagnostics and representative Godot captures prove:

- every spawn and squad formation capsule is clear, separated, and outside opening enemy sight;
- all three power stages produce the declared route and extraction states;
- objective order is stable for the shared seed and network state can reconstruct it;
- the player can reach both objectives and extraction using authored routes;
- upper walks have collision-matched treads, landings, rails, and at least two ways off the core;
- the capsule vault is inaccessible at stages 0–1 and reachable at stage 2;
- loot counts and grades satisfy the three-tier economy without duplicate network identities;
- visible art, gameplay collision, minimap landmarks, cover points, patrols, QRF entries, and boss
  route all lie within the declared bounds;
- GLB loading, unload, and reload release references cleanly;
- player-height, overview, central landmark, objective district, and extraction captures pass visual
  review for scale, silhouette, material coherence, lighting, collision alignment, clipping, draw
  calls, and texture memory.
- the Cathode Well and Data Ossuary read as distinct destinations at player distance, with a clear
  bridge/aisle route and no accidental closure of the existing central spine or east service loop.
