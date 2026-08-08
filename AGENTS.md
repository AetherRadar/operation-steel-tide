# AGENTS.md — Operation Steel Tide

## 交付纪律（强制 / Mandatory delivery discipline）

- 每个任务的最后一步必须是提交代码：把本次任务的全部改动按功能分组做 `git commit`（可以多个 commit），确认工作区干净后才能结束任务。
- Every task MUST end with git commits: group all task changes into logical commits (multiple allowed) and leave a clean working tree before finishing. Never hand off uncommitted work to the next task.
- 提交前必须通过：`dotnet build OperationSteelTide.csproj` 0 警告 0 错误；与改动相关的 `--validate-*` 诊断全部通过（退出码 0）。
- 提交信息用英文祈使句，一个功能一个 commit，例如 `Fix mirrored residential stairs`、`Add AI teammate revive for downed player`。
- 不要主动创建新分支；直接在当前分支提交。

## 构建与验证 / Build & validation

- 构建：`dotnet build OperationSteelTide.csproj`（必须 0 警告 0 错误）。
- 诊断（Godot 控制台版）：
  `& "C:\Users\85730\Downloads\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe" --path . -- --validate-<name>`
- 常用诊断：`stairs`、`skylinks`、`residential`、`squad`、`vehicle-drive`、`backpack-tab`、`loot`、`weapon-ui`、`equipment`、`pickup`、`corpse-loot`、`objectives`、`reinforcements`、`stance-armor`、`aircraft-combat`、`map-density`、`large-map`、`goal-pack`、`extraction-spawns`、`extraction-ai`、`extraction-loot`、`extraction-loadout`、`extraction-los`、`extract-rank`。
- 改动地图几何/碰撞后至少跑 `residential`、`stairs`、`skylinks`、`vehicle-drive`；改动小队/复活逻辑后跑 `squad`；改动 HUD/背包后跑 `backpack-tab`、`weapon-ui`、`loot`。

## 代码约定 / Conventions

- Godot 4.6 Mono，C# partial class 拆分文件（如 `TacticalPlayer.cs` + `TacticalPlayer.Squad.cs`、`FreightTerminalWorld.cs` + `.Level/.Expansion/.Residential/.Squad.cs`）；新成员放进语义对应的 partial 文件。
- 源文件 UTF-8 无 BOM、CRLF。中文文案写入 `csharp/GameLocalization.cs` 静态构造函数，用 `\uXXXX` 转义添加，避免编码损坏。
- 诊断参数统一 `--validate-xxx`，输出 `XXX_CHECK ...` 与 `XXX_PASS valid=...`，以 `GetTree().Quit(valid ? 0 : 2)` 结束；诊断必须确定性（出生点是随机分散的，涉及位置断言时先把玩家/队友传送到固定空旷点）。
- Godot 4.6 中同名兄弟节点会被改名为 `@类名@序号`：批量生成节点时保证名字唯一（参考 `ModelProp` 的计数器）。
- 物理层约定：世界静态几何 layer 1，车辆/玩家 layer 1 mask 1|2，队友 layer 4。

## 玩法基线 / Gameplay baseline

- 冷启动：玩家与 AI 队友出生空手，需要搜刮武器；背包 UI 不得在未持枪时显示主武器。
- 玩家倒地后由最近的存活 AI 队友跑过来跪地读条救援（`UpdateLeaderReviveAi`），每个角色每条命只能被救一次。
- 车辆：WASD 驾驶，低矮路缘/道具自动越障（`TryCurbStep`），完全受阻时提示倒车；卡车出生点正前方车道（x -2..1，自 -0.5,-11.5 向 -Z）必须保持畅通。