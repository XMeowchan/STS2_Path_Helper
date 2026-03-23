# STS2 Path Helper

[简体中文](./README.md) | [English](./README.en.md) | [日本語](./README.ja.md)

STS2 Path Helper は *Slay the Spire 2* 向けのマップ経路計画 Mod です。

マップの凡例の横にルート統計を表示し、複数の凡例ボタンをクリックして優先順位付きの経路計画を行えます。

## 機能

- マップ凡例から推奨ルートを直接プレビュー
- 複数条件によるルート優先順位付けに対応
- 同率最適ルートの切り替えに対応
- 各凡例項目の横に、そのルート上のノード数を表示
- ローカル UI とローカル計算のみを使用

## 優先順位ルール

- 最初にクリックした凡例が最優先
- その後のクリックは下位のタイブレーク条件として使用
- 既に選択済みの凡例を再クリックしても順序は変わらず、現在の最適集合内でルートを切り替えるだけ
- 描画クリア時には優先順位スタックと凡例カウンターも同時にクリア

## パッケージ形式

このプロジェクトは新しい *Slay the Spire 2* の Mod 構成に対応しています。

- `Sts2PathHelper.json`
- `Sts2PathHelper.dll`
- `Sts2PathHelper.pck`
- `config.cfg`

## よく使うコマンド

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mod-artifacts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable-package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

## 配布物

- `Sts2PathHelper-Setup-x.y.z.exe`
- `Sts2PathHelper-portable-x.y.z.zip`

## マルチプレイ互換

- `affects_gameplay = false`
- `config.cfg` の `hide_from_multiplayer_mod_list = true` がデフォルト
