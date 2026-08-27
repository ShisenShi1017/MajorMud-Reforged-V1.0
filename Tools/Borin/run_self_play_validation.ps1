param(
    [string]$Configuration = "Release",
    [switch]$FullRepositoryValidation,
    [switch]$BuildMajorMudKnowledge
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$reportDirectory = Join-Path $repoRoot "Release\Logs\BorinBrain"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDirectory "Borin-SelfPlay-Validation-$timestamp.json"
$diagnosticsPath = Join-Path $reportDirectory "SelfPlayDiagnostics"
$projectPath = Join-Path $repoRoot "Apps\Source\Borin.CharacterTests\Borin.CharacterTests.csproj"

New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

Write-Host "Borin self-play validation"
Write-Host "Repository: $repoRoot"
Write-Host "Configuration: $Configuration"
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK is required. Install/enable the repository's supported .NET 8 SDK before running this validation."
}

if ($FullRepositoryValidation) {
    if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
        throw "Python is required for Tools\validator.py."
    }
    Write-Host "Running full repository validator first..."
    & python (Join-Path $repoRoot "Tools\validator.py")
    if ($LASTEXITCODE -ne 0) {
        throw "Full repository validation failed with exit code $LASTEXITCODE."
    }
    Write-Host ""
}

Write-Host "Restoring Borin character validation project..."
& dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    throw "Borin character-test restore failed with exit code $LASTEXITCODE."
}

Write-Host "Building Borin character validation project..."
& dotnet build $projectPath --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Borin character-test build failed with exit code $LASTEXITCODE."
}

$arguments = @(
    "run",
    "--project", $projectPath,
    "--configuration", $Configuration,
    "--no-build",
    "--",
    "--repository", $repoRoot,
    "--report", $reportPath
)
if ($BuildMajorMudKnowledge) {
    $arguments += "--build-majormud-knowledge"
}

Write-Host "Running deterministic Borin tests, including the integrated self-play lifecycle..."
& dotnet @arguments
$testExit = $LASTEXITCODE

Write-Host ""
Write-Host "Validation report: $reportPath"
Write-Host "Live self-play diagnostics: $diagnosticsPath"
Write-Host "The live diagnostics folder is populated only while the configured Borin player runtime actually runs."

if ($testExit -ne 0) {
    throw "Borin self-play validation failed with exit code $testExit. Review the JSON report above before live testing."
}

Write-Host "Borin self-play validation PASSED."
