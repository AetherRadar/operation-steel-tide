# Engineering Standards

These rules define the maintainability baseline for Operation Steel Tide. They apply to new production code and to any subsystem touched by a refactor. `MUST` rules are release gates. `SHOULD` rules require a short explanation in the commit or pull request when they are intentionally not followed.

## Compatibility contract

- Refactors MUST preserve save-file keys and formats, input actions and default bindings, mission rules, spawn and cold-start loadout behavior, network messages, public signals, and existing `--validate-*` / `--capture-*` command names unless a task explicitly changes that contract.
- A refactor MUST pass `dotnet build OperationSteelTide.csproj` with zero warnings and zero errors plus every diagnostic named by `AGENTS.md` for the affected subsystem.
- Behavior migrations MUST be incremental: move one coherent subsystem per commit and keep the project runnable after each commit. Do not combine a broad rewrite with unrelated gameplay work.
- Diagnostic-only behavior MUST remain behind an explicit diagnostic argument and MUST NOT alter normal play.

## Dependency direction

Production dependencies MUST flow in this direction:

```text
scene / composition root
  -> feature controller or runtime service
     -> gameplay rules and domain state
        -> data contracts
```

- Data contracts and pure gameplay rules MUST NOT depend on Godot `Node`, UI controls, scene paths, or a composition root.
- Feature controllers MAY depend on focused interfaces or domain types. They MUST NOT reach into another feature's private state or search the global scene tree for an implementation detail.
- UI views MUST expose user intent through signals, events, or explicit callbacks. They MUST NOT directly mutate world, player, persistence, networking, or mission state.
- `FreightTerminalWorld` is a composition root and compatibility facade during migration. New world generation, mission coordination, spawning, extraction, networking, or persistence logic MUST be introduced in a focused service instead of adding another responsibility to `FreightTerminalWorld.cs`.
- `CombatHUD` is a compatibility facade during migration. New interface surfaces MUST use a dedicated scene and controller/view type instead of adding static node construction to `CombatHUD.cs`.

## File and type boundaries

- A C# `partial` file MAY group legacy members of one aggregate while it is being migrated. It MUST NOT be treated as a module boundary: all partial files still share state and lifecycle.
- New production C# files SHOULD stay at or below 500 lines. A file above 800 lines MUST include a documented reason and a concrete extraction follow-up before merge.
- A type SHOULD have one lifecycle owner and one primary reason to change. State used only by one feature belongs with that feature, not in a shared root.
- Cross-feature access MUST use the narrowest stable API. Do not make fields public or internal only to avoid defining an explicit input, result, event, or interface.
- Pure calculations, eligibility rules, selection logic, and state transitions SHOULD be regular C# types without `Node` dependencies so they can be unit tested without starting Godot.

## Godot scenes and UI

- Static UI hierarchy, anchors, offsets, minimum sizes, colors, and theme overrides MUST live in `.tscn` or shared `.tres` resources. C# MAY create dynamic rows, world-driven markers, previews, and other data-dependent children.
- Every reusable UI scene SHOULD have a focused controller/view class that binds its own required nodes, owns presentation updates, and emits user-intent signals. The composition root is responsible only for instantiation, connection, and supplying state.
- Required scene nodes MUST use stable unique names and typed `GetNode<T>` bindings. Optional nodes MAY use `GetNodeOrNull<T>` with an explicit fallback.
- A view MUST document its inputs, output signals, and lifecycle in its public API. Showing or hiding a view MUST NOT implicitly save settings or change gameplay state.
- Every newly extracted UI scene MUST have a deterministic `--validate-*` diagnostic that verifies scene loading, required bindings, data synchronization without feedback signals, localization where applicable, and user-intent signals.

## Services and state

- World assembly, mission orchestration, spawning, extraction, networking, and persistence MUST become separate services as those areas are touched; a service MUST have explicit construction inputs and a bounded lifecycle.
- Persistent settings and progression MUST have a single owner. Views receive snapshots and emit requested changes; they do not read or write `user://` directly.
- Long-lived event subscriptions MUST be paired with a clear disconnect or with a shared Godot ownership lifetime that guarantees cleanup.
- Runtime services MUST NOT depend on diagnostic fixtures. Diagnostics may call production APIs and inspect stable diagnostic projections.

## Verification and delivery

- New rules require focused tests or deterministic diagnostics at the same time as the behavior is introduced. A screenshot alone is not a behavioral test.
- UI diagnostics MUST set their own deterministic state and report one machine-readable `*_CHECK` line, one `*_PASS valid=...` line, and exit with `0` on success or `2` on failure.
- CI MUST retain the C# build gate. The next gates to add are Go tests, pinned Godot headless diagnostics, and a packaged-export smoke test, in that order as reproducibility permits.
- Commits MUST be cohesive, use an English imperative subject, and contain every task change before delivery, as required by `AGENTS.md`.
- Worktree tasks MAY run on a detached HEAD and MUST NOT require a shared Local checkout or assume that a current branch/upstream exists. Parallel development remains isolated in task worktrees; only final `main` integration and synchronization are serialized.
- Every task MUST finish the delivery sequence defined in `AGENTS.md`: commit all task changes, update onto the latest `origin/main`, rerun required gates after any rebase, deliver with a non-force fast-forward push or an actually merged pull request, and fast-forward the clean local `main` checkout to `origin/main`.
- Final verification MUST prove that the landed commit is an ancestor of `origin/main` (the task commit for a direct push, or the platform-created commit for a squash/rebase pull request), the local `main` checkout equals `origin/main`, and both the task worktree and local `main` checkout are clean. A task MUST NOT be reported as delivered when authentication, network failure, branch protection, concurrent integration, or a dirty/diverged local checkout prevents any required step.

## Review checklist

Before merging a feature or refactor, verify:

1. The change preserves every compatibility item not explicitly changed by the task.
2. Static UI is editor-visible and dynamic UI creation is justified by data.
3. No new responsibility was added to `FreightTerminalWorld.cs` or `CombatHUD.cs`.
4. Dependencies point toward domain rules rather than back toward the scene tree.
5. Required build, diagnostics, and focused tests pass.
6. Documentation and diagnostic coverage describe the new boundary.
