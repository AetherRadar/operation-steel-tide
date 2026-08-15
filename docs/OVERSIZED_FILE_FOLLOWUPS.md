# Oversized File Follow-ups

The files below exceed 800 lines after the current gameplay work. They were already oversized legacy aggregates or compatibility-facade entry points. Splitting them in the same change would combine gameplay fixes with a broad structural rewrite. The next focused refactor for each file is recorded here so the temporary exception has a concrete exit path.

| File | Why it remains oversized in this change | Required extraction follow-up |
| --- | --- | --- |
| `csharp/CombatHUD.cs` | Legacy HUD compatibility facade and shared binding owner. | Move the remaining loot/backpack composition and diagnostics into focused view controllers, leaving only scene binding and delegation. |
| `csharp/EnemyOperator.cs` | Legacy enemy lifecycle, combat, loot, and movement aggregate. | Extract loot acquisition and loadout state into a regular C# controller, then move the remaining authored-visual hooks to the existing visual partial. |
| `csharp/FreightTerminalWorld.Demolition.Diagnostics.cs` | Deterministic demolition scenarios still share one fixture lifecycle. | Split round-flow, tactical-route, and economy scenarios into separate diagnostic partial files with one shared setup helper. |
| `csharp/FreightTerminalWorld.Demolition.cs` | Compatibility coordinator for the existing demolition mode. | Extract round transitions and spectator state into a bounded demolition match coordinator. |
| `csharp/FreightTerminalWorld.Residential.cs` | Procedural residential assembly and its legacy diagnostics share one partial. | Move residential gameplay diagnostics into `FreightTerminalWorld.Residential.Diagnostics.cs`, then extract room/cache assembly into a builder service. |
| `csharp/FreightTerminalWorld.Squad.cs` | Legacy squad lifecycle, rescue coordination, spectator state, and diagnostics share one partial. | Move the squad validator into `FreightTerminalWorld.Squad.Diagnostics.cs`, then extract AI rescue assignment into a pure coordinator. |
| `csharp/FreightTerminalWorld.cs` | Main compatibility facade still owns legacy interaction and diagnostic entry points. | Extract loot interaction orchestration and the remaining validators into focused partials/controllers, leaving composition and lifecycle delegation. |
| `csharp/SquadMate.cs` | Legacy squad actor aggregate still owns navigation, orders, revive state, and animation. | Extract revive locomotion/state transitions into a focused controller and keep the node as the lifecycle adapter. |
| `csharp/TacticalPlayer.cs` | Legacy player aggregate still owns health, inventory, input locks, and weapon state. | Extract downed/interaction-lock state and inventory ownership into focused controllers while preserving existing public signals and save keys. |

Each extraction must land as its own behavior-preserving change and pass the diagnostics named for that subsystem in `AGENTS.md`.
