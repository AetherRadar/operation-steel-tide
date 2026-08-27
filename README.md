# Operation Steel Tide

English | [简体中文](README.zh-CN.md)

**An open-source tactical extraction FPS built with Godot 4.6 and C#: command a three-operator squad, keep the gear you extract, or enter a separate 5v5 demolition match.**

![Operation Steel Tide key art in Jianghai Old City](docs/media/cover.png)

*AI-assisted project key art based on Jianghai Old City; direct captures from the current build are linked below.*

[Download for Windows](https://github.com/AetherRadar/operation-steel-tide/releases/latest) · [Download for macOS](https://github.com/AetherRadar/operation-steel-tide/releases/latest) · [View the presentation gallery](#presentation-gallery) · [Explore the squad AI](csharp/FreightTerminalWorld.Squad.cs) · [Read the architecture notes](ARCHITECTURE.md)

[![Godot 4.6](https://img.shields.io/badge/Godot-4.6-478CBF?logo=godot-engine&logoColor=white)](https://godotengine.org/)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Go 1.26](https://img.shields.io/badge/Go-1.26-00ADD8?logo=go&logoColor=white)](https://go.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-42e7c1.svg)](LICENSE)

> **Play in under a minute:** download the latest Windows x64 or macOS universal ZIP. Extract it, then run `PLAY.bat` on Windows or open `Operation Steel Tide.app` on macOS. Neither build requires Godot or .NET to be installed.

## What Makes It Different

- **A squad that carries its weight:** five operator roles, follow/hold/move orders, class abilities, combat, revives, and automatic AI takeover when a co-op player disconnects.
- **Extraction with consequences:** complete physical objectives, search buildings and bodies, survive reinforcements, and bank only the value brought out above the deployment baseline.
- **A second competitive ruleset:** browse a twelve-map pool with Tideforge Arena, Harbor Locks, and Tideglass Reactor currently playable, then enter MR12 5v5 rounds with manual purchases, side swaps, overtime, planting, defusing, and role-aware tactical AI.
- **Loadouts with real tradeoffs:** weapons, attachments, armor, packs, ammunition calibers, and ammunition grades all feed the persistent deployment economy.
- **One shared hostile simulation:** rival squads, garrison troops, civilians, a roaming three-phase Boss, hostile aircraft, vehicles, and elevated routes remain active around the mission.

> **Work in progress:** this is a playable systems-heavy prototype, not a finished commercial game. Authored characters, weapons, and city modules are in place, while several vehicles and world areas still need another art pass. Try a run, inspect the source, and [report the first thing that breaks](https://github.com/AetherRadar/operation-steel-tide/issues).

## Presentation Gallery

The cover and squad still are AI-assisted promotional art derived from this project's Jianghai environment and direct captures; they are not presented as gameplay screenshots.

![Three operators advancing through Jianghai Old City](docs/media/squad-key-art.png)

**Squad advance.** A three-operator fireteam approaches the elevated market through Jianghai's rain-soaked shophouse avenue.

For an exact look at the current build, open the deterministic [squad advance](docs/media/squad.webp), [market footbridge](docs/media/city.webp), or [temple approach](docs/media/hero.webp) captures. They are generated directly by Godot with `--capture-promotion` and intentionally remain separate from the promotional art.

> **Development disclosure:** This is an AI-assisted solo prototype. AI tools were used for portions of implementation and documentation; the repository owner remains responsible for design decisions, integration, debugging, and validation. It is not presented as a production-ready architecture reference. See [ARCHITECTURE.md](ARCHITECTURE.md), [Engineering Standards](docs/ENGINEERING_STANDARDS.md), and [Content Provenance](docs/CONTENT_PROVENANCE.md) for the current boundaries, refactor rules, and known origin of shipped content.

## Run a release build

### Windows

1. Open [the latest release](https://github.com/AetherRadar/operation-steel-tide/releases/latest).
2. Download the Windows x64 ZIP and its optional `.sha256` file.
3. Extract the complete ZIP to a writable folder and run `PLAY.bat`.

The package contains the game, required .NET runtime files, and local mission/progression service. It has no installer and does not request administrator access. Because this prototype is not code-signed, Windows SmartScreen may show an unknown-publisher warning; the source, packaging script, release notes, and checksum are all available in this repository for inspection.

### macOS

1. Open [the latest release](https://github.com/AetherRadar/operation-steel-tide/releases/latest).
2. Download the macOS universal ZIP and its optional `.sha256` file.
3. Extract the ZIP and open `Operation Steel Tide.app`.

The universal app includes both Intel and Apple Silicon code plus its required runtime files. It uses the built-in offline mission/progression path when the optional local service is not running. The current build is unsigned and not notarized, so macOS may require explicit approval before the first launch.

For internet co-op without router port forwarding, the host can run a [playit.gg](https://playit.gg/) UDP tunnel to `127.0.0.1:28960`; only the host installs the agent. Other players enter the complete public endpoint, such as `example.gl.at.ply.gg:41237`, in `JOIN GAME`. See [ONLINE_PLAY.md](ONLINE_PLAY.md) for the exact setup, current service limits, and private-network alternatives.

## Run from source

Install Godot 4.6.3 Mono and the .NET 8 SDK. Go is optional for the offline fallback, but required for the local mission and progression service.

Double-click `START_GAME.bat`. The launcher locates a compatible Godot 4.6 Mono executable through `GODOT_MONO`, PATH, or the default Downloads location, builds the C# assembly, and completes Godot's resource import before play. A fresh checkout can take longer on its first run while authored models and textures are imported. Multiple launchers may use the same checkout: shared build/import preparation runs one at a time, then each Godot game runs concurrently with its own logs. Each actual launch writes separate import and runtime logs, plus logs for any launcher-owned backend, under `logs/startup/<run-id>` and prints that directory before launching. Log retention never removes a directory whose launcher is still active, retains the 20 most recent inactive run directories, and treats top-level Godot errors as startup failures even when Godot itself exits with code 0.

The first coordinated game instance may reuse or start the matching Steel Tide service on `127.0.0.1:8787`; it stops only a service that its launcher created. Additional same-checkout game instances use the built-in offline mission flow, isolated temporary operator progression, and read-only shared settings. This prevents one parallel debug game from stopping the primary game's service or overwriting its persistent local state. A service owned by another checkout or an incompatible older launcher is also left untouched.

To select a specific Godot executable before launching:

```bat
set "GODOT_MONO=C:\Tools\Godot\Godot_v4.6.3-stable_mono_win64.exe"
START_GAME.bat
```

The backend can also be built manually from the repository root:

```bat
cd backend
go build -o ..\steel-tide-server.exe ./cmd/server
```

On macOS or Linux, build and launch the client directly with the platform's Godot Mono executable:

```bash
dotnet build OperationSteelTide.csproj
godot --headless --path . --import
godot --path .
```

## What developers can inspect

| System | Start here | What is exercised |
| --- | --- | --- |
| Squad AI and human/AI slot handoff | [`FreightTerminalWorld.Squad.cs`](csharp/FreightTerminalWorld.Squad.cs), [`SquadMate.Combat.cs`](csharp/SquadMate.Combat.cs), [`SquadNetwork.cs`](csharp/SquadNetwork.cs) | Orders, combat movement, class abilities, revive routing, disconnect replacement, and host-authoritative relays |
| Extraction state machine and authored aircraft | [`FreightTerminalWorld.Extraction.cs`](csharp/FreightTerminalWorld.Extraction.cs), [`ExtractionAircraft.cs`](csharp/ExtractionAircraft.cs), and the [Blender build script](scripts/blender/build_extraction_aircraft.py) | Objective gate, countdown reset, Blender-to-GLB visual rig, animated rotor/door pivots, boarding, squad seating, cinematic transfer, and completion |
| Persistent loadouts and loot | [`FreightTerminalWorld.Economy.cs`](csharp/FreightTerminalWorld.Economy.cs), [`OperatorProgression.cs`](csharp/OperatorProgression.cs), [`CombatHUD.LootComparison.cs`](csharp/CombatHUD.LootComparison.cs) | Atomic profile saves, deployment cost, grade-preserving transfer, equipment comparison, and extraction profit |
| Deterministic diagnostics | [`FreightTerminalWorld.RuntimeDiagnostics.cs`](csharp/FreightTerminalWorld.RuntimeDiagnostics.cs) and the [diagnostics reference](#diagnostics) | Scriptable gameplay checks and real in-engine capture modes used during development |

The rescue tilt-rotor is an editable Blender asset rather than runtime programmer art. Its checked-in `.blend` source lives under [`source_art/extraction_aircraft`](source_art/extraction_aircraft), while Godot loads the exported GLB and retains the previous procedural aircraft as a safe runtime fallback. Regenerate both asset files with Blender 5.x from the repository root:

```bash
blender --background --factory-startup --python scripts/blender/build_extraction_aircraft.py
```

<details>
<summary><strong>Open the full controls and gameplay systems reference</strong></summary>

## Controls

- `WASD` move, `Shift` sprint, `C` toggles crouch or starts a slide while sprinting, `Z` toggles prone, `Space` jumps while standing
- `Q`/mouse side button 1 leans left; `E`/mouse side button 2 leans right
- `1` draws the primary weapon, `2` draws the sidearm when one is equipped, `3` draws the tactical knife, `4` selects a frag grenade, and `5` selects the current utility item (a smoke grenade); grenade and utility slots stay hidden at zero inventory, and the lower-right slots can also be clicked while the pointer is available
- Left mouse fires or strikes with the knife, right mouse aims through the installed optic, and `R` performs a full magazine reload
- Left mouse uses a selected grenade or utility item and returns to the previous weapon when that stack is empty; `G` remains the direct frag-grenade shortcut
- Tap `F` to open or close nearby loot immediately, enter or exit a parked vehicle, or hold `F` to operate an objective terminal
- `V` switch between AUTO and SEMI, `T` toggle the weapon light
- `X` starts applying a spare armor plate and pressing `X` again cancels it; movement is allowed, while incoming damage interrupts the action
- Hold `B` to open the three-sector medical wheel, aim at bandages, a field medkit, or adrenaline, then release `B` or click to use it; taking damage interrupts treatment
- `H` activates the selected class skill
- `F1` orders AI squadmates to follow, `F2` orders them to hold their current positions, and `F3` sends them to the aimed world position
- `Esc` opens pause/settings, where the interface can switch between English and Chinese; `Enter` redeploys after a failed mission

Fire and movement input are armed only after their controls have returned to neutral after launch or refocus. This prevents the click used to launch the game, or a held movement key, from becoming an accidental shot or deployment exit.

## Squad and online co-op

Every deployment uses a three-operator squad drawn from five roles. The deployment screen starts on a random operator, which can be changed before launch; local AI then selects two distinct roles from the remaining roster. `HOST GAME` now creates a room and stays in the lobby. Other players join from the automatic LAN list or by address, then the host selects `START OPERATION`. All peers load the same seeded map while gameplay remains paused; the host sends the initial authoritative enemy, squad, mission, reinforcement, extraction, and loot state before releasing the shared world. A disconnected human slot returns to AI, and clients return to Operations if the host disappears. Sessions use ENet over UDP port `28960`, so the host may need to allow the game through the Windows firewall. Join accepts either `host` (default port `28960`) or `host:port`, including the public endpoint assigned by a UDP tunnel. See [ONLINE_PLAY.md](ONLINE_PLAY.md) for the free playit.gg setup and alternatives.

- Assault has higher base health, movement speed, reload speed, and fire rate. `H` activates Combat Overdrive for a larger temporary movement, rate-of-fire, reload, and recoil-control boost.
- Medic raises a visible trauma sprayer. Aim at an injured or downed squadmate and press `H` to heal or revive them; with no valid teammate in the spray cone, the medicine is applied to the Medic.
- Recon raises a visible pulse scanner. Press `H` to reveal nearby hostiles through cover for ten seconds.
- Scavenger carries four extra loot stacks, searches faster, and uses Fortune Finder to appraise and mark up to eight of the richest nearby searchable caches.
- Locksmith carries two extra loot stacks and searches faster. `H` activates Skeleton Key for a short burst of rapid unlocking and searching.

Every player role now has its own authored character silhouette and palette: VIPER uses the armored Soldier preset, HERON the rescue-worker preset, LYNX the Sci-Fi sensor suit, MAGPIE the equipped Adventurer, and JACKAL the lightweight Punk preset. The deployment preview switches the complete character model and carries the selected primary weapon, while AI teammates and rival operators use the same role-specific appearance in the field. Garrison defenders deliberately retain one separate fixed authored defender model.

AI squadmates follow by default, engage only after deployment protection ends and contact begins, fight nearby hostiles, and use their class abilities. Downed operators crawl slowly while waiting for help; hold `F` near a downed teammate to revive them with a progress bar. When the player is downed, the nearest living AI squadmate automatically sprints over, kneels, and channels a revive; if that rescuer falls, the next living teammate takes over. Each operator can be revived only once per life — a second down cannot be revived again. AI Medics can still spray trauma medicine, but revive uses the same once-per-life budget. AI class skills have twice the player cooldown, begin with staggered timers, and cannot be triggered twice in succession; the roster shows `H READY` or each member's remaining seconds. The hostile tilt-rotor patrols and fires on operators inside its engagement range until destroyed. Network peers relay operator transform, role, health, class actions, visible gunfire, and player hit damage through the host, which also rejects class actions sent before cooldown expires.

## Mission flow

1. Spawn inside a protected southern deployment zone. The protection remains active after the countdown reaches READY.
2. Cross the deployment line to begin infiltration. Enemies use view cones, physics line of sight, distance, suspicion accumulation, patrols, cover, and sound propagation.
3. Disable the communications relay, then download the shipping manifest. Hold `F` near each physical terminal to complete the operation.
4. Confirmed combat builds a response level using the backend mission's reinforcement threshold. If it fills, a three-operator QRF enters after a seven-second radio warning. Disabling the relay raises the threshold and reduces accumulated response pressure.
5. Completing both objectives enables the remote seawall extraction site. Follow the north service road through the rail yard and tank farm; the green beacon marks the final pad. Hostile kills improve rewards but are not required for extraction.

The deployment lobby doubles as a persistent equipment market. Players spend an 18,000-credit starting balance on one of six firearms (M4A1, AK-74N, SCAR-L, MP5A5, M24, or AXMC), an armor package, one of five ammunition tiers, and a 30/60/90/180-round ammunition pack. Ammunition cost scales independently with grade, quantity, and caliber; choosing the scavenger kit still supports a knife-only loot run. Friendly AI teammates and rival extraction operators deploy armed, while the player's purchased selection is applied only after the local profile is atomically saved. Successful extraction banks only value gained above the deployment baseline into `user://operator_profile.json`, preventing purchased gear from being credited back as profit.

The same lobby now includes a deployment-map selector. `MAP 01 // FREIGHT TERMINAL` and `MAP 02 // JIANGHAI OLD CITY` are playable extraction operations, while `MAP 03 // ORBITAL COMPLEX` remains visibly locked. Selecting a different playable map stages the squad/loadout and reloads the world, so only one 340 m x 320 m extraction map is resident at a time.

Jianghai Old City replaces the legacy Blackwater visual identity with a single DCC-authored district assembled from project-authored composition and recorded CC0 source assets. Dense shophouse streets connect Guangchang Pawnshop, Red Star Electronics, a temple compound, the lit market footbridge, two separated high-value zones, and physical objective terminals. Cached scene loading, quality-tier shadow culling, simple collision proxies, clear vehicle routes, rooftop traversal, loot, guards, and minimap landmarks keep the detailed map playable and testable; the legacy map ID and `--validate-refinery-map` command remain stable for saves and diagnostics.

The Operations Office also launches the separate demolition match from a twelve-map pool. Its briefing shows one map at a time with previous/next controls: `TIDEFORGE ARENA`, `HARBOR LOCKS`, and `TIDEGLASS REACTOR` are playable, while the other nine slots remain visibly locked until their geometry ships. Tideforge splits its sites across an open foundry and enclosed assembly hall. Harbor Locks recomposes Kenney's CC0 City Kit (Industrial) models into a lock-gate district with pump stations, control buildings, two long quayside channels, three attack routes, and hard-cover rotations. Tideglass Reactor abandons that blue industrial kit entirely: its construction tower and crane, complete brick reactor hall, civic crossroad, distinct perimeter gates, orange-gray modular halls, and street furniture combine 46 unique authored model files from seven CC0 source collections without repeating a scene. Six capsule-clear routes connect the two sites while preserving distinct approach and rotation choices. Each match is MR12 5v5: the player plus four AI teammates against five opponents, first to 13 rounds, halftime side swap with wallets reset to $800, and a win-by-two overtime that swaps sides every four rounds. A 15-second buy phase freezes combat before every round and presents sidearms, primary weapons, armor, and grenades with exact prices. The opening $800 can buy a P226 or M1911 but cannot buy a primary; confirming deducts the validated total once, while timeout accepts the current affordable selection or starts knife-only. Round wins pay $3,000, losses start at $1,900 plus a $500 loss-streak escalation, planting or defusing pays $300, and wallets cap at $9,000. Opponents purchase from the same price ladder, while extraction progression and its wallet remain isolated. When the player squad defends, enemy AI picks a carrier who walks a route, plants, and the remaining attackers hold angles; attackers defuse through cover while defenders rotate.

The playable district is approximately 340 m x 320 m. The original freight terminal remains the deployment complex, while the expanded grounds add a rail yard with parked freight cars, a maintenance hangar, an overflow container yard, a four-tank fuel farm, a quay crane, and a seawall approach to extraction. Multiple rival three-operator squads spawn on separated pads across the map and fight the player, each other, and garrison NPCs; NPCs prefer hunting those squads and fall back to looting buildings when quiet. Graded loot (common→legendary) glows by rarity inside buildings; the bottom-right backpack control shows total inventory value (guns, gear, ammo). Chinese UI mode localizes the new backpack/grade strings. The new ground and corrugated-metal PBR surfaces are CC0 assets from Poly Haven; their source links are recorded in `assets/textures/LICENSE.md`.

The TIDE HUNTER is a unique 900-health rogue Boss hostile to the player, friendly squadmates, garrison troops, and rival operators. It patrols a 14-point, 230 m x 209 m route through every major district instead of waiting in an arena, hunts targets with a custom AXMC, and escalates through long-range hunt, tidal surge, and riptide-overdrive phases. The final two phases add a clearly telegraphed radial pulse that can damage every faction. A minimap marker tracks the roaming threat while phase-change broadcasts announce escalation without a persistent screen-wide health bar; defeating it leaves a searchable legendary cache containing its AXMC, 7x optic, .338 Magnum ammunition, heavy armor, unique Tide Hunter knife finish, and high-value transponder.

All eleven former skyline blocks are now part of the playable residential ring. Each 6- to 13-story tower has a tall standing-height street entrance, lit corridors with carpet runners, and seven rotating room archetypes: family apartments, clinics, evacuation shelters, maintenance flats, security posts, concealed smuggler units, and community kitchens. Forty-four collision-backed corner annexes, wall condensers, rain tanks, utility cables, lane markings, and cover crates close the empty gaps around the towers. Thirty-three searchable residential caches distribute matching medical, evacuation, workshop, security, contraband, pantry, and family supplies across every tower, and every cache now carries at least one usable medicine. Split floor slabs contain an enclosed switchback stair core with two clear flights, a 4.96 m by 1.8 m mid-level turn platform, a recessed center spine, shaft walls, and a doorway into each corridor, plus continuous handrails, balusters, safety strips, utility lockers, floor markings, and a walkable rooftop exit. Each tower's first floor-2 skyway door also has a galvanized exterior fire escape with 36 discrete collision-backed treads, a ground landing, a bridge-height turn platform, guard rails, and stringers, so the glass platform can be reached from the street without entering the building first. Twenty-two 3.5-meter-wide aerospace skyways connect every tower into a closed common-floor-2 ring plus a second set of elevated routes. Their transparent side and roof glazing, waist-high protective sills, cyan light strips, and exposed structural ribs preserve long rifle sightlines while keeping every span physically walkable. Six stationary M24 garrison marksmen occupy selected long spans, creating counter-sniper threats while friendly and rival AI squads retain their armed deployment kits. Thirty-nine non-combatants occupy ground and upper floors: residents, evacuees, medical volunteers, community guards, and utility workers wander within their rooms, shelter when hostiles approach, and each offer one contextual assist such as healing, recon, vehicle repair, or supplies. Parked service trucks and court vehicles around the ring can be entered with `F`, driven with WASD, and used to ram hostiles; trucks climb low curbs and props automatically and show a "reverse to break free" warning when fully blocked. The distant tilt-rotor can be shot down, but its armored bombs cannot be intercepted; they descend at 20 m/s and must be evaded before impact.

The medical wheel uses real backpack stacks rather than unlimited abilities. Bandages provide a fast partial heal, field medkits restore heavy damage over a longer treatment, and adrenaline supplies a small heal, refills stamina, briefly improves movement and stamina recovery, and trims the class-skill cooldown. Incoming attacks now produce a directional center-screen marker, exact health damage, body-region and source readouts, an armor/flesh color response, a short camera impulse, and a distinct impact sound so health loss is immediately attributable. Player and friendly-AI takedowns add a top-right knockdown entry with the defeated operator callsign. A live top-left tactical minimap tracks player position and heading while marking deployment, extraction, mission terminals, the warehouse, radar spire, residential ring, and command hub.

M4A1, AK-74N, SCAR-L, M24, MP5A5, and AXMC receivers accept separate optics, barrels, muzzle devices, foregrips, stocks, and magazines. The M24 remains a five-round, forced-semi 7.62 precision rifle with an 8x optic. The new AXMC is a five-round .338 Magnum long-range rifle with 148 base damage, a 700-meter effective range, an independent 40-round reserve, and a dedicated 7x optic that narrows ADS to 19 degrees. The MP5A5 uses a separate 9 mm reserve and trades range for a 0.067-second automatic fire interval. Rifle, sniper, .338 Magnum, and SMG ammunition are tracked by caliber and by five separate tiers; reloads consume the selected stack, the HUD identifies the loaded tier, and upper tiers increase damage and armor penetration. Each fitted part changes damage, effective range, recoil, handling, fire interval, capacity, or sound radius. Micro reflex, holographic, 4x combat, 7x long-range, and 8x precision optics have independent visible models. The 2.45-second reload removes the depleted magazine, retrieves and inserts a fresh one, then cycles the charging handle.

The deployment and backpack paper-doll previews use BAMEN's CC BY 4.0 rigged military soldier, normalized through the checked-in Blender pipeline and rendered with a restrained tactical material pass. The first-person rig still uses custom-mesh tactical gloves with shaped palms, articulated fingers, thumbs, cuffs, and tapered sleeves. Pressing `3` draws a forward-grip tactical knife; its attack winds up on the right, rises across the reticle toward the left, and uses a matching diagonal close-range damage trace. Four loot-compatible knife finishes are available: Carbon Black, Crimson Circuit, Arctic Glass, and Hazard Stripe; equipping one returns the previous finish to its source or backpack.

Nine loot locations have distinct inventories: the warehouse armory, customs office, maintenance room, security checkpoint, fuel depot, barracks, rail dispatch office, maintenance hangar, and seawall shelter. Tap `F` to open or close a physical case or fallen operator immediately. The two-column field inventory supports drag-and-drop transfer and replacement for weapons, fitted parts, knife finishes, helmets, body armor, backpacks, caliber-specific ammunition, and plates. `Tab` opens a full-height personal item grid beside the current loadout. Static 3D previews render assembled rifles, the selected tactical knife finish, helmets, body armor, and backpacks with material and lighting while suspending their sub-viewports after warmup. Weapon detail shows every fitted slot, component effect, and final statistic. Empty cases and searched bodies can be reopened, replaced gear returns to its source, and backpack capacity changes with the equipped pack.

The freight terminal now has a realtime domain-warped sky with layered moving clouds, a visible sun halo, soft industrial smoke, a moving distant tilt-rotor, and a playable residential skyline. A cantilevered command hub and a 24-meter radar spire provide recognizable landmarks. Open lanes contain staggered Jersey barrier lines, HESCO walls, military crate stacks, pipe bundles, and service trucks, with 97 matching AI cover points distributed across the combat space.

Standing, crouched, and prone stances use different movement speed, camera and collision height, weapon stability, and footstep motion. Leaning and aiming remain available while crouched or prone. Hits are resolved as head, torso, or limb impacts: helmets protect the head, body armor protects the torso, protection falls with durability, and armor plates repair the currently equipped vest. Enemy equipment keeps its remaining durability when recovered from a body.

Enemy operators use layered anatomical meshes with independent leg motion, helmet, goggles, headset, microphone, plate carrier, magazine pouches, radio, backpack, knee protection, gloves, boots, and a complete rifle silhouette. Their materials vary slightly per operator so a patrol does not read as a row of identical targets.

The Go backend provides three mission definitions, objective text, detection rules, reinforcement thresholds, profiles, session persistence, XP, credits, and completion rewards. The C# `BackendClient` uses the service when available and falls back to a local mission when it is offline.

Jumping while moving into collision-backed furniture or cover 0.3-1.1 meters high now vaults the player onto a clear top surface, including the yellow residential search furniture used to reach elevated glass access routes.

Any accepted incoming hit immediately closes the active search or backpack view through the normal close path, restores movement, and recaptures the mouse. Weapon cards show directional damage, range, recoil, and handling comparisons; helmets, body armor, and backpacks show protection, durability, or capacity changes. Green and red comparison text communicates benefit at a glance, while arrows preserve the raw stat direction. Item borders and equipped-slot captions use the grade stored on each actual item, and weapon, attachment, knife, and equipment replacements preserve that grade when the old item returns to its source.

</details>

<details>
<summary><strong>Open the technical layout and diagnostics reference</strong></summary>

## Technical layout

- `csharp/ClientBootstrap.cs`: C# client entry point.
- `csharp/FreightTerminalWorld.cs`: mission runtime, combat effects, interactions, settings, and validation.
- `csharp/FreightTerminalWorld.Level.cs`: procedural industrial level, PBR materials, lighting, props, and extraction zone.
- `csharp/FreightTerminalWorld.Expansion.cs`: large-harbor districts, rail yard, tank farm, seawall, extraction beacon, and expanded cover/light dressing.
- `csharp/FreightTerminalWorld.Residential.cs`: eleven enterable apartment towers, physical stairwells, rooftops, courtyards, occupants, and residential diagnostics.
- `csharp/FreightTerminalWorld.Residential.Access.cs`: exterior fire escapes to the floor-2 glass skyways, access collision, rails, and deterministic access diagnostics.
- `csharp/FreightTerminalWorld.Boss.cs`, `EnemyOperator.Boss.cs`, and `CombatHUD.Boss.cs`: roaming TIDE HUNTER behavior, phases, pulse attack, rewards, minimap tracking, and Boss diagnostics.
- `csharp/DemolitionArenaLayout*.cs`, `DemolitionArenaBuilder*.cs`, and `DemolitionArenaRuntime.cs`: Tideforge, Harbor Locks, and Tideglass Reactor geometry data, authored multi-pack scene composition, invisible gameplay collision, activation isolation, route balance, long rotations, and minimap markers.
- `csharp/DemolitionMatchState.cs`, `DemolitionEconomy.cs`, `DemolitionBuyCatalog.cs`, and `DemolitionStrategyPlanner.cs`: pure MR12 scoring with halftime swaps and win-by-two overtime, the $800 round economy, purchase eligibility and pricing, and role-, health-, range-, survival-, and position-aware team assignments.
- `csharp/DemolitionMapCatalog.cs`: the twelve-map demolition pool with per-map availability for the briefing carousel.
- `csharp/FreightTerminalWorld.Demolition.Strategy.cs`: runtime snapshots, squad/defender plan application, retake routing, and physical plant/defuse movement kept outside the round controller.
- `ui/DemolitionBriefingView.tscn` and `csharp/DemolitionBriefingView.cs`: scene-authored demolition briefing, localized arena intelligence, role selection, a previous/next map carousel, and intent signals.
- `ui/DemolitionBuyView.tscn` and `csharp/DemolitionBuyView.cs`: scene-authored round purchase panel with localized offers, affordability state, countdown, running total, remaining funds, and purchase intent signals.
- `csharp/FreightTerminalWorld.Squad.cs`: squad slots, AI fill, orders, class effects, co-op combat relay, and squad diagnostics.
- `csharp/FreightTerminalWorld.Tactical.cs`, `TacticalMinimap.cs`, and `AmmoTierSystem.cs`: minimap landmarks, knockdown feedback, graded ammunition, and tactical HUD diagnostics.
- `csharp/FreightTerminalWorld.Economy.cs`, `OperatorProgression.cs`, `DeploymentMaps.cs`, and `CombatHUD.Deployment.cs`: atomic local profile persistence, deployment purchases, map selection, extraction banking, and market diagnostics.
- `csharp/SquadNetwork.cs`, `SquadMate.cs`, and `SquadSystem.cs`: ENet session relay, friendly operator AI/models, and shared role definitions.
- `csharp/CivilianNpc.cs`: wandering and sheltering residents, evacuees, volunteers, guards, and utility workers.
- `csharp/TacticalPlayer.cs`, `EnemyOperator.cs`, and `CombatHUD.cs`: first-person combat, tactical AI, and interface.
- `csharp/MissionDirector.cs`: deployment, infiltration, contact, combat, objectives, extraction, and result state machine.
- `csharp/BackendClient.cs`: HTTP session and result persistence.
- `backend/`: Go HTTP service and JSON persistence.

## Diagnostics

```text
Godot_console.exe --path . -- --capture-deployment
Godot_console.exe --path . -- --validate-pause-ui
Godot_console.exe --path . -- --validate-demolition
Godot_console.exe --path . -- --validate-demolition-rules
Godot_console.exe --path . -- --validate-demolition-arena
Godot_console.exe --path . -- --validate-harbor-locks
Godot_console.exe --path . -- --capture-harbor-locks
Godot_console.exe --path . -- --validate-tideglass-reactor
Godot_console.exe --path . -- --capture-tideglass-reactor
Godot_console.exe --path . -- --validate-demolition-briefing
Godot_console.exe --path . -- --validate-demolition-buy
Godot_console.exe --path . -- --validate-objectives
Godot_console.exe --path . -- --validate-reinforcements
Godot_console.exe --path . -- --capture-ads
Godot_console.exe --path . -- --validate-equipment
Godot_console.exe --path . -- --validate-pickup
Godot_console.exe --path . -- --capture-reload
Godot_console.exe --path . -- --capture-operator
Godot_console.exe --path . -- --capture-zh
Godot_console.exe --path . -- --capture-knife
Godot_console.exe --path . -- --validate-loot
Godot_console.exe --path . -- --validate-backpack-tab
Godot_console.exe --path . -- --validate-corpse-loot
Godot_console.exe --path . -- --capture-backpack
Godot_console.exe --path . -- --capture-optics
Godot_console.exe --path . -- --validate-ads-alignment
Godot_console.exe --path . -- --validate-stance-armor
Godot_console.exe --path . -- --capture-expanded-map
Godot_console.exe --path . -- --capture-extraction
Godot_console.exe --path . -- --validate-extraction-sequence
Godot_console.exe --path . -- --validate-large-map
Godot_console.exe --path . -- --validate-weapon-ui
Godot_console.exe --path . -- --validate-quick-slots
Godot_console.exe --path . -- --validate-arsenal
Godot_console.exe --path . -- --validate-combat-models
Godot_console.exe --path . -- --validate-operator-animations
Godot_console.exe --path . -- --validate-operator-roster
Godot_console.exe --path . -- --validate-boss
Godot_console.exe --path . -- --capture-boss
Godot_console.exe --path . -- --validate-squad
Godot_console.exe --path . -- --validate-extraction-loadout
Godot_console.exe --path . -- --validate-tactical-hud
Godot_console.exe --path . -- --validate-progression
Godot_console.exe --path . -- --validate-deployment-ui
Godot_console.exe --path . -- --validate-refinery-map
Godot_console.exe --path . -- --capture-refinery-map
Godot_console.exe --resolution 1600x900 --path . -- --capture-promotion
Godot_console.exe --path . -- --validate-industrial-interiors
Godot_console.exe --path . -- --capture-industrial-interiors
Godot_console.exe --path . -- --validate-residential
Godot_console.exe --path . -- --validate-performance
Godot_console.exe --path . -- --validate-residential-gameplay
Godot_console.exe --path . -- --validate-residential-localization
Godot_console.exe --path . -- --validate-residential-cover
Godot_console.exe --path . -- --validate-residential-density
Godot_console.exe --path . -- --validate-residential-street-art
Godot_console.exe --path . -- --validate-medical
Godot_console.exe --path . -- --validate-hit-feedback
Godot_console.exe --path . -- --capture-residential
Godot_console.exe --path . -- --capture-residential-gameplay
Godot_console.exe --path . -- --capture-residential-stairs
Godot_console.exe --path . -- --capture-residential-street-art
Godot_console.exe --path . -- --capture-medical-wheel
Godot_console.exe --path . -- --capture-hit-feedback
Godot_console.exe --path . -- --capture-tactical-hud
Godot_console.exe --path . -- --capture-skylinks
Godot_console.exe --path . -- --validate-skylinks
Godot_console.exe --path . -- --capture-skybridge-access
Godot_console.exe --path . -- --validate-skybridge-access
Godot_console.exe --path . -- --validate-vehicle-drive
Godot_console.exe --path . -- --capture-squad-lobby
Godot_console.exe --path . -- --capture-operator-roster
Godot_console.exe --path . -- --capture-squad
Godot_console.exe --path . -- --validate-network-endpoint
Godot_console.exe --headless --path . -- --validate-network-host
Godot_console.exe --headless --path . -- --validate-network-client
Godot_console.exe --headless --path . -- --validate-extraction-network-host
Godot_console.exe --headless --path . -- --validate-extraction-network-client
```

`--validate-deployment-ui` verifies the full operator preview, six market entries, four quick-kit presets, four ammunition quantities, independent grade/quantity pricing, the three-slot map selector, locked-map rejection, kit cost, and projected post-deployment balance.

`--validate-operator-roster` verifies five unique non-garrison player visual IDs, all five authored GLBs, 25-action animation contracts, movement-time rifle fit, role-aware armed previews, random player/AI/rival selection, fixed garrison identity, and the existing Scavenger and Locksmith loot benefits. `--capture-operator-roster` saves a five-column player-camera preview for visual comparison.

`--validate-refinery-map` boots Jianghai Old City through the legacy map ID and verifies its authored model placements, CC0 source coverage, scene caching, quality tiers, box-only collision proxies, distinct districts, separated high-value loot zones, clear vehicle routes, rooftop squad traversal, loot/garrison/minimap integration, and strict rendering budgets. `--capture-refinery-map` retains the compatibility command and legacy output names while saving an overhead composition frame, street-level approaches, both high-value compounds, and the rooftop route. `--capture-promotion` uses fixed staging and camera positions to reproduce the 1600 x 900 HUD-free hero, squad-advance, and market-footbridge images under `docs/media`, plus the 1280 x 640 social preview.

`--validate-industrial-interiors` verifies all 23 Blender-edited freight buildings, 63 new hinged or overhead doors, open/closed ballistic clearance, 276 enclosed-room wall rays, floor and roof collision, AI door traversal, and the seeded 8-cache/3-guard/12-empty room distribution. `--capture-industrial-interiors` saves closed/open mixed-door facades plus representative authored cache and resting-guard rooms.

`--validate-pause-ui` verifies the authored pause scene, required control bindings, signal-free settings synchronization, English and Chinese labels, pause visibility and mouse release, and the existing resume event path.

`--validate-demolition` verifies the Operations Office entry, role and map selection, the frozen buy phase before live combat, exact opening-pistol spending, sidearm-only firing, empty protection/utility state, isolated economy and extraction systems, the 5v5 squad fill, opponent purchases, opening and post-plant AI duties, planting, physical AI defusing, the tactical AI layer (combat-first arbitration with hysteresis, smoke-aware target loss, safe-frontier retries, carrier/defuser route recovery, clock-pressure site switching, and squad post anchoring), round rewards, round scoring/reset, the round-13 halftime side swap with enemy-carrier planting and player defusing, MR12/13-win/overtime win-by-two rules, and the economy reward table. `--validate-demolition-rules` verifies the demolition-only HUD, recon boundary, utility binding, elimination collision state, and localized spectator flow. `--validate-demolition-arena` checks Tideforge activation, 108 m spawn separation, roughly 80-90 m A/B approaches, the roughly 113 m rotation, balanced travel, capsule-clear routes, blocked spawn sightlines, 77 m site separation, site placement, localized minimap markers, and spatial isolation. `--validate-harbor-locks` checks the second selectable map, all imported CC0 industrial-model instances, localization, collision lifecycle, and deterministic routes to both sites; `--capture-harbor-locks` renders its tactical overview. `--validate-tideglass-reactor` checks the third selectable map, 26 unique dressing scenes plus 20 unique collision-backed authored props, all 46 distinct scenes from seven CC0 source packs, opaque solid materials with explicit glass/window exceptions, tightly model-aligned collision, scale-baked authored ground and landmark trimeshes, closed modular-building structure, visible perimeter-gate alignment, exact authored collision and player traversal for both elevated-walkway stairs, physical spawn sightline blocking, clear cover and strategy targets, and all six routes against both layout and runtime physics; `--capture-tideglass-reactor` renders one overview and eighteen player-height views, including focused captures of the four replacement civic buildings, the rebuilt elevated walkway, four closed modular halls from complementary angles, the arch gateway, and both perimeter gates. `--validate-demolition-briefing` verifies scene loading, required bindings, English and Chinese synchronization, three playable maps in the twelve-entry carousel, locked-map rejection, role/map selection without deployment, and back/deploy intent signals. `--validate-demolition-buy` verifies the pure price rules, opening-primary lock, P226 and smoke-grenade costs, unaffordable-selection blocking, authored scene bindings, English and Chinese synchronization, live HUD state, and purchase intent payload.

`--validate-extraction-sequence` verifies the authored GLB visual rig, locked objective gate, 12-second hold, leave-zone reset, aircraft arrival, boarding state, and mission completion. `--capture-extraction` renders the live countdown and landed rescue tilt-rotor at the seawall pad.

`--capture-deployment` waits 14 real seconds at spawn and prints health, armor, ammo, and phase. `--capture-ads` captures the centered reflex sight. `--capture-reload` freezes the seven-stage reload while the fresh magazine is moving into the magwell. `--capture-operator` isolates the detailed enemy model. `--capture-zh` checks the Chinese HUD and settings menu. `--capture-backpack` captures the full personal item grid, 3D gear previews, caliber ammunition, knife finishes, and weapon detail modal, while `--capture-optics` captures all optic models, including the AXMC 7x sight. `--validate-weapon-ui` verifies both cycle directions and detail opening. `--validate-quick-slots` verifies the authored five-slot bar, localized presentation, input bindings, grenade consumption, smoke deployment, and empty-slot fallback. `--validate-combat-models` verifies the authored rifle and operator GLBs plus their player, squad, enemy, and Boss integrations. `--validate-operator-animations` verifies all 14 field-operator actions, the attachment sockets, and the deterministic stance/death transition sequence. `--validate-ads-alignment` checks the rifle sight axis across stances and reload state. `--validate-arsenal` verifies the M24, MP5A5, and AXMC catalogs, independent caliber reserves, forced-semi sniper behavior, 7x ADS FOV, knife-finish replacement, world drops, and Boss weapon rewards. `--validate-boss` verifies all-faction hostility, the map-spanning patrol, three phase transitions, pulse damage, minimap tracking without a persistent top HUD, searchable death state, and the complete legendary reward set; `--capture-boss` renders its final-phase model and verifies the unobstructed HUD. `--validate-loot` verifies source-card single-click transfer, empty-primary auto-equip, backpack action menus, drag-to-ground discard, `F` closing, immediate movement restoration, held-key gating, empty-source reopening, and weapon replacement. `--validate-corpse-loot` checks repeated body searches. `--validate-stance-armor` checks crouched ADS leaning, prone height, hit regions, and equipment durability. `--capture-expanded-map` captures the complete 340 m x 320 m district and prints dimensions, enemy count, nine loot sources, extraction distance, sky state, cover-point count, residential towers, and civilians. `--capture-extraction` captures the unlocked seawall beacon and pad. `--validate-large-map` checks all six industrial districts, the remote extraction distance, marker unlock, and actual Area3D mission completion. `--validate-objectives` drives both terminals and verifies that C# enters `EXTRACTION` only after both operations complete. `--validate-reinforcements` forces confirmed combat and verifies the delayed QRF wave. `--validate-equipment` checks plating, fire mode, and weapon light state changes.

`--validate-residential` checks all eleven towers, 96 floors, 192 stair flights, 96 detailed stair landings, 44 corner annexes, open entrances, actual player ascent, rooftop access, upper-floor occupants, and all five civilian roles. `--validate-residential-density` separately verifies unique collision-backed annexes, stair utility fixtures, and an unobstructed standing-height entrance. `--validate-residential-street-art` asserts all 39 authored street placements, non-primitive model geometry, real-world scale, bounded gameplay collision, and the dedicated asphalt road material; `--capture-residential-street-art` saves a player-height close visual check of the authored lamps, carts, bins, supply props, shadows, and asphalt. `--validate-residential-gameplay` verifies one neutral unopened chest in each of 384 rooms, first-open-only resolution, all five loot grades, all five encounter outcomes, reachable placement, seven room archetypes, and AI weapon-source intent. `--validate-residential-localization` verifies every residential and room-encounter string in English and Chinese. `--validate-residential-cover` samples solid facades across all 96 residential floors, reproduces a close-range muzzle clipping through a thin wall, and verifies both the clamped shot origin and authoritative damage gate while preserving open fire. `--validate-medical` exercises wheel selection, timed healing, adrenaline, item consumption and stacking, cache distribution, and the `B` binding. `--validate-hit-feedback` verifies actual post-armor damage, attack direction, body region, source, camera impulse, and treatment interruption. `--validate-stairs` walks the player from the ground floor through the deep mid-level turn and into the second-floor corridor, requiring all four climb waypoints. `--capture-residential-stairs`, `--capture-medical-wheel`, `--capture-hit-feedback`, and `--capture-tactical-hud` provide focused visual checks. `--capture-residential` renders the exterior community, lobby and stairs, an occupied apartment doorway, and a rooftop; `--capture-residential-gameplay` separately captures the clinic, evacuation shelter, and security-post interiors; `--capture-skylinks` renders an interior sniper lane and an exterior stacked-span view; `--capture-skybridge-access` renders a complete exterior fire escape, landing, and connected bridge. `--validate-skylinks` walks the player from one tower corridor, across a skyway, and into the neighboring tower while also verifying all eleven towers share the floor-2 ring, all 22 spans have glazing and ribs, every span keeps a clear sniper lane, and six M24 sentries remain armed. `--validate-skybridge-access` checks all eleven exterior access routes, 396 discrete tread collisions, both landings per route, bridge-height clearance, and an actual player walk from the ground to the glass skyway on every tower. `--validate-squad` checks role fill, class effects, orders, combat AI, teammate-to-teammate and leader rescue priority under enemy contact, the rescuer health gate, one-revive-per-life rules, immediate first-down spectating, downed input lock, camera tracking, and player-view restoration after revival. `--validate-vehicle-drive` boards the service truck, verifies W/S throttle over 60 m of open lane, climbs a synthetic curb through the step-up assist, and checks isolated reverse. The paired `--validate-network-host` and `--validate-network-client` diagnostics run a real host/client pair and verify remote slot replacement, shot relay, and class-ability relay. The paired extraction diagnostics additionally verify lobby waiting, host-only start, shared seed, persistent transport across scene reload, paused loading, reliable bootstrap assembly, and authoritative enemy, squad, objective, and loot mutations. `--validate-extraction-loadout` verifies the player stays knife-only until a shop kit or world weapon is equipped, while friendly and rival AI operators deploy armed, then exercises the production loot-equip path for all three actor types. `--validate-tactical-hud` verifies minimap projection, ammunition tier scaling, and callsign knockdown feedback. `--validate-progression` verifies purchase deduction, atomic persistence, player loadout application, armed AI baselines, extraction credit, and insufficient-funds rejection.

`--validate-performance` enforces the map optimization budgets: fewer than 40,000 runtime nodes, fewer than 7,500 static bodies, shared box-mesh resources, distance-culling for interior detail, quality-scaled 3D rendering and sky updates, batched stair visuals, preserved per-step collision shapes, and no more than two stair-related static bodies per floor.

</details>

## License

The original source code and project-authored assets are available under the [MIT License](LICENSE). Third-party Poly Haven models and textures are CC0; their attribution and source links are recorded in [`assets/models/LICENSE.md`](assets/models/LICENSE.md) and [`assets/textures/LICENSE.md`](assets/textures/LICENSE.md). The project's AI-assistance disclosure and content-origin inventory are recorded in [`docs/CONTENT_PROVENANCE.md`](docs/CONTENT_PROVENANCE.md).
