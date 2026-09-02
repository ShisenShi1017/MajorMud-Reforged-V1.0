param(
    [string]$Configuration = "Release",
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$reportDirectory = Join-Path $repoRoot "Reports\RuntimeVerification"
$jsonPath = Join-Path $reportDirectory "validator_latest.json"
$markdownPath = Join-Path $reportDirectory "validator_latest.md"
$validationProject = "Tools/ReforgedValidator/DotNet/Reforged.Validation.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET 8 SDK is required to run the Reforged validator."
}

$steps = [System.Collections.Generic.List[object]]::new()
if (-not $SkipRestore) {
    $steps.Add([pscustomobject]@{
        Name = "solution_restore"
        Arguments = @("restore", "MajorMud.Reforged.sln")
    })
}

$steps.Add([pscustomobject]@{
    Name = "solution_build"
    Arguments = @("build", "MajorMud.Reforged.sln", "--configuration", $Configuration, "--no-restore")
})
$steps.Add([pscustomobject]@{
    Name = "classic_generation"
    Arguments = @("run", "--project", "Apps/Compiler/MajorMud.Reforged.Compiler.csproj", "--configuration", $Configuration, "--no-build", "--", "build", "--world", "Classic", "--clean")
})
$steps.Add([pscustomobject]@{
    Name = "compiler_tests"
    Arguments = @("test", "Apps/Compiler.Tests/MajorMud.Reforged.Compiler.Tests.csproj", "--configuration", $Configuration, "--no-build")
})
$steps.Add([pscustomobject]@{
    Name = "core_smoke"
    Arguments = @("run", "--project", "Tests/Source/Reforged.Core.Tests/Reforged.Core.Tests.csproj", "--configuration", $Configuration, "--no-build")
})

$validationCommands = @(
    @("classic_data_audit", "--audit-classic-data"),
    @("decode_exhaustion", "--audit-decode-exhaustion"),
    @("generated_data", "--smoke-generated-data"),
    @("room_exits", "--smoke-room-exits"),
    @("npcs", "--smoke-npcs"),
    @("character_foundation", "--smoke-character-foundation"),
    @("items", "--smoke-items"),
    @("spells", "--smoke-spells"),
    @("spell_execution", "--smoke-spell-execution"),
    @("combat", "--smoke-combat"),
    @("combat_runtime", "--smoke-combat-runtime"),
    @("combat_runtime_part2", "--smoke-combat-runtime-part2"),
    @("actors_abilities", "--smoke-actors-abilities"),
    @("interaction_narrative", "--smoke-interaction-narrative"),
    @("services_economy", "--smoke-services-economy"),
    @("world_multiplayer", "--smoke-world-multiplayer"),
    @("live_gameplay", "--smoke-live-gameplay"),
    @("megamud_connection", "--smoke-megamud-connection"),
    @("classic_parity_closure", "--smoke-classic-parity-closure")
)

foreach ($validationCommand in $validationCommands) {
    $steps.Add([pscustomobject]@{
        Name = $validationCommand[0]
        Arguments = @("run", "--project", $validationProject, "--configuration", $Configuration, "--no-build", "--", $validationCommand[1])
    })
}

$steps.Add([pscustomobject]@{
    Name = "borin_character"
    Arguments = @("run", "--project", "Tests/Source/Borin.CharacterTests/Borin.CharacterTests.csproj", "--configuration", $Configuration, "--no-build")
})
$steps.Add([pscustomobject]@{
    Name = "borin_ai_provider"
    Arguments = @("run", "--project", "Tests/Source/Borin.AiProviderTests/Borin.AiProviderTests.csproj", "--configuration", $Configuration, "--no-build")
})

New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
Push-Location $repoRoot
try {
    $results = [System.Collections.Generic.List[object]]::new()
    Write-Host "Repository: $repoRoot"
    Write-Host "Running the Reforged current-health validator..."

    foreach ($step in $steps) {
        $started = Get-Date
        Write-Host ""
        Write-Host "[$($step.Name)] dotnet $($step.Arguments -join ' ')"
        $output = & dotnet @($step.Arguments) 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        $elapsed = [Math]::Round(((Get-Date) - $started).TotalSeconds, 3)
        $status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
        $results.Add([pscustomobject]@{
            name = $step.Name
            command = @("dotnet") + $step.Arguments
            status = $status
            exit_code = $exitCode
            seconds = $elapsed
            output = ($output -join [Environment]::NewLine)
        })
        Write-Host "$status $($step.Name) ($elapsed seconds)"
        if ($exitCode -ne 0) {
            break
        }
    }

    $passed = @($results | Where-Object status -eq "PASS").Count
    $allPassed = $results.Count -eq $steps.Count -and $passed -eq $results.Count
    $summary = [ordered]@{
        gate = "current_health_validator"
        timestamp_utc = [DateTime]::UtcNow.ToString("o")
        status = if ($allPassed) { "PASS" } else { "FAIL" }
        passed = $passed
        total = $results.Count
        expected = $steps.Count
        results = $results
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8

    $markdown = [System.Collections.Generic.List[string]]::new()
    $markdown.Add("# Reforged Validator")
    $markdown.Add("")
    $markdown.Add("- Status: **$($summary.status)**")
    $markdown.Add("- Passed: $passed/$($steps.Count)")
    $markdown.Add("- Timestamp: $($summary.timestamp_utc)")
    $markdown.Add("")
    $markdown.Add("| Status | Step | Seconds |")
    $markdown.Add("|---|---|---:|")
    foreach ($result in $results) {
        $markdown.Add("| $($result.status) | ``$($result.name)`` | $($result.seconds) |")
    }
    $markdown.Add("")
    $markdown.Add("Raw details: ``Reports/RuntimeVerification/validator_latest.json``")
    $markdown.Add("")
    $markdown | Set-Content -LiteralPath $markdownPath -Encoding utf8

    Write-Host ""
    Write-Host "Validator result: $($summary.status) ($passed/$($steps.Count))"
    Write-Host "Report: $markdownPath"
    if (-not $allPassed) {
        exit 1
    }
}
finally {
    Pop-Location
}
