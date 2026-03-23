# STS2 Path Helper

[简体中文](./README.md) | [English](./README.en.md) | [日本語](./README.ja.md)

STS2 Path Helper is a *Slay the Spire 2* map-planning mod.

It adds route statistics next to the map legend and lets the player build route priorities by clicking multiple legend entries.

## Features

- Preview recommended routes directly from the map legend
- Supports multi-factor route planning
- Supports cycling between equally optimal routes
- Displays per-route node counts next to each legend entry
- Uses only local UI and local route evaluation

## Priority Rules

- The first clicked legend entry has the highest priority
- Later clicks are used as lower-priority tie-breakers
- Clicking an already selected legend entry does not reorder priorities; it cycles routes within the current best set
- Clearing the drawing board also clears the active priority stack and legend counters

## Packaging Format

This project now follows the newer *Slay the Spire 2* mod layout:

- `Sts2PathHelper.json`
- `Sts2PathHelper.dll`
- `Sts2PathHelper.pck`
- `config.cfg`

## Common Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mod-artifacts.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable-package.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

## Release Artifacts

- `Sts2PathHelper-Setup-x.y.z.exe`
- `Sts2PathHelper-portable-x.y.z.zip`

## Multiplayer Compatibility

- `affects_gameplay = false`
- `config.cfg` keeps `hide_from_multiplayer_mod_list = true` by default
