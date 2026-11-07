[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Version,

    [ValidateSet("win-x64", "linux-x64")]
    [string]$RuntimeIdentifier,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$project = Join-Path $repoRoot "Apps/Source/RealmGate.Desktop/RealmGate.Desktop.csproj"
$compilerProject = Join-Path $repoRoot "Apps/Compiler/RealmGate.Compiler.csproj"
$compilerCliDll = Join-Path $repoRoot "Apps/Compiler/bin/$Configuration/net8.0/RealmGate.Compiler.dll"
$safeVersion = ($Version -replace '[^0-9A-Za-z._-]', '-')
$workRoot = Join-Path $repoRoot "Release/DesktopPackaging/$RuntimeIdentifier-$safeVersion"
$publishRoot = Join-Path $workRoot "publish"
$stageRoot = Join-Path $workRoot "stage"
$runtimeRoot = Join-Path $stageRoot "RealmGate"
$packageRoot = Join-Path $repoRoot "Release/DesktopPackages"
$packageBaseName = "RealmGate-Beta-$safeVersion-$RuntimeIdentifier"
$isWindowsPackage = $RuntimeIdentifier -eq "win-x64"
$applicationName = if ($isWindowsPackage) { "RealmGate.Desktop.exe" } else { "RealmGate.Desktop" }

# Release builds do not need Avalonia's optional product telemetry, and keeping it disabled avoids
# writing build logs outside the repository on locked-down developer and CI machines.
$env:AVALONIA_TELEMETRY_OPTOUT = "1"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    & $Command @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$Command' failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PackagedHostSmoke {
    param(
        [Parameter(Mandatory = $true)][string]$ApplicationPath,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $ApplicationPath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.Arguments = "--smoke-packaged-host-first-run"
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Packaged first-run Host smoke could not be started."
    }

    try {
        if (-not $process.WaitForExit(600000)) {
            $process.Kill()
            throw "Packaged first-run Host smoke exceeded 10 minutes."
        }
        if ($process.ExitCode -ne 0) {
            throw "Packaged first-run Host smoke failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}

function Copy-RuntimeFile {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $source = Join-Path $repoRoot $RelativePath
    $destination = Join-Path $runtimeRoot $RelativePath
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required runtime source was not found: '$RelativePath'."
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

function Get-DirectorySnapshot {
    param([Parameter(Mandatory = $true)][string]$Root)

    return @(
        Get-ChildItem -LiteralPath $Root -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                $relative = (($_.FullName.Substring($Root.Length)) -replace '^[\\/]+', '') -replace '\\', '/'
                $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                "$relative|$hash"
            }
    )
}

function Write-TesterInstructions {
    $instructionsPath = Join-Path $runtimeRoot "START-HERE.txt"
    if ($isWindowsPackage) {
        $instructions = @"
RealmGate Windows Preview $Version

1. Keep every extracted file and folder together.
2. Run RealmGate.Desktop.exe.
3. Open Server, start the local Host, then connect from Play.

This package is self-contained; a separate .NET installation is not required.
The first Host start generates disposable world data from the packaged Librarium and can take longer than later starts.
This public tester package does not contain Borin or private AI files.
"@
    }
    else {
        $instructions = @"
RealmGate Linux Preview $Version

1. Extract the entire archive.
2. Open a terminal in the extracted RealmGate folder.
3. Run: chmod +x RealmGate.Desktop Start-RealmGate.sh
4. Run: ./Start-RealmGate.sh
5. Open Server, start the local Host, then connect from Play.

This package is self-contained; a separate .NET installation is not required.
The first Host start generates disposable world data from the packaged Librarium and can take longer than later starts.
Secure credential storage uses Linux Secret Service. Install your distribution's secret-tool/libsecret package only if you need saved secure credentials.
This public tester package does not contain Borin or private AI files.

Please report the Linux distribution and version, desktop environment, whether the window opened, whether the Host started, and any text shown in Server Logs.
"@
    }

    Set-Content -LiteralPath $instructionsPath -Value $instructions -Encoding UTF8
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "RealmGate Avalonia project was not found at '$project'."
}

foreach ($directory in @($workRoot, $packageRoot)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null

Write-Host "Publishing self-contained RealmGate desktop ($RuntimeIdentifier)..."
Invoke-Checked -Command dotnet -ArgumentList @(
    "publish",
    $project,
    "--configuration", $Configuration,
    "--runtime", $RuntimeIdentifier,
    "--self-contained", "true",
    "-p:NuGetAudit=false",
    "-p:PublishSingleFile=false",
    "--output", $publishRoot
)

$publishedApplication = Join-Path $publishRoot $applicationName
if (-not (Test-Path -LiteralPath $publishedApplication)) {
    throw "Published application was not found at '$publishedApplication'."
}

$forbiddenPublishMatches = @(
    Get-ChildItem -LiteralPath $publishRoot -Recurse -Force |
        Where-Object { $_.FullName -match '(?i)(Borin|LLamaSharp|NvidiaApiKey|LivingForge)' }
)
if ($forbiddenPublishMatches.Count -ne 0) {
    $paths = ($forbiddenPublishMatches | Select-Object -ExpandProperty FullName) -join [Environment]::NewLine
    throw "Public desktop publish contains private/Borin files:$([Environment]::NewLine)$paths"
}

Write-Host "Staging compact Librarium-first runtime..."
Copy-Item -Path (Join-Path $publishRoot "*") -Destination $runtimeRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "ProjectManifest.json") -Destination $runtimeRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "Config") -Destination (Join-Path $runtimeRoot "Config") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "ThirdPartyLicenses") -Destination (Join-Path $runtimeRoot "ThirdPartyLicenses") -Recurse -Force

# Server bootstrap deliberately checks the product-root layout. Runtime packages keep these
# directories without shipping the development source trees.
foreach ($directoryName in @("Apps", "Core", "Data", "Docs", "Reports", "Bin", "Saves")) {
    New-Item -ItemType Directory -Path (Join-Path $runtimeRoot $directoryName) -Force | Out-Null
}

$librariumRoot = Join-Path $runtimeRoot "Librarium"
New-Item -ItemType Directory -Path $librariumRoot -Force | Out-Null
$compilerDll = Join-Path $runtimeRoot "RealmGate.Compiler.dll"
if (-not (Test-Path -LiteralPath $compilerDll)) {
    throw "RealmGate.Compiler.dll was not staged by the Avalonia publish."
}

# Applications reference the compiler as a library so its command-line apphost does not collide
# with the desktop app during self-contained publish. Build the repository CLI separately for the
# release-time bundle/regeneration proof; the installed app uses the staged library directly.
Invoke-Checked -Command dotnet -ArgumentList @(
    "build", $compilerProject,
    "--configuration", $Configuration,
    "--no-restore",
    "-p:NuGetAudit=false"
)
if (-not (Test-Path -LiteralPath $compilerCliDll)) {
    throw "RealmGate compiler CLI was not produced at '$compilerCliDll'."
}

$bundlePath = Join-Path $librariumRoot "RealmGate.librarium"
Invoke-Checked -Command dotnet -ArgumentList @($compilerCliDll, "bundle", "--root", $repoRoot, "--output", $bundlePath)
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "Compact Librarium bundle was not produced at '$bundlePath'."
}

$roomDescriptionsSource = Join-Path $repoRoot "Librarium/Objects/room_descriptions"
$roomDescriptionsDestination = Join-Path $librariumRoot "Objects/room_descriptions"
if (-not (Test-Path -LiteralPath $roomDescriptionsSource)) {
    throw "Promoted Librarium room descriptions were not found."
}
New-Item -ItemType Directory -Path $roomDescriptionsDestination -Force | Out-Null
Get-ChildItem -LiteralPath $roomDescriptionsSource -File -Filter "*.json" |
    Where-Object { $_.Name -match '(?i)(_records\.json$|_records_)' } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $roomDescriptionsDestination $_.Name) -Force
    }

foreach ($relativePath in @(
    "Librarium/Objects/textblocks/textblock_records.json",
    "Librarium/Objects/native_textblock_payloads/native_textblock_payload_records.json",
    "Librarium/Objects/items/item_records.json",
    "Librarium/Objects/quests/quest_walkthrough_guides.txt",
    "Librarium/Objects/character_foundation/native_runtime_promoted_0137.json",
    "Librarium/NativeEvidence/classic_native_room_spawn_index.br64",
    "Librarium/NativeEvidence/classic_native_monster_group_index.br64",
    "Librarium/NativeEvidence/classic_native_room_local_caps.br64"
)) {
    Copy-RuntimeFile -RelativePath $relativePath
}

# Public tester builds start with a clean derived world. Testers may create or import their own
# Custom1 changes locally; repository-local private overrides are never distributed.
$customOverrides = Join-Path $librariumRoot "WorldOverrides/Custom1"
New-Item -ItemType Directory -Path $customOverrides -Force | Out-Null

$requiredPaths = @(
    $applicationName,
    "RealmGate.Compiler.dll",
    "RealmGate.Server.Runtime.dll",
    "ProjectManifest.json",
    "Config/server.worlds.json",
    "Librarium/RealmGate.librarium",
    "Librarium/Objects/room_descriptions",
    "Librarium/Objects/textblocks/textblock_records.json",
    "Librarium/Objects/native_textblock_payloads/native_textblock_payload_records.json",
    "Librarium/Objects/items/item_records.json",
    "Librarium/Objects/quests/quest_walkthrough_guides.txt",
    "Librarium/Objects/character_foundation/native_runtime_promoted_0137.json",
    "Librarium/NativeEvidence/classic_native_room_spawn_index.br64",
    "Librarium/NativeEvidence/classic_native_monster_group_index.br64",
    "Librarium/NativeEvidence/classic_native_room_local_caps.br64",
    "Librarium/WorldOverrides/Custom1"
)
foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $runtimeRoot $relativePath))) {
        throw "Tester runtime staging is missing '$relativePath'."
    }
}

$librariumSnapshotBeforeGeneration = Get-DirectorySnapshot -Root $librariumRoot
$worldConfig = Get-Content -LiteralPath (Join-Path $runtimeRoot "Config/server.worlds.json") -Raw | ConvertFrom-Json
$enabledWorlds = @($worldConfig.worlds | Where-Object { $_.runtimeEnabled -eq $true })
if ($enabledWorlds.Count -eq 0) {
    throw "No enabled worlds were found in Config/server.worlds.json."
}

Write-Host "Verifying packaged Librarium regeneration..."
foreach ($world in $enabledWorlds) {
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $runtimeRoot ([string]$world.dataRoot)))
    $allowedDataRoot = [IO.Path]::GetFullPath((Join-Path $runtimeRoot "Data"))
    $allowedPrefix = $allowedDataRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not ($outputRoot -ieq $allowedDataRoot) -and -not $outputRoot.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "World '$($world.id)' has an invalid dataRoot outside Data/: '$($world.dataRoot)'."
    }

    Invoke-Checked -Command dotnet -ArgumentList @(
        $compilerCliDll,
        "build",
        "--root", $runtimeRoot,
        "--world", [string]$world.id,
        "--clean",
        "--output", $outputRoot
    )
    foreach ($requiredWorldFile in @([string]$world.roomsFile, "world_manifest.json", "character_foundation.json")) {
        if (-not (Test-Path -LiteralPath (Join-Path $outputRoot $requiredWorldFile))) {
            throw "World '$($world.id)' generation did not produce '$requiredWorldFile'."
        }
    }

    $foundation = Get-Content -LiteralPath (Join-Path $outputRoot "character_foundation.json") -Raw | ConvertFrom-Json
    if ([string]$foundation.authority -ne "Librarium" -or
        @($foundation.races).Count -lt 1 -or
        @($foundation.classes).Count -lt 1 -or
        $null -eq $foundation.character_creation_rules) {
        throw "World '$($world.id)' produced an incomplete character foundation."
    }
}

$librariumSnapshotAfterGeneration = Get-DirectorySnapshot -Root $librariumRoot
$librariumChanges = @(Compare-Object -ReferenceObject $librariumSnapshotBeforeGeneration -DifferenceObject $librariumSnapshotAfterGeneration)
if ($librariumChanges.Count -ne 0) {
    throw "Runtime generation modified the staged Librarium."
}

# Generated Data is intentionally excluded. The embedded Host reproduces it locally on first run.
Get-ChildItem -LiteralPath (Join-Path $runtimeRoot "Data") -Force | Remove-Item -Recurse -Force

Write-Host "Verifying packaged first-run Host generation from an empty Data folder..."
Invoke-PackagedHostSmoke `
    -ApplicationPath (Join-Path $runtimeRoot $applicationName) `
    -WorkingDirectory $runtimeRoot

# The first-run smoke proves the installed application can regenerate and start its Host. Keep the
# public archive Librarium-first by removing the disposable generated output once more.
Get-ChildItem -LiteralPath (Join-Path $runtimeRoot "Data") -Force | Remove-Item -Recurse -Force

Write-TesterInstructions
if (-not $isWindowsPackage) {
    $launcherPath = Join-Path $runtimeRoot "Start-RealmGate.sh"
    $launcher = "#!/usr/bin/env sh`nSCRIPT_DIR=`$(CDPATH= cd -- `"`$(dirname -- `"`$0`")`" && pwd)`nexec `"`$SCRIPT_DIR/RealmGate.Desktop`" `"`$@`"`n"
    [IO.File]::WriteAllText($launcherPath, $launcher, [Text.UTF8Encoding]::new($false))
    Invoke-Checked -Command chmod -ArgumentList @("+x", (Join-Path $runtimeRoot "RealmGate.Desktop"), $launcherPath)
}

Write-Host "Creating tester archive..."
if ($isWindowsPackage) {
    $packagePath = Join-Path $packageRoot "$packageBaseName.zip"
    Compress-Archive -LiteralPath $runtimeRoot -DestinationPath $packagePath -CompressionLevel Optimal -Force
}
else {
    $packagePath = Join-Path $packageRoot "$packageBaseName.tar.gz"
    Invoke-Checked -Command tar -ArgumentList @("-C", $stageRoot, "-czf", $packagePath, "RealmGate")
}

if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Tester archive was not produced."
}

$checksum = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$packagePath.sha256"
Set-Content -LiteralPath $checksumPath -Value "$checksum  $([IO.Path]::GetFileName($packagePath))" -Encoding ASCII

Write-Host ""
Write-Host "RealmGate desktop tester package is ready:"
Write-Host "  Package:  $packagePath"
Write-Host "  Checksum: $checksumPath"
Write-Host "  Runtime:  self-contained, Borin-free, Librarium-first"
