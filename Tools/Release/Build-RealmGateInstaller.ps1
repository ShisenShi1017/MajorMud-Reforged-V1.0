[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Version,
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$publisher = Join-Path $PSScriptRoot "Publish-RealmGate.ps1"
$installerDefinition = Join-Path $PSScriptRoot "RealmGate.iss"
$publishDirectory = Join-Path $repoRoot "Release\RealmGate-Public"
$packageDirectory = Join-Path $repoRoot "Release\Packages"
$stagingRoot = Join-Path $repoRoot "Release\Packaging"
$runtimeRoot = Join-Path $stagingRoot "RealmGate"
$safeVersion = ($Version -replace '[^0-9A-Za-z._-]', '-')
$installerBaseName = "RealmGate-Beta-$safeVersion-Setup"
$portableFileName = "RealmGate-Beta-$safeVersion-win-x64-portable.zip"
$checksumFileName = "RealmGate-Beta-$safeVersion-SHA256.txt"

function Find-InnoCompiler {
    $fromPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $fromPath) {
        return $fromPath.Source
    }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup 6 was not found. Install Inno Setup 6, then run this script again."
}

if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null

Write-Host "Building Borin-free, self-contained RealmGate public beta $Version..."
& $publisher -Configuration Release -RuntimeIdentifier $RuntimeIdentifier -PublicRelease
if ($LASTEXITCODE -ne 0) {
    throw "RealmGate public publish failed with exit code $LASTEXITCODE."
}

$realmGateExe = Join-Path $publishDirectory "RealmGate.exe"
if (-not (Test-Path -LiteralPath $realmGateExe)) {
    throw "RealmGate.exe was not produced at '$realmGateExe'."
}

$runtimeConfig = Join-Path $publishDirectory "RealmGate.runtimeconfig.json"
if (-not (Test-Path -LiteralPath $runtimeConfig)) {
    throw "RealmGate.runtimeconfig.json was not produced."
}

$forbiddenPublishMatches = Get-ChildItem -LiteralPath $publishDirectory -Recurse -Force |
    Where-Object { $_.FullName -match '(?i)(Borin|LLamaSharp|NvidiaApiKey|LivingForge)' }
if ($forbiddenPublishMatches) {
    $paths = ($forbiddenPublishMatches | Select-Object -ExpandProperty FullName) -join [Environment]::NewLine
    throw "Public application publish contains private/Borin-named files:$([Environment]::NewLine)$paths"
}

$depsFile = Join-Path $publishDirectory "RealmGate.deps.json"
if (Test-Path -LiteralPath $depsFile) {
    $depsText = Get-Content -LiteralPath $depsFile -Raw
    $forbiddenDependencies = @('LLamaSharp', 'LLamaSharp.Backend.Vulkan', 'Microsoft.CodeAnalysis.CSharp')
    foreach ($dependency in $forbiddenDependencies) {
        if ($depsText -match [regex]::Escape($dependency)) {
            throw "Public package unexpectedly contains private dependency '$dependency'."
        }
    }
}

Write-Host "Staging Librarium-first public runtime..."
Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $runtimeRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "ProjectManifest.json") -Destination $runtimeRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "Config") -Destination (Join-Path $runtimeRoot "Config") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "Librarium") -Destination (Join-Path $runtimeRoot "Librarium") -Recurse -Force

$requiredRuntimePaths = @(
    "RealmGate.exe",
    "RealmGate.Compiler.dll",
    "ProjectManifest.json",
    "Config\server.worlds.json",
    "Librarium\Objects",
    "Librarium\WorldOverrides"
)
foreach ($relativePath in $requiredRuntimePaths) {
    $fullPath = Join-Path $runtimeRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Public runtime staging is missing required path '$relativePath'."
    }
}

if (Test-Path -LiteralPath (Join-Path $runtimeRoot "Data\Classic")) {
    throw "Generated Data/Classic must not be shipped as authoritative public content. It must be rebuilt from Librarium locally."
}

Write-Host "Verifying packaged Librarium can regenerate all enabled runtime worlds..."
$dataRoot = Join-Path $runtimeRoot "Data"
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
$compilerDll = Join-Path $runtimeRoot "RealmGate.Compiler.dll"
$worldConfig = Get-Content -LiteralPath (Join-Path $runtimeRoot "Config\server.worlds.json") -Raw | ConvertFrom-Json
$enabledWorlds = @($worldConfig.worlds | Where-Object { $_.runtimeEnabled -eq $true })
if ($enabledWorlds.Count -eq 0) {
    throw "No enabled worlds were found in Config/server.worlds.json."
}

foreach ($world in $enabledWorlds) {
    Write-Host "  Generating $($world.id) from staged Librarium..."
    & dotnet $compilerDll build --root $runtimeRoot --world $world.id --clean
    if ($LASTEXITCODE -ne 0) {
        throw "Packaged Librarium failed to generate world '$($world.id)' (exit $LASTEXITCODE)."
    }

    $normalizedWorld = if ($world.id -ieq "classic") { "Classic" } else { [string]$world.id }
    $generatedRooms = Join-Path $runtimeRoot "Data\$normalizedWorld\rooms.json"
    $generatedManifest = Join-Path $runtimeRoot "Data\$normalizedWorld\world_manifest.json"
    if (-not (Test-Path -LiteralPath $generatedRooms) -or -not (Test-Path -LiteralPath $generatedManifest)) {
        throw "World '$($world.id)' generation did not produce required runtime files."
    }
}

Write-Host "Packaged Librarium regeneration test passed. Removing disposable generated Data before packaging..."
Remove-Item -LiteralPath $dataRoot -Recurse -Force

Write-Host "Creating portable ZIP..."
$portablePath = Join-Path $packageDirectory $portableFileName
Compress-Archive -LiteralPath $runtimeRoot -DestinationPath $portablePath -CompressionLevel Optimal -Force

Write-Host "Compiling Windows installer..."
$iscc = Find-InnoCompiler
$innoArguments = @(
    "/DMyAppVersion=$Version",
    "/DSourceDir=$runtimeRoot",
    "/DOutputDir=$packageDirectory",
    "/DOutputBaseFilename=$installerBaseName",
    $installerDefinition
)
& $iscc @innoArguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $packageDirectory "$installerBaseName.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not produced at '$installerPath'."
}

$checksumPath = Join-Path $packageDirectory $checksumFileName
$checksumLines = foreach ($file in @($installerPath, $portablePath)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($file))"
}
Set-Content -LiteralPath $checksumPath -Value $checksumLines -Encoding ASCII

Write-Host ""
Write-Host "RealmGate public beta packages are ready:"
Write-Host "  Installer: $installerPath"
Write-Host "  Portable:  $portablePath"
Write-Host "  Checksums: $checksumPath"
Write-Host ""
Write-Host "The package is self-contained and Librarium-first. Data/ is generated locally from Librarium on first host start."
