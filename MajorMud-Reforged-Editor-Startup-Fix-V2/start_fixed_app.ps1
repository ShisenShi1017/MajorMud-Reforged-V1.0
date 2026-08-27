$ErrorActionPreference = "Stop"
$repoFile = Join-Path $PSScriptRoot "repo_path.txt"

if (-not (Test-Path $repoFile)) {
    Write-Host "Run APPLY_EDITOR_STARTUP_FIX_V2.bat first." -ForegroundColor Yellow
    exit 2
}

$repo = ([System.IO.File]::ReadAllText($repoFile)).Trim()
$exe = Join-Path $repo "Release\MajorMudReforgedApp\MajorMudReforged.exe"

if (-not (Test-Path $exe)) {
    Write-Host "Fresh published EXE was not found:" -ForegroundColor Red
    Write-Host $exe
    Write-Host "Run APPLY_EDITOR_STARTUP_FIX_V2.bat again."
    exit 3
}

Write-Host "Starting fresh published app:" -ForegroundColor Cyan
Write-Host $exe
Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe)
exit 0
