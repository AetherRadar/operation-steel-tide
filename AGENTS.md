# AGENTS.md — Operation Steel Tide

## 并行 Worktree 与交付纪律（强制 / Parallel worktree delivery discipline）

- 开发任务默认留在各自 Codex worktree 中并行执行；不要仅为交付或测试而切换到共享的 Local 检出。Codex 管理的 worktree 可能处于 detached HEAD，任何流程都不得假定存在当前分支或 upstream。
- Development tasks stay in their own Codex worktrees so they can run in parallel. Do not move them into the shared Local checkout merely for delivery or testing. A Codex-managed worktree may use a detached HEAD, so delivery MUST NOT assume that a current branch or upstream exists.
- 需要在交付前人工测试时，直接以任务 worktree 作为 Godot 的 `--path`；持有 `main` 的主检出只用于已经集成内容的本地测试和最终同步。
- 每个任务必须按功能分组提交全部改动，不得把未提交或仅存在于 detached worktree 的改动留给后续任务。提交信息使用英文祈使句，例如 `Fix mirrored residential stairs`、`Add AI teammate revive for downed player`。
- 提交和交付前必须通过 `dotnet build OperationSteelTide.csproj`（0 警告、0 错误）以及与改动相关的全部 `--validate-*` 诊断（退出码 0）。
- `main` 是唯一交付分支。提交后执行 `git fetch origin`，并用 `git merge-base --is-ancestor origin/main HEAD` 确认任务基于最新 `origin/main`；若不是，必须把任务提交 rebase 到最新 `origin/main`，重新运行构建和相关诊断，再继续交付。
- 使用普通非强制推送 `git push origin HEAD:main` 交付。若因另一并发任务先更新 `origin/main` 而被拒绝，必须重新 fetch、rebase、验证并重试；严禁 force push `main`。若分支保护要求 PR，则推送唯一的 `codex/<task>` 分支并等待 PR 实际合入，未合入不得宣称完成。
- 远端推送或 PR 合入成功后，必须使用 `git worktree list --porcelain` 动态找到持有 `refs/heads/main` 的主检出并完成本地 `main` 同步，使用户的常用本地目录立即看到已交付改动；本地 `main` 未同步完成时不得宣称交付完成。
- 主检出的同步是唯一允许串行等待的步骤；其他 worktree 的开发、构建和诊断必须继续并行。若检测到另一个同步过程或 Git 锁，等待并有限重试，禁止删除锁文件或强杀进程。
- 同步前必须检查主检出的分支、工作区和 Git 状态。若存在 tracked/untracked 改动、merge/rebase 状态或无法快进，不得以此为由跳过同步；先研究本地与远端内容的来源和意图，保全所有独有内容，再逐项解决冲突并完成合并。与即将同步的 tracked 内容逐字节相同的未跟踪文件，可在校验一致后移除并由同步恢复；任何有独有内容的本地改动必须先保存到可恢复的专用备份分支/提交或明确备份路径，再同步并重新应用或合并。
- 解决同步冲突后必须运行受影响的构建和诊断。严禁使用 hard reset、force checkout、整批覆盖或 force push `main` 来丢弃任一侧内容。只有认证、网络或仓库损坏等外部问题在合理重试后仍无法克服时，才可停止并报告准确阻塞原因。
- 最终必须再次 `git fetch origin` 并确认：本任务实际落地 commit 是 `origin/main` 的祖先（直接推送时为任务 commit；squash/rebase PR 时为平台生成的落地 commit）；主检出 `HEAD` 与 `origin/main` 完全一致；主检出和任务 worktree 均干净；主检出 `git status --short --branch` 不显示 ahead/behind。

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
