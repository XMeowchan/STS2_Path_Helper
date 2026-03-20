param(
    [string]$GameDir,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")

$manifest = Get-ProjectManifest -ProjectRoot $projectRoot
$modId = [string]$manifest.id

$resolvedGameDir = Resolve-Sts2GameDir -RequestedPath $GameDir
$srcDir = Join-Path $projectRoot "src"
$buildOut = Join-Path $srcDir "bin\$Configuration"
$dllPath = Join-Path $buildOut "$modId.dll"
$pckPath = Join-Path $buildOut "$modId.pck"
$shippingManifestPath = Join-Path $buildOut (Get-ShippingManifestFileName -Manifest $manifest)
$shippingConfigPath = Join-Path $buildOut (Get-ShippingConfigFileName)
$modDir = Join-Path (Resolve-Sts2ModsRoot -GameDir $resolvedGameDir) $modId

$buildArgs = @(
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "build-mod-artifacts.ps1"),
    "-Configuration", $Configuration,
    "-GameDir", $resolvedGameDir
)
& powershell @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "build-mod-artifacts failed."
}

New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Remove-LegacyModFiles -ModDir $modDir
Copy-Item $dllPath (Join-Path $modDir "$modId.dll") -Force
Copy-Item $pckPath (Join-Path $modDir "$modId.pck") -Force
Set-PckCompatibilityHeader -Path (Join-Path $modDir "$modId.pck") -EngineMinorVersion 5
Copy-Item $shippingManifestPath (Join-Path $modDir (Split-Path $shippingManifestPath -Leaf)) -Force
Copy-Item $shippingConfigPath (Join-Path $modDir (Split-Path $shippingConfigPath -Leaf)) -Force

Write-Host "Detected game dir: $resolvedGameDir"
Write-Host "Deployed $modId to $modDir"
