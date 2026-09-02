param(
    [string]$Configuration = "Release",
    [switch]$SkipCharacterTests
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$configPath = Join-Path $repoRoot "AI\Config\borin-player.json"
$reportDirectory = Join-Path $repoRoot "Reports\Validation\Borin"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDirectory "Borin-LongRun-Validation-$timestamp.json"
$memoryRoot = Join-Path $repoRoot "AI\Memory"
$diagnosticRoot = Join-Path $repoRoot "Reports\Validation\Borin\SelfPlayDiagnostics"

New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
if (-not (Test-Path $configPath)) { throw "Borin player configuration was not found: $configPath" }
$settings = Get-Content $configPath -Raw | ConvertFrom-Json

function Get-DirectoryBytes([string]$Path) {
    if (-not (Test-Path $Path)) { return 0L }
    return [long]((Get-ChildItem -LiteralPath $Path -File -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum)
}

$checks = [ordered]@{}
$checks.rawJournalRetentionDays = [int]$settings.rawGameplayJournalRetentionDays
$checks.rawJournalMaximumMegabytes = [int]$settings.rawGameplayJournalMaximumMegabytes
$checks.rawJournalMaximumFiles = [int]$settings.rawGameplayJournalMaximumFiles
$checks.diagnosticMaxFileKilobytes = [int]$settings.selfPlayDiagnosticMaxFileKilobytes
$checks.diagnosticRetentionFiles = [int]$settings.selfPlayDiagnosticRetentionFiles
$checks.memorySessionSummaryLimit = [int]$settings.memoryRepositorySessionSummaryLimit
$checks.historyCompactionEnabled = [bool]$settings.memoryRepositoryHistoryCompactionEnabled
$checks.historyCompactionIntervalCommits = [int]$settings.memoryRepositoryHistoryCompactionIntervalCommits

if ($checks.rawJournalMaximumMegabytes -lt 8) { throw "rawGameplayJournalMaximumMegabytes is below the safe normalized minimum." }
if ($checks.rawJournalMaximumFiles -lt 4) { throw "rawGameplayJournalMaximumFiles is below the safe normalized minimum." }
if ($checks.memorySessionSummaryLimit -lt 25) { throw "memoryRepositorySessionSummaryLimit is below the safe normalized minimum." }
if ($checks.historyCompactionEnabled -and $checks.historyCompactionIntervalCommits -lt 10) { throw "memoryRepositoryHistoryCompactionIntervalCommits is below the safe normalized minimum." }

if (-not $SkipCharacterTests) {
    & (Join-Path $PSScriptRoot "run_self_play_validation.ps1") -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Borin character/self-play validation failed." }
    $checks.characterTests = "PASS"
}
else {
    $checks.characterTests = "SKIPPED"
}

$result = [ordered]@{
    passed = $true
    timestamp = (Get-Date).ToString("o")
    repository = $repoRoot
    checks = $checks
    currentUsage = [ordered]@{
        localMemoryBytes = Get-DirectoryBytes $memoryRoot
        selfPlayDiagnosticsBytes = Get-DirectoryBytes $diagnosticRoot
    }
    note = "Retention is enforced during normal journal rotation/cleanup and private-memory sync. Git history compaction is lease-protected and runs only after the configured number of successful memory snapshot commits."
}
$result | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $reportPath
Write-Host "Borin long-run stability validation PASSED."
Write-Host "Report: $reportPath"
