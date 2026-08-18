# AGENTS.md — Operation Steel Tide

## 交付纪律（强制 / Mandatory delivery discipline）

- 每个任务的最终交付序列必须是：按功能分组完成全部 `git commit`（可以多个 commit），`git push` 当前分支到上游，再执行下述远端一致性核验；禁止把未提交或仅存在于本地的改动留给下一个任务。
- Every task MUST finish with this delivery sequence: commit all changes in logical groups, `git push` the current branch to its upstream, then perform the remote-consistency checks below. Never hand off uncommitted or local-only work to the next task.
- 提交前必须通过：`dotnet build OperationSteelTide.csproj` 0 警告 0 错误；与改动相关的 `--validate-*` 诊断全部通过（退出码 0）。
- 提交信息用英文祈使句，一个功能一个 commit，例如 `Fix mirrored residential stairs`、`Add AI teammate revive for downed player`。
- 推送后必须运行 `git fetch`，确认本地 `HEAD` 与上游分支提交一致，且 `git status --short --branch` 工作区干净、不显示 `ahead`。若认证、网络或分支保护导致推送失败，任务不得宣称已经交付完成。
- 不要主动创建新分支；直接在当前分支提交。

## 构建与验证 / Build & validation

- 构建：`dotnet build OperationSteelTide.csproj`（必须 0 警告 0 错误）。
- 诊断（Godot 控制台版）：
  `& "C:\Users\85730\Downloads\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe" --path . -- --validate-<name>`
- 常用诊断：`stairs`、`skylinks`、`residential`、`residential-gameplay`、`residential-localization`、`squad`、`vehicle-drive`、`backpack-tab`、`loot`、`weapon-ui`、`equipment`、`pickup`、`corpse-loot`、`objectives`、`reinforcements`、`stance-armor`、`aircraft-combat`、`map-density`、`large-map`、`goal-pack`、`extraction-spawns`、`extraction-ai`、`extraction-loot`、`extraction-loadout`、`extraction-los`、`extract-rank`。
- 改动地图几何/碰撞后至少跑 `residential`、`stairs`、`skylinks`、`vehicle-drive`；改动小队/复活逻辑后跑 `squad`；改动 HUD/背包后跑 `backpack-tab`、`weapon-ui`、`loot`。
- 改动住宅区可见文字或语言切换逻辑后至少跑 `residential-localization`。

## 代码约定 / Conventions

- 所有新代码和重构必须遵守 `docs/ENGINEERING_STANDARDS.md`；其中的 `MUST` 是提交门槛，`SHOULD` 若偏离必须在提交或 PR 中说明。
- Godot 4.6 Mono，C# partial class 拆分文件（如 `TacticalPlayer.cs` + `TacticalPlayer.Squad.cs`、`FreightTerminalWorld.cs` + `.Level/.Expansion/.Residential/.Squad.cs`）；新成员放进语义对应的 partial 文件。
- 源文件 UTF-8 无 BOM、CRLF。中文文案写入 `csharp/GameLocalization.cs` 静态构造函数，用 `\uXXXX` 转义添加，避免编码损坏。
- 诊断参数统一 `--validate-xxx`，输出 `XXX_CHECK ...` 与 `XXX_PASS valid=...`，以 `GetTree().Quit(valid ? 0 : 2)` 结束；诊断必须确定性（出生点是随机分散的，涉及位置断言时先把玩家/队友传送到固定空旷点）。
- Godot 4.6 中同名兄弟节点会被改名为 `@类名@序号`：批量生成节点时保证名字唯一（参考 `ModelProp` 的计数器）。
- 物理层约定：世界静态几何 layer 1，车辆/玩家 layer 1 mask 1|2，队友 layer 4。

## 玩法基线 / Gameplay baseline

- 冷启动：玩家与 AI 队友出生空手，需要搜刮武器；背包 UI 不得在未持枪时显示主武器。
- 玩家倒地后由最近的存活 AI 队友跑过来跪地读条救援（`UpdateLeaderReviveAi`），每个角色每条命只能被救一次。
- 车辆：WASD 驾驶，低矮路缘/道具自动越障（`TryCurbStep`），完全受阻时提示倒车；卡车出生点正前方车道（x -2..1，自 -0.5,-11.5 向 -Z）必须保持畅通。

## 3D art quality gate (mandatory)

- Characters, buildings, vehicles, and major visible props MUST use production-quality authored 3D assets. Use a suitable finished asset from a reputable model marketplace, or create/edit the asset in a real DCC workflow such as Blender. Code-generated primitive meshes, CSG, and assembled boxes MUST NOT be presented as final visible art.
- Procedural geometry is allowed only for grayboxing, diagnostics, collision, navigation, occlusion, or invisible gameplay scaffolding. It may ship as visible art only when the user explicitly approves a procedural or primitive visual style.
- Before adding or replacing a character, building, vehicle, or major prop, inspect suitable assets on sources such as Fab, Sketchfab, Poly Haven, CGTrader, or KitBash3D. Prefer `GLB`/`glTF` or `FBX`, usable materials, and an appropriate polygon budget. Existing programmer art is a placeholder and must not be used as the visual quality reference for new work.
- This is a public MIT repository. Raw third-party assets may be committed only when their license explicitly permits redistribution, normally CC0 or CC BY 4.0. A zero price does not imply redistribution permission. Fab Standard, marketplace, editorial, personal-use, or unclear-license assets MUST remain outside the public repository.
- Record the creator, source URL, exact license, acquisition date, required attribution, and local file mapping in `assets/models/LICENSE.md` and `docs/CONTENT_PROVENANCE.md`. Preserve a copy or screenshot of the license evidence when practical.
- Paid or non-redistributable assets must live in a private asset store. Commit only import scripts, adapters, placeholders, and acquisition/setup instructions; never commit or reconstruct the protected raw asset.
- Validate every final asset in Godot from representative player-camera distances. Check scale, silhouette, PBR materials, animation deformation, equipment attachment, lighting, collision alignment, clipping, draw calls, and texture memory; capture screenshots for visual review before declaring the work complete.
- If no suitable licensable asset can be found and a proper DCC asset cannot be produced with the available tools, stop and report the constraint. Do not silently substitute programmer art, primitive geometry, or a code-only procedural model as the final result.
