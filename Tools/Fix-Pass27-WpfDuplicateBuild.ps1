[CmdletBinding()]
param(
    [string]$RepoRoot = (Get-Location).Path,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Text) {
    Write-Host "[Pass27 WPF Fix] $Text" -ForegroundColor Cyan
}

$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$AppDir = Join-Path $RepoRoot 'Apps\Source\Reforged.App'
$Project = Join-Path $AppDir 'Reforged.App.csproj'

if (-not (Test-Path $Project)) {
    throw "Reforged.App.csproj was not found at: $Project`nRun this from the MajorMud-Reforged Github repository root, or pass -RepoRoot explicitly."
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$BackupRoot = Join-Path $RepoRoot ("_Pass27_WpfDuplicateBackup_" + $stamp)
$Moved = New-Object System.Collections.Generic.List[string]

function Is-GeneratedPath([string]$FullName) {
    $relative = [System.IO.Path]::GetRelativePath($AppDir, $FullName)
    return $relative -match '^(bin|obj)[\\/]'
}

function Move-DuplicateFile([System.IO.FileInfo]$File, [string]$Reason) {
    $relative = [System.IO.Path]::GetRelativePath($AppDir, $File.FullName)
    $destination = Join-Path $BackupRoot $relative
    $destinationDir = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
    Move-Item -LiteralPath $File.FullName -Destination $destination -Force
    $Moved.Add("$relative :: $Reason")
    Write-Host "  backed up duplicate: $relative" -ForegroundColor Yellow
}

function Has-ExactClass([string]$Text, [string]$ClassName) {
    return [regex]::IsMatch($Text, "(?m)\\b(?:public\\s+|internal\\s+|protected\\s+|private\\s+)?(?:partial\\s+)?class\\s+" + [regex]::Escape($ClassName) + "\\b")
}

Write-Step "Scanning for duplicate WPF App/MainWindow source definitions."

$canonicalXaml = @{
    'Reforged.App.App'        = [System.IO.Path]::GetFullPath((Join-Path $AppDir 'App.xaml'))
    'Reforged.App.MainWindow' = [System.IO.Path]::GetFullPath((Join-Path $AppDir 'MainWindow.xaml'))
}

Get-ChildItem -LiteralPath $AppDir -Recurse -File -Filter '*.xaml' | ForEach-Object {
    if (Is-GeneratedPath $_.FullName) { return }
    $text = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($entry in $canonicalXaml.GetEnumerator()) {
        if ($text -match ('x:Class\s*=\s*["'']' + [regex]::Escape($entry.Key) + '["'']')) {
            $full = [System.IO.Path]::GetFullPath($_.FullName)
            if ($full -ne $entry.Value) {
                Move-DuplicateFile $_ ("duplicate x:Class " + $entry.Key)
            }
        }
    }
}

$canonicalCs = @{
    'App'        = [System.IO.Path]::GetFullPath((Join-Path $AppDir 'App.xaml.cs'))
    'MainWindow' = [System.IO.Path]::GetFullPath((Join-Path $AppDir 'MainWindow.xaml.cs'))
}

Get-ChildItem -LiteralPath $AppDir -Recurse -File -Filter '*.cs' | ForEach-Object {
    if (Is-GeneratedPath $_.FullName) { return }
    $full = [System.IO.Path]::GetFullPath($_.FullName)
    $text = Get-Content -LiteralPath $_.FullName -Raw
    if ($text -notmatch '(?m)^\s*namespace\s+Reforged\.App\s*(?:;|\{)') { return }

    foreach ($entry in $canonicalCs.GetEnumerator()) {
        if ((Has-ExactClass $text $entry.Key) -and $full -ne $entry.Value) {
            Move-DuplicateFile $_ ("duplicate Reforged.App." + $entry.Key + " class")
        }
    }
}

Write-Step "Removing generated WPF build output (bin/obj only)."
foreach ($generated in @((Join-Path $AppDir 'bin'), (Join-Path $AppDir 'obj'))) {
    if (Test-Path $generated) {
        Remove-Item -LiteralPath $generated -Recurse -Force
        Write-Host "  removed: $generated"
    }
}

# Verify that exactly one canonical XAML class remains for App and MainWindow.
foreach ($entry in $canonicalXaml.GetEnumerator()) {
    $matches = @(
        Get-ChildItem -LiteralPath $AppDir -Recurse -File -Filter '*.xaml' |
        Where-Object { -not (Is-GeneratedPath $_.FullName) } |
        Where-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw
            $text -match ('x:Class\s*=\s*["'']' + [regex]::Escape($entry.Key) + '["'']')
        }
    )
    if ($matches.Count -ne 1) {
        throw "Expected exactly one XAML definition for $($entry.Key), found $($matches.Count). Files: $($matches.FullName -join ', ')"
    }
}

foreach ($entry in $canonicalCs.GetEnumerator()) {
    $matches = @(
        Get-ChildItem -LiteralPath $AppDir -Recurse -File -Filter '*.cs' |
        Where-Object { -not (Is-GeneratedPath $_.FullName) } |
        Where-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw
            ($text -match '(?m)^\s*namespace\s+Reforged\.App\s*(?:;|\{)') -and (Has-ExactClass $text $entry.Key)
        }
    )
    if ($matches.Count -ne 1) {
        throw "Expected exactly one C# definition for Reforged.App.$($entry.Key), found $($matches.Count). Files: $($matches.FullName -join ', ')"
    }
}

if ($Moved.Count -gt 0) {
    Write-Step "Backed up $($Moved.Count) duplicate source file(s) to $BackupRoot"
    $Moved | Set-Content -LiteralPath (Join-Path $BackupRoot 'MOVED_FILES.txt') -Encoding UTF8
} else {
    Write-Step "No duplicate source files were found. Stale generated output was still removed."
}

if (-not $SkipBuild) {
    Write-Step "Building Reforged.App Release configuration."
    & dotnet build $Project -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
    Write-Host "`nPass 27 WPF duplicate cleanup + build completed successfully." -ForegroundColor Green
} else {
    Write-Host "`nCleanup completed. Build was skipped by request." -ForegroundColor Green
}
