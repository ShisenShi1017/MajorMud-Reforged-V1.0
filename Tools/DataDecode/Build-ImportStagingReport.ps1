param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-JsonRecordCount {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0
    }

    $json = Read-JsonFile $Path
    if ($json -is [array]) {
        return @($json).Count
    }

    if ($null -eq $json) {
        return 0
    }

    foreach ($propertyName in @("records", "items", "spells", "skills", "messages")) {
        if (($json.PSObject.Properties.Name -contains $propertyName) -and ($json.$propertyName -is [array])) {
            return @($json.$propertyName).Count
        }
    }

    foreach ($property in $json.PSObject.Properties) {
        if ($property.Value -is [array]) {
            return @($property.Value).Count
        }
    }

    return 1
}

function Get-CsvCount {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0
    }

    return @((Import-Csv -LiteralPath $Path)).Count
}

function Get-FileHashOrEmpty {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return ""
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Join-RepoPath {
    param([string[]]$Parts)

    $path = $ProjectRoot
    foreach ($part in $Parts) {
        $path = Join-Path $path $part
    }

    return $path
}

function Write-Csv {
    param(
        [object[]]$Rows,
        [string]$Path
    )

    $Rows | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}

$projectManifest = Read-JsonFile (Join-Path $ProjectRoot "ProjectManifest.json")

$outRoot = Join-Path $ProjectRoot "DataDecodeCheckpoints\ImportStaging"
$summaryRoot = Join-Path $outRoot "Summary"
$reportsRoot = Join-Path $outRoot "Reports"
$queueRoot = Join-Path $outRoot "Queues"

New-Item -ItemType Directory -Force -Path $summaryRoot | Out-Null
New-Item -ItemType Directory -Force -Path $reportsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $queueRoot | Out-Null

$dataRoot = Join-Path $ProjectRoot "Data"
$simRoot = Join-Path $ProjectRoot "Tools\Simulator\MajorMUD-Reforged-SimulatorPrototype"
$evidenceRoot = Join-Path $simRoot "Evidence"
$dataDecode0110Root = Join-Path $ProjectRoot "DataDecodeCheckpoints\DataDecode0110"

$evidenceFiles = @{
    rooms = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0001-MDBCSVWBBWCCExpectedRuntimeModel", "Payload", "RuntimePrep0001", "ExpectedRuntimeEntitySummary_RuntimePrep0001.csv")
    roomPopulation = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0002-WorldPopulationExtraction", "Payload", "RuntimePrep0002", "RoomPopulationMatrix_RuntimePrep0002.csv")
    navigationEdges = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0003-NavigationExitExtraction", "Payload", "RuntimePrep0003", "NavigationEdgeMatrix_RuntimePrep0003.csv")
    oneWayExits = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0003-NavigationExitExtraction", "Payload", "RuntimePrep0003", "OneWayExitCandidateMatrix_RuntimePrep0003.csv")
    shops = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0004-ShopInventoryItemAvailabilityExtraction", "Payload", "RuntimePrep0004", "ShopCatalog_RuntimePrep0004.csv")
    shopItems = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0004-ShopInventoryItemAvailabilityExtraction", "Payload", "RuntimePrep0004", "ShopInventoryMatrix_RuntimePrep0004.csv")
    monsterDrops = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0005-MonsterDropsItemRewardExtraction", "Payload", "RuntimePrep0005", "MonsterDropMatrix_RuntimePrep0005.csv")
    monsterCurrency = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0005-MonsterDropsItemRewardExtraction", "Payload", "RuntimePrep0005", "MonsterCurrencyDropMatrix_RuntimePrep0005.csv")
    npcDialogue = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0006-NPCDialogueScriptTriggerQuestInteractionExtraction", "Payload", "RuntimePrep0006", "DialogueScriptQuestCandidateMatrix_RuntimePrep0006.csv")
    combat = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0007-CombatSystemBehaviorModel", "Payload", "RuntimePrep0007", "MonsterCombatExpectationMatrix_RuntimePrep0007.csv")
    spells = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0007-CombatSystemBehaviorModel", "Payload", "RuntimePrep0007", "SpellSystemExpectationMatrix_RuntimePrep0007.csv")
    skills = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0007-CombatSystemBehaviorModel", "Payload", "RuntimePrep0007", "SkillSystemExpectationMatrix_RuntimePrep0007.csv")
    equipment = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0007-CombatSystemBehaviorModel", "Payload", "RuntimePrep0007", "EquipmentStatExpectationMatrix_RuntimePrep0007.csv")
    readiness = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0009-FinalRuntimePrepSimulatorReadinessCertification", "Payload", "RuntimePrep0009", "FinalSimulatorReadinessCertification_RuntimePrep0009.csv")
    runtimeBacklog = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0009-FinalRuntimePrepSimulatorReadinessCertification", "Payload", "RuntimePrep0009", "RuntimeValidationLaunchBacklog_RuntimePrep0009.csv")
}

$canonicalRows = foreach ($file in Get-ChildItem -LiteralPath $dataRoot -Filter "*.json" | Sort-Object Name) {
    [pscustomobject]@{
        dataFile = $file.Name
        records = Get-JsonRecordCount $file.FullName
        bytes = $file.Length
        sha256 = Get-FileHashOrEmpty $file.FullName
        importStatus = if ($file.Length -le 7 -or (Get-JsonRecordCount $file.FullName) -eq 0) { "placeholder_or_empty" } else { "populated" }
    }
}
Write-Csv -Rows @($canonicalRows) -Path (Join-Path $summaryRoot "CanonicalDataImportSurface.csv")

$evidenceRows = foreach ($key in ($evidenceFiles.Keys | Sort-Object)) {
    $path = $evidenceFiles[$key]
    [pscustomobject]@{
        evidenceKey = $key
        relativePath = $path.Substring($ProjectRoot.Length).TrimStart('\', '/')
        exists = Test-Path -LiteralPath $path
        rows = Get-CsvCount $path
        sha256 = Get-FileHashOrEmpty $path
    }
}
Write-Csv -Rows @($evidenceRows) -Path (Join-Path $summaryRoot "SimulatorEvidenceSurface.csv")

$certificationRows = @()
if (Test-Path -LiteralPath (Join-Path $dataDecode0110Root "FinalStaticCertification_DataDecode0110.csv")) {
    $certificationRows = @(Import-Csv -LiteralPath (Join-Path $dataDecode0110Root "FinalStaticCertification_DataDecode0110.csv"))
}

$metricsRows = @()
if (Test-Path -LiteralPath (Join-Path $dataDecode0110Root "DataDecode0110_Metrics.csv")) {
    $metricsRows = @(Import-Csv -LiteralPath (Join-Path $dataDecode0110Root "DataDecode0110_Metrics.csv"))
}

$metricLookup = @{}
foreach ($row in $metricsRows) {
    $metricLookup[$row.metric] = $row.value
}

$previousCodeUpdate = [string]$projectManifest.codeUpdate
$databaseVersion = [string]$projectManifest.databaseVersion
$milestone = [string]$projectManifest.milestone

$runtimeMetricsPath = Join-RepoPath @("Tools", "Simulator", "MajorMUD-Reforged-SimulatorPrototype", "Evidence", "RuntimePrep0008-ExpectedRuntimeWorldIntegration", "Payload", "RuntimePrep0008", "CumulativeRuntimePrepMetrics_RuntimePrep0008.csv")
$runtimeMetrics = @()
if (Test-Path -LiteralPath $runtimeMetricsPath) {
    $runtimeMetrics = @(Import-Csv -LiteralPath $runtimeMetricsPath)
}

function Get-RuntimeMetric {
    param(
        [string]$Checkpoint,
        [string]$Metric,
        [int]$Fallback = 0
    )

    $row = $runtimeMetrics | Where-Object { $_.checkpoint -eq $Checkpoint -and $_.metric -eq $Metric } | Select-Object -First 1
    if ($null -eq $row) {
        return $Fallback
    }

    $value = 0
    if ([int]::TryParse([string]$row.value, [ref]$value)) {
        return $value
    }

    return $Fallback
}

$roomsLoaded = Get-RuntimeMetric -Checkpoint "RuntimePrep0002" -Metric "Rooms loaded" -Fallback (Get-CsvCount $evidenceFiles.rooms)
$monstersLoaded = Get-RuntimeMetric -Checkpoint "RuntimePrep0005" -Metric "Monsters loaded" -Fallback (Get-CsvCount $evidenceFiles.combat)
$navigationEdges = Get-RuntimeMetric -Checkpoint "RuntimePrep0003" -Metric "Navigation/path edges extracted" -Fallback (Get-CsvCount $evidenceFiles.navigationEdges)
$shopsLoaded = Get-RuntimeMetric -Checkpoint "RuntimePrep0004" -Metric "Shops loaded" -Fallback (Get-CsvCount $evidenceFiles.shops)
$shopInventoryEdges = Get-RuntimeMetric -Checkpoint "RuntimePrep0004" -Metric "Inventory edges" -Fallback (Get-CsvCount $evidenceFiles.shopItems)
$monsterDropEdges = Get-RuntimeMetric -Checkpoint "RuntimePrep0005" -Metric "Monster item drop edges" -Fallback (Get-CsvCount $evidenceFiles.monsterDrops)
$currencyDropRows = Get-RuntimeMetric -Checkpoint "RuntimePrep0005" -Metric "Monsters with configured currency drops" -Fallback (Get-CsvCount $evidenceFiles.monsterCurrency)
$questProgressionCandidates = Get-RuntimeMetric -Checkpoint "RuntimePrep0006" -Metric "Quest/progression candidates" -Fallback (Get-CsvCount $evidenceFiles.npcDialogue)
$itemRecordsLoaded = Get-RuntimeMetric -Checkpoint "RuntimePrep0007" -Metric "Item/equipment records loaded from repository data" -Fallback (Get-CsvCount $evidenceFiles.equipment)
$spellRecordsLoaded = Get-RuntimeMetric -Checkpoint "RuntimePrep0007" -Metric "Spell records loaded from repository data" -Fallback (Get-CsvCount $evidenceFiles.spells)
$skillRecordsLoaded = Get-RuntimeMetric -Checkpoint "RuntimePrep0007" -Metric "Skill records loaded from repository data" -Fallback (Get-CsvCount $evidenceFiles.skills)

$queueRows = @(
    [pscustomobject]@{
        targetDataFile = "Data/rooms.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "rooms.json" }).records
        expectedEvidenceRows = $roomsLoaded
        supportingEvidence = "ExpectedRuntimeEntitySummary_RuntimePrep0001.csv; NavigationEdgeMatrix_RuntimePrep0003.csv; RoomPopulationMatrix_RuntimePrep0002.csv"
        confidence = "high_static_structure"
        recommendedAction = "Stage room identity/navigation/population mapping for reviewed import; $navigationEdges navigation edges are available as expected-model evidence."
        blockedBy = "Live runtime validation for hidden exits, scripted exits, and special transition behavior."
    },
    [pscustomobject]@{
        targetDataFile = "Data/shops.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "shops.json" }).records
        expectedEvidenceRows = $shopsLoaded
        supportingEvidence = "ShopCatalog_RuntimePrep0004.csv; ShopInventoryMatrix_RuntimePrep0004.csv; WCCShopBinaryCorroboration_RuntimePrep0004.csv"
        confidence = "high_static_structure"
        recommendedAction = "Stage shop catalog and $shopInventoryEdges inventory edges for reviewed import; keep transaction/restock behavior separate."
        blockedBy = "Runtime validation for pricing quirks, restock logic, and dynamic shop behavior."
    },
    [pscustomobject]@{
        targetDataFile = "Data/loot_tables.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "loot_tables.json" }).records
        expectedEvidenceRows = $monsterDropEdges
        supportingEvidence = "MonsterDropMatrix_RuntimePrep0005.csv; MonsterCurrencyDropMatrix_RuntimePrep0005.csv; ContainerOpenRewardMatrix_RuntimePrep0005.csv"
        confidence = "medium_static_structure"
        recommendedAction = "Stage reward/drop relationships as expected-model data; $currencyDropRows currency drop rows are available, but probability and event-order fields remain provisional."
        blockedBy = "Runtime validation for actual drop probability, currency roll order, and container/open events."
    },
    [pscustomobject]@{
        targetDataFile = "Data/quests.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "quests.json" }).records
        expectedEvidenceRows = $questProgressionCandidates
        supportingEvidence = "DialogueScriptQuestCandidateMatrix_RuntimePrep0006.csv; NPCQuestRuntimeChecklist_RuntimePrep0006.csv; message/textblock ownership projections"
        confidence = "partial"
        recommendedAction = "Keep as staging/reference only until quest trigger/state mutation evidence is stronger."
        blockedBy = "Runtime proof for trigger order, flag mutation, completion logic, and event dispatch."
    },
    [pscustomobject]@{
        targetDataFile = "Data/trainers.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "trainers.json" }).records
        expectedEvidenceRows = $skillRecordsLoaded
        supportingEvidence = "SkillSystemExpectationMatrix_RuntimePrep0007.csv; class/skill expected matrices"
        confidence = "partial"
        recommendedAction = "Stage trainer/skill availability candidates only after source rows are linked to room/NPC evidence."
        blockedBy = "Runtime validation for training rules, requirements, costs, and trainer-specific availability."
    },
    [pscustomobject]@{
        targetDataFile = "Data/items.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "items.json" }).records
        expectedEvidenceRows = $itemRecordsLoaded
        supportingEvidence = "EquipmentStatExpectationMatrix_RuntimePrep0007.csv; DataDecode0068 where spells reference items; current populated canonical items"
        confidence = "already_populated_needs_reconciliation"
        recommendedAction = "Compare and reconcile identity/field coverage before any rewrite; avoid overwriting current populated data."
        blockedBy = "Need field-level diff and confidence map."
    },
    [pscustomobject]@{
        targetDataFile = "Data/spells.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "spells.json" }).records
        expectedEvidenceRows = $spellRecordsLoaded
        supportingEvidence = "SpellSystemExpectationMatrix_RuntimePrep0007.csv; DataDecode0068 Spell Engine Proof"
        confidence = "already_populated_needs_reconciliation"
        recommendedAction = "Use spell proof to reconcile sourceId/legacy mappings and keep effect behavior marked uncertain."
        blockedBy = "Runtime validation for effects, costs, cast timing, resistance, and dispatch."
    },
    [pscustomobject]@{
        targetDataFile = "Data/monsters.json"
        currentCanonicalRecords = ($canonicalRows | Where-Object { $_.dataFile -eq "monsters.json" }).records
        expectedEvidenceRows = $monstersLoaded
        supportingEvidence = "MonsterCombatExpectationMatrix_RuntimePrep0007.csv; RoomPopulationMatrix_RuntimePrep0002.csv"
        confidence = "populated_sample_needs_import_reconciliation"
        recommendedAction = "Stage monster population/combat baseline as a reviewed diff, not a blind replacement."
        blockedBy = "Runtime validation for AI priorities, spawn timing, combat formulas, and special behavior."
    }
)
Write-Csv -Rows $queueRows -Path (Join-Path $queueRoot "CanonicalImportStagingQueue.csv")

$phoneRows = foreach ($row in $queueRows) {
    [pscustomobject]@{
        targetDataFile = $row.targetDataFile
        sourceEvidenceFile = $row.supportingEvidence
        confidence = $row.confidence
        fieldsProven = "To be filled by phone-side decode handoff with binary-backed field names only."
        fieldsUncertain = $row.blockedBy
        notes = $row.recommendedAction
    }
}
Write-Csv -Rows $phoneRows -Path (Join-Path $queueRoot "PhoneDecodeImportHandoffTemplate.csv")

$reportPath = Join-Path $reportsRoot "CodeUpdate-0051-ImportStagingReadinessReport.md"
$report = @"
# CodeUpdate-0051 Import Staging Readiness Report

## Purpose

CodeUpdate-0051 prepares a reviewed import staging surface. It does not rewrite canonical `Data/`, promote fields, change server behavior, add gameplay, or certify live runtime behavior.

## Current Baseline

- CodeUpdate-0050 is green by user report and is treated as the verified baseline for this local CodeUpdate.
- Current repository CodeUpdate before this report: $previousCodeUpdate.
- Current Database Version: $databaseVersion.
- Current Milestone: $milestone.

## Static Certification Inputs

- DataDecode0110 static certification scope: ImportedDatabases only; canonical `Data/` was excluded.
- Imported database entries checked: $($metricLookup["imported_database_entries_checked"]).
- Duplicate node keys found: $($metricLookup["duplicate_node_keys_found"]).
- Missing edge endpoints: $($metricLookup["missing_edge_endpoints"]).
- Unsupported inferences made: $($metricLookup["unsupported_inferences_made"]).
- Unresolved ownership rows carried forward: $($metricLookup["unresolved_ownership_rows_carried_forward"]).
- Unresolved static rows carried forward: $($metricLookup["unresolved_static_rows_carried_forward"]).
- Runtime validation started: $($metricLookup["runtime_validation_started"]).

## Import Readiness Summary

The evidence is ready for an import staging/diff pass. It is not ready for a blind production import.

High-confidence static staging candidates:

- Rooms/navigation identity and basic graph data.
- Shop catalog and static inventory data.
- World population placement relationships.

Medium/partial staging candidates:

- Loot/reward relationships, with probabilities and event order marked provisional.
- NPC/dialogue/quest candidates, with trigger/state mutation marked unproven.
- Trainers/skill availability, pending room/NPC/source linkage.

Already populated reconciliation candidates:

- Items.
- Spells.
- Monsters.

## Generated Files

- `Summary/CanonicalDataImportSurface.csv`
- `Summary/SimulatorEvidenceSurface.csv`
- `Queues/CanonicalImportStagingQueue.csv`
- `Queues/PhoneDecodeImportHandoffTemplate.csv`
- `ImportStagingManifest.json`

## Boundaries

- No `Data/` rewrite.
- No imported/reference deletion.
- No field promotion.
- No gameplay systems.
- No server behavior change.
- No live Worldgroup runtime validation claim.
- No OmniVerse-only promotion.
- No assumption that public IDs equal binary record indexes.

## Next Recommended Step

Use the staging queue to create a reviewed import diff for one high-confidence category first. The safest first category is rooms/navigation because canonical `Data/rooms.json` is currently a placeholder and the simulator/decode evidence has strong static coverage, while hidden/scripted exits can remain flagged uncertain.
"@

$report | Set-Content -LiteralPath $reportPath -Encoding UTF8

$manifest = [pscustomobject]@{
    name = "CodeUpdate-0051 Import Staging Readiness"
    codeUpdate = "0051"
    databaseVersion = $projectManifest.databaseVersion
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    policy = "Staging and diff reports only. No canonical Data rewrite, field promotion, server behavior change, or gameplay work."
    baseline = [pscustomobject]@{
        previousCodeUpdate = $projectManifest.codeUpdate
        previousUpdateGreen = "CodeUpdate-0050 GitHub Actions green by user report."
        milestone = $projectManifest.milestone
    }
    dataDecode0110 = [pscustomobject]@{
        importedDatabaseEntriesChecked = $metricLookup["imported_database_entries_checked"]
        duplicateNodeKeysFound = $metricLookup["duplicate_node_keys_found"]
        missingEdgeEndpoints = $metricLookup["missing_edge_endpoints"]
        unsupportedInferencesMade = $metricLookup["unsupported_inferences_made"]
        unresolvedOwnershipRowsCarriedForward = $metricLookup["unresolved_ownership_rows_carried_forward"]
        unresolvedStaticRowsCarriedForward = $metricLookup["unresolved_static_rows_carried_forward"]
        runtimeValidationStarted = $metricLookup["runtime_validation_started"]
    }
    outputs = @(
        "Summary/CanonicalDataImportSurface.csv",
        "Summary/SimulatorEvidenceSurface.csv",
        "Queues/CanonicalImportStagingQueue.csv",
        "Queues/PhoneDecodeImportHandoffTemplate.csv",
        "Reports/CodeUpdate-0051-ImportStagingReadinessReport.md"
    )
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outRoot "ImportStagingManifest.json") -Encoding UTF8

$readme = @"
# Import Staging

This folder is the reviewed import staging surface created by CodeUpdate-0051.

It compares current canonical `Data/` files with static decode and SimulatorBuild0002 expected-model evidence. It is intentionally report-only.

## Important Boundary

These files do not promote fields, rewrite canonical data, certify runtime behavior, or replace original binary evidence. They only show what is ready to stage, what needs a diff, and what remains blocked by runtime-only behavior.

## Phone Handoff

Use `Queues/PhoneDecodeImportHandoffTemplate.csv` when working from Android ChatGPT. Fill only fields proven by binary-backed evidence. Leave uncertain behavior in the uncertainty column.
"@

$readme | Set-Content -LiteralPath (Join-Path $outRoot "README.md") -Encoding UTF8

Write-Output "Import staging reports written to $outRoot"
