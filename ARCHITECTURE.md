# Architecture

Operation Steel Tide is a playable Godot 4.6 Mono prototype. Its current architecture favors fast, deterministic iteration over editor-authored composition. This document describes the code as it exists today and the boundaries that future refactors should introduce without changing gameplay.

The enforceable rules for new work and incremental refactors live in [docs/ENGINEERING_STANDARDS.md](docs/ENGINEERING_STANDARDS.md). This document explains the current shape and migration direction; the standards document defines the required dependency, scene, file-size, compatibility, and verification constraints.

## Runtime composition

The startup path is intentionally small:

```text
main.tscn
  -> ClientBootstrap
     -> FreightTerminalWorld
        -> level geometry and mission modes
        -> player, squad, enemies, vehicles, and loot
        -> CombatHUD and inventory surfaces
        -> MissionDirector, networking, and backend client
```

`main.tscn` contains only the root node and `ClientBootstrap`. `ClientBootstrap` creates `FreightTerminalWorld`, which assembles most nodes at runtime. The industrial district, residential towers, links, cover, loot, and mission-specific spaces are generated in C#. Imported `PackedScene` assets are used for selected props, while collision, repeated geometry, and much of the interface are constructed programmatically.

The optional Go service owns mission definitions, sessions, progression, and result persistence. `BackendClient` uses it over local HTTP; the client retains an offline mission fallback, so the service is not required to play.

## Current boundaries

- `FreightTerminalWorld*.cs` is the composition root and shared mission runtime. Its partial files group level construction, residential content, squad behavior, extraction, economy, and diagnostic fixtures by subject.
- `TacticalPlayer*.cs`, `SquadMate.cs`, `EnemyOperator*.cs`, and the vehicle/aircraft classes own actor behavior and presentation.
- `CombatHUD*.cs`, inventory controls, and model-preview classes construct the current code-first interface.
- `MissionDirector.cs` owns the high-level deployment-to-extraction state machine. `SquadNetwork.cs` owns ENet session relay.
- `LootSystem.cs`, `WeaponSystem.cs`, `MedicalSystem.cs`, `OperatorProgression.cs`, and related data classes contain reusable gameplay rules and state.
- `backend/` is a separate Go module with HTTP and persistence concerns. It does not construct or simulate the Godot world.

C# `partial` files improve navigation but compile into the same class. They are not independent modules, do not enforce dependency direction, and do not isolate lifecycle state. In particular, `FreightTerminalWorld` currently combines composition, mode control, level generation, integration glue, and diagnostic dispatch. `CombatHUD` similarly combines several interface surfaces in one runtime-built tree. These are known prototype tradeoffs rather than intended final boundaries.

## Why code-first today

Programmatic construction supports repeatable large layouts, shared mesh/material caches, generated collision, and deterministic route checks. It also made rapid changes to combat and mission systems practical while the design was still moving.

The cost is reduced editor visibility, a larger startup composition root, tight lifecycle coupling, and more difficult isolated testing. Static authored UI and mode composition no longer benefit enough from being fully generated in code; repeated world geometry and data-driven placement still do.

## Diagnostics

Godot diagnostics are selected through `--validate-*` and `--capture-*` user arguments. They exercise production paths for traversal, squads, loot, objectives, extraction, networking, localization, and rendering, then return a process exit code. The full command catalog and assertions are documented in [README.md](README.md#diagnostics).

Diagnostic argument dispatch is centralized in a dedicated runner and delegates to fixtures that exercise the existing production APIs. Several fixtures still live in `FreightTerminalWorld` partial files, giving them direct access to runtime state but coupling test support to production composition. GitHub Actions therefore runs the reliable .NET compilation gate only; Godot executable diagnostics remain local until a pinned, reproducible headless environment is added.

## Refactor direction

Refactoring should be incremental and behavior-preserving. Each phase must keep `dotnet build` warning-free and retain the relevant deterministic Godot diagnostics.

1. Continue the diagnostic-runner extraction by moving fixture setup and capture orchestration out of the world composition root while keeping assertions against the same production APIs.
2. Move static authored UI surfaces, beginning with the operations office, loadout, backpack, and pause/settings views, into reusable `.tscn` scenes backed by a shared `Theme` resource. Keep dynamic item rows, previews, and reticles data-driven.
3. Give the operations office, extraction operation, and demolition mode explicit scene/controller boundaries with clear enter, exit, and reset lifecycles.
4. Extract world responsibilities into focused services such as level assembly, spawn direction, extraction flow, and mission runtime coordination. Keep `FreightTerminalWorld` as a thin composition root during migration.
5. Add focused unit tests for pure gameplay rules and extend CI with pinned Godot diagnostics only after the runner is reproducible on clean machines.

This is not a mandate to convert all procedural content to scenes. Authored layouts and interface structure belong in scenes/resources; generated districts, repeated geometry, dynamic entities, and deterministic validation data can remain in C#.

## Change discipline

- Follow [docs/ENGINEERING_STANDARDS.md](docs/ENGINEERING_STANDARDS.md) for mandatory boundaries and review gates.
- Preserve save data, controls, mission rules, spawn/loadout behavior, and diagnostic command names while extracting ownership.
- Prefer one subsystem migration at a time over a wholesale rewrite.
- Keep production behavior independent of diagnostics unless a validation flag is present.
- Treat the existing `--validate-*` suite as the compatibility contract for refactors, supplemented by focused tests where coverage is missing.

## Licensing

Project source is available under the root [MIT License](LICENSE). Third-party models and textures retain their own attribution and license records in `assets/models/LICENSE.md` and `assets/textures/LICENSE.md`.
