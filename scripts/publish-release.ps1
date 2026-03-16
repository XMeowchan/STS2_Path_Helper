param(
    [string]$Configuration = "Release",
    [string]$Repo = $env:GITHUB_REPOSITORY,
    [string]$GameDir,
    [string]$NotesPath,
    [switch]$Upload,
    [switch]$Prerelease,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

function Get-Manifest {
    return Get-Content -LiteralPath (Join-Path $projectRoot "mod_manifest.json") -Raw | ConvertFrom-Json
}

function New-ReleaseNotes {
    param(
        [Parameter(Mandatory)]
        [string]$Version,
        [Parameter(Mandatory)]
        [string]$OutputPath,
        [Parameter(Mandatory)]
        [string]$PortableName,
        [Parameter(Mandatory)]
        [string]$InstallerName,
        [Parameter(Mandatory)]
        [string]$ModName
    )

$content = @"
# $ModName $Version

## Assets

- $InstallerName
- $PortableName

## Notes

- Adds map route planning directly from the legend buttons.
- Reuses the game's built-in map drawing tool to preview planned routes.
- Supports cycling through all best routes for the selected node type.
- Keeps multiplayer-friendly behavior by hiding this mod from strict mod-list checks by default.
"@

    Set-Content -LiteralPath $OutputPath -Value $content -Encoding UTF8
}

function Invoke-BuildArtifacts {
    param(
        [Parameter(Mandatory)]
        [string]$BuildConfiguration,
        [AllowNull()]
        [string]$BuildGameDir
    )

    $buildArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "build-mod-artifacts.ps1"),
        "-Configuration", $BuildConfiguration
    )
    if ($BuildGameDir) {
        $buildArgs += @( "-GameDir", $BuildGameDir )
    }

    $portableArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "build-portable-package.ps1"),
        "-Configuration", $BuildConfiguration,
        "-SkipBuild"
    )
    $installerArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "build-installer.ps1"),
        "-Configuration", $BuildConfiguration,
        "-SkipBuild"
    )

    if ($BuildGameDir) {
        $portableArgs += @( "-GameDir", $BuildGameDir )
        $installerArgs += @( "-GameDir", $BuildGameDir )
    }

    & powershell @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "build-mod-artifacts failed."
    }

    & powershell @portableArgs
    if ($LASTEXITCODE -ne 0) {
        throw "build-portable-package failed."
    }

    & powershell @installerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "build-installer failed."
    }
}

function Test-GhInstalled {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    return $null -ne $gh
}

function Get-ReleaseToken {
    foreach ($name in @("GITHUB_TOKEN", "GH_TOKEN", "GITHUB_RELEASE_TOKEN")) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return $null
}

function Get-ReleaseHeaders {
    param(
        [Parameter(Mandatory)]
        [string]$Token
    )

    return @{
        Authorization          = "Bearer $Token"
        Accept                 = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent"          = "STS2-Mod-Template-ReleaseScript"
    }
}

function Get-ReleaseApiBase {
    param(
        [Parameter(Mandatory)]
        [string]$Repository
    )

    return "https://api.github.com/repos/$Repository/releases"
}

function Get-ReleaseByTagViaApi {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$TagName,
        [Parameter(Mandatory)]
        [string]$Token
    )

    $uri = "{0}/tags/{1}" -f (Get-ReleaseApiBase -Repository $Repository), $TagName
    try {
        return Invoke-RestMethod -Method Get -Uri $uri -Headers (Get-ReleaseHeaders -Token $Token)
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            return $null
        }

        throw
    }
}

function Ensure-ReleaseViaApi {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$TagName,
        [Parameter(Mandatory)]
        [string]$Title,
        [Parameter(Mandatory)]
        [string]$NotesFile,
        [Parameter(Mandatory)]
        [string]$Token,
        [switch]$IsPrerelease
    )

    $headers = Get-ReleaseHeaders -Token $Token
    $notes = [string](Get-Content -LiteralPath $NotesFile -Raw)
    $existing = Get-ReleaseByTagViaApi -Repository $Repository -TagName $TagName -Token $Token
    if ($existing) {
        $body = @{
            name       = $Title
            body       = $notes
            prerelease = [bool]$IsPrerelease
        } | ConvertTo-Json -Depth 6

        return Invoke-RestMethod -Method Patch -Uri ("{0}/{1}" -f (Get-ReleaseApiBase -Repository $Repository), $existing.id) -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -ContentType "application/json; charset=utf-8"
    }

    $createBody = @{
        tag_name   = $TagName
        name       = $Title
        body       = $notes
        draft      = $false
        prerelease = [bool]$IsPrerelease
    } | ConvertTo-Json -Depth 6

    return Invoke-RestMethod -Method Post -Uri (Get-ReleaseApiBase -Repository $Repository) -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($createBody)) -ContentType "application/json; charset=utf-8"
}

function Remove-ExistingAssetsViaApi {
    param(
        [Parameter(Mandatory)]
        [psobject]$Release,
        [Parameter(Mandatory)]
        [string]$Token,
        [Parameter(Mandatory)]
        [string[]]$AssetNames
    )

    $headers = Get-ReleaseHeaders -Token $Token
    foreach ($asset in @($Release.assets)) {
        if ($AssetNames -contains $asset.name) {
            $deleteUri = "$($asset.url)"
            if ([string]::IsNullOrWhiteSpace($deleteUri)) {
                throw "Release asset '$($asset.name)' does not include an API URL."
            }
            Invoke-RestMethod -Method Delete -Uri $deleteUri -Headers $headers | Out-Null
        }
    }
}

function Upload-AssetsViaApi {
    param(
        [Parameter(Mandatory)]
        [psobject]$Release,
        [Parameter(Mandatory)]
        [string]$Token,
        [Parameter(Mandatory)]
        [string[]]$AssetPaths
    )

    $headers = Get-ReleaseHeaders -Token $Token
    $uploadBase = ($Release.upload_url -replace '\{\?name,label\}$', '')
    foreach ($assetPath in $AssetPaths) {
        $assetName = Split-Path $assetPath -Leaf
        $uploadUri = "{0}?name={1}" -f $uploadBase, [System.Uri]::EscapeDataString($assetName)
        Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -InFile $assetPath -ContentType "application/octet-stream" | Out-Null
    }
}

function Test-ReleaseExists {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$TagName
    )

    & gh release view $TagName --repo $Repository *> $null
    return $LASTEXITCODE -eq 0
}

function Publish-ReleaseAssets {
    param(
        [Parameter(Mandatory)]
        [string]$Repository,
        [Parameter(Mandatory)]
        [string]$TagName,
        [Parameter(Mandatory)]
        [string]$Title,
        [Parameter(Mandatory)]
        [string]$NotesFile,
        [Parameter(Mandatory)]
        [string[]]$AssetPaths,
        [switch]$IsPrerelease
    )

    if ([string]::IsNullOrWhiteSpace($Repository)) {
        throw "Repository is required for -Upload. Pass -Repo owner/repo or set GITHUB_REPOSITORY."
    }

    $token = Get-ReleaseToken
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $release = Ensure-ReleaseViaApi `
            -Repository $Repository `
            -TagName $TagName `
            -Title $Title `
            -NotesFile $NotesFile `
            -Token $token `
            -IsPrerelease:$IsPrerelease
        Remove-ExistingAssetsViaApi -Release $release -Token $token -AssetNames ($AssetPaths | ForEach-Object { Split-Path $_ -Leaf })
        $release = Get-ReleaseByTagViaApi -Repository $Repository -TagName $TagName -Token $token
        Upload-AssetsViaApi -Release $release -Token $token -AssetPaths $AssetPaths
        return
    }

    if (-not (Test-GhInstalled)) {
        throw "Neither GitHub CLI nor GITHUB_TOKEN is available. Install gh, or provide GITHUB_TOKEN for upload."
    }

    if (Test-ReleaseExists -Repository $Repository -TagName $TagName) {
        & gh release upload $TagName @AssetPaths --clobber --repo $Repository
        if ($LASTEXITCODE -ne 0) {
            throw "gh release upload failed."
        }
        return
    }

    $args = @(
        "release", "create", $TagName,
        "--repo", $Repository,
        "--title", $Title,
        "--notes-file", $NotesFile
    )
    if ($IsPrerelease) {
        $args += "--prerelease"
    }
    $args += $AssetPaths

    & gh @args
    if ($LASTEXITCODE -ne 0) {
        throw "gh release create failed."
    }
}

$manifest = Get-Manifest
$version = [string]$manifest.version
$modId = [string]$manifest.pck_name
$modName = [string]$manifest.name
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "mod_manifest.json does not contain a version."
}
if ([string]::IsNullOrWhiteSpace($modId)) {
    throw "mod_manifest.json does not contain pck_name."
}
if ([string]::IsNullOrWhiteSpace($modName)) {
    $modName = $modId
}

$tag = "v$version"
$releaseDir = Join-Path $projectRoot "dist\release"
$installerDir = Join-Path $projectRoot "dist\installer\output"

if (-not $SkipBuild) {
    Invoke-BuildArtifacts -BuildConfiguration $Configuration -BuildGameDir $GameDir
}

$portablePath = Join-Path $releaseDir ("{0}-portable-{1}.zip" -f $modId, $version)
$installerPath = Join-Path $installerDir ("{0}-Setup-{1}.exe" -f $modId, $version)
foreach ($requiredPath in @($portablePath, $installerPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Missing release artifact: $requiredPath"
    }
}

if (-not $NotesPath) {
    $NotesPath = Join-Path $releaseDir ("RELEASE_NOTES-{0}.md" -f $tag)
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $NotesPath) | Out-Null
New-ReleaseNotes `
    -Version $version `
    -OutputPath $NotesPath `
    -PortableName (Split-Path $portablePath -Leaf) `
    -InstallerName (Split-Path $installerPath -Leaf) `
    -ModName $modName

Write-Host "Release tag: $tag"
Write-Host "Release notes: $NotesPath"
Write-Host "Installer: $installerPath"
Write-Host "Portable: $portablePath"

if ($Upload) {
    Publish-ReleaseAssets `
        -Repository $Repo `
        -TagName $tag `
        -Title $tag `
        -NotesFile $NotesPath `
        -AssetPaths @($installerPath, $portablePath) `
        -IsPrerelease:$Prerelease
    Write-Host "Uploaded release assets to $Repo ($tag)."
} else {
    Write-Host "Upload skipped. Attach both files to the GitHub Release manually, or rerun with -Upload."
}
