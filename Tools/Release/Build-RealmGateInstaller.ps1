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
$portableRoot = Join-Path $stagingRoot "RealmGate"
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
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

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

$runtimeConfigText = Get-Content -LiteralPath $runtimeConfig -Raw
if ($runtimeConfigText -match 'Microsoft\.NETCore\.App.*version') {
    Write-Verbose "Runtime configuration present."
}

$forbiddenPathMatches = Get-ChildItem -LiteralPath $publishDirectory -Recurse -Force |
    Where-Object { $_.FullName -match '(?i)(Borin|LLamaSharp|NvidiaApiKey|LivingForge)' }
if ($forbiddenPathMatches) {
    $paths = ($forbiddenPathMatches | Select-Object -ExpandProperty FullName) -join [Environment]::NewLine
    throw "Public package contains private/Borin-named files:$([Environment]::NewLine)$paths"
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

Write-Host "Creating portable ZIP..."
Copy-Item -LiteralPath $publishDirectory -Destination $portableRoot -Recurse -Force
$portablePath = Join-Path $packageDirectory $portableFileName
Compress-Archive -LiteralPath $portableRoot -DestinationPath $portablePath -CompressionLevel Optimal -Force

Write-Host "Compiling Windows installer..."
$iscc = Find-InnoCompiler
$innoArguments = @(
    "/DMyAppVersion=$Version",
    "/DSourceDir=$publishDirectory",
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
Write-Host "The installer is self-contained; testers do not need to install the .NET Desktop Runtime separately."
