# STS2 Path Helper

这是一个用于《Slay the Spire 2》的地图路径规划小助手 Mod。

它会在地图界面追加一个小型策略面板，帮助玩家从当前节点开始快速预览更偏向：

- 精英最多的路线
- 问号（事件倾向）最多的路线
- 商店最多的路线

当前版本使用一层独立的路线预览覆盖层来画线，不会清掉玩家自己手动画的地图涂鸦；同时默认继续隐藏自己在联机 Mod 校验列表中的显示，尽量降低“必须双方 Mod 列表一致”的硬要求。

## 当前能力

- 地图界面内按钮回调入口
- 基于地图节点数据的简单策略选路
- 按地图 UI 实际节点位置绘制路线预览
- 保留 Windows-first 的构建、部署、ZIP、安装器、GitHub Release 流程
- 保留多人联机下的“隐藏 Mod 列表校验”补丁

## 策略说明

- `精英 / Elite`: 优先最大化精英数量，并用商店、篝火等节点做次级打分
- `问号 / Event`: 当前按问号节点(`Unknown`)优先，适合作为“事件倾向”路线的近似预览
- `商店 / Shop`: 优先最大化商店数量，并对路线质量做简单次级打分

## 关键源码

| 路径 | 作用 |
| --- | --- |
| `src/ModEntry.cs` | Mod 初始化入口 |
| `src/MapPlannerPatches.cs` | 地图界面注入补丁 |
| `src/MapPlannerController.cs` | 地图策略面板与路线绘制覆盖层 |
| `src/MapPlannerService.cs` | 选路算法 |
| `src/MapPlannerModels.cs` | 策略配置、路线统计模型 |
| `src/MultiplayerModListPatches.cs` | 联机 Mod 列表隐藏补丁 |

## 资源与打包

- 打进 `.pck` 的静态资源放在 `pack_assets/Sts2PathHelper/`
- 构建脚本会读取 `mod_manifest.json` 中的 `pck_name`
- `deploy / portable / installer / release` 四条链路都继续沿用现有 PowerShell 脚本

## 常用命令

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mod-artifacts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable-package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

## 发布产物

- `Sts2PathHelper.dll`
- `Sts2PathHelper.pck`
- `Sts2PathHelper-portable-x.y.z.zip`
- `Sts2PathHelper-Setup-x.y.z.exe`

## 联机兼容说明

- `config.json` 默认保持 `hide_from_multiplayer_mod_list = true`
- 当前路线预览是本地 UI 辅助层，不依赖外部服务，也不会引入联网数据依赖
- 目标是“装了更方便，不装也不至于卡死联机校验”
