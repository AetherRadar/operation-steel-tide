# Operation Steel Tide

Operation Steel Tide is a tactical FPS prototype built with Godot 4.6 Forward+.
The complete game client is C#, including the procedural world, player controller, weapons, enemy AI, HUD, effects, objectives, and backend integration. The local progression and mission service is Go.

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

- `WASD` move, `Shift` sprint, `C` crouch or slide, `Space` jump
- `Q`/mouse side button 1 leans left; `E`/mouse side button 2 leans right
- `1` draws the primary weapon, `3` draws the tactical knife, and `Tab` opens the personal backpack
- Left mouse fires or strikes with the knife, right mouse aims through the installed optic, and `R` performs a full magazine reload
- `G` throw a frag grenade, `F` hold to operate a nearby objective terminal
- `V` switch between AUTO and SEMI, `T` toggle the weapon light
- `X` apply a spare armor plate while stationary; movement, sprinting, firing, or damage interrupts it
- `Esc` opens pause/settings, where the interface can switch between English and Chinese; `Enter` redeploys after a failed mission

Fire and movement input are armed only after their controls have returned to neutral after launch or refocus. This prevents the click used to launch the game, or a held movement key, from becoming an accidental shot or deployment exit.

## Mission flow

1. Spawn inside a protected southern deployment zone. The protection remains active after the countdown reaches READY.
2. Cross the deployment line to begin infiltration. Enemies use view cones, physics line of sight, distance, suspicion accumulation, patrols, cover, and sound propagation.
3. Disable the communications relay, then download the shipping manifest. Hold `F` near each physical terminal to complete the operation.
4. Confirmed combat builds a response level using the backend mission's reinforcement threshold. If it fills, a three-operator QRF enters after a seven-second radio warning. Disabling the relay raises the threshold and reduces accumulated response pressure.
5. Completing both objectives enables the northern extraction zone. Hostile kills improve rewards but are not required for extraction.

M4A1, AK-74N, and SCAR-L receivers accept separate optics, barrels, muzzle devices, foregrips, stocks, and magazines. Each fitted part changes damage, effective range, recoil, handling, fire interval, capacity, or sound radius. Micro reflex, holographic, and 4x combat optics have independent visible models. The 2.45-second reload removes the depleted magazine, retrieves and inserts a fresh one, then cycles the charging handle.

The first-person rig includes smooth custom-mesh tactical gloves with shaped palms, articulated fingers, thumbs, cuffs, and tapered sleeves. Pressing `3` draws a tactical knife with its own strike animation and close-range damage trace.

Three secured loot rooms have distinct inventories and dressing: the warehouse armory favors SCAR-L and precision parts, the customs office favors M4A1 parts, and maintenance favors AK-74N recoil-control parts. Hold `F` to search a physical weapon case or a fallen operator. Corpses remain in the world and expose carried weapons, fitted parts, ammunition, and armor through a two-column inventory; taken gear moves into the 12-slot backpack, and replaced equipment returns to the searched inventory.

Enemy operators use layered anatomical meshes with independent leg motion, helmet, goggles, headset, microphone, plate carrier, magazine pouches, radio, backpack, knee protection, gloves, boots, and a complete rifle silhouette. Their materials vary slightly per operator so a patrol does not read as a row of identical targets.

The Go backend provides three mission definitions, objective text, detection rules, reinforcement thresholds, profiles, session persistence, XP, credits, and completion rewards. The C# `BackendClient` uses the service when available and falls back to a local mission when it is offline.

## Technical layout

- `csharp/ClientBootstrap.cs`: C# client entry point.
- `csharp/FreightTerminalWorld.cs`: mission runtime, combat effects, interactions, settings, and validation.
- `csharp/FreightTerminalWorld.Level.cs`: procedural industrial level, PBR materials, lighting, props, and extraction zone.
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
```

`--capture-deployment` waits 14 real seconds at spawn and prints health, armor, ammo, and phase. `--capture-ads` captures the centered reflex sight. `--capture-reload` freezes the seven-stage reload while the fresh magazine is moving into the magwell. `--capture-operator` isolates the detailed enemy model. `--capture-zh` checks the Chinese HUD and settings menu. `--capture-backpack` validates the Chinese personal inventory, while `--capture-optics` captures all three optic models. `--validate-loot` and `--validate-corpse-loot` exercise case and body searching, weapon replacement, and fitted statistics. `--validate-objectives` drives both terminals and verifies that C# enters `EXTRACTION` only after both operations complete. `--validate-reinforcements` forces confirmed combat and verifies the delayed QRF wave. `--validate-equipment` checks plating, fire mode, and weapon light state changes.
