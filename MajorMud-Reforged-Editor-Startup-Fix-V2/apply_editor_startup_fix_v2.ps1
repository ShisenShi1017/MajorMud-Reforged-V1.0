param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
$relativeTarget = "Apps\Source\Reforged.App\Views\EditorView.xaml"
$projectRelative = "Apps\Source\Reforged.App\Reforged.App.csproj"
$publishRelative = "Release\MajorMudReforgedApp"

function Test-Repo([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    return (Test-Path (Join-Path $path $relativeTarget)) -and
           (Test-Path (Join-Path $path $projectRelative))
}

function Find-Repo {
    param([string]$Requested)

    if (Test-Repo $Requested) { return (Resolve-Path $Requested).Path }

    $candidates = @(
        (Split-Path -Parent $PSScriptRoot),
        (Get-Location).Path,
        "E:\MajorMud-Reforged Github",
        "E:\MajorMud-Reforged Github(1)"
    ) | Where-Object { $_ } | Select-Object -Unique

    foreach ($candidate in $candidates) {
        if (Test-Repo $candidate) { return (Resolve-Path $candidate).Path }
    }

    # Also check one level below the folder containing this fix.
    $parent = Split-Path -Parent $PSScriptRoot
    if (Test-Path $parent) {
        foreach ($dir in Get-ChildItem -Path $parent -Directory -ErrorAction SilentlyContinue) {
            if (Test-Repo $dir.FullName) { return $dir.FullName }
        }
    }

    while ($true) {
        Write-Host ""
        Write-Host "Could not automatically locate the MajorMUD Reforged repo." -ForegroundColor Yellow
        $entered = Read-Host "Enter repo folder (example: E:\MajorMud-Reforged Github)"
        if ($entered) {
            $entered = $entered.Trim().Trim('"')
            if (Test-Repo $entered) { return (Resolve-Path $entered).Path }
        }
        Write-Host "That folder does not contain the expected Reforged.App project." -ForegroundColor Red
    }
}

$RepoRoot = Find-Repo $RepoRoot
$target = Join-Path $RepoRoot $relativeTarget
$project = Join-Path $RepoRoot $projectRelative
$publishDir = Join-Path $RepoRoot $publishRelative

Write-Host ""
Write-Host "Repo: $RepoRoot" -ForegroundColor Cyan
Write-Host "Target: $relativeTarget"

$text = [System.IO.File]::ReadAllText($target)
$alreadyFixed = $text -match 'Text="\{Binding\s+BaseRecordJson\s*,\s*Mode\s*=\s*OneWay\s*\}"'
$plainBinding = $text -match 'Text="\{Binding\s+BaseRecordJson\s*\}"'

if (-not $alreadyFixed) {
    if (-not $plainBinding) {
        Write-Host "ERROR: BaseRecordJson binding was not found in the expected form." -ForegroundColor Red
        Write-Host "No source was overwritten. Send this screen back to ChatGPT."
        exit 2
    }

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backup = "$target.pre-editor-startup-fix-v2-$stamp.bak"
    Copy-Item $target $backup -Force
    Write-Host "Backup: $backup"

    $text = [regex]::Replace(
        $text,
        'Text="\{Binding\s+BaseRecordJson\s*\}"',
        'Text="{Binding BaseRecordJson, Mode=OneWay}"'
    )
    [System.IO.File]::WriteAllText($target, $text, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Applied Mode=OneWay to BaseRecordJson." -ForegroundColor Green
} else {
    Write-Host "Source binding is already Mode=OneWay." -ForegroundColor Green
}

# Verify the exact source after writing.
$verify = [System.IO.File]::ReadAllText($target)
if ($verify -notmatch 'Text="\{Binding\s+BaseRecordJson\s*,\s*Mode\s*=\s*OneWay\s*\}"') {
    Write-Host "ERROR: Verification failed. The source file was not patched." -ForegroundColor Red
    exit 3
}
Write-Host "Verified source binding." -ForegroundColor Green

# Kill a running copy so Windows does not keep the old executable/resources locked.
Get-Process MajorMudReforged -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping running MajorMudReforged process..."
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

$appDir = Join-Path $RepoRoot "Apps\Source\Reforged.App"
foreach ($name in @("obj", "bin")) {
    $path = Join-Path $appDir $name
    if (Test-Path $path) {
        Write-Host "Removing stale $name folder..."
        Remove-Item $path -Recurse -Force
    }
}

Write-Host ""
Write-Host "[1/2] Building Reforged.App Release..." -ForegroundColor Cyan
& dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "The binding fix is applied, but the repo has another build error." -ForegroundColor Yellow
    Write-Host "Send the new build output to ChatGPT."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "[2/2] Publishing fresh Release app..." -ForegroundColor Cyan
& dotnet publish $project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build succeeded, but publish failed." -ForegroundColor Yellow
    Write-Host "Send the publish output to ChatGPT."
    exit $LASTEXITCODE
}

$exe = Join-Path $publishDir "MajorMudReforged.exe"
if (-not (Test-Path $exe)) {
    Write-Host "ERROR: Publish reported success, but the expected EXE is missing:" -ForegroundColor Red
    Write-Host $exe
    exit 4
}

# Save the resolved repo path so START_FIXED_APP.bat launches this exact build.
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot "repo_path.txt"), $RepoRoot, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "FIX COMPLETE." -ForegroundColor Green
Write-Host "Fresh published EXE:" -ForegroundColor Green
Write-Host $exe
Write-Host ""
Write-Host "IMPORTANT: Use START_FIXED_APP.bat from this fix folder for the first launch." -ForegroundColor Yellow
exit 0
