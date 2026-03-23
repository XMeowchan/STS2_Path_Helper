# STS2 Path Helper

[简体中文](./README.md) | [English](./README.en.md) | [日本語](./README.ja.md)

一个用于 *Slay the Spire 2* 的地图路径规划 Mod。

它会在地图图例旁显示路线统计，并允许玩家通过点击多个图例按钮，按优先级组合规划路线。

## 功能

- 点击图例按钮后，在地图上预览推荐路线
- 支持多因子优先级选路
- 支持同优先级最佳路线轮换
- 在图例旁显示当前路线的节点统计
- 仅使用本地 UI 与本地计算，不依赖外部服务

## 当前优先级规则

- 最先点击的图例优先级最高
- 后续点击的图例依次作为次级条件
- 再次点击已加入优先级的图例时，不改变顺序，只轮换当前最优解集合
- 清空画板时，会同时清空当前优先级栈与图例统计

## 产物结构

当前版本使用 Slay the Spire 2 新版 Mod 结构：

- `Sts2PathHelper.json`
- `Sts2PathHelper.dll`
- `Sts2PathHelper.pck`
- `config.cfg`

## 常用命令

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mod-artifacts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable-package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

## 发布产物

- `Sts2PathHelper-Setup-x.y.z.exe`
- `Sts2PathHelper-portable-x.y.z.zip`

## 多人兼容

- `affects_gameplay = false`
- `config.cfg` 默认保持 `hide_from_multiplayer_mod_list = true`

## 更多文档

- [English README](./README.en.md)
- [日本語 README](./README.ja.md)
