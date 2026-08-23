param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$sourceRoot = Join-Path $RepositoryRoot 'ImportedDatabases\OmniVerseReference\CodeUpdate0046\OmniVerseCodexApi\wcc\rooms'
$outputPath = Join-Path $RepositoryRoot 'Librarium\Objects\rooms\wcc_room_environment_records_0139.json'

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Decoded WCC room source was not found: $sourceRoot"
}

$records = foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -File -Filter '*.json' | Sort-Object Name) {
    $room = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ([int]$room.map_id -le 0 -or [int]$room.room_number -le 0) {
        continue
    }

    [ordered]@{
        id = "room.map$($room.map_id).$($room.room_number)"
        map_number = [int]$room.map_id
        room_number = [int]$room.room_number
        room_type = [int]$room.room_type
        light_level = [int]$room.light_level
        room_lair_id = [int]$room.monster_type
        attributes = [int]$room.attributes
        death_room_id = [int]$room.death_room_id
        gang_house = [int]$room.gang_house
        respawn_minutes = [int]$room.respawn_minutes
        max_area_cap = [int]$room.max_area_cap
        linked_spawn_room = [int]$room.linked_spawn_room
        source_classification = 'decoded_omniverse_wcc_room_record'
        evidence_path = ('ImportedDatabases/OmniVerseReference/CodeUpdate0046/OmniVerseCodexApi/wcc/rooms/' + $file.Name)
    }
}

$document = [ordered]@{
    authority = 'Librarium'
    world_id = 'Classic'
    family = 'room_environment'
    classification = 'decoded_wcc_room_environment_promotion'
    source = 'OmniVerse WCC room decode'
    record_count = @($records).Count
    records = @($records)
}

$json = $document | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($outputPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output "Promoted $(@($records).Count) decoded WCC room environment records to $outputPath"
