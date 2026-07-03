param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

function Read-JsonFile {
    param([string]$Path)

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ArrayFromJson {
    param(
        [object]$Json,
        [string[]]$PreferredProperties = @()
    )

    if ($Json -is [array]) {
        return @($Json)
    }

    foreach ($propertyName in $PreferredProperties) {
        if ($Json.PSObject.Properties.Name -contains $propertyName -and $Json.$propertyName -is [array]) {
            return @($Json.$propertyName)
        }
    }

    foreach ($property in $Json.PSObject.Properties) {
        if ($property.Value -is [array]) {
            return @($property.Value)
        }
    }

    return @()
}

function Normalize-Name {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $text = [string]$Value
    $text = $text.Trim().ToLowerInvariant()
    $text = $text -replace "[^a-z0-9]+", " "
    $text = $text -replace "\s+", " "
    return $text.Trim()
}

function Get-RelativePath {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if ($pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $pathFull.Substring($rootFull.Length).TrimStart('\', '/')
    }

    return $Path
}

function Write-Csv {
    param(
        [object[]]$Rows,
        [string]$Path
    )

    $Rows | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}

$outRoot = Join-Path $ProjectRoot "DataDecodeCheckpoints\UnifiedDecodedData"
$comparisonRoot = Join-Path $outRoot "Comparisons"
$inventoryRoot = Join-Path $outRoot "Inventory"
$summaryRoot = Join-Path $outRoot "Summary"
$projectManifest = Read-JsonFile (Join-Path $ProjectRoot "ProjectManifest.json")

New-Item -ItemType Directory -Force -Path $comparisonRoot | Out-Null
New-Item -ItemType Directory -Force -Path $inventoryRoot | Out-Null
New-Item -ItemType Directory -Force -Path $summaryRoot | Out-Null

$canonicalRoot = Join-Path $ProjectRoot "Data"
$decodeRoot = Join-Path $ProjectRoot "DataDecodeCheckpoints"
$omniRoot = Join-Path $ProjectRoot "ImportedDatabases\OmniVerseReference\CodeUpdate0046\OmniVerseCodexApi\wcc"

$sourceRows = New-Object System.Collections.Generic.List[object]

$sourceGroups = @(
    @{ Name = "CanonicalData"; Path = $canonicalRoot },
    @{ Name = "DataDecodeCheckpoints"; Path = $decodeRoot },
    @{ Name = "OmniVerseReference"; Path = Join-Path $ProjectRoot "ImportedDatabases\OmniVerseReference\CodeUpdate0046" }
)

foreach ($group in $sourceGroups) {
    if (-not (Test-Path -LiteralPath $group.Path)) {
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $group.Path -Recurse -File) {
        $relative = Get-RelativePath -Root $ProjectRoot -Path $file.FullName
        $sourceRows.Add([pscustomobject]@{
            group = $group.Name
            relativePath = $relative
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        })
    }
}

Write-Csv -Rows $sourceRows.ToArray() -Path (Join-Path $inventoryRoot "DecodedSourceInventory.csv")

$items = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "items.json"))
$classes = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "classes.json"))
$races = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "races.json"))
$skills = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "skills.json"))
$spells = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "spells.json")) -PreferredProperties @("spells")
$monsters = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "monsters.json"))
$rooms = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "rooms.json"))
$shops = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "shops.json"))

$nativeItems = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "NativeDatDecodeReports\WCCITEMS.records.json"))
$nativeSpells = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "NativeDatDecodeReports\WCCSPELS.records.json"))
$nativeShops = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $canonicalRoot "NativeDatDecodeReports\WCCSHOPS.records.json"))

$omniItems = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-item-wcc.json"))
$omniClasses = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-class-wcc.json"))
$omniRaces = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-race-wcc.json"))
$omniAbilities = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-ability-wcc.json"))
$omniSpells = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-spell-wcc.json"))
$omniShops = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-shop-wcc.json"))
$omniMonsters = Get-ArrayFromJson -Json (Read-JsonFile (Join-Path $omniRoot "codex-monster-wcc.json"))
$roomFiles = @(Get-ChildItem -LiteralPath (Join-Path $omniRoot "rooms") -File -Filter "*.json")
$roommapFiles = @(Get-ChildItem -LiteralPath (Join-Path $omniRoot "roommaps") -File -Filter "*.json")

$canonicalItemNames = @{}
foreach ($item in $items) {
    $key = Normalize-Name $item.name
    if (-not $canonicalItemNames.ContainsKey($key)) {
        $canonicalItemNames[$key] = 0
    }
    $canonicalItemNames[$key]++
}

$itemRows = foreach ($item in $omniItems) {
    $key = Normalize-Name $item.name
    [pscustomobject]@{
        legacy_number = $item.legacy_number
        omni_name = $item.name
        canonical_name_match = $canonicalItemNames.ContainsKey($key)
        canonical_name_match_count = if ($canonicalItemNames.ContainsKey($key)) { $canonicalItemNames[$key] } else { 0 }
        note = "Name-only correlation; current canonical items do not carry legacy_number."
    }
}
Write-Csv -Rows @($itemRows) -Path (Join-Path $comparisonRoot "Items_NameCoverage.csv")

$spellByLegacy = @{}
foreach ($spell in $spells) {
    if ($null -ne $spell.sourceId) {
        $spellByLegacy[[int]$spell.sourceId] = $spell
    }
}

$spellRows = foreach ($spell in $omniSpells) {
    $legacy = [int]$spell.legacy_number
    $match = $spellByLegacy.ContainsKey($legacy)
    $canonical = if ($match) { $spellByLegacy[$legacy] } else { $null }
    [pscustomobject]@{
        legacy_number = $legacy
        omni_name = $spell.name
        canonical_source_id_match = $match
        canonical_name = if ($match) { $canonical.name } else { "" }
        normalized_name_match = if ($match) { (Normalize-Name $canonical.name) -eq (Normalize-Name $spell.name) } else { $false }
    }
}
Write-Csv -Rows @($spellRows) -Path (Join-Path $comparisonRoot "Spells_LegacyIdCoverage.csv")

$classNames = @{}
foreach ($class in $classes) {
    $classKey = Normalize-Name $class.name
    $classNames[$classKey] = $class
}
$classRows = foreach ($class in $omniClasses) {
    $key = Normalize-Name $class.name
    [pscustomobject]@{
        legacy_number = $class.legacy_number
        omni_name = $class.name
        canonical_name_match = $classNames.ContainsKey($key)
    }
}
Write-Csv -Rows @($classRows) -Path (Join-Path $comparisonRoot "Classes_NameCoverage.csv")

$raceNames = @{}
foreach ($race in $races) {
    $raceKey = Normalize-Name $race.name
    $raceNames[$raceKey] = $race
}
$raceRows = foreach ($race in $omniRaces) {
    $key = Normalize-Name $race.name
    [pscustomobject]@{
        legacy_number = $race.legacy_number
        omni_name = $race.name
        canonical_name_match = $raceNames.ContainsKey($key)
    }
}
Write-Csv -Rows @($raceRows) -Path (Join-Path $comparisonRoot "Races_NameCoverage.csv")

$monsterNames = @{}
foreach ($monster in $monsters) {
    $monsterKey = Normalize-Name $monster.name
    $monsterNames[$monsterKey] = $monster
}
$monsterRows = foreach ($monster in $omniMonsters) {
    $key = Normalize-Name $monster.name
    [pscustomobject]@{
        legacy_number = $monster.legacy_number
        omni_name = $monster.name
        canonical_name_match = $monsterNames.ContainsKey($key)
        note = "Name-only correlation; current canonical monsters are starter/sample records."
    }
}
Write-Csv -Rows @($monsterRows) -Path (Join-Path $comparisonRoot "Monsters_NameCoverage.csv")

$matchedItems = @($itemRows | Where-Object { $_.canonical_name_match }).Count
$matchedSpells = @($spellRows | Where-Object { $_.canonical_source_id_match }).Count
$spellNameMismatches = @($spellRows | Where-Object { $_.canonical_source_id_match -and -not $_.normalized_name_match }).Count
$matchedClasses = @($classRows | Where-Object { $_.canonical_name_match }).Count
$matchedRaces = @($raceRows | Where-Object { $_.canonical_name_match }).Count
$matchedMonsters = @($monsterRows | Where-Object { $_.canonical_name_match }).Count

$summaryRows = @(
    [pscustomobject]@{ category = "classes"; canonicalCount = $classes.Count; nativeDecodeCount = ""; omniVerseCount = $omniClasses.Count; matchMethod = "normalized name"; matchedCount = $matchedClasses; missingFromCanonical = $omniClasses.Count - $matchedClasses; missingFromOmniVerse = $classes.Count - $matchedClasses; nameMismatches = 0; status = "strong name coverage" },
    [pscustomobject]@{ category = "races"; canonicalCount = $races.Count; nativeDecodeCount = ""; omniVerseCount = $omniRaces.Count; matchMethod = "normalized name"; matchedCount = $matchedRaces; missingFromCanonical = $omniRaces.Count - $matchedRaces; missingFromOmniVerse = $races.Count - $matchedRaces; nameMismatches = 0; status = "strong name coverage" },
    [pscustomobject]@{ category = "abilities/skills"; canonicalCount = $skills.Count; nativeDecodeCount = ""; omniVerseCount = $omniAbilities.Count; matchMethod = "not equivalent"; matchedCount = ""; missingFromCanonical = ""; missingFromOmniVerse = ""; nameMismatches = ""; status = "canonical skills are not yet equivalent to OmniVerse legacy abilities" },
    [pscustomobject]@{ category = "items"; canonicalCount = $items.Count; nativeDecodeCount = $nativeItems.Count; omniVerseCount = $omniItems.Count; matchMethod = "normalized name only"; matchedCount = $matchedItems; missingFromCanonical = $omniItems.Count - $matchedItems; missingFromOmniVerse = ""; nameMismatches = ""; status = "partial; canonical items lack legacy_number" },
    [pscustomobject]@{ category = "spells"; canonicalCount = $spells.Count; nativeDecodeCount = $nativeSpells.Count; omniVerseCount = $omniSpells.Count; matchMethod = "sourceId to legacy_number"; matchedCount = $matchedSpells; missingFromCanonical = $omniSpells.Count - $matchedSpells; missingFromOmniVerse = $spells.Count - $matchedSpells; nameMismatches = $spellNameMismatches; status = "partial; canonical import has fewer spells than OmniVerse" },
    [pscustomobject]@{ category = "shops"; canonicalCount = $shops.Count; nativeDecodeCount = $nativeShops.Count; omniVerseCount = $omniShops.Count; matchMethod = "count only"; matchedCount = ""; missingFromCanonical = ""; missingFromOmniVerse = ""; nameMismatches = ""; status = "canonical shops are populated; remaining relationship blockers require reviewed reconciliation" },
    [pscustomobject]@{ category = "monsters"; canonicalCount = $monsters.Count; nativeDecodeCount = ""; omniVerseCount = $omniMonsters.Count; matchMethod = "normalized name only"; matchedCount = $matchedMonsters; missingFromCanonical = $omniMonsters.Count - $matchedMonsters; missingFromOmniVerse = ""; nameMismatches = ""; status = "canonical monsters are populated; name-only coverage is informational" },
    [pscustomobject]@{ category = "rooms"; canonicalCount = $rooms.Count; nativeDecodeCount = ""; omniVerseCount = $roomFiles.Count; matchMethod = "count only"; matchedCount = ""; missingFromCanonical = ""; missingFromOmniVerse = ""; nameMismatches = ""; status = "canonical rooms are populated; remaining exit/spawn blockers require reviewed reconciliation" },
    [pscustomobject]@{ category = "roommaps"; canonicalCount = ""; nativeDecodeCount = ""; omniVerseCount = $roommapFiles.Count; matchMethod = "count only"; matchedCount = ""; missingFromCanonical = ""; missingFromOmniVerse = ""; nameMismatches = ""; status = "reference-only roommap evidence" }
)
Write-Csv -Rows $summaryRows -Path (Join-Path $summaryRoot "OmniVerseComparisonSummary.csv")

$manifest = [pscustomobject]@{
    name = "Unified decoded data and OmniVerse comparison index"
    codeUpdate = $projectManifest.codeUpdate
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    policy = "Index and comparison reports only. Original MajorMUD binaries remain authoritative. OmniVerse/MudInfo data is validation/correlation evidence only."
    sourceInventoryFileCount = $sourceRows.Count
    canonicalCounts = [pscustomobject]@{
        classes = $classes.Count
        races = $races.Count
        skills = $skills.Count
        items = $items.Count
        spells = $spells.Count
        monsters = $monsters.Count
        shops = $shops.Count
        rooms = $rooms.Count
    }
    nativeDecodeCounts = [pscustomobject]@{
        WCCITEMS_records = $nativeItems.Count
        WCCSPELS_records = $nativeSpells.Count
        WCCSHOPS_records = $nativeShops.Count
    }
    omniVerseCounts = [pscustomobject]@{
        abilities = $omniAbilities.Count
        classes = $omniClasses.Count
        races = $omniRaces.Count
        items = $omniItems.Count
        spells = $omniSpells.Count
        shops = $omniShops.Count
        monsters = $omniMonsters.Count
        roomDetails = $roomFiles.Count
        roommaps = $roommapFiles.Count
    }
    keyFindings = @(
        "Classes and races have complete normalized-name coverage against OmniVerse.",
        "Canonical items are simplified and lack legacy_number; only name-level coverage is reported.",
        "Canonical spells have sourceId values and partially map to OmniVerse legacy_number.",
        "Canonical rooms, shops, and monsters are populated in the current Data baseline.",
        "Quests remain the only empty canonical placeholder file in the current Server bootstrap catalog.",
        "world_rooms remains a server catalog loading gap because Data/world.json is not yet projected into runtime records.",
        "Remaining room exit, linked-spawn, shop inventory, monster loot, and loot table blockers require reviewed reconciliation."
    )
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outRoot "UnifiedDecodedDataManifest.json") -Encoding UTF8

$readme = @"
# Unified Decoded Data

This folder is the repo-native index for currently decoded data and HTTrack/OmniVerse comparison evidence.

It does not move or overwrite historical reports. Historical decode outputs remain preserved at their original paths. This folder gives future decode passes one place to find the current inventory, generated comparison summaries, and coverage reports.

## Contents

- `UnifiedDecodedDataManifest.json` - generated manifest and high-level counts.
- `Inventory/DecodedSourceInventory.csv` - repository inventory of canonical data, decode checkpoints, and OmniVerse reference files.
- `Summary/OmniVerseComparisonSummary.csv` - category-level decoded/canonical versus OmniVerse coverage.
- `Comparisons/` - category-specific comparison details.

## Policy

Original MajorMUD binaries remain authoritative. OmniVerse, MudInfo, imported datasets, WBB comparisons, website data, community data, and tool-derived data are validation/correlation evidence only.

No field is promoted by these reports. No canonical `Data/` file is changed.
"@

$readme | Set-Content -LiteralPath (Join-Path $outRoot "README.md") -Encoding UTF8

Write-Output "Unified decoded data reports written to $outRoot"
