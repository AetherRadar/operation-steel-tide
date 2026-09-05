# Tripo Studio + Godot Bridge 助手

这个助手把 Tripo Studio 的网页生成流程和 Operation Steel Tide 的本地资源流程接起来，但**不调用 Tripo 私有接口、不读取浏览器 Cookie、不保存账号密码，也不绕过 Studio/API 的计费边界**。

## 它能做什么

- 根据参考图和提示词创建一个本地任务清单；
- 打开你购买积分的 Tripo Studio 工作区；
- 记录参考图 SHA-256，避免生成任务和素材来源混乱；
- 接收官方 Godot Bridge 导出的 GLB/GLTF/FBX/OBJ；
- 将模型归档到 `assets/models/tripo/<asset-slug>/model.*`；
- 写入 `tripo_asset.json`，记录来源、任务、哈希和导入时间；
- 列出任务、扫描收件箱、验证任务清单完整性。

## 第一次设置

1. 在 Tripo Studio 下载并安装官方 Godot Bridge。
2. 在 Godot 4.6+ 中启用 Bridge 插件。
3. 在 Tripo Studio 的 DCC Bridge 导出面板中，将接收目录设置为本项目的：

   ```text
   assets/tripo_inbox
   ```

4. 推荐导出 **GLB**，这样网格、材质和贴图可以保持在一个文件中。

官方 Bridge 支持 Windows、Mac 和 Godot 4.6+，并可将 Studio 中生成的模型直接发送到打开的 Godot 项目。详见官方指南：<https://www.tripo3d.ai/zh/blog/tripo-dcc-bridge-for-godot>

## 命令

以下命令在项目根目录的 PowerShell 中运行。

### 准备任务

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 prepare `
  -ReferenceImage "C:\refs\armored_vehicle.png" `
  -AssetName "armored_vehicle" `
  -Prompt "Realistic modern armored vehicle, game-ready Smart Mesh, clean hard-surface topology, separate wheels and turret, no floating parts, neutral studio lighting." `
  -Pipeline textured_pbr
```

任务清单会写入 `build/tripo_studio/tasks/`。这个目录默认属于构建产物，不会把你的参考图提交到公共仓库。

如果希望准备完直接打开 Studio：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 prepare `
  -ReferenceImage "C:\refs\armored_vehicle.png" `
  -AssetName "armored_vehicle" `
  -Prompt "Realistic modern armored vehicle, game-ready Smart Mesh, clean hard-surface topology." `
  -OpenAfterPrepare
```

### 单独打开 Studio

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 open
```

### 扫描 Bridge 收件箱

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 scan
```

扫描只列出候选模型，不会擅自覆盖文件。

### 归档生成模型

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 import `
  -Source ".\assets\tripo_inbox\armored_vehicle.glb" `
  -AssetName "armored_vehicle" `
  -TaskId "20260905-120000-armored-vehicle"
```

模型会被复制到：

```text
assets/models/tripo/armored-vehicle/model.glb
assets/models/tripo/armored-vehicle/tripo_asset.json
```

Godot 会自动发现新文件。导入后仍需检查缩放、PBR、碰撞、LOD、面数和代表性镜头距离。

### 查看任务和验证

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 list
powershell -ExecutionPolicy Bypass -File .\tools\tripo_studio_bridge.ps1 validate
```

`validate` 会输出一条 `TRIPO_STUDIO_CHECK` 和一条 `TRIPO_STUDIO_PASS valid=...`，发现参考图缺失或哈希变化时以退出码 2 结束。

## AI 使用约定

你可以直接对 AI 说：

> 用 `assets/reference/tank.png` 生成一辆现代装甲车，风格写实，Godot 游戏资产，Smart Mesh、PBR 材质、GLB，导入到 `assets/models/tripo/armored-vehicle/`。

AI 应该先生成任务清单，再让你在 Studio 网页中完成上传和生成，最后用官方 Godot Bridge 发送模型并执行 `import`。上传参考图和生成模型会把数据传给 Tripo，这一步仍由你在网页端确认。

## 重要边界

- 这个助手不把 Studio 积分转换成 API 积分；Tripo 官方说明两者是独立计费系统。
- 不要把账号密码、浏览器 Cookie 或 API Key 写入任务清单或提交到 Git。
- 公开发行前，保留 Tripo 付费计划凭证，并核对参考图和生成资产的使用权。
- AI 生成模型仍需经过 Blender/Godot 的材质、碰撞、LOD、性能和授权检查，不能未经审核直接视为最终资产。
