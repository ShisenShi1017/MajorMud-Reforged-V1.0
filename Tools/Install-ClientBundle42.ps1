[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$RepositoryPath,

    [switch]$SkipBuild,
    [switch]$SkipPublish,
    [switch]$NoBackup,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$bundleRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repository = [System.IO.Path]::GetFullPath($RepositoryPath)
$manifestPath = Join-Path $bundleRoot "CLIENT_BUNDLE_42_MANIFEST.json"

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if (-not (Test-Path -LiteralPath $repository -PathType Container)) {
    throw "Repository folder does not exist: $repository"
}
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Bundle manifest is missing: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$files = @($manifest.files)
if ($files.Count -eq 0) {
    throw "Bundle manifest contains no files."
}

Write-Step "Validating cumulative bundle source files"
foreach ($entry in $files) {
    $relative = [string]$entry.path
    $source = Join-Path $bundleRoot ($relative -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Bundle file is missing: $relative"
    }
    $actualHash = Get-Sha256 $source
    if ($actualHash -ne ([string]$entry.sha256).ToLowerInvariant()) {
        throw "Bundle hash mismatch: $relative"
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $repository ".client-bundle-backups\bundle42-$timestamp"
if (-not $NoBackup) {
    Write-Step "Backing up repository files that Bundle 42 will replace"
    foreach ($entry in $files) {
        $relative = [string]$entry.path
        $target = Join-Path $repository ($relative -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            $backup = Join-Path $backupRoot ($relative -replace '/', [System.IO.Path]::DirectorySeparatorChar)
            $backupDir = Split-Path -Parent $backup
            New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
            Copy-Item -LiteralPath $target -Destination $backup -Force
        }
    }
    $backupInfo = [ordered]@{
        bundle = 42
        createdUtc = (Get-Date).ToUniversalTime().ToString("o")
        repository = $repository
        sourceManifest = "CLIENT_BUNDLE_42_MANIFEST.json"
    }
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    $backupInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backupRoot "backup-info.json") -Encoding UTF8
}

Write-Step "Installing cumulative Client Bundle 42 into the main repository"
foreach ($entry in $files) {
    $relative = [string]$entry.path
    $source = Join-Path $bundleRoot ($relative -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $target = Join-Path $repository ($relative -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $targetDir = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

Write-Step "Verifying installed repository files"
foreach ($entry in $files) {
    $relative = [string]$entry.path
    $target = Join-Path $repository ($relative -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        throw "Installed file is missing: $relative"
    }
    $actualHash = Get-Sha256 $target
    if ($actualHash -ne ([string]$entry.sha256).ToLowerInvariant()) {
        throw "Installed file hash mismatch: $relative"
    }
}

$python = Get-Command py -ErrorAction SilentlyContinue
if ($null -ne $python) {
    Write-Step "Running Bundle 42 static validation"
    Push-Location $repository
    try {
        & $python.Source -3 "Tools\validate_client_bundle_42.py"
        if ($LASTEXITCODE -ne 0) { throw "Bundle 42 static validation failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}
else {
    Write-Warning "Python launcher not found; static validation was skipped."
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $SkipBuild -and $null -ne $dotnet) {
    Write-Step "Building the unified application ($Configuration)"
    Push-Location $repository
    try {
        if (Test-Path -LiteralPath "MajorMud.Reforged.sln") {
            & $dotnet.Source build "MajorMud.Reforged.sln" -c $Configuration
        }
        elseif (Test-Path -LiteralPath "Apps\Source\Reforged.App\Reforged.App.csproj") {
            & $dotnet.Source build "Apps\Source\Reforged.App\Reforged.App.csproj" -c $Configuration
        }
        else {
            Write-Warning "No solution or Reforged.App project file was found; build skipped."
        }
        if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}
elseif (-not $SkipBuild) {
    Write-Warning ".NET SDK not found; build was skipped."
}

if (-not $SkipPublish -and $null -ne $dotnet) {
    $project = Join-Path $repository "Apps\Source\Reforged.App\Reforged.App.csproj"
    if (Test-Path -LiteralPath $project) {
        $releaseRoot = Join-Path $repository "Artifacts\ClientBundle42"
        $publishRoot = Join-Path $releaseRoot "publish"
        $packageRoot = Join-Path $releaseRoot "packages"
        New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

        Write-Step "Publishing the unified application"
        & $dotnet.Source publish $project -c $Configuration -r win-x64 --self-contained false -o $publishRoot
        if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

        $releaseZip = Join-Path $packageRoot "MajorMud-Reforged-Client-Bundle42-win-x64.zip"
        if (Test-Path -LiteralPath $releaseZip) { Remove-Item -LiteralPath $releaseZip -Force }
        Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $releaseZip -CompressionLevel Optimal
        $releaseHash = Get-Sha256 $releaseZip
        "$releaseHash  $([System.IO.Path]::GetFileName($releaseZip))" | Set-Content -LiteralPath (Join-Path $packageRoot "SHA256SUMS.txt") -Encoding ASCII

        $releaseManifest = [ordered]@{
            bundle = 42
            createdUtc = (Get-Date).ToUniversalTime().ToString("o")
            runtime = "win-x64"
            selfContained = $false
            configuration = $Configuration
            package = [System.IO.Path]::GetFileName($releaseZip)
            sha256 = $releaseHash
        }
        $releaseManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageRoot "release-manifest.json") -Encoding UTF8
        Write-Host "Release package: $releaseZip" -ForegroundColor Green
    }
    else {
        Write-Warning "Reforged.App project not found; publish skipped."
    }
}
elseif (-not $SkipPublish) {
    Write-Warning ".NET SDK not found; publish and release ZIP creation were skipped."
}

Write-Host "`nClient Bundle 42 installed and verified successfully." -ForegroundColor Green
if (-not $NoBackup) {
    Write-Host "Backup: $backupRoot"
}
