param(
    [string]$Configuration = "Release",
    [switch]$SkipCharacterTests
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$configPath = Join-Path $repoRoot "AI\Config\borin-player.json"
$reportDirectory = Join-Path $repoRoot "Reports\Validation\Borin"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportDirectory "Borin-Memory-Sync-Validation-$timestamp.json"

New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

if (-not (Test-Path $configPath)) {
    throw "Borin player configuration was not found: $configPath"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git for Windows is required before Borin can synchronize his private memory repository."
}

$settings = Get-Content $configPath -Raw | ConvertFrom-Json
$remote = [string]$settings.memoryRepositoryRemoteUrl
$branch = [string]$settings.memoryRepositoryBranch
if ([string]::IsNullOrWhiteSpace($remote)) {
    throw "memoryRepositoryRemoteUrl is empty in borin-player.json."
}
if ([string]::IsNullOrWhiteSpace($branch)) {
    $branch = "main"
}

$previousPrompt = $env:GIT_TERMINAL_PROMPT
$previousGcm = $env:GCM_INTERACTIVE
$env:GIT_TERMINAL_PROMPT = "0"
$env:GCM_INTERACTIVE = "Never"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("BorinMemorySyncValidation-" + [guid]::NewGuid().ToString("N"))
$steps = [ordered]@{}

try {
    Write-Host "Borin private-memory sync validation"
    Write-Host "Remote: $remote"
    Write-Host "Branch: $branch"
    Write-Host "Interactive Git prompts: disabled for validation"
    Write-Host ""

    $gitVersion = (& git --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Git could not be started." }
    $steps.git = $gitVersion
    Write-Host "[PASS] $gitVersion"

    $remoteResult = (& git ls-remote --exit-code $remote "refs/heads/$branch" 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "Git cannot read the configured private memory branch non-interactively. Sign into GitHub once with Git Credential Manager (or configure SSH), then rerun this validation. Detail: $remoteResult"
    }
    $steps.readAccess = "PASS"
    Write-Host "[PASS] Private repository and branch are readable with saved Git authentication."

    & git clone --quiet --no-tags --depth 1 --branch $branch $remote $tempRoot 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "The private Borin memory repository could not be cloned into the validation sandbox."
    }
    $steps.clone = "PASS"
    Write-Host "[PASS] Private repository clone succeeded."

    # Git writes normal status text such as "Everything up-to-date" to stderr.
    # Windows PowerShell can promote that stderr text to a terminating NativeCommandError
    # when $ErrorActionPreference is Stop even though git exits with code 0.
    # Capture stdout/stderr non-terminatingly and use Git's exit code as authoritative.
    $previousNativeErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $pushOutput = & git -C $tempRoot push --dry-run origin "HEAD:refs/heads/$branch" 2>&1
        $pushExitCode = $LASTEXITCODE
        $pushResult = ($pushOutput | Out-String).Trim()
    }
    finally {
        $ErrorActionPreference = $previousNativeErrorAction
    }

    if ($pushExitCode -ne 0) {
        throw "Git can read Borin-Memory but cannot verify push access. The dry-run did not modify GitHub. Detail: $pushResult"
    }
    $steps.pushDryRun = "PASS"
    $steps.pushDryRunDetail = if ([string]::IsNullOrWhiteSpace($pushResult)) { "git exited 0" } else { $pushResult }
    Write-Host "[PASS] Push permission dry-run succeeded; GitHub was not modified."

    $readme = Join-Path $tempRoot "README.md"
    if (-not (Test-Path $readme)) {
        throw "Borin-Memory is reachable but its initialized README.md is missing from $branch."
    }
    $steps.initializedMain = "PASS"
    Write-Host "[PASS] Initialized memory repository content is present."

    if (-not $SkipCharacterTests) {
        $selfPlayScript = Join-Path $PSScriptRoot "run_self_play_validation.ps1"
        & $selfPlayScript -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "The Borin character/self-play validation failed after Git preflight."
        }
        $steps.characterTests = "PASS"
    }
    else {
        $steps.characterTests = "SKIPPED"
    }

    $result = [ordered]@{
        passed = $true
        timestamp = (Get-Date).ToString("o")
        remote = $remote
        branch = $branch
        repository = $repoRoot
        steps = $steps
        note = "The push check used git push --dry-run and did not modify the private repository. Runtime cleanup sync still requires an actual Borin cleanup boundary."
    }
    $result | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $reportPath
    Write-Host ""
    Write-Host "Borin private-memory sync validation PASSED."
    Write-Host "Report: $reportPath"
}
catch {
    $result = [ordered]@{
        passed = $false
        timestamp = (Get-Date).ToString("o")
        remote = $remote
        branch = $branch
        repository = $repoRoot
        steps = $steps
        error = $_.Exception.Message
    }
    $result | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $reportPath
    Write-Host ""
    Write-Host "Validation report: $reportPath"
    throw
}
finally {
    $env:GIT_TERMINAL_PROMPT = $previousPrompt
    $env:GCM_INTERACTIVE = $previousGcm
    if (Test-Path $tempRoot) {
        Remove-Item -Recurse -Force $tempRoot -ErrorAction SilentlyContinue
    }
}
