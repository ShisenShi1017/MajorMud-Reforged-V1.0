param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $ProjectRoot 'Simulator\DataSimVer\0017\ItemVisibilityStateMatrix.csv'
$roomsRoot = Join-Path $ProjectRoot 'Data\Classic\rooms.parts'
$itemsPath = Join-Path $ProjectRoot 'Data\Classic\items.json'
$outputRoot = Join-Path $ProjectRoot 'Librarium\Objects\room_item_placements'
$outputPath = Join-Path $outputRoot 'room_item_placement_records_0139.json'
$reportPath = Join-Path $ProjectRoot 'Reports\RuntimeVerification\room_item_placement_promotion.json'

$roomIndex = @{}
Get-ChildItem -LiteralPath $roomsRoot -Filter '*.json' | ForEach-Object {
    $catalog = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    foreach ($room in $catalog.records) {
        $key = (([string]$room.room_number) + '|' + ([string]$room.name).Trim()).ToLowerInvariant()
        if (-not $roomIndex.ContainsKey($key)) {
            $roomIndex[$key] = [System.Collections.ArrayList]::new()
        }
        [void]$roomIndex[$key].Add($room)
    }
}

$items = (Get-Content -LiteralPath $itemsPath -Raw | ConvertFrom-Json).records
$itemIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($item in $items) {
    [void]$itemIds.Add([string]$item.legacy_id)
}

$accepted = [System.Collections.Generic.List[object]]::new()
$rejected = [System.Collections.Generic.List[object]]::new()
$ordinal = 0
foreach ($row in (Import-Csv -LiteralPath $sourcePath)) {
    $ordinal++
    $numeric = [regex]::Match($row.item_legacy, '\d+')
    $legacyId = if ($numeric.Success) { $numeric.Value } else { '' }
    $key = (([string]$row.room_number) + '|' + $row.room_name.Trim()).ToLowerInvariant()
    $matches = @(if ($roomIndex.ContainsKey($key)) { $roomIndex[$key] | ForEach-Object { $_ } })
    $itemResolved = $itemIds.Contains($legacyId)
    if ($matches.Count -ne 1 -or -not $itemResolved) {
        $rejected.Add([ordered]@{
            source_row = $ordinal
            room_number = [int]$row.room_number
            room_name = $row.room_name
            item_legacy_id = $legacyId
            room_match_count = $matches.Count
            item_resolved = $itemResolved
            reason = if ($matches.Count -ne 1) { 'map_qualified_room_not_uniquely_resolved' } else { 'item_not_resolved' }
        })
        continue
    }

    $room = $matches[0]
    $hidden = $row.visibility_state -eq 'hidden_searchable_item'
    $accepted.Add([ordered]@{
        id = "room-item-placement.$($room.id).item.$legacyId.$ordinal"
        object_type = 'room_item_placement'
        world_scope = @('classic')
        room_id = [string]$room.id
        map_number = [int]$room.map_number
        room_number = [int]$room.room_number
        room_name = [string]$room.name
        item_id = "item.$legacyId"
        item_legacy_id = [int]$legacyId
        quantity = 1
        visibility = if ($hidden) { 'hidden_searchable' } else { 'visible' }
        search_required = $hidden
        runtime_disposition = 'generated_static_room_item'
        confidence = 'exact_room_number_name_and_item_resolution'
        evidence_refs = @([ordered]@{
            path = 'Simulator/DataSimVer/0017/ItemVisibilityStateMatrix.csv'
            source_row = $ordinal
            direct_data_field = $row.direct_data_field
        })
    })
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$catalog = [ordered]@{
    authority = 'Librarium'
    update = '0139'
    scope = 'classic_exact_map_qualified_room_item_placements'
    record_count = $accepted.Count
    records = $accepted
}
$catalog | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputPath -Encoding utf8

$report = [ordered]@{
    update = '0139'
    source_rows = $ordinal
    accepted_records = $accepted.Count
    visible_records = @($accepted | Where-Object { $_.visibility -eq 'visible' }).Count
    hidden_records = @($accepted | Where-Object { $_.visibility -eq 'hidden_searchable' }).Count
    rejected_records = $rejected.Count
    rejected = $rejected
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding utf8

Write-Output "Accepted: $($accepted.Count)"
Write-Output "Rejected: $($rejected.Count)"
Write-Output "Output: $outputPath"
Write-Output "Report: $reportPath"
