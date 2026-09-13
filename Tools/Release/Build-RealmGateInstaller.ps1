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

function Copy-RuntimeLibrariumFile {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $source = Join-Path $repoRoot $RelativePath
    $destination = Join-Path $runtimeRoot $RelativePath
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required runtime Librarium source was not found: '$RelativePath'."
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

function Get-LibrariumSnapshot {
    param([Parameter(Mandatory = $true)][string]$Root)

    return @(
        Get-ChildItem -LiteralPath $Root -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                $relative = [IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
                $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                "$relative|$hash"
            }
    )
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

Write-Host "Staging compact Librarium-first public runtime..."
Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $runtimeRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "ProjectManifest.json") -Destination $runtimeRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "Config") -Destination (Join-Path $runtimeRoot "Config") -Recurse -Force

$librariumRoot = Join-Path $runtimeRoot "Librarium"
New-Item -ItemType Directory -Path $librariumRoot -Force | Out-Null
$compilerDll = Join-Path $runtimeRoot "RealmGate.Compiler.dll"
if (-not (Test-Path -LiteralPath $compilerDll)) {
    throw "RealmGate.Compiler.dll was not staged before Librarium bundling."
}

$bundlePath = Join-Path $librariumRoot "RealmGate.librarium"
Write-Host "Creating compact authoritative Librarium bundle..."
& dotnet $compilerDll bundle --root $repoRoot --output $bundlePath
if ($LASTEXITCODE -ne 0) {
    throw "Compact Librarium bundle creation failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "Compact Librarium bundle was not produced at '$bundlePath'."
}

# The room compiler resolves promoted descriptions independently from object-family loading.
# Keep only these promoted runtime description records loose; do not ship the rest of the
# development/audit Librarium tree.
$roomDescriptionsSource = Join-Path $repoRoot "Librarium\Objects\room_descriptions"
$roomDescriptionsDestination = Join-Path $librariumRoot "Objects\room_descriptions"
if (-not (Test-Path -LiteralPath $roomDescriptionsSource)) {
    throw "Promoted Librarium room descriptions were not found at '$roomDescriptionsSource'."
}
New-Item -ItemType Directory -Path $roomDescriptionsDestination -Force | Out-Null
Get-ChildItem -LiteralPath $roomDescriptionsSource -File -Filter "*.json" |
    Where-Object { $_.Name -match '(?i)(_records\.json$|_records_)' } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $roomDescriptionsDestination $_.Name) -Force
    }

# A few legacy projection writers still consume these canonical sources by exact path.
# Keep only these bridge files loose until those writers are fully bundle-native. This is
# intentionally a tiny allow-list, not a recursive Objects copy.
$runtimeBridgeFiles = @(
    "Librarium\Objects\textblocks\textblock_records.json",
    "Librarium\Objects\native_textblock_payloads\native_textblock_payload_records.json",
    "Librarium\Objects\items\item_records.json",
    "Librarium\Objects\quests\quest_walkthrough_guides.txt"
)
foreach ($relativePath in $runtimeBridgeFiles) {
    Copy-RuntimeLibrariumFile -RelativePath $relativePath
}

# World overrides stay loose and writable because Creator Studio edits them.
$worldOverridesSource = Join-Path $repoRoot "Librarium\WorldOverrides"
$worldOverridesDestination = Join-Path $librariumRoot "WorldOverrides"
if (-not (Test-Path -LiteralPath $worldOverridesSource)) {
    throw "Librarium world overrides were not found at '$worldOverridesSource'."
}
Copy-Item -LiteralPath $worldOverridesSource -Destination $worldOverridesDestination -Recurse -Force

$requiredRuntimePaths = @(
    "RealmGate.exe",
    "RealmGate.Compiler.dll",
    "RealmGate.Server.Runtime.dll",
    "ProjectManifest.json",
    "Config\server.worlds.json",
    "Librarium\RealmGate.librarium",
    "Librarium\Objects\room_descriptions",
    "Librarium\Objects\textblocks\textblock_records.json",
    "Librarium\Objects\native_textblock_payloads\native_textblock_payload_records.json",
    "Librarium\Objects\items\item_records.json",
    "Librarium\Objects\quests\quest_walkthrough_guides.txt",
    "Librarium\WorldOverrides"
)
foreach ($relativePath in $requiredRuntimePaths) {
    $fullPath = Join-Path $runtimeRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Public runtime staging is missing required path '$relativePath'."
    }
}

$forbiddenLibrariumDirectories = @(
    "Assertions",
    "BehaviorGraph",
    "Parity",
    "SourceImports",
    "Reports"
)
foreach ($directoryName in $forbiddenLibrariumDirectories) {
    if (Test-Path -LiteralPath (Join-Path $librariumRoot $directoryName)) {
        throw "Public package unexpectedly contains development Librarium directory '$directoryName'."
    }
}

$librariumFiles = @(Get-ChildItem -LiteralPath $librariumRoot -Recurse -File)
Write-Host "Compact public Librarium staged with $($librariumFiles.Count) loose/bundle file(s)."
$librariumSnapshotBeforeGeneration = Get-LibrariumSnapshot -Root $librariumRoot

if (Test-Path -LiteralPath (Join-Path $runtimeRoot "Data\Classic")) {
    throw "Generated Data/Classic must not be shipped as authoritative public content. It must be rebuilt from Librarium locally."
}

Write-Host "Verifying compact packaged Librarium can regenerate all enabled runtime worlds..."
$dataRoot = Join-Path $runtimeRoot "Data"
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
$fullDataRoot = [IO.Path]::GetFullPath($dataRoot)
$worldConfig = Get-Content -LiteralPath (Join-Path $runtimeRoot "Config\server.worlds.json") -Raw | ConvertFrom-Json
$enabledWorlds = @($worldConfig.worlds | Where-Object { $_.runtimeEnabled -eq $true })
if ($enabledWorlds.Count -eq 0) {
    throw "No enabled worlds were found in Config/server.worlds.json."
}

foreach ($world in $enabledWorlds) {
    $configuredOutputRoot = [IO.Path]::GetFullPath((Join-Path $runtimeRoot ([string]$world.dataRoot)))
    $dataPrefix = $fullDataRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not ($configuredOutputRoot -ieq $fullDataRoot) -and -not $configuredOutputRoot.StartsWith($dataPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "World '$($world.id)' has invalid dataRoot '$($world.dataRoot)'. Generated runtime worlds must remain under Data/."
    }

    Write-Host "  Generating $($world.id) into $configuredOutputRoot from compact staged Librarium..."
    & dotnet $compilerDll build --root $runtimeRoot --world $world.id --clean --output $configuredOutputRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Compact packaged Librarium failed to generate world '$($world.id)' (exit $LASTEXITCODE)."
    }

    $generatedRooms = Join-Path $configuredOutputRoot ([string]$world.roomsFile)
    $generatedManifest = Join-Path $configuredOutputRoot "world_manifest.json"
    if (-not (Test-Path -LiteralPath $generatedRooms) -or -not (Test-Path -LiteralPath $generatedManifest)) {
        throw "World '$($world.id)' generation did not produce required runtime files in '$configuredOutputRoot'."
    }
}

$librariumSnapshotAfterGeneration = Get-LibrariumSnapshot -Root $librariumRoot
$librariumChanges = @(Compare-Object -ReferenceObject $librariumSnapshotBeforeGeneration -DifferenceObject $librariumSnapshotAfterGeneration)
if ($librariumChanges.Count -ne 0) {
    $changeText = ($librariumChanges | Out-String).Trim()
    throw "Runtime Data generation modified the staged Librarium. Librarium must remain authoritative/read-only during generation.$([Environment]::NewLine)$changeText"
}

foreach ($forbiddenGeneratedDirectory in @("Data", "Classic", "Custom1")) {
    if (Test-Path -LiteralPath (Join-Path $librariumRoot $forbiddenGeneratedDirectory)) {
        throw "Runtime Data generation created '$forbiddenGeneratedDirectory' inside Librarium. Generated worlds must exist only under Data/."
    }
}

Write-Host "Compact Librarium regeneration test passed without mutating Librarium. Removing disposable generated Data before packaging..."
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
Write-Host "  Librarium: $bundlePath"
Write-Host ""
Write-Host "The package is self-contained and Librarium-first. The public build carries a compact authoritative RealmGate.librarium bundle, and Data/ is generated locally from it on first host start."
