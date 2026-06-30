$ErrorActionPreference = "Stop"

$UpdateName = "CodeUpdate-0041-Classic-Parity-Audit-Foundation"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PayloadRoot = Join-Path $ScriptRoot "Payload"

function Find-RepoRoot {
    param([string]$Start)
    $current = Resolve-Path $Start
    while ($null -ne $current) {
        if ((Test-Path (Join-Path $current "ProjectManifest.json")) -and (Test-Path (Join-Path $current "Data"))) {
            return $current.Path
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) { break }
        $current = $parent
    }
    throw "Could not locate repository root. Run this BAT from the update folder or somewhere under the Reforged repository."
}

function Backup-File {
    param([string]$Path, [string]$RepoRoot)
    if (Test-Path $Path) {
        $relative = Resolve-Path $Path -Relative
        $backupRoot = Join-Path $RepoRoot ("Backups\CodeUpdate-0041\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
        $backupPath = Join-Path $backupRoot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupPath) | Out-Null
        Copy-Item -Force $Path $backupPath
    }
}

function Copy-Payload {
    param([string]$RepoRoot)
    Get-ChildItem -Path $PayloadRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($PayloadRoot.Length).TrimStart('\','/')
        $target = Join-Path $RepoRoot $relative
        Backup-File -Path $target -RepoRoot $RepoRoot
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -Force $_.FullName $target
        Write-Host "Copied $relative"
    }
}

function Replace-Or-Append {
    param([string]$Path, [string]$Pattern, [string]$Replacement, [string]$AppendText)
    if (!(Test-Path $Path)) { return }
    $text = Get-Content -Raw -Path $Path
    if ($text -match $Pattern) {
        $text = [regex]::Replace($text, $Pattern, $Replacement, 1)
    } elseif ($AppendText) {
        $text = $text.TrimEnd() + "`r`n`r`n" + $AppendText + "`r`n"
    }
    Set-Content -Path $Path -Value $text -Encoding UTF8
}

function Update-Docs {
    param([string]$RepoRoot)

    $readme = Join-Path $RepoRoot "README.md"
    Backup-File -Path $readme -RepoRoot $RepoRoot
    Replace-Or-Append -Path $readme -Pattern "Current Code Update:\*\* `CodeUpdate-00\d+[^`]*`" -Replacement "Current Code Update:** `CodeUpdate-0041 - Classic Parity Audit Foundation`" -AppendText ""
    Replace-Or-Append -Path $readme -Pattern "Current Priority:\*\* .*" -Replacement "Current Priority:** Apply CodeUpdate-0041, verify local build/tests, push, then verify GitHub Actions green before adding field-level Classic parsers." -AppendText ""
    Replace-Or-Append -Path $readme -Pattern "(?s)(## CodeUpdate-0039 Summary)" -Replacement "## CodeUpdate-0041 Summary`r`n`r`nCodeUpdate-0041 adds the first read-only Classic parity audit foundation. It inventories canonical `Data/` and imported reference sources, groups broad Classic content categories, and prints audit status during Server bootstrap. It does not parse proprietary binary formats yet and does not claim exact MajorMUD parity.`r`n`r`nNo data rewrite, gameplay features, combat, character creation, world simulation, networking expansion, live editor implementation, or imported-reference overwrite is added.`r`n`r`n`$1" -AppendText ""

    $version = Join-Path $RepoRoot "VERSION.md"
    Backup-File -Path $version -RepoRoot $RepoRoot
    Replace-Or-Append -Path $version -Pattern "Current Code Update: 00\d+" -Replacement "Current Code Update: 0041" -AppendText ""
    Replace-Or-Append -Path $version -Pattern "(?s)(## Reference Data Policy)" -Replacement "- CodeUpdate-0041 adds a read-only Classic parity audit foundation for canonical `Data/` and imported reference material.`r`n- CodeUpdate-0041 does not parse binary legacy formats yet and does not claim exact MajorMUD parity.`r`n`r`n`$1" -AppendText ""

    $changelog = Join-Path $RepoRoot "CHANGELOG.md"
    Backup-File -Path $changelog -RepoRoot $RepoRoot
    Replace-Or-Append -Path $changelog -Pattern "(?s)(# Changelog\s*)" -Replacement "`$1`r`n## CodeUpdate-0041 - Classic Parity Audit Foundation`r`n`r`n- Added read-only Classic parity audit inventory for canonical `Data/` and imported reference material.`r`n- Added broad category summaries for rooms, monsters, items, spells, shops, quests, classes/races, ganghouses, maps/paths, and raw legacy databases.`r`n- Attached the audit to non-gameplay Server bootstrap and startup output.`r`n- Added smoke-test coverage for audit inventory and bootstrap attachment.`r`n- No gameplay systems, combat, character creation, world simulation, networking expansion, live editor implementation, data rewrite, imported-reference overwrite, or exact parity claim is included.`r`n`r`n" -AppendText ""

    $release = Join-Path $RepoRoot "ReleaseNotes.md"
    Backup-File -Path $release -RepoRoot $RepoRoot
    Replace-Or-Append -Path $release -Pattern "(?s)^" -Replacement "# CodeUpdate-0041 - Classic Parity Audit Foundation`r`n`r`nThis update adds the first read-only Classic parity audit foundation. The Server bootstrap now inventories canonical `Data/` and imported reference material so future updates can measure how close Reforged is to Classic MajorMUD after exact parsers are added.`r`n`r`n## Boundaries`r`n`r`n- No canonical `Data/` rewrite.`r`n- No `ImportedDatabases/` overwrite.`r`n- No gameplay systems.`r`n- No exact MajorMUD parity claim yet.`r`n`r`n## Previous Release Notes`r`n" -AppendText ""

    $ai = Join-Path $RepoRoot "AI_CONTEXT.md"
    Backup-File -Path $ai -RepoRoot $RepoRoot
    Replace-Or-Append -Path $ai -Pattern "Current Code Update \| 00\d+" -Replacement "Current Code Update | 0041" -AppendText ""
    Replace-Or-Append -Path $ai -Pattern "Last Update Name \| [^|\r\n]+" -Replacement "Last Update Name | CodeUpdate-0041-Classic-Parity-Audit-Foundation " -AppendText ""
    Replace-Or-Append -Path $ai -Pattern "(?s)(## CodeUpdate-0039 Handoff)" -Replacement "## CodeUpdate-0041 Handoff`r`n`r`nCodeUpdate-0041 adds a read-only Classic parity audit foundation. It inventories canonical `Data/` and imported reference material, groups broad Classic content categories, and attaches the result to Server bootstrap. It does not parse binary legacy formats yet, does not rewrite data, and does not claim exact MajorMUD parity.`r`n`r`nNext-scope candidate:`r`n`r`n- Add the first narrow field-level parser/comparison for one approved Classic category, likely rooms or shops, after CodeUpdate-0041 is pushed and green.`r`n`r`n`$1" -AppendText ""

    $agents = Join-Path $RepoRoot "AGENTS.md"
    Backup-File -Path $agents -RepoRoot $RepoRoot
    Replace-Or-Append -Path $agents -Pattern "Current project position after CodeUpdate-00\d+" -Replacement "Current project position after CodeUpdate-0041" -AppendText ""
    Replace-Or-Append -Path $agents -Pattern "Current Code Update: `00\d+`" -Replacement "Current Code Update: `0041`" -AppendText ""
    Replace-Or-Append -Path $agents -Pattern "(?s)$" -Replacement "`r`nCodeUpdate-0041 adds the read-only Classic parity audit foundation. Future parity claims must be backed by parsed comparison evidence; imported reference material must not overwrite canonical `Data/` without reviewed approval.`r`n" -AppendText ""

    $roadmap = Join-Path $RepoRoot "Docs\Architecture\PROJECT_ROADMAP.md"
    Backup-File -Path $roadmap -RepoRoot $RepoRoot
    Replace-Or-Append -Path $roadmap -Pattern "Current Code Update: `00\d+`" -Replacement "Current Code Update: `0041`" -AppendText ""
    Replace-Or-Append -Path $roadmap -Pattern "(?s)(- CodeUpdate-0039: Server bootstrap host-tool readiness summary\.)" -Replacement "`$1`r`n- CodeUpdate-0041: Classic parity audit foundation for canonical and imported reference data inventory." -AppendText ""
    Replace-Or-Append -Path $roadmap -Pattern "(?s)(Boundaries until approved:\s*)" -Replacement "`$1" -AppendText ""

    $manifest = Join-Path $RepoRoot "ProjectManifest.json"
    Backup-File -Path $manifest -RepoRoot $RepoRoot
    if (Test-Path $manifest) {
        $json = Get-Content -Raw -Path $manifest | ConvertFrom-Json
        $json.codeUpdate = "0041"
        $json.developmentStatus.currentCodeUpdate = "0041"
        $json.developmentStatus.lastUpdateName = "CodeUpdate-0041-Classic-Parity-Audit-Foundation"
        $json.developmentStatus.nextRecommendedWork = @(
            "Push CodeUpdate-0041 and confirm GitHub Actions restore/build/test passes.",
            "After green Actions, approve one narrow field-level Classic parser/comparison category.",
            "Do not claim exact MajorMUD parity until parsed comparison evidence supports it."
        )
        $json.buildVerification = "CodeUpdate-0041 adds read-only Classic parity audit inventory for canonical Data and ImportedDatabases reference material. Local build/test verification must be run after applying this patch."
        $json | ConvertTo-Json -Depth 20 | Set-Content -Path $manifest -Encoding UTF8
    }
}

$RepoRoot = Find-RepoRoot -Start (Get-Location).Path
Write-Host "Repository root: $RepoRoot"
Copy-Payload -RepoRoot $RepoRoot
Update-Docs -RepoRoot $RepoRoot
Write-Host "CodeUpdate-0041 applied. Run local build/tests before committing."
