# Operation Steel Tide

Operation Steel Tide is a four-operator tactical FPS prototype built with Godot 4.6 Forward+.
The complete game client is C#, including LAN co-op, AI squadmates, three operator classes, the procedural world, player controller, weapons, enemy AI, HUD, effects, objectives, and backend integration. The local progression and mission service is Go.

## Run

Install Godot 4.6.3 Mono and the .NET 8 SDK. Go is optional for the offline fallback, but required for the local mission and progression service.

Double-click `START_GAME.bat`. The launcher locates Godot Mono through `GODOT_MONO`, PATH, or the default Downloads location. When Go is available, it builds and starts the service on `127.0.0.1:8787`, waits for the process, then launches Godot. Closing the game closes the service and removes `backend/backend.pid`.

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

## Controls

- `WASD` move, `Shift` sprint, `C` toggles crouch or starts a slide while sprinting, `Z` toggles prone, `Space` jumps while standing
- `Q`/mouse side button 1 leans left; `E`/mouse side button 2 leans right
- `1` draws the primary weapon, `3` draws the tactical knife, and `2` or the mouse wheel cycles between them; the lower-right weapon slots can also be clicked while the pointer is available
- Left mouse fires or strikes with the knife, right mouse aims through the installed optic, and `R` performs a full magazine reload
- `G` throws a frag grenade; tap `F` to open or close nearby loot immediately, or hold `F` to operate an objective terminal
- `V` switch between AUTO and SEMI, `T` toggle the weapon light
- `X` apply a spare armor plate while stationary; movement, sprinting, firing, or damage interrupts it
- `H` activates the selected class skill
- `F1` orders AI squadmates to follow, `F2` orders them to hold their current positions, and `F3` sends them to the aimed world position
- `Esc` opens pause/settings, where the interface can switch between English and Chinese; `Enter` redeploys after a failed mission

Fire and movement input are armed only after their controls have returned to neutral after launch or refocus. This prevents the click used to launch the game, or a held movement key, from becoming an accidental shot or deployment exit.

## Squad and LAN co-op

Every deployment uses a four-operator squad. Choose Assault, Medic, or Recon on the deployment screen, then start with `LOCAL + AI`, host a LAN session, or enter the host address and join. Empty slots are immediately filled by AI; a connected player replaces one AI slot, and disconnecting hands that slot back to AI without restarting the mission. LAN sessions use ENet over UDP port `28960`, so the host may need to allow the game through the Windows firewall.

- Assault has higher base health, movement speed, reload speed, and fire rate. `H` activates Combat Overdrive for a larger temporary movement, rate-of-fire, reload, and recoil-control boost.
- Medic raises a visible trauma sprayer. Aim at an injured or downed squadmate and press `H` to heal or revive them; with no valid teammate in the spray cone, the medicine is applied to the Medic.
- Recon raises a visible pulse scanner. Press `H` to reveal nearby hostiles through cover for ten seconds.

AI squadmates follow by default, engage only after deployment protection ends and contact begins, fight nearby hostiles, and use their class abilities. AI Medics seek injured or downed friendlies even outside combat. AI class skills have twice the player cooldown, begin with staggered timers, and cannot be triggered twice in succession; the roster shows `H READY` or each member's remaining seconds. LAN peers relay operator transform, role, health, class actions, visible gunfire, and player hit damage through the host, which also rejects class actions sent before cooldown expires.

## Mission flow

1. Spawn inside a protected southern deployment zone. The protection remains active after the countdown reaches READY.
2. Cross the deployment line to begin infiltration. Enemies use view cones, physics line of sight, distance, suspicion accumulation, patrols, cover, and sound propagation.
3. Disable the communications relay, then download the shipping manifest. Hold `F` near each physical terminal to complete the operation.
4. Confirmed combat builds a response level using the backend mission's reinforcement threshold. If it fills, a three-operator QRF enters after a seven-second radio warning. Disabling the relay raises the threshold and reduces accumulated response pressure.
5. Completing both objectives enables the remote seawall extraction site. Follow the north service road through the rail yard and tank farm; the green beacon marks the final pad. Hostile kills improve rewards but are not required for extraction.

The playable district is approximately 340 m x 320 m. The original freight terminal remains the deployment complex, while the expanded grounds add a rail yard with parked freight cars, a maintenance hangar, an overflow container yard, a four-tank fuel farm, a quay crane, and a seawall approach to extraction. The new ground and corrugated-metal PBR surfaces are CC0 assets from Poly Haven; their source links are recorded in `assets/textures/LICENSE.md`.

All eleven former skyline blocks are now part of the playable residential ring. Each 6- to 13-story tower has an open street entrance, lit corridors, accessible apartment rooms, collision-backed split floor slabs, two physical stair ramps per story, and a walkable rooftop exit. Courtyards add sidewalks, entry canopies, planters, benches, and signed building entrances. Thirty-nine non-combatants occupy ground and upper floors: residents, evacuees, medical volunteers, community guards, and utility workers wander within their rooms and shelter when hostiles approach.

M4A1, AK-74N, and SCAR-L receivers accept separate optics, barrels, muzzle devices, foregrips, stocks, and magazines. Each fitted part changes damage, effective range, recoil, handling, fire interval, capacity, or sound radius. Micro reflex, holographic, and 4x combat optics have independent visible models. The 2.45-second reload removes the depleted magazine, retrieves and inserts a fresh one, then cycles the charging handle.

The first-person rig includes smooth custom-mesh tactical gloves with shaped palms, articulated fingers, thumbs, cuffs, and tapered sleeves. Pressing `3` draws a forward-grip tactical knife; its attack winds up on the right, rises across the reticle toward the left, and uses a matching diagonal close-range damage trace.

Nine loot locations have distinct inventories: the warehouse armory, customs office, maintenance room, security checkpoint, fuel depot, barracks, rail dispatch office, maintenance hangar, and seawall shelter. Tap `F` to open or close a physical case or fallen operator immediately. The two-column field inventory supports drag-and-drop transfer and replacement for weapons, fitted parts, helmets, body armor, backpacks, ammunition, and plates. `Tab` opens a full-height personal item grid beside the current loadout. Static 3D previews render assembled rifles, the tactical knife, helmets, body armor, and backpacks with material and lighting while suspending their sub-viewports after warmup. Weapon detail shows every fitted slot, component effect, and final statistic. Empty cases and searched bodies can be reopened, replaced gear returns to its source, and backpack capacity changes with the equipped pack.

The freight terminal now has a realtime domain-warped sky with layered moving clouds, a visible sun halo, soft industrial smoke, a moving distant tilt-rotor, and a playable residential skyline. A cantilevered command hub and a 24-meter radar spire provide recognizable landmarks. Open lanes contain staggered Jersey barrier lines, HESCO walls, military crate stacks, pipe bundles, and service trucks, with 97 matching AI cover points distributed across the combat space.

Standing, crouched, and prone stances use different movement speed, camera and collision height, weapon stability, and footstep motion. Leaning and aiming remain available while crouched or prone. Hits are resolved as head, torso, or limb impacts: helmets protect the head, body armor protects the torso, protection falls with durability, and armor plates repair the currently equipped vest. Enemy equipment keeps its remaining durability when recovered from a body.

Enemy operators use layered anatomical meshes with independent leg motion, helmet, goggles, headset, microphone, plate carrier, magazine pouches, radio, backpack, knee protection, gloves, boots, and a complete rifle silhouette. Their materials vary slightly per operator so a patrol does not read as a row of identical targets.

The Go backend provides three mission definitions, objective text, detection rules, reinforcement thresholds, profiles, session persistence, XP, credits, and completion rewards. The C# `BackendClient` uses the service when available and falls back to a local mission when it is offline.

## Technical layout

- `csharp/ClientBootstrap.cs`: C# client entry point.
- `csharp/FreightTerminalWorld.cs`: mission runtime, combat effects, interactions, settings, and validation.
- `csharp/FreightTerminalWorld.Level.cs`: procedural industrial level, PBR materials, lighting, props, and extraction zone.
- `csharp/FreightTerminalWorld.Expansion.cs`: large-harbor districts, rail yard, tank farm, seawall, extraction beacon, and expanded cover/light dressing.
- `csharp/FreightTerminalWorld.Residential.cs`: eleven enterable apartment towers, physical stairwells, rooftops, courtyards, occupants, and residential diagnostics.
- `csharp/FreightTerminalWorld.Squad.cs`: squad slots, AI fill, orders, class effects, co-op combat relay, and squad diagnostics.
- `csharp/SquadNetwork.cs`, `SquadMate.cs`, and `SquadSystem.cs`: ENet session relay, friendly operator AI/models, and shared role definitions.
- `csharp/CivilianNpc.cs`: wandering and sheltering residents, evacuees, volunteers, guards, and utility workers.
- `csharp/TacticalPlayer.cs`, `EnemyOperator.cs`, and `CombatHUD.cs`: first-person combat, tactical AI, and interface.
- `csharp/MissionDirector.cs`: deployment, infiltration, contact, combat, objectives, extraction, and result state machine.
- `csharp/BackendClient.cs`: HTTP session and result persistence.
- `backend/`: Go HTTP service and JSON persistence.

## Diagnostics

```text
Godot_console.exe --path . -- --capture-deployment
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
Godot_console.exe --path . -- --validate-corpse-loot
Godot_console.exe --path . -- --capture-backpack
Godot_console.exe --path . -- --capture-optics
Godot_console.exe --path . -- --validate-stance-armor
Godot_console.exe --path . -- --capture-expanded-map
Godot_console.exe --path . -- --capture-extraction
Godot_console.exe --path . -- --validate-large-map
Godot_console.exe --path . -- --validate-weapon-ui
Godot_console.exe --path . -- --validate-squad
Godot_console.exe --path . -- --validate-residential
Godot_console.exe --path . -- --capture-residential
Godot_console.exe --path . -- --capture-squad-lobby
Godot_console.exe --path . -- --capture-squad
Godot_console.exe --headless --path . -- --validate-network-host
Godot_console.exe --headless --path . -- --validate-network-client
```

`--capture-deployment` waits 14 real seconds at spawn and prints health, armor, ammo, and phase. `--capture-ads` captures the centered reflex sight. `--capture-reload` freezes the seven-stage reload while the fresh magazine is moving into the magwell. `--capture-operator` isolates the detailed enemy model. `--capture-zh` checks the Chinese HUD and settings menu. `--capture-backpack` validates the Chinese personal item grid, 3D gear previews, and weapon detail modal, while `--capture-optics` captures all three optic models. `--validate-weapon-ui` verifies both cycle directions and detail opening. `--validate-loot` also verifies `F` closing and immediate movement restoration, alongside held-key gating, empty-source reopening, transfer, and weapon replacement. `--validate-corpse-loot` checks repeated body searches. `--validate-stance-armor` checks crouched ADS leaning, prone height, hit regions, and equipment durability. `--capture-expanded-map` captures the complete 340 m x 320 m district and prints dimensions, enemy count, nine loot sources, extraction distance, sky state, cover-point count, residential towers, and civilians. `--capture-extraction` captures the unlocked seawall beacon and pad. `--validate-large-map` checks all six industrial districts, the remote extraction distance, marker unlock, and actual Area3D mission completion. `--validate-objectives` drives both terminals and verifies that C# enters `EXTRACTION` only after both operations complete. `--validate-reinforcements` forces confirmed combat and verifies the delayed QRF wave. `--validate-equipment` checks plating, fire mode, and weapon light state changes.

`--validate-residential` checks all eleven towers, 96 floors, 192 stair flights, open entrances, ramp collision, actual player ascent, rooftop access, upper-floor occupants, and all five civilian roles. `--capture-residential` renders the exterior community, lobby and stairs, an occupied apartment doorway, and a rooftop. `--validate-squad` checks four-member AI fill, physical default-follow movement, enforced AI cooldown, all three class effects, all three squad orders, and HUD state. The paired `--validate-network-host` and `--validate-network-client` diagnostics run a real host/client pair and verify remote slot replacement, shot relay, and class-ability relay.
