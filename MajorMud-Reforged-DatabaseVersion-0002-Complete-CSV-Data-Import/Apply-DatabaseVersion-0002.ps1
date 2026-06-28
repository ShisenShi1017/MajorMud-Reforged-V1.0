$ErrorActionPreference = "Stop"
$PatchRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ContentRoot = Join-Path $PatchRoot "Content"

function Test-ProjectRoot($p) {
    return (Test-Path (Join-Path $p "Data")) -and
           (Test-Path (Join-Path $p "Docs")) -and
           (Test-Path (Join-Path $p "ImportedDatabases"))
}

$candidates = New-Object System.Collections.Generic.List[string]
$cursor = Resolve-Path $PatchRoot
for ($i = 0; $i -lt 6 -and $cursor; $i++) {
    $parent = Split-Path $cursor -Parent
    if ($parent -and (Test-Path $parent)) {
        Get-ChildItem -LiteralPath $parent -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            if (Test-ProjectRoot $_.FullName) { $candidates.Add($_.FullName) }
        }
    }
    if (Test-ProjectRoot $cursor) { $candidates.Add($cursor) }
    $cursor = $parent
}

$homeSearch = @(
    [Environment]::GetFolderPath("Desktop"),
    [Environment]::GetFolderPath("MyDocuments"),
    (Join-Path $env:USERPROFILE "Downloads")
) | Where-Object { $_ -and (Test-Path $_) }
foreach ($base in $homeSearch) {
    Get-ChildItem -LiteralPath $base -Directory -Recurse -Depth 3 -ErrorAction SilentlyContinue | ForEach-Object {
        if (Test-ProjectRoot $_.FullName) { $candidates.Add($_.FullName) }
    }
}

$unique = $candidates | Select-Object -Unique
if ($unique.Count -eq 0) {
    throw "Could not auto-locate the MajorMud Reforged project folder. Place this extracted database-version folder next to the project folder and run again."
}
if ($unique.Count -gt 1) {
    $ranked = $unique | Sort-Object { (Get-Item $_).LastWriteTime } -Descending
    $ProjectRoot = $ranked[0]
    Write-Host "Multiple project folders found. Using newest modified folder:" $ProjectRoot
} else {
    $ProjectRoot = $unique[0]
}

Write-Host "Applying Database Version 0002 to:" $ProjectRoot
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $ProjectRoot "DatabaseVersionBackup\Before-DatabaseVersion-0002-$stamp"
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

# Backup existing data folders that this version replaces. Do not touch Saves or ImportedDatabases.
$replaceDirs = @("Classes","Items","Lairs","Monsters","Races","Rooms","Shops","Spells","TextBlocks")
foreach ($dir in $replaceDirs) {
    $target = Join-Path $ProjectRoot "Data\$dir"
    if (Test-Path $target) {
        New-Item -ItemType Directory -Force -Path (Join-Path $backupRoot "Data") | Out-Null
        Move-Item -LiteralPath $target -Destination (Join-Path $backupRoot "Data\$dir") -Force
    }
}
$manifest = Join-Path $ProjectRoot "Data\_DatabaseVersionManifest.json"
if (Test-Path $manifest) {
    New-Item -ItemType Directory -Force -Path (Join-Path $backupRoot "Data") | Out-Null
    Move-Item -LiteralPath $manifest -Destination (Join-Path $backupRoot "Data\_DatabaseVersionManifest.json") -Force
}

Copy-Item -LiteralPath (Join-Path $ContentRoot "Data\*") -Destination (Join-Path $ProjectRoot "Data") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $ContentRoot "Docs\*") -Destination (Join-Path $ProjectRoot "Docs") -Recurse -Force

Write-Host "Backup created at:" $backupRoot
Write-Host "Database Version 0002 installed. Saves and ImportedDatabases were not modified."
