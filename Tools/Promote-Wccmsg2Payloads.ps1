param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$sourcePath = Join-Path $ProjectRoot 'ImportedDatabases/WCCDefaultInstall/wcc default/wccmsg2.dat'
$outputPath = Join-Path $ProjectRoot 'Librarium/Objects/native_message_payloads/native_message_payload_records.json'
$dataStartOffset = 0x4000

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Required WCC default-install evidence is missing: $sourcePath"
}

$raw = [IO.File]::ReadAllBytes($sourcePath)
$hasher = [Security.Cryptography.SHA256]::Create()
try {
    $sha = ([BitConverter]::ToString($hasher.ComputeHash($raw)) -replace '-', '').ToLowerInvariant()
}
finally {
    $hasher.Dispose()
}

$encoding = [Text.Encoding]::GetEncoding(437)
$physical = [Collections.Generic.List[object]]::new()
$dataEndOffsetExclusive = 0x127000
for ($offset = $dataStartOffset; $offset -lt $dataEndOffsetExclusive - 258; $offset++) {
    # WCCMSG2 logical payload record: 01 00, little-endian message ID, 00 00,
    # then a NUL-terminated CP437 payload inside the bounded physical record.
    if ($raw[$offset] -ne 1 -or $raw[$offset + 1] -ne 0 -or
        $raw[$offset + 4] -ne 0 -or $raw[$offset + 5] -ne 0) {
        continue
    }

    $messageId = [BitConverter]::ToUInt16($raw, $offset + 2)
    if ($messageId -le 0 -or $messageId -gt 10000) {
        continue
    }

    $trailerValid = $true
    foreach ($trailerIndex in 252..257) {
        if ($raw[$offset + $trailerIndex] -ne 0) {
            $trailerValid = $false
            break
        }
    }
    if (-not $trailerValid) {
        continue
    }

    $slots = [Collections.Generic.List[string]]::new()
    foreach ($slotOffset in 6, 88, 170) {
        $slot = $raw[($offset + $slotOffset)..($offset + $slotOffset + 81)]
        $nul = [Array]::IndexOf($slot, [byte]0)
        if ($nul -lt 0) {
            $slots.Clear()
            break
        }
        $slots.Add($encoding.GetString($slot, 0, $nul).TrimEnd())
    }
    if ($slots.Count -ne 3) {
        continue
    }

    $physical.Add([pscustomobject]@{
        MessageId = [int]$messageId
        HeaderOffset = [long]$offset
        Slot1 = $slots[0]
        Slot2 = $slots[1]
        Slot3 = $slots[2]
    })
}

$records = [Collections.Generic.List[object]]::new()
$variantSeparator = [char]31
foreach ($group in ($physical | Group-Object MessageId | Sort-Object { [int]$_.Name })) {
    $variants = @($group.Group | ForEach-Object {
        "$($_.Slot1)$variantSeparator$($_.Slot2)$variantSeparator$($_.Slot3)"
    } | Select-Object -Unique)
    if ($variants.Count -ne 1) {
        $details = $group.Group | Select-Object HeaderOffset, Slot1, Slot2, Slot3 | ConvertTo-Json -Compress
        throw "WCCMSG2 message ID $($group.Name) has conflicting duplicate payloads: $details"
    }
    $first = $group.Group | Sort-Object HeaderOffset | Select-Object -First 1
    $records.Add([ordered]@{
        id = "native_message_payload.$($first.MessageId)"
        object_type = 'native_message_payload'
        world_scope = @('classic')
        message_id = $first.MessageId
        player_payload = $first.Slot1
        secondary_payload = $first.Slot2
        tertiary_payload = $first.Slot3
        player_payload_is_empty = [string]::IsNullOrEmpty($first.Slot1)
        header_offset = $first.HeaderOffset
        slot_size = 82
        slot_offsets = @(6, 88, 170)
        physical_record_size = 258
        physical_occurrence_count = $group.Count
        physical_header_offsets = @($group.Group | Sort-Object HeaderOffset | Select-Object -ExpandProperty HeaderOffset)
        record_header = '01 00 + uint16_le_message_id + 00 00'
        encoding = 'cp437'
        evidence_state = 'verified_native_runtime'
        classic_lock = 'evidence_backed_immutable'
    })
}

if ($physical.Count -ne 3887 -or $records.Count -ne 3869) {
    throw "Expected 3,887 physical / 3,869 logical WCCMSG2 payloads; found $($physical.Count) / $($records.Count)."
}

$document = [ordered]@{
    schema = 'majormud.librarium.native-message-payloads.v1'
    source = [ordered]@{
        file = 'ImportedDatabases/WCCDefaultInstall/wcc default/wccmsg2.dat'
        size = $raw.Length
        sha256 = $sha
        data_start_offset = $dataStartOffset
        data_end_offset_exclusive = $dataEndOffsetExclusive
    }
    physical_record_count = $physical.Count
    logical_message_count = $records.Count
    duplicate_identical_record_count = $physical.Count - $records.Count
    records = @($records)
}

$outputDirectory = Split-Path -Parent $outputPath
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
[IO.File]::WriteAllText(
    $outputPath,
    (($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))

Write-Host "Promoted $($records.Count) logical WCCMSG2 payloads ($($physical.Count) physical records) to $outputPath"
