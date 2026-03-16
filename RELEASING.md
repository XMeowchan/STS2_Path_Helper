# Releasing

这个模板默认产出两类发布物：

- `<ModId>-Setup-x.y.z.exe`
- `<ModId>-portable-x.y.z.zip`

其中：

- 安装器适合普通玩家首次安装
- portable zip 适合手动安装、调试或快速分发

## 本地发布流程

1. 更新 `mod_manifest.json` 中的 `version`
2. 构建发布产物：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1
```

这会自动：

- 构建 DLL
- 构建 `.pck`
- 生成 portable zip
- 生成 installer exe
- 生成 release notes 到 `dist\release\`

## 可选代码签名

如果你有 PFX 证书，可以先设置：

```powershell
$env:CODESIGN_PFX_PATH="C:\codesign\your-cert.pfx"
$env:CODESIGN_PFX_PASSWORD="your-pfx-password"
```

可选时间戳：

```powershell
$env:CODESIGN_TIMESTAMP_URL="http://timestamp.digicert.com"
```

不设置也可以正常构建，只会跳过签名。

## 上传到 GitHub Release

如果本地安装了 `gh` 或设置了 `GITHUB_TOKEN`，可以直接上传：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Upload
```

如果当前环境没有可用的 GitHub 仓库上下文，请显式传入：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-release.ps1 -Upload -Repo owner/repo
```
